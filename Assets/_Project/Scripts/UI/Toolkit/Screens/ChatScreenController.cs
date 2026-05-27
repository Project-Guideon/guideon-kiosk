using Cysharp.Threading.Tasks;
using Guideon.Audio;
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
        private Button _endButton;
        private VisualElement _mascotDots;
        private VisualElement[] _dotElements;

        private WaveformElement _waveform;
        private IVisualElementScheduledItem _waveformJob;
        private IVisualElementScheduledItem _dotsAnimJob;
        private float _rmsLevel;
        private int _dotsAnimPhase;

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Chat;
        }

        protected override void OnBindUI()
        {
            _bubbleList        = Q("bubble-list");
            _chatScroll        = Q<ScrollView>("chat-scroll");
            _thinkingGroup     = Q("thinking-group");
            _micButton         = Q<Button>("mic-button");
            _waveformContainer = Q("waveform-container");
            _endButton         = Q<Button>("btn-end");
            _mascotDots        = Q("mascot-emote-dots");

            if (_mascotDots != null)
            {
                _dotElements = new VisualElement[_mascotDots.childCount];
                for (int i = 0; i < _mascotDots.childCount; i++)
                    _dotElements[i] = _mascotDots[i];
            }

            _waveform = new WaveformElement(_waveformContainer);

            _micButton?.RegisterCallback<ClickEvent>(_ => OnMicClicked());
            _endButton?.RegisterCallback<ClickEvent>(_ => OnEndClicked());

            SetThinking(false);
            SetRecording(false);
            SetMascotDotsVisible(false);
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
            StopDotsAnim();
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

        // ── 대화 종료 버튼 ─────────────────────────────────────────────────

        private void OnEndClicked()
        {
            EventBus.Publish(new ChatExitRequestedEvent());
        }

        // ── STT / AI 응답 ──────────────────────────────────────────────────

        private void OnSttResult(SttResultEvent e)
        {
            if (!e.IsFinal) return;
            AppendBubble(e.Transcript, isUser: true);
            SetThinking(true);
            SetMascotDotsVisible(true);
        }

        private void OnChatResponse(ChatResponseEvent e)
        {
            if (TtsManager.HasInstance)
                TtsManager.Instance.HoldPlayback();

            SetThinking(false);
            SetMascotDotsVisible(false);
            AppendBubble(e.Answer, isUser: false);

            Root?.schedule.Execute(() =>
            {
                ScrollToBottom();
                if (TtsManager.HasInstance)
                    TtsManager.Instance.ReleasePlayback();
            }).ExecuteLater(50);
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
                var label = new Label(message);
                label.AddToClassList("bubble__text");
                bubble.Add(label);

                row.Add(avatar);
                row.Add(bubble);
                container.Add(row);
            }

            _bubbleList.Add(container);
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

        // ── 마스코트 타이핑 점 ──────────────────────────────────────────────

        private void SetMascotDotsVisible(bool on)
        {
            if (_mascotDots == null) return;
            _mascotDots.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) StartDotsAnim();
            else StopDotsAnim();
        }

        private void StartDotsAnim()
        {
            _dotsAnimPhase = 0;
            _dotsAnimJob?.Pause();
            _dotsAnimJob = Root?.schedule.Execute(StepDotsAnim).Every(200);
        }

        private void StopDotsAnim()
        {
            _dotsAnimJob?.Pause();
            _dotsAnimJob = null;
            if (_dotElements == null) return;
            foreach (var d in _dotElements)
                d.style.translate = new StyleTranslate(
                    new Translate(new Length(0), new Length(0)));
        }

        private void StepDotsAnim()
        {
            if (_dotElements == null) return;
            int active = _dotsAnimPhase % _dotElements.Length;
            for (int i = 0; i < _dotElements.Length; i++)
            {
                float y = (i == active) ? -10f : 0f;
                _dotElements[i].style.translate = new StyleTranslate(
                    new Translate(new Length(0), new Length(y, LengthUnit.Pixel)));
            }
            _dotsAnimPhase++;
        }
    }
}
