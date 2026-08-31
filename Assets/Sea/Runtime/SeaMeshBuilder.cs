// ROLE: builds the single-grid mesh of the sea surface. Once at start-up.
// CALLED BY: SeaSurface (Awake).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// ONE GRID, ONE DRAW CALL — NOT A MULTI-LEVEL CLIPMAP.
///
/// A geometry clipmap would have needed **all** of these parts from
/// `[SOURCE: Asirvatham & Hoppe, GPU Gems 2 Chapter 2]`, none of them
/// skippable: an odd grid size, 12 blocks (`m = (n+1)/4`), four `m×3` fix-up
/// strips, four oriented L-trims, a degenerate triangle skirt and
/// `alpha = max(ax, ay)` transition blending. Miss one and the mesh tears,
/// opens holes or shimmers.
///
/// Instead: one continuous mesh. Quads near the centre are small and grow in
/// power-of-two steps with distance.
///
/// **ALIGNMENT PROOF (spec §10.1).** Every quad size is a power-of-two
/// multiple of the finest quad size. Therefore a SINGLE snap step equal to
/// the finest quad size keeps every ring's vertices on its own lattice. No
/// per-level snap is needed, so no inter-level drift is possible.
public static class SeaMeshBuilder
{
    /// Quads per ring (along one edge). Ring 0 is a solid square, the rest
    /// are rings. The spec §10.2 table follows from this number:
    ///   ring 0: 0.5 m quads, 128x128 -> 32 m radius
    ///   ring 1: 1.0 m quads, a ring  -> 96 m
    ///   ...each ring exactly twice the previous one
    public const int QuadPerSide = 128;

    public static Mesh Build(float finestQuad, int ringCount)
    {
        var vertices = new List<Vector3>(300000);
        var indices = new List<int>(1200000);

        // For vertex sharing: world position to index. Vertices ARE SHARED
        // across rings — which makes T-junctions, and therefore seams,
        // structurally impossible (spec §10.2).
        var lookup = new Dictionary<long, int>(400000);

        // --- Ring 0: solid square ---
        float q0 = finestQuad;
        int half0 = QuadPerSide / 2;

        for (int z = -half0; z < half0; z++)
            for (int x = -half0; x < half0; x++)
                AddQuad(vertices, indices, lookup,
                        x * q0, z * q0, q0, q0);

        // --- Rings 1..N: each with quads twice the previous size ---
        float innerRadius = half0 * q0;

        for (int ring = 1; ring < ringCount; ring++)
        {
            float q = finestQuad * (1 << ring);

            // Outer radius of this ring: inner radius + QuadPerSide/2 x quad
            int step = QuadPerSide / 2;
            float outerRadius = innerRadius + step * q;

            // The ring surrounding the inner square: outer square minus inner.
            int outerSteps = Mathf.RoundToInt(outerRadius / q);
            int innerSteps = Mathf.RoundToInt(innerRadius / q);

            for (int z = -outerSteps; z < outerSteps; z++)
                for (int x = -outerSteps; x < outerSteps; x++)
                {
                    // The inner square does not belong to this ring — the
                    // previous ring already drew it.
                    if (x >= -innerSteps && x < innerSteps &&
                        z >= -innerSteps && z < innerSteps)
                        continue;

                    AddQuad(vertices, indices, lookup, x * q, z * q, q, q);
                }

            innerRadius = outerRadius;
        }

        var mesh = new Mesh
        {
            name = "SeaSurfaceGrid",

            // The vertex count exceeds 65535 (spec §10.2).
            indexFormat = IndexFormat.UInt32,
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(indices, 0);

        // NO NORMALS AND NO TANGENTS. The normal comes from the FFT slope
        // texture in the fragment shader (spec §10.5); carrying it in the
        // mesh would be wasted bandwidth.
        mesh.RecalculateBounds();

        // THE BOUNDS ARE WIDENED BY HAND. Displacement happens in the vertex
        // shader, so the CPU does not know the real extents; left narrow the
        // sea disappears depending on camera angle (spec §10.2, §18 pitfall
        // table).
        float halfSize = innerRadius * 2f;
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(halfSize, 400f, halfSize));

        mesh.UploadMeshData(false);

        return mesh;
    }

    /// Adds one quad as two triangles. Corners are shared: a vertex at a
    /// given world position is created once and its index reused.
    static void AddQuad(List<Vector3> vertices, List<int> indices,
                        Dictionary<long, int> lookup,
                        float x, float z, float w, float d)
    {
        int a = VertexIndex(vertices, lookup, x, z);
        int b = VertexIndex(vertices, lookup, x + w, z);
        int c = VertexIndex(vertices, lookup, x + w, z + d);
        int e = VertexIndex(vertices, lookup, x, z + d);

        indices.Add(a); indices.Add(e); indices.Add(b);
        indices.Add(b); indices.Add(e); indices.Add(c);
    }

    /// Vertex index from a world position. Asking for the same position a
    /// second time returns the existing index — this is where vertex sharing
    /// happens.
    ///
    /// The key is a pair of integers rounded to millimetres: integers rather
    /// than a floating point comparison, because the quad sizes are
    /// power-of-two multiples and no summation error accumulates.
    static int VertexIndex(List<Vector3> vertices, Dictionary<long, int> lookup,
                           float x, float z)
    {
        long ix = Mathf.RoundToInt(x * 1000f);
        long iz = Mathf.RoundToInt(z * 1000f);
        long key = (ix << 32) ^ (iz & 0xFFFFFFFFL);

        if (lookup.TryGetValue(key, out int idx)) return idx;

        idx = vertices.Count;
        vertices.Add(new Vector3(x, 0f, z));
        lookup[key] = idx;

        return idx;
    }
}
