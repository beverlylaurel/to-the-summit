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
#define SEA_MIN_DEPTH            0.05

/// Wave damping at the shoreline (m). Below this depth the wave height goes
/// to zero; otherwise the mesh intersects the terrain and flickers.
/// [CALIBRATION]
#define SEA_SHORE_FADE_DEPTH     0.60

/// Depth at which horizontal displacement dies out in shallow water (m). The
/// wave steepens instead of spreading horizontally. [CALIBRATION]
#define SEA_CHOP_FADE_DEPTH      8.00

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
#define SEA_BREAK_FOAM_GAIN      1.60

// --- Foam (Jacobian) ---

/// Jacobian threshold and transition range. J < 0 means the surface has
/// folded; the threshold starts before that so foam enters smoothly.
/// [SOURCE: Tessendorf 2004 4.6 — folding test] [CALIBRATION: threshold value]
#define SEA_FOAM_J_THRESHOLD     0.55
#define SEA_FOAM_J_RANGE         0.55

/// Foam decay rate (1/s). Foam appears INSTANTLY and fades SLOWLY; with a
/// direct assignment foam would disappear instantly. [CALIBRATION]
#define SEA_FOAM_DECAY           0.28

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
