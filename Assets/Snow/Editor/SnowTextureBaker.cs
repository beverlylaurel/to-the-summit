// Generates procedural textures required by the snow subsystem and writes them as assets.
// Invoked by: SnowDebugWindow (scene setup).

using System.IO;
using UnityEditor;
using UnityEngine;

public static class SnowTextureBaker
{
    public const string BreakupPath = "Assets/Snow/Textures/T_Snow_Breakup.png";
    public const string DetailNormalPath = "Assets/Snow/Textures/T_Snow_DetailNormal.png";

    const int BreakupResolution = 256;

    const int Seed = 20260822;

    public static Texture2D EnsureBreakup()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(BreakupPath);
        if (existing != null) return existing;

        EnsureFolder();

        var tex = new Texture2D(BreakupResolution, BreakupResolution, TextureFormat.RGBA32, false, true);
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

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(BreakupPath);
    }

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

    public const string SastrugiNoisePath = "Assets/Snow/Textures/T_Sastrugi_Noise.png";

    public static Texture2D EnsureSastrugiNoise()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(SastrugiNoisePath);
        if (existing != null) return existing;

        EnsureFolder();

        const int Res = 256;

        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false, true);
        var px = new Color32[Res * Res];

        for (int y = 0; y < Res; y++)
        for (int x = 0; x < Res; x++)
        {
            float u = (x + 0.5f) / Res;
            float v = (y + 0.5f) / Res;

            float wave = Mathf.Sin(u * Mathf.PI * 2f * 3f) * 0.5f + 0.5f;
            float noise = TilingFbm(u, v);

            float value = Mathf.Clamp01(wave * 0.55f + noise * 0.45f);

            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
            px[y * Res + x] = new Color32(b, b, b, 255);
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        File.WriteAllBytes(SastrugiNoisePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SastrugiNoisePath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(SastrugiNoisePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(SastrugiNoisePath);
    }

    public const string FlakeAtlasPath = "Assets/Snow/Textures/T_Flake_Atlas.png";

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

                alpha = Mathf.Max(alpha, 1f - Step01(coreRadius * 0.6f, coreRadius, dist));

                if (!graupel)
                {
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
                    float bump = Mathf.Sin(Mathf.Atan2(v, u) * 7f) * 0.035f;
                    alpha = Mathf.Max(alpha, 1f - Step01(coreRadius + bump,
                                                          coreRadius + bump + 0.08f, dist));
                }

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

    const float NormalStrength = 0.06f;

    static void EnsureFolder()
    {
        string folder = Path.GetDirectoryName(BreakupPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Snow", "Textures");
    }

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
