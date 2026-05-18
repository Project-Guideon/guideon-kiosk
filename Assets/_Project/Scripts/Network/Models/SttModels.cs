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

    // Client → Server: 스트림 시작 (JSON)
    [Serializable]
    public class SttStartMessage
    {
        [JsonProperty("type")] public string Type = "start";
        [JsonProperty("language")] public string Language;
        [JsonProperty("sampleRate")] public int SampleRate;
        [JsonProperty("encoding")] public string Encoding = "pcm16";
    }

    // Client → Server: 스트림 종료 (JSON)
    [Serializable]
    public class SttStopMessage
    {
        [JsonProperty("type")] public string Type = "stop";
    }
}
