using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SteelDistrict.UI
{
    public static class MenuUI
    {
        public static readonly Color Ink = new Color(.035f, .05f, .075f);
        public static readonly Color Panel = new Color(.07f, .095f, .13f);
        public static readonly Color Accent = new Color(.08f, .76f, 1f);
        private static Font font;
        // OS 글꼴은 현재 PC 프로토타입용입니다. 모바일 배포 전 재배포 가능한 한글 폰트를 에셋으로 지정해야 합니다.
        public static Font Font => font != null ? font : font = UnityEngine.Font.CreateDynamicFontFromOSFont(
            new[] { "Malgun Gothic", "Noto Sans CJK KR", "Noto Sans KR", "Arial" }, 28);
        public static RectTransform Canvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            go.GetComponent<Canvas>().sortingOrder = 10;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null)
                new GameObject("UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            return go.GetComponent<RectTransform>();
        }
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = new Vector2(x, y); r.anchorMax = new Vector2(x + w, y + h);
            r.offsetMin = r.offsetMax = Vector2.zero;
            return r;
        }
        public static Image Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color; return image;
        }
        public static Text Text(Transform parent, string name, string value, float x, float y, float w, float h,
            int size = 24, Color? color = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = Font; text.text = value; text.fontSize = size; text.color = color ?? Color.white;
            text.alignment = align; text.raycastTarget = false;
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 12; text.resizeTextMaxSize = size;
            return text;
        }
        public static Button Button(Transform parent, string name, string value, float x, float y, float w, float h,
            UnityAction action, bool accent = false)
        {
            var background = Box(parent, name, x, y, w, h, accent ? Accent : Panel);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background; button.onClick.AddListener(action);
            Text(background.transform, "Label", value, .06f, .05f, .88f, .9f, 24, accent ? Ink : Color.white, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
