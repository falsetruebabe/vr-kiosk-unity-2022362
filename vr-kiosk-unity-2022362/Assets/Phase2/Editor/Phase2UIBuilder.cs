#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Phase2.Data;
using System.IO;

namespace Phase2.Editor
{
    public static class Phase2UIBuilder
    {
        private const float CANVAS_SCALE   = 0.005f;
        private const int   KIOSK_W        = 1080;
        private const int   KIOSK_H        = 1920;

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

            EnsureEventSystem();
            Camera cam = BuildPlayer();
            Canvas kiosk = BuildKioskCanvas(cam);

            // Panels
            var startCG   = BuildStartPanel(kiosk.transform);
            var menuCG    = BuildMenuPanel(kiosk.transform);
            var optionCG  = BuildOptionPanel(kiosk.transform);
            var cartCG    = MakePanel(kiosk.transform, "CartReviewPanelHidden", false, Color.black).GetComponent<CanvasGroup>(); // Dummy, not used but needed for state manager
            var payCG     = BuildPaymentPanel(kiosk.transform);
            var finishCG  = BuildFinishPanel(kiosk.transform);
            
            // A2: 유휴 패턴 힌트 패널
            var hintCG    = BuildHintPanel(kiosk.transform);

            // 힌트 버튼 (노란색, 메뉴 패널 내 우측 하단)
            var hintBtnResult = BuildHintButton(kiosk.transform);

            // 힌트 상세 팝업 (메뉴 위치 안내)
            var hintDetailResult = BuildHintDetailPopup(kiosk.transform);

            BuildMissionOverlay();

            // GameManager with all managers
            GameObject gm = new GameObject("─── GameManager ───");
            Undo.RegisterCreatedObjectUndo(gm, "Create GameManager");

            var stateManager = gm.AddComponent<Core.KioskStateManager>();
            var cartManager  = gm.AddComponent<Core.CartManager>();
            var idleTracker  = gm.AddComponent<Core.IdleTimeTracker>();
            var uiController = gm.AddComponent<Core.KioskUIController>();
            var missionManager = gm.AddComponent<Mission.MissionManager>();

            // Wire references
            WireField(stateManager, "startPanelGroup",  startCG);
            WireField(stateManager, "menuPanelGroup",   menuCG);
            WireField(stateManager, "optionPopupGroup", optionCG);
            WireField(stateManager, "cartReviewGroup",  cartCG);
            WireField(stateManager, "paymentGroup",     payCG);
            WireField(stateManager, "finishGroup",      finishCG);
            
            WireField(idleTracker, "hintPanelGroup", hintCG);
            WireField(idleTracker, "hintButtonGroup", hintBtnResult.cg);
            WireField(idleTracker, "hintButton", hintBtnResult.btn);
            WireField(idleTracker, "hintDetailGroup", hintDetailResult.cg);
            WireField(idleTracker, "hintDetailLabel", hintDetailResult.label);
            WireField(idleTracker, "hintDetailCloseButton", hintDetailResult.closeBtn);

            WireField(uiController, "stateManager", stateManager);
            WireField(uiController, "cartManager",  cartManager);

            // Create sample data and wire database
            CafeMenuDatabase db = CreateSampleData();
            WireField(uiController, "menuDatabase", db);

            // Wire UI element references from panels
            WireUIControllerPanelRefs(uiController, kiosk.transform);

            Undo.CollapseUndoOperations(ug);
            Debug.Log("[Phase2UIBuilder] ✅ 완료!");
            EditorUtility.DisplayDialog("Phase 2 Builder",
                "Phase 2 환경 생성 완료!\n\n▶ Play 버튼을 눌러 테스트해보세요.", "확인");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  EventSystem & Camera
        // ═══════════════════════════════════════════════════════════════════
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

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
            bg.color = new Color(0.06f, 0.07f, 0.10f, 0.97f);
            return c;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Panel Builders
        // ═══════════════════════════════════════════════════════════════════

        // ── START PANEL ──
        private static CanvasGroup BuildStartPanel(Transform parent)
        {
            var panel = MakePanel(parent, "StartPanel", true, new Color(0.08f, 0.10f, 0.16f));
            MakeLabel(panel, "TitleLabel", "카페 키오스크", 84,
                new Vector2(0, 0.55f), new Vector2(1, 0.75f), Color.white);
            
            // 제안2: 매장, 포장 2개 버튼 (하단 20%)
            MakeButton(panel, "DineInButton", "매장", 48,
                new Color(0.20f, 0.50f, 0.90f),
                new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.25f));
                
