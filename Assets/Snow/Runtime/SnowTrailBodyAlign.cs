// ROL: kar izi gövdesini hareket yönüne hizalar, kar yüzeyine oturtur ve
// adım adım hafifçe oynatır.
// Çağıran: yok — gövdenin kendi bileşeni.

using UnityEngine;

/// İZ GÖVDESİ HAREKET YÖNÜNE BAKAR, OYUNCUYA DEĞİL.
///
/// Gövde oval (dar ve uzun). Oyuncunun rotasyonuna bağlı kalsaydı, oyuncu
/// çapraz yürürken gövde de çapraz duruyor ve iz yana çıkıyordu — kullanıcı
/// bunu iki kübik ayak proxy'si döneminde bildirdi. Hareket yönüne hizalanınca
/// oluk her zaman gidiş doğrultusunda uzuyor.
///
/// DURURKEN SON YÖN KORUNUYOR. Hız sıfıra inince hedef yön tanımsız olur;
/// serbest bırakılırsa gövde rastgele bir yöne sıçrar ve durduğu yerde izin
/// şekli değişir.
///
/// ADIM ADIM OYNAMA. Sabit bir gövde oluk boyunca aynı deseni basıyor ve
/// tekrar gözle yakalanıyor (kullanıcı bildirdi: "sürekli aynı deseni
/// üretiyor, çok yapay"). Gerçek yürüyüşte her adım biraz farklı yere, biraz
/// farklı derinliğe basar. Sapma adım sayacından türeyen bir hash'ten geliyor:
/// tekrar etmiyor ama kare kare de titremiyor — bir adım boyunca sabit.
///
/// SAPMA KÜÇÜK KALIR. Önce ±3 cm yanal / ±1.2 cm dikey kullanıldı; her adımda
/// gövde belirgin biçimde yana zıplayınca oluk kenarı DÜZENLİ çentikler
/// üretiyordu — kullanıcı bunu "dikdörtgen / testere dişi" olarak bildirdi.
/// Kenarın düzensizliği zaten `KDeform`'daki prosedürel kaydırmadan geliyor;
/// o sürekli bir alan, bu ise adım frekansında ayrık bir sıçrama. Sapmanın
/// işi yalnız iki adımı birbirinin tıpatıp aynısı olmaktan çıkarmak.
[DisallowMultipleComponent]
public class SnowTrailBodyAlign : MonoBehaviour
{
    [Tooltip("Hareket yönünü okuduğumuz gövde.")]
    [SerializeField] CharacterController body;

    [Tooltip("Altında hizalamanın kapandığı hız (m/s).")]
    [SerializeField] float minSpeed = 0.15f;

    [Tooltip("Yön değişiminin yumuşama hızı (derece/saniye).")]
    [SerializeField] float turnRate = 540f;

    [Tooltip("Adım başına yanal sapmanın genliği (m).")]
    [SerializeField] float lateralJitter = 0.008f;

    [Tooltip("Adım başına derinlik sapmasının genliği (m).")]
    [SerializeField] float depthJitter = 0.003f;

    [Tooltip("Adım fazını okuduğumuz ritim. Yoksa sapma uygulanmaz.")]
    [SerializeField] SnowStepRhythm rhythm;

    [Tooltip("Kar yüksekliğini okuduğumuz örnekleyici. Yoksa gövde sabit " +
             "yükseklikte kalır (taban davranışı).")]
    [SerializeField] SnowSampler surfaceSampler;

    [Tooltip("Kürenin kar YÜZEYİNE göre batması (m). Küre alt noktası yüzeyin " +
             "bu kadar altına iner; oluk derinliği buradan gelir.")]
    [SerializeField] float surfaceSink = 0.05f;

    [Tooltip("Gövde yüksekliğinin yumuşama süresi (s). Yakalama kare başına " +
             "bir damga basıyor; yükseklik kare kare sıçrarsa her damga farklı " +
             "derinlikte kalır ve iz satır satır dilimlenir.")]
    // YÜRÜRKEN GÖVDE ZIPLAMASIN.
    //
    // 0.09 s'de gövde `CharacterController`'ın adım salınımını izliyordu:
    // her adımda biraz batıp biraz çıkıyor, iz sürekli bir oluk yerine
    // ARALIKLI KAPSÜLLER dizisi oluyordu (kullanıcı bildirdi: "bazen böyle
    // saçma bir iz bırakıyor"). 0.25 s salınımı süzüyor, arazi eğimini
    // yakalayacak kadar da hızlı kalıyor.
    [SerializeField] float heightSmoothTime = 0.25f;

    float yaw;
    Vector3 baseLocalPos;
    float radius;
    int lastStep = -1;
    Vector2 stepOffset;
    float smoothY;
    float smoothVel;
    bool smoothBaslatildi;

