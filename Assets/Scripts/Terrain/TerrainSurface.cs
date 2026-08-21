using System;
using UnityEngine;

/// Dağ yüzeyi materyalini sürer. Görünüm kararlarını vermez — onlar ayarlarda ve
/// shader'da. Buradaki tek iş, paylaşılan atmosfer durumunu materyale aktarmak.
[ExecuteAlways]
[RequireComponent(typeof(Terrain))]
public class TerrainSurface : MonoBehaviour
{
    [SerializeField] TerrainMaterialSettings settings;
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;
    [Tooltip("Kar kuşağının kotlarını buradan okur; ikinci bir yerde tanımlanmaz.")]
    [SerializeField] AltitudeWeatherDriver weatherDriver;
    [Tooltip("Erime sıcaklıktan sürülür; kar sıfırın altında ERİMEZ.")]
    [SerializeField] TemperatureField temperature;

    [Header("Kar mikro yüzeyleri (boş = detay kapalı)")]
    [Tooltip("Taze toz kar. Haritalar ve LUT'lar set içinde; TextureIngest üretiyor.")]
    [SerializeField] SurfaceMaterialSet snowPowder;
    [Tooltip("Rüzgârın sıkıştırdığı sert kar. Yönlü doku: UV rüzgâr eksenine döner.")]
    [SerializeField] SurfaceMaterialSet snowPacked;
    [SerializeField] Texture2D surfaceMaps;
    [Tooltip("Kar birikim ağırlığı. Hâkim rüzgâr yönüne göre pişiyor; "
             + "gölgelendirme, geometri ve çarpışma üçü de bunu okuyor.")]
    [SerializeField] Texture2D snowDriftWeight;
    [SerializeField] Texture2D groundNormals;
    [SerializeField] Texture2DArray horizon;
    [Tooltip("Arazi yüksekliği dokusu. Sis katmanları yerden yüksekliği buradan okuyor.")]
    [SerializeField] Texture2D terrainHeights;
    [SerializeField] Shader surfaceShader;

    static readonly int SurfaceMapsId = Shader.PropertyToID("_SurfaceMaps");
    static readonly int SnowDriftWeightId = Shader.PropertyToID("_SnowDriftWeight");
    static readonly int SurfaceMapsSizeId = Shader.PropertyToID("_SurfaceMapsSize");
    static readonly int GroundNormalsId = Shader.PropertyToID("_GroundNormals");
    static readonly int HorizonId = Shader.PropertyToID("_HorizonMap");
    static readonly int TerrainOriginId = Shader.PropertyToID("_TerrainOrigin");
    static readonly int TerrainSizeId = Shader.PropertyToID("_TerrainSize");

    static readonly int RockPrimaryId = Shader.PropertyToID("_RockPrimary");
    static readonly int RockSecondaryId = Shader.PropertyToID("_RockSecondary");
    static readonly int LowlandTintId = Shader.PropertyToID("_LowlandTint");
    static readonly int AlpineTintId = Shader.PropertyToID("_AlpineTint");
    static readonly int LichenColorId = Shader.PropertyToID("_LichenColor");
    static readonly int OxideColorId = Shader.PropertyToID("_OxideColor");
    static readonly int ScreeColorId = Shader.PropertyToID("_ScreeColor");
    static readonly int SnowColorId = Shader.PropertyToID("_SnowColor");

    static readonly int GrainScaleId = Shader.PropertyToID("_GrainScale");
    static readonly int GrainStrengthId = Shader.PropertyToID("_GrainStrength");
    static readonly int RockSmoothnessId = Shader.PropertyToID("_RockSmoothness");
    static readonly int BandThicknessId = Shader.PropertyToID("_BandThickness");
    static readonly int BandWarpId = Shader.PropertyToID("_BandWarp");
    static readonly int BandWarpScaleId = Shader.PropertyToID("_BandWarpScale");
    static readonly int BandContrastId = Shader.PropertyToID("_BandContrast");
    static readonly int LowlandCeilingId = Shader.PropertyToID("_LowlandCeiling");
    static readonly int AlpineFloorId = Shader.PropertyToID("_AlpineFloor");
    static readonly int AltitudeTintStrengthId = Shader.PropertyToID("_AltitudeTintStrength");
    static readonly int LichenAmountId = Shader.PropertyToID("_LichenAmount");
    static readonly int LichenCeilingId = Shader.PropertyToID("_LichenCeiling");
    static readonly int LichenMoistureBiasId = Shader.PropertyToID("_LichenMoistureBias");
    static readonly int LichenSunSensitivityId = Shader.PropertyToID("_LichenSunSensitivity");
    static readonly int OxideAmountId = Shader.PropertyToID("_OxideAmount");
    static readonly int OxideScaleId = Shader.PropertyToID("_OxideScale");
    static readonly int ScreeAmountId = Shader.PropertyToID("_ScreeAmount");
    static readonly int ScreeRangeId = Shader.PropertyToID("_ScreeRange");
    static readonly int ScreeSlopeLimitId = Shader.PropertyToID("_ScreeSlopeLimit");
    static readonly int SnowSlopeLimitId = Shader.PropertyToID("_SnowSlopeLimit");
    static readonly int SnowBreakupId = Shader.PropertyToID("_SnowBreakup");
    static readonly int PatternSeedId = Shader.PropertyToID("_PatternSeed");
    static readonly int SnowBurialId = Shader.PropertyToID("_SnowBurial");
    static readonly int SnowRoundingId = Shader.PropertyToID("_SnowRounding");
    static readonly int SnowDriftStrengthId = Shader.PropertyToID("_SnowDriftStrength");
    static readonly int SnowDriftCoverBiteId = Shader.PropertyToID("_SnowDriftCoverBite");
    static readonly int SnowDisplaceMaxId = Shader.PropertyToID("_SnowDisplaceMax");
    static readonly int SnowDisplaceStartId = Shader.PropertyToID("_SnowDisplaceStart");
    static readonly int SnowTessFactorId = Shader.PropertyToID("_SnowTessFactor");
    static readonly int SnowFootNearId = Shader.PropertyToID("_SnowFootNear");
    static readonly int SnowFootFarId = Shader.PropertyToID("_SnowFootFar");
    static readonly int SnowFootTessId = Shader.PropertyToID("_SnowFootTess");
    static readonly int SnowTessNearId = Shader.PropertyToID("_SnowTessNear");
    static readonly int SnowTessFarId = Shader.PropertyToID("_SnowTessFar");
    /// Yüzey başına altı doku. Son ekler shader'daki DECLARE_SURFACE_DETAIL
    /// makrosunun ürettikleriyle birebir; iki yerde ayrı yazılsaydı bir harita
    /// eklendiğinde biri sessizce boş kalırdı.
    static readonly string[] SurfaceMapSuffixes =
        { "Normal", "NormalLut", "Rough", "RoughLut", "Height", "HeightLut" };

