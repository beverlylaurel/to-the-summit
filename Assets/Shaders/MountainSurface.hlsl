#ifndef MOUNTAIN_SURFACE_INCLUDED
#define MOUNTAIN_SURFACE_INCLUDED

// Dağ yüzeyinin tarifi. Yalnızca "burada ne var" sorusunu cevaplar; ışık hesabı
// UniversalFragmentPBR'a ait. Gölge, derinlik ve normal geçişleri de URP'nin kendi
// dosyalarından geliyor — kafa lambası gibi ek ışıklar bedava çalışsın diye.

#include "MountainSurfaceInput.hlsl"
// Teşhis için: `SnowMacroDepth` ve `SnowDisplacement` fragment'ta okunuyor. Koruma
// makrosu var, `SnowTessellation.hlsl` ikinci kez dahil ettiğinde sorun çıkmıyor.
#include "SnowDisplacement.hlsl"

// Dünya koordinatı binlerce metreye çıkıyor. sin tabanlı hash o ölçekte float
// hassasiyetini tüketip piksel piksel gürültüye dönüşür; hücre indeksi önce küçük
// bir periyoda katlanır, tekrar kilometrelerce ötede kalır.
float MountainHash(float3 p)
{
    // Tohum burada uygulanıyor, çağrı yerlerinde değil — bkz. `SnowDrift.hlsl`,
    // `_PatternSeed`. Ölçekli koordinata eklendiği için her katman farklı dünya
    // mesafesi kadar kayıyor; katmanlar birbirinden bağımsız yenileniyor.
    p = fmod(abs(p + _PatternSeed.xyz), 512.0);
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float MountainNoise(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float n000 = MountainHash(i),                     n100 = MountainHash(i + float3(1, 0, 0));
    float n010 = MountainHash(i + float3(0, 1, 0)),   n110 = MountainHash(i + float3(1, 1, 0));
    float n001 = MountainHash(i + float3(0, 0, 1)),   n101 = MountainHash(i + float3(1, 0, 1));
    float n011 = MountainHash(i + float3(0, 1, 1)),   n111 = MountainHash(i + float3(1, 1, 1));

    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

/// Değer + ANALİTİK GRADYAN, tek örneklemede. Sonlu farkla eğim çıkarmak aynı
/// gürültüyü 3 kez örneklemek demek (8 hash × 3); türev kapalı formda zaten
/// elimizdeki 8 köşeden çıkıyor.
///   n = trilinear(köşeler, u),  u = f²(3−2f),  du/df = 6f(1−f)
float MountainNoiseD(float3 p, out float3 grad)
{
    float3 i = floor(p), f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float3 du = 6.0 * f * (1.0 - f);

    float n000 = MountainHash(i),                     n100 = MountainHash(i + float3(1, 0, 0));
    float n010 = MountainHash(i + float3(0, 1, 0)),   n110 = MountainHash(i + float3(1, 1, 0));
    float n001 = MountainHash(i + float3(0, 0, 1)),   n101 = MountainHash(i + float3(1, 0, 1));
    float n011 = MountainHash(i + float3(0, 1, 1)),   n111 = MountainHash(i + float3(1, 1, 1));

    float k0 = n000;
    float k1 = n100 - n000;
    float k2 = n010 - n000;
    float k3 = n001 - n000;
    float k4 = n000 - n100 - n010 + n110;
    float k5 = n000 - n010 - n001 + n011;
    float k6 = n000 - n100 - n001 + n101;
    float k7 = -n000 + n100 + n010 - n110 + n001 - n101 - n011 + n111;

    grad = du * float3(k1 + k4 * u.y + k6 * u.z + k7 * u.y * u.z,
                       k2 + k5 * u.z + k4 * u.x + k7 * u.z * u.x,
                       k3 + k6 * u.x + k5 * u.y + k7 * u.x * u.y);

    return k0 + k1 * u.x + k2 * u.y + k3 * u.z
         + k4 * u.x * u.y + k5 * u.y * u.z + k6 * u.z * u.x
         + k7 * u.x * u.y * u.z;
}

/// fbm + gradyan. Oktav başına koordinat 2.03 ile ölçeklendiği için gradyan da
/// aynı çarpanla büyür (zincir kuralı).
float MountainFbmD(float3 p, int octaves, out float3 grad)
{
    float sum = 0.0, amp = 0.5, freq = 1.0;
    grad = 0.0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        if (i >= octaves) break;
        float3 g;
        sum += MountainNoiseD(p, g) * amp;
        grad += g * (amp * freq);
        p *= 2.03;
        freq *= 2.03;
        amp *= 0.5;
    }
    return sum;
}

// Gürültü 3D olarak dünya konumundan örnekleniyor: hiçbir projeksiyon yok, dolayısıyla
// dik yamaçta gerilme de yok. Triplanar yalnızca 2D doku eşlemesi için gerekir.
float MountainFbm(float3 p, int octaves)
{
    float sum = 0.0, amp = 0.5;

    // [unroll]: oktav sayısı her çağrı yerinde SABİT bir sayı (2 ya da 3) ama döngü
    // dinamik yazıldığı için derleyici açamıyordu — her oktav için dallanma, döngü
    // sayacı ve register tutuluyordu. Açılınca `i >= octaves` derleme zamanında
    // çözülüyor ve gövde düz koda iniyor. Sonuç bit düzeyinde aynı.
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        if (i >= octaves) break;
        sum += MountainNoise(p) * amp;
        p *= 2.03;
        amp *= 0.5;
    }
    return sum;
}

/// Jeolojik bantlar: yatay katmanlar, tektonikle bükülmüş. Bükülme olmadan düz çizgiler
/// çıkar ve dağ pastadan kesilmiş gibi görünür.
float MountainBand(float3 worldPos)
{
    float warp = (MountainFbm(worldPos * _BandWarpScale, 2) - 0.5) * _BandWarp;
    float band = (worldPos.y + warp) / max(_BandThickness, 1.0);

    // Üçgen dalga: katmanlar arasında yumuşak gidip gelme
    return abs(frac(band) * 2.0 - 1.0);
}

struct MountainSurface
{
    half3 albedo;
    half3 emission;
    half  smoothness;
    half  occlusion;
    float3 normalWS;
};

/// Arazinin güneş gölgesi, pişirilmiş ufuk haritasından.
///
/// Nokta gölgededir ancak ve ancak güneş, o yöndeki ufuk açısının altındaysa. Ufuk
/// açısı alanı pürüzsüz ve dağla birlikte sabit; güneşin azimutuna komşu iki yön
/// okunup harmanlanır, yükseklik açısı ufukla karşılaştırılır. İki doku okuması,
/// sıfır rastgelelik.
///
/// Işın yürüyüşü denendi ve iki kez geri alındı: tek ışın artı eşik, kenarda ya jilet
/// ya nokta üretiyor — gürültüyü çözecek zamansal birikim yok. Gölge haritası ondan
/// önce denendi: üçgen silüetlerin gölgesi sırtlarda testere dişiydi. İkisinin de
/// kökü aynıydı, gölgeyi örneklenmiş bir yüzeyden türetmek; ufuk haritası onu
/// pürüzsüz bir açı alanından türetiyor.
///
/// Yarı gölge, güneşin ufka açısal yakınlığından: ufkun hemen üstünde alacakaranlık
/// bandı, altında tam gölge. Gerçekte de dağ gölgesinin kenarı böyle yumuşar.
float TerrainSunShadow(float3 worldPos, float3 sunDir)
{
    if (sunDir.y < 0.0) return 1.0;   // gece ışığını kendi yönü söndürür

    float2 uv = (worldPos.xz - _TerrainOrigin.xz) / _TerrainSize.xz;

    // PİŞİRİLMİŞ ALANIN DIŞINDA GÖLGE YOK. Ufuk haritası yalnız arazinin kapladığı
    // kutu için pişiriliyor. Dışarıda uv [0,1]'den çıkıyor ve dokunun sarma kipi ne
    // verirse o okunuyordu; yüksek bir ufuk okununca `smoothstep` SIFIR dönüyor, yani
    // doğrudan güneş tamamen kesiliyor.
    //
    // Belirti: ovada, gündüz, zemin simsiyah. Ölçüm zinciri sırayla eledi — sis
    // (hacim probu zeminde kırmızı: `renk × 1 + 0`), froxel hacmi, bulut gölgesi
    // cookie'si, gölge haritası (`_TerrainShadowReceive` anahtarı hiçbir şey
    // değiştirmedi çünkü BU yolu kesmiyor). Yüzey probu `renk × 8` ile de siyah kaldı:
    // yüzeye giren değer zaten sıfırdı.
    //
    // Gece/gündüz kapısı da buradan: fonksiyon `sunDir.y < 0` iken 1.0 dönüyor, yani
    // gölge yalnız güneş ufkun üstündeyken hesaplanıyor. Belirtinin 08:02'de başlayıp
    // 19:11'de bitmesinin sebebi bu — kullanıcı sınırı dakikasıyla ölçtü.
    //
    // Dışarıda doğru cevap "engel yok": pişirilmiş arazi orada bitiyor ve güneşi
    // kesecek bir kütle de yok. Ufuk sıfır sayılır, yüzey tam aydınlanır.
    if (any(uv != saturate(uv))) return 1.0;

    // Azimut hangi iki pişirilmiş yönün arasında?
    const float TwoPi = 6.2831853;
    float sector = atan2(sunDir.z, sunDir.x) / TwoPi * 16.0;
    sector += sector < 0.0 ? 16.0 : 0.0;

    float lower = floor(sector);
    float blend = sector - lower;

    float a0 = SAMPLE_TEXTURE2D_ARRAY_LOD(_HorizonMap, sampler_HorizonMap,
                   uv, fmod(lower, 16.0), 0).r;
    float a1 = SAMPLE_TEXTURE2D_ARRAY_LOD(_HorizonMap, sampler_HorizonMap,
                   uv, fmod(lower + 1.0, 16.0), 0).r;

    float horizon = lerp(a0, a1, blend) * 1.5707963;
    float elevation = FastASin(saturate(sunDir.y));

    // Yarı gölgenin açısal genişliği (radyan): ufkun altına az, üstüne geniş —
    // gölgenin içi dolu kalır, kenarı alacakaranlık gibi açılır
    return smoothstep(horizon - 0.02, horizon + 0.10, elevation);
}

