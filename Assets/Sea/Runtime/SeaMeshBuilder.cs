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

            // EACH RING DOUBLES THE RADIUS AND IS EXACT ON ITS OWN LATTICE.
            //
            // The steps used to be derived with `RoundToInt(innerRadius / q)`,
            // and that ratio stops being a whole number at ring 7: the inner
            // square was taken as 4096 m while the ring inside it ended at
            // 4064 m, so a 32 m strip belonged to NO ring. It read as a square
            // outline around the camera, 8.1 km across -- and it only appeared
            // once the ring count went past 7, because before that the fraction
            // never came up.
            //
            // Written this way the ring always runs from half its outer radius
            // to its outer radius, so both numbers are whole in units of q and
            // no rounding is involved at all.
            const int innerSteps = QuadPerSide / 4;
            const int outerSteps = QuadPerSide / 2;
            float outerRadius = outerSteps * q;

            for (int z = -outerSteps; z < outerSteps; z++)
                for (int x = -outerSteps; x < outerSteps; x++)
                {
                    // The inner square does not belong to this ring — the
                    // previous ring already drew it.
                    if (x >= -innerSteps && x < innerSteps &&
                        z >= -innerSteps && z < innerSteps)
                        continue;

                    // WHICH EDGES FACE THE FINER RING. Those are the ones that
                    // need splitting: the ring inside steps at half this quad's
                    // size, so it leaves a vertex in the middle of the shared
                    // edge that this quad would otherwise ignore.
                    bool zInside = z >= -innerSteps && z < innerSteps;
                    bool xInside = x >= -innerSteps && x < innerSteps;

                    AddQuad(vertices, indices, lookup, x * q, z * q, q, q,
                            splitMinusX: x == innerSteps && zInside,
                            splitPlusX:  x == -innerSteps - 1 && zInside,
                            splitMinusZ: z == innerSteps && xInside,
                            splitPlusZ:  z == -innerSteps - 1 && xInside);
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

    /// Adds one quad. Corners are shared: a vertex at a given world position is
    /// created once and its index reused.
    ///
    /// THE SPLIT FLAGS ARE WHAT STITCHES THE RINGS. Sharing corner vertices does
    /// NOT close a 2:1 edge, whatever the alignment note above says -- measured on
    /// the ring 7 / ring 8 boundary, vertices sit 32 m apart along a line whose
    /// outer quads are 64 m wide, so every outer edge skipped the vertex in its
    /// middle. The wave lifts that vertex off the straight edge and the seam opens
    /// as a bright square around the camera, which is exactly what was seen.
    ///
    /// A split edge gets its middle vertex back and the quad is woven from its own
    /// centre, so the two sides share every vertex along the seam. Only the quads
    /// on a boundary pay for it.
    static void AddQuad(List<Vector3> vertices, List<int> indices,
                        Dictionary<long, int> lookup,
                        float x, float z, float w, float d,
                        bool splitMinusX = false, bool splitPlusX = false,
                        bool splitMinusZ = false, bool splitPlusZ = false)
    {
        if (!splitMinusX && !splitPlusX && !splitMinusZ && !splitPlusZ)
        {
            int a = VertexIndex(vertices, lookup, x, z);
            int b = VertexIndex(vertices, lookup, x + w, z);
            int c = VertexIndex(vertices, lookup, x + w, z + d);
            int e = VertexIndex(vertices, lookup, x, z + d);

            indices.Add(a); indices.Add(e); indices.Add(b);
            indices.Add(b); indices.Add(e); indices.Add(c);
            return;
        }

        // The outline, walked once, with a middle vertex inserted on every side
        // that faces the finer ring.
        var outline = new List<int>(8);
        outline.Add(VertexIndex(vertices, lookup, x, z));
        if (splitMinusZ) outline.Add(VertexIndex(vertices, lookup, x + w * 0.5f, z));
        outline.Add(VertexIndex(vertices, lookup, x + w, z));
        if (splitPlusX) outline.Add(VertexIndex(vertices, lookup, x + w, z + d * 0.5f));
        outline.Add(VertexIndex(vertices, lookup, x + w, z + d));
        if (splitPlusZ) outline.Add(VertexIndex(vertices, lookup, x + w * 0.5f, z + d));
        outline.Add(VertexIndex(vertices, lookup, x, z + d));
        if (splitMinusX) outline.Add(VertexIndex(vertices, lookup, x, z + d * 0.5f));

        int centre = VertexIndex(vertices, lookup, x + w * 0.5f, z + d * 0.5f);

        // Wound the other way round from the walk, to match the two-triangle case
        // above: that one faces +Y and a fan following the walk would face -Y.
        for (int i = 0; i < outline.Count; i++)
        {
            int j = i + 1 == outline.Count ? 0 : i + 1;
            indices.Add(centre); indices.Add(outline[j]); indices.Add(outline[i]);
        }
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
