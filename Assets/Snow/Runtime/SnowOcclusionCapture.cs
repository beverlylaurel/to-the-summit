// ROL: gökyüzü görünürlüğü haritasını üretir. Ortografik bir kamera SnowOccluder
// katmanını tepeden çizer, her teksele o noktanın üstündeki en yüksek engelin dünya
// Y'si yazılır. Üç tüketicisi var: zemin birikmesi, nesne üstü kar, kar tanesi kesme.
// Çağıran: SnowManager (bölge merkezi kayınca), SnowRendererFeature (çizim geçişi).

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public class SnowOcclusionCapture : MonoBehaviour
{
    /// Engel katmanının adı (§4.1). Karakterler ve hareketli nesneler bu katmanda
    /// OLMAYACAK — yoksa oyuncunun altına kar yağmaz.
    public const string OccluderLayerName = "SnowOccluder";

    /// Engel yokken dokuda duran değer. Örnekleme bunu "çok aşağıda" okur ve
    /// görünürlüğü 1 yapar.
    const float ClearHeight = -9999f;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;
    [SerializeField] Shader occlusionShader;

    Camera captureCamera;
    RenderTexture occlusion;
    Material overrideMaterial;

    Vector2 lastCaptureCenter;
    bool hasCaptured;
    bool dirty = true;

    /// Kar yönü. Rüzgârda eğilebilir (§8.4); şimdilik dik.
    Vector3 upDirection = Vector3.up;

    /// Teşhis: kaç kez yenilendi. §4.2'nin "her frame değil" kuralının kanıtı.
    public int CaptureCount { get; private set; }

    public RenderTexture OcclusionTexture => occlusion;
    public Vector2 LastCaptureCenter => lastCaptureCenter;
    public bool HasCaptured => hasCaptured;
    public Camera CaptureCamera => captureCamera;

    public Vector3 UpDirection
    {
        get => upDirection;
        set
        {
            upDirection = value.sqrMagnitude > 1e-6f ? value.normalized : Vector3.up;
            dirty = true;
        }
    }

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException("SnowOcclusionCapture: SnowSettings atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (occlusionShader == null)
            throw new System.InvalidOperationException("SnowOcclusionCapture: Hidden/Snow/OcclusionDepth atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        int layer = LayerMask.NameToLayer(OccluderLayerName);
        if (layer < 0)
            throw new System.InvalidOperationException(
                "SnowOcclusionCapture: '" + OccluderLayerName + "' katmanı yok. Kar Teşhisi > Sahneyi kur çalıştır.");

        overrideMaterial = CoreUtils.CreateEngineMaterial(occlusionShader);

        int resolution = settings.QualityData.OcclusionResolution;

        // DERİNLİK TAMPONU GEREKLİ: en yüksek engelin kazanması derinlik testiyle
        // oluyor, ayrı bir karşılaştırma yok.
        occlusion = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.RHalf)
        {
            name = "RT_Occlusion",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
        };
        occlusion.Create();

        CreateCamera(layer);

        dirty = true;
        hasCaptured = false;
        CaptureCount = 0;

        WriteGlobals();
    }

    void OnDisable()
    {
        if (captureCamera != null)
        {
            captureCamera.targetTexture = null;
            DestroyImmediate(captureCamera.gameObject);
            captureCamera = null;
        }

        if (occlusion != null)
        {
            occlusion.Release();
            DestroyImmediate(occlusion);
            occlusion = null;
        }

        CoreUtils.Destroy(overrideMaterial);
        overrideMaterial = null;
    }

    void CreateCamera(int occluderLayer)
    {
        var go = new GameObject("SnowOcclusionCamera") { hideFlags = HideFlags.HideAndDontSave };
        captureCamera = go.AddComponent<Camera>();

        // ELLE RENDER: enabled = false, her karede değil yalnız gerektiğinde Render().
        captureCamera.enabled = false;
        captureCamera.orthographic = true;
        captureCamera.orthographicSize = SnowConstants.OcclusionArea * 0.5f;
        captureCamera.cullingMask = 1 << occluderLayer;
        captureCamera.targetTexture = occlusion;
        captureCamera.useOcclusionCulling = false;
        captureCamera.allowMSAA = false;
        captureCamera.allowHDR = false;

        // Kesme düzlemleri takip hedefine GÖRE. Spec 400 m yükseklik / 800 m derinlik
        // veriyor; 6 km'lik bir dağda bunlar sahnenin mutlak uçlarına değil oyuncunun
        // etrafındaki dilime karşılık geliyor — engel olabilecek her şey o dilimde.
        captureCamera.nearClipPlane = 0.3f;
        captureCamera.farClipPlane = settings.OcclusionCameraDepth;

        // Renk temizliği BİZİM geçişimizde yapılıyor: Unity arka plan rengini aktif
        // renk uzayına çeviriyor ve negatif bileşende bu NaN üretir.
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = Color.black;

        UniversalAdditionalCameraData cameraData = captureCamera.GetUniversalAdditionalCameraData();

        // KENDİ RENDERER'I. Projenin ana renderer'ındaki gökyüzü/bulut/sis geçişleri
        // bu kameranın tek kanallı hedefinde çalışmıyor ve render graph'i çökertiyor.
        if (settings.OcclusionRendererIndex < 0)
            throw new System.InvalidOperationException(
                "SnowOcclusionCapture: engel renderer'sı kurulmamış. " +
                "To The Summit > Kar > Kar Teşhisi > Sahneyi kur çalıştır.");

        cameraData.SetRenderer(settings.OcclusionRendererIndex);
        cameraData.renderShadows = false;
        cameraData.requiresColorOption = CameraOverrideOption.Off;
        cameraData.requiresDepthOption = CameraOverrideOption.Off;
        cameraData.renderPostProcessing = false;
        cameraData.antialiasing = AntialiasingMode.None;
    }

    /// Yıkılabilir/açılıp kapanan nesneler (kapı, köprü) bunu çağırır.
    public void MarkDirty() => dirty = true;

    /// SnowManager her karede çağırır. §4.2: yalnız merkez 4 m'den fazla kaydığında
    /// veya elle kirletildiğinde yenilenir.
    public void UpdateCapture(Vector3 center)
    {
        if (captureCamera == null) return;

        Vector2 centerXZ = new Vector2(center.x, center.z);

        if (hasCaptured && !dirty &&
            (centerXZ - lastCaptureCenter).sqrMagnitude <
            SnowConstants.OcclusionMoveThreshold * SnowConstants.OcclusionMoveThreshold)
            return;

        captureCamera.transform.SetPositionAndRotation(
            center + upDirection * settings.OcclusionCameraHeight,
            Quaternion.LookRotation(-upDirection, Vector3.forward));

        lastCaptureCenter = centerXZ;
        dirty = false;
        hasCaptured = true;
        CaptureCount++;

        WriteGlobals();

        captureCamera.Render();
    }

    void WriteGlobals()
    {
        Shader.SetGlobalTexture(SnowShaderIDs.SnowOcclusionTex, occlusion);
        Shader.SetGlobalVector(SnowShaderIDs.OcclCenterXZ,
            new Vector4(lastCaptureCenter.x, lastCaptureCenter.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.OcclAreaSize, SnowConstants.OcclusionArea);
        Shader.SetGlobalFloat(SnowShaderIDs.OcclResolution, settings.QualityData.OcclusionResolution);
        Shader.SetGlobalVector(SnowShaderIDs.SnowUpDirection, upDirection);
    }

    // ------------------------------------------------------------------ çizim geçişi

    /// Yakalama kamerası şu an mı çiziliyor.
    public static bool IsCaptureCamera(Camera camera)
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null) return false;

        SnowOcclusionCapture capture = manager.Occlusion;
        return capture != null && capture.captureCamera == camera;
    }

    public static Material ActiveOverrideMaterial =>
        SnowManager.Active != null && SnowManager.Active.Occlusion != null
            ? SnowManager.Active.Occlusion.overrideMaterial
            : null;

    /// Engelleri override materyalle çizen geçiş.
    ///
    // ASSUMPTION: §4.1 `SetReplacementShader` diyor ama o API SRP'de desteklenmiyor
    // ve §14 URP'yi zorunlu kılıyor. Aynı işi URP'nin kendi yolu yapıyor: renderer
    // listesi + override materyal. Shader ve çıktı birebir aynı, yalnız bağlanma
    // şekli değişti.
    public class Pass : ScriptableRenderPass
    {
        class PassData
        {
            public RendererListHandle List;
        }

        static readonly ShaderTagId[] ShaderTags =
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
        };

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            Material material = ActiveOverrideMaterial;
            if (material == null) return;

            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Kar Engel Haritası", out PassData passData);

            var sortingCriteria = SortingCriteria.CommonOpaque;
            var drawingSettings = RenderingUtils.CreateDrawingSettings(
                new System.Collections.Generic.List<ShaderTagId>(ShaderTags),
                renderingData, cameraData, lightData, sortingCriteria);

            drawingSettings.overrideMaterial = material;
            drawingSettings.overrideMaterialPassIndex = 0;

            var filteringSettings = new FilteringSettings(RenderQueueRange.all, cameraData.camera.cullingMask);

            passData.List = renderGraph.CreateRendererList(
                new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings));

            builder.UseRendererList(passData.List);
            builder.SetRenderAttachment(resources.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resources.activeDepthTexture);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                // TEMİZLİK BURADA. Kameranın arka plan rengi kullanılamıyor: Unity onu
                // aktif renk uzayına çeviriyor ve negatif bileşen NaN veriyor.
                context.cmd.ClearRenderTarget(RTClearFlags.Color,
                    new Color(ClearHeight, ClearHeight, ClearHeight, 1f), 1f, 0);

                context.cmd.DrawRendererList(data.List);
            });
        }
    }
}
