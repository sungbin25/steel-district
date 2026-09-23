using System;
using UnityEngine;

namespace SteelDistrict.Vehicles
{
    [Serializable]
    public sealed class VehicleEntry
    {
        [Tooltip("씬 이동에 사용할 고유 차종 ID입니다.")] public string id;
        [Tooltip("목록과 상세 화면에 표시할 이름입니다.")] public string displayName;
        [Tooltip("공통 주행 모듈이 연결된 차량 프리팹입니다.")] public GameObject drivePrefab;
        [Tooltip("물리·입력·효과를 제거한 표시 전용 프리팹입니다.")] public GameObject previewPrefab;
        [Tooltip("개발용 차고에서 선택을 허용합니다. 실제 계정 소유권이나 구매 기록이 아닙니다.")] public bool availableInPrototype = true;
        [Tooltip("새 플레이 세션의 초기 보유 여부입니다. 보유 차량은 차고에, 미보유 차량은 상점에 표시합니다.")] public bool initiallyOwned = true;
        [Tooltip("차량 구매 가격(골드)입니다. 새 세션에서 적용하며 테스트 기본값은 10,000입니다.")]
        [Min(0)] public int purchasePrice = 10000;
        public float Mass => drivePrefab != null ? drivePrefab.GetComponent<Rigidbody>().mass : 0;
        public ArcadeVehicleDrive Drive => drivePrefab != null ? drivePrefab.GetComponent<ArcadeVehicleDrive>() : null;
        public bool IsValid => !string.IsNullOrEmpty(id) && drivePrefab != null && previewPrefab != null &&
            drivePrefab.GetComponent<Rigidbody>() != null && Drive != null && drivePrefab.GetComponent<VehicleSuspension>() != null;
    }

    [CreateAssetMenu(menuName = "Steel District/차량 카탈로그")]
    public sealed class VehicleCatalog : ScriptableObject
    {
        [Tooltip("새 플레이 세션의 시작 골드입니다. 씬 이동 시 다시 지급하지 않습니다.")]
        [Min(0)] public int initialGold = 1000000;
        [Tooltip("메뉴와 월드에서 함께 사용하는 차량 목록입니다.")] public VehicleEntry[] vehicles = Array.Empty<VehicleEntry>();
        public VehicleEntry Find(string id) => Array.Find(vehicles, x => x != null && x.id == id);
        public VehicleEntry DefaultVehicle => Array.Find(vehicles, x => x != null && x.IsValid && x.availableInPrototype && x.initiallyOwned);
    }
}
