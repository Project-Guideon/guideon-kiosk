using System;
using System.Text;
using Cysharp.Threading.Tasks;
using Guideon.Core;
using Guideon.Network.Models;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Guideon.Network
{
    /// <summary>
    /// Kiosk BFF REST API 통신 클라이언트.
    /// UnityWebRequest + UniTask 기반, 공통 헤더 자동 주입.
    /// </summary>
    public class ApiClient
    {
        private readonly ConfigManager _config;

        public ApiClient(ConfigManager config)
        {
            _config = config;
        }

        // ── Public API ────────────────────────────────────

        /// <summary>GET 요청. 인증 헤더 포함.</summary>
        public UniTask<ApiResponse<T>> GetAsync<T>(string endpoint)
        {
            return SendAsync<T>("GET", endpoint, null, useAuth: true);
        }

        /// <summary>POST 요청. 인증 헤더 포함.</summary>
        public UniTask<ApiResponse<T>> PostAsync<T>(string endpoint, object body = null)
        {
            return SendAsync<T>("POST", endpoint, body, useAuth: true);
        }

        /// <summary>PATCH 요청. 인증 헤더 포함.</summary>
        public UniTask<ApiResponse<T>> PatchAsync<T>(string endpoint, object body = null)
        {
            return SendAsync<T>("PATCH", endpoint, body, useAuth: true);
        }

        /// <summary>인증 없이 GET 요청. 페어링 API용.</summary>
        public UniTask<ApiResponse<T>> GetNoAuthAsync<T>(string endpoint)
        {
            return SendAsync<T>("GET", endpoint, null, useAuth: false);
        }

        /// <summary>인증 없이 POST 요청. 페어링 API용.</summary>
        public UniTask<ApiResponse<T>> PostNoAuthAsync<T>(string endpoint, object body = null)
        {
            return SendAsync<T>("POST", endpoint, body, useAuth: false);
        }

        // ── Core ──────────────────────────────────────────

        private async UniTask<ApiResponse<T>> SendAsync<T>(
            string method, string endpoint, object body, bool useAuth)
        {
            string url = _config.Config.server.baseUrl + endpoint;

            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = _config.Config.server.timeout;

            // JSON body
            if (body != null)
            {
                string json = JsonConvert.SerializeObject(body);
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            // 인증 헤더
            if (useAuth)
            {
                var device = _config.Config.device;
                request.SetRequestHeader("X-Device-Id", device.id);
                request.SetRequestHeader("X-Device-Token", device.token);
            }

            // 전송
            try
            {
                await request.SendWebRequest();
            }
            catch (UnityWebRequestException e)
            {
                Debug.LogError($"[ApiClient] {method} {endpoint} → Network error: {e.Message}");
                PublishNetworkError(e.Message, 0);
                return CreateErrorResponse<T>("NETWORK_ERROR", e.Message);
            }

            // 응답 파싱
            string responseText = request.downloadHandler.text;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[ApiClient] {method} {endpoint} → {request.responseCode}: {responseText}");
                return HandleErrorResponse<T>(responseText, (int)request.responseCode);
            }

            try
            {
                var response = JsonConvert.DeserializeObject<ApiResponse<T>>(responseText);
                return response;
            }
            catch (JsonException e)
            {
                Debug.LogError($"[ApiClient] JSON parse error: {e.Message}\nBody: {responseText}");
                return CreateErrorResponse<T>("PARSE_ERROR", "응답 파싱 실패");
            }
        }

        // ── Error Handling ────────────────────────────────

        private ApiResponse<T> HandleErrorResponse<T>(string responseText, int statusCode)
        {
            // 서버 에러 응답 (JSON envelope) 파싱 시도
            try
            {
                var errorResponse = JsonConvert.DeserializeObject<ApiResponse<T>>(responseText);
                if (errorResponse != null)
                {
                    string errorCode = errorResponse.Error?.Code ?? "UNKNOWN";
                    string errorMsg = errorResponse.Error?.Message ?? responseText;
                    PublishNetworkError($"[{errorCode}] {errorMsg}", statusCode);
                    return errorResponse;
                }
            }
            catch (JsonException)
            {
                // JSON 아닌 에러 응답 (HTML 등)
            }

            PublishNetworkError(responseText, statusCode);
            return CreateErrorResponse<T>("HTTP_ERROR", $"HTTP {statusCode}");
        }

        private static ApiResponse<T> CreateErrorResponse<T>(string code, string message)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Data = default,
                Error = new ApiError { Code = code, Message = message }
            };
        }

        private static void PublishNetworkError(string message, int statusCode)
        {
            EventBus.Publish(new NetworkErrorEvent
            {
                Message = message,
                StatusCode = statusCode
            });
        }
    }
}
