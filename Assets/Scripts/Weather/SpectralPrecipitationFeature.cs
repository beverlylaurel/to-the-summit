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

    [Tooltip("Perdenin dolma mesafesi (metre). Bu kadar yolda perdenin %63'ü birikir; " +
             "üstel yasa, doğrusal rampa değil. Küçük olursa yakın yamaç da perde alır " +
             "ve desen araziye yapışmış gibi görünür.")]
    [SerializeField] float fillDistance = 1400f;

    [Tooltip("Desenin ekrandaki döşeme boyu (piksel). PİŞMİŞ DOKU BOYUNA EŞİT OLMALI " +
             "(512): halka döşeme birimi cinsinden tanımlı, oran bozulunca özellik boyu " +
             "kayıyor ve desen kar değil mermer gibi görünüyor.")]
    [SerializeField] float tileSize = 512f;

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
            far = fillDistance,
            tile = tileSize,
            flow = flowSpeed,
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (pass == null || material == null) return;
        if (data.cameraData.cameraType == CameraType.Reflection) return;
        if (data.cameraData.cameraType == CameraType.Preview) return;

        pass.near = nearCutoff;
        pass.far = fillDistance;
        pass.tile = tileSize;
        pass.flow = flowSpeed;

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

        public float near, far, tile, flow;

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

            // YAĞIŞIN EKRAN YÖNÜ. Perdenin akış ekseni bu — düşüş artı rüzgâr,
            // kameranın görüşüne izdüşürülmüş.
            //
            // Genleşme odağı (makale §7) BİLİNÇLİ OLARAK ALINMADI: yön piksel başına
            // değişince desen dönmüyor, koordinat alanı buruluyor ve odağın çevresinde
            // ışınsal bir girdap kalıyor (ölçüldü, sahne görünümünde net). Makale bunu
            // döşemeyle çözüyor — döşeme içinde yön sabit, kenarlarda harmanlama. O
            // makine kameranın ÖTELENMESİNİ modelliyor ve tırmanışçı yavaş hareket
            // ediyor; tek yön hem doğru hem ucuz.
            var camera = cameraData.camera;

            float width = cameraData.cameraTargetDescriptor.width;
            float height = cameraData.cameraTargetDescriptor.height;

            Vector3 velocity = SpectralPrecipitationState.Velocity;
            Vector3 viewVelocity = camera.worldToCameraMatrix.MultiplyVector(velocity);

            Vector2 screenDir = new Vector2(viewVelocity.x, viewVelocity.y);
            screenDir = screenDir.sqrMagnitude > 1e-8f
                ? screenDir.normalized
                : new Vector2(0f, -1f);

            using var builder = renderGraph.AddRasterRenderPass<PassData>(
                "Spektral Yağış Perdesi", out var passData);

            passData.material = material;

            builder.SetRenderAttachment(resources.activeColorTexture, 0);
            builder.UseTexture(resources.cameraDepthTexture);
            builder.AllowPassCulling(false);

            material.SetTexture(PatternId, texture);
            material.SetVector(ParamsId, new Vector4(
                tile, flow, intensity, SpectralPrecipitationState.Time));
            material.SetVector(DepthId, new Vector4(
                near,
                // Yatay görüş açısı (radyan): desenin açısal ölçeği buradan, ekranda
                // istenen döşeme boyu korunsun diye.
                camera.fieldOfView * Mathf.Deg2Rad * camera.aspect,
                SpectralPrecipitationState.Snowiness,
                SpectralPrecipitationState.Visibility));
            material.SetVector(FoeId, new Vector4(screenDir.x, screenDir.y, width, height));

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

    /// Görüş mesafesi (metre). Perde sisin opaklığını buradan türetiyor — ışın boyunca
    /// integral almak yerine tek üstel, çünkü tam ekranda sekiz örnek 3.5 ms tutuyordu.
    public static float Visibility = 10000f;

    /// Yağışın DÜNYA hızı (düşüş + rüzgâr). Perde akış eksenini buradan alıyor;
    /// taneler de aynı hızla düşüyor, iki katman ayrışmasın diye.
    public static Vector3 Velocity;

    /// Döngü fazı. Kare süresiyle ilerliyor; `Time.time` doğrudan kullanılmıyor çünkü
    /// oyun hızı çarpanı (test panelinde var) perdeyi de yavaşlatmalı.
    public static float Time;
}
