// ROL: kar sisteminin ortak GPU yardımcıları — durum dönüşümleri, dünya↔teksel
// eşlemesi, zemin yüksekliği örneklemesi.
// Çağıran: SnowSim.compute ve bütün kar shader'ları.

#ifndef SNOW_COMMON_INCLUDED
#define SNOW_COMMON_INCLUDED

#include "SnowConstants.hlsl"

SAMPLER(snow_linear_clamp_sampler);

// ---------------------------------------------------------------- kar durumu

/// Normalize yoğunluktan gerçek yoğunluk, kg/m³ (spec §6.3).
float SnowDensity(float rhoN)
{
    return lerp(SNOW_RHO_MIN, SNOW_RHO_MAX, saturate(rhoN));
}

/// Gerçek yoğunluktan normalize yoğunluk.
float SnowDensityN(float rho)
{
    return saturate((rho - SNOW_RHO_MIN) / (SNOW_RHO_MAX - SNOW_RHO_MIN));
}

/// Bozulmamış kar sütununun yüksekliği, metre. `h = SWE * 1000 / rho`.
///
/// Kar bir yükseklik değil MADDE: korunan nicelik SWE, görünür derinlik ondan
/// türetiliyor. Aynı SWE sıkışınca alçalıyor — batma, patika ve izlerin dolması
/// bu tek denklemden çıkıyor.
float SnowBaseHeight(float swe, float rhoN)
{
    return swe * SNOW_RHO_WATER / max(SnowDensity(rhoN), 1.0);
}

/// İz oyulduktan ve kenar sırtı eklendikten sonraki yüzey yüksekliği, metre.
float SnowSurfaceHeight(float swe, float rhoN, float carve, float rim)
{
    return max(SnowBaseHeight(swe, rhoN) - carve + rim, 0.0);
}

// ------------------------------------------------------------ dünya ↔ teksel

float2 _SnowAreaCenter;      // bölgenin dünya XZ merkezi, snap'lenmiş
float  _SnowAreaSize;        // bölgenin kenar uzunluğu, metre
float  _SnowResolution;      // doku çözünürlüğü, teksel

float2 SnowWorldToUV(float3 p)
{
    return (p.xz - _SnowAreaCenter) / _SnowAreaSize + 0.5;
}

float2 SnowUVToWorld(float2 uv)
{
    return (uv - 0.5) * _SnowAreaSize + _SnowAreaCenter;
}

float2 SnowTexelToWorld(uint2 id)
{
    return SnowUVToWorld((float2(id) + 0.5) / _SnowResolution);
}

float SnowTexelSize()
{
    return _SnowAreaSize / _SnowResolution;
}

/// Bölgenin kenarında yumuşak sönüm. Dışarıda 0, ortada 1.
float SnowInsideMask(float2 uv)
{
    float2 e = abs(uv - 0.5) * 2.0;
    return 1.0 - smoothstep(0.88, 1.0, max(e.x, e.y));
}

// ------------------------------------------------------------ zemin yüksekliği

TEXTURE2D(_GroundHeightTex);

float2 _GroundOriginXZ;      // zemin dokusunun dünya köşesi
float2 _GroundSizeXZ;        // kapsadığı alan, metre
float  _GroundBaseY;         // 0..1 değerin haritalandığı taban kot
float  _GroundHeightRange;   // 0..1 değerin haritalandığı aralık

/// Zemin yüksekliği (spec §7.3). MeshBake yolunda doku doğrudan dünya Y tutar;
/// orada `_GroundBaseY = 0`, `_GroundHeightRange = 1` yazılır ve aynı satır çalışır.
float SampleGroundHeight(float2 posXZ)
{
    float2 uv = (posXZ - _GroundOriginXZ) / _GroundSizeXZ;
    float  n  = SAMPLE_TEXTURE2D_LOD(_GroundHeightTex, snow_linear_clamp_sampler, saturate(uv), 0).r;
    return _GroundBaseY + n * _GroundHeightRange;
}

#endif
