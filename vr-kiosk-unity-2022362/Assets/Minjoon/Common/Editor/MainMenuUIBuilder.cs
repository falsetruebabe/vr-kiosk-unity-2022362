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
    /// 메인화면 씬(Scene_Main)의 World Space UI를 자동 생성하는 에디터 빌더.
    /// Tools > Generate Main Menu 메뉴로 실행.
    /// </summary>
    public static class MainMenuUIBuilder
    {
        private const float CANVAS_SCALE = 0.003f;
        private const int CANVAS_W = 1080;
        private const int CANVAS_H = 1920;

        private const string FONT_ASSET_PATH = "Assets/Font/GmarketSansTTFBold SDF.asset";
        private const string KIOSK_IMAGE_PATH = "Assets/IMG/Main_Scene_Kiosk 1.png";
        private static TMP_FontAsset _cachedFont;

        private static readonly Vector3 PLAYER_POS = new Vector3(0f, 1.5f, -2.5f);
        private static readonly Vector3 CANVAS_POS = new Vector3(0f, 1.3f, 0.7f);

        [MenuItem("Tools/Generate Main Menu")]
        public static void Generate()
        {
            Undo.SetCurrentGroupName("Generate Main Menu");
            int ug = Undo.GetCurrentGroup();

            // Load custom font
            _cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
            if (_cachedFont == null)
                Debug.LogWarning($"[MainMenuUIBuilder] Font not found at {FONT_ASSET_PATH}. Using TMP default.");

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
            U.MakeLabel(bg, "TitleLabel", "카페 키오스크\n가상훈련소", 90,
                new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f), new Color(0.15f, 0.10f, 0.08f), _cachedFont);

            // 부제
            U.MakeLabel(bg, "SubtitleLabel", "노인 교육용 VR 키오스크 시뮬레이터", 40,
                new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.82f),
                new Color(0.30f, 0.30f, 0.35f), _cachedFont);

            // 시작하기 버튼
            U.MakeHoverTextButton(bg, "StartButton", "시작하기", 70,
                new Vector2(0.15f, 0.55f), new Vector2(0.85f, 0.68f), _cachedFont);

            // 종료하기 버튼
            U.MakeHoverTextButton(bg, "QuitButton", "종료하기", 70,
                new Vector2(0.20f, 0.40f), new Vector2(0.80f, 0.52f), _cachedFont);

            // 종료 확인 팝업 (상단 4/6 영역 내)
            BuildConfirmPopup(canvas.transform);

            // GameManager
            var gm = new GameObject("─── MainMenuManager ───");
            Undo.RegisterCreatedObjectUndo(gm, "MainMenuManager");
            var controller = gm.AddComponent<MainMenuController>();

            // Wire references
            var so = new SerializedObject(controller);
            U.WireProp(so, "startButton", U.Find(canvas.transform, "BackgroundPanel/StartButton"));
            U.WireProp(so, "quitButton", U.Find(canvas.transform, "BackgroundPanel/QuitButton"));

            // Confirm Popup
            U.WireProp(so, "confirmPopupGroup", U.Find(canvas.transform, "ConfirmPopupPanel"));
            U.WireProp(so, "confirmYesButton", U.Find(canvas.transform, "ConfirmPopupPanel/Box/ConfirmYesButton"));
            U.WireProp(so, "confirmNoButton", U.Find(canvas.transform, "ConfirmPopupPanel/Box/ConfirmNoButton"));

            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.CollapseUndoOperations(ug);
            Debug.Log("[MainMenuUIBuilder] ✅ 메인화면 생성 완료!");
            EditorUtility.DisplayDialog("Main Menu Builder",
                "메인화면 생성 완료!\n\n▶ Play 버튼을 눌러 테스트해보세요.", "확인");
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
                go.AddComponent<Phase2.Desktop.DesktopFPSController>(); // 추가: 메인 화면에서도 FPS 컨트롤러 작동
            if (!go.GetComponent<AudioListener>())
                go.AddComponent<AudioListener>();
            return cam;
        }

        private static Canvas BuildCanvas(Camera cam)
        {
            var go = new GameObject("MainMenuCanvas [WorldSpace]");
            Undo.RegisterCreatedObjectUndo(go, "MainMenuCanvas");
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

        private static void BuildConfirmPopup(Transform parent)
        {
            var panel = U.MakePanel(parent, "ConfirmPopupPanel", false, new Color(0f, 0f, 0f, 0.85f));

            // 키오스크 흰색 화면 영역에 맞춰 위치/크기 조정
            var prt = panel.GetComponent<RectTransform>();
            prt.offsetMin = new Vector2(-6, 315);    // Left=-6, Bottom=315
            prt.offsetMax = new Vector2(-6, -3);      // Right=6, Top=3
            panel.transform.localScale = new Vector3(0.75f, 0.85f, 1f);

            // 중앙 박스 (가로 넓게 — Phase2 스타일)
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(panel.transform, false);
            var brt = box.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.10f, 0.35f);
            brt.anchorMax = new Vector2(0.90f, 0.65f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            box.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.22f);

            // 안내 문구 (한 줄에 다 보이게)
            U.MakeLabel(box, "ConfirmText", "정말 종료하시겠습니까?", 60,
                new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.90f), Color.white, _cachedFont);

            // 확인 / 취소 버튼 (Phase2 스타일 — 배경색 있는 버튼)
            U.MakeColoredButton(box, "ConfirmYesButton", "종료", 50,
                new Color(0.70f, 0.25f, 0.25f),
                new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.38f), _cachedFont);
            U.MakeColoredButton(box, "ConfirmNoButton", "취소", 50,
                new Color(0.35f, 0.35f, 0.40f),
                new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.38f), _cachedFont);
        }

        [MenuItem("Tools/Generate Main Menu", true)]
        private static bool Validate() => true;
    }
}
#endif
