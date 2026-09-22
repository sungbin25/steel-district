using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SteelDistrict.Editor.Bridge
{
    /// <summary>
    /// 씬·프리팹 구조 편집 명령(scene.*).
    /// 모든 변경 명령은 apply=false 미리보기가 기본이며, 실패하면 요청 전체를 되돌립니다.
    /// 씬 편집은 Undo 그룹 하나로 묶여 Unity에서 Ctrl+Z 한 번으로 취소할 수 있습니다.
    /// </summary>
    internal static class BridgeSceneCommands
    {
        private const int MaxOperations = 100;

        internal static void Execute(BridgeRequest request, BridgeResponse result)
        {
            switch (request.command)
            {
                case "scene.hierarchy": Hierarchy(request, result); break;
                case "scene.edit": Edit(request, result); break;
                case "scene.open": Open(request, result); break;
                case "scene.close": Close(request, result); break;
                case "scene.new": New(request, result); break;
                case "scene.save": Save(request, result); break;
                default: throw new ArgumentException("알 수 없는 씬 명령입니다.");
            }
        }

        // ───────────────────────── 대상 확인 ─────────────────────────

        private static BridgeTarget Single(BridgeRequest request)
        {
            if (request.targets == null || request.targets.Length != 1 || request.targets[0] == null)
                throw new ArgumentException("targets는 정확히 1개를 지정하세요.");
            return request.targets[0];
        }

        private static bool IsPrefab(BridgeTarget target) =>
            (target.assetPath ?? "").EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        private static Scene FindScene(int handle)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.handle == handle) return scene;
            }
            return default;
        }

        private static Scene LoadedScene(BridgeTarget target)
        {
            if (!string.IsNullOrEmpty(target.objectId)) throw new ArgumentException("씬 명령에는 objectId 대신 assetPath 또는 sceneHandle을 지정하세요.");
            bool byHandle = target.sceneHandle != 0;
            if (byHandle == !string.IsNullOrEmpty(target.assetPath)) throw new ArgumentException("assetPath와 sceneHandle 중 하나만 지정하세요.");
            Scene scene = byHandle ? FindScene(target.sceneHandle) : SceneManager.GetSceneByPath(BridgeCommands.AssetPath(target.assetPath, ".unity"));
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("Editor에 로드된 씬이 아닙니다. scene.open으로 먼저 여세요.");
            return scene;
        }

        private static string SceneLabel(Scene scene) => string.IsNullOrEmpty(scene.path) ? "<scene:" + scene.handle + ">" : scene.path;

        // ───────────────────────── scene.hierarchy ─────────────────────────

        private static void Hierarchy(BridgeRequest request, BridgeResponse result)
        {
            var target = Single(request);
            if (!string.IsNullOrEmpty(target.hierarchyPath) || IsPrefab(target) || !string.IsNullOrEmpty(target.objectId))
            {
                using (var resolved = BridgeCommands.Resolve(target))
                {
                    result.checkedCount++;
                    BridgeCommands.InspectObject(resolved.Root, resolved.Label, request, result);
                }
                return;
            }
            Scene preview = default;
            try
            {
                Scene scene;
                if (target.sceneHandle != 0) scene = LoadedScene(target);
                else
                {
                    string path = BridgeCommands.AssetPath(target.assetPath, ".unity");
                    scene = SceneManager.GetSceneByPath(path);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        // 닫힌 씬은 읽기 전용 Preview Scene으로 열고 즉시 닫습니다.
                        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new FileNotFoundException("씬 파일이 없습니다: " + path);
                        preview = EditorSceneManager.OpenPreviewScene(path);
                        scene = preview;
                    }
                }
                string label = preview.IsValid() ? target.assetPath : SceneLabel(scene);
                int limit = Mathf.Clamp(request.limit, 1, 200);
                result.checkedCount = 1;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (result.objects.Count >= limit)
                    {
                        result.Warning(label, "TRUNCATED", "조회 개수 제한(" + limit + ")으로 나머지 객체를 생략했습니다. hierarchyPath로 범위를 좁히세요.");
                        break;
                    }
                    BridgeCommands.InspectObject(root, label, request, result);
                }
            }
            finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
        }

        // ───────────────────────── scene.open / close / new / save ─────────────────────────

        private static void Open(BridgeRequest request, BridgeResponse result)
        {
            var target = Single(request);
            string path = BridgeCommands.AssetPath(target.assetPath, ".unity");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) throw new FileNotFoundException("씬 파일이 없습니다: " + path);
            string mode = string.IsNullOrEmpty(request.mode) ? "additive" : request.mode.ToLowerInvariant();
            if (mode != "additive" && mode != "single") throw new ArgumentException("mode는 additive 또는 single입니다.");
            result.checkedCount = 1;
            var existing = SceneManager.GetSceneByPath(path);
            bool loaded = existing.IsValid() && existing.isLoaded;
            if (loaded && mode == "additive")
            {
                result.values.Add(new BridgeValue { target = path, component = "Scene", property = "loaded", before = "true", after = "true" });
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("씬 열기는 Edit Mode에서만 가능합니다.");
            if (mode == "single")
            {
                // 단일 모드는 열린 씬을 모두 닫으므로 저장하지 않은 작업이 있으면 거부합니다.
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty)
                        throw new InvalidOperationException("저장하지 않은 씬이 있어 single 전환을 거부합니다: " + SceneLabel(SceneManager.GetSceneAt(i)));
            }
            result.values.Add(new BridgeValue { target = path, component = "Scene", property = "loaded", before = loaded ? "true" : "false",
                after = "true(" + mode + ")", changed = true });
            if (!request.apply) return;
            var scene = EditorSceneManager.OpenScene(path, mode == "single" ? OpenSceneMode.Single : OpenSceneMode.Additive);
            if (!scene.IsValid()) throw new IOException("씬을 열지 못했습니다: " + path);
            result.changedCount = 1;
            result.scenes.Add(new BridgeScene { handle = scene.handle, path = scene.path, name = scene.name, loaded = true, dirty = scene.isDirty,
                active = scene == SceneManager.GetActiveScene() });
        }

        private static void Close(BridgeRequest request, BridgeResponse result)
        {
            var scene = LoadedScene(Single(request));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("씬 닫기는 Edit Mode에서만 가능합니다.");
            if (SceneManager.sceneCount <= 1) throw new InvalidOperationException("마지막으로 열린 씬은 닫을 수 없습니다.");
            if (scene.isDirty) throw new InvalidOperationException("저장하지 않은 변경이 있어 닫기를 거부합니다. scene.save 후 다시 요청하세요.");
            string label = SceneLabel(scene);
            result.checkedCount = 1;
            result.values.Add(new BridgeValue { target = label, component = "Scene", property = "loaded", before = "true", after = "false", changed = true });
            if (!request.apply) return;
            if (!EditorSceneManager.CloseScene(scene, true)) throw new IOException("씬을 닫지 못했습니다: " + label);
            result.changedCount = 1;
        }

        private static void New(BridgeRequest request, BridgeResponse result)
        {
            var target = Single(request);
            string path = BridgeCommands.AssetPath(target.assetPath, ".unity");
            if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null) throw new InvalidOperationException("이미 존재하는 경로입니다: " + path);
            string folder = path.Substring(0, path.LastIndexOf('/'));
            if (!AssetDatabase.IsValidFolder(folder)) throw new DirectoryNotFoundException("상위 폴더가 없습니다: " + folder);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("씬 생성은 Edit Mode에서만 가능합니다.");
            result.checkedCount = 1;
            result.values.Add(new BridgeValue { target = path, component = "Scene", property = "exists", before = "false", after = "true(additive, empty)", changed = true });
            if (!request.apply) return;
            // 기존 열린 씬을 건드리지 않도록 빈 씬을 추가 모드로 만들고 바로 저장합니다.
            var previousActive = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            // 추가 모드 생성이 활성 씬을 바꾸면 사용자의 원래 활성 씬으로 되돌립니다.
            if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                EditorSceneManager.CloseScene(scene, true);
                throw new IOException("새 씬을 저장하지 못했습니다: " + path);
            }
            result.changedCount = 1;
            result.scenes.Add(new BridgeScene { handle = scene.handle, path = scene.path, name = scene.name, loaded = true, dirty = scene.isDirty });
        }

        private static void Save(BridgeRequest request, BridgeResponse result)
        {
            var scene = LoadedScene(Single(request));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("씬 저장은 Edit Mode에서만 가능합니다.");
            if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("저장 경로가 없는 씬입니다. scene.new로 만든 씬을 사용하세요.");
            result.checkedCount = 1;
            result.values.Add(new BridgeValue { target = scene.path, component = "Scene", property = "dirty", before = scene.isDirty ? "true" : "false",
                after = "false", changed = scene.isDirty });
            if (!request.apply || !scene.isDirty) return;
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패: " + scene.path);
            result.changedCount = 1;
        }

        // ───────────────────────── scene.edit ─────────────────────────

        private sealed class EditContext
        {
            public Scene Scene;
            public GameObject PrefabRoot;
            public bool UseUndo;
            public string Label;
            public BridgeResponse Result;
            public bool IsPrefab => PrefabRoot != null;
        }

        private static void Edit(BridgeRequest request, BridgeResponse result)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("씬·프리팹 편집은 Edit Mode에서만 가능합니다.");
            var target = Single(request);
            if (!string.IsNullOrEmpty(target.hierarchyPath) || !string.IsNullOrEmpty(target.objectId))
                throw new ArgumentException("scene.edit 대상은 씬(assetPath/sceneHandle) 또는 프리팹 assetPath만 지정합니다. 객체 경로는 operations에 적으세요.");
            var operations = request.operations;
            if (operations == null || operations.Length == 0 || operations.Length > MaxOperations || operations.Any(x => x == null || string.IsNullOrEmpty(x.op)))
                throw new ArgumentException("operations는 op가 있는 1~" + MaxOperations + "개를 지정하세요.");
            if (IsPrefab(target)) EditPrefab(target, request, result);
            else EditScene(target, request, result);
        }

        private static void EditScene(BridgeTarget target, BridgeRequest request, BridgeResponse result)
        {
            var scene = LoadedScene(target);
            string label = SceneLabel(scene);
            bool wasDirty = scene.isDirty;
            if (request.apply && request.save)
            {
                if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("저장 경로가 없는 씬입니다.");
                // 사용자의 미저장 작업까지 함께 저장하지 않도록 거부합니다.
                if (wasDirty) throw new InvalidOperationException("요청 전부터 저장하지 않은 변경이 있어 save=true를 거부합니다. Unity에서 먼저 저장하거나 save=false로 적용하세요.");
            }
            result.checkedCount = 1;
            var context = new EditContext { Scene = scene, UseUndo = true, Label = label, Result = result };
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bridge scene.edit " + request.id);
            bool failed = false;
            try { Run(context, request.operations); }
            catch (Exception e) { failed = true; result.Error(label, "OPERATION_FAILED", e.Message); }

            if (failed || !request.apply)
            {
                // 미리보기와 실패는 같은 경로로 되돌립니다. 미리보기 결과는 실제 적용과 같은 검사를 거친 값입니다.
                Undo.RevertAllDownToGroup(group);
                if (failed && request.apply) result.Error(label, "ROLLED_BACK", "요청의 모든 작업을 되돌렸습니다.");
                if (!wasDirty && scene.isDirty)
                    result.Warning(label, "DIRTY_AFTER_REVERT", "내용은 원래대로 되돌렸지만 씬이 변경됨으로 표시됩니다. 저장하지 않았습니다.");
                return;
            }
            Undo.CollapseUndoOperations(group);
            result.changedCount = result.values.Count(x => x.changed);
            if (result.changedCount == 0) return;
            EditorSceneManager.MarkSceneDirty(scene);
            if (request.save)
            {
                if (!EditorSceneManager.SaveScene(scene)) result.Error(label, "SAVE_FAILED", "변경은 Editor 메모리에 남아 있고 저장하지 못했습니다.");
            }
            else result.Warning(label, "NOT_SAVED", "Editor 메모리에만 적용했습니다. Ctrl+Z로 취소하거나 scene.save 또는 save=true로 저장하세요.");
        }

        private static void EditPrefab(BridgeTarget target, BridgeRequest request, BridgeResponse result)
        {
            string path = BridgeCommands.AssetPath(target.assetPath, ".prefab");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Model)
                throw new InvalidOperationException("편집 가능한 프리팹이 아닙니다: " + path);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path) throw new InvalidOperationException("열린 Prefab Stage를 먼저 닫으세요.");
            if (!AssetDatabase.IsOpenForEdit(path)) throw new InvalidOperationException("프리팹이 쓰기 가능한 상태가 아닙니다.");
            result.checkedCount = 1;
            // 격리된 프리팹 내용에서 작업하고, 성공한 apply만 파일에 저장합니다.
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var context = new EditContext { PrefabRoot = root, Scene = root.scene, UseUndo = false, Label = path, Result = result };
                try { Run(context, request.operations); }
                catch (Exception e) { result.Error(path, "OPERATION_FAILED", e.Message + " 프리팹 파일은 변경하지 않았습니다."); return; }
                int changed = result.values.Count(x => x.changed);
                if (!request.apply || changed == 0) return;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) { result.Error(path, "SAVE_FAILED", "프리팹 저장에 실패했습니다."); return; }
                result.changedCount = changed;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Run(EditContext context, BridgeOperation[] operations)
        {
            for (int i = 0; i < operations.Length; i++)
            {
                var operation = operations[i];
                try { RunOne(context, operation); }
                catch (Exception e) { throw new InvalidOperationException("#" + i + " " + operation.op + ": " + e.Message, e); }
            }
        }

        private static void RunOne(EditContext context, BridgeOperation op)
        {
            switch (op.op)
            {
                case "create": Create(context, op); break;
                case "delete": Delete(context, op); break;
                case "rename": Rename(context, op); break;
                case "setParent": SetParent(context, op); break;
                case "setActive": SetActive(context, op); break;
                case "setLayer": SetLayer(context, op); break;
                case "setTag": SetTag(context, op); break;
                case "transform": SetTransform(context, op); break;
                case "addComponent": AddComponent(context, op); break;
                case "removeComponent": RemoveComponent(context, op); break;
                case "set": SetProperty(context, op); break;
                default: throw new ArgumentException("알 수 없는 op입니다. create/delete/rename/setParent/setActive/setLayer/setTag/transform/addComponent/removeComponent/set 중 하나를 사용하세요.");
            }
        }

        // ───────────────────────── 경로 ─────────────────────────

        private static IEnumerable<Transform> AllTransforms(EditContext context) => context.IsPrefab
            ? context.PrefabRoot.GetComponentsInChildren<Transform>(true)
            : context.Scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true));

        private static string PathOf(EditContext context, Transform node)
        {
            if (!context.IsPrefab) return BridgeCommands.Hierarchy(node);
            if (node == context.PrefabRoot.transform) return "";
            string path = node.name;
            for (var parent = node.parent; parent != null && parent != context.PrefabRoot.transform; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }

        private static Transform FindOrNull(EditContext context, string path)
        {
            if (context.IsPrefab && string.IsNullOrEmpty(path)) return context.PrefabRoot.transform;
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path가 필요합니다.");
            var matches = AllTransforms(context).Where(x => PathOf(context, x) == path).Take(2).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("같은 경로의 객체가 여러 개입니다: " + path);
            return matches.Length == 1 ? matches[0] : null;
        }

        private static Transform Find(EditContext context, string path) =>
            FindOrNull(context, path) ?? throw new InvalidOperationException("객체를 찾을 수 없습니다: " + path);

        private static Transform ParentOf(EditContext context, string parent)
        {
            if (string.IsNullOrEmpty(parent)) return context.IsPrefab ? context.PrefabRoot.transform : null;
            return Find(context, parent);
        }

        private static string Join(string parent, string name) => string.IsNullOrEmpty(parent) ? name : parent + "/" + name;

        private static string ValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Contains("/")) throw new ArgumentException("name은 비어 있지 않고 '/'가 없어야 합니다.");
            return name;
        }

        private static void Record(EditContext context, string target, string component, string property, string before, string after)
        {
            context.Result.values.Add(new BridgeValue { target = context.Label + ":" + target, component = component, property = property,
                before = before, after = after, changed = before != after });
        }

        // ───────────────────────── 객체 작업 ─────────────────────────

        private static void Create(EditContext context, BridgeOperation op)
        {
            string name = ValidName(op.name);
            var parent = ParentOf(context, op.parent);
            string path = context.IsPrefab && parent == context.PrefabRoot.transform ? name : Join(op.parent, name);
            if (FindOrNull(context, path) != null)
            {
                if (op.optional) { Record(context, path, "GameObject", "exists", "true", "true"); return; }
                throw new InvalidOperationException("이미 존재합니다: " + path + " (재실행을 허용하려면 optional=true)");
            }
            GameObject created;
            string source;
            if (!string.IsNullOrEmpty(op.prefab))
            {
                string prefabPath = BridgeCommands.AssetPath(op.prefab, ".prefab");
                if (context.IsPrefab && prefabPath == context.Label) throw new ArgumentException("프리팹 자신을 중첩할 수 없습니다.");
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ?? throw new FileNotFoundException("프리팹이 없습니다: " + prefabPath);
                created = (GameObject)PrefabUtility.InstantiatePrefab(asset, context.Scene);
                if (created == null) throw new InvalidOperationException("프리팹 인스턴스를 만들지 못했습니다.");
                source = "prefab:" + prefabPath;
            }
            else if (!string.IsNullOrEmpty(op.primitive))
            {
                if (!Enum.TryParse(op.primitive, true, out PrimitiveType primitive)) throw new ArgumentException("primitive는 Cube/Sphere/Capsule/Cylinder/Plane/Quad 중 하나입니다.");
                created = GameObject.CreatePrimitive(primitive);
                source = "primitive:" + primitive;
            }
            else
            {
                created = new GameObject();
                source = "empty";
            }
            created.name = name;
            if (parent != null) created.transform.SetParent(parent, false);
            else if (created.scene != context.Scene) SceneManager.MoveGameObjectToScene(created, context.Scene);
            if (context.UseUndo) Undo.RegisterCreatedObjectUndo(created, "Bridge create");
            ApplyTransform(created.transform, op, null, null);
            Record(context, path, "GameObject", "exists", "false", "true(" + source + ")");
        }

        private static void Delete(EditContext context, BridgeOperation op)
        {
            var node = FindOrNull(context, op.path);
            if (node == null)
            {
                if (op.optional) { Record(context, op.path, "GameObject", "exists", "false", "false"); return; }
                throw new InvalidOperationException("객체를 찾을 수 없습니다: " + op.path);
            }
            if (context.IsPrefab && node == context.PrefabRoot.transform) throw new InvalidOperationException("프리팹 루트는 삭제할 수 없습니다.");
            var gameObject = node.gameObject;
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject) && !PrefabUtility.IsOutermostPrefabInstanceRoot(gameObject))
                throw new InvalidOperationException("프리팹 인스턴스 내부 객체는 삭제할 수 없습니다. 원본 프리팹을 편집하거나 인스턴스 루트를 삭제하세요.");
            if (context.UseUndo) Undo.DestroyObjectImmediate(gameObject);
            else Object.DestroyImmediate(gameObject);
            Record(context, op.path, "GameObject", "exists", "true", "false");
        }

        private static void Rename(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            string name = ValidName(op.name);
            if (context.IsPrefab && node == context.PrefabRoot.transform) throw new InvalidOperationException("프리팹 루트 이름은 파일 이름을 따릅니다.");
            if (node.name == name) { Record(context, op.path, "GameObject", "name", name, name); return; }
            string parentPath = node.parent == null || (context.IsPrefab && node.parent == context.PrefabRoot.transform) ? "" : PathOf(context, node.parent);
            if (FindOrNull(context, Join(parentPath, name)) != null) throw new InvalidOperationException("같은 이름의 형제 객체가 있습니다: " + name);
            if (context.UseUndo) Undo.RecordObject(node.gameObject, "Bridge rename");
            string before = node.name;
            node.name = name;
            Record(context, op.path, "GameObject", "name", before, name);
        }

        private static void SetParent(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            if (context.IsPrefab && node == context.PrefabRoot.transform) throw new InvalidOperationException("프리팹 루트는 이동할 수 없습니다.");
            if (PrefabUtility.IsPartOfPrefabInstance(node) && !PrefabUtility.IsOutermostPrefabInstanceRoot(node.gameObject))
                throw new InvalidOperationException("프리팹 인스턴스 내부 객체는 이동할 수 없습니다.");
            var parent = ParentOf(context, op.parent);
            if (parent != null && (parent == node || parent.IsChildOf(node))) throw new InvalidOperationException("자기 자신 또는 자식 아래로 이동할 수 없습니다.");
            string before = node.parent == null ? "" : PathOf(context, node.parent);
            string after = parent == null ? "" : PathOf(context, parent);
            if (node.parent == parent) { Record(context, op.path, "Transform", "parent", before, after); return; }
            if (FindOrNull(context, context.IsPrefab && parent == context.PrefabRoot.transform ? node.name : Join(after, node.name)) != null)
                throw new InvalidOperationException("이동할 위치에 같은 이름의 객체가 있습니다: " + node.name);
            // 월드 위치를 유지합니다(Unity Hierarchy 끌어놓기와 같은 동작).
            if (context.UseUndo) Undo.SetTransformParent(node, parent, "Bridge setParent");
            else node.SetParent(parent, true);
            Record(context, op.path, "Transform", "parent", before, after);
        }

        private static void SetActive(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            bool value = ParseBool(op.value);
            string before = node.gameObject.activeSelf ? "true" : "false";
            if (node.gameObject.activeSelf != value)
            {
                if (context.UseUndo) Undo.RecordObject(node.gameObject, "Bridge setActive");
                node.gameObject.SetActive(value);
            }
            Record(context, op.path, "GameObject", "activeSelf", before, value ? "true" : "false");
        }

        private static void SetLayer(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            int layer = int.TryParse(op.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : LayerMask.NameToLayer(op.value ?? "");
            if (layer < 0 || layer > 31) throw new ArgumentException("존재하지 않는 레이어입니다: " + op.value);
            string before = LayerText(node.gameObject.layer);
            if (node.gameObject.layer != layer)
            {
                if (context.UseUndo) Undo.RecordObject(node.gameObject, "Bridge setLayer");
                node.gameObject.layer = layer;
            }
            Record(context, op.path, "GameObject", "layer", before, LayerText(layer));
        }

        private static string LayerText(int layer)
        {
            string name = LayerMask.LayerToName(layer);
            return string.IsNullOrEmpty(name) ? layer.ToString(CultureInfo.InvariantCulture) : layer + ":" + name;
        }

        private static void SetTag(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            if (string.IsNullOrEmpty(op.value) || !UnityEditorInternal.InternalEditorUtility.tags.Contains(op.value))
                throw new ArgumentException("Tag Manager에 없는 태그입니다: " + op.value);
            string before = node.gameObject.tag;
            if (before != op.value)
            {
                if (context.UseUndo) Undo.RecordObject(node.gameObject, "Bridge setTag");
                node.gameObject.tag = op.value;
            }
            Record(context, op.path, "GameObject", "tag", before, op.value);
        }

        private static void SetTransform(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            if (string.IsNullOrEmpty(op.position) && string.IsNullOrEmpty(op.rotation) && string.IsNullOrEmpty(op.scale))
                throw new ArgumentException("position, rotation, scale 중 하나 이상을 지정하세요.");
            if (context.UseUndo) Undo.RecordObject(node, "Bridge transform");
            ApplyTransform(node, op, context, op.path);
        }

        // 로컬 좌표 기준입니다. rotation은 오일러 각(도)입니다.
        private static void ApplyTransform(Transform node, BridgeOperation op, EditContext context, string recordPath)
        {
            if (!string.IsNullOrEmpty(op.position))
            {
                var value = ParseVector(op.position, 3);
                string before = Text(node.localPosition);
                node.localPosition = new Vector3(value[0], value[1], value[2]);
                if (context != null) Record(context, recordPath, "Transform", "localPosition", before, Text(node.localPosition));
            }
            if (!string.IsNullOrEmpty(op.rotation))
            {
                var value = ParseVector(op.rotation, 3);
                string before = Text(node.localEulerAngles);
                node.localEulerAngles = new Vector3(value[0], value[1], value[2]);
                if (context != null) Record(context, recordPath, "Transform", "localEulerAngles", before, Text(node.localEulerAngles));
            }
            if (!string.IsNullOrEmpty(op.scale))
            {
                var value = ParseVector(op.scale, 3);
                string before = Text(node.localScale);
                node.localScale = new Vector3(value[0], value[1], value[2]);
                if (context != null) Record(context, recordPath, "Transform", "localScale", before, Text(node.localScale));
            }
        }

        // ───────────────────────── 컴포넌트 작업 ─────────────────────────

        private static Dictionary<string, Type[]> componentTypes;

        private static Type ComponentType(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("component가 필요합니다.");
            if (componentTypes == null)
            {
                var all = TypeCache.GetTypesDerivedFrom<Component>().Where(x => !x.IsAbstract && !x.IsGenericTypeDefinition).ToArray();
                componentTypes = all.Where(x => x.FullName != null).GroupBy(x => x.FullName).ToDictionary(x => x.Key, x => x.ToArray());
                foreach (var group in all.GroupBy(x => x.Name))
                    if (!componentTypes.ContainsKey(group.Key)) componentTypes[group.Key] = group.ToArray();
                if (!componentTypes.ContainsKey("Transform")) componentTypes["Transform"] = new[] { typeof(Transform) };
            }
            if (!componentTypes.TryGetValue(name, out var types)) throw new ArgumentException("컴포넌트 형식을 찾을 수 없습니다: " + name);
            if (types.Length > 1)
                throw new ArgumentException("이름이 같은 형식이 여러 개입니다. 전체 이름을 쓰세요: " + string.Join(", ", types.Take(5).Select(x => x.FullName)));
            return types[0];
        }

        private static Component GetComponent(Transform node, BridgeOperation op, bool required)
        {
            var type = ComponentType(op.component);
            var components = node.GetComponents(type);
            if (op.index < 0) throw new ArgumentException("index는 0 이상입니다.");
            if (op.index < components.Length) return components[op.index];
            if (required) throw new InvalidOperationException(op.component + "[" + op.index + "] 컴포넌트가 없습니다.");
            return null;
        }

        private static void AddComponent(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            var type = ComponentType(op.component);
            if (typeof(Transform).IsAssignableFrom(type)) throw new ArgumentException("Transform은 추가할 수 없습니다.");
            if (node.GetComponent(type) != null)
            {
                if (op.optional) { Record(context, op.path, type.Name, "exists", "true", "true"); return; }
                throw new InvalidOperationException(type.Name + "이 이미 있습니다. 중복 추가를 막으려면 optional=true로 재실행 가능한 요청을 만드세요.");
            }
            var added = context.UseUndo ? Undo.AddComponent(node.gameObject, type) : node.gameObject.AddComponent(type);
            if (added == null) throw new InvalidOperationException(type.Name + " 추가 실패. 필수 조건·중복 금지 규칙을 확인하세요.");
            Record(context, op.path, type.Name, "exists", "false", "true");
        }

        private static void RemoveComponent(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            var component = GetComponent(node, op, !op.optional);
            if (component == null) { Record(context, op.path, op.component, "exists", "false", "false"); return; }
            if (component is Transform) throw new ArgumentException("Transform은 제거할 수 없습니다.");
            string typeName = component.GetType().Name;
            if (context.UseUndo) Undo.DestroyObjectImmediate(component);
            else Object.DestroyImmediate(component);
            if (component != null) throw new InvalidOperationException(typeName + "에 의존하는 컴포넌트가 있어 제거되지 않았습니다.");
            Record(context, op.path, typeName, "exists", "true", "false");
        }

        private static void SetProperty(EditContext context, BridgeOperation op)
        {
            var node = Find(context, op.path);
            if (string.IsNullOrEmpty(op.property)) throw new ArgumentException("property가 필요합니다(직렬화 이름, 예: m_Mass, maxSpeedKph).");
            Object owner = op.component == "GameObject" ? (Object)node.gameObject : GetComponent(node, op, true);
            string componentName = owner is GameObject ? "GameObject" : owner.GetType().Name;
            using (var serialized = new SerializedObject(owner))
            {
                var property = serialized.FindProperty(op.property) ?? throw new ArgumentException("직렬화 필드를 찾을 수 없습니다: " + componentName + "." + op.property);
                string before = ReadValue(context, property);
                if (property.propertyType == SerializedPropertyType.ObjectReference) AssignReference(context, serialized, op.property, op.value);
                else
                {
                    Assign(property, op.value);
                    Apply(context, serialized);
                }
                serialized.Update();
                string after = ReadValue(context, serialized.FindProperty(op.property));
                Record(context, op.path, componentName, op.property, before, after);
            }
        }

        private static void Apply(EditContext context, SerializedObject serialized)
        {
            if (context.UseUndo) serialized.ApplyModifiedProperties();
            else serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────────────────── 값 변환 ─────────────────────────

        private static void Assign(SerializedProperty property, string value)
        {
            if (value == null) throw new ArgumentException("value가 필요합니다.");
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: property.floatValue = ParseFloat(value); break;
                case SerializedPropertyType.Integer:
                    if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) throw new ArgumentException("정수가 필요합니다: " + value);
                    property.longValue = integer;
                    break;
                case SerializedPropertyType.Boolean: property.boolValue = ParseBool(value); break;
                case SerializedPropertyType.String: property.stringValue = value; break;
                case SerializedPropertyType.Enum:
                    int index = Array.IndexOf(property.enumNames, value);
                    if (index >= 0) property.enumValueIndex = index;
                    else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int enumValue)) property.intValue = enumValue;
                    else throw new ArgumentException("허용 값: " + string.Join(", ", property.enumNames));
                    break;
                case SerializedPropertyType.LayerMask:
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int mask)) property.intValue = mask;
                    else
                    {
                        int bits = 0;
                        foreach (string name in value.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0))
                        {
                            int layer = LayerMask.NameToLayer(name);
                            if (layer < 0) throw new ArgumentException("존재하지 않는 레이어입니다: " + name);
                            bits |= 1 << layer;
                        }
                        property.intValue = bits;
                    }
                    break;
                case SerializedPropertyType.Vector2: { var v = ParseVector(value, 2); property.vector2Value = new Vector2(v[0], v[1]); break; }
                case SerializedPropertyType.Vector3: { var v = ParseVector(value, 3); property.vector3Value = new Vector3(v[0], v[1], v[2]); break; }
                case SerializedPropertyType.Vector4: { var v = ParseVector(value, 4); property.vector4Value = new Vector4(v[0], v[1], v[2], v[3]); break; }
                case SerializedPropertyType.Vector2Int: { var v = ParseVector(value, 2); property.vector2IntValue = new Vector2Int(ToInt(v[0]), ToInt(v[1])); break; }
                case SerializedPropertyType.Vector3Int: { var v = ParseVector(value, 3); property.vector3IntValue = new Vector3Int(ToInt(v[0]), ToInt(v[1]), ToInt(v[2])); break; }
                case SerializedPropertyType.Quaternion: { var v = ParseVector(value, 3); property.quaternionValue = Quaternion.Euler(v[0], v[1], v[2]); break; }
                case SerializedPropertyType.Color:
                    if (value.StartsWith("#", StringComparison.Ordinal))
                    {
                        if (!ColorUtility.TryParseHtmlString(value, out var html)) throw new ArgumentException("색상 형식이 잘못되었습니다: " + value);
                        property.colorValue = html;
                    }
                    else { var v = ParseVector(value, 4); property.colorValue = new Color(v[0], v[1], v[2], v[3]); }
                    break;
                default: throw new ArgumentException("지원하지 않는 필드 형식입니다: " + property.propertyType + ". 배열·구조체는 하위 필드 경로(예: wheels.Array.data[0])를 지정하세요.");
            }
        }

        // 참조 형식은 필드 타입을 알 수 없으므로 후보를 차례로 대입하고 Unity가 받아들인 값을 사용합니다.
        private static void AssignReference(EditContext context, SerializedObject serialized, string propertyPath, string value)
        {
            var property = serialized.FindProperty(propertyPath);
            if (string.IsNullOrEmpty(value) || value == "null")
            {
                property.objectReferenceValue = null;
                Apply(context, serialized);
                return;
            }
            var resolved = ResolveReference(context, value);
            var candidates = new List<Object> { resolved };
            if (resolved is GameObject gameObject) candidates.AddRange(gameObject.GetComponents<Component>().Where(x => x != null));
            else if (resolved is Component component) candidates.Add(component.gameObject);
            foreach (var candidate in candidates)
            {
                serialized.Update();
                property = serialized.FindProperty(propertyPath);
                property.objectReferenceValue = candidate;
                Apply(context, serialized);
                serialized.Update();
                if (serialized.FindProperty(propertyPath).objectReferenceValue == candidate) return;
            }
            throw new InvalidOperationException("필드 형식과 맞지 않거나 다른 씬의 객체라서 참조를 지정할 수 없습니다: " + value);
        }

        // 참조 표기: asset:Assets/경로[#하위에셋이름], scene:객체경로[|컴포넌트], GlobalObjectId_V1-..., null
        private static Object ResolveReference(EditContext context, string value)
        {
            if (value.StartsWith("asset:", StringComparison.Ordinal))
            {
                string text = value.Substring(6);
                string sub = null;
                int hash = text.IndexOf('#');
                if (hash >= 0) { sub = text.Substring(hash + 1); text = text.Substring(0, hash); }
                if (!text.StartsWith("Assets/", StringComparison.Ordinal) && !text.StartsWith("Packages/", StringComparison.Ordinal))
                    throw new ArgumentException("asset: 뒤에는 Assets/ 또는 Packages/ 경로가 필요합니다.");
                var asset = sub == null ? AssetDatabase.LoadMainAssetAtPath(text)
                    : AssetDatabase.LoadAllAssetsAtPath(text).FirstOrDefault(x => x != null && x.name == sub);
                return asset ?? throw new FileNotFoundException("에셋을 찾을 수 없습니다: " + value);
            }
            if (value.StartsWith("scene:", StringComparison.Ordinal))
            {
                string text = value.Substring(6);
                string componentName = null;
                int bar = text.IndexOf('|');
                if (bar >= 0) { componentName = text.Substring(bar + 1); text = text.Substring(0, bar); }
                var node = Find(context, text);
                if (componentName == null) return node.gameObject;
                return node.GetComponent(ComponentType(componentName)) ?? throw new InvalidOperationException(text + "에 " + componentName + "이 없습니다.");
            }
            if (GlobalObjectId.TryParse(value, out var id))
                return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) ?? throw new InvalidOperationException("objectId 대상을 찾을 수 없습니다.");
            throw new ArgumentException("참조 값은 asset:경로, scene:경로[|컴포넌트], GlobalObjectId, null 중 하나입니다.");
        }

        private static string ReadValue(EditContext context, SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Enum:
                    var names = property.enumNames;
                    return property.enumValueIndex >= 0 && property.enumValueIndex < names.Length ? names[property.enumValueIndex] : property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Vector2: return Text(property.vector2Value.x, property.vector2Value.y);
                case SerializedPropertyType.Vector3: return Text(property.vector3Value);
                case SerializedPropertyType.Vector4: { var v = property.vector4Value; return Text(v.x, v.y, v.z, v.w); }
                case SerializedPropertyType.Vector2Int: return Text(property.vector2IntValue.x, property.vector2IntValue.y);
                case SerializedPropertyType.Vector3Int: { var v = property.vector3IntValue; return Text(v.x, v.y, v.z); }
                case SerializedPropertyType.Quaternion: return Text(property.quaternionValue.eulerAngles);
                case SerializedPropertyType.Color: { var c = property.colorValue; return Text(c.r, c.g, c.b, c.a); }
                case SerializedPropertyType.Integer: return property.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.ObjectReference:
                    var target = property.objectReferenceValue;
                    if (target == null) return "null";
                    if (EditorUtility.IsPersistent(target))
                    {
                        string path = AssetDatabase.GetAssetPath(target);
                        return "asset:" + path + (AssetDatabase.IsMainAsset(target) ? "" : "#" + target.name);
                    }
                    var owner = target as GameObject ?? (target as Component)?.gameObject;
                    if (owner != null && owner.scene == context.Scene)
                        return "scene:" + PathOf(context, owner.transform) + (target is Component ? "|" + target.GetType().Name : "");
                    return GlobalObjectId.GetGlobalObjectIdSlow(target).ToString();
                default: return BridgeCommands.Read(property);
            }
        }

        private static float ParseFloat(string value)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || float.IsInfinity(number))
                throw new ArgumentException("유한한 숫자가 필요합니다: " + value);
            return number;
        }

        private static bool ParseBool(string value)
        {
            if (value == "true") return true;
            if (value == "false") return false;
            throw new ArgumentException("true 또는 false가 필요합니다: " + value);
        }

        private static float[] ParseVector(string value, int count)
        {
            var parts = (value ?? "").Split(',');
            if (parts.Length != count) throw new ArgumentException(count + "개 숫자를 쉼표로 구분하세요(예: " + string.Join(",", Enumerable.Repeat("0", count)) + "): " + value);
            return parts.Select(x => ParseFloat(x.Trim())).ToArray();
        }

        private static int ToInt(float value)
        {
            if (Mathf.Abs(value - Mathf.Round(value)) > 0.0001f) throw new ArgumentException("정수가 필요합니다: " + value);
            return Mathf.RoundToInt(value);
        }

        private static string Text(Vector3 value) => Text(value.x, value.y, value.z);
        private static string Text(params float[] values) => string.Join(",", values.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));
        private static string Text(params int[] values) => string.Join(",", values.Select(x => x.ToString(CultureInfo.InvariantCulture)));
    }
}
