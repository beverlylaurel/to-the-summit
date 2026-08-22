// ROL: uzak yağış katmanının döşenebilir kar tanesi dokusunu üretir (spec §17.2).
// Çağıran: menü (doku değişince tekrar koşturulur).

using System.IO;
using UnityEditor;
using UnityEngine;

/// DÖŞENEBİLİR KAR TANESİ GÜRÜLTÜSÜ (spec §17.2).
///
/// Spec dokuyu tarif ediyor ama üretmiyor: "kar tanesi gürültüsü, alpha
/// kanallı, tileable". Elle çizilmiş bir doku dosyası repoda tutulacaksa
/// nereden geldiği kaybolur; burada üretiliyor, sayılar okunabilir kalıyor.
///
/// DÖŞENEBİLİRLİK SARMALAMAYLA sağlanıyor: her tane dokunun dört kenarından
/// da taşacak şekilde çiziliyor. Kenarda kesilen tane, karşı kenarda devam
/// ediyor — dikiş görünmüyor.
public static class SnowCurtainTextureBuilder
{
    const int Size = 512;
    const string Path = "Assets/Snow/Textures/T_SnowfallCurtain.png";

    /// Tane sayısı. Kaplama oranı yaklaşık `FlakeCount * pi * r_ort^2 / 512^2`.
    ///
    /// İlk deneme 900 taneydi: %8 kaplama, perdenin ekrana katkısı 1.05/255
    /// (ölçüldü — perde açık/kapalı iki kare farkı). Uzaktaki karın yerine
    /// geçmesi gereken katman görünmüyordu.
    ///
    /// 2400 tane ~%22 kaplama veriyor. Alpha'ya DOKUNULMADI: spec §17.2'nin
    /// 0.10/0.07/0.05'i yerinde: tülü kalınlaştırmak yerine tane sayısını
    /// artırmak doğru, çünkü perdenin temsil ettiği şey uzaktaki TANELER.
    const int FlakeCount = 2400;

    /// Yarıçap piksel. Uçlar 1.5–4: doku ekranda ~2 kat büyütülerek
    /// döşendiği için tane 3–8 piksel arası çıkıyor.
    const float RadiusMin = 1.5f;
    const float RadiusMax = 4f;

    [MenuItem("To The Summit/Kar/Uzak Perde Dokusunu Üret", false, 62)]
    static void Build()
    {
        var pixels = new Color32[Size * Size];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 0);

        // TOHUM SABİT. Her üretimde aynı doku çıkıyor; doku değiştiğinde
        // sebebi kodun değişmesi olsun, rastgeleliğin değil.
        var rng = new System.Random(20260822);

        for (int f = 0; f < FlakeCount; f++)
        {
            float cx = (float)rng.NextDouble() * Size;
            float cy = (float)rng.NextDouble() * Size;
            float r = Mathf.Lerp(RadiusMin, RadiusMax, (float)rng.NextDouble());

            // Parlaklık uçları: aynı boyda tanelerin hepsi aynı yoğunlukta
            // olursa katman düz bir tül gibi görünüyor.
            float peak = Mathf.Lerp(0.45f, 1f, (float)rng.NextDouble());

            Stamp(pixels, cx, cy, r, peak);
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true);
        tex.SetPixels32(pixels);
        tex.Apply();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.sRGBTexture = false;
        importer.SaveAndReimport();

        Debug.Log("Uzak perde dokusu üretildi: " + Path +
                  "  " + Size + "²  " + FlakeCount + " tane");
    }

    /// SARMALAYARAK BASIYOR. Merkez kenara yakınsa tane karşı kenardan
    /// devam ediyor; döşemede dikiş oluşmuyor.
    static void Stamp(Color32[] pixels, float cx, float cy, float r, float peak)
    {
        int span = Mathf.CeilToInt(r) + 1;
        int x0 = Mathf.FloorToInt(cx);
        int y0 = Mathf.FloorToInt(cy);

        for (int dy = -span; dy <= span; dy++)
        for (int dx = -span; dx <= span; dx++)
        {
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > r) continue;

            // Kenar yumuşaması: sert daire ekranda pikselli görünüyor.
            float a = peak * (1f - Mathf.SmoothStep(r * 0.4f, r, d));
            if (a <= 0f) continue;

            int x = ((x0 + dx) % Size + Size) % Size;
            int y = ((y0 + dy) % Size + Size) % Size;

            int idx = y * Size + x;

            // Üst üste binen taneler toplanıyor, birbirini silmiyor.
            byte prev = pixels[idx].a;
            pixels[idx].a = (byte)Mathf.Min(255f, prev + a * 255f);
        }
    }
}
