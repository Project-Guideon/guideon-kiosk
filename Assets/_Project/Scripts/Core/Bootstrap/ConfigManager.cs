using System.IO;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Guideon.Core
{
    /// <summary>
    /// StreamingAssets/config.json을 로드해서 앱 전역 설정을 제공.
    /// config.local.json이 있으면 그게 우선 (로컬 개발용).
    /// </summary>
    public class ConfigManager : MonoSingleton<ConfigManager>
    {
        public AppConfig Config { get; private set; }
        public bool IsLoaded { get; private set; }

        private string _loadedPath;

        public bool HasDeviceCredentials =>
            !string.IsNullOrEmpty(Config?.device?.id) &&
            !string.IsNullOrEmpty(Config?.device?.token);

        public async UniTask LoadAsync()
        {
            string localPath = Path.Combine(Application.streamingAssetsPath, "config.local.json");
            string defaultPath = Path.Combine(Application.streamingAssetsPath, "config.json");
            _loadedPath = File.Exists(localPath) ? localPath : defaultPath;

            if (!File.Exists(_loadedPath))
            {
                Debug.LogError($"[ConfigManager] config.json not found at: {_loadedPath}");
                return;
            }

            string json = await File.ReadAllTextAsync(_loadedPath);
            Config = JsonConvert.DeserializeObject<AppConfig>(json);
            IsLoaded = true;

            Debug.Log($"[ConfigManager] Loaded from: {Path.GetFileName(_loadedPath)}");
        }

        /// <summary>
        /// 페어링 완료 후 device.id와 device.token을 config 파일에 저장.
        /// </summary>
        public async UniTask SaveDeviceCredentialsAsync(string deviceId, string plainToken)
        {
            Config.device.id = deviceId;
            Config.device.token = plainToken;

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            };
            string json = JsonConvert.SerializeObject(Config, settings);
            await File.WriteAllTextAsync(_loadedPath, json);

            Debug.Log($"[ConfigManager] Device credentials saved to: {Path.GetFileName(_loadedPath)}");
        }
    }
}
