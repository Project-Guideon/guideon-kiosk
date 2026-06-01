using Cysharp.Threading.Tasks;
using Guideon.Chat;
using Guideon.Mascot;
using Guideon.Network;
using Guideon.Network.Stt;
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
        [Header("Panels")]
        [SerializeField] private IdleScreenController _idleScreen;
        [SerializeField] private ChatScreenController _chatScreen;
        [Tooltip("Main 씬 패널들. UIManager에 동적 등록되어 Boot 씬의 동명 패널을 덮어쓴다.")]
        [SerializeField] private UIManager.PanelEntry[] _scenePanels;

        [Header("마스코트")]
        [SerializeField] private MascotStage _mascotStage;

        private void Awake()
        {
            // Boot 씬에서 등록됐던 IdlePanel/ErrorPanel 참조는 씬 전환으로 destroy됨.
            // Main 씬 패널을 같은 id로 다시 등록해서 UIManager가 정상 동작하도록 한다.
            if (UIManager.HasInstance)
                UIManager.Instance.BindPanels(_scenePanels);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UserTouchedEvent>(OnUserTouched);
            EventBus.Subscribe<IdleTimeoutEvent>(OnIdleTimeout);
            EventBus.Subscribe<ChatExitRequestedEvent>(OnChatExitRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UserTouchedEvent>(OnUserTouched);
            EventBus.Unsubscribe<IdleTimeoutEvent>(OnIdleTimeout);
            EventBus.Unsubscribe<ChatExitRequestedEvent>(OnChatExitRequested);
        }

        private void Start()
        {
            ApplyBootstrapData();
            UIManager.Instance.ShowOnly(UIManager.Panel.Idle);

            // RenderTexture를 Idle 화면 mascot-slot에 바인딩
            if (_mascotStage != null)
                _idleScreen?.SetMascotTexture(_mascotStage.Texture);
        }

        private void ApplyBootstrapData()
        {
            if (_idleScreen == null) return;
            var data = AuthManager.HasInstance ? AuthManager.Instance.BootstrapData : null;
            if (data != null && !string.IsNullOrEmpty(data.SiteName))
                _idleScreen.SetSiteName(data.SiteName);
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

            // Chat 화면이 활성화된 직후 RenderTexture 바인딩 + Greeting 재생
            if (_mascotStage != null)
            {
                _chatScreen?.SetMascotTexture(_mascotStage.Texture);
                _mascotStage.PlayGreeting();
            }

            await ChatManager.Instance.CreateSessionAsync();
            IdleTimeoutManager.Instance.Begin();

            if (SttManager.HasInstance)
                SttManager.Instance.StartAsync().Forget();
        }

        // ── ChatPanel → IdlePanel ─────────────────────────

        private void OnIdleTimeout(IdleTimeoutEvent _)
        {
            ReturnToIdleAsync().Forget();
        }

        private void OnChatExitRequested(ChatExitRequestedEvent _)
        {
            ReturnToIdleAsync().Forget();
        }

        private async UniTaskVoid ReturnToIdleAsync()
        {
            IdleTimeoutManager.Instance.Stop();
            ChatManager.Instance.EndSession();
            await UIManager.Instance.TransitionToAsync(UIManager.Panel.Idle);

            // Idle 복귀 시 마스코트 포즈 리셋
            _mascotStage?.ResetToIdle();
        }
    }
}