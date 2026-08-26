// ROL: kar sisteminin ortak GPU yardımcıları — durum dönüşümleri, dünya↔teksel
// eşlemesi, zemin yüksekliği örneklemesi.
// Çağıran: SnowSim.compute ve bütün kar shader'ları.

#ifndef SNOW_COMMON_INCLUDED
#define SNOW_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"
#include "SnowConstants.hlsl"

// sampler_LinearClamp URP core'un GlobalSamplers.hlsl'inde tanımlı (ölçüldü).
// Kendi sampler'ımızı açmıyoruz — spec bu adı kullanıyor (§7.3, §9.4, §10.2, §12.2).

// ---------------------------------------------------------------- kar durumu

/// Normalize yoğunluktan gerçek yoğunluk, kg/m³ (spec §6.3).
float SnowDensity(float rhoN)
{
    return lerp(SNOW_RHO_MIN, SNOW_RHO_MAX, saturate(rhoN));
}

/// Gerçek yoğunluktan normalize yoğunluk.
float SnowDensityN(float rho)
{
    return saturate((rho - SNOW_RHO_MIN) / (SNOW_RHO_MAX - SNOW_RHO_MIN));
}

/// Bozulmamış kar sütununun yüksekliği, metre. `h = SWE * 1000 / rho`.
///
/// Kar bir yükseklik değil MADDE: korunan nicelik SWE, görünür derinlik ondan
/// türetiliyor. Aynı SWE sıkışınca alçalıyor — batma, patika ve izlerin dolması
/// bu tek denklemden çıkıyor.
float SnowBaseHeight(float swe, float rhoN)
{
    return swe * SNOW_RHO_WATER / max(SnowDensity(rhoN), 1.0);
}

// ------------------------------------------------------------ dünya ↔ teksel

float2 _SnowAreaCenter;      // bölgenin dünya XZ merkezi, snap'lenmiş
float  _SnowAreaSize;        // bölgenin kenar uzunluğu, metre
float  _SnowResolution;      // doku çözünürlüğü, teksel

float2 SnowWorldToUV(float3 p)
{
    return (p.xz - _SnowAreaCenter) / _SnowAreaSize + 0.5;
}

float2 SnowUVToWorld(float2 uv)
{
    return (uv - 0.5) * _SnowAreaSize + _SnowAreaCenter;
}

float2 SnowTexelToWorld(uint2 id)
{
    return SnowUVToWorld((float2(id) + 0.5) / _SnowResolution);
}

float SnowTexelSize()
{
    return _SnowAreaSize / _SnowResolution;
}

/// Bölgenin kenarında yumuşak sönüm. Dışarıda 0, ortada 1.
/// TABAN DERİNLİĞİ İÇİN GENİŞ MASKE.
///
/// Bölgenin kenarında yumuşak geçiş. Sert kesilseydi deformasyon alanının
/// sınırı yerde görünür bir kare olurdu.
float SnowInsideMask(float2 uv)
{
    float2 e = abs(uv - 0.5) * 2.0;
    return 1.0 - smoothstep(0.88, 1.0, max(e.x, e.y));
}

// ---------------------------------------------------------------------- çevre

/// HEPSİ MEVCUT SİSTEMLERDEN OKUNUYOR (spec §3). Kar sistemi bunlardan hiçbirini
/// üretmiyor; `SnowManager.WriteGlobals` köprüden alıp yayınlıyor.
float3 _WindWS;
float  _WindSpeed;
float  _TemperatureC;
float  _SunElevation01;

/// KENAR KIRILMA GÜRÜLTÜSÜ. Kar mesh'i, arazinin kar katmanı ve nesne üstü
/// kar maskesi AYNI dokuyu okuyor; tanımı tek yerde, ikisi de bu dosyayı
/// dahil ediyor.
TEXTURE2D(_SnowBreakup);
SAMPLER(sampler_SnowBreakup);

