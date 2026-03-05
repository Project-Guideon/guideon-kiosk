using Guideon.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Guideon.UI
{
    /// <summary>
    /// 대기 화면에서 터치/클릭을 감지해 UserTouchedEvent를 발행.
    /// IdlePanel이 활성화 상태일 때만 동작.
    /// </summary>
    public class TouchDetector : MonoBehaviour
    {
        private void Update()
        {
            if (!UIManager.HasInstance) return;
            if (!UIManager.Instance.IsVisible(UIManager.Panel.Idle)) return;

            if (WasTouched())
                EventBus.Publish(new UserTouchedEvent());
        }

        // 터치스크린(키오스크) 과 마우스(에디터 개발) 둘 다 처리
        private static bool WasTouched()
        {
            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                return true;

            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
                return true;

            return false;
        }
    }
}
