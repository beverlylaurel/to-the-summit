#ifndef TOTHESUMMIT_HEIGHT_FOG_INCLUDED
#define TOTHESUMMIT_HEIGHT_FOG_INCLUDED

// Yükseklik sisi. Havanın kendi hesabı — hangi yüzeyin çizildiğinden bağımsız.
//
// Unity'nin sisi yükseklikten bağımsız: zirvede de etekte de aynı yoğunlukta kalıyor ve
// tırmanmanın görsel karşılığı doğmuyordu. Bu yüzden hiç kullanılmıyor, sönüm burada
// hesaplanıyor.
//
// Ayrı dosyada duruyor çünkü tek bir yüzeyin özelliği değil: dağ, kaya, props, ne
// çizilirse çizilsin aynı havanın içinde duruyor. Yüzey shader'ının içinde kalırsa
// ikinci bir yüzey geldiğinde ya kopyalanır ya da o yüzey sissiz kalır — ikisi de
// aynı havada iki farklı görünürlük demek.
//
// Yoğunluk modeli AYRI DOSYADA: froxel hacmini süren compute shader onu include
// ediyor, bu dosyayı edemiyor. Gerekçe orada yazılı.
#include "VolumetricFogShared.hlsl"

// Sabit tamponun dışında: AtmosphereController bunları global olarak yazıyor.
float4 _HeightFogColor;
float4 _HeightFogShadowColor; // güneşin karşı ufku: Dünya'nın gölgesi, gökyüzüyle ortak
float4 _HeightFogZenith;   // göğün tepe rengi: ışın dikleştikçe hava buna kararır
float4 _HeightFogSunColor; // gök, güneş yönünde, ufkun 2° üstü
float3 _SunDirection;      // AtmosphereController global yazar; gökyüzü ve bulut da okur

// Şimşek: LightningFlash yazar, bulut ve gökyüzü de aynı değeri okur.
float4 _LightningFlash;

/// Sisin çakmadan aldığı pay. Bulut kadar almaz: bulut kütlesi kilometrelerce derin ve
/// çakma onun içinde, sis ise ince ve boşalmanın altında kalıyor.
static const float LightningFogScatter = 0.6;

// TimeOfDay yayınlıyor. Burada bildiriliyor çünkü sis dosyası yüzeyden ÖNCE include
// ediliyor. İkinci bir isim uydurulmuştu; o global gelmediğinde değer sıfır kalıyor,
// perde "güneş alçak" sanılıp ham gök mavisine boyanıyordu.
float _SunHeight;
float _SpindriftBrightness;  // perdenin kendi parlaklığı, gök luminansı çarpanı
float _SpindriftMaxDepth;    // perdenin optik derinlik tavanı

// BULUT SİSTEMİ SÖKÜLDÜ — burada yalnız iki iz kaldı.
//
// `_CloudBottom` bildirimi DURUYOR: şimşek shader'ı (`LightningBolt.shader`) çakmayı
// bulut tabanı küresiyle kesiştiriyor ve globali oradan okuyor. Kendi bildirimini
// açarsa derleyici çakışıyor.
//
// `CloudShadowAt` SİLİNDİ. Yer bulut gölgesi artık bulut sisteminin kendi yolundan
// geliyor: `VolumetricCloudsURP` gölgeyi ana ışığın cookie dokusuna yazıyor, URP de
// onu `_LIGHT_COOKIES` açık olan her yüzeye uyguluyor.
//
// Sözleşme böylece kendiliğinden sağlanıyor (`CLOUDS_REBUILD.md` madde 1): gölge,
// gökyüzünü çizen yoğunluk alanının ta kendisinden türüyor. İkinci bir yaklaşım yok,
// dolayısıyla "gökte bulut yokken yerde gölge" durumu da yok.
float _CloudBottom;        // katmanın tabanı (metre)

