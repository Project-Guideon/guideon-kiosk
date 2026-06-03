using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guideon.Core;
using Guideon.Network;
using Guideon.Network.Models;
using UniGLTF;
using UnityEngine;
using UnityEngine.Networking;

namespace Guideon.Mascot
{
    /// <summary>
    /// GLB 마스코트 런타임 로더.
    ///
    /// 우선순위:
    ///   1. animModelUrl 있음  → anim GLB(메쉬+클립 내장) 로드 → GltfClipAnimator
    ///   2. animModelUrl 없음  → base modelUrl(또는 로컬 fallback) → ProceduralMascotAnimator
    ///
    /// 부트 선행 캐싱: BootSceneController가 PreloadAsync(MascotData)를 호출해
    /// Main 씬 로딩 전에 GLB 바이트를 받아둔다.
    /// </summary>
    public class MascotLoader : MonoBehaviour
    {
        [Header("기본 마스코트 (fallback)")]
        [SerializeField] private string _defaultGlbAssetPath = "_Project/Art/Models/Mascot/mascot.glb";

        [Header("배치")]
        [SerializeField] private Transform _mountPoint;
        [SerializeField] private Vector3 _positionOffset = Vector3.zero;
        [SerializeField] private Vector3 _rotation = Vector3.zero;
        [SerializeField] private float _scale = 1.5f;

        // ── 부트 선행 캐시 (static) ─────────────────────────────

        /// <summary>Boot 씬에서 미리 받아둔 GLB 바이트. null이면 런타임 다운로드.</summary>
        public static byte[] CachedModelBytes    { get; private set; }
        public static string  CachedModelName    { get; private set; }
        /// <summary>캐시된 GLB에 애니메이션 클립이 내장돼 있으면 true.</summary>
        public static bool    CachedHasAnim      { get; private set; }
        /// <summary>서버 animClips 맵 (상태→클립명). null이면 순서 폴백 사용.</summary>
        public static Dictionary<string, string> CachedAnimClips { get; private set; }

        /// <summary>
        /// BootSceneController에서 호출.
        /// animModelUrl 우선으로 다운로드, 없으면 base modelUrl, 둘 다 없으면 로컬 fallback.
        /// 실패해도 Main 씬에서 fallback 재시도 가능 — 예외 throw 안 함.
        /// </summary>
        public static async UniTask<bool> PreloadAsync(MascotData mascot)
        {
            // 1순위: anim GLB (메쉬+클립)
            if (mascot != null && !string.IsNullOrEmpty(mascot.AnimModelUrl))
            {
                bool ok = await DownloadBytesAsync(mascot.AnimModelUrl, "mascot_anim");
                if (ok)
                {
                    CachedHasAnim    = true;
                    CachedAnimClips  = mascot.AnimClips;
                    Debug.Log($"[MascotLoader] 선행 캐싱 완료 (anim GLB) — {CachedModelBytes.Length} bytes");
                    return true;
                }
                Debug.LogWarning("[MascotLoader] anim GLB 다운로드 실패 → base 모델로 폴백");
            }

            // 2순위: base GLB (리깅만)
            string baseUrl = mascot?.ModelUrl;
            if (!string.IsNullOrEmpty(baseUrl))
            {
                bool ok = await DownloadBytesAsync(baseUrl, "mascot_server");
                if (ok)
                {
                    CachedHasAnim   = false;
                    CachedAnimClips = null;
                    Debug.Log($"[MascotLoader] 선행 캐싱 완료 (서버 base) — {CachedModelBytes.Length} bytes");
                    return true;
                }
                Debug.LogWarning("[MascotLoader] base 모델 다운로드 실패 → 로컬 fallback");
            }

            // 3순위: 로컬 파일
            return LoadLocalFallback();
        }

        /// <summary>기존 API 호환용 (modelUrl만 있을 때). anim 없음으로 처리.</summary>
        public static async UniTask<bool> PreloadBytesAsync(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                bool ok = await DownloadBytesAsync(url, "mascot_server");
                if (ok)
                {
                    CachedHasAnim   = false;
                    CachedAnimClips = null;
                    return true;
                }
                return false;
            }
            return LoadLocalFallback();
        }