    static readonly int SnowDetailScaleId = Shader.PropertyToID("_SnowDetailScale");
    static readonly int SnowDetailStrengthId = Shader.PropertyToID("_SnowDetailStrength");
    static readonly int SnowDetailRoughId = Shader.PropertyToID("_SnowDetailRough");
    static readonly int SnowDetailFadeId = Shader.PropertyToID("_SnowDetailFade");
    static readonly int SastrugiId = Shader.PropertyToID("_Sastrugi");
    static readonly int SnowSmoothnessId = Shader.PropertyToID("_SnowSmoothness");
    static readonly int SnowDepthScaleId = Shader.PropertyToID("_SnowDepthScale");
    static readonly int WetDarkeningId = Shader.PropertyToID("_WetDarkening");
    static readonly int WetSmoothnessId = Shader.PropertyToID("_WetSmoothness");
    static readonly int BumpStrengthId = Shader.PropertyToID("_BumpStrength");
    static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
    static readonly int CavityStrengthId = Shader.PropertyToID("_CavityStrength");

    static readonly int DawnColorId = Shader.PropertyToID("_SurfaceDawnColor");
    static readonly int DawnDirId = Shader.PropertyToID("_SurfaceDawnDir");
    static readonly int DawnStrengthId = Shader.PropertyToID("_SurfaceDawnStrength");
    static readonly int AlpenglowFacingId = Shader.PropertyToID("_AlpenglowFacing");

    static readonly int TerrainHeightMapId = Shader.PropertyToID("_TerrainHeightMap");
    static readonly int TerrainHeightAreaId = Shader.PropertyToID("_TerrainHeightArea");
    /// Atmosfer yazıyor, burada yalnız OKUNUYOR: rüzgâr eşiği geçildi mi ve ne kadar.
    /// Eşik kuralı atmosferin ayarında duruyor; burada ikinci kez kurmak iki sistemi
    /// ayırırdı. `PrecipitationRenderer` de aynı sebeple globalden okuyor.
    static readonly int SpindriftLiftId = Shader.PropertyToID("_SpindriftLift");

    static readonly int SnowProfileId = Shader.PropertyToID("_SnowProfile");
    static readonly int SnowProfileRangeId = Shader.PropertyToID("_SnowProfileRange");
    static readonly int PermanentSnowLineId = Shader.PropertyToID("_PermanentSnowLine");
    static readonly int PermanentSnowBandId = Shader.PropertyToID("_PermanentSnowBand");
    static readonly int SnowlineSunLiftId = Shader.PropertyToID("_SnowlineSunLift");
    static readonly int SnowlineGullyDropId = Shader.PropertyToID("_SnowlineGullyDrop");
    static readonly int SnowlineRaggedId = Shader.PropertyToID("_SnowlineRagged");
    static readonly int SnowfallFloorId = Shader.PropertyToID("_SnowfallFloor");
    static readonly int SnowfallCeilingId = Shader.PropertyToID("_SnowfallCeiling");
    static readonly int WetnessId = Shader.PropertyToID("_SurfaceWetness");
    static readonly int WindDirId = Shader.PropertyToID("_SurfaceWindDir");
    static readonly int SunDirId = Shader.PropertyToID("_SurfaceSunDir");

    Material material;
    int appliedRevision = -1;
    float wetness;
    // Kar birikimi KOT EKSENİNDE tutulur. Tek bir global sayıyla kar sınırı ne
    // inebiliyor ne çekilebiliyordu: dağın tamamı 90 saniyede beyazlayıp öyle
    // kalıyordu. Her bant kendi kotundaki havayla dolar ve kendi sıcaklığıyla erir.
    const int Bands = 128;

    readonly float[] bandCover = new float[Bands];
    readonly float[] bandPack = new float[Bands];

    /// Hava kilidinin son görülen değeri. Kilit değişince profil yeniden kuruluyor.
    float lastIntensityOverride = -1f;
    float lastSnowinessOverride = -1f;
    readonly Color[] bandPixels = new Color[Bands];

    /// Profilin yenilenme aralığı (saniye). Bakınız IntegrateSnowBands: bu aralıkta
    /// tek adım atmak kare kare atmakla aynı sayıyı veriyor. Çeyrek saniyede örtü
    /// binde üç değişiyor — göze görünmez, ama doku yüklemesi yirmide bire iniyor.
    /// Erimenin tam hıza ulaştığı sıcaklık (°C). Altında kareyle yavaşlar, sıfırın
    /// altında tamamen durur.
    const float MeltFullWarmth = 6f;

