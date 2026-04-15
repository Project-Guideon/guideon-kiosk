using Cysharp.Threading.Tasks;
using Guideon.Core;
using Guideon.Network.Models;
using UnityEngine;

namespace Guideon.Network
{
    /// <summary>
    /// 디바이스 인증(verify) 및 부트스트랩(bootstrap) 처리.
    /// 부팅 시 토큰 유효성 확인 → 키오스크 초기 설정 로드.
    /// </summary>
    public class AuthManager : MonoSingleton<AuthManager>
    {
        private ApiClient _api;

        /// <summary>verify 성공 후 저장되는 디바이스 컨텍스트.</summary>
        public VerifyResponse DeviceContext { get; private set; }

        /// <summary>bootstrap 성공 후 저장되는 키오스크 설정.</summary>
        public BootstrapResponse BootstrapData { get; private set; }

        public bool IsVerified { get; private set; }
        public bool IsBootstrapped { get; private set; }

        protected override void OnInitialize()
        {
            // ApiClient는 실제 사용 시점에 생성 (ConfigManager Awake 순서 보장 불가)
        }

        private void EnsureApiClient()
        {
            _api ??= new ApiClient(ConfigManager.Instance);
        }

        /// <summary>
        /// 디바이스 토큰 유효성 확인.
        /// POST /kiosk/auth/verify → DeviceContext 저장.
        /// </summary>
        public async UniTask<bool> VerifyAsync()
        {
            Debug.Log("[AuthManager] 토큰 검증 요청...");
            EnsureApiClient();

            var response = await _api.PostAsync<VerifyResponse>(KioskApiEndpoints.AuthVerify);

            if (!response.Success || response.Data == null)
            {
                Debug.LogError($"[AuthManager] 인증 실패: {response.Error?.Code} — {response.Error?.Message}");
                IsVerified = false;
                return false;
            }

            DeviceContext = response.Data;
            IsVerified = true;

            Debug.Log($"[AuthManager] 인증 성공 — device: {DeviceContext.DeviceId}, " +
                      $"site: {DeviceContext.SiteId}, zone: {DeviceContext.ZoneId}");

            EventBus.Publish(new AuthVerifiedEvent
            {
                DeviceId = DeviceContext.DeviceId,
                SiteId = DeviceContext.SiteId,
                ZoneId = DeviceContext.ZoneId
            });

            return true;
        }

        /// <summary>
        /// 키오스크 초기 설정 로드.
        /// GET /kiosk/bootstrap → 마스코트, 사이트, 구역 정보 수신.
        /// </summary>
        public async UniTask<bool> BootstrapAsync()
        {
            Debug.Log("[AuthManager] 부트스트랩 요청...");
            EnsureApiClient();

            var response = await _api.GetAsync<BootstrapResponse>(KioskApiEndpoints.Bootstrap);

            if (!response.Success || response.Data == null)
            {
                Debug.LogError($"[AuthManager] 부트스트랩 실패: {response.Error?.Code} — {response.Error?.Message}");
                IsBootstrapped = false;
                return false;
            }

            BootstrapData = response.Data;
            IsBootstrapped = true;

            Debug.Log($"[AuthManager] 부트스트랩 성공 — site: {BootstrapData.SiteName}, " +
                      $"zone: {BootstrapData.ZoneName ?? "OUTER"}, " +
                      $"mascot: {BootstrapData.Mascot?.Name ?? "없음"}");

            EventBus.Publish(new BootstrapLoadedEvent { Data = BootstrapData });

            return true;
        }

        /// <summary>verify → bootstrap 순차 실행.</summary>
        public async UniTask<bool> AuthenticateAsync()
        {
            if (!await VerifyAsync()) return false;
            if (!await BootstrapAsync()) return false;
            return true;
        }
    }
}
