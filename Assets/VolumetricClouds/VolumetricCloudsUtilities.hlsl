#ifndef URP_VOLUMETRIC_CLOUDS_UTILITIES_HLSL
#define URP_VOLUMETRIC_CLOUDS_UTILITIES_HLSL

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/VolumeRendering.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

half3 EvaluateVolumetricCloudsAmbientProbe(half3 normalWS)
{
    // Linear + constant polynomial terms
    half3 res = SHEvalLinearL0L1(normalWS, clouds_SHAr, clouds_SHAg, clouds_SHAb);

    // Quadratic polynomials
    res += SHEvalLinearL2(normalWS, clouds_SHBr, clouds_SHBg, clouds_SHBb, clouds_SHC);

    return res;
}

// From HDRP: VolumetricCloudsUtilities.hlsl

// The number of octaves for the multi-scattering
#define NUM_MULTI_SCATTERING_OCTAVES 2
#define PHASE_FUNCTION_STRUCTURE half2
// Global offset to the high frequency noise
#define CLOUD_DETAIL_MIP_OFFSET 0.0
// Density below wich we consider the density is zero (optimization reasons)
#define CLOUD_DENSITY_TRESHOLD 0.001
// Number of steps before we start the large steps
#define EMPTY_STEPS_BEFORE_LARGE_STEPS 8
// TWO-LOBE HENYEY-GREENSTEIN. g0 = 0.8 forward scattering (the silver edge), g1 = -0.5
// back scattering (so a cloud at the opposite angle does not look dead).
//
// In the port the two were SUMMED. Because each HG integrates to 1 over the sphere the sum
// came to 2, i.e. energy was not conserved. The brief requires a normalized combination;
// `lerp` takes a weighted average of the two lobes and the integral stays at 1.
//
// THE MIX WENT 0.5 -> 0.15, BY MEASUREMENT. The symptom: at dawn, clouds away from the sun
// did not darken enough. 0.5 was Frostbite's default in the brief and had never been
// verified in this scene; because the back lobe is THREE TIMES the forward lobe at 90° it
// was holding the far field up on its own. Measured with a stop contour: the deck was
// squeezed into 3 stops, 70% of it inside a single band.
//
//   mix       15°     90°     ratio
//   0.50      0.503   0.0282  17.8x
//   0.15      0.842   0.0180  46.8x
//
// The forward lobe's eccentricity was NOT TOUCHED: lowering g drops the peak a little but
// widens the lobe and raises the far field — at 0.60 the 90° value is 0.0337, brighter even
// than before the correction. The lever is not there.
//
// Because `lerp` is a weighted average, lowering the mix raised the forward lobe's share and
// the area around the sun brightened 1.7x; that side was handled with the bloom threshold in
// `LookSettings` (1.10 -> 2.00, the other presets by the same ratio).
#define FORWARD_ECCENTRICITY 0.8
#define BACKWARD_ECCENTRICITY -0.5
#define PHASE_LOBE_BLEND 0.15
// Gurultu dokularinin cozunurlugu; voxel dunya boyu buradan cikiyor.
#define SHAPE_NOISE_RESOLUTION 128.0
#define EROSION_NOISE_RESOLUTION 32.0
// Value that is used to normalize the noise textures
#define NOISE_TEXTURE_NORMALIZATION_FACTOR 100000.0
// Maximal distance until which the "skybox"
#define MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE 200000.0 //FLT_MAX
// Maximal size of a light step
#define LIGHT_STEP_MAXIMAL_SIZE 1000.0

// The planet center position
#define _PlanetCenterPosition _PlanetCenterRadius.xyz
#define ConvertToPS(x) (x - _PlanetCenterPosition)

// Structure that holds all the data required for the cloud ray marching
struct CloudRay
{
    // Origin of the ray in camera-relative space
    float3 originWS;
    // Direction of the ray in world space
    half3 direction;
    // Maximal ray length before hitting the far plane or an occluder
    float maxRayLength;
    // Integration Noise
    float integrationNoise;
};

// Structure that holds the result of our volumetric ray
struct VolumetricRayResult
{
    // Amount of lighting that reach the clouds
    // We keep track of sun light and ambient light separately for optimization
    // They are combine at the end of tracing
    half3 scattering;
    half ambient;
    // Transmittance through the clouds
    half transmittance;
    // The glow the flash accumulates inside the mass. Kept separate because its color belongs
    // neither to the sun nor to the ambient, but to the flash itself.
    half glow;
    // Mean distance of the clouds
    float meanDistance;
    // Flag that defines if the ray is valid or not
    bool invalidRay;
};

// Perceptual blending
half EvaluateFinalTransmittance(half3 sceneColor, half transmittance)
{
    // Due to the high intensity of the sun, we often need apply the transmittance in a tonemapped space
    // As we only produce one transmittance, we evaluate the approximation on the luminance of the color
    half luminance = Luminance(sceneColor * _PostExposure);

    if (luminance > 0.0)
    {
        // Apply the transmittance in tonemapped space
        half resultLuminance = luminance * rcp(1.0 + luminance) * transmittance;
        resultLuminance = resultLuminance * rcp(1.0 - resultLuminance);

        // By softening the transmittance attenuation curve for pixels adjacent to cloud boundaries when the luminance is super high,  
        // We can prevent sun flicker and improve perceptual blending. (https://www.desmos.com/calculator/vmly6erwdo)
        half finalTransmittance = max(resultLuminance * rcp(luminance), pow(transmittance, 6));

        // This approach only makes sense if the color is not black
        transmittance = lerp(transmittance, finalTransmittance, _ImprovedTransmittanceBlend);
    }
    return saturate(transmittance);
}

