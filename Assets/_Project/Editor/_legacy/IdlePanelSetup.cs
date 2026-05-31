#if UNITY_EDITOR
using Guideon.Mascot;
using Guideon.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Guideon.Editor
{
    /// <summary>
    /// Tools > GUIDEON > Setup Idle Panel 을 실행하면
    /// 활성 씬의 IdlePanel 계층을 03-대기화면 디자인으로 재구성한다.
    /// 기존 자식들은 모두 제거하고 새로 생성하므로 씬 저장 전에 백업 권고.
    /// </summary>
    public static class IdlePanelSetup
    {
        // ── Asset GUIDs ──────────────────────────────────────────
        private const string GuidLogo        = "ec92966065d84539b61a141387b37fc1";
        private const string GuidThermometer = "f2b54cf1230a7654084f1a13433aaa64";
        private const string GuidWifi        = "6704b1933528a5f48b0d2f0db1a03dad";
        private const string GuidLanguage    = "e267289eb06a46b41a8a670b747d226f";
        private const string GuidFontNanum   = "d75cb7c3237d3f74d9ca5f050d485e97";
        private const string GuidFontChosun  = "449235d9139007746a9ffeb6e3da618f";

        [MenuItem("Tools/GUIDEON/Setup Idle Panel")]
        public static void Run()
        {
            var idleGo = GameObject.Find("IdlePanel");
            if (idleGo == null)
            {
                EditorUtility.DisplayDialog("오류", "씬에서 'IdlePanel' 오브젝트를 찾을 수 없습니다.", "확인");
                return;
            }
            if (!EditorUtility.DisplayDialog("확인",
                "IdlePanel의 자식을 모두 지우고 새로 만듭니다.\n씬을 저장해 두었나요?",
                "계속", "취소")) return;

            Undo.RegisterFullObjectHierarchyUndo(idleGo, "Setup Idle Panel");

            for (int i = idleGo.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(idleGo.transform.GetChild(i).gameObject);

            var root = idleGo.GetComponent<RectTransform>();
            var panel = idleGo.GetComponent<IdlePanel>();

            // ── 에셋 로드 ───────────────────────────────────────
            var logoSprite  = LoadSprite(GuidLogo);
            var thermSprite = LoadSprite(GuidThermometer);
            var wifiSprite  = LoadSprite(GuidWifi);
            var langSprite  = LoadSprite(GuidLanguage);
            var fontNanum   = LoadFont(GuidFontNanum);
            var fontChosun  = LoadFont(GuidFontChosun);
            var roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // ── 배경 ────────────────────────────────────────────
            var bg = MakeImg(root, "Background", Hex("#FFFBF7"));
            Stretch(bg);

            // ── Header (height=80) ───────────────────────────────
            var headerRt = MakePanel(root, "Header");
            headerRt.anchorMin = new Vector2(0, 1);
            headerRt.anchorMax = Vector2.one;
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.offsetMin = headerRt.offsetMax = Vector2.zero;
            headerRt.sizeDelta = new Vector2(0, 80);

            var headerHlg = headerRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerHlg.childAlignment = TextAnchor.MiddleLeft;
            headerHlg.spacing = 12;
            headerHlg.padding = new RectOffset(28, 28, 0, 0);
            headerHlg.childControlWidth = false;
            headerHlg.childControlHeight = false;
            headerHlg.childForceExpandWidth = false;
            headerHlg.childForceExpandHeight = true;

            // Logo
            var logoRt = MakeImg(headerRt, "Logo", Color.white);
            logoRt.GetComponent<Image>().sprite = logoSprite;
            logoRt.GetComponent<Image>().preserveAspect = true;
            LE(logoRt.gameObject, pw: 120, ph: 44);

            // Flex spacer
            FlexSpacer(headerRt.gameObject);

            // StatusBar ────────────────────────────────────────
            var sbRt = MakePanel(headerRt, "StatusBar");
            LE(sbRt.gameObject, pw: 340, ph: 44);
            var sbHlg = sbRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            sbHlg.childAlignment = TextAnchor.MiddleRight;
            sbHlg.spacing = 8;
            sbHlg.padding = new RectOffset(0, 0, 0, 0);
            sbHlg.childControlWidth = false;
            sbHlg.childControlHeight = false;
            sbHlg.childForceExpandWidth = false;
            sbHlg.childForceExpandHeight = true;

            var clockTmp = MakeTMP(sbRt, "ClockText", "09:00", 22, fontNanum);
            clockTmp.fontStyle = FontStyles.Bold;
            clockTmp.color = Hex("#1E293B");
            clockTmp.alignment = TextAlignmentOptions.Center;
            LE(clockTmp.gameObject, pw: 64);

            MakeDiv(sbRt.gameObject, 1, 28, Hex("#E2E8F0"));

            var opsRt = MakeImg(sbRt, "ThermometerIcon", Color.white);
            opsRt.GetComponent<Image>().sprite = thermSprite;
            LE(opsRt.gameObject, pw: 18, ph: 18);

            var tempTmp = MakeTMP(sbRt, "TemperatureText", "22°C", 15, fontNanum);
            tempTmp.color = Hex("#64748B");
            LE(tempTmp.gameObject, pw: 44);

            MakeDiv(sbRt.gameObject, 1, 28, Hex("#E2E8F0"));

            var wifiRt = MakeImg(sbRt, "WifiIcon", Color.white);
            wifiRt.GetComponent<Image>().sprite = wifiSprite;
            LE(wifiRt.gameObject, pw: 20, ph: 20);

            MakeDiv(sbRt.gameObject, 1, 28, Hex("#E2E8F0"));

            var opsTxt = MakeTMP(sbRt, "OperatingHoursText", "09:00-18:00", 14, fontNanum);
            opsTxt.color = Hex("#64748B");
            LE(opsTxt.gameObject, pw: 90);

            MakeDiv(sbRt.gameObject, 1, 28, Hex("#E2E8F0"));

            // Language button
            var langGo = new GameObject("LanguageButton", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(langGo, "create");
            langGo.transform.SetParent(sbRt, false);
            var langBtn = langGo.AddComponent<Button>();
            LE(langGo, pw: 36, ph: 36);
            var langImgRt = MakeImg(langGo.GetComponent<RectTransform>(), "LanguageIcon", Color.white);
            langImgRt.GetComponent<Image>().sprite = langSprite;
            Stretch(langImgRt, 6);

            // ── Main (3 columns below header) ───────────────────
            var mainRt = MakePanel(root, "MainContent");
            mainRt.anchorMin = Vector2.zero;
            mainRt.anchorMax = Vector2.one;
            mainRt.offsetMin = Vector2.zero;
            mainRt.offsetMax = new Vector2(0, -80);

            var mainHlg = mainRt.gameObject.AddComponent<HorizontalLayoutGroup>();
            mainHlg.childAlignment = TextAnchor.MiddleCenter;
            mainHlg.spacing = 0;
            mainHlg.padding = new RectOffset(40, 40, 32, 32);
            mainHlg.childControlWidth = true;
            mainHlg.childControlHeight = true;
            mainHlg.childForceExpandWidth = true;
            mainHlg.childForceExpandHeight = true;

            // ── 왼쪽: Welcome (45%) ──────────────────────────────
            var wSecRt = MakePanel(mainRt, "WelcomeSection");
            LE(wSecRt.gameObject, fw: 45);

            var wGroupRt = MakePanel(wSecRt, "WelcomeGroup");
            wGroupRt.anchorMin = new Vector2(0, 0.5f);
            wGroupRt.anchorMax = new Vector2(1, 0.5f);
            wGroupRt.pivot = new Vector2(0.5f, 0.5f);
            wGroupRt.anchoredPosition = Vector2.zero;
            var wGroup = wGroupRt.gameObject.AddComponent<CanvasGroup>();
            var wVlg = wGroupRt.gameObject.AddComponent<VerticalLayoutGroup>();
            wVlg.childAlignment = TextAnchor.MiddleLeft;
            wVlg.spacing = 12;
            wVlg.childControlWidth = true;
            wVlg.childControlHeight = false;
            wVlg.childForceExpandWidth = true;
            wVlg.childForceExpandHeight = false;
            var wCsf = wGroupRt.gameObject.AddComponent<ContentSizeFitter>();
            wCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var siteNameTmp = MakeTMP(wGroupRt, "SiteNameText", "경주 월성", 20, fontNanum);
            siteNameTmp.color = Hex("#FF6B2C");
            siteNameTmp.fontStyle = FontStyles.Bold;

            var koreanTmp = MakeTMP(wGroupRt, "WelcomeKoreanText",
                "어서오세요!\n반갑습니다.", 58, fontChosun);
            koreanTmp.color = Hex("#1E293B");
            koreanTmp.fontStyle = FontStyles.Bold;
            koreanTmp.lineSpacingAdjustment = 4;

            var subTmp = MakeTMP(wGroupRt, "WelcomeSubtitleText",
                "Welcome  •  欢迎光临  •  ようこそ", 18, fontNanum);
            subTmp.color = Hex("#64748B");

            // ── 가운데: Mascot (30%) ─────────────────────────────
            var mascotSecRt = MakePanel(mainRt, "MascotArea");
            LE(mascotSecRt.gameObject, fw: 30);

            var mascotLoaderGo = new GameObject("MascotLoader", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(mascotLoaderGo, "create");
            mascotLoaderGo.transform.SetParent(mascotSecRt, false);
            var mascotLoaderComp = mascotLoaderGo.AddComponent<MascotLoader>();
            Stretch(mascotLoaderGo.GetComponent<RectTransform>());

            // placeholder circle
            var phRt = MakeImg(mascotSecRt, "MascotPlaceholder", Hex("#E2E8F080"));
            phRt.GetComponent<Image>().sprite = roundedSprite;
            phRt.GetComponent<Image>().type = Image.Type.Sliced;
            phRt.anchorMin = phRt.anchorMax = phRt.pivot = new Vector2(0.5f, 0.5f);
            phRt.sizeDelta = new Vector2(200, 260);
            phRt.anchoredPosition = Vector2.zero;

            // ── 오른쪽: TouchCard (25%) ──────────────────────────
            var tcSecRt = MakePanel(mainRt, "TouchCardSection");
            LE(tcSecRt.gameObject, fw: 25);

            var tcRt = MakePanel(tcSecRt, "TouchCard");
            tcRt.anchorMin = tcRt.anchorMax = tcRt.pivot = new Vector2(0.5f, 0.5f);
            tcRt.sizeDelta = new Vector2(220, 300);
            tcRt.anchoredPosition = Vector2.zero;
            var tcGroup = tcRt.gameObject.AddComponent<CanvasGroup>();

            // Card shadow/bg
            var cardBgRt = MakeImg(tcRt, "CardBg", Color.white);
            cardBgRt.GetComponent<Image>().sprite = roundedSprite;
            cardBgRt.GetComponent<Image>().type = Image.Type.Sliced;
            Stretch(cardBgRt);

            // Card content
            var ccRt = MakePanel(tcRt, "CardContent");
            Stretch(ccRt, 20);
            var ccVlg = ccRt.gameObject.AddComponent<VerticalLayoutGroup>();
            ccVlg.childAlignment = TextAnchor.MiddleCenter;
            ccVlg.spacing = 14;
            ccVlg.childControlWidth = true;
            ccVlg.childControlHeight = false;
            ccVlg.childForceExpandWidth = true;
            ccVlg.childForceExpandHeight = false;

            // Touch icon placeholder
            var tcIconRt = MakeImg(ccRt, "TouchIcon", Hex("#FF6B2C40"));
            tcIconRt.GetComponent<Image>().sprite = roundedSprite;
            tcIconRt.GetComponent<Image>().type = Image.Type.Sliced;
            LE(tcIconRt.gameObject, ph: 64);

            var tcMainTmp = MakeTMP(ccRt, "TouchMainText", "화면을\n터치하세요", 26, fontNanum);
            tcMainTmp.color = Hex("#1E293B");
            tcMainTmp.fontStyle = FontStyles.Bold;
            tcMainTmp.alignment = TextAlignmentOptions.Center;

            var tcSubTmp = MakeTMP(ccRt, "TouchSubText", "Touch to Start", 15, fontNanum);
            tcSubTmp.color = Hex("#64748B");
            tcSubTmp.alignment = TextAlignmentOptions.Center;

            // ── 직렬 연결 ────────────────────────────────────────
            if (panel != null)
            {
                var so = new SerializedObject(panel);
                Ref(so, "_logoImage",           logoRt.GetComponent<Image>());
                Ref(so, "_operatingHoursText",  opsTxt);
                Ref(so, "_temperatureText",     tempTmp);
                Ref(so, "_thermometerIcon",     opsRt.GetComponent<Image>());
                Ref(so, "_wifiIcon",            wifiRt.GetComponent<Image>());
                Ref(so, "_languageButton",      langBtn);
                Ref(so, "_languageIcon",        langImgRt.GetComponent<Image>());
                Ref(so, "_welcomeGroup",        wGroup);
                Ref(so, "_welcomeKoreanText",   koreanTmp);
                Ref(so, "_welcomeSubtitleText", subTmp);
                Ref(so, "_siteNameText",        siteNameTmp);
                Ref(so, "_mascotLoader",        mascotLoaderComp);
                Ref(so, "_mascotArea",          mascotSecRt);
                Ref(so, "_touchCardGroup",      tcGroup);
                Ref(so, "_touchCardRect",       tcRt);
                Ref(so, "_touchMainText",       tcMainTmp);
                Ref(so, "_touchSubText",        tcSubTmp);
                Ref(so, "_touchIcon",           tcIconRt.GetComponent<Image>());
                Ref(so, "_clockText",           clockTmp);
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(idleGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(idleGo.scene);
            Debug.Log("[IdlePanelSetup] 완료! Ctrl+S로 씬 저장하세요.");
            EditorUtility.DisplayDialog("완료", "IdlePanel 재구성 완료!\nCtrl+S로 씬을 저장하세요.", "확인");
        }

        // ── Factories ─────────────────────────────────────────────

        static RectTransform MakePanel(RectTransform p, string n)
        {
            var go = new GameObject(n, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "cp");
            go.transform.SetParent(p, false);
            return go.GetComponent<RectTransform>();
        }

        static RectTransform MakeImg(RectTransform p, string n, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "ci");
            go.transform.SetParent(p, false);
            go.GetComponent<Image>().color = c;
            return go.GetComponent<RectTransform>();
        }

        static TextMeshProUGUI MakeTMP(RectTransform p, string n,
            string text, float size, TMP_FontAsset font)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "ct");
            go.transform.SetParent(p, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.enableWordWrapping = true;
            if (font != null) tmp.font = font;
            return tmp;
        }

        static void MakeDiv(GameObject parent, float w, float h, Color c)
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(go, "cd");
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Image>().color = c;
            LE(go, pw: w, ph: h);
        }

        static void FlexSpacer(GameObject parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            Undo.RegisterCreatedObjectUndo(go, "cs");
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1;
        }

        // ── Layout helpers ────────────────────────────────────────

        static void LE(GameObject go, float pw = -1, float ph = -1, float fw = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (pw >= 0) le.preferredWidth = pw;
            if (ph >= 0) le.preferredHeight = ph;
            if (fw >= 0) le.flexibleWidth = fw;
        }

        static void Stretch(RectTransform rt, float pad = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        // ── Assets ────────────────────────────────────────────────

        static Sprite LoadSprite(string guid)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(p);
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
            else Debug.LogWarning($"[IdlePanelSetup] 필드 없음: {field}");
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
#endif
