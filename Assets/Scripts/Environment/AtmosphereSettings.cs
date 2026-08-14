using UnityEngine;

/// Atmosferin görünüm ayarları. Sis, bulut ve gökyüzünün bütün sayıları burada.
///
/// Bileşenin üstünde `[SerializeField]` olarak durdukları sürece değerin üç kopyası
/// oluyordu: koddaki varsayılan, sahnedeki serileştirilmiş kopya ve asset. Sahne kazanıyor,
/// üstelik Unity sahneyi kendi belleğinden istediği an diske yeniden yazıyor — koddan
/// yapılan bir düzeltme sessizce kayboluyordu. Tek dosyada yaşayınca ayrışacak ikinci
/// kopya kalmıyor.
[CreateAssetMenu(menuName = "To The Summit/Atmosphere", fileName = "AtmosphereSettings")]
public class AtmosphereSettings : ScriptableObject
{
    [Header("Görüş mesafesi (metre)")]
    [Tooltip("Açık havada, gündüz. Dağın iki bin metresinde gerçek açık hava görüşü " +
             "100-200 km; temiz havada Rayleigh tek başına ~300 km verir. Düşük tutmak " +
             "yalnız uzağı soldurmuyor: zeminden buluta 2.6 km tırmanan ışının optik " +
             "derinliğini de şişirip bulut denizini siliyor.")]
    public float clearVisibility = 25000f;
    [Tooltip("En şiddetli yağmurda.")]
    public float rainVisibility = 900f;
    [Tooltip("Rüzgârsız yoğun karda.")]
    public float snowVisibility = 320f;
    [Tooltip("Sert rüzgârda görüşün ek olarak kısaldığı oran. Tipi görüşü kapatır.")]
    [Range(0f, 0.9f)] public float windClosure = 0.65f;

    [Header("Renk — gündüz")]
    public Color clearDay = new(0.60f, 0.68f, 0.80f);
    public Color rainDay = new(0.42f, 0.45f, 0.50f);
    public Color snowDay = new(0.80f, 0.84f, 0.90f);

    [Header("Renk — şafak ve gün batımı")]
    [Tooltip("Kapalı havada şafak daha sönük ve solgun olur. Açık havadaki ton " +
             "TimeOfDay'in süzülme hesabından gelir, burada seçilmez.")]
    public Color duskOvercast = new(0.62f, 0.44f, 0.38f);
    [Tooltip("Sıcak tonun ne kadar baskın olduğu.")]
    [Range(0f, 1f)] public float duskStrength = 0.75f;

    [Header("Renk — gece")]
    public Color clearNight = new(0.05f, 0.07f, 0.12f);
    public Color rainNight = new(0.06f, 0.07f, 0.09f);
    public Color snowNight = new(0.14f, 0.16f, 0.22f);

    [Header("Bulut kapsaması")]
    [Tooltip("Açık havadaki bulut kapsaması.")]
    [Range(0f, 1f)] public float clearCoverage = 0.18f;
    [Tooltip("En yoğun yağıştaki kapsama.")]
    [Range(0f, 1f)] public float stormCoverage = 0.95f;
    [Tooltip("Kapsamanın alt sınırı. Altında gökyüzü boş ve bulutlar cılız görünüyor.")]
    [Range(0f, 1f)] public float minCoverage = 0.27f;
    [Tooltip("Açık pencere tam açıldığında inilen kapsama. Tabanı delebilen tek şey bu: " +
             "nadir, kısa, ve tırmanışın ödülü — bulutlar aralanır, zirve görünür.")]
    [Range(0f, 1f)] public float openCoverage = 0.1f;
    [Tooltip("Kapsamanın şiddete göre kazancı. Gökyüzü yağış tam sertleşmeden kapanır; " +
             "zamanda bir önceleme değil, eğrinin dikleşmesi.")]
    [Range(0f, 1f)] public float coverageGain = 0.35f;

