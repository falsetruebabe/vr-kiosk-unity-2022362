using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Phase2.Data;
using Common;

namespace Phase2.Core
{
    public enum OrderType { None, DineIn, TakeOut }

    /// <summary>
    /// Phase 2 키오스크 UI 중앙 컨트롤러 (슬림 버전).
    /// BUG-05: 기능별 서브 컨트롤러에 위임하여 단일 책임 원칙(SRP) 준수.
    /// 
    /// 분리된 기능:
    ///   - MenuPanelController:  카테고리 탭 + 메뉴 카드 렌더링
    ///   - OptionPanelController: 옵션 선택 + 검증
    ///   - CartUIRenderer:       인라인 장바구니 + 스크롤뷰
    ///   - FinishPanelController: 결제 + 영수증
    /// </summary>
    public class KioskUIController : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════
        //  Serialized Fields (Inspector 참조 — 기존과 완전 동일)
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
        [SerializeField] private TextMeshProUGUI receiptLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button backToDifficultyButton;

        [Header("Back Button (주문 진행 중 뒤로가기)")]
        [SerializeField] private Button backDuringOrderButton;

        [Header("Confirm Popup (주문 취소 확인)")]
        [SerializeField] private CanvasGroup confirmPopupGroup;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        // ═══════════════════════════════════════════════════════════════════
        //  Sub-Controllers (BUG-05: 기능별 분리)
        // ═══════════════════════════════════════════════════════════════════
        private MenuPanelController _menuController;
        private OptionPanelController _optionController;
        private CartUIRenderer _cartRenderer;
        private FinishPanelController _finishController;
        private ConfirmPopupController _confirmPopup;

        // ═══════════════════════════════════════════════════════════════════
        //  Runtime State
        // ═══════════════════════════════════════════════════════════════════
        private OrderType _currentOrderType = OrderType.None;
        private CafeMenuItem _selectedMenuItem;

        // ═══════════════════════════════════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════════════════════════════════
        private void Awake()
        {
            // Start Panel 버튼
            dineInButton?.onClick.AddListener(() => StartOrder(OrderType.DineIn));
            takeOutButton?.onClick.AddListener(() => StartOrder(OrderType.TakeOut));
            restartButton?.onClick.AddListener(OnRestart);
            backToDifficultyButton?.onClick.AddListener(BackToDifficulty);

            // 주문 진행 중 뒤로가기 + 확인 팝업
            _confirmPopup = new ConfirmPopupController(confirmPopupGroup, confirmYesButton, confirmNoButton);
            backDuringOrderButton?.onClick.AddListener(() =>
            {
                if (stateManager.CurrentState == KioskStateManager.KioskState.MenuSelect ||
                    stateManager.CurrentState == KioskStateManager.KioskState.OptionSelect)
                {
                    _confirmPopup.Show(() =>
                    {
                        SceneManager.LoadScene("Scene_DifficultySelect");
                    });
                }
                else if (stateManager.CurrentState == KioskStateManager.KioskState.Idle)
                {
                    // 아무것도 담지 않은 초기 상태에서는 팝업 없이 바로 뒤로가기
                    SceneManager.LoadScene("Scene_DifficultySelect");
                }
            });

            // Sub-Controller 생성
            _menuController = new MenuPanelController(
                menuDatabase, uiFont, categoryTabContainer, menuCardContainer,
                onMenuItemSelected: (item) =>
                {
                    _selectedMenuItem = item;
                    stateManager.TransitionTo(KioskStateManager.KioskState.OptionSelect);
                });

            _optionController = new OptionPanelController(
                uiFont, optionMenuNameLabel, optionPriceLabel, optionButtonContainer,
                addToCartButton, cancelOptionButton,
                onAddToCart: (item, options) =>
                {
                    cartManager.AddItem(item, options);
                    stateManager.TransitionTo(KioskStateManager.KioskState.MenuSelect);
                },
                onCancel: () =>
                {
                    stateManager.TransitionTo(KioskStateManager.KioskState.MenuSelect);
                });

            _cartRenderer = new CartUIRenderer(
                cartManager, uiFont, inlineCartListContainer, inlineTotalPriceLabel, payButton);
            _cartRenderer.SetupScrollView();

            _finishController = new FinishPanelController(
                this, paymentProgressLabel, finishMessageLabel, missionResultLabel,
                receiptLabel, cartManager,
                onPaymentComplete: () =>
                {
                    stateManager.TransitionTo(KioskStateManager.KioskState.Finish);
                });

            // Pay 버튼
            payButton?.onClick.AddListener(() =>
                stateManager.TransitionTo(KioskStateManager.KioskState.PaymentProcessing));
        }

