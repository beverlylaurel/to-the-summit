// ROL: kar sisteminin sahibi — durum dokularını yaratır, bölgeyi takip eder,
// çevre değerlerini global uniform olarak yayınlar, compute işini kuyruğa yazar.
// Çağıran: SnowRenderPass (iş kuyruğu), SnowDebugWindow (okuma).

using UnityEngine;
using UnityEngine.Rendering;

/// KAYNAK BULAMAZSA DEVRE DIŞI KALIR, VARSAYILAN UYDURMAZ (spec §3.2).
///
/// Bu sınıf mevcut sistemlere hiçbir şey YAZMAZ. `RenderSettings`,
/// `VolumeProfile` ve `Light.intensity`'ye dokunan tek satır yok; dışarıya
/// bildirdiği her şey `SnowRuntimeState`'te (spec §3.3).
[DisallowMultipleComponent]
public class SnowManager : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;

    [Tooltip("Oyunun hava/rüzgâr/gündöngü sistemlerini kar sistemine bağlayan köprü.")]
    [SerializeField] MonoBehaviour environmentSource;

    [Tooltip("Bölgenin merkezinde duracak hedef — genelde oyuncu.")]
    [SerializeField] Transform followTarget;

    [SerializeField] SnowGroundHeight groundHeight;
    [SerializeField] ComputeShader simCompute;

    /// Sahnede tek bir yönetici var; teşhis penceresi ve geçişler bunu okur.
    /// `FindObjectOfType` yerine: arama gizli bağımlılık yaratır, bu ise açık.
    public static SnowManager Active { get; private set; }

    ISnowEnvironmentSource env;

    RenderTexture capture, captureBlur;
    RenderTexture trail, trailTemp;
    RenderTexture snow, snowTemp;
    RenderTexture skyVis;
    RenderTexture windShadow;

    int clearKernel = -1;
    int scrollKernel = -1;

    Vector2Int centerTexel;
    bool pendingClear;
    bool pendingScroll;
    Vector2Int pendingScrollTexels;

    public bool IsReady { get; private set; }

    public SnowSettings Settings => settings;
    public ISnowEnvironmentSource Environment => env;
    public SnowGroundHeight GroundHeight => groundHeight;

    public RenderTexture CaptureTexture => capture;
    public RenderTexture CaptureBlurTexture => captureBlur;
    public RenderTexture TrailTexture => trail;
    public RenderTexture SnowTexture => snow;
    public RenderTexture SkyVisTexture => skyVis;
    public RenderTexture WindShadowTexture => windShadow;

    /// Bölgenin dünya XZ merkezi, snap'lenmiş.
    public Vector2 AreaCenter => new(centerTexel.x * TexelSize, centerTexel.y * TexelSize);

    public float TexelSize => settings.QualityData.AreaSize / settings.QualityData.Resolution;

    /// Teşhis: son kaydırmanın teksel cinsinden miktarı.
    public Vector2Int LastScrollTexels { get; private set; }

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: {nameof(settings)} atanmadı.");
        if (simCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: SnowSim.compute atanmadı.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: takip hedefi atanmadı.");
        if (groundHeight == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: {nameof(groundHeight)} atanmadı.");

        env = environmentSource as ISnowEnvironmentSource;

        if (env == null)
        {
            // Spec §3.2: kaynak yoksa hata basıp devre dışı kal. Kendi varsayılanını
            // uydurmak, kar sisteminin sessizce mevcut sistemlerden kopması demek.
            Debug.LogError($"{nameof(SnowManager)}: {nameof(ISnowEnvironmentSource)} bulunamadı. " +
                           "Kar sistemi devre dışı.");
            enabled = false;
            return;
        }

        SnowQualityData q = settings.QualityData;

        capture = Create("RT_Capture", q.Resolution, RenderTextureFormat.ARGBHalf);
        captureBlur = Create("RT_CaptureBlur", q.Resolution, RenderTextureFormat.ARGBHalf);

        // RT_Trail ARGBHalf: B (kabuk) ve A (sastrugi) Faz 11–12'de doluyor ama doku
        // BAŞTAN dört kanallı açılıyor. Sonradan format değiştirmek bütün kernel'leri
        // ve shader'ları etkiliyor (spec §6.2).
        trail = Create("RT_Trail", q.Resolution, RenderTextureFormat.ARGBHalf);
        trailTemp = Create("RT_TrailTemp", q.Resolution, RenderTextureFormat.ARGBHalf);

        snow = Create("RT_Snow", q.Resolution, RenderTextureFormat.ARGBHalf);

        // ASSUMPTION: RT_SnowTemp spec §6.2 tablosunda yok ama KScroll komşu teksel
        // okuyor ve §20 "komşu okuyan pass'lerde ping-pong" diyor — RT_Snow'un da
        // kendi tamponuna ihtiyacı var. Başka bir dokuyu ödünç almak (örn.
        // RT_CaptureBlur) format aynı olsa bile o dokunun içeriğini siler.
        // Bedeli 8 MB.
        snowTemp = Create("RT_SnowTemp", q.Resolution, RenderTextureFormat.ARGBHalf);
        skyVis = Create("RT_SkyVis", q.SkyResolution, RenderTextureFormat.RHalf);

        // ASSUMPTION: RT_WindShadow RGHalf. Spec §6.2 tablosu RGHalf diyor (R=Wz,
        // G=Wsz), §18.0 metni RHalf diyor. `KWindShadow` iki kanal birden yazıyor
        // (`_WindShadowZOut`, `_WindShadowSzOut`); tek kanal yetmez, tablo kazandı.
        windShadow = Create("RT_WindShadow", q.SkyResolution, RenderTextureFormat.RGHalf);

        clearKernel = simCompute.FindKernel("KClear");
        scrollKernel = simCompute.FindKernel("KScroll");

        centerTexel = SnapToTexelGrid(followTarget.position, TexelSize);
        pendingClear = true;
        pendingScroll = false;

        SnowRuntimeState.Reset();

        Active = this;
        IsReady = true;

        WriteGlobals();
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        IsReady = false;

        SnowRuntimeState.Reset();

        Release(ref capture);
        Release(ref captureBlur);
        Release(ref trail);
        Release(ref trailTemp);
        Release(ref snow);
        Release(ref snowTemp);
        Release(ref skyVis);
        Release(ref windShadow);
    }

    void LateUpdate()
    {
        if (!IsReady) return;

        UpdateRegion();

        // GLOBALLER HER KAREDE YAZILIYOR, yalnız değişince değil. Bileşenlerin
        // OnEnable sırası eklenme sırasına bağlı; bir doku henüz hazır değilken
        // yazılan global null kalıyor ve belirti bir kare siyah zemin oluyor.
        WriteGlobals();
    }

    // ------------------------------------------------------------------ bölge

    /// STATİK VE PARAMETRELİ: sınama bu fonksiyonun KENDİSİNİ çağırıyor.
    /// Teste kopyasını yazmak, kopyanın doğruluğunu sınamak olur.
    public static Vector2Int SnapToTexelGrid(Vector3 worldPos, float texelSize)
    {
        // ÖNCE SnapStep IZGARASINA, SONRA TAM SAYI TEKSELE. Kesirli snap, snap
        // yapmamakla aynı belirtiyi üretiyor: izler teksel altı kayıp titriyor
        // (spec §6.4).
        float snapped = SnowConstants.SnapStep;

        float x = Mathf.Floor(worldPos.x / snapped) * snapped;
        float z = Mathf.Floor(worldPos.z / snapped) * snapped;

        return new Vector2Int(Mathf.RoundToInt(x / texelSize), Mathf.RoundToInt(z / texelSize));
    }

    void UpdateRegion()
    {
        Vector2Int next = SnapToTexelGrid(followTarget.position, TexelSize);
        if (next == centerTexel) return;

        // Teksel id'si merkez +delta kadar kayınca aynı dünya içeriği kaynakta
        // +delta teksel ötede kalıyor; KScroll bu yüzden `src = id + _ScrollTexels`.
        Vector2Int delta = next - centerTexel;

        pendingScrollTexels = pendingScroll ? pendingScrollTexels + delta : delta;
        pendingScroll = true;

        centerTexel = next;
    }

    // ---------------------------------------------------------------- globaller

    void WriteGlobals()
    {
        SnowQualityData q = settings.QualityData;
        Vector2 center = AreaCenter;

        Shader.SetGlobalVector(SnowShaderIDs.SnowAreaCenter, new Vector4(center.x, center.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.SnowAreaSize, q.AreaSize);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowResolution, q.Resolution);

        Shader.SetGlobalTexture(SnowShaderIDs.SnowStateTex, snow);
        Shader.SetGlobalTexture(SnowShaderIDs.SnowTrailTex, trail);
        Shader.SetGlobalTexture(SnowShaderIDs.SnowSkyVisTex, skyVis);
        Shader.SetGlobalTexture(SnowShaderIDs.SnowWindShadowTex, windShadow);

        // ÇEVRE SADECE OKUNUYOR (spec §3.6). Kar sistemi kendi rüzgârını üretmiyor,
        // gust simülasyonu kurmuyor; ne varsa köprüden geliyor.
        Vector3 wind = env.WindDirection * env.WindSpeed;

        Shader.SetGlobalVector(SnowShaderIDs.WindWS, new Vector4(wind.x, wind.y, wind.z, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, env.WindSpeed);
        Shader.SetGlobalFloat(SnowShaderIDs.TemperatureC, env.TemperatureC);
        Shader.SetGlobalFloat(SnowShaderIDs.SunElevation01, env.SunElevation01);
        Shader.SetGlobalFloat(SnowShaderIDs.FogDensity01, env.FogDensity01);
        Shader.SetGlobalVector(SnowShaderIDs.SnowUpDirection, Vector3.up);

        float rainOnSnow = env.PrecipKind == PrecipitationKind.Rain ? env.PrecipIntensity01 : 0f;
        Shader.SetGlobalFloat(SnowShaderIDs.RainOnSnow01, rainOnSnow);

        Shader.SetGlobalFloat(SnowShaderIDs.FallbackSWE, settings.DefaultSwe);
        Shader.SetGlobalFloat(SnowShaderIDs.FallbackRhoN, settings.DefaultRhoN);

        SnowRuntimeState.Stormness01 =
            Mathf.Clamp01(env.PrecipIntensity01 * Mathf.Clamp01(env.WindSpeed / 15f));
    }

    // ------------------------------------------------------------------ dispatch

    /// SnowRenderPass tek CommandBuffer içinde çağırıyor (spec §15.2).
    public void Dispatch(CommandBuffer cmd)
    {
        if (!IsReady) return;

        SnowQualityData q = settings.QualityData;
        int groups = Mathf.CeilToInt(q.Resolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.Resolution, q.Resolution);

        if (pendingClear)
        {
            ClearTo(cmd, snow, groups, new Vector4(settings.DefaultSwe, settings.DefaultRhoN, 0f, 0f));
            ClearTo(cmd, trail, groups, Vector4.zero);
            ClearTo(cmd, trailTemp, groups, Vector4.zero);
            ClearTo(cmd, capture, groups, Vector4.zero);
            ClearTo(cmd, captureBlur, groups, Vector4.zero);

            pendingClear = false;
        }

        if (pendingScroll)
        {
            LastScrollTexels = pendingScrollTexels;

            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.ScrollTexels,
                new Vector4(pendingScrollTexels.x, pendingScrollTexels.y, 0f, 0f));

            // RT_Trail: yeni şerit boş açılıyor — orada iz yok.
            Scroll(cmd, groups, ref trail, ref trailTemp, Vector4.zero);

            // RT_Snow: yeni şerit dünyanın genel kar durumuyla doluyor (spec §6.4).
            Scroll(cmd, groups, ref snow, ref snowTemp,
                   new Vector4(settings.DefaultSwe, settings.DefaultRhoN, 0f, 0f));

            pendingScroll = false;
            pendingScrollTexels = Vector2Int.zero;
        }
    }

    void ClearTo(CommandBuffer cmd, RenderTexture target, int groups, Vector4 value)
    {
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.ClearValue, value);
        cmd.SetComputeTextureParam(simCompute, clearKernel, SnowShaderIDs.Dst, target);
        cmd.DispatchCompute(simCompute, clearKernel, groups, groups, 1);
    }

    void Scroll(CommandBuffer cmd, int groups, ref RenderTexture src, ref RenderTexture dst,
                Vector4 newEdge)
    {
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.NewEdgeValue, newEdge);
        cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.Src, src);
        cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.Dst, dst);
        cmd.DispatchCompute(simCompute, scrollKernel, groups, groups, 1);

        // Ping-pong: aynı doku hem kaynak hem hedef olamaz (spec §20).
        (src, dst) = (dst, src);
    }

    // ------------------------------------------------------------------ dokular

    static RenderTexture Create(string name, int resolution, RenderTextureFormat format)
    {
        var rt = new RenderTexture(resolution, resolution, 0, format)
        {
            name = name,
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            hideFlags = HideFlags.HideAndDontSave,
        };

        rt.Create();
        return rt;
    }

    static void Release(ref RenderTexture rt)
    {
        if (rt == null) return;

        rt.Release();
        DestroyImmediate(rt);
        rt = null;
    }
}
