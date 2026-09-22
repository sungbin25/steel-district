# 검증 진입점과 절차

차량 변경을 검증하거나 Editor 테스트를 확장할 때 읽는다.

**현재 실행 정책(2026-09-23): 사용자 요청에 따라 주행시험을 실시하지 않는다.** 명시적으로 다시 요청받기 전에는 아래 주행·입력 재생·물리 시뮬레이션 및 주행을 포함한 통합 검증을 실행하지 않는다. 주행시험용 복사본·worker 준비도 생략한다. 기존 명령과 절차는 참고용으로 보존하며, 일반 개발 요청을 시험 재개 요청으로 해석하지 않는다. 컴파일·정적 확인·컴포넌트/참조 검사는 필요한 범위만 유지한다.

- 코드: [VehicleDrivingValidation.cs](../Assets/Code/Editor/Validation/VehicleDrivingValidation.cs)
- 진입 메서드: `SteelDistrict.Editor.VehicleDrivingValidation.RunBatch`.
- 입력 씬: 복사본의 `Assets/Scene/GameScene.unity`.
- 보고서: 복사본의 `Logs/VehicleDrivingValidation.txt`.

## 실행 조건

Editor 자동화에서는 [UnityEditorBridge](UnityEditorBridge.md)의 `drive.test`가 별도 복사본과 worker를 준비한다. 코스·입력·판정은 [TestTrack](TestTrack.md)을 따른다. 추가된 검증 타입의 네임스페이스는 `SteelDistrict.Editor.Bridge`다.

- `VehicleReplayValidation.RunBatch`: worker 요청 기반의 코스별 입력 재생.
- `BridgeValidation.RunBatch`: 일괄 적용·Undo·미저장 씬 보존·요청 중복·반복 시험.
- `BridgePlayValidation.RunBatch`: 별도 Play Mode에서 재생·초기화·재실행.
- 브리지 자체 검증은 `.bridge-validation-copy` 표식이 있는 폐기 가능한 복사본에서만 실행한다.

아래는 기존 VehicleDrivingValidation의 실행 조건이다.

이 메서드는 batch mode에서만 동작하고 씬을 열며 종료 시 EditorApplication.Exit를 호출한다. **사용자 작업 프로젝트나 열린 Editor에서 실행하지 않는다.** 최신 코드·메타·에셋·ProjectSettings·Packages를 갖춘 별도 복사본에서 실행한다. 복사본의 Library는 재사용할 수 있지만 오래된 소스와 이동 전 중복 스크립트가 남지 않게 확인한다.

호출 인수 형식:

```text
Unity.exe -batchmode -nographics -projectPath "<검증용 프로젝트 절대 경로>" -executeMethod SteelDistrict.Editor.VehicleDrivingValidation.RunBatch -logFile "<이번 실행 로그 절대 경로>"
```

Unity 실행 파일은 프로젝트 버전에 맞춘다. Windows의 백그라운드 프로세스는 숨겨 실행한다. 패키지 해결이 막혔을 때 확인된 로컬 캐시 참조를 사용할 수 있지만 검증 복사본의 manifest만 변경하며 그 차이를 보고한다.

## 현재 검사 범위

컴포넌트·카메라 참조와 Missing Script, 실제 씬 초기 접지, 임시 평지에서 가속·제동·후진·정지 조향·공중 추진, 한 바퀴 요철의 압축과 외형 이동, 저속/고속/핸드브레이크 슬립 비교, 해제 후 접지 복원, 외력 슬립 효과와 정지·공중 출력 차단, 카메라 모드·위치를 검사한다.

Physics.Simulate와 직접 명령 전달을 사용한다. 실제 키보드 이벤트·Play Mode 게임 루프·HDRP 렌더 화면·Inspector 가독성을 확인하는 도구는 아니다.

## 완료 판정

차량 선택 관련 검증은 `SteelDistrict.Editor.Bridge.MenuValidation.RunBatch`를 사용한다. `.menu-validation-copy`가 있는 별도 복사본의 batch mode에서 두 차량 코스 반복·미리보기 구성·실제 Play Mode UI 동작과 왕복을 확인한다. `RunVisual`은 같은 표식이 있는 별도 복사본의 그래픽 Editor에서 UI·주행 왕복과 화면 캡처를 확인한다. 이 표식을 사용자 프로젝트에 만들지 않는다. RunVisual은 검증 복사본의 미리보기 조명을 초기 설정으로 맞추고 작업 종료 시 해당 검증 Editor를 종료한다.

화면 캡처가 생성되었는지와 그 내용도 확인해야 한다. 셰이더 컴파일 실패나 아직 렌더되지 않은 첫 프레임을 성공 화면으로 보고하지 않는다.

이번 실행의 컴파일 오류, 종료 코드, 보고서 성공/실패를 함께 확인한다. 실패하면 첫 원인과 환경·에셋·코드 중 어느 단계인지 기록한다. 파일 이동은 추가로 C# 내용·기존 .meta GUID·씬/프리팹의 변경 여부를 비교한다. 검사 수를 고정 성공 조건으로 하드코딩하지 않는다.

변경과 관계없는 전체 재검증을 반복하지 않는다. 문서만 수정하면 링크·경로·구현 설명의 정합성을 확인한다. 실제 결과와 미실행 검증은 [CurrentState](CurrentState.md)에 기록한다.