/// KAR IŞIKLANDIRMASININ AYARLARI GLOBAL, MATERYALDE DEĞİL. Arazinin kar
/// katmanı da aynı ışıklandırmayı kullanıyor (`MountainSurface.shader`) ve o
/// ayrı bir materyal. Per-materyal kalsalardı arazi bu değerleri SIFIR okur,
/// iki yüzey yine ayrışırdı. Tek sahibi `SnowSettings`; yayını `SnowManager`.
float4 _ShadowTint;
float  _TranslucencyStrength;

/// PARILTI AYARLARI GLOBAL, MATERYALDE DEĞİL. Arazi karı da parıldıyor
/// (`MountainSurface.shader`) ve o ayrı bir materyal. Per-materyal kalsaydı
/// iki yüzey iki farklı sayıyla parıldar, bölge sınırı görünürdü.
/// Tek sahibi `SnowSettings`; yayını `SnowManager`.
float  _SparkleCellSize;
float  _SparkleDensity;
float  _SparkleSharpness;
float  _SparkleIntensity;
float  _FogDensity01;
float  _RainOnSnow01;
float3 _SnowUpDirection;

/// Bölgenin dışındaki dünyanın genel kar durumu.
float _FallbackSWE;
float _FallbackRhoN;

/// Dünyanın kar sütunu, metre — `SnowManager` hesaplayıp yayınlıyor.
/// Arazi kar ışıklandırmasının derinliği bu; aynı hesabı fragment aşamasında
/// yapmak denendi ve arazi ışıklandırmasını bozdu.
float _WorldSnowDepth;

/// DÜNYANIN GENEL KAR KALINLIĞI, metre. Deformasyon bölgesinin DIŞINDA zemin
/// bu kadar kar taşıyor.
///
/// Arazi bunu geometri olarak da uyguluyor (`MountainSurface.shader` köşe
/// shader'ı). Uygulamazsa mesh kar kalınlığı kadar yükselirken arazi yerinde
/// kalıyor ve bölge sınırında DERİNLİKLE ÖLÇEKLENEN bir basamak oluşuyor:
/// 2 metrelik kenar rampasında 1 cm karda %0.5, 20 cm'de %10, 50 cm'de %25
/// eğim. Kullanıcı 1 ve 5 cm'de sorun görmeyip 20 ve 50 cm'de gördü.
float SnowWorldCoverHeight()
{
    return SnowBaseHeight(_FallbackSWE, _FallbackRhoN);
}

/// TAM SAYI HASH — PCG3D [KAYNAK: Jarzynski & Olano, JCGT 2020,
/// "Hash Functions for GPU Rendering"].
///
/// `frac(sin(dot(p, k)))` BÜYÜK GİRDİDE ÇÖKÜYOR. Ölçüldü: 104 000 tane için
/// X ekseninde yalnız 5237 FARKLI değer üretiyordu (%5). Yüz bin tane beş bin
/// dikey hat üzerine yığılınca ekranda "solucan", "sigara dumanı" ve
/// "bir yerde yağıyor bir yerde boş gökyüzü" görünüyordu (`SYMPTOMS.md`).
///
/// PCG3D'de çökme yok: aynı ölçümde 104 000/104 000 farklı değer, kova sapması
/// ×1.04, eksenler arası korelasyon 0.0003.
uint3 SnowPcg3d(uint3 v)
{
    v = v * 1664525u + 1013904223u;

    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;

    return v;
}

/// 0..1 aralığında üç bağımsız sayı.
float3 SnowRandU3(uint3 seed)
{
    return float3(SnowPcg3d(seed)) * (1.0 / 4294967296.0);
}

/// Tam sayı ızgara hücresinden — negatif koordinatlar da güvenli.
float3 SnowRandCell3(int3 cell)
{
    return SnowRandU3(asuint(cell));
}

