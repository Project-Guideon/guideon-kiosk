using System;
using UnityEngine;

namespace Guideon.UI.Map
{
    /// <summary>
    /// Android 외 환경(에디터, Windows)용 폴백 뷰.
    /// 네이티브 웹뷰가 없으므로 ChatScreenController가 직접 map-placeholder에
    /// URL 텍스트를 표시하고 OpenURL 버튼을 노출한다.
    ///
    /// IMapView 인터페이스만 맞추고 실제 렌더는 ChatScreenController가 UI로 처리.
    /// </summary>
    public class PlaceholderMapView : IMapView
    {
        public event Action<string> OnUrlChanged;

        private string _currentUrl;

        public string CurrentUrl => _currentUrl;

        public void Show(string url, Rect screenRect)
        {
            _currentUrl = url;
            OnUrlChanged?.Invoke(url);
        }

        public void SetRect(Rect screenRect)
        {
            // 폴백은 UI Toolkit 레이아웃으로 크기가 결정됨 — 무시
        }

        public void Hide()
        {
            _currentUrl = null;
            OnUrlChanged?.Invoke(null);
        }

        public void Dispose()
        {
            OnUrlChanged = null;
        }

        /// <summary>현재 URL을 브라우저/카카오맵 앱으로 열기.</summary>
        public void OpenInBrowser()
        {
            if (!string.IsNullOrEmpty(_currentUrl))
                Application.OpenURL(_currentUrl);
        }
    }
}
