#ifndef SNOW_LIT_INPUT_INCLUDED
#define SNOW_LIT_INPUT_INCLUDED

// ROL: kar yüzeyi shader'ının girdileri. Materyal özellikleri UnityPerMaterial
// tamponunda (SRP Batcher şartı, §8.5); durum dokusu ve hava değerleri global.
// Çağıran: SnowLit.shader.

#include "SnowCommon.hlsl"

// Durum dokusu GLOBAL. SnowManager her karede Shader.SetGlobalTexture ile yazıyor;
// materyalin kendi ayarı değil.
TEXTURE2D(_SnowStateTex);

// Bölge dışında deformasyon yok ama kar var (§7.2).
float _FallbackSWE;
float _FallbackRhoN;

/// UZAK KASKAD (Faz 10). Yakın bölgenin dışında düz yedeğe düşmek yerine bu okunuyor;
/// at ve araba izleri 192 m'ye kadar görünür kalıyor.
TEXTURE2D(_SnowCascadeTex);
float2 _SnowCascadeCenter;
float  _SnowCascadeAreaSize;

CBUFFER_START(UnityPerMaterial)
    half4 _AlbedoFresh;
    half4 _AlbedoPacked;
    half4 _TintWet;
    half4 _ShadowTint;

    half  _TranslucencyStrength;
    half  _SparkleIntensity;
    float _SparkleCellSize;
    float _SparkleDensity;
    float _SparkleSharpness;
    float _WindDetailStrength;
CBUFFER_END

/// Bir UV'deki tam durum, bölge dışı yedeğiyle birlikte.
float4 SnowStateAt(float2 uv)
{
    float inside = SnowInsideMask(uv);
    float4 state = SAMPLE_TEXTURE2D_LOD(_SnowStateTex, snow_linear_clamp_sampler, saturate(uv), 0);

    // Bölge dışında: önce kaskad, kaskad da bitince düz yedek.
    float2 far = float2(_FallbackSWE, _FallbackRhoN);

    if (_SnowCascadeAreaSize > 0.001)
    {
        float2 world = SnowUVToWorld(uv);
        float2 cuv = (world - _SnowCascadeCenter) / _SnowCascadeAreaSize + 0.5;

        float2 edge = abs(cuv - 0.5) * 2.0;
        float cascadeInside = 1.0 - smoothstep(0.90, 1.0, max(edge.x, edge.y));

        float2 sampled = SAMPLE_TEXTURE2D_LOD(_SnowCascadeTex, snow_linear_clamp_sampler,
                                              saturate(cuv), 0).rg;

        far = lerp(far, sampled, cascadeInside);
    }

    // Islaklık ve tazelik yakın bölgeye özel: ikisi de deformasyonla ilgili ve
    // bölge dışında deformasyon yok.
    return float4(lerp(far.x, state.r, inside),
                  lerp(far.y, state.g, inside),
                  state.b * inside,
                  state.a * inside);
}

float SnowHeightAt(float2 uv)
{
    float4 state = SnowStateAt(uv);
    return SnowHeight(state.r, state.g);
}

#endif
