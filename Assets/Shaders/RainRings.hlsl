#ifndef RAIN_RINGS_INCLUDED
#define RAIN_RINGS_INCLUDED

// ROLE: the ring a raindrop leaves on a water surface, as a slope perturbation.
// USED BY: the sea (`SeaLit.shader`) and the wet rock (`MountainSurface.hlsl`).
//
// IT IS THE WATER'S PHENOMENON, NOT THE SEA'S. A drop landing in a puddle and a drop
// landing in the ocean leave the same ring: the same speed, the same wavelength, the same
// decay. The maths therefore lives here rather than inside either surface, so the two
// cannot drift apart.

/// THE RING'S SPEED IS THE WATER'S, NOT THE RAIN'S. The minimum phase speed of a
/// capillary-gravity wave, `sqrt(2 sqrt(sigma g / rho))`, at a wavelength of 1.73 cm.
/// [SOURCE: Lamb, Hydrodynamics 267; sigma = 0.0728 N/m, rho = 1000]
#define RAIN_RING_SPEED          0.231

/// How long one ring is worth drawing (s). Viscous damping of a 1.7 cm ripple has an
/// e-folding time of 3.7 s (`2 nu k^2`, nu = 1e-6), but by then the crest has spread over a
/// circumference eight times longer and is no longer readable.
#define RAIN_RING_LIFE           1.0

/// The crest's own width (m): about one capillary wavelength.
#define RAIN_RING_WIDTH          0.017

/// The lattice is anchored to a world grid this coarse (m), not to the world origin.
///
/// A crest is 17 mm wide but a float carries only about 1 mm of resolution 14 km out, so a
/// lattice built on absolute coordinates degenerates there. MEASURED 2026-09-03, standing
/// at x = 14000: only 45% of the water's pixels carried a ring at all, and their mean
/// strength was 41% of what the same code produced near the origin.
///
/// 256 is exact in binary, so the snapped origin is exact and `posXZ - origin` is exact
/// too. The lattice re-rolls when the camera crosses a boundary; a ring lives one second,
/// so a re-roll is a single frame of an already boiling stipple.
#define RAIN_RING_ORIGIN_STEP    256.0

/// Coefficient that sets a ring's peak surface slope. It is NOT the slope itself: the
/// profile, the 1/r spread and the birth ramp all attenuate it before it reaches the
/// surface, so the number is chosen from what has to come out the far end.
///
/// TARGET. A 1.5 mm drop leaves a crater about 0.5 mm deep; a 0.5 mm crest on the 1.73 cm
/// capillary wavelength is a slope of `2 pi A / lambda` = 0.182, that is 10.5 degrees.
///
/// WHAT THE CHAIN TAKES. `profile` peaks at 0.4288 (`x exp(-x^2)` at `x = 1/sqrt(2)`), and
/// `spread * birth` peaks at 0.469 when the birth ramp has just saturated (age 0.083, the
/// crest one width out). Their product is 0.201, so 0.182 / 0.201 = 0.90.
///
/// MEASURED (2026-09-03): at 0.12 the peak reached the surface as a slope of 0.015 -- 0.9
/// degrees -- and an A/B on identical frames could not separate rings from no rings.
#define RAIN_RING_SLOPE          0.90

/// Folded into a period so the hash keeps its precision far from the world origin.
#define RAIN_RING_HASH_PERIOD    512.0

float2 RainRingHash22(float2 p)
{
    p = fmod(abs(p), RAIN_RING_HASH_PERIOD);

    float3 q = frac(float3(p.x, p.y, p.x + 19.19) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac(float2((q.x + q.y) * q.z, (q.x + q.z) * q.y));
}

float RainRingHash21(float2 p)
{
    p = fmod(abs(p), RAIN_RING_HASH_PERIOD);

    float3 q = frac(float3(p.x, p.y, p.x + 19.19) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.x + q.y) * q.z);
}

/// The world position with `RAIN_RING_ORIGIN_STEP` snapped out of it. Every caller must
/// pass the result of this rather than a raw world position -- see the constant.
float2 RainRingLocal(float2 posXZ, float2 cameraXZ)
{
    return posXZ - floor(cameraXZ / RAIN_RING_ORIGIN_STEP) * RAIN_RING_ORIGIN_STEP;
}

/// How much of the ring this pixel is allowed to see. Below one crest per two pixels the
/// ring is noise, and drawing it there is the aliasing every other scale already fades out.
float RainRingResolvable(float pixelSize)
{
    return saturate(RAIN_RING_WIDTH * 4.0 / max(pixelSize * 2.0, 1e-5) - 1.0);
}

/// The slope the rings add to a water surface. `time` is seconds; `intensity` is 0..1.
float2 RainRings(float2 localXZ, float time, float intensity)
{
    if (intensity <= 0.001) return 0.0;

    // THE CLOCK IS WRAPPED, NOT USED RAW. `age` is `frac(time / life + hash)`, and after an
    // hour of play a float holds only about a millisecond of `time` -- the ring ages in
    // visible steps. 4096 is an exact multiple of the lifetime and exact in binary, so
    // wrapping there changes no ring's phase and hands the division small numbers.
    time = fmod(time, 4096.0);

    // The three cell sizes are spaced by an irrational-ish ratio so their lattices never
    // line up; the ring speed and life are the water's and are shared by all three.
    const float3 cellSize = float3(0.11, 0.19, 0.37);
    const float speed = RAIN_RING_SPEED;
    const float life  = RAIN_RING_LIFE;

    float2 slope = 0.0;

    [unroll]
    for (int layer = 0; layer < 3; ++layer)
    {
        float L = cellSize[layer];
        float2 cell = floor(localXZ / L);

        // Each cell drops once per lifetime, at its own moment and its own spot inside it.
        float2 h = RainRingHash22(cell + float2(layer * 37.1, layer * 71.7));
        float2 centre = (cell + h) * L;

        float age = frac(time / life + RainRingHash21(cell + layer * 13.7));

        float2 d = localXZ - centre;
        float r = length(d);
        if (r < 1e-4) continue;

        // The crest travels outward at the water's own speed.
        float front = age * speed * life;

        // A narrow annulus: the ring is a single crest, not a train.
        float w = RAIN_RING_WIDTH;
        float x = (r - front) / w;
        float profile = x * exp(-x * x);        // odd: a crest with a trough behind it

        // It fades as it spreads -- the same energy on an ever longer circumference -- and
        // it is born rather than appearing, so the first instant does not pop.
        float spread = 1.0 / max(1.0 + front / w, 1.0);
        float birth = saturate(age * 12.0);

        slope += normalize(d) * (profile * spread * birth);
    }

    return slope * (RAIN_RING_SLOPE * intensity);
}

#endif
