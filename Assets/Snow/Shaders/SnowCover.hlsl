// ROL: nesnelerin üstünde kar birikmesinin maskesi ve uygulanması (spec §16).
// Çağıran: SnowCoverObject.shader; mevcut nesne shader'ları isterse.

#ifndef SNOW_COVER_INCLUDED
#define SNOW_COVER_INCLUDED

#include "SnowCommon.hlsl"
#include "../../Shaders/StochasticTiling.hlsl"

TEXTURE2D(_SnowBreakup);
SAMPLER(sampler_SnowBreakup);

/// Zeminde ne kadar kar varsa nesnelerde de o kadar. `SnowCoverageDriver`
/// besliyor; ayrı bir kaynak kurulmuyor.
float _SnowCoverage;

/// ÖRTÜ AYARLARI GLOBAL (spec §16). Arazinin kar katmanı da nesne shader'ı da
/// bunları okuyor; `SnowCoverageDriver` tek sahibi.
float _SnowCoverSlopeSharpness;
float _SnowCoverBreakupStrength;
float _SnowCoverEdgeSharpness;
float _SnowCoverThickness;

/// KAR YATAYA YAKIN YÜZEYLERDE BİRİKİR
/// [KAYNAK: Company of Heroes 2, KGC 2013].
///
/// Dört çarpan: eğim, gökyüzünü görme, oyuk (AO), ve gürültü. Gökyüzü
/// çarpanı olmadan çatının ALTI da karlanır — en çok fark edilen hata.
/// ÖRNEKLEME DIŞARIDA. `SAMPLE_TEXTURE2D` örtük türev kullanıyor ve compute
/// shader'da derlenmiyor; gürültü parametre olarak alınınca maskenin mantığı
/// hem fragman'dan hem sınamadan aynen çağrılabiliyor.
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
    // STOKASTİK DÖŞEME — düz döşemenin ızgarası burada da görünüyordu.
    float noise = SampleStochasticMask(TEXTURE2D_ARGS(_SnowBreakup, sampler_SnowBreakup),
                                       posWS.xz * breakupScale);

    return SnowCoverMaskWithNoise(posWS, N, ao, noise,
                                  slopeThreshold, slopeSharpness,
                                  breakupStrength, edgeSharpness);
}

/// Maskenin kenarını şişirerek kar birikintisinin kalınlığını gösteriyor.
/// Sadece normal işi — geometri değişmiyor.
float3 SnowCoverNormal(float3 N, float mask, float edgeBulge)
{
    float3 up = _SnowUpDirection;

    N = normalize(lerp(N, up, pow(mask, 0.55)));

    float2 g = float2(ddx(mask), ddy(mask));
    return normalize(N + edgeBulge * float3(g.x, 0.0, g.y));
}

#endif
