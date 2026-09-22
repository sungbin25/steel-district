# 차량 입력

조작키·포커스 처리·명령 전달을 바꿀 때 읽는다.

- 코드: [VehicleDriveInput.cs](../Assets/Code/Runtime/Vehicles/Input/VehicleDriveInput.cs)
- 타입: `VehicleDriveInput`, `VehicleDriveCommand`.
- 의존성: Unity Input System. 명령 소비자: [Driving](Driving.md).

## 책임과 흐름

Awake에서 InputActionMap과 키 바인딩을 만들고 Update에서 명령 스냅샷을 갱신한다. 주행의 FixedUpdate가 ConsumeCommand로 가져간다. `BrakePressed`는 제동 버튼의 새 누름이며 소비 후 한 번만 초기화된다. 나머지 값은 다음 입력 갱신까지 유지된다.

| 입력 | 명령 |
|---|---|
| W / 위 화살표 | Throttle |
| S / 아래 화살표 | Brake와 새 누름 BrakePressed |
| A·D / 좌우 화살표 | Steering (-1~1) |
| Space | Handbrake |

포커스 상실·비활성화·일시정지 시 잔여 명령을 비운다. OnDestroy에서 InputActionMap을 해제한다. 제동과 후진의 전환 판단은 주행 모듈이 맡는다. V·Tab은 카메라 모듈의 입력이다.

## 변경과 확인

시험장 재생기는 SetReplayCommand로 고정 스텝 명령을 주입하고 ClearReplayCommand로 키보드 경로를 복원한다. 재생 중에는 키보드 Update 샘플링을 대신한다. [TestTrack](TestTrack.md)을 참조한다. AI·네트워크·터치 입력원과 입력 재지정 UI는 아직 없다.

추가할 때 주행 물리와 입력 수집을 섞지 않는다. 짧은 S 재입력이 FixedUpdate 사이에서 사라지지 않는지, 포커스를 잃거나 비활성화한 뒤 입력이 남지 않는지 확인한다. 기존 배치 검사는 명령을 직접 전달하므로 실제 키 이벤트 검증을 대체하지 않는다.
