using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guideon.UI
{
    /// <summary>
    /// Boot 씬 스플래시 화면.
    /// 로고 페이드인 → 상태 텍스트 표시 → 다음 단계 전환.
    /// </summary>
    public class BootPanel : MonoBehaviour
    {
        [Header("Logo")]
        [SerializeField] private CanvasGroup _logoGroup;
        [SerializeField] private RectTransform _logoRect;

        [Header("Status")]
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private Image _progressBar;
        [SerializeField] private RectTransform _progressBarFill;

        [Header("Animation")]
        [SerializeField] private float _logoFadeDuration = 1.2f;
        [SerializeField] private float _logoFloatAmplitude = 8f;
        [SerializeField] private float _logoFloatSpeed = 1.5f;

        private Vector2 _logoBasePos;
        private float _targetProgress;
        private float _currentProgress;

        private void Awake()
        {
            if (_logoGroup != null)
                _logoGroup.alpha = 0f;
            if (_logoRect != null)
                _logoBasePos = _logoRect.anchoredPosition;
        }

        private void OnEnable()
        {
            PlayEntrance().Forget();
        }

        private void Update()
        {
            // 로고 부드러운 떠다니는 효과
            if (_logoRect != null)
            {
                float yOffset = Mathf.Sin(Time.time * _logoFloatSpeed) * _logoFloatAmplitude;
                _logoRect.anchoredPosition = _logoBasePos + new Vector2(0, yOffset);
            }

            // 프로그레스 바 부드러운 채움
            if (_progressBarFill != null)
            {
                _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.deltaTime * 5f);
                _progressBarFill.anchorMax = new Vector2(_currentProgress, 1f);
            }
        }

        /// <summary>상태 텍스트와 진행률을 업데이트.</summary>
        public void SetStatus(string message, float progress01 = -1f)
        {
            if (_statusText != null)
                _statusText.text = message;
            if (progress01 >= 0f)
                _targetProgress = Mathf.Clamp01(progress01);
        }

        private async UniTaskVoid PlayEntrance()
        {
            if (_logoGroup == null) return;

            // 로고 페이드인
            float elapsed = 0f;
            while (elapsed < _logoFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / _logoFadeDuration;
                _logoGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                await UniTask.Yield();
            }
            _logoGroup.alpha = 1f;
        }
    }
}
