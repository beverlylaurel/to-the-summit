#ifndef URP_VOLUMETRIC_CLOUDS_DEFINES_HLSL
#define URP_VOLUMETRIC_CLOUDS_DEFINES_HLSL

CBUFFER_START(UnityPerMaterial)
float _PixelFootprintScale;
half _NumPrimarySteps;
half _NumLightSteps;
half _MaxStepSize;
float _HighestCloudAltitude;
float _LowestCloudAltitude;
half4 _ShapeNoiseOffset;
half _VerticalShapeNoiseOffset;
half4 _WindDirection;
half4 _WindVector;
half _VerticalShapeWindDisplacement;
half _VerticalErosionWindDisplacement;
half _MediumWindSpeed;
half _SmallWindSpeed;
half _AltitudeDistortion;
half _DensityMultiplier;
half _PowderEffectIntensity;
half _ShapeScale;
half _ShapeFactor;
half _ErosionScale;
half _ErosionFactor;
half _ErosionOcclusion;
half _MicroErosionScale;
half _MicroErosionFactor;
half _FadeInStart;
half _FadeInDistance;
half _MultiScattering;
half4 _ScatteringTint;
half _AmbientProbeDimmer;
half _SunLightDimmer;
float _EarthRadius;
half _AccumulationFactor;
half _CloudNearPlane;
float4 _CloudMapTiling;
half _CloudCoverage;
half _AnvilAmount;
half _ExtinctionCoefficient;
CBUFFER_END

// Ambient Probe (unity_SH)
half4 clouds_SHAr;
half4 clouds_SHAg;
half4 clouds_SHAb;
half4 clouds_SHBr;
half4 clouds_SHBg;
half4 clouds_SHBb;
half4 clouds_SHC;

half _ImprovedTransmittanceBlend;
float _PostExposure; // Exposure from the ColorAdjustments override
half3 _SunColor;

// IŞIK PAYI PROBU — GEÇİCİ. Bulutu, onu AYDINLATAN terime göre boyar:
//   YEŞİL   doğrudan güneş/ay baskın  → faz fonksiyonu ekranda iş görüyor
//   KIRMIZI ortam baskın              → faz tepesi boğuluyor, bulut düz görünür
//   SARI    ikisi denk
// Belirti: şafak ve batımda güneşe yakın bulutla uzaktaki bulut aynı görünüyor.
// "Fark var mı" göz kararıdır; bu prob yerine RENK SINIFI soruyor.
float _CloudLightProbe;

/// ÇAKMA. `LightningFlash` global olarak yazıyor: `_LightningFlash.rgb` o anki parlama,
/// `_LightningPosition` = (dünya konumu xyz, leke yarıçapı w). Konum çakma başına bir kez
/// yazılıyor, yani maske kare kare değişmiyor — `[N22 s.180]`'in titreme çaresi bu.
float4 _LightningFlash;
float4 _LightningPosition;

#ifndef URP_PHYSICALLY_BASED_SKY_DEFINES_INCLUDED
float4 _PlanetCenterRadius;
#endif

#endif