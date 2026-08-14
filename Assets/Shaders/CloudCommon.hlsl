#ifndef TO_THE_SUMMIT_CLOUD_COMMON
#define TO_THE_SUMMIT_CLOUD_COMMON

// Volumetrik bulut örnekleme ve ışın yürütme.
// Parametreler global olarak AtmosphereController tarafından yazılır; hem gökyüzü
// hem yarım çözünürlüklü bulut geçişi aynı değerleri okur.

TEXTURE3D(_DetailNoise);
SAMPLER(sampler_DetailNoise);

// 2B hava haritası — editörde pişirilir (CloudWeatherMapBaker). Gökyüzünün neresinde
// ne tür bulut olduğunu TEK okumada söyler: R kapsama, G tip, B taban kayması,
// A tavan alanı. Tavan pişirmede bulanıklaştırıldığı için dünya eğimi sınırlı —
// iğne/bıçak biçimli bulut üretilemez; kubbe garantisi haritanın kendisindedir.
// `_WeatherMap`, `_WeatherMapScale`, `_CloudWind`, `_CloudBottom` ve `_Coverage`
// bildirimleri HeightFog.hlsl'de: yüzey de bulut gölgesi için aynı haritayı okuyor,
// iki yerde bildirilirse derleyici çakışır.

// Kaba maksimum-kapsama haritası (64², pişirmede genişletilmiş). Boş gökte ışın
// yalnız bunu okur ve büyük adımlarla atlar: tam yoğunluk değerlendirmesi yapılmaz.
// Genişletme, harita bükümünün ulaşabileceği her yeri kapsar — sıçrama bir bulutun
// üstünden atlayamaz.
TEXTURE2D(_CloudSkipMap);
SAMPLER(sampler_CloudSkipMap);

// Curl gürültüsü (2B, ıraksamasız): aşındırmanın okunduğu koordinatı büker —
// bulut kenarlarına burgulu türbülans verir. Iraksamasız olduğu için alan yalnız
// KAYAR, şişip sönmez.
TEXTURE2D(_CloudCurlNoise);
SAMPLER(sampler_CloudCurlNoise);
float _CloudCurlStrength;

// Yüksek irtifa katmanı (sirrus/altokümülüs/altostratus). Hacimsel katmanın çok
// üstünde ve ince: ışın yürüyüşü yerine tek ışın-küre kesişimi + tek doku okuması.
TEXTURE2D(_CloudHighNoise);
SAMPLER(sampler_CloudHighNoise);
float _HighCloudAmount;    // katmanın toplam varlığı (0 = kapalı)
float _HighCloudType;      // 0 sirrus · 0.5 altokümülüs · 1 altostratus
float _HighCloudAltitude;  // katmanın kotu (metre)
float _HighCloudScale;     // dünya ölçeği (1/periyot)

// _SunDirection ve _LightningFlash bildirimleri de buradan gelir; bindirme geçişi
// bulutu kameranın önündeki sisin ardına koymak için sis API'sini kullanır.
#include "HeightFog.hlsl"

float3 _MoonDirection;
float _CloudShearTurn;      // rüzgâr yönünün katman boyunca dönme açısı (radyan)
float _CloudRise;           // konvektif yükselmenin biriken mesafesi (metre)
float4 _CloudBrightColor;
float4 _CloudDarkColor;
float _CloudHazeDistance; // bu mesafede bulut tamamen atmosfere karışır
float4 _CloudSunColor;
float4 _CloudMoonColor;

float _CloudTop;
float _DetailScale;
float _DetailStrength;
float _ShearAmount;
float _DetailDistance;
float _DensityScale;
float _CloudSteps;
float _CloudLightSteps;
float _CloudRimStrength;    // gümüş kenarın şiddeti
float _CloudPowderStrength; // Beer's-Powder koyu kenar etkisinin gücü
float _CloudRainAbsorb;     // yağışta ışık soğurmasının artışı
float _CloudAmbient;        // gökyüzünden gelen dağınık ışığın şiddeti
float _CloudLightReach;     // ışık sondasının menzili (metre)
float _CloudMultiScatter;   // çoklu saçılmanın gücü: ışığın buluta işleme derinliği
float _CloudAmbientFloor;   // bulut altının en düşük aydınlığı
float4 _CloudDuskTint;      // şafak ve batımda buluta binen sıcak ton
float _CloudDuskStrength;   // o tonun gücü
float _CloudMassWarmth;     // kütleden kütleye renk sıcaklığı sapması
float _CloudMassBrightness; // kütleden kütleye parlaklık sapması

// Detay dokusunun cozunurlugu. Mip secimi icin gerekli; asset'ten okunup
// yayinlaniyor, burada sabit tutulsa uretici degistiginde sessizce ayrisirdi.
// Taban dokusununki HeightFog.hlsl'de: yer golgesi de ayni alani okuyor.
float _DetailNoiseTexels;

// Işın başlangıcını dağıtan Bayer kaymasının gücü, 0-1. Kayma banding'i gizlemek için
// var ama sonucu yumuşatan bir filtre olmadığı için ekrana ham basılıyor. Sıfırlanınca
// bütün pikseller aynı kafeste örneklenir: gürültü gider, yerine banding gelir.
float _CloudDither;

// Kenar yumuşatmasının gücü. Eşiğin alt ucunu örnekleme ölçeğiyle aşağı açar; kenar
// yumuşar ama her bulutun çevresinde eşik seviyesinde zayıf bir zar bırakır ve o zar
// kenarından bakıldığında ince çizgiler olarak görünebilir. İkisi arasında ayar.
float _CloudEdgeSoften;

// Büyük ölçekli oktavın payı. Sıfırda yalnızca küçük bulutlar kalır, bire yaklaştıkça
// büyük kütleler baskınlaşır ve gökyüzünü ele geçirir.
float _CloudLargeWeight;

// Bulut küresinin yarıçapı. Dünya'nın gerçek yarıçapı DEĞİL, bilerek küçültülmüş:
// 6360 km'de 2600 m'lik bir taban ufka ancak 182 km'de değer, biz o mesafeye kadar
// yürüyemeyiz ve deniz ufka varmadan sisin içinde kesilir — "gri bant" bundan çıkıyor.
// Yarıçap çizim menziline göre seçilince bulut GERÇEKTEN ufka iner, kesilmez.
// (HZD aynı numarayı yapıyor: sahne ölçeğini yarıçap zorluyor.)
float _PlanetRadius;
#define PlanetRadius _PlanetRadius

// TEŞHİS: iterasyon ısı haritası. Bütçenin nerede yandığını tahminle değil ekranda
// görmek için. 0 = kapalı (varsayılan), global yazılmasa bile davranış değişmez.

/// Yoğunluk yerine dönen işaret: hava haritası burada hiç bulut olmadığını söylüyor.
static const float CloudRegionEmpty = -1.0;

/// Boş bölgede tek adımda geçilebilecek mesafe (metre). En küçük çekirdek 700 m
/// yarıçaplı ve kapsama saçak kapısıyla kernelın içinde başlıyor; 500 m'lik sıçrama
/// hiçbir bulutu atlayamaz. (300 fazla temkinliydi — boşluk taraması kare
/// bütçesinin büyük kısmını yiyor.)
static const float CloudSkipMeters = 500.0;

/// Kaba haritanın boş dediği yerde sıçranabilecek mesafe. Genişletme 4 texel (3 km),
/// büküm payı ~1.7 km; aradaki fark güvenlik marjı.
static const float CloudSkipCoarseMeters = 1200.0;

/// Kaba haritanın döndürdüğü işaret: büyük sıçrama serbest.
static const float CloudRegionCoarseEmpty = -2.0;

/// Aşındırmanın söndüğü bandın genişliği (metre), _DetailDistance'ın hemen berisinde
static const float CloudDetailFadeBand = 1500.0;

/// Bulutların anma tabanının altına sarkabileceği pay (metre). Yürüyüş kabuğu bu
/// kadar aşağıdan başlar; taban kesimini küre değil yoğunluk yapar.
static const float CloudBaseSag = 350.0;



/// Adım boyu mesafeyle büyür.
///
/// Örnekleme yoğunluğu dünyada değil **ekranda** sabit olmalı: 20 km ötedeki 200 metrelik
/// bir adım, 500 m ötedeki 40 metrelik adımla ekranda aynı yeri kaplar. Sabit adımla hem
/// yakını yumuşatmak hem ufka ulaşmak aynı bütçeyle mümkün değil — biri kenarları bıçak
/// gibi bırakıyor, diğeri uzaktaki bulutları hiç göstermiyordu.
///
/// Ölçü ışın boyunca kat edilen mesafe; sahne derinliği değil. Aynı mesafedeki her piksel
/// aynı adımı alır, dolayısıyla arazinin silueti buluta desen olarak basılmaz — derinliğe
/// bağlanan bir önceki denemenin hatası oydu. Mip seçimi büyüyen adıma göre dokuları
/// kendiliğinden bulanıklaştırdığı için uzakta aliasing de üretmiyor.
///
/// Taban adım kalite sürgüsünden türer: 2000 / adım sayısı. 48'de 42 m, 96'da 21 m.
/// Sabit yazılınca sürgü yalnızca iterasyon bütçesini değiştiriyor ve kaliteyi hiç
/// etkilemiyordu — kalite sürgüsünün kaliteyi değiştirmemesi başlı başına hata.
#define CLOUD_STEP_BASE (2000.0 / max(8.0, _CloudSteps))

/// Adımın iki katına çıktığı mesafe. Küçük değer uzağa yetişir ama uzaktaki adımı
/// kalınlaştırıp bandı geri getirir; büyük değer bandı keser, menzili kısaltır.
float _CloudStepDouble;

/// 4×4 Bayer matrisi: komşu piksellerin ışınları farklı noktadan başlar.
/// Zamana bağlı olmadığı için titremez.
float CloudBayer(float2 pixel)
{
    const float pattern[16] =
    {
        0.0000, 0.5000, 0.1250, 0.6250,
        0.7500, 0.2500, 0.8750, 0.3750,
        0.1875, 0.6875, 0.0625, 0.5625,
        0.9375, 0.4375, 0.8125, 0.3125
    };

    int2 cell = int2(fmod(abs(pixel), 4.0));
    return pattern[cell.y * 4 + cell.x];
}

float CloudRemap(float value, float low, float high, float newLow, float newHigh)
{
    return newLow + (value - low) / max(0.0001, high - low) * (newHigh - newLow);
}

/// Her iki kesişim: x yakın, y uzak. Kesişim yoksa (-1, -1).
/// FELAKET SADELEŞME KORUMASI — `Atmosphere.RaySphere` ile aynı gerekçe. `c` gezegen
/// ölçeğinde iki 4·10¹³ sayının farkı; kamera tam katman sınırındayken (taban ya da
/// tavan kotunda) c teorik olarak sıfır ve hesabın kendisi yuvarlama gürültüsü. Kaynak
/// küre yüzeyinde veya dışındaysa (c ≥ 0) ışın ancak küreye DOĞRU giderse kesişir.
float2 CloudRaySphere(float3 origin, float3 direction, float radius)
{
    float b = dot(origin, direction);
    float c = dot(origin, origin) - radius * radius;

    if (c >= 0.0 && b >= 0.0) return float2(-1.0, -1.0);

    float d = b * b - c;
    if (d < 0.0) return float2(-1.0, -1.0);

    float sqrtD = sqrt(d);
    return float2(-b - sqrtD, -b + sqrtD);
}

/// Işının bulut katmanı içinde kaldığı aralık. Kamera katmanın altında,
/// içinde veya üstünde olabilir; üçü ayrı hesaplanır.
float2 CloudSpan(float3 position, float3 direction)
{
    float altitude = length(position) - PlanetRadius;

    // Kabuğun tabanı anma kotunun ALTINDA: taban düzlemi örnekleme sınırı olursa
    // sarkmak isteyen bulut tam o kotta kırpılıyor ve "görünmez bir sınır" gibi
    // dümdüz kesiliyordu. Sarkma payı yürüyüşe dahil edilir; tabanın nerede bittiğini
    // küre değil, yoğunluğun kendisi söyler.
    float floorAltitude = _CloudBottom - CloudBaseSag;

    float2 inner = CloudRaySphere(position, direction, PlanetRadius + floorAltitude);
    float2 outer = CloudRaySphere(position, direction, PlanetRadius + _CloudTop);

    if (outer.y < 0.0) return float2(-1.0, -1.0);

    if (altitude < floorAltitude)
    {
        // Katman altındayken aşağı bakan ışın gezegen eğriliği yüzünden katmanı ufkun
        // yüz kilometre ötesinde yeniden kesiyor: oyuncuya görünmeyen, adım boyu devasa,
        // örneklemesi yetersiz bir yürüyüş. Maliyet koruması mesafeye bakar (bkz.
        // RaymarchClouds), yöne değil: yönle kesmek kameradan geçen yatay düzlemde
        // bıçak gibi düz bir sınır bırakıyordu — az aşağı bakan ışında bulut var,
        // biraz daha aşağıda hiç yok.
        if (inner.y < 0.0) return float2(-1.0, -1.0);
        return float2(inner.y, outer.y);
    }

    if (altitude > _CloudTop)
    {
        if (outer.x < 0.0) return float2(-1.0, -1.0);
        return float2(outer.x, inner.x > 0.0 ? inner.x : outer.y);
    }

    return float2(0.0, inner.x > 0.0 ? inner.x : outer.y);
}

/// Katman içi oran. Kasıtlı olarak KIRPILMAZ: taban sarkması negatif değerlerle
/// çalışır — kırpılınca sarkan bulutun tamamı sıfır kotuna yapışıp yine düz kesiliyordu.
float CloudHeightFraction(float3 position)
{
    float altitude = length(position) - PlanetRadius;
    return (altitude - _CloudBottom) / max(1.0, _CloudTop - _CloudBottom);
}

