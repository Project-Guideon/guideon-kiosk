#if UNITY_EDITOR
using Guideon.Mascot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Guideon.Editor
{
    /// <summary>
    /// Tools > GUIDEON > Setup Mascot Stage 메뉴.
    ///
    /// Main 씬에서 실행하면 MascotStage 계층을 자동 생성·배선한다:
    ///   MascotStage (MascotStage 컴포넌트)
    ///     ├── MascotCamera (Camera)
    ///     ├── MascotLight  (Light)
    ///     └── MascotMount  (MascotLoader 컴포넌트, 빈 Transform)
    ///
    /// 실행 후 해야 할 일:
    ///   1. Mascot 레이어 설정 (콘솔에 출력되는 안내 확인)
    ///   2. MainSceneController Inspector에서 Mascot Stage 필드 연결
    /// </summary>
    public static class MascotStageSetup
    {
        private const string StageName  = "MascotStage";
        private const string CamName    = "MascotCamera";
        private const string LightName  = "MascotLight";
        private const string MountName  = "MascotMount";

        [MenuItem("Tools/GUIDEON/Setup Mascot Stage")]
        public static void SetupMascotStage()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // ── 1. Mascot 레이어 확보 ────────────────────────
            int mascotLayer = EnsureLayer("Mascot");

            // ── 2. 루트 GameObject ───────────────────────────
            var stageGo = GetOrCreate(StageName);
            var stage   = GetOrAddComponent<MascotStage>(stageGo);

            // ── 3. MascotCamera ──────────────────────────────
            var camGo   = GetOrCreateChild(stageGo, CamName);
            var cam     = GetOrAddComponent<Camera>(camGo);

            // 카메라 위치: 마스코트 앞 적당한 거리
            camGo.transform.localPosition = new Vector3(0f, 0.8f, -2.5f);
            camGo.transform.localRotation = Quaternion.identity;

            // 배경 투명 (URP에서 실제 투명은 가이드 문서 참조)
            cam.clearFlags    = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 10f;
            cam.fieldOfView   = 35f;
            cam.depth         = -5; // 메인 카메라보다 낮은 depth

            // Mascot 레이어만 렌더링 (레이어가 있을 때만 설정)
            if (mascotLayer >= 0)
                cam.cullingMask = 1 << mascotLayer;

            // ── 4. MascotLight ───────────────────────────────
            var lightGo = GetOrCreateChild(stageGo, LightName);
            var light   = GetOrAddComponent<Light>(lightGo);

            lightGo.transform.localPosition = new Vector3(0.5f, 2f, -1.5f);
            lightGo.transform.localRotation = Quaternion.Euler(45f, -30f, 0f);
            light.type      = LightType.Directional;
            light.intensity = 1.2f;
            light.color     = new Color(1f, 0.95f, 0.88f); // 따뜻한 조명

            // Mascot 레이어만 비춤
            if (mascotLayer >= 0)
                light.cullingMask = 1 << mascotLayer;

            // ── 5. MascotMount (MascotLoader) ────────────────
            var mountGo = GetOrCreateChild(stageGo, MountName);
            mountGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            var loader = GetOrAddComponent<MascotLoader>(mountGo);

            // Mascot 레이어 할당
            if (mascotLayer >= 0)
            {
                stageGo.layer  = mascotLayer;
                camGo.layer    = mascotLayer;
                lightGo.layer  = mascotLayer;
                mountGo.layer  = mascotLayer;
            }

            // ── 6. MascotStage 내부 참조 배선 ────────────────
            stage.Editor_SetRefs(cam, loader);

            // ── 7. 씬에 MainSceneController 자동 연결 시도 ───
            var allBehaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (var mb in allBehaviours)
            {
                if (mb.GetType().Name == "MainSceneController")
                {
                    var so     = new SerializedObject(mb);
                    var prop   = so.FindProperty("_mascotStage");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = stage;
                        so.ApplyModifiedProperties();
                        Debug.Log("[GUIDEON] MainSceneController._mascotStage 자동 연결 완료.");
                    }
                    break;
                }
            }

            // ── 완료 메시지 ───────────────────────────────────
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Selection.activeGameObject = stageGo;

            string layerNote = mascotLayer >= 0
                ? $"레이어 '{LayerMask.LayerToName(mascotLayer)}'(index {mascotLayer}) 적용 완료."
                : "⚠️ 'Mascot' 레이어를 찾지 못했습니다. Project Settings > Tags & Layers에서 수동 추가 후 카메라/라이트의 Culling Mask를 설정하세요.";

            Debug.Log(
                $"[GUIDEON] Setup Mascot Stage 완료!\n" +
                $"  {layerNote}\n" +
                $"  다음 단계:\n" +
                $"  1. MainSceneController Inspector → 'Mascot Stage' 필드에 MascotStage 오브젝트를 드래그.\n" +
                $"  2. MascotCamera의 Position/FOV를 원하는 구도로 조정.\n" +
                $"  3. Play → Idle 화면 mascot-slot에 마스코트가 렌더링되는지 확인.\n" +
                $"  4. 배경이 검게 보이면 가이드(docs/UNITY_MASCOT_SETUP_GUIDE.md)의 URP 투명 배경 섹션 참고."
            );
        }

        // ── 유틸리티 ─────────────────────────────────────────

        private static int EnsureLayer(string layerName)
        {
            int idx = LayerMask.NameToLayer(layerName);
            if (idx >= 0) return idx;

            // TagManager에서 빈 슬롯을 찾아 레이어 생성
            var tagManager = new SerializedObject(
                AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
            var layersProp = tagManager.FindProperty("layers");
            if (layersProp == null) return -1;

            for (int i = 8; i < layersProp.arraySize; i++) // 0~7은 예약
            {
                var element = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log($"[GUIDEON] '{layerName}' 레이어를 index {i}에 생성했습니다.");
                    return i;
                }
            }

            Debug.LogWarning($"[GUIDEON] 레이어 슬롯이 가득 차 '{layerName}' 레이어를 생성할 수 없습니다. 수동으로 추가해 주세요.");
            return -1;
        }

        private static GameObject GetOrCreate(string name)
        {
            var existing = GameObject.Find(name);
            return existing != null ? existing : new GameObject(name);
        }

        private static GameObject GetOrCreateChild(GameObject parent, string childName)
        {
            var existing = parent.transform.Find(childName);
            if (existing != null) return existing.gameObject;

            var go = new GameObject(childName);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }
    }
}
#endif
