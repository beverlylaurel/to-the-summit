using UnityEditor;
using UnityEngine;

/// FITS RIM TO CIRCLE. In generated front wheel, a 40-degree arc had 12 mm bulge,
/// hopping once per revolution during roll. Pivot was verified correct (1 mm),
/// establishing mesh geometry as the cause.
///
/// Correction applied ONLY RADIALLY: vertex angle, axial position, and thickness remain unchanged.
/// Measured outer profile is normalized against mean radius and vertices are scaled inward/outward.
/// Maximum correction is 3.3%, average 0.6% — easily absorbed by tire profile.
///
/// HUB AND SPOKES UNTOUCHED: correction weight fades to zero toward inner rim.
/// Scaling entire mesh would deform hub hole oval and distort spokes.
///
/// Output written to asset file and excluded from git; generated assets are rebuilt via editor menus.
public static class WheelRounding
{
    const string Folder = "Assets/Models/Bike/Generated";

    /// Wheels below this deviation threshold are untouched (meters). Rear wheel has 0.9 mm deviation
    /// and is already circular; rounding would create redundant mesh assets without visual benefit.
    const float Threshold = 0.0015f;

    /// Inner boundary where correction begins: as fraction of outer profile radius.
    /// Vertices below (hub, spokes, braking surface) remain completely static.
    const float Inner = 0.7f;

    /// Generated meshes (rounded rim, zoned handlebar/rack/pedals) are generated once on disk.
    /// If zoning boundaries or correction settings change, existing assets must be rebuilt.
    ///
    /// NOTE: Manually painted material masks reside within these meshes; resetting deletes paint masks.
    [MenuItem("To The Summit/Model/Bicycle/Reset Generated Meshes", false, 125)]
    static void Reset()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            ToolLog.Write("[Bicycle] No generated meshes found.");
            return;
        }

        AssetDatabase.DeleteAsset(Folder);
        ToolLog.Write("[Bicycle] Generated meshes deleted (including paint masks); will regenerate during setup.");
    }

    /// Corrects wheel mesh and returns rounded mesh. If runout is below threshold, returns source mesh untouched.
    public static Mesh Round(Mesh source, Transform space, Vector3 axis,
        string assetName, string label)
    {
        // Asset filename derived from part name, not mesh name: generated models may share mesh names,
        // which would cause second wheel to overwrite first wheel's asset.
        string path = $"{Folder}/{assetName}_Round.asset";

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        WheelProfile profile = WheelProfile.Measure(source, space, axis);

        if (profile.Deviation < Threshold)
        {
            ToolLog.Write($"[Wheel] {label} already circular "
                    + $"({profile.Deviation * 1000f:F1} mm deviation) — skipped.");
            return source;
        }

        Mesh rounded = Correct(source, space, profile);
        WheelProfile check = WheelProfile.Measure(rounded, space, axis);

        // DO NOT APPLY IF CORRECTION WORSENS DEVIATION. When measurement is noisy, correction
        // imprints noise onto surface; previously doubled deviation. Verifying prevents unchecked degradation.
        if (check.Deviation >= profile.Deviation)
        {
            Debug.LogWarning($"[Wheel] {label} correction not applied: deviation "
                + $"{profile.Deviation * 1000f:F1} mm -> {check.Deviation * 1000f:F1} mm, "
                + "indicating noisy measurement.");
            return source;
        }

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Models/Bike", "Generated");

        AssetDatabase.CreateAsset(rounded, path);
        AssetDatabase.SaveAssets();

        ToolLog.Write($"[Wheel] {label} fitted to circle.\n"
            + $"  deviation {profile.Deviation * 1000f:F1} mm -> {check.Deviation * 1000f:F1} mm\n"
            + $"  max - min spread {(profile.Max - profile.Min) * 1000f:F0} mm -> "
            + $"{(check.Max - check.Min) * 1000f:F0} mm\n"
            + $"  radius {check.Radius:F3} m, width {check.Width * 1000f:F0} mm");

        return rounded;
    }

    static Mesh Correct(Mesh source, Transform space, WheelProfile profile)
    {
        // Measurement in world space, vertices in mesh space: vertices transform to world,
        // receive correction, and transform back to local space. Correcting in mesh space
        // would ignore the 100x part transform scaling.
        Matrix4x4 toWorld = space.localToWorldMatrix;
        Matrix4x4 toLocal = space.worldToLocalMatrix;

        Vector3[] vertices = source.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 world = toWorld.MultiplyPoint3x4(vertices[i]);
            Vector3 offset = world - profile.Centre;

            float along = Vector3.Dot(offset, profile.Axis);
            float x = Vector3.Dot(offset, profile.Right);
            float y = Vector3.Dot(offset, profile.Up);

            float radius = Mathf.Sqrt(x * x + y * y);
            if (radius < 1e-5f) continue;

            float measured = profile.RadiusAt(Mathf.Atan2(y, x));
            if (measured < 1e-5f) continue;

            // Smoothly blend correction weight toward outer rim edge.
            float t = Mathf.Clamp01((radius / measured - Inner) / (1f - Inner));
            float weight = t * t * (3f - 2f * t);

            float scale = Mathf.Lerp(1f, profile.Radius / measured, weight);
            float target = radius * scale;

            vertices[i] = toLocal.MultiplyPoint3x4(profile.Centre + profile.Axis * along
                        + (profile.Right * x + profile.Up * y) / radius * target);
        }

        var mesh = new Mesh { name = source.name };
        if (vertices.Length > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);
        mesh.SetTriangles(source.triangles, 0);

        // Normals retained from SOURCE: correction shifts surface by a few percent;
        // recalculating would destroy custom shading smoothing, faceting the rim.
        Vector3[] normals = source.normals;
        if (normals.Length == vertices.Length) mesh.SetNormals(normals);

        Vector2[] uv = source.uv;
        if (uv.Length == vertices.Length) mesh.SetUVs(0, uv);

        // Zeroed vertex colors initialized to avoid shader defaulting missing stream to white.
        mesh.SetColors(new Color32[vertices.Length]);
        mesh.SetUVs(1, new Vector2[vertices.Length]);

        mesh.RecalculateBounds();
        return mesh;
    }
}
