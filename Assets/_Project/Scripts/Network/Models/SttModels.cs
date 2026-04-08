using System;
using Newtonsoft.Json;

namespace Guideon.Network.Models
{
    // Server → Client: STT 인식 결과 (JSON)
    [Serializable]
    public class SttMessage
    {
        [JsonProperty("type")] public string Type; // "partial" or "final"
        [JsonProperty("transcript")] public string Transcript;
        [JsonProperty("confidence")] public float Confidence;
        [JsonProperty("sessionId")] public string SessionId;
    }
}