        private void Start()
        {
            AssignAutoMission();
        }

        private void OnEnable()
        {
            KioskStateManager.OnStateChanged += OnStateChanged;
            CartManager.OnCartChanged += _cartRenderer.Refresh;
        }

        private void OnDisable()
        {
            KioskStateManager.OnStateChanged -= OnStateChanged;
            CartManager.OnCartChanged -= _cartRenderer.Refresh;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  State Dispatch (BUG-09: 상태 전환 시 코루틴 안전 중단)
        // ═══════════════════════════════════════════════════════════════════
        private void OnStateChanged(KioskStateManager.KioskState state)
        {
            // BUG-09: PaymentProcessing을 벗어날 때 결제 코루틴 안전 중단
            if (state != KioskStateManager.KioskState.PaymentProcessing)
                _finishController?.StopPayment();

            // UX FIX: 결제 중이거나 결제 완료(영수증) 화면에서는 뒤로가기 버튼 숨김
            if (backDuringOrderButton != null)
            {
                bool showBackBtn = (state == KioskStateManager.KioskState.Idle || 
                                    state == KioskStateManager.KioskState.MenuSelect || 
                                    state == KioskStateManager.KioskState.OptionSelect);
                backDuringOrderButton.gameObject.SetActive(showBackBtn);
            }

            switch (state)
            {
                case KioskStateManager.KioskState.MenuSelect:
                    _menuController.Build();
                    _cartRenderer.Refresh();
                    break;
                case KioskStateManager.KioskState.OptionSelect:
                    _optionController.Build(_selectedMenuItem);
                    break;
                case KioskStateManager.KioskState.PaymentProcessing:
                    _finishController.StartPayment();
                    break;
                case KioskStateManager.KioskState.Finish:
                    _finishController.BuildFinish(_currentOrderType);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Handlers
        // ═══════════════════════════════════════════════════════════════════
        private void StartOrder(OrderType type)
        {
            _currentOrderType = type;
            stateManager.TransitionTo(KioskStateManager.KioskState.MenuSelect);
        }

        /// <summary>"다시 주문하기" — 인씬 리셋 (씬 전환 없이 처음 상태로)</summary>
        private void OnRestart()
        {
            cartManager?.Clear();
            _currentOrderType = OrderType.None;
            _menuController.ResetCategory();

            // FEAT-02: 새 미션 자동 생성
            if (Mission.MissionManager.Instance != null && menuDatabase != null)
                Mission.MissionManager.Instance.GenerateRandomMission(menuDatabase);

            stateManager.TransitionTo(KioskStateManager.KioskState.Idle);
        }

        /// <summary>"난이도 선택 화면으로" — 씬 전환</summary>
        private void BackToDifficulty()
        {
            SceneManager.LoadScene("Scene_DifficultySelect");
        }

        private void AssignAutoMission()
        {
            // 다중 복합 미션 시스템: 음료+디저트 무작위 조합 할당
            if (Mission.MissionManager.Instance != null && menuDatabase != null)
            {
                Mission.MissionManager.Instance.GenerateRandomMission(menuDatabase);
            }
        }
    }
}
