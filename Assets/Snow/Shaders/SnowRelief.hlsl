// Real-time snow trail relief mapping and micro-relief procedural geometry.
// Included by: MountainSurface.hlsl.

#ifndef SNOW_RELIEF_INCLUDED
#define SNOW_RELIEF_INCLUDED

#include "SnowCommon.hlsl"

float SnowDentAt(float2 uv)
{
    float4 trail = SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, saturate(uv), 0);
    return max(0.0, trail.r) * SnowInsideMask(uv);
}

float SnowShadeHeightAt(float2 uv)
{
    float4 trail = SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, saturate(uv), 0);
    return (trail.r - trail.g * SNOW_RIM_SHADE) * SnowInsideMask(uv);
}

float SnowDentSmooth(float2 uv)
{
    float2 boyut = (float2)_SnowResolution;
    float2 koord = uv * boyut - 0.5;
    float2 t     = floor(koord);
    float2 f     = koord - t;

    float2 f2 = f * f;
    float2 f3 = f2 * f;

    float2 w0 = (1.0 / 6.0) * (-f3 + 3.0 * f2 - 3.0 * f + 1.0);
    float2 w1 = (1.0 / 6.0) * (3.0 * f3 - 6.0 * f2 + 4.0);
    float2 w2 = (1.0 / 6.0) * (-3.0 * f3 + 3.0 * f2 + 3.0 * f + 1.0);
    float2 w3 = (1.0 / 6.0) * f3;

    float2 s0 = w0 + w1;
    float2 s1 = w2 + w3;

    float2 uv0 = (t - 0.5 + w1 / s0) / boyut;
    float2 uv1 = (t + 1.5 + w3 / s1) / boyut;

    float a = SnowShadeHeightAt(float2(uv0.x, uv0.y));
    float b = SnowShadeHeightAt(float2(uv1.x, uv0.y));
    float c = SnowShadeHeightAt(float2(uv0.x, uv1.y));
    float d = SnowShadeHeightAt(float2(uv1.x, uv1.y));

    return lerp(lerp(a, b, s1.x), lerp(c, d, s1.x), s1.y);
}

// Offsets the trail lookup along the view ray. The carve is reconstructed cubically before
// the offset is calculated; this keeps the apparent wall continuous instead of following
// the source texture's texel staircase. It is intentionally a single stable offset rather
// than a long ray march: the terrain keeps its real depth buffer while the footprint gains
// the near-field depth cue it needs.
float2 SnowReliefOffset(float3 posWS, float3 viewDirWS, out float dentOut)
{
    dentOut = 0.0;

    float2 uv0 = SnowWorldToUV(posWS);
    if (SnowInsideMask(uv0) < 0.01) return (float2)0.0;

    // Nearly every snow fragment is outside a footprint. A single raw lookup rejects those
    // pixels before cubic reconstruction; the threshold is sub-millimetre, below the
    // visible relief, so it does not trim the reconstructed wall.
    if (SnowDentAt(uv0) < 0.0002) return (float2)0.0;

    // Horizontal travel per metre of vertical descent. Clamp vector length rather than
    // components so diagonal views retain their direction.
    float vertical = max(viewDirWS.y, 0.15);
    float2 rayXZ = -viewDirWS.xz / vertical;
    float rayLength = length(rayXZ);
    rayXZ *= min(1.0, SNOW_RELIEF_MAX_STRETCH / max(rayLength, 1e-5));
    rayLength = min(rayLength, SNOW_RELIEF_MAX_STRETCH);

    // SnowDentSmooth already reconstructs the signed carve-plus-rim field cubically. The
    // offset only follows its positive (depressed) side; the raised rim still enters the
    // final normal but cannot push the lookup backwards.
    dentOut = min(max(0.0, SnowDentSmooth(uv0)), SNOW_RELIEF_MAX_DEPTH);
    return rayXZ * dentOut;
}

half SnowReliefShadow(float3 lightDirWS, float dent, float skyAmount)
{
    float horizonTan = dent / max(_SnowCavityRadius, 1e-3);
    float gunesTan = lightDirWS.y / max(length(lightDirWS.xz), 1e-4);
    float engel = saturate(1.0 - gunesTan / max(horizonTan, 1e-4));

    float baseVal = saturate(skyAmount + (1.0 - skyAmount) * SNOW_SHADOW_BOUNCE);
    float golge = saturate(1.0 - engel);

    return (half)lerp(baseVal, 1.0, golge);
}

half2 SnowDentSlope(float2 uv)
{
    float t = SNOW_DENT_SLOPE_TEXELS / _SnowResolution;
    float meterScale = _SnowAreaSize * t;

    float dL = SnowDentSmooth(uv - float2(t, 0));
    float dR = SnowDentSmooth(uv + float2(t, 0));
    float dD = SnowDentSmooth(uv - float2(0, t));
    float dU = SnowDentSmooth(uv + float2(0, t));

    return half2((dR - dL) / (2.0 * meterScale), (dU - dD) / (2.0 * meterScale));
}

float SnowPikselBoyu(float2 worldXZ)
{
#ifdef SHADER_STAGE_COMPUTE
    return 0.0;
#else
    float fx = fwidth(worldXZ.x);
    float fy = fwidth(worldXZ.y);
    return sqrt(max(fx * fy, 1e-10));
#endif
}

float SnowOctaveWeight(float wavelength, float pikselBoyu)
{
    return saturate(wavelength / max(pikselBoyu * 2.0, 1e-5) - 1.0);
}

