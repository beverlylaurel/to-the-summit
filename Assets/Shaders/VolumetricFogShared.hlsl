#ifndef TOTHESUMMIT_VOLUMETRIC_FOG_SHARED_INCLUDED
#define TOTHESUMMIT_VOLUMETRIC_FOG_SHARED_INCLUDED

// SİSİN YOĞUNLUK MODELİ. Havanın nerede ne kadar yoğun olduğu — ışığın orada ne yaptığı
// DEĞİL. Renk, aydınlatma ve uygulama `HeightFog.hlsl`'de kalır.
//
// AYRILMA SEBEBİ: bu model iki yerde değerlendiriliyor. Froxel hacminin içinde compute
// shader, hacmin ötesinde yüzey shader'ının analitik kuyruğu. `HeightFog.hlsl` compute
// tarafından include EDİLEMEZ — orası `_WorldSpaceCameraPos`, URP aydınlatması ve yüzey
// bağlamına bağlı. Model tek yerde durmazsa iki değerlendirici ayrışır ve hacmin
// sınırında sisin yapısı değişir.
//
// Include eden dosya URP `Core.hlsl`'i ÖNCE almış olmalı: `TEXTURE2D`, `SAMPLER` ve
// `SAMPLE_TEXTURE2D_LOD` oradan geliyor.

// ---------------------------------------------------------------------------
// FROXEL HACMİ — kamera frustum'una hizalı 3B ızgara.
//
// x/y ekseni doğrudan ekran koordinatı, z ekseni ÜSTEL derinlik dağılımı. Wronski
// dağılımın "kameraya yakın yoğunlaştığını" söylüyor ama formülü vermiyor (spec §10.1);
// saf üstel seçildi:
//
//     z(s) = near · (far/near)^s ,  s ∈ [0,1]
//
// Dilim başına oran sabit: 64 dilim, 0.5 → 1000 m için (2000)^(1/63) = 1.128, yani her
// dilim bir öncekinden %12.8 kalın. İlk 128 metreye 46 dilim düşüyor — Wronski'nin tüm
// hacmi 64 dilimle 128 m'ye yaydığı yerde biz o mesafeye 46 dilim koyuyoruz, yani
// menzil sekiz katına çıkarken yakın alan hassasiyeti düşmüyor.
//
// TERS Z TUZAĞI. Dağılım LİNEER GÖRÜŞ UZAYI derinliğinden kuruluyor, clip-space z'den
// değil — Unity çoğu platformda reversed-Z kullanıyor ve clip z ile kurulan bir üstel
// dağılım dilimleri ters uçta toplardı (spec §9.3, §12.7).
//
// x = near, y = far, z = log(far/near), w = dilim sayısı
float4 _FogVolumeDepth;

// Frustum köşe ışınları, her biri ileri eksene izdüşümü 1 olacak şekilde ölçekli:
// `worldPos = cameraPos + ray · viewDepth`. Normalize EDİLMİYOR — normalize edilseydi
// köşelerde derinlik merkeze göre uzardı ve dilimler küresel kabuk olurdu, oysa froxel
// düzlemsel dilim.
float4 _FogCornerRays[4];   // 00, 10, 01, 11 (sol-alt, sağ-alt, sol-üst, sağ-üst)

// x = dilime eklenen zamansal kayma [0,1), y/z/w boş. Wronski jitter'ı aliasing'i
// gürültüye takas etmek için öneriyor (spec §6.2); TAA deseni zaten dağıtıyor.
float4 _FogJitter;

// Kameranın ileri ekseni. Görüş uzayı derinliği bundan çıkıyor: `dot(ray, forward)`.
// Matristen türetmek yerine açıkça yazılıyor — `UNITY_MATRIX_V`'nin işaret düzeni
// platforma göre değişiyor ve sessizce ters dönen bir derinlik hacmi tamamen kaydırırdı.
float4 _FogCameraForward;


