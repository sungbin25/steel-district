using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SteelDistrict.Vehicles;

namespace SteelDistrict.Editor.Bridge
{
    public static class BridgeValidation
    {
        private static readonly StringBuilder report = new StringBuilder();
        public static void RunBatch()
        {
            if (!Application.isBatchMode || !File.Exists(".bridge-validation-copy"))
                throw new InvalidOperationException("표식이 있는 검증 복사본에서만 실행하세요.");
            bool ok = false;
            try
            {
                report.Clear();
                // 표식으로 보호된 복사본에서 이전 검증이 만든 고정 경로만 재생성합니다.
                AssetDatabase.DeleteAsset(VehicleTestTrack.ScenePath);
                AssetDatabase.DeleteAsset("Assets/Testing");
                EditorSceneManager.OpenScene("Assets/Scene/GameScene.unity", OpenSceneMode.Single);
                Scene active = SceneManager.GetActiveScene();
                string sourceHash = EditorBridge.Hash(File.ReadAllText(active.path));
                var marker = new GameObject("UnSaved_User_Marker");
                EditorSceneManager.MarkSceneDirty(active);
                var status = Call("editor.status");
                Require(status.ok && status.scenes.Any(x => x.path == active.path && x.dirty), "미저장 씬 상태 조회");
                Require(BridgeCommands.Execute(new BridgeRequest { command = "object.inspect", targets = new[] { new BridgeTarget { sceneHandle = active.handle, hierarchyPath = marker.name } } }).objects.Count == 1, "sceneHandle로 미저장 객체 조회");
                var source = new BridgeTarget { assetPath = active.path, hierarchyPath = "Simple Retro Car" };
                var inspect = BridgeCommands.Execute(new BridgeRequest { command = "vehicle.inspect", targets = new[] { source }, fields = new[] { "ArcadeVehicleDrive.maxSpeedKph" } });
                Require(inspect.ok && inspect.values.Count > 0, "실제 차량 검사·필드 조회");
                var preview = BridgeCommands.Execute(new BridgeRequest { command = "track.create", targets = new[] { source } });
                Require(preview.ok && !File.Exists(VehicleTestTrack.ScenePath), "시험장 dry run 비변경");
                var create = BridgeCommands.Execute(new BridgeRequest { command = "track.create", targets = new[] { source }, apply = true });
                Require(create.ok && File.Exists(VehicleTestTrack.ScenePath), "평지·요철·경사·벽 시험장 생성: " + string.Join("; ", create.issues.Select(x => x.code + ": " + x.message)));
                Require(SceneManager.GetActiveScene() == active && active.isDirty && marker != null, "활성 씬과 사용자 미저장 객체 유지");
                Require(sourceHash == EditorBridge.Hash(File.ReadAllText(active.path)), "GameScene 원본 파일 유지");
                var again = BridgeCommands.Execute(new BridgeRequest { command = "track.create", targets = new[] { source }, apply = true });
                Require(again.ok && again.changedCount == 0, "시험장 재실행 중복 생성 없음");
                var prefabTarget = new BridgeTarget { assetPath = VehicleTestTrack.VehiclePath };
                var prefabCheck = BridgeCommands.Execute(new BridgeRequest { command = "vehicle.inspect", targets = new[] { prefabTarget } });
                Require(prefabCheck.ok, "생성 프리팹 필수 참조: " + string.Join("; ", prefabCheck.issues.Where(x => x.severity == "error").Select(x => x.code + ": " + x.message)));
                string other = "Assets/Testing/Vehicles/Second_Test.prefab";
                Require(AssetDatabase.CopyAsset(VehicleTestTrack.VehiclePath, other), "두 번째 프리팹 시험 준비");
                var original = File.ReadAllText(VehicleTestTrack.VehiclePath);
                var apply = new BridgeRequest { command = "vehicle.apply", targets = new[] { prefabTarget, new BridgeTarget { assetPath = other } },
                    changes = new[] { new BridgeChange { component = "ArcadeVehicleDrive", property = "maxSpeedKph", value = "131" } } };
                var dry = BridgeCommands.Execute(apply);
                Require(dry.ok && dry.changedCount == 0 && dry.values.Count == 2 && original == File.ReadAllText(VehicleTestTrack.VehiclePath), "일괄 dry run 값 반환·파일 불변");
                apply.apply = true;
                apply.changes[0].checkExpected = true;
                apply.changes[0].expectedValue = "-99";
                Require(!BridgeCommands.Execute(apply).ok && original == File.ReadAllText(VehicleTestTrack.VehiclePath), "기대값 불일치 시 전체 적용 차단");
                apply.changes[0].checkExpected = false;
                apply.changes[0].value = "-1";
                Require(!BridgeCommands.Execute(apply).ok, "필드 최소 범위 검사");
                apply.changes[0].value = "131";
                var changed = BridgeCommands.Execute(apply);
                Require(changed.ok && changed.changedCount == 2, "두 프리팹 지정 필드 적용");
                Require(ReadSpeed(VehicleTestTrack.VehiclePath) == 131 && ReadSpeed(other) == 131, "실제 적용값 확인");
                string appliedFile = File.ReadAllText(VehicleTestTrack.VehiclePath);
                var noOp = BridgeCommands.Execute(apply);
                Require(noOp.ok && noOp.changedCount == 0 && appliedFile == File.ReadAllText(VehicleTestTrack.VehiclePath), "같은 값 재적용 시 재저장 없음");
                Undo.PerformUndo();
                Require(ReadSpeed(VehicleTestTrack.VehiclePath) != 131 && ReadSpeed(other) != 131, "일괄 변경 Undo");
                PrefabUtility.SavePrefabAsset(AssetDatabase.LoadAssetAtPath<GameObject>(VehicleTestTrack.VehiclePath));
                PrefabUtility.SavePrefabAsset(AssetDatabase.LoadAssetAtPath<GameObject>(other));
                var forbidden = new BridgeRequest { command = "vehicle.apply", apply = true, targets = new[] { prefabTarget },
                    changes = new[] { new BridgeChange { component = "ArcadeVehicleDrive", property = "m_Script", value = "0" } } };
                Require(!BridgeCommands.Execute(forbidden).ok, "허용되지 않은 필드 차단");
                forbidden.changes = new[] { new BridgeChange { component = "VehicleSuspension", property = "groundMask", value = "0" } };
                Require(!BridgeCommands.Execute(forbidden).ok, "빈 접지 레이어 변경 차단");
                Require(!BridgeCommands.Execute(new BridgeRequest { command = "vehicle.apply", targets = new[] { new BridgeTarget { assetPath = "Assets/../outside.prefab" } }, changes = apply.changes }).ok, "외부 경로 차단");
                Debug.LogWarning("Bridge validation log");
                Debug.LogWarning("Bridge validation log");
                var console = BridgeCommands.Execute(new BridgeRequest { command = "editor.console", level = "Warning" });
                Require(console.logs.Any(x => x.message == "Bridge validation log" && x.count == 2), "콘솔 수집·중복 집계");

                // 전송 경로도 실제 JSON 파일 요청/응답으로 검증합니다.
                var wire = new BridgeRequest { id = "wire_" + Guid.NewGuid().ToString("N"), command = "editor.status" };
                string wireJson = JsonUtility.ToJson(wire);
                File.WriteAllText(Path.Combine(EditorBridge.Root, "requests", wire.id + ".json"), wireJson);
                EditorBridge.PumpForValidation();
                var response = JsonUtility.FromJson<BridgeResponse>(File.ReadAllText(Path.Combine(EditorBridge.Root, "responses", wire.id + ".json")));
                Require(response.ok && response.requestHash == EditorBridge.Hash(wireJson), "JSON 요청·응답과 요청 해시");
                File.WriteAllText(Path.Combine(EditorBridge.Root, "requests", wire.id + ".json"), wireJson);
                EditorBridge.PumpForValidation();
                Require(!File.Exists(Path.Combine(EditorBridge.Root, "requests", wire.id + ".json")), "동일 id 완료 요청 재사용");
                var replay = VehicleReplayValidation.Run(new BridgeRequest { command = "drive.test", scenario = "suite", repeat = 2 });
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/BridgeReplayResult.json", JsonUtility.ToJson(replay, true));
                Require(replay.ok && replay.tests.Count == 10, "5개 코스 × 2회 반복: " + string.Join("; ", replay.issues.Where(x => x.severity == "error").Select(x => x.message)));
                var custom = VehicleReplayValidation.Run(new BridgeRequest { command = "drive.test", scenario = "custom", repeat = 2,
                    steps = new[] { new SteelDistrict.Testing.VehicleReplayStep { ticks = 50, throttle = 0.5f } } });
                Require(custom.ok && custom.tests.Count == 2, "사용자 입력 목록 재생");
                report.AppendLine("ALL CHECKS PASSED");
                ok = true;
            }
            catch (Exception e) { report.AppendLine(e.ToString()); Debug.LogException(e); }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BridgeValidation.txt", report.ToString());
            EditorApplication.Exit(ok ? 0 : 1);
        }
        private static BridgeResponse Call(string command) => BridgeCommands.Execute(new BridgeRequest { command = command });
        private static float ReadSpeed(string path)
        {
            using (var serialized = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<ArcadeVehicleDrive>()))
                return serialized.FindProperty("maxSpeedKph").floatValue;
        }
        private static void Require(bool value, string message)
        {
            report.AppendLine((value ? "PASS " : "FAIL ") + message);
            if (!value) throw new InvalidOperationException(message);
        }
        public static void RunWorker()
        {
            if (!Application.isBatchMode || !File.Exists(".bridge-validation-copy")) throw new InvalidOperationException("검증 복사본이 아닙니다.");
            double deadline = EditorApplication.timeSinceStartup + 600;
            EditorApplication.update += () => { if (EditorApplication.timeSinceStartup > deadline || File.Exists(".bridge-stop-worker")) EditorApplication.Exit(0); };
        }
    }
}
