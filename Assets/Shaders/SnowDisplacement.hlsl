#ifndef TOTHESUMMIT_SNOW_DISPLACEMENT_INCLUDED
#define TOTHESUMMIT_SNOW_DISPLACEMENT_INCLUDED

#include "SnowDrift.hlsl"

// KARIN GEOMETRİK DERİNLİĞİ. Kar örtüsü yüzeyi şimdiye kadar hiç değiştirmiyordu:
// karlı ve çıplak yüzey birebir aynı üçgenlerdi ve yakından bakınca boyanmış gibi
// duruyordu (bkz. DECISIONS.md, 8b).
//
// ÖLÇEK AYRIMI. Kar iki ayrı ölçekte derinlik yapar:
//   makro 0.2-3 m — kaya dibi birikintisi, sırt kornişi, dolmuş oluk. SİLUETİ değiştirir.
//   mikro 1-10 cm — yüzey tümseği, sastrugi. Normal haritası zaten taşıyor.
// Buradaki yer değiştirme YALNIZ makro.
//
// ŞEKLİ BİRİKİNTİ ALANI VERİYOR. Yalnız kot bandı, eğim ve rüzgâr maruziyeti
// kullanılsaydı derinlik arazi ızgarasının altında (4.28 m) dümdüz kalır ve
// geometriye çevrilince birikinti değil yumuşak bir kabarma çıkardı.
//
// FORMÜL BİLEREK SADE. Aynı hesap CPU'da da çalışıyor (`SnowSurface.cs`) — çarpışma
// yüzeyi görsel yüzeyi izlemeli. İki kopya karmaşıklaştıkça kaçınılmaz olarak ayrışır
// ve "kar var ama içinden geçiyorum" diye görünür. Gölgelendirmedeki `snow.depth`
// mikro gürültü ve serpinti içeriyor; o değer BURAYA GİRMİYOR.
//
// LOD ÇATLAĞI KURALI: yer değiştirme yalnız DÜNYA KONUMUNUN fonksiyonu. Yamadan,
// bölünme seviyesinden, köşe indeksinden girdi almaz. Alsaydı komşu yamalar ortak
// kenarda farklı değer üretir, aralarında delik açılırdı.

float _SnowDisplaceMax;        // en kalın birikintinin yüksekliği (metre)
float _SnowDisplaceStart;      // bu derinliğin altında geometri hiç oynamıyor

// Sönüm BÖLÜNMEDEN türüyor, ayrı bir ayar değil. Ayrı olduğunda yer değiştirme
// bölünmeden daha uzağa gidiyordu: aradaki kuşakta kaba üçgenler oynuyor ama
// bölünmüş komşularıyla aynı noktalardan örneklemiyorlar ve sınırda dikey boşluk
// açılıyordu. Yer değiştirme bölünmeden ÖNCE bitmek zorunda.
float _SnowTessNear;
float _SnowTessFar;

/// Makro kar derinliği, metre. Dört girdi — dördü de CPU'da birebir hesaplanabiliyor.
// ---- AYAK İZİ DEFORMASYONU ----
//
// Yerel, yüksek çözünürlüklü tampon; `SnowDeformation.cs` sürüyor. Kar örtüsünden
// AYRI bir sistem çünkü örtü ızgarası arazininkiyle aynı (7.32 m) ve ayak izi 0.3 m.
//
// Doku dünyaya DÖŞENİYOR: `uv = worldPos.xz / _SnowDeform.x`, Repeat sarımlı. Yani
// kaydırma/kopyalama yok. Döşemenin komşu kopyası görünmesin diye değer pencerenin
// içteğet çemberinde sönüyor: kare yarı kenarı 12 m, köşesi 17 m, sönüm 12 m'de biter.
//
// İşaret: pozitif = yüzey çökmüş, negatif = kenara itilmiş kar kabarmış.
TEXTURE2D(_SnowDeformTex);
SAMPLER(sampler_SnowDeformTex);
float4 _SnowDeform;   // x pencere kenarı (m), yz pencere merkezi (dünya xz), w sönüm yarıçapı

float SnowFootprint(float3 worldPos);

