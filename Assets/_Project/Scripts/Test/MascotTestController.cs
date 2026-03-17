using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UniGLTF;
using Guideon.Mascot;

namespace Guideon.Test
{
    /// <summary>
    /// GLB 마스코트 모델을 로드하고, BlendShape/Bone/Animation/프로시저럴 애니메이션을
    /// IMGUI 버튼으로 테스트할 수 있는 컨트롤러.
    /// 빈 씬에 빈 GameObject 하나 만들고 이 스크립트를 붙이면 된다.
    /// </summary>
    public class MascotTestController : MonoBehaviour
    {
        [Header("GLB 파일 경로 (Assets/ 기준)")]
        [SerializeField] private string glbPath = "_Project/Art/Models/Mascot/mascot.glb";

        [Header("모델 배치")]
        [SerializeField] private Vector3 modelPosition = new Vector3(0f, -1f, 3f);
        [SerializeField] private float modelScale = 1f;

        [Header("Jaw Lip-Sync 테스트")]
        [SerializeField] private float jawMaxAngle = 20f;
        [SerializeField] private float jawSpeed = 8f;

        // --- Runtime State ---
        private GameObject _modelRoot;
        private RuntimeGltfInstance _gltfInstance;

        // BlendShape
        private List<SkinnedMeshRenderer> _skinnedRenderers = new();
        private Dictionary<string, (SkinnedMeshRenderer renderer, int index)> _blendShapes = new();
        private Dictionary<string, float> _blendShapeValues = new();

        // Bones
        private Transform _jawBone;
        private List<Transform> _allBones = new();
        private Quaternion _jawClosedRot;

        // Animations
        private Animator _animator;
        private Animation _legacyAnimation;
        private List<string> _animClipNames = new();

        // Procedural Animation
        private ProceduralMascotAnimator _proceduralAnimator;
        private BoneRig _boneRig;

        // UV Picker
        private enum UVPickMode { None, LeftEye, RightEye, Mouth }
        private UVPickMode _uvPickMode = UVPickMode.None;
        private Vector2 _uvPickStart;
        private Vector2 _uvPickEnd;
        private bool _uvDragging;
        private Rect _leftEyeUV, _rightEyeUV, _mouthUV;
        private bool _leftEyeSet, _rightEyeSet, _mouthSet;
        private string _lastPickInfo = "";

        // UI State
        private Vector2 _scrollPos;
        private int _tabIndex;
        private readonly string[] _tabNames = { "Info", "BlendShape", "Bones", "Animation", "Jaw Test", "Procedural" };
        private bool _jawTestRunning;
        private float _jawTestTimer;
        private string _statusMessage = "Press 'Load GLB' to start";
        private bool _isLoaded;

        // Camera orbit
        private float _camDistance = 3f;
        private float _camYaw;
        private float _camPitch = 10f;

        void Update()
        {
            HandleCameraOrbit();
            UpdateJawTest();
            HandleUVPicker();
        }

        #region GLB Loading

        private async void LoadGlb()
        {
            // Cleanup previous
            if (_modelRoot != null)
            {
                Destroy(_modelRoot);
                _modelRoot = null;
            }
            ClearState();

            string fullPath = Path.Combine(Application.dataPath, glbPath);
            if (!File.Exists(fullPath))
            {
                _statusMessage = $"File not found: {fullPath}";
                Debug.LogError(_statusMessage);
                return;
            }

            _statusMessage = "Loading GLB...";

            try
            {
                byte[] glbBytes = File.ReadAllBytes(fullPath);
                var glbData = new GlbBinaryParser(glbBytes, "mascot").Parse();

                // URP 프로젝트이므로 URP material generator를 명시
                var materialGenerator = new UrpGltfMaterialDescriptorGenerator();
                using var loader = new ImporterContext(glbData, materialGenerator: materialGenerator);

                var awaitCaller = new RuntimeOnlyAwaitCaller();
                _gltfInstance = await loader.LoadAsync(awaitCaller);
                _gltfInstance.ShowMeshes();

                _modelRoot = _gltfInstance.Root;
                _modelRoot.transform.position = modelPosition;
                _modelRoot.transform.localScale = Vector3.one * modelScale;

                AnalyzeModel();
                SetupProceduralAnimator();
                _isLoaded = true;
                _statusMessage = $"Loaded! Bones: {_allBones.Count}, BlendShapes: {_blendShapes.Count}, Rig: {(_boneRig.IsValid ? "OK" : "Incomplete")}";
                Debug.Log(_statusMessage);
            }
            catch (System.Exception e)
            {
                _statusMessage = $"Load failed: {e.Message}";
                Debug.LogError($"[MascotTest] {e}");
            }
        }

