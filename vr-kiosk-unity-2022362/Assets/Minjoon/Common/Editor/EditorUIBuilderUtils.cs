#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Common.Editor
{
    /// <summary>
    /// 3개 UI 빌더(Phase2, MainMenu, DifficultySelect)가 공유하는 에디터 유틸리티.
    /// DRY 원칙: MakePanel, MakeLabel, MakeButton 등 중복 팩토리 메서드를 통합.
    /// </summary>
    public static class EditorUIBuilderUtils
    {
        // ═══════════════════════════════════════════════════════════════════
        //  EventSystem
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>씬에 EventSystem이 없으면 생성합니다.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Panel / Container
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>CanvasGroup 기반 패널을 생성합니다.</summary>
        public static GameObject MakePanel(Transform parent, string name, bool visible, Color bg)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = bg;
            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible; cg.blocksRaycasts = visible;
            return go;
        }

        /// <summary>레이아웃 컨테이너를 생성합니다 (Image 없음).</summary>
        public static GameObject MakeContainer(GameObject parent, string name,
            Vector2 ancMin, Vector2 ancMax)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Label
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>TextMeshProUGUI 레이블을 생성합니다.</summary>
        public static GameObject MakeLabel(GameObject parent, string name, string text,
            float size, Vector2 ancMin, Vector2 ancMax, Color color, TMP_FontAsset font = null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax;
            rt.offsetMin = new Vector2(10, 0); rt.offsetMax = new Vector2(-10, 0);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            if (font != null) tmp.font = font;
            return go;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Button (2가지 스타일)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 배경색이 있는 버튼 (Phase2 키오스크 스타일).
        /// 흰색 텍스트, 선택적 ClickFeedbackHandler 부착.
        /// </summary>
        public static GameObject MakeColoredButton(GameObject parent, string name, string label,
            float fontSize, Color bgColor, Vector2 ancMin, Vector2 ancMax,
            TMP_FontAsset font = null, bool addClickFeedback = false)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            if (addClickFeedback)
                go.AddComponent<Phase2.Core.ClickFeedbackHandler>();

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = fontSize;
            tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
            if (font != null) tmp.font = font;
            return go;
        }

        /// <summary>
        /// 투명 히트박스 + 텍스트 색상 호버 버튼 (메인메뉴/난이도 스타일).
        /// 호버 시 주황색으로 텍스트 색상이 변합니다.
        /// </summary>
        public static GameObject MakeHoverTextButton(GameObject parent, string name, string label,
            float fontSize, Vector2 ancMin, Vector2 ancMax, TMP_FontAsset font = null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // 투명색 명중 영역 (XR 및 GraphicRaycaster 판정용)
            var img = go.AddComponent<Image>();
            img.color = new Color(0, 0, 0, 0);

            var btn = go.AddComponent<Button>();

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;

            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            if (font != null) tmp.font = font;

            // 텍스트 호버 스타일링 (호버시 주황색 강조)
            btn.targetGraphic = tmp;
            var cb = btn.colors;
            cb.normalColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            cb.highlightedColor = new Color(0.90f, 0.42f, 0.22f, 1f);
            cb.pressedColor = new Color(1f, 0.55f, 0.20f, 1f);
            cb.selectedColor = new Color(0.15f, 0.15f, 0.18f, 1f);
            cb.disabledColor = new Color(0.45f, 0.45f, 0.50f, 0.6f);
            cb.colorMultiplier = 1f;
            btn.colors = cb;

            return go;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Wiring Helpers
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>SerializedObject를 통해 단일 필드에 Object 참조를 연결합니다.</summary>
        public static void WireField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// SerializedObject 프로퍼티에 Transform에서 추출한 컴포넌트를 자동 연결합니다.
        /// Button, TextMeshProUGUI, CanvasGroup, Transform 타입을 자동 판별합니다.
        /// </summary>
        public static void WireProp(SerializedObject so, string field, Transform found)
        {
            if (found == null) return;
            var prop = so.FindProperty(field);
            if (prop == null) return;

            string typeName = prop.type;
            if (typeName.Contains("Button"))
                prop.objectReferenceValue = found.GetComponent<Button>();
            else if (typeName.Contains("TextMeshProUGUI"))
                prop.objectReferenceValue = found.GetComponent<TextMeshProUGUI>();
            else if (typeName.Contains("CanvasGroup"))
                prop.objectReferenceValue = found.GetComponent<CanvasGroup>();
            else
                prop.objectReferenceValue = found;
        }

        /// <summary>Transform.Find 래퍼.</summary>
        public static Transform Find(Transform root, string path)
        {
            return root.Find(path);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Sprite Loading
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// PNG/JPG 이미지를 Sprite로 로드합니다.
        /// TextureImporter 설정이 Sprite가 아니면 자동 변환합니다.
        /// </summary>
        public static Sprite LoadSpriteFromPath(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
#endif