// These 2 functions were moved to the Core RP package by the commit below:
// "[HDRP] Optimizations and quality improvements to PBR sky"
// https://github.com/Unity-Technologies/Graphics/commit/9f7464a87cb8a09f23869dc178560bb8b072d4ca
#if UNITY_VERSION < 202330

// Use an infinite far plane
// https://chaosinmotion.com/2010/09/06/goodbye-far-clipping-plane/
// 'depth' is the linear depth (view-space Z position)
float EncodeInfiniteDepth(float depth, float near)
{
    return saturate(near / depth);
}

// 'z' is the depth encoded in the depth buffer (1 at near plane, 0 at far plane)
float DecodeInfiniteDepth(float z, float near)
{
    return near / max(z, FLT_EPS);
}

#endif

// Function that takes a world space position and converts it to a depth value
float ConvertCloudDepth(float3 position)
{
    float4 hClip = TransformWorldToHClip(position);
    return hClip.z / hClip.w;
}

// There is NO per-frame varying term. Temporal accumulation only converges if the same pixel
// reads the same sample across frames; the port's time term produced a new pattern every
// frame, convergence never happened and it showed up as flicker.
float GenerateRandomFloat(float2 screenUV)
{
    return GenerateHashedRandomFloat(uint3(screenUV * _ScreenSize.xy, 0));
}

// Returns the closest hit in X and the farthest hit in Y.
// Returns a negative number if there's no intersection.
// (result.y >= 0) indicates success.
// (result.x < 0) indicates that we are inside the sphere.
float2 IntersectSphere(float sphereRadius, float cosChi,
                       float radialDistance, float rcpRadialDistance)
{
    // r_o = float2(0, r)
    // r_d = float2(sinChi, cosChi)
    // p_s = r_o + t * r_d
    //
    // R^2 = dot(r_o + t * r_d, r_o + t * r_d)
    // R^2 = ((r_o + t * r_d).x)^2 + ((r_o + t * r_d).y)^2
    // R^2 = t^2 + 2 * dot(r_o, r_d) + dot(r_o, r_o)
    //
    // t^2 + 2 * dot(r_o, r_d) + dot(r_o, r_o) - R^2 = 0
    //
    // Solve: t^2 + (2 * b) * t + c = 0, where
    // b = r * cosChi,
    // c = r^2 - R^2.
    //
    // t = (-2 * b + sqrt((2 * b)^2 - 4 * c)) / 2
    // t = -b + sqrt(b^2 - c)
    // t = -b + sqrt((r * cosChi)^2 - (r^2 - R^2))
    // t = -b + r * sqrt((cosChi)^2 - 1 + (R/r)^2)
    // t = -b + r * sqrt(d)
    // t = r * (-cosChi + sqrt(d))
    //
    // Why do we do this? Because it is more numerically robust.

    float d = Sq(sphereRadius * rcpRadialDistance) - saturate(1 - cosChi * cosChi);

    // Return the value of 'd' for debugging purposes.
    return (d < 0) ? d : (radialDistance * float2(-cosChi - sqrt(d),
                                                  -cosChi + sqrt(d)));
}

// TODO: remove.
float2 IntersectSphere(float sphereRadius, float cosChi, float radialDistance)
{
    return IntersectSphere(sphereRadius, cosChi, radialDistance, rcp(radialDistance));
}

float ComputeCosineOfHorizonAngle(float r)
{
    float R = _EarthRadius;
    float sinHor = R * rcp(r);
    return -sqrt(saturate(1 - sinHor * sinHor));
}

// Function that interects a ray with a sphere (optimized for very large sphere), returns up to two positives distances.

// numSolutions: 0, 1 or 2 positive solves
// startWS: rayOriginWS, might be camera positionWS
// dir: normalized ray direction
// radius: planet radius
// result: the distance of hitPos, which means the value of solves
int RaySphereIntersection(float3 startWS, float3 dir, float radius, out float2 result)
{
    float3 startPS = startWS + float3(0, _EarthRadius, 0);
    float a = dot(dir, dir);
    float b = 2.0 * dot(dir, startPS);
    float c = dot(startPS, startPS) - (radius * radius);
    float d = (b * b) - 4.0 * a * c;
    result = 0.0;
    int numSolutions = 0;
    if (d >= 0.0)
    {
        // Compute the values required for the solution eval
        float sqrtD = sqrt(d);
        float q = -0.5 * (b + FastSign(b) * sqrtD);
        result = float2(c / q, q / a);
        // Remove the solutions we do not want
        numSolutions = 2;
        if (result.x < 0.0)
        {
            numSolutions--;
            result.x = result.y;
        }
        if (result.y < 0.0)
            numSolutions--;
    }
    // Return the number of solutions
    return numSolutions;
}

// Returns true if the ray exits the cloud volume (doesn't intersect earth)
// The ray is supposed to start inside the volume
bool ExitCloudVolume(float3 originPS, half3 dir, float higherBoundPS, out float tExit)
{
    // Given that we are inside the volume, we are guaranteed to exit at the outer bound
    float radialDistance = length(originPS);
    float cosChi = dot(originPS, dir) * rcp(radialDistance);
    tExit = IntersectSphere(higherBoundPS, cosChi, radialDistance, rcp(radialDistance)).y;

    // If the ray intersects the earth, then the sun is occluded by the earth
    return cosChi >= ComputeCosineOfHorizonAngle(radialDistance);
}

struct RayMarchRange
{
    // The start of the range
    float start;
    // The length of the range
    float end;
};

