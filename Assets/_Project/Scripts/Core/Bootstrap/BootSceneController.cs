using Cysharp.Threading.Tasks;
using Guideon.Mascot;
using Guideon.UI;
using UnityEngine;

namespace Guideon.Core
{
    public class BootSceneController : MonoBehaviour
    {
        [SerializeField] private BootScreenController _bootScreen;
        [SerializeField] private PairingScreenController _pairingScreen;
        [SerializeField] private ErrorScreenController _errorScreen;

        private void Awake()
        {
            // Boot 씬 재로드 시 (재페어링 등) persistent UIManager의 패널 맵을 갱신한다.
            // UIManager는 DontDestroyOnLoad 싱글톤이라 OnInitialize가 다시 실행되지 않으므로
            // MainSceneController.Awake와 동일한 패턴으로 새 씬 패널을 명시적으로 재등록한다.
            // 최초 부팅 시에는 UIManager.OnInitialize가 _panels 직렬화 참조로 등록하므로 건너뜀.
            if (UIManager.HasInstance)
            {
                UIManager.Instance.BindPanel(UIManager.Panel.Boot,    _bootScreen);
                UIManager.Instance.BindPanel(UIManager.Panel.Pairing, _pairingScreen);
                UIManager.Instance.BindPanel(UIManager.Panel.Error,   _errorScreen);
            }
        }

        private async void Start()
        {
            await RunAsync();
        }

        private async UniTask RunAsync()
        {
            try
            {
                // Boot 패널 표시
                UIManager.Instance.ShowOnly(UIManager.Panel.Boot);
                _bootScreen.SetStatus("시스템 초기화 중...", 0.1f);

                await UniTask.Delay(500); // 스플래시 최소 표시 시간

                // Config 로드
                _bootScreen.SetStatus("설정 불러오는 중...", 0.2f);
                await ConfigManager.Instance.LoadAsync();

                if (!ConfigManager.Instance.IsLoaded)
                {
                    ShowError("설정 파일 오류", "config.json을 불러올 수 없습니다.\n관리자에게 문의하세요.");
                    return;
                }

                // 토큰 확인 → 분기
                if (!ConfigManager.Instance.HasDeviceCredentials)
                {
                    // 페어링 흐름
                    _bootScreen.SetStatus("디바이스 등록 필요", 0.3f);
                    await UniTask.Delay(300);

                    await UIManager.Instance.TransitionToAsync(UIManager.Panel.Pairing);

                    bool paired = await Network.PairingManager.Instance.RunPairingFlowAsync();
                    if (!paired)
                    {
                        ShowError("페어링 실패", "디바이스 등록에 실패했습니다.\n잠시 후 다시 시도합니다.",
                            onRetry: () => RunAsync().Forget());
                        return;
                    }

                    // 페어링 성공 → Boot 패널로 복귀
                    await UIManager.Instance.TransitionToAsync(UIManager.Panel.Boot);
                }

                // 인증
                _bootScreen.SetStatus("서버 인증 중...", 0.5f);
                bool verified = await Network.AuthManager.Instance.VerifyAsync();
                if (!verified)
                {
                    ShowError("인증 실패", "디바이스 토큰이 유효하지 않습니다.\n관리자에게 문의하세요.", "AUTH_INVALID",
                        onRetry: () => RunAsync().Forget());
                    return;
                }

                // 부트스트랩
                _bootScreen.SetStatus("키오스크 설정 로드 중...", 0.7f);
                bool bootstrapped = await Network.AuthManager.Instance.BootstrapAsync();
                if (!bootstrapped)
                {
                    ShowError("초기화 실패", "키오스크 설정을 불러올 수 없습니다.",
                        onRetry: () => RunAsync().Forget());
                    return;
                }

                // 마스코트 GLB 선행 다운로드 (animModelUrl 우선, 없으면 modelUrl, 없으면 로컬 fallback)
                _bootScreen.SetStatus("마스코트 불러오는 중...", 0.85f);
                var mascotData = Network.AuthManager.Instance.BootstrapData?.Mascot;
                await MascotLoader.PreloadAsync(mascotData);

                // 하트비트 시작
                _bootScreen.SetStatus("준비 완료!", 1.0f);
                Network.HeartbeatService.Instance.StartHeartbeat();

                await UniTask.Delay(600); // 완료 메시지 잠깐 표시

                // Main 씬 전환
                await GameManager.Instance.LoadSceneAsync(GameManager.Scenes.Main);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                ShowError("시스템 오류", "예상치 못한 오류가 발생했습니다.\n잠시 후 다시 시도합니다.",
                    onRetry: () => RunAsync().Forget());
            }
        }

        private void ShowError(string title, string message, string code = null, System.Action onRetry = null)
        {
            UIManager.Instance.ShowOnly(UIManager.Panel.Error);
            _errorScreen.Show(title, message, code, onRetry);
        }
    }
}

