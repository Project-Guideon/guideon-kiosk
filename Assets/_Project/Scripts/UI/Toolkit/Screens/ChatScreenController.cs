using Guideon.Chat;
using Guideon.Core;
using Guideon.Network.Stt;
using Guideon.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI
{
    public class ChatScreenController : PanelControllerBase
    {
        private VisualElement _bubbleList;
        private ScrollView _chatScroll;
        private VisualElement _thinkingGroup;
        private Button _micButton;
        private VisualElement _waveformContainer;
        private Label _speechText;
        private Label _timeoutText;
        private VisualElement _timeoutFill;

        private WaveformElement _waveform;
        private IVisualElementScheduledItem _waveformJob;
        private float _rmsLevel;

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Chat;
        }

        protected override void OnBindUI()
        {
            _bubbleList       = Q("bubble-list");
            _chatScroll       = Q<ScrollView>("chat-scroll");
            _thinkingGroup    = Q("thinking-group");
            _micButton        = Q<Button>("mic-button");
            _waveformContainer = Q("waveform-container");
            _speechText       = Q<Label>("speech-text");
            _timeoutText      = Q<Label>("timeout-text");
            _timeoutFill      = Q("timeout-fill");

            _waveform = new WaveformElement(_waveformContainer);

            _micButton?.RegisterCallback<ClickEvent>(_ => OnMicClicked());

            SetThinking(false);
            SetRecording(false);
            ClearBubbles();

            _waveformJob = Root?.schedule.Execute(() => _waveform?.SetLevel(_rmsLevel)).Every(32);
        }

        protected override void SubscribeEvents()
        {
            EventBus.Subscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Subscribe<SttResultEvent>(OnSttResult);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged += OnRecordingStateChanged;
        }

        protected override void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Unsubscribe<SttResultEvent>(OnSttResult);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged -= OnRecordingStateChanged;
        }

        protected override void OnDisable()
        {
            _waveformJob?.Pause();
            if (SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel -= OnRmsLevel;
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _waveformJob?.Resume();
        }

        // ── 마이크 ─────────────────────────────────────────────────────────

        private void OnMicClicked()
        {
            if (!SttManager.HasInstance) return;
            if (SttManager.Instance.IsRecording)
                SttManager.Instance.Stop();
            else
                SttManager.Instance.StartAsync().Forget();
        }

        private void OnRecordingStateChanged(bool recording)
        {
            SetRecording(recording);
        }

        public void SetRecording(bool recording)
        {
            _waveform?.SetVisible(recording);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel -= OnRmsLevel;
            if (recording && SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel += OnRmsLevel;
        }

        private void OnRmsLevel(float rms) => _rmsLevel = rms;

        // ── STT / AI 응답 ──────────────────────────────────────────────────

        private void OnSttResult(SttResultEvent e)
        {
            if (!e.IsFinal) return;
            AppendBubble(e.Transcript, isUser: true);
            SetThinking(true);
        }

        private void OnChatResponse(ChatResponseEvent e)
        {
            SetThinking(false);
            AppendBubble(e.Answer, isUser: false);
            if (_speechText != null)
                _speechText.text = e.Answer.Length > 60 ? e.Answer[..60] + "..." : e.Answer;
        }

        // ── 버블 ───────────────────────────────────────────────────────────

        private void AppendBubble(string message, bool isUser)
        {
            if (_bubbleList == null) return;

            var container = new VisualElement();
            container.style.marginBottom = 28;

            if (isUser)
            {
                container.AddToClassList("bubble-user");
                var label = new Label(message);
                label.AddToClassList("bubble__text");
                label.style.color = new StyleColor(GuideonColors.Ink);
                container.Add(label);
            }
            else
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;

                var avatar = new VisualElement();
                avatar.AddToClassList("ai-avatar");
                avatar.style.marginRight = 18;
                var avatarLabel = new Label("G");
                avatarLabel.AddToClassList("ai-avatar__text");
                avatar.Add(avatarLabel);

                var bubble = new VisualElement();
                bubble.AddToClassList("bubble-ai");
                bubble.style.flexGrow = 1;
                var label = new Label(message);
                label.AddToClassList("bubble__text");
                bubble.Add(label);

                row.Add(avatar);
                row.Add(bubble);
                container.Add(row);
            }

            _bubbleList.Add(container);
            Root?.schedule.Execute(ScrollToBottom).ExecuteLater(50);
        }

        private void ScrollToBottom()
        {
            if (_chatScroll != null)
                _chatScroll.verticalScroller.value = _chatScroll.verticalScroller.highValue;
        }

        private void ClearBubbles()
        {
            _bubbleList?.Clear();
        }

        private void SetThinking(bool on)
        {
            if (_thinkingGroup == null) return;
            _thinkingGroup.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
