using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// İZ VERİTABANININ KARE BAŞINA GEREKEN DİLİMİ — `[Garg 2006]`, `rain-spec.md` §6.3.1.
///
/// Veritabanı 4500 dilim (v 10 × h 9 × osc 10) × 5 kamera açısı. Hepsini GPU'ya koymak
/// gerekmiyor: sahnede TEK yönlü kaynak var (güneş), yani kare başına yalnız o yönün
/// dört açısal komşusu okunuyor.
///
///   4 komşu (v,h) × 5 dcam × 10 osc = 200 dilim
///
/// `size16`'da 3.4 MB. Güneşin açısal hücresi değişmedikçe kopyalama da yapılmıyor.
///
/// AÇILARIN NEYE GÖRE ÖLÇÜLDÜĞÜ — burası karıştırılırsa izler yanlış aydınlanır:
///
///   `v` — ışığın damlanın DÜŞÜŞ EKSENİNE göre yükseklik açısı. Kameradan bağımsız.
///         Ölçüldü: `θ_l = 90° − v` (v = ±90'da veritabanında tek `h` var, yani kutup;
///         `rain-spec.md` §11.2-8'in doğrulanmamış bıraktığı eşleme).
///   `h` — ışığın azimutu, damlanın x-ekseninden. O eksen KAMERANIN optik ekseninin
///         düşüş eksenine dik düzleme izdüşümü (`§2.1`), yani kamera dönünce `h` de
///         döner. Kare başına yeniden hesaplanmalı.
///   `dcam` — kameranın diklikten sapması, `θ_v = 90° − dcam`. Ekran boyunca damladan
///         damlaya değişiyor (her damlaya farklı yönden bakılıyor), o yüzden beş açının
///         hepsi çalışma kümesinde duruyor.
public class RainStreakWorkingSet : MonoBehaviour
{
    [Tooltip("Pişmiş iz veritabanı. `To The Summit/Yağmur/İz veritabanını kur` üretiyor.")]
    [SerializeField] RainStreakDatabase database;

    /// ÇÖZÜNÜRLÜK SEVİYESİ — `[Garg 2006, §5]`: "the resolution level with textures of
    /// widths just larger than the width of the projected rain streak".
    ///
    /// Seviyeler artan sırada: `size4` (4×132), `size8` (8×263), `size16` (16×525).
    /// Bir dönem hep en yükseği (`size16`) kullanılıyordu; ÖLÇÜM onun yanlış olduğunu
    /// gösterdi.
    ///
    /// Bizim izlerimiz ekranda 1.2 piksel geniş (`MinPixelWidth` tabanı) ve gerçek
    /// genişlikleri daha da ince: 1.4 mm'lik damla 1 metrede 1.4 px, 5 metrede 0.28 px.
    /// 4 pikseli aşan tek durum 4 mm'den iri damlanın 1 metreden yakın olması — kâğıtta
    /// hesaplandı, karede BİR İKİ tanecik. Yani makalenin kuralı bütün sahne için
    /// `size4` diyor.
    ///
    /// `size16` kullanmanın bedeli boştan büyük doku değil, ALT ÖRNEKLEME: 525 piksel
    /// yüksekliğindeki iz uzak damlada 9 piksele iniyor (58 kat) ve dizilerde mipmap
    /// YOK, yani donanım da düzeltemiyor. Makalenin dipnotu tam bunu söylüyor —
    /// "to avoid artifacts due to severe down-sampling when rendering streaks far from
    /// the camera". `size4`'te oran 14 kata iniyor.
    ///
    /// Bedeli yakın damlada: ekranda 228 piksel boyundaki iz 132 pikselden geliyor,
    /// yani 1.7 kat büyütme — hafif yumuşama. Etkilenen tanecik sayısı yüzde birin
    /// altında.
    ///
    /// Serileştirilmiyor: Inspector'a girince sahnedeki bileşen eski değerle donar ve
    /// koddaki değişiklik etkisiz kalır.
    const int level = 0;

    /// Kare başına kopyalanan dilim düzeni. Sıra SABİT — shader indeksi buradan
    /// hesaplıyor, arama yapmıyor.
    ///   dilim = ((corner * 5) + dcamIndex) * 10 + osc
    const int Corners = 4;
    const int Osc = 10;

