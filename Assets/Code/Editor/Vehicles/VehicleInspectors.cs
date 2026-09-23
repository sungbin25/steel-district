using System.Collections.Generic;
using SteelDistrict.Vehicles;
using UnityEditor;
using UnityEngine;

namespace SteelDistrict.Editor
{
    // SerializedProperty로 그려 기존 값·Undo·프리팹 오버라이드·다중 선택 편집을 유지합니다.
    public abstract class KoreanVehicleInspector : UnityEditor.Editor
    {
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "input", "운전 입력" },
            { "suspension", "서스펜션" },
            { "driveType", "구동 방식" },
            { "maxSpeedKph", "전진 목표 최고속도 (km/h)" },
            { "reverseSpeedKph", "후진 목표 최고속도 (km/h)" },
            { "acceleration", "가속도 (m/s²)" },
            { "brakeAcceleration", "브레이크 감속도 (m/s²)" },
            { "coastingDeceleration", "자연 감속도 (m/s²)" },
            { "turnRate", "기준 회전 속도 (°/s)" },
            { "steeringResponse", "조향 응답 시간 (s)" },
            { "gripRecovery", "횡미끄러짐 보정 시간 (s)" },
            { "maximumGripAcceleration", "최대 접지 가속도 (m/s²)" },
            { "oversteerStartKph", "오버스티어 시작 속도 (km/h)" },
            { "highSpeedRearGrip", "고속 뒤 타이어 접지 배율" },
            { "handbrakeRearGrip", "핸드브레이크 뒤 접지 배율" },
            { "gripRestoreSeconds", "접지 회복 시간 기준 (s)" },
            { "handbrakeDeceleration", "핸드브레이크 감속도 (m/s²)" },
            { "centerOfMass", "무게중심 (로컬 m)" },
            { "wheels", "바퀴 모델 4개" },
            { "groundMask", "접지 검사 레이어" },
            { "wheelRadius", "휠 반경 (m)" },
            { "travel", "편측 서스펜션 이동량 (m)" },
            { "springFrequency", "스프링 고유진동수 (Hz)" },
            { "dampingRatio", "댐핑 비율" },
            { "antiRoll", "좌우 기울기 억제 계수" },
            { "smokePrefab", "타이어 연기 프리팹" },
            { "skidPrefab", "타이어 자국 프리팹" },
            { "smokeRate", "최대 연기 방출량 (개/s)" },
            { "slipStartSpeed", "효과 시작 미끄러짐 속도 (m/s)" },
            { "slipStopSpeed", "효과 종료 미끄러짐 속도 (m/s)" },
            { "target", "추적할 차량" },
            { "targetBody", "차량 Rigidbody" },
            { "viewMode", "시작 시점" },
            { "topDownHeight", "탑다운 기본 높이 (m)" },
            { "topDownAngle", "탑다운 내려다보는 각도 (°)" },
            { "chaseDistance", "추적 거리 (m)" },
            { "chaseHeight", "추적 높이 (m)" },
            { "followTime", "카메라 추적 완화 시간 (s)" },
            { "obstructionMask", "카메라 장애물 레이어" },
        };
        internal static readonly string[] WheelNames = { "앞 왼쪽", "앞 오른쪽", "뒤 왼쪽", "뒤 오른쪽" };

        public override void OnInspectorGUI()
        {
            // 긴 한국어 이름이 기본 라벨 폭에서 잘리지 않도록 인스펙터 폭에 맞춥니다.
            float previousWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.58f, 180f, 280f);
            try { DrawProperties(); }
            finally { EditorGUIUtility.labelWidth = previousWidth; }
        }

        private void DrawProperties()
        {
            serializedObject.Update();
            bool explain = SessionState.GetBool("SteelDistrict.Vehicle.Explain", false);
            explain = EditorGUILayout.ToggleLeft("항목별 한국어 설명 펼치기 (마우스를 올려도 표시)", explain);
            SessionState.SetBool("SteelDistrict.Vehicle.Explain", explain);
            var property = serializedObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.PropertyField(property, new GUIContent("스크립트"));
                    continue;
                }
                string label = Labels.TryGetValue(property.name, out string korean) ? korean : property.displayName;
                if (property.name == "wheels")
                    DrawWheels(property, label);
                else
                    EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
                if (explain && !string.IsNullOrEmpty(property.tooltip))
                    EditorGUILayout.HelpBox(property.tooltip, MessageType.None);
            }
            serializedObject.ApplyModifiedProperties();
            DrawExtra();
        }

        private static void DrawWheels(SerializedProperty property, string label)
        {
            // 배열 구조는 그대로 유지하고 각 슬롯의 역할만 번역합니다.
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), false);
            if (!property.isExpanded) return;
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(property.FindPropertyRelative("Array.size"), new GUIContent("바퀴 수 (4개 필요)"));
                for (int i = 0; i < property.arraySize; i++)
                    EditorGUILayout.PropertyField(property.GetArrayElementAtIndex(i),
                        new GUIContent(i < 4 ? WheelNames[i] : "추가 슬롯 " + i));
            }
        }

        protected virtual void DrawExtra() { }
    }

    [CustomEditor(typeof(ArcadeVehicleDrive)), CanEditMultipleObjects]
    public sealed class ArcadeVehicleDriveInspector : KoreanVehicleInspector
    {
        protected override void DrawExtra()
        {
            if (!Application.isPlaying || targets.Length != 1) return;
            var drive = (ArcadeVehicleDrive)target;
            EditorGUILayout.HelpBox(
                $"현재 속도 {drive.SpeedKph:F1} km/h  |  접지 {drive.GroundedWheels}/4\n" +
                $"미끄러짐 각도 {drive.SlipAngle:F1}°  |  뒤 접지 배율 {drive.RearGrip:F2}", MessageType.None);
        }
        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }

    [CustomEditor(typeof(VehicleSuspension)), CanEditMultipleObjects]
    public sealed class VehicleSuspensionInspector : KoreanVehicleInspector
    {
        protected override void DrawExtra()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("씬 뷰 시각화", EditorStyles.boldLabel);
            DrawToggle("SteelDistrict.Vehicle.Geometry", "휠 반경과 서스펜션 범위 표시");
            DrawToggle("SteelDistrict.Vehicle.Labels", "바퀴 이름·치수·접지 상태 표시");
            DrawToggle("SteelDistrict.Vehicle.CenterOfMass", "무게중심 표시");
            EditorGUILayout.HelpBox(
                "차량을 선택하고 Scene 뷰의 Gizmos를 켜세요.\n" +
                "하늘색 원: 기준 휠 반경 / 노란 선과 옅은 원: 압축·늘어남 한계\n" +
                "흰 점선: 지면 검사 레이 / 자홍색 십자: 무게중심\n" +
                "플레이 중 초록: 접지 / 주황: 미끄러짐 / 빨강: 공중\n" +
                "표시는 물리 치수 안내이며 휠 모델의 크기를 바꾸지 않습니다.", MessageType.Info);
            foreach (var selected in targets)
            {
                var suspension = (VehicleSuspension)selected;
                if ((suspension.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
                    EditorGUILayout.HelpBox(suspension.name + ": 서스펜션은 차량의 월드 스케일 (1, 1, 1)을 기준으로 계산합니다.", MessageType.Warning);
            }
            var wheels = serializedObject.FindProperty("wheels");
            if (wheels.arraySize != 4)
                EditorGUILayout.HelpBox("바퀴 수는 정확히 4개여야 주행할 수 있습니다.", MessageType.Warning);
        }

        private static void DrawToggle(string key, string label)
        {
            bool value = SessionState.GetBool(key, true);
            bool next = EditorGUILayout.ToggleLeft(label, value);
            if (next == value) return;
            SessionState.SetBool(key, next);
            SceneView.RepaintAll();
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
    }

    [CustomEditor(typeof(VehicleTireEffects)), CanEditMultipleObjects]
    public sealed class VehicleTireEffectsInspector : KoreanVehicleInspector
    {
        protected override void DrawExtra()
        {
            var start = serializedObject.FindProperty("slipStartSpeed");
            var stop = serializedObject.FindProperty("slipStopSpeed");
            if (!start.hasMultipleDifferentValues && !stop.hasMultipleDifferentValues && stop.floatValue >= start.floatValue)
                EditorGUILayout.HelpBox("효과 종료 속도를 시작 속도보다 작게 설정하세요.", MessageType.Warning);
        }
    }

    [CustomEditor(typeof(VehicleFollowCamera)), CanEditMultipleObjects]
    public sealed class VehicleFollowCameraInspector : KoreanVehicleInspector { }

    [CustomEditor(typeof(VehicleDriveInput)), CanEditMultipleObjects]
    public sealed class VehicleDriveInputInspector : KoreanVehicleInspector
    {
        protected override void DrawExtra()
        {
            EditorGUILayout.HelpBox("W / ↑: 가속   S / ↓: 제동·후진\nA·D / ←·→: 조향   Space: 핸드브레이크\n정지 후 S를 놓았다 다시 누르면 후진합니다.\n카메라 V: 시점 전환   Tab 누르기: 뒤쪽 보기", MessageType.Info);
        }
    }

    // 에디터 전용 도형입니다. 플레이어 빌드나 물리 계산에는 포함되지 않습니다.
    public static class VehicleSceneGuides
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        private static void DrawSuspension(VehicleSuspension suspension, GizmoType gizmoType)
        {
            if (!SessionState.GetBool("SteelDistrict.Vehicle.Geometry", true)) return;
            bool labels = SessionState.GetBool("SteelDistrict.Vehicle.Labels", true);
            bool live = Application.isPlaying && suspension.IsReady && suspension.isActiveAndEnabled;
            Vector3 up = suspension.transform.up;
            Vector3 axle = suspension.transform.right;
            float radius = suspension.Radius, travel = suspension.Travel;
            using (new Handles.DrawingScope(Color.white))
            {
                for (int i = 0; i < 4; i++)
                {
                    if (!suspension.TryGetRestWheelCenter(i, out Vector3 rest)) continue;
                    var contact = suspension.Contacts[i];
                    Vector3 top = rest + up * travel, bottom = rest - up * travel;
                    Vector3 end = bottom - up * radius;
                    // 초기 휠 중심을 기준으로 그려 압축된 모델 때문에 표시 범위가 따라 움직이지 않게 합니다.
                    Handles.color = new Color(0.25f, 0.85f, 1f, 0.9f);
                    Handles.DrawWireDisc(rest, axle, radius);
                    Handles.DrawLine(rest, rest + up * radius);
                    Handles.color = new Color(1f, 0.8f, 0.15f, 0.4f);
                    Handles.DrawWireDisc(top, axle, radius);
                    Handles.DrawWireDisc(bottom, axle, radius);
                    Handles.color = Color.yellow;
                    Handles.DrawLine(top, bottom);
                    Handles.DrawLine(top - axle * 0.1f, top + axle * 0.1f);
                    Handles.DrawLine(bottom - axle * 0.1f, bottom + axle * 0.1f);
                    Handles.color = Color.white;
                    Handles.DrawDottedLine(top, end, 4f);
                    if (live)
                    {
                        Handles.color = !contact.Grounded ? Color.red :
                            contact.SlipAmount > 0 ? new Color(1f, 0.45f, 0.05f) : Color.green;
                        Vector3 current = rest + up * contact.Compression;
                        Vector3 steeredAxle = Quaternion.AngleAxis(contact.SteeringAngle, up) * axle;
                        Handles.DrawWireDisc(current, steeredAxle, radius);
                        if (contact.Grounded)
                        {
                            Handles.DrawWireDisc(contact.Point, contact.Normal, 0.1f);
                            Handles.DrawLine(contact.Point, contact.Point + contact.Normal * 0.25f);
                        }
                    }
                    if (labels)
                    {
                        string status = live ? (contact.Grounded ?
                            $"\n접지 / 압축 {contact.Compression:F2} m / 미끄러짐 {contact.SlipSpeed:F2} m/s" : "\n공중 / 최대 늘어남") : "";
                        Handles.Label(top + up * (radius + 0.08f),
                            $"{KoreanVehicleInspector.WheelNames[i]}  반경 {radius:F3} m (지름 {radius * 2:F3} m)\n" +
                            $"이동 ±{travel:F2} m (전체 {2 * travel:F2} m){status}", EditorStyles.helpBox);
                    }
                }
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
        private static void DrawCenterOfMass(ArcadeVehicleDrive drive, GizmoType gizmoType)
        {
            if (!SessionState.GetBool("SteelDistrict.Vehicle.CenterOfMass", true)) return;
            // 실행 중에는 Rigidbody에 실제 적용된 무게중심을 표시합니다.
            Vector3 center;
            if (Application.isPlaying && drive.Body != null)
                center = drive.Body.worldCenterOfMass;
            else
            {
                using (var serialized = new SerializedObject(drive))
                    center = drive.transform.position + drive.transform.rotation * serialized.FindProperty("centerOfMass").vector3Value;
            }
            using (new Handles.DrawingScope(Color.magenta))
            {
                Handles.DrawWireDisc(center, drive.transform.up, 0.14f);
                Handles.DrawWireDisc(center, drive.transform.right, 0.14f);
                Handles.DrawLine(center - drive.transform.right * 0.22f, center + drive.transform.right * 0.22f);
                Handles.DrawLine(center - drive.transform.up * 0.22f, center + drive.transform.up * 0.22f);
                Handles.DrawLine(center - drive.transform.forward * 0.22f, center + drive.transform.forward * 0.22f);
                if (SessionState.GetBool("SteelDistrict.Vehicle.Labels", true))
                    Handles.Label(center + drive.transform.up * 0.25f,
                        Application.isPlaying ? "실제 무게중심" : "무게중심 (시작 시 적용)", EditorStyles.helpBox);
            }
        }
    }
}
