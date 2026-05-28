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
        private ScrollView    _chatScroll;
        private VisualElement _thinkingGroup;
        private Button        _micButton;
        private VisualElement _waveformContainer;
        private Button        _endButton;
        private Label         _micHint;
        private VisualElement _mascotDots;
        private VisualElement[] _dotElements;

        // 마이크 펄스 링
        private VisualElement _pulseRing;
        private IVisualElementScheduledItem _pulseJob;
        private float _pulsePhase;

        private WaveformElement _waveform;
        private IVisualElementScheduledItem _waveformJob;
        private IVisualElementScheduledItem _dotsAnimJob;
        private float _rmsLevel;
        private int   _dotsAnimPhase;

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
            _micHint           = Q<Label>("mic-hint");
            _mascotDots        = Q("mascot-emote-dots");

            // 마이크 버튼에 펄스 링 동적 생성
            if (_micButton != null)
            {
                _pulseRing = new VisualElement();
                _pulseRing.AddToClassList("mic-pulse-ring");
                _micButton.Add(_pulseRing);
            }

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
            SetMicState(MascotState.Idle);

            _waveformJob = Root?.schedule.Execute(() => _waveform?.SetLevel(_rmsLevel)).Every(32);
        }

        protected override void SubscribeEvents()
        {
            EventBus.Subscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Subscribe<SttResultEvent>(OnSttResult);
            EventBus.Subscribe<MascotStateEvent>(OnMascotState);
            EventBus.Subscribe<ChatNoticeEvent>(OnChatNotice);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged += OnRecordingStateChanged;
        }

        protected override void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Unsubscribe<SttResultEvent>(OnSttResult);
            EventBus.Unsubscribe<MascotStateEvent>(OnMascotState);
            EventBus.Unsubscribe<ChatNoticeEvent>(OnChatNotice);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged -= OnRecordingStateChanged;
        }

        protected override void OnDisable()
        {
            _waveformJob?.Pause();
            StopDotsAnim();
            StopPulse();
            if (SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel -= OnRmsLevel;
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _waveformJob?.Resume();
        }

        // ── 마스코트 상태 → UI 동기화 ─────────────────────────

        private void OnMascotState(MascotStateEvent e)
        {
            SetMicState(e.State);
        }

        private void SetMicState(MascotState state)
        {
            if (_micHint == null) return;

            // 힌트 텍스트 & 색상 클래스
            _micHint.RemoveFromClassList("mic-hint--listening");
            _micHint.RemoveFromClassList("mic-hint--thinking");

            switch (state)
            {
                case MascotState.Listening:
                    _micHint.text = "듣고 있어요…";
                    _micHint.AddToClassList("mic-hint--listening");
                    SetMicButtonState(active: true, busy: false);
                    StartPulse();
                    break;

                case MascotState.Thinking:
                    _micHint.text = "답변을 준비하고 있어요…";
                    _micHint.AddToClassList("mic-hint--thinking");
                    SetMicButtonState(active: false, busy: true);
                    StopPulse();
                    break;

                case MascotState.Speaking:
                    _micHint.text = "답변하고 있어요";
                    SetMicButtonState(active: false, busy: true);
                    StopPulse();
                    break;

                default: // Idle, Greeting
                    _micHint.text = "말씀해 보세요";
                    SetMicButtonState(active: false, busy: false);
                    StopPulse();
                    break;
            }
        }

        private void SetMicButtonState(bool active, bool busy)
        {
            if (_micButton == null) return;
            if (active)
            {
                _micButton.AddToClassList("mic-circle--active");
                _micButton.RemoveFromClassList("mic-circle--busy");
            }
            else if (busy)
            {
                _micButton.RemoveFromClassList("mic-circle--active");
                _micButton.AddToClassList("mic-circle--busy");
            }
            else
            {
                _micButton.RemoveFromClassList("mic-circle--active");
                _micButton.RemoveFromClassList("mic-circle--busy");
            }
        }

        // ── 마이크 펄스 ────────────────────────────────────────

        private void StartPulse()
        {
            if (_pulseRing == null) return;
            _pulseRing.AddToClassList("mic-pulse-ring--visible");
            _pulsePhase = 0f;
            _pulseJob?.Pause();
            _pulseJob = Root?.schedule.Execute(StepPulse).Every(32); // ~30fps
        }

        private void StopPulse()
        {
            _pulseJob?.Pause();
            _pulseJob = null;
            _pulseRing?.RemoveFromClassList("mic-pulse-ring--visible");
        }

        private void StepPulse()
        {
            if (_pulseRing == null) return;
            _pulsePhase += 0.032f; // ~1 rad/sec
            float alpha = Mathf.Sin(_pulsePhase * Mathf.PI) * 0.5f + 0.5f;
            float scale = 0.9f + Mathf.Sin(_pulsePhase * Mathf.PI) * 0.15f;
            _pulseRing.style.opacity = alpha;
            _pulseRing.style.scale = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
        }

        // ── 마이크 ─────────────────────────────────────────────

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

        // ── 대화 종료 버튼 ─────────────────────────────────────

        private void OnEndClicked()
        {
            EventBus.Publish(new ChatExitRequestedEvent());
        }

        // ── STT / AI 응답 ──────────────────────────────────────

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

        private void OnChatNotice(ChatNoticeEvent e)
        {
            SetThinking(false);
            SetMascotDotsVisible(false);
            AppendNoticeBubble(e.Message, e.Type == ChatNoticeType.Error);
            Root?.schedule.Execute(ScrollToBottom).ExecuteLater(50);
        }

        // ── 버블 ───────────────────────────────────────────────

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

            EnqueueBubble(container);
        }

        private void AppendNoticeBubble(string message, bool isError)
        {
            if (_bubbleList == null) return;

            var container = new VisualElement();
            container.AddToClassList("bubble-notice");
            if (isError) container.AddToClassList("bubble-notice--error");
            container.style.marginBottom = 20;

            var label = new Label(message);
            label.AddToClassList("bubble-notice__text");
            container.Add(label);

            EnqueueBubble(container);
        }

        /// <summary>버블을 등장 애니메이션과 함께 추가.</summary>
        private void EnqueueBubble(VisualElement el)
        {
            el.AddToClassList("bubble-enter");
            _bubbleList.Add(el);

            // 다음 프레임에 --in 클래스 추가 → USS transition 발동
            Root?.schedule.Execute(() =>
            {
                el.AddToClassList("bubble-enter--in");
                ScrollToBottom();
            }).ExecuteLater(16);
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

        // ── 마스코트 타이핑 점 ──────────────────────────────────

        private void SetMascotDotsVisible(bool on)
        {
            if (_mascotDots == null) return;
            _mascotDots.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (on) StartDotsAnim();
            else    StopDotsAnim();
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
