using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Guideon.Core
{
    /// <summary>
    /// 앱 생명주기와 씬 전환을 담당하는 최상위 매니저.
    /// Boot 씬에서 생성되며 이후 모든 씬에서 유지된다.
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        public static class Scenes
        {
            public const string Boot = "Boot";
            public const string Main = "Main";
        }

        protected override void OnInitialize()
        {
            Debug.Log("[GameManager] Initialized.");
        }

        public async UniTask LoadSceneAsync(string sceneName)
        {
            Debug.Log($"[GameManager] Loading scene: {sceneName}");
            await SceneManager.LoadSceneAsync(sceneName);
            EventBus.Publish(new SceneReadyEvent { SceneName = sceneName });
        }

        private void OnApplicationQuit()
        {
            Debug.Log("[GameManager] Application quit.");
        }
    }
}
