#ifndef TOTHESUMMIT_SURFACE_DETAIL_INCLUDED
#define TOTHESUMMIT_SURFACE_DETAIL_INCLUDED

#include "StochasticTiling.hlsl"

// ORTAK YÜZEY DETAY OKUMASI. Kar, kaya, çakıl, toprak — hepsi buradan geçiyor.
//
// Her yüzey üç harita taşıyor (normal, pürüzlülük, yükseklik) ve her biri stokastik
// dönüşümden geçmiş: Gauss histogramı + ters LUT. Bildirimleri ve okumayı yüzey başına
// elle yazmak on iki satır ediyordu; ikinci yüzeyde yirmi dört, üçüncüde otuz altı.
//
// Makrolar kullanılıyor çünkü HLSL'de doku demeti bir yapıya konamaz — TEXTURE2D
// bildirimi tür değil, isim üretir.

/// Bir yüzeyin bütün doku bildirimleri. Yüzey adı ön ek olarak geçiyor:
///   DECLARE_SURFACE_DETAIL(Rock) → _RockNormal, _RockNormalLut, ...
#define DECLARE_SURFACE_DETAIL(name)      \
    TEXTURE2D(_##name##Normal);           \
    TEXTURE2D(_##name##NormalLut);        \
    TEXTURE2D(_##name##Rough);            \
    TEXTURE2D(_##name##RoughLut);         \
    TEXTURE2D(_##name##Height);           \
    TEXTURE2D(_##name##HeightLut);

/// Yüzeyden okunan mikro detay.
struct SurfaceDetail
{
    float3 normal;      // teğet uzayı
    float roughness;
    float height;       // 0-1; yükseklik harmanı bunu kullanıyor
};

/// Tek yüzey okuması. `sharedSampler` bütün yüzeyler için ortak — örnekleyici
/// sayısı donanımda sınırlı ve hepsi aynı ayarı istiyor (Repeat, trilinear, aniso).
#define SAMPLE_SURFACE_DETAIL(name, sharedSampler, uv, ddxUV, ddyUV, result)          \
{                                                                                     \
    float4 n = SampleStochastic(TEXTURE2D_ARGS(_##name##Normal, sharedSampler),       \
                                TEXTURE2D_ARGS(_##name##NormalLut, sharedSampler),    \
                                uv, ddxUV, ddyUV);                                    \
    float4 r = SampleStochastic(TEXTURE2D_ARGS(_##name##Rough, sharedSampler),        \
                                TEXTURE2D_ARGS(_##name##RoughLut, sharedSampler),     \
                                uv, ddxUV, ddyUV);                                    \
    float4 h = SampleStochastic(TEXTURE2D_ARGS(_##name##Height, sharedSampler),       \
                                TEXTURE2D_ARGS(_##name##HeightLut, sharedSampler),    \
                                uv, ddxUV, ddyUV);                                    \
    /* Normal haritası "Default" olarak içe aktarılıyor (dönüşüm kanal              */\
    /* paketlemesini bozardı), o yüzden açma elle: 0-1'den -1..1'e.                 */\
    result.normal = normalize(n.xyz * 2.0 - 1.0);                                     \
    result.roughness = r.r;                                                           \
    result.height = h.r;                                                              \
}

/// YÜKSEKLİK HARMANI. Doğrusal karışım iki yüzeyi her yerde yarı yarıya
/// bulanıklaştırır; gerçekte üstteki malzeme önce ÇUKURLARI doldurur, alttaki
/// tümseklerde açıkta kalır. Eşik `t` ile yükselirken üst malzeme alçak yerlerden
/// başlayıp tümsekleri örtüyor; geçiş de doğal bir sınır çizgisi kazanıyor.
///
/// `sharpness` sıfır olsaydı sınır bıçak gibi olurdu, geniş olsaydı doğrusal
/// karışıma dönerdi.
float SurfaceHeightBlend(float lowerHeight, float upperHeight, float t, float sharpness)
{
    float threshold = lerp(-sharpness, 1.0 + sharpness, t);
    return saturate((upperHeight - lowerHeight + threshold) / (2.0 * sharpness));
}

/// İki yüzeyin yükseklik tabanlı karışımı.
SurfaceDetail BlendSurfaceDetail(SurfaceDetail lower, SurfaceDetail upper,
                                 float t, float sharpness)
{
    float blend = SurfaceHeightBlend(lower.height, upper.height, t, sharpness);

    SurfaceDetail result;
    result.normal = normalize(lerp(lower.normal, upper.normal, blend));
    result.roughness = lerp(lower.roughness, upper.roughness, blend);
    result.height = lerp(lower.height, upper.height, blend);
    return result;
}

/// EĞİM DÜZELTMELİ UV. Dünya XZ'sinden alınan UV dik yamaçta 1/cos(eğim) kadar
/// gerilir. Dikey düzlem izdüşümüyle harmanlanarak düzeltiliyor — tam triplanar üç
/// örnekleme demek, iki yeterli.
float2 SurfacePlanarUV(float3 worldPos, float3 normalWS, float scale)
{
    float2 flat = worldPos.xz * scale;
    float2 side = abs(normalWS.x) > abs(normalWS.z)
                ? worldPos.zy * scale
                : worldPos.xy * scale;

    float steep = 1.0 - saturate(normalWS.y);
    return lerp(flat, side, smoothstep(0.25, 0.75, steep));
}

/// UV'yi bir eksene döndürür. Yönlü dokular (rüzgâr sastrugisi, katmanlı kaya)
/// dünyada sabit yönde çizilmiş; sahnedeki gerçek yöne hizalanmaları gerekiyor.
void SurfaceAlignUV(float2 axis, inout float2 uv, inout float2 ddxUV, inout float2 ddyUV)
{
    float2 perpendicular = float2(-axis.y, axis.x);
    uv = float2(dot(uv, axis), dot(uv, perpendicular));
    ddxUV = float2(dot(ddxUV, axis), dot(ddxUV, perpendicular));
    ddyUV = float2(dot(ddyUV, axis), dot(ddyUV, perpendicular));
}

#endif
