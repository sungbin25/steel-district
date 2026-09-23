# 차량 선택 화면과 Prometheus

## 사용 방법

`Assets/Scene/MainMenuScene.unity`에서 Play한다. 빌드 첫 씬도 MainMenuScene이다.

1. 보유 차량 또는 상점을 선택한다.
2. Simple Retro / Prometheus 썸네일을 눌러 3D 미리보기를 연다.
3. 모델 영역을 마우스로 드래그하여 회전하고 휠로 확대/축소한다. 좌우 버튼은 현재 목록 안에서 순환한다.
4. 보유 차량 화면에서 **출전 차량 선택**을 누른다. 구경하는 차량과 출전 차량은 별개다.
5. **월드 진입**을 누르면 선택한 차량으로 GameScene에서 시작한다.
6. 월드의 **차량 선택** 버튼 또는 Esc로 돌아온다. 출전 선택은 같은 Play 세션에서 유지한다.

초기 보유 차량은 Simple Retro, 미보유 차량은 Prometheus다. 차고에는 보유 차량만, 상점에는 미보유 차량만 표시한다. 세션 내 골드 구매를 지원하며 계정 소유권·개조·업그레이드·영구 저장은 구현하지 않았다. 카탈로그의 initiallyOwned는 새 세션 초기 보유값이고 availableInPrototype은 개발용 출전/구매 허용값이다. 두 값은 서로 다른 의미다.

## 골드와 차량 구매

- 시작 골드는 **1,000,000**, 차량별 테스트 가격은 **10,000 골드**다. `Assets/GameContent/VehicleSelection/VehicleCatalog.asset`의 Initial Gold와 각 Vehicles 항목의 Purchase Price에서 수정한다. 새 Play 세션부터 적용되며, 메뉴로 돌아올 때 다시 지급하지 않는다.
- 상단에 현재 골드를 표시한다. 상점에서는 월드 진입/출전 선택 버튼을 숨기고 구매 버튼을 표시한다. 차량 상세를 열어야 구매할 수 있다. 목록 카드와 구매 버튼에서 가격을 확인한다.
- 정상 구매 시 골드 차감과 소유권 변경을 함께 처리한다. 중복 구매·음수 가격·잔액 부족은 상태를 바꾸지 않는다. 구매한 차량은 상점에서 제외하고 차고 상세 화면으로 이동한다. 출전 확정은 기존 선택 버튼을 사용한다.
- 골드·구매 소유권·차종별 능력치는 현재 세션에서 씬 이동 동안 유지된다. 게임 재시작/Play 재시작 시 초기값으로 돌아간다. 서버 결제나 영구 저장이 아니다.
- 구매 경로는 `MainMenuController.PurchaseCurrent → VehicleSession.TryPurchase → VehiclePurchaseTransaction.TryCommit`이다. 가격과 소유권은 차종별 세션 정의를 사용한다. 구매/장면 전환 중 재진입은 UI와 세션에서 차단한다.

목록은 4열 × 2행, 페이지당 최대 8대다. 페이지 이동 시 이전 썸네일을 해제하고 현재 페이지의 320×180 썸네일만 만든다. 상세 보기에서 배경 주차장과 차량을 표시하며 드래그로 360° 회전, 휠로 확대한다. 목록에 두 대 이상 있으면 화살표로 차량을 바꾸면서 카메라가 다음 주차 슬롯으로 0.85초 동안 이동한다. 한 대뿐이면 화살표는 비활성화된다. 현재 각 목록은 한 대이므로 이 조건에 해당한다.

## 구성과 데이터 흐름