/// Alpenglow: ayrı bir ışık değil, kızıllaşmış güneşin KENDİSİ — vadi Dünya'nın
/// gölgesine girmişken yüksek yüzeyler hâlâ doğrudan ışık alır. Zirvenin pembe-kızıl
/// parlaması budur.
///
/// Güneş ufuktayken pay arazi gölgesine kapılanır: gölgedeki yamaç parlamaz. Eski
/// hâli gölgesiz emisyondu ve şafakta sahneyi düz, gözü alan bir vuruşla yakıyordu —
/// ışık, ışık gibi davranmalı. Batımdan sonra kalan pay artçı parıltıdır (atmosferde
/// saçılmış ışık): gölgesiz ama cılız.
half3 Alpenglow(float3 worldPos, float3 normalWS, float altitude, half3 albedo,
                float exposure)
{
    if (_SurfaceDawnStrength <= 0.001) return 0.0;

    // Doğrudan faz mı, artçı parıltı mı: güneş ufkun üstünde mi?
    float directPhase = smoothstep(-0.02, 0.06, _SurfaceDawnDir.y);

    // DÜNYA GÖLGESİ TIRMANIR. Alpenglow'u tanınabilir yapan şey, gölge çizgisinin
    // yamaçta yukarı yürümesidir: vadi önce söner, ışık zirveye çekilir. Sabit bir
    // irtifa bandı bunu veremiyordu — dağın tamamı birlikte pembeleşip birlikte
    // sönüyordu, yapay duran şey buydu.
    //
    // Gölgenin üst sınırı güneşin ufuk altı açısından çıkar: h = R(1/cosθ − 1),
    // küçük açıda ≈ R·θ²/2. _SurfaceDawnDir.y güneş yüksekliğinin sinüsü, yani
    // θ ≈ −y. Zirvemiz ~2100 m: gösteri 0° ile −1.5° arasında geçer.
    //   0.5° → 240 m,  1.0° → 975 m,  1.5° → 2190 m
    float below = max(0.0, -_SurfaceDawnDir.y);
    float shadowHeight = 6371000.0 * 0.5 * below * below;

    // Sınır keskin değil: gölge kenarı atmosferde birkaç yüz metreye yayılır.
    float lit = lerp(smoothstep(shadowHeight - 300.0, shadowHeight + 300.0, altitude),
                     1.0, directPhase);
    if (lit <= 0.0) return 0.0;

    // YÖN yalnız doğrudan fazda. Güneş battıktan sonra aydınlatan şey noktasal bir
    // kaynak değil, kızıla boyanmış BÜTÜN GÖKYÜZÜ. Yönlü sönüm o fazda fiziksel
    // karşılığı olmayan bir maskeye dönüşüp parlamayı tek yamaca hapsediyordu.
    float facing = saturate(dot(normalWS, _SurfaceDawnDir.xyz) * 0.5 + 0.5);
    facing = lerp(1.0, facing, _AlpenglowFacing * directPhase);

    // Yüzey rengi parlamayı etkiler ama tamamen belirlemez: kar kızıla boyanır, koyu
    // kaya daha az yakalar. Doğrudan albedo ile çarpmak bazalt gibi koyu bir yüzeyde
    // parlamayı görünmezliğe itiyor.
    half3 receptivity = lerp(0.45, albedo, 0.65);

    // GÖLGELEME de faza bağlı. Doğrudan fazda güneş gölgesi doğru ölçü. Batıştan
    // sonra kaynak gökyüzü olduğu için güneş yönlü gölge anlamsız — o noktanın
    // gökyüzünü ne kadar gördüğü (maruziyet) doğru ölçüdür: çukur az alır, sırt çok.
    float shade = TerrainSunShadow(worldPos, _SurfaceDawnDir.xyz);
    float gate = lerp(lerp(0.25, 1.0, exposure), shade, directPhase);

    return receptivity * _SurfaceDawnColor.rgb
         * (_SurfaceDawnStrength * lit * facing * gate);
}

/// Kar parlaması: taze karın buz kristali yüzcükleri minik aynalardır — her biri
/// güneşi ancak yarım-vektörle tam hizadayken bir anlığına yansıtır. Parıltı dünyada
/// sabittir (kristal yerinde durur); oyuncu hareket edince yüzcükler hizaya girip
/// çıkar ve kar yürüdükçe yanıp söner. Yalnız doğrudan güneş ışığında olur — gölgede
/// kar parıldamaz (arazi gölgesi kapılar) — ve ancak yakın mesafede seçilir: açısal
/// bir olay, uzakta pırıltı kaybolur. Yakınlık kapısı aynı zamanda maliyet kapısı.
/// Güneşin yüksekliği (sinüs), TimeOfDay'in yayınladığı GLOBAL. Materyal
/// property'si üzerinden gelen sürüm bu kapıyı kapatmıyordu.

