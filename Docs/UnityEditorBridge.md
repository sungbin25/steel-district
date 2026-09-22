# Unity Editor Bridge

Editor 조회·차량 검사·설정 일괄 적용·입력 재생 시험을 실행할 때 읽는다. 구현은 `Assets/Code/Editor/Bridge/`, 시험장 생성은 `Assets/Code/Editor/Testing/`에 있다.

## 연결 방식

Unity 프로젝트를 열고 컴파일을 완료하면 InitializeOnLoad가 로컬 파일 큐를 준비한다. 별도 포트·외부 서버·MCP 패키지 설치가 필요하지 않다. **현재 구현은 PowerShell에서 호출하는 Editor Bridge이며 MCP 서버는 아니다.**

요청/처리 중/응답은 프로젝트의 `Library/SteelDistrictBridge/{requests,processing,responses}/`에 저장된다. Unity 메인 스레드가 요청을 처리한다. 클라이언트는 응답 JSON을 표준 출력으로 반환하며 정상 0, 명령 실패 1, 대기 시간 초과 2로 종료한다.

```powershell
# 프로젝트 루트에서 실행
& './Assets/Code/Editor/Bridge/Tools/Invoke-UnityBridge.ps1' -Command editor.status
& './Assets/Code/Editor/Bridge/Tools/Invoke-UnityBridge.ps1' -RequestFile './Docs/Bridge/Requests/inspect-vehicle.json'
& './Assets/Code/Editor/Bridge/Tools/Invoke-UnityBridge.ps1' -RequestFile './Docs/Bridge/Requests/replay-suite.json' -TimeoutSeconds 660
```

다른 프로젝트를 대상으로 호출할 때는 `-ProjectRoot`에 절대 경로를 지정한다. 브리지가 준비되지 않았거나 heartbeat가 오래되면 요청을 실행 큐에 넣지 않고 원인을 반환한다. Editor가 꺼져 있거나 컴파일 오류로 코드를 로드하지 못한 경우에는 먼저 Editor 로그를 확인한다.

## 명령

| command | 동작 | 주요 결과 |
|---|---|---|
| bridge.commands | 사용 가능한 명령 목록 | commands |
| editor.status | Editor 실행·컴파일·갱신·일시정지 및 열린 씬/Prefab Stage | scenes의 dirty, path, handle |
| editor.console | 현재 도메인 로드 이후 로그 조회 | level·message·stack·count |
| object.inspect | 대상 계층·컴포넌트·요청한 직렬화 필드 조회 | objects, values |
| vehicle.inspect | 필수 구성·참조·휠·Collider·레이어 검사 | checkedCount, passedCount, issues |
| vehicle.apply | 지정 프리팹 루트의 허용된 설정 필드 일괄 적용 | values의 before/after, changedCount |
| track.create | GameScene 차량에서 시험 프리팹·시험장 생성 | 생성 경로와 변경 개수 |
| drive.test | 별도 Unity 복사본에서 입력 재생·반복 비교 | tests, issues, reportPath |
| menu.setup | 고정 대상 MainMenuScene·GameScene에 차량 선택 구성 | 변경 범위·생성 개수·백업 경로 |
| menu.rebuildPreviews | 카탈로그 주행 프리팹에서 표시 전용 프리팹 재생성 | 대상 경로·변경 개수 |
| editor.refresh | 에셋 새로고침 예약. targets의 assetPath 파일은 직접 가져오고 .cs가 있으면 컴파일 요청 | checkedCount=지정 파일 수. 컴파일 완료는 후속 조회 |
| scene.hierarchy | 씬·프리팹 계층과 컴포넌트 조회. 닫힌 씬은 Preview Scene으로 읽음 | objects, 요청한 fields 값 |
| scene.edit | 로드된 씬 또는 프리팹에 작업 목록 적용 | values의 before/after, changedCount |
| scene.open / scene.close | 씬 추가 열기(mode=single은 모든 씬이 저장된 경우만) / 저장된 씬 닫기 | 변경 여부 |
| scene.new / scene.save | 빈 씬 파일을 추가 모드로 생성·저장 / 로드된 씬 저장 | 변경 여부 |

메뉴 명령의 범위와 제한은 [VehicleSelection](VehicleSelection.md)을 참조한다. setup은 이미 구성된 카탈로그를 덮어쓰지 않는다. rebuildPreviews는 지정된 표시 프리팹 내용을 재생성하므로 apply=false로 범위를 확인하고 호출한다.

로그 조회는 Unity Console 전체 과거 기록의 복원이 아니다. 현재 도메인에서 받은 최근 500개를 보관하고 연속 중복을 집계한다. `level: "Warning"` 등의 필터와 limit을 사용할 수 있다. 도메인 재로드 전·브리지 설치 전의 로그는 Editor 로그 파일에서 확인한다.

