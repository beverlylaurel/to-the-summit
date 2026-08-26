// ROL: kar yüzeyinin detay normalleri ve harmanlanması (spec §14.2).
// Çağıran: SnowLitForwardPass, SnowLitDepthNormalsPass.

#ifndef SNOW_DETAIL_NORMALS_INCLUDED
#define SNOW_DETAIL_NORMALS_INCLUDED

TEXTURE2D(_SnowDetailNormal);
SAMPLER(sampler_SnowDetailNormal);

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

/// Detay dokusundan tanjant uzayı EĞİMİ (n.xy / n.z).
/// Eğimler doğrusal toplanır; normaller toplanmaz.
float2 SampleDetailSlope(float2 worldXZ, float tileMeters, float strength)
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
    // NaN eğim toplamından geçince yüzeyin TAMAMI siyah çıkıyor (ölçüldü:
    // dağ kapkara, ayakta kalan tek şey dokusu materyalinde duran kar mesh'i).
    //
    // Bu bir telafi terimi değil: eksik bir referansın bedeli "detay yok"
    // olmalı, "arazi yok" değil.
    if (!all(isfinite(n))) n = float3(0.0, 0.0, 1.0);

    n.xy *= strength;
    n = normalize(n);

    // EĞİM DÖNÜYOR, PAKETLİ NORMAL DEĞİL. Tanjant normalinin eğimi n.xy/n.z;
    // eğimler doğrusal toplanır, normaller toplanmaz.
    return n.xy / max(n.z, 1e-3);
}

/// Dört katman (spec §14.2 tablosu). Kaç tanesinin açık olduğunu kalite
/// preseti belirliyor (spec §15.3).
///
/// MİKRO MESAFEYLE KAPANIYOR. Açık kalırsa TAA ile kaynayan bir yüzey
/// oluşuyor — 16 m'de tamamen kapanmalı.
float3 SnowApplyDetailNormals(float3 normalWS, float3 positionWS,
                              float disturb, float distanceToCamera)
{
    // TABANIN EĞİMİ KORUNUYOR. Detay katmanları taban normalinin ÜSTÜNE
    // eğim olarak biniyor; taban eğimi toplamın içinde aynen duruyor.
    //
    // RNM yeniden yönlendirme yoluyla aynı işi yapmaya çalışıyordu ve
    // ÖLÇÜMDE tabanı koruyamadı: kar izinin oluğu 7.5 cm derindi, taban
    // normali onu görüyordu (N.y 0.80'e iniyor), detaydan sonra N.y her
    // yerde 0.998 kalıyordu. Son görüntüde oluğun kontrastı:
    //   detay devrede    %0.8
    //   taban normali    %10.6
    // Detay şiddeti sıfıra indirilince bile %0.5 — yani sorun şiddet değil,
    // RNM'nin bu bağlamdaki davranışıydı.
    //
    // Eğim toplamı bu tuzağı yapısı gereği kuramaz: detay sıfırsa sonuç
    // tabanın kendisidir.
    float2 tabanEgim = float2(normalWS.x, normalWS.z) / max(normalWS.y, 1e-3);

    float distFade = 1.0 - saturate((distanceToCamera - 6.0) / 10.0);

    // MAKRO KATMAN SİLİNDİ — RÜZGÂR DALGALARINI RÖLYEF ZATEN ÜRETİYOR.
    //
    // 8 metreye gerilmiş bir detay dokusu, işi `SnowYuzeyRolyef`'in fBm
    // (1.25 m), ripple (17 cm) ve sastrugi katmanlarıyla ÇAKIŞIYORDU:
    // aynı ölçek iki kez, biri arazide ölçülmüş verilerden
    // (Filhol & Sturm), öteki fotogrametri dokusundan.
    //
    // Ölçüldü (17:49, bulut 0, 50 cm kar, sabit kadraj — p99 gradyan,
    // yani kenar sertliği):
    //   baz                          22.0
    //   makro kapalı                  3.0
    //   mezo kapalı                  22.0
    //   mikro kapalı                 20.0
    //   detay normali tamamen kapalı  3.0
    // Makro'yu kapatmak tüm detay normalini kapatmakla AYNI sonucu
    // veriyordu: keskin kenarların tamamı bu katmandandı. Belirti
    // alçak güneşte keskin kenarlı adacıklar olarak görülüyordu.
    //
    // Mezo (0.6 m) ve mikro (5 cm) duruyor — yakın plan detayı onlarda,
    // ve ikisi de kenar sertliğine katkı vermiyor.
    float2 detayEgim = (float2)0.0;

#if defined(_SNOW_QUALITY_MEDIUM) || defined(_SNOW_QUALITY_HIGH)
    // Mezo — kar topakları
    detayEgim += SampleDetailSlope(positionWS.xz, 0.6, 0.50);
#endif

#if defined(_SNOW_QUALITY_HIGH)
    // Mikro — kristal detayı
    detayEgim += SampleDetailSlope(positionWS.xz, 0.05, 0.40 * distFade);

    // Ezilmiş — iz içi
    detayEgim += SampleDetailSlope(positionWS.xz, 0.25, disturb * 0.90);
#endif

    float2 toplam = tabanEgim + detayEgim;
    return normalize(float3(toplam.x, 1.0, toplam.y));
}

#endif
