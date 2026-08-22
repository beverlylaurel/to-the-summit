// ROL: kar yüzeyinin DepthNormals geçişi. SSAO ve sonraki fazlarda ekran uzayı
// efektleri bu tamponu okuyor; kar burada olmazsa karın üstünde AO oluşmaz.
// Çağıran: SnowLit.shader.

#ifndef SNOW_LIT_DEPTHNORMALS_PASS_INCLUDED
#define SNOW_LIT_DEPTHNORMALS_PASS_INCLUDED

#include "SnowLitForwardPass.hlsl"

struct DepthNormalsAttributes
{
    float4 positionOS : POSITION;
    float2 ringId     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DepthNormalsVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

DepthNormalsVaryings SnowDepthNormalsVertex(DepthNormalsAttributes IN)
{
    DepthNormalsVaryings OUT = (DepthNormalsVaryings)0;

    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

    float h;
    float3 flat = positionWS;

    positionWS = SnowDisplacedPositionWS(positionWS, IN.ringId.x, h);

    if (IN.ringId.y > 0.5 && IN.ringId.y < 1.5 && _SnowSkirtOff < 0.5) positionWS.y -= SNOW_SKIRT_DEPTH;
    if (IN.ringId.y > 1.5 && _SnowStitchOff < 0.5) positionWS.y = SnowStitchedWorldY(flat.xz, IN.ringId.x);

    OUT.positionWS = positionWS;
    OUT.positionCS = TransformWorldToHClip(positionWS);

    return OUT;
}

half4 SnowDepthNormalsFragment(DepthNormalsVaryings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);

    // İLERİ GEÇİŞLE AYNI KURULUM. SSAO bu tamponu okuyor; detay normalleri
    // burada yoksa karın üstündeki AO yüzeyin gerçek eğimini görmez.
    float3 N;
    SnowSurface surface;
    float height;

    SnowShadeSetup(IN.positionWS, N, surface, height);

    return half4(NormalizeNormalPerPixel(N), 0.0);
}

#endif
