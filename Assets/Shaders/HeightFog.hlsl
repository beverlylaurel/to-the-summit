#ifndef TOTHESUMMIT_HEIGHT_FOG_INCLUDED
#define TOTHESUMMIT_HEIGHT_FOG_INCLUDED

// Yükseklik sisi. Havanın kendi hesabı — hangi yüzeyin çizildiğinden bağımsız.
//
// Unity'nin sisi yükseklikten bağımsız: zirvede de etekte de aynı yoğunlukta kalıyor ve
// tırmanmanın görsel karşılığı doğmuyordu. Bu yüzden hiç kullanılmıyor, sönüm burada
// hesaplanıyor.
//
// Ayrı dosyada duruyor çünkü tek bir yüzeyin özelliği değil: dağ, kaya, props, ne
// çizilirse çizilsin aynı havanın içinde duruyor. Yüzey shader'ının içinde kalırsa
// ikinci bir yüzey geldiğinde ya kopyalanır ya da o yüzey sissiz kalır — ikisi de
// aynı havada iki farklı görünürlük demek.
//
// Sabit tamponun dışında: AtmosphereController bunları global olarak yazıyor.
float4 _HeightFogColor;
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
float4 _HeightFogShadowColor; // güneşin karşı ufku: Dünya'nın gölgesi, gökyüzüyle ortak
float4 _HeightFogZenith;   // göğün tepe rengi: ışın dikleştikçe hava buna kararır
float4 _HeightFogSunColor; // gök, güneş yönünde, ufkun 2° üstü
float3 _HeightFogChroma;   // kanal başına sönüm çarpanı: berrakta Rayleigh, siste nötr
float3 _SunDirection;      // AtmosphereController global yazar; gökyüzü ve bulut da okur

// Şimşek: LightningFlash yazar, bulut ve gökyüzü de aynı değeri okur.
float4 _LightningFlash;

/// Sisin çakmadan aldığı pay. Bulut kadar almaz: bulut kütlesi kilometrelerce derin ve
/// çakma onun içinde, sis ise ince ve boşalmanın altında kalıyor.
static const float LightningFogScatter = 0.6;

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

// TimeOfDay yayınlıyor. Burada bildiriliyor çünkü sis dosyası yüzeyden ÖNCE include
// ediliyor. İkinci bir isim uydurulmuştu; o global gelmediğinde değer sıfır kalıyor,
// perde "güneş alçak" sanılıp ham gök mavisine boyanıyordu.
float _SunHeight;
float _SpindriftDensity;     // rüzgâr eşiği CPU'da uygulanmış hâliyle
float _SpindriftFalloff;     // 1/yarı-yükseklik
float _SpindriftBrightness;  // perdenin kendi parlaklığı, gök luminansı çarpanı
float _SpindriftMaxDepth;    // perdenin optik derinlik tavanı
float4 _SpindriftCrest;      // x kret kaldırma katı, y kret yükselme katı
float4 _SpindriftDrift;      // xz taşınan alan kayması (metre)
float4 _SpindriftWind;       // xz birim yön, w şiddet

float TerrainHeightAt(float2 xz)
{
    float2 uv = (xz - _TerrainHeightArea.xy) / max(1.0, _TerrainHeightArea.z);
    return SAMPLE_TEXTURE2D_LOD(_TerrainHeightMap, sampler_TerrainHeightMap,
                                saturate(uv), 0).r * _TerrainHeightArea.w;
}

// Hava haritası: bulutların NEREDE olduğu. Bulut shader'ı da aynı dokuyu okur —
// gölgenin bulutla uyuşması için ikisinin aynı kaynaktan beslenmesi şart.
TEXTURE2D(_WeatherMap);
SAMPLER(sampler_WeatherMap);
float _WeatherMapScale;    // 1 / harita periyodu (metre)
float3 _CloudWind;         // bulutların birikmiş sürüklenmesi (metre)
float _CloudBottom;        // katmanın tabanı (metre)
float _Coverage;           // o anki bulut kapsaması

float _WeatherMapTexels;   // haritanin texel sayisi; mip secimi icin gerekli

// BULUT YER IZININ ORTAK KAYNAGI. Bu bildirimler ve asagidaki fonksiyon eskiden
// CloudCommon.hlsl'deydi, yani yalniz GOKYUZU onlari goruyordu. Yer golgesi kendi
// yaklasimini kuruyordu: warp'siz, evrimsiz, firtina dolgusuz. Iki alan hicbir zaman
// tutmadi - gokte bulut olmayan yerde golge, golge olmayan yerde bulut. Katsayi
// ayarlamak bunu duzeltmez; tek kaynak duzeltir.
TEXTURE3D(_BaseNoise);
SAMPLER(sampler_BaseNoise);
float _BaseNoiseTexels;
float _CloudScale;
float _Evolution;
float3 _CloudShearOffset;   // ust katmanlarin yanal kaymasi (metre)

/// Kapsamanin cikabilecegi tavan. Bire dayanmasi esigi sifirliyor ve kapali havayi
/// dalgasiz bir levhaya ceviriyor; kucuk bir pay birakmak tabanin kivrimini koruyor.
static const float CloudCoverageCeiling = 0.85;

