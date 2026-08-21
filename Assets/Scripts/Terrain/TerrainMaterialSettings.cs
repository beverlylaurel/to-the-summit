using UnityEngine;

/// Dağ yüzeyinin görünümü. Katmanların "nerede olacağı" dağın kendi biçiminden okunur
/// (bkz. SurfaceMapBaker); buradaki değerler yalnızca ne kadar ve hangi renkte olacağını
/// belirler.
[CreateAssetMenu(menuName = "To The Summit/Terrain Material", fileName = "TerrainMaterialSettings")]
public class TerrainMaterialSettings : ScriptableObject
{
    /// Ayar her değiştiğinde artan sayaç. `TerrainSurface` bunu okuyup materyale
    /// yazmayı yalnız gerçekten değişince yapıyor: kırk küsur alan her kare tekrar
    /// gönderiliyordu ve hiçbiri kendiliğinden değişmiyor.
    ///
    /// Inspector ve ayar penceresi `OnValidate`'i tetikliyor, yani canlı ayar
    /// yapılırken geri bildirim anında geliyor.
    [System.NonSerialized] public int revision;

    void OnValidate() => revision++;

    [Header("Kaya")]
    public Color rockPrimary = new(0.13f, 0.14f, 0.16f);
    [Tooltip("Jeolojik bantlarda görünen ikincil kaya.")]
    public Color rockSecondary = new(0.27f, 0.26f, 0.24f);
    [Tooltip("Yüzey tanesinin ölçeği. Büyük değer ince tane.")]
    public float grainScale = 0.08f;
    [Range(0f, 1f)] public float grainStrength = 0.35f;
    [Tooltip("Kayanın parlaklığı. Islak kaya bunun üstüne çıkar.")]
    [Range(0f, 1f)] public float rockSmoothness = 0.12f;

    [Header("Jeolojik bantlar")]
    [Tooltip("Bir bandın kalınlığı (metre).")]
    public float bandThickness = 130f;
    [Tooltip("Bantların tektonikle bükülme miktarı (metre). Sıfır = yapay düz çizgiler.")]
    public float bandWarp = 150f;
    public float bandWarpScale = 0.0016f;
    [Range(0f, 1f)] public float bandContrast = 0.5f;

    [Header("Rakım tonu")]
    [Tooltip("Alçak rakımda kayaya karışan sıcak toprak tonu.")]
    public Color lowlandTint = new(0.30f, 0.26f, 0.20f);
    [Tooltip("Yüksek rakımda buzul aşındırmasının bıraktığı soğuk ton.")]
    public Color alpineTint = new(0.29f, 0.32f, 0.37f);
    [Tooltip("Toprak tonunun tamamen bittiği yükseklik (metre).")]
    public float lowlandCeiling = 1400f;
    [Tooltip("Soğuk tonun başladığı yükseklik (metre).")]
    public float alpineFloor = 3200f;
    [Range(0f, 1f)] public float altitudeTintStrength = 0.5f;

    [Header("Liken — konkavlık, gölge ve rakım")]
    public Color lichenColor = new(0.33f, 0.35f, 0.21f);
    [Range(0f, 1f)] public float lichenAmount = 0.5f;
    [Tooltip("Likenin yaşayabildiği en yüksek kot (metre).")]
    public float lichenCeiling = 2600f;
    [Tooltip("Nemin ne kadar oyuk gerektirdiği. Yüksek değer yalnızca derin yarıklara koyar.")]
    [Range(0f, 1f)] public float lichenMoistureBias = 0.55f;
    [Tooltip("Güneş gören yüzlerde likenin ne kadar kuruduğu.")]
    [Range(0f, 1f)] public float lichenSunSensitivity = 0.7f;

    [Header("Oksit — jeolojik bantları izler")]
    public Color oxideColor = new(0.40f, 0.20f, 0.10f);
    [Tooltip("Demirli katmanların payı. Lekeler bantların dışına çıkmaz.")]
    [Range(0f, 1f)] public float oxideAmount = 0.3f;
    [Tooltip("Leke ölçeği. Küçük değer geniş lekeler.")]
    public float oxideScale = 0.004f;

