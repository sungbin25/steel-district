# 현재 구현 상태

상태 문서 갱신일: 2026-09-23. 아래 실행 검증의 날짜는 각 기록을 따른다. 새 작업 시작 시 읽고, 수정 대상의 실제 코드와 씬을 다시 확인한다. 이 문서는 기능 상태를 기록하며 인스펙터 튜닝값의 원본이 아니다.

## 환경과 진입점

- 확정 목표: PC·Android 우선 크로스플레이, iOS 후속 지원, 저사양 기기에서도 60 FPS. 최소 기준 기기와 PC OS 범위는 미정이다. [플랫폼·성능 기준](PlatformAndPerformance.md)을 따른다.

- Unity `6000.3.10f1`: `ProjectSettings/ProjectVersion.txt`.
- HDRP `17.3.0`, Input System `1.18.0`: `Packages/manifest.json`.
- 빌드 첫 씬: `Assets/Scene/MainMenuScene.unity`. 월드·차량 개발 씬: `Assets/Scene/GameScene.unity`. 차량: Simple Retro Car, Prometheus.
- 코드 위치: [ProjectMap](ProjectMap.md). Runtime/Editor를 책임별 폴더로 분류하고 Bridge·Testing·검증 코드를 추가했다.

## 구현됨

### 2026-09-23 차량 구동방식 선택

- `ArcadeVehicleDrive`의 기본 주행 인스펙터에 한국어 구동 방식 드롭다운: 전륜(FWD)·후륜(RWD)·4륜(AWD). 기존 씬·프리팹은 기본 AWD이며 튜닝값과 에셋을 재저장하지 않았다.
- 선택한 축의 접지 바퀴에 전진·후진 엔진 힘을 배분한다(2륜 각 50%, 4륜 각 25%). 일반 제동과 자연 감속은 네 바퀴, 핸드브레이크는 뒤 바퀴 유지. 엔진 힘은 타이어 조향 방향을 따르므로 기존 AWD의 가속 선회 감각도 달라질 수 있다.
- `VehicleConfiguration.driveType`의 캡처·복사·유효성 검사·적용을 연결해 메뉴/월드 공통 설정으로 유지한다. 수정 위치와 세션 반영 규칙은 [Driving](Driving.md) 참조.
- 검증: Unity 컴파일 완료, 재로드 후 Console 수집 오류 없음, 두 주행 프리팹에서 새 Enum 직렬화 필드 조회, 설정 전달 및 힘 배분 코드 정적 확인. 씬·프리팹·기존 GUID 변경 없음. 주행시험·물리 시뮬레이션·인스펙터 화면 확인은 미실시. 차동장치/종방향 타이어 마찰 한계/엔진 휠스핀 모델은 이번 범위에 포함하지 않았다.

| 영역 | 실제 구현 |
|---|---|
| 입력 | 키보드 가속·제동·조향·핸드브레이크와 명령 스냅샷 |
| 주행 | Rigidbody 가속·감속·정지 후 재입력 후진·조향 |
| 접지 | 네 바퀴 레이 기반 스프링·댐퍼·안티롤과 휠 상하 이동 |
| 미끄러짐 | 고속 뒤 접지 저하·핸드브레이크 드리프트·접지 회복·제한적 회전 보조 |
| 효과 | 실제 타이어 슬립에 따른 Prometheus 연기·자국 |
| 카메라 | 탑다운·후방 추적·V 전환·Tab 후방 확인·추적 장애물 검사 |
| Editor | 한국어 인스펙터 설명·휠 치수·이동 범위·접지 상태·무게중심 시각화 |
| 검증 | 별도 프로젝트 batch mode의 수동 물리·참조·카메라 검사 |
| Editor Bridge | 로컬 JSON 명령·상태/미저장 씬/컴포넌트/로그 조회·프리팹 검사·일괄 적용·씬/프리팹 구조 편집(scene.*) |
| 시험장 | 평지·요철·경사·벽 구간, 시험 차량 프리팹, 고정 입력 재생·초기화 |
| 반복 시험 | 별도 worker의 코스별 측정·반복 비교, custom 입력 목록 재생 |
| 차량 선택 | 두 차종 카탈로그·보유 차량/상점 목록·차량 썸네일·3D 회전/확대·좌우 전환 |
| 성능 표시 | 차종별 세션 확정 능력치의 질량·목표 속도·가속/제동 설정을 숫자와 공통 범위 막대로 표시 |
| 차량 공통 데이터 | 주행 프리팹 초기값 → 차종별 능력치 사본 → 메뉴 표시/월드 Awake 전 적용. 차고 적용용 API 제공, 업그레이드 UI는 미구현 |
| 월드 진입 | 명시적 출전 선택·비동기 GameScene 진입·차량 한 대 생성·카메라 재연결·메뉴 복귀 |
| Prometheus | GameScene 객체와 전용 프리팹에 공통 주행·서스펜션·효과 연결, 기존 WheelCollider 중복 제거 |