/// HÜCRESEL (BLOK) GÜRÜLTÜ — 0..1, hücre içinde SABİT.
///
/// `SnowValueNoise` dört hücreyi bilinear harmanlıyor, yani doğası gereği
/// PÜRÜZSÜZ. Kar ise kırılır: taşıma gücü yenildiğinde kenar bir kayma yüzeyi
/// boyunca kopuyor ve kohezyonlu kar AÇISAL parçalara ayrılıyor
/// [KAYNAK: Terzaghi yerel kayma göçmesi — temel kenarında kama ve kayma
/// yüzeyi; kar mekaniği literatüründe "cohesive slab breaks into angular
/// chunks"].
///
/// Harmanlama olmadığı için hücre sınırında BASAMAK var — ve burada istenen
/// tam olarak o. Kenarın düzgün bir eğri olmaktan çıkıp parça parça
/// kopmasını sağlıyor (kullanıcı bildirdi: "pütür, dağılma, tomurcuk yok").
///
/// Hücre ızgarası dünya uzayında sabit; iz hareket ederken bloklar kaymıyor.
float SnowBlockNoise(float2 p)
{
    return SnowRandCell3(int3((int2)floor(p), 0)).x;
}


/// DEĞER GÜRÜLTÜSÜ — dört hücre hash'inin bilinear karışımı.
///
/// Hücre hash'i tek başına teksel teksel kırılıyor: iz kenarı lekelenmiyor,
/// tuz-biber oluyor. Bilinear karışım gürültüye bir DALGA BOYU veriyor;
/// çağıran ölçeği seçerek leke boyunu belirliyor.
///
/// `frac`'ın smoothstep'lenmesi (3t²−2t³) türevi sınırda sürekli kılıyor;
/// olmadan hücre kenarlarında görünür bir ızgara kalıyor.
float SnowValueNoise(float2 p)
{
    float2 h = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = SnowRandCell3(int3((int2)h + int2(0, 0), 0)).x;
    float b = SnowRandCell3(int3((int2)h + int2(1, 0), 0)).x;
    float c = SnowRandCell3(int3((int2)h + int2(0, 1), 0)).x;
    float d = SnowRandCell3(int3((int2)h + int2(1, 1), 0)).x;

    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

/// BÜKÜLMÜŞ HÜCRESEL GÜRÜLTÜ — hücreler kare değil, düzensiz.
///
/// Ham `SnowBlockNoise` `floor()` kullandığı için hücreler EKSENE HİZALI
/// KARE; ekranda piksel olarak okunuyor (kullanıcı bildirdi: "piksel piksel
/// oldu"). Gerçek kar bloğu kare değil, kırık kenarlı düzensiz bir çokgen.
///
/// Voronoi doğru cevap ama 3×3 tohum taraması gerektiriyor ve bu alan gradyan
/// için dört kez örnekleniyor — 36 tohum/piksel. Alan büküldüğünde aynı
/// düzensizlik tek ek gürültüyle elde ediliyor: kare ızgara eğrilip
/// çokgenleşiyor, keskin sınır (kırılma) korunuyor.
float SnowWarpedBlockNoise(float2 p)
{
    float2 buk = float2(SnowValueNoise(p * 0.63),
                        SnowValueNoise(p * 0.63 + 27.7)) * 2.0 - 1.0;

    return SnowBlockNoise(p + buk * 0.75);
}

// ------------------------------------------------------------ zemin yüksekliği

TEXTURE2D(_GroundHeightTex);

float2 _GroundOriginXZ;      // zemin dokusunun dünya köşesi
float2 _GroundTexelXZ;       // zemin dokusunun bir tekselinin dünya boyu
float2 _GroundSizeXZ;        // kapsadığı alan, metre
float  _GroundBaseY;         // 0..1 değerin haritalandığı taban kot
float  _GroundHeightRange;   // 0..1 değerin haritalandığı aralık

/// Zemin yüksekliği (spec §7.3). MeshBake yolunda doku doğrudan dünya Y tutar;
/// orada `_GroundBaseY = 0`, `_GroundHeightRange = 1` yazılır ve aynı satır çalışır.
float SampleGroundHeight(float2 posXZ)
{
    float2 uv = (posXZ - _GroundOriginXZ) / _GroundSizeXZ;
    float  n  = SAMPLE_TEXTURE2D_LOD(_GroundHeightTex, sampler_LinearClamp, saturate(uv), 0).r;
    return _GroundBaseY + n * _GroundHeightRange;
}

/// ASSUMPTION: spec §13.3 `SampleGroundNormal`'ı çağırıyor ama tanımlamıyor.
/// Zemin yükseklik dokusundan merkezi farkla türetiliyor — kar sistemi
/// böylece mevcut arazi bileşenlerinden hiçbir şey OKUMUYOR (spec §3).
/// Adım zemin dokusunun kendi teksel boyu; kar tekseliyle (1.5 cm) örneklenirse
/// aynı teksele düşer ve normal her yerde dümdüz yukarı çıkar.
float3 SampleGroundNormal(float2 posXZ)
{
    float2 e = max(_GroundTexelXZ, 1e-3);

    float hL = SampleGroundHeight(posXZ - float2(e.x, 0.0));
    float hR = SampleGroundHeight(posXZ + float2(e.x, 0.0));
    float hD = SampleGroundHeight(posXZ - float2(0.0, e.y));
    float hU = SampleGroundHeight(posXZ + float2(0.0, e.y));

    return normalize(float3(hL - hR, e.x + e.y, hD - hU));
}

// ------------------------------------------------------ gökyüzü görünürlüğü

TEXTURE2D(_SnowSkyVisTex);

float2 _SkyCenterXZ;
float  _SkyAreaSize;
float  _SkyResolution;

/// Bu noktanın gökyüzünü ne kadar gördüğü, 0..1 (spec §12.2).
///
/// ÜÇ TÜKETİCİSİ OLAN TEK HARİTA: zemin birikmesi, nesne üstü kar, kar tanesi
/// kesme. Ayrı ayrı çözüm üretilmiyor.
///
/// 3×3 örnekleme saçakta yumuşak geçiş veriyor; tek örnekle çatı kenarı
/// jilet gibi kesilir.
float SampleSkyVisibility(float3 posWS)
{
    float2 uv = (posWS.xz - _SkyCenterXZ) / _SkyAreaSize + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0)) return 1.0;

    float t = 1.0 / _SkyResolution;
    float vis = 0.0;

    [unroll]
    for (int y = -1; y <= 1; ++y)
    [unroll]
    for (int x = -1; x <= 1; ++x)
    {
        float occlY = SAMPLE_TEXTURE2D_LOD(_SnowSkyVisTex, sampler_LinearClamp,
                                           uv + float2(x, y) * t, 0).r;
        vis += 1.0 - smoothstep(0.05, 0.40, occlY - posWS.y);
    }

    return vis * (1.0 / 9.0);
}