/// Ornekler arasi mesafeye gore mip seviyesi. Bir texel adim boyundan kucuk kaldiginda
/// o doku artik orneklenemez ve okunan deger aliasing olarak geri gelir. Dokular
/// mipmap'li; cozulemeyen frekansi okumak yerine donanimin ortalamasini okuruz.
///
/// Bir texel'in dunya karsiligi 1 / (olcek x texel sayisi) metredir. Taban ve detay
/// dokularinin cozunurlugu farkli oldugu icin texel sayisi disaridan verilir.
float _CloudLodLock;   // TESHIS: 1 iken mip seviyesi sifira kilitleniyor

float CloudSampleLod(float stepSize, float scale, float texels)
{
    // TESHIS ANAHTARI. Bulutun icinde esmerkezli soganlar goruluyor ve iki aday var:
    // adim kabuklari ile mip gecisleri. Mip sifira kilitlendiginde halkalar kayboluyorsa
    // sucu mip gecisi, duruyorsa adimlama. Anahtar kapaliyken hesap aynen isliyor.
    float lod = max(0.0, log2(max(1e-6, stepSize * scale * texels)));
    return lerp(lod, 0.0, saturate(_CloudLodLock));
}

/// Bir kolonun hava durumu: bulut govdesi de yer golgesi de bundan turer.
struct CloudFootprint
{
    float4 colWarp;    // kolonsal alan; warp, dalga ve cephe nefesi bunu okur
    float4 weather;    // haritanin ham okumasi: g tip, b taban kaymasi, a tavan
    float coverage;    // esiklenmis yerel kapsama, 0 - CloudCoverageCeiling
};

/// stormFill: firtina dolgusunun payi. horizonBias: uzakta gogu kapatma payi - YER
/// golgesi icin sifir, o duzeltme isinin ufka teget gecmesiyle ilgili.
/// colLod / mapLod: ornekleme ayak izinden gelen mip. Gokyuzu adim boyundan hesaplar,
/// golge pikselin dunya genisliginden.
CloudFootprint CloudFootprintAt(float2 mapPos, float stormFill, float horizonBias,
                                float colLod, float mapLod)
{
    float4 colWarp = SAMPLE_TEXTURE3D_LOD(_BaseNoise, sampler_BaseNoise,
                                          float3(mapPos.x, 87.3 + _Evolution * 26.0, mapPos.y)
                                          * (_CloudScale * 0.5),
                                          colLod);
    // Yalnız pürüzsüz kanallar: r (perlin ağırlıklı, ~1.3 km) ve g (worley-6,
    // ~1.1 km). b/a kanalları bu ölçekte de 300-550 m'lik hücreler taşıyor —
    // vektöre girseler katlanma geri gelir.
    float2 windFlat = normalize(_CloudShearOffset.xz + float2(0.001, 0.0));
    float2 mapWarp = float2(colWarp.g - 0.5, colWarp.r - 0.5) * 650.0
                   + windFlat * saturate(colWarp.r - 0.45) * 1000.0;

    // HAVA KOLONSALDIR: kapsama, tip, tepe kotu ve taban kayması bir kolonun TAMAMI
    // için tek değerdir — 2B harita bunu yapısal olarak garanti eder. Harita editörde
    // pişirilir (CloudWeatherMapBaker): çekirdek-birleşim dağılımı, bulut başına
    // kimlik ve eğim garantili tavan orada kurulur; burada tek doku okuması kalır.
    float4 w = SAMPLE_TEXTURE2D_LOD(_WeatherMap, sampler_WeatherMap,
                                    (mapPos + mapWarp) * _WeatherMapScale,
                                    mapLod);

    // Fırtına dolgusu: kapsama arttıkça haritanın boşlukları da dolar — kapalı gök
    // gerçek bir örtüdür, delikli bir kolaj değil. Taban SABİT DEĞİL, tavan kanalından
    // varyanslı: sabit taban (0.75) her yeri doyuruyor, eşik düzleşiyor ve örtü tek
    // parça halıya dönüyordu — doyma sınırında gezen dolgu örtüyü yamalı bırakır.
    // Dolgu 0.45'te başlar, 0.85'te tam: harita kendi kapsamasını ~0.5'te tüketiyor,
    // geç başlayan dolgu 0.5-0.7 arasında ÖLÜ PLATO bırakıyordu — boşluklarda hiç
    // bulut yok, sonra 0.7'de pat diye katman. Erken başlamak güvenli çünkü dolgu
    // artık DESENLİ (aşağıda): tutkal değil, ayrık bulut serper.
    // Dolgu geç başlar (0.55): erken başlangıç "ölü plato" içindi; harita artık
    // orta kapsamayı kendisi taşıyor (yoğun istif kalibrasyonu) ve erken dolgu
    // %70'te göğü %100 gibi kapatıyordu.
    float p = w.r;

    // Dolgu DÜZ TABAN değil, kolon gürültüsü DESENLİ. Düz taban (0.45+0.35A)
    // iki fazlı yapaylık üretiyordu: %70'e kadar boşluklar bomboş, sonra pat diye
    // ince bir tabaka yükseliyordu. Desenli dolgu boşluklara kapsamayla birlikte
    // sayısı ve boyu büyüyen ~1 km'lik YENİ bulutlar serper — gökyüzü büyüyerek
    // kapanır, ayrı bir çarşaf belirmez.
    // Desen kaynağı PÜRÜZSÜZ kanal (colWarp.g, ~1.1 km): colBump'ın perlin-worley
    // karışımı onlarca metrelik iç frekans taşıyor — kolon-sabit olduğu için
    // yoğunluğa dikeyde hiç değişmeyen dev DİKEY PERDELER basıyordu (yakından
    // kocaman soğan halkaları).
    p = lerp(p, max(p, 0.06 + 0.70 * colWarp.g), stormFill);

    // Kapsamanın bire dayanmasına izin verilmez: eşik sıfırlanınca şekil gürültüsünün
    // tamamı buluta dönüşüyor ve kapalı hava dümdüz kâğıt oluyordu.
    // Kazanç 1.8 — 2.4 iç bölgeyi doyuruyordu: eşik düzleşince şekil gürültüsü gövdeyi
    // oyamıyor ve bulut çıplak çekirdek geometrisi olarak (silindir) çiziliyordu.
    // Kapsama-eşik bağı DOĞRUSAL ve sade tutulur. İki ders: alt-doğrusal üs köprü
    // dokusunu eşiğin üstüne itip yamaları kıtaya kaynattı; çekirdek vurgusu da
    // seyrek bulutların dengesini bozdu. Dev kütle sorunu buranın işi değil —
    // haritanın yama içi doluluğunun işi (pişiricide parçalama alanı).
    // Cephe nefesi: kolonsal alanın (zamanla ilerleyen) bir kanalı kapsamayı yavaşça
    // güçlendirip zayıflatır — bulut kümeleri yerinde büyür ve dağılır.
    p *= 1.20 + 0.80 * colWarp.b;
    p = lerp(p, max(p, 0.34), horizonBias);

    float localCoverage = saturate(p * _Coverage * 1.8) * CloudCoverageCeiling;

    CloudFootprint fp;
    fp.colWarp = colWarp;
    fp.weather = w;
    fp.coverage = localCoverage;
    return fp;
}


