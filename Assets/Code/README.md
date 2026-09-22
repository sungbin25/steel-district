# 코드 탐색 안내

새 작업은 [현재 상태](../../Docs/CurrentState.md)에서 시작하고, 관련 코드 위치는 [프로젝트 지도](../../Docs/ProjectMap.md)에서 찾는다. 공통 규칙은 [AGENTS.md](../../AGENTS.md)를 따른다.

| 폴더 | 진입 코드 | 모듈 문서 |
|---|---|---|
| Runtime/Vehicles/Input | VehicleDriveInput.cs | [입력](../../Docs/Input.md) |
| Runtime/Vehicles/Driving | ArcadeVehicleDrive.cs | [주행](../../Docs/Driving.md) |
| Runtime/Vehicles/Suspension | VehicleSuspension.cs | [서스펜션](../../Docs/Suspension.md) |
| Runtime/Vehicles/Effects | VehicleTireEffects.cs | [타이어 효과](../../Docs/TireEffects.md) |
| Runtime/Camera | VehicleFollowCamera.cs | [카메라](../../Docs/Camera.md) |
| Editor/Vehicles | VehicleInspectors.cs | [인스펙터와 시각화](../../Docs/EditorTools.md) |
| Editor/Validation | VehicleDrivingValidation.cs | [검증](../../Docs/Validation.md) |
| Editor/Bridge | BridgeCommands.cs, EditorBridge.cs, Tools/Invoke-UnityBridge.ps1 | [Editor Bridge](../../Docs/UnityEditorBridge.md) |
| Editor/Testing | VehicleTestTrack.cs | [시험장](../../Docs/TestTrack.md) |
| Runtime/Testing | VehicleReplayDriver.cs | [입력 재생](../../Docs/TestTrack.md) |
| Runtime/Vehicles/Catalog, Runtime/UI, Runtime/Scenes | VehicleCatalog.cs, MainMenuController.cs, VehicleSession.cs, WorldVehicleController.cs | [차량 선택](../../Docs/VehicleSelection.md) |
| Editor/Menu | MainMenuSetup.cs | [메뉴 구성](../../Docs/VehicleSelection.md) |

차량 전체 연결은 [Vehicle.md](../../Docs/Vehicle.md), 저장·씬 이동의 현재 상태와 후속 설계는 [SaveAndScene.md](../../Docs/SaveAndScene.md)를 참조한다.

개발 씬은 `Assets/Scene/GameScene.unity`다. W/↑ 가속, S/↓ 제동·정지 후 재입력 후진, A·D/←·→ 조향, Space 핸드브레이크, V 시점 전환, Tab 후방 확인을 사용한다.

Runtime과 Editor는 폴더로 책임을 나누며 기존 타입·직렬화 필드·.meta GUID를 유지한다. 모듈 이동 시 이 목록과 프로젝트 지도를 함께 수정한다.
