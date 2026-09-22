# 차량 선택 화면과 Prometheus

## 사용 방법

`Assets/Scene/MainMenuScene.unity`에서 Play한다. 빌드 첫 씬도 MainMenuScene이다.

1. 보유 차량 또는 상점을 선택한다.
2. Simple Retro / Prometheus 썸네일을 눌러 3D 미리보기를 연다.
3. 모델 영역을 마우스로 드래그하여 회전하고 휠로 확대/축소한다. 좌우 버튼은 현재 목록 안에서 순환한다.
4. 보유 차량 화면에서 **출전 차량 선택**을 누른다. 구경하는 차량과 출전 차량은 별개다.
5. **월드 진입**을 누르면 선택한 차량으로 GameScene에서 시작한다.
6. 월드의 **차량 선택** 버튼 또는 Esc로 돌아온다. 출전 선택은 같은 Play 세션에서 유지한다.

현재 두 차량은 개발용 차고에서 선택 가능하다. 상점은 목록·미리보기만 제공한다. 구매·재화·계정 소유권·개조·업그레이드·영구 저장은 구현하지 않았다. 카탈로그의 availableInPrototype은 실제 소유권이 아니다.

## 구성과 데이터 흐름

| 파일/에셋 | 책임 |
|---|---|
| `Assets/Code/Runtime/Vehicles/Catalog/VehicleCatalog.cs` | 차종 ID·표시 이름·주행/미리보기 프리팹·개발용 선택 가능 여부 |
| `Assets/GameContent/VehicleSelection/VehicleCatalog.asset` | 두 차량 정의와 프리팹 참조 |
| `Assets/Code/Runtime/UI/MainMenuController.cs` | 탭·목록·썸네일·상세·성능 막대·출전 선택 |
| `Assets/Code/Runtime/UI/MenuUI.cs` | Canvas·텍스트·버튼의 공통 생성과 표시 스타일 |
| `Assets/Code/Runtime/UI/VehiclePreview.cs` | 표시 전용 차량 하나·카메라·마우스 조작·RenderTexture 수명 |
| `Assets/Code/Runtime/Scenes/VehicleSession.cs` | 세션 내 출전 차종 ID와 중복을 막는 비동기 씬 로드 |
| `Assets/Code/Runtime/Scenes/WorldVehicleController.cs` | 선택한 월드 차량 활성화, 기존 카메라 재연결, 메뉴 복귀 |
| `Assets/Code/Editor/Menu/MainMenuSetup.cs` | 기존 씬에 메뉴 연결·Prometheus 주행 구성·전용 프리팹 생성 |

`카탈로그 → 썸네일/3D 미리보기 → 명시적 출전 선택(ID) → GameScene의 해당 차량 → 기존 주행 모듈`

UI는 Play 때 생성한다. 배치와 스타일은 MainMenuController/MenuUI에서 수정하고 차량 추가·프리팹 연결은 카탈로그에서 설정한다. 미리보기는 기존 모델을 재사용한 별도 표시 프리팹이며 Rigidbody·Collider·주행·입력·타이어 효과를 포함하지 않는다. 차량 교체 때 이전 모델을 비활성화 후 해제한다.

메뉴 진입 때 두 차량 썸네일을 한 카메라로 순서대로 촬영하고 텍스처로 재사용한다. 목록에서는 미리보기 카메라를 끈다. 미리보기 전용 레이어 비트 31을 사용하므로 이 씬의 다른 콘텐츠를 해당 비트에 배치하지 않는다. 아직 Addressables 기반 스트리밍은 없으며 카탈로그 프리팹은 직접 참조한다.

## Prometheus 주행 구성

GameScene의 기존 Prometheus 객체에 Simple Retro와 같은 VehicleDriveInput·ArcadeVehicleDrive·VehicleSuspension·VehicleTireEffects를 연결한다. 기존 WheelCollider와 샘플 효과 객체는 이 씬 객체에서 제거하여 중복 물리·효과 실행을 막는다. 외부 패키지 원본 Prometheus.prefab과 모델·재질은 수정하지 않는다.

Prometheus 질량은 원래 950 kg을 유지하고, 바퀴 반경은 모델의 기존 WheelCollider 기준 0.36 m로 맞춘다. 휠 참조는 `Wheels/Meshes/FrontLeftWheel`, `FrontRightWheel`, `RearLeftWheel`, `RearRightWheel` 순서다. 나머지 최초 주행 설정과 효과 참조는 GameScene의 Simple Retro에서 복사한다. 이후 차종별 프리팹 설정은 독립적으로 조정할 수 있다.

메뉴에서 진입하면 기존 씬 배치 차량 두 대를 비활성화하고 선택한 카탈로그의 주행 프리팹 한 대를 Simple Retro의 시작 위치·방향에 생성한다. VehicleFollowCamera.SetTarget으로 추적 대상을 바꾼다. 직접 GameScene에서 Play하면 개발 중인 기존 Simple Retro를 사용한다.

## 성능 표시와 튜닝

표시 항목은 Rigidbody 질량(kg), ArcadeVehicleDrive의 목표 최고 속도(km/h), 가속/제동 설정(m/s²)이다. 실제 0→100 기록이나 정지 거리를 측정한 값이 아니다. 공통 막대 범위는 질량 2000 kg, 속도 300 km/h, 가속/제동 40 m/s²이며 초과 시 막대만 최대 길이로 제한하고 숫자는 실제 값을 표시한다. 무게가 높다고 더 좋은 차량을 뜻하지 않는다.

카탈로그는 `Assets/GameContent/VehicleSelection/*_Drive.prefab`의 값을 읽고 월드 진입도 같은 프리팹을 생성한다. 메뉴에서 사용하는 차량 튜닝은 이 주행 프리팹에서 변경한다. GameScene의 배치 차량은 직접 씬을 실행하는 개발용이며, 배치 객체만 수정한 값은 카탈로그 프리팹에 자동 반영되지 않는다.

## Editor 명령과 검증

- `menu.setup`: apply=false로 변경 범위를 확인하고 apply=true로 구성한다. 대상은 고정된 두 씬과 전용 생성 경로다. 열린 대상 씬이 dirty이면 거부한다. 이미 카탈로그가 있으면 재생성하지 않는다. 원본 씬·빌드 목록 백업 위치는 reportPath다.
- `menu.rebuildPreviews`: 현재 카탈로그의 주행 프리팹에서 표시 프리팹 두 개를 다시 만든다. 프리팹 GUID는 유지한다. 미리보기 전용 편집은 재생성 시 덮어쓰므로 필요할 때만 명시적으로 호출한다.
- `editor.refresh`: Unity 에셋 새로고침을 예약한다. 반환은 예약 완료이며 컴파일 완료는 이후 editor.status/콘솔로 확인한다.
- `MenuValidation.RunBatch`: `.menu-validation-copy`가 있는 별도 batch 프로젝트에서만 사용한다. 두 차량의 코스 반복과 실제 Play Mode 목록·미리보기·선택·왕복·중복 객체 검사를 수행한다.

원본 Editor에서 검증 메서드를 실행하지 않는다. 실행 결과는 [CurrentState](CurrentState.md)에 기록한다.

## 현재 제약

현재 HDRP/PC 프로토타입이다. 실제 구매·온라인 소유권과 개조 데이터는 연결하지 않았다. 선택은 게임 재시작 후 복원하지 않는다. 한글 표시는 OS 글꼴을 사용하므로 모바일 배포 전 재배포 가능한 한글 폰트 에셋과 터치·안전 영역·실기기 60 FPS 검증이 필요하다.
