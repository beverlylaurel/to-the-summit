#ifndef TOTHESUMMIT_STOCHASTIC_TILING_INCLUDED
#define TOTHESUMMIT_STOCHASTIC_TILING_INCLUDED

// HEITZ-NEYRET STOKASTİK DÖŞEME.
//
// Sorun: doku metrelerce döşenince desen tekrar eder ve ızgara olarak okunur. Basit
// harmanlama tekrarı zayıflatır ama VARYANSI da düşürür — iki örneğin ortalaması
// yarı kontrast demek, doku bulanıklaşır.
//
// Yöntem: düzlem altıgen ızgaraya bölünür, her piksel üç köşeye ait olur. Her köşe
// dokuyu KENDİ rastgele kaymasıyla örnekler; üç örnek baryantrik ağırlıkla harmanlanır.
// Kayma her hücrede farklı olduğu için tekrar periyodu ortadan kalkar.
//
// Kontrast korunuyor çünkü doku ÖN İŞLEMDE Gauss histogramına dönüştürüldü
// (StochasticTextureBaker). Gauss değişkenlerin ağırlıklı toplamı yine Gauss;
// ağırlıklar karelerinin toplamına bölünerek varyans birde tutuluyor. Sonuç ters
// LUT'tan geçirilip özgün histograma dönüyor.

/// Altıgen ızgara: UV'yi üç köşeye ve baryantrik ağırlıklara ayırır.
void StochasticHexGrid(float2 uv, out float2 vertex1, out float2 vertex2,
                       out float2 vertex3, out float3 weights)
{
    // Eşkenar üçgen ızgarasına eğrilmiş koordinat. 1.7320508 = kök 3.
    const float2x2 gridToSkewed = float2x2(1.0, 0.0, -0.57735027, 1.15470054);
    float2 skewed = mul(gridToSkewed, uv * 3.4641016);

    float2 baseId = floor(skewed);
    float3 temp = float3(frac(skewed), 0.0);
    temp.z = 1.0 - temp.x - temp.y;

    // Üçgenin hangi yarısında olduğumuza göre köşeler ve ağırlıklar.
    if (temp.z > 0.0)
    {
        weights = float3(temp.z, temp.y, temp.x);
        vertex1 = baseId;
        vertex2 = baseId + float2(0.0, 1.0);
        vertex3 = baseId + float2(1.0, 0.0);
    }
    else
    {
        weights = float3(-temp.z, 1.0 - temp.y, 1.0 - temp.x);
        vertex1 = baseId + float2(1.0, 1.0);
        vertex2 = baseId + float2(1.0, 0.0);
        vertex3 = baseId + float2(0.0, 1.0);
    }
}

/// Hücre başına rastgele kayma. Aynı hücre her zaman aynı kaymayı alır: desen
/// kararlı, kamera oynayınca kaynamıyor.
float2 StochasticHash(float2 cell)
{
    const float2x2 mixer = float2x2(127.1, 311.7, 269.5, 183.3);
    return frac(sin(mul(mixer, cell)) * 43758.5453);
}

/// Üç örnekli stokastik okuma. `texture`/`samplerState` Gauss dönüşümlü doku,
/// `lut` ters dönüşüm tablosu.
///
/// Türevler ELLE geçiriliyor: her örnek farklı kaymadan okunduğu için donanımın
/// hesapladığı türev hücre sınırında sıçrıyor ve mip seviyesi bir piksellik
/// çizgiler hâlinde atlıyordu.
///
/// Dokular TEXTURE2D_PARAM ile alınıyor: `TEXTURE2D(x)` parametre listesinde
/// BİLDİRİM üretir, parametre değil — doku fonksiyona hiç geçmez.
float4 SampleStochastic(TEXTURE2D_PARAM(tex, samplerState),
                        TEXTURE2D_PARAM(lut, lutSampler),
                        float2 uv, float2 ddxUV, float2 ddyUV)
{
    float2 vertex1, vertex2, vertex3;
    float3 weights;
    StochasticHexGrid(uv, vertex1, vertex2, vertex3, weights);

    float4 sample1 = SAMPLE_TEXTURE2D_GRAD(tex, samplerState,
                                           uv + StochasticHash(vertex1), ddxUV, ddyUV);
    float4 sample2 = SAMPLE_TEXTURE2D_GRAD(tex, samplerState,
                                           uv + StochasticHash(vertex2), ddxUV, ddyUV);
    float4 sample3 = SAMPLE_TEXTURE2D_GRAD(tex, samplerState,
                                           uv + StochasticHash(vertex3), ddxUV, ddyUV);

    // VARYANS KORUMA: Gauss örneklerin ağırlıklı toplamının standart sapması
    // ağırlıkların karekök toplamı kadar küçülür. Ona bölünce dağılım birde kalıyor;
    // bu adım atlanırsa harman yine bulanıklaşır ve bütün yöntem anlamsızlaşır.
    float4 mixed = weights.x * sample1 + weights.y * sample2 + weights.z * sample3;
    mixed = (mixed - 0.5) / length(weights) + 0.5;

    // Ters LUT: Gauss uzayından özgün histograma. Kanal başına ayrı okuma —
    // dönüşüm de kanal başına yapılmıştı.
    float4 result;
    result.r = SAMPLE_TEXTURE2D_LOD(lut, lutSampler, float2(saturate(mixed.r), 0.5), 0).r;
    result.g = SAMPLE_TEXTURE2D_LOD(lut, lutSampler, float2(saturate(mixed.g), 0.5), 0).g;
    result.b = SAMPLE_TEXTURE2D_LOD(lut, lutSampler, float2(saturate(mixed.b), 0.5), 0).b;
    result.a = 1.0;
    return result;
}

#endif