    const float ProfileUploadSeconds = 0.25f;

    float profileAge;
    Texture2D snowProfile;
    float profileFloor;
    float profileCeiling;

    /// Arazinin yatay genişliği (metre). Yüzey haritası UV'si buradan türüyor;
    /// her sorguda bileşen aranmasın diye saklanıyor.
    float terrainSpan;

    /// Genişlik materyal kurulumunda yazılıyor ama okuyanlar daha erken çalışabiliyor
    /// (rüzgâr sığınağı ilk karede soruyor). Sıfırsa arazinin kendi verisinden alınır.
    float TerrainSpan
    {
        get
        {
            if (terrainSpan <= 0f) terrainSpan = GetComponent<Terrain>().terrainData.size.x;
            return terrainSpan;
        }
    }

    public TerrainMaterialSettings Settings => settings;


    /// Verilen kottaki taze örtü, 0-1. Birikim artık tek bir sayı değil; sorulacak kot
    /// dışarıdan gelmeli. Yalnızca gösterge okuyor.
    public float SnowCoverAt(float altitude) => SampleBand(bandCover, altitude);

    /// Verilen kottaki kalınlık deposu, 0-1. Örtüden yavaş dolar, ondan hızlı boşalır.
    public float SnowPackAt(float altitude) => SampleBand(bandPack, altitude);

    /// Yüzey haritasının EĞİM kanalı (1 düz, 0 dik) verilen dünya konumunda.
    /// Kar derinliğinin CPU ikizi buradan okuyor; haritayı ve arazi sınırlarını bu
    /// bileşen tutuyor, dışarı ikinci bir kopya çıkarmıyor.
    ///
    /// Okuma bilinear ve mip 0 — shader tarafındaki `SampleSurfaceMapsFast` de öyle.
    public float SlopeAt(Vector3 worldPos)
    {
        Vector3 origin = transform.position;
        float span = Mathf.Max(1f, TerrainSpan);
        return surfaceMaps.GetPixelBilinear((worldPos.x - origin.x) / span,
                                            (worldPos.z - origin.z) / span).a;
    }

    /// Kar birikim ağırlığı verilen dünya konumunda, 0.67-2.0. 1 nötr.
    /// Çarpışma yüzeyinin CPU ikizi buradan okuyor — shader ile AYNI doku, aynı
    /// ara değerleme. Normalden ya da eğrilikten yeniden türetmek iki tarafı ayırırdı.
    public float DriftWeightAt(Vector3 worldPos)
    {
        Vector3 origin = transform.position;
        float span = Mathf.Max(1f, TerrainSpan);
        return snowDriftWeight.GetPixelBilinear((worldPos.x - origin.x) / span,
                                                (worldPos.z - origin.z) / span).r * 2f;
    }

    public void Bind(TerrainMaterialSettings source, WeatherState weatherState, WindField windField,
        TimeOfDay timeOfDay, AltitudeWeatherDriver driver, TemperatureField thermometer,
        Texture2D maps, Texture2D driftWeight,
        Texture2D normals, Texture2DArray horizonMap, Texture2D heightMap,
        SurfaceMaterialSet powderSet, SurfaceMaterialSet packedSet, Shader shader)
    {
        settings = source;
        weather = weatherState;
        wind = windField;
        time = timeOfDay;
        weatherDriver = driver;
        temperature = thermometer;
        surfaceMaps = maps;
        snowDriftWeight = driftWeight;
        groundNormals = normals;
        horizon = horizonMap;
        terrainHeights = heightMap;
        snowPowder = powderSet;
        snowPacked = packedSet;
        surfaceShader = shader;

        // Eski materyal YOK EDİLİYOR. Sadece referansı bırakmak sızıntıydı: kurulum
        // betiği her derlemede yeniden bağlıyor ve her seferinde bir materyal daha
        // sahipsiz kalıyordu. `hideFlags = DontSave` onları sahneden gizliyor, bellekten
        // değil.
        DestroyOwned(material);
        material = null;          // yeniden bağlanınca materyal de yenilensin
        appliedRevision = -1;     // yeni materyale ayarlar baştan yazılır
    }

    void OnDisable()
    {
        DestroyOwned(snowProfile);
        snowProfile = null;

        DestroyOwned(material);
        material = null;
    }

    /// Çalışma anında `Destroy`, editörde `DestroyImmediate`. Editörde `Destroy` bir
    /// sonraki kareye erteleniyor ve o kare hiç gelmeyebiliyor.
    static void DestroyOwned(UnityEngine.Object owned)
    {
        if (owned == null) return;

        if (Application.isPlaying) Destroy(owned);
        else DestroyImmediate(owned);
    }

    void Update()
    {
        EnsureMaterial();
        ApplySettings();

        float precipitation = weather != null ? weather.Precipitation : 0f;
        float snowiness = weather != null ? weather.Snowiness : 0f;

        // Islanma hızlı, kuruma yavaş: yağmur dininde kaya bir süre koyu kalır
        float target = precipitation * (1f - snowiness);
        float duration = target > wetness ? 8f : Mathf.Max(1f, settings.dryingSeconds);
        wetness = Mathf.Lerp(wetness, target, 1f - Mathf.Exp(-Time.deltaTime / duration));

        if (weatherDriver != null && temperature != null)
        {
            PublishSnowBands();
            IntegrateSnowBands();
        }

        material.SetFloat(WetnessId, wetness);

        // HÂKİM yön, anlık hız değil. Yüzeydeki birikinti ve sastrugi bu eksene
        // oturuyor; eksen esintiyle oynayınca desen dünyada sürükleniyordu (alan
        // `dot(worldXZ, windAxis)` üzerinden kuruluyor, bkz. WindField).
        Vector3 windDir = wind != null ? wind.PrevailingDirection : Vector3.right;
        // Şiddet w'de: sastrugi yalnızca yön değil güç de istiyor — dingin havada
        // yüzey taranmamış kalır, fırtınada çizgilenir.
        material.SetVector(WindDirId, new Vector4(windDir.x, windDir.y, windDir.z,
            wind != null ? wind.Strength : 0f));

        // Anlık güneş değil öğle güneşi: liken yıllık güneşlenmeye göre yerleşir,
        // gün içinde yanıp sönmez
        material.SetVector(SunDirId, time != null ? time.NoonSunDirection : Vector3.up);

        ApplyAlpenglow();
    }