/// İZİN BULANIK OKUNMASI — kaba geometri için.
///
/// Deformasyon dokusunun texel'i 4.7 cm. Geometri ondan kabaysa (dış halkada 37.5 cm)
/// kenar hücrelere basamaklanıyor: ekranda izin kıyısında DÜZENLİ MERDİVEN çıkıyor,
/// kullanıcı bildirdi. Tek örnek, sekiz texel'lik bir aralığı temsil edemiyor.
///
/// Dört köşe örneği hücrenin yarı boyunda alınıp ortalanıyor — kutu bulanıklaştırma.
/// Dokuda mip zinciri yok (her kare yazılıyor, mip üretmek pahalı), o yüzden elle.
float SnowFootprintWide(float3 worldPos, float radius)
{
    float sum = SnowFootprint(worldPos + float3(-radius, 0.0, -radius))
              + SnowFootprint(worldPos + float3( radius, 0.0, -radius))
              + SnowFootprint(worldPos + float3(-radius, 0.0,  radius))
              + SnowFootprint(worldPos + float3( radius, 0.0,  radius));
    return sum * 0.25;
}

float SnowFootprint(float3 worldPos)
{
    if (_SnowDeform.x < 0.001) return 0.0;

    float2 uv = worldPos.xz / _SnowDeform.x;
    float value = SAMPLE_TEXTURE2D_LOD(_SnowDeformTex, sampler_SnowDeformTex, uv, 0).r;
    if (abs(value) < 1e-5) return 0.0;

    float toCenter = distance(worldPos.xz, _SnowDeform.yz);
    return value * (1.0 - smoothstep(_SnowDeform.w * 0.8, _SnowDeform.w, toCenter));
}

/// KARIN MİKRO KABARTISI — SASTRUGİ.
///
/// YALNIZ `SnowPatch` KULLANIYOR, arazi kullanmıyor. Arazi üçgeni 7.32 m ve
/// bölünmeyle en iyi 11.4 cm; 30 santimlik bir dalgacığı oraya yazmak aliasing
/// üretiyordu ve bu yüzden mikro kabartı bir dönem yalnız normal haritasına
/// bırakılmıştı (`SnowDisplacement.hlsl` başındaki ölçek ayrımı). Sonuç: yüzey ışıkta
/// pütürlü, siluette dümdüz — kullanıcı "kar tutması çok düzenli, minik dalgacıklar
/// yok" dedi. Yamanın hücresi 4.7 cm olduğu için kısıt orada yok.
///
/// FİZİK: sastrugi rüzgârın oyduğu sırtlardır. Rüzgâr yönünde UZUN, ona dik YAKIN
/// aralıklı; saha ölçümlerinde dalga boyu 0.2-2 m, yükseklik 2-20 cm. Rüzgârsız havada
/// oluşmazlar — genlik rüzgâr şiddetiyle ölçekleniyor.
///
/// İki bileşen:
///   SIRT   — rüzgâra dik 0.35 m, rüzgâr boyunca 2.5 m. Anizotropi sastruginin
///            kendisi; izotrop gürültü "kar" değil "kum" veriyor.
///   TANE   — 0.12 m, izotrop. Yüzeyin granüler dokusu. Hücre 4.7 cm olduğu için
///            bundan incesi geometriye ulaşmaz.
float SnowMicroRelief(float2 worldXZ, float depth)
{
    // Kar yoksa kabartı da yok; ince örtüde de sönük.
    float body = smoothstep(0.0, 0.25, depth);
    if (body <= 0.001) return 0.0;

    float2 windAxis = normalize(_SurfaceWindDir.xz + float2(0.0001, 0.0));
    float2 perpendicular = float2(-windAxis.y, windAxis.x);

    float along = dot(worldXZ, windAxis);
    float across = dot(worldXZ, perpendicular);

    // Rüzgâr boyunca uzatılmış koordinat: aynı gürültü, farklı ölçek.
    float ridge = SnowDriftNoise(float2(along / 2.5, across / 0.35));
    float grain = SnowDriftNoise(float2(along, across) / 0.12);

    // Rüzgâr sırtları oyuyor: dingin havada yüzey düz, fırtınada çizgili.
    float carved = lerp(0.25, 1.0, saturate(_SurfaceWindDir.w));

    return body * carved * ((ridge - 0.5) * 0.06 + (grain - 0.5) * 0.02);
}

