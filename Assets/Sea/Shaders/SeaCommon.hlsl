// ROLE: declarations, globals and shared functions used by both the sea
// compute shaders and the surface shader.
// CALLED BY: SeaSpectrum.compute, SeaFFT.compute, SeaFoam.compute, SeaLit.shader

#ifndef SEA_COMMON_INCLUDED
#define SEA_COMMON_INCLUDED

// URP CORE FIRST.
//
// The `SAMPLER` macro comes from here. Without it a compute kernel SILENTLY
// fails to compile: `GetComputeShaderMessages` returns empty but `FindKernel`
// reports "kernel at index 0 is invalid". The snow system burned a round on
// this (`RATIONALE.md` — "Two silent compile traps in compute shaders").
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "SeaConstants.hlsl"

// ---------------------------------------------------------------- globals

/// Wind direction * speed (m/s). The MAIN input of the spectrum; `SeaManager`
/// writes it every frame. The sea DOES NOT build its own wind noise (spec 3.4).
float2 _SeaWindWS;

/// Loop-quantized time (s).
float  _SeaTime;

/// World Y coordinate of the sea surface.
float  _SeaLevelY;

// --- Bathymetry (spec 9) ---
TEXTURE2D(_SeaBathyTex);

// ITS OWN SAMPLER.
//
// `sampler_LinearClamp` is URP's internal sampler and is defined ONLY in the
// fragment/vertex stages; in compute it reports "undeclared identifier" and
// the kernel stays SILENTLY invalid — `HasKernel` still returns True and the
// error only surfaces at dispatch as "Kernel at index (0) is invalid".
SAMPLER(sampler_SeaBathyTex);
float2 _SeaBathyOriginXZ;
float2 _SeaBathySizeXZ;
float  _SeaBathyResolution;
float  _SeaDeepWaterDepth;

// --- Tier parameters (spec 6.6) ---
float3 _SeaPatchSizes;
float3 _SeaTierWeights;
float3 _SeaChoppinessPerTier;

float _SeaSpectrumDepth;
float _SeaMaxShoalingGain;

/// Significant wave height Hs (m) of the CURRENT sea state. The breaking
/// criterion needs the wave's height, and a pixel does not know it: it only
/// knows its own elevation. Published by `SeaManager`.
float _SeaSignificantHeight;

/// The RUNNING FFT size and its log2. Comes from the quality preset;
/// `SEA_FFT_SIZE` is only the upper bound.
uint _SeaFftSize;
uint _SeaFftLog2;

float _SeaFetch;
float _SeaSwell;
float _SeaSmallWaveCutoff;
float _SeaLoopPeriod;
float _SeaChoppiness;

// --- Diagnostics ---

// ------------------------------------------------------------- wave field

// FFT OUTPUT. `SeaSimulation` writes it every frame.
//
// SAMPLED FROM WORLD COORDINATES, NO `frac()`. The textures are created with
// `wrapMode = Repeat` and the hardware already repeats; adding `frac()`
// creates a seam at texel boundaries (spec 10.4).
TEXTURE2D_ARRAY(_SeaDisplacement);
SAMPLER(sampler_SeaDisplacement);

TEXTURE2D_ARRAY(_SeaDerivatives);
SAMPLER(sampler_SeaDerivatives);

TEXTURE2D_ARRAY(_SeaFoam);
SAMPLER(sampler_SeaFoam);

