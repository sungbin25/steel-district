using System;
using System.IO;
using System.Linq;
using SteelDistrict.Testing;
using SteelDistrict.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SteelDistrict.Editor.Bridge
{
    public static class VehicleReplayValidation
    {
        public const float Dt = 0.02f;
        public static void RunBatch()
        {
            if (!Application.isBatchMode || !File.Exists("bridge-worker-request.json"))
                throw new InvalidOperationException("별도 복사본의 브리지 batch worker에서만 실행하세요.");
            var request = JsonUtility.FromJson<BridgeRequest>(File.ReadAllText("bridge-worker-request.json"));
            BridgeResponse response;
            try { response = Run(request); }
            catch (Exception e)
            {
                response = new BridgeResponse { id = request.id, command = request.command };
                response.Error("drive.test", "TEST_FAILED", e.ToString());
            }
            response.Finish();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BridgeReplayResult.json", JsonUtility.ToJson(response, true));
            EditorApplication.Exit(response.ok ? 0 : 1);
        }
        public static BridgeResponse Run(BridgeRequest request)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("사용자 Editor에서는 물리 시험을 실행하지 않습니다.");
            var result = new BridgeResponse { id = request.id, command = request.command, utc = DateTime.UtcNow.ToString("O"), editorVersion = Application.unityVersion };
            ValidateRequest(request);
            string prefabPath = request.targets.Length == 0 ? VehicleTestTrack.VehiclePath : BridgeCommands.AssetPath(request.targets[0].assetPath, ".prefab");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new InvalidOperationException("시험 프리팹이 없습니다: " + prefabPath);
            BridgeCommands.InspectVehicle(prefab, prefabPath, result);
            if (result.issues.Any(x => x.severity == "error")) { result.Finish(); return result; }
            result.values.Add(new BridgeValue { target = prefabPath, property = "dependencyHash", before = AssetDatabase.GetAssetDependencyHash(prefabPath).ToString() });
            result.values.Add(new BridgeValue { target = "simulation", property = "fixedDeltaTime", before = "0.02" });
            EditorSceneManager.OpenScene(VehicleTestTrack.ScenePath, OpenSceneMode.Single);
            var previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var names = request.scenario == "suite" ? new[] { "flat", "bumps", "slope", "wall", "drift" } : new[] { request.scenario };
            try
            {
                for (int run = 1; run <= request.repeat; run++)
                    foreach (string name in names)
                    {
                        var metric = RunCase(prefab, name, run, request.steps);
                        result.tests.Add(metric);
                        result.checkedCount++;
                        if (metric.passed) result.passedCount++;
                        else result.Error(prefabPath, "SCENARIO_FAILED", name + ": " + metric.error);
                    }
                foreach (string name in names)
                {
                    var group = result.tests.Where(x => x.scenario == name).ToArray();
                    for (int i = 1; i < group.Length; i++)
                        if (Mathf.Abs(group[i].distance - group[0].distance) > 0.05f ||
                            Mathf.Abs(group[i].maxSpeedKph - group[0].maxSpeedKph) > 0.1f ||
                            Mathf.Abs(group[i].maxSlip - group[0].maxSlip) > 0.2f)
                            result.Error(name, "REPEAT_DIVERGED", "동일 입력 반복의 거리/속도/슬립 허용 오차를 넘었습니다.");
                }
            }
            finally { Physics.simulationMode = previous; }
            result.Warning("drive.test", "MANUAL_SIMULATION", "고정 스텝 수동 물리 검증입니다. 실제 키 입력, 화면 품질, 모바일 FPS 검증이 아닙니다.");
            result.Finish();
            return result;
        }
        internal static void ValidateRequest(BridgeRequest request)
        {
            if (request.repeat < 1 || request.repeat > 3) throw new ArgumentException("repeat는 1~3입니다.");
            if (request.targets == null) request.targets = Array.Empty<BridgeTarget>();
            if (request.targets.Length > 1) throw new ArgumentException("시험 프리팹은 한 번에 1개를 지정하세요.");
            if (request.targets.Length == 1) BridgeCommands.AssetPath(request.targets[0].assetPath, ".prefab");
            if (!new[] { "suite", "flat", "bumps", "slope", "wall", "drift", "custom" }.Contains(request.scenario))
                throw new ArgumentException("알 수 없는 시험 시나리오입니다.");
            if (request.scenario == "custom")
            {
                if (request.steps == null || request.steps.Length == 0 || request.steps.Length > 100 ||
                    request.steps.Any(x => x == null || x.ticks < 1 || x.ticks > 3000 ||
                        !Finite(x.throttle) || !Finite(x.brake) || !Finite(x.steering) ||
                        x.throttle < 0 || x.throttle > 1 || x.brake < 0 || x.brake > 1 || Mathf.Abs(x.steering) > 1) ||
                    request.steps.Sum(x => (long)x.ticks) > 3000)
                    throw new ArgumentException("custom 입력은 유한한 정상 범위의 값과 총 1~3000 ticks가 필요합니다.");
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static ReplayResult RunCase(GameObject prefab, string name, int run, VehicleReplayStep[] custom)
        {
            var metric = new ReplayResult { scenario = name, run = run };
            var car = UnityEngine.Object.Instantiate(prefab);
            float lane = name == "bumps" ? 30 : name == "slope" ? 60 : name == "wall" ? 90 : 0;
            var start = new Vector3(lane, 0.05f, 0);
            car.transform.SetPositionAndRotation(start, Quaternion.identity);
            var body = car.GetComponent<Rigidbody>();
            var drive = car.GetComponent<ArcadeVehicleDrive>();
            try
            {
                body.interpolation = RigidbodyInterpolation.None;
                drive.SendMessage("Awake");
                car.GetComponent<VehicleDriveInput>().enabled = false;
                var effects = car.GetComponent<VehicleTireEffects>();
                if (effects != null) effects.enabled = false;
                Physics.SyncTransforms();
                for (int i = 0; i < 100; i++) { drive.SimulateStep(default, Dt); Physics.Simulate(Dt); }
                if (drive.GroundedWheels != 4) throw new InvalidOperationException("초기 네 바퀴 접지 실패");
                var origin = body.position;
                body.linearVelocity = Vector3.forward * (name == "drift" ? 18 : name == "wall" ? 12 : name == "bumps" || name == "slope" ? 8 : 0);
                VehicleReplayStep[] steps = custom;
                if (name == "flat") steps = new[] { new VehicleReplayStep { ticks = 100, throttle = 1 }, new VehicleReplayStep { ticks = 150, brake = 1, brakePressed = true } };
                if (name == "bumps" || name == "slope" || name == "wall") steps = new[] { new VehicleReplayStep { ticks = 150, throttle = 0.35f } };
                if (name == "drift") steps = new[] { new VehicleReplayStep { ticks = 45, throttle = 0.5f, steering = 0.85f, handbrake = true }, new VehicleReplayStep { ticks = 200, throttle = 0.25f } };
                foreach (var step in steps)
                    for (int tick = 0; tick < step.ticks; tick++)
                    {
                        drive.SimulateStep(step.Command(tick == 0), Dt);
                        Physics.Simulate(Dt);
                        drive.Suspension.UpdateVisuals(Dt);
                        metric.ticks++;
                        if (drive.GroundedWheels > 0) metric.groundedTicks++;
                        metric.maxSpeedKph = Mathf.Max(metric.maxSpeedKph, body.linearVelocity.magnitude * 3.6f);
                        metric.maxHeight = Mathf.Max(metric.maxHeight, body.position.y);
                        metric.maxSlip = Mathf.Max(metric.maxSlip, Mathf.Abs(drive.SlipAngle));
                        foreach (var wheel in drive.Suspension.Contacts) metric.maxCompression = Mathf.Max(metric.maxCompression, wheel.Compression);
                        if (!Finite(body.position.y) || body.position.y < -5) throw new InvalidOperationException("비정상 위치 또는 지면 이탈");
                    }
                metric.finalSpeedKph = body.linearVelocity.magnitude * 3.6f;
                metric.distance = Vector3.Distance(origin, body.position);
                metric.endZ = body.position.z;
                metric.passed = name == "flat" ? metric.maxSpeedKph > 20 && metric.finalSpeedKph < 2 :
                    name == "bumps" ? metric.maxCompression > 0.035f && metric.endZ > 18 :
                    name == "slope" ? metric.maxHeight > 1 && metric.endZ > 12 :
                    name == "wall" ? metric.endZ < 21 && metric.endZ > 10 && metric.finalSpeedKph < 5 :
                    name == "drift" ? metric.maxSlip > 10 && metric.maxSlip < 70 && Mathf.Abs(drive.SlipAngle) < 6 && drive.RearGrip > 0.95f :
                    metric.groundedTicks > 0;
                if (!metric.passed) metric.error = "표준 합격 기준 미달. 프리팹 튜닝과 측정값을 확인하세요.";
            }
            catch (Exception e) { metric.passed = false; metric.error = e.Message; }
            finally { UnityEngine.Object.DestroyImmediate(car); Physics.SyncTransforms(); }
            return metric;
        }
    }
}