    /// Kar kuşağının kotları. HER KARE yazılıyor: donma seviyesi hareketli, sınır
    /// fırtınada iniyor ısınmada çıkıyor. Ayarlardan gelen sabitlerle aynı yerde
    /// duruyordu ve onların "yalnız değişince yaz" kapısına takılıyordu.
    void PublishSnowBands()
    {
        Shader.SetGlobalFloat(SnowfallFloorId, weatherDriver.RainCeiling);
        Shader.SetGlobalFloat(SnowfallCeilingId, weatherDriver.SnowFloor);

        // Kalıcı kar çizgisi kar kuşağından TÜRER, ayrı bir sabit değildir. Sabitken
        // 1900±600 idi: zemin 1300 m'den beyazlamaya başlıyor, oysa yağış 2111 m'ye
        // kadar yağmur. Ayağının altında kar, tepende yağmur — aynı anda iki farklı
        // iklim. Şimdi çizgi her zaman kar kuşağının üstünde kalıyor.
        Shader.SetGlobalFloat(PermanentSnowLineId,
            weatherDriver.ReferenceSnowFloor + settings.permanentSnowRise);
    }

    /// Tohumdan üç eksende kaydırma. Aynı tohum → aynı yüzey; co-op'ta senkronlanacak
    /// bir şey yok çünkü paylaşılan durum yok.
    static Vector4 PatternOffset(int seed)
    {
        // Küçük tamsayı karıştırıcı; ardışık tohumlar ilişkisiz kaydırma versin diye.
        uint h = (uint)seed * 2654435761u;
        float x = (h & 0xFFu) * 2f;
        float y = ((h >> 8) & 0xFFu) * 2f;
        float z = ((h >> 16) & 0xFFu) * 2f;
        return new Vector4(x, y, z, 0f);
    }

