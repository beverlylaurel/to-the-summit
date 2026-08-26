// ROL: kar sisteminin FİZİKSEL sabitleri, GPU tarafı. SnowConstants.cs ile
// BİREBİR aynı değerleri taşır; SnowConstantsTest bunu doğrular.
// Çağıran: SnowCommon.hlsl ve bütün kar shader'ları.

#ifndef SNOW_CONSTANTS_INCLUDED
#define SNOW_CONSTANTS_INCLUDED

// --- Yoğunluk (spec §6.3) ---
#define SNOW_RHO_MIN                50.0
#define SNOW_RHO_MAX               550.0
#define SNOW_RHO_WATER            1000.0

// --- Bölge takibi (spec §6.4) ---

/// Bölge merkezi kaç quad'lık adımlarla yer değiştiriyor. Adımın METRE
/// karşılığı türetilmiş: `QuadSize × SNOW_SNAP_QUADS`. Sabit metre yazılırsa
/// preset değişince oran bozulur ve izler teksel altı titrer (§22).
#define SNOW_SNAP_QUADS              2.0

/// Kenar sönümünün başladığı normalize kenar uzaklığı (spec §8.3).
/// 24 m alanın dış 2 metresi: 1 − 2×2/24 = 0.833.
#define SNOW_EDGE_FADE_START         0.833

// --- Kar / arazi çakışması (spec §8.1) ---
#define SNOW_MIN_VISIBLE_HEIGHT      0.004

/// KENAR GEÇİŞ ARALIĞI (m). `SnowLit.shader`'ın `_SnowEdgeFadeRange`'iyle AYNI
/// olmak zorunda: biri yüzeyin nerede çizileceğini, öteki örtü metriğinin ne
/// söyleyeceğini belirliyor. Ayrışırlarsa nesneler zemin beyazlamadan önce
/// (ya da sonra) beyazlar.
///
/// 4 mm → 10 mm bandı. Kar önce çukurlara düşüyor, lekeler büyüyor, 1 cm'de
/// sürekli örtü oluyor. Ani sıçrama yok.
///
/// 20 mm İDİ VE 1 CM'DE İKİ YÜZEY ÇELİŞİYORDU. Örtü metriği 1 cm'de 0.294
/// diyordu (arazi %29 beyaz), kar mesh'i ise aynı yerde kendi eşiğine göre
/// TAM örtü çiziyordu. Ekranda oyuncuyu takip eden beyaz bir kare, içi de
/// `clip(edgeFade − breakup·0.6)` yüzünden piksel ölçeğinde delik deşik
/// (edgeFade 0.30, gürültünün ortalaması 0.30 → yüzeyin yarısı kesiliyordu).
///
/// Bant 1 cm'de kapanınca ikisi de aynı şeyi söylüyor: arazi tam beyaz,
/// mesh'te delik yok.
///
/// Tutma temposu (ölçülen SWE hızı 2.89e-6 m/s, ρ55 → 3.15 mm/dk):
///   tam fırtınada  ilk beyazlama 1.3 dk, sürekli örtü 3.2 dk
///   %30 şiddette   ilk beyazlama 4.2 dk, sürekli örtü 10.6 dk
#define SNOW_EDGE_FADE_RANGE         0.006


// --- Parıltı mesafesi ---

/// PARILTI UZAKTA KAPANIYOR.
///
/// Bowles & Wang yöntemi parıltı YOĞUNLUĞUNU ekran uzayında sabit tutuyor ama
/// parıltının BOYUTU hücre boyuna bağlı; hücre piksel ayak izine göre
/// LOD'landığı için uzakta metrelerce büyüyor ve tek hücre birçok pikseli
/// birden kaplıyor. Sonuç uzaktan "kocaman parlayan piksel" (kullanıcı
/// bildirdi). Gerçekte de kristal parıltısı yakın bir olaydır: uzaktaki kar
/// alanı düzgün beyaz görünür, tek tek kristaller seçilmez.
///
/// Kapı mesafeye göre; ayak izine göre değil. Ayak izi bakış açısıyla da
/// değişiyor (grazing açıda patlıyor) ve aynı mesafedeki iki yüzey farklı
/// kapanırdı.
/// PARILTI HÜCRESİNİN TAVANI (m). `fwidth` sıyırtma açıda patlıyor; tavan
/// olmadan hücre metrelerce oluyor ve parıltı dikdörtgen lekeye dönüyor.
#define SNOW_SPARKLE_MAX_FOOTPRINT   0.04

#define SNOW_SPARKLE_FADE_START      28.0
#define SNOW_SPARKLE_FADE_END        50.0

/// MESH'İN GÖRÜNÜR OLDUĞU EN KÜÇÜK YEREL SAPMA, metre.
///
/// Kar tabanını arazi çiziyor; mesh yalnız arazinin veremeyeceği yerel
/// sapmayı (iz oyuğu, kenar sırtı) çiziyor. Bunun altında mesh tamamen
/// çekiliyor ve düz alan TEK shader'dan geliyor — bölge sınırının kare olarak
/// görünmesinin kaynağı iki ayrı shader'ın aynı yüzeyi çizmesiydi.
///
/// 2 mm: bir tekselin sayısal gürültüsünün üstünde, gözle seçilebilen en sığ
/// izin altında (ölçülen iz derinlikleri 60-80 mm).
/// RELIEF MAPPING — iz arazinin kendi yüzeyinde sanal derinlik olarak çiziliyor.
///
/// ADIM SAYISI IŞININ BOYUNDAN TÜRÜYOR, SABİT DEĞİL.
///
/// Sabit 12 adım tepeden bakışta bol, sıyırtma açıda yetersizdi: ışın 66 cm
/// uzarken adım başına 2.4 teksel atlanıyor ve kesişim adım ızgarasına
/// yuvarlanıyordu. Belirtisi düz bir oluğun ekranda LOB LOB, zigzaglı
/// görünmesiydi — ve loblar uzaklaştıkça (bakış yattıkça) büyüyordu.
///
/// Hedef adım başına en fazla bir teksel. Tavan tepeden bakışın maliyetini
/// değil, en yatık bakışın maliyetini sınırlıyor: 0.35 m × 3.0 / 0.023 m = 45,
/// 32'de kesiliyor çünkü o noktadan sonra iz zaten mesafeyle küçülüyor.
#define SNOW_RELIEF_STEPS_MIN          8
#define SNOW_RELIEF_STEPS_MAX          32
#define SNOW_RELIEF_MAX_DEPTH          0.35
/// Sıyırtma açıda ışın yatıyor ve XZ kayması patlıyor; tavan olmadan iz
/// metrelerce uzayıp bulaşıyor.
#define SNOW_RELIEF_MAX_STRETCH        3.0

