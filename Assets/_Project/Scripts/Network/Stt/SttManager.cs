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
        private bool _waitingForFinal;
        private bool _sentToChat;        // 이번 세션에서 이미 ChatManager 호출했는지
        private string _lastTranscript;  // partial/final 중 마지막으로 받은 텍스트

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

            await SendJsonAsync(new SttStartMessage
            {
                Language = ConfigManager.Instance.Config.kiosk.language,
                SampleRate = ConfigManager.Instance.Config.kiosk.sttSampleRate
            });
            Debug.Log("[SttManager] start 메시지 전송");

            StartMic();
            IsRecording = true;
            _waitingForFinal = false;
            _sentToChat = false;
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
            string url = $"{cfg.server.wsUrl}/kiosk/stt?sessionId={sessionId}";

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

            if (!string.IsNullOrEmpty(msg.Transcript))
                _lastTranscript = msg.Transcript;

            if (msg.Type != "final")
                EventBus.Publish(new SttResultEvent { Transcript = msg.Transcript, IsFinal = false });

            if (IdleTimeoutManager.HasInstance)
                IdleTimeoutManager.Instance.NotifyInteraction();

            if (msg.Type == "final")
            {
                Debug.Log($"[SttManager] final 수신 — '{msg.Transcript}'");
                _waitingForFinal = false;
                TrySendToChat(msg.Transcript);
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
            // 서버가 final 없이 연결을 닫은 경우 마지막 transcript로 폴백
            if (_waitingForFinal)
            {
                _waitingForFinal = false;
                TrySendToChat(_lastTranscript);
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
                _waitingForFinal = true;

                // final 대기 최대 5초 — 서버가 final 없이 닫으면 OnWsClose가 폴백 처리
                float waited = 0f;
                while (_waitingForFinal && waited < 5f)
                {
                    await UniTask.Yield();
                    waited += Time.deltaTime;
                }

                // 5초 타임아웃: 서버가 final도 안 보내고 연결도 안 닫은 경우
                if (_waitingForFinal)
                {
                    _waitingForFinal = false;
#if UNITY_EDITOR
                    const string debugText = "경복궁 가는 길 알려줘";
                    Debug.LogWarning($"[SttManager] DEBUG: 서버 응답 없음 — 디버그 텍스트로 강제 전송 '{debugText}'");
                    TrySendToChat(debugText);
#else
                    Debug.LogWarning("[SttManager] final 타임아웃 — 마지막 transcript로 폴백");
                    TrySendToChat(_lastTranscript);
#endif
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

        private void TrySendToChat(string transcript)
        {
            if (_sentToChat) return;
            if (string.IsNullOrWhiteSpace(transcript))
            {
                Debug.LogWarning("[SttManager] 전송할 텍스트 없음 — 무시");
                return;
            }
            _sentToChat = true;
            Debug.Log($"[SttManager] ChatManager 전송 — '{transcript}'");
            EventBus.Publish(new SttResultEvent { Transcript = transcript, IsFinal = true });
            ChatManager.Instance.SendMessageAsync(transcript).Forget();
            EventBus.Publish(new MascotStateEvent { State = MascotState.Thinking });
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
