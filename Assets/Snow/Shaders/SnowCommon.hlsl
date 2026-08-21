#ifndef SNOW_COMMON_INCLUDED
#define SNOW_COMMON_INCLUDED

// ROL: kar durumunun ortak matematiği. Hem compute kernel'leri hem vertex/fragment
// shader'ları bunu include eder — SWE/yoğunluk/derinlik üçlüsü ve dünya-teksel
// dönüşümü tek yerde tanımlı olsun diye.
// Çağıran: SnowSim.compute, SnowLit.shader, SnowCoverObject.shader.

// TEXTURE2D / SAMPLE_TEXTURE2D_LOD makroları buradan geliyor. Compute tarafı
// Core.hlsl'i include etmiyor, o yüzden çekirdek kütüphane doğrudan alınıyor.
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

#include "SnowConstants.hlsl"

// Bölge takibinin global'leri. SnowManager her karede Shader.SetGlobal ile yazar.
// CBUFFER dışında — §8.5: global property'ler UnityPerMaterial'e girmez.
float2 _SnowAreaCenter;
float  _SnowAreaSize;
float  _SnowResolution;

// Zemin yüksekliği haritası (§3).
TEXTURE2D(_GroundHeightTex);
float2 _GroundOriginXZ;
float2 _GroundSizeXZ;
float  _GroundBaseY;
float  _GroundHeightRange;

// Yarım teksel düzeltmesi: (x = (res-1)/res, y = 0.5/res).
//
// Terrain'in yükseklik örnekleri teksel MERKEZLERİNDE değil teksel KÖŞELERİNDE
// tanımlı. Düzeltilmezse harita yarım teksel kayar; bu projede aynı hata bir kez
// 3.6 m'lik kaymaya yol açtı.
float2 _GroundHeightUV;

// Gökyüzü görünürlüğü haritası (§4).
TEXTURE2D(_SnowOcclusionTex);
float2 _OcclCenterXZ;
float  _OcclAreaSize;
float  _OcclResolution;
float3 _SnowUpDirection;

// Kendi örnekleyicimiz. URP'nin sampler_LinearClamp'i Core.hlsl'de tanımlı ve compute
// tarafı Core.hlsl'i include etmiyor; iki taraftan da erişilebilen tek yol Unity'nin
// satır içi örnekleyici adlandırması.
SamplerState snow_linear_clamp_sampler;

// --- §2.1 durum yardımcıları ---

float SnowDensity(float rhoN)      { return lerp(SNOW_RHO_MIN, SNOW_RHO_MAX, saturate(rhoN)); }
float SnowDensityN(float rho)      { return saturate((rho - SNOW_RHO_MIN) / (SNOW_RHO_MAX - SNOW_RHO_MIN)); }
float SnowHeight(float swe, float rhoN) { return swe * SNOW_RHO_WATER / max(SnowDensity(rhoN), 1.0); }

// --- §2.5 dünya <-> teksel ---

float2 SnowWorldToUV(float3 posWS) {
    return (posWS.xz - _SnowAreaCenter) / _SnowAreaSize + 0.5;
}
float2 SnowUVToWorld(float2 uv) {
    return (uv - 0.5) * _SnowAreaSize + _SnowAreaCenter;
}
float2 SnowTexelToWorld(uint2 id) {
    return SnowUVToWorld((float2(id) + 0.5) / _SnowResolution);
}
float SnowTexelSize() { return _SnowAreaSize / _SnowResolution; }

int2 SnowWorldToTexel(float2 worldXZ)
{
    float2 uv = (worldXZ - _SnowAreaCenter) / _SnowAreaSize + 0.5;
    return int2(floor(uv * _SnowResolution));
}

// Bölge dışına düşen örneklemeler için.
float SnowInsideMask(float2 uv) {
    float2 e = abs(uv - 0.5) * 2.0;
    return 1.0 - smoothstep(0.88, 1.0, max(e.x, e.y));
}

// --- §5.3 taşıma kapasitesi ---

/// Verilen basıncı taşımak için gereken yoğunluk. Islak kar daha zayıf.
///
/// SPEC'İN `SnowBearing` FONKSİYONU SİLİNDİ: §5.3 ikisini birden tanımlıyor ama
/// modelin kullandığı yalnız bu ters form; öne doğru form hiçbir yerden çağrılmıyordu.
///
/// pow'un tabanı açıkça pozitife kelepçeleniyor: derleyici işaretini kanıtlayamıyor ve
/// uyarı basıyor. Negatif basınç zaten fiziksel olarak yok.
float SnowRequiredDensity(float P, float wet)
{
    float wetF = lerp(1.0, 0.55, wet);
    float ratio = max(P, 1.0) / max(SNOW_SIGMA_REF * wetF, 1e-4);

    return SNOW_RHO_REF * pow(max(ratio, 0.0), 1.0 / SNOW_BEARING_N);
}

// --- §3 zemin yüksekliği ---

float SampleGroundHeight(float2 posXZ)
{
    float2 uv = (posXZ - _GroundOriginXZ) / _GroundSizeXZ;
    uv = uv * _GroundHeightUV.x + _GroundHeightUV.y;

    float n = SAMPLE_TEXTURE2D_LOD(_GroundHeightTex, snow_linear_clamp_sampler, saturate(uv), 0).r;
    return _GroundBaseY + n * _GroundHeightRange;
}

/// Zemin normali, merkezi fark. Kar inceyken arazi eğimi baskın olsun diye
/// (§7.3) kar normaliyle karıştırılıyor.
float3 SampleGroundNormal(float2 posXZ, float spacing)
{
    float hL = SampleGroundHeight(posXZ - float2(spacing, 0.0));
    float hR = SampleGroundHeight(posXZ + float2(spacing, 0.0));
    float hD = SampleGroundHeight(posXZ - float2(0.0, spacing));
    float hU = SampleGroundHeight(posXZ + float2(0.0, spacing));

    return normalize(float3(hL - hR, 2.0 * spacing, hD - hU));
}

// --- §4.3 gökyüzü görünürlüğü ---

/// 1 = gökyüzü açık, 0 = üstü kapalı.
///
/// 3x3 örnekleme ve 0.05..0.40 geçişi bilinçli: saçak altında kar kademeli olarak
/// azalır, keskin bir çizgi oluşmaz.
float SampleSkyVisibility(float3 posWS)
{
    float2 uv = (posWS.xz - _OcclCenterXZ) / _OcclAreaSize + 0.5;
    if (any(uv < 0) || any(uv > 1)) return 1.0;          // harita dışı = açık

    float texel = 1.0 / _OcclResolution;
    float vis = 0;

    [unroll] for (int y = -1; y <= 1; ++y)
    [unroll] for (int x = -1; x <= 1; ++x)
    {
        float occlY = SAMPLE_TEXTURE2D_LOD(_SnowOcclusionTex, snow_linear_clamp_sampler,
                                           uv + float2(x, y) * texel, 0).r;
        vis += 1.0 - smoothstep(0.05, 0.40, occlY - posWS.y);
    }

    return vis * (1.0 / 9.0);
}

#endif
