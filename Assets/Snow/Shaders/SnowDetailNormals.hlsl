// ROL: kar yüzeyinin detay normalleri ve harmanlanması (spec §14.2).
// Çağıran: SnowLitForwardPass, SnowLitDepthNormalsPass.

#ifndef SNOW_DETAIL_NORMALS_INCLUDED
#define SNOW_DETAIL_NORMALS_INCLUDED

TEXTURE2D(_SnowDetailNormal);
SAMPLER(sampler_SnowDetailNormal);

/// REORIENTED NORMAL MAPPING
/// [KAYNAK: Barré-Brisebois & Hill 2012; Batman GDC 2014'te birebir].
///
/// NORMAL'LER RENK DEĞİLDİR. `lerp` veya overlay ile harmanlanmaz — ikisi de
/// yüzeyin eğimini yanlış toplar ve detay ya kaybolur ya da abartılır
/// (spec §22).
float3 RNMBlend(float3 baseSample, float3 detailSample)
{
    float3 t = baseSample   * float3( 2,  2,  2) + float3(-1, -1,  0);
    float3 u = detailSample * float3(-2, -2,  2) + float3( 1,  1, -1);
    return normalize(t * dot(t, u) / t.z - u);
}

/// Detay dokusundan tanjant uzayı normali, 0..1 aralığına geri paketlenmiş
/// hâlde. `RNMBlend` girdilerini bu aralıkta bekliyor.
float3 SampleDetailPacked(float2 worldXZ, float tileMeters, float strength)
{
    float3 n = UnpackNormal(SAMPLE_TEXTURE2D(_SnowDetailNormal, sampler_SnowDetailNormal,
                                             worldXZ / max(tileMeters, 1e-3)));

    n.xy *= strength;
    n = normalize(n);

    return n * 0.5 + 0.5;
}

/// TANJANT ÇERÇEVESİ DÜNYA HİZALI. Kar yüzeyi yataya yakın ve mesh'in kendi
/// tanjantı yok; çerçeve +Y yukarı olacak şekilde sabitleniyor. Dünya
/// normalinin tanjant uzayındaki karşılığı basit bir eksen takası:
/// (x, y, z)_dünya → (x, z, y)_tanjant.
float3 WorldNormalToTangentPacked(float3 n)
{
    return float3(n.x, n.z, n.y) * 0.5 + 0.5;
}

float3 TangentPackedToWorldNormal(float3 packed)
{
    float3 n = packed * 2.0 - 1.0;
    return normalize(float3(n.x, n.z, n.y));
}

/// Dört katman (spec §14.2 tablosu). Kaç tanesinin açık olduğunu kalite
/// preseti belirliyor (spec §15.3).
///
/// MİKRO MESAFEYLE KAPANIYOR. Açık kalırsa TAA ile kaynayan bir yüzey
/// oluşuyor — 16 m'de tamamen kapanmalı.
float3 SnowApplyDetailNormals(float3 normalWS, float3 positionWS, float freshness,
                              float disturb, float distanceToCamera)
{
    float3 packed = WorldNormalToTangentPacked(normalWS);

    float distFade = 1.0 - saturate((distanceToCamera - 6.0) / 10.0);

    // Makro — rüzgâr dalgaları
    packed = RNMBlend(packed, SampleDetailPacked(positionWS.xz, 8.0, 0.35 * freshness));

#if defined(_SNOW_QUALITY_MEDIUM) || defined(_SNOW_QUALITY_HIGH)
    // Mezo — kar topakları
    packed = RNMBlend(packed, SampleDetailPacked(positionWS.xz, 0.6, 0.50));
#endif

#if defined(_SNOW_QUALITY_HIGH)
    // Mikro — kristal detayı
    packed = RNMBlend(packed, SampleDetailPacked(positionWS.xz, 0.05, 0.40 * distFade));

    // Ezilmiş — iz içi
    packed = RNMBlend(packed, SampleDetailPacked(positionWS.xz, 0.25, disturb * 0.90));
#endif

    return TangentPackedToWorldNormal(packed);
}

#endif
