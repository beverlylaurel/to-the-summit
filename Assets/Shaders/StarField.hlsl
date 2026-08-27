#ifndef TTS_STAR_FIELD_INCLUDED
#define TTS_STAR_FIELD_INCLUDED

/// STAR FIELD — PROCEDURAL. It used to be baked as a cubemap; on a 512 face one texel is
/// 0.176°, while on screen at 1920px/90° FOV one pixel is 0.047°. So every star was
/// necessarily four pixels wide, and bilinear filtering spread it over its 2x2 texels into
/// a soft blob. Getting down to one pixel would need a 2048 face: 201 MB in RGBAHalf.
/// The texture route cannot solve this.
///
/// Here a star is drawn from the screen-space derivative, i.e. always ~1 pixel regardless
/// of resolution. Twinkling is only possible here too: a static texture cannot twinkle.
///
/// NOISE FROM AN INTEGER MIXER. `frac(sin(...))` correlates on small integer inputs and
/// produces a regular pattern (`CLOUDS_REBUILD.md`, lesson 9).

/// Grid per face. A cell is 90/128 = 0.70°, about 15 pixels on screen.
#define STAR_GRID 128.0

/// Stars visible to the naked eye: ~6000. The cell count is 6 x 128² = 98304, so the
/// probability of one falling in a cell is 0.061.
#define STAR_DENSITY 0.061

/// Magnitude range. Each magnitude is 10^(-0.4) times the previous one.
#define STAR_FAINTEST_MAGNITUDE 6.0

/// x = sine of the sun's elevation. Daytime fading comes from here; `SkyWeatherDriver` drives it.
float4 _StarFieldParams;

uint StarMix(uint x)
{
    x ^= x >> 17; x *= 0xed5ad4bbu;
    x ^= x >> 11; x *= 0xac4c1b51u;
    x ^= x >> 15; x *= 0x31848babu;
    x ^= x >> 14;
    return x;
}

float StarHash01(uint seed, uint channel)
{
    uint h = StarMix(seed * 747796405u + channel * 2891336453u + 1u);
    return (h & 0x00FFFFFFu) / 16777215.0;
}

/// Direction to cube face and in-face coordinate. Consistency across faces is not sought:
/// each face carries its own grid, only the cell boundary changes at the seam, and because
/// the star sits in the middle of its cell it is never clipped.
void StarDirectionToFace(float3 d, out uint face, out float2 uv)
{
    float3 a = abs(d);
    float major;
    float2 st;

    if (a.x >= a.y && a.x >= a.z)
    {
        major = a.x;
        face = d.x > 0.0 ? 0u : 1u;
        st = float2(d.x > 0.0 ? -d.z : d.z, -d.y);
    }
    else if (a.y >= a.z)
    {
        major = a.y;
        face = d.y > 0.0 ? 2u : 3u;
        st = float2(d.x, d.y > 0.0 ? d.z : -d.z);
    }
    else
    {
        major = a.z;
        face = d.z > 0.0 ? 4u : 5u;
        st = float2(d.z > 0.0 ? d.x : -d.x, -d.y);
    }

    uv = 0.5 * (st / max(major, 1e-6) + 1.0);
}

/// Star color from its temperature: hot ones blue-white, cool ones orange. Most sit close to
/// white — the selection is squeezed toward the middle so the extremes stay a minority.
float3 StarColor(float pick)
{
    float t = (pick - 0.5) * 2.0;
    t = sign(t) * t * t;

    return t < 0.0
        ? lerp(float3(1.0, 1.0, 1.0), float3(0.72, 0.80, 1.00), -t)
        : lerp(float3(1.0, 1.0, 1.0), float3(1.00, 0.84, 0.68), t);
}

