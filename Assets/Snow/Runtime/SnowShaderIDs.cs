// ROL: shader property ID önbelleği. Her karede string ile SetFloat çağırmak yasak
// (§11.3), bütün ID'ler burada bir kez çözülür.
// Çağıran: SnowManager, SnowRenderPass, SnowDebugWindow.

using UnityEngine;

public static class SnowShaderIDs
{
    // --- bölge takibi, global (§2.5) ---
    public static readonly int SnowAreaCenter = Shader.PropertyToID("_SnowAreaCenter");
    public static readonly int SnowAreaSize = Shader.PropertyToID("_SnowAreaSize");
    public static readonly int SnowResolution = Shader.PropertyToID("_SnowResolution");
    public static readonly int SnowStateTex = Shader.PropertyToID("_SnowStateTex");

    // --- bölge dışı yedek değerler (§7.2) ---
    public static readonly int FallbackSWE = Shader.PropertyToID("_FallbackSWE");
    public static readonly int FallbackRhoN = Shader.PropertyToID("_FallbackRhoN");

    // --- SnowSim.compute (§2.4) ---
    public static readonly int Src = Shader.PropertyToID("_Src");
    public static readonly int Dst = Shader.PropertyToID("_Dst");
    public static readonly int Resolution = Shader.PropertyToID("_Resolution");
    public static readonly int ScrollTexels = Shader.PropertyToID("_ScrollTexels");
    public static readonly int DefaultSWE = Shader.PropertyToID("_DefaultSWE");
    public static readonly int DefaultRhoN = Shader.PropertyToID("_DefaultRhoN");
    public static readonly int DefaultWet = Shader.PropertyToID("_DefaultWet");

    // --- gokyuzu gorunurlugu, global (SS4) ---
    public static readonly int SnowOcclusionTex = Shader.PropertyToID("_SnowOcclusionTex");
    public static readonly int OcclCenterXZ = Shader.PropertyToID("_OcclCenterXZ");
    public static readonly int OcclAreaSize = Shader.PropertyToID("_OcclAreaSize");
    public static readonly int OcclResolution = Shader.PropertyToID("_OcclResolution");
    public static readonly int SnowUpDirection = Shader.PropertyToID("_SnowUpDirection");

    // --- zemin yüksekliği (§3) ---
    public static readonly int GroundHeightTex = Shader.PropertyToID("_GroundHeightTex");
    public static readonly int GroundOriginXZ = Shader.PropertyToID("_GroundOriginXZ");
    public static readonly int GroundSizeXZ = Shader.PropertyToID("_GroundSizeXZ");
    public static readonly int GroundBaseY = Shader.PropertyToID("_GroundBaseY");
    public static readonly int GroundHeightRange = Shader.PropertyToID("_GroundHeightRange");
    public static readonly int GroundHeightUV = Shader.PropertyToID("_GroundHeightUV");

    // --- KAccumulate (§6) ---
    public static readonly int State = Shader.PropertyToID("_State");
    public static readonly int DeltaTimeEff = Shader.PropertyToID("_DeltaTimeEff");
    public static readonly int TileIndex = Shader.PropertyToID("_TileIndex");
    public static readonly int TileWidth = Shader.PropertyToID("_TileWidth");
    public static readonly int SnowfallSWERate = Shader.PropertyToID("_SnowfallSWERate");
    public static readonly int WindWS = Shader.PropertyToID("_WindWS");
    public static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
    public static readonly int SnowWetness = Shader.PropertyToID("_SnowWetness");
    public static readonly int TemperatureC = Shader.PropertyToID("_TemperatureC");
    public static readonly int DriftBias = Shader.PropertyToID("_DriftBias");
    public static readonly int SettleTau = Shader.PropertyToID("_SettleTau");
    public static readonly int DisturbTau = Shader.PropertyToID("_DisturbTau");
    public static readonly int MeltDDF = Shader.PropertyToID("_MeltDDF");
    public static readonly int SnowCoverage = Shader.PropertyToID("_SnowCoverage");

    // --- deformasyon (§5) ---
    public static readonly int Deformers = Shader.PropertyToID("_Deformers");
    public static readonly int DeformerMassOut = Shader.PropertyToID("_DeformerMassOut");
    public static readonly int DeformerRingSum = Shader.PropertyToID("_DeformerRingSum");
    public static readonly int DeformerCount = Shader.PropertyToID("_DeformerCount");
    public static readonly int DeformerBoxTexels = Shader.PropertyToID("_DeformerBoxTexels");
    public static readonly int StampAtlas = Shader.PropertyToID("_StampAtlas");
    public static readonly int RimVelocityBias = Shader.PropertyToID("_RimVelocityBias");
    public static readonly int RelaxSrc = Shader.PropertyToID("_RelaxSrc");
    public static readonly int RelaxDst = Shader.PropertyToID("_RelaxDst");
    public static readonly int ReposeTan = Shader.PropertyToID("_ReposeTan");
    public static readonly int RelaxRate = Shader.PropertyToID("_RelaxRate");
    public static readonly int ForceRelaxAll = Shader.PropertyToID("_ForceRelaxAll");

