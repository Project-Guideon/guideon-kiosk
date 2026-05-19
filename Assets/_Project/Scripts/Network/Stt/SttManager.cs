using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guideon.Audio;
using Guideon.Chat;
using Guideon.Core;
using Guideon.Network.Models;
using NativeWebSocket;
using Newtonsoft.Json;
using UnityEngine;

namespace Guideon.Network.Stt
{
    /// <summary>
    /// STT WebSocket 연결/송수신 담당.
    /// 탭 시작 → 마이크 캡처 → binary PCM 전송 → VAD 무음 감지 → 자동 종료.
    /// final 텍스트 수신 시 ChatManager.SendMessageAsync 자동 호출.
    /// </summary>
    public class SttManager : MonoSingleton<SttManager>
    {
        public event Action<bool> OnRecordingStateChanged; // true=시작, false=종료
        public event Action<float> OnRmsLevel;             // 파형 위젯용

        public bool IsRecording { get; private set; }

        private WebSocket _ws;
        private MicrophoneCapture _mic;
        private VoiceActivityDetector _vad;
        private bool _waitingForDone;
        private bool _sentUserBubble;    // 이번 세션에서 유저 버블을 이미 표시했는지
        private string _lastTranscript;  // stt_interim/stt_final 중 마지막으로 받은 텍스트

        private const float SilenceThreshold = 0.01f;

        protected override void OnInitialize()
        {
            _mic = gameObject.AddComponent<MicrophoneCapture>();
            Debug.Log("[SttManager] 초기화 완료");
        }

        // ── 공개 API ──────────────────────────────────────

        public async UniTask StartAsync()
        {
            if (IsRecording)
            {
                Debug.LogWarning("[SttManager] 이미 녹음 중");
                return;
            }

            if (!ChatManager.HasInstance)
            {
                Debug.LogError("[SttManager] ChatManager 없음");
                return;
            }
            if (!ChatManager.Instance.HasActiveSession)
            {
                bool ok = await ChatManager.Instance.CreateSessionAsync();
                if (!ok) return;
            }

            string sessionId = ChatManager.Instance.CurrentSessionId;
            if (!await ConnectAsync(sessionId)) return;

            StartMic();
            IsRecording = true;
            _waitingForDone = false;
            _sentUserBubble = false;
            _lastTranscript = null;
            OnRecordingStateChanged?.Invoke(true);
            EventBus.Publish(new MascotStateEvent { State = MascotState.Listening });
            Debug.Log("[SttManager] 녹음 시작");
        }

        public void Stop()
        {
            if (!IsRecording) return;
            StopCaptureAndClose(sendStop: true).Forget();
        }

        // ── 연결 ──────────────────────────────────────────

        private async UniTask<bool> ConnectAsync(string sessionId)
        {
            var cfg = ConfigManager.Instance.Config;
            string url = $"{cfg.server.wsUrl}/kiosk/stt?sessionId={sessionId}&languageCode={cfg.kiosk.language}";

            var headers = new Dictionary<string, string>
            {
                { "X-Device-Id",    cfg.device.id },
                { "X-Device-Token", cfg.device.token }
            };

            _ws = new WebSocket(url, headers);
            _ws.OnMessage += OnWsMessage;
            _ws.OnError   += OnWsError;
            _ws.OnClose   += OnWsClose;

            Debug.Log($"[SttManager] 연결 시도 — {url}");

            var tcs = new UniTaskCompletionSource<bool>();
            WebSocketOpenEventHandler  onOpen = () => tcs.TrySetResult(true);
            WebSocketErrorEventHandler onErr  = _ => tcs.TrySetResult(false);

            _ws.OnOpen  += onOpen;
            _ws.OnError += onErr;

            // Connect()는 Task를 반환하지만 완료 시점은 OnOpen/OnError로 받음
            _ = _ws.Connect();

            // 최대 5초 대기
            bool connected;
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                try { connected = await tcs.Task.AttachExternalCancellation(cts.Token); }
                catch (OperationCanceledException) { connected = false; }
            }

            _ws.OnOpen  -= onOpen;
            _ws.OnError -= onErr;

            if (!connected)
            {
                Debug.LogError("[SttManager] 연결 실패 또는 타임아웃");
                _ws = null;
                return false;
            }

            Debug.Log("[SttManager] 연결 성공");
            return true;
        }

        // ── 마이크 / VAD ──────────────────────────────────

        private void StartMic()
        {
            var cfg = ConfigManager.Instance.Config.kiosk;

            _vad = new VoiceActivityDetector(SilenceThreshold, cfg.sttSilenceTimeoutMs);
            _vad.OnSilenceTimeout += OnVadSilenceTimeout;
            _vad.Begin();

            _mic.OnAudioFrame += OnAudioFrame;
            _mic.OnRmsLevel   += OnMicRms;
            _mic.StartCapture(cfg.sttSampleRate, cfg.sttFrameMs);
        }

        private void StopMic()
        {
            _mic.OnAudioFrame -= OnAudioFrame;
            _mic.OnRmsLevel   -= OnMicRms;
            _mic.StopCapture();

            if (_vad != null)
            {
                _vad.OnSilenceTimeout -= OnVadSilenceTimeout;
                _vad.Stop();
                _vad = null;
            }
        }

