#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Phase2.Data;
using Common.Editor;
using System.IO;
using U = Common.Editor.EditorUIBuilderUtils;

namespace Phase2.Editor
{
    public static class Phase2UIBuilder
    {
        private const float CANVAS_SCALE   = 0.005f;
        private const int   KIOSK_W        = 1080;
        private const int   KIOSK_H        = 1920;

        private const string FONT_ASSET_PATH = "Assets/Font/GmarketSansTTFBold SDF.asset";
        private const string MAIN_IMAGE_PATH = "Assets/IMG/Kiosk_Main.png";

        private static TMP_FontAsset _cachedFont;

        private static readonly Vector3 PLAYER_POS = new Vector3(0f, 1.5f, -1.5f);
        private static readonly Vector3 CANVAS_POS = new Vector3(0f, 1.3f,  3f);

        // ═══════════════════════════════════════════════════════════════════
        //  Entry Point
        // ═══════════════════════════════════════════════════════════════════
        [MenuItem("Tools/Generate Phase 2 Environment")]
        public static void Generate()
        {
            Undo.SetCurrentGroupName("Generate Phase 2 Environment");
            int ug = Undo.GetCurrentGroup();

            // Load custom font
            _cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
            if (_cachedFont == null)
                Debug.LogWarning($"[Phase2UIBuilder] Font not found at {FONT_ASSET_PATH}. Using TMP default.");

            U.EnsureEventSystem();
            Camera cam = BuildPlayer();
            Canvas kiosk = BuildKioskCanvas(cam);

            // Panels
            var startCG   = BuildStartPanel(kiosk.transform);
            var menuCG    = BuildMenuPanel(kiosk.transform);
            var optionCG  = BuildOptionPanel(kiosk.transform);
            var cartCG    = U.MakePanel(kiosk.transform, "CartReviewPanelHidden", false, Color.black).GetComponent<CanvasGroup>(); // Phase 3 확장용 더미 패널 (현재 미사용, FSM 상태 참조용으로 유지)
            var payCG     = BuildPaymentPanel(kiosk.transform);
            var finishCG  = BuildFinishPanel(kiosk.transform);
            
            // A2: 유휴 패턴 힌트 패널
            var hintCG    = BuildHintPanel(kiosk.transform);

            // 힌트 버튼 (노란색, 메뉴 패널 내 우측 하단)
            var hintBtnResult = BuildHintButton(kiosk.transform);

            // 힌트 상세 팝업 (메뉴 위치 안내)
            var hintDetailResult = BuildHintDetailPopup(kiosk.transform);

            // 주문 진행 중 뒤로가기 버튼 + 확인 팝업
            var backBtnGo = BuildBackDuringOrderButton(kiosk.transform);
            var confirmPopupResult = BuildConfirmPopup(kiosk.transform);

            BuildMissionOverlay();

            // GameManager with all managers
            GameObject gm = new GameObject("─── GameManager ───");
            Undo.RegisterCreatedObjectUndo(gm, "Create GameManager");

            var stateManager = gm.AddComponent<Core.KioskStateManager>();
            var cartManager  = gm.AddComponent<Core.CartManager>();
            var idleTracker  = gm.AddComponent<Core.IdleTimeTracker>();
            var uiController = gm.AddComponent<Core.KioskUIController>();
            var missionManager = gm.AddComponent<Mission.MissionManager>();
            gm.AddComponent<Core.SoundManager>(); // FIX-04: 공유 AudioSource 중앙 사운드 매니저

            // Wire references
            U.WireField(stateManager, "startPanelGroup",  startCG);
            U.WireField(stateManager, "menuPanelGroup",   menuCG);
            U.WireField(stateManager, "optionPopupGroup", optionCG);
            // A-4: Dim 오버레이 wiring
            var dimOverlayCG = kiosk.transform.Find("OptionDimOverlay")?.GetComponent<CanvasGroup>();
            U.WireField(stateManager, "optionDimGroup", dimOverlayCG);
            U.WireField(stateManager, "cartReviewGroup",  cartCG);
            U.WireField(stateManager, "paymentGroup",     payCG);
            U.WireField(stateManager, "finishGroup",      finishCG);
            
            U.WireField(idleTracker, "hintPanelGroup", hintCG);
            U.WireField(idleTracker, "hintButtonGroup", hintBtnResult.cg);
            U.WireField(idleTracker, "hintButton", hintBtnResult.btn);
            U.WireField(idleTracker, "hintDetailGroup", hintDetailResult.cg);
            U.WireField(idleTracker, "hintDetailLabel", hintDetailResult.label);
            U.WireField(idleTracker, "hintDetailCloseButton", hintDetailResult.closeBtn);

            U.WireField(uiController, "stateManager", stateManager);
            U.WireField(uiController, "cartManager",  cartManager);
            U.WireField(uiController, "uiFont", _cachedFont);

            // Create sample data and wire database
            CafeMenuDatabase db = CreateSampleData();
            U.WireField(uiController, "menuDatabase", db);

            // Wire UI element references from panels
            WireUIControllerPanelRefs(uiController, kiosk.transform);

            Undo.CollapseUndoOperations(ug);
            Debug.Log("[Phase2UIBuilder] ✅ 완료!");
            EditorUtility.DisplayDialog("Phase 2 Builder",
                "Phase 2 환경 생성 완료!\n\n▶ Play 버튼을 눌러 테스트해보세요.", "확인");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Camera & Player
        // ═══════════════════════════════════════════════════════════════════
        private static Camera BuildPlayer()
        {
            Camera cam = Camera.main;
            GameObject go;
            if (cam != null) { go = cam.gameObject; go.name = "Player [MainCamera]"; }
            else
            {
                go = new GameObject("Player [MainCamera]");
                Undo.RegisterCreatedObjectUndo(go, "Player");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
            }
            go.transform.position = PLAYER_POS;
            go.transform.rotation = Quaternion.identity;
            if (!go.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>())
                go.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
            if (!go.GetComponent<Phase2.Desktop.DesktopFPSController>())
                go.AddComponent<Phase2.Desktop.DesktopFPSController>();
                
            // 전역 AudioListener 중복 제거 (다중 씬 로드 시 경고 방지)
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            foreach (var l in listeners)
            {
                if (l.gameObject != go) Object.DestroyImmediate(l);
            }
            if (!go.GetComponent<AudioListener>())
                go.AddComponent<AudioListener>();
                
            return cam;
        }

        private static Canvas BuildKioskCanvas(Camera cam)
        {
            var go = new GameObject("KioskCanvas [WorldSpace]");
            Undo.RegisterCreatedObjectUndo(go, "KioskCanvas");
            go.transform.position = CANVAS_POS;
            go.transform.localScale = Vector3.one * CANVAS_SCALE;

            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.worldCamera = cam;
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(KIOSK_W, KIOSK_H);
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<CanvasScaler>();

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.94f, 0.94f, 0.96f, 0.97f); // 라이트 테마 베이스
            return c;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Panel Builders
        // ═══════════════════════════════════════════════════════════════════

        // ── START PANEL ──
        private static CanvasGroup BuildStartPanel(Transform parent)
        {
            var panel = U.MakePanel(parent, "StartPanel", true, new Color(0.95f, 0.95f, 0.98f));

            // 메인 이미지 삽입 (패널 크기에 딱 맞게 전체 채우기)
            var mainSprite = U.LoadSpriteFromPath(MAIN_IMAGE_PATH);
            if (mainSprite != null)
            {
                var imgGo = new GameObject("MainImage");
                Undo.RegisterCreatedObjectUndo(imgGo, "MainImage");
                imgGo.transform.SetParent(panel.transform, false);
                imgGo.transform.SetAsFirstSibling(); // 텍스트/버튼 뒤로 보내기
                
                var rt = imgGo.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; // 0, 0
                rt.anchorMax = Vector2.one;  // 1, 1
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                
                var img = imgGo.AddComponent<Image>();
                img.sprite = mainSprite;
                // preserveAspect = false 처리하여 패널 크기에 꽉 차게 됨
            }
            else
            {
                Debug.LogWarning($"[Phase2UIBuilder] Main image not found at {MAIN_IMAGE_PATH}");
            }

            var titleGo = U.MakeLabel(panel, "TitleLabel", "카페 키오스크", 84,
                new Vector2(0, 0.55f), new Vector2(1, 0.75f), new Color(0.1f, 0.1f, 0.1f), _cachedFont);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.offsetMin = new Vector2(10, -700);
            titleRt.offsetMax = new Vector2(-10, -700);
            
            // 제안2: 매장, 포장 2개 버튼 (하단 20%)
            var dineIn = U.MakeColoredButton(panel, "DineInButton", "매장", 48,
                new Color(0.20f, 0.50f, 0.90f),
                new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.25f), _cachedFont, true);
            var dineRt = dineIn.GetComponent<RectTransform>();
            dineRt.offsetMin = new Vector2(0, -78);
            dineRt.offsetMax = new Vector2(0, -78);
                
            var takeOut = U.MakeColoredButton(panel, "TakeOutButton", "포장", 48,
                new Color(0.80f, 0.40f, 0.20f),
                new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.25f), _cachedFont, true);
            var takeRt = takeOut.GetComponent<RectTransform>();
            takeRt.offsetMin = new Vector2(0, -78);
            takeRt.offsetMax = new Vector2(0, -78);
                
