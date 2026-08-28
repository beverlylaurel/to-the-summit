using System;
using UnityEditor;
using UnityEngine;

/// Generates surface data maps from heightfield.
/// Allows material layer distribution to drive from mountain morphology rather than noise.
///
/// Channels:
///   R  accumulation — downhill material flow; gravel collects in gullies
///   G  concavity    — local hollows; moisture collects in crevices, dries on ridges
///   B  exposure     — sky view factor; sheltered crevices remain shadowed and moist
///   A  slope        — vertical normal component (cos), from smoothed ground
public static class SurfaceMapBaker
{
    const string MapPath = "Assets/Terrain/MountainSurfaceMaps.asset";
    const string NormalPath = "Assets/Terrain/MountainNormals.asset";

    public const int NormalResolution = 4096;
    const string NormalName = "MountainNormals-4096-blur4";

    const string HeightPath = "Assets/Terrain/MountainHeight.asset";
    public const int HeightResolution = 1024;
    const string HeightName = "MountainHeight-r16-1k";

    const string HorizonPath = "Assets/Terrain/MountainHorizon.asset";
    const string HorizonName = "MountainHorizon-r16-nolocal";

    public const int HorizonDirections = 16;
    const int HorizonResolution = 1024;

    const string MapName = "MountainSurfaceMaps-slope";
    public const int MapResolution = 1024;

    const string DriftPath = "Assets/Terrain/MountainWindWeight.asset";

    static string DriftName(float prevailingDegrees) =>
        $"MountainWindWeight-r8-{Mathf.RoundToInt(prevailingDegrees)}";

    static void StampVersion(string path, string version)
    {
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

        if (asset != null && asset.name != fileName)
        {
            asset.name = fileName;
            EditorUtility.SetDirty(asset);
        }

        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer == null || importer.userData == version) return;

