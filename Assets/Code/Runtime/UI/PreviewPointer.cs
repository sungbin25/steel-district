using UnityEngine;
using UnityEngine.EventSystems;

namespace SteelDistrict.UI
{
    // 씬에 저장하는 MonoBehaviour는 파일명과 클래스명을 일치시켜 MonoScript GUID로 참조합니다.
    public sealed class PreviewPointer : MonoBehaviour, IDragHandler, IScrollHandler
    {
        [Tooltip("드래그와 휠 입력을 전달할 차량 미리보기입니다.")] public VehiclePreview preview;
        public void OnDrag(PointerEventData data)
        { if(preview!=null && data.button==PointerEventData.InputButton.Left) preview.Orbit(data.delta); }
        public void OnScroll(PointerEventData data) { if(preview!=null) preview.Zoom(data.scrollDelta.y); }
    }
}
