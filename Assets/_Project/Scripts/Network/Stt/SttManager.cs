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
    /// final 텍스트 수신 시 AI 응답을 ChatResponseEvent로 발행.
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

        private float _recordingElapsed;
        private float _maxRecordingSec;

        // ── 이탈 플래그 ──────────────────────────────────
        // 종료 버튼 / 무발화 자동 복귀 시 TtsDone 자동 재시작 방지
        private bool _exiting;

        // 답변 수신 여부 — done-타임아웃 시 에러 메시지 분기용
        private bool _awaitingAnswer;

        protected override void OnInitialize()
        {
            _mic = gameObject.AddComponent<MicrophoneCapture>();

            // TtsManager를 같은 부트스트랩 타이밍에 생성
            if (!TtsManager.HasInstance)
            {
                var go = new GameObject("TtsManager");
                go.AddComponent<TtsManager>();
            }

            EventBus.Subscribe<TtsDoneEvent>(OnTtsDone);
            EventBus.Subscribe<ChatExitRequestedEvent>(OnChatExitRequested);
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

            _exiting = false;

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

            if (_exiting) return; // 연결 중 이탈 요청이 들어온 경우

            // 이전 TTS 재생 중이면 즉시 abort하고 새 세션 시작
            if (TtsManager.HasInstance) TtsManager.Instance.BeginSession(sessionId);

            StartMic();
            IsRecording = true;
            _recordingElapsed = 0f;
            _maxRecordingSec  = ConfigManager.Instance.Config.kiosk.sttMaxRecordingSeconds;
            _waitingForDone = false;
            _sentUserBubble = false;
            _lastTranscript = null;

            _awaitingAnswer = false;

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
                PublishNotice("연결에 문제가 있어요. 잠시 후 다시 시도해 주세요.");
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

            _vad = new VoiceActivityDetector(
                cfg.sttVadThreshold,
                cfg.sttSilenceTimeoutMs,
                cfg.sttNoSpeechExitSeconds);

            _vad.OnSilenceTimeout  += OnVadSilenceTimeout;
            _vad.OnNoSpeechTimeout += OnVadNoSpeechTimeout;
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
                _vad.OnSilenceTimeout  -= OnVadSilenceTimeout;
                _vad.OnNoSpeechTimeout -= OnVadNoSpeechTimeout;
                _vad.Stop();
                _vad = null;
            }
        }

        private void OnMicRms(float rms)
        {
            _vad?.Feed(rms, Time.deltaTime);
            OnRmsLevel?.Invoke(rms);

            // 실제 음성일 때만 idle 타임아웃 리셋 (마이크가 항상 열려 있어도 120초 idle이 정상 작동)
            if (rms >= ConfigManager.Instance.Config.kiosk.sttVadThreshold)
            {
                if (IdleTimeoutManager.HasInstance)
                    IdleTimeoutManager.Instance.NotifyInteraction();
            }
        }

        private void OnAudioFrame(byte[] pcm)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            _ws.Send(pcm);
        }

        private void OnVadSilenceTimeout()
        {
            if (!IsRecording) return; // WS 외부 종료 후 VAD 뒤늦게 fire 방지
            StopCaptureAndClose(sendStop: true).Forget();
        }

        private void OnVadNoSpeechTimeout()
        {
            if (!IsRecording) return; // WS 외부 종료 후 VAD 뒤늦게 fire 방지
            Debug.Log("[SttManager] 무발화 종료 → Idle 복귀");
            _exiting = true;
            StopCaptureAndClose(sendStop: false).Forget();
            // MainSceneController가 처리하도록 이벤트 발행
            EventBus.Publish(new ChatExitRequestedEvent());
        }

        // ── 종료 버튼 / 이탈 ─────────────────────────────

        private void OnChatExitRequested(ChatExitRequestedEvent _)
        {
            if (_exiting) return; // 이미 OnVadNoSpeechTimeout이 처리 중 — 이중 StopCaptureAndClose 방지
            _exiting = true;
            if (IsRecording) Stop();
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

                case "tts_chunk":
                    if (TtsManager.HasInstance)
                        TtsManager.Instance.EnqueueChunk(msg.Seq, msg.AudioFormat, msg.AudioB64);
                    break;

                case "error":
                    Debug.LogWarning($"[SttManager] 서버 오류 — {msg.Code}: {msg.ErrorMessage}");
                    _waitingForDone   = false;
                    _awaitingAnswer   = false;
                    PublishNotice("죄송해요, 답변을 가져오지 못했어요. 다시 말씀해 주세요.", isError: true);
                    break;

                case "done":
                    Debug.Log("[SttManager] done 수신");
                    _waitingForDone = false;
                    if (TtsManager.HasInstance) TtsManager.Instance.MarkServerDone();
                    break;
            }
        }

        private void ShowUserBubble(string transcript)
        {
            if (_sentUserBubble) return;
            if (string.IsNullOrWhiteSpace(transcript)) return;
            _sentUserBubble = true;
            _awaitingAnswer = true;

            // tts_chunk가 final_text보다 먼저 도착해 재생이 앞서는 것을 방지
            // ReleasePlayback은 ChatScreenController가 AI 버블 렌더 후 호출
            if (TtsManager.HasInstance) TtsManager.Instance.HoldPlayback();

            EventBus.Publish(new SttResultEvent { Transcript = transcript, IsFinal = true });
            EventBus.Publish(new MascotStateEvent { State = MascotState.Thinking });
        }

        private void HandleAiResponse(string answer)
        {
            if (string.IsNullOrWhiteSpace(answer)) return;
            _awaitingAnswer = false;
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
            _awaitingAnswer = false;
            PublishNotice("네트워크 오류가 발생했어요. 다시 시도해 주세요.", isError: true);
            CleanupState();
        }

        private void OnWsClose(WebSocketCloseCode code)
        {
            Debug.Log($"[SttManager] WS 종료 — {code}");
            if (_waitingForDone)
            {
                _waitingForDone = false;
                ShowUserBubble(_lastTranscript);
            }
            // WS가 외부에서 닫혀도 마이크를 확실히 정리
            StopMic();
            // done 없이 WS가 닫힌 경우 TTS를 강제 완료해 TtsDoneEvent 보장
            if (TtsManager.HasInstance) TtsManager.Instance.MarkServerDone();
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
                    if (_awaitingAnswer)
                    {
                        _awaitingAnswer = false;
                        PublishNotice("응답이 너무 오래 걸려요. 다시 말씀해 주세요.", isError: true);
                    }
                    else
                    {
                        ShowUserBubble(_lastTranscript);
                    }
                    // done 없이 타임아웃 시 TTS 강제 완료해 TtsDoneEvent 보장
                    if (TtsManager.HasInstance) TtsManager.Instance.MarkServerDone();
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
            _awaitingAnswer = false;
            OnRecordingStateChanged?.Invoke(false);
            if (!_exiting) // 종료 전환 중엔 힌트 업데이트 불필요 — "말씀해 보세요" 노출 방지
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

        private static void PublishNotice(string message, bool isError = false)
        {
            EventBus.Publish(new ChatNoticeEvent
            {
                Message = message,
                Type    = isError ? ChatNoticeType.Error : ChatNoticeType.Info
            });
        }

        private void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
            // 최대 녹음 시간 초과
            if (IsRecording && _maxRecordingSec > 0f)
            {
                _recordingElapsed += Time.deltaTime;
                if (_recordingElapsed >= _maxRecordingSec)
                {
                    _recordingElapsed = 0f;
                    Debug.Log("[SttManager] 최대 녹음 시간 초과 → 자동 stop");
                    StopCaptureAndClose(sendStop: true).Forget();
                }
            }

        }

        private void OnTtsDone(TtsDoneEvent _)
        {
            // 이탈 중이 아닐 때만 자동 재시작
            if (!_exiting && !IsRecording)
                StartAsync().Forget();
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<TtsDoneEvent>(OnTtsDone);
            EventBus.Unsubscribe<ChatExitRequestedEvent>(OnChatExitRequested);
            if (IsRecording) Stop();
            base.OnDestroy();
        }
    }
}