| 파일/에셋 | 책임 |
|---|---|
| `Assets/Code/Runtime/Vehicles/Catalog/VehicleCatalog.cs` | 차종 ID·표시 이름·주행/미리보기 프리팹·개발용 선택 가능 여부 |
| `Assets/GameContent/VehicleSelection/VehicleCatalog.asset` | 두 차량 정의와 프리팹 참조 |
| `Assets/Code/Runtime/UI/MainMenuController.cs` | 탭·목록·썸네일·상세·성능 막대·출전 선택 |
| `Assets/Code/Runtime/UI/MainMenuView.cs` | 씬에 저장한 UI 영역·버튼·카드·성능 표시 참조 |
| `Assets/Code/Runtime/UI/PreviewPointer.cs` | 별도 MonoScript GUID로 저장하는 미리보기 드래그/휠 입력 |
| `Assets/Code/Runtime/UI/MenuUI.cs` | Canvas·텍스트·버튼의 공통 생성과 표시 스타일 |
| `Assets/Code/Runtime/UI/VehiclePreview.cs` | 재사용 주차 슬롯·최대 두 표시 차량·카메라 이동/회전·RenderTexture 수명 |
| `Assets/Prefabs/Environment/MenuParking.prefab` | GameScene의 메시·재질을 복제한 주차장 배경. 주행·물리 없음 |
| `Assets/Code/Editor/Menu/VehicleShowroomSetup.cs` | 주차장 복제·메뉴 슬롯 연결·초기 보유 설정과 정적 참조 검사 |
| `Assets/Code/Runtime/Vehicles/Catalog/VehicleConfiguration.cs` | 질량·주행·접지·서스펜션 능력치의 독립 사본과 적용 |
| `Assets/Code/Runtime/Vehicles/Catalog/VehiclePurchaseTransaction.cs` | 잔액·중복·가격 확인 후 골드/소유권 동시 변경 |
| `Assets/Code/Editor/Menu/VehicleShopSetup.cs` | 골드/구매 UI 연결·성능 세로 배치·주행 없는 구매 규칙 검사 |
| `Assets/Code/Runtime/Scenes/VehicleSession.cs` | 차종별 확정 능력치·출전 ID·원본 프리팹 참조와 비동기 씬 로드 |
| `Assets/Code/Runtime/Scenes/WorldVehicleController.cs` | 선택한 월드 차량 활성화, 기존 카메라 재연결, 메뉴 복귀 |
| `Assets/Code/Editor/Menu/MainMenuSetup.cs` | 기존 씬에 메뉴 연결·Prometheus 주행 구성·전용 프리팹 생성 |

`카탈로그 → 썸네일/3D 미리보기 → 명시적 출전 선택(ID) → GameScene의 해당 차량 → 기존 주행 모듈`

UI는 MainMenuScene의 **Main Menu UI** 객체로 저장되어 Play를 켜지 않아도 편집할 수 있다. 자식 RectTransform·Image·Text에서 위치·크기·색·글꼴을 수정한다. Main Menu UI의 MainMenuView 인스펙터에서 **편집 화면: 차량 목록 / 차량 상세** 버튼으로 영역을 전환한다. 저장 후 실행하면 편집한 배치를 그대로 사용하며 카드를 재생성하지 않는다. 차량명·숫자·썸네일·막대 채움·보유 상태에 따른 활성 여부만 런타임 데이터로 갱신한다. 편집 상태의 카드 8개와 예시 숫자는 실제 보유 목록/능력치가 아니다. 3D 모델과 썸네일 렌더는 Play에서 생성한다.

카드는 `Vehicle List/Cards/Card Slot 1~8`, 성능은 `Vehicle Detail/Performance`에서 편집한다. ValueBar는 Filled Image의 가로 채움으로 유지하고 최대 범위는 MainMenuView의 Stats 배열에서 설정한다. 오브젝트 이름은 바꿀 수 있지만 View의 참조를 끊지 않는다. 글꼴은 `Assets/UI/Fonts/NanumGothic-Regular.ttf`이며 OFL 라이선스를 함께 보관한다. 임시 OS Font를 CreateAsset으로 저장하면 글꼴 데이터가 없어 렌더링되지 않으므로 사용하지 않는다.

MainMenuScene에서는 Preview Key 하나만 방향광 그림자를 생성한다. 기본 Directional Light와 Preview Fill은 그림자를 끄며 기본 조명은 전시 레이어(31)를 제외한다. HDRP의 Cascade Shadow atlas 오류를 막기 위해 다른 Directional Light의 그림자를 동시에 켜지 않는다.