// Returns true if the ray intersects the cloud volume
// Outputs the entry and exit distance from the volume
bool IntersectCloudVolume(float3 originPS, half3 dir, float lowerBoundPS, float higherBoundPS, out float tEntry, out float tExit)
{
    bool intersect;
    float radialDistance = length(originPS);
    float rcpRadialDistance = rcp(radialDistance);
    float cosChi = dot(originPS, dir) * rcpRadialDistance;
    float2 tInner = IntersectSphere(lowerBoundPS, cosChi, radialDistance, rcpRadialDistance);
    float2 tOuter = IntersectSphere(higherBoundPS, cosChi, radialDistance, rcpRadialDistance);

    if (tInner.x < 0.0 && tInner.y >= 0.0) // Below the lower bound
    {
        // The ray starts at the intersection with the lower bound and ends at the intersection with the outer bound
        tEntry = tInner.y;
        tExit = tOuter.y;
        // We don't see the clouds if they are behind Earth
        intersect = cosChi >= ComputeCosineOfHorizonAngle(radialDistance);
    }
    else // Inside or above the cloud volume
    {
        // The ray starts at the intersection with the outer bound, or at 0 if we are inside
        // The ray ends at the lower bound if we hit it, at the outer bound otherwise
        tEntry = max(tOuter.x, 0.0f);
        tExit = tInner.x >= 0.0 ? tInner.x : tOuter.y;
        // We don't see the clouds if we don't hit the outer bound
        intersect = tOuter.y >= 0.0f;
    }

    return intersect;
}

bool GetCloudVolumeIntersection(float3 originWS, half3 dir, out RayMarchRange rayMarchRange)
{
#ifdef _LOCAL_VOLUMETRIC_CLOUDS
    return IntersectCloudVolume(ConvertToPS(originWS), dir, _LowestCloudAltitude, _HighestCloudAltitude, rayMarchRange.start, rayMarchRange.end);
#else
    {
        ZERO_INITIALIZE(RayMarchRange, rayMarchRange);

        // intersect with all three spheres
        float2 intersectionInter, intersectionOuter;
        int numInterInner = RaySphereIntersection(originWS, dir, _LowestCloudAltitude, intersectionInter);
        int numInterOuter = RaySphereIntersection(originWS, dir, _HighestCloudAltitude, intersectionOuter);

        // The ray starts at the first intersection with the lower bound and goes up to the first intersection with the outer bound
        rayMarchRange.start = intersectionInter.x;
        rayMarchRange.end = intersectionOuter.x;

        // Return if we have an intersection
        return true;
    }
#endif
}

struct CloudProperties
{
    // Normalized float that tells the "amount" of clouds that is at a given location
    half density;
    // Ambient occlusion for the ambient probe
    half ambientOcclusion;
    // Normalized value that tells us the height within the cloud volume (vertically)
    float height;
    // Extinction over the interval
    half sigmaT;
};

// Global attenuation of the density based on the camera distance
half DensityFadeValue(float distanceToCamera)
{
    return saturate((distanceToCamera - _FadeInStart) * rcp(_FadeInStart + _FadeInDistance));
}

// The largest size one sample covers in the world (m): the larger of the screen pixel's
// projection at that distance and the ray step. The noise cannot be finer than this.
float SampleFootprint(float distanceToCamera, float stepSize)
{
    return max(distanceToCamera * _PixelFootprintScale * _ScreenSize.w, stepSize);
}

// Gurultu dokusunun bir voxel'i `tekrar / cozunurluk` metre. Ornek ayak izi bundan
// buyukse mip'e cikilir; her mip voxel boyunu ikiye katliyor.
float BandLimitMip(float footprint, float repeatMeters, float resolution)
{
    float voxelSize = repeatMeters * rcp(resolution);
    return max(0.0, log2(footprint * rcp(voxelSize)));
}

// The band limit for the shape and erosion noise. In the port the erosion mip was pinned
// between 3-100 km: it did not hold as the screen resolution, field of view and noise scale
// changed, which is why distant clouds pixelated.
float ShapeMipOffset(float distanceToCamera, float stepSize)
{
    return BandLimitMip(SampleFootprint(distanceToCamera, stepSize),
                        NOISE_TEXTURE_NORMALIZATION_FACTOR * rcp(max(_ShapeScale, 1e-4)),
                        SHAPE_NOISE_RESOLUTION);
}

float ErosionMipOffset(float distanceToCamera, float stepSize)
{
    return BandLimitMip(SampleFootprint(distanceToCamera, stepSize),
                        NOISE_TEXTURE_NORMALIZATION_FACTOR * rcp(max(_ErosionScale, 1e-4)),
                        EROSION_NOISE_RESOLUTION);
}

// Function that returns the normalized height inside the cloud layer
float EvaluateNormalizedCloudHeight(float3 positionPS)
{
    return RangeRemap(_LowestCloudAltitude, _HighestCloudAltitude, length(positionPS));
}

// Animation of the cloud shape position
float3 AnimateShapeNoisePosition(float3 positionPS)
{
    // We reduce the top-view repetition of the pattern
    positionPS.y += (positionPS.x / 3.0 + positionPS.z / 7.0);
    // We add the contribution of the wind displacements
    return positionPS + float3(_WindVector.x, 0.0, _WindVector.y) * _MediumWindSpeed + float3(0.0, _VerticalShapeWindDisplacement, 0.0);
}

// Animation of the cloud erosion position
float3 AnimateErosionNoisePosition(float3 positionPS)
{
    return positionPS + float3(_WindVector.x, 0.0, _WindVector.y) * _SmallWindSpeed + float3(0.0, _VerticalErosionWindDisplacement, 0.0);
}

// Structure that holds all the data used to define the cloud density of a point in space
struct CloudCoverageData
{
    // From a top down view, in what proportions this pixel has clouds
    half coverage;
    // From a top down view, in what proportions this pixel has clouds
    half rainClouds;
    // Maximal cloud height
    half maxCloudHeight;
    // The weather map's density channel (`w_d`) — `DensityAlter` reads it `[H18 App B.3]`
    half mapDensity;
};