    [Header("Bulut katmanı")]
    [Tooltip("Bulut katmanının alt sınırı (metre).")]
    public float cloudBottom = 2600f;
    [Tooltip("Sakin havadaki bulut tabanı (metre). Yağışsız ve rüzgârsız gecede soğuk hava " +
             "vadiye çöker, taban buraya iner ve zirveden bakınca bulut denizi görünür.")]
    public float calmCloudBottom = 1700f;
    [Tooltip("Tabanın yeni yüksekliğine varma süresi (saniye). Kütle ağırdır, rüzgârın " +
             "sekiz saniyelik esintileriyle inip kalkmaz.")]
    public float cloudBottomSmoothing = 120f;
    [Tooltip("Katmanın üst sınırı (metre). Bulut boyları artık METRE cinsinden " +
             "kuruluyor, bu değer yalnızca en yükseğin tavana çarpmaması için var: " +
             "gerçek kümülonimbus troposferin tepesine kadar çıkar.")]
    public float cloudTop = 7000f;
    [Tooltip("Kümülonimbusun (fırtına bulutu) erişebileceği kalınlık (metre). " +
             "Gerçekte 10 km'yi aşar; oyun ölçeğinde zirveyi (5686 m) ezmeyecek " +
             "kadar tutulur.")]
    public float cumulonimbusHeight = 3800f;
    [Tooltip("Bulut tepesinin inebileceği en alçak seviye, katman kalınlığının oranı. " +
             "PİŞİRME girdisi: hava haritasının tavan kanalına işlenir, çalışma anında " +
             "okunmaz. Değiştirince harita yeniden pişirilmeli (To The Summit menüsü).")]
    [Range(0.15f, 1f)] public float cloudTopFloor = 0.55f;

    [Header("Hava haritası (pişirme)")]
    [Tooltip("Haritanın deterministik tohumu. Aynı tohum aynı gökyüzü dağılımını üretir.")]
    public int weatherMapSeed = 86;
    [Tooltip("Haritanın dünya periyodu (metre). Ufuk pusu 45 km'de kapandığı için 48 km " +
             "döşeme tekrarı görünmez; görünürse büyütülür, pişirme maliyeti değişmez.")]
    public float weatherMapWorldSize = 48000f;
    [Tooltip("Tek çekirdeğin en büyük yarıçapı (metre). Dev bulut boyunun ana vidası: " +
             "birleşmeler bunun 2-3 katına çıkabilir.")]
    [Range(1200f, 4500f)] public float coreRadiusMax = 1600f;
    [Tooltip("İstif tavanı: bulutlu bölgelerde çekirdeklerin ne kadar sık dizilebildiği. " +
             "Yükseldikçe birleşme artar — devasa kütleler buradan doğar.")]
    [Range(0.3f, 0.95f)] public float corePacking = 0.9f;
    [Tooltip("Boşluk serpintisi: harita boşluklarına düşen tek tük bulut payı. " +
             "0 = boşluklar bomboş; yükseldikçe boş/dolu farkı silinir.")]
    [Range(0f, 0.5f)] public float packingFloor = 0.35f;
    [Tooltip("Yama penceresi: organizasyon alanının bulutlu saydığı eşik. Küçük değer " +
             "bulutlu bölgeleri genişletir, büyük değer boşluk payını artırır.")]
    [Range(0.30f, 0.60f)] public float patchWindow = 0.35f;
    [Tooltip("Çekirdek yoğunluğu çarpanı: genel gök doluluğu.")]
    [Range(0.4f, 2.2f)] public float coreDensity = 1.6f;

