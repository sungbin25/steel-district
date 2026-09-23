# 프로젝트 지도

관련 코드를 찾을 때 읽는다. 실제 기능 상태와 검증 결과는 [CurrentState.md](CurrentState.md), 공통 규칙은 [AGENTS.md](../AGENTS.md)를 따른다. 경로는 프로젝트 루트 기준이다.

## 코드 구조

```text
Assets/Code/
├─ README.md
├─ Runtime/
│  ├─ Vehicles/
│  │  ├─ Input/VehicleDriveInput.cs
│  │  ├─ Driving/ArcadeVehicleDrive.cs
│  │  ├─ Suspension/VehicleSuspension.cs
│  │  ├─ Effects/VehicleTireEffects.cs
│  │  └─ Catalog/                   # VehicleCatalog · VehicleConfiguration · VehiclePurchaseTransaction
│  ├─ Camera/VehicleFollowCamera.cs
│  ├─ UI/                           # 차량 목록·미리보기·성능 표시
│  ├─ Scenes/                       # 출전 선택 상태·메뉴/월드 전환
│  └─ Testing/VehicleReplayDriver.cs
└─ Editor/
   ├─ Vehicles/VehicleInspectors.cs
   ├─ Bridge/                       # JSON 명령·프리팹 검사/변경·PowerShell 클라이언트
   ├─ Testing/VehicleTestTrack.cs
   ├─ Menu/                         # MainMenuSetup · VehiclePrefabOrganization · VehicleShowroomSetup · MainMenuAuthoring/ViewBuilder · VehicleShopSetup
   └─ Validation/                   # 기존 차량·입력 재생·브리지·Play Mode 검증
```

폴더는 책임별 탐색 경계다. 현재 별도 asmdef는 없으며 런타임 코드는 기존 `SteelDistrict.Vehicles`, Editor 코드는 `SteelDistrict.Editor` 네임스페이스를 유지한다. 카메라도 기존 직렬화 타입을 유지하기 위해 네임스페이스를 바꾸지 않았다.

## 읽을 문서 선택

| 문서 | 책임 | 읽는 시점 |
|---|---|---|
| [CurrentState](CurrentState.md) | 구현·미구현·확인할 문제·검증 상태 | 새 작업 시작 |
| [Collaboration](Collaboration.md) | GitHub 동기화·Codex/Claude 교대·인계 | 도구 교대와 업로드 작업 |
| [PlatformAndPerformance](PlatformAndPerformance.md) | PC·Android/iOS 범위·저사양 60 FPS·전환 준비 | 플랫폼·렌더링·입력·네트워크·최적화 작업 |
| [Vehicle](Vehicle.md) | 차량 구성과 모듈 간 데이터 흐름 | 차량 전체 동작 변경 |
| [Input](Input.md) | 키 입력과 명령 스냅샷 | 조작·입력 변경 |
| [Driving](Driving.md) | 가속·제동·조향·드리프트 | 주행 감각·힘 계산 변경 |
| [Suspension](Suspension.md) | 네 바퀴 접지·하중·휠 외형 | 서스펜션·휠 배치 변경 |
| [TireEffects](TireEffects.md) | 연기·타이어 자국 | 효과 출력 변경 |
| [Camera](Camera.md) | 탑다운·추적·후방 보기 | 카메라 변경 |
| [EditorTools](EditorTools.md) | 한국어 인스펙터·Gizmo | 편집 도구 변경 |
| [Validation](Validation.md) | 기존 검증 진입점·실행 조건 | 검증 수행·확장 |
| [UnityEditorBridge](UnityEditorBridge.md) | 명령 규격·조회·일괄 적용·실패 처리 | Editor 자동화 호출 |
| [TestTrack](TestTrack.md) | 평지·요철·경사·벽·입력 재생·초기화 | 반복 주행 시험 |
| [SaveAndScene](SaveAndScene.md) | 현재 씬 기준·저장과 전환의 후속 설계 | 씬 이동·저장 작업 |
| [MainMenuFlow](MainMenuFlow.md) | 메인 화면·차량 미리보기·개조·월드 진입의 요구와 구현 순서 | 메인 화면·차고·상점·진입 흐름 작업 |
| [VehicleSelection](VehicleSelection.md) | 구현된 두 차량 선택·3D 미리보기·성능·월드 연결 | 메뉴 UI·카탈로그·Prometheus 작업 |

## 코드 외 주요 위치

| 경로 | 역할 |
|---|---|
| `Assets/Scene/GameScene.unity` | 현재 차량 개발 씬. Simple Retro Car와 Main Camera |
| `Assets/Scene/MainMenuScene.unity` | 빌드 첫 씬. 차량 선택과 미리보기 |
| `Assets/GameContent/VehicleSelection/` | 차량 카탈로그·미리보기 조명 |
| `Assets/Prefabs/Vehicles/SimpleRetro/`, `Assets/Prefabs/Vehicles/Prometheus/` | 차종별 주행·표시 프리팹. 초기 능력치는 주행 프리팹에서 편집 |
| `Assets/Prefabs/Environment/MenuParking.prefab` | GameScene에서 복제한 주차장 원본. MainMenuScene의 MenuParking 인스턴스를 편집하며 런타임은 활성 상태만 전환 |
| `Assets/UI/Fonts/` | 메뉴용 한글 TTF·OFL 라이선스·출처 |
| `Assets/OutdoorsScene.unity` | 기존 빌드 등록을 보존한 씬. 메인 진입 흐름에서는 사용하지 않음 |
| `Assets/Scene/Tests/VehicleTestTrack.unity` | 반복 주행 시험장. 빌드 목록에 자동 추가하지 않음 |
| `Assets/Testing/` | 시험 차량 프리팹·시험장 재질 |
| `Assets/Unity Asset/Polyeler/` | 외부 차량 모델과 샘플 |
| `Assets/Unity Asset/PROMETEO - Car Controller/` | Prometheus 모델·타이어 효과와 샘플 |
| `Assets/Settings/`, `Assets/Compositor/` | 렌더링 관련 에셋. 렌더 문제일 때만 탐색 |
| `ProjectSettings/`, `Packages/` | Unity·프로젝트·패키지 기준 |
| `Logs/`, `Temp/` | 검증 기록과 임시 복사본. 기본 소스 탐색 제외 |

새 기능은 책임에 맞는 Runtime 또는 Editor 하위 폴더에 둔다. 미구현 기능의 빈 폴더·가짜 컴포넌트는 미리 만들지 않는다. 파일을 이동하거나 모듈 경계를 바꿀 때 이 지도와 해당 모듈 문서의 링크를 함께 갱신한다.