/// Folded for the same reason as `SeaHash21`, and by the same rule.
float2 SeaHash22(float2 p)
{
    p = fmod(abs(p), SEA_HASH_PERIOD);

    float3 q = frac(float3(p.x, p.y, p.x + 19.19) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac(float2((q.x + q.y) * q.z, (q.x + q.z) * q.y));
}

/// HEXAGONAL TILING — BREAKING THE FFT OWN REPEAT.
///
/// An FFT patch repeats exactly every `L` metres. Measured on the tiers in use:
/// tier 2 has L = 37 m and fits into the visible sea 110 times, tier 1 has 191 m
/// and fits 21 times. Coprime patch sizes stop the three lining up with each
/// other, but each one still repeats on its own.
///
/// The cure is by-example synthesis: cover the plane with hexagons, give each
/// hexagon a random offset into the SAME texture, and blend the three that cover a
/// point. Three samples, no extra memory, and cheaper than adding a fourth FFT
/// tier. [SOURCE: Heitz & Neyret 2018; Ubisoft La Forge, ocean tiling and blending]
///
/// THE GAUSSIAN STEP IS NOT NEEDED HERE. Heitz and Neyret "Gaussianise" the input
/// and undo it after blending, because a photographed texture is not Gaussian. A
/// Tessendorf field IS: it is a sum of components with random phases. So plain
/// mean-and-variance preservation is exact for us, and the histogram transform —
/// the expensive half of the method — drops out.
void SeaHexWeights(float2 uv, out float2 o0, out float2 o1, out float2 o2, out float3 w)
{
    // Skew the square lattice into the hexagonal one.
    const float2x2 toSkewed = float2x2(1.0, 0.0, -0.57735027, 1.15470054);

    float2 skewed = mul(toSkewed, uv * SEA_HEX_TILES);
    float2 baseCell = floor(skewed);
    float3 bary = float3(frac(skewed), 0.0);
    bary.z = 1.0 - bary.x - bary.y;

    float2 v0, v1, v2;
    if (bary.z > 0.0)
    {
        w  = float3(bary.z, bary.y, bary.x);
        v0 = baseCell;
        v1 = baseCell + float2(0.0, 1.0);
        v2 = baseCell + float2(1.0, 0.0);
    }
    else
    {
        w  = float3(-bary.z, 1.0 - bary.y, 1.0 - bary.x);
        v0 = baseCell + float2(1.0, 1.0);
        v1 = baseCell + float2(1.0, 0.0);
        v2 = baseCell + float2(0.0, 1.0);
    }

    // Each hexagon reads the patch from its own random place. The texture wraps,
    // so any offset is legal.
    o0 = SeaHash22(v0);
    o1 = SeaHash22(v1);
    o2 = SeaHash22(v2);
}

/// Variance-preserving blend of three samples of a ZERO-MEAN Gaussian field.
/// A plain weighted sum would shrink the variance wherever the weights are even,
/// and the sea would go flat in the middle of every hexagon.
float3 SeaHexBlend3(float3 a, float3 b, float3 c, float3 w)
{
    return (a * w.x + b * w.y + c * w.z) * rsqrt(dot(w, w));
}

float2 SeaHexBlend2(float2 a, float2 b, float2 c, float3 w)
{
    return (a * w.x + b * w.y + c * w.z) * rsqrt(dot(w, w));
}

/// Sum of the three tiers' displacement. w = the SMALLEST Jacobian across
/// the tiers.
///
/// The smallest is taken because a fold in a single tier still means the
/// surface has folded; averaging would dissolve a fold in the fine chop into
/// the flatness of the coarse tier.
float4 SeaSampleDisplacement(float2 posXZ)
{
    float3 disp = 0.0;
    float  jac  = 1.0;

    [unroll]
    for (int s = 0; s < SEA_TIER_COUNT; ++s)
    {
        float2 uv = posXZ / _SeaPatchSizes[s];

        if (s == SEA_HEX_TIER)
        {
            float2 o0, o1, o2; float3 w;
            SeaHexWeights(uv, o0, o1, o2, w);

            float4 d0 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SeaDisplacement, sampler_SeaDisplacement, uv + o0, s, 0);
            float4 d1 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SeaDisplacement, sampler_SeaDisplacement, uv + o1, s, 0);
            float4 d2 = SAMPLE_TEXTURE2D_ARRAY_LOD(_SeaDisplacement, sampler_SeaDisplacement, uv + o2, s, 0);

            disp += SeaHexBlend3(d0.xyz, d1.xyz, d2.xyz, w) * _SeaTierWeights[s];

            // THE JACOBIAN IS NOT BLENDED. It is not a zero-mean Gaussian — it sits
            // around 1 and folding is the MINIMUM, not the average. Taking the most
            // folded of the three keeps foam where a fold actually is.
            jac = min(jac, min(d0.w, min(d1.w, d2.w)));
        }
        else
        {
            float4 d = SAMPLE_TEXTURE2D_ARRAY_LOD(_SeaDisplacement,
                                                  sampler_SeaDisplacement, uv, s, 0);
            disp += d.xyz * _SeaTierWeights[s];
            jac   = min(jac, d.w);
        }
    }

    return float4(disp, jac);
}

