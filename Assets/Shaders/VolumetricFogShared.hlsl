#ifndef TOTHESUMMIT_VOLUMETRIC_FOG_SHARED_INCLUDED
#define TOTHESUMMIT_VOLUMETRIC_FOG_SHARED_INCLUDED

// THE DENSITY MODEL OF FOG. Where the air is how dense — NOT what the light does
// there. Color, lighting and application stay in `HeightFog.hlsl`.
//
// WHY IT IS SEPARATE: this model is evaluated in two places. Inside the froxel volume
// by a compute shader, beyond the volume by the surface shader's analytic tail.
// `HeightFog.hlsl` CANNOT be included by the compute side — it depends on
// `_WorldSpaceCameraPos`, URP lighting and the surface context. If the model did not
// live in one place the two evaluators would drift and the structure of the fog would
// change at the volume boundary.
// The including file must have taken URP `Core.hlsl` FIRST: `TEXTURE2D`, `SAMPLER` and
// `SAMPLE_TEXTURE2D_LOD` oradan geliyor.

// ---------------------------------------------------------------------------
// FROXEL VOLUME — a 3D grid aligned to the camera frustum.
//
// The x/y axes are screen coordinates directly, the z axis is an EXPONENTIAL depth
// distribution. Wronski says the distribution "concentrates near the camera" but does
// not give the formula (spec §10.1); a pure exponential was chosen:
//
//     z(s) = near · (far/near)^s ,  s ∈ [0,1]
//
// The ratio per slice is constant: for 64 slices over 0.5 -> 1000 m, (2000)^(1/63) = 1.128,
// so each slice is 12.8% thicker than the previous one. 46 slices fall in the first 128
// metres — where Wronski spreads the entire volume over 128 m with 64 slices we put 46
// slices in that distance, so the range grows eightfold without losing near-field precision.
//
// THE REVERSED-Z TRAP. The distribution is built from LINEAR VIEW SPACE depth, not from
// clip-space z — Unity uses reversed-Z on most platforms and an exponential distribution
// built on clip z would pile the slices at the wrong end (spec §9.3, §12.7).
//
// x = near, y = far, z = log(far/near), w = slice count
float4 _FogVolumeDepth;

// Frustum corner rays, each scaled so its projection on the forward axis is 1:
// `worldPos = cameraPos + ray * viewDepth`. They are NOT normalized — normalized, the
// depth at the corners would stretch relative to the centre and the slices would become
// spherical shells, whereas a froxel slice is planar.
float4 _FogCornerRays[4];   // 00, 10, 01, 11 (bottom-left, bottom-right, top-left, top-right)

// x = temporal offset added to the slice [0,1), y/z/w unused. Wronski suggests jitter to
// trade aliasing for noise (spec §6.2); TAA already spreads the pattern.
float4 _FogJitter;

// Forward axis of the camera. View space depth comes from it: `dot(ray, forward)`.
// Written out explicitly rather than derived from the matrix — the sign convention of
// `UNITY_MATRIX_V` varies by platform and a silently flipped depth would shift the whole volume.
float4 _FogCameraForward;


// Accumulated scattering volume. The compute shader declares it as RW, so this block is
// closed there; declaring the same name with two different types makes the compiler clash.
#ifndef FOG_VOLUME_COMPUTE
TEXTURE3D(_FogScatteringVolume);
SAMPLER(sampler_FogScatteringVolume);
#endif

/// Linear view space depth from a slice index. `s` is continuous over [0,1] — not an
/// integer slice index, because the jitter wants the values in between too.
float FogViewDepthFromSlice(float s)
{
    return _FogVolumeDepth.x * exp(_FogVolumeDepth.z * s);
}

/// The other direction: slice coordinate from depth. Needed when sampling the volume
/// texture. Depth below `near` is clamped to zero — the logarithm goes negative there
/// and the sampling would run off the edge of the texture.
float FogSliceFromViewDepth(float viewDepth)
{
    return log(max(viewDepth, _FogVolumeDepth.x) / _FogVolumeDepth.x) / _FogVolumeDepth.z;
}

/// Sampling coordinate of the volume texture: screen uv plus the slice derived from depth.
float3 FogVolumeUVW(float2 screenUV, float viewDepth)
{
    return float3(screenUV, saturate(FogSliceFromViewDepth(viewDepth)));
}

