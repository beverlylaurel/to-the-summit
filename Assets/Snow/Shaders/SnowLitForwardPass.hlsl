// ROL: kar yüzeyinin köşe yer değiştirmesi, normali ve ışıklandırması.
// Çağıran: SnowLit.shader.

#ifndef SNOW_LIT_FORWARD_PASS_INCLUDED
#define SNOW_LIT_FORWARD_PASS_INCLUDED

#include "SnowLighting.hlsl"
#include "../../Shaders/HeightFog.hlsl"
#include "SnowDetailNormals.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float2 ringId     : TEXCOORD0;      // x = halka indeksi
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float  snowHeight : TEXCOORD1;
    float4 shadowCoord : TEXCOORD2;
    float  fogFactor  : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

/// KÖŞE YER DEĞİŞTİRMESİ (spec §13.2).
///
/// Kamera mesafesine göre kısma YOK — kısılırsa yüzey oyuncu yaklaştıkça
/// kayar ve dalgalanır.
/// Teşhis: 1 olduğunda kar mesh'i hiç yükselmiyor.
float _SnowFlattenProbe;

/// Yüzeyin dünya Y'si, verilen XZ'de. Dikiş için ayrı ayrı örnekleniyor.
float SnowSurfaceWorldY(float2 posXZ)
{
    float3 p = float3(posXZ.x, 0.0, posXZ.y);
    return SampleGroundHeight(posXZ) + SnowSurfaceAt(SnowWorldToUV(p));
}

/// T-KAVŞAĞI DİKİŞİ.
///
/// Sınır köşesi yüksekliği KABA ızgaradan okuyor: dış halkanın köşelerinin
/// arasındaki bilinear yüzey. İki halka sınırda birebir aynı çizgiyi
/// paylaşıyor, arada yarık kalmıyor.
float SnowStitchedWorldY(float2 posXZ, float ringIndex)
{
    float ringQuad = _SnowRing0Quad * pow(SNOW_RING_SCALE, ringIndex);
    float coarse = ringQuad * SNOW_RING_SCALE;

    float2 a = floor(posXZ / coarse) * coarse;
    float2 t = saturate((posXZ - a) / coarse);
    float2 b = a + coarse;

    float y00 = SnowSurfaceWorldY(float2(a.x, a.y));
    float y10 = SnowSurfaceWorldY(float2(b.x, a.y));
    float y01 = SnowSurfaceWorldY(float2(a.x, b.y));
    float y11 = SnowSurfaceWorldY(float2(b.x, b.y));

    return lerp(lerp(y00, y10, t.x), lerp(y01, y11, t.x), t.y);
}

float3 SnowDisplacedPositionWS(float3 positionWS, float ringIndex, out float heightOut)
{
    float groundY = SampleGroundHeight(positionWS.xz);
    float2 uv     = SnowWorldToUV(positionWS);

    float h = SnowSurfaceAt(uv);

    // TEŞHİS: yer değiştirmeyi tamamen kapatır. Şerit GEOMETRİ mi yoksa
    // GÖLGELEME mi — iki gündür geometri sanılıp yükseklik yamaları yazıldı.
    // 1 olduğunda yüzey araziye yapışıyor; şerit duruyorsa geometri değil.
    h *= 1.0 - _SnowFlattenProbe;

    heightOut = h;

    // DIŞ HALKALAR BİR TIK AŞAĞIDA. Halkalar kendi ızgaralarına snap'lendiği
    // için sınırda birkaç santimlik kaplama kalıyor; orada iç halka derinlik
    // testini kazansın diye dış halka milimetrik itiliyor. Gerekçe
    // `DECISIONS.md`.
    positionWS.y = groundY + h - ringIndex * SNOW_RING_DEPTH_BIAS;

    return positionWS;
}