        private void OnMicRms(float rms)
        {
            _vad?.Feed(rms, Time.deltaTime);
            OnRmsLevel?.Invoke(rms);

            if (IdleTimeoutManager.HasInstance)
                IdleTimeoutManager.Instance.NotifyInteraction();
        }

        private void OnAudioFrame(byte[] pcm)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            _ws.Send(pcm);
        }

        private void OnVadSilenceTimeout()
        {
            StopCaptureAndClose(sendStop: true).Forget();
        }

        // ── WebSocket 콜백 ────────────────────────────────

        private void OnWsMessage(byte[] data)
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            SttMessage msg;
            try { msg = JsonConvert.DeserializeObject<SttMessage>(json); }
            catch { Debug.LogWarning($"[SttManager] JSON 파싱 실패: {json}"); return; }

            if (IdleTimeoutManager.HasInstance)
                IdleTimeoutManager.Instance.NotifyInteraction();

            switch (msg.Type)
            {
                case "stt_interim":
                    _lastTranscript = msg.Text;
                    EventBus.Publish(new SttResultEvent { Transcript = msg.Text, IsFinal = false });
                    break;

                case "stt_final":
                    _lastTranscript = msg.Text;
                    Debug.Log($"[SttManager] stt_final — '{msg.Text}'");
                    ShowUserBubble(msg.Text);
                    break;

                case "final_text":
                    Debug.Log($"[SttManager] final_text — '{msg.Answer}'");
                    HandleAiResponse(msg.Answer);
                    break;

                case "status":
                    HandleStatus(msg.Stage);
                    break;

                case "error":
                    Debug.LogWarning($"[SttManager] 서버 오류 — {msg.Code}: {msg.ErrorMessage}");
                    _waitingForDone = false;
                    break;

                case "done":
                    Debug.Log("[SttManager] done 수신");
                    _waitingForDone = false;
                    break;
            }
        }

        private void ShowUserBubble(string transcript)
        {
            if (_sentUserBubble) return;
            if (string.IsNullOrWhiteSpace(transcript)) return;
            _sentUserBubble = true;
            EventBus.Publish(new SttResultEvent { Transcript = transcript, IsFinal = true });
            EventBus.Publish(new MascotStateEvent { State = MascotState.Thinking });
        }

        private void HandleAiResponse(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer)) return;
            EventBus.Publish(new ChatResponseEvent
            {
                Answer    = answer,
                SessionId = ChatManager.HasInstance ? ChatManager.Instance.CurrentSessionId : null,
                Emotion   = "default",
                Language  = ConfigManager.Instance.Config.kiosk.language,
            });
            EventBus.Publish(new MascotStateEvent { State = MascotState.Speaking });
        }

        private void HandleStatus(string stage)
        {
            switch (stage)
            {
                case "stt_start":
                    EventBus.Publish(new MascotStateEvent { State = MascotState.Listening });
                    break;
                case "stt_done":
                case "graph_start":
                    EventBus.Publish(new MascotStateEvent { State = MascotState.Thinking });
                    break;
                case "tts_start":
                    EventBus.Publish(new MascotStateEvent { State = MascotState.Speaking });
                    break;
            }
        }

        private void OnWsError(string error)
        {
            Debug.LogError($"[SttManager] WS 오류: {error}");
            CleanupState();
        }

        private void OnWsClose(WebSocketCloseCode code)
        {
            Debug.Log($"[SttManager] WS 종료 — {code}");
            // 서버가 done 없이 연결을 닫은 경우 마지막 transcript로 폴백
            if (_waitingForDone)
            {
                _waitingForDone = false;
                ShowUserBubble(_lastTranscript);
            }
            CleanupState();
        }

        // ── 종료 흐름 ─────────────────────────────────────

        private async UniTaskVoid StopCaptureAndClose(bool sendStop)
        {
            StopMic();

            if (sendStop && _ws != null && _ws.State == WebSocketState.Open)
            {
                await SendJsonAsync(new SttStopMessage());
                Debug.Log("[SttManager] stop 메시지 전송");
                _waitingForDone = true;

                // done 대기 최대 30초 — 서버가 done 없이 닫으면 OnWsClose가 폴백 처리
                float waited = 0f;
                while (_waitingForDone && waited < 30f)
                {
                    await UniTask.Yield();
                    waited += Time.deltaTime;
                }

                if (_waitingForDone)
                {
                    _waitingForDone = false;
                    Debug.LogWarning("[SttManager] done 타임아웃 — 마지막 transcript로 폴백");
                    ShowUserBubble(_lastTranscript);
                }
            }

            if (_ws != null)
            {
                await _ws.Close();
                _ws = null;
            }

            CleanupState();
        }

        private void CleanupState()
        {
            if (!IsRecording) return;
            IsRecording = false;
            OnRecordingStateChanged?.Invoke(false);
            EventBus.Publish(new MascotStateEvent { State = MascotState.Idle });
            Debug.Log("[SttManager] 녹음 종료 완료");
        }


        // ── 유틸 ──────────────────────────────────────────

        private async UniTask SendJsonAsync<T>(T msg)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            string json = JsonConvert.SerializeObject(msg);
            await _ws.SendText(json);
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
        }

        protected override void OnDestroy()
        {
            if (IsRecording) Stop();
            base.OnDestroy();
        }
    }
}
