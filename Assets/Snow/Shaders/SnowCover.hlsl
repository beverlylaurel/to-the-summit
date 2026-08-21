#ifndef SNOW_COVER_INCLUDED
#define SNOW_COVER_INCLUDED

// ROL: nesnelerin üstünde kar tutması (§9). Başka shader'lara include edilebilir.
// Çağıran: SnowCoverObject.shader.

#include "SnowCommon.hlsl"
#include "SnowDetailNormals.hlsl"   // SnowValueNoise

float _SnowSlopeThreshold;
float _SnowSlopeSharpness;
float _SnowBreakupScale;
float _SnowBreakupStrength;
float _SnowEdgeSharpness;
float _SnowEdgeBulge;
float _SnowThickness;

/// Hava sürücüsünden geliyor: 0 = kar yok, 1 = tam kaplı. Global — sahnedeki her
/// yüzey aynısını okumalı.
float _SnowCoverage;

/// Nesnenin üstünde ne kadar kar var (§9).
///
// ASSUMPTION: §9 kırılma gürültüsünü `_SnowBreakup` dokusundan okuyor ama spec'in
// dosya listesinde böyle bir doku yok. Aynı işi prosedürel değer gürültüsü yapıyor:
// tekrar deseni yok, asset bağımlılığı yok. Ölçek ve şiddet parametreleri aynı.
float SnowCoverMask(float3 posWS, float3 N, float ao)
{
    float3 up = _SnowUpDirection;

    // 1. Eğim: kar yatay yüzeylerde birikir.
    float slope = dot(N, up);
    float slopeMask = saturate((slope - _SnowSlopeThreshold) / max(1.0 - _SnowSlopeThreshold, 1e-3));
    slopeMask = pow(slopeMask, _SnowSlopeSharpness);

    // 2. Gökyüzü görünürlüğü: çatı altına kar yağmaz.
    float skyVis = SampleSkyVisibility(posWS);

    // 3. Cavity: girintilerde kar tutmaz, çıkıntılarda tutar.
    float cavity = saturate(ao * 1.35 - 0.35);

    // 4. Kırılma gürültüsü: kenarlar düz çizgi olmasın.
    float noise = SnowValueNoise(posWS.xz * _SnowBreakupScale);
    noise = lerp(0.5, noise, _SnowBreakupStrength);

    float raw = slopeMask * skyVis * cavity * _SnowCoverage * 1.7;
    return saturate((raw - noise) * _SnowEdgeSharpness);
}

/// Maskeye göre normali karın yönüne büküyor ve kenarı yuvarlıyor.
float3 SnowCoverNormal(float3 N, float mask)
{
    float3 up = _SnowUpDirection;

    N = normalize(lerp(N, up, pow(mask, 0.55)));

    // Kenar yuvarlaklığı: maskenin gradyanı yönünde bükme. Kar tabakasının kenarı
    // keskin bir duvar değil, yuvarlanan bir sırt.
    float2 g = float2(ddx(mask), ddy(mask));
    return normalize(N + _SnowEdgeBulge * float3(g.x, 0, g.y));
}

#endif
