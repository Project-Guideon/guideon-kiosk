#if UNITY_EDITOR
using Guideon.Network.Stt;
using Guideon.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Guideon.Editor
{
    /// <summary>
    /// Tools > GUIDEON > Setup Chat Panel 실행 시:
    ///   1. ChatPanel 계층을 음성 입력 UI로 재구성 (버블 스크롤 + 마이크 버튼 + 파형 위젯)
    ///   2. 씬에 SttManager GameObject 생성 (없으면)
    /// 기존 자식들은 모두 제거 후 재생성하므로 씬 저장 후 실행 권고.
    /// </summary>
    public static class ChatPanelSetup
    {
        private const string GuidFontNanum = "d75cb7c3237d3f74d9ca5f050d485e97";

        [MenuItem("Tools/GUIDEON/Setup Chat Panel")]
        public static void Run()
        {
            var chatGo = GameObject.Find("ChatPanel");
            if (chatGo == null)
            {
                EditorUtility.DisplayDialog("오류", "씬에서 'ChatPanel' 오브젝트를 찾을 수 없습니다.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("확인",
                "ChatPanel의 자식을 모두 지우고 새로 만듭니다.\n씬을 저장해 두었나요?",
                "계속", "취소")) return;

            Undo.RegisterFullObjectHierarchyUndo(chatGo, "Setup Chat Panel");

            for (int i = chatGo.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(chatGo.transform.GetChild(i).gameObject);

            var root = chatGo.GetComponent<RectTransform>();
            var panel = chatGo.GetComponent<ChatPanel>();
            var fontNanum = LoadFont(GuidFontNanum);
            var roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // ── 배경 ────────────────────────────────────────────────
            var bg = MakeImg(root, "Background", Hex("#FFFBF7"));
            Stretch(bg);

            // ── 버블 스크롤 뷰 (하단 바 높이 제외 전체) ─────────────
            var scrollGo = new GameObject("BubbleScrollView",
                typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            Undo.RegisterCreatedObjectUndo(scrollGo, "create");
            scrollGo.transform.SetParent(root, false);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = Color.clear;

            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0, 120);  // 하단 바 120px
            scrollRt.offsetMax = Vector2.zero;

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.scrollSensitivity = 30;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            // Viewport
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            Undo.RegisterCreatedObjectUndo(vpGo, "create");
            vpGo.transform.SetParent(scrollGo.transform, false);
            var vpImg = vpGo.GetComponent<Image>();
            vpImg.color = Color.white;
            vpImg.sprite = roundedSprite;
            vpImg.type = Image.Type.Sliced;
            var vpMask = vpGo.GetComponent<Mask>();
            vpMask.showMaskGraphic = false;
            var vpRt = vpGo.GetComponent<RectTransform>();
            Stretch(vpRt);
            scrollRect.viewport = vpRt;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(contentGo, "create");
            contentGo.transform.SetParent(vpGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = Vector2.one;
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = contentRt.offsetMax = Vector2.zero;

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.spacing = 12;
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRt;

            // ── 생각 중 인디케이터 (스크롤 뷰 위, 하단 바 위) ───────
            var thinkGo = new GameObject("ThinkingGroup", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(thinkGo, "create");
            thinkGo.transform.SetParent(root, false);
            var thinkRt = thinkGo.GetComponent<RectTransform>();
            thinkRt.anchorMin = new Vector2(0, 0);
            thinkRt.anchorMax = new Vector2(1, 0);
            thinkRt.pivot = new Vector2(0.5f, 0);
            thinkRt.anchoredPosition = new Vector2(0, 120);
            thinkRt.sizeDelta = new Vector2(0, 40);
            thinkGo.SetActive(false);

            var thinkHlg = thinkGo.AddComponent<HorizontalLayoutGroup>();
            thinkHlg.childAlignment = TextAnchor.MiddleCenter;
            thinkHlg.spacing = 8;
            thinkHlg.padding = new RectOffset(20, 20, 4, 4);
            thinkHlg.childControlWidth = false;
            thinkHlg.childControlHeight = false;
            thinkHlg.childForceExpandWidth = false;
            thinkHlg.childForceExpandHeight = true;

            var thinkTmpGo = new GameObject("ThinkingText", typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(thinkTmpGo, "create");
            thinkTmpGo.transform.SetParent(thinkGo.transform, false);
            var thinkTmp = thinkTmpGo.GetComponent<TextMeshProUGUI>();
            thinkTmp.text = "생각 중...";
            thinkTmp.fontSize = 16;
            thinkTmp.color = Hex("#94A3B8");
            if (fontNanum != null) thinkTmp.font = fontNanum;
            var thinkLE = thinkTmpGo.AddComponent<LayoutElement>();
            thinkLE.preferredWidth = 120;

            // ── 하단 바 (높이 120) ───────────────────────────────────
            var bottomBarGo = new GameObject("BottomBar", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(bottomBarGo, "create");
            bottomBarGo.transform.SetParent(root, false);
            var bottomRt = bottomBarGo.GetComponent<RectTransform>();
            bottomRt.anchorMin = Vector2.zero;
            bottomRt.anchorMax = new Vector2(1, 0);
            bottomRt.pivot = new Vector2(0.5f, 0);
            bottomRt.anchoredPosition = Vector2.zero;
            bottomRt.sizeDelta = new Vector2(0, 120);

            var bottomHlg = bottomBarGo.AddComponent<HorizontalLayoutGroup>();
            bottomHlg.childAlignment = TextAnchor.MiddleCenter;
            bottomHlg.spacing = 20;
            bottomHlg.padding = new RectOffset(40, 40, 16, 16);
            bottomHlg.childControlWidth = false;
            bottomHlg.childControlHeight = false;
            bottomHlg.childForceExpandWidth = false;
            bottomHlg.childForceExpandHeight = true;

            // 파형 위젯
            var waveGo = new GameObject("WaveformWidget",
                typeof(RectTransform), typeof(AudioWaveformWidget));
            Undo.RegisterCreatedObjectUndo(waveGo, "create");
            waveGo.transform.SetParent(bottomBarGo.transform, false);
            var waveWidget = waveGo.GetComponent<AudioWaveformWidget>();
            var waveLE = waveGo.AddComponent<LayoutElement>();
            waveLE.preferredWidth = 200;
            waveLE.preferredHeight = 56;

            // 마이크 버튼
            var micBtnGo = new GameObject("MicButton",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(micBtnGo, "create");
            micBtnGo.transform.SetParent(bottomBarGo.transform, false);
            var micBtnImg = micBtnGo.GetComponent<Image>();
            micBtnImg.sprite = roundedSprite;
            micBtnImg.type = Image.Type.Sliced;
            micBtnImg.color = Hex("#F1F5F9");
            var micBtnLE = micBtnGo.AddComponent<LayoutElement>();
            micBtnLE.preferredWidth = 80;
            micBtnLE.preferredHeight = 80;

            // 마이크 아이콘 Image (버튼 위에 오버레이)
            var micIconGo = new GameObject("MicIcon",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(micIconGo, "create");
            micIconGo.transform.SetParent(micBtnGo.transform, false);
            var micIconImg = micIconGo.GetComponent<Image>();
            micIconImg.color = Hex("#666666");
            var micIconRt = micIconGo.GetComponent<RectTransform>();
            Stretch(micIconRt, 20);

            var micBtn = micBtnGo.GetComponent<Button>();

            // ── SttManager GO 생성 (없으면) ─────────────────────────
            var sttGo = GameObject.Find("SttManager");
            if (sttGo == null)
            {
                sttGo = new GameObject("SttManager");
                Undo.RegisterCreatedObjectUndo(sttGo, "create SttManager");
                // SttManager의 OnInitialize가 Awake에서 실행되므로 여기선 AddComponent만
                sttGo.AddComponent<SttManager>();
                Debug.Log("[ChatPanelSetup] SttManager GameObject 생성 완료");
            }
            else
            {
                Debug.Log("[ChatPanelSetup] 기존 SttManager 사용");
            }

            // ── ChatPanel 필드 직렬 연결 ────────────────────────────
            if (panel != null)
            {
                var so = new SerializedObject(panel);
                Ref(so, "_scrollRect",    scrollRect);
                Ref(so, "_contentRoot",   contentRt);
                Ref(so, "_thinkingGroup", thinkGo);
                Ref(so, "_micButton",     micBtn);
                Ref(so, "_micIcon",       micIconImg);
                Ref(so, "_waveformWidget", waveWidget);
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(chatGo);
            EditorUtility.SetDirty(sttGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(chatGo.scene);
            Debug.Log("[ChatPanelSetup] 완료! 버블 Prefab을 Inspector에서 수동 연결 후 Ctrl+S로 씬 저장하세요.");
            EditorUtility.DisplayDialog("완료",
                "ChatPanel 재구성 완료!\n\n" +
                "Inspector에서 ChatPanel의\n" +
                "User Bubble Prefab / AI Bubble Prefab을\n수동으로 연결한 뒤 Ctrl+S로 저장하세요.",
                "확인");
        }

        // ── Factories ──────────────────────────────────────────────

        static RectTransform MakeImg(RectTransform p, string n, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "ci");
            go.transform.SetParent(p, false);
            go.GetComponent<Image>().color = c;
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        static TMP_FontAsset LoadFont(string guid)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
        }

        static void Ref(SerializedObject so, string field, Object val)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = val;
            else Debug.LogWarning($"[ChatPanelSetup] 필드 없음: {field}");
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
#endif
