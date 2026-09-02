// ROLE: every constant of the sea system. Carries EXACTLY the same values as
// `SeaConstants.cs`; `SeaConstantsTest` verifies the parity.
// CALLED BY: all sea shaders, through SeaCommon.hlsl.

#ifndef SEA_CONSTANTS_INCLUDED
#define SEA_CONSTANTS_INCLUDED

// --- Physics ---

/// Gravity. [SOURCE: Tessendorf 2004 4.2]
#define SEA_G                    9.81

#define SEA_SQRT2                1.41421356
#define SEA_TWO_PI               6.28318530718

/// Index of refraction of water. [SOURCE: Tessendorf 2004 6.1.2, 6.3 sample shader]
#define SEA_WATER_IOR            1.34

/// Bulk reflectivity of the water volume, treated as a Lambertian reflector.
/// [SOURCE: Tessendorf 2004 7.1]
#define SEA_BULK_REFLECTIVITY    0.04

// --- Spectrum (JONSWAP / TMA) ---

/// Peak sharpness. [SOURCE: Horvath 2015 / JONSWAP]
#define SEA_JONSWAP_GAMMA        3.30

/// Peak width; different below and above the peak frequency.
/// [SOURCE: JONSWAP]
#define SEA_JONSWAP_SIGMA_LO     0.07
#define SEA_JONSWAP_SIGMA_HI     0.09

/// Deep-water steepness limit. If the FFT output exceeds it the wave already
/// produces foam through the Jacobian test; NO separate check is written.
/// [SOURCE: Michell 1893]
#define SEA_MICHELL_STEEPNESS    0.142

// --- Shallow water and breaking ---

/// Floor depth that prevents division by zero (m). [CALIBRATION]
/// Period the noise coordinate is folded into before hashing. Big enough that the
/// repeat is never in view (512 cells is 640 m at the foam's tiling), small enough
/// that a float still carries the fraction.
#define SEA_HASH_PERIOD          512.0

/// Distance over which the sea bed reaches deep water outside the terrain (m).
/// Measured: the edge is 25.4 m deep, deep water is 200 m, so this length is a
/// 4.4% gradient — a continental slope. The terrain's own bed falls at 0.61% and
/// holding that would need 28.6 km, far past anything the mesh draws (4064 m).
#define SEA_OFFSHORE_RAMP        4000.0

/// The first tier that gets the hexagonal tiling; every tier from here up is
/// tiled. Measured repeats inside the visible sea (4064 m):
///
///     tier 2   L =  37 m   110 repeats   tiled
///     tier 1   L = 191 m    21 repeats   tiled
///     tier 0   L = 967 m     4 repeats   NOT tiled
///
/// Tier 0 stays whole for two reasons, and the second is the important one: four
/// repeats across the whole visible sea is at the edge of noticing, and tier 0
/// carries the long swell whose crests run for hundreds of metres. Cutting those
/// into 280 m hexagons is exactly how the big waves were lost once before.
#define SEA_HEX_TIER_MIN         1

/// Hexagons per patch, along one axis. Chosen by measurement on the REAL field
/// (read back from the GPU, 1200 m of line across the crests at 8 m/s):
///
///     tiles   tier1 cell   peak repeat
///     none         -       0.489 @ 190 m     <- tier 1's own patch
///     2.00        96 m     0.322 @  61 m
///     3.46        55 m     0.343 @  64 m
///     5.50        35 m     0.419 @  61 m
///     8.00        24 m     0.372 @  61 m
///
/// 2.0 wins, and the big cells are also the safest for the wave shape: a hexagon
/// smaller than the waves it carries would cut them up.
#define SEA_HEX_TILES            2.0

/// SUBSURFACE GLOW — the forward-scattering lobe's sharpness.
///
/// Water scatters strongly FORWARD (Petzold's measured phase function is two to
/// three orders of magnitude higher near zero degrees than sideways), so the light
/// that entered the back of a crest leaves it in nearly the same direction. You see
/// it when you look towards the sun through a wave, and not otherwise.
#define SEA_SSS_POWER            4.0

