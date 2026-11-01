using PinchOfTheft.CCTV;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public sealed class CCTVAnalogRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class EffectSettings
    {
        [Range(0, 1)] public float grain = 0.24f;
        [Range(0, 1)] public float scanlines = 0.28f;
        [Range(0, 1)] public float glitch = 0.18f;
        [Range(0, 1)] public float blur = 0.22f;
        [Range(0, 1)] public float desaturation = 0.42f;
        [Range(0, 1)] public float vignette = 0.22f;
        [Range(0, 1)] public float chromatic = 0.3f;
        [Range(0, 1)] public float flicker = 0.3f;
    }

    [SerializeField] private EffectSettings settings = new EffectSettings();
    private Material material;
    private AnalogPass pass;

    public override void Create()
    {
        var shader = Shader.Find("Hidden/PinchOfTheft/CCTVAnalog");
        if (shader == null) return;
        if (material != null) CoreUtils.Destroy(material);
        material = CoreUtils.CreateEngineMaterial(shader);
        pass = new AnalogPass(material, settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass != null && renderingData.cameraData.cameraType == CameraType.Game &&
            renderingData.cameraData.camera.GetComponent<CCTVCamera>() != null)
            renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
    }

    private sealed class AnalogPass : ScriptableRenderPass
    {
        private readonly Material material;
        private readonly EffectSettings settings;
        public AnalogPass(Material material, EffectSettings settings)
        {
            this.material = material;
            this.settings = settings;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid()) return;
            var descriptor = graph.GetTextureDesc(resources.activeColorTexture);
            descriptor.name = "CCTV Analog Result";
            descriptor.clearBuffer = false;
            var destination = graph.CreateTexture(descriptor);
            var camera = cameraData.camera;
            material.SetVector("_CCTVParamsA", new Vector4(settings.grain, settings.scanlines, settings.glitch, settings.blur));
            material.SetVector("_CCTVParamsB", new Vector4(settings.desaturation, settings.vignette, settings.chromatic, settings.flicker));
            material.SetVector("_CCTVInfo", new Vector4(Time.unscaledTime, camera.pixelWidth, camera.pixelHeight, Time.frameCount));
            graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(resources.activeColorTexture, destination, material, 0), "CCTV Analog Surveillance");
            resources.cameraColor = destination;
        }
    }
}
