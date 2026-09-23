# 차량 모듈 개요

차량 전체의 데이터 흐름을 바꿀 때 읽는다. 특정 문제는 [프로젝트 지도](ProjectMap.md)에서 해당 모듈 문서로 바로 이동한다.

## GameScene 구성

| 대상 | 구성 |
|---|---|
| Simple Retro Car 루트 | Rigidbody, VehicleDriveInput, ArcadeVehicleDrive, VehicleSuspension, VehicleTireEffects |
| Body 자식 | 기존 차체 BoxCollider |
| 네 바퀴 모델 | 앞 왼쪽·앞 오른쪽·뒤 왼쪽·뒤 오른쪽 Transform |
| Main Camera | Camera, VehicleFollowCamera 및 HDRP 설정 |

씬 참조가 실제 원본이다. 기존 모델·콜라이더·튜닝값을 코드 기본값으로 다시 만들지 않는다. 현재 구현은 WheelCollider 없이 접지 레이와 Rigidbody 힘을 사용한다.

## 데이터 흐름

Prometheus도 같은 주행 모듈을 사용한다. 카탈로그는 `Assets/GameContent/VehicleSelection/`, 주행/표시 프리팹은 `Assets/Prefabs/Vehicles/<차종>/`에 있다. 월드 진입 시 선택한 주행 프리팹에 공통 `VehicleConfiguration`을 적용하고 기존 배치 차량을 비활성화한다. 구동방식(전륜·후륜·4륜)도 공통 설정에 포함된다. 연결·튜닝 기준은 [VehicleSelection](VehicleSelection.md), 구동력 배분은 [Driving](Driving.md)을 따른다.

```text
Input System → VehicleDriveInput.Update → VehicleDriveCommand
                                              ↓ ConsumeCommand
ArcadeVehicleDrive.FixedUpdate → SimulateStep
    ├─ VehicleSuspension.SimulateStep → Contacts: 접지·하중·지면 속도
    └─ 타이어 힘·회전 보조 → Rigidbody, Contacts: 조향·회전·슬립

VehicleSuspension.LateUpdate → Contacts → 바퀴 모델 위치·회전
VehicleTireEffects.LateUpdate → Contacts → 연기·자국
VehicleFollowCamera.LateUpdate → 차량 Transform/Rigidbody → 카메라
```

서스펜션 물리는 주행 모듈이 스텝마다 한 번 호출한다. 외형 갱신과 카메라는 물리 힘을 다시 적용하지 않는다. LateUpdate 컴포넌트 사이의 상대 실행 순서는 별도로 지정하지 않았다.

## 공유 데이터와 수명

- `VehicleDriveCommand`는 입력 파일에, `VehicleWheelContact`는 서스펜션 파일에 정의되어 있다. 둘은 MonoBehaviour가 아니다.
- Contacts의 접지·하중은 서스펜션, 조향·각속도·슬립은 주행이 기록한다. 효과는 이를 읽는다.
- 초기화 때 바퀴 기준 위치·회전과 무게중심을 설정한다. 비활성화 때 입력·슬립·효과 등 각 모듈의 일시 상태를 정리한다.
- 모델·효과 프리팹과 Inspector 설정은 직렬화 참조로 유지한다. VehicleCatalog에 차종 ID·이름·주행/표시 프리팹을 정의한다. 장착·개조·영구 저장 DTO는 아직 없다.

각 모듈의 책임·튜닝·검증은 [Input](Input.md), [Driving](Driving.md), [Suspension](Suspension.md), [TireEffects](TireEffects.md), [Camera](Camera.md)를 참조한다.
