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
/// KARIN BRDF'i BUZUN F0'INDAN KURULUYOR.
///
/// URP'nin `InitializeBRDFData` metalik yolu F0'i `kDielectricSpec` = 0.04'e
/// sabitliyor; o deger cam/plastik icin (n = 1.5). Buz n = 1.31 ve F0 = 0.018.
/// Fark 2.2 kat ve dogrudan spekuler siddetine giriyor.
///
/// `InitializeBRDFDataDirect` reflectivity'yi disaridan aliyor: diffuse
/// `albedo * (1 - F0)`, spekuler F0, grazing terimi ikisinin toplami.
void SnowInitBRDF(half3 albedo, half smoothness, half f0, inout half alpha,
                  out BRDFData brdf)
{
    half oneMinus = 1.0h - f0;

    InitializeBRDFDataDirect(albedo, albedo * oneMinus, half3(f0, f0, f0),
                             f0, oneMinus, smoothness, alpha, brdf);
}

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
    // Aralık iki kez daraltıldı ve iki kez yetmedi: 0.26/0.48, sonra
    // 0.45/0.72. İkisinde de sıkışmış kar pürüzsüzlük 0.55'in üstünde kaldı
    // ve güneş yansıması ARABA BOYASI gibi keskin duruyordu (kullanıcı
    // bildirdi: "metalik bir görüntü", sonra "ışığın vurma açısına göre
    // bazen sulu zemin gibi").
    //
    // Sayı sonunda ölçüldü, tahmin edilmedi: pürüzsüzlük 0.72'de GGX tepe
    // yoğunluğu 52 ve öğle vakti spekülerin toplam içindeki payı %70. Kar
    // için fizik ~%1 söylüyor. Gerekçe ve uçlar `SNOW_ROUGH_PACKED`'te.
    //
    // Sıkışmış yüzey taze kardan DAHA düzgün olduğu için hâlâ daha düşük.
    s.roughness = lerp(SNOW_ROUGH_PACKED, SNOW_ROUGH_FRESH, freshness) * lerp(1.0, 0.62, wet);

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
    SnowInitBRDF(s.albedo, 1.0h - s.roughness, (half)SNOW_ICE_F0, alpha, s.brdfData);

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
half3 SnowDirectLight(Light L, float3 N, float3 V, SnowSurface s)
{
    // WRAP GENİŞLİĞİ = ışığın kar içinde yanal yayılma mesafesinin YÜZEY
    // EĞRİLİK YARIÇAPINA oranı.
    //
    // [ÖLÇÜM: yeşil ışıkta (550 nm) karın e-katlanma derinliği 37.4 mm.]
    // Ripple'ın eğrilik yarıçapı R = lambda^2/(4*pi^2*A) = 0.17^2/(4*pi^2*0.0029)
    // = 25 cm. Oran 3.7/25 = 0.15.
    //
    // 0.55 idi ve kullanıcı teşhis anahtarıyla gösterdi: wrap KAPALIYKEN
    // görüntü daha iyi. Fazla wrap `dot`u kaydırıp bütün kontrastı dar bir
    // banda sıkıştırıyor; güneşten kaçık yerler sönmek yerine orta griye
    // oturuyor ve leke leke okunuyor. Önce 0.20'ye çekildi, şimdi ölçülmüş
    // sayıya oturdu.
    const half W = 0.15;

    // BÖLEN (1+W)^2, (1+W) DEĞİL.
    //
    // Kâğıtta: wrap'li irradyansın yarımküre entegrali
    //   2*pi/(1+W) * integral_{-W}^{1} (u+W) du = 2*pi/(1+W) * (1+W)^2/2 = pi*(1+W)
    // Lambert'inki pi. Yani (1+W) kat FAZLA enerji çıkıyor ve yüzey aldığından
    // çoğunu geri veriyor. Tek bölenle yazılmış hâli 0.55'te %55 fazla
    // veriyordu.
    //
    // Normalizasyon KONTRASTI DEĞİŞTİRMİYOR (oran aynı kalıyor), yalnız
    // seviyeyi düşürüyor — kontrastı düzelten şey W'nin kendisi.
    half wrapNdotL = saturate((dot(N, L.direction) + W) / ((1.0 + W) * (1.0 + W)));
    if (_SnowDbgNoWrap > 0.5) wrapNdotL = saturate(dot(N, L.direction));
    half3 diffuse = s.albedo * wrapNdotL;

    // Arkadan aydınlanma: ince karda ışık öbür taraftan sızıyor.
    half back  = saturate(dot(V, -L.direction));
    half trans = pow(back, 3.0) * exp(-s.snowDepth * 7.0) * _TranslucencyStrength;
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
    // YALNIZ ORTAMA. Doğrudan ışığa uygulamak gölgeyi iki kez saymaktır ve
    // izleri siyah lekelere çevirir (spec §18.5, §22).
    if (_SnowDbgNoAO <= 0.5) ambient *= heightAO;

    // YATAY TRANSFER AO'NUN DIŞINDA — AO GÖĞÜ MODELLİYOR, YANI DEĞİL.
    //
    // Terim bir tur `heightAO` çarpımının içinde kaldı ve AO'nun düştüğü
    // yerde birlikte söndü. Komşu kardan YANLAMASINA gelen ışık, göğün
    // ne kadar görüldüğüyle kısılmaz: çukurun içi göğü az görür ama
    // duvarlarını tam görür, ve o duvarlar aydınlık kardır.
    if (_SnowDbgNoBounce <= 0.5)
        ambient += gunesRenk * saturate(gunesYon.y) * s.albedo * SNOW_LATERAL_BOUNCE;

    return ambient;
}

#endif
