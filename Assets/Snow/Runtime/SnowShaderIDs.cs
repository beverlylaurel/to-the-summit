// ROL: bütün shader property adlarının tek yeri (spec §0.8). String tabanlı
// SetFloat("_X") her karede hash hesaplıyor ve allocation yapıyor; ID'ler bir
// kez çözülüp saklanıyor.
// Çağıran: bütün kar bileşenleri.

using UnityEngine;

public static class SnowShaderIDs
{
    // --- Bölge ve dünya eşlemesi (SnowCommon.hlsl) ---
    public static readonly int SnowAreaCenter = Shader.PropertyToID("_SnowAreaCenter");
    public static readonly int SnowAreaSize = Shader.PropertyToID("_SnowAreaSize");
    public static readonly int SnowResolution = Shader.PropertyToID("_SnowResolution");

    // --- Zemin yüksekliği ---
    public static readonly int GroundHeightTex = Shader.PropertyToID("_GroundHeightTex");
    public static readonly int GroundOriginXZ = Shader.PropertyToID("_GroundOriginXZ");
    public static readonly int GroundSizeXZ = Shader.PropertyToID("_GroundSizeXZ");
    public static readonly int GroundTexelXZ = Shader.PropertyToID("_GroundTexelXZ");
    public static readonly int GroundBaseY = Shader.PropertyToID("_GroundBaseY");
    public static readonly int GroundHeightRange = Shader.PropertyToID("_GroundHeightRange");

    // --- Çevre: mevcut sistemlerden okunup yayınlanan değerler (spec §3) ---
    public static readonly int WindWS = Shader.PropertyToID("_WindWS");
    public static readonly int WindSpeed = Shader.PropertyToID("_WindSpeed");
    public static readonly int TemperatureC = Shader.PropertyToID("_TemperatureC");
    public static readonly int SunElevation01 = Shader.PropertyToID("_SunElevation01");
    public static readonly int FogDensity01 = Shader.PropertyToID("_FogDensity01");
    public static readonly int RainOnSnow01 = Shader.PropertyToID("_RainOnSnow01");
    public static readonly int SnowUpDirection = Shader.PropertyToID("_SnowUpDirection");

    // --- Yağış ---
    public static readonly int SnowfallSWERate = Shader.PropertyToID("_SnowfallSWERate");
    public static readonly int SnowWetness = Shader.PropertyToID("_SnowWetness");
    public static readonly int SnowCoverage = Shader.PropertyToID("_SnowCoverage");

    // --- Örtü ayarları (spec §16). Arazi ve nesneler AYNI sayıları okusun diye
    // global; iki yerde ayrı tutulsaydı sınırda çelişirlerdi.
    public static readonly int CoverSlopeSharpness = Shader.PropertyToID("_SnowCoverSlopeSharpness");
    public static readonly int CoverBreakupStrength = Shader.PropertyToID("_SnowCoverBreakupStrength");
    public static readonly int CoverEdgeSharpness = Shader.PropertyToID("_SnowCoverEdgeSharpness");
    public static readonly int CoverThickness = Shader.PropertyToID("_SnowCoverThickness");

    /// Dünyanın kar sütunu, metre. Arazi kar ışıklandırmasının derinliği;
    /// `_SnowCoverThickness` NESNE üstündeki ince örtü için ve arazi için
    /// yanlış büyüklük.
    public static readonly int WorldSnowDepth = Shader.PropertyToID("_WorldSnowDepth");
    public static readonly int SnowAccum = Shader.PropertyToID("_SnowAccum");
    public static readonly int SnowLineY = Shader.PropertyToID("_SnowLineY");

    // --- Durum dokuları ---
    public static readonly int SnowStateTex = Shader.PropertyToID("_SnowStateTex");
    public static readonly int SnowTrailTex = Shader.PropertyToID("_SnowTrailTex");
    public static readonly int SnowSkyVisTex = Shader.PropertyToID("_SnowSkyVisTex");
    public static readonly int SnowWindShadowTex = Shader.PropertyToID("_SnowWindShadowTex");