    [Header("Çakıl — birikim haritasından")]
    public Color screeColor = new(0.30f, 0.29f, 0.27f);
    [Range(0f, 1f)] public float screeAmount = 0.6f;
    [Tooltip("Birikim haritasında çakılın başladığı ve tamamlandığı eşikler. " +
             "Aralık dar tutulunca yalnızca en yoğun oluklar seçilir.")]
    public Vector2 screeRange = new(0.62f, 0.88f);
    [Tooltip("Çakılın tutunabildiği en dik açı (derece).")]
    [Range(10f, 60f)] public float screeSlopeLimit = 38f;

    [Header("Kar")]
    public Color snowColor = new(0.90f, 0.93f, 0.98f);
    [Tooltip("Karın tutunabildiği en dik açı (derece). Buradaki soru KALINLIK değil " +
             "TUTUNMA: karın duruş açısı 70-75 dereceye kadar çıkar, dik yüzde ince " +
             "bir kaplama olarak durur. Literatürdeki 50-55 derece rakamı çığ " +
             "üretecek kalınlıkta slab birikmesinin eşiği — bu ayarın anlamı o değil. " +
             "52 denendi: 55 derecenin üstü tamamen çıplak kaldı, dağ kahverengiye " +
             "döndü.")]
    [Range(20f, 75f)] public float snowSlopeLimit = 65f;
    [Tooltip("Kar sınırının gürültüyle kırılması. Sıfır = jilet gibi yapay çizgi.")]
    [Range(0f, 1f)] public float snowBreakup = 0.5f;
    [Tooltip("Kalın karın kayanın kabartısını gömme oranı. Örtü bir renk değil bir " +
             "kalınlık: altındaki taş dokusu derinleştikçe kaybolmalı.")]
    [Range(0f, 1f)] public float snowBurial = 0.9f;
    [Tooltip("Kalın karın yüzeyi yuvarlaması. Kar çukurları doldurup sırtları " +
             "körleştirir; fazlası dağı düzleştirip hacmini alır.")]
    [Range(0f, 0.5f)] public float snowRounding = 0.28f;

    [Header("Kar birikintisi")]
    [Tooltip("Birikinti alanının derinliğe karışma payı. 0 = kapalı (kar dört metrenin " +
             "altında dümdüz), 1 = tam. Alan rüzgâr eksenine hizalı: yığınlar rüzgâr " +
             "boyunca uzar, ona dik daralır.")]
    [Range(0f, 1f)] public float snowDriftStrength = 0.75f;
    [Tooltip("Birikinti kenarının ÖRTÜYÜ inceltme payı. Kazınmış şerit yer yer delinip " +
             "altındaki taşı gösterir. Fazlası dağı benekli yapıyor.")]
    [Range(0f, 1f)] public float snowDriftCoverBite = 0.45f;

    [Tooltip("En kalın birikintinin yüksekliği (metre). Kar artık GEOMETRİ: kayaların " +
             "dibinde dolgu, sırtta korniş, oyukta yığılma. Fazlası dağı şişiriyor.")]
    /// Prosedürel yüzeyin tohumu. Kaya bandı, oksit, liken, tanecik, kırılma ve
    /// birikinti şeklinin tamamı dünya koordinatına bağlı; tohum değişmeden arazi
    /// baştan üretilse bile aynı koordinatta aynı desen çıkıyor.
    ///
    /// Dağ yeniden üretildiğinde bu da artırılır, yoksa eski dağdan yerler tanıdık
    /// gelir — bir kez yaşandı ve ölçüldü.
    public int patternSeed = 2;

    [Range(0f, 8f)] public float snowDisplaceMax = 3.2f;
    [Tooltip("Bu derinliğin altında geometri hiç oynamıyor. İnce örtü arazi " +
             "ızgarasında zaten çözülemiyor; uygulanınca bütün dağ hafifçe şişiyor.")]
    [Range(0.02f, 1f)] public float snowDisplaceStart = 0.18f;
    [Tooltip("Bölünme katsayısı: en yakın yamada kenar başına kaç parça. Kar " +
             "birikintisinin yüzey dalgası ~2.6 m; arazi 4.28 m/örnek.")]
    [Range(1f, 16f)] public float snowTessFactor = 6f;
    [Tooltip("Bu mesafeye kadar tam bölünme (metre).")]
    [Range(10f, 200f)] public float snowTessNear = 35f;
    [Tooltip("Bu mesafeden sonra bölünme kapalı (metre). Arazi LOD'u değişmeden " +
             "önce bitmeli: farklı LOD'daki komşu yamaların ortak kenarı aynı " +
             "köşeleri taşımıyor ve bölünme oraya kadar sürerse çatlak açılıyor.")]
    [Range(20f, 400f)] public float snowTessFar = 80f;

