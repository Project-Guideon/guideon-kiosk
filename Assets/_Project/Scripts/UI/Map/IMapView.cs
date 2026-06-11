using UnityEngine;

namespace Guideon.UI.Map
{
    /// <summary>
    /// 플랫폼별 웹뷰 구현을 감추는 인터페이스.
    /// Android → GreeMapView (gree/unity-webview)
    /// Editor/Windows → PlaceholderMapView (URL 표시 + OpenURL 폴백)
    /// </summary>
    public interface IMapView
    {
        /// <summary>url을 screenRect 영역에 표시한다. 이미 열려있으면 URL만 교체.</summary>
        void Show(string url, Rect screenRect);

        /// <summary>표시 영역을 변경한다 (화면 회전/리사이즈 대응).</summary>
        void SetRect(Rect screenRect);

        /// <summary>웹뷰를 숨긴다. 잔상 없이 완전히 가려야 한다.</summary>
        void Hide();

        /// <summary>웹뷰 오브젝트를 파괴한다. IDisposable 대신 Unity 친화적 방식.</summary>
        void Dispose();
    }
}
