# 반복 가능한 차량 시험장

시험장: `Assets/Scene/Tests/VehicleTestTrack.unity`. 시험 차량: `Assets/Testing/Vehicles/SimpleRetroCar_Test.prefab`. 원본 GameScene과 외부 모델 프리팹은 보존한다.

## 준비와 수동 확인

[시험장 생성 요청](Bridge/Requests/create-track.json)은 GameScene의 Simple Retro Car를 복사해 시험 에셋을 만든다. 생성은 기존 씬을 저장하지 않고 별도의 Additive 씬만 저장·닫는다. 같은 명령을 다시 실행하면 기존 시험장을 유지한다. 기존 시험 차량의 튜닝값을 초기화하지 않는다.

Unity에서 시험장을 열어 Play하면 차량을 생성한다. `입력 재생` 오브젝트의 VehicleReplayDriver 컴포넌트 메뉴에서 **시험/초기화 후 입력 재생**, **시험/재생 중지**, **시험/차량 초기화**를 사용할 수 있다. 자동 재생은 playOnStart로 설정한다. 재생하지 않을 때는 기존 키보드로 운전한다.

| 구간 | 시작 X / Z | 구성 |
|---|---|---|
| 평지 | 0 / 0 | 공통 평지 |
| 요철 | 30 / 0 | 높이 0.12m의 요철 4개 |
| 경사 | 60 / 0 | 8도 경사 |
| 벽 | 90 / 0 | Z=22의 충돌 벽 |

수동 재생의 spawnPosition을 해당 구간으로 바꿔 같은 입력을 비교한다. Camera는 시험 차량을 따라간다. 화면 밝기·HDRP 실제 외형은 별도 화면 확인 항목이다.

## 입력 재생

런타임 코드: `Assets/Code/Runtime/Testing/VehicleReplayDriver.cs`.
VehicleReplayStep의 ticks·throttle·brake·steering·handbrake·brakePressed를 사용한다. brakePressed는 각 구간의 첫 스텝에만 전달된다.

재생기는 주행 FixedUpdate 전에 VehicleDriveInput.SetReplayCommand로 명령을 제출한다. 재생 중 키보드 샘플링을 잠시 대신하고 종료/비활성화 시 ClearReplayCommand로 해제한다. 초기화는 기존 차량을 비활성화·제거하고 프리팹에서 새 차량을 생성하여 이전 슬립·효과·휠 기준 위치가 남지 않게 한다.

## 자동 시험

[기본 요청](Bridge/Requests/replay-suite.json)은 평지·요철·경사·벽·드리프트를 각각 2회 실행한다. 시험마다 새 차량을 생성하고 100스텝 접지 안정화 후 입력을 재생한다. 스텝은 0.02초다. 원본의 Fixed Timestep 설정을 변경하지 않는다.

측정값은 최대/최종 속도(km/h), 시작점부터 최종 위치까지 직선 거리(m), 최대 높이(m), 최대 슬립 각도(도), 최대 휠 압축량(m), 접지 스텝 수, 최종 Z다. distance는 실제 누적 주행 거리가 아니다.

| 시나리오 | 표준 합격 기준 |
|---|---|
| flat | 최대 속도 >20 km/h, 제동 후 최종 속도 <2 km/h |
| bumps | 최대 압축 >0.035m, 최종 Z>18 |
| slope | 최대 높이 >1m, 최종 Z>12 |
| wall | 최종 10<Z<21, 최종 속도 <5 km/h |
| drift | 최대 슬립 10~70도, 복원 후 슬립 <6도, 뒤 접지 >0.95 |
| custom | 유효한 물리 상태 유지와 접지 관찰. 특정 주행 성능 합격은 별도 정의 필요 |

반복 간 최종 거리 0.05m, 최대 속도 0.1km/h, 최대 슬립 0.2도 이내의 차이를 허용한다. 같은 빌드와 환경에서의 회귀 기준이며 서로 다른 PC·모바일 간 물리 결정성을 보장하지 않는다.

현재 표준은 기본 시험 차량을 위한 것이다. 다른 차종 튜닝이 실패하면 임의로 기준을 낮추지 말고 측정값과 차종별 기대 동작을 확인한다. [사용자 입력 예시](Bridge/Requests/replay-custom.json)는 총 3000스텝 이하, 최대 100개 구간, repeat 1~3으로 제한한다.

## 검증 도구 자체의 검사

`BridgeValidation.RunBatch`: 명령·미저장 씬 보존·dry run·일괄 변경·기대값·범위·Undo·요청 중복·5개 코스 반복을 검사한다.

`BridgePlayValidation.RunBatch`: 별도 복사본의 실제 Unity Play Mode에서 재생·차량 이동·새 차량으로 초기화 후 재실행을 검사한다. nographics 실행 시 화면 품질은 검증하지 않는다.

둘 다 `.bridge-validation-copy` 표식이 있는 폐기 가능한 프로젝트 복사본에서만 실행한다. 생성/초기화 시험이 복사본의 고정 시험 에셋 경로를 재생성할 수 있으므로 사용자 프로젝트에 표식을 만들어 실행하지 않는다.