/// BULUT GÖLGESİ. Bulutlar tepede geziyordu ama yer bunu bilmiyordu: ışık sabit
/// kaldığı sürece yamaç hep aynı okunuyor. Dağ manzarasının imzası, yamaçlar boyunca
/// süzülen gölgelerdir.
///
/// Gölge güneşe doğru GERİ İZLENİR: yüzeyden bulut tabanına kadar olan yükseklik,
/// güneşin eğimiyle yatay bir kaymaya dönüşür. Bu yapılmazsa gölge bulutun tam altında
/// kalır ve güneş alçakken manzarayla uyuşmaz.
///
/// Kaynak bulutun kendi hava haritası ve kendi sürüklenmesi; ayrı bir gürültü kurulmuyor.
float CloudShadowAt(float3 worldPos)
{
    if (_Coverage <= 0.001) return 1.0;

    // Gunes ufkun altindaysa golge diye bir sey yok; ay isigi golge basacak kadar
    // guclu degil.
    float sunUp = saturate(_SunDirection.y * 6.0);
    if (sunUp <= 0.001) return 1.0;

    float rise = max(0.0, _CloudBottom - worldPos.y);
    float2 slide = _SunDirection.xz / max(0.15, _SunDirection.y) * rise;
    float2 mapPos = worldPos.xz + slide + _CloudWind.xz * 0.72;

    // Ornekleme ayak izi: uzaktaki bir piksel yerde metrelerce genisligi kapsar. LOD 0
    // okunursa cozulemeyen frekans kayniyor. 0.0012 ~ 1080p / 60 derece yatay icin
    // piksel basina aci; mesafeyle carpilinca pikselin dunyadaki genisligi cikar.
    float footprint = max(1.0, distance(worldPos, _WorldSpaceCameraPos) * 0.0012);

    // GOLGE, BULUTUN KENDI YER IZI. Gokyuzu hangi fonksiyondan yogunluk aliyorsa yer de
    // ondan golge aliyor; ikinci bir alan yok. horizonBias sifir: o duzeltme isinin
    // ufka teget gecmesiyle ilgili, yerdeki golgeyle degil.
    CloudFootprint fp = CloudFootprintAt(mapPos,
        smoothstep(0.55, 0.95, _Coverage), 0.0,
        CloudSampleLod(footprint, _CloudScale * 0.5, _BaseNoiseTexels),
        CloudSampleLod(footprint, _WeatherMapScale, _WeatherMapTexels));

    float shade = saturate(fp.coverage / CloudCoverageCeiling);

    // Tam karanlik olmuyor: bulut golgesindeki yuzey gokten hala isik aliyor. Kapali
    // havada zaten her yer golgede, orada da kontrast dusuk olmali.
    return 1.0 - shade * 0.55 * sunUp;
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
    float2 p = (xz - _SpindriftDrift.xz) * 0.042;
    float a = sin(p.x + sin(p.y * 1.7)) * sin(p.y * 0.8 - p.x * 0.6);

    float2 q = (xz - _SpindriftDrift.xz * 1.4) * 0.011;
    float b = sin(q.x * 1.3 - q.y * 0.9) * sin(q.y * 1.1 + q.x * 0.4);

    return lerp(0.25, 1.75, saturate(0.5 + a * 0.32 + b * 0.26));
}