Varyings SnowLitVertex(Attributes IN)
{
    Varyings OUT = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

    float h;
    float3 flat = positionWS;

    positionWS = SnowDisplacedPositionWS(positionWS, IN.ringId.x, h);

    // ETEK: işaret 1. Aşağı iniyor, mesh ile arazi arasını kapatıyor.
    if (IN.ringId.y > 0.5 && IN.ringId.y < 1.5)
        positionWS.y -= SNOW_SKIRT_DEPTH;

    // DİKİŞ: işaret 2. Sınır köşesi kaba ızgaradan okuyor.
    if (IN.ringId.y > 1.5)
        positionWS.y = SnowStitchedWorldY(flat.xz, IN.ringId.x);

    OUT.positionWS = positionWS;
    OUT.snowHeight = h;
    OUT.positionCS = TransformWorldToHClip(positionWS);
    OUT.shadowCoord = TransformWorldToShadowCoord(positionWS);
    OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

    return OUT;
}

/// Merkezi fark, adım DIŞARIDAN veriliyor. Vertex shader'ından çağrılabilen
/// hâli bu: `fwidth` yalnız fragman komutudur ve vertex'te derlenmez.
float3 SnowNormalAtStep(float2 uv, float t, float hHere, float3 positionWS)
{
    float ms = SnowTexelSize();

    float hL = SnowSurfaceAt(uv - float2(t, 0.0));
    float hR = SnowSurfaceAt(uv + float2(t, 0.0));
    float hD = SnowSurfaceAt(uv - float2(0.0, t));
    float hU = SnowSurfaceAt(uv + float2(0.0, t));

    float3 nSnow   = normalize(float3(hL - hR, 2.0 * ms, hD - hU));
    float3 nGround = SampleGroundNormal(positionWS.xz);

    // KENAR BANDINDA NORMAL ARAZİDEN.
    //
    // Sönüm bandında kalınlık birkaç metrede 45 cm düşüyor; merkezi türev
    // `hL − hR` devasa çıkıyor ve normal neredeyse YATAY oluyor. Shader
    // orayı dik bir kaya duvarı sanıp aşırı parlatıyor ya da karartıyor —
    // ekrandaki dikey çizgili şerit buydu (ölçüldü: yer değiştirme
    // kapatılınca şerit kalıyordu, yani gölgeleme).
    //
    // Bandın içinde yüzey zaten araziye oturuyor; normali de arazininki.
    float meshFade = SnowMeshEdgeFade(positionWS.xz);

    // İnce karda zeminin şekli baskın; kalınlaştıkça karın kendi yüzeyi.
    float3 n = normalize(lerp(nGround, nSnow, saturate(hHere / 0.08)));

    return normalize(lerp(nGround, n, meshFade));
}

/// NORMAL FRAGMENT'TA, MERKEZİ FARKLA (spec §13.3). Vertex'te hesaplanırsa
/// normal quad başına sabit kalır ve yüzey bloklu görünür (spec §22).
/// Adım piksel ayak izinden büyür — uzakta örnekleme aralığı genişleyince
/// normal kaynamaz.
float3 SnowNormalAt(float2 uv, float hHere, float3 positionWS)
{
    float t = max(1.0 / _SnowResolution, length(fwidth(uv)) * 0.5);
    return SnowNormalAtStep(uv, t, hHere, positionWS);
}

/// KAR NEREDE ÇİZİLMEZ (spec §8.1, §8.2).
///
/// 4 mm altındaki kar hiç çizilmiyor: z-fighting tamamen ortadan kalkıyor ve
/// kar araziye kaybolarak karışıyor. Kenar düz çizgi olmasın diye eşiğin
/// hemen üstünde gürültüyle kırılıyor.
void SnowClipEdge(float h, float3 positionWS)
{
    // KENAR BANDINDA KIRPMA YOK.
    //
    // Kırpmanın işi SIĞ KARI gizlemek: 4 mm altındaki kar araziyle
    // z-fight ederdi. Ama mesh'in dış kenarında kalınlık bilerek negatife
    // indiriliyor (arazinin altına gömülsün diye) ve kırpma orada da
    // çalışınca mesh toprağa GİREMİYOR — 4 mm'de havada bıçakla kesiliyor
    // ve geriye kalınlık kadar dik bir duvar kalıyordu.
    //
    // İki değişiklik birbiriyle çelişiyordu: biri gömmeye çalışıyor, öteki
    // gömülmeden kesiyordu. Kenar bandında kırpma kapalı; oradaki geometri
    // arazinin altında kaldığı için zaten derinlik testinde kaybediyor.
    // SÖNÜM SÜREKLİ, BASAMAK DEĞİL. `if (meshFade > 0.999)` yazıldığında
    // kırpma sönüm bandının başladığı yerde ANİDEN kesiliyor ve o hatta ince
    // kar birden katılaşıp görünür bir çember bırakıyordu. Eşik de gürültü de
    // sönümle çarpılıyor: bandın içine girildikçe kırpma yumuşakça bırakıyor.
    float meshFade = SnowMeshEdgeFade(positionWS.xz);

    clip(h - SNOW_MIN_VISIBLE_HEIGHT * meshFade);

    float edgeFade = saturate((h - SNOW_MIN_VISIBLE_HEIGHT * meshFade)
                              / max(_SnowEdgeFadeRange, 1e-4));

    float breakup = SAMPLE_TEXTURE2D(_SnowBreakup, sampler_SnowBreakup,
                                     positionWS.xz * _SnowBreakupScale).r;

    clip(edgeFade - breakup * 0.6 * meshFade);
}