/// Sum of the three tiers' slope. The normal is built from this — NO CENTRAL
/// DIFFERENCE: the slope already comes from the FFT and that is more accurate
/// (spec 6.7, 10.5).
float2 SeaSampleSlope(float2 posXZ)
{
    float2 slope = 0.0;

    [unroll]
    for (int s = 0; s < SEA_TIER_COUNT; ++s)
    {
        float2 uv = posXZ / _SeaPatchSizes[s];

        if (s == SEA_HEX_TIER)
        {
            float2 o0, o1, o2; float3 w;
            SeaHexWeights(uv, o0, o1, o2, w);

            // THE SAME WEIGHTS AS THE DISPLACEMENT. Blended differently, the normal
            // would stop describing the surface the geometry actually has.
            float2 s0 = SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives, sampler_SeaDerivatives, uv + o0, s).xy;
            float2 s1 = SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives, sampler_SeaDerivatives, uv + o1, s).xy;
            float2 s2 = SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives, sampler_SeaDerivatives, uv + o2, s).xy;

            slope += SeaHexBlend2(s0, s1, s2, w) * _SeaTierWeights[s];
        }
        else
        {
            slope += SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives,
                                            sampler_SeaDerivatives, uv, s).xy
                   * _SeaTierWeights[s];
        }
    }

    return slope;
}

/// Whitecap foam density and the FOLD DIRECTION.
///
/// The LARGEST value across tiers is taken — foam is coverage, not an
/// average. The direction comes from the tier that WON: taking another
/// tier's direction would make the pattern unrelated to the direction the
/// foam actually stretches in.
float SeaSampleFoam(float2 posXZ, out float2 foldDirection)
{
    float f = 0.0;
    foldDirection = float2(1.0, 0.0);

    [unroll]
    for (int s = 0; s < SEA_TIER_COUNT; ++s)
    {
        float2 uv = posXZ / _SeaPatchSizes[s];
        float2 pick = uv;
        float k;

        if (s == SEA_HEX_TIER)
        {
            float2 o0, o1, o2; float3 w;
            SeaHexWeights(uv, o0, o1, o2, w);

            // FOAM IS COVERAGE, SO THE LARGEST WINS — the same rule already used
            // across tiers. Blending it would average foam away where the hexagons
            // meet and draw a honeycomb.
            float k0 = SAMPLE_TEXTURE2D_ARRAY(_SeaFoam, sampler_SeaFoam, uv + o0, s).r;
            float k1 = SAMPLE_TEXTURE2D_ARRAY(_SeaFoam, sampler_SeaFoam, uv + o1, s).r;
            float k2 = SAMPLE_TEXTURE2D_ARRAY(_SeaFoam, sampler_SeaFoam, uv + o2, s).r;

            float2 best = o0;
            k = k0;
            if (k1 > k) { k = k1; best = o1; }
            if (k2 > k) { k = k2; best = o2; }
            pick = uv + best;
        }
        else
        {
            k = SAMPLE_TEXTURE2D_ARRAY(_SeaFoam, sampler_SeaFoam, uv, s).r;
        }

        if (k > f)
        {
            f = k;
            foldDirection = SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives,
                                                   sampler_SeaDerivatives, pick, s).zw;
        }
    }

    return f;
}

