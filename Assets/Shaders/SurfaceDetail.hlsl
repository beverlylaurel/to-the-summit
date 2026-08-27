#ifndef TOTHESUMMIT_SURFACE_DETAIL_INCLUDED
#define TOTHESUMMIT_SURFACE_DETAIL_INCLUDED

#include "StochasticTiling.hlsl"

// SHARED SURFACE DETAIL SAMPLING. Snow, rock, gravel, soil — all of it here.
//
// Every surface carries three maps (normal, roughness, height) and each one has
// been through the stochastic transform: Gaussian histogram + inverse LUT. Writing
// the declarations and the sampling by hand cost twelve lines per surface; the
// second surface made it twenty-four, the third thirty-six.
//
// Macros are used because HLSL cannot put a texture bundle in a struct — a
// TEXTURE2D declaration is not a type, it produces names.

/// Every texture declaration of one surface. The surface name is the prefix:
///   DECLARE_SURFACE_DETAIL(Rock) → _RockNormal, _RockNormalLut, ...
#define DECLARE_SURFACE_DETAIL(name)      \
    TEXTURE2D(_##name##Normal);           \
    TEXTURE2D(_##name##NormalLut);        \
    TEXTURE2D(_##name##Rough);            \
    TEXTURE2D(_##name##RoughLut);         \
    TEXTURE2D(_##name##Height);           \
    TEXTURE2D(_##name##HeightLut);

/// Micro detail read from a surface.
struct SurfaceDetail
{
    float3 normal;      // tangent space
    float roughness;
    float height;       // 0-1; the height blend uses this
};

/// A single surface read. `sharedSampler` is shared by every surface — the number
/// of samplers is limited in hardware and they all want the same settings (Repeat,
#define SAMPLE_SURFACE_DETAIL(name, sharedSampler, uv, ddxUV, ddyUV, result)          \
{                                                                                     \
    float4 n = SampleStochastic(TEXTURE2D_ARGS(_##name##Normal, sharedSampler),       \
                                TEXTURE2D_ARGS(_##name##NormalLut, sharedSampler),    \
                                uv, ddxUV, ddyUV);                                    \
    float4 r = SampleStochastic(TEXTURE2D_ARGS(_##name##Rough, sharedSampler),        \
                                TEXTURE2D_ARGS(_##name##RoughLut, sharedSampler),     \
                                uv, ddxUV, ddyUV);                                    \
    float4 h = SampleStochastic(TEXTURE2D_ARGS(_##name##Height, sharedSampler),       \
                                TEXTURE2D_ARGS(_##name##HeightLut, sharedSampler),    \
                                uv, ddxUV, ddyUV);                                    \
    /* The normal map is imported as "Default" (a conversion would break the        */\
    /* channel packing), so the unpacking is manual: 0-1 to -1..1.                  */\
    result.normal = normalize(n.xyz * 2.0 - 1.0);                                     \
    result.roughness = r.r;                                                           \
    result.height = h.r;                                                              \
}

/// HEIGHT BLEND. A linear mix blurs the two surfaces half and half everywhere; in
/// reality the upper material first fills the HOLLOWS and the lower one stays
/// exposed on the bumps. As the threshold `t` rises the upper material starts from
/// the low ground and covers the bumps; the transition gains a natural boundary.
///
/// With `sharpness` at zero the boundary would be knife sharp; wide, it would fall
/// back to a linear mix.
float SurfaceHeightBlend(float lowerHeight, float upperHeight, float t, float sharpness)
{
    float threshold = lerp(-sharpness, 1.0 + sharpness, t);
    return saturate((upperHeight - lowerHeight + threshold) / (2.0 * sharpness));
}

/// Height-based mix of two surfaces.
SurfaceDetail BlendSurfaceDetail(SurfaceDetail lower, SurfaceDetail upper,
                                 float t, float sharpness)
{
    float blend = SurfaceHeightBlend(lower.height, upper.height, t, sharpness);

    SurfaceDetail result;
    result.normal = normalize(lerp(lower.normal, upper.normal, blend));
    result.roughness = lerp(lower.roughness, upper.roughness, blend);
    result.height = lerp(lower.height, upper.height, blend);
    return result;
}

/// SLOPE-CORRECTED UV. A UV taken from world XZ stretches by 1/cos(slope) on a
/// steep face. It is corrected by blending with a vertical plane projection — full
/// triplanar means three samples, two are enough.
float2 SurfacePlanarUV(float3 worldPos, float3 normalWS, float scale)
{
    float2 flat = worldPos.xz * scale;
    float2 side = abs(normalWS.x) > abs(normalWS.z)
                ? worldPos.zy * scale
                : worldPos.xy * scale;

    float steep = 1.0 - saturate(normalWS.y);
    return lerp(flat, side, smoothstep(0.25, 0.75, steep));
}

/// Rotates a UV onto an axis. Directional textures (wind sastrugi, layered rock)
/// are drawn along a fixed direction in the world; they have to line up with the
void SurfaceAlignUV(float2 axis, inout float2 uv, inout float2 ddxUV, inout float2 ddyUV)
{
    float2 perpendicular = float2(-axis.y, axis.x);
    uv = float2(dot(uv, axis), dot(uv, perpendicular));
    ddxUV = float2(dot(ddxUV, axis), dot(ddxUV, perpendicular));
    ddyUV = float2(dot(ddyUV, axis), dot(ddyUV, perpendicular));
}

#endif
