using UnityEngine;

/// Rüzgâr vektörünü üretir. Yağış onu tüketen ilk sistem, tek sahibi değil —
/// tırmanma ve ses de buradan okuyacak.
///
/// Hız iki zaman ölçeğinden oluşur: yavaş taban (havanın kendisi) ve hızlı esinti.
/// Ne kadar eseceğini dışarıdan gelen Severity belirler; yükseklik ve fırtına
/// mantığı havanın yönetmenine aittir, burada değil.
public class WindField : MonoBehaviour
{
    [SerializeField] WindSettings settings;

    /// 0 = neredeyse durgun, 1 = tam fırtına. Havanın yönetmeni yazar.
    public float Severity { get; set; } = 0.3f;

    /// ARAZİ MARUZİYETİ. 1 = açık sırt, 0 = korunaklı oyuk. Dışarıdan itiliyor:
    /// rüzgâr araziyi bilmez, ama arazi rüzgârı bilir. `AltitudeWeatherDriver`
    /// `Severity`'yi nasıl sürüyorsa `TerrainWindShelter` de bunu öyle sürer.
    ///
    /// Rüzgâr global olduğu sürece sırtın tepesi ile vadinin dibi aynı esiyordu;
    /// oysa dağda hissedilen farkın en büyüğü budur.
    public float Exposure { get; set; } = 0.6f;

    /// HÂKİM rüzgâr yönü, birim vektör. Anlık `Velocity`'den ayrı: esinti ve yalpa
    /// içermiyor.
    ///
    /// Kar birikintisi ve sastrugi bunu okur, `Velocity`'yi değil. İkisi karıştırılınca
    /// desen dünyada kayıyor: alan `dot(worldXZ, windAxis)` üzerinden kuruluyor ve
    /// dağın ortasında |worldXZ| yedi bin metre — bir hamlenin 0.14 radyanlık sapması
    /// deseni 980 metre sürüklüyordu (gövde 45 m). Tanecik, ses ve savrulma anlık
    /// rüzgârı okumaya devam ediyor.
    public Vector3 PrevailingDirection
    {
        get
        {
            float angle = overrideActive ? overrideAngle : PrevailingAngle;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }

    float PrevailingAngle => settings.prevailingDegrees * Mathf.Deg2Rad;

    /// Dünya uzayında rüzgâr hızı (m/s). Dikey bileşen yok.
    public Vector3 Velocity { get; private set; }

    /// 0-1 sürekli şiddet, esinti içermez. Severity 1'de tam 1'e ulaşır.
    /// Yavaş tepki veren sistemler bunu okur: görüş sekiz saniyelik bir esintiyle
    /// açılıp kapanmaz, bulut katmanı esintiyle inip kalkmaz.
    public float Strength { get; private set; }

    /// SERBEST HAVA HIZI (m/s): yalnız `Severity`'den türer. Arazi maruziyeti ve esinti
    /// UYGULANMAZ.
    ///
    /// Bulut katmanı bunu okur. `Strength` maruziyetle ölçekleniyor; onu okusaydı oyuncu
    /// kayanın arkasına geçtiğinde iki kilometre yukarıdaki bulut yavaşlardı.
    public float FreeAirSpeed => Mathf.Lerp(settings.calmSpeed, settings.stormSpeed,
        ShapeSeverity(overrideActive ? overrideSeverity : Mathf.Clamp01(Severity)));

    /// ŞİDDET → HIZ EĞRİSİ. Doğrusal değil, kare.
    ///
    /// Bu bir fizik yasası değil, DAĞILIM kararı — açıkça öyle. Şiddetin kendisi
    /// Perlin'den geliyor ve zamanın çoğunu orta bantta geçiriyor. Doğrusal eşlemede
    /// "yarı yarıya rüzgâr" doğrudan 8 m/s, yani Beaufort 5 demekti ve oyun sürekli
    /// sert esintide geçiyordu.
    ///
    /// Belirti yağmurda okundu: 0.57 şiddette rüzgâr 8.5 m/s ve 1 mm'lik damla
    /// yataydan 25° iniyordu — fizik doğru, rüzgâr fazlaydı. Kullanıcı "yatayda
    /// hareket eden damlalar var" dedi; ölçüm sürüklenmeyi doğruladı (F1 → rüzgâr
    /// sürüklenmesi kapatılınca bitti).
    ///
    /// UÇLAR SABİT: 0 → calmSpeed, 1 → stormSpeed. Yalnız orta bant iniyor:
    ///
    ///   şiddet 0.25 → 5.0 yerine 2.0 m/s
    ///   şiddet 0.50 → 8.0 yerine 2.8 m/s
    ///   şiddet 0.75 → 11.0 yerine 5.8 m/s
    ///   şiddet 0.90 → 12.8 yerine 9.9 m/s
    ///
    /// ÜS 4 SÜRGÜYLE BULUNDU, hesapla değil — doğru değer tercih. Sürgü F1'deydi,
    /// değer bulununca silindi.
    ///
    /// EŞİKLER KORUNUYOR: `Strength` de bu hızdan türüyor, yani sürüklenen karın rüzgâr
    /// eşiği (0.22) artık şiddet ~0.56'da açılıyor, tam blizzard ~0.87'de. Fırtına
    /// olay oluyor, kural değil.
    ///
    /// Dingin hava kural, fırtına olay olur.
    ///
    /// TEK YERDE UYGULANIR. `Strength` de bu hızdan türüyor, yani rüzgâra bağlı bütün
    /// sistemler (sis kapanması, sürüklenen kar eşiği, girdap genliği, ses, bulut
    /// hızı) birlikte iniyor. Ayrı ayrı ayarlanırsa hava kendi içinde çelişir.
    static float ShapeSeverity(float severity)
    {
        float s = Mathf.Clamp01(severity);
        float sq = s * s;
        return sq * sq;
    }

    /// Sürekli şiddetin üstüne binen anlık sapma, -1..1. Duyulan ve görülen şey budur.
    ///
    /// Tek bir sayı ikisini birden taşıyamıyordu: esinti fırtına hızını aştığında
    /// normalize şiddet kırpılıyor, ama hız kırpılmıyordu. Sonuçta tanecikler
    /// hızlanmaya devam ederken ses ve görüş tavana yapışık kalıyordu — aynı rüzgârı
    /// iki tüketici farklı şiddette görüyordu.
    public float Gust { get; private set; }

    bool overrideActive;
    float overrideSeverity;
    float overrideAngle;

    public void Bind(WindSettings tuning) => settings = tuning;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException(
                $"{nameof(WindField)}: {nameof(settings)} atanmadı.");
    }