차량 추가·프리팹 연결은 카탈로그에서 설정한다. 미리보기는 기존 모델을 재사용한 별도 표시 프리팹이며 Rigidbody·Collider·주행·입력·타이어 효과를 포함하지 않는다. 차량 교체 때 이전 모델을 비활성화 후 해제한다.

현재 페이지의 썸네일을 한 카메라로 순서대로 촬영한 뒤 목록에서는 카메라와 주차장 표시를 끈다. 상세 전환 중에만 이전/다음 차량 두 대를 유지하고 이동 완료 후 이전 차량을 해제한다. 연속 클릭은 전환 완료까지 차단한다. 모든 차량을 씬에 주차 배치할 필요가 없다. 미리보기 전용 레이어 비트 31을 사용하므로 다른 콘텐츠를 이 비트에 배치하지 않는다. 아직 Addressables 기반 스트리밍은 없으며 카탈로그 프리팹은 직접 참조하므로 에셋 로드 메모리가 차량 수와 무관하다는 뜻은 아니다.

MainMenuScene에 **MenuParking** 프리팹 인스턴스를 미리 배치했다. Play를 켜지 않고 해당 객체와 자식 메시·재질·배치를 수정하고 씬을 저장한다. `Vehicle Selection` → VehiclePreview의 **Environment Root**는 이 씬 객체를 참조한다. Environment Prefab은 Editor 최초 배치용 원본이며 런타임 생성에 사용하지 않는다. 실행 시 시작/목록에서는 비활성화하고 썸네일 촬영/차량 상세에서는 활성화한다. 주차장 자체를 Instantiate/Destroy하거나 런타임에 Transform·레이어를 초기화하지 않는다.

MenuParking 하위 `Showroom Parking Slot 1/2`의 위치와 방향으로 차량 주차 위치를 조정한다. 배경을 이동/회전하면 슬롯도 함께 움직인다. 슬롯의 월드 Y는 타이어 밑면의 바닥 높이다. VehiclePreview의 슬롯 배열과 전환 시간(초)도 조절할 수 있다. 원본 프리팹 전체에 반영하려면 Unity의 Overrides/Apply를 사용하고, 메뉴 전용 수정이면 씬 오버라이드로 남긴다. GameScene 원본 변경은 MenuParking 프리팹에 자동 반영하지 않는다. 차량 추가는 주행/표시 프리팹과 고유 ID를 카탈로그에 등록하고 initiallyOwned를 지정한다. 구매/보상 구현 시 성공 후 `VehicleSession.GrantOwnership(entry)`를 호출하고 목록을 다시 구성한다. 미보유 차량은 출전 선택과 차고 능력치 확정 API에서도 차단한다.

## Prometheus 주행 구성

GameScene의 기존 Prometheus 객체에 Simple Retro와 같은 VehicleDriveInput·ArcadeVehicleDrive·VehicleSuspension·VehicleTireEffects를 연결한다. 기존 WheelCollider와 샘플 효과 객체는 이 씬 객체에서 제거하여 중복 물리·효과 실행을 막는다. 외부 패키지 원본 Prometheus.prefab과 모델·재질은 수정하지 않는다.

Prometheus 질량은 원래 950 kg을 유지하고, 바퀴 반경은 모델의 기존 WheelCollider 기준 0.36 m로 맞춘다. 휠 참조는 `Wheels/Meshes/FrontLeftWheel`, `FrontRightWheel`, `RearLeftWheel`, `RearRightWheel` 순서다. 나머지 최초 주행 설정과 효과 참조는 GameScene의 Simple Retro에서 복사한다. 이후 차종별 프리팹 설정은 독립적으로 조정할 수 있다.

모든 진입 경로에서 기존 씬 배치 차량 두 대를 비활성화하고 선택한 주행 프리팹 한 대를 생성한다. 직접 GameScene에서 Play해도 카탈로그 기본 차량과 같은 능력치 경로를 사용한다. `spawnPoint`를 지정하면 그 위치·방향, 비어 있으면 기존 Simple Retro 위치·방향을 사용한다. VehicleFollowCamera.SetTarget으로 추적 대상을 바꾼다. 기존 씬 차량의 직렬화 튜닝은 보존하지만 실행 능력치로 사용하지 않는다.

