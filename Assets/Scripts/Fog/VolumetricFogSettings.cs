using UnityEngine;

/// Froxel sis hacminin ayarları. Sayılar Wronski 2014'ten ve kâğıtta hesaptan geliyor;
/// gerekçeler `.claude/PRPs/plans/volumetric-fog.plan.md` → "Kâğıtta hesaplanan sayılar".
///
/// Yoğunluk BURADA DEĞİL: onun sahibi `AtmosphereController` ve `AtmosphereSettings`.
/// Havanın nerede ne kadar yoğun olduğu hava durumundan türüyor; bu asset yalnız hacmin
/// kendi geometrisini ve ışık tepkisini taşıyor. İkisi karışırsa sis iki kaynaktan
/// sürülür.
[CreateAssetMenu(menuName = "To The Summit/Volumetrik Sis", fileName = "VolumetricFogSettings")]
public class VolumetricFogSettings : ScriptableObject
{
    [Header("Hacim")]
    [Tooltip("Hacmin ekran eksenindeki çözünürlüğü. Wronski 160x90 kullanıyor ve maliyet " +
             "ekran çözünürlüğünden BAĞIMSIZ kalıyor.")]
    [SerializeField, Range(80, 320)] int width = 160;

    [SerializeField, Range(45, 180)] int height = 90;

    [Tooltip("Derinlik dilimi sayısı. Wronski 64 veya 128 kullanıyor (platforma bağlı).")]
    [SerializeField, Range(32, 128)] int sliceCount = 64;

    /// MENZİL 1000 m. Wronski'nin doğruladığı menzil 50–128 m; uzun menzil için
    /// "üstel dağılım veya kademeli yaklaşım" diyor ama kademeliyi TANIMLAMIYOR.
    /// Kademeli yerine tek hacim + analitik kuyruk seçildi (`DECISIONS.md`, karar 1).
    ///
    /// Üstel dağılım sayesinde menzil sekiz katına çıkarken yakın alan hassasiyeti
    /// düşmüyor: ilk 128 metreye 46 dilim düşüyor, Wronski'nin tüm hacmi 64 dilimle o
    /// mesafeye yaydığı yerde.
    [Tooltip("Hacmin başladığı görüş uzayı derinliği (metre).")]
    [SerializeField, Range(0.1f, 5f)] float nearDistance = 0.5f;

    [Tooltip("Hacmin bittiği derinlik (metre). Ötesini analitik kuyruk sürüyor.")]
    [SerializeField, Range(100f, 4000f)] float farDistance = 1000f;

    [Header("Işık tepkisi")]
    /// Sisin KENDİ anizotropisi. Gökyüzü paketinin `_AerosolAnisotropy`'sinden ayrı,
    /// çünkü farklı ortam: sis su damlacığı (ileri saçılım belirgin ama bulut kadar
    /// değil), gökyüzününki toz aerosolü.
    [Tooltip("Henyey-Greenstein anizotropisi. 0 izotropik, 1 tamamen ileri.")]
    [SerializeField, Range(0f, 0.95f)] float anisotropy = 0.6f;

    /// Ortam katkısı ortam probe'undan, yani gökyüzünden pişen tek durumdan geliyor.
    /// Atmosferin in-scattering'ini buraya ayrıca eklemek çift sayım olur — homojen
    /// atmosferin sahibi gökyüzü paketi (`DECISIONS.md`, karar 2).
    [Tooltip("Gölgeli sisin ortam ışığından aldığı pay.")]
    [SerializeField, Range(0f, 2f)] float ambientDimmer = 1f;

    [Tooltip("Ana ışığın sise katkısı.")]
    [SerializeField, Range(0f, 2f)] float lightDimmer = 1f;

    public int Width => width;
    public int Height => height;
    public int SliceCount => sliceCount;
    public float NearDistance => nearDistance;
    public float FarDistance => Mathf.Max(farDistance, nearDistance * 2f);
    public float Anisotropy => anisotropy;
    public float AmbientDimmer => ambientDimmer;
    public float LightDimmer => lightDimmer;
}
