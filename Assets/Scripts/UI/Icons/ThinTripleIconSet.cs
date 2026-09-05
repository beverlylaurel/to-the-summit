using System;
using UnityEngine;

public enum ThinTripleIconId
{
    Camera,
    Aperture,
    Shutter,
    Iso,
    Exposure,
    Zoom,
    Card,
    Gallery,
    Close,
    Focus,
    MouseLeft,
    MouseWheel,
    MouseRight
}

[CreateAssetMenu(menuName = "To The Summit/UI/Thin Triple Icon Set")]
public sealed class ThinTripleIconSet : ScriptableObject
{
    [Serializable]
    public sealed class Path
    {
        public bool closed;
        public bool filled;
        public bool repeatInLayers = true;
        public Vector2[] points;
    }

    [Serializable]
    public sealed class Icon
    {
        public ThinTripleIconId id;
        public Path[] paths;
        public Texture2D small;
        public Texture2D medium;
        public Texture2D large;
    }

    [SerializeField] Icon[] icons = Array.Empty<Icon>();

    public Icon Get(ThinTripleIconId id)
    {
        for (int i = 0; i < icons.Length; i++)
            if (icons[i].id == id) return icons[i];
        return null;
    }

#if UNITY_EDITOR
    public void PopulateDefaults()
    {
        icons = new[]
        {
            Entry(ThinTripleIconId.Camera,
                Closed(P(5, 11), P(10, 11), P(12, 8), P(20, 8), P(22, 11), P(27, 11), P(27, 26), P(5, 26)),
                Circle(16, 18, 5)),
            Entry(ThinTripleIconId.Aperture,
                Circle(16, 16, 13.33f),
                DetailOpen(P(19.08f, 10.67f), P(26.73f, 23.92f)),
                DetailOpen(P(12.92f, 10.67f), P(28.23f, 10.67f)),
                DetailOpen(P(9.84f, 16), P(17.49f, 2.75f)),
                DetailOpen(P(12.92f, 21.33f), P(5.27f, 8.08f)),
                DetailOpen(P(19.08f, 21.33f), P(3.77f, 21.33f)),
                DetailOpen(P(22.16f, 16), P(14.51f, 29.25f))),
            Entry(ThinTripleIconId.Shutter,
                Circle(16, 18.67f, 10.67f),
                DetailOpen(P(16, 18.67f), P(20, 14.67f)),
                DetailOpen(P(13.33f, 2.67f), P(18.67f, 2.67f))),
            Entry(ThinTripleIconId.Iso,
                Closed(P(7, 7), P(25, 7), P(25, 25), P(7, 25)),
                DetailClosed(P(11, 11), P(21, 11), P(21, 21), P(11, 21))),
            Entry(ThinTripleIconId.Exposure,
                Circle(16, 16, 11),
                DetailOpen(P(7, 16), P(14, 16)),
                DetailOpen(P(21, 11), P(21, 21)),
                DetailOpen(P(16, 16), P(26, 16))),
            Entry(ThinTripleIconId.Zoom,
                Circle(14, 14, 7),
                Open(P(19, 19), P(27, 27))),
            Entry(ThinTripleIconId.Card,
                Closed(P(8, 5), P(21, 5), P(25, 9), P(25, 27), P(8, 27)),
                DetailOpen(P(19, 5), P(19, 11), P(23, 11))),
            Entry(ThinTripleIconId.Gallery,
                Closed(P(5, 6), P(27, 6), P(27, 26), P(5, 26)),
                DetailOpen(P(7, 23), P(13, 16), P(17, 20), P(20, 17), P(25, 23)),
                DetailCircle(22, 11, 2)),
            Entry(ThinTripleIconId.Close,
                Open(P(8, 8), P(24, 24)),
                Open(P(24, 8), P(8, 24))),
            Entry(ThinTripleIconId.Focus,
                Open(P(5, 12), P(5, 5), P(12, 5)),
                Open(P(20, 5), P(27, 5), P(27, 12)),
                Open(P(27, 20), P(27, 27), P(20, 27)),
                Open(P(12, 27), P(5, 27), P(5, 20))),
            Entry(ThinTripleIconId.MouseLeft,
                Closed(P(16, 2.67f), P(21, 2.67f), P(25.33f, 8), P(25.33f, 21.33f), P(23, 27), P(19, 29.33f), P(13, 29.33f), P(9, 27), P(6.67f, 21.33f), P(6.67f, 8), P(11, 2.67f)),
                Filled(P(8, 13), P(8, 9), P(10.5f, 5.5f), P(15, 4.5f), P(15, 13))),
            Entry(ThinTripleIconId.MouseWheel,
                Closed(P(16, 2.67f), P(21, 2.67f), P(25.33f, 8), P(25.33f, 21.33f), P(23, 27), P(19, 29.33f), P(13, 29.33f), P(9, 27), P(6.67f, 21.33f), P(6.67f, 8), P(11, 2.67f)),
                Filled(P(14.25f, 5.5f), P(17.75f, 5.5f), P(17.75f, 13), P(14.25f, 13))),
            Entry(ThinTripleIconId.MouseRight,
                Closed(P(16, 2.67f), P(21, 2.67f), P(25.33f, 8), P(25.33f, 21.33f), P(23, 27), P(19, 29.33f), P(13, 29.33f), P(9, 27), P(6.67f, 21.33f), P(6.67f, 8), P(11, 2.67f)),
                Filled(P(17, 4.5f), P(21.5f, 5.5f), P(24, 9), P(24, 13), P(17, 13)))
        };
    }

