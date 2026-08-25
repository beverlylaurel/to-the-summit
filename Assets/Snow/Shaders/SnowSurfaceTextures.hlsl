// ROL: kar yuzeyinin fotogrametri dokularini durum degiskenlerinden harmanlar.
// Cagiran: SnowLighting.hlsl (SnowBuildSurface).

#ifndef SNOW_SURFACE_TEXTURES_INCLUDED
#define SNOW_SURFACE_TEXTURES_INCLUDED

#include "SnowCommon.hlsl"
#include "../../Shaders/StochasticTiling.hlsl"

/// DORT KAR YUZEYI, DORT FIZIKSEL DURUM.
///
/// Albedo eskiden yalniz yogunluktan tureyen iki sabit arasinda lerp'leniyordu
/// (taze 0.90 / sikismis 0.70). Yuzeyin kendi dokusu yoktu: kar her yerde ayni
/// duz renkti ve izin prosedurel kenari cevresindeki bosluga oturmuyordu
/// (kullanici bildirdi: "izin dagilma kenarlariyla disardaki karlar arasinda
/// uyum yok").
///
/// Dortu de AYNI durum zincirinden besleniyor, ayri bir kaynak kurulmuyor:
///
///   TAZE      dusuk yogunluk, bozulmamis  -> duz, ozelliksiz ortu
///   TOZ       kuru ve soguk               -> ince taneli, granullu
///   YERLESMIS yogunluk yuksek             -> topakli, orta puruzlu
///   RUZGAR    ruzgar maruziyeti yuksek    -> oluklu, sastrugi cizgili
///
/// Sicaklik ve ruzgar zaten atmosfer durumundan geliyor (`_TemperatureC`,
/// `SampleWindShadow`); yogunluk ve bozulma kar dokusundan. Yani yuzey
/// gorunumu havayla ayni tek durumdan turuyor.
TEXTURE2D(_SnowSurfTazeColor);      TEXTURE2D(_SnowSurfTazeNormal);      TEXTURE2D(_SnowSurfTazeRough);
TEXTURE2D(_SnowSurfTozColor);       TEXTURE2D(_SnowSurfTozNormal);       TEXTURE2D(_SnowSurfTozRough);
TEXTURE2D(_SnowSurfYerlesmisColor); TEXTURE2D(_SnowSurfYerlesmisNormal); TEXTURE2D(_SnowSurfYerlesmisRough);
TEXTURE2D(_SnowSurfRuzgarColor);    TEXTURE2D(_SnowSurfRuzgarNormal);    TEXTURE2D(_SnowSurfRuzgarRough);
/// ORTAK GLOBAL SAMPLER. Doku basina SAMPLER() bildirmek DepthNormals
/// gecisinde kirildi: o gecis rengi kullanmadigi icin derleyici dokuyu eliyor,
/// geriye esi olmayan bir sampler kaliyor ve "does not match any texture"
/// hatasi veriyor. URP'nin global sampler'i her gecidte gecerli.
#define SNOW_SURF_SAMPLER sampler_TrilinearRepeat

/// Dokunun dunya olcegi: bir dosemenin kapladigi metre.
float _SnowSurfTileMeters;

/// Doku katkisinin gucu. 0 = eski duz renk, 1 = dokunun tamami.
float _SnowSurfStrength;

struct SnowSurfaceBlend
{
    half3 albedoTint;   // 1 civarinda carpan
    half  roughAdd;     // puruzluluge eklenen sapma
    half2 normalSlope;  // egim uzayinda detay (n.xy / n.z)
};

