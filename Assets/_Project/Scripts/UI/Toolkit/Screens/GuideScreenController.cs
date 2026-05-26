using Cysharp.Threading.Tasks;
using Guideon.Chat;
using Guideon.Core;
using Guideon.Network.Stt;
using Guideon.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI
{
    public class GuideScreenController : PanelControllerBase
    {
        private Label _userQuestion;
        private Label _aiAnswer;
        private Label _guideSpeedText;
        private Button _micButton;
        private VisualElement _waveformContainer;
        private WaveformElement _waveform;
        private IVisualElementScheduledItem _waveformJob;
        private float _rmsLevel;

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Guide;
        }

        protected override void OnBindUI()
        {
            _userQuestion     = Q<Label>("user-question");
            _aiAnswer         = Q<Label>("ai-answer");
            _guideSpeedText   = Q<Label>("guide-speech-text");
            _micButton        = Q<Button>("mic-button");
            _waveformContainer = Q("waveform-container");

            _waveform = new WaveformElement(_waveformContainer);
            _micButton?.RegisterCallback<ClickEvent>(_ => OnMicClicked());
            _waveformJob = Root?.schedule.Execute(() => _waveform?.SetLevel(_rmsLevel)).Every(32);
        }

        protected override void SubscribeEvents()
        {
            EventBus.Subscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Subscribe<SttResultEvent>(OnSttResult);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged += OnRecordingStateChanged;
        }

        protected override void UnsubscribeEvents()
        {
            EventBus.Unsubscribe<ChatResponseEvent>(OnChatResponse);
            EventBus.Unsubscribe<SttResultEvent>(OnSttResult);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRecordingStateChanged -= OnRecordingStateChanged;
        }

        protected override void OnDisable()
        {
            _waveformJob?.Pause();
            if (SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel -= OnRmsLevel;
            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _waveformJob?.Resume();
        }

        private void OnMicClicked()
        {
            if (!SttManager.HasInstance) return;
            if (SttManager.Instance.IsRecording)
                SttManager.Instance.Stop();
            else
                SttManager.Instance.StartAsync().Forget();
        }

        private void OnRecordingStateChanged(bool recording)
        {
            _waveform?.SetVisible(recording);
            if (SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel -= OnRmsLevel;
            if (recording && SttManager.HasInstance)
                SttManager.Instance.OnRmsLevel += OnRmsLevel;
        }

        private void OnRmsLevel(float rms) => _rmsLevel = rms;

        private void OnSttResult(SttResultEvent e)
        {
            if (!e.IsFinal) return;
            if (_userQuestion != null) _userQuestion.text = e.Transcript;
        }

        private void OnChatResponse(ChatResponseEvent e)
        {
            if (_aiAnswer != null) _aiAnswer.text = e.Answer;
            if (_guideSpeedText != null)
                _guideSpeedText.text = e.Answer.Length > 40 ? e.Answer[..40] + "..." : e.Answer;
        }
    }
}