    [Tooltip("Kar mikro doku periyodu (metre). Kar taneleri ve rüzgâr kabuğu bu " +
             "ölçekte tekrarlar. Büyük değer deseni belli eder, küçük değer " +
             "uzaklaşınca kaynar.")]
    [Range(0.3f, 6f)] public float snowDetailTiling = 1.6f;
    [Tooltip("Mikro kabartmanın gücü. Sıfır = doku kapalı, yalnız prosedürel kabartı.")]
    [Range(0f, 2f)] public float snowDetailStrength = 0.6f;
    [Tooltip("Dokunun pürüzlülüğünün cilaya karışma payı. Cilanın sahibi hâlâ kar " +
             "sistemi; doku yalnız yüzey düzensizliğini ekliyor.")]
    [Range(0f, 1f)] public float snowDetailRoughness = 0.5f;
    [Tooltip("Bu mesafede tamamen söner (metre). Uzakta texel piksel altına düşüp " +
             "kaynıyor; söndürmek hem doğru hem ucuz.")]
    [Range(5f, 150f)] public float snowDetailFade = 60f;
    [Tooltip("Güneş gören yamaçta kar çizgisinin yükselmesi (metre). Öğle güneşine " +
             "bakan yüzler karı eritir; kuzey yüzü aynı kotta karlı kalır.")]
    [Range(0f, 500f)] public float snowlineSunLift = 200f;
    [Tooltip("Oluklarda kar çizgisinin sarkması (metre). Kar, dere yataklarından dil " +
             "gibi aşağı iner — gölge ve soğuk hava orada tutar.")]
    [Range(0f, 400f)] public float snowlineGullyDrop = 150f;
    [Tooltip("Kar çizgisinin kilometre ölçeğindeki düzensizliği (metre). Sıfırda çizgi " +
             "dağın çevresini dolaşan temiz bir kot konturu olur ve boyanmış durur.")]
    [Range(0f, 300f)] public float snowlineRagged = 120f;
    [Tooltip("Rüzgârın kar yüzeyini tarama gücü (sastrugi). Rüzgâr karı kendi yönünde " +
             "oyar ve biriktirir, yüzeyde o yöne uzanan sırtlar bırakır.")]
    [Range(0f, 1f)] public float sastrugi = 0.6f;
    [Tooltip("Kar kalınlığının genel ölçeği. Kalınlık üç çarpanın çarpımı (eğim, " +
             "rüzgâr yüzü, oyuk) olduğu için ideal koşulda bile 1'e varmıyor; bu " +
             "katsayı doyum noktasını yaklaştırır. Kalınlık kayanın dokusunu gömer: " +
             "1'e yaklaştıkça çıkıntılar kaybolur.")]
    [Range(0.5f, 3f)] public float snowDepthScale = 1.4f;
    [Tooltip("Karın parlaklığı. Taze kar mat, sıkışmış kar parlaktır.")]
    [Range(0f, 1f)] public float snowSmoothness = 0.3f;
    [Tooltip("Kalıcı kar çizgisinin, yağışın tamamen kara döndüğü kotun (sürücünün " +
             "`SnowFloor`'u) NE KADAR ÜSTÜNDE olduğu (metre). Mutlak kot olarak " +
             "verilmez: ayrı bir sabit tutulunca zemin, tepeden hâlâ yağmur yağarken " +
             "beyazlıyordu. Kalıcı kar denge çizgisidir, donma seviyesinin üstünde " +
             "kalır — bu yüzden pozitif. **Yumuşatma bandından büyük tutulmalı**: küçük " +
             "kalırsa çizginin alt ucu kar kuşağının içine sarkar ve aynı çelişki dar bir " +
             "şeritte geri gelir.")]
    public float permanentSnowRise = 400f;
    [Tooltip("Çizginin yumuşaklığı (metre). Keskin bir kot yapay bir bant bırakır.")]
    public float permanentSnowBand = 350f;
    [Tooltip("Tam şiddetteki kar yağışında örtünün tamamlanma süresi (saniye). Birikme hızı " +
             "yağışın şiddetiyle orantılı: yarı şiddette iki katı sürer.")]
    /// OYUNUN KENDİ SAATİNDEN TÜRÜYOR. `TimeOfDay.dayLengthMinutes = 40`, yani gün
    /// 40 gerçek dakika: zaman 36 kat sıkışık. Şiddetli kar 5 cm/sa yağar ve zeminin
    /// görünür biçimde beyazlaması ~2 cm ister — gerçek dünyada 24 dakika, oyunun
    /// saatinde 40 saniye. Eski değer 90'dı, yani oyunun kendi zamanından iki kat
    /// yavaştı ve geçip giden bir kar fırtınası zemine hiç dokunamıyordu.
    public float snowAccumulationSeconds = 40f;
    [Tooltip("Rüzgârın gevşek karı süpürüp bitirme süresi (saniye), tam kaldırmada. " +
             "Sürüklenme kaynağını TÜKETİR: rüzgâr yerdeki karı alıp götürür ve yeni " +
             "kar yağmadıkça sürüklenecek bir şey kalmaz. Bu olmadan perde sonsuza " +
             "kadar aynı şiddette akıyordu.")]
    public float snowScourSeconds = 300f;
    [Tooltip("Süblimasyon süresi (saniye). Sıfırın ALTINDA karın tek kaybı budur: " +
             "erimeden buhara geçer. Çok yavaş olmalı — dağın karı bir koşu boyunca " +
             "gözle görülür şekilde azalmamalı, ama sonsuza kadar da birikmemeli.")]
    public float snowSublimationSeconds = 36000f;
    [Tooltip("SIFIRIN ÜSTÜNDE, tam ılıklıkta (+6 °C) erime süresi (saniye). Daha " +
             "serinde kareyle yavaşlar, sıfırın altında tamamen durur — kar erimez, " +
             "yalnız süblimleşir.")]
    public float snowMeltWarmSeconds = 600f;
    [Tooltip("Donma seviyesinin altında kalınlık deposunun boşalma süresi (saniye).")]
    public float snowPackMeltWarmSeconds = 300f;
    [Tooltip("Tam şiddetteki karda kalınlık deposunun dolma süresi (saniye). Örtü hızlı " +
             "kapanır, kalınlık arkadan gelir: önce serpinti, sonra beyazlık, dolgunluk en son.")]
    public float snowPackSeconds = 360f;