/// GÖLGEDEKİ KARIN ÇEVRESİNDEN ALDIĞI DOLGU.
///
/// Gölge tavanı artık sabit değil, gök payından geliyor (`SnowRelief.hlsl`).
/// Ama gölgedeki kar o payda kalmıyor: kar albedosu 0.85 ve gölge lekesi
/// çevresindeki aydınlık karın yaklaşık yarısını görüyor, çarpımı 0.43.
///
/// Kâğıtta: açık öğle gök payı 0.15 → tavan 0.15 + 0.85×0.43 = 0.52.
/// Alçak güneş 0.40 → 0.66. Kapalı hava 1.0 → 1.0 (gölge yok).
///
/// Eski `SNOW_SHADOW_FLOOR` 0.55'ti ve MEĞER AÇIK ÖĞLE İÇİN DOĞRUYMUŞ;
/// yanlış olan onu her havada ve her saatte kullanmaktı.
#define SNOW_SHADOW_BOUNCE             0.43

/// KAR-KAR YATAY TRANSFERİNİN KATSAYISI.
///
/// Gölgedeki kar çevresindeki aydınlık kardan ışık alıyor. Katkı
/// `albedo × görüş payı × aydınlık kar radyansı`; aydınlık kar radyansının
/// içinde bir albedo daha var. 0.85 × 0.5 = 0.43.
///
/// Kâğıtta uçlar: öğle (NdotL 0.9, gök 0.15) aydınlık 1.43 / gölgeli 0.53,
/// oran 0.37. Şafak (NdotL 0.07, gök 0.02) aydınlık 0.12 / gölgeli 0.05,
/// oran 0.42. İkisi de ölçülü kar gölgesi oranına (0.4-0.6) oturuyor;
/// öncesinde şafakta 0.08'di.
#define SNOW_LATERAL_BOUNCE            0.43

/// TERRAIN KOSE ARALIGI (m). Olculdu: arazi 30000 m, heightmap cozunurlugu
/// 4097 -> 30000/4096 = 7.32 m.
///
/// Tessellation faktoru 64'te (donanim tavani) en ince geometri 7.32/64 =
/// 11.4 cm. Bu bir TAVAN: alt-11-cm hicbir sey geometri olamaz, normal
/// haritasinda kalir. `SNOW_TESS_MIN_DALGA` o tavandan tureniyor.
///
/// DAGIN BOYUNA BAGLI — `SCALE.md`'de kayitli.
#define SNOW_TERRAIN_VERTEX_SPACING    7.32

/// GEOMETRIYE GIREN EN KISA DALGA BOYU (m).
///
/// Kagitta: en ince geometri 11.4 cm (7.32 m / 64). Nyquist bir dalganin
/// tasinabilmesi icin dalga boyunun ornek araliginin iki kati olmasini
/// istiyor -> 22.8 cm. Guvenlik payiyla 50 cm.
///
/// Bunun altindaki oktavlar (ripple 17 cm, mikro 8.3 cm) YER DEGISTIRMEYE
/// GIRMIYOR, normal haritasinda kaliyor. Girerlerse ornekleme frekansinin
/// altinda kalip kamera kipirdadikca titrerler — belirti bir kez olculdu
/// ("zemin tir tir titriyor").
#define SNOW_TESS_MIN_DALGA            0.50

/// BUZUN FRESNEL TABANI (F0).
///
/// Buzun kirilma indisi n = 1.31. Dik gelen isinda
/// F0 = ((n-1)/(n+1))^2 = ((0.31)/(2.31))^2 = 0.018.
///
/// URP'nin dielektrik varsayilani 0.04 (n = 1.5, cam/plastik). Kar o degerle
/// cizilince yuzey 2.2 kat fazla speküler donduruyor.
#define SNOW_ICE_F0                    0.018

/// KARIN PURUZLULUGU: TABAN VE TAZE UC.
///
/// Kuru kar neredeyse Lambert; yansimasi coklu sacilmadan gelir, genis ve
/// yonsuze yakin. Ayna gibi davranan sey islak kar ve buz kabugu — ikisi de
/// kendi carpaniyla ayrica ele aliniyor (`SnowLighting.hlsl`).
///
/// Olculdu: sikismis kar 0.28 puruzlulukteydi (puruzsuzluk 0.72) ve GGX tepe
/// yogunlugu D(0) = 1/(pi*alpha^2) = 52 veriyordu. Ogle vakti duz zeminde
/// diffuse 1.747 / spekuler 4.133 — spekuler payi %70. Kar icin fizik ~%1
/// soyluyor. Ekranda bu "sulu zemin" olarak okunuyordu.
///
/// 0.78 puruzsuzlukte 0.22 demek, D(0) = 0.75. Sikismis kar taze kardan biraz
/// daha DUZ oldugu icin taze uctan dusuk kaliyor, ama 0.72 puruzsuzluk buz
/// kabugu seviyesiydi ve kar degildi.
#define SNOW_ROUGH_PACKED              0.78
#define SNOW_ROUGH_FRESH               0.92

