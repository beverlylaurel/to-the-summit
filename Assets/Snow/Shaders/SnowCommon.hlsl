// Shared GPU helpers for the snow subsystem — state transformations, world <-> texel
// mapping, ground height sampling.
// Included by: SnowSim.compute and all snow shaders.

#ifndef SNOW_COMMON_INCLUDED
#define SNOW_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "SnowConstants.hlsl"

// --- Snow state ---

/// Real density from normalized density, kg/m³ (spec §6.3).
float SnowDensity(float rhoN)
{
    return lerp(SNOW_RHO_MIN, SNOW_RHO_MAX, saturate(rhoN));
}

/// Normalized density from real density.
float SnowDensityN(float rho)
{
    return saturate((rho - SNOW_RHO_MIN) / (SNOW_RHO_MAX - SNOW_RHO_MIN));
}

/// Uncompacted snow column height, meters. `h = SWE * 1000 / rho`.
float SnowBaseHeight(float swe, float rhoN)
{
    return swe * SNOW_RHO_WATER / max(SnowDensity(rhoN), 1.0);
}

// --- World <-> Texel coordinate mapping ---

float2 _SnowAreaCenter;      // Active region world XZ center (snapped)
float  _SnowAreaSize;        // Active region dimension, meters
float  _SnowResolution;      // Texture resolution, texels

float2 SnowWorldToUV(float3 p)
{
    return (p.xz - _SnowAreaCenter) / _SnowAreaSize + 0.5;
}

float2 SnowUVToWorld(float2 uv)
{
    return (uv - 0.5) * _SnowAreaSize + _SnowAreaCenter;
}

float2 SnowTexelToWorld(uint2 id)
{
    return SnowUVToWorld((float2(id) + 0.5) / _SnowResolution);
}

float SnowTexelSize()
{
    return _SnowAreaSize / _SnowResolution;
}

/// Soft margin fade mask. 0 outside, 1 inside.
float SnowInsideMask(float2 uv)
{
    float2 e = abs(uv - 0.5) * 2.0;
    return 1.0 - smoothstep(0.88, 1.0, max(e.x, e.y));
}

// --- Environment globals ---

float3 _WindWS;
float  _WindSpeed;
float  _TemperatureC;
float  _SunElevation01;

TEXTURE2D(_SnowBreakup);
SAMPLER(sampler_SnowBreakup);

float4 _ShadowTint;
float  _TranslucencyStrength;

float  _SparkleCellSize;
float  _SparkleDensity;
float  _SparkleSharpness;
float  _SparkleIntensity;
float  _FogDensity01;
float  _RainOnSnow01;
float3 _SnowUpDirection;

float _FallbackSWE;
float _FallbackRhoN;
float _WorldSnowDepth;

float SnowWorldCoverHeight()
{
    return SnowBaseHeight(_FallbackSWE, _FallbackRhoN);
}

/// Integer hash — PCG3D [Jarzynski & Olano, JCGT 2020].
uint3 SnowPcg3d(uint3 v)
{
    v = v * 1664525u + 1013904223u;

    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;

    return v;
}

/// 3 independent floats in [0, 1].
float3 SnowRandU3(uint3 seed)
{
    return float3(SnowPcg3d(seed)) * (1.0 / 4294967296.0);
}

/// Integer grid cell hash.
float3 SnowRandCell3(int3 cell)
{
    return SnowRandU3(asuint(cell));
}

/// Block noise — constant per cell in [0, 1].
float SnowBlockNoise(float2 p)
{
    return SnowRandCell3(int3((int2)floor(p), 0)).x;
}

/// Value noise — bilinear interpolation of 4 cell hashes with smoothstep curve.
float SnowValueNoise(float2 p)
{
    float2 h = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = SnowRandCell3(int3((int2)h + int2(0, 0), 0)).x;
    float b = SnowRandCell3(int3((int2)h + int2(1, 0), 0)).x;
    float c = SnowRandCell3(int3((int2)h + int2(0, 1), 0)).x;
    float d = SnowRandCell3(int3((int2)h + int2(1, 1), 0)).x;

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// Domain-warped cellular noise.
float SnowWarpedBlockNoise(float2 p)
{
    float2 warp = float2(SnowValueNoise(p * 0.63),
                         SnowValueNoise(p * 0.63 + 27.7)) * 2.0 - 1.0;

    return SnowBlockNoise(p + warp * 0.75);
}

// --- Ground elevation ---

TEXTURE2D(_GroundHeightTex);

float2 _GroundOriginXZ;
float2 _GroundTexelXZ;
float2 _GroundSizeXZ;
float  _GroundBaseY;
float  _GroundHeightRange;

/// Ground elevation sample (spec §7.3).
float SampleGroundHeight(float2 posXZ)
{
    float2 uv = (posXZ - _GroundOriginXZ) / _GroundSizeXZ;
    float  n  = SAMPLE_TEXTURE2D_LOD(_GroundHeightTex, sampler_LinearClamp, saturate(uv), 0).r;
    return _GroundBaseY + n * _GroundHeightRange;
}

/// Ground normal sampled via central differences.
float3 SampleGroundNormal(float2 posXZ)
{
    float2 e = max(_GroundTexelXZ, 1e-3);

    float hL = SampleGroundHeight(posXZ - float2(e.x, 0.0));
    float hR = SampleGroundHeight(posXZ + float2(e.x, 0.0));
    float hD = SampleGroundHeight(posXZ - float2(0.0, e.y));
    float hU = SampleGroundHeight(posXZ + float2(0.0, e.y));

    return normalize(float3(hL - hR, e.x + e.y, hD - hU));
}

// --- Sky visibility ---

TEXTURE2D(_SnowSkyVisTex);

float2 _SkyCenterXZ;
float  _SkyAreaSize;
float  _SkyResolution;

/// Sky visibility factor in [0, 1] (spec §12.2).
float SampleSkyVisibility(float3 posWS)
{
    float2 uv = (posWS.xz - _SkyCenterXZ) / _SkyAreaSize + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0)) return 1.0;

    float t = 1.0 / _SkyResolution;
    float vis = 0.0;

    [unroll]
    for (int y = -1; y <= 1; ++y)
    [unroll]
    for (int x = -1; x <= 1; ++x)
    {
        float occlY = SAMPLE_TEXTURE2D_LOD(_SnowSkyVisTex, sampler_LinearClamp,
                                           uv + float2(x, y) * t, 0).r;
        vis += 1.0 - smoothstep(0.05, 0.40, occlY - posWS.y);
    }

    return vis * (1.0 / 9.0);
}

