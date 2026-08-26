using UnityEngine;
using UnityEngine.Rendering;
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

    [SerializeField] Material skyMaterial;

    /// Yüzey aydınlatması (ışınım) → katılımcı ortam (radyans) dönüşümü. π'dir ve bu
    /// projede ölçülmüştür: probe DC luminansı 0.156 iken sis rengi 0.492, oran 3.15.
    const float AmbientToMedium = 3.15f;

    static readonly int HeightFogColorId = Shader.PropertyToID("_HeightFogColor");
    static readonly int HeightFogDensityId = Shader.PropertyToID("_HeightFogDensity");
    static readonly int HeightFogFalloffId = Shader.PropertyToID("_HeightFogFalloff");
    static readonly int HeightFogBaseId = Shader.PropertyToID("_HeightFogBase");
    static readonly int FogSeaDensityId = Shader.PropertyToID("_FogSeaDensity");
    static readonly int FogSeaFalloffId = Shader.PropertyToID("_FogSeaFalloff");
    static readonly int FogInversionHeightId = Shader.PropertyToID("_FogInversionHeight");
    static readonly int FogInversionWidthId = Shader.PropertyToID("_FogInversionWidth");
    static readonly int FogFreeDensityId = Shader.PropertyToID("_FogFreeDensity");

    /// Ham kaldırma payı (0-1), yoğunlukla çarpılmamış hâli. Yüzey bunu okuyup yerdeki
    /// karı süpürüyor: eşik kuralı burada duruyor, orada ikinci kez kurulmasın diye.

    /// HAM rüzgâr: yön × hız (m/s), w ani esinti.
    /// yalnız kar için ve CPU'da savrulma eşiği uygulanmış hâli — kar kalkmayan hafif
    /// esintide sıfır. Bitki örtüsü ham rüzgârı okuyor: yapraklar o esintide de kıpırdar.
    static readonly int WindVectorId = Shader.PropertyToID("_WindVector");
    static readonly int FogFreeFalloffId = Shader.PropertyToID("_FogFreeFalloff");
    static readonly int FogBankDriftId = Shader.PropertyToID("_FogBankDrift");
    static readonly int FogBankStrengthId = Shader.PropertyToID("_FogBankStrength");
    static readonly int HeightFogShadowColorId = Shader.PropertyToID("_HeightFogShadowColor");
    static readonly int HeightFogZenithId = Shader.PropertyToID("_HeightFogZenith");
    static readonly int HeightFogSunColorId = Shader.PropertyToID("_HeightFogSunColor");
    static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
    static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    static readonly int MoonColorId = Shader.PropertyToID("_MoonColor");

    static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");



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

    Vector3 fogDrift;
    float activeCloudBottom;
    float airThinning = 1f;
    Color shadowColor;

    /// Bulutların okuduğu gök tonları. Yalnız RENK taşırlar; parlaklık taban renkten
    /// gelir, çünkü radyans ile ışınım arasında π kat fark var ve doğrudan kullanmak
    /// bulutları karartıyor.
    Color skyBright = Color.white, skyShade = Color.gray;

    Color zenith, targetZenith;
    float nextEditorApply;
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





    public float Visibility => effectiveVisibility > 0f ? effectiveVisibility : visibility;

    /// Hata ayıklama paneli ayarları canlı değiştirebilsin diye açık.
    public AtmosphereSettings Settings => settings;

    /// KÜRESEL BULUT KAPSAMASI, 0-1. Gök rengi, sis, yıldız yoğunluğu ve yansıma seviyesi
    /// bunu kullanıyor; hacimsel bulut sistemi de `CloudWeatherDriver` üzerinden bunu
    /// okuyor.
    ///
    /// TEK EŞLEME. Kural burada duruyor (fırtına kütlesi, kuru hava ritmi, açık pencere,
    /// test kilidi) ve bulut onu tüketiyor. İki yerde iki eşleme olsaydı gökyüzü "kapalı"
    /// derken bulutlar "açık" diyebilirdi.
    public float Coverage => coverage;

    // `CloudBottom` ve `CloudTop` KALDIRILDI: silinen bulut sistemine aitti, gökyüzünde
    // çizilenle ilgisi kalmamıştı. Tüketicileri artık `CloudLayerProbe`'dan okuyor.

    public void Bind(AtmosphereSettings source, WeatherState weatherState, WindField windField,
        TimeOfDay timeOfDay, AltitudeWeatherDriver driver, Camera camera, Material sky)
    {
        settings = source;
        weather = weatherState;
        wind = windField;
        time = timeOfDay;
        weatherDriver = driver;
        view = camera;
        skyMaterial = sky;

        Initialize();
    }

    /// ExecuteAlways yüzünden OnEnable, AddComponent anında çalışır — o an Bind henüz
    /// çağrılmamış olabilir.
    void OnEnable() => Initialize();

    void Initialize()
    {
        if (settings == null || weather == null || wind == null || time == null
            || weatherDriver == null) return;

        initialized = false;
        Apply();
    }

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

        // YAĞMURUN GÖRÜŞÜ SABİT DEĞİL, YAĞIŞ ORANINDAN TÜRÜYOR.
        //
        // `V(m) = 18000 · R^(−0.70)`, R yağış oranı (mm/sa).
        //
        // KATSAYI ÖLÇÜLEN VERİYE UYDURULDU. Bir dönem `1900 · R^(−0.63)` yazıyordu ve
        // yedi kat fazla kapatıyordu — büyük ihtimalle birim karışıklığı, "1.9 km"
        // katsayısı 1900 m diye alınmış. Milano'da ölçülen optik sönümleme değerleri
        // (Hameed ve diğ., FSO çalışması) bunu çürütüyor:
        //
        //   R =  25 mm/sa → 7.3 dB/km  → görüş 2330 m   (eski formül 387 m diyordu)
        //   R =  50 mm/sa → 14.6 dB/km → görüş 1160 m   (eski formül 162 m)
        //   R = 100 mm/sa → 23.8 dB/km → görüş  714 m
        //
        // Dönüşüm `σ = A/4.343`, `V = 3.912/σ` (Koschmieder). Uydurma R = 50 ve 100'de
        // %0, R = 25'te %19 sapıyor.
        //
        // BU YALNIZ YAĞMURUN KENDİ SÖNÜMLEMESİ. Yağmurlu havanın puslu HİSSİ alçak
        // bulut ve nemden geliyor; onlar bulut/tavan zincirinde ayrıca var. İkisini
        // burada toplamak yağmuru sis yerine koymak olurdu.
        // Oran `PrecipitationRenderer`ın damla dağılımıyla AYNI eşlemeden geliyor
        // (şiddet 1.0 = 50 mm/sa), yoksa damlalar bir yoğunluğu, hava başka bir
        // yoğunluğu anlatırdı.
        //
        // Eskiden `rainVisibility = 900 m` sabitti ve şiddetle DOĞRUSAL harmanlanıyordu.
        // Ölçüldü: tam yağmurda ekranda 2063 m görüş vardı, fizik 167 m diyor — yağmur
        // havayı neredeyse hiç puslandırmıyordu ve yağmurun DERİNLİĞİ yoktu (yakındaki
        // izler doğru, uzağı bomboş). Kullanıcı bildirdi.
        //
        // Üstel bağıntı şeklin kendisini de düzeltiyor: hafif yağmur görüşü az kapatır,
        // sağanak sert kapatır. Doğrusal harman ikisini de yanlış veriyordu.
        float rainRate = 50f * precipitation;                      // mm/sa
        float rainVisibility = rainRate > 0.01f
            ? 18000f * Mathf.Pow(rainRate, -0.70f)
            : settings.clearVisibility;

        // Sabit yerinde kalıyor.
        float wet = rainVisibility;
        float targetVisibility = Mathf.Min(settings.clearVisibility,
            Mathf.Lerp(settings.clearVisibility, wet, Mathf.Min(1f, precipitation * 4f)));

        // Rüzgâr savurdukça görüş kapanır — tipinin asıl etkisi budur. Yalnızca yağış
        // varken anlamlı: açık havada rüzgâr görüşü kapatmaz.
        //
        // KARLILIKLA AĞIRLIKLI. Savrulan kar gerçekten görüşü öldürür: taneler yerden
        // kalkıp havada asılı kalır ve sönümleme yağışın kendi katkısının kat kat üstüne
        // çıkar. Yağmurda böyle bir mekanizma yok — rüzgâr damlayı eğer ve hızlandırır,
        // ama havada asılı su miktarını artırmaz; sönümleme yağış oranından gelir.
        //
        // Ölçüldü: ağırlıksız hâlde rüzgâr 0.95 ve yağış 1.0'da kapanma 0.62, yani görüş
        // 1164 m'den 445 m'ye düşüyordu — yağmurun kendi sönümlemesinin iki buçuk katı
        // bir kesinti, kaynağı da yalnızca rüzgâr.
        float closure = wind.Strength * settings.windClosure * precipitation
                      * 0.2f;
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
        Color dayColor = Color.Lerp(settings.clearDay, settings.rainDay, precipitation);
        Color nightColor = Color.Lerp(settings.clearNight, settings.rainNight, precipitation);
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

        // SEVİYE GÖKTEN, TON SABİTTEN. Yukarıdaki sabitler (`clearDay`, `clearNight`,
        // yağış/kar varyantları, şafak paleti) artık yalnız TON taşıyor; parlaklığı
        // gökyüzünün kendi ölçüsü belirliyor.
        //
        // Eskiden seviye de sabitten geliyordu ve ölçüldü: gök gündüz–gece arasında ~230
        // kat değişirken sis rengi 9.6 kat değişiyordu. Sonuç, sabitin tek bir hava
        // koşulunda doğru olup geri kalan her yerde kayması:
        //   gündüz  probe DC 0.469 → sis 0.672 → oran 1.43  (2.2 kat FAZLA KOYU)
        //   gece    probe DC 0.0020 → sis 0.0698 → oran 34.6 (11.0 kat FAZLA PARLAK)
        // Gece sisin örttüğü her şey 3.5 durak yukarı kayıyordu; "sis kapalıyken gördüğüm
        // gece gerçekçi" gözlemi tam olarak buydu.
        //
        // Oran 3.15 bu projenin KENDİ ölçümü (froxel sisinin ortam kaynağı araştırması):
        // probe yüzey aydınlatması birimindedir, katılımcı ortam radyans ister, dönüşüm π.
        // TEK KATSAYI, ÜÇ RENGE. Her rengi ayrı ayrı hedefe oturtmak aralarındaki
        // ORANLARI ezerdi: zenit'in yağışa bağlı payı (berrak havada 0.55, yağışta 1.0)
        // ve gölge tarafının şafak payı o oranların içinde duruyor.
        float scale = LevelScale(targetColor, AmbientLevel() * AmbientToMedium);

        targetColor *= scale;
        targetZenith *= scale;
        shadowColor *= scale;

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

        // ORTAM IŞIĞI, GÖKYÜZÜ VE YANSIMA ARTIK PAKETTE. `PhysicallyBasedSkyURP` skybox'ı,
        // ambient probe'u ve yansıma küpünü kendi LUT'larından pişiriyor; burada ikinci
        // bir yazar olsaydı sonuç kare içindeki yazma sırasına kalırdı.
        //
        // Buradan türeyen `color` duruyor: yükseklik sisi, bulut tonu ve ses/renk
        // düzenlemesi onu okuyor.

        if (view != null) view.clearFlags = CameraClearFlags.Skybox;

        ApplyShadowDistance();
        ApplySky(precipitation);
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
    void ApplySky(float precipitation)
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
        // Kapsamanın tabanı SÜRÜCÜDEN geliyor: yağış sıfırken bile gökyüzü kendi
        // ritmiyle kapanıp açılıyor (bkz. `AltitudeWeatherDriver.DryCoverage`). Sabit bir
        // taban, yağmadığı her an aynı gökyüzü demekti.
        float floor = Mathf.Max(settings.minCoverage, weatherDriver.DryCoverage);

        // Tek istisna açık pencere — sürücünün nadiren açtığı o an, tabanın delinebildiği
        // tek yol.
        coverage = Mathf.Max(coverage,
            Mathf.Lerp(floor, settings.openCoverage, weatherDriver.ClearWindow));

        // BULUT SÜRÜKLENMESİ BURADAN ÇIKTI. Kaymayı artık hacimsel bulut sistemi
        // biriktiriyor ve rüzgârı `CloudWeatherDriver` üzerinden doğrudan okuyor; buradaki
        // yumuşatılmış yön/hız çifti kimse tarafından okunmuyordu.
        // Konvektif yükselme gündüz sürer: kaynağı ısınan zemindir. Gece zemin
        // soğur, yükselme durur — bulutlar yalnız sürüklenir.


        // Gökyüzü gradyanı artık materyale gitmiyor: gökyüzü, sisle aynı AirColor
        // fonksiyonunu okur ve o fonksiyon _HeightFog* globallerinden beslenir.

        // Kadranın rengi ışığın rengiyle aynı; bulut kalınlaştıkça güneş perdelenir
        float veil = 1f - coverage * 0.75f;
        skyMaterial.SetColor(SunColorId, time.CurrentSunColor * veil);
        skyMaterial.SetColor(MoonColorId, time.MoonTint * veil);

        // (BULUT DOKU YAYINLARI SİLİNDİ — gürültü ve hava haritası yeniden yazılıyor.)
        //
        // `_CloudBottom` aşağıda yazılmaya DEVAM ediyor: şimşek shader'ı çakmayı bulut
        // tabanı küresiyle kesiştiriyor. Bulut DURUMU (kapsama, taban, tavan, sürüklenme)
        // bu bileşende kalıyor — hava modelinin çıktısı, render tesisatı değil. Yeni
        // bulut sistemi bunları okuyacak; bağların listesi `CLOUDS_REBUILD.md`'de.

        // `_SunDirection` yükseklik sisinin ışık yönü. `_MoonDirection` SİLİNDİ: onu
        // yalnız `Sky.shader` okuyordu, o da artık skybox değil.
        Shader.SetGlobalVector(SunDirectionId, time.SunDirection);

        // Makaslama sabit bir mesafe: katman kalınlığının oranı kadar yanal kayma
        // BOYUTSUZ: shader katman kalınlığıyla çarpıyor. Burada çarpılıyordu ve katman
        // 5.3 km'yken ötelenme 2927 m oluyordu — tipik bulutun eninden büyük, kolonun
        // tepesi tabanının yanından çıkıyor, dönmeyle birlikte kancaya dönüşüyordu.
        // Katman 2.5 km olunca aynı oran 1500 m veriyor: bulutun kendi ölçeğinde.

        // Yön dönmesi rüzgâr şiddetiyle azalır: sert rüzgârda hava kütlesi bütün
        // katmanda aynı yöne sürüklenir, sakin havada sapma belirginleşir.

        // Bulut renkleri atmosferin renginden türer: şafakta kızıllık buluta da geçer
        // Çarpan bire yakın tutulur: renk zaten doygun geldiği için 1.5 ile çarpmak
        // kırmızı kanalı taşırıp bulutu beyaza çeviriyordu. Parlaklık ambient şiddetinden
        // geliyor, renkten değil.
        // BULUT RENGİ GÖKTEN, PARLAKLIK ESKİ TABANDAN. Gök radyansını doğrudan vermek
        // denendi ve bulutlar simsiyah oldu: bulutu aydınlatan şey zenit RADYANSI değil
        // gökten gelen toplam IŞINIM, arada π kat fark var. Radyansın taşıdığı doğru
        // bilgi RENK; parlaklık zaten kalibre edilmiş taban rengin işi. İkisi ayrılınca
        // bulut şafakta turuncuya döner ama kararmaz.
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


        // Yüksek katman hacimsel örtü kapandıkça sönümlenir: altından görünmez zaten,
        // çizmek boşuna. Cinsi ayarın kendisi seçer.
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

        // `_CloudBottom` ve `_CloudTop` BURADAN YAYINLANMIYOR. Katmanın gerçek kotlarını
        // yalnız bulut sistemi biliyor; `CloudLayerProbe` yayınlıyor (bağ 8). Buradaki
        // `activeCloudBottom` eski modelin kendi değeri ve yalnız sis/gök için duruyor.
        Shader.SetGlobalFloat(PlanetRadiusId, settings.planetRadius);


        // Fırtınada bulut yalnızca gökyüzünü kaplamaz, kalınlaşır da. Kapsamayla aynı
        // kaynaktan: kalınlık da katmanın kendi durumu.
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

        // PERDE GÖRÜŞ MESAFESİNİ BURADAN OKUR. Sisin opaklığını ışın boyunca integre
        // etmek yerine tek üstelle türetiyor; sahibi burası, ikinci bir kaynak yok.

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


        Vector3 flow = wind.Velocity;

        Shader.SetGlobalVector(WindVectorId,
            new Vector4(flow.x, flow.y, flow.z, wind.Gust));
    }

    /// HeightFog.hlsl'deki FogBankAt ile aynı alan, çarpansız hâli (0..1).
    /// Formül değişirse ikisi birlikte değişmeli — iki tüketici, tek alan.
    static float BankField(Vector2 p)
    {
        // ÇARPIM DEĞİL TOPLAM — gerekçe `VolumetricFogShared.hlsl → FogBankAt` içinde.
        // Bileşenler oradakiyle BİREBİR aynı olmak zorunda: iki tüketici, tek alan.
        float s = Mathf.Sin(Vector2.Dot(p, new Vector2( 0.003534f,  0.001081f))) * 0.34f
                + Mathf.Sin(Vector2.Dot(p, new Vector2( 0.001090f,  0.005607f))) * 0.26f
                + Mathf.Sin(Vector2.Dot(p, new Vector2(-0.005424f,  0.006239f))) * 0.20f
                + Mathf.Sin(Vector2.Dot(p, new Vector2(-0.011122f, -0.004720f))) * 0.13f
                + Mathf.Sin(Vector2.Dot(p, new Vector2( 0.005250f, -0.017167f))) * 0.07f;

        return Mathf.Clamp01(0.5f + 0.5f * s);
    }

    /// Gökyüzünden pişen ortam probunun DC terimi. `SkyAmbientBaker` onu her kare
    /// gökyüzü materyalinden pişiriyor, yani zincir tek yönlü: gök → probe → sis rengi.
    /// Sis hacminin ortam kaynağıyla AYNI büyüklük (`sh[c,0] - sh[c,6]`) — iki tüketici
    /// aynı sayıyı görmezse hacim ile analitik kuyruk gece ayrışır.
    static float AmbientLevel()
    {
        SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;

        return Mathf.Max(0f,
            0.2126f * (probe[0, 0] - probe[0, 6])
          + 0.7152f * (probe[1, 0] - probe[1, 6])
          + 0.0722f * (probe[2, 0] - probe[2, 6]));
    }

    /// Taban rengi hedef parlaklığa oturtan katsayı. Ton kaynaktan, seviye ölçümden —
    /// projenin "bir değere bağlanmadan önce" kuralının gereği. Katsayı döndürüyor,
    /// rengi değil: çağıran onu birden çok renge uygulayıp aralarındaki oranı koruyor.
    static float LevelScale(Color reference, float target)
    {
        float current = 0.2126f * reference.r + 0.7152f * reference.g + 0.0722f * reference.b;

        return current < 1e-6f ? 1f : target / current;
    }
}