// --------------------------------------------------------- rüzgâr gölgesi

TEXTURE2D(_SnowWindShadowTex);

/// > 0 → rüzgâr gölgesinde (birikme bölgesi), 0 → açık (erozyon mümkün).
/// Spec §18.0 birebir.
float SampleWindShadow(float3 posWS)
{
    float2 uv = (posWS.xz - _SkyCenterXZ) / _SkyAreaSize + 0.5;
    if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;

    // Doku Wz tutuyor; gölge Wz − A. A yüzeyin kendisi.
    float wz = SAMPLE_TEXTURE2D_LOD(_SnowWindShadowTex, sampler_LinearClamp, uv, 0).r;

    return max(0.0, wz - posWS.y);
}

/// KOMPAKT DESTEKLİ DÜŞÜŞ [KAYNAK: Wyvill, Guy & Galin 1999].
/// Yarıçapı dışında TAM OLARAK sıfır — bu sayede erken çıkış mümkün.
/// Lineer veya Gauss kullanmak keskin daire ya da sonsuz kuyruk üretir
/// (spec §20).
float WyvillFalloff(float r, float R)
{
    float t = saturate(1.0 - (r * r) / max(R * R, 1e-6));
    return t * t * t;
}

// --------------------------------------------------------------- ısı kaynağı

