// ROL: kar yüzeyinin ışıklandırması (spec §14.1, §14.3).
// Çağıran: SnowLitForwardPass.

#ifndef SNOW_LIGHTING_INCLUDED
#define SNOW_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SnowLitInput.hlsl"
#include "SnowSparkle.hlsl"
#include "SnowSurfaceTextures.hlsl"

/// Bir tekseldeki karın ışıkla ilgili her şeyi. Tek yerde toplanıyor ki
/// doğrudan ışık, ek ışıklar ve ortam aynı yüzeyi görsün.
struct SnowSurface
{
    half3  albedo;
    half   roughness;
    half   wet;
    half   disturb;
    half   crust;
    half2  surfSlope;   // yüzey dokusunun eğim uzayındaki detayı
    float  snowDepth;
    float3 positionWS;
    float  pixelFootprint;
    BRDFData brdfData;
};

/// YÜZEY PARAMETRELERİ (spec §14.1).
///
/// Ölçüm: taze kuru kar albedosu 0.80–0.90, eski/sıkışmış 0.45–0.70.
/// ÖRNEKLENMİŞ HARMANLA KURULUM.
///
/// Doku okuması dışarıda kalıyor: arazi yüzey dokusunu zaten kendi
/// albedo/normal bloğunda okuyor, `SnowBuildSurface` içinde ikinci kez
/// örneklenince aynı piksel için on iki doku erişimi iki katına çıkıyordu.
SnowSurface SnowBuildSurfaceFrom(SnowSurfaceBlend yuzey,
                                 float rhoN, float wet, float disturb, float crust,
                                 float snowDepth, float3 positionWS, float pixelFootprint)
{
    float freshness = 1.0 - saturate((SnowDensity(rhoN) - 100.0) / 350.0);

    const half3 ALBEDO_FRESH  = half3(0.90, 0.92, 0.95);
    const half3 ALBEDO_PACKED = half3(0.70, 0.73, 0.79);
    const half3 TINT_WET      = half3(0.84, 0.86, 0.89);

    SnowSurface s;

    s.albedo    = lerp(ALBEDO_PACKED, ALBEDO_FRESH, freshness) * lerp(half3(1, 1, 1), TINT_WET, wet);
    s.roughness = lerp(0.26, 0.48, freshness) * lerp(1.0, 0.38, wet);

    // YÜZEY DOKUSU. Dört fotogrametri seti durum zincirinden harmanlanıyor
    // (gerekçe `SnowSurfaceTextures.hlsl`). Albedonun SEVİYESİ yukarıdaki
    // fizikten geliyor; doku yalnız deseni çarpan olarak ekliyor.
    s.albedo    = saturate(s.albedo * yuzey.albedoTint);
    s.roughness = saturate(s.roughness + yuzey.roughAdd);
    s.surfSlope = yuzey.normalSlope;

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

/// Dokuyu kendi okuyan sarmalayıcı. Kar mesh'i bunu kullanıyor: orada yüzey
/// dokusunun tek okuyucusu bu fonksiyon.
SnowSurface SnowBuildSurface(float rhoN, float wet, float disturb, float crust,
                             float snowDepth, float3 positionWS, float pixelFootprint)
{
    return SnowBuildSurfaceFrom(SnowSampleSurface(positionWS, rhoN, wet, disturb),
                                rhoN, wet, disturb, crust, snowDepth,
                                positionWS, pixelFootprint);
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

    // SPEKÜLER URP SÖZLEŞMESİYLE. `DirectBRDFSpecular` yalnız D·V SKALERİNİ
    // döndürüyor; URP onu `brdfData.specular` ile ve `NdotL` ile çarpıyor
    // (`LightingPhysicallyBased`). Spec §14.1 ikisini de yazmamış, kod da
    // sadık kalmıştı.
    //
    // Ölçüldü (öğle, 20 cm kar, düz zemin, post kapalı):
    //   diffuse 1.747   spec 4.133   sparkle 0.161   toplam 6.041
    //   aynı karede arazi karının doğrudan ışığı 0.868
    // Yani doğrudan ışığın %68'i, dielektrik karda 0.04 olması gereken bir
    // çarpanın hiç uygulanmamasından geliyordu.
    half NdotL = saturate(dot(N, L.direction));
    half3 spec = s.brdfData.specular
               * DirectBRDFSpecular(s.brdfData, N, L.direction, V) * NdotL;

    // PARILTI SADECE GÜNDÜZ. `_SunElevation01` gündöngüsünden geliyor;
    // uygulanmazsa gece kar parıldar (spec §22).
    half sunGate = saturate(_SunElevation01 * 20.0);

    half sparkle = 0;

#if !defined(_SNOW_QUALITY_LOW)
    // MESAFE KAPISI. Gerekçe `SNOW_SPARKLE_FADE_START` yanında: parıltının
    // boyutu hücre boyuna bağlı, hücre uzakta LOD ile büyüyor ve tek hücre
    // birçok pikseli kaplayınca uzaktan iri parlak lekeler çıkıyor.
    float sparkleDist = distance(s.positionWS, _WorldSpaceCameraPos);
    half distGate = 1.0h - (half)smoothstep(SNOW_SPARKLE_FADE_START,
                                            SNOW_SPARKLE_FADE_END, sparkleDist);

    if (distGate > 0.0h)
        sparkle = SnowSparkle(s.positionWS, V, L.direction, s.pixelFootprint)
                * (1.0 - s.wet) * (1.0 - s.disturb * 0.85)
                * (1.0 - s.crust * 0.7)
                * saturate(dot(N, L.direction) * 4.0) * sunGate * distGate;
#endif

    half3 lightCol = L.color * (L.distanceAttenuation * L.shadowAttenuation);

    return (diffuse + spec + sparkle * _SparkleIntensity) * lightCol;
}

/// SEKİZ YÖN. `kDir8` sekiz pusula yönü; AO bunların ufuk açısını topluyor.
static const float2 kDir8[8] =
{
    float2( 1.0,  0.0), float2( 0.7071,  0.7071),
    float2( 0.0,  1.0), float2(-0.7071,  0.7071),
    float2(-1.0,  0.0), float2(-0.7071, -0.7071),
    float2( 0.0, -1.0), float2( 0.7071, -0.7071)
};

#if defined(_SNOW_QUALITY_HIGH)
    #define SNOW_AO_DIRS  8
    #define SNOW_AO_STEPS 3
#elif defined(_SNOW_QUALITY_MEDIUM)
    #define SNOW_AO_DIRS  4
    #define SNOW_AO_STEPS 2
#else
    #define SNOW_AO_DIRS  0
    #define SNOW_AO_STEPS 0
#endif

/// İZ İÇİ AMBIENT OCCLUSION (spec §18.5)
/// [KAYNAK: Cordonnier ve ark., EG 2018, §4.1 — ortam ışığı ufuk açısı türevi]
/// [KAYNAK: Batman GDC 2014 — "AO fills-in the trails"]
///
/// Ufuk açılarının KOSİNÜS KARELERİNİN ortalaması. Makalenin türevi
/// `Isky = ksky · (π/N) · Σ cos²(φi)`; burada normalize edilmiş hâli.
///
/// URP'nin SSAO'suna DOKUNULMUYOR — o mevcut projenin render ayarı (§1.1).
/// İkisi açıksa üst üste biniyor; SSAO ekran uzayında kaba, bu ise iz
/// ölçeğinde ince çalışıyor.
half SnowHeightAO(float2 uv, float hCenter)
{
#if SNOW_AO_DIRS == 0
    return 1.0h;
#else
    // Deformasyon alanının dışında iz yok; hesaba hiç girmiyoruz.
    if (SnowInsideMask(uv) < 0.01) return 1.0h;

    float2 stepUV = _SnowAORadius / _SnowAreaSize;
    float sum = 0.0;

    [unroll]
    for (int k = 0; k < SNOW_AO_DIRS; ++k)
    {
        float maxTan = 0.0;

        [unroll]
        for (int m = 1; m <= SNOW_AO_STEPS; ++m)
        {
            float2 o = kDir8[k] * stepUV * (float)m / (float)SNOW_AO_STEPS;

            float hn = SnowSurfaceAt(uv + o);
            float dist = _SnowAORadius * (float)m / (float)SNOW_AO_STEPS;

            maxTan = max(maxTan, (hn - hCenter) / max(dist, 1e-4));
        }

        float cosPhi = rsqrt(1.0 + maxTan * maxTan);
        sum += cosPhi * cosPhi;
    }

    return (half)saturate(lerp(1.0, sum / (float)SNOW_AO_DIRS, _SnowAOStrength));
#endif
}

/// ORTAM (spec §14.3) [KAYNAK: Batman GDC 2014 — geçiş bölgesinde diffuse'u
/// gökyüzü rengiyle tint'leyerek sahte SSS].
///
/// GECE KAR KOYU OLUR. Ortam düşükse kar da koyu; bu doğru davranış. Karı
/// gece aydınlatmak için `_ShadowTint`'i veya ambient'i yükseltmek yasak.
half3 SnowAmbient(float3 N, SnowSurface s, half mainShadow, half heightAO)
{
    half3 ambient = SampleSH(N) * s.albedo;

    half shadowed = 1.0 - mainShadow;
    ambient *= lerp(half3(1, 1, 1), (half3)_ShadowTint.rgb, shadowed);

    // YALNIZ ORTAMA. Doğrudan ışığa uygulamak gölgeyi iki kez saymaktır ve
    // izleri siyah lekelere çevirir (spec §18.5, §22).
    ambient *= heightAO;

    return ambient;
}

#endif
