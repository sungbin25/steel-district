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
        [Tooltip("기존 차량은 시작 위치/방향만 제공하고 실행 시 비활성화됩니다. 능력치는 프리팹과 세션 데이터에서 가져옵니다.")] public GameObject originalVehicle;
        [Tooltip("이전 씬 배치 차량입니다. 중복 실행을 막기 위해 비활성화합니다.")] public GameObject prometheusVehicle;
        [Tooltip("다른 월드 씬에서 사용할 시작 지점입니다. 비워 두면 기존 차량 또는 이 객체의 위치를 사용합니다.")] public Transform spawnPoint;
        [Tooltip("실제로 생성된 차량에 다시 연결할 기존 추적 카메라입니다.")] public VehicleFollowCamera followCamera;
        public GameObject ActiveVehicle { get; private set; }
        private Text status;
        private bool inputBlocked;
        private void Awake()
        {
            // 도착 씬에 같은 ID의 다른 설정이 있어도 메뉴/차고에서 확정한 구성을 우선합니다.
            var entry = VehicleSession.ResolveSelected(catalog);
            if (entry == null || !entry.IsValid || followCamera == null)
            { VehicleSession.Error = "차량/카메라 연결이 없습니다."; return; }
            var configuration=VehicleSession.GetConfiguration(entry);
            if(configuration==null || !configuration.IsValid) { VehicleSession.Error="차량 능력치가 유효하지 않습니다."; return; }
            var spawn=spawnPoint != null ? spawnPoint : originalVehicle != null ? originalVehicle.transform : transform;
            Vector3 position=spawn.position; Quaternion rotation=spawn.rotation;
            if (prometheusVehicle != null) prometheusVehicle.SetActive(false);
            if (originalVehicle != null) originalVehicle.SetActive(false);
            // Awake 이전에 능력치를 적용하도록 비활성 부모 아래에서 생성합니다.
            var staging=new GameObject("Vehicle Configuration Staging"); staging.SetActive(false);
            try
            {
                ActiveVehicle = Instantiate(entry.drivePrefab, staging.transform);
                configuration.Apply(ActiveVehicle);
                ActiveVehicle.transform.SetPositionAndRotation(position,rotation);
                ActiveVehicle.name = entry.displayName;
                ActiveVehicle.transform.SetParent(null,true);
                ActiveVehicle.SetActive(true);
            }
            finally { Destroy(staging); }
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
