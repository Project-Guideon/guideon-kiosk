using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guideon.UI
{
    /// <summary>
    /// 채팅 버블. 사용자 / AI 따라 색상과 정렬이 달라진다.
    /// ChatPanel의 ScrollRect Content 아래 동적으로 생성됨.
    /// </summary>
    public class MessageBubble : MonoBehaviour
    {
        public enum Sender { User, Ai }

        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Image _background;
        [SerializeField] private HorizontalLayoutGroup _container;

        public void SetMessage(string message, Sender sender)
        {
            if (_text != null)
            {
                _text.text = message;
                _text.color = sender == Sender.User
                    ? GuideonColors.TextOnPrimary
                    : GuideonColors.TextPrimary;
            }

            if (_background != null)
            {
                _background.color = sender == Sender.User
                    ? GuideonColors.Primary
                    : GuideonColors.BgCard;
            }

            if (_container != null)
            {
                _container.childAlignment = sender == Sender.User
                    ? TextAnchor.MiddleRight
                    : TextAnchor.MiddleLeft;
            }
        }
    }
}