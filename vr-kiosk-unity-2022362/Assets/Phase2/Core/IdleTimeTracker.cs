using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

namespace Phase2.Core
{
    /// <summary>
    /// Phase 2 전용 지능형 유휴 감지기.
    /// 1) 10초 유휴 → 힌트 안내 문구 페이드인
    /// 2) 키오스크 WorldSpace UI 클릭 시 안내문 닫힘 + 노란색 [힌트] 버튼 페이드인
    /// 3) [힌트] 버튼 → 미션 기반 동적 힌트 상세 팝업 표시
    /// 4) 주문 완료 시 일괄 비활성화
    /// </summary>
    public class IdleTimeTracker : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        //  Configuration
        // -----------------------------------------------------------------------
        [Header("Idle Detection")]
        [SerializeField, Tooltip("힌트를 표시하기까지의 유휴 시간 (초)")]
        private float idleThresholdSeconds = 10f;

        [Header("Hint Message Panel (10초 유휴 시 표시되는 안내 문구)")]
        [SerializeField] private CanvasGroup hintPanelGroup;

        [Header("Hint Button (노란색 힌트 버튼 - 키오스크 World Canvas 내)")]
        [SerializeField] private CanvasGroup hintButtonGroup;
        [SerializeField] private Button hintButton;

        [Header("Hint Detail Popup (메뉴 위치 안내 팝업 - 키오스크 World Canvas 내)")]
        [SerializeField] private CanvasGroup hintDetailGroup;
        [SerializeField] private TextMeshProUGUI hintDetailLabel;
        [SerializeField] private Button hintDetailCloseButton;

        [Header("Fade Settings")]
        [SerializeField] private float fadeInSpeed = 2f;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioClip idleVoiceClip;

        // -----------------------------------------------------------------------
        //  Runtime State
        // -----------------------------------------------------------------------
        private float _idleTimer;
        private bool _hintMessageVisible;    // 안내 문구 표시 중
        private bool _hintButtonActivated;   // 힌트 버튼 활성화됨 (미션 완료까지 유지)
        private bool _hintDetailVisible;     // 힌트 상세 팝업 표시 중
        private AudioSource _audioSource;
        private bool _kioskClicked;          // 이번 프레임에 키오스크 UI 클릭 감지됨