/// Yükseklik profili — üç tip ön ayarı, bulut tipine göre karışır (Nubis/HZD modeli).
/// Tek profil bütün gökyüzüne aynı silueti giydiriyordu; tip kanalı artık gerçekten
/// biçim değiştiriyor: yayvan levha ↔ klasik kümülüs ↔ örslü dev.
///
/// `ceiling` kolonun tepe payı (haritadan): profili dikeyde ölçekler, böylece her
/// bulut kendi boyunu korur. `baseLift` kolon başına taban kaydırması.
float CloudHeightGradient(float fraction, float type, float ceiling, float baseLift)
{
    // Profil kendi 0-1 uzayında çalışır: tabandan tavana normalize edilmiş yükseklik.
    float span = max(0.05, ceiling - baseLift);
    float h = saturate((fraction - baseLift) / span);

    // Stratus: alçak, yayvan, ince dilim. Kümülüs: geniş tabanlı, orta gövdeli,
    // yuvarlak tepeli. Kümülonimbus: neredeyse tüm katmanı kaplar, geç söner.
    float stratus = smoothstep(0.0, 0.10, h) - smoothstep(0.18, 0.32, h);
    float cumulus = smoothstep(0.0, 0.14, h) - smoothstep(0.52, 0.92, h);
    float cumulonimbus = smoothstep(0.0, 0.08, h) - smoothstep(0.05, 1.0, h);

    float profile = type < 0.5
        ? lerp(stratus, cumulus, saturate(type * 2.0))
        : lerp(cumulus, cumulonimbus, saturate((type - 0.5) * 2.0));

    // Yoğunluk irtifayla artar, TABANDA azalır: gerçek kümülüsün altı tüylüdür,
    // üstü dolgun. Bu çarpan olmadan bulut alttan da keskin bir levha gibi bitiyor.
    float baseFalloff = saturate(CloudRemap(h, 0.0, 0.05, 0.0, 1.0));

    return saturate(profile) * baseFalloff;
}


