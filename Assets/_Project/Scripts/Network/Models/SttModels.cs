using System;
using Newtonsoft.Json;

namespace Guideon.Network.Models
{
    // Server → Client: 서버에서 오는 모든 메시지 공용 모델
    // type: status | stt_interim | stt_final | llm_sentence | tts_chunk | final_text | error | done
    [Serializable]
    public class SttMessage
    {
        [JsonProperty("type")]          public string Type;
        [JsonProperty("text")]          public string Text;          // stt_interim, stt_final
        [JsonProperty("stage")]         public string Stage;         // status
        [JsonProperty("answer")]        public string Answer;        // final_text
        [JsonProperty("query")]         public string Query;         // final_text
        [JsonProperty("category")]      public string Category;      // final_text
        [JsonProperty("map_url")]       public string MapUrl;        // final_text (DIRECTION 카테고리)
        [JsonProperty("language_code")] public string LanguageCode;
        [JsonProperty("confidence")]    public float  Confidence;
        [JsonProperty("is_final")]      public bool   IsFinal;
        [JsonProperty("code")]          public string Code;          // error
        [JsonProperty("message")]       public string ErrorMessage;  // error
        [JsonProperty("trace_id")]      public string TraceId;

        // tts_chunk
        [JsonProperty("seq")]          public int    Seq;
        [JsonProperty("audio_format")] public string AudioFormat;
        [JsonProperty("audio_b64")]    public string AudioB64;
    }

    // Client → Server: 스트림 종료 (JSON)
    [Serializable]
    public class SttStopMessage
    {
        [JsonProperty("type")] public string Type = "stop";
    }
}