            return panel.GetComponent<CanvasGroup>();
        }

        // ── MENU PANEL ──
        private static CanvasGroup BuildMenuPanel(Transform parent)
        {
            var panel = U.MakePanel(parent, "MenuPanel", false, new Color(0.96f, 0.96f, 0.98f));

            // ═══ 3단 레이아웃: 상단 주황색 헤더 바 (93% ~ 100%) ═══
            var headerBg = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
            headerBg.transform.SetParent(panel.transform, false);
            var hrt = headerBg.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0, 0.93f); hrt.anchorMax = new Vector2(1, 1);
            hrt.offsetMin = hrt.offsetMax = Vector2.zero;
            headerBg.GetComponent<Image>().color = Phase2.Core.UIPoolHelper.COL_PRIMARY;

            // 안내 문구 — 좌측 "뒤로" 버튼 공간(~20%)을 비우고 우측에 배치
            U.MakeLabel(headerBg, "MenuHeaderLabel", "원하시는 메뉴를 선택해 주세요", 36,
                new Vector2(0.20f, 0.05f), new Vector2(0.97f, 0.95f), Color.white, _cachedFont);

            // ═══ 카테고리 탭 영역 (85% ~ 91%) — 헤더와 간격을 두어 독립 배치 ═══
            var tabArea = U.MakeContainer(panel, "CategoryTabContainer",
                new Vector2(0.02f, 0.85f), new Vector2(0.98f, 0.91f));
            var hLayout = tabArea.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 12; hLayout.childForceExpandWidth = true; hLayout.childForceExpandHeight = true;

