using UnityEngine;

namespace Guideon.Mascot
{
    /// <summary>
    /// 본 기반 프로시저럴 애니메이션 시스템.
    /// GLB 모델에 애니메이션 클립 없이도 Idle/Greeting/Listening/Thinking/Speaking 동작 수행.
    /// </summary>
    public class ProceduralMascotAnimator : MonoBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float transitionSpeed = 5f;

        [Header("Idle")]
        [SerializeField] private float idleBreathSpeed = 1.5f;
        [SerializeField] private float idleBreathAmount = 1.5f;
        [SerializeField] private float idleSwaySpeed = 0.8f;
        [SerializeField] private float idleSwayAmount = 1f;

        [Header("Greeting")]
        [SerializeField] private float greetingBowAngle = 20f;
        [SerializeField] private float greetingArmAngle = 40f;
        [SerializeField] private float greetingSpeed = 3f;

        [Header("Listening")]
        [SerializeField] private float listeningTiltAngle = 10f;
        [SerializeField] private float listeningLeanAngle = 3f;

        [Header("Thinking")]
        [SerializeField] private float thinkingTiltAngle = 15f;
        [SerializeField] private float thinkingLookUpAngle = 10f;

        [Header("Speaking")]
        [SerializeField] private float speakingJawMaxAngle = 15f;
        [SerializeField] private float speakingJawSpeed = 8f;
        [SerializeField] private float speakingHeadBobAmount = 3f;
        [SerializeField] private float speakingGestureAmount = 8f;

        private BoneRig _rig;
        private ExpressionOverlay _expressionOverlay;
        private MascotState _currentState = MascotState.Idle;
        private MascotState _targetState = MascotState.Idle;
        private float _stateTimer;
        private float _transitionProgress = 1f; // 1 = fully transitioned

        // Greeting은 one-shot: 끝나면 Idle로 복귀
        private bool _greetingDone;

        public BoneRig Rig => _rig;
        public MascotState CurrentState => _currentState;
        public ExpressionOverlay ExpressionOverlay => _expressionOverlay;

        public void Initialize(BoneRig rig)
        {
            _rig = rig;

            // Head bone이 있으면 표정 오버레이 자동 설정
            if (_rig.Head != null)
            {
                _expressionOverlay = gameObject.AddComponent<ExpressionOverlay>();
                _expressionOverlay.Initialize(_rig.Head);
            }
        }

        public void SetState(MascotState state)
        {
            if (_rig == null || !_rig.IsValid) return;

            _targetState = state;
            _transitionProgress = 0f;
            _stateTimer = 0f;
            _greetingDone = false;

            // 표정도 같이 변경
            if (_expressionOverlay != null)
            {
                _expressionOverlay.SetState(state);
            }
        }

        void Update()
        {
            if (_rig == null || !_rig.IsValid) return;

            _stateTimer += Time.deltaTime;

            // 상태 전이
            if (_transitionProgress < 1f)
            {
                _transitionProgress += Time.deltaTime * transitionSpeed;
                if (_transitionProgress >= 1f)
                {
                    _transitionProgress = 1f;
                    _currentState = _targetState;
                }
            }

            // 매 프레임 초기 포즈로 리셋 후 애니메이션 적용
            _rig.ResetAll();

            // Idle은 항상 베이스로 깔림
            ApplyIdle();

            // 현재 상태 애니메이션 블렌딩
            float blend = _transitionProgress;
            switch (_targetState)
            {
                case MascotState.Idle:
                    // Idle만 적용 (이미 위에서 함)
                    break;
                case MascotState.Greeting:
                    ApplyGreeting(blend);
                    break;
                case MascotState.Listening:
                    ApplyListening(blend);
                    break;
                case MascotState.Thinking:
                    ApplyThinking(blend);
                    break;
                case MascotState.Speaking:
                    ApplySpeaking(blend);
                    break;
            }
        }

        #region Idle - 호흡 + 미세 흔들림

        private void ApplyIdle()
        {
            float breathPhase = Time.time * idleBreathSpeed;
            float swayPhase = Time.time * idleSwaySpeed;

            // 호흡: Spine 약간 위아래
            if (_rig.Spine != null)
            {
                float breathAngle = Mathf.Sin(breathPhase) * idleBreathAmount;
                RotateAdditively(_rig.Spine, breathAngle, 0, 0);
            }

            // 몸 흔들림: 좌우 미세 sway
            if (_rig.Spine1 != null || _rig.Spine != null)
            {
                var target = _rig.Spine1 != null ? _rig.Spine1 : _rig.Spine;
                float swayAngle = Mathf.Sin(swayPhase) * idleSwayAmount;
                RotateAdditively(target, 0, 0, swayAngle);
            }

            // 머리 약간 움직임
            if (_rig.Head != null)
            {
                float headBob = Mathf.Sin(Time.time * 0.7f) * 1f;
                float headTurn = Mathf.Sin(Time.time * 0.3f) * 1.5f;
                RotateAdditively(_rig.Head, headBob, headTurn, 0);
            }
        }

        #endregion

        #region Greeting - 인사 (고개 숙이기 + 팔 흔들기)

