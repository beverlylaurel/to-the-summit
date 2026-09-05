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

    /// Amplitude of a single sinusoid with unit rms.
    public const float Sqrt2 = 1.41421356f;

    /// Index of refraction of water. [SOURCE: Tessendorf 2004 §6.1.2]
    public const float WaterIor = 1.34f;


    // --- Spectrum (JONSWAP / TMA) ---

    /// Peak sharpness. [SOURCE: Horvath 2015 / JONSWAP]
    public const float JonswapGamma = 3.30f;

    /// Peak width; different below and above the peak frequency.
    /// [SOURCE: JONSWAP]
    public const float JonswapSigmaLo = 0.07f;
    public const float JonswapSigmaHi = 0.09f;


    /// Period the procedural noise folds its coordinate into before hashing.
    /// The shore sits kilometres from the origin and a float stops carrying the
    /// fraction there; measured, the hash fell to 39 distinct values in 4096 cells
    /// and the foam came out as a lattice (`RATIONALE.md`).
    public const float HashPeriod = 512f;

    /// Distance over which the sea bed reaches deep water outside the terrain (m).
    /// A 4.4% gradient from the measured 25.4 m edge depth. [CALIBRATION]
    public const float OffshoreRamp = 4000f;

    // --- Shallow water and breaking ---

    /// Floor depth that prevents division by zero (m). [CALIBRATION]
    public const float MinDepth = 0.05f;

    /// Geometry damping at the waterline (m). The previous shared 0.60 m
    /// value erased the wave over roughly ten horizontal metres on this beach;
    /// optics retain that wider transition independently. [CALIBRATION]
    public const float ShoreGeometryFadeDepth = 0.18f;

    /// Optical hand-off into the refracted ground (m). [CALIBRATION]
    public const float ShoreOpticalFadeDepth = 0.60f;

    /// Minimum screen-space width of the optical hand-off on a steep bank (pixels).
    /// Gentle beaches remain governed by ShoreOpticalFadeDepth. [CALIBRATION]
    public const float ShoreOpticalMinPixels = 2.00f;

    /// Waterline displacement by the foam noise (m of depth). [CALIBRATION]
    public const float ShoreEdgeNoise = 0.06f;

    /// Depth at which horizontal displacement dies out in shallow water (m).
    /// [CALIBRATION]
    public const float ChopFadeDepth = 8.00f;

    /// Breaker depth index, lower and upper end of the slope-dependent range.
    /// [SOURCE: DNV 2017; Galvin 1969 / Weggel 1972]
    public const float GammaMild = 0.55f;
    public const float GammaSteep = 1.10f;

    /// Foam gain produced by breaking. [CALIBRATION]
    public const float BreakFoamGain = 0.85f;

    // --- Foam (Jacobian) ---

    /// Jacobian threshold and transition range. [SOURCE: Tessendorf 2004 §4.6]
    public const float FoamJThreshold = 0.62f;
    public const float FoamJRange = 0.22f;

    /// Bright whitecap decay rate (1/s). [CALIBRATION]
    public const float FoamDecay = 0.42f;

    /// Aerated residue decay rate (1/s). The second, slower lifetime keeps a
    /// broken crest visible after its bright cap has collapsed. [CALIBRATION]
    public const float FoamResidueDecay = 0.10f;

    /// Share of vanished bright foam transferred into the residue channel.
    public const float FoamResidueTransfer = 0.65f;

    /// Surface drift as a fraction of the local 10 m wind speed.
    public const float FoamWindDrift = 0.018f;

    // --- FFT and grid ---

    /// FFT grid size. [SOURCE: Tessendorf 2004 §4.4]
    public const int FftSize = 256;

    /// Number of tiers. [SOURCE: Tessendorf 2004 §4.4; Dupuy & Bruneton 2012]
    public const int TierCount = 4;
}
