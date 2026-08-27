// ROL: deniz sisteminin butun shader property ID'leri.
// Cagiran: SeaManager, SeaSimulation, SeaBathymetry, SeaSurface.

using UnityEngine;

/// STRING'DEN ID'YE ÇEVİRİM BİR KEZ.
///
/// `Shader.SetGlobalFloat("_Ad", ...)` her çağrıda string hash'liyor ve
/// `Update` içinde bu allocation demek. Spec §0.8 bunu yasaklıyor.
public static class SeaShaderIDs
{
    // --- Ortam (spec §3.4) ---
    public static readonly int SeaWindWS = Shader.PropertyToID("_SeaWindWS");
    public static readonly int SeaTime = Shader.PropertyToID("_SeaTime");
    public static readonly int SunElevation01 = Shader.PropertyToID("_SeaSunElevation01");
    public static readonly int SkyColor = Shader.PropertyToID("_SeaSkyColor");
    public static readonly int HorizonColor = Shader.PropertyToID("_SeaHorizonColor");
    public static readonly int CloudCover01 = Shader.PropertyToID("_SeaCloudCover01");
    public static readonly int PrecipIntensity01 = Shader.PropertyToID("_SeaPrecipIntensity01");

    // --- Deniz seviyesi ve bathymetry (spec §9) ---
    public static readonly int SeaLevelY = Shader.PropertyToID("_SeaLevelY");
    public static readonly int BathyTex = Shader.PropertyToID("_SeaBathyTex");
    public static readonly int BathyOriginXZ = Shader.PropertyToID("_SeaBathyOriginXZ");
    public static readonly int BathySizeXZ = Shader.PropertyToID("_SeaBathySizeXZ");
    public static readonly int BathyResolution = Shader.PropertyToID("_SeaBathyResolution");
    public static readonly int DeepWaterDepth = Shader.PropertyToID("_SeaDeepWaterDepth");

    // --- Spektrum ve FFT (spec §6, §11.1) ---
    public static readonly int H0 = Shader.PropertyToID("_SeaH0");
    public static readonly int SpectrumHt = Shader.PropertyToID("_SeaSpectrumHt");
    public static readonly int SpectrumSlope = Shader.PropertyToID("_SeaSpectrumSlope");
    public static readonly int PingPong = Shader.PropertyToID("_SeaPingPong");
    public static readonly int Displacement = Shader.PropertyToID("_SeaDisplacement");
    public static readonly int Derivatives = Shader.PropertyToID("_SeaDerivatives");
    public static readonly int Foam = Shader.PropertyToID("_SeaFoam");
    public static readonly int FoamPrev = Shader.PropertyToID("_SeaFoamPrev");

    // --- Compute yazma hedefleri (spec §11.1) ---
    //
    // AYRI AD. Aynı doku hem `RWTexture2DArray` hem `Texture2DArray` olarak
    // bağlanamıyor; compute'un yazdığı ad `RW` sonekli, yüzey shader'ının
    // okuduğu ad soneksiz.
    public static readonly int H0RW = Shader.PropertyToID("_SeaH0RW");
    public static readonly int SpectrumHtRW = Shader.PropertyToID("_SeaSpectrumHtRW");
    public static readonly int SpectrumSlopeRW = Shader.PropertyToID("_SeaSpectrumSlopeRW");
    public static readonly int DisplacementRW = Shader.PropertyToID("_SeaDisplacementRW");
    public static readonly int DerivativesRW = Shader.PropertyToID("_SeaDerivativesRW");

    /// Kademe bandı üst sınırları (rad/m).
    public static readonly int TierCutoffK = Shader.PropertyToID("_SeaTierCutoffK");

    public static readonly int PatchSizes = Shader.PropertyToID("_SeaPatchSizes");
    public static readonly int TierWeights = Shader.PropertyToID("_SeaTierWeights");
    public static readonly int ChoppinessPerTier = Shader.PropertyToID("_SeaChoppinessPerTier");
    public static readonly int SpectrumDepth = Shader.PropertyToID("_SeaSpectrumDepth");
    public static readonly int Fetch = Shader.PropertyToID("_SeaFetch");
    public static readonly int Swell = Shader.PropertyToID("_SeaSwell");
    public static readonly int SmallWaveCutoff = Shader.PropertyToID("_SeaSmallWaveCutoff");
    public static readonly int LoopPeriod = Shader.PropertyToID("_SeaLoopPeriod");
    public static readonly int Choppiness = Shader.PropertyToID("_SeaChoppiness");

    // --- FFT dispatch parametreleri ---
    public static readonly int FftStep = Shader.PropertyToID("_SeaFftStep");
    public static readonly int FftTier = Shader.PropertyToID("_SeaFftTier");
    public static readonly int DeltaTime = Shader.PropertyToID("_SeaDeltaTime");

    // --- Sığ su (spec §8) ---
    public static readonly int MaxShoalingGain = Shader.PropertyToID("_SeaMaxShoalingGain");
    public static readonly int RunupMaxDepth = Shader.PropertyToID("_SeaRunupMaxDepth");
    public static readonly int PeakPeriod = Shader.PropertyToID("_SeaPeakPeriod");
    public static readonly int ShoreFoamPhase = Shader.PropertyToID("_SeaShoreFoamPhase");

    // --- Optik (spec §12) ---
    public static readonly int ExtinctionRGB = Shader.PropertyToID("_SeaExtinctionRGB");
    public static readonly int UpwellingColor = Shader.PropertyToID("_SeaUpwellingColor");
    public static readonly int RefractionStrength = Shader.PropertyToID("_SeaRefractionStrength");
    public static readonly int RoughnessCalm = Shader.PropertyToID("_SeaRoughnessCalm");
    public static readonly int RoughnessRough = Shader.PropertyToID("_SeaRoughnessRough");

    // --- Köpük (spec §13) ---
    public static readonly int ShoreFoamDepth = Shader.PropertyToID("_SeaShoreFoamDepth");
    public static readonly int FoamColor = Shader.PropertyToID("_SeaFoamColor");
    public static readonly int FoamRoughness = Shader.PropertyToID("_SeaFoamRoughness");

    // --- Islak kum (spec §14) ---
    public static readonly int SeaWetLevelY = Shader.PropertyToID("_SeaWetLevelY");
    public static readonly int SeaWetFadeM = Shader.PropertyToID("_SeaWetFadeM");

    // --- Teşhis ---
    public static readonly int DbgNoWaves = Shader.PropertyToID("_SeaDbgNoWaves");
    public static readonly int DbgNoShallow = Shader.PropertyToID("_SeaDbgNoShallow");
    public static readonly int DbgNoFoam = Shader.PropertyToID("_SeaDbgNoFoam");
    public static readonly int DbgNoRefraction = Shader.PropertyToID("_SeaDbgNoRefraction");
}