/// YUZEY DOKUSUNUN IKI AYRI MESAFE KAPISI.
///
/// Uc cikti tek `guc` ile birlikte kesiliyordu (8-28 m) ve 28 m'den sonra
/// kar duz beyaz kaliyordu: yerine hicbir sey gelmiyordu. Kullanici bunu
/// "yakindaki detaylar goukuyor ama azcik ilerisi dumduz, oraya dogru
/// yurudukce detaylar geliyor" diye bildirdi.
///
/// KABARTI (normal + puruzluluk) 8-28 m'de kesiliyordu ve KESIM CIZGISI
/// EKRANDAN OKUNUYORDU: yakinda gunes goren kabarti yuzleri parlak lekeler
/// birakiyor, 28 m'den sonra zemin birden duzlesiyordu (kullanici bildirdi:
/// "golgeleme belirli bir yakinlik mesafesinde calisiyor zeminde").
///
/// Kesmenin gerekcesi aliasing'di ama dogru degil: `SnowSurfIkiOlcek` normali
/// EGIM UZAYINDA okuyor ve egimin mip ortalamasi dogal — mip'lenen normal
/// duzlesiyor, patlamiyor. Uzakta detay kendiliginden zayifliyor; ustune bir
/// de kapi koymak duzluk uretiyor.
///
/// 30-120 m: gecis artik ufka yakin ve tek bir cizgi olarak okunmuyor.
///
/// RENK DESENI DAHA DA UZAK. Olculdu: doku 4096^2, doseme 2.5 m -> teksel
/// 0.61 mm. 28 m'de bir ekran pikseli ~89 teksel kapliyor, yani mip 6-7 —
/// trilinear zaten ortaliyor ve desen yumusak bir lekeye donuyor. Kesmek
/// icin bir sebep yok; kesince duzluk kaliyor. Tekrarlamayi stokastik
/// doseme kiriyor.
#define SNOW_SURF_KABARTI_FADE_START  30.0
#define SNOW_SURF_KABARTI_FADE_END   120.0
#define SNOW_SURF_RENK_FADE_START     80.0
#define SNOW_SURF_RENK_FADE_END      250.0


#define SNOW_LOCAL_MIN               0.002

/// İZİN ÇEVRESİNDE ÇİZİLEN ŞERİDİN GENİŞLİĞİ, teksel.
///
/// Oluğun duvarı iz dışındaki düz kar yüzeyine bağlanıyor. O yüzey
/// çizilmezse duvarın üst kenarı boşlukta asılı kalıyor — yandan bakınca
/// havada duran plakalar olarak görüldü. Komşuların en büyüğü alınıp izin
/// çevresinde bu kadar teksel daha çizilince duvar oraya oturuyor.
///
/// Şerit dar tutulur: düz alanda mesh ile arazi yine yan yana geliyor ve
/// aradaki fark orada da var. 3 teksel (7 cm) duvarı bağlamaya yetiyor,
/// göze kuşak olarak okunacak kadar geniş değil.
#define SNOW_LOCAL_SKIRT_TEXELS       9.0

// --- İz oluşumu (spec §10.1) ---
#define SNOW_LOOSE_N                 0.10
#define SNOW_PACKED_N                0.55
/// EN FAZLA BATMA (m) — TAŞIMA GÜCÜ.
///
/// Spec §10.1 `min(penetration, baseH)` diyor, yani kar ne kadar kalınsa ayak
/// o kadar batıyor. O ifade oyuncunun karın ÜSTÜNDE yürüdüğünü varsayıyor;
/// bizim oyuncu araziye bastığı için batma her zaman tabakanın tamamı oluyor
/// ve 20 cm karda 19 cm derinliğinde, dik duvarlı bir çukur açılıyordu
/// (ölçüldü — kesit: 20/20 20/20 | 3/0 3/0 | 20/20).
///
/// Fizikte eksik olan taşıma gücü: bot battıkça altındaki kar sıkışır,
/// yoğunluğu artar ve bir noktada yükü taşıyıp batmayı durdurur. 1 m karda
/// ayak zemine inmez.
///
/// 8 cm taze kar için makul: bot tabanı ~180 cm², 70 kg → ~39 kPa; taze karın
/// (ρ 55) sıkışarak ρ≈200'e ulaştığı derinlik bu mertebede.
///
/// GÖRSEL GEREKÇE DÜŞTÜ, FİZİK GEREKÇESİ DÜZELTİLDİ.
///
/// Sayının ikinci gerekçesi "8 cm'lik oluk mesh'in 4.7 cm'lik köşe aralığıyla
/// yumuşak temsil edilebiliyor, 19 cm'lik çukur edilemiyordu" idi. Kar mesh'i
/// silindi; iz artık arazi yüzeyinde relief mapping ile çiziliyor ve köşe
/// aralığı diye bir sınırı yok.
///
/// Fizik tarafı da eksikti: 39 kPa TAZE karda (ρ≈55) 8 cm'de durmaz. Taze
/// karda ayak 20-30 cm batar — "postholing" denen şey budur. 8 cm sıkışmış ya
/// da rüzgâr paketlemiş kara ait bir sayı.
///
/// Taban 0.22'ye çıkarıldı; sıkışmış karda `sinkScale` onu zaten orantılı
/// indiriyor, yani sert zeminde hâlâ az batılıyor.
///
/// Ölçülen belirti: iz dokusunda oyma tam 0.0800'de tıkanıyordu ve relief
/// 8 cm'den derin bir çukuru hiç göremiyordu (ekranda iz "şeffaf" görünüyordu).
/// DELİĞİN BATMAYLA GENİŞLEMESİ (m yarıçap / m batma).
///
/// Bacak derin kara girerken kenarı itiyor, çıkarken yıkıyor; delik botun
/// ölçüsünde kalmıyor. 0.22: 22 cm batmada 7 cm yarıçap 12 cm'ye çıkıyor.
/// ÇÖKÜNTÜNÜN KUYRUĞU: kenardaki payı ve uzunluğu (yarıçap katı).
///
/// Taşıma gücü göçmesinde kayma yüzeyleri temelin dışına taşıyor; çevre kar
/// geniş bir alanda hafifçe çöküyor. Kuyruk olmadan oyma kapsülün kenarında
/// BİTİYOR ve iz düz kara tek bir çizgiyle bağlanıyor.
///
/// UZUNLUK KÂĞITTA SINIRLANDI. Önce 1.5 yarıçaptı ve toplam iz genişliği
/// 1.3 m çıkıyordu — insan izi değil hendek (kullanıcı bildirdi: "bu genişlik
/// haliz mi"). İnsan izi derin karda 40-55 cm.
///
/// 0.6 yarıçap ile (R = 8.2 cm): kuyruk 4.9 cm'de 1/e'ye, 12 cm'de %9'a
/// iniyor. Toplam yarı-genişlik 0.10 + 0.082 + 0.12 = 30 cm, iz 60 cm.
/// Geçiş bandı yine 5 tekselden 8 teksele çıkıyor ama iz şişmiyor.
#define SNOW_SETTLE_TAIL             0.12
#define SNOW_SETTLE_TAIL_LEN         0.55

