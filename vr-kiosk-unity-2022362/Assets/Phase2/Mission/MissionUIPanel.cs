using TMPro;
using UnityEngine;

namespace Phase2.Mission
{
    /// <summary>
    /// 현재 미션 텍스트를 플레이어가 항상 확인할 수 있도록 표시하는 UI 패널.
    /// - Screen Space - Overlay 캔버스의 상단에 배치 (항상 화면에 고정)
    /// - MissionManager.OnMissionTextUpdated 이벤트를 구독하여 텍스트 갱신
    /// - CanvasGroup.alpha로 표시/숨김 제어 (SetActive 지양)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MissionUIPanel : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        //  Serialized References
        // -----------------------------------------------------------------------
        [Header("텍스트 컴포넌트")]
        [SerializeField] private TextMeshProUGUI missionLabel;

        [Header("표시 설정")]
        [SerializeField, Tooltip("미션이 없을 때 패널을 숨길지 여부")]
        private bool hideWhenNoMission = true;

        // -----------------------------------------------------------------------
        //  Cached Components
        // -----------------------------------------------------------------------
        private CanvasGroup _canvasGroup;

        // -----------------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------------
        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            MissionManager.OnMissionTextUpdated += HandleMissionTextUpdated;
            MissionManager.OnMissionValidated   += HandleMissionValidated;
        }

        private void OnDisable()
        {
            MissionManager.OnMissionTextUpdated -= HandleMissionTextUpdated;
            MissionManager.OnMissionValidated   -= HandleMissionValidated;
        }

        private void Start()
        {
            if (MissionManager.Instance != null && MissionManager.Instance.IsMissionActive)
            {
                HandleMissionTextUpdated(MissionManager.Instance.CurrentMissionText);
            }
            else if (hideWhenNoMission)
            {
                SetPanelVisible(false);
            }
        }

        // -----------------------------------------------------------------------
        //  Event Handlers
        // -----------------------------------------------------------------------
        private void HandleMissionTextUpdated(string missionText)
        {
            if (missionLabel != null)
                missionLabel.text = missionText;

            SetPanelVisible(true);
        }

        private void HandleMissionValidated(bool success)
        {
            // 결과 텍스트를 잠시 표시한 뒤 숨기거나 갱신
            if (missionLabel != null)
                missionLabel.text = success
                    ? "[성공] 미션 달성! 잘 하셨습니다."
                    : "[실패] 미션 실패. 다시 도전해보세요.";
        }

        // -----------------------------------------------------------------------
        //  Private Helpers
        // -----------------------------------------------------------------------
        private void SetPanelVisible(bool visible)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }
    }
}
