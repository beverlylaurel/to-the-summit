// ROLE: every constant of the sea system, C# side. Carries EXACTLY the same
// values as `SeaConstants.hlsl`; `SeaConstantsTest` verifies the parity.
// CALLED BY: SeaManager, SeaSimulation, SeaBathymetry.

/// A NUMBER MUST HAVE A SINGLE SOURCE.
///
/// In the snow system the same constant was written separately in two files
/// (`MountainSurface` 0.28, `SnowBuildSurfaceFrom` 0.45) and the same snow
/// was drawn at two different brightnesses. A comment claimed "both paths
/// must use the same number" — but the number had been copied. Here a test
/// verifies the parity.
public static class SeaConstants
{
    // --- Physics ---

    /// Gravity. [SOURCE: Tessendorf 2004 §4.2]
    public const float G = 9.81f;

    public const float TwoPi = 6.28318530718f;

    /// Index of refraction of water. [SOURCE: Tessendorf 2004 §6.1.2]
    public const float WaterIor = 1.34f;

    /// Bulk reflectivity of the water volume. [SOURCE: Tessendorf 2004 §7.1]
    public const float BulkReflectivity = 0.04f;

    // --- Spectrum (JONSWAP / TMA) ---

    /// Peak sharpness. [SOURCE: Horvath 2015 / JONSWAP]
    public const float JonswapGamma = 3.30f;

    /// Peak width; different below and above the peak frequency.
    /// [SOURCE: JONSWAP]
    public const float JonswapSigmaLo = 0.07f;
    public const float JonswapSigmaHi = 0.09f;

    /// Deep-water steepness limit. [SOURCE: Michell 1893]
    public const float MichellSteepness = 0.142f;

    // --- Shallow water and breaking ---

    /// Floor depth that prevents division by zero (m). [CALIBRATION]
    public const float MinDepth = 0.05f;

    /// Wave damping at the shoreline (m). [CALIBRATION]
    public const float ShoreFadeDepth = 0.60f;

    /// Depth at which horizontal displacement dies out in shallow water (m).
    /// [CALIBRATION]
    public const float ChopFadeDepth = 8.00f;

    /// Breaker depth index, lower and upper end of the slope-dependent range.
    /// [SOURCE: DNV 2017; Galvin 1969 / Weggel 1972]
    public const float GammaMild = 0.55f;
    public const float GammaSteep = 1.10f;

    /// Foam gain produced by breaking. [CALIBRATION]
    public const float BreakFoamGain = 1.60f;

    // --- Foam (Jacobian) ---

    /// Jacobian threshold and transition range. [SOURCE: Tessendorf 2004 §4.6]
    public const float FoamJThreshold = 0.55f;
    public const float FoamJRange = 0.55f;

    /// Foam decay rate (1/s). [CALIBRATION]
    public const float FoamDecay = 0.28f;

    // --- FFT and grid ---

    /// FFT grid size. [SOURCE: Tessendorf 2004 §4.4]
    public const int FftSize = 256;
    public const int FftLog2 = 8;

    /// Number of tiers. [SOURCE: Tessendorf 2004 §4.4; Dupuy & Bruneton 2012]
    public const int TierCount = 3;
}
