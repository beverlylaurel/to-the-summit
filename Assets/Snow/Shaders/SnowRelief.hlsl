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

/// GÖLGELEME YÜKSEKLİĞİ — sırt DAHİL. İşaretli: + aşağı, − yukarı.
///
/// `trail.g` karın yukarı itilmiş kısmı: oluğun iki yanındaki kabarma
/// (ölçüldü, enine kesit: oluk 204 mm derin, yanında 40 mm sırt). Bu kabarma
/// HESAPLANIYOR ama hiç çizilmiyordu; ekranda oluk düz kardan tek bir çizgiyle
/// ayrılıyordu (kullanıcı bildirdi: "geçiş çok keskin, yapay duruyor").
///
/// SIRT YALNIZ GÖLGELEMEYE GİRİYOR, IŞIN YÜRÜYÜŞÜNE DEĞİL. Işın alanından
/// çıkarıldığında sırt tam omuzun üstüne düşüp omuzu siliyordu; ölçüldü,
/// `r − g`'nin genişliği 19 tekselden 12'ye çöküyordu. Işın `trail.r`'ı
/// yürüyor (oluğun eni sabit), normal ve iz-içi gölge `r − g`'yi okuyor
/// (dudak görünüyor).
float SnowShadeHeightAt(float2 uv)
{
    float4 trail = SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, saturate(uv), 0);

    // SIRT KENDİ EĞİMİYLE SINIRLI — KOYU HALKA BURADAN ÇIKIYORDU.
    //
    // İzin DIŞINDA `trail.r = 0` ama sırt `trail.g` dolu; yükseklik doğrudan
    // `−g` oluyor ve normal orada sertçe deviriliyor. Ekranda izin çevresinde
    // koyu bir çerçeve görülüyordu (kullanıcı bildirdi: "kenarlarda koyulaşma
    // var, sanki border gibi").
    //
    // `KRim` sırdı 4 cm'ye kadar yazıyor ama profili dar: 2-3 tekselde
    // (5-7 cm) sıfırdan tepeye çıkıyor, yani yan eğimi 30-40°. Gerçek bir
    // yığın duruş açısını (38°) aşamaz ve ayak izinin yanındaki yığın çok
    // daha yayvandır.
    //
    // Sırt burada `SNOW_RIM_SHADE` payıyla okunuyor: geometrisi duruyor,
    // gölgelemeye giren payı azalıyor.
    return (trail.r - trail.g * SNOW_RIM_SHADE) * SnowInsideMask(uv);
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

    float d = SnowShadeHeightAt(uv) * 0.25;

    d += SnowShadeHeightAt(uv + float2( b.x, 0.0)) * 0.125;
    d += SnowShadeHeightAt(uv + float2(-b.x, 0.0)) * 0.125;
    d += SnowShadeHeightAt(uv + float2( 0.0,  b.y)) * 0.125;
    d += SnowShadeHeightAt(uv + float2( 0.0, -b.y)) * 0.125;

    d += SnowShadeHeightAt(uv + float2( b.x,  b.y)) * 0.0625;
    d += SnowShadeHeightAt(uv + float2(-b.x,  b.y)) * 0.0625;
    d += SnowShadeHeightAt(uv + float2( b.x, -b.y)) * 0.0625;
    d += SnowShadeHeightAt(uv + float2(-b.x, -b.y)) * 0.0625;

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

    // İZ RENKTEN DEĞİL GÖLGEDEN GÖRÜNÜR.
    //
    // [KAYNAK: adli ayak izi belgeleme — "most detail in an impression
    // photograph is visible due to small shadows within the impression".]
    //
    // GÜNEŞ ALÇAKKEN GÖLGE ZAYIFLIYOR. Işın yatay ilerlemesi
    // `lightDir.xz / lightDir.y`; güneş alçaldıkça bu büyüyor ve `engel`
    // neredeyse her yerde 1'e doyuyor. Tam güç bırakılınca akşam izin tamamı
    // gölgede kalıp simsiyah oluyordu (ölçüldü: 17:49, gündüz oranı 0.33).
    //
    // Fizikte de böyle: güneş alçakken toplam aydınlatmada direkt payı
    // düşüyor, gök payı artıyor; gölge kontrastı azalıyor.
    float gunesYuksekligi = saturate(lightDirWS.y * 3.0);
    float guc = lerp(SNOW_SHADOW_LOW_SUN, 1.0, gunesYuksekligi);

    // KAR YARI SAYDAM: engelleme tam olsa bile gölge siyaha inmiyor. Ölçülü
    // kar gölgesi/güneş oranı açık gökte 0.5-0.6.
    float golge = saturate(1.0 - engel * guc);

    return (half)lerp(SNOW_SHADOW_FLOOR, 1.0, golge);
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