/// rayDir: ışının yönü (birim). slabSkip: dikey dilim elemesi devreye girdiğinde
/// GÜVENLE atlanabilecek mesafe, metre. Sıfır dönerse normal adım.
/// deep: ışın büyük ölçüde kapanmış, yani buradaki katkı transmittance ile çarpılıp
/// küçülüyor. Döşeme kırıcı ikinci kafes ve tepe tümseği o noktada görünmez ama iki
/// 3B okuma yiyor. Tespit kademesiyle (cheap) karıştırılmaz: cheap ≥ full garantisi
/// izo-yüzey testi için şart, deep yalnız gölgelenen kuyrukta açılır.
float CloudDensity(float3 position, bool cheap, float distance, float stepSize,
                   float3 rayDir, out float slabSkip, bool deep = false)
{
    slabSkip = 0.0;
    float fraction = CloudHeightFraction(position);
    if (fraction <= -0.1 || fraction >= 1.0) return 0.0;   // -0.1 ≈ sarkma payının dibi

    float3 drifted = position + _CloudWind;

    // HAVA HARİTASI DAHA YAVAŞ AKAR (rüzgârın %72'si). Alanın tamamı tek vektörle
    // rijit ötelenince gökyüzü kayan bir levha gibi duruyordu: hiçbir bulut yolda
    // değişmiyor, sadece geçiyordu. Kapsama zarfı şekil alanından yavaş akınca
    // şekiller zarfın içinden geçer — bulut ön kenarında oluşur, arka kenarında
    // dağılır. Gerçek gökyüzünün "taşınırken yaşama" hissi buradan gelir; kolonsal
    // tutarlılık bozulmaz (harita yine yükseklikten bağımsız).
    float2 mapPos = position.xz + _CloudWind.xz * 0.72;

    // KABA ELEME önce: tek küçük doku okuması. Boşsa hiçbir 3B doku okunmaz ve
    // yürüyüş büyük adımla atlar. Fırtına dolgusu kapsamayı her yerde yükselttiği
    // için testte o da hesaba katılır (dolgu tabanının tavanı 0.64).
    float stormFill = smoothstep(0.55, 0.95, _Coverage);
    // UFUK KURALI (HZD): 15 km'den sonra tip kümülüse, kapsama tabana doğru
    // çekilir — ufuk her havada "epik" kalır, çıplak şerit olmaz. Yakın gökyüzü
    // hava durumunun kendi dağılımını aynen taşır.
    float horizonBias = saturate((distance - 15000.0) / 12000.0);

    // Kapalı havada bu okuma ispatlanabilir şekilde boşa gider: stormFill 1 iken
    // coarseMax ≥ 0.70, ×1.22 ×_Coverage ×1.8 zaten 1'e doyuyor ve test asla "boş"
    // diyemiyor. Yalnız dolgunun doymadığı havalarda okunuyor — sonuç birebir aynı.
    float coarse = stormFill < 0.98
        ? SAMPLE_TEXTURE2D_LOD(_CloudSkipMap, sampler_CloudSkipMap,
                               mapPos * _WeatherMapScale, 0).r
        : 1.0;

    // Ufuk payı kaba elemeye DEĞİL, aşağıda kapsamaya uygulanır. Elemeye yazmak
    // (coarse = max(coarse, horizonBias·0.45)) 15 km'den sonra sıçramayı fiilen
    // kapatıyordu: ufka bakan ışın — ki en uzun yürüyen odur — boş gökte adım adım
    // ilerliyor, kare bütçesini yakıyordu. Elemenin tek görevi "burada kesinlikle
    // bulut yok" demek; ufuk kuralı bir GÖRÜNÜM kuralı, eleme ölçütü değil.
    float coarseMax = lerp(coarse, max(coarse, 0.64), stormFill);
    coarseMax = max(coarseMax, horizonBias * 0.34);   // dolgunun kapsamaya kattığı taban

    // Cephe nefesi (aşağıda p *= 0.78 + 0.44·colWarp.b) kapsamayı %22'ye kadar
    // YÜKSELTEBİLİR. Eleme bunu bilmezse üst sınır gerçek değerin altında kalır ve
    // sıçrama zayıf da olsa var olan bir bulutun üstünden atlayabilir.
    coarseMax *= 1.22;
    if (saturate(coarseMax * _Coverage * 1.8) * CloudCoverageCeiling < 0.06)
        return CloudRegionCoarseEmpty;

    // Kolon-sabit gürültü ÖNCE okunur: r tepe tümsekleri + dolgu deseni, gba HARİTA
    // UV BÜKÜMÜ. Harita düz koordinatla okununca ayak izleri kernel'in temiz pasta
    // konturunu taşıyordu — "dağılma yok, saçaklanma yok". Büküm kıyıyı ±700 m
    // dişler; rüzgâraltı DİL bileşeni (üst rüzgâr yönünde, asimetrik, 1.1 km'ye
    // kadar) bulutun kenarından savrulan uzantılar çeker — gerçek kümülüsün
    // rüzgârla taranmış saçağı. Kolonsal olduğu için dikey tutarlılık bozulmaz.
    // Büküm vektörü ALÇAK frekanstan (6.7 km periyot): ilk deneme yüksek frekanslı
    // kanallardan almıştı — 50-185 m'lik hücrelere 700-1100 m genlik binince uzay
    // katlandı, gök şerit/nokta zinciri enkazına döndü. Kural: bükümün dalga boyu
    // genliğinden BÜYÜK olmalı (gradyan ~0.2, katlanma imkânsız).
    // UCUZ KADEME (PDF'in tanımı): "yalnız alçak frekans şekil, yüksek frekans detay
    // yok". Aşındırma yalnızca OYDUĞU için bu tanım kendiliğinden muhafazakârdır:
    // ucuz değer her zaman tam değere eşit ya da ondan büyüktür. İki kademeli
    // yürüyüşün izo-yüzey testi bunu gerektirir — ucuz örnek "boş" derse ışın oradan
    // atlar, değer eksik çıkarsa bulut ıskalanır. Başka okumaları da atlamak
    // (kafes kırıcı örneklem, tavan tümseği) garantiyi bozuyordu; onları telafi için
    // şişirmek ise gölge sondasını koyulaştırıyordu — sonda da aynı ucuz yolu
    // kullanıyor.
    // Büküm ucuz yolda da OKUNUR. Atlanması denendi: gölge ışını o zaman farklı
    // hizalanmış bir alan örnekliyor, yoğunluğu olduğundan AZ görüyor, ışık fazla
    // geçiyor ve bulutlar yıkanmış beyaza dönüyordu. Gölgenin gördüğü alan ile
    // birincil alan aynı yerde durmalı; ucuz yol yalnız KATKISI KÜÇÜK okumaları
    // (tepe tümseği, kafes kırıcı ikinci örneklem) atlar.
    // ZAMAN EVRİMİ: kolonsal alan 3B dokuda y ekseninde YAVAŞÇA ilerler — 2B alanın
    // kendisi biçim değiştirir. Harita rüzgârla ötelenirken cepheler ayrıca büyür ve
    // dağılır; bu olmadan gökyüzü hep aynı deseni taşıyıp yalnız kayıyordu. Ek doku
    // okuması yok, yalnız koordinat.
    CloudFootprint fp = CloudFootprintAt(mapPos, stormFill, horizonBias,
        CloudSampleLod(stepSize, _CloudScale * 0.5, _BaseNoiseTexels),
        CloudSampleLod(stepSize, _WeatherMapScale, _WeatherMapTexels));
    float4 colWarp = fp.colWarp;
    float4 w = fp.weather;
    float localCoverage = fp.coverage;

    // Kolonsal warp PÜRÜZSÜZ alandan: harita kanalları (G/B) 280-600 m'lik doku
    // taşıyor ve sık istifte çekirdek geçişleriyle hızlanıyor — 500+ m genlik
    // binince uzay dikey yapraklar hâlinde katlanıp bulut içine dev perde/soğan
    // katmanları basıyordu. Dalga boyu kuralı: büküm kaynağının özelliği (~1.1 km)
    // genlikten büyük.
    float3 warp = float3(colWarp.g - 0.5, 0.0, colWarp.r - 0.5) * 450.0;


    // Saçak kapısı BURADA UYGULANMAZ — aşağıda (erken kapı, fıçı sapmasını
    // mıhlıyordu). Erken çıkış = kapının matematiksel sıfırı: 0.08/1.25 = 0.064
    // hamın altında kapı kesin kapalı. 0.028 denendi — "temkin" diye o kadar
    // düşürülünce sıçrama hiç ateşlemedi: ışın bütçesi yakında tükendi, uzak
    // bulutlar hiç çizilemedi ("görüş azaldı" — iki sahte teşhis de bundandı).
    if (localCoverage < 0.06) return CloudRegionEmpty;

    // DİKEY DİLİM ELEMESİ: kolonun bulut dilimi haritadan (tek 2B okuma) muhafazakâr
    // sınırlarla biliniyor — taban kaydırması w.b'den, tavan w.a ve fırtına
    // dolgusundan; saçak/tümsek paylarına marj bırakılıyor. Dilimin dışındaki örnek
    // hiçbir koşulda yoğunluk üretemez, ama eskiden bunu öğrenmek için dört 3B doku
    // okunuyordu. Katman 3400 m, tipik bulut ~1000 m: ışın boyunca örneklerin çoğu
    // dilim dışı. Sıçrama İŞARETİ dönmüyor (0.0 dönüyor) — adım kafesi ve giriş
    // noktası aynen korunuyor, yalnız okumalar kalkıyor.
    float layerThickness = max(1.0, _CloudTop - _CloudBottom);
    float baseLiftMin = (w.b - 0.5) * (800.0 / layerThickness) - 0.015;
    float ceilingMax = max(w.a, stormFill * 0.54) + 0.09;
    if (fraction < baseLiftMin - 0.01 || fraction > ceilingMax + 0.01)
    {
        // DİLİM DIŞI ATLANIR. Burada yoğunluk üretilemez — eleme zaten bunu söylüyor —
        // ama eskiden tek nominal adım (14-30 m) ilerleyip aynı üç okumayı tekrar
        // yapıyorduk. Katman ~5 km, tipik kolonun dilimi onun üçte biri: katman içi
        // örneklerin çoğu buraya düşüyor ve hiçbir şey üretmeden okuma yakıyordu.
        //
        // Atlanacak mesafe iki sınırın küçüğü:
        //   dikey — dilimin kenarına uzaklık, ışının eğimine bölünür
        //   yatay — kolon verisi ancak yatayda değişir; harita texel'i 94 m ve
        //           A/B kanalları 2-6 texel bulanık, 200 m güvenli pay
        // Küçüğü alınınca atlama BAŞKA bir kolonun dilimine giremez: kayıpsız.
        float layer = max(1.0, _CloudTop - _CloudBottom);
        float gap = fraction < baseLiftMin
                  ? (baseLiftMin - 0.01 - fraction) * layer
                  : (fraction - ceilingMax - 0.01) * layer;

        float vertical = gap / max(0.02, abs(rayDir.y));
        // Yatay pay 200 → 90 m (bir harita texel'i). 200 m fazla cömertti: harita
        // BÜKÜLMÜŞ koordinattan okunuyor (kıyı dişlemesi ±650 m, rüzgâraltı dili
        // 1000 m'ye kadar), yani atlanan yerde kolon verisi sanılandan çok
        // değişebiliyor. Ufka yakın ışınlarda yatay sınır belirleyici olduğu için
        // hata orada birikip uzak bulutları yutuyordu.
        float horizontal = 90.0 / max(0.02, length(rayDir.xz));
        slabSkip = min(vertical, horizontal);
        return 0.0;
    }

    // Tepe tümseği okuması BOŞLUK TESTİNDEN SONRA: boş kolonlarda hiç okunmuyor.
    // Gökyüzünün büyük kısmı boş ve o ışınlar kare bütçesinin çoğunu yürüyor —
    // erken çıkıştan önce okunması her boş örneğe bir 3B doku faturası çıkarıyordu.
    float colBump = deep ? 0.5
        : SAMPLE_TEXTURE3D_LOD(_BaseNoise, sampler_BaseNoise,
                               float3(mapPos.x, 310.7, mapPos.y) * (_CloudScale * 3.0),
                               CloudSampleLod(stepSize, _CloudScale * 3.0,
                                              _BaseNoiseTexels)).r;

    // Tip çekirdek başına sabittir (haritada boyanır): yan yana bulutlar farklı
    // karakterde — biri yayvan ve ince, öbürü kabarık ve opak.
    // Ufukta tip kümülüse kayar: uzak gökyüzü yayvan levhalarla değil, tanınabilir
    // kabarık kütlelerle dolar.
    float type = lerp(w.g, max(w.g, 0.62), horizonBias);

    // Rüzgâr makaslaması: üst katmanlar alta göre sabit bir mesafe kayar.
    // Biriken sürüklenme değerine bağlanınca kayma zamanla sınırsız büyüyor
    // ve üst katmanlar giderek hızlanıyordu.
    // MAKASLAMA: yanal kayma + YÖN DÖNMESİ. Gerçek atmosferde rüzgârın yalnız hızı
    // değil YÖNÜ de irtifayla değişir (sürtünme yerde rüzgârı yavaşlatıp saptırır;
    // yükseldikçe düzelir — Ekman spirali). Tek yönde kaydırmak bulutun tepesini
    // tabanına göre öteliyor ama hepsi aynı hizada kalıyordu: katmanlar birbirine
    // göre DÖNMÜYORDU. Dönme, kayma vektörünü yükseklikle çevirerek kurulur —
    // tepe rüzgârı tabana göre saat yönünde sapar, kütle burularak uzar.
    float shearTurn = _CloudShearTurn * fraction;
    float shearCos = cos(shearTurn);
    float shearSin = sin(shearTurn);
    float3 shearDir = float3(_CloudShearOffset.x * shearCos - _CloudShearOffset.z * shearSin,
                             0.0,
                             _CloudShearOffset.x * shearSin + _CloudShearOffset.z * shearCos);
    float3 shear = shearDir * fraction;

    // Evrim ÜÇ EKSENDE: tek eksende (y) kaydırmak gürültü alanını dikey olarak
    // sürüklüyor ve bu da bir kayma okunuşu veriyordu. Üç eksende farklı hızlarda
    // kayınca alan yerinde kaynıyor — bulut taşınırken şekil de değişiyor.
    // KONVEKTİF YÜKSELME: bulut kütlesi yalnız yatayda akmaz, yerden gelen ısıyla
    // YÜKSELİR ("fabrikadan çıkan buhar gibi"). Şekil alanı zamanla aşağı kaydırılır;
    // örneklem yukarı doğru akan bir alanı okur, yani tomurcuklar tabandan doğup
    // yukarı tırmanır. Yükselme tabanın hemen üstünde en hızlıdır (sıcak hava oradan
    // girer), tepeye doğru sönümlenir — doruk zaten yayvanlaşıp duruyordur.
    float riseFade = 1.0 - saturate(fraction);
    float3 rise = float3(0.0, -_CloudRise * (0.35 + 0.65 * riseFade), 0.0);

    float3 uvw = (drifted + warp + shear + rise) * _CloudScale
               + float3(_Evolution * 0.55, _Evolution, _Evolution * 0.33);

    // İkincil örneklem TAM okunur (maliyet aynı): r büyük oktav, gba taban için 3B
    // büküm. Taban dokusu 2.86 km'de döşenir; kolon warp'ı bölgesel sabit kaldığı
    // için yerel kafesi bükemiyordu — 7.7 km periyotlu 3B büküm yumru kafesini dalga
    // dalga kaydırır. Genlik 700: gradyan ~0.12, uzay katlanmaz.
    // CURL ÖNCE İSTENİR. Koordinatı yalnız drifted'a bağlı — hiçbir şeyi beklemiyor —
    // ama erozyon bloğunun içinde, en sonda okunuyordu ve oradan `detail`e giden ikinci
    // bir bekleme zinciri doğuyordu. Başta istenince gecikmesi 3B okumaların altında
    // erir. Ucuz kademede istenmez: orada erozyon zaten yok.
    // Kapı burada da geçerli: erozyon yalnız _DetailDistance içinde çalışıyor ve o
    // sınır sadece mesafeye bağlı, yani en baştan biliniyor. Kapısız hâli 9 km
    // ötedeki her örneğe bedava olmayan bir okuma çıkarıyordu.
    float3 curl = (cheap || distance >= _DetailDistance) ? 0.0
        : SAMPLE_TEXTURE2D_LOD(_CloudCurlNoise, sampler_CloudCurlNoise,
                               drifted.xz * _DetailScale * 0.5, 0).rgb * 2.0 - 1.0;

    // Derinde büyük oktav ve 3B büküm de okunmuyor: ikisi de şeklin ince ayarı ve
    // ışın kapandıktan sonra ekrana ulaşmıyor. Üçüncü 3B okuma da böylece düşüyor.
    // r = 0: büyük oktav katkısı yok. 0.5 döndürmek `max(shape, secondary)` üzerinden
    // yoğunluğa 0.5 TABAN biniyordu — sonda bulutu olduğundan yoğun görüp gölgeleri
    // karartıyordu. gba = 0.5 nötr (baseWarp sıfır çıkar).
    float4 sec4 = deep ? float4(0.0, 0.5, 0.5, 0.5)
        : SAMPLE_TEXTURE3D_LOD(_BaseNoise, sampler_BaseNoise, uvw * 0.37 + 13.7,
                               CloudSampleLod(stepSize, _CloudScale * 0.37, _BaseNoiseTexels));
    // sn2 ÖNCE İSTENİR. Koordinatı yalnız drifted+warp'a bağlı, yani sec4'ü beklemek
    // zorunda değil — ama kodda sonra durduğu için derleyici onu shapeNoise'ın
    // ardına diziyordu ve üç 3B okuma zincir hâlinde bekliyordu. Sıra değişince
    // sec4 ile sn2 BİRLİKTE issue edilir, shapeNoise yalnız sec4'ü bekler: zincir
    // derinliği 4'ten 3'e iner. Okunan değer birebir aynı, görüntü değişmez.
    // Döndürülmüş ikinci PW örneklemi: tek dokunun tek örneklemi dünyada 2.9 km'de
    // bir AYNEN kopyalanır — 37° dönme + ×1.26 ölçek bağımsız ikinci kafes verir.
    float2 rot = float2((drifted.x + warp.x) * 0.7986 - (drifted.z + warp.z) * 0.6018,
                        (drifted.x + warp.x) * 0.6018 + (drifted.z + warp.z) * 0.7986);
    float4 sn2 = SAMPLE_TEXTURE3D_LOD(_BaseNoise, sampler_BaseNoise,
                                      float3(rot.x, drifted.y, rot.y) * _CloudScale * 1.26 + 71.3,
                                      CloudSampleLod(stepSize, _CloudScale * 1.26, _BaseNoiseTexels));

    float secondary = sec4.r;
    float3 baseWarp = deep ? 0.0 : (sec4.gba - 0.5) * 700.0;

    float4 shapeNoise = SAMPLE_TEXTURE3D_LOD(_BaseNoise, sampler_BaseNoise,
                                             uvw + baseWarp * _CloudScale,
                                             CloudSampleLod(stepSize, _CloudScale, _BaseNoiseTexels));

    float shape = CloudRemap(shapeNoise.r,
        (shapeNoise.g * 0.625 + shapeNoise.b * 0.25 + shapeNoise.a * 0.125) - 1.0, 1.0, 0.0, 1.0);
    float shapeB = CloudRemap(sn2.r,
        (sn2.g * 0.625 + sn2.b * 0.25 + sn2.a * 0.125) - 1.0, 1.0, 0.0, 1.0);

    // ORTALAMAYLA birleşir, max ile değil: max denendi — zayıf bantta etkisiz, geniş
    // bantta alanı basıp dev levha yapıyordu. Ortalama ancak İKİ kafes birden tekrar
    // ederse tekrar eder; ortak periyot pratikte yok — birebir kopya bulut imkânsız.
    // Gerilme (×1.3) ortalamanın yuttuğu varyansı geri verir, eşik davranışı korunur.
    shape = deep ? shape : lerp(shape, shapeB, 0.4);
    shape = saturate((shape - 0.5) * 1.3 + 0.5);

    // Büyük oktav max ile: ortalamak onu siliyordu — iki boy bulut aynı gökyüzünde.
    shape = max(shape, secondary * _CloudLargeWeight);

    // Taban kaydırması iki ölçekten: kolon payı haritadan (bulut kümeleri arasında
    // yüzlerce metre kot farkı — kimi bulut alçak, kimi yüksek oturur), saçak payı
    // şekil gürültüsünden (alt kenarın tutamlanması).
    float baseLift = (w.b - 0.5) * (800.0 / max(1.0, _CloudTop - _CloudBottom))
                   + (shapeNoise.g - 0.5) * 0.03;

    // Fırtınada tavan da dolar: yaygın yükselme tepeleri her yerde kaldırır ama
    // tekdüze değil — büyük oktav km ölçekli dalgalar verir, küme türetleri (yüksek A)
    // örtüyü içinden deler. Dalga ikincil oktavın yaşam bandına (0.35-0.75) gerilir.
    // Dalga KOLON-SABİT kanaldan. Eskiden `secondary` (sec4.r) kullanılıyordu ve o
    // örneğin y'sini taşıyor: tavan kolon boyunca yukarı çıktıkça DEĞİŞİYORDU.
    // Tavan kolon başına tek sayı olmak zorunda — değilse bulutun üst yüzeyi 3B
    // pürüzsüz gürültünün izo-yüzeyi olur, yani yuvarlak kapaklar. Yüksek kapsamada
    // (stormFill → 1) tavanı tamamen bu dalga sürdüğü için tepeler kubbeleşiyordu.
    // colWarp.r aynı doku kanalı (Perlin-Worley) ama y'si sabit: dağılım aynı,
    // 0.35-0.75 bandı geçerli kalıyor, ek doku okuması yok.
    float wave = 0.15 + 0.39 * saturate((colWarp.r - 0.35) / 0.40);
    float ceiling01 = max(w.a, stormFill * wave);

    // Tepe tümsekleri KOLON-SABİT gürültüden (colBump yukarıda okundu): tavanın
    // kendisi kolon kolon iner çıkar (±~300 m). Doruğu 3B gürültüye yontturmak bant
    // genişliği pazarlığına mahkûmdu — dar bant tepe düzlüğü, geniş bant gövdeden
    // kopuk yüzen şapkalar doğuruyordu. Kolon-sabit tümsekte zarf her kolonda tek
    // parça monoton: şapka MATEMATİKSEL olarak imkânsız, tepe yine biçimli.
    ceiling01 = saturate(ceiling01 + (colBump - 0.5) * 0.18);

    // Tavan, görünür ayak iziyle AYNI TEMPODA büyür — faktör KÜRESEL (_Coverage),
    // kolon değil. Kolon-bazlı ezme denendi: kolon kapsaması sürgüyle dimdik
    // tırmanınca bulutlar genişlemeden sadece DİKEY büyüyordu, üstelik merkez uzun
    // kenar basık kalıp mermi çekirdeği çiziyordu. Küresel eğri %25'in (doğru
    // görünen) oranını sabitler, %69+'ta 1'e doyup efsane hâli korur.
    ceiling01 *= saturate(0.30 + 0.85 * _Coverage);

    // Zarf, şekil alanını ÇARPMAZ — kapsamayı kısar. Çarpım alanı tepeye doğru
    // inceltiyor ve eşiği yalnız gürültü zirveleri geçebiliyordu: hayatta kalanlar
    // sivrilerek İĞNEYE dönüşüyordu — bulut tepelerindeki koni ormanının matematiği
    // buydu. Zarf eşiği yükselttiğinde ise yumrular kendi doğal omuzlarından
    // kapanır: tepeler yuvarlak biter, gerçek kümülüsün kubbemsi türetleri gibi.
    // Tavan haritadan pişmiş geliyor: eğim garantili, kule/iğne üretemez.
    float span = saturate((fraction - baseLift) / max(0.05, ceiling01 - baseLift));

    // Kapı: yumuşak burun (0.08 — kenar tülü), 0.26'da TAM açık. Uzatılan bant
    // uzağı öldürüyordu (mip kapsamayı ortalamaya yatırır).
    float covLateral = localCoverage
                     * smoothstep(0.08, 0.26, localCoverage * (0.75 + 0.5 * colWarp.g));

    // Yükseklik profili: üç tip ön ayarı (stratus/kümülüs/kümülonimbus) tipe göre
    // karışır, taban yoğunluğu düşürülür. Mercek/fıçı sapması EMEKLİ — kümülüs
    // profili geniş tabanı ve yuvarlak tepeyi zaten veriyor; sapma alanı bozmadan
    // aynı formu üretemiyordu (kenarlar duvarlaşıyordu).
    float envelope = CloudHeightGradient(fraction, type, ceiling01, baseLift);
    if (envelope <= 0.001) return 0.0;

    float heightCoverage = covLateral * envelope;
    if (heightCoverage <= 0.001) return 0.0;

    // Kenar yumuşatması eşiği aşağı açmaz.
    //
    // Mip seçimi dokuyu adım boyuna göre bulanıklaştırıyor — aliasing için doğru — ama
    // hemen ardından keskin bir eşik uygulanınca o yumuşaklık tekrar sertleşiyor.
    // İlk çözüm eşiğin alt ucunu açmaktı: kenar yumuşuyordu ama daha önce hiç bulut
    // olmayan yerde zayıf bir yoğunluk yaratıyordu. O yoğunluk uzun ışın yollarında
    // birikip gökyüzüne ince zarlar ve örtünün üstünde havada asılı hayalet kabarcıklar
    // bırakıyordu — üstelik yumuşatma mesafeyle büyüdüğü için ikisi de uzakta toplanıyordu.
    //
    // Aynı görsel etki destek genişletmeden elde ediliyor: eşik yerinde kalır, yalnızca
    // eşiğin hemen içindeki yoğunluk daha yavaş yükselir. Kenar geniş bir bantta söner,
    // boş gökyüzünde hiçbir şey belirmez.
    // Taban 0.5: yumuşatma tamamen adım boyuna bağlıyken yakın bulutlarda sıfıra
    // iniyor ve kenarlar pasta gibi keskin kesiliyordu — dağılma/saçaklanma yoktu.
    // Taban her mesafede asgari yarı gücü garanti eder; destek genişlemez (hayalet
    // zar riski yok), yalnız kenar yoğunluğu daha geniş bantta söner.
    float ramp = saturate(0.5 + stepSize / 400.0);

    float t = CloudRemap(shape, 1.0 - heightCoverage, 1.0, 0.0, 1.0);
    if (t <= 0.0) return 0.0;

    // Uzun kuyruklu kuvvet eğrisi: smoothstep t≈0.8'de doyuyordu — kenar kabuğu
    // birkaç on metrede tam yoğunluğa bağlanıyor, teğet geçen ışın bile opak alfa
    // topluyordu ("kenarlar hâlâ şeffaf değil"). Kuvvet eğrisi tam yoğunluğu ancak
    // t→1'de verir: kabuk dünya uzayında 400-600 m'lik gerçek gradyana yayılır,
    // çekirdek doygun kalır, iç yapı da tekdüzelikten çıkar. Destek genişlemez —
    // boş göğe sızıntı yok. Mesafe yumuşatması üssü büyütür (uzakta daha da tül).
    // Üs 3.0: şeffaflaşma merkezden dışa — doygun çekirdek küçülür, tül kuşağı
    // bulutun içine doğru derinleşir. UZAKTA üs 1.6'ya iner: mip şekil değerlerini
    // ortalamaya yatırır, t³ o ortalamayı 4 kat ezip uzak kütleleri pusla birlikte
    // görünmez kılıyordu ("görüş azaldı") — tül yakının hakkı, uzak gövde katı.
    float farBlend = saturate((distance - 9000.0) / 12000.0);
    float edgeExp = lerp(lerp(3.99, 1.6, farBlend), 4.0,
                         saturate(ramp * _CloudEdgeSoften) * (1.0 - farBlend));
    float density = pow(saturate(t), edgeExp);

    // (Çekirdek vurgusu yukarıdaki kuvvet eğrisine katlandı; cılızlık telafisi
    // emekli — toplam opaklığın görevi densityScale'de.)

    // Örtü ALTI benekleme: kapalı gökte dolgu yoğunluğu tekdüzeleştiriyor, tabandan
    // bakan oyuncu derinliksiz düz gri bir tavan görüyordu — "bulut var mı yok mu
    // belli değil". Gerçek örtünün altı benek beneklidir: kalın kolonlar koyu, ince
    // yerler aydınlık sızdırır. Alt yarıda kolon-sabit, km ölçekli yoğunluk
    // dalgalanması; zirveden görünüm etkilenmez (üst yarıda söner).
    // Taban ÇEYREĞİNE kilitli ((1-span)²) ve ölçülü (±%30): alt yarıya yayılan
    // ±%50'lik pay, bulut İÇİNDEEN bakınca rüzgârla yürüyen dikey koyu sütunlar
    // olarak okunuyordu. Zeminden bakış tam taban bandını gördüğü için benekli
    // örtü izlenimi korunur.
    // Kaynak 3B (secondary, y'de değişken): kolon-sabit alan tanımı gereği dikeyde
    // sabittir — yoğunluğu onunla çarpmak bulut içine PERDE basıyor, rüzgârla akınca
    // "dikey hareket eden örüntü" olarak okunuyordu. Genliği kısmak yetmedi, kaynak
    // değişmeliydi.
    float underBand = (1.0 - span) * (1.0 - span);
    density *= lerp(1.0, 0.7 + 0.6 * secondary, stormFill * underBand);

    // Tip hem karakter hem opaklık taşır: çekirdek başına rastgele olduğu için her
    // bulutun kendi yoğunluğu buradan gelir — ince olan ışığı geçirir (Beer-Lambert
    // kendiliğinden verir), kabarık olan opak durur. Taban 0.55: 0.45'te ince
    // bulutların içinden dağ NET görünüyordu; en ince altostratus bile güneşi ancak
    // süt camı gibi geçirir, manzarayı değil.
    // frac tabanlı ayrı "kimlik" DENENDİ VE SÖKÜLDÜ: sürekli kanalların hash'i tamsayı
    // geçişlerinde 1→0 sıçrıyor, bulut içinde fermuar kenarlı dikey şerit perdeler
    // çiziyordu — süreksiz fonksiyon sürekli alana uygulanamaz.
    density *= lerp(0.55, 1.5, type);

    // Uzaktaki bulutta ince aşındırma bir pikselden küçük kalır. Sert kesmek yerine son
    // bir buçuk kilometrede söndürülüyor: o mesafede doku çoktan üst mip'e düşüp neredeyse
    // sabitlendiği için kesme, sabit uzaklıkta bir küre kabuğu boyunca yoğunluğu bir anda
    // yaklaşık onda bir oynatıyor ve gökyüzünde halka bırakabiliyordu. Bandın ötesinde
    // yine hiçbir doku okunmuyor, maliyet değişmiyor.
    float detailFade = saturate((_DetailDistance - distance) / CloudDetailFadeBand);

    // Aşındırma yalnız GÖVDEDE okunur: gücü zaten smoothstep(0, 0.45, density) ile
    // kuyrukta sıfıra iniyordu ama doku yine de okunuyordu — t³ kuyruğu örneklerin
    // büyük çoğunluğu olduğu için bu, boşa yanan bir 3B fetch demekti. Eşik 0-1
    // ölçeğinde (buradaki density henüz _DensityScale ile çarpılmadı): 0.05'te
    // aşındırma katsayısı binde üç, gözle görülmez.
    if (!cheap && detailFade > 0.0 && density > 0.05)
    {
        // Aşındırma kendi zamanında kaynar (evrimin ~3 katı): ince yapı en hızlı
        // değişen katmandır, gövdeyle aynı tempoda ötelenirse desen buluta yapışık
        // duruyor ve kaymayı vurguluyordu.
        // CURL BÜKÜMÜ (Nubis/HZD): aşındırmanın okunduğu koordinat, ıraksamasız bir
        // vektör alanıyla kaydırılır — kenarlarda burgulu, akışkan türbülans çıkar.
        // Iraksamasız olması şart: sıradan gürültüyle bükmek alanı şişirip söndürür,
        // curl yalnız kaydırır. Büküm TABANDA güçlü: alt kenarların rüzgârla
        // taranmış, tutam tutam görüntüsü oradan gelir.
        // Aşındırma kendi zamanında kaynar (evrimin ~3 katı): ince yapı en hızlı
        // değişen katmandır.
        //
        // AMA ÖTELENMESİ GÖVDEYLE AYNI. Bir dönem rüzgârın 1.4 katıyla kaydırılıyordu ve
        // sonuç şuydu: bulut kütlesi yerinde duruyor, kenarları sürünüyordu. Bulut
        // hızı yavaşlatılınca bu iyice belli oldu — "bulutlar hareket etmiyor ama
        // kenarları çok hızlı değişiyor". İnce yapı da aynı hava kütlesinin parçası.
        float3 detailUvw = drifted * _DetailScale
                         + curl * _CloudCurlStrength * (1.0 - saturate(span)) * _DetailScale
                         + float3(_Evolution * 2.1, _Evolution * 3.0, _Evolution * 1.6);
        float3 detail = SAMPLE_TEXTURE3D_LOD(_DetailNoise, sampler_DetailNoise, detailUvw,
                                             CloudSampleLod(stepSize, _DetailScale, _DetailNoiseTexels)).rgb;
        float erosionFbm = detail.r * 0.625 + detail.g * 0.25 + detail.b * 0.125;

        // TABANDA TERS WORLEY: Worley'nin tersi tutam tutam, tüylü biçimler verir —
        // gerçek kümülüsün alt kenarı böyledir. Tepede normal hâli kalır: karnabahar
        // tomurcukları. Geçiş ilk %10'luk yükseklikte (HZD'nin kuralı).
        float erosion = lerp(1.0 - erosionFbm, erosionFbm, saturate(span * 10.0));

        // Aşındırma TEPEDE güçlü: kümülüsün en çok didiklenen yeri doruğudur. Zayıf
        // kuyrukta kapanır (Worley hücre duvarları örümcek ağı olarak çıkıyordu).
        float strength = _DetailStrength * lerp(0.7, 1.12, fraction)
                         * lerp(0.8, 1.0, localCoverage)
                         * smoothstep(0.0, 0.45, density)
                         * detailFade;

        // Aşındırma yalnızca oyar. Eklemeli hâli denendi ve geri alındı: doku 28 m
        // texel'lerle çalışıyor, eklediği kabarcık da o boyda oluyor ve katmanın üstü
        // patlamış mısır tarlasına dönüyordu — sorun miktar değil ölçek.
        density = CloudRemap(density, erosion * strength, 1.0, 0.0, 1.0);
    }

    return saturate(density) * _DensityScale;
}