    [Header("Bulut biçimi")]
    [Tooltip("Kütle dokusunun dünya ölçeği. 1/değer = tekrar periyodu.")]
    public float cloudScale = 0.00035f;
    [Tooltip("Aşındırma dokusunun ölçeği. Yüksek frekans: bulutun ince yapısı buradan gelir.")]
    public float detailScale = 0.0011f;
    [Range(0f, 1f)] public float detailStrength = 0.4f;
    [Tooltip("Büyük ölçekli bulut oktavının payı. İki ölçek ortalanmaz, en büyüğü alınır: " +
             "ortalamak ikisini de silip gökyüzünü tek boy bulutla dolduruyordu. Sıfırda " +
             "yalnızca küçük bulutlar kalır; bire yaklaştıkça devasa kütleler baskınlaşıp " +
             "gökyüzünü ele geçirir. Karışım aradadır.")]
    [Range(0f, 1f)] public float largeCloudWeight = 1f;
    [Tooltip("Curl bükümünün gücü (metre). Aşındırma dokusunun okunduğu koordinat " +
             "ıraksamasız bir alanla kaydırılır: kenarlarda burgulu türbülans. Tabanda " +
             "güçlü, tepeye doğru söner — alt kenarların rüzgârla taranmış hâli.")]
    public float curlStrength = 240f;
    [Tooltip("Rüzgâr makaslaması: üst katmanlar alta göre kayar, bulutlar dikey sıvanmaz.")]
    [Range(0f, 1f)] public float shearAmount = 0.6f;
    [Tooltip("Rüzgâr yönünün katman boyunca dönmesi (derece). Gerçek atmosferde " +
             "sürtünme yerdeki rüzgârı saptırır, yükseldikçe düzelir (Ekman spirali): " +
             "bulutun tepesi tabanına göre hem kayar hem DÖNER. 0 = tüm katmanlar aynı " +
             "yönde, kütleler burulmadan öteler.")]
    [Range(0f, 90f)] public float shearTurnDegrees = 28f;
    [Tooltip("Bulut yönünün rüzgâra uyum süresi (saniye). Kısa tutulursa bulut kütlesi " +
             "yer rüzgârıyla birlikte savrulur.")]
    public float headingSmoothing = 240f;
    [Tooltip("Metre başına sönümleme katsayısı. Adım başına biriken optik derinlik bunun " +
             "adım boyuyla çarpımıdır ve ~0.2'yi aşarsa bulut bir-iki adımda opaklaşıp her " +
             "adım sınırı ekranda dilim olarak görünür. 700 m kalınlıkta bir kümülüsün " +
             "opaklaşması için 0.006 civarı doğru; on katı bulutu hacim değil duvar yapar.")]
    public float densityScale = 0.006f;
    [Tooltip("Yer rüzgârının bulut hızına çarpanı. Yüksekteki rüzgâr yerdekinden güçlü: " +
             "yerde 10 m/s esen rüzgâr iki kilometre yukarıda 20 m/s dolayında. Katsayı " +
             "birin altındayken bulutlar yer rüzgârından yavaş gidiyordu, kendi " +
             "açıklamasıyla çelişiyordu.")]
    public float cloudDrift = 6f;
    [Tooltip("Yer rüzgârı dinginken bile bulutların süzülme hızı (m/s). Dingin havada " +
             "yüksek katmanlar durmaz, birkaç metre saniye süzülür. " +
             "Bu değer 90 m/s idi — 324 km/h, yani rüzgâr sıfıra çekilse bile gökyüzü " +
             "akıp gidiyordu. Rüzgâr kilidi bulutları durdurmuyordu çünkü taban zaten " +
             "her şeyi eziyordu.")]
    public float minCloudSpeed = 30f;
    [Tooltip("Bulut biçimlerinin değişme hızı. Sıfırsa bulutlar yalnızca öteler, şekil değiştirmez.")]
    public float evolutionSpeed = 0.004f;
    [Tooltip("Konvektif yükselme hızı (m/s). Bulut kütlesi yerden gelen ısıyla yükselir: " +
             "tomurcuklar tabandan doğup yukarı tırmanır. Gündüz güçlü, gece söner — " +
             "yükselmenin kaynağı ısınan zemindir.")]
    [Range(0f, 3f)] public float convectiveRise = 0.9f;
    [Tooltip("Fırtınada bulutun kalınlaşma oranı. 1 = yağıştan bağımsız sabit kalınlık.")]
    [Range(1f, 4f)] public float stormDensityBoost = 2.4f;