// Birikmiş saçılım hacmi. Compute onu RW olarak bildirdiği için orada bu blok kapalı;
// aynı isim iki farklı tipte bildirilirse derleyici çakışıyor.
#ifndef FOG_VOLUME_COMPUTE
TEXTURE3D(_FogScatteringVolume);
SAMPLER(sampler_FogScatteringVolume);
#endif

/// Dilim indeksinden lineer görüş uzayı derinliği. `s` [0,1] aralığında sürekli —
/// tam sayı dilim indeksi değil, çünkü jitter aradaki değerleri de istiyor.
float FogViewDepthFromSlice(float s)
{
    return _FogVolumeDepth.x * exp(_FogVolumeDepth.z * s);
}

/// Ters yön: derinlikten dilim koordinatı. Hacim dokusunu örneklerken gerekiyor.
/// `near`'ın altındaki derinlik sıfıra kırpılıyor — logaritma orada negatife gider ve
/// örnekleme dokunun dışına taşardı.
float FogSliceFromViewDepth(float viewDepth)
{
    return log(max(viewDepth, _FogVolumeDepth.x) / _FogVolumeDepth.x) / _FogVolumeDepth.z;
}

/// Hacim dokusunun örnekleme koordinatı: ekran uv + derinlikten türeyen dilim.
float3 FogVolumeUVW(float2 screenUV, float viewDepth)
{
    return float3(screenUV, saturate(FogSliceFromViewDepth(viewDepth)));
}

/// Froxel merkezinin dünya konumu. `uv` hücrenin ekran düzlemindeki merkezi,
/// `viewDepth` o hücrenin derinliği.
float3 FogFroxelWorldPos(float3 cameraPos, float2 uv, float viewDepth)
{
    float3 bottom = lerp(_FogCornerRays[0].xyz, _FogCornerRays[1].xyz, uv.x);
    float3 top    = lerp(_FogCornerRays[2].xyz, _FogCornerRays[3].xyz, uv.x);

    return cameraPos + lerp(bottom, top, uv.y) * viewDepth;
}

// ---------------------------------------------------------------------------

float _HeightFogDensity;   // yerleşik havanın taban kotundaki yoğunluğu
float _HeightFogFalloff;   // metre başına seyrelme katsayısı
float _HeightFogBase;      // yoğunluğun ölçüldüğü kot (metre)
float _FogInversionHeight; // sisin kesildiği kot: soğuk havanın tavanı
float _FogInversionWidth;  // o kesimin yumuşaklığı (metre)
// Serbest troposfer ÜÇÜNCÜ KATMAN. İnversiyonun üstü "kalıntı oran" ile modelleniyordu
// (`_FogAboveInversion`): sınır tabakasının kendi sığ profiliyle ÇARPILDIĞI için birkaç
// bin metrede sıfırlanıyor ve zirveden bakışta uzak sırtlar hiç puslanmıyordu — otuz
// kilometredeki sırt tam kontrastla, karton gibi. Havanın kendi molekülleri (Rayleigh)
// oradadır ve kendi ölçek yüksekliği vardır; ayrı katman olarak toplanır, çarpan değil.
// Hava olayları sınır tabakasında yaşadığı için bu katman yağıştan ETKİLENMEZ.
float _FogFreeDensity;     // serbest havanın taban kotundaki yoğunluğu
float _FogFreeFalloff;     // Rayleigh ölçek yüksekliğinden (çok daha yayvan)
// Vadi sis denizi AYRI KATMAN. Tek kanaldan geçiyordu: CPU onu 120 m'lik kendi
// profiliyle hesaplayıp `max()` ile yerleşik havanın yoğunluğuna katlıyor, shader ise
// 1400 m'lik profille yayıyordu. Sığ bir deniz bulut tabanına kadar tırmanıyor, yol
// boyunca optik derinlik on kat fazla çıkıyor ve şafakta bulutları siliyordu.
float _FogSeaDensity;      // denizin taban kotundaki yoğunluğu
float _FogSeaFalloff;      // denizin kendi seyrelme katsayısı (çok daha dik)
float3 _FogBankDrift;      // bank alanının rüzgârla birikmiş ötelemesi (metre)
float _FogBankStrength;    // bankların yoğunluğu ne kadar yerel oynattığı, 0-1

