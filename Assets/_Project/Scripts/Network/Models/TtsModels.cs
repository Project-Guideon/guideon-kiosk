using System;
using Newtonsoft.Json;

namespace Guideon.Network.Models
{
    // Client → Server: TTS 요청 (JSON)
    [Serializable]
    public class TtsRequest
    {
        [JsonProperty("sessionId")] public string SessionId;
        [JsonProperty("text")] public string Text;
        [JsonProperty("language")] public string Language;
        [JsonProperty("voiceId")] public string VoiceId;
    }

    // Server → Client: TTS 문장별 오디오 청크 (JSON)
    [Serializable]
    public class TtsChunk
    {
        [JsonProperty("type")] public string Type; // "chunk" or "done"
        [JsonProperty("seq")] public int Seq;
        [JsonProperty("sentence")] public string Sentence;
        [JsonProperty("audioBase64")] public string AudioBase64;
        [JsonProperty("last")] public bool Last;
        [JsonProperty("sessionId")] public string SessionId;
    }
}
