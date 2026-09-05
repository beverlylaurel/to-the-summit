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
    /// THE PERIOD IS AN EVENT, NOT A CONSTANT.
    ///
    /// It was fixed at 10 s, and a fixed period fixes the breaker type: the Iribarren
    /// number `tan(beta)/sqrt(H0/L0)` came out 0.32 on this shore and never moved, which
    /// is a SPILLING breaker -- the surf never plunges, so it is never a wave anyone
    /// could ride. The same beach with a 16 s swell gives 0.78, which plunges.
    /// [SOURCE: Battjes 1974; thresholds 0.5 and 3.3, `RATIONALE.md`]
    ///
    /// A wind sea's own period is short; a groundswell that has travelled from a distant
    /// storm is long. Both reach the same beach on different days, so the period wanders
    /// between them.
    [Tooltip("Kısa peryot ucu (s): yakın rüzgârın denizi.")]
    [Range(4f, 12f)] public float swellPeriodShort = 8f;

    [Tooltip("Uzun peryot ucu (s): uzaktan gelen ölü dalga. 16 s = 400 m dalga boyu.")]
    [Range(8f, 22f)] public float swellPeriodLong = 16f;

    [Tooltip("Ölü dalga olayının tam turu (s). Uzak fırtınanın kendi saati; " +
             "yerel havayla ilgisi yok.")]
    [Min(30f)] public float swellEventSeconds = 900f;

    /// ENERGY RIDES WITH PERIOD, BECAUSE BOTH COME FROM THE SAME DISTANT STORM.
    ///
    /// A 16 s swell is not a long, small wave: it is the signature of a big storm far
    /// away, and it arrives carrying that storm's energy. Measured without this, the
    /// period wandered but the total spectrum's peak stayed on the WIND sea at 5.7 s,
    /// so the shore never felt the swell at all.
    [Tooltip("Olayın tepesinde ölü dalga enerjisi kaç katına çıkar.")]
    [Range(1f, 12f)] public float swellEventGain = 6f;

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

    [Header("Sea-state evolution")]
    [Tooltip("Seed of the remote sea-state sequence. The local weather has its own seed.")]
    public int seaStateSeed = 7319;

    [Tooltip("Duration of one remote swell state keyframe (s). Adjacent states blend smoothly.")]
    [Min(60f)] public float seaStateSegmentSeconds = 360f;

    [Tooltip("How slowly the remote swell direction wanders (s per keyframe).")]
    [Min(120f)] public float swellDirectionSegmentSeconds = 1200f;

    [Tooltip("Minimum multiplier of the background swell energy.")]
    [Range(0.1f, 2f)] public float swellEnergyMin = 0.55f;

    [Tooltip("Maximum multiplier reached by a strong remote swell event.")]
    [Range(2f, 12f)] public float swellEnergyMax = 8f;

    [Tooltip("Response time while the local wind sea is growing (s).")]
    [Min(1f)] public float windSeaRiseSeconds = 45f;

    [Tooltip("Response time while the local wind sea is calming down (s).")]
    [Min(1f)] public float windSeaFallSeconds = 120f;

    /// [SOURCE: Tessendorf 2004 equation 41]
    [Tooltip("Small wave cutoff length (m).")]
    /// IT IS THE FINEST GRID'S NYQUIST, NOT A FREE NUMBER. Tessendorf's `exp(-k^2 l^2)`
    /// has its knee at `k = 1/l`, and the only wavenumber worth putting it at is the one
    /// past which the grid cannot represent a wave at all: `k_nyq = pi N / L`, which for
    /// 256 samples over the 1.2 m patch is 670 rad/m. So `l = 1/670`.
    ///
    /// MEASURED TWICE ON THE WAY HERE. At 0.15 (the old value, from when the finest patch
    /// was 37 m) the whole capillary band came out as `exp(-(370*0.15)^2)` = zero. At the
    /// finest CELL, 0.0047, the knee landed at 210 rad/m -- below the capillary peak at
    /// 370 -- and left 5% of it: slope variance reached 59% of Cox-Munk instead of 32%,
    /// but the band it was meant to add was still being cut in half.
    [Min(0.0005f)] public float smallWaveCutoff = 0.0015f;

    /// Every frequency is rounded to a multiple of this. MANDATORY: it
    /// prevents loss of float precision as `t` grows over a long session.
    /// [SOURCE: Tessendorf 2004 §4.2]
    [Tooltip("Loop period (s). Kept beyond a normal observation window so recurrence is not visible.")]
    [Min(10f)] public float loopPeriod = 3600f;

    [Header("Tiers (spec §6.6)")]
    /// FOUR TIERS. A single patch cannot carry both a 400 m swell and a
    /// 20 cm chop. The `dx` values must be 10–1000 times smaller than `U²/g`
    /// (spec §6.6); for U = 8 m/s, U²/g = 6.52 m.
    ///
    ///   tier 0: 4093 m / 256 = 15.99 m (long swell; 10 periods even at 16 s)
    ///   tier 1:  509 m / 256 =  1.99 m
    ///   tier 2:   61 m / 256 =  0.24 m
    ///   tier 3:  1.2 m / 256 =  0.0047 m (capillary band)
    ///
    /// THE THREE GRAVITY LENGTHS ARE PAIRWISE COPRIME. 512 / 128 / 24 shared a factor
    /// of 128 between the first two, so their tiles lined up and the repeat
    /// was visible on the water. All three are primes now, so no two tiles
    /// come back into phase within the drawn sea.
    /// [SOURCE: rtryan98, "Ocean Rendering" — "if a common factor for any two
    ///  values of L exists, then the tiling will be visible"]
    ///
    /// Tier 0 grew again after the swell became event-driven. A 16 s wave is about
    /// 400 m long; the old 967 m patch held only 2.4 periods and therefore could
    /// only look like a repeating train. 4093 m holds more than ten.
    /// THE FOURTH PATCH IS THE CAPILLARY BAND, AND ITS SIZE IS NOT A TASTE.
    ///
    /// The gravity-capillary peak sits at `k_m = 370 rad/m`, a 1.7 cm wave
    /// [SOURCE: Elfouhaily et al. 1997 equation 24]. A 1.2 m patch on a 256 grid
    /// reaches `k = 2 pi * 128 / 1.2 = 670 rad/m`, so the peak is inside the band
    /// rather than at its edge; and the tier rule (four periods per patch) hands
    /// it everything under 30 cm, which is exactly where JONSWAP stops describing
    /// the water.
    [Tooltip("The square each tier covers in the world (m).")]
    public Vector4 patchSizes = new Vector4(4093f, 509f, 61f, 1.2f);

    [Tooltip("Summation weight of each tier.")]
    public Vector4 tierWeights = new Vector4(1f, 1f, 1f, 1f);

    /// WHERE ONE TIER STOPS AND THE NEXT BEGINS, IN WAVENUMBER.
    ///
    /// If all three tiers carried the same `k` range the energy would be counted
    /// three times. A tier carries a wavelength only if at least four full periods
    /// fit in its patch (lambda <= L/4); anything longer is handed to a coarser
    /// tier. The four is a [CALIBRATION].
    ///
    /// Both the GPU spectrum and the CPU slope moment read it here, so the two
    /// cannot drift apart.
    public Vector3 TierBandLimits => new Vector3(
        4f * SeaConstants.TwoPi / Mathf.Max(patchSizes.y, 1f),
        4f * SeaConstants.TwoPi / Mathf.Max(patchSizes.z, 1f),
        4f * SeaConstants.TwoPi / Mathf.Max(patchSizes.w, 0.05f));

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
    /// The capillary tier gets NO choppiness. Choppy displacement sharpens crests
    /// by moving water horizontally; at 1.7 cm that motion is smaller than the
    /// grid cell and only folds the tier into itself.
    public Vector4 choppinessPerTier = new Vector4(1f, 0.85f, 0.45f, 0f);

    [Header("Shallow water (spec §8)")]
    /// Green's law goes to infinity in very shallow water; in reality
    /// breaking takes over. [CALIBRATION]
    [Tooltip("Upper bound of the shoaling gain.")]
    [Range(1f, 5f)] public float maxShoalingGain = 2.2f;

    /// MEAN SLOPE OF THE BEACH FACE, USED BY THE RUN-UP.
    ///
    /// Stockdon's R2% needs the slope of the face the water climbs, and that is a
    /// property of the TERRAIN, not of the sea. The default is measured on the
    /// generated shore: waterline to 1.68 m of depth in 29 m. Regenerate the mountain
    /// at a different size and this wants re-measuring (`SCALE.md`).
    [Tooltip("Mean slope of the beach face (rise over run).")]
    [Range(0.005f, 0.4f)] public float shoreSlope = 0.058f;

    [Header("Optics (spec §12)")]
    /// Red decays fastest, blue slowest — the reason water looks blue.
    /// Tuned for coastal water. [CALIBRATION]
    [Tooltip("Extinction coefficient per channel (1/m).")]
    public Vector3 extinctionRgb = new Vector3(0.30f, 0.075f, 0.05f);

    /// Blue-dominant coastal upwelling; calibrated against the open and shallow-water views.
    [Tooltip("Upwelling color.")]
    public Color upwellingColor = new Color(0.08f, 0.45f, 0.65f);

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
