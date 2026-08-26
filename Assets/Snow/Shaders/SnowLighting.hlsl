// ROL: kar yüzeyinin ışıklandırması (spec §14.1, §14.3).
// Çağıran: SnowLitForwardPass.

#ifndef SNOW_LIGHTING_INCLUDED
#define SNOW_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "SnowCommon.hlsl"
#include "SnowSparkle.hlsl"
#include "SnowSurfaceTextures.hlsl"

/// Bir tekseldeki karın ışıkla ilgili her şeyi. Tek yerde toplanıyor ki
/// doğrudan ışık, ek ışıklar ve ortam aynı yüzeyi görsün.
struct SnowSurface
{
    half3  albedo;
    half   roughness;
    half   wet;
    half   disturb;
    half   crust;
    half2  surfSlope;   // yüzey dokusunun eğim uzayındaki detayı
    float  snowDepth;
    float3 positionWS;
    float  pixelFootprint;
    BRDFData brdfData;
};

/// YÜZEY PARAMETRELERİ (spec §14.1).
///
/// Ölçüm: taze kuru kar albedosu 0.80–0.90, eski/sıkışmış 0.45–0.70.
/// ÖRNEKLENMİŞ HARMANLA KURULUM.
///
/// Doku okuması dışarıda kalıyor: arazi yüzey dokusunu zaten kendi
/// albedo/normal bloğunda okuyor, `SnowBuildSurface` içinde ikinci kez
/// örneklenince aynı piksel için on iki doku erişimi iki katına çıkıyordu.
SnowSurface SnowBuildSurfaceFrom(SnowSurfaceBlend yuzey,
                                 float rhoN, float wet, float disturb, float crust,
                                 float snowDepth, float3 positionWS, float pixelFootprint)
{
    float freshness = 1.0 - saturate((SnowDensity(rhoN) - 100.0) / 350.0);

    const half3 ALBEDO_FRESH  = half3(0.90, 0.92, 0.95);
    const half3 ALBEDO_PACKED = half3(0.70, 0.73, 0.79);
    const half3 TINT_WET      = half3(0.84, 0.86, 0.89);

    SnowSurface s;

    s.albedo    = lerp(ALBEDO_PACKED, ALBEDO_FRESH, freshness) * lerp(half3(1, 1, 1), TINT_WET, wet);
    // KURU KAR PÜRÜZLÜDÜR — PARLAK DEĞİL.
    //
    // Eskiden sıkışmış kar 0.26, taze kar 0.48 pürüzlülük alıyordu; yani
    // pürüzsüzlük 0.74 ve 0.52. Bu değerlerde speküler lob dar kalıyor ve
    // güneş yansıması karın üstünde ARABA BOYASI gibi keskin bir parlaklık
    // bırakıyor (kullanıcı bildirdi: "metalik bir görüntü").
    //
    // Kuru karın yansıması çoklu saçılımdan gelir: geniş, sönük, yönsüze
    // yakın. Ayna gibi davranan şey ıslak kar ve buz kabuğudur — ikisi de
    // aşağıda ayrıca ele alınıyor.
    //
    // Sıkışmış yüzey taze kardan DAHA düzgün olduğu için hâlâ daha düşük.
    s.roughness = lerp(0.45, 0.72, freshness) * lerp(1.0, 0.62, wet);

    // YÜZEY DOKUSU. Dört fotogrametri seti durum zincirinden harmanlanıyor
    // (gerekçe `SnowSurfaceTextures.hlsl`). Albedonun SEVİYESİ yukarıdaki
    // fizikten geliyor; doku yalnız deseni çarpan olarak ekliyor.
    s.albedo    = saturate(s.albedo * yuzey.albedoTint);
    s.roughness = saturate(s.roughness + yuzey.roughAdd);
    s.surfSlope = yuzey.normalSlope;

    // KABUK BUZDUR (spec §18.3). Daha parlak, daha az parıldar. Faz 11'e
    // kadar `crust` sıfır kalıyor ve bu satırlar hiçbir şey yapmıyor.
    half crustMask = saturate((crust - 0.35) / 0.35);

    // Kabuk buz: gerçekten parlak, ama 0.12 cam gibiydi. Buz yüzeyi de
    // mikro çatlaklı ve mattır; 0.25 keskin ama abartısız bir yansıma veriyor.
    s.roughness = lerp(s.roughness, 0.25, crustMask);
    s.albedo    = lerp(s.albedo, s.albedo * half3(0.93, 0.95, 1.00), crustMask);

    s.wet = wet;
    s.disturb = disturb;
    s.crust = crustMask;
    s.snowDepth = snowDepth;
    s.positionWS = positionWS;
    s.pixelFootprint = pixelFootprint;

    // `alpha` PARAMETRESİ `inout`; sabit geçilemez. Kar opak, değer geri
    // okunmuyor ama derleyicinin l-value istemesi bir yerel gerektiriyor.
    half alpha = 1.0h;
    InitializeBRDFData(s.albedo, 0.0h, half3(0, 0, 0), 1.0h - s.roughness, alpha, s.brdfData);

    return s;
}