/// Spec §18.2: on altı elemanlı uniform dizi. `StructuredBuffer` kullanılmıyor.
#define SNOW_MAX_HEAT_SOURCES 16

float4 _HeatSources[SNOW_MAX_HEAT_SOURCES];   // xyz = konum, w = yarıçap
float4 _HeatParams[SNOW_MAX_HEAT_SOURCES];    // x = şiddet
int    _HeatCount;

/// SICAKLIK ALANLARI TOPLANARAK BİRLEŞİYOR [KAYNAK: Grosbellet ve ark.,
/// CGF 2016, §4]. Örtü alanları çarpılır, sıcaklık alanları TOPLANIR;
/// karıştırılırsa iki ateşin üst üste binmesi karı eritmek yerine korur.
float SnowHeatField(float3 posWS)
{
    float theta = 0.0;

    [loop]
    for (int hi = 0; hi < _HeatCount; ++hi)
    {
        float3 hp = _HeatSources[hi].xyz;
        float  hr = _HeatSources[hi].w;

        float r = distance(posWS, hp);
        if (r >= hr) continue;

        theta += _HeatParams[hi].x * WyvillFalloff(r, hr);
    }

    return theta;
}

// ------------------------------------------------------------------ sastrugi

TEXTURE2D(_SastrugiNoise);
SAMPLER(sampler_SastrugiNoise);

/// CPU'da yumuşatılmış rüzgâr yönü. Ham yön kullanılırsa mevcut rüzgâr
/// sisteminin esintileri deseni titretiyor (spec §18.4).
float2 _SastrugiWindDir;

/// Cukurun ortalama yaricapi (m). `SnowManager.BuildTrailSegments` sahnedeki
/// deformer parcalarindan hesapliyor — sabit degil, cunku ayak izi uc kapsul
/// ve her birinin yaricapi ayri.
float _SnowCavityRadius;

/// SIRTLAR RÜZGÂRA DİK UZANIYOR (transverse). Dalga boyu rüzgâr yönünde
/// KISA, sırtlar rüzgâra dik yönde UZUN. UV'ler ters yazılırsa desen 90°
/// yanlış olur (spec §18.4, §22).
float SnowSastrugiOffset(float2 posXZ, float amplitude)
{
    if (amplitude <= 0.001) return 0.0;

    float2 wd = _SastrugiWindDir;
    float2 wp = float2(-wd.y, wd.x);

    float2 sUV = float2(dot(posXZ, wd) / SNOW_SASTRUGI_LENGTH,
                        dot(posXZ, wp) / SNOW_SASTRUGI_WIDTH);

    float n = SAMPLE_TEXTURE2D_LOD(_SastrugiNoise, sampler_SastrugiNoise, sUV, 0).r * 2.0 - 1.0;

    return n * SNOW_SASTRUGI_HEIGHT * amplitude;
}

// ------------------------------------------------------------- kar yüzeyi

TEXTURE2D(_SnowStateTex);
TEXTURE2D(_SnowTrailTex);