    /// Her kot bandı kendi havasıyla dolar ve kendi sıcaklığıyla erir.
    ///
    /// İki depo, iki hız: örtü hızlı kapanır, kalınlık arkadan gelir — yağış başlayınca
    /// önce serpinti, sonra beyazlık, dolgunluk dakikalar sonra. Erirken ters sıra:
    /// kalınlık örtüden hızlı boşalır, kar önce incelir, sonra delinir, en son çıplak
    /// kalır. Tek depoyla ikisi aynı anda dolup boşalıyor ve birikme bir geçiş değil,
    /// açılıp kapanan bir boya gibi duruyordu.
    ///
    /// Biriktiren şey yağışın *şiddeti*, varlığı değil: hız şiddetle orantılı, tavan bir.
    /// İkili eşikle sürülünce çisenti düzeyinde sulu kar da tam sağanak da dağı aynı
    /// sürede sonuna kadar beyazlatıyordu.
    ///
    /// Birikme hızı bandın kotundaki yağıştan gelir — oyuncunun kotundakinden değil.
    /// Eskiden tek bir global sayı vardı ve onu oyuncunun bulunduğu yerin havası
    /// sürüyordu: 3000 metrede fırtınadayken dağın eteği de tam hızda kar biriktiriyordu.
    ///
    /// Erime hızı bandın donma seviyesine göre konumundan gelir: altında dakikalar,
    /// üstünde saatler. Kar sınırının fırtınadan sonra yukarı çekilmesi buradan çıkıyor.
    void IntegrateSnowBands()
    {
        EnsureProfile();

        // BİRİKEN SÜREYLE TEK ADIM. Dolum integrali dt'de doğrusal, erime üstel —
        // ikisi de birleşmeli, yani 50 milisaniyelik tek adım kare kare atmakla aynı
        // sayıyı veriyor. 128 bandın döngüsü ve dokunun yüklenmesi böylece kare başına
        // değil aralık başına bir kez oluyor.
        SyncWeatherOverride();

        profileAge += Time.deltaTime;
        if (profileAge < ProfileUploadSeconds) return;

        float dt = profileAge;
        profileAge = 0f;

        // SÜRÜKLENME KAYNAĞI TÜKETİR. Rüzgâr gevşek karı alıp götürüyor; yeni kar
        // yağmadıkça sürüklenecek bir şey kalmıyor. Perde bu olmadan sonsuza kadar
        // aynı şiddette akıyordu.
        //
        // Yalnız ÖRTÜ süpürülüyor, kalınlık deposu değil: rüzgâr gevşek üst tabakayı
        // alır, altındaki sıkışmış karı sıkıştırmaya devam eder.
        //
        // Basitleştirme: süpürülen kar burada YOK oluyor, başka banda taşınmıyor.
        // Gerçekte rüzgâr altına yığılır — o yeniden dağılım yüzeyde `lee` çarpanıyla
        // mekânsal olarak zaten var, kot ekseninde ikinci kez modellenmiyor.
        float lift = Mathf.Clamp01(Shader.GetGlobalFloat(SpindriftLiftId));
        float scour = lift * dt / Mathf.Max(1f, settings.snowScourSeconds);

        for (int i = 0; i < Bands; i++)
        {
            float altitude = BandAltitude(i);
            float falling = weatherDriver.SnowfallRateAt(altitude);

            if (falling > 0.001f)
            {
                bandCover[i] = Mathf.Min(1f, bandCover[i] +
                    falling * dt / Mathf.Max(1f, settings.snowAccumulationSeconds));
                bandPack[i] = Mathf.Min(1f, bandPack[i] +
                    falling * dt / Mathf.Max(1f, settings.snowPackSeconds));
            }
            else
            {
                // ERİME SICAKLIKTAN. Önceden karlılık oranından sürülüyordu: bu bir
                // sıcaklık VEKİLİYDİ ve sıfırın çok altındaki bandı bile "ılık" sayıp
                // eritebiliyordu. Kar sıfırın altında ERİMEZ — enerji yoksa faz değişimi
                // de yok. Dağın karı bu yüzden kalıcıdır.
                //
                // Sıfırın üstünde erime hızlanır ve derece başına ivmelenir: +1 °C'de
                // yavaş, +6 °C'de hızlı. Doğrusal değil çünkü erime ışınım, iletim ve
                // yoğuşmadan birden beslenir.
                float celsius = temperature.At(altitude);
                float warmth = Mathf.Clamp01(celsius / MeltFullWarmth);

                if (warmth > 0.001f)
                {
                    float coverMelt = settings.snowMeltWarmSeconds / (warmth * warmth);
                    float packMelt = settings.snowPackMeltWarmSeconds / (warmth * warmth);

                    bandCover[i] = Mathf.Lerp(bandCover[i], 0f,
                        1f - Mathf.Exp(-dt / Mathf.Max(1f, coverMelt)));
                    bandPack[i] = Mathf.Lerp(bandPack[i], 0f,
                        1f - Mathf.Exp(-dt / Mathf.Max(1f, packMelt)));
                }
                else
                {
                    // Sıfırın altında tek kayıp SÜBLİMASYON: kar erimeden buhara geçer.
                    // Çok yavaştır — bir koşu boyunca gözle görülmez, ama sıfır da
                    // değildir, yoksa kar sonsuza kadar birikirdi.
                    bandCover[i] = Mathf.Lerp(bandCover[i], 0f,
                        1f - Mathf.Exp(-dt / settings.snowSublimationSeconds));
                    bandPack[i] = Mathf.Lerp(bandPack[i], 0f,
                        1f - Mathf.Exp(-dt / settings.snowSublimationSeconds));
                }
            }

            bandCover[i] = Mathf.Max(0f, bandCover[i] - scour);

            bandPixels[i] = new Color(bandCover[i], bandPack[i], 0f, 0f);
        }

        // DOKU HER KARE YÜKLENMİYOR. Birikim integrali dt'de doğrusal, erime üstel —
        // ikisi de birleşmeli, yani biriken süreyle tek adım atmak kare kare atmakla
        // aynı sayıyı veriyor. Aradaki bekleme 60 fps'te 50 ms; örtü o sürede binde
        // altı değişiyor, yani göze görünmez.
        snowProfile.SetPixels(bandPixels);
        snowProfile.Apply(false);

        // Global: bkz. MountainSurfaceInput.hlsl. Materyal tamponuna yazılan hava
        // değerleri shader'a ulaşmıyordu.
        Shader.SetGlobalTexture(SnowProfileId, snowProfile);
        Shader.SetGlobalVector(SnowProfileRangeId,
            new Vector4(profileFloor, profileCeiling - profileFloor, 0f, 0f));
    }

    /// Kar profilini o anki havaya göre kurar.
    ///
    /// Kalıcı kar birikimin değil iklimin sonucudur: kar çizgisinin üstünde kar zaten
    /// vardır, oyun başladığı için birikmeye başlamaz.
    ///
    /// `SnowfallRateAt` KİLİTLERİ OKUYOR (`AltitudeWeatherDriver`), yani F1'den
    /// zorlanan hava burada da geçerli. Kar tutmasının ayrı bir yükseklik sınırı yok.
    ///
    /// `raiseOnly` — YALNIZ YÜKSELTİR, ASLA SİLMEZ.
    ///
    /// Bir dönem her çağrıda atama yapıyordu ve kilit değişimine bağlıydı: kullanıcı
    /// kar yağışını kapattığı an bantlar `max(0, 0)` ile sıfırlanıyor, biriken kar
    /// yok oluyordu. Belirti "kar yalnız yağarken var, dinince anında gidiyor" —
    /// ekran probu yağarken YEŞİL, dinince KIRMIZI gösterdi.
    ///
    /// Karı azaltan tek yol erimedir ve o zamanla işler. Hava kilidi kar EKLEYEBİLİR,
    /// var olanı kaldıramaz.
    void Prime(bool raiseOnly)
    {
        for (int i = 0; i < Bands; i++)
        {
            float altitude = BandAltitude(i);

            // Yağan kar tutar; yağmıyorsa iklimin bıraktığı kalıcı örtü kalır.
            float settled = Mathf.Max(weatherDriver.SnowfallRateAt(altitude),
                                      weatherDriver.SnowinessAt(altitude));

            bandCover[i] = raiseOnly ? Mathf.Max(bandCover[i], settled) : settled;
            bandPack[i] = raiseOnly ? Mathf.Max(bandPack[i], settled) : settled;
        }
    }

