// ROL: bu nesnenin karda iz bırakmasını sağlar. Kendini bir küre olarak tarif
// eder; iki kare arasındaki hareketi bir doğru parçası olarak yayınlar.
// Çağıran: SnowDeformerRegistry (kayıt), SnowManager (parça tamponu).

using UnityEngine;

/// İZ RASTERİZE EDİLMİYOR, HESAPLANIYOR.
///
/// Eski hâl nesnenin alt yüzeyini aşağıdan bakan ortografik bir yakalamaya
/// ÇİZİYORDU (Batman GDC 2014 yolu). Gerçek şekli bedavaya veriyordu ama
/// kenarı üç ayrı yerde teksel ızgarasına takıyordu: rasterin kendi kenarı,
/// Poisson blur'un dört tapı, kapsama payının eşiği. Ölçüldü: düz yürüyüşte
/// iz kenarı ±1.5 teksel dalgalanıyor, dalga boyu 5 teksel — yakında yumru,
/// uzakta testere dişi.
///
/// Yeni hâl: nesne bir KÜRE (merkez + yarıçap). `KDeform` tekselin bu kare
/// içinde süpürülen doğru parçasına yatay uzaklığını kapalı formülle bulup
/// oymayı o uzaklığın sürekli fonksiyonu olarak yazıyor. Izgara yok, raster
/// yok, blur yok, eşik yok.
///
/// YÜKSEKLİK YAYINLANMIYOR. Ne kadar batılacağını kar söylüyor (taşıma gücü,
/// yoğunluk, kabuk); nesne yalnız NEREDE olduğunu. Nesnenin Y'si okunsaydı ve
/// o Y karın durumundan türetilseydi döngü kapanırdı — bir kez kapandı
/// (`SYMPTOMS.md`: gövdenin yerel Y'si 10-30 mm salınıyor, iz genişliği 21
/// tekselden 13'e inip çıkıyor).
[ExecuteAlways]
[DisallowMultipleComponent]
public class SnowDeformer : MonoBehaviour
{
    [Tooltip("Karı ezen kürenin yarıçapı (m). Ayak için ~0.15.")]
    [SerializeField, Min(0.01f)] float radius = 0.15f;

    Vector3 prevPosition;

    /// Bu karenin süpürdüğü doğru parçasının başı (önceki kare konumu).
    public Vector3 SegmentA { get; private set; }

    /// Doğru parçasının sonu (bu kare konumu).
    public Vector3 SegmentB { get; private set; }

    public float Radius => radius;

    /// Yatay hız (m/s). Sırtın hareket yönünde asimetrik olmasını sağlıyor
    /// (spec §10.2).
    public Vector2 VelocityXZ { get; private set; }

    void OnEnable()
    {
        prevPosition = transform.position;
        SegmentA = prevPosition;
        SegmentB = prevPosition;
        VelocityXZ = Vector2.zero;

        SnowDeformerRegistry.Register(this);
    }

    void OnDisable() => SnowDeformerRegistry.Unregister(this);

    /// PARÇA SAKLANIYOR, TÜRETİLMİYOR.
    ///
    /// `SnowManager` parçayı çizim zamanında okuyor; o an bu bileşenin
    /// `LateUpdate`'i çalışmış da olabilir çalışmamış da. Parça burada
    /// saklandığı için okuma sırası sonucu değiştirmiyor: en kötü ihtimalle
    /// bir kare eski parça kullanılır ve parçalar uç uca eklendiği için izde
    /// boşluk oluşmaz.
    void LateUpdate()
    {
        Vector3 p = transform.position;

        SegmentA = prevPosition;
        SegmentB = p;

        Vector3 v = (p - prevPosition) / Mathf.Max(Time.deltaTime, 1e-4f);
        VelocityXZ = new Vector2(v.x, v.z);

        prevPosition = p;
    }
}
