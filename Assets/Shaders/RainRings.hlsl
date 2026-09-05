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
/// Intensity primarily controls HOW MANY drop events survive. Scaling only the final
/// slope leaves the same frantic number of circles in drizzle, merely fainter.
float2 RainRingCell(float2 localXZ, float time, float intensity,
                    int layer, float cellSize, float2 cell)
{
    // A pixel evaluates its own cell and the one across its nearest X/Y boundary. To make
    // that four-cell footprint exact, a ring must be zero before half a cell: at the middle
    // of a cell the omitted cell on the opposite side is at least 0.5 L away. The small gap
    // is the smooth support band, not a hard clip.
    float support = cellSize * 0.45;
    float maxFront = max(RAIN_RING_WIDTH, support - RAIN_RING_WIDTH * 2.0);
    float life = min(RAIN_RING_LIFE, maxFront / RAIN_RING_SPEED);

    float eventClock = time / life + RainRingHash21(cell + layer * 13.7);
    float eventCycle = fmod(floor(eventClock), 4096.0);
    float age01 = frac(eventClock);
    float age = age01 * life;

    // A cell is only an event budget, not a permanent drop location. Reusing one stable
    // cell hash made every cycle land at the same point with the same strength. The cycle
    // salt re-rolls those properties only when the previous event wraps; temporalTail has
    // already brought that event to zero, so the new centre cannot pop while visible.
    float2 eventSalt = float2(eventCycle * 17.17, eventCycle * 43.13);
    float2 eventKey = cell + eventSalt;
    float2 h = RainRingHash22(eventKey + float2(layer * 37.1, layer * 71.7));
    float2 centre = (cell + h) * cellSize;

    // Rain amount is chiefly a drop ARRIVAL RATE. Each cell has a stable rank;
    // drizzle admits only the lowest-ranked events, while a downpour admits all. Rank and
    // impact strength belong to this event, so neither repeats with the cell.
    float eventRank = RainRingHash21(eventKey + layer * 19.7 + 103.5);
    float eventWeight = smoothstep(eventRank, min(eventRank + 0.03, 1.0), intensity);
    if (eventWeight <= 0.001) return 0.0;
    float impactStrength = lerp(0.75, 1.25,
        RainRingHash21(eventKey + layer * 29.3 + 211.7));

    float2 d = localXZ - centre;
    float r = length(d);
    if (r < 1e-4) return 0.0;

    float front = age * RAIN_RING_SPEED;
    float w = RAIN_RING_WIDTH;
    float x = (r - front) / w;
    float profile = x * exp(-x * x);

    float spread = 1.0 / max(1.0 + front / w, 1.0);
    float birth = saturate(age * 12.0);

    // Both ends are compact and smooth. `temporalTail` makes the old event reach zero before
    // its phase wraps to the newborn event; `spatialSupport` makes cells outside the selected
    // four provably irrelevant. There is therefore no cell edge at which a non-zero normal can
    // be cut into the square seen in the game.
    float temporalTail = 1.0 - smoothstep(max(life - 0.08, life * 0.65), life, age);
    float spatialSupport = 1.0 - smoothstep(support - w, support, r);

    return normalize(d) * (profile * spread * birth * temporalTail
                          * spatialSupport * eventWeight * impactStrength);
}

float2 RainRings(float2 localXZ, float time, float intensity)
{
    if (intensity <= 0.001) return 0.0;

    // The three cell sizes are deliberately incommensurate enough that their lattices do not
    // line up. They are larger than the old cells because each cell now represents one compact
    // event whose full visible support fits inside the four-candidate neighbourhood.
    const float3 cellSize = float3(0.22, 0.34, 0.52);

    float2 slope = 0.0;

    [unroll]
    for (int layer = 0; layer < 3; ++layer)
    {
        float L = cellSize[layer];
        float2 grid = localXZ / L;
        float2 cell = floor(grid);

        // Pick the neighbour across the nearest boundary on each axis. With support below
        // 0.5 L these are the only four cells that can affect the pixel. Crucially the SAME
        // neighbour's ring is evaluated from both sides of a cell boundary, so the circular
        // normal continues instead of being clipped to a square.
        float2 side = step(0.5, frac(grid)) * 2.0 - 1.0;
        slope += RainRingCell(localXZ, time, intensity, layer, L, cell);
        slope += RainRingCell(localXZ, time, intensity, layer, L,
                              cell + float2(side.x, 0.0));
        slope += RainRingCell(localXZ, time, intensity, layer, L,
                              cell + float2(0.0, side.y));
        slope += RainRingCell(localXZ, time, intensity, layer, L, cell + side);
    }

    // Individual drops remain readable in drizzle; heavier rain also carries somewhat
    // larger drops. Density supplies the main intensity response, this modest gain the rest.
    float dropStrength = lerp(0.65, 1.0, saturate(intensity));
    return slope * (RAIN_RING_SLOPE * dropStrength);
}

#endif