// Function that evaluates the coverage data for a given point in planet space
void GetCloudCoverageData(float3 positionPS, out CloudCoverageData data)
{
    // The weather map tiles over world XZ; `_CloudMapTiling.xy` carries the period.
    // The camera offset is applied here: the shadow path calls this function too and both
    // paths have to read the same world point.
    float2 cloudMapPositionWS = positionPS.xz;
#ifndef _LOCAL_VOLUMETRIC_CLOUDS
    cloudMapPositionWS += _WorldSpaceCameraPos.xz;
#endif
    float2 cloudMapUV = cloudMapPositionWS * _CloudMapTiling.xy + _CloudMapTiling.zw;
    half4 cloudMapData = SAMPLE_TEXTURE2D_LOD(_CloudMapTexture, s_linear_repeat_sampler, cloudMapUV, 0);

    // `[H18 p.11]`: the coverage slider crosses between TWO maps. Up to 0.5 only the sparse
    // layout (R); above it the dense map (G) comes in and closes the sky.
    data.coverage = max(cloudMapData.x, saturate(_CloudCoverage - 0.5) * cloudMapData.y * 2.0);
    // The rain cloud is not wired in v1; sigmaT stays constant (see `CLOUDS_REBUILD.md`).
    data.rainClouds = 0.0;
    data.maxCloudHeight = cloudMapData.z;
    data.mapDensity = cloudMapData.w;
}

// Density remapping function
half DensityRemap(half x, half a, half b, half c, half d)
{
    return (((x - a) * rcp(b - a)) * (d - c)) + c;
}

/// The SHAPE-ALTERING height function `[H18 App B.2]`. It rounds the base a little and the
/// top a lot. The anvil changes it as an EXPONENT, not as a multiplier; with
/// `_AnvilAmount = 0` the exponent reduces to 1 and the term drops out.
half HeightAlter(half percentHeight, half maxCloudHeight)
{
    half value = saturate(DensityRemap(percentHeight, 0.0, 0.07, 0.0, 1.0));
    half stopHeight = saturate(maxCloudHeight + 0.12);
    value *= saturate(DensityRemap(percentHeight, stopHeight * 0.2, stopHeight, 1.0, 0.0));
    value = pow(value, saturate(DensityRemap(percentHeight, 0.65, 0.95,
                                             1.0, 1.0 - _AnvilAmount * _CloudCoverage)));
    return value;
}

/// DETAIL ALTERER `[H18 App B.5]`. It does two things:
///
/// 1. Plain Worley at the base, inverted at the top — the transition is in the first 20%
///    (`SAT(p_h x 5)`). Low down the structure comes out wispy, high up rounded. In the port
///    it was a fixed `1 - detail`, the same at every height.
/// 2. The scraping amount DECREASES with global coverage (`0.35 x e^(-g_c x 0.75)`). In the
///    port it INCREASED with coverage (`0.75 x WM_c`) — the wrong direction. In overcast
///    weather fine structure is unnecessary and the paper measures that and reduces it.
///
/// The user's erosion slider is a separate multiplier on top: at 1.0 it matches the paper exactly.
half DetailModifier(half detail, half percentHeight)
{
    half modifier = lerp(detail, 1.0 - detail, saturate(percentHeight * 5.0));
    return modifier * 0.35 * exp(-_CloudCoverage * 0.75);
}

/// The DENSITY-ALTERING height function `[H18 App B.3]`. It makes the base wispy and the top
/// gradual; global density (`g_d`) is multiplied in by the caller. Once an anvil is added the
/// density model MUST change too — otherwise the top stays too dense `[H18 p.17]`.
half DensityAlter(half percentHeight, half mapDensity)
{
    half value = percentHeight;
    value *= saturate(DensityRemap(percentHeight, 0.0, 0.2, 0.0, 1.0));
    value *= mapDensity * 2.0;
    value *= lerp(1.0, saturate(DensityRemap(sqrt(percentHeight), 0.4, 0.95, 1.0, 0.2)),
                  _AnvilAmount);
    value *= saturate(DensityRemap(percentHeight, 0.9, 1.0, 1.0, 0.0));
    return value;
}

// Horizon zero dawn technique to darken the clouds
half PowderEffect(half cloudDensity, half cosAngle, half intensity)
{
    half powderEffect = 1.0 - exp(-cloudDensity * 4.0);
    powderEffect = saturate(powderEffect * 2.0);
    return lerp(1.0, lerp(1.0, powderEffect, smoothstep(0.5, -0.5, cosAngle)), intensity);
}