    Texture2DArray point, ambient;
    int cachedV = int.MinValue, cachedH = int.MinValue;

    /// Yönlü kaynak izleri, 200 dilim.
    public Texture2DArray Point => point;

    /// Ambient izler, 50 dilim (dcam × osc). Işık yönü yok, hücreye bağlı değil,
    /// bir kez kuruluyor.
    public Texture2DArray Ambient => ambient;

    /// `(v,h)` hücresinin köşe ağırlıkları — shader bilineer harmanlamada kullanacak.
    public Vector2 CellBlend { get; private set; }

    /// Dört köşe veritabanında var mı (1) yok mu (0). Sıra shader'ın beklediğiyle aynı:
    /// (vLow,hLow) (vLow,hHigh) (vHigh,hLow) (vHigh,hHigh).
    ///
    /// EKSİKLİK `osc`'DEN BAĞIMSIZ — ölçüldü: `dcam` başına 740 mevcut ve
    /// 8 v × 9 h × 10 + 2 v × 1 h × 10 = 740, yani eksik olan yalnız `v = ±90`
    /// kutuplarındaki `h ≠ 170` hücreleri ve orada on `osc`'nin hepsi birden yok.
    /// Bu yüzden köşe başına tek değer yetiyor, dilim başına tablo gerekmiyor.
    public Vector4 CornerPresent { get; private set; }

    /// Her `dcam` diliminin kaç satırı geçerli (0-1), `dcam` artan sırada. İz boyu
    /// kamera açısıyla kısalıyor ve çalışma kümesi en uzununa göre dolduruluyor;
    /// shader bu payın ötesini örneklememeli.
    ///
    /// `cos(dcam)` İLE HESAPLANMIYOR. Oran ona yakın (ölçüldü: 1.000/0.940/0.771/
    /// 0.517/0.206 karşı 1.000/0.940/0.766/0.500/0.174) ama 80°'de %18 sapıyor.
    /// Değer dokunun kendisinden okunuyor.
    public float[] DcamHeightFraction { get; private set; }

    int Level => Mathf.Min(level, database.Sizes.Length - 1);

    /// Sahne kurulumu koddan yapılıyor; veritabanı elle sürüklenmiyor.
    public void Bind(RainStreakDatabase streakDatabase) => database = streakDatabase;

    void OnEnable()
    {
        if (database == null)
            throw new MissingReferenceException(
                $"{name}: iz veritabanı bağlanmamış. Menüden kurulup Inspector'a verilmeli.");

        BuildAmbient();
    }

    void OnDisable()
    {
        if (point != null) Destroy(point);
        if (ambient != null) Destroy(ambient);
        point = ambient = null;
        cachedV = cachedH = int.MinValue;
    }

    /// Güneşin damla çerçevesindeki açısını hesaplar ve gerekiyorsa çalışma kümesini
    /// yeniler. `fallAxis` yağışın DÜNYA yönü (aşağı doğru), `viewAxis` kameranın optik
    /// ekseni.
    public void Refresh(Vector3 sunDirection, Vector3 fallAxis, Vector3 viewAxis)
    {
        // Damlanın çerçevesi: y ekseni düşüşün TERSİ (`§2.1`).
        Vector3 up = -fallAxis.normalized;

        // x ekseni: kameranın optik ekseninin y'ye dik düzleme izdüşümü. Kamera tam
        // yukarı ya da aşağı bakarken izdüşüm sıfıra gidiyor; o durumda azimut zaten
        // tanımsız, herhangi bir dik eksen aynı sonucu veriyor.
        Vector3 x = Vector3.ProjectOnPlane(viewAxis, up);
        x = x.sqrMagnitude > 1e-8f ? x.normalized : Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        Vector3 y = Vector3.Cross(up, x);

        Vector3 l = sunDirection.normalized;

        // `v` yatay düzlemden ölçülen yükseklik: +90 kutup (tepeden), −90 diğer kutup.
        float v = Mathf.Asin(Mathf.Clamp(Vector3.Dot(l, up), -1f, 1f)) * Mathf.Rad2Deg;

        // `h` azimut, x ekseninden. Veritabanı 10°–170° örnekliyor; 180°–360° aynalanmış
        // dokuyla karşılanıyor (`§5.2`), yani işaret shader'a ayrıca gidiyor.
        float h = Mathf.Atan2(Vector3.Dot(l, y), Vector3.Dot(l, x)) * Mathf.Rad2Deg;
        MirroredAzimuth = h < 0f;
        h = Mathf.Abs(h);

        int vLow = LowerIndex(database.Vertical, v, out float vT);
        int hLow = LowerIndex(database.Horizontal, h, out float hT);
        CellBlend = new Vector2(vT, hT);

        if (vLow == cachedV && hLow == cachedH) return;
        cachedV = vLow;
        cachedH = hLow;
        BuildPoint(vLow, hLow);
    }

