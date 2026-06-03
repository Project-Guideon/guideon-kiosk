using System;
using System.Collections.Generic;
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

        // ── 애니메이션 (animate_retarget 파이프라인 완료 시 채워짐) ──
        /// <summary>상태별 클립 5개 내장 GLB URL. retarget 미완료/실패 시 null.</summary>
        [JsonProperty("animModelUrl")] public string AnimModelUrl;
        /// <summary>키오스크 상태 → GLB 내부 클립명 맵. null/빈 맵이면 순서 폴백으로 재생.</summary>
        [JsonProperty("animClips")] public Dictionary<string, string> AnimClips;
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

        // ── 애니메이션 (animate_retarget 파이프라인 완료 시 채워짐) ──
        /// <summary>상태별 클립 5개 내장 GLB URL. retarget 미완료/실패 시 null.</summary>
        [JsonProperty("animModelUrl")] public string AnimModelUrl;
        /// <summary>키오스크 상태 → GLB 내부 클립명 맵. null/빈 맵이면 순서 폴백으로 재생.</summary>
        [JsonProperty("animClips")] public Dictionary<string, string> AnimClips;
    }

    // POST /kiosk/heartbeat 요청
    [Serializable]
    public class HeartbeatRequest
    {
        [JsonProperty("version")] public string Version;
        [JsonProperty("errorCode")] public string ErrorCode;
    }
}
