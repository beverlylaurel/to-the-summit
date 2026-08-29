// ROLE: every shader property ID of the sea system.
// CALLED BY: SeaManager, SeaSimulation, SeaBathymetry, SeaSurface.

using UnityEngine;

/// STRING TO ID CONVERSION HAPPENS ONCE.
///
/// `Shader.SetGlobalFloat("_Name", ...)` hashes the string on every call,
/// and inside `Update` that means an allocation. Spec §0.8 forbids it.
public static class SeaShaderIDs
{
    // --- Environment (spec §3.4) ---
    public static readonly int SeaWindWS = Shader.PropertyToID("_SeaWindWS");
    public static readonly int SeaTime = Shader.PropertyToID("_SeaTime");
    public static readonly int SunElevation01 = Shader.PropertyToID("_SeaSunElevation01");
    public static readonly int CloudCover01 = Shader.PropertyToID("_SeaCloudCover01");
    public static readonly int PrecipIntensity01 = Shader.PropertyToID("_SeaPrecipIntensity01");

    // --- Sea level and bathymetry (spec §9) ---
    public static readonly int SeaLevelY = Shader.PropertyToID("_SeaLevelY");
    public static readonly int BathyTex = Shader.PropertyToID("_SeaBathyTex");
    public static readonly int BathyOriginXZ = Shader.PropertyToID("_SeaBathyOriginXZ");
    public static readonly int BathySizeXZ = Shader.PropertyToID("_SeaBathySizeXZ");
    public static readonly int BathyResolution = Shader.PropertyToID("_SeaBathyResolution");
    public static readonly int DeepWaterDepth = Shader.PropertyToID("_SeaDeepWaterDepth");

    // --- Wave field, read by the surface shader (spec §11.1) ---
    public static readonly int Displacement = Shader.PropertyToID("_SeaDisplacement");
    public static readonly int Derivatives = Shader.PropertyToID("_SeaDerivatives");
    public static readonly int Foam = Shader.PropertyToID("_SeaFoam");

    // --- Compute write targets (spec §11.1) ---
    //
    // SEPARATE NAMES. The same texture cannot be bound as both
    // `RWTexture2DArray` and `Texture2DArray`; the name the compute shader
    // writes carries the `RW` suffix, the name the surface shader reads does
    // not.
    public static readonly int H0RW = Shader.PropertyToID("_SeaH0RW");
    public static readonly int SpectrumHtRW = Shader.PropertyToID("_SeaSpectrumHtRW");
    public static readonly int SpectrumSlopeRW = Shader.PropertyToID("_SeaSpectrumSlopeRW");
    public static readonly int DisplacementRW = Shader.PropertyToID("_SeaDisplacementRW");
    public static readonly int DerivativesRW = Shader.PropertyToID("_SeaDerivativesRW");
    public static readonly int FoamRW = Shader.PropertyToID("_SeaFoamRW");
    public static readonly int FoamPrevRW = Shader.PropertyToID("_SeaFoamPrevRW");

    /// Upper bounds of the tier bands (rad/m).
    public static readonly int TierCutoffK = Shader.PropertyToID("_SeaTierCutoffK");

    public static readonly int PatchSizes = Shader.PropertyToID("_SeaPatchSizes");
    public static readonly int TierWeights = Shader.PropertyToID("_SeaTierWeights");
    public static readonly int ChoppinessPerTier = Shader.PropertyToID("_SeaChoppinessPerTier");
    public static readonly int SpectrumDepth = Shader.PropertyToID("_SeaSpectrumDepth");
    public static readonly int Fetch = Shader.PropertyToID("_SeaFetch");
    public static readonly int Swell = Shader.PropertyToID("_SeaSwell");
    public static readonly int SwellAlpha = Shader.PropertyToID("_SeaSwellAlpha");
    public static readonly int SwellPeakOmega = Shader.PropertyToID("_SeaSwellPeakOmega");
    public static readonly int SwellGamma = Shader.PropertyToID("_SeaSwellGamma");
    public static readonly int SwellSpreadS = Shader.PropertyToID("_SeaSwellSpreadS");
    public static readonly int SwellDirOffset = Shader.PropertyToID("_SeaSwellDirOffset");
    public static readonly int SmallWaveCutoff = Shader.PropertyToID("_SeaSmallWaveCutoff");
    public static readonly int LoopPeriod = Shader.PropertyToID("_SeaLoopPeriod");
    public static readonly int Choppiness = Shader.PropertyToID("_SeaChoppiness");

    // --- FFT dispatch parameters ---
    public static readonly int DeltaTime = Shader.PropertyToID("_SeaDeltaTime");
    public static readonly int FftSize = Shader.PropertyToID("_SeaFftSize");
    public static readonly int FftLog2 = Shader.PropertyToID("_SeaFftLog2");

    // --- Shallow water (spec §8) ---
    public static readonly int MaxShoalingGain = Shader.PropertyToID("_SeaMaxShoalingGain");
    public static readonly int SignificantHeight = Shader.PropertyToID("_SeaSignificantHeight");
    public static readonly int RunupMaxDepth = Shader.PropertyToID("_SeaRunupMaxDepth");
    public static readonly int PeakPeriod = Shader.PropertyToID("_SeaPeakPeriod");
    public static readonly int ShoreFoamPhase = Shader.PropertyToID("_SeaShoreFoamPhase");

    // --- Optics (spec §12) ---
    public static readonly int ExtinctionRGB = Shader.PropertyToID("_SeaExtinctionRGB");
    public static readonly int UpwellingColor = Shader.PropertyToID("_SeaUpwellingColor");
    public static readonly int RefractionStrength = Shader.PropertyToID("_SeaRefractionStrength");
    public static readonly int RoughnessCalm = Shader.PropertyToID("_SeaRoughnessCalm");
    public static readonly int RoughnessRough = Shader.PropertyToID("_SeaRoughnessRough");

    // --- Foam (spec §13) ---
    public static readonly int ShoreFoamDepth = Shader.PropertyToID("_SeaShoreFoamDepth");
    public static readonly int FoamColor = Shader.PropertyToID("_SeaFoamColor");
    public static readonly int FoamRoughness = Shader.PropertyToID("_SeaFoamRoughness");
    public static readonly int FoamTiling = Shader.PropertyToID("_SeaFoamTiling");
    public static readonly int FoamBreakupTiling = Shader.PropertyToID("_SeaFoamBreakupTiling");

    // --- Wet sand (spec §14) ---
    public static readonly int SeaWetLevelY = Shader.PropertyToID("_SeaWetLevelY");
    public static readonly int SeaWetFadeM = Shader.PropertyToID("_SeaWetFadeM");
    public static readonly int SeaWetBandM = Shader.PropertyToID("_SeaWetBandM");
    public static readonly int SeaWetDarkening = Shader.PropertyToID("_SeaWetDarkening");

    // --- Diagnostics ---
    public static readonly int DbgNoWaves = Shader.PropertyToID("_SeaDbgNoWaves");
    public static readonly int DbgNoShallow = Shader.PropertyToID("_SeaDbgNoShallow");
    public static readonly int DbgNoFoam = Shader.PropertyToID("_SeaDbgNoFoam");
    public static readonly int DbgNoRefraction = Shader.PropertyToID("_SeaDbgNoRefraction");
}
