using Cysharp.Threading.Tasks;
using Guideon.Core;
using Guideon.Network;
using Guideon.Network.Models;
using UnityEngine;

namespace Guideon.Chat
{
    /// <summary>
    /// 채팅 세션 생명주기 + 메시지 송수신 담당.
    /// 키오스크 사용자 1명당 세션 1개 — 타임아웃/이탈 시 폐기, 다음 사용자에게 새로 발급.
    /// </summary>
    public class ChatManager : MonoSingleton<ChatManager>
    {
        private ApiClient _api;

        public string CurrentSessionId { get; private set; }
        public bool HasActiveSession => !string.IsNullOrEmpty(CurrentSessionId);
        public bool IsWaitingForResponse { get; private set; }

        private void EnsureApiClient()
        {
            _api ??= new ApiClient(ConfigManager.Instance);
        }

        /// <summary>새 채팅 세션 생성. POST /kiosk/chat/sessions.</summary>
        public async UniTask<bool> CreateSessionAsync()
        {
            EnsureApiClient();

            var response = await _api.PostAsync<ChatSessionResponse>(KioskApiEndpoints.ChatSessions);

            if (!response.Success || response.Data == null || string.IsNullOrEmpty(response.Data.SessionId))
            {
                Debug.LogError($"[ChatManager] 세션 생성 실패: {response.Error?.Code} — {response.Error?.Message}");
                return false;
            }

            CurrentSessionId = response.Data.SessionId;
            Debug.Log($"[ChatManager] 세션 생성 완료 — {CurrentSessionId}");
            return true;
        }

        /// <summary>
        /// 사용자 메시지를 서버로 전송하고 AI 응답을 ChatResponseEvent로 발행.
        /// 세션이 없으면 자동으로 생성한다.
        /// </summary>
        public async UniTask<bool> SendMessageAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!HasActiveSession)
            {
                if (!await CreateSessionAsync()) return false;
            }

            EnsureApiClient();
            IsWaitingForResponse = true;

            try
            {
                var body = new ChatMessageRequest
                {
                    Message = text,
                    Language = ConfigManager.Instance.Config.kiosk.language
                };

                var response = await _api.PostAsync<ChatMessageResponse>(
                    KioskApiEndpoints.ChatMessages(CurrentSessionId), body);

                if (!response.Success || response.Data == null)
                {
                    Debug.LogError($"[ChatManager] 메시지 전송 실패: {response.Error?.Code} — {response.Error?.Message}");
                    return false;
                }

                var data = response.Data;
                Debug.Log($"[ChatManager] 응답 수신 — emotion: {data.Emotion}, " +
                          $"display: {(data.Display != null ? data.Display.PlaceName : "null")}");

                EventBus.Publish(new ChatResponseEvent
                {
                    SessionId = data.SessionId,
                    Answer = data.Answer,
                    Emotion = data.Emotion,
                    Language = data.Language,
                    Display = data.Display
                });

                return true;
            }
            finally
            {
                IsWaitingForResponse = false;
            }
        }

        /// <summary>현재 세션 폐기. 타임아웃 또는 사용자 이탈 시 호출.</summary>
        public void EndSession()
        {
            if (!HasActiveSession) return;
            Debug.Log($"[ChatManager] 세션 종료 — {CurrentSessionId}");
            CurrentSessionId = null;
        }
    }
}