// ROL: bu nesnenin karda iz bırakmasını sağlar. Kendini bir veya birkaç
// kapsül parçası olarak tarif eder.
// Çağıran: SnowDeformerRegistry (kayıt), SnowManager (parça tamponu).

using UnityEngine;

/// İZ RASTERİZE EDİLMİYOR, HESAPLANIYOR.
///
/// Eski hâl nesnenin alt yüzeyini aşağıdan bakan ortografik bir yakalamaya
/// ÇİZİYORDU (Batman GDC 2014 yolu). Gerçek şekli bedavaya veriyordu ama
/// kenarı üç ayrı yerde teksel ızgarasına takıyordu: rasterin kendi kenarı,
/// Poisson blur'un dört tapı, kapsama payının eşiği. Ölçüldü: düz yürüyüşte
/// iz kenarı ±1.5 teksel dalgalanıyor — yakında yumru, uzakta testere dişi.
///
/// Yeni hâl: nesne bir KAPSÜL (iki uç + yarıçap). `KDeform` tekselin kapsüle
/// yatay uzaklığını kapalı formülle bulup oymayı o uzaklığın sürekli
/// fonksiyonu olarak yazıyor. Izgara yok, raster yok, blur yok, eşik yok.
///
/// YÜKSEKLİK YAYINLANMIYOR. Ne kadar batılacağını kar söylüyor (taşıma gücü,
/// yoğunluk, kabuk); nesne yalnız NEREDE olduğunu ve yükü ne kadar
/// yoğunlaştırdığını. Nesnenin Y'si okunsaydı ve o Y karın durumundan
/// türetilseydi döngü kapanırdı — bir kez kapandı (`SYMPTOMS.md`).
///
/// AYRI AYRI AYAK İZLERİ DENENDİ VE GERİ ALINDI. Adım olayı yarım adımda bir
/// (39 cm) düşüyor ve iz o anda BİRDEN beliriyordu — kullanıcı bunu "Minecraft'ta
/// blok koyar gibi, gecikmeli" diye bildirdi. Üstüne sol/sağ ayrık damgalar
/// ekranda zigzag olarak okunuyordu. Düzensizlik ayrık damgadan değil, SÜREKLİ
/// bir alandan gelmeli.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SnowDeformer : MonoBehaviour
{
    [Tooltip("Karı ezen kapsülün yarıçapı (m).")]
    [SerializeField, Min(0.01f)] float radius = 0.15f;

    [Tooltip("Yol boyunca yarıçabın oynama payı (0 = sabit).")]
    [SerializeField, Range(0f, 0.5f)] float widthWobble = 0.16f;

    [Tooltip("Yol boyunca batmanın oynama payı (0 = sabit).")]
    [SerializeField, Range(0f, 0.5f)] float depthWobble = 0.22f;

    [Tooltip("Oynamanın dalga boyu (m). Adım uzunluğu mertebesinde olmalı.")]
    [SerializeField, Min(0.05f)] float wobbleLength = 0.55f;

    Vector3 prevPosition;
    Vector3 segmentA, segmentB;

    /// Kat edilen yatay yol (m). Düzensizlik ZAMANA değil YOLA bağlı: durunca
    /// iz oynamıyor, yavaş yürüyünce de aynı deseni veriyor.
    float travelled;

    public float Radius => radius;

    /// Yatay hız (m/s). Sırtın hareket yönünde asimetrik olmasını sağlıyor
    /// (spec §10.2).
    public Vector2 VelocityXZ { get; private set; }

    /// Bu karede kar dokusuna yazılacak parça sayısı.
    public virtual int SegmentCount => 1;

    /// `a.xyz` başlangıç, `a.w` yarıçap; `b.xyz` bitiş, `b.w` batma çarpanı.
    ///
    /// GENİŞLİK VE DERİNLİK YOL BOYUNCA DALGALANIYOR — SÜREKLİ, ANİ DEĞİL.
    ///
    /// Sabit yarıçap ve sabit batma boru gibi tek tip bir oluk veriyor
    /// (kullanıcı bildirdi: "gerçekte bir yönde ilerlerken iz bu kadar düzenli
    /// mi olur?"). Gerçek yürüyüşte her basış biraz farklı yere, biraz farklı
    /// derinliğe iniyor ve oluk boyunca genişleyip daralıyor.
    ///
    /// Modülasyon KAT EDİLEN YOLA bağlı ve SÜREKLİ. Ayrık damga denendi
    /// (adım başına bir iz) ve reddedildi: iz 39 cm'de bir birden beliriyordu.
    /// Sürekli bir dalga aynı düzensizliği veriyor ama izin ucu her karede
    /// bir tık daha uzuyor — belirme yok.
    public virtual void GetSegment(int index, out Vector4 a, out Vector4 b)
    {
        // İki uçta ayrı örnek: parçanın kendisi de boyunca daralıp genişliyor.
        float wA = Dalga(travelled - Vector3.Distance(segmentA, segmentB));
        float wB = Dalga(travelled);

        float rA = radius * (1f + wA * widthWobble);
        float derinlik = 1f + wB * depthWobble;

        a = new Vector4(segmentA.x, segmentA.y, segmentA.z, rA);
        b = new Vector4(segmentB.x, segmentB.y, segmentB.z, derinlik);
    }

    /// SİNÜS DEĞİL DEĞER GÜRÜLTÜSÜ, −1..1.
    ///
    /// Önce iki sinüsün toplamıydı ve tam PERİYODİKTİ: desen 55 cm'de bir
    /// birebir tekrar ediyordu (kullanıcı bildirdi: "çok düzenli desene sahip,
    /// hep aynı izi çıkarıyor"). İki farklı frekanslı sinüs "düzensiz" değil,
    /// yalnız daha uzun periyotlu.
    ///
    /// Değer gürültüsü hash'ten geliyor: tekrar etmiyor ama yola bağlı olduğu
    /// için tekrarlanabilir — aynı yol aynı izi verir, geri sarma bozulmaz.
    float Dalga(float s)
    {
        float u = s / Mathf.Max(0.05f, wobbleLength);

        return (Gurultu(u) * 0.62f
              + Gurultu(u * 2.17f + 11.3f) * 0.26f
              + Gurultu(u * 4.61f + 37.9f) * 0.12f) * 2f - 1f;
    }

    /// Bir boyutlu değer gürültüsü, 0..1. İki tam sayı hücresinin hash'i
    /// smoothstep'lenmiş kesirle harmanlanıyor.
    static float Gurultu(float u)
    {
        float h = Mathf.Floor(u);
        float f = u - h;
        f = f * f * (3f - 2f * f);

        return Mathf.Lerp(Hash((int)h), Hash((int)h + 1), f);
    }

    static float Hash(int n)
    {
        uint x = (uint)n * 747796405u + 2891336453u;
        x = ((x >> ((int)(x >> 28) + 4)) ^ x) * 277803737u;
        x = (x >> 22) ^ x;

        return x * (1f / 4294967296f);
    }

    protected virtual void OnEnable()
    {
        prevPosition = transform.position;
        segmentA = prevPosition;
        segmentB = prevPosition;
        VelocityXZ = Vector2.zero;
        travelled = 0f;

        SnowDeformerRegistry.Register(this);
    }

    protected virtual void OnDisable() => SnowDeformerRegistry.Unregister(this);

    /// PARÇA SAKLANIYOR, TÜRETİLMİYOR.
    ///
    /// `SnowManager` parçayı çizim zamanında okuyor; o an bu bileşenin
    /// `LateUpdate`'i çalışmış da olabilir çalışmamış da. Parça burada
    /// saklandığı için okuma sırası sonucu değiştirmiyor: en kötü ihtimalle
    /// bir kare eski parça kullanılır ve parçalar uç uca eklendiği için izde
    /// boşluk oluşmaz.
    protected virtual void LateUpdate()
    {
        Vector3 p = transform.position;

        segmentA = prevPosition;
        segmentB = p;

        Vector3 yatay = p - prevPosition;
        yatay.y = 0f;
        travelled += yatay.magnitude;

        Vector3 v = (p - prevPosition) / Mathf.Max(Time.deltaTime, 1e-4f);
        VelocityXZ = new Vector2(v.x, v.z);

        prevPosition = p;
    }
}