        private void ApplyGreeting(float blend)
        {
            // 2초짜리 one-shot 애니메이션
            float duration = 2f;
            float t = Mathf.Clamp01(_stateTimer / duration);

            // 전반부: 숙이기, 후반부: 올라오기
            float bowCurve = Mathf.Sin(t * Mathf.PI); // 0→1→0
            float armCurve = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f; // 흔드는 느낌

            if (_rig.Spine != null)
            {
                RotateAdditively(_rig.Spine, greetingBowAngle * bowCurve * blend, 0, 0);
            }

            if (_rig.Head != null)
            {
                RotateAdditively(_rig.Head, greetingBowAngle * 0.5f * bowCurve * blend, 0, 0);
            }

            // 오른팔 흔들기
            if (_rig.RightUpperArm != null)
            {
                RotateAdditively(_rig.RightUpperArm, 0, 0, -greetingArmAngle * bowCurve * blend);
            }
            if (_rig.RightForearm != null)
            {
                float waveAngle = Mathf.Sin(_stateTimer * 8f) * 20f * bowCurve;
                RotateAdditively(_rig.RightForearm, 0, 0, waveAngle * blend);
            }

            // one-shot 끝나면 Idle로 복귀
            if (t >= 1f && !_greetingDone)
            {
                _greetingDone = true;
                SetState(MascotState.Idle);
            }
        }

        #endregion

        #region Listening - 듣기 (고개 갸웃 + 앞으로 기울임)

        private void ApplyListening(float blend)
        {
            float phase = Time.time * 0.5f;

            // 고개 옆으로 갸웃
            if (_rig.Head != null)
            {
                float tilt = listeningTiltAngle + Mathf.Sin(phase) * 2f;
                RotateAdditively(_rig.Head, 0, 0, tilt * blend);
            }

            // 살짝 앞으로 기울이기
            if (_rig.Spine != null)
            {
                RotateAdditively(_rig.Spine, listeningLeanAngle * blend, 0, 0);
            }

            // 양손 앞으로 모으는 느낌
            if (_rig.LeftUpperArm != null)
                RotateAdditively(_rig.LeftUpperArm, 5f * blend, 0, 5f * blend);
            if (_rig.RightUpperArm != null)
                RotateAdditively(_rig.RightUpperArm, 5f * blend, 0, -5f * blend);
        }

        #endregion

        #region Thinking - 생각 (고개 갸웃 + 위 보기)

        private void ApplyThinking(float blend)
        {
            float phase = Time.time * 0.6f;

            if (_rig.Head != null)
            {
                // 위를 보면서 옆으로 갸웃
                float lookUp = -thinkingLookUpAngle + Mathf.Sin(phase) * 3f;
                float tilt = -thinkingTiltAngle;
                RotateAdditively(_rig.Head, lookUp * blend, 0, tilt * blend);
            }

            // 오른손 턱에 대는 포즈
            if (_rig.RightUpperArm != null)
            {
                RotateAdditively(_rig.RightUpperArm, 30f * blend, 0, -20f * blend);
            }
            if (_rig.RightForearm != null)
            {
                RotateAdditively(_rig.RightForearm, -80f * blend, 0, 0);
            }

            // 왼팔은 자연스럽게
            if (_rig.LeftUpperArm != null)
            {
                RotateAdditively(_rig.LeftUpperArm, 0, 0, 3f * blend);
            }
        }

        #endregion

        #region Speaking - 말하기 (Jaw 립싱크 + 제스처)

        private void ApplySpeaking(float blend)
        {
            // Jaw 립싱크 (사인파 기반, 나중에 오디오 amplitude로 교체)
            if (_rig.Jaw != null)
            {
                float jawOpen = (Mathf.Sin(_stateTimer * speakingJawSpeed) + 1f) * 0.5f;
                // 다양한 주파수 혼합으로 자연스러운 입 움직임
                jawOpen += Mathf.Sin(_stateTimer * speakingJawSpeed * 1.7f) * 0.3f;
                jawOpen = Mathf.Clamp01(jawOpen);
                float angle = jawOpen * speakingJawMaxAngle;
                RotateAdditively(_rig.Jaw, -angle * blend, 0, 0);
            }

            // 말하면서 머리 까딱
            if (_rig.Head != null)
            {
                float nod = Mathf.Sin(_stateTimer * 2f) * speakingHeadBobAmount;
                float turn = Mathf.Sin(_stateTimer * 1.2f) * speakingHeadBobAmount * 0.5f;
                RotateAdditively(_rig.Head, nod * blend, turn * blend, 0);
            }

            // 제스처: 양팔 살짝 움직임
            if (_rig.RightUpperArm != null)
            {
                float gesture = Mathf.Sin(_stateTimer * 1.8f) * speakingGestureAmount;
                RotateAdditively(_rig.RightUpperArm, gesture * blend * 0.5f, 0, -gesture * blend);
            }
            if (_rig.LeftUpperArm != null)
            {
                float gesture = Mathf.Sin(_stateTimer * 1.5f + 1f) * speakingGestureAmount;
                RotateAdditively(_rig.LeftUpperArm, gesture * blend * 0.5f, 0, gesture * blend);
            }

            // 몸도 살짝 움직임
            if (_rig.Spine != null)
            {
                float lean = Mathf.Sin(_stateTimer * 1.3f) * 2f;
                RotateAdditively(_rig.Spine, 0, lean * blend, 0);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// 현재 로컬 회전에 추가 회전을 적용 (기존 포즈 위에 덧씌움)
        /// </summary>
        private void RotateAdditively(Transform bone, float x, float y, float z)
        {
            if (bone == null) return;
            bone.localRotation *= Quaternion.Euler(x, y, z);
        }

        #endregion
    }
}