half3 SnowSparkle(float3 worldPos, float3 normalWS, float cover)
{
    if (cover <= 0.1) return 0.0;

    // Güneş battıysa kaynak yok; ay pırıltısı gerçekte de gözle zor seçilir, atlanır.
    // Ufuktaki güneş de parıldatır (kızıl çakımlar) — alt sınır ufkun hemen altı.
    // Kapı yalnız "güneş var mı" demiyor, ŞİDDETİ de taşıyor. Çakım
    // _SurfaceDawnColor ile çarpılıyor ve o renk tepe kanalı 1'e normalize edilmiş
    // bir TON — parlaklık taşımıyor. Sadece varlık kapısı kullanılınca ortalık
    // kararırken çakımlar aynı kalıyor ve zemin yıldız tarlasına dönüyordu.
    // Güneş alçaldıkça sönmesi gerek: 11°'de tam, ufukta sıfır.
    // Bant yukarı çekildi (8.6° → 27°) ve kareyle keskinleştirildi: alacakaranlıkta
    // sahne loşken çakımlar tam güçte kalıp göze batıyordu. Pırıltı güneşin gerçekten
    // yükseldiği saatlerin işidir.
    float sunUp = smoothstep(0.15, 0.45, _SunHeight);
    sunUp *= sunUp;
    if (sunUp <= 0.0) return 0.0;

    float3 toCamera = _WorldSpaceCameraPos - worldPos;
    float distance = length(toCamera);
    if (distance > 75.0) return 0.0;

    // Gölgedeki kar parıldamaz: doğrudan güneş şartı
    float shade = TerrainSunShadow(worldPos, _SurfaceDawnDir.xyz);
    if (shade <= 0.02) return 0.0;

    float3 viewDir = toCamera / max(distance, 0.01);
    float3 halfVector = normalize(viewDir + _SurfaceDawnDir.xyz);

    float sparkle = 0.0;

    // --- Yakın alan: tek tek kristaller ---
    // ~3 cm'lik hücreler, her hücrede kristal yok; kristal hücre içinde tek NOKTA.
    // Nokta gerçek boyuna yakın tutulur, uzaklaştıkça yalnız örtüşmeyi önleyecek
    // kadar büyür ve parlaklığı alanıyla bölüşür (enerji korunumu) — büyüyen nokta
    // parlamaz, yayvanlaşır.
    float near = 1.0 - smoothstep(25.0, 75.0, distance);
    if (near > 0.0)
    {
        // Tazelik parlaklığı değil YOĞUNLUĞU sürer: yaşlı karda sağlam yüzcük azalır
        // ama kalan yüzcük yine ayna gibi parlaktır. Parlaklığı kısmak çakımları
        // bloom eşiğinin altına itiyor ve pırıltıyı toptan görünmez ediyordu —
        // eşik bir uçurum, kısılan çakım sönmüyor, YOK oluyor.
        float3 cell = floor(worldPos * 30.0);
        // SIKLIK MESAFEYLE SEYRELİR. Hücre ızgarası dünyaya sabit (3.3 cm), dolayısıyla
        // piksel başına düşen hücre sayısı mesafenin KARESİYLE artıyor: uzakta çakımlar
        // piksel altı istatistiğe dönüşüp parazit gibi okunuyordu. Geçen hücre oranını
        // 1/mesafe² ile kısmak ekrandaki çakım YOĞUNLUĞUNU sabit tutar — menzili
        // kesmeye gerek kalmaz, uzak yamaç da pırıldar ama seyrek.
        float keepNear = 1.0 - lerp(0.6, 0.3, SampleSnowProfile(worldPos.y - _TerrainOrigin.y).r);
        // Referans 25 m: o mesafeye kadar tam yoğunluk, ötesinde ters kareyle
        // seyrelir (50 m'de %25, 75 m'de %11). Referans 8 m denendi ve fazla geldi —
        // 30 m'de yoğunluk %7'ye iniyor, pırıltı pratikte kayboluyordu.
        float keep = keepNear * saturate(625.0 / max(1.0, distance * distance));

        if (MountainHash(cell.xzy) >= 1.0 - keep)
        {
            // Nokta boyu ekran-sabit — bilinçli ödün: gerçek kristal boyuna inmek
            // denendi ve söküldü, çekirdek pikselaltına düşüp örneklenemez oluyor
            // ve pırıltı tamamen kayboluyordu.
            float3 sub = frac(worldPos * 30.0) - 0.5;
            float radius = lerp(0.10, 0.34, smoothstep(8.0, 75.0, distance));
            float core = smoothstep(radius, radius * 0.35, length(sub));

            if (core > 0.0)
            {
                // Çoğu kristal silik, nadiri göz alır — tekdüzelik etiket gibi durur
                float bright = MountainHash(cell.zyx);
                bright = bright * bright * bright;

                // Yüzcükler TAM rastgele yatar: dar dağılım pırıltıyı belirli bakış
                // geometrisine hapsedip ekranın yarısını boş bırakıyordu
                float3 jitter = float3(MountainHash(cell), MountainHash(cell.yzx),
                                       MountainHash(cell.zxy)) - 0.5;
                float3 facet = normalize(normalWS + jitter * 2.2);

                // Lob 120 -> 40: 120 çok dar, kamera azıcık oynayınca çakım açıyı
                // kaybedip PAT diye sönüyordu — kıpır kıpır, sinir bozucu. Geniş lob
                // aynı çakımı birkaç derecede yumuşak açıp kapatır: pırıltı kalır,
                // titreşim gider.
                float glint = pow(saturate(dot(facet, halfVector)), 16.0);

                // Genlik ekranda kalibre edildi. Başlangıçta 2.5+9.5 idi: çakım 12'ye
                // kadar çıkıyor, _SurfaceDawnColor tepe kanalı 1'e normalize olduğu için
                // şiddet taşımıyor ve bloom eşiği deliniyordu — öğleden sonra göz alıyordu.
                sparkle += glint * (0.18 + 0.72 * bright) * core * near;
            }
        }
    }

    // Uzak alan pırıltı şeridi DENENDİ VE SÖKÜLDÜ: pikselaltı yüzcük istatistiğini
    // hücre gürültüsüyle taklit etmek, güneş yolu yerine yamacı kaplayan kirli bir
    // çizik dokusu bastı. Uzak pırıltı ancak gerçek pikselaltı örnekleme/temporal
    // birikimle olur; o maliyete değmez — yakın alan kristalleri yeter.

    return _SurfaceDawnColor.rgb * (sparkle * cover * sunUp * shade);
}

/// Yüzey haritalarını kübik B-spline ile okur — dört bilinear okumanın ağırlıklı
/// birleşimi.
///
/// Bilinear yetmiyor: kar maskesi bu kanalların çarpımına dar bir eşik vuruyor ve
/// bilinear alanların eş değer çizgileri texel köşelerinde X biçiminde kırılıyor —
/// maske kenarı 17 metrelik ızgaranın kristal desenini giyiyordu. Teşhisle kesinlendi:
/// desen hiçbir bileşen kanalında yok, yalnız eşiklenmiş sonuçta. B-spline alanı C1
/// kurar; eş değer çizgileri kırıksız, kristal yapısal olarak imkânsız.
float4 SampleSurfaceMaps(float2 uv)
{
    float2 t = uv * _SurfaceMapsSize.xy - 0.5;
    float2 cell = floor(t);
    float2 f = t - cell;

    float2 f2 = f * f;
    float2 f3 = f2 * f;

    float2 w0 = (1.0 - 3.0 * f + 3.0 * f2 - f3) / 6.0;
    float2 w1 = (4.0 - 6.0 * f2 + 3.0 * f3) / 6.0;
    float2 w2 = (1.0 + 3.0 * f + 3.0 * f2 - 3.0 * f3) / 6.0;
    float2 w3 = f3 / 6.0;

    float2 g0 = w0 + w1;
    float2 g1 = w2 + w3;

    float2 p0 = (cell - 0.5 + w1 / g0) * _SurfaceMapsSize.zw;
    float2 p1 = (cell + 1.5 + w3 / g1) * _SurfaceMapsSize.zw;

    return g0.y * (g0.x * SAMPLE_TEXTURE2D(_SurfaceMaps, sampler_SurfaceMaps, float2(p0.x, p0.y))
                 + g1.x * SAMPLE_TEXTURE2D(_SurfaceMaps, sampler_SurfaceMaps, float2(p1.x, p0.y)))
         + g1.y * (g0.x * SAMPLE_TEXTURE2D(_SurfaceMaps, sampler_SurfaceMaps, float2(p0.x, p1.y))
                 + g1.x * SAMPLE_TEXTURE2D(_SurfaceMaps, sampler_SurfaceMaps, float2(p1.x, p1.y)));
}

struct SnowCoverage
{
    float cover;   // örtü maskesi: 0 çıplak kaya, 1 tam örtü
    float depth;   // kabartı kalınlığı: normali yuvarlar, tarağı ve pütürü besler
    float burial;  // GÖMÜLME kalınlığı: altındaki taş görünüyor mu. Birikinti KARIŞMAZ
    float patch;   // serpinti gürültüsünün ham değeri; sastrugi kümelenmesi yeniden kullanır
    float shelter; // arazi birikim ağırlığı, 0.67-2.0. Rüzgârın burada ne kadar yavaşladığı
    float fresh;   // taze pay: yeni yağmış toz mu, yıllanmış névé mi
};