        importer.userData = version;
        AssetDatabase.WriteImportSettingsIfDirty(path);
    }

    static bool VersionMatches(string path, string version)
    {
        AssetImporter importer = AssetImporter.GetAtPath(path);
        return importer != null && importer.userData == version;
    }

    /// Invalidates version stamps on all baked surface maps.
    public static void Invalidate()
    {
        foreach (string path in new[] { MapPath, NormalPath, HorizonPath, HeightPath, DriftPath })
        {
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null || importer.userData.Length == 0) continue;
            importer.userData = string.Empty;
            importer.SaveAndReimport();
        }
    }

    public static bool MapsCurrent(float prevailingDegrees)
    {
        var maps = Load();
        return maps != null && maps.width == MapResolution
            && VersionMatches(MapPath, MapName)
            && VersionMatches(NormalPath, NormalName)
            && VersionMatches(HorizonPath, HorizonName)
            && VersionMatches(HeightPath, HeightName)
            && LoadDrift() != null
            && VersionMatches(DriftPath, DriftName(prevailingDegrees));
    }

    public static Texture2D Load() => AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
    public static Texture2D LoadDrift() => AssetDatabase.LoadAssetAtPath<Texture2D>(DriftPath);
    public static Texture2D LoadNormals() => AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
    public static Texture2DArray LoadHorizon() => AssetDatabase.LoadAssetAtPath<Texture2DArray>(HorizonPath);
    public static Texture2D LoadHeight() => AssetDatabase.LoadAssetAtPath<Texture2D>(HeightPath);

    public static Texture2D Bake(Terrain terrain, float prevailingDegrees)
    {
        if (terrain == null)
            throw new InvalidOperationException($"{nameof(SurfaceMapBaker)}: terrain is null.");

        var data = terrain.terrainData;
        int res = MapResolution;
        float[,] height = Downsample(data, res);

        float spacing = data.size.x / (res - 1);
        float vertical = data.size.y;

        int cells = res * res;
        var accumulation = Accumulate(height, res, spacing, vertical);
        var concavity = new float[cells];
        var exposure = new float[cells];

        System.Threading.Tasks.Parallel.For(0, res, y =>
        {
            for (int x = 0; x < res; x++)
            {
                int index = y * res + x;
                concavity[index] = Concavity(height, res, x, y, spacing, vertical);
                exposure[index] = SkyExposure(height, res, x, y, spacing, vertical);
            }
        });

        BakeWindWeight(height, concavity, res, spacing, vertical, prevailingDegrees);

        Normalize(accumulation);
        Normalize(concavity);
        Normalize(exposure);

        var slope = new float[cells];
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            slope[y * res + x] = SlopeCosine(height, res, x, y, spacing, vertical);

        var pixels = new Color32[cells];
        for (int i = 0; i < cells; i++)
            pixels[i] = new Color32(ToByte(accumulation[i]), ToByte(concavity[i]),
                                    ToByte(exposure[i]), ToByte(slope[i]));

        var texture = Load();
        if (texture == null || texture.width != res)
        {
            texture = new Texture2D(res, res, TextureFormat.RGBA32, true, true);
            AssetDatabase.CreateAsset(texture, MapPath);
        }

        StampVersion(MapPath, MapName);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(true);

        BakeNormals(data);
        BakeHorizon(data);
        BakeHeight(data);

        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssets();

        return texture;
    }

    static void BakeNormals(TerrainData data)
    {
        int res = NormalResolution;
        float[,] height = Downsample(data, res);

        float spacing = data.size.x / (res - 1);
        float vertical = data.size.y;

        var gx = new float[res, res];
        var gz = new float[res, res];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, res - 1);
            int y0 = Mathf.Max(y - 1, 0), y1 = Mathf.Min(y + 1, res - 1);

            gx[y, x] = (height[y, x1] - height[y, x0]) * vertical / ((x1 - x0) * spacing);
            gz[y, x] = (height[y1, x] - height[y0, x]) * vertical / ((y1 - y0) * spacing);
        }

        for (int pass = 0; pass < 2; pass++)
        {
            BoxBlur(gx, res);
            BoxBlur(gz, res);
        }

        var pixels = new Color32[res * res];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            var normal = new Vector3(-gx[y, x], 1f, -gz[y, x]).normalized;

            pixels[y * res + x] = new Color32(
                (byte)((normal.x * 0.5f + 0.5f) * 255f),
                (byte)((normal.z * 0.5f + 0.5f) * 255f), 0, 255);
        }

        var texture = LoadNormals();
        if (texture == null || texture.width != res)
        {
            texture = new Texture2D(res, res, TextureFormat.RGBA32, true, true);
            AssetDatabase.CreateAsset(texture, NormalPath);
        }

        StampVersion(NormalPath, NormalName);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply(true);

        EditorUtility.SetDirty(texture);
    }

    static void BakeHeight(TerrainData data)
    {
        int res = HeightResolution;
        float[,] height = Downsample(data, res);

        var pixels = new Color[res * res];
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
            pixels[y * res + x] = new Color(height[y, x], 0f, 0f, 0f);

        var texture = LoadHeight();
        if (texture == null || texture.width != res)
        {
            texture = new Texture2D(res, res, TextureFormat.RHalf, false, true);
            AssetDatabase.CreateAsset(texture, HeightPath);
        }

        StampVersion(HeightPath, HeightName);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels(pixels);
        texture.Apply(false);

        EditorUtility.SetDirty(texture);
    }

    static void BakeHorizon(TerrainData data)
    {
        int res = HorizonResolution;
        float[,] height = Downsample(data, res);

        float spacing = data.size.x / (res - 1);
        float vertical = data.size.y;

        var texture = LoadHorizon();
        if (texture == null || texture.width != res || texture.depth != HorizonDirections
            || texture.format != TextureFormat.R16)
        {
            texture = new Texture2DArray(res, res, HorizonDirections,
                TextureFormat.R16, false, true);
            AssetDatabase.CreateAsset(texture, HorizonPath);
        }

        var slice = new ushort[res * res];

        for (int d = 0; d < HorizonDirections; d++)
        {
            EditorUtility.DisplayProgressBar("Surface",
                $"Baking horizon map ({d + 1}/{HorizonDirections})...",
                d / (float)HorizonDirections);

            float angle = d * Mathf.PI * 2f / HorizonDirections;
            float dirX = Mathf.Cos(angle);
            float dirZ = Mathf.Sin(angle);

            System.Threading.Tasks.Parallel.For(0, res, y =>
            {
                for (int x = 0; x < res; x++)
                {
                    float h0 = height[y, x] * vertical;
                    float steepest = 0f;

                    float travelled = spacing;

                    while (true)
                    {
                        float sx = x + dirX * (travelled / spacing);
                        float sy = y + dirZ * (travelled / spacing);

                        if (sx < 0f || sy < 0f || sx > res - 2 || sy > res - 2) break;

                        int ix = (int)sx, iy = (int)sy;
                        float fx = sx - ix, fy = sy - iy;

                        float h = (height[iy, ix] * (1f - fx) + height[iy, ix + 1] * fx) * (1f - fy)
                                + (height[iy + 1, ix] * (1f - fx) + height[iy + 1, ix + 1] * fx) * fy;

                        float slope = (h * vertical - h0) / travelled;
                        if (slope > steepest) steepest = slope;

                        travelled *= 1.3f;
                    }

                    // Subtract local slope from horizon angle to decouple terrain self-occlusion from N.L.
                    int lx0 = Mathf.Max(x - 1, 0), lx1 = Mathf.Min(x + 1, res - 1);
                    int ly0 = Mathf.Max(y - 1, 0), ly1 = Mathf.Min(y + 1, res - 1);
                    float gxLocal = (height[y, lx1] - height[y, lx0]) * vertical / ((lx1 - lx0) * spacing);
                    float gzLocal = (height[ly1, x] - height[ly0, x]) * vertical / ((ly1 - ly0) * spacing);
                    float localRise = Mathf.Atan(Mathf.Max(gxLocal * dirX + gzLocal * dirZ, 0f));
                    float occlusion = Mathf.Max(Mathf.Atan(steepest) - localRise, 0f);

                    slice[y * res + x] = (ushort)(occlusion / (Mathf.PI * 0.5f) * 65535f);
                }
            });

            texture.SetPixelData(slice, 0, d);
        }

        EditorUtility.ClearProgressBar();

        StampVersion(HorizonPath, HorizonName);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(false);

        EditorUtility.SetDirty(texture);
    }

    static void BoxBlur(float[,] values, int res)
    {
        const int Radius = 2;
        var row = new float[res];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = -Radius; k <= Radius; k++)
                {
                    int i = x + k;
                    if (i < 0 || i >= res) continue;
                    sum += values[y, i];
                    count++;
                }
                row[x] = sum / count;
            }
            for (int x = 0; x < res; x++) values[y, x] = row[x];
        }

        for (int x = 0; x < res; x++)
        {
            for (int y = 0; y < res; y++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = -Radius; k <= Radius; k++)
                {
                    int i = y + k;
                    if (i < 0 || i >= res) continue;
                    sum += values[i, x];
                    count++;
                }
                row[y] = sum / count;
            }
            for (int y = 0; y < res; y++) values[y, x] = row[y];
        }
    }

    static byte ToByte(float value) => (byte)(Mathf.Clamp01(value) * 255f);

    static float SlopeCosine(float[,] height, int res, int x, int y, float spacing, float vertical)
    {
        int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, res - 1);
        int y0 = Mathf.Max(y - 1, 0), y1 = Mathf.Min(y + 1, res - 1);

        float gx = (height[y, x1] - height[y, x0]) * vertical / ((x1 - x0) * spacing);
        float gz = (height[y1, x] - height[y0, x]) * vertical / ((y1 - y0) * spacing);

        return 1f / Mathf.Sqrt(1f + gx * gx + gz * gz);
    }

    /// WIND WEIGHT: Liston & Sturm wind-terrain / MicroMet formulation:
    ///   W = 1 + 0.5*Ws + 0.5*Wc, W in [0.5, 1.5], accumulation proportional to 1/W
    static void BakeWindWeight(float[,] height, float[] concavity, int res,
        float spacing, float vertical, float prevailingDegrees)
    {
        float angle = prevailingDegrees * Mathf.Deg2Rad;
        float windX = Mathf.Cos(angle);
        float windZ = Mathf.Sin(angle);

        int cells = res * res;
        var alongWind = new float[cells];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int x0 = Mathf.Max(x - 1, 0), x1 = Mathf.Min(x + 1, res - 1);
            int y0 = Mathf.Max(y - 1, 0), y1 = Mathf.Min(y + 1, res - 1);

            float dx = (height[y, x1] - height[y, x0]) * vertical / ((x1 - x0) * spacing);
            float dz = (height[y1, x] - height[y0, x]) * vertical / ((y1 - y0) * spacing);

            alongWind[y * res + x] = dx * windX + dz * windZ;
        }

        NormalizeSigned(alongWind);

        var curvature = (float[])concavity.Clone();
        NormalizeSigned(curvature);

        var pixels = new byte[cells];
        for (int i = 0; i < cells; i++)
        {
            float w = Mathf.Clamp(1f + 0.5f * alongWind[i] - 0.5f * curvature[i], 0.5f, 1.5f);
            pixels[i] = ToByte(1f / w * 0.5f);
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(DriftPath);
        if (texture == null || texture.width != res || texture.format != TextureFormat.R8)
        {
            texture = new Texture2D(res, res, TextureFormat.R8, true, true);
            AssetDatabase.CreateAsset(texture, DriftPath);
        }

        StampVersion(DriftPath, DriftName(prevailingDegrees));
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixelData(pixels, 0);
        texture.Apply(true);

        EditorUtility.SetDirty(texture);
        AssetDatabase.SaveAssetIfDirty(texture);
    }

    static void NormalizeSigned(float[] values)
    {
        var sorted = (float[])values.Clone();
        for (int i = 0; i < sorted.Length; i++) sorted[i] = Mathf.Abs(sorted[i]);
        Array.Sort(sorted);

        float scale = sorted[Mathf.FloorToInt(sorted.Length * 0.98f)];
        if (scale <= 1e-6f) return;

        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Clamp(values[i] / scale, -1f, 1f) * 0.5f;
    }

    static void Normalize(float[] values)
    {
        var sorted = (float[])values.Clone();
        Array.Sort(sorted);

        float low = sorted[Mathf.FloorToInt(sorted.Length * 0.02f)];
        float high = sorted[Mathf.FloorToInt(sorted.Length * 0.98f)];
        float span = high - low;

        if (span <= 1e-6f) return;

        for (int i = 0; i < values.Length; i++)
            values[i] = Mathf.Clamp01((values[i] - low) / span);
    }

    static float[,] Downsample(TerrainData data, int res)
    {
        int source = data.heightmapResolution;
        float[,] full = data.GetHeights(0, 0, source, source);

        if (source == res) return full;

        var result = new float[res, res];
        float step = (source - 1f) / (res - 1f);
        int radius = Mathf.Max(1, Mathf.CeilToInt(step * 0.5f));

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int cx = Mathf.RoundToInt(x * step);
            int cy = Mathf.RoundToInt(y * step);

            int x0 = Mathf.Max(cx - radius, 0), x1 = Mathf.Min(cx + radius, source - 1);
            int y0 = Mathf.Max(cy - radius, 0), y1 = Mathf.Min(cy + radius, source - 1);

            float sum = 0f;
            for (int sy = y0; sy <= y1; sy++)
            for (int sx = x0; sx <= x1; sx++)
                sum += full[sy, sx];

            result[y, x] = sum / ((x1 - x0 + 1) * (y1 - y0 + 1));
        }

        return result;
    }

    static float[] Accumulate(float[,] height, int res, float spacing, float vertical)
    {
        int cells = res * res;
        var load = new float[cells];
        var slopes = new float[8];

        for (int i = 0; i < cells; i++) load[i] = 1f;

        var order = new int[cells];
        var keys = new float[cells];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            int index = y * res + x;
            order[index] = index;
            keys[index] = -height[y, x];
        }

        Array.Sort(keys, order);

        foreach (int here in order)
        {
            int x = here % res, y = here / res;
            float carried = load[here];
            float h = height[y, x] * vertical;

            float total = 0f;
            int n = 0;

            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;

                int nx = x + ox, ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= res || ny >= res) { slopes[n++] = 0f; continue; }

                float drop = h - height[ny, nx] * vertical;
                if (drop <= 0f) { slopes[n++] = 0f; continue; }

                float distance = spacing * ((ox != 0 && oy != 0) ? 1.41421f : 1f);
                float slope = drop / distance;

                slopes[n++] = slope;
                total += slope;
            }

            if (total <= 0f) continue;

            n = 0;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;

                float share = slopes[n++];
                if (share <= 0f) continue;

                load[(y + oy) * res + (x + ox)] += carried * (share / total);
            }
        }

        for (int i = 0; i < cells; i++)
            load[i] = Mathf.Log(1f + load[i]);

        return Blur(load, res);
    }

    static float[] Blur(float[] source, int res)
    {
        var result = new float[source.Length];

        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float sum = 0f;
            int count = 0;

            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int nx = x + ox, ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= res || ny >= res) continue;

                sum += source[ny * res + nx];
                count++;
            }

            result[y * res + x] = sum / count;
        }

        return result;
    }

    const int ConcavityRadius = 6;

    static float Concavity(float[,] height, int res, int x, int y, float spacing, float vertical)
    {
        int x0 = Mathf.Max(x - ConcavityRadius, 0), x1 = Mathf.Min(x + ConcavityRadius, res - 1);
        int y0 = Mathf.Max(y - ConcavityRadius, 0), y1 = Mathf.Min(y + ConcavityRadius, res - 1);

        const float Sigma = ConcavityRadius * 0.5f;
        const float TwoSigmaSquared = 2f * Sigma * Sigma;
        const float RadiusSquared = ConcavityRadius * ConcavityRadius;

        float sum = 0f;
        float weightTotal = 0f;

        for (int ny = y0; ny <= y1; ny++)
        for (int nx = x0; nx <= x1; nx++)
        {
            if (nx == x && ny == y) continue;

            float dx = nx - x, dy = ny - y;
            float distanceSquared = dx * dx + dy * dy;
            if (distanceSquared > RadiusSquared) continue;

            float weight = Mathf.Exp(-distanceSquared / TwoSigmaSquared);
            sum += height[ny, nx] * weight;
            weightTotal += weight;
        }

        return (sum / weightTotal - height[y, x]) * vertical / (spacing * ConcavityRadius);
    }

    static readonly int[] ExposureSteps = { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89 };

    static float SkyExposure(float[,] height, int res, int x, int y, float spacing, float vertical)
    {
        const int Directions = 8;

        float h = height[y, x] * vertical;
        float open = 0f;

        for (int d = 0; d < Directions; d++)
        {
            float angle = d * Mathf.PI * 2f / Directions;
            float ux = Mathf.Cos(angle), uy = Mathf.Sin(angle);

            float highest = 0f;

            foreach (int step in ExposureSteps)
            {
                int nx = x + Mathf.RoundToInt(ux * step);
                int ny = y + Mathf.RoundToInt(uy * step);
                if (nx < 0 || ny < 0 || nx >= res || ny >= res) break;

                float rise = height[ny, nx] * vertical - h;
                if (rise <= 0f) continue;

                highest = Mathf.Max(highest, rise / (spacing * step));
            }

            open -= highest;
        }

        return open / Directions;
    }
}