/// DAYTIME FADING FROM SUN ELEVATION, SEPARATELY PER MAGNITUDE.
///
/// There used to be no fading: the package multiplies stars by `(1 - skyOpacity)` and that was
/// assumed to handle daytime. MEASURED, WRONG — at the zenith the daytime optical depth is ~0.2,
/// so opacity is ~0.2 and 80% of the stars came through; at 8 in the morning the sky was full of
/// stars. What really hides stars is not opacity but the sky being 10^5 times brighter; ours were
/// raised so they would be visible at night, so they survived the day as well.
///
/// The thresholds come from the real twilight definitions: a bright star appears once the sun
/// drops below -3°, the faintest waits for -18° (the end of astronomical twilight).
float StarDaylightFade(float magnitude)
{
    float faint = magnitude / STAR_FAINTEST_MAGNITUDE;
    float threshold = lerp(-0.052, -0.309, faint); // sin(−3°) … sin(−18°)

    return saturate((threshold - _StarFieldParams.x) * 20.0);
}

/// SCINTILLATION DEPENDS ON AIR MASS. Near the horizon the light passes through a far thicker
/// layer of air and refractive index fluctuations accumulate; at the zenith a star sits almost
/// still. It has no timer of its own, it uses `_Time` and a hashed phase.
float StarTwinkle(uint seed, float altitudeSin)
{
    float airmass = 1.0 / max(altitudeSin, 0.08);
    float amplitude = saturate((airmass - 1.0) * 0.30) * StarHash01(seed, 5u);

    float phase = StarHash01(seed, 6u) * 6.2831853;
    float speed = 3.0 + StarHash01(seed, 7u) * 5.0;

    // Two frequencies: a single sine reads as a regular pulse, and scintillation is irregular.
    float wave = sin(_Time.y * speed + phase) * 0.6
               + sin(_Time.y * speed * 1.73 + phase * 2.1) * 0.4;

    return 1.0 + amplitude * wave;
}

/// `dir` is the view direction with the space rotation applied, `altitudeSin` the sine of the
/// elevation in world space (air mass is computed from it, not from the star field's rotation).
float3 EvaluateStarField(float3 dir, float altitudeSin)
{
    uint face;
    float2 uv;
    StarDirectionToFace(dir, face, uv);

    float2 cellUV = uv * STAR_GRID;

    // Cell units per pixel. The derivative blows up at the seam; without clamping the star there
    // spreads over the whole cell.
    float2 footprint = fwidth(cellUV);
    float pixel = clamp(max(footprint.x, footprint.y), 1e-5, 0.25);

    int2 cell = int2(floor(cellUV));
    uint seed = StarMix(face + StarMix((uint)cell.x + StarMix((uint)cell.y)));

    if (StarHash01(seed, 0u) > STAR_DENSITY) return 0.0;

    // The star is placed in the MIDDLE 70% OF THE CELL. That way neighbouring cells never need
    // to be checked: there is a 15% margin to the edge, the cell is ~15 pixels and the star ~1.
    float2 starPos = float2(StarHash01(seed, 1u), StarHash01(seed, 2u)) * 0.7 + 0.15;

    // Faint stars are many, bright ones few. The cube root gives a distribution close to the real
    // count, which grows ~2.5x per magnitude.
    float magnitude = STAR_FAINTEST_MAGNITUDE * pow(StarHash01(seed, 3u), 1.0 / 3.0);
    float brightness = pow(10.0, -0.4 * magnitude);

    float fade = StarDaylightFade(magnitude);
    if (fade <= 0.0) return 0.0;

    // The radius is in PIXELS, so it is resolution independent. A bright star is slightly larger:
    // the eye reads it that way too, spreading with brightness despite being a point source.
    float radius = lerp(0.75, 1.35, 1.0 - magnitude / STAR_FAINTEST_MAGNITUDE);
    float distance = length(frac(cellUV) - starPos) / (pixel * radius);
    float core = exp(-distance * distance * 1.6);

    return StarColor(StarHash01(seed, 4u))
         * brightness * core * fade * StarTwinkle(seed, altitudeSin);
}

#endif // TTS_STAR_FIELD_INCLUDED
