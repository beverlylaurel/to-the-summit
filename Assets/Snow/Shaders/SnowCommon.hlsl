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

/// İz oyulduktan ve kenar sırtı eklendikten sonraki yüzey yüksekliği, metre.
float SnowSurfaceHeight(float swe, float rhoN, float carve, float rim)
{
    return max(SnowBaseHeight(swe, rhoN) - carve + rim, 0.0);
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
float  _FogDensity01;
float  _RainOnSnow01;
float3 _SnowUpDirection;

/// Bölgenin dışındaki dünyanın genel kar durumu.
float _FallbackSWE;
float _FallbackRhoN;

/// KAR ÇİZGİSİ. Donma seviyesinin üstünde kar kalıcı; altında yağdığı sürece
/// birikip sonra eriyor. Başlangıç durumu, bölgeye YENİ giren teksel ve
/// kaskadın da dışı bu eğriden doluyor — dağ karlı doğuyor.
///
/// `_SnowLineGroundY` donma seviyesinden geliyor (`ISnowEnvironmentSource`), ayrı
/// bir sayı değil: "sıcaklık +8 ama tepe karsız" çelişkisi böyle imkânsız.
float _SnowLineGroundY;
float _SnowLineBand;
float _SnowLineSWE;

float SnowInitialSweAt(float groundY)
{
    float t = saturate((groundY - _SnowLineGroundY) / max(_SnowLineBand, 1e-3));
    return lerp(_FallbackSWE, _SnowLineSWE, t * t * (3.0 - 2.0 * t));
}

// ------------------------------------------------------------------ yakalama

/// Yakalama hacminin sıfır noktası — gözlemcinin dünya Y'si.
float _SnowCaptureOriginY;

/// RT_Capture'ın R kanalı GÖRELİ tutuluyor (yarım hassasiyet, bkz.
/// Hidden_SnowCaptureDepth). Dünya Y'sine dönüşü tek yerden geçiyor ki
/// çözücü tarafta unutulmasın.
float SnowCaptureY(float encoded)
{
    return _SnowCaptureOriginY + encoded;
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

// --- Uzak kaskad (spec §21 Faz 10) ---
TEXTURE2D(_SnowFarTex);

float2 _SnowFarCenter;
float  _SnowFarAreaSize;

/// Yakın bölgenin DIŞINDAKİ karın durumu. Eskiden sabit bir sayıydı ve
/// dağın tamamı aynı kalınlıkta kar taşıyordu; kaskad orada da gerçek
/// birikme ve erime veriyor. Kaskadın da dışında sabite düşülüyor.
float2 SnowFarStateAt(float2 posXZ)
{
    float2 uv = (posXZ - _SnowFarCenter) / max(_SnowFarAreaSize, 1e-3) + 0.5;

    // Kaskadın da dışı: sabit değil, kar çizgisi eğrisi. Sabit olsaydı
    // dağın tepesi ile eteği aynı kalınlıkta kar taşırdı.
    if (any(uv < 0.0) || any(uv > 1.0))
        return float2(SnowInitialSweAt(SampleGroundHeight(posXZ)), _FallbackRhoN);

    return SAMPLE_TEXTURE2D_LOD(_SnowFarTex, sampler_LinearClamp, uv, 0).rg;
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

    float2 far = SnowFarStateAt(SnowUVToWorld(uv));

    s.r = lerp(far.x, s.r, inside);
    s.g = lerp(far.y, s.g, inside);
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

float SnowSurfaceAt(float2 uv)
{
    float  inside = SnowInsideMask(uv);
    float2 uvC    = saturate(uv);

    float4 s = SAMPLE_TEXTURE2D_LOD(_SnowStateTex, sampler_LinearClamp, uvC, 0);
    float4 t = SAMPLE_TEXTURE2D_LOD(_SnowTrailTex, sampler_LinearClamp, uvC, 0);

    float2 far = SnowFarStateAt(SnowUVToWorld(uv));

    float swe  = lerp(far.x, s.r, inside);
    float rhoN = lerp(far.y, s.g, inside);

    float h = SnowSurfaceHeight(swe, rhoN, t.r * inside, t.g * inside);

    // SASTRUGİ BURAYA DA EKLENİYOR. Yalnız köşe shader'ına eklenirse
    // normal'ler düz kalıyor ve sırtlar ışığa hiç tepki vermiyor — spec
    // §18.4'ün "en sık atlanan adım" dediği yer burası.
    h += SnowSastrugiOffset(SnowUVToWorld(uv), t.a * inside);

    return max(h, 0.0);
}

#endif
