// ROL: kar yüzeyinin gölge geçişi. Yer değiştirme ve kenar kesme ileri
// geçişle AYNI olmak zorunda; yoksa kar kendi gölgesini yanlış yerden atar.
// Çağıran: SnowLit.shader.

#ifndef SNOW_LIT_SHADOW_PASS_INCLUDED
#define SNOW_LIT_SHADOW_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "SnowLitForwardPass.hlsl"

struct ShadowAttributes
{
    float4 positionOS : POSITION;
    float2 ringId     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

ShadowVaryings SnowShadowVertex(ShadowAttributes IN)
{
    ShadowVaryings OUT = (ShadowVaryings)0;

    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

    float h;
    positionWS = SnowDisplacedPositionWS(positionWS, IN.ringId.x, h);

    OUT.positionWS = positionWS;

    // Normal karın kendi yüzeyinden; gölge sapması ona göre uygulanıyor.
    // ADIM SABİT: `fwidth` vertex shader'da derlenmiyor, tek teksel kullanılıyor.
    float2 uv = SnowWorldToUV(positionWS);
    float3 normalWS = SnowNormalAtStep(uv, 1.0 / _SnowResolution, h, positionWS);

    float3 lightDir = _MainLightPosition.xyz;
    OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));

#if UNITY_REVERSED_Z
    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif

    return OUT;
}

half4 SnowShadowFragment(ShadowVaryings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);

    // AYNI KESME. İleri geçişte kesilen yer burada da kesilmeli, yoksa
    // görünmeyen kar görünen gölge atar.
    float2 uv = SnowWorldToUV(IN.positionWS);
    SnowClipEdge(SnowSurfaceAt(uv), IN.positionWS);

    return 0;
}

#endif