/// KUYRUK UZUN VE SIĞ. Önce kısa ve derindi (0.40 yarıçap, %20 pay) ve izin
/// kenarı tek bir koyu hat olarak okunuyordu. Uzatıp sığlaştırmak geçişi
/// yumuşatıyor ama izi ŞİŞİRMİYOR: kenarda 1.8 cm, 15 cm ötede 2 mm.
///
/// Menzil sabit değil: `SNOW_SETTLE_TAIL_SCALE` ile dünya uzayında kırılıyor,
/// yoksa izin çevresine kusursuz dairesel bir hale çiziyor (`SYMPTOMS.md`).
///
/// 1.00'de kuyruk tek başına 6.9 cm yarıçap ekliyordu ve izin görünür
/// genişliğinin beşte ikisiydi. 0.55 onu 5.4 cm'ye indiriyor; saçaklanma
/// menzilde olduğu için kısalma haleyi geri getirmiyor.

/// Kuyruk menzilini kıran gürültünün ölçeği (1/m). 5 = 20 cm dalga boyu.
#define SNOW_SETTLE_TAIL_SCALE       5.0

#define SNOW_HOLE_FLARE              0.08

/// İZ GENİŞLİĞİ BATMAYA BAĞLI — TAVAN ONDAN SEÇİLDİ.
///
/// Batma arttıkça `KRepose`'un göçürdüğü duvar da yükseliyor ve omuz
/// genişliyor: yayılım `(batma − yarıçap − duruş) / tan(38°)`. 0.35'te iz
/// 90 cm çıkıyordu (kullanıcı bildirdi: "bu genişlik haliz mi", "hâlâ çok
/// geniş"). Derin karda insan izi 50-60 cm.
#define SNOW_MAX_SINK                0.15

#define SNOW_PACKED_SINK_SCALE       0.18
/// SIKIŞMA KAZANCI — ULAŞILAN OYMANIN FONKSİYONU.
///
/// Spec §10.1 `compact = SNOW_COMPACT_RATE * saturate(...)` diyor ama neye
/// orantılı olduğunu söylemiyor. Denenen iki yol da belirti üretti:
///
/// - `× _SnowDeltaTime`: BEKLEME SÜRESİNE bağlı. Yerinde bekleyen oyuncunun
///   altında iz çukur gibi derinleşiyordu (yoğunluk arttıkça `baseH` düşüyor).
/// - kare başına AÇILAN oymaya orantılı: tekselin yoğunluğuna hangi karede
///   basıldığı kazınıyor. Çapraz yürüyüşte yoğunluk alanı enine ÇİZGİLİ
///   çıkıyordu (ölçüldü, `SYMPTOMS.md`).
///
/// Doğrusu ulaşılan oymanın kar sütununa oranı: `trail.r / baseH`. İdempotent,
/// kare sayısından ve yol geometrisinden bağımsız. Patika yine oluşuyor çünkü
/// yoğunluk arttıkça `baseH` düşüyor ve sonraki geçişin oranı yükseliyor.
/// KAZANÇ ÖLÇÜLDÜ VE ÇOK DÜŞÜKTÜ.
///
/// 0.15 ile izin içindeki yoğunluk 96 kg/m³ çıkıyordu — hâlâ TAZE kar.
/// Yürünmüş kar gerçekte 200-300 kg/m³. Sonucu ekranda şuydu: yüzey dokusu
/// izin içinde ve dışında AYNI katmanı seçiyor (`packed = (ρ−100)/250` her
/// ikisinde de 0), yani iz farklı bir malzeme gibi değil, aynı malzemenin
/// gölgeli bir yaması gibi duruyordu (kullanıcı bildirdi: "izin texture'ı
/// dışardaki karla aynı değil galiba", "uyum yok").
///
/// 0.60 ile yoğunluk 218 kg/m³: yürünmüş karın ortası. Doku ağırlığı 0.47,
/// yani yerleşmiş (topaklı) katman yarı yarıya devreye giriyor; albedo
/// 0.90 → 0.83, pürüzlülük 0.72 → 0.57. İz kararmıyor, MALZEME değiştiriyor.
#define SNOW_COMPACT_GAIN            0.60

// --- Kenar yığılması (spec §10.2) ---
#define SNOW_RIM_VELOCITY_BIAS       0.04
/// SIRT GÜCÜ — HACİM KORUNUMUNDAN, KEYFİ DEĞİL.
///
/// 1.8 idi ve 20 cm karda `raised × 1.8 × 0.8 ≈ 20 cm` sırt hedefi çıkıyordu;
/// tavan 10 cm'e kırpsa bile karın YARISI kadar bir duvar demek. Ekranda iz
/// kanyona, kenarı diken diken bir sıraya dönüşüyordu (kullanıcı bildirdi).
///
/// Oyma burada SIKIŞTIRMA: kar yoğunlaşıyor, hacminin çoğu yana taşınmıyor.
/// Yana taşınan pay yalnız sıkışmayan kısım ve o da izin çevresine, izden
/// GENİŞ bir halkaya yayılıyor. İkisi birlikte sırtı oymanın küçük bir kesrine
/// indiriyor.
#define SNOW_RIM_STRENGTH            0.55

