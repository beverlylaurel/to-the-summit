#ifndef SNOW_LIT_FORWARD_PASS_INCLUDED
#define SNOW_LIT_FORWARD_PASS_INCLUDED

// ROL: kar yüzeyinin vertex yer değiştirmesi ve ışıklandırması.
// Çağıran: SnowLit.shader (ForwardLit, DepthOnly, DepthNormals geçişleri).

#include "SnowLitInput.hlsl"
#include "SnowLighting.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
};

/// Yüzeyi zeminin üstüne, kar derinliği kadar kaldırır (§7.2).
///
/// KAMERA UZAKLIĞINA GÖRE YER DEĞİŞTİRME KISILMIYOR — kısılsaydı yüzey oyuncu
/// yaklaştıkça kayardı. Detay seviyesini halkanın kendi quad boyu zaten hallediyor.
float3 SnowSurfacePosition(float3 positionOS)
{
    float3 posWS = TransformObjectToWorld(positionOS);

    posWS.y = SampleGroundHeight(posWS.xz) + SnowHeightAt(SnowWorldToUV(posWS));
    return posWS;
}

Varyings SnowVertex(Attributes input)
{
    Varyings output;
    output.positionWS = SnowSurfacePosition(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(output.positionWS);
    return output;
}

/// Yüzey normali FRAGMENT'ta, merkezi farkla (§7.3).
///
/// Vertex'te hesaplanamaz: düşük çözünürlüklü dış halkalarda quad 67 cm ve normal
/// tamamen çöküyor.
float3 SnowGeometryNormal(float3 positionWS, out float spacing)
{
    float2 uv = SnowWorldToUV(positionWS);

    // Örnekleme aralığı MESAFEYLE BÜYÜYOR. Sabit bir teksel aralığı uzakta teksel
    // gürültüsünü büyütüp TAA'da kaynayan bir yüzey üretiyor.
    float t = max(1.0 / _SnowResolution, length(fwidth(uv)) * 0.5);

    // Metre cinsinden gerçek aralık. Spec burada tek tekselin boyunu yazıyor ama
    // aralık mesafeyle büyüdüğü için sabit boy eğimi olduğundan büyük gösterirdi.
    spacing = t * _SnowAreaSize;

    float hL = SnowHeightAt(uv - float2(t, 0.0));
    float hR = SnowHeightAt(uv + float2(t, 0.0));
    float hD = SnowHeightAt(uv - float2(0.0, t));
    float hU = SnowHeightAt(uv + float2(0.0, t));

    float3 snowNormal = normalize(float3(hL - hR, 2.0 * spacing, hD - hU));

    // Kar inceyken arazi eğimi baskın.
    float3 groundNormal = SampleGroundNormal(positionWS.xz, max(spacing, 0.5));
    float here = SnowHeightAt(uv);

    return normalize(lerp(groundNormal, snowNormal, saturate(here / 0.08)));
}

half4 SnowFragment(Varyings input) : SV_Target
{
    float spacing;
    float3 geometryNormal = SnowGeometryNormal(input.positionWS, spacing);

    float4 state = SnowStateAt(SnowWorldToUV(input.positionWS));

    // Piksel başına düşen dünya mesafesi. Parıltının LOD'u ve mikro detayın sönümü
    // bundan çıkıyor.
    float footprint = length(fwidth(input.positionWS.xz));
    float viewDistance = length(GetCameraPositionWS() - input.positionWS);

    SnowSurfaceData surface = SnowBuildSurface(state, input.positionWS, footprint,
                                               _AlbedoFresh.rgb, _AlbedoPacked.rgb, _TintWet.rgb);

    float3 N = SnowDetailNormal(geometryNormal, input.positionWS,
                                surface.freshness, surface.disturb,
                                viewDistance, _WindDetailStrength);

    float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);

    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    half3 color = SnowDirectLight(mainLight, N, V, surface,
                                  _SparkleIntensity, _TranslucencyStrength,
                                  _SparkleCellSize, _SparkleDensity, _SparkleSharpness);

    color += SnowAmbient(N, surface, mainLight.shadowAttenuation, _ShadowTint.rgb);

#if defined(_ADDITIONAL_LIGHTS)
    // LIGHT_LOOP_BEGIN makrosu `inputData`yı ADIYLA okuyor (_CLUSTER_LIGHT_LOOP açıkken
    // ekran uzayı UV'sinden küme indeksini çıkarıyor). Kendi ışık döngümüzü
    // yazıyoruz ama makronun beklediği değişken yine de kurulmak zorunda.
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.normalWS = N;
    inputData.viewDirectionWS = V;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    uint lightCount = GetAdditionalLightsCount();

    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));

        color += SnowDirectLight(light, N, V, surface,
                                 _SparkleIntensity, _TranslucencyStrength,
                                 _SparkleCellSize, _SparkleDensity, _SparkleSharpness);
    LIGHT_LOOP_END
#endif

    return half4(color, 1.0);
}

// --- derinlik geçişleri ---

float4 SnowDepthVertex(Attributes input) : SV_POSITION
{
    return TransformWorldToHClip(SnowSurfacePosition(input.positionOS.xyz));
}

half4 SnowDepthFragment() : SV_Target
{
    return 0;
}

Varyings SnowDepthNormalsVertex(Attributes input)
{
    return SnowVertex(input);
}

half4 SnowDepthNormalsFragment(Varyings input) : SV_Target
{
    // DERİNLİK GEÇİŞİ AYNI YER DEĞİŞTİRMEYİ UYGULAMAK ZORUNDA. Biri atlanırsa SSAO
    // yüzeyin altından okur ve hayalet gölge basar.
    float spacing;
    return half4(SnowGeometryNormal(input.positionWS, spacing), 0.0);
}

#endif