/// Yükseklik sisi: ışının kat ettiği yol boyunca yoğunluk integrali.
///
/// Sabit yoğunluklu sis bunu yapamaz — zirveden vadiye bakarken de vadiden zirveye
/// bakarken de aynı miktarı uygular, oysa ilkinde ışın yoğun katmana giriyor,
/// ikincisinde ondan çıkıyor.
///
/// İnversiyon tavanı profili keskinleştirdiği için integralin kapalı çözümü kalmıyor;
/// yol boyunca birkaç örnek alınıyor. Sekiz örnek, tavanın kestiği yeri gözle görülür
/// bir basamak bırakmadan yakalıyor.
/// Işın boyunca yoğunluk integrali, bank çarpanı olmadan. Bankı çağıran seçer:
/// arazi yol boyunca örnekler, bulut peçesi yalnız kameranın yerelinden okur.
/// Sis ve sürüklenen kar TEK TARAMADA. İkisi ayrı döngüdeyken aynı ışın, aynı `t`
/// değerlerinde iki kez taranıyordu — sonuç birebir aynı, maliyet iki katı.
/// Sürüklenen kar ayrı dönüyor çünkü rengi ve sönüm eğrisi sisinkinden farklı.
float HeightFogIntegral(float3 cameraPos, float3 worldPos, out float drift)
{
    drift = 0.0;

    float3 ray = worldPos - cameraPos;
    float distance = length(ray);

    bool hasFog = _HeightFogDensity > 0.0 || _FogSeaDensity > 0.0 || _FogFreeDensity > 0.0;
    bool hasDrift = _SpindriftDensity > 0.0;

    if (distance < 0.01 || (!hasFog && !hasDrift)) return 0.0;

    const int Steps = 8;

    float startHeight = cameraPos.y - _HeightFogBase;
    float endHeight = worldPos.y - _HeightFogBase;

    float sum = 0.0;
    float driftSum = 0.0;

    // PERDE KENDİ ADIMLARIYLA TARANIYOR. Sisin profili yayvan ve pürüzsüz, sekiz adım
    // fazlasıyla yetiyor. Perde ise yere yapışık, ince ve akan bir yapı taşıyor —
    // aynı sekiz adım desenin üstünden atlıyor ve titreme bırakıyordu. Adım sayısı
    // yalnız perde etkinken ve yalnız perde terimi için ikiye katlanıyor.
    const int DriftSubSteps = 2;

    [unroll]
    for (int i = 0; i < Steps; i++)
    {
        float t = (i + 0.5) / Steps;
        sum += FogDensityAt(lerp(startHeight, endHeight, t));

        if (hasDrift)
        {
            [unroll]
            for (int k = 0; k < DriftSubSteps; k++)
            {
                float s = (i + (k + 0.5) / DriftSubSteps) / Steps;
                driftSum += SpindriftAt(lerp(cameraPos, worldPos, s)) / DriftSubSteps;
            }
        }
    }

    // PERDE DOYUMA GİTMEZ ama YAPISINI DA KAYBETMEZ. Sert kırpma (`min`) uzakta bütün
    // değerleri aynı tavana yapıştırıyor ve akış deseninin kontrastı siliniyordu:
    // "bembeyaz olmasın" isteğiyle "yapısı görünsün" isteği tek sayıda çarpışıyordu.
    //
    // Yumuşak doyum tavana asla varmıyor, yalnız yaklaşıyor — iki katı derinlik hâlâ
    // daha koyu çıkıyor, yani desen uzakta da okunuyor.
    float raw = distance * driftSum / Steps;
    drift = _SpindriftMaxDepth * raw / (raw + _SpindriftMaxDepth);

    // `FogDensityAt` artık mutlak yoğunluk veriyor: dışarıda ikinci bir çarpım yok,
    // yoksa iki katmandan biri iki kez ölçeklenirdi.
    return distance * sum / Steps;
}

