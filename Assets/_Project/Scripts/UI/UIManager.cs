using System;
using System.Collections.Generic;
using UnityEngine;
using Guideon.Core;

namespace Guideon.UI
{
    /// <summary>
    /// 패널 Show/Hide 및 화면 전환 담당. 모든 UI 패널은 여기서 관리.
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        // 패널 ID 상수. 문자열 오타 방지용.
        public static class Panel
        {
            public const string Boot = "Boot";
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

        private readonly Dictionary<string, GameObject> _panelMap = new();

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
        }

        /// <summary>패널 하나만 활성화하고 나머지는 전부 끔.</summary>
        public void ShowOnly(string panelId)
        {
            foreach (var kv in _panelMap)
                kv.Value.SetActive(kv.Key == panelId);
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
    }
}
