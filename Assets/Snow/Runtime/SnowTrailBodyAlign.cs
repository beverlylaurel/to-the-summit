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



    [Tooltip("Kar yüksekliğini okuduğumuz örnekleyici. Yoksa gövde sabit " +
             "yükseklikte kalır (taban davranışı).")]
    [SerializeField] SnowSampler surfaceSampler;

    // AYARLAR ASSET'TEN, SAHNEDEN DEĞİL.
    //
    // Çap, batma ve yumuşama sahne bileşeninin alanıydı. Sahne dosyası Unity
    // açıkken dışarıdan düzenlenince Unity bellekteki kopyayı okumaya devam
    // ediyor ve Play ESKİ değerlerle çalışıyor; iki turluk düzeltme ekrana
    // hiç ulaşmadı. Tek sahip asset olunca o tuzak kapanıyor.
    [Tooltip("İz gövdesinin ölçüleri buradan okunuyor.")]
    [SerializeField] SnowSettings settings;

    float surfaceSink;
    float heightSmoothTime;



    float yaw;
    Vector3 baseLocalPos;
    float radius;
    float smoothY;
    float smoothVel;
    bool smoothBaslatildi;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException(
                $"{nameof(SnowTrailBodyAlign)}: {nameof(settings)} atanmadı.");

        // ÖLÇÜ HER AÇILIŞTA ASSET'TEN YAZILIYOR. Sahnedeki değer ne olursa
        // olsun geçersiz; tek kaynak var.
        float cap = Mathf.Max(0.02f, settings.TrailBodyDiameter);
        transform.localScale = new Vector3(cap, transform.localScale.y, cap);

        surfaceSink = settings.TrailBodySink;
        heightSmoothTime = settings.TrailBodySmoothTime;

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

            // İZ ÖNCESİ SÜTUN DOĞRUDAN OKUNUYOR.
            //
            // `ss.Depth + ss.SinkDepth` kullanılıyordu; açılımı
            // `baseHeight + trail.g`, yani içinde SIRT var. Sırt `max` ile
            // birikiyor ve konumu hız yönüne göre kaydırılıyor: gövdenin
            // oturma yüksekliği onun peşinden 4 cm zıplıyor, iz derinliği
            // basamaklanıyordu.
            //
            // Belirti aralıklıydı çünkü örnek 30 karede bir tazelenen bir
            // asenkron geri okumadan geliyor; basamağın görünürlüğü o kadansla
            // yürüyüş hızının fazına bağlıydı. Kullanıcı üç kez "her zaman
            // olmuyor" diye bildirdi.
            //
            // `BaseHeight` yalnız yağış ve oturmayla değişiyor, yani saniyeler
            // ölçeğinde sabit; geri okumanın gecikmesi onu etkilemiyor.
            float izOncesiYuzey = ss.BaseHeight;
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

        // ADIM SAPMASI KALDIRILDI.
        //
        // Her adımda gövde ±8 mm yana kaydırılıyordu; amaç iki adımı birbirinin
        // tıpatıp aynısı olmaktan çıkarmaktı. Ama iz yalnız 30 cm geniş ve
        // sırt (`trail.g`) ile relief kenarı bu kaymayı büyütüyor: düz
        // yürürken bile ekranda ÖRGÜ gibi bir zigzag çıkıyordu.
        //
        // Ölçüldü: izin merkez çizgisi ±1.6 cm salınıyor — sapmanın tepeden
        // tepeye değeri. Yani zigzagın kaynağı fizik değil, bu süsleme.
        //
        // Tekrarı kırma işi `KRepose`'un duruş yüksekliği gürültüsünde: o
        // SÜREKLİ bir alan ve dalga boyu teksel ızgarasının sekiz katı, adım
        // frekansında ayrık bir sıçrama değil.

        transform.localPosition = p;
    }

}
