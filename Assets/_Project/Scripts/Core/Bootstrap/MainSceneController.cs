using Cysharp.Threading.Tasks;
using Guideon.Chat;
using Guideon.Network;
using Guideon.UI;
using UnityEngine;

namespace Guideon.Core
{
    /// <summary>
    /// Main 씬 진입점. IdlePanel ↔ ChatPanel 전환을 관리.
    /// 인증/부트스트랩은 BootSceneController가 끝낸 상태로 진입한다고 가정.
    /// </summary>
    public class MainSceneController : MonoBehaviour
    {
        [SerializeField] private IdlePanel _idlePanel;

        private void OnEnable()
        {
            EventBus.Subscribe<UserTouchedEvent>(OnUserTouched);
            EventBus.Subscribe<IdleTimeoutEvent>(OnIdleTimeout);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UserTouchedEvent>(OnUserTouched);
            EventBus.Unsubscribe<IdleTimeoutEvent>(OnIdleTimeout);
        }

        private void Start()
        {
            ApplyBootstrapData();
            UIManager.Instance.ShowOnly(UIManager.Panel.Idle);
        }

        private void ApplyBootstrapData()
        {
            if (_idlePanel == null) return;
            var data = AuthManager.HasInstance ? AuthManager.Instance.BootstrapData : null;
            if (data != null && !string.IsNullOrEmpty(data.SiteName))
                _idlePanel.SetSiteName(data.SiteName);
        }

        // ── IdlePanel → ChatPanel ─────────────────────────

        private void OnUserTouched(UserTouchedEvent _)
        {
            EnterChatAsync().Forget();
        }

        private async UniTaskVoid EnterChatAsync()
        {
            if (UIManager.Instance.IsVisible(UIManager.Panel.Chat)) return;

            await UIManager.Instance.TransitionToAsync(UIManager.Panel.Chat);

            await ChatManager.Instance.CreateSessionAsync();
            IdleTimeoutManager.Instance.Begin();
        }

        // ── ChatPanel → IdlePanel ─────────────────────────

        private void OnIdleTimeout(IdleTimeoutEvent _)
        {
            ReturnToIdleAsync().Forget();
        }

        private async UniTaskVoid ReturnToIdleAsync()
        {
            IdleTimeoutManager.Instance.Stop();
            ChatManager.Instance.EndSession();
            await UIManager.Instance.TransitionToAsync(UIManager.Panel.Idle);
        }
    }
}