## 성능 표시와 튜닝

성능 그래프는 상세 화면 **왼쪽 하단의 세로 6행**으로 배치했다. 항목은 질량(kg), 목표 최고 속도(km/h), 가속/제동 설정(m/s²), 조향 속도(°/s), 최대 접지 가속도(m/s²)다. 실제 0→100 기록이나 정지 거리를 측정한 값이 아니다. 공통 막대 범위는 순서대로 2000, 300, 40, 40, 180, 30이며 초과 시 막대만 제한한다. 무게가 높다고 더 좋은 차량을 뜻하지 않는다. 참고 이미지의 내구도·개조 시스템은 아직 없다.

프리팹 위치는 `Assets/Prefabs/Vehicles/SimpleRetro/`와 `Assets/Prefabs/Vehicles/Prometheus/`다. 각 폴더의 `*_Drive.prefab`에서 초기 능력치를 수정한다. `*_Preview.prefab`은 표시 전용이다. 카탈로그와 조명은 기존 `Assets/GameContent/VehicleSelection/`에 있다. 이동 전 GUID·프리팹 내용·카탈로그 연결을 유지했다.

차종을 처음 조회하면 주행 프리팹 능력치를 VehicleSession에 복사한다. 메뉴 숫자·막대와 월드 생성은 같은 확정값을 사용한다. 월드 차량의 Awake 전에 질량·주행·접지·서스펜션 값을 적용하므로 도착 씬의 다른 설정이 이를 덮어쓰지 않는다. 출전 차량 변경이나 메뉴 왕복에도 차종별 설정을 유지한다. 씬 배치 차량이나 실행 중 프리팹 Inspector를 수정해도 이미 확정한 세션 값을 자동 변경하지 않는다. 초기값 튜닝은 Play 종료 후 주행 프리팹에서 수행한다.

후속 차고 업그레이드 UI는 `VehicleSession.GetConfiguration(entry)`로 편집용 사본을 얻고, 적용 버튼에서 `CommitConfiguration(entry, configuration)`의 성공 여부를 확인한 뒤 성능 표시를 갱신한다. 취소한 초안은 확정값에 영향을 주지 않는다. 현재 업그레이드 UI 자체는 미구현이다. 씬 이동 시 차량 인스턴스의 값을 다시 저장하지 않으므로 월드의 임시 조정이 차고 확정값을 덮어쓰지 않는다.

다른 주행 씬도 WorldVehicleController에 카탈로그·추적 카메라·시작 지점을 연결하면 같은 선택/능력치를 사용한다. Travel은 빌드 목록에서 로드 가능한 씬으로 이동할 수 있다. 이 데이터는 같은 Play 세션에서만 유지하며 게임 재시작·Editor 도메인 재로드 후 복원하는 영구 저장은 없다.

## Editor 명령과 검증

