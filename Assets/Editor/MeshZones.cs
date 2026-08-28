using System;
using System.Collections.Generic;
using UnityEngine;

/// SPLITS A SINGLE MESH INTO ZONES. In generated models, distinct materials arrive in a
/// single mesh: handlebar, grips, and cables in one piece; luggage rack, chain guard, and
/// pedals in one piece. Materials cannot be assigned because a renderer takes one material per submesh.
///
/// SUB-MESHES INSTEAD OF SEPARATE MESHES. Cutting the mesh in two would duplicate vertices
/// and double memory; sub-meshes only partition the triangle list while vertices remain shared.
/// The renderer can assign distinct materials per sub-mesh.
///
/// ZONE BOUNDARIES SPECIFIED AS RATIOS, not meters: model files mix meters and centimeters
/// (mesh data at 1/100 scale, transform with 100x scale). Normalizing boundaries against
/// the part's bounding box eliminates unit ambiguity.
public static class MeshZones
{
    /// Partitions triangles by zone index and assigns each zone to a separate sub-mesh.
    /// Classification checks triangle BARYCENTER: checking vertices placed boundary
    /// triangles into both zones, leaving holes in both.
    public static Mesh Build(Mesh source, Func<Vector3, int> zoneOf, int zones, string name)
    {
        Vector3[] vertices = source.vertices;
        int[] triangles = source.triangles;

        var buckets = new List<int>[zones];
        for (int i = 0; i < zones; i++) buckets[i] = new List<int>();

        for (int t = 0; t < triangles.Length; t += 3)
        {
            Vector3 centre = (vertices[triangles[t]]
                            + vertices[triangles[t + 1]]
                            + vertices[triangles[t + 2]]) / 3f;

            int zone = Mathf.Clamp(zoneOf(centre), 0, zones - 1);

            buckets[zone].Add(triangles[t]);
            buckets[zone].Add(triangles[t + 1]);
            buckets[zone].Add(triangles[t + 2]);
        }

        var mesh = new Mesh { name = name };
        if (vertices.Length > 65535)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        mesh.SetVertices(vertices);

        Vector3[] normals = source.normals;
        if (normals.Length == vertices.Length) mesh.SetNormals(normals);

        Vector2[] uv = source.uv;
        if (uv.Length == vertices.Length) mesh.SetUVs(0, uv);

        // VERTEX COLORS CLEARED. Generated models carry vertex colors and shaders read them
        // as manually painted material masks; if carried over, unpainted surfaces would appear
        // painted by default. Painting is applied on top of this clean canvas.
        mesh.SetColors(new Color32[vertices.Length]);

        // Secondary UV channel for slot index is initialized empty: zoned parts are
        // painted manually later, and without the channel the brush has nowhere to write.
        mesh.SetUVs(1, new Vector2[vertices.Length]);

        mesh.subMeshCount = zones;
        for (int i = 0; i < zones; i++) mesh.SetTriangles(buckets[i], i);

        mesh.RecalculateBounds();
        return mesh;
    }

    /// Point elevation within part bounds (0 bottom, 1 top). Mesh data arrives in file coordinates,
    /// where Z is up.
    public static float Height(Bounds bounds, Vector3 point) =>
        Mathf.InverseLerp(bounds.min.z, bounds.max.z, point.z);
}