/// Havada asılı karın rengi. Gökyüzü rengiNDEN türemiyor: `AirColor` bakış yönüne bağlı
/// ve aşağı bakarken neredeyse siyah dönüyor — sis için doğru (vadiye bakınca sis koyu
/// okunur), havada asılı kar için yanlış. Savrulan kar yukarıdan güneşle ve altındaki
/// karın yansımasıyla aydınlanır; hangi yöne bakıldığı onu karartmaz.
///
/// Kaynak ufkun hemen üstündeki gök rengi — zaten sisin okuduğu ışık, ayrı bir kaynak
/// kurulmuyor. Doygunluğu alınıyor (kristal dalga boyu seçmez) ve yukarı ölçekleniyor
/// (kar albedosu havanın saçılmasından yüksek).
float3 SpindriftColor()
{
    // Kristal DALGA BOYU SEÇMEZ: rengi kendinden değil, üstüne düşen ışıktan gelir.
    // Şafakta savrulan kar kızıl olmalı — tam doygunluk alınırsa asla olmaz.
    //
    // Ama gündüz de ufuk göğünün rengini alamaz: güneş tepedeyken perdeyi aydınlatan
    // şey tek bir yön değil, gök kubbenin tamamı — sonuç nötr beyazdır. Ufuk mavisini
    // taşımak yamacı fosforlu maviye çeviriyordu.
    //
    // Ayrım güneşin yüksekliğinden: alçakken ışık yönlü ve renkli, yükseldikçe dağınık
    // ve nötr.
    float3 light = _HeightFogSunColor.rgb;
    float luma = dot(light, float3(0.2126, 0.7152, 0.0722));

    float lowSun = 1.0 - smoothstep(0.02, 0.28, _SunHeight);

    // PARLAKLIK AYRI BİR KATSAYI. Ufuk göğünün luminansı kapalı havada karın kendisinden
    // düşük; perde olduğu gibi kullanılınca parlak beyazın üstüne KOYU nötr bir film
    // oturuyor ve göz onu mavi-gri okuyor (eşzamanlı kontrast). Oysa savrulan kar aynı
    // kardır, aynı ışıkla aydınlanır — zeminden koyu olamaz.
    // KATSAYI IŞIK ZAYIFKEN ÇALIŞIR. İşi perdeyi "kardan koyu" bandından çıkarmak;
    // ışık zaten parlakken yükseltilecek bir şey yok. Sabit çarpanla gündüz taban
    // luminansı 1'i aşıp beyaza kırpılıyordu — dağ akşamüstü fosforlu görünüyordu.
    // RENK ve PARLAKLIK AYRI. `_HeightFogSunColor` ham gök ışıması × sahne kazancı,
    // yani HDR ve ÜST SINIRI YOK — üstelik en büyük olduğu yer tam olarak güneş
    // yönündeki ufuk, yani şafak ve akşamüstü. Olduğu gibi renk olarak geçirilince
    // 1'i aşıyor ve beyaza kırpılıyordu: dağın fosforlu görünmesi buydu. Katsayıyı
    // kısmak yetmez, tavan gerekir.
    //
    // Tavan fiziksel: perde kardır, en fazla tam aydınlanmış kar kadar parlak olabilir.
    float3 hue = light / max(1e-4, luma);
    float3 tinted = lerp(1.0, hue, lowSun * 0.9);

    // ZEMİNE ULAŞAN IŞIK, ufuk parıltısı değil. Yamaç şafakta hâlâ gölgedeyken perde
    // ufkun parlaklığını alırsa dağ aydınlatılmış görünüyor. Güneşin yüksekliği
    // aydınlanmanın gerçek ölçüsü.
    float dayLevel = saturate(_SunHeight * 3.5);
    float gain = lerp(_SpindriftBrightness, 1.0, saturate(luma));

    float level = saturate(luma * gain) * lerp(0.3, 1.0, dayLevel);
    return tinted * level;
}

float HeightFogAmount(float3 cameraPos, float3 worldPos)
{
    float drift;
    float integral = HeightFogIntegral(cameraPos, worldPos, drift)
                   * FogBankPath(cameraPos.xz, worldPos.xz);
    return saturate(1.0 - exp(-integral));
}

