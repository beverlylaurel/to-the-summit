using System.IO;
using UnityEditor;
using UnityEngine;

/// SPEKTRAL YAĞIŞ DOKUSU — `[Langer 2004]`, `snow-spec.md` §7.
///
/// NE İŞE YARIYOR: yoğun kar/yağmur particle sayısıyla ölçekleniyor ve kare süresi
/// duvara çarpıyor. Langer'ın gözlemi: yağan kar hem parçacık hem DOKU. Taneler ayrı ayrı
/// görünür ama asıl "kar duvarı" hissi, yüz binlerce tanenin oluşturduğu dinamik dokudan
/// gelir. Ölçülmüş (makale): 150 000 particle 121.9 ms/kare; seyrek particle + spektral
/// doku 24.6 ms, üstelik daha iyi görüntüyle.
///
/// ÜÇ BOYUT YERİNE BİR DÖNGÜ. Makale her ekran döşemesi için ayrı yön (θ) ve hız (C)
/// kullanıyor. İkisini de pişirmek gerekmiyor:
///
///   θ SAF DÖNMEDİR. Genlik spektrumu izotropik bir halka (|α̂| = 1/ω); dispersiyon
///   bağıntısındaki θ yalnızca frekans düzlemini döndürüyor. Döndürülmüş spektrumun ters
///   dönüşümü, dönmüş desendir. Yani tek desen pişer, çalışma zamanında UV döndürülür.
///
///   C SAF ZAMAN ÖLÇEĞİDİR. `ω_t = C·f(ω)` — C'yi ölçeklemek bütün zamansal frekansları
///   aynı oranda ölçekliyor. Bu, aynı döngüyü farklı hızda oynatmakla birebir aynı şey.
///
/// Sonuç: 64×64×30 tek kanal, 123 KB. Makalenin kare başına 400 IFFT'si sıfıra iniyor
/// (`snow-spec.md` §11.3.3 zaten bu takası öneriyor).
///
/// DÖNGÜ DİKİŞSİZ. Zamansal frekanslar döngü başına TAM SAYI çevrime yuvarlanıyor;
/// yuvarlanmazsa desen 30. karede sıçrıyor. Farklı hızda oynatmak dikişi bozmuyor çünkü
/// sinyal periyodik.
public static class SpectralPrecipitationBaker
{
    const string AssetPath = "Assets/Settings/SpectralPrecipitation.asset";

    /// Döşeme genişliği.
    ///
    /// MAKALE M = 64 KULLANIYOR ama 1024'lük görüntüde ~20 döşeme olarak, yani TEXEL
    /// BAŞINA BİR PİKSEL. Halka `[N/32, N/4]` döşeme birimi cinsinden; ekrandaki özellik
    /// boyu `4T/N` ile `32T/N` arası (T = döşemenin ekran boyu). Bağıntı `T = N` olmak
    /// zorunda, yoksa ölçek kayıyor.
    ///
    /// Bir dönem 64'lük doku 460 piksele yayılmıştı: özellikler 29-230 piksel çıktı ve
    /// ekranda kar değil MERMER DESENİ göründü (kullanıcı bildirdi). `T = N` ile
    /// özellikler 4-32 piksele iniyor — Langer'ın kendi aralığı.
    ///
    /// 512 seçildi çünkü tek pişmiş döşeme ekranda tekrar ediyor; büyük olan tekrar
    /// aralığını uzatıyor. Maliyeti 512×512×30 tek kanal = 7.9 MB.
    const int Size = 512;

    /// Döngü uzunluğu (kare). Makale 30 fps'te 30 kare = 1 saniye.
    const int Frames = 30;

    /// HALKA. Güç yalnız `[M/32, M/4]` bandındaki uzamsal frekanslara veriliyor
    /// `[Langer 2004, §5.3]` — ÜÇ OKTAV, yani sekiz kat hız aralığı. Altında desen tek
    /// bir lekeye, üstünde kum tanesine dönüyor; makale ikisini de denemiş.
    ///
    /// 512'lik dokuda 16-128 çevrim, ekranda 32-4 piksel dalga boyu — makalenin M=64
    /// döşemesindeki 2-16 çevrimle AYNI özellik boyu (`T = N` bağıntısı).
    ///
    /// YAĞMUR AYNI BANDI KULLANIYOR. Bir dönem bandı bir oktav yukarı kaydırmıştım
    /// ("damla taneden küçük"); makale öyle yapmıyor ve iki oktava düşüyordu.
    const float MinFreq = Size / 32f;   // 16 çevrim -> 32 piksel
    const float MaxFreq = Size / 4f;    // 128 çevrim -> 4 piksel

