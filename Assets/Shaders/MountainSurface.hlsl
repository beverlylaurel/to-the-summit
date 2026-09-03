#ifndef MOUNTAIN_SURFACE_INCLUDED
#define MOUNTAIN_SURFACE_INCLUDED

// The description of the mountain surface. It only answers "what is here"; the light
// computation belongs to UniversalFragmentPBR. The shadow, depth and normal passes also
// come from URP's own files — so extra lights like a headlamp work for free.

#include "MountainSurfaceInput.hlsl"
#include "RainRings.hlsl"

// World coordinates reach thousands of metres. A sin-based hash exhausts float precision
// at that scale and turns into per-pixel noise; the cell index is first folded into a small
// period, so the repeat stays kilometres away.
float MountainHash(float3 p)
{
    // The seed is applied here, not at the call sites.
    // `_PatternSeed`. Because it is added to the scaled coordinate every layer shifts by a
    // different world distance; the layers refresh independently of each other.
    p = fmod(abs(p + _PatternSeed.xyz), 512.0);
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float MountainNoise(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = MountainHash(i),                     n100 = MountainHash(i + float3(1, 0, 0));
    float n010 = MountainHash(i + float3(0, 1, 0)),   n110 = MountainHash(i + float3(1, 1, 0));
    float n001 = MountainHash(i + float3(0, 0, 1)),   n101 = MountainHash(i + float3(1, 0, 1));
    float n011 = MountainHash(i + float3(0, 1, 1)),   n111 = MountainHash(i + float3(1, 1, 1));

    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

/// Value + ANALYTIC GRADIENT in a single sample. Extracting the slope with a finite
/// difference means sampling the same noise 3 times (8 hashes x 3); the derivative in
/// closed form already falls out of the 8 corners we have.
///   n = trilinear(corners, u),  u = f²(3-2f),  du/df = 6f(1-f)
float MountainNoiseD(float3 p, out float3 grad)
{
    float3 i = floor(p), f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float3 du = 6.0 * f * (1.0 - f);

    float n000 = MountainHash(i),                     n100 = MountainHash(i + float3(1, 0, 0));
    float n010 = MountainHash(i + float3(0, 1, 0)),   n110 = MountainHash(i + float3(1, 1, 0));
    float n001 = MountainHash(i + float3(0, 0, 1)),   n101 = MountainHash(i + float3(1, 0, 1));
    float n011 = MountainHash(i + float3(0, 1, 1)),   n111 = MountainHash(i + float3(1, 1, 1));

    float k0 = n000;
    float k1 = n100 - n000;
    float k2 = n010 - n000;
    float k3 = n001 - n000;
    float k4 = n000 - n100 - n010 + n110;
    float k5 = n000 - n010 - n001 + n011;
    float k6 = n000 - n100 - n001 + n101;
    float k7 = -n000 + n100 + n010 - n110 + n001 - n101 - n011 + n111;

    grad = du * float3(k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
                       k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
                       k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y);

    return k0 + k1 * u.x + k2 * u.y + k3 * u.z
         + k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x
         + k7 * u.x * u.y * u.z;
}

/// fbm + gradient. Because the coordinate is scaled by 2.03 per octave the gradient grows
/// by the same factor (chain rule).
float MountainFbmD(float3 p, int octaves, out float3 grad)
{
    float sum = 0.0, amp = 0.5, freq = 1.0;
    grad = 0.0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        if (i >= octaves) break;
        float3 g;
        sum += MountainNoiseD(p, g) * amp;
        grad += g * (amp * freq);
        p *= 2.03;
        freq *= 2.03;
        amp *= 0.5;
    }
    return sum;
}

// The noise is sampled in 3D from the world position: no projection at all, and therefore
// no stretching on a steep face. Triplanar is only needed for 2D texture mapping.
float MountainFbm(float3 p, int octaves)
{
    float sum = 0.0, amp = 0.5;

    // [unroll]: the octave count is a CONSTANT at every call site (2 or 3), but because the
    // loop was written dynamically the compiler could not unroll it — a branch, a loop
    // counter and a register were kept per octave. Unrolled, `i >= octaves` resolves at
    // compile time and the body drops to straight-line code. The result is bit identical.
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        if (i >= octaves) break;
        sum += MountainNoise(p) * amp;
        p *= 2.03;
        amp *= 0.5;
    }
    return sum;
}

/// Geological banding: horizontal layers bent by tectonics. Without the bending they come
/// out as straight lines and the mountain looks like a slice of cake.
float MountainBand(float3 worldPos)
{
    float warp = (MountainFbm(worldPos * _BandWarpScale, 2) - 0.5) * _BandWarp;
    float band = (worldPos.y + warp) / max(_BandThickness, 1.0);

    // Triangle wave: a smooth back and forth between the layers
    return abs(frac(band) * 2.0 - 1.0);
}

// THE SNOW STATE IS READ FROM HERE. The mountain's snow and the snow mesh's snow come from
// the SAME chain (near region -> far cascade -> snow line); there is no separate "terrain
// snow" number, so they cannot contradict at the boundary.
//
// With the snow system absent from the scene these globals stay zero and the layer turns
// itself off — no extra switch is needed.
#include "../Snow/Shaders/SnowCommon.hlsl"

// The detail normals come from spec §14.2's own table. On the mountain side only the MACRO
// layer (8 m tile, wind waves) is enabled: the quality keywords are not defined here, so the
// Meso/Micro blocks are not compiled.
//
// That is not a limitation but the right place: Meso is 0.6 m and Micro 0.05 m near-field
// detail, and that area is already drawn with the full stack by the snow mesh (clipmap,
// 128 m). The mountain layer starts beyond 128 m, where both are sub-pixel.
//
// Macro's tile is 8 m — the terrain heightmap's texel is 7.32 m. This is exactly the layer
// that breaks the step scale flat white snow puts on display.
#include "../Snow/Shaders/SnowDetailNormals.hlsl"
#include "../Snow/Shaders/SnowCover.hlsl"
#include "../Snow/Shaders/SnowSparkle.hlsl"
#include "../Snow/Shaders/SnowLighting.hlsl"
#include "../Snow/Shaders/SnowRelief.hlsl"


struct MountainSurface
{
    half3 albedo;
    half3 emission;
    half  smoothness;
    half  occlusion;

    /// Local height of the snow surface (m). `SnowSurfaceSlope` returns it together with the
    /// gradient; ambient occlusion reads it. A separate call would have meant six more
    /// noise samples per pixel.
    half  snowSurfaceHeight;
    float3 normalWS;

    /// The snow's share at this point. The sparkle is weighted by it: with the snow mesh
    /// sparkling and the terrain not, the boundary showed up as a line.
    half  snowMask;

    /// The snow's LOCAL state, carried into the lighting block. Compacted snow inside a
    /// trail; the light has to see that density too, otherwise the surface looks crushed
    /// but reflects light like virgin snow.
    half snowRhoN;
    half snowWet;
    half snowDisturb;

    /// The trail's depth at that pixel (m). The lighting block computes the cavity's own
    /// shadow from it.
    half snowDentDepth;