/// Kar örtüsü ve kalınlığı. Kapsama "kar var mı" der, kalınlık "ne kadar" — ikisi
/// aynı kaynaktan gelir ama farklı yerlerde zirve yapar: rüzgâr sırttan alıp oyuğa
/// bırakır, dik yüzde tutunacak yer yoktur. Kar bir renk olarak kaldığı sürece dağ,
/// üstüne beyaz sürülmüş kaya gibi duruyordu.
///
/// micro: kabartı gürültüsünün değeri, çağıran hesaplayıp veriyor. Yeni gürültü
/// örneği alınmıyor; prosedürel normalin zaten ürettiği değer paylaşılıyor.
SnowCoverage BuildSnowCoverage(float3 worldPos, float3 normalWS, float altitude,
                               float slope, float concavity, float micro)
{
    float snowFit = smoothstep(cos(radians(_SnowSlopeLimit)) - 0.16,
                               cos(radians(_SnowSlopeLimit)) + 0.10, slope);

    // ARAZİ AĞIRLIĞI. Rüzgâra bakan yüz süpürülür, arkasında birikir; oyuk dolar,
    // sırt kazınır. İkisi ayrı terim değil: arazi rüzgârın HIZINI değiştiriyor, hız da
    // birikimi. Eskiden `lee` anlık normalden, `hollow` konkavlık kanalından ayrı ayrı
    // geliyordu — aynı fiziği iki yerden hesaplamak demekti ve geometrik derinlik
    // eklenince üçüncü bir kopya çıkacaktı.
    //
    // Ağırlık PİŞMİŞ (bkz. `SurfaceMapBaker.BakeDriftWeight`): Liston & Sturm'ün
    // W = 1 + 0.5·Ωs + 0.5·Ωc bağıntısı, birikim 1/W. Gölgelendirme, geometri ve
    // çarpışma üçü de bu tek dokuyu okuyor.
    float shelter = SampleDriftWeight(worldPos);

    // Kar çizgisi tek bir kot değil, yerel bir değer. Sabit kot, dağın çevresini
    // dolaşan temiz bir kontur çiziyor ve etekte boyanmış bir bant gibi okunuyordu.
    // Gerçek çizgiyi üç şey oynatır ve üçü de buradan hesaplanır:
    //
    //   Bakı — öğle güneşini gören yamaç karı eritir, çizgi orada yükselir. Kuzey
    //   yüzü aynı kotta karlı kalır. Liken de aynı güneşe göre yerleşiyor.
    //
    //   Oluklar — kar dere yataklarından dil gibi sarkar: gölge ve soğuk hava
    //   çukurda tutar. Konkavlık çizgiyi aşağı çeker.
    //
    //   Düzensizlik — kilometre ölçeğinde kaba gürültü; çizginin kontur olduğunu
    //   belli eden son ipucunu kırar.
    // ÜÇ TERİM DE SIFIR ORTALAMALI OLMALI. Ayarlanan kot "ortalama arazide çizginin
    // yeri" demek; terimlerden biri sistematik bir kayma taşırsa çizgi ayarlandığı
    // yerde değil, hep başka bir yerde oluşur.
    //
    // Bakı DÜZ ZEMİNE GÖRE. Ham `dot(normal, güneş)` düz zemine öğle güneşinin
    // yüksekliği kadar (burada 0.5'in üstü) pay veriyordu: hiçbir yüzey nötr değildi
    // ve çizgi her yerde ~100 metre yukarı kayıyordu. Farkı alınca düz zemin sıfır,
    // güneye bakan yamaç pozitif, kuzey yüzü NEGATİF olur — kuzey yüzünde kar daha
    // aşağı iner, gerçekte de öyle.
    // BAKI YAMAÇ YÜZÜ ÖLÇEĞİNDEN OKUNUYOR, karo normalinden DEĞİL.
    //
    // Ölçüldü: karo normali 14.65 m'de ortanca 4 derece değişiyor, bu `dot`'u ~0.17
    // kaydırıyor ve `_SnowlineSunLift` 200 m ile çarpılınca kar çizgisi 15 METREDE
    // 34 METRE oynuyor. Sonuç yumuşak bant değil, her kabartıyı takip eden keskin
    // kıvrım. Renk probu sınır boyunca kot rampasını suçladı ve tek titreyen girdi
    // buydu — `wobble` 625 m dalga boylu, `hollowness` pişmiş harita, ikisi yumuşak.
    //
    // Fizik: bakının kar çizgisine etkisi MEVSİMLİK IŞINIM üzerinden. O bir yamaç
    // yüzünün toplamı, tek karonun değil. Mip 4 = 2048/16 = 128 texel = 234 m/texel,
    // yani yamaç yüzü ölçeği.
    //
    // Gölgelendirme normali DEĞİŞMEDİ: o piksel başına kalmalı, kabartı ondan geliyor.
    float2 faceUv = (worldPos.xz - _TerrainOrigin.xz) / _TerrainSize.xz;
    float2 facePacked = SAMPLE_TEXTURE2D_LOD(_GroundNormals, sampler_GroundNormals,
                                             faceUv, 4).rg * 2.0 - 1.0;
    float3 faceNormal = float3(facePacked.x,
                               sqrt(saturate(1.0 - dot(facePacked, facePacked))),
                               facePacked.y);

    float flatSun = saturate(_SurfaceSunDir.y);
    float aspect = saturate(dot(faceNormal, _SurfaceSunDir.xyz)) - flatSun;

    // Konkavlık haritası 0-1 normalize (bkz. SurfaceMapBaker.Normalize), ortancası
    // 0.5. Ham haliyle çarpılınca sabit bir aşağı kayma taşıyordu.
    float hollowness = concavity - 0.5;

    float wobble = MountainFbm(worldPos * 0.0016, 2) - 0.5;

    float lineShift = aspect * _SnowlineSunLift
                    - hollowness * _SnowlineGullyDrop
                    + wobble * _SnowlineRagged * 2.0;

    // KAYMA BANDI SİLEMEZ. Üç terim de mutlak metre (toplamda ±470 m) ve kalıcı kar
    // çizgisinin 700 metrelik bandı için ayarlanmıştı. Yağış bandı ise dar — kar
    // sınırı fırtınada indiğinde 220 metreye kadar iniyor. Kayma bandın kendisinden
    // büyük olunca maskeyi tamamen kapatabiliyor: kar yağıyor, birikim doluyor, zemin
    // çıplak kalıyordu.
    //
    // Her çizgi kendi bandının yarısı kadar oynayabilir. Çizgi hâlâ düzensiz, ama
    // bandın dışına çıkamıyor.


    // Kalıcı kar çizgisi: bu kotun üstü yaz kış karlıdır ve havadan bağımsızdır.
    // Yüzeyin kendi rakımına bakar — havanın karlılığı oyuncunun bulunduğu kottan
    // geldiği için, ona bağlanınca zirvedeyken etek de karlı görünüyordu.
    float permanentShift = clamp(lineShift, -_PermanentSnowBand, _PermanentSnowBand);
    float permanent = smoothstep(_PermanentSnowLine - _PermanentSnowBand,
                                 _PermanentSnowLine + _PermanentSnowBand,
                                 altitude - permanentShift);

    // TAZE ÖRTÜNÜN TEK KAYNAĞI PROFİL.
    //
    // Burada bir dönem İKİNCİ BİR KAR ÇİZGİSİ vardı: profil ayrıca
    // `smoothstep(_SnowfallFloor, _SnowfallCeiling, altitude)` ile çarpılıyordu.
    // Aynı bilgi iki yerden geliyordu ve ikisi ayrışıyordu — profil "bu kotta kar
    // birikti" derken çarpan "bu kotta kar yağmaz" diyip siliyordu.
    //
    // Belirti: F1'den yağış 1 / kar 1 kilitlenip 206 metrede beklenince zemin çıplak
    // kalıyordu. Profil doluyordu (birikim `SnowfallRateAt` üzerinden kilidi okuyor),
    // ama çarpan kilidi GÖRMEYEN `_SnowfallFloor/_Ceiling` eşiğinden geliyordu.
    // Kullanıcı üç turda bildirdi; ilk iki şüphelim (birikim hızı, başlangıç durumu)
    // yanlıştı.
    //
    // Profil zaten kot ekseninde tutulan bir dizi: "hangi kotta ne kadar kar var".
    // Kar tutmasının AYRI bir yükseklik sınırı yok — sınır yağışın kendisinde.
    //
    // Yerel düzensizlik kayboluyor değil, YERİ DEĞİŞİYOR: kayma artık profilin
    // örneklendiği kota uygulanıyor. Güneşe bakan yüz profili yukarıdan, oluk
    // aşağıdan okuyor; kar sınırı yine dolaşarak düzensiz, ama tek kaynaktan.
    float snowBand = max(1.0, _SnowfallCeiling - _SnowfallFloor);
    float snowfallShift = clamp(lineShift, -snowBand * 0.5, snowBand * 0.5);

    float2 profile = SampleSnowProfile(altitude - snowfallShift);
    float fresh = profile.r;

    float supply = max(permanent, fresh);

    // KIRILMA GÜRÜLTÜSÜ ARAZİ ÖLÇEĞİNDE. 0.05 (20 m taban, 2 oktav) yazıyordu ve
    // ölçüldü: `cover`'ın ORTALAMASI yumuşak — bant probunda yedi bandın hepsi geniş
    // bir kuşak kaplıyor — ama YEREL VARYANS devasaydı. Bantların içi tuz-biberdi,
    // komşu iki piksel birkaç bant atlıyordu. Gözün "sert" dediği şey kenarın
    // genişliği değil DOKUSUYDU.
    //
    // Fizik: gerçek kar sınırının düzensizliği arazi ölçeğinde olur — oluk, sırt,
    // kaya çıkıntısı, onlarca-yüzlerce metre. Piksel ölçeğinde tuz-biber saçılmanın
    // karşılığı yok.
    //
    // 125 m taban, 4 oktav: sınır DOLAŞARAK düzensiz. İnce bileşen duruyor ama genliği
    // 1/8 — kenara serpilen cılız benekler yaşıyor, dantel gidiyor.
    float sprinkle = MountainFbm(worldPos * 0.008, 4);
    float edge = (sprinkle - 0.5) * _SnowBreakup * 0.6;
    float cover = saturate(supply * snowFit * shelter + edge);

    // Mikro yerleşim: kabartı gürültüsünün çukurunda örtü bir tık güçlenir, tepeciğinde
    // zayıflar — kar mikro çukura oturur, sırtın ucunu rüzgâr açar. Çarpan yumuşak ve
    // eşiksiz (0.85–1.15); arz sıfırsa sıfırda kalır. 0.375, iki oktavlı FBM'in ortası.
    cover *= 1.0 + (0.375 - micro) * 0.4;

    // BİRİKİNTİ ALANI. Derinlik şimdiye kadar yalnız kot bandı, eğim ve rüzgâr
    // maruziyetinden geliyordu — üçü de arazi ızgarasında (4.28 m) değişiyor, yani
    // dört metrenin altında derinlik DÜMDÜZ. Kar bu yüzden yakından boyanmış gibi
    // duruyordu: kalınlık var ama şekli yok.
    //
    // Alan rüzgâr eksenine hizalı: yığınlar rüzgâr boyunca uzar, ona dik daralır.
    // 0.5 nötr — altı kazınmış, üstü yığılmış.
    float2 windAxis = normalize(_SurfaceWindDir.xz + float2(0.0001, 0.0));
    float drift = SnowDriftShape(worldPos.xz, windAxis);

    // Birikinti kenarı ÖRTÜYÜ de ısırıyor: kazınmış şeritte kar incelmekle kalmaz,
    // yer yer delinip altındaki taşı gösterir.
    //
    // EŞİKTEN ÖNCE. Eşikten sonra çarpıldığında doymuş örtü (kalıcı kar çizgisinin
    // üstünde cover = 1) doğrudan 0.89'a düşüyordu ve `lerp(kaya, kar, 0.89)` bütün
    // yamaca yüzde on bir kaya karıştırıyordu: rüzgâr ekseninde uzamış, kahverengi-siyah,
    // yarı saydam şeritler. Kar bol olduğu yerde birikinti örtüyü DELMEZ, yalnız
    // inceltir; delinme örtünün zaten cılız olduğu kenarda olur. Eşiğin girdisine
    // uygulanınca bol arz yeniden 1'e doyuyor, cılız kenar ise gerçekten deliniyor.
    cover *= lerp(1.0, lerp(0.75, 1.1, drift), _SnowDriftCoverBite);

    // EŞİK ÇOK DÜŞÜK OLMALI. 0.16-0.42'ydi ve ölçüldü: tam beyaz olmak için mevsimlik
    // deponun %42'si gerekiyordu, %20'nin altında ise hiçbir şey görünmüyordu. Oysa iki
    // santim taze kar zemini beyaza çevirir — albedo neredeyse anında doyar, KALINLIK
    // yavaş gelir. İkisi ayrı hızda ve ayrı kanalda (`cover` ile `burial`).
    //
    // ÜST EŞİK 0.18'DEN 0.45'E AÇILDI. Kusur genişlik değil BENEKTİ ve ölçüldü:
    //
    //   geçiş penceresi        0.15
    //   kırılma gürültüsü      ±0.15   (`_SnowBreakup` 0.5 × 0.6)
    //   oran                   1.00
    //
    // Gürültü pencerenin TAMAMINI süpürünce sonuç ikili oluyor — ya tam kar ya tam
    // kaya, arada yumuşama yok. Ekranda kenar dantel gibi çıkıyordu. 0.42'lik pencerede
    // aynı gürültü üçte bire iniyor: kenar düzensiz kalıyor ama yumuşuyor.
    //
    // `snowBreakup`'ı kısmak da benek sorununu çözerdi ama serpintiyi öldürürdü; o bir
    // kez denenip geri alınmıştı. Pencereyi açmak ikisini birden koruyor.
    //
    // Alt eşik 0.03'te DURUYOR: "iki santim taze kar zemini beyaza çevirir" kuralı
    // derinlik için doğru ve bozulmuyor. Değişen yalnız doymanın hızı.
    //
    // Bedeli ölçüldü: kalıcı çizgi ±350 m'lik rampadan geliyor, görünür geçiş
    // 106 m → 226 m'ye çıkıyor. Gerçek kar sınırı da 100-300 m arasında geçer.
    cover = smoothstep(0.03, 0.45, cover);

    // Konkavlık burada büyütülmüyor. Harita akış birikiminden türüyor ve ızgaraya
    // hizalı bir gürültü taşıyor (bkz. DECISIONS.md). Katkısı büyütülünce o gürültü
    // kalınlığa, oradan kabartıya geçiyor ve yamaçta dişli, düzenli bir desen
    // bırakıyordu.
    // Rüzgâr yüzü kalınlıkta YUMUŞATILARAK uygulanır: aynı çarpan örtüde zaten var,
    // ham haliyle ikinci kez çarpılınca rüzgâra bakan yamaçta pay 0.55'ten 0.30'a
    // düşüyor ve kar hep ince kalıyordu. Süpürme gerçek, ama iki kez sayılmamalı.
    float pile = slope * shelter;

    // Kalınlığın arzı ayrı depodan: örtü hızlı kapanır, kalınlık arkadan gelir.
    // Erirken ters — depo örtüden hızlı boşalır: kar önce incelir, sonra delinir,
    // en son çıplak kalır. Kalıcı çizginin üstünde kalınlık havadan bağımsız tam.
    // KALINLIK TOPLANIR, seçilmez. `max()` ile kalıcı kar payı 1'e vardığı anda taze
    // karın kalınlığa katkısı yok oluyordu: fırtınadan önce ve sonra yüzey birebir
    // aynıydı, birikme gözle görülmüyordu. Toplandığında kalıcı kar tek başına
    // kayaları yarı gömülü bırakıyor, üstüne taze kar gelince gömülüyorlar — çıkıntının
    // kaybolması birikmenin en okunaklı işareti.
    // `snowfall` çarpanı KALKTI: taze örtüyle aynı sebep, profil zaten kot ekseninde
    // ve ikinci bir kar çizgisi onu siliyordu. Profil de üstteki `snowfallShift` ile
    // örneklendiği için düzensizlik kalınlıkta da duruyor.
    float packSupply = saturate(permanent * 0.7 + profile.g);

    // Derinliğe ÇARPAN olarak giriyor, toplanmıyor: karın olmadığı yerde birikinti
    // de olmaz. Kazınan yerde yarıya iner, yığılan yerde bir buçuk katına çıkar.
    float driftDepth = lerp(1.0, lerp(0.45, 1.55, drift), _SnowDriftStrength);

    SnowCoverage snow;
    snow.cover = cover;
    snow.shelter = shelter;

    // İKİ AYRI KALINLIK. Birikinti alanı kabartıyı şekillendirir ama "taş görünüyor mu"
    // sorusuna karışamaz: 60 cm kar da 90 cm kar da altındaki taşı TAMAMEN gizler,
    // gömülme doyar. Tek kanal olduğunda birikinti çukurunda `buried` 1'in altına
    // düşüyor ve `powder` üzerinden kayanın rengi karın içinden geri geliyordu —
    // rüzgâr ekseninde uzamış, gri, yarı saydam şeritler. Ölçüldü: F1 teşhis
    // panelinde birikintiyi kapatmak izleri tek başına siliyor.
    snow.burial = snow.cover * saturate(packSupply * pile * _SnowDepthScale);
    snow.depth = snow.cover * saturate(packSupply * pile * _SnowDepthScale * driftDepth);
    snow.patch = sprinkle;
    cover = snow.cover;   // aşağıdaki tazelik hesabı örtüyü buradan okuyor

    // TAZELİK, örtüden ayrı bir sinyal. Kalıcı kar çizgisinin üstünde örtü zaten 1;
    // orada yeni yağan karın kapsamaya ekleyeceği bir şey yok, o yüzden `max()` taze
    // karı yutuyordu ve fırtınadan sonra yüzey hiç değişmiyordu. Oysa gerçekte fark
    // kapsamada değil YÜZEYDE: taze toz mat, pütürsüz ve parıltılı; yıllanmış névé
    // camsı ve rüzgârla oyulmuş.
    snow.fresh = saturate(fresh) * cover;
    return snow;
}