    [Header("Islaklık")]
    [Tooltip("Yağışta kayanın ne kadar koyulaşacağı.")]
    [Range(0f, 1f)] public float wetDarkening = 0.45f;
    [Tooltip("Islak yüzeyin kazandığı parlaklık.")]
    [Range(0f, 1f)] public float wetSmoothness = 0.6f;
    [Tooltip("Yağış dindikten sonra kuruma süresi (saniye).")]
    public float dryingSeconds = 120f;

    [Header("Yüzey kabartısı")]
    [Tooltip("Prosedürel normalin gücü. Sıfır = plastik görünüm.")]
    [Range(0f, 2f)] public float bumpStrength = 0.9f;
    public float bumpScale = 0.3f;

    [Header("Alpenglow — şafak ve gün batımı")]
    [Tooltip("Güneş ufka yakınken yüksek yüzeylerin kızıl parlaması. Dağcılığın en " +
             "tanınmış görüntüsü; ışıktan gelir, sisten değil. Arazi gölgesine tabidir " +
             "ve kaynak rengi artık gerçekten kızıl — eski 2.2, gölgesiz emisyonla " +
             "birleşince şafakta sahneyi yakıyordu.")]
    // 0.9 -> 0.35: eskiden parlama irtifa rampasıyla (tabanda ×0.35) kısılıyordu.
    // Rampa Dünya gölgesine devredilince o kısma kalktı ve her yüzey tam gücü aldı —
    // batışta sahne düz kırmızıya boyanıyordu.
    [Range(0f, 4f)] public float alpenglowStrength = 0.35f;
    [Tooltip("Güneşe bakan yüzlerin ne kadar öne çıkacağı. Sıfırda her yön eşit parlar. " +
             "Yalnız güneş ufkun üstündeyken çalışır: battıktan sonra aydınlatan şey " +
             "noktasal bir kaynak değil, kızıla boyanmış bütün gökyüzüdür.")]
    [Range(0f, 1f)] public float alpenglowFacing = 0.7f;

    [Header("Gölgelenme")]
    [Tooltip("Maruziyet haritasının ambient ışığı ne kadar kısacağı. Vadi dipleri koyulaşır.")]
    [Range(0f, 1f)] public float cavityStrength = 0.55f;
}
