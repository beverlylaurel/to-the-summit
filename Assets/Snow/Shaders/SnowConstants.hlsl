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
/// 4 mm → 24 mm bandı, tam şiddette ~13 dakika: kar önce çukurlara düşüyor,
/// lekeler büyüyor, sonunda sürekli örtü oluyor. Ani sıçrama yok.
#define SNOW_EDGE_FADE_RANGE         0.020

// --- Yakalama (spec §9.1, §9.4) ---
#define SNOW_CAPTURE_BELOW           3.0
#define SNOW_CAPTURE_ABOVE           3.0
#define SNOW_BLUR_RADIUS_TEXELS      1.5

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
/// SIKIŞMA HIZI — SANİYE BAŞINA, KARE BAŞINA DEĞİL.
///
/// Spec §10.1 `compact = SNOW_COMPACT_RATE * saturate(...)` diyor ve `dt`
/// içermiyor; kare başına uygulanınca KARE HIZINA BAĞLI oluyor. 100 fps'de
/// 0.1 saniyelik bir ayak teması 10 kare eder ve rhoN 0.10'dan 0.55'e tek
/// adımda çıkıyor — kar bir basışta tamamen sıkışıyor.
///
/// Sonucu ölçüldü: `baseH = SWE × 1000 / ρ` 20 cm'den 3 cm'ye düşüyor ve iz
/// 19 cm derinliğinde, dik duvarlı bir çukur oluyor. Oymayı sınırlamak
/// çözmüyor çünkü derinlik oymadan değil SIKIŞMADAN geliyor.
///
/// Spec'in kendi davranış tarifi: "aynı hattan 5–6 geçişten sonra batma %18'e
/// düşer, patika oluşur". Ayak teması ~0.3 s; 6 geçiş 1.8 s eder. rhoN'un
/// 0.10'dan 0.55'e (Δ0.45) 1.8 saniyede çıkması için hız 0.25/s.
#define SNOW_COMPACT_RATE            0.25

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

/// Oymanın GÖRÜNTÜ için yayılma yarıçapı, teksel. 2.3 cm/teksel × 3 = 7 cm,
/// yani ayak genişliğiyle aynı mertebe: çukur ayağın izini koruyor ama duvarı
/// eğimleniyor ve mesh onu merdivensiz temsil edebiliyor.
#define SNOW_CARVE_SMOOTH_TEXELS     4.0

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
