using System;
using Newtonsoft.Json;

namespace Guideon.Network.Models
{
    // POST /kiosk/auth/verify 응답
    [Serializable]
    public class VerifyResponse
    {
        [JsonProperty("deviceId")] public string DeviceId;
        [JsonProperty("siteId")] public long SiteId;
        [JsonProperty("zoneId")] public long? ZoneId;
    }

    // GET /kiosk/bootstrap 응답
    [Serializable]
    public class BootstrapResponse
    {
        [JsonProperty("deviceId")] public string DeviceId;
        [JsonProperty("siteId")] public long SiteId;
        [JsonProperty("siteName")] public string SiteName;
        [JsonProperty("zoneId")] public long? ZoneId;
        [JsonProperty("zoneName")] public string ZoneName;
        [JsonProperty("mascot")] public MascotData Mascot;
    }

    [Serializable]
    public class MascotData
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("modelId")] public string ModelId;
        [JsonProperty("modelUrl")] public string ModelUrl;
        [JsonProperty("modelFormat")] public string ModelFormat;
        [JsonProperty("defaultAnim")] public string DefaultAnim;
        [JsonProperty("greetingMsg")] public string GreetingMsg;
        [JsonProperty("ttsVoiceId")] public string TtsVoiceId;
        [JsonProperty("ttsVoiceJson")] public TtsVoiceSettings TtsVoiceJson;
    }

    [Serializable]
    public class TtsVoiceSettings
    {
        [JsonProperty("speed")] public float Speed = 1.0f;
        [JsonProperty("pitch")] public float Pitch = 0f;
    }

    // GET /kiosk/mascot 응답 (bootstrap보다 상세 — imageUrl 포함)
    [Serializable]
    public class MascotDetailResponse
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("modelId")] public string ModelId;
        [JsonProperty("imageUrl")] public string ImageUrl;
        [JsonProperty("modelUrl")] public string ModelUrl;
        [JsonProperty("modelFormat")] public string ModelFormat;
        [JsonProperty("defaultAnim")] public string DefaultAnim;
        [JsonProperty("greetingMsg")] public string GreetingMsg;
        [JsonProperty("ttsVoiceId")] public string TtsVoiceId;
        [JsonProperty("ttsVoiceJson")] public TtsVoiceSettings TtsVoiceJson;
    }

    // POST /kiosk/heartbeat 요청
    [Serializable]
    public class HeartbeatRequest
    {
        [JsonProperty("version")] public string Version;
        [JsonProperty("errorCode")] public string ErrorCode;
    }
}