## 아직 구현되지 않음

차량 장착·개조·업그레이드 데이터, 체력·전투·무장·그래플, 교통 AI, 서버 재화 판정·계정 소유권·임무·이벤트·영구 저장·불러오기·온라인 접속은 아직 없다. 현재 초기 차고는 Simple Retro, 상점은 미보유 Prometheus를 표시하며 보유·출전 선택은 같은 Play 세션에서만 유지된다. 다운로드로 제공된 예시 코드와 기획은 미구현 목록을 구현 완료로 바꾸는 근거가 아니다.

Editor Bridge는 [UnityEditorBridge.md](UnityEditorBridge.md)의 로컬 파일 큐와 PowerShell 클라이언트로 구현했다. MCP 서버 연결과 독립적인 Codex 프롬프트 규칙 문서는 후속 작업이다. 로컬 브리지를 MCP 설치 완료로 표현하지 않는다.

## 확인할 사항

- 공유 저장소는 `https://github.com/sungbin25/steel-district.git`이다. Codex·Claude 교대 시 [협업 절차](Collaboration.md)를 따르며 Claude 진입 안내는 루트 CLAUDE.md에 있다.
- 스키드 마크 표시 문제 대응: VehicleTireEffects의 런타임 HDRP/Unlit 재질에 양면 렌더링(`_DoubleSidedEnable=1`, `_CullMode=Off`)을 적용했다. 기본 Back 컬링이 얇은 Trail 메시를 숨길 수 있어 보정했다. Unity 내장 C# 컴파일러와 Unity 참조 어셈블리로 해당 파일 컴파일을 확인했으며, 직렬화 프리팹 필드의 CS0649 경고만 있었다. Editor 전체 재컴파일·수정 후 화면 확인·주행시험은 실시하지 않았다. 다음 확인은 사용자가 Play를 종료하고 재컴파일한 뒤 새로 생성된 자국의 표시 여부다. 실제 화면에서 해결되었다고 아직 확정하지 않는다.
- 2026-09-23 사용자 요청: 토큰 절약을 위해 앞으로 주행시험을 실시하지 않는다. 명시적인 재요청 전까지 주행 포함 통합 검사·입력 재생·물리 시뮬레이션과 시험용 worker 준비를 생략한다. 아래 과거 시험 결과는 이 정책 이전의 실행 기록이다.
- 초기 시나리오는 [MainMenuFlow](MainMenuFlow.md), 현재 구현의 사용법과 튜닝 기준은 [VehicleSelection](VehicleSelection.md)을 따른다. Main Menu UI는 씬에 저장되어 편집 가능하며 메뉴는 한글 TTF를 사용한다. 기존 월드 UI의 OS 글꼴 의존과 모바일 터치/핀치·안전 영역 검증은 남아 있다.

- 현재 HDRP는 Android·iOS 목표와 맞지 않는다. 공통 URP 전환 검증을 다음 준비 작업으로 제안하며, 아직 패키지·재질·카메라를 전환하지 않았다. 모바일 입력·네트워크와 실기기 60 FPS 검증도 미구현/미실행이다.

- 빌드 순서는 MainMenuScene → GameScene이다. 기존 OutdoorsScene 등록을 보존했으며 새 진입 흐름에서 사용하지 않는다. 직접 GameScene을 실행해도 카탈로그 기본 차량의 프리팹·세션 능력치를 사용한다. 기존 배치 차량 값은 보존하되 실행 시 비활성화하며, 초기 튜닝은 `Assets/Prefabs/Vehicles/<차종>/*_Drive.prefab`에서 한다.

## 2026-09-23 테스트 골드·차량 구매·성능 그래프 배치

