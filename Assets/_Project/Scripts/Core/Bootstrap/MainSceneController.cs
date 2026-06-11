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

        [Header("관리자")]
        [SerializeField] private AdminScreenController _adminScreen;

        [Header("마스코트")]
        [SerializeField] private MascotStage _mascotStage;

        private void Awake()
        {
            // Boot 씬에서 등록됐던 IdlePanel/ErrorPanel 참조는 씬 전환으로 destroy됨.
            // Main 씬 패널을 같은 id로 다시 등록해서 UIManager가 정상 동작하도록 한다.
            if (UIManager.HasInstance)
            {
                UIManager.Instance.BindPanels(_scenePanels);

                // Admin 패널 — Inspector에 배선돼 있으면 그대로 쓰고,
                // 미배선(null)이면 씬에서 자동 탐색 (Setup 메뉴 실행 후 Inspector 재배선 생략 가능)
                if (_adminScreen == null)
                    _adminScreen = FindObjectOfType<AdminScreenController>();
                if (_adminScreen != null)
                    UIManager.Instance.BindPanel(UIManager.Panel.Admin, _adminScreen);
                else
                    Debug.LogWarning("[MainSceneController] AdminScreenController를 찾을 수 없음 — AdminScreen GameObject가 씬에 있는지 확인.");
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<UserTouchedEvent>(OnUserTouched);
            EventBus.Subscribe<IdleTimeoutEvent>(OnIdleTimeout);
            EventBus.Subscribe<ChatExitRequestedEvent>(OnChatExitRequested);
            EventBus.Subscribe<AdminRequestedEvent>(OnAdminRequested);
            EventBus.Subscribe<AdminClosedEvent>(OnAdminClosed);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<UserTouchedEvent>(OnUserTouched);
            EventBus.Unsubscribe<IdleTimeoutEvent>(OnIdleTimeout);
            EventBus.Unsubscribe<ChatExitRequestedEvent>(OnChatExitRequested);
            EventBus.Unsubscribe<AdminRequestedEvent>(OnAdminRequested);
            EventBus.Unsubscribe<AdminClosedEvent>(OnAdminClosed);
        }

        private void Start()
        {
            ApplyBootstrapData();
            UIManager.Instance.ShowOnly(UIManager.Panel.Idle);
            // 텍스처 바인딩은 화면 컨트롤러의 OnBindUI에서 MascotStage.Active 경유로 자동 처리됨
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
            if (UIManager.Instance.IsVisible(UIManager.Panel.Admin)) return;

            await UIManager.Instance.TransitionToAsync(UIManager.Panel.Chat);

            // Greeting 재생 (텍스처 바인딩은 ChatScreenController.OnBindUI에서 자동 처리)
            MascotStage.Active?.PlayGreeting();

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

            // Idle 복귀 시 마스코트 포즈 리셋 (MascotStage.Active로 Inspector 배선 불필요)
            MascotStage.Active?.ResetToIdle();
        }

        // ── Idle ↔ Admin ──────────────────────────────────────────

        private void OnAdminRequested(AdminRequestedEvent _)
        {
            EnterAdminAsync().Forget();
        }

        private async UniTaskVoid EnterAdminAsync()
        {
            if (UIManager.Instance.IsVisible(UIManager.Panel.Admin)) return;
            await UIManager.Instance.TransitionToAsync(UIManager.Panel.Admin);
        }

        private void OnAdminClosed(AdminClosedEvent _)
        {
            ReturnFromAdminAsync().Forget();
        }

        private async UniTaskVoid ReturnFromAdminAsync()
        {
            await UIManager.Instance.TransitionToAsync(UIManager.Panel.Idle);
        }
    }
}