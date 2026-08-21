#ifndef SNOW_DETAIL_NORMALS_INCLUDED
#define SNOW_DETAIL_NORMALS_INCLUDED

// ROL: kar yüzeyinin dört detay normal katmanı (§8.2).
// Katmanlar prosedürel: spec bir doku adı vermiyor ve dokuya bağlanmak hem asset
// hem tekrar deseni getirirdi.
// Çağıran: SnowLighting.hlsl.

#include "SnowSparkle.hlsl"   // SnowHash33

/// Değer gürültüsü. Tamsayı hash üstüne kübik yumuşatma.
float SnowValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);

    f = f * f * (3.0 - 2.0 * f);

    float a = SnowHash33(float3(i + float2(0, 0), 3.7)).x;
    float b = SnowHash33(float3(i + float2(1, 0), 3.7)).x;
    float c = SnowHash33(float3(i + float2(0, 1), 3.7)).x;
    float d = SnowHash33(float3(i + float2(1, 1), 3.7)).x;

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// Bir katmanın Y-yukarı normali. `tile` metre cinsinden dalga boyu.
float3 SnowDetailLayer(float2 worldXZ, float tile, float strength)
{
    if (strength <= 0.0001) return float3(0, 1, 0);

    float2 p = worldXZ / tile;

    // Örnekleme aralığı dalga boyunun onda biri: daha küçüğü hash'in kendi
    // gürültüsünü türev olarak okur, daha büyüğü şekli düzleştirir.
    const float e = 0.1;

    float h  = SnowValueNoise(p);
    float hx = SnowValueNoise(p + float2(e, 0));
    float hy = SnowValueNoise(p + float2(0, e));

    // Eğim = yükseklik farkı / mesafe. Mesafe metre cinsinden: e * tile.
    float2 slope = float2(hx - h, hy - h) / (e * tile) * strength;

    return normalize(float3(-slope.x, 1.0, -slope.y));
}

/// Taban normalinin etrafına oturtma. Detay Y-yukarı üretiliyor; taban normalinden
/// bir dik çatı kurulup detay ona döndürülüyor.
///
/// BASİT TOPLAMA KULLANILMIYOR (§8.2): normalize edilmemiş sonuç ışıklandırmayı
/// bozuyor ve dik yüzeylerde detay tamamen kayboluyor.
float3 SnowReorient(float3 baseN, float3 detailN)
{
    float3 up = abs(baseN.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
    float3 t = normalize(cross(up, baseN));
    float3 b = cross(baseN, t);

    return normalize(t * detailN.x + baseN * detailN.y + b * detailN.z);
}

/// Dört katman: makro rüzgâr dalgaları, meso topaklar, mikro kristal, ezilmiş kar.
///
/// MİKRO KATMAN MESAFEDE KAPANIYOR. Açık bırakılırsa TAA ile kaynayan bir yüzey
/// oluşuyor: dalga boyu 5 cm, 16 m ötede bir pikselin altına düşüyor.
float3 SnowDetailNormal(float3 baseN, float3 posWS, float freshness, float disturb,
                        float viewDistance, float windStrength)
{
    float3 n = baseN;

#if defined(_SNOW_QUALITY_LOW)
    n = SnowReorient(n, SnowDetailLayer(posWS.xz, 0.6, 0.5));
    return n;
#else

    // Makro: rüzgâr dalgaları ve birikinti dalgalanması.
    n = SnowReorient(n, SnowDetailLayer(posWS.xz, 8.0, 0.35 * freshness * windStrength));

    // Meso: kar topakları.
    n = SnowReorient(n, SnowDetailLayer(posWS.xz, 0.6, 0.5));

#if defined(_SNOW_QUALITY_HIGH)
    float distanceFade = 1.0 - saturate((viewDistance - 6.0) / 10.0);

    // Mikro: kristal detayı.
    n = SnowReorient(n, SnowDetailLayer(posWS.xz, 0.05, 0.4 * distanceFade));

    // Ezilmiş kar: yalnız izin içinde.
    n = SnowReorient(n, SnowDetailLayer(posWS.xz, 0.25, disturb * 0.9));
#endif

    return n;
#endif
}

#endif
