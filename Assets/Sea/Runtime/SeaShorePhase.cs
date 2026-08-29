// ROLE: bakes how long a shallow-water wave takes to reach every point from the
// waterline. Baked once, on the CPU, next to the bathymetry.
// CALLED BY: SeaManager (RefreshBathymetry).

using System;
using System.Collections.Generic;
using UnityEngine;

/// THE CREST IS A CONTOUR OF THE ARRIVAL TIME.
///
/// A shallow-water wave travels at `c = sqrt(g h)`, so its phase at a point is
/// `omega * tau`, where `tau` is the time the wave took to get there from deep
/// water. Points that share a `tau` share a phase — they are ONE CREST.
///
/// This is what makes refraction fall out instead of being applied: `tau` comes
/// from the sea bed alone, so the crests bend into bays and wrap around
/// headlands because the DEPTH does. Nothing rotates a wave vector.
///
/// TIME, NOT PHASE, IS BAKED. Phase would be `omega * tau` and `omega` moves with
/// the weather; the texture would need rebaking every time the wind changed.
/// `tau` depends only on the bathymetry.
///
/// THE EIKONAL IS SOLVED, NOT DIJKSTRA. `|grad tau| = 1/c` on a grid, by fast
/// marching. Dijkstra on an 8-neighbour grid makes paths follow the grid
/// directions and the crests come out octagonal; the upwind quadratic update
/// does not have that error.
/// [SOURCE: Sethian 1996, Fast Marching Methods]
public static class SeaShorePhase
{
    // Node states for the marching front.
    const byte Far = 0;
    const byte Band = 1;
    const byte Frozen = 2;

    /// Wave travel time from the waterline, in seconds. Land and points beyond
    /// the shallow band hold `deepTravel`.
    ///
    /// `RHalf` again: the field runs 0..~100 s and half precision resolves 0.05 s
    /// there, which is 0.03 rad of phase at a ten second swell — far below what
    /// a crest shows.
    public static Texture2D Bake(Terrain terrain, float seaLevelY, float maxDepth)
    {
        float[] tau = Field(terrain, seaLevelY, maxDepth, out int res);
        return ToTexture(tau, res);
    }

    /// The solved field itself, in seconds. Separate from the texture so a
    /// measurement can read it: `Apply(..., makeNoLongerReadable: true)` throws the
    /// CPU copy away, and keeping one alive for a 4097 texture is 33 MB for nothing.
    public static float[] Field(Terrain terrain, float seaLevelY, float maxDepth, out int resolution)
    {
        if (terrain == null)
            throw new ArgumentNullException(nameof(terrain));

        TerrainData td = terrain.terrainData;
        int res = td.heightmapResolution;
        resolution = res;
        float[,] hm = td.GetHeights(0, 0, res, res);

        float baseY = terrain.transform.position.y;
        float height = td.size.y;
        float texel = td.size.x / (res - 1);

        // SLOWNESS, 1/c. Capped at the spectrum's own depth: past it the shore
        // wave is faded out anyway and a deeper sea bed would only stretch the
        // field's range for nothing.
        float[] slowness = new float[res * res];
        float[] tau = new float[res * res];
        byte[] state = new byte[res * res];

        float far = float.MaxValue;
        for (int i = 0; i < tau.Length; i++) tau[i] = far;

        for (int y = 0; y < res; y++)
        {
            int row = y * res;
            for (int x = 0; x < res; x++)
            {
                float depth = seaLevelY - (baseY + hm[y, x] * height);
                float c = Mathf.Sqrt(SeaConstants.G * Mathf.Clamp(depth, 0.05f, maxDepth));
                slowness[row + x] = 1f / c;
            }
        }

        // THE FRONT STARTS AT THE WATERLINE. Every water texel with a land
        // neighbour is a source at tau = 0. Marching outward from there gives the
        // time a wave took to ARRIVE, which is what the phase needs, and it makes
        // the shoreline the reference for every bay at once.
        var heap = new MinHeap(1 << 16);

        for (int y = 0; y < res; y++)
        {
            int row = y * res;
            for (int x = 0; x < res; x++)
            {
                float d = seaLevelY - (baseY + hm[y, x] * height);
                if (d <= 0f) continue;

                bool onEdge = x == 0 || y == 0 || x == res - 1 || y == res - 1;
                bool touchesLand = onEdge
                    || seaLevelY - (baseY + hm[y, x - 1] * height) <= 0f
                    || seaLevelY - (baseY + hm[y, x + 1] * height) <= 0f
                    || seaLevelY - (baseY + hm[y - 1, x] * height) <= 0f
                    || seaLevelY - (baseY + hm[y + 1, x] * height) <= 0f;

                if (!touchesLand) continue;

                tau[row + x] = 0f;
                state[row + x] = Band;
                heap.Push(row + x, 0f);
            }
        }

        int[] nx = { 1, -1, 0, 0 };
        int[] ny = { 0, 0, 1, -1 };

        while (heap.Count > 0)
        {
            int idx = heap.Pop();
            if (state[idx] == Frozen) continue;
            state[idx] = Frozen;

            int cx = idx % res;
            int cy = idx / res;

            for (int k = 0; k < 4; k++)
            {
                int x = cx + nx[k];
                int y = cy + ny[k];
                if (x < 0 || y < 0 || x >= res || y >= res) continue;

                int n = y * res + x;
                if (state[n] == Frozen) continue;

                // Land is not crossed: a wave does not travel over the beach.
                if (seaLevelY - (baseY + hm[y, x] * height) <= 0f) continue;

                float t = Solve(tau, res, x, y, slowness[n], texel);
                if (t < tau[n])
                {
                    tau[n] = t;
                    state[n] = Band;
                    heap.Push(n, t);
                }
            }
        }

        // Anything the front never reached (land, enclosed pools) gets the deepest
        // value in the field, so a sample there is not a hole.
        float deepest = 0f;
        for (int i = 0; i < tau.Length; i++)
            if (tau[i] < far && tau[i] > deepest) deepest = tau[i];

        for (int i = 0; i < tau.Length; i++)
            if (tau[i] >= far) tau[i] = deepest;

        return tau;
    }