float SpindriftAt(float3 pos)
{
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
/// dakika sonra açılır. Alan rüzgârla sürüklenir; iki farklı frekansın çarpımı tekrar
/// desenini kırar. Dalga boyları yüzlerce metre.
///
/// AtmosphereController aynı formülü CPU'da örnekler (kuşak yamaları, görüş nefesi):
/// iki tüketici, tek alan — formül değişirse ikisi birlikte değişmeli.
float FogBankAt(float2 pos)
{
    float2 p = pos - _FogBankDrift.xz;
    float a = sin(dot(p, float2(0.0093, 0.0071))) * sin(dot(p, float2(-0.0052, 0.0087)));
    float b = sin(dot(p, float2(0.0031, -0.0024)));
    float bank = 0.5 + a * 0.35 + b * 0.15;               // 0..1, ortalama 0.5

    // Tam güçte 0.3-1.7 aralığı: bank sisi yerel olarak üçte birine indirir ama
    // hiç sıfırlamaz — sisli havada tamamen berrak delik gerçekdışı duruyor.
    return lerp(1.0, 0.3 + bank * 1.4, _FogBankStrength);
}

/// Yol boyunca bank çarpanı: üç örnek, öndeki bankla arkadaki ayrışsın diye.
/// Integral döngüsünün içinde değil — banklar yatayda yüzlerce metre genişken
/// sekiz kat gürültü maliyeti görünür, üç örnek yeter.
float FogBankPath(float2 fromXZ, float2 toXZ)
{
    return (FogBankAt(lerp(fromXZ, toXZ, 0.2))
          + FogBankAt(lerp(fromXZ, toXZ, 0.5))
          + FogBankAt(lerp(fromXZ, toXZ, 0.8))) / 3.0;
}

/// Yükseklik sisi: ışının kat ettiği yol boyunca yoğunluk integrali.
///
/// Sabit yoğunluklu sis bunu yapamaz — zirveden vadiye bakarken de vadiden zirveye
/// bakarken de aynı miktarı uygular, oysa ilkinde ışın yoğun katmana giriyor,
/// ikincisinde ondan çıkıyor.
///
/// İnversiyon tavanı profili keskinleştirdiği için integralin kapalı çözümü kalmıyor;
/// yol boyunca birkaç örnek alınıyor. Sekiz örnek, tavanın kestiği yeri gözle görülür
/// bir basamak bırakmadan yakalıyor.
/// Işın boyunca yoğunluk integrali, bank çarpanı olmadan. Bankı çağıran seçer:
/// arazi yol boyunca örnekler, bulut peçesi yalnız kameranın yerelinden okur.
/// Sis ve sürüklenen kar TEK TARAMADA. İkisi ayrı döngüdeyken aynı ışın, aynı `t`
/// değerlerinde iki kez taranıyordu — sonuç birebir aynı, maliyet iki katı.
/// Sürüklenen kar ayrı dönüyor çünkü rengi ve sönüm eğrisi sisinkinden farklı.
float HeightFogIntegral(float3 cameraPos, float3 worldPos, out float drift)
{
    drift = 0.0;

    float3 ray = worldPos - cameraPos;
    float distance = length(ray);

    bool hasFog = _HeightFogDensity > 0.0 || _FogSeaDensity > 0.0 || _FogFreeDensity > 0.0;
    bool hasDrift = _SpindriftDensity > 0.0;

    if (distance < 0.01 || (!hasFog && !hasDrift)) return 0.0;

    const int Steps = 8;

    float startHeight = cameraPos.y - _HeightFogBase;
    float endHeight = worldPos.y - _HeightFogBase;

    float sum = 0.0;
    float driftSum = 0.0;

    // PERDE KENDİ ADIMLARIYLA TARANIYOR. Sisin profili yayvan ve pürüzsüz, sekiz adım
    // fazlasıyla yetiyor. Perde ise yere yapışık, ince ve akan bir yapı taşıyor —
    // aynı sekiz adım desenin üstünden atlıyor ve titreme bırakıyordu. Adım sayısı
    // yalnız perde etkinken ve yalnız perde terimi için ikiye katlanıyor.
    const int DriftSubSteps = 2;

    [unroll]
    for (int i = 0; i < Steps; i++)
    {
        float t = (i + 0.5) / Steps;
        sum += FogDensityAt(lerp(startHeight, endHeight, t));

        if (hasDrift)
        {
            [unroll]
            for (int k = 0; k < DriftSubSteps; k++)
            {
                float s = (i + (k + 0.5) / DriftSubSteps) / Steps;
                driftSum += SpindriftAt(lerp(cameraPos, worldPos, s)) / DriftSubSteps;
            }
        }
    }

    // PERDE DOYUMA GİTMEZ ama YAPISINI DA KAYBETMEZ. Sert kırpma (`min`) uzakta bütün
    // değerleri aynı tavana yapıştırıyor ve akış deseninin kontrastı siliniyordu:
    // "bembeyaz olmasın" isteğiyle "yapısı görünsün" isteği tek sayıda çarpışıyordu.
    //
    // Yumuşak doyum tavana asla varmıyor, yalnız yaklaşıyor — iki katı derinlik hâlâ
    // daha koyu çıkıyor, yani desen uzakta da okunuyor.
    float raw = distance * driftSum / Steps;
    drift = _SpindriftMaxDepth * raw / (raw + _SpindriftMaxDepth);

    // `FogDensityAt` artık mutlak yoğunluk veriyor: dışarıda ikinci bir çarpım yok,
    // yoksa iki katmandan biri iki kez ölçeklenirdi.
    return distance * sum / Steps;
}

/// Havada asılı karın rengi. Gökyüzü rengiNDEN türemiyor: `AirColor` bakış yönüne bağlı
/// ve aşağı bakarken neredeyse siyah dönüyor — sis için doğru (vadiye bakınca sis koyu
/// okunur), havada asılı kar için yanlış. Savrulan kar yukarıdan güneşle ve altındaki
/// karın yansımasıyla aydınlanır; hangi yöne bakıldığı onu karartmaz.
///
/// Kaynak ufkun hemen üstündeki gök rengi — zaten sisin okuduğu ışık, ayrı bir kaynak
/// kurulmuyor. Doygunluğu alınıyor (kristal dalga boyu seçmez) ve yukarı ölçekleniyor
/// (kar albedosu havanın saçılmasından yüksek).
float3 SpindriftColor()
{
    // Kristal DALGA BOYU SEÇMEZ: rengi kendinden değil, üstüne düşen ışıktan gelir.
    // Şafakta savrulan kar kızıl olmalı — tam doygunluk alınırsa asla olmaz.
    //
    // Ama gündüz de ufuk göğünün rengini alamaz: güneş tepedeyken perdeyi aydınlatan
    // şey tek bir yön değil, gök kubbenin tamamı — sonuç nötr beyazdır. Ufuk mavisini
    // taşımak yamacı fosforlu maviye çeviriyordu.
    //
    // Ayrım güneşin yüksekliğinden: alçakken ışık yönlü ve renkli, yükseldikçe dağınık
    // ve nötr.
    float3 light = _HeightFogSunColor.rgb;
    float luma = dot(light, float3(0.2126, 0.7152, 0.0722));

    float lowSun = 1.0 - smoothstep(0.02, 0.28, _SunHeight);

    // PARLAKLIK AYRI BİR KATSAYI. Ufuk göğünün luminansı kapalı havada karın kendisinden
    // düşük; perde olduğu gibi kullanılınca parlak beyazın üstüne KOYU nötr bir film
    // oturuyor ve göz onu mavi-gri okuyor (eşzamanlı kontrast). Oysa savrulan kar aynı
    // kardır, aynı ışıkla aydınlanır — zeminden koyu olamaz.
    // KATSAYI IŞIK ZAYIFKEN ÇALIŞIR. İşi perdeyi "kardan koyu" bandından çıkarmak;
    // ışık zaten parlakken yükseltilecek bir şey yok. Sabit çarpanla gündüz taban
    // luminansı 1'i aşıp beyaza kırpılıyordu — dağ akşamüstü fosforlu görünüyordu.
    // RENK ve PARLAKLIK AYRI. `_HeightFogSunColor` ham gök ışıması × sahne kazancı,
    // yani HDR ve ÜST SINIRI YOK — üstelik en büyük olduğu yer tam olarak güneş
    // yönündeki ufuk, yani şafak ve akşamüstü. Olduğu gibi renk olarak geçirilince
    // 1'i aşıyor ve beyaza kırpılıyordu: dağın fosforlu görünmesi buydu. Katsayıyı
    // kısmak yetmez, tavan gerekir.
    //
    // Tavan fiziksel: perde kardır, en fazla tam aydınlanmış kar kadar parlak olabilir.
    float3 hue = light / max(1e-4, luma);
    float3 tinted = lerp(1.0, hue, lowSun * 0.9);

    // ZEMİNE ULAŞAN IŞIK, ufuk parıltısı değil. Yamaç şafakta hâlâ gölgedeyken perde
    // ufkun parlaklığını alırsa dağ aydınlatılmış görünüyor. Güneşin yüksekliği
    // aydınlanmanın gerçek ölçüsü.
    float dayLevel = saturate(_SunHeight * 3.5);
    float gain = lerp(_SpindriftBrightness, 1.0, saturate(luma));

    float level = saturate(luma * gain) * lerp(0.3, 1.0, dayLevel);
    return tinted * level;
}

float HeightFogAmount(float3 cameraPos, float3 worldPos)
{
    float drift;
    float integral = HeightFogIntegral(cameraPos, worldPos, drift)
                   * FogBankPath(cameraPos.xz, worldPos.xz);
    return saturate(1.0 - exp(-integral));
}

/// Gökyüzüne giden ışının sis OPTİK DERİNLİĞİ. Arazi yolu sonlu ve örnekle integre
/// ediliyor; gök yolu sonsuz — her katmanın üstel profili kapalı biçimde integre
/// edilir. Gök sislenmeden atmosfer tek olmuyordu: sis yalnız araziye uygulanınca
/// çorbanın içinde yukarı bakan oyuncu yıldız görüyordu, banklar da önlerinde arazi
/// yokken hiç çizilmiyordu.
///
/// Metre yerine derinlik döndürüyor: üç katmanın yoğunlukları farklı, tek bir "yol"
/// sayısını dışarıda tek bir yoğunlukla çarpmak üçünü birden temsil edemez.
/// `maxPath` her katmanın KENDİ yoluna ayrı ayrı uygulanır — güneş kadranı sonsuz
/// yolla söndürülmesin diye (bkz. Sky.shader).
float SkyFogDepth(float3 cameraPos, float3 dir, float maxPath)
{
    float h0 = cameraPos.y - _HeightFogBase;

    // Ufka inen ışın: eğim sıfıra dayandıkça yol yatay kapasiteye oturur (~100 km
    // eşdeğeri). Ufuk her havada hava rengine doyar — gerçekte de öyle.
    float s = max(dir.y, 0.02);

    float k = _HeightFogFalloff;

    // Sınır tabakası inversiyonda BİTER: üstünde kalan pay artık serbest katmanın işi.
    float boundaryPath = h0 < _FogInversionHeight
        ? (exp(-k * h0) - exp(-k * _FogInversionHeight)) / (k * s)
        : 0.0;

    // İkisinin de tavanı yok; profilleri kendileri bitiriyor.
    float seaPath = exp(-_FogSeaFalloff * max(0.0, h0)) / (_FogSeaFalloff * s);
    float freePath = exp(-_FogFreeFalloff * max(0.0, h0)) / (_FogFreeFalloff * s);

    // Sürüklenen kar yalnız kameranın çevresinden okunuyor: katman araziye yapışık ve
    // arazi yükseklik alanı ışın boyunca kapalı biçimde integre edilemez. Göğe giden
    // ışın zaten katmanı birkaç on metrede terk ediyor; uzaktaki sırtın perdesi arazi
    // yolunda hesaplanıyor.
    float sky = _HeightFogDensity * min(boundaryPath, maxPath)
              + _FogSeaDensity * min(seaPath, maxPath)
              + _FogFreeDensity * min(freePath, maxPath);

    // Rüzgâr eşiğin altındaysa iki doku örneği boşa gidiyordu: arazi yüksekliği ve
    // kar profili, sonucu sıfırla çarpılacak bir değer için okunuyordu. Gökyüzü her
    // pikselde bu yoldan geçiyor.
    if (_SpindriftDensity <= 0.0) return sky;

    float driftGround = TerrainHeightAt(cameraPos.xz);
    float driftHeight = max(0.0, cameraPos.y - driftGround);
    float driftPath = exp(-_SpindriftFalloff * driftHeight) / (_SpindriftFalloff * s);
    float driftDensity = _SpindriftDensity
                       * SampleSnowProfile(driftGround).r
                       * SpindriftFlow(cameraPos.xz);

    float rawDrift = driftDensity * min(driftPath, maxPath);
    return sky + _SpindriftMaxDepth * rawDrift / (rawDrift + _SpindriftMaxDepth);
}

float SkyFogAmount(float3 cameraPos, float3 dir)
{
    if (_HeightFogDensity <= 0.0 && _FogSeaDensity <= 0.0 && _FogFreeDensity <= 0.0)
        return 0.0;

    // Kameranın önündeki bank boş gökte görünür bir leke bırakır: "vadide gezen sis"
    // ancak gök de sislenince var olabiliyor.
    float2 ahead = cameraPos.xz + normalize(dir.xz + 0.0001) * 900.0;
    float bank = (FogBankAt(cameraPos.xz) + FogBankAt(ahead)) * 0.5;

    return saturate(1.0 - exp(-SkyFogDepth(cameraPos, dir, 1e9) * bank));
}

/// Havanın kendi rengi: gökyüzü gradyanının ta kendisi. Gökyüzü de sis de bunu çağırır
/// — tek formül, iki tüketici; tam sislenen arazi gökten ayırt edilemez. Sis ayrı bir
/// renk taşıdığı sürece her hava/saat köşesinde yeni bir "parlayan karton dağ" çıkıyor
/// ve elle yamanıyordu.
///
/// Kızıllık güneşin bulunduğu yönde yoğunlaşır; karşı ufukta Dünya'nın gölgesi
/// yükselir (mavi-mor, ayrıca karanlık — ışık yatay gelirken o yöne düşmez). Ayrışma
/// yalnız güneş ufka yakınken. Yükseldikçe tepe rengine kararır.
float3 AirColor(float3 direction)
{
    float3 sunward = normalize(float3(_SunDirection.x, 0.0, _SunDirection.z) + 0.0001);
    float3 viewFlat = normalize(float3(direction.x, 0.0, direction.z) + 0.0001);
    float towardSun = smoothstep(-0.85, 0.85, dot(viewFlat, sunward));

    // Yatay yönün ANLAMLI olduğu bölge. İki koşul birden: kutuplara (tam yukarı, tam
    // aşağı) yaklaşınca azimut tanımsızlaşır; ve ufkun ALTINDA gökyüzü bandı diye bir
    // şey yoktur — aşağı bakan ışın yerin üstündeki havayı görür, göğün güneş bandını
    // değil. İkincisi eksikti: bant nadir'e doğru bir noktaya toplanıp şafakta yere
    // bakınca koni bırakıyordu. Ufuk seviyesinde 1, 14.5° aşağıda 0 — ufka yakın uzak
    // arazi sıcaklığını korur, dik aşağıda yapıyı söndürür.
    float azimuth = saturate(length(direction.xz) * 3.0)
                  * saturate(direction.y * 4.0 + 1.0);
    towardSun = lerp(0.5, towardSun, azimuth);

    float lowSun = 1.0 - saturate(abs(_SunDirection.y) / 0.3);

    // Alacakaranlık paleti — sayılar Python simülasyonundan (dusk_palette_sim.py,
    // "canlı" varyantı): zincirin tamamı — süzülmüş güneş, controller karışımları,
    // bu formül, altın saat kademesi, ACES — ekransız çizilip referans batış
    // fotoğrafının rampasına oturtuldu.
    //
    // Batış göğü tek renk değildir: güneşin çevresi ALTIN, açıldıkça turuncuya ve
    // kızıla iner, karşı yarı soğuk gri-maviye düşer. Altın, süzülmüş güneş renginden
    // çarpımla üretilemez — o renkte yeşil tükenmiştir, çarpım sarı çıkaramaz; altın
    // ucu açık yazılır, kızıl ucu süzülmüş güneşten gelir, arası kendiliğinden turuncu.
    // Bantlar towardSun'dan değil ham açıdan türer: towardSun 0.85'te doyuyor ve bant
    // sanılandan üç kat geniş çıkıp parlaklığı ekrana bakılamaz hâle getiriyordu.
    // Aynı kapı buna da: altın ucu towardSun'dan bağımsız hesaplanıyor, kapısız
    // kalınca koniyi tek başına üretmeye yetiyor.
    float sunDot = saturate(dot(viewFlat, sunward)) * azimuth;
    // Altın ucu bir tık kısık: tam parlak altın, güneş tarafını gözü alacak kadar
    // dolduruyordu. Ay tarafı gölge renginden gelir, bu kısma ondan bağımsız.
    // ALTIN UCU AÇIK YAZILIR — fizik örneği değil. Denendi ve ölçüldü: güneş tam
    // ufuktayken (06:00) gök örneği luminans 0.151, bu sabit 0.571 — 3.8 kat. Ekranda
    // şafak tamamen sönüyordu.
    //
    // Sebep modelin hatası değil, KAPSAMI: `Atmosphere` temiz atmosferi çiziyor
    // (Bruneton'un pristine Mie katsayısı). Şafağın altın patlaması ise AEROSOLÜN işi —
    // toz, nem, is. Güneş çevresindeki hâleyi kuran Mie saçılması gerçek havada
    // bizimkinden kat kat güçlü. Yani bu sabit bir sanatçı uydurması değil, tozlu bir
    // atmosferin yaklaşık karşılığı; aerosol modellenirse fizik yerini alır.
    //
    // Sabit yalnız güneşin TAM azimutunda baskın: `pow(sunDot, 1.8)` ile sönüyor,
    // çeperde fizik örneği devrede ve o saatle ilerliyor.
    // TON GÜNEŞİN YÜKSEKLİĞİYLE KIZILLAŞIR. Tek bir altın sabit, güneş ufkun on
    // derece üstündeyken de dibindeyken de aynı sarıyı veriyordu — batımda kızıllık
    // hiç gelmiyordu.
    //
    // Fizik: kızıllık YOL UZUNLUĞUNDAN doğar. Güneş alçaldıkça ışık atmosferde daha
    // uzun yol alır, önce mavi sonra yeşil süpürülür, geriye kırmızı kalır. Sabitin
    // parlaklığı bilinçli abartma olarak kalıyor (bkz. DECISIONS.md), ama TONU artık
    // güneşin yüksekliğini izliyor.
    //
    // Kızıl uç yeşili sarıdan üçte iki oranında kısıyor: (0.9, 0.52) → (0.85, 0.20).
    // Mavi zaten ihmal edilebilir.
    float3 gold = lerp(float3(0.9, 0.52, 0.11), float3(0.85, 0.20, 0.05),
                       1.0 - smoothstep(0.0, 0.09, _SunHeight));

    float3 duskHue = lerp(_HeightFogSunColor.rgb, gold, pow(sunDot, 1.8));

    float3 warm = lerp(_HeightFogColor.rgb, duskHue, pow(saturate(towardSun), 1.2) * lowSun);
    warm *= 1.0 + pow(sunDot, 8.0) * lowSun * 0.10;

    float3 horizon = lerp(_HeightFogColor.rgb,
                          lerp(_HeightFogShadowColor.rgb, warm, towardSun),
                          lowSun);

    // Üs 0.55 → 0.35: sıcaklık ufka yakın kalmalı. Yüksek üs ufuk rengini göğün
    // yarısına kadar taşıyıp bandı sanılandan çok geniş gösteriyordu.
    //
    // Üssün 0'daki eğimi SONSUZ: göz hizasının ilk yarım derecesinde harman sıfırdan
    // 0.15'e sıçrıyor, altında `saturate` ile kırpılıyor. Ufuk sıcak, zenit koyu mavi
    // olduğu için o kırılma Mach bandı gibi düz bir çizgi bırakıyor. Gökyüzünde
    // farkedilmiyordu; arazi puslanınca hava rengi uzak dağın üstünde de baskın oldu ve
    // çizgi dağın içinden geçiyormuş gibi, "dağ şeffafmış gibi" göründü.
    //
    // Üs korunur — sıcaklığın ufka yakın kalması ondan geliyor. Yalnız ilk üç derece
    // smoothstep'le C1 sürekli hâle getirilir; 3.4°'nin üstünde eğri birebir aynı.
    float rise = pow(saturate(direction.y), 0.35)
               * smoothstep(0.0, 0.06, direction.y);
    float3 air = lerp(horizon, _HeightFogZenith.rgb, saturate(rise));

    // Karşı yarı kararır ama simsiyah değil: gerçek karşı ufuk yumuşak mor-gridir
    air *= lerp(1.0, lerp(0.55, 1.0, towardSun), lowSun);

    // İleri saçılım, çift lob: geniş pus parlaması + dar parlak çekirdek. Sisin
    // içinden güneş keskin disk olarak değil ışıyan bir yumak olarak görünür —
    // şafak sisindeki güneş budur; sis denizinin üstüne çıkınca gerçek disk döner.
    float sunUp = smoothstep(-0.08, 0.12, _SunDirection.y);
    float alignment = saturate(dot(direction, normalize(_SunDirection + 0.0001)));
    // Dar lob ölçülü: büyütülünce diskin oturduğu yeri dolduruyor ve güneşin
    // kendisi kendi parlamasının içinde kayboluyordu
    float forward = pow(alignment, 8.0) * 0.05 + pow(alignment, 64.0) * 0.12;
    air += _HeightFogSunColor.rgb * (forward * sunUp);

    return air;
}

/// Çizilmiş rengi havanın içine oturtur. Çağıran tarafın miktarı ayrıca alıp lerp'i
/// kendi yazmasına gerek yok — o iki satır her yüzeyde birebir aynı olurdu.
///
/// Sis şimşeği de saçar. Rengi sabit tutulunca fırtınada — yani şimşeğin çaktığı tek
/// havada — görüş yedi yüz metreye düşüyor ve arazinin büyük kısmı o değişmeyen rengin
/// altında kalıyordu: yüzey aydınlansa bile üstü örtülü olduğu için hiçbir şey
/// görünmüyordu. Gerçekte tam tersi olur, çakma anında sisin kendisi içeriden parlar.
float3 ApplyHeightFog(float3 color, float3 cameraPos, float3 worldPos)
{
    float3 air = AirColor(normalize(worldPos - cameraPos))
               + _LightningFlash.rgb * LightningFogScatter;

    // Geçirgenlik kanal başına: Rayleigh saçılması maviyi kırmızıdan çok süpürür.
    // Uzaktaki koyu kaya maviye kayar (araya mavi saçılır), uzak kar hafif ılıklaşır
    // (mavisi süzülür) — ressamın hava perspektifi. Yoğun sis su damlasıdır (Mie),
    // renk seçmez; çarpan görüş kapandıkça nötre iner ve bunu CPU belirler.
    float drift;
    float integral = HeightFogIntegral(cameraPos, worldPos, drift)
                   * FogBankPath(cameraPos.xz, worldPos.xz);

    // İKİ KATMAN SIRAYLA, tek karışımda değil. Sürüklenen kar araziye yapışık ve
    // gözün ÖNÜNDE duruyor; sisin mavisi ise yol boyunca dağılmış. Tek karışımda
    // toplanınca perdenin sönümü sisin payını da açıyor ve rüzgâr arttıkça yamaç
    // beyazlayacağına MAVİLEŞİYORDU.
    //
    // Önce sis uygulanır (uzak), sonra perde onun üstüne biner (yakın). Perde kendi
    // nötr rengini taşıyor, altındakini boyamıyor.
    float3 withFog = lerp(air, color, exp(-integral * _HeightFogChroma));

    // PERDE DERİNLEŞTİKÇE SÖNER, gökyüzünün rengine boyanmaz. `AirColor` bakış yönüne
    // bağlı ve gökyüzü gradyanını taşıyor; ona yakınsatınca o gradyan dağın üstüne
    // biniyor ve ufuk çizgisi dağın içinden geçiyormuş gibi görünüyordu.
    //
    // Fosforluluğun çaresi renk değiştirmek değil parlaklığı kısmak: kalın perde daha
    // çok saçar ama kendi içinde de söner, dışarı çıkan ışık doyuma gider.
    float3 veil = SpindriftColor()
                * lerp(1.0, 0.55, saturate(drift / max(0.01, _SpindriftMaxDepth)));

    return lerp(veil, withFog, exp(-drift));
}

#endif
