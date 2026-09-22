using UnityEngine;

namespace SteelDistrict.Vehicles
{
    [DisallowMultipleComponent, RequireComponent(typeof(VehicleSuspension))]
    public sealed class VehicleTireEffects : MonoBehaviour
    {
        [Tooltip("바퀴별 접지·미끄러짐 정보를 제공하는 컴포넌트입니다.")]
        [SerializeField] private VehicleSuspension suspension;
        [Tooltip("미끄러지는 바퀴의 접지점에서 생성할 Prometheus 파티클 프리팹입니다.")]
        [SerializeField] private ParticleSystem smokePrefab;
        [Tooltip("미끄러질 때 지면에 남길 Prometheus Trail Renderer 프리팹입니다.")]
        [SerializeField] private TrailRenderer skidPrefab;
        [Tooltip("미끄러짐이 강할 때 바퀴 하나에서 초당 방출할 파티클 수입니다. 약한 미끄러짐에서는 코드의 최소값 5와 이 값 사이를 보간합니다.")]
        [SerializeField, Min(0)] private float smokeRate = 35;
        [Tooltip("타이어와 지면의 상대 미끄러짐 속도가 이 값을 넘으면 연기와 자국을 켭니다. 차량의 전진 속도가 아닙니다.")]
        [SerializeField, Min(0.1f)] private float slipStartSpeed = 1.2f;
        [Tooltip("효과가 켜진 후 이 값 이하가 되면 끕니다. 시작값보다 작게 설정하면 경계에서 깜빡이는 현상을 줄입니다.")]
        [SerializeField, Min(0)] private float slipStopSpeed = 0.65f;
        private readonly ParticleSystem[] smoke = new ParticleSystem[4];
        private readonly TrailRenderer[] skids = new TrailRenderer[4];
        private readonly bool[] slipping = new bool[4];
        private readonly Vector3[] lastPosition = new Vector3[4];
        private Material skidMaterial;
        private bool initialized;
        public int EmittingWheelCount { get; private set; }
        private void Start() => Initialize();

        public void Initialize()
        {
            if (initialized) return;
            if (suspension == null) suspension = GetComponent<VehicleSuspension>();
            if (smokePrefab == null || skidPrefab == null)
            {
                Debug.LogError("Assign the Prometheus tire smoke and skid prefabs.", this);
                enabled = false;
                return;
            }
            // Keep the original trail geometry, width and lifetime, with an HDRP material.
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader != null)
            {
                skidMaterial = new Material(shader) { name = "Tire skid (HDRP runtime)" };
                skidMaterial.SetColor("_UnlitColor", new Color(0.025f, 0.025f, 0.025f, 1));
                // 지면에 눕힌 Trail은 진행 방향에 따라 삼각형 앞/뒷면이 달라질 수 있습니다.
                // HDRP/Unlit 기본 Back 컬링을 끄고 위쪽 카메라에서도 자국을 표시합니다.
                skidMaterial.SetFloat("_DoubleSidedEnable", 1f);
                skidMaterial.SetFloat("_CullMode", (float)UnityEngine.Rendering.CullMode.Off);
            }
            for (int i = 0; i < 4; i++)
            {
                smoke[i] = Instantiate(smokePrefab, transform);
                smoke[i].name = "Tire smoke " + i;
                var main = smoke[i].main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = smoke[i].emission;
                emission.rateOverTime = 0;
                smoke[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                skids[i] = Instantiate(skidPrefab, transform);
                skids[i].name = "Tire skid " + i;
                skids[i].emitting = false;
                skids[i].Clear();
                skids[i].alignment = LineAlignment.TransformZ;
                if (skidMaterial != null) skids[i].sharedMaterial = skidMaterial;
            }
            initialized = true;
        }

        private void LateUpdate() => UpdateEffects();
        public void UpdateEffects()
        {
            if (!initialized || !suspension.IsReady) return;
            EmittingWheelCount = 0;
            for (int i = 0; i < 4; i++)
            {
                var wheel = suspension.Contacts[i];
                bool active = isActiveAndEnabled && suspension.isActiveAndEnabled &&
                    wheel.Grounded && wheel.Load > 1 && wheel.SlipSpeed > (slipping[i] ? slipStopSpeed : slipStartSpeed);
                Vector3 point = wheel.Point + wheel.Normal * 0.018f;
                bool discontinuity = (point - lastPosition[i]).sqrMagnitude > 9;
                if (discontinuity) skids[i].Clear();
                if (!slipping[i] || discontinuity) skids[i].emitting = false;
                if (wheel.Grounded)
                {
                    Quaternion rotation = Quaternion.LookRotation(wheel.Normal, transform.forward);
                    skids[i].transform.SetPositionAndRotation(point, rotation);
                    smoke[i].transform.SetPositionAndRotation(point, Quaternion.identity);
                    lastPosition[i] = point;
                }
                skids[i].emitting = active;
                var emission = smoke[i].emission;
                emission.rateOverTime = active ? Mathf.Lerp(5, smokeRate, wheel.SlipAmount) : 0;
                if (active)
                {
                    EmittingWheelCount++;
                    if (!smoke[i].isPlaying) smoke[i].Play();
                }
                else if (smoke[i].isPlaying) smoke[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                slipping[i] = active;
            }
        }

        private void OnDisable()
        {
            EmittingWheelCount = 0;
            for (int i = 0; i < 4; i++)
            {
                slipping[i] = false;
                if (smoke[i] != null) smoke[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (skids[i] != null) { skids[i].emitting = false; skids[i].Clear(); }
            }
        }
        private void OnDestroy()
        {
            if (skidMaterial != null)
            {
                if (Application.isPlaying) Destroy(skidMaterial);
                else DestroyImmediate(skidMaterial);
            }
        }
    }
}
