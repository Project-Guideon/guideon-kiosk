using Cysharp.Threading.Tasks;
using Guideon.Chat;
using Guideon.Core;
using Guideon.Network.Stt;
using Guideon.UI.Map;
using Guideon.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI
{
    public class ChatScreenController : PanelControllerBase
    {
        private VisualElement _bubbleList;
        private ScrollView    _chatScroll;
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
        // 타이핑 버블
        private VisualElement               _typingBubbleRow;
        private VisualElement[]             _typingDotEls;
        private IVisualElementScheduledItem _typingDotsJob;
        private int                         _typingDotsPhase;

        private IVisualElementScheduledItem _dotsAnimJob;
        private float _rmsLevel;
        private int   _dotsAnimPhase;

        // ── 지도 모드 ──────────────────────────────────────────
        private IMapView      _mapView;
        private string        _lastMapUrl;
        private bool          _mapOpen;
        private VisualElement _mapRegion;
        private VisualElement _mapWebviewArea;
        private Label         _mapUrlLabel;
        private Label         _mapSpeechText;
        private Button        _mapBtnClose;
        private Button        _btnOpenBrowser;
        private Button        _btnMap;

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Chat;
        }

        // 에디터 테스트 전용: M 키 → DIRECTION mock 이벤트 발사
        private void Update()
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.M))
            {
                Debug.Log("[ChatScreen] TEST: M키 → DIRECTION mock 이벤트");
                EventBus.Publish(new ChatResponseEvent
                {
                    Answer   = "(테스트) 화장실은 본관 1층에 있어요.",
                    Category = "DIRECTION",
                    MapUrl   = "https://map.kakao.com/link/to/본관화장실,37.5796,126.9770",
                    Emotion  = "default",
                    Language = "ko-KR",
                });
            }