/// Işık yönünde koni örnekleme: tek çizgi yerine hacimsel gölge
/// cheapSampling: ışık sondasının erozyon (detay) katmanını okuyup okumayacağı.
/// HZD kuralı: bulutun ÖN yüzünde — birincil ışın daha alfa 0.3'e varmamışken — tam
/// örnekleme yapılır, derinde ucuza düşülür. Sebep: kabarık kenarların kendi gölgesi
/// onları üç boyutlu okutan şey; erozyon okunmazsa kenar düz bir zar gibi aydınlanıyor.
/// Derinde ise ışık zaten sönmüş, detayın gölgeye katkısı ölçülemez — orada okumak
/// bedava değil, bedelsiz de değil.
/// viewDistance: örneğin KAMERAYA uzaklığı. Sonda kendi mesafesini uydurmaz — detay
/// sönümü birincil ışınla aynı mesafeden hesaplanmazsa yakın bulutun gövdesi detaylı,
/// gölgesi detaysız olur.
/// openness: ışının ne kadar açık olduğu (transmittance türevi). Gölge alçak
/// frekanslı ve katkısı transmittance ile çarpılıyor; ışın kapandıkça hem koni
/// örneği sayısı hem uzak komşu örneği gereksizleşiyor.
float CloudLightTransmittance(float3 position, float3 lightDirection, bool cheapSampling,
                              float viewDistance, float openness)
{
    // Örnek sayısı SABİT. Şeffaflıkla azaltmak denendi ve geri alındı: adımlar üstel
    // (stepSize = menzil/(2ⁿ−1)), yani sayıyı düşürmek ilk adımı 39 m'den 171 m'ye
    // çıkarıyor. Sonda yakın alandaki hızlı yoğunluk düşüşünü atlayıp daha derinden
    // okuyor, optik derinliği fazla topluyor ve bulutlar toptan kararıyordu.
    int samples = (int)max(2.0, _CloudLightSteps);

    // Işık sondası kısa tutulur: katmanın tamamını taramak optik derinliği
    // şişirip güneş ışığının tamamını ilk katmanda söndürüyordu
    // Sonda menzili METRE: katman oranı olarak yazılıydı ve katman kalınlığı
    // değişince (kümülonimbus için 3400 → 6900 m) sonda iki katına çıkıyordu —
    // gölge tarağı kabalaşır, ilk adım 30 m'den 160 m'ye fırlardı. Gölgenin
    // belirleyicisi bulut kalınlığı (~1-2 km), katmanın kendisi değil.
    float reach = max(200.0, _CloudLightReach);

    // Adımlar üstel büyür — koni örnekleme.
    //
    // Sabit aralıkla örneklemek gölgelenmeyi tek bir frekansa kilitliyordu: her piksel,
    // her nokta ışık yönünde tam olarak aynı mesafelerden okuyor ve yoğunluk alanı o
    // sabit tarağın içinden geçerken bulutun üstüne düzenli oluklar biniyordu. İndeksten
    // türeyen bir kayma bunu kıramıyor, çünkü komşu noktalar aynı kaymayı paylaşıyor.
    //
    // Üstel dizilim tek frekansı ortadan kaldırır ve aynı örnek sayısıyla yakın
    // gölgelenmeyi kat kat ince çözer: gölgenin asıl belirleyicisi ilk birkaç yüz metre.
    float total = exp2((float)samples) - 1.0;
    float stepSize = reach / total;

    // Başlangıç konuma göre kaydırılır. Sabit dizilim gölgelenmeyi tek bir uzamsal
    // frekansa kilitliyor ve bulutun üstüne düzenli oluklar biniyordu: her nokta ışık
    // yönünde tam olarak aynı mesafelerden okuyunca yoğunluk alanı o sabit tarağın
    // içinden geçiyor. Konuma bağlı kayma komşu noktaların desenini ayrıştırır ve
    // birincil yürüyüşün onlarca örneği boyunca ortalanıp gözden kaybolur.
    // Kare-değişken faz DENENDİ VE GERİ ALINDI: yarım çözünürlük birleştirmesi
    // bilinçli olarak HARMANSIZ (komşuluk kelepçesi, üstel karışım yok) — dönen faz
    // ortalanacak yer bulamayıp yüzeye akan kontur şeritleri basıyordu.
    //
    // Hash YEREL koordinatta ve sin'siz. Eski hash gezegen-merkezli pozisyonla
    // (y ≈ 6.37e6) sin(5e8) hesaplıyordu — fp32'de anlamsız: jitter bölge bölge
    // sabitleniyor ve ışık kabukları çıplak merdiven olarak basılıyordu. Gözü
    // kanatan yatay soğan halkalarının gerçek kökü buydu.
    // Hash TAM frekansta (~10 m periyot). Ön ölçek (×0.0173) hash'in periyodunu
    // ~560 metreye çıkarıyordu: gölge tarağının ölçeği o boyda hücrelerde sabit
    // kalıyor, hücre bir bulut boyunu kapladığı için dikeyde tek değer taşıyor ve
    // bulut kenarlarına DİKEY KARALTI şeritleri basıyordu. Yüksek frekansta örnek
    // başına bağımsız: ışın boyunca ortalanıp yok oluyor.
    float3 hp = frac(float3(position.x, position.y - PlanetRadius, position.z)
                     * float3(0.1031, 0.1030, 0.0973));
    hp += dot(hp, hp.yzx + 33.33);
    float jitter = frac((hp.x + hp.y) * hp.z);

    // Tarağın ÖLÇEĞİ pozisyon başına oynar (±%40): faz kayması yalnız ilk adım
    // payını (≤30 m) kaydırıyor, üstel tarağın iri dişlerinin (240-480 m) kabuk
    // sınırlarına dokunamıyordu — bulutun içinde dünyaya çakılı DEV kavisli
    // halkalar duruyor, bulut aktıkça üstünde sürünüyordu. Ölçek oynayınca komşu
    // kolonlar farklı tarak taşır: dev kabuk uzamsal olarak çözülür.
    stepSize *= 0.6 + 0.8 * frac(jitter * 7.13);

    float accumulated = 0.0;
    float travelled = stepSize * jitter;

    // KONİ ÖRNEKLEME (Nubis/HZD): örnekler ışık yönünde tek çizgide değil, güneşe
    // doğru açılan bir koni içinde alınır. İki kazanç: (1) tek çizginin ürettiği
    // bantlaşma yerine komşu yoğunluklar ortalanır — yumuşak, ambient benzeri bir
    // gölge; (2) koni bulutun yan komşularını da tarar, yani gölge kütlenin
    // bütününü görür. Yayılım mesafeyle açılır: yakında dar (ince gölge korunur),
    // uzakta geniş (ortalama).
    const float3 coneKernel[8] =
    {
        float3( 0.38, -0.24,  0.15),
        float3(-0.31,  0.19, -0.36),
        float3( 0.12,  0.42,  0.28),
        float3(-0.27, -0.35,  0.24),
        float3( 0.44,  0.11, -0.31),
        float3(-0.18,  0.36,  0.41),
        float3( 0.29, -0.41, -0.19),
        float3(-0.40, -0.14,  0.33)
    };

    [loop]
    for (int i = 0; i < samples; i++)
    {
        travelled += stepSize;
        float3 spread = coneKernel[min(i, 7)] * travelled * 0.35;
        // Sonda GÖVDEYLE AYNI ALANI okumak zorunda. `deep` verilerek yalınlaştırıldı
        // ve kenarlarda koyu zar çıktı: sonda farklı bir alan görünce gölge geometriyle
        // hizalanmıyor. Aynı hata daha önce de yaşandı (ucuz yolun colWarp'ı atlaması).
        // Ucuzluk yalnız erozyon katmanında olabilir — o da gövdede zaten var.
        float probeSkip;
        accumulated += max(0.0, CloudDensity(position + lightDirection * travelled + spread,
                                            cheapSampling, viewDistance, stepSize,
                                            lightDirection, probeSkip)) * stepSize;
        stepSize *= 2.0;

        // Erken çıkış: optik derinlik 3.2'yi aştığında en zayıf sönüm terimi bile
        // (exp(-0.08·a)) kalan örneklerle ancak binde birler oynar — sonuç değişmez,
        // örnekler boşa yanar. Matematiksel eşdeğer, görsel etki yok.
        if (accumulated > 3.2) break;
    }

    // UZAK ÖRNEK: koninin son örneği çok ötede (sondanın 6 katı). Yakın taramada
    // görünmeyen komşu kütlelerin gölgesi buradan gelir — bulutlar birbirini
    // gölgeler, gökyüzü katman katman okunur. Tek örnek, ucuz yol.
    // Uzak komşu örneği yalnız ışın açıkken: kapanmış kuyrukta komşu bulutun
    // gölgesi ekrana ulaşmıyor.
    if (openness > 0.15)
    {
        float distantSkip;
        accumulated += max(0.0, CloudDensity(position + lightDirection * reach * 6.0,
                                             true, 1e9, reach, lightDirection, distantSkip))
                     * reach * 0.5;
    }

    // YAĞMUR SOĞURMASI (HZD): yağış olan yerde soğurma artırılır — yağmur bulutu
    // gözle görülür şekilde kararır, fırtına ağırlaşır. Sonda boyunca biriken optik
    // derinliğin kendisi "bu kolon kalın mı" sorusunun cevabı: soğurma artışı ince
    // bulutlara dokunmaz, yalnız kalın olanları ağırlaştırır.
    accumulated *= 1.0 + _CloudRainAbsorb * saturate(accumulated * 0.6);

    // Çoklu saçılma yaklaşımı: gerçek bulutta ışık defalarca saçılarak derine işler.
    // Tek terimli Beer-Lambert bunu veremez ve gövdeyi simsiyah bırakır.
    // Her terim daha zayıf sönümlemeyle daha derine taşıyan bir saçılma kuşağı.
    float scatter = saturate(_CloudMultiScatter);

    return exp(-accumulated * 1.4) * lerp(1.0, 0.45, scatter)
         + exp(-accumulated * 0.35) * 0.35 * scatter
         + exp(-accumulated * 0.08) * 0.20 * scatter;
}

