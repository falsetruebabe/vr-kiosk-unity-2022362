using UnityEngine;

namespace Phase2.Desktop
{
    /// <summary>
    /// HMD 없이 에디터/데스크톱 환경에서 키오스크를 테스트하기 위한 1인칭 컨트롤러.
    ///
    /// 동작 조건:
    ///   - XR 기기가 연결되지 않은 경우에만 활성화 (XRSettings.isDeviceActive 분기)
    ///   - W/A/S/D : 전후좌우 이동
    ///   - 마우스 Right-Button 홀드 + 마우스 이동 : 시점 회전 (우클릭 홀드 방식으로 UI 클릭 방해 없음)
    ///   - Escape 키 : 커서 잠금 해제
    ///
    /// 성능 규칙:
    ///   - Update() 내 new / GetComponent 호출 없음
    /// </summary>
    public class DesktopFPSController : MonoBehaviour
    {
        // -----------------------------------------------------------------------
        //  Configuration
        // -----------------------------------------------------------------------
        [Header("Movement")]
        [SerializeField, Tooltip("이동 속도 (m/s)")]
        private float moveSpeed = 3f;

        [Header("Mouse Look")]
        [SerializeField, Tooltip("마우스 회전 감도")]
        private float mouseSensitivity = 2f;

        [SerializeField, Tooltip("수직 시야각 상하 제한 (도)")]
        private float verticalClampAngle = 80f;

        [SerializeField, Tooltip("마우스 오른쪽 버튼을 누른 상태에서만 시점 회전 여부")]
        private bool requireRightClickToLook = false; // 기본값 false로 변경 (마인크래프트 방식)

        // -----------------------------------------------------------------------
        //  Cached State (Update 내 GC 방지)
        // -----------------------------------------------------------------------
        private float _yaw;       // 수평 회전 (Y축)
        private float _pitch;     // 수직 회전 (X축)
        private Transform _transform;

        // -----------------------------------------------------------------------
        //  UI Interaction State (Hover/Click 우회 처리용)
        // -----------------------------------------------------------------------
        private GameObject _lastHoveredUI;

        // GC-Free: PointerEventData 캐싱 (매 프레임 new 호출 방지)
        private UnityEngine.EventSystems.PointerEventData _cachedPointerData;
        private readonly System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> _cachedRayResults
            = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>(8);

        // -----------------------------------------------------------------------
        //  Lifecycle
        // -----------------------------------------------------------------------
        private void Awake()
        {
            _transform = transform;

            // VR 디바이스가 활성 상태이면 이 컨트롤러 자체를 비활성화
#if UNITY_XR_MANAGEMENT
            CheckXRAndDisable();
#endif
        }

        private void Start()
        {
            Vector3 euler = _transform.eulerAngles;
            _yaw   = euler.y;
            _pitch = euler.x;

            // 커서 숨기고 화면 중앙에 잠금
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            HandleMouseLook();

            // 커서가 잠겨있을 때만 중앙 십자선 상호작용 활성화
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                HandleUICrosshairInteraction(); // 심층 분석: 커서 잠금 시 UI 상호작용 강제 처리
            }

            // ESC 키 입력 시 커서 잠금 해제 (에디터 에스케이프)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // 커서가 풀린 상태에서 화면 좌클릭 시 다시 게임으로 포커스(잠금)
            if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void HandleUICrosshairInteraction()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null) return;

            // GC-Free: 캐싱된 PointerEventData 재사용
            if (_cachedPointerData == null)
                _cachedPointerData = new UnityEngine.EventSystems.PointerEventData(eventSystem);
            _cachedPointerData.position = new Vector2(Screen.width / 2f, Screen.height / 2f);

            _cachedRayResults.Clear();
            eventSystem.RaycastAll(_cachedPointerData, _cachedRayResults);

            // 첫 번째 유효한 UI GameObject 찾기
            GameObject currentHovered = _cachedRayResults.Count > 0 ? _cachedRayResults[0].gameObject : null;

            // 1. 호버(Hover) 상태 처리: PointerEnter / PointerExit 강제 발생
            if (_lastHoveredUI != currentHovered)
            {
                if (_lastHoveredUI != null)
                {
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(_lastHoveredUI, _cachedPointerData, UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
                }
                if (currentHovered != null)
                {
                    UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(currentHovered, _cachedPointerData, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
                }
                _lastHoveredUI = currentHovered;
            }

            // 2. 클릭(Click) 이벤트 처리: PointerDown, PointerUp, PointerClick 강제 발생
            if (Input.GetMouseButtonDown(0) && currentHovered != null)
            {
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(currentHovered, _cachedPointerData, UnityEngine.EventSystems.ExecuteEvents.pointerDownHandler);
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(currentHovered, _cachedPointerData, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            }
            if (Input.GetMouseButtonUp(0) && currentHovered != null)
            {
                UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(currentHovered, _cachedPointerData, UnityEngine.EventSystems.ExecuteEvents.pointerUpHandler);
            }
        }

        private void OnGUI()
        {
            // 화면 중앙에 십자선(점) 그리기
            float size = 8f; // 1.5배 확대 (5f → 8f)
            float x = (Screen.width - size) / 2f;
            float y = (Screen.height - size) / 2f;
            
            // 라이트 테마 대비: 검은색 조준점
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // -----------------------------------------------------------------------
        //  Private Handlers
        // -----------------------------------------------------------------------
        private void HandleMouseLook()
        {
            // 커서가 잠금 해제된 상태(에디터 조작 등)에서는 시점 회전 중단
            if (Cursor.lockState != CursorLockMode.Locked) return;

            bool canLook = !requireRightClickToLook || Input.GetMouseButton(1);
            if (!canLook) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            _yaw   += mouseX;
            _pitch -= mouseY;
            _pitch  = Mathf.Clamp(_pitch, -verticalClampAngle, verticalClampAngle);

            _transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        // -----------------------------------------------------------------------
        //  XR Compatibility
        // -----------------------------------------------------------------------
#if UNITY_XR_MANAGEMENT
        private void CheckXRAndDisable()
        {
            var xrManager = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (xrManager != null &&
                xrManager.Manager != null &&
                xrManager.Manager.activeLoader != null)
            {
                enabled = false;
                Debug.Log("[DesktopFPSController] XR 디바이스 감지됨 – FPS 컨트롤러 비활성화.");
            }
        }
#endif
    }
}
