using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Guideon.UI
{
    /// <summary>
    /// 대기 화면. 관광객이 없을 때 표시.
    /// 터치 유도 애니메이션 + 시계 + 환영 메시지.
    /// </summary>
    public class IdlePanel : MonoBehaviour
    {
        [Header("Welcome")]
        [SerializeField] private TextMeshProUGUI _welcomeText;
        [SerializeField] private TextMeshProUGUI _siteNameText;
        [SerializeField] private CanvasGroup _welcomeGroup;

        [Header("Touch Prompt")]
        [SerializeField] private CanvasGroup _touchPromptGroup;
        [SerializeField] private RectTransform _touchPromptRect;
        [SerializeField] private TextMeshProUGUI _touchPromptText;

        [Header("Clock")]
        [SerializeField] private TextMeshProUGUI _clockText;

        [Header("Animation")]
        [SerializeField] private float _promptPulseSpeed = 1.5f;
        [SerializeField] private float _promptFloatAmplitude = 12f;
        [SerializeField] private float _promptFloatSpeed = 1.0f;

        private Vector2 _promptBasePos;

        private void Awake()
        {
            if (_touchPromptRect != null)
                _promptBasePos = _touchPromptRect.anchoredPosition;
        }

        private void OnEnable()
        {
            PlayEntrance().Forget();
        }

        private void Update()
        {
            // 터치 유도 — 부드럽게 위아래 떠다니기 + 페이드 펄스
            if (_touchPromptGroup != null)
            {
                float pulse = (Mathf.Sin(Time.time * _promptPulseSpeed) + 1f) * 0.5f;
                _touchPromptGroup.alpha = Mathf.Lerp(0.4f, 1f, pulse);
            }
            if (_touchPromptRect != null)
            {
                float y = Mathf.Sin(Time.time * _promptFloatSpeed) * _promptFloatAmplitude;
                _touchPromptRect.anchoredPosition = _promptBasePos + new Vector2(0, y);
            }

            // 시계
            if (_clockText != null)
                _clockText.text = System.DateTime.Now.ToString("HH:mm");
        }

        /// <summary>관광지 이름 세팅 (bootstrap 후 호출).</summary>
        public void SetSiteName(string siteName)
        {
            if (_siteNameText != null)
                _siteNameText.text = siteName;
        }

        /// <summary>환영 메시지 세팅.</summary>
        public void SetWelcomeMessage(string message)
        {
            if (_welcomeText != null)
                _welcomeText.text = message;
        }

        private async UniTaskVoid PlayEntrance()
        {
            // 환영 메시지 페이드인
            if (_welcomeGroup == null) return;
            _welcomeGroup.alpha = 0f;

            await UniTask.Delay(300);

            float duration = 0.8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _welcomeGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                await UniTask.Yield();
            }
            _welcomeGroup.alpha = 1f;
        }
    }
}