/// How much of the light entering a crest comes back out towards the eye.
/// [CALIBRATION] The physical chain is backscatter coefficient x path x phase
/// function; for coastal water that is a couple of percent times a forward lobe of
/// a few hundred, which lands in this range. The tint is NOT a separate colour: it
/// is the sunlight attenuated over the path through the crest, so it comes from the
/// same `_SeaExtinctionRGB` the depth colour uses.
#define SEA_SSS_GAIN             0.35

#define SEA_MIN_DEPTH            0.05

/// Wave damping at the shoreline (m). Below this depth the wave height goes
/// to zero; otherwise the mesh intersects the terrain and flickers.
/// [CALIBRATION]
#define SEA_SHORE_FADE_DEPTH     0.60

/// How far the waterline is displaced by the foam's own noise (m of depth).
/// On the measured 5% shore slope this moves the line about 1.2 m, which stays
/// under the 2.9 m feature size of the noise that produces it.
#define SEA_SHORE_EDGE_NOISE     0.06

/// Depth at which horizontal displacement dies out in shallow water (m). The
/// wave steepens instead of spreading horizontally. [CALIBRATION]
#define SEA_CHOP_FADE_DEPTH      8.00

// ------------------------------------------------------ shore wave train

/// How far out the shore train reaches, as a multiple of the BREAKING depth.
///
/// This was tied to the deep-water wavelength (L0/2, "where a wave first feels
/// the bottom") and that is far too far out: L0/2 is 37 m of water for a 6.9 s
/// sea, which on this 3.4% shore is a kilometre offshore. Over that whole band
/// the open-sea field was being replaced by a train whose height is capped at
/// gamma*h/2 -- measured, 48% of the swell was erased in 10 m of water, and the
/// big waves went missing.
///
/// A wave feeling the bottom is not the same as a wave breaking. The shore train
/// belongs to the surf zone, so it starts where the wave actually breaks:
/// h_b = Hs * shoal / gamma, about 5 m here, 140 m offshore.
#define SEA_SHORE_WAVE_BREAK_MULT 1.5

/// THE CUSP. For a trochoidal wave `kA < 1` is a sharp crest, `kA = 1` is a corner and
/// beyond it the surface passes through itself. The shore train's forward throw is held
/// at the corner: a pitched face, never a torn mesh.
/// [SOURCE: Tessendorf 2004, section 4.1]
#define SEA_SHORE_THROW_AK       1.0

/// How much of the breaking limit the shore train claims. The rest stays with
/// the open-sea field, which is still shoaling underneath it.
///
/// `amp` is a half-height, so the raw limit `gamma * h / 2` is a wave exactly at
/// the point of breaking everywhere. The saturated inner surf zone does not run
/// that hard: 0.65 of the limit puts the significant height at `H/h = 0.51`.
/// [SOURCE: Thornton & Guza 1982; saturated H_rms/h = 0.42, and Hs = 1.41 H_rms]
///
/// APPLIED ONCE. It used to weight the crossfade in `SeaDeform` as well, so the
/// surface saw 0.65^2 = 0.42 of the limit.
#define SEA_SHORE_WAVE_SHARE     0.65

/// How much of the SURFACE the shore train may claim at the waterline. Not the
/// same question as the height share, and it took a measurement to separate them.
///
/// Refraction turns the long waves parallel to the beach, and the shore train is
/// how that is drawn. It does not turn the short chop riding on top: refraction
/// works on the ratio of wavelength to the depth scale, so a 30 m swell is turned
/// where a 2 m wind wave barely is. Something of the open-sea field has to survive
/// into the surf zone, and it is what carries the small detail there.
///
/// Measured: letting the crossfade reach 1.0 erased that field and the shallow
/// water went flat -- the frame at 2 m depth dropped from luma 24.6 to 11.5 and the
/// sand went with it. The ceiling is not decoration.
#define SEA_SHORE_WAVE_TAKE_MAX  0.65


