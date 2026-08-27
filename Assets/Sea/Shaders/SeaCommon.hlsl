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

// KENDI ORNEKLEYICISI.
//
// `sampler_LinearClamp` URP'nin ic ornekleyicisi ve YALNIZ parca/vertex
// asamasinda tanimli; compute'ta "undeclared identifier" veriyor ve
// kernel SESSIZCE gecersiz kaliyor — `HasKernel` yine True donuyor, hata
// ancak dispatch'te "Kernel at index (0) is invalid" olarak cikiyor.
SAMPLER(sampler_SeaBathyTex);
float2 _SeaBathyOriginXZ;
float2 _SeaBathySizeXZ;
float  _SeaBathyResolution;
float  _SeaDeepWaterDepth;

// --- Kademe parametreleri (spec 6.6) ---
float3 _SeaPatchSizes;
float3 _SeaTierWeights;
float3 _SeaChoppinessPerTier;

float _SeaSpectrumDepth;
float _SeaMaxShoalingGain;

/// CALISAN FFT boyutu ve log2'si. Kalite presetinden geliyor;
/// `SEA_FFT_SIZE` yalniz ust sinir.
uint _SeaFftSize;
uint _SeaFftLog2;
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

// ------------------------------------------------------- dalga alani

// FFT CIKTISI. `SeaSimulation` her frame yaziyor.
//
// ORNEKLEME DUNYA KOORDINATINDAN, `frac()` YOK. Dokular
// `wrapMode = Repeat` ile kuruluyor ve donanim zaten tekrarliyor;
// `frac()` eklemek teksel sinirlarinda dikis yaratiyor (spec 10.4).
TEXTURE2D_ARRAY(_SeaDisplacement);
SAMPLER(sampler_SeaDisplacement);

TEXTURE2D_ARRAY(_SeaDerivatives);
SAMPLER(sampler_SeaDerivatives);

TEXTURE2D_ARRAY(_SeaFoam);
SAMPLER(sampler_SeaFoam);

/// Uc kademenin displacement toplami. w = kademeler arasi EN KUCUK Jacobian.
///
/// En kucugu aliniyor cunku katlanma tek bir kademede olsa bile yuzey
/// katlanmis sayilir; ortalama alinsaydi ince cirpintinin katlanmasi kaba
/// kademenin duzlugunde erirdi.
float4 SeaSampleDisplacement(float2 posXZ)
{
    float3 disp = 0.0;
    float  jac  = 1.0;

    [unroll]
    for (int s = 0; s < SEA_TIER_COUNT; ++s)
    {
        float2 uv = posXZ / _SeaPatchSizes[s];
        float4 d = SAMPLE_TEXTURE2D_ARRAY_LOD(_SeaDisplacement,
                                              sampler_SeaDisplacement, uv, s, 0);
        disp += d.xyz * _SeaTierWeights[s];
        jac   = min(jac, d.w);
    }

    return float4(disp, jac);
}

/// Uc kademenin egim toplami. Normal bundan kuruluyor — MERKEZI FARK
/// KULLANILMIYOR, egim zaten FFT ile uretiliyor ve o daha dogru
/// (spec 6.7, 10.5).
float2 SeaSampleSlope(float2 posXZ)
{
    float2 egim = 0.0;

    [unroll]
    for (int s = 0; s < SEA_TIER_COUNT; ++s)
    {
        float2 uv = posXZ / _SeaPatchSizes[s];
        egim += SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives,
                                       sampler_SeaDerivatives, uv, s).xy
              * _SeaTierWeights[s];
    }

    return egim;
}

/// Tepe kopugu yogunlugu ve KATLANMA YONU.
///
/// Kademeler arasi EN BUYUK aliniyor — kopuk ortulme, ortalama degil.
/// Yon, kopugu KAZANAN kademeden geliyor: baska kademenin yonu alinsaydi
/// desen kopugun uzandigi yonle ilgisiz cikardi.
float SeaSampleFoam(float2 posXZ, out float2 katlanmaYonu)
{
    float f = 0.0;
    katlanmaYonu = float2(1.0, 0.0);

    [unroll]
    for (int s = 0; s < SEA_TIER_COUNT; ++s)
    {
        float2 uv = posXZ / _SeaPatchSizes[s];
        float k = SAMPLE_TEXTURE2D_ARRAY(_SeaFoam, sampler_SeaFoam, uv, s).r;

        if (k > f)
        {
            f = k;
            katlanmaYonu = SAMPLE_TEXTURE2D_ARRAY(_SeaDerivatives,
                                                  sampler_SeaDerivatives, uv, s).zw;
        }
    }

    return f;
}