/// BÖLGENİN DIŞINDAKİ DÜNYANIN GENEL KAR DURUMU (spec §6.4, §8.4).
///
/// 24 m ötesinde kar mesh'i YOK; uzaktaki kar arazi materyaline uygulanan kar
/// tutması shader'ıyla gösteriliyor (spec §16). O katmanın okuduğu durum burası.
///
/// KAR İRTİFAYA BAĞLI DEĞİL. Yükseklikten türeyen bir "kar çizgisi" eğrisi
/// vardı; kaldırıldı. Kar yağarsa tutar, yağmazsa tutmaz. Yüksekte karın daha
/// çok olması sıcaklıktan kendiliğinden çıkıyor: `TemperatureField` kotla
/// düşüyor, yağış §3.4 histerezisiyle kara dönüyor. İkinci bir irtifa terimi
/// aynı şeyi ikinci kez söylerdi ve ikisi çelişebilirdi.
float2 SnowOutsideStateAt(float2 posXZ)
{
    return float2(_FallbackSWE, _FallbackRhoN);
}

/// Kar yüzeyinin zeminden yüksekliği, verilen bölge UV'sinde.
///
/// BÖLGE DIŞINDA DÜNYANIN GENEL DURUMU. `SnowInsideMask` kenarda yumuşak
/// geçiş veriyor; sert kesilseydi deformasyon alanının sınırı yerde görünür
/// bir kare olurdu.
/// Kar durumunun ham hâli, bölge dışında dünyanın genel durumuyla
/// harmanlanmış. R=swe G=rhoN B=wet A=disturb.
float4 SnowStateAt(float2 uv)
{
    float  inside = SnowInsideMask(uv);
    float4 s = SAMPLE_TEXTURE2D_LOD(_SnowStateTex, sampler_LinearClamp, saturate(uv), 0);

    float2 outside = SnowOutsideStateAt(SnowUVToWorld(uv));

    s.r = lerp(outside.x, s.r, inside);
    s.g = lerp(outside.y, s.g, inside);
    s.b *= inside;
    s.a *= inside;

    return s;
}

/// İz dokusunun ham hâli. R=carve G=rim B=kabuk A=sastrugi. Bölge dışında
/// iz yok.
float4 SnowTrailAt(float2 uv)
{
    return SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, saturate(uv), 0)
           * SnowInsideMask(uv);
}

/// 1 iken arazi, izin DERINLIGINI renk olarak basar: siyah = iz yok,
/// kirmizi = SNOW_RELIEF_MAX_DEPTH. Verinin shader'a ulasip ulasmadigini
/// isiklandirmadan bagimsiz ayirir.
float _SnowDebugDent;

/// 1 iken yuzey normalini ve NdotL'yi renk olarak basar:
/// kirmizi = duz NdotL, yesil = wrap NdotL, mavi = N.y.
/// Lekelerin normalden gelip gelmedigini tek bakista ayirir.
float _SnowDebugNormal;

/// PROB: yerdeki lekeler NE? Tek bakista ayirir.
///   kirmizi = dot(N, gunes) NEGATIF (gunesten kacik egim)
///   yesil   = ana isik golgesi
///   mavi    = ortam ortmesi (AO)
/// Lekeler hangi kanalda parliyorsa sorumlu o.
float _SnowDebugProbe;

/// PROB: kar ortusu maskesi ve carpanlari.
///   kirmizi = son maske, yesil = cavity (AO), mavi = egim x gok
float _SnowDebugCover;

/// TEŞHİS ANAHTARLARI — her terim tek tek kapanir.
float _SnowDbgNoFbm;
float _SnowDbgNoRipple;
float _SnowDbgNoSastrugi;
float _SnowDbgNoMicro;
float _SnowDbgNoLod;
float _SnowDbgNoSpec;
float _SnowDbgNoSparkle;
float _SnowDbgNoWrap;
float _SnowDbgNoAO;
float _SnowDbgNoBounce;
float _SnowDbgNoTexNormal;
float _SnowDbgNoCavityShadow;

/// 1 iken kar yuzeyinin normali TAMAMEN duzlestiriliyor (dunya +Y).
/// Lekeler burada da duruyorsa kaynak normal DEGIL.
/// Gidiyorsa ve rolyef anahtarlari etkisizse kaynak ARAZININ kendi normali.
float _SnowDbgFlatNormal;

#endif
