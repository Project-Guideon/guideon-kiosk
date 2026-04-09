using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Guideon.Core;
using Guideon.Network.Models;
using UnityEngine;

namespace Guideon.Network
{
    /// <summary>
    /// 키오스크 첫 부팅 시 페어링 흐름 관리.
    /// 코드 발급 → 화면 표시 → 상태 폴링 → 토큰 수령 → 로컬 저장.
    /// </summary>
    public class PairingManager : MonoSingleton<PairingManager>
    {
        private const float PollIntervalSeconds = 3f;

        private ApiClient _api;

        public string CurrentCode { get; private set; }
        public string ExpiresAt { get; private set; }
        public bool IsPairing { get; private set; }

        private CancellationTokenSource _pollCts;

        protected override void OnInitialize()
        {
            // ApiClient는 실제 사용 시점에 생성 (ConfigManager Awake 순서 보장 불가)
        }

        private void EnsureApiClient()
        {
            _api ??= new ApiClient(ConfigManager.Instance);
        }

        /// <summary>
        /// 페어링 전체 흐름 실행.
        /// 코드 발급 → 폴링 → 토큰 수령 → config 저장까지 완료 후 반환.
        /// </summary>
        public async UniTask<bool> RunPairingFlowAsync()
        {
            IsPairing = true;
            _pollCts = new CancellationTokenSource();

            try
            {
                while (true)
                {
                    // 1. 코드 발급
                    bool issued = await RequestPairingCodeAsync();
                    if (!issued) return false;

                    // 2. 폴링 → PAIRED 대기
                    string status = await PollUntilPairedAsync(_pollCts.Token);

                    if (status == "PAIRED")
                    {
                        // 3. 토큰 수령
                        bool claimed = await ClaimTokenAsync();
                        return claimed;
                    }

                    if (status == "EXPIRED")
                    {
                        Debug.Log("[PairingManager] 코드 만료, 재발급합니다.");
                        continue; // 재발급 루프
                    }

                    // CANCELLED or ERROR
                    return false;
                }
            }
            finally
            {
                IsPairing = false;
                _pollCts?.Dispose();
                _pollCts = null;
            }
        }

        /// <summary>페어링 폴링을 취소합니다.</summary>
        public void CancelPairing()
        {
            _pollCts?.Cancel();
        }

        // ── 내부 단계 ─────────────────────────────────────

        private async UniTask<bool> RequestPairingCodeAsync()
        {
            Debug.Log("[PairingManager] 페어링 코드 발급 요청...");
            EnsureApiClient();

            var response = await _api.PostNoAuthAsync<PairingCodeResponse>(
                KioskApiEndpoints.PairingRequest);

            if (!response.Success || response.Data == null)
            {
                Debug.LogError($"[PairingManager] 코드 발급 실패: {response.Error?.Message}");
                return false;
            }

            CurrentCode = response.Data.PairingCode;
            ExpiresAt = response.Data.ExpiresAt;

            Debug.Log($"[PairingManager] 코드 발급: {CurrentCode} (만료: {ExpiresAt})");

            EventBus.Publish(new PairingCodeIssuedEvent
            {
                PairingCode = CurrentCode,
                ExpiresAt = ExpiresAt
            });

            return true;
        }

        private async UniTask<string> PollUntilPairedAsync(CancellationToken ct)
        {
            Debug.Log($"[PairingManager] 폴링 시작 (코드: {CurrentCode})");

            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), cancellationToken: ct);

                var response = await _api.GetNoAuthAsync<PairingStatusResponse>(
                    KioskApiEndpoints.PairingStatus(CurrentCode));

                if (!response.Success || response.Data == null)
                {
                    Debug.LogWarning("[PairingManager] 폴링 응답 실패, 재시도...");
                    continue;
                }

                string status = response.Data.Status;
                Debug.Log($"[PairingManager] 폴링 상태: {status}");

                if (status == "PAIRED" || status == "EXPIRED")
                    return status;

                // WAITING → 계속 폴링
            }

            return "CANCELLED";
        }

        private async UniTask<bool> ClaimTokenAsync()
        {
            Debug.Log($"[PairingManager] 토큰 수령 요청 (코드: {CurrentCode})");

            var response = await _api.PostNoAuthAsync<PairingClaimResponse>(
                KioskApiEndpoints.PairingClaim(CurrentCode));

            if (!response.Success || response.Data == null)
            {
                Debug.LogError($"[PairingManager] 토큰 수령 실패: {response.Error?.Message}");
                return false;
            }

            var claim = response.Data;
            Debug.Log($"[PairingManager] 토큰 수령 완료 — deviceId: {claim.Device.DeviceId}");

            // config에 저장
            await ConfigManager.Instance.SaveDeviceCredentialsAsync(
                claim.Device.DeviceId, claim.PlainToken);

            EventBus.Publish(new PairingCompletedEvent
            {
                DeviceId = claim.Device.DeviceId
            });

            return true;
        }

        private void OnDestroy()
        {
            _pollCts?.Cancel();
            _pollCts?.Dispose();
        }
    }
}
