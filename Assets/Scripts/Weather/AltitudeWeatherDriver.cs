using System;
using UnityEngine;

/// Havanın yönetmeni. Tek girdi oyuncunun **yüksekliği** — kat edilen mesafe değil.
/// Dağın etrafında yükselmeden dolaşmak havayı sertleştirmez.
///
/// Kuşaklar:
///   açılış      → çok hafif yağmur, neredeyse rüzgârsız
///   yağmur      → kademeli sertleşir, kar yok
///   geçiş       → yağmur çekilir, kar yerleşir
///   prosedürel  → bazen tipi, bazen sakin kar
///   zirve       → dalgalanma kapanır, sürekli tam fırtına
public class AltitudeWeatherDriver : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] Transform observer;
    [SerializeField] TimeOfDay time;
    [Tooltip("Donma seviyesi buradan gelir; sürücü kendi sıcaklık modelini kurmaz.")]
    [SerializeField] TemperatureField temperature;
    [SerializeField] WeatherDriverSettings settings;

    [Header("Kuşak sınırları (metre) — hepsini bootstrap hesaplar")]
    [Tooltip("Düz arazinin kotu.")]
    [SerializeField] float groundAltitude;
    [Tooltip("Yağmurun tam şiddete ulaştığı yükseklik. DEĞİŞKEN: " +
             "donma seviyesiyle birlikte iner ve çıkar.")]
    [SerializeField] float rainCeiling;
    [Tooltip("Yüksek fırtına kuşağının başladığı yükseklik. DEĞİŞKEN.")]
    [SerializeField] float stormFloor;
    [Tooltip("Donma seviyesinin uzun vadeli ortalaması. Kalıcı kar çizgisi buradan " +
             "türer — hareketli sınırdan türeseydi buzul da gelgit yapardı.")]
    [SerializeField] float referenceRainCeiling;
    [SerializeField] float referenceStormFloor;
    [Tooltip("Dalgalanmanın kapanıp sürekli fırtınanın başladığı yükseklik.")]
    [SerializeField] float stormPeakAltitude;
    [Tooltip("Dağın zirvesi. Yalnızca tırmanış göstergesi okuyor.")]
    [SerializeField] float summitAltitude;

    // Kuşaklar dağın yüksekliğine oranla tanımlanır: dağ değişince kotlar kendiliğinden
    // kayar, tırmanışın hangi kısmının fırtınalı olduğu sabit kalır.
    //
    // Serileştirilmiyorlar. Serileştirilen bir alanın sahnedeki kopyası kod varsayılanını
    // eziyor: kuşak sınırları bir kez yanlış değerle kaydedildikten sonra kodu değiştirmek
    // hiçbir işe yaramıyordu ve fark ancak oyunda görüldü.
    const float RainShare = 0.10f;    // dağın bu kadarı yalnızca yağmur
    const float UpperBandShare = 0.04f;   // üstündeki sulu kar kuşağının genişliği

    // Perlin teorik olarak 0-1 ama bir çizgi boyunca örneklenince pratikte ~0.30-0.70
    // arasında geziyor; uçlara neredeyse hiç varmıyor. Ham değere eşik koymak bu yüzden
    // yanıltıcı: 0.80'lik bir eşik hiçbir zaman aşılmıyor ve pencere hiç açılmıyordu.
    // Önce bu aralık tam genişliğe açılır, eşikler ondan sonra anlam kazanır.
    const float NoiseFloor = 0.30f;
    const float NoiseCeiling = 0.70f;

    // Açık pencerenin eşiği, normalize edilmiş gürültü üzerinde.
    const float WindowOpen = 0.65f;
    const float WindowBand = 0.15f;

    // Zirvede eşik yükselir. Pencere seyrekleşir ama açıldığında tam açılır — genliği
    // kısmak yanlış olurdu, zayıf ve sık değil seyrek ve tam olmalı.
    const float SummitWindowOpen = 0.85f;

    // Yağış bulut **kütlesinin** bittiği yerde söner, katmanın nominal tavanında değil.
    //
    // Yoğunluk profili tavana varmadan sıfırlanıyor: en kabarık bulut bile kendi
    // tepesinin %55'inden itibaren sönmeye başlıyor, yayvan olanlar katmanın alt
    // üçte birinde bitiyor. Tavana yaslanmış bir sönüm bu yüzden geç kalıyordu —
    // oyuncu bulut denizinin üstünde dururken üstüne kar düşmeye devam ediyordu.
    //
    // Kütlenin tavandan ne kadar aşağıda bittiği ve sönümün ne kadar bir bantta
    // olduğu. Sönüm tavanın bu kadar altında biter, bandı da onun altında başlar.

    float intensity;
    float cloudMass;
    float windowRoll;
    float driftCombined;
    float progressAltitude;
    bool initialized;

    /// Fırtınanın ham şiddeti: bulut tavanının üstünde sönmemiş hali.
    ///
    /// Bulut kapsaması ve kalınlığı bunu okur, `WeatherState.Precipitation`'ı değil.
    /// Yağış senin üstünde bulut kalmadığı için diner ama altındaki deniz aynı fırtınanın
    /// denizidir; sen yükseldin diye incelmesi anlamsız olurdu.
    public float StormIntensity => intensity;

    /// Bulut kütlesinin gördüğü şiddet. Yağışın *geciken* hali: yağış kesildiğinde bulut
    /// hemen dağılmaz, yeniden başladığında hemen toplanmaz. Kısa açık pencereler bu
    /// yüzden gökyüzünü açmadan geçer, uzun olanlar açar — hangisi olacağı pencerenin
    /// süresine bağlı, ayrı bir kurala değil.
    public float CloudMass => cloudMass;

    /// KURU HAVA BULUTLULUĞU. Yağış sıfırken bile gökyüzü boş durmaz: alçak basınç
    /// geçer, nem taşınır, kapsama saatler içinde gezinir. Yağıştan BAĞIMSIZ ama aynı
    /// zaman çizgisinde — ayrı bir rastgelelik kaynağı değil, sürücünün kendi saati.
    ///
    /// Atmosfer bunu kapsamanın TABANI olarak okuyor: yağış geldiğinde kapsama zaten
    /// yükseliyor, bu değer yalnız "yağmıyorken gökyüzü ne kadar kapalı" sorusunu
    /// cevaplıyor.
    public float DryCoverage { get; private set; }

    /// Bulut sütununun tepesi (metre). Atmosfer her karede iter — gerçek yüksekliğini
    /// yalnızca o biliyor (hava haritası + o anki bulut tabanı). Sürücü çekmiyor ki
    /// iki sistem birbirine referansla bağlanmasın. Skaler bir kesme payı yerine KOT
    /// gönderiliyor: kar profili her bandın kesmesini kendi kotundan hesaplıyor.
    public float CloudColumnTop { get; set; } = float.PositiveInfinity;

    /// Açık pencere tam açıldığında yağıştan geriye kalan pay. Yalnızca gösterge.
    public float WindowResidue { get; private set; }

    /// 0 = kapalı, 1 = hava tamamen açık. Nadir ve kısa sürer.
    ///
    /// Bulut kapsaması bunu okur ve kalıcı alt sınırının altına iner — o sınırı geçebilen
    /// tek yol budur. İki kural yoksa çelişiyordu: sürücü "bulutlar aralanır, zirve
    /// görünür" diye söz veriyor, atmosfer "hiçbir yol tabanın altına inemez" diyordu.
    /// İkincisi birinciyi yutuyor ve vaat edilen an hiç gelmiyordu.
    public float ClearWindow { get; private set; }

    /// Test anahtarı: pencereyi gürültüyü beklemeden tam açar. Açılma nadir ve
    /// tahmin edilemez olduğu için etkisini görmek başka türlü dakikalar sürüyor.
    public bool ForceWindow { get; set; }

    /// Test anahtarı: ulaşılan seviye izlenmez, yumuşatma uygulanmaz. Hava anlık kota
    /// anında uyar. İkisi de gameplay'de bilinçli kurallar; bu yalnızca serbest uçuşla
    /// gezerken bir kotun havasını görmek için beklemeyi ortadan kaldırır.
    public bool Instant { get; set; }

    /// Test anahtarı: hedef şiddet dışarıdan verilir. Negatif = kapalı.
    ///
    /// Bileşeni KAPATMAK yerine bu kullanılır. Kapatılınca `intensity` donuyor ama
    /// `AtmosphereController` `StormIntensity` ve `ClearWindow`'u okumaya devam ediyor:
    /// sürgü oynatılırken yağış, görüş, sis ve renk izliyor ama bulut kapsaması,
    /// kalınlığı, yağmur soğurması ve yüksek katman kilitlenme anındaki değerde
    /// donuyordu. Tek durum iki kanala ayrılıyor ve çelişiyordu.
    public float IntensityOverride { get; set; } = -1f;

    /// Havanın baktığı yükseklik: anlık Y değil, tırmanışın ulaştığı seviye.
    public float ProgressAltitude => progressAltitude;

    /// Sürekli fırtınanın başladığı yükseklik.
    public float BlizzardAltitude => stormPeakAltitude;

    /// Donma seviyesinin uzun vadeli ortalamasından türeyen sabit referans. Kalıcı kar
    /// çizgisi bunu okur: buzul hava durumuyla gelgit yapmaz.
    public float ReferenceStormFloor => referenceStormFloor;

    /// Yağışın kar olarak düşmeye başladığı ve tamamen kara döndüğü kotlar.
    /// Yüzeyin taze karı da bu bandı izler: aşağıda yağmur yağarken zemin beyazlamamalı.
    public float RainCeiling => rainCeiling;
    public float StormFloor => stormFloor;

    /// Dağın gerçek zirvesi. Yalnızca gösterge için.
    public float SummitAltitude => summitAltitude;

    /// Duz arazinin kotu. Kar profili bant araligini buradan kuruyor.
    public float GroundAltitude => groundAltitude;

    public void Bind(WeatherState state, WindField windField, Transform target,
        TimeOfDay clock, TemperatureField thermometer,
        WeatherDriverSettings tuning, float ground, float peak)
    {
        weather = state;
        wind = windField;
        observer = target;
        time = clock;
        temperature = thermometer;
        settings = tuning;

        groundAltitude = ground;
        summitAltitude = peak;

        float height = Mathf.Max(1f, peak - ground);

        // Tırmanışın alt kısmı yağmurda, üstü karda geçer. Aradaki sulu kar kuşağı dar:
        // ikisi de "sadece" olmalı, geçiş bir bant değil bir sınır gibi okunmalı.
        referenceRainCeiling = ground + height * RainShare;
        referenceStormFloor = referenceRainCeiling + height * UpperBandShare;
        rainCeiling = referenceRainCeiling;
        stormFloor = referenceStormFloor;

        // Zirve fırtınası son 1000 metrede. Dağ değişse de kendiliğinden kayar.
        stormPeakAltitude = Mathf.Max(referenceStormFloor + 200f, peak - 1000f);
    }

    void OnEnable()
    {
        if (weather == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(weather)} atanmadı.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(wind)} atanmadı.");
        if (settings == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(settings)} atanmadı.");
        if (observer == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(observer)} atanmadı.");
        if (time == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(time)} atanmadı.");
        if (temperature == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(temperature)} atanmadı.");

        initialized = false;
    }

    void Update()
    {
        float altitude = TrackProgress(observer.position.y);

        SampleNoise();
        UpdateFreezingLevel();

        bool overridden = IntensityOverride >= 0f;

        float target = overridden ? IntensityOverride : Baseline(altitude) * Variation(altitude);

        // Hedef ne kadar zıplarsa zıplasın gerçek değer kayarak varır: sağanaktan bir
        // anda dingin havaya geçmek fiziksel olarak imkânsız olur.
        if (!initialized || Instant || overridden)
        {
            intensity = target;
            cloudMass = target;
            initialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, settings.smoothingSeconds));
            intensity = Mathf.Lerp(intensity, target, t);

            // Kütlenin kendi, çok daha yavaş zaman sabiti var. Aynı değerden sürülünce
            // bulutlar yağışla birlikte anında inceliyordu: yağış duruyor, aynı karede
            // gökyüzü açılıyordu. Gerçekte bulut yağıştan sonra da bir süre durur.
            float m = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, settings.cloudLagSeconds));
            cloudMass = Mathf.Lerp(cloudMass, target, m);
        }

        // Kuru hava bulutluluğu: yavaş gezinen tek boyutlu bir alan. Perlin sürekli ve
        // türevlenebilir, yani kapsama zıplamıyor; periyot dakikalar mertebesinde.
        float wander = Mathf.PerlinNoise(Time.time / Mathf.Max(1f, settings.cloudWanderSeconds), 0.37f);
        DryCoverage = Mathf.Lerp(settings.dryCoverageLow, settings.dryCoverageHigh, wander);

        // Bulut tavanının üstünde yağış olamaz: tepende bulut yoksa kar düşmez.
        // Sütunun tepesi ATMOSFERDEN İTİLİR (CloudColumnTop), buradan hesaplanmaz. Burada
        // ayarın nominal tavanı (7000 m) kullanılıyordu; kütlenin gerçek tepesi hava
        // haritasının o noktadaki sütun yüksekliğiyle belirleniyor ve çoğu yerde çok
        // daha alçak. Nominal değere göre sönme 5800 m'de başlıyordu, zirve 5686 m —
        // yani kural hiç işlemiyordu: bulut denizinin üstünde de yağış devam ediyordu.
        ClearWindow = WindowAt(altitude);

        weather.Set(intensity * CeilingAt(observer.position.y));

        // Rüzgâr aynı değere bağlı: yağış sertleşirken rüzgâr da sertleşir,
        // chill ara geldiğinde ikisi birlikte diner.
        // Taban bir alt sınır; ölçek olarak kullanmak dingin anları da yukarı itiyordu.
        // Sönmemiş şiddetten sürülür: bulutların üstü yağışsızdır ama rüzgârsız değil,
        // zirve yağmasa da acımasız kalır.
        wind.Severity = Mathf.Max(settings.windAtBase, intensity);
    }

    /// Dağ sürekli yükselmez: sırtı aşıp boyuna inersin, sonra tekrar çıkarsın.
    /// Anlık yüksekliğe bakılırsa hava her inişte geri sarar. Bunun yerine tırmanışın
    /// ulaştığı seviye izlenir: yukarı anında, aşağı ölü bant ve gecikmeyle.
    float TrackProgress(float altitude)
    {
        if (!initialized) progressAltitude = altitude;

        // Test anahtarı açıkken izleme yok: bir kotun havasını görmek için oraya uçup
        // ölü bandın ve geri çekilmenin geçmesini beklemek gerekmesin
        if (Instant)
        {
            progressAltitude = altitude;
            return progressAltitude;
        }

        if (altitude > progressAltitude)
        {
            progressAltitude = altitude;
            return progressAltitude;
        }

        // Ölü bandın içindeki inişler havayı hiç etkilemez
        float floor = altitude + settings.descentDeadband;
        if (progressAltitude <= floor) return progressAltitude;

        // Bandı da aşan gerçek iniş: kamp için aşağı inildiğinde hava yumuşasın
        float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, settings.descentSeconds));
        progressAltitude = Mathf.Lerp(progressAltitude, floor, t);

        return progressAltitude;
    }

    /// Donma seviyesi sabit değildir: soğuk cephe onu aşağı iter, öğle ısınması yukarı.
    /// Kar sınırının fırtınada inip sonra çekilmesi buradan gelir — sabit sınırla dağda
    /// gözle görülür hiçbir şey değişmiyordu.
    ///
    /// Girdi olarak *geçen karenin* yumuşatılmış şiddeti kullanılıyor. Şiddet sınırdan,
    /// sınır şiddetten besleniyor; halka bir kare gecikme ve dakikalar ölçeğindeki
    /// yumuşatmayla sönümleniyor.
    void UpdateFreezingLevel()
    {
        // DONMA SEVİYESİ SICAKLIKTAN GELİYOR, ayrı bir formülden değil. Burada
        // "referans kot − fırtına düşüşü + gündüz yükselişi" diye örtük bir sıcaklık
        // modeli vardı: sıcaklığın kendisi hiç var olmadan sonuçları hesaplanıyordu.
        // Nefes, üşüme, donma gibi her yeni özellik kendi tahminini uyduracaktı.
        //
        // Sayılar değişmedi, ifade değişti: eski metre kaymaları °C'ye çevrildi ve
        // 6.5 °C/km düşüşle aynı kotları veriyor.
        float target = temperature.FreezingLevel;

        if (!initialized || Instant)
        {
            rainCeiling = target;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime
                / Mathf.Max(0.01f, settings.freezingSmoothSeconds));
            rainCeiling = Mathf.Lerp(rainCeiling, target, t);
        }

        // Sulu kar bandının genişliği sabit: sınır kayarken bandın kendisi daralıp
        // genişlemez, olduğu gibi taşınır.
        stormFloor = rainCeiling + (referenceStormFloor - referenceRainCeiling);
    }

    /// Yükseklikten gelen zemin şiddet. Kuşak köşeleri arasında doğrusal geçer.
    ///
    /// Köşeler REFERANS kotlardan okunur, hareketli donma seviyesinden değil. İkisi
    /// farklı fizik: bu eğri orografik yağış profili (yükseldikçe daha çok yağar),
    /// donma seviyesi ise sıcaklık. Hareketli sınıra bağlıyken zirvede şiddet 1.0'a
    /// çıkınca sınır açılış platosunun altına iniyor ve eğri orada kopuyordu —
    /// 14 metrede 0.12'den 0.41'e sıçrama. Ayrıca bu ayrım kar sınırının zemine kadar
    /// inmesini serbest bırakıyor: şiddet eğrisi bundan etkilenmiyor.
    float Baseline(float altitude)
    {
        float openingEnd = groundAltitude + settings.openingRise;

        if (altitude <= openingEnd) return settings.openingIntensity;

        if (altitude < referenceRainCeiling)
            return Mathf.Lerp(settings.openingIntensity, settings.rainPeak,
                Mathf.InverseLerp(openingEnd, referenceRainCeiling, altitude));

        // Geçiş kuşağı: yağmur tavanından fırtınanın sakin tabanına iner
        if (altitude < referenceStormFloor)
            return Mathf.Lerp(settings.rainPeak, settings.stormBase,
                Mathf.InverseLerp(referenceRainCeiling, referenceStormFloor, altitude));

        if (altitude < stormPeakAltitude)
            return Mathf.Lerp(settings.stormBase, settings.stormPeak,
                Mathf.InverseLerp(referenceStormFloor, stormPeakAltitude, altitude));

        return 1f;
    }

    /// Gürültüler karede BİR KEZ örneklenir. Dalgalanma artık her kot için ayrı
    /// sorulabiliyor (kar profili 128 kot bandı soruyor); Perlin'i her sorguda yeniden
    /// çağırmak hem israf hem de yan etki kaynağıydı: ClearWindow son sorulan kotun
    /// değerine kayıyordu.
    void SampleNoise()
    {
        float t = Time.time;

        windowRoll = Mathf.InverseLerp(NoiseFloor, NoiseCeiling,
            Mathf.PerlinNoise(t * settings.clearWindowFrequency, 77.3f));

        // PENCERENİN DERİNLİĞİ DE DEĞİŞKEN. Sabit bir kalıntıyla (0.15) her açılma
        // birbirinin aynısıydı. Kendi yavaş gürültüsü olunca pencereler birbirine
        // benzemiyor: çoğunda yağış tamamen kesilir, bazılarında çiselemeye devam eder.
        // Frekans pencereninkinden ayrı — aynı olsa ikisi kilitlenirdi.
        float residueRoll = Mathf.InverseLerp(NoiseFloor, NoiseCeiling,
            Mathf.PerlinNoise(t * settings.clearWindowResidueFrequency, 12.9f));

        // Eğri kareli: kalıntı çoğunlukla sıfıra yakın, ara sıra yukarı çıkıyor.
        WindowResidue = settings.clearWindowResidue * residueRoll * residueRoll;

        // Gürültü kuşaklar arasında sıfırlanmaz: tek sürekli akış, faz kırılmaz
        float slow = Mathf.PerlinNoise(t * settings.slowFrequency, 0f);
        float fast = Mathf.PerlinNoise(t * settings.fastFrequency, 31.7f);
        driftCombined = slow * 0.7f + fast * 0.3f;
    }

    /// Açık pencerenin o kottaki açıklığı. Eşik zirveye doğru yükselir: pencere orada
    /// seyrekleşir ama açıldığında tam açılır.
    ///
    /// Açık pencere dalgalanmanın *dışında* hesaplanır. İçeride bırakılınca zirvede
    /// genlik sıfıra indiği için erken çıkılıyor ve pencere tam da en çok istendiği
    /// yerde hiç açılmıyordu: bulut denizinin üstünde durma anı hiç oluşmuyordu.
    float WindowAt(float altitude)
    {
        if (ForceWindow) return settings.clearWindowStrength;

        float summit = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(stormPeakAltitude - 300f, stormPeakAltitude, altitude));
        float open = Mathf.Lerp(WindowOpen, SummitWindowOpen, summit);

        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(open, open + WindowBand, windowRoll))
               * settings.clearWindowStrength;
    }

    /// Zamanla değişen çarpan. İki hızda gürültü üst üste binerek tekdüzeliği kırar.
    /// Saf: aynı karede aynı kot için hep aynı değeri döner.
    float Variation(float altitude)
    {
        float amount = VariationAmount(altitude);
        float multiplier = 1f + (driftCombined - 0.5f) * 2f * amount;

        // Pencerenin etkisi dalgalanma genliğinden bağımsız. Genlikle ölçeklenince zirve
        // kuşağına girerken 300 metrede sönüyor, zirvede ise ölçek olmadığı için tam
        // etkiye sıçrıyordu: sınırı geçtiğin anda gökyüzü açılıyordu.
        return multiplier * Mathf.Lerp(1f, WindowResidue, WindowAt(altitude));
    }

    /// Yağışın o kotta kar olarak düşen payı.

    /// Bulut sütununun tepesine göre yağışın o kotta hayatta kalan payı.
    public float CeilingAt(float altitude) =>
        1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(CloudColumnTop, CloudColumnTop + 300f, altitude));

    float VariationAmount(float altitude)
    {
        // Bu da referanstan: dalgalanmanın genliği hava kütlesinin karakteri, kar
        // sınırının o anki yeri değil.
        if (altitude < referenceRainCeiling) return settings.rainVariation;

        // Zirve kuşağına girerken genlik 300 metrede DARALIR — sıfırlanmaz. Sıfır olunca
        // zirvede şiddet 1.00'e çakılıyordu: yükseklik kazandıkça hava tek bir sabit
        // sağanağa dönüşüyor, saatlerce hiçbir şey değişmiyordu. Taban genlikle bile
        // aralık dar (0.70-1.00): zirve hâlâ acımasız, ama ölü değil.
        float fade = Mathf.Lerp(1f, settings.summitVariation, Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(stormPeakAltitude - 300f, stormPeakAltitude, altitude)));

        return Mathf.Lerp(settings.rainVariation, settings.stormVariation,
            Mathf.InverseLerp(referenceRainCeiling, referenceStormFloor, altitude)) * fade;
    }
}
