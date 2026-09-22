# 차량 Editor 도구

한국어 인스펙터·설정 안내·씬 시각화를 바꿀 때 읽는다.

- 코드: [VehicleInspectors.cs](../Assets/Code/Editor/Vehicles/VehicleInspectors.cs)
- 범위: 입력·주행·서스펜션·효과·카메라의 CustomEditor, VehicleSceneGuides.
- Editor 폴더 아래에 두어 플레이어 코드와 분리한다.

## 인스펙터

KoreanVehicleInspector가 SerializedProperty와 한국어 라벨을 사용한다. Tooltip 설명은 런타임 코드의 필드 선언에 있다. 항목별 설명 펼치기와 휠 슬롯의 앞/뒤·좌/우 표기를 제공한다. 필드를 추가하면 직렬화 필드명은 유지하며 해당 라벨과 Tooltip을 함께 작성한다.

SerializedProperty 경로를 통해 Undo·다중 선택·프리팹 오버라이드를 유지한다. 설명과 시각화 표시 선택은 SessionState에 보관하므로 씬의 물리 설정값을 바꾸지 않는다.

## Scene 뷰 표시

차량을 선택하고 Gizmos를 켠다. 서스펜션 인스펙터에서 도형·라벨·무게중심 표시를 조절한다.

| 표시 | 의미 |
|---|---|
| 하늘색 원 | 기준 휠 반경 |
| 노란 선·옅은 원 | 압축·신장 한계 |
| 흰 점선 | 지면 검사 레이 |
| 자홍색 십자 | 무게중심 |
| 실행 중 초록/주황/빨강 | 접지/슬립/공중 |

무게중심은 편집 중 설정값, 실행 중 Rigidbody의 실제 값을 표시한다. 도형은 물리 치수 안내이며 모델을 리사이즈하지 않는다.

변경 시 컴파일 외에 좁은 Inspector의 가독성, 설명 펼치기, 다중 선택·Undo·프리팹 값 유지, Scene 뷰 치수 표시를 확인한다.

이 파일은 Inspector/Gizmo 도구다. 별도로 구현한 [Unity Editor Bridge](UnityEditorBridge.md)가 조회·프리팹 검사·일괄 변경·시험 명령을 제공한다. MCP 서버는 아직 없다. 배치 검사 진입점은 [Validation](Validation.md)에 있다.