/// World position of a froxel centre. `uv` is the cell's centre on the screen plane,
/// `viewDepth` that cell's depth.
float3 FogFroxelWorldPos(float3 cameraPos, float2 uv, float viewDepth)
{
    float3 bottom = lerp(_FogCornerRays[0].xyz, _FogCornerRays[1].xyz, uv.x);
    float3 top    = lerp(_FogCornerRays[2].xyz, _FogCornerRays[3].xyz, uv.x);

    return cameraPos + lerp(bottom, top, uv.y) * viewDepth;
}

// ---------------------------------------------------------------------------

float _HeightFogDensity;   // density of the settled air at its base elevation
float _HeightFogFalloff;   // thinning coefficient per metre
float _HeightFogBase;      // elevation the density is measured at (metres)
float _FogInversionHeight; // elevation where the fog is cut off: the ceiling of the cold air
float _FogInversionWidth;  // softness of that cut (metres)
// The free troposphere is a THIRD LAYER. Above the inversion used to be modelled as a
// "residual fraction" (`_FogAboveInversion`): because it was MULTIPLIED by the boundary
// layer's own shallow profile it zeroed out within a few thousand metres, and from the
// summit distant ridges had no haze at all — a ridge thirty kilometres away at full
// contrast, like cardboard. The air's own molecules (Rayleigh) are there and have their
// own scale height; they are added as a separate layer, not as a multiplier.
float _FogFreeDensity;     // density of the free air at its base elevation
float _FogFreeFalloff;     // from the Rayleigh scale height (far broader)
// The valley fog sea is a SEPARATE LAYER. It used to go through a single channel: the CPU
// computed it with its own 120 m profile and folded it into the settled air's density with
// `max()`, while the shader spread it with a 1400 m profile. A shallow sea climbed to the
// cloud base, the optical depth along the path came out ten times too high and it
// erased the clouds at dawn.
float _FogSeaDensity;      // density of the sea at its base elevation
float _FogSeaFalloff;      // the sea's own thinning coefficient (far steeper)
float3 _FogBankDrift;      // accumulated wind-driven translation of the bank field (metres)
float _FogBankStrength;    // how much the banks move density locally, 0-1

/// ABSOLUTE fog density at a given elevation. THREE layers are summed — each with its
/// own half height. Squeezing them into a shared profile or multiplying them together was
/// the source of three separate symptoms in this file; summing keeps them structurally apart.
///
/// BOUNDARY LAYER: moisture and dust collect low down, shallow, and deepen with
/// precipitation. An inversion sits on top: cold air is trapped in the valley, warm air
/// stands above it and the two do not mix. The fog does not end exponentially at that
/// boundary but almost as if cut with a knife — that is why, seen from the mountain,
/// the valley is full while everything above it is crystal clear.
///
/// Terrain height. Snow lifted by the wind clings to the GROUND; a profile that decays
/// relative to sea level is invisible on a ridge and drowns the valley. The texture is
/// baked in `SurfaceMapBaker`: 512 texels / 17.5 km = 34 metres, enough for a distant layer.
TEXTURE2D(_TerrainHeightMap);
SAMPLER(sampler_TerrainHeightMap);
float4 _TerrainHeightArea;   // xy corner position, z width, w height scale

float TerrainHeightAt(float2 xz)
{
    float2 uv = (xz - _TerrainHeightArea.xy) / max(1.0, _TerrainHeightArea.z);
    return SAMPLE_TEXTURE2D_LOD(_TerrainHeightMap, sampler_TerrainHeightMap,
                                saturate(uv), 0).r * _TerrainHeightArea.w;
}

// Accumulated fresh snow, ALONG THE ELEVATION AXIS. A 128x1 texture: R cover, G depth
// store. It decides both the surface color and the drifting snow — with no snow on the
// ground the wind has nothing to lift. It lives in the fog file because it is world state: both the surface and the sky read it.

