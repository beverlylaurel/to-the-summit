using UnityEngine;

/// PHYSICAL constants for the snow system (spec §0.10). Artistic settings live in SnowSettings.
/// CPU mirror of `Shaders/SnowConstants.hlsl`. Verified for parity by `SnowConstantsTest`.
public static class SnowConstants
{
    // --- Density (spec §6.3) ---

    /// Fresh powder snow, kg/m³.
    public const float RhoMin = 50f;

    /// Packed, icy snow, kg/m³.
    public const float RhoMax = 550f;

    /// Water density, kg/m³. SWE -> depth conversion multiplier.
    public const float RhoWater = 1000f;

    // --- Active region tracking (spec §6.4) ---

    /// Snap step in quad units for active region center.
    public const float SnapQuads = 2f;

    /// Integer version of `SnapQuads` for direct texel scroll math.
    public const int SnapQuadsInt = 2;

    /// Normalized distance from center where edge fade begins (spec §8.3).
    public const float EdgeFadeStart = 0.833f;

    // --- Snow / terrain intersection (spec §8.1) ---

    /// Snow below this depth is clipped to eliminate z-fighting, meters.
    public const float MinVisibleHeight = 0.004f;

    /// Edge fade transition range, meters.
    public const float EdgeFadeRange = 0.006f;

    // --- Deformation & compaction (spec §10.1) ---

    /// Normalized density of loose snow.
    public const float LooseN = 0.10f;

    /// Normalized density of packed snow.
    public const float PackedN = 0.55f;

    /// Sink scale on fully packed snow.
    public const float PackedSinkScale = 0.18f;

    // --- Rim displacement (spec §10.2) ---

    /// Offset along velocity direction for rim calculation, seconds.
    public const float RimVelocityBias = 0.04f;

    /// Layer thickness below which snow escapes sideways instead of compacting, meters.
    /// The boot sole's width: once the layer is as thick as the sole is wide, snow under
    /// the middle has as far to travel sideways as it is deep, so escaping is no easier
    /// than compressing and the packing limit takes over on its own.
    public const float LateralEscape = 0.110f;

    /// Rim blur radius, texels.
    public const float RimBlurTexels = 7f;

    // --- Trail infill (spec §10.3) ---

    /// SWE to snow depth conversion gain: `rho_water / rho_snow`.
    public static float FillGain(float rhoKar) => SnowConstants.RhoWater / Mathf.Max(rhoKar, 1f);

    /// Additional infill rate per m/s of wind above 4 m/s, m/s.
    public const float WindFill = 0.0012f;

    /// Angle of repose relaxation iterations per frame.
    public const int ReposeIterations = 10;

    // --- Accumulation, settling, melting (spec §11) ---

    /// Snow settling time constant, seconds (6 hours).
    public const float SettleTau = 21600f;

    /// Disturbance decay time constant, seconds.
    public const float DisturbTau = 900f;

    /// Degree-day melt factor, m/(°C·s) (4 mm/(°C·day)).
    public const float MeltDdf = 4.63e-8f;

    /// Wind-directed redistribution bias.
    public const float DriftBias = 0.45f;

    /// Melt acceleration factor during rain-on-snow events.
    public const float RainMeltBoost = 2.5f;

    /// Maximum SWE ceiling, meters.
    public const float SweMax = 0.60f;

    // --- Precipitation (spec §3.4, §17.2) ---

    /// Peak precipitation SWE rate, m/s (5 mm/hr).
    public const float MaxSweRate = 1.39e-6f;

    /// Peak snowfall flake spawn rate, flakes/second.
    public const float MaxFlakeRate = 16000f;

    // --- Sky visibility (spec §12.1) ---

    /// Sky visibility coverage area, meters.
    public const float SkyAreaSize = 96f;

    /// Movement threshold before refreshing sky map, meters.
    public const float SkyMoveThreshold = 4f;

    // --- Wind transport (spec §18.0, §18.1) ---

