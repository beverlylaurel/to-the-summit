using System;
using UnityEngine;
using UnityEngine.Rendering;

/// Yağışı tek draw call ile çizer. Tanecik konumları CPU'da tutulmaz;
/// vertex shader zaman + rüzgâr kaymasından üretir.
///
/// Damla ve kar tanesi ayrı popülasyonlardır: her taneciğin türü sabittir, kendi hızıyla
/// düşer ve kendi şeklinde çizilir. Karlılık yalnızca oranı belirler — sulu kar gerçekte
/// de damlanın taneye dönüşmesi değil, ikisinin bir arada bulunmasıdır.
public class PrecipitationRenderer : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] Shader shader;
    [Tooltip("Bulut katmanının tek kaynağı. Yağış kalın sütunların altına düşer.")]
    [SerializeField] CloudLayerProbe cloudLayer;
    [Tooltip("Yağışın örnekleneceği nokta — oyuncu.")]
    [SerializeField] Transform observer;

    [Tooltip("Garg-Nayar iz veritabanının kare başına çalışma kümesi. Boşsa yağmur " +
             "izleri veritabanından değil eski prosedürel yoldan çizilir.")]
    [SerializeField] RainStreakWorkingSet streaks;

    [Tooltip("Güneş yönünün kaynağı. İz görünümü ışık yönüne güçlü bağlı — makalenin " +
             "Senaryo 1'i aynı sahneyi 60° ve 10° azimutta belirgin farklı gösteriyor.")]
    [SerializeField] TimeOfDay timeOfDay;

    /// Kameranın pozlama süresi (saniye). Hem izin BOYUNU (veritabanı kırpması) hem
    /// ŞEFFAFLIĞINI belirliyor: `α = 2r₀/(v·T_exp)`, kısa pozlamada iz daha opak.
    /// Kare süresinden TÜREMİYOR — türeseydi yağmurun görüntüsü fps ile değişirdi.
    const float ExposureTime = 1f / 60f;

    /// Veritabanının pişirildiği salınım periyodu. `T_db = 1/60`, r₀ = 1.6 mm damlanın
    /// `2π/ω₂`'si (`rain-spec.md` §5.3).
    const float DatabasePeriod = 1f / 60f;

    /// KALİBRASYON — ÖLÇÜLDÜ. Veritabanı izleri kendi render kurulumunun radyansında
    /// (kaynak 10 m'de); bizim güneşimiz o kaynak değil, mutlak seviye taşınmıyor.
    /// `rain-spec.md` §11.3.5 bu katsayıyı zorunlu kılıyor.
    ///
    /// F1'deki "oran" probuyla İKİ TURDA ölçüldü. Damla ışık üretmiyor, gökten geleni
    /// kırıyor; hedefi ardındaki gökle eşitlenmek, yani oran 1.
    ///   ×1 → oran 0.16-0.36 (camgöbeği), damla gökten sönük
    ///   ×4 → oran 1.4 üstü (kırmızı), damla gökten parlak
    /// İkisinin arası: 4 / 1.4 ≈ 3.
    const float SourceScale = 3f;

    /// Bir taneciğin temsil ettiği damla kümesinin ekran payı. Yoğunluğumuz 0.8
    /// damla/m³, gerçek şiddetli yağmur ~1000/m³ — bin kat eksik ve o yoğunluğa çıkmak
    /// 90 milyon tanecik demek. Tanecik kendi hacmindeki kümenin payını taşıyor.
    /// ÖLÇÜLDÜ, KEYFÎ DEĞİL: 19 m kutuda 250 000 tanecik = 36 damla/m³; şiddetli
    /// yağmurun gerçeği ~1000/m³, yani oran 27. Bir tanecik 27 damlayı temsil ediyor.
    ///
    /// TAVAN DOYUMDAN: 27'nin üstünde `α_eff` 0.6'yı aşıp doyuma gidiyor ve damlalar
    /// arasındaki opaklık farkı — yani kalınlık algısı — siliniyor.
    ///
    /// Kaplamaya giriyor, geometriye değil: `α_eff = 1 − (1−α)^N`. Tek damlanın
    /// α'sı 0.02 → 0.47.
    const float Representation = 27f;

    /// Teşhis kipi F1 panelinden sürülüyor, Inspector'dan değil.
    public static int StreakProbe;

    /// Teşhiste quad'ın büyütme katsayısı. 24 m'deki iz fiziksel ölçekte 0.4 × 12
    /// piksel; 40 kat büyütme onu 16 × 480'e çıkarıyor, yani desen okunur oluyor.
    const float StreakProbeScale = 40f;

    // Ayarlar bilerek serileştirilmiyor: Inspector'a girince sahnedeki bileşen eski
    // değerlerle donuyor ve koddaki değişiklik etkisiz kalıyor.

    // Alan. Kar kendi kutusunda sarılır: nokta biçimli tane, uzayan damla kadar ekran
    // alanı kaplamadığı için aynı bütçenin kameraya daha sıkı paketlenmesi gerekir.
    /// Yağış kutusu. 48 m'den indirildi.
    ///
    /// Ölçüldü: 48 m küp = 110 592 m³, 250 000 tanecik → 2.26 damla/m³. Şiddetli
    /// yağmurun gerçek yoğunluğu ~1000/m³, yani 442 kat eksik. O açığı temsil payıyla
    /// kapatmak imkânsız: alfa en fazla 50 kat artabilir, kalan 9 kat geometriye biner
    /// ve izler gerçek boyunun kat kat üstüne çıkar (bir dönem 1.8 m uzunluğunda
    /// "damla" çıktı).
    ///
    /// Kutu 20 m: hacim 8000 m³, yoğunluk 31/m³, açık 32 kata iniyor. Uzak damla zaten
    /// piksel altı ve görünmüyor — bütçeyi oraya harcamanın karşılığı yok. Uzağı
    /// yoğunluk katmanı taşımalı (`DECISIONS.md`, perde şu an kapalı).
    /// Kutu 19 m. 48 → 20 → 12 → 19; hepsi ölçümle.
    ///
    /// Son daraltmanın sebebi KALINLIK: damlanın gerçek kalınlığı ancak quad 1.2
    /// piksellik raster tabanını aştığında ekrana ulaşıyor. 3 mm'lik damla için o sınır
    /// 2.6 metre. 20 m'lik kutuda o kadar yakın damla yok denecek kadar azdı ve bütün
    /// izler aynı kalınlıkta çiziliyordu (kullanıcı bildirdi) — fark yalnız parlaklıkta
    /// kalıyordu.
    ///
    /// 12 m'de hacmin %4.3'ü 2.6 m'nin içinde, yani ~11 000 damla gerçek kalınlığıyla
    /// çiziliyor.
    ///
    /// SONRA 19 m'YE ÇIKARILDI. Kutu kameranın etrafına sarılıyor, yani görünür yarıçap
    /// yarı genişlik: 12 m kutuda yağmur 3 metrede sönmeye başlayıp 6 metrede bitiyordu
    /// ("sadece etrafıma yağıyor", kullanıcı bildirdi).
    ///
    /// Tavanı doyum belirliyor, bütçe değil: temsil payı alfaya giriyor ve
    /// `α_eff = 1−(1−α)^N` doyduğunda bütün damlalar aynı opaklığa gelip kalınlık farkı
    /// siliniyor. Kademelenmeyi koruyan sınır N ≤ 27 (en opak damla 0.60'ta kalır,
    /// aralık 1.8 kat) → yoğunluk 37/m³ → 250 000 tanecikle hacim 6757 m³ → kenar 19 m,
    /// görünür yarıçap 9.5 m.
    ///
    /// 15 metre istenirse tek yol tanecik sayısı: 1 M gerekir.
    static readonly Vector3 BoxSize = new(19f, 19f, 19f);
    static readonly Vector3 SnowBoxSize = new(40f, 40f, 40f);

    /// Sürüklenen kar kutusu. Yatayda dar tutuluyor: aynı tanecik sayısı küçük alana
    /// sıkışınca yoğunluk kareyle artıyor. Uzaklık sönümü de kutu boyundan türüyor
    /// (yarısında biter), yani kutuyu daraltmak görünür bölgeyi de yoğunlaştırıyor.
    /// 90 ve 45 metrede taneler ayırt edilemeyecek kadar seyrekti — uzağı zaten
    /// hacimsel perde taşıyor, yakın katmanın işi oyuncunun çevresi.
    static readonly Vector3 SpindriftBoxSize = new(24f, 12f, 24f);

    /// Tane boyu. Gerçek sürüklenen kar taneciği 0.05-0.2 mm; burada 14 mm, yani
    /// fiziksel boyun ~100 katı. Bilerek: gerçek boyda her tane piksel altına düşer ve
    /// tek tek görünmez olur — o zaten uzak perdenin işi. Yakın katmanın işi taneyi
    /// GÖSTERMEK, sayısını değil hareketini okutmak.
    const float SpindriftSize = 0.014f;

    /// Katmanın yerden kalınlığı (metre). Tane yüksekliği bunun içinde küpsel
    /// dağılıyor: çoğu yere yapışık, seyrek olanı yukarıda.
    const float SpindriftLayerHeight = 9f;
    /// Yağış tanecikleri. Bütçe bölünüyor: ilk blok yağış, kalanı sürüklenen kar.
    /// Ayrı bir mesh yerine tek mesh büyütüldü — iki sistem aynı anda çalışabilsin diye.
    /// Gerçekte kar yağarken de yerden kar kalkar; paylaşımlı bütçe bunu yasaklardı.
    /// 90 000'den çıkarıldı. Ölçüldü: 48 m küp kutuda 90 000 tanecik 0.8 damla/m³
    /// demek; şiddetli yağmurun gerçek yoğunluğu Marshall-Palmer'a göre ~1000/m³.
    /// Yani eski tavan gerçekte orta şiddetin bile altındaydı ve %50 ÇİSELTİ gibi
    /// okunuyordu (kullanıcı bildirdi).
    ///
    /// Damlayı şişirmek yerine sayı artırıldı: temsil payını büyütmek izi kalınlaştırıp
    /// gerçekçiliği bozuyor, sayı ise doğrudan eksik olan büyüklük.
    const int PrecipitationParticles = 250000;

    /// Sürüklenen kar tanecikleri. Yağıştan FAZLA: ince toz ancak taneler tek tek
    /// seçilemeyecek kadar sıkışınca okunuyor. 40.000'de metrekareye ~70 tane düşüyordu
    /// ve göz her birini ayırt ediyordu — "taneli", "toz değil". Kutuyu daraltmak
    /// çözmez: uzaklık sönümü kutu boyundan türüyor, daraltınca toz birkaç metrede
    /// bitip baloncuk gibi görünüyor. Tek doğru kaldıraç sayı.
    const int SpindriftParticles = 80000;

    const int ParticleCount = PrecipitationParticles + SpindriftParticles;

    const int PrecipitationSubMesh = 0;
    const int SpindriftSubMesh = 1;
    /// Şiddetin çizilen tanecik sayısına dönüşüm eğrisi — MARSHALL-PALMER'DAN.
    ///
    /// Dağılımda `N₀` sabit ve `Λ = 4.1·R^(−0.21)`, yani toplam damla sayısı
    /// `N = N₀/Λ ∝ R^0.21`. Yağış şiddetlenince damla SAYISI neredeyse hiç artmıyor;
    /// artan şey damla BOYU. Sağanağı sağanak yapan iri ve hızlı damlalardır.
    ///
    /// Eskiden 1.6'ydı, yani sayı şiddetle sert büküyordu. Şiddet zaten Λ üzerinden
    /// boyu da sürüyor — çifte sayım. Ölçüldü: şiddet 0.30'da yalnız %14 tanecik
    /// çiziliyordu (36k) ve hafif yağmur ekranda yok oluyordu.
    const float DensityExponent = 0.21f;
    // Karın tanecik bütçesindeki payı. Yağmurla kar bir aradayken ikisini bölmek için
    // var; saf kar fırtınasında ise yağmur payı zaten sıfır olduğundan kısıtlamanın
    // karşılığı yok. 0.45'te bütçenin yarısından fazlası hiçbir işe yaramadan eleniyor
    // ve tam karlılıkta kar seyrek görünüyordu.
    const float SnowDensityScale = 0.9f;
    const int MeshSeed = 1;

    // Yağmur. Damla boyutu hem düşme hızını hem rüzgâra direncini belirler:
    // ince serpinti yanlamasına uçar, iri damla dik iner. Ölçekler shader'da
    // damla başına uygulanır, buradaki değerler bandın uçlarıdır.
    /// Terminal hız — Gunn & Kinzer ölçümlerinin Atlas bağıntısı:
    ///   `v(D) = 9.65 − 10.3·exp(−0.6·D)`,  D = çap (mm)
    ///
    /// Çap bandı 0.5-5 mm, yani hız 2.02-9.14 m/s.
    ///
    /// ESKİDEN 16 m/s'YE ABARTILIYORDU ve gerekçesi yazılıydı: "tanecikler 16-24 m
    /// uzakta olduğundan açısal hız düşük kalıyor". O abartı eski görsel modele aitti.
    /// Artık İZİN BOYU fiziksel terminal hızdan çiziliyor (`v·T_exp`); hareket başka
    /// hızda olursa damla kat ettiği yoldan kısa iz bırakır — ölçüldü, 16'ya karşı 9.14,
    /// yani iz gerçek yolun %57'si kadardı.
    ///
    /// Hız damla başına hesaplanamıyor: rüzgâr sürüklenmesi CPU'da sınıf başına integre
    /// ediliyor. Sınıfın temsilci yarıçapından türüyor, shader'daki formülün aynısı.
    static float TerminalVelocity(float t)
    {
        float diameterMm = 0.5f + 4.5f * t;
        return 9.65f - 10.3f * Mathf.Exp(-0.6f * diameterMm);
    }
    const float RainWindFactor = 0.85f;   // iri damlanın yediği rüzgâr oranı
    const float RainWindLightFactor = 1f; // ince damla rüzgârı tam yer
    // Hız sürekli olsaydı her damla kaymayı farklı ölçekle çarpardı ve sarma noktası
    // kutunun katı olmaktan çıkıp damlaları zıplatırdı. Sınıf başına ayrı kayma tutulur.
    const int RainSpeedClasses = 8;
    static readonly Color RainColor = new(0.78f, 0.83f, 0.92f, 0.42f);

    // Kar
    const float SnowFallSpeed = 1.4f;
    const float SnowWindFactor = 1f;      // hafif olduğu için rüzgârı tam yer
    const float SnowSize = 0.11f;
    const float SnowSpinCalm = 0.8f;      // dingin havada dönme hızı, rad/s
    const float SnowSpinStorm = 4.5f;     // tam fırtınada dönme hızı, rad/s

    // Girdap genlikleri. Dingin havada tanecikler neredeyse düz iner; genlik
    // rüzgârdan ölçeklenir, kendi zamanlayıcısını kurmaz
    const float SnowTurbulenceCalm = 0.15f;
    const float SnowTurbulenceStorm = 1.2f;
    const float RainTurbulenceCalm = 0.03f;
    const float RainTurbulenceStorm = 0.25f;
    // Tanenin rengi değil, havanın rengine binen ton. Kar kendi ışığını üretmiyor:
    // gölgelendirici havanın rengini bununla çarpıp parlatıyor, böylece şafakta turuncu,
    // gece koyu, şimşekte parlak oluyor. Sabit beyaz, kapalı gökyüzünün önünde patlayıp
    // yıldız gibi duruyordu.
    static readonly Color SnowColor = new(0.95f, 0.96f, 1f, 1f);

    static readonly int BoxSizeId = Shader.PropertyToID("_BoxSize");
    static readonly int SnowBoxSizeId = Shader.PropertyToID("_SnowBoxSize");
    static readonly int StreakPointId = Shader.PropertyToID("_StreakPoint");
    static readonly int StreakAmbientId = Shader.PropertyToID("_StreakAmbient");
    static readonly int StreakCellBlendId = Shader.PropertyToID("_StreakCellBlend");
    static readonly int StreakCornerPresentId = Shader.PropertyToID("_StreakCornerPresent");
    static readonly int StreakMirrorId = Shader.PropertyToID("_StreakMirror");
    static readonly int StreakDcamFractionId = Shader.PropertyToID("_StreakDcamFraction");
    static readonly int StreakExposureId = Shader.PropertyToID("_StreakExposure");
    static readonly int StreakDbPeriodId = Shader.PropertyToID("_StreakDbPeriod");
    static readonly int StreakSourceScaleId = Shader.PropertyToID("_StreakSourceScale");
    static readonly int StreakSunRadianceId = Shader.PropertyToID("_StreakSunRadiance");
    static readonly int StreakRepresentationId = Shader.PropertyToID("_StreakRepresentation");
    static readonly int StreakDebugId = Shader.PropertyToID("_StreakDebug");
    static readonly int StreakDebugScaleId = Shader.PropertyToID("_StreakDebugScale");

    static readonly int RainDriftsId = Shader.PropertyToID("_RainDrifts");
    static readonly int RainDirectionsId = Shader.PropertyToID("_RainDirections");
    static readonly int SnowDriftId = Shader.PropertyToID("_SnowDrift");
    static readonly int SnowDirectionId = Shader.PropertyToID("_SnowDirection");
    static readonly int SpindriftBoxId = Shader.PropertyToID("_SpindriftBox");
    static readonly int SpindriftDriftId = Shader.PropertyToID("_SpindriftParticleDrift");
    static readonly int SpindriftSizeId = Shader.PropertyToID("_SpindriftSize");
    static readonly int SpindriftLayerId = Shader.PropertyToID("_SpindriftLayer");

    /// Atmosfer yazıyor, burada yalnız OKUNUYOR: rüzgâr eşiği geçilmediyse toz alt
    /// parçası hiç çizilmesin diye. İkinci bir eşik hesabı kurmak iki sistemi ayırırdı.
    static readonly int SpindriftDensityId = Shader.PropertyToID("_SpindriftDensity");
    static readonly int SnowinessId = Shader.PropertyToID("_Snowiness");
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int PrecipitationId = Shader.PropertyToID("_Precipitation");
    static readonly int SnowDensityScaleId = Shader.PropertyToID("_SnowDensityScale");
    static readonly int SnowSizeId = Shader.PropertyToID("_SnowSize");
    static readonly int SnowTurbulenceId = Shader.PropertyToID("_SnowTurbulence");
    static readonly int RainTurbulenceId = Shader.PropertyToID("_RainTurbulence");
    static readonly int SnowSpinId = Shader.PropertyToID("_SnowSpin");
    static readonly int WindSweepId = Shader.PropertyToID("_WindSweep");
    static readonly int RainColorId = Shader.PropertyToID("_RainColor");
    static readonly int SnowColorId = Shader.PropertyToID("_SnowColor");


    Mesh mesh;
    Material material;
    readonly Vector4[] rainDrifts = new Vector4[RainSpeedClasses];
    readonly Vector4[] rainDirections = new Vector4[RainSpeedClasses];
    readonly Vector3[] rainVelocities = new Vector3[RainSpeedClasses];
    Vector3 snowDrift;
    Vector3 spindriftDrift;
    Vector3 windSweep;
    float snowSpin;
    float density;
    float precipitation;
    float snowiness;
    float localFactor = 1f;

    /// Yağış artık gökten tek parça düşmüyor: kaynağı tepedeki bulut kolonudur.
    /// Bulut sistemine tek yönlü, salt okunur bir bağ — yağış hangi bulutun yağdığını
    /// sormaz, yalnız "şu an başımın üstünde ne kadar var" değerini okur.
    public void Bind(WeatherState state, WindField windField, Shader precipitationShader,
                     CloudLayerProbe layer, Transform eye,
                     RainStreakWorkingSet streakSet, TimeOfDay clock)
    {
        weather = state;
        wind = windField;
        shader = precipitationShader;
        cloudLayer = layer;
        observer = eye;
        streaks = streakSet;
        timeOfDay = clock;
    }

    void OnEnable()
    {
        if (weather == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(weather)} atanmadı.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(wind)} atanmadı.");
        if (shader == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(shader)} atanmadı.");
        if (cloudLayer == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(cloudLayer)} atanmadı.");
        if (observer == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(observer)} atanmadı.");

        weather.Changed += OnWeatherChanged;
        OnWeatherChanged(weather);

        // Süzülmüş hız sıfırdan başlarsa ilk karelerde yön vektörü sıfıra normalize
        // olur ve shader'da NaN üretir; düşme hızıyla başlatılır.
        for (int i = 0; i < RainSpeedClasses; i++)
        {
            float t = i / (RainSpeedClasses - 1f);
            rainVelocities[i] = Vector3.down * TerminalVelocity(t);
        }
    }

    /// İZ VERİTABANINI KARE BAŞINA HAZIRLAR — `[Garg 2006, §5]`.
    ///
    /// Üç açı da burada belirleniyor ve üçü de FARKLI şeye bağlı:
    ///   ışığın yüksekliği — güneşin damlanın düşüş eksenine göre açısı
    ///   ışığın azimutu   — aynı açının kameranın eksenine göre bileşeni
    ///   `θ_v`            — kameranın bakışıyla düşüş yönü arasındaki açı (shader'da,
    ///                      damla başına, çünkü ekranın her yerinde farklı)
    void UpdateStreaks(Vector3 rainVelocity, float snowiness)
    {
        // TEK SEFERLİK DURUM RAPORU. Üç bağ da sessizce eksik olabiliyordu ve belirti
        // "yağmur hiç görünmüyor" — hangisinin eksik olduğu ekrandan anlaşılmıyor.
        if (!streakStateReported)
        {
            streakStateReported = true;
            Debug.Log($"Yağmur izi bağları: çalışma kümesi {(streaks != null ? "var" : "YOK")}"
                      + $", saat {(timeOfDay != null ? "var" : "YOK")}"
                      + $", ana kamera {(Camera.main != null ? "var" : "YOK")}");
        }

        if (streaks == null || timeOfDay == null) return;

        var camera = Camera.main;
        if (camera == null) return;

        // Yağışın DÜNYA yönü — rüzgârla eğilmiş düşüş ekseni. Kar payı yüksekken de
        // hesaplanıyor: karlılık geçişinde damlalar hâlâ çiziliyor.
        Vector3 fall = rainVelocity.sqrMagnitude > 1e-8f
            ? rainVelocity.normalized
            : Vector3.down;

        streaks.Refresh(timeOfDay.SunDirection, fall, camera.transform.forward);

        if (streaks.Point == null || streaks.Ambient == null) return;

        material.SetTexture(StreakPointId, streaks.Point);
        material.SetTexture(StreakAmbientId, streaks.Ambient);
        material.SetVector(StreakCellBlendId, streaks.CellBlend);
        material.SetVector(StreakCornerPresentId, streaks.CornerPresent);
        material.SetFloat(StreakMirrorId, streaks.MirroredAzimuth ? 1f : 0f);
        material.SetFloatArray(StreakDcamFractionId, streaks.DcamHeightFraction);
        material.SetFloat(StreakExposureId, ExposureTime);
        material.SetFloat(StreakDbPeriodId, DatabasePeriod);
        // GÜNEŞ DİSKİNİN RADYANSI. `TimeOfDay` rengi 1'e normalize tutuyor ve şiddeti
        // ayrı taşıyor; çarpımları gerçek büyüklük (`TimeOfDay` içinde yazılı).
        Color sun = timeOfDay.CurrentSunColor * timeOfDay.SunIntensity;
        material.SetVector(StreakSunRadianceId, new Vector4(sun.r, sun.g, sun.b, 1f));

        material.SetFloat(StreakRepresentationId, Representation);
        material.SetFloat(StreakSourceScaleId, SourceScale);
        material.SetFloat(StreakDebugId, StreakProbe);
        material.SetFloat(StreakDebugScaleId, StreakProbeScale);

        if (!streakTextureReported)
        {
            streakTextureReported = true;
            Debug.Log($"İz dokuları bağlandı: yönlü {streaks.Point.width}×{streaks.Point.height}"
                      + $"×{streaks.Point.depth}, ambient {streaks.Ambient.depth} dilim, "
                      + $"köşe varlık {streaks.CornerPresent}, "
                      + $"dcam payları [{string.Join(", ", streaks.DcamHeightFraction)}]");
        }
    }

    bool streakStateReported, streakTextureReported;

    void OnDisable()
    {
        if (weather != null) weather.Changed -= OnWeatherChanged;
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
    }

    void OnWeatherChanged(WeatherState state)
    {
        // MARSHALL-PALMER SIFIRDA GEÇERSİZ, KAPI ŞART.
        //
        // `N ∝ R^0.21` eğrisi sıfıra yakın çok dik: şiddet 0.001'de bile yoğunluk 0.234
        // çıkıyor, yani taneciklerin dörtte biri çiziliyor. Hava yumuşatmayla sıfıra
        // YAKLAŞIYOR ama oturmuyor; sonuç, panel "yağış 0,00" gösterirken ekranda
        // yağmur olması (kullanıcı bildirdi, çap probuyla görüldü).
        //
        // Bağıntı R > 0 için doğru ama R → 0'da yağış OLAYININ kendisi bitmeli. Kapı
        // şiddetin en alt diliminde: 0.05 altı çiseleme bile değil (R < 2.5 mm/sa),
        // orada damla sayısı sıfıra iniyor.
        density = Mathf.Pow(state.Precipitation, DensityExponent)
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.05f, state.Precipitation));
        precipitation = state.Precipitation;
        snowiness = state.Snowiness;
    }

    void Update()
    {
        EnsureResources();

        // BAĞ 4: yağış o SÜTUNUN kapsamasıyla ölçekleniyor — komşu bulut yağmazken bunun
        // altında yağabilsin diye. Sütunun tepesinin üstündeysen hiç yağmıyor; tavan
        // kesimini `AltitudeWeatherDriver` yapıyor, burada yalnız yatay dağılım var.
        localFactor = cloudLayer.CoverageAt(observer.position);

        // İnce serpinti yavaş düşüp rüzgârı tam yer, iri damla hızlı inip direnir.
        // Aradaki bütün açılar böylece kendiliğinden dolar.
        //
        // Damla rüzgâr değişimine anında uyamaz: gevşeme süresi terminal hız / g —
        // ince serpinti hamleye çabuk döner, iri damla geç. Hız süzülmeden alınınca
        // her hamle bütün yağmuru aynı karede tek parça yatırıyordu: karton perde.
        // Konum da iz yönü de aynı süzülmüş hızdan türer; ayrışırlarsa iz, damlanın
        // gittiği yönden başka bir yöne uzar.
        for (int i = 0; i < RainSpeedClasses; i++)
        {
            float t = i / (RainSpeedClasses - 1f);
            float fallSpeed = TerminalVelocity(t);
            Vector3 target = Vector3.down * fallSpeed
                             + wind.Velocity * Mathf.Lerp(RainWindLightFactor, RainWindFactor, t);

            float blend = 1f - Mathf.Exp(-Time.deltaTime * 9.81f / fallSpeed);
            rainVelocities[i] = Vector3.Lerp(rainVelocities[i], target, blend);

            rainDrifts[i] = Advance(rainDrifts[i], rainVelocities[i], BoxSize);
            rainDirections[i] = rainVelocities[i].normalized;
        }

        Vector3 snowVelocity = Vector3.down * SnowFallSpeed + wind.Velocity * SnowWindFactor;
        snowDrift = Advance(snowDrift, snowVelocity, SnowBoxSize);

        // SÜRÜKLENEN KAR YALNIZ YATAY GİDER: yerden kalkan tane düşmüyor, rüzgârla
        // taşınıyor. Rüzgârı tam yiyor — kırık kar taneciği yağan kar tanesinden çok
        // daha küçük, havanın hızına anında oturuyor. Dikey kayma yok, çünkü tanenin
        // yerden yüksekliği kutudan değil arazi yüzeyinden türüyor.
        Vector3 spindriftVelocity = new(wind.Velocity.x, 0f, wind.Velocity.z);
        spindriftDrift = Advance(spindriftDrift, spindriftVelocity, SpindriftBoxSize);

        // Tanecik esintiyi anında yer: sürekli şiddet değil, o andaki hız neyse o.
        // Hız zaten Velocity üzerinden esintili geliyordu; dönme ve türbülans sürekli
        // şiddette kalınca aynı tane hızlanırken dönmesi sabit kalıyordu.
        float felt = Mathf.Clamp01(wind.Strength * (1f + wind.Gust));

        // Dönme rüzgârdan gelir: dingin havada süzülür, fırtınada savrulup hızlı döner.
        // Açı birikerek gider; shader'da zamanla çarpılsaydı rüzgâr her değiştiğinde
        // taneler fazdan fazla sıçrardı
        // Sarma yok: her tane açıyı farklı katsayıyla çarptığı için sarma noktası
        // her tanede başka yere düşer ve sıçrama görünür. Saatler sonra bile
        // float hassasiyeti binde bir radyan seviyesinde, göze çarpmaz
        float spinRate = Mathf.Lerp(SnowSpinCalm, SnowSpinStorm, felt);
        snowSpin += spinRate * Time.deltaTime;

        // Girdap alanının rüzgârla sürüklenme ötelemesi (Taylor: türbülans ortalama
        // akışla taşınır). Sarılmıyor: alanın içinde 0.7-0.9 gibi karışık frekans
        // çarpanları var, hiçbir periyot hepsine ortak gelmiyor ve sarma anı
        // girdapları görünür biçimde ışınlıyor. Sarmamak yalnızca sin argümanını
        // büyütür — shader zaten _Time.y ile aynı büyüklükte argüman kullanıyor.
        windSweep += wind.Velocity * Time.deltaTime;

        material.SetVector(BoxSizeId, BoxSize);
        material.SetVector(SnowBoxSizeId, SnowBoxSize);
        material.SetVectorArray(RainDriftsId, rainDrifts);
        material.SetVectorArray(RainDirectionsId, rainDirections);
        material.SetVector(SnowDriftId, snowDrift);
        material.SetVector(SnowDirectionId, snowVelocity.normalized);
        material.SetVector(SpindriftBoxId, SpindriftBoxSize);
        material.SetVector(SpindriftDriftId, spindriftDrift);
        material.SetFloat(SpindriftSizeId, SpindriftSize);
        material.SetFloat(SpindriftLayerId, SpindriftLayerHeight);
        material.SetFloat(SnowinessId, snowiness);
        material.SetFloat(DensityId, density * localFactor);
        material.SetFloat(PrecipitationId, precipitation * localFactor);

        UpdateStreaks(snowVelocity, snowiness);

        material.SetFloat(SnowDensityScaleId, SnowDensityScale);
        material.SetFloat(SnowSizeId, SnowSize);
        material.SetFloat(SnowTurbulenceId,
            Mathf.Lerp(SnowTurbulenceCalm, SnowTurbulenceStorm, felt));
        material.SetFloat(RainTurbulenceId,
            Mathf.Lerp(RainTurbulenceCalm, RainTurbulenceStorm, felt));
        material.SetFloat(SnowSpinId, snowSpin);
        material.SetVector(WindSweepId, windSweep);
        material.SetColor(RainColorId, RainColor);
        material.SetColor(SnowColorId, SnowColor);

        Draw();
    }

    /// Kayma birikerek gider; kendi kutusunun katına sararak float hassasiyetini korur.
    /// Yanlış kutuya sarmak tanecikleri dünyada kayar gösterir.
    static Vector3 Advance(Vector3 drift, Vector3 velocity, Vector3 box)
    {
        drift += velocity * Time.deltaTime;
        return new Vector3(
            Mathf.Repeat(drift.x, box.x),
            Mathf.Repeat(drift.y, box.y),
            Mathf.Repeat(drift.z, box.z));
    }

    /// Play mode'da yeniden derleme mesh ve materyali düşürebilir; kullanım anında doğrulanır.
    void EnsureResources()
    {
        if (mesh == null) mesh = BuildMesh();
        if (material == null) material = new Material(shader);
    }

    /// İKİ ALT PARÇA, İKİ AYRI ÇİZİM. Kapalı olan sistem hiç gönderilmiyor: rüzgâr
    /// eşiğin altındayken 80.000 toz quad'ı, yağış yokken 90.000 yağış quad'ı vertex
    /// shader'a hiç girmiyor. Tek çizimken kapalı sistemin bütün taneleri işlenip
    /// sıfır boyutla eleniyordu — görünmeyen şeyin tam maliyeti ödeniyordu.
    ///
    /// `MeshRenderer` yerine doğrudan çizim: bileşen bütün alt parçaları gönderir,
    /// hangisinin atlanacağını seçemez.
    void Draw()
    {
        var parameters = new RenderParams(material)
        {
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,

            // Konumlar shader'da üretiliyor; gerçek sınır hesaplanamaz. Kamera etrafında
            // sarıldıkları için her zaman görünür kabul ediliyorlar.
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
        };

        var transform = Matrix4x4.identity;

        if (density * localFactor > 0.0005f)
            Graphics.RenderMesh(parameters, mesh, PrecipitationSubMesh, transform);

        if (Shader.GetGlobalFloat(SpindriftDensityId) > 0.00001f)
            Graphics.RenderMesh(parameters, mesh, SpindriftSubMesh, transform);
    }

    /// Her tanecik bir quad. Köşe bilgisi UV0'da, tanecik tohumu UV1/UV2'de, tanecik
    /// TÜRÜ vertex konumunun x'inde. Konum kanalı başka türlü kullanılmıyor; shader
    /// dünya konumunu tohumdan üretir.
    Mesh BuildMesh()
    {
        int vertexCount = ParticleCount * 4;

        var positions = new Vector3[vertexCount];
        var corners = new Vector2[vertexCount];
        var seedXY = new Vector2[vertexCount];
        var seedZW = new Vector2[vertexCount];
        var indices = new int[ParticleCount * 6];

        var random = new System.Random(MeshSeed);

        for (int i = 0; i < ParticleCount; i++)
        {
            var xy = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            var zw = new Vector2((float)random.NextDouble(), (float)random.NextDouble());

            // Tanecik türü vertex konumunun x'inde taşınıyor. Konum kanalı zaten boştu
            // (shader dünya konumunu tohumdan üretiyor), yani ek bir vertex akışı
            // açmadan bayrak taşınabiliyor.
            float kind = i < PrecipitationParticles ? 0f : 1f;

            int v = i * 4;
            corners[v + 0] = new Vector2(0f, 0f);
            corners[v + 1] = new Vector2(1f, 0f);
            corners[v + 2] = new Vector2(1f, 1f);
            corners[v + 3] = new Vector2(0f, 1f);

            for (int c = 0; c < 4; c++)
            {
                positions[v + c] = new Vector3(kind, 0f, 0f);
                seedXY[v + c] = xy;
                seedZW[v + c] = zw;
            }

            int t = i * 6;
            indices[t + 0] = v + 0;
            indices[t + 1] = v + 1;
            indices[t + 2] = v + 2;
            indices[t + 3] = v + 0;
            indices[t + 4] = v + 2;
            indices[t + 5] = v + 3;
        }

        var built = new Mesh { name = "Precipitation", indexFormat = IndexFormat.UInt32 };
        built.SetVertices(positions);
        built.SetUVs(0, corners);
        built.SetUVs(1, seedXY);
        built.SetUVs(2, seedZW);
        built.subMeshCount = 2;
        built.SetIndices(indices, 0, PrecipitationParticles * 6,
                         MeshTopology.Triangles, PrecipitationSubMesh, false);
        built.SetIndices(indices, PrecipitationParticles * 6, SpindriftParticles * 6,
                         MeshTopology.Triangles, SpindriftSubMesh, false);

        // Konumlar shader'da üretildiği için hesaplanan sınırlar anlamsız; culling'i kapat
        built.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return built;
    }
}