/// Tavan batmanın yarısı kadar: 8 cm oluğun kenarında 4 cm'lik yumuşak bir
/// kabartı. Spec §10.2 "bu parça atlanırsa izler ÇUKUR gibi görünür" diyor;
/// bir tur 2 cm'e kısıldı ve oluk gerçekten çukur gibi kaldı.
/// Sırdın gölgelemeye giren payı. Tam yazıldığında izin çevresinde koyu bir
/// çerçeve oluşuyordu: `KRim`'in profili 2-3 tekselde tepeye çıkıyor ve yan
/// eğimi 30-40°'ye ulaşıyor. Gerçek bir yığın o kadar dik duramaz.
#define SNOW_RIM_SHADE               0.35

#define SNOW_RIM_MAX                 0.04

/// Sırt topaklarının ölçeği (1/m). 7 = 14 cm topak; sırdın kendi genişliği
/// `_RimBlurTexels` 7 teksel × 2.4 cm = 17 cm, yani topak sırtla aynı
/// mertebede. Daha ince olsaydı gürültü, daha kaba olsaydı sırt yer yer
/// tamamen kaybolurdu.
#define SNOW_RIM_CLUMP_SCALE         7.0

/// Topaklar arası en düşük sırt payı. 0 olsaydı sırt kesik kesik olurdu;
/// 0.35 süreksizliği gösteriyor ama sırdı koparmıyor.
#define SNOW_RIM_CLUMP_FLOOR         0.35
#define SNOW_RIM_REF_DEPTH           0.25
#define SNOW_RIM_BLUR_TEXELS         7.0

/// EĞİM FARKININ ADIMI, teksel.
///
/// 1 teksel merkezi fark tam ızgara Nyquist'inde çalışıyor ve merdiveni EN ÇOK
/// büyüten adım o. Duvarın kendi eğimi 3 teksele yayıldığı için 2 tekselllik
/// fark duvarı kaybetmiyor, merdiveni ise söndürüyor.
#define SNOW_DENT_SLOPE_TEXELS       2.0

// --- İzlerin dolması (spec §10.3) ---
/// KARIN DURUŞ AÇISININ TANJANTI.
///
/// Gevşek kuru kar ~38°'ye kadar duruyor [KAYNAK: Cordonnier ve ark., EG 2018,
/// §5.4 — talus açısı]. tan(38°) = 0.781.
/// KAR YARI SAYDAM — ÇUKURUN GÖLGESİ MAVİYE KAYIYOR.
///
/// Buzun soğurma katsayısı 600 nm'de 450 nm'dekinin ~10 katı; çoklu saçılmada
/// foton yolu uzadığı için derin bir çukurda kırmızı kaybolur, mavi kalır.
/// Gerçek kar fotoğraflarının en tanınır özelliği bu.
///
/// Uç değer `SNOW_RELIEF_MAX_DEPTH`'te; sığ izde etkisi yok.
#define SNOW_SSS_TINT                float3(0.90, 0.94, 1.00)

#define SNOW_REPOSE_TAN              0.781

/// DUVARIN KENDİ KENDİNE DURABİLDİĞİ YÜKSEKLİK (m).
///
/// TALUS AÇISI TEK BAŞINA YANLIŞ. O açı KOHEZYONSUZ tanelerin açısı; kar
/// sinterlenir ve gerçek bir kohezyonu vardır. Günlük gözlem de bunu söylüyor:
/// karda ayak izinin duvarı DİK durur, tepesinde küçük bir göçük olur.
///
/// Saf talus modeli 22 cm'lik izi her yana 28 cm açıyordu — toplam 76 cm
/// (kullanıcı bildirdi: "iz şu an çok geniş"). Kohezyon eklenince yalnız bu
/// yüksekliğin ÜSTÜNDE kalan pay göçüyor; altı dik kalıyor.
///
/// Kohezyon yoğunlukla artıyor: taze kar az tutar, sıkışmış kar çok.
///
/// KAR KUM DEĞİL, KOHEZYONLU. Serbest duran duvar yüksekliği
/// `h = 2c / (rho g)`; taze tozda c ≈ 300-1000 Pa ve rho ≈ 100 kg/m³ veriyor,
/// yani **60 cm ile 2 m**. Kar mağarasının kazılabilmesinin sebebi bu.
///
/// Değer 4 cm'ken 20 cm karda duvarın 11 cm'i göçüyor ve 14 cm omuz açıyordu:
/// iz 56 cm çıkıyordu (kullanıcı bildirdi: "20 ve 50 cm'dekiler çok büyük
/// geniş izler"). 12 cm hâlâ kohezyon hesabının çok altında — muhafazakâr
/// seçildi ki `SNOW_MAX_SINK` (15 cm) altında bir miktar göçme kalsın ve
/// kenar bıçak gibi dik durmasın.
///
/// Ölçüldü (kâğıtta): 4 cm → iz 56 cm, 12 cm → iz 35 cm, 14 cm → iz 31 cm.
/// 12 cm'de de "aşırı iz" bildirildi; 14 cm kohezyon hesabının hâlâ çok
/// altında ve `SNOW_MAX_SINK`'in (15 cm) 1 cm altında kalıyor — duvarın son
/// santimi göçüyor, kenar bıçak gibi dik durmuyor.
#define SNOW_STAND_LOOSE             0.140

/// Sıkışmış kar DAHA YÜKSEK duvar tutar, daha az değil. Aynı formülde
/// c ≈ 5-20 kPa ve rho = 550 kg/m³ metrelerce veriyor. Değer 0.07'ydi ve
/// `SNOW_STAND_LOOSE` 0.12'ye çıkınca sıralama TERSİNE dönmüştü:
/// `lerp(LOOSE, PACKED, packed)` sıkışmış karda duvarı alçaltıyordu.
#define SNOW_STAND_PACKED            0.200

