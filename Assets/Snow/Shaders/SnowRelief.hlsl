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
    // BÖLGE DIŞINDA İZ YOK — KENET DEĞERİ SONSUZA UZANMAZ.
    //
    // `saturate(uv)` kenardaki tekseli bölge dışındaki HER noktaya kopyalıyor.
    // Sınırdaki bir teksel oyuluysa o oyuk dünyanın geri kalanına şerit olarak
    // yayılıyor ve `SnowInsideMask`'in kestiği yerde DİKDÖRTGEN bir plato
    // olarak görünüyor (kullanıcı bildirdi: "karın içinden dikdörtgen cisimler
    // çıkıyor", "kar yok seçeneğinde gidiyor").
    //
    // Durum dokusunun aynı sorunu `SnowStateAt` içinde zaten çözülmüş: orada
    // bölge dışı dünyanın genel değerine harmanlanıyor. İzin dünya karşılığı
    // yok — bölge dışında iz de yok.
    //
    // SIRT ÇUKURUN DERİNLİĞİNDEN ÇIKARILMIYOR.
    //
    // `trail.g` karın YUKARI İTİLMİŞ kısmı — izin kenarındaki kabarma. Onu
    // `trail.r`'dan çıkarmak iki ayrı geometriyi tek sayıya sıkıştırıyor ve
    // sırt tam olarak omuzun üstüne düştüğü için omuzu SİLİYOR.
    //
    // Ölçüldü: `trail.r`'ın genişliği iz boyunca sabit 19-22 teksel, ama
    // `r - g`'nin genişliği periyodik olarak 19'dan 12'ye çöküyordu. Ekranda
    // belirtisi izin parça parça, eksene hizalı bloklara ayrılmasıydı —
    // sırdın konumu yakalanan HIZLA kaydırıldığı için desen yön değiştirince
    // değişiyor, düz yürürken de yürüyüş boyunca dalgalanıyordu.
    float4 trail = SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, saturate(uv), 0);
    return max(0.0, trail.r) * SnowInsideMask(uv);
}

