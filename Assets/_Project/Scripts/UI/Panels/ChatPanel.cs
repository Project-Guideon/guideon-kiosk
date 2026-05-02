using Cysharp.Threading.Tasks;
using Guideon.Chat;
using Guideon.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Guideon.UI
{
    /// <summary>
    /// 사용자/AI 메시지를 버블로 누적 표시하는 채팅 화면.
    /// 개발 단계에서는 텍스트 입력으로 메시지를 보내고, 추후 STT가 동일 채널로 들어온다.
    /// </summary>
    public class ChatPanel : MonoBehaviour
    {
        [Header("Bubble List")]
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private MessageBubble _userBubblePrefab;
        [SerializeField] private MessageBubble _aiBubblePrefab;

        [Header("Input (개발 테스트용 — STT 연동 시 대체)")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _sendButton;

        [Header("Thinking Indicator")]
        [SerializeField] private GameObject _thinkingGroup;
        [SerializeField] private TextMeshProUGUI _thinkingText;

        private void OnEnable()
        {
            EventBus.Subscribe<ChatResponseEvent>(OnChatResponse);

            if (_sendButton != null) _sendButton.onClick.AddListener(OnSendClicked);
            if (_inputField != null) _inputField.onSubmit.AddListener(OnInputSubmit);

            ClearBubbles();
            SetThinking(false);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ChatResponseEvent>(OnChatResponse);

            if (_sendButton != null) _sendButton.onClick.RemoveListener(OnSendClicked);
            if (_inputField != null) _inputField.onSubmit.RemoveListener(OnInputSubmit);
        }

        // ── 입력 ──────────────────────────────────────────

        private void OnSendClicked() => SubmitInput();
        private void OnInputSubmit(string _) => SubmitInput();

        private void SubmitInput()
        {
            if (_inputField == null) return;
            string text = _inputField.text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _inputField.text = "";
            _inputField.ActivateInputField();

            AppendBubble(text, MessageBubble.Sender.User);
            SendAsync(text).Forget();
        }

        private async UniTaskVoid SendAsync(string text)
        {
            SetThinking(true);
            try
            {
                if (IdleTimeoutManager.HasInstance)
                    IdleTimeoutManager.Instance.NotifyInteraction();
                await ChatManager.Instance.SendMessageAsync(text);
            }
            finally
            {
                SetThinking(false);
            }
        }

        // ── 응답 ──────────────────────────────────────────

        // display는 ChatResponseEvent에 그대로 실려 발행되므로
        // 지도 자동 표시 책임은 Phase 7의 MapPanelController에서 같은 이벤트를 구독해 처리한다.
        private void OnChatResponse(ChatResponseEvent e)
        {
            AppendBubble(e.Answer, MessageBubble.Sender.Ai);
        }

        // ── UI 헬퍼 ───────────────────────────────────────

        private void AppendBubble(string message, MessageBubble.Sender sender)
        {
            if (_contentRoot == null) return;

            var prefab = sender == MessageBubble.Sender.User ? _userBubblePrefab : _aiBubblePrefab;
            if (prefab == null) return;

            var bubble = Instantiate(prefab, _contentRoot);
            bubble.SetMessage(message, sender);

            ScrollToBottomNextFrame().Forget();
        }

        private void ClearBubbles()
        {
            if (_contentRoot == null) return;
            for (int i = _contentRoot.childCount - 1; i >= 0; i--)
                Destroy(_contentRoot.GetChild(i).gameObject);
        }

        private void SetThinking(bool on)
        {
            if (_thinkingGroup != null) _thinkingGroup.SetActive(on);
            if (on && _thinkingText != null) _thinkingText.text = "생각 중...";
        }

        // Layout이 반영된 다음 프레임에 스크롤해야 자식 크기가 적용된다
        private async UniTaskVoid ScrollToBottomNextFrame()
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}