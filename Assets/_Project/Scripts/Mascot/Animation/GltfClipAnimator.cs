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
    ///
    /// 루트 모션 억제:
    ///   Tripo retarget 클립에 Hip ~ Armature 체인 전체에 이동·회전이 베이크될 수 있음.
    ///   Initialize()에서 Hip→instance.Root 방향으로 조상 체인을 수집하고,
    ///   LateUpdate()에서 초기 바인드포즈로 매 프레임 강제 복원한다.
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

        /// <summary>클립에 실제 애니메이션 데이터(channel)가 있으면 true. false면 본 이름 불일치 등으로 combine 실패.</summary>
        public bool HasUsableClips { get; private set; }

        // ── 루트 모션 억제 — Hip 조상 체인 ──────────────────────
        // Hip → instance.Root 사이 모든 노드를 잠금.
        // Hip만 잠그면 Armature 등 그 위 노드가 돌아갈 수 있으므로 체인 전체를 잠근다.
        private readonly List<(Transform bone, Vector3 initPos, Quaternion initRot)> _lockedChain = new();

        // ── 전체 본 바인드 포즈 위치·스케일 (회전 전용 리타게팅) ──────
        // Mixamo translation/scale 채널이 Tripo 스켈레톤 비율과 달라 본이 폭발하는 문제 방지.
        // LateUpdate에서 localPosition + localScale을 복원 → localRotation만 애니메이션이 구동.
        private readonly List<(Transform bone, Vector3 pos, Vector3 scale)> _boneRestPose = new();

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
                sb.AppendLine($"  [{i}] \"{clips[i].name}\"  length={clips[i].length:F2}s  wrapMode={clips[i].wrapMode}");
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

            // ── 루트 모션 억제 체인 구성 ────────────────────────
            // Hip 본을 찾아, Hip → instance.Root(exclusive) 방향으로 거슬러 올라가며
            // 모든 중간 노드를 잠금 체인에 추가.
            // 이렇게 하면 Armature 등 Hip 위의 씬 노드가 회전/이동해도 억제된다.
            Transform hipBone = null;
            foreach (var t in gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLower().Contains("hip") || t.name.ToLower() == "pelvis")
                {
                    hipBone = t;
                    break;
                }
            }

            if (hipBone != null)
            {
                var chainLog = new StringBuilder("[GltfClipAnimator] 루트 모션 잠금 체인:\n");
                var cur = hipBone;
                while (cur != null && cur != gameObject.transform)
                {
                    _lockedChain.Add((cur, cur.localPosition, cur.localRotation));
                    chainLog.AppendLine(
                        $"  \"{cur.name}\"  pos={cur.localPosition:F3}  rot=({cur.localEulerAngles:F1})");
                    cur = cur.parent;
                }
                Debug.Log(chainLog.ToString());
            }
            else
            {
                // Hip 탐색 실패 → instance.Root 직계 자식 전체를 fallback 잠금
                var chainLog = new StringBuilder(
                    "[GltfClipAnimator] Hip 탐색 실패 — 직계 자식 fallback 잠금:\n");
                foreach (Transform child in gameObject.transform)
                {
                    _lockedChain.Add((child, child.localPosition, child.localRotation));
                    chainLog.AppendLine($"  \"{child.name}\"");
                }
                Debug.LogWarning(chainLog.ToString());
            }

            // ── 클립 사용 가능 여부 판정 ─────────────────────────────
            // combiner.js가 본 이름을 못 찾으면 채널이 0개 → clip.length ≈ 0
            int usableCount = 0;
            foreach (var kvp in _stateClipName)
            {
                var c = _anim.GetClip(kvp.Value);
                if (c != null && c.length > 0.01f) usableCount++;
            }
            HasUsableClips = usableCount > 0;

            if (!HasUsableClips)
            {
                // 본 이름 전체 덤프 → combiner.js 수정 가이드
                var boneNames = new StringBuilder(
                    $"[GltfClipAnimator] ⚠ 클립 데이터 없음 ({usableCount}/{_stateClipName.Count}개) — " +
                    "combiner.js 본 이름 불일치 가능성.\n모델 본 이름 목록:\n");
                foreach (var t in gameObject.GetComponentsInChildren<Transform>(true))
                    boneNames.AppendLine($"  \"{t.name}\"");
                Debug.LogWarning(boneNames.ToString());
                return; // 이하 초기화 불필요
            }

            // ── 전체 본 바인드 포즈 캡처 ────────────────────────────
            // Initialize 시점(SetState 호출 전) = 바인드 포즈 → 이 값을 LateUpdate에서 복원.
            foreach (var t in gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue; // 루트는 MascotLoader.LateUpdate가 관리
                _boneRestPose.Add((t, t.localPosition, t.localScale));
            }

            _initialized = true;
        }

        // ── 루트 모션 억제 (LateUpdate) ──────────────────────────

        /// <summary>
        /// Legacy Animation이 Update 이전에 본에 커브를 적용한 뒤,
        /// LateUpdate에서 잠금 체인 전체를 초기 바인드포즈로 덮어쓴다.
        /// Hip 단독 잠금으로 해결 안 될 경우 Armature 등 상위 노드까지 억제한다.
        /// </summary>
        private void LateUpdate()
        {
            // 1. 모든 본: 위치·스케일 바인드 포즈 복원 (translation/scale 채널 무력화)
            //    → localRotation은 건드리지 않아 클립 회전 애니메이션이 그대로 살아있음
            foreach (var (bone, pos, scale) in _boneRestPose)
            {
                bone.localPosition = pos;
                bone.localScale    = scale;
            }

            // 2. Hip 체인: 회전까지 완전 잠금 (루트 모션 억제)
            foreach (var (bone, pos, rot) in _lockedChain)
            {
                bone.localPosition = pos;
                bone.localRotation = rot;
            }
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