/// YUMUŞATILMIŞ DERİNLİK — GÖRÜNTÜ İÇİN.
///
/// Yakalama tekseli 2.3 cm ve oyma alanının kenarı bir tekselde bitiyor:
/// ham okunduğunda izin kenarı MERDİVEN gibi çıkıyor, dağılma diye bir şey
/// kalmıyor (kullanıcı bildirdi: "kenarlar çok keskin, yumuşaklık sıfır").
///
/// Bu yumuşatma bir dönem `SnowSurfaceAt` içinde vardı; kar mesh'i silinince
/// o fonksiyonla birlikte gitti ve relief ham tekseli okumaya başladı.
///
/// ÇEKİRDEK EŞ YÖNLÜ OLMAK ZORUNDA.
///
/// Önce merkez + YALNIZ DÖRT KÖŞEGEN tap vardı. Köşegen giden bir izde bu
/// çekirdeğin iki tapı kenar BOYUNCA, ikisi kenarı KESEREK düşüyor; filtrenin
/// tepkisi kenar boyunca ızgara periyoduyla modüle oluyor ve iz TIRTIL gibi
/// dişleniyordu. Eksen hizalı izde dört tap simetrik olduğu için belirti
/// görünmüyordu — ölçüldü: +X yürüyüşünde iz pürüzsüz, (1, 0.6) yönünde
/// düzenli diş (kullanıcı bildirdi: "farklı yönlere giderken sıkıntılı").
///
/// Tam 3x3 çadır çekirdeği: dört eksen tapı köşegenlerin kök2 katı ağırlıkta.
/// Yarıçap 1.5 tekselden büyük olamaz, yoksa ızgara merdiveniyle birlikte
/// oluğun 3 tekselllik duvarını da siler.
float SnowDentSmooth(float2 uv)
{
    float2 b = SNOW_CARVE_SMOOTH_TEXELS / _SnowResolution;

    float d = SnowDentAt(uv) * 0.25;

    d += SnowDentAt(uv + float2( b.x, 0.0)) * 0.125;
    d += SnowDentAt(uv + float2(-b.x, 0.0)) * 0.125;
    d += SnowDentAt(uv + float2( 0.0,  b.y)) * 0.125;
    d += SnowDentAt(uv + float2( 0.0, -b.y)) * 0.125;

    d += SnowDentAt(uv + float2( b.x,  b.y)) * 0.0625;
    d += SnowDentAt(uv + float2(-b.x,  b.y)) * 0.0625;
    d += SnowDentAt(uv + float2( b.x, -b.y)) * 0.0625;
    d += SnowDentAt(uv + float2(-b.x, -b.y)) * 0.0625;

    return d;
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

    // TAVAN UZUNLUĞA, BİLEŞENE DEĞİL.
    //
    // `clamp(yatay, -k, k)` bileşenleri ayrı ayrı kesiyor ve bu bir vektörün
    // AÇISINI DEĞİŞTİRİYOR: çapraz bakışta x kesilip z kesilmeyince ışın
    // bakışın olmadığı bir yöne gidiyor. Belirtisi izin bakış yönüne göre
    // yalpalaması — düz yürünmüş bir oluk zigzag görünüyor, en çok çapraz
    // bakışta (kullanıcı bildirdi: "farklı yönlere giderken sıkıntılı izler").
    float uzunluk = length(yatay);
    yatay *= min(1.0, SNOW_RELIEF_MAX_STRETCH / max(uzunluk, 1e-5));
    uzunluk = min(uzunluk, SNOW_RELIEF_MAX_STRETCH);

    // ERKEN ÇIKIŞ IŞININ TAMAMINA BAKIYOR, YALNIZ MERKEZE DEĞİL.
    //
    // Yalnız `uv0` sorulduğunda oluğun DIŞINDAKİ piksel hiç yürümüyordu — oysa
    // relief mapping'in bütün amacı o pikselin çukurun içini görmesi. Kenarda
    // kayma sıfırdan `yatay * derinlik` kadarına (66 cm'ye) sıçrıyor ve bu
    // süreksizlik ekranda LOB olarak çıkıyor.
    float2 ucNokta = posWS.xz + yatay * SNOW_RELIEF_MAX_DEPTH;
    float merkez = SnowDentAt(uv0);
    float uzak    = SnowDentAt(SnowWorldToUV(float3(ucNokta.x, posWS.y, ucNokta.y)));

    if (max(merkez, uzak) < 0.001) return (float2)0.0;

    // ADIM SAYISI IŞININ BOYUNDAN.
    //
    // Sabit adım sayısı sıyırtma açıda yetmiyor: ışın 66 cm uzarken 12 adım
    // teksel başına 2.4 örnek atlıyor ve kesişim adım ızgarasına yuvarlanıyor.
    // Adım başına en fazla bir teksel gidilsin istiyoruz.
    float tekselBoyu = _SnowAreaSize / _SnowResolution;
    int ADIM = (int)clamp(uzunluk * SNOW_RELIEF_MAX_DEPTH / max(tekselBoyu, 1e-4),
                          SNOW_RELIEF_STEPS_MIN, SNOW_RELIEF_STEPS_MAX);

    float oncekiDerinlik = 0.0;
    float onceki = merkez;

    for (int i = 1; i <= ADIM; ++i)
    {
        float t = (float)i / (float)ADIM;
        float derinlik = t * SNOW_RELIEF_MAX_DEPTH;
        float dent = SnowDentAt(SnowWorldToUV(posWS + float3(yatay.x, 0, yatay.y) * derinlik));

        // KARŞILAŞTIRMANIN YÖNÜ. Işın yüzeyden aşağı iniyor; çukurun TABANINI
        // arıyor. Durma koşulu "ışın yükseklik alanının altına indi", yani
        // `derinlik >= dent`.
        //
        // Bir tur ters yazıldı (`dent >= derinlik`) ve ışın ilk adımda
        // duruyordu: 22 cm'lik oluk 3 cm okunuyor, hem paralaks hem örtülme
        // yok oluyordu — iz ekranda "şeffaf" görünüyordu (kullanıcı bildirdi).
        if (derinlik >= dent)
        {
            // KESİŞİM DOĞRUSAL ÇÖZÜLÜYOR, İKİLİ BÖLMEYLE DEĞİL.
            //
            // İkili bölme derinliği adım ızgarasına yuvarlıyordu: aynı
            // derinliği paylaşan pikseller bant bant çıkıyor ve kamera
            // kıpırdadıkça bantlar kayıyordu — izin içinde HAREKET EDEN
            // SOĞAN HALKALARI (kullanıcı bildirdi).
            //
            // İki örnek arasında hem ışının derinliği hem yükseklik alanı
            // doğrusal; kesişim kapalı biçimde çözülüyor ve sonuç sürekli.
            float dOnce = onceki - oncekiDerinlik;   // önceki adımda ışın yüzeyin ÜSTÜNDE
            float dSimdi = dent - derinlik;          // bu adımda ALTINDA
            float t = saturate(dOnce / max(dOnce - dSimdi, 1e-5));

            float son = lerp(oncekiDerinlik, derinlik, t);
            dentOut = son;
            return yatay * son;
        }

        onceki = dent;
        oncekiDerinlik = derinlik;
    }

    // Tavana çarptı: çukur `SNOW_RELIEF_MAX_DEPTH`'ten derin. En derin
    // noktayı döndürüyoruz, sıfırı değil — yoksa en derin yerde iz kayboluyor.
    dentOut = SNOW_RELIEF_MAX_DEPTH;
    return yatay * SNOW_RELIEF_MAX_DEPTH;
}

