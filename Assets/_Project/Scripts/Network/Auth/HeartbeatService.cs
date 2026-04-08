using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Guideon.Core;
using Guideon.Network.Models;
using UnityEngine;

namespace Guideon.Network
{
    /// <summary>
    /// 키오스크 생존 신호를 주기적으로 서버에 전송.
    /// POST /kiosk/heartbeat (30초 간격).
    /// 실패해도 키오스크 운영을 차단하지 않음.
    /// </summary>
    public class HeartbeatService : MonoSingleton<HeartbeatService>
    {
        private const float IntervalSeconds = 30f;
        private const int MaxConsecutiveFailures = 5;

        private ApiClient _api;
        private CancellationTokenSource _cts;
        private int _consecutiveFailures;

        public bool IsRunning { get; private set; }

        protected override void OnInitialize()
        {
            _api = new ApiClient(ConfigManager.Instance);
        }

        /// <summary>하트비트 루프 시작.</summary>
        public void StartHeartbeat()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _consecutiveFailures = 0;
            IsRunning = true;

            HeartbeatLoopAsync(_cts.Token).Forget();
            Debug.Log("[HeartbeatService] 하트비트 시작");
        }

        /// <summary>하트비트 루프 중지.</summary>
        public void StopHeartbeat()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;

            Debug.Log("[HeartbeatService] 하트비트 중지");
        }

        private async UniTaskVoid HeartbeatLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(IntervalSeconds), cancellationToken: ct);

                if (ct.IsCancellationRequested) break;

                await SendHeartbeatAsync();
            }
        }

        private async UniTask SendHeartbeatAsync()
        {
            var body = new HeartbeatRequest
            {
                Version = Application.version,
                ErrorCode = null
            };

            var response = await _api.PostAsync<object>(KioskApiEndpoints.Heartbeat, body);

            if (response.Success)
            {
                _consecutiveFailures = 0;
            }
            else
            {
                _consecutiveFailures++;
                Debug.LogWarning($"[HeartbeatService] 실패 ({_consecutiveFailures}/{MaxConsecutiveFailures}): " +
                                 $"{response.Error?.Message}");

                if (_consecutiveFailures >= MaxConsecutiveFailures)
                {
                    Debug.LogError("[HeartbeatService] 연속 실패 한도 도달");
                }
            }
        }

        private void OnDestroy()
        {
            StopHeartbeat();
        }
    }
}
