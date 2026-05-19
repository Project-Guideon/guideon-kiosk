using System;
using UnityEngine;

namespace Guideon.Audio
{
    /// <summary>
    /// Unity Microphone API 래퍼.
    /// 지정된 프레임 크기(frameMs)로 PCM int16 청크와 RMS 레벨을 콜백으로 제공한다.
    /// </summary>
    public class MicrophoneCapture : MonoBehaviour
    {
        public event Action<byte[]> OnAudioFrame;  // binary PCM 16kHz int16 청크
        public event Action<float> OnRmsLevel;     // 0~1 볼륨 레벨

        public bool IsCapturing { get; private set; }

        private AudioClip _clip;
        private int _sampleRate;
        private int _frameSamples;
        private int _readPos;

        // 네이티브 레이트 → 16kHz 다운샘플용
        private int _nativeRate;
        private float[] _readBuffer;

        public void StartCapture(int targetSampleRate, int frameMs)
        {
            if (IsCapturing) return;

            _sampleRate = targetSampleRate;
            _frameSamples = targetSampleRate * frameMs / 1000;

            // 마이크 권한 요청 (Windows Editor에서도 동작)
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                StartCoroutine(RequestAndStart(targetSampleRate, frameMs));
                return;
            }

            BeginMicrophone();
        }

        private System.Collections.IEnumerator RequestAndStart(int targetSampleRate, int frameMs)
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
            if (Application.HasUserAuthorization(UserAuthorization.Microphone))
                BeginMicrophone();
            else
                Debug.LogError("[MicrophoneCapture] 마이크 권한 거부됨");
        }

        private void BeginMicrophone()
        {
            string device = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;

            // AudioClip 루프 버퍼 (1초)
            _nativeRate = AudioSettings.outputSampleRate;
            _clip = Microphone.Start(device, true, 1, _nativeRate);
            _readPos = 0;
            IsCapturing = true;
            Debug.Log($"[MicrophoneCapture] 시작 — device={device ?? "default"}, native={_nativeRate}Hz → target={_sampleRate}Hz");
        }

        public void StopCapture()
        {
            if (!IsCapturing) return;
            Microphone.End(null);
            IsCapturing = false;
            _clip = null;
            Debug.Log("[MicrophoneCapture] 중지");
        }

        private void Update()
        {
            if (!IsCapturing || _clip == null) return;

            int writePos = Microphone.GetPosition(null);
            // 루프 버퍼 내 새로운 샘플 개수
            int available = (writePos - _readPos + _nativeRate) % _nativeRate;
            if (available == 0) return;

            // 다운샘플 비율
            float ratio = (float)_nativeRate / _sampleRate;
            // 타겟 기준 프레임 하나에 해당하는 네이티브 샘플 수
            int nativeFrameSamples = Mathf.RoundToInt(_frameSamples * ratio);
            if (nativeFrameSamples == 0) return;

            while (available >= nativeFrameSamples)
            {
                // OnRmsLevel 콜백 내부에서 StopCapture()가 호출될 수 있으므로 매 루프 재확인
                if (!IsCapturing || _clip == null) return;

                if (_readBuffer == null || _readBuffer.Length != nativeFrameSamples)
                    _readBuffer = new float[nativeFrameSamples];

                _clip.GetData(_readBuffer, _readPos);
                _readPos = (_readPos + nativeFrameSamples) % _nativeRate;
                available -= nativeFrameSamples;

                byte[] pcm = Downsample(_readBuffer, nativeFrameSamples, _frameSamples);
                float rms = CalculateRms(_readBuffer);

                OnRmsLevel?.Invoke(rms);
                OnAudioFrame?.Invoke(pcm);
            }
        }

        private static byte[] Downsample(float[] src, int srcLen, int dstLen)
        {
            byte[] result = new byte[dstLen * 2]; // int16 = 2 bytes
            float step = (float)srcLen / dstLen;

            for (int i = 0; i < dstLen; i++)
            {
                int start = Mathf.FloorToInt(i * step);
                int end = Mathf.Min(Mathf.FloorToInt((i + 1) * step), srcLen);

                float sum = 0f;
                for (int j = start; j < end; j++) sum += src[j];
                float avg = sum / (end - start);

                short sample = (short)Mathf.Clamp(Mathf.RoundToInt(avg * 32767f), short.MinValue, short.MaxValue);
                result[i * 2] = (byte)(sample & 0xFF);
                result[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return result;
        }

        private static float CalculateRms(float[] samples)
        {
            float sum = 0f;
            foreach (float s in samples) sum += s * s;
            return Mathf.Sqrt(sum / samples.Length);
        }

        private void OnDestroy()
        {
            StopCapture();
        }
    }
}
