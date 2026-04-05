using UnityEngine;

namespace Phase2.Core
{
    /// <summary>
    /// 키오스크 화면 흐름을 제어하는 유한 상태 머신 (FSM).
    /// CanvasGroup.alpha로 패널 전환 – SetActive 호출 지양 원칙 준수.
    /// </summary>
    public class KioskStateManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        //  State Definition
        // -----------------------------------------------------------------------
        public enum KioskState
        {
            Idle,
            MenuSelect,
            OptionSelect,
            CartReview,
            PaymentProcessing,
            Finish
        }

        // -----------------------------------------------------------------------
        //  Events
        // -----------------------------------------------------------------------
        public static event System.Action<KioskState> OnStateChanged;

        // -----------------------------------------------------------------------
        //  Serialized Panel References
        // -----------------------------------------------------------------------
        [Header("Panel CanvasGroups (World Space Canvas 하위 패널)")]
        [SerializeField] private CanvasGroup startPanelGroup;
        [SerializeField] private CanvasGroup menuPanelGroup;
        [SerializeField] private CanvasGroup optionPopupGroup;
        [SerializeField] private CanvasGroup cartReviewGroup;
        [SerializeField] private CanvasGroup paymentGroup;
        [SerializeField] private CanvasGroup finishGroup;

        // -----------------------------------------------------------------------
        //  Runtime State
        // -----------------------------------------------------------------------
        private KioskState _currentState = KioskState.Idle;
        public KioskState CurrentState => _currentState;

        // -----------------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------------
        private void Start()
        {
            // 기본값 Idle이므로 TransitionTo의 동등성 체크를 우회하여 직접 초기화
            ApplyPanelVisibility();
            OnStateChanged?.Invoke(_currentState);
            Debug.Log($"[KioskFSM] Initial → {_currentState}");
        }

        // -----------------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------------
        public void TransitionTo(KioskState nextState)
        {
            if (_currentState == nextState) return;

            _currentState = nextState;
            ApplyPanelVisibility();
            OnStateChanged?.Invoke(_currentState);

            Debug.Log($"[KioskFSM] → {_currentState}");
        }

        // -----------------------------------------------------------------------
        //  Private Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// CanvasGroup.alpha + interactable + blocksRaycasts 를 함께 전환하여
        /// SetActive 없이 패널 표시/숨김을 제어한다.
        /// </summary>
        private void ApplyPanelVisibility()
        {
            SetGroup(startPanelGroup,   _currentState == KioskState.Idle);
            SetGroup(menuPanelGroup,    _currentState == KioskState.MenuSelect);
            SetGroup(optionPopupGroup,  _currentState == KioskState.OptionSelect);
            SetGroup(cartReviewGroup,   _currentState == KioskState.CartReview);
            SetGroup(paymentGroup,      _currentState == KioskState.PaymentProcessing);
            SetGroup(finishGroup,       _currentState == KioskState.Finish);
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
