# 타이어 연기와 자국

타이어 효과·슬립 임계값·렌더링을 바꿀 때 읽는다.

- 코드: [VehicleTireEffects.cs](../Assets/Code/Runtime/Vehicles/Effects/VehicleTireEffects.cs)
- 입력: VehicleSuspension.Contacts. 물리 힘을 적용하지 않는다.
- 소스: `Assets/Unity Asset/PROMETEO - Car Controller/Effects/`의 `TireSmoke_PS` ParticleSystem과 `TireSkid` TrailRenderer 프리팹.

## 출력 조건

Start에서 바퀴별 프리팹 인스턴스를 생성하고 LateUpdate에서 갱신한다. 접지·하중·슬립 속도가 조건을 만족할 때 켠다. Space 키 자체를 효과의 조건으로 사용하지 않아 급선회·제동·외력에도 반응한다.

`slipStartSpeed`는 시작, `slipStopSpeed`는 유지 중 종료 기준이다. 종료값을 시작값보다 낮게 둔다. 단위는 차량 전진 속도가 아닌 타이어 상대 미끄러짐 속도 m/s다. 방출량은 SlipAmount로 최소 5와 smokeRate 사이를 보간한다.

연기는 월드 공간에서 재생하고 자국은 접지점에서 지면 법선 방향으로 배치한다. 위치가 크게 불연속이면 이전 자국을 비운다. 정지·공중 상태에서 새 효과를 출력하지 않으며 비활성화 시 효과를 정리한다.

## 렌더링과 수명

기존 Trail 형상·폭·수명을 재사용하고 HDRP/Unlit 재질을 런타임에 만든다. OnDestroy에서 이 재질을 해제한다. 프리팹 원본을 수정해 차종별 복사본을 늘리지 않는다. 현재는 차량별 네 쌍의 인스턴스이며 전역 풀 시스템은 아니다.

자국은 얇은 Trail 메시이므로 양면 렌더링을 사용한다. 런타임 재질에 `_DoubleSidedEnable=1`, `_CullMode=Off`를 설정한다. HDRP/Unlit의 기본 Back 컬링을 유지하면 메시가 생성되어 Scene 선택 윤곽에는 보이더라도 실제 카메라에서는 뒷면으로 제거될 수 있다. 접지점의 법선 방향 0.018 m 띄우기는 유지한다.

변경 시 슬립 시작/종료, 핸드브레이크 없이 횡외력으로 미끄러질 때의 출력, 공중·정지 차단, 연기 입자 생성과 자국의 지면 배치를 확인한다. 배치 검사만으로 HDRP 실제 색상과 화면 품질을 검증했다고 보고하지 않는다.
