using UnityEngine;
using UnityEngine.InputSystem;

namespace SteelDistrict.Vehicles
{
    public struct VehicleDriveCommand
    {
        public float Throttle;
        public float Brake;
        public float Steering;
        public bool Handbrake;
        public bool BrakePressed;
    }

    // Input Actions are sampled on the render loop; physics consumes a snapshot.
    [DisallowMultipleComponent]
    public sealed class VehicleDriveInput : MonoBehaviour
    {
        private InputActionMap actions;
        private InputAction throttle, brake, steering, handbrake;
        private VehicleDriveCommand command;
        private bool replaying;

        // 시험장의 고정 스텝 입력을 실제 키보드 입력과 같은 명령 경로로 전달합니다.
        public void SetReplayCommand(VehicleDriveCommand value) { replaying = true; command = value; }
        public void ClearReplayCommand() { replaying = false; command = default; }

        private void Awake()
        {
            actions = new InputActionMap("Vehicle");
            throttle = actions.AddAction("Throttle", InputActionType.Value);
            throttle.AddBinding("<Keyboard>/w");
            throttle.AddBinding("<Keyboard>/upArrow");
            brake = actions.AddAction("BrakeReverse", InputActionType.Button);
            brake.AddBinding("<Keyboard>/s");
            brake.AddBinding("<Keyboard>/downArrow");
            steering = actions.AddAction("Steering", InputActionType.Value);
            steering.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            steering.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            handbrake = actions.AddAction("Handbrake", InputActionType.Button, "<Keyboard>/space");
            brake.performed += OnBrakePressed;
        }

        private void OnEnable() => actions?.Enable();
        private void OnBrakePressed(InputAction.CallbackContext context) => command.BrakePressed = true;

        private void Update()
        {
            if (replaying) return;
            if (!Application.isFocused || Time.timeScale <= 0f)
            {
                command = default;
                return;
            }
            command.Throttle = throttle.ReadValue<float>();
            command.Brake = brake.ReadValue<float>();
            command.Steering = steering.ReadValue<float>();
            command.Handbrake = handbrake.IsPressed();
        }

        public VehicleDriveCommand ConsumeCommand()
        {
            var result = command;
            command.BrakePressed = false;
            return result;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) command = default;
        }

        private void OnDisable()
        {
            replaying = false;
            actions?.Disable();
            command = default;
        }

        private void OnDestroy() => actions?.Dispose();
    }
}