/// Dort dokunun agirligi. Toplam 1'e normalize.
half4 SnowSurfaceWeights(float rhoN, float wet, float disturb, float3 posWS)
{
    // YOGUNLUK: taze (dusuk) <-> yerlesmis (yuksek)
    half packed = (half)saturate((SnowDensity(rhoN) - 100.0) / 250.0);

    // KURULUK: soguk ve kuru kar toz kalir; islanan kar topaklanir.
    // -12 C altinda tam toz, -2 C uzerinde hic yok.
    half kuru = (half)saturate((-_TemperatureC - 2.0) / 10.0) * (half)(1.0 - saturate(wet * 2.0));

    // RUZGAR MARUZIYETI: siperde kalan yuzey oluk tutmaz.
    half ruzgar = (half)saturate(SampleWindShadow(posWS) * 1.2 - 0.1);

    // Bozulmus (uzerinden gecilmis) kar dokusunu yitirir, yerlesmise yaklasir.
    packed = max(packed, (half)saturate(disturb));

    half wRuzgar    = ruzgar * (half)0.9;
    half wToz       = kuru * ((half)1.0 - wRuzgar) * (half)0.9;
    half wYerlesmis = packed * ((half)1.0 - wRuzgar) * ((half)1.0 - wToz);
    half wTaze      = (half)1.0 - wRuzgar - wToz - wYerlesmis;

    half4 w = half4(max(wTaze, (half)0.0), wToz, wYerlesmis, wRuzgar);
    return w / max(w.x + w.y + w.z + w.w, (half)1e-4);
}

/// Normal haritayi egim uzayinda okur. Egim toplama, RNM'in aksine tabani
/// yapisi geregi koruyor (`SnowDetailNormals.hlsl` ile ayni gerekce).
/// EGIME FIZIKSEL TAVAN.
///
/// `n.xy / n.z` normal haritanin mavi kanali sifira yaklastiginda patliyor.
/// BC7 sikistirmasi tek tek dokuları o sinira itiyor ve ekranda IZOLE koyu
/// mavi noktalar cikiyordu (olculdu: guc 1.6'da zemin lekeli). Kar yuzeyinin
/// mikro kabartisi 45 dereceyi gecmez — kar o egimde durmaz, akar. tan(45)=1
/// tavan, guvenlik payiyla 0.7 (35 derece).
#define SNOW_SURF_EGIM_TAVANI 0.7

half2 SnowSurfEgimKis(half2 e)
{
    half boy = length(e);
    return boy > (half)SNOW_SURF_EGIM_TAVANI
         ? e * ((half)SNOW_SURF_EGIM_TAVANI / boy)
         : e;
}

half2 SnowSurfEgimTavan(half3 n)
{
    return SnowSurfEgimKis(n.xy / max(n.z, (half)0.2));
}

/// STOKASTIK OKUMA — DOSEME TEKRARI KIRILIYOR.
///
/// Duz doseme 2.5 m'de kendini tekrar ediyor ve zemin duzenli leke izgarasi
/// olarak okunuyor (olculdu: kabarti geldigi anda tekrar da geldi). Heitz-Neyret
/// altigen izgarasi her hucreye kendi kaymasini veriyor, periyot kayboluyor.
///
/// VARYANS GERI KAZANIMI EGIMDE GECERLI. Uc ornegin agirlikli ortalamasi
/// genligi `length(weights)` kadar dusuruyor; egim SIFIR ORTALAMALI bir
/// buyukluk oldugu icin ayni katsayiya bolmek genligi aynen geri getiriyor.
/// (Renkte gecerli degil, orada ortalama sifir degil — bu yuzden renk tek
/// ornekle okunuyor; albedo degisimi zaten %2, tekrari gorunmuyor.)
half2 SnowSurfSlope(TEXTURE2D_PARAM(tex, samplerState), float2 uv)
{
    float2 v1, v2, v3;
    float3 w;
    StochasticHexGrid(uv, v1, v2, v3, w);

    float2 dx = ddx(uv);
    float2 dy = ddy(uv);

    half3 n1 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tex, samplerState, uv + StochasticHash(v1), dx, dy));
    half3 n2 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tex, samplerState, uv + StochasticHash(v2), dx, dy));
    half3 n3 = UnpackNormal(SAMPLE_TEXTURE2D_GRAD(tex, samplerState, uv + StochasticHash(v3), dx, dy));

    half2 e1 = SnowSurfEgimTavan(n1);
    half2 e2 = SnowSurfEgimTavan(n2);
    half2 e3 = SnowSurfEgimTavan(n3);

    half2 harman = (half)w.x * e1 + (half)w.y * e2 + (half)w.z * e3;
    return SnowSurfEgimKis(harman / (half)max(length(w), 1e-3));
}

