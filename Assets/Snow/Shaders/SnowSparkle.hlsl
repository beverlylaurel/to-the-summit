// ROLE: the snow's sparkle (spec §14.4).
// CALLED BY: SnowLighting.hlsl.

#ifndef SNOW_SPARKLE_INCLUDED
#define SNOW_SPARKLE_INCLUDED

// Tam sayi hash buradan (SnowPcg3d / SnowRandCell3). Include yoktu ve
// SnowTestKernels.compute'un ALTI kerneli birden derlenmedi; dispatch'ler
// sessizce sifir dondu, on ayri sinama 'yanlis sonuc' verdi. Gercek sebep
// tek satirlik eksik include'du.
#include "SnowCommon.hlsl"

/// A SPARKLE THAT DOES NOT FLICKER WITH DISTANCE
/// [SOURCE: Bowles & Wang, "Sparkly but not too Sparkly!", SIGGRAPH 2015].
///
/// A naive sparkle flickers horribly at distance: hundreds of crystals fall inside a
/// single pixel and which of them flashes changes frame to frame. The answer is to keep
/// the density constant IN SCREEN SPACE — the cell size is LODed against the pixel
/// footprint, and the threshold loosens by the same ratio.

/// THE SAME COLLAPSE WAS HERE TOO. The cell is `floor(posWS.xz / cellSize)`; on a 6000 m
/// mountain with a millimetre cell the input goes into the millions and `frac(sin(...))`
/// produces a repeating value there. An integer hash has no such limit
/// (`SnowCommon.hlsl` → `SnowPcg3d`).
float3 SnowHash33(int3 cell)
{
    return SnowRandCell3(cell);
}

/// The micro normal of one crystal cell. Those facing down are turned up —
/// a crystal facing into the surface cannot flash.
float3 SparkleCellNormal(float2 cell)
{
    float3 r = SnowHash33(int3((int2)cell, 17));
    float3 n = normalize(r * 2.0 - 1.0);
    return (n.y < 0) ? float3(n.x, -n.y, n.z) : n;
}

half SnowSparkle(float3 posWS, float3 V, float3 L, float pixelFootprint)
{
    float3 H = normalize(V + L);

    // WHAT IS CLAMPED IS NOT THE LOD BUT THE FOOTPRINT.
    //
    // Bowles & Wang grow the cell with the pixel footprint and keep the density
    // constant in screen space. At one point the LOD was limited to two levels, because
    // LARGE RECTANGULAR patches appeared at distance. That limit closed the symptom but
    // killed the sparkle too: with the cell unable to grow, `cellsPerPixel` inflates,
    // `pTarget` falls to zero and the threshold presses against 1 — no crystal flashes
    // at distance at all (the user reported it: "it only shows very close up").
    //
    // The rectangles' real cause is the footprint itself: `fwidth(posWS.xz)` explodes at
    // a grazing angle, the cell becomes metres wide and a single cell covers dozens of
    // pixels. With a ceiling on the footprint the cell stays a few centimetres at most —
    // one or two pixels at 30 m, i.e. a sparkle rather than a patch.
    float fp = clamp(pixelFootprint, _SparkleCellSize, SNOW_SPARKLE_MAX_FOOTPRINT);
    float lodF = max(log2(fp / _SparkleCellSize), 0.0);
    int   l0 = (int)floor(lodF);
    float f  = lodF - l0;

    half acc = 0;

    [unroll]
    for (int k = 0; k < 2; ++k)
    {
        float cellSize = _SparkleCellSize * exp2(l0 + k);
        float3 nMicro  = SparkleCellNormal(floor(posWS.xz / cellSize));

        float cellsPerPixel = max(pow(fp / cellSize, 2.0), 1.0);
        float pTarget = saturate(_SparkleDensity / cellsPerPixel);
        float thr = 1.0 - 2.0 * pTarget;

        half v = (half)saturate((dot(H, nMicro) - thr) / max(1.0 - thr, 1e-4));
        acc += lerp(1.0 - f, f, (half)k) * pow(v, _SparkleSharpness);
    }

    return acc;
}

#endif