/// Gökyüzüne giden ışının sis OPTİK DERİNLİĞİ. Arazi yolu sonlu ve örnekle integre
/// ediliyor; gök yolu sonsuz — her katmanın üstel profili kapalı biçimde integre
/// edilir. Gök sislenmeden atmosfer tek olmuyordu: sis yalnız araziye uygulanınca
/// çorbanın içinde yukarı bakan oyuncu yıldız görüyordu, banklar da önlerinde arazi
/// yokken hiç çizilmiyordu.
///
/// Metre yerine derinlik döndürüyor: üç katmanın yoğunlukları farklı, tek bir "yol"
/// sayısını dışarıda tek bir yoğunlukla çarpmak üçünü birden temsil edemez.
/// `maxPath` her katmanın KENDİ yoluna ayrı ayrı uygulanır — güneş kadranı sonsuz
/// yolla söndürülmesin diye (bkz. Sky.shader).
float SkyFogDepth(float3 cameraPos, float3 dir, float maxPath)
{
    float h0 = cameraPos.y - _HeightFogBase;

    // Ufka inen ışın: eğim sıfıra dayandıkça yol yatay kapasiteye oturur (~100 km
    // eşdeğeri). Ufuk her havada hava rengine doyar — gerçekte de öyle.
    float s = max(dir.y, 0.02);

    float k = _HeightFogFalloff;

    // Sınır tabakası inversiyonda BİTER: üstünde kalan pay artık serbest katmanın işi.
    float boundaryPath = h0 < _FogInversionHeight
        ? (exp(-k * h0) - exp(-k * _FogInversionHeight)) / (k * s)
        : 0.0;

    // İkisinin de tavanı yok; profilleri kendileri bitiriyor.
    float seaPath = exp(-_FogSeaFalloff * max(0.0, h0)) / (_FogSeaFalloff * s);
    float freePath = exp(-_FogFreeFalloff * max(0.0, h0)) / (_FogFreeFalloff * s);

    // Sürüklenen kar yalnız kameranın çevresinden okunuyor: katman araziye yapışık ve
    // arazi yükseklik alanı ışın boyunca kapalı biçimde integre edilemez. Göğe giden
    // ışın zaten katmanı birkaç on metrede terk ediyor; uzaktaki sırtın perdesi arazi
    // yolunda hesaplanıyor.
    float sky = _HeightFogDensity * min(boundaryPath, maxPath)
              + _FogSeaDensity * min(seaPath, maxPath)
              + _FogFreeDensity * min(freePath, maxPath);

    // Rüzgâr eşiğin altındaysa iki doku örneği boşa gidiyordu: arazi yüksekliği ve
    // kar profili, sonucu sıfırla çarpılacak bir değer için okunuyordu. Gökyüzü her
    // pikselde bu yoldan geçiyor.
    if (_SpindriftDensity <= 0.0) return sky;

    float driftGround = TerrainHeightAt(cameraPos.xz);
    float driftHeight = max(0.0, cameraPos.y - driftGround);
    float driftPath = exp(-_SpindriftFalloff * driftHeight) / (_SpindriftFalloff * s);
    float driftDensity = _SpindriftDensity
                       * SampleSnowProfile(driftGround).r
                       * SpindriftFlow(cameraPos.xz);

    float rawDrift = driftDensity * min(driftPath, maxPath);
    return sky + _SpindriftMaxDepth * rawDrift / (rawDrift + _SpindriftMaxDepth);
}

float SkyFogAmount(float3 cameraPos, float3 dir)
{
    if (_HeightFogDensity <= 0.0 && _FogSeaDensity <= 0.0 && _FogFreeDensity <= 0.0)
        return 0.0;

    // Kameranın önündeki bank boş gökte görünür bir leke bırakır: "vadide gezen sis"
    // ancak gök de sislenince var olabiliyor.
    float2 ahead = cameraPos.xz + normalize(dir.xz + 0.0001) * 900.0;
    float bank = (FogBankAt(cameraPos.xz) + FogBankAt(ahead)) * 0.5;

    return saturate(1.0 - exp(-SkyFogDepth(cameraPos, dir, 1e9) * bank));
}

