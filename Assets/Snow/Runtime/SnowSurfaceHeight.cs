// ROLE: gives the snow surface's height on the CPU. The exact twin of
// `SnowYuzeyRolyef` inside `SnowRelief.hlsl`.
// CALLED BY: GroundSnap (seats the character on the surface).

using UnityEngine;

/// THE VISUAL AND THE PHYSICS HAVE TO SEE THE SAME SURFACE.
///
/// The snow height was once put into the geometry and reverted because it had no
/// counterpart on the physics side: "the foot at 205.539, the rock at 205.489, the drawn
/// surface at 205.98 — the character started half a metre buried" (the
/// `MountainSurface.shader` comment). This class closes that gap.
///
/// THE DUPLICATION IS DELIBERATE AND TESTED. The same formula is written twice in two
/// languages; the divergence is caught by `SnowHeightParityTest`. The alternatives are
/// worse: an async readback from the GPU is a frame late (the character stands on last
/// frame's surface), and a synchronous read stalls the pipeline and blows up the frame time.
///
/// CO-OP: the function is PURE. Its inputs are only the world position, the snow depth, the
/// wind direction and the exposure; there is NO frame counter, no `Time` and no local
/// randomness. So every client computes the same height at the same XZ and there is no need
/// to share heights over the network. The rule is written in `COOP.md` and cannot be broken.
///
/// THE DIAGNOSTIC SWITCHES ARE NOT READ. `_SnowDbgNoFbm` and its siblings are for visual
/// diagnosis only; had the physics seen them, a player turning the switch on would fall
/// through the ground.
public static class SnowSurfaceHeight
{
    // --- PCG3D hash: the twin of `SnowCommon.hlsl` → `SnowPcg3d` ---
    //
    // [SOURCE: Jarzynski & Olano, JCGT 2020, "Hash Functions for GPU
    // Rendering".] `frac(sin(dot(p,k)))` collapses on large inputs; an integer
    // hash has no such limit.
    static void Pcg3d(ref uint x, ref uint y, ref uint z)
    {
        unchecked
        {
            x = x * 1664525u + 1013904223u;
            y = y * 1664525u + 1013904223u;
            z = z * 1664525u + 1013904223u;

            x += y * z; y += z * x; z += x * y;

            x ^= x >> 16; y ^= y >> 16; z ^= z >> 16;

            x += y * z; y += z * x; z += x * y;
        }
    }

    /// The twin of `SnowRandCell3(int3(cx, cy, 0)).x` — only the first component is used,
    /// the others have to be computed because the mixing stages tie all three together.
    static float RandCell(int cx, int cy)
    {
        // `asuint`: int bitlerini uint olarak yeniden yorumla.
        uint x = unchecked((uint)cx);
        uint y = unchecked((uint)cy);
        uint z = 0u;

        Pcg3d(ref x, ref y, ref z);

        return x * (1.0f / 4294967296.0f);
    }