float SnowOctaveWeightModal(float wavelength, float pikselBoyu, bool yalnizGeometri)
{
    if (yalnizGeometri && wavelength < SNOW_TESS_MIN_WAVELENGTH) return 0.0;
    return SnowOctaveWeight(wavelength, pikselBoyu);
}

float SnowSurfaceRelief(float2 worldXZ, float pikselBoyu, float snowDepth,
                      bool yalnizGeometri, float exposure)
{
    float ceiling = snowDepth * SNOW_BEDFORM_DEPTH_FRAC;

    float sastrugiAmount = exposure;
    float driftAmount    = 1.0 - exposure;

    float h   = 0.0;
    float amp = min(SNOW_FBM_AMP, ceiling);
    float frq = SNOW_FBM_SCALE;

    [unroll]
    for (int i = 0; i < 4; ++i)
    {
        h += (SnowValueNoise(worldXZ * frq + (float)i * 17.3) * 2.0 - 1.0) * amp
           * SnowOctaveWeightModal(1.0 / frq, pikselBoyu, yalnizGeometri);

        amp *= SNOW_FBM_GAIN;
        frq *= 2.0;
    }

    float2 w  = _SastrugiWindDir;
    float  uz = length(w);
    w = uz > 1e-3 ? w / uz : float2(1.0, 0.0);

    float2 dik = float2(-w.y, w.x);

    float2 pr = float2(dot(worldXZ, w)   / SNOW_RIPPLE_LENGTH,
                       dot(worldXZ, dik) / (SNOW_RIPPLE_LENGTH * 6.0));

    h += (SnowValueNoise(pr) * 2.0 - 1.0) * min(SNOW_RIPPLE_AMP, ceiling)
       * SnowOctaveWeightModal(SNOW_RIPPLE_LENGTH, pikselBoyu, yalnizGeometri);

    float2 ps = float2(dot(worldXZ, w)   / SNOW_SASTRUGI_WIDTH,
                       dot(worldXZ, dik) / SNOW_SASTRUGI_LENGTH);

    float ns = SnowValueNoise(ps);
    ns = ns * ns * (3.0 - 2.0 * ns);

    h += (ns - 0.5) * min(SNOW_SASTRUGI_HEIGHT, ceiling) * sastrugiAmount
       * SnowOctaveWeightModal(SNOW_SASTRUGI_LENGTH, pikselBoyu, yalnizGeometri);

    float2 pd = float2(dot(worldXZ, w)   / SNOW_DRIFT_WIDTH,
                       dot(worldXZ, dik) / SNOW_DRIFT_LENGTH);

    h += (SnowValueNoise(pd) - 0.5) * min(SNOW_DRIFT_HEIGHT, ceiling) * driftAmount
       * SnowOctaveWeightModal(SNOW_DRIFT_LENGTH, pikselBoyu, yalnizGeometri);

    return h;
}

half2 SnowSurfaceSlope(float2 worldXZ, float groundY, float snowDepth, out float heightOut)
{
    const float e = 0.02;

    float pikselBoyu = SnowPikselBoyu(worldXZ);
    float exposure = 1.0 - saturate(
        SampleWindShadow(float3(worldXZ.x, groundY, worldXZ.y)) * 1.2);

    float hL = SnowSurfaceRelief(worldXZ - float2(e, 0.0), pikselBoyu, snowDepth, false, exposure);
    float hR = SnowSurfaceRelief(worldXZ + float2(e, 0.0), pikselBoyu, snowDepth, false, exposure);
    float hD = SnowSurfaceRelief(worldXZ - float2(0.0, e), pikselBoyu, snowDepth, false, exposure);
    float hU = SnowSurfaceRelief(worldXZ + float2(0.0, e), pikselBoyu, snowDepth, false, exposure);

    heightOut = (hL + hR + hD + hU) * 0.25;
    return half2((hR - hL) / (2.0 * e), (hU - hD) / (2.0 * e));
}

float SnowMicroRelief(float2 worldXZ, float dent, float pikselBoyu)
{
    float w = lerp(SNOW_MICRO_BASE, 1.0, saturate(dent / SNOW_MICRO_REF_DEPTH));

    float n  = (SnowValueNoise(worldXZ * SNOW_MICRO_SCALE_A) * 2.0 - 1.0) * SNOW_MICRO_AMP_A
             * SnowOctaveWeight(1.0 / SNOW_MICRO_SCALE_A, pikselBoyu);
    n += (SnowValueNoise(worldXZ * SNOW_MICRO_SCALE_B + 13.9) * 2.0 - 1.0) * SNOW_MICRO_AMP_B
       * SnowOctaveWeight(1.0 / SNOW_MICRO_SCALE_B, pikselBoyu);
    n += (SnowValueNoise(worldXZ * SNOW_MICRO_SCALE_C + 71.3) * 2.0 - 1.0) * SNOW_MICRO_AMP_C
       * SnowOctaveWeight(1.0 / SNOW_MICRO_SCALE_C, pikselBoyu);

    return n * w;
}

half2 SnowMicroSlope(float2 worldXZ, float dent)
{
    const float e = 0.01;
    float pikselBoyu = SnowPikselBoyu(worldXZ);

    float mL = SnowMicroRelief(worldXZ - float2(e, 0.0), dent, pikselBoyu);
    float mR = SnowMicroRelief(worldXZ + float2(e, 0.0), dent, pikselBoyu);
    float mD = SnowMicroRelief(worldXZ - float2(0.0, e), dent, pikselBoyu);
    float mU = SnowMicroRelief(worldXZ + float2(0.0, e), dent, pikselBoyu);

    return half2((mR - mL) / (2.0 * e), (mU - mD) / (2.0 * e));
}

#endif