- VehicleCatalog.asset의 초기 골드 1,000,000, 차종별 가격 10,000을 직렬화 설정으로 추가했다. 메뉴 상단에 보유 골드, 상점 카드/상세 구매 버튼에 가격을 표시한다. 설정 변경은 새 세션부터 적용한다.
- 상점에서 월드 진입/출전 선택을 숨기고 구매 버튼을 표시한다. 구매 시 골드 차감·소유권 획득을 동시에 처리하고 차고 상세로 이동한다. 구매 차량은 상점에서 제외된다. 메뉴/월드 왕복에서 잔액과 소유를 유지하며 게임 재시작 복원은 미구현이다.
- 기존 6개 실제 성능 막대를 왼쪽 하단 세로 목록으로 배치했다. UI는 씬에 저장되어 직접 편집 가능하며 재실행으로 배치를 덮어쓰지 않는다.
- Unity 컴파일 완료 및 bridge 2026-09-23.10 로드 확인. menu.checkShop 7항목 통과: 정상 구매(1,000,000→990,000), 중복 차감 차단, 잔액 부족/음수 가격 상태 보존, 정확한 잔액 구매, 저장 UI 참조, 카탈로그 범위. 주행/실제 Play 구매 클릭/씬 왕복/화면 가독성 검사는 실시하지 않았다.

## 2026-09-23 MenuParking 씬 배치

- MainMenuScene에 MenuParking 프리팹 인스턴스를 미리 배치하고 VehiclePreview.environmentRoot에 연결했다. 비실행 상태에서 배경과 자식을 편집할 수 있다. 실행 시 시작/목록에서 끄고 썸네일 촬영/상세에서 켜며, 주차장 Instantiate/Destroy와 런타임 배치/레이어 초기화를 제거했다. 차량 모델 교체 방식은 유지한다.
- 주차 슬롯 2개를 월드 위치를 보존하여 MenuParking 하위로 옮겼다. 배경 이동/회전 시 슬롯도 따라간다. 원본 프리팹 연결과 씬 오버라이드를 보존한다.
- 미저장 상태를 Logs/MenuParkingScene-20260923-064457.unity로 백업한 뒤 현재 사용자 편집을 보존하여 저장했다. menu.materializeParking은 이미 연결한 인스턴스를 재생성하지 않는다.
- Unity 재컴파일 및 bridge 2026-09-23.9 로드 확인. 저장된 씬의 menu.inspectShowroom 4항목 통과: 환경 인스턴스/슬롯 부모·카탈로그·표시 모델·물리/실행 컴포넌트 부재. 현재 도메인 콘솔 오류 없음. 주행시험과 이번 변경의 Play Mode 활성 전환 검사는 실시하지 않았다.

## 2026-09-23 편집 가능한 메뉴 UI·그림자·글꼴 복구

- MainMenuScene에 Main Menu UI와 카드 8개·성능 6개를 저장했다. MainMenuView가 직렬화 참조를 보유하고 MainMenuController는 내용·채움·활성 여부와 런타임 버튼 동작만 갱신한다. 사용자 RectTransform·색·폰트·정적 라벨을 재생성/덮어쓰기하지 않는다. MainMenuView 인스펙터 버튼으로 목록/상세 편집 영역을 전환한다.
- 기본 Directional Light와 Preview Key가 모두 그림자를 생성하던 상태에서 Preview Key 하나로 통일했다. 기본 조명의 전시 레이어도 제외했다. 설치된 HDRP HDShadowManager의 Cascade atlas 오류 발생 지점과 중복 설정을 확인했다.
- 최초 UI 저장 후 발생한 Missing Script는 VehiclePreview.cs 안에 함께 선언했던 PreviewPointer의 비영구 MonoScript 참조였다. PreviewPointer.cs로 분리하고 씬의 해당 컴포넌트를 에셋 GUID 참조로 교체했다. 기존 VehiclePreview GUID는 유지했다.
- 텍스트 누락은 OS 임시 Font를 fontsettings로 저장하면서 실제 글꼴 데이터가 비어 발생했다. Google Fonts의 NanumGothic-Regular.ttf와 OFL.txt를 추가하고 Text 54개를 연결했다. 무참조인 잘못된 MenuFont.fontsettings는 제거했다.
- 미저장 UI 변경은 Logs/MenuReferenceRepair-20260923-063413.unity에 현재 상태 그대로 복사한 뒤 필요한 참조만 복구·저장했다. 이후 사용자가 만든 새로운 미저장 변경은 유지했다.
- Unity 재컴파일 완료. menu.inspectUI에서 저장된 씬 재로드·UI 참조·Missing Script·PreviewPointer 영구 GUID·한글 글리프 생성·그림자 방향광 1개 확인 통과. computer-use로 편집 모드의 한글 UI와 사용자 실행 중 메뉴의 텍스트/포인터 표시를 확인했다. 수정 후 현재 도메인 콘솔 조회에는 새 오류가 없었다. 주행시험은 실시하지 않았다.
- 편집 모드의 빈 썸네일 8칸과 능력치 숫자는 배치용 샘플이다. 실제 차량 이미지와 데이터는 Play에서 갱신한다. 기존 MenuValidation은 새 씬 저장 UI/소유권/전환 흐름에 맞춘 후속 수정이 필요하다.