    /// The surface texture blend, carried in its READ-ONCE form.
    ///
    /// The lighting block wants the same blend; resampled there, the twelve texture fetches
    /// for the same pixel would double. The snow mesh and the terrain MUST see the SAME
    /// texture: the mesh only draws the local deviation, the terrain draws the flat area,
    /// and if the two surfaces saw different textures the boundary would separate.
    SnowSurfaceBlend snowBlend;
};

/// The terrain's sun shadow, from the baked horizon map.
///
/// A point is in shadow if and only if the sun is below the horizon angle in that direction.
/// The horizon angle field is smooth and fixed with the mountain; the two directions
/// neighbouring the sun's azimuth are read and blended, and the elevation angle is compared
/// with the horizon. Two texture reads, zero randomness.
///
/// Ray marching was tried and reverted twice: a single ray plus a threshold produces either
/// a razor edge or dots — there is no temporal accumulation to resolve the noise. A shadow
/// map was tried before that: the shadows of triangle silhouettes were sawtoothed on the
/// ridges. Both had the same root, deriving the shadow from a sampled surface; the horizon
/// map derives it from a smooth angle field.
///
/// The penumbra comes from the sun's angular proximity to the horizon: a twilight band just
/// above the horizon, full shadow below. That is how the edge of a mountain shadow really softens.
float TerrainSunShadow(float3 worldPos, float3 sunDir)
{
    // IT DOES NOT RELEASE IMMEDIATELY BELOW THE HORIZON — FOR THE ALPENGLOW.
    //
    // It used to be cut with `sunDir.y < 0.0`: the MOMENT the sun touched the horizon the
    // terrain closed its own shadow completely, and through the alpenglow after sunset
    // valley floors and the backs of ridges stayed as bright as the ridges themselves —
    // the sense of depth vanished in a single frame.
    //
    // The light's own direction already extinguishes it; the gate here only exists to stop
    // the horizon map from being read at a meaningless angle. So the limit was pulled a
    // little BELOW the horizon and the shadow is released softly in between.
    if (sunDir.y < -0.035) return 1.0;

    // Between -0.035 and 0 the shadow fades out: a hard on/off would make a visible jump
    // at sunset.
    float horizonFade = saturate(sunDir.y / 0.035 + 1.0);

    float2 uv = (worldPos.xz - _TerrainOrigin.xz) / _TerrainSize.xz;

    // NO SHADOW OUTSIDE THE BAKED AREA. The horizon map is only baked for the box the
    // terrain covers. Outside it the uv leaves [0,1] and whatever the texture's wrap mode
    // returned was read; reading a high horizon made `smoothstep` return ZERO, i.e. the
    // direct sun was cut entirely.
    //
    // The symptom: on the plain, in daylight, the ground was pitch black. The measurement
    // chain eliminated them in order — the fog (the volume probe was red on the ground:
    // `color x 1 + 0`), the froxel volume, the cloud shadow cookie, the shadow map (the
    // `_TerrainShadowReceive` switch changed nothing because it does not cut THIS path).
    // The surface probe stayed black with `color x 8` too: the value entering the surface
    // was already zero.
    //
    // The day/night gate is here as well: the function returns 1.0 while `sunDir.y < 0`, so
    // the shadow is only computed while the sun is above the horizon. That is why the
    // symptom started at 08:02 and ended at 19:11 — the user measured the boundary to the minute.
    //
    // Outside, the right answer is "no obstacle": the baked terrain ends there and there
    // is no mass to cut the sun. The horizon counts as zero and the surface is fully lit.
    if (any(uv != saturate(uv))) return 1.0;

    // Which two baked directions is the azimuth between?
    const float TwoPi = 6.2831853;
    float sector = atan2(sunDir.z, sunDir.x) / TwoPi * 16.0;
    sector += sector < 0.0 ? 16.0 : 0.0;

    float lower = floor(sector);
    float blend = sector - lower;

    float a0 = SAMPLE_TEXTURE2D_ARRAY_LOD(_HorizonMap, sampler_HorizonMap,
                   uv, fmod(lower, 16.0), 0).r;
    float a1 = SAMPLE_TEXTURE2D_ARRAY_LOD(_HorizonMap, sampler_HorizonMap,
                   uv, fmod(lower + 1.0, 16.0), 0).r;

    float horizon = lerp(a0, a1, blend) * 1.5707963;
    float elevation = FastASin(saturate(sunDir.y));

    // The angular width of the penumbra (radians): little below the horizon, wide above —
    // the shadow's interior stays solid while its edge opens like twilight
    return lerp(1.0, smoothstep(horizon - 0.02, horizon + 0.10, elevation), horizonFade);
}

/// Alpenglow: not a separate light but the reddened sun ITSELF — while the valley has
/// entered Earth's shadow, high surfaces still receive direct light. That is the pink-red
/// glow of a summit.
///
/// While the sun is on the horizon the share is gated by the terrain shadow: a slope in
/// shadow does not glow. It used to be shadowless emission and at dawn it burned the scene
/// with a flat, blinding hit — light has to behave like light. What remains after sunset is
/// the afterglow (light scattered in the atmosphere): shadowless but faint.
half3 Alpenglow(float3 worldPos, float3 normalWS, float altitude, half3 albedo,
                float exposure)
{
    if (_SurfaceDawnStrength <= 0.001) return 0.0;

    // Direct phase or afterglow: is the sun above the horizon?
    float directPhase = smoothstep(-0.02, 0.06, _SurfaceDawnDir.y);

    // EARTH'S SHADOW CLIMBS. What makes an alpenglow recognisable is the shadow line
    // walking up the slope: the valley goes out first and the light withdraws to the summit.
    // A fixed altitude band could not do that — the whole mountain turned pink together and
    // went out together, and that was what looked artificial.
    //
    // The shadow's upper limit follows from the sun's angle below the horizon:
    // h = R(1/cosθ - 1), which for small angles is ~ R·θ²/2. `_SurfaceDawnDir.y` is the sine
    // of the sun's elevation, so θ ~ -y. Our summit is ~2100 m: the show runs between 0° and -1.5°.
    //   0.5° -> 240 m,  1.0° -> 975 m,  1.5° -> 2190 m
    float below = max(0.0, -_SurfaceDawnDir.y);
    float shadowHeight = 6371000.0 * 0.5 * below * below;

    // The boundary is not sharp: the shadow edge spreads over a few hundred metres in the atmosphere.
    float lit = lerp(smoothstep(shadowHeight - 300.0, shadowHeight + 300.0, altitude),
                     1.0, directPhase);
    if (lit <= 0.0) return 0.0;

    // DIRECTION only in the direct phase. After the sun has set what illuminates is not a
    // point source but the WHOLE SKY painted red. Directional falloff in that phase becomes
    // a mask with no physical counterpart and confined the glow to a single slope.
    float facing = saturate(dot(normalWS, _SurfaceDawnDir.xyz) * 0.5 + 0.5);
    facing = lerp(1.0, facing, _AlpenglowFacing * directPhase);

    // The surface color affects the glow but does not fully determine it: snow is painted
    // red, dark rock catches less. Multiplying by the albedo directly pushes the glow into
    // invisibility on a dark surface like basalt.
    half3 receptivity = lerp(0.45, albedo, 0.65);

    // SHADOWING depends on the phase too. In the direct phase the sun shadow is the right
    // measure. After sunset the source is the sky, so a sun-directional shadow is
    // meaningless — the right measure is how much of the sky that point sees (exposure): a hollow gets little, a ridge a lot.
    float shade = TerrainSunShadow(worldPos, _SurfaceDawnDir.xyz);
    float gate = lerp(lerp(0.25, 1.0, exposure), shade, directPhase);

    return receptivity * _SurfaceDawnColor.rgb
         * (_SurfaceDawnStrength * lit * facing * gate);
}

