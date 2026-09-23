using UnityEngine;
using SteelDistrict.Vehicles;

namespace SteelDistrict.UI
{
    public sealed class VehiclePreview : MonoBehaviour
    {
        [Tooltip("전시 전용 레이어(31)의 차량과 주차장을 촬영합니다.")] public Camera previewCamera;
        [Tooltip("슬롯이 없을 때 사용할 기본 차량 위치입니다.")] public Transform pivot;
        [Tooltip("Editor에서 주차장을 최초 배치할 때 사용하는 원본입니다. 실행 중에는 생성하지 않습니다.")] public GameObject environmentPrefab;
        [Tooltip("MainMenuScene에 미리 배치한 MenuParking입니다. 배치는 이 객체에서 편집하며 실행 중에는 활성 상태만 전환합니다.")] public GameObject environmentRoot;
        [Tooltip("차량을 교대로 불러올 주차 위치입니다. 각 슬롯의 Y는 타이어가 닿을 바닥 높이(m)입니다.")] public Transform[] parkingSlots;
        [Tooltip("다음 주차 위치까지 카메라 이동 시간(초). 클수록 부드럽고 느리게 이동합니다.")]
        [Min(.1f)] public float transitionSeconds = .85f;
        private GameObject model, outgoing;
        private int slot;
        private float yaw = 140, pitch = 16, distance = 7, fittedDistance = 7;
        private Vector3 focus, startPosition;
        private Quaternion startRotation;
        private float elapsed;
        public bool Transitioning { get; private set; }
        public RenderTexture Texture { get; private set; }
        public string CurrentId { get; private set; }
        public bool Ready => model != null;
        public int RenderedFrames { get; private set; }
        private void OnEnable() => UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Rendered;
        private void OnDisable() => UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Rendered;
        private void Rendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        { if (camera == previewCamera && model != null) RenderedFrames++; }
        private void Awake()
        {
            Texture = new RenderTexture(1280, 720, 24) { name = "Vehicle Showroom", antiAliasing = 1 };
            Texture.Create(); previewCamera.targetTexture = Texture;
            previewCamera.aspect=16f/9f;
            if(environmentRoot!=null) environmentRoot.SetActive(false);
            else Debug.LogError("씬의 MenuParking을 VehiclePreview.environmentRoot에 연결하세요.",this);
        }
        public bool Show(VehicleEntry entry) => Show(entry, 0);
        public bool Show(VehicleEntry entry, int direction)
        {
            if(entry==null || !entry.IsValid || Transitioning || environmentRoot==null) return false;
            bool animate=model!=null && direction!=0 && parkingSlots!=null && parkingSlots.Length>1;
            int nextSlot=animate ? (slot+(direction>0?1:-1)+parkingSlots.Length)%parkingSlots.Length : 0;
            Transform anchor=parkingSlots!=null && parkingSlots.Length>0 ? parkingSlots[nextSlot] : pivot;
            if(anchor==null) return false;
            var next=Instantiate(entry.previewPrefab, anchor.position, anchor.rotation);
            SetLayer(next); next.SetActive(true);
            var renderers=next.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0) { Destroy(next); return false; }
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            // 프리팹마다 원점 높이가 달라도 타이어 밑면을 슬롯 바닥에 맞춥니다.
            float lift=anchor.position.y-bounds.min.y;
            next.transform.position+=Vector3.up*lift;
            focus=bounds.center+Vector3.up*lift;
            fittedDistance=Mathf.Max(5,bounds.size.magnitude*1.25f);
            if(!animate) { yaw=140; pitch=16; distance=fittedDistance; }
            else distance=Mathf.Clamp(distance,fittedDistance*.65f,fittedDistance*1.8f);
            Release(ref outgoing);
            if(animate) outgoing=model; else Release(ref model);
            model=next; slot=nextSlot; CurrentId=entry.id; RenderedFrames=0;
            environmentRoot.SetActive(true);
            previewCamera.enabled=true;
            if(animate)
            {
                startPosition=previewCamera.transform.position; startRotation=previewCamera.transform.rotation;
                elapsed=0; Transitioning=true;
            }
            else Pose();
            return true;
        }
        public void Orbit(Vector2 delta)
        {
            if(!Ready || Transitioning) return;
            yaw=Mathf.Repeat(yaw+delta.x*.25f,360); pitch=Mathf.Clamp(pitch-delta.y*.15f,7,65); Pose();
        }
        public void Zoom(float delta)
        {
            if(!Ready || Transitioning) return;
            distance=Mathf.Clamp(distance-delta*.5f,fittedDistance*.65f,fittedDistance*1.8f); Pose();
        }
        private void LateUpdate()
        {
            if(!Transitioning) return;
            elapsed+=Time.unscaledDeltaTime;
            float t=Mathf.Clamp01(elapsed/Mathf.Max(.1f,transitionSeconds));
            float eased=t*t*(3-2*t);
            var rotation=Quaternion.Euler(pitch,yaw,0);
            var destination=focus-rotation*Vector3.forward*distance;
            previewCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition,destination,eased),Quaternion.Slerp(startRotation,rotation,eased));
            if(t>=1) { Transitioning=false; Release(ref outgoing); }
        }
        private void Pose()
        {
            var rotation=Quaternion.Euler(pitch,yaw,0);
            previewCamera.transform.SetPositionAndRotation(focus-rotation*Vector3.forward*distance,rotation);
        }
        private static void SetLayer(GameObject go)
        { foreach(var child in go.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=31; }
        private void Release(ref GameObject go) { if(go!=null) { go.SetActive(false); Destroy(go); } go=null; }
        public void Clear()
        {
            Transitioning=false; Release(ref model); Release(ref outgoing); CurrentId=null;
            if(environmentRoot!=null) environmentRoot.SetActive(false);
            if(previewCamera!=null) previewCamera.enabled=false;
        }
        private void OnDestroy()
        {
            Clear();
            if(previewCamera!=null) previewCamera.targetTexture=null;
            if(Texture!=null) { Texture.Release(); Destroy(Texture); }
        }
    }
}
