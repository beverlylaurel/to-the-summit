#ifndef MOUNTAIN_SURFACE_INPUT_INCLUDED
#define MOUNTAIN_SURFACE_INPUT_INCLUDED

// Yüzey detay makroları burada KULLANILIYOR (DECLARE_SURFACE_DETAIL), o yüzden
// tanımı da burada include ediliyor. MountainSurface.hlsl'de olsaydı sıra ters
// düşüyor ve makro tanımsız kalıyordu.
#include "SurfaceDetail.hlsl"
#include "SnowDrift.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// Materyal sabitleri tek yerde. URP'nin hazır gölge/derinlik geçişleri de bu dosyayı
// include ediyor; tampon her geçişte birebir aynı olmazsa SRP Batcher materyali
// uyumsuz sayar ve toplu çizim devre dışı kalır.
CBUFFER_START(UnityPerMaterial)
    float4 _RockPrimary, _RockSecondary;
    float4 _LowlandTint, _AlpineTint;
    float4 _LichenColor, _OxideColor, _ScreeColor, _SnowColor;

    float _GrainScale, _GrainStrength, _RockSmoothness;
    float _BandThickness, _BandWarp, _BandWarpScale, _BandContrast;
    float _LowlandCeiling, _AlpineFloor, _AltitudeTintStrength;
    float _LichenAmount, _LichenCeiling, _LichenMoistureBias, _LichenSunSensitivity;
    float _OxideAmount, _OxideScale;
    float _ScreeAmount, _ScreeSlopeLimit, _SnowSmoothness;
    float2 _ScreeRange;
    float _SnowSlopeLimit, _SnowBreakup;
    float _SnowBurial, _SnowRounding, _Sastrugi, _SnowDepthScale;
    float _PermanentSnowBand;
    float _SnowlineSunLift, _SnowlineGullyDrop, _SnowlineRagged;
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

// KAR BİRİKİM AĞIRLIĞI. Arazi rüzgârın hızını değiştirir, hız da birikimi: rüzgârüstü
// ve dışbükey yüzeyde rüzgâr hızlanır ve kar kazınır, rüzgâraltı ve içbükey yüzeyde
// yavaşlar ve kar yığılır (Liston & Sturm, SnowTran-3D). Harita hâkim rüzgâr yönüne
// göre PİŞİYOR — yön sabit bir ayar, çalışma anında hesaplanacak bir şey yok.
TEXTURE2D(_SnowDriftWeight);
SAMPLER(sampler_SnowDriftWeight);

/// Birikim ağırlığı, 0.67-2.0. 1 nötr. Bayta sığsın diye yarıya bölünmüş saklanıyor.
float SampleDriftWeight(float3 worldPos)
{
    return SAMPLE_TEXTURE2D_LOD(_SnowDriftWeight, sampler_SnowDriftWeight,
                                SurfaceMapUV(worldPos), 0).r * 2.0;
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

// KAR MİKRO DETAYI. İki yüzey durumu, ikisi de ambientCG (CC0), ışık pişmemiş
// (ölçüldü: renk-eğim korelasyonu ~0.03):
//   POWDER — taze toz kar: yönsüz, kabarık, taneli
//   PACKED — rüzgârın sıkıştırdığı sert kar: yönlü, cilalı, kabuklu
// Renk ALINMIYOR: karın rengi kar sistemine bağlı (tazelik, derinlik, ıslaklık,
// alpenglow). Dokudan yalnız kabartma, pürüzlülük ve yükseklik geliyor.
//
// Bildirimler ortak makrodan: yüzey başına on iki satır elle yazmak ikinci yüzeyde
// yirmi dört, üçüncüde otuz altı ederdi.
DECLARE_SURFACE_DETAIL(SnowPowder)
DECLARE_SURFACE_DETAIL(SnowPacked)
SAMPLER(sampler_SnowPowderNormal);

// BİRİKİNTİ ALANI. Kar derinliğinin yatay şekli — rüzgâr hizalı, arazi eğrisiyle
// modüle. Bkz. SnowDrift.hlsl.
float _SnowDriftStrength;   // derinliğe karışma payı; 0 = alan kapalı
float _SnowDriftCoverBite;  // birikinti kenarının örtüyü de inceltme payı

float _SnowDetailScale;      // 1 / desen periyodu (metre)
float _SnowDetailStrength;
float _SnowDetailRough;      // pürüzlülük dokusunun ağırlığı
float _SnowDetailFade;       // bu mesafede tamamen söner (metre)

// HAVA DURUMUNDAN GELEN DEĞERLER GLOBAL. Materyalin kendi ayarı değiller — hava
// sürücüsünden geliyorlar ve sahnedeki her yüzey aynısını okumalı. `UnityPerMaterial`
// tamponunun içindeyken materyale yazılan değer shader'a ULAŞMIYORDU: tampon eski
// değerde kalıyor, kar maskesi hep kapalı okunuyordu. Sis de aynı sebeple global.
float _SnowfallFloor, _SnowfallCeiling;
float _PermanentSnowLine;

// Ufuk haritası: on altı pusula yönü için ufku kapatan açı (0-1 = 0-90 derece).
// Güneş gölgesi buradan okunuyor; gölge haritası arazi için hiç okunmuyor.
TEXTURE2D_ARRAY(_HorizonMap);
SAMPLER(sampler_HorizonMap);

// Yükseklik sisi. Nem ve toz alçakta toplanır; yoğunluk yükseldikçe üstel olarak
// seyrelir. Hesabın kendisi HeightFog.hlsl'de: tek bir yüzeyin özelliği değil,
// havanın kendisi.
#include "HeightFog.hlsl"

#endif
