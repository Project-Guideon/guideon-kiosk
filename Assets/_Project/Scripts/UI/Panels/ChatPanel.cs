using Cysharp.Threading.Tasks;
using Guideon.Chat;
using Guideon.Core;
using Guideon.Network.Stt;
using UnityEngine;
using UnityEngine.UI;

namespace Guideon.UI
{
    /// <summary>
    /// 사용자/AI 메시지를 버블로 누적 표시하는 채팅 화면.
    /// 마이크 버튼 탭 → SttManager 시작/중지. partial 수신 시 파형만 반응,
    /// final 수신 시 SttManager가 ChatManager를 직접 호출해 AI 응답을 받는다.
    /// </summary>
    public class ChatPanel : MonoBehaviour
    {
        [Header("Bubble List")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private MessageBubble _userBubblePrefab;
        [SerializeField] private MessageBubble _aiBubblePrefab;

        [Header("Thinking Indicator")]
        [SerializeField] private GameObject _thinkingGroup;

        [Header("Voice Input")]
        [SerializeField] private Button _micButton;
        [SerializeField] private Image _micIcon;
        [SerializeField] private AudioWaveformWidget _waveformWidget;

        [Header("Mic Icon Colors")]
        [SerializeField] private Color _micIdleColor   = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color _micActiveColor = new Color(1f, 0.4f, 0.1f);

        private void Awake()
        {
            EnsureContentLayout();
        }

        private void EnsureContentLayout()
        {
            if (_contentRoot == null) return;

            var vlg = _contentRoot.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = _contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.childAlignment         = TextAnchor.UpperLeft;
                vlg.childControlWidth      = true;
                vlg.childControlHeight     = true;
                vlg.childForceExpandWidth  = true;
                vlg.childForceExpandHeight = false;
                vlg.spacing = 12f;
                vlg.padding = new RectOffset(16, 16, 16, 16);
            }

            var csf = _contentRoot.GetComponent<ContentSizeFitter>();
            if (csf == null)
            {
                csf = _contentRoot.gameObject.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Subscribe<SttResultEvent>(OnSttResult);

            if (_micButton != null) _micButton.onClick.AddListener(OnMicClicked);

            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged += OnRecordingStateChanged;

            ClearBubbles();
            SetThinking(false);
            SetRecording(false);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Unsubscribe<SttResultEvent>(OnSttResult);

            if (_micButton != null) _micButton.onClick.RemoveListener(OnMicClicked);

            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged -= OnRecordingStateChanged;
        }

        // ── 마이크 입력 ───────────────────────────────────

        private void OnMicClicked()
        {
            if (!SttManager.HasInstance)
            {
                Debug.LogError("[ChatPanel] SttManager 없음");
                return;
            }

            if (SttManager.Instance.IsRecording)
                SttManager.Instance.Stop();
            else
                SttManager.Instance.StartAsync().Forget();
        }

        private void OnRecordingStateChanged(bool recording)
        {
            SetRecording(recording);
        }

        // ── STT 결과 ──────────────────────────────────────

        private void OnSttResult(SttResultEvent e)
        {
            if (!e.IsFinal) return;
            // final 텍스트는 SttManager가 ChatManager로 이미 전달.
            // ChatPanel은 사용자 버블 표시 + thinking 인디케이터 ON.
            AppendBubble(e.Transcript, MessageBubble.Sender.User);
            SetThinking(true);
        }

        // ── AI 응답 ───────────────────────────────────────

        private void OnChatResponse(ChatResponseEvent e)
        {
            SetThinking(false);
            AppendBubble(e.Answer, MessageBubble.Sender.Ai);
        }

        // ── UI 헬퍼 ───────────────────────────────────────

        private void AppendBubble(string message, MessageBubble.Sender sender)
        {
            if (_contentRoot == null) return;

            var prefab = sender == MessageBubble.Sender.User ? _userBubblePrefab : _aiBubblePrefab;
            if (prefab == null)
            {
                Debug.LogError($"[ChatPanel] {sender} prefab이 null입니다!");
                return;
            }

            var bubble = Instantiate(prefab, _contentRoot);
            bubble.SetMessage(message, sender);

            var csf = bubble.GetComponent<ContentSizeFitter>();
            if (csf == null)
            {
                csf = bubble.gameObject.AddComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRoot);
            ScrollToBottomNextFrame().Forget();
        }

        private void ClearBubbles()
        {
            if (_contentRoot == null) return;
            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
                Destroy(_contentRoot.GetChild(i).gameObject);
        }

        public void SetRecording(bool recording)
        {
            _waveformWidget?.SetActive(recording);
            if (_micIcon != null)
                _micIcon.color = recording ? _micActiveColor : _micIdleColor;

            if (SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel -= OnRmsLevel;
            if (recording && SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel += OnRmsLevel;
        }

        public void SetWaveformLevel(float level)
        {
            _waveformWidget?.SetAudioLevel(level);
        }

        private void OnRmsLevel(float rms)
        {
            SetWaveformLevel(rms);
        }

        private void SetThinking(bool on)
        {
            if (_thinkingGroup != null) _thinkingGroup.SetActive(on);
        }

        private async UniTaskVoid ScrollToBottomNextFrame()
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, this.GetCancellationTokenOnDestroy());
            if (_scrollRect != null && _scrollRect.content != null)
                _scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
