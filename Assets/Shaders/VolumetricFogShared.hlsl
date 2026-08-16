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


// TEŞHİS ARAÇLARI — GEÇİCİ. Sis doğrulanınca bunlar ve F1'deki bölüm silinir.
// Silme tek adımda yapılmalı: bu globaller `HeightFog.hlsl`, `SkyFog.shader`,
// `VolumetricClouds.shader` ve `VolumetricFog.compute` içinde de okunuyor.
float _FogAudit;           // ortam tek biçim + tek renk (macenta): kapsama testi
float _FogLayerProbe;      // yeşil arazi · kırmızı gök · mavi bulut: KİM çiziyor
float _FogVolumeProbe;     // kırmızı geçirgenlik · yeşil saçılım: froxel hacmi dolu mu
float _FogSurfaceProbe;    // sis atlanır, yüzeyin ham luminansı logaritmik basılır
float _FogCloudsDisabled;  // bulut birleştirmesindeki sis uygulamasını kapatır

// Koschmieder: β = 3.912 / görüş. 40 m için 0.0978 /m.
static const float FogAuditExtinction = 0.0978;

// Ton eşleme parlaklığı oynatır ama TONU oynatmaz; okuma tona dayanıyor.
static const float3 FogAuditColor = float3(0.5, 0.0, 0.5);

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
float4 _SnowProfileRange;   // x taban kot, y aralık

TEXTURE2D(_SnowProfile);
SAMPLER(sampler_SnowProfile);

float2 SampleSnowProfile(float altitude)
{
    float t = saturate((altitude - _SnowProfileRange.x) / max(1.0, _SnowProfileRange.y));
    return SAMPLE_TEXTURE2D_LOD(_SnowProfile, sampler_SnowProfile, float2(t, 0.5), 0).rg;
}

float _SpindriftDensity;     // rüzgâr eşiği CPU'da uygulanmış hâliyle
float _SpindriftFalloff;     // 1/yarı-yükseklik

float4 _SpindriftCrest;      // x kret kaldırma katı, y kret yükselme katı
float4 _SpindriftDrift;      // xz taşınan alan kayması (metre)
float4 _SpindriftWind;       // xz birim yön, w şiddet

/// SÜRÜKLENEN KAR (spindrift): rüzgâr eşiği aşınca yerdeki gevşek kar havalanır ve
/// yüzeye yapışık, sığ, hızlı bir perde oluşturur. Sırtın rüzgâr üstü yüzü kazınır,
/// arkasına yığılır; uzaktan bakınca sırttan savrulan duman gibi okunur.
///
/// Dördüncü sis katmanı olarak duruyor, ayrı bir tanecik sistemi değil: sıfır ek çizim,
/// ve güneş rengini sisin okuduğu yerden alıyor — ayrı bir renk kaynağı kurulmuyor.
///
/// İki koşul birden: RÜZGÂR eşiği aşacak (CPU'da hesaplanıp `_SpindriftDensity`'ye
/// gömülü) ve YERDE gevşek kar olacak. İkincisi kot profilinden okunuyor — yıllanmış
/// buzul sürüklenmez, taze toz sürüklenir.
///
/// Yükseklik YERDEN ölçülüyor. Deniz seviyesine göre sönen bir profil sırtın üstünde
/// hiç görünmez, vadide ise boğardı.
/// Sürüklenen karın AKAN yapısı. Tekdüze bir perde renk değiştirir ama hareket
/// etmez — göz onu sis sanır. Gerçek spindrift şerit şerit akar: alan rüzgârla
/// taşınıyor ve dalga boyu yüz metre mertebesinde, sis banklarından çok daha ince.
///
/// Alan rüzgâr hızıyla kayıyor (`_SpindriftDrift` CPU'da biriktiriliyor). Bank sisiyle
/// aynı yapıda ama on kat hızlı: bank dakikalar ölçeğinde gezer, sürüklenen kar
/// saniyeler ölçeğinde.
/// Perdenin kütle dağılımı — İNCE YAPI DEĞİL. Işın 8 adımda integre ediliyor; bu
/// sayıda örnekle taranabilecek en küçük özellik yüzlerce metre. Dalga boyu 70 metreye
/// indirildiğinde örnekler kamera oynadıkça zıpladı ve perdenin içinde yağmur yağıyor
/// gibi bir titreme çıktı — ders kitabı undersampling. Literatürdeki çözümü temporal
/// reprojection + blue noise + TAA; bizde TAA yok (bkz. DECISIONS.md).
///
/// Bu yüzden uzak katman PÜRÜZSÜZ kalıyor: kütleyi, rengi ve sönümü o taşıyor.
/// Şerit şerit akan hareket yakın tanecik katmanının işi — yanlış sistemden istendi.
/// Perdenin akan yapısı. Dalga boyu ~150 metre: 12 m/s rüzgârda bir desen on saniyede
/// geçiyor, yani hareket gözle görülüyor. 1570 metredeyken 130 saniye sürüyordu ve
/// perde duruyormuş gibi okunuyordu.
///
/// Bu ölçek ancak perde terimi KENDİ adımlarıyla tarandığı için mümkün (bkz.
/// `HeightFogIntegral`): sisin sekiz adımıyla taranınca örnekler desenin üstünden
/// atlıyor ve perdenin içinde yağmur yağıyormuş gibi bir titreme çıkıyordu.
///
/// İkinci oktav, oranı tam sayı DEĞİL: tek desen düzenli okunuyor, kapanmayan iki eğri
/// hiç aynı şekli tekrar etmiyor.
float SpindriftFlow(float2 xz)
{
    // ÇARPIM DEĞİL TOPLAM. Gerekçe `FogBankAt` ile birebir aynı: `sin(k1·p)·sin(k2·p)`
    // ayrıştırılabilir bir ifadedir ve düzenli bir KAFES üretir. Bankta düzeltilmişti,
    // burası atlanmıştı — aynı sınıfın ikinci kullanım yeri.
    //
    // Belirti: rüzgâr arttıkça yukarı uzanan, titreyen dikey şeritler. Ölçüldü —
    // rüzgâr 0.10'a çekilince kayboluyor, yani kaynak perdenin akış alanı.
    //
    // İKİ SÜRÜKLENME HIZI KORUNUYOR: katmanlar arası paralaks perdeye akış hissini
    // veriyor; tek hızda desen blok hâlinde kayıyor. Dalga boyları 114-646 m,
    // yönleri paralel değil, oranları tam sayı değil — bileşke tekrar etmiyor.
    float2 p = xz - _SpindriftDrift.xz;
    float2 q = xz - _SpindriftDrift.xz * 1.4;

    float s = sin(dot(p, float2( 0.02840,  0.00869))) * 0.30
            + sin(dot(p, float2(-0.01330,  0.03760))) * 0.24
            + sin(dot(p, float2( 0.05100, -0.02100))) * 0.14
            + sin(dot(q, float2( 0.00930,  0.00284))) * 0.20
            + sin(dot(q, float2(-0.00435,  0.01231))) * 0.12;

    return lerp(0.25, 1.75, saturate(0.5 + 0.5 * s));
}

