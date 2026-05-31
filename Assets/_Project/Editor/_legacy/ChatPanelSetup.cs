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
    /// Tools > GUIDEON > Setup Chat Panel
    /// 기존 ChatPanel 계층을 유지하면서 STT 마이크 입력으로 전환한다.
    ///   1. TMP_InputField / 전송 Button 제거
    ///   2. AudioWaveformWidget이 없으면 하단에 생성, 있으면 그대로 활용
    ///   3. MicButton 생성 (없으면)
    ///   4. SttManager GO 씬에 추가 (없으면)
    ///   5. ChatPanel 직렬화 필드 연결
    /// </summary>
    public static class ChatPanelSetup
    {
        [MenuItem("Tools/GUIDEON/Setup Chat Panel")]
        public static void Run()
        {
            var panel = Object.FindObjectOfType<ChatPanel>(true);
            if (panel == null)
            {
                EditorUtility.DisplayDialog("오류", "씬에서 ChatPanel 컴포넌트를 찾을 수 없습니다.", "확인");
                return;
            }
            var chatGo = panel.gameObject;
            Undo.RegisterFullObjectHierarchyUndo(chatGo, "Setup Chat Panel (STT)");

            // ── 1. 기존 InputField / SendButton 제거 ──────────────────
            RemoveComponentOwner<TMP_InputField>(chatGo);
            // InputField 없어진 뒤 남은 고아 Send 버튼도 정리
            // (이름 기반 — 기존 코드에서 "SendButton" 또는 "InputArea" 였음)
            TryDestroyChild(chatGo, "SendButton");
            TryDestroyChild(chatGo, "InputArea");

            // ── 2. 기존 요소 탐색 ─────────────────────────────────────
            var scrollRect    = chatGo.GetComponentInChildren<ScrollRect>(true);
            var contentRoot   = scrollRect != null ? scrollRect.content : null;
            var thinkingGroup = FindChildByName(chatGo.transform, "ThinkingGroup");
            var waveWidget    = chatGo.GetComponentInChildren<AudioWaveformWidget>(true);

            // ── 3. BottomBar 찾거나 생성 ──────────────────────────────
            var bottomBarGo = FindChildByName(chatGo.transform, "BottomBar")?.gameObject;
            if (bottomBarGo == null)
            {
                bottomBarGo = new GameObject("BottomBar", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(bottomBarGo, "create BottomBar");
                bottomBarGo.transform.SetParent(chatGo.transform, false);

                var rt = bottomBarGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = new Vector2(1, 0);
                rt.pivot     = new Vector2(0.5f, 0);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0, 100);

                var hlg = bottomBarGo.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment       = TextAnchor.MiddleCenter;
                hlg.spacing              = 24;
                hlg.padding              = new RectOffset(40, 40, 12, 12);
                hlg.childControlWidth    = false;
                hlg.childControlHeight   = false;
                hlg.childForceExpandWidth  = false;
                hlg.childForceExpandHeight = true;

                // 스크롤뷰 하단 여백 확보
                if (scrollRect != null)
                {
                    var srt = scrollRect.GetComponent<RectTransform>();
                    srt.offsetMin = new Vector2(srt.offsetMin.x, 100);
                }
            }
            var bottomBarRt = bottomBarGo.GetComponent<RectTransform>();

            // ── 4. WaveformWidget ─────────────────────────────────────
            if (waveWidget == null)
            {
                var wGo = new GameObject("WaveformWidget",
                    typeof(RectTransform), typeof(AudioWaveformWidget));
                Undo.RegisterCreatedObjectUndo(wGo, "create WaveformWidget");
                wGo.transform.SetParent(bottomBarGo.transform, false);
                waveWidget = wGo.GetComponent<AudioWaveformWidget>();
                var le = wGo.AddComponent<LayoutElement>();
                le.preferredWidth  = 200;
                le.preferredHeight = 56;
            }
            else if (waveWidget.transform.parent != bottomBarRt)
            {
                // 기존 WaveformWidget이 다른 위치에 있으면 BottomBar로 이동
                Undo.SetTransformParent(waveWidget.transform, bottomBarRt, "move WaveformWidget");
                var le = waveWidget.GetComponent<LayoutElement>()
                      ?? waveWidget.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth  = 200;
                le.preferredHeight = 56;
            }

            // ── 5. MicButton ──────────────────────────────────────────
            Button micBtn   = null;
            Image  micIcon  = null;

            var existingMicBtn = FindChildByName(bottomBarRt, "MicButton");
            if (existingMicBtn != null)
            {
                micBtn  = existingMicBtn.GetComponent<Button>();
                micIcon = existingMicBtn.GetComponentInChildren<Image>(true);
            }
            else
            {
                var roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

                var micGo = new GameObject("MicButton",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                Undo.RegisterCreatedObjectUndo(micGo, "create MicButton");
                micGo.transform.SetParent(bottomBarRt, false);

                var micImg = micGo.GetComponent<Image>();
                micImg.sprite = roundedSprite;
                micImg.type   = Image.Type.Sliced;
                micImg.color  = Hex("#F1F5F9");

                var le = micGo.AddComponent<LayoutElement>();
                le.preferredWidth  = 80;
                le.preferredHeight = 80;

                micBtn = micGo.GetComponent<Button>();

                // 아이콘 Image
                var iconGo = new GameObject("MicIcon",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Undo.RegisterCreatedObjectUndo(iconGo, "create MicIcon");
                iconGo.transform.SetParent(micGo.transform, false);
                micIcon = iconGo.GetComponent<Image>();
                micIcon.color = Hex("#666666");
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = Vector2.zero;
                iconRt.anchorMax = Vector2.one;
                iconRt.offsetMin = new Vector2(20, 20);
                iconRt.offsetMax = new Vector2(-20, -20);
            }

            // ── 6. SttManager GO ──────────────────────────────────────
            var sttGo = GameObject.Find("SttManager")
                     ?? Object.FindObjectOfType<SttManager>(true)?.gameObject;
            if (sttGo == null)
            {
                sttGo = new GameObject("SttManager");
                Undo.RegisterCreatedObjectUndo(sttGo, "create SttManager");
                sttGo.AddComponent<SttManager>();
                Debug.Log("[ChatPanelSetup] SttManager 생성");
            }

            // ── 7. ChatPanel 필드 연결 ────────────────────────────────
            var so = new SerializedObject(panel);
            Ref(so, "_scrollRect",     scrollRect);
            Ref(so, "_contentRoot",    contentRoot);
            Ref(so, "_thinkingGroup",  thinkingGroup?.gameObject);
            Ref(so, "_micButton",      micBtn);
            Ref(so, "_micIcon",        micIcon);
            Ref(so, "_waveformWidget", waveWidget);
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(chatGo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(chatGo.scene);

            Debug.Log("[ChatPanelSetup] 완료! Inspector에서 User/AI BubblePrefab 확인 후 Ctrl+S");
            EditorUtility.DisplayDialog("완료",
                "ChatPanel STT 전환 완료!\n\n" +
                "Inspector → ChatPanel에서\n" +
                "User Bubble Prefab / AI Bubble Prefab이\n" +
                "연결되어 있는지 확인 후 Ctrl+S 저장하세요.",
                "확인");
        }

        // ── 헬퍼 ──────────────────────────────────────────────────────

        static void RemoveComponentOwner<T>(GameObject root) where T : Component
        {
            var comp = root.GetComponentInChildren<T>(true);
            if (comp == null) return;
            // 자기 GO를 통째로 지우면 ScrollView 같은 부모를 지울 수 있으므로
            // 컴포넌트의 직접 GO가 루트나 ScrollView가 아닌 경우에만 지움
            var go = comp.gameObject;
            if (go == root) { Undo.DestroyObjectImmediate(comp); return; }
            if (go.GetComponent<ScrollRect>() != null) return; // ScrollRect 보호
            Undo.DestroyObjectImmediate(go);
        }

        static void TryDestroyChild(GameObject root, string name)
        {
            var t = root.transform.Find(name);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }

        static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
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