    /// `SnowCommon.hlsl` → `SnowValueNoise` ikizi.
    static float ValueNoise(float px, float py)
    {
        float hx = Mathf.Floor(px);
        float hy = Mathf.Floor(py);

        float fx = px - hx;
        float fy = py - hy;

        fx = fx * fx * (3.0f - 2.0f * fx);
        fy = fy * fy * (3.0f - 2.0f * fy);

        int ix = (int)hx;
        int iy = (int)hy;

        float a = RandCell(ix,     iy);
        float b = RandCell(ix + 1, iy);
        float c = RandCell(ix,     iy + 1);
        float d = RandCell(ix + 1, iy + 1);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// The twin of `SnowRelief.hlsl` → `SnowOktavAgirligiKipli`.
    ///
    /// On the CPU `pikselBoyu` is zero: the sampling frequency is infinite and there is no
    /// Nyquist cut. The geometry threshold is still applied — the physics surface has to be
    /// the same as the GEOMETRIC surface, not the fine octaves in the normal map.
    static float OktavAgirligi(float dalgaBoyu, float pikselBoyu, bool yalnizGeometri)
    {
        if (yalnizGeometri && dalgaBoyu < SnowConstants.TessMinDalga) return 0f;

        return Mathf.Clamp01(dalgaBoyu / Mathf.Max(pikselBoyu * 2.0f, 1e-5f) - 1.0f);
    }

    /// The twin of `SnowRelief.hlsl` → `SnowYuzeyRolyef`.
    ///
    /// The order has to be the same as in HLSL: ceiling → fBm four octaves → ripple
    /// → sastrugi → drift. In floating point the order of summation changes the result.
    public static float Rolyef(Vector2 worldXZ, float karDerinligi,
                               Vector2 sastrugiWindDir, float maruziyet,
                               float pikselBoyu = 0f, bool yalnizGeometri = true)
    {
        float tavan = karDerinligi * SnowConstants.BedformDepthFrac;

        float sastrugiPay = maruziyet;
        float driftPay    = 1f - maruziyet;

        // --- the fBm base: four octaves, self-affine ---
        float h   = 0f;
        float amp = Mathf.Min(SnowConstants.FbmAmp, tavan);
        float frq = SnowConstants.FbmScale;

        for (int i = 0; i < 4; i++)
        {
            h += (ValueNoise(worldXZ.x * frq + i * 17.3f,
                             worldXZ.y * frq + i * 17.3f) * 2f - 1f) * amp
               * OktavAgirligi(1f / frq, pikselBoyu, yalnizGeometri);

            amp *= SnowConstants.FbmGain;
            frq *= 2f;
        }

        // --- the wind axis ---
        Vector2 w = sastrugiWindDir;
        float uz = w.magnitude;
        w = uz > 1e-3f ? w / uz : new Vector2(1f, 0f);

        Vector2 dik = new Vector2(-w.y, w.x);

        float boyunca = worldXZ.x * w.x + worldXZ.y * w.y;
        float enine   = worldXZ.x * dik.x + worldXZ.y * dik.y;

        // --- RIPPLE: ridges perpendicular to the wind ---
        h += (ValueNoise(boyunca / SnowConstants.RippleLength,
                         enine / (SnowConstants.RippleLength * 6f)) * 2f - 1f)
           * Mathf.Min(SnowConstants.RippleAmp, tavan)
           * OktavAgirligi(SnowConstants.RippleLength, pikselBoyu, yalnizGeometri);

        // --- SASTRUGI: parallel to the wind, sharp ---
        float ns = ValueNoise(boyunca / SnowConstants.SastrugiWidth,
                              enine / SnowConstants.SastrugiLength);
        ns = ns * ns * (3f - 2f * ns);

        h += (ns - 0.5f) * Mathf.Min(SnowConstants.SastrugiHeight, tavan) * sastrugiPay
           * OktavAgirligi(SnowConstants.SastrugiLength, pikselBoyu, yalnizGeometri);

        // --- DRIFT: deposition mounds, soft ---
        h += (ValueNoise(boyunca / SnowConstants.DriftWidth,
                         enine / SnowConstants.DriftLength) - 0.5f)
           * Mathf.Min(SnowConstants.DriftHeight, tavan) * driftPay
           * OktavAgirligi(SnowConstants.DriftLength, pikselBoyu, yalnizGeometri);

        return h;
    }

    /// The height directly from a world position.
    ///
    /// The snow depth and the wind shadow come FROM OUTSIDE: this class has to stay pure
    /// (the co-op rule) and must not depend on `SnowManager` — systems do not call each
    /// other directly (`CLAUDE.md`).
    public static float RolyefDunya(Vector3 posWS, float karDerinligi,
                                    float ruzgarGolgesi, Vector2 sastrugiWindDir)
    {
        if (karDerinligi <= 0f) return 0f;

        // The exposure is the inverse of `SampleWindShadow`: that function measures
        // shelteredness. The coefficient has to be the same as in `SnowTessellation.hlsl`.
        float maruziyet = 1f - Mathf.Clamp01(ruzgarGolgesi * 1.2f);

        return Rolyef(new Vector2(posWS.x, posWS.z), karDerinligi,
                      sastrugiWindDir, maruziyet);
    }
}
