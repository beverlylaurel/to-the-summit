using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// HEITZ-NEYRET stokastik döşeme ön işlemesi.
///
/// Sorun: dağ 17.5 km, doku metrelerce döşeniyor — birkaç metrelik desen on binden
/// fazla tekrar eder ve ızgara olarak okunur. Basit harmanlama tekrarı zayıflatır ama
/// kontrastı da düşürür: iki örnek ortalanınca varyans yarıya iner, doku bulanıklaşır.
///
/// Yöntem: dokuyu HİSTOGRAM DÖNÜŞÜMÜNDEN geçir. Her kanal, değerleri Gauss dağılımına
/// eşleyen bir sıralama dönüşümüyle yeniden yazılır. Gauss değişkenlerin ağırlıklı
/// toplamı yine Gauss olduğu için üç örnek harmanlanınca dağılım BOZULMAZ; sonra ters
/// LUT ile özgün histograma geri çevrilir. Ortalama alma yerine dağılım koruma.
///
/// Hangi dokuların pişeceği ELLE YAZILMIYOR: diskteki bütün `SurfaceMaterialSet`
/// asset'leri taranıyor. Yeni yüzey eklemek bu dosyaya dokunmayı gerektirmiyor.
public static class StochasticTextureBaker
{
    const int LutSize = 256;

    /// Üretim değişince artırılır; işaret dosyası eskiyse hepsi yeniden pişer.
    public const int Revision = 3;
    static string MarkerPath => TextureIngest.Folder + "/stochastic-rev.txt";

    /// Harita adı → normal harita mı (üç kanal) yoksa tek kanal mı.
    static readonly (string map, bool threeChannel)[] Maps =
    {
        ("Normal", true),
        ("Roughness", false),
        ("Height", false)
    };

    [MenuItem("To The Summit/Doku/Yüzey Dokularını Yeniden Pişir", false, 61)]
    static void Rebake()
    {
        if (File.Exists(MarkerPath)) File.Delete(MarkerPath);
        EnsureAll();
    }

    /// Eksik ya da eski çıktıları pişirir. Bir şey pişmişse true döner.
    public static bool EnsureAll()
    {
        var sets = TextureIngest.AllSets();
        if (sets.Length == 0) return false;

        bool current = File.Exists(MarkerPath)
                    && File.ReadAllText(MarkerPath).Trim() == Revision.ToString();

        var baked = new List<string>();

        foreach (var set in sets)
        {
            if (string.IsNullOrEmpty(set.assetPrefix)) continue;

            foreach (var (map, threeChannel) in Maps)
            {
                string source = $"{TextureIngest.Folder}/{set.assetPrefix}_{map}.png";
                string output = $"{set.assetPrefix}_{map}_T";

                if (!File.Exists(source)) continue;
                if (current && File.Exists($"{TextureIngest.Folder}/{output}.png")) continue;

                Bake(source, output, threeChannel);
                baked.Add(output);
            }
        }

        if (baked.Count == 0) return false;

        File.WriteAllText(MarkerPath, Revision.ToString());
        AssetDatabase.Refresh();

        foreach (string output in baked)
        {
            Configure($"{TextureIngest.Folder}/{output}.png", false);
            Configure($"{TextureIngest.Folder}/{output}_LUT.png", true);
        }

        foreach (var set in sets) TextureIngest.Resolve(set);
        AssetDatabase.SaveAssets();

        Debug.Log($"Stokastik döşeme pişti: {baked.Count} doku.");
        return true;
    }

    static void Bake(string path, string outputName, bool threeChannel)
    {
        // Okuma için geçici olarak sıkıştırmasız ve okunabilir yapılıyor: sıkıştırılmış
        // doku GetPixels'te bloklara yuvarlanır ve histogram bozulur.
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool wasReadable = importer.isReadable;
        var wasType = importer.textureType;
        var wasCompression = importer.textureCompression;

        importer.isReadable = true;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int size = texture.width;
        var pixels = texture.GetPixels();

        var transformed = new Color[pixels.Length];
        var lut = new Color[LutSize];

        // Kanal başına bağımsız dönüşüm: kanallar arası ilişki bozulur ama Heitz'in
        // makalesi de böyle yapıyor — görsel fark yok, matematik çok daha basit.
        int channels = threeChannel ? 3 : 1;

        for (int c = 0; c < channels; c++)
        {
            var values = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) values[i] = Channel(pixels[i], c);

            // Sıralama: her pikselin histogramdaki yeri (birikimli olasılık).
            var order = new int[values.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => values[a].CompareTo(values[b]));

            // İleri dönüşüm: birikimli olasılık → Gauss. 0.5 ortalamalı, 1/6 standart
            // sapmalı bir Gauss'a oturtuluyor ki 0-1 aralığına sığsın.
            var gauss = new float[values.Length];
            for (int rank = 0; rank < order.Length; rank++)
            {
                float u = (rank + 0.5f) / order.Length;
                gauss[order[rank]] = Mathf.Clamp01(InverseGauss(u) / 6f + 0.5f);
            }

            for (int i = 0; i < pixels.Length; i++)
                SetChannel(ref transformed[i], c, gauss[i]);

            // TERS LUT: Gauss değerinden özgün değere. Shader harmanladığı Gauss
            // örneğini buradan geri çeviriyor.
            for (int i = 0; i < LutSize; i++)
            {
                float g = (i + 0.5f) / LutSize;
                float u = GaussCdf((g - 0.5f) * 6f);
                int rank = Mathf.Clamp(Mathf.RoundToInt(u * (order.Length - 1)), 0, order.Length - 1);
                SetChannel(ref lut[i], c, values[order[rank]]);
            }
        }

