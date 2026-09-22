using System;
using System.IO;
using System.Linq;
using SteelDistrict.Vehicles;
using SteelDistrict.UI;
using SteelDistrict.Scenes;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace SteelDistrict.Editor.Bridge
{
    [InitializeOnLoad]
    public static class MenuValidation
    {
        private const string Key="SteelDistrict.MenuValidation.";
        private const string Report="Logs/MenuValidation.txt";
        static MenuValidation()
        {
            if(File.Exists(".menu-validation-copy")) EditorApplication.update+=Tick;
        }
        public static void RunBatch()
        {
            if(!Application.isBatchMode || !File.Exists(".menu-validation-copy")) throw new InvalidOperationException("별도 검증 복사본에서만 실행하세요.");
            Directory.CreateDirectory("Logs"); File.WriteAllText(Report,"");
            try
            {
                var response=new BridgeResponse(); MainMenuSetup.RebuildPreviews(new BridgeRequest {apply=true},response);
                var catalog=AssetDatabase.LoadAssetAtPath<VehicleCatalog>(MainMenuSetup.CatalogPath);
                Check(catalog!=null && catalog.vehicles.Length==2,"두 차량 카탈로그");
                foreach(var entry in catalog.vehicles)
                {
                    var inspect=new BridgeResponse(); BridgeCommands.InspectVehicle(entry.drivePrefab,entry.id,inspect);
                    Check(!inspect.issues.Any(x=>x.severity=="error"),entry.id+" 필수 구성·참조·Collider");
                    Check(entry.previewPrefab.GetComponentsInChildren<MonoBehaviour>(true).Length==0 && entry.previewPrefab.GetComponentsInChildren<Rigidbody>(true).Length==0 && entry.previewPrefab.GetComponentsInChildren<Collider>(true).Length==0,entry.id+" 미리보기 물리·입력 제거");
                    var replay=VehicleReplayValidation.Run(new BridgeRequest {command="drive.test",scenario="suite",repeat=2,targets=new[]{new BridgeTarget{assetPath=AssetDatabase.GetAssetPath(entry.drivePrefab)}}});
                    File.WriteAllText("Logs/MenuReplay-"+entry.id+".json",JsonUtility.ToJson(replay,true));
                    Check(replay.ok,entry.id+" 5개 코스 × 2회: "+string.Join("; ",replay.issues.Where(x=>x.severity=="error").Select(x=>x.message)));
                }
                StartPlay();
            }
            catch(Exception e) { File.AppendAllText(Report,"FAIL "+e+"\n"); EditorApplication.Exit(1); }
        }
        public static void RunVisual()
        {
            if(!File.Exists(".menu-validation-copy")) throw new InvalidOperationException("별도 검증 복사본에서만 실행하세요.");
            Directory.CreateDirectory("Logs"); File.WriteAllText(Report,"");
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
            MainMenuSetup.SetInitialPreviewLighting();
            StartPlay();
        }
        private static void StartPlay()
        {
            EditorSceneManager.OpenScene(VehicleSession.MenuScene,OpenSceneMode.Single);
            SessionState.SetString(Key+"error",""); SessionState.SetFloat(Key+"started",(float)EditorApplication.timeSinceStartup);
            Phase("menu"); EditorApplication.EnterPlaymode();
        }
        private static void Phase(string phase) { SessionState.SetString(Key+"phase",phase); SessionState.SetFloat(Key+"at",(float)EditorApplication.timeSinceStartup); }
        private static double Age => EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"at",0);
        private static void Check(bool value,string label)
        {
            File.AppendAllText(Report,(value?"PASS ":"FAIL ")+label+"\n");
            if(!value) throw new InvalidOperationException(label);
        }
        private static void Click(string name)
        {
            var button=Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x=>x.name==name);
            Check(button.interactable,"버튼 활성: "+name); button.onClick.Invoke();
        }
        private static void Capture(string name)
        {
            if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null) ScreenCapture.CaptureScreenshot("Logs/"+name+".png");
        }
        private static void Tick()
        {
            string phase=SessionState.GetString(Key+"phase",""); if(string.IsNullOrEmpty(phase)) return;
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"started",0)>180) throw new InvalidOperationException("메뉴 Play 검증 시간 초과: "+phase);
                if(phase=="stop" && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    bool ok=string.IsNullOrEmpty(SessionState.GetString(Key+"error",""));
                    File.AppendAllText(Report,ok?"ALL CHECKS PASSED\n":"FAIL "+SessionState.GetString(Key+"error","")+"\n");
                    SessionState.EraseString(Key+"phase"); EditorApplication.Exit(ok?0:1); return;
                }
                if(!Application.isPlaying || VehicleSession.Busy) return;
                Application.runInBackground=true;
                var menu=Object.FindFirstObjectByType<MainMenuController>();
                if(phase=="menu" && menu!=null && menu.Ready && Age>2)
                {
                    Check(!menu.InPreview,"첫 화면 차량 목록");
                    Check(Object.FindObjectsByType<ArcadeVehicleDrive>(FindObjectsSortMode.None).Length==0,"메뉴에서 주행 모듈 없음");
                    Capture("Menu-List"); Phase("preview");
                }
                else if(phase=="preview" && Age>1)
                {
                    Click("Card_prometheus"); Check(menu.InPreview && menu.Current.id=="prometheus","Prometheus 썸네일 → 3D");
                    Check(VehicleSession.SelectedId=="simple-retro","미리보기와 출전 차량 분리");
                    Click("Next"); Check(menu.Current.id=="simple-retro","다음 차량 순환");
                    Click("Previous"); Check(menu.Current.id=="prometheus","이전 차량 순환");
                    var pointer=Object.FindFirstObjectByType<PreviewPointer>(); var camera=menu.preview.previewCamera;
                    Quaternion before=camera.transform.rotation;
                    ExecuteEvents.Execute(pointer.gameObject,new PointerEventData(EventSystem.current){delta=new Vector2(80,20),button=PointerEventData.InputButton.Left},ExecuteEvents.dragHandler);
                    Check(Quaternion.Angle(before,camera.transform.rotation)>1,"미리보기 포인터 회전");
                    Vector3 position=camera.transform.position;
                    ExecuteEvents.Execute(pointer.gameObject,new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,1)},ExecuteEvents.scrollHandler);
                    Check(Vector3.Distance(position,camera.transform.position)>.1f,"미리보기 휠 확대");
                    Capture("Menu-Prometheus"); Phase("shop");
                }
                else if(phase=="shop" && Age>1)
                {
                    Click("ShopTab"); Click("Card_prometheus");
                    Check(!Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(x=>x.name=="SelectVehicle").interactable,"상점 미리보기에서 출전 변경 차단");
                    Click("OwnedTab"); Click("Card_prometheus"); Click("SelectVehicle");
                    Check(VehicleSession.SelectedId=="prometheus","출전 차량 확정"); Click("EnterWorld");
                    Check(!VehicleSession.Travel(VehicleSession.WorldScene),"연속 월드 진입 차단"); Phase("prom-world");
                }
                else if(phase=="prom-world" && Age>3)
                {
                    var world=Object.FindFirstObjectByType<WorldVehicleController>();
                    Check(world!=null && world.ActiveVehicle!=null && world.ActiveVehicle.name=="Prometheus","GameScene Prometheus 선택 반영");
                    Check(Object.FindObjectsByType<ArcadeVehicleDrive>(FindObjectsSortMode.None).Length==1,"월드 활성 차량 한 대");
                    Check(world.followCamera.Target==world.ActiveVehicle.transform,"추적 카메라 연결");
                    Check(world.ActiveVehicle.GetComponent<ArcadeVehicleDrive>().GroundedWheels==4,"GameScene Prometheus 네 바퀴 접지");
                    var p=world.ActiveVehicle.transform.position; SessionState.SetString(Key+"origin",JsonUtility.ToJson(new Position{value=p}));
                    world.ActiveVehicle.GetComponent<VehicleDriveInput>().SetReplayCommand(new VehicleDriveCommand{Throttle=1}); Phase("drive");
                }
                else if(phase=="drive" && Age>2)
                {
                    var world=Object.FindFirstObjectByType<WorldVehicleController>();
                    Check(Vector3.Distance(world.ActiveVehicle.transform.position,JsonUtility.FromJson<Position>(SessionState.GetString(Key+"origin","")).value)>3,"실제 Play Mode Prometheus 이동");
                    Capture("World-Prometheus"); Click("ReturnMenu"); Phase("return");
                }
                else if(phase=="return" && menu!=null && menu.Ready && Age>2)
                {
                    Check(VehicleSession.SelectedId=="prometheus","메뉴 복귀 후 출전 선택 유지");
                    Check(Object.FindObjectsByType<ArcadeVehicleDrive>(FindObjectsSortMode.None).Length==0,"복귀 후 월드 차량 해제");
                    Click("Card_simple-retro"); Capture("Menu-Retro"); Phase("retro-select");
                }
                else if(phase=="retro-select" && Age>1)
                { Click("SelectVehicle"); Click("EnterWorld"); Phase("retro-world"); }
                else if(phase=="retro-world" && Age>3)
                {
                    var world=Object.FindFirstObjectByType<WorldVehicleController>();
                    Check(world.ActiveVehicle.name=="Simple Retro" && !world.originalVehicle.activeSelf && !world.prometheusVehicle.activeSelf,"Simple Retro 선택 시 카탈로그 프리팹 사용·배치 차량 중복 제거");
                    Check(Mathf.Approximately(world.ActiveVehicle.GetComponent<ArcadeVehicleDrive>().TargetMaxSpeedKph,world.catalog.Find("simple-retro").Drive.TargetMaxSpeedKph),"성능 표시와 월드 주행 설정 일치");
                    Check(Object.FindObjectsByType<ArcadeVehicleDrive>(FindObjectsSortMode.None).Length==1,"두 번째 왕복에도 활성 차량 한 대");
                    Click("ReturnMenu"); Phase("final-menu");
                }
                else if(phase=="final-menu" && menu!=null && menu.Ready && Age>2)
                {
                    Check(VehicleSession.SelectedId=="simple-retro","두 번째 복귀 선택 유지");
                    Check(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"왕복 후 EventSystem 한 개");
                    Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x=>x.enabled)==1,"왕복 후 AudioListener 한 개");
                    Capture("Menu-List"); Phase("final-capture");
                }
                else if(phase=="final-capture" && Age>3) { Phase("stop"); EditorApplication.ExitPlaymode(); }
            }
            catch(Exception e)
            {
                SessionState.SetString(Key+"error",e.ToString()); Phase("stop");
                SessionState.SetFloat(Key+"started",(float)EditorApplication.timeSinceStartup);
                if(EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
            }
        }
        [Serializable] private sealed class Position { public Vector3 value; }
    }
}
