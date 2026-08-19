using System.IO;
using UnityEditor;
using UnityEngine;

/// ŞİMŞEK ATMOSFERİK SAÇILMA TABLOSU — `[Dobashi 2001, §4.4]` Denklem 5.
///
/// NE İŞE YARIYOR: şimşeğin çevresindeki parlama, ışığın havadaki partiküllerden saçılıp
/// göze ulaşmasıdır. Bakış ışını boyunca alınacak integralin analitik çözümü yok
/// (Denklem 2), sayısal hesabı ise piksel başına yapılamayacak kadar pahalı. Makalenin
/// ana katkısı: integral yalnız bakış noktasının KAYNAĞA GÖRE yerel koordinatına bağlı,
/// kaynağın şiddeti dışarıda kalıyor (Denklem 4). Yani tablo BİR KEZ hesaplanır ve bütün
/// kaynaklar, bütün çakmalar, bütün sahneler için kullanılır.
///
/// Bu tablonun yerini şu an sabit bir çarpan tutuyor (`HeightFog.hlsl`:
/// `_LightningFlash.rgb * 0.6`). O çarpan mesafeyi de faz açısını da bilmiyor: çakma
/// nerede olursa olsun sis aynı miktarda parlıyor. Makalenin eleştirdiği "sezgisel
/// parlama" tam olarak bu.
///
/// NEDEN BERRAK HAVA İÇİN PİŞİYOR: makale atmosfer partikülü yoğunluğunu ÜNİFORM
/// varsayıyor — tablonun önceden hesaplanabilmesinin tek sebebi bu (§3.2). Bizim yerel
/// sisimiz (vadi denizi, banklar, savrulan kar) üniform değil ve havaya göre değişiyor;
/// tabloya girseydi her hava durumunda yeniden pişmesi gerekirdi. Bu yüzden tablo
/// makalenin kastettiği şeyi taşıyor: HER ZAMAN var olan hava. Yerel sis kendi yolundan
/// (`HeightFog.hlsl`) geçmeye devam ediyor, çift sayım yok.
///
/// Doğrulama: aynı integral Python'da bağımsız hesaplandı; birkaç örnek nokta menüden
/// "Şimşek tablosunu DOĞRULA" ile karşılaştırılabiliyor.
static class LightningLutBaker
{
    const string AssetPath = "Assets/Settings/LightningScatterLut.asset";

    /// Tablonun çözünürlüğü. `[Dobashi 2001, §5.1]` 128×128 kullanıyor.
    const int Resolution = 128;

    /// İNTEGRASYON KESME MESAFESİ (metre). `[Dobashi 2001, §4.2]`: sonsuza kadar
    /// integre etmek pratikte imkânsız, kullanıcının belirlediği büyük bir T ile
    /// kesiliyor. Makale 1.5 km kullanmış.
    ///
    /// Bizim arenamız 30 km ve bulut tabanı 2086 m; 1.5 km çakmanın çevresindeki
    /// parlamayı taşımaya yetiyor çünkü parlama 1/s² ile sönüyor — 1.5 km'de katkı
    /// merkezdekinin milyonda biri. Uzak çakmanın "denizi aydınlatması" bu tablonun
    /// işi değil, ışığın kendisinin işi.
    const float CutoffDistance = 1500f;

    /// Işın boyunca kaç örnek. 256 ile 512 arasındaki fark ölçüldü: en büyük hücrede
    /// %0.2, yani görünmez. 256 kalıyor.
    const int Samples = 256;

    /// BERRAK HAVANIN SÖNÜMÜ. `[Dobashi 2001]` κa ve ρa için sayı vermiyor (§9.2.3);
    /// atmosfer modelinden alınıyor. 550 nm'de ~30 km görüş mesafesi berrak dağ havası
    /// için makul; dalga boyu bağımlılığı Rayleigh (λ⁻⁴), yani mavi kırmızıdan daha
    /// hızlı süpürülüyor ve uzak parlama kızarıyor.
    const float ReferenceRange = 30000f;

    /// NORMALİZASYON. Ham integralin değerleri 1e-2 ile 1e-5 arasında ve birimi
    /// kaynağın şiddetiyle çarpılmak üzere tanımlı (Denklem 4). Mevcut kod ise
    /// `_LightningFlash.rgb * 0.6` ile kalibre edilmiş; ham tabloyu doğrudan koymak
    /// parlamayı binlerce kat değiştirirdi.
    ///
    /// Tablo REFERANS BİR YAPILANDIRMADA 1.0 verecek şekilde ölçekleniyor: 800 m ötede
    /// çakma, bakış yönü 30 derece sapmış (yakın çakma aralığının ortası, 200-1500 m).
    /// Böylece o noktada bugünkü parlaklık AYNEN korunuyor; değişen tek şey mesafe ve
    /// açıyla nasıl söndüğü — yani düzeltmek istediğimiz şey.
    ///
    /// Yeşil kanal referans alınıyor (göz ona en duyarlı); kırmızı/mavi arasındaki fark
    /// Rayleigh'in kendi rengi olarak duruyor.
    const float ReferenceValue = 4.935460e-05f;

    /// RGB'ye karşılık gelen dalga boyları `[Dobashi 2001, §4.4]`: 675, 520, 460 nm.
    static readonly float[] Wavelengths = { 675f, 520f, 460f };