    /// TEMEL ZAMANSAL FREKANS (döngü başına çevrim). `ω_t ∈ [-C, C]`.
    ///
    /// YAĞMURUN İZİ BURADAN ÇIKIYOR, ayrı bir anizotropiden değil `[Langer 2004, §7]`:
    /// "We used vertical motion direction and a high value of C, such that the only
    /// spatial frequency components that contributed to the spectral sum were those in
    /// which |ωy| was near zero, that is, only long wavelengths in the y direction."
    ///
    /// Mekanizma: `ω_t = C·ωx/ω`, zamansal Nyquist kesmesi `|ω_t| > T/2` olanı
    /// sıfırlıyor. C büyürse yalnız `|ωx|/ω < T/(2C)` olan modlar kalıyor — hareket
    /// eksenine DİK modlar, yani hareket yönünde uzun dalga boyları. Sonuç: hareket
    /// yönü boyunca uzamış izler.
    ///
    /// Kar 6: Nyquist döngü/2 = 15, bol pay var, desen izotrop kalıyor.
    /// Yağmur 60: `|ωx|/ω < 0.25`, yani en boy oranı ~4:1 izler.
    const float SnowC = 6f;
    const float RainC = 60f;

    /// `[0,1]`'e eşlemede kaç standart sapma aralığa sığacak. 5 σ'da kırpılan pay
    /// ölçüldü: %1.7. Shader ortalamayı 0.5 kabul ediyor; bu sabit değişirse orası
    /// değil yalnız kontrast değişir.
    const float Spread = 5f;

    [InitializeOnLoadMethod]
    static void BakeIfMissing()
    {
        if (!File.Exists(AssetPath)) Bake();
    }

    /// YOKSA PİŞİRİR, VARSA YÜKLER. Bootstrap bunu çağırıyor.
    ///
    /// `InitializeOnLoadMethod` tek başına yetmiyordu: bootstrap `delayCall` üzerinden
    /// çalışıyor ve doku silinmişse ikisi YARIŞIYOR — bootstrap önce koşup "desen yok"
    /// diye patlıyordu. Varlığı isteyen taraf üretimi de tetiklemeli.
    public static Texture3D EnsureExists()
    {
        if (!File.Exists(AssetPath)) Bake();
        return AssetDatabase.LoadAssetAtPath<Texture3D>(AssetPath);
    }