/// Henyey-Greenstein: ışığın öne saçılması, güneş çevresindeki gümüş kenar
float CloudPhase(float cosAngle, float eccentricity)
{
    float g2 = eccentricity * eccentricity;
    return (1.0 - g2) / pow(abs(1.0 + g2 - 2.0 * eccentricity * cosAngle), 1.5) * 0.25;
}

/// rgb: saçılan ışık, a: örtme oranı
/// Bir bakış yönünün bulut katmanında karşılık geldiği dünya noktası.
///
/// Bulut bir yüzey olmadığı için tek bir doğru nokta yok; katmanın ortasıyla kesişim
/// yeterince iyi bir çapa — bulutlar orada yoğunlaşıyor ve hata katman kalınlığıyla
/// sınırlı kalıyor. Hem geçmişi yeniden konumlandırmak hem şimşeğin nereyi aydınlattığını
/// bulmak aynı soruyu soruyor, o yüzden tek yerde duruyor.
float3 CloudAnchor(float3 direction)
{
    float3 centre = float3(0.0, -PlanetRadius, 0.0);
    float3 origin = _WorldSpaceCameraPos - centre;

    float mid = (_CloudBottom + _CloudTop) * 0.5;
    float2 hit = CloudRaySphere(origin, direction, PlanetRadius + mid);

    float travel = hit.x > 0.0 ? hit.x : hit.y;
    return _WorldSpaceCameraPos + direction * max(travel, 1.0);
}

/// Yüksek irtifa katmanı: hacimsel bulutların ÜSTÜNDE, tek kesişimle çizilir.
/// Sirrus tüyleri, altokümülüs benekleri ve altostratus levhası aynı dokunun üç
/// kanalı; hava durumu hangisinin baskın olduğunu seçer. Işın yürüyüşü yok —
/// katman kilometrelerce yukarıda ve birkaç yüz metre kalınlıkta, hacimsel çözüm
/// oraya harcanmaz (PDF'in tercihi de bu).
float4 CloudHighLayer(float3 origin, float3 direction, float maxDistance,
                      float3 lightDirection, float3 lightColor, float ambient)
{
    // Ufuk sınırı SERT DEĞİL. 0.02 (1.15°) eşiği katmanı tek pikselde kesiyordu:
    // eşikte alfa hâlâ 0.15·kapsama olduğu için gökte jilet gibi yatay bir çizgi
    // kalıyordu. Kesme yerine sönme; gerçek sınırı pus zaten koyuyor.
    if (_HighCloudAmount <= 0.001 || direction.y <= 0.0) return 0.0;
    float horizonFade = smoothstep(0.0, 0.06, direction.y);

    float3 center = float3(0.0, -PlanetRadius, 0.0);
    float3 position = origin - center;
    float altitude = length(position) - PlanetRadius;
    if (altitude > _HighCloudAltitude) return 0.0;   // katmanın üstündeyiz

    float2 hit = CloudRaySphere(position, direction, PlanetRadius + _HighCloudAltitude);
    float travel = hit.y;
    if (travel <= 0.0 || travel > maxDistance) return 0.0;

    float3 world = origin + direction * travel;

    // Rüzgârla kayar, alt katmandan bağımsız hızda: yüksek rüzgâr daha güçlüdür.
    float2 uv = (world.xz + _CloudWind.xz * 2.3) * _HighCloudScale;
    float3 layers = SAMPLE_TEXTURE2D(_CloudHighNoise, sampler_CloudHighNoise, uv).rgb;

    float cirrus = layers.r;
    float alto = layers.g;
    float sheet = layers.b;
    float shape = _HighCloudType < 0.5
        ? lerp(cirrus, alto, saturate(_HighCloudType * 2.0))
        : lerp(alto, sheet, saturate((_HighCloudType - 0.5) * 2.0));

    float coverage = saturate(shape * (0.35 + _HighCloudAmount) - (1.0 - _HighCloudAmount) * 0.35);
    if (coverage <= 0.001) return 0.0;

    // Ufka doğru yatık ışın katmanı eğik keser: aynı yolda daha çok madde. Ayrıca
    // katman ufka yaklaştıkça pusla kapanır.
    float grazing = saturate(0.25 / max(0.05, direction.y));
    float alpha = saturate(coverage * 0.85 * (0.6 + 0.4 * grazing));

    // Aydınlatma ucuz: ince katman ışığı büyük ölçüde geçirir, öne saçılma güneşin
    // çevresini parlatır. Hacimsel modelin tamamı burada gereksiz.
    float forward = saturate(dot(direction, lightDirection));
    float3 lit = lightColor * (0.55 + 0.75 * pow(forward, 6.0)) + ambient;

    // Hava perspektifi: uzak yüksek bulut da atmosferin rengine gömülür. Karışılan
    // renk YÖNE BAĞLI havanın kendisi — burada tek düz bir taban renk vardı ve şafakta
    // yüksek bulut her yönde aynı griye karışırken hemen altındaki hacimsel katman
    // güneşe doğru altına karışıyordu; iki katman arasında hem renk hem çizgi farkı
    // duruyordu. Alçak katman bu düzeltmeyi almıştı, yüksek katman atlanmıştı.
    float haze = saturate(travel / max(1.0, _CloudHazeDistance * 1.6));
    haze *= haze;
    lit = lerp(lit, AirColor(direction), haze);
    alpha *= 1.0 - haze * 0.85;

    alpha *= horizonFade;
    return float4(lit * alpha, alpha);
}

