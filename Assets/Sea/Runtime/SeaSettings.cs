// ROLE: settings of the sea system. No numbers buried in code; supplied from
// outside as an asset (CLAUDE.md — settings move into a ScriptableObject).
// CALLED BY: SeaManager, SeaSimulation, SeaBathymetry, SeaMeshBuilder.

using UnityEngine;

/// PATCH SIZES ARE A SETTING, NOT A CONSTANT.
///
/// Spec §6.6 marks all three tiers `[CALIBRATION]` and they change with the
/// quality preset, so they did not go into `SeaConstants`. The power-of-two
/// rule is `[SOURCE]` though — the resolution lives in
/// `SeaConstants.FftSize`, not here.
[CreateAssetMenu(menuName = "To The Summit/Sea Settings", fileName = "SeaSettings")]
public class SeaSettings : ScriptableObject
{
    [Header("Quality")]
    /// THE QUALITY TIER DRIVES FFT RESOLUTION, TIER COUNT AND THE MESH.
    /// The numbers live in the `SeaQuality.Of` table (spec §15.3); only which
    /// tier we are on is stored here.
    [Tooltip("Quality tier. The numbers live in the SeaQuality table.")]
    public SeaQualityPreset quality = SeaQualityPreset.Medium;

    [Header("Sea level")]
    /// THE SEA LEVEL WAS CHOSEN BY MEASUREMENT.
    ///
    /// The shore profile along the western edge of the terrain was measured
    /// at twelve cross sections along Z. At 30 m the water strip is 2.7–4.1 km
    /// in the band z in [-9000, +2000] — the range we wanted. At 10 m it is
    /// 2.0 km (too narrow); at 60 m it is 4.0 km but too much of the
    /// mountain's foot goes under water.
    [Tooltip("World Y coordinate of the sea surface (m).")]
    public float seaLevelY = 30f;

    [Tooltip("Default water depth outside the terrain (m). Open sea to the horizon.")]
    [Min(10f)] public float deepWaterDepth = 200f;

    [Header("Spectrum")]
    /// The FFT works with a single depth value; local depth variation is
    /// applied on the mesh (spec §6.4).
    [Tooltip("Mean depth the spectrum assumes (m).")]
    [Min(1f)] public float spectrumDepth = 60f;

    /// The distance the wind blows over water. The sea area is ~250 km², so
    /// its characteristic length is ~15 km; the fetch is of that order.
    [Tooltip("Fetch — the distance the wind blows over water (m).")]
    [Min(100f)] public float fetch = 12000f;

    /// A sea viewed from the shore wants regular wave trains.
    /// [SOURCE: Horvath 2015 "swell" parameter]
    [Tooltip("Swell fraction. 0 = local chop, 1 = distant storm swell.")]
    [Range(0f, 1f)] public float swell = 0.72f;

    /// [SOURCE: Tessendorf 2004 equation 41]
    [Tooltip("Small wave cutoff length (m).")]
    [Min(0.01f)] public float smallWaveCutoff = 0.15f;

    /// Every frequency is rounded to a multiple of this. MANDATORY: it
    /// prevents loss of float precision as `t` grows over a long session.
    /// [SOURCE: Tessendorf 2004 §4.2]
    [Tooltip("Loop period (s). The simulation repeats over this interval.")]
    [Min(10f)] public float loopPeriod = 200f;

    [Header("Tiers (spec §6.6)")]
    /// THREE TIERS. A single patch cannot carry both a 200 m swell and a
    /// 20 cm chop. The `dx` values must be 10–1000 times smaller than `U²/g`
    /// (spec §6.6); for U = 8 m/s, U²/g = 6.52 m.
    ///
    ///   tier 0: 512 m / 256 = 2.00 m  -> ratio  3.3 (deliberately low, it
    ///                                    only carries long waves)
    ///   tier 1: 128 m / 256 = 0.50 m  -> ratio 13.0
    ///   tier 2:  24 m / 256 = 0.094 m -> ratio 69.4
    [Tooltip("The square each tier covers in the world (m).")]
    public Vector3 patchSizes = new Vector3(512f, 128f, 24f);

    [Tooltip("Summation weight of each tier.")]
    public Vector3 tierWeights = new Vector3(1f, 1f, 1f);

    /// Choppy displacement scale. It sharpens the crests and broadens the
    /// troughs — the nonlinear behaviour that makes the FFT representation
    /// look real. [SOURCE: Tessendorf 2004 equation 44]
    [Tooltip("Choppy displacement scale.")]
    [Range(0f, 2f)] public float choppiness = 1.1f;

    /// DIFFERENT PER TIER. Full choppiness at high wave numbers compresses
    /// the chop and leads to knotting (spec §6.7).
    [Tooltip("Choppiness multiplier per tier.")]
    public Vector3 choppinessPerTier = new Vector3(1f, 0.85f, 0.45f);

    [Header("Shallow water (spec §8)")]
    /// Green's law goes to infinity in very shallow water; in reality
    /// breaking takes over. [CALIBRATION]
    [Tooltip("Upper bound of the shoaling gain.")]
    [Range(1f, 5f)] public float maxShoalingGain = 2.2f;

    /// The largest amount by which the run-up band raises the water level (m).
    /// The wet sand band breathes with it. [CALIBRATION]
    [Tooltip("Depth contribution of the run-up band (m).")]
    [Range(0f, 2f)] public float runupMaxDepth = 0.45f;

    [Header("Optics (spec §12)")]
    /// Red decays fastest, blue slowest — the reason water looks blue.
    /// Tuned for coastal water. [CALIBRATION]
    [Tooltip("Extinction coefficient per channel (1/m).")]
    public Vector3 extinctionRgb = new Vector3(0.30f, 0.08f, 0.05f);

    /// [SOURCE: Tessendorf 2004 §6.3 sample shader — upwelling = (0, 0.2, 0.3)]
    [Tooltip("Upwelling color.")]
    public Color upwellingColor = new Color(0.00f, 0.20f, 0.30f);

    [Tooltip("Refraction offset strength.")]
    [Range(0f, 2f)] public float refractionStrength = 0.35f;

    /// Calm and stormy surface roughness; blended by wind speed.
    [Range(0f, 0.5f)] public float roughnessCalm = 0.02f;
    [Range(0f, 0.5f)] public float roughnessRough = 0.14f;

    [Header("Foam (spec §13)")]
    [Tooltip("Depth at which shore foam appears (m).")]
    [Min(0.1f)] public float shoreFoamDepth = 1.2f;

    /// Foam is a scattering surface, not a glossy one. [CALIBRATION]
    public Color foamColor = new Color(0.92f, 0.94f, 0.95f);
    [Range(0f, 1f)] public float foamRoughness = 0.85f;

    /// World scale of the whitecap pattern (1/m). The pattern is stretched
    /// along the fold direction. [CALIBRATION]
    [Tooltip("Whitecap pattern scale (1/m).")]
    [Range(0.05f, 4f)] public float foamTiling = 0.8f;

    /// THE SHORE FOAM EDGE IS BROKEN UP WITH NOISE. Without it the foam band
    /// becomes a straight line and the shoreline looks drawn on
    /// (spec §18 pitfall table). [SOURCE: Crest, SIGGRAPH 2017]
    [Tooltip("Scale of the shore foam edge noise (1/m).")]
    [Range(0.05f, 2f)] public float foamBreakupTiling = 0.35f;
}
