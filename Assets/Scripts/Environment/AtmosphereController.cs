using UnityEngine;
using UnityEngine.Rendering.Universal;

/// Sis, ortam ışığı ve gökyüzünü tek bir renkten türetir.
/// Ayrı ayrı ayarlanınca ufukta keskin sınır ve "boyanmış duvar" hissi oluşuyordu.
///
/// Havayı, rüzgârı ve saati okur; ses ve renk düzenlemesi de aynı kaynakları okuduğu için
/// üçü çelişmez.
[ExecuteAlways]
public class AtmosphereController : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;
    [Tooltip("Yalnızca açık pencere sinyali için. Kapsamanın kalıcı tabanını delebilen " +
             "tek şey o; başka hiçbir değer buradan okunmaz.")]
    [SerializeField] AltitudeWeatherDriver weatherDriver;
    [SerializeField] Camera view;

    [Tooltip("Görünüm ayarlarının tamamı. Bileşenin üstünde durdukları sürece değerin " +
             "sahnede ikinci bir kopyası oluyor ve Unity sahneyi kendi belleğinden diske " +
             "yeniden yazdığında koddan yapılan düzeltme sessizce kayboluyordu.")]
    [SerializeField] AtmosphereSettings settings;

    [Tooltip("Perlin-Worley kütle dokusu. Bulut geçişi ayrı materyal kullandığı için " +
             "global olarak yayınlanır.")]
    [SerializeField] Texture3D baseNoise;
    [Tooltip("Worley aşındırma dokusu.")]
    [SerializeField] Texture3D detailNoise;
    [Tooltip("2B hava haritası (CloudWeatherMapBaker pişirir): kapsama, tip, taban " +
             "kayması ve eğim garantili tavan. Bulut dağılımının tek kaynağı.")]
    [SerializeField] Texture2D weatherMap;
    [Tooltip("Kaba maksimum-kapsama haritası: ışın yürüyüşü boş gökte bunu okuyup " +
             "büyük adımlarla atlar. Görüntüyü etkilemez, yalnız hızlandırır.")]
    [SerializeField] Texture2D skipMap;
    [Tooltip("Iraksamasız curl gürültüsü: aşındırmanın koordinatını büker (türbülans).")]
    [SerializeField] Texture2D curlNoise;
    [Tooltip("Yüksek irtifa katmanı dokusu: sirrus / altokümülüs / altostratus.")]
    [SerializeField] Texture2D highNoise;
    [SerializeField] Material skyMaterial;

    static readonly int BaseNoiseId = Shader.PropertyToID("_BaseNoise");
    static readonly int DetailNoiseId = Shader.PropertyToID("_DetailNoise");
    static readonly int BaseNoiseTexelsId = Shader.PropertyToID("_BaseNoiseTexels");
    static readonly int DetailNoiseTexelsId = Shader.PropertyToID("_DetailNoiseTexels");
    static readonly int HeightFogColorId = Shader.PropertyToID("_HeightFogColor");
    static readonly int HeightFogDensityId = Shader.PropertyToID("_HeightFogDensity");
    static readonly int HeightFogFalloffId = Shader.PropertyToID("_HeightFogFalloff");
    static readonly int HeightFogBaseId = Shader.PropertyToID("_HeightFogBase");
    static readonly int FogSeaDensityId = Shader.PropertyToID("_FogSeaDensity");
    static readonly int FogSeaFalloffId = Shader.PropertyToID("_FogSeaFalloff");
    static readonly int FogInversionHeightId = Shader.PropertyToID("_FogInversionHeight");
    static readonly int FogInversionWidthId = Shader.PropertyToID("_FogInversionWidth");
    static readonly int FogFreeDensityId = Shader.PropertyToID("_FogFreeDensity");
    static readonly int SpindriftDensityId = Shader.PropertyToID("_SpindriftDensity");
    static readonly int SpindriftFalloffId = Shader.PropertyToID("_SpindriftFalloff");
    static readonly int SpindriftBrightnessId = Shader.PropertyToID("_SpindriftBrightness");

    /// Ham kaldırma payı (0-1), yoğunlukla çarpılmamış hâli. Yüzey bunu okuyup yerdeki
    /// karı süpürüyor: eşik kuralı burada duruyor, orada ikinci kez kurulmasın diye.
    static readonly int SpindriftLiftId = Shader.PropertyToID("_SpindriftLift");
    static readonly int SpindriftMaxDepthId = Shader.PropertyToID("_SpindriftMaxDepth");
    static readonly int SpindriftCrestId = Shader.PropertyToID("_SpindriftCrest");
    static readonly int SpindriftDriftId = Shader.PropertyToID("_SpindriftDrift");
    static readonly int SpindriftWindId = Shader.PropertyToID("_SpindriftWind");

    /// HAM rüzgâr: yön × hız (m/s), w ani esinti. `_SpindriftWind`'den AYRI çünkü o
    /// yalnız kar için ve CPU'da savrulma eşiği uygulanmış hâli — kar kalkmayan hafif
    /// esintide sıfır. Bitki örtüsü ham rüzgârı okuyor: yapraklar o esintide de kıpırdar.
    static readonly int WindVectorId = Shader.PropertyToID("_WindVector");
    static readonly int FogFreeFalloffId = Shader.PropertyToID("_FogFreeFalloff");
    static readonly int FogBankDriftId = Shader.PropertyToID("_FogBankDrift");
    static readonly int FogBankStrengthId = Shader.PropertyToID("_FogBankStrength");
    static readonly int HeightFogShadowColorId = Shader.PropertyToID("_HeightFogShadowColor");
    static readonly int HeightFogZenithId = Shader.PropertyToID("_HeightFogZenith");
    static readonly int HeightFogSunColorId = Shader.PropertyToID("_HeightFogSunColor");
    static readonly int HeightFogChromaId = Shader.PropertyToID("_HeightFogChroma");
    static readonly int CloudBrightId = Shader.PropertyToID("_CloudBrightColor");
    static readonly int CloudDarkId = Shader.PropertyToID("_CloudDarkColor");
    static readonly int CloudSunColorId = Shader.PropertyToID("_CloudSunColor");
    static readonly int CloudHazeDistanceId = Shader.PropertyToID("_CloudHazeDistance");
    static readonly int CloudRimStrengthId = Shader.PropertyToID("_CloudRimStrength");
    static readonly int CloudPowderStrengthId = Shader.PropertyToID("_CloudPowderStrength");
    static readonly int CloudRainAbsorbId = Shader.PropertyToID("_CloudRainAbsorb");
    static readonly int CloudAmbientId = Shader.PropertyToID("_CloudAmbient");
    static readonly int CloudAmbientFloorId = Shader.PropertyToID("_CloudAmbientFloor");
    static readonly int CloudMassWarmthId = Shader.PropertyToID("_CloudMassWarmth");
    static readonly int CloudMassBrightnessId = Shader.PropertyToID("_CloudMassBrightness");
    static readonly int CloudLightReachId = Shader.PropertyToID("_CloudLightReach");
    static readonly int CloudMultiScatterId = Shader.PropertyToID("_CloudMultiScatter");
    static readonly int CloudDuskTintId = Shader.PropertyToID("_CloudDuskTint");
    static readonly int CloudDuskStrengthId = Shader.PropertyToID("_CloudDuskStrength");
    static readonly int CloudMoonColorId = Shader.PropertyToID("_CloudMoonColor");
    static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
    static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    static readonly int MoonColorId = Shader.PropertyToID("_MoonColor");
    static readonly int MoonDirectionId = Shader.PropertyToID("_MoonDirection");
    static readonly int CloudWindId = Shader.PropertyToID("_CloudWind");
    static readonly int CloudShearOffsetId = Shader.PropertyToID("_CloudShearOffset");
    static readonly int CloudShearTurnId = Shader.PropertyToID("_CloudShearTurn");
    static readonly int CloudRiseId = Shader.PropertyToID("_CloudRise");
    static readonly int CoverageId = Shader.PropertyToID("_Coverage");
    static readonly int CloudBottomId = Shader.PropertyToID("_CloudBottom");
    static readonly int CloudTopId = Shader.PropertyToID("_CloudTop");
    static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");
    static readonly int CloudScaleId = Shader.PropertyToID("_CloudScale");
    static readonly int DetailScaleId = Shader.PropertyToID("_DetailScale");
    static readonly int DetailStrengthId = Shader.PropertyToID("_DetailStrength");
    static readonly int ShearAmountId = Shader.PropertyToID("_ShearAmount");
    static readonly int LargeWeightId = Shader.PropertyToID("_CloudLargeWeight");

    static readonly int WeatherMapId = Shader.PropertyToID("_WeatherMap");
    static readonly int WeatherMapScaleId = Shader.PropertyToID("_WeatherMapScale");
    static readonly int WeatherMapTexelsId = Shader.PropertyToID("_WeatherMapTexels");
    static readonly int SkipMapId = Shader.PropertyToID("_CloudSkipMap");
    static readonly int CurlNoiseId = Shader.PropertyToID("_CloudCurlNoise");
    static readonly int HighNoiseId = Shader.PropertyToID("_CloudHighNoise");
    static readonly int HighAmountId = Shader.PropertyToID("_HighCloudAmount");
    static readonly int HighTypeId = Shader.PropertyToID("_HighCloudType");
    static readonly int HighAltitudeId = Shader.PropertyToID("_HighCloudAltitude");
    static readonly int HighScaleId = Shader.PropertyToID("_HighCloudScale");
    static readonly int CurlStrengthId = Shader.PropertyToID("_CloudCurlStrength");
    static readonly int EvolutionId = Shader.PropertyToID("_Evolution");
    static readonly int DetailDistanceId = Shader.PropertyToID("_DetailDistance");
    static readonly int CloudDitherId = Shader.PropertyToID("_CloudDither");
    static readonly int CloudEdgeSoftenId = Shader.PropertyToID("_CloudEdgeSoften");
    static readonly int StepGrowthId = Shader.PropertyToID("_CloudStepDouble");

    static readonly int DensityScaleId = Shader.PropertyToID("_DensityScale");
    static readonly int StepsId = Shader.PropertyToID("_CloudSteps");
    static readonly int LightStepsId = Shader.PropertyToID("_CloudLightSteps");
    static readonly int StarStrengthId = Shader.PropertyToID("_StarStrength");

    float visibility;

    /// Sisin GERÇEK yoğunluğuna denk düşen görüş. `visibility` havanın hedefi;
    /// vadi çarpanı ve şafak denizi tabanı onun üstüne biniyor, dolayısıyla ekranda
    /// görülen ve bulutların karıştığı mesafe budur.
    float effectiveVisibility;

    /// Yalnız YERLEŞİK havanın görüşü — vadi sis denizi hariç. Bulut menzili bunu
    /// kullanır: bulut 2.6 km yukarıda, 120 m derinliğindeki deniz onu ilgilendirmez.
    float settledVisibility;
    Color color;
    const float EditorApplyInterval = 0.1f;

    Vector2 cloudOffset;
    Vector3 fogDrift;
    float activeCloudBottom;
    float airThinning = 1f;
    Color shadowColor;

    /// Bulutların okuduğu gök tonları. Yalnız RENK taşırlar; parlaklık taban renkten
    /// gelir, çünkü radyans ile ışınım arasında π kat fark var ve doğrudan kullanmak
    /// bulutları karartıyor.
    Color skyBright = Color.white, skyShade = Color.gray;

    Color zenith, targetZenith;
    Vector3 smoothedHeading = Vector3.right;
    float smoothedDrift;
    float nextEditorApply;
    float evolution;
    float convectiveRise;
    float localRain = 1f;
    Vector2 spindriftDrift;
    float appliedShadowDistance = -1f;
    float coverage;
    bool initialized;

    /// Test için yükseklik sisini kapatır; arazi açıkta görünür. Bulutların kendi hava
    /// perspektifi ayrı bir mekanizma ve bundan etkilenmez — ikisini tek anahtara bağlamak
    /// "sisi kapattım, sorun devam ediyor" gibi yanlış bir kanıt üretir.
    public bool FogEnabled { get; set; } = true;

    /// Test için bulut kapsamasını havadan bağımsız sabitler.
    public bool CoverageLocked { get; set; }
    public float LockedCoverage { get; set; } = 0.5f;


    /// Teşhis paneli canlı ürettiği haritayı buradan takar; global doku bir sonraki
    /// Apply'da yeniden yayınlanır. Kalıcılık asset pişirmesinin işi.
    public void SetWeatherMap(Texture2D map) => weatherMap = map;

    /// Oyuncunun ÜSTÜNDEKİ kolonun yağış payı, 0-1. Yağış artık gökten tek parça
    /// düşmüyor: yağmuru düşüren şey belirli bulutlardır, bir bulutun altındayken
    /// yağar, açıklığa çıkınca diner. Hava haritasından okunur (kapsama × kabarıklık),
    /// yavaş yumuşatılır — bulut kenarından geçerken yağmur açılıp kapanmaz, kısalır.
    public float LocalRain => localRain;




    public float Visibility => effectiveVisibility > 0f ? effectiveVisibility : visibility;

    public float Coverage => coverage;


    /// Hata ayıklama paneli ayarları canlı değiştirebilsin diye açık.
    public AtmosphereSettings Settings => settings;

    /// Bulut katmanının o anki tabanı (metre). Sakin havada iner, yağış ve rüzgâr
    /// yükseltir; dakikalar ölçeğinde yer değiştirdiği için gözle takip edilemiyor.
    public float CloudBottom => activeCloudBottom;

    /// Katmanın tavanı. Taban havayla oynadığı için değişken, tavan ayarın kendisi.
    public float CloudTop => settings.cloudTop;

    public void Bind(AtmosphereSettings source, WeatherState weatherState, WindField windField,
        TimeOfDay timeOfDay, AltitudeWeatherDriver driver, Camera camera, Material sky,
        Texture3D shapeNoise, Texture3D erosionNoise, Texture2D weatherMapTexture,
        Texture2D skipMapTexture, Texture2D curlNoiseTexture, Texture2D highNoiseTexture)
    {
        settings = source;
        weather = weatherState;
        wind = windField;
        time = timeOfDay;
        weatherDriver = driver;
        view = camera;
        skyMaterial = sky;
        baseNoise = shapeNoise;
        detailNoise = erosionNoise;
        weatherMap = weatherMapTexture;
        skipMap = skipMapTexture;
        curlNoise = curlNoiseTexture;
        highNoise = highNoiseTexture;

        Initialize();
    }

    /// ExecuteAlways yüzünden OnEnable, AddComponent anında çalışır — o an Bind henüz
    /// çağrılmamış olabilir.
    void OnEnable() => Initialize();

    /// Bulutların o anki süzülme hızı (m/s). Teşhis içindir: rüzgâr sıfırlanınca da
    /// hareket ediyorlarsa sebebin taban hız mı yoksa rüzgâr mı olduğu ancak bu sayıyla
    /// ayrılıyor.
    public float CloudSpeed => smoothedDrift;

    /// Yansıma haritasının en son hangi gökyüzünde pişirildiği. Gökyüzü sürekli
    /// değişiyor ama harita her karede pişirilemez — pişirme milisaniyeler yiyor.
    Color reflectionSky = new Color(-1f, -1f, -1f);

    void Initialize()
    {
        if (settings == null || weather == null || wind == null || time == null
            || weatherDriver == null) return;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        if (skyMaterial != null) RenderSettings.skybox = skyMaterial;

        initialized = false;
        Apply();
    }

    /// İki gök rengi arasındaki fark verilen eşiği geçiyor mu. Kanal kanal bakılıyor:
    /// toplam parlaklık aynı kalırken renk kayabiliyor (şafakta kızıl, fırtınada gri) ve
    /// yansıma o kaymayı da taşımalı.
    static bool ColourMoved(Color a, Color b, float threshold) =>
        Mathf.Abs(a.r - b.r) > threshold
        || Mathf.Abs(a.g - b.g) > threshold
        || Mathf.Abs(a.b - b.b) > threshold;

    void Update()
    {
        // Edit mode'da her kare tam hesap gereksiz: sahne durağan, yalnızca gökyüzünün
        // güncel görünmesi yetiyor. ExecuteAlways bunu kısıtlamasız bırakırsa editör
        // boşuna yükleniyor.
        if (!Application.isPlaying)
        {
            if (Time.realtimeSinceStartup < nextEditorApply) return;
            nextEditorApply = Time.realtimeSinceStartup + EditorApplyInterval;
        }

        Apply();
    }

    void Apply()
    {
        if (settings == null || weather == null || wind == null || time == null
            || weatherDriver == null) return;

        float precipitation = weather.Precipitation;
        float snowiness = weather.Snowiness;
        float day = time.DayFactor;



        // Bulut denizi sakin havada oluşur: soğuk hava vadiye çöker ve üstünde durgun
        // bir tavan bulur. Rüzgâr o havayı karıştırıp inversiyonu dağıtır, yağış ise
        // nemi yukarı taşır — ikisi de tabanı yükseltir.
        // Tabanın yeri de katmanın kendi durumu: sönmüş yağışa bağlanınca oyuncu denizin
        // üstüne çıktığı anda deniz de alçalmaya başlıyordu.
        float calm = (1f - weatherDriver.CloudMass) * (1f - wind.Strength);
        float targetBottom = Mathf.Lerp(settings.cloudBottom, settings.calmCloudBottom, calm);

        // Kütle ağırdır, esintiyle inip kalkmaz. Rüzgâr şiddeti sekiz saniyelik
        // esintilerle oynadığı için doğrudan bağlanırsa katman zıplar; taban kendi
        // ağırlığıyla, dakikalar ölçeğinde yer değiştirir.
        if (!initialized) activeCloudBottom = targetBottom;
        else
            activeCloudBottom = Mathf.Lerp(activeCloudBottom, targetBottom,
                1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1f, settings.cloudBottomSmoothing)));

        // Yağış tipine göre hedef görüş; kar yağmurdan çok daha kapatıcı
        float wet = Mathf.Lerp(settings.rainVisibility, settings.snowVisibility, snowiness);
        float targetVisibility = Mathf.Lerp(settings.clearVisibility, wet, precipitation);

        // Rüzgâr savurdukça görüş daha da kapanır — tipinin asıl etkisi budur.
        // Yalnızca yağış varken anlamlı: açık havada rüzgâr görüşü kapatmaz.
        float closure = wind.Strength * settings.windClosure * precipitation;
        targetVisibility *= 1f - closure;

        // Sis bankları rüzgârla sürüklenir; yüzey sürtünmesi yüzünden bulut kadar
        // hızlı taşınmazlar. Sarma yok — gerekçe bulut kaymasıyla aynı.
        fogDrift += wind.Velocity * (0.6f * Time.deltaTime);

        // Kameranın bulunduğu yerdeki bank değeri. Kuşak yamaları ve görüş nefesi
        // CPU'dan, mekânsal desen GPU'dan aynı alanı okur — iki tüketici, tek alan.
        Vector2 camXZ = view != null
            ? new Vector2(view.transform.position.x, view.transform.position.z)
            : Vector2.zero;
        float bank = BankField(camXZ - new Vector2(fogDrift.x, fogDrift.z));

        // Görüş nefesi: dakikalar ölçeğinde salınım. Aynı fırtınada sis epizotlar
        // hâlinde kalınlaşıp seyrelir; sabit görüş "boyanmış hava" gibi duruyordu.
        targetVisibility *= 1f + (Mathf.PerlinNoise(Time.time * 0.008f, 53f) * 2f - 1f)
                                 * settings.visibilityBreathing;

        // Bulut kuşağı: dağın yamacına oturan bulutların içinden geçerken görüş kapanır.
        // Gerçekte de tırmanırken bulutun içine girilir, üstünde açık havaya çıkılır.
        // Kuşağın içi tekdüze çorba değil: bank aralandığında görüş açılır, yamaç bir
        // görünür bir kaybolur. İçeriden mekânsal yapı zaten seçilemediği için yamalar
        // CPU'dan, zamanla gelir — GPU maliyeti sıfır.
        float deckClose = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.5f, bank));
        float deckVis = Mathf.Lerp(settings.deckOpenVisibility, settings.deckVisibility, deckClose);

        float deck = DeckAmount(precipitation);
        targetVisibility = Mathf.Lerp(targetVisibility, deckVis, deck);

        // Gece sis kararır; renk hava tipiyle birlikte seçilir
        Color dayColor = Blend(settings.clearDay, settings.rainDay, settings.snowDay, precipitation, snowiness);
        Color nightColor = Blend(settings.clearNight, settings.rainNight, settings.snowNight, precipitation, snowiness);
        Color targetColor = Color.Lerp(nightColor, dayColor, day);

        // Şafak ve gün batımı: güneş ufuktayken atmosfer kızıllaşır.
        // Kapalı havada bu sıcaklık solar — bulutun ardından süzülür.
        // Şafak tonu artık elle seçilmiyor: TimeOfDay ışığın atmosferden nasıl süzüldüğünü
        // hesaplıyor, sis de bulut da aynı renkten besleniyor. Ayrı sabitler tutmak
        // gökyüzü kızarırken sisin soluk kalmasına yol açıyordu.
        Color dusk = Color.Lerp(time.CurrentSunColor, settings.duskOvercast, precipitation * 0.7f);
        // Gündüz çarpanı buradan kaldırıldı: şafakta DayFactor daha 0.25 olduğu için
        // kızıllığı yarıya kısıyor ve taban renk gece mavisinde kalıyordu. Güneş ufkun
        // yakınında değilken HorizonFactor zaten sıfırlanıyor, gece kızıllık kendiliğinden
        // kapanıyor — ikinci bir kısıtlamaya gerek yok.
        float duskMask = time.HorizonFactor * settings.duskStrength;

        // Güneşin karşı tarafı: Dünya'nın gölgesi ufuktan yükselir, orası soğuk kalır.
        // Kızıllığın uygulanmadığı hâl bu, gökyüzü shader'ı ikisi arasında yön çarpanıyla
        // geçiş yapıyor.
        shadowColor = Color.Lerp(targetColor, nightColor, duskMask * 0.65f);

        // Şafak yalnızca ufku boyar; gökyüzünün tepesi gecenin mavisinde kalır. İkisini
        // birlikte kızartmak kontrastı öldürüyor: her yer aynı tonda olunca turuncu bir
        // renk olarak değil, soluk bir zemin olarak okunuyor. Turuncuyu çarpıcı yapan
        // şey o maviyle yan yana durması.
        targetZenith = Color.Lerp(targetColor * 0.55f, targetColor, precipitation);

        // Taban hava rengine kızılın yarısı verilir: tamamı verilince sahnenin bütün
        // havası bordo bir sosa batıyordu. Dramın kalanı yöne bağlı paletin işi
        // (AirColor): güneş tarafı altın-kızıl yanar, taban mütevazı kalır.
        // Katsayı Python simülasyonundan (dusk_palette_sim.py, "canlı" varyantı).
        targetColor = Color.Lerp(targetColor, dusk, duskMask * 0.55f);

        if (!initialized)
        {
            visibility = targetVisibility;
            color = targetColor;
            zenith = targetZenith;
            initialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, settings.transitionSeconds));
            visibility = Mathf.Lerp(visibility, targetVisibility, t);
            color = Color.Lerp(color, targetColor, t);
            zenith = Color.Lerp(zenith, targetZenith, t);
        }

        // Unity'nin sisi hiç kullanılmıyor: yükseklikten bağımsız olduğu için zirvede de
        // etekte de aynı yoğunlukta çıkıyor. Yükseklik sisi HeightFog.hlsl'de, yüzeyin
        // kendi hesabı olarak duruyor.
        ApplyHeightFog();

        // Gökyüzü ve ortam ışığı aynı renkten türer: ufukta sınır kalmaz,
        // nesneler içinde bulundukları havayla aynı ışığı alır
        RenderSettings.ambientLight = color * settings.ambientStrength;

        // YANSIMA İKİ ADIMDA. Harita gökyüzünden pişiyor ve gökyüzü rengi kaydığında
        // yenileniyor — gece, fırtına, bulut denizi, şafak, hepsi aynı kapıdan.
        //
        // ŞİDDET DE KISILIYOR ve bu ÖLÇÜLMÜŞ bir gerek: harita tek başına gece
        // kararmıyor. Kaldırıldığında bisikletin kromu karanlıkta yeniden parladı, geri
        // konduğunda düzeldi. Sebebi, pişen haritanın gök kubbenin ortalama radyansını
        // değil malzemenin kendi parlaklığını taşıması; ölçüm kazanıyor, teori değil.
        //
        // Oran ayrı bir "gece ayarı" değil: ortam ışığının kendi parlaklığından çıkıyor,
        // yani gökyüzü hangi sebeple kararırsa yansıma da onunla kararıyor.
        float skyLevel = Mathf.Max(color.r, Mathf.Max(color.g, color.b))
                       * settings.ambientStrength;

        RenderSettings.reflectionIntensity = Mathf.Clamp01(skyLevel * 2.2f);

        if (ColourMoved(color, reflectionSky, 0.02f))
        {
            reflectionSky = color;
            DynamicGI.UpdateEnvironment();
        }

        if (view != null) view.clearFlags = CameraClearFlags.Skybox;

        ApplyShadowDistance();
        ApplySky(precipitation, snowiness, day);
    }

    /// Kameranın bulut kuşağının neresinde olduğu: 0 dışında, 1 tam içinde.
    /// Kuşağın kenarlarında yumuşak geçer, aniden içine girilmez.
    float DeckAmount(float precipitation)
    {
        if (view == null) return 0f;

        // Kuşak bulut tabanının hemen altından başlar ve katmanın içine girer:
        // içinden geçtiğin bulut ile yukarıda gördüğün bulut aynı şey olmalı
        float altitude = view.transform.position.y;
        float center = activeCloudBottom - settings.deckLeadMeters + settings.deckThickness * 0.5f;
        float distance = Mathf.Abs(altitude - center) / Mathf.Max(1f, settings.deckThickness * 0.5f);

        float inside = 1f - Mathf.SmoothStep(0.55f, 1f, distance);
        return inside * Mathf.Lerp(settings.deckClearAmount, 1f, precipitation);
    }

    /// Sisin arkasındaki gölgeyi kimse görmez. Görüş kapandıkça gölge mesafesi de
    /// kısalır: hava sertleştiğinde, yani GPU en çok yorulduğunda, maliyet düşer.
    void ApplyShadowDistance()
    {
        if (UniversalRenderPipeline.asset == null) return;

        float distance = Mathf.Min(settings.maxShadowDistance,
                                   visibility * settings.shadowVisibilityRatio);

        // Yalnız KAYDA DEĞER değişince yazılıyor. Pipeline asset'ine yazmak onu
        // kirletiyor ve ayar yenilemesi tetikliyor. Eşik geniş tutulmalı: görüş
        // sürekli nefes alıyor (bank gürültüsü + `visibilityBreathing`), dar eşikle
        // her karede aşılıyor ve yazma seyrekleşmiyordu. Yirmi beş metre gölge
        // mesafesinde gözle ayırt edilmez — gölgeler zaten o uzaklıkta sönümleniyor.
        if (Mathf.Abs(distance - appliedShadowDistance) < 25f) return;

        appliedShadowDistance = distance;
        UniversalRenderPipeline.asset.shadowDistance = distance;
    }

    /// Bulut geçişi ayrı bir shader'da çalıştığı için parametreler global yazılır;
    /// gökyüzü ve bulutlar aynı değerleri okur, ikisi çelişemez.
    void ApplySky(float precipitation, float snowiness, float day)
    {
        if (skyMaterial == null) return;

        // Kapsama şiddetten daha hızlı yükselir: gökyüzü yağış tam sertleşmeden kapanır,
        // yoksa sağanak açık gökyüzünün altında başlıyormuş gibi duruyor. Zamanda bir
        // önceleme değil — eğri dikleştiriliyor, kapsama tavanına şiddetten önce varıyor.
        // Katmanın kendi durumu fırtınanın ham şiddetinden gelir. Yağış, oyuncu bulut
        // tavanının üstüne çıkınca sönüyor — ama altındaki deniz aynı fırtınanın denizi;
        // sönmüş değere bağlanınca zirveye varır varmaz bulutlar da inceliyordu.
        float storm = weatherDriver.CloudMass;

        coverage = CoverageLocked
            ? LockedCoverage
            : Mathf.Lerp(settings.clearCoverage, settings.stormCoverage, Mathf.Clamp01(storm * (1f + settings.coverageGain)));

        // Alt sınır normalde bağlar: altında bulutlar cılız ve dağınık kalıyor. Tek
        // istisna açık pencere — sürücünün nadiren açtığı o an, tabanın delinebildiği
        // tek yol. İki kural aksi halde çelişiyordu: sürücü "bulutlar aralanır, zirve
        // görünür" diye söz verirken taban o anın hiç gelmemesini sağlıyordu.
        coverage = Mathf.Max(coverage,
            Mathf.Lerp(settings.minCoverage, settings.openCoverage, weatherDriver.ClearWindow));

        // Bulutlar rüzgârla aynı yöne sürüklenir, ama yer rüzgârının hızıyla değil:
        // 3 km yükseklikteki hava akımı çok daha güçlüdür ve yer dinginken bile eser.
        Vector3 horizontal = new(wind.Velocity.x, 0f, wind.Velocity.z);
        Vector3 target = horizontal.sqrMagnitude > 0.01f ? horizontal.normalized : Vector3.right;

        // Yer rüzgârı yönü dakikada birkaç tur dönüyor. Bulut kütlesi bu kadar çevik
        // değildir; ağır yumuşatma olmadan makaslama vektörü savrulup üst katmanları
        // yüzlerce m/s hızla süpürüyordu.
        float turn = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1f, settings.headingSmoothing));
        smoothedHeading = Vector3.Slerp(smoothedHeading.sqrMagnitude < 0.01f ? target : smoothedHeading,
                                        target, turn).normalized;

        Vector3 heading = smoothedHeading;

        // Hız da yön gibi ağır yumuşatılır: kütle esintiyle hızlanmaz. Ham hız, rüzgâra
        // eklenen saniyelik sarsıntılarla titremeye başlayınca kayan doku 16 karelik
        // zamansal birikimin altında seğiriyor, geçmiş kelepçeden atılıyor ve bulut
        // kenarları blok blok pikselleşiyordu.
        float targetSpeed = Mathf.Max(settings.minCloudSpeed, horizontal.magnitude * settings.cloudDrift);
        float speed = smoothedDrift = smoothedDrift <= 0f
            ? targetSpeed
            : Mathf.Lerp(smoothedDrift, targetSpeed, turn);
        // Sarmalama yok: aynı kayma vektörü hava haritası, temel şekil, varyasyon ve
        // detay dokularında dört ayrı ölçekte örnekleniyor. Tek bir periyoda sarmak
        // yalnızca birinde tam tura denk gelir, kalan üçü sarma anında yer değiştirir —
        // en görünürü hava haritası, çünkü bulutların nerede olduğunu o belirler.
        // Hassasiyet kaygısı makaslama ayrı vektöre taşındığında ortadan kalktı:
        // saatlerce kayan değer bile doku örneklemesi için fazlasıyla ince.
        cloudOffset -= new Vector2(heading.x, heading.z) * (speed * Time.deltaTime);

        // Biçim evrimi rüzgârla hızlanır: fırtınada bulutlar daha çabuk değişir
        evolution += settings.evolutionSpeed * (0.5f + wind.Strength) * Time.deltaTime;

        // Konvektif yükselme gündüz sürer: kaynağı ısınan zemindir. Gece zemin
        // soğur, yükselme durur — bulutlar yalnız sürüklenir.
        convectiveRise += settings.convectiveRise * time.DayFactor * Time.deltaTime;

        UpdateLocalRain();

        // Gökyüzü gradyanı artık materyale gitmiyor: gökyüzü, sisle aynı AirColor
        // fonksiyonunu okur ve o fonksiyon _HeightFog* globallerinden beslenir.

        // Kadranın rengi ışığın rengiyle aynı; bulut kalınlaştıkça güneş perdelenir
        float veil = 1f - coverage * 0.75f;
        skyMaterial.SetColor(SunColorId, time.CurrentSunColor * veil);
        skyMaterial.SetColor(MoonColorId, time.MoonTint * veil);

        Shader.SetGlobalFloat(StarStrengthId, (1f - day) * (1f - coverage) * 1.2f);

        // Bulut parametreleri global: ayrı geçişte çalışan bulut shader'ı da bunları okur
        // Çözünürlük dokunun kendisinden okunur: shader mip seviyesini adım boyuyla
        // karşılaştırarak seçiyor ve bunun için bir texel'in kaç metre olduğunu bilmesi
        // gerekiyor. Sabit olarak yazılsa gürültü üreticisi değiştiğinde sessizce ayrışır.
        if (baseNoise != null)
        {
            Shader.SetGlobalTexture(BaseNoiseId, baseNoise);
            Shader.SetGlobalFloat(BaseNoiseTexelsId, baseNoise.width);
        }

        if (detailNoise != null)
        {
            Shader.SetGlobalTexture(DetailNoiseId, detailNoise);
            Shader.SetGlobalFloat(DetailNoiseTexelsId, detailNoise.width);
        }

        if (weatherMap != null)
        {
            Shader.SetGlobalTexture(WeatherMapId, weatherMap);
            Shader.SetGlobalFloat(WeatherMapScaleId,
                1f / Mathf.Max(1f, settings.weatherMapWorldSize));
            Shader.SetGlobalFloat(WeatherMapTexelsId, weatherMap.width);
        }

        if (skipMap != null) Shader.SetGlobalTexture(SkipMapId, skipMap);
        if (curlNoise != null) Shader.SetGlobalTexture(CurlNoiseId, curlNoise);
        if (highNoise != null) Shader.SetGlobalTexture(HighNoiseId, highNoise);

        Shader.SetGlobalVector(SunDirectionId, time.SunDirection);
        Shader.SetGlobalVector(MoonDirectionId, time.MoonDirection);
        Shader.SetGlobalVector(CloudWindId, new Vector3(cloudOffset.x, 0f, cloudOffset.y));

        // Makaslama sabit bir mesafe: katman kalınlığının oranı kadar yanal kayma
        Shader.SetGlobalVector(CloudShearOffsetId,
            heading * (settings.shearAmount * (settings.cloudTop - activeCloudBottom)));

        // Yön dönmesi rüzgâr şiddetiyle azalır: sert rüzgârda hava kütlesi bütün
        // katmanda aynı yöne sürüklenir, sakin havada sapma belirginleşir.
        Shader.SetGlobalFloat(CloudShearTurnId,
            settings.shearTurnDegrees * Mathf.Deg2Rad * Mathf.Lerp(1f, 0.35f, wind.Strength));

        // Bulut renkleri atmosferin renginden türer: şafakta kızıllık buluta da geçer
        // Çarpan bire yakın tutulur: renk zaten doygun geldiği için 1.5 ile çarpmak
        // kırmızı kanalı taşırıp bulutu beyaza çeviriyordu. Parlaklık ambient şiddetinden
        // geliyor, renkten değil.
        // BULUT RENGİ GÖKTEN, PARLAKLIK ESKİ TABANDAN. Gök radyansını doğrudan vermek
        // denendi ve bulutlar simsiyah oldu: bulutu aydınlatan şey zenit RADYANSI değil
        // gökten gelen toplam IŞINIM, arada π kat fark var. Radyansın taşıdığı doğru
        // bilgi RENK; parlaklık zaten kalibre edilmiş taban rengin işi. İkisi ayrılınca
        // bulut şafakta turuncuya döner ama kararmaz.
        Shader.SetGlobalColor(CloudBrightId,
            Recolour(color, skyBright) * Mathf.Lerp(1.05f, 1.25f, snowiness * day));
        Shader.SetGlobalColor(CloudDarkId,
            Recolour(color, skyShade) * Mathf.Lerp(0.4f, 0.7f, snowiness));
        // BULUTUN IŞIĞI KENDİ KOTUNDAN. Yerdeki huzme kullanılıyordu: şafakta güneş
        // ufkun altındayken yerde ışık yok, dolayısıyla bulut da ışıksız kalıyor ve
        // koyu bir siluete dönüşüyordu — en ince sis bile onu yutuyordu.
        //
        // Gerçekte bulut 1.7 km yukarıda ve güneşi YERDEN ÖNCE görür: Dünya'nın
        // gölgesi vadideyken bulut o gölgenin üstündedir. Şafakta bulut tabanlarının
        // turuncu yanmasının sebebi tam olarak budur — alpenglow'la aynı geometri.
        //
        // Renk ve şiddet birleşik: geçirgenlik hem kızarmayı hem sönümü taşıyor.
        Vector3 cloudBeam = Atmosphere.BeamTransmittance(activeCloudBottom, time.SunDirection);

        // BULUTUN ISINMASI GEÇ AÇILIR. Doğrudan ışık alçak güneşte derin kızıl; bulutun
        // ortam ışığı ise zenitten geldiği için mavimsi. İkisi üst üste binince ekranda
        // PEMBE okunuyor ve şafağın ilk çeyreğinde bulutlar pembeleşiyordu. Kısıcının
        // karesi alınınca ısınma üç dereceden sonra başlıyor: pembe pencere kapanıyor,
        // geçiş yine sürekli kalıyor.
        float cloudWarm = Atmosphere.LowSunFade(activeCloudBottom, time.SunDirection);
        cloudWarm *= cloudWarm;
        cloudBeam *= cloudWarm;

        Shader.SetGlobalColor(CloudSunColorId,
            new Color(cloudBeam.x, cloudBeam.y, cloudBeam.z, 1f));

        // Bulut perspektifi görüşle birlikte değişir. Sabit bir mesafe, dağ üç yüz
        // metrede kaybolurken bulutları hâlâ berrak gösteriyordu: ikisi aynı havayı
        // paylaşmıyor gibi duruyor.
        // Rakım havayı seyreltir ama sonsuz berrak yapmaz: yatay bakışta ışın yine
        // kilometrelerce hava kat ediyor ve Rayleigh saçılması en berrak havada bile
        // görüşü sınırlıyor. Tavansız bırakılınca zirvede karışma yüz kilometrelere
        // çıkıyor ve bulut denizinin ufku çıplak bir çizgi olarak duruyordu.
        //
        // ÖLÇÜ DENİZİ GÖRMEZ. `Visibility` kameranın kotundaki TOPLAM havayı anlatıyor ve
        // şafakta onu 120 m derinliğindeki sis denizi belirliyor: 1871 m çıkıyor, menzil
        // 16 km'ye kırpılıyor ve `marchable = start <= hazeDistance` yüzünden katmana
        // 16 km'den uzakta giren her ışın — yani ufkun 9.3° altındaki her yön — hiç
        // çizilmiyordu. Zeminde gökyüzünü dolduran bulutlar 5-25° bandında; yarısı
        // soluyor, altı tamamen siliniyordu. Bulut 2.6 km yukarıda, denizin çok üstünde.
        float hazeDistance = settledVisibility * settings.hazeVisibilityFactor
                             / Mathf.Max(0.01f, airThinning);

        // Taban, görüşün bulut menzilini sürüklemesini durduruyor. Görüş yerdeki havayı
        // anlatıyor, bulut ise onun üstünde duruyor; ikisini doğrusal bağlamak fırtınada
        // gökyüzünde delik açıyordu.
        hazeDistance = Mathf.Clamp(hazeDistance,
            settings.minHazeDistance, settings.maxHazeDistance);

        Shader.SetGlobalFloat(CloudHazeDistanceId, hazeDistance);
        Shader.SetGlobalFloat(CloudRimStrengthId, settings.rimStrength);
        Shader.SetGlobalFloat(CloudPowderStrengthId, settings.powderStrength);
        Shader.SetGlobalFloat(CloudRainAbsorbId, settings.rainAbsorption * storm);

        // Yüksek katman hacimsel örtü kapandıkça sönümlenir: altından görünmez zaten,
        // çizmek boşuna. Cinsi ayarın kendisi seçer.
        Shader.SetGlobalFloat(HighAmountId,
            Mathf.Lerp(settings.highCloudClear, settings.highCloudStorm, storm)
            * Mathf.Clamp01(1f - coverage * 0.9f));
        Shader.SetGlobalFloat(HighTypeId, settings.highCloudType);
        Shader.SetGlobalFloat(HighAltitudeId, settings.highCloudAltitude);
        Shader.SetGlobalFloat(HighScaleId, settings.highCloudScale);
        Shader.SetGlobalFloat(CloudAmbientId, settings.cloudAmbient);
        Shader.SetGlobalFloat(CloudAmbientFloorId, settings.ambientFloor);
        Shader.SetGlobalFloat(CloudMassWarmthId, settings.massWarmth);
        Shader.SetGlobalFloat(CloudMassBrightnessId, settings.massBrightness);
        Shader.SetGlobalFloat(CloudLightReachId, settings.lightProbeMeters);
        Shader.SetGlobalFloat(CloudMultiScatterId, settings.multiScatter);
        // Buluta binen sıcak ton huzmenin süzülme renginden gelir ve yalnızca güneş
        // ufka yakınken açılır.
        //
        // Rengi GÖKTEN almak denendi (R7) ve silindi. Kurgu doğruydu — huzme ufkun
        // altında sıfır, dolayısıyla ton siyaha düşüyor ve şafak öncesi bulutlar hiç
        // renk almıyor. Ama ölçüm sorunun orada olmadığını gösterdi: güneş −4°'deyken
        // gerçek ortam ~13 lüks (ay ışığı seviyesi) ve gerçek bir dağda da bulut
        // tabanları o saatte yanmaz. Üstelik bizim alacakaranlık göğümüz gerçeğe göre
        // zaten FAZLA parlak (−6°'de 5.6 kat). Yani ufkun altına renk taşımak, olmayan
        // bir olguyu üretmek olurdu. Şafağın gerçek gösterisi −1° ile +3° arasında,
        // huzmenin zaten var olduğu yerde.
        Shader.SetGlobalColor(CloudDuskTintId, time.CurrentSunColor);
        Shader.SetGlobalFloat(CloudDuskStrengthId,
            settings.duskCloudStrength * time.HorizonFactor * cloudWarm);
        Shader.SetGlobalColor(CloudMoonColorId, time.MoonTint);

        Shader.SetGlobalFloat(CoverageId, coverage);
        Shader.SetGlobalFloat(CloudBottomId, activeCloudBottom);
        Shader.SetGlobalFloat(CloudTopId, settings.cloudTop);
        Shader.SetGlobalFloat(PlanetRadiusId, settings.planetRadius);
        Shader.SetGlobalFloat(CloudScaleId, settings.cloudScale);
        Shader.SetGlobalFloat(DetailScaleId, settings.detailScale);
        Shader.SetGlobalFloat(DetailStrengthId, settings.detailStrength);
        Shader.SetGlobalFloat(ShearAmountId, settings.shearAmount);
        Shader.SetGlobalFloat(LargeWeightId, settings.largeCloudWeight);
        Shader.SetGlobalFloat(CurlStrengthId, settings.curlStrength);
        Shader.SetGlobalFloat(EvolutionId, evolution);
        Shader.SetGlobalFloat(CloudRiseId, convectiveRise);
        Shader.SetGlobalFloat(DetailDistanceId, settings.detailDistance);
        Shader.SetGlobalFloat(CloudDitherId, settings.cloudDither);
        Shader.SetGlobalFloat(CloudEdgeSoftenId, settings.cloudEdgeSoften);
        Shader.SetGlobalFloat(StepGrowthId, settings.stepGrowthDistance);


        // Fırtınada bulut yalnızca gökyüzünü kaplamaz, kalınlaşır da. Kapsamayla aynı
        // kaynaktan: kalınlık da katmanın kendi durumu.
        Shader.SetGlobalFloat(DensityScaleId, settings.densityScale * Mathf.Lerp(1f, settings.stormDensityBoost, storm));
        Shader.SetGlobalFloat(StepsId, settings.raymarchSteps);
        Shader.SetGlobalFloat(LightStepsId, settings.lightSteps);
    }

    /// Oyuncunun üstündeki kolonu hava haritasından okur: yağış payı kapsamayla ve
    /// bulutun kabarıklığıyla (tip) birlikte artar — yayvan ince katman yağdırmaz,
    /// kabarık kalın kütle yağdırır. Harita rüzgârla aktığı için okuma noktası da
    /// kayar; bulut geçtikçe yağmur başlar ve diner.
    /// Gökyüzünün verilen yöndeki FİZİKSEL rengi (Atmosphere.SkyRadiance). Ham radyans
    /// 10⁻² mertebesinde; kazanç yalnız sahne birimlerine taşır, rengi değiştirmez.
    /// Yoğunluktan görüş mesafesi (Koschmieder), FİZİKSEL TAVANLA. Sis katmanı
    /// yükseldikçe üstel seyreldiği için bölüm yukarıda patlıyor: sığ katmanla zirvede
    /// "3900 km görüş" çıkıyordu. Hava boşluk değildir — en temiz havada bile Rayleigh
    /// saçılması görüşü birkaç yüz kilometrede kapatır, tavan oradan.
    const float AtmosphericVisibilityLimit = 300000f;

    float Visible(float density)
        => Mathf.Min(AtmosphericVisibilityLimit,
                     settings.fogThickness / Mathf.Max(1e-6f, density));

    /// Gökyüzünün verilen yöndeki FİZİKSEL rengi. Kazanç `Atmosphere.SceneGain` —
    /// pozlama seviyesiyle ORTAK. Ayrı tutulduklarında biri değişince öteki yerinde
    /// kalıyor ve gökyüzü ile ondan türeyen değer ayrışıyordu.
    static Color SampleSky(Vector3 view, Vector3 sun)
    {
        Vector3 r = Atmosphere.SkyRadiance(0f, view, sun) * Atmosphere.SceneGain;
        return new Color(r.x, r.y, r.z, 1f);
    }

    /// Kaynağın PARLAKLIĞINI koruyup RENGİNİ hedefe taşır. Fiziksel gök örneği doğru
    /// tonu biliyor ama birimi bulutun beklediğinden farklı; ikisini böyle birleştirmek
    /// hem şafak rengini getiriyor hem kalibrasyonu bozmuyor.
    static Color Recolour(Color source, Color hue)
    {
        float sourceLuma = source.r * 0.2126f + source.g * 0.7152f + source.b * 0.0722f;
        float hueLuma = hue.r * 0.2126f + hue.g * 0.7152f + hue.b * 0.0722f;
        if (hueLuma <= 1e-5f) return source;

        float scale = sourceLuma / hueLuma;
        return new Color(hue.r * scale, hue.g * scale, hue.b * scale, 1f);
    }

    void UpdateLocalRain()
    {
        if (weatherMap == null || view == null) return;

        Vector2 mapPos = new Vector2(view.transform.position.x, view.transform.position.z)
                         + cloudOffset * 0.72f;
        float scale = 1f / Mathf.Max(1f, settings.weatherMapWorldSize);
        Color column = weatherMap.GetPixelBilinear(mapPos.x * scale, mapPos.y * scale);

        // Kapsama eşiği: saçak altında yağmur olmaz. Tip eşiği: yağmuru kümülüs ve
        // üstü düşürür.
        float mass = Mathf.InverseLerp(0.12f, 0.55f, column.r);
        float build = Mathf.InverseLerp(0.30f, 0.75f, column.g);
        float target = Mathf.Clamp01(mass * build);

        // Bulut tepesinin ÜSTÜNDE yağış yok — yağan şeyin kaynağı aşağıda kalır.
        // Zirve bulutları deldiğinde yağmurun kesilmesi tırmanışın en belirgin
        // eşiklerinden biri.
        float columnTop = activeCloudBottom + column.a * (settings.cloudTop - activeCloudBottom);
        float belowTop = 1f - Mathf.SmoothStep(0f, 1f,
                              Mathf.InverseLerp(columnTop, columnTop + 300f, view.transform.position.y));
        target *= belowTop;

        // Sütunun tepesi sürücüye KOT olarak veriliyor, kesme payı olarak değil: kar
        // profili her kot bandının kesmesini kendi yüksekliğinden hesaplıyor. Skaler
        // pay yalnızca oyuncunun bulunduğu kot için doğruydu.
        weatherDriver.CloudColumnTop = columnTop;

        // Yavaş yumuşatma: bulut kenarından geçerken yağmurun bir anda kesilmesi
        // yapay duruyor; gerçekte perde kenarı birkaç saniyede zayıflar.
        localRain = Mathf.Lerp(localRain, target,
                               1f - Mathf.Exp(-Time.deltaTime / 2.5f));
    }

    /// Yükseklik sisi parametreleri. Görüş mesafesi zaten havayı, rüzgârı ve bulut
    /// kuşağını hesaba katıyor; burada yalnızca yüksekliğe dağıtılıyor.
    void ApplyHeightFog()
    {
        // Yarılanma yüksekliğinden üstel katsayı: exp(-k · h) = 0.5 → k = ln2 / h
        // KATMAN DERİNLİĞİ HAVADAN. Açık havadaki pus sığ bir sınır tabakasıdır; yağışta
        // sütun dikey karışır ve yağmur tepeden dibe doldurur. Sabit tutmak ikisinden
        // birini hep bozuyordu: sığ değer 1000 m kotta sağanakta 5 km görüş veriyor,
        // derin değer açık havada bulut denizini siliyordu. Görüş ve inversiyon tavanı
        // gibi bu da tek kaynaktan (yağış şiddeti) sürülür, üçü çelişemez.
        float halfHeight = Mathf.Lerp(settings.fogHalfHeightClear,
                                      settings.fogHalfHeightStorm, weather.Precipitation);
        float falloff = 0.6931f / Mathf.Max(1f, halfHeight);

        // Taban yoğunluk görüş mesafesinden gelir. Sis yükseldikçe seyreldiği için
        // görüş yalnızca taban kotunda bu değere denk düşer, yukarısında açılır.
        float density = settings.fogThickness / Mathf.Max(1f, visibility);

        // Vadi sisi gece işidir: zemin gece boyunca ısı kaybeder, havadaki nem yoğuşur
        // ve sis şafakta en kalın hâline ulaşır. Güneş yükseldikçe zemini ısıtır ve sis
        // yukarıdan aşağı erir. Bunu yalnızca hava durumuna bağlamak sabahın kendine has
        // ağırlığını kaybettiriyordu — tırmanışa çıkarken vadi dolu olmalı.
        float burnOff = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time.SunHeight / settings.valleyFogBurnOff));

        // Vadi sisi GECENİN ürünüdür (ışınımsal soğuma): gece birikir, sabah güneşle
        // dağılır ve akşam GERİ GELMEZ. Sönüm yalnız güneş yüksekliğine bağlanınca
        // formül simetrik kalıyor ve batımda vadiyi yeniden dolduruyordu — yerden
        // bakan oyuncu gün batımında bulutları göremiyordu: peçe doğruydu, sisin
        // orada olması yanlıştı.
        float clock = time.Normalized;
        float morningSide = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.6f, clock));
        float lateNight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, clock));
        float seaSeason = Mathf.Max(morningSide, lateNight);

        // ŞAFAK VADİ SİSİNİN TEK KAYNAĞI DENİZ KATMANIDIR. Burada ayrıca yerleşik havayı
        // kalınlaştıran bir çarpan vardı (`valleyFogAtDawn`): aynı olay iki mekanizmayla
        // modelleniyordu ve derin olanı (yarı yükseklik 1400 m) 2.6 km yukarıdaki
        // bulutlara kadar uzanıp onları siliyordu. Deniz 120 m'de biter, vadide kalır.

        // Şafak sis denizi AYRI KATMAN. Açık gecede bile vadi tabanı havadan bağımsız
        // dolar; görüşten türeyen yoğunluk açık havada o kadar küçük ki çarpan
        // hissedilmiyordu. Güneşle birlikte erir.
        //
        // Eskiden `max()` ile yerleşik havanın yoğunluğuna katlanıyordu ve tek kanaldan
        // geçiyordu: CPU denizi 120 m'lik kendi profiliyle hesaplıyor, shader ise onu
        // 1400 m'lik yerleşik hava profiliyle yayıyordu. Sığ deniz bulut tabanına kadar
        // tırmanıyor, yol boyunca optik derinlik ON KAT fazla çıkıyor ve şafakta yukarı
        // bakan oyuncuya bulutları tamamen siliyordu. Artık iki katman, iki kanal;
        // yükseklik profilini her biri kendi katsayısıyla shader'da uyguluyor.
        float seaFalloff = 0.6931f / Mathf.Max(1f, settings.dawnSeaHalfHeight);
        float seaDensity = settings.fogThickness / Mathf.Max(1f, settings.dawnSeaVisibility)
                           * (1f - burnOff) * seaSeason;

        // GERÇEK GÖRÜŞ, YOĞUNLUĞUN KENDİSİNDEN. Yoğunluk yukarıda zorlanıyor (vadi
        // çarpanı, deniz katmanı) ama `visibility` o zorlamaları görmüyordu: HUD 13.6 km
        // derken gerçek yoğunluk 600 m'lik sise denk geliyordu — 22 kat. Bulut peçesi
        // gerçeği kullandığı için bulutlar siliniyor, oyuncu ise "13 km görüş var, bulut
        // nerede" diyordu.
        //
        // AYRI ALAN: `visibility` havanın hedefi, girdi. Onu yoğunluktan geri yazmak
        // geri besleme kuruyor (yoğunluk görüşten, görüş yoğunluktan) ve değer her
        // karede katlanıp metrelere çöküyordu.
        //
        // SERBEST TROPOSFER — ÜÇÜNCÜ KATMAN. Havanın kendi molekülleri (Rayleigh):
        // yayvan, ve hava olayları sınır tabakasında yaşadığı için YAĞIŞTAN BAĞIMSIZ.
        // Bir "inversiyon üstü kalıntı oranı" olarak modellenip sınır tabakasının kendi
        // sığ profiliyle çarpılıyordu; birkaç bin metrede sıfırlanıyor ve zirveden
        // bakışta otuz kilometredeki sırt tam kontrastla, karton gibi duruyordu.
        float freeFalloff = 0.6931f / Mathf.Max(1f, settings.freeAirHalfHeight);
        float freeDensity = settings.fogThickness / Mathf.Max(1f, settings.freeAirVisibility);

        // Fırtınada nem yukarı taşınır ve tavan yükselir: sakin havada vadiye çöken sis,
        // yağışta dağın yarısını sarar. Havayla bağlanmazsa inversiyon sabit bir çizgi
        // gibi durur ve fırtınada zirve boşuna berrak kalır.
        //
        // Tavan ve kapak BURADA hesaplanır: görüş göstergesi, bulut menzili ve havanın
        // seyrekliği üçü de aynı ifadeden beslenmeli. Ayrı ayrı türetilince biri
        // inversiyonu görüp öteki görmüyordu.
        float ceiling = settings.inversionHeight + weather.Precipitation * settings.inversionStormRise;

        // Bulut tabanı inversiyon tavanına inmişse ikisi aynı katmandır: sisin bittiği
        // yerde bulut başlamalı, arada boşluk kalmamalı.
        ceiling = Mathf.Min(ceiling, activeCloudBottom);

        // Ölçü KAMERANIN KOTUNDA, üç katmanın toplamı: taban değeri kullanmak zirvedeki
        // oyuncuya vadi görüşünü okutuyordu.
        float cameraHeight = view != null
            ? view.transform.position.y - settings.fogBaseAltitude : 0f;
        float eyeHeight = Mathf.Max(0f, cameraHeight);

        float lid = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(ceiling - settings.inversionWidth,
                              ceiling + settings.inversionWidth, cameraHeight));

        float eyeBoundary = density * Mathf.Exp(-falloff * eyeHeight) * lid;
        float eyeFree = freeDensity * Mathf.Exp(-freeFalloff * eyeHeight);
        float eyeSea = seaDensity * Mathf.Exp(-seaFalloff * eyeHeight);

        effectiveVisibility = Visible(eyeBoundary + eyeSea + eyeFree);

        // Bulut menzilinin ölçüsü: vadi denizi HARİÇ. Deniz 120 m'de biter, bulut 2.6 km
        // yukarıdadır; onun menzilini vadi dibindeki sis belirleyemez.
        settledVisibility = Visible(eyeBoundary + eyeFree);

        // Kameranın kotundaki havanın seyrekliği: taban kotundaki toplama oranla.
        // Bulutların karışma mesafesi de bunu kullanır — görüş yer seviyesinin değeri,
        // yükseklere olduğu gibi taşınınca dağın kilometrelerce net göründüğü havada
        // bulutlar birkaç yüz metrede yok oluyordu; ikisi aynı atmosferi paylaşmalı.
        // Serbest katman hem payda hem paydada: zirvede sıfıra gitmemesinin sebebi o.
        //
        // SIRA ÖNEMLİ: `FogEnabled` kapanışından ÖNCE. Sonra hesaplanırsa payda sıfırlanır
        // ama pay sıfırlanmaz ve oran patlar.
        airThinning = (eyeBoundary + eyeFree)
                      / Mathf.Max(1e-9f, density + freeDensity);

        // Test anahtarı kapalıyken üç katman da sıfırlanır: kalan parametreler shader'da
        // yoğunlukla çarpıldığı için etkisiz kalır, ayrıca kapatmaya gerek yok.
        if (!FogEnabled) { density = 0f; seaDensity = 0f; freeDensity = 0f; }

        // Bankların gücü havadan: fırtına sisi daha yamalı sarar, şafak denizi de
        // bank bank gezer. Sabit güç, sisi her yerde aynı anda kalınlaştırıyordu.
        float bankStrength = Mathf.Lerp(settings.fogBankClear, settings.fogBankStorm,
            weather.Precipitation);
        bankStrength = Mathf.Max(bankStrength, 0.7f * (1f - burnOff));

        Shader.SetGlobalColor(HeightFogColorId, color);
        // Sisin gölge tarafı gökyüzününkiyle aynı renk: ikisi ayrışırsa alacakaranlıkta
        // dağ, arkasındaki koyu gökten parlak kalıp düz bir karton gibi yapışıyordu.
        // ADIM 1: güneş tarafındaki ufuk rengi artık fizikten. Eskiden buraya huzme
        // rengi (CurrentSunColor) veriliyordu — ama huzme ile GÖĞÜN o yöndeki rengi
        // ayrı şeyler: huzme kızarır, gök o kızıllığı saçarak turuncu-altın bir bant
        // kurar. İkisini aynı saymak şafağı tek renge indiriyordu.
        Vector3 sunFlat = new Vector3(time.SunDirection.x, 0f, time.SunDirection.z);
        sunFlat = sunFlat.sqrMagnitude > 1e-6f ? sunFlat.normalized : Vector3.forward;
        // Örnek ufkun 2° üstünden: bant orada en güçlü. 6°'de zaten seyrelmiş havayı
        // okuyorduk ve turuncu sönük çıkıyordu.
        //
        // 1° denendi ve geri alındı: fizik `gold`'un parlaklığını yakalıyor (0.84 / 0.90)
        // ama ekranda bulutlar beyazlıyor, gök koyulaşıyordu. Ufka bir derece daha
        // yaklaşmak yalnız parlaklığı değil, kontrastı da sertleştiriyor.
        Vector3 sunwardHorizon = (sunFlat * Mathf.Cos(2f * Mathf.Deg2Rad)
                                  + Vector3.up * Mathf.Sin(2f * Mathf.Deg2Rad)).normalized;

        Vector3 awayHorizon = (-sunFlat * Mathf.Cos(2f * Mathf.Deg2Rad)
                               + Vector3.up * Mathf.Sin(2f * Mathf.Deg2Rad)).normalized;


        Color physicalSunward = SampleSky(sunwardHorizon, time.SunDirection);
        Color physicalZenith = SampleSky(Vector3.up, time.SunDirection);
        Color physicalAway = SampleSky(awayHorizon, time.SunDirection);

        // Bulutun PARLAK yüzü bundan beslenir ve KISILMAMIŞ hâli kullanılır: kısıcı
        // parlaklığı düşürüyor, `Recolour` ise sıfıra yakın bir kaynaktan ton alamıyor
        // ve palete geri düşüyordu.
        Color sunwardHue = physicalSunward;

        // Huzme ve güneş rengiyle AYNI çarpan: üçü ayrışırsa bulutlar alçak güneşte
        // pembeleşiyor ya da gök bir anda kızıla dönüyor.
        float lowSun = Atmosphere.LowSunFade(0f, time.SunDirection);
        physicalSunward *= lowSun;
        physicalAway *= lowSun;


        // Kapalı havada gök kendi saçılmasını kaybedip bulutun grisine yaklaşır —
        // ama tamamen değil: yağışlı şafakta bile ufukta kızıl bir yarık kalır.
        float overcast = weather.Precipitation * 0.55f;

        Shader.SetGlobalColor(HeightFogSunColorId,
            Color.Lerp(physicalSunward, color, overcast));

        // ADIM 2: zenit de fizikten. Şafağın çarpıcılığı turuncunun kendisinden değil,
        // üstündeki MAVİYLE yan yana durmasından geliyor. Zenit eski palette kalınca
        // gökyüzü tek tonda gri-kahve bir zemine dönüyordu.
        Shader.SetGlobalColor(HeightFogZenithId,
            Color.Lerp(physicalZenith, zenith, overcast));

        // ADIM 3: karşı ufuk da fizikten. Gökyüzü üçlüsü tamamlandı; shader aradaki
        // yönleri bu üç örnekten harmanlıyor, yani yayılım artık elle değil modelden.
        Shader.SetGlobalColor(HeightFogShadowColorId,
            Color.Lerp(physicalAway, shadowColor, overcast));


        // BULUTUN ORTAM RENGİ ŞAFAKTA UFKA KAYAR. Parlak yüz normalde zenitten beslenir
        // ama zenit alçak güneşte MAVİ kalıyor; doğrudan ışık ise derin kızıl. İkisi üst
        // üste binince bulut PEMBE okunuyor ve şafağın ilk çeyreği pembeleşiyordu.
        //
        // Gerçekte bulutu aydınlatan şey tepe göğü değil, parlak ufuktur. Ton ufka
        // kayınca ortam ve doğrudan ışık aynı aileden gelir: pembe kapanır, şafak
        // topyekûn ılık olur. Pencereyi `HorizonFactor` açar — güneş ufka yakınken tam,
        // yükseldikçe zenite döner.
        skyBright = Color.Lerp(physicalZenith, sunwardHue, time.HorizonFactor);
        skyBright = Color.Lerp(skyBright, color, overcast);

        // GÖLGE YÜZÜ ISITILMAZ. Parlak yüz ufka kayıyor (yukarıda) ama gölge yüzü
        // kaymamalı: karşı ufuk örneği alçak güneşte kendisi kızıl (0.142, 0.084, 0.048)
        // ve buluta taşınınca ay tarafındaki bulut hem kızıl doğrudan ışık hem kızıl
        // ortam alıyor, topyekûn kızarıyordu. Gölge yüzü serin kalmalı — iki yarı
        // arasındaki ayrım da zaten oradan doğuyor.
        skyShade = Color.Lerp(physicalAway, color, overcast);


        // Sönümün renk seçiciliği: berrak havada Rayleigh (mavi önce süpürülür),
        // görüş kapandıkça su damlası baskınlaşır (Mie) ve sönüm nötrleşir.
        float mie = 1f - Mathf.Clamp01(visibility / 8000f);
        Shader.SetGlobalVector(HeightFogChromaId,
            Vector3.Lerp(new Vector3(0.75f, 1f, 1.35f), Vector3.one, mie));

        Shader.SetGlobalFloat(HeightFogDensityId, density);
        Shader.SetGlobalFloat(HeightFogFalloffId, falloff);
        Shader.SetGlobalFloat(HeightFogBaseId, settings.fogBaseAltitude);
        Shader.SetGlobalFloat(FogSeaDensityId, seaDensity);
        Shader.SetGlobalFloat(FogSeaFalloffId, seaFalloff);
        Shader.SetGlobalFloat(FogInversionHeightId, ceiling);
        Shader.SetGlobalFloat(FogInversionWidthId, settings.inversionWidth);
        Shader.SetGlobalFloat(FogFreeDensityId, freeDensity);
        Shader.SetGlobalFloat(FogFreeFalloffId, freeFalloff);
        Shader.SetGlobalVector(FogBankDriftId, fogDrift);
        Shader.SetGlobalFloat(FogBankStrengthId, bankStrength);

        // SÜRÜKLENEN KAR. Rüzgâr eşiği burada uygulanıyor, shader'da değil: kaldırmanın
        // olup olmadığı tek bir dünya durumu, piksel başına sorulacak bir şey değil.
        // Yerde kar olup olmadığını shader kot profilinden kendisi okuyor — orası
        // gerçekten konuma bağlı.
        // HAMLE ATAKLARI. Sürüklenen kar sürekli akmaz: hamleyle 10-20 saniye fışkırır,
        // diner, tekrar gelir. Sürekli şiddet tek başına okunduğunda perde hiç kesilmeyen
        // düz bir akıntı oluyordu.
        //
        // Kar taşınımı sürtünme hızının KÜPÜYLE gider. Küp, hamlenin tepesini patlamaya
        // dibini sakinliğe çeviriyor — atak yapısı buradan doğuyor, ayrı bir zamanlayıcı
        // kurmaya gerek yok.
        float felt = wind.Strength * (1f + wind.Gust);

        float over = Mathf.InverseLerp(settings.spindriftWindThreshold,
                                       settings.spindriftWindThreshold
                                       + settings.spindriftWindBand, felt);
        float lift = over * over * over;

        // Perde rüzgâr sertleştikçe kalınlaşır: daha güçlü akım kar tanesini daha
        // yükseğe taşır.
        float driftHeight = Mathf.Lerp(settings.spindriftHeightCalm,
                                       settings.spindriftHeightStorm, felt);

        Shader.SetGlobalFloat(SpindriftDensityId, settings.spindriftDensity * lift);
        Shader.SetGlobalFloat(SpindriftFalloffId, 1f / Mathf.Max(1f, driftHeight));
        Shader.SetGlobalFloat(SpindriftBrightnessId, settings.spindriftBrightness);
        Shader.SetGlobalFloat(SpindriftLiftId, lift);
        // Tavan rüzgârla büyüyor: hafif rüzgârda uzak yamaç okunur kalır, gerçek
        // fırtınada whiteout gelir. Sabitken ikisi uzakta aynı görünüyordu.
        Shader.SetGlobalFloat(SpindriftMaxDepthId,
            Mathf.Lerp(settings.spindriftMaxDepthCalm,
                       settings.spindriftMaxDepthStorm, lift));

        // Kret tüyünün gücü ve boyu ayarlardan; ikisi de yalnız shader'ın işine yarıyor.
        Shader.SetGlobalVector(SpindriftCrestId,
            new Vector4(settings.spindriftCrestBoost, settings.spindriftCrestRise, 0f, 0f));

        // Akan alan rüzgâr HIZIYLA taşınıyor. Sis banklarının kayması dakikalar
        // ölçeğinde; sürüklenen kar rüzgârın kendisiyle gider, saniyeler ölçeğinde.
        // Taşınmazsa perde renk değiştirir ama akmaz ve göz onu sis sanar.
        Vector3 flow = wind.Velocity;
        spindriftDrift += new Vector2(flow.x, flow.z) * Time.deltaTime;

        Vector2 windDir = new Vector2(flow.x, flow.z);
        windDir = windDir.sqrMagnitude > 0.01f ? windDir.normalized : Vector2.right;

        Shader.SetGlobalVector(SpindriftDriftId,
            new Vector4(spindriftDrift.x, spindriftDrift.y, 0f, 0f));
        Shader.SetGlobalVector(SpindriftWindId,
            new Vector4(windDir.x, windDir.y, 0f, wind.Strength));

        Shader.SetGlobalVector(WindVectorId,
            new Vector4(flow.x, flow.y, flow.z, wind.Gust));
    }

    /// HeightFog.hlsl'deki FogBankAt ile aynı alan, çarpansız hâli (0..1).
    /// Formül değişirse ikisi birlikte değişmeli — iki tüketici, tek alan.
    static float BankField(Vector2 p)
    {
        float a = Mathf.Sin(Vector2.Dot(p, new Vector2(0.0093f, 0.0071f)))
                * Mathf.Sin(Vector2.Dot(p, new Vector2(-0.0052f, 0.0087f)));
        float b = Mathf.Sin(Vector2.Dot(p, new Vector2(0.0031f, -0.0024f)));
        return 0.5f + a * 0.35f + b * 0.15f;
    }

    static Color Blend(Color clear, Color rain, Color snow, float precipitation, float snowiness)
        => Color.Lerp(clear, Color.Lerp(rain, snow, snowiness), precipitation);
}
