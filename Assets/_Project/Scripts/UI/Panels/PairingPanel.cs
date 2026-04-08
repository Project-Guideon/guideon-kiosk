using System;
using Cysharp.Threading.Tasks;
using Guideon.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guideon.UI
{
    /// <summary>
    /// 페어링 코드 표시 화면.
    /// 6자리 코드를 대형으로 표시 + 만료 카운트다운 + 펄스 애니메이션.
    /// </summary>
    public class PairingPanel : MonoBehaviour
    {
        [Header("Code Display")]
        [SerializeField] private TextMeshProUGUI _codeText;
        [SerializeField] private CanvasGroup _codeGroup;

        [Header("Info")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _instructionText;
        [SerializeField] private TextMeshProUGUI _timerText;

        [Header("Visual")]
        [SerializeField] private Image _codeBackground;
        [SerializeField] private float _pulseSpeed = 1.2f;
        [SerializeField] private float _pulseMin = 0.85f;

        private DateTime _expiresAt;
        private bool _isActive;

        private void OnEnable()
        {
            EventBus.Subscribe<PairingCodeIssuedEvent>(OnPairingCodeIssued);
            _isActive = true;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<PairingCodeIssuedEvent>(OnPairingCodeIssued);
            _isActive = false;
        }

        private void Update()
        {
            if (!_isActive) return;

            // 코드 카드 펄스 효과 (부드럽게 숨쉬는 느낌)
            if (_codeGroup != null)
            {
                float pulse = Mathf.Lerp(_pulseMin, 1f,
                    (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f);
                _codeGroup.alpha = pulse;
            }

            // 만료 카운트다운
            UpdateTimer();
        }

        private void OnPairingCodeIssued(PairingCodeIssuedEvent e)
        {
            DisplayCode(e.PairingCode, e.ExpiresAt);
        }

        public void DisplayCode(string code, string expiresAt)
        {
            if (_codeText != null)
            {
                // 코드를 3자리씩 끊어서 표시: "A3F 29K"
                string formatted = code.Length == 6
                    ? $"{code[..3]}  {code[3..]}"
                    : code;
                _codeText.text = formatted;
            }

            if (DateTime.TryParse(expiresAt, out var dt))
                _expiresAt = dt;

            PlayCodeEntrance().Forget();
        }

        private void UpdateTimer()
        {
            if (_timerText == null) return;

            var remaining = _expiresAt - DateTime.Now;
            if (remaining.TotalSeconds <= 0)
            {
                _timerText.text = "코드 만료 — 재발급 중...";
                _timerText.color = GuideonColors.Warning;
            }
            else
            {
                _timerText.text = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                _timerText.color = remaining.TotalSeconds < 60
                    ? GuideonColors.Warning
                    : GuideonColors.TextSecondary;
            }
        }

        private async UniTaskVoid PlayCodeEntrance()
        {
            if (_codeGroup == null) return;

            // 코드 스케일 + 페이드 입장
            _codeGroup.alpha = 0f;
            var rect = _codeGroup.GetComponent<RectTransform>();
            Vector3 startScale = Vector3.one * 0.7f;
            Vector3 endScale = Vector3.one;

            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float ease = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic
                _codeGroup.alpha = ease;
                if (rect != null)
                    rect.localScale = Vector3.Lerp(startScale, endScale, ease);
                await UniTask.Yield();
            }
            _codeGroup.alpha = 1f;
            if (rect != null) rect.localScale = endScale;
        }
    }
}