/// Verilen kottaki MUTLAK sis yoğunluğu. ÜÇ katman toplanır — hepsi kendi yarı
/// yüksekliğiyle. Ortak profile sıkıştırmak ya da birbirine çarpmak, bu dosyada üç
/// ayrı belirtinin kaynağı oldu; toplama yapıları gereği ayrık tutar.
///
/// SINIR TABAKASI: nem ve toz alçakta toplanır, sığ ve yağışla derinleşir. Üstüne
/// inversiyon biner: soğuk hava vadide hapsolur, üstünde sıcak hava durur ve ikisi
/// karışmaz. Sis o sınırda üstel olarak değil, neredeyse bıçakla kesilmiş gibi biter —
/// dağdan bakınca vadinin dolu, yukarısının pırıl pırıl olmasının sebebi budur.
///
/// Arazi yüksekliği. Rüzgârın kaldırdığı kar YERE yapışır; deniz seviyesine göre sönen
/// bir profil sırtın üstünde hiç görünmez, vadide ise boğar. Doku `SurfaceMapBaker`'da
/// pişiriliyor: 512 texel / 17.5 km = 34 metre, uzak katman için yeterli.
TEXTURE2D(_TerrainHeightMap);
SAMPLER(sampler_TerrainHeightMap);
float4 _TerrainHeightArea;   // xy köşe konumu, z genişlik, w yükseklik ölçeği

float TerrainHeightAt(float2 xz)
{
    float2 uv = (xz - _TerrainHeightArea.xy) / max(1.0, _TerrainHeightArea.z);
    return SAMPLE_TEXTURE2D_LOD(_TerrainHeightMap, sampler_TerrainHeightMap,
                                saturate(uv), 0).r * _TerrainHeightArea.w;
}

// Birikmiş taze kar, KOT EKSENİNDE. 128x1 doku: R örtü, G kalınlık deposu. Yüzey
// rengini de sürüklenen karı da bu belirliyor — yerde kar yoksa rüzgâr kaldıracak bir
// şey bulamaz. Sis dosyasında duruyor çünkü dünya durumu: yüzey de gökyüzü de okuyor.

/// SİS DENİZİ: gecenin ışınımsal soğumasıyla vadi dibinde biriken çok sığ katman —
/// yüz metrede biter. Ortak profille yayılınca bulut tabanına kadar tırmanıyor ve yolun
/// optik derinliğini on kata çıkarıyordu.
///
/// SERBEST TROPOSFER: havanın kendi molekülleri. Yayvan (Rayleigh ölçek yüksekliği) ve
/// yağıştan bağımsız. İnversiyon üstü bir "kalıntı oran" olarak modellenip sınır
/// tabakasının profiliyle çarpılıyordu; birkaç bin metrede sıfırlanıyor ve zirveden
/// bakışta otuz kilometredeki sırt tam kontrastla, karton gibi duruyordu.
float FogDensityAt(float height)
{

    float lid = 1.0 - smoothstep(_FogInversionHeight - _FogInversionWidth,
                                 _FogInversionHeight + _FogInversionWidth, height);

    float boundary = _HeightFogDensity * exp(-_HeightFogFalloff * height) * lid;

    // İkisinin de tavanı yok: biri inversiyonun çok altında biter, öteki çok üstüne çıkar.
    float sea = _FogSeaDensity * exp(-_FogSeaFalloff * height);
    float free = _FogFreeDensity * exp(-_FogFreeFalloff * height);

    return boundary + sea + free;
}

