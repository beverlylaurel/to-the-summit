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

    /// FETCH IS THE OPEN OCEAN'S, NOT THE VISIBLE WATER'S. It was 12 km,
    /// derived from the drawn sea area, and that is a lagoon's number: with
    /// the shore wind measured at 0.62 m/s it gives a peak wavelength of
    /// 2.3 m and a period of 1.22 s — the whole sea was one uniform ripple.
    /// This coast opens onto an ocean; what limits the fetch is the ocean,
    /// not the piece of it we draw.
    [Tooltip("Fetch — the distance the wind blows over water (m).")]
    [Min(100f)] public float fetch = 150000f;

    /// HOW NARROW THE WIND SEA'S CRESTS ARE, not whether there is swell —
    /// the swell is its own partition below. At 0.72 it narrowed the
    /// spreading so far that every crest came out parallel: measured, the
    /// wind-band energy share went from 62.6% to 88.2% between 0 and 1.
    /// A real wind sea is short-crested and confused.
    /// [SOURCE: Horvath 2015 "swell" parameter]
    [Tooltip("How much the wind sea's crests line up. 0 = confused chop, 1 = parallel trains.")]
    [Range(0f, 1f)] public float swell = 0.18f;

    [Header("Swell — the spectrum's second peak")]
    /// THE SEA IS NEVER DEAD. A real open coast carries waves born in storms
    /// hundreds of kilometres away: long, slow, narrow-crested, and unrelated
    /// to the local wind. Without this partition the sea went flat whenever
    /// the wind did, and every wave in it was the same size — one peak in the
    /// spectrum can only make one size of wave.
    ///
    /// It is NOT a second weather source: the local wind still drives the
    /// wind sea and a storm still makes the sea rage. This is the sea's own
    /// physics, not the sky's.
    [Tooltip("Swell peak period (s). 10 s = a 156 m wavelength in deep water.")]
    [Range(4f, 20f)] public float swellPeriod = 10f;

    [Tooltip("Swell energy. 0 switches the partition off. [CALIBRATION]")]
    [Min(0f)] public float swellAlpha = 0.0075f;

    [Tooltip("Peak sharpness. Higher than the wind sea's 3.3: a swell arrives " +
             "as a narrow band of periods.")]
    [Range(1f, 12f)] public float swellGamma = 7f;

    [Tooltip("Directional spreading exponent (cosine-2s). Fixed across " +
             "frequency — a swell comes from one direction at every period.")]
    [Range(2f, 60f)] public float swellSpread = 26f;

    [Tooltip("The swell's angle from the wind direction (degrees). A swell " +
             "born in a distant storm rarely runs with today's wind; crossing " +
             "them is what breaks the corduroy look.")]
    [Range(-180f, 180f)] public float swellDirectionOffset = 38f;

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
    ///   tier 0: 967 m / 256 = 3.78 m  (deliberately coarse, it only carries
    ///                                   long waves — the swell lives here)
    ///   tier 1: 191 m / 256 = 0.75 m
    ///   tier 2:  37 m / 256 = 0.145 m
    ///
    /// THE THREE LENGTHS ARE PAIRWISE COPRIME. 512 / 128 / 24 shared a factor
    /// of 128 between the first two, so their tiles lined up and the repeat
    /// was visible on the water. All three are primes now, so no two tiles
    /// come back into phase within the drawn sea.
    /// [SOURCE: rtryan98, "Ocean Rendering" — "if a common factor for any two
    ///  values of L exists, then the tiling will be visible"]
    ///
    /// Tier 0 also GREW: at 512 m a 156 m swell had barely three periods per
    /// tile, i.e. three modes — that few modes is a periodic pattern by
    /// definition. At 967 m it has six.
    [Tooltip("The square each tier covers in the world (m).")]
    public Vector3 patchSizes = new Vector3(967f, 191f, 37f);

    [Tooltip("Summation weight of each tier.")]
    public Vector3 tierWeights = new Vector3(1f, 1f, 1f);

    /// Choppy displacement scale. It sharpens the crests and broadens the
    /// troughs — the nonlinear behaviour that makes the FFT representation
    /// look real. [SOURCE: Tessendorf 2004 equation 44]
    ///
    /// IT FOLLOWS THE WIND, IT IS NOT A CONSTANT. At a fixed 1.1 the surface
    /// never folded: measured, in a full storm the smallest Jacobian over the
    /// whole field was 0.580 while the foam threshold is 0.55 — so a whitecap
    /// NEVER appeared, at any wind. A calm swell really is smooth and a storm
    /// sea really is steep enough to break; one number cannot be both.
    [Tooltip("Choppy displacement in calm air. A swell is round, not sharp.")]
    [Range(0f, 3f)] public float choppinessCalm = 0.55f;

    [Tooltip("Choppy displacement in a full storm. High enough for the surface " +
             "to fold — that fold is what makes a whitecap.")]
    [Range(0f, 3f)] public float choppinessStorm = 2.4f;

    [Tooltip("The wind speed at which the storm value is reached (m/s).")]
    [Min(1f)] public float choppinessWindFull = 15f;

    /// ONE PLACE COMPUTES IT. The simulation writes the choppiness onto the
    /// compute shader and `SeaManager` writes the same number as a global for
    /// the vertex stage. Two separate `Lerp`s would drift the moment one of
    /// them was edited, and the displacement the foam is computed from would
    /// stop matching the displacement the mesh is built with.
    public float ChoppinessAt(float windSpeed) =>
        Mathf.Lerp(choppinessCalm, choppinessStorm,
                   Mathf.Clamp01(windSpeed / Mathf.Max(1f, choppinessWindFull)));

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