/// KAR MİKRO DETAYI: iki yüzey durumunun tazelik oranına göre karışımı.
/// Okuma, harman ve UV kurulumu ortak modülde (`SurfaceDetail.hlsl`); burada yalnız
/// KARA ÖZGÜ kararlar var — hangi iki yüzey, hangi eksende hizalı, hangi oranla.
float3 SnowMicroNormal(float3 worldPos, float3 normalWS, float fresh, out float roughness)
{
    float2 uv = SurfacePlanarUV(worldPos, normalWS, _SnowDetailScale);

    // Türevler bir kez hesaplanıp elle geçiriliyor: stokastik okuma her örneği
    // farklı kaymadan alıyor, donanımın türevi hücre sınırında sıçrardı.
    float2 ddxUV = ddx(uv);
    float2 ddyUV = ddy(uv);

    SurfaceDetail powder;
    SAMPLE_SURFACE_DETAIL(SnowPowder, sampler_SnowPowderNormal, uv, ddxUV, ddyUV, powder)

    // SASTRUGİ RÜZGÂRI İZLER. Sıkışmış kar dokusu yönlü (ölçüldü: 0.78 anizotropi,
    // toz 1.21) ama yönü dokuda sabit. Prosedürel sastrugi tarağı zaten rüzgâr
    // eksenine hizalı; doku hizalanmazsa iki desen çapraz durur ve yüzey karışır.
    float2 windUV = uv, windDdx = ddxUV, windDdy = ddyUV;
    SurfaceAlignUV(normalize(_SurfaceWindDir.xz + float2(0.0001, 0.0)),
                   windUV, windDdx, windDdy);

    SurfaceDetail packed;
    SAMPLE_SURFACE_DETAIL(SnowPacked, sampler_SnowPowderNormal, windUV, windDdx, windDdy, packed)

    // Karışım YÜKSEKLİĞE göre: taze kar önce çukurları doldurur, sert kabuk
    // tümseklerde açıkta kalır. Doğrusal karışım ikisini her yerde bulanıklaştırıyordu.
    SurfaceDetail snow = BlendSurfaceDetail(packed, powder, fresh, 0.12);

    roughness = snow.roughness;
    return snow.normal;
}