    /// Vertical acceleration of wind-influence surface, m/s².
    public const float WindShadowC = 0.7f;

    /// Surface erosion rate, m/(s·s).
    public const float ErosionRate = 1.16e-6f;

    /// Threshold wind speed at 10 m for loose snow drift, m/s.
    public const float DriftU10Loose = 5f;

    /// Threshold wind speed at 10 m for packed snow drift, m/s.
    public const float DriftU10Packed = 11f;

    // --- Heat sources (spec §18.2) ---

    /// Maximum simultaneous heat sources.
    public const int MaxHeatSources = 16;

    /// Heat field melt rate, m SWE / (m theta · s).
    public const float HeatMeltRate = 0.0009f;

    /// Heat field wetness rate, 1 / (m theta · s).
    public const float HeatWetRate = 0.25f;

    // --- Crust (spec §18.3) ---

    /// Temperature above which snow destabilizes, °C.
    public const float TWarm = 5f;

    /// Optimal melt-freeze crust formation temperature, °C.
    public const float TCool = -5f;

    /// Temperature threshold for dry settling under self-weight, °C.
    public const float TFreeze = -20f;

    /// Crust growth rate, 1/s.
    public const float CrustGain = 1.4e-4f;

    /// Wind slab contribution to crust growth, 1/s.
    public const float CrustWindGain = 6.0e-5f;

    /// Crust thermal degradation time constant, seconds.
    public const float CrustMeltTau = 1200f;

    /// Fresh snow burial coefficient for crust.
    public const float CrustBury = 220f;

    /// Crust threshold value for load-bearing surface.
    public const float CrustSolid = 0.55f;

    /// Sink penetration depth triggering crust fracture, meters.
    public const float CrustBreakPen = 0.05f;

    /// Sink scale on solid unbroken crust.
    public const float CrustSinkScale = 0.04f;

    // --- Sastrugi (spec §18.4) ---

    /// Wind direction smoothing time constant, seconds.
    public const float SastrugiWindTau = 120f;

    // --- Suspension curtains (spec §18.7) ---

    /// Suspension layer scale height, meters.
    public const float SuspScaleH = 1.1f;

    /// Base opacity for suspension curtains.
    public const float SuspAlphaBase = 0.16f;

    /// Maximum suspension layer ceiling, meters.
    public const float SuspMaxHeight = 5f;

    // --- Spray (spec §18.6) ---

    /// Particle count spawned per displaced cubic meter of snow.
    public const float SprayParticlesPerM3 = 40000f;

    // --- Compute (spec §20) ---

    /// Compute shader thread group dimension (8x8x1).
    public const int GroupSize = 8;

    /// Vertical bounds margin for snow mesh, meters (spec §8.2).
    public const float MeshBoundsHeight = 600f;

    // --- Snow surface micro-relief geometry ---

    /// Terrain vertex spacing, meters (30000 m / 4097).
    public const float TerrainVertexSpacing = 7.32f;

    /// Minimum geometric wavelength allowed into tessellation, meters.
    public const float TessMinWavelength = 0.50f;

    /// Fraction of snow depth subject to bedform relief modulation.
    public const float BedformDepthFrac = 0.60f;

    /// fBm micro-relief parameters: amplitude (m), base frequency, octave gain.
    public const float FbmAmp = 0.015f;
    public const float FbmScale = 0.80f;
    public const float FbmGain = 0.574f;

    /// Ripples: half-amplitude (m) and wavelength (m) transverse to wind.
    public const float RippleAmp = 0.006f;
    public const float RippleLength = 0.17f;

    /// Sastrugi: peak-to-trough height (m), transverse spacing, longitudinal span.
    public const float SastrugiHeight = 0.20f;
    public const float SastrugiLength = 0.90f;
    public const float SastrugiWidth = 2.20f;

    /// Drifts: relief height (m), transverse spacing, longitudinal span.
    public const float DriftHeight = 0.15f;
    public const float DriftLength = 0.90f;
    public const float DriftWidth = 1.60f;
}
