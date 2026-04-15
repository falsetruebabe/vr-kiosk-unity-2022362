#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Common;
using Common.Editor;
using U = Common.Editor.EditorUIBuilderUtils;

namespace Common.Editor
{
    /// <summary>
    /// 난이도 선택 씬(Scene_DifficultySelect)의 World Space UI를 자동 생성하는 에디터 빌더.
    /// Tools > Generate Difficulty Select 메뉴로 실행.
    /// </summary>
    public static class DifficultySelectUIBuilder
    {
        private const float CANVAS_SCALE = 0.003f;
        private const int CANVAS_W = 1080;
        private const int CANVAS_H = 1920;

        private const string FONT_ASSET_PATH = "Assets/Font/GmarketSansTTFBold SDF.asset";
        private const string KIOSK_IMAGE_PATH = "Assets/IMG/Main_Scene_Kiosk 1.png";
        private static TMP_FontAsset _cachedFont;

        private static readonly Vector3 PLAYER_POS = new Vector3(0f, 1.5f, -2.5f);
        private static readonly Vector3 CANVAS_POS = new Vector3(0f, 1.3f, 0.7f);

        [MenuItem("Tools/Generate Difficulty Select")]
        public static void Generate()
        {
            Undo.SetCurrentGroupName("Generate Difficulty Select");
            int ug = Undo.GetCurrentGroup();

            // Load custom font
            _cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
            if (_cachedFont == null)
                Debug.LogWarning($"[DifficultySelectUIBuilder] Font not found at {FONT_ASSET_PATH}. Using TMP default.");

            U.EnsureEventSystem();
            Camera cam = BuildCamera();
            Canvas canvas = BuildCanvas(cam);

            // ── 키오스크 프레임 이미지 (배경) ──
            var kioskSprite = U.LoadSpriteFromPath(KIOSK_IMAGE_PATH);
            if (kioskSprite != null)
            {
                var frameGo = new GameObject("KioskFrameImage");
                Undo.RegisterCreatedObjectUndo(frameGo, "KioskFrameImage");
                frameGo.transform.SetParent(canvas.transform, false);
                var frt = frameGo.AddComponent<RectTransform>();
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = frt.offsetMax = Vector2.zero;
                var fimg = frameGo.AddComponent<Image>();
                fimg.sprite = kioskSprite;
                fimg.raycastTarget = false;
            }

            // 배경 패널 (투명 - 모든 UI의 부모)
            var bg = U.MakePanel(canvas.transform, "BackgroundPanel", true, new Color(0f, 0f, 0f, 0f));
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.offsetMin = new Vector2(0, -100);
            bgRt.offsetMax = new Vector2(0, -100);

            // ── 상단 4/6 영역에 UI 배치 (Y: 0.333 ~ 1.0) ──

            // 타이틀
            U.MakeLabel(bg, "TitleLabel", "난이도 선택", 90,
                new Vector2(0.05f, 0.87f), new Vector2(0.95f, 0.96f), new Color(0.15f, 0.10f, 0.08f), _cachedFont);

            // 부제
            U.MakeLabel(bg, "SubtitleLabel", "원하시는 훈련 단계를 선택해주세요", 40,
                new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.87f),
                new Color(0.30f, 0.30f, 0.35f), _cachedFont);

            // 1단계
            U.MakeHoverTextButton(bg, "Phase1Button", "1단계 (쉬움)", 60,
                new Vector2(0.10f, 0.64f), new Vector2(0.90f, 0.75f), _cachedFont);

            // 2단계
            U.MakeHoverTextButton(bg, "Phase2Button", "2단계 (보통)", 60,
                new Vector2(0.10f, 0.51f), new Vector2(0.90f, 0.62f), _cachedFont);

            // 3단계
            U.MakeHoverTextButton(bg, "Phase3Button", "3단계 (어려움)", 60,
                new Vector2(0.10f, 0.38f), new Vector2(0.90f, 0.49f), _cachedFont);

            // 뒤로가기 버튼 (상단 4/6 영역 최하단)
            U.MakeHoverTextButton(bg, "BackButton", "← 뒤로 가기", 40,
                new Vector2(0.20f, 0.34f), new Vector2(0.80f, 0.37f), _cachedFont);

            // GameManager
            var gm = new GameObject("─── DifficultyManager ───");
            Undo.RegisterCreatedObjectUndo(gm, "DifficultyManager");
            var controller = gm.AddComponent<DifficultySelectController>();

            // Wire references
            var so = new SerializedObject(controller);
            U.WireProp(so, "phase1Button", U.Find(canvas.transform, "BackgroundPanel/Phase1Button"));
            U.WireProp(so, "phase2Button", U.Find(canvas.transform, "BackgroundPanel/Phase2Button"));
            U.WireProp(so, "phase3Button", U.Find(canvas.transform, "BackgroundPanel/Phase3Button"));
            U.WireProp(so, "backButton", U.Find(canvas.transform, "BackgroundPanel/BackButton"));
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.CollapseUndoOperations(ug);
            Debug.Log("[DifficultySelectUIBuilder] ✅ 난이도 선택 화면 생성 완료!");
            EditorUtility.DisplayDialog("Difficulty Select Builder",
                "난이도 선택 화면 생성 완료!\n\n▶ Play 버튼을 눌러 테스트해보세요.", "확인");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Builders
        // ═══════════════════════════════════════════════════════════════════
        private static Camera BuildCamera()
        {
            Camera cam = Camera.main;
            GameObject go;
            if (cam != null) { go = cam.gameObject; go.name = "MainCamera"; }
            else
            {
                go = new GameObject("MainCamera");
                Undo.RegisterCreatedObjectUndo(go, "Camera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            go.transform.position = PLAYER_POS;
            go.transform.rotation = Quaternion.identity;
            if (!go.GetComponent<PhysicsRaycaster>())
                go.AddComponent<PhysicsRaycaster>();
            if (!go.GetComponent<Phase2.Desktop.DesktopFPSController>())
                go.AddComponent<Phase2.Desktop.DesktopFPSController>(); // 추가: 난이도 화면에서도 FPS 컨트롤러 작동
            if (!go.GetComponent<AudioListener>())
                go.AddComponent<AudioListener>();
            return cam;
        }

        private static Canvas BuildCanvas(Camera cam)
        {
            var go = new GameObject("DifficultyCanvas [WorldSpace]");
            Undo.RegisterCreatedObjectUndo(go, "DifficultyCanvas");
            go.transform.position = CANVAS_POS;
            go.transform.localScale = Vector3.one * CANVAS_SCALE;

            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.worldCamera = cam;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(CANVAS_W, CANVAS_H);
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<CanvasScaler>();

            // 배경 Image 컴포넌트 제거 (투명 캔버스 유지)
            return c;
        }

        [MenuItem("Tools/Generate Difficulty Select", true)]
        private static bool Validate() => true;
    }
}
#endif
