# 차량 주행과 드리프트

가속·제동·조향·접지 감각을 바꿀 때 읽는다.

- 코드: [ArcadeVehicleDrive.cs](../Assets/Code/Runtime/Vehicles/Driving/ArcadeVehicleDrive.cs)
- 필수 구성: Rigidbody, VehicleDriveInput, VehicleSuspension.
- 입력: VehicleDriveCommand, 네 바퀴 Contacts. 출력: Rigidbody 힘·토크와 Contacts의 슬립·조향·각속도.

## 처리 순서

SimulateStep은 서스펜션 계산 후 지면 기준 전후·횡속도를 구한다. 가속·제동·후진 상태를 판단하고 접지한 바퀴에 힘을 적용한다. 횡방향 힘은 접지 하중의 영향을 받는다. 엔진 구동력은 선택한 구동 축의 타이어 방향으로 전달하며, 접지 바퀴가 없으면 지상 추진을 적용하지 않는다.

`Arcade Vehicle Drive > 기본 주행 > 구동 방식`에서 전륜(FWD)·후륜(RWD)·4륜(AWD)을 선택한다. 전륜은 앞 두 바퀴, 후륜은 뒤 두 바퀴에 각각 총 구동력의 50%, 4륜은 네 바퀴에 각각 25%를 배분한다. 전진·후진 모두 같은 배분을 사용하며 뜬 바퀴의 몫은 다른 바퀴로 재분배하지 않는다. 일반 제동·자연 감속은 네 바퀴, 핸드브레이크는 뒤 바퀴에 유지된다. 조향된 앞바퀴 방향으로도 엔진 힘이 작용하므로 기존 AWD도 가속 중 선회 감각이 달라질 수 있다. 차동장치·타이어 종방향 마찰 한계·엔진 휠스핀은 아직 모델링하지 않는다.

`VehicleDriveType`의 저장값은 AWD=0, FWD=1, RWD=2로 고정한다. 기존 씬·프리팹의 기본값은 AWD이며 개별 튜닝값을 재저장하지 않는다. 실제 출전 차량 설정은 `Assets/Prefabs/Vehicles/<차종>/<차종>_Drive.prefab`에서 수정한다. `VehicleConfiguration.driveType`을 캡처·복사·검증·적용하므로 메뉴에서 월드로 이동해도 선택한 구동방식이 유지된다. 세션 최초 읽기 이후 프리팹 수정은 다음 실행부터 반영하며, 세션 내 변경은 `VehicleSession.CommitConfiguration`을 사용한다.

S는 먼저 제동하고, 정지 후 놓았다 다시 눌러 후진에 진입한다. 후진 중 W는 먼저 제동한다. 저속·고속에 맞춰 조향을 조절하고, 고속 급선회 또는 핸드브레이크에서 뒤 타이어의 접지 배율을 낮춘다. 슬립 각도가 클 때 제한적인 카운터스티어·회전 억제 보조를 적용한다. 강한 충돌 직후에는 주행 보조를 잠시 줄인다.

## 설정과 관찰

| 설정 묶음 | 대표 필드 | 의미 |
|---|---|---|
| 구동 방식 | driveType | 전륜·후륜·4륜 엔진 구동력 배분 |
| 속도·가감속 | maxSpeedKph, reverseSpeedKph, acceleration, brakeAcceleration | 목표 속도는 km/h, 가감속은 m/s² |
| 조향 | turnRate, steeringResponse | 목표 회전 속도와 입력 응답 |
| 횡접지 | gripRecovery, maximumGripAcceleration | 옆미끄러짐 보정 시간과 힘 제한 |
| 뒤 접지 | oversteerStartKph, highSpeedRearGrip, handbrakeRearGrip, gripRestoreSeconds | 슬립 유도와 복원 |
| 자세 | centerOfMass | 초기화 때 Rigidbody에 적용하는 로컬 무게중심 |

현재 속도·SlipAngle·RearGrip·GroundedWheels는 공개 읽기 속성으로 확인한다. 고속 보간 상한 90 km/h와 일부 회전 보조 상수는 코드에 있다. 필드의 자세한 설명은 한국어 Tooltip이 원본이며, 이 문서의 설명을 특정 차종의 확정 튜닝값으로 사용하지 않는다.

사용자 지시에 따라 주행시험은 실시하지 않는다. 컴파일·정적 코드·참조 확인만 수행하며, 실제 가속·선회·드리프트 감각은 미검증이다. 주행시험 재개를 명시적으로 요청받은 경우에만 [Validation](Validation.md)을 따른다.