    /// Hava KİLİDİ değiştiğinde profil anında oturuyor, birikmesi beklenmiyor.
    ///
    /// Kilit bir ölçüm aracı: "yağış 1, kar 1" dendiğinde görülmek istenen şey o havanın
    /// SONUCU, kırk saniyelik bir geçiş değil. Doğal havada bu yol hiç çalışmıyor —
    /// kilit yokken iki değer de -1 kalıyor ve karşılaştırma hiç tetiklenmiyor.
    void SyncWeatherOverride()
    {
        float intensity = weatherDriver.IntensityOverride;
        float snowiness = weatherDriver.SnowinessOverride;

        if (Mathf.Approximately(intensity, lastIntensityOverride)
            && Mathf.Approximately(snowiness, lastSnowinessOverride)) return;

        lastIntensityOverride = intensity;
        lastSnowinessOverride = snowiness;

        if (snowProfile == null) return;

        // Yalnız yükseltir: kilit kar ekleyebilir, biriken karı silemez.
        Prime(raiseOnly: true);
    }

    // ---- TEŞHİS: KAR ZİNCİRİ ----
    //
    // "F1'de yağış 1 kar 1 yapıyorum, kar tutmuyor" belirtisi dört tur sürdü ve üç
    // şüphelim de yanlış çıktı. Zincirin hangi halkasında koptuğu ekrandan
    // anlaşılmıyor; her halka sayı olarak dışarı açılıyor.
    //
    // Belirti kapanınca bu blok ve F1'deki bölüm silinir.

    public float ProfileFloor => profileFloor;
    public float ProfileCeiling => profileCeiling;
    public int ProfileBands => Bands;

    public int BandIndexAt(float altitude) => Mathf.Clamp(
        Mathf.FloorToInt(Mathf.InverseLerp(profileFloor, profileCeiling, altitude) * Bands),
        0, Bands - 1);

    public float CoverAt(float altitude) => bandCover[BandIndexAt(altitude)];
    public float PackAt(float altitude) => bandPack[BandIndexAt(altitude)];

    /// Profil dokusunun O AN İÇİNDEKİ değer — CPU dizisi değil. İkisi ayrışıyorsa
    /// yükleme kopmuş demektir.
    public float UploadedCoverAt(float altitude)
    {
        if (snowProfile == null) return -1f;
        return snowProfile.GetPixel(BandIndexAt(altitude), 0).r;
    }

    /// Arazi kökeni. Dünya kotu ile profil kotu arasındaki fark buradan.
    public float TerrainOriginY => transform.position.y;

    public float SnowDisplaceMax => settings.snowDisplaceMax;
    public Vector3 PrevailingWind => wind != null ? wind.PrevailingDirection : Vector3.right;

    float BandAltitude(int index) =>
        Mathf.Lerp(profileFloor, profileCeiling, (index + 0.5f) / Bands);