## 2026-09-23 차고·상점 주차장 상세 화면

- MainMenuScene의 차고/상점을 보유 여부로 분리했다. 사용자 지정 초기값은 Simple Retro 보유, Prometheus 미보유다. 미보유 차량은 출전 선택과 능력치 확정 API에서도 차단한다. 세션 소유 상태 API만 제공하며 결제·온라인 소유권·영구 저장은 미구현이다.
- 4열×2행 카드 목록과 페이지 이동, 현재 페이지 최대 8장의 320×180 썸네일을 구현했다. 페이지 이동 시 해제하고 한 카메라로 촬영한다. 상세는 실제 능력치 6개 숫자/막대, 360° 회전·확대, 0.85초 카메라 이동을 사용한다.
- GameScene의 주차장 메시/재질 34개 Renderer를 MenuParking.prefab으로 복제하고 메뉴에 슬롯 2개를 연결했다. 전환 중 최대 2대, 완료 후 1대만 표시한다. 현재 목록별 차량이 한 대여서 좌우 버튼은 비활성화된다. 같은 목록에 차량을 추가하면 활성화된다.
- Unity 재컴파일과 bridge 2026-09-23.7 로드 확인. menu.inspectShowroom 4항목 통과: 초기 보유/카탈로그·카메라/환경·슬롯·표시 모델 참조, Missing Script 및 물리/실행 컴포넌트 부재. GameScene은 활성/clean 상태를 유지했고 기존 사용자 차량 배치 변경을 보존했다.
- 주행시험, Play Mode 메뉴 화면·카메라 전환의 실제 시각 확인은 실시하지 않았다. 기존 MenuValidation은 두 차량 모두 보유·즉시 전환을 가정하므로 새 흐름에 맞춘 후속 수정이 필요하고 현재 실행 금지다. 카탈로그 직접 참조 구조이므로 Addressables 스트리밍과 모바일 실기기 성능 최적화 완료를 뜻하지 않는다.

## 2026-09-23 차량 프리팹 관리·능력치 일원화

- 두 차종의 주행/미리보기 프리팹 4개를 `Assets/Prefabs/Vehicles/SimpleRetro/`, `Prometheus/`로 이동했다. Editor Bridge `vehicle.organizePrefabs`로 GUID 보존을 확인했으며 카탈로그 참조와 프리팹 튜닝을 유지했다.
- VehicleSession은 차종별 독립 능력치 사본을 유지한다. 메뉴 표시와 월드 생성이 같은 값을 사용하며 씬 배치 값이나 도착 씬의 같은 ID 정의로 덮어쓰지 않는다. 차고 확정 API는 GetConfiguration/CommitConfiguration이며 편집용 사본을 제공한다. 게임 종료 후 복원하는 영구 저장은 구현하지 않았다.
- Unity Editor에서 Runtime/Editor 스크립트 재컴파일과 새 브리지 버전 `2026-09-23.6` 로드를 확인했다. 프리팹 필수 컴포넌트·휠/효과 연결·Collider 검사에서 오류가 없었다. 기존 접지 마스크에 차량 레이어가 포함된 경고는 두 차종 모두 남아 있으며, 이번 작업에서 사용자 튜닝을 변경하지 않았다.
- MainMenuScene은 열린 상태와 미저장 여부(false)를 유지했다. 주행시험·Play Mode 씬 왕복·화면 확인은 실시하지 않았다. 이전 주행 결과는 이번 변경의 검증으로 사용하지 않는다.
- 고속 오버스티어 속도 보간의 상한 90 km/h 등 일부 보조값은 코드에 고정되어 있다. 다른 차종·튜닝 범위에 일반화되었다고 보지 않는다.
- 사용자 미저장 Inspector 값은 파일만으로 확인할 수 없다. 문서·코드 초기값으로 덮어쓰지 않는다.
- 실제 주행 감각과 Gizmo·인스펙터의 화면 가독성은 Play Mode와 Scene 뷰에서 별도 확인이 필요하다.

