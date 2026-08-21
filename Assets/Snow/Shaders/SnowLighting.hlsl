#ifndef SNOW_LIGHTING_INCLUDED
#define SNOW_LIGHTING_INCLUDED

// ROL: karın kendi ışıklandırma döngüsü (§8.3).
// UniversalFragmentPBR KULLANILMIYOR: sarmalanmış diffüz ve parıltı hazır fonksiyonun
// içine enjekte edilemiyor. Parçalar yine URP'nin kendi fonksiyonları.
// Çağıran: SnowLitForwardPass.hlsl.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SnowSparkle.hlsl"
#include "SnowDetailNormals.hlsl"

/// Yüzeyin o pikseldeki hâli.
struct SnowSurfaceData
{
    half3 albedo;
    half  roughness;
    half  wet;
    half  disturb;
    half  freshness;
    float depth;             // kar derinliği, metre
    float3 positionWS;
    float  pixelFootprint;   // piksel başına düşen dünya mesafesi
    BRDFData brdf;
};

/// TEK IŞIK KATKISI.
///
/// Sarmalanmış diffüz (§8.3): kar çok saçılmalı bir ortam, ışık yüzeyin altına girip
/// yanlardan çıkıyor. Lambert kullanılırsa terminatör keskin çıkar ve kar plastik
/// görünür.
half3 SnowDirectLight(Light light, float3 N, float3 V, SnowSurfaceData s,
                      half sparkleIntensity, half translucencyStrength,
                      float sparkleCellSize, float sparkleDensity, float sparkleSharpness)
{
    const half W = 0.55;

    half wrapNdotL = saturate((dot(N, light.direction) + W) / (1.0 + W));
    half3 diffuse = s.albedo * wrapNdotL;

    // Arkadan geçirgenlik: ince karın kenarları ışığa karşı parlıyor. Derinlik
    // arttıkça üstel olarak sönüyor — 15 cm'de pratik olarak yok.
    half back  = saturate(dot(V, -light.direction));
    half trans = pow(back, 3.0) * exp(-s.depth * 7.0) * translucencyStrength;
    diffuse += s.albedo * trans * half3(1.00, 1.02, 1.10);

    half3 spec = DirectBRDFSpecular(s.brdf, N, light.direction, V);

    // PARILTI YALNIZ: doğrudan güneşte, kuru karda, bozulmamış yüzeyde.
    half sparkle = 0;

#if !defined(_SNOW_QUALITY_LOW)
    sparkle = SnowSparkle(s.positionWS, N, V, light.direction, s.pixelFootprint,
                          sparkleCellSize, sparkleDensity, sparkleSharpness)
            * (1.0 - s.wet) * (1.0 - s.disturb * 0.85)
            * saturate(dot(N, light.direction) * 4.0);
#endif

    half3 lightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);

    // Gölgede parıltı olmaz — çarpım bunu zaten hallediyor.
    return (diffuse + spec + sparkle * sparkleIntensity) * lightColor;
}

/// Ortam ışığı. KARIN GÖLGESİ MAVİDİR çünkü orada yalnız gökyüzünden ışık geliyor.
half3 SnowAmbient(float3 N, SnowSurfaceData s, half shadowAttenuation, half3 shadowTint)
{
    half3 ambient = SampleSH(N) * s.albedo;
    half shadowed = 1.0 - shadowAttenuation;

    return ambient * lerp(half3(1, 1, 1), shadowTint, shadowed);
}

/// §8.1 yüzey parametreleri. Taze toz ile sıkışmış kar arasında hem albedo hem
/// pürüzlülük ayrışıyor; izin içi gözle görülür şekilde daha koyu ve daha parlak.
SnowSurfaceData SnowBuildSurface(float4 state, float3 positionWS, float pixelFootprint,
                                 half3 albedoFresh, half3 albedoPacked, half3 tintWet)
{
    SnowSurfaceData s;

    float rho = SnowDensity(state.g);

    s.freshness = (half)(1.0 - saturate((rho - 100.0) / 350.0));
    s.wet = (half)saturate(state.b);
    s.disturb = (half)saturate(state.a);
    s.depth = SnowHeight(state.r, state.g);
    s.positionWS = positionWS;
    s.pixelFootprint = pixelFootprint;

    s.albedo = lerp(albedoPacked, albedoFresh, s.freshness);
    s.albedo *= lerp(half3(1, 1, 1), tintWet, s.wet);

    s.roughness = lerp(0.26, 0.48, s.freshness) * lerp(1.0, 0.38, s.wet);

    // Metalik sıfır: kar iletken değil. alpha `inout` olduğu için değişken şart.
    half alpha = 1.0h;
    InitializeBRDFData(s.albedo, 0.0h, half3(0, 0, 0), (half)(1.0 - s.roughness), alpha, s.brdf);

    return s;
}

#endif