    /// Bant okuması BİLİNEAR, en yakın değil. Shader aynı diziyi doku olarak bilinear
    /// örnekliyor; en yakın komşu yarım bant (birkaç metre kot) kayma bırakıyordu.
    /// Çarpışma yüzeyi bu sayıdan türediği için kayma doğrudan "kara gömülme"ye
    /// dönüşüyor — iki taraf aynı ara değerlemeyi kullanmak zorunda.
    float SampleBand(float[] band, float altitude)
    {
        float t = Mathf.Clamp01(Mathf.InverseLerp(profileFloor, profileCeiling, altitude));

        // Doku örnekleyicisinin texel merkezi düzeni: uv=t → x = t*Bands - 0.5
        float x = t * Bands - 0.5f;
        int i0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, Bands - 1);
        int i1 = Mathf.Clamp(i0 + 1, 0, Bands - 1);
        return Mathf.Lerp(band[i0], band[i1], Mathf.Clamp01(x - i0));
    }

    /// Profil dokusu 128x1: bilinear örneklendiği için bantlar arası geçiş sürekli.
    /// Bant başına 43 metre — kar sınırının hareketi bu çözünürlükte pürüzsüz okunuyor.
    void EnsureProfile()
    {
        // Kotlar arazinin kendi kökenine göre: shader de altitude'u böyle okuyor
        // (worldPos.y - _TerrainOrigin.y). İki taraf farklı sıfır kullanırsa profil
        // kayar.
        float origin = transform.position.y;
        float floor = weatherDriver.GroundAltitude - origin;
        float ceiling = weatherDriver.SummitAltitude - origin;

        if (snowProfile != null && Mathf.Approximately(floor, profileFloor)
            && Mathf.Approximately(ceiling, profileCeiling)) return;

        profileFloor = floor;
        profileCeiling = ceiling;

        // BAŞLANGIÇ DURUMU SIFIR DEĞİL, KUŞAKTAN TÜRÜYOR.
        //
        // Bir dönem `Array.Clear` idi: dağ çıplak doğuyor ve karını oyun sırasında
        // gerçek zamanda biriktiriyordu. Belirti kullanıcıdan geldi — "kar tutmuyor ki
        // arazide". Tutuyordu, ama sıfırdan başlayıp dakikalar sürüyordu ve 5700
        // metrelik bir dağın KALICI karı da o kuyruğa giriyordu.
        //
        // KAR TUTMASI İÇİN AYRI BİR YÜKSEKLİK SINIRI YOK. Tek koşul o kotta karın
        // YAĞIYOR olması; yağıyorsa tutar. Kot bağımlılığı zaten yağışın kendisinde
        // (yağmur mu kar mı) ve kilit varken o da devre dışı.
        //
        // Bir dönem başlangıç durumu `SnowinessAt`'ten okunuyordu, yani iklim
        // kuşağından. Kilit açıkken bile alçak kotları çıplak bırakıyordu: kullanıcı
        // F1'de yağış 1 / kar 1 yapıyor, dağ hâlâ çıplak duruyordu. `SnowfallRateAt`
        // kilitleri okuyor, `SnowinessAt` okumuyor — yanlış olan çağrıydı.
        Prime(raiseOnly: false);

        if (snowProfile != null) return;

        // RGBAFloat: `SetPixels` yalnızca belirli formatları yazıyor, ötekileri SESSİZCE
        // yok sayıyor. RGHalf denendi — doku oluştu, materyale bağlandı, içi sıfır kaldı
        // ve zemin hiç beyazlamadı. 128 texel, 2 KB; küçültmenin bir kazancı yok.
        snowProfile = new Texture2D(Bands, 1, TextureFormat.RGBAFloat, false, true)
        {
            name = "SnowProfile",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    /// Şafak ve batımda ufuktan gelen kızıl ışık. Rengi ve zamanlaması TimeOfDay'den
    /// gelir; ayrı bir zamanlayıcı kurulsaydı gökyüzüyle çelişirdi.
    void ApplyAlpenglow()
    {
        if (time == null)
        {
            material.SetFloat(DawnStrengthId, 0f);

            // Yön de yazılır: yazılmazsa materyalde en son ne kaldıysa o durur ve
            // bu yönü okuyan her şey (kristal pırıltısı kapısı, karın gece
            // matlaşması) gündüz sanır. Ufkun altı = kaynak yok.
            material.SetVector(DawnDirId, Vector3.down);
            return;
        }

        // Ufuk çarpanı zaten şafak ve batımda tepe yapıyor. Kareyle daraltmak
        // parlamayı o iki ana sıkıştırıyor; geniş bırakılırsa öğlene kadar sürüyor.
        float horizon = time.HorizonFactor * time.HorizonFactor;

        // Güneş ufkun altına indikçe kızıllık bir süre daha sürer, sonra biter
        // Pencere daraldı: aydınlanmanın sınırını artık Dünya'nın gölgesi çiziyor
        // (shader'da h ≈ R·θ²/2). Zirve ~2100 m ve gölge o kotu güneş −1.5°'deyken
        // (SunHeight ≈ −0.026) geçiyor; ondan sonra ortada aydınlanacak yüzey
        // kalmıyor. Eski −0.18 sınırı gece boyunca boşuna güç taşıyordu.
        float alive = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.05f, time.SunHeight));

        material.SetColor(DawnColorId, time.CurrentSunColor);
        material.SetVector(DawnDirId, time.SunDirection);
        material.SetFloat(DawnStrengthId, horizon * alive * settings.alpenglowStrength);
        material.SetFloat(AlpenglowFacingId, settings.alpenglowFacing);
    }

    /// Play mode'da yeniden derleme materyali düşürebilir; kullanım anında doğrulanır.
    /// Bağımlılık kontrolü de burada: ExecuteAlways bileşende OnEnable, bileşen sahneye
    /// eklendiği anda yani Bind'den önce çalışıyor.
    void EnsureMaterial()
    {
        if (material != null) return;

        if (settings == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: {nameof(settings)} atanmadı.");
        if (surfaceShader == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: {nameof(surfaceShader)} atanmadı.");
        if (surfaceMaps == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: yüzey haritaları atanmadı.");
        if (snowDriftWeight == null)
            throw new InvalidOperationException($"{nameof(TerrainSurface)}: birikim ağırlığı haritası atanmadı.");

        var terrain = GetComponent<Terrain>();
        appliedRevision = -1;
        material = new Material(surfaceShader) { hideFlags = HideFlags.DontSave };
        terrain.materialTemplate = material;

        material.SetTexture(SurfaceMapsId, surfaceMaps);
        material.SetTexture(SnowDriftWeightId, snowDriftWeight);
        material.SetVector(SurfaceMapsSizeId, new Vector4(surfaceMaps.width, surfaceMaps.height,
            1f / surfaceMaps.width, 1f / surfaceMaps.height));
        material.SetTexture(GroundNormalsId, groundNormals);
        material.SetTexture(HorizonId, horizon);
        material.SetVector(TerrainOriginId, transform.position);
        material.SetVector(TerrainSizeId, terrain.terrainData.size);
        terrainSpan = terrain.terrainData.size.x;

        // Arazi yüksekliği GLOBAL: sis ve gökyüzü de okuyor, yalnız yüzeyin ayarı değil.
        // xy köşe konumu, z genişlik, w yükseklik ölçeği — dokudaki 0-1 değer bununla
        // metreye çevriliyor, dönüşüm tek yerde duruyor.
        Vector3 size = terrain.terrainData.size;
        Shader.SetGlobalTexture(TerrainHeightMapId, terrainHeights);
        Shader.SetGlobalVector(TerrainHeightAreaId, new Vector4(
            transform.position.x, transform.position.z, size.x, size.y));
    }

    /// Tuner sürgüleri asset'i değiştiriyor; her karede okunması anlık geri bildirim verir.
    /// Ayar asset'inden gelen alanlar KENDİLİĞİNDEN DEĞİŞMEZ; yalnız elle ayar
    /// yapılınca değişir. Kırk küsur yazma her kare tekrarlanıyordu. `revision`
    /// karşılaştırması bunu bir kereye indiriyor — materyal yenilenirse ya da ayar
    /// değişirse yeniden gönderiliyor.
    /// Bir yüzeyin altı haritasını materyale yazar. Set yoksa hepsi boşaltılır —
    /// eski dokular materyalde asılı kalmasın.
    static void ApplySurfaceSet(Material material, string prefix, SurfaceMaterialSet set)
    {
        var maps = set == null
            ? new Texture2D[6]
            : new[] { set.normal, set.normalLut, set.roughness,
                      set.roughnessLut, set.height, set.heightLut };

        for (int i = 0; i < SurfaceMapSuffixes.Length; i++)
            material.SetTexture(prefix + SurfaceMapSuffixes[i], maps[i]);
    }

    void ApplySettings()
    {
        if (appliedRevision == settings.revision) return;
        appliedRevision = settings.revision;

        material.SetColor(RockPrimaryId, settings.rockPrimary);
        material.SetColor(RockSecondaryId, settings.rockSecondary);
        material.SetColor(LowlandTintId, settings.lowlandTint);
        material.SetColor(AlpineTintId, settings.alpineTint);
        material.SetColor(LichenColorId, settings.lichenColor);
        material.SetColor(OxideColorId, settings.oxideColor);
        material.SetColor(ScreeColorId, settings.screeColor);
        material.SetColor(SnowColorId, settings.snowColor);

        material.SetFloat(GrainScaleId, settings.grainScale);
        material.SetFloat(GrainStrengthId, settings.grainStrength);
        material.SetFloat(RockSmoothnessId, settings.rockSmoothness);
        material.SetFloat(BandThicknessId, settings.bandThickness);
        material.SetFloat(BandWarpId, settings.bandWarp);
        material.SetFloat(BandWarpScaleId, settings.bandWarpScale);
        material.SetFloat(BandContrastId, settings.bandContrast);
        material.SetFloat(LowlandCeilingId, settings.lowlandCeiling);
        material.SetFloat(AlpineFloorId, settings.alpineFloor);
        material.SetFloat(AltitudeTintStrengthId, settings.altitudeTintStrength);
        material.SetFloat(LichenAmountId, settings.lichenAmount);
        material.SetFloat(LichenCeilingId, settings.lichenCeiling);
        material.SetFloat(LichenMoistureBiasId, settings.lichenMoistureBias);
        material.SetFloat(LichenSunSensitivityId, settings.lichenSunSensitivity);
        material.SetFloat(OxideAmountId, settings.oxideAmount);
        material.SetFloat(OxideScaleId, settings.oxideScale);
        material.SetFloat(ScreeAmountId, settings.screeAmount);
        material.SetVector(ScreeRangeId, settings.screeRange);
        // TOHUM GLOBAL, materyal alanı DEĞİL: iki hash kökü iki ayrı dosyada ve biri
        // yer değiştirme geçişinde okunuyor. Materyale yazılırsa o geçiş göremez.
        //
        // Kaydırma tohumdan TÜRETİLİYOR, elle girilmiyor: üç eksen birbirinden bağımsız
        // ve 512'lik hash sarmasının içinde kalıyor (`MountainHash`'te `fmod(..., 512)`).
        Shader.SetGlobalVector(PatternSeedId, PatternOffset(settings.patternSeed));

        material.SetFloat(ScreeSlopeLimitId, settings.screeSlopeLimit);
        material.SetFloat(SnowSlopeLimitId, settings.snowSlopeLimit);
        material.SetFloat(SnowBreakupId, settings.snowBreakup);
        material.SetFloat(SnowBurialId, settings.snowBurial);
        material.SetFloat(SnowRoundingId, settings.snowRounding);
        material.SetFloat(SnowDriftStrengthId, settings.snowDriftStrength);
        material.SetFloat(SnowDriftCoverBiteId, settings.snowDriftCoverBite);
        material.SetFloat(SnowDisplaceMaxId, settings.snowDisplaceMax);
        material.SetFloat(SnowDisplaceStartId, settings.snowDisplaceStart);
        material.SetFloat(SnowTessFactorId, settings.snowTessFactor);
        material.SetFloat(SnowFootNearId, settings.snowFootNear);
        material.SetFloat(SnowFootFarId, settings.snowFootFar);
        material.SetFloat(SnowFootTessId, settings.snowFootTess);
        material.SetFloat(SnowTessNearId, settings.snowTessNear);
        material.SetFloat(SnowTessFarId, settings.snowTessFar);

        // Mikro doku: yalnız kabartma, pürüzlülük ve yükseklik taşıyor. Renk
        // taşımıyor — karın rengi kar sistemine bağlı.
        ApplySurfaceSet(material, "_SnowPowder", snowPowder);
        ApplySurfaceSet(material, "_SnowPacked", snowPacked);

        bool hasDetail = snowPowder != null && snowPowder.IsComplete
                      && snowPacked != null && snowPacked.IsComplete;

        material.SetFloat(SnowDetailScaleId, 1f / Mathf.Max(0.01f, settings.snowDetailTiling));
        material.SetFloat(SnowDetailStrengthId, hasDetail ? settings.snowDetailStrength : 0f);
        material.SetFloat(SnowDetailRoughId, settings.snowDetailRoughness);
        material.SetFloat(SnowDetailFadeId, settings.snowDetailFade);
        material.SetFloat(SastrugiId, settings.sastrugi);
        material.SetFloat(SnowSmoothnessId, settings.snowSmoothness);
        material.SetFloat(SnowDepthScaleId, settings.snowDepthScale);
        material.SetFloat(SnowlineSunLiftId, settings.snowlineSunLift);
        material.SetFloat(SnowlineGullyDropId, settings.snowlineGullyDrop);
        material.SetFloat(SnowlineRaggedId, settings.snowlineRagged);
        material.SetFloat(PermanentSnowBandId, settings.permanentSnowBand);

        material.SetFloat(WetDarkeningId, settings.wetDarkening);
        material.SetFloat(WetSmoothnessId, settings.wetSmoothness);
        material.SetFloat(BumpStrengthId, settings.bumpStrength);
        material.SetFloat(BumpScaleId, settings.bumpScale);
        material.SetFloat(CavityStrengthId, settings.cavityStrength);
    }
}
