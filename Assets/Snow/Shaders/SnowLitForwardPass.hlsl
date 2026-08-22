// ROL: kar yüzeyinin köşe yer değiştirmesi, normali ve ışıklandırması.
// Çağıran: SnowLit.shader.

#ifndef SNOW_LIT_FORWARD_PASS_INCLUDED
#define SNOW_LIT_FORWARD_PASS_INCLUDED

#include "SnowLighting.hlsl"
#include "../../Shaders/HeightFog.hlsl"

#include "SnowDetailNormals.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
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

/// KÖŞE YER DEĞİŞTİRMESİ (spec §8.3).
///
/// Kamera mesafesine göre kısma YOK — kısılırsa yüzey kayar (spec §8.3).
float3 SnowDisplacedPositionWS(float3 positionWS, out float heightOut)
{
    float groundY = SampleGroundHeight(positionWS.xz);

    // `SnowSurfaceAt` spec §8.3'ün `SnowSurfaceHeight` + sastrugi + kenar
    // sönümü zincirini tek yerde topluyor. Fragment normali de aynı
    // fonksiyondan türüyor (spec §8.6); ikisi ayrı yazılırsa geometri ile
    // normal farklı yüzeyi tarif eder.
    float h = SnowSurfaceAt(SnowWorldToUV(positionWS));

    heightOut = h;
    positionWS.y = groundY + h;

    return positionWS;
}

Varyings SnowLitVertex(Attributes IN)
{
    Varyings OUT = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

    float h;
    positionWS = SnowDisplacedPositionWS(positionWS, h);

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

    // İnce karda zeminin şekli baskın; kalınlaştıkça karın kendi yüzeyi
    // (spec §8.6).
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

/// KAR NEREDE ÇİZİLMEZ (spec §8.4).
///
/// Üç kural, üçü de zorunlu. Birincisi köşe shader'ında (kenar sönümü);
/// ikisi burada:
///
/// 2. 4 mm'nin altındaki kar hiç çizilmiyor. Hem mesh kenarını hem karın
///    eridiği yerleri z-fighting'siz çözüyor.
/// 3. Gürültülü bitiş `[KAYNAK: Company of Heroes 2, KGC 2013]` — kar kenarı
///    düz çizgi değil, gürültülü lekeler hâlinde bitiyor.
void SnowClipEdge(float h, float3 positionWS)
{
    clip(h - SNOW_MIN_VISIBLE_HEIGHT);

    float edgeFade = saturate((h - SNOW_MIN_VISIBLE_HEIGHT)
                              / max(_SnowEdgeFadeRange, 1e-4));

    float breakup = SAMPLE_TEXTURE2D(_SnowBreakup, sampler_SnowBreakup,
                                     positionWS.xz * _SnowBreakupScale).r;

    clip(edgeFade - breakup * 0.6);
}

/// Fragman'da ortak kurulum: kesme, normal, yüzey. Hem ileri geçiş hem
/// DepthNormals aynı yüzeyi görmeli.
void SnowShadeSetup(float3 positionWS, out float3 N, out SnowSurface surface, out float height)
{
    float2 uv = SnowWorldToUV(positionWS);
    height = SnowSurfaceAt(uv);

    SnowClipEdge(height, positionWS);

    float4 state = SnowStateAt(uv);
    float4 trail = SnowTrailAt(uv);

    float dist = length(GetCameraPositionWS() - positionWS);
    float footprint = length(fwidth(positionWS.xz));

    float freshness = 1.0 - saturate((SnowDensity(state.g) - 100.0) / 350.0);

    N = SnowNormalAt(uv, height, positionWS);
    // DÜZLEMSEL XZ KAPLAMA DİK YÜZEYDE EZİLİYOR (rapor §2). Yüzey dikleştikçe
    // XZ izdüşümü sıfıra yaklaşıyor, doku dikey şeritler hâlinde uzuyor.
    // Detay normalleri yataylık oranıyla ağırlıklandırılıyor — kar zaten
    // yataya yakın yüzeyde durur, dolayısıyla kaybedilen bir şey yok.
    float planar = saturate((N.y - 0.35) / 0.35);

    // Detay yalnız yataya yakın yüzeyde (rapor §2): dik kenarda düzlemsel XZ
    // kaplaması dikey şeritler hâlinde uzuyor.
    float3 detailed = SnowApplyDetailNormals(N, positionWS, freshness, state.a, dist);
    N = normalize(lerp(N, detailed, planar));

    surface = SnowBuildSurface(state.g, state.b, state.a, trail.b,
                               height, positionWS, footprint);
}

half4 SnowLitFragment(Varyings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);

    float3 N;
    SnowSurface surface;
    float height;

    SnowShadeSetup(IN.positionWS, N, surface, height);

    float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

    Light mainLight = GetMainLight(IN.shadowCoord);

    half heightAO = SnowHeightAO(SnowWorldToUV(IN.positionWS), height);

    half3 color = SnowDirectLight(mainLight, N, V, surface);
    color += SnowAmbient(N, surface, mainLight.shadowAttenuation, heightAO);

#if defined(_ADDITIONAL_LIGHTS)
    // Forward+ kümeleme `inputData`'nın alanlarını okuyor; makro onu isimle
    // arıyor.
    InputData inputData = (InputData)0;
    inputData.positionWS = IN.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

    uint pixelLightCount = GetAdditionalLightsCount();

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
        color += SnowDirectLight(light, N, V, surface);
    LIGHT_LOOP_END
#endif

    // MEVCUT SİSİN KENDİSİ (spec §14). Kendi sis hesabımız yok.
    // PROJENİN SİSİ, URP'NİNKİ DEĞİL (rapor §7). Dağ `ApplyHeightFog`
    // kullanıyor; kar mesh'i `MixFog` kullandığı için sınırda farklı
    // parlaklıkta ayrışıyordu.
    color = ApplyHeightFog(color, GetCameraPositionWS(), IN.positionWS);

    return half4(color, 1.0);
}

#endif
