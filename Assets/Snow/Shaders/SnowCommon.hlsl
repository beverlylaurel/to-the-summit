// ROL: kar sisteminin ortak GPU yardımcıları — durum dönüşümleri, dünya↔teksel
// eşlemesi, zemin yüksekliği örneklemesi.
// Çağıran: SnowSim.compute ve bütün kar shader'ları.

#ifndef SNOW_COMMON_INCLUDED
#define SNOW_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "SnowConstants.hlsl"

// sampler_LinearClamp URP core'un GlobalSamplers.hlsl'inde tanımlı (ölçüldü).
// Kendi sampler'ımızı açmıyoruz — spec bu adı kullanıyor (§7.3, §9.4, §10.2, §12.2).

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

// ---------------------------------------------------------------------- çevre

/// HEPSİ MEVCUT SİSTEMLERDEN OKUNUYOR (spec §3). Kar sistemi bunlardan hiçbirini
/// üretmiyor; `SnowManager.WriteGlobals` köprüden alıp yayınlıyor.
float3 _WindWS;
float  _WindSpeed;
float  _TemperatureC;
float  _SunElevation01;
float  _FogDensity01;
float  _RainOnSnow01;
float3 _SnowUpDirection;

/// Bölgenin dışındaki dünyanın genel kar durumu.
float _FallbackSWE;
float _FallbackRhoN;

// ------------------------------------------------------------------ yakalama

/// Yakalama hacminin sıfır noktası — gözlemcinin dünya Y'si.
float _SnowCaptureOriginY;

/// RT_Capture'ın R kanalı GÖRELİ tutuluyor (yarım hassasiyet, bkz.
/// Hidden_SnowCaptureDepth). Dünya Y'sine dönüşü tek yerden geçiyor ki
/// çözücü tarafta unutulmasın.
float SnowCaptureY(float encoded)
{
    return _SnowCaptureOriginY + encoded;
}

// ------------------------------------------------------------ zemin yüksekliği

TEXTURE2D(_GroundHeightTex);

float2 _GroundOriginXZ;      // zemin dokusunun dünya köşesi
float2 _GroundTexelXZ;       // zemin dokusunun bir tekselinin dünya boyu
float2 _GroundSizeXZ;        // kapsadığı alan, metre
float  _GroundBaseY;         // 0..1 değerin haritalandığı taban kot
float  _GroundHeightRange;   // 0..1 değerin haritalandığı aralık

/// Zemin yüksekliği (spec §7.3). MeshBake yolunda doku doğrudan dünya Y tutar;
/// orada `_GroundBaseY = 0`, `_GroundHeightRange = 1` yazılır ve aynı satır çalışır.
float SampleGroundHeight(float2 posXZ)
{
    float2 uv = (posXZ - _GroundOriginXZ) / _GroundSizeXZ;
    float  n  = SAMPLE_TEXTURE2D_LOD(_GroundHeightTex, sampler_LinearClamp, saturate(uv), 0).r;
    return _GroundBaseY + n * _GroundHeightRange;
}

/// ASSUMPTION: spec §13.3 `SampleGroundNormal`'ı çağırıyor ama tanımlamıyor.
/// Zemin yükseklik dokusundan merkezi farkla türetiliyor — kar sistemi
/// böylece mevcut arazi bileşenlerinden hiçbir şey OKUMUYOR (spec §3).
/// Adım zemin dokusunun kendi teksel boyu; kar tekseliyle (1.5 cm) örneklenirse
/// aynı teksele düşer ve normal her yerde dümdüz yukarı çıkar.
float3 SampleGroundNormal(float2 posXZ)
{
    float2 e = max(_GroundTexelXZ, 1e-3);

    float hL = SampleGroundHeight(posXZ - float2(e.x, 0.0));
    float hR = SampleGroundHeight(posXZ + float2(e.x, 0.0));
    float hD = SampleGroundHeight(posXZ - float2(0.0, e.y));
    float hU = SampleGroundHeight(posXZ + float2(0.0, e.y));

    return normalize(float3(hL - hR, e.x + e.y, hD - hU));
}

// ------------------------------------------------------------- kar yüzeyi

TEXTURE2D(_SnowStateTex);
TEXTURE2D(_SnowTrailTex);

/// Kar yüzeyinin zeminden yüksekliği, verilen bölge UV'sinde.
///
/// BÖLGE DIŞINDA DÜNYANIN GENEL DURUMU. `SnowInsideMask` kenarda yumuşak
/// geçiş veriyor; sert kesilseydi deformasyon alanının sınırı yerde görünür
/// bir kare olurdu.
float SnowSurfaceAt(float2 uv)
{
    float  inside = SnowInsideMask(uv);
    float2 uvC    = saturate(uv);

    float4 s = SAMPLE_TEXTURE2D_LOD(_SnowStateTex, sampler_LinearClamp, uvC, 0);
    float4 t = SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, uvC, 0);

    float swe  = lerp(_FallbackSWE,  s.r, inside);
    float rhoN = lerp(_FallbackRhoN, s.g, inside);

    return SnowSurfaceHeight(swe, rhoN, t.r * inside, t.g * inside);
}

#endif