// Function that evaluates the cloud properties at a given absolute world space position
void EvaluateCloudProperties(float3 positionPS, float noiseMipOffset, float erosionMipOffset, bool cheapVersion, bool lightSampling,
                            out CloudProperties properties)
{
    // Initliaze all the values to 0 in case
    ZERO_INITIALIZE(CloudProperties, properties);

    // When using a cloud map, we cannot support the full planet due to UV issues

    // Remove global clouds below the horizon
#ifndef _LOCAL_VOLUMETRIC_CLOUDS
    if (positionPS.y < _EarthRadius)
        return;
#endif

    // By default the ambient occlusion is 1.0
    properties.ambientOcclusion = 1.0;

    // Evaluate the normalized height of the position within the cloud volume
    properties.height = EvaluateNormalizedCloudHeight(positionPS);

    // The coverage is read WITHOUT the camera offset applied: `GetCloudCoverageData` applies
    // the offset itself, and applied here as well it would be added twice.
    CloudCoverageData cloudCoverageData;
    GetCloudCoverageData(positionPS, cloudCoverageData);

    // If this region of space has no cloud coverage, exit right away
    if (cloudCoverageData.coverage.x <= CLOUD_DENSITY_TRESHOLD || cloudCoverageData.maxCloudHeight < properties.height)
        return;

    // When rendering in camera space, we still want horizontal scrolling
#ifndef _LOCAL_VOLUMETRIC_CLOUDS
    positionPS.xz += _WorldSpaceCameraPos.xz;
#endif

    // TILING BREAKER. The base noise repeats verbatim every few kilometres in the world: up
    // close one or two repeats are visible and the eye does not pick them out, from the summit
    // hundreds are visible at once and the lattice reads.
    //
    // THE BREAKER'S PERIOD MUST BE LONGER THAN THE VIEW RANGE. A warp with a small period does
    // not hide the repeat, it adds a lattice of its own. 48 km is the same as the weather
    // map's period; unaligned, two separate patterns would overlay each other.
    //
    // The amplitude is half the tile: any larger and it folds the sampling coordinate and the
    // noise overlaps itself. The gradient stays at 0.23, i.e. space does not fold.
    const float TILE_BREAK_PERIOD = 48000.0;
    const float TILE_BREAK_AMPLITUDE = 1400.0;
    float2 tileBreakPhase = positionPS.xz * (6.2831853 / TILE_BREAK_PERIOD);
    float2 tileBreak = float2(sin(tileBreakPhase.y) + 0.5 * sin(2.31 * tileBreakPhase.y + 1.7),
                              sin(tileBreakPhase.x) + 0.5 * sin(2.31 * tileBreakPhase.x + 0.4))
                     * (TILE_BREAK_AMPLITUDE / 1.5);
    float3 shapePositionPS = positionPS;
    shapePositionPS.xz += tileBreak;

    // Evaluate the generic sampling coordinates
    float3 baseNoiseSamplingCoordinates = float3(AnimateShapeNoisePosition(shapePositionPS).xzy / NOISE_TEXTURE_NORMALIZATION_FACTOR) * _ShapeScale - float3(_ShapeNoiseOffset.x, _ShapeNoiseOffset.y, _VerticalShapeNoiseOffset);

    // Evaluate the coordinates at which the noise will be sampled and apply wind displacement
    baseNoiseSamplingCoordinates += properties.height * float3(_WindDirection.x, _WindDirection.y, 0.0f) * _AltitudeDistortion;

    // Read the low frequency Perlin-Worley and Worley noises
    half lowFrequencyNoise = SAMPLE_TEXTURE3D_LOD(_Worley128RGBA, s_trilinear_repeat_sampler, baseNoiseSamplingCoordinates.xyz, noiseMipOffset).r;

    // Read from the LUT
    half3 densityErosionAO = SAMPLE_TEXTURE2D_LOD(_CloudCurveTexture, s_linear_repeat_sampler, half2(0.0, properties.height), 0).xyz;

    // Adjust the shape and erosion factor based on the LUT and the coverage
    half shapeFactor = lerp(0.1, 1.0, _ShapeFactor) * densityErosionAO.y;
    half erosionFactor = _ErosionFactor * densityErosionAO.y;
#if defined(_CLOUDS_MICRO_EROSION)
    half microDetailFactor = _MicroErosionFactor * densityErosionAO.y;
#endif

    // Combine with the low frequency noise, we want less shaping for large clouds
    lowFrequencyNoise = lerp(1.0, lowFrequencyNoise, shapeFactor);

    // `[H18 p.14-16]`: the shape noise is FIRST multiplied by the shape-height function and
    // enters the coverage remap that way.
    lowFrequencyNoise *= HeightAlter(properties.height, cloudCoverageData.maxCloudHeight);
    // `[H18 p.14]`: the remap limit is `1 - g_c x WM_c`. The coverage slider enters the chain
    // HERE; the port had no such link, which is why the lower half of the slider did nothing.
    half base_cloud = 1.0 - _CloudCoverage * cloudCoverageData.coverage.x;
    // `x coverage²` WAS REMOVED. It was the port's own term with no counterpart in `[H18]`:
    // coverage already enters through the remap limit (`1 - g_c x WM_c`) and squaring punished
    // it a second time. A region whose coverage fell to 0.6 dropped to 0.36, and combined with
    // the remap limit it opened a full hole — which is why gaps appeared even in an overcast sky.
    base_cloud = saturate(DensityRemap(lowFrequencyNoise, base_cloud, 1.0, 0.0, 1.0));

    // Weight the ambient occlusion's contribution
    properties.ambientOcclusion = densityErosionAO.z;

    // EXTINCTION COEFFICIENT: extinction per metre at unit density (m^-1). It triples in a
    // rain cloud — the port's 0.04 -> 0.12 was the same ratio, and it now derives from the setting `[N22 p.164]`.
    properties.sigmaT = _ExtinctionCoefficient * lerp(1.0, 3.0, cloudCoverageData.rainClouds);

    // The ambient occlusion value that is baked is less relevant if there is shaping or erosion, small hack to compensate that
    half ambientOcclusionBlend = saturate(1.0 - max(erosionFactor, shapeFactor) * 0.5);
    properties.ambientOcclusion = lerp(1.0, properties.ambientOcclusion, ambientOcclusionBlend);

    // Apply the erosion for nicer details
    if (!cheapVersion)
    {
        float3 erosionCoords = AnimateErosionNoisePosition(positionPS) / NOISE_TEXTURE_NORMALIZATION_FACTOR * _ErosionScale;
        half detail = SAMPLE_TEXTURE3D_LOD(_ErosionNoise, s_linear_repeat_sampler, erosionCoords, CLOUD_DETAIL_MIP_OFFSET + erosionMipOffset).x;
        half erosionNoise = DetailModifier(detail, properties.height);
        erosionNoise = lerp(0.0, erosionNoise, erosionFactor);
        properties.ambientOcclusion = saturate(properties.ambientOcclusion - sqrt(erosionNoise * _ErosionOcclusion));
        // SUBTRACTION, NOT DIVISION `[N22 p.34]`:
        //     saturate(cloud_noise_composite - (1.0 - dimensional_profile))
        //
        // `DensityRemap(x, n, 1, 0, 1)` = `(x - n) / (1 - n)`. The numerator was right, the
        // division was extra: it normalizes the edge band and pushes the density quickly to 1.
        // `NUBIS_NOTES.md` had already written this for the coverage chain — "this is the
        // mathematical source of the hard edges" — but the fix had only been applied to the
        // coverage, and the division was still there on the erosion side.
        //
        // The symptom: pixel-scale speckle in thin cloud. With a small base density the
        // division turns the result binary — where `erosionNoise > base_cloud` it is 0,
        // elsewhere solid, with no value in between. It was measured to be unrelated to
        // resolution: at `resolutionScale` 0.5 and 1.0, and with `upscaleMode` Bilinear and
        // Bilateral, the screen is identical.
        base_cloud = saturate(base_cloud - erosionNoise);

        #if defined(_CLOUDS_MICRO_EROSION)
        // Micro erosion has no counterpart in the paper, it is the port's own addition. It uses
        // the same height and coverage behaviour so the two detail layers do not work against each other.
        float3 fineCoords = AnimateErosionNoisePosition(positionPS) / (NOISE_TEXTURE_NORMALIZATION_FACTOR) * _MicroErosionScale;
        half fine = SAMPLE_TEXTURE3D_LOD(_ErosionNoise, s_linear_repeat_sampler, fineCoords, CLOUD_DETAIL_MIP_OFFSET + erosionMipOffset).x;
        half fineNoise = lerp(0.0, DetailModifier(fine, properties.height), microDetailFactor);
        // The micro layer subtracts too: the two detail layers have to use the same algebra.
        base_cloud = saturate(base_cloud - fineNoise);
        #endif
    }

    // Given that we are not sampling the erosion texture, we compensate by substracting an erosion value
    if (lightSampling)
    {
        base_cloud -= erosionFactor * 0.1;
        #if defined(_CLOUDS_MICRO_EROSION)
        base_cloud -= microDetailFactor * 0.15;
        #endif
    }

    // Make sure we do not send any negative values
    base_cloud = max(0, base_cloud);

    // `[H18 p.14-16]`: the density-height function is the LAST multiplier in the chain. In the
    // port the `.x` channel of the curve texture was inside the remap limit; `DensityAlter` took its place.
    properties.density = base_cloud * DensityAlter(properties.height, cloudCoverageData.mapDensity)
                       * _DensityMultiplier;
}