            MakeButton(panel, "TakeOutButton", "포장", 48,
                new Color(0.80f, 0.40f, 0.20f),
                new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.25f));
                
            return panel.GetComponent<CanvasGroup>();
        }

        // ── MENU PANEL ──
        private static CanvasGroup BuildMenuPanel(Transform parent)
        {
            var panel = MakePanel(parent, "MenuPanel", false, new Color(0.06f, 0.08f, 0.12f));

            MakeLabel(panel, "MenuHeaderLabel", "메뉴 선택", 48,
                new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.98f), Color.white);

            // 제안3: 메뉴 카드 영역은 35% ~ 85%
            var tabArea = MakeContainer(panel, "CategoryTabContainer",
                new Vector2(0.02f, 0.84f), new Vector2(0.98f, 0.91f));
            var hLayout = tabArea.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 12; hLayout.childForceExpandWidth = true; hLayout.childForceExpandHeight = true;

            var cardArea = MakeContainer(panel, "MenuCardContainer",
                new Vector2(0.02f, 0.35f), new Vector2(0.98f, 0.83f));
            var grid = cardArea.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300, 260);
            grid.spacing = new Vector2(25, 25);
            grid.padding = new RectOffset(20, 20, 20, 20);
            grid.childAlignment = TextAnchor.UpperLeft;

            // 구분선
            var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
            lineGo.transform.SetParent(panel.transform, false);
            var lrt = lineGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.02f, 0.33f); lrt.anchorMax = new Vector2(0.98f, 0.33f);
            lrt.sizeDelta = new Vector2(0, 4);
            lineGo.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f);

            // 인라인 장바구니 리스트 영역 (12% ~ 32%)
            var cartArea = MakeContainer(panel, "InlineCartListContainer",
                new Vector2(0.02f, 0.12f), new Vector2(0.98f, 0.32f));
            var vLayout = cartArea.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 8; vLayout.childForceExpandWidth = true; vLayout.childForceExpandHeight = false; vLayout.childControlHeight = false;

            // 하단 결제 정보 및 버튼 (0% ~ 10%)
            MakeLabel(panel, "InlineTotalPriceLabel", "합계: 0원", 42,
                new Vector2(0.03f, 0.02f), new Vector2(0.5f, 0.10f), Color.white);
                
            MakeButton(panel, "PayButton", "결제하기", 36,
                new Color(0.20f, 0.50f, 0.90f),
                new Vector2(0.55f, 0.02f), new Vector2(0.95f, 0.10f));

            return panel.GetComponent<CanvasGroup>();
        }

        // ── OPTION POPUP PANEL ──
        private static CanvasGroup BuildOptionPanel(Transform parent)
        {
            var panel = MakePanel(parent, "OptionPopupPanel", false, new Color(0.10f, 0.10f, 0.14f, 0.95f));

            MakeLabel(panel, "OptionMenuNameLabel", "(메뉴 이름)", 52,
                new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.90f), Color.white);
            MakeLabel(panel, "OptionPriceLabel", "가격: 0원", 36,
                new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.75f),
                new Color(0.7f, 0.85f, 1f));

            var optContainer = MakeContainer(panel, "OptionButtonContainer",
                new Vector2(0.10f, 0.28f), new Vector2(0.90f, 0.58f));
            var vLayout = optContainer.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 14; vLayout.childForceExpandWidth = true; vLayout.childForceExpandHeight = false; vLayout.childControlHeight = false;

            MakeButton(panel, "CancelOptionButton", "취소", 34,
                new Color(0.50f, 0.25f, 0.25f),
                new Vector2(0.08f, 0.10f), new Vector2(0.48f, 0.20f));
            MakeButton(panel, "AddToCartButton", "담기", 34,
                new Color(0.20f, 0.65f, 0.40f),
                new Vector2(0.52f, 0.10f), new Vector2(0.92f, 0.20f));

            return panel.GetComponent<CanvasGroup>();
        }

        // ── PAYMENT PANEL ──
        private static CanvasGroup BuildPaymentPanel(Transform parent)
        {
            var panel = MakePanel(parent, "PaymentPanel", false, new Color(0.05f, 0.06f, 0.10f));
            MakeLabel(panel, "PaymentProgressLabel", "결제 처리 중...", 48,
                new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.60f), Color.white);
            return panel.GetComponent<CanvasGroup>();
        }

        // ── FINISH PANEL ──
        private static CanvasGroup BuildFinishPanel(Transform parent)
        {
            var panel = MakePanel(parent, "FinishPanel", false, new Color(0.06f, 0.10f, 0.08f));
            MakeLabel(panel, "FinishMessageLabel", "주문이 완료되었습니다!", 48,
                new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.78f), Color.white);
            MakeLabel(panel, "MissionResultLabel", "", 36,
                new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.60f),
                new Color(0.7f, 1f, 0.8f));
            MakeButton(panel, "RestartButton", "처음으로", 38,
                new Color(0.20f, 0.50f, 0.90f),
                new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.38f));
            return panel.GetComponent<CanvasGroup>();
        }
        
        // ── HINT PANEL (10초 유휴 후 뜨는 안내 문구) ──
        private static CanvasGroup BuildHintPanel(Transform parent)
        {
            var panel = MakePanel(parent, "HintPanel", false, Color.clear);
            
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(panel.transform, false);
            var rt = bgGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.3f); rt.anchorMax = new Vector2(0.9f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = new Color(0, 0, 0, 0.8f);
            
            MakeLabel(bgGo, "HintText", "어디가 어려우신가요?\n아래 노란색 힌트 버튼을 클릭해보세요!", 40,
                new Vector2(0, 0), new Vector2(1, 1), Color.yellow);
                
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

            return new HintBtnResult { cg = cg, btn = btn };
        }

        // ── HINT DETAIL POPUP (미션 기반 메뉴 위치 안내) ──
        private struct HintDetailResult { public CanvasGroup cg; public TextMeshProUGUI label; public Button closeBtn; }
        private static HintDetailResult BuildHintDetailPopup(Transform parent)
        {
            var panel = MakePanel(parent, "HintDetailPopup", false, new Color(0.06f, 0.06f, 0.10f, 0.92f));
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

            // 힌트 텍스트
            var labelGo = MakeLabel(panel, "HintDetailText", "(힌트 로딩 중...)", 30,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.82f), Color.white);
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
            prt.anchorMax = new Vector2(0.22f, 0.99f);   // 가로 21% 폭, 세로 34%
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
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TMPro.TextOverflowModes.Truncate;

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
            WireProp(so, "dineInButton", Find(canvas, "StartPanel/DineInButton"));
            WireProp(so, "takeOutButton", Find(canvas, "StartPanel/TakeOutButton"));

            // Menu
            WireProp(so, "categoryTabContainer", Find(canvas, "MenuPanel/CategoryTabContainer"));
            WireProp(so, "menuCardContainer", Find(canvas, "MenuPanel/MenuCardContainer"));
            WireProp(so, "inlineCartListContainer", Find(canvas, "MenuPanel/InlineCartListContainer"));
            WireProp(so, "inlineTotalPriceLabel", Find(canvas, "MenuPanel/InlineTotalPriceLabel"));
            WireProp(so, "payButton", Find(canvas, "MenuPanel/PayButton"));

            // Option
            WireProp(so, "optionMenuNameLabel", Find(canvas, "OptionPopupPanel/OptionMenuNameLabel"));
            WireProp(so, "optionPriceLabel", Find(canvas, "OptionPopupPanel/OptionPriceLabel"));
            WireProp(so, "optionButtonContainer", Find(canvas, "OptionPopupPanel/OptionButtonContainer"));
            WireProp(so, "addToCartButton", Find(canvas, "OptionPopupPanel/AddToCartButton"));
            WireProp(so, "cancelOptionButton", Find(canvas, "OptionPopupPanel/CancelOptionButton"));

            // Payment
            WireProp(so, "paymentProgressLabel", Find(canvas, "PaymentPanel/PaymentProgressLabel"));

            // Finish
            WireProp(so, "finishMessageLabel", Find(canvas, "FinishPanel/FinishMessageLabel"));
            WireProp(so, "missionResultLabel", Find(canvas, "FinishPanel/MissionResultLabel"));
            WireProp(so, "restartButton", Find(canvas, "FinishPanel/RestartButton"));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Sample Data
        // ═══════════════════════════════════════════════════════════════════
        private static CafeMenuDatabase CreateSampleData()
        {
            string dir = "Assets/Phase2/Data/SampleData";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/Phase2/Data", "SampleData");
            }

            // Options
            var hot  = MakeOption(dir, "HOT",    "hot",  OptionCategoryType.TEMPERATURE, 0);
            var ice  = MakeOption(dir, "ICE",    "ice",  OptionCategoryType.TEMPERATURE, 500);
            var shot = MakeOption(dir, "샷 추가", "shot1", OptionCategoryType.DENSITY, 500);

            // 제안1: 커피 메뉴용(샷 추가 포함) vs 논커피 메뉴용(샷 추가 제외)
            var coffeeOpts = new CafeMenuOption[] { hot, ice, shot };
            var nonCoffeeOpts = new CafeMenuOption[] { hot, ice };

            // Menu Items
            var items = new (string name, string id, int price, string cat)[]
            {
                ("아메리카노",     "americano",  4500, "커피"),
                ("카페라떼",       "cafelatte",  5000, "커피"),
                ("바닐라라떼",     "vanilla",    5500, "커피"),
                ("카라멜마끼아또", "caramel",    5500, "커피"),
                ("초코라떼",       "choco",      5500, "논커피"),
                ("녹차라떼",       "greentea",   5500, "논커피"),
                ("스콘",           "scone",      3500, "디저트"),
                ("베이글",         "bagel",      4000, "디저트"),
                ("치즈케이크",     "cheesecake", 6500, "디저트"),
            };

            var coffees   = new System.Collections.Generic.List<CafeMenuItem>();
            var nonCoffee = new System.Collections.Generic.List<CafeMenuItem>();
            var dessert   = new System.Collections.Generic.List<CafeMenuItem>();

            foreach (var (n, id, p, cat) in items)
            {
                var mi = ScriptableObject.CreateInstance<CafeMenuItem>();
                mi.menuName = n; mi.menuId = id; mi.basePrice = p;
                mi.category = cat; 
                mi.availableOptions = (cat == "커피") ? coffeeOpts : (cat == "논커피" ? nonCoffeeOpts : new CafeMenuOption[0]);
                
                // Asset replace or create
                string assetPath = $"{dir}/{id}.asset";
                if (File.Exists(assetPath)) AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(mi, assetPath);
                
                if (cat == "커피") coffees.Add(mi); 
                else if (cat == "논커피") nonCoffee.Add(mi);
                else dessert.Add(mi);
            }

            // Database
            string dbPath = $"{dir}/CafeMenuDatabase.asset";
            if (File.Exists(dbPath)) AssetDatabase.DeleteAsset(dbPath);
            var db = ScriptableObject.CreateInstance<CafeMenuDatabase>();
            db.categories.Add(new CafeMenuDatabase.MenuCategory
                { categoryName = "커피", items = coffees });
            db.categories.Add(new CafeMenuDatabase.MenuCategory
                { categoryName = "논커피", items = nonCoffee });
            db.categories.Add(new CafeMenuDatabase.MenuCategory
                { categoryName = "디저트", items = dessert });
            AssetDatabase.CreateAsset(db, dbPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Phase2UIBuilder] 샘플 데이터 생성/업데이트 완료!");
            return db;
        }

        private static CafeMenuOption MakeOption(string dir, string label, string id,
            OptionCategoryType cat, int price)
        {
            string path = $"{dir}/{id}.asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            var o = ScriptableObject.CreateInstance<CafeMenuOption>();
            o.optionLabel = label; o.optionId = id;
            o.category = cat; o.additionalPrice = price;
            AssetDatabase.CreateAsset(o, path);
            return o;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  UI Factory Utilities
        // ═══════════════════════════════════════════════════════════════════
        private static GameObject MakePanel(Transform parent, string name, bool visible, Color bg)
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

        private static GameObject MakeLabel(GameObject parent, string name, string text,
            float size, Vector2 ancMin, Vector2 ancMax, Color color)
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
            return go;
        }

        private static GameObject MakeButton(GameObject parent, string name, string label,
            float fontSize, Color bg, Vector2 ancMin, Vector2 ancMax)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = ancMin; rt.anchorMax = ancMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = bg;
            go.AddComponent<Button>();

            // UX C1: Click Feedback
            go.AddComponent<Core.ClickFeedbackHandler>();

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = fontSize;
            tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }

        private static GameObject MakeContainer(GameObject parent, string name,
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
        //  Wiring Helpers
        // ═══════════════════════════════════════════════════════════════════
        private static void WireField(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireProp(SerializedObject so, string field, Transform found)
        {
            if (found == null) return;
            var prop = so.FindProperty(field);
            if (prop == null) return;

            // Auto-detect: Button, TextMeshProUGUI, or Transform
            string typeName = prop.type;
            if (typeName.Contains("Button"))
                prop.objectReferenceValue = found.GetComponent<Button>();
            else if (typeName.Contains("TextMeshProUGUI"))
                prop.objectReferenceValue = found.GetComponent<TextMeshProUGUI>();
            else
                prop.objectReferenceValue = found;
        }

        private static Transform Find(Transform root, string path)
        {
            return root.Find(path);
        }

        [MenuItem("Tools/Generate Phase 2 Environment", true)]
        private static bool Validate() => true;
    }
}
#endif
