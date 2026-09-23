using System;
using System.IO;
using System.Linq;
using SteelDistrict.UI;
using SteelDistrict.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SteelDistrict.Editor.Bridge
{
    public static class MainMenuAuthoring
    {
        private const string FontPath="Assets/UI/Fonts/NanumGothic-Regular.ttf";
        private const string BrokenFontPath="Assets/GameContent/VehicleSelection/MenuFont.fontsettings";
        public static void RepairReferences(BridgeRequest request,BridgeResponse result)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 뒤 실행하세요.");
            var scene=SceneManager.GetSceneByPath(VehicleSession.MenuScene);
            if(!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("MainMenuScene을 열어 두세요.");
            var controller=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MainMenuController>(true)).Single();
            var view=controller.view;
            if(view==null || view.display==null) throw new InvalidOperationException("UI/미리보기 영역 연결 누락");
            var font=AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if(font==null) throw new InvalidOperationException("한글 TTF 가져오기를 먼저 완료하세요.");
            var texts=view.GetComponentsInChildren<Text>(true).Where(x=>x.font==null || AssetDatabase.GetAssetPath(x.font)==BrokenFontPath).ToArray();
            var area=view.display.gameObject;
            int missing=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(area);
            if(missing>1) throw new InvalidOperationException("미리보기 영역에 예상보다 많은 Missing Script가 있습니다.");
            var pointer=area.GetComponent<PreviewPointer>();
            bool pointerBroken=pointer==null || !EditorUtility.IsPersistent(MonoScript.FromMonoBehaviour(pointer));
            result.values.Add(new BridgeValue{target="Main Menu UI/Text",property="font",before="데이터 없는 임시 글꼴 "+texts.Length+"개",after=FontPath,changed=texts.Length>0});
            result.values.Add(new BridgeValue{target="Vehicle Detail/Orbit Area",property="PreviewPointer",before="missing="+missing+", persistent="+!pointerBroken,after="PreviewPointer.cs GUID 참조",changed=pointerBroken || missing>0});
            if(!request.apply) return;
            string backup="Logs/MenuReferenceRepair-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true)) throw new IOException("미저장 작업 백업 실패");
            result.reportPath=backup;
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("메뉴 글꼴·스크립트 참조 복구");
            try
            {
                foreach(var text in texts) { Undo.RecordObject(text,"한글 글꼴 연결"); text.font=font; result.changedCount++; }
                if(missing>0) { Undo.RegisterCompleteObjectUndo(area,"끊어진 포인터 참조 제거"); GameObjectUtility.RemoveMonoBehavioursWithMissingScript(area); result.changedCount++; }
                if(pointerBroken)
                {
                    if(pointer!=null) Undo.DestroyObjectImmediate(pointer);
                    pointer=Undo.AddComponent<PreviewPointer>(area); result.changedCount++;
                }
                Undo.RecordObject(pointer,"미리보기 입력 연결"); pointer.preview=controller.preview;
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new IOException("씬 저장 실패");
                Undo.CollapseUndoOperations(group);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
        [MenuItem("Steel District/Menu/편집 가능한 UI 구성·그림자 수정")]
        private static void FromMenu()
        {
            var result=new BridgeResponse(); Apply(new BridgeRequest{apply=true},result);
            Debug.Log("메뉴 UI 구성 완료: "+result.changedCount+"개 변경");
        }
        public static void Apply(BridgeRequest request,BridgeResponse result)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play를 종료한 뒤 실행하세요.");
            var scene=SceneManager.GetSceneByPath(VehicleSession.MenuScene);
            if(scene.IsValid() && scene.isDirty) throw new InvalidOperationException("메인 씬의 미저장 변경을 먼저 저장하세요.");
            var active=SceneManager.GetActiveScene(); bool close=!scene.IsValid() || !scene.isLoaded;
            if(close) scene=EditorSceneManager.OpenScene(VehicleSession.MenuScene,OpenSceneMode.Additive);
            int group=-1;
            try
            {
                var roots=scene.GetRootGameObjects();
                var controller=roots.SelectMany(x=>x.GetComponentsInChildren<MainMenuController>(true)).Single();
                var key=roots.Single(x=>x.name=="Preview Key").GetComponent<Light>();
                var lights=roots.SelectMany(x=>x.GetComponentsInChildren<Light>(true)).Where(x=>x.type==LightType.Directional).ToArray();
                if(controller.view==null && roots.Any(x=>x.name=="Main Menu UI")) throw new InvalidOperationException("기존 Main Menu UI를 컨트롤러 View에 연결하세요. 덮어쓰지 않습니다.");
                result.values.Add(new BridgeValue{target="Main Menu UI",before=controller.view==null?"없음":"기존 씬 UI",after="씬에 저장된 편집 가능 UI",changed=controller.view==null});
                foreach(var light in lights)
                    result.values.Add(new BridgeValue{target=light.name,property="shadows",before=light.shadows.ToString(),after=(light==key?LightShadows.Soft:LightShadows.None).ToString(),changed=light.shadows!=(light==key?LightShadows.Soft:LightShadows.None)});
                if(!request.apply) return;
                string backup="Logs/MainMenuAuthoring-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
                File.Copy(VehicleSession.MenuScene,backup); result.reportPath=backup;
                SceneManager.SetActiveScene(scene);
                Undo.IncrementCurrentGroup(); group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("메뉴 UI 저장 및 그림자 광원 정리");
                if(controller.view==null)
                {
                    var font=AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                    if(font==null) throw new InvalidOperationException("한글 TTF 글꼴이 없습니다: "+FontPath);
                    var systemsBefore=scene.GetRootGameObjects().ToArray();
                    var view=new MainMenuViewBuilder().Build(controller);
                    foreach(var text in view.GetComponentsInChildren<Text>(true)) text.font=font;
                    Undo.RegisterCreatedObjectUndo(view.gameObject,"Main Menu UI 생성");
                    foreach(var root in scene.GetRootGameObjects().Except(systemsBefore).Where(x=>x!=view.gameObject)) Undo.RegisterCreatedObjectUndo(root,"UI EventSystem 생성");
                    Undo.RecordObject(controller,"메뉴 UI 연결"); controller.view=view;
                    EditorUtility.SetDirty(controller); result.changedCount++;
                }
                foreach(var light in lights)
                {
                    var wanted=light==key?LightShadows.Soft:LightShadows.None;
                    if(light.shadows==wanted) continue;
                    Undo.RecordObject(light,"단일 방향광 그림자"); light.shadows=wanted;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light); result.changedCount++;
                }
                // 기본 씬 조명은 전시 레이어를 비추지 않게 하여 Key/Fill과 중복 조명을 피합니다.
                var legacy=lights.FirstOrDefault(x=>x.name=="Directional Light");
                if(legacy!=null && (legacy.cullingMask & (1<<31))!=0)
                {
                    int before=legacy.cullingMask; Undo.RecordObject(legacy,"전시 레이어 조명 분리"); legacy.cullingMask &= ~(1<<31);
                    result.values.Add(new BridgeValue{target=legacy.name,property="cullingMask",before=before.ToString(),after=legacy.cullingMask.ToString(),changed=true}); result.changedCount++;
                }
                if(!controller.view.IsValid) throw new InvalidOperationException("생성된 UI 참조 검사 실패");
                if(result.changedCount>0) { EditorSceneManager.MarkSceneDirty(scene); if(!EditorSceneManager.SaveScene(scene)) throw new IOException("메인 씬 저장 실패"); }
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Font>(FontPath)); Undo.CollapseUndoOperations(group);
            }
            catch { if(group>=0) Undo.RevertAllDownToGroup(group); throw; }
            finally
            {
                if(close) EditorSceneManager.CloseScene(scene,true);
                if(active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }
        public static void Inspect(BridgeResponse result)
        {
            var scene=EditorSceneManager.OpenPreviewScene(VehicleSession.MenuScene);
            try
            {
                var roots=scene.GetRootGameObjects();
                var controller=roots.SelectMany(x=>x.GetComponentsInChildren<MainMenuController>(true)).Single();
                var view=controller.view;
                if(view==null || !view.IsValid) throw new InvalidOperationException("저장된 UI 참조 누락");
                if(roots.SelectMany(x=>x.GetComponentsInChildren<MainMenuView>(true)).Count()!=1) throw new InvalidOperationException("UI 중복");
                if(view.GetComponentsInChildren<Component>(true).Any(x=>x==null)) throw new InvalidOperationException("UI Missing Script");
                if(view.GetComponentsInChildren<Text>(true).Any(x=>x.font==null || !EditorUtility.IsPersistent(x.font))) throw new InvalidOperationException("저장되지 않은 글꼴 참조");
                var pointer=view.display.GetComponent<PreviewPointer>();
                if(pointer==null || pointer.preview!=controller.preview || !EditorUtility.IsPersistent(MonoScript.FromMonoBehaviour(pointer))) throw new InvalidOperationException("포인터 스크립트의 에셋 GUID 참조 누락");
                var font=view.title.font;
                font.RequestCharactersInTexture("차량상점보유선택무게속도",24,FontStyle.Normal);
                if(!font.GetCharacterInfo('차',out var glyph,24,FontStyle.Normal) || glyph.glyphWidth==0) throw new InvalidOperationException("한글 글리프 생성 실패");
                if(view.stats.Any(x=>x.fill.type!=Image.Type.Filled)) throw new InvalidOperationException("성능 막대는 Filled Image여야 합니다.");
                var shadowLights=roots.SelectMany(x=>x.GetComponentsInChildren<Light>(true)).Where(x=>x.isActiveAndEnabled && x.type==LightType.Directional && x.shadows!=LightShadows.None).ToArray();
                if(shadowLights.Length!=1 || shadowLights[0].name!="Preview Key") throw new InvalidOperationException("방향광 그림자 중복 또는 Preview Key 누락");
                result.values.Add(new BridgeValue{target="Main Menu UI",after="카드 "+view.cards.Length+", 성능 "+view.stats.Length+", 저장된 글꼴/참조 정상"});
                result.values.Add(new BridgeValue{target="Directional shadows",after=shadowLights[0].name+" (1개)"});
                result.checkedCount=result.passedCount=2;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
    [CustomEditor(typeof(MainMenuView))]
    public sealed class MainMenuViewInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var view=(MainMenuView)target;
            EditorGUILayout.HelpBox("배치·색·폰트는 자식 UI 오브젝트에서 편집합니다. 차량명·능력치·썸네일·표시 여부는 실행 시 갱신됩니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if(GUILayout.Button("편집 화면: 차량 목록")) Show(view,false);
                if(GUILayout.Button("편집 화면: 차량 상세")) Show(view,true);
            }
            DrawDefaultInspector();
        }
        private static void Show(MainMenuView view,bool detail)
        {
            if(view.list==null || view.detail==null) return;
            Undo.RecordObjects(new UnityEngine.Object[]{view.list.gameObject,view.detail.gameObject},"메뉴 편집 화면 전환");
            view.list.gameObject.SetActive(!detail); view.detail.gameObject.SetActive(detail);
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
        }
    }
}
