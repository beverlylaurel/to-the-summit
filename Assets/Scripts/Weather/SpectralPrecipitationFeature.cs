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

    [Tooltip("Döşeme boyu (piksel). AÇISAL ÇÖZÜNÜRLÜK: her döşemenin kendi akış yönü ve " +
             "hızı var, küçük olursa genleşme odağı çevresinde yön daha yumuşak değişir. " +
             "Ekran genişliğinin sekizde biri civarı iyi.")]
    [SerializeField] float tileSize = 240f;

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
            tile = tileSize,
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
        pass.tile = tileSize;
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
        static readonly int FoeId = Shader.PropertyToID("_CurtainFoe");

        readonly Material material;

        public float near, tile, flow, patternScale;

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

            // GENLEŞME ODAĞI (FOE). Kamera ilerlerken görüntü hareketi bir noktadan
            // dışa açılıyor; o nokta hareket yönünün ekrana izdüşümü `[Langer 2004, §7]`.
            // Perdenin parallax'ı buradan geliyor — döşeme başına yön ve hız bununla
            // hesaplanıyor.
            //
            // Kamera durgunken odak, yağışın KENDİ yönünün izdüşümü oluyor: kar dururken
            // de düşüyor ve akış ekseni o zaman rüzgârın ekseni.
            var camera = cameraData.camera;

            float width = cameraData.cameraTargetDescriptor.width;
            float height = cameraData.cameraTargetDescriptor.height;

            Vector3 position = camera.transform.position;
            Vector3 motion = hasPrevious ? position - previousPosition : Vector3.zero;
            previousPosition = position;
            hasPrevious = true;

            // Kameranın hareketi mi yağışın hareketi mi baskın: ikisi de görüntü akışını
            // sürüyor ve odağın yeri bileşkelerinden çıkıyor.
            Vector3 drift = SpectralPrecipitationState.Velocity * Time.deltaTime - motion;

            Vector2 foe = new Vector2(width * 0.5f, height * 0.5f);

            if (drift.sqrMagnitude > 1e-10f)
            {
                // Akışın GELDİĞİ yön odaktır: taneler oradan gelip dışa açılıyor.
                Vector3 source = position - drift.normalized * 1000f;
                Vector3 projected = camera.WorldToScreenPoint(source);

                // İzdüşüm kameranın gerisindeyse odak ekranın dışında ve işaret ters;
                // merkezde bırakmak ters akıştan iyi.
                if (projected.z > 0f) foe = new Vector2(projected.x, projected.y);
            }

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "Spektral Yağış Perdesi", out var passData);

            passData.material = material;

            builder.SetRenderAttachment(resources.activeColorTexture, 0);
            builder.UseTexture(resources.cameraDepthTexture);
            builder.AllowPassCulling(false);

            material.SetTexture(PatternId, texture);
            material.SetVector(ParamsId, new Vector4(
                tile, patternScale, intensity, SpectralPrecipitationState.Time));
            material.SetVector(DepthId, new Vector4(
                near, flow, SpectralPrecipitationState.Snowiness, 0f));
            material.SetVector(FoeId, new Vector4(foe.x, foe.y, width, height));

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
