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
/// Yakalama teksel ızgarasına raster ediliyor; gövde ÇAPRAZ giderken ardışık
/// damgalar ızgarayla 45° yapıyor ve kenar basamak basamak çıkıyor
/// (ekrandan görüldü: düz gidişte kenar temiz, çaprazda merdiven). 1.5 teksel
/// (3.5 cm) bandı bunu örtmeye yetmiyor.
///
/// ÜST SINIR ÖLÇÜLDÜ. 4.0 teksel denendi ve izi ÖLDÜRDÜ: bulanıklık kapsama
/// payını yayarken zayıflatıyor (`RT_CaptureBlur` tepe değeri 1.00 → 0.80),
/// oyma sığlaşıyor ve iz görünürlük eşiğinin altında kalıyor — dokuda 5000
/// teksel yerine 110 teksel kaldı. 2.0 teksel (4.7 cm) kapsamayı tam
/// tutarken kenarı yumuşatıyor.
#define SNOW_BLUR_RADIUS_TEXELS      1.5

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
#define SNOW_SPARKLE_FADE_START      6.0
#define SNOW_SPARKLE_FADE_END        16.0

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
/// Görsel sonucu da bu: 8 cm derinliğinde, 25 cm genişliğinde bir oluk
/// mesh'in 4.7 cm'lik köşe aralığıyla YUMUŞAK temsil edilebiliyor. 19 cm'lik
/// çukur edilemiyordu.
#define SNOW_MAX_SINK                0.08

/// TEK GEÇİŞTE EN FAZLA YOĞUNLAŞMA (normalize birim).
///
/// SWE korunuyor, yani `baseH = SWE × 1000 / ρ`. Yoğunluk artışı DOĞRUDAN
/// yükseklik kaybı demek: rhoN 0.01'den 0.55'e çıkınca 20 cm kar 3 cm'ye
/// iniyor ve iz 17 cm derinliğinde bir çukur oluyor (ölçüldü).
///
/// Sınır olmadan bir ayak teması yoğunluğu tepeye çıkarıyordu. Tek geçişte
/// 0.06 ile spec'in "5–6 geçişten sonra patika oluşur" tarifi de korunuyor
/// (0.10 → 0.55 arası ~7 geçiş), ama TEK iz sığ kalıyor: 20 cm karda
/// yoğunluk 55 → 85, yükseklik 20 → 13 cm.
#define SNOW_MAX_COMPACT_PER_PASS    0.06

#define SNOW_PACKED_SINK_SCALE       0.18
/// SIKIŞMA KAZANCI — AÇILAN OYMA BAŞINA, GEÇEN SÜRE BAŞINA DEĞİL.
///
/// Spec §10.1 `compact = SNOW_COMPACT_RATE * saturate(...)` diyor. Kare
/// başına uygulanınca KARE HIZINA, `dt` ile çarpılınca BEKLEME SÜRESİNE
/// bağlı oluyor. İkincisi ölçülebilir bir belirti üretti: yerinde bekleyen
/// oyuncunun altında iz yuvarlak bir çukur gibi derinleşiyordu (kullanıcı
/// bildirdi). Yoğunluk arttıkça `baseH = SWE × 1000 / ρ` düşüyor.
///
/// Kar öyle davranmaz: yük sabitken sıkışma bir kerede dengeye gelir.
/// Sıkışma artık o karede AÇILAN oymaya orantılı — ilk temasta oluyor,
/// sonraki karelerde `yeniOyma` sıfır olduğu için duruyor. Kare hızından da
/// bağımsız: toplam oyma kare sayısına bağlı değil.
///
/// Değer: ilk temasta `yeniOyma / baseH` ≈ 0.08/0.20 = 0.4; kazanç 0.15 ile
/// `compact` ≈ 0.06 çıkıyor, yani tam olarak
/// `SNOW_MAX_COMPACT_PER_PASS` tavanı. Spec'in "5–6 geçişten sonra patika"
/// tarifi böylece korunuyor: her geçiş bir tavan dolduruyor.
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
#define SNOW_CARVE_SMOOTH_TEXELS     2.0

/// İZ KENARININ DAĞILMASI — leke boyu (1/m) ve genlik.
///
/// Kapsama payı yanal profili veriyor ve tek başına DÜZGÜN bir oluk kenarı
/// üretiyor: iki kenar da matematiksel olarak paralel, göz bunu yapay
/// buluyor (kullanıcı bildirdi). Gerçek karda kenar göçer, tanecik kayar,
/// sınır lekeli biter.
///
/// GÜRÜLTÜ EŞİĞE DEĞİL, OKUMA KONUMUNA UYGULANIYOR.
///
/// Önce kapsama bir eşik gibi kesildi (`(kapsama - gürültü*A) / (1-A)`).
/// Ölçüldü ve battı: bölme rampanın kontrastını `1/(1-A)` kadar artırıyor,
/// A=0.60'ta kenar rampası tamamen yok oldu — kesit `0 0 0 80 80 … 80 0`
/// oldu, iz 21.9 cm'den 14.6 cm'ye indi ve yer yer tek tekselde koptu.
/// Kenar sapması istenen bandın (3–5 cm) içine ancak izi bozarak giriyordu.
///
/// Kapsamanın OKUNDUĞU teksel kaydırılınca rampa olduğu gibi taşınıyor:
/// sınır oynuyor, profil bozulmuyor, iz kopmuyor. Merkezde kapsama düz
/// olduğu için kaydırmanın etkisi yok — düzensizlik yalnız kenarda görünür.
///
/// İki ölçek: 2.5 (40 cm leke) ana düzensizliği, 9.0 (11 cm) kenarın
/// kendi tırtığını veriyor. Tek ölçek ya çok yumuşak ya çok gürültülü.
///
/// KAYDIRMA GENLİĞİ RAMPA GENİŞLİĞİNİ AŞAMAZ. Rampa 4 teksel; 1.5 teksel
/// (≈3.5 cm) sınırı gözle görünür oynatıyor ama iki kenarı birbirine
/// geçirmiyor. 22 cm genişliğinde bir olukta doğal sapma 3–5 cm.
#define SNOW_TRAIL_EDGE_SCALE_A      2.5
#define SNOW_TRAIL_EDGE_SCALE_B      9.0
#define SNOW_TRAIL_EDGE_WARP_TEXELS  1.5

// --- İzlerin dolması (spec §10.3) ---
#define SNOW_FILL_GAIN             900.0
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
#define SNOW_AO_RADIUS               0.10
#define SNOW_AO_STRENGTH             1.0

// --- Süspansiyon perdeleri (spec §18.7) ---
#define SNOW_SUSP_SCALE_H            1.1
#define SNOW_SUSP_ALPHA_BASE         0.16
#define SNOW_SUSP_MAX_HEIGHT         5.0

// --- Püskürtme (spec §18.6) ---
#define SNOW_SPRAY_PARTICLES_PER_M3  40000.0

// --- Hesaplama (spec §20) ---
#define SNOW_GROUP_SIZE              8


#endif