    [Header("Yüksek irtifa katmanı")]
    [Tooltip("Sirrus/alto katmanının açık havadaki varlığı. Hacimsel katmanın çok " +
             "üstünde, ince: ışın yürüyüşü yerine tek kesişimle çizilir.")]
    [Range(0f, 1f)] public float highCloudClear = 0.45f;
    [Tooltip("Fırtınada varlığı. Yaklaşan cephenin önü sirrusla kaplanır; sonra " +
             "hacimsel katman kalınlaştıkça zaten görünmez olur.")]
    [Range(0f, 1f)] public float highCloudStorm = 0.8f;
    [Tooltip("Cinsi: 0 sirrus (tüy çizgileri) · 0.5 altokümülüs (benek tarlası) · " +
             "1 altostratus (düz levha).")]
    [Range(0f, 1f)] public float highCloudType = 0.25f;
    [Tooltip("Katmanın kotu (metre). Hacimsel tavanın belirgin üstünde olmalı.")]
    public float highCloudAltitude = 9000f;
    [Tooltip("Dünya ölçeği. 1/değer = tekrar periyodu; 0.00004 ≈ 25 km.")]
    public float highCloudScale = 0.00004f;

    [Header("Bulut kalitesi")]
    [Tooltip("Adım boyunu belirler: taban adım = 2000 / bu sayı. Maliyetin ana kaynağı, " +
             "ve dilimlenmenin de: adım boyu × yoğunluk ölçeği ~0.2'yi aşmamalı.")]
    [Range(16, 128)] public int raymarchSteps = 100;
    [Tooltip("Işık yönünde örnek sayısı. İkinci en pahalı kalem.")]
    [Range(2, 8)] public int lightSteps = 5;
    [Tooltip("Bu mesafenin ötesinde ince aşındırma dokusu okunmaz; bir pikselden küçük kalır.")]
    public float detailDistance = 9000f;
    [Tooltip("Işın başlangıcını dağıtan Bayer kaymasının gücü. 1'de desen ekrana ham basılıp " +
             "bulutlar dama tahtası gibi görünür; 0'da desen yok ama adım kafesi kenarlarda " +
             "basamak bırakır.")]
    [Range(0f, 1f)] public float cloudDither = 0.2f;
    [Tooltip("Kenar yumuşatması. Eşiğin alt ucunu örnekleme ölçeğiyle aşağı açar: kenarlar " +
             "yumuşar, ama her bulutun çevresinde zayıf bir zar kalır.")]
    [Range(0f, 1f)] public float cloudEdgeSoften = 0.6f;
    [Tooltip("Işın adımının iki katına çıktığı mesafe (metre). Küçük değer ufka yetişir ama " +
             "uzaktaki adımı kalınlaştırıp dilimlenmeyi geri getirir; büyük değer dilimi " +
             "keser, menzili kısaltır.")]
    public float stepGrowthDistance = 2300f;
    [Tooltip("Görüş mesafesinin kaç katında bulut tamamen atmosfere karışır. Bulutlar " +
             "kilometrelerce yukarıda ve havanın o yüksekliği daha berrak.")]
    [Range(2f, 12f)] public float hazeVisibilityFactor = 5.5f;
    [Tooltip("Karışma mesafesinin tavanı (metre). Rakım havayı seyreltir ama sonsuz berrak " +
             "yapmaz; yatay bakışta ışın yine kilometrelerce hava kat eder. Tavansız " +
             "bırakılınca zirvede bulut denizinin ufku sise gömülmeden, çıplak bir çizgi " +
             "olarak bitiyor. Bu ÇİZİM YARIÇAPI DEĞİL: denizin nerede bittiğini gezegen " +
             "yarıçapı belirler. Bu sayı denizin kendi ufkuna eşitlenirse ufuktaki bulut " +
             "tam karışıma girer ve kaybolur — belirgin şekilde uzun tutulur.")]
    public float maxHazeDistance = 55000f;