        // -----------------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------------
        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            ResetAll();
            KioskStateManager.OnStateChanged += OnKioskStateChanged;
        }

        private void OnDisable()
        {
            KioskStateManager.OnStateChanged -= OnKioskStateChanged;
        }

        private void Start()
        {
            // 버튼 이벤트 연결
            if (hintButton != null)
                hintButton.onClick.AddListener(OnHintButtonClicked);
            if (hintDetailCloseButton != null)
                hintDetailCloseButton.onClick.AddListener(OnHintDetailClose);
        }

        private void Update()
        {
            // 키오스크 UI 클릭 감지 (GraphicRaycaster 기반)
            _kioskClicked = false;
            if (Input.GetMouseButtonDown(0))
            {
                // Raycast를 통해 WorldSpace Canvas 위의 UI를 클릭했는지 확인
                var pointerData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
                pointerData.position = Input.mousePosition;
                var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointerData, results);
                
                foreach (var r in results)
                {
                    // World Space Canvas 하위 UI에 히트했는지 확인
                    Canvas canvas = r.gameObject.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                    {
                        _kioskClicked = true;
                        break;
                    }
                }
            }

            // 키오스크 UI 클릭 시에만 타이머 리셋 & 안내 문구 닫힘
            if (_kioskClicked)
            {
                ResetTimer();
                if (_hintMessageVisible) DismissHintMessage();
            }

            // 유휴 타이머 증가 (키오스크 클릭이 없으면)
            if (!_kioskClicked)
                _idleTimer += Time.deltaTime;

            // 10초 경과 → 안내 문구 표시
            if (!_hintMessageVisible && !_hintButtonActivated && _idleTimer >= idleThresholdSeconds)
                ShowHintMessage();

            // 페이드인 애니메이션
            if (_hintMessageVisible && hintPanelGroup != null && hintPanelGroup.alpha < 1f)
                hintPanelGroup.alpha = Mathf.MoveTowards(hintPanelGroup.alpha, 1f, fadeInSpeed * Time.deltaTime);

            if (_hintButtonActivated && hintButtonGroup != null && hintButtonGroup.alpha < 1f)
                hintButtonGroup.alpha = Mathf.MoveTowards(hintButtonGroup.alpha, 1f, fadeInSpeed * Time.deltaTime);
        }

        // -----------------------------------------------------------------------
        //  Public API
        // -----------------------------------------------------------------------
        public void ResetTimer()
        {
            _idleTimer = 0f;
        }

        /// <summary>주문 완료(Finish) 시 호출하여 모든 힌트 UI를 일괄 비활성화.</summary>
        public void DeactivateAllHints()
        {
            ResetAll();
        }

        // -----------------------------------------------------------------------
        //  State Change Listener
        // -----------------------------------------------------------------------
        private void OnKioskStateChanged(KioskStateManager.KioskState state)
        {
            if (state == KioskStateManager.KioskState.Finish || 
                state == KioskStateManager.KioskState.Idle ||
                state == KioskStateManager.KioskState.PaymentProcessing ||
                state == KioskStateManager.KioskState.CartReview)
            {
                ResetAll();
            }
        }

        // -----------------------------------------------------------------------
        //  Hint Message (안내 문구)
        // -----------------------------------------------------------------------
        private void ShowHintMessage()
        {
            _hintMessageVisible = true;
            if (hintPanelGroup != null)
            {
                hintPanelGroup.interactable = true;
                hintPanelGroup.blocksRaycasts = true;
            }

            if (_audioSource != null && idleVoiceClip != null)
                _audioSource.PlayOneShot(idleVoiceClip);

            Debug.Log("[IdleTimeTracker] 유휴 감지 → 안내 문구 표시.");
        }

        /// <summary>안내 문구를 닫고, 노란색 힌트 버튼을 활성화합니다.</summary>
        private void DismissHintMessage()
        {
            _hintMessageVisible = false;
            SetGroupVisible(hintPanelGroup, false);

            // 힌트 버튼 활성화 (미션 완료까지 유지)
            _hintButtonActivated = true;
            if (hintButtonGroup != null)
            {
                hintButtonGroup.alpha = 0f; // 페이드인 시작점
                hintButtonGroup.interactable = true;
                hintButtonGroup.blocksRaycasts = true;
            }

            Debug.Log("[IdleTimeTracker] 안내 문구 닫힘 → 힌트 버튼 활성화.");
        }

        // -----------------------------------------------------------------------
        //  Hint Button & Detail Popup
        // -----------------------------------------------------------------------
        private void OnHintButtonClicked()
        {
            if (_hintDetailVisible)
            {
                OnHintDetailClose();
                return;
            }

            // 미션 기반 동적 힌트 텍스트 생성
            string hintText = BuildHintLocationText();
            if (hintDetailLabel != null)
                hintDetailLabel.text = hintText;

            _hintDetailVisible = true;
            SetGroupVisible(hintDetailGroup, true);

            Debug.Log("[IdleTimeTracker] 힌트 상세 팝업 열림.");
        }

        private void OnHintDetailClose()
        {
            _hintDetailVisible = false;
            SetGroupVisible(hintDetailGroup, false);
            Debug.Log("[IdleTimeTracker] 힌트 상세 팝업 닫힘.");
        }

        // -----------------------------------------------------------------------
        //  Dynamic Hint Text Builder
        // -----------------------------------------------------------------------
        private string BuildHintLocationText()
        {
            if (Mission.MissionManager.Instance == null || !Mission.MissionManager.Instance.IsMissionActive)
                return "현재 활성화된 미션이 없습니다.";

            var missions = Mission.MissionManager.Instance.GetMissionTargets();
            var db = Mission.MissionManager.Instance.GetDatabase();
            
            if (missions == null || missions.Count == 0)
                return "미션 정보를 불러올 수 없습니다.";

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < missions.Count; i++)
            {
                var t = missions[i];
                string tabName = FindCategoryTab(db, t.MenuItem);
                sb.Append($"{t.MenuItem.menuName}");
                sb.Append(HasJongseong(t.MenuItem.menuName[t.MenuItem.menuName.Length - 1]) ? "은 " : "는 ");
                sb.Append($"[{tabName}] 탭에");

                if (i < missions.Count - 1)
                    sb.Append(",\n");
                else
                    sb.Append(" 있습니다!");
            }
            return sb.ToString();
        }

        private string FindCategoryTab(Data.CafeMenuDatabase db, Data.CafeMenuItem item)
        {
            if (db == null) return item.category ?? "알 수 없음";
            foreach (var cat in db.categories)
            {
                foreach (var menuItem in cat.items)
                {
                    if (menuItem.menuId == item.menuId)
                        return cat.categoryName;
                }
            }
            return item.category ?? "알 수 없음";
        }

        private bool HasJongseong(char c)
        {
            if (c >= 0xAC00 && c <= 0xD7A3) return (c - 0xAC00) % 28 > 0;
            return false;
        }

        // -----------------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------------
        private void ResetAll()
        {
            _idleTimer = 0f;
            _hintMessageVisible = false;
            _hintButtonActivated = false;
            _hintDetailVisible = false;

            SetGroupVisible(hintPanelGroup, false);
            SetGroupVisible(hintButtonGroup, false);
            SetGroupVisible(hintDetailGroup, false);
        }

        private void SetGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
