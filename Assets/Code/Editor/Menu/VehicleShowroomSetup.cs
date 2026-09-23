using System;
using System.IO;
using System.Linq;
using SteelDistrict.Scenes;
using SteelDistrict.UI;
using SteelDistrict.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;
using Object=UnityEngine.Object;

namespace SteelDistrict.Editor.Bridge
{
    public static class VehicleShowroomSetup
    {
        public const string EnvironmentPath="Assets/Prefabs/Environment/MenuParking.prefab";
        // 기존 인스턴스와 사용자 오버라이드를 유지하며 최초 한 번만 배치합니다.
        private static int EnsureSceneEnvironment(VehiclePreview view,Scene scene)
        {
            int changed=0;
            if(view.environmentRoot==null)
            {
                var matches=scene.GetRootGameObjects().Where(x=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x)==EnvironmentPath).ToArray();
                if(matches.Length>1) throw new InvalidOperationException("MenuParking 인스턴스가 여러 개입니다. 사용할 객체를 먼저 연결하세요.");
                if(matches.Length==1) view.environmentRoot=matches[0];
                else
                {
                    if(scene.GetRootGameObjects().Any(x=>x.name=="MenuParking" || x.name=="MenuParking(Clone)"))
                        throw new InvalidOperationException("기존 MenuParking 객체를 environmentRoot에 연결하세요. 중복 생성하지 않습니다.");
                    if(view.environmentPrefab==null) throw new InvalidOperationException("주차장 원본 프리팹 연결 누락");
                    view.environmentRoot=(GameObject)PrefabUtility.InstantiatePrefab(view.environmentPrefab,scene);
                    Undo.RegisterCreatedObjectUndo(view.environmentRoot,"편집 가능한 MenuParking 배치");
                    view.environmentRoot.name="MenuParking";
                    view.environmentRoot.layer=31;
                    view.environmentRoot.SetActive(true);
                }
                changed++;
            }
            if(EditorUtility.IsPersistent(view.environmentRoot) || view.environmentRoot.scene!=scene)
                throw new InvalidOperationException("environmentRoot는 같은 씬의 객체여야 합니다.");
            // 배경을 이동/회전하면 차량 슬롯도 함께 이동하되, 기존 월드 배치를 보존합니다.
            foreach(var slot in view.parkingSlots ?? Array.Empty<Transform>())
            {
                if(slot==null || slot.IsChildOf(view.environmentRoot.transform)) continue;
                if(slot==view.environmentRoot.transform || view.environmentRoot.transform.IsChildOf(slot)) throw new InvalidOperationException("슬롯 부모 관계가 잘못되었습니다.");
                Undo.SetTransformParent(slot,view.environmentRoot.transform,"주차장과 슬롯 함께 편집"); changed++;
            }
            return changed;
        }
        public static void Materialize(BridgeRequest request,BridgeResponse result)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 실행하세요.");
            var scene=SceneManager.GetSceneByPath(VehicleSession.MenuScene);
            if(!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("MainMenuScene을 열어 두세요.");
            var view=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<VehiclePreview>(true)).Single();
            result.values.Add(new BridgeValue{target="Vehicle Selection",property="environmentRoot",before=view.environmentRoot!=null?view.environmentRoot.name:"없음",after="씬의 MenuParking 참조",changed=view.environmentRoot==null});
            foreach(var slot in view.parkingSlots ?? Array.Empty<Transform>())
                if(slot!=null) result.values.Add(new BridgeValue{target=slot.name,property="parent",before=slot.parent!=null?slot.parent.name:"씬 루트",after="MenuParking (월드 위치 유지)",changed=view.environmentRoot==null || !slot.IsChildOf(view.environmentRoot.transform)});
            if(!request.apply) return;
            string backup="Logs/MenuParkingScene-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true)) throw new IOException("미저장 상태 백업 실패");
            result.reportPath=backup;
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("MenuParking 씬 배치");
            try
            {
                Undo.RecordObject(view,"씬 주차장 연결");
                result.changedCount=EnsureSceneEnvironment(view,scene);
                if(result.changedCount>0)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene)) throw new IOException("메뉴 씬 저장 실패");
                }
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
        public static void Inspect(BridgeResponse result)
        {
            var scene=EditorSceneManager.OpenPreviewScene(VehicleSession.MenuScene);
            try
            {
                var view=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<VehiclePreview>(true)).Single();
                var menu=view.GetComponent<MainMenuController>();
                if(view.previewCamera==null || view.environmentRoot==null || menu==null || menu.catalog==null)
                    throw new InvalidOperationException("메뉴/카메라/환경/카탈로그 연결 누락");
                if(view.parkingSlots==null || view.parkingSlots.Length<2 || view.parkingSlots.Any(x=>x==null) ||
                   Vector3.Distance(view.parkingSlots[0].position,view.parkingSlots[1].position)<5)
                    throw new InvalidOperationException("주차 슬롯 연결 또는 간격(최소 5 m) 오류");
                var environment=view.environmentRoot;
                if(EditorUtility.IsPersistent(environment) || environment.scene!=scene) throw new InvalidOperationException("씬 주차장 연결 오류");
                if(view.parkingSlots.Any(x=>!x.IsChildOf(environment.transform))) throw new InvalidOperationException("슬롯이 주차장 하위에 없습니다.");
                if(environment.GetComponentsInChildren<Component>(true).Any(x=>x==null) ||
                   environment.GetComponentsInChildren<MonoBehaviour>(true).Length!=0 ||
                   environment.GetComponentsInChildren<Rigidbody>(true).Length!=0 ||
                   environment.GetComponentsInChildren<Collider>(true).Length!=0 ||
                   environment.GetComponentsInChildren<Camera>(true).Length!=0)
                    throw new InvalidOperationException("전시 환경에 Missing Script 또는 실행/물리 컴포넌트가 있습니다.");
                var entries=menu.catalog.vehicles;
                if(entries.Any(x=>x==null || !x.IsValid) || entries.Select(x=>x.id).Distinct().Count()!=entries.Length)
                    throw new InvalidOperationException("차량 정의 누락 또는 ID 중복");
                foreach(var entry in entries)
                {
                    if(entry.previewPrefab.GetComponentsInChildren<Component>(true).Any(x=>x==null) ||
                       entry.previewPrefab.GetComponentsInChildren<MonoBehaviour>(true).Length!=0 ||
                       entry.previewPrefab.GetComponentsInChildren<Rigidbody>(true).Length!=0 ||
                       entry.previewPrefab.GetComponentsInChildren<Collider>(true).Length!=0)
                        throw new InvalidOperationException("표시 차량에 실행/물리 컴포넌트가 있습니다: "+entry.id);
                    result.values.Add(new BridgeValue{target=entry.id,property="initiallyOwned",before=entry.initiallyOwned.ToString(),after=entry.initiallyOwned ? "차고" : "상점"});
                }
                result.values.Add(new BridgeValue{target=EnvironmentPath,property="renderers / slots",after=environment.GetComponentsInChildren<Renderer>(true).Length+" / "+view.parkingSlots.Length});
                result.checkedCount=result.passedCount=entries.Length+2;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        public static void Run(BridgeRequest request,BridgeResponse result)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage()!=null)
                throw new InvalidOperationException("Edit Mode에서 Prefab Stage를 닫고 실행하세요.");
            foreach(string path in new[]{VehicleSession.MenuScene,VehicleSession.WorldScene})
            {
                var loaded=SceneManager.GetSceneByPath(path);
                if(loaded.IsValid() && loaded.isDirty) throw new InvalidOperationException("미저장 씬: "+path);
            }
            var catalog=AssetDatabase.LoadAssetAtPath<VehicleCatalog>(MainMenuSetup.CatalogPath);
            if(catalog==null || EditorUtility.IsDirty(catalog)) throw new InvalidOperationException("카탈로그 누락 또는 미저장 변경");
            if(catalog.Find("simple-retro")==null || catalog.Find("prometheus")==null) throw new InvalidOperationException("초기 차량 ID 누락");
            result.values.Add(new BridgeValue{target=EnvironmentPath,before="GameScene 표시 환경",after="물리/주행 없는 주차장 프리팹",changed=!File.Exists(EnvironmentPath)});
            result.values.Add(new BridgeValue{target=VehicleSession.MenuScene,before="단일 미리보기 위치",after="주차장 배경 + 2개 재사용 슬롯",changed=true});
            result.values.Add(new BridgeValue{target=MainMenuSetup.CatalogPath,before="초기 보유 설정",after="Simple Retro 보유 / Prometheus 미보유",changed=true});
            if(!request.apply) return;
            string backup="Logs/ShowroomSetup-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            File.Copy(VehicleSession.MenuScene,backup+"/MainMenuScene.unity");
            File.Copy(MainMenuSetup.CatalogPath,backup+"/VehicleCatalog.asset");
            result.reportPath=backup;
            var active=SceneManager.GetActiveScene();
            var source=SceneManager.GetSceneByPath(VehicleSession.WorldScene);
            bool sourcePreview=!source.IsValid() || !source.isLoaded;
            if(sourcePreview) source=EditorSceneManager.OpenPreviewScene(VehicleSession.WorldScene);
            var menu=SceneManager.GetSceneByPath(VehicleSession.MenuScene);
            bool closeMenu=!menu.IsValid() || !menu.isLoaded;
            int group=-1;
            bool createdAsset=false;
            try
            {
                var roots=source.GetRootGameObjects();
                var retro=roots.Single(x=>x.name=="Simple Retro Car");
                var prometheus=roots.Single(x=>x.name=="Prometheus");
                var environment=AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPath);
                if(environment==null)
                {
                    if(!AssetDatabase.IsValidFolder("Assets/Prefabs/Environment")) AssetDatabase.CreateFolder("Assets/Prefabs","Environment");
                    var temporary=EditorSceneManager.NewPreviewScene();
                    try
                    {
                        var container=new GameObject("Menu Parking"); SceneManager.MoveGameObjectToScene(container,temporary);
                        foreach(var root in roots)
                        {
                            if(root.GetComponentInChildren<ArcadeVehicleDrive>(true)!=null || root.GetComponentInChildren<WorldVehicleController>(true)!=null ||
                               root.GetComponentInChildren<Camera>(true)!=null || root.GetComponentsInChildren<Renderer>(true).Length==0) continue;
                            var clone=Object.Instantiate(root,container.transform,true);
                            clone.name=root.name;
                            foreach(var c in clone.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(c);
                            foreach(var c in clone.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(c);
                            foreach(var c in clone.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                            foreach(var c in clone.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);
                            foreach(var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=31;
                        }
                        if(container.GetComponentsInChildren<Renderer>(true).Length==0) throw new InvalidOperationException("복제할 환경 메시가 없습니다.");
                        environment=PrefabUtility.SaveAsPrefabAsset(container,EnvironmentPath,out bool saved);
                        if(!saved) throw new IOException("주차장 프리팹 저장 실패");
                        createdAsset=true; result.changedCount++;
                    }
                    finally { EditorSceneManager.ClosePreviewScene(temporary); }
                }
                if(closeMenu) menu=EditorSceneManager.OpenScene(VehicleSession.MenuScene,OpenSceneMode.Additive);
                var view=menu.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<VehiclePreview>(true)).Single();
                Undo.IncrementCurrentGroup(); group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("차량 전시 주차장 연결");
                Undo.RecordObject(view,"주차장 프리팹 연결");
                if(view.environmentPrefab!=environment) { view.environmentPrefab=environment; result.changedCount++; }
                if(view.parkingSlots==null || view.parkingSlots.Length==0)
                {
                    view.parkingSlots=new Transform[2];
                    Vector3[] positions={retro.transform.position,prometheus.transform.position};
                    // 기존 씬의 차량 배치 위치를 재사용하되, 슬롯 Y는 타이어 밑면이 닿는 바닥 높이입니다.
                    for(int i=0;i<2;i++)
                    {
                        var slot=new GameObject("Showroom Parking Slot "+(i+1));
                        SceneManager.MoveGameObjectToScene(slot,menu); Undo.RegisterCreatedObjectUndo(slot,"주차 위치 생성");
                        slot.transform.position=new Vector3(positions[i].x,.025f,positions[i].z);
                        slot.transform.rotation=retro.transform.rotation;
                        view.parkingSlots[i]=slot.transform; result.changedCount++;
                    }
                }
                result.changedCount+=EnsureSceneEnvironment(view,menu);
                Undo.RecordObject(view.previewCamera,"전시 카메라 원거리"); view.previewCamera.farClipPlane=350;
                var hd=view.previewCamera.GetComponent<HDAdditionalCameraData>();
                if(hd!=null) { Undo.RecordObject(hd,"전시 배경색"); hd.backgroundColorHDR=new Color(.09f,.16f,.25f); }
                var key=menu.GetRootGameObjects().FirstOrDefault(x=>x.name=="Preview Key")?.GetComponent<Light>();
                if(key!=null) { Undo.RecordObject(key,"차량 전시 그림자"); key.shadows=LightShadows.Soft; }
                Undo.RecordObject(catalog,"초기 보유 차량 설정");
                catalog.Find("simple-retro").initiallyOwned=true;
                catalog.Find("prometheus").initiallyOwned=false;
                EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(view);
                PrefabUtility.RecordPrefabInstancePropertyModifications(view);
                EditorSceneManager.MarkSceneDirty(menu);
                if(!EditorSceneManager.SaveScene(menu)) throw new IOException("메인 씬 저장 실패");
                AssetDatabase.SaveAssetIfDirty(catalog);
                Undo.CollapseUndoOperations(group); result.changedCount++;
                result.checkedCount=4;
            }
            catch
            {
                if(group>=0) Undo.RevertAllDownToGroup(group);
                File.Copy(backup+"/MainMenuScene.unity",VehicleSession.MenuScene,true);
                File.Copy(backup+"/VehicleCatalog.asset",MainMenuSetup.CatalogPath,true);
                if(createdAsset) AssetDatabase.DeleteAsset(EnvironmentPath);
                AssetDatabase.ImportAsset(MainMenuSetup.CatalogPath,ImportAssetOptions.ForceUpdate);
                result.changedCount=0;
                throw;
            }
            finally
            {
                if(closeMenu && menu.IsValid() && menu.isLoaded) EditorSceneManager.CloseScene(menu,true);
                if(sourcePreview) EditorSceneManager.ClosePreviewScene(source);
                if(active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }
    }
}
