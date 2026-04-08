using Cysharp.Threading.Tasks;
using Guideon.Network;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Guideon.Core
{
    /// <summary>
    /// 앱 생명주기와 씬 전환 담당. Boot 씬에서 생성되고 이후 전 씬에서 유지됨.
    /// 부팅 시 토큰 유무에 따라 페어링/인증 흐름을 분기한다.
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        public static class Scenes
        {
            public const string Boot = "Boot";
            public const string Main = "Main";
        }

        public enum BootState
        {
            Initializing,
            NeedsPairing,
            Authenticating,
            Ready,
            Error
        }

        public BootState CurrentBootState { get; private set; } = BootState.Initializing;

        protected override void OnInitialize()
        {
            Debug.Log("[GameManager] Initialized.");
        }

        /// <summary>
        /// Boot 씬에서 호출. Config 로드 → 페어링 or 인증 → Main 씬 전환.
        /// </summary>
        public async UniTask RunBootSequenceAsync()
        {
            CurrentBootState = BootState.Initializing;

            // 1. Config 로드
            await ConfigManager.Instance.LoadAsync();
            if (!ConfigManager.Instance.IsLoaded)
            {
                CurrentBootState = BootState.Error;
                Debug.LogError("[GameManager] Config 로드 실패. 부팅 중단.");
                return;
            }

            // 2. 토큰 유무 확인 → 분기
            if (!ConfigManager.Instance.HasDeviceCredentials)
            {
                // 페어링 필요
                CurrentBootState = BootState.NeedsPairing;
                Debug.Log("[GameManager] 토큰 없음 → 페어링 흐름 진입");

                bool paired = await PairingManager.Instance.RunPairingFlowAsync();
                if (!paired)
                {
                    CurrentBootState = BootState.Error;
                    Debug.LogError("[GameManager] 페어링 실패. 부팅 중단.");
                    return;
                }
            }

            // 3. 인증 (verify → bootstrap)
            CurrentBootState = BootState.Authenticating;
            Debug.Log("[GameManager] 인증 흐름 시작");

            bool authenticated = await AuthManager.Instance.AuthenticateAsync();
            if (!authenticated)
            {
                CurrentBootState = BootState.Error;
                Debug.LogError("[GameManager] 인증 실패. 부팅 중단.");
                return;
            }

            // 4. 하트비트 시작
            HeartbeatService.Instance.StartHeartbeat();

            // 5. 준비 완료 → Main 씬 전환
            CurrentBootState = BootState.Ready;
            Debug.Log("[GameManager] 부팅 완료 → Main 씬 전환");
            await LoadSceneAsync(Scenes.Main);
        }

        public async UniTask LoadSceneAsync(string sceneName)
        {
            Debug.Log($"[GameManager] Loading scene: {sceneName}");
            await SceneManager.LoadSceneAsync(sceneName);
            EventBus.Publish(new SceneReadyEvent { SceneName = sceneName });
        }

        private void OnApplicationQuit()
        {
            HeartbeatService.Instance.StopHeartbeat();
            Debug.Log("[GameManager] Application quit.");
        }
    }
}
