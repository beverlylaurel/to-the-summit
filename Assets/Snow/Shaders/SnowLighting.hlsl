// ROL: kar yüzeyinin ışıklandırması (spec §14.1, §14.3).
// Çağıran: SnowLitForwardPass.

#ifndef SNOW_LIGHTING_INCLUDED
#define SNOW_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SnowLitInput.hlsl"
#include "SnowSparkle.hlsl"

/// Bir tekseldeki karın ışıkla ilgili her şeyi. Tek yerde toplanıyor ki
/// doğrudan ışık, ek ışıklar ve ortam aynı yüzeyi görsün.
struct SnowSurface
{
    half3  albedo;
    half   roughness;
    half   wet;
    half   disturb;
    half   crust;
    float  snowDepth;
    float3 positionWS;
    float  pixelFootprint;
    BRDFData brdfData;
};

/// YÜZEY PARAMETRELERİ (spec §14.1).
///
/// Ölçüm: taze kuru kar albedosu 0.80–0.90, eski/sıkışmış 0.45–0.70.
SnowSurface SnowBuildSurface(float rhoN, float wet, float disturb, float crust,
                             float snowDepth, float3 positionWS, float pixelFootprint)
{
    float freshness = 1.0 - saturate((SnowDensity(rhoN) - 100.0) / 350.0);

    const half3 ALBEDO_FRESH  = half3(0.90, 0.92, 0.95);
    const half3 ALBEDO_PACKED = half3(0.70, 0.73, 0.79);
    const half3 TINT_WET      = half3(0.84, 0.86, 0.89);

    SnowSurface s;

    s.albedo    = lerp(ALBEDO_PACKED, ALBEDO_FRESH, freshness) * lerp(half3(1, 1, 1), TINT_WET, wet);
    s.roughness = lerp(0.26, 0.48, freshness) * lerp(1.0, 0.38, wet);

    // KABUK BUZDUR (spec §18.3). Daha parlak, daha az parıldar. Faz 11'e
    // kadar `crust` sıfır kalıyor ve bu satırlar hiçbir şey yapmıyor.
    half crustMask = saturate((crust - 0.35) / 0.35);
    s.roughness = lerp(s.roughness, 0.12, crustMask);
    s.albedo    = lerp(s.albedo, s.albedo * half3(0.93, 0.95, 1.00), crustMask);

    s.wet = wet;
    s.disturb = disturb;
    s.crust = crustMask;
    s.snowDepth = snowDepth;
    s.positionWS = positionWS;
    s.pixelFootprint = pixelFootprint;

    // `alpha` PARAMETRESİ `inout`; sabit geçilemez. Kar opak, değer geri
    // okunmuyor ama derleyicinin l-value istemesi bir yerel gerektiriyor.
    half alpha = 1.0h;
    InitializeBRDFData(s.albedo, 0.0h, half3(0, 0, 0), 1.0h - s.roughness, alpha, s.brdfData);

    return s;
}

/// DOĞRUDAN IŞIK (spec §14.3).
///
/// Sarmalı NdotL: kar yarı saydam, ışık yüzeyin altına girip yandan çıkıyor.
/// Sert bir NdotL karı plastik gösterir.
half3 SnowDirectLight(Light L, float3 N, float3 V, SnowSurface s)
{
    const half W = 0.55;

    half wrapNdotL = saturate((dot(N, L.direction) + W) / (1.0 + W));
    half3 diffuse = s.albedo * wrapNdotL;

    // Arkadan aydınlanma: ince karda ışık öbür taraftan sızıyor.
    half back  = saturate(dot(V, -L.direction));
    half trans = pow(back, 3.0) * exp(-s.snowDepth * 7.0) * _TranslucencyStrength;
    diffuse += s.albedo * trans * half3(1.00, 1.02, 1.10);

    half3 spec = DirectBRDFSpecular(s.brdfData, N, L.direction, V);

    // PARILTI SADECE GÜNDÜZ. `_SunElevation01` gündöngüsünden geliyor;
    // uygulanmazsa gece kar parıldar (spec §22).
    half sunGate = saturate(_SunElevation01 * 20.0);

    half sparkle = 0;

#if !defined(_SNOW_QUALITY_LOW)
    sparkle = SnowSparkle(s.positionWS, V, L.direction, s.pixelFootprint)
            * (1.0 - s.wet) * (1.0 - s.disturb * 0.85)
            * (1.0 - s.crust * 0.7)
            * saturate(dot(N, L.direction) * 4.0) * sunGate;
#endif

    half3 lightCol = L.color * (L.distanceAttenuation * L.shadowAttenuation);

    return (diffuse + spec + sparkle * _SparkleIntensity) * lightCol;
}

/// ORTAM (spec §14.3) [KAYNAK: Batman GDC 2014 — geçiş bölgesinde diffuse'u
/// gökyüzü rengiyle tint'leyerek sahte SSS].
///
/// GECE KAR KOYU OLUR. Ortam düşükse kar da koyu; bu doğru davranış. Karı
/// gece aydınlatmak için `_ShadowTint`'i veya ambient'i yükseltmek yasak.
half3 SnowAmbient(float3 N, SnowSurface s, half mainShadow)
{
    half3 ambient = SampleSH(N) * s.albedo;

    half shadowed = 1.0 - mainShadow;
    ambient *= lerp(half3(1, 1, 1), (half3)_ShadowTint.rgb, shadowed);

    return ambient;
}

#endif
