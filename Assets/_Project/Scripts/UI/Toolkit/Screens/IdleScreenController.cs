using System.Collections.Generic;
using Guideon.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI
{
    public class IdleScreenController : PanelControllerBase
    {
        private Label _locationText;
        private Label _welcomeHeadline;
        private Label _chipHoursVal;
        private Label _chipWeatherVal;
        private Label _chipCrowdVal;

        private VisualElement _touchCard;
        private VisualElement _centerCircle;
        private List<VisualElement> _rippleRings;

        private VisualElement _dot1;
        private VisualElement _dot2;
        private VisualElement _dot3;

        private IVisualElementScheduledItem _dotJob;
        private IVisualElementScheduledItem _animJob;

        private float _dotPhase;
        private float _ripplePhase;  // 소나 리플 전체 주기 위상
        private float _cardPhase;    // 카드 숨쉬기 위상

        // 리플 링마다 위상 오프셋 (0 ~ 1)
        private static readonly float[] RippleOffsets = { 0f, 0.33f, 0.66f };
        // 각 링의 기본 최대 알파 (sm이 가장 진하게)
        private static readonly float[] RippleBaseAlpha = { 0.7f, 0.45f, 0.22f };

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Idle;
        }

        protected override void OnBindUI()
        {
            _locationText    = Q<Label>("location-text");
            _welcomeHeadline = Q<Label>("welcome-headline");
            _chipHoursVal    = Q<Label>("chip-hours-val");
            _chipWeatherVal  = Q<Label>("chip-weather-val");
            _chipCrowdVal    = Q<Label>("chip-crowd-val");
            _touchCard       = Q("idle-touch-card");
            _dot1            = Q("dot-1");
            _dot2            = Q("dot-2");
            _dot3            = Q("dot-3");

            // 리플 링 수집 (sm → md → lg 순서)
            _rippleRings = new List<VisualElement>();
            _rippleRings.Add(Root?.Q(className: "touch-ripple--sm"));
            _rippleRings.Add(Root?.Q(className: "touch-ripple--md"));
            _rippleRings.Add(Root?.Q(className: "touch-ripple--lg"));

            _centerCircle = Root?.Q(className: "touch-center-circle");

            // 화면 어디를 눌러도 터치 이벤트 발행 (UI Toolkit 포인터 이벤트로 확실히 처리)
            var idleRoot = Root?.Q("idle-root");
            idleRoot?.RegisterCallback<PointerDownEvent>(_ =>
                Guideon.Core.EventBus.Publish(new Guideon.Core.UserTouchedEvent()));

            StartAnimations();

            // 패널이 켜질 때마다 마스코트 텍스처 재바인딩
            // (UIDocument는 활성화 시 UXML에서 비주얼 트리를 새로 복제하므로 매번 주입 필요)
            if (Mascot.MascotStage.Active != null)
                SetMascotTexture(Mascot.MascotStage.Active.Texture);
        }

        protected override void OnDisable()
        {
            _dotJob?.Pause();
            _animJob?.Pause();
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _dotJob?.Resume();
            _animJob?.Resume();
        }

        // ── Public API ────────────────────────────────────────────

        public void SetSiteName(string siteName)
        {
            if (_locationText != null)
                _locationText.text = siteName;
        }

        public void SetTemperature(float celsius)
        {
            if (_chipWeatherVal != null)
                _chipWeatherVal.text = $"맑음 · {celsius:F0}°C";
        }

        public void SetOperatingHours(string hours)
        {
            if (_chipHoursVal != null)
                _chipHoursVal.text = hours;
        }

        public void SetWifiStatus(bool connected) { }

        public void SetWelcomeText(string korean, string subtitle = null)
        {
            if (_welcomeHeadline != null && !string.IsNullOrEmpty(korean))
                _welcomeHeadline.text = korean;
        }

        public Vector2 GetMascotSlotScreenPos()
        {
            var slot = Q("mascot-slot");
            if (slot == null) return Vector2.zero;
            var rect = slot.worldBound;
            return new Vector2(rect.x, rect.y);
        }

        /// <summary>
        /// RenderTexture를 mascot-slot 배경에 주입.
        /// MascotStage 초기화 후 MainSceneController에서 호출.
        /// </summary>
        public void SetMascotTexture(UnityEngine.RenderTexture rt)
        {
            var slot = Q("mascot-slot");
            if (slot == null || rt == null) return;
            slot.style.backgroundImage = new UnityEngine.UIElements.StyleBackground(
                UnityEngine.UIElements.Background.FromRenderTexture(rt));
        }

        // ── 애니메이션 ─────────────────────────────────────────────

        private void StartAnimations()
        {
            // 도트 파동 (80ms 틱, 3.5초 주기 — opacity + scale 동시)
            _dotJob = Root?.schedule.Execute(() =>
            {
                _dotPhase = (_dotPhase + 0.023f) % 1f; // 80ms × ? = 3.5s 주기
                float a1 = 0.5f + 0.5f * Mathf.Sin(_dotPhase * Mathf.PI * 2f);
                float a2 = 0.5f + 0.5f * Mathf.Sin((_dotPhase + 0.33f) * Mathf.PI * 2f);
                float a3 = 0.5f + 0.5f * Mathf.Sin((_dotPhase + 0.66f) * Mathf.PI * 2f);
                ApplyDot(_dot1, a1);
                ApplyDot(_dot2, a2);
                ApplyDot(_dot3, a3);
            }).Every(80);

            // 소나 리플 + 카드 펄스 (32ms 틱)
            _animJob = Root?.schedule.Execute(StepAnim).Every(32);
        }

        private static void ApplyDot(VisualElement dot, float t)
        {
            if (dot == null) return;
            dot.style.opacity = Mathf.Lerp(0.2f, 1f, t);
            float s = Mathf.Lerp(0.65f, 1.35f, t);
            dot.style.scale = new StyleScale(new Scale(new Vector3(s, s, 1f)));
        }

        private void StepAnim()
        {
            const float dt = 0.032f;

            // ── 소나 리플 ──────────────────────────────────────────
            // 주기 1.8초 (phase 0→1)
            _ripplePhase = (_ripplePhase + dt / 1.8f) % 1f;

            for (int i = 0; i < _rippleRings.Count; i++)
            {
                var ring = _rippleRings[i];
                if (ring == null) continue;

                // 각 링은 오프셋만큼 뒤처져서 퍼져나감
                float phase = (_ripplePhase + RippleOffsets[i]) % 1f;

                // scale: 0.55 → 1.65 (점에서 퍼짐)
                float scale = Mathf.Lerp(0.55f, 1.65f, phase);
                // opacity: 기본 알파 → 0 (점점 투명)
                float alpha = RippleBaseAlpha[i] * (1f - phase);

                ring.style.scale   = new StyleScale(new Scale(new Vector3(scale, scale, 1f)));
                ring.style.opacity = alpha;
            }

            // ── 센터 원 미세 펄스 ──────────────────────────────────
            if (_centerCircle != null)
            {
                float cs = 0.92f + 0.08f * Mathf.Sin(_ripplePhase * Mathf.PI * 2f);
                _centerCircle.style.scale = new StyleScale(new Scale(new Vector3(cs, cs, 1f)));
            }

            // ── 카드 숨쉬기 (scale + opacity) ─────────────────────
            // 주기 2.2초
            _cardPhase = (_cardPhase + dt / 2.2f) % 1f;
            if (_touchCard != null)
            {
                float pulse    = Mathf.Sin(_cardPhase * Mathf.PI * 2f);
                float opacity  = Mathf.Lerp(0.82f, 1f, (pulse + 1f) * 0.5f);
                float cardScale = Mathf.Lerp(0.992f, 1.008f, (pulse + 1f) * 0.5f);
                _touchCard.style.opacity = opacity;
                _touchCard.style.scale   = new StyleScale(new Scale(new Vector3(cardScale, cardScale, 1f)));
            }
        }
    }
}