    [Tooltip("SANAT YÖNÜ: elle boyanmış hava haritası. Kanallar üretilenle aynı anlamı " +
             "taşır — R kapsama, G tip, B taban kayması, A tavan. Boş bırakılırsa " +
             "yalnız üretilen harita kullanılır. Boyanacak taban dosyayı " +
             "'To The Summit/Hava Haritasını PNG Olarak Dışa Aktar' üretir. " +
             "Dokunun Read/Write Enabled olması şart.")]
    public Texture2D artDirectionMap;

    [Tooltip("Elle boyanmış haritanın payı. 0 = tamamen üretilen, 1 = tamamen boyanan. " +
             "Harman PİŞİRMEDE yapılır: kaba sıçrama haritası sonuçtan türediği için " +
             "çalışma zamanında harmanlamak sıçramayı yalancı yapar ve boyanmış " +
             "bulutun üstünden atlar. Çalışma zamanı maliyeti sıfır.")]
    [Range(0f, 1f)] public float artDirectionBlend;

    [Tooltip("Bulut küresinin yarıçapı (metre). Denizin ufka değdiği mesafe sqrt(2·R·Δh) " +
             "ve buradaki Δh gözün DENİZ ÜSTÜNDEKİ payı — zirvede topu topu birkaç yüz " +
             "metre. Küçültmek denizi erkenden aşağı bükip zirvede ufuktaki bulutları " +
             "yok ediyor: 235 km'de deniz 13 km'de bitiyordu. Gerçek yarıçapta uç, " +
             "sönümün kapandığı mesafenin ötesinde kalır ve hiç görünmez.")]
    public float planetRadius = 6360000f;

    [Tooltip("Karışma mesafesinin tabanı (metre). Görüş, ayağının dibindeki havayı " +
             "ölçüyor: yoğun alçak hava ve düşen yağış, birkaç yüz metrelik bir katman. " +
             "Bulut katmanı onun üstünde ve kilometrelerce geniş — sisi kapanan bir vadi, " +
             "bulut denizinin ne kadarını gördüğünü belirlemez. Tabansız bırakılınca " +
             "fırtınada menzil dört kilometreye, bulutun içindeyken üç yüz metreye " +
             "düşüyor ve yanı başındaki bulut çizilmiyordu.")]
    public float minHazeDistance = 16000f;

    [Header("Bulut aydınlatma")]
    [Tooltip("Güneşe bakan kenarlardaki gümüş parlama. Yüksek değer beyaz kontur yapar.")]
    [Range(0f, 1f)] public float rimStrength = 0.08f;
    [Tooltip("Beer's-Powder etkisinin gücü: ışığa BAKAN kenarların koyulaşması. " +
             "Gerçek bulutlarda yüzeye yakın noktaya çevreden saçılan ışık az gelir; " +
             "bu terim olmadan bulutlar yıkanmış beyaz görünür. Yalnız güneş arkadayken " +
             "okunur — güneşe bakarken gümüş kenar hâkimdir.")]
    [Range(0f, 1f)] public float powderStrength = 0.75f;
    [Tooltip("Gökyüzünden gelen dağınık ışığın şiddeti. Bulutun genel aydınlığı.")]
    [Range(0f, 2f)] public float cloudAmbient = 0.75f;
    [Tooltip("Bulut altının en düşük aydınlığı. Yükseldikçe altlar aydınlanır ama hacim " +
             "hissi ışık-gölge farkından doğduğu için form da düzleşir: 0.35'te bulutlar " +
             "havada uçan beyaz levhalara dönüyordu.")]
    [Range(0f, 1f)] public float ambientFloor = 0.15f;
    [Tooltip("Kütleden kütleye renk sıcaklığı farkı.")]
    [Range(0f, 0.6f)] public float massWarmth = 0.35f;
    [Tooltip("Kütleden kütleye parlaklık farkı.")]
    [Range(0f, 0.8f)] public float massBrightness = 0.35f;
    [Tooltip("Işık sondasının menzili (metre). Kısa = ışık derine işler, uzun = gövde " +
             "kararır. Katman kalınlığından bağımsız: gölgeyi belirleyen bulutun kendi " +
             "kalınlığıdır (~1-2 km), katmanın toplam yüksekliği değil.")]
    [Range(200f, 3000f)] public float lightProbeMeters = 1200f;
    [Tooltip("Çoklu saçılma gücü. 0 = tek saçılma, gövde simsiyah kalır. Yüksek değer " +
             "ışığı derine taşır ama kontrastı da yıkar; hacmi görünür kılan şey o kontrast.")]
    [Range(0f, 1f)] public float multiScatter = 0.6f;
    [Tooltip("Yağışta ışık soğurmasının artışı. Yağmur bulutu gözle görülür şekilde " +
             "kararır: fırtına ağırlaşır, kütlenin altı kurşuni olur. 0 = yağış rengi " +
             "etkilemez.")]
    [Range(0f, 3f)] public float rainAbsorption = 1.6f;
    [Tooltip("Şafak ve batımda buluta binen sıcak tonun gücü. Rengin kendisi TimeOfDay'den gelir.")]
    [Range(0f, 1f)] public float duskCloudStrength = 0.6f;

