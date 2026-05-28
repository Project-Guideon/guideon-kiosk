using System;
using Guideon.Core;
using Guideon.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI
{
    public class PairingScreenController : PanelControllerBase
    {
        private Label[] _digits = new Label[6];
        private Label _timerCountdown;
        private VisualElement _timerFill;

        private DateTime _expiresAt;
        private float _totalSeconds = 300f;
        private bool _hasCode;
        private IVisualElementScheduledItem _timerJob;

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Pairing;
        }

        protected override void OnBindUI()
        {
            for (int i = 0; i < 6; i++)
                _digits[i] = Q<Label>($"digit-{i}");

            _timerCountdown = Q<Label>("timer-countdown");
            _timerFill = Q("timer-fill");

            _timerJob = Root?.schedule
                .Execute(UpdateTimer)
                .Every(1000);
        }

        protected override void SubscribeEvents()
        {
            EventBus.Subscribe<PairingCodeIssuedEvent>(OnPairingCodeIssued);
        }

        protected override void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<PairingCodeIssuedEvent>(OnPairingCodeIssued);
        }

        protected override void OnDisable()
        {
            _timerJob?.Pause();
            _hasCode = false;
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _timerJob?.Resume();
        }

        private void OnPairingCodeIssued(PairingCodeIssuedEvent e)
        {
            DisplayCode(e.PairingCode, e.ExpiresAt);
        }

        public void DisplayCode(string code, string expiresAt)
        {
            for (int i = 0; i < 6 && i < code.Length; i++)
            {
                if (_digits[i] != null)
                    _digits[i].text = code[i].ToString();
            }

            if (DateTimeOffset.TryParse(expiresAt, out var dto))
            {
                _expiresAt = dto.LocalDateTime;
                _totalSeconds = (float)(dto.LocalDateTime - DateTime.Now).TotalSeconds;
                _hasCode = true;
            }
            else
            {
                Debug.LogWarning($"[PairingScreen] expiresAt 파싱 실패: '{expiresAt}'");
            }
        }

        private void UpdateTimer()
        {
            if (_timerCountdown == null || !_hasCode) return;
            var remaining = _expiresAt - DateTime.Now;
            if (remaining.TotalSeconds <= 0)
            {
                _timerCountdown.text = "00:00";
                if (_timerFill != null)
                    _timerFill.style.width = new Length(0f, LengthUnit.Percent);
            }
            else
            {
                _timerCountdown.text = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                if (_timerFill != null && _totalSeconds > 0f)
                {
                    float pct = (float)(remaining.TotalSeconds / _totalSeconds) * 100f;
                    _timerFill.style.width = new Length(pct, LengthUnit.Percent);
                }
            }
        }
    }
}
