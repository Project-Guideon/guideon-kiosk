using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Guideon.Core
{
    /// <summary>
    /// 앱 전역 설정 로더/세이버.
    ///
    /// 로드 우선순위:
    ///   1. persistentDataPath/config.json   — 저장된 사용자 설정 (페어링 토큰 포함)
    ///   2. StreamingAssets/config.local.json — 개발용 로컬 오버라이드
    ///   3. StreamingAssets/config.json      — 번들 기본값
    ///
    /// 저장은 항상 persistentDataPath/config.json 으로 — Android StreamingAssets는 읽기 전용.
    /// StreamingAssets 읽기는 UnityWebRequest 사용 (Android JAR URI 대응).
    /// </summary>
    public class ConfigManager : MonoSingleton<ConfigManager>
    {
        public AppConfig Config { get; private set; }
        public bool IsLoaded { get; private set; }

        private string _savePath;

        protected override void OnInitialize()
        {
            _savePath = Path.Combine(Application.persistentDataPath, "config.json");
            LoadAsync().Forget();
        }

        public bool HasDeviceCredentials =>
            !string.IsNullOrEmpty(Config?.device?.id) &&
            !string.IsNullOrEmpty(Config?.device?.token);

        public async UniTask LoadAsync()
        {
            string json = null;

            // 1. 저장된 사용자 설정 (페어링 토큰이 여기에 영속화됨)
            if (File.Exists(_savePath))
            {
                json = await File.ReadAllTextAsync(_savePath);
                Debug.Log("[ConfigManager] Loaded from persistentDataPath/config.json");
            }

            // 2. StreamingAssets — config.local.json (개발용) → config.json (기본값)
            if (json == null)
            {
                json = await TryReadStreamingAssetAsync("config.local.json");
                if (json != null)
                    Debug.Log("[ConfigManager] Loaded from StreamingAssets/config.local.json");
            }

            if (json == null)
            {
                json = await TryReadStreamingAssetAsync("config.json");
                if (json != null)
                    Debug.Log("[ConfigManager] Loaded from StreamingAssets/config.json");
            }

            if (json == null)
            {
                Debug.LogError("[ConfigManager] config.json not found in any location!");
                return;
            }

            Config = JsonConvert.DeserializeObject<AppConfig>(json);
            IsLoaded = true;
        }

        /// <summary>현재 Config를 persistentDataPath/config.json에 저장.</summary>
        public async UniTask SaveConfigAsync()
        {
            if (Config == null) return;

            var serializerSettings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            string json = JsonConvert.SerializeObject(Config, serializerSettings);
            await File.WriteAllTextAsync(_savePath, json);

            Debug.Log("[ConfigManager] Config saved to persistentDataPath/config.json");
        }

        public async UniTask SaveDeviceCredentialsAsync(string deviceId, string plainToken)
        {
            Config.device.id = deviceId;
            Config.device.token = plainToken;
            await SaveConfigAsync();
        }

        public async UniTask ResetDeviceCredentialsAsync()
        {
            Config.device.id = "";
            Config.device.token = "";
            await SaveConfigAsync();
            Debug.Log("[ConfigManager] Device credentials cleared.");
        }

        // ── 내부 ────────────────────────────────────────────

        /// <summary>
        /// StreamingAssets 파일을 UnityWebRequest로 읽는다.
        /// Android: streamingAssetsPath = "jar:file://..." → System.IO.File 불가, UWR 필수.
        /// </summary>
        private static async UniTask<string> TryReadStreamingAssetAsync(string filename)
        {
            string uri = BuildStreamingUri(filename);
            using var req = UnityWebRequest.Get(uri);
            await req.SendWebRequest();
            return req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null;
        }

        private static string BuildStreamingUri(string filename)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Android: streamingAssetsPath이 이미 "jar:file://..." 포함
            return $"{Application.streamingAssetsPath}/{filename}";
#else
            return $"file://{Path.Combine(Application.streamingAssetsPath, filename).Replace('\\', '/')}";
#endif
        }
    }
}