/// Havanın kendi rengi: gökyüzü gradyanının ta kendisi. Gökyüzü de sis de bunu çağırır
/// — tek formül, iki tüketici; tam sislenen arazi gökten ayırt edilemez. Sis ayrı bir
/// renk taşıdığı sürece her hava/saat köşesinde yeni bir "parlayan karton dağ" çıkıyor
/// ve elle yamanıyordu.
///
/// Kızıllık güneşin bulunduğu yönde yoğunlaşır; karşı ufukta Dünya'nın gölgesi
/// yükselir (mavi-mor, ayrıca karanlık — ışık yatay gelirken o yöne düşmez). Ayrışma
/// yalnız güneş ufka yakınken. Yükseldikçe tepe rengine kararır.
float3 AirColor(float3 direction)
{
    float3 sunward = normalize(float3(_SunDirection.x, 0.0, _SunDirection.z) + 0.0001);
    float3 viewFlat = normalize(float3(direction.x, 0.0, direction.z) + 0.0001);
    float towardSun = smoothstep(-0.85, 0.85, dot(viewFlat, sunward));

    // Yatay yönün ANLAMLI olduğu bölge. İki koşul birden: kutuplara (tam yukarı, tam
    // aşağı) yaklaşınca azimut tanımsızlaşır; ve ufkun ALTINDA gökyüzü bandı diye bir
    // şey yoktur — aşağı bakan ışın yerin üstündeki havayı görür, göğün güneş bandını
    // değil. İkincisi eksikti: bant nadir'e doğru bir noktaya toplanıp şafakta yere
    // bakınca koni bırakıyordu. Ufuk seviyesinde 1, 14.5° aşağıda 0 — ufka yakın uzak
    // arazi sıcaklığını korur, dik aşağıda yapıyı söndürür.
    float azimuth = saturate(length(direction.xz) * 3.0)
                  * saturate(direction.y * 4.0 + 1.0);
    towardSun = lerp(0.5, towardSun, azimuth);

    float lowSun = 1.0 - saturate(abs(_SunDirection.y) / 0.3);

    // Alacakaranlık paleti — sayılar Python simülasyonundan (dusk_palette_sim.py,
    // "canlı" varyantı): zincirin tamamı — süzülmüş güneş, controller karışımları,
    // bu formül, altın saat kademesi, ACES — ekransız çizilip referans batış
    // fotoğrafının rampasına oturtuldu.
    //
    // Batış göğü tek renk değildir: güneşin çevresi ALTIN, açıldıkça turuncuya ve
    // kızıla iner, karşı yarı soğuk gri-maviye düşer. Altın, süzülmüş güneş renginden
    // çarpımla üretilemez — o renkte yeşil tükenmiştir, çarpım sarı çıkaramaz; altın
    // ucu açık yazılır, kızıl ucu süzülmüş güneşten gelir, arası kendiliğinden turuncu.
    // Bantlar towardSun'dan değil ham açıdan türer: towardSun 0.85'te doyuyor ve bant
    // sanılandan üç kat geniş çıkıp parlaklığı ekrana bakılamaz hâle getiriyordu.
    // Aynı kapı buna da: altın ucu towardSun'dan bağımsız hesaplanıyor, kapısız
    // kalınca koniyi tek başına üretmeye yetiyor.
    float sunDot = saturate(dot(viewFlat, sunward)) * azimuth;
    // Altın ucu bir tık kısık: tam parlak altın, güneş tarafını gözü alacak kadar
    // dolduruyordu. Ay tarafı gölge renginden gelir, bu kısma ondan bağımsız.
    // ALTIN UCU AÇIK YAZILIR — fizik örneği değil. Denendi ve ölçüldü: güneş tam
    // ufuktayken (06:00) gök örneği luminans 0.151, bu sabit 0.571 — 3.8 kat. Ekranda
    // şafak tamamen sönüyordu.
    //
    // Sebep modelin hatası değil, KAPSAMI: `Atmosphere` temiz atmosferi çiziyor
    // (Bruneton'un pristine Mie katsayısı). Şafağın altın patlaması ise AEROSOLÜN işi —
    // toz, nem, is. Güneş çevresindeki hâleyi kuran Mie saçılması gerçek havada
    // bizimkinden kat kat güçlü. Yani bu sabit bir sanatçı uydurması değil, tozlu bir
    // atmosferin yaklaşık karşılığı; aerosol modellenirse fizik yerini alır.
    //
    // Sabit yalnız güneşin TAM azimutunda baskın: `pow(sunDot, 1.8)` ile sönüyor,
    // çeperde fizik örneği devrede ve o saatle ilerliyor.
    // TON GÜNEŞİN YÜKSEKLİĞİYLE KIZILLAŞIR. Tek bir altın sabit, güneş ufkun on
    // derece üstündeyken de dibindeyken de aynı sarıyı veriyordu — batımda kızıllık
    // hiç gelmiyordu.
    //
    // Fizik: kızıllık YOL UZUNLUĞUNDAN doğar. Güneş alçaldıkça ışık atmosferde daha
    // uzun yol alır, önce mavi sonra yeşil süpürülür, geriye kırmızı kalır. Sabitin
    // parlaklığı bilinçli abartma olarak kalıyor (bkz. DECISIONS.md), ama TONU artık
    // güneşin yüksekliğini izliyor.
    //
    // Kızıl uç yeşili sarıdan üçte iki oranında kısıyor: (0.9, 0.52) → (0.85, 0.20).
    // Mavi zaten ihmal edilebilir.
    float3 gold = lerp(float3(0.9, 0.52, 0.11), float3(0.85, 0.20, 0.05),
                       1.0 - smoothstep(0.0, 0.09, _SunHeight));

    float3 duskHue = lerp(_HeightFogSunColor.rgb, gold, pow(sunDot, 1.8));

    float3 warm = lerp(_HeightFogColor.rgb, duskHue, pow(saturate(towardSun), 1.2) * lowSun);
    warm *= 1.0 + pow(sunDot, 8.0) * lowSun * 0.10;

    float3 horizon = lerp(_HeightFogColor.rgb,
                          lerp(_HeightFogShadowColor.rgb, warm, towardSun),
                          lowSun);

    // Üs 0.55 → 0.35: sıcaklık ufka yakın kalmalı. Yüksek üs ufuk rengini göğün
    // yarısına kadar taşıyıp bandı sanılandan çok geniş gösteriyordu.
    //
    // Üssün 0'daki eğimi SONSUZ: göz hizasının ilk yarım derecesinde harman sıfırdan
    // 0.15'e sıçrıyor, altında `saturate` ile kırpılıyor. Ufuk sıcak, zenit koyu mavi
    // olduğu için o kırılma Mach bandı gibi düz bir çizgi bırakıyor. Gökyüzünde
    // farkedilmiyordu; arazi puslanınca hava rengi uzak dağın üstünde de baskın oldu ve
    // çizgi dağın içinden geçiyormuş gibi, "dağ şeffafmış gibi" göründü.
    //
    // Üs korunur — sıcaklığın ufka yakın kalması ondan geliyor. Yalnız ilk üç derece
    // smoothstep'le C1 sürekli hâle getirilir; 3.4°'nin üstünde eğri birebir aynı.
    float rise = pow(saturate(direction.y), 0.35)
               * smoothstep(0.0, 0.06, direction.y);
    float3 air = lerp(horizon, _HeightFogZenith.rgb, saturate(rise));

    // Karşı yarı kararır ama simsiyah değil: gerçek karşı ufuk yumuşak mor-gridir
    air *= lerp(1.0, lerp(0.55, 1.0, towardSun), lowSun);

    // İleri saçılım, çift lob: geniş pus parlaması + dar parlak çekirdek. Sisin
    // içinden güneş keskin disk olarak değil ışıyan bir yumak olarak görünür —
    // şafak sisindeki güneş budur; sis denizinin üstüne çıkınca gerçek disk döner.
    float sunUp = smoothstep(-0.08, 0.12, _SunDirection.y);
    float alignment = saturate(dot(direction, normalize(_SunDirection + 0.0001)));
    // Dar lob ölçülü: büyütülünce diskin oturduğu yeri dolduruyor ve güneşin
    // kendisi kendi parlamasının içinde kayboluyordu
    float forward = pow(alignment, 8.0) * 0.05 + pow(alignment, 64.0) * 0.12;
    air += _HeightFogSunColor.rgb * (forward * sunUp);

    return air;
}