## 요청 형식

version은 1이며 id는 영문·숫자·밑줄·하이픈 1~64자다. 클라이언트가 누락된 version과 id를 채운다. 요청은 최대 64 KiB, targets 1~100개, changes 1~50개다.

대상 식별은 아래 중 하나를 사용한다.

- 프리팹: `{ "assetPath": "Assets/.../Car.prefab" }`. 조회는 선택적으로 hierarchyPath로 자식을 지정할 수 있다.
- 저장된 씬: `{ "assetPath": "Assets/Scene/GameScene.unity", "hierarchyPath": "Simple Retro Car" }`. 열려 있으면 현재 상태를 읽고, 닫혀 있으면 별도 Preview Scene으로 읽은 후 닫는다.
- 저장하지 않은 새 씬: status의 handle과 정확한 hierarchyPath를 사용한다. handle은 현재 Editor 세션에서만 유효하다.
- 저장된 객체의 GlobalObjectId: 조회 결과의 objectId를 사용한다. 미저장 객체와 도메인/씬 수명에 따라 사용할 수 없으므로 씬 경로/handle을 우선한다.

계층 경로가 중복되면 임의의 첫 객체를 선택하지 않는다. 객체 조회는 limit으로 전체 반환 객체 수를 제한한다(최대 200). 상세 값은 `fields: ["ArcadeVehicleDrive.maxSpeedKph", "Rigidbody.m_Mass"]`처럼 요청한 필드만 반환한다.

## 차량 검사와 설정 적용

검사 기준: 루트에 Rigidbody·입력·주행·서스펜션·타이어 효과 각 1개, 동적 중력 Rigidbody, 차체 Collider, 네 바퀴의 내부/고유 참조, 효과 프리팹 연결, 단위 스케일, 비어 있지 않은 접지 마스크. Missing Script와 WheelCollider 중복도 확인한다.

현재 차량과 지면이 같은 레이어이면 경고를 반환한다. 자기 차량은 주행 코드에서 제외하지만 다른 차량을 지면으로 인식할 수 있으므로 자동으로 레이어를 바꾸지 않는다.

일괄 변경의 기본은 **apply=false 미리보기**다. [예시](Bridge/Requests/preview-settings.json)로 before/after를 확인한 뒤 원하는 요청에 apply=true를 지정한다. value는 숫자 문자열이며 float 직렬화 설정과 groundMask 정수, Rigidbody의 m_Mass만 허용한다. 컴포넌트 추가/삭제·참조 교체·Transform·배열·임의 코드 실행은 이 명령에 포함하지 않는다.

`checkExpected: true, expectedValue: "120"`처럼 기대한 기존 값을 지정할 수 있다. expectedValue는 조회 결과 before와 같은 문자열을 사용한다. 한 대상이라도 범위·필드·기대값·편집 상태 검사가 실패하면 모두 적용하지 않는다. 열린 Prefab Stage나 미저장 프리팹은 충돌을 반환한다.

적용은 Undo 그룹으로 묶고 지정 프리팹만 저장한다. 같은 값을 재적용하면 저장하지 않는다. 저장 중 실패하면 이번 명령의 프리팹 파일을 복원한다. Undo 후 디스크 반영이 필요하면 Unity에서 해당 프리팹을 저장하고 다시 조회한다.

values.changed는 **제안 전후 값이 다른지**를 나타낸다. 실제 적용 수는 changedCount다. dry run과 사전 검사 실패의 changedCount는 0이다.

## 씬·프리팹 편집 (scene.*)

구현은 `Assets/Code/Editor/Bridge/BridgeSceneCommands.cs`다. 모든 변경 명령은 **apply=false 미리보기**가 기본이며 targets는 정확히 1개다.

- **scene.edit 대상:** 로드된 씬(`assetPath` .unity 또는 `sceneHandle`) 또는 프리팹(`assetPath` .prefab). 닫힌 씬은 `scene.open`으로 먼저 연다.
- **씬 편집:** 한 요청을 Undo 그룹 하나로 적용한다. 미리보기와 실패는 같은 그룹을 되돌린다. 적용 후 기본은 메모리 반영뿐이며(`NOT_SAVED` 경고), `save=true`면 저장한다. 요청 전부터 dirty인 씬은 사용자 미저장 작업 보호를 위해 `save=true`를 거부한다.
- **프리팹 편집:** `LoadPrefabContents`로 격리해 작업하고 apply 성공 시에만 저장한다. 미리보기는 파일을 바꾸지 않는다. 열린 Prefab Stage 대상은 거부한다.
- **Play Mode에서는 편집을 거부한다.**

operations(최대 100개)는 순서대로 실행되며, 하나라도 실패하면 전체를 되돌린다. `values`에는 시도한 변경이 남으므로 실제 반영 여부는 `ok`와 `changedCount`로 판단한다.

