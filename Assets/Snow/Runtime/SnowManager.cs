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

    [Tooltip("Kar detay normali (spec §14.2). Global yayınlanıyor: kar mesh'i " +
             "ile dağın kar katmanı AYNI dokuyu kullanıyor.")]
    [SerializeField] Texture2D detailNormal;

    [Tooltip("İzlerin bölgeden çıkınca saklanması. Boş bırakılırsa izler kaybolur.")]
    [SerializeField] SnowPersistence persistence;

    [Tooltip("Süspansiyon perdeleri. Boş bırakılırsa çizilmez.")]
    [SerializeField] ComputeShader simCompute;

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

    Material skyMaterial;

    /// İZ PARÇALARI. Her deformer bu kare içinde bir doğru parçası süpürüyor;
    /// tampon kare başına doldurulup `KDeform`'a bağlanıyor.
    ///
    /// Parça başına İKİ `float4`, stride 16 B. Yapı kullanılırsa tamponun
    /// stride'ı (32) ile `Vector4[]` eleman boyu (16) ayrışıyor ve `SetData`'nın
    /// eleman sayımı sessizce yanlış oluyor.
    const int TrailSegmentStride = 16;

    /// Aynı anda karda iz bırakan nesne sayısı için tavan. `SnowDeformerRegistry`
    /// daha fazlasını tutabilir; bölge dışındakiler zaten eleniyor.
    const int MaxTrailSegments = 16;

    ComputeBuffer trailSegmentBuffer;
    readonly Vector4[] trailSegmentData = new Vector4[MaxTrailSegments * 2];
    int trailSegmentCount;

    readonly SnowSkyCamera skyCamera = new();
    readonly SnowfallController snowfall = new();

    int clearKernel = -1;
    int scrollKernel = -1;
    int deformKernel = -1;
    int reposeKernel = -1;
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

    int accumulateTile;
    int lastReadbackFrame = -1;
    bool readbackPending;

    /// İlk geri okuma gelene kadar kaplama dünyanın genel kar durumundan
    /// türetiliyor; sonra gerçek ölçüm devralıyor.
    bool coverageMeasured;

    /// Sahnede birden çok kamera var (oyun + sahne görünümü). Geçiş her kamera
    /// için kaydediliyor; simülasyon KARE BAŞINA BİR KEZ koşmalı, yoksa
    /// editörde her şey iki kat hızlı ilerler.
    float lastSimulatedTime = -1f;
    float simDeltaTime;

    /// SİMÜLASYON İZOLASYON ANAHTARLARI (teşhis).
    ///
    /// Prob, kusurun VERİDE olduğunu gösterdi: taban derinliğinde kama
    /// şeklinde fazla kar. Kütleyi hareket ettiren tek şey rüzgâr taşınımı;
    /// onu ve beslediği rüzgâr gölgesini ayrı ayrı kapatmak sorumluyu tek
    /// turda ayırıyor.
    [System.NonSerialized] public bool WindTransportOff;
    [System.NonSerialized] public bool WindShadowOff;

    /// Yakın bölgenin ortalama SWE'si ve yoğunluğu — kaskadla karşılaştırmak
    /// için. Derinlik = SWE × 1000 / ρ.
    public float MeanSwe { get; private set; } = -1f;
    public float MeanRhoN { get; private set; } = -1f;

    /// UYKU KOŞULU (spec §15.2). Zeminde kar yok VE yağmıyor.
    ///
    /// `pendingClear` uykuyu bozuyor: bölge daha hiç doldurulmadıysa bir kez
    /// koşması gerekiyor, yoksa dokular çöp kalıyor ve uyanınca bir kare
    /// bozuk kar görünüyor.
    /// `pendingFill` de uykuyu bozuyor: sınama doldurması yağış kapalıyken
    /// yapılıyor ve uyku bunu hiç dispatch etmiyordu — düğmeye basılıyor,
    /// hiçbir şey olmuyordu (ölçüldü).
    public bool IsDormant => !pendingClear
                             && !pendingFill
                             && !SnowRuntimeState.IsSnowing
                             && SnowRuntimeState.GroundCoverage01 < 0.01f;

    Vector2Int centerTexel;
    bool pendingClear;
    bool pendingScroll;
    Vector2Int pendingScrollTexels;

    public bool IsReady { get; private set; }

    public SnowSettings Settings => settings;
    public ISnowEnvironmentSource Environment => env;

    /// YAĞIŞIN NE KADARI KAR: 1 tamamen kar, 0 tamamen yağmur.
    ///
    /// Varsayılan 1 — "yağış varsa kar yağar". Sıcaklıkla ilgisi yok; hava
    /// sistemi ya da teşhis paneli buradan sürüyor.
    public float SnowFraction01 { get; set; } = 1f;
    public SnowGroundHeight GroundHeight => groundHeight;

    /// Bu karede bölgede iz bırakan bir nesne var mıydı. Yoksa KDeform ve
    /// KRim atlanıyor (spec §15.2).
    public bool CaptureActive => trailSegmentCount > 0;
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

        // RT_Trail ARGBHalf: B (kabuk) ve A (sastrugi) Faz 11–12'de doluyor ama doku
        // BAŞTAN dört kanallı açılıyor. Sonradan format değiştirmek bütün kernel'leri
        // ve shader'ları etkiliyor (spec §6.2).
        trail = Create("RT_Trail", q.Resolution, RenderTextureFormat.ARGBHalf);
        trailTemp = Create("RT_TrailTemp", q.Resolution, RenderTextureFormat.ARGBHalf);

        // RT_SNOW ARGBFloat, HALF DEĞİL — SWE'nin büyüklüğü yarım hassasiyete sığmıyor.
        //
        // R kanalı su eşdeğeri (m). Tipik değerler 1e-6 – 1e-2 arası; half'ta
        // 6.1e-5'in altı SUBNORMAL ve temsil adımı sabit 5.96e-8. Kare başına
        // eklenen `1.39e-6 × dt` ise 5e-8 — adımın ALTINDA. Artış yuvarlanmada
        // eriyor, kar hiç tutmuyor.
        //
        // Ölçüldü: gerçekleşen birikme hızı beklenenin 1/470'i.
        //
        // Aynı sınıfın emsali hemen aşağıda: RT_SkyVis de mutlak dünya Y
        // tuttuğu için RHalf'tan RFloat'a alınmıştı.
        //
        // Bedel: 1024² × 16 B = 16 MB (half'ta 8 MB), iki tampon için +16 MB.
        snow = Create("RT_Snow", q.Resolution, RenderTextureFormat.ARGBFloat);

        // ASSUMPTION: RT_SnowTemp spec §6.2 tablosunda yok ama KScroll komşu teksel
        // okuyor ve §20 "komşu okuyan pass'lerde ping-pong" diyor — RT_Snow'un da
        // kendi tamponuna ihtiyacı var. Başka bir dokuyu ödünç almak format aynı
        // olsa bile o dokunun içeriğini siler. Bedeli 8 MB.
        // Formatı RT_Snow ile AYNI olmak zorunda: `CopyTexture` ikisi arasında
        // şerit kopyalıyor ve format ayrışırsa kopya sessizce düşer.
        snowTemp = Create("RT_SnowTemp", q.Resolution, RenderTextureFormat.ARGBFloat);
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

        skyDepth = CreateDepth("RT_SkyDepth", q.SkyResolution);

        reduced = Create("RT_SnowReduced", ReducedResolution, RenderTextureFormat.ARGBFloat);

        skyMaterial = new Material(skyShader) { hideFlags = HideFlags.HideAndDontSave };

        clearKernel = simCompute.FindKernel("KClear");
        scrollKernel = simCompute.FindKernel("KScroll");
        deformKernel = simCompute.FindKernel("KDeform");
        reposeKernel = simCompute.FindKernel("KRepose");
        rimBlurHKernel = simCompute.FindKernel("KRimBlurH");
        rimBlurVKernel = simCompute.FindKernel("KRimBlurV");
        rimKernel = simCompute.FindKernel("KRim");
        accumulateKernel = simCompute.FindKernel("KAccumulate");
        reduceKernel = simCompute.FindKernel("KReduceState");
        windShadowKernel = simCompute.FindKernel("KWindShadow");
        windTransportKernel = simCompute.FindKernel("KWindTransport");

        windShadowIterationsLeft = WindShadowIterations;
        windShadowDirection = Vector2.zero;
        sastrugiWindDir = Vector2.right;

        accumulateTile = 0;
        lastReadbackFrame = -1;
        readbackPending = false;
        coverageMeasured = false;

        snowfall.Reset();

        // Engeller statik; bir kez taranıyor (spec §12.1 — her kare değil).
        skyCamera.Rescan(LayerMask.NameToLayer(OccluderLayerName));

        lastSimulatedTime = -1f;

        centerTexel = SnapToTexelGrid(followTarget.position, TexelSize, settings.QualityData.SnapStep);
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

        trailSegmentCount = 0;

        if (trailSegmentBuffer != null)
        {
            trailSegmentBuffer.Release();
            trailSegmentBuffer = null;
        }

        if (skyMaterial != null)
        {
            DestroyImmediate(skyMaterial);
            skyMaterial = null;
        }

        snowfall.Reset();

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
        snowfall.Tick(env, SnowFraction01);

        TickWorldSnow(Time.deltaTime * Mathf.Max(0f, SimTimeScale));

        // GLOBALLER HER KAREDE YAZILIYOR, yalnız değişince değil. Bileşenlerin
        // OnEnable sırası eklenme sırasına bağlı; bir doku henüz hazır değilken
        // yazılan global null kalıyor ve belirti bir kare siyah zemin oluyor.
        WriteGlobals();
    }

    // ------------------------------------------------------------------ bölge

    /// STATİK VE PARAMETRELİ: sınama bu fonksiyonun KENDİSİNİ çağırıyor.
    /// Teste kopyasını yazmak, kopyanın doğruluğunu sınamak olur.
    ///
    /// SNAP ADIMI DIŞARIDAN. Sabit değil, kalite presetinden türüyor
    /// (`SnowQualityData.SnapStep` = quad × 2). Sabit yazılsaydı preset
    /// değişince quad boyu kayar, adım kalır ve oran bozulurdu (spec §6.4).
    public static Vector2Int SnapToTexelGrid(Vector3 worldPos, float texelSize, float snapStep)
    {
        // ÖNCE SnapStep IZGARASINA, SONRA TAM SAYI TEKSELE. Kesirli snap, snap
        // yapmamakla aynı belirtiyi üretiyor: izler teksel altı kayıp titriyor
        // (spec §6.4).
        float snapped = snapStep;

        float x = Mathf.Floor(worldPos.x / snapped) * snapped;
        float z = Mathf.Floor(worldPos.z / snapped) * snapped;

        return new Vector2Int(Mathf.RoundToInt(x / texelSize), Mathf.RoundToInt(z / texelSize));
    }

    void UpdateRegion()
    {
        Vector2Int next = SnapToTexelGrid(followTarget.position, TexelSize, settings.QualityData.SnapStep);
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

        // KAR ÜSTÜNE YAĞMUR, KARIN KENDİ KARARINDAN — `PrecipKind` ETİKETİNDEN DEĞİL.
        //
        // `env.PrecipKind` hava sisteminin etiketi ve yağış sıcaklıktan
        // koparıldığından beri −5 °C'de bile `Rain` diyor. Buradan okununca kar
        // YAĞARKEN zemin üstüne yağmur yağıyormuş gibi davranıyordu: yoğunluk
        // 55'ten 167 kg/m³'e çıkıyor, derinlik üçte bire iniyor ve 4 mm'lik
        // görünürlük eşiğini hiç geçemiyordu. Ölçüldü — SWE 5.5e-4 iken
        // derinlik 3.31 mm, örtü 0.
        //
        // Kar mı yağmur mu KESKİN bir karar ve sahibi `SnowfallController`
        // (eşik 0.5, ikisi asla birlikte). Fizik de aynı kararı okumalı, yoksa
        // ekranda kar yağarken zeminde yağmur işliyor.
        float rainOnSnow = SnowRuntimeState.IsSnowing
                         ? 0f
                         : SnowRuntimeState.RainWeight01 * env.PrecipIntensity01;

        Shader.SetGlobalFloat(SnowShaderIDs.RainOnSnow01, rainOnSnow);

        // DETAY NORMALİ GLOBAL. Materyalde de duruyor ama dağın kar katmanı
        // ayrı bir materyal; ikisinin aynı dokuyu kullanması için tek yer bu.
        if (detailNormal != null)
            Shader.SetGlobalTexture(SnowShaderIDs.SnowDetailNormal, detailNormal);

        // PARILTI AYARLARI DA GLOBAL, AYNI GEREKÇEYLE: arazi karı da
        // parıldıyor ve o ayrı bir materyal. Tek sahibi bu ayar asset'i.
        Shader.SetGlobalColor(SnowShaderIDs.ShadowTint, settings.ShadowTint);
        Shader.SetGlobalFloat(SnowShaderIDs.TranslucencyStrength, settings.TranslucencyStrength);
        Shader.SetGlobalFloat(SnowShaderIDs.SparkleCellSize, settings.SparkleCellSize);
        Shader.SetGlobalFloat(SnowShaderIDs.SparkleDensity, settings.SparkleDensity);
        Shader.SetGlobalFloat(SnowShaderIDs.SparkleSharpness, settings.SparkleSharpness);
        Shader.SetGlobalFloat(SnowShaderIDs.SparkleIntensity, settings.SparkleIntensity);

        // YÜZEY DOKULARI GLOBAL. Kar mesh'i ve arazi aynı dokuyu görmek
        // zorunda: mesh yalnız yerel sapmayı çiziyor, düz alanı arazi çiziyor
        // ve ikisi farklı doku kullansaydı bölge sınırı yine kendini
        // gösterirdi (gerekçe `SnowSettings` → yüzey dokuları).
        Yayinla("_SnowSurfTazeColor", settings.SurfTazeColor);
        Yayinla("_SnowSurfTazeNormal", settings.SurfTazeNormal);
        Yayinla("_SnowSurfTazeRough", settings.SurfTazeRough);
        Yayinla("_SnowSurfTozColor", settings.SurfTozColor);
        Yayinla("_SnowSurfTozNormal", settings.SurfTozNormal);
        Yayinla("_SnowSurfTozRough", settings.SurfTozRough);
        Yayinla("_SnowSurfYerlesmisColor", settings.SurfYerlesmisColor);
        Yayinla("_SnowSurfYerlesmisNormal", settings.SurfYerlesmisNormal);
        Yayinla("_SnowSurfYerlesmisRough", settings.SurfYerlesmisRough);
        Yayinla("_SnowSurfRuzgarColor", settings.SurfRuzgarColor);
        Yayinla("_SnowSurfRuzgarNormal", settings.SurfRuzgarNormal);
        Yayinla("_SnowSurfRuzgarRough", settings.SurfRuzgarRough);

        Shader.SetGlobalFloat("_SnowSurfTileMeters", Mathf.Max(0.01f, settings.SurfTileMeters));

        // Kar-gök çoklu yansıması normalde açık; ölçüm sırasında dışarıdan
        // 0 yazılıp aynı karede kapalı hâli alınabiliyor.
        Shader.SetGlobalFloat("_SnowMultiScatter", 1f);
        Shader.SetGlobalFloat("_SnowSurfStrength", settings.SurfStrength);

        // BÖLGE DIŞI DÜNYANIN KARINI GÖRÜYOR, sabit bir varsayılanı değil.
        Shader.SetGlobalFloat(SnowShaderIDs.FallbackSWE, Mathf.Max(0f, WorldSwe));

        // YOĞUNLUK BÖLGENİN ÖLÇÜLEN ORTALAMASINDAN GELİYOR.
        //
        // `WorldRhoN` yalnız yağışla güncelleniyor; bölgedeki doku ise ayrıca
        // SIKIŞIYOR (`KDeform`). İkisi zamanla ayrışıyordu — ölçüldü:
        // `WorldRhoN = 0.0119`, dokunun ortalaması `0.0799`, altı kattan fazla.
        //
        // Yoğunluk hem albedoyu hem pürüzlülüğü sürüyor, üstelik
        // `SnowBaseHeight = SWE·1000/ρ` üzerinden KALINLIĞI da: aynı SWE ile
        // arazi 50.4 cm, kar mesh'i 33.7 cm veriyordu. Bölgenin sınırı hem
        // renk hem kot atlıyor ve oyuncuyu izleyen 24 m'lik bir kare olarak
        // görünüyordu.
        //
        // Bölge dünyanın örneklendiği yer; ortalaması dünyanın değeri için en
        // iyi tahmin. Ölçüm henüz yapılmadıysa yağıştan gelen değere düşülüyor.
        float dunyaRhoN = MeanRhoN >= 0f ? MeanRhoN
                        : (WorldRhoN >= 0f ? WorldRhoN : settings.DefaultRhoN);
        Shader.SetGlobalFloat(SnowShaderIDs.FallbackRhoN, dunyaRhoN);

        // ARAZİ KAR IŞIKLANDIRMASININ DERİNLİĞİ.
        //
        // Arazi `_SnowCoverThickness` (4 cm) kullanıyordu; o sabit NESNELERİN
        // üstündeki ince örtü için (spec §16). Kar mesh'i ise gerçek sütunu
        // (~50 cm) görüyor. Derinlik `SnowAmbient`'ın sızma terimini
        // `exp(-derinlik·7)` ile sürüyor, yani iki yüzey aynı noktada farklı
        // parlaklıkta çıkıyordu — 24 m'lik kare belirtisinin ölçülen
        // paylarından biri (1.61 kat farkın bir bölümü).
        //
        // Değer BURADA hesaplanıyor, shader'da fonksiyon çağrılmıyor: aynı
        // hesabı fragment aşamasında yapmak denendi ve arazi ışıklandırmasını
        // bozdu.
        float dunyaDerinlik = SnowBaseHeightMetre(Mathf.Max(0f, WorldSwe), dunyaRhoN);
        Shader.SetGlobalFloat(SnowShaderIDs.WorldSnowDepth, dunyaDerinlik);

        // KAR ÇİZGİSİ DONMA SEVİYESİNDEN. Ayrı bir sayı tanımlanmıyor;
        // sıcaklık alanı neredeyse kar da orada başlıyor.

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
    /// `restoreView` / `restoreProj`: gökyüzü yakalaması kendi ortografik
    /// matrisini yazdıktan sonra kameranınkini geri koyar.
    public void Dispatch(CommandBuffer cmd, Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        if (!IsReady) return;

        // SİMÜLASYON ADIMI GEÇEN ZAMANDAN TÜRÜYOR, `Time.deltaTime`'DAN DEĞİL.
        //
        // `RecordRenderGraph` her kamera için ayrı koşuyor (oyun görünümü,
        // sahne görünümü, yardımcı kameralar) ve `Time.frameCount` bunların
        // arasında ilerleyebiliyor — kare sayacına bakan koruma tutmuyordu.
        //
        // Ölçüldü: KDeform 525 karede 1602 kez koştu (3.05 kat) ve HER koşu
        // tam bir karelik zaman uyguladı. Belirtisi ayak izinin saniyeler
        // içinde kapanmasıydı; ama hızlanan yalnız iz değil, oturma, kabuk,
        // birikme ve sastrugi de aynı katsayıyla akıyordu.
        //
        // Geçen zamanı okumak kamera sayısından bağımsız: aynı anda gelen
        // ikinci çağrı sıfır adım alır ve erken çıkar, simülasyon toplamda
        // gerçek zaman kadar ilerler.
        if (lastSimulatedTime < 0f) lastSimulatedTime = Time.time;

        float gecen = Time.time - lastSimulatedTime;
        if (gecen <= 0f) return;

        lastSimulatedTime = Time.time;

        // Takılma sonrası tek karede dakikalarca simüle etmemek için tavan;
        // `Time.deltaTime`'ın kendi tavanıyla aynı büyüklük.
        simDeltaTime = Mathf.Min(gecen, Time.maximumDeltaTime);

        // KAR YOKSA HER ŞEY KAPALI (spec §15.2 — "bu entegrasyon için kritik").
        //
        // Yazın oyun kar sistemi yokmuş gibi performans göstermeli. Mesh ve
        // yağış zaten kendi kapılarını taşıyordu; compute pass'leri taşımıyordu
        // ve zeminde tek gram kar yokken de her kare koşuyorlardı.
        //
        // Bekleyen kaydırma/temizlik BİRİKMEYE devam ediyor: uyanınca bölge
        // doğru yerden doluyor, uykuda geçen mesafe kaybolmuyor.
        if (IsDormant) return;

        SnowQualityData q = settings.QualityData;
        int groups = Mathf.CeilToInt(q.Resolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.Resolution, q.Resolution);

        if (pendingClear)
        {
            ClearTo(cmd, snow, groups, WorldSnowValue);
            ClearTo(cmd, trail, groups, Vector4.zero);
            ClearTo(cmd, trailTemp, groups, Vector4.zero);

            pendingClear = false;
        }

        if (pendingFill)
        {
            // İZ DOKUSU DA SIFIRLANIYOR: eski oyuklar yeni kar tabakasının
            // altında kalırdı ve "10 cm kar" dendiğinde ekranda delik delik
            // bir yüzey çıkardı.
            ClearTo(cmd, snow, groups, new Vector4(fillSwe, fillRhoN, 0f, 0f));
            ClearTo(cmd, trail, groups, Vector4.zero);
            ClearTo(cmd, trailTemp, groups, Vector4.zero);

            pendingFill = false;
        }

        if (pendingScroll)
        {
            LastScrollTexels = pendingScrollTexels;

            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.ScrollTexels,
                new Vector4(pendingScrollTexels.x, pendingScrollTexels.y, 0f, 0f));

            // RT_Trail: yeni şerit boş açılıyor — orada iz yok.
            Scroll(cmd, groups, ref trail, ref trailTemp, Vector4.zero);

            // RT_Snow: yeni şerit dünyanın genel kar durumuyla doluyor (spec §6.4).
            Scroll(cmd, groups, ref snow, ref snowTemp, WorldSnowValue);

            pendingScroll = false;
            pendingScrollTexels = Vector2Int.zero;
        }

        // HER GEÇİŞ AYRI ÖRNEKLEYİCİDE (spec §15.1). `SnowProfiler` bunları
        // okuyup ms cinsinden yayınlıyor; ölçülmeden kabul edilmiyor.
        cmd.BeginSample(SnowProfiler.MarkerNames[0]);
        DispatchSky(cmd, restoreView, restoreProj);
        cmd.EndSample(SnowProfiler.MarkerNames[0]);

        // COMPUTE PARAMETRELERİ BÜTÜN ÇEKİRDEKLERDEN ÖNCE. Compute shader'lar
        // `Shader.SetGlobalFloat` ile yayınlanan globalleri ALMIYOR; buradan
        // yazılmayan her değer çekirdekte eski kalır.
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowfallSWERate,
                                 snowfall.SnowfallSweRate);

        // İZSİZ KARIN YOĞUNLUĞU DA ÇEKİRDEĞE GİDİYOR. `KDeform` sıkışmanın
        // tabanını buradan alıyor; sabit bir döşeme yazılırsa diskin kenarında
        // yoğunluk sıçrıyor ve iz tarak gibi çıkıyor (gerekçe `SnowSim.compute`).
        // Global olarak da yayınlanıyor ama compute globalleri görmüyor.
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.FallbackRhoN,
                                 WorldRhoN >= 0f ? WorldRhoN : settings.DefaultRhoN);

        cmd.BeginSample(SnowProfiler.MarkerNames[1]);
        BuildTrailSegments(cmd);
        cmd.EndSample(SnowProfiler.MarkerNames[1]);

        cmd.BeginSample(SnowProfiler.MarkerNames[2]);
        DispatchTrail(cmd, groups);
        cmd.EndSample(SnowProfiler.MarkerNames[2]);

        cmd.BeginSample(SnowProfiler.MarkerNames[3]);
        DispatchAccumulate(cmd, groups);

        if (!WindShadowOff) DispatchWindShadow(cmd);
        // RÜZGÂR TAŞINIMI EŞİK KAPILI (spec §15.2).
        //
        // `KWindTransport` haç döşemesi yüzünden BEŞ dispatch; savrulacak
        // gevşek kar yokken hepsi boşuna koşuyordu. Eşik `DriftActive01` —
        // §18.1'in tetiğiyle aynı sayı, ikinci bir eşik tanımlanmıyor.
        float driftActive = SnowDriftVfxController.DriftActiveFor(
            env.WindSpeed, SnowRuntimeState.LooseSnowFraction);

        if (!WindTransportOff && driftActive > 0f)
            DispatchWindTransport(cmd, groups);

        // KALICILIK BİRİKMEDEN SONRA. Geri yüklenen blok en son bilinen
        // durumu taşıyor; birikme onun üstüne yazsaydı yükleme boşa giderdi.
        if (persistence != null) persistence.Dispatch(cmd);

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

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowDeltaTime, simDeltaTime);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.RimBlurTexels, SnowConstants.RimBlurTexels);

        // --- KDeform: batma ve dolma ---
        // ZEMİN DOKUSU ELLE BAĞLANIYOR. `Shader.SetGlobalTexture` compute
        // kernel'ine ulaşmıyor (bu projede ölçüldü, `DECISIONS.md`).
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.GroundHeightTex, ground);
        cmd.SetComputeBufferParam(simCompute, deformKernel, SnowShaderIDs.TrailSegments, trailSegmentBuffer);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.Trail, trail);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.TrailOut, trailTemp);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.SnowOut, snowTemp);
        cmd.DispatchCompute(simCompute, deformKernel, groups, groups, 1);

        (trail, trailTemp) = (trailTemp, trail);
        (snow, snowTemp) = (snowTemp, snow);

        // --- KRepose: duvarın duruş açısına göçmesi ---
        //
        // Koni tek geçişte bir teksel yayılıyor; kare başına birkaç geçişle
        // birkaç karede yerine oturuyor. Sonuç idempotent, o yüzden geçiş
        // sayısı görünümü değil YAKINSAMA HIZINI belirliyor.
        for (int i = 0; i < SnowConstants.ReposeIterations; i++)
        {
            cmd.SetComputeTextureParam(simCompute, reposeKernel, SnowShaderIDs.Trail, trail);
            cmd.SetComputeTextureParam(simCompute, reposeKernel, SnowShaderIDs.Snow, snow);
            cmd.SetComputeTextureParam(simCompute, reposeKernel, SnowShaderIDs.TrailOut, trailTemp);
            cmd.DispatchCompute(simCompute, reposeKernel, groups, groups, 1);

            (trail, trailTemp) = (trailTemp, trail);
        }

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

    /// Bölgeyi dünyanın güncel kar durumuyla baştan doldurur. Sınama
    /// geçersiz kılması değiştiğinde çağrılıyor.
    public void RefillRegion() => pendingClear = true;

    // ------------------------------------------------- dünyanın kar durumu

    /// DÜNYA ÇAPINDA SWE (m). Yüksek çözünürlüklü bölge oyuncunun çevresinde
    /// yalnız 24 m; kar ise BÜTÜN DAĞA yağıyor.
    ///
    /// Bu değer olmadan bölge dışı sabit `settings.DefaultSwe` (0) kalıyordu ve
    /// kar yalnız oyuncunun çevresindeki KARE alanda tutuyordu — kullanıcı
    /// ekran görüntüsüyle bildirdi. Bölge bir DETAY katmanı (ayak izi, sırt);
    /// karın kendisi dünyaya ait.
    ///
    /// Üç yerde birden kullanılıyor: bölge dışının görünümü (`_FallbackSWE`),
    /// kaydırmada açılan yeni şerit, ve bölge ilk doldurulduğunda. Üçü ayrı
    /// değer okusaydı oyuncu yürüdükçe kar kalınlığı basamak yapardı.
    public float WorldSwe { get; private set; } = -1f;

    /// Dünya karının normalize yoğunluğu. Bölgedeki oturma eğrisinin aynısı:
    /// taze karla başlıyor, 6 saatlik zaman sabitiyle oturmuş kara yaklaşıyor.
    public float WorldRhoN { get; private set; } = -1f;

    /// Oturmuş kuru karın normalize yoğunluğu — çekirdekteki `rhoTarget`
    /// (190 kg/m³) ile aynı sayı.
    const float WorldSettledRhoN = 0.28f;

    void TickWorldSnow(float dt)
    {
        if (WorldSwe < 0f)
        {
            WorldSwe = settings.DefaultSwe;
            WorldRhoN = settings.DefaultRhoN;
        }

        float fall = snowfall.SnowfallSweRate * dt;

        if (fall > 0f)
        {
            // Yeni kar taze; ortalama yoğunluk ağırlıklı harman — çekirdeğin
            // yaptığının aynısı.
            float taze = Mathf.InverseLerp(50f, 550f, 55f);
            float toplam = WorldSwe + fall;

            WorldRhoN = (WorldRhoN * WorldSwe + taze * fall) / Mathf.Max(toplam, 1e-9f);
            WorldSwe = Mathf.Min(toplam, SnowConstants.SweMax);
        }

        // Oturma: çekirdekteki `SNOW_SETTLE_TAU` ile aynı 6 saat.
        if (WorldRhoN < WorldSettledRhoN)
            WorldRhoN += (WorldSettledRhoN - WorldRhoN) * (1f - Mathf.Exp(-dt / 21600f));
    }

    /// Bölge dışının ve yeni açılan şeridin kar durumu.
    Vector4 WorldSnowValue => new Vector4(WorldSwe, WorldRhoN, 0f, 0f);

    // ------------------------------------------------------- sınama kancaları

    /// SİMÜLASYON ZAMAN ÇARPANI — yalnız sınama için.
    ///
    /// Birikme, oturma, erime ve kabuk saat mertebesinde işliyor; gerçek
    /// zamanda beklemek dakikalar alıyor. Çarpan `_DeltaTimeEff`'e giriyor,
    /// yani sahte durum yazmıyor: aynı fizik daha hızlı koşuyor.
    ///
    /// Çarpan 1 iken hiçbir şey değişmiyor; sınama dışında dokunulmuyor.
    public float SimTimeScale { get; set; } = 1f;

    /// Bölgeyi VERİLEN DERİNLİKTE karla doldurur (m). Bir sonraki karede
    /// uygulanıyor — komut tamponu zaten o karede kuruluyor.
    ///
    /// Derinlik SWE'ye yoğunluktan çevriliyor: `swe = derinlik × ρ / ρ_su`.
    /// Taze kar yoğunluğu kullanılıyor (55 kg/m³) ki "10 cm kar" dendiğinde
    /// ekranda 10 cm görünsün.
    public void FillSnowDepth(float metre)
    {
        const float TazeYogunluk = 55f;
        const float SuYogunlugu = 1000f;

        fillSwe = Mathf.Max(0f, metre) * TazeYogunluk / SuYogunlugu;
        fillRhoN = Mathf.InverseLerp(50f, 550f, TazeYogunluk);
        pendingFill = true;

        // DÜNYA DA DOLUYOR. Yalnız bölge doldurulsaydı sınama "kar sadece
        // çevremdeki karede" belirtisini kendi eliyle üretirdi.
        WorldSwe = fillSwe;
        WorldRhoN = fillRhoN;
    }

    bool pendingFill;
    float fillSwe;
    float fillRhoN;

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

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowDeltaTime, simDeltaTime);

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

        // YAĞIŞ ORANI COMPUTE PARAMETRESİ OLARAK YAZILIYOR, GLOBAL OLARAK DEĞİL.
        //
        // `SnowfallController` bunu `Shader.SetGlobalFloat` ile yayınlıyor ve
        // MALZEME shader'ları oradan okuyor. COMPUTE shader'lar global shader
        // değişkenlerini ALMIYOR; `_SnowfallSWERate` çekirdekte eski bir
        // değerde kalıyordu.
        //
        // Belirti: kar hiç tutmuyor. Ölçüldü — gerçekleşen birikme hızı
        // 1.06e-9 m/s, beklenen 1.39e-6 m/s, yani 1300 kat eksik. Sıfır
        // olmamasının sebebi editör sınamalarının (`SnowAccumulationTest`)
        // aynı compute asset'ine `sim.SetFloat` ile yazdığı değerin asset'te
        // KALMASI.
        //
        // BİRİM SINAMASININ GEÇİP OYUNUN ÇALIŞMAMASININ SEBEBİ DE BU: sınama
        // `sim.SetFloat` kullanıyor, oyun global kullanıyordu. İki ayrı yol.
        //
        // Yazma YUKARIDA, `KDeform`'dan önce (`WriteComputeParams`) — o çekirdek
        // de aynı değeri okuyor ve bu dispatch'ten ÖNCE koşuyor.

        // DÖŞEME DÖNDÜRMESİ (spec §15.2). Her karede dokunun 1/tiles'ı
        // işleniyor, dt aynı katla çarpılıyor. Kar oturması ve erimesi saat
        // mertebesinde; görsel fark yok.
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DeltaTimeEff,
                                 simDeltaTime * tiles * Mathf.Max(0f, SimTimeScale));
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
        float swe = 0f;
        float rhoN = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            swe += data[i].r;
            rhoN += data[i].g;
            coverage += data[i].b;
            loose += 1f - data[i].g;
        }

        float inv = 1f / Mathf.Max(1, data.Length);

        SnowRuntimeState.GroundCoverage01 = Mathf.Clamp01(coverage * inv);
        SnowRuntimeState.LooseSnowFraction = Mathf.Clamp01(loose * inv);

        // YAKIN BÖLGENİN DERİNLİĞİ. Kaskadınkiyle karşılaştırılıyor: ikisi
        // ayrı kernellerle yoğunluk geliştiriyor ve ayrışırlarsa devir
        // noktasında (±8 m) derinlik basamağı oluşuyor.
        MeanSwe = swe * inv;
        MeanRhoN = rhoN * inv;

        coverageMeasured = true;
    }

    // ------------------------------------------------------------ iz parçaları

    /// BÖLGEDEKİ DEFORMER'LARIN BU KARE SÜPÜRDÜĞÜ DOĞRU PARÇALARI.
    ///
    /// Yakalama kamerası, override materyali ve blur zincirinin yerini bu
    /// tampon aldı. Parça başına 32 bayt; tipik sahnede bir tane.
    ///
    /// ELEME KUTUSU PARÇANIN KENDİSİNDEN: nesnenin `Renderer.bounds`'u yok
    /// artık, sınır küreden türüyor. Bölgeye değmeyen parça yazılmıyor.
    void BuildTrailSegments(CommandBuffer cmd)
    {
        trailSegmentCount = 0;

        Vector2 center = AreaCenter;
        float half = settings.QualityData.AreaSize * 0.5f;

        for (int i = 0; i < SnowDeformerRegistry.Count && trailSegmentCount < MaxTrailSegments; i++)
        {
            SnowDeformer d = SnowDeformerRegistry.Get(i);
            if (d == null) continue;

            Vector3 a = d.SegmentA;
            Vector3 b = d.SegmentB;
            float r = d.Radius;

            // Parçanın XZ sınır kutusu bölgeyle kesişmiyorsa hiç yazma.
            float minX = Mathf.Min(a.x, b.x) - r, maxX = Mathf.Max(a.x, b.x) + r;
            float minZ = Mathf.Min(a.z, b.z) - r, maxZ = Mathf.Max(a.z, b.z) + r;

            if (maxX < center.x - half || minX > center.x + half) continue;
            if (maxZ < center.y - half || minZ > center.y + half) continue;

            int slot = trailSegmentCount * 2;
            trailSegmentData[slot]     = new Vector4(a.x, a.y, a.z, r);
            trailSegmentData[slot + 1] = new Vector4(b.x, b.y, b.z, 0f);

            trailSegmentCount++;

            // Sırt asimetrisi tek hızdan türüyor; birden çok deformer varsa
            // sonuncusununki kalır. Sırt zaten ikinci mertebe bir süsleme.
            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.TrailVelocityXZ,
                                      new Vector4(d.VelocityXZ.x, d.VelocityXZ.y, 0f, 0f));
        }

        if (trailSegmentCount == 0) return;

        trailSegmentBuffer ??= new ComputeBuffer(MaxTrailSegments * 2, TrailSegmentStride);
        trailSegmentBuffer.SetData(trailSegmentData, 0, 0, trailSegmentCount * 2);

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TrailSegmentCount, trailSegmentCount);
    }

    void ClearTo(CommandBuffer cmd, RenderTexture target, int groups, Vector4 value)
    {
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.ClearValue, value);
        cmd.SetComputeTextureParam(simCompute, clearKernel, SnowShaderIDs.Dst, target);
        cmd.DispatchCompute(simCompute, clearKernel, groups, groups, 1);
    }

    /// TEK KAYDIRMA KERNELİ (spec §6.4). Durum dokusu için ayrı bir kernel
    /// vardı; SWE kanalını kar çizgisi eğrisinden dolduruyordu. Çizgi kalkınca
    /// ikisi birebir aynı işi yapıyor — yeni şerit `_NewEdgeValue`'dan doluyor.
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

    /// SWE ve normalize yoğunluktan kar sütunu, metre. Shader'daki
    /// `SnowBaseHeight` ile aynı formül; ikisi ayrışırsa arazi ile mesh farklı
    /// kalınlık görür.
    /// Doku atanmamışsa global YAZILMIYOR: `null` yazmak shader'daki
    /// varsayılanı (beyaz/bump) da siler ve yüzey siyaha döner.
    static void Yayinla(string ad, Texture2D tex)
    {
        if (tex != null) Shader.SetGlobalTexture(ad, tex);
    }

    static float SnowBaseHeightMetre(float swe, float rhoN)
    {
        float rho = Mathf.Max(SnowConstants.RhoMin + rhoN * (SnowConstants.RhoMax - SnowConstants.RhoMin), 1f);
        return swe * SnowConstants.RhoWater / rho;
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
