using Cysharp.Threading.Tasks;
using Guideon.Core;
using Guideon.Network;
using Guideon.Network.Stt;
using Guideon.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.UI
{
    /// <summary>
    /// 관리자 설정 페이지.
    /// Idle 화면 좌상단 모서리 3초 롱프레스로 진입, 닫기 버튼으로 복귀.
    /// 마이크 민감도, 유휴 복귀 시간, 디바이스 정보 표시, 재페어링, 앱 종료 제공.
    /// </summary>
    public class AdminScreenController : PanelControllerBase
    {
        // ── 임계값 ↔ 민감도% 변환 상수 ────────────────────────────
        // pct 0 = 가장 둔감(야외, threshold=0.10), pct 100 = 가장 민감(실내, threshold=0.005)
        private const float ThresholdMin = 0.005f;
        private const float ThresholdMax = 0.10f;

        // ── UI 요소 ──────────────────────────────────────────────
        private Label  _infoDeviceId;
        private Label  _infoServerUrl;
        private VisualElement _infoPairedDot;
        private Label  _infoPairedText;

        private Slider _sliderMic;
        private Label  _valMic;
        private Label  _hintVad;

        private Slider _sliderIdle;
        private Label  _valIdle;

        private Button _btnClose;
        private Button _btnRepair;
        private Button _btnQuit;

        private VisualElement _confirmOverlay;
        private Label  _confirmTitle;
        private Label  _confirmMsg;
        private Button _btnConfirmOk;
        private Button _btnConfirmCancel;

        // ── 확인 다이얼로그 상태 ──────────────────────────────────
        private enum ConfirmAction { None, Repair, Quit }
        private ConfirmAction _pendingAction = ConfirmAction.None;

        // ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            PanelId = UIManager.Panel.Admin;
        }

        protected override void OnBindUI()
        {
            // 정보 라벨
            _infoDeviceId  = Q<Label>("info-device-id");
            _infoServerUrl = Q<Label>("info-server-url");
            _infoPairedDot = Q("info-paired-dot");
            _infoPairedText= Q<Label>("info-paired-text");

            // 마이크 민감도
            _sliderMic = Q<Slider>("slider-mic");
            _valMic    = Q<Label>("val-mic");
            _hintVad   = Q<Label>("hint-vad");

            // 유휴 복귀 시간
            _sliderIdle = Q<Slider>("slider-idle");
            _valIdle    = Q<Label>("val-idle");

            // 버튼
            _btnClose  = Q<Button>("btn-close");
            _btnRepair = Q<Button>("btn-repair");
            _btnQuit   = Q<Button>("btn-quit");

            // 확인 다이얼로그
            _confirmOverlay  = Q("confirm-overlay");
            _confirmTitle    = Q<Label>("confirm-title");
            _confirmMsg      = Q<Label>("confirm-msg");
            _btnConfirmOk    = Q<Button>("btn-confirm-ok");
            _btnConfirmCancel= Q<Button>("btn-confirm-cancel");

            // 콜백 등록
            _btnClose?.RegisterCallback<ClickEvent>(_ => OnCloseClicked());
            _sliderMic?.RegisterCallback<ChangeEvent<float>>(OnMicSliderChanged);
            _sliderIdle?.RegisterCallback<ChangeEvent<float>>(OnIdleSliderChanged);
            _btnRepair?.RegisterCallback<ClickEvent>(_ => ShowConfirm(
                ConfirmAction.Repair,
                "디바이스 초기화",
                "기기 연결이 해제되고 앱이 재시작됩니다.\n페어링 코드를 다시 발급받아야 합니다.\n\n계속하시겠습니까?"));
            _btnQuit?.RegisterCallback<ClickEvent>(_ => ShowConfirm(
                ConfirmAction.Quit,
                "앱 종료",
                "GUIDEON 키오스크를 종료합니다.\n\n계속하시겠습니까?"));
            _btnConfirmOk?.RegisterCallback<ClickEvent>(_ => OnConfirmOk());
            _btnConfirmCancel?.RegisterCallback<ClickEvent>(_ => HideConfirm());

            // 현재 설정 값으로 컨트롤 초기화
            PopulateFromConfig();
        }

        // ── 설정 로드 ─────────────────────────────────────────────

        private void PopulateFromConfig()
        {
            var cfg = ConfigManager.Instance?.Config;
            if (cfg == null) return;

            // 디바이스 정보
            if (_infoDeviceId  != null) _infoDeviceId.text  = string.IsNullOrEmpty(cfg.device.id) ? "미등록" : cfg.device.id;
            if (_infoServerUrl != null) _infoServerUrl.text = cfg.server.baseUrl;

            bool hasCreds = ConfigManager.Instance.HasDeviceCredentials;
            if (_infoPairedDot  != null) _infoPairedDot.style.backgroundColor  = hasCreds ? new Color(0.36f, 0.72f, 0.36f) : Color.gray;
            if (_infoPairedText != null) _infoPairedText.text = hasCreds ? "완료" : "미등록";

            // 마이크 민감도 슬라이더
            float threshold = cfg.kiosk.sttVadThreshold;
            float micPct    = ThresholdToPct(threshold);
            if (_sliderMic != null) _sliderMic.SetValueWithoutNotify(micPct);
            UpdateMicLabels(micPct, threshold);

            // 유휴 복귀 시간 슬라이더
            float idleSec = cfg.kiosk.idleTimeoutSeconds;
            if (_sliderIdle != null) _sliderIdle.SetValueWithoutNotify(idleSec);
            UpdateIdleLabel(idleSec);
        }

        // ── 슬라이더 이벤트 ──────────────────────────────────────

        private void OnMicSliderChanged(ChangeEvent<float> evt)
        {
            float pct       = evt.newValue;
            float threshold = PctToThreshold(pct);

            // 인메모리 즉시 반영 (SttManager는 다음 녹음 세션부터 자동 적용)
            if (ConfigManager.Instance?.Config != null)
                ConfigManager.Instance.Config.kiosk.sttVadThreshold = threshold;

            UpdateMicLabels(pct, threshold);
        }

        private void OnIdleSliderChanged(ChangeEvent<float> evt)
        {
            int sec = Mathf.RoundToInt(evt.newValue);

            if (ConfigManager.Instance?.Config != null)
                ConfigManager.Instance.Config.kiosk.idleTimeoutSeconds = sec;

            UpdateIdleLabel(sec);
        }

        private void UpdateMicLabels(float pct, float threshold)
        {
            if (_valMic  != null) _valMic.text  = $"{Mathf.RoundToInt(pct)}%";
            if (_hintVad != null) _hintVad.text = $"현재 임계값: {threshold:F3}";
        }

        private void UpdateIdleLabel(float sec)
        {
            if (_valIdle == null) return;
            int s = Mathf.RoundToInt(sec);
            _valIdle.text = s >= 60 ? $"{s / 60}분 {s % 60}초" : $"{s}초";
        }

        // ── 닫기 ─────────────────────────────────────────────────

        private void OnCloseClicked()
        {
            // 변경된 설정을 디스크에 영속화
            ConfigManager.Instance?.SaveConfigAsync().Forget();
            EventBus.Publish(new AdminClosedEvent());
        }

        // ── 확인 다이얼로그 ───────────────────────────────────────

        private void ShowConfirm(ConfirmAction action, string title, string msg)
        {
            _pendingAction = action;
            if (_confirmTitle != null) _confirmTitle.text = title;
            if (_confirmMsg   != null) _confirmMsg.text   = msg;
            _confirmOverlay?.RemoveFromClassList("hidden");
        }

        private void HideConfirm()
        {
            _pendingAction = ConfirmAction.None;
            _confirmOverlay?.AddToClassList("hidden");
        }

        private void OnConfirmOk()
        {
            var action = _pendingAction;
            HideConfirm();

            switch (action)
            {
                case ConfirmAction.Repair: RepairAsync().Forget(); break;
                case ConfirmAction.Quit:   Application.Quit();     break;
            }
        }

        // ── 재페어링 흐름 ─────────────────────────────────────────

        private async UniTaskVoid RepairAsync()
        {
            Debug.Log("[AdminScreen] 재페어링 시작 — 자격증명 초기화 및 Boot 재로드");

            // 1. 진행 중인 서비스 중단
            if (HeartbeatService.HasInstance) HeartbeatService.Instance.StopHeartbeat();
            if (SttManager.HasInstance)       SttManager.Instance.Stop();

            // 2. 인메모리 캐시 초기화 (stale 인증 상태 제거)
            if (AuthManager.HasInstance)    AuthManager.Instance.Reset();
            if (PairingManager.HasInstance) PairingManager.Instance.Reset();

            // 3. config 파일에서 자격증명 삭제
            await ConfigManager.Instance.ResetDeviceCredentialsAsync();

            // 4. Boot 씬 재로드 → BootSceneController.RunAsync() 가 페어링 흐름 진입
            await GameManager.Instance.LoadSceneAsync(GameManager.Scenes.Boot);
        }

        // ── 매핑 헬퍼 ────────────────────────────────────────────

        /// <summary>
        /// VAD 임계값을 민감도% (0=둔감/야외, 100=민감/실내) 로 변환.
        /// </summary>
        private static float ThresholdToPct(float threshold)
        {
            float t = Mathf.InverseLerp(ThresholdMax, ThresholdMin, threshold);
            return Mathf.Clamp01(t) * 100f;
        }

        /// <summary>
        /// 민감도% 를 VAD 임계값으로 변환.
        /// </summary>
        private static float PctToThreshold(float pct)
        {
            return Mathf.Lerp(ThresholdMax, ThresholdMin, pct / 100f);
        }
    }
}
