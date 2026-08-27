// ROLE: the mask and the application of snow settling on objects (spec §16).
// CALLED BY: SnowCoverObject.shader; and existing object shaders if they want it.

#ifndef SNOW_COVER_INCLUDED
#define SNOW_COVER_INCLUDED

#include "SnowCommon.hlsl"
#include "../../Shaders/StochasticTiling.hlsl"

/// However much snow is on the ground, that much is on the objects. `SnowCoverageDriver`
/// feeds it; no separate source is set up.
float _SnowCoverage;

/// THE COVER SETTINGS ARE GLOBAL (spec §16). Both the terrain's snow layer and the object
/// shader read them; `SnowCoverageDriver` is their sole owner.
float _SnowCoverSlopeSharpness;
float _SnowCoverBreakupStrength;
float _SnowCoverEdgeSharpness;
float _SnowCoverThickness;

/// SNOW SETTLES ON SURFACES CLOSE TO HORIZONTAL
/// [SOURCE: Company of Heroes 2, KGC 2013].
///
/// Four factors: slope, sky visibility, cavity (AO) and noise. Without the sky
/// factor the UNDERSIDE of a roof gets snow too — the most noticeable error.
/// THE SAMPLING IS OUTSIDE. `SAMPLE_TEXTURE2D` uses implicit derivatives and does not
/// compile in a compute shader; with the noise taken as a parameter the mask's logic
/// can be called identically from the fragment and from the test.
float SnowCoverMaskWithNoise(float3 posWS, float3 N, float ao, float noise01,
                             float slopeThreshold, float slopeSharpness,
                             float breakupStrength, float edgeSharpness)
{
    float3 up = _SnowUpDirection;

    float slope = dot(N, up);
    float slopeMask = saturate((slope - slopeThreshold) / max(1.0 - slopeThreshold, 1e-3));
    slopeMask = pow(slopeMask, slopeSharpness);

    float skyVis = SampleSkyVisibility(posWS);
    float cavity = saturate(ao * 1.35 - 0.35);

    float noise = lerp(0.5, noise01, breakupStrength);

    float raw = slopeMask * skyVis * cavity * _SnowCoverage * 1.7;
    return saturate((raw - noise) * edgeSharpness);
}

float SnowCoverMask(float3 posWS, float3 N, float ao,
                    float slopeThreshold, float slopeSharpness,
                    float breakupScale, float breakupStrength, float edgeSharpness)
{
    // STOCHASTIC TILING — a plain tiling's grid was visible here too.
    float noise = SampleStochasticMask(TEXTURE2D_ARGS(_SnowBreakup, sampler_SnowBreakup),
                                       posWS.xz * breakupScale);

    return SnowCoverMaskWithNoise(posWS, N, ao, noise,
                                  slopeThreshold, slopeSharpness,
                                  breakupStrength, edgeSharpness);
}

/// Shows the thickness of the snow drift by swelling the mask's edge.
/// Only a normal's work — the geometry does not change.
float3 SnowCoverNormal(float3 N, float mask, float edgeBulge)
{
    float3 up = _SnowUpDirection;

    N = normalize(lerp(N, up, pow(mask, 0.55)));

    float2 g = float2(ddx(mask), ddy(mask));
    return normalize(N + edgeBulge * float3(g.x, 0.0, g.y));
}

#endif
