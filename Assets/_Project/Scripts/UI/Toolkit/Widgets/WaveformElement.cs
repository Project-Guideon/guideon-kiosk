using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI.Toolkit
{
    /// <summary>
    /// 26개 막대로 STT RMS 레벨을 시각화하는 UI Toolkit 위젯.
    /// waveform-container VisualElement에 동적으로 자식을 생성.
    /// </summary>
    public class WaveformElement
    {
        private const int BarCount = 26;
        private readonly VisualElement _container;
        private readonly VisualElement[] _bars = new VisualElement[BarCount];
        private float _level;

        // 막대별 기본 높이 (idle 상태의 자연스러운 파형 느낌)
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
                bar.style.height = BaseHeights[i % BaseHeights.Length];
                _container.Add(bar);
                _bars[i] = bar;
            }
        }

        /// <summary>RMS 레벨(0~1)로 막대 높이 업데이트. schedule.Execute에서 매 프레임 호출.</summary>
        public void SetLevel(float rms)
        {
            _level = Mathf.Clamp01(rms);
            float maxH = 36f;
            float minH = 4f;
            for (int i = 0; i < BarCount; i++)
            {
                float noise = Mathf.PerlinNoise(i * 0.3f + Time.time * 3f, 0f);
                float h = _level > 0.01f
                    ? Mathf.Lerp(minH, maxH, _level * noise)
                    : Mathf.Lerp(minH, BaseHeights[i % BaseHeights.Length], 0.4f);
                _bars[i].style.height = h;
            }
        }

        public void SetVisible(bool visible)
        {
            _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
