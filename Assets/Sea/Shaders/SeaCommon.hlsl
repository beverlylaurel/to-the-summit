// ROL: deniz compute ve yuzey shader'larinin paylastigi tanimlar, globaller
// ve ortak fonksiyonlar.
// Cagiran: SeaSpectrum.compute, SeaFFT.compute, SeaFoam.compute, SeaLit.shader

#ifndef SEA_COMMON_INCLUDED
#define SEA_COMMON_INCLUDED

// URP CORE ONCE.
//
// `SAMPLER` makrosu buradan geliyor. Eksikse compute kernel SESSIZCE
// derlenmiyor: `GetComputeShaderMessages` bos doner ama `FindKernel`
// "kernel at index 0 is invalid" verir. Kar sisteminde bir tur bu yuzden
// yandi (`RATIONALE.md` — "Compute shader'da iki sessiz derleme tuzagi").
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "SeaConstants.hlsl"

// ------------------------------------------------------------- globaller

/// Ruzgar yonu * hizi (m/s). Spektrumun ANA girdisi; `SeaManager` her frame
/// yaziyor. Deniz kendi ruzgar noise'unu KURMUYOR (spec 3.4).
float2 _SeaWindWS;

/// Dongü kuantize edilmis zaman (s).
float  _SeaTime;

/// Deniz yuzeyinin dunya Y koordinati.
float  _SeaLevelY;

// --- Bathymetry (spec 9) ---
TEXTURE2D(_SeaBathyTex);
float2 _SeaBathyOriginXZ;
float2 _SeaBathySizeXZ;
float  _SeaBathyResolution;
float  _SeaDeepWaterDepth;

// --- Kademe parametreleri (spec 6.6) ---
float3 _SeaPatchSizes;
float3 _SeaTierWeights;
float3 _SeaChoppinessPerTier;

float _SeaSpectrumDepth;
float _SeaFetch;
float _SeaSwell;
float _SeaSmallWaveCutoff;
float _SeaLoopPeriod;
float _SeaChoppiness;

// --- Teshis ---
float _SeaDbgNoWaves;
float _SeaDbgNoShallow;
float _SeaDbgNoFoam;
float _SeaDbgNoRefraction;

// -------------------------------------------------------- karmasik sayi

/// Karmasik carpim.
float2 SeaCMul(float2 a, float2 b)
{
    return float2(a.x * b.x - a.y * b.y, a.x * b.y + a.y * b.x);
}

/// Karmasik eslenik.
float2 SeaConj(float2 a)
{
    return float2(a.x, -a.y);
}

/// e^{i*theta}
float2 SeaExpI(float theta)
{
    float s, c;
    sincos(theta, s, c);
    return float2(c, s);
}

// -------------------------------------------------------------- fizik

/// SIG SU DISPERSIYONU.
///
/// omega^2 = g*k*tanh(k*D). Taban cok derinse tanh 1'e gider ve derin su
/// bagintisina indirgenir — yani tek formul her iki durumu da kapsiyor.
/// **Derin su bagintisi AYRICA YAZILMIYOR** (spec 6.4, 17).
/// [KAYNAK: Tessendorf 2004 denklem 31 ve 32]
float SeaOmega(float k, float depth)
{
    return sqrt(SEA_G * k * tanh(k * depth));
}

/// DONGU KUANTIZASYONU.
///
/// Butun frekanslar temel frekansin kati olmali ki simulasyon T saniyede
/// tekrarlasin. ZORUNLU: alanin GPU'da yeniden hesaplanmadan tekrarlanabilir
/// olmasini saglar ve uzun oturumlarda `t` buyudukce float hassasiyeti
/// kaybini engeller.
/// [KAYNAK: Tessendorf 2004 4.2 denklem 34, 35]
float SeaQuantizeOmega(float omega)
{
    float omega0 = SEA_TWO_PI / _SeaLoopPeriod;
    return floor(omega / omega0) * omega0;
}

// --------------------------------------------------------- bathymetry

/// Su derinligi (m). >0 su, <0 kara.
///
/// Terrain heightmap'i shader'da DOGRUDAN ORNEKLENMIYOR — Unity surumleri
/// arasinda olcekleme sabitleri degisiyor. CPU'da bir kez bake ediliyor
/// (spec 9).
float SeaSampleDepth(float2 posXZ)
{
    float2 uv = (posXZ - _SeaBathyOriginXZ) / _SeaBathySizeXZ;

    // Arazi disi = acik deniz. Deniz mesh'i araziden buyuk ve ufka kadar
    // uzaniyor; disarisi sabit derinlik.
    if (any(uv < 0.0) || any(uv > 1.0)) return _SeaDeepWaterDepth;

    return SAMPLE_TEXTURE2D_LOD(_SeaBathyTex, sampler_LinearClamp, uv, 0).r;
}

/// Taban egimi (tan theta). Kirilma indeksi bundan tureniyor (spec 8.3).
float SeaSampleBottomSlope(float2 posXZ)
{
    float e = _SeaBathySizeXZ.x / _SeaBathyResolution;

    float dx = SeaSampleDepth(posXZ + float2(e, 0)) - SeaSampleDepth(posXZ - float2(e, 0));
    float dz = SeaSampleDepth(posXZ + float2(0, e)) - SeaSampleDepth(posXZ - float2(0, e));

    return length(float2(dx, dz)) / (2.0 * e);
}

// ------------------------------------------------------- sig su donusumu

/// SIGLASMA — GENLIK ARTISI.
///
/// Yavas degisen bir egim uzerinde ilerleyen dalganin genligi h^(-1/4) ile
/// orantili buyur. [KAYNAK: Green yasasi]
float SeaShoalingGain(float depthLocal, float depthRef)
{
    float d = max(depthLocal, SEA_MIN_DEPTH);

    // `max(..., 0)` derleyici uyarisi icin: `d` zaten pozitif ve `depthRef`
    // de oyle, yani oran negatif olamaz — ama derleyici bunu goremiyor ve
    // "pow will not work for negative f" uyarisi veriyor.
    return pow(max(depthRef / d, 0.0), 0.25);
}

/// KIRILMA DERINLIK INDEKSI, EGIME BAGLI.
///
/// Sabit 0.78 KULLANILMIYOR (spec 8.3, 17): cok hafif egimlerde alt sinir
/// 0.55'e iniyor, dik sahillerde 1.0'in ustune cikiyor.
/// [KAYNAK: McCowan 1894; Nelson 1983; DNV 2017; Galvin 1969; Weggel 1972]
float SeaBreakerIndex(float slope)
{
    return lerp(SEA_GAMMA_MILD, SEA_GAMMA_STEEP, saturate(slope / 0.10));
}

#endif