    [Header("Yükseklik sisi")]
    [Tooltip("Açık havada yoğunluğun yarıya indiği yükseklik farkı (metre). Katmanın " +
             "SIĞLIĞI, yatay ve dikey pusu birbirinden ayıran vidadır: yatay ışın gözün " +
             "kotunda kalıp katmanın içinde kilometrelerce ilerler (uzak sırt solar), " +
             "buluta giden ışın onu birkaç yüz metrede terk eder (bulut denizi silinmez).")]
    public float fogHalfHeightClear = 400f;
    [Tooltip("Sağanaktaki derinlik (metre). Yağışta sütun dikey karışır ve yağmur tepeden " +
             "dibe doldurur; sığ bırakmak 1000 m kotta sağanakta 5 km görüş veriyordu.")]
    public float fogHalfHeightStorm = 2000f;
    [Tooltip("Yoğunluğun ölçüldüğü kot (metre). Genellikle arazinin tabanı.")]
    public float fogBaseAltitude;
    [Tooltip("Görüş mesafesindeki sönüm bütçesi. 3.9 = Koschmieder: 'görüş X metre' " +
             "dendiğinde X metredeki cisim gerçekten kaybolur (kontrast %2). Küçük değer " +
             "sisi olduğundan seyrek gösterir — HUD 320 derken 800'e kadar seçiliyordu.")]
    [Range(0.5f, 6f)] public float fogThickness = 3.9f;
    [Tooltip("İnversiyon tavanı (metre): soğuk hava vadide hapsolur, üstünde sıcak hava " +
             "durur ve ikisi karışmaz.")]
    public float inversionHeight = 1700f;
    [Tooltip("Kesimin yumuşaklığı (metre). Dar tutulunca sınır keskin bir yüzey gibi durur.")]
    public float inversionWidth = 220f;
    [Tooltip("Serbest troposferin görüşü (metre). İnversiyonun üstünde kalan hava — " +
             "havanın kendi molekülleri. Yağıştan bağımsız: hava olayları sınır " +
             "tabakasında yaşar. Fiziksel değer ~290 km (Rayleigh, β_yeşil 13.6e-6 → " +
             "3.9/β). Kısmak yüksek kottaki hava perspektifini güçlendirir ama bulut " +
             "peçesini de kapatır; ikisi aynı yolu paylaşıyor.")]
    public float freeAirVisibility = 290000f;
    [Tooltip("Serbest katmanın yarı yüksekliği (metre). Rayleigh ölçek yüksekliği " +
             "8000 m × ln2. Sınır tabakasınınkinden kat kat yayvan — ayrımı bu yapar.")]
    public float freeAirHalfHeight = 5545f;
    [Tooltip("Yağış tavanı yükseltir: fırtınada nem yukarı taşınır, sis kuşağı kalınlaşır.")]
    public float inversionStormRise = 900f;
    [Tooltip("Güneş yükseldikçe sisin dağılma hızı.")]
    [Range(0.1f, 1f)] public float valleyFogBurnOff = 0.45f;
    [Tooltip("Şafak sis denizinin taban kotundaki görüşü (metre). Gece yoğuşan nem vadiyi " +
             "havadan bağımsız doldurur; güneş yükselince dağılır, banklar yerel deler.")]
    // 600 → 2200 m. 600 m vadi dibinde DUVAR gibi sisti: 186 m'de bile görüş 1.8 km'de
    // kalıyor ve 1.7 km'deki bulut katmanı 3 km'lik yoldan geçemeyip tamamen siliniyordu.
    // 2200 m pus seviyesi: vadi dolu görünür, yamaçtan bakan bulutları da görür.
    public float dawnSeaVisibility = 2200f;