    public static readonly int SumOut = Shader.PropertyToID("_SumOut");

    // --- kar yağışı (§10.1) ---
    public static readonly int Flakes = Shader.PropertyToID("_Flakes");
    public static readonly int FlakeCapacity = Shader.PropertyToID("_FlakeCapacity");
    public static readonly int FlakeActive = Shader.PropertyToID("_FlakeActive");
    public static readonly int FlakeSpindrift = Shader.PropertyToID("_FlakeSpindrift");
    public static readonly int FlakeBoxCenter = Shader.PropertyToID("_FlakeBoxCenter");
    public static readonly int FlakeBoxSize = Shader.PropertyToID("_FlakeBoxSize");
    public static readonly int FlakeDeltaTime = Shader.PropertyToID("_FlakeDeltaTime");
    public static readonly int FlakeTime = Shader.PropertyToID("_FlakeTime");
    public static readonly int FlakeWind = Shader.PropertyToID("_FlakeWind");
    public static readonly int FlakeWindSpeed = Shader.PropertyToID("_FlakeWindSpeed");
    public static readonly int FlakeWetness = Shader.PropertyToID("_FlakeWetness");
    public static readonly int FlutterFreq = Shader.PropertyToID("_FlutterFreq");
    public static readonly int FlutterAmp = Shader.PropertyToID("_FlutterAmp");
    public static readonly int LooseOut = Shader.PropertyToID("_LooseOut");
    public static readonly int MinPixelSize = Shader.PropertyToID("_MinPixelSize");
    public static readonly int ScreenHeight = Shader.PropertyToID("_ScreenHeight");
    public static readonly int TanHalfFov = Shader.PropertyToID("_TanHalfFov");
    public static readonly int WindStretch = Shader.PropertyToID("_WindStretch");
    public static readonly int FlakeTint = Shader.PropertyToID("_FlakeTint");
    public static readonly int FlakeEmissive = Shader.PropertyToID("_FlakeEmissive");
    public static readonly int SoftFadeDistance = Shader.PropertyToID("_SoftFadeDistance");

    // --- lens karı (§10.2) ---
    public static readonly int LensSnowAmount = Shader.PropertyToID("_LensSnowAmount");
    public static readonly int LensTime = Shader.PropertyToID("_LensTime");
    public static readonly int LensCellDensity = Shader.PropertyToID("_LensCellDensity");

    // --- clipmap bölgesi (§7.1) ---
    public static readonly int SnowClipRegion = Shader.PropertyToID("_SnowClipRegion");

    // --- uzak kaskad (Faz 10) ---
    public static readonly int Cascade = Shader.PropertyToID("_Cascade");
    public static readonly int CascadeSrc = Shader.PropertyToID("_CascadeSrc");
    public static readonly int CascadeDst = Shader.PropertyToID("_CascadeDst");
    public static readonly int CascadeTex = Shader.PropertyToID("_SnowCascadeTex");
    public static readonly int CascadeCenter = Shader.PropertyToID("_SnowCascadeCenter");
    public static readonly int CascadeAreaSize = Shader.PropertyToID("_SnowCascadeAreaSize");
    public static readonly int CascadeResolution = Shader.PropertyToID("_CascadeResolution");
    public static readonly int CascadeScrollTexels = Shader.PropertyToID("_CascadeScrollTexels");
    public static readonly int CascadeWriteOrigin = Shader.PropertyToID("_CascadeWriteOrigin");
    public static readonly int CascadeWriteSize = Shader.PropertyToID("_CascadeWriteSize");
    public static readonly int CascadeRatio = Shader.PropertyToID("_CascadeRatio");

    // --- Hidden_SnowDebug.shader ---
    public static readonly int DebugMode = Shader.PropertyToID("_DebugMode");
    public static readonly int DebugRange = Shader.PropertyToID("_DebugRange");
    public static readonly int DebugGridSize = Shader.PropertyToID("_DebugGridSize");
    public static readonly int DebugWorldCenter = Shader.PropertyToID("_DebugWorldCenter");
    public static readonly int DebugWorldSize = Shader.PropertyToID("_DebugWorldSize");
}
