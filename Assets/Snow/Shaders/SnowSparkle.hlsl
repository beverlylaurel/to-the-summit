// ROL: karın parıltısı (spec §14.4).
// Çağıran: SnowLighting.hlsl.

#ifndef SNOW_SPARKLE_INCLUDED
#define SNOW_SPARKLE_INCLUDED

// Tam sayi hash buradan (SnowPcg3d / SnowRandCell3). Include yoktu ve
// SnowTestKernels.compute'un ALTI kerneli birden derlenmedi; dispatch'ler
// sessizce sifir dondu, on ayri sinama 'yanlis sonuc' verdi. Gercek sebep
// tek satirlik eksik include'du.
#include "SnowCommon.hlsl"

/// MESAFEDE TİTREMEYEN PARILTI
/// [KAYNAK: Bowles & Wang, "Sparkly but not too Sparkly!", SIGGRAPH 2015].
///
/// Naif parıltı uzakta korkunç titrer: bir pikselin içine yüzlerce kristal
/// düşer ve hangisinin parladığı kare kare değişir. Çözüm yoğunluğu EKRAN
/// UZAYINDA sabit tutmak — hücre boyu piksel ayak izine göre LOD'lanıyor,
/// eşik de aynı oranda gevşiyor.

/// AYNI ÇÖKME BURADA DA VARDI. Hücre `floor(posWS.xz / cellSize)`; 6000 m'lik
/// dağda ve milimetrik hücrede girdi milyonlara çıkıyor, `frac(sin(...))`
/// orada tekrar eden değer üretiyor. Tam sayı hash'te bu sınır yok
/// (`SnowCommon.hlsl` → `SnowPcg3d`).
float3 SnowHash33(int3 cell)
{
    return SnowRandCell3(cell);
}

/// Bir kristal hücresinin mikro normali. Aşağı bakanlar yukarı çevriliyor —
/// yüzeyin altına bakan bir kristal parlayamaz.
float3 SparkleCellNormal(float2 cell)
{
    float3 r = SnowHash33(int3((int2)cell, 17));
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