/// KENARIN DÜZENSİZLİĞİ ve dalga boyu (1/m).
///
/// Duruş yüksekliği yerel olarak dalgalanınca omzun bittiği yer de dalgalanıyor
/// ve iz kenarı düz bir çizgi olmaktan çıkıyor.
///
/// GENLİK KÂĞITTA DOĞRULANDI. Kenarın kayması `durus × genlik / tan(38°)`:
///   eski (durus 0.06, genlik 0.45) → ±3.5 cm = ±1.5 teksel  → ZİGZAG
///   yeni (durus 0.015, genlik 0.50) → ±1.0 cm = ±0.4 teksel → teksel altı
/// Aynı gürültü, dört kat küçük duruş yüksekliğiyle görünür bir dalga
/// üretemiyor; yalnız kenarı kemiriyor.
///
/// Dalga boyu 1/8 = 12.5 cm = 5.3 teksel. Izgaradan uzun (taşınabiliyor),
/// omuzdan (3 teksel) uzun (omzu yok etmiyor).
#define SNOW_STAND_NOISE             0.50
#define SNOW_STAND_NOISE_SCALE       8.0

/// DAMGA SİLUETİNİN KEMİRİLMESİ ve dalga boyu (1/m).
///
/// Kusursuz kapsül kenarı "kalıptan çıkmış" okunuyor. Yarıçap tekselin DÜNYA
/// konumuna bağlı gürültüyle modüle ediliyor; düzensizlik zeminde sabit
/// duruyor, damga hareket ederken kenar titremiyor.
///
/// ÜÇ OKTAV: 1/9 = 11.1 cm, sonra 5.6 ve 2.8 cm. Sonuncusu tekselin (2.34 cm)
/// hemen üstünde; daha ince oktav ızgarada aliasing yapar.
///
/// Toplam genlik (1 + 0.5 + 0.25) × 0.18 = ±%31 × 5.5 cm yarıçap = ±1.7 cm.
/// En ince oktavın kendi payı ±0.4 cm — teksel altı, zigzag üretemez.
#define SNOW_EDGE_BREAK              0.18
#define SNOW_EDGE_BREAK_SCALE        9.0

/// OLUĞUN ORTASINDAKİ SIRT — iki ayak arasında ezilmeyen şerit.
///
/// Duruş genişliği 17 cm, ayak eni 11 cm; arada ~6 cm dokunulmamış kar kalır.
/// Tek bir kapsül bunu kendiliğinden veremez, o yüzden oyma eksende bilerek
/// sığ bırakılıyor: çıkıntı ayrı bir geometri değil, OYULMAYAN kar.
///
/// KRepose SIRTI SİLMİYOR — KÂĞITTA DOĞRULANDI. `KRepose` bir maksimum
/// filtresi; sırt tekseli komşusundan sığ olduğu için dolma riski var.
/// Sırt 4 cm yüksek ve 7 cm yarı-genişlikte, yani yan eğimi
/// atan(4/7) = 30° — duruş açısının (38°) ALTINDA, dolayısıyla stabil.
/// Sayıyla: 22 cm derin komşu 3 teksel (7 cm) ötede, göçme onu
/// 22 − 7×tan38° = 16.5 cm'e taşıyor; sırt tekseli zaten 18 cm derin,
/// 18 > 16.5 olduğu için `max` sırta dokunmuyor.
///
/// Oran batmaya göre: derin karda sırt da yükseliyor, eğimi 38°'yi aşınca
/// kısmen eriyor. Bu doğru davranış — dize kadar batılan karda iki ayak
/// arasında sırt kalmaz.
#define SNOW_MIDRIDGE                0.26
#define SNOW_MIDRIDGE_WIDTH          0.085

/// MİKRO RÖLYEF — üç oktav, metre cinsinden genlik ve 1/m cinsinden ölçek.
///
/// SİMÜLASYON ALANINA YAZILAMAZ. Denendi: `KRepose` bir MAKSİMUM filtresi ve
/// 12 teksel menzille komşuların en derinine göre dolduruyor; 11 cm dalga
/// boylu bir gürültüyü tamamen süpürdü (kullanıcı bildirdi: "oluğun içi çok
/// düzenli"). Detay bu yüzden ÇİZİM tarafında.
///
/// ÇİZİM TARAFINDA IZGARA YOK. Değer piksel başına hesaplanıyor, 2.34 cm'lik
/// teksel ızgarasında örneklenmiyor; dalga boyu tanenin kendi boyuna kadar
/// inebiliyor. Kar dokusunun kendi normal haritası da aynı işi yapıyor ama o
/// izin İÇİYLE DIŞINI ayırt etmiyor — bu alan yalnız bozulmuş karda var.
///
/// Dalga boyları 8.3 / 3.6 / 1.6 cm; genlikler 20 / 9 / 4 mm.
/// MİKRO GENLİKLER — ÖLÇÜ EĞİM, YÜKSEKLİK DEĞİL.
///
/// 0.008/0.004/0.0015 idi; taban çarpanı (0.55) ile birlikte üç oktavın
/// eğimleri 18°/21°/18°, RMS'i 33° — yüzeyin en büyük tek kaynağı ve
/// arazide ölçülen kar yüzeyi RMS eğiminin (5-15°) iki katı.
///
/// Dalga boyları 8/4/2 cm; yakın planda ekranda birkaç piksel ediyorlar,
/// yani dik eğim doğrudan keskin gradyana dönüşüyor. 0.4 katsayısıyla
/// RMS 13°'ye iniyor.
#define SNOW_MICRO_AMP_A             0.0022
#define SNOW_MICRO_AMP_B             0.0011
#define SNOW_MICRO_AMP_C             0.0004
#define SNOW_MICRO_SCALE_A           12.0
#define SNOW_MICRO_SCALE_B           27.5
#define SNOW_MICRO_SCALE_C           62.0

/// Bozulmamış kardaki mikro rölyef payı. 0 = düz kar tamamen özelliksiz.
///
/// Sıfırdı ve iz ile çevresi arasında detay uçurumu açılıyordu. Gerçek kar
/// yüzeyi de pürüzlü; fark derece farkı olmalı, varlık-yokluk farkı değil.
#define SNOW_MICRO_BASE              0.55