/// Dokuyu kendi okuyan sarmalayıcı. Kar mesh'i bunu kullanıyor: orada yüzey
/// dokusunun tek okuyucusu bu fonksiyon.
SnowSurface SnowBuildSurface(float rhoN, float wet, float disturb, float crust,
                             float snowDepth, float3 positionWS, float pixelFootprint)
{
    return SnowBuildSurfaceFrom(SnowSampleSurface(positionWS, rhoN, wet, disturb),
                                rhoN, wet, disturb, crust, snowDepth,
                                positionWS, pixelFootprint);
}

/// DOĞRUDAN IŞIK (spec §14.3).
///
/// Sarmalı NdotL: kar yarı saydam, ışık yüzeyin altına girip yandan çıkıyor.
/// Sert bir NdotL karı plastik gösterir.
/// TEŞHİS ANAHTARLARI — kar ışıklandırmasının her terimi tek tek kapanır.
///
/// Alçak güneşte keskin kenarlı bej adacıklar bildirildi ve maske, cavity,
/// bulut gölgesi, gölge haritası, geometri sırayla elendi. Kalan yer bu
/// dosya. Şüpheliyi tur tur aramak yerine terimlerin TAMAMI aynı anda
/// kapatılabilir yapıldı.
float _SnowDbgNoSpec;
float _SnowDbgNoSparkle;
float _SnowDbgNoTrans;
float _SnowDbgNoWrap;
float _SnowDbgNoAO;
float _SnowDbgNoBounce;
float _SnowDbgNoShadowTint;

half3 SnowDirectLight(Light L, float3 N, float3 V, SnowSurface s)
{
    const half W = 0.55;

    half wrapNdotL = saturate((dot(N, L.direction) + W) / (1.0 + W));
    if (_SnowDbgNoWrap > 0.5) wrapNdotL = saturate(dot(N, L.direction));

    half3 diffuse = s.albedo * wrapNdotL;

    // Arkadan aydınlanma: ince karda ışık öbür taraftan sızıyor.
    half back  = saturate(dot(V, -L.direction));
    half trans = pow(back, 3.0) * exp(-s.snowDepth * 7.0) * _TranslucencyStrength;
    if (_SnowDbgNoTrans <= 0.5)
        diffuse += s.albedo * trans * half3(1.00, 1.02, 1.10);

    // SPEKÜLER URP SÖZLEŞMESİYLE. `DirectBRDFSpecular` yalnız D·V SKALERİNİ
    // döndürüyor; URP onu `brdfData.specular` ile ve `NdotL` ile çarpıyor
    // (`LightingPhysicallyBased`). Spec §14.1 ikisini de yazmamış, kod da
    // sadık kalmıştı.
    //
    // Ölçüldü (öğle, 20 cm kar, düz zemin, post kapalı):
    //   diffuse 1.747   spec 4.133   sparkle 0.161   toplam 6.041
    //   aynı karede arazi karının doğrudan ışığı 0.868
    // Yani doğrudan ışığın %68'i, dielektrik karda 0.04 olması gereken bir
    // çarpanın hiç uygulanmamasından geliyordu.
    half NdotL = saturate(dot(N, L.direction));
    half3 spec = s.brdfData.specular
               * DirectBRDFSpecular(s.brdfData, N, L.direction, V) * NdotL;

    if (_SnowDbgNoSpec > 0.5) spec = (half3)0.0;

    // PARILTI SADECE GÜNDÜZ. `_SunElevation01` gündöngüsünden geliyor;
    // uygulanmazsa gece kar parıldar (spec §22).
    half sunGate = saturate(_SunElevation01 * 20.0);

    half sparkle = 0;

#if !defined(_SNOW_QUALITY_LOW)
    // MESAFE KAPISI. Gerekçe `SNOW_SPARKLE_FADE_START` yanında: parıltının
    // boyutu hücre boyuna bağlı, hücre uzakta LOD ile büyüyor ve tek hücre
    // birçok pikseli kaplayınca uzaktan iri parlak lekeler çıkıyor.
    float sparkleDist = distance(s.positionWS, _WorldSpaceCameraPos);
    half distGate = 1.0h - (half)smoothstep(SNOW_SPARKLE_FADE_START,
                                            SNOW_SPARKLE_FADE_END, sparkleDist);

    if (distGate > 0.0h)
        sparkle = SnowSparkle(s.positionWS, V, L.direction, s.pixelFootprint)
                * (1.0 - s.wet) * (1.0 - s.disturb * 0.45)
                * (1.0 - s.crust * 0.7)
                * saturate(dot(N, L.direction) * 4.0) * sunGate * distGate;
#endif

    half3 lightCol = L.color * (L.distanceAttenuation * L.shadowAttenuation);

    if (_SnowDbgNoSparkle > 0.5) sparkle = 0;

    return (diffuse + spec + sparkle * _SparkleIntensity) * lightCol;
}