        private void ClearState()
        {
            _skinnedRenderers.Clear();
            _blendShapes.Clear();
            _blendShapeValues.Clear();
            _allBones.Clear();
            _animClipNames.Clear();
            _jawBone = null;
            _animator = null;
            _legacyAnimation = null;
            _proceduralAnimator = null;
            _boneRig = null;
            _isLoaded = false;
            _jawTestRunning = false;
        }

        #endregion

        #region Model Analysis

        private void AnalyzeModel()
        {
            if (_modelRoot == null) return;

            // --- SkinnedMeshRenderer & BlendShapes ---
            _skinnedRenderers.AddRange(_modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>());
            foreach (var smr in _skinnedRenderers)
            {
                var mesh = smr.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    string key = $"{smr.gameObject.name}/{name}";
                    _blendShapes[key] = (smr, i);
                    _blendShapeValues[key] = 0f;
                }
            }

            // --- All Bones ---
            var allTransforms = _modelRoot.GetComponentsInChildren<Transform>();
            foreach (var t in allTransforms)
            {
                _allBones.Add(t);

                // Jaw bone 자동 탐색
                string lower = t.name.ToLower();
                if (lower.Contains("jaw") || lower.Contains("chin"))
                {
                    _jawBone = t;
                    _jawClosedRot = t.localRotation;
                    Debug.Log($"[MascotTest] Jaw bone found: {t.name}");
                }
            }

            // --- Animator ---
            _animator = _modelRoot.GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                {
                    _animClipNames.Add(clip.name);
                }
            }

