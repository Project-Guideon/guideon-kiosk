using Guideon.Core;
using UnityEngine;

namespace Guideon.Mascot
{
    /// <summary>
    /// 본 기반 프로시저럴 애니메이션 시스템.
    /// GLB 모델에 애니메이션 클립 없이도 Idle/Greeting/Listening/Thinking/Speaking 동작 수행.
    /// </summary>
    public class ProceduralMascotAnimator : MonoBehaviour, IMascotAnimator
    {
        [Header("Animation Settings")]
        [SerializeField] private float transitionSpeed = 5f;

        [Header("Idle")]
        [SerializeField] private float idleBreathSpeed = 1.5f;
        [SerializeField] private float idleBreathAmount = 3.5f;
        [SerializeField] private float idleSwaySpeed = 0.8f;
        [SerializeField] private float idleSwayAmount = 6f;
        [SerializeField] private float idleWeightShiftAmount = 10f;  // Hips z-roll — 2등신은 크게 흔들려야 보임
        [SerializeField] private float idleLegSwayAmount = 5f;       // Thigh z
        [SerializeField] private float idleArmSwayAmount = 9f;       // UpperArm 기본 진자
        [SerializeField] private float idleWavePeriod = 0.8f;        // 주기적 손 흔들기 주파수 (≈8초마다 한 번)
        [SerializeField] private float idleArmRaise = 45f;           // 손 흔들기 시 팔 들기 각도
        [SerializeField] private float idleWaveSpeed = 8f;           // Forearm 흔들기 속도
        [SerializeField] private float idleWaveAngle = 28f;          // Forearm 흔들기 진폭

        [Header("Greeting")]
        [SerializeField] private float greetingDuration = 3f;
        [SerializeField] private float greetingArmRaise = 55f;          // RightUpperArm 들기 각도
        [SerializeField] private float greetingWaveSpeed = 9f;          // Forearm/Hand 흔들기 속도
        [SerializeField] private float greetingWaveAngle = 22f;         // Forearm/Hand 흔들기 진폭
        [SerializeField] private float greetingBodySwayAmount = 6f;     // 몸 좌우 흔들기 진폭
        [SerializeField] private float greetingBodySwaySpeed = 5f;      // 몸 좌우 흔들기 속도
        [SerializeField] private float greetingNodAngle = 6f;           // 가벼운 고개 끄덕

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

        [Header("T포즈 보정 (Inspector에서 직접 튜닝)")]
        [Tooltip("왼팔 UpperArm을 T포즈에서 내리는 로컬 회전값. 모델마다 다름 — Play 중 조절 가능.")]
        [SerializeField] private Vector3 _leftArmRestEuler  = new Vector3(0f,  0f, -75f);
        [Tooltip("오른팔 UpperArm 보정값.")]
        [SerializeField] private Vector3 _rightArmRestEuler = new Vector3(0f,  0f,  75f);

        private BoneRig _rig;
        private MascotState _currentState = MascotState.Idle;
        private MascotState _targetState = MascotState.Idle;
        private float _stateTimer;
        private float _transitionProgress = 1f; // 1 = fully transitioned

        // Greeting은 one-shot: 끝나면 Idle로 복귀
        private bool _greetingDone;

        public BoneRig Rig => _rig;
        public MascotState CurrentState => _currentState;

        public void Initialize(BoneRig rig)
        {
            _rig = rig;
        }

