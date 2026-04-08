using System;
using Newtonsoft.Json;

namespace Guideon.Network.Models
{
    // POST /kiosk/pairing/request 응답
    [Serializable]
    public class PairingCodeResponse
    {
        [JsonProperty("pairingCode")] public string PairingCode;
        [JsonProperty("expiresAt")] public string ExpiresAt;
    }

    // GET /kiosk/pairing/{code}/status 응답
    [Serializable]
    public class PairingStatusResponse
    {
        [JsonProperty("pairingCode")] public string PairingCode;
        [JsonProperty("status")] public string Status; // WAITING, PAIRED, EXPIRED
    }

    // POST /kiosk/pairing/{code}/claim 응답
    [Serializable]
    public class PairingClaimResponse
    {
        [JsonProperty("pairingCode")] public string PairingCode;
        [JsonProperty("status")] public string Status;
        [JsonProperty("plainToken")] public string PlainToken;
        [JsonProperty("device")] public PairingDeviceInfo Device;
    }

    [Serializable]
    public class PairingDeviceInfo
    {
        [JsonProperty("deviceId")] public string DeviceId;
        [JsonProperty("siteId")] public long SiteId;
    }
}
