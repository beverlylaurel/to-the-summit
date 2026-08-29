#ifndef MOUNTAIN_SURFACE_INPUT_INCLUDED
#define MOUNTAIN_SURFACE_INPUT_INCLUDED

// The surface detail macros are USED here (DECLARE_SURFACE_DETAIL), so their
// definition is included here too. In MountainSurface.hlsl the order would be
// reversed and the macro would be undefined.
#include "SurfaceDetail.hlsl"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

// The terrain rises by the snow depth (`SnowWorldCoverHeight`); the shadow and
// depth passes apply the same offset, so the definition lives here.
#include "../Snow/Shaders/SnowCommon.hlsl"

// Material constants in one place. URP's built-in shadow/depth passes include this
// file too; if the buffer is not byte-for-byte identical in every pass the SRP
// Batcher treats the material as incompatible and batching is turned off.
CBUFFER_START(UnityPerMaterial)
    /// Seed of the procedural surface. The rock banding, oxide, lichen, grain and
    /// fracture pattern are tied to world coordinates; with the seed unchanged the
    /// same coordinate gives the same pattern. Incremented when the mountain is rebuilt.
    float4 _PatternSeed;

    float4 _RockPrimary, _RockSecondary;
    float4 _LowlandTint, _AlpineTint;
    float4 _LichenColor, _OxideColor, _ScreeColor;

    float _GrainScale, _GrainStrength, _RockSmoothness;
    float _BandThickness, _BandWarp, _BandWarpScale, _BandContrast;
    float _LowlandCeiling, _AlpineFloor, _AltitudeTintStrength;
    float _LichenAmount, _LichenCeiling, _LichenMoistureBias, _LichenSunSensitivity;
    float _OxideAmount, _OxideScale;
    float _ScreeAmount, _ScreeSlopeLimit;
    float2 _ScreeRange;
    float _WetDarkening, _WetSmoothness, _BumpStrength, _BumpScale, _CavityStrength;

    float4 _SandTint;
    float _SandAmount, _SandTexScale, _SandNormalStrength;
    float _SandBandAbove, _SandBandBelow, _SandFade;
    float _SandPatchScale, _SandPatchThreshold;
    float2 _SandSlopeCos;

    float4 _TerrainOrigin;   // xyz corner position
    float4 _TerrainSize;     // xyz boyut

    float _SurfaceWetness;
    float4 _SurfaceWindDir;   // xyz direction, w sustained strength
    float4 _SurfaceSunDir;

    // The red light coming from the horizon at dawn and dusk. Color and strength are driven by TimeOfDay.
    float4 _SurfaceDawnColor;
    float4 _SurfaceDawnDir;
    float _SurfaceDawnStrength;
    float _AlpenglowFacing;

    // Fields URP's built-in passes expect. The surface is opaque and untextured so
    // they are unused, but without the declarations those passes do not compile.
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half   _Cutoff;
CBUFFER_END

// SEA SHORE WETNESS (sea spec §14).
//
// **OUTSIDE** THE CBUFFER. These are GLOBAL values published with
// `Shader.SetGlobalFloat`; inside `UnityPerMaterial` they would be expected per
// material and the global write would never reach them — the symptom would be
// "the wet band never shows up, and there is no error either".
//
// Published by `SeaWetnessDriver`. While the sea is off the level is pulled to a
// very low elevation and the band is 0 everywhere.
float _SeaWetLevelY;
float _SeaWetFadeM;
float _SeaWetBandM;
float _SeaWetDarkening;

// STILL-WATER LEVEL, published by `SeaManager`. The sand band hangs from this, not
// from `_SeaWetLevelY`: that one carries the run-up and rises and falls with every
// wave, and a beach does not move at that rate.
float _SeaLevelY;

// Sand maps of the shore. Outside the CBUFFER because textures always are; the
// scalars that go with them are inside `UnityPerMaterial`.
TEXTURE2D(_SandAlbedo);      SAMPLER(sampler_SandAlbedo);
TEXTURE2D(_SandNormal);
TEXTURE2D(_SandRough);
TEXTURE2D(_SandAO);

// Surface data extracted from the mountain (see SurfaceMapBaker). Noise cannot
// answer "where"; these three channels are read from the mountain's own shape.
//   R deposition — material flowing from above, gravel collects in gullies
//   G concavity  — local hollowness, moisture clings in the cracks
//   B exposure   — how much of the sky is visible
TEXTURE2D(_SurfaceMaps);
SAMPLER(sampler_SurfaceMaps);
float4 _SurfaceMapsSize;   // xy resolution, zw 1/resolution

/// UV of the surface maps. From the terrain corner and size; no separate transform.
float2 SurfaceMapUV(float3 worldPos)
{
    return (worldPos.xz - _TerrainOrigin.xz) / max(1.0, _TerrainSize.x);
}

/// Cheap bilinear read. The main pass uses a bicubic sampler (the surface color was
/// giving away the texel grid); DISPLACEMENT does not need it and sixteen reads are
/// expensive in the hull/domain stage — deposition is a metre-scale quantity anyway.
float4 SampleSurfaceMapsFast(float3 worldPos)
{
    return SAMPLE_TEXTURE2D_LOD(_SurfaceMaps, sampler_SurfaceMaps,
                                SurfaceMapUV(worldPos), 0);
}

// Ground normal. Vertex normals live on a four-metre grid and filling in between
// the triangles left diagonal seams — under a low sun the mountain broke into a
// quilt pattern. The texture is read bilinearly and has no diagonal; fine detail comes from the procedural bump.
TEXTURE2D(_GroundNormals);
SAMPLER(sampler_GroundNormals);

// Horizon map: the angle blocking the horizon for sixteen compass directions (0-1 = 0-90 degrees).
// The sun shadow is read from here; the shadow map is never read for the terrain.
TEXTURE2D_ARRAY(_HorizonMap);
SAMPLER(sampler_HorizonMap);

// Height fog. Moisture and dust collect low down; density thins exponentially with
// altitude. The computation itself lives in HeightFog.hlsl: it is a property of the
// air, not of any one surface.
#include "HeightFog.hlsl"

#endif
