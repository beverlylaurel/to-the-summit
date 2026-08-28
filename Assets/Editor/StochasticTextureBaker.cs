using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// HEITZ-NEYRET stochastic tiling preprocessing.
///
/// Problem: mountain is 17.5 km, texture tiles over meters — a several-meter pattern repeats
/// over 10,000 times reading as a grid. Simple blending weakens repetition but degrades contrast:
/// averaging two samples halves variance, blurring the texture.
///
/// Method: HISTOGRAM TRANSFORMATION on texture. Each channel is rewritten using a rank transform
/// mapping values to a Gaussian distribution. Because weighted sums of Gaussian variables remain Gaussian,
/// blending three samples PRESERVES distribution; an inverse LUT subsequently restores the original histogram.
/// Preserving distribution instead of averaging.
///
/// Baked textures are NOT HARDCODED: all `SurfaceMaterialSet` assets on disk are scanned.
/// Adding a new surface does not require modifying this file.
public static class StochasticTextureBaker
{
    const int LutSize = 256;

    /// Incremented when generation logic changes; outdated markers trigger rebake.
    public const int Revision = 3;
    static string MarkerPath => TextureIngest.Folder + "/stochastic-rev.txt";

    /// Map name -> normal map (3 channels) or single channel.
    static readonly (string map, bool threeChannel)[] Maps =
    {
        ("Normal", true),
        ("Roughness", false),
        ("Height", false)
    };

    [MenuItem("To The Summit/Textures/Rebake Surface Textures", false, 61)]
    static void Rebake()
    {
        if (File.Exists(MarkerPath)) File.Delete(MarkerPath);
        EnsureAll();
    }

    /// Bakes missing or outdated outputs. Returns true if any texture was baked.
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

        ToolLog.Write($"Stochastic tiling baked: {baked.Count} texture(s).");
        return true;
    }

    static void Bake(string path, string outputName, bool threeChannel)
    {
        // Temporarily make uncompressed and readable for processing:
        // compressed textures quantize into blocks in GetPixels, corrupting the histogram.
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

        // Independent per-channel transformation: cross-channel correlation is decoupled,
        // following Heitz's paper — visual difference is negligible, math is much simpler.
        int channels = threeChannel ? 3 : 1;

        for (int c = 0; c < channels; c++)
        {
            var values = new float[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) values[i] = Channel(pixels[i], c);

            // Sorting: cumulative probability rank for each pixel in histogram.
            var order = new int[values.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => values[a].CompareTo(values[b]));

            // Forward transform: cumulative probability -> Gaussian.
            // Fitted to Gaussian with 0.5 mean and 1/6 standard deviation to fit [0, 1].
            var gauss = new float[values.Length];
            for (int rank = 0; rank < order.Length; rank++)
            {
                float u = (rank + 0.5f) / order.Length;
                gauss[order[rank]] = Mathf.Clamp01(InverseGauss(u) / 6f + 0.5f);
            }

            for (int i = 0; i < pixels.Length; i++)
                SetChannel(ref transformed[i], c, gauss[i]);

            // INVERSE LUT: Gaussian value back to original value.
            // Shader evaluates blended Gaussian samples against this LUT.
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

    /// Inverse cumulative distribution function for standard normal distribution (Acklam approximation).
    /// No closed-form solution; approximation precision is 1e-9.
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

    /// Standard normal cumulative distribution function — error function approximation.
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

        // Transformed texture and LUT are both DATA: if marked as normal map,
        // Unity repacks channels and corrupts the transform.
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = !isLut;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = isLut ? 0 : 8;

        // LUT is sampled edge-to-edge: wrapping would bleed opposite edge.
        importer.wrapMode = isLut ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;

        // Compression corrupts histogram — transformation relies on numerical precision.
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = isLut ? 256 : 1024;
        importer.SaveAndReimport();
    }
}
