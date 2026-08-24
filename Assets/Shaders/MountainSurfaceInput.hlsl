#ifndef MOUNTAIN_SURFACE_INPUT_INCLUDED
#define MOUNTAIN_SURFACE_INPUT_INCLUDED

// Yüzey detay makroları burada KULLANILIYOR (DECLARE_SURFACE_DETAIL), o yüzden
// tanımı da burada include ediliyor. MountainSurface.hlsl'de olsaydı sıra ters
// düşüyor ve makro tanımsız kalıyordu.
#include "SurfaceDetail.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Arazi kar kalınlığı kadar yükseliyor (`SnowWorldCoverHeight`); gölge ve
// derinlik geçişleri de aynı ofseti uyguluyor, dolayısıyla tanım burada.
#include "../Snow/Shaders/SnowCommon.hlsl"

// Materyal sabitleri tek yerde. URP'nin hazır gölge/derinlik geçişleri de bu dosyayı
// include ediyor; tampon her geçişte birebir aynı olmazsa SRP Batcher materyali
// uyumsuz sayar ve toplu çizim devre dışı kalır.
CBUFFER_START(UnityPerMaterial)
    /// Prosedürel yüzeyin tohumu. Kaya bandı, oksit, liken, tanecik ve kırılma
    /// deseni dünya koordinatına bağlı; tohum değişmeden aynı koordinat aynı deseni
    /// verir. Dağ yeniden üretilince artırılıyor.
    float4 _PatternSeed;

    float4 _RockPrimary, _RockSecondary;
    float4 _LowlandTint, _AlpineTint;
    float4 _LichenColor, _OxideColor, _ScreeColor;

    float _GrainScale, _GrainStrength, _RockSmoothness;
    float _BandThickness, _BandWarp, _BandWarpScale, _BandContrast;
    float _LowlandCeiling, _AlpineFloor, _AltitudeTintStrength;
    float _LichenAmount, _LichenCeiling, _LichenMoistureBias, _LichenSunSensitivity;
    float _OxideAmount, _OxideScale;
    float _ScreeAmount, _ScreeSlopeLimit;
    float2 _ScreeRange;
    float _WetDarkening, _WetSmoothness, _BumpStrength, _BumpScale, _CavityStrength;

    float4 _TerrainOrigin;   // xyz köşe konumu
    float4 _TerrainSize;     // xyz boyut

    float _SurfaceWetness;
    float4 _SurfaceWindDir;   // xyz yön, w sürekli şiddet
    float4 _SurfaceSunDir;

    // Şafak ve batımda ufuktan gelen kızıl ışık. Rengi ve gücü TimeOfDay'den sürülür.
    float4 _SurfaceDawnColor;
    float4 _SurfaceDawnDir;
    float _SurfaceDawnStrength;
    float _AlpenglowFacing;

    // URP'nin hazır geçişlerinin beklediği alanlar. Yüzey opak ve dokusuz olduğu için
    // kullanılmıyorlar, ama tanımlı olmazlarsa o geçişler derlenmiyor.
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half   _Cutoff;
CBUFFER_END

// Dağdan çıkarılan yüzey verisi (bkz. SurfaceMapBaker). Gürültü "nerede" sorusunu
// cevaplayamaz; bu üç kanal dağın kendi biçiminden okunur.
//   R birikim   — yukarıdan akan malzeme, çakıl oluklarda toplanır
//   G konkavlık — yerel çukurluk, nem yarıklarda tutunur
//   B maruziyet — göğü görme oranı
TEXTURE2D(_SurfaceMaps);
SAMPLER(sampler_SurfaceMaps);
float4 _SurfaceMapsSize;   // xy çözünürlük, zw 1/çözünürlük

/// Yüzey haritalarının UV'si. Arazi köşesinden ve boyutundan; ayrı bir dönüşüm yok.
float2 SurfaceMapUV(float3 worldPos)
{
    return (worldPos.xz - _TerrainOrigin.xz) / max(1.0, _TerrainSize.x);
}

/// Ucuz bilinear okuma. Ana geçiş bikübik örnekleyici kullanıyor (yüzey rengi
/// texel ızgarasını ele veriyordu); DEPLASMAN için o gerekmiyor ve on altı okuma
/// köşe/domain aşamasında pahalı — birikinti zaten metre ölçeğinde.
float4 SampleSurfaceMapsFast(float3 worldPos)
{
    return SAMPLE_TEXTURE2D_LOD(_SurfaceMaps, sampler_SurfaceMaps,
                                SurfaceMapUV(worldPos), 0);
}

// Zemin normali. Köşe normalleri dört metrelik ızgarada yaşıyor ve üçgen aralarının
// doldurulması köşegen dikişler bırakıyor — alçak güneşte dağ yorgan desenine
// bölünüyordu. Doku bilinear okunur, köşegeni yok; ince ayrıntı prosedürel kabartıdan.
TEXTURE2D(_GroundNormals);
SAMPLER(sampler_GroundNormals);

// Ufuk haritası: on altı pusula yönü için ufku kapatan açı (0-1 = 0-90 derece).
// Güneş gölgesi buradan okunuyor; gölge haritası arazi için hiç okunmuyor.
TEXTURE2D_ARRAY(_HorizonMap);
SAMPLER(sampler_HorizonMap);

// Yükseklik sisi. Nem ve toz alçakta toplanır; yoğunluk yükseldikçe üstel olarak
// seyrelir. Hesabın kendisi HeightFog.hlsl'de: tek bir yüzeyin özelliği değil,
// havanın kendisi.
#include "HeightFog.hlsl"

#endif
