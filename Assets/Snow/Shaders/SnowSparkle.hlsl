// ROL: karın parıltısı (spec §14.4).
// Çağıran: SnowLighting.hlsl.

#ifndef SNOW_SPARKLE_INCLUDED
#define SNOW_SPARKLE_INCLUDED

/// MESAFEDE TİTREMEYEN PARILTI
/// [KAYNAK: Bowles & Wang, "Sparkly but not too Sparkly!", SIGGRAPH 2015].
///
/// Naif parıltı uzakta korkunç titrer: bir pikselin içine yüzlerce kristal
/// düşer ve hangisinin parladığı kare kare değişir. Çözüm yoğunluğu EKRAN
/// UZAYINDA sabit tutmak — hücre boyu piksel ayak izine göre LOD'lanıyor,
/// eşik de aynı oranda gevşiyor.

float3 SnowHash33(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));

    return frac(sin(p) * 43758.5453123);
}

/// Bir kristal hücresinin mikro normali. Aşağı bakanlar yukarı çevriliyor —
/// yüzeyin altına bakan bir kristal parlayamaz.
float3 SparkleCellNormal(float2 cell)
{
    float3 r = SnowHash33(float3(cell, 17.0));
    float3 n = normalize(r * 2.0 - 1.0);
    return (n.y < 0) ? float3(n.x, -n.y, n.z) : n;
}

half SnowSparkle(float3 posWS, float3 V, float3 L, float pixelFootprint)
{
    float3 H = normalize(V + L);

    float lodF = max(log2(max(pixelFootprint / _SparkleCellSize, 1e-5)), 0.0);
    int   l0 = (int)floor(lodF);
    float f  = lodF - l0;

    half acc = 0;

    [unroll]
    for (int k = 0; k < 2; ++k)
    {
        float cellSize = _SparkleCellSize * exp2(l0 + k);
        float3 nMicro  = SparkleCellNormal(floor(posWS.xz / cellSize));

        float cellsPerPixel = max(pow(pixelFootprint / cellSize, 2.0), 1.0);
        float pTarget = saturate(_SparkleDensity / cellsPerPixel);
        float thr = 1.0 - 2.0 * pTarget;

        half v = (half)saturate((dot(H, nMicro) - thr) / max(1.0 - thr, 1e-4));
        acc += lerp(1.0 - f, f, (half)k) * pow(v, _SparkleSharpness);
    }

    return acc;
}

#endif