    [Tooltip("Şafak sis denizinin yarı yükseklik ölçeği (metre). Genel sisinkinden " +
             "çok küçüktür: ışınımsal vadi sisi 30-300 m derinliğinde SIĞ bir katmandır, " +
             "üstünden bakınca sis denizi görürsün. Genel ölçek (1400 m) kullanılınca " +
             "deniz kilometrelerce yukarı uzanıyor ve vadinin 200 m üstündeki oyuncu da " +
             "600 m görüşe hapsoluyordu.")]
    public float dawnSeaHalfHeight = 120f;
    [Tooltip("Sis banklarının açık havadaki gücü. Sıfırda sis üniform bir çorba olur; " +
             "banklar gezmez, yamaç sarılıp açılmaz.")]
    [Range(0f, 1f)] public float fogBankClear = 0.35f;
    [Tooltip("Bankların yağıştaki gücü: fırtına sisi daha yamalı sarar.")]
    [Range(0f, 1f)] public float fogBankStorm = 0.75f;
    [Tooltip("Görüşün dakikalar ölçeğindeki nefes payı: aynı fırtınada sis epizotlar " +
             "hâlinde kalınlaşıp seyrelir, hiçbir an sabit durmaz.")]
    [Range(0f, 0.5f)] public float visibilityBreathing = 0.2f;

