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

        public async UniTask LoadAsync()
        {
            string localPath = Path.Combine(Application.streamingAssetsPath, "config.local.json");
            string defaultPath = Path.Combine(Application.streamingAssetsPath, "config.json");
            string path = File.Exists(localPath) ? localPath : defaultPath;

            if (!File.Exists(path))
            {
                Debug.LogError($"[ConfigManager] config.json not found at: {path}");
                return;
            }

            string json = await File.ReadAllTextAsync(path);
            Config = JsonConvert.DeserializeObject<AppConfig>(json);
            IsLoaded = true;

            Debug.Log($"[ConfigManager] Loaded from: {Path.GetFileName(path)}");
        }
    }
}