/// ÇUKURUN KENDİ GÖLGESİ.
///
/// Relief mapping tek başına derinliği verir ama ışığın o derinliğe ULAŞIP
/// ulaşmadığını söylemez. Alçak güneşte sonuç tersine döner: çukurun güneşe
/// bakan duvarı parlar, yakın duvarı gölgelenmediği için ayak izi TÜMSEK gibi
/// okunur (ölçüldü: 10:00'da çukur görünüyor, 17:00'de tümsek).
///
/// Işık yönünde kısa bir yürüyüş: yükseklik alanı ışının üstüne çıkıyorsa o
/// nokta gölgededir. Adım sayısı düşük; gölge kenarı yumuşasın diye sert
/// karar yerine en büyük engel oranı kullanılıyor.
///
/// KIRPMA UZUNLUĞA, BİLEŞENE DEĞİL — aynı gerekçe `SnowReliefOffset`'te.
half SnowReliefShadow(float3 posWS, float3 lightDirWS, float dent)
{
    if (dent < 0.005) return 1.0h;

    // Işık yukarı bakan yön; yatay ilerleme birim derinlik başına.
    float dikey = max(lightDirWS.y, 0.08);
    float2 yatay = lightDirWS.xz / dikey;
    float uzunluk = length(yatay);
    yatay *= min(1.0, SNOW_RELIEF_MAX_STRETCH / max(uzunluk, 1e-5));

    float engel = 0.0;

    [unroll]
    for (int i = 1; i <= SNOW_RELIEF_SHADOW_STEPS; ++i)
    {
        float t = (float)i / (float)SNOW_RELIEF_SHADOW_STEPS;

        // Çukurun tabanından yukarı çıkan ışın: bu adımda yüzeyin altında
        // kalan pay ne kadar.
        float isinDerinlik = dent * (1.0 - t);
        float2 uv = SnowWorldToUV(posWS + float3(yatay.x, 0, yatay.y) * (dent * t));
        float komsu = SnowDentSmooth(uv);

        engel = max(engel, saturate((komsu - isinDerinlik) / max(dent, 1e-3)));
    }

    return (half)saturate(1.0 - engel * SNOW_RELIEF_SHADOW_STRENGTH);
}

/// Çukurun eğimi — normal buradan geliyor. Merkezi fark, adım bir teksel.
half2 SnowDentSlope(float2 uv)
{
    float t = SNOW_DENT_SLOPE_TEXELS / _SnowResolution;
    float metre = _SnowAreaSize * t;

    // EĞİM DE YUMUŞATILMIŞ ALANDAN. Ham alanın gradyanı teksel sınırında
    // sıçrıyor ve izin duvarı basamaklı görünüyordu.
    //
    // FARKIN ADIMI DA BİR TEKSEL OLAMAZ: o adım ızgara Nyquist'inde çalışıyor
    // ve köşegen izin merdivenini en çok büyüten yer orası (gerekçe
    // `SNOW_DENT_SLOPE_TEXELS` yanında).
    float dL = SnowDentSmooth(uv - float2(t, 0));
    float dR = SnowDentSmooth(uv + float2(t, 0));
    float dD = SnowDentSmooth(uv - float2(0, t));
    float dU = SnowDentSmooth(uv + float2(0, t));

    // Derinlik aşağı doğru pozitif; yüzey normali ters yöne devriliyor.
    return half2((dR - dL) / (2.0 * metre), (dU - dD) / (2.0 * metre));
}

#endif
