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
    /// count was chosen to keep the radius near 4 km:
    ///
    ///   Low  (1.00 m, 6 rings) -> 4032 m
    ///   Med  (0.50 m, 7 rings) -> 4064 m
    ///   High (0.25 m, 8 rings) -> 4080 m
    ///
    /// The triangle counts do not match the spec's 180k/480k/900k either: in
    /// a single grid the triangle count depends only on the RING count, not
    /// on the quad size. The quad size decides how closely things are
    /// resolved.
    public static Levels Of(SeaQualityPreset preset)
    {
        switch (preset)
        {
            case SeaQualityPreset.Low:    return new Levels(128, 7, 2, 6, 1.00f);
            case SeaQualityPreset.High:   return new Levels(256, 8, 3, 8, 0.25f);
            default:                      return new Levels(256, 8, 3, 7, 0.50f);
        }
    }

    /// Outer radius of the mesh (m). Ring 0 is a solid square, each ring
    /// carries quads twice the size of the previous one, and there are
    /// `SeaMeshBuilder.QuadPerSide` quads per side.
    public static float OuterRadius(SeaQualityPreset preset)
    {
        Levels l = Of(preset);
        return (SeaMeshBuilder.QuadPerSide / 2) * l.FinestQuad * ((1 << l.RingCount) - 1);
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
