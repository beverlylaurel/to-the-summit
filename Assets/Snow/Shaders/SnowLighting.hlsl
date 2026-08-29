// Snow surface shading and BRDF evaluation (spec §14.1, §14.3).
// Included by: SnowLitForwardPass.

#ifndef SNOW_LIGHTING_INCLUDED
#define SNOW_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SnowCommon.hlsl"
#include "SnowSparkle.hlsl"
#include "SnowSurfaceTextures.hlsl"

struct SnowSurface
{
    half3  albedo;
    half   roughness;
    half   wet;
    half   disturb;
    half   crust;
    half2  surfSlope;
    float  snowDepth;
    float3 positionWS;
    float  pixelFootprint;
    BRDFData brdfData;
};

void SnowInitBRDF(half3 albedo, half smoothness, half f0, inout half alpha,
                  out BRDFData brdf)
{
    half oneMinus = 1.0h - f0;

    InitializeBRDFDataDirect(albedo, albedo * oneMinus, half3(f0, f0, f0),
                             f0, oneMinus, smoothness, alpha, brdf);
}

SnowSurface SnowBuildSurfaceFrom(SnowSurfaceBlend surface,
                                 float rhoN, float wet, float disturb, float crust,
                                 float snowDepth, float3 positionWS, float pixelFootprint)
{
    float freshness = 1.0 - saturate((SnowDensity(rhoN) - 100.0) / 350.0);

    const half3 ALBEDO_FRESH  = half3(0.90, 0.92, 0.95);
    const half3 ALBEDO_PACKED = half3(0.70, 0.73, 0.79);
    const half3 TINT_WET      = half3(0.84, 0.86, 0.89);

    SnowSurface s;

    s.albedo    = lerp(ALBEDO_PACKED, ALBEDO_FRESH, freshness) * lerp(half3(1, 1, 1), TINT_WET, wet);
    s.roughness = lerp(SNOW_ROUGH_PACKED, SNOW_ROUGH_FRESH, freshness) * lerp(1.0, 0.62, wet);

    s.albedo    = saturate(s.albedo * surface.albedoTint);
    s.roughness = saturate(s.roughness + surface.roughAdd);
    s.surfSlope = surface.normalSlope;

    half crustMask = saturate((crust - 0.35) / 0.35);

    s.roughness = lerp(s.roughness, 0.25, crustMask);
    s.albedo    = lerp(s.albedo, s.albedo * half3(0.93, 0.95, 1.00), crustMask);

    s.wet = wet;
    s.disturb = disturb;
    s.crust = crustMask;
    s.snowDepth = snowDepth;
    s.positionWS = positionWS;
    s.pixelFootprint = pixelFootprint;

    half alpha = 1.0h;
    SnowInitBRDF(s.albedo, 1.0h - s.roughness, (half)SNOW_ICE_F0, alpha, s.brdfData);

    return s;
}

SnowSurface SnowBuildSurface(float rhoN, float wet, float disturb, float crust,
                             float snowDepth, float3 positionWS, float pixelFootprint)
{
    return SnowBuildSurfaceFrom(SnowSampleSurface(positionWS, rhoN, wet, disturb),
                                rhoN, wet, disturb, crust, snowDepth,
                                positionWS, pixelFootprint);
}

half3 SnowDirectLight(Light L, float3 N, float3 V, SnowSurface s)
{
    const half W = 0.15;

    half wrapNdotL = saturate((dot(N, L.direction) + W) / ((1.0 + W) * (1.0 + W)));
    half3 diffuse = s.albedo * wrapNdotL;

    half back  = saturate(dot(V, -L.direction));
    half trans = pow(back, 3.0) * exp(-s.snowDepth * 7.0) * _TranslucencyStrength;
    diffuse += s.albedo * trans * half3(1.00, 1.02, 1.10);

    half NdotL = saturate(dot(N, L.direction));
    half3 spec = s.brdfData.specular
               * DirectBRDFSpecular(s.brdfData, N, L.direction, V) * NdotL;


    half sunGate = saturate(_SunElevation01 * 20.0);
    half sparkle = 0;

#if !defined(_SNOW_QUALITY_LOW)
    float sparkleDist = distance(s.positionWS, _WorldSpaceCameraPos);
    half distGate = 1.0h - (half)smoothstep(SNOW_SPARKLE_FADE_START,
                                            SNOW_SPARKLE_FADE_END, sparkleDist);

    if (distGate > 0.0h)
        sparkle = SnowSparkle(s.positionWS, V, L.direction, s.pixelFootprint)
                * (1.0 - s.wet) * (1.0 - s.disturb * 0.45)
                * (1.0 - s.crust * 0.7)
                * saturate(dot(N, L.direction) * 4.0) * sunGate * distGate;
#endif

    half3 lightCol = L.color * (L.distanceAttenuation * L.shadowAttenuation);


    return (diffuse + spec + sparkle * _SparkleIntensity) * lightCol;
}

float _SnowMultiScatter;

half3 SnowAmbient(float3 N, SnowSurface s, half mainShadow, half heightAO,
                  half3 sunColor, float3 sunDirection)
{
    half3 ambient = SampleSH(N) * s.albedo;

    half shadowed = 1.0 - mainShadow;
    ambient *= lerp(half3(1, 1, 1), (half3)_ShadowTint.rgb, shadowed);

    half karAlbedo = (s.albedo.r + s.albedo.g + s.albedo.b) * (half)0.3333;
    half multiScatter = (half)1.0 / max((half)1.0 - karAlbedo * (half)0.25, (half)0.25);

    ambient *= lerp((half)1.0, multiScatter, (half)_SnowMultiScatter);

    ambient *= heightAO;
    ambient += sunColor * saturate(sunDirection.y) * s.albedo * SNOW_LATERAL_BOUNCE;

    return ambient;
}

#endif