    [MenuItem("To The Summit/Yağış/Spektral dokuyu pişir")]
    static void Bake()
    {
        int n = Size, frames = Frames;

        // İKİ KANAL, TEK DOKU. R kar, G yağmur; shader karlılıkla harmanlıyor. Ayrı doku
        // ikinci bir örnekleme demekti — perde tam ekran, örnek sayısı doğrudan kare
        // süresine yazılıyor (bir dönem sekiz örnek 3.5 ms tutmuştu).
        var snow = Synthesize(n, frames, MinFreq, MaxFreq, SnowC, 20260819, "kar");
        var rain = Synthesize(n, frames, MinFreq, MaxFreq, RainC, 20260820, "yagmur");

        var tex = new Texture3D(n, n, frames, TextureFormat.RG16, false)
        {
            name = "SpectralPrecipitation",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };

        // SetPixelData, SetPixels32 DEĞİL. `SetPixels32` Color32 aldığı için dokuyu
        // RGBA32'ye şişiriyordu — ölçüldü: asset 31.4 MB, yani texel başına 4 bayt.
        // RG16 iki bayt; bayt dizisini doğrudan yazmak formatı olduğu gibi bırakıyor.
        var data = new byte[n * n * frames * 2];
        for (int i = 0; i < snow.Length; i++)
        {
            data[i * 2] = snow[i];
            data[i * 2 + 1] = rain[i];
        }

        tex.SetPixelData(data, 0);
        tex.Apply(false, false);

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        if (AssetDatabase.LoadAssetAtPath<Texture3D>(AssetPath) != null)
            AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(tex, AssetPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Spektral yagis dokusu pisti: {n}x{n}x{frames} -> {AssetPath}");
        // FORMAT DENETİMİ. `SetPixels32` bir dönem dokuyu sessizce RGBA32'ye
        // şişiriyordu; texel başına bayt sayısı bunu tek bakışta gösteriyor.
        // Beklenen 2.00 (RG16). Diskteki .asset bunun iki katı görünür — o
        // serileştirmenin katlaması, VRAM değil.
        Debug.Log($"  format {tex.format} / {tex.graphicsFormat}, texel başına "
                  + $"{tex.GetPixelData<byte>(0).Length / (float)(n * n * frames):F2} bayt "
                  + "(beklenen 2.00)");
        VerifyLoop(snow, n, frames, "kar");
        VerifyLoop(rain, n, frames, "yagmur");
    }

    /// TEK KANAL SENTEZİ. Halka + `1/ω` genlik + dispersiyon bağıntısı + zamansal kesme,
    /// sonra `[0,1]`'e eşleme. Dönen dizi kare kare sıralı, piksel başına bir bayt.
    static byte[] Synthesize(int n, int frames, float minFreq, float maxFreq,
                             float baseC, int seed, string label)
    {
        // Rastgele faz alanı: karelerde SABİT. Kare kare değişseydi desen akmaz,
        // kaynardı — hareket zamansal frekanstan gelmeli, gürültüden değil.
        var rnd = new System.Random(seed);
        var ampRe = new float[n * n];
        var ampIm = new float[n * n];
        var omegaT = new float[n * n];

        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            // Merkezi sıfırda olan frekans koordinatı.
            float fx = x <= n / 2 ? x : x - n;
            float fy = y <= n / 2 ? y : y - n;
            float w = Mathf.Sqrt(fx * fx + fy * fy);

            int i = y * n + x;

            if (w < minFreq || w > maxFreq) continue;

            // 1/ω GENLİĞİ: her oktav bandına eşit güç, yani her görüntü hızı eşit
            // görünür `[Langer 2004, §6.1]`.
            float amp = 1f / w;

            float phase = (float)(rnd.NextDouble() * 2.0 * Mathf.PI);
            ampRe[i] = amp * Mathf.Cos(phase);
            ampIm[i] = amp * Mathf.Sin(phase);

            // DİSPERSİYON BAĞINTISI (Denklem 6), θ = 0 için:
            //   ω_t = C · ωx / √(ωx² + ωy²)
            // Yakındaki tane hem daha hızlı hem daha büyük görünür; ikisi birleşince
            // zamansal frekans uzamsal frekansın YÖNÜNE bağlı kalıyor, büyüklüğüne değil.
            float wt = baseC * fx / w;

            // TAM SAYIYA YUVARLANIYOR: döngünün dikişsiz olması için zamansal frekans
            // döngü başına tam çevrim olmalı.
            wt = Mathf.Round(wt);

            // ZAMANSAL BULANIKLIK. Nyquist'i aşan bileşenler örtüşüyor ve θ yönündeki
            // hız, θ+180° yönünde hız gibi görünüyor `[Langer 2004, §6.2]`. Genlikleri
            // sıfırlamak spektral yöntemin bedava motion blur'u.
            if (Mathf.Abs(wt) > frames / 2f) { ampRe[i] = 0f; ampIm[i] = 0f; }

            omegaT[i] = wt;
        }

        var slices = new float[frames][];
        double sum = 0.0, sumSq = 0.0;

        var re = new float[n * n];
        var im = new float[n * n];

        for (int t = 0; t < frames; t++)
        {
            float phaseStep = 2f * Mathf.PI * t / frames;

            for (int i = 0; i < n * n; i++)
            {
                // Zamanda ilerletme: e^(i·ω_t·2π·t/T) ile çarpım.
                float c = Mathf.Cos(omegaT[i] * phaseStep);
                float sn = Mathf.Sin(omegaT[i] * phaseStep);
                re[i] = ampRe[i] * c - ampIm[i] * sn;
                im[i] = ampRe[i] * sn + ampIm[i] * c;
            }

            Inverse2D(re, im, n);

            var slice = new float[n * n];
            for (int i = 0; i < n * n; i++)
            {
                slice[i] = re[i];
                sum += re[i];
                sumSq += (double)re[i] * re[i];
            }
            slices[t] = slice;
        }

        int total = frames * n * n;
        float mean = (float)(sum / total);
        float sd = Mathf.Sqrt(Mathf.Max((float)(sumSq / total - (double)mean * mean), 1e-12f));

        // [0,1]'E EŞLEME `[Langer 2004, §7.7]`: ORTALAMA TAM 0.5'E oturuyor, standart
        // sapma neredeyse her değer aralıkta kalacak kadar küçültülüyor, aykırılar
        // kırpılıyor.
        //
        // SONRA KARESİ ALINIYOR `[Langer 2004, §5.6]`: "we apply a non-linear
        // transformation, namely we square the α(x,y,t) values. This compresses the
        // opacity values to the lower part of the interval [0,1]. Thus, after the
        // compositing step, the variations in opacity are more visible."
        //
        // Bir dönem bunu silmiştim ve yerine shader'da ortalamayı çıkarıyordum. İkisi
        // AYNI işi yapmıyor: kare alma her yerde gradyan bırakıp tepeleri öne çıkarıyor,
        // ortalama çıkarma ekranın yarısını tam sıfıra kırpıp ikili maske üretiyor.
        // Makalenin çıktısı ayrık beyaz lekeler; kırpma onu gürültüye çeviriyordu.
        var outBytes = new byte[total];
        for (int t = 0; t < frames; t++)
        for (int i = 0; i < n * n; i++)
        {
            float v = Mathf.Clamp01((slices[t][i] - mean) / (Spread * sd) + 0.5f);
            v *= v;
            outBytes[t * n * n + i] = (byte)Mathf.RoundToInt(v * 255f);
        }

        Report(outBytes, label, mean, sd);
        return outBytes;
    }

