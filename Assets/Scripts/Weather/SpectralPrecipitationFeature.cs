using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// SPEKTRAL YAĞIŞ PERDESİ — geçişi boru hattına takar.
///
/// `PrecipitationRenderer` taneleri çiziyor; bu geçiş aralarını dolduran dinamik dokuyu
/// çiziyor (`[Langer 2004]`, `snow-spec.md` §7). İkisi çakışmıyor: particle yakını, perde
/// uzağı taşıyor ve perde `yakın kesme` mesafesinden önce hiç görünmüyor.
///
/// SAYDAMLARDAN SONRA. Perde havadaki yağışı temsil ediyor, yani sahnedeki her şeyin
/// önünde — ama derinlik kapısı sayesinde yakın nesnelerin önüne geçmiyor. Saydamlardan
/// önce çizilseydi tanelerin arkasında kalırdı ve iki katman ayrışırdı.
public class SpectralPrecipitationFeature : ScriptableRendererFeature
{
    [SerializeField] Shader curtainShader;

    [Tooltip("Perdenin görünmeye başladığı mesafe (metre). Bundan yakını taneler taşıyor; " +
             "tırmanış duvarı ve el hep bu mesafenin berisinde kalıyor.")]
    [SerializeField] float nearCutoff = 40f;

    [Tooltip("Desen ölçeği (piksel). ÖZELLİK BOYU: pişmiş doku boyuna eşit olmalı (512), " +
             "yoksa halka ölçeği kayıyor ve desen kar değil mermer gibi görünüyor. " +
             "Döşeme boyundan AYRI tutuluyor çünkü ikisi çelişen şeyler istiyor.")]
    [SerializeField] float patternScale = 512f;

    [Tooltip("Kameranın hareketinin akışa katkısı.")]
    [SerializeField] float flowSpeed = 1.4f;

    CurtainPass pass;
    Material material;

    public override void Create()
    {
        if (curtainShader == null) return;

        material = CoreUtils.CreateEngineMaterial(curtainShader);

        pass = new CurtainPass(material)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
            near = nearCutoff,
            flow = flowSpeed,
            patternScale = patternScale,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (pass == null || material == null) return;
        if (data.cameraData.cameraType == CameraType.Reflection) return;
        if (data.cameraData.cameraType == CameraType.Preview) return;

        pass.near = nearCutoff;
        pass.flow = flowSpeed;
        pass.patternScale = patternScale;

        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(material);
        material = null;
    }

    class CurtainPass : ScriptableRenderPass
    {
        static readonly int PatternId = Shader.PropertyToID("_CurtainPattern");
        static readonly int ParamsId = Shader.PropertyToID("_CurtainParams");
        static readonly int DepthId = Shader.PropertyToID("_CurtainDepth");
        static readonly int FlowId = Shader.PropertyToID("_CurtainFlow");

        readonly Material material;

        public float near, flow, patternScale;

        /// Kameranın önceki konumu — odak, kameranın ve yağışın bileşke akışından çıkıyor.
        Vector3 previousPosition;
        bool hasPrevious;

        public CurtainPass(Material curtainMaterial) => material = curtainMaterial;

        class PassData { public Material material; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            var texture = SpectralPrecipitationState.Pattern;
            if (texture == null) return;

            float intensity = SpectralPrecipitationState.Intensity;
            if (intensity <= 1e-4f) return;

            // TEK AKIŞ YÖNÜ, EKRAN GENELİNDE.
            //
            // Genleşme odağı ve döşeme başına θ SÖKÜLDÜ. Gerekçe shader'da uzun uzun
            // yazılı; özeti: yöntem θ'nın zamanla değişmesine uygun değil (makale θ'yı
            // faza artımlı işliyor, biz pişmiş dokuyu döndürüyoruz) ve makalenin kendisi
            // de θ'yı zamanla değiştirmiyor (`§7.2`).
            //
            // Kalan tek büyüklük: yağışın ekrandaki akış yönü. Kameranın hareketi de
            // yağışın hareketi de bunu sürüyor, bileşkeleri alınıyor.
            var camera = cameraData.camera;

            float width = cameraData.cameraTargetDescriptor.width;
            float height = cameraData.cameraTargetDescriptor.height;

            Vector3 position = camera.transform.position;
            Vector3 motion = hasPrevious ? position - previousPosition : Vector3.zero;
            previousPosition = position;
            hasPrevious = true;

            Vector3 drift = SpectralPrecipitationState.Velocity * Time.deltaTime - motion;

            // Varsayılan aşağı: yağış her zaman düşüyor, rüzgâr sıfırsa bile.
            Vector2 flowDir = new Vector2(0f, -1f);

            if (drift.sqrMagnitude > 1e-10f)
            {
                // Kamera uzayında x sağa, y yukarı — ekranla aynı. Görüş eksenindeki
                // bileşen atılıyor: ekranda akışın yönü yalnız görüntü düzlemine düşen
                // paydan çıkıyor.
                Vector3 dirVS = camera.worldToCameraMatrix.MultiplyVector(drift.normalized);
                Vector2 image = new Vector2(dirVS.x, dirVS.y);
                if (image.sqrMagnitude > 1e-12f) flowDir = image.normalized;
            }

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "Spektral Yağış Perdesi", out var passData);

            passData.material = material;

            builder.SetRenderAttachment(resources.activeColorTexture, 0);
            builder.UseTexture(resources.cameraDepthTexture);
            builder.AllowPassCulling(false);

            material.SetTexture(PatternId, texture);
            material.SetVector(ParamsId, new Vector4(
                width, height, intensity, SpectralPrecipitationState.Time));
            material.SetVector(FlowId, new Vector4(
                flowDir.x, flowDir.y, flow, patternScale));
            material.SetVector(DepthId, new Vector4(
                near, SpectralPrecipitationState.Snowiness, 0f, 0f));

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, new Vector4(1f, 1f, 0f, 0f), data.material, 0);
            });
        }
    }
}

/// PERDENİN DURUMU. Feature bir `ScriptableRendererFeature` ve sahnedeki bileşenlere
/// doğrudan bağlanamıyor (renderer asset'i sahneden bağımsız). Yağış tarafı buraya
/// yazıyor, geçiş buradan okuyor — tek yön, tek yer.
public static class SpectralPrecipitationState
{
    public static Texture3D Pattern;

    /// 0-1. Yağış şiddeti × yerel bulut payı.
    public static float Intensity;

    /// 0 yağmur, 1 kar.
    public static float Snowiness;

    /// Yağışın DÜNYA hızı (düşüş + rüzgâr). Perde akış eksenini buradan alıyor;
    /// taneler de aynı hızla düşüyor, iki katman ayrışmasın diye.
    public static Vector3 Velocity;

    /// Döngü fazı. Kare süresiyle ilerliyor; `Time.time` doğrudan kullanılmıyor çünkü
    /// oyun hızı çarpanı (test panelinde var) perdeyi de yavaşlatmalı.
    public static float Time;
}
