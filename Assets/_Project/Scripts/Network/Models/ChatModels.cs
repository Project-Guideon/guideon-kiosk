using System;
using Newtonsoft.Json;

namespace Guideon.Network.Models
{
    // POST /kiosk/chat/sessions 응답
    [Serializable]
    public class ChatSessionResponse
    {
        [JsonProperty("sessionId")] public string SessionId;
    }

    // POST /kiosk/chat/sessions/{id}/messages 요청
    [Serializable]
    public class ChatMessageRequest
    {
        [JsonProperty("message")] public string Message;
        [JsonProperty("language")] public string Language;
    }

    // POST /kiosk/chat/sessions/{id}/messages 응답
    [Serializable]
    public class ChatMessageResponse
    {
        [JsonProperty("sessionId")] public string SessionId;
        [JsonProperty("answer")] public string Answer;
        [JsonProperty("emotion")] public string Emotion;
        [JsonProperty("language")] public string Language;
        [JsonProperty("display")] public DisplayHint Display;
    }

    [Serializable]
    public class DisplayHint
    {
        [JsonProperty("type")] public string Type; // "PLACE"
        [JsonProperty("placeId")] public long PlaceId;
        [JsonProperty("placeName")] public string PlaceName;
        [JsonProperty("imageUrl")] public string ImageUrl;
        [JsonProperty("latitude")] public double Latitude;
        [JsonProperty("longitude")] public double Longitude;
    }
}