    static Icon Entry(ThinTripleIconId id, params Path[] paths) => new() { id = id, paths = paths };
    static Path Open(params Vector2[] points) => new() { points = points };
    static Path Closed(params Vector2[] points) => new() { closed = true, points = points };
    static Path DetailOpen(params Vector2[] points) => new() { repeatInLayers = false, points = points };
    static Path DetailClosed(params Vector2[] points) =>
        new() { closed = true, repeatInLayers = false, points = points };
    static Path Filled(params Vector2[] points) =>
        new() { closed = true, filled = true, repeatInLayers = false, points = points };
    static Vector2 P(float x, float y) => new(x, y);

    static Path Circle(float x, float y, float radius)
    {
        const int segments = 24;
        Vector2[] points = new Vector2[segments];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            points[i] = new Vector2(x + Mathf.Cos(angle) * radius, y + Mathf.Sin(angle) * radius);
        }
        return Closed(points);
    }

    static Path DetailCircle(float x, float y, float radius)
    {
        Path path = Circle(x, y, radius);
        path.repeatInLayers = false;
        return path;
    }

    public void BakeTextures()
    {
        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(assetPath))
            throw new InvalidOperationException("Save the icon set asset before baking textures.");

        UnityEngine.Object[] existing = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < existing.Length; i++)
            if (existing[i] is Texture2D texture) DestroyImmediate(texture, true);

        for (int i = 0; i < icons.Length; i++)
        {
            icons[i].small = Bake(icons[i], 1, 20, icons[i].id + " 20px");
            icons[i].medium = Bake(icons[i], 2, 32, icons[i].id + " 32px");
            icons[i].large = Bake(icons[i], 3, 48, icons[i].id + " 48px");
            UnityEditor.AssetDatabase.AddObjectToAsset(icons[i].small, this);
            UnityEditor.AssetDatabase.AddObjectToAsset(icons[i].medium, this);
            UnityEditor.AssetDatabase.AddObjectToAsset(icons[i].large, this);
        }
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.AssetDatabase.SaveAssets();
    }

    static Texture2D Bake(Icon icon, int layerCount, int size, string textureName)
    {
        const int samples = 4;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = textureName,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideInHierarchy
        };
        var pixels = new Color32[size * size];
        float[] scales = { 1f, 0.75f, 0.5625f };
        float pixelToDesign = 32f / size;
        float[] widths =
        {
            pixelToDesign,
            pixelToDesign * 0.75f,
            pixelToDesign * 0.625f
        };
        float[] opacities = { 1f, 0.55f, 0.25f };

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float coverage = 0f;
            for (int sy = 0; sy < samples; sy++)
            for (int sx = 0; sx < samples; sx++)
            {
                Vector2 sample = new(
                    (x + (sx + 0.5f) / samples) * 32f / size,
                    32f - (y + (sy + 0.5f) / samples) * 32f / size);
                float alpha = 0f;
                for (int layer = 0; layer < layerCount; layer++)
                {
                    bool hit = false;
                    for (int pathIndex = 0; pathIndex < icon.paths.Length && !hit; pathIndex++)
                    {
                        Path path = icon.paths[pathIndex];
                        if (layer > 0 && !path.repeatInLayers) continue;
                        hit = path.filled
                            ? Contains(path, sample, scales[layer])
                            : Hits(path, sample, scales[layer], widths[layer] * 0.5f);
                    }
                    if (hit) alpha = 1f - (1f - alpha) * (1f - opacities[layer]);
                }
                coverage += alpha;
            }
            byte a = (byte)Mathf.RoundToInt(coverage / (samples * samples) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, a);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    static bool Hits(Path path, Vector2 sample, float scale, float halfWidth)
    {
        if (path.points == null || path.points.Length < 2) return false;
        int segments = path.closed ? path.points.Length : path.points.Length - 1;
        for (int i = 0; i < segments; i++)
        {
            Vector2 a = Scale(path.points[i], scale);
            Vector2 b = Scale(path.points[(i + 1) % path.points.Length], scale);
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0f
                ? Mathf.Clamp01(Vector2.Dot(sample - a, segment) / lengthSquared) : 0f;
            if ((sample - (a + segment * t)).sqrMagnitude <= halfWidth * halfWidth) return true;
        }
        return false;
    }

    static bool Contains(Path path, Vector2 sample, float scale)
    {
        bool inside = false;
        int count = path.points != null ? path.points.Length : 0;
        for (int i = 0, previous = count - 1; i < count; previous = i++)
        {
            Vector2 a = Scale(path.points[i], scale);
            Vector2 b = Scale(path.points[previous], scale);
            bool crosses = (a.y > sample.y) != (b.y > sample.y)
                && sample.x < (b.x - a.x) * (sample.y - a.y) / (b.y - a.y) + a.x;
            if (crosses) inside = !inside;
        }
        return inside;
    }

    static Vector2 Scale(Vector2 point, float scale) =>
        (point - Vector2.one * 16f) * scale + Vector2.one * 16f;
#endif
}
