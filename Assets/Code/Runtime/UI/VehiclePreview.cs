using UnityEngine;
using UnityEngine.EventSystems;
using SteelDistrict.Vehicles;

namespace SteelDistrict.UI
{
    public sealed class VehiclePreview : MonoBehaviour
    {
        [Tooltip("전용 레이어의 차량만 촬영하는 미리보기 카메라입니다.")] public Camera previewCamera;
        [Tooltip("표시 전용 차량을 배치할 중심입니다.")] public Transform pivot;
        private GameObject model;
        private float yaw = 140, pitch = 16, distance = 7;
        private Vector3 focus;
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
            Texture = new RenderTexture(1024, 576, 24) { name = "Vehicle Preview", antiAliasing = 1 };
            Texture.Create(); previewCamera.targetTexture = Texture;
        }
        public bool Show(VehicleEntry entry)
        {
            Clear();
            RenderedFrames = 0;
            if (entry == null || !entry.IsValid) return false;
            model = Instantiate(entry.previewPrefab, pivot);
            model.SetActive(true);
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Clear(); return false; }
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            focus = bounds.center; distance = Mathf.Max(5, bounds.size.magnitude * 1.35f);
            yaw = 140; pitch = 16; CurrentId = entry.id;
            previewCamera.enabled = true; Pose(); return true;
        }
        public void Orbit(Vector2 delta) { yaw += delta.x * .25f; pitch = Mathf.Clamp(pitch - delta.y * .15f, 5, 65); Pose(); }
        public void Zoom(float delta) { distance = Mathf.Clamp(distance - delta * .5f, 3.5f, 12); Pose(); }
        private void Pose()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            previewCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
        public void Clear()
        {
            if (model != null) { model.SetActive(false); Destroy(model); }
            model = null; CurrentId = null;
            if (previewCamera != null) previewCamera.enabled = false;
        }
        private void OnDestroy()
        {
            Clear();
            if (previewCamera != null) previewCamera.targetTexture = null;
            if (Texture != null) { Texture.Release(); Destroy(Texture); }
        }
    }

    // Raycast가 이 영역에 도달한 드래그/휠만 처리하므로 UI 버튼 조작과 충돌하지 않습니다.
    public sealed class PreviewPointer : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public VehiclePreview preview;
        public void OnDrag(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) preview.Orbit(data.delta); }
        public void OnScroll(PointerEventData data) => preview.Zoom(data.scrollDelta.y);
    }
}
