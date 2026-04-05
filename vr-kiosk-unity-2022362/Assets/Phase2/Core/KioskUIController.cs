using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Phase2.Data;

namespace Phase2.Core
{
    public enum OrderType { None, DineIn, TakeOut }

    /// <summary>
    /// Phase 2 키오스크 UI 인터랙션 컨트롤러.
    /// KioskStateManager.OnStateChanged를 구독하여 각 패널의 동적 콘텐츠를 생성하고
    /// 모든 버튼 클릭 이벤트를 중앙에서 관리한다.
    /// Pool 패턴을 적용하여 동적 UI 생성/파괴 비용을 감소.
    /// </summary>
    public class KioskUIController : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════
        //  Serialized Fields
        // ═══════════════════════════════════════════════════════════════════
        [Header("Data")]
        [SerializeField] private CafeMenuDatabase menuDatabase;

        [Header("Font")]
        [SerializeField] private TMP_FontAsset uiFont;

        [Header("Managers")]
        [SerializeField] private KioskStateManager stateManager;
        [SerializeField] private CartManager cartManager;

        [Header("Start Panel")]
        [SerializeField] private Button dineInButton;
        [SerializeField] private Button takeOutButton;

        [Header("Menu Panel")]
        [SerializeField] private Transform categoryTabContainer;
        [SerializeField] private Transform menuCardContainer;
        [SerializeField] private Transform inlineCartListContainer;
        [SerializeField] private TextMeshProUGUI inlineTotalPriceLabel;
        [SerializeField] private Button payButton;

        [Header("Option Popup")]
        [SerializeField] private TextMeshProUGUI optionMenuNameLabel;
        [SerializeField] private TextMeshProUGUI optionPriceLabel;
        [SerializeField] private Transform optionButtonContainer;
        [SerializeField] private Button addToCartButton;
        [SerializeField] private Button cancelOptionButton;

        [Header("Payment Panel")]
        [SerializeField] private TextMeshProUGUI paymentProgressLabel;

        [Header("Finish")]
        [SerializeField] private TextMeshProUGUI finishMessageLabel;
        [SerializeField] private TextMeshProUGUI missionResultLabel;
        [SerializeField] private Button restartButton;

        // ═══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ═══════════════════════════════════════════════════════════════════
        private OrderType _currentOrderType = OrderType.None;
        private CafeMenuItem _selectedMenuItem;
        private readonly List<CafeMenuOption> _selectedOptions = new List<CafeMenuOption>(4);
        private string _currentCategory;

        // Simple Object Pools
        private readonly List<GameObject> _poolMenuCards = new List<GameObject>(16);
        private readonly List<GameObject> _poolCatTabs   = new List<GameObject>(8);
        private readonly List<GameObject> _poolCartRows  = new List<GameObject>(16);
        private readonly List<GameObject> _poolOptToggles= new List<GameObject>(8);
        private readonly List<GameObject> _poolOptCategoryRows = new List<GameObject>(4); // 카테고리 컨테이너 풀
        private struct OptToggle { public GameObject go; public CafeMenuOption opt; public Image img; }
        private readonly List<OptToggle> _dynOptToggles = new List<OptToggle>(8);

        // Colors
        private static readonly Color COL_CARD_BG   = new Color(0.16f, 0.18f, 0.24f);
        private static readonly Color COL_PRIMARY    = new Color(0.20f, 0.50f, 0.90f);
        private static readonly Color COL_SUCCESS    = new Color(0.15f, 0.65f, 0.40f);
        private static readonly Color COL_TAB_OFF    = new Color(0.22f, 0.22f, 0.28f);
        private static readonly Color COL_OPT_ON     = new Color(0.18f, 0.62f, 0.42f);
        private static readonly Color COL_OPT_OFF    = new Color(0.28f, 0.28f, 0.34f);
        private static readonly Color COL_CART_ROW   = new Color(0.14f, 0.15f, 0.20f);

        // ═══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════════════════════
        private void Awake()
        {
            dineInButton?.onClick.AddListener(() => StartOrder(OrderType.DineIn));
            takeOutButton?.onClick.AddListener(() => StartOrder(OrderType.TakeOut));

            addToCartButton?.onClick.AddListener(OnAddToCart);
            cancelOptionButton?.onClick.AddListener(() =>
                stateManager.TransitionTo(KioskStateManager.KioskState.MenuSelect));
            payButton?.onClick.AddListener(() =>
                stateManager.TransitionTo(KioskStateManager.KioskState.PaymentProcessing));
            restartButton?.onClick.AddListener(OnRestart);
            
            SetupCartScrollView();
        }

