#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;

namespace Guideon.UI.Map
{
    /// <summary>
    /// gree/unity-webview 기반 Android 네이티브 웹뷰.
    /// WebViewObject를 스크린 픽셀 좌표로 위치시켜 카카오맵 URL을 표시한다.
    /// 손가락 팬/핀치는 네이티브 처리 — 별도 코드 불필요.
    ///
    /// 주의: 네이티브 오버레이 방식이므로 Unity RenderTexture 마스코트와 합성 불가.
    ///        마스코트는 웹뷰 사각형 밖 별도 슬롯에 배치해 겹침을 방지한다.
    /// </summary>
    public class GreeMapView : IMapView
    {
        private WebViewObject _webView;
        private bool _loaded;

        public GreeMapView()
        {
            var go = new GameObject("KakaoMapWebView");
            Object.DontDestroyOnLoad(go);
            _webView = go.AddComponent<WebViewObject>();
            _webView.Init(
                cb: (msg) => Debug.Log($"[GreeMapView] JS→Unity: {msg}"),
                err: (msg) => Debug.LogWarning($"[GreeMapView] 오류: {msg}"),
                httpErr: (msg) => Debug.LogWarning($"[GreeMapView] HTTP 오류: {msg}"),
                ld: (msg) => { _loaded = true; Debug.Log($"[GreeMapView] 로드 완료: {msg}"); },
                enableWKWebView: true
            );
            _webView.SetVisibility(false);
        }

        public void Show(string url, Rect screenRect)
        {
            ApplyRect(screenRect);
            _webView.SetVisibility(true);
            _webView.LoadURL(url);
        }

        public void SetRect(Rect screenRect)
        {
            ApplyRect(screenRect);
        }

        public void Hide()
        {
            _webView?.SetVisibility(false);
        }

        public void Dispose()
        {
            if (_webView != null)
            {
                Object.Destroy(_webView.gameObject);
                _webView = null;
            }
        }

        // ── 내부 ────────────────────────────────────────────

        /// <summary>
        /// Rect(x,y=좌상단, width, height — 스크린 픽셀)를
        /// gree SetMargins(left, top, right, bottom)으로 변환.
        /// </summary>
        private void ApplyRect(Rect r)
        {
            int screenW = Screen.width;
            int screenH = Screen.height;
            int left   = Mathf.RoundToInt(r.x);
            int top    = Mathf.RoundToInt(r.y);
            int right  = Mathf.RoundToInt(screenW - r.xMax);
            int bottom = Mathf.RoundToInt(screenH - r.yMax);
            _webView.SetMargins(left, top, right, bottom);
        }
    }
}
#endif