/// Mikro rölyefin tam güce ulaştığı oyma derinliği (m). Sığ izde zayıf,
/// derin izde tam.
#define SNOW_MICRO_REF_DEPTH         0.06

#define SNOW_WIND_FILL               0.0012

// --- Birikme, oturma, erime (spec §11) ---
#define SNOW_SETTLE_TAU          21600.0
#define SNOW_DISTURB_TAU           900.0
#define SNOW_MELT_DDF                4.63e-8

/// ERİME ANAHTARI. 0 = kapalı, 1 = açık.
///
/// Tasarım kararı: yağan kar kolay kolay erimemeli. Erime sonra ele alınacak;
/// formül yerinde duruyor ki geri açmak tek sayı olsun (`DECISIONS.md`).
#define SNOW_MELT_ENABLED            0.0
#define SNOW_DRIFT_BIAS              0.45
#define SNOW_RAIN_MELT_BOOST         2.5
#define SNOW_SWE_MAX                 0.60

// --- Yağış (spec §3.4, §17.2) ---
#define SNOW_MAX_SWE_RATE            1.39e-6
#define SNOW_MAX_FLAKE_RATE      16000.0

// --- Gökyüzü görünürlüğü (spec §12.1) ---
#define SNOW_SKY_AREA_SIZE          96.0
#define SNOW_SKY_MOVE_THRESHOLD      4.0

// --- Rüzgâr taşınımı (spec §18.0, §18.1) ---
#define SNOW_WINDSHADOW_C            0.7
#define SNOW_EROSION_RATE            1.16e-6
#define SNOW_DRIFT_U10_LOOSE         5.0
#define SNOW_DRIFT_U10_PACKED       11.0

// --- Isı kaynakları (spec §18.2) ---
#define SNOW_MAX_HEAT_SOURCES       16
#define SNOW_HEAT_MELT_RATE          0.0009
#define SNOW_HEAT_WET_RATE           0.25

// --- Kabuk (spec §18.3) ---
#define SNOW_T_WARM                  5.0
#define SNOW_T_COOL                 -5.0
#define SNOW_T_FREEZE              -20.0
#define SNOW_CRUST_GAIN              1.4e-4
#define SNOW_CRUST_WIND_GAIN         6.0e-5
#define SNOW_CRUST_MELT_TAU       1200.0
#define SNOW_CRUST_BURY            220.0
#define SNOW_CRUST_SOLID             0.55
#define SNOW_CRUST_BREAK_PEN         0.05
#define SNOW_CRUST_SINK_SCALE        0.04

/// EN DIŞ HALKANIN ETEĞİ bu kadar aşağı iniyor (rapor §5).
///
// --- Sastrugi (spec §18.4) ---
/// --- KAR YÜZEYİ YER ŞEKİLLERİ [KAYNAK: Filhol & Sturm 2015;
///     Kochanski ve ark. 2019, The Cryosphere 13:1267] ---

/// fBm TABANI. Doğal yüzeyler self-affine: `C(q) ~ q^(-2(H+1))`. Kar için
/// H = 0.8, yani oktav başına genlik oranı 2^(-0.8) = 0.574.
///
/// ÖLÇÜLDÜ: GÖRÜNÜRLÜĞÜ GENLİK DEĞİL EĞİM BELİRLİYOR.
///
/// İlk değerler (4.5 m dalga, ±9 cm) eğim olarak 2.3° veriyordu ve ekranda
/// yüzey dümdüz kalıyordu. Genlik 10 katına çıkarılıp bakıldığında rölyef net
/// göründü — yani bağlantı çalışıyor, sorun eğimdi. `atan(2A/λ)` oranı
/// belirliyor; kısa dalga boyu aynı genlikte çok daha dik.
///
/// Oktavlar: 1.25 / 0.63 / 0.31 / 0.16 m, genlikler 5.5 / 3.2 / 1.8 / 1.0 cm,
/// eğimler 5.0 / 5.7 / 6.6 / 7.4°. Toplam RMS 6.9 cm.
///
/// Metre üstü ölçek (ölçülen "snow wave", 10-20 m) bilerek yok: bizim bölge
/// 24 m ve o dalga boyu tek bir eğime dönüşüp yüzeyi eğik gösterir.
/// Yer şekli genliğinin kar derinliğine oranı tavanı.
///
/// Sastrugi ve ripple kar tabakasını OYAN şekiller; tabakadan derin olamazlar.
/// Bu bağ olmadan 1 cm ile 50 cm kar arasında hiçbir görsel fark kalmıyor.
/// ÖLÇÜLDÜ: 0.35 ile 1 cm ve 5 cm ayrılmıyordu. Büyük ölçekli yüzey kontrastı
/// (32-64 piksel bloklar) 1cm 4.65, 5cm 4.91, 20cm 6.66, 50cm 6.66 — yani
/// yalnız sığ/derin ayrımı vardı, ara basamaklar yoktu.
///
/// 0.60: 5 cm karda tavan 3 cm, 20 cm'de 12 cm, 50 cm'de 30 cm. fBm'in en
/// büyük oktavı 5.5 cm olduğu için 1/5/20 basamakları ayrışıyor.
/// Ölçülen sastrugi derinliği 14-40 cm ve o kar tabakası 50+ cm; oran 0.3-0.8
/// bandında, 0.60 ortası.
#define SNOW_BEDFORM_DEPTH_FRAC      0.60

/// Arazi oyuklarının tamamen gömüldüğü kar kalınlığı (m).
#define SNOW_BURY_REF_DEPTH          0.30

