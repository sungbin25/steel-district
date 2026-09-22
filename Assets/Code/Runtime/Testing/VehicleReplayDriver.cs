using System;
using SteelDistrict.Vehicles;
using UnityEngine;

namespace SteelDistrict.Testing
{
    [Serializable] public sealed class VehicleReplayStep
    {
        [Tooltip("이 입력을 유지할 물리 스텝 수입니다.")][Min(1)] public int ticks = 50;
        [Tooltip("가속 입력 강도입니다. 0~1 범위를 사용합니다.")][Range(0, 1)] public float throttle;
        [Tooltip("제동 입력 강도입니다. 0~1 범위를 사용합니다.")][Range(0, 1)] public float brake;
        [Tooltip("조향 입력입니다. -1은 왼쪽, 1은 오른쪽입니다.")][Range(-1, 1)] public float steering;
        [Tooltip("이 구간 동안 핸드브레이크를 유지합니다.")] public bool handbrake;
        [Tooltip("구간 첫 스텝에 제동 버튼을 새로 누른 명령을 전달합니다.")] public bool brakePressed;
        public VehicleDriveCommand Command(bool first) => new VehicleDriveCommand
        {
            Throttle = throttle, Brake = brake, Steering = steering,
            Handbrake = handbrake, BrakePressed = first && brakePressed
        };
    }

    // 주행 FixedUpdate보다 먼저 명령을 넣어 렌더 FPS에 의존하지 않게 합니다.
    [DefaultExecutionOrder(-200)]
    public sealed class VehicleReplayDriver : MonoBehaviour
    {
        [Tooltip("초기화 때 새로 생성할 시험 차량 프리팹입니다.")]
        public GameObject vehiclePrefab;
        [Tooltip("화면에서 차량을 따라갈 카메라입니다.")]
        public Transform followCamera;
        [Tooltip("재생할 고정 스텝 입력 목록입니다.")]
        public VehicleReplayStep[] steps = Array.Empty<VehicleReplayStep>();
        [Tooltip("Play 진입 직후 자동 재생할지 정합니다.")]
        public bool playOnStart;
        [Tooltip("차량을 생성할 위치입니다. 시험 구간별 시작점으로 변경할 수 있습니다.")]
        public Vector3 spawnPosition = new Vector3(0, 0.05f, 0);
        private GameObject vehicle;
        private VehicleDriveInput input;
        private int segment, tick;
        public bool IsReplaying { get; private set; }
        private void Start() { if (playOnStart) StartReplay(); else ResetVehicle(); }

        [ContextMenu("시험/차량 초기화")]
        public void ResetVehicle()
        {
            if (!Application.isPlaying) return;
            StopReplay();
            if (vehicle != null) { vehicle.SetActive(false); Destroy(vehicle); }
            if (vehiclePrefab == null) return;
            vehicle = Instantiate(vehiclePrefab, spawnPosition, Quaternion.identity);
            input = vehicle.GetComponent<VehicleDriveInput>();
            segment = tick = 0;
        }
        [ContextMenu("시험/초기화 후 입력 재생")]
        public void StartReplay()
        {
            if (!Application.isPlaying) return;
            ResetVehicle();
            IsReplaying = input != null && steps != null && steps.Length > 0;
        }
        [ContextMenu("시험/재생 중지")]
        public void StopReplay() { IsReplaying = false; if (input != null) input.ClearReplayCommand(); }
        private void FixedUpdate()
        {
            if (!IsReplaying || input == null) return;
            if (segment >= steps.Length) { StopReplay(); return; }
            var step = steps[segment];
            input.SetReplayCommand(step.Command(tick == 0));
            if (++tick >= Mathf.Max(1, step.ticks)) { tick = 0; segment++; }
        }
        private void LateUpdate()
        {
            if (followCamera == null || vehicle == null) return;
            Vector3 focus = vehicle.transform.position + Vector3.up;
            followCamera.SetPositionAndRotation(focus + new Vector3(0, 14, -10), Quaternion.Euler(55, 0, 0));
        }
        private void OnDisable() => StopReplay();
    }
}
