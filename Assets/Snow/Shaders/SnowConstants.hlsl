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

// --- Yakalama (spec §9.1, §9.4) ---
#define SNOW_CAPTURE_BELOW           3.0
#define SNOW_CAPTURE_ABOVE           3.0
/// YAKALAMA BULANIKLIĞI KENARIN YUMUŞAKLIĞINI BELİRLİYOR.
///
/// ESKİ GEREKÇE YANLIŞTI. "Çapraz gidişte ızgara merdiveni" deniyordu; ölçüm
/// bunu çürüttü: kenardaki dişlerin periyodu YÜRÜME HIZIYLA ölçekleniyor
/// (1.2 m/s'de 20 teksel, 0.3 m/s'de 7 teksel), yani ızgaradan değil damga
/// kadansından geliyor. Izgara merdiveni olsaydı periyot hızdan bağımsız
/// olurdu. Damga kadansı ayrı bir kayıt (`SYMPTOMS.md`).
///
/// Bulanıklığın gerçek işi kenarı yumuşatmak: 2.5 teksel (5.9 cm) bandı
/// oluğun duvarını üç teksele yayıyor ve kapsama tepesini düşürmüyor
/// (ölçüldü: en derin nokta 22.00 cm, iz 10305 teksel).
///
/// ÜST SINIR ÖLÇÜLDÜ. 4.0 teksel denendi ve izi ÖLDÜRDÜ: bulanıklık kapsama
/// payını yayarken zayıflatıyor (`RT_CaptureBlur` tepe değeri 1.00 → 0.80),
/// oyma sığlaşıyor ve iz görünürlük eşiğinin altında kalıyor — dokuda 5000
/// teksel yerine 110 teksel kaldı. 2.0 teksel (4.7 cm) kapsamayı tam
/// tutarken kenarı yumuşatıyor.
#define SNOW_BLUR_RADIUS_TEXELS      2.5

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
#define SNOW_RELIEF_SHADOW_STRENGTH    0.5

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
#define SNOW_MAX_SINK                0.22

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
#define SNOW_COMPACT_GAIN            0.15

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
#define SNOW_CARVE_SMOOTH_TEXELS     3.0

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
#define SNOW_STAND_LOOSE             0.06
#define SNOW_STAND_PACKED            0.18

/// DURUŞ YÜKSEKLİĞİNİN DÜZENSİZLİĞİ ve dalga boyu (1/m).
///
/// Sabit yükseklik omuzun dış sınırını DÜZ bir çizgi yapıyor; iz kenarı
/// keskin ve tekdüze çıkıyor (kullanıcı bildirdi: "iz kenarı çok keskin,
/// istediğim dağılmaya sahip değil").
///
/// Dalga boyu 1/5.5 = 18 cm; omuz 4-9 teksel (9-21 cm). Dalga boyu omuzdan
/// UZUN: kısa olsaydı omuzu yok ederdi (dalga boyu kuralı, `RATIONALE.md`).
#define SNOW_STAND_NOISE             0.45
#define SNOW_STAND_NOISE_SCALE       5.5

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
#define SNOW_SASTRUGI_TAU          900.0
#define SNOW_SASTRUGI_BURY         260.0
#define SNOW_SASTRUGI_HEIGHT         0.035
#define SNOW_SASTRUGI_LENGTH         0.35
#define SNOW_SASTRUGI_WIDTH          1.20
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
