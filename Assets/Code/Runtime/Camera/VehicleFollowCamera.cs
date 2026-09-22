using UnityEngine;
using UnityEngine.InputSystem;

namespace SteelDistrict.Vehicles
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class VehicleFollowCamera : MonoBehaviour
    {
        public enum ViewMode { [InspectorName("탑다운")] TopDown, [InspectorName("추적")] Chase }
        [Tooltip("카메라가 따라갈 차량의 Transform입니다.")]
        [SerializeField] private Transform target;
        [Tooltip("주행 속도에 맞춰 시선과 높이를 보정하고 차량 자체를 장애물 검사에서 제외할 때 사용합니다.")]
        [SerializeField] private Rigidbody targetBody;
        [Tooltip("탑다운은 위에서 내려다보고 추적 시점은 차량 뒤를 따라갑니다. 플레이 중 V 키로 전환합니다.")]
        [SerializeField] private ViewMode viewMode = ViewMode.TopDown;
        [Tooltip("주시점 위의 기본 높이입니다. 주행 속도에 따라 최대 6 m가 추가됩니다.")]
        [SerializeField, Min(1)] private float topDownHeight = 22f;
        [Tooltip("수평선에서 아래를 바라보는 각도입니다. 90도에 가까울수록 수직으로 내려다봅니다.")]
        [SerializeField, Range(30, 85)] private float topDownAngle = 65f;
        [Tooltip("추적 시점에서 주시점 뒤로 떨어진 거리입니다.")]
        [SerializeField, Min(1)] private float chaseDistance = 7f;
        [Tooltip("추적 시점에서 주시점보다 높은 거리입니다.")]
        [SerializeField, Min(1)] private float chaseHeight = 3.5f;
        [Tooltip("위치와 시선 변화의 완화 기준 시간입니다. 작을수록 빠르게 따라옵니다.")]
        [SerializeField, Min(0.01f)] private float followTime = 0.12f;
        [Tooltip("추적 시점에서 카메라를 가리는 벽 등을 검사할 레이어입니다.")]
        [SerializeField] private LayerMask obstructionMask = ~0;
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private InputAction switchView, lookBack;
        private Vector3 followVelocity;
        private float heading, headingVelocity;
        private bool snap = true;
        public Transform Target => target;
        public ViewMode Mode => viewMode;

        public void SetTarget(Transform value)
        {
            target = value;
            targetBody = value != null ? value.GetComponent<Rigidbody>() : null;
            followVelocity = Vector3.zero;
            headingVelocity = 0;
            snap = true;
        }

        private void Awake()
        {
            switchView = new InputAction("SwitchVehicleView", InputActionType.Button, "<Keyboard>/v");
            lookBack = new InputAction("LookBehind", InputActionType.Button, "<Keyboard>/tab");
            switchView.performed += _ => ToggleView();
        }

        private void OnEnable()
        {
            switchView?.Enable();
            lookBack?.Enable();
            snap = true;
        }

        public void ToggleView()
        {
            viewMode = viewMode == ViewMode.TopDown ? ViewMode.Chase : ViewMode.TopDown;
        }

        private void LateUpdate() => UpdateCamera(Time.deltaTime);

        public void UpdateCamera(float dt)
        {
            if (target == null || dt <= 0f) return;
            Vector3 velocity = targetBody != null ? Vector3.ProjectOnPlane(targetBody.linearVelocity, Vector3.up) : Vector3.zero;
            Vector3 focus = target.position + Vector3.up * 0.7f;
            bool behind = Application.isFocused && lookBack != null && lookBack.IsPressed();
            Vector3 position;
            Quaternion rotation;
            if (viewMode == ViewMode.TopDown)
            {
                focus += Vector3.ClampMagnitude(velocity * (behind ? -0.2f : 0.25f), 7f);
                float height = topDownHeight + Mathf.Clamp(velocity.magnitude * 0.15f, 0, 6);
                rotation = Quaternion.Euler(topDownAngle, 0, 0);
                position = focus - rotation * Vector3.forward * (height / Mathf.Sin(topDownAngle * Mathf.Deg2Rad));
            }
            else
            {
                if (snap) heading = target.eulerAngles.y;
                heading = Mathf.SmoothDampAngle(heading, target.eulerAngles.y, ref headingVelocity, 0.22f,
                    Mathf.Infinity, dt);
                Vector3 forward = Quaternion.Euler(0, heading + (behind ? 180 : 0), 0) * Vector3.forward;
                focus += forward * 1.5f;
                position = focus - forward * chaseDistance + Vector3.up * chaseHeight;
                rotation = Quaternion.LookRotation(focus - position, Vector3.up);
            }
            Vector3 next = snap ? position : Vector3.SmoothDamp(transform.position, position,
                ref followVelocity, followTime, Mathf.Infinity, dt);
            if (viewMode == ViewMode.Chase)
            {
                Vector3 delta = next - focus;
                int count = Physics.SphereCastNonAlloc(focus, 0.25f, delta.normalized, hits, delta.magnitude,
                    obstructionMask, QueryTriggerInteraction.Ignore);
                float distance = delta.magnitude;
                for (int i = 0; i < count; i++)
                    if (hits[i].rigidbody != targetBody && !hits[i].transform.IsChildOf(target))
                        distance = Mathf.Min(distance, Mathf.Max(0.35f, hits[i].distance - 0.15f));
                next = focus + delta.normalized * distance;
                rotation = Quaternion.LookRotation(focus - next, Vector3.up);
            }
            transform.SetPositionAndRotation(next, snap ? rotation :
                Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-dt / followTime)));
            snap = false;
        }

        private void OnDisable()
        {
            switchView?.Disable();
            lookBack?.Disable();
            followVelocity = Vector3.zero;
        }

        private void OnDestroy()
        {
            switchView?.Dispose();
            lookBack?.Dispose();
        }
    }
}
