// Physical constants for the snow subsystem (GPU mirror of SnowConstants.cs).
// Included by: SnowCommon.hlsl and all snow shaders.

#ifndef SNOW_CONSTANTS_INCLUDED
#define SNOW_CONSTANTS_INCLUDED

// --- Density (spec §6.3) ---
#define SNOW_RHO_MIN                50.0
#define SNOW_RHO_MAX               550.0
#define SNOW_RHO_WATER            1000.0

// --- Region tracking (spec §6.4) ---
#define SNOW_SNAP_QUADS              2.0
#define SNOW_EDGE_FADE_START         0.833

// --- Snow / terrain intersection (spec §8.1) ---
#define SNOW_MIN_VISIBLE_HEIGHT      0.004
#define SNOW_EDGE_FADE_RANGE         0.006

// --- Sparkle distance gates ---
#define SNOW_SPARKLE_MAX_FOOTPRINT   0.04
#define SNOW_SPARKLE_FADE_START      28.0
#define SNOW_SPARKLE_FADE_END        50.0

// --- Relief normalization ---
#define SNOW_RELIEF_MAX_DEPTH          0.35
#define SNOW_RELIEF_MAX_STRETCH          3.0
#define SNOW_SHADOW_BOUNCE             0.43
#define SNOW_LATERAL_BOUNCE            0.43
#define SNOW_TERRAIN_VERTEX_SPACING    7.32
#define SNOW_TESS_MIN_WAVELENGTH            0.50

// --- Optics ---
#define SNOW_ICE_F0                    0.018
#define SNOW_ROUGH_PACKED              0.78
#define SNOW_ROUGH_FRESH               0.92

#define SNOW_SURF_RELIEF_FADE_START  30.0
#define SNOW_SURF_RELIEF_FADE_END   120.0
#define SNOW_SURF_RENK_FADE_START     80.0
#define SNOW_SURF_RENK_FADE_END      250.0

#define SNOW_LOCAL_MIN               0.002
#define SNOW_LOCAL_SKIRT_TEXELS       9.0

// --- Deformation (spec §10.1) ---
#define SNOW_LOOSE_N                 0.10
#define SNOW_PACKED_N                0.55
#define SNOW_SETTLE_TAIL             0.12
#define SNOW_SETTLE_TAIL_LEN         0.55
#define SNOW_SETTLE_TAIL_SCALE       5.0
#define SNOW_MAX_SINK                0.15
#define SNOW_LATERAL_ESCAPE          0.110

/// WIDTH OF THE SOFT SHOULDER BETWEEN THE PRESSED SOLE AND UNDISTURBED SNOW (m).
/// This is deliberately wider than the rubber's physical bevel: loose snow yields beside
/// the sole, so the visible transition is a small compression/shear zone rather than the
/// sharp edge of the boot itself.
///
/// MEASURED (2026-09-03) with the sole modelled as a sphere of the boot's own half width:
/// in 1 cm of snow the sink is 9.3 mm, so the sphere only touched out to a radius of
/// 3.1 cm -- the print came out 6.1 cm wide against an 11 cm boot, and at a 2.3 cm texel
/// that is 2.6 texels across. On screen it read as an axis-aligned dark block, not a print.
///
/// With a 35 mm shoulder the 2048 deformation grid resolves the transition with about
/// three source texels. It is centered on the physical sole boundary, so the half-depth
/// contour retains the boot's real width. The quintic profile in KDeform has zero slope
/// and curvature at both ends, joining the flat snow without a visible step.
#define SNOW_SOLE_EDGE               0.035
#define SNOW_PACKED_SINK_SCALE       0.18

// --- Rim displacement (spec §10.2) ---
#define SNOW_RIM_VELOCITY_BIAS       0.04
#define SNOW_RIM_STRENGTH            0.40
#define SNOW_RIM_SHADE               0.25
#define SNOW_RIM_MAX                 0.015
#define SNOW_RIM_CLUMP_SCALE        18.0
#define SNOW_RIM_CLUMP_FLOOR         0.70
#define SNOW_RIM_BLUR_METERS         0.035
#define SNOW_DENT_SLOPE_TEXELS       2.0