    // --- Gökyüzü görünürlüğü ---
    public static readonly int SkyCenterXZ = Shader.PropertyToID("_SkyCenterXZ");
    public static readonly int SkyAreaSize = Shader.PropertyToID("_SkyAreaSize");
    public static readonly int SkyResolution = Shader.PropertyToID("_SkyResolution");

    // --- Compute: kaynak / hedef ---
    public static readonly int Src = Shader.PropertyToID("_Src");
    public static readonly int Dst = Shader.PropertyToID("_Dst");
    public static readonly int Snow = Shader.PropertyToID("_Snow");
    public static readonly int SnowOut = Shader.PropertyToID("_SnowOut");
    public static readonly int Trail = Shader.PropertyToID("_Trail");
    public static readonly int TrailOut = Shader.PropertyToID("_TrailOut");
    public static readonly int BlurredCarve = Shader.PropertyToID("_BlurredCarve");
    public static readonly int ReducedOut = Shader.PropertyToID("_ReducedOut");
    public static readonly int CarveOut = Shader.PropertyToID("_CarveOut");

    // --- Compute: parametreler ---
    public static readonly int Resolution = Shader.PropertyToID("_Resolution");
    public static readonly int ScrollTexels = Shader.PropertyToID("_ScrollTexels");
    public static readonly int NewEdgeValue = Shader.PropertyToID("_NewEdgeValue");
    public static readonly int ClearValue = Shader.PropertyToID("_ClearValue");
    public static readonly int DeltaTimeEff = Shader.PropertyToID("_DeltaTimeEff");
    public static readonly int SnowDeltaTime = Shader.PropertyToID("_SnowDeltaTime");
    public static readonly int TileIndex = Shader.PropertyToID("_TileIndex");
    public static readonly int TileCount = Shader.PropertyToID("_TileCount");
    public static readonly int RimBlurTexels = Shader.PropertyToID("_RimBlurTexels");
    public static readonly int GSParity = Shader.PropertyToID("_GSParity");

    // --- İz parçaları ---
    public static readonly int TrailSegments = Shader.PropertyToID("_TrailSegments");
    public static readonly int TrailSegmentCount = Shader.PropertyToID("_TrailSegmentCount");
    public static readonly int TrailVelocityXZ = Shader.PropertyToID("_TrailVelocityXZ");

    // --- Isı kaynakları (spec §18.2) ---
    public static readonly int HeatSources = Shader.PropertyToID("_HeatSources");
    public static readonly int HeatParams = Shader.PropertyToID("_HeatParams");
    public static readonly int HeatCount = Shader.PropertyToID("_HeatCount");

    // --- Sastrugi (spec §18.4) ---
    public static readonly int SastrugiWindDir = Shader.PropertyToID("_SastrugiWindDir");
    public static readonly int SastrugiNoise = Shader.PropertyToID("_SastrugiNoise");

    // --- Kar yüzeyi materyali ---
    public static readonly int FallbackSWE = Shader.PropertyToID("_FallbackSWE");
    // KOT KAR CIZGISI. Yukaridaki `_SnowLineY` KARAKTER ustu birikmenin
    // kendi cizgisi (spec 16.1) ve MaterialPropertyBlock ile veriliyor; bu
    // ise ARAZININ hangi kottan yukarisinin karli oldugu, global. Ayni ada
    // konsalardi property block globali ezerdi.
    public static readonly int FallbackRhoN = Shader.PropertyToID("_FallbackRhoN");
    public static readonly int SnowBreakup = Shader.PropertyToID("_SnowBreakup");
    public static readonly int SnowDetailNormal = Shader.PropertyToID("_SnowDetailNormal");