// Function that evaluates the transmittance to the sun at a given cloud position
/// THE FLOOR OF SUN TRANSMITTANCE `[H18 App B.6]`, `Attenuation`.
///
/// Pure Beer-Lambert goes to zero inside a cloud: `sigmaT` 0.04, a light step of 1000 m and a
/// typical density of 0.2 give `extinction ~ 16`, `exp(-16) = 1.1e-7`. Even the second octave
/// is `exp(-8.4)`. The cloud interior is left with ambient light alone and comes out pitch black.
///
/// A real cloud is not black because light scatters many times inside it. HZD handles that
/// with a floor: `exp(-b x a_c) x 0.7`, `b = 6`, `a_c = 0.2` `[H18 p.58]` -> 0.211. Looking
/// toward the sun the clamp relaxes and halves.
half SunTransmittanceFloor(half cosAngle)
{
    const half beer = 6.0;
    const half attenuationClamp = 0.2;
    half value = exp(-beer * attenuationClamp) * 0.7;
    return lerp(value, value * 0.5, saturate(cosAngle));
}

half3 EvaluateSunTransmittance(float3 positionPS, half3 sunDirection, half cosAngle, PHASE_FUNCTION_STRUCTURE phaseFunction)
{
    // Compute the Ray to the limits of the cloud volume in the direction of the light
    float totalLightDistance = 0.0;
    half3 transmittance = half3(0.0, 0.0, 0.0);

    // If we early out, this means we've hit the earth itself
    if (ExitCloudVolume(positionPS, sunDirection, _HighestCloudAltitude, totalLightDistance))
    {
        // Because of the very limited numebr of light steps and the potential humongous distance to cover, we decide to potnetially cover less and make it more useful
        totalLightDistance = clamp(totalLightDistance, 0, _NumLightSteps * LIGHT_STEP_MAXIMAL_SIZE);

        // Apply a small bias to compensate for the imprecision in the ray-sphere intersection at world scale.
        totalLightDistance += 5.0;

        // Compute the size of the current step
        float intervalSize = totalLightDistance * rcp((float)_NumLightSteps);
        float opticalDepth = 0;

        // Collect total density along light ray.
        for (int j = 0; j < _NumLightSteps; j++)
        {
            // Here we intentionally do not take the right step size for the first step
            // as it helps with darkening the clouds a bit more than they should at low light samples
            float dist = intervalSize * (0.25 + j);

            // Evaluate the current sample point
            float3 currentSamplePointPS = positionPS + sunDirection * dist;
            // Get the cloud properties at the sample point
            CloudProperties lightRayCloudProperties;
            EvaluateCloudProperties(currentSamplePointPS, 3.0 * j / _NumLightSteps, 0.0, true, true, lightRayCloudProperties);

            opticalDepth += lightRayCloudProperties.density * lightRayCloudProperties.sigmaT;
        }

        // Compute the luminance for each octave
        // https://magnuswrenninge.com/wp-content/uploads/2010/03/Wrenninge-OzTheGreatAndVolumetric.pdf
        half3 extinction = intervalSize * opticalDepth * _ScatteringTint.xyz;
        half floorValue = SunTransmittanceFloor(cosAngle);
        for (int o = 0; o < NUM_MULTI_SCATTERING_OCTAVES; ++o)
        {
            half msFactor = PositivePow(_MultiScattering, o);
            half3 beerTerm = max(floorValue, exp(-extinction * msFactor));
            transmittance += beerTerm * (phaseFunction[o] * msFactor);
        }
    }

    return transmittance;
}

