// ROL: kamera lensine yapışan karın render geçişi (§10.2, opsiyonel).
// Şiddet yağıştan ve kameranın rüzgâra dönük olmasından türüyor: rüzgâra dönünce
// lens doluyor, sırtını dönünce temizleniyor.
// Çağıran: URP renderer asset'i.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class SnowLensFeature : ScriptableRendererFeature
{
    /// Sahnedeki sürücü. Renderer bir asset; sahneye başka köprü yok.
    public static SnowWeather ActiveWeather;

    [SerializeField] Shader lensShader;

    [Tooltip("Lensin dolma hızı, 1/saniye.")]
    [SerializeField] float fillRate = 0.35f;

    [Tooltip("Lensin temizlenme hızı, 1/saniye.")]
    [SerializeField] float clearRate = 0.5f;

    [Tooltip("Ekrandaki leke hücresi yoğunluğu.")]
    [SerializeField] float cellDensity = 14f;

    LensPass pass;
    Material material;

    public override void Create()
    {
        if (lensShader == null) return;

        material = CoreUtils.CreateEngineMaterial(lensShader);

        pass = new LensPass(material, fillRate, clearRate, cellDensity)
        {
            // POST-PROCESS'TEN SONRA. Lens kamerada, sahnede değil: tonemap ve
            // renk düzenlemesi ona uygulanmamalı.
            renderPassEvent = RenderPassEvent.AfterRendering
        };
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || ActiveWeather == null) return;
        if (renderingData.cameraData.cameraType != CameraType.Game) return;

        renderer.EnqueuePass(pass);
    }

    class LensPass : ScriptableRenderPass
    {
        class PassData
        {
            public Material Material;
            public TextureHandle Source;
        }

        readonly Material material;
        readonly float fillRate;
        readonly float clearRate;
        readonly float cellDensity;

        float amount;

        public LensPass(Material lensMaterial, float fill, float clear, float density)
        {
            material = lensMaterial;
            fillRate = fill;
            clearRate = clear;
            cellDensity = density;

            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            UpdateAmount(cameraData.camera);
            if (amount <= 0.001f) return;

            material.SetFloat(SnowShaderIDs.LensSnowAmount, amount);
            material.SetFloat(SnowShaderIDs.LensTime, Time.time);
            material.SetFloat(SnowShaderIDs.LensCellDensity, cellDensity);

            TextureHandle source = resources.activeColorTexture;

            var descriptor = renderGraph.GetTextureDesc(source);
            descriptor.name = "Snow Lens";
            descriptor.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(descriptor);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Kar Lensi", out PassData passData))
            {
                passData.Material = material;
                passData.Source = source;

                builder.UseTexture(source);
                builder.SetRenderAttachment(destination, 0);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    Blitter.BlitTexture(context.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f), data.Material, 0));
            }

            resources.cameraColor = destination;
        }

        /// RÜZGÂRA DÖNÜNCE LENS DOLUYOR. Sırtını dönünce boşalıyor; ikisi ayrı hızda,
        /// çünkü kar yapışması birikme, temizlenmesi erime.
        void UpdateAmount(Camera camera)
        {
            SnowWeather weather = ActiveWeather;

            Vector3 wind = weather.WindWS;
            float facing = wind.sqrMagnitude > 1e-6f
                ? Mathf.Clamp01(Vector3.Dot(camera.transform.forward, -wind.normalized))
                : 0f;

            float target = Mathf.Clamp01(weather.Coverage * facing);

            float rate = target > amount ? fillRate : clearRate;
            amount = Mathf.MoveTowards(amount, target, rate * Time.deltaTime);
        }
    }
}
