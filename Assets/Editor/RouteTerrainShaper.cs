using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// SHAPES TERRAIN ALONG ROUTES. Drawn route paths carve and fill the terrain —
/// cutting uphill slopes, filling downhill batters, leaving flat roadbeds in between.
///
/// TWO PHASES:
///   1. PROFILE GRADING — aligns route gradient to transport limits (bus <= 10%, bike <= 12%).
///   2. CUT AND FILL — daylight slopes: cut slope 1:1 (45 deg, consolidated ground),
///      fill slope 1:1.5 (34 deg, angle of repose for loose fill).
///
/// ORDER MATTERS: Runs immediately after terrain generation before baking surface maps.
public static class RouteTerrainShaper
{
    public const int Version = 9;

    const float CutSlope = 1.0f;
    const float FillRun = 1.5f;
    const float Shoulder = 5f;
    const float RoadShoulder = 11f;
    const float SpawnClearing = 45f;
    const float TreadCut = 1.0f;
    const float MaxReach = 70f;
    const float CampShoulder = 4f;

    public static bool[] TouchedMask { get; private set; }
    public static int MaskResolution { get; private set; }

    public static void Shape(Terrain terrain, MountainRoute route)
    {
        if (terrain == null || route == null) return;

        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        TouchedMask = new bool[res * res];
        MaskResolution = res;

        float metresPerCell = data.size.x / (res - 1);
        float vertical = Mathf.Max(1f, data.size.y);
        Vector3 origin = terrain.transform.position;

        Carve(heights, res, metresPerCell, vertical, origin, terrain,
              route.road, RouteProfile.RoadGrade, RoadShoulder);

        foreach (MountainRoute.Branch branch in route.branches)
            Carve(heights, res, metresPerCell, vertical, origin, terrain,
                  branch.marks, RouteProfile.BikeGrade, Shoulder);

        if (route.spawnSet)
        {
            Vector3 spawn = MountainRoute.ToWorld(route.spawn, terrain);
            Flatten(heights, res, metresPerCell, vertical, origin,
                    spawn, SpawnClearing, CampShoulder * 2f);
        }

        foreach (MountainRoute.Mark camp in route.camps)
        {
            Vector3 centre = MountainRoute.ToWorld(camp.position, terrain);
            Flatten(heights, res, metresPerCell, vertical, origin,
                    centre, camp.radius, CampShoulder);
        }

        data.SetHeights(0, 0, heights);
        SaveMask();
        ReportResult(heights, res, metresPerCell, vertical, origin, route, terrain);
    }

    const string MaskPath = "Assets/Terrain/RouteShapeMask.png";
    const int MaskTexture = 1024;

