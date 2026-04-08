using System;
using Newtonsoft.Json;

namespace Guideon.Network.Models
{
    [Serializable]
    public class ApiResponse<T>
    {
        [JsonProperty("success")] public bool Success;
        [JsonProperty("data")] public T Data;
        [JsonProperty("error")] public ApiError Error;
        [JsonProperty("trace_id")] public string TraceId;
    }

    [Serializable]
    public class ApiError
    {
        [JsonProperty("code")] public string Code;
        [JsonProperty("message")] public string Message;
        [JsonProperty("details")] public object Details;
    }
}
