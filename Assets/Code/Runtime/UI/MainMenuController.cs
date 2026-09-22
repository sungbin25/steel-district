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
        [Tooltip("물리 없는 차량 전시 공간입니다.")] public VehiclePreview preview;
        private RectTransform root, list, detail, stats;
        private Text title, position, status, selected;
        private Button selectButton, enterButton;
        private readonly Dictionary<string, Texture2D> thumbnails = new Dictionary<string, Texture2D>();
        private VehicleEntry[] visible = System.Array.Empty<VehicleEntry>();
        private int index;
        private bool shop, generating;
        public bool InPreview => detail != null && detail.gameObject.activeSelf;
        public bool Ready => !generating && visible.Length > 0;
        public VehicleEntry Current => visible.Length > 0 ? visible[index] : null;
        public bool IsShop => shop;
        private IEnumerator Start()
        {
            BuildUI();
            if (catalog == null || catalog.DefaultVehicle == null) { status.text = "유효한 차량 카탈로그가 없습니다."; yield break; }
            if (catalog.Find(VehicleSession.SelectedId) == null) VehicleSession.Select(catalog.DefaultVehicle);
            // 첫 목록에서 두 장의 썸네일만 촬영해 재사용합니다. 목록용 카메라를 차량마다 유지하지 않습니다.
            generating = true; status.text = "차량 목록을 준비하는 중…";
            foreach (var entry in catalog.vehicles.Where(x => x != null && x.IsValid))
            {
                if (!preview.Show(entry)) continue;
                float deadline = Time.realtimeSinceStartup + 10;
                while (preview.RenderedFrames < 3 && Time.realtimeSinceStartup < deadline &&
                    SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null) yield return null;
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    var old = RenderTexture.active;
                    RenderTexture.active = preview.Texture;
                    var image = new Texture2D(preview.Texture.width, preview.Texture.height, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply();
                    RenderTexture.active = old; thumbnails[entry.id] = image;
                }
            }
            preview.Clear(); generating = false; ShowList(false);
        }
        private void BuildUI()
        {
            root = MenuUI.Canvas("Main Menu UI");
            MenuUI.Box(root, "Background", 0, 0, 1, 1, MenuUI.Ink);
            MenuUI.Text(root, "Brand", "STEEL DISTRICT", .04f, .90f, .48f, .07f, 40);
            MenuUI.Text(root, "Subtitle", "차량을 선택하고 거리로 나가세요", .04f, .85f, .48f, .04f, 20, new Color(.6f,.67f,.75f));
            MenuUI.Button(root, "OwnedTab", "보유 차량", .58f, .89f, .16f, .065f, () => ShowList(false));
            MenuUI.Button(root, "ShopTab", "상점", .76f, .89f, .16f, .065f, () => ShowList(true));
            list = MenuUI.Rect(root, "Vehicle List", .04f, .20f, .92f, .60f);
            detail = MenuUI.Rect(root, "Vehicle Detail", .04f, .19f, .92f, .65f);
            var frame = MenuUI.Rect(detail, "Preview Frame", .19f, .29f, .62f, .66f);
            var display = MenuUI.Rect(frame, "Orbit Area", 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
            var fit = display.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f/9f;
            display.texture = preview.Texture;
            display.gameObject.AddComponent<PreviewPointer>().preview = preview;
            title = MenuUI.Text(detail, "Vehicle Name", "", 0, .87f, .42f, .10f, 32);
            position = MenuUI.Text(detail, "Index", "", .83f, .85f, .17f, .08f, 24, MenuUI.Accent, TextAnchor.MiddleRight);
            MenuUI.Button(detail, "Previous", "〈", .01f, .49f, .08f, .16f, () => Move(-1));
            MenuUI.Button(detail, "Next", "〉", .91f, .49f, .08f, .16f, () => Move(1));
            MenuUI.Text(detail, "Orbit Help", "드래그: 둘러보기   ·   휠: 확대/축소", .30f, .26f, .40f, .05f, 18, new Color(.6f,.67f,.75f), TextAnchor.MiddleCenter);
            stats = MenuUI.Rect(detail, "Performance", 0, 0, 1, .23f);
            detail.gameObject.SetActive(false);
            status = MenuUI.Text(root, "Status", "", .04f, .14f, .64f, .04f, 18, new Color(.6f,.67f,.75f));
            selected = MenuUI.Text(root, "Selected", "", .04f, .045f, .33f, .065f, 22);
            MenuUI.Button(root, "BackToList", "목록으로", .39f, .045f, .14f, .065f, () => ShowList(shop));
            selectButton = MenuUI.Button(root, "SelectVehicle", "출전 차량 선택", .55f, .045f, .18f, .065f, SelectCurrent);
            enterButton = MenuUI.Button(root, "EnterWorld", "월드 진입  →", .75f, .045f, .21f, .065f, EnterWorld, true);
        }
        public void ShowList(bool showShop)
        {
            if (generating || VehicleSession.Busy || catalog == null) return;
            shop = showShop; preview.Clear(); detail.gameObject.SetActive(false); list.gameObject.SetActive(true);
            foreach (Transform child in list) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            visible = catalog.vehicles.Where(x => x != null && x.IsValid && (shop || x.availableInPrototype)).ToArray();
            MenuUI.Text(list, "Heading", shop ? "상점 / 전체 차량" : "보유 차량 / 개발용 차고", 0, .86f, 1, .12f, 28);
            if (visible.Length == 0) MenuUI.Text(list, "Empty", "표시할 차량이 없습니다.", 0, .4f, 1, .2f);
            // 현재 카탈로그는 두 대. 가로 스크롤로 이후 차량이 늘어나도 화면 밖 선택을 막지 않습니다.
            var view = MenuUI.Rect(list, "Scroll View", 0, 0, 1, .83f);
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            var content = MenuUI.Rect(view, "Cards", 0, 0, 1, 1);
            content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0,1); content.pivot = Vector2.zero;
            content.sizeDelta = new Vector2(Mathf.Max(1,visible.Length) * 490,0);
            scroll.viewport = view; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i=0; i<visible.Length; i++)
            {
                int captured = i; var entry = visible[i];
                float count = Mathf.Max(1,visible.Length);
                var card = MenuUI.Button(content, "Card_"+entry.id, "", i/count, .04f, .94f/count, .92f, () => OpenPreview(captured));
                var imageFrame = MenuUI.Rect(card.transform, "Image Frame", .02f, .24f, .96f, .72f);
                var raw = MenuUI.Rect(imageFrame, "Thumbnail", 0, 0, 1, 1).gameObject.AddComponent<RawImage>();
                var imageFit = raw.gameObject.AddComponent<AspectRatioFitter>();
                imageFit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; imageFit.aspectRatio = 16f/9f;
                raw.raycastTarget = false;
                if (thumbnails.TryGetValue(entry.id, out var texture)) raw.texture = texture;
                MenuUI.Text(card.transform, "Name", entry.displayName, .06f, .08f, .88f, .12f, 28);
                MenuUI.Text(card.transform, "Hint", "클릭하여 3D 미리보기  →", .06f, .005f, .88f, .07f, 16, MenuUI.Accent);
            }
            status.text = shop ? "상점 미리보기 · 구매 기능은 준비 중입니다." : "개발용 차량 2대 · 선택 상태는 현재 플레이 세션에서 유지됩니다.";
            RefreshSelection();
        }
        public void OpenPreview(int requestedIndex)
        {
            if (generating || VehicleSession.Busy || visible.Length == 0) return;
            index = (requestedIndex % visible.Length + visible.Length) % visible.Length;
            if (!preview.Show(Current)) { status.text = "차량 모델을 불러올 수 없습니다."; return; }
            list.gameObject.SetActive(false); detail.gameObject.SetActive(true);
            title.text = Current.displayName; position.text = $"{index+1:00} / {visible.Length:00}";
            foreach (Transform child in stats) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            Stat(0, "무게", Current.Mass, 2000, "kg");
            Stat(1, "목표 최고 속도", Current.Drive.TargetMaxSpeedKph, 300, "km/h");
            Stat(2, "가속 설정", Current.Drive.AccelerationSetting, 40, "m/s²");
            Stat(3, "제동 설정", Current.Drive.BrakeAccelerationSetting, 40, "m/s²");
            status.text = "막대는 공통 범위의 설정값입니다. 실제 측정 기록이나 성능 점수가 아닙니다.";
            RefreshSelection();
        }
        private void Stat(int slot, string label, float value, float maximum, string unit)
        {
            var panel = MenuUI.Box(stats, label, slot*.25f, 0, .235f, 1, MenuUI.Panel);
            MenuUI.Text(panel.transform, "Label", label, .07f, .62f, .86f, .27f, 18, new Color(.6f,.67f,.75f));
            MenuUI.Text(panel.transform, "Value", $"{value:0.#} {unit}", .07f, .27f, .86f, .34f, 26);
            MenuUI.Box(panel.transform, "Range", .07f, .14f, .86f, .055f, MenuUI.Ink);
            MenuUI.Box(panel.transform, "ValueBar", .07f, .14f, .86f*Mathf.Clamp01(value/maximum), .055f, MenuUI.Accent).raycastTarget=false;
        }
        public void Move(int direction) { if (InPreview) OpenPreview(index+direction); }
        public void SelectCurrent()
        {
            if (!InPreview || shop || !VehicleSession.Select(Current)) return;
            RefreshSelection(); status.text = Current.displayName + " 출전 준비 완료";
        }
        public void EnterWorld()
        {
            var entry = catalog.Find(VehicleSession.SelectedId);
            if (entry == null || !entry.IsValid || !entry.availableInPrototype) { status.text="출전 차량을 먼저 선택하세요."; return; }
            if (!VehicleSession.Travel(VehicleSession.WorldScene)) status.text = VehicleSession.Error;
        }
        private void RefreshSelection()
        {
            var entry = catalog.Find(VehicleSession.SelectedId);
            selected.text = "출전  /  " + (entry != null ? entry.displayName : "선택 안 됨");
            selectButton.interactable = InPreview && !shop && Current != null && Current.availableInPrototype && !VehicleSession.Busy;
            enterButton.interactable = entry != null && entry.IsValid && !VehicleSession.Busy;
        }
        private void Update()
        {
            if (root == null || catalog == null) return;
            if (VehicleSession.Busy) status.text = "월드로 이동하는 중…";
            else if (!string.IsNullOrEmpty(VehicleSession.Error)) status.text = VehicleSession.Error;
            RefreshSelection();
        }
        private void OnDestroy() { foreach (var image in thumbnails.Values) Destroy(image); }
    }
}