- `menu.setup`: apply=false로 변경 범위를 확인하고 apply=true로 구성한다. 대상은 고정된 두 씬과 전용 생성 경로다. 열린 대상 씬이 dirty이면 거부한다. 이미 카탈로그가 있으면 재생성하지 않는다. 원본 씬·빌드 목록 백업 위치는 reportPath다.
- `vehicle.organizePrefabs`: 고정된 차량 프리팹 4개를 차종 폴더로 이동한다. apply=false 사전 검사, apply=true 이동과 GUID 확인. 이미 이동한 항목은 변경하지 않으며 실패 시 완료한 이동을 되돌린다. 씬 저장·주행시험을 수행하지 않는다.
- `menu.setupShowroom`: GameScene의 표시 환경을 새 프리팹으로 복제하고 MainMenuScene에 연결한다. 기존 환경 프리팹/슬롯은 재생성하지 않는다. 초기 보유값(Simple Retro=true, Prometheus=false)을 설정하므로 카탈로그 커스텀 보유값을 유지해야 할 때 재실행하지 않는다. dirty 씬/카탈로그를 거부하고 변경 전 메뉴·카탈로그를 Logs에 백업한다. 원본 GameScene은 저장하지 않는다.
- `menu.inspectShowroom`: 저장된 메뉴를 읽기 전용 Preview Scene에서 열어 카메라·환경·슬롯·카탈로그·표시 모델 연결과 물리/실행 컴포넌트 제거를 확인한다. Play Mode·입력 재생·물리 시뮬레이션은 수행하지 않는다.
- `menu.materializeParking`: 열린 MainMenuScene의 현재 미저장 상태를 복사본으로 백업하고 MenuParking을 최초 배치/연결한다. 기존 인스턴스·오버라이드는 유지하며 슬롯을 월드 위치 보존으로 하위에 배치한다. 환경이 이미 연결돼 있으면 중복 생성하지 않는다. 변경이 있으면 현재 편집 상태를 저장한다.
- `menu.setupShop`: 현재 메뉴 편집 상태를 백업하고 골드/구매 UI 및 왼쪽 하단 세로 성능 표시를 최초 구성한다. 이미 구성된 UI는 재배치하지 않는다. 카탈로그 초기 골드/가격은 기존 값을 유지하여 저장한다.
- `menu.checkShop`: 정상·중복·잔액 부족·음수 가격·정확한 잔액 구매를 독립 변수로 검사하고 저장된 메뉴 UI/카탈로그 참조를 확인한다. 실제 플레이 세션 잔액/소유권·씬 이동·물리를 변경하지 않는다.
- `menu.materializeUI`: 최초 Main Menu UI 생성·View 연결과 방향광 그림자 정리. 기존 View는 재생성하지 않아 편집 내용을 보존한다. 메뉴 씬이 dirty면 중단한다. `Steel District > Menu > 편집 가능한 UI 구성·그림자 수정`에서도 실행 가능하다. 처음 menu.setup으로 만든 씬에는 이 단계를 추가로 적용한다.
- `menu.inspectUI`: 저장된 UI 참조·Missing Script·실제 MonoScript 에셋 참조·한글 글리프 생성·단일 방향광 그림자를 검사한다. 화면/주행 검사는 아니다.
- `menu.repairUIReferences`: 열린 MainMenuScene의 미저장 상태를 복사본으로 백업한 뒤, 초기 구현의 임시 글꼴과 Orbit Area의 비영구 PreviewPointer 참조만 복구하고 현재 편집 상태를 저장한다. 다른 사용자 글꼴과 배치를 덮어쓰지 않는다.
- `menu.rebuildPreviews`: 현재 카탈로그의 주행 프리팹에서 표시 프리팹 두 개를 다시 만든다. 프리팹 GUID는 유지한다. 미리보기 전용 편집은 재생성 시 덮어쓰므로 필요할 때만 명시적으로 호출한다.
- `editor.refresh`: Unity 에셋 새로고침을 예약한다. 반환은 예약 완료이며 컴파일 완료는 이후 editor.status/콘솔로 확인한다.
- `MenuValidation.RunBatch`: `.menu-validation-copy`가 있는 별도 batch 프로젝트에서만 사용한다. 두 차량의 코스 반복과 실제 Play Mode 목록·미리보기·선택·왕복·중복 객체 검사를 수행한다.

MenuValidation은 주행을 포함하므로 현재 실행 금지다. 과거 두 차량 모두 보유·즉시 모델 교체를 전제한 시나리오는 새 보유 정책·비동기 카메라 이동에 맞춰 수정한 뒤, 사용자 재요청이 있을 때만 실행해야 한다. 실행 결과는 [CurrentState](CurrentState.md)에 기록한다.

## 현재 제약

현재 HDRP/PC 프로토타입이다. 온라인 소유권·서버 재화 판정과 개조 데이터는 연결하지 않았다. 골드·구매·선택은 게임 재시작 후 복원하지 않는다. 메뉴는 한글 TTF를 사용하지만 기존 월드 UI의 OS 글꼴 의존과 모바일 터치·안전 영역·실기기 60 FPS 검증은 별도 후속 작업이다.
