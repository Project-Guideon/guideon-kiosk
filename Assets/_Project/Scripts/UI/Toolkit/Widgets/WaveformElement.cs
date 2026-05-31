using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI.Toolkit
{
    /// <summary>
    /// 26개 막대로 STT RMS 레벨을 시각화하는 UI Toolkit 위젯.
    /// waveform-container VisualElement에 동적으로 자식을 생성.
    ///
    /// 개선사항:
    ///   - RMS 게인(×7) 적용 — 마이크 RMS가 작아 반응 약한 문제 해결.
    ///   - 막대별 Lerp 스무딩 — 부드러운 움직임.
    ///   - 최대 높이 52px, 최소 6px로 상향.
    ///   - 레벨에 따라 막대 색 알파 가변.
    ///   - Idle shimmer 유지.
    /// </summary>
    public class WaveformElement
    {
        private const int   BarCount     = 26;
        private const float RmsGain      = 7f;   // 게인: 작은 RMS를 증폭
        private const float MaxH         = 52f;
        private const float MinH         = 6f;
        private const float SmoothSpeed  = 12f;  // Lerp 계수 (per second, 32ms tick ≈ 0.38)
        private const float SmoothDt     = 0.032f;

        private readonly VisualElement   _container;
        private readonly VisualElement[] _bars = new VisualElement[BarCount];
        private readonly float[]         _currentH = new float[BarCount];

        // 막대별 기본 높이 (idle shimmer)
        private static readonly float[] BaseHeights =
        {
            10, 24, 16, 32, 20, 36, 14, 28, 18, 34, 12, 26, 20, 30, 16, 24, 10, 32, 20, 16, 28, 12, 36, 18, 14, 26
        };

        public WaveformElement(VisualElement container)
        {
            _container = container;
            Build();
        }

        private void Build()
        {
            _container.Clear();
            for (int i = 0; i < BarCount; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList("waveform-bar");
                float h = BaseHeights[i % BaseHeights.Length];
                bar.style.height = h;
                _currentH[i] = h;
                _container.Add(bar);
                _bars[i] = bar;
            }
        }

        /// <summary>RMS 레벨(0~1)로 막대 높이 업데이트. schedule.Execute에서 ~32ms마다 호출.</summary>
        public void SetLevel(float rms)
        {
            float amplified = Mathf.Clamp01(rms * RmsGain);
            float lerpT = 1f - Mathf.Pow(1f - Mathf.Clamp01(SmoothSpeed * SmoothDt), 1f);

            for (int i = 0; i < BarCount; i++)
            {
                float noise  = Mathf.PerlinNoise(i * 0.35f + Time.time * 2.5f, 0f);
                float target = amplified > 0.02f
                    ? Mathf.Lerp(MinH, MaxH, amplified * noise)
                    : Mathf.Lerp(MinH, BaseHeights[i % BaseHeights.Length] * 0.55f,
                          0.4f + 0.2f * Mathf.Sin(Time.time * 1.2f + i * 0.4f));

                _currentH[i] = Mathf.Lerp(_currentH[i], target, lerpT);
                _bars[i].style.height = _currentH[i];
            }

            // 활성 레벨에 따라 알파 가변 (0.55 ~ 1.0)
            float alpha = Mathf.Lerp(0.55f, 1f, amplified);
            var col = new Color(177f / 255f, 74f / 255f, 46f / 255f, alpha);
            for (int i = 0; i < BarCount; i++)
                _bars[i].style.backgroundColor = new StyleColor(col);
        }

        public void SetVisible(bool visible)
        {
            _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
