using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Guideon.Core
{
    /// <summary>
    /// 앱 생명주기와 씬 전환 담당. Boot 씬에서 생성되고 이후 전 씬에서 유지됨.
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