/// Çizilmiş rengi havanın içine oturtur. Çağıran tarafın miktarı ayrıca alıp lerp'i
/// kendi yazmasına gerek yok — o iki satır her yüzeyde birebir aynı olurdu.
///
/// Sis şimşeği de saçar. Rengi sabit tutulunca fırtınada — yani şimşeğin çaktığı tek
/// havada — görüş yedi yüz metreye düşüyor ve arazinin büyük kısmı o değişmeyen rengin
/// altında kalıyordu: yüzey aydınlansa bile üstü örtülü olduğu için hiçbir şey
/// görünmüyordu. Gerçekte tam tersi olur, çakma anında sisin kendisi içeriden parlar.
float3 ApplyHeightFog(float3 color, float3 cameraPos, float3 worldPos)
{
    float3 air = AirColor(normalize(worldPos - cameraPos))
               + _LightningFlash.rgb * LightningFogScatter;

    // HACİM VE KUYRUK. Froxel hacmi 0–`far` arasını gölgelenmiş olarak taşıyor; ötesini
    // analitik integral sürüyor. İkisi de AYNI yoğunluk modelini okuduğu için sınırda
    // yapı değişmiyor (`VolumetricFogShared.hlsl`).
    //
    // Kompozisyon Beer-Lambert gereği: geçirgenlikler çarpılır, in-scattering öndekinin
    // geçirgenliğiyle ağırlıklanıp toplanır. Bu yüzden ayrıca bir blend penceresi
    // gerekmiyor — geçiş yapı gereği sürekli.
    float3 volumeScatter = 0.0;
    float volumeTransmittance = 1.0;
    float3 tailStart = cameraPos;

    // Hacim yoksa `_FogVolumeDepth` sıfır kalır; o zaman kuyruk kameradan başlar ve
    // davranış hacim öncesiyle BİREBİR aynı olur. Doğrulama basamağı bu.
    if (_FogVolumeDepth.z > 0.0)
    {
        float viewDepth = dot(worldPos - cameraPos, _FogCameraForward.xyz);

        if (viewDepth > _FogVolumeDepth.x)
        {
            float2 screenUV = ComputeNormalizedDeviceCoordinates(worldPos, UNITY_MATRIX_VP);
            float sampleDepth = min(viewDepth, _FogVolumeDepth.y);

            float4 volume = SAMPLE_TEXTURE3D_LOD(_FogScatteringVolume, sampler_FogScatteringVolume,
                                                 FogVolumeUVW(screenUV, sampleDepth), 0);

            volumeScatter = volume.rgb;
            volumeTransmittance = volume.a;

            // Kuyruk hacmin bittiği yerden başlıyor. Yön ileri eksene izdüşümü 1 olacak
            // şekilde ölçekli, yani `dir · derinlik` doğrudan o derinlikteki nokta.
            float3 dir = (worldPos - cameraPos) / max(viewDepth, 1e-4);
            tailStart = cameraPos + dir * min(viewDepth, _FogVolumeDepth.y);
        }
    }

    // KANAL BAŞINA SÖNÜM KALDIRILDI (`_HeightFogChroma`). Rayleigh'in maviyi kırmızıdan
    // önce süpürmesi gerçek ama artık onun SAHİBİ gökyüzü paketinin hava perspektifi:
    // aynı atmosferi iki yerden modellemek çift sayım demek. Bu dosyanın taşıdığı ortam
    // YEREL — vadi sisi, banklar, sürüklenen kar — ve su damlası baskın olduğu için
    // sönümü zaten nötr (Mie renk seçmez).
    float drift;
    float integral = HeightFogIntegral(tailStart, worldPos, drift)
                   * FogBankPath(tailStart.xz, worldPos.xz);

    // İKİ KATMAN SIRAYLA, tek karışımda değil. Sürüklenen kar araziye yapışık ve
    // gözün ÖNÜNDE duruyor; sisin mavisi ise yol boyunca dağılmış. Tek karışımda
    // toplanınca perdenin sönümü sisin payını da açıyor ve rüzgâr arttıkça yamaç
    // beyazlayacağına MAVİLEŞİYORDU.
    //
    // Önce sis uygulanır (uzak), sonra perde onun üstüne biner (yakın). Perde kendi
    // nötr rengini taşıyor, altındakini boyamıyor.
    float3 withFog = lerp(air, color, exp(-integral));

    // PERDE DERİNLEŞTİKÇE SÖNER, gökyüzünün rengine boyanmaz. `AirColor` bakış yönüne
    // bağlı ve gökyüzü gradyanını taşıyor; ona yakınsatınca o gradyan dağın üstüne
    // biniyor ve ufuk çizgisi dağın içinden geçiyormuş gibi görünüyordu.
    //
    // Fosforluluğun çaresi renk değiştirmek değil parlaklığı kısmak: kalın perde daha
    // çok saçar ama kendi içinde de söner, dışarı çıkan ışık doyuma gider.
    float3 veil = SpindriftColor()
                * lerp(1.0, 0.55, saturate(drift / max(0.01, _SpindriftMaxDepth)));

    float3 tail = lerp(veil, withFog, exp(-drift));

    // Hacim kuyruğun ÖNÜNDE: kuyruğun sonucu hacmin geçirgenliğiyle süzülüp hacmin
    // kendi saçılımı üstüne biniyor. Spec §5.4'teki `renk × transmittance + inScattering`
    // formülünün ta kendisi.
    return tail * volumeTransmittance + volumeScatter;
}

#endif
