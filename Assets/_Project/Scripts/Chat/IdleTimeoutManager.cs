using Guideon.Core;
using UnityEngine;

namespace Guideon.Chat
{
    /// <summary>
    /// 사용자 무응답 시간을 추적해 일정 시간 경과 시 IdleTimeoutEvent 발행.
    /// ChatPanel 진입 시 Begin(), IdlePanel 복귀 시 Stop() 으로 카운트다운을 직접 제어한다.
    /// </summary>
    public class IdleTimeoutManager : MonoSingleton<IdleTimeoutManager>
    {
        private float _lastInteractionTime;
        private bool _running;
        private int _timeoutSeconds;

        protected override void OnInitialize()
        {
            EventBus.Subscribe<ChatResponseEvent>(OnChatResponse);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<ChatResponseEvent>(OnChatResponse);
            base.OnDestroy();
        }

        /// <summary>카운트다운 시작. ChatPanel 진입 시 호출.</summary>
        public void Begin()
        {
            _timeoutSeconds = ConfigManager.HasInstance
                ? ConfigManager.Instance.Config.kiosk.idleTimeoutSeconds
                : 120;
            _lastInteractionTime = Time.time;
            _running = true;
            Debug.Log($"[IdleTimeoutManager] 시작 ({_timeoutSeconds}초)");
        }

        /// <summary>카운트다운 정지. IdlePanel 복귀 시 호출.</summary>
        public void Stop()
        {
            _running = false;
        }

        /// <summary>인터랙션 발생 시 카운트다운 리셋.</summary>
        public void NotifyInteraction()
        {
            _lastInteractionTime = Time.time;
        }

        // AI 응답이 도착해도 활성 인터랙션으로 간주 — 답변을 읽는 시간을 확보한다.
        private void OnChatResponse(ChatResponseEvent _) => NotifyInteraction();

        private void Update()
        {
            if (!_running) return;
            if (Time.time - _lastInteractionTime < _timeoutSeconds) return;

            _running = false;
            Debug.Log("[IdleTimeoutManager] 타임아웃 → IdleTimeoutEvent 발행");
            EventBus.Publish(new IdleTimeoutEvent());
        }
    }
}