        private void Start()
        {
            AssignAutoMission();
        }

        private void OnEnable()
        {
            KioskStateManager.OnStateChanged += OnStateChanged;
            CartManager.OnCartChanged += RefreshCartBadge;
        }

        private void OnDisable()
        {
            KioskStateManager.OnStateChanged -= OnStateChanged;
            CartManager.OnCartChanged -= RefreshCartBadge;
        }

        private void AssignAutoMission()
        {
            // 다중 복합 미션 시스템: 음료+디저트 무작위 조합 할당
            if (Mission.MissionManager.Instance != null && menuDatabase != null)
            {
                Mission.MissionManager.Instance.GenerateRandomMission(menuDatabase);
            }
        }

        private void SetupCartScrollView()
        {
            if (inlineCartListContainer == null) return;
            
            // 이미 설정된 경우 스킵
            if (inlineCartListContainer.parent != null && inlineCartListContainer.parent.name == "CartViewport") return;

            // 1. ScrollView 래퍼 생성 (마우스 휠 이벤트를 원천 차단한 커스텀 ScrollRect 사용)
            GameObject scrollViewWrapper = new GameObject("CartScrollView", typeof(RectTransform), typeof(XRNoWheelScrollRect));
            scrollViewWrapper.transform.SetParent(inlineCartListContainer.parent, false);
            RectTransform scrollRt = scrollViewWrapper.GetComponent<RectTransform>();
            
            // 이전 컨테이너의 영역 크기 및 위치 상속
            RectTransform originalRt = inlineCartListContainer.GetComponent<RectTransform>();
            scrollRt.anchorMin = originalRt.anchorMin;
            scrollRt.anchorMax = originalRt.anchorMax;
            scrollRt.pivot = originalRt.pivot;
            scrollRt.anchoredPosition = originalRt.anchoredPosition;
            scrollRt.sizeDelta = originalRt.sizeDelta;

            // 2. Viewport 및 Mask 생성 (영역 밖 클리핑)
            GameObject viewport = new GameObject("CartViewport", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Mask));
            viewport.transform.SetParent(scrollViewWrapper.transform, false);
            RectTransform viewRt = viewport.GetComponent<RectTransform>();
            viewRt.anchorMin = Vector2.zero;
            viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = Vector2.zero;
            viewRt.offsetMax = Vector2.zero;

            UnityEngine.UI.Image viewImg = viewport.GetComponent<UnityEngine.UI.Image>();
            viewImg.color = new Color(0, 0, 0, 0.01f);
            viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

            // 3. Content 설정
            inlineCartListContainer.SetParent(viewport.transform, false);
            originalRt.anchorMin = new Vector2(0, 1);
            originalRt.anchorMax = new Vector2(1, 1);
            originalRt.pivot = new Vector2(0.5f, 1);
            originalRt.anchoredPosition = Vector2.zero;

            // [수정점] 세로 레이아웃 겹침 방지를 위해 childControlHeight 비활성화 적용
            UnityEngine.UI.VerticalLayoutGroup vlg = inlineCartListContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (vlg == null) vlg = inlineCartListContainer.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = false; // 항목들이 서로 겹쳐서 납작해지는 현상 방지
            vlg.childControlWidth = true;
            vlg.spacing = 10;
            vlg.padding = new RectOffset(5, 5, 5, 5);

            UnityEngine.UI.ContentSizeFitter csf = inlineCartListContainer.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (csf == null) csf = inlineCartListContainer.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