        private static async UniTask<bool> DownloadBytesAsync(string url, string name)
        {
            try
            {
                using var req = UnityWebRequest.Get(url);
                await req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[MascotLoader] 다운로드 실패 ({name}): {req.error}");
                    return false;
                }
                CachedModelBytes = req.downloadHandler.data;
                CachedModelName  = name;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MascotLoader] 다운로드 예외 ({name}): {e.Message}");
                return false;
            }
        }

        private static bool LoadLocalFallback()
        {
            string path = System.IO.Path.Combine(
                Application.dataPath, "_Project/Art/Models/Mascot/mascot.glb");
            if (!System.IO.File.Exists(path))
            {
                Debug.LogWarning($"[MascotLoader] 로컬 GLB 없음: {path}");
                return false;
            }
            CachedModelBytes = System.IO.File.ReadAllBytes(path);
            CachedModelName  = "mascot_default";
            CachedHasAnim    = false;
            CachedAnimClips  = null;
            Debug.Log($"[MascotLoader] 선행 캐싱 완료 (로컬) — {CachedModelBytes.Length} bytes");
            return true;
        }

        // ── 인스턴스 필드 ────────────────────────────────────────

        private RuntimeGltfInstance _currentInstance;
        private IMascotAnimator _animator;
        private GameObject _modelRoot;

        // 런타임 로드 시 anim 여부를 추적 (캐시 없는 경로용)
        private bool _instanceHasAnim;
        private Dictionary<string, string> _instanceAnimClips;

        public bool IsLoaded => _currentInstance != null;

        private void Start()
        {
            LoadMascotAsync().Forget();
        }

        /// <summary>
        /// Play 중 Inspector에서 _rotation/_positionOffset/_scale을 바꾸면 즉시 반영.
        /// 클립 애니메이션은 본(localRotation)만 조작하므로 루트 트랜스폼과 무충돌.
        /// </summary>
        private void LateUpdate()
        {
            if (_modelRoot == null) return;
            _modelRoot.transform.localPosition = _positionOffset;
            _modelRoot.transform.localRotation = Quaternion.Euler(_rotation);
            _modelRoot.transform.localScale    = Vector3.one * _scale;
        }

        // ── Public API ──────────────────────────────────────────

        public void SetState(MascotState state) => _animator?.SetState(state);

        public async UniTask LoadDefaultAsync()
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath, _defaultGlbAssetPath);
            if (!System.IO.File.Exists(fullPath))
            {
                Debug.LogError($"[MascotLoader] 기본 GLB 없음: {fullPath}");
                return;
            }
            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
            _instanceHasAnim    = false;
            _instanceAnimClips  = null;
            await LoadBytesAsync(bytes, "mascot_default");
        }

        public async UniTask LoadFromUrlAsync(string url, bool hasAnim = false,
            Dictionary<string, string> animClips = null)
        {
            Debug.Log($"[MascotLoader] URL 로딩: {url}");
            using var req = UnityWebRequest.Get(url);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MascotLoader] 다운로드 실패: {req.error}. 기본 마스코트 유지.");
                return;
            }
            _instanceHasAnim   = hasAnim;
            _instanceAnimClips = animClips;
            await LoadBytesAsync(req.downloadHandler.data, "mascot_server");
        }

        public void SwapMascot(GameObject newModel)
        {
            DestroyCurrentInstance();
            _modelRoot = newModel;
            SetupModel(newModel, null); // 외부 교체는 anim 없음으로 처리
        }

        // ── Internal ─────────────────────────────────────────────

        private async UniTaskVoid LoadMascotAsync()
        {
            if (CachedModelBytes != null)
            {
                Debug.Log("[MascotLoader] 캐시 바이트로 파싱 — 다운로드 생략");
                _instanceHasAnim   = CachedHasAnim;
                _instanceAnimClips = CachedAnimClips;
                await LoadBytesAsync(CachedModelBytes, CachedModelName ?? "mascot_cached");
                return;
            }

            // 캐시 없음 → 직접 로드
            var mascot = AuthManager.HasInstance
                ? AuthManager.Instance.BootstrapData?.Mascot
                : null;

            // anim GLB 우선
            if (mascot != null && !string.IsNullOrEmpty(mascot.AnimModelUrl))
            {
                Debug.Log($"[MascotLoader] 캐시 없음 → anim GLB 직접 로드: {mascot.AnimModelUrl}");
                await LoadFromUrlAsync(mascot.AnimModelUrl, hasAnim: true, animClips: mascot.AnimClips);
                return;
            }

            // base GLB
            if (mascot != null && !string.IsNullOrEmpty(mascot.ModelUrl))
            {
                Debug.Log($"[MascotLoader] 캐시 없음 → base GLB 직접 로드: {mascot.ModelUrl}");
                await LoadFromUrlAsync(mascot.ModelUrl);
                return;
            }

            Debug.Log("[MascotLoader] 캐시 없음 → 로컬 fallback");
            await LoadDefaultAsync();
        }

        private async UniTask LoadBytesAsync(byte[] bytes, string name)
        {
            try
            {
                var glbData = new GlbBinaryParser(bytes, name).Parse();
                var materialGenerator = new UrpGltfMaterialDescriptorGenerator();
                using var loader = new ImporterContext(glbData, materialGenerator: materialGenerator);
                var instance = await loader.LoadAsync(new RuntimeOnlyAwaitCaller());
                instance.ShowMeshes();

                DestroyCurrentInstance();
                _currentInstance = instance;
                _modelRoot       = instance.Root;
                SetupModel(instance.Root, instance);

                EventBus.Publish(new MascotLoadedEvent());
                Debug.Log($"[MascotLoader] 로드 완료: {name}  hasAnim={_instanceHasAnim}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MascotLoader] 로드 실패: {e.Message}");
            }
        }

        /// <param name="instance">UniGLTF RuntimeGltfInstance. null이면 외부 교체(SwapMascot) 경로.</param>
        private void SetupModel(GameObject model, RuntimeGltfInstance instance)
        {
            var parent = _mountPoint != null ? _mountPoint : transform;
            model.transform.SetParent(parent, false);
            model.transform.localPosition = _positionOffset;
            model.transform.localRotation = Quaternion.Euler(_rotation);
            model.transform.localScale    = Vector3.one * _scale;

            // GLB 로드 시 생성된 모든 오브젝트를 부모와 같은 레이어로 맞춤
            int layer = parent.gameObject.layer;
            if (layer > 0)
            {
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = layer;
            }

            // Tripo retarget 클립이 캐릭터 체형과 맞지 않아 어색하므로
            // 메쉬/리깅은 anim GLB를 그대로 사용하되 애니메이션은 프로시저럴로 구동.
            // GltfClipAnimator는 백엔드 애니메이션 품질이 확보되면 재활성화.
            var rig = BoneRig.Build(model);
            var proceduralAnimator = model.AddComponent<ProceduralMascotAnimator>();
            proceduralAnimator.Initialize(rig);
            _animator = proceduralAnimator;
            _animator.SetState(MascotState.Idle);
        }

        private void DestroyCurrentInstance()
        {
            if (_currentInstance != null)
            {
                Destroy(_currentInstance.Root);
                _currentInstance = null;
                _animator        = null;
                _modelRoot       = null;
            }
        }
    }
}
