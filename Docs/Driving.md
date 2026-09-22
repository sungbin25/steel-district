# 차량 주행과 드리프트

가속·제동·조향·접지 감각을 바꿀 때 읽는다.

- 코드: [ArcadeVehicleDrive.cs](../Assets/Code/Runtime/Vehicles/Driving/ArcadeVehicleDrive.cs)
- 필수 구성: Rigidbody, VehicleDriveInput, VehicleSuspension.
- 입력: VehicleDriveCommand, 네 바퀴 Contacts. 출력: Rigidbody 힘·토크와 Contacts의 슬립·조향·각속도.

## 처리 순서

SimulateStep은 서스펜션 계산 후 지면 기준 전후·횡속도를 구한다. 가속·제동·후진 상태를 판단하고 바퀴별 종방향·횡방향 힘을 접지 하중에 맞춰 적용한다. 접지 바퀴가 없으면 지상 추진을 적용하지 않는다.

S는 먼저 제동하고, 정지 후 놓았다 다시 눌러 후진에 진입한다. 후진 중 W는 먼저 제동한다. 저속·고속에 맞춰 조향을 조절하고, 고속 급선회 또는 핸드브레이크에서 뒤 타이어의 접지 배율을 낮춘다. 슬립 각도가 클 때 제한적인 카운터스티어·회전 억제 보조를 적용한다. 강한 충돌 직후에는 주행 보조를 잠시 줄인다.

## 설정과 관찰

| 설정 묶음 | 대표 필드 | 의미 |
|---|---|---|
| 속도·가감속 | maxSpeedKph, reverseSpeedKph, acceleration, brakeAcceleration | 목표 속도는 km/h, 가감속은 m/s² |
| 조향 | turnRate, steeringResponse | 목표 회전 속도와 입력 응답 |
| 횡접지 | gripRecovery, maximumGripAcceleration | 옆미끄러짐 보정 시간과 힘 제한 |
| 뒤 접지 | oversteerStartKph, highSpeedRearGrip, handbrakeRearGrip, gripRestoreSeconds | 슬립 유도와 복원 |
| 자세 | centerOfMass | 초기화 때 Rigidbody에 적용하는 로컬 무게중심 |

현재 속도·SlipAngle·RearGrip·GroundedWheels는 공개 읽기 속성으로 확인한다. 고속 보간 상한 90 km/h와 일부 회전 보조 상수는 코드에 있다. 필드의 자세한 설명은 한국어 Tooltip이 원본이며, 이 문서의 설명을 특정 차종의 확정 튜닝값으로 사용하지 않는다.

변경 후 가속·정지·후진·정지 조향·공중 추진 차단·저속/고속/핸드브레이크 슬립·해제 후 접지 복원을 확인한다. [Validation](Validation.md)의 기존 배치 검사와 실제 주행 감각 확인을 구분한다.
