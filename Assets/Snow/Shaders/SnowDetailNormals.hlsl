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
///
/// GİRDİ DE ÇIKTI DA PAKETLİ (0..1). Formülün kendisi paketlenmemiş bir
/// normal üretiyor; sonuç tekrar paketleniyor ki katmanlar zincirlenebilsin.
///
/// Eksikken: her katman bir öncekinin çıktısını paketli sanıp `*2-1`
/// uyguluyordu. Düz bir yüzeyde bile (0,0,1) → (-1,-1,1) oluyor ve normal
/// deviriliyordu. Ölçüldü — kar yüzeyinde N.y:
///   detay yok  0.997   makro sonrası  0.565   mezo sonrası  0.042
/// Kâğıttaki değerler 1/√3 = 0.577 ve 0.051; ölçümle birebir.
/// Belirti: oyuncunun çevresinde şekilsiz düz bir levha — görünür KARE.
float3 RNMBlend(float3 baseSample, float3 detailSample)
{
    float3 t = baseSample   * float3( 2,  2,  2) + float3(-1, -1,  0);
    float3 u = detailSample * float3(-2, -2,  2) + float3( 1,  1, -1);
    return normalize(t * dot(t, u) / t.z - u) * 0.5 + 0.5;
}

/// STOKASTİK DÖŞEME [KAYNAK: Heitz & Neyret, "High-Performance By-Example
/// Noise using a Histogram-Preserving Blending Operator", HPG 2018].
///
/// TEK DOKU HER YERDE AYNI DESENİ BASIYOR. Dört katman da aynı 256²
/// dokuyu okuyor; 0.6 m'de tekrarlanınca göz ızgarayı yakalıyor ve yukarıdan
/// bakınca yüzey kareli görünüyor (ölçüldü — kullanıcı ekran görüntüsü).
///
/// Çözüm: dünya üçgen ızgaraya bölünüyor, her hücreye kendi rastgele KAYDIRMASI
/// veriliyor ve üç komşu hücrenin örneği barisentrik ağırlıkla harmanlanıyor.
/// Desen aynı, ama hiçbir yerde hizalanmıyor.
///
/// DÖNDÜRME YOK, YALNIZ KAYDIRMA. Normal haritasını döndürmek teğet uzaydaki
/// XY'yi de döndürmeyi gerektirir; atlanırsa ışık yanlış yönden gelir.
/// Kaydırma tek başına ızgarayı kırıyor.
///
/// Spec §13.2'nin döşeme boyları ve şiddetleri DEĞİŞMİYOR — yalnız örnekleme
/// değişiyor.

/// Üçgen ızgara koordinatları: UV'yi eşkenar üçgen hücrelere böler, üç köşenin
/// hücre kimliğini ve barisentrik ağırlıklarını döndürür.
void SnowTriangleGrid(float2 uv, out float w1, out float w2, out float w3,
                      out int2 v1, out int2 v2, out int2 v3)
{
    // Eşkenar üçgen ızgarasına dönüşüm. 1.7320508 = sqrt(3).
    const float2x2 gridToSkewed = float2x2(1.0, 0.0, -0.57735027, 1.15470054);

    float2 skewed = mul(gridToSkewed, uv * 3.4641016);   // 2*sqrt(3)
    int2 baseId = int2(floor(skewed));
    float2 f = frac(skewed);

    float3 temp = float3(f.x, f.y, 1.0 - f.x - f.y);

    if (temp.z > 0.0)
    {
        w1 = temp.z; w2 = temp.y; w3 = temp.x;
        v1 = baseId;
        v2 = baseId + int2(0, 1);
        v3 = baseId + int2(1, 0);
    }
    else
    {
        w1 = -temp.z; w2 = 1.0 - temp.y; w3 = 1.0 - temp.x;
        v1 = baseId + int2(1, 1);
        v2 = baseId + int2(1, 0);
        v3 = baseId + int2(0, 1);
    }
}

/// Hücre kimliğinden kaydırma. PCG3D — `frac(sin(dot()))` büyük indekste
/// çöküyor (spec §17.1'de aynı sebeple değiştirilmişti).
float2 SnowCellOffset(int2 cell)
{
    return SnowRandU3(uint3(asuint(cell.x), asuint(cell.y), 0x9E3779B9u)).xy;
}

/// Detay dokusundan tanjant uzayı normali, 0..1 aralığına geri paketlenmiş
/// hâlde. `RNMBlend` girdilerini bu aralıkta bekliyor.
float3 SampleDetailPacked(float2 worldXZ, float tileMeters, float strength)
{
    float2 uv = worldXZ / max(tileMeters, 1e-3);

    float w1, w2, w3;
    int2 v1, v2, v3;
    SnowTriangleGrid(uv, w1, w2, w3, v1, v2, v3);

    // TÜREVLER HÜCRE KAYDIRMASINDAN ÖNCE ALINIYOR. Kaydırma hücre sınırında
    // sıçradığı için `SAMPLE_TEXTURE2D` kendi türevini kullanırsa orada mip
    // patlar ve dikişte bulanık bir çizgi kalır.
    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    float4 c1 = SAMPLE_TEXTURE2D_GRAD(_SnowDetailNormal, sampler_SnowDetailNormal,
                                      uv + SnowCellOffset(v1), dx, dy);
    float4 c2 = SAMPLE_TEXTURE2D_GRAD(_SnowDetailNormal, sampler_SnowDetailNormal,
                                      uv + SnowCellOffset(v2), dx, dy);
    float4 c3 = SAMPLE_TEXTURE2D_GRAD(_SnowDetailNormal, sampler_SnowDetailNormal,
                                      uv + SnowCellOffset(v3), dx, dy);

    // HARMAN DOĞRUSAL, HİSTOGRAM KORUYUCU DEĞİL. Heitz'in histogram operatörü
    // ALBEDO için; normal haritasında üç örneğin doğrusal ortalaması zaten
    // doğru eğimi veriyor (normaller türev, türev doğrusal toplanır).
    float3 n = UnpackNormal(c1 * w1 + c2 * w2 + c3 * w3);

    // BAĞLANMAMIŞ DOKU NaN ÜRETİYOR. Unity bağlanmamış bir sampler'a beyaz
    // veriyor; DXT5nm yolunda `UnpackNormal(beyaz)` → `sqrt(1 - 1 - 1)` → NaN.
    // NaN `RNMBlend`'den geçince yüzeyin TAMAMI siyah çıkıyor (ölçüldü: dağ
    // kapkara, ayakta kalan tek şey dokusu materyalinde duran kar mesh'i).
    //
    // Bu bir telafi terimi değil: eksik bir referansın bedeli "detay yok"
    // olmalı, "arazi yok" değil.
    if (!all(isfinite(n))) n = float3(0.0, 0.0, 1.0);

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
        return TangentPackedToWorldNormal(SampleDetailPacked(positionWS.xz, 8.0, 0.35 * freshness));

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
