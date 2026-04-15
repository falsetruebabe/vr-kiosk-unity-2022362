using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Phase2.Core
{
    /// <summary>
    /// 키오스크 화면 흐름을 제어하는 유한 상태 머신 (FSM).
    /// CanvasGroup.alpha로 패널 전환 – SetActive 호출 지양 원칙 준수.
    /// FEAT-03: 코루틴 기반 페이드 애니메이션 (0.25초) 적용.
    /// </summary>
    public class KioskStateManager : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        //  Singleton
        // -----------------------------------------------------------------------
        public static KioskStateManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        // -----------------------------------------------------------------------
        //  State Definition
        // -----------------------------------------------------------------------
        public enum KioskState
        {
            Idle,
            MenuSelect,
            OptionSelect,
            CartReview,           // Phase 3 확장용 (현재 미사용)
            PaymentProcessing,
            Finish
        }

        // -----------------------------------------------------------------------
        //  Events
        // -----------------------------------------------------------------------
        public static event System.Action<KioskState> OnStateChanged;

        // BUG-06: 씬 전환 시 정적 이벤트 구독 정리 (싱글톤 인스턴스만 정리)
        private void OnDestroy()
        {
            if (Instance == this)
            {
                OnStateChanged = null;
                Instance = null;
            }
        }

        // -----------------------------------------------------------------------
        //  Serialized Panel References
        // -----------------------------------------------------------------------
        [Header("Panel CanvasGroups (World Space Canvas 하위 패널)")]
        [SerializeField] private CanvasGroup startPanelGroup;
        [SerializeField] private CanvasGroup menuPanelGroup;
        [SerializeField] private CanvasGroup optionPopupGroup;
        [SerializeField] private CanvasGroup optionDimGroup; // A-4: 모달 Dim 오버레이
        [SerializeField] private CanvasGroup cartReviewGroup;
        [SerializeField] private CanvasGroup paymentGroup;
        [SerializeField] private CanvasGroup finishGroup;

        [Header("Fade Settings")]
        [SerializeField, Tooltip("패널 전환 시 페이드 소요 시간 (초)")]
        private float fadeDuration = 0.25f;

        // -----------------------------------------------------------------------
        //  Runtime State
        // -----------------------------------------------------------------------
        private KioskState _currentState = KioskState.Idle;
        public KioskState CurrentState => _currentState;

        // FEAT-03: 코루틴 중첩 방지용 추적 (위험 3-A 대응)
        private readonly Dictionary<CanvasGroup, Coroutine> _activeFades
            = new Dictionary<CanvasGroup, Coroutine>();

        // -----------------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------------
        private void Start()
        {
            // 기본값 Idle이므로 TransitionTo의 동등성 체크를 우회하여 직접 초기화
            // 초기화는 즉시 전환 (페이드 없이)
            ApplyPanelVisibility(instant: true);
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
            ApplyPanelVisibility(instant: false);
            OnStateChanged?.Invoke(_currentState);

            Debug.Log($"[KioskFSM] → {_currentState}");
        }

        // -----------------------------------------------------------------------
        //  Private Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// CanvasGroup.alpha + interactable + blocksRaycasts 를 함께 전환하여
        /// SetActive 없이 패널 표시/숨김을 제어한다.
        /// instant=true이면 즉시 전환, false이면 페이드 애니메이션.
        /// </summary>
        private void ApplyPanelVisibility(bool instant)
        {
            SetGroup(startPanelGroup,   _currentState == KioskState.Idle,               instant);
            SetGroup(menuPanelGroup,    _currentState == KioskState.MenuSelect,          instant);
            SetGroup(optionPopupGroup,  _currentState == KioskState.OptionSelect,        instant);
            SetGroup(optionDimGroup,    _currentState == KioskState.OptionSelect,        instant); // A-4: Dim 동기화
            SetGroup(cartReviewGroup,   _currentState == KioskState.CartReview,          instant);
            SetGroup(paymentGroup,      _currentState == KioskState.PaymentProcessing,   instant);
            SetGroup(finishGroup,       _currentState == KioskState.Finish,              instant);
        }

        private void SetGroup(CanvasGroup group, bool visible, bool instant)
        {
            if (group == null) return;

            // interactable/blocksRaycasts는 항상 즉시 전환 (입력 차단은 즉각 반영)
            group.interactable = visible;
            group.blocksRaycasts = visible;

            if (instant)
            {
                // 초기화 시 즉시 전환 (페이드 없이)
                group.alpha = visible ? 1f : 0f;
            }
            else
            {
                // 기존 페이드 코루틴 중단 (위험 3-A 대응: 빠른 전환 시 중첩 방지)
                if (_activeFades.TryGetValue(group, out var running) && running != null)
                    StopCoroutine(running);

                _activeFades[group] = StartCoroutine(CoFadeGroup(group, visible ? 1f : 0f));
            }
        }

        private IEnumerator CoFadeGroup(CanvasGroup group, float target)
        {
            float start = group.alpha;
            if (Mathf.Approximately(start, target))
            {
                group.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                group.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
                yield return null;
            }
            group.alpha = target;
        }
    }
}
