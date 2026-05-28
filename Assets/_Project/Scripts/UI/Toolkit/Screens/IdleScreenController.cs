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

            StartAnimations();
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

        // ── 애니메이션 ─────────────────────────────────────────────

        private void StartAnimations()
        {
            // 도트 blink (50ms 틱)
            _dotJob = Root?.schedule.Execute(() =>
            {
                _dotPhase = (_dotPhase + 0.1f) % 1f;
                float a1 = 0.5f + 0.5f * Mathf.Sin(_dotPhase * Mathf.PI * 2f);
                float a2 = 0.5f + 0.5f * Mathf.Sin((_dotPhase + 0.33f) * Mathf.PI * 2f);
                float a3 = 0.5f + 0.5f * Mathf.Sin((_dotPhase + 0.66f) * Mathf.PI * 2f);
                if (_dot1 != null) _dot1.style.opacity = Mathf.Lerp(0.2f, 1f, a1);
                if (_dot2 != null) _dot2.style.opacity = Mathf.Lerp(0.2f, 1f, a2);
                if (_dot3 != null) _dot3.style.opacity = Mathf.Lerp(0.2f, 1f, a3);
            }).Every(50);

            // 소나 리플 + 카드 펄스 (32ms 틱)
            _animJob = Root?.schedule.Execute(StepAnim).Every(32);
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
