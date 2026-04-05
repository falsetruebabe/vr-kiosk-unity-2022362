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
        private bool requireRightClickToLook = true;

        // -----------------------------------------------------------------------
        //  Cached State (Update 내 GC 방지)
        // -----------------------------------------------------------------------
        private float _yaw;       // 수평 회전 (Y축)
        private float _pitch;     // 수직 회전 (X축)
        private Transform _transform;

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
        }

        private void Update()
        {
            HandleMovement();
            HandleMouseLook();
        }

        // -----------------------------------------------------------------------
        //  Private Handlers
        // -----------------------------------------------------------------------
        private void HandleMovement()
        {
            float h = Input.GetAxis("Horizontal");   // A/D
            float v = Input.GetAxis("Vertical");     // W/S

            // 전진 방향은 카메라 forward 기준, Y 성분 제거로 지면 이동 유지
            Vector3 forward = _transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 right = _transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 move = (forward * v + right * h) * moveSpeed * Time.deltaTime;
            _transform.position += move;
        }

        private void HandleMouseLook()
        {
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