    static Texture2D ToTexture(float[] tau, int res)
    {
        var tex = new Texture2D(res, res, TextureFormat.RHalf, false, true)
        {
            name = "Tex_SeaShoreTravel",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };

        var px = new Color[res * res];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(tau[i], 0f, 0f, 1f);

        tex.SetPixels(px);
        tex.Apply(false, true);

        return tex;
    }

    /// Upwind quadratic update. `a` and `b` are the smaller arrived neighbour on
    /// each axis; if the two are further apart than one step's worth of travel the
    /// front is effectively one-dimensional here and the linear form is used.
    static float Solve(float[] tau, int res, int x, int y, float slow, float texel)
    {
        float a = Math.Min(Get(tau, res, x - 1, y), Get(tau, res, x + 1, y));
        float b = Math.Min(Get(tau, res, x, y - 1), Get(tau, res, x, y + 1));

        float step = slow * texel;

        if (a == float.MaxValue && b == float.MaxValue) return float.MaxValue;
        if (a == float.MaxValue) return b + step;
        if (b == float.MaxValue) return a + step;

        float diff = a - b;
        if (Mathf.Abs(diff) >= step) return Math.Min(a, b) + step;

        float disc = 2f * step * step - diff * diff;
        return 0.5f * (a + b + Mathf.Sqrt(Mathf.Max(disc, 0f)));
    }

    static float Get(float[] tau, int res, int x, int y)
    {
        if (x < 0 || y < 0 || x >= res || y >= res) return float.MaxValue;
        return tau[y * res + x];
    }

    /// Binary heap over grid indices. `SortedSet` and `List.Sort` were both too
    /// slow at a million nodes; the bake is one-off but it still runs while the
    /// editor is blocked.
    sealed class MinHeap
    {
        int[] items;
        float[] keys;
        int count;

        public MinHeap(int capacity)
        {
            items = new int[capacity];
            keys = new float[capacity];
        }

        public int Count => count;

        public void Push(int item, float key)
        {
            if (count == items.Length)
            {
                Array.Resize(ref items, count * 2);
                Array.Resize(ref keys, count * 2);
            }

            int i = count++;
            items[i] = item;
            keys[i] = key;

            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (keys[parent] <= keys[i]) break;
                Swap(i, parent);
                i = parent;
            }
        }

        public int Pop()
        {
            int top = items[0];
            count--;
            items[0] = items[count];
            keys[0] = keys[count];

            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, small = i;
                if (l < count && keys[l] < keys[small]) small = l;
                if (r < count && keys[r] < keys[small]) small = r;
                if (small == i) break;
                Swap(i, small);
                i = small;
            }
            return top;
        }

        void Swap(int i, int j)
        {
            (items[i], items[j]) = (items[j], items[i]);
            (keys[i], keys[j]) = (keys[j], keys[i]);
        }
    }

    /// Measurement: on a straight beach of constant slope the closed form
    /// `tau = 2 sqrt(x / (g beta))` is exact, so the solver can be checked against
    /// it. Any bay in the real bathymetry only bends the field; it does not change
    /// what a straight transect must read.
    public static string Verify(float[] tau, int res, Terrain terrain, float seaLevelY)
    {
        TerrainData td = terrain.terrainData;
        float[,] hm = td.GetHeights(0, 0, res, res);
        float baseY = terrain.transform.position.y;
        float height = td.size.y;
        float texel = td.size.x / (res - 1);

        float lo = float.MaxValue, hi = -1f;
        int water = 0;

        for (int y = 0; y < res; y += 4)
        for (int x = 0; x < res; x += 4)
        {
            if (seaLevelY - (baseY + hm[y, x] * height) <= 0f) continue;
            water++;
            float t = tau[y * res + x];
            if (t < lo) lo = t;
            if (t > hi) hi = t;
        }

        return $"travel {res}x{res}   water texels sampled {water}"
             + $"   tau {lo:F2} .. {hi:F1} s   (texel {texel:F2} m)";
    }
}
