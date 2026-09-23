using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SteelDistrict.Vehicles;
using SteelDistrict.Scenes;
using UnityEngine;
using UnityEngine.UI;

namespace SteelDistrict.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Tooltip("목록·미리보기·출전에 공통으로 사용할 차량 정의입니다.")] public VehicleCatalog catalog;
        [Tooltip("물리 없는 주차장 전시 공간입니다.")] public VehiclePreview preview;
        [Tooltip("씬에 저장한 편집 가능한 UI입니다. 배치와 스타일은 이 객체의 자식에서 수정합니다.")] public MainMenuView view;
        private const int PageSize=8;
        private RectTransform root,list,detail;
        private Text title,position,status,selected,heading,pageLabel;
        private Button selectButton,enterButton,previous,next,pagePrevious,pageNext;
        private readonly List<Texture2D> thumbnails=new List<Texture2D>();
        private VehicleEntry[] visible=System.Array.Empty<VehicleEntry>();
        private int index,page;
        private bool shop,generating;
        private Coroutine thumbnailJob;
        public bool InPreview=>detail!=null && detail.gameObject.activeSelf;
        public bool Ready=>!generating && visible.Length>0;
        public VehicleEntry Current=>visible.Length>0 ? visible[index] : null;
        public bool IsShop=>shop;
        private void Start()
        {
            if(preview==null || preview.previewCamera==null) { Debug.LogError("메뉴 미리보기 연결이 없습니다.",this); return; }
            if(!BindUI()) return;
            if(catalog==null) { status.text="차량 카탈로그가 없습니다."; return; }
            VehicleSession.ResolveSelected(catalog);
            ShowList(false);
        }
        private bool BindUI()
        {
            if(view==null || !view.IsValid) { Debug.LogError("저장된 Main Menu UI 연결이 없습니다. 메뉴 UI 생성 도구로 구성하세요.",this); return false; }
            root=view.GetComponent<RectTransform>(); list=view.list; detail=view.detail;
            title=view.title; position=view.position; status=view.status; selected=view.selected; heading=view.heading; pageLabel=view.pageLabel;
            selectButton=view.select; enterButton=view.enter; previous=view.previous; next=view.next; pagePrevious=view.pagePrevious; pageNext=view.pageNext;
            view.display.texture=preview.Texture;
            view.ownedTab.onClick.AddListener(()=>ShowList(false)); view.shopTab.onClick.AddListener(()=>ShowList(true));
            view.back.onClick.AddListener(()=>ShowList(shop)); selectButton.onClick.AddListener(SelectCurrent); enterButton.onClick.AddListener(EnterWorld);
            view.purchase.onClick.AddListener(PurchaseCurrent);
            previous.onClick.AddListener(()=>Move(-1)); next.onClick.AddListener(()=>Move(1));
            pagePrevious.onClick.AddListener(()=>ChangePage(-1)); pageNext.onClick.AddListener(()=>ChangePage(1));
            for(int i=0;i<PageSize;i++) { int local=i; view.cards[i].button.onClick.AddListener(()=>OpenPreview(page*PageSize+local)); }
            return true;
        }
        public void ShowList(bool showShop)
        {
            if(VehicleSession.Busy || catalog==null || root==null) return;
            CancelThumbnails(); shop=showShop; page=0; index=0;
            preview.Clear(); detail.gameObject.SetActive(false); list.gameObject.SetActive(true);
            visible=catalog.vehicles.Where(x=>x!=null && x.IsValid && (shop ? !VehicleSession.IsOwned(x) : VehicleSession.IsOwned(x))).ToArray();
            heading.text=(shop ? "차량 상점  /  미보유 차량" : "차고  /  보유 차량")+$"   {visible.Length}";
            BuildPage(); RefreshSelection();
        }
        private void ChangePage(int direction)
        {
            if(InPreview || VehicleSession.Busy) return;
            int target=Mathf.Clamp(page+direction,0,Mathf.Max(0,(visible.Length-1)/PageSize));
            if(target==page) return;
            CancelThumbnails(); page=target; BuildPage();
        }
        private void CancelThumbnails()
        {
            if(thumbnailJob!=null) StopCoroutine(thumbnailJob);
            thumbnailJob=null; generating=false;
            foreach(var image in thumbnails) Destroy(image);
            thumbnails.Clear();
        }
        private void BuildPage()
        {
            preview.Clear();
            int count=Mathf.Min(PageSize,visible.Length-page*PageSize);
            pageLabel.text=$"{page+1} / {Mathf.Max(1,(visible.Length+PageSize-1)/PageSize)}";
            pagePrevious.interactable=page>0; pageNext.interactable=(page+1)*PageSize<visible.Length;
            view.empty.gameObject.SetActive(count==0);
            for(int i=0;i<PageSize;i++) view.cards[i].button.gameObject.SetActive(i<count);
            if(count==0)
            {
                view.empty.text=shop ? "모든 차량을 보유하고 있습니다." : "보유 차량이 없습니다.";
                status.text=shop ? "미보유 차량만 상점에 표시됩니다." : "획득한 차량은 이곳에 표시됩니다.";
                return;
            }
            var images=new RawImage[count]; var buttons=new Button[count];
            for(int i=0;i<count;i++)
            {
                var entry=visible[page*PageSize+i]; var card=view.cards[i];
                card.button.interactable=false; buttons[i]=card.button;
                card.image.texture=null; images[i]=card.image;
                card.name.text=entry.displayName;
                card.hint.text=shop ? $"{VehicleSession.GetPrice(entry):N0} 골드 · 상세 보기" : "보유 · 상세 보기";
            }
            generating=true; status.text="차량 이미지를 준비하는 중…";
            thumbnailJob=StartCoroutine(GenerateThumbnails(images,buttons,page*PageSize));
        }
        private IEnumerator GenerateThumbnails(RawImage[] images,Button[] buttons,int offset)
        {
            // 한 페이지의 8개만 촬영하고 페이지 이동 시 해제합니다. 차량 수만큼 카메라/모델을 유지하지 않습니다.
            for(int i=0;i<images.Length;i++)
            {
                if(preview.Show(visible[offset+i]))
                {
                    float deadline=Time.realtimeSinceStartup+3;
                    while(preview.RenderedFrames<2 && Time.realtimeSinceStartup<deadline && SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null) yield return null;
                    if(preview.RenderedFrames>=2)
                    {
                        var old=RenderTexture.active;
                        var scaled=RenderTexture.GetTemporary(320,180,0);
                        try
                        {
                            Graphics.Blit(preview.Texture,scaled); RenderTexture.active=scaled;
                            var image=new Texture2D(320,180,TextureFormat.RGB24,false);
                            image.ReadPixels(new Rect(0,0,320,180),0,0); image.Apply();
                            thumbnails.Add(image); images[i].texture=image;
                        }
                        finally { RenderTexture.active=old; RenderTexture.ReleaseTemporary(scaled); }
                    }
                }
            }
            foreach(var button in buttons) button.interactable=true;
            preview.Clear(); generating=false; thumbnailJob=null;
            status.text=shop ? "차량을 선택해 가격을 확인하고 구매하세요." : "차량을 눌러 상세 정보를 확인하세요.";
        }
        public void OpenPreview(int requestedIndex) => OpenPreview(requestedIndex,0);
        private void OpenPreview(int requestedIndex,int direction)
        {
            if(generating || VehicleSession.Busy || visible.Length==0 || preview.Transitioning) return;
            int target=(requestedIndex%visible.Length+visible.Length)%visible.Length;
            if(!preview.Show(visible[target],direction)) { status.text="차량 모델을 불러올 수 없습니다."; return; }
            index=target; list.gameObject.SetActive(false); detail.gameObject.SetActive(true);
            title.text=Current.displayName; position.text=$"{index+1:00} / {visible.Length:00}";
            var configuration=VehicleSession.GetConfiguration(Current);
            Stat(0,"무게",configuration.mass,2000,"kg");
            Stat(1,"목표 최고 속도",configuration.maxSpeedKph,300,"km/h");
            Stat(2,"가속 설정",configuration.acceleration,40,"m/s²");
            Stat(3,"제동 설정",configuration.brakeAcceleration,40,"m/s²");
            Stat(4,"조향 속도",configuration.turnRate,180,"°/s");
            Stat(5,"최대 접지 가속도",configuration.maximumGripAcceleration,30,"m/s²");
            status.text=shop ? $"구매 가격: {VehicleSession.GetPrice(Current):N0} 골드" : "차량 성능은 현재 설정값입니다. 출전 차량으로 선택할 수 있습니다.";
            RefreshSelection();
        }
        private void Stat(int slot,string label,float value,float maximum,string unit)
        {
            var binding=view.stats[slot];
            binding.value.text=$"{value:0.#} {unit}";
            binding.fill.fillAmount=Mathf.Clamp01(value/Mathf.Max(.001f,binding.maximum));
        }
        public void Move(int direction)
        { if(InPreview && visible.Length>1) OpenPreview(index+direction,direction); }
        public void SelectCurrent()
        {
            if(!InPreview || shop || preview.Transitioning || !VehicleSession.Select(Current)) return;
            RefreshSelection(); status.text=Current.displayName+" 출전 준비 완료";
        }
        public void EnterWorld()
        {
            if(shop) return;
            var entry=VehicleSession.SelectedVehicle;
            if(entry==null || !entry.IsValid || !entry.availableInPrototype || !VehicleSession.IsOwned(entry)) { status.text="보유 차량을 먼저 선택하세요."; return; }
            if(!VehicleSession.Travel(VehicleSession.WorldScene)) status.text=VehicleSession.Error;
        }
        private void RefreshSelection()
        {
            var entry=VehicleSession.SelectedVehicle;
            view.gold.text=$"보유 골드  {VehicleSession.Gold:N0}";
            enterButton.gameObject.SetActive(!shop);
            selectButton.gameObject.SetActive(!shop);
            view.purchase.gameObject.SetActive(shop);
            bool canBuy=shop && InPreview && Current!=null && Current.availableInPrototype && !VehicleSession.IsOwned(Current);
            int price=canBuy ? VehicleSession.GetPrice(Current) : -1;
            view.purchaseLabel.text=canBuy ? $"구매  {price:N0} 골드" : "차량을 선택하세요";
            view.purchase.interactable=canBuy && price>=0 && VehicleSession.Gold>=price && !VehicleSession.Busy && !preview.Transitioning;
            selected.text="출전  /  "+(entry!=null ? entry.displayName : "선택 안 됨");
            selectButton.interactable=InPreview && !shop && Current!=null && Current.availableInPrototype && VehicleSession.IsOwned(Current) && !VehicleSession.Busy && !preview.Transitioning;
            enterButton.interactable=entry!=null && entry.IsValid && VehicleSession.IsOwned(entry) && !VehicleSession.Busy;
            previous.interactable=next.interactable=InPreview && visible.Length>1 && !preview.Transitioning && !VehicleSession.Busy;
        }
        public void PurchaseCurrent()
        {
            if(!shop || !InPreview || Current==null || generating || preview.Transitioning) return;
            var purchased=Current;
            if(!VehicleSession.TryPurchase(purchased,out string error)) { status.text=error; return; }
            // 상점 필터에서 즉시 제외하고 차고 상세로 이동합니다. 출전 확정은 기존 선택 버튼을 사용합니다.
            ShowList(false);
            CancelThumbnails();
            OpenPreview(System.Array.FindIndex(visible,x=>x.id==purchased.id));
            status.text=purchased.displayName+" 구매 완료 · 차고에 추가되었습니다.";
        }
        private void Update()
        {
            if(root==null || catalog==null) return;
            if(VehicleSession.Busy) status.text="월드로 이동하는 중…";
            else if(!string.IsNullOrEmpty(VehicleSession.Error)) status.text=VehicleSession.Error;
            RefreshSelection();
        }
        private void OnDestroy() { CancelThumbnails(); }
    }
}