    void OnEnable()
    {
        baseLocalPos = transform.localPosition;
        radius = transform.localScale.y * 0.5f;
        yaw = transform.eulerAngles.y;
        smoothBaslatildi = false;
        smoothVel = 0f;
    }

    void LateUpdate()
    {
        if (body != null)
        {
            Vector3 v = body.velocity;
            v.y = 0f;

            if (v.sqrMagnitude > minSpeed * minSpeed)
                yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg,
                                             turnRate * Time.deltaTime);
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        Vector3 p = baseLocalPos;

        // KÜRE KAR YÜZEYİNE OTURUYOR, TABANA DEĞİL.
        //
        // Gövde ayakta (kar sütununun tabanında) dururken küre 20 cm karın
        // tamamını deliyor; batma `enFazlaOyma` sınırına dayanıp geniş bir düz
        // taban bırakıyor — iz dikdörtgen görünüyor (ölçüldü: kesit 80 mm
        // plato). Küre yalnız yüzeyin `surfaceSink` kadar altına inince kar
        // içinde kalan kısım kürenin dar alt eğrisi oluyor ve oluk U kesitine
        // dönüyor.
        //
        // İZ-ÖNCESİ yükseklik `Depth + SinkDepth` kullanılıyor: `Depth` tek
        // başına oyulmuş yüzeydir, küre kendi izini okuyup her kare daha derine
        // iner (geri besleme). Toplam iz-öncesi kar kalınlığını verir ve sabit
        // kalır.
        //
        // AYAK HİZASI DENETLEYİCİDEN TÜRETİLİYOR, `baseLocalPos`'tan DEĞİL.
        // Bu bileşen her kare `localPosition` yazıyor; `OnEnable` bir sonraki
        // açılışta kendi çıktısını taban sanır ve ofset birikir (ölçüldü:
        // gövde kar yüzeyinin 15 cm üstüne çıktı). `center.y - height/2`
        // bu döngüye kapalı.
        if (surfaceSampler != null && body != null &&
            surfaceSampler.TrySampleSnow(transform.position, out SnowSample ss) && ss.Valid)
        {
            float ayakY = body.center.y - body.height * 0.5f;
            float izOncesiYuzey = ss.Depth + ss.SinkDepth;
            float hedefY = ayakY + izOncesiYuzey - surfaceSink + radius;

            // YÜKSEKLİK ZAMANDA YUMUŞATILIYOR.
            //
            // Yakalama kare başına BİR damga basıyor. Gövdenin yüksekliği iki
            // kaynaktan kare kare sıçrıyor: karakter denetleyicisinin zemine
            // oturma salınımı ve kar yüzeyi okumasının gürültüsü. Ölçüldü:
            // batma -5.6 ile -9.3 cm arasında zıplıyor. Her damga farklı
            // derinlikte kalınca iz sürekli bir oluk değil, ARDIŞIK DİLİMLER
            // yığını oluyor — kullanıcı bunu "satır satır iz" ve "dikdörtgen"
            // diye bildirdi, özellikle yön değiştirirken.
            //
            // Yumuşatma damgaları ortak bir yüksekliğe oturtuyor; oluk sürekli
            // çıkıyor. Süre kısa tutuluyor: uzun olursa gövde arazi eğimini
            // geç yakalar ve yokuşta karın içinde/üstünde kalır.
            if (!smoothBaslatildi) { smoothY = hedefY; smoothBaslatildi = true; }
            else smoothY = Mathf.SmoothDamp(smoothY, hedefY, ref smoothVel,
                                            heightSmoothTime, Mathf.Infinity, Time.deltaTime);

            p.y = smoothY;
        }

        if (rhythm != null)
        {
            // Adım değiştiği KARE'de yeni sapma seçiliyor; adım boyunca sabit
            // kalıyor ki gövde kare kare titremesin.
            int step = rhythm.StepCount;
            if (step != lastStep)
            {
                lastStep = step;
                stepOffset = new Vector2(Hash01(step * 2 + 0), Hash01(step * 2 + 1)) * 2f - Vector2.one;
            }

            p.x += stepOffset.x * lateralJitter;
            p.y += stepOffset.y * depthJitter;
        }

        transform.localPosition = p;
    }

    /// Tam sayıdan 0..1. `frac(sin(...))` büyük indekste tekrar ediyor;
    /// bu karıştırıcı 32 bit boyunca dağınık kalıyor.
    static float Hash01(int n)
    {
        uint x = (uint)n * 747796405u + 2891336453u;
        x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
        x = (x >> 22) ^ x;
        return (x & 0xFFFFFFu) / 16777215f;
    }
}
