using System;
using System.IO;
using SteelDistrict.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SteelDistrict.Editor.Bridge
{
    public static class VehicleTestTrack
    {
        public const string ScenePath = "Assets/Scene/Tests/VehicleTestTrack.unity";
        public const string VehiclePath = "Assets/Testing/Vehicles/SimpleRetroCar_Test.prefab";
        public const string MaterialPath = "Assets/Testing/Materials/TrackSurface.mat";
        internal static VehicleReplayStep[] DefaultSteps() => new[]
        {
            new VehicleReplayStep { ticks = 100 },
            new VehicleReplayStep { ticks = 100, throttle = 1 },
            new VehicleReplayStep { ticks = 45, throttle = 0.5f, steering = 0.85f, handbrake = true },
            new VehicleReplayStep { ticks = 160, throttle = 0.25f },
            new VehicleReplayStep { ticks = 150, brake = 1, brakePressed = true }
        };
        public static void Create(BridgeRequest request, BridgeResponse result)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("시험장 생성은 Edit Mode에서 실행하세요.");
            BridgeCommands.CheckTargets(request);
            if (request.targets.Length != 1) throw new ArgumentException("시험 차량 원본은 1개만 지정하세요.");
            using (var source = BridgeCommands.Resolve(request.targets[0]))
            {
                BridgeCommands.InspectVehicle(source.Root, source.Label, result);
                if (result.issues.Exists(x => x.severity == "error")) return;
                string[] paths = { ScenePath, VehiclePath, MaterialPath };
                int exists = 0;
                foreach (string path in paths) if (File.Exists(path)) exists++;
                if (exists == paths.Length)
                {
                    result.Warning(ScenePath, "ALREADY_EXISTS", "기존 시험장을 유지합니다. 생성 명령은 기존 튜닝과 씬을 덮어쓰지 않습니다.");
                    result.passedCount = 3;
                    return;
                }
                if (exists != 0) throw new InvalidOperationException("시험장 생성 경로에 일부 에셋이 이미 있습니다. 충돌을 먼저 해소하세요.");
                foreach (string path in paths) result.values.Add(new BridgeValue { target = path, before = "<absent>", after = "<create>", changed = true });
                if (!request.apply) return;
                var oldActive = SceneManager.GetActiveScene();
                // Preview Scene은 저장할 수 없으므로 새 Additive 씬만 생성·저장·닫습니다.
                // 기존 씬은 닫거나 저장하지 않으며 finally에서 활성 씬을 복원합니다.
                Scene preview = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                    Directory.CreateDirectory(Path.GetDirectoryName(VehiclePath));
                    Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                    AssetDatabase.Refresh();
                    var clone = UnityEngine.Object.Instantiate(source.Root);
                    SceneManager.MoveGameObjectToScene(clone, preview);
                    clone.name = "Simple Retro Car";
                    clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(clone, VehiclePath, out bool saved);
                    UnityEngine.Object.DestroyImmediate(clone);
                    if (!saved || prefab == null) throw new IOException("시험 차량 프리팹 저장에 실패했습니다.");
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
                    if (shader == null) throw new InvalidOperationException("시험장 재질 셰이더를 찾을 수 없습니다.");
                    var material = new Material(shader) { name = "TrackSurface" };
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.22f, 0.29f, 0.32f));
                    if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(0.22f, 0.29f, 0.32f));
                    AssetDatabase.CreateAsset(material, MaterialPath);
                    BuildGeometry(preview, material);
                    var rig = new GameObject("입력 재생 - 우클릭 메뉴로 초기화 및 재생");
                    SceneManager.MoveGameObjectToScene(rig, preview);
                    var replay = rig.AddComponent<VehicleReplayDriver>();
                    replay.vehiclePrefab = prefab;
                    replay.steps = DefaultSteps();
                    var cameraObject = new GameObject("Test Camera", typeof(Camera), typeof(AudioListener));
                    SceneManager.MoveGameObjectToScene(cameraObject, preview);
                    cameraObject.tag = "MainCamera";
                    cameraObject.transform.SetPositionAndRotation(new Vector3(0, 15, -10), Quaternion.Euler(55, 0, 0));
                    replay.followCamera = cameraObject.transform;
                    var lightObject = new GameObject("Test Sun", typeof(Light));
                    SceneManager.MoveGameObjectToScene(lightObject, preview);
                    var light = lightObject.GetComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 2;
                    lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
                    if (!EditorSceneManager.SaveScene(preview, ScenePath)) throw new IOException("시험장 씬 저장 실패");
                    result.changedCount = 3;
                    result.passedCount = 3;
                }
                catch
                {
                    foreach (string path in paths) if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
                    throw;
                }
                finally
                {
                    EditorSceneManager.CloseScene(preview, true);
                    if (oldActive.IsValid() && oldActive.isLoaded) SceneManager.SetActiveScene(oldActive);
                }
            }
        }
        private static void BuildGeometry(Scene scene, Material material)
        {
            Box(scene, material, "평지 - 전체 지면", new Vector3(45, -0.5f, 55), new Vector3(150, 1, 180), Quaternion.identity);
            for (int i = 0; i < 4; i++)
                Box(scene, material, "요철 " + (i + 1), new Vector3(30, 0.06f, 8 + i * 3), new Vector3(10, 0.12f, 0.6f), Quaternion.identity);
            Box(scene, material, "8도 경사", new Vector3(60, 1.42f, 15), new Vector3(12, 0.5f, 24), Quaternion.Euler(-8, 0, 0));
            Box(scene, material, "충돌 벽", new Vector3(90, 2, 22), new Vector3(12, 4, 1), Quaternion.identity);
            foreach (var lane in new[] { ("평지 시작", 0f), ("요철 시작", 30f), ("경사 시작", 60f), ("벽 시작", 90f) })
            {
                var marker = new GameObject(lane.Item1);
                SceneManager.MoveGameObjectToScene(marker, scene);
                marker.transform.position = new Vector3(lane.Item2, 0.05f, 0);
            }
        }
        private static void Box(Scene scene, Material material, string name, Vector3 position, Vector3 size, Quaternion rotation)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(obj, scene);
            obj.name = name;
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.localScale = size;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic);
        }
    }
}
