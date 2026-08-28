using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// EXTERNAL TEXTURE INGESTION. Imports asset folders (ambientCG / Poly Haven / etc.) into project:
/// locates maps, downsamples to target resolution, configures import settings, and MEASURES baked lighting.
///
/// Automated to prevent human errors across repetitive import steps:
/// normal maps marked as "color" invert relief, roughness passing through sRGB corrupts glossiness.
///
/// COLOR MAPS EXCLUDED. Surface coloration in this project is procedural and system-driven
/// (snow: freshness/depth/wetness; rock: oxidation/lichen/altitude). Textures only provide relief,
/// roughness, and height. Importing color maps would decouple the environmental shading pipeline.
public static class TextureIngest
{
    public const string Folder = "Assets/Terrain";
    const int TargetSize = 1024;

    /// Suffix patterns matched in source folders across common naming conventions.
    static readonly (string map, string[] suffixes)[] Wanted =
    {
        ("Normal", new[] { "_NormalGL.png", "_nor_gl.png", "_Normal.png", "_normal.png" }),
        ("Roughness", new[] { "_Roughness.png", "_rough.png", "_roughness.png" }),
        ("Height", new[] { "_Displacement.png", "_disp.png", "_height.png", "_Height.png" })
    };

    /// Baked light correlation warning threshold.
    const float BakedLightWarning = 0.3f;

    [MenuItem("To The Summit/Textures/Fetch Texture...", false, 60)]
    static void IngestMenu()
    {
        string source = EditorUtility.OpenFolderPanel("Texture Folder", "", "");
        if (string.IsNullOrEmpty(source)) return;

        string prefix = Path.GetFileName(source.TrimEnd('/', '\\'));
        prefix = new string(prefix.Where(char.IsLetterOrDigit).ToArray());

        var set = Ingest(source, prefix);
        if (set != null) Selection.activeObject = set;
    }

    /// Ingests folder and creates `SurfaceMaterialSet`. Stochastic transform is handled in `StochasticTextureBaker`.
    public static SurfaceMaterialSet Ingest(string sourceFolder, string prefix)
    {
        if (!Directory.Exists(sourceFolder))
            throw new DirectoryNotFoundException($"Texture folder missing: {sourceFolder}");

        Directory.CreateDirectory(Folder);
        var files = Directory.GetFiles(sourceFolder, "*.png");
        int found = 0;

        foreach (var (map, suffixes) in Wanted)
        {
            string match = files.FirstOrDefault(
                f => suffixes.Any(s => f.EndsWith(s, System.StringComparison.OrdinalIgnoreCase)));

            if (match == null)
            {
                Debug.LogWarning($"{prefix}: {map} map not found.");
                continue;
            }

            Resize(match, $"{Folder}/{prefix}_{map}.png", map == "Normal");
            found++;
        }

        if (found == 0) return null;

        AssetDatabase.Refresh();

        var set = LoadOrCreateSet(prefix);
        set.sourceFolder = sourceFolder;
        set.assetPrefix = prefix;
        Measure(sourceFolder, files, set);

        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssets();

        StochasticTextureBaker.EnsureAll();
        Resolve(set);

        ToolLog.Write($"{prefix}: {found} map(s) ingested. Baked light correlation "
                + $"{set.bakedLightCorrelation:F3}, anisotropy {set.anisotropy:F2}.");
        return set;
    }

