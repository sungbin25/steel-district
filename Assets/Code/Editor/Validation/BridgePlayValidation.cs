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
    [InitializeOnLoad]
    public static class BridgePlayValidation
    {
        private const string Key = "SteelDistrict.BridgePlayValidation.";
        static BridgePlayValidation()
        {
            if (Application.isBatchMode && File.Exists(".bridge-validation-copy"))
                EditorApplication.update += Tick;
        }
        public static void RunBatch()
        {
            if (!Application.isBatchMode || !File.Exists(".bridge-validation-copy"))
                throw new InvalidOperationException("별도 검증 복사본에서만 실행하세요.");
            EditorSceneManager.OpenScene(VehicleTestTrack.ScenePath, OpenSceneMode.Single);
            var replay = UnityEngine.Object.FindFirstObjectByType<VehicleReplayDriver>();
            replay.playOnStart = true;
            EditorSceneManager.MarkSceneDirty(replay.gameObject.scene);
            EditorSceneManager.SaveScene(replay.gameObject.scene);
            SessionState.SetString(Key + "phase", "starting");
            SessionState.SetFloat(Key + "started", (float)EditorApplication.timeSinceStartup);
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            string phase = SessionState.GetString(Key + "phase", "");
            if (string.IsNullOrEmpty(phase)) return;
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "started", 0);
                if (elapsed > 45) throw new InvalidOperationException("Play Mode 검증 시간 초과");
                if (phase == "stopping" && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    Directory.CreateDirectory("Logs");
                    string error = SessionState.GetString(Key + "error", "");
                    File.WriteAllText("Logs/BridgePlayValidation.txt", string.IsNullOrEmpty(error)
                        ? "PASS 실제 Play Mode 입력 재생\nPASS 차량 이동\nPASS 초기화 후 새 차량 하나로 재실행\nALL CHECKS PASSED"
                        : "FAIL " + error);
                    SessionState.EraseString(Key + "phase");
                    EditorApplication.Exit(string.IsNullOrEmpty(error) ? 0 : 1);
                    return;
                }
                if (!Application.isPlaying) return;
                var replay = UnityEngine.Object.FindFirstObjectByType<VehicleReplayDriver>();
                if (phase == "starting" && replay != null && replay.IsReplaying)
                {
                    SessionState.SetString(Key + "phase", "running");
                    SessionState.SetFloat(Key + "runningAt", (float)EditorApplication.timeSinceStartup);
                }
                else if (phase == "running" && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "runningAt", 0) > 4)
                {
                    var car = UnityEngine.Object.FindFirstObjectByType<ArcadeVehicleDrive>();
                    if (car == null || Vector3.Distance(car.transform.position, replay.spawnPosition) < 5)
                        throw new InvalidOperationException("입력 재생 후 차량 이동이 없습니다.");
                    SessionState.SetInt(Key + "oldCar", car.GetInstanceID());
                    replay.StartReplay();
                    SessionState.SetFloat(Key + "resetAt", (float)EditorApplication.timeSinceStartup);
                    SessionState.SetString(Key + "phase", "resetting");
                }
                else if (phase == "resetting" && EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "resetAt", 0) > 0.2)
                {
                    var cars = UnityEngine.Object.FindObjectsByType<ArcadeVehicleDrive>(FindObjectsSortMode.None);
                    if (cars.Length != 1 || cars[0].GetInstanceID() == SessionState.GetInt(Key + "oldCar", 0) ||
                        Vector3.Distance(cars[0].transform.position, replay.spawnPosition) > 0.2f || !replay.IsReplaying)
                        throw new InvalidOperationException("초기화/재생 또는 단일 차량 유지 실패");
                    SessionState.SetString(Key + "error", "");
                    SessionState.SetString(Key + "phase", "stopping");
                    EditorApplication.ExitPlaymode();
                }
            }
            catch (Exception e)
            {
                SessionState.SetString(Key + "error", e.Message);
                SessionState.SetString(Key + "phase", "stopping");
                SessionState.SetFloat(Key + "started", (float)EditorApplication.timeSinceStartup);
                if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            }
        }
    }
}

