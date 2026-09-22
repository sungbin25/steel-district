using UnityEngine;

namespace SteelDistrict.Vehicles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(VehicleDriveInput), typeof(VehicleSuspension))]
    public sealed class ArcadeVehicleDrive : MonoBehaviour
    {
        [Tooltip("가속·제동·조향·핸드브레이크 명령을 읽는 컴포넌트입니다. 비워 두면 같은 오브젝트에서 찾습니다.")]
        [SerializeField] private VehicleDriveInput input;
        [Tooltip("네 바퀴의 접지와 하중을 계산하는 컴포넌트입니다. 비워 두면 같은 오브젝트에서 찾습니다.")]
        [SerializeField] private VehicleSuspension suspension;
        // 주행 단위: 속도 km/h, 가속도 m/s², 응답 시간 s.
        [Header("기본 주행")]
        [Tooltip("이 속도에 가까워질수록 가속력이 줄어듭니다. 내리막이나 충돌로 넘을 수 있는 목표 속도입니다.")]
        [SerializeField, Min(1)] private float maxSpeedKph = 120;
        [Tooltip("후진 가속력이 줄어드는 기준 속도입니다.")]
        [SerializeField, Min(1)] private float reverseSpeedKph = 30;
        [Tooltip("전진 가속의 기준값입니다. 클수록 빠르게 속도를 올립니다.")]
        [SerializeField, Min(0)] private float acceleration = 14;
        [Tooltip("일반 브레이크의 감속 세기입니다. 클수록 짧은 거리에서 정지합니다.")]
        [SerializeField, Min(0)] private float brakeAcceleration = 24;
        [Tooltip("가속과 제동 입력을 놓았을 때 줄어드는 속도입니다.")]
        [SerializeField, Min(0)] private float coastingDeceleration = 1.2f;
        [Tooltip("최대 조향 입력의 목표 회전 속도입니다. 실제 회전은 속도·접지·미끄러짐의 영향을 받습니다.")]
        [SerializeField, Min(1)] private float turnRate = 100;
        [Tooltip("조향 입력이 0에서 최대에 도달하는 시간입니다. 작을수록 즉각적이며 반대 방향 전환은 더 걸립니다.")]
        [SerializeField, Min(0.01f)] private float steeringResponse = 0.12f;
        // 뒤 접지 배율을 낮추면 오버스티어와 드리프트가 쉬워집니다.
        [Header("타이어 접지와 오버스티어")]
        [Tooltip("옆으로 흐르는 속도를 줄이는 기준 시간입니다. 작을수록 빠르게 붙잡지만 최대 접지 가속도의 제한을 받습니다.")]
        [SerializeField, Min(0.05f)] private float gripRecovery = 0.18f;
        [Tooltip("타이어가 옆미끄러짐을 억제하는 기준 한계입니다. 실제 힘은 바퀴 하중과 뒤 타이어 접지 배율 등에 따라 달라집니다.")]
        [SerializeField, Min(1)] private float maximumGripAcceleration = 15;
        [Tooltip("이 속도부터 회전 강도에 따라 뒤 타이어 접지력을 낮춥니다. 현재 속도 보간의 상한은 코드상 90 km/h입니다.")]
        [SerializeField, Min(1)] private float oversteerStartKph = 50;
        [Tooltip("강한 고속 회전에서 뒤 타이어에 적용할 접지 배율입니다. 낮을수록 차 뒤쪽이 쉽게 흐릅니다.")]
        [SerializeField, Range(0.1f, 1)] private float highSpeedRearGrip = 0.48f;
        [Tooltip("주행 중 핸드브레이크를 잡았을 때 뒤 타이어의 접지 배율입니다. 낮을수록 드리프트를 시작하기 쉽습니다.")]
        [SerializeField, Range(0.02f, 0.5f)] private float handbrakeRearGrip = 0.12f;
        [Tooltip("뒤 접지 배율이 1만큼 회복하는 데 걸리는 시간입니다. 클수록 드리프트 후 접지 회복이 느립니다.")]
        [SerializeField, Min(0.05f)] private float gripRestoreSeconds = 0.55f;
        [Tooltip("뒤 바퀴에 작용하는 핸드브레이크 감속의 기준 세기입니다.")]
        [SerializeField, Min(0)] private float handbrakeDeceleration = 5;
        [Tooltip("차량 원점 기준 X=좌우, Y=높이, Z=앞뒤입니다. 낮추면 전복이 줄어듭니다. Rigidbody에는 초기화 시 적용되므로 실행 중 변경 후에는 재실행해야 합니다.")]
        [SerializeField] private Vector3 centerOfMass = new Vector3(0, 0.45f, 0.15f);
        private Rigidbody body;
        private float steering, rearGrip = 1, assistWeight = 1, suppressionTime;
        private bool reversing;
        public Rigidbody Body => body;
        // 메뉴는 실제 프리팹 설정을 표시하며 측정하지 않은 성능 점수를 만들지 않습니다.
        public float TargetMaxSpeedKph => maxSpeedKph;
        public float AccelerationSetting => acceleration;
        public float BrakeAccelerationSetting => brakeAcceleration;
        public VehicleSuspension Suspension => suspension;
        public float SpeedKph => body == null ? 0 : body.linearVelocity.magnitude * 3.6f;
        public float ForwardSpeed { get; private set; }
        public float SlipAngle { get; private set; }
        public float RearGrip => rearGrip;
        public int GroundedWheels => suspension == null ? 0 : suspension.GroundedCount;
        public bool IsReversing => reversing;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (input == null) input = GetComponent<VehicleDriveInput>();
            if (suspension == null) suspension = GetComponent<VehicleSuspension>();
            body.centerOfMass = centerOfMass;
            suspension.Initialize();
            if (!suspension.IsReady) enabled = false;
        }

        private void FixedUpdate() => SimulateStep(input != null && input.isActiveAndEnabled ?
            input.ConsumeCommand() : default, Time.fixedDeltaTime);

        public void SimulateStep(VehicleDriveCommand command, float dt)
        {
            if (body == null || !enabled || dt <= 0) return;
            suspension.SimulateStep();
            suppressionTime = Mathf.Max(0, suppressionTime - dt);
            assistWeight = Mathf.MoveTowards(assistWeight, suppressionTime > 0 ? 0.35f : 1, dt * 3);
            steering = Mathf.MoveTowards(steering, Mathf.Clamp(command.Steering, -1, 1), dt / steeringResponse);
            Vector3 normal = Vector3.zero, groundVelocity = Vector3.zero;
            foreach (var wheel in suspension.Contacts)
                if (wheel.Grounded) { normal += wheel.Normal; groundVelocity += wheel.GroundVelocity; }
            Vector3 physicsForward = body.rotation * Vector3.forward;
            if (GroundedWheels == 0)
            {
                ForwardSpeed = Vector3.Dot(body.linearVelocity, physicsForward);
                SlipAngle = 0;
                return;
            }
            normal.Normalize();
            groundVelocity /= GroundedWheels;
            Vector3 forward = Vector3.ProjectOnPlane(physicsForward, normal).normalized;
            Vector3 right = Vector3.Cross(normal, forward);
            Vector3 relative = body.linearVelocity - groundVelocity;
            ForwardSpeed = Vector3.Dot(relative, forward);
            float lateralSpeed = Vector3.Dot(relative, right);
            float speed = Mathf.Abs(ForwardSpeed);
            SlipAngle = Mathf.Atan2(lateralSpeed, Mathf.Max(1, speed)) * Mathf.Rad2Deg;

            if (command.Brake <= 0) reversing = false;
            if (command.BrakePressed && command.Brake > 0 && speed <= 1f / 3.6f) reversing = true;
            if (command.Throttle > 0) reversing = false;
            float drive = 0;
            bool braking = command.Brake > 0 && (!reversing || command.Throttle > 0);
            if (command.Throttle > 0 && command.Brake <= 0)
            {
                if (ForwardSpeed < -0.1f) braking = true;
                else drive = command.Throttle * acceleration * Mathf.Clamp01(1 - Mathf.Max(0, ForwardSpeed) / (maxSpeedKph / 3.6f));
            }
            else if (reversing)
                drive = -command.Brake * acceleration * 0.65f * Mathf.Clamp01(1 - Mathf.Max(0, -ForwardSpeed) / (reverseSpeedKph / 3.6f));
            if (braking)
                drive = -Mathf.Sign(ForwardSpeed) * Mathf.Min(speed / dt, brakeAcceleration);
            else if (command.Throttle <= 0 && !reversing)
                drive = -Mathf.Sign(ForwardSpeed) * Mathf.Min(speed / dt, coastingDeceleration);

            float desiredYaw = steering * turnRate * Mathf.Deg2Rad * Mathf.Clamp01(speed / 2) *
                Mathf.Lerp(1, 0.4f, Mathf.Clamp01(speed / (maxSpeedKph / 3.6f))) * Mathf.Sign(ForwardSpeed);
            float cornerDemand = Mathf.Abs(ForwardSpeed * desiredYaw);
            float oversteer = Mathf.InverseLerp(oversteerStartKph / 3.6f, 90f / 3.6f, speed) *
                Mathf.InverseLerp(5, 16, cornerDemand);
            float targetGrip = command.Handbrake && speed > 0.5f ? handbrakeRearGrip : Mathf.Lerp(1, highSpeedRearGrip, oversteer);
            rearGrip = Mathf.MoveTowards(rearGrip, targetGrip, dt / (targetGrip < rearGrip ? 0.14f : gripRestoreSeconds));
            float tireSteer = Mathf.Atan(2.52f * desiredYaw / Mathf.Max(2, speed)) * Mathf.Rad2Deg * Mathf.Sign(ForwardSpeed);
            if (speed > 3 && rearGrip < 0.95f && desiredYaw * SlipAngle < 0)
            {
                float counterAssist = Mathf.InverseLerp(25, 55, Mathf.Abs(SlipAngle)) * (1f - rearGrip);
                tireSteer = Mathf.Lerp(tireSteer, Mathf.Clamp(SlipAngle * 0.5f, -25, 25), counterAssist);
            }

            for (int i = 0; i < 4; i++)
            {
                var wheel = suspension.Contacts[i];
                bool rear = i >= 2;
                wheel.SteeringAngle = rear ? 0 : tireSteer;
                if (!wheel.Grounded) continue;
                Vector3 tireForward = Quaternion.AngleAxis(wheel.SteeringAngle, wheel.Normal) *
                    Vector3.ProjectOnPlane(physicsForward, wheel.Normal).normalized;
                Vector3 tireRight = Vector3.Cross(wheel.Normal, tireForward);
                Vector3 pointVelocity = body.GetPointVelocity(wheel.Hub) - wheel.GroundVelocity;
                wheel.ForwardSpeed = Vector3.Dot(pointVelocity, tireForward);
                wheel.LateralSpeed = Vector3.Dot(pointVelocity, tireRight);
                float grip = rear ? rearGrip : 1;
                float loadFactor = Mathf.Clamp(wheel.Load / (body.mass * Physics.gravity.magnitude / 4), 0, 1.5f);
                float lateral = Mathf.Clamp(-wheel.LateralSpeed / gripRecovery,
                    -maximumGripAcceleration * grip, maximumGripAcceleration * grip) * loadFactor * assistWeight;
                float longitudinal = drive;
                bool locked = speed > 0.6f && (braking || (rear && command.Handbrake));
                if (rear && command.Handbrake)
                    longitudinal -= Mathf.Sign(wheel.ForwardSpeed) * Mathf.Min(Mathf.Abs(wheel.ForwardSpeed) / dt, handbrakeDeceleration * 2);
                if (locked && braking) lateral *= 0.55f;
                body.AddForceAtPosition((forward * longitudinal + tireRight * lateral) * (body.mass / 4),
                    wheel.Hub, ForceMode.Force);
                wheel.AngularSpeed = locked ? 0 : wheel.ForwardSpeed / suspension.Radius;
                float longitudinalSlip = wheel.ForwardSpeed - wheel.AngularSpeed * suspension.Radius;
                wheel.SlipSpeed = new Vector2(wheel.LateralSpeed, longitudinalSlip).magnitude;
                wheel.SlipAmount = Mathf.InverseLerp(0.8f, 6f, wheel.SlipSpeed);
            }
            float yaw = Vector3.Dot(body.angularVelocity, normal);
            float contactWeight = GroundedWheels / 4f;
            // Tire forces generate yaw too. This limited helper keeps countersteering responsive.
            float yawAssist = Mathf.Lerp(0.35f, 1f, rearGrip);
            body.AddTorque(normal * Mathf.Clamp((desiredYaw - yaw) / 0.24f, -5, 5) *
                assistWeight * contactWeight * yawAssist, ForceMode.Acceleration);
            // Stop a developing powerslide from turning into an uncontrollable full spin.
            // Only oppose rotation that increases slip; countersteering remains available.
            if (speed > 3 && rearGrip < 0.95f && yaw * SlipAngle < 0)
            {
                float slideLimit = Mathf.InverseLerp(25, 55, Mathf.Abs(SlipAngle));
                body.AddTorque(normal * Mathf.Clamp(-yaw * slideLimit * 8f, -10, 10) *
                    contactWeight * assistWeight, ForceMode.Acceleration);
            }
            Vector3 tiltVelocity = body.angularVelocity - normal * yaw;
            body.AddTorque((Vector3.Cross(body.rotation * Vector3.up, normal) * 12 - tiltVelocity * 2.5f) *
                contactWeight * assistWeight, ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.sqrMagnitude > 9) suppressionTime = 0.4f;
        }

        private void OnDisable()
        {
            steering = suppressionTime = 0;
            rearGrip = assistWeight = 1;
            reversing = false;
            suspension?.ClearMotion();
        }
    }
}