/// Fragman'da ortak kurulum: kesme, normal, yüzey. Hem ileri geçiş hem
/// DepthNormals aynı yüzeyi görmeli.
void SnowShadeSetup(float3 positionWS, out float3 N, out SnowSurface surface, out float height)
{
    float2 uv = SnowWorldToUV(positionWS);
    height = SnowSurfaceAt(uv);

    SnowClipEdge(height, positionWS);

    float4 state = SnowStateAt(uv);
    float4 trail = SnowTrailAt(uv);

    float dist = length(GetCameraPositionWS() - positionWS);
    float footprint = length(fwidth(positionWS.xz));

    float freshness = 1.0 - saturate((SnowDensity(state.g) - 100.0) / 350.0);

    N = SnowNormalAt(uv, height, positionWS);
    // DÜZLEMSEL XZ KAPLAMA DİK YÜZEYDE EZİLİYOR (rapor §2). Yüzey dikleştikçe
    // XZ izdüşümü sıfıra yaklaşıyor, doku dikey şeritler hâlinde uzuyor.
    // Detay normalleri yataylık oranıyla ağırlıklandırılıyor — kar zaten
    // yataya yakın yüzeyde durur, dolayısıyla kaybedilen bir şey yok.
    float planar = saturate((N.y - 0.35) / 0.35);

    // Detay yalnız yataya yakın yüzeyde (rapor §2): dik kenarda düzlemsel XZ
    // kaplaması dikey şeritler hâlinde uzuyor.
    float3 detailed = SnowApplyDetailNormals(N, positionWS, freshness, state.a, dist);
    N = normalize(lerp(N, detailed, planar));

    surface = SnowBuildSurface(state.g, state.b, state.a, trail.b,
                               height, positionWS, footprint);
}

half4 SnowLitFragment(Varyings IN) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(IN);

    float3 N;
    SnowSurface surface;
    float height;

    SnowShadeSetup(IN.positionWS, N, surface, height);

    float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);

    Light mainLight = GetMainLight(IN.shadowCoord);

    half heightAO = SnowHeightAO(SnowWorldToUV(IN.positionWS), height);

    half3 color = SnowDirectLight(mainLight, N, V, surface);
    color += SnowAmbient(N, surface, mainLight.shadowAttenuation, heightAO);

#if defined(_ADDITIONAL_LIGHTS)
    // Forward+ kümeleme `inputData`'nın alanlarını okuyor; makro onu isimle
    // arıyor.
    InputData inputData = (InputData)0;
    inputData.positionWS = IN.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);

    uint pixelLightCount = GetAdditionalLightsCount();

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, IN.positionWS, half4(1, 1, 1, 1));
        color += SnowDirectLight(light, N, V, surface);
    LIGHT_LOOP_END
#endif

    // MEVCUT SİSİN KENDİSİ (spec §14). Kendi sis hesabımız yok.
    // PROJENİN SİSİ, URP'NİNKİ DEĞİL (rapor §7). Dağ `ApplyHeightFog`
    // kullanıyor; kar mesh'i `MixFog` kullandığı için sınırda farklı
    // parlaklıkta ayrışıyordu.
    color = ApplyHeightFog(color, GetCameraPositionWS(), IN.positionWS);

    return half4(color, 1.0);
}

#endif
