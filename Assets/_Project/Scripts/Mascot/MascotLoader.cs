using System;
using Cysharp.Threading.Tasks;
using Guideon.Core;
using Guideon.Network;
using UniGLTF;
using UnityEngine;
using UnityEngine.Networking;

namespace Guideon.Mascot
{
    /// <summary>
    /// GLB 마스코트 런타임 로더.
    /// 서버 URL 또는 로컬 에셋에서 GLB를 불러와 씬에 배치하고
    /// ProceduralMascotAnimator를 자동 연결한다.
    /// </summary>
    public class MascotLoader : MonoBehaviour
    {
        [Header("기본 마스코트 (fallback)")]
        [SerializeField] private string _defaultGlbAssetPath = "_Project/Art/Models/Mascot/mascot.glb";

        [Header("배치")]
        [SerializeField] private Transform _mountPoint;
        [SerializeField] private Vector3 _positionOffset = Vector3.zero;
        [SerializeField] private Vector3 _rotation = Vector3.zero;
        [SerializeField] private float _scale = 1f;

        private RuntimeGltfInstance _currentInstance;
        private ProceduralMascotAnimator _animator;
        private GameObject _modelRoot; // LateUpdate 트랜스폼 라이브 적용용

        public ProceduralMascotAnimator Animator => _animator;
        public bool IsLoaded => _currentInstance != null;

        private void Start()
        {
            LoadMascotAsync().Forget();
        }

        /// <summary>
        /// 부트스트랩에서 받은 서버 URL로 로드 시도. 없으면 로컬 fallback.
        /// </summary>
        private async UniTaskVoid LoadMascotAsync()
        {
            string url = AuthManager.HasInstance
                ? AuthManager.Instance.BootstrapData?.Mascot?.ModelUrl
                : null;

            if (!string.IsNullOrEmpty(url))
            {
                Debug.Log($"[MascotLoader] 서버 모델 로드: {url}");
                await LoadFromUrlAsync(url);
            }
            else
            {
                Debug.Log("[MascotLoader] 서버 URL 없음 → 로컬 fallback");
                await LoadDefaultAsync();
            }
        }

        /// <summary>
        /// Play 중 Inspector에서 _rotation/_positionOffset/_scale을 바꾸면 즉시 반영.
        /// ProceduralMascotAnimator는 본(localRotation)만 조작하므로 루트 트랜스폼과 무충돌.
        /// </summary>
        private void LateUpdate()
        {
            if (_modelRoot == null) return;
            _modelRoot.transform.localPosition = _positionOffset;
            _modelRoot.transform.localRotation = Quaternion.Euler(_rotation);
            _modelRoot.transform.localScale    = Vector3.one * _scale;
        }

        // ── Public API ──────────────────────────────────────

        public async UniTask LoadDefaultAsync()
        {
            string fullPath = System.IO.Path.Combine(Application.dataPath, _defaultGlbAssetPath);
            if (!System.IO.File.Exists(fullPath))
            {
                Debug.LogError($"[MascotLoader] 기본 GLB 없음: {fullPath}");
                return;
            }
            byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
            await LoadBytesAsync(bytes, "mascot_default");
        }

        /// <summary>서버 URL에서 GLB 다운로드 후 교체.</summary>
        public async UniTask LoadFromUrlAsync(string url)
        {
            Debug.Log($"[MascotLoader] URL 로딩: {url}");
            using var req = UnityWebRequest.Get(url);
            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MascotLoader] 다운로드 실패: {req.error}. 기본 마스코트 유지.");
                return;
            }
            await LoadBytesAsync(req.downloadHandler.data, "mascot_server");
        }

        /// <summary>현재 마스코트 교체 (이미 로드된 GameObject).</summary>
        public void SwapMascot(GameObject newModel)
        {
            DestroyCurrentInstance();
            _modelRoot = newModel;
            SetupModel(newModel);
        }

        public void SetState(MascotState state) => _animator?.SetState(state);

        // ── Internal ─────────────────────────────────────────

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
                _modelRoot = instance.Root;
                SetupModel(instance.Root);

                // 로드 완료 → 화면 컨트롤러가 RT 재주입(repaint 강제)
                EventBus.Publish(new MascotLoadedEvent());
                Debug.Log($"[MascotLoader] 로드 완료: {name}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MascotLoader] 로드 실패: {e.Message}");
            }
        }

        private void SetupModel(GameObject model)
        {
            var parent = _mountPoint != null ? _mountPoint : transform;
            model.transform.SetParent(parent, false);
            model.transform.localPosition = _positionOffset;
            model.transform.localRotation = Quaternion.Euler(_rotation);
            model.transform.localScale = Vector3.one * _scale;

            // GLB 로드 시 생성된 모든 오브젝트를 부모와 같은 레이어로 맞춤
            // (MascotCamera가 Mascot 레이어만 렌더링하기 때문에 필수)
            int layer = parent.gameObject.layer;
            if (layer > 0)
            {
                foreach (var t in model.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = layer;
            }

            var rig = BoneRig.Build(model);
            _animator = model.AddComponent<ProceduralMascotAnimator>();
            _animator.Initialize(rig);
            _animator.SetState(MascotState.Idle);
        }

        private void DestroyCurrentInstance()
        {
            if (_currentInstance != null)
            {
                Destroy(_currentInstance.Root);
                _currentInstance = null;
                _animator = null;
                _modelRoot = null;
            }
        }
    }
}