/// Reads the surface maps with a cubic B-spline — the weighted combination of four bilinear
/// reads.
///
/// Bilinear is not enough: the snow mask puts a narrow threshold on the product of these
/// channels and the iso-lines of bilinear fields break into an X shape at texel corners —
/// the mask edge was wearing the crystal pattern of the 17 metre grid. Confirmed by
/// diagnostic: the pattern is in none of the component channels, only in the thresholded
/// result. A B-spline field is C1; its iso-lines have no breaks and the crystal is structurally impossible.
float4 SampleSurfaceMaps(float2 uv)
{
    float2 t = uv * _SurfaceMapsSize.xy - 0.5;
    float2 cell = floor(t);
    float2 f = t - cell;

    float2 f2 = f * f;
    float2 f3 = f2 * f;

    float2 w0 = (1.0 - 3.0 * f + 3.0 * f2 - f3) / 6.0;
    float2 w1 = (4.0 - 6.0 * f2 + 3.0 * f3) / 6.0;
    float2 w2 = (1.0 + 3.0 * f + 3.0 * f2 - 3.0 * f3) / 6.0;
    float2 w3 = f3 / 6.0;

    float2 g0 = w0 + w1;
    float2 g1 = w2 + w3;

    float2 p0 = (cell - 0.5 + w1 / g0) * _SurfaceMapsSize.zw;
    float2 p1 = (cell + 1.5 + w3 / g1) * _SurfaceMapsSize.zw;

    return g0.y * (g0.x * SAMPLE_TEXTURE2D(_SurfaceMaps, sampler_SurfaceMaps, float2(p0.x, p0.y))
                 + g1.x * SAMPLE_TEXTURE2D(_SurfaceMaps, sampler_SurfaceMaps, float2(p1.x, p0.y)))
         + g1.y * (g0.x * SAMPLE_TEXTURE2D(_SurfaceMaps, sampler_SurfaceMaps, float2(p0.x, p1.y))
                 + g1.x * SAMPLE_TEXTURE2D(_SurfaceMaps, sampler_SurfaceMaps, float2(p1.x, p1.y)));
}