        public void SetState(MascotState state)
        {
            if (_rig == null || !_rig.IsValid) return;

            _targetState = state;
            _transitionProgress = 0f;
            _stateTimer = 0f;
            _greetingDone = false;
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

            // T포즈 → 자연스러운 팔 내림 보정 (ResetAll 직후, 애니메이션 적용 전)
            ApplyRestPose();

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

        #region RestPose - T포즈 보정

        private void ApplyRestPose()
        {
            if (_rig.LeftUpperArm != null)
                RotateAdditively(_rig.LeftUpperArm,
                    _leftArmRestEuler.x, _leftArmRestEuler.y, _leftArmRestEuler.z);
            if (_rig.RightUpperArm != null)
                RotateAdditively(_rig.RightUpperArm,
                    _rightArmRestEuler.x, _rightArmRestEuler.y, _rightArmRestEuler.z);
        }

        #endregion

        #region Idle - 호흡 + 체중 이동 + 팔/다리 흔들기

        private void ApplyIdle()
        {
            float breath = Mathf.Sin(Time.time * idleBreathSpeed);
            float sway   = Mathf.Sin(Time.time * idleSwaySpeed); // -1..1 공통 위상

            // 호흡: Spine 위아래
            if (_rig.Spine != null)
                RotateAdditively(_rig.Spine, breath * idleBreathAmount, 0, 0);

            // 무게중심 좌우 이동: Hips를 좌우로 기울여 몸 전체가 흔들림
            if (_rig.Hips != null)
                RotateAdditively(_rig.Hips, 0, 0, sway * idleWeightShiftAmount);

            // 상체는 반대로 살짝 보정 (뻣뻣한 판자 느낌 제거)
            var upper = _rig.Spine1 != null ? _rig.Spine1 : _rig.Spine;
            if (upper != null)
                RotateAdditively(upper, 0, 0, -sway * idleSwayAmount);

            // 다리: Hips 반대 부호로 체중 이동 느낌 (과도한 쏠림 방지)
            if (_rig.LeftThigh  != null) RotateAdditively(_rig.LeftThigh,  0, 0, -sway * idleLegSwayAmount);
            if (_rig.RightThigh != null) RotateAdditively(_rig.RightThigh, 0, 0, -sway * idleLegSwayAmount);

            // 팔: 몸 흔들림 따라 가볍게 흔들리는 진자
            if (_rig.LeftUpperArm  != null) RotateAdditively(_rig.LeftUpperArm,  0, 0,  sway * idleArmSwayAmount);
            if (_rig.RightUpperArm != null) RotateAdditively(_rig.RightUpperArm, 0, 0,  sway * idleArmSwayAmount);

            // 머리: 약간 더 큰 bob + sway 반대 방향 turn으로 생동감
            if (_rig.Head != null)
                RotateAdditively(_rig.Head, Mathf.Sin(Time.time * 0.7f) * 1.5f, -sway * 2f, 0);
        }

        #endregion

        #region Greeting - 인사 (손 흔들기 + 몸 흔들흔들)

        private void ApplyGreeting(float blend)
        {
            float t = Mathf.Clamp01(_stateTimer / greetingDuration);

            // 팔 들기 엔벨로프: 처음 20%에 올리고, 마지막 20%에 내림 (가운데 유지)
            float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.2f))
                        * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) / 0.2f));
            float k = raise * blend;

            // 몸 전체 흔들흔들 (좌우)
            float bodySway = Mathf.Sin(_stateTimer * greetingBodySwaySpeed);
            if (_rig.Hips  != null) RotateAdditively(_rig.Hips,  0, 0,  bodySway * greetingBodySwayAmount * k);
            if (_rig.Spine != null) RotateAdditively(_rig.Spine, 0, 0, -bodySway * greetingBodySwayAmount * 0.4f * k);

            // 가벼운 끄덕 + 몸 흔들림 따라 고개도 살짝
            if (_rig.Head != null)
                RotateAdditively(_rig.Head, greetingNodAngle * raise * blend, bodySway * 3f * k, 0);

            // 오른팔 번쩍 들기 (z 음수 = 팔 올리기, 기존 부호 유지)
            if (_rig.RightUpperArm != null)
                RotateAdditively(_rig.RightUpperArm, 0, 0, -greetingArmRaise * k);

            // 손 흔들기: Forearm + Hand 좌우 반복
            float wave = Mathf.Sin(_stateTimer * greetingWaveSpeed);
            if (_rig.RightForearm != null)
                RotateAdditively(_rig.RightForearm, 0, 0, wave * greetingWaveAngle * k);
            if (_rig.RightHand != null)
                RotateAdditively(_rig.RightHand, 0, 0, wave * greetingWaveAngle * 0.5f * k);

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
