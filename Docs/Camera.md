# 차량 카메라

시점·거리·높이·가림 처리를 바꿀 때 읽는다.

- 코드: [VehicleFollowCamera.cs](../Assets/Code/Runtime/Camera/VehicleFollowCamera.cs)
- 대상: GameScene의 Main Camera.
- 입력: target Transform, targetBody Rigidbody. 주행 명령과 별개인 V·Tab InputAction을 사용한다.
- 현재 네임스페이스는 기존 타입 보존을 위해 SteelDistrict.Vehicles다.

## 동작

LateUpdate에서 UpdateCamera를 실행한다. 탑다운은 고정된 월드 방향에서 차량 속도를 이용해 주시점과 높이를 보정한다. 추적 시점은 차량 방향을 완화해서 뒤쪽을 따라가며 SphereCast로 장애물을 검사한다.

V로 탑다운/추적을 전환한다. Tab을 누르는 동안 추적 시점은 반대 방향을 보고, 탑다운은 속도 기반 앞보기 방향을 뒤쪽으로 바꾼다. OnEnable의 첫 갱신은 위치를 바로 맞추고 이후에는 부드럽게 따라간다.

## 설정과 경계

topDownHeight·topDownAngle은 탑다운 높이·각도, chaseDistance·chaseHeight는 주시점 기준 추적 거리·높이, followTime은 추적 완화 시간이다. obstructionMask는 추적 시점 가림 검사에만 사용한다.

카메라는 차량의 힘이나 조향을 수정하지 않는다. 현재 차량 교체·씬 전환 시 target 재연결 서비스와 카메라 설정 저장은 없다. 후속 구현 시 [SaveAndScene](SaveAndScene.md)의 카메라 소유권과 함께 설계한다.

검증: V 왕복 전환, Tab 유지/해제, 차량 화면 내 유지, 벽 앞 가림 처리, 빠른 방향 전환의 가독성. 배치 카메라 좌표 검사와 실제 영상의 품질 검사는 별개다.
