using System;
using System.IO;
using System.Linq;
using SteelDistrict.UI;
using SteelDistrict.Scenes;
using SteelDistrict.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SteelDistrict.Editor.Bridge
{
    public static class VehicleShopSetup
    {
        // 최초 한 번만 구매 UI와 세로 그래프를 구성합니다. 이후 사용자의 배치를 다시 덮어쓰지 않습니다.
        public static void Configure(MainMenuView view)
        {
            if(view.gold!=null && view.purchase!=null && view.purchaseLabel!=null) return;
            var root=view.transform;
            if(view.gold==null) view.gold=MenuUI.Text(root,"Gold","보유 골드  1,000,000",.70f,.952f,.26f,.036f,23,MenuUI.Accent,TextAnchor.MiddleRight);
            if(view.purchase==null)
            {
                view.purchase=MenuUI.Button(root,"PurchaseVehicle","구매  10,000 골드",.75f,.03f,.21f,.065f,()=>{},true);
                view.purchase.gameObject.SetActive(false);
            }
            view.purchaseLabel=view.purchase.transform.Find("Label").GetComponent<Text>();
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Fonts/NanumGothic-Regular.ttf");
            view.gold.font=font; view.purchaseLabel.font=font;
            Place(view.ownedTab.GetComponent<RectTransform>(),.66f,.889f,.13f,.052f);
            Place(view.shopTab.GetComponent<RectTransform>(),.81f,.889f,.15f,.052f);
            var backdrop=view.detail.Find("Performance Backdrop").GetComponent<RectTransform>();
            Place(backdrop,.025f,.16f,.40f,.34f);
            var stats=view.stats[0].value.transform.parent.parent.GetComponent<RectTransform>();
            Place(stats,.035f,.172f,.38f,.316f);
            var heading=MenuUI.Text(stats,"Performance Heading","차량 성능",.035f,.885f,.93f,.105f,24,Color.white);
            heading.font=font;
            for(int i=0;i<view.stats.Length;i++)
            {
                var binding=view.stats[i]; var panel=binding.value.transform.parent.GetComponent<RectTransform>();
                Place(panel,0,.75f-i*.14f,1,.125f);
                Place(panel.Find("Label").GetComponent<RectTransform>(),.025f,0,.33f,1);
                Place(panel.Find("Range").GetComponent<RectTransform>(),.38f,.35f,.34f,.30f);
                Place(binding.fill.rectTransform,.38f,.35f,.34f,.30f);
                Place(binding.value.rectTransform,.74f,0,.24f,1);
                Undo.RecordObject(binding.value,"성능 값 우측 정렬"); binding.value.alignment=TextAnchor.MiddleRight;
            }
            Place(view.title.rectTransform,.45f,.365f,.50f,.07f);
            EditorUtility.SetDirty(view);
        }
        private static void Place(RectTransform rect,float x,float y,float w,float h)
        {
            Undo.RecordObject(rect,"상점 UI 배치");
            rect.anchorMin=new Vector2(x,y); rect.anchorMax=new Vector2(x+w,y+h); rect.offsetMin=rect.offsetMax=Vector2.zero;
        }
        public static void Apply(BridgeRequest request,BridgeResponse result)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 실행하세요.");
            var scene=SceneManager.GetSceneByPath(VehicleSession.MenuScene);
            if(!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("MainMenuScene을 열어 두세요.");
            var controller=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MainMenuController>(true)).Single();
            if(controller.view==null || controller.catalog==null) throw new InvalidOperationException("메뉴 View/카탈로그 누락");
            bool configured=controller.view.gold!=null && controller.view.purchase!=null && controller.view.purchaseLabel!=null;
            result.values.Add(new BridgeValue{target="Main Menu UI",before=configured?"이미 구성됨":"구매 UI 없음 / 성능 가로 카드",after="보유 골드·상점 구매·왼쪽 하단 세로 성능",changed=!configured});
            result.values.Add(new BridgeValue{target=MainMenuSetup.CatalogPath,property="initialGold",before=controller.catalog.initialGold.ToString(),after=controller.catalog.initialGold.ToString()});
            foreach(var entry in controller.catalog.vehicles) result.values.Add(new BridgeValue{target=entry.id,property="purchasePrice",before=entry.purchasePrice.ToString(),after=entry.purchasePrice.ToString()});
            if(!request.apply || configured) return;
            string backup="Logs/VehicleShop-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity";
            if(!EditorSceneManager.SaveScene(scene,backup,true)) throw new IOException("메뉴 편집 상태 백업 실패");
            result.reportPath=backup;
            Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("차량 구매 UI 구성");
            var before=controller.view.GetComponentsInChildren<Transform>(true).Select(x=>x.gameObject).ToArray();
            try
            {
                Undo.RecordObject(controller.view,"구매 UI 연결");
                Configure(controller.view);
                foreach(var created in controller.view.GetComponentsInChildren<Transform>(true).Select(x=>x.gameObject).Except(before)) Undo.RegisterCreatedObjectUndo(created,"구매 UI 추가");
                // 새 필드의 초기값을 에셋에 기록합니다. 기존 설정값은 덮어쓰지 않습니다.
                EditorUtility.SetDirty(controller.catalog); AssetDatabase.SaveAssetIfDirty(controller.catalog);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new IOException("메뉴 저장 실패");
                Undo.CollapseUndoOperations(group); result.changedCount=1;
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
        public static void Check(BridgeResponse result)
        {
            int gold=1000000; bool owned=false;
            Require(VehiclePurchaseTransaction.TryCommit(ref gold,ref owned,10000,out _) && gold==990000 && owned,"정상 구매");
            Require(!VehiclePurchaseTransaction.TryCommit(ref gold,ref owned,10000,out _) && gold==990000 && owned,"중복 구매 차단");
            gold=9999; owned=false;
            Require(!VehiclePurchaseTransaction.TryCommit(ref gold,ref owned,10000,out _) && gold==9999 && !owned,"잔액 부족 보존");
            Require(!VehiclePurchaseTransaction.TryCommit(ref gold,ref owned,-1,out _) && gold==9999 && !owned,"음수 가격 거부");
            gold=10000;
            Require(VehiclePurchaseTransaction.TryCommit(ref gold,ref owned,10000,out _) && gold==0 && owned,"정확한 잔액 구매");
            var scene=EditorSceneManager.OpenPreviewScene(VehicleSession.MenuScene);
            try
            {
                var menu=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<MainMenuController>(true)).Single();
                Require(menu.view!=null && menu.view.IsValid,"구매/골드 UI 참조");
                Require(menu.catalog.initialGold>=0 && menu.catalog.vehicles.All(x=>x.purchasePrice>=0),"골드/가격 유효성");
                result.values.Add(new BridgeValue{target="구매 규칙",after="정상·중복·잔액 부족·음수 가격·정확한 잔액: 통과"});
                result.values.Add(new BridgeValue{target=MainMenuSetup.CatalogPath,property="initialGold",after=menu.catalog.initialGold.ToString()});
                foreach(var entry in menu.catalog.vehicles) result.values.Add(new BridgeValue{target=entry.id,property="purchasePrice",after=entry.purchasePrice.ToString()});
                result.checkedCount=result.passedCount=7;
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static void Require(bool value,string label) { if(!value) throw new InvalidOperationException(label+" 실패"); }
    }
}
