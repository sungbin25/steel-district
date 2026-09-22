using SteelDistrict.Vehicles;
using SteelDistrict.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SteelDistrict.Scenes
{
    [DefaultExecutionOrder(-500)]
    public sealed class WorldVehicleController : MonoBehaviour
    {
        [Tooltip("메인 화면과 같은 차량 카탈로그입니다.")] public VehicleCatalog catalog;
        [Tooltip("GameScene의 기존 Simple Retro Car입니다. 기본 차량 선택 시 그대로 사용합니다.")] public GameObject originalVehicle;
        [Tooltip("GameScene에 배치된 Prometheus입니다. 선택 시 시작 위치로 옮겨 사용합니다.")] public GameObject prometheusVehicle;
        [Tooltip("실제로 생성된 차량에 다시 연결할 기존 추적 카메라입니다.")] public VehicleFollowCamera followCamera;
        public GameObject ActiveVehicle { get; private set; }
        private Text status;
        private bool inputBlocked;
        private void Awake()
        {
            var entry = catalog != null ? catalog.Find(VehicleSession.SelectedId) : null;
            bool fromMenu = entry != null && entry.IsValid && entry.availableInPrototype;
            if (entry == null || !entry.IsValid || !entry.availableInPrototype) entry = catalog != null ? catalog.DefaultVehicle : null;
            if (entry == null || originalVehicle == null || followCamera == null)
            { VehicleSession.Error = "차량/카메라 연결이 없습니다."; return; }
            VehicleSession.Select(entry);
            if (prometheusVehicle != null) prometheusVehicle.SetActive(false);
            if (!fromMenu) ActiveVehicle = originalVehicle;
            else
            {
                originalVehicle.SetActive(false);
                // 메뉴에 표시한 프리팹을 그대로 생성하여 수치와 실제 출전 구성이 어긋나지 않게 합니다.
                ActiveVehicle = Instantiate(entry.drivePrefab);
                ActiveVehicle.transform.SetPositionAndRotation(originalVehicle.transform.position, originalVehicle.transform.rotation);
                ActiveVehicle.name = entry.displayName;
                ActiveVehicle.SetActive(true);
            }
            followCamera.SetTarget(ActiveVehicle.transform);
        }
        private void Start()
        {
            var ui = MenuUI.Canvas("World Menu UI");
            MenuUI.Button(ui, "ReturnMenu", "← 차량 선택", .025f, .905f, .16f, .065f, ReturnToMenu);
            status = MenuUI.Text(ui, "World Status", "WASD: 주행 · Space: 핸드브레이크 · V: 시점 · Esc: 차량 선택", .21f, .91f, .76f, .05f, 18);
            if (ActiveVehicle == null) status.text = VehicleSession.Error;
        }
        public void ReturnToMenu() => VehicleSession.Travel(VehicleSession.MenuScene);
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ReturnToMenu();
            if (ActiveVehicle != null && inputBlocked != VehicleSession.Busy)
            {
                inputBlocked = VehicleSession.Busy;
                ActiveVehicle.GetComponent<VehicleDriveInput>().enabled = !inputBlocked;
            }
            if (status != null && !string.IsNullOrEmpty(VehicleSession.Error)) status.text = VehicleSession.Error;
        }
    }
}