/// BREAKER DEPTH INDEX, SLOPE DEPENDENT.
///
/// McCowan's 0.78 is the most common first guess in engineering practice but
/// it is NOT CONSTANT: on very mild slopes the lower bound drops to 0.55, on
/// steep shores it rises above 1.0. Hence a slope-dependent lerp rather than
/// a fixed 0.78.
/// [SOURCE: McCowan 1894; Nelson 1983; DNV 2017; Galvin 1969; Weggel 1972]
#define SEA_GAMMA_MILD           0.55
#define SEA_GAMMA_STEEP          1.10

/// Foam gain produced by breaking. [CALIBRATION]
#define SEA_BREAK_FOAM_GAIN      0.85

// --- Foam (Jacobian) ---

/// Jacobian threshold and transition range. J < 0 means the surface has
/// folded; the threshold starts before that so foam enters smoothly.
/// [SOURCE: Tessendorf 2004 4.6 — folding test] [CALIBRATION: threshold value]
/// WHERE FOAM STARTS, ON THE JACOBIAN. Solved from a measured law, not chosen.
///
/// Monahan & O'Muircheartaigh 1980 give the whitecap coverage of a real sea as
/// `W = 3.84e-6 * U10^3.41`. Ours was 10 to 30 times short of it:
///
///     U10    bizim     Monahan
///       8    0.00%      0.46%
///      12    0.06%      1.84%
///      15    0.23%      3.93%
///      20    0.37%     10.49%
///
/// Solving for the threshold that reproduces `W` at each wind gives 0.837, 0.835,
/// 0.842, 0.866, 1.023 — very nearly the SAME number. That is the finding: the
/// Jacobian's statistics already carry the right wind dependence, and the only
/// thing wrong was where the line was drawn. 0.85 covers 6 to 20 m/s; the storm end
/// stays slightly conservative on purpose.
#define SEA_FOAM_J_THRESHOLD     0.85
#define SEA_FOAM_J_RANGE         0.85

/// Foam decay rate (1/s). Foam appears INSTANTLY and fades SLOWLY; with a
/// direct assignment foam would disappear instantly. [CALIBRATION]
/// FOAM FADES EXPONENTIALLY, NOT LINEARLY (1/s).
///
/// A real whitecap has two stages: the active crest is bright for one to three
/// seconds, then a residue of bubbles drifts and fades for tens of seconds. A
/// linear decay cannot be both — at 0.28/s foam went from full to nothing in 3.6 s
/// and the sea never looked used.
///
/// An exponential does both with one number. At 0.15/s the foam stays bright
/// (over 0.7) for 2.4 s and stays visible (over 0.05) for 20 s.
#define SEA_FOAM_DECAY           0.15

// --- FFT and grid ---

/// UPPER BOUND of the FFT grid. Quality presets run below it (`_SeaFftSize`);
/// this is the value of `numthreads` and the largest texture size — it does
/// not change.
///
/// `numthreads` IS NOT TIED TO A KEYWORD. A variant-dependent `numthreads`
/// would mean a separate `GetKernelThreadGroupSizes` and a separate dispatch
/// count per variant; with a smaller FFT half of the 256 threads idle, but
/// the barriers stay on a single branch and no silent undefined behaviour
/// appears.
/// [SOURCE: Tessendorf 2004 4.4 — "For many situations, values in the
/// range 128 to 512 are sufficient"]
#define SEA_FFT_SIZE             256
#define SEA_FFT_LOG2             8

/// UPPER BOUND on tiers. A preset may run fewer; the textures are always
/// created at this depth.
/// A single patch cannot carry both a 200 m swell and a 20 cm chop.
/// [SOURCE: Tessendorf 2004 4.4; Dupuy & Bruneton 2012]
#define SEA_TIER_COUNT           3

#endif