// --- Wind shadow ---

TEXTURE2D(_SnowWindShadowTex);

/// Wind shadow depth (> 0 sheltered/deposition, 0 exposed/erosion) (spec §18.0).
float SampleWindShadow(float3 posWS)
{
    float2 uv = (posWS.xz - _SkyCenterXZ) / _SkyAreaSize + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;

    float wz = SAMPLE_TEXTURE2D_LOD(_SnowWindShadowTex, sampler_LinearClamp, uv, 0).r;
    return max(0.0, wz - posWS.y);
}

/// Compact support falloff curve [Wyvill et al. 1999].
float WyvillFalloff(float r, float R)
{
    float t = saturate(1.0 - (r * r) / max(R * R, 1e-6));
    return t * t * t;
}

// --- Heat sources ---

#define SNOW_MAX_HEAT_SOURCES 16

float4 _HeatSources[SNOW_MAX_HEAT_SOURCES];
float4 _HeatParams[SNOW_MAX_HEAT_SOURCES];
int    _HeatCount;

/// Total heat field temperature contribution.
float SnowHeatField(float3 posWS)
{
    float theta = 0.0;

    [loop]
    for (int hi = 0; hi < _HeatCount; ++hi)
    {
        float3 hp = _HeatSources[hi].xyz;
        float  hr = _HeatSources[hi].w;

        float r = distance(posWS, hp);
        if (r >= hr) continue;

        theta += _HeatParams[hi].x * WyvillFalloff(r, hr);
    }

    return theta;
}

// --- Sastrugi ---

TEXTURE2D(_SastrugiNoise);
SAMPLER(sampler_SastrugiNoise);

float2 _SastrugiWindDir;
float  _SnowCavityRadius;

// --- Tessellation ---

float3 _SnowTessCameraPos;
float  _SnowTessMax;
float  _SnowTessNear;
float  _SnowTessFar;
float  _SnowDbgNoTess;
float  _SnowDbgNoDrift;

/// Sastrugi surface offset.
float SnowSastrugiOffset(float2 posXZ, float amplitude)
{
    if (amplitude <= 0.001) return 0.0;

    float2 wd = _SastrugiWindDir;
    float2 wp = float2(-wd.y, wd.x);

    float2 sUV = float2(dot(posXZ, wd) / SNOW_SASTRUGI_LENGTH,
                        dot(posXZ, wp) / SNOW_SASTRUGI_WIDTH);

    float n = SAMPLE_TEXTURE2D_LOD(_SastrugiNoise, sampler_SastrugiNoise, sUV, 0).r * 2.0 - 1.0;

    return n * SNOW_SASTRUGI_HEIGHT * amplitude;
}

// --- Snow surface state ---

TEXTURE2D(_SnowStateTex);
TEXTURE2D(_SnowTrailTex);

/// Fallback snow state outside active region.
float2 SnowOutsideStateAt(float2 posXZ)
{
    return float2(_FallbackSWE, _FallbackRhoN);
}

/// Sampled snow state vector (R=swe, G=rhoN, B=wet, A=disturb).
float4 SnowStateAt(float2 uv)
{
    float  inside = SnowInsideMask(uv);
    float4 s = SAMPLE_TEXTURE2D_LOD(_SnowStateTex, sampler_LinearClamp, saturate(uv), 0);

    float2 outside = SnowOutsideStateAt(SnowUVToWorld(uv));

    s.r = lerp(outside.x, s.r, inside);
    s.g = lerp(outside.y, s.g, inside);
    s.b *= inside;
    s.a *= inside;

    return s;
}

/// Sampled trail state vector (R=carve, G=rim, B=crust, A=sastrugi).
float4 SnowTrailAt(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, saturate(uv), 0)
           * SnowInsideMask(uv);
}

// --- Diagnostic debug uniforms ---

float _SnowDebugDent;
float _SnowDebugNormal;
float _SnowDebugProbe;
float _SnowDebugCover;

float _SnowDbgNoFbm;
float _SnowDbgNoRipple;
float _SnowDbgNoSastrugi;
float _SnowDbgNoMicro;
float _SnowDbgNoLod;
float _SnowDbgNoSpec;
float _SnowDbgNoSparkle;
float _SnowDbgNoWrap;
float _SnowDbgNoAO;
float _SnowDbgNoBounce;
float _SnowDbgNoTexNormal;
float _SnowDbgNoCavityShadow;
float _SnowDbgFlatNormal;

#endif
