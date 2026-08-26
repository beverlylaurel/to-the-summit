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

/// Çukurun kendi gölgesi. Alçak güneşte yakın duvar gölgelenmezse ayak izi
/// tümsek gibi okunuyor (ölçüldü: 17:00'de ters görünüyordu).
/// Yuzey dokusu MIKRO detay: yakinda var, uzakta yok. Acik kalirsa gorus
/// alanindaki butun kar ayni desenle kapaniyor.
#define SNOW_SURF_FADE_START           8.0
#define SNOW_SURF_FADE_END             28.0

#define SNOW_RELIEF_SHADOW_STEPS       5
/// İZ-İÇİ GÖLGENİN TABANI — KAR YARI SAYDAM.
///
/// Engelleme tam olsa bile gölge siyaha inmiyor: ışık kar tanelerinin
/// arasından çok kez saçılarak çukurun içine sızıyor. Taze karın tek-saçılma
/// albedosu 1'e çok yakın, difüzyon güçlü.
///
/// Eskiden gölgenin GÜCÜ 0.5'e çekilerek aynı sonuç aranıyordu; o, engellemeyi
/// zayıflatıyor, yani kenar geçişini de siliyordu. Doğrusu engellemeyi tam
/// bırakıp TABANI yükseltmek: gölge keskin ama karanlık değil.
/// Ölçülü kar gölgesi/güneş oranı açık gökte 0.5–0.6; kar o kadar yüksek
/// albedolu ki gölgeli yüzey çevre kardan ve gökten aydınlanmaya devam
/// ediyor. 0.45 bu aralığın altındaydı.
#define SNOW_SHADOW_FLOOR              0.55

/// Güneş ufka yakınken iz-içi gölgenin gücü. Alçak güneşte ışın uzun yol
/// alıyor ve `engel` her yerde doyuyor; gücü kısmazsak iz akşam simsiyah
/// oluyor (ölçüldü: 17:49, gündüz oranı 0.33, iz çevresinden çok koyu).
#define SNOW_SHADOW_LOW_SUN            0.35


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
#define SNOW_SETTLE_TAIL_LEN         0.70

/// Kuyruk menzilini kıran gürültünün ölçeği (1/m). 5 = 20 cm dalga boyu.
#define SNOW_SETTLE_TAIL_SCALE       5.0

/// KUYRUK UZUN VE SIĞ. Önce kısa ve derindi (0.40 yarıçap, %20 pay) ve izin
/// kenarı tek bir koyu hat olarak okunuyordu. Uzatıp sığlaştırmak geçişi
/// yumuşatıyor ama izi ŞİŞİRMİYOR: kenarda 1.8 cm, 15 cm ötede 2 mm.

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
#define SNOW_RIM_REF_DEPTH           0.25
#define SNOW_RIM_BLUR_TEXELS         7.0