float4 RaymarchClouds(float3 origin, float3 direction, float2 pixel, float maxDistance)
{
    float3 center = float3(0.0, -PlanetRadius, 0.0);
    float3 position = origin - center;

    float2 span = CloudSpan(position, direction);

    // TEK ÇIKIŞ: hacimsel katmanla kesişme olmasa bile fonksiyon erken dönmez —
    // yüksek irtifa katmanı (sirrus) sondaki kompozitte çiziliyor ve erken dönüş
    // onu tamamen yutuyordu: katmanın çok üstünde/altında, ufka yakın ışınlarda ve
    // sahne yakında kesiyorsa gökyüzündeki tüm yüksek bulutlar kayboluyordu.
    bool marchable = span.x >= 0.0 && span.y > span.x;

    float start = max(0.0, span.x);

    // Işının erişebildiği en uzak mesafe. Adım mesafeyle üstel büyüdüğü için kapalı
    // biçimi var: D · (e^(taban · bütçe / D) − 1). Zirveden bakıldığında bulut
    // katmanının kendi ufku ~80 km; menzil oraya yetişmezse deniz ufka varmadan bitiyor.
    float reach = _CloudStepDouble
                * (exp(CLOUD_STEP_BASE * max(8.0, _CloudSteps) * 4.0 / _CloudStepDouble) - 1.0);

    // Hava perspektifi yürüyüş bitmeden kapanmalı; yoksa yürüyüşün sınırı çıplak kalıyor
    // ve kameranın etrafındaki o küre katmanı kestiği yerde ekrana deniz ufku gibi
    // dümdüz bir çizgi bırakıyor. Menzil kısa tutulduğunda bu kural her şeyi soldurur —
    // ikisi birlikte ayarlanmak zorunda.
    float hazeDistance = min(_CloudHazeDistance, reach * 0.85);

    // Sahne geometrisi ışını kesiyorsa, ya da perspektifin tamamen kapandığı mesafenin
    // ötesinde başlıyorsa yürümeye değmez.
    marchable = marchable && start <= maxDistance && start <= hazeDistance;

    // Yürüyüş katmandan çıkınca ya da hava perspektifi tamamen kapanınca biter. Ayrı bir
    // mesafe sınırı yok: adım boyu mesafeyle büyüdüğü için iterasyon bütçesi kendiliğinden
    // onlarca kilometreye yetiyor, sabit bir tavan koymak yalnızca ufku kesiyordu.
    float distance = min(span.y - start, hazeDistance);
    distance = min(distance, maxDistance - start);
    marchable = marchable && distance > 0.0;

    // Geçiş kuşağı geniş tutulur: dar olunca batımdan geceye zıplama oluyor
    float sunUp = smoothstep(-0.22, 0.12, _SunDirection.y);

    // Güneş ufka yakınken ışık yatay geliyor: karşı taraftaki bulutlara ne doğrudan
    // ışık ne de aydınlık gökyüzü düşüyor. Yalnızca rengi soğutmak yarım iş — o taraf
    // aynı zamanda belirgin şekilde karanlık olmalı.
    float3 sunward = normalize(float3(_SunDirection.x, 0.0, _SunDirection.z) + 0.0001);
    float3 viewFlat = normalize(float3(direction.x, 0.0, direction.z) + 0.0001);
    // Geçiş bandı neredeyse tüm açı aralığına yayılır: gökyüzü güneşten karşı ufka
    // kadar kademeli koyulaşır. Dar bir banda sıkıştırmak ortada görünür bir dikiş
    // bırakıyordu.
    float towardSun = smoothstep(-0.85, 0.85, dot(viewFlat, sunward));

    // Tam yukarıda yatay yön tanımsız: xz sıfıra yaklaşınca normalize komşu piksellerde
    // sıçrıyor ve zenith'te ışınsal bir tekillik bırakıyor. Yön etkisi ufka yaklaştıkça
    // güçlenir, tepede nötre döner — gökyüzünün tepesi zaten iki tarafa eşit uzaktır.
    towardSun = lerp(0.5, towardSun, saturate(length(direction.xz) * 3.0));

    float horizonSun = 1.0 - saturate(abs(_SunDirection.y) / 0.3);
    // Taban 0.18 → 0.75. Bu terim BAKIŞ yönüne göre doğrudan ışığı kısıyordu: güneşe
    // bakmıyorsan şafakta bulut 5.5 kat sönük çiziliyordu. Oysa güneşin buluta ulaşıp
    // ulaşmadığı bakış yönünden bağımsızdır — gerçek şafakta gökyüzünün TAMAMINDAKİ
    // bulutlar yanar, yalnız güneş tarafındakiler değil. Görüş yönüne bağlı olan şey
    // faz fonksiyonu (gümüş kenar), o da ayrıca hesaplanıyor; burada ikinci kez
    // kısmak aynı işi iki kez yapmaktı.
    // Taban 0.18 → 0.75 → 0.50. 0.18'de güneşe bakmıyorken bulut 5.5 kat sönük
    // çiziliyordu (gerçek şafakta gökyüzünün TAMAMINDAKİ bulutlar yanar); 0.75 ise
    // iki yarıyı ayıramıyor, ay tarafındaki bulutlar da aydınlık kalıyordu. Ortası.
    float directionalDim = lerp(1.0, lerp(0.50, 1.0, towardSun), horizonSun);


    // Yön ortada değiştirilir; renk sürekli harmanlandığı için sıçrama görünmez
    float3 lightDirection = sunUp > 0.5 ? _SunDirection : _MoonDirection;

    // Işığın atmosferden süzülmüş rengi TimeOfDay'de hesaplanıyor; burada yeniden
    // kızartıp parlatmak aynı işi iki kez yapmak oluyordu. Sonucu da bozuyordu:
    // güneşe bakan yönde öne saçılma tepesiyle çarpılınca üç kanal birden parlaklık
    // sınırına dayanıp renk beyaza kayıyor, uzaktaki bulutlar kızıl kalıyordu.
    float horizonLight = 1.0 - saturate(abs(_SunDirection.y) / 0.3);
    float3 warmed = _CloudSunColor.rgb;

    // Renk sürekli harmanlanır: sert dallanma batımda ani sıçrama yapıyordu
    // Renk bir kez uygulanır, parlaklık ayrı. Işığın rengiyle bulut aydınlık rengini
    // çarpmak ikisi de turuncuyken kırmızı kanalı bire çıkarıyor; ton eşleme orada
    // doygunluğu düşürdüğü için bulut ne kadar aydınlıksa o kadar beyazlıyordu.
    float brightness = max(_CloudBrightColor.r,
                           max(_CloudBrightColor.g, _CloudBrightColor.b));

    // Güneşe yakın yönde ışık en kısa yolu kat eder, sarıyı taşıyan yeşil kanal henüz
    // tükenmemiştir: o bölgede aydınlatma altın rengine kayar. Bunu sonradan çarpımla
    // yapmak imkânsız — çarpım rengi koyultabilir ama açamaz, yeşil kanalı yükseltmek
    // gerekiyor. Rengin kaynağı ışığın kendisi olmalı.
    // Altın açık yazılır; gök örneğinden türetmek denendi ve geri alındı — aynı gerekçe
    // `HeightFog.hlsl`'de: `Atmosphere` temiz atmosferi çiziyor, şafağın altını ise
    // aerosolün işi. Gök tonu (1.00, 0.42, 0.16) huzme tonuna (1.00, 0.26, 0.02) o kadar
    // yakın ki terim etkisiz kalıyor, altın hiç doğmuyordu.
    float3 golden = float3(1.0, 0.9, 0.55);
    float3 nearSunTint = lerp(warmed, golden, pow(saturate(towardSun), 3.0) * horizonLight);

    float3 sunLight = nearSunTint * brightness;
    float3 moonLight = _CloudMoonColor.rgb * brightness * 0.25;
    float3 lightColor = lerp(moonLight, sunLight, sunUp);

    // Öne saçılma tepesi yumuşatıldı: g 0.75'te güneşe bakarken 7 kat parlatıyordu.
    // Gümüş kenar korunur ama beyaz konturu yaratan doyma olmaz.
    float cosAngle = dot(direction, lightDirection);
    // Taban aydınlatma korunur; rim yalnızca güneşe bakan tepeyi ne kadar
    // güçlendireceğini belirler. Taban düşük tutulunca bulutlar kararıyordu.
    // Geriye saçılma gerçek bir olgu ama zayıftır. 0.6 katsayısıyla güneşin tam
    // karşısında ikinci bir parlaklık tepesi doğuyor ve şafakta ay tarafındaki bulutlar
    // aya yaklaştıkça parlıyordu — olması gerekenin tam tersi.
    float peak = max(CloudPhase(cosAngle, 0.45), CloudPhase(cosAngle, -0.2) * 0.15);
    float phase = 1.0 + saturate(_CloudRimStrength) * max(0.0, peak - 1.0);

    // Alacakaranlık ışığı: güneş ufka inince ışınları bulut katmanının ALTINA girer ve
    // tabanları alttan boyar — batış kızıllığı bulutun aldığı ışıktır. Eski hâli sonucu
    // renkle ÇARPIYORDU: batışta bulut zaten karanlık, sıfıra yakını kızılla çarpmak
    // siyah bırakır; kızıllık bu yüzden hiç görünmüyordu. Işık eklenir, çarpılmaz.
    //
    // Çift bant: dar çekirdek güneş çevresine yangını, geniş bant güneş yarısına hafif
    // ılıklığı verir. Karşı yarı sıfır alır — orası Dünya'nın gölgesi, yön karartması
    // zaten soğuk ve karanlık tutar: kafanı çevirince iki ayrı dünya. Zaman penceresi
    // HorizonFactor'dan (_CloudDuskStrength'e katlanmış geliyor): batıştan sonra da
    // bir süre yaşar, yüksek bulutlar en son söner.
    // Yön kapısında taban var: bulutun aldığı ışık, oyuncunun ona NEREDEN baktığıyla
    // değişmez — batışta bulut her açıdan kızıldır. Kapı tamamen bakış azimutuna
    // bağlanınca aşağı bakan oyuncu için (yatay bileşen yok, towardSun nötr 0.5'e
    // düşer) kızıl sıfırlanıyordu: zirveden görünen deniz, içine uçunca soluyordu.
    // Güneş tarafı yine daha güçlü yanar; karşı taraf directionalDim ile zaten soğuk.
    float duskCore = pow(saturate(towardSun), 6.0);
    float duskWide = saturate(towardSun * 1.4 - 0.4) * 0.25;

    // ÇAKIŞMA KISMEN GERİ ÇEKİLİR. Bu terim, doğrudan ışık ham kızıl huzmeyken
    // yazılmıştı. `nearSunTint` artık güneşe doğru zaten altına kayıyor (yukarıda) ve
    // ikisi aynı olguyu — şafağın sıcak ışığını — iki kez ekliyordu: güneş 5.5°'deyken
    // altın payı 0.679, dusk katsayısı 0.634, toplam kırmızı kanalda 1'i aşıp ton
    // eşlemede beyaza gidiyordu.
    //
    // Pay YARIM: tam geri çekince ek 0.634 → 0.203'e düşüyor ve ay tarafındaki 0.106
    // ile neredeyse eşitleniyor — güneş tarafı ile karşı taraf aynı renge geliyordu.
    // Yön farkı bu terimin asıl işi; yalnız taşma payı alınır.
    float duskOverlap = 1.0 - 0.55 * pow(saturate(towardSun), 3.0) * horizonLight;

    float3 duskLight = _CloudDuskTint.rgb
                     * (saturate(_CloudDuskStrength) * (0.25 + duskCore + duskWide)
                        * 0.9 * duskOverlap);

    // Işığın erişimi güneşin yüksekliğine bağlı: güneş ufuktayken ışınlar YATAY gelir,
    // tepeler ve güneşe bakan yüzler de yanar — buluta yaklaşınca kızıllık kaybolmaz,
    // çünkü kızıl bulutun kendi ışığıdır, aradaki havanın boyası değil. Güneş
    // yükseldikçe erişim tabanlara çekilir (alttan vuran ışık ancak alçak güneşte var).
    float duskReach = saturate(_SunDirection.y * 6.0);

    // Batışta gökten gelen dağınık ışık da ölür: bulut gövdesi koyulaşır ve kızıl
    // KONTRASTLA patlar. Ambient aynı kalınca gri, kızılın üstüne binip onu somona
    // sulandırıyordu.
    float duskContrast = 1.0 - 0.55 * saturate(_CloudDuskStrength);

    int steps = (int)max(8.0, _CloudSteps);

    // Bayer kayması tarama fazını kaydırmalı, pencereyi kısaltmamalı. Başlangıca
    // eklenip bitişe eklenmeyince her piksel yalnızca (distance - dither) kadar
    // yürüyordu: bir tam adımlık bulut, pikselin Bayer hücresine göre eksik kalıyordu.
    // Bunun yerine yürüyüş start'ta başlar ve kayma ilk adımın boyu olarak uygulanır —
    // pencere her pikselde tam, faz yine piksele göre farklı.
    //
    // Faz payı ORANDIR, metre değil: taban adıma (20 m) bağlı sabit faz, uzakta
    // adım 100-240 m'ye büyüyünce dilimlerin yanında kayboluyordu ve uzak bulutlar
    // soğan zarı gibi eş değer halkaları giyiyordu. Oran, fazı her mesafede yerel
    // adımın tamamına yayar — bant grene kırılır, gren zamansal birikimde erir.
    // Faz TAM adımı kapsar ve 4 px tekrarı kırılır. Bayer tek başına 16 seviye ve
    // (0.45 kısmıyla) faz uzayının yarısından azı: hata uzamsal olarak korelasyonlu
    // kalıyor, yumuşak kenar kuyruğunda eş-mesafe halkası olarak okunuyordu. Sürekli
    // piksel hash'i seviyeler arasını doldurur, tekrarı bozar. Gren riski eski
    // dönemin çok altında: yamuk kural + kuyruk incelmesi kalıntıyı O(h²)'ye
    // düşürdü, faz yalnız o kalıntının işaretini dağıtıyor.
    float2 cell = floor(pixel);
    float pixelHash = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);

    // Faz genliği MESAFEYLE söner. Her tam-çözünürlük pikseli kendi ışınını taşıyor
    // (harman yok — bilinçli mimari), dolayısıyla faz farkı doğrudan piksel-piksel
    // alfa farkıdır: yakında adım 13 m, yamuk kalıntısı kıl kadar → zararsız;
    // uzakta adım 200+ m, aynı faz farkı KENAR BENEKLENMESİ olarak okunuyordu.
    // Uzakta halka riskini yamuk kural ve yumuşak ışık kapısı zaten dörtte bire
    // indirdi, faza gerek kalmıyor.
    float phaseFade = lerp(1.0, 0.12, saturate((start - 8000.0) / 20000.0));
    float dither = frac(CloudBayer(pixel) + pixelHash * 0.0625)
                 * _CloudDither * phaseFade;
    float3 samplePoint = position + direction * start;

    float transmittance = 1.0;
    float3 scattered = 0.0;
    float prevDensity = 0.0;
    float cachedLit = -1.0;   // gölge sondası önbelleği (bkz. aşağıdaki adımlama)

    // İKİ KADEMELİ YÜRÜYÜŞ (Nubis/HZD): ışın, buluta değene kadar UCUZ örnek alır —
    // aşındırma ve kafes kırıcı okumalar yapılmaz, adım iki katıdır. İzo-yüzeye
    // değince bir adım GERİ gidilir (ucuz adımda atlanmış olabilecek ince yapı
    // kaçmasın) ve tam örneklemeye geçilir. Arka arkaya birkaç boş örnekten sonra
    // tekrar ucuza dönülür. Gökyüzünün büyük kısmı boş olduğu için kare bütçesinin
    // çoğu bu kademede harcanıyordu.
    bool coarsePass = true;
    int emptyRun = 0;

    float travelled = start;


    // Yürüyüşün gerçek sınırı. Adım sayısı tek başına sınır değildi: boş ışın hep kaba
    // adımla gittiği için hesaplanan mesafenin iki katını, bulutun içindeki ışın ince
    // adımla gittiği için yarısını yürüyordu. Mesafe yalnızca adım boyunu belirliyor,
    // kimse ona uymuyordu — hem maliyet sınırı tutmuyor hem bitiş noktası ışının
    // üzerindeki bulut içeriğine göre oynuyordu.
    float limit = start + distance;

    // İlk buluta olan uzaklık. Hava perspektifi bunu ister: baktığın şey ne kadar uzakta.
    // Yürüyüşün nerede bittiği içeriğe göre basamak basamak değişiyor, ışının katmana
    // giriş açısı da yalnızca yüksekliğe bağlı olduğu için o basamaklar gökyüzüne
    // eksen etrafında simetrik, iç içe halkalar olarak biniyordu.
    float firstHit = -1.0;

    // Bütçe menzilin adım boyuna oranını karşılamalı: 20 katlık yürüyüşü 6 katlık adımla
    // yürümek yaklaşık 3.3 kat iterasyon ister. Dik ışınlar katmandan çıkıp erken duruyor,
    // bedeli yalnızca ufka yakın yatık ışınlar ödüyor.
    [loop]
    for (int i = 0; i < steps * 5; i++)   // 5: kuyruk incelmesinin yediği payı karşılar
    {
        if (!marchable || travelled >= limit) break;

        // Adım mesafeyle büyür: yakında ince, uzakta kaba. Ekranda kapladığı yer sabit
        // kalır, dolayısıyla yakının yumuşaklığı da ufkun menzili de aynı bütçeden çıkar.
        float baseStep = CLOUD_STEP_BASE * (1.0 + travelled / max(500.0, _CloudStepDouble));

        // DERİNDE ADIM BÜYÜR. Bir dilimin ekrana katkısı o noktadaki transmittance ile
        // çarpılıyor: ışın kapanmaya başladıktan sonra oradaki entegrasyon hatası da
        // aynı çarpanla küçülüyor. Yani derin kuyruğu KESMEK görünür (denendi, kalite
        // gitti), ama aynı kuyruğu daha kaba adımlarla yürümek görünmez. Katman içinde
        // ışın 2-4 km yol alıyor ve o yolun çoğu bu kuyrukta geçiyordu.
        baseStep *= 1.0 + 2.0 * saturate((0.65 - transmittance) / 0.6);

        float threshold = _DensityScale * 0.05;

        // Adım, bir önceki örneğin yoğunluğuna göre **kademesiz** incelir.
        //
        // Önceki hali iki modluydu: kaba tarama bulut bulunca ince moda geçiyor, sınırı
        // ikili aramayla buluyor, boşlukta tekrar kabaya dönüyordu. Yoğunluk bulutun
        // içinde eşiği defalarca kesiyor ve her kesişimde adım boyu sıçrıyor; entegrasyon
        // hatası da onunla sıçrayınca bulutun içi kat kat kabuklara ayrılıyordu — her
        // geçiş bir dikiş. Sürekli bir fonksiyon o dikişleri tamamen ortadan kaldırıyor.
        float nominalStep = baseStep;

        // Kuyruk incelmesi: önceki örnek zayıf-ama-sıfır değilse (yumuşak kenar
        // gradyanındayız) adım yarıya iner. Halkaların kaldığı yer tam burası —
        // 20 km'de kuyruk ~500 m, adım ~124 m, yani kafeste yalnız 4 nokta;
        // yamuk kuralı hatayı kareye düşürse de 4 noktada görünür kalıyordu.
        // Gövde ve boşluk aynı maliyette: incelme yalnız kuyruk bandında.
        // Örnekleme ölçeği (nominalStep, mip seçimi) değişmez — sadece ilerleme.
        // YAKIN ALANLA SINIRLI: incelme iterasyon bütçesinden yiyor (bütçe sabit),
        // her mesafede açıkken ışın erken tükeniyor ve UZAK BULUTLAR ÇİZİLEMİYORDU.
        // Kuyruk ekranda zaten yakında geniş; uzakta bir piksele sıkışıyor.
        // ŞEFFAFLIK KAPISI: incelme yalnız ışın HENÜZ AÇIKKEN anlamlı. Halkalar
        // bulutun ön kenarında, alfa daha birikmemişken görünüyordu; oraya girildikten
        // sonra hata zaten transmittance ile çarpılıp küçülüyor. Kapısız hâli ince
        // bulutun İÇİNDE de yarım adım kullanıyordu — orada tüm yol "zayıf yoğunluk"
        // bandına düşüyor ve örnek sayısı iki katına çıkıyordu. Kalın bulutta 100 FPS,
        // incede 40 FPS farkının sebebi buydu.
        // İncelme ŞEFFAFLIKLA KADEMESİZ zayıflar. İkili kapı (transmittance > 0.8)
        // denendi: ince bulutta hızlı ama kenarlarda halka geri geldi — kapı bir
        // dikiş, dikiş de tam halkanın doğduğu yer. Kademeli hâli, hatanın ekrana
        // ulaşma ağırlığını (transmittance) izler: ışın açıkken tam incelme, kapandıkça
        // sıfıra iner. İnce bulutun içi zaten kapanmış bölgedir, orada incelme yok.
        float openness = saturate((transmittance - 0.30) / 0.55);
        float tailRefine = (prevDensity > 0.0 && prevDensity < _DensityScale * 0.35
                            && travelled < 12000.0)
                         ? lerp(1.0, 0.5, openness) : 1.0;
        float step = min(nominalStep * (coarsePass ? 2.0 : tailRefine), limit - travelled);

        // İlk adım Bayer kayması kadar kısa: tarama fazı piksele göre kayar, pencere
        // her pikselde tam kalır. Mip yine anma adımına bakar, kırpılmışa değil.
        if (i == 0 && dither > 0.0) step = min(step, max(nominalStep * dither, 1.0));

        // EROZYON DERİNDE DE OKUNUR. Ucuza düşürmek denendi ve geri alındı: erozyon
        // yoğunluğu yalnızca AZALTAN bir işlem (Remap(d, e·s, 1, 0, 1)), atlanınca
        // kuyruk boyunca yoğunluk olması gerekenden yüksek kalıyor, optik derinlik
        // fazla birikiyor ve bulutlar toptan kararıyordu. Derinde ucuzlatılabilecek
        // olanlar yalnızca yoğunluğa yön vermeyenler (sn2, colBump, sec4).
        float slabSkip;
        float density = CloudDensity(samplePoint, coarsePass, travelled, nominalStep,
                                     direction, slabSkip,
                                     !coarsePass && transmittance < 0.35);

        // Ucuz kademede bulut bulundu: bir adım geri dön, tam örneklemeye geç.
        // Bu örnek biriktirilmez — geri döndüğümüz yerden tam kademeyle yeniden
        // örneklenecek.
        if (coarsePass && density > 0.0)
        {
            coarsePass = false;
            emptyRun = 0;
            prevDensity = 0.0;
            samplePoint -= direction * step;
            travelled -= step;
            continue;
        }

        // Tam kademede arka arkaya boşluk: ucuza dön. Sayaç küçük tutulur, çünkü
        // bulut içindeki kısa boşluklarda mod değiştirmek entegrasyonu kesintiye
        // uğratır; 6 örnek bir bulut aralığı için yeterli tampon.
        if (!coarsePass)
        {
            emptyRun = density > 0.0 ? 0 : emptyRun + 1;
            if (emptyRun >= 6) { coarsePass = true; emptyRun = 0; }
        }

        // Dilimlenme değişmezi: adım × yoğunluk ≤ 0.30 (adım opaklığı ≤ ~%26 —
        // teraslama eşiğinin altı). 0.18 denendi: dilimi zaten kesiyordu ama 3 km'den
        // itibaren tepe yoğunluğu tıraşlayıp bulutları kontrastsız beyaza yıkadı;
        // halkaların asıl kaynağı da bu değil, ışık yürüyüşü kabuklarıydı (yukarıda
        // kare-değişken fazla eritildi). Kelepçe yakında etkisiz, uzakta dilimi
        // yapısal olarak imkânsız kılar.
        // 0.45: menzil boyası ölçümü 0.30'un uzak yoğunluğu dörtte bire tıraşlayıp
        // örtü ufkunu erken sildiğini gösterdi; %36'lık adım opaklığı teras eşiğinin
        // hâlâ altında.
        if (density > 0.0) density = min(density, 0.45 / max(1.0, step));

        // Piksel tahılı DENENDİ VE SÖKÜLDÜ: temporal birleştirme bilinçli HARMANSIZ —
        // tahılın eriyeceği ortalama yok, kenarlar fena pikselleşti.

        // Boş bölgede sıçra.
        //
        // Maliyetin kaynağı bulut değil boşluk: yoğun havada ışın birkaç örnekte
        // opaklaşıp kırılıyor, seyrek havada ise bütçesinin tamamını boş katmanda
        // yürüyor. Ölçüm bunu doğruluyor — kapsama %90'da 35, %40'ta 21 kare.
        //
        // Sıfır yoğunluklu bir aralıkta adım boyu sonucu değiştirmez: sıfır katkı, adım
        // ne olursa olsun sıfır. Ne kadar sıçranabileceği de tahmin değil — bölge
        // haritası bilerek belli bir texel boyunda okunuyor, o ölçeğin altında yapı
        // taşımıyor, dolayısıyla bir noktada kapsama sıfırsa yarım texel boyunca da
        // sıfırdır. Pay bırakmak için onun altında kalınıyor.
        //
        // Örnekleme ölçeği (nominalStep) değişmiyor, yalnızca ilerleme: mip seçimi ona
        // bağlı olduğu için sıçrama okunan alanı da değiştirseydi görüntü kayardı.
        // Dilim dışı da sıçrar. Sıfır katkı veren bölge, sıçrama gerekçesi bakımından
        // boş bölgeden farklı değil; mesafeyi CloudDensity analitik olarak veriyor ve
        // komşu kolona giremeyecek şekilde sınırlıyor.
        if (density == 0.0 && slabSkip > nominalStep) density = -0.5;

        if (density < 0.0)
        {
            // Kaba harita "boş" dediyse sıçrama uzun; ince eleme daha temkinli;
            // dilim elemesi kendi analitik mesafesini taşır.
            float jump = density < -1.5 ? CloudSkipCoarseMeters
                       : density > -0.9 ? slabSkip
                       : CloudSkipMeters;
            density = 0.0;
            {
                // Sıçrama TAM ADIM KATLARI hâlinde: serbest uzunlukta sıçramak ışının
                // örnekleme kafesini kaydırıyordu — komşu piksellerden biri sıçrayıp
                // öbürü sıçramayınca buluta 300 m'lik kuantalarla giriliyor, giriş
                // noktası eş-mesafe kabuklarına oturuyordu (soğan halkası) ve piksel
                // başına farklı düşünce benek çıkıyordu. Katlarla sıçrayınca faz
                // korunur: sıçrayan ışın, adım adım yürümüş gibi aynı kafeste kalır.
                float whole = max(1.0, floor(jump / max(1.0, nominalStep)));
                step = min(whole * nominalStep, limit - travelled);
            }
        }

        // Katkı eşiksiz birikir. Eşiğin altındakini atmak yoğunluk alanını belirli bir
        // seviyede kesip bir **eş-yüzey** çıkarıyordu: bulut hacim değil o yüzeyin içi
        // olarak çiziliyor, kenarı da yüzeyi belirleyen örneğin kafesine oturup
        // köşeleniyordu. Kaba örnek şekil dokusunu 163 metre çözünürlükte okuduğu için
        // kenarlar o boyda düz parçalara bölünüyordu. Sıfır yoğunluk zaten sıfır katkı
        // verir; elemek yalnızca yumuşak geçişi yok ediyordu.
        // YAMUK KURALI: dilim, iki uç örneğin ORTALAMASIYLA toplanır — dikdörtgen
        // kural (dilim başındaki yoğunluk) hatayı adım boyuyla orantılı ve uzamsal
        // olarak korelasyonlu bırakıyordu; yumuşak kenar kuyruğunda bu doğrudan
        // eş-mesafe halkası demek. Yamukta hata mertebesi kareye düşer, ek örnek
        // ve gren yok. Giriş dilimi kendiliğinden yarım derinlik alır (prev=0),
        // çıkış dilimi de yarım kuyruğunu kapatır — elle yazdığım giriş yaması
        // bunun özel hâliydi.
        float effDensity = 0.5 * (prevDensity + density);
        prevDensity = density;

        if (effDensity > 0.0)
        {
            density = effDensity;
            if (density > threshold && firstHit < 0.0) firstHit = travelled;

            // Işık yürüyüşü en pahalı kısım; arkadaki katkı görünmezse atlanır.
            // Eşiğin altındaki silik katkı da yalnızca ambient'le geçer: kenarı yumuşatan
            // pay o kadar küçük ki ayrı bir ışık yürüyüşünü hak etmiyor, maliyet sabit kalır.
            // Kapı YUMUŞAK: sert eşik (transmittance > 0.15) aydınlatmayı uzayda bir
            // kabuk boyunca bir anda kesiyor; o kabuğun yeri piksele göre kaydığı
            // için bulut içinde ikinci bir halka ailesi doğuruyordu. Maliyet aynı.
            // Kapı DAR (0.04-0.12): geniş bant (0.10-0.22) doğrudan güneş ışığını
            // yarı yolda kısıp bulutları ambient'e bırakıyordu — renk ve sıcak/soğuk
            // ayrımı siliniyordu. Kapının varlık sebebi halka avıydı; halkaların
            // gerçek kaynağı (sıçrama kafesi + mercek sapması) çözüldü, kapı yalnız
            // en derin iç bölgede süreksizliği önlemek için kalıyor.
            // Sonda eşiği ESKİ YERİNDE. Yükseltmek (0.10-0.30·_DensityScale) denendi:
            // ince ve uzak bulutlar tam o bantta yaşıyor, doğrudan güneşi kaybedip
            // ambient'e kalıyorlar — ufuk gri bir şeride dönüyordu. Kazanç oradan
            // değil, dikey dilim elemesinden gelir.
            float probeGate = smoothstep(threshold, threshold * 3.0, density);
            float gate = smoothstep(0.04, 0.12, transmittance);

            // Sonda UZAKTA iki örnekte bir: yakında (≤5 km) gölge ekranda büyük ve
            // her örnek yürür — kalite orada belli oluyor. Uzakta adım zaten 100+ m,
            // gölge alt-piksel: ara örnek öncekini taşır. Sıçramadan sonra önbellek
            // geçersiz (arada kilometreler olabilir).
            float lit = 0.0;
            if (probeGate > 0.0 && gate > 0.0)
            {
                // Tam örnekleme yalnız ön yüzde: birincil ışının alfası 0.3'ü geçene
                // kadar sonda erozyonu da okur, sonra ucuza düşer (HZD).
                // Sonda önbelleği DERİNLİKLE seyrelir. Gölge alçak frekanslı bir alan;
                // ışın kapandıkça hem katkısı hem hatası transmittance ile çarpılıp
                // küçülüyor, dolayısıyla derinde birkaç örnekte bir hesaplamak
                // görünmüyor. Sonda örnek başına ~35 doku okuması yapıyor, yani bu
                // yürüyüşün en pahalı kalemi.
                int probeMask = transmittance > 0.6 ? 0
                              : transmittance > 0.35 ? 1 : 3;

                if (cachedLit < 0.0 || (travelled < 5000.0 && probeMask == 0)
                    || (i & max(probeMask, 1)) == 0)
                    cachedLit = CloudLightTransmittance(samplePoint, lightDirection,
                                                        transmittance <= 0.7, travelled,
                                                        openness);
                lit = cachedLit * gate;
            }
            else cachedLit = -1.0;

            // Işık katkısı eşikte sıfırdan başlar. Sert açılışta eşiğin bir tık üstündeki
            // örnek aydınlanıyor, bir tık altındaki yalnızca ambient alıyordu: eşik yüzeyi
            // boyunca parlaklıkta bıçak gibi bir sıçrama oluşuyor ve o yüzey gökyüzüne
            // yayılmış ince bir zar olduğu için kenarından bakınca örümcek ağı gibi
            // görünüyordu. Yumuşak açılış maliyeti değiştirmiyor: yürüyüş yine yalnızca
            // eşiğin üstünde yapılıyor, yalnızca katkısı sıfırdan büyüyor.
            float light = lit * probeGate;

            float fraction = CloudHeightFraction(samplePoint);

            // Yerel yoğunluk 0-1 aralığına indirgenir. Adım boyuna bağlı formüller
            // adım sayısı değişince davranışı bozuyor.
            float local = saturate(density / max(0.00001, _DensityScale));

            // Powder yalnızca hafif bir koyulaştırma; enerjiyi sıfıra indirmez.
            // İnce kenarın katkısı zaten alfa ile küçülüyor, enerjiyi de kısmak
            // çift sayım oluyor ve kenarları simsiyah bırakıyordu.
            // BEER'S-POWDER (Nubis/HZD): Beer yasası tek başına bulutun ışığa BAKAN
            // yüzeyini olduğundan parlak bırakır — yüzeye yakın noktaya çevreden
            // saçılan ışık az gelir, derindeki noktaya çok. Sonuç gerçek bulutlarda
            // ışığa bakan kenarların KOYU olması; bu terim olmadan bulutlar
            // yıkanmış beyaz görünüyordu.
            //
            // Etki BAKIŞA BAĞLI: yalnız güneşi arkamıza alıp aydınlık yüzü
            // gördüğümüzde okunur (PDF s.66). Güneşe bakarken sıfıra iner, orada
            // öne saçılma (gümüş kenar) hâkimdir.
            float powderDepth = 1.0 - exp(-local * 3.0);
            float powderView = saturate(dot(direction, -lightDirection) * 0.5 + 0.5);
            float powder = lerp(1.0, powderDepth,
                                saturate(_CloudPowderStrength) * powderView);

            // Bulut altları gökyüzünden ve yerden yansıyan ışıkla aydınlanır,
            // tamamen kararmaz. Yoğunluk payı: alt bölgede yoğun örnek ambient'i
            // AŞAĞI çeker — örtünün altı kalın yerlerde koyu sarkar, incelerde
            // aydınlık sızdırır. Bu olmadan karışım yalnız kottan geliyordu: aynı
            // yükseklikteki her kolon aynı renkti ve %100 kapsamada tabandan bakan
            // oyuncu bulutsuz gibi DÜMDÜZ tek renk tavan görüyordu.
            // Yoğunluk payı ÖLÇÜLÜ (0.10): 0.3'te, zaten var olan yoğun-çekirdek
            // kontrastıyla (aşağıda ambient ×0.55) üst üste binip bulutlara kocaman
            // kara lekeler basıyordu — local yüksek yoğunlukta 1'e doyuyor ve iki
            // koyulaştırma çarpışıyor.
            float ambientBlend = saturate(_CloudAmbientFloor + fraction * (1.0 - _CloudAmbientFloor)
                                          - 0.16 * local * (1.0 - fraction));

            // AMBIENT AZİMUTA DA BAĞLANIR. Harman yalnız bulutun içindeki yüksekliğe
            // bakıyordu — taban koyu, tepe aydınlık — ama yön yoktu: güneş tarafındaki
            // bulutla ay tarafındaki bulut birebir aynı gök ışığını alıyordu. Şafakta
            // baskın terim bu olduğu için karşı taraf da aydınlık kalıyor, iki yarı
            // aynı renge geliyordu.
            //
            // Alacakaranlıkta karşı ufuk Dünya'nın gölgesindedir; oradaki bulut çok
            // daha az gök ışığı görür. Kapı `horizonSun` — güneş yükselince gökyüzü her
            // yönde aydınlanır ve ayrım kapanır.
            ambientBlend *= lerp(1.0, lerp(0.3, 1.0, towardSun), horizonSun);

            // Yoğun çekirdek gök ışığını içeri işletmez: kalın kısım ambient'te de
            // koyulaşır. Bu olmadan bulutun rengi sisin rengiyle aynılaşıyordu —
            // fırtınada katmanın içindeki oyuncu %91 kapsamaya rağmen tek bulut
            // GÖREMİYORDU: kütleler oradaydı ama süte süt katılmış gibi kamufleydi.
            // Kontrastın kaynağı ton değil yoğunluk farkı olmalı.
            // YEREL YAĞIŞ: yağmur bütün gökten değil, KALIN kolonlardan düşer. Aynı
            // havada bir bulut yağarken komşusu yağmaz — yağış bulutun kendi
            // kalınlığının işi. Yoğun kolonlar yağış arttıkça kurşuni kararır;
            // ince yayvan olanlar aydınlık kalır. Ek doku okuması yok: ölçü zaten
            // elimizdeki yerel yoğunluk.
            float rainColumn = saturate(_CloudRainAbsorb * saturate(local * 1.4 - 0.15));

            // TEK ÇARPAN. Yağış karartması ayrı bir çarpan olarak eklenmişti ve ikisi
            // de aynı işi yapıyordu: yerel yoğunlukla karartmak. Çarpılınca yoğun
            // örnekte ambient 0.64'ten 0.29'a düşüyor, kalın bulutlar siyahımsı griye
            // kaçıyordu. rainColumn zaten local ≈ 0.55'te doyduğu için gövdenin
            // tamamı tam cezayı yiyordu. Yağış artık karartmanın DERİNLİĞİNİ
            // belirliyor: kuru gövde 0.55'te kalıyor (eski hâl), yağmurlu gövde
            // 0.32'ye iniyor — kurşuni ama siyah değil.
            float bodyDarken = lerp(0.55, 0.32, rainColumn);

            float3 ambient = lerp(_CloudDarkColor.rgb, _CloudBrightColor.rgb, ambientBlend)
                             * (_CloudAmbient * duskContrast)
                             * lerp(1.0, bodyDarken, local);

            // Karartma toplam enerjiye uygulanır. Yalnızca ambient'e uygulamak yetmiyordu:
            // doğrudan ışık katkısı üstünden geçip etkiyi yutuyordu.
            // Kalın bulut ışığı geçirmez: içi ve altı koyu, soğuk kalır. İnce olan ışığı
            // geçirir, sıcak ve parlak görünür. Kütleler arası fark buradan doğar —
            // yoğunluğun kendisi zaten yumuşak olduğu için artefakt üretmiyor.
            float3 thin = float3(1.0 + _CloudMassWarmth, 1.0, 1.0 - _CloudMassWarmth);
            float3 thick = float3(1.0 - _CloudMassWarmth * 0.6, 1.0,
                                  1.0 + _CloudMassWarmth * 0.6);

            float3 body = lerp(thin, thick, local)
                        * lerp(1.0 + _CloudMassBrightness * 0.5,
                               1.0 - _CloudMassBrightness * 0.5, local);

            // Güneş ufuktayken ışık her yüzeye ulaşır (yatay); yükseldikçe yalnız
            // tabanlara (alttan vuran ışık). Tepeden bakan oyuncu için fark bu:
            // tabanlara kilitli ışık, denize üstten bakınca sıfır pay veriyordu.
            float under = (1.0 - fraction) * (1.0 - fraction);
            float reach = lerp(1.0, under, duskReach);

            float3 energy = (lightColor * light * phase * powder + ambient
                             + duskLight * reach)
                            * directionalDim * body;

            float stepTransmittance = exp(-density * step);

            scattered += transmittance * energy * (1.0 - stepTransmittance);
            transmittance *= stepTransmittance;

            if (transmittance < 0.06) break;
        }

        samplePoint += direction * step;
        travelled += step;
    }

    // Hava perspektifi: uzaktaki bulut kalın atmosfer katmanından geçerek gelir,
    // kontrastı düşer ve ufuk rengine yaklaşır. Derinlik algısının asıl kaynağı budur.
    // Ölçü ilk buluta olan uzaklık; bulut yoksa perspektif de yok.
    // Solma mesafenin dördüncü kuvvetiyle gider, karesiyle değil.
    //
    // Kareyle yarı yolda %25 solmuş oluyor ve uzaktaki bulutlar daha görünmeden
    // kayboluyordu; mesafeyi büyütmek de bu sefer ufku çıplak bırakıyor. Dik eğri ikisini
    // birden veriyor: orta mesafe berrak kalır, kapanma yalnızca en son dilimde olur ve
    // katmanın teğet ufku o dilimin içinde gömülür.
    // İlk vuruş yoksa (yalnız eşik altı zayıf yoğunluk biriktiyse) mesafe yürüyüşün
    // ULAŞTIĞI yerdir. Eskiden 0 sayılıyordu: o pikseller hiç puslanmıyor ve ufuk
    // şeridi, önündeki puslanmış bulutlardan kopuk, tam kontrastlı bir bant olarak
    // havada asılı duruyordu.
    float hazeStart = firstHit > 0.0 ? firstHit : travelled;
    float haze = saturate(hazeStart / max(1.0, hazeDistance));
    haze *= haze;
    haze *= haze;

    float coverage = 1.0 - transmittance;

    // Yön karartması buraya da uygulanır: perspektif rengi tek ve yön bağımsız olduğu
    // için ay tarafındaki uzak bulutlar güneş tarafının aydınlığını alıyordu. Yakındakiler
    // doğru kararırken uzaktakilerin açık kalması bundandı.
    //
    // Perspektif tamamlandığında bulut ayrı bir cisim olmaktan çıkar, havanın kendisi
    // olur: görüş sınırındaki bulut hem rengini hem sınırını kaybeder. Yalnızca rengi
    // karıştırmak şekli alfada bırakıyor, beyaz körlükte düz gri görülmesi gereken
    // yerde silik siluetler kalıyordu.
    //
    // Pay eskiden 0.4'te tutuluyordu: ölçü yürüyüşün bittiği yer olduğu için uzaklığı
    // olduğundan büyük gösteriyordu ve kısılmasa yakın bulutlar da düzleşiyordu.
    // Ölçü gerçek uzaklık olunca o kısıtlamaya gerek kalmadı.
    // Karışma tamamlandığında bulut söner, kapsama dolmaz.
    //
    // Doldurmak beyaz körlükte silik siluet bırakmamak içindi ama katmanın teğet ufkunda
    // dikiş üretiyordu: çizginin üstü "sis rengi, alfa 1" ile boyanıyor, altında hiç bulut
    // olmadığı için gökyüzü shader'ı görünüyor ve iki rengin küçük farkı düz bir çizgi
    // olarak duruyordu. Kapsama arttıkça dolu bölge büyüdüğü için çizgi de uzuyordu.
    //
    // Tam karışan bulut zaten gökyüzüyle aynı renge geliyor; söndürmek aynı görüntüyü
    // verir ve sınırın iki yanında çizilen şey aynı olduğu için dikiş kalmaz.
    float fade = 1.0 - haze;

    // Karışılan renk SABİT DEĞİL, o yöndeki havanın rengi. Burada tek bir düz taban
    // renk vardı ve şafakta gri-mavi kalıyordu: uzak bulutlar güneşe doğru bakarken
    // bile griye karışıyordu. Arazi ve gökyüzü zaten AirColor'ı kullanıyor; bulut da
    // aynı fonksiyonu kullanınca üçü ayrışamaz.
    float3 airHere = AirColor(direction);

    scattered = lerp(scattered, airHere * coverage * directionalDim, haze) * fade;
    coverage *= fade;

    // Kamera önü sisinin peçesi BURADA: ilk bulut mesafesi yalnız yürüyüşün içinde
    // biliniyor. Peçe daha önce bindirme geçişindeydi ve mesafe yerine yeniden
    // yansıtma çapasını (katman ortası küresi) kullanıyordu — ufka yakın ışında o
    // çapa 100+ km'ye düşüyor, sis integrali absürt yolu sarıyor ve katman ortasının
    // ALTINDAKİ kameraya bulutlar görünmez oluyordu; ortayı aşınca çapa dejenere
    // olup (1 m) peçe bire fırlıyor ve bulutlar bir anda beliriyordu. Tırmanıştaki
    // "incelme → kayboluş → pat diye render" üçlüsünün tamamı buydu.
    if (firstHit > 0.0)
    {
        float veilDrift;
        float veil = exp(-HeightFogIntegral(origin, origin + direction * firstHit, veilDrift)
                         * FogBankAt(origin.xz) * 0.6);

        // Peçe RENGİ süzer, alfayı DEĞİL. `coverage *= veil` bulutu saydamlaştırıyor
        // ve arkadaki dağ bulutun içinden okunuyordu — sisin örttüğü bulut şeffaf
        // değil, sise batmış OPAK bir kütledir: örtme gücü kalır, rengi aradaki
        // havanın rengine çöker.
        // Peçenin rengi de aradaki havanın kendisi. Sabit taban renk yüzünden şafakta
        // zeminden bakılan bulutlar griye boyanıyordu — sis kapatılınca düzelmesinin
        // sebebi buydu (integral sıfırlanınca peçe devre dışı kalıyor).
        scattered = lerp(airHere * coverage, scattered, veil);
    }

    // YÜKSEK KATMAN hacimsel bulutların ARKASINDA: fiziksel olarak daha yüksek ve
    // daha uzak, dolayısıyla hacimsel sonucun altına kompozit edilir. Sahne
    // derinliği (dağ) katmanı zaten kesiyor — maxDistance oradan geliyor.
    float4 high = CloudHighLayer(origin, direction, maxDistance, lightDirection,
                                 lightColor * directionalDim,
                                 _CloudBrightColor.rgb * (_CloudAmbient * 0.35));
    if (high.a > 0.0)
    {
        float behind = 1.0 - coverage;
        scattered += high.rgb * behind;
        coverage = coverage + high.a * behind;
    }


    return float4(scattered, coverage);
}

#endif
