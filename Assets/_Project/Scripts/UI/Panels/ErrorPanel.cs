using Cysharp.Threading.Tasks;
using Guideon.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guideon.UI
{
    /// <summary>
    /// 에러 표시 패널. 인증 실패, 네트워크 오류 등.
    /// 에러 아이콘 + 메시지 + 재시도 버튼.
    /// </summary>
    public class ErrorPanel : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private TextMeshProUGUI _errorCodeText;
        [SerializeField] private Image _iconImage;

        [Header("Retry")]
        [SerializeField] private Button _retryButton;
        [SerializeField] private TextMeshProUGUI _retryCountdownText;
        [SerializeField] private float _autoRetrySeconds = 15f;

        [Header("Animation")]
        [SerializeField] private CanvasGroup _contentGroup;

        private float _retryTimer;
        private bool _autoRetryActive;
        private System.Action _onRetry;

        private void OnEnable()
        {
            _retryButton?.onClick.AddListener(OnRetryClicked);
            PlayEntrance().Forget();
        }

        private void OnDisable()
        {
            _retryButton?.onClick.RemoveListener(OnRetryClicked);
            _autoRetryActive = false;
        }

        private void Update()
        {
            if (!_autoRetryActive) return;

            _retryTimer -= Time.deltaTime;
            if (_retryCountdownText != null)
                _retryCountdownText.text = $"{Mathf.CeilToInt(_retryTimer)}초 후 자동 재시도";

            if (_retryTimer <= 0f)
            {
                _autoRetryActive = false;
                _onRetry?.Invoke();
            }
        }

        /// <summary>에러 내용을 표시하고 재시도 콜백을 등록.</summary>
        public void Show(string title, string message, string errorCode = null, System.Action onRetry = null)
        {
            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;
            if (_errorCodeText != null)
            {
                _errorCodeText.text = errorCode ?? "";
                _errorCodeText.gameObject.SetActive(!string.IsNullOrEmpty(errorCode));
            }

            _onRetry = onRetry;

            if (onRetry != null)
            {
                _retryTimer = _autoRetrySeconds;
                _autoRetryActive = true;
                if (_retryButton != null) _retryButton.gameObject.SetActive(true);
            }
            else
            {
                _autoRetryActive = false;
                if (_retryButton != null) _retryButton.gameObject.SetActive(false);
            }
        }

        private void OnRetryClicked()
        {
            _autoRetryActive = false;
            _onRetry?.Invoke();
        }

        private async UniTaskVoid PlayEntrance()
        {
            if (_contentGroup == null) return;
            _contentGroup.alpha = 0f;

            float duration = 0.4f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _contentGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                await UniTask.Yield();
            }
            _contentGroup.alpha = 1f;
        }
    }
}
