using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guideon.UI.Toolkit;
using UnityEngine;

namespace Guideon.UI
{
    public class UIManager : Core.MonoSingleton<UIManager>
    {
        public static class Panel
        {
            public const string Boot    = "Boot";
            public const string Pairing = "Pairing";
            public const string Idle    = "Idle";
            public const string Chat    = "Chat";
            public const string Guide   = "Guide";
            public const string Error   = "Error";
        }

        [Serializable]
        public struct PanelEntry
        {
            public string id;
            public PanelControllerBase controller;
        }

        [SerializeField] private PanelEntry[] _panels;
        [SerializeField] private CanvasGroup _transitionOverlay;
        [SerializeField] private float _transitionDuration = 0.35f;

        private readonly Dictionary<string, PanelControllerBase> _controllerMap = new();
        private string _currentPanelId;
        private bool _isTransitioning;

        protected override void OnInitialize()
        {
            foreach (var entry in _panels)
            {
                if (string.IsNullOrEmpty(entry.id) || entry.controller == null)
                {
                    Debug.LogWarning("[UIManager] 비어있는 패널 항목 있음. Inspector 확인 요망.");
                    continue;
                }
                _controllerMap[entry.id] = entry.controller;
                entry.controller.gameObject.SetActive(false);
            }

            if (_transitionOverlay != null)
            {
                _transitionOverlay.alpha = 0f;
                _transitionOverlay.blocksRaycasts = false;
            }
        }

        public void ShowOnly(string panelId)
        {
            foreach (var kv in _controllerMap)
            {
                if (kv.Value == null) continue;
                kv.Value.gameObject.SetActive(kv.Key == panelId);
            }
            _currentPanelId = panelId;
        }

        public void BindPanel(string panelId, PanelControllerBase controller)
        {
            if (string.IsNullOrEmpty(panelId) || controller == null) return;
            _controllerMap[panelId] = controller;
        }

        public void BindPanels(PanelEntry[] entries)
        {
            if (entries == null) return;
            foreach (var e in entries)
                BindPanel(e.id, e.controller);
        }

        public void UnbindPanel(string panelId)
        {
            if (string.IsNullOrEmpty(panelId)) return;
            _controllerMap.Remove(panelId);
            if (_currentPanelId == panelId) _currentPanelId = null;
        }

        public async UniTask TransitionToAsync(string panelId)
        {
            if (_isTransitioning || panelId == _currentPanelId) return;
            _isTransitioning = true;

            if (_transitionOverlay != null)
            {
                _transitionOverlay.blocksRaycasts = true;
                await FadeCanvasGroupAsync(_transitionOverlay, 0f, 1f, _transitionDuration);
            }

            ShowOnly(panelId);

            if (_transitionOverlay != null)
            {
                await FadeCanvasGroupAsync(_transitionOverlay, 1f, 0f, _transitionDuration);
                _transitionOverlay.blocksRaycasts = false;
            }

            _isTransitioning = false;
        }

        public void Show(string panelId)
        {
            if (!_controllerMap.TryGetValue(panelId, out var controller) || controller == null)
            {
                Debug.LogWarning($"[UIManager] 패널 없음: {panelId}");
                return;
            }
            controller.gameObject.SetActive(true);
        }

        public void Hide(string panelId)
        {
            if (!_controllerMap.TryGetValue(panelId, out var controller) || controller == null)
            {
                Debug.LogWarning($"[UIManager] 패널 없음: {panelId}");
                return;
            }
            controller.gameObject.SetActive(false);
        }

        public bool IsVisible(string panelId) =>
            _controllerMap.TryGetValue(panelId, out var c) && c != null && c.gameObject.activeSelf;

        public string CurrentPanel => _currentPanelId;

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
