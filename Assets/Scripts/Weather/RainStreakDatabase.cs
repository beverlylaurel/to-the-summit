using System;
using UnityEngine;

/// GARG-NAYAR IZ VERİTABANI — `[Garg 2006]`, `rain-spec.md` §5.
///
/// Yağmur izinin görünümü ışık yönü, bakış yönü ve damlanın salınımının karmaşık bir
/// fonksiyonu; ray-tracing gerektiriyor. Makalenin çözümü: offline render et, sakla,
/// çalışma zamanında ara. Bu asset o aramanın veri tarafı.
///
/// ÜÇ AÇISAL EKSEN artı salınım varyantı:
///   `v` — ışığın DİKEY açısı, damlanın düşüş eksenine göre  (10 değer)
///   `h` — ışığın YATAY açısı                                 (9 değer)
///   `dcam` — kameranın dikliğinden sapması, `θ_v = 90° − dcam` (5 değer)
///   `osc` — salınım varyantı, damla başına rastgele            (10 değer)
///
/// `dcam` HER SEVİYEDE AYRI DİZİ. İzin boyu kamera açısıyla kısalıyor (ölçüldü,
/// `size16`: 525/494/405/272/108 — oranı `cos(dcam)`), çünkü bakış yönü damlanın
/// düşüş yönüne dik değilse iz ekranda kısalır. Hepsini tek diziye doldurmak %40 boş
/// piksel demekti.
///
/// İKİ AYDINLATMA. `point` yönlü kaynak (güneş), `ambient` kapalı gökyüzü. Makale
/// ikisini AYRI hesaplayıp topluyor (`§6.3.3`); ikisi farklı görünüyor — ambient izin
/// tamamını yumuşak dolduruyor, yönlü kaynak ince keskin bir filament bırakıyor.
[CreateAssetMenu(fileName = "RainStreakDatabase", menuName = "To The Summit/Yağmur İz Veritabanı")]
public class RainStreakDatabase : ScriptableObject
{
    /// Bir kamera açısı için tüm çözünürlük seviyeleri.
    [Serializable]
    public class CameraAngle
    {
        [Tooltip("Kameranın diklikten sapması (derece). θ_v = 90° − bu.")]
        public int Dcam;

        [Tooltip("Yönlü kaynak dizileri, `Sizes` ile aynı sırada. Dilim indeksi " +
                 "((v * 9) + h) * 10 + osc.")]
        public Texture2DArray[] Point;

        [Tooltip("Ambient dizileri, `Sizes` ile aynı sırada. Dilim indeksi osc.")]
        public Texture2DArray[] Ambient;

        [Tooltip("Varlık tablosu, 900 giriş. 0 olan (v,h,osc) veritabanında YOK — " +
                 "uç dikey açılarda iz dejenere olduğu için render edilmemiş. " +
                 "Interpolasyon o komşuyu atlayıp ağırlıkları yeniden normalize eder.")]
        public byte[] Present;
    }

    [Tooltip("İz genişlikleri (piksel), artan sırada. Damlanın ekrana düşen iz " +
             "genişliğinden hemen büyük olan seçilir (`§6.3`).")]
    public int[] Sizes;

    [Tooltip("Işığın dikey açı ekseni (derece).")]
    public int[] Vertical;

    [Tooltip("Işığın yatay açı ekseni (derece).")]
    public int[] Horizontal;

    [Tooltip("Kamera açısı başına diziler, `dcam` artan sırada.")]
    public CameraAngle[] Angles;

    /// Dilim indeksi — dizideki sıra pişiricide sabit, isim aranmıyor.
    public static int SliceIndex(int vIndex, int hIndex, int osc) =>
        (vIndex * 9 + hIndex) * 10 + osc;
}