/// FOG SEA: a very shallow layer collecting at the valley floor from radiative cooling at
/// night — it ends within a hundred metres. Spread with the shared profile it climbed to
/// the cloud base and raised the path's optical depth tenfold.
///
/// FREE TROPOSPHERE: the air's own molecules. Broad (Rayleigh scale height) and independent
/// of precipitation. It used to be modelled as a "residual fraction" above the inversion and
/// multiplied by the boundary layer's profile; it zeroed out within a few thousand metres and
/// from the summit a ridge thirty kilometres away stood at full contrast, like cardboard.
float FogDensityAt(float height)
{

    float lid = 1.0 - smoothstep(_FogInversionHeight - _FogInversionWidth,
                                 _FogInversionHeight + _FogInversionWidth, height);

    float boundary = _HeightFogDensity * exp(-_HeightFogFalloff * height) * lid;

    // Neither has a ceiling: one ends far below the inversion, the other reaches far above it.
    float sea = _FogSeaDensity * exp(-_FogSeaFalloff * height);
    float free = _FogFreeDensity * exp(-_FogFreeFalloff * height);

    return boundary + sea + free;
}

/// Fog banks: a low frequency field that multiplies density locally. Real mountain fog is
/// not a uniform soup — it travels in banks: it wraps a slope, sends a tongue into the
/// valley, and clears two minutes later. The field drifts with the wind. Wavelengths of hundreds of metres.
///
/// AtmosphereController samples the same formula on the CPU (band patches, visibility
/// breathing): two consumers, one field — if the formula changes both must change together.
float FogBankAt(float2 pos)
{

    float2 p = pos - _FogBankDrift.xz;

    // A SUM, NOT A PRODUCT. Two sines used to be MULTIPLIED and the comment said "the
    // product of two different frequencies breaks the repeat pattern" — it does not.
    // `sin(k1·p)·sin(k2·p)` is a separable expression and mathematically produces a regular
    // LATTICE; mixing frequencies does not change that. The symptom: at night, looking down
    // from 3700 m, a diagonal grid above the fog. Measured — the fog diagnostic (forcing the
    // medium uniform) destroyed the grid, the volume and cloud paths were ruled out, and this field was what remained.
    //
    // A random field is a SUPERPOSITION of modes; that is the definition of spectral noise.
    // Five components, their directions not parallel and their wavelengths incommensurate —
    // the resultant does not repeat in practice. A sine gives bit-identical results on CPU and
    // GPU; hash-based noise would not, and `AtmosphereController` has to sample the same field on the CPU.
    //
    // Wavelengths 350-1700 m: a fog bank is a structure hundreds of metres wide.
    float s = sin(dot(p, float2( 0.003534,  0.001081))) * 0.34
            + sin(dot(p, float2( 0.001090,  0.005607))) * 0.26
            + sin(dot(p, float2(-0.005424,  0.006239))) * 0.20
            + sin(dot(p, float2(-0.011122, -0.004720))) * 0.13
            + sin(dot(p, float2( 0.005250, -0.017167))) * 0.07;

    float bank = saturate(0.5 + 0.5 * s);                 // 0..1, ortalama 0.5

    // At full strength the range is 0.3-1.7: a bank cuts the fog locally to a third but never
    // to zero — a completely clear hole in foggy weather looks unreal.
    return lerp(1.0, 0.3 + bank * 1.4, _FogBankStrength);
}

/// The bank multiplier along the path: three samples, so the bank in front differs from the
/// one behind. Not inside the integration loop — with banks hundreds of metres wide
/// horizontally, eight times the noise cost would show and three samples are enough.
float FogBankPath(float2 fromXZ, float2 toXZ)
{
    float average = (FogBankAt(lerp(fromXZ, toXZ, 0.2))
                   + FogBankAt(lerp(fromXZ, toXZ, 0.5))
                   + FogBankAt(lerp(fromXZ, toXZ, 0.8))) / 3.0;

    // A LONG PATH CONVERGES TO THE MEAN. The field's wavelength is 350-1700 m; a path of
    // kilometres passes through dozens of banks and the true average settles on the field's
    // mean (multiplier 1). Three samples cannot produce that convergence — they fluctuate
    // with view direction and leave a pattern at distance: right up close, a lie far away.
    //
    // The convergence follows the path length: at a few hundred metres the bank structure is
    // fully visible, at kilometres it dies out. The limit comes from the field's own wavelength, not from thin air.
    float length2D = distance(fromXZ, toXZ);

    return lerp(1.0, average, exp(-length2D / 900.0));
}

#endif // TOTHESUMMIT_VOLUMETRIC_FOG_SHARED_INCLUDED
