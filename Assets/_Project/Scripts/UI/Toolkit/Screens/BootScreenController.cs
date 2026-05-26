using Guideon.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI
{
    public class BootScreenController : PanelControllerBase
    {
        private Label _statusText;
        private VisualElement _progressFill;
        private VisualElement _spinner;

        private float _targetProgress;
        private float _currentProgress;
        private IVisualElementScheduledItem _spinnerJob;
        private float _spinnerAngle;

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Boot;
        }

        protected override void OnBindUI()
        {
            _statusText = Q<Label>("status-text");
            _progressFill = Q("progress-fill");
            _spinner = Q("spinner");

            _targetProgress = 0f;
            _currentProgress = 0f;
            SetProgressWidth(0f);

            _spinnerJob = Root?.schedule
                .Execute(() =>
                {
                    _spinnerAngle = (_spinnerAngle + 6f) % 360f;
                    if (_spinner != null)
                        _spinner.style.rotate = new StyleRotate(new Rotate(_spinnerAngle));

                    // 부드러운 프로그레스
                    _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, 0.05f);
                    SetProgressWidth(_currentProgress);
                })
                .Every(16);
        }

        protected override void OnDisable()
        {
            _spinnerJob?.Pause();
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _spinnerJob?.Resume();
        }

        public void SetStatus(string message, float progress01 = -1f)
        {
            if (_statusText != null)
                _statusText.text = message;
            if (progress01 >= 0f)
                _targetProgress = Mathf.Clamp01(progress01);
        }

        private void SetProgressWidth(float t)
        {
            if (_progressFill == null) return;
            _progressFill.style.width = new Length(t * 100f, LengthUnit.Percent);
        }
    }
}