#endif
        }

        protected override void OnBindUI()
        {
            _bubbleList        = Q("bubble-list");
            _chatScroll        = Q<ScrollView>("chat-scroll");
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
            _endButton?.RegisterCallback<PointerDownEvent>(_ => AnimateEndButtonPress());

            // 지도 패널 요소 바인딩
            _btnMap         = Q<Button>("btn-map");
            _mapRegion      = Q("map-region");
            _mapWebviewArea = Q("map-webview-area");
            _mapUrlLabel    = Q<Label>("map-url-label");
            _mapSpeechText  = Q<Label>("map-speech-text");
            _mapBtnClose    = Q<Button>("btn-map-close");
            _btnOpenBrowser = Q<Button>("btn-open-browser");

            _btnMap?.RegisterCallback<ClickEvent>(_ => OnMapButtonClicked());
            _mapBtnClose?.RegisterCallback<ClickEvent>(_ => CloseMap());
            _btnOpenBrowser?.RegisterCallback<ClickEvent>(_ => OnOpenBrowserClicked());

            SetRecording(false);
            SetMascotDotsVisible(false);
            ClearBubbles();
            SetMicState(MascotState.Idle);

            _waveformJob = Root?.schedule.Execute(() => _waveform?.SetLevel(_rmsLevel)).Every(32);

            // 패널이 켜질 때마다 마스코트 텍스처 재바인딩
            // (UIDocument는 활성화 시 UXML에서 비주얼 트리를 새로 복제하므로 매번 주입 필요)
            if (Mascot.MascotStage.Active != null)
                SetMascotTexture(Mascot.MascotStage.Active.Texture);
        }

        protected override void SubscribeEvents()
        {
            EventBus.Subscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Subscribe<SttResultEvent>(OnSttResult);
            EventBus.Subscribe<MascotStateEvent>(OnMascotState);
            EventBus.Subscribe<ChatNoticeEvent>(OnChatNotice);
            EventBus.Subscribe<MascotLoadedEvent>(OnMascotLoaded);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged += OnRecordingStateChanged;
        }

        protected override void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Unsubscribe<SttResultEvent>(OnSttResult);
            EventBus.Unsubscribe<MascotStateEvent>(OnMascotState);
            EventBus.Unsubscribe<ChatNoticeEvent>(OnChatNotice);
            EventBus.Unsubscribe<MascotLoadedEvent>(OnMascotLoaded);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged -= OnRecordingStateChanged;
        }

        private void OnMascotLoaded(MascotLoadedEvent _)
        {
            if (Mascot.MascotStage.Active != null)
                SetMascotTexture(Mascot.MascotStage.Active.Texture);
        }

        protected override void OnDisable()
        {
            _waveformJob?.Pause();
            HideTypingBubble();
            StopDotsAnim();
            StopPulse();
            if (SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel -= OnRmsLevel;
            // 화면을 떠날 때 웹뷰 잔상 반드시 제거
            _mapView?.Hide();
            _mapOpen = false;
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

        private void AnimateEndButtonPress()
        {
            if (_endButton == null) return;
            _endButton.AddToClassList("btn-end-pressed");
            Root?.schedule.Execute(() =>
                _endButton?.RemoveFromClassList("btn-end-pressed")).ExecuteLater(220);
        }

        // ── STT / AI 응답 ──────────────────────────────────────

        private void OnSttResult(SttResultEvent e)
        {
            if (!e.IsFinal) return;
            AppendBubble(e.Transcript, isUser: true);
            ShowTypingBubble();
            SetMascotDotsVisible(true);
        }

        private void OnChatResponse(ChatResponseEvent e)
        {
            HideTypingBubble();
            SetMascotDotsVisible(false);
            AppendBubble(e.Answer, isUser: false);

            Root?.schedule.Execute(ScrollToBottom).ExecuteLater(50);

            // 길안내 응답이면 지도 패널 자동 열기, 아니면 열려있던 지도 닫기
            if (e.Category == "DIRECTION" && !string.IsNullOrEmpty(e.MapUrl))
                OpenMap(e.MapUrl, placeName: null);
            else if (_mapOpen)
                CloseMap();
        }

        private void OnChatNotice(ChatNoticeEvent e)
        {
            HideTypingBubble();
            SetMascotDotsVisible(false);
            AppendNoticeBubble(e.Message, e.Type == ChatNoticeType.Error);
            // 오류 시 "말씀해 보세요" 힌트가 남아 마이크가 작동 중인 것처럼 보이는 문제 해소
            if (e.Type == ChatNoticeType.Error && _micHint != null)
            {
                _micHint.text = "잠시 후 다시 말씀해 주세요";
                SetMicButtonState(active: false, busy: false);
                StopPulse();
            }
            Root?.schedule.Execute(ScrollToBottom).ExecuteLater(50);
        }

        // ── 타이핑 버블 ────────────────────────────────────────

        private void ShowTypingBubble()
        {
            HideTypingBubble(); // 중복 방지

            if (_bubbleList == null) return;

            // AI 행 래퍼
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 28;

            var avatar = new VisualElement();
            avatar.AddToClassList("ai-avatar");
            avatar.style.marginRight = 18;
            var avatarLabel = new Label("G");
            avatarLabel.AddToClassList("ai-avatar__text");
            avatar.Add(avatarLabel);

            // 점 3개가 들어있는 버블
            var bubble = new VisualElement();
            bubble.AddToClassList("typing-bubble");

            _typingDotEls = new VisualElement[3];
            for (int i = 0; i < 3; i++)
            {
                var dot = new VisualElement();
                dot.AddToClassList("typing-dot");
                bubble.Add(dot);
                _typingDotEls[i] = dot;
            }

            row.Add(avatar);
            row.Add(bubble);
            _typingBubbleRow = row;

            EnqueueBubble(row);

            // 점 바운스 시작
            _typingDotsPhase = 0;
            _typingDotsJob = Root?.schedule.Execute(StepTypingDots).Every(200);
        }

        private void HideTypingBubble()
        {
            _typingDotsJob?.Pause();
            _typingDotsJob = null;
            _typingDotEls  = null;

            if (_typingBubbleRow != null)
            {
                _typingBubbleRow.RemoveFromHierarchy();
                _typingBubbleRow = null;
            }
        }

        private void StepTypingDots()
        {
            if (_typingDotEls == null) return;
            int active = _typingDotsPhase % _typingDotEls.Length;
            for (int i = 0; i < _typingDotEls.Length; i++)
            {
                float y = (i == active) ? -9f : 0f;
                _typingDotEls[i].style.translate = new StyleTranslate(
                    new Translate(new Length(0), new Length(y, LengthUnit.Pixel)));
            }
            _typingDotsPhase++;
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

        /// <summary>
        /// RenderTexture를 mascot-slot 배경에 주입.
        /// Chat 화면 전환 완료 후 MainSceneController에서 호출.
        /// </summary>
        public void SetMascotTexture(UnityEngine.RenderTexture rt)
        {
            var slot = Q("mascot-slot");
            if (slot == null || rt == null) return;
            slot.style.backgroundImage = new UnityEngine.UIElements.StyleBackground(
                UnityEngine.UIElements.Background.FromRenderTexture(rt));
        }

        // ── 지도 모드 ──────────────────────────────────────────

        /// <summary>
        /// DIRECTION 응답 수신 또는 btn-map 수동 클릭 시 지도 패널을 연다.
        /// placeName이 있으면 말풍선에 표시.
        /// </summary>
        private void OpenMap(string url, string placeName)
        {
            if (string.IsNullOrEmpty(url)) return;
            _lastMapUrl = url;

            // IMapView 초기화 (최초 1회)
            if (_mapView == null)
            {
                _mapView = MapViewFactory.Create();
                // 폴백(Placeholder)이면 URL 변경 이벤트를 UI에 연결
                if (_mapView is PlaceholderMapView placeholder)
                    placeholder.OnUrlChanged += OnPlaceholderUrlChanged;
            }

            // 채팅창 축소 + 마스코트 패널 숨김 + 지도 패널 표시
            var chatLeft = Q("chat-left");
            chatLeft?.AddToClassList("chat-left--map");
            var chatRight = Q("chat-right");
            if (chatRight != null) chatRight.style.display = DisplayStyle.None;
            if (_mapRegion != null) _mapRegion.style.display = DisplayStyle.Flex;

            // 웹뷰 영역 사각형 계산 후 웹뷰 열기 (레이아웃 확정 다음 프레임)
            Root?.schedule.Execute(ShowWebViewAfterLayout).ExecuteLater(32);
            _mapOpen = true;
            IdleTimeoutManager.Instance?.Stop();

            // btn-map 버튼 텍스트 → 닫기 토글
            if (_btnMap != null) _btnMap.text = "🗺 지도 닫기";
        }

        private void ShowWebViewAfterLayout()
        {
            if (_mapWebviewArea == null || _mapView == null || string.IsNullOrEmpty(_lastMapUrl)) return;

            // UI Toolkit 패널 좌표(포인트) → 스크린 픽셀 변환
            var panel = Root?.panel;
            if (panel == null) return;

            Rect panelRect = _mapWebviewArea.worldBound; // 패널 포인트 단위
            float ppp      = panel.scaledPixelsPerPoint;  // 포인트→픽셀 배율

            // Y 축: UI Toolkit은 top=0이 화면 위, 스크린은 bottom=0이 화면 아래
            float screenH = Screen.height;
            var screenRect = new Rect(
                panelRect.x      * ppp,
                screenH - (panelRect.yMax * ppp),
                panelRect.width  * ppp,
                panelRect.height * ppp
            );

            _mapView.Show(_lastMapUrl, screenRect);
        }

        private void CloseMap()
        {
            _mapOpen = false;
            _mapView?.Hide();
            IdleTimeoutManager.Instance?.Begin();

            // 채팅창 복원 + 마스코트 패널 복원
            var chatLeft = Q("chat-left");
            chatLeft?.RemoveFromClassList("chat-left--map");
            var chatRight = Q("chat-right");
            if (chatRight != null) chatRight.style.display = DisplayStyle.Flex;
            if (_mapRegion != null) _mapRegion.style.display = DisplayStyle.None;

            // 메인 마스코트 슬롯 텍스처 재주입
            if (Mascot.MascotStage.Active != null)
                SetMascotTexture(Mascot.MascotStage.Active.Texture);

            if (_btnMap != null) _btnMap.text = "🗺 지도 보기";
        }

        private void OnMapButtonClicked()
        {
            if (_mapOpen)
                CloseMap();
            else if (!string.IsNullOrEmpty(_lastMapUrl))
                OpenMap(_lastMapUrl, placeName: null);
        }

        private void OnOpenBrowserClicked()
        {
            if (_mapView is PlaceholderMapView p)
                p.OpenInBrowser();
            else if (!string.IsNullOrEmpty(_lastMapUrl))
                Application.OpenURL(_lastMapUrl);
        }

        private void OnPlaceholderUrlChanged(string url)
        {
            if (_mapUrlLabel != null)
                _mapUrlLabel.text = url ?? "";
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