    public static readonly int ShadowTint = Shader.PropertyToID("_ShadowTint");
    public static readonly int TranslucencyStrength = Shader.PropertyToID("_TranslucencyStrength");
    public static readonly int SparkleCellSize = Shader.PropertyToID("_SparkleCellSize");
    public static readonly int SparkleDensity = Shader.PropertyToID("_SparkleDensity");
    public static readonly int SparkleSharpness = Shader.PropertyToID("_SparkleSharpness");
    public static readonly int SparkleIntensity = Shader.PropertyToID("_SparkleIntensity");
    public static readonly int SnowAORadius = Shader.PropertyToID("_SnowAORadius");
    public static readonly int SnowAOStrength = Shader.PropertyToID("_SnowAOStrength");

    // --- Kar yağışı (Faz 8) ---
    public static readonly int Flakes = Shader.PropertyToID("_Flakes");
    public static readonly int FlakeCapacity = Shader.PropertyToID("_FlakeCapacity");
    public static readonly int FlakeAliveCount = Shader.PropertyToID("_FlakeAliveCount");
    public static readonly int FlakeBaseSize = Shader.PropertyToID("_FlakeBaseSize");
    public static readonly int FlakeSeed = Shader.PropertyToID("_FlakeSeed");
    public static readonly int FlakeSeedU = Shader.PropertyToID("_FlakeSeedU");
    public static readonly int FlakeAtlas = Shader.PropertyToID("_FlakeAtlas");
    public static readonly int SpawnCenter = Shader.PropertyToID("_SpawnCenter");
    public static readonly int SpawnExtent = Shader.PropertyToID("_SpawnExtent");
    public static readonly int TurbulenceIntensity = Shader.PropertyToID("_TurbulenceIntensity");
    public static readonly int TurbulenceFrequency = Shader.PropertyToID("_TurbulenceFrequency");
    public static readonly int TurbulenceDrag = Shader.PropertyToID("_TurbulenceDrag");
    public static readonly int FlutterFreq = Shader.PropertyToID("_FlutterFreq");
    public static readonly int FlutterAmp = Shader.PropertyToID("_FlutterAmp");
    public static readonly int DriftOrigin = Shader.PropertyToID("_DriftOrigin");
    public static readonly int DriftStripLength = Shader.PropertyToID("_DriftStripLength");
    public static readonly int DriftStripWidth = Shader.PropertyToID("_DriftStripWidth");
    public static readonly int StretchAlongVelocity = Shader.PropertyToID("_StretchAlongVelocity");
    public static readonly int AlphaScale = Shader.PropertyToID("_AlphaScale");
    public static readonly int BurstGravity = Shader.PropertyToID("_BurstGravity");
    public static readonly int BurstDrag = Shader.PropertyToID("_BurstDrag");
    public static readonly int BurstWindPull = Shader.PropertyToID("_BurstWindPull");
    public static readonly int BurstGrowth = Shader.PropertyToID("_BurstGrowth");

    // --- Uzak kaskad ve kalıcılık (Faz 10) ---
    public static readonly int BlockBuffer = Shader.PropertyToID("_BlockBuffer");
    public static readonly int BlockOrigin = Shader.PropertyToID("_BlockOrigin");
    public static readonly int BlockTexels = Shader.PropertyToID("_BlockTexels");
    public static readonly int BlockStored = Shader.PropertyToID("_BlockStored");

    // --- Rüzgâr gölgesi ve taşınım (Faz 12) ---
    public static readonly int WindShadow = Shader.PropertyToID("_WindShadow");
    public static readonly int SkyVisY = Shader.PropertyToID("_SkyVisY");
    public static readonly int SnowRW = Shader.PropertyToID("_SnowRW");
    public static readonly int TrailRW = Shader.PropertyToID("_TrailRW");


    // --- Teşhis penceresi ---
    public static readonly int DebugMode = Shader.PropertyToID("_DebugMode");
    public static readonly int DebugRange = Shader.PropertyToID("_DebugRange");
    public static readonly int DebugBias = Shader.PropertyToID("_DebugBias");
    public static readonly int DebugGridSize = Shader.PropertyToID("_DebugGridSize");
    public static readonly int DebugWorldCenter = Shader.PropertyToID("_DebugWorldCenter");
    public static readonly int DebugWorldSize = Shader.PropertyToID("_DebugWorldSize");
}
