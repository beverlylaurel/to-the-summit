// ROLE: every constant of the sea system. Carries EXACTLY the same values as
// `SeaConstants.cs`; `SeaConstantsTest` verifies the parity.
// CALLED BY: all sea shaders, through SeaCommon.hlsl.

#ifndef SEA_CONSTANTS_INCLUDED
#define SEA_CONSTANTS_INCLUDED

// --- Physics ---

/// Gravity. [SOURCE: Tessendorf 2004 4.2]
#define SEA_G                    9.81

#define SEA_TWO_PI               6.28318530718

/// Index of refraction of water. [SOURCE: Tessendorf 2004 6.1.2, 6.3 sample shader]
#define SEA_WATER_IOR            1.34


// --- Spectrum (JONSWAP / TMA) ---

/// Peak sharpness. [SOURCE: Horvath 2015 / JONSWAP]
#define SEA_JONSWAP_GAMMA        3.30

/// Peak width; different below and above the peak frequency.
/// [SOURCE: JONSWAP]
#define SEA_JONSWAP_SIGMA_LO     0.07
#define SEA_JONSWAP_SIGMA_HI     0.09


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

/// The first tier that gets the hexagonal de-tiling; every tier from here up is
/// de-tiled. All tiers now participate. The former 967 m long-wave patch had to
/// stay whole because its cells were shorter than the swell. At 4093 m a tier-0
/// hex cell is about two kilometres wide, still five 16 s wavelengths, so the
/// transform no longer cuts the long crests. Removing the raw patch repeat also
/// matters from altitude.
#define SEA_HEX_TIER_MIN         0

/// Hexagons per patch, along one axis. Two keeps every cell at least twice as
/// wide as the longest wavelength assigned to that tier; smaller cells would
/// disguise repetition by cutting coherent waves into unrelated pieces.
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
/// to zero; otherwise the mesh intersects the terrain and flickers. This is
/// deliberately narrower than the optical fade: geometry must survive almost
/// to the beach without making the water/ground colour hand-off abrupt.
/// [CALIBRATION]
#define SEA_SHORE_GEOMETRY_FADE_DEPTH 0.18

/// Depth over which every visible water layer fades into the refracted ground.
/// On the measured 5.8% beach this is about ten metres of horizontal transition.
/// Sharing the 0.18 m geometry fade here compressed that hand-off to about three
/// metres and made the waterline read as a cut polygon again. [CALIBRATION]
#define SEA_SHORE_OPTICAL_FADE_DEPTH 0.60

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


// --- Rain rings ---
//
// They live in `Assets/Shaders/RainRings.hlsl`: a drop leaves the same ring in a
// puddle as it does in the ocean, so the sea does not own that maths.


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
/// WHERE FOAM STARTS, ON THE JACOBIAN. After the spectrum normalization fix,
/// the old 0.85 threshold activated 22.08% of tier 1 at U10=14 and the long
/// residue eventually covered 92.13%: a grey sheet. At 0.62 only the steepest
/// roughly two percent births foam; persistence and procedural breakup build the
/// visible coverage from that sparse source. [CALIBRATION]
#define SEA_FOAM_J_THRESHOLD     0.62
#define SEA_FOAM_J_RANGE         0.22

/// Foam fades exponentially, not linearly (1/s). [CALIBRATION]
///
/// A real whitecap has two stages: the active crest is bright for one to three
/// seconds, then a residue of bubbles drifts and fades for tens of seconds.
/// Separate channels make both possible: the bright cap has a 2.38 s time
/// constant and the residue a 10 s time constant, with part of the collapsed
/// cap transferred between them.
#define SEA_FOAM_DECAY           0.42
#define SEA_FOAM_RESIDUE_DECAY   0.10
#define SEA_FOAM_RESIDUE_TRANSFER 0.65
#define SEA_FOAM_WIND_DRIFT      0.018

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

/// UPPER BOUND on tiers. A preset may run fewer; the textures are always
/// created at this depth.
/// A single patch cannot carry both a 200 m swell and a 20 cm chop.
/// [SOURCE: Tessendorf 2004 4.4; Dupuy & Bruneton 2012]
#define SEA_TIER_COUNT           4

/// THE GRAVITY-CAPILLARY WAVENUMBER, `k_m = sqrt(rho g / T)` = 370 rad/m, a 1.7 cm wave.
/// Below it gravity restores the surface, above it surface tension does; the phase speed
/// has its minimum exactly here.
/// [SOURCE: Elfouhaily et al. 1997 equation 24 and 43]
#define SEA_CAPILLARY_KM         370.0

/// The minimum phase speed, `c_m = sqrt(2g/k_m)` = 0.23 m/s -- the same number the rain
/// ring travels at, because it is the same physics.
#define SEA_CAPILLARY_CM         0.23

#endif
