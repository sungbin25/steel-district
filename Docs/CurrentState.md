# 현재 구현 상태

상태 문서 갱신일: 2026-09-23. 아래 실행 검증의 날짜는 각 기록을 따른다. 새 작업 시작 시 읽고, 수정 대상의 실제 코드와 씬을 다시 확인한다. 이 문서는 기능 상태를 기록하며 인스펙터 튜닝값의 원본이 아니다.

## 환경과 진입점

- 확정 목표: PC·Android 우선 크로스플레이, iOS 후속 지원, 저사양 기기에서도 60 FPS. 최소 기준 기기와 PC OS 범위는 미정이다. [플랫폼·성능 기준](PlatformAndPerformance.md)을 따른다.

- Unity `6000.3.10f1`: `ProjectSettings/ProjectVersion.txt`.
- HDRP `17.3.0`, Input System `1.18.0`: `Packages/manifest.json`.
- 빌드 첫 씬: `Assets/Scene/MainMenuScene.unity`. 월드·차량 개발 씬: `Assets/Scene/GameScene.unity`. 차량: Simple Retro Car, Prometheus.
- 코드 위치: [ProjectMap](ProjectMap.md). Runtime/Editor를 책임별 폴더로 분류하고 Bridge·Testing·검증 코드를 추가했다.

## 구현됨

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
| 성능 표시 | 주행 프리팹의 질량·목표 속도·가속/제동 설정을 숫자와 공통 범위 막대로 표시 |
| 월드 진입 | 명시적 출전 선택·비동기 GameScene 진입·차량 한 대 생성·카메라 재연결·메뉴 복귀 |
| Prometheus | GameScene 객체와 전용 프리팹에 공통 주행·서스펜션·효과 연결, 기존 WheelCollider 중복 제거 |

## 아직 구현되지 않음

차량 장착·개조·업그레이드 데이터, 체력·전투·무장·그래플, 교통 AI, 상점 구매·재화·계정 소유권·임무·이벤트·영구 저장·불러오기·온라인 접속은 아직 없다. 현재 차고는 두 차량을 선택 가능한 개발용 목록이며 출전 선택은 같은 Play 세션에서만 유지된다. 다운로드로 제공된 예시 코드와 기획은 미구현 목록을 구현 완료로 바꾸는 근거가 아니다.

Editor Bridge는 [UnityEditorBridge.md](UnityEditorBridge.md)의 로컬 파일 큐와 PowerShell 클라이언트로 구현했다. MCP 서버 연결과 독립적인 Codex 프롬프트 규칙 문서는 후속 작업이다. 로컬 브리지를 MCP 설치 완료로 표현하지 않는다.

## 확인할 사항

- 2026-09-23 사용자 요청: 토큰 절약을 위해 앞으로 주행시험을 실시하지 않는다. 명시적인 재요청 전까지 주행 포함 통합 검사·입력 재생·물리 시뮬레이션과 시험용 worker 준비를 생략한다. 아래 과거 시험 결과는 이 정책 이전의 실행 기록이다.
- 초기 시나리오는 [MainMenuFlow](MainMenuFlow.md), 현재 구현의 사용법과 튜닝 기준은 [VehicleSelection](VehicleSelection.md)을 따른다. UI는 Play 때 코드로 생성하며 한글은 OS 글꼴을 사용한다. 모바일 배포용 글꼴·터치/핀치·안전 영역 검증이 필요하다.

- 현재 HDRP는 Android·iOS 목표와 맞지 않는다. 공통 URP 전환 검증을 다음 준비 작업으로 제안하며, 아직 패키지·재질·카메라를 전환하지 않았다. 모바일 입력·네트워크와 실기기 60 FPS 검증도 미구현/미실행이다.

- 빌드 순서는 MainMenuScene → GameScene이다. 기존 OutdoorsScene 등록을 보존했으며 새 진입 흐름에서 사용하지 않는다. 직접 GameScene을 실행하면 배치된 Simple Retro를 사용하고, 메뉴에서 진입하면 카탈로그의 주행 프리팹을 사용한다.
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

