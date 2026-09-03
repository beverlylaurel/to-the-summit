// ROLE: quality tiers and the numbers behind each one. Spec 15.3 table.
// CALLED BY: SeaSimulation, SeaSurface, SeaManager.

using UnityEngine;

public enum SeaQualityPreset { Low, Medium, High }

/// THE SPEC 15.3 TABLE, IN ONE PLACE.
///
/// If the numbers lived in two places (one in SeaSimulation, one in
/// SeaSurface) one of them would change and the other would not, and the
/// mesh would end up at a different quality than the wave field.
public static class SeaQuality
{
    public readonly struct Levels
    {
        /// FFT grid size. `SeaConstants.FftSize` is its UPPER BOUND — the
        /// compute shader's `numthreads` comes from there and never changes;
        /// this is the texture size and the length of the transform.
        public readonly int FftSize;
        public readonly int FftLog2;

        /// How many tiers are computed. The weight of an unused tier is zero
        /// and its dispatch never happens.
        public readonly int TierCount;

        public readonly int RingCount;
        public readonly float FinestQuad;

        public Levels(int fftSize, int fftLog2, int tierCount,
                      int ringCount, float finestQuad)
        {
            FftSize = fftSize;
            FftLog2 = fftLog2;
            TierCount = tierCount;
            RingCount = ringCount;
            FinestQuad = finestQuad;
        }
    }

    /// | Setting       | Low  | Medium | High |
    /// | FFT           | 128  | 256    | 256  |
    /// | Tiers         | 2    | 3      | 3    |
    /// | Ring 0 quad   | 1.0  | 0.5    | 0.25 |
    /// | Rings         | 6    | 7      | 8    |
    ///
    /// THE RING COUNT DEVIATES FROM THE SPEC TABLE — MEASURED.
    ///
    /// Spec §15.3 gives 5/7/7. This project's mesh is a single grid (the
    /// deviation §10.1 allows) and its outer radius is
    /// `64 · quad · (2^rings − 1)`. Applying the table verbatim gives:
    ///
    ///   Low  (1.00 m, 5 rings) -> 1984 m
    ///   High (0.25 m, 7 rings) -> 2032 m
    ///
    /// Neither reaches the horizon; the sea would end with a cut edge two
    /// kilometres out (spec §10.6 check 6 catches exactly this). The ring
    /// count was first chosen to keep the radius near 4 km.
    ///
    /// FOUR KILOMETRES WAS SIZED FOR EYE LEVEL, AND THE PLAYER CLIMBS.
    /// The horizon is `3.57 * sqrt(h)` km:
    ///
    ///   2 m (standing)  ->   5 km  -- 4 km is enough only here
    ///   45 m (the base) ->  24 km
    ///   839 m           -> 103 km  -- the sea showed as a lake with a cut edge
    ///   6028 m (summit) -> 277 km  -- and from higher, as a plain SQUARE
    ///
    /// The square is the mesh itself: a clipmap centred on the camera. Cost is
    /// not the reason to keep it small -- ring 0 is 32768 triangles and every
    /// ring after it costs a FIXED 24576 while DOUBLING the radius:
    ///
    ///   Med (0.50 m,  7 rings) ->   2 km
    ///   Med (0.50 m, 11 rings) ->  33 km
    ///   Med (0.50 m, 13 rings) -> 131 km
    ///
    /// IT NO LONGER STOPS AT 4 km. The radius is `64 * quad * 2^(rings-1)`, and the ring
    /// counts below put it at 131 km on Medium -- past the camera's far plane (map x 3 =
    /// 90 km), so the far plane clips the sea and the mesh's own edge is never reached.
    ///
    /// THE PRICE IS THE FAR EDGE, AND IT IS NOT THE CLOUDS.
    ///
    /// The clouds were the first suspect and they are innocent: the cloud pass was moved
    /// to `AfterRenderingTransparents` for exactly this reason, and crossing sea on/off
    /// with clouds on/off shows the deck occluding the water correctly wherever it is
    /// thick (110.4 with clouds, 142.9 without). Where it is thin the water shows through,
    /// which is what should happen.
    ///
    /// What is left is the cut itself. The far plane ends the sea at 90 km while the
    /// horizon from the summit is 277 km, and the step is one pixel wide across the whole
    /// frame -- row 331 has no water, row 332 is all water, luma 121 -> 143. Aerial
    /// perspective should have swallowed the surface long before that; the height fog does
    /// not saturate over a 90 km slant path (`SYMPTOMS.md`).
    ///
    /// The triangle counts do not match the spec's 180k/480k/900k either: in
    /// a single grid the triangle count depends only on the RING count, not
    /// on the quad size. The quad size decides how closely things are
    /// resolved.
    public static Levels Of(SeaQualityPreset preset)
    {
        switch (preset)
        {
            case SeaQualityPreset.Low:    return new Levels(128, 7, 2, 12, 1.00f);
            case SeaQualityPreset.High:   return new Levels(256, 8, 4, 14, 0.25f);
            default:                      return new Levels(256, 8, 3, 13, 0.50f);
        }
    }

    /// Outer radius of the mesh (m). Ring 0 is a solid square, each ring
    /// carries quads twice the size of the previous one, and there are
    /// `SeaMeshBuilder.QuadPerSide` quads per side.
    public static float OuterRadius(SeaQualityPreset preset)
    {
        Levels l = Of(preset);
        return (SeaMeshBuilder.QuadPerSide / 2) * l.FinestQuad * (1 << (l.RingCount - 1));
    }

    /// THE KEYWORD MUST MATCH A `multi_compile`.
    ///
    /// A keyword enabled with `Shader.EnableKeyword` but not declared as
    /// `#pragma multi_compile` in the shader means the variant is NEVER
    /// compiled and `#if defined(...)` silently stays false. In the snow
    /// system three detail layers never ran for exactly this reason.
    public static string Keyword(SeaQualityPreset preset)
    {
        switch (preset)
        {
            case SeaQualityPreset.Low:  return "_SEA_QUALITY_LOW";
            case SeaQualityPreset.High: return "_SEA_QUALITY_HIGH";
            default:                    return "_SEA_QUALITY_MEDIUM";
        }
    }

    static readonly string[] AllKeywords =
    {
        "_SEA_QUALITY_LOW", "_SEA_QUALITY_MEDIUM", "_SEA_QUALITY_HIGH",
    };

    /// Enables the global keyword and disables the others.
    public static void Apply(SeaQualityPreset preset)
    {
        string wanted = Keyword(preset);

        foreach (string k in AllKeywords)
        {
            if (k == wanted) Shader.EnableKeyword(k);
            else Shader.DisableKeyword(k);
        }
    }
}