/// KAR YÜZEYİNİN KENDİ RÖLYEFİ — ÖLÇÜLMÜŞ YER ŞEKİLLERİ.
///
/// [KAYNAK: Filhol & Sturm 2015, "Snow bedforms: A review, new data, and a
/// formation model", JGR Earth Surface; Kochanski, Anderson & Tucker 2019,
/// "The evolution of snow bedforms in the Colorado Front Range",
/// The Cryosphere 13:1267 — arazide ölçülmüş boyutlar ve eşikler.]
///
///   plane bed   rüzgâr < 6.4 m/s VE kar < 1.4 gün  -> düz
///   ripple      0.5-2 cm yüksek, 10-25 cm dalga    -> rüzgâra DİK
///   sastrugi    14-40 cm derin, 45-90 cm aralık    -> rüzgâra PARALEL
///   snow wave   tepe aralığı 10-20 m               -> rüzgâra dik/eğik
///
/// Rüzgâr eşikleri de ölçülmüş: kar hareketi 7-14 m/s, sastrugi oluşumu
/// en az 20 m/s. Sakin havada yüzey plane bed'e yakın kalıyor; yer şekilleri
/// rüzgârla beliriyor.
///
/// TABAN fBm — DOĞAL YÜZEYLER SELF-AFFINE.
///
/// [KAYNAK: yüzey pürüzlülüğü literatürü — self-affine bir yüzeyin güç
/// spektrumu `C(q) ~ q^(-2(H+1))`.] Oktavlar arası genlik oranı keyfi değil:
/// frekans iki katına çıkarken genlik `2^(-H)` ile düşüyor. Kar için H = 0.8,
/// yani oran 0.574. Bu kural olmadan oktav genlikleri elle seçiliyor ve yüzey
/// ya tek ölçekli (tarak gibi) ya da gürültülü çıkıyor.
float SnowYuzeyRolyef(float2 worldXZ)
{
    // --- fBm tabanı: dört oktav, self-affine ---
    float h   = 0.0;
    float amp = SNOW_FBM_AMP;
    float frq = SNOW_FBM_SCALE;

    [unroll]
    for (int i = 0; i < 4; ++i)
    {
        h += (SnowValueNoise(worldXZ * frq + (float)i * 17.3) * 2.0 - 1.0) * amp;

        amp *= SNOW_FBM_GAIN;
        frq *= 2.0;
    }

    // --- rüzgâr ekseni ---
    float2 w  = _WindWS.xz;
    float  uz = length(w);
    w = uz > 1e-4 ? w / uz : float2(1.0, 0.0);

    float2 dik = float2(-w.y, w.x);

    // --- RIPPLE: rüzgâra DİK sırtlar ---
    //
    // Sırtlar rüzgâra dik olduğu için dalga rüzgâr YÖNÜNDE ilerliyor; dik
    // eksende altı kat uzun tutulup sırt hâline getiriliyor.
    float ripplePay = SNOW_RIPPLE_BASE
                    + (1.0 - SNOW_RIPPLE_BASE) * saturate((_WindSpeed - 4.0) / 6.0);

    float2 pr = float2(dot(worldXZ, w)   / SNOW_RIPPLE_LENGTH,
                       dot(worldXZ, dik) / (SNOW_RIPPLE_LENGTH * 6.0));

    h += (SnowValueNoise(pr) * 2.0 - 1.0) * SNOW_RIPPLE_AMP * ripplePay;

    // --- SASTRUGİ: rüzgâra PARALEL, keskin ---
    //
    // `n²(3−2n)` üst yarıyı düzleştirip alt yarıyı dikleştiriyor: sastrugi bir
    // EROZYON şekli, rüzgârüstü yüzü dik ("upwind-facing points resembling
    // anvils").
    float sastrugiPay = SNOW_SASTRUGI_BASE
                      + (1.0 - SNOW_SASTRUGI_BASE) * saturate((_WindSpeed - 8.0) / 12.0);

    float2 ps = float2(dot(worldXZ, w)   / SNOW_SASTRUGI_WIDTH,
                       dot(worldXZ, dik) / SNOW_SASTRUGI_LENGTH);

    float ns = SnowValueNoise(ps);
    ns = ns * ns * (3.0 - 2.0 * ns);

    h += (ns - 0.5) * SNOW_SASTRUGI_HEIGHT * sastrugiPay;

    return h;
}

