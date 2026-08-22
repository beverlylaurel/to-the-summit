// ROL: kar yüzeyinin köşe yer değiştirmesi, normali ve (Faz 4'te geçici)
// ışıklandırması. Faz 6'da ışıklandırma SnowLighting.hlsl'e taşınacak.
// Çağıran: SnowLit.shader.

#ifndef SNOW_LIT_FORWARD_PASS_INCLUDED
#define SNOW_LIT_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SnowLitInput.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float2 ringId     : TEXCOORD0;      // x = halka indeksi
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float  snowHeight : TEXCOORD1;
    float4 shadowCoord : TEXCOORD2;
    float  fogFactor  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

/// KÖŞE YER DEĞİŞTİRMESİ (spec §13.2).
///
/// Kamera mesafesine göre kısma YOK — kısılırsa yüzey oyuncu yaklaştıkça
/// kayar ve dalgalanır.
float3 SnowDisplacedPositionWS(float3 positionWS, float ringIndex, out float heightOut)
{
    float groundY = SampleGroundHeight(positionWS.xz);
    float2 uv     = SnowWorldToUV(positionWS);

    float h = SnowSurfaceAt(uv);

    heightOut = h;

    // DIŞ HALKALAR BİR TIK AŞAĞIDA. Halkalar kendi ızgaralarına snap'lendiği
    // için sınırda birkaç santimlik kaplama kalıyor; orada iç halka derinlik
    // testini kazansın diye dış halka milimetrik itiliyor. Gerekçe
    // `DECISIONS.md`.
    positionWS.y = groundY + h - ringIndex * SNOW_RING_DEPTH_BIAS;

    return positionWS;
}

Varyings SnowLitVertex(Attributes IN)
{
    Varyings OUT = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

    float h;
    positionWS = SnowDisplacedPositionWS(positionWS, IN.ringId.x, h);

    OUT.positionWS = positionWS;
    OUT.snowHeight = h;
    OUT.positionCS = TransformWorldToHClip(positionWS);
    OUT.shadowCoord = TransformWorldToShadowCoord(positionWS);
    OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

    return OUT;
}

/// Merkezi fark, adım DIŞARIDAN veriliyor. Vertex shader'ından çağrılabilen
/// hâli bu: `fwidth` yalnız fragman komutudur ve vertex'te derlenmez.
float3 SnowNormalAtStep(float2 uv, float t, float hHere, float3 positionWS)
{
    float ms = SnowTexelSize();

    float hL = SnowSurfaceAt(uv - float2(t, 0.0));
    float hR = SnowSurfaceAt(uv + float2(t, 0.0));
    float hD = SnowSurfaceAt(uv - float2(0.0, t));
    float hU = SnowSurfaceAt(uv + float2(0.0, t));

    float3 nSnow   = normalize(float3(hL - hR, 2.0 * ms, hD - hU));
    float3 nGround = SampleGroundNormal(positionWS.xz);

    // İnce karda zeminin şekli baskın; kalınlaştıkça karın kendi yüzeyi.
    return normalize(lerp(nGround, nSnow, saturate(hHere / 0.08)));
}

/// NORMAL FRAGMENT'TA, MERKEZİ FARKLA (spec §13.3). Vertex'te hesaplanırsa
/// normal quad başına sabit kalır ve yüzey bloklu görünür (spec §22).
/// Adım piksel ayak izinden büyür — uzakta örnekleme aralığı genişleyince
/// normal kaynamaz.
float3 SnowNormalAt(float2 uv, float hHere, float3 positionWS)
{
    float t = max(1.0 / _SnowResolution, length(fwidth(uv)) * 0.5);
    return SnowNormalAtStep(uv, t, hHere, positionWS);
}

/// KAR NEREDE ÇİZİLMEZ (spec §8.1, §8.2).
///
/// 4 mm altındaki kar hiç çizilmiyor: z-fighting tamamen ortadan kalkıyor ve
/// kar araziye kaybolarak karışıyor. Kenar düz çizgi olmasın diye eşiğin
/// hemen üstünde gürültüyle kırılıyor.
void SnowClipEdge(float h, float3 positionWS)
{
    clip(h - SNOW_MIN_VISIBLE_HEIGHT);

    float edgeFade = saturate((h - SNOW_MIN_VISIBLE_HEIGHT) / _SnowEdgeFadeRange);

    float breakup = SAMPLE_TEXTURE2D(_SnowBreakup, sampler_SnowBreakup,
                                     positionWS.xz * _SnowBreakupScale).r;

    clip(edgeFade - breakup * 0.6);
}

half4 SnowLitFragment(Varyings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);

    float2 uv = SnowWorldToUV(IN.positionWS);
    float  h  = SnowSurfaceAt(uv);

    SnowClipEdge(h, IN.positionWS);

    float3 N = SnowNormalAt(uv, h, IN.positionWS);

    // FAZ 4 GEÇİCİ IŞIKLANDIRMASI. Spec §14'ün tam modeli (sarmalı NdotL,
    // yarı saydamlık, parıltı, RNM detay normalleri) Faz 6'da geliyor.
    // Buradaki tek amaç geometriyi ve normali görünür kılmak.
    const half3 ALBEDO = half3(0.90, 0.92, 0.95);

    Light mainLight = GetMainLight(IN.shadowCoord);

    half ndotl = saturate(dot(N, mainLight.direction));
    half3 direct = ALBEDO * ndotl * mainLight.color * mainLight.shadowAttenuation;
    half3 ambient = SampleSH(N) * ALBEDO;

    half3 color = direct + ambient;

    // MEVCUT SİSİN KENDİSİ (spec §14). Kendi sis hesabımız yok.
    color = MixFog(color, IN.fogFactor);

    return half4(color, 1.0);
}

#endif
