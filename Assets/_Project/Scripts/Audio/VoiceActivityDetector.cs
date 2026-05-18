using System;
using UnityEngine;

namespace Guideon.Audio
{
    /// <summary>
    /// RMS 기반 음성 활동 감지기(VAD).
    /// MicrophoneCapture의 RMS 레벨을 입력받아, silenceTimeoutMs 동안 무음이 지속되면
    /// OnSilenceTimeout을 발행해 SttManager에 녹음 종료를 알린다.
    /// </summary>
    public class VoiceActivityDetector
    {
        public event Action OnSilenceTimeout;

        public bool IsVoiceDetected { get; private set; }

        private readonly float _silenceThreshold;
        private readonly float _silenceTimeoutSec;
        private float _silenceTimer;
        private bool _running;

        public VoiceActivityDetector(float silenceThreshold, int silenceTimeoutMs)
        {
            _silenceThreshold = silenceThreshold;
            _silenceTimeoutSec = silenceTimeoutMs / 1000f;
        }

        public void Begin()
        {
            _silenceTimer = 0f;
            IsVoiceDetected = false;
            _running = true;
        }

        public void Stop()
        {
            _running = false;
        }

        /// <summary>MicrophoneCapture.OnRmsLevel 콜백에서 호출. deltaTime 전달.</summary>
        public void Feed(float rms, float deltaTime)
        {
            if (!_running) return;

            if (rms >= _silenceThreshold)
            {
                IsVoiceDetected = true;
                _silenceTimer = 0f;
            }
            else
            {
                _silenceTimer += deltaTime;
                if (_silenceTimer >= _silenceTimeoutSec)
                {
                    _running = false;
                    Debug.Log($"[VAD] 무음 감지 → 종료 요청 ({_silenceTimeoutSec:F1}s)");
                    OnSilenceTimeout?.Invoke();
                }
            }
        }
    }
}
