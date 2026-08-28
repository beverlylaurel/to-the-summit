using UnityEditor;
using UnityEngine;

/// Generates volumetric weather map. Channels [H18 p.11]:
///   R = sparse coverage (w_c0) — cloud locations with clear gaps in between
///   G = dense coverage (w_c1) — overcast coverage when slider exceeds 0.5
///   B = maximum cloud height (w_h) — column ceiling
///
/// G is derived from the SAME noise as R with a lower threshold, ensuring clouds expand outward naturally as coverage increases.
/// Noise wraps periodically to match world XZ tiling.
public static class CloudMapGenerator
{
    const string MapPath = "Assets/VolumetricClouds/Textures/CloudMap.asset";

    /// GENERATOR VERSION. Incremented when algorithm or constants change;
    /// bootstrap checks label and regenerates if stale.
    const int MapVersion = 4;
    static string VersionLabel => $"CloudMap-v{MapVersion}";
    const int Resolution = 512;
    const int Octaves = 5;
    const int BaseCells = 4;
    const float MinCloudTop = 0.55f;

    // `DA = ... * w_d * 2` [H18 App B.3]: 0.5 neutral multiplier. Range [0.35, 0.65] -> multiplier [0.70, 1.30].
    const float MinMapDensity = 0.35f;
    const float MaxMapDensity = 0.65f;

    // Empirically tuned:
    // 0.50/0.15 -> 47% cloud cover, 23% saturated core, 0.74 mean in-cloud value.
    const float SparseThreshold = 0.50f;
    const float SparseEdge = 0.15f;

    // Dense threshold calibrated for overcast skies:
    // 0.00 / 0.40 -> mean 0.888, 64% saturated, 0% total gap, 27% thinning.
    const float DenseThreshold = 0.0f;
    const float DenseEdge = 0.40f;

    [MenuItem("To The Summit/Clouds/Bake Weather Map", false, 40)]
    public static void Generate()
    {
        CreateOrUpdate();
        AssetDatabase.SaveAssets();
    }

    /// Creates weather map if missing, updates if stale, or returns existing if up-to-date.
    public static Texture2D EnsureExists()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
        if (existing == null) return CreateOrUpdate();

        foreach (var label in AssetDatabase.GetLabels(existing))
            if (label == VersionLabel) return existing;

        return CreateOrUpdate();
    }

    static Texture2D CreateOrUpdate()
    {
        // Ceiling uses separate noise field from placement:
        float[] placement = BuildField(0x51ED270B);
        float[] tops = BuildField(0x2F6E1A93);
        float[] densities = BuildField(0x7A19C4E5);

        var pixels = new Color[Resolution * Resolution];
        for (int i = 0; i < pixels.Length; i++)
        {
            float sparse = Plateau(placement[i], SparseThreshold, SparseEdge);
            float dense = Plateau(placement[i], DenseThreshold, DenseEdge);
            float top = Mathf.Lerp(MinCloudTop, 1.0f, tops[i]);
            float density = Mathf.Lerp(MinMapDensity, MaxMapDensity, densities[i]);
            pixels[i] = new Color(sparse, dense, top, density);
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
        bool isNew = texture == null;
        if (isNew)
            texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, mipChain: false, linear: true);
        else if (texture.format != TextureFormat.RGBA32 || texture.width != Resolution)
            texture.Reinitialize(Resolution, Resolution, TextureFormat.RGBA32, hasMipMap: false);

        texture.name = "CloudMap";
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false);

        if (isNew) AssetDatabase.CreateAsset(texture, MapPath);
        else EditorUtility.SetDirty(texture);

        AssetDatabase.SetLabels(texture, new[] { VersionLabel });

        return texture;
    }

    /// Generates fBm field normalized to [0, 1].
    static float[] BuildField(uint seed)
    {
        var field = new float[Resolution * Resolution];
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float value = Fbm(x / (float)Resolution, y / (float)Resolution, seed);
                field[y * Resolution + x] = value;
                if (value < min) min = value;
                if (value > max) max = value;
            }
        }

        float range = max - min;
        for (int i = 0; i < field.Length; i++)
            field[i] = (field[i] - min) / range;

        return field;
    }

    /// Zero below threshold, 1.0 beyond (threshold + edge). Soft edge with saturated core.
    static float Plateau(float value, float threshold, float edge)
    {
        return Mathf.Clamp01((value - threshold) / edge);
    }

    static float Fbm(float u, float v, uint seed)
    {
        float sum = 0.0f;
        float amplitude = 1.0f;
        float normalization = 0.0f;
        int cells = BaseCells;

        for (int octave = 0; octave < Octaves; octave++)
        {
            sum += ValueNoise(u * cells, v * cells, cells, seed + (uint)octave * 0x9E3779B1u) * amplitude;
            normalization += amplitude;
            amplitude *= 0.5f;
            cells *= 2;
        }

        return sum / normalization;
    }

    /// Periodic value noise wrapping on cell period.
    static float ValueNoise(float x, float y, int period, uint seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float fx = x - x0;
        float fy = y - y0;
        fx = fx * fx * (3.0f - 2.0f * fx);
        fy = fy * fy * (3.0f - 2.0f * fy);

        float v00 = Hash(x0, y0, period, seed);
        float v10 = Hash(x0 + 1, y0, period, seed);
        float v01 = Hash(x0, y0 + 1, period, seed);
        float v11 = Hash(x0 + 1, y0 + 1, period, seed);

        return Mathf.Lerp(Mathf.Lerp(v00, v10, fx), Mathf.Lerp(v01, v11, fx), fy);
    }

    static uint Mix(uint h)
    {
        h ^= h >> 16; h *= 0x7feb352du;
        h ^= h >> 15; h *= 0x846ca68bu;
        h ^= h >> 16;
        return h;
    }

    static float Hash(int x, int y, int period, uint seed)
    {
        uint cx = (uint)(((x % period) + period) % period);
        uint cy = (uint)(((y % period) + period) % period);
        uint h = Mix(cx * 0x9E3779B1u) ^ Mix(cy * 0x85EBCA77u) ^ Mix(seed);
        return Mix(h) / 4294967296.0f;
    }
}