    static void Dilate(Color32[] pixels, int radius)
    {
        var source = (Color32[])pixels.Clone();
        var red = new Color32(255, 40, 30, 255);

        for (int y = 0; y < MaskTexture; y++)
        for (int x = 0; x < MaskTexture; x++)
        {
            if (source[y * MaskTexture + x].r < 200) continue;

            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int ny = y + dy, nx = x + dx;
                if (ny < 0 || nx < 0 || ny >= MaskTexture || nx >= MaskTexture) continue;

                pixels[ny * MaskTexture + nx] = red;
            }
        }
    }

    static void SaveMask()
    {
        if (TouchedMask == null) return;

        var pixels = new Color32[MaskTexture * MaskTexture];
        int ratio = Mathf.Max(1, MaskResolution / MaskTexture);

        for (int y = 0; y < MaskTexture; y++)
        for (int x = 0; x < MaskTexture; x++)
        {
            bool touched = false;

            for (int dy = 0; dy < ratio && !touched; dy++)
            for (int dx = 0; dx < ratio && !touched; dx++)
            {
                int sz = y * ratio + dy, sx = x * ratio + dx;
                if (sz < MaskResolution && sx < MaskResolution
                    && TouchedMask[sz * MaskResolution + sx]) touched = true;
            }

            pixels[y * MaskTexture + x] = touched
                ? new Color32(255, 40, 30, 255)
                : new Color32(18, 18, 22, 255);
        }

        Dilate(pixels, 3);

        var texture = new Texture2D(MaskTexture, MaskTexture, TextureFormat.RGBA32, false);
        texture.SetPixels32(pixels);
        texture.Apply(false);

        System.IO.File.WriteAllBytes(MaskPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceUpdate);
    }

    static void ReportResult(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, MountainRoute route, Terrain terrain)
    {
        int touched = 0;
        foreach (bool cellTouched in TouchedMask) if (cellTouched) touched++;

        var report = new System.Text.StringBuilder();
        report.Append($"[Grading] touched cells: {touched}");

        if (route.spawnSet)
        {
            Vector3 spawn = MountainRoute.ToWorld(route.spawn, terrain);
            report.Append($"\n  spawn clearing ({SpawnClearing:F0} m): "
                        + Spread(heights, res, cell, vertical, origin, spawn, SpawnClearing));
        }

        if (route.road.Count > 2)
        {
            Vector3 middle = MountainRoute.ToWorld(
                route.road[route.road.Count / 2].position, terrain);
            report.Append($"\n  road corridor (14 m): "
                        + Spread(heights, res, cell, vertical, origin, middle, 14f));
        }

        ToolLog.Write(report.ToString());
    }

    static string Spread(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, Vector3 centre, float radius)
    {
        float low = float.MaxValue, high = float.MinValue;
        float n = 0f, sx = 0f, sz = 0f, sh = 0f;
        float sxx = 0f, szz = 0f, sxz = 0f, sxh = 0f, szh = 0f;

        int x0 = Mathf.Max(0, Mathf.FloorToInt((centre.x - radius - origin.x) / cell));
        int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.x + radius - origin.x) / cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((centre.z - radius - origin.z) / cell));
        int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.z + radius - origin.z) / cell));

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var point = new Vector2(origin.x + x * cell, origin.z + z * cell);
            if (Vector2.Distance(point, new Vector2(centre.x, centre.z)) > radius) continue;

            float h = heights[z, x] * vertical;
            low = Mathf.Min(low, h);
            high = Mathf.Max(high, h);

            float px = point.x - centre.x, pz = point.y - centre.z;
            n++; sx += px; sz += pz; sh += h;
            sxx += px * px; szz += pz * pz; sxz += px * pz;
            sxh += px * h; szh += pz * h;
        }

        if (low > high || n < 3f) return "no cells";

        float dxx = sxx - sx * sx / n, dzz = szz - sz * sz / n, dxz = sxz - sx * sz / n;
        float dxh = sxh - sx * sh / n, dzh = szh - sz * sh / n;
        float determinant = dxx * dzz - dxz * dxz;

        float ax = 0f, az = 0f;
        if (Mathf.Abs(determinant) > 1e-3f)
        {
            ax = (dxh * dzz - dzh * dxz) / determinant;
            az = (dzh * dxx - dxh * dxz) / determinant;
        }

        float grade = new Vector2(ax, az).magnitude;
        return $"slope {grade * 100f:F1}%, elevation range {high - low:F2} m";
    }

    static void Mark(int z, int x, int res)
    {
        if (TouchedMask != null) TouchedMask[z * res + x] = true;
    }

    static void Carve(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, Terrain terrain, List<MountainRoute.Mark> marks, float maxGrade,
        float shoulder)
    {
        if (marks.Count < 2) return;

        var points = new Vector3[marks.Count];
        for (int i = 0; i < marks.Count; i++)
        {
            Vector3 world = MountainRoute.ToWorld(marks[i].position, terrain);
            world.y = terrain.SampleHeight(world) + origin.y;
            points[i] = world;
        }

        SmoothProfile(points, 60f);
        LimitGrade(points, maxGrade);

        for (int i = 1; i < points.Length; i++)
            CarveSegment(heights, res, cell, vertical, origin,
                         points[i - 1], points[i],
                         marks[i - 1].radius, marks[i].radius, shoulder);
    }

    static void SmoothProfile(Vector3[] points, float window)
    {
        var distances = new float[points.Length];
        for (int i = 1; i < points.Length; i++)
            distances[i] = distances[i - 1]
                         + Vector2.Distance(new Vector2(points[i].x, points[i].z),
                                             new Vector2(points[i - 1].x, points[i - 1].z));

        var smoothed = new float[points.Length];

        for (int i = 0; i < points.Length; i++)
        {
            float sum = 0f, weight = 0f;

            for (int k = i; k < points.Length && distances[k] - distances[i] <= window; k++)
            {
                float w = 1f - (distances[k] - distances[i]) / window;
                sum += points[k].y * w;
                weight += w;
            }

            for (int k = i - 1; k >= 0 && distances[i] - distances[k] <= window; k--)
            {
                float w = 1f - (distances[i] - distances[k]) / window;
                sum += points[k].y * w;
                weight += w;
            }

            smoothed[i] = weight > 0f ? sum / weight : points[i].y;
        }

        for (int i = 0; i < points.Length; i++) points[i].y = smoothed[i];
    }

    static void LimitGrade(Vector3[] points, float maxGrade)
    {
        for (int pass = 0; pass < 4; pass++)
        {
            for (int i = 1; i < points.Length; i++)
                Clamp(ref points[i], points[i - 1], maxGrade);

            for (int i = points.Length - 2; i >= 0; i--)
                Clamp(ref points[i], points[i + 1], maxGrade);
        }
    }

    static void Clamp(ref Vector3 point, Vector3 anchor, float maxGrade)
    {
        float run = Vector2.Distance(new Vector2(point.x, point.z),
                                     new Vector2(anchor.x, anchor.z));
        if (run < 0.01f) return;

        float limit = run * maxGrade;
        point.y = Mathf.Clamp(point.y, anchor.y - limit, anchor.y + limit);
    }

    static void CarveSegment(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, Vector3 from, Vector3 to, float radiusFrom, float radiusTo,
        float shoulder)
    {
        float half = Mathf.Max(radiusFrom, radiusTo) + shoulder;

        var a = new Vector2(from.x, from.z);
        var b = new Vector2(to.x, to.z);

        float minX = Mathf.Min(a.x, b.x) - half - MaxReach;
        float maxX = Mathf.Max(a.x, b.x) + half + MaxReach;
        float minZ = Mathf.Min(a.y, b.y) - half - MaxReach;
        float maxZ = Mathf.Max(a.y, b.y) + half + MaxReach;

        int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - origin.x) / cell));
        int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxX - origin.x) / cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((minZ - origin.z) / cell));
        int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((maxZ - origin.z) / cell));

        Vector2 axis = b - a;
        float lengthSquared = Mathf.Max(1e-4f, axis.sqrMagnitude);

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var point = new Vector2(origin.x + x * cell, origin.z + z * cell);

            float t = Mathf.Clamp01(Vector2.Dot(point - a, axis) / lengthSquared);
            Vector2 nearest = a + axis * t;

            float distance = Vector2.Distance(point, nearest);
            float edge = Mathf.Lerp(radiusFrom, radiusTo, t) + shoulder;

            if (distance > edge + MaxReach) continue;

            float target = Mathf.Lerp(from.y, to.y, t) - TreadCut;
            float current = heights[z, x] * vertical + origin.y;

            float beyond = Mathf.Max(0f, distance - edge);
            float allowedAbove = beyond * CutSlope;
            float allowedBelow = beyond / FillRun;

            float shaped = Mathf.Clamp(current, target - allowedBelow, target + allowedAbove);

            if (Mathf.Abs(shaped - current) < 0.001f) continue;

            heights[z, x] = Mathf.Clamp01((shaped - origin.y) / vertical);
            Mark(z, x, res);
        }
    }

    static void Flatten(float[,] heights, int res, float cell, float vertical,
        Vector3 origin, Vector3 centre, float radius, float shoulder)
    {
        float half = radius + shoulder;

        int x0 = Mathf.Max(0, Mathf.FloorToInt((centre.x - half - MaxReach - origin.x) / cell));
        int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.x + half + MaxReach - origin.x) / cell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((centre.z - half - MaxReach - origin.z) / cell));
        int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((centre.z + half + MaxReach - origin.z) / cell));

        float n = 0f, sx = 0f, sz = 0f, sh = 0f;
        float sxx = 0f, szz = 0f, sxz = 0f, sxh = 0f, szh = 0f;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var point = new Vector2(origin.x + x * cell, origin.z + z * cell);
            if (Vector2.Distance(point, new Vector2(centre.x, centre.z)) > half) continue;

            float px = point.x - centre.x, pz = point.y - centre.z;
            float h = heights[z, x] * vertical + origin.y;

            n++; sx += px; sz += pz; sh += h;
            sxx += px * px; szz += pz * pz; sxz += px * pz;
            sxh += px * h; szh += pz * h;
        }

        if (n < 3f) return;

        float dxx = sxx - sx * sx / n;
        float dzz = szz - sz * sz / n;
        float dxz = sxz - sx * sz / n;
        float dxh = sxh - sx * sh / n;
        float dzh = szh - sz * sh / n;

        float determinant = dxx * dzz - dxz * dxz;
        float slopeX = 0f, slopeZ = 0f;

        if (Mathf.Abs(determinant) > 1e-3f)
        {
            slopeX = (dxh * dzz - dzh * dxz) / determinant;
            slopeZ = (dzh * dxx - dxh * dxz) / determinant;
        }

        var slope = new Vector2(slopeX, slopeZ);
        if (slope.magnitude > 0.02f) slope = slope.normalized * 0.02f;

        float level = sh / n - (slope.x * sx + slope.y * sz) / n;

        for (int z = z0; z <= z1; z++)
        for (int x = x0; x <= x1; x++)
        {
            var point = new Vector2(origin.x + x * cell, origin.z + z * cell);
            float distance = Vector2.Distance(point, new Vector2(centre.x, centre.z));
            if (distance > half + MaxReach) continue;

            float current = heights[z, x] * vertical + origin.y;
            float beyond = Mathf.Max(0f, distance - half);

            float target = level + slope.x * (point.x - centre.x)
                                 + slope.y * (point.y - centre.z);

            target = Mathf.Lerp(target, current, 0.1f);

            float shaped = Mathf.Clamp(current, target - beyond / FillRun,
                                                target + beyond * CutSlope);

            if (Mathf.Abs(shaped - current) < 0.001f) continue;

            heights[z, x] = Mathf.Clamp01((shaped - origin.y) / vertical);
            Mark(z, x, res);
        }
    }
}