// ------------------------------------------------------------- gurultu

/// PROSEDUREL KOPUK DESENI — DOKU YOK.
///
/// Spec 13 `T_Foam` ve `T_FoamBreakup` dokularini istiyor. Bu projede
/// doku uretimi kredili servisten geciyor ve `CLAUDE.md` ilk denemenin
/// dogru olmasini sart kosuyor; kopugun nasil gorunmesi gerektigi ekranda
/// oturmadan istem yazmak kredi yakardi. Plan bu alternatifi kendisi
/// veriyor. Doku takilinca bu fonksiyon silinir.
float SeaHash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SeaValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = SeaHash21(i);
    float b = SeaHash21(i + float2(1.0, 0.0));
    float c = SeaHash21(i + float2(0.0, 1.0));
    float d = SeaHash21(i + float2(1.0, 1.0));

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// Uc oktav. Kopuk hem iri kume hem ince kabarcik tasiyor.
float SeaFoamNoise(float2 p)
{
    return SeaValueNoise(p)        * 0.60
         + SeaValueNoise(p * 2.37) * 0.30
         + SeaValueNoise(p * 5.13) * 0.10;
}

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
    // TANH ARGUMANI KIRPILIYOR.
    //
    // Derleyici `tanh`'i (e^x - e^-x)/(e^x + e^-x) olarak aciyor; x ~ 88'i
    // gecince e^x tasip inf oluyor ve inf/inf = NaN cikiyor. Olculdu:
    // 60 m derinlikte |n| > 112 olan butun tekseller NaN, oradan butun
    // FFT alanina yayiliyor.
    //
    // 20'de tanh zaten 1'e 1e-17 yakin — kirpma fiziksel bir sey
    // degistirmiyor, sadece tasmayi engelliyor.
    return sqrt(SEA_G * k * tanh(min(k * depth, 20.0)));
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

    return SAMPLE_TEXTURE2D_LOD(_SeaBathyTex, sampler_SeaBathyTex, uv, 0).r;
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

// ---------------------------------------------------- yuzey deformasyonu

struct SeaSurfaceNokta
{
    float3 posWS;
    float  depth;
    float  jacobian;
};

/// SIG SU DONUSUMU (spec 8) — vertex asamasi.
///
/// ILERI VE DERINLIK GECISI AYNI FONKSIYONU CAGIRIYOR. Ayri yazilsalardi
/// derinlik tamponu ile renk tamponu farkli bir yuzey gorurdu ve deniz
/// kendi golgesine takilirdi.
///
/// Derinlik SAPTIRILMAMIS xz'den okunuyor: yatay displacement dalganin
/// kendi hareketi, tabani tasimiyor (spec 10.4).
SeaSurfaceNokta SeaDeform(float3 posWS)
{
    SeaSurfaceNokta o;
    o.depth = SeaSampleDepth(posWS.xz);

    float4 alan = SeaSampleDisplacement(posWS.xz);
    float3 disp = _SeaDbgNoWaves > 0.5 ? 0.0 : alan.xyz;
    o.jacobian  = alan.w;

    if (_SeaDbgNoShallow <= 0.5)
    {
        float slope = SeaSampleBottomSlope(posWS.xz);

        // SIGLASMA. Green yasasi cok sig suda sinirsiza gidiyor; tavan
        // konuyor, gerceginde kirilma devreye giriyor (spec 8.1).
        float shoal = min(SeaShoalingGain(o.depth, _SeaSpectrumDepth),
                          _SeaMaxShoalingGain);

        // YATAY DISPLACEMENT SIG SUDA SONUYOR: dalga dikelesir, yatayda
        // yayilmaz (spec 8.2).
        float chopScale = saturate(o.depth / SEA_CHOP_FADE_DEPTH);

        // KIYI SONUMU. Derinlik sifira giderken dalga yuksekligi de sifira
        // gitmeli, yoksa mesh araziyle kesisip titriyor (spec 8.4).
        float shoreFade = smoothstep(0.0, SEA_SHORE_FADE_DEPTH, o.depth);

        disp.y  *= shoal * shoreFade;
        disp.xz *= chopScale * shoreFade;

        // KIRILMA YUKSEKLIK SINIRI, EGIME BAGLI (spec 8.3).
        float gamma = SeaBreakerIndex(slope);
        float hMax  = gamma * o.depth * 0.5;
        disp.y = sign(disp.y) * min(abs(disp.y), hMax);
    }

    posWS.xz += disp.xz;
    posWS.y  += disp.y;

    o.posWS = posWS;
    return o;
}

#endif