// ------------------------------------------------------------------ noise

/// PROCEDURAL FOAM PATTERN — NO TEXTURE.
///
/// Spec 13 asks for `T_Foam` and `T_FoamBreakup` textures. The foam pattern is
/// built procedurally instead: value noise here for the waterline's irregular
/// outline, and a CELLULAR field (`SeaFoamBubbles`) for the bubble structure.
/// The reasoning is in `DECISIONS.md` — value noise cannot describe foam at
/// any octave count, and the cellular field needs no texture at all.
/// THE COORDINATE IS FOLDED BEFORE IT IS HASHED.
///
/// The shore sits at x = 12000, z = -13000. `frac(p * 123.34)` on a number that
/// large is `frac()` of about 1.5 million, and a float carries 24 bits of
/// mantissa: what comes back is a handful of quantized values, not noise.
///
/// MEASURED, over a 64x64 block of cells (4096 possible values):
///
///     cell origin        distinct values
///     0                  1040
///     1000                157
///     9600                 39
///     15000                20
///
/// Twenty values across four thousand cells is a LATTICE, and that is the grid
/// that showed on the foam. Folding into a 512-unit period first, then using
/// small multipliers, holds 2400 at every origin.
///
/// This is `MountainHash`'s own recipe (`MountainSurface.hlsl`) — the terrain hit
/// the same wall at the same scale and solved it there.
float SeaHash21(float2 p)
{
    p = fmod(abs(p), SEA_HASH_PERIOD);

    float3 q = frac(float3(p.x, p.y, p.x + 19.19) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

float SeaValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = SeaHash21(i);
    float b = SeaHash21(i + float2(1.0, 0.0));
    float c = SeaHash21(i + float2(0.0, 1.0));
    float d = SeaHash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// Three octaves. Foam carries both coarse clumps and fine bubbles.
float SeaFoamNoise(float2 p)
{
    return SeaValueNoise(p)        * 0.60
         + SeaValueNoise(p * 2.37) * 0.30
         + SeaValueNoise(p * 5.13) * 0.10;
}

/// Two 2D random offsets for a cell.

/// WORLEY (CELLULAR) F1 — the distance to the nearest scattered point.
///
/// FOAM IS NOT VALUE NOISE. Value noise is a smooth hill field; whatever the
/// octaves, it produces soft clouds. Foam is a packed mass of BUBBLES: round
/// cells crowded against each other with thin walls between them. That is
/// exactly what a cellular field describes, and it is the reason foam built
/// from value noise reads as a wash of paint rather than a surface.
float SeaCellular(float2 p)
{
    float2 cell = floor(p);
    float2 f    = frac(p);

    float best = 1e9;

    [unroll]
    for (int y = -1; y <= 1; ++y)
    [unroll]
    for (int x = -1; x <= 1; ++x)
    {
        float2 offset = float2(x, y);
        float2 point0 = offset + SeaHash22(cell + offset);
        float2 d      = point0 - f;
        best = min(best, dot(d, d));
    }

    return sqrt(best);
}

/// BUBBLE FIELD, 0..1. High where a bubble's body is, low on the walls
/// between them.
///
/// Two scales: big bubbles with small ones packed into the gaps, which is how
/// real foam is graded. The result is squared to leave the walls thin — a
/// linear falloff makes every bubble a soft blob and the mass turns back into
/// a wash.
float SeaFoamBubbles(float2 p)
{
    float coarse = 1.0 - SeaCellular(p);
    float fine   = 1.0 - SeaCellular(p * 2.9 + 17.3);

    float bubbles = coarse * 0.65 + fine * 0.35;
    return saturate(bubbles * bubbles * 1.35);
}

// -------------------------------------------------------- complex numbers

/// Complex multiply.
float2 SeaCMul(float2 a, float2 b)
{
    return float2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

/// Complex conjugate.
float2 SeaConj(float2 a)
{
    return float2(a.x, -a.y);
}

/// e^{i*theta}
float2 SeaExpI(float theta)
{
    float s, c;
    sincos(theta, s, c);
    return float2(c, s);
}

// ---------------------------------------------------------------- physics

/// SHALLOW WATER DISPERSION.
///
/// omega^2 = g*k*tanh(k*D). When the bottom is very deep tanh goes to 1 and
/// this reduces to the deep-water relation — one formula covers both cases.
/// **The deep-water relation is NOT WRITTEN SEPARATELY** (spec 6.4, 17).
/// [SOURCE: Tessendorf 2004 equations 31 and 32]
float SeaOmega(float k, float depth)
{
    // THE TANH ARGUMENT IS CLAMPED.
    //
    // The compiler expands `tanh` as (e^x - e^-x)/(e^x + e^-x); past x ~ 88
    // e^x overflows to inf and inf/inf gives NaN. Measured: at 60 m depth
    // every texel with |n| > 112 was NaN, and it spread from there across the
    // whole FFT field.
    //
    // At 20 tanh is already within 1e-17 of 1 — the clamp changes nothing
    // physical, it only prevents the overflow.
    return sqrt(SEA_G * k * tanh(min(k * depth, 20.0)));
}

/// LOOP QUANTIZATION.
///
/// Every frequency must be a multiple of the fundamental so the simulation
/// repeats every T seconds. MANDATORY: it makes the field repeatable without
/// recomputing on the GPU and prevents loss of float precision as `t` grows
/// over long sessions.
/// [SOURCE: Tessendorf 2004 4.2 equations 34, 35]
float SeaQuantizeOmega(float omega)
{
    float omega0 = SEA_TWO_PI / _SeaLoopPeriod;
    return floor(omega / omega0) * omega0;
}

// ------------------------------------------------------------- bathymetry

/// Water depth (m). >0 water, <0 land.
///
/// The terrain heightmap is NOT SAMPLED DIRECTLY in a shader — the scaling
/// constants change between Unity versions. It is baked once on the CPU
/// (spec 9).
/// THE SEA BED DOES NOT FALL OFF A CLIFF AT THE TERRAIN'S EDGE.
///
/// This used to return `_SeaDeepWaterDepth` the moment the sample left the
/// terrain box. Measured on the four edges: the real depth there is 12.9-30.0 m
/// (mean 25.4 m), and one texel further out the shader read 200 m. An eight-fold
/// step in the quantity that drives absorption, shoaling and breaking — along a
/// PERFECTLY STRAIGHT line, because the terrain box is a square. Two of those
/// lines meet at a corner. That was the straight-edged patch on the water.
///
/// The bed now keeps descending outward. `saturate(uv)` reads the nearest edge
/// texel, so the value is continuous ACROSS the boundary, and from there it
/// reaches deep water over `SEA_OFFSHORE_RAMP`.
float SeaSampleDepth(float2 posXZ)
{
    float2 uv = (posXZ - _SeaBathyOriginXZ) / _SeaBathySizeXZ;

    float bed = SAMPLE_TEXTURE2D_LOD(_SeaBathyTex, sampler_SeaBathyTex, saturate(uv), 0).r;

    // How far outside the terrain box the sample is, in metres.
    float2 outsideUV = max(max(-uv, uv - 1.0), 0.0);
    float  outside   = length(outsideUV * _SeaBathySizeXZ);

    return lerp(bed, _SeaDeepWaterDepth, saturate(outside / SEA_OFFSHORE_RAMP));
}

/// Bottom slope (tan theta). The breaker index follows from it (spec 8.3).
float SeaSampleBottomSlope(float2 posXZ)
{
    float e = _SeaBathySizeXZ.x / _SeaBathyResolution;

    float dx = SeaSampleDepth(posXZ + float2(e, 0)) - SeaSampleDepth(posXZ - float2(e, 0));
    float dz = SeaSampleDepth(posXZ + float2(0, e)) - SeaSampleDepth(posXZ - float2(0, e));

    return length(float2(dx, dz)) / (2.0 * e);
}

// ------------------------------------------------- shallow water transform

/// SHOALING — AMPLITUDE GROWTH.
///
/// A wave travelling over a slowly varying slope grows in amplitude
/// proportionally to h^(-1/4). [SOURCE: Green's law]
float SeaShoalingGain(float depthLocal, float depthRef)
{
    float d = max(depthLocal, SEA_MIN_DEPTH);

    // The `max(..., 0)` is for a compiler warning: `d` is already positive
    // and so is `depthRef`, so the ratio cannot be negative — but the
    // compiler cannot see that and emits "pow will not work for negative f".
    return pow(max(depthRef / d, 0.0), 0.25);
}

/// BREAKER DEPTH INDEX, SLOPE DEPENDENT.
///
/// A fixed 0.78 IS NOT USED (spec 8.3, 17): on very mild slopes the lower
/// bound drops to 0.55, on steep shores it rises above 1.0.
/// [SOURCE: McCowan 1894; Nelson 1983; DNV 2017; Galvin 1969; Weggel 1972]
float SeaBreakerIndex(float slope)
{
    return lerp(SEA_GAMMA_MILD, SEA_GAMMA_STEEP, saturate(slope / 0.10));
}

// -------------------------------------------------------- surface deform

struct SeaSurfacePoint
{
    float3 posWS;
    float  depth;
    float  jacobian;
};

/// SHALLOW WATER TRANSFORM (spec 8) — vertex stage.
///
/// THE FORWARD AND DEPTH PASSES CALL THE SAME FUNCTION. Written separately,
/// the depth buffer and the color buffer would see different surfaces and the
/// sea would catch on its own depth test.
///
/// Depth is read from the UNDISPLACED xz: horizontal displacement is the
/// wave's own motion, it does not move the sea bed (spec 10.4).
SeaSurfacePoint SeaDeform(float3 posWS)
{
    SeaSurfacePoint o;
    o.depth = SeaSampleDepth(posWS.xz);

    float4 field = SeaSampleDisplacement(posWS.xz);
    float3 disp  = field.xyz;
    o.jacobian   = field.w;

    {
        float slope = SeaSampleBottomSlope(posWS.xz);

        // SHOALING. Green's law goes to infinity in very shallow water; a
        // ceiling is applied, in reality breaking takes over (spec 8.1).
        float shoal = min(SeaShoalingGain(o.depth, _SeaSpectrumDepth),
                          _SeaMaxShoalingGain);

        // HORIZONTAL DISPLACEMENT DIES OUT IN SHALLOW WATER: the wave
        // steepens instead of spreading horizontally (spec 8.2).
        float chopScale = saturate(o.depth / SEA_CHOP_FADE_DEPTH);

        // SHORE DAMPING. As depth goes to zero the wave height must go to
        // zero too, otherwise the mesh intersects the terrain and flickers
        // (spec 8.4).
        float shoreFade = smoothstep(0.0, SEA_SHORE_FADE_DEPTH, o.depth);

        disp.y  *= shoal * shoreFade;
        disp.xz *= chopScale * shoreFade;

        // BREAKING HEIGHT LIMIT, SLOPE DEPENDENT (spec 8.3).
        float gamma = SeaBreakerIndex(slope);
        float hMax  = gamma * o.depth * 0.5;
        disp.y = sign(disp.y) * min(abs(disp.y), hMax);
    }

    posWS.xz += disp.xz;
    posWS.y  += disp.y;

    o.posWS = posWS;
    return o;
}

#endif
