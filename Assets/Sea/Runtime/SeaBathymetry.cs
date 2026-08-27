// ROLE: derives the water depth field from terrain height. Baked once.
// CALLED BY: SeaManager (Awake and RefreshBathymetry).

using System;
using UnityEngine;

/// WATER DEPTH IS BAKED ONCE ON THE CPU.
///
/// `terrainData.heightmapTexture` is not sampled directly in a shader: the
/// scaling constants change between Unity versions (spec §9). Baking once on
/// the CPU removes that uncertainty.
///
/// Shoaling (§8.1), breaking (§8.3) and shore damping (§8.4) all need a depth
/// per texel; every one of them reads this texture.
public static class SeaBathymetry
{
    /// Water depth texture. `>0` water, `<0` land.
    ///
    /// `RHalf` is enough: the depth range is 0–200 m and half precision
    /// resolves finer than 0.1 m there. `Float` would be twice the bandwidth
    /// with no visual difference (spec §15.2).
    public static Texture2D Bake(Terrain terrain, float seaLevelY)
    {
        if (terrain == null)
            throw new ArgumentNullException(nameof(terrain));

        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;

        // GetHeights returns [y, x] ORDERED — mind the index order (spec §9).
        float[,] hm = td.GetHeights(0, 0, res, res);

        var tex = new Texture2D(res, res, TextureFormat.RHalf, false, true)
        {
            name = "Tex_SeaBathymetry",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };

        var px = new Color[res * res];
        float baseY = terrain.transform.position.y;
        float height = td.size.y;

        for (int y = 0; y < res; y++)
        {
            int row = y * res;

            for (int x = 0; x < res; x++)
            {
                float elevation = baseY + hm[y, x] * height;

                // Texture pixel (x, y) <- depth. The heightmap was read as
                // [y, x], so the row corresponds to the Z axis and lines up
                // one to one with UV.
                px[row + x] = new Color(seaLevelY - elevation, 0f, 0f, 1f);
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, true);

        return tex;
    }

    /// Measurement: verifies the texture holds the expected values. Three
    /// known points are enough — land, water, outside the terrain.
    public static string Verify(Terrain terrain, float seaLevelY, Texture2D tex)
    {
        TerrainData td = terrain.terrainData;
        Vector3 o = terrain.transform.position;

        int res = td.heightmapResolution;
        float[,] hm = td.GetHeights(0, 0, res, res);

        // Lowest and highest point
        float lowest = float.MaxValue, highest = float.MinValue;

        for (int y = 0; y < res; y += 8)
            for (int x = 0; x < res; x += 8)
            {
                float elevation = o.y + hm[y, x] * td.size.y;
                if (elevation < lowest) lowest = elevation;
                if (elevation > highest) highest = elevation;
            }

        int water = 0, total = 0;

        for (int y = 0; y < res; y += 8)
            for (int x = 0; x < res; x += 8)
            {
                total++;
                if (o.y + hm[y, x] * td.size.y < seaLevelY) water++;
            }

        return $"bathymetry {res}x{res} | sea level {seaLevelY:F1} m\n" +
               $"  terrain elevation {lowest:F1} .. {highest:F1} m\n" +
               $"  deepest water {seaLevelY - lowest:F1} m\n" +
               $"  area below water {100f * water / total:F1}%";
    }
}