    /// Test için taban şiddeti ve yönü sabitler; dalgalanma (taban salınımı + esinti)
    /// üstünde çalışmaya devam eder. Eski kilit Gust'ı sıfırlayıp bileşeni kapatıyordu:
    /// gerçek rüzgâr hiç düz esmez, sürgü hangi değerde olursa olsun ölü bir rüzgâr
    /// test ediliyordu.
    public void ApplyOverride(float strength, float angleDegrees)
    {
        overrideActive = true;
        overrideSeverity = Mathf.Clamp01(strength);
        overrideAngle = angleDegrees * Mathf.Deg2Rad;
    }

    /// Kilidi kaldırır: şiddet yeniden Severity'den, yön kendi kaymasından gelir.
    public void ClearOverride() => overrideActive = false;

    void Update()
    {
        float t = Time.time;

        // Sürekli hız yalnızca Severity'den gelir. Gürültü artık bu değeri üretmiyor,
        // üstüne binen sapmayı üretiyor: tam fırtına Perlin'in ne verdiğinden bağımsız
        float severity = ShapeSeverity(
            overrideActive ? overrideSeverity : Mathf.Clamp01(Severity));
        float sustained = Mathf.Lerp(settings.calmSpeed, settings.stormSpeed, severity);

        // MARUZİYET SÜREKLİ HIZI ÖLÇEKLER, hamleyi değil: sırt rüzgârı hızlandırır,
        // oyuk keser. Hamlenin oransal yapısı ikisinde de aynı — korunaklı yerde de
        // rüzgâr nefes alır, sadece küçük eser.
        //
        // Kilitliyken devre dışı: test sürgüsü ne verdiyse o olmalı, arazi karıştırmasın.
        if (!overrideActive)
            sustained *= Mathf.Lerp(settings.shelteredFactor, settings.exposedFactor,
                                    Mathf.Clamp01(Exposure));

        // Yavaş katman: havanın genel hali, dakikalar ölçeğinde
        float slow = Mathf.PerlinNoise(t * settings.baseFrequency, 0f) * 2f - 1f;

        // Hızlı katman: esintiler, saniyeler ölçeğinde. İşaret korunarak karesi
        // alınır: Perlin'in simetrik salınımı rüzgârı soluyormuş gibi gösteriyordu.
        // Gerçek anemometre grafiği testere gibidir — sivri hamleler, arada sakin
        // platolar. Kare alma tepeleri sivriltir, ortaları platoya yatırır; 1.4
        // kaybolan genliği geri koyar.
        float fast = Mathf.PerlinNoise(t * settings.gustFrequency, 37f) * 2f - 1f;
        fast = fast * Mathf.Abs(fast) * 1.4f;

        // Sarsıntı katmanı: saniye altı çarpmalar. Esinti 12 saniyelik dalga;
        // ceketi dalgalandıran 1-3 saniyelik türbülans tepesi bu katmandan gelir.
        float flicker = Mathf.PerlinNoise(t * settings.flickerFrequency, 71f) * 2f - 1f;

        // Fırtına hamleyi sertleştirir: aynı oransal esinti dingin günde nazik,
        // fırtınada hırçın vurur.
        float buffet = Mathf.Lerp(0.75f, 1.25f, severity);

        // Katmanlar toplanır, çarpılmaz: çarpılınca genlikler birbirini büyütüyor ve
        // tavan 1.75 katına çıkıyordu. Şiddet yükseldiğinde esinti fırtına hızını aşıyor,
        // normalize değer kırpılıyor ve zirvede rüzgâr nefes almayı bırakıyordu.
        // Alt sınır -1: sürgüler birden sonuna açılırsa hız ters yöne dönmesin.
        Gust = Mathf.Clamp(slow * settings.baseVariation
             + (fast * settings.gustAmount + flicker * settings.flickerAmount) * buffet,
             -1f, 1f);

        float speed = sustained * (1f + Gust);

        // Yön HÂKİM eksenin etrafında oynuyor, serbest dönmüyor. Yüzeydeki kar
        // birikintisi bu eksene oturuyor ve eksen kayarsa bütün desen dünyada
        // sürükleniyor (bkz. WindSettings.directionSpread).
        float prevailing = PrevailingAngle;
        float wander = Mathf.PerlinNoise(0f, t * settings.directionDrift) * 2f - 1f;

        float angle = overrideActive
            ? overrideAngle
            : prevailing + wander * settings.directionSpread * Mathf.Deg2Rad;

        // Hamle yönü de kımıldatır: her esinti birkaç derece saptırır, rüzgâr
        // yalpalar. Kilitliyken de geçerli — sabitlenen yön, esintinin etrafında
        // oynadığı eksendir.
        angle += fast * 0.14f;

        Velocity = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * speed;

        Strength = Mathf.Clamp01(sustained / settings.stormSpeed);
    }
}
