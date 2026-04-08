using Cysharp.Threading.Tasks;
using Guideon.UI;
using UnityEngine;

namespace Guideon.Core
{
    /// <summary>
    /// Boot 씬의 진입점. 씬 로드 시 자동으로 부팅 시퀀스를 시작.
    /// BootPanel 상태 텍스트를 업데이트하면서 GameManager.RunBootSequenceAsync()를 실행.
    /// </summary>
    public class BootSceneController : MonoBehaviour
    {
        [SerializeField] private BootPanel _bootPanel;
        [SerializeField] private PairingPanel _pairingPanel;
        [SerializeField] private ErrorPanel _errorPanel;

        private async void Start()
        {
            await RunAsync();
        }

        private async UniTask RunAsync()
        {
            // Boot 패널 표시
            UIManager.Instance.ShowOnly(UIManager.Panel.Boot);
            _bootPanel.SetStatus("시스템 초기화 중...", 0.1f);

            await UniTask.Delay(500); // 스플래시 최소 표시 시간

            // Config 로드
            _bootPanel.SetStatus("설정 불러오는 중...", 0.2f);
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
                _bootPanel.SetStatus("디바이스 등록 필요", 0.3f);
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
            _bootPanel.SetStatus("서버 인증 중...", 0.5f);
            bool verified = await Network.AuthManager.Instance.VerifyAsync();
            if (!verified)
            {
                ShowError("인증 실패", "디바이스 토큰이 유효하지 않습니다.\n관리자에게 문의하세요.", "AUTH_INVALID",
                    onRetry: () => RunAsync().Forget());
                return;
            }

            // 부트스트랩
            _bootPanel.SetStatus("키오스크 설정 로드 중...", 0.7f);
            bool bootstrapped = await Network.AuthManager.Instance.BootstrapAsync();
            if (!bootstrapped)
            {
                ShowError("초기화 실패", "키오스크 설정을 불러올 수 없습니다.",
                    onRetry: () => RunAsync().Forget());
                return;
            }

            // 하트비트 시작
            _bootPanel.SetStatus("준비 완료!", 1.0f);
            Network.HeartbeatService.Instance.StartHeartbeat();

            await UniTask.Delay(600); // 완료 메시지 잠깐 표시

            // Main 씬 전환
            await GameManager.Instance.LoadSceneAsync(GameManager.Scenes.Main);
        }

        private void ShowError(string title, string message, string code = null, System.Action onRetry = null)
        {
            UIManager.Instance.ShowOnly(UIManager.Panel.Error);
            _errorPanel.Show(title, message, code, onRetry);
        }
    }
}