/// Sis bankları: yoğunluğu yerel çarpan alçak frekanslı alan. Gerçek dağ sisi üniform
/// bir çorba değildir — bank bank gezer: bir yamacı sarar, vadiye dil uzatır, iki
/// dakika sonra açılır. Alan rüzgârla sürüklenir. Dalga boyları yüzlerce metre.
///
/// AtmosphereController aynı formülü CPU'da örnekler (kuşak yamaları, görüş nefesi):
/// iki tüketici, tek alan — formül değişirse ikisi birlikte değişmeli.
float FogBankAt(float2 pos)
{

    float2 p = pos - _FogBankDrift.xz;

    // ÇARPIM DEĞİL TOPLAM. Eskiden iki sinüs ÇARPILIYORDU ve yorumu "iki farklı
    // frekansın çarpımı tekrar desenini kırar" diyordu — kırmıyor. `sin(k1·p)·sin(k2·p)`
    // ayrıştırılabilir bir ifadedir ve matematiksel olarak düzenli bir KAFES üretir;
    // frekans karıştırmak bunu değiştirmez. Belirti: gece, 3700 m'den aşağı bakınca
    // sisin üstünde çapraz ızgara. Ölçüldü — sis denetimi (ortam tek biçime zorlanınca)
    // ızgarayı yok ediyordu, hacim ve bulut yolu elenmişti, geriye bu alan kalmıştı.
    //
    // Rastgele bir alan modların ÜST ÜSTE BİNMESİDİR; spektral gürültünün tanımı budur.
    // Beş bileşen, yönleri paralel değil ve dalga boyları oransız — bileşke pratikte
    // tekrar etmiyor. Sinüs CPU ile GPU'da birebir aynı sonucu veriyor; hash tabanlı
    // gürültü vermezdi ve `AtmosphereController` aynı alanı CPU'da örneklemek zorunda.
    //
    // Dalga boyları 350-1700 m: sis bankı yüzlerce metre genişliğinde bir yapıdır.
    float s = sin(dot(p, float2( 0.003534,  0.001081))) * 0.34
            + sin(dot(p, float2( 0.001090,  0.005607))) * 0.26
            + sin(dot(p, float2(-0.005424,  0.006239))) * 0.20
            + sin(dot(p, float2(-0.011122, -0.004720))) * 0.13
            + sin(dot(p, float2( 0.005250, -0.017167))) * 0.07;

    float bank = saturate(0.5 + 0.5 * s);                 // 0..1, ortalama 0.5

    // Tam güçte 0.3-1.7 aralığı: bank sisi yerel olarak üçte birine indirir ama
    // hiç sıfırlamaz — sisli havada tamamen berrak delik gerçekdışı duruyor.
    return lerp(1.0, 0.3 + bank * 1.4, _FogBankStrength);
}

/// Yol boyunca bank çarpanı: üç örnek, öndeki bankla arkadaki ayrışsın diye.
/// Integral döngüsünün içinde değil — banklar yatayda yüzlerce metre genişken
/// sekiz kat gürültü maliyeti görünür, üç örnek yeter.
float FogBankPath(float2 fromXZ, float2 toXZ)
{
    float average = (FogBankAt(lerp(fromXZ, toXZ, 0.2))
                   + FogBankAt(lerp(fromXZ, toXZ, 0.5))
                   + FogBankAt(lerp(fromXZ, toXZ, 0.8))) / 3.0;

    // UZUN YOL ORTALAMAYA YAKINSAR. Alanın dalga boyu 350-1700 m; kilometrelerce yol
    // onlarca bankın içinden geçiyor ve gerçek ortalama alanın ortalamasına (çarpan 1)
    // oturuyor. Üç örnek o yakınsamayı üretemez, bakış yönüne göre dalgalanıp uzakta
    // desen bırakır — yakın mesafede doğru, uzakta yalan.
    //
    // Yakınsama yolun uzunluğuyla: birkaç yüz metrede bank yapısı tam görünür,
    // kilometrelerde söner. Sınır alanın kendi dalga boyundan geliyor, uydurma değil.
    float length2D = distance(fromXZ, toXZ);

    return lerp(1.0, average, exp(-length2D / 900.0));
}

#endif // TOTHESUMMIT_VOLUMETRIC_FOG_SHARED_INCLUDED
