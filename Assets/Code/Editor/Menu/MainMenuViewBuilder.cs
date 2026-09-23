using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using SteelDistrict.UI;
namespace SteelDistrict.Editor.Bridge
{
    // 최초 생성에만 사용하는 배치입니다. 이미 저장된 UI에는 재적용하지 않습니다.
    internal sealed class MainMenuViewBuilder
    {
        private RectTransform root,list,detail,stats,cards;
        private Text title,position,status,selected,heading,pageLabel;
        private Button selectButton,enterButton,previous,next,pagePrevious,pageNext;
        public MainMenuView Build(MainMenuController controller)
        {
            var preview=controller.preview;
            root=MenuUI.Canvas("Main Menu UI");
            MenuUI.Box(root,"Background",0,0,1,1,MenuUI.Ink);
            detail=MenuUI.Rect(root,"Vehicle Detail",0,0,1,1);
            var display=MenuUI.Rect(detail,"Orbit Area",0,0,1,1).gameObject.AddComponent<RawImage>();
            display.color=new Color(.2f,.3f,.4f);
            display.gameObject.AddComponent<PreviewPointer>().preview=preview;
            var fit=display.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode=AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio=16f/9f;
            MenuUI.Box(detail,"Performance Backdrop",0,.16f,1,.20f,new Color(.025f,.055f,.10f,.96f));
            title=MenuUI.Text(detail,"Vehicle Name","",.10f,.36f,.80f,.07f,36,Color.white,TextAnchor.MiddleCenter);
            position=MenuUI.Text(detail,"Index","",.82f,.82f,.13f,.05f,22,MenuUI.Accent,TextAnchor.MiddleRight);
            previous=MenuUI.Button(detail,"Previous","〈",.025f,.49f,.075f,.14f,()=>{},true);
            next=MenuUI.Button(detail,"Next","〉",.90f,.49f,.075f,.14f,()=>{},true);
            MenuUI.Text(detail,"Orbit Help","드래그: 360° 둘러보기  ·  휠: 확대/축소",.25f,.79f,.50f,.035f,17,Color.white,TextAnchor.MiddleCenter);
            stats=MenuUI.Rect(detail,"Performance",.035f,.18f,.93f,.15f);
            detail.gameObject.SetActive(false);
            list=MenuUI.Rect(root,"Vehicle List",.035f,.18f,.93f,.65f);
            heading=MenuUI.Text(list,"Heading","",0,.89f,.7f,.09f,27);
            cards=MenuUI.Rect(list,"Cards",0,.10f,1,.77f);
            pagePrevious=MenuUI.Button(list,"Previous Page","〈",.34f,0,.07f,.075f,()=>{});
            pageLabel=MenuUI.Text(list,"Page","",.42f,0,.16f,.075f,19,Color.white,TextAnchor.MiddleCenter);
            pageNext=MenuUI.Button(list,"Next Page","〉",.59f,0,.07f,.075f,()=>{});
            MenuUI.Box(root,"Header",0,.87f,1,.13f,new Color(.02f,.04f,.07f,.96f));
            MenuUI.Box(root,"Header Line",0,.867f,1,.003f,MenuUI.Accent).raycastTarget=false;
            MenuUI.Text(root,"Brand","STEEL DISTRICT  /  차량 선택",.035f,.905f,.54f,.055f,31);
            MenuUI.Button(root,"OwnedTab","차고",.66f,.902f,.13f,.06f,()=>{});
            MenuUI.Button(root,"ShopTab","차량 상점",.81f,.902f,.15f,.06f,()=>{});
            MenuUI.Box(root,"Footer",0,0,1,.16f,new Color(.02f,.04f,.07f,.97f));
            status=MenuUI.Text(root,"Status","",.035f,.112f,.93f,.035f,17,new Color(.65f,.76f,.85f));
            selected=MenuUI.Text(root,"Selected","",.035f,.035f,.31f,.055f,20);
            MenuUI.Button(root,"BackToList","목록으로",.39f,.03f,.14f,.065f,()=>{});
            selectButton=MenuUI.Button(root,"SelectVehicle","출전 차량 선택",.55f,.03f,.18f,.065f,()=>{});
            enterButton=MenuUI.Button(root,"EnterWorld","월드 진입  →",.75f,.03f,.21f,.065f,()=>{},true);
            var view=root.gameObject.AddComponent<MainMenuView>();
            view.list=list; view.detail=detail; view.display=display; view.display.color=Color.white;
            view.title=title; view.position=position; view.status=status; view.selected=selected; view.heading=heading; view.pageLabel=pageLabel;
            view.select=selectButton; view.enter=enterButton; view.previous=previous; view.next=next; view.pagePrevious=pagePrevious; view.pageNext=pageNext;
            view.ownedTab=root.Find("OwnedTab").GetComponent<Button>(); view.shopTab=root.Find("ShopTab").GetComponent<Button>(); view.back=root.Find("BackToList").GetComponent<Button>();
            view.empty=MenuUI.Text(cards,"Empty","표시할 차량이 없습니다.",0,.35f,1,.2f,26,Color.white,TextAnchor.MiddleCenter);
            view.empty.gameObject.SetActive(false);
            view.cards=new MainMenuView.Card[8];
            for(int i=0;i<8;i++)
            {
                int col=i%4,row=i/4;
                var card=MenuUI.Button(cards,"Card Slot "+(i+1),"",col*.25f,.51f-row*.51f,.238f,.48f,()=>{});
                var image=MenuUI.Rect(card.transform,"Thumbnail",.025f,.22f,.95f,.75f).gameObject.AddComponent<RawImage>();
                image.raycastTarget=false;
                var name=MenuUI.Text(card.transform,"Name",i==0?"Simple Retro":"차량 슬롯 "+(i+1),.045f,.085f,.91f,.13f,22);
                var hint=MenuUI.Text(card.transform,"Hint","보유 · 상세 보기",.045f,.01f,.91f,.08f,14,MenuUI.Accent);
                view.cards[i]=new MainMenuView.Card{button=card,image=image,name=name,hint=hint};
            }
            string[] labels={"무게","목표 최고 속도","가속 설정","제동 설정","조향 속도","최대 접지 가속도"};
            float[] maxima={2000,300,40,40,180,30};
            string[] samples={"1700 kg","120 km/h","14 m/s²","24 m/s²","100 °/s","15 m/s²"};
            view.stats=new MainMenuView.Stat[6];
            for(int i=0;i<6;i++)
            {
                var panel=MenuUI.Box(stats,labels[i],i/6f,0,.155f,1,MenuUI.Panel);
                MenuUI.Text(panel.transform,"Label",labels[i],.07f,.63f,.86f,.28f,17,new Color(.65f,.76f,.85f));
                var value=MenuUI.Text(panel.transform,"Value",samples[i],.07f,.26f,.86f,.35f,24);
                MenuUI.Box(panel.transform,"Range",.07f,.12f,.86f,.055f,MenuUI.Ink);
                var fill=MenuUI.Box(panel.transform,"ValueBar",.07f,.12f,.86f,.055f,MenuUI.Accent);
                fill.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                fill.type=Image.Type.Filled; fill.fillMethod=Image.FillMethod.Horizontal; fill.fillOrigin=0; fill.fillAmount=.6f; fill.raycastTarget=false;
                view.stats[i]=new MainMenuView.Stat{value=value,fill=fill,maximum=maxima[i]};
            }
            title.text="선택한 차량"; position.text="01 / 01"; heading.text="차고 / 보유 차량"; pageLabel.text="1 / 1";
            status.text="편집용 배치 · 실행 시 차량 데이터로 갱신됩니다."; selected.text="출전 / Simple Retro";
            VehicleShopSetup.Configure(view);
            return view;
        }

    }
}