float ChapmanUpperApprox(float z, float cosTheta)
{
    float c = cosTheta;
    float n = 0.761643 * ((1 + 2 * z) - (c * c * z));
    float d = c * z + sqrt(z * (1.47721 + 0.273828 * (c * c * z)));

    return 0.5 * c + (n * rcp(d));
}

float ChapmanHorizontal(float z)
{
    float r = rsqrt(z);
    float s = z * r; // sqrt(z)

    return 0.626657 * (r + 2 * s);
}

// Default atmosphere settings of HDRP physically based sky
#if defined(PHYSICALLY_BASED_SKY)
half _AirScaleHeight;
half _AerosolScaleHeight;
half _AirDensityFalloff;
half _AerosolDensityFalloff;
//float _AtmosphericRadius;
#define _PlanetaryRadius _EarthRadius // TODO: unify earth radius control
half3 _AirSeaLevelExtinction;
half _AerosolSeaLevelExtinction;
#else
#define _AirScaleHeight 8000.0
#define _AerosolScaleHeight 1200.0
#define _AirDensityFalloff 1.0 / _AirScaleHeight
#define _AerosolDensityFalloff 1.0 / _AerosolScaleHeight
#define _PlanetaryRadius _EarthRadius
#define _AirSeaLevelExtinction (half3(5.8, 13.5, 33.1) / 1000000.0)
#define _AerosolSeaLevelExtinction 0.00001
#endif

//#define _AlphaSaturation 1.0
//#define _AlphaMultiplier 1.0

float3 ComputeAtmosphericOpticalDepth(float r, float cosTheta, bool aboveHorizon)
{
    const float2 n = float2(_AirDensityFalloff, _AerosolDensityFalloff);
    const float2 H = float2(_AirScaleHeight, _AerosolScaleHeight);
    const float  R = _PlanetaryRadius;

    float2 z = n * r;
    float2 Z = n * R;

    float sinTheta = sqrt(saturate(1 - cosTheta * cosTheta));

    float2 ch;
    ch.x = ChapmanUpperApprox(z.x, abs(cosTheta)) * exp(Z.x - z.x); // Rescaling adds 'exp'
    ch.y = ChapmanUpperApprox(z.y, abs(cosTheta)) * exp(Z.y - z.y); // Rescaling adds 'exp'

    if (!aboveHorizon) // Below horizon, intersect sphere
    {
        float sinGamma = (r / R) * sinTheta;
        float cosGamma = sqrt(saturate(1 - sinGamma * sinGamma));

        float2 ch_2;
        ch_2.x = ChapmanUpperApprox(Z.x, cosGamma); // No need to rescale
        ch_2.y = ChapmanUpperApprox(Z.y, cosGamma); // No need to rescale

        ch = ch_2 - ch;
    }
    else if (cosTheta < 0)   // Above horizon, lower hemisphere
    {
        // z_0 = n * r_0 = (n * r) * sin(theta) = z * sin(theta).
        // Ch(z, theta) = 2 * exp(z - z_0) * Ch(z_0, Pi/2) - Ch(z, Pi - theta).
        float2 z_0 = z * sinTheta;
        float2 b = exp(Z - z_0); // Rescaling cancels out 'z' and adds 'Z'
        float2 a;
        a.x = 2 * ChapmanHorizontal(z_0.x);
        a.y = 2 * ChapmanHorizontal(z_0.y);
        float2 ch_2 = a * b;

        ch = ch_2 - ch;
    }

    float2 optDepth = ch * H;

    return optDepth.x * _AirSeaLevelExtinction.xyz + optDepth.y * _AerosolSeaLevelExtinction;
}

// This function evaluates the sun color attenuation from the physically based sky
half3 EvaluateSunColorAttenuation(float3 positionPS, half3 sunDirection, bool estimatePenumbra = false)
{
    float r = length(positionPS);
    float cosTheta = dot(positionPS, sunDirection) * rcp(r); // Normalize

    // Point can be below horizon due to precision issues
    r = max(r, _PlanetaryRadius);
    float cosHoriz = ComputeCosineOfHorizonAngle(r);

    if (cosTheta >= cosHoriz) // Above horizon
    {
        float3 oDepth = ComputeAtmosphericOpticalDepth(r, cosTheta, true);
        half3 opacity = 1 - TransmittanceFromOpticalDepth(oDepth);
        half penumbra = saturate((cosTheta - cosHoriz) / 0.0019); // very scientific value
        half3 attenuation = 1 - opacity;// (Desaturate(opacity, _AlphaSaturation) * _AlphaMultiplier);
        return estimatePenumbra ? attenuation * penumbra : attenuation;
    }
    else
    {
        return 0;
    }
}

// Function that evaluates the sun color along the ray
half3 EvaluateSunColor(float3 entryEvaluationPointPS, float3 exitEvaluationPointPS, half3 sunDirection, half3 sunColor, float relativeRayDistance)
{
    // evaluate the attenuation at both points (entrance and exit of the cloud layer)
    half3 sunColor0 = sunColor * EvaluateSunColorAttenuation(entryEvaluationPointPS, sunDirection, true);
    half3 sunColor1 = sunColor * EvaluateSunColorAttenuation(exitEvaluationPointPS, sunDirection, false);

    return lerp(sunColor0, sunColor1, relativeRayDistance);
}