/// ORTAM (spec §14.3) [KAYNAK: Batman GDC 2014 — geçiş bölgesinde diffuse'u
/// gökyüzü rengiyle tint'leyerek sahte SSS].
///
/// GECE KAR KOYU OLUR. Ortam düşükse kar da koyu; bu doğru davranış. Karı
/// gece aydınlatmak için `_ShadowTint`'i veya ambient'i yükseltmek yasak.
/// 1 = kar-gok coklu yansimasi acik. Olcum icin disaridan 0 yazilabiliyor.
float _SnowMultiScatter;

half3 SnowAmbient(float3 N, SnowSurface s, half mainShadow, half heightAO,
                  half3 gunesRenk, float3 gunesYon)
{
    half3 ambient = SampleSH(N) * s.albedo;

    half shadowed = 1.0 - mainShadow;
    if (_SnowDbgNoShadowTint <= 0.5)
        ambient *= lerp(half3(1, 1, 1), (half3)_ShadowTint.rgb, shadowed);

    // KAR İLE GÖK ARASINDA ÇOKLU YANSIMA.
    //
    // Kar gelen ışığın ~%85'ini geri gönderiyor; gök (ve özellikle bulut
    // tabanı) onun bir kısmını tekrar aşağı yansıtıyor. Sonsuz seri, kapalı
    // biçimde: 1 / (1 - a·s), a = kar albedosu, s = göğün geri yansıtma payı.
    // Kar sahalarında bu terim olmadan gölgeler olduğundan çok koyu çıkar.
    //
    // Ölçüldü (10:00, bulut gölgesi altında / açıkta): zemin luması 0.0898
    // ↔ 0.8461, yani 1/9. Gerçekte güneşli ve bulut gölgeli kar bu kadar
    // ayrışmaz; eksik olan terim buydu.
    //
    // `s` SABİT ve ölçülü. Bir dönem gölgeye bağlanmıştı ("güneş kısıldıysa
    // üstümüzde bulut vardır") — yanlış: dağın kendi gölgesi açık gökte de
    // olur ve orada bulut yoktur. Bulut kapsaması global olarak yayınlanmıyor
    // (ölçüldü: `_CloudCoverage` global sıfır), yani ayrım yapılamıyor.
    // Ayrım yapamayan bir katsayı sabit kalır.
    //
    // 0.25: yüksek irtifada açık gökte Rayleigh + aerosol geri yansıtması.
    // Kâğıtta çarpan 1 / (1 - 0.9·0.25) = 1.29 — ölçülü bir artış, gölgeyi
    // yok etmiyor, yalnız kar sahasının gerçek parlaklığını geri veriyor.
    half karAlbedo = (s.albedo.r + s.albedo.g + s.albedo.b) * (half)0.3333;
    half cokKat = (half)1.0 / max((half)1.0 - karAlbedo * (half)0.25, (half)0.25);

    // Teşhis anahtarı: 0 yazılınca terim kapanır, aynı karede ölçüm alınır.
    ambient *= lerp((half)1.0, cokKat, (half)_SnowMultiScatter);

    // KAR-KAR YATAY TRANSFERİ — GÖLGEDEKİ KAR IŞIĞI YANDAN ALIYOR.
    //
    // Yukarıdaki terim karın GÖKLE çoklu yansımasını veriyor. Ama gölgedeki
    // kar asıl ışığı gökten değil YANDAN alıyor: çevresindeki AYDINLIK kar
    // ona yansıtıyor. `SampleSH` bunu içeremiyor çünkü SH statik ve güneşin
    // o anki katkısını taşımıyor — gölge, güneş ne kadar parlarsa parlasın
    // aynı kalıyordu.
    //
    // Ölçüldü (kullanıcı, 06:20 karesi): aydınlık kar ~180, gölgeli ~15,
    // oran 0.08. Bu dosyanın kendi kaydı aynı sayıyı veriyor: "zemin luması
    // 0.0898 ↔ 0.8461, yani 1/9". Kâğıtta olması gereken 0.49 — gölgeli
    // nokta yarımkürenin ~yarısını aydınlık kar olarak görüyor ve kar
    // albedosu 0.85. ALTI KAT eksikti.
    //
    // Gök terimi (1.29) bu farkı kapatmaya çalışıyordu ve yetmiyordu, çünkü
    // yanlış yönü modelliyor: eksik olan dikey değil YATAY transfer.
    //
    // GÖLGEYE BAĞLANMIYOR. Aydınlık kar da komşusundan ışık alıyor — kar
    // sahasının gerçekten parlak olmasının sebebi bu. Gölgeye bağlansaydı
    // telafi terimi olurdu, fizik değil.
    if (_SnowDbgNoBounce <= 0.5)
        ambient += gunesRenk * saturate(gunesYon.y) * s.albedo * SNOW_LATERAL_BOUNCE;

    // YALNIZ ORTAMA. Doğrudan ışığa uygulamak gölgeyi iki kez saymaktır ve
    // izleri siyah lekelere çevirir (spec §18.5, §22).
    if (_SnowDbgNoAO <= 0.5) ambient *= heightAO;

    return ambient;
}

#endif
