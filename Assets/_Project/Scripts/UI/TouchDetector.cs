using Guideon.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Guideon.UI
{
    /// <summary>
    /// Idle 화면의 모든 터치/제스처를 단일 소유.
    /// ─ 일반 영역 탭   → UserTouchedEvent  (채팅 진입)
    /// ─ 좌상단 모서리 3초 홀드 → AdminRequestedEvent  (관리자 진입)
    /// ─ 좌상단 모서리 홀드 중  → AdminLongPressEvent { Active=true, Progress } (진행 표시)
    /// ─ 홀드 취소/완료         → AdminLongPressEvent { Active=false }
    ///
    /// Input System 화면 좌표: 원점 좌하단 → 좌상단 = x < CornerSize, y > Screen.height - CornerSize
    /// </summary>
    public class TouchDetector : MonoBehaviour
    {
        private const float CornerSize    = 150f; // px — 좌상단 히트 영역
        private const float LongPressSec  = 3f;   // 초 — 관리자 진입 임계

        private enum GestureState { Idle, CornerHold, Done }

        private GestureState _state    = GestureState.Idle;
        private float        _holdTime = 0f;

        private void Update()
        {
            if (!UIManager.HasInstance) return;

            // Admin 화면이 열려 있으면 아무것도 하지 않고 상태 리셋
            if (UIManager.Instance.IsVisible(UIManager.Panel.Admin))
            {
                ResetState(publishCancel: false);
                return;
            }

            // Idle 화면이 아닐 때도 상태 리셋
            if (!UIManager.Instance.IsVisible(UIManager.Panel.Idle))
            {
                ResetState(publishCancel: false);
                return;
            }

            switch (_state)
            {
                case GestureState.Idle:
                    HandleIdleState();
                    break;

                case GestureState.CornerHold:
                    HandleCornerHoldState();
                    break;

                case GestureState.Done:
                    // 완료 후 손을 뗄 때까지 대기해 중복 발행 방지
                    if (!IsHeld())
                        _state = GestureState.Idle;
                    break;
            }
        }

        // ── 상태 핸들러 ──────────────────────────────────────────────

        private void HandleIdleState()
        {
            if (!WasPressedThisFrame(out Vector2 pos)) return;

            if (IsAdminCorner(pos))
            {
                // 좌상단 모서리 — 롱프레스 추적 시작
                _state    = GestureState.CornerHold;
                _holdTime = 0f;
            }
            else
            {
                // 일반 영역 — 즉시 채팅 진입
                EventBus.Publish(new UserTouchedEvent());
            }
        }

        private void HandleCornerHoldState()
        {
            if (!IsHeld())
            {
                // 손을 뗌 — 3초 미만이면 짧은 탭으로 간주해 채팅 진입
                PublishLongPressCancel();
                EventBus.Publish(new UserTouchedEvent());
                _state    = GestureState.Idle;
                _holdTime = 0f;
                return;
            }

            _holdTime += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_holdTime / LongPressSec);
            EventBus.Publish(new AdminLongPressEvent { Active = true, Progress = progress });

            if (_holdTime >= LongPressSec)
            {
                // 3초 달성 — 관리자 진입
                _state = GestureState.Done;
                EventBus.Publish(new AdminLongPressEvent { Active = false, Progress = 0f });
                EventBus.Publish(new AdminRequestedEvent());
                Debug.Log("[TouchDetector] 관리자 롱프레스 완료 — AdminRequestedEvent 발행");
            }
        }

        // ── 헬퍼 ────────────────────────────────────────────────────

        private void ResetState(bool publishCancel)
        {
            if (publishCancel && _state == GestureState.CornerHold)
                PublishLongPressCancel();
            _state    = GestureState.Idle;
            _holdTime = 0f;
        }

        private static void PublishLongPressCancel()
        {
            EventBus.Publish(new AdminLongPressEvent { Active = false, Progress = 0f });
        }

        /// <summary>Input System 화면 좌표 기준 좌상단 모서리 판정.</summary>
        private static bool IsAdminCorner(Vector2 pos)
        {
            return pos.x < CornerSize && pos.y > Screen.height - CornerSize;
        }

        /// <summary>이번 프레임에 새로 눌렸는지 + 눌린 위치 반환.</summary>
        private static bool WasPressedThisFrame(out Vector2 position)
        {
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                position = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                position = Mouse.current.position.ReadValue();
                return true;
            }
            position = default;
            return false;
        }

        /// <summary>현재 프레임에 터치/마우스가 눌려 있는지.</summary>
        private static bool IsHeld()
        {
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.isPressed)
                return true;
            if (Mouse.current != null &&
                Mouse.current.leftButton.isPressed)
                return true;
            return false;
        }
    }
}
