namespace Guideon.UI.Map
{
    /// <summary>
    /// 빌드 플랫폼에 따라 적절한 IMapView 구현을 생성한다.
    /// </summary>
    public static class MapViewFactory
    {
        public static IMapView Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new GreeMapView();
#else
            return new PlaceholderMapView();
#endif
        }
    }
}
