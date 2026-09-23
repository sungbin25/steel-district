using UnityEngine;

namespace SteelDistrict.Vehicles
{
    public sealed class VehicleWheelContact
    {
        public bool Grounded;
        public Vector3 Point, Normal, GroundVelocity, Hub;
        public float Compression, Load, ForwardSpeed, LateralSpeed, SlipSpeed, SlipAmount;
        public float SteeringAngle, AngularSpeed;
        internal float Spin;
    }

    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleSuspension : MonoBehaviour
    {
        // 휠 중심에서 위·아래로 travel만큼 움직입니다. 반경과 이동량은 월드 미터 단위입니다.
        [Header("네 바퀴와 서스펜션")]
        [Tooltip("반드시 앞 왼쪽 → 앞 오른쪽 → 뒤 왼쪽 → 뒤 오른쪽 순서입니다. 각 Transform 원점을 바퀴 중심으로 사용하며 위치를 초기화 시 저장합니다.")]
        [SerializeField] private Transform[] wheels = new Transform[4];
        [Tooltip("서스펜션 레이가 지면으로 인식할 레이어입니다. 자기 차량·트리거·너무 가파른 면은 제외합니다.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("물리 접지 계산에 사용하는 반경입니다. 지름은 이 값의 2배이며 모델 메시 크기를 변경하지 않습니다. 씬 뷰 원을 타이어 외곽에 맞추세요.")]
        [SerializeField, Min(0.05f)] private float wheelRadius = 0.326f;
        [Tooltip("기준 휠 중심에서 위로 압축·아래로 늘어날 수 있는 각각의 거리입니다. 전체 이동 범위는 이 값의 2배입니다.")]
        [SerializeField, Min(0.05f)] private float travel = 0.22f;
        [Tooltip("스프링 강성을 정하는 값입니다. 높을수록 단단하고 차체 움직임이 빠르며 낮을수록 부드럽습니다.")]
        [SerializeField, Range(1f, 5f)] private float springFrequency = 2.5f;
        [Tooltip("스프링 진동을 억제합니다. 1은 임계 감쇠의 기준이며 작으면 출렁이고 크면 움직임이 둔해집니다.")]
        [SerializeField, Range(0.2f, 2f)] private float dampingRatio = 0.85f;
        [Tooltip("같은 차축 좌우 바퀴의 압축 차이를 줄이는 힘의 계수입니다. 0은 끄기이며 클수록 차체가 덜 기울어집니다.")]
        [SerializeField, Range(0, 20)] private float antiRoll = 6f;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Vector3[] restPositions = new Vector3[4];
        private readonly Quaternion[] restRotations = new Quaternion[4];
        private Rigidbody body;
        public VehicleWheelContact[] Contacts { get; } =
        {
            new VehicleWheelContact(), new VehicleWheelContact(),
            new VehicleWheelContact(), new VehicleWheelContact()
        };
        public int GroundedCount { get; private set; }
        public float Radius => wheelRadius;
        public float Travel => travel;
        public void CaptureConfiguration(VehicleConfiguration value)
        {
            value.wheelRadius=wheelRadius; value.travel=travel; value.springFrequency=springFrequency;
            value.dampingRatio=dampingRatio; value.antiRoll=antiRoll; value.groundMask=groundMask.value;
        }
        public void ApplyConfiguration(VehicleConfiguration value)
        {
            wheelRadius=value.wheelRadius; travel=value.travel; springFrequency=value.springFrequency;
            dampingRatio=value.dampingRatio; antiRoll=value.antiRoll; groundMask=value.groundMask;
        }
        public bool IsReady { get; private set; }
        private void Awake() => Initialize();

        // 시각화에서 움직이는 모델 위치 대신 초기 기준 중심을 읽습니다. 물리 상태는 바꾸지 않습니다.
        public bool TryGetRestWheelCenter(int index, out Vector3 center)
        {
            center = default;
            if (wheels == null || index < 0 || index >= wheels.Length || index >= restPositions.Length || wheels[index] == null) return false;
            center = IsReady && body != null
                ? body.position + body.rotation * restPositions[index]
                : wheels[index].position;
            return true;
        }

        public void Initialize()
        {
            if (IsReady) return;
            body = GetComponent<Rigidbody>();
            if (wheels.Length != 4) { enabled = false; return; }
            for (int i = 0; i < 4; i++)
            {
                if (wheels[i] == null)
                {
                    Debug.LogError("Assign all four wheels to VehicleSuspension.", this);
                    enabled = false;
                    return;
                }
                restPositions[i] = transform.InverseTransformPoint(wheels[i].position);
                restRotations[i] = wheels[i].localRotation;
            }
            IsReady = true;
        }

        // Called once by the drive before tire forces, not from a second FixedUpdate.
        public void SimulateStep()
        {
            if (!IsReady) return;
            GroundedCount = 0;
            Vector3 up = body.rotation * Vector3.up;
            float omega = 2f * Mathf.PI * springFrequency;
            float cornerMass = body.mass / 4f;
            float staticForce = cornerMass * Physics.gravity.magnitude;
            for (int i = 0; i < 4; i++)
            {
                var wheel = Contacts[i];
                wheel.Hub = body.position + body.rotation * restPositions[i];
                Vector3 mount = wheel.Hub + up * travel;
                wheel.Grounded = FindGround(mount, -up, wheelRadius + 2f * travel, out var hit);
                wheel.Load = 0;
                wheel.Compression = -travel;
                wheel.SlipAmount = wheel.SlipSpeed = 0;
                if (!wheel.Grounded) continue;
                GroundedCount++;
                wheel.Point = hit.point;
                wheel.Normal = hit.normal;
                wheel.GroundVelocity = hit.rigidbody != null ? hit.rigidbody.GetPointVelocity(hit.point) : Vector3.zero;
                wheel.Compression = Mathf.Clamp(wheelRadius + travel - hit.distance, -travel, travel);
                float compressionSpeed = -Vector3.Dot(body.GetPointVelocity(mount) - wheel.GroundVelocity, up);
                wheel.Load = Mathf.Clamp(staticForce + cornerMass *
                    (omega * omega * wheel.Compression + 2f * dampingRatio * omega * compressionSpeed),
                    0, staticForce * 6f);
                body.AddForceAtPosition(up * wheel.Load, wheel.Hub, ForceMode.Force);
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
                    hit.rigidbody.AddForceAtPosition(-up * wheel.Load, hit.point, ForceMode.Force);
            }
            ApplyAntiRoll(0, 1, up, staticForce);
            ApplyAntiRoll(2, 3, up, staticForce);
        }

        private void ApplyAntiRoll(int left, int right, Vector3 up, float staticForce)
        {
            var a = Contacts[left];
            var b = Contacts[right];
            if (!a.Grounded || !b.Grounded) return;
            float force = Mathf.Clamp((a.Compression - b.Compression) * antiRoll * body.mass,
                -Mathf.Min(staticForce, b.Load), Mathf.Min(staticForce, a.Load));
            body.AddForceAtPosition(-up * force, a.Hub, ForceMode.Force);
            body.AddForceAtPosition(up * force, b.Hub, ForceMode.Force);
            a.Load -= force;
            b.Load += force;
        }

        private bool FindGround(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, groundMask, QueryTriggerInteraction.Ignore);
            closest = default;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.rigidbody == body || hit.transform.IsChildOf(transform) ||
                    Vector3.Dot(hit.normal, Vector3.up) < 0.5f || hit.distance >= best) continue;
                closest = hit;
                best = hit.distance;
            }
            return best < float.PositiveInfinity;
        }

        private void LateUpdate() => UpdateVisuals(Time.deltaTime);
        public void UpdateVisuals(float dt)
        {
            if (!IsReady) return;
            for (int i = 0; i < 4; i++)
            {
                var wheel = Contacts[i];
                Vector3 local = restPositions[i] + Vector3.up * wheel.Compression;
                Vector3 destination = transform.TransformPoint(local);
                wheels[i].position = Vector3.Lerp(wheels[i].position, destination, 1f - Mathf.Exp(-dt * 35f));
                wheel.Spin = Mathf.Repeat(wheel.Spin + wheel.AngularSpeed * Mathf.Rad2Deg * dt, 360);
                wheels[i].localRotation = restRotations[i] * Quaternion.Euler(0, wheel.SteeringAngle, 0) *
                    Quaternion.Euler(wheel.Spin, 0, 0);
            }
        }

        public void ClearMotion()
        {
            foreach (var wheel in Contacts)
            {
                wheel.Grounded = false;
                wheel.SlipAmount = wheel.SlipSpeed = wheel.AngularSpeed = wheel.Spin = 0;
                wheel.SteeringAngle = 0;
            }
        }
    }
}