float SpindriftAt(float3 pos)
{
    // DENETİM: perde kapalı — kendi nötr rengi macentayı beyaza çekerdi.
    if (_FogAudit > 0.5) return 0.0;


    if (_SpindriftDensity <= 0.0) return 0.0;

    float ground = TerrainHeightAt(pos.xz);
    float above = pos.y - ground;
    if (above < 0.0) return 0.0;

    // Rüzgâr ekseninde üç örnek daha: bir örnek "neredeyiz" sorusunu cevaplayamıyor,
    // dizi arazinin o eksendeki BİÇİMİNİ veriyor.
    float2 step = _SpindriftWind.xz * 150.0;
    float upwind = TerrainHeightAt(pos.xz - step);
    float downwind = TerrainHeightAt(pos.xz + step);
    float far = TerrainHeightAt(pos.xz - step * 2.0);

    // SIRT ARKASINDA YIĞILIR. Rüzgâr üstündeki arazi bizden yüksekse rüzgâr altında
    // kalmışız demektir; tepeyi aşan kar oradaki durgun bölgeye çöker.
    float lee = saturate((upwind - ground) / 80.0);

    // KRETTEN FIŞKIRIR. Spindrift yamacın tamamından değil sırtın kendisinden kalkar:
    // rüzgâr tepeyi aşarken hızlanır, gevşek karı havaya fırlatır. Kret, iki yanı da
    // kendisinden alçak olan nokta.
    float crest = saturate((ground - max(upwind, downwind)) / 60.0);

    // TÜY RÜZGÂR ALTINA UZANIR. Kretten kalkan kar orada asılı kalmıyor, rüzgârla
    // taşınıp sırtın arkasına bir kuyruk bırakıyor — "savrulan duman" görüntüsünün
    // asıl kaynağı o kuyruk. Etki kretin çevresinde simetrik kaldığı sürece hiç
    // oluşmuyordu.
    //
    // Kuyruk, RÜZGÂR ÜSTÜNDEKİ noktanın kret olup olmadığından okunuyor: `upwind`'in
    // iki komşusu zaten elimizde (`ground` ve `far`), yani tek ek örnekle o noktanın
    // kret testi yapılabiliyor. Böylece sırtın arkasındaki her nokta "yukarıda kret
    // var" deyip tüyü devralıyor.
    float tail = saturate((upwind - max(ground, far)) / 60.0);
    float plume = max(crest, tail * 0.8);

    // TÜY YÜKSELİR. Tüyün olduğu yerde katman kalınlaşıyor: sönüm zayıflayınca kar
    // yukarı fışkırıyor, kuyruk bitince tekrar yere yapışıyor.
    float falloff = _SpindriftFalloff / lerp(1.0, _SpindriftCrest.y, plume);

    // DİKEY PROFİL KUVVET YASASI. Süspansiyon üstel değil Rouse tipi dağılır: dipte
    // yoğun, yukarı doğru UZUN kuyruk. Üstel sönüm kuyruğu çok erken bitiriyordu ve
    // tüyler kısa kalıyordu — kret yükseltmesini dört kata çıkarmak zorunda kalmamın
    // sebebi buydu, yanlış profili katsayıyla telafi ediyordum.
    float h = above * falloff;
    float vertical = 1.0 / (1.0 + h * h);

    return _SpindriftDensity * SampleSnowProfile(ground).r
         * SpindriftFlow(pos.xz)
         * lerp(0.85, 1.6, lee) * lerp(1.0, _SpindriftCrest.x, plume)
         * vertical;
}

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
    // DENETİM: ortam TEK BİÇİMLİ. Görüş 40 m, her kotta aynı.
    if (_FogAudit > 0.5) return FogAuditExtinction;


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
    // DENETİM: bank yok — yoğunluğu yerel oynatması tek biçimliliği bozardı.
    if (_FogAudit > 0.5) return 1.0;


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
