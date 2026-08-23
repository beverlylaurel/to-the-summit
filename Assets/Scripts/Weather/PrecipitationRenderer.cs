using System;
using UnityEngine;
using UnityEngine.Rendering;

/// Yağışı tek draw call ile çizer. Tanecik konumları CPU'da tutulmaz;
/// vertex shader zaman + rüzgâr kaymasından üretir.
///
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
    /// HEDEF ORAN 1 DEĞİL — İLK KALİBRASYON MANTIK HATASIYDI.
    ///
    /// Oran ardındaki gökle eşitlenecek diye 3'e ayarlanmıştı. Ama arka planıyla aynı
    /// parlaklıktaki bir şey tanım gereği GÖRÜNMEZ: göğe bakınca arka plan gök, damla da
    /// gök parlaklığında, kontrast sıfır. Kullanıcı "havaya bakarken damlalar
    /// gözükmüyor" dedi.
    ///
    /// Damla ışık üretmiyor ama arka planından parlak OLABİLİR ve olur: geniş bir katı
    /// açıdan (gök kubbesinin tamamı dahil) topladığı ışığı kameraya kırıyor, oysa
    /// örttüğü arka plan çok daha küçük bir katı açı. `[Tatarchuk 2006, §3.6.1]`:
    /// "a drop tends to be much brighter than its background".
    ///
    /// Kâğıtta kontrast `α × (oran − 1)`, α ortanca 0.377:
    ///   oran 1.0 → 0.00  görünmez
    ///   oran 1.5 → 0.19  zayıf
    ///   oran 2.0 → 0.38  net
    ///   oran 3.0 → 0.75  güçlü
    ///
    /// Ölçülen: ×1'de oran 0.16-0.36, ×3'te ortanca 0.8. Oranı 2.0'a taşımak için
    /// 3 × 2.0/0.8 = 7.5.
    const float SourceScale = 7.5f;

    /// TANECİK YOĞUNLUKLARI, damla/m³. Shader temsil payını KONUMDAN türetiyor:
    /// `N(r) = 1000 / yoğunluk(r)`, yoğunluk = dış + (iç kutunun kapsadığı yerde) iç.
    ///
    /// Payın konumdan türemesi zorunlu: aynı noktadaki iki tanecik hangi kutudan
    /// geldiğine bakılmaksızın AYNI sayıda gerçek damlayı temsil etmeli, yoksa aynı
    ///
    /// Şiddetli yağmurun gerçek yoğunluğu ~1000/m³. Dış kutuda 2.03, iç kutunun
    /// içinde 2.03 + 14.47 = 16.5 → temsil payı 491'den 61'e iniyor. Yani yakın damla
    /// gerçeğe çok daha yakın bir kümeyi taşıyor.
    ///
    /// Kaplamaya giriyor, geometriye değil: `α_eff = 1 − (1−α)^N`.
    static float OuterDensity =>
        (PrecipitationParticles - NearParticles) / (BoxSize.x * BoxSize.y * BoxSize.z);

    static float NearDensity =>
        NearParticles / (NearBoxSize.x * NearBoxSize.y * NearBoxSize.z);

    /// Teşhis kipi F1 panelinden sürülüyor, Inspector'dan değil.

    /// Teşhiste quad'ın büyütme katsayısı. 24 m'deki iz fiziksel ölçekte 0.4 × 12
    /// piksel; 40 kat büyütme onu 16 × 480'e çıkarıyor, yani desen okunur oluyor.

    // Ayarlar bilerek serileştirilmiyor: Inspector'a girince sahnedeki bileşen eski
    // değerlerle donuyor ve koddaki değişiklik etkisiz kalıyor.

    // alanı kaplamadığı için aynı bütçenin kameraya daha sıkı paketlenmesi gerekir.
    /// Yağış kutusu 48 m; kamera merkezde sarılıyor, yani görünür yarıçap 24 m.
    ///
    /// TARİH: 48 → 20 → 12 → 19 → 24 → 32 → 48. Her adım ölçümle, ama ilk beş adım
    /// `widen` üssü 0.5 iken ölçülmüştü. Üs 0.35'e inince (bkz. `Precipitation.shader`,
    /// kalınlık telafisi) tablo değişti ve daralmanın gerekçesi ortadan kalktı.
    ///
    /// ÜS 0.35 İLE TARANDI (şiddet 0.4, 250 000 tanecik, 20 000 örnek):
    ///
    ///    32 m → yarıçap 16 m, ortanca alfa 0.352, görünmez %7.2
    ///    48 m → yarıçap 24 m, ortanca alfa 0.352, görünmez %7.2   ← seçilen
    ///    64 m → yarıçap 32 m, ortanca alfa 0.318, görünmez %7.7
    ///   100 m → yarıçap 50 m, ortanca alfa 0.272, görünmez %8.3
    ///
    /// 32 → 48 BEDAVA: opaklık hiç değişmiyor, yarıçap 1.5 kat artıyor. Sebep temsil
    /// payının doyuma girmiş olması — kutu büyüyünce hem mesafe cezası hem doyum payı
    /// artıyor ve ikisi 48'e kadar birbirini götürüyor. Bedel 64 m'de başlıyor.
    ///
    /// Dolgu maliyeti de artmıyor: tanecik sayısı sabit, taneler uzaklaştıkça ekranda
    /// küçülüyor.
    ///
    /// KALINLIK ARTIK KUTUYU BELİRLEMİYOR. Bir dönem kutu bunun için daraltılmıştı:
    /// damlanın gerçek kalınlığı ancak quad 1.2 piksellik raster tabanını aştığında
    /// ekrana ulaşıyor ve 3 mm'lik damla için o sınır 2.6 m. Ama hacim r³ ile büyüdüğü
    /// için o yakınlıkta 32 m'de zaten damlaların yalnız %0.2'si vardı (48 m'de %0.07)
    /// — ikisi de sıfır sayılır. Ekrandaki kalınlık kademelenmesini gerçek genişlik
    /// değil, `pow(widen, -0.35)` parlaklık telafisi taşıyor ve o mesafeden bağımsız
    /// çalışıyor.
    ///
    /// YARIÇAPI BÜYÜTMENİN TEK GERÇEK SINIRI 64 m'de: orada opaklık %10 düşüyor.
    /// Daha uzağı tanecikle değil, sisin yağıştan gelen görüş mesafesi taşıyor
    /// (`AtmosphereController`, 18000·R^−0.70).
    static readonly Vector3 BoxSize = new(48f, 48f, 48f);

    /// İÇ KUTU. Yağış kutusu kameranın etrafında PERİYODİK olarak sarıyor ve periyodik
    /// bir döşeme yoğunluk gradyanı taşıyamaz — yani tek kutuyla "yakında sık, uzakta
    /// seyrek" kurulamaz. Hacim `r³` ile büyüdüğü için bütçenin neredeyse tamamı uzağa
    /// gidiyordu: 48 m'lik tek kutuda 5 metrenin içinde 1 188 tanecik vardı, yani
    /// binde beş. Oysa oyuncunun TEK TEK DAMLA olarak okuduğu hacim orası.
    ///
    /// Çözüm iç içe kutu: ikisi de kamerada merkezli, ikisi de kendi içinde tekdüze ve
    /// kendi kutusuna sarıyor. İç kutunun kapsadığı yerde yoğunluklar TOPLANIYOR, yani
    /// yakın alan kendiliğinden sıklaşıyor. Hareket her iki kutuda da tam doğru kalıyor
    /// çünkü her biri kendi kaymasıyla integre ediliyor.
    ///
    /// ÜS TARANDI. Önce sürekli radyal dağılım (`yoğunluk ∝ r^-p`) ölçüldü; en iyi
    /// `p = 1`, yani `1/r`. Sonra kutu şemasıyla ne kadarının yakalandığı ölçüldü
    /// (şiddet 1.0, ekran kaplaması kilopiksel):
    ///
    ///   tek kutu 48          5 m içi  87   toplam  934
    ///   48 + 12, iç %5       5 m içi 194   toplam 1027
    ///   48 + 12, iç %10      5 m içi 227   toplam 1033   ← seçilen
    ///   48 + 12, iç %20      5 m içi 265   toplam 1003   (ortanca alfa düşüyor)
    ///   48 + 16 + 6          5 m içi 221   toplam 1061   (üçüncü kutu kayda değmiyor)
    ///
    /// %10'da yakın alan kaplaması İKİ BUÇUK KAT, toplam %11 artıyor ve ortanca alfa
    /// değişmiyor. %20'de yakın biraz daha artıyor ama toplam ve ortanca düşüyor.
    static readonly Vector3 NearBoxSize = new(12f, 12f, 12f);

    /// sıkışınca yoğunluk kareyle artıyor. Uzaklık sönümü de kutu boyundan türüyor
    /// (yarısında biter), yani kutuyu daraltmak görünür bölgeyi de yoğunlaştırıyor.
    /// 90 ve 45 metrede taneler ayırt edilemeyecek kadar seyrekti — uzağı zaten
    /// hacimsel perde taşıyor, yakın katmanın işi oyuncunun çevresi.

    /// fiziksel boyun ~100 katı. Bilerek: gerçek boyda her tane piksel altına düşer ve
    /// tek tek görünmez olur — o zaten uzak perdenin işi. Yakın katmanın işi taneyi
    /// GÖSTERMEK, sayısını değil hareketini okutmak.

    /// Katmanın yerden kalınlığı (metre). Tane yüksekliği bunun içinde küpsel
    /// dağılıyor: çoğu yere yapışık, seyrek olanı yukarıda.
    /// Ayrı bir mesh yerine tek mesh büyütüldü — iki sistem aynı anda çalışabilsin diye.
    /// 90 000'den çıkarıldı. Ölçüldü: 48 m küp kutuda 90 000 tanecik 0.8 damla/m³
    /// demek; şiddetli yağmurun gerçek yoğunluğu Marshall-Palmer'a göre ~1000/m³.
    /// Yani eski tavan gerçekte orta şiddetin bile altındaydı ve %50 ÇİSELTİ gibi
    /// okunuyordu (kullanıcı bildirdi).
    ///
    /// Damlayı şişirmek yerine sayı artırıldı: temsil payını büyütmek izi kalınlaştırıp
    /// gerçekçiliği bozuyor, sayı ise doğrudan eksik olan büyüklük.
    const int PrecipitationParticles = 250000;

    /// İç kutuya düşen pay. Mesh'te İLK `NearParticles` tanecik iç kutuda, kalanı dış
    /// kutuda; bayrak vertex konumunun `y`'sinde taşınıyor.
    const int NearParticles = 25000;

    /// seçilemeyecek kadar sıkışınca okunuyor. 40.000'de metrekareye ~70 tane düşüyordu
    /// ve göz her birini ayırt ediyordu — "taneli", "toz değil". Kutuyu daraltmak
    /// çözmez: uzaklık sönümü kutu boyundan türüyor, daraltınca toz birkaç metrede
    /// bitip baloncuk gibi görünüyor. Tek doğru kaldıraç sayı.

    const int ParticleCount = PrecipitationParticles;

    const int PrecipitationSubMesh = 0;
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
    // karşılığı yok. 0.45'te bütçenin yarısından fazlası hiçbir işe yaramadan eleniyor
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
    /// RÜZGÂRIN SINIR TABAKASI SHADER'DA, BURADA DEĞİL.
    ///
    /// Kayma sınıf başına burada integre ediliyor ve tek vektör yüksekliğe göre
    /// değişemez. Bir denemede sınıf başına dört yükseklik bandı kuruldu; ÖLÇÜMLE
    /// ELENDİ: bantların kaymaları zamanla sınırsız ayrışıyor (30 sn'de 101 m) ve
    /// kutuya sarılınca aradaki fark rastgele bir sayıya dönüşüyor. Düşen damla
    /// bantlar arasında geçerken o fark ona 21 m/s'ye kadar sahte yatay hız olarak
    /// biniyordu.
    ///
    /// Doğrusu kapalı biçimde ve damla başına: `Precipitation.shader`, `WIND_LAG_TOP`
    /// çevresi. Burada yalnız SERBEST AKIŞ kayması durur.
    const float RainWindFactor = 0.85f;   // iri damlanın yediği rüzgâr oranı
    const float RainWindLightFactor = 1f; // ince damla rüzgârı tam yer
    // Hız sürekli olsaydı her damla kaymayı farklı ölçekle çarpardı ve sarma noktası
    // kutunun katı olmaktan çıkıp damlaları zıplatırdı. Sınıf başına ayrı kayma tutulur.
    const int RainSpeedClasses = 8;
    static readonly Color RainColor = new(0.78f, 0.83f, 0.92f, 0.42f);

    // Girdap genlikleri. Dingin havada tanecikler neredeyse düz iner; genlik
    // rüzgârdan ölçeklenir, kendi zamanlayıcısını kurmaz
    const float RainTurbulenceCalm = 0.03f;
    const float RainTurbulenceStorm = 0.25f;
    // gölgelendirici havanın rengini bununla çarpıp parlatıyor, böylece şafakta turuncu,
    // gece koyu, şimşekte parlak oluyor. Sabit beyaz, kapalı gökyüzünün önünde patlayıp
    // yıldız gibi duruyordu.

    static readonly int BoxSizeId = Shader.PropertyToID("_BoxSize");
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
    static readonly int RainDensityId = Shader.PropertyToID("_RainDensity");

    static readonly int RainDriftsId = Shader.PropertyToID("_RainDrifts");
    static readonly int RainDriftsNearId = Shader.PropertyToID("_RainDriftsNear");
    static readonly int NearBoxSizeId = Shader.PropertyToID("_NearBoxSize");
    static readonly int RainDirectionsId = Shader.PropertyToID("_RainDirections");

    /// Atmosfer yazıyor, burada yalnız OKUNUYOR: rüzgâr eşiği geçilmediyse toz alt
    /// parçası hiç çizilmesin diye. İkinci bir eşik hesabı kurmak iki sistemi ayırırdı.
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int PrecipitationId = Shader.PropertyToID("_Precipitation");
    static readonly int RainTurbulenceId = Shader.PropertyToID("_RainTurbulence");
    static readonly int WindSweepId = Shader.PropertyToID("_WindSweep");
    static readonly int RainColorId = Shader.PropertyToID("_RainColor");

    Mesh mesh;
    Material material;
    readonly Vector4[] rainDrifts = new Vector4[RainSpeedClasses];
    readonly Vector4[] rainDriftsNear = new Vector4[RainSpeedClasses];
    readonly Vector4[] rainDirections = new Vector4[RainSpeedClasses];
    readonly Vector3[] rainVelocities = new Vector3[RainSpeedClasses];
    Vector3 windSweep;
    float density;
    float precipitation;

    /// bir şey varsa hangisinin olduğu başka türlü ayrılamıyor.
    public float DebugRainIntensity => precipitation;
    public float DebugDensity => density;
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

        RefreshDensity();

        // Süzülmüş hız sıfırdan başlarsa ilk karelerde yön vektörü sıfıra normalize
        // olur ve shader'da NaN üretir; düşme hızıyla başlatılır.
        for (int i = 0; i < RainSpeedClasses; i++)
        {
            float t = i / (RainSpeedClasses - 1f);
            rainVelocities[i] = Vector3.down * TerminalVelocity(t);
            rainDirections[i] = WithSpeed(rainVelocities[i]);
        }
    }

    /// Yön ve BÜYÜKLÜK tek vektörde: `xyz` birim yön, `w` bileşke hız (m/s).
    ///
    /// Büyüklük shader'a lazım çünkü damla başına yön sapması bir ORAN: türbülans
    /// hız dalgalanmasının bileşke hıza oranı. Normalize edilmiş yön tek başına o
    /// oranı kuramaz — payda kaybolur ve sapma dingin havada da fırtınadaki kadar
    static Vector4 WithSpeed(Vector3 velocity)
    {
        float speed = velocity.magnitude;
        Vector3 direction = speed > 1e-4f ? velocity / speed : Vector3.down;
        return new Vector4(direction.x, direction.y, direction.z, speed);
    }

    /// İZ VERİTABANINI KARE BAŞINA HAZIRLAR — `[Garg 2006, §5]`.
    ///
    /// Üç açı da burada belirleniyor ve üçü de FARKLI şeye bağlı:
    ///   ışığın yüksekliği — güneşin damlanın düşüş eksenine göre açısı
    ///   ışığın azimutu   — aynı açının kameranın eksenine göre bileşeni
    ///   `θ_v`            — kameranın bakışıyla düşüş yönü arasındaki açı (shader'da,
    ///                      damla başına, çünkü ekranın her yerinde farklı)
    void UpdateStreaks(Vector3 rainVelocity)
    {
        if (streaks == null || timeOfDay == null) return;

        var camera = Camera.main;
        if (camera == null) return;

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

        material.SetFloat(StreakSourceScaleId, SourceScale);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
    }

    /// YOĞUNLUK OLAYDA DEĞİL, HER KARE.
    ///
    /// Eskiden `WeatherState.Changed` olayında bir kez hesaplanıyordu. Ama
    /// girdilerinden biri `SnowRuntimeState.RainWeight01` ve o kar oranı
    /// sürgüsüyle değişiyor — sürgü hava olayı YAYINLAMIYOR. Sonuç: kar oranı
    /// 1'ken bile yağmur son olaydaki yoğunlukta çizilmeye devam ediyordu
    /// (ölçüldü: `RainWeight01 = 0` iken ekran yağmur izleriyle doluydu).
    void RefreshDensity()
    {
        WeatherState state = weather;
        if (state == null) return;

        // KAR YAĞARKEN YAĞMUR SUSUYOR (kar spec §3.4, §17.1).
        //
        // `SnowRuntimeState` kar sisteminin YAYINLADIĞI durum; okumak
        // sistemler arası çağrı değil, ilan edilmiş arayüz. Kar sistemi de
        // buradan hiçbir şey okumuyor — bağ tek yönlü.
        float rainIntensity = state.Precipitation * SnowRuntimeState.RainWeight01;

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
        density = Mathf.Pow(rainIntensity, DensityExponent)
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.05f, rainIntensity));

        precipitation = rainIntensity;
    }

    void Update()
    {
        EnsureResources();
        RefreshDensity();

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
            rainDriftsNear[i] = Advance(rainDriftsNear[i], rainVelocities[i], NearBoxSize);
            rainDirections[i] = WithSpeed(rainVelocities[i]);
        }
        // daha küçük, havanın hızına anında oturuyor. Dikey kayma yok, çünkü tanenin
        // yerden yüksekliği kutudan değil arazi yüzeyinden türüyor.

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

        // Girdap alanının rüzgârla sürüklenme ötelemesi (Taylor: türbülans ortalama
        // akışla taşınır). Sarılmıyor: alanın içinde 0.7-0.9 gibi karışık frekans
        // çarpanları var, hiçbir periyot hepsine ortak gelmiyor ve sarma anı
        // girdapları görünür biçimde ışınlıyor. Sarmamak yalnızca sin argümanını
        // büyütür — shader zaten _Time.y ile aynı büyüklükte argüman kullanıyor.
        windSweep += wind.Velocity * Time.deltaTime;

        material.SetVector(BoxSizeId, BoxSize);
        // KOŞULSUZ: temsil payı buradan türüyor ve `UpdateStreaks`'in önünde dört erken
        // çıkış var. Uniform yazılmazsa HLSL varsayılanı sıfır olur, yoğunluk tabana
        material.SetVector(RainDensityId, new Vector4(OuterDensity, NearDensity, 0f, 0f));
        material.SetVector(NearBoxSizeId, NearBoxSize);
        material.SetVectorArray(RainDriftsId, rainDrifts);
        material.SetVectorArray(RainDriftsNearId, rainDriftsNear);
        material.SetVectorArray(RainDirectionsId, rainDirections);
        material.SetFloat(DensityId, density * localFactor);
        material.SetFloat(PrecipitationId, precipitation * localFactor);

        UpdateStreaks(rainVelocities[RainSpeedClasses / 2]);
        material.SetFloat(RainTurbulenceId,
            Mathf.Lerp(RainTurbulenceCalm, RainTurbulenceStorm, felt));
        material.SetVector(WindSweepId, windSweep);
        material.SetColor(RainColorId, RainColor);

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

            // İç kutu bayrağı `y`'de. `x` zaten tür için kullanılıyor, `z` boş kalıyor.
            float near = i < NearParticles ? 1f : 0f;

            int v = i * 4;
            corners[v + 0] = new Vector2(0f, 0f);
            corners[v + 1] = new Vector2(1f, 0f);
            corners[v + 2] = new Vector2(1f, 1f);
            corners[v + 3] = new Vector2(0f, 1f);

            for (int c = 0; c < 4; c++)
            {
                positions[v + c] = new Vector3(kind, near, 0f);
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
        built.subMeshCount = 1;
        built.SetIndices(indices, 0, PrecipitationParticles * 6,
                         MeshTopology.Triangles, PrecipitationSubMesh, false);

        // Konumlar shader'da üretildiği için hesaplanan sınırlar anlamsız; culling'i kapat
        built.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return built;
    }
}
