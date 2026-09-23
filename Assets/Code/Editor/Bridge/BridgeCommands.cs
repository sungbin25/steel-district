using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using SteelDistrict.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SteelDistrict.Editor.Bridge
{
    public static class BridgeCommands
    {
        // 배포된 브리지 코드가 컴파일·로드되었는지 editor.status의 logScope로 확인합니다. 브리지 코드 변경 시 갱신합니다.
        public const string Version = "2026-09-23.10";
        public static readonly string[] Names = { "bridge.commands", "editor.status", "editor.console", "object.inspect", "vehicle.inspect", "vehicle.apply", "track.create", "drive.test", "menu.setup", "menu.rebuildPreviews", "editor.refresh",
            "menu.setupShop", "menu.checkShop", "menu.materializeParking", "menu.repairUIReferences", "menu.materializeUI", "menu.inspectUI", "menu.inspectShowroom", "menu.setupShowroom", "vehicle.organizePrefabs", "scene.hierarchy", "scene.edit", "scene.open", "scene.close", "scene.new", "scene.save" };
        internal static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>
        {
            { "ArcadeVehicleDrive", typeof(ArcadeVehicleDrive) }, { "VehicleSuspension", typeof(VehicleSuspension) },
            { "VehicleTireEffects", typeof(VehicleTireEffects) }, { "VehicleDriveInput", typeof(VehicleDriveInput) },
            { "VehicleFollowCamera", typeof(VehicleFollowCamera) }, { "Rigidbody", typeof(Rigidbody) }
        };
        public static BridgeResponse Execute(BridgeRequest request)
        {
            var result = new BridgeResponse { id = request.id, command = request.command, dryRun = !request.apply,
                utc = DateTime.UtcNow.ToString("O"), editorVersion = Application.unityVersion };
            try
            {
                if (request.version != 1) throw new ArgumentException("지원하지 않는 프로토콜 버전입니다.");
                if (!Names.Contains(request.command)) throw new ArgumentException("알 수 없는 명령입니다.");
                if (request.command == "bridge.commands") result.commands.AddRange(Names);
                else if (request.command == "editor.status") Status(result);
                else if (request.command == "editor.console") EditorBridge.ReadLogs(request, result);
                else if (request.command == "editor.refresh") Refresh(request, result);
                else
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                        throw new InvalidOperationException("컴파일/에셋 갱신 중입니다. 완료 후 새 요청으로 실행하세요.");
                    if (request.command == "vehicle.apply") Apply(request, result);
                    else if (request.command == "track.create") VehicleTestTrack.Create(request, result);
                    else if (request.command == "vehicle.organizePrefabs") VehiclePrefabOrganization.Run(request, result);
                    else if (request.command == "menu.setupShowroom") VehicleShowroomSetup.Run(request, result);
                    else if (request.command == "menu.inspectShowroom") VehicleShowroomSetup.Inspect(result);
                    else if (request.command == "menu.materializeParking") VehicleShowroomSetup.Materialize(request,result);
                    else if (request.command == "menu.setupShop") VehicleShopSetup.Apply(request,result);
                    else if (request.command == "menu.checkShop") VehicleShopSetup.Check(result);
                    else if (request.command == "menu.materializeUI") MainMenuAuthoring.Apply(request,result);
                    else if (request.command == "menu.inspectUI") MainMenuAuthoring.Inspect(result);
                    else if (request.command == "menu.repairUIReferences") MainMenuAuthoring.RepairReferences(request,result);
                    else if (request.command == "menu.setup") MainMenuSetup.Create(request, result);
                    else if (request.command == "menu.rebuildPreviews") MainMenuSetup.RebuildPreviews(request, result);
                    else if (request.command.StartsWith("scene.", StringComparison.Ordinal)) BridgeSceneCommands.Execute(request, result);
                    else if (request.command == "drive.test")
                        throw new InvalidOperationException("drive.test는 비동기 브리지 또는 검증용 batch 진입점에서 실행하세요.");
                    else
                    {
                        CheckTargets(request);
                        foreach (var target in request.targets)
                        {
                            try
                            {
                                using (var resolved = Resolve(target))
                                {
                                    result.checkedCount++;
                                    if (request.command == "vehicle.inspect")
                                    {
                                        int errors = result.issues.Count(x => x.severity == "error");
                                        InspectVehicle(resolved.Root, resolved.Label, result);
                                        if (errors == result.issues.Count(x => x.severity == "error")) result.passedCount++;
                                    }
                                    InspectObject(resolved.Root, resolved.Label, request, result);
                                }
                            }
                            catch (Exception e) { result.Error(target.assetPath ?? target.objectId, "TARGET_FAILED", e.Message); }
                        }
                    }
                }
            }
            catch (Exception e) { result.Error(request.command, "COMMAND_FAILED", e.Message); }
            result.Finish();
            return result;
        }

        // Editor가 백그라운드일 때 전체 Refresh가 외부 변경을 놓칠 수 있어, 지정한 파일은 직접 가져오고 스크립트면 컴파일을 요청합니다.
        private static void Refresh(BridgeRequest request, BridgeResponse result)
        {
            var paths = (request.targets ?? Array.Empty<BridgeTarget>()).Where(x => x != null && !string.IsNullOrEmpty(x.assetPath)).Select(x => x.assetPath).ToArray();
            if (paths.Length > 100) throw new ArgumentException("가져올 파일은 100개 이하로 지정하세요.");
            foreach (string path in paths)
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("\\") || path.Split('/').Any(x => x == ".." || x == "." || x.Length == 0) || !File.Exists(path))
                    throw new ArgumentException("가져올 파일은 Assets 내부의 존재하는 파일이어야 합니다: " + path);
            result.checkedCount = paths.Length;
            bool scripts = paths.Any(x => x.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));
            // delayCall은 Unity 창이 백그라운드일 때 실행되지 않아(2026-09-23 확인) 요청 처리 중에 바로 가져옵니다.
            // 컴파일과 도메인 재로드는 비동기이므로 이 요청의 응답은 먼저 기록됩니다.
            Debug.Log("[Bridge] refresh 시작: 파일 " + paths.Length + "개, 활성 창=" + UnityEditorInternal.InternalEditorUtility.isApplicationActive);
            foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (scripts) UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            result.compiling = EditorApplication.isCompiling;
        }

        [InitializeOnLoadMethod]
        private static void WatchCompilation()
        {
            UnityEditor.Compilation.CompilationPipeline.compilationStarted += _ => Debug.Log("[Bridge] 컴파일 시작");
            UnityEditor.Compilation.CompilationPipeline.compilationFinished += _ => Debug.Log("[Bridge] 컴파일 완료");
        }

        internal static void CheckTargets(BridgeRequest request)
        {
            if (request.targets == null || request.targets.Length == 0 || request.targets.Length > 100 || request.targets.Any(x => x == null))
                throw new ArgumentException("targets는 1~100개를 지정하세요.");
        }

        internal static string AssetPath(string value, string extension)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Contains("\\") || !value.StartsWith("Assets/", StringComparison.Ordinal) ||
                value.Split('/').Any(x => x == ".." || x == "." || x.Length == 0) ||
                !value.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Assets 내부의 정규 " + extension + " 경로가 필요합니다.");
            return value;
        }

        internal sealed class Resolved : IDisposable
        {
            public GameObject Root;
            public string Label;
            public Scene Preview;
            public void Dispose() { if (Preview.IsValid()) EditorSceneManager.ClosePreviewScene(Preview); }
        }
        internal static Resolved Resolve(BridgeTarget target)
        {
            var result = new Resolved();
            try
            {
                int selectors = (!string.IsNullOrEmpty(target.objectId) ? 1 : 0) + (!string.IsNullOrEmpty(target.assetPath) ? 1 : 0) + (target.sceneHandle != 0 ? 1 : 0);
                if (selectors != 1) throw new ArgumentException("assetPath, objectId, sceneHandle 중 하나만 지정하세요.");
                if (!string.IsNullOrEmpty(target.objectId))
                {
                    if (!GlobalObjectId.TryParse(target.objectId, out var id)) throw new ArgumentException("잘못된 objectId입니다.");
                    var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id);
                    result.Root = obj as GameObject ?? (obj as Component)?.gameObject;
                    result.Label = target.objectId;
                }
                else if (target.assetPath != null && target.assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                {
                    string path = AssetPath(target.assetPath, ".prefab");
                    result.Root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    result.Label = path;
                    if (!string.IsNullOrEmpty(target.hierarchyPath) && result.Root != null)
                        result.Root = result.Root.transform.Find(target.hierarchyPath)?.gameObject;
                }
                else
                {
                    string path = target.sceneHandle != 0 ? "<scene:" + target.sceneHandle + ">" : AssetPath(target.assetPath, ".unity");
                    Scene scene = target.sceneHandle != 0 ? FindScene(target.sceneHandle) : SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        if (target.sceneHandle != 0) throw new ArgumentException("현재 Editor에서 로드된 sceneHandle이 아닙니다.");
                        result.Preview = EditorSceneManager.OpenPreviewScene(path);
                        scene = result.Preview;
                    }
                    if (string.IsNullOrWhiteSpace(target.hierarchyPath)) throw new ArgumentException("씬 대상에는 정확한 hierarchyPath가 필요합니다.");
                    var matches = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                        .Where(x => Hierarchy(x) == target.hierarchyPath).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException("씬 경로가 없거나 중복됩니다: " + target.hierarchyPath);
                    result.Root = matches[0].gameObject;
                    result.Label = path + ":" + target.hierarchyPath;
                }
                if (result.Root == null) throw new InvalidOperationException("대상을 찾을 수 없습니다.");
                return result;
            }
            catch { result.Dispose(); throw; }
        }
        internal static string Hierarchy(Transform node) => node.parent == null ? node.name : Hierarchy(node.parent) + "/" + node.name;

        private static Scene FindScene(int handle)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.handle == handle) return scene;
            }
            return default;
        }

        private static void Status(BridgeResponse result)
        {
            result.compiling = EditorApplication.isCompiling;
            result.updating = EditorApplication.isUpdating;
            result.playing = EditorApplication.isPlayingOrWillChangePlaymode;
            result.paused = EditorApplication.isPaused;
            result.editorPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            result.logScope = "bridge " + Version + "; 활성 창=" + UnityEditorInternal.InternalEditorUtility.isApplicationActive;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                result.scenes.Add(new BridgeScene { handle = scene.handle, name = scene.name, path = scene.path, dirty = scene.isDirty,
                    loaded = scene.isLoaded, active = scene == SceneManager.GetActiveScene() });
            }
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) result.scenes.Add(new BridgeScene { name = "Prefab Stage", path = stage.assetPath, dirty = stage.scene.isDirty, loaded = true });
        }

        internal static void InspectObject(GameObject root, string label, BridgeRequest request, BridgeResponse result)
        {
            int limit = Mathf.Clamp(request.limit, 1, 200);
            int remaining = Mathf.Max(0, limit - result.objects.Count);
            foreach (var node in root.GetComponentsInChildren<Transform>(true).Take(remaining))
            {
                var entry = new BridgeObject { target = label + "/" + Hierarchy(node), name = node.name,
                    objectId = GlobalObjectId.GetGlobalObjectIdSlow(node.gameObject).ToString(), layer = node.gameObject.layer,
                    layerName = LayerMask.LayerToName(node.gameObject.layer), active = node.gameObject.activeInHierarchy,
                    dirty = EditorUtility.IsDirty(node.gameObject) };
                foreach (var component in node.GetComponents<Component>())
                {
                    if (component == null) { entry.components.Add("<Missing Script>"); continue; }
                    entry.components.Add(component.GetType().Name);
                    // 상세 값은 요청한 필드만 반환하여 모델·Transform 전체 직렬화를 피합니다.
                    if (request.fields == null || request.fields.Length == 0) continue;
                    using (var serialized = new SerializedObject(component))
                    {
                        foreach (string field in request.fields.Take(50))
                        {
                            string prefix = component.GetType().Name + ".";
                            if (!field.StartsWith(prefix, StringComparison.Ordinal)) continue;
                            var property = serialized.FindProperty(field.Substring(prefix.Length));
                            if (property != null)
                                result.values.Add(new BridgeValue { target = entry.target, component = component.GetType().Name,
                                    property = property.propertyPath, before = Read(property) });
                        }
                    }
                }
                result.objects.Add(entry);
            }
            if (root.GetComponentsInChildren<Transform>(true).Length > remaining)
                result.Warning(label, "TRUNCATED", "조회 개수 제한으로 일부 자식이 생략되었습니다.");
        }

        internal static void InspectVehicle(GameObject root, string label, BridgeResponse result)
        {
            foreach (Type type in new[] { typeof(Rigidbody), typeof(VehicleDriveInput), typeof(ArcadeVehicleDrive), typeof(VehicleSuspension), typeof(VehicleTireEffects) })
            {
                if (root.GetComponents(type).Length != 1) result.Error(label, "COMPONENT", type.Name + "이 루트에 정확히 1개 필요합니다.");
            }
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    result.Error(label + "/" + Hierarchy(child), "MISSING_SCRIPT", "끊어진 스크립트 참조입니다.");
            var body = root.GetComponent<Rigidbody>();
            if (body != null && (body.isKinematic || !body.useGravity || body.mass <= 0))
                result.Error(label, "RIGIDBODY", "동적 Rigidbody, 중력 사용, 양수 질량이 필요합니다.");
            // 프리팹 에셋은 물리 월드에 없으므로 attachedRigidbody 대신 계층 소유권을 확인합니다.
            if (!root.GetComponentsInChildren<Collider>(true).Any(x => x.enabled && !x.isTrigger &&
                (EditorUtility.IsPersistent(root) ? x.GetComponentInParent<Rigidbody>(true) == body : x.attachedRigidbody == body)))
                result.Error(label, "COLLIDER", "루트 Rigidbody에 연결된 활성 비트리거 Collider가 필요합니다.");
            if (root.GetComponentsInChildren<WheelCollider>(true).Length != 0)
                result.Error(label, "DUPLICATE_PHYSICS", "레이 기반 주행과 WheelCollider를 함께 사용하지 않습니다.");
            if ((root.transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
                result.Error(label, "SCALE", "차량 스케일은 (1,1,1)이어야 합니다.");
            var suspension = root.GetComponent<VehicleSuspension>();
            if (suspension != null)
            {
                using (var serialized = new SerializedObject(suspension))
                {
                    var wheels = serialized.FindProperty("wheels");
                    var seen = new HashSet<UnityEngine.Object>();
                    if (wheels.arraySize != 4) result.Error(label, "WHEELS", "휠 배열은 4개여야 합니다.");
                    for (int i = 0; i < wheels.arraySize; i++)
                    {
                        var wheel = wheels.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                        if (wheel == null || !wheel.IsChildOf(root.transform) || !seen.Add(wheel))
                            result.Error(label, "WHEEL_REFERENCE", "휠 " + i + ": 누락·외부 참조·중복입니다.");
                    }
                    int mask = serialized.FindProperty("groundMask").intValue;
                    if (mask == 0) result.Error(label, "GROUND_MASK", "접지 레이어가 비어 있습니다.");
                    if ((mask & (1 << root.layer)) != 0)
                        result.Warning(label, "VEHICLE_IN_GROUND_MASK", "접지 마스크에 차량 레이어가 포함됩니다. 자기 차량은 코드에서 제외하지만 다른 차량이 지면으로 잡힐 수 있습니다.");
                }
            }
            foreach (var component in new Component[] { root.GetComponent<ArcadeVehicleDrive>(), root.GetComponent<VehicleTireEffects>() })
            {
                if (component == null) continue;
                using (var serialized = new SerializedObject(component))
                {
                    foreach (string name in new[] { "input", "suspension" })
                    {
                        var property = serialized.FindProperty(name);
                        if (property?.objectReferenceValue is Component reference && reference.gameObject != root)
                            result.Error(label, "EXTERNAL_REFERENCE", component.GetType().Name + "." + name + "이 다른 차량을 참조합니다.");
                    }
                    if (component is VehicleTireEffects)
                        foreach (string name in new[] { "smokePrefab", "skidPrefab" })
                            if (serialized.FindProperty(name).objectReferenceValue == null)
                                result.Error(label, "EFFECT_REFERENCE", name + " 프리팹이 없습니다.");
                }
            }
        }

        private sealed class Plan
        {
            public GameObject root;
            public Component component;
            public BridgeValue value;
            public float number;
            public bool integer;
        }
        private static void Apply(BridgeRequest request, BridgeResponse result)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("프리팹 변경은 Edit Mode에서만 가능합니다.");
            CheckTargets(request);
            if (request.changes == null || request.changes.Length == 0 || request.changes.Length > 50)
                throw new ArgumentException("changes는 1~50개를 지정하세요.");
            var plans = new List<Plan>();
            var keys = new HashSet<string>();
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            foreach (var target in request.targets)
            {
                string path = target.assetPath;
                try
                {
                    AssetPath(path, ".prefab");
                    if (!string.IsNullOrEmpty(target.objectId) || !string.IsNullOrEmpty(target.hierarchyPath) || target.sceneHandle != 0)
                        throw new ArgumentException("일괄 변경은 프리팹 루트만 지원합니다.");
                    if (stage != null && stage.assetPath == path) throw new InvalidOperationException("열린 Prefab Stage를 먼저 닫으세요.");
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (root == null || PrefabUtility.GetPrefabAssetType(root) == PrefabAssetType.Model)
                        throw new InvalidOperationException("편집 가능한 프리팹이 아닙니다.");
                    if (EditorUtility.IsDirty(root) || root.GetComponents<Component>().Any(x => x != null && EditorUtility.IsDirty(x)))
                        throw new InvalidOperationException("저장되지 않은 프리팹 변경이 있습니다.");
                    if (!AssetDatabase.IsOpenForEdit(path)) throw new InvalidOperationException("프리팹이 쓰기 가능한 상태가 아닙니다.");
                    result.checkedCount++;
                    foreach (var change in request.changes)
                    {
                        string key = path + ":" + change.component + "." + change.property;
                        if (!keys.Add(key)) throw new ArgumentException("대상/필드 중복: " + key);
                        if (!Types.TryGetValue(change.component ?? "", out var type)) throw new ArgumentException("허용되지 않은 컴포넌트입니다.");
                        var component = root.GetComponent(type);
                        if (component == null) throw new ArgumentException("컴포넌트가 없습니다: " + change.component);
                        bool integer = false;
                        if (type == typeof(Rigidbody))
                        {
                            if (change.property != "m_Mass") throw new ArgumentException("Rigidbody는 m_Mass만 변경할 수 있습니다.");
                        }
                        else
                        {
                            var field = type.GetField(change.property ?? "", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (field == null || !Attribute.IsDefined(field, typeof(SerializeField)) ||
                                (field.FieldType != typeof(float) && field.FieldType != typeof(LayerMask)))
                                throw new ArgumentException("허용되지 않은 설정 필드입니다: " + change.property);
                            integer = field.FieldType == typeof(LayerMask);
                        }
                        if (!float.TryParse(change.value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || float.IsInfinity(number))
                            throw new ArgumentException("유한한 숫자가 필요합니다.");
                        if (integer && !int.TryParse(change.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                            throw new ArgumentException("레이어 마스크는 32비트 정수여야 합니다.");
                        if (integer && int.Parse(change.value, CultureInfo.InvariantCulture) == 0)
                            throw new ArgumentException("접지 레이어 마스크를 비울 수 없습니다.");
                        var reflected = type.GetField(change.property, BindingFlags.Instance | BindingFlags.NonPublic);
                        var min = reflected?.GetCustomAttribute<MinAttribute>();
                        var range = reflected?.GetCustomAttribute<RangeAttribute>();
                        if ((min != null && number < min.min) || (range != null && (number < range.min || number > range.max)) ||
                            (type == typeof(Rigidbody) && number <= 0))
                            throw new ArgumentException("필드의 허용 범위를 벗어났습니다: " + change.property);
                        using (var serialized = new SerializedObject(component))
                        {
                            var property = serialized.FindProperty(change.property);
                            if (property == null) throw new ArgumentException("직렬화 필드를 찾을 수 없습니다.");
                            string before = Read(property);
                            if (change.checkExpected && before != change.expectedValue)
                                throw new InvalidOperationException("기대값 불일치: " + key + " 실제=" + before);
                            string after = integer ? int.Parse(change.value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : number.ToString("R", CultureInfo.InvariantCulture);
                            var value = new BridgeValue { target = path, component = change.component, property = change.property, before = before, after = after, changed = before != after };
                            plans.Add(new Plan { root = root, component = component, value = value, number = number, integer = integer });
                            result.values.Add(value);
                        }
                    }
                }
                catch (Exception e) { result.Error(path, "PREFLIGHT_FAILED", e.Message); }
            }
            if (result.issues.Exists(x => x.severity == "error")) return;
            // 모든 대상의 사전 검사가 끝난 뒤 적용합니다. dry run은 메모리·파일을 바꾸지 않습니다.
            if (!request.apply) return;
            if (!plans.Any(x => x.value.changed)) { result.passedCount = request.targets.Length; return; }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("차량 설정 일괄 적용");
            var roots = plans.Where(x => x.value.changed).Select(x => x.root).Distinct().ToArray();
            var backups = roots.ToDictionary(x => x, x => File.ReadAllBytes(AssetDatabase.GetAssetPath(x)));
            try
            {
                foreach (var plan in plans.Where(x => x.value.changed))
                {
                    using (var serialized = new SerializedObject(plan.component))
                    {
                        var property = serialized.FindProperty(plan.value.property);
                        if (plan.integer) property.intValue = int.Parse(plan.value.after, CultureInfo.InvariantCulture);
                        else property.floatValue = plan.number;
                        serialized.ApplyModifiedProperties();
                    }
                }
                foreach (var root in roots)
                {
                    PrefabUtility.SavePrefabAsset(root, out bool saved);
                    if (!saved) throw new IOException("프리팹 저장 실패: " + AssetDatabase.GetAssetPath(root));
                }
                Undo.CollapseUndoOperations(group);
                result.changedCount = plans.Count(x => x.value.changed);
                result.passedCount = roots.Length;
            }
            catch (Exception e)
            {
                Undo.RevertAllDownToGroup(group);
                foreach (var pair in backups)
                {
                    string path = AssetDatabase.GetAssetPath(pair.Key);
                    File.WriteAllBytes(path, pair.Value);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
                result.changedCount = 0;
                result.Error("vehicle.apply", "ROLLED_BACK", e.Message + " 원본 프리팹을 복원했습니다.");
            }
        }

        internal static string Read(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: return property.floatValue.ToString("R", CultureInfo.InvariantCulture);
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask: return property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return property.boolValue ? "true" : "false";
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.Vector3: return JsonUtility.ToJson(property.vector3Value);
                case SerializedPropertyType.ObjectReference: return property.objectReferenceValue == null ? "null" : GlobalObjectId.GetGlobalObjectIdSlow(property.objectReferenceValue).ToString();
                default: return "<" + property.propertyType + ">";
            }
        }
    }
}