    /// DÖNGÜ DENETİMİ. İki şey ölçülüyor:
    ///
    ///   HAREKET VAR MI — ardışık kareler arasındaki ortalama fark. Sıfıra yakınsa desen
    ///   duruyor demektir ve zamansal frekanslar yanlış kurulmuştur.
    ///
    ///   DİKİŞ VAR MI — son kareden ilk kareye geçişteki fark, ardışık kare farkıyla
    ///   AYNI büyüklükte olmalı. Belirgin büyükse döngü kapanmıyor ve saniyede bir
    ///   sıçrama görünür.
    static void VerifyLoop(byte[] pixels, int n, int frames, string label)
    {
        int plane = n * n;

        double consecutive = 0.0;
        for (int t = 0; t < frames - 1; t++)
        for (int i = 0; i < plane; i++)
            consecutive += Mathf.Abs(pixels[(t + 1) * plane + i] - pixels[t * plane + i]);
        consecutive /= (frames - 1) * plane;

        double wrap = 0.0;
        for (int i = 0; i < plane; i++)
            wrap += Mathf.Abs(pixels[i] - pixels[(frames - 1) * plane + i]);
        wrap /= plane;

        Debug.Log($"  {label} döngü: ardışık kare farkı {consecutive:F2}/255, "
                  + $"son→ilk {wrap:F2}/255, oran {wrap / Mathf.Max((float)consecutive, 1e-6f):F2} "
                  + "(1'e yakın = dikişsiz, 0'a yakın hareket = ölü desen)");
    }

    /// Ayrılabilir ters FFT: önce satırlar, sonra sütunlar. 64 nokta için radix-2 yeter.
    static void Inverse2D(float[] re, float[] im, int n)
    {
        var rowRe = new float[n];
        var rowIm = new float[n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++) { rowRe[x] = re[y * n + x]; rowIm[x] = im[y * n + x]; }
            Fft(rowRe, rowIm, true);
            for (int x = 0; x < n; x++) { re[y * n + x] = rowRe[x]; im[y * n + x] = rowIm[x]; }
        }

        for (int x = 0; x < n; x++)
        {
            for (int y = 0; y < n; y++) { rowRe[y] = re[y * n + x]; rowIm[y] = im[y * n + x]; }
            Fft(rowRe, rowIm, true);
            for (int y = 0; y < n; y++) { re[y * n + x] = rowRe[y]; im[y * n + x] = rowIm[y]; }
        }
    }

    /// Cooley-Tukey radix-2, yerinde. `inverse` işareti ve 1/N ölçeği değiştiriyor.
    static void Fft(float[] re, float[] im, bool inverse)
    {
        int n = re.Length;

        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (int len = 2; len <= n; len <<= 1)
        {
            float angle = 2f * Mathf.PI / len * (inverse ? 1f : -1f);
            float wRe = Mathf.Cos(angle), wIm = Mathf.Sin(angle);

            for (int i = 0; i < n; i += len)
            {
                float curRe = 1f, curIm = 0f;

                for (int k = 0; k < len / 2; k++)
                {
                    int a = i + k, b = i + k + len / 2;

                    float tRe = re[b] * curRe - im[b] * curIm;
                    float tIm = re[b] * curIm + im[b] * curRe;

                    re[b] = re[a] - tRe; im[b] = im[a] - tIm;
                    re[a] += tRe;        im[a] += tIm;

                    float nextRe = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe;
                    curRe = nextRe;
                }
            }
        }

        if (!inverse) return;
        for (int i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }
    }

    /// Pişirdikten sonra dağılımı basar. Perde "düz tül" gibi duruyorsa sebebi burada
    /// görünür: ortalama 0.5'ten uzaksa ya da varyans çok küçükse desen yok demektir.
    static void Report(byte[] pixels, string label, float mean, float sd)
    {
        double s = 0.0, s2 = 0.0;
        int zero = 0, full = 0;

        foreach (var p in pixels)
        {
            float v = p / 255f;
            s += v; s2 += v * v;
            if (p == 0) zero++;
            if (p == 255) full++;
        }

        int count = pixels.Length;
        float m = (float)(s / count);
        float d = Mathf.Sqrt(Mathf.Max((float)(s2 / count - (double)m * m), 0f));

        Debug.Log($"  {label}: ham ortalama {mean:E3}  ham sigma {sd:E3}\n"
                  + $"    opaklik ortalama {m:F3}  sigma {d:F3}  "
                  + $"sifir %{100f * zero / count:F1}  doygun %{100f * full / count:F2}");
    }
}
