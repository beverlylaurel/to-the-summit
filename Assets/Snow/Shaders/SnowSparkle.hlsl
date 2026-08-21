#ifndef SNOW_SPARKLE_INCLUDED
#define SNOW_SPARKLE_INCLUDED

// ROL: kenar yumuşatmalı kar parıltısı (§8.4).
// Yöntem: Bowles & Wang, "Sparkly but not too Sparkly!", SIGGRAPH 2015 Advances.
// Çağıran: SnowLighting.hlsl.

/// Tamsayı hash. SİNÜS TABANLI HASH KULLANILMIYOR — bu projede bir kez ölçüldü:
/// sinüs hash'i GPU'ya göre farklı yuvarlanıyor ve görünür bir ızgara deseni bırakıyor.
float3 SnowHash33(float3 p)
{
    uint3 q = uint3(int3(p)) * uint3(1597334673u, 3812015801u, 2798796415u);
    q = (q.x ^ q.y ^ q.z) * uint3(1597334673u, 3812015801u, 2798796415u);

    return float3(q) * (1.0 / float(0xffffffffu));
}

/// Bir hücrenin rastgele mikro-normali. Üst yarıküreye zorlanıyor: aşağı bakan bir
/// kristal yüzü zaten görünmez.
float3 SnowSparkleCellNormal(float2 cell)
{
    float3 r = SnowHash33(float3(cell, 17.0));
    float3 n = normalize(r * 2.0 - 1.0);

    return (n.y < 0) ? float3(n.x, -n.y, n.z) : n;
}

/// PARILTI YOĞUNLUĞU EKRAN UZAYINDA SABİT, eşik LOD ile ayarlanıyor.
///
/// Naif parıltı (`pow(dot(H, noiseNormal), 200)`) mesafede korkunç titrer: piksel
/// başına düşen hücre sayısı arttıkça ya hepsi söner ya hepsi yanar. Burada olasılık
/// hedefi piksel başına hücre sayısına bölünüyor, yani ekranda görünen parıltı sayısı
/// mesafeden bağımsız kalıyor.
///
/// İki LOD arasında geçiş yapılıyor; tek LOD kullanılırsa seviye atlarken parıltılar
/// topluca yer değiştirir.
half SnowSparkle(float3 posWS, float3 N, float3 V, float3 L, float pixelFootprint,
                 float cellSize, float density, float sharpness)
{
    float3 H = normalize(V + L);

    float lodF = max(log2(max(pixelFootprint / cellSize, 1e-5)), 0.0);
    int   l0   = (int)floor(lodF);
    float f    = lodF - l0;

    half acc = 0;

    [unroll] for (int k = 0; k < 2; ++k)
    {
        float layerSize = cellSize * exp2(l0 + k);
        float2 cell     = floor(posWS.xz / layerSize);
        float3 nMicro   = SnowSparkleCellNormal(cell);

        float cellsPerPixel = max(pow(pixelFootprint / layerSize, 2.0), 1.0);
        float pTarget       = saturate(density / cellsPerPixel);

        // Rastgele bir normalin H'ye pTarget olasılıkla yakın olması için koni kosinüsü.
        float thr = 1.0 - 2.0 * pTarget;

        float d = dot(H, nMicro);
        half  v = (half)saturate((d - thr) / max(1.0 - thr, 1e-4));

        acc += lerp(1.0 - f, f, (half)k) * pow(v, sharpness);
    }

    return acc;
}

#endif
