# 서스펜션과 휠

바퀴 위치·크기·접지·차체 출렁임을 바꿀 때 읽는다.

- 코드: [VehicleSuspension.cs](../Assets/Code/Runtime/Vehicles/Suspension/VehicleSuspension.cs)
- 타입: VehicleSuspension, VehicleWheelContact.
- 물리 호출자: ArcadeVehicleDrive. 외형 갱신: 자체 LateUpdate.

## 좌표와 접지

바퀴 배열 순서는 **앞 왼쪽 → 앞 오른쪽 → 뒤 왼쪽 → 뒤 오른쪽**이다. 각 Transform의 원점을 휠 중심으로 사용하고 Initialize에서 기준 위치·회전을 저장한다. 현재 계산은 차량 월드 스케일 (1, 1, 1)을 기준으로 한다.

기준 중심에서 위로 travel만큼 올린 지점에서 아래로 `wheelRadius + 2 × travel` 길이의 레이를 쏜다. 자기 차량·자식 Collider·트리거·법선이 너무 기울어진 면을 제외하고 가장 가까운 유효 지면을 사용한다.

- wheelRadius: 반경(m). 메시 크기를 바꾸지 않는다.
- travel: 기준 중심에서 위·아래 각각의 이동량(m). 전체 이동 범위는 2배다.
- springFrequency / dampingRatio: 스프링 강성과 진동 억제.
- antiRoll: 같은 차축 양쪽이 접지했을 때 좌우 압축 차이를 억제.
- groundMask: 지면 검사 대상 레이어.

## 데이터와 갱신

Contacts의 Grounded·Point·Normal·GroundVelocity·Hub·Compression·Load를 계산한다. 비접지 바퀴는 최대 신장 상태다. 스프링과 댐퍼 힘을 차체에 적용하고 움직이는 지면에는 반작용을 적용한다.

LateUpdate의 UpdateVisuals가 압축량·조향각·회전 속도로 모델을 갱신한다. TryGetRestWheelCenter는 [EditorTools](EditorTools.md)의 시각화가 초기 기준 중심을 읽는 데 사용한다. 주행에서 이미 호출하므로 별도의 FixedUpdate 물리 호출을 추가하지 않는다.

변경 시 평지 네 바퀴 접지, 한 바퀴 요철 압축과 모델 이동, 공중 최대 신장, 자기 Collider 배제, Scene 뷰 반경과 실제 타이어 외곽을 확인한다.