MountainSurface BuildMountainSurface(float3 worldPos)
{
    float2 uv = (worldPos.xz - _TerrainOrigin.xz) / _TerrainSize.xz;
    float4 maps = SampleSurfaceMaps(uv);

    // Zemin normali köşe normalinden değil, pişirilmiş dokudan
    float2 packed = SAMPLE_TEXTURE2D(_GroundNormals, sampler_GroundNormals, uv).rg * 2.0 - 1.0;
    float3 normalWS = float3(packed.x,
                             sqrt(saturate(1.0 - dot(packed, packed))), packed.y);

    float deposition = maps.r;
    float concavity  = maps.g;
    float exposure   = maps.b;

    // Eğim, köşe normalinden değil pişirilmiş haritadan. Köşe normalleri 4 metrelik
    // ızgarada yaşıyor ve aralarının doldurulması, eş değer çizgileri paylaşılan
    // köşegenler boyunca kıvrılan bir alan üretiyor. Işıkta görünmüyor — eşik görünür
    // kılıyor: yamaç eğim sınırının bandındayken kar ve çakıl maskeleri o kafes boyunca
    // açılıp kapanıyor ve yüzeye baklava deseni çıkıyordu. Haritanın texel'i ızgaranın
    // dört katı geniş, desen taşıyamaz.
    float slope = maps.a;

    float altitude = worldPos.y - _TerrainOrigin.y;
    float grain = MountainFbm(worldPos * _GrainScale, 3);

    // --- Kaya: iki ton, jeolojik bantlarla harmanlanıyor ---
    float band = MountainBand(worldPos);
    float rockMix = saturate(band * _BandContrast + (grain - 0.5) * _GrainStrength);
    float3 albedo = lerp(_RockPrimary.rgb, _RockSecondary.rgb, rockMix);

    // --- Rakım tonu: altta toprak, üstte buzul aşındırması ---
    float lowland = 1.0 - smoothstep(0.0, _LowlandCeiling, altitude);
    float alpine = smoothstep(_AlpineFloor, _AlpineFloor + 1200.0, altitude);
    albedo = lerp(albedo, _LowlandTint.rgb, lowland * _AltitudeTintStrength);
    albedo = lerp(albedo, _AlpineTint.rgb, alpine * _AltitudeTintStrength);

    // --- Oksit: demir damarları katmanları izler, serbest gezmez ---
    float vein = MountainFbm(worldPos * _OxideScale, 2);
    float oxide = smoothstep(0.55, 0.85, vein) * (1.0 - band) * _OxideAmount;
    albedo = lerp(albedo, _OxideColor.rgb, saturate(oxide));

    // --- Liken: nem oyuklarda tutunur, güneş kurutur, rakım sınırlar ---
    float moisture = smoothstep(_LichenMoistureBias, 1.0, concavity);
    float shelter = 1.0 - exposure;
    // Öğle güneşi kullanılıyor: anlık güneşe bağlanırsa liken gün içinde yanıp söner
    float sunFacing = saturate(dot(normalWS, _SurfaceSunDir.xyz) * 0.5 + 0.5);
    float dried = lerp(1.0, 1.0 - sunFacing, _LichenSunSensitivity);
    float alive = 1.0 - smoothstep(_LichenCeiling - 600.0, _LichenCeiling, altitude);
    float patchy = smoothstep(0.35, 0.75, MountainFbm(worldPos * 0.02, 2));

    float lichen = saturate((moisture * 0.6 + shelter * 0.4) * dried * alive * patchy) * _LichenAmount;
    albedo = lerp(albedo, _LichenColor.rgb, lichen);

    // --- Çakıl: yukarıdan akan malzeme oluklarda toplanır, dikte tutunmaz ---
    float screeFit = smoothstep(cos(radians(_ScreeSlopeLimit)) - 0.12,
                                cos(radians(_ScreeSlopeLimit)) + 0.08, slope);
    float scree = smoothstep(_ScreeRange.x, _ScreeRange.y, deposition) * screeFit * _ScreeAmount;
    albedo = lerp(albedo, _ScreeColor.rgb * (0.8 + grain * 0.4), scree);

    // --- Islaklık: yağış kayayı koyultur ve parlatır ---
    float wet = _SurfaceWetness * (1.0 - exposure * 0.3);
    albedo *= 1.0 - wet * _WetDarkening;

    // --- Kabartı gürültüsü: prosedürel normalin hammaddesi; bu olmadan yüzey plastik
    //     görünür. Değeri karın mikro yerleşimine, gradyanı gölgelendirmeye gider —
    //     iki tüketici, tek örnekleme ---
    // Eğim ANALİTİK: sonlu fark üç örnekleme istiyordu (8 hash × 3), türev tek
    // örneklemenin içinden çıkıyor. bx = ∂f/∂x · e, çünkü sonlu fark birinci
    // mertebeden bunun aynısı.
    float e = 0.35;
    float3 bumpGrad;
    float here = MountainFbmD(worldPos * _BumpScale, 2, bumpGrad);
    float bx = bumpGrad.x * _BumpScale * e;
    float bz = bumpGrad.z * _BumpScale * e;

    // --- Kar: eğim, oyuk, rüzgâr yönü ve hava durumu ---
    SnowCoverage snow = BuildSnowCoverage(worldPos, normalWS, altitude,
                                          slope, concavity, here);

    // Toz kar bandı: cılız örtü kayayı beyaza boyamaz, rengini soldurur — taşın tonu
    // serpintinin altından okunur. Saf kar rengine ancak kalınlıkla ulaşılır.
    // Eğim yumuşak: 2.5 çarpanı geçişi sıkıştırıyor ve kalınlığın rüzgâraltı
    // sınırlarında hızlı değiştiği yerlerde (sırt aşımı) iki ton arasında keskin
    // bir bant çiziyordu — gerçek kar tonu metrelerce yayılarak değişir.
    // İKİ AYRI KAVRAM, tek değişkende toplanmışlardı:
    //   `buried`  — kaya ne kadar gömüldü. Yalnız kalınlıktan gelir. RENGİ bu sürer.
    //   `aged`    — kar ne kadar yıllandı. Taze pay bunu geri çeker. PARLAKLIĞI bu sürer.
    // Tek değişkenken taze karın albedosu da düşüyordu; oysa taze toz karın EN parlak
    // hâlidir, en mat olanı da odur. Yanlış olan parlaklık değil, ikisinin bağlanmasıydı.
    float buried = saturate(snow.burial * 1.7);
    float aged = buried * (1.0 - snow.fresh * 0.6);
    half3 powder = lerp(albedo, _SnowColor.rgb, 0.6);
    albedo = lerp(albedo, lerp(powder, _SnowColor.rgb, buried), snow.cover);

    // ---- TEŞHİS ----
    //
    // "Kar tutmuyor" belirtisi dört tur sürdü. CPU zinciri baştan sona ÖLÇÜLDÜ ve
    // dolu çıktı (örtü 0.955), yani hesap beyaz veriyor ama ekran vermiyor. Kod ile
    // ekran çeliştiğinde ölçüm haklıdır: değer doğrudan ekrana basılıyor.
    //
    // Renk TON tabanlı, parlaklık tabanlı değil — ışık, pozlama ve tonemap parlaklığı
    // ezer ama tonu bırakır.
    //
    //   1 ÖRTÜ    kırmızı = 0, mavi = ara, yeşil = 1
    //   2 ARZ     aynı bantlar, `supply` (profil × çarpanlar öncesi)
    //   3 GÖMÜLME aynı bantlar, `burial`
    if (_SnowDebug > 0.5)
    {
        // 1-3: gölgelendirme payları, 0-1 aralığında.
        // 4-5: GEOMETRİK derinlik, METRE. Bantlar farklı çünkü büyüklük farklı —
        //      kırmızı 0.2 m altı (yok sayılır), mavi 0.2-1 m, yeşil 1 m üstü.
        //
        //   4 MAKRO DERİNLİK  `SnowMacroDepth` — karın hesaplanan kalınlığı
        //   5 YER DEĞİŞTİRME  `SnowDisplacement` — köşeye gerçekten uygulanan
        //
        // 4 yeşil ama 5 kırmızıysa kalınlık hesaplanıyor ama geometriye ulaşmıyor:
        // suçlu mesafe sönümü ya da eşik.
        half3 color;
        if (_SnowDebug < 3.5)
        {
            float probe = _SnowDebug < 1.5 ? snow.cover
                        : _SnowDebug < 2.5 ? snow.fresh
                                           : snow.burial;

            color = probe < 0.05 ? half3(1.0, 0.0, 0.0)
                  : probe > 0.95 ? half3(0.0, 1.0, 0.0)
                                 : half3(0.0, 0.3, 1.0);
        }
        else
        {
            float metres = _SnowDebug < 4.5 ? SnowMacroDepth(worldPos)
                                            : SnowDisplacement(worldPos);

            color = metres < 0.2 ? half3(1.0, 0.0, 0.0)
                  : metres > 1.0 ? half3(0.0, 1.0, 0.0)
                                 : half3(0.0, 0.3, 1.0);
        }

        albedo = color;
    }

    // Kabartıyı gömen şey kapsama değil kalınlık: bir parmak kar altındaki taşı
    // gösterir, yarım metre kar göstermez.
    float rockRelief = _BumpStrength * (1.0 - snow.burial * _SnowBurial) * (1.0 + wet * 0.3);

    // Gömülen taş dokusunun yerine karın kendi dokusu gelir. Gömme tek başına
    // çalışınca kalın kar düz plastiğe dönüyor ve sastrugi'nin taradığı gradyan da
    // onunla birlikte siliniyordu — kalınlık arttıkça kar yüzeyi kendi kabartısını
    // kazanır.
    float snowRelief = _BumpStrength * 0.45 * snow.depth;

    float2 gradient = float2(-bx, -bz);

    // Sastrugi: rüzgâr kar yüzeyini kendi yönünde oyar ve biriktirir, geriye o yöne
    // uzanan sırtlar bırakır. Aynı gürültünün rüzgâr yönündeki bileşeni kısılıp yanal
    // bileşeni güçlendirilince desen o yönde uzuyor — yüzey taranmış gibi çizgileniyor.
    //
    // Yeni bir gürültü örneği alınmıyor. Arazi fragmanında her ek örnek kare hızından
    // ölçülebilir pay götürüyor (bkz. DECISIONS.md); eldeki eğimi yeniden şekillendirmek
    // aynı görüntüyü bedelsiz veriyor.
    //
    // Tarama gücü serpinti gürültüsüyle yamalanır: gerçek sastrugi tekdüze tarak izi
    // değil, sırt yamalarıdır. Gürültü karın kenar serpintisiyle paylaşılıyor.
    //
    // Rüzgârın oyduğu yüzey RÜZGÂRIN HIZLI ESTİĞİ yüzeydir. Eskiden gökyüzü açıklığı
    // kanalı okunuyordu (liken için pişmiş, bir doku okuması kazanıyordu) ama o kanal
    // yönsüz: bir çukurun tabanı göğü görmez ama rüzgâr da almaz, oysa göğü gören bir
    // rüzgâraltı terası taranmaz. Birikim ağırlığı zaten aynı fonksiyonda örneklendi;
    // ödünç kanalın gerekçesi kalmadı.
    //
    // Ağırlık 0.67 (rüzgâr hızlı, kar kazınır) → 2.0 (yavaş, kar yığılır). Tarak
    // hızlının tarafında güçlü.
    float2 windAxis = normalize(_SurfaceWindDir.xz + 0.0001);
    float2 sideAxis = float2(-windAxis.y, windAxis.x);
    // Taze kar sastrugiyi SÖNDÜRÜR: tarak rüzgârın günler süren işidir, yeni yağan
    // toz onu örter. Fırtına dinip kar durduğunda tarak yeniden ortaya çıkar.
    float comb = saturate(_Sastrugi * _SurfaceWindDir.w * snow.depth
                          * (0.4 + snow.patch * 1.6)
                          * lerp(0.35, 1.3, saturate((2.0 - snow.shelter) / 1.33)))
               * (1.0 - snow.fresh * 0.8);

    float2 combed = windAxis * dot(gradient, windAxis) * (1.0 - comb * 0.75)
                  + sideAxis * dot(gradient, sideAxis) * (1.0 + comb * 1.5);

    // Kayada ham gradyan taş genliğiyle, karda taranmış gradyan kar genliğiyle.
    // İnce kar ikisini üst üste gösterir: taş az gömülü, kar dokusu henüz cılız.
    float2 shaped = gradient * rockRelief + combed * (snowRelief * snow.cover);

    // Pütür: kar dümdüz yağmaz — çökme, rüzgâr ve kabuklanma desimetre ölçeğinde
    // höyükler bırakır. Taş kabartısının dalga boyu (~3 m) bunun için fazla iri;
    // tek başına kalınca örtü "kağıt gibi eşit yağmış" okunuyordu. İnce oktav
    // yalnız karlı piksellerde örneklenir: dallanma mekânsal olarak tutarlı,
    // çıplak arazide maliyeti yok.
    if (snow.cover > 0.05)
    {
        float lumpScale = _BumpScale * 3.0;
        float le = 0.12;
        float3 lumpGrad;
        float lump = MountainFbmD(worldPos * lumpScale, 2, lumpGrad);
        float lumpX = lumpGrad.x * lumpScale * le;
        float lumpZ = lumpGrad.z * lumpScale * le;

        shaped += float2(-lumpX, -lumpZ)
                * (_BumpStrength * 0.6 * snow.depth * snow.cover);
    }

    // KAR MİKRO DETAYI. Prosedürel kabartı METRE ölçeğinde (sastrugi, tümsek);
    // doku SANTİMETRE ölçeğini dolduruyor. Yalnız yakında: uzakta texel piksel
    // altına düşer ve kaynar.
    float snowDetailRough = 0.0;
    if (snow.cover > 0.05 && _SnowDetailStrength > 0.001)
    {
        float toCamera = distance(worldPos, _WorldSpaceCameraPos);
        float visible = 1.0 - smoothstep(_SnowDetailFade * 0.4, _SnowDetailFade, toCamera);

        if (visible > 0.001)
        {
            float3 micro = SnowMicroNormal(worldPos, normalWS, snow.fresh, snowDetailRough);

            // Teğet uzayı eğimi dünya eğimine ekleniyor. Kar örtüsü ve derinliğiyle
            // ölçekli: çıplak kayada kar kabartısı olmaz, ince örtüde zayıf kalır.
            shaped += micro.xy * (_SnowDetailStrength * visible
                                  * snow.cover * saturate(0.35 + snow.depth));

            snowDetailRough *= visible * snow.cover;
        }
    }

    // Kalın kar çukuru doldurur, sırtı körleştirir: gölgelendirme normali yukarı döner.
    // Fazlası dağın hacmini alıyor, o yüzden ayar dar bir aralıkta tutuluyor.
    float3 shaded = normalize(normalWS + float3(shaped.x, 0.0, shaped.y));

    MountainSurface surface;
    surface.albedo = albedo;
    surface.emission = Alpenglow(worldPos, normalWS, altitude, albedo, exposure)
                     + SnowSparkle(worldPos, normalWS, snow.cover);
    // Taze kar küçük kabartıyı örter: yeni yağmış örtü altındaki dokuyu yumuşatır,
    // yıllanmış kar rüzgârla oyulup sertleştiği için altındaki biçimi geri verir.
    surface.normalWS = normalize(lerp(shaded, float3(0.0, 1.0, 0.0),
        snow.depth * _SnowRounding * (1.0 + snow.fresh * 0.5)));

    // İnce toz mat, yerleşmiş kalın örtü kar parlaklığına ulaşır; rüzgâr taraması
    // yüzeyi bir tık daha cilalar — sertleşmiş sastrugi karı taze tozdan parlaktır.
    half snowGloss = saturate(_SnowSmoothness * (0.55 + 0.45 * aged) + comb * 0.08);

    // GECE MATLAŞIR. Güneş battığında yönlü ışık AYA çevriliyor; kar pürüzsüzlüğü
    // gündüz değerinde kalınca ay ışığı dar speküler lobla çakıyor ve yüzey normali
    // kar tümsekleri/sastrugi tarağıyla dalgalı olduğu için kamera oynadıkça yanıp
    // sönüyor — gece boyunca süren sahte bir pırıltı. Şiddet düşünce diffuse zemine
    // gömülüyor ama dar lob tonemap'ten sağ çıkıyor: oran değişmiyor, görünürlük
    // değişiyor. Gerçekte ay ışığında kar hafif bir cila verir, ÇAKIM vermez —
    // çakım güneşin işi (kristal pırıltısı zaten ayrıca güneşe kapılı).
    // Kaynak _SurfaceDawnDir.y: güneşin yüksekliği, ayrı bir zamanlayıcı yok.
    snowGloss *= lerp(0.35, 1.0, smoothstep(-0.06, 0.10, _SunHeight));

    // Dokunun pürüzlülüğü parlaklığa YAMA olarak biniyor, yerine geçmiyor: cila
    // hâlâ kar sisteminin (tazelik, ıslaklık, gece, sastrugi) kararı. Doku yalnız
    // yüzeyin kendi düzensizliğini ekliyor — cilalı kabuk parlar, toz mat kalır.
    snowGloss = saturate(snowGloss * lerp(1.0, 1.35 - snowDetailRough, _SnowDetailRough));
    surface.smoothness = lerp(lerp(_RockSmoothness, _WetSmoothness, wet), snowGloss, snow.cover);

    // Maruziyet haritası ambient'i kısar: vadi dibi göğün küçük bir parçasını görür.
    // Yüz metre ölçeğinde çalışır; santimetre ölçeğinin sahibi SSAO idi ama kapatıldı —
    // derinlik tamponundan arazi üçgenlerinin kırıklarını gölgeliyordu (bkz. DECISIONS).
    //
    // O ölçeğin telafisi mikro-oyuk — kabartının DEĞERİNDEN değil EĞRİLİĞİNDEN:
    // değeri karartmak gürültünün alçak bölgelerini metre ölçekli kirli yamalar
    // hâlinde boyuyordu (denendi, geri alındı). Çukurun tanımı ikinci türevdir:
    // çevresi kendinden yüksek nokta dip'tir, yalnız orası loşlaşır. İki ek örnek
    // yalnız yakın alanda alınır — SSAO'nun kapsadığı ölçek de zaten buydu, uzak
    // dağ dokunulmaz. Kar kalınlaştıkça kabartıyla birlikte gömülür.
    float microCavity = 1.0;
    float cavityDip = 0.0;
    float cavityRange = 1.0 - smoothstep(20.0, 50.0, length(_WorldSpaceCameraPos - worldPos));
    if (cavityRange > 0.0)
    {
        float bx2 = MountainFbm((worldPos - float3(e, 0, 0)) * _BumpScale, 2) - here;
        float bz2 = MountainFbm((worldPos - float3(0, 0, e)) * _BumpScale, 2) - here;

        // Laplasyen: (f(+e)+f(-e)-2f) iki eksenin toplamı; pozitif = çukur
        float coarseDip = saturate((bx + bx2 + bz + bz2) * 6.0);

        // İnce çatlak oktavı: kabartının ~3 metrelik çukurlarının altına ~1 metrelik
        // yarık ölçeği. Kayaya asıl "çatlak dibi" hissini bu verir; yalnız yakın
        // alanda örneklendiği için uzak dağa maliyeti yok.
        float fineScale = _BumpScale * 3.0;
        const float fe = 0.12;
        float fineLap = MountainFbm((worldPos + float3(fe, 0, 0)) * fineScale, 2)
                      + MountainFbm((worldPos - float3(fe, 0, 0)) * fineScale, 2)
                      + MountainFbm((worldPos + float3(0, 0, fe)) * fineScale, 2)
                      + MountainFbm((worldPos - float3(0, 0, fe)) * fineScale, 2)
                      - 4.0 * MountainFbm(worldPos * fineScale, 2);
        float fineDip = saturate(fineLap * 9.0);

        cavityDip = saturate(coarseDip * 0.28 + fineDip * 0.18) * cavityRange
                  * (1.0 - snow.burial * _SnowBurial);
        microCavity = 1.0 - cavityDip;
    }
    //
    // Ama kar çukuru doldurur. Dolan bir oyuk artık göğün küçük bir parçasını görmüyor;
    // yüzeyi düzleşmiş, göğe açılmıştır. Kısıntı kalınlıkla geri açılıyor — kalınlığın
    // en güçlü ipucu bu: kabartının gömülmesi ince kalıyor, kapanan gölge ise okunuyor.
    surface.occlusion = lerp(lerp(1.0, exposure, _CavityStrength), 1.0, snow.burial)
                      * microCavity;

    // Diplere toz ve kırıntı birikir: çukur yalnız loş değil, MAT da. Aynı dip
    // değeri pürüzlülüğe çevrilir — bedava. Kar kendi parlaklığını korur.
    surface.smoothness *= 1.0 - cavityDip * 1.2 * (1.0 - snow.cover);

    return surface;
}

#endif
