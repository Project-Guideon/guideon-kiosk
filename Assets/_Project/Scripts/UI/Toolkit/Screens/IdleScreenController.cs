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
        private VisualElement _dot1;
        private VisualElement _dot2;
        private VisualElement _dot3;

        private IVisualElementScheduledItem _dotJob;
        private IVisualElementScheduledItem _pulseJob;
        private float _pulsePhase;
        private float _dotPhase;

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Idle;
        }

        protected override void OnBindUI()
        {
            _locationText   = Q<Label>("location-text");
            _welcomeHeadline = Q<Label>("welcome-headline");
            _chipHoursVal   = Q<Label>("chip-hours-val");
            _chipWeatherVal = Q<Label>("chip-weather-val");
            _chipCrowdVal   = Q<Label>("chip-crowd-val");
            _touchCard      = Q("idle-touch-card");
            _dot1 = Q("dot-1");
            _dot2 = Q("dot-2");
            _dot3 = Q("dot-3");

            StartAnimations();
        }

        protected override void OnDisable()
        {
            _dotJob?.Pause();
            _pulseJob?.Pause();
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _dotJob?.Resume();
            _pulseJob?.Resume();
        }

        // ── Public API (MainSceneController에서 호출) ──────────────────────

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

        public void SetWifiStatus(bool connected)
        {
            // 혼잡도 칩에 상태 반영(UI 설계상 wifi → 혼잡도로 대체됨)
        }

        public void SetWelcomeText(string korean, string subtitle = null)
        {
            if (_welcomeHeadline != null && !string.IsNullOrEmpty(korean))
                _welcomeHeadline.text = korean;
        }

        /// <summary>Step 9 대비: 마스코트 슬롯의 화면 좌표 반환.</summary>
        public Vector2 GetMascotSlotScreenPos()
        {
            var slot = Q("mascot-slot");
            if (slot == null) return Vector2.zero;
            var rect = slot.worldBound;
            return new Vector2(rect.x, rect.y);
        }

        // ── 애니메이션 ──────────────────────────────────────────────────────

        private void StartAnimations()
        {
            // 도트 깜빡 (1.5초 주기)
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

            // CTA 카드 펄스
            _pulseJob = Root?.schedule.Execute(() =>
            {
                _pulsePhase += 0.02f;
                float pulse = 0.75f + 0.25f * Mathf.Sin(_pulsePhase);
                if (_touchCard != null)
                    _touchCard.style.opacity = pulse;
            }).Every(32);
        }
    }
}
