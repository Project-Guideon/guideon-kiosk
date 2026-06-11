#if UNITY_EDITOR
using Guideon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Guideon.Editor
{
    public static class UIToolkitSceneSetup
    {
        private const string UxmlRoot  = "Assets/_Project/UI/UXML/";
        private const string ThemePath = "Assets/_Project/UI/Settings/GuideonTheme.tss";

        // ── Boot 씬 ────────────────────────────────────────────────

        [MenuItem("Tools/GUIDEON/Setup Boot Scene UI")]
        public static void SetupBootScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var panelSettings = LoadOrWarnPanelSettings();
            if (panelSettings == null) return;

            // BootScreenController GameObject
            var bootGo = GetOrCreate("BootScreen");
            var bootDoc = GetOrAddComponent<UIDocument>(bootGo);
            bootDoc.panelSettings = panelSettings;
            bootDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlRoot + "BootScreen.uxml");
            GetOrAddComponent<BootScreenController>(bootGo);

            // PairingScreenController GameObject
            var pairingGo = GetOrCreate("PairingScreen");
            var pairingDoc = GetOrAddComponent<UIDocument>(pairingGo);
            pairingDoc.panelSettings = panelSettings;
            pairingDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlRoot + "PairingScreen.uxml");
            GetOrAddComponent<PairingScreenController>(pairingGo);

            // ErrorScreenController GameObject
            var errorGo = GetOrCreate("ErrorScreen");
            var errorDoc = GetOrAddComponent<UIDocument>(errorGo);
            errorDoc.panelSettings = panelSettings;
            errorDoc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlRoot + "ErrorScreen.uxml");
            GetOrAddComponent<ErrorScreenController>(errorGo);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[GUIDEON] Boot Scene UI 설정 완료. BootSceneController의 필드를 Inspector에서 연결하세요.");
        }

        // ── Main 씬 ────────────────────────────────────────────────

        [MenuItem("Tools/GUIDEON/Setup Main Scene UI")]
        public static void SetupMainScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var panelSettings = LoadOrWarnPanelSettings();
            if (panelSettings == null) return;

            // 각 스크린 생성
            CreateScreen<IdleScreenController>("IdleScreen",   "IdleScreen.uxml",   panelSettings);
            CreateScreen<ChatScreenController>("ChatScreen",   "ChatScreen.uxml",   panelSettings);
            CreateScreen<GuideScreenController>("GuideScreen", "GuideScreen.uxml",  panelSettings);
            CreateScreen<ErrorScreenController>("ErrorScreen", "ErrorScreen.uxml",  panelSettings);
            CreateScreen<AdminScreenController>("AdminScreen", "AdminScreen.uxml",  panelSettings);

            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[GUIDEON] Main Scene UI 설정 완료. MainSceneController의 _scenePanels와 _adminScreen을 Inspector에서 연결하세요.");
        }

        // ── TMP SDF 힌트 ──────────────────────────────────────────

        [MenuItem("Tools/GUIDEON/Open TMP Font Asset Creator")]
        public static void OpenTmpFontCreator()
        {
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Font Asset Creator");
            Debug.Log("[GUIDEON] Font Asset Creator를 열었습니다.\n" +
                      "Assets/_Project/Font/Noto/ 폴더의 .otf 파일을 Source Font로 지정하고\n" +
                      "4096×4096, 한글 상용 2350자 범위로 Generate하세요.");
        }

        // ── 검증 ─────────────────────────────────────────────────

        [MenuItem("Tools/GUIDEON/Validate UI Setup")]
        public static void ValidateUISetup()
        {
            var issues = new System.Collections.Generic.List<string>();

            CheckAsset<VisualTreeAsset>(UxmlRoot + "BootScreen.uxml",    issues);
            CheckAsset<VisualTreeAsset>(UxmlRoot + "PairingScreen.uxml", issues);
            CheckAsset<VisualTreeAsset>(UxmlRoot + "IdleScreen.uxml",    issues);
            CheckAsset<VisualTreeAsset>(UxmlRoot + "ChatScreen.uxml",    issues);
            CheckAsset<VisualTreeAsset>(UxmlRoot + "GuideScreen.uxml",   issues);
            CheckAsset<VisualTreeAsset>(UxmlRoot + "ErrorScreen.uxml",   issues);
            CheckAsset<VisualTreeAsset>(UxmlRoot + "AdminScreen.uxml",   issues);
            CheckAsset<StyleSheet>("Assets/_Project/UI/USS/tokens.uss",  issues);
            CheckAsset<StyleSheet>("Assets/_Project/UI/USS/common.uss",  issues);

            if (issues.Count == 0)
                Debug.Log("[GUIDEON] ✅ UI 자산 검증 완료 — 문제 없음.");
            else
                Debug.LogWarning("[GUIDEON] ⚠ 누락된 자산:\n" + string.Join("\n", issues));
        }

        // ── 헬퍼 ─────────────────────────────────────────────────

        private static void CreateScreen<T>(string goName, string uxmlFile, PanelSettings settings)
            where T : MonoBehaviour
        {
            var go  = GetOrCreate(goName);
            var doc = GetOrAddComponent<UIDocument>(go);
            doc.panelSettings    = settings;
            doc.visualTreeAsset  = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlRoot + uxmlFile);
            GetOrAddComponent<T>(go);
        }

        private static PanelSettings LoadOrWarnPanelSettings()
        {
            const string path = "Assets/_Project/UI/Settings/GuideonPanelSettings.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null)
                Debug.LogError($"[GUIDEON] PanelSettings가 없습니다: {path}\n" +
                               "Project > Create > UI Toolkit > Panel Settings로 생성 후\n" +
                               "Scale Mode = Scale With Screen Size, 1920×1080, Match 0.5 로 설정하세요.");
            return ps;
        }

        private static GameObject GetOrCreate(string name)
        {
            var found = GameObject.Find(name);
            return found != null ? found : new GameObject(name);
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static void CheckAsset<T>(string path, System.Collections.Generic.List<string> issues)
            where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
                issues.Add($"  누락: {path}");
        }
    }
}
#endif