            // ═══ 중단 메뉴 카드 영역 (35% ~ 84%) ═══
            var cardArea = U.MakeContainer(panel, "MenuCardContainer",
                new Vector2(0.02f, 0.35f), new Vector2(0.98f, 0.84f));
            var grid = cardArea.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300, 260);
            grid.spacing = new Vector2(25, 25);
            grid.padding = new RectOffset(20, 20, 20, 20);
            grid.childAlignment = TextAnchor.UpperLeft;

            // ═══ 하단 회색 결제 영역 (0% ~ 34%) ═══
            var bottomBg = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
            bottomBg.transform.SetParent(panel.transform, false);
            var brt2 = bottomBg.GetComponent<RectTransform>();
            brt2.anchorMin = new Vector2(0, 0); brt2.anchorMax = new Vector2(1, 0.34f);
            brt2.offsetMin = brt2.offsetMax = Vector2.zero;
            bottomBg.GetComponent<Image>().color = new Color(0.93f, 0.93f, 0.95f);

            // 구분선 (34% 라인)
            var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(panel.transform, false);
            var lrt = lineGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.02f, 0.34f); lrt.anchorMax = new Vector2(0.98f, 0.34f);
            lrt.sizeDelta = new Vector2(0, 3);
            lineGo.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.85f);

            // 인라인 장바구니 리스트 영역 (12% ~ 33%)
            var cartArea = U.MakeContainer(panel, "InlineCartListContainer",
                new Vector2(0.02f, 0.12f), new Vector2(0.98f, 0.33f));
            var vLayout = cartArea.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 8; vLayout.childForceExpandWidth = true; vLayout.childForceExpandHeight = false; vLayout.childControlHeight = false;

            // 하단 결제 정보 및 버튼 (0% ~ 10%)
            U.MakeLabel(panel, "InlineTotalPriceLabel", "합계: 0원", 42,
                new Vector2(0.03f, 0.02f), new Vector2(0.5f, 0.10f), new Color(0.1f, 0.1f, 0.1f), _cachedFont);
                
            U.MakeColoredButton(panel, "PayButton", "결제하기", 36,
                Phase2.Core.UIPoolHelper.COL_PRIMARY,
                new Vector2(0.55f, 0.02f), new Vector2(0.95f, 0.10f), _cachedFont, true);

            return panel.GetComponent<CanvasGroup>();
        }

        // ── OPTION POPUP PANEL ──
        private static CanvasGroup BuildOptionPanel(Transform parent)
        {
            // A-4: 모달 Dim 오버레이 (반투명 검은색 장막 — 시선 집중용)
            var dimOverlay = U.MakePanel(parent, "OptionDimOverlay", false, new Color(0f, 0f, 0f, 0.6f));
            
            var panel = U.MakePanel(parent, "OptionPopupPanel", false, new Color(0.98f, 0.98f, 1.0f, 0.98f));

            U.MakeLabel(panel, "OptionMenuNameLabel", "(메뉴 이름)", 52,
                new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.90f), new Color(0.1f, 0.1f, 0.1f), _cachedFont);
            U.MakeLabel(panel, "OptionPriceLabel", "가격: 0원", 36,
                new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.75f),
                Phase2.Core.UIPoolHelper.COL_PRIMARY, _cachedFont);

            var optContainer = U.MakeContainer(panel, "OptionButtonContainer",
                new Vector2(0.10f, 0.28f), new Vector2(0.90f, 0.58f));
            var vLayout = optContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 14; vLayout.childForceExpandWidth = true; vLayout.childForceExpandHeight = false; vLayout.childControlHeight = false;

            U.MakeColoredButton(panel, "CancelOptionButton", "취소", 34,
                new Color(0.50f, 0.25f, 0.25f),
                new Vector2(0.08f, 0.10f), new Vector2(0.48f, 0.20f), _cachedFont, true);
            U.MakeColoredButton(panel, "AddToCartButton", "담기", 34,
                new Color(0.20f, 0.65f, 0.40f),
                new Vector2(0.52f, 0.10f), new Vector2(0.92f, 0.20f), _cachedFont, true);

            return panel.GetComponent<CanvasGroup>();
        }

        // ── PAYMENT PANEL ──
        private static CanvasGroup BuildPaymentPanel(Transform parent)
        {
            var panel = U.MakePanel(parent, "PaymentPanel", false, new Color(0.95f, 0.95f, 0.97f));
            U.MakeLabel(panel, "PaymentProgressLabel", "결제 처리 중...", 48,
                new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.60f), new Color(0.1f, 0.1f, 0.1f), _cachedFont);
            return panel.GetComponent<CanvasGroup>();
        }

        // ── FINISH PANEL ──
        private static CanvasGroup BuildFinishPanel(Transform parent)
        {
            var panel = U.MakePanel(parent, "FinishPanel", false, new Color(0.95f, 0.96f, 0.95f));
            U.MakeLabel(panel, "FinishMessageLabel", "주문이 완료되었습니다!", 48,
                new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.95f), new Color(0.1f, 0.1f, 0.1f), _cachedFont);
            U.MakeLabel(panel, "MissionResultLabel", "", 36,
                new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.82f),
                new Color(0.1f, 0.1f, 0.1f), _cachedFont);

            // FEAT-01: 영수증 영역
            var receiptGO = U.MakeLabel(panel, "ReceiptLabel", "", 26,
                new Vector2(0.10f, 0.26f), new Vector2(0.90f, 0.70f),
                new Color(0.15f, 0.15f, 0.15f), _cachedFont);
            var receiptTMP = receiptGO.GetComponent<TMPro.TextMeshProUGUI>();
            if (receiptTMP != null)
            {
                receiptTMP.alignment = TMPro.TextAlignmentOptions.TopLeft;
                receiptTMP.lineSpacing = 8f;
            }

            // 버튼 2개: 다시 주문하기 / 난이도 선택 화면으로
            U.MakeColoredButton(panel, "RestartButton", "다시 주문하기", 36,
                Phase2.Core.UIPoolHelper.COL_PRIMARY,
                new Vector2(0.05f, 0.06f), new Vector2(0.48f, 0.20f), _cachedFont, true);
            U.MakeColoredButton(panel, "BackToDifficultyButton", "난이도 선택 화면으로", 32,
                new Color(0.70f, 0.70f, 0.73f),
                new Vector2(0.52f, 0.06f), new Vector2(0.95f, 0.20f), _cachedFont, true);
            return panel.GetComponent<CanvasGroup>();
        }
        
        // ── HINT PANEL (10초 유휴 후 뜨는 안내 문구) ──
        private static CanvasGroup BuildHintPanel(Transform parent)
        {
            var panel = U.MakePanel(parent, "HintPanel", false, Color.clear);
            
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(panel.transform, false);
            var rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.3f); rt.anchorMax = new Vector2(0.9f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);
            
            U.MakeLabel(bgGo, "HintText", "어디가 어려우신가요?\n아래 노란색 힌트 버튼을 클릭해보세요!", 40,
                new Vector2(0, 0), new Vector2(1, 1), Color.yellow, _cachedFont);
                
            return panel.GetComponent<CanvasGroup>();
        }

        // ── HINT BUTTON (노란색, 키오스크 우측 하단) ──
        private struct HintBtnResult { public CanvasGroup cg; public Button btn; }
        private static HintBtnResult BuildHintButton(Transform parent)
        {
            // CanvasGroup 래퍼
            var wrapper = new GameObject("HintButtonWrapper");
            Undo.RegisterCreatedObjectUndo(wrapper, "HintButtonWrapper");
            wrapper.transform.SetParent(parent, false);
            var wrt = wrapper.AddComponent<RectTransform>();
            wrt.anchorMin = Vector2.zero; wrt.anchorMax = Vector2.one;
            wrt.offsetMin = wrt.offsetMax = Vector2.zero;
            var cg = wrapper.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

            // 버튼 자체
            var btnGo = new GameObject("HintButton");
            Undo.RegisterCreatedObjectUndo(btnGo, "HintButton");
            btnGo.transform.SetParent(wrapper.transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.70f, 0.35f);
            brt.anchorMax = new Vector2(0.98f, 0.43f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            btnGo.AddComponent<Image>().color = new Color(0.95f, 0.85f, 0.15f); // 노란색
            var btn = btnGo.AddComponent<Button>();
            btnGo.AddComponent<Core.ClickFeedbackHandler>();

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(btnGo.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = "힌트"; tmp.fontSize = 36;
            tmp.color = new Color(0.1f, 0.1f, 0.1f); tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = TMPro.FontStyles.Bold;
            if (_cachedFont != null) tmp.font = _cachedFont;

            return new HintBtnResult { cg = cg, btn = btn };
        }

        // ── HINT DETAIL POPUP (미션 기반 메뉴 위치 안내) ──
        private struct HintDetailResult { public CanvasGroup cg; public TextMeshProUGUI label; public Button closeBtn; }
        private static HintDetailResult BuildHintDetailPopup(Transform parent)
        {
            var panel = U.MakePanel(parent, "HintDetailPopup", false, new Color(0.06f, 0.06f, 0.10f, 0.92f));
            var prt = panel.GetComponent<RectTransform>();
            // 키오스크 하단 약 50% 영역
            prt.anchorMin = new Vector2(0.03f, 0.12f);
            prt.anchorMax = new Vector2(0.97f, 0.55f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;

            // X 닫기 버튼 (우측 상단)
            var closeGo = new GameObject("CloseButton");
            Undo.RegisterCreatedObjectUndo(closeGo, "CloseButton");
            closeGo.transform.SetParent(panel.transform, false);
            var crt = closeGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.88f, 0.85f);
            crt.anchorMax = new Vector2(0.98f, 0.98f);
            crt.offsetMin = crt.offsetMax = Vector2.zero;
            closeGo.AddComponent<Image>().color = new Color(0.7f, 0.2f, 0.2f);
            var closeBtn = closeGo.AddComponent<Button>();
            closeGo.AddComponent<Core.ClickFeedbackHandler>();

            var xLbl = new GameObject("XLabel");
            xLbl.transform.SetParent(closeGo.transform, false);
            var xlrt = xLbl.AddComponent<RectTransform>();
            xlrt.anchorMin = Vector2.zero; xlrt.anchorMax = Vector2.one;
            xlrt.offsetMin = xlrt.offsetMax = Vector2.zero;
            var xtmp = xLbl.AddComponent<TextMeshProUGUI>();
            xtmp.text = "X"; xtmp.fontSize = 28;
            xtmp.color = Color.white; xtmp.alignment = TextAlignmentOptions.Center;
            if (_cachedFont != null) xtmp.font = _cachedFont;

            // 힌트 텍스트
            var labelGo = U.MakeLabel(panel, "HintDetailText", "(힌트 로딩 중...)", 30,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.82f), Color.white, _cachedFont);
            var labelTmp = labelGo.GetComponent<TextMeshProUGUI>();
            labelTmp.alignment = TextAlignmentOptions.TopLeft;
            labelTmp.enableWordWrapping = true;

            return new HintDetailResult
            {
                cg = panel.GetComponent<CanvasGroup>(),
                label = labelTmp,
                closeBtn = closeBtn
            };
        }

        // ── BACK DURING ORDER BUTTON (주문 진행 중 뒤로가기 - 키오스크 좌측 상단) ──
        private static GameObject BuildBackDuringOrderButton(Transform parent)
        {
            var btnGo = new GameObject("BackDuringOrderButton");
            Undo.RegisterCreatedObjectUndo(btnGo, "BackDuringOrderButton");
            btnGo.transform.SetParent(parent, false);
            var rt = btnGo.AddComponent<RectTransform>();
            // 헤더 바(93%~100%) 좌측 내부에 시각적으로 통합 배치
            rt.anchorMin = new Vector2(0.02f, 0.94f);
            rt.anchorMax = new Vector2(0.18f, 0.99f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            btnGo.AddComponent<Image>().color = new Color(0.80f, 0.30f, 0.18f, 0.9f); // 헤더보다 약간 진한 주황
            btnGo.AddComponent<Button>();
            btnGo.AddComponent<Core.ClickFeedbackHandler>();

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(btnGo.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = "← 뒤로"; tmp.fontSize = 30;
            tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
            if (_cachedFont != null) tmp.font = _cachedFont;

            return btnGo;
        }

        // ── CONFIRM POPUP (주문 취소 확인 팝업) ──
        private static CanvasGroup BuildConfirmPopup(Transform parent)
        {
            var panel = U.MakePanel(parent, "ConfirmPopupPanel", false, new Color(0f, 0f, 0f, 0.85f));

            // 중앙 박스
            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(panel.transform, false);
            var brt = box.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.10f, 0.35f);
            brt.anchorMax = new Vector2(0.90f, 0.65f);
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            box.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.22f);

            // 안내 문구
            U.MakeLabel(box, "ConfirmText", "주문을 취소하시겠습니까?\n장바구니가 초기화되고\n난이도 선택 화면으로 이동합니다.", 36,
                new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.90f), Color.white, _cachedFont);

            // 확인 / 취소 버튼
            U.MakeColoredButton(box, "ConfirmYesButton", "확인", 34,
                new Color(0.70f, 0.25f, 0.25f),
                new Vector2(0.08f, 0.08f), new Vector2(0.48f, 0.38f), _cachedFont, true);
            U.MakeColoredButton(box, "ConfirmNoButton", "취소", 34,
                new Color(0.35f, 0.35f, 0.40f),
                new Vector2(0.52f, 0.08f), new Vector2(0.92f, 0.38f), _cachedFont, true);

            return panel.GetComponent<CanvasGroup>();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Mission Overlay
        // ═══════════════════════════════════════════════════════════════════
        private static void BuildMissionOverlay()
        {
            var go = new GameObject("MissionOverlayCanvas");
            Undo.RegisterCreatedObjectUndo(go, "MissionOverlay");
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            // 주문서 패널 - 좌측 상단에 영수증 스타일로 배치
            var panel = new GameObject("MissionPanel");
            Undo.RegisterCreatedObjectUndo(panel, "MissionPanel");
            panel.transform.SetParent(go.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.01f, 0.65f);  // 좌측 상단
            prt.anchorMax = new Vector2(0.28f, 0.99f);   // 가로 27% 폭, 세로 34%
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.88f);
            var cg = panel.AddComponent<CanvasGroup>();
            cg.alpha = 0f; cg.interactable = false; cg.blocksRaycasts = false;

            var missionUI = panel.AddComponent<Mission.MissionUIPanel>();

            var labelGO = new GameObject("MissionLabel");
            Undo.RegisterCreatedObjectUndo(labelGO, "MissionLabel");
            labelGO.transform.SetParent(panel.transform, false);
            var lrt = labelGO.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.0f, 0.0f); lrt.anchorMax = new Vector2(1.0f, 1.0f);
            lrt.offsetMin = new Vector2(12, 8);   // 좌하단 패딩  
            lrt.offsetMax = new Vector2(-12, -8);  // 우상단 패딩
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "(대기 중)"; tmp.fontSize = 24;
            tmp.color = Color.white; 
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TMPro.TextOverflowModes.Truncate;
            if (_cachedFont != null) tmp.font = _cachedFont;

            var so = new SerializedObject(missionUI);
            so.FindProperty("missionLabel").objectReferenceValue = tmp;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Wire UI Controller Panel References
        // ═══════════════════════════════════════════════════════════════════
        private static void WireUIControllerPanelRefs(Core.KioskUIController ctrl, Transform canvas)
        {
            var so = new SerializedObject(ctrl);

            // Start
            U.WireProp(so, "dineInButton", U.Find(canvas, "StartPanel/DineInButton"));
            U.WireProp(so, "takeOutButton", U.Find(canvas, "StartPanel/TakeOutButton"));

            // Menu
            U.WireProp(so, "categoryTabContainer", U.Find(canvas, "MenuPanel/CategoryTabContainer"));
            U.WireProp(so, "menuCardContainer", U.Find(canvas, "MenuPanel/MenuCardContainer"));
            U.WireProp(so, "inlineCartListContainer", U.Find(canvas, "MenuPanel/InlineCartListContainer"));
            U.WireProp(so, "inlineTotalPriceLabel", U.Find(canvas, "MenuPanel/InlineTotalPriceLabel"));
            U.WireProp(so, "payButton", U.Find(canvas, "MenuPanel/PayButton"));

            // Option
            U.WireProp(so, "optionMenuNameLabel", U.Find(canvas, "OptionPopupPanel/OptionMenuNameLabel"));
            U.WireProp(so, "optionPriceLabel", U.Find(canvas, "OptionPopupPanel/OptionPriceLabel"));
            U.WireProp(so, "optionButtonContainer", U.Find(canvas, "OptionPopupPanel/OptionButtonContainer"));
            U.WireProp(so, "addToCartButton", U.Find(canvas, "OptionPopupPanel/AddToCartButton"));
            U.WireProp(so, "cancelOptionButton", U.Find(canvas, "OptionPopupPanel/CancelOptionButton"));

            // Payment
            U.WireProp(so, "paymentProgressLabel", U.Find(canvas, "PaymentPanel/PaymentProgressLabel"));

            // Finish
            U.WireProp(so, "finishMessageLabel", U.Find(canvas, "FinishPanel/FinishMessageLabel"));
            U.WireProp(so, "missionResultLabel", U.Find(canvas, "FinishPanel/MissionResultLabel"));
            U.WireProp(so, "receiptLabel", U.Find(canvas, "FinishPanel/ReceiptLabel"));  // FEAT-01
            U.WireProp(so, "restartButton", U.Find(canvas, "FinishPanel/RestartButton"));
            U.WireProp(so, "backToDifficultyButton", U.Find(canvas, "FinishPanel/BackToDifficultyButton"));

            // Back during order + Confirm Popup
            U.WireProp(so, "backDuringOrderButton", U.Find(canvas, "BackDuringOrderButton"));
            U.WireProp(so, "confirmPopupGroup", U.Find(canvas, "ConfirmPopupPanel"));
            U.WireProp(so, "confirmYesButton", U.Find(canvas, "ConfirmPopupPanel/Box/ConfirmYesButton"));
            U.WireProp(so, "confirmNoButton", U.Find(canvas, "ConfirmPopupPanel/Box/ConfirmNoButton"));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Sample Data
        // ═══════════════════════════════════════════════════════════════════
        private static CafeMenuDatabase CreateSampleData()
        {
            // BUG-02/03/04: 단일 데이터 소스(MenuDataUpdater)를 사용하여
            // ICE 가격 불일치, 고아 에셋, 디저트 카테고리 문제를 일괄 해결
            return Phase2.EditorScripts.MenuDataUpdater.EnsureDatabase();
        }

        [MenuItem("Tools/Generate Phase 2 Environment", true)]
        private static bool Validate() => true;
    }
}
#endif