MountainSurface BuildMountainSurface(float3 worldPos)
{
    float2 uv = (worldPos.xz - _TerrainOrigin.xz) / _TerrainSize.xz;
    float4 maps = SampleSurfaceMaps(uv);

    // The ground normal comes from the baked texture, not from the vertex normal
    float2 packed = SAMPLE_TEXTURE2D(_GroundNormals, sampler_GroundNormals, uv).rg * 2.0 - 1.0;
    float3 normalWS = float3(packed.x,
                             sqrt(saturate(1.0 - dot(packed, packed))), packed.y);

    float deposition = maps.r;
    float concavity  = maps.g;
    float exposure   = maps.b;

    // The slope comes from the baked map rather than the vertex normal. Vertex normals live
    // on a 4 metre grid and filling in between them produces a field whose iso-lines curve
    // along the shared diagonals. It is invisible in light — the threshold makes it visible:
    // with a slope in the band of the slope limit, the snow and gravel masks opened and
    // closed along that lattice and a diamond pattern appeared on the surface. The map's
    // texel is four times wider than the grid and cannot carry the pattern.
    float slope = maps.a;

    float altitude = worldPos.y - _TerrainOrigin.y;
    float grain = MountainFbm(worldPos * _GrainScale, 3);

    // --- Rock: two tones, blended with geological banding ---
    float band = MountainBand(worldPos);
    float rockMix = saturate(band * _BandContrast + (grain - 0.5) * _GrainStrength);
    float3 albedo = lerp(_RockPrimary.rgb, _RockSecondary.rgb, rockMix);

    // --- Altitude tint: soil below, glacial scouring above ---
    float lowland = 1.0 - smoothstep(0.0, _LowlandCeiling, altitude);
    float alpine = smoothstep(_AlpineFloor, _AlpineFloor + 1200.0, altitude);
    albedo = lerp(albedo, _LowlandTint.rgb, lowland * _AltitudeTintStrength);
    albedo = lerp(albedo, _AlpineTint.rgb, alpine * _AltitudeTintStrength);

    // --- Oxide: iron veins follow the layers, they do not wander freely ---
    float vein = MountainFbm(worldPos * _OxideScale, 2);
    float oxide = smoothstep(0.55, 0.85, vein) * (1.0 - band) * _OxideAmount;
    albedo = lerp(albedo, _OxideColor.rgb, saturate(oxide));

    // --- Lichen: moisture clings in hollows, the sun dries it, altitude limits it ---
    float moisture = smoothstep(_LichenMoistureBias, 1.0, concavity);
    float shelter = 1.0 - exposure;
    // The noon sun is used: tied to the instantaneous sun the lichen would blink through the day
    float sunFacing = saturate(dot(normalWS, _SurfaceSunDir.xyz) * 0.5 + 0.5);
    float dried = lerp(1.0, 1.0 - sunFacing, _LichenSunSensitivity);
    float alive = 1.0 - smoothstep(_LichenCeiling - 600.0, _LichenCeiling, altitude);
    float patchy = smoothstep(0.35, 0.75, MountainFbm(worldPos * 0.02, 2));

    float lichen = saturate((moisture * 0.6 + shelter * 0.4) * dried * alive * patchy) * _LichenAmount;
    albedo = lerp(albedo, _LichenColor.rgb, lichen);

    // --- Gravel: material flowing from above collects in gullies, it does not cling to steep faces ---
    float screeFit = smoothstep(cos(radians(_ScreeSlopeLimit)) - 0.12,
                                cos(radians(_ScreeSlopeLimit)) + 0.08, slope);
    float scree = smoothstep(_ScreeRange.x, _ScreeRange.y, deposition) * screeFit * _ScreeAmount;
    albedo = lerp(albedo, _ScreeColor.rgb * (0.8 + grain * 0.4), scree);

    // --- Sand: part of the shore, not all of it ---
    //
    // Three conditions have to hold AT ONCE, and that is what leaves part of the coast
    // as rock: the elevation inside the band, a slope gentle enough for sand to hold,
    // and the patch field open at that point. A steep shore stays a rock headland; a
    // gentle one with the patch open becomes a bay of sand.
    //
    // The band hangs from `_SeaLevelY` — the STILL-water level — not from
    // `_SeaWetLevelY`: that one carries the run-up and moves with every wave.
    float sandTop    = _SeaLevelY + _SandBandAbove;
    float sandBottom = _SeaLevelY - _SandBandBelow;
    float sandBand = (1.0 - smoothstep(sandTop - _SandFade, sandTop, worldPos.y))
                   * smoothstep(sandBottom, sandBottom + _SandFade, worldPos.y);

    // The patch scale is in metres and asks for a wavelength; `MountainFbm` wants a
    // frequency, hence the reciprocal. Two octaves: the boundary should undulate, not fray.
    float sandPatch = MountainFbm(float3(worldPos.x, 0.0, worldPos.z)
                                  * (1.0 / max(1.0, _SandPatchScale)), 2);

    // THE SLOPE WINDOW ARRIVES AS TWO COSINES, it is not built here. The rock and gravel
    // masks add a fixed +-0.08 to `cos(limit)`, which works at their 38 degrees but breaks
    // at a shallow limit: `cos(6 deg) + 0.06` is 1.05 and no surface can reach it, so the
    // mask saturated at 0.73 even on dead flat ground. The CPU sends `cos(limit +- 3 deg)`
    // and the window is the same three degrees wherever the limit sits.
    float sandFit = smoothstep(_SandSlopeCos.x, _SandSlopeCos.y, slope);

    float sand = sandBand * sandFit * _SandAmount
               * smoothstep(_SandPatchThreshold - 0.12, _SandPatchThreshold + 0.12, sandPatch);

    // THE DERIVATIVES ARE TAKEN OUTSIDE THE BRANCH. `ddx`/`ddy` are undefined in
    // divergent control flow, and at the edge of the band one quad really does diverge.
    // Taken here and handed to `SAMPLE_TEXTURE2D_GRAD`, the mip is correct in both branches.
    float2 sandUV = worldPos.xz / max(0.01, _SandTexScale);
    float2 sandDX = ddx(sandUV);
    float2 sandDY = ddy(sandUV);

    // The rock's own relief is faded out by the same mask further down, so the two do not add up.
    float2 sandSlopeXZ = float2(0.0, 0.0);
    float sandSmoothness = 0.0;

    UNITY_BRANCH
    if (sand > 0.002)
    {
        half3 sandColor = SAMPLE_TEXTURE2D_GRAD(_SandAlbedo, sampler_SandAlbedo,
                                                sandUV, sandDX, sandDY).rgb * _SandTint.rgb;
        half sandOcc = SAMPLE_TEXTURE2D_GRAD(_SandAO, sampler_SandAlbedo,
                                             sandUV, sandDX, sandDY).r;
        half sandRough = SAMPLE_TEXTURE2D_GRAD(_SandRough, sampler_SandAlbedo,
                                               sandUV, sandDX, sandDY).r;
        half4 sandNrm = SAMPLE_TEXTURE2D_GRAD(_SandNormal, sampler_SandAlbedo,
                                              sandUV, sandDX, sandDY);

        albedo = lerp(albedo, sandColor * sandOcc, sand);
        sandSmoothness = 1.0 - sandRough;

        // The map is OpenGL convention: X is the world X tilt, Y the world Z tilt. The
        // ground here is nearly flat by definition (the slope limit lets nothing steep in),
        // so the tangent frame is the world frame and no basis has to be built.
        float3 n = UnpackNormalScale(sandNrm, _SandNormalStrength);
        sandSlopeXZ = n.xy;
    }

    // --- Wetness: precipitation darkens and glosses the rock ---
    float wet = _SurfaceWetness * (1.0 - exposure * 0.3);
    albedo *= 1.0 - wet * _WetDarkening;

    // --- Sea wetness: the shore band (sea spec §14) ---
    //
    // THERE IS A NAME COLLISION: the `wet` above is PRECIPITATION wetness driven by
    // `_SurfaceWetness`. The sea band is a separate variable; colliding, the shore
    // would darken twice in rainy weather.
    //
    // The level is published by `SeaWetnessDriver`, and while the sea is off it pulls it
    // to a very low elevation, so `seaWet` is 0 everywhere. The sea system writes
    // NOTHING into this material — only two globals are read.
    // A BAND, NOT A HALF-SPACE.
    //
    // This was `1 - smoothstep(level - fade, level, y)`, which returns 1 for EVERY
    // point below the waterline elevation — a metre from the water or a kilometre
    // inland, all of it counted as soaking wet. Measured on the sand texture
    // (mean albedo 0.61/0.54/0.43, mean roughness 0.67): the albedo went to 0.55 of
    // a warm beige, i.e. dark grey, and the roughness to 0.35 of 0.67 = 0.23, which
    // is lacquer, not wet sand. The whole beach came out grey and plastic.
    //
    // A swash zone is a band: from the run-up line down to about where the water
    // stands.
    float seaWetBottom = _SeaWetLevelY - max(_SeaWetBandM, 1e-3);

    // ABOVE THE WATER'S REACH THIS WHOLE BLOCK PRODUCES ZERO, SO IT IS NOT RUN.
    //
    // `swash` is `1 - smoothstep(level - fade, level, y)`, which is 0 for every point at or
    // above `_SeaWetLevelY`; `submerged` is the same shape at `_SeaLevelY`. The run-up level
    // is the still level plus a non-negative surge, so a single comparison covers both — the
    // `max` is there because that ordering is an invariant of another system, not of this file.
    //
    // What the gate skips is not the smoothsteps, it is `laceNoise`: two fbm calls, three plus
    // two octaves, five `MountainNoise` evaluations, FORTY hashes. Paid on every terrain pixel
    // of a 6 km mountain for a band a few metres tall.
    //
    // Bit-identical: every term below is multiplied into `albedo` through a weight that is
    // exactly 0 up there, and `seaRough` further down reads `swash`, which is initialised to 0.
    float swash = 0.0;

    if (worldPos.y < max(_SeaWetLevelY, _SeaLevelY))
    {
        swash = (1.0 - smoothstep(_SeaWetLevelY - _SeaWetFadeM,
                                        _SeaWetLevelY, worldPos.y))
                    * smoothstep(seaWetBottom - _SeaWetFadeM,
                                 seaWetBottom, worldPos.y);

        // BELOW THE WATERLINE THE GROUND IS WET, FULL STOP.
        //
        // The band's bottom was written on the assumption that "the sea draws over it
        // anyway". It does not: the sea shows what is UNDER it. `refracted` samples the
        // scene colour and the water is clear over the first metres — measured
        // transmittance at 25 m out (path 2.8 m, extinction 0.30/0.08/0.05 per m) is
        // 0.43 / 0.80 / 0.87. So the sea bed arrived at the eye almost undimmed, drawn
        // with DRY sand (albedo 0.61/0.54/0.43): the shallows read as a bright cream
        // sheet out to where the water finally gets deep.
        //
        // The band's bottom still exists — it is what stops ground far ABOVE the water
        // from counting as wet, which is the symptom it was added for.
        float submerged = 1.0 - smoothstep(_SeaLevelY - _SeaWetFadeM,
                                           _SeaLevelY, worldPos.y);

        float seaWet = max(swash, submerged);
        albedo = lerp(albedo, albedo * _SeaWetDarkening, seaWet);

        // --- The swash lace, on the sand ---
        //
        // THE WATERLINE ENDED ON A DRAWN LINE. The sea mesh is clipped at depth 0,
        // so the foam stops at a geometric edge and meets dry sand there: white on
        // one side, dark on the other, with nothing in between. A real swash does
        // not end at the still waterline — it runs up the beach and leaves a lace
        // that thins as it drains.
        //
        // The terrain draws that part. It is not a second source: the sea publishes
        // the run-up level (`_SeaWetLevelY` already carries the phase) and the sand
        // draws the residue below it. The noise eats the coverage from underneath,
        // so the lace breaks into patches instead of ending on an edge of its own.
        // The lace rides on the SWASH band only. On the submerged part there is no
        // swash — the sea draws its own foam there.
        float laceBand = swash;

        float laceNoise = MountainFbm(worldPos * 0.75, 3)
                        + MountainFbm(worldPos * 3.1, 2) * 0.5;

        float lace = saturate((laceBand - (1.25 - laceNoise) * 0.7) * 2.2);

        // Foam is a scattering surface: it takes the light but shows none of the
        // sand under it. The colour is the sea's foam colour, kept off-white.
        albedo = lerp(albedo, float3(0.78, 0.80, 0.82), lace * 0.6);
    }

    // --- Bump noise: the raw material of the procedural normal; without it the surface
    //     looks like plastic. Its value goes to the snow's micro placement and its gradient
    //     to the shading — two consumers, one sample ---
    // The slope is ANALYTIC: a finite difference wanted three samples (8 hashes x 3), while
    // the derivative falls out of a single sample. bx = df/dx · e, because to first order a
    // finite difference is exactly that.
    float e = 0.35;
    float3 bumpGrad;
    float here = MountainFbmD(worldPos * _BumpScale, 2, bumpGrad);
    float bx = bumpGrad.x * _BumpScale * e;
    float bz = bumpGrad.z * _BumpScale * e;

    float rockRelief = _BumpStrength * (1.0 + wet * 0.3);

    float2 gradient = float2(-bx, -bz);

    // The rock relief and the sand relief are CROSSFADED, not summed: the sand map already
    // carries its own dimples and grain, and adding the procedural rock bump on top would
    // leave the beach with two conflicting reliefs at two different scales.
    float2 shaped = lerp(gradient * rockRelief, sandSlopeXZ, sand);

    // --- Rain rings ---
    //
    // THE SAME RING AS THE SEA'S, AND THE SAME FILE. A drop landing in a puddle leaves what
    // it leaves in the ocean; only the amount of water it lands in differs.
    //
    // TWO TERMS, NOT ONE. `_SurfaceRainIntensity` is whether a drop is landing at all --
    // it stops the instant the rain does. `wet` is whether there is a film for it to ring
    // in: on dry rock a drop leaves a dark spot, not a ring, and here that is simply
    // nothing. The film lags the rain by design, so the rings fade out with the shower and
    // the darkening outlives them.
    //
    // Faded by the pixel like every other scale, or it is the aliasing all over again.
    float2 ringPixel = float2(length(ddx(worldPos.xz)), length(ddy(worldPos.xz)));
    float2 ringLocal = RainRingLocal(worldPos.xz, _WorldSpaceCameraPos.xz);
    shaped += RainRings(ringLocal, _Time.y, _SurfaceRainIntensity * wet)
            * RainRingResolvable(max(ringPixel.x, ringPixel.y));

    float3 shaded = normalize(normalWS + float3(shaped.x, 0.0, shaped.y));

    MountainSurface surface;
    surface.snowMask = 0;
    surface.snowBlend.albedoTint  = half3(1, 1, 1);
    surface.snowBlend.roughAdd    = 0;
    surface.snowBlend.normalSlope = half2(0, 0);
    surface.snowRhoN    = (half)_FallbackRhoN;
    surface.snowWet     = (half)_SurfaceWetness;
    surface.snowDisturb = 0;
    surface.snowDentDepth = 0;
    surface.albedo = albedo;
    surface.emission = Alpenglow(worldPos, normalWS, altitude, albedo, exposure);
    surface.normalWS = shaded;
    surface.smoothness = lerp(_RockSmoothness, _WetSmoothness, wet);

    // The sand's own roughness map replaces the rock's; the sea wetness below then makes the
    // wet strip glossier on top of it, which is the order it happens in reality too.
    surface.smoothness = lerp(surface.smoothness, sandSmoothness, sand);

    // WET SAND IS GLOSSIER. Spec §14 cuts the roughness to 0.35 of its value; this code
    // holds smoothness, so it is converted to roughness and back — multiplying smoothness
    // by 0.65 directly would work in the OPPOSITE direction.
    // WET SAND IS GLOSSIER, NOT LACQUERED. At 0.35 the sand's own 0.67 roughness
    // fell to 0.23 and the surface read as plastic. Real wet sand sits around
    // 0.40-0.45 perceptual roughness.
    // The sheen belongs to the SWASH: a film of water on sand. Under water there is
    // no film, and smoothing the sea bed would put a lacquer under the surface.
    float seaRough = (1.0 - surface.smoothness) * lerp(1.0, 0.65, swash);
    surface.smoothness = 1.0 - seaRough;

    // The exposure map scales the ambient down: a valley floor sees only a small part of the
    // sky. It works at the hundred-metre scale; the centimetre scale was owned by SSAO but
    // that was turned off — from the depth buffer it was shading the breaks of the terrain
    // triangles (see DECISIONS).
    //
    // That scale is compensated by the micro-cavity — from the bump's CURVATURE, not its
    // VALUE: darkening the value painted the low regions of the noise as metre-scale dirty
    // patches (tried, reverted). The definition of a hollow is the second derivative: a
    // point lower than its surroundings is a pit, and only that dims. The two extra samples
    // are only taken in the near field — which was also the scale SSAO covered; the distant
    // mountain is untouched. As the snow thickens it is buried along with the bump.
    float microCavity = 1.0;
    float cavityDip = 0.0;
    float cavityRange = 1.0 - smoothstep(20.0, 50.0, length(_WorldSpaceCameraPos - worldPos));
    if (cavityRange > 0.0)
    {
        float bx2 = MountainFbm((worldPos - float3(e, 0, 0)) * _BumpScale, 2) - here;
        float bz2 = MountainFbm((worldPos - float3(0, 0, e)) * _BumpScale, 2) - here;

        // Laplacian: (f(+e)+f(-e)-2f) summed over two axes; positive = a pit
        float coarseDip = saturate((bx + bx2 + bz + bz2) * 6.0);

        // A fine crack octave: a ~1 metre fissure scale beneath the bump's ~3 metre hollows.
        // This is what gives rock the real "bottom of a crack" feel; because it is only
        // sampled in the near field it costs the distant mountain nothing.
        float fineScale = _BumpScale * 3.0;
        const float fe = 0.12;
        float fineLap = MountainFbm((worldPos + float3(fe, 0, 0)) * fineScale, 2)
                      + MountainFbm((worldPos - float3(fe, 0, 0)) * fineScale, 2)
                      + MountainFbm((worldPos + float3(0, 0, fe)) * fineScale, 2)
                      + MountainFbm((worldPos - float3(0, 0, fe)) * fineScale, 2)
                      - 4.0 * MountainFbm(worldPos * fineScale, 2);
        float fineDip = saturate(fineLap * 9.0);

        cavityDip = saturate(coarseDip * 0.28 + fineDip * 0.18) * cavityRange;
        microCavity = 1.0 - cavityDip;
    }

    surface.occlusion = lerp(1.0, exposure, _CavityStrength) * microCavity;
    surface.snowSurfaceHeight = 0;

    // Dust and debris collect at the bottom: a hollow is not only dim, it is also MATTE. The
    // same pit value is converted into roughness — for free.
    surface.smoothness *= 1.0 - cavityDip * 1.2;

    // ----------------------------------------------------------------- snow
    //
    // NO DISPLACEMENT, this is a shading layer. The real snow that deforms only exists in
    // the 24 m region around the player (the snow mesh).
    //
    // THE TERRAIN'S SNOW IS COVER, NOT DEPTH (spec §16).
    //
    // It used to read DEPTH from `SnowStateAt`. That function returns the state texture
    // inside the region and `_FallbackSWE` outside — two separate numbers. The snow mesh
    // meanwhile drops its thickness to zero at its edge (spec §8.3). Overlapped, a TRENCH
    // remained in the outer 2 metres of the mesh: the mesh showed 0 cm while the terrain
    // painted 45 cm. The square around the player was the frame of that trench (measured —
    // thickness probe, `SYMPTOMS.md`).
    //
    // The spec's own path: the terrain cover comes from the GLOBAL SCALAR `_SnowCoverage`
    // and its thickness is `_SnowCoverThickness` (4 cm). The same number everywhere, and
    // there is no such thing as a region boundary.

    // SLOPE: snow does not hold on steep rock. 0.45 ~ 63° of inclination.
    float snowSlope = saturate((normalWS.y - 0.45) / 0.35);

    // THE EDGE BREAKUP COMES FROM THE MOUNTAIN'S OWN NOISE. No new texture is added —
    // the rock's bump is already there, and with the snow boundary settling on it the
    // boundary does not look like a straight cut.
    float snowBreak = MountainFbm(worldPos * _BumpScale * 0.35, 2) * 0.5 + 0.5;

    float snowMask = SnowCoverMaskWithNoise(worldPos, normalWS, surface.occlusion, snowBreak,
                                            0.45, _SnowCoverSlopeSharpness,
                                            _SnowCoverBreakupStrength, _SnowCoverEdgeSharpness);

    // SNOW DOES NOT LIE ON THE SEA BED.
    //
    // `SnowCoverMaskWithNoise` asks four questions — slope, sky, cavity, noise — and the sea
    // is not among them, so the snow line ran straight down the beach and carried on under
    // the water: standing in the shallows and looking down, the bottom was white.
    //
    // Sea water does not let it. Salt puts the freezing point at -1.9 C and the sea's heat
    // capacity holds the surface layer there whatever the air is doing, so a flake that
    // reaches the water melts; the sea bed never accumulates. The same is true of the swash
    // zone, which every run-up wets with that water.
    //
    // The boundary is NOT a second invented line: it is `_SeaWetLevelY`, the run-up level the
    // sea already publishes and the wet-sand band already hangs from, and it fades over
    // `_SeaWetFadeM`, the same fade. Snow stops exactly where the sand stops being wet.
    // `max` with the still level because the run-up is dragged far below the world when the
    // sea is switched off, and the still level is the honest floor in that case.
    float seaReach = max(_SeaWetLevelY, _SeaLevelY);
    snowMask *= smoothstep(seaReach, seaReach + max(_SeaWetFadeM, 1e-3), worldPos.y);

    if (snowMask > 0.001)
    {
        // THE TRAIL IS REAL GEOMETRY — NOT PARALLAX.
        //
        // RELIEF MAPPING WAS REMOVED. `SnowReliefOffset` marched the view ray to find
        // where the hollow appeared, and every later read was taken from the SHIFTED
        // position. Once the snow surface became real geometry through tessellation the
        // same hollow was carved a second time inside `SnowTessYerDegistirme`: the trail
        // looked TWICE as deep.
        //
        // The geometry already gives everything parallax gave — and it breaks the
        // silhouette too, so neighbouring bumps really do occlude each other. The ray
        // march's 12-32 steps went with it.
        float3 trailPos = worldPos;
        float2 trailUV = SnowWorldToUV(trailPos);
        float trailDepth = SnowDentSmooth(trailUV);

        // THE INSIDE OF A TRAIL IS CRUSHED SNOW — THE DENSITY IS READ LOCALLY.
        //
        // The density used to be the world's general value everywhere (`_FallbackRhoN`)
        // and the state texture was not read; the reasoning was "freshness jumps at the
        // region boundary and the square comes back". That reasoning held while there was
        // a second surface: there is no boundary any more, because one shader draws it all.
        //
        // The jump risk is closed by `SnowInsideMask`: at the region edge the local value
        // transitions smoothly into the world's. The gain is that the INSIDE of a footprint
        // really looks compacted — pressed snow densifies, its albedo drops and its
        // roughness rises.
        float4 snowState = SnowStateAt(trailUV);
        float bolgeIci  = SnowInsideMask(trailUV);

        float localRho = lerp(_FallbackRhoN, snowState.g, bolgeIci);
        float localWet = lerp(_SurfaceWetness, max(_SurfaceWetness, snowState.b), bolgeIci);
        float localDisturb = snowState.a * bolgeIci;

        // WHERE THE BOOT TOOK THE SNOW AWAY, THERE IS NO SNOW.
        //
        // `SnowCoverMaskWithNoise` asks four questions — slope, sky, cavity, noise — and not
        // one of them is the trail. The mask therefore stayed at full strength inside a
        // footprint even when the sole had scraped the layer down to nothing, so a print in
        // thin snow could only ever be white-on-white: a hollow lit slightly differently,
        // never bare ground.
        //
        // That is the wrong end of the physics. On 1 cm of snow a boot print is unmistakable
        // precisely BECAUSE it exposes what is underneath — the contrast is a material
        // change, not a depth cue. Depth is what makes a print in 20 cm of snow read, and
        // that mechanism (relief, normals, its own shadow) is already wired.
        //
        // The remaining column is what the coverage kernel already fades on
        // (`SnowSim.compute` KCoverage): below SNOW_MIN_VISIBLE_HEIGHT there is nothing left
        // to draw. The same threshold is applied here to the COLUMN AFTER CARVING.
        //
        // THE COLUMN IS MEASURED AT THE WORLD'S DENSITY, NOT THE LOCAL ONE. `KDeform` clamps
        // the carve against `refThickness = SnowBaseHeight(snow.r, _FallbackRhoN)`, so that is
        // the frame the carve is expressed in. Using the LOCAL density here mixes two
        // quantities: inside a print the snow is compacted, its column height shrinks, and the
        // carve — which does not shrink with it — overtook the column. MEASURED with the local
        // density: a 20 cm print reported a negative remainder and opened to bare ground, which
        // is exactly the artefact this term exists to avoid.
        //
        // MEASURED, walking the same four metres, against the world's column:
        //   1 cm layer  -> column 9.9 mm, carve 7.0 mm -> 2.9 mm left, under the 4 mm floor
        //   20 cm layer -> column 200 mm, carve 108 mm -> 92 mm left, far above it
        // so the thin print opens to the ground and the deep print is untouched.
        float carved = SnowDentAt(trailUV);
        float remainingColumn = max(0.0, SnowBaseHeight(snowState.r, _FallbackRhoN) - carved);

        snowMask *= lerp(1.0, saturate((remainingColumn - SNOW_MIN_VISIBLE_HEIGHT)
                                       / SNOW_EDGE_FADE_RANGE), bolgeIci);

        // Spec §14.1: albedo and roughness come from FRESHNESS, and freshness from density.
        float freshness = 1.0 - saturate((SnowDensity(localRho) - 100.0) / 350.0);

        half3 snowAlbedo = lerp(half3(0.70, 0.73, 0.79), half3(0.90, 0.92, 0.95), freshness);
        // Dry snow is rough; the reasoning is in `SnowConstants.hlsl` -> `SNOW_ROUGH_PACKED`.
        //
        // TWO PATHS MUST READ THE SAME CONSTANT. 0.28 had been written here while
        // `SnowBuildSurfaceFrom` used 0.45 — the same snow was drawn at two different
        // brightnesses on the terrain and on the snow mesh. The comment said "they have to
        // use the same number" but the number sat separately in two places.
        //
        // COMPACTED SNOW IS NOT DARK, IT IS HARD — BUT IT IS NOT A MIRROR EITHER.
        //
        // [SOURCE: snow material breakdown — "flatter areas scoured by the wind reveal a
        // slightly more compacted frozen snow layer underneath", and those areas are LESS
        // rough.] Pressure breaks the dendrites, the edges round off and the surface
        // flattens. It flattens, it does not vitrify: ice's F0 is fixed at 0.018 and
        // compaction does not change it.
        half  snowRough  = lerp(SNOW_ROUGH_PACKED, SNOW_ROUGH_FRESH, freshness);

        // THE SURFACE TEXTURE ENTERS HERE.
        //
        // The terrain's snow albedo is built in this block, independent of
        // `SnowBuildSurface`; wired only into that one, the texture had NO effect at all on
        // the terrain (measured: no screen difference between strength 0 and 3).
        //
        // The snow mesh uses the same blend. The two must see the same texture: the mesh
        // only draws the local deviation, the terrain draws the flat area.
        SnowSurfaceBlend snowSurface = SnowSampleSurface(trailPos, localRho, localWet, localDisturb);
        surface.snowBlend   = snowSurface;
        surface.snowRhoN    = (half)localRho;
        surface.snowWet     = (half)localWet;
        surface.snowDisturb = (half)localDisturb;
        surface.snowDentDepth = (half)trailDepth;

        snowAlbedo = saturate(snowAlbedo * snowSurface.albedoTint);
        snowRough  = saturate(snowRough + snowSurface.roughAdd);

        surface.albedo     = lerp(surface.albedo, snowAlbedo, snowMask);
        surface.smoothness = lerp(surface.smoothness, 1.0 - snowRough, snowMask);

        // Snow BURIES the rock's bump: the normal flattens toward the geometric normal.
        // Showing a crack under the snow makes it wet rock, not snow.
        float3 snowNormal = normalWS;

        // Spec §14.2's Macro layer. `disturb` is zero: there is no trail on the mountain and
        // the crushed snow layer has no counterpart here either.
        //
        // DETAIL ONLY ON NEAR-HORIZONTAL SURFACES. The spec's own precondition:
        // `WorldNormalToTangentPacked` pins the tangent frame to world +Y and gives its
        // reasoning as "the snow surface is near horizontal". True for the snow MESH; not on
        // the mountain. A planar XZ sampling is squashed vertically on a steep face and
        // leaves black streaks running down the surface (measured: the streaks appear only
        // in the band where the slope steepens).
        //
        // The weight is `snowSlope`: already computed, not a new term. Where snow holds and
        // where the detail is valid are the same place.
        float3 detailed = SnowApplyDetailNormals(snowNormal, worldPos, 0.0,
                                                 length(_WorldSpaceCameraPos - worldPos));

        // Add the texture normal too: the real information of the snow texture is here.
        {
            float2 e = float2(detailed.x, detailed.z) / max(detailed.y, 1e-3)
                     + (float2)snowSurface.normalSlope;
            detailed = normalize(float3(e.x, 1.0, e.y));
        }


        snowNormal = normalize(lerp(snowNormal, detailed, snowSlope));

        surface.normalWS = normalize(lerp(surface.normalWS, snowNormal, snowMask));

        // THE CAVITY'S SLOPE ENTERS THE NORMAL LAST.
        //
        // Placed INSIDE the `snowMask` blend it would be watered down by the mask; a trail
        // is not a lighting layer but the surface's own shape — if the snow is there, so is
        // the hollow. Measured: inside the blend a 22 cm trail was barely discernible on
        // screen.
        {
            // THE GATE ASKS "IS THERE A TRAIL", NOT "HOW DEEP IS IT".
            //
            // `trailSlope` ALREADY carries the depth: it is the derivative of the dent, so a
            // shallow hollow produces a gentle slope on its own. Weighting it a second time by
            // the absolute depth charged shallow prints twice.
            //
            // `trailDepth * 20` reaches 1 only at 5 cm. MEASURED, walking the same four metres:
            //   20 cm layer -> dent 107 mm -> weight 1.00
            //   1 cm layer  -> dent 7 mm   -> weight 0.14
            // so the 1 cm print's normal was applied at a seventh strength on top of a slope
            // that was already fifteen times gentler, and the print rendered as untouched snow.
            //
            // 2 mm is the presence threshold: below it the dent is texture noise rather than a
            // footprint. Everything above it gets its own slope, at full strength, whatever the
            // depth. Deep snow is unaffected — it saturated long before either threshold.
            const float TrailPresenceMeters = 0.002;

            half2 trailSlope = SnowDentSlope(trailUV);
            float3 n = surface.normalWS;
            float2 e = float2(n.x, n.z) / max(n.y, 1e-3) - (float2)trailSlope;
            surface.normalWS = normalize(lerp(n, normalize(float3(e.x, 1.0, e.y)),
                                              saturate(trailDepth / TrailPresenceMeters)));
        }

        // THE SNOW SURFACE'S OWN RELIEF — INDEPENDENT OF THE TRAIL.
        //
        // THE ROOT BUG WAS HERE. The bedforms (fBm, ripple, sastrugi) and the micro relief
        // came through `SnowDentSlope`, and that was blended with the
        // `saturate(trailDepth * 20.0)` weight above. On flat snow `trailDepth = 0`, i.e. the
        // weight is ZERO: every surface detail added never reached the screen at all (the
        // user reported it three rounds in a row: "there is no detail at all in the snow outside").
        //
        // That gate is only right for the TRAIL — where there is no trail there should be no
        // cavity slope either. But the surface's own relief exists EVERYWHERE there is snow,
        // so its weight is `snowMask`.
        {
            float surfaceHeight;
            // Thickness of the snow layer: the bedforms cannot be deeper than this.
            float snowThickness = SnowBaseHeight(snowState.r, localRho);

            half2 surfaceSlope = SnowSurfaceSlope(trailPos.xz, trailPos.y, snowThickness, surfaceHeight)
                            + SnowMicroSlope(trailPos.xz, trailDepth);

            surface.snowSurfaceHeight = (half)surfaceHeight;

            float3 n = surface.normalWS;
            float2 e = float2(n.x, n.z) / max(n.y, 1e-3) - (float2)surfaceSlope;

            surface.normalWS = normalize(lerp(n, normalize(float3(e.x, 1.0, e.y)),
                                              snowMask));

        }

        // The micro-cavity is buried under the snow — but NOT COMPLETELY.
        //
        // Flattened with 0.7 the terrain's hollows vanished under the snow and the snow
        // stayed one solid white (measured: ground deviation 0.010, 0.0023 with no sun).
        // Snow fills a hollow, it does not erase it: a 15-20 cm cover does not close a
        // metre-scale pit. The share was lowered to 0.55: at 0.35 the ground luma fell from
        // 0.88 to 0.59 and stayed too dark for sunlit snow.
        // THE BURIAL SHARE COMES FROM THE SNOW THICKNESS.
        //
        // It was a fixed 0.55: 1 cm of snow buried the terrain's hollows as much as 50 cm
        // did. Physics does not give that — a centimetre cover does not close a metre pit, a
        // half-metre cover does.
        //
        // This is the strongest visual consequence of snow thickness: in thin snow the
        // terrain's own bump reads, in thick snow the surface turns into solid white (the
        // user reported: "there is no difference between 1cm, 5cm, 20cm and 50cm").
        //
        // `SNOW_BURY_REF_DEPTH` = 0.30 m: at that thickness the burial reaches the full
        // share (0.55), below it proportionally.
        {
            float thickness = SnowBaseHeight(snowState.r, localRho);
            half buryFactor = (half)(0.55 * saturate(thickness / SNOW_BURY_REF_DEPTH));

            surface.occlusion = lerp(surface.occlusion, 1.0, snowMask * buryFactor);
        }

        // THE CAVITY'S VIEW FACTOR IS COMPENSATED BY MULTIPLE SCATTERING.
        //
        // A hollow sees the sky through a narrow angle: the view factor `V` falls with
        // depth. But THE CAVITY'S OWN WALLS take the place of the lost sky light, and those
        // walls are white. The equilibrium in a cavity of albedo `a`:
        //
        //     gain = V / (1 - a(1 - V))
        //
        // The same multiple-scattering formula is used in the snow-sky chain as well
        // (`SnowLighting.hlsl` -> `SnowAmbient`); no separate source is created.
        //
        // With numbers: at a = 0.91 and V = 0.65 the gain is 0.95 — only 5% darker. Without
        // the compensation the same V was applied directly, and DARKENING A WHITE SURFACE BY
        // A FLAT RATIO turned it GREY (the user reported: "the snow trail is grey?? I don't
        // know why it's grey either").
        //
        // It is applied AFTER the snow flattening: the flattening would erase the hollow.
        {
            half a = dot(snowAlbedo, half3(0.2126, 0.7152, 0.0722));
            // A HOLLOW SEES MORE OF THE SKY THAN WE ASSUMED.
            //
            // The coefficient was 0.55 and at 22 cm of depth it dropped the view factor to
            // 0.65. The geometry does not support that: the trail is 60 cm wide and 22 cm
            // deep; seen from the edge the half-angle is atan(30/22) = 54°, so most of the
            // sky is visible. The view factor is ~0.78.
            //
            // Three terms (view factor, its own shadow, compacted snow albedo) are applied
            // as a product; each is reasonable on its own while their product dropped the
            // trail to a third of flat snow (the user reported: "why is the inside of the
            // snow dark").
            half V = (half)saturate(1.0 - 0.35 * trailDepth / SNOW_RELIEF_MAX_DEPTH);

            surface.occlusion *= V / max((half)1.0 - a * ((half)1.0 - V), (half)0.05);
        }

        // THE SURFACE'S OWN AMBIENT OCCLUSION — INDEPENDENT OF THE LIGHT.
        //
        // The normal's contribution is only visible with DIRECT light: with the sun overhead
        // a 7° slope changes NdotL by 1% and the surface reads flat (measured: 12:00,
        // SunHeight 0.88 — no relief visible; clearly visible at night under grazing light).
        // That is how it is in physics too.
        //
        // But real snow reads at noon as well, because hollows see less of the sky. That
        // term comes from the surface's HEIGHT, not its slope, and is independent of the sun
        // direction.
        //
        // IT WAS PUT LAST. Inside the normal block, the `lerp(occlusion, 1.0,
        // snowMask * 0.55)` share that followed immediately erased more than half of it
        // (measured: no effect visible in the noon frame).
        {
            // THE DENOMINATOR IS THE SURFACE RELIEF'S REAL CEILING, NOT THE fBm AMPLITUDE.
            //
            // `SNOW_FBM_AMP` (1.5 cm) was written there and that is ONLY the fBm layer's
            // amplitude. `snowSurfaceHeight` meanwhile is the sum of every layer; once the
            // drift (15 cm) and sastrugi (20 cm) reached terrain scale, an ordinary 10 cm
            // hollow saturated to 1.0 from `saturate(0.10/0.015)` = 6.67.
            //
            // The consequence: every hollow took FULL darkening, no mid tones remained and
            // the surface broke into two-tone blotches with hard edges. Neighbouring
            // saturated hollows merged and the blotches grew to tens of metres (the user
            // reported: "I can't get my head round this shading", and found the culprit
            // himself: "that's the snow surface's own shadow").
            //
            // The right denominator is the relief's own ceiling — `SnowSurfaceRelief` already
            // clips the height there. In 50 cm of snow that is 30 cm; the same 10 cm hollow
            // gives 0.33 and the mid tones come back.
            float reliefMax = SnowBaseHeight(snowState.r, localRho)
                              * SNOW_BEDFORM_DEPTH_FRAC;

            half depression = (half)saturate(-surface.snowSurfaceHeight
                                        / max(reliefMax, 1e-4));

            surface.occlusion *= lerp((half)1.0, (half)1.0 - SNOW_SURFACE_AO,
                                      depression * (half)snowMask);
        }

        // THE INSIDE OF A HOLLOW SHIFTS TOWARD BLUE — SNOW IS TRANSLUCENT.
        //
        // Ice's absorption coefficient at 600 nm is ~10 times its value at 450 nm. In
        // multiple scattering the path a photon travels grows with the hollow's depth, so
        // red is absorbed and blue remains. This is the most recognisable feature of real
        // snow photographs; without it a trail reads as FLAT GREY (the user reported: "the
        // trail is dead grey, it has no detail at all").
        //
        // It is NOT a compensation term: the block above returns the INTENSITY of the lost
        // sky light, this one returns its COLOR. They are two different quantities.
        {
            half depth01 = (half)saturate(trailDepth / SNOW_RELIEF_MAX_DEPTH);

            surface.albedo = lerp(surface.albedo,
                                  surface.albedo * (half3)SNOW_SSS_TINT,
                                  depth01 * (half)snowMask);
        }


        surface.snowMask = (half)snowMask;
    }

    return surface;
}

#endif