        for (int i = 0; i < transformed.Length; i++) transformed[i].a = 1f;
        for (int i = 0; i < lut.Length; i++) lut[i].a = 1f;

        WritePng($"{TextureIngest.Folder}/{outputName}.png", transformed, size, size);
        WritePng($"{TextureIngest.Folder}/{outputName}_LUT.png", lut, LutSize, 1);

        importer.isReadable = wasReadable;
        importer.textureType = wasType;
        importer.textureCompression = wasCompression;
        importer.SaveAndReimport();
    }

    static float Channel(Color c, int index) => index switch
    {
        0 => c.r,
        1 => c.g,
        _ => c.b
    };

    static void SetChannel(ref Color c, int index, float value)
    {
        switch (index)
        {
            case 0: c.r = value; break;
            case 1: c.g = value; break;
            default: c.b = value; break;
        }
    }

    /// Standart normal dağılımın ters birikimli fonksiyonu (Acklam yaklaşımı).
    /// Kapalı biçimde çözümü yok; bu yaklaşım 1e-9 hassasiyetinde.
    static float InverseGauss(double p)
    {
        const double a1 = -39.69683028665376, a2 = 220.9460984245205;
        const double a3 = -275.9285104469687, a4 = 138.3577518672690;
        const double a5 = -30.66479806614716, a6 = 2.506628277459239;
        const double b1 = -54.47609879822406, b2 = 161.5858368580409;
        const double b3 = -155.6989798598866, b4 = 66.80131188771972;
        const double b5 = -13.28068155288572;
        const double c1 = -0.007784894002430293, c2 = -0.3223964580411365;
        const double c3 = -2.400758277161838, c4 = -2.549732539343734;
        const double c5 = 4.374664141464968, c6 = 2.938163982698783;
        const double d1 = 0.007784695709041462, d2 = 0.3224671290700398;
        const double d3 = 2.445134137142996, d4 = 3.754408661907416;
        const double low = 0.02425, high = 1 - low;

        double q, r;

        if (p < low)
        {
            q = Math.Sqrt(-2 * Math.Log(p));
            return (float)((((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6)
                         / ((((d1 * q + d2) * q + d3) * q + d4) * q + 1));
        }

        if (p > high)
        {
            q = Math.Sqrt(-2 * Math.Log(1 - p));
            return (float)(-(((((c1 * q + c2) * q + c3) * q + c4) * q + c5) * q + c6)
                          / ((((d1 * q + d2) * q + d3) * q + d4) * q + 1));
        }

        q = p - 0.5;
        r = q * q;
        return (float)((((((a1 * r + a2) * r + a3) * r + a4) * r + a5) * r + a6) * q
                     / (((((b1 * r + b2) * r + b3) * r + b4) * r + b5) * r + 1));
    }

    /// Standart normal birikimli dağılım — hata fonksiyonunun yaklaşımı.
    static float GaussCdf(double x)
    {
        double t = 1.0 / (1.0 + 0.2316419 * Math.Abs(x));
        double d = 0.3989423 * Math.Exp(-x * x / 2);
        double p = d * t * (0.3193815 + t * (-0.3565638 + t * (1.781478
                 + t * (-1.821256 + t * 1.330274))));
        return (float)(x > 0 ? 1 - p : p);
    }

    static void WritePng(string path, Color[] pixels, int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    static void Configure(string path, bool isLut)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;

        // Dönüştürülmüş doku ve LUT ikisi de VERİ: normal harita olarak işaretlenirse
        // Unity kanalları yeniden paketler ve dönüşüm bozulur.
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = !isLut;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = isLut ? 0 : 8;

        // LUT kenardan kenara okunuyor: sarma tersini getirir.
        importer.wrapMode = isLut ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

        // Sıkıştırma histogramı bozar — dönüşümün tamamı değer hassasiyetine dayalı.
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = isLut ? 256 : 1024;
        importer.SaveAndReimport();
    }
}