// Evaluates the inscattering from this position
void EvaluateCloud(CloudProperties cloudProperties, half3 rayDirection,
                float3 currentPositionPS, float stepSize, float relativeRayDistance,
                inout VolumetricRayResult volumetricRay)
{
    // Apply the extinction
    const half extinction = cloudProperties.density * cloudProperties.sigmaT;
    const half transmittance = exp(-extinction * stepSize);

    Light sun = GetMainLight();
    half cosAngle = dot(rayDirection, sun.direction);

    // Evaluate the phase function for each of the octaves
    half2 phaseFunction = half2(0.0, 0.0);
    half forwardP = HenyeyGreensteinPhaseFunction(FORWARD_ECCENTRICITY * PositivePow(_MultiScattering, 0), cosAngle);
    half backwardsP = HenyeyGreensteinPhaseFunction(BACKWARD_ECCENTRICITY * PositivePow(_MultiScattering, 0), cosAngle);
    phaseFunction[0] = lerp(forwardP, backwardsP, PHASE_LOBE_BLEND);

#if NUM_MULTI_SCATTERING_OCTAVES >= 2
    forwardP = HenyeyGreensteinPhaseFunction(FORWARD_ECCENTRICITY * PositivePow(_MultiScattering, 1), cosAngle);
    backwardsP = HenyeyGreensteinPhaseFunction(BACKWARD_ECCENTRICITY * PositivePow(_MultiScattering, 1), cosAngle);
    phaseFunction[1] = lerp(forwardP, backwardsP, PHASE_LOBE_BLEND);
#endif

#if NUM_MULTI_SCATTERING_OCTAVES >= 3
    forwardP = HenyeyGreensteinPhaseFunction(FORWARD_ECCENTRICITY * PositivePow(_MultiScattering, 2), cosAngle);
    backwardsP = HenyeyGreensteinPhaseFunction(BACKWARD_ECCENTRICITY * PositivePow(_MultiScattering, 2), cosAngle);
    phaseFunction[2] = lerp(forwardP, backwardsP, PHASE_LOBE_BLEND);
#endif

    // Compute the powder effect
    half powderEffect = PowderEffect(cloudProperties.density, cosAngle, _PowderEffectIntensity);

    // THE FLASH IS INSIDE THE RAY MARCH. Not in the compositing pass: there the glow rode on
    // top of the mass rather than coming from INSIDE it. There is no flicker concern because
    // the source positions are written once per flash and do not change frame to frame `[N22 p.180]`.
    //
    // FALLOFF IS 1/r², NOT AN INTUITIVE EXPONENT. It used to be `pow(1 - d/R, 12)`: the twelve
    // has no physical counterpart, it was an invented curve. `[Dobashi 2001]` Equation 9 gives
    // the light reaching a metaball from a source as `I·exp(-tau)/r²`.
    //
    // The form used is `R²/(r² + R²)`: it goes to 1/r² at distance, does not diverge at r=0
    // (the point source idealisation is invalid there), and at R it drops exactly by HALF —
    // which is the already documented meaning of the `glowRadius` setting ("at this distance
    // from the strike point the glow halves").
    //
    // ALL OF THE SOURCES, not a single point. The fog looked at eight sources and the cloud at
    // one point; the two systems saw the same flash in different places.
    //
    // THE HEIGHT GRADIENT WAS REMOVED. It was Nubis's artistic choice (let the cloud above glow
    // more); the vertical structure now comes FROM THE GEOMETRY — the sources are lined up from
    // the cloud down to the ground.
    //
    // THE OPTICAL DEPTH tau IS APPROXIMATE. Equation 10 wants the density integral between the
    // source and the point; the paper solves it with a cube-screen method and that method is
    // specific to metaballs and cannot be carried here (spec §8.1). A local density proxy is
    // used instead: a denser place transmits less. The paper also accepted the cost of the tau
    // computation and gave it up in the LOD selection (Condition 12 instead of 11).
    float3 currentPositionWS = currentPositionPS + _PlanetCenterPosition;

    half radius = max(_LightningPosition.w, 1.0);
    half radiusSq = radius * radius;

    int strikeCount = (int)min(_LightningSourceCount, (float)LIGHTNING_MAX_SOURCES);
    half reach = 0.0;

    [loop]
    for (int sIdx = 0; sIdx < strikeCount; sIdx++)
    {
        float3 delta = currentPositionWS - _LightningSources[sIdx].xyz;
        half rSq = dot(delta, delta);
        reach += radiusSq / (rSq + radiusSq);
    }

    reach = strikeCount > 0 ? reach / strikeCount : 0.0;

    half pseudoAttenuation = 1.0 - saturate(cloudProperties.density * 5.0);
    half glowEnergy = reach * pseudoAttenuation;

    // Evaluate the sun visibility
    half3 sunTransmittance = EvaluateSunTransmittance(currentPositionPS, sun.direction, cosAngle, phaseFunction);

    // Compute luminance separately to factor out color multiplication at the end of the loop
    // Use 1 as placeholder to compute the 'transfer function'
    half3 sunLuminance = 1.0 * sunTransmittance * powderEffect;
    half ambientLuminance = 1.0 * cloudProperties.ambientOcclusion;

    // "Energy-conserving analytical integration"
    // See slide 28 at http://www.frostbite.com/2015/08/physically-based-unified-volumetric-rendering-in-frostbite/
    // No division by clamped extinction because albedo == 1 => sigma_s == sigma_e so it simplifies
    // Note: this is not true anymore when _ScatteringTint is modified, but it still looks correct
    volumetricRay.scattering += sunLuminance     * (volumetricRay.transmittance - volumetricRay.transmittance * transmittance);
    volumetricRay.ambient    += ambientLuminance * (volumetricRay.transmittance - volumetricRay.transmittance * transmittance);
    volumetricRay.glow       += glowEnergy       * (volumetricRay.transmittance - volumetricRay.transmittance * transmittance);
    volumetricRay.transmittance *= transmittance;
}

#endif