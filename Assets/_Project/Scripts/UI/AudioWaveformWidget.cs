using UnityEngine;
using UnityEngine.UI;

namespace Guideon.UI
{
    /// <summary>
    /// 음성 녹음/재생 시각화 위젯.
    /// 여러 개의 바가 sin 파형으로 살아 움직이며 오디오 입력 레벨에 반응.
    /// </summary>
    public class AudioWaveformWidget : MonoBehaviour
    {
        [Header("바 구성")]
        [SerializeField] private RectTransform _barContainer;
        [SerializeField] private int _barCount = 11;
        [SerializeField] private float _barWidth = 6f;
        [SerializeField] private float _barSpacing = 5f;
        [SerializeField] private float _barMinHeight = 6f;
        [SerializeField] private float _barMaxHeight = 48f;

        [Header("애니메이션")]
        [SerializeField] private float _waveSpeed = 4f;
        [SerializeField] private float _phaseOffset = 0.5f;
        [SerializeField] private float _smoothing = 8f;

        [Header("색상")]
        [SerializeField] private Color _activeColor = Color.white;
        [SerializeField] private Color _inactiveColor = new(1f, 1f, 1f, 0.3f);

        private RectTransform[] _bars;
        private float[] _targetHeights;
        private float[] _currentHeights;
        private float _audioLevel;
        private bool _isActive;

        private void Awake()
        {
            BuildBars();
        }

        private void OnEnable()
        {
            SetActive(false);
        }

        private void Update()
        {
            if (!_isActive) return;

            float t = Time.time * _waveSpeed;
            for (int i = 0; i < _bars.Length; i++)
            {
                float sin = (Mathf.Sin(t + i * _phaseOffset) + 1f) * 0.5f;
                float target = Mathf.Lerp(_barMinHeight, _barMaxHeight, sin * (0.4f + _audioLevel * 0.6f));
                _targetHeights[i] = target;
            }

            for (int i = 0; i < _bars.Length; i++)
            {
                _currentHeights[i] = Mathf.Lerp(_currentHeights[i], _targetHeights[i], Time.deltaTime * _smoothing);
                var size = _bars[i].sizeDelta;
                _bars[i].sizeDelta = new Vector2(size.x, _currentHeights[i]);
            }
        }

        // ── Public API ────────────────────────────────────────────

        public void SetActive(bool active)
        {
            _isActive = active;
            if (_bars == null) return;

            Color col = active ? _activeColor : _inactiveColor;
            foreach (var bar in _bars)
            {
                var img = bar.GetComponent<Image>();
                if (img) img.color = col;
            }

            if (!active)
                ResetBars();
        }

        /// <param name="level">0~1 범위의 마이크 입력 레벨</param>
        public void SetAudioLevel(float level)
        {
            _audioLevel = Mathf.Clamp01(level);
        }

        // ── Private ───────────────────────────────────────────────

        private void BuildBars()
        {
            if (_barContainer == null)
            {
                var go = new GameObject("BarContainer", typeof(RectTransform));
                go.transform.SetParent(transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                _barContainer = rt;
            }

            for (int i = _barContainer.childCount - 1; i >= 0; i--)
                Destroy(_barContainer.GetChild(i).gameObject);

            _bars = new RectTransform[_barCount];
            _targetHeights = new float[_barCount];
            _currentHeights = new float[_barCount];

            float totalWidth = _barCount * _barWidth + (_barCount - 1) * _barSpacing;
            float startX = -totalWidth * 0.5f + _barWidth * 0.5f;

            for (int i = 0; i < _barCount; i++)
            {
                var go = new GameObject($"Bar_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_barContainer, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(_barWidth, _barMinHeight);
                rt.anchoredPosition = new Vector2(startX + i * (_barWidth + _barSpacing), 0f);

                var img = go.GetComponent<Image>();
                img.color = _inactiveColor;

                _bars[i] = rt;
                _currentHeights[i] = _barMinHeight;
                _targetHeights[i] = _barMinHeight;
            }
        }

        private void ResetBars()
        {
            for (int i = 0; i < _bars.Length; i++)
            {
                _currentHeights[i] = _barMinHeight;
                _bars[i].sizeDelta = new Vector2(_barWidth, _barMinHeight);
            }
        }
    }
}
