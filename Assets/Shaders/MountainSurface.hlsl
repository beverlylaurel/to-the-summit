#ifndef MOUNTAIN_SURFACE_INCLUDED
#define MOUNTAIN_SURFACE_INCLUDED

// Dağ yüzeyinin tarifi. Yalnızca "burada ne var" sorusunu cevaplar; ışık hesabı
// UniversalFragmentPBR'a ait. Gölge, derinlik ve normal geçişleri de URP'nin kendi
// dosyalarından geliyor — kafa lambası gibi ek ışıklar bedava çalışsın diye.

#include "MountainSurfaceInput.hlsl"

// Dünya koordinatı binlerce metreye çıkıyor. sin tabanlı hash o ölçekte float
// hassasiyetini tüketip piksel piksel gürültüye dönüşür; hücre indeksi önce küçük
// bir periyoda katlanır, tekrar kilometrelerce ötede kalır.
float MountainHash(float3 p)
{
    // Tohum burada uygulanıyor, çağrı yerlerinde değil.
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

// KAR DURUMU BURADAN OKUNUYOR. Dağın karı ile kar mesh'inin karı AYNI
// zincirden geliyor (yakın bölge → uzak kaskad → kar çizgisi); ayrı bir
// "arazi karı" sayısı yok, o yüzden sınırda çelişemezler.
//
// Kar sistemi sahnede yoksa bu globaller sıfır kalıyor ve katman
// kendiliğinden kapanıyor — ek bir anahtar gerekmiyor.
#include "../Snow/Shaders/SnowCommon.hlsl"

// Detay normalleri spec §14.2'nin kendi tablosundan. Dağ tarafında yalnız
// MACRO katmanı (8 m tile, rüzgâr dalgaları) açılıyor: kalite keyword'leri
// burada tanımlı değil, o yüzden Meso/Micro blokları derlenmiyor.
//
// Bu bir kısıtlama değil, doğru yer: Meso 0.6 m ve Micro 0.05 m yakın alan
// detayı ve o alanı zaten kar mesh'i (clipmap, 128 m) tam katmanla çiziyor.
// Dağ katmanı 128 m'nin ötesinde başlıyor, orada ikisi de alt piksel.
//
// Macro'nun tile'ı 8 m — arazi heightmap'inin tekseli 7.32 m. Düz beyaz karın
// teşhir ettiği o basamak ölçeğini kıran katman tam bu.
#include "../Snow/Shaders/SnowDetailNormals.hlsl"
#include "../Snow/Shaders/SnowCover.hlsl"
#include "../Snow/Shaders/SnowSparkle.hlsl"
#include "../Snow/Shaders/SnowLighting.hlsl"
#include "../Snow/Shaders/SnowRelief.hlsl"


struct MountainSurface
{
    half3 albedo;
    half3 emission;
    half  smoothness;
    half  occlusion;
    float3 normalWS;

    /// Karın bu noktadaki payı. Parıltı bununla ağırlıklanıyor: kar mesh'i
    /// parıldayıp arazi parıldamayınca sınır çizgi hâlinde görünüyordu.
    half  snowMask;

    /// Karın YEREL durumu, ışıklandırma bloğuna taşınıyor. İzin içi sıkışmış
    /// kar; ışık da o yoğunluğu görmek zorunda, yoksa yüzey ezilmiş görünüp
    /// ışığı bakir kar gibi yansıtır.
    half snowRhoN;
    half snowWet;
    half snowDisturb;

    /// İzin o pikseldeki derinliği (m). Işıklandırma bloğu çukurun kendi
    /// gölgesini bununla hesaplıyor.
    half snowDentDepth;

    /// Yüzey dokusunun harmanı, BİR KEZ okunmuş hâliyle taşınıyor.
    ///
    /// Işıklandırma bloğu da aynı harmanı istiyor; orada yeniden örneklenseydi
    /// aynı piksel için on iki doku erişimi iki katına çıkardı. Kar mesh'i ile
    /// arazinin AYNI dokuyu görmesi zorunlu: mesh yalnız yerel sapmayı çiziyor,
    /// düz alanı arazi çiziyor, iki yüzey farklı doku görürse sınır ayrışır.
    SnowSurfaceBlend snowBlend;
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
    // UFUK ALTINDA HEMEN BIRAKMIYOR — ALPENGLOW İÇİN.
    //
    // Eskiden `sunDir.y < 0.0` ile kesiliyordu: güneş ufka değdiği AN arazi
    // kendi gölgesini tamamen kapatıyor, gün batımından sonraki alpenglow
    // boyunca vadi dipleri ve sırt arkaları sırtlarla aynı parlaklıkta
    // kalıyordu — derinlik hissi bir karede kayboluyor.
    //
    // Işığın kendi yönü zaten söndürüyor; buradaki kapının işi yalnız ufuk
    // haritasının anlamsız açı okumasını engellemek. O yüzden sınır ufkun
    // biraz ALTINA çekildi ve arada gölge yumuşakça bırakılıyor.
    if (sunDir.y < -0.035) return 1.0;

    // −0.035 ile 0 arasında gölge sönerek çekiliyor: sert bir açma/kapama
    // gün batımında görünür bir sıçrama yapardı.
    float horizonFade = saturate(sunDir.y / 0.035 + 1.0);

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
    return lerp(1.0, smoothstep(horizon - 0.02, horizon + 0.10, elevation), horizonFade);
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

    float rockRelief = _BumpStrength * (1.0 + wet * 0.3);

    float2 gradient = float2(-bx, -bz);

    float2 shaped = gradient * rockRelief;

    float3 shaded = normalize(normalWS + float3(shaped.x, 0.0, shaped.y));

    MountainSurface surface;
    surface.snowMask = 0;
    surface.snowBlend.albedoTint  = half3(1, 1, 1);
    surface.snowBlend.roughAdd    = 0;
    surface.snowBlend.normalSlope = half2(0, 0);
    surface.snowRhoN    = (half)_FallbackRhoN;
    surface.snowWet     = (half)_SurfaceWetness;
    surface.snowDisturb = 0;
    surface.snowDentDepth = 0;
    surface.albedo = albedo;
    surface.emission = Alpenglow(worldPos, normalWS, altitude, albedo, exposure);
    surface.normalWS = shaded;
    surface.smoothness = lerp(_RockSmoothness, _WetSmoothness, wet);

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

        cavityDip = saturate(coarseDip * 0.28 + fineDip * 0.18) * cavityRange;
        microCavity = 1.0 - cavityDip;
    }

    surface.occlusion = lerp(1.0, exposure, _CavityStrength) * microCavity;

    // Diplere toz ve kırıntı birikir: çukur yalnız loş değil, MAT da. Aynı dip
    // değeri pürüzlülüğe çevrilir — bedava.
    surface.smoothness *= 1.0 - cavityDip * 1.2;

    // ------------------------------------------------------------------ kar
    //
    // YERİNDEN OYNATMA YOK, bu bir gölgeleme katmanı. Deforme olan gerçek kar
    // yalnız oyuncunun çevresindeki 24 m'lik bölgede (kar mesh'i).
    //
    // ARAZİNİN KARI DERİNLİK DEĞİL ÖRTÜ (spec §16).
    //
    // Eskiden `SnowStateAt`'ten DERİNLİK okunuyordu. O fonksiyon bölgenin
    // içinde durum dokusunu, dışında `_FallbackSWE`'yi veriyor — iki ayrı
    // sayı. Kar mesh'i ise kenarında kalınlığı sıfıra indiriyor (spec §8.3).
    // İkisi üst üste gelince mesh'in dış 2 metresinde HENDEK kalıyordu:
    // mesh 0 cm gösterirken arazi 45 cm boyuyordu. Oyuncunun çevresindeki
    // kare o hendeğin çerçevesiydi (ölçüldü — kalınlık probu, `SYMPTOMS.md`).
    //
    // Spec'in kendi yolu: arazi örtüsü GLOBAL SKALER `_SnowCoverage`'dan
    // gelir ve kalınlığı `_SnowCoverThickness` (4 cm). Her yerde aynı sayı,
    // bölge sınırı diye bir şey yok.

    // EĞİM: dik kayada kar durmaz. 0.45 ≈ 63° yatıklık.
    float snowSlope = saturate((normalWS.y - 0.45) / 0.35);

    // KENAR KIRILMASI DAĞIN KENDİ GÜRÜLTÜSÜNDEN. Yeni doku eklenmiyor —
    // kayanın kabartısı zaten orada ve kar sınırı ona oturunca düz kesilmiş
    // bir çizgi gibi durmuyor.
    float snowBreak = MountainFbm(worldPos * _BumpScale * 0.35, 2) * 0.5 + 0.5;

    float snowMask = SnowCoverMaskWithNoise(worldPos, normalWS, surface.occlusion, snowBreak,
                                            0.45, _SnowCoverSlopeSharpness,
                                            _SnowCoverBreakupStrength, _SnowCoverEdgeSharpness);

    if (snowMask > 0.001)
    {
        // İZ BURADA ÇİZİLİYOR — İKİNCİ BİR YÜZEYLE DEĞİL.
        //
        // Gerekçe `SnowRelief.hlsl`. Işın yürüyüşü çukurun görünen yerini
        // buluyor; sonraki bütün okumalar (doku, normal, gölgeleme) KAYDIRILMIŞ
        // konumdan yapılıyor, böylece çukur üç boyutlu okunuyor.
        float3 bakisWS = normalize(_WorldSpaceCameraPos - worldPos);
        float izDerinlik;
        float2 izKayma = SnowReliefOffset(worldPos, bakisWS, izDerinlik);
        float3 izPos = worldPos + float3(izKayma.x, 0.0, izKayma.y);
        float2 izUV = SnowWorldToUV(izPos);

        // İZİN İÇİ EZİLMİŞ KARDIR — YOĞUNLUK YEREL OKUNUYOR.
        //
        // Eskiden yoğunluk her yerde dünyanın genel değeriydi (`_FallbackRhoN`)
        // ve durum dokusu okunmuyordu; gerekçe "bölge sınırında tazelik sıçrar
        // ve kare geri gelir" idi. O gerekçe ikinci yüzey varken geçerliydi:
        // sınır artık yok, çünkü çizen shader tek.
        //
        // Sıçrama riski `SnowInsideMask` ile kapalı: bölge kenarında yerel
        // değer dünyanınkine yumuşak geçiyor. Kazanç, ayak izinin İÇİNİN
        // gerçekten sıkışmış görünmesi — kar basılınca yoğunlaşır, albedosu
        // düşer, pürüzlülüğü artar.
        float4 karDurum = SnowStateAt(izUV);
        float bolgeIci  = SnowInsideMask(izUV);

        float yerelRho = lerp(_FallbackRhoN, karDurum.g, bolgeIci);
        float yerelIslak = lerp(_SurfaceWetness, max(_SurfaceWetness, karDurum.b), bolgeIci);
        float yerelBozulma = karDurum.a * bolgeIci;

        // Spec §14.1: albedo ve pürüzlülük TAZELİKTEN, tazelik de yoğunluktan.
        float freshness = 1.0 - saturate((SnowDensity(yerelRho) - 100.0) / 350.0);

        half3 snowAlbedo = lerp(half3(0.70, 0.73, 0.79), half3(0.90, 0.92, 0.95), freshness);
        // Kuru kar pürüzlüdür; gerekçe `SnowLighting.hlsl` → `SnowBuildSurfaceFrom`.
        // İki yol aynı sayıyı kullanmak zorunda, yoksa aynı kar iki farklı
        // parlaklıkla çizilir.
        half  snowRough  = lerp(0.45, 0.72, freshness);

        // YÜZEY DOKUSU BURAYA GİRİYOR.
        //
        // Arazinin kar albedosu bu blokta kuruluyor, `SnowBuildSurface`'ten
        // bağımsız; doku yalnız oraya bağlandığında arazide HİÇBİR etkisi
        // olmuyordu (ölçüldü: güç 0 ile 3 arasında ekran farkı yok).
        //
        // Kar mesh'i de aynı harmanı kullanıyor. İkisi aynı dokuyu görmek
        // zorunda: mesh yalnız yerel sapmayı çiziyor, düz alanı arazi çiziyor.
        SnowSurfaceBlend karYuzey = SnowSampleSurface(izPos, yerelRho, yerelIslak, yerelBozulma);
        surface.snowBlend   = karYuzey;
        surface.snowRhoN    = (half)yerelRho;
        surface.snowWet     = (half)yerelIslak;
        surface.snowDisturb = (half)yerelBozulma;
        surface.snowDentDepth = (half)izDerinlik;

        snowAlbedo = saturate(snowAlbedo * karYuzey.albedoTint);
        snowRough  = saturate(snowRough + karYuzey.roughAdd);

        surface.albedo     = lerp(surface.albedo, snowAlbedo, snowMask);
        surface.smoothness = lerp(surface.smoothness, 1.0 - snowRough, snowMask);

        // Kar kayanın kabartısını GÖMÜYOR: normal düzleşip geometrik normale
        // dönüyor. Kar altındaki çatlağı göstermek kar değil, ıslak kaya olur.
        float3 snowNormal = normalWS;

        // Spec §14.2 Macro katmanı. `disturb` sıfır: dağda iz yok, ezilmiş kar
        // katmanının burada karşılığı da yok.
        //
        // DETAY YALNIZ YATAYA YAKIN YÜZEYDE. Spec'in kendi ön koşulu:
        // `WorldNormalToTangentPacked` tanjant çerçevesini dünya +Y'ye
        // sabitliyor ve gerekçesini "kar yüzeyi yataya yakın" diye yazıyor.
        // Kar MESH'i için doğru; dağda değil. Düzlemsel XZ örneklemesi dik
        // yamaçta dikey olarak eziliyor ve yüzeyde akan siyah şeritler
        // bırakıyor (ölçüldü: şeritler yalnız eğimin dikleştiği bantta).
        //
        // Ağırlık `snowSlope`: zaten hesaplanmış, yeni terim değil. Karın
        // durduğu yer ile detayın geçerli olduğu yer aynı yer.
        float3 detailed = SnowApplyDetailNormals(snowNormal, worldPos, freshness, 0.0,
                                                 length(_WorldSpaceCameraPos - worldPos));

        // Doku normalini de ekle: kar dokusunun asıl bilgisi burada.
        {
            float2 e = float2(detailed.x, detailed.z) / max(detailed.y, 1e-3)
                     + (float2)karYuzey.normalSlope;
            detailed = normalize(float3(e.x, 1.0, e.y));
        }

        snowNormal = normalize(lerp(snowNormal, detailed, snowSlope));

        surface.normalWS = normalize(lerp(surface.normalWS, snowNormal, snowMask));

        // ÇUKURUN EĞİMİ NORMALE EN SON GİRİYOR.
        //
        // `snowMask` harmanının İÇİNE konsaydı maskeyle sulanırdı; iz bir
        // ışıklandırma katmanı değil, yüzeyin kendi biçimi — kar oradaysa
        // çukur da oradadır. Ölçüldü: harmanın içindeyken 22 cm'lik iz ekranda
        // zar zor seçiliyordu.
        {
            half2 izEgim = SnowDentSlope(izUV);
            float3 n = surface.normalWS;
            float2 e = float2(n.x, n.z) / max(n.y, 1e-3) - (float2)izEgim;
            surface.normalWS = normalize(lerp(n, normalize(float3(e.x, 1.0, e.y)),
                                              saturate(izDerinlik * 20.0)));
        }

        // Mikro-oyuk karın altında kalıyor — ama TAMAMEN değil.
        //
        // 0.7 ile düzleştirilince arazinin oyukları kar altında yok oluyordu
        // ve kar tek parça beyaz kalıyordu (ölçüldü: zemin sapması 0.010,
        // güneşsizken 0.0023). Kar oyuğu doldurur, silmez: 15-20 cm'lik örtü
        // metrelik bir çukuru kapatmaz. Pay 0.55'e indirildi: 0.35'te zemin
        // luması 0.88'den 0.59'a düşüp güneşli kar için fazla koyu kaldı.
        surface.occlusion = lerp(surface.occlusion, 1.0, snowMask * 0.55);

        // ÇUKURUN GÖRÜŞ PAYI ÇOK YANSIMAYLA TELAFİ EDİLİYOR.
        //
        // Çukur göğü dar bir açıdan görüyor: görüş payı `V` derinlikle düşüyor.
        // Ama kaybolan gök ışığının yerine ÇUKURUN KENDİ DUVARLARI geçiyor ve
        // o duvarlar beyaz. Albedo `a` olan bir kovukta denge:
        //
        //     kazanç = V / (1 - a(1 - V))
        //
        // Aynı çok yansıma formülü kar-gök zincirinde de kullanılıyor
        // (`SnowLighting.hlsl` → `SnowAmbient`), ayrı bir kaynak kurulmuyor.
        //
        // Sayılarla: a = 0.91, V = 0.65 iken kazanç 0.95 — yalnız %5 koyu.
        // Telafi olmadan aynı V doğrudan uygulanıyordu ve BEYAZ YÜZEYİ DÜZ
        // ORANLA KARARTMAK onu GRİ yapıyordu (kullanıcı bildirdi: "kar izi
        // gri?? niye gri onu da bilmiyorum").
        //
        // Kar düzleştirmesinden SONRA uygulanıyor: düzleştirme çukuru silerdi.
        {
            half a = dot(snowAlbedo, half3(0.2126, 0.7152, 0.0722));
            half V = (half)saturate(1.0 - 0.55 * izDerinlik / SNOW_RELIEF_MAX_DEPTH);

            surface.occlusion *= V / max((half)1.0 - a * ((half)1.0 - V), (half)0.05);
        }


        surface.snowMask = (half)snowMask;
    }

    return surface;
}

#endif