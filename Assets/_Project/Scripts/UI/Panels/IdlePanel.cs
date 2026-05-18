using Cysharp.Threading.Tasks;
using Guideon.Mascot;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Guideon.UI
{
    public class IdlePanel : MonoBehaviour
    {
        // ── Header ──────────────────────────────────────────────
        [Header("Header — 로고 & 상태바")]
        [SerializeField] private Image _logoImage;
        [SerializeField] private TextMeshProUGUI _operatingHoursText;
        [SerializeField] private TextMeshProUGUI _temperatureText;
        [SerializeField] private Image _thermometerIcon;
        [SerializeField] private Image _wifiIcon;
        [SerializeField] private Button _languageButton;
        [SerializeField] private Image _languageIcon;

        // ── Welcome ──────────────────────────────────────────────
        [Header("환영 텍스트")]
        [SerializeField] private CanvasGroup _welcomeGroup;
        [FormerlySerializedAs("_welcomeText")]
        [SerializeField] private TextMeshProUGUI _welcomeKoreanText;
        [SerializeField] private TextMeshProUGUI _welcomeSubtitleText;
        [SerializeField] private TextMeshProUGUI _siteNameText;

        // ── Mascot ───────────────────────────────────────────────
        [Header("마스코트")]
        [SerializeField] private MascotLoader _mascotLoader;
        [SerializeField] private RectTransform _mascotArea;

        // ── Touch Card ───────────────────────────────────────────
        [Header("터치 유도 카드")]
        [FormerlySerializedAs("_touchPromptGroup")]
        [SerializeField] private CanvasGroup _touchCardGroup;
        [FormerlySerializedAs("_touchPromptRect")]
        [SerializeField] private RectTransform _touchCardRect;
        [FormerlySerializedAs("_touchPromptText")]
        [SerializeField] private TextMeshProUGUI _touchMainText;
        [SerializeField] private TextMeshProUGUI _touchSubText;
        [SerializeField] private Image _touchIcon;

        // ── Clock ────────────────────────────────────────────────
        [Header("시계")]
        [SerializeField] private TextMeshProUGUI _clockText;

        // ── Animation Params ─────────────────────────────────────
        [Header("애니메이션")]
        [SerializeField] private float _cardPulseSpeed = 1.2f;
        [SerializeField] private float _cardFloatAmplitude = 10f;
        [SerializeField] private float _cardFloatSpeed = 0.8f;
        [SerializeField] private float _mascotBreathAmplitude = 4f;
        [SerializeField] private float _mascotBreathSpeed = 0.6f;

        // ── Runtime State ────────────────────────────────────────
        private Vector2 _cardBasePos;
        private Vector2 _mascotBasePos;
        private bool _isVisible;

        private void Awake()
        {
            if (_touchCardRect != null)
                _cardBasePos = _touchCardRect.anchoredPosition;
            if (_mascotArea != null)
                _mascotBasePos = _mascotArea.anchoredPosition;
        }

        private void OnEnable()
        {
            _isVisible = true;
            PlayEntrance().Forget();
        }

        private void OnDisable()
        {
            _isVisible = false;
        }

        private void Update()
        {
            if (!_isVisible) return;

            AnimateTouchCard();
            AnimateMascotArea();
            UpdateClock();
        }

        // ── Public API ────────────────────────────────────────────

        public void SetSiteName(string siteName)
        {
            if (_siteNameText != null)
                _siteNameText.text = siteName;
        }

        public void SetTemperature(float celsius)
        {
            if (_temperatureText != null)
                _temperatureText.text = $"{celsius:F0}°C";
        }

        public void SetOperatingHours(string hours)
        {
            if (_operatingHoursText != null)
                _operatingHoursText.text = hours;
        }

        public void SetWifiStatus(bool connected)
        {
            if (_wifiIcon != null)
                _wifiIcon.color = connected
                    ? Color.white
                    : new Color(1f, 1f, 1f, 0.35f);
        }

        public void SetWelcomeText(string korean, string subtitle = null)
        {
            if (_welcomeKoreanText != null)
                _welcomeKoreanText.text = korean;
            if (_welcomeSubtitleText != null && subtitle != null)
                _welcomeSubtitleText.text = subtitle;
        }

        public MascotLoader GetMascotLoader() => _mascotLoader;

        // ── Private ───────────────────────────────────────────────

        private void AnimateTouchCard()
        {
            if (_touchCardGroup != null)
            {
                float pulse = (Mathf.Sin(Time.time * _cardPulseSpeed) + 1f) * 0.5f;
                _touchCardGroup.alpha = Mathf.Lerp(0.55f, 1f, pulse);
            }
            if (_touchCardRect != null)
            {
                float y = Mathf.Sin(Time.time * _cardFloatSpeed) * _cardFloatAmplitude;
                _touchCardRect.anchoredPosition = _cardBasePos + new Vector2(0, y);
            }
        }

        private void AnimateMascotArea()
        {
            if (_mascotArea == null) return;
            float y = Mathf.Sin(Time.time * _mascotBreathSpeed) * _mascotBreathAmplitude;
            _mascotArea.anchoredPosition = _mascotBasePos + new Vector2(0, y);
        }

        private void UpdateClock()
        {
            if (_clockText != null)
                _clockText.text = System.DateTime.Now.ToString("HH:mm");
        }

        private async UniTaskVoid PlayEntrance()
        {
            if (_welcomeGroup == null) return;
            _welcomeGroup.alpha = 0f;
            if (_touchCardGroup != null) _touchCardGroup.alpha = 0f;

            await UniTask.Delay(200);

            float duration = 0.7f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _welcomeGroup.alpha = t;
                await UniTask.Yield();
            }
            _welcomeGroup.alpha = 1f;

            await UniTask.Delay(150);

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (_touchCardGroup != null) _touchCardGroup.alpha = t;
                await UniTask.Yield();
            }
            if (_touchCardGroup != null) _touchCardGroup.alpha = 1f;
        }
    }
}
