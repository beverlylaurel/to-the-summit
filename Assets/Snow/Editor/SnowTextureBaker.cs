// ROL: kar sisteminin ihtiyaç duyduğu prosedürel dokuları üretir ve asset
// olarak yazar. Bir kez koşar; doku varsa dokunmaz.
// Çağıran: SnowDebugWindow (sahne kurulumu).

using System.IO;
using UnityEditor;
using UnityEngine;

/// DOKU ÜRETİLİYOR, İNDİRİLMİYOR. Spec §8.2 `_SnowBreakup` diye bir gürültü
/// istiyor ama dosya listesinde yok. Prosedürel üretmek hem tohumu kayda
/// geçiriyor hem de repoya ikili bir varlık eklemeden tekrarlanabilir kılıyor.
public static class SnowTextureBaker
{
    public const string BreakupPath = "Assets/Snow/Textures/T_Snow_Breakup.png";

    const int BreakupResolution = 256;

    /// Tohum sabit: aynı doku her makinede aynı çıksın.
    const int Seed = 20260822;

    public static Texture2D EnsureBreakup()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(BreakupPath);
        if (existing != null) return existing;

        string folder = Path.GetDirectoryName(BreakupPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Snow", "Textures");

        var tex = new Texture2D(BreakupResolution, BreakupResolution, TextureFormat.R8, false, true);
        var px = new Color32[BreakupResolution * BreakupResolution];

        for (int y = 0; y < BreakupResolution; y++)
        for (int x = 0; x < BreakupResolution; x++)
        {
            float n = TilingFbm(x / (float)BreakupResolution, y / (float)BreakupResolution);
            byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(n * 255f), 0, 255);
            px[y * BreakupResolution + x] = new Color32(v, v, v, 255);
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        File.WriteAllBytes(BreakupPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(BreakupPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(BreakupPath);
        importer.textureType = TextureImporterType.SingleChannel;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(BreakupPath);
    }

    /// DÖŞENEBİLİR gürültü. Kafes noktaları frekansa göre sarılıyor; sarılmazsa
    /// kar kenarında dokunun dikişi düz bir çizgi olarak görünür — tam da
    /// kırmaya çalıştığımız şey.
    static float TilingFbm(float u, float v)
    {
        float sum = 0f;
        float amplitude = 0.5f;
        int frequency = 4;

        for (int octave = 0; octave < 4; octave++)
        {
            sum += TilingValueNoise(u, v, frequency, octave) * amplitude;
            amplitude *= 0.5f;
            frequency *= 2;
        }

        return Mathf.Clamp01(sum);
    }

    static float TilingValueNoise(float u, float v, int frequency, int octave)
    {
        float x = u * frequency;
        float y = v * frequency;

        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);

        float fx = x - x0;
        float fy = y - y0;

        // Smoothstep: doğrusal harmanlama kafes çizgilerini görünür bırakıyor.
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        float v00 = Lattice(x0, y0, frequency, octave);
        float v10 = Lattice(x0 + 1, y0, frequency, octave);
        float v01 = Lattice(x0, y0 + 1, frequency, octave);
        float v11 = Lattice(x0 + 1, y0 + 1, frequency, octave);

        return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
    }

    static float Lattice(int x, int y, int frequency, int octave)
    {
        // Sarma: frekansın katında aynı değere dönüyor → doku döşenebiliyor.
        x = ((x % frequency) + frequency) % frequency;
        y = ((y % frequency) + frequency) % frequency;

        unchecked
        {
            int h = Seed;
            h = h * 73856093 ^ x * 19349663;
            h = h * 83492791 ^ y * 39916801;
            h ^= octave * 2654435761u.GetHashCode();
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;

            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
