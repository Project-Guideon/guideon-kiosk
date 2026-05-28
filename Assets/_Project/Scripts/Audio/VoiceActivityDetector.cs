using System;
using UnityEngine;

namespace Guideon.Audio
{
    /// <summary>
    /// RMS 기반 음성 활동 감지기(VAD).
    /// MicrophoneCapture의 RMS 레벨을 입력받아 두 가지 타임아웃을 발행한다.
    ///
    ///   OnSilenceTimeout    — 발화 후 silenceTimeoutMs 동안 무음 → 서버 처리 트리거.
    ///   OnNoSpeechTimeout   — 처음부터 발화 없이 noSpeechTimeoutSec 초 경과 → 대화 종료 트리거.
    /// </summary>
    public class VoiceActivityDetector
    {
        public event Action OnSilenceTimeout;
        public event Action OnNoSpeechTimeout;

        public bool IsVoiceDetected { get; private set; }

        private readonly float _silenceThreshold;
        private readonly float _silenceTimeoutSec;
        private readonly float _noSpeechTimeoutSec;
        private float _silenceTimer;
        private bool _running;

        /// <param name="silenceThreshold">음성 판단 최소 RMS</param>
        /// <param name="silenceTimeoutMs">발화 후 종료까지 무음 대기 ms</param>
        /// <param name="noSpeechTimeoutSec">발화 없이 종료까지 대기 초 (≤0 이면 비활성)</param>
        public VoiceActivityDetector(float silenceThreshold, int silenceTimeoutMs,
                                     float noSpeechTimeoutSec = 0f)
        {
            _silenceThreshold   = silenceThreshold;
            _silenceTimeoutSec  = silenceTimeoutMs / 1000f;
            _noSpeechTimeoutSec = noSpeechTimeoutSec;
        }

        public void Begin()
        {
            _silenceTimer    = 0f;
            IsVoiceDetected  = false;
            _running         = true;
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
                _silenceTimer   = 0f;
            }
            else
            {
                _silenceTimer += deltaTime;

                if (IsVoiceDetected)
                {
                    // 발화 후 무음 지속 → 서버 처리
                    if (_silenceTimer >= _silenceTimeoutSec)
                    {
                        _running = false;
                        Debug.Log($"[VAD] 발화 후 무음 감지 → 종료 요청 ({_silenceTimeoutSec:F1}s)");
                        OnSilenceTimeout?.Invoke();
                    }
                }
                else
                {
                    // 처음부터 발화 없음 → 대화 종료
                    if (_noSpeechTimeoutSec > 0f && _silenceTimer >= _noSpeechTimeoutSec)
                    {
                        _running = false;
                        Debug.Log($"[VAD] 무발화 종료 감지 ({_noSpeechTimeoutSec:F1}s)");
                        OnNoSpeechTimeout?.Invoke();
                    }
                }
            }
        }
    }
}