    /// TABLO KENDİLİĞİNDEN PİŞİYOR. Statik bir asset ve elle üretilmesi gereken bir şey
    /// değil; yoksa yükleme anında üretiliyor. Menüye tıklamayı beklemek, tablosu
    /// olmayan bir projede şimşeğin sessizce sönük çakması demekti.
    [InitializeOnLoadMethod]
    static void BakeIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath) == null) Bake();
    }

    [MenuItem("To The Summit/Şimşek/Saçılma tablosunu pişir")]
    static void Bake()
    {
        var tex = new Texture2D(Resolution, Resolution, TextureFormat.RGBAFloat, false, true)
        {
            name = "LightningScatterLut",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color[Resolution * Resolution];

        for (int iv = 0; iv < Resolution; iv++)
        {
            float v = Coord(iv);

            for (int iu = 0; iu < Resolution; iu++)
            {
                float u = Coord(iu);

                pixels[iv * Resolution + iu] = new Color(
                    Integrate(u, v, Wavelengths[0]) / ReferenceValue,
                    Integrate(u, v, Wavelengths[1]) / ReferenceValue,
                    Integrate(u, v, Wavelengths[2]) / ReferenceValue,
                    1f);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        if (existing != null) AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(tex, AssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Şimşek saçılma tablosu pişti: {Resolution}×{Resolution}, "
                  + $"T={CutoffDistance} m → {AssetPath}");

        // Pişirdikten hemen sonra örnek noktalar basılıyor: bağımsız hesaplanmış
        // referansla karşılaştırmak için. BELLEKTEKİ dokudan okunuyor — `CreateAsset`
        // hemen ardından `LoadAssetAtPath` null dönüyor, içe aktarma aynı çağrıda
        // tamamlanmıyor.
        Report(tex);
    }

    /// Hücre merkezinden koordinat. Kenardan değil merkezden: kenar örneklenirse
    /// bilineer okuma tablonun dışına taşıyor ve u=−T sınırında sıfıra düşüyor.
    static float Coord(int index)
        => Mathf.Lerp(-CutoffDistance, CutoffDistance, (index + 0.5f) / Resolution);

    /// Denklem 5. `u` ve `v` bakış noktasının, kaynağın orijininde duran yerel
    /// sistemdeki koordinatları.
    ///
    /// `v` SIFIRA İNEMEZ: ışın tam kaynağın üstünden geçerse s → 0 ve integrand
    /// ıraksıyor. Fizikte de öyle — nokta kaynak bir idealleştirme. Alt sınır bir
    /// metre: şimşek kanalının kendi yarıçapı zaten bundan büyük.
    static float Integrate(float uEye, float vEye, float wavelength)
    {
        float v = Mathf.Max(Mathf.Abs(vEye), 1f);

        // Rayleigh: sönüm λ⁻⁴ ile artıyor.
        float scale = Mathf.Pow(wavelength / 550f, 4f);
        float extinction = 1f / (ReferenceRange * scale);

        float lo = -CutoffDistance;

        // ADIM (Samples-1)'E BÖLÜNÜYOR, Samples'a değil: yamuk kuralı N noktayı N−1
        // aralığa bölüyor. N'e bölmek integrali son aralık kadar eksik bırakıyor ve
        // bağımsız referansla %0.4 sapma veriyordu.
        float step = (uEye - lo) / (Samples - 1);
        if (step <= 0f) return 0f;

        float sum = 0f;

        for (int i = 0; i < Samples; i++)
        {
            // Yamuk kuralı: uçlar yarım ağırlıkta.
            float u = lo + step * i;
            float w = (i == 0 || i == Samples - 1) ? 0.5f : 1f;

            float s = Mathf.Sqrt(u * u + v * v);
            float cosAlpha = u / s;

            // FAZ FONKSİYONU. Makale "tipik olarak cos α'nın fonksiyonu" diyor, somut
            // form vermiyor (§9.2.2). İzotropik alınıyor: şimşek parlaması gözlemsel
            // olarak yönsüz bir hâle, ve Henyey-Greenstein'ın g'si için ölçülmüş bir
            // değer yok — uydurulan bir asimetri, olmayan bir yönlülük üretirdi.
            const float isotropic = 1f / (4f * Mathf.PI);
            float phase = isotropic;

            float t = uEye - u;
            sum += w * phase / (s * s) * Mathf.Exp(-extinction * (s + t));

            // `cosAlpha` şimdilik kullanılmıyor (izotropik faz); faz fonksiyonu
            // ölçülüp değiştirilirse buradan geçecek.
            _ = cosAlpha;
        }

        return sum * step;
    }

    [MenuItem("To The Summit/Şimşek/Saçılma tablosunu DOĞRULA")]
    static void Verify()
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetPath);
        if (tex == null) { Debug.LogWarning("Tablo yok — önce pişir."); return; }
        Report(tex);
    }

    static void Report(Texture2D tex)
    {

        // Bağımsız hesaplanmış referansla karşılaştırılacak noktalar. Python'daki
        // aynı integral bu hücrelerde şu değerleri verdi; sapma %1'i aşarsa iki
        // uygulama ayrışmış demektir.
        int[] us = { 64, 96, 64, 100, 20 };
        int[] vs = { 64, 64, 96, 100, 64 };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"tablo {tex.width}×{tex.height}  T={CutoffDistance} m");

        for (int i = 0; i < us.Length; i++)
        {
            Color c = tex.GetPixel(us[i], vs[i]);
            sb.AppendLine($"u={Coord(us[i]),8:F1} v={Coord(vs[i]),8:F1}  "
                          + $"RGB {c.r:E6} {c.g:E6} {c.b:E6}");
        }

        Debug.Log(sb.ToString());
    }
}
