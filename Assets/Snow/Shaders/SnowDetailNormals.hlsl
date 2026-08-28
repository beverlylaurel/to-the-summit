// Snow surface detail normals and blending (spec §14.2).
// Included by: SnowLitForwardPass, SnowLitDepthNormalsPass.

#ifndef SNOW_DETAIL_NORMALS_INCLUDED
#define SNOW_DETAIL_NORMALS_INCLUDED

TEXTURE2D(_SnowDetailNormal);
SAMPLER(sampler_SnowDetailNormal);

void SnowTriangleGrid(float2 uv, out float w1, out float w2, out float w3,
                      out int2 v1, out int2 v2, out int2 v3)
{
    const float2x2 gridToSkewed = float2x2(1.0, 0.0, -0.57735027, 1.15470054);

    float2 skewed = mul(gridToSkewed, uv * 3.4641016);   // 2*sqrt(3)
    int2 baseId = int2(floor(skewed));
    float2 f = frac(skewed);

    float3 temp = float3(f.x, f.y, 1.0 - f.x - f.y);

    if (temp.z > 0.0)
    {
        w1 = temp.z; w2 = temp.y; w3 = temp.x;
        v1 = baseId;
        v2 = baseId + int2(0, 1);
        v3 = baseId + int2(1, 0);
    }
    else
    {
        w1 = -temp.z; w2 = 1.0 - temp.y; w3 = 1.0 - temp.x;
        v1 = baseId + int2(1, 1);
        v2 = baseId + int2(1, 0);
        v3 = baseId + int2(0, 1);
    }
}

float2 SnowCellOffset(int2 cell)
{
    return SnowRandU3(uint3(asuint(cell.x), asuint(cell.y), 0x9E3779B9u)).xy;
}

float2 SampleDetailSlope(float2 worldXZ, float tileMeters, float strength)
{
    float2 uv = worldXZ / max(tileMeters, 1e-3);

    float w1, w2, w3;
    int2 v1, v2, v3;
    SnowTriangleGrid(uv, w1, w2, w3, v1, v2, v3);

    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    float4 c1 = SAMPLE_TEXTURE2D_GRAD(_SnowDetailNormal, sampler_SnowDetailNormal,
                                      uv + SnowCellOffset(v1), dx, dy);
    float4 c2 = SAMPLE_TEXTURE2D_GRAD(_SnowDetailNormal, sampler_SnowDetailNormal,
                                      uv + SnowCellOffset(v2), dx, dy);
    float4 c3 = SAMPLE_TEXTURE2D_GRAD(_SnowDetailNormal, sampler_SnowDetailNormal,
                                      uv + SnowCellOffset(v3), dx, dy);

    float3 n = UnpackNormal(c1 * w1 + c2 * w2 + c3 * w3);

    if (!all(isfinite(n))) n = float3(0.0, 0.0, 1.0);

    n.xy *= strength;
    n = normalize(n);

    return n.xy / max(n.z, 1e-3);
}

float3 SnowApplyDetailNormals(float3 normalWS, float3 positionWS,
                              float disturb, float distanceToCamera)
{
    float2 baseSlope = float2(normalWS.x, normalWS.z) / max(normalWS.y, 1e-3);
    float distFade = 1.0 - saturate((distanceToCamera - 6.0) / 10.0);

    float2 detailSlope = (float2)0.0;

#if defined(_SNOW_QUALITY_MEDIUM) || defined(_SNOW_QUALITY_HIGH)
    detailSlope += SampleDetailSlope(positionWS.xz, 0.6, 0.50);
#endif

#if defined(_SNOW_QUALITY_HIGH)
    detailSlope += SampleDetailSlope(positionWS.xz, 0.05, 0.40 * distFade);
    detailSlope += SampleDetailSlope(positionWS.xz, 0.25, disturb * 0.90);
#endif

    float2 toplam = baseSlope + detailSlope;
    return normalize(float3(toplam.x, 1.0, toplam.y));
}

#endif