// --- Infill & Angle of repose (spec §10.3) ---
#define SNOW_SSS_TINT                float3(0.90, 0.94, 1.00)
#define SNOW_REPOSE_TAN              0.781
#define SNOW_STAND_LOOSE             0.140
#define SNOW_STAND_PACKED            0.200
#define SNOW_STAND_NOISE             0.50
#define SNOW_STAND_NOISE_SCALE       8.0
#define SNOW_EDGE_BREAK_METERS       0.003
#define SNOW_EDGE_BREAK_SCALE        9.0

// --- Micro-relief ---
#define SNOW_MICRO_AMP_A             0.0022
#define SNOW_MICRO_AMP_B             0.0011
#define SNOW_MICRO_AMP_C             0.0004
#define SNOW_MICRO_SCALE_A           12.0
#define SNOW_MICRO_SCALE_B           27.5
#define SNOW_MICRO_SCALE_C           62.0
#define SNOW_MICRO_BASE              0.55
#define SNOW_MICRO_REF_DEPTH         0.06
#define SNOW_WIND_FILL               0.0012

// --- Accumulation, settling, melting (spec §11) ---
#define SNOW_SETTLE_TAU          21600.0
#define SNOW_DISTURB_TAU           900.0
#define SNOW_MELT_DDF                4.63e-8
#define SNOW_MELT_ENABLED            0.0
#define SNOW_DRIFT_BIAS              0.45
#define SNOW_RAIN_MELT_BOOST         2.5
#define SNOW_SWE_MAX                 0.60

// --- Precipitation (spec §3.4, §17.2) ---
#define SNOW_MAX_SWE_RATE            1.39e-6
#define SNOW_MAX_FLAKE_RATE      16000.0

// --- Sky visibility (spec §12.1) ---
#define SNOW_SKY_AREA_SIZE          96.0
#define SNOW_SKY_MOVE_THRESHOLD      4.0

// --- Wind transport (spec §18.0, §18.1) ---
#define SNOW_WINDSHADOW_C            0.7
#define SNOW_EROSION_RATE            1.16e-6
#define SNOW_DRIFT_U10_LOOSE         5.0
#define SNOW_DRIFT_U10_PACKED       11.0

// --- Heat sources (spec §18.2) ---
#define SNOW_MAX_HEAT_SOURCES       16
#define SNOW_HEAT_MELT_RATE          0.0009
#define SNOW_HEAT_WET_RATE           0.25

// --- Crust (spec §18.3) ---
#define SNOW_T_WARM                  5.0
#define SNOW_T_COOL                 -5.0
#define SNOW_T_FREEZE              -20.0
#define SNOW_CRUST_GAIN              1.4e-4
#define SNOW_CRUST_WIND_GAIN         6.0e-5
#define SNOW_CRUST_MELT_TAU       1200.0
#define SNOW_CRUST_BURY            220.0
#define SNOW_CRUST_SOLID             0.55
#define SNOW_CRUST_BREAK_PEN         0.05
#define SNOW_CRUST_SINK_SCALE        0.04

// --- Sastrugi & Bedforms (spec §18.4) ---
#define SNOW_BEDFORM_DEPTH_FRAC      0.60
#define SNOW_BURY_REF_DEPTH          0.30
#define SNOW_FBM_AMP                 0.015
#define SNOW_FBM_SCALE               0.80
#define SNOW_FBM_GAIN                0.574
#define SNOW_SURFACE_AO              0.50
#define SNOW_RIPPLE_AMP              0.006
#define SNOW_RIPPLE_LENGTH           0.17
#define SNOW_SASTRUGI_TAU          900.0
#define SNOW_SASTRUGI_BURY         260.0
#define SNOW_SASTRUGI_HEIGHT         0.20
#define SNOW_SASTRUGI_LENGTH         0.90
#define SNOW_SASTRUGI_WIDTH          2.20
#define SNOW_SASTRUGI_WIND_TAU     120.0
#define SNOW_DRIFT_HEIGHT              0.15
#define SNOW_DRIFT_LENGTH              0.90
#define SNOW_DRIFT_WIDTH               1.60

// --- Suspension curtains (spec §18.7) ---
#define SNOW_SUSP_SCALE_H            1.1
#define SNOW_SUSP_ALPHA_BASE         0.16
#define SNOW_SUSP_MAX_HEIGHT         5.0

// --- Spray (spec §18.6) ---
#define SNOW_SPRAY_PARTICLES_PER_M3  40000.0

// --- Compute (spec §20) ---
#define SNOW_GROUP_SIZE              8

#endif