/// fBm TABAN GENLİĞİ — ÖLÇÜ EĞİM, YÜKSEKLİK DEĞİL.
///
/// 0.055 idi ve dört oktavın RMS eğimi 35° çıkıyordu; arazide ölçülen
/// kar yüzeyi RMS eğimi 5-15°. Taban oktav tek başına 15.5°'ydi.
///
/// Belirti alçak güneşte görünüyordu: 35°'lik bir yüzey, güneş 2.4°'de
/// iken NdotL'yi 0 ile 0.6 arasında gezdiriyor ve zemin keskin kenarlı
/// açık/koyu adacıklara ayrılıyor. Anahtar taramasıyla ölçüldü: on üç
/// terimden yalnız fBm oranı değiştirdi (0.75 → 0.86), ötekiler ±0.02.
///
/// 0.022 ile RMS eğim 15° — ölçülmüş aralığın üst ucu, yani rüzgârlı
/// kar. Genliği düşürmek detayı silmiyor; detay hissi eğimden geliyor
/// ve 15° hâlâ görünür.
#define SNOW_FBM_AMP                 0.015
#define SNOW_FBM_SCALE               0.80
#define SNOW_FBM_GAIN                0.574

/// Yüzey çukurlarının ortam örtmesi. Normal katkısı yalnız direkt ışıkla
/// görünüyor; güneş tepedeyken yüzey düz okunuyor. Çukurun göğü daha az
/// görmesi ise ışık yönünden bağımsız ve öğlen de çalışıyor.
#define SNOW_SURFACE_AO              0.50

/// RIPPLE. Ölçülen: 0.5-2 cm yüksek, 10-25 cm dalga boyu, rüzgâra DİK.
/// 17 cm ve ±1.2 cm seçildi -> eğim 8°. Kar hareket eşiği 7 m/s; altında
/// yeni ripple oluşmuyor ama var olan siniyor, o yüzden taban 0.35.
#define SNOW_RIPPLE_AMP              0.012
#define SNOW_RIPPLE_LENGTH           0.17
#define SNOW_RIPPLE_BASE             0.24

/// SASTRUGİ TABANI. Oluşumu 20 m/s istiyor; oyunda o rüzgâra ancak fırtınada
/// çıkılıyor. Taban 0.25: sakin havada yüzey plane bed'e yakın, fırtınada
/// sastrugi alanına dönüyor.
/// 0.08 = sakin havada gerçekten PLANE BED. 0.25 idi ve rüzgâr sıfırken
/// bile 4.5 cm sastrugi bırakıyordu — 60 cm dalga boyunda 25° eğim, yani
/// yüzeyin en dik tek bileşeni. Yorumun kendi hedefi "sakin havada yüzey
/// plane bed'e yakın" diyordu ama sayı onu vermiyordu.
///
/// 0.08 ile genlik 1.44 cm, eğim 8.6° — sakin havada okunur ama yüzeyi
/// domine etmiyor. Fırtınada rüzgâr çarpanı zaten 1'e çıkarıyor.
#define SNOW_SASTRUGI_BASE           0.055

#define SNOW_SASTRUGI_TAU          900.0
#define SNOW_SASTRUGI_BURY         260.0
/// Ölçülen sastrugi derinliği 14-40 cm, sivri uç aralığı 45-90 cm.
///
/// LENGTH RÜZGÂRA DİK EKSENDE, WIDTH RÜZGÂR YÖNÜNDE (`SnowYuzeyRolyef`).
/// Bir tur LENGTH 0.60 → 2.00 yapıldı "eğim çok dik" diye; YANLIŞ
/// EKSENDİ ve sastrugiyi enine şişirip yönsüzleştirdi. Geri alındı.
///
/// O turdaki eğim ölçümü de hatalıydı: genlik `HEIGHT × BASE` ile
/// çarpılıyor, yani 18 cm değil 4.5 cm. Gerçek eğim 2π×0.045/0.60 =
/// 0.47, yani 25° — arazi ölçümüyle uyumlu. Sastrugi suçsuz.
#define SNOW_SASTRUGI_HEIGHT         0.180
#define SNOW_SASTRUGI_LENGTH         0.60
#define SNOW_SASTRUGI_WIDTH          2.20
#define SNOW_SASTRUGI_WIND_TAU     120.0

/// DRIFT — BIRIKME TEPECIKLERI.
///
/// Sastrugi erozyon sekli: ruzgar kari OYUYOR, keskin sirt ve dik yuz
/// birakiyor. Drift bunun tersi: ruzgarin tasidigi kar bir yerde COKUYOR ve
/// yuvarlak, yumusak tepecik birakiyor. Ikisi ayni yerde olmuyor —
/// `SnowYuzeyRolyef` ikisini ruzgar maruziyetiyle ayiriyor.
///
/// [KAYNAK: Filhol & Sturm 2015, kar yer sekilleri sinifi — olculen aralik
/// 2 cm (ripple) ile 2.5 m (whaleback dune) arasi; drift tepecikleri bu
/// araligin ortasinda.]
///
/// Genlik tepe-dip 30 cm, dalga boyu 90 cm -> egim 2*pi*0.15/0.90 = 1.05,
/// yani 46 derece. Karin durus acisi 38-45 derece; drift o sinirin hemen
/// ustunde duruyor cunku birikme sirasinda kar kendini destekliyor
/// (kohezyon, `SNOW_STAND_LOOSE` ile ayni fizik).
#define SNOW_DRIFT_HEIGHT              0.30
#define SNOW_DRIFT_LENGTH              0.90

/// Ruzgar yonundeki uzama. Drift tepecikleri ruzgar boyunca uzuyor ama
/// sastrugi kadar degil (sastrugi 2.20 m).
#define SNOW_DRIFT_WIDTH               1.60

// --- İz içi AO (spec §18.5) ---

// --- Süspansiyon perdeleri (spec §18.7) ---
#define SNOW_SUSP_SCALE_H            1.1
#define SNOW_SUSP_ALPHA_BASE         0.16
#define SNOW_SUSP_MAX_HEIGHT         5.0

// --- Püskürtme (spec §18.6) ---
#define SNOW_SPRAY_PARTICLES_PER_M3  40000.0

// --- Hesaplama (spec §20) ---
#define SNOW_GROUP_SIZE              8


#endif
