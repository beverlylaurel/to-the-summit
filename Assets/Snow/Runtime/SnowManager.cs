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

    [Tooltip("Kar yağışı. Boş bırakılırsa yağış çizilmez, gerisi çalışır.")]
    [SerializeField] SnowfallRenderer snowfallRenderer;

    [Tooltip("Ayak tozu ve püskürtme havuzu. Boş bırakılırsa çizilmez.")]
    [SerializeField] SnowBurstParticles burstParticles;

    [Tooltip("Uzak kaskad. Boş bırakılırsa bölge dışı sabit kar durumuna düşer.")]
    [SerializeField] SnowFarCascade farCascade;

    [Tooltip("İzlerin bölgeden çıkınca saklanması. Boş bırakılırsa izler kaybolur.")]
    [SerializeField] SnowPersistence persistence;
    [SerializeField] ComputeShader simCompute;

    [Tooltip("Hidden/Snow/CaptureDepth — deformer'ların alt yüzeyini yazar.")]
    [SerializeField] Shader captureShader;

    [Tooltip("Hidden/Snow/SkyDepth — kar yağışını engelleyen geometriyi yazar.")]
    [SerializeField] Shader skyShader;

    /// Gökyüzü haritasının engelleri bu layer'dan geliyor (spec §1.3).
    const string OccluderLayerName = "SnowOccluder";

    /// İndirgenmiş durumun kenarı (spec §17.1).
    const int ReducedResolution = 64;

    /// Geri okuma aralığı, kare (spec §17.1).
    const int ReadbackInterval = 30;

    /// Gauss-Seidel iterasyon sayısı (spec §18.0).
    const int WindShadowIterations = 24;

    /// Rüzgâr bu kadar dönünce gölge yeniden çözülüyor (spec §18.0).
    const float WindShadowAngleThreshold = 15f;

    /// Rüzgâr taşınımının haç döşemesi (spec §18.1): beş dispatch.
    const int WindTransportTiles = 5;

    /// Sahnede tek bir yönetici var; teşhis penceresi ve geçişler bunu okur.
    /// `FindObjectOfType` yerine: arama gizli bağımlılık yaratır, bu ise açık.
    public static SnowManager Active { get; private set; }

    ISnowEnvironmentSource env;

    RenderTexture capture, captureBlur;

    // ASSUMPTION: spec §6.2 tablosunda derinlik dokusu yok, ama §9.2 `ZWrite On`
    // + `ZTest LEqual` istiyor — en alçak yüzeyin kazanması derinlik tamponu
    // olmadan çözülemez. Renk kanallarında min alma (BlendOp Min) işe yaramaz:
    // R'nin en küçüğü ile GB'nin en küçüğü FARKLI fragmanlardan gelir ve hız
    // yanlış tekselle eşleşir. Bedeli 4 MB.
    RenderTexture captureDepth;
    RenderTexture trail, trailTemp;
    RenderTexture snow, snowTemp;
    RenderTexture skyVis;
    RenderTexture windShadow;

    // ASSUMPTION: spec §10.2 "iki geçişli separable blur" istiyor ama §6.2
    // tablosunda ikinci geçişin hedefi yok. İlkini RT_TrailTemp karşılıyor,
    // ikincisi kendi hedefini gerektiriyor — aynı dokuyu hem kaynak hem hedef
    // yapmak yasak (spec §20). Tek kanal, 2 MB.
    RenderTexture rimBlur;
    RenderTexture skyDepth;

    /// 64² indirgenmiş durum. Kaplama ve gevşek kar oranı buradan geri
    /// okunuyor; tam çözünürlükte okumak 8 MB'lık bir transfer olurdu.
    RenderTexture reduced;

    Material captureMaterial;
    Material skyMaterial;

    readonly SnowCaptureCamera captureCamera = new();
    readonly SnowSkyCamera skyCamera = new();
    readonly SnowfallController snowfall = new();

    int clearKernel = -1;
    int scrollKernel = -1;
    int blurCaptureKernel = -1;
    int deformKernel = -1;
    int rimBlurHKernel = -1;
    int rimBlurVKernel = -1;
    int rimKernel = -1;
    int accumulateKernel = -1;
    int reduceKernel = -1;
    int windShadowKernel = -1;
    int windTransportKernel = -1;

    /// Rüzgâr gölgesi ZAMANA YAYILIYOR. Yirmi dört iterasyon tek karede
    /// koşarsa 1024²'de elli milyon çağrı eder ve her dört metrede bir
    /// görünür bir takılma olur. Kare başına bir iterasyon: aynı sonuç,
    /// yarım saniyede yakınsıyor.
    int windShadowIterationsLeft;
    Vector2 windShadowDirection;

    /// Sastrugi yönü CPU'da yumuşatılıyor (spec §18.4, tau 120 s). Ham
    /// rüzgâr yönü kullanılırsa mevcut sistemin esintileri deseni titretiyor.
    Vector2 sastrugiWindDir = Vector2.right;

    int windTransportTile;

    int accumulateTile;
    int lastReadbackFrame = -1;
    bool readbackPending;

    /// İlk geri okuma gelene kadar kaplama dünyanın genel kar durumundan
    /// türetiliyor; sonra gerçek ölçüm devralıyor.
    bool coverageMeasured;

    /// Sahnede birden çok kamera var (oyun + sahne görünümü). Geçiş her kamera
    /// için kaydediliyor; simülasyon KARE BAŞINA BİR KEZ koşmalı, yoksa
    /// editörde her şey iki kat hızlı ilerler.
    int lastSimulatedFrame = -1;

    Vector2Int centerTexel;
    bool pendingClear;
    bool pendingScroll;
    Vector2Int pendingScrollTexels;

    public bool IsReady { get; private set; }

    public SnowSettings Settings => settings;
    public ISnowEnvironmentSource Environment => env;
    public SnowGroundHeight GroundHeight => groundHeight;

    /// Bu karede bölgede deformer var mıydı. Yoksa yakalama, blur ve (sonraki
    /// fazlarda) KDeform/KRim atlanıyor (spec §15.2).
    public bool CaptureActive { get; private set; }

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
        if (captureShader == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: {nameof(captureShader)} atanmadı.");
        if (skyShader == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: {nameof(skyShader)} atanmadı.");

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
        // ASSUMPTION: spec §6.2 tablosu RHalf diyor; RFloat açılıyor.
        // Doku MUTLAK dünya Y tutuyor ve §12.2'nin eşikleri 0.05–0.40 m.
        // Bu projenin arazisi ~4900 m'de ve yarım hassasiyetin oradaki adımı
        // 4 METRE (ölçüldü) — eşikler tamamen anlamsız kalırdı. Göreli
        // kodlama da yetmiyor: 50 m yukarıdaki bir kaya çıkıntısında adım
        // 6 cm'ye çıkıyor, eşik 5 cm. Bedeli 2 MB.
        skyVis = Create("RT_SkyVis", q.SkyResolution, RenderTextureFormat.RFloat);

        // ASSUMPTION: RT_WindShadow RGHalf. Spec §6.2 tablosu RGHalf diyor (R=Wz,
        // G=Wsz), §18.0 metni RHalf diyor. `KWindShadow` iki kanal birden yazıyor
        // (`_WindShadowZOut`, `_WindShadowSzOut`); tek kanal yetmez, tablo kazandı.
        // ASSUMPTION: spec §6.2 tablosu RGHalf diyor; RGFloat açılıyor.
        // Doku MUTLAK yüzey yüksekliği (Wz) tutuyor ve Gauss-Seidel onu
        // adım adım biriktiriyor. Bu projenin arazisi ~4900 m'de; yarım
        // hassasiyetin oradaki adımı 4 metre (ölçüldü) ve rüzgâr gölgesi
        // santimetre mertebesinde bir fark. RT_SkyVis ile aynı sebep.
        windShadow = Create("RT_WindShadow", q.SkyResolution, RenderTextureFormat.RGFloat);

        rimBlur = Create("RT_RimBlur", q.Resolution, RenderTextureFormat.RHalf);

        captureDepth = CreateDepth("RT_CaptureDepth", q.Resolution);
        skyDepth = CreateDepth("RT_SkyDepth", q.SkyResolution);

        reduced = Create("RT_SnowReduced", ReducedResolution, RenderTextureFormat.ARGBFloat);

        captureMaterial = new Material(captureShader) { hideFlags = HideFlags.HideAndDontSave };
        skyMaterial = new Material(skyShader) { hideFlags = HideFlags.HideAndDontSave };

        clearKernel = simCompute.FindKernel("KClear");
        scrollKernel = simCompute.FindKernel("KScroll");
        blurCaptureKernel = simCompute.FindKernel("KBlurCapture");
        deformKernel = simCompute.FindKernel("KDeform");
        rimBlurHKernel = simCompute.FindKernel("KRimBlurH");
        rimBlurVKernel = simCompute.FindKernel("KRimBlurV");
        rimKernel = simCompute.FindKernel("KRim");
        accumulateKernel = simCompute.FindKernel("KAccumulate");
        reduceKernel = simCompute.FindKernel("KReduceState");
        windShadowKernel = simCompute.FindKernel("KWindShadow");
        windTransportKernel = simCompute.FindKernel("KWindTransport");

        windShadowIterationsLeft = WindShadowIterations;
        windShadowDirection = Vector2.zero;
        windTransportTile = 0;
        sastrugiWindDir = Vector2.right;

        accumulateTile = 0;
        lastReadbackFrame = -1;
        readbackPending = false;
        coverageMeasured = false;

        snowfall.Reset();

        // Engeller statik; bir kez taranıyor (spec §12.1 — her kare değil).
        skyCamera.Rescan(LayerMask.NameToLayer(OccluderLayerName));

        lastSimulatedFrame = -1;

        centerTexel = SnapToTexelGrid(followTarget.position, TexelSize);
        pendingClear = true;
        pendingScroll = false;

        SnowRuntimeState.Reset();
        ApplyQualityKeyword(q);

        Active = this;
        IsReady = true;

        WriteGlobals();
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        IsReady = false;

        SnowRuntimeState.Reset();

        CaptureActive = false;

        if (captureMaterial != null)
        {
            DestroyImmediate(captureMaterial);
            captureMaterial = null;
        }

        if (skyMaterial != null)
        {
            DestroyImmediate(skyMaterial);
            skyMaterial = null;
        }

        snowfall.Reset();

        Release(ref capture);
        Release(ref captureBlur);
        Release(ref captureDepth);
        Release(ref trail);
        Release(ref trailTemp);
        Release(ref snow);
        Release(ref snowTemp);
        Release(ref skyVis);
        Release(ref windShadow);
        Release(ref rimBlur);
        Release(ref skyDepth);
        Release(ref reduced);
    }

    void LateUpdate()
    {
        if (!IsReady) return;

        UpdateRegion();

        // Yağış kararı ÖNCE: `_SnowfallSWERate` aynı karede KDeform ve
        // KAccumulate tarafından okunuyor.
        snowfall.Tick(env);

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

        float rainOnSnow = env.PrecipKind == PrecipitationKind.Rain ? env.PrecipIntensity01 : 0f;
        Shader.SetGlobalFloat(SnowShaderIDs.RainOnSnow01, rainOnSnow);

        if (farCascade != null) farCascade.WriteGlobals();

        Shader.SetGlobalFloat(SnowShaderIDs.FallbackSWE, settings.DefaultSwe);
        Shader.SetGlobalFloat(SnowShaderIDs.FallbackRhoN, settings.DefaultRhoN);

        // Sastrugi yönü YUMUŞATILIYOR (spec §18.4, tau 120 s).
        Vector2 rawWind = new Vector2(env.WindDirection.x, env.WindDirection.z);

        if (rawWind.sqrMagnitude > 1e-4f)
        {
            rawWind.Normalize();

            float k = 1f - Mathf.Exp(-Time.deltaTime / SnowConstants.SastrugiWindTau);
            sastrugiWindDir = Vector2.Lerp(sastrugiWindDir, rawWind, k).normalized;
        }

        Shader.SetGlobalVector(SnowShaderIDs.SastrugiWindDir,
            new Vector4(sastrugiWindDir.x, sastrugiWindDir.y, 0f, 0f));

        // Isı kaynakları: yalnız bölgeye değenler (spec §18.2).
        SnowHeatRegistry.Publish(AreaCenter, q.AreaSize);

        SnowRuntimeState.Stormness01 =
            Mathf.Clamp01(env.PrecipIntensity01 * Mathf.Clamp01(env.WindSpeed / 15f));

        // FAZ 4 VEKİLİ. Gerçek kaplama Faz 5'te `KAccumulate`'ten
        // `AsyncGPUReadback` ile geliyor (spec §11). O gelene kadar dünyanın
        // genel kar durumundan türetiliyor — yoksa kar mesh'leri hiç açılmaz
        // (spec §15.2 kaplama sıfırken her şeyi kapatıyor).
        if (!coverageMeasured)
        {
            // İLK GERİ OKUMAYA KADAR VEKİL. Ölçüm gelene kadar kar mesh'leri
            // hiç açılmasaydı ilk kareler çıplak arazi olurdu.
            float fallbackHeight = settings.DefaultSwe * SnowConstants.RhoWater /
                                   Mathf.Max(1f, Mathf.Lerp(SnowConstants.RhoMin,
                                                            SnowConstants.RhoMax,
                                                            settings.DefaultRhoN));

            SnowRuntimeState.GroundCoverage01 =
                Mathf.Clamp01(fallbackHeight / SnowConstants.MinVisibleHeight);
        }

        // `_SnowCoverage` ve `_SnowUpDirection` BURADAN YAZILMIYOR.
        // Sahibi `SnowCoverageDriver` (spec §16); iki yerden yazılsaydı
        // hangisinin kazandığı bileşen sırasına kalırdı.

        // Gökyüzü haritasının kapsamı — üç tüketici de bunu okuyor (spec §12).
        Vector2 skyCenter = skyCamera.Center;

        Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ,
            new Vector4(skyCenter.x, skyCenter.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, SnowConstants.SkyAreaSize);
        Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, q.SkyResolution);
    }

    // ------------------------------------------------------------------ dispatch

    /// SnowRenderPass tek CommandBuffer içinde çağırıyor (spec §15.2).
    /// `restoreView` / `restoreProj`: yakalama kendi ortografik matrisini
    /// yazdıktan sonra kameranınkini geri koyar.
    public void Dispatch(CommandBuffer cmd, Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        if (!IsReady) return;

        // KARE BAŞINA BİR KEZ. İki kamera aynı kareyi çizerse simülasyon iki
        // kez ilerler ve belirtisi "editörde kar iki kat hızlı eriyor" olur.
        if (lastSimulatedFrame == Time.frameCount) return;
        lastSimulatedFrame = Time.frameCount;

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

        // HER GEÇİŞ AYRI ÖRNEKLEYİCİDE (spec §15.1). `SnowProfiler` bunları
        // okuyup ms cinsinden yayınlıyor; ölçülmeden kabul edilmiyor.
        cmd.BeginSample(SnowProfiler.MarkerNames[0]);
        DispatchSky(cmd, restoreView, restoreProj);
        cmd.EndSample(SnowProfiler.MarkerNames[0]);

        cmd.BeginSample(SnowProfiler.MarkerNames[1]);
        DispatchCapture(cmd, groups, restoreView, restoreProj);
        cmd.EndSample(SnowProfiler.MarkerNames[1]);

        cmd.BeginSample(SnowProfiler.MarkerNames[2]);
        DispatchTrail(cmd, groups);
        cmd.EndSample(SnowProfiler.MarkerNames[2]);

        cmd.BeginSample(SnowProfiler.MarkerNames[3]);
        DispatchAccumulate(cmd, groups);

        DispatchWindShadow(cmd);
        DispatchWindTransport(cmd, groups);

        // KALICILIK BİRİKMEDEN SONRA. Geri yüklenen blok en son bilinen
        // durumu taşıyor; birikme onun üstüne yazsaydı yükleme boşa giderdi.
        if (persistence != null) persistence.Dispatch(cmd);

        if (farCascade != null) farCascade.Dispatch(cmd);
        cmd.EndSample(SnowProfiler.MarkerNames[3]);

        // Yağış simülasyonu da AYNI tamponda (spec §15.2).
        cmd.BeginSample(SnowProfiler.MarkerNames[4]);
        if (snowfallRenderer != null) snowfallRenderer.Dispatch(cmd);
        if (burstParticles != null) burstParticles.Dispatch(cmd);
        cmd.EndSample(SnowProfiler.MarkerNames[4]);

        // Ping-pong sonrası hangi dokunun güncel olduğu değişti; aynı karenin
        // geometrisi eskisini okumasın diye globaller burada tazeleniyor.
        cmd.SetGlobalTexture(SnowShaderIDs.SnowStateTex, snow);
        cmd.SetGlobalTexture(SnowShaderIDs.SnowTrailTex, trail);
    }

    // ------------------------------------------------------------- iz oluşumu

    void DispatchTrail(CommandBuffer cmd, int groups)
    {
        // Deformer yoksa iz de oluşmaz; KDeform ve KRim atlanıyor (spec §15.2).
        if (!CaptureActive) return;

        Texture ground = groundHeight.HeightTexture;
        if (ground == null) return;

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowDeltaTime, Time.deltaTime);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.RimBlurTexels, SnowConstants.RimBlurTexels);

        // --- KDeform: batma ve dolma ---
        // ZEMİN DOKUSU ELLE BAĞLANIYOR. `Shader.SetGlobalTexture` compute
        // kernel'ine ulaşmıyor (bu projede ölçüldü, `DECISIONS.md`).
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.GroundHeightTex, ground);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.CaptureBlur, captureBlur);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.Trail, trail);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.TrailOut, trailTemp);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.SnowOut, snowTemp);
        cmd.DispatchCompute(simCompute, deformKernel, groups, groups, 1);

        (trail, trailTemp) = (trailTemp, trail);
        (snow, snowTemp) = (snowTemp, snow);

        // --- Ayrılabilir blur: yatay, sonra düşey ---
        cmd.SetComputeTextureParam(simCompute, rimBlurHKernel, SnowShaderIDs.Src, trail);
        cmd.SetComputeTextureParam(simCompute, rimBlurHKernel, SnowShaderIDs.Dst, trailTemp);
        cmd.DispatchCompute(simCompute, rimBlurHKernel, groups, groups, 1);

        cmd.SetComputeTextureParam(simCompute, rimBlurVKernel, SnowShaderIDs.Src, trailTemp);
        cmd.SetComputeTextureParam(simCompute, rimBlurVKernel, SnowShaderIDs.CarveOut, rimBlur);
        cmd.DispatchCompute(simCompute, rimBlurVKernel, groups, groups, 1);

        // --- KRim: blur(carve) − carve ---
        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.Trail, trail);
        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.CaptureBlur, captureBlur);
        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.BlurredCarve, rimBlur);
        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.TrailOut, trailTemp);
        cmd.DispatchCompute(simCompute, rimKernel, groups, groups, 1);

        (trail, trailTemp) = (trailTemp, trail);
    }

    // ------------------------------------------------------- gökyüzü haritası

    void DispatchSky(CommandBuffer cmd, Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        // HER KARE DEĞİL (spec §12.1). Statik geometrinin silueti kare kare
        // değişmiyor.
        if (!skyCamera.NeedsRefresh(AreaCenter)) return;

        skyCamera.Record(cmd, skyVis, skyDepth, skyMaterial,
                         AreaCenter, followTarget.position.y, restoreView, restoreProj);
    }

    /// Sahneye engel eklendiğinde veya taşındığında çağrılır.
    public void MarkSkyVisDirty()
    {
        skyCamera.Rescan(LayerMask.NameToLayer(OccluderLayerName));
    }

    // ----------------------------------------------------- rüzgâr gölgesi

    void DispatchWindShadow(CommandBuffer cmd)
    {
        Vector2 wind = new Vector2(env.WindDirection.x, env.WindDirection.z);

        // Rüzgâr 15°'den fazla döndüyse baştan çöz (spec §18.0).
        if (wind.sqrMagnitude > 1e-4f)
        {
            wind.Normalize();

            float dot = Vector2.Dot(wind, windShadowDirection);

            if (windShadowDirection == Vector2.zero ||
                dot < Mathf.Cos(WindShadowAngleThreshold * Mathf.Deg2Rad))
            {
                windShadowDirection = wind;
                windShadowIterationsLeft = WindShadowIterations;
            }
        }

        if (windShadowIterationsLeft <= 0) return;

        windShadowIterationsLeft--;

        SnowQualityData q = settings.QualityData;
        int groups = Mathf.CeilToInt(q.SkyResolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeTextureParam(simCompute, windShadowKernel, SnowShaderIDs.SkyVisY, skyVis);
        cmd.SetComputeTextureParam(simCompute, windShadowKernel, SnowShaderIDs.WindShadow, windShadow);

        Texture ground = groundHeight.HeightTexture;
        if (ground == null) return;

        cmd.SetComputeTextureParam(simCompute, windShadowKernel, SnowShaderIDs.GroundHeightTex, ground);

        // DAMA TAHTASI: iki parite, aynı karede. Tek parite koşarsa çözüm
        // yarım kalıyor ve gölge hiç oluşmuyor (spec §22).
        for (int parity = 0; parity < 2; parity++)
        {
            cmd.SetComputeIntParam(simCompute, SnowShaderIDs.GSParity, parity);
            cmd.DispatchCompute(simCompute, windShadowKernel, groups, groups, 1);
        }
    }

    // ---------------------------------------------------- rüzgâr taşınımı

    void DispatchWindTransport(CommandBuffer cmd, int groups)
    {
        Texture ground = groundHeight.HeightTexture;
        if (ground == null) return;

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowDeltaTime, Time.deltaTime);

        cmd.SetComputeTextureParam(simCompute, windTransportKernel, SnowShaderIDs.GroundHeightTex, ground);
        cmd.SetComputeTextureParam(simCompute, windTransportKernel,
                                   SnowShaderIDs.SnowWindShadowTex, windShadow);
        cmd.SetComputeTextureParam(simCompute, windTransportKernel, SnowShaderIDs.SnowRW, snow);
        cmd.SetComputeTextureParam(simCompute, windTransportKernel, SnowShaderIDs.TrailRW, trail);

        // HAÇ DÖŞEMESİ: beş dispatch, her biri komşularıyla çakışmayan
        // hücreleri işliyor (spec §18.1). Atomik kullanılmıyor.
        for (int tile = 1; tile <= WindTransportTiles; tile++)
        {
            cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TileIndex, tile);
            cmd.DispatchCompute(simCompute, windTransportKernel, groups, groups, 1);
        }
    }

    // --------------------------------------------------------------- birikme

    void DispatchAccumulate(CommandBuffer cmd, int groups)
    {
        SnowQualityData q = settings.QualityData;

        Texture ground = groundHeight.HeightTexture;
        if (ground == null) return;

        int tiles = Mathf.Max(1, q.AccumulateTiles);
        accumulateTile = (accumulateTile + 1) % tiles;

        // DÖŞEME DÖNDÜRMESİ (spec §15.2). Her karede dokunun 1/tiles'ı
        // işleniyor, dt aynı katla çarpılıyor. Kar oturması ve erimesi saat
        // mertebesinde; görsel fark yok.
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DeltaTimeEff, Time.deltaTime * tiles);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TileIndex, accumulateTile);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TileCount, tiles);

        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.GroundHeightTex, ground);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.SnowSkyVisTex, skyVis);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.SnowOut, snowTemp);

        // KABUK RT_Trail.B'DE (spec §18.3, §20). KAccumulate onu da yazıyor;
        // iz dokusunun aynı şeridi de taşınmak zorunda.
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.Trail, trail);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.TrailOut, trailTemp);

        int tileGroups = Mathf.Max(1, groups / tiles);
        cmd.DispatchCompute(simCompute, accumulateKernel, tileGroups, groups, 1);

        // YALNIZ BİR ŞERİT YAZILDI. Ping-pong yapılamaz — diğer şeritler eski
        // tamponda kalırdı. Yazılan şerit hedefe kopyalanıyor.
        int tileWidth = q.Resolution / tiles;
        int tileX = accumulateTile * tileWidth;

        cmd.CopyTexture(snowTemp, 0, 0, tileX, 0, tileWidth, q.Resolution,
                        snow, 0, 0, tileX, 0);

        cmd.CopyTexture(trailTemp, 0, 0, tileX, 0, tileWidth, q.Resolution,
                        trail, 0, 0, tileX, 0);

        DispatchReduce(cmd);
    }

    void DispatchReduce(CommandBuffer cmd)
    {
        // Otuz karede bir (spec §17.1). Daha sık okumanın kazancı yok:
        // kaplama saat mertebesinde değişiyor.
        if (readbackPending) return;
        if (Time.frameCount - lastReadbackFrame < ReadbackInterval) return;

        lastReadbackFrame = Time.frameCount;

        int reduceGroups = Mathf.CeilToInt(ReducedResolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeTextureParam(simCompute, reduceKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, reduceKernel, SnowShaderIDs.ReducedOut, reduced);
        cmd.DispatchCompute(simCompute, reduceKernel, reduceGroups, reduceGroups, 1);

        readbackPending = true;
        cmd.RequestAsyncReadback(reduced, OnReduced);
    }

    void OnReduced(AsyncGPUReadbackRequest request)
    {
        readbackPending = false;

        if (!IsReady || request.hasError) return;

        Unity.Collections.NativeArray<Color> data = request.GetData<Color>();

        float coverage = 0f;
        float loose = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            coverage += data[i].b;
            loose += 1f - data[i].g;
        }

        float inv = 1f / Mathf.Max(1, data.Length);

        SnowRuntimeState.GroundCoverage01 = Mathf.Clamp01(coverage * inv);
        SnowRuntimeState.LooseSnowFraction = Mathf.Clamp01(loose * inv);

        coverageMeasured = true;
    }

    // ------------------------------------------------------------------ yakalama

    void DispatchCapture(CommandBuffer cmd, int groups, Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        float areaSize = settings.QualityData.AreaSize;
        float observerY = followTarget.position.y;

        // KAR YOKSA HİÇ İŞ YOK. Bölgede deformer yoksa ne çizim ne blur olur;
        // sonraki fazlarda KDeform ve KRim de bu bayrağa bakacak (spec §15.2).
        CaptureActive = SnowCaptureCamera.HasWork(AreaCenter, areaSize, observerY);
        if (!CaptureActive) return;

        captureCamera.Record(cmd, capture, captureDepth, captureMaterial,
                             AreaCenter, areaSize, observerY, restoreView, restoreProj);

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.BlurRadiusTexels,
                                 SnowConstants.BlurRadiusTexels);
        cmd.SetComputeTextureParam(simCompute, blurCaptureKernel, SnowShaderIDs.Src, capture);
        cmd.SetComputeTextureParam(simCompute, blurCaptureKernel, SnowShaderIDs.Dst, captureBlur);
        cmd.DispatchCompute(simCompute, blurCaptureKernel, groups, groups, 1);
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

    /// KALİTE KADEMESİ SHADER'A KEYWORD OLARAK GİDİYOR (spec §15.3). Detay
    /// normal katmanı sayısı ve parıltı bunlarla açılıp kapanıyor; runtime
    /// dalı yerine varyant seçilmesi dallanmayı tamamen kaldırıyor.
    static void ApplyQualityKeyword(SnowQualityData quality)
    {
        Shader.DisableKeyword(SnowQuality.KeywordLow);
        Shader.DisableKeyword(SnowQuality.KeywordMedium);
        Shader.DisableKeyword(SnowQuality.KeywordHigh);

        Shader.EnableKeyword(quality.Keyword);
    }

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

    /// Yakalamanın derinlik tamponu. `enableRandomWrite` yok — compute buraya
    /// yazmıyor, yalnız çizim kullanıyor.
    static RenderTexture CreateDepth(string name, int resolution)
    {
        var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.Depth)
        {
            name = name,
            filterMode = FilterMode.Point,
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