| op | 필드 | 동작 |
|---|---|---|
| create | name, parent, prefab 또는 primitive, position/rotation/scale | 빈 객체·기본 도형·프리팹 인스턴스 생성. 이미 있으면 실패, optional=true면 건너뜀 |
| delete | path | 삭제. 프리팹 인스턴스 내부 객체·프리팹 루트는 거부. optional=true면 없을 때 건너뜀 |
| rename / setParent | path, name / parent | 형제 이름 충돌과 순환 부모를 거부. setParent는 월드 위치 유지 |
| setActive / setLayer / setTag | path, value | true·false / 레이어 이름 또는 번호 / Tag Manager의 태그 |
| transform | path, position/rotation/scale | 로컬 좌표, rotation은 오일러 각(도). `"x,y,z"` |
| addComponent / removeComponent | path, component, index | 형식 이름 또는 전체 이름. 중복·의존성 위반을 거부. optional로 재실행 허용 |
| set | path, component, property, value, index | 직렬화 필드 설정. component=`GameObject`는 객체 자체 |

- **경로:** 씬은 루트부터 `Group/Child`. 프리팹은 루트 기준 상대 경로이며 `""`가 루트다. 같은 경로가 여러 개면 거부한다.
- **set 값:** 숫자·정수·`true/false`·문자열·enum 이름(또는 정수값)·레이어 마스크(정수 또는 `Default,Ground`)·벡터 `"x,y,z"`·Quaternion(오일러)·색상 `"r,g,b,a"` 또는 `#RRGGBB`. 배열 원소는 `m_Materials.Array.data[0]` 형식이다.
- **참조 값:** `asset:Assets/경로[#하위에셋]`, `scene:객체경로[|컴포넌트]`, GlobalObjectId, `null`. 필드 형식을 모르므로 객체와 그 컴포넌트를 차례로 대입해 Unity가 받아들인 값을 사용한다.

재실행 안전: 브리지는 같은 id·같은 내용을 한 번만 실행한다. 새 id로 같은 작업을 반복할 때는 create/addComponent/delete/removeComponent에 `optional: true`를 지정한다. 예시는 [scene-edit-example](Bridge/Requests/scene-edit-example.json)이다.

**Cowork(클라우드) 세션에서 호출:** PowerShell 없이 요청 JSON을 `Library/SteelDistrictBridge/requests/<id>.json`에 쓰고 `responses/<id>.json`을 읽는다. 파일명과 id가 같아야 하며 요청은 파일명 순서로 처리된다. Unity 창이 백그라운드이면 외부에서 바꾼 스크립트를 가져오지 않을 수 있으므로 코드 변경 후 `editor.refresh`에 해당 파일을 targets로 지정한다.

## 중단과 실패 처리

응답의 ok와 issues를 먼저 확인한다. warning은 관찰 사항이며 error가 있으면 ok=false다. 부분 진행 여부는 changedCount와 before/after를 함께 확인한다.

완료된 id와 동일 요청은 이전 결과를 재사용한다. 같은 id에 다른 내용을 넣으면 충돌이다. 도메인 재로드/종료 중 처리 중이던 명령은 INTERRUPTED로 표시하며 자동 재적용하지 않는다. 실제 값과 시험 로그를 확인한 뒤 새 요청 여부를 결정한다.

클라이언트 시간 초과 시 처리 전 요청은 취소를 시도한다. 이미 처리 중이면 취소되었다고 가정하지 않는다. 반환된 id를 같은 요청 파일에 넣어 결과를 다시 조회한다. 취소되어 아직 실행되지 않은 요청을 같은 id로 다시 보내면 새 실행 요청이 될 수 있다.

## 주행 시험 실행 비용과 한계

drive.test는 요청별 `Temp/BridgeTests/<id>/` 복사본에 Assets·Packages·ProjectSettings를 복사하고 숨겨진 Unity batch worker를 시작한다. 복사본에서만 설치 패키지 캐시를 file 참조로 사용한다. 첫 에셋 임포트 때문에 완료까지 수 분이 걸릴 수 있다. 같은 명령의 repeat는 같은 worker 안에서 수행한다. 다른 주행 시험이 실행 중이면 중복 시작하지 않는다.

worker는 10분으로 제한하며 해당 명령이 시작한 전용 프로세스만 종료할 수 있다. 원본 Editor·씬·프리팹을 시험 중 수정하지 않는다. 복사본과 로그는 디버깅을 위해 남기며 원본으로 덮어쓰지 않는다.

테스트는 [TestTrack.md](TestTrack.md)의 고정 스텝 기준을 사용한다. 화면 품질·네트워크·실기기 60 FPS를 검증하지 않는다.
