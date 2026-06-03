using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using Guideon.Core;
using UnityEngine;

namespace Guideon.Mascot
{
    /// <summary>
    /// UniGLTF가 임포트한 legacy AnimationClip을 상태별로 재생하는 애니메이터.
    /// GLB 루트 GameObject에 부착되며, MascotLoader.SetupModel()이 초기화한다.
    ///
    /// 상태→클립 해석 우선순위:
    ///   1. animClips 맵에서 상태 키(idle/speaking/…)로 클립명 조회 → 같은 이름 클립 탐색
    ///   2. 이름 불일치 또는 맵 없음 → canonical 인덱스 순서로 폴백
    ///      (idle=0, speaking=1, listening=2, thinking=3, greeting=4)
    /// </summary>
    [DisallowMultipleComponent]
    public class GltfClipAnimator : MonoBehaviour, IMascotAnimator
    {
        // canonical 순서 (서버 animClips 키와 동일)
        private static readonly string[] StateKeys =
            { "idle", "speaking", "listening", "thinking", "greeting" };

        // MascotState enum 값 → StateKeys 인덱스 매핑 (순서 일치)
        private static readonly MascotState[] StateOrder =
        {
            MascotState.Idle,
            MascotState.Speaking,
            MascotState.Listening,
            MascotState.Thinking,
            MascotState.Greeting,
        };

        private Animation _anim;
        // 상태 → 실제 재생할 클립명 (Initialize에서 확정)
        private readonly Dictionary<MascotState, string> _stateClipName = new();

        private MascotState _currentState = MascotState.Idle;
        private bool _initialized;

        // ── 루트 본 모션 억제 ────────────────────────────────────
        // Tripo retarget 클립에 Hips의 이동·회전이 베이크돼 있을 경우
        // LateUpdate에서 초기 바인드포즈로 매 프레임 강제 복원.
        private Transform _rootBone;
        private Vector3    _rootBoneInitLocalPos;
        private Quaternion _rootBoneInitLocalRot;

        // ── 초기화 ──────────────────────────────────────────────

        /// <summary>
        /// MascotLoader.SetupModel()에서 GLB 로드 직후 호출.
        /// </summary>
        /// <param name="anim">GLB 루트의 Animation 컴포넌트 (UniGLTF가 자동 부착).</param>
        /// <param name="clips">RuntimeGltfInstance.AnimationClips (legacy clip 목록).</param>
        /// <param name="animClips">서버 animClips 맵 (상태→클립명). null/빈 맵 허용.</param>
        public void Initialize(
            Animation anim,
            IReadOnlyList<AnimationClip> clips,
            IDictionary<string, string> animClips)
        {
            _anim = anim;

            // ── 클립 등록 보장 (이미 import 시 AddClip 됐지만 재확인) ──
            foreach (var clip in clips)
            {
                clip.legacy = true; // UniGLTF가 이미 true로 설정하지만 방어적으로
                if (_anim.GetClip(clip.name) == null)
                    _anim.AddClip(clip, clip.name);
            }

            // ── 실제 클립명 목록 로그 (검증용) ──
            var sb = new StringBuilder($"[GltfClipAnimator] 로드된 클립 {clips.Count}개:\n");
            for (int i = 0; i < clips.Count; i++)
                sb.AppendLine($"  [{i}] \"{clips[i].name}\"  wrapMode={clips[i].wrapMode}");
            Debug.Log(sb.ToString());

            // ── 상태→클립명 해석 ──
            var resolveLog = new StringBuilder("[GltfClipAnimator] 상태→클립 매핑:\n");

            for (int si = 0; si < StateOrder.Length; si++)
            {
                MascotState state = StateOrder[si];
                string stateKey   = StateKeys[si];
                string resolved   = null;

                // 1차: animClips 맵으로 이름 탐색
                if (animClips != null && animClips.TryGetValue(stateKey, out string mappedName))
                {
                    foreach (var clip in clips)
                    {
                        if (clip.name == mappedName)
                        {
                            resolved = mappedName;
                            break;
                        }
                    }
                    if (resolved == null)
                        resolveLog.AppendLine($"  {stateKey}: 이름 불일치 \"{mappedName}\" → 인덱스 폴백");
                }

                // 2차: 인덱스 폴백
                if (resolved == null && si < clips.Count)
                    resolved = clips[si].name;

                if (resolved != null)
                {
                    _stateClipName[state] = resolved;
                    resolveLog.AppendLine($"  {stateKey} → \"{resolved}\"");
                }
                else
                {
                    resolveLog.AppendLine($"  {stateKey} → (없음, 클립 부족)");
                }
            }

            Debug.Log(resolveLog.ToString());

            // ── 루트 본(Hips) 탐색 — 이동·회전 억제용 ──────────
            foreach (var t in gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().Contains("hip") || t.name.ToLower() == "pelvis")
                {
                    _rootBone             = t;
                    _rootBoneInitLocalPos = t.localPosition;
                    _rootBoneInitLocalRot = t.localRotation;
                    Debug.Log($"[GltfClipAnimator] 루트 본 고정: \"{t.name}\"");
                    break;
                }
            }
            if (_rootBone == null)
                Debug.LogWarning("[GltfClipAnimator] 루트 본(Hips) 탐색 실패 — 루트 모션 억제 비활성");

            _initialized = true;
        }

        // ── 루트 본 모션 억제 (LateUpdate) ──────────────────────

        /// <summary>
        /// Legacy Animation이 Update 이전에 본에 커브를 적용한 뒤,
        /// LateUpdate에서 Hips 본을 초기 바인드포즈로 덮어써
        /// 180° 뒤집힘과 위치 드리프트를 제거한다.
        /// </summary>
        private void LateUpdate()
        {
            if (_rootBone == null) return;
            _rootBone.localPosition = _rootBoneInitLocalPos;
            _rootBone.localRotation = _rootBoneInitLocalRot;
        }

        // ── IMascotAnimator ──────────────────────────────────────

        public void SetState(MascotState state)
        {
            if (!_initialized || _anim == null) return;
            if (!_stateClipName.TryGetValue(state, out string clipName)) return;

            _currentState = state;

            if (state == MascotState.Greeting)
            {
                // 1회 재생 후 Idle 자동 복귀
                var clip = _anim.GetClip(clipName);
                if (clip != null)
                {
                    clip.wrapMode = WrapMode.Once;
                    _anim.wrapMode = WrapMode.Once;
                }
                _anim.CrossFade(clipName, 0.3f);
                ReturnToIdleAfterGreeting(clip).Forget();
            }
            else
            {
                var clip = _anim.GetClip(clipName);
                if (clip != null)
                {
                    clip.wrapMode = WrapMode.Loop;
                    _anim.wrapMode = WrapMode.Loop;
                }
                _anim.CrossFade(clipName, 0.3f);
            }
        }

        // ── Internal ────────────────────────────────────────────

        private async UniTaskVoid ReturnToIdleAfterGreeting(AnimationClip clip)
        {
            float duration = clip != null ? clip.length : 2f;
            // CrossFade 0.3s 포함해 클립 길이 뒤 Idle 복귀
            await UniTask.Delay(System.TimeSpan.FromSeconds(duration));

            // 아직 Greeting 상태일 때만 복귀 (다른 상태로 이미 전환됐으면 무시)
            if (_currentState == MascotState.Greeting)
                SetState(MascotState.Idle);
        }
    }
}