## Editor Bridge 씬 편집 확장 (2026-09-23)

- `scene.hierarchy/edit/open/close/new/save`를 추가했다. 오브젝트 생성·삭제·이름/부모/활성/레이어/태그·Transform·컴포넌트 추가/제거·직렬화 필드와 참조 설정을 지원한다. 사용법은 [UnityEditorBridge](UnityEditorBridge.md#씬프리팹-편집-scene)를 따른다.
- 실행 중인 원본 Editor(PID 16200)에서 Cowork 파일 큐로 확인했다. 시험 씬 `Assets/Scene/Tests/BridgeSandbox.unity`에서 미리보기 후 씬 빈 상태 유지, 12개 작업 적용·저장, 실패 작업 포함 요청의 전체 되돌림(위치 0,1,0 유지), optional 재실행 시 변경 0, 컴포넌트 제거·비활성·태그·필드·삭제, scene.save·close를 확인했다. 프리팹 미리보기 후 `SimpleRetro_Preview.prefab` 파일 시각은 바뀌지 않았다. MainMenuScene은 저장된 상태를 유지했다.
- 시험 씬은 빌드 목록에 추가하지 않았다. 사용자 GameScene·MainMenuScene은 편집하지 않았다. 주행시험은 사용자 지시에 따라 실시하지 않았다.
- 미확인: 지정 파일 가져오기(editor.refresh targets)와 scene.new의 활성 씬 복원은 코드 반영 후 Unity 재컴파일이 필요하며 아직 동작 확인 전이다. Unity가 백그라운드일 때 기존 전체 Refresh로는 외부 변경 스크립트를 가져오지 않았다.

## 차량 선택·Prometheus 검증 (2026-09-23)

- 현재 차량 프리팹으로 Simple Retro와 Prometheus 각각 5개 코스 × 2회, 총 20회 주행 시험 통과. 필수 컴포넌트·효과 연결과 표시 프리팹의 물리/입력 제거도 확인했다.
- 최종 코드의 그래픽 Editor Play Mode 검사 38개 PASS. 차량 목록·썸네일 선택·포인터 회전/휠 확대·좌우 순환·상점 출전 차단·중복 진입 차단·Prometheus 실제 이동·두 차량 월드 왕복·성능 설정 일치·단일 차량/리스너/EventSystem을 확인했다.
- 1920×1080 캡처에서 두 차량 목록·미리보기·성능표·월드 차량을 직접 확인했다. 첫 썸네일 촬영을 카메라 렌더 완료 뒤로 옮기고 과도한 미리보기 밝기와 Bloom을 보정했다.
- 원본 프로젝트는 Editor Bridge로 구성했으며 기존 열린 GameScene을 유지했다. 설정 전 씬과 빌드 목록은 `Logs/MenuSetupBackup-20260922-153508/`에 백업했다. 기존 Simple Retro의 입력·주행·서스펜션 직렬화 블록은 변경 전후 동일하다. 외부 차량 모델/프리팹 원본은 변경하지 않았다.
- 별도 복사본에서만 로컬 PackageCache 참조를 사용했다. 첫 DX11 배치 렌더는 HDRP 셰이더 메모리 부족으로 화면 확인에 사용하지 않았다. 이후 DX12 그래픽 Editor에서 화면을 확인했다. Unity Search 인덱스 시작 예외와 Editor 종료 시 JobTempAlloc 경고는 남아 있어 무오류 콘솔이나 메모리 최적화 완료로 보고하지 않는다. 실제 PC 빌드·모바일 실기기 60 FPS·네트워크는 미검증이다.

근거: [초기 구성·물리 검사](../Logs/MenuValidation-Initial.txt), [최종 메뉴/월드 검사](../Logs/MenuValidation.txt), [Simple Retro 주행](../Logs/MenuReplay-simple-retro.json), [Prometheus 주행](../Logs/MenuReplay-prometheus.json), [차량 목록 화면](../Logs/VehicleSelection/Menu-List.png), [Prometheus 미리보기](../Logs/VehicleSelection/Menu-Prometheus.png).

## Editor Bridge·반복 시험장 검증

2026-09-14 최신 코드를 반영한 별도 Unity 프로젝트 복사본에서 확인했다.

| 검증 | 결과 |
|---|---|
| Unity 컴파일·브리지 통합 검사 | 25개 PASS. 미저장 씬/객체 조회와 보존, 프리팹 검사, 일괄 변경 미리보기·적용·Undo, 범위/기대값 검사, 요청 중복 처리 포함 |
| 고정 입력 주행 | 평지·요철·경사·벽·드리프트 각 2회 통과. 사용자 입력 목록 2회 재생도 통과 |
| 실제 PowerShell 호출 | 상태·차량 검사·명령 목록 조회 성공. drive.test 한 번의 호출로 별도 worker를 시작하고 10회 시험 결과 수신 |
| 실제 Unity Play Mode | 입력 재생·차량 이동·새 차량 하나로 초기화 후 재실행 3개 PASS |
| 기존 차량 회귀 검사 | 현재 입력 재생 코드를 포함하여 92개 PASS |
| 원본 GameScene | 작업 전후 SHA-256 동일. 기존 차량 튜닝과 씬 파일 유지 |

근거: [브리지 통합 검사](../Logs/BridgeValidation.txt), [자동 주행 응답](../Logs/Bridge-LiveReplay.json), [Play Mode 검사](../Logs/BridgePlayValidation.txt), [기존 차량 회귀 검사](../Logs/Bridge-OriginalVehicleRegression.txt).

시험 차량의 접지 마스크에 차량 레이어가 포함되어 경고를 반환한다. 기존 설정을 보존했으며 다른 차량을 지면으로 인식할 가능성은 후속 레이어 정리 항목이다. Play Mode 배치 시작 중 Unity Search 인덱스의 ArgumentOutOfRangeException도 관찰했다. 재생·초기화 검사는 통과했지만 콘솔 전체가 오류 없이 깨끗했다고 보지 않는다.

별도 복사본에서만 설치 패키지 캐시를 사용했다. nographics의 HDRP 경고가 있으므로 화면 밝기·효과·카메라 외형은 검증하지 않았다. 모바일 실기기 60 FPS와 네트워크 크로스플레이도 이번 검사 범위 밖이다. 사용 절차는 [UnityEditorBridge](UnityEditorBridge.md), 시험 조건은 [TestTrack](TestTrack.md)에 있다.

## 이전 모듈 구조 변경 검증

2026-09-14 이번 변경을 반영한 별도 복사본에서 확인했다.

| 검증 | 결과 |
|---|---|
| C# 7개와 기존 .meta 7개 | 이동 전후 내용 해시 동일. 타입·직렬화 필드·GUID 유지 |
| 기존 코드 외 에셋 151개 | 내용 해시 동일. 씬·프리팹·튜닝값 재저장 없음 |
| 문서 링크 | 프로젝트 내 대상 경로 확인 |
| Unity 컴파일·기존 차량 검사 | 컴파일 오류 없이 실행, 92개 PASS 및 ALL CHECKS PASSED |
| 실제 Play Mode·화면 확인 | 미실행. 주행 감각·Gizmo·Inspector 화면 품질은 이번 검사 범위 밖 |

검증 복사본에서만 설치된 로컬 PackageCache를 manifest의 file 참조로 사용했다. 원본 패키지 설정은 바꾸지 않았다. nographics 실행의 HDRP 그래픽 장치 경고가 있어 실제 렌더 품질의 근거로 사용하지 않는다.

근거: [파일 보존 검사](../Logs/ModuleStructure-Validation.txt), [Unity 검사 보고서](../Logs/ModuleStructure-UnityValidation.txt). 임시 복사본과 로그는 생성물이므로 없어진 경우 필요한 검사만 다시 실행한다.

상태를 갱신할 때 구현 증거·확인 날짜·미실행 검증을 함께 기록한다. 새 기능 계획은 해당 설계 문서에 두고 이 문서에는 구현 상태만 요약한다.