    /// Azimut aynalandı mı — `§5.2`: 180° üstü için doku yatay çevriliyor.
    public bool MirroredAzimuth { get; private set; }

    /// Eksen üzerinde alt komşunun indeksi ve harmanlama payı. Eksen artan sıralı.
    static int LowerIndex(int[] axis, float value, out float t)
    {
        if (value <= axis[0]) { t = 0f; return 0; }
        if (value >= axis[^1]) { t = 0f; return axis.Length - 1; }

        int i = 0;
        while (i + 1 < axis.Length && axis[i + 1] <= value) i++;
        t = Mathf.InverseLerp(axis[i], axis[i + 1], value);
        return i;
    }

    void BuildAmbient()
    {
        var angles = database.Angles;
        var source = angles[0].Ambient[Level];

        ambient = new Texture2DArray(source.width, source.height, angles.Length * Osc,
                                     source.graphicsFormat, TextureCreationFlags.None)
        {
            name = "RainStreakAmbient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        for (int d = 0; d < angles.Length; d++)
        {
            var src = angles[d].Ambient[Level];
            for (int osc = 0; osc < Osc; osc++)
                Graphics.CopyTexture(src, osc, 0, 0, 0, src.width, src.height,
                                     ambient, d * Osc + osc, 0, 0, 0);
        }

        StoreHeightFractions();
    }

    void BuildPoint(int vLow, int hLow)
    {
        var angles = database.Angles;
        var tallest = angles[0].Point[Level];

        if (point == null)
        {
            point = new Texture2DArray(tallest.width, tallest.height,
                                       Corners * angles.Length * Osc,
                                       tallest.graphicsFormat, TextureCreationFlags.None)
            {
                name = "RainStreakWorkingSet",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        int vHigh = Mathf.Min(vLow + 1, database.Vertical.Length - 1);
        int hHigh = Mathf.Min(hLow + 1, database.Horizontal.Length - 1);
        var corners = new[] { (vLow, hLow), (vLow, hHigh), (vHigh, hLow), (vHigh, hHigh) };

        var present = Vector4.zero;
        for (int c = 0; c < Corners; c++)
        {
            var (cv, ch) = corners[c];
            present[c] = angles[0].Present[RainStreakDatabase.SliceIndex(cv, ch, 0)];
        }
        CornerPresent = present;

        for (int c = 0; c < Corners; c++)
        {
            var (vi, hi) = corners[c];
            for (int d = 0; d < angles.Length; d++)
            {
                var src = angles[d].Point[Level];
                for (int osc = 0; osc < Osc; osc++)
                {
                    int slice = RainStreakDatabase.SliceIndex(vi, hi, osc);

                    // EKSİK KOMBİNASYON: veritabanında yok (uç dikey açıda iz dejenere,
                    // `§5.4.5`). Kopyalanmıyor; dilim önceki karenin içeriğiyle kalmasın
                    // diye ağırlığı shader'a sıfır gidiyor.
                    if (angles[d].Present[slice] == 0) continue;

                    Graphics.CopyTexture(src, slice, 0, 0, 0, src.width, src.height,
                                         point, (c * angles.Length + d) * Osc + osc, 0, 0, 0);
                }
            }
        }
    }

    /// Çalışma kümesi en uzun dizinin boyunda; kısa `dcam`'ler üstte duruyor ve altı
    /// boş kalıyor. Shader o boşluğu örneklememeli.
    void StoreHeightFractions()
    {
        var angles = database.Angles;
        float tallest = angles[0].Point[Level].height;

        DcamHeightFraction = new float[angles.Length];
        for (int d = 0; d < angles.Length; d++)
            DcamHeightFraction[d] = angles[d].Point[Level].height / tallest;
    }
}