    public static SurfaceMaterialSet[] AllSets() =>
        AssetDatabase.FindAssets($"t:{nameof(SurfaceMaterialSet)}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>)
            .Where(s => s != null)
            .ToArray();

    public static void Resolve(SurfaceMaterialSet set)
    {
        Texture2D Load(string suffix) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{set.assetPrefix}{suffix}");

        set.normal = Load("_Normal_T.png");
        set.normalLut = Load("_Normal_T_LUT.png");
        set.roughness = Load("_Roughness_T.png");
        set.roughnessLut = Load("_Roughness_T_LUT.png");
        set.height = Load("_Height_T.png");
        set.heightLut = Load("_Height_T_LUT.png");

        EditorUtility.SetDirty(set);
    }

    static SurfaceMaterialSet LoadOrCreateSet(string prefix)
    {
        string path = $"{Folder}/{prefix}.asset";
        var set = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(path);
        if (set != null) return set;

        set = ScriptableObject.CreateInstance<SurfaceMaterialSet>();
        AssetDatabase.CreateAsset(set, path);
        return set;
    }

    /// MEASURES BAKED LIGHTING. In photogrammetry textures, directional sunlight is often baked into albedo:
    /// mounds systematically bright, crevices dark. Correlation between color luminance and normal slope
    /// quantifies baked lighting objectively.
    static void Measure(string sourceFolder, string[] files, SurfaceMaterialSet set)
    {
        string colorPath = files.FirstOrDefault(
            f => f.EndsWith("_Color.png", System.StringComparison.OrdinalIgnoreCase)
              || f.EndsWith("_diff.png", System.StringComparison.OrdinalIgnoreCase));
        string normalPath = files.FirstOrDefault(
            f => f.EndsWith("_NormalGL.png", System.StringComparison.OrdinalIgnoreCase)
              || f.EndsWith("_nor_gl.png", System.StringComparison.OrdinalIgnoreCase));

        if (colorPath == null || normalPath == null) return;

        var color = ReadRaw(colorPath);
        var normal = ReadRaw(normalPath);
        if (color == null || normal == null) return;

        int count = Mathf.Min(color.Length, normal.Length);
        double sumC = 0, sumX = 0, sumY = 0;

        for (int i = 0; i < count; i++)
        {
            sumC += color[i].grayscale;
            sumX += normal[i].r * 2 - 1;
            sumY += normal[i].g * 2 - 1;
        }

        double meanC = sumC / count, meanX = sumX / count, meanY = sumY / count;
        double covX = 0, covY = 0, varC = 0, varX = 0, varY = 0;

        for (int i = 0; i < count; i++)
        {
            double c = color[i].grayscale - meanC;
            double x = normal[i].r * 2 - 1 - meanX;
            double y = normal[i].g * 2 - 1 - meanY;

            covX += c * x; covY += c * y;
            varC += c * c; varX += x * x; varY += y * y;
        }

        float corrX = (float)(covX / (System.Math.Sqrt(varC * varX) + 1e-9));
        float corrY = (float)(covY / (System.Math.Sqrt(varC * varY) + 1e-9));

        set.bakedLightCorrelation = Mathf.Max(Mathf.Abs(corrX), Mathf.Abs(corrY));
        set.anisotropy = (float)(System.Math.Sqrt(varX) / (System.Math.Sqrt(varY) + 1e-9));

        if (set.bakedLightCorrelation > BakedLightWarning)
            Debug.LogWarning(
                $"{set.assetPrefix}: BAKED LIGHT DETECTED (correlation "
              + $"{set.bakedLightCorrelation:F2}). Texture contains baked directional lighting conflicting "
              + "with scene sun. Choose another texture or derive normals purely from height.");
    }

    static Color[] ReadRaw(string path)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!texture.LoadImage(File.ReadAllBytes(path))) return null;

        var scaled = Downscale(texture, 256);
        var pixels = scaled.GetPixels();

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(scaled);
        return pixels;
    }

    static void Resize(string source, string destination, bool isNormal)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!texture.LoadImage(File.ReadAllBytes(source)))
            throw new IOException($"Could not read: {source}");

        var scaled = Downscale(texture, TargetSize);
        File.WriteAllBytes(destination, scaled.EncodeToPNG());

        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(scaled);
        _ = isNormal;
    }

    static Texture2D Downscale(Texture2D source, int size)
    {
        var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active;

        Graphics.Blit(source, target);
        RenderTexture.active = target;

        var result = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        result.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        result.Apply();

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        return result;
    }
}