/// Oymanın GÖRÜNTÜ için yayılma yarıçapı, teksel.
///
/// GEREKÇESİ DEĞİŞTİ, DEĞERİ DE. Eskiden 4 teksel (9.4 cm) idi ve işi "duvarı
/// eğimlemek"ti: oyma DİK BASAMAK olarak yazılıyordu (0 → 80 mm → 0, ölçüldü)
/// ve bulanıklık onu tek başına eğimlendiriyordu.
///
/// Basamak kaynağında düzeltildi — oymanın profilini artık yakalamanın
/// kapsama payı veriyor ve üç tekselde yumuşakça iniyor (`KDeform`). Geriye
/// kalan tek iş MESH'İN TAŞIYABİLECEĞİ bant genişliği: köşe aralığı 4.7 cm =
/// 2 teksel, Nyquist bunun altındaki her şeyi merdiven yapar.
///
/// 4 teksel kalsaydı 19 cm'lik oluğun yarısını düzleştirirdi. Ölçüldü: oluk
/// son görüntüyü yalnız %2.3 değiştiriyordu (lineer %5.5), oysa 48°'lik bir
/// duvar Lambert'te %34 koyulaştırır.
///
/// 3.0'da bırakıldı. Bir tur "ızgara merdivenini kesmek için" diye 1.5'e
/// indirilip geri alındı; o gerekçe ölçümle çürüdü (bkz. yakalama bulanıklığı
/// — dişlerin kaynağı damga kadansı). Yarıçapın işi duvarı yumuşatmak.
/// ÖLÇÜLDÜ: 2.6 teksel (6 cm) geçiş bandı, iz ise 50 cm geniş — ekranda
/// geçiş izin %5'i, kenar tek bir koyu hat olarak okunuyor (kullanıcı
/// defalarca bildirdi: "kenarlarda koyulaşma var, sanki border gibi").
///
/// 5.5 teksel = 13 cm, yani izin dörtte biri. Geometri değişmiyor; değişen
/// yalnız normalin kenarda ne kadar geniş bir bantta döndüğü.
#define SNOW_CARVE_SMOOTH_TEXELS     5.5

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
/// TAZE KAR 1.5 CM'DEN FAZLA DİK DUVAR TUTAR.
///
/// 1.5 cm ile duvarın neredeyse tamamı göçüyordu ve omuz 17 cm'ye ulaşıyordu.
/// Gerçek ayak izinin kenarı bir süre dik durur — ayağın ittiği duvar
/// SIKIŞMIŞTIR, çevredeki gevşek kar gibi davranmaz.
#define SNOW_STAND_LOOSE             0.040
#define SNOW_STAND_PACKED            0.07

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
/// Dalga boyu 1/9 = 11 cm = 4.7 teksel (ızgaradan uzun). Genlik ±%18 × 5.5 cm
/// yarıçap = ±1 cm = 0.42 teksel — teksel altı, zigzag üretemez.
/// KENARIN KIRILMASI — blok genliği ve hücre ölçeği (1/m).
///
/// Taşıma gücü yenildiğinde kenar kayma yüzeyi boyunca kopuyor ve kohezyonlu
/// kar açısal parçalara ayrılıyor. Blok bileşeni hücre içinde SABİT, sınırda
/// basamaklı — kenarın parça parça kopmasını veren bileşen bu.
///
/// Hücre 1/11 = 9.1 cm = 3.9 teksel. Izgaradan büyük (temsil edilebiliyor),
/// oluk genişliğinin (30 cm) altında (blok blok görünüyor, oluğu bozmuyor).
/// Genlik ±%22 × 15 cm yarıçap = ±3.3 cm = ±1.4 teksel — kenarın gözle
/// görülür biçimde kopması için gereken en küçük değer.
#define SNOW_EDGE_BLOCK              0.08
#define SNOW_EDGE_BLOCK_SCALE        11.0

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
#define SNOW_MICRO_AMP_A             0.008
#define SNOW_MICRO_AMP_B             0.004
#define SNOW_MICRO_AMP_C             0.0015
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

#define SNOW_FBM_AMP                 0.055
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
#define SNOW_RIPPLE_BASE             0.35

/// SASTRUGİ TABANI. Oluşumu 20 m/s istiyor; oyunda o rüzgâra ancak fırtınada
/// çıkılıyor. Taban 0.25: sakin havada yüzey plane bed'e yakın, fırtınada
/// sastrugi alanına dönüyor.
#define SNOW_SASTRUGI_BASE           0.25

#define SNOW_SASTRUGI_TAU          900.0
#define SNOW_SASTRUGI_BURY         260.0
/// Ölçülen sastrugi derinliği 14-40 cm, sivri uç aralığı 45-90 cm.
/// Yükseklik 18 cm, aralık 60 cm -> eğim 31°. Erozyon şekli, dik olması
/// doğru.
#define SNOW_SASTRUGI_HEIGHT         0.180
#define SNOW_SASTRUGI_LENGTH         0.60
#define SNOW_SASTRUGI_WIDTH          2.20
#define SNOW_SASTRUGI_WIND_TAU     120.0

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
