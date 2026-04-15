using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Guideon.Core;

namespace Guideon.UI
{
    /// <summary>
    /// 패널 Show/Hide 및 화면 전환 담당. 모든 UI 패널은 여기서 관리.
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        public static class Panel
        {
            public const string Boot = "Boot";
            public const string Pairing = "Pairing";
            public const string Idle = "Idle";
            public const string Chat = "Chat";
            public const string Error = "Error";
        }

        [Serializable]
        private struct PanelEntry
        {
            public string id;
            public GameObject panel;
        }

        [SerializeField] private PanelEntry[] _panels;
        [SerializeField] private CanvasGroup _transitionOverlay;
        [SerializeField] private float _transitionDuration = 0.35f;

        private readonly Dictionary<string, GameObject> _panelMap = new();
        private string _currentPanelId;
        private bool _isTransitioning;

        protected override void OnInitialize()
        {
            foreach (var entry in _panels)
            {
                if (string.IsNullOrEmpty(entry.id) || entry.panel == null)
                {
                    Debug.LogWarning("[UIManager] 비어있는 패널 항목 있음. Inspector 확인 요망.");
                    continue;
                }
                _panelMap[entry.id] = entry.panel;
            }

            if (_transitionOverlay != null)
            {
                _transitionOverlay.alpha = 0f;
                _transitionOverlay.blocksRaycasts = false;
            }
        }

        /// <summary>패널 하나만 활성화하고 나머지는 전부 끔 (즉시, 애니메이션 없음).</summary>
        public void ShowOnly(string panelId)
        {
            foreach (var kv in _panelMap)
                kv.Value.SetActive(kv.Key == panelId);
            _currentPanelId = panelId;
        }

        /// <summary>페이드 전환으로 패널 교체. 디자인 퀄리티용.</summary>
        public async UniTask TransitionToAsync(string panelId)
        {
            if (_isTransitioning || panelId == _currentPanelId) return;
            _isTransitioning = true;

            // Fade out
            if (_transitionOverlay != null)
            {
                _transitionOverlay.blocksRaycasts = true;
                await FadeCanvasGroupAsync(_transitionOverlay, 0f, 1f, _transitionDuration);
            }

            // Switch panel
            ShowOnly(panelId);

            // Fade in
            if (_transitionOverlay != null)
            {
                await FadeCanvasGroupAsync(_transitionOverlay, 1f, 0f, _transitionDuration);
                _transitionOverlay.blocksRaycasts = false;
            }

            _isTransitioning = false;
        }

        /// <summary>지정 패널만 켬. 다른 패널 상태는 그대로.</summary>
        public void Show(string panelId)
        {
            if (!_panelMap.TryGetValue(panelId, out var panel))
            {
                Debug.LogWarning($"[UIManager] 패널 없음: {panelId}");
                return;
            }
            panel.SetActive(true);
        }

        /// <summary>지정 패널 끔.</summary>
        public void Hide(string panelId)
        {
            if (!_panelMap.TryGetValue(panelId, out var panel))
            {
                Debug.LogWarning($"[UIManager] 패널 없음: {panelId}");
                return;
            }
            panel.SetActive(false);
        }

        public bool IsVisible(string panelId) =>
            _panelMap.TryGetValue(panelId, out var panel) && panel.activeSelf;

        public string CurrentPanel => _currentPanelId;

        // ── 유틸 ──────────────────────────────────────────

        private static async UniTask FadeCanvasGroupAsync(
            CanvasGroup cg, float from, float to, float duration)
        {
            float elapsed = 0f;
            cg.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
                await UniTask.Yield();
            }
            cg.alpha = to;
        }
    }
}