            // 4. Scrollbar UI 동적 생성 (XR 디바이스 터치/포인팅 지원용)
            GameObject scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Scrollbar));
            scrollbarGo.transform.SetParent(scrollViewWrapper.transform, false);
            RectTransform sbRt = scrollbarGo.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1, 0);
            sbRt.anchorMax = new Vector2(1, 1);
            sbRt.pivot = new Vector2(1, 0.5f);
            sbRt.sizeDelta = new Vector2(25, 0); // 폭을 15에서 25로 확장 (사용자 요청)
            sbRt.anchoredPosition = new Vector2(-5, 0); // 우측 가장자리에서 안쪽으로 살짝 이동

            UnityEngine.UI.Image sbImg = scrollbarGo.GetComponent<UnityEngine.UI.Image>();
            sbImg.color = new Color(0.12f, 0.13f, 0.16f, 1f); // 어두운 배경

            GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarGo.transform, false);
            RectTransform saRt = slidingArea.GetComponent<RectTransform>();
            saRt.anchorMin = Vector2.zero; saRt.anchorMax = Vector2.one;
            saRt.offsetMin = new Vector2(2, 2); saRt.offsetMax = new Vector2(-2, -2); // 여백 조절

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform hRt = handle.GetComponent<RectTransform>();
            
            // [버그 원인 수정] 핸들의 크기가 기본 100x100으로 생성되어 튀어나오던 현상
            hRt.anchorMin = Vector2.zero; 
            hRt.anchorMax = Vector2.one;
            hRt.sizeDelta = Vector2.zero; 
            hRt.anchoredPosition = Vector2.zero;

            UnityEngine.UI.Image hImg = handle.GetComponent<UnityEngine.UI.Image>();
            hImg.color = new Color(0.5f, 0.5f, 0.5f, 1f); // 회색 핸들

            UnityEngine.UI.Scrollbar sbar = scrollbarGo.GetComponent<UnityEngine.UI.Scrollbar>();
            sbar.handleRect = hRt;
            sbar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;

            // Scrollbar 자리를 위해 Viewport 우측 여백 깎아내기 (스크롤바 너비에 맞게 조정)
            viewRt.offsetMax = new Vector2(-35, 0);

            // 5. ScrollRect 최종 연결
            XRNoWheelScrollRect sr = scrollViewWrapper.GetComponent<XRNoWheelScrollRect>();
            sr.content = originalRt;
            sr.viewport = viewRt;
            sr.horizontal = false; 
            sr.vertical = true;
            sr.movementType = UnityEngine.UI.ScrollRect.MovementType.Elastic;
            sr.verticalScrollbar = sbar;
            sr.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent; // 스크롤바 씬 내 상시 표출
            sr.verticalScrollbarSpacing = 2f;
        }

        private void StartOrder(OrderType type)
        {
            _currentOrderType = type;
            stateManager.TransitionTo(KioskStateManager.KioskState.MenuSelect);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  State Dispatch
        // ═══════════════════════════════════════════════════════════════════
        private void OnStateChanged(KioskStateManager.KioskState state)
        {
            switch (state)
            {
                case KioskStateManager.KioskState.MenuSelect:       BuildMenuPanel();    break;
                case KioskStateManager.KioskState.OptionSelect:     BuildOptionPanel();  break;
                case KioskStateManager.KioskState.PaymentProcessing:StartCoroutine(CoPayment()); break;
                case KioskStateManager.KioskState.Finish:           BuildFinishPanel();  break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Menu Panel (Integrated Inline Cart)
        // ═══════════════════════════════════════════════════════════════════
        private void BuildMenuPanel()
        {
            DeactivatePool(_poolCatTabs);
            DeactivatePool(_poolMenuCards);

            if (menuDatabase == null || menuDatabase.categories.Count == 0) return;
            if (string.IsNullOrEmpty(_currentCategory))
                _currentCategory = menuDatabase.categories[0].categoryName;

            // Category tabs
            for (int i = 0; i < menuDatabase.categories.Count; i++)
            {
                var cat = menuDatabase.categories[i];
                string name = cat.categoryName;
                bool active = name == _currentCategory;

                var go = GetPooledItem(_poolCatTabs, categoryTabContainer);
                SetupButtonUI(go, name, 28, active ? COL_PRIMARY : COL_TAB_OFF, new Vector2(200, 70));
                
                var btn = go.GetComponentInChildren<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    _currentCategory = name;
                    BuildMenuPanel();
                });
            }

            // Menu cards
            var items = menuDatabase.GetByCategory(_currentCategory);
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    CafeMenuItem captured = items[i];
                    var go = GetPooledItem(_poolMenuCards, menuCardContainer);
                    SetupMenuCardUI(go, captured);

                    var btn = go.GetComponentInChildren<Button>();
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() =>
                    {
                        _selectedMenuItem = captured;
                        _selectedOptions.Clear();
                        stateManager.TransitionTo(KioskStateManager.KioskState.OptionSelect);
                    });
                }
            }

            RefreshCartBadge();
        }

        private void RefreshCartBadge()
        {
            // Inline Cart Update
            DeactivatePool(_poolCartRows);
            if (cartManager == null) return;

            var items = cartManager.Items;
            for (int i = 0; i < items.Count; i++)
            {
                int index = i; // Closure capture
                var ci = items[i];
                string optStr = "";
                foreach (var o in ci.selectedOptions) optStr += o.optionLabel + " ";
                string text = $"{ci.menuItem.menuName}  {optStr}   {ci.CalculateTotalPrice():N0}원";

                var go = GetPooledItem(_poolCartRows, inlineCartListContainer);
                SetupCartRowUI(go, text, ci.quantity, index);
            }

            if (inlineTotalPriceLabel != null)
                inlineTotalPriceLabel.text = items.Count > 0 ? $"합계: {cartManager.TotalPrice:N0}원" : "메뉴를 담아주세요.";

            // C3: 빈 장바구니 결제 방지
            if (payButton != null)
                payButton.interactable = items.Count > 0;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Option Panel
        // ═══════════════════════════════════════════════════════════════════
        private void BuildOptionPanel()
        {
            DeactivatePool(_poolOptToggles);
            if (_poolOptCategoryRows != null) DeactivatePool(_poolOptCategoryRows);
            _dynOptToggles.Clear();
            _selectedOptions.Clear();

            if (_selectedMenuItem == null) return;
            if (optionMenuNameLabel != null) optionMenuNameLabel.text = _selectedMenuItem.menuName;
            
            if (_selectedMenuItem.availableOptions != null && _selectedMenuItem.availableOptions.Length > 0)
            {
                // 카테고리별 분리
                var groupedOpts = new Dictionary<OptionCategoryType, List<CafeMenuOption>>();
                foreach(var opt in _selectedMenuItem.availableOptions)
                {
                    if (opt.category == OptionCategoryType.NONE) continue;
                    if (!groupedOpts.ContainsKey(opt.category)) groupedOpts[opt.category] = new List<CafeMenuOption>();
                    groupedOpts[opt.category].Add(opt);
                }

                // 카테고리별 Row 렌더링
                foreach (var kvp in groupedOpts)
                {
                    var rowGo = GetPooledItem(_poolOptCategoryRows, optionButtonContainer);
                    Transform btnContainer = SetupOptionCategoryRow(rowGo, CategoryToKrString(kvp.Key));

                    foreach (var captured in kvp.Value)
                    {
                        var go = GetPooledItem(_poolOptToggles, btnContainer);
                        string btnLabel = captured.optionLabel;
                        if (captured.additionalPrice > 0) btnLabel += $" (+{captured.additionalPrice})";
                        
                        SetupButtonUI(go, btnLabel, 24, COL_OPT_OFF, new Vector2(160, 70));
                        
                        var img = go.GetComponent<Image>();
                        var btn = go.GetComponentInChildren<Button>();
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => ToggleOption(captured));
                        
                        _dynOptToggles.Add(new OptToggle { go = go, opt = captured, img = img });
                    }
                }
            }

            ValidateOptionSelection();
        }

        private string CategoryToKrString(OptionCategoryType cat)
        {
            switch (cat)
            {
                case OptionCategoryType.TEMPERATURE: return "온도";
                case OptionCategoryType.SIZE: return "사이즈";
                case OptionCategoryType.DENSITY: return "농도";
                default: return "옵션";
            }
        }

        private Transform SetupOptionCategoryRow(GameObject row, string title)
        {
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, 80);
            
            var hlg = row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (hlg == null) hlg = row.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            hlg.childControlHeight = true; hlg.childControlWidth = true; // 사이즈 강제 제어
            hlg.childForceExpandHeight = false; hlg.childForceExpandWidth = false;
            hlg.spacing = 20;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.childAlignment = TextAnchor.MiddleLeft;

            var titleGo = row.transform.Find("Title")?.gameObject;
            if (titleGo == null)
            {
                titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(UnityEngine.UI.LayoutElement));
                titleGo.transform.SetParent(row.transform, false);
                var trt = titleGo.GetComponent<RectTransform>();
                trt.sizeDelta = new Vector2(120, 70); 
                
                var tle = titleGo.GetComponent<UnityEngine.UI.LayoutElement>();
                tle.preferredWidth = 120;
                tle.preferredHeight = 70;
            }
            var tmp = titleGo.GetComponent<TextMeshProUGUI>();
            tmp.text = title; tmp.fontSize = 28; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            if (uiFont != null) tmp.font = uiFont;

            var btnCont = row.transform.Find("BtnContainer")?.gameObject;
            if (btnCont == null)
            {
                btnCont = new GameObject("BtnContainer", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup), typeof(UnityEngine.UI.LayoutElement));
                btnCont.transform.SetParent(row.transform, false);
                
                var blg = btnCont.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                blg.childControlHeight = true; blg.childControlWidth = true; // 자식 버튼 크기 강제 제어
                blg.childForceExpandHeight = false; blg.childForceExpandWidth = false;
                blg.spacing = 15;

                var le = btnCont.GetComponent<UnityEngine.UI.LayoutElement>();
                le.flexibleWidth = 1f; 
                le.flexibleHeight = 1f;
            }
            return btnCont.transform;
        }

        private void ToggleOption(CafeMenuOption opt)
        {
            // 같은 카테고리 내에서는 상호 배타적 선택
            if (opt.category != OptionCategoryType.NONE)
                _selectedOptions.RemoveAll(o => o.category == opt.category);

            _selectedOptions.Add(opt);

            // Visual refresh
            foreach (var t in _dynOptToggles)
            {
                if (t.img != null)
                    t.img.color = _selectedOptions.Contains(t.opt) ? COL_OPT_ON : COL_OPT_OFF;
            }
            ValidateOptionSelection();
        }

        private void ValidateOptionSelection()
        {
            // 금액 업데이트
            if (optionPriceLabel != null && _selectedMenuItem != null)
            {
                int price = _selectedMenuItem.basePrice;
                foreach (var o in _selectedOptions) price += o.additionalPrice;
                optionPriceLabel.text = $"가격: {price:N0}원";
            }

            // 모든 제공된 카테고리를 1개씩 선택해야 담기 허용
            bool allSelected = true;
            if (_selectedMenuItem != null && _selectedMenuItem.availableOptions != null)
            {
                var reqCategories = new HashSet<OptionCategoryType>();
                foreach (var o in _selectedMenuItem.availableOptions)
                {
                    if (o.category != OptionCategoryType.NONE) reqCategories.Add(o.category);
                }

                foreach (var reqCat in reqCategories)
                {
                    bool found = false;
                    foreach(var sel in _selectedOptions)
                    {
                        if (sel.category == reqCat) { found = true; break; }
                    }
                    if (!found) { allSelected = false; break; }
                }
            }

            if (addToCartButton != null)
                addToCartButton.interactable = allSelected;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Payment & Finish
        // ═══════════════════════════════════════════════════════════════════
        private IEnumerator CoPayment()
        {
            float duration = 2.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (paymentProgressLabel != null)
                {
                    string dots = new string('.', (int)(elapsed * 4f) % 4);
                    paymentProgressLabel.text = $"결제 처리 중{dots}\n{(duration - elapsed):F1}초";
                }
                yield return null;
            }

            stateManager.TransitionTo(KioskStateManager.KioskState.Finish);
        }

        private void BuildFinishPanel()
        {
            bool success = false;
            if (Mission.MissionManager.Instance != null && Mission.MissionManager.Instance.IsMissionActive)
                success = Mission.MissionManager.Instance.ValidateMission(cartManager.Items);

            if (finishMessageLabel != null)
                finishMessageLabel.text = "주문이 완료되었습니다!";
            if (missionResultLabel != null)
                missionResultLabel.text = Mission.MissionManager.Instance != null && Mission.MissionManager.Instance.IsMissionActive
                    ? (success ? "[성공] 미션 성공!" : "[실패] 미션 실패")
                    : "";
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Button Handlers
        // ═══════════════════════════════════════════════════════════════════
        private void OnAddToCart()
        {
            if (_selectedMenuItem == null || cartManager == null) return;
            cartManager.AddItem(_selectedMenuItem, _selectedOptions);
            stateManager.TransitionTo(KioskStateManager.KioskState.MenuSelect);
        }

        private void OnRestart()
        {
            cartManager?.Clear();
            _currentOrderType = OrderType.None;
            _currentCategory = null;
            stateManager.TransitionTo(KioskStateManager.KioskState.Idle);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  UI Factory & Pooling Helpers
        // ═══════════════════════════════════════════════════════════════════
        private GameObject GetPooledItem(List<GameObject> pool, Transform parent)
        {
            foreach (var item in pool)
            {
                if (!item.activeSelf)
                {
                    item.transform.SetParent(parent, false); // [FIX] 재활용 시 올바른 부모로 재배치 (매우 중요)
                    item.SetActive(true);
                    item.transform.SetAsLastSibling();
                    return item;
                }
            }
            var newGo = new GameObject("PooledItem", typeof(RectTransform));
            newGo.transform.SetParent(parent, false);
            pool.Add(newGo);
            return newGo;
        }

        private void DeactivatePool(List<GameObject> pool)
        {
            foreach (var item in pool)
                item.SetActive(false);
        }

        private void SetupButtonUI(GameObject go, string label, float fontSize, Color bgColor, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            
            var le = go.GetComponent<UnityEngine.UI.LayoutElement>();
            if (le == null) le = go.AddComponent<UnityEngine.UI.LayoutElement>();
            le.preferredWidth = size.x;
            le.preferredHeight = size.y;
            
            var img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();

            if (go.GetComponent<ClickFeedbackHandler>() == null)
                go.AddComponent<ClickFeedbackHandler>();

            var labelGO = go.transform.Find("Label")?.gameObject;
            if (labelGO == null)
            {
                labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGO.transform.SetParent(go.transform, false);
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = new Vector2(8, 4); lrt.offsetMax = new Vector2(-8, -4);
            }

            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = fontSize;
            tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center;
            if (uiFont != null) tmp.font = uiFont;
        }

        private void SetupMenuCardUI(GameObject card, CafeMenuItem item)
        {
            var rrt = card.GetComponent<RectTransform>();
            rrt.sizeDelta = new Vector2(280, 200);

            var img = card.GetComponent<Image>();
            if (img == null) img = card.AddComponent<Image>();
            img.color = COL_CARD_BG;

            var btn = card.GetComponent<Button>();
            if (btn == null) btn = card.AddComponent<Button>();

            if (card.GetComponent<ClickFeedbackHandler>() == null)
                card.AddComponent<ClickFeedbackHandler>();

            // B3: 썸네일 영역 추가
            var thumbGO = card.transform.Find("Thumb")?.gameObject;
            if (thumbGO == null)
            {
                thumbGO = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                thumbGO.transform.SetParent(card.transform, false);
                var trt = thumbGO.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0.5f, 1); trt.anchorMax = new Vector2(0.5f, 1);
                trt.pivot = new Vector2(0.5f, 1);
                trt.sizeDelta = new Vector2(100, 100);
                trt.anchoredPosition = new Vector2(0, -10);
            }
            var tImg = thumbGO.GetComponent<Image>();
            if (item.thumbnail != null) { tImg.sprite = item.thumbnail; tImg.color = Color.white; }
            else { tImg.color = new Color(0.3f, 0.3f, 0.35f); } // placeholder grey

            // Name
            var nameGO = card.transform.Find("Name")?.gameObject;
            if (nameGO == null)
            {
                nameGO = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
                nameGO.transform.SetParent(card.transform, false);
                var nrt = nameGO.GetComponent<RectTransform>();
                nrt.anchorMin = new Vector2(0, 0.25f); nrt.anchorMax = new Vector2(1, 0.45f);
                nrt.offsetMin = new Vector2(5, 0); nrt.offsetMax = new Vector2(-5, 0);
            }
            var ntmp = nameGO.GetComponent<TextMeshProUGUI>();
            ntmp.text = item.menuName; ntmp.fontSize = 28; ntmp.color = Color.white;
            ntmp.alignment = TextAlignmentOptions.Center;
            if (uiFont != null) ntmp.font = uiFont;

            // Price
            var priceGO = card.transform.Find("Price")?.gameObject;
            if (priceGO == null)
            {
                priceGO = new GameObject("Price", typeof(RectTransform), typeof(TextMeshProUGUI));
                priceGO.transform.SetParent(card.transform, false);
                var prt = priceGO.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0, 0); prt.anchorMax = new Vector2(1, 0.25f);
                prt.offsetMin = new Vector2(5, 0); prt.offsetMax = new Vector2(-5, 0);
            }
            var ptmp = priceGO.GetComponent<TextMeshProUGUI>();
            ptmp.text = $"{item.basePrice:N0}원"; ptmp.fontSize = 24;
            ptmp.color = new Color(0.7f, 0.85f, 1f); ptmp.alignment = TextAlignmentOptions.Center;
            if (uiFont != null) ptmp.font = uiFont;
        }

        private void SetupCartRowUI(GameObject row, string text, int qty, int cartIndex)
        {
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(940, 70); // 스크롤바(25px) 및 여백과 겹치지 않도록 원래 1000에서 940으로 축소
            
            var img = row.GetComponent<Image>();
            if (img == null) img = row.AddComponent<Image>();
            img.color = COL_CART_ROW;

            // Text Label
            var labelGO = row.transform.Find("Label")?.gameObject;
            if (labelGO == null)
            {
                labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelGO.transform.SetParent(row.transform, false);
                var lrt = labelGO.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(0.6f, 1);
                lrt.offsetMin = new Vector2(20, 0); lrt.offsetMax = Vector2.zero;
            }
            var tmp = labelGO.GetComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = 26; tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            if (uiFont != null) tmp.font = uiFont;

            // Quantiy controls (C5)
            CreateQtyButton(row, "BtnMinus", "-", new Vector2(0.65f, 0.5f), () => {
                cartManager.AdjustQuantity(cartIndex, -1);
            });
            var qtyGO = row.transform.Find("QtyText")?.gameObject;
            if (qtyGO == null)
            {
                qtyGO = new GameObject("QtyText", typeof(RectTransform), typeof(TextMeshProUGUI));
                qtyGO.transform.SetParent(row.transform, false);
                var qrt = qtyGO.GetComponent<RectTransform>();
                qrt.anchorMin = qrt.anchorMax = new Vector2(0.72f, 0.5f);
                qrt.sizeDelta = new Vector2(50, 50);
            }
            var qtmp = qtyGO.GetComponent<TextMeshProUGUI>();
            qtmp.text = qty.ToString(); qtmp.fontSize = 28; qtmp.color = Color.white;
            qtmp.alignment = TextAlignmentOptions.Center;
            if (uiFont != null) qtmp.font = uiFont;

            CreateQtyButton(row, "BtnPlus", "+", new Vector2(0.79f, 0.5f), () => {
                cartManager.AdjustQuantity(cartIndex, 1);
            });

            // Delete Button
            CreateQtyButton(row, "BtnDel", "X", new Vector2(0.92f, 0.5f), () => {
                cartManager.RemoveAt(cartIndex);
            }, new Color(0.8f, 0.3f, 0.3f));
        }

        private void CreateQtyButton(GameObject parent, string name, string label, Vector2 anchorPivot, UnityEngine.Events.UnityAction action, Color? color = null)
        {
            var btnGo = parent.transform.Find(name)?.gameObject;
            if (btnGo == null)
            {
                btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(ClickFeedbackHandler));
                btnGo.transform.SetParent(parent.transform, false);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = anchorPivot;
                rt.sizeDelta = new Vector2(50, 50);

                var txtGo = new GameObject("txt", typeof(RectTransform), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(btnGo.transform, false);
                var trt = txtGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
            }

            btnGo.GetComponent<Image>().color = color ?? new Color(0.3f, 0.3f, 0.3f);
            var btn = btnGo.GetComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);

            var t = btnGo.GetComponentInChildren<TextMeshProUGUI>();
            t.text = label; t.fontSize = 24; t.color = Color.white; t.alignment = TextAlignmentOptions.Center;
            if (uiFont != null) t.font = uiFont;
        }
    }

    /// <summary>
    /// 마우스 휠 스크롤 이벤트를 완전히 무시(삭제)하는 XR 전용 커스텀 ScrollRect
    /// </summary>
    public class XRNoWheelScrollRect : UnityEngine.UI.ScrollRect
    {
        public override void OnScroll(UnityEngine.EventSystems.PointerEventData data)
        {
            // 마우스 휠 이벤트가 들어와도 부모 로직을 호출하지 않고 소멸시킴 (스크립트 로직 삭제)
        }
    }
}