float SnowMacroDepth(float3 worldPos)
{
    float altitude = worldPos.y - _TerrainOrigin.y;

    // Kalınlık DEPOSU (g), örtü (r) değil: örtü yüzeyin beyazlığı, depo altındaki
    // kalınlık. Bant dokusu kota göre; CPU'da aynı dizi zaten tutuluyor.
    float supply = SampleSnowProfile(altitude).g;
    if (supply < 0.001) return 0.0;

    // Yalnız EĞİM okunuyor. Konkavlık kanalı ızgaraya hizalı gürültü taşıyor ve
    // birikintiye girince yüzeyde tarama çizgisi bırakıyor (bkz. SnowDrift.hlsl).
    float slope = SampleSurfaceMapsFast(worldPos).a;   // 1 = düz, 0 = dik

    // Eğim: dik yamaçta kalın kar durmaz. Duruş açısı 70-75 derece ama KALIN
    // birikinti çok daha erken kayar — 40 derecede pratik olarak sıfır.
    float slopeFit = saturate((slope - 0.72) / 0.28);
    slopeFit *= slopeFit;

    // Birikinti şekli: rüzgâr hizalı, arazi eğrisiyle modüle. Derinliğin yatay
    // detayının tamamı buradan geliyor.
    float2 windAxis = normalize(_SurfaceWindDir.xz + float2(0.0001, 0.0));
    float drift = SnowDriftShape(worldPos.xz, windAxis);

    // ARAZİ AĞIRLIĞI. Birikinti alanı gürültüden gelen şekli veriyor; ağırlık ise
    // arazinin nerede kar TUTTUĞUNU: sırtın rüzgâraltı 0.67'den 2.0'a kadar. Uçlar
    // arası 3.0 kat — saha ölçümü rüzgâraltı yamaçta iki kat (taze karda dörde kadar).
    return supply * slopeFit * lerp(0.35, 1.4, drift)
         * SampleDriftWeight(worldPos) * _SnowDisplaceMax;
}

/// Geometriye uygulanacak yükseklik. Eşiğin altındaki ince örtü hiç oynamıyor:
/// 20 cm'lik bir örtü 4.28 m'lik ızgarada zaten çözülemiyor ve uygulanınca bütün
/// dağ hafifçe şişip hiçbir şey kazandırmıyordu.
float SnowDisplacement(float3 worldPos)
{
    float depth = SnowMacroDepth(worldPos);

    // Eşik yumuşak: sert kesme, birikintinin kenarında basamak bırakır.
    float above = smoothstep(_SnowDisplaceStart, _SnowDisplaceStart * 2.0, depth);

    // Sönüm bölünmenin İÇİNDE bitiyor: bölünme _SnowTessNear ile _SnowTessFar
    // arasında azalıyor, yer değiştirme ondan önce sıfıra iniyor. Böylece
    // bölünmesiz hiçbir üçgen yer değiştirmiş olmuyor.
    float toCamera = distance(worldPos, _WorldSpaceCameraPos);
    float fadeEnd = lerp(_SnowTessNear, _SnowTessFar, 0.75);
    float near = 1.0 - smoothstep(_SnowTessNear, fadeEnd, toCamera);

    return depth * above * near;
}

/// Geometriye uygulanan toplam.
///
/// İZ ARTIK BURADA DEĞİL. Arazi üçgeni 7.32 m ve izi çözemiyor; kazımak için gereken
/// bölünme kare başına üç milyon üçgen ediyordu. İzi `SnowPatch` taşıyor — kendi
/// ızgarası 4.7 cm ve arazi onun kapsadığı yerde zaten çiziliyor bile değil.
float SnowTotalDisplacement(float3 worldPos)
{
    return SnowDisplacement(worldPos);
}

/// Yer değiştirmenin EĞİMİ (d/dx, d/dz). Yüzey kabarıyorsa gölgelendirme normali de
/// o eğimi bilmeli; bilmezse siluet kabarır ama ışık düz yüzeyi aydınlatır ve
/// kabartma sahte görünür. Kapalı türev yok (doku okuması içeriyor), iki komşu örnek.
float2 SnowDisplacementGradient(float3 worldPos)
{
    // Adım birikinti gövdesinin onda biri kadar: daha küçüğü gürültünün kendi
    // hücresi içinde kalıp sıfır türev verir, daha büyüğü şekli düzler.
    const float Step = 3.0;

    float here = SnowDisplacement(worldPos);
    float dx = SnowDisplacement(worldPos + float3(Step, 0.0, 0.0)) - here;
    float dz = SnowDisplacement(worldPos + float3(0.0, 0.0, Step)) - here;

    // İZİN TÜREVİ ARTIK BURADA DEĞİL. Arazi izi hiç çizmiyor; onu `SnowPatch` taşıyor
    // ve kendi normalini kendi ızgarasında (4.7 cm) kuruyor.
    return float2(dx, dz) / Step;
}

#endif
