using Guideon.Core;
using Guideon.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI
{
    public class ErrorScreenController : PanelControllerBase
    {
        private Label _titleLabel;
        private Label _messageLabel;
        private Label _codeLabel;
        private Button _retryButton;
        private Label _countdownLabel;

        private System.Action _onRetry;
        private float _retryTimer;
        private bool _autoRetryActive;
        private IVisualElementScheduledItem _timerJob;

        private const float AutoRetrySeconds = 15f;

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Error;
        }

        protected override void OnBindUI()
        {
            _titleLabel    = Q<Label>("error-title");
            _messageLabel  = Q<Label>("error-message");
            _codeLabel     = Q<Label>("error-code");
            _retryButton   = Q<Button>("retry-button");
            _countdownLabel = Q<Label>("countdown-text");

            _retryButton?.RegisterCallback<ClickEvent>(_ => OnRetryClicked());
        }

        public void Show(string title, string message, string errorCode = null, System.Action onRetry = null)
        {
            if (_titleLabel != null)   _titleLabel.text = title;
            if (_messageLabel != null) _messageLabel.text = message;

            if (_codeLabel != null)
            {
                _codeLabel.text = errorCode ?? "";
                _codeLabel.style.display = string.IsNullOrEmpty(errorCode) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            _onRetry = onRetry;

            if (onRetry != null)
            {
                _retryTimer = AutoRetrySeconds;
                _autoRetryActive = true;
                if (_retryButton != null) _retryButton.style.display = DisplayStyle.Flex;
                StartCountdown();
            }
            else
            {
                _autoRetryActive = false;
                if (_retryButton != null) _retryButton.style.display = DisplayStyle.None;
                if (_countdownLabel != null) _countdownLabel.text = "";
            }
        }

        private void StartCountdown()
        {
            _timerJob?.Pause();
            _timerJob = Root?.schedule.Execute(TickCountdown).Every(1000);
        }

        private void TickCountdown()
        {
            if (!_autoRetryActive) return;
            _retryTimer -= 1f;
            if (_countdownLabel != null)
                _countdownLabel.text = $"{Mathf.CeilToInt(_retryTimer)}초 후 자동 재시도";

            if (_retryTimer <= 0f)
            {
                _autoRetryActive = false;
                _timerJob?.Pause();
                _onRetry?.Invoke();
            }
        }

        private void OnRetryClicked()
        {
            _autoRetryActive = false;
            _timerJob?.Pause();
            _onRetry?.Invoke();
        }

        protected override void OnDisable()
        {
            _timerJob?.Pause();
            _autoRetryActive = false;
            base.OnDisable();
        }
    }
}