            // --- Legacy Animation ---
            _legacyAnimation = _modelRoot.GetComponentInChildren<Animation>();
            if (_legacyAnimation != null)
            {
                foreach (AnimationState state in _legacyAnimation)
                {
                    if (!_animClipNames.Contains(state.name))
                        _animClipNames.Add(state.name);
                }
            }
        }

        private void SetupProceduralAnimator()
        {
            _boneRig = BoneRig.Build(_modelRoot);
            Debug.Log($"[MascotTest] BoneRig:\n{_boneRig.GetDebugInfo()}");

            if (!_boneRig.IsValid)
            {
                Debug.LogWarning("[MascotTest] BoneRig is incomplete - procedural animation may not work correctly.");
            }

            // 첫 번째 SkinnedMeshRenderer를 표정 텍스처 대상으로 사용
            var mainSmr = _skinnedRenderers.Count > 0 ? _skinnedRenderers[0] : null;

            _proceduralAnimator = _modelRoot.AddComponent<ProceduralMascotAnimator>();
            _proceduralAnimator.Initialize(_boneRig, mainSmr);

            // MeshCollider 추가 (UV 피커용 레이캐스트)
            if (mainSmr != null && mainSmr.GetComponent<MeshCollider>() == null)
            {
                var col = mainSmr.gameObject.AddComponent<MeshCollider>();
                col.sharedMesh = mainSmr.sharedMesh;
            }
        }

        #endregion

        #region Camera

        private void HandleCameraOrbit()
        {
            // Right mouse drag to orbit
            if (Input.GetMouseButton(1))
            {
                _camYaw += Input.GetAxis("Mouse X") * 3f;
                _camPitch -= Input.GetAxis("Mouse Y") * 3f;
                _camPitch = Mathf.Clamp(_camPitch, -80f, 80f);
            }

            _camDistance -= Input.GetAxis("Mouse ScrollWheel") * 2f;
            _camDistance = Mathf.Clamp(_camDistance, 0.5f, 20f);

            Vector3 target = modelPosition + Vector3.up * 1f;
            Quaternion rot = Quaternion.Euler(_camPitch, _camYaw, 0);
            Vector3 pos = target - rot * Vector3.forward * _camDistance;

            Camera.main.transform.position = pos;
            Camera.main.transform.LookAt(target);
        }

        #endregion

        #region UV Picker

        private void HandleUVPicker()
        {
            if (_uvPickMode == UVPickMode.None) return;
            if (!_isLoaded || Camera.main == null) return;

            // 왼쪽 클릭 시작 — 드래그로 영역 선택
            if (Input.GetMouseButtonDown(0) && !IsMouseOverUI())
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit))
                {
                    _uvPickStart = hit.textureCoord;
                    _uvDragging = true;
                    _lastPickInfo = $"Start UV: ({_uvPickStart.x:F3}, {_uvPickStart.y:F3})";
                }
            }

            // 드래그 중
            if (_uvDragging && Input.GetMouseButton(0))
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit))
                {
                    _uvPickEnd = hit.textureCoord;
                }
            }

            // 드래그 끝 — 영역 확정
            if (_uvDragging && Input.GetMouseButtonUp(0))
            {
                _uvDragging = false;

                float minU = Mathf.Min(_uvPickStart.x, _uvPickEnd.x);
                float minV = Mathf.Min(_uvPickStart.y, _uvPickEnd.y);
                float maxU = Mathf.Max(_uvPickStart.x, _uvPickEnd.x);
                float maxV = Mathf.Max(_uvPickStart.y, _uvPickEnd.y);
                var rect = new Rect(minU, minV, maxU - minU, maxV - minV);

                switch (_uvPickMode)
                {
                    case UVPickMode.LeftEye:
                        _leftEyeUV = rect;
                        _leftEyeSet = true;
                        _lastPickInfo = $"Left Eye UV set: {rect}";
                        break;
                    case UVPickMode.RightEye:
                        _rightEyeUV = rect;
                        _rightEyeSet = true;
                        _lastPickInfo = $"Right Eye UV set: {rect}";
                        break;
                    case UVPickMode.Mouth:
                        _mouthUV = rect;
                        _mouthSet = true;
                        _lastPickInfo = $"Mouth UV set: {rect}";
                        break;
                }

                _uvPickMode = UVPickMode.None;
                Debug.Log($"[MascotTest] {_lastPickInfo}");
            }
        }

        private bool IsMouseOverUI()
        {
            // IMGUI 패널 영역 체크 (왼쪽 380px)
            return Input.mousePosition.x < 390;
        }

        #endregion

        #region Jaw Test

        private void UpdateJawTest()
        {
            if (!_jawTestRunning || _jawBone == null) return;

            _jawTestTimer += Time.deltaTime * jawSpeed;
            float openAmount = (Mathf.Sin(_jawTestTimer) + 1f) * 0.5f; // 0~1
            float angle = openAmount * jawMaxAngle;
            _jawBone.localRotation = _jawClosedRot * Quaternion.Euler(-angle, 0, 0);
        }

        #endregion

        #region IMGUI

        void OnGUI()
        {
            float panelWidth = 380f;
            float panelHeight = Screen.height - 20f;

            GUILayout.BeginArea(new Rect(10, 10, panelWidth, panelHeight));
            GUILayout.BeginVertical("box");

            // Title
            GUILayout.Label("<size=16><b>Mascot Test Controller</b></size>", new GUIStyle(GUI.skin.label) { richText = true });

            // Status
            GUILayout.Label(_statusMessage);
            GUILayout.Space(5);

            // Load Button
            if (GUILayout.Button("Load GLB", GUILayout.Height(35)))
            {
                LoadGlb();
            }

            if (_isLoaded && GUILayout.Button("Unload", GUILayout.Height(25)))
            {
                if (_modelRoot != null) Destroy(_modelRoot);
                ClearState();
                _statusMessage = "Unloaded.";
            }

            GUILayout.Space(10);

            // Tabs
            _tabIndex = GUILayout.Toolbar(_tabIndex, _tabNames);
            GUILayout.Space(5);

            // Scroll area
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            switch (_tabIndex)
            {
                case 0: DrawInfoTab(); break;
                case 1: DrawBlendShapeTab(); break;
                case 2: DrawBonesTab(); break;
                case 3: DrawAnimationTab(); break;
                case 4: DrawJawTestTab(); break;
                case 5: DrawProceduralTab(); break;
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();

            // Controls help (bottom right)
            GUI.Label(new Rect(Screen.width - 310, Screen.height - 60, 300, 50),
                "<size=11>Right-click drag: Orbit | Scroll: Zoom</size>",
                new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.LowerRight });
        }

        private void DrawInfoTab()
        {
            if (!_isLoaded) { GUILayout.Label("Load a model first."); return; }

            GUILayout.Label($"<b>Hierarchy</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Label($"  Total transforms: {_allBones.Count}");
            GUILayout.Label($"  SkinnedMeshRenderers: {_skinnedRenderers.Count}");
            GUILayout.Label($"  BlendShapes: {_blendShapes.Count}");
            GUILayout.Label($"  Jaw bone: {(_jawBone != null ? _jawBone.name : "Not found")}");
            GUILayout.Label($"  Animator: {(_animator != null ? "Yes" : "No")}");
            GUILayout.Label($"  Legacy Animation: {(_legacyAnimation != null ? "Yes" : "No")}");
            GUILayout.Label($"  Animation clips: {_animClipNames.Count}");

            GUILayout.Space(10);
            GUILayout.Label("<b>Materials & Textures:</b>", new GUIStyle(GUI.skin.label) { richText = true });
            var allRenderers = _modelRoot.GetComponentsInChildren<Renderer>();
            foreach (var rend in allRenderers)
            {
                GUILayout.Label($"  [{rend.gameObject.name}]");
                foreach (var mat in rend.materials)
                {
                    if (mat == null) continue;
                    string texName = mat.mainTexture != null
                        ? $"{mat.mainTexture.name} ({mat.mainTexture.width}x{mat.mainTexture.height})"
                        : "no texture";
                    GUILayout.Label($"    mat: {mat.name}  tex: {texName}");
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>Bone Rig Mapping:</b>", new GUIStyle(GUI.skin.label) { richText = true });
            if (_boneRig != null)
            {
                GUILayout.Label(_boneRig.GetDebugInfo());
                GUILayout.Label($"\n  Rig valid: {(_boneRig.IsValid ? "<color=green>YES</color>" : "<color=red>NO</color>")}",
                    new GUIStyle(GUI.skin.label) { richText = true });
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>Bone-like transforms:</b>", new GUIStyle(GUI.skin.label) { richText = true });
            foreach (var bone in _allBones)
            {
                string lower = bone.name.ToLower();
                bool isBoneKeyword = lower.Contains("bone") || lower.Contains("spine") ||
                                      lower.Contains("arm") || lower.Contains("leg") ||
                                      lower.Contains("head") || lower.Contains("neck") ||
                                      lower.Contains("jaw") || lower.Contains("hip") ||
                                      lower.Contains("hand") || lower.Contains("foot") ||
                                      lower.Contains("finger") || lower.Contains("shoulder") ||
                                      lower.Contains("chest") || lower.Contains("eye");
                if (isBoneKeyword)
                {
                    GUILayout.Label($"  - {bone.name}");
                }
            }
        }

        private void DrawBlendShapeTab()
        {
            if (!_isLoaded) { GUILayout.Label("Load a model first."); return; }
            if (_blendShapes.Count == 0) { GUILayout.Label("No BlendShapes found in this model."); return; }

            // Reset all button
            if (GUILayout.Button("Reset All to 0"))
            {
                var keys = new List<string>(_blendShapeValues.Keys);
                foreach (var key in keys)
                {
                    _blendShapeValues[key] = 0f;
                    var (renderer, index) = _blendShapes[key];
                    renderer.SetBlendShapeWeight(index, 0f);
                }
            }

            GUILayout.Space(5);

            foreach (var kvp in _blendShapes)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(kvp.Key, GUILayout.Width(200));
                float val = _blendShapeValues[kvp.Key];
                float newVal = GUILayout.HorizontalSlider(val, 0f, 100f, GUILayout.Width(120));
                GUILayout.Label($"{newVal:F0}", GUILayout.Width(30));
                GUILayout.EndHorizontal();

                if (!Mathf.Approximately(val, newVal))
                {
                    _blendShapeValues[kvp.Key] = newVal;
                    kvp.Value.renderer.SetBlendShapeWeight(kvp.Value.index, newVal);
                }
            }
        }

        private void DrawBonesTab()
        {
            if (!_isLoaded) { GUILayout.Label("Load a model first."); return; }

            GUILayout.Label("Click a bone to highlight it (yellow gizmo in Scene view).");
            GUILayout.Space(5);

            foreach (var bone in _allBones)
            {
                if (bone == null) continue;
                string lower = bone.name.ToLower();
                bool isNotMesh = !lower.Contains("mesh") && !lower.Contains("renderer");
                if (!isNotMesh) continue;

                if (GUILayout.Button(bone.name, GUILayout.Height(22)))
                {
                    // Rotate bone slightly to see which part moves
                    bone.localRotation *= Quaternion.Euler(0, 0, 15f);
                    Debug.Log($"[MascotTest] Rotated bone: {bone.name} (+15 deg Z)");
                }
            }
        }

        private void DrawAnimationTab()
        {
            if (!_isLoaded) { GUILayout.Label("Load a model first."); return; }

            if (_animClipNames.Count == 0)
            {
                GUILayout.Label("No animation clips found in this model.");
                GUILayout.Space(5);
                GUILayout.Label("(Tripo AI GLB models typically don't\ninclude animations. That's normal.)");
                GUILayout.Space(10);
                GUILayout.Label("<b>Use the 'Procedural' tab instead!</b>",
                    new GUIStyle(GUI.skin.label) { richText = true });
                return;
            }

            foreach (var clipName in _animClipNames)
            {
                if (GUILayout.Button($"Play: {clipName}", GUILayout.Height(30)))
                {
                    if (_legacyAnimation != null)
                    {
                        _legacyAnimation.Play(clipName);
                    }
                    else if (_animator != null)
                    {
                        _animator.Play(clipName);
                    }
                    Debug.Log($"[MascotTest] Playing animation: {clipName}");
                }
            }
        }

        private void DrawJawTestTab()
        {
            if (!_isLoaded) { GUILayout.Label("Load a model first."); return; }

            if (_jawBone == null)
            {
                GUILayout.Label("<color=yellow>Jaw bone not found automatically.</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Space(5);
                GUILayout.Label("Select a bone from the Bones tab to\nidentify which one is the jaw.");
                GUILayout.Space(5);
                GUILayout.Label("Bone-like transforms:");
                foreach (var bone in _allBones)
                {
                    if (bone == null) continue;
                    string lower = bone.name.ToLower();
                    if (lower.Contains("head") || lower.Contains("jaw") ||
                        lower.Contains("chin") || lower.Contains("mouth"))
                    {
                        if (GUILayout.Button($"Set as Jaw: {bone.name}"))
                        {
                            _jawBone = bone;
                            _jawClosedRot = bone.localRotation;
                            Debug.Log($"[MascotTest] Jaw bone set to: {bone.name}");
                        }
                    }
                }

                // Fallback: show all bones
                GUILayout.Space(5);
                GUILayout.Label("Or pick from all transforms:");
                foreach (var bone in _allBones)
                {
                    if (bone == null || bone == _modelRoot.transform) continue;
                    if (GUILayout.Button($"Set as Jaw: {bone.name}", GUILayout.Height(20)))
                    {
                        _jawBone = bone;
                        _jawClosedRot = bone.localRotation;
                    }
                }
                return;
            }

            GUILayout.Label($"Jaw bone: <b>{_jawBone.name}</b>",
                new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Space(5);

            jawMaxAngle = GUILayout.HorizontalSlider(jawMaxAngle, 5f, 45f);
            GUILayout.Label($"Max angle: {jawMaxAngle:F1}");

            jawSpeed = GUILayout.HorizontalSlider(jawSpeed, 1f, 20f);
            GUILayout.Label($"Speed: {jawSpeed:F1}");

            GUILayout.Space(10);

            if (!_jawTestRunning)
            {
                if (GUILayout.Button("Start Jaw Animation", GUILayout.Height(35)))
                {
                    _jawTestRunning = true;
                    _jawTestTimer = 0f;
                }
            }
            else
            {
                if (GUILayout.Button("Stop Jaw Animation", GUILayout.Height(35)))
                {
                    _jawTestRunning = false;
                    if (_jawBone != null)
                        _jawBone.localRotation = _jawClosedRot;
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Manual jaw open:");
            if (GUILayout.RepeatButton("Open Jaw"))
            {
                _jawBone.localRotation = _jawClosedRot * Quaternion.Euler(-jawMaxAngle, 0, 0);
            }
            if (GUILayout.Button("Close Jaw"))
            {
                _jawBone.localRotation = _jawClosedRot;
            }
        }

        private void DrawProceduralTab()
        {
            if (!_isLoaded) { GUILayout.Label("Load a model first."); return; }

            if (_boneRig == null || !_boneRig.IsValid)
            {
                GUILayout.Label("<color=red>BoneRig is invalid or incomplete.</color>",
                    new GUIStyle(GUI.skin.label) { richText = true });
                GUILayout.Space(5);
                if (_boneRig != null)
                {
                    GUILayout.Label(_boneRig.GetDebugInfo());
                }
                GUILayout.Label("\nHead and Spine bones are required\nfor procedural animation.");
                return;
            }

            var richStyle = new GUIStyle(GUI.skin.label) { richText = true };

            GUILayout.Label("<b>Procedural Animation Test</b>", richStyle);
            GUILayout.Label($"Current State: <color=cyan>{_proceduralAnimator.CurrentState}</color>", richStyle);
            GUILayout.Space(10);

            // 상태 버튼들
            GUILayout.Label("<b>States:</b>", richStyle);
            GUILayout.Space(5);

            if (GUILayout.Button("Idle  (대기)", GUILayout.Height(40)))
            {
                _jawTestRunning = false; // Jaw 테스트 중지
                _proceduralAnimator.SetState(MascotState.Idle);
                Debug.Log("[MascotTest] State → Idle");
            }

            if (GUILayout.Button("Greeting  (인사)", GUILayout.Height(40)))
            {
                _jawTestRunning = false;
                _proceduralAnimator.SetState(MascotState.Greeting);
                Debug.Log("[MascotTest] State → Greeting");
            }

            if (GUILayout.Button("Listening  (듣는 중)", GUILayout.Height(40)))
            {
                _jawTestRunning = false;
                _proceduralAnimator.SetState(MascotState.Listening);
                Debug.Log("[MascotTest] State → Listening");
            }

            if (GUILayout.Button("Thinking  (생각 중)", GUILayout.Height(40)))
            {
                _jawTestRunning = false;
                _proceduralAnimator.SetState(MascotState.Thinking);
                Debug.Log("[MascotTest] State → Thinking");
            }

            if (GUILayout.Button("Speaking  (말하는 중)", GUILayout.Height(40)))
            {
                _jawTestRunning = false;
                _proceduralAnimator.SetState(MascotState.Speaking);
                Debug.Log("[MascotTest] State → Speaking");
            }

            GUILayout.Space(15);

            // 리셋
            if (GUILayout.Button("Reset Pose", GUILayout.Height(30)))
            {
                _boneRig.ResetAll();
                _proceduralAnimator.SetState(MascotState.Idle);
                Debug.Log("[MascotTest] Pose reset");
            }

            GUILayout.Space(15);

            // ===== 표정 텍스처 설정 =====
            GUILayout.Label("<b>--- Face Texture Expression ---</b>", richStyle);
            var faceTex = _proceduralAnimator.FaceTexture;

            if (faceTex == null)
            {
                GUILayout.Label("<color=red>FaceTextureModifier not initialized.</color>", richStyle);
            }
            else if (!faceTex.RegionsSet)
            {
                GUILayout.Label("Step 1: Save texture to see UV layout", richStyle);
                if (GUILayout.Button("Save Texture PNG", GUILayout.Height(30)))
                {
                    string path = faceTex.SaveOriginalTextureToDisk();
                    if (path != null)
                        _lastPickInfo = $"Saved: {path}";
                }

                GUILayout.Space(5);
                GUILayout.Label("Step 2: Pick face regions on model\n(Click and drag on the 3D model)", richStyle);

                // UV Pick 모드 버튼
                string pickLabel = _uvPickMode != UVPickMode.None
                    ? $"<color=yellow>Picking: {_uvPickMode}... Click on model</color>"
                    : "Select a region to pick:";
                GUILayout.Label(pickLabel, richStyle);

                GUI.enabled = _uvPickMode == UVPickMode.None;

                if (GUILayout.Button(_leftEyeSet ? "Left Eye  [SET]" : "Pick Left Eye", GUILayout.Height(28)))
                    _uvPickMode = UVPickMode.LeftEye;
                if (GUILayout.Button(_rightEyeSet ? "Right Eye  [SET]" : "Pick Right Eye", GUILayout.Height(28)))
                    _uvPickMode = UVPickMode.RightEye;
                if (GUILayout.Button(_mouthSet ? "Mouth  [SET]" : "Pick Mouth", GUILayout.Height(28)))
                    _uvPickMode = UVPickMode.Mouth;

                GUI.enabled = true;

                if (!string.IsNullOrEmpty(_lastPickInfo))
                {
                    GUILayout.Space(3);
                    GUILayout.Label(_lastPickInfo);
                }

                GUILayout.Space(5);

                // 3개 다 설정되면 적용 버튼
                if (_leftEyeSet && _rightEyeSet && _mouthSet)
                {
                    GUILayout.Label("<color=green>All regions set!</color>", richStyle);
                    if (GUILayout.Button("Apply Face Regions", GUILayout.Height(35)))
                    {
                        faceTex.SetFaceRegions(_leftEyeUV, _rightEyeUV, _mouthUV);
                        Debug.Log("[MascotTest] Face regions applied!");
                    }

                    if (GUILayout.Button("Debug: Show Region Borders", GUILayout.Height(25)))
                    {
                        faceTex.SetFaceRegions(_leftEyeUV, _rightEyeUV, _mouthUV);
                        faceTex.DebugDrawRegions();
                    }
                }
            }
            else
            {
                GUILayout.Label("<color=green>Face regions configured!</color>", richStyle);
                GUILayout.Label("Expression changes with animation state.", richStyle);

                if (GUILayout.Button("Debug: Show Borders", GUILayout.Height(25)))
                {
                    faceTex.DebugDrawRegions();
                }
                if (GUILayout.Button("Reset Regions", GUILayout.Height(25)))
                {
                    _leftEyeSet = false;
                    _rightEyeSet = false;
                    _mouthSet = false;
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("<b>Bone Rig:</b>", richStyle);
            GUILayout.Label(_boneRig.GetDebugInfo());
        }

        #endregion
    }
}
