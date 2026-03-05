using UnityEngine;

namespace Guideon.Core
{
    /// <summary>
    /// 씬 전환 시에도 유지되는 싱글톤 베이스 클래스.
    /// 모든 Manager 클래스는 이 클래스를 상속받아 사용한다.
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                    Debug.LogError($"[Singleton] {typeof(T).Name} instance not found in scene.");
                return _instance;
            }
        }

        public static bool HasInstance => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = (T)this;
            DontDestroyOnLoad(gameObject);
            OnInitialize();
        }

        /// <summary>
        /// 싱글톤 초기화 시 호출. Awake 대신 이 메서드를 오버라이드한다.
        /// </summary>
        protected virtual void OnInitialize() { }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}