    [Header("Sürüklenen kar")]
    [Tooltip("Rüzgârın yerdeki gevşek karı kaldırdığı perdenin yoğunluğu (1/metre). " +
             "Tam etkide 0.004 ≈ bir kilometre görüş.")]
    public float spindriftDensity = 0.004f;
    [Tooltip("Kaldırmanın başladığı rüzgâr şiddeti. Gerçekte kar yaklaşık 4.5 m/s'de " +
             "sürüklenmeye başlar; şiddet 0-1 ölçeğinde bunun karşılığı ~0.22.")]
    [Range(0f, 1f)] public float spindriftWindThreshold = 0.22f;
    [Tooltip("Eşikten tam etkiye geçiş genişliği. Dar tutulunca hamle atakları yalnız " +
             "eşiğin hemen üstünde yaşıyor: rüzgâr biraz artınca bant doyuyor ve perde " +
             "sürekliye dönüyor. Geniş bant atakları yüksek rüzgârda da sürdürüyor.")]
    [Range(0.02f, 0.8f)] public float spindriftWindBand = 0.45f;
    [Tooltip("Perdenin parlaklığı, ufuk göğünün luminansı çarpanı. 1 bırakılırsa perde " +
             "karın kendisinden koyu kalıyor ve parlak beyazın üstünde mavi-gri bir " +
             "film gibi okunuyor. Savrulan kar aynı kardır, zeminden koyu olamaz.")]
    [Range(0.5f, 5f)] public float spindriftBrightness = 2.2f;
    [Tooltip("Perdenin optik derinlik tavanı, eşik civarındaki hafif rüzgârda. " +
             "0.5 ≈ uzaktaki yüzey renginin %60'ını korur. Tavan olmadan ışın yamaca " +
             "paralel gidince kilometrelerce yol alıp doyuma gidiyor ve uzak yamaç " +
             "her rüzgârda bembeyaz kesiliyordu.")]
    [Range(0.2f, 3f)] public float spindriftMaxDepthCalm = 0.5f;
    [Tooltip("Optik derinlik tavanı tam fırtınada. Gerçek ground blizzard'da uzak arazi " +
             "TAMAMEN kaybolur (görüş 400 metrenin altı); 3.0 ≈ %5 görünürlük. Sabit " +
             "tavanla hafif rüzgâr ile fırtına uzakta aynı görünüyordu — mesafe " +
             "şiddeti anlatamıyordu.")]
    [Range(0.5f, 8f)] public float spindriftMaxDepthStorm = 3f;
    [Tooltip("Sırt kretinde kaldırmanın kaç katına çıktığı. Spindrift yamacın " +
             "tamamından değil KRETTEN fışkırır: rüzgâr tepeyi aşarken hızlanır ve " +
             "gevşek karı havaya fırlatır.")]
    [Range(1f, 6f)] public float spindriftCrestBoost = 3f;
    [Tooltip("Kretin üstünde katmanın kaç katına kalınlaştığı. Tüy sırttan yukarı " +
             "fışkırıp rüzgâr altına dökülür; sabit kalınlıkla perde yamaca yapışık " +
             "kalıyor ve fışkırma hiç görünmüyordu.")]
    [Range(1f, 8f)] public float spindriftCrestRise = 4f;
    [Tooltip("Perdenin yarı-yüksekliği dingin uçta (metre). Yerden ölçülür.")]
    public float spindriftHeightCalm = 10f;
    [Tooltip("Perdenin yarı-yüksekliği fırtınada (metre). Ölçüm: saltasyon ilk on " +
             "santimde biter, süspansiyon taşınım hesaplarında 5 metreyle sınırlanır, " +
             "ama konvektif koşullarda YÜZLERCE metreye çıkar. 45 metre o aralığın çok " +
             "altındaydı; fırtına ucu gerçeğe göre alçak kalıyordu.")]
    public float spindriftHeightStorm = 150f;

    [Header("Bulut kuşağı")]
    [Tooltip("Sisli kuşağın bulut tabanının ne kadar altından başladığı (metre).")]
    public float deckLeadMeters = 400f;
    [Tooltip("Kuşağın kalınlığı (metre).")]
    public float deckThickness = 900f;
    [Tooltip("Kuşağın içindeyken görüşün düştüğü mesafe (metre).")]
    public float deckVisibility = 60f;
    [Tooltip("Kuşak içinde bir bank aralandığında görüşün açıldığı mesafe (metre). " +
             "Bulutun içi tekdüze çorba değildir: yamaç bir görünür, bir kaybolur.")]
    public float deckOpenVisibility = 260f;
    [Tooltip("Kuşağın yoğunluğu havaya göre değişir; açık havada da bir miktar bulunur.")]
    [Range(0f, 1f)] public float deckClearAmount = 0.35f;

    [Header("Ortam ışığı ve gölge")]
    [Tooltip("Ortam ışığının sis rengine oranı. Yüksek = düz ve puslu aydınlatma.")]
    [Range(0f, 1.5f)] public float ambientStrength = 0.85f;
    [Tooltip("Açık havadaki gölge mesafesi (metre). Sis kapandıkça bu değer düşer.")]
    public float maxShadowDistance = 150f;
    [Tooltip("Gölge mesafesi görüşün bu oranı kadar olsun. Sisin içindeki gölge görünmez.")]
    [Range(0.3f, 1f)] public float shadowVisibilityRatio = 0.8f;
    [Tooltip("Değişimin yumuşaklığı (saniye).")]
    public float transitionSeconds = 3f;
}
