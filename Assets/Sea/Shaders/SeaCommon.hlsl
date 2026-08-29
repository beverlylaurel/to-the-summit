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
float _SeaDbgNoWaves;
float _SeaDbgNoShallow;
float _SeaDbgNoFoam;
float _SeaDbgNoRefraction;
float _SeaDbgNoSurface;

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
        float4 d = SAMPLE_TEXTURE2D_ARRAY_LOD(_SeaDisplacement,
                                              sampler_SeaDisplacement, uv, s, 0);
        disp += d.xyz * _SeaTierWeights[s];
        jac   = min(jac, d.w);
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
        slope += SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives,
                                        sampler_SeaDerivatives, uv, s).xy
               * _SeaTierWeights[s];
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
        float k = SAMPLE_TEXTURE2D_ARRAY(_SeaFoam, sampler_SeaFoam, uv, s).r;

        if (k > f)
        {
            f = k;
            foldDirection = SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives,
                                                   sampler_SeaDerivatives, uv, s).zw;
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
float SeaHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
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
float2 SeaHash22(float2 p)
{
    float3 q = frac(p.xyx * float3(127.1, 311.7, 74.7));
    q += dot(q, q.yzx + 34.23);
    return frac(float2(q.x * q.y, q.y * q.z));
}

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
float SeaSampleDepth(float2 posXZ)
{
    float2 uv = (posXZ - _SeaBathyOriginXZ) / _SeaBathySizeXZ;

    // Outside the terrain = open sea. The sea mesh is larger than the terrain
    // and reaches the horizon; beyond it the depth is constant.
    if (any(uv < 0.0) || any(uv > 1.0)) return _SeaDeepWaterDepth;

    return SAMPLE_TEXTURE2D_LOD(_SeaBathyTex, sampler_SeaBathyTex, uv, 0).r;
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
    float3 disp  = _SeaDbgNoWaves > 0.5 ? 0.0 : field.xyz;
    o.jacobian   = field.w;

    if (_SeaDbgNoShallow <= 0.5)
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