/// Dort dokunun harmanı. Agirligi ihmal edilebilir olan doku OKUNMUYOR:
/// dort tam okuma (renk + normal + puruzluluk) on iki doku erisimi demek ve
/// cogu piksel icin ikisi zaten sifir agirlikta.
SnowSurfaceBlend SnowSampleSurface(float3 posWS, float rhoN, float wet, float disturb)
{
    SnowSurfaceBlend o;
    o.albedoTint  = half3(1, 1, 1);
    o.roughAdd    = 0;
    o.normalSlope = half2(0, 0);

    if (_SnowSurfStrength <= 0.001) return o;

    half4 w = SnowSurfaceWeights(rhoN, wet, disturb, posWS);
    float2 uv = posWS.xz / max(_SnowSurfTileMeters, 0.01);

    // İKİ ÖLÇEKLİ ALAN BÜKÜMÜ — DESEN KENDİNİ TEKRAR ETMESİN.
    //
    // Stokastik döşeme periyodu kırıyor ama okuma hâlâ DÜZENLİ bir ızgaradan
    // geliyor: aynı leke aynı yönde, aynı aralıkla dizilmiş görünüyor
    // (kullanıcı bildirdi: "kar dokusu çok düzenli"). Gerçek kar yüzeyinde
    // desen rüzgârla akar, gerilir, kıvrılır.
    //
    // Koordinatın kendisi büküldüğünde desen artık düz bir ızgarada
    // durmuyor: uzun dalga lekeleri sürüklüyor, kısa dalga kenarlarını
    // kemiriyor. Genlik ikisinde de kendi dalga boyunun altında —
    // üstünde olsaydı doku kendi üstüne katlanıp bulanırdı.
    float2 bukum =
        (float2(SnowValueNoise(posWS.xz * 0.11),
                SnowValueNoise(posWS.xz * 0.11 + 23.7)) * 2.0 - 1.0) * 0.65 +
        (float2(SnowValueNoise(posWS.xz * 0.47),
                SnowValueNoise(posWS.xz * 0.47 + 61.3)) * 2.0 - 1.0) * 0.14;

    uv += bukum;

    half3 renk = 0;
    half  puru = 0;
    half2 egim = 0;

    // HER DOKUNUN KENDI UZAMSAL ORTALAMASI, DOGRUSAL UZAYDA OLCULDU.
    // (Assets/Snow/Textures/Surface, 256x256 orneklem.)
    half3 ortToplam = 0;

    const half ESIK = 0.02;

    if (w.x > ESIK)
    {
        renk += w.x * SAMPLE_TEXTURE2D(_SnowSurfTazeColor, SNOW_SURF_SAMPLER, uv).rgb;
        puru += w.x * SAMPLE_TEXTURE2D(_SnowSurfTazeRough, SNOW_SURF_SAMPLER, uv).r;
        ortToplam += w.x * half3(0.8434, 0.8965, 0.9446);
        egim += w.x * SnowSurfSlope(TEXTURE2D_ARGS(_SnowSurfTazeNormal, SNOW_SURF_SAMPLER), uv);
    }
    if (w.y > ESIK)
    {
        renk += w.y * SAMPLE_TEXTURE2D(_SnowSurfTozColor, SNOW_SURF_SAMPLER, uv).rgb;
        puru += w.y * SAMPLE_TEXTURE2D(_SnowSurfTozRough, SNOW_SURF_SAMPLER, uv).r;
        ortToplam += w.y * half3(0.2949, 0.2990, 0.3019);
        egim += w.y * SnowSurfSlope(TEXTURE2D_ARGS(_SnowSurfTozNormal, SNOW_SURF_SAMPLER), uv);
    }
    if (w.z > ESIK)
    {
        renk += w.z * SAMPLE_TEXTURE2D(_SnowSurfYerlesmisColor, SNOW_SURF_SAMPLER, uv).rgb;
        puru += w.z * SAMPLE_TEXTURE2D(_SnowSurfYerlesmisRough, SNOW_SURF_SAMPLER, uv).r;
        ortToplam += w.z * half3(0.7740, 0.8602, 0.9412);
        egim += w.z * SnowSurfSlope(TEXTURE2D_ARGS(_SnowSurfYerlesmisNormal, SNOW_SURF_SAMPLER), uv);
    }
    if (w.w > ESIK)
    {
        renk += w.w * SAMPLE_TEXTURE2D(_SnowSurfRuzgarColor, SNOW_SURF_SAMPLER, uv).rgb;
        puru += w.w * SAMPLE_TEXTURE2D(_SnowSurfRuzgarRough, SNOW_SURF_SAMPLER, uv).r;
        ortToplam += w.w * half3(0.7585, 0.8271, 0.8837);
        egim += w.w * SnowSurfSlope(TEXTURE2D_ARGS(_SnowSurfRuzgarNormal, SNOW_SURF_SAMPLER), uv);
    }

    // RENK CARPAN OLARAK GIRIYOR, YERINE GECMIYOR.
    //
    // Albedonun seviyesi fizikten geliyor (taze 0.90 / sikismis 0.70) ve
    // isiklandirma zinciri o araliga gore ayarli. Doku yerine konsaydi kar
    // fotogrametri orneginin kendi pozlamasini tasirdi. Dokunun isi DESENI
    // vermek: kendi ortalamasina bolunup 1 civarinda bir carpan haline
    // getiriliyor, sonra gucle yumusatiliyor.
    // ORTALAMA DOKUNUN UZAMSAL ORTALAMASI, PIKSELIN KENDI PARLAKLIGI DEGIL.
    //
    // Once `(renk.r+renk.g+renk.b)/3` kullaniliyordu: bu pikselin KENDI
    // parlakligi. Ona bolununce her piksel 1'e normalize oluyor ve dokunun
    // parlaklik deseni tamamen siliniyor; geriye yalniz renk tonu kaliyor.
    // Olculdu: guc 0 ile 3 arasinda ekran sapmasi 0.01003 -> 0.00971, yani
    // desen hic gelmiyordu. Sabit uzamsal ortalama deseni yerinde birakiyor.
    half3 ortalama = max(ortToplam, (half3)1e-3);

    // KONTRAST GERI KAZANIMI. Kar dokusunun kendi kontrasti dusuk (her sey
    // beyaz); ortalamaya bolununce carpan 0.97-1.03 araligina siksiyor ve
    // ekranda hic gorunmuyor. Sapma ortalamadan uzaklastirilarak deseni
    // gorunur kiliyor; seviye hala 1 civarinda kaliyor, yani albedonun
    // fizikten gelen buyuklugu korunuyor.
    // ÇARPANA TAVAN. Kar albedosunun uzamsal değişimi gerçekte %20'yi geçmez;
    // beyaz bir maddenin deseni renkte değil kabartıdadır. Sınır olmadan güç
    // büyüdükçe çarpan kanal kanal ayrışıp yüzeyi doygun mavi/hardal lekeye
    // çeviriyordu (materyal 3 güçle çizerken görüldü).
    half3 carpan = clamp((half)1.0 + (renk / ortalama - (half)1.0) * (half)2.5,
                         (half3)0.8, (half3)1.2);

    o.albedoTint  = lerp(half3(1, 1, 1), carpan, (half)_SnowSurfStrength);
    o.roughAdd    = (puru - (half)0.5) * (half)0.25 * (half)_SnowSurfStrength;
    o.normalSlope = egim * (half)_SnowSurfStrength;
    return o;
}

#endif
