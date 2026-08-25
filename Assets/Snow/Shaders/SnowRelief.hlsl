// ROL: kar izini ARAZİNİN KENDİ yüzeyinde sanal derinlik olarak çizer.
// Çağıran: MountainSurface.hlsl.

#ifndef SNOW_RELIEF_INCLUDED
#define SNOW_RELIEF_INCLUDED

#include "SnowCommon.hlsl"

/// İZ İKİNCİ BİR YÜZEYLE DEĞİL, RELIEF MAPPING'LE ÇİZİLİYOR.
///
/// Önceki mimaride iz, arazinin üstüne oturan 24 m'lik ayrı bir mesh'ti. O
/// yamanın nereye konduğu fark etmiyordu: araziyle aynı kotta olunca karakter
/// gömülüyor, yukarı çıkınca kenarı KARE olarak görünüyordu. Üç gün boyunca
/// kapatılan belirtilerin hepsi o sınırın kendisiydi.
///
/// [KAYNAK: Colin Barré-Brisebois, "Deformable Snow Rendering in Batman:
/// Arkham Origins", GDC 2014.] Sevkiyat çözümü ikinci yüzey kurmuyor: aynı
/// yüzeyi ya tessellate ediyor (PC) ya da relief mapping ile SANAL derinlik
/// veriyor (konsol) — "üçgen yoğunluğundan bağımsız", "yarı-düşük frekanslı
/// detay". Rise of the Tomb Raider de aynı yerde duruyor: deformasyon
/// arazinin kendi geometrisine uygulanıyor.
///
/// Bizde arazi bir Unity Terrain; köşe yoğunluğu 7.3 m, yani tessellation
/// yolu kapalı. Geriye konsol yolu kalıyor ve ayak izi tam olarak onun
/// tarif ettiği detay sınıfı.
///
/// SINIR YOK: çizen shader tek, yüzey tek, collider aynı yüzey.

/// Çukurun DERİNLİĞİ, metre. Pozitif = aşağı.
///
/// Tek örnekleme: ışın yürüyüşü adım başına bir okuma yapıyor, yumuşatılmış
/// `SnowSurfaceAt` (beş okuma) buraya konsaydı maliyet beşe katlanırdı.
/// Yumuşatma yalnız son normalde gerekiyor.
float SnowDentAt(float2 uv)
{
    float4 trail = SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, saturate(uv), 0);
    return max(0.0, trail.r - trail.g);
}

/// IŞIN YÜRÜYÜŞÜ. Bakış ışını yüzeyin altına iner; yükseklik alanı ışının
/// derinliğini yakaladığı yerde durulur.
///
/// Adım sayısı sabit ve düşük (Batman: "minimal taps"). Kaba adımdan sonra iki
/// ikili bölme, keskin kenarı bulanıklaştırmadan yerine oturtuyor.
float2 SnowReliefOffset(float3 posWS, float3 viewDirWS, out float dentOut)
{
    dentOut = 0.0;

    // Deformasyon alanının dışında iz yok.
    float2 uv0 = SnowWorldToUV(posWS);
    if (SnowInsideMask(uv0) < 0.01) return (float2)0.0;

    // Bir metre derinlik için XZ'de gidilen yol. Sıyırtma açıda ışın yatar ve
    // bu vektör patlar; tavan konuyor, yoksa iz metrelerce uzar.
    float dikey = max(viewDirWS.y, 0.15);
    float2 yatay = -viewDirWS.xz / dikey;
    yatay = clamp(yatay, -SNOW_RELIEF_MAX_STRETCH, SNOW_RELIEF_MAX_STRETCH);

    const int ADIM = SNOW_RELIEF_STEPS;
    float onceki = 0.0;
    float oncekiDerinlik = 0.0;

    [unroll]
    for (int i = 1; i <= ADIM; ++i)
    {
        float t = (float)i / (float)ADIM;
        float derinlik = t * SNOW_RELIEF_MAX_DEPTH;
        float dent = SnowDentAt(SnowWorldToUV(posWS + float3(yatay.x, 0, yatay.y) * derinlik));

        if (dent >= derinlik)
        {
            // İkili bölme: kesişim önceki adımla bu adım arasında.
            float a = oncekiDerinlik, b = derinlik;
            [unroll]
            for (int k = 0; k < 2; ++k)
            {
                float m = (a + b) * 0.5;
                float d = SnowDentAt(SnowWorldToUV(posWS + float3(yatay.x, 0, yatay.y) * m));
                if (d >= m) b = m; else a = m;
            }

            float son = (a + b) * 0.5;
            dentOut = son;
            return yatay * son;
        }

        onceki = dent;
        oncekiDerinlik = derinlik;
    }

    return (float2)0.0;
}

/// Çukurun eğimi — normal buradan geliyor. Merkezi fark, adım bir teksel.
half2 SnowDentSlope(float2 uv)
{
    float t = 1.0 / _SnowResolution;
    float metre = _SnowAreaSize * t;

    float dL = SnowDentAt(uv - float2(t, 0));
    float dR = SnowDentAt(uv + float2(t, 0));
    float dD = SnowDentAt(uv - float2(0, t));
    float dU = SnowDentAt(uv + float2(0, t));

    // Derinlik aşağı doğru pozitif; yüzey normali ters yöne devriliyor.
    return half2((dR - dL) / (2.0 * metre), (dU - dD) / (2.0 * metre));
}

#endif
