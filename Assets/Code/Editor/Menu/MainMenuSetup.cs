using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SteelDistrict.Vehicles;
using SteelDistrict.UI;
using SteelDistrict.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace SteelDistrict.Editor.Bridge
{
    public static class MainMenuSetup
    {
        public const string Root = "Assets/GameContent/VehicleSelection";
        public const string CatalogPath = Root + "/VehicleCatalog.asset";
        public const string PrometheusPath = Root + "/Prometheus_Drive.prefab";
        public const string RetroPath = Root + "/SimpleRetro_Drive.prefab";
        private static readonly List<string> created = new List<string>();
        public static void SetInitialPreviewLighting()
        {
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"/PreviewLighting.asset");
            if(profile==null) throw new InvalidOperationException("미리보기 조명 프로필 없음");
            if(profile.TryGet<Exposure>(out var exposure)) { exposure.fixedExposure.Override(11.5f); EditorUtility.SetDirty(exposure); }
            if(!profile.TryGet<Bloom>(out var bloom)) { bloom=profile.Add<Bloom>(); AssetDatabase.AddObjectToAsset(bloom,profile); }
            bloom.intensity.Override(0); EditorUtility.SetDirty(bloom); EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        public static void RebuildPreviews(BridgeRequest request, BridgeResponse result)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("Edit Mode에서 Prefab Stage를 닫고 실행하세요.");
            var catalog=AssetDatabase.LoadAssetAtPath<VehicleCatalog>(CatalogPath);
            if(catalog==null) throw new InvalidOperationException("카탈로그 없음");
            foreach(var entry in catalog.vehicles)
            {
                string path=AssetDatabase.GetAssetPath(entry.previewPrefab);
                if(!path.StartsWith(Root+"/",StringComparison.Ordinal)) throw new InvalidOperationException("범위 밖 미리보기: "+path);
                result.values.Add(new BridgeValue {target=path,before="표시용 구성",after="주행/입력/물리 없는 표시용 구성",changed=true});
            }
            if(!request.apply) return;
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                foreach(var entry in catalog.vehicles)
                { Export(entry.drivePrefab,AssetDatabase.GetAssetPath(entry.previewPrefab),scene,true); result.changedCount++; }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static void Create(BridgeRequest request, BridgeResponse result)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode에서 실행하세요.");
            foreach (string path in new[] { VehicleSession.MenuScene, VehicleSession.WorldScene })
            {
                var loaded = SceneManager.GetSceneByPath(path);
                if (loaded.IsValid() && loaded.isDirty) throw new InvalidOperationException("미저장 씬을 먼저 저장하거나 변경을 정리하세요: " + path);
                if (!File.Exists(path)) throw new InvalidOperationException("기존 씬이 없습니다: " + path);
            }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Prefab Stage를 닫은 뒤 실행하세요.");
            if (AssetDatabase.LoadAssetAtPath<VehicleCatalog>(CatalogPath) != null)
            { result.Warning(CatalogPath,"ALREADY_EXISTS","이미 구성되어 있습니다. 재실행으로 튜닝이나 씬을 덮어쓰지 않습니다."); return; }
            if (Directory.Exists(Root)) throw new InvalidOperationException("생성 경로에 기존 파일이 있습니다: " + Root);
            result.values.Add(new BridgeValue { target=VehicleSession.WorldScene, before="Simple Retro Car + 미설정 Prometheus", after="두 차량 공통 주행 + 선택 차량 제어",changed=true });
            result.values.Add(new BridgeValue { target=VehicleSession.MenuScene, before="기존 메인 씬", after="차량 목록·3D 미리보기·성능·월드 진입",changed=true });
            result.values.Add(new BridgeValue { target="EditorBuildSettings", before=string.Join(",",EditorBuildSettings.scenes.Where(x=>x.enabled).Select(x=>x.path)), after="MainMenuScene → GameScene (기존 항목 보존)",changed=true });
            if (!request.apply) return;
            var active = SceneManager.GetActiveScene();
            bool closeGame=!SceneManager.GetSceneByPath(VehicleSession.WorldScene).isLoaded;
            bool closeMenu=!SceneManager.GetSceneByPath(VehicleSession.MenuScene).isLoaded;
            var game = closeGame ? EditorSceneManager.OpenScene(VehicleSession.WorldScene,OpenSceneMode.Additive) : SceneManager.GetSceneByPath(VehicleSession.WorldScene);
            var menu = closeMenu ? EditorSceneManager.OpenScene(VehicleSession.MenuScene,OpenSceneMode.Additive) : SceneManager.GetSceneByPath(VehicleSession.MenuScene);
            var buildBefore = EditorBuildSettings.scenes;
            var backups = new Dictionary<string,byte[]>();
            foreach (string path in new[] { VehicleSession.WorldScene,VehicleSession.MenuScene,"ProjectSettings/EditorBuildSettings.asset" }) backups[path]=File.ReadAllBytes(path);
            string backupDir="Logs/MenuSetupBackup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backupDir);
            foreach(var pair in backups) File.WriteAllBytes(backupDir+"/"+Path.GetFileName(pair.Key),pair.Value);
            result.reportPath=backupDir;
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("차량 선택 화면 구성");
            created.Clear();
            try
            {
                var retro=game.GetRootGameObjects().Single(x=>x.name=="Simple Retro Car");
                var prom=game.GetRootGameObjects().Single(x=>x.name=="Prometheus");
                BridgeCommands.InspectVehicle(retro,"Simple Retro Car",result);
                if(result.issues.Any(x=>x.severity=="error")) throw new InvalidOperationException("기존 차량 검사 실패");
                if(game.GetRootGameObjects().Any(x=>x.GetComponent<WorldVehicleController>()!=null) || menu.GetRootGameObjects().Any(x=>x.GetComponent<MainMenuController>()!=null))
                    throw new InvalidOperationException("기존 메뉴/월드 컨트롤러가 있습니다.");
                Directory.CreateDirectory(Root); AssetDatabase.Refresh();
                Undo.RegisterFullObjectHierarchyUndo(prom,"Prometheus 주행 연결");
                ConfigurePrometheus(prom,retro);
                var temporary=EditorSceneManager.NewPreviewScene();
                GameObject retroPrefab, promPrefab, retroPreview, promPreview;
                try
                {
                    retroPrefab=Export(retro,RetroPath,temporary,false);
                    promPrefab=Export(prom,PrometheusPath,temporary,false);
                    retroPreview=Export(retro,Root+"/SimpleRetro_Preview.prefab",temporary,true);
                    promPreview=Export(prom,Root+"/Prometheus_Preview.prefab",temporary,true);
                }
                finally { EditorSceneManager.ClosePreviewScene(temporary); }
                var catalog=ScriptableObject.CreateInstance<VehicleCatalog>();
                catalog.vehicles=new[] {
                    new VehicleEntry { id="simple-retro",displayName="Simple Retro",drivePrefab=retroPrefab,previewPrefab=retroPreview },
                    new VehicleEntry { id="prometheus",displayName="Prometheus",drivePrefab=promPrefab,previewPrefab=promPreview }
                };
                AssetDatabase.CreateAsset(catalog,CatalogPath); created.Add(CatalogPath);
                var world=NewObject(game,"World Vehicle Selection").AddComponent<WorldVehicleController>();
                world.catalog=catalog; world.originalVehicle=retro; world.prometheusVehicle=prom;
                world.followCamera=game.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<VehicleFollowCamera>(true)).Single();
                SetupMenu(menu,catalog);
                var registered=new List<EditorBuildSettingsScene> {
                    new EditorBuildSettingsScene(VehicleSession.MenuScene,true),new EditorBuildSettingsScene(VehicleSession.WorldScene,true)
                };
                registered.AddRange(buildBefore.Where(x=>x.path!=VehicleSession.MenuScene && x.path!=VehicleSession.WorldScene));
                EditorBuildSettings.scenes=registered.ToArray();
                EditorSceneManager.MarkSceneDirty(game); EditorSceneManager.MarkSceneDirty(menu);
                if(!EditorSceneManager.SaveScene(game) || !EditorSceneManager.SaveScene(menu)) throw new IOException("씬 저장 실패");
                AssetDatabase.SaveAssets(); Undo.CollapseUndoOperations(group);
                result.changedCount=created.Count+3;
                BridgeCommands.InspectVehicle(prom,"GameScene/Prometheus",result);
                result.passedCount=result.issues.Count(x=>x.severity=="error")==0?2:0;
            }
            catch
            {
                Undo.RevertAllDownToGroup(group); EditorBuildSettings.scenes=buildBefore;
                foreach(var path in created.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
                foreach(var pair in backups) File.WriteAllBytes(pair.Key,pair.Value);
                throw;
            }
            finally
            {
                if(closeMenu) EditorSceneManager.CloseScene(menu,true);
                if(closeGame) EditorSceneManager.CloseScene(game,true);
                if(active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }
        private static void ConfigurePrometheus(GameObject prom,GameObject source)
        {
            foreach(var collider in prom.GetComponentsInChildren<WheelCollider>(true)) Undo.DestroyObjectImmediate(collider);
            var effects=prom.transform.Find("Effects"); if(effects!=null) Undo.DestroyObjectImmediate(effects.gameObject);
            var body=prom.GetComponent<Rigidbody>(); Undo.RecordObject(body,"Prometheus 물리");
            body.interpolation=RigidbodyInterpolation.Interpolate; body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic=false; body.useGravity=true;
            foreach(var collider in prom.GetComponentsInChildren<MeshCollider>(true)) { Undo.RecordObject(collider,"차체 충돌"); collider.convex=true; }
            var input=Undo.AddComponent<VehicleDriveInput>(prom);
            var suspension=Undo.AddComponent<VehicleSuspension>(prom);
            EditorUtility.CopySerialized(source.GetComponent<VehicleSuspension>(),suspension);
            using(var s=new SerializedObject(suspension))
            {
                var wheels=s.FindProperty("wheels"); wheels.arraySize=4;
                string[] names={"FrontLeftWheel","FrontRightWheel","RearLeftWheel","RearRightWheel"};
                for(int i=0;i<4;i++) wheels.GetArrayElementAtIndex(i).objectReferenceValue=prom.transform.Find("Wheels/Meshes/"+names[i]) ?? throw new InvalidOperationException("바퀴 없음: "+names[i]);
                s.FindProperty("wheelRadius").floatValue=.36f; s.ApplyModifiedProperties();
            }
            var drive=Undo.AddComponent<ArcadeVehicleDrive>(prom); EditorUtility.CopySerialized(source.GetComponent<ArcadeVehicleDrive>(),drive);
            using(var s=new SerializedObject(drive)) { s.FindProperty("input").objectReferenceValue=input; s.FindProperty("suspension").objectReferenceValue=suspension; s.ApplyModifiedProperties(); }
            var tire=Undo.AddComponent<VehicleTireEffects>(prom); EditorUtility.CopySerialized(source.GetComponent<VehicleTireEffects>(),tire);
            using(var s=new SerializedObject(tire)) { s.FindProperty("suspension").objectReferenceValue=suspension; s.ApplyModifiedProperties(); }
        }
        private static GameObject Export(GameObject source,string path,Scene scene,bool preview)
        {
            var clone=Object.Instantiate(source); SceneManager.MoveGameObjectToScene(clone,scene);
            clone.name=source.name; clone.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); clone.SetActive(true);
            if(preview)
            {
                foreach(var c in clone.GetComponentsInChildren<ArcadeVehicleDrive>(true)) Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<VehicleTireEffects>(true)) Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
                foreach(var c in clone.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(c.gameObject);
                foreach(var c in clone.GetComponentsInChildren<TrailRenderer>(true)) Object.DestroyImmediate(c.gameObject);
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(clone,path,out bool saved); Object.DestroyImmediate(clone);
            if(!saved) throw new IOException("프리팹 저장 실패: "+path); created.Add(path); return prefab;
        }
        private static GameObject NewObject(Scene scene,string name)
        {
            var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene); Undo.RegisterCreatedObjectUndo(go,"메뉴 구성"); return go;
        }
        private static void SetupMenu(Scene scene,VehicleCatalog catalog)
        {
            var host=NewObject(scene,"Vehicle Selection");
            var view=host.AddComponent<VehiclePreview>();
            view.pivot=NewObject(scene,"Preview Vehicle Pivot").transform;
            var camera=NewObject(scene,"Vehicle Preview Camera").AddComponent<Camera>();
            camera.cullingMask=1<<31; camera.fieldOfView=35; camera.nearClipPlane=.1f; camera.farClipPlane=100;
            var hd=camera.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode=HDAdditionalCameraData.ClearColorMode.Color; hd.backgroundColorHDR=new Color(.035f,.05f,.075f);
            hd.volumeLayerMask=1<<31;
            view.previewCamera=camera;
            var controller=host.AddComponent<MainMenuController>(); controller.catalog=catalog; controller.preview=view;
            var volume=NewObject(scene,"Preview Lighting").AddComponent<Volume>(); volume.gameObject.layer=31; volume.isGlobal=true;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile,Root+"/PreviewLighting.asset"); created.Add(Root+"/PreviewLighting.asset");
            var exposure=profile.Add<Exposure>(true); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(10);
            AssetDatabase.AddObjectToAsset(exposure,profile); EditorUtility.SetDirty(profile); volume.sharedProfile=profile;
            SetInitialPreviewLighting();
            Sun(scene,"Preview Key",new Vector3(35,-35,0),18000);
            Sun(scene,"Preview Fill",new Vector3(25,145,0),9000);
            // 사용자 씬의 기존 카메라·조명은 유지하고 미리보기는 전용 레이어로 분리합니다.
            foreach(var listener in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<AudioListener>(true)).Skip(1))
            { Undo.RecordObject(listener,"리스너 중복 방지"); listener.enabled=false; }
        }
        private static void Sun(Scene scene,string name,Vector3 euler,float intensity)
        {
            var light=NewObject(scene,name).AddComponent<Light>(); light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(euler); light.cullingMask=1<<31;
            light.gameObject.AddComponent<HDAdditionalLightData>(); light.intensity=intensity;
            light.shadows=LightShadows.None;
        }
    }
}
