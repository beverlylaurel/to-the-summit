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
    public const string DetailNormalPath = "Assets/Snow/Textures/T_Snow_DetailNormal.png";

    const int BreakupResolution = 256;

    /// Tohum sabit: aynı doku her makinede aynı çıksın.
    const int Seed = 20260822;

    public static Texture2D EnsureBreakup()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(BreakupPath);
        if (existing != null) return existing;

        EnsureFolder();

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

    /// TEK DETAY NORMALİ, DÖRT ÖLÇEKTE. Spec §14.2 dört katman istiyor
    /// (makro 8 m, mezo 0.6 m, mikro 0.05 m, ezilmiş 0.25 m) ama dört ayrı
    /// doku istemiyor — aynı döşenebilir normal farklı tile'larla örneklenince
    /// katmanlar birbirine benzemiyor.
    public static Texture2D EnsureDetailNormal()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(DetailNormalPath);
        if (existing != null) return existing;

        EnsureFolder();

        const int Res = 256;

        var height = new float[Res, Res];

        for (int y = 0; y < Res; y++)
        for (int x = 0; x < Res; x++)
            height[y, x] = TilingFbm(x / (float)Res, y / (float)Res);

        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false, true);
        var px = new Color32[Res * Res];

        // Yükseklikten normal: merkezi fark, döşenebilirlik için sarmalı.
        for (int y = 0; y < Res; y++)
        for (int x = 0; x < Res; x++)
        {
            float hL = height[y, (x - 1 + Res) % Res];
            float hR = height[y, (x + 1) % Res];
            float hD = height[(y - 1 + Res) % Res, x];
            float hU = height[(y + 1) % Res, x];

            var n = new Vector3(hL - hR, hD - hU, NormalStrength).normalized;

            px[y * Res + x] = new Color32(
                (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f),
                (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f),
                (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f),
                255);
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        File.WriteAllBytes(DetailNormalPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(DetailNormalPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(DetailNormalPath);
        importer.textureType = TextureImporterType.NormalMap;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(DetailNormalPath);
    }

    // ------------------------------------------------------------ tane atlası

    public const string FlakeAtlasPath = "Assets/Snow/Textures/T_Flake_Atlas.png";

    /// 4×4 TANE ATLASI (spec §17.1). On altı AYRI kristal — hepsi aynı
    /// olsaydı yağış tekrar eden bir desen olurdu ve "irili ufaklı değil"
    /// belirtisi buradan doğardı.
    ///
    /// Altı katlı simetri gerçek kar kristalinin kendi simetrisi; kollar ve
    /// yan dallar hücreden hücreye değişiyor. Son iki hücre yuvarlak
    /// (graupel) — sulu karda o biçim baskın.
    public static Texture2D EnsureFlakeAtlas()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(FlakeAtlasPath);
        if (existing != null) return existing;

        EnsureFolder();

        const int Cell = 64;
        const int Grid = 4;
        const int Res = Cell * Grid;

        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, true, true);
        var px = new Color32[Res * Res];

        for (int cy = 0; cy < Grid; cy++)
        for (int cx = 0; cx < Grid; cx++)
        {
            int index = cy * Grid + cx;
            bool graupel = index >= Grid * Grid - 2;

            var rng = new System.Random(Seed + index * 7919);

            // Kolun uzunluğu ve kalınlığı, yan dalların yeri — hücre başına.
            float armLength = graupel ? 0.30f : Mathf.Lerp(0.62f, 0.92f, (float)rng.NextDouble());
            float armWidth = Mathf.Lerp(0.055f, 0.110f, (float)rng.NextDouble());
            float coreRadius = graupel ? Mathf.Lerp(0.34f, 0.46f, (float)rng.NextDouble())
                                       : Mathf.Lerp(0.10f, 0.18f, (float)rng.NextDouble());

            int branchCount = graupel ? 0 : 2 + (int)(rng.NextDouble() * 2.0);

            var branchAt = new float[branchCount];
            var branchLen = new float[branchCount];
            var branchAngle = new float[branchCount];

            for (int b = 0; b < branchCount; b++)
            {
                branchAt[b] = Mathf.Lerp(0.25f, 0.80f, (b + 0.5f) / branchCount);
                branchLen[b] = Mathf.Lerp(0.12f, 0.34f, (float)rng.NextDouble()) * armLength;
                branchAngle[b] = Mathf.Lerp(35f, 65f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
            }

            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                float u = (x + 0.5f) / Cell * 2f - 1f;
                float v = (y + 0.5f) / Cell * 2f - 1f;

                float dist = Mathf.Sqrt(u * u + v * v);
                float alpha = 0f;

                // Çekirdek
                alpha = Mathf.Max(alpha, 1f - Step01(coreRadius * 0.6f, coreRadius, dist));

                if (!graupel)
                {
                    // ALTI KATLI SİMETRİ: açı 60°'ye katlanıyor, tek kol
                    // çiziliyor, altısı birden çıkıyor.
                    float angle = Mathf.Atan2(v, u);
                    float folded = Mathf.Repeat(angle, Mathf.PI / 3f) - Mathf.PI / 6f;

                    var p = new Vector2(Mathf.Cos(folded) * dist, Mathf.Sin(folded) * dist);

                    float d = SegmentDistance(p, Vector2.zero, new Vector2(armLength, 0f));

                    for (int b = 0; b < branchCount; b++)
                    {
                        var root = new Vector2(armLength * branchAt[b], 0f);
                        var tip = root + new Vector2(Mathf.Cos(branchAngle[b]),
                                                     Mathf.Sin(branchAngle[b])) * branchLen[b];
                        var tipMirror = root + new Vector2(Mathf.Cos(-branchAngle[b]),
                                                           Mathf.Sin(-branchAngle[b])) * branchLen[b];

                        d = Mathf.Min(d, SegmentDistance(p, root, tip));
                        d = Mathf.Min(d, SegmentDistance(p, root, tipMirror));
                    }

                    alpha = Mathf.Max(alpha, 1f - Step01(armWidth * 0.5f, armWidth, d));
                }
                else
                {
                    // Graupel: pütürlü yuvarlak.
                    float bump = Mathf.Sin(Mathf.Atan2(v, u) * 7f) * 0.035f;
                    alpha = Mathf.Max(alpha, 1f - Step01(coreRadius + bump,
                                                          coreRadius + bump + 0.08f, dist));
                }

                // Kenarda tam sıfıra in: atlas hücreleri birbirine sızmasın.
                alpha *= 1f - Step01(0.88f, 1.0f, dist);

                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255);
                px[(cy * Cell + y) * Res + (cx * Cell + x)] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(px);
        tex.Apply(true, false);

        File.WriteAllBytes(FlakeAtlasPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(FlakeAtlasPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(FlakeAtlasPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(FlakeAtlasPath);
    }

    /// GERÇEK EŞİK FONKSİYONU. `Mathf.SmoothStep(a, b, t)` GLSL'in
    /// `smoothstep`'i DEĞİL — a ile b ARASINDA interpolasyon yapıyor, eşik
    /// uygulamıyor. Karıştırıldığında atlas kristal değil düz soluk bir leke
    /// çıkıyor (ölçüldü: on altı hücrenin kaplaması %2.0–%2.8, hepsi aynı).
    static float Step01(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, 1e-6f));
        return t * t * (3f - 2f * t);
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return Vector2.Distance(p, a + ab * t);
    }

    /// Yüzeyin dikliği. Küçültülürse normaller yatıklaşıp detay kaybolur,
    /// büyütülürse yüzey plastik görünür.
    const float NormalStrength = 0.06f;

    static void EnsureFolder()
    {
        string folder = Path.GetDirectoryName(BreakupPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Snow", "Textures");
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