/// Yüzey rölyefinin eğimi. Dört örnek, 2 cm adım — alan analitik olduğu için
/// teksel ızgarası hiç devreye girmiyor.
///
/// AYRI FONKSİYON, ÇÜNKÜ YÜKSEKLİK ALANINA KONAMAZ. Denendi: yer şekilleri
/// `SnowShadeHeightAt`'e konunca `SnowDentSmooth` (9 tap) × `SnowDentSlope`
/// (4 tap) = 36 çağrı × 6 gürültü = piksel başına 180 örnek oluyor. Hem kare
/// süresi hem shader derleme süresi patlıyor.
half2 SnowYuzeyEgim(float2 worldXZ, out float yukseklik)
{
    const float e = 0.02;

    float hL = SnowYuzeyRolyef(worldXZ - float2(e, 0.0));
    float hR = SnowYuzeyRolyef(worldXZ + float2(e, 0.0));
    float hD = SnowYuzeyRolyef(worldXZ - float2(0.0, e));
    float hU = SnowYuzeyRolyef(worldXZ + float2(0.0, e));

    // YÜKSEKLİK DE BURADAN — AYRI ÇAĞRI YAPILMIYOR.
    //
    // Ortam örtmesi yüzeyin yüksekliğine ihtiyaç duyuyor; ayrı bir
    // `SnowYuzeyRolyef` çağrısı piksel başına altı gürültü örneği daha
    // demekti. Dört komşunun ortalaması merkezi zaten veriyor.
    yukseklik = (hL + hR + hD + hU) * 0.25;

    return half2((hR - hL) / (2.0 * e), (hU - hD) / (2.0 * e));
}

/// MİKRO RÖLYEF — tane ölçeği, metre.
///
/// Üç oktav, 8.3 / 3.6 / 1.6 cm dalga boyu. Yer şekillerinden ayrı bir
/// fonksiyon çünkü ağırlığı farklı: bozulmuş kar daha kaba, ama bozulmamış
/// kar da tamamen pürüzsüz değil.
///
/// SİMÜLASYON DOKUSUNA YAZILAMAZ: `KRepose` bir maksimum filtresi ve 10
/// tekselllik menzille bu ölçeği tamamen süpürüyor. Burada alan analitik,
/// teksel ızgarası devrede değil.
float SnowMikroRolyef(float2 worldXZ, float dent)
{
    float w = lerp(SNOW_MICRO_BASE, 1.0, saturate(dent / SNOW_MICRO_REF_DEPTH));

    float n  = (SnowValueNoise(worldXZ * SNOW_MICRO_SCALE_A) * 2.0 - 1.0) * SNOW_MICRO_AMP_A;
    n += (SnowValueNoise(worldXZ * SNOW_MICRO_SCALE_B + 13.9) * 2.0 - 1.0) * SNOW_MICRO_AMP_B;
    n += (SnowValueNoise(worldXZ * SNOW_MICRO_SCALE_C + 71.3) * 2.0 - 1.0) * SNOW_MICRO_AMP_C;

    return n * w;
}

/// Mikro rölyefin eğimi. Adım 1 cm — en ince oktav (1.6 cm) da geçsin diye.
half2 SnowMikroEgim(float2 worldXZ, float dent)
{
    const float e = 0.01;

    float mL = SnowMikroRolyef(worldXZ - float2(e, 0.0), dent);
    float mR = SnowMikroRolyef(worldXZ + float2(e, 0.0), dent);
    float mD = SnowMikroRolyef(worldXZ - float2(0.0, e), dent);
    float mU = SnowMikroRolyef(worldXZ + float2(0.0, e), dent);

    return half2((mR - mL) / (2.0 * e), (mU - mD) / (2.0 * e));
}

#endif
