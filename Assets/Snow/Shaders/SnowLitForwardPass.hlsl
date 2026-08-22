// ROL: kar yüzeyinin köşe yer değiştirmesi, normali ve ışıklandırması.
// Çağıran: SnowLit.shader.

#ifndef SNOW_LIT_FORWARD_PASS_INCLUDED
#define SNOW_LIT_FORWARD_PASS_INCLUDED

#include "SnowLighting.hlsl"
#include "../../Shaders/HeightFog.hlsl"

/// Teşhis görünümü. 0 = kapalı. Değerleri `DebugMenu`'de yazıyor.
float _SnowMeshProbe;

/// Teşhis anahtarları. 1 = kapalı. İkisi de ayrı ayrı kapatılabiliyor ki
/// çıkıntının sahibi tek turda ayrılsın.
float _SnowStitchOff;
float _SnowSkirtOff;
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
    float2 probeData  : TEXCOORD4;      // x = halka indeksi, y = köşe işareti
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
    // SIFIRA BÖLME KORUMASI. `_SnowRing0Quad` yalnız clipmap görünürken
    // yazılıyor; ilk karelerde ya da mesh kapalıyken sıfır kalıyor ve
    // `floor(x / 0)` NaN üretiyor. NaN köşe, ekranda bıçak gibi bir yaprak
    // olarak çıkıyor.
    float ringQuad = max(_SnowRing0Quad, 1e-4) * pow(SNOW_RING_SCALE, ringIndex);
    float coarse = max(ringQuad * SNOW_RING_SCALE, 1e-4);

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
    if (IN.ringId.y > 0.5 && IN.ringId.y < 1.5 && _SnowSkirtOff < 0.5)
        positionWS.y -= SNOW_SKIRT_DEPTH;

    // DİKİŞ: işaret 2. Sınır köşesi kaba ızgaradan okuyor.
    if (IN.ringId.y > 1.5 && _SnowStitchOff < 0.5)
    {
        float stitched = SnowStitchedWorldY(flat.xz, IN.ringId.x);

        // Sonuç sonlu değilse dikiş uygulanmıyor. Bir NaN köşe bütün üçgeni
        // ekrana yayıyor; bedeli o köşede dikişsiz kalmak olmalı, yaprak
        // değil.
        if (isfinite(stitched)) positionWS.y = stitched;
    }

    OUT.probeData = IN.ringId;
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
    // ------------------------------------------------------------------ prob
    //
    // TEK CEVAPLI TEŞHİS GÖRÜNÜMÜ.
    //
    // Ekrandaki bir kusurun sahibini göz kararıyla aramak tur yakıyor. Bu
    // görünüm her şüpheliyi AYRI RENGE boyuyor ve ışıktan, sisten,
    // tonemap'ten, pozlamadan BAĞIMSIZ çıkıyor — araç kendi yalanını
    // söyleyemiyor. Renk doğrudan dönüyor, aydınlatma zinciri hiç
    // çalışmıyor.
    //
    // Bu bölüm kar yüzeyi kabul edilince silinecek.
    if (_SnowMeshProbe > 0.5)
    {
        float mode = _SnowMeshProbe;
        float ring = IN.probeData.x;
        float flag = IN.probeData.y;

        // 1 — HALKA. Sınırlar ve hangi halkanın nereyi çizdiği.
        if (mode < 1.5)
        {
            float3 c = ring < 0.5 ? float3(1, 0, 0)
                     : ring < 1.5 ? float3(0, 1, 0)
                     : ring < 2.5 ? float3(0, 0.4, 1)
                                  : float3(1, 1, 0);
            return half4(c, 1);
        }

        // 2 — KÖŞE İŞARETİ. Etek ve dikiş yerinde mi.
        if (mode < 2.5)
        {
            float3 c = flag > 1.5 ? float3(0, 1, 1)      // dikiş
                     : flag > 0.5 ? float3(1, 0, 1)      // etek
                                  : float3(0.15, 0.15, 0.15);
            return half4(c, 1);
        }

        // 3 — KALINLIK. 0–60 cm gri; basamak varsa doğrudan görünür.
        if (mode < 3.5)
            return half4((half3)saturate(height / 0.6).xxx, 1);

        // 4 — KENAR SÖNÜMÜ. Bandın nerede başlayıp bittiği.
        if (mode < 4.5)
            return half4((half3)SnowMeshEdgeFade(IN.positionWS.xz).xxx, 1);

        // 5 — QUAD IZGARASI. Dama tahtası; kare boyu quad boyu.
        if (mode < 5.5)
        {
            float quad = max(_SnowRing0Quad, 1e-4) * pow(SNOW_RING_SCALE, ring);
            float2 cell = floor(IN.positionWS.xz / quad);
            float checker = fmod(cell.x + cell.y, 2.0);

            return half4((half3)lerp(0.25, 0.75, checker).xxx, 1);
        }

        float2 puv = SnowWorldToUV(IN.positionWS);

        // 6 — NaN AVCISI. Sonlu olmayan tek bir değer bile KIRMIZI.
        if (mode < 6.5)
        {
            bool bad = !isfinite(height)
                    || !all(isfinite(IN.positionWS))
                    || !all(isfinite(puv));

            return bad ? half4(1, 0, 0, 1) : half4(0.1, 0.1, 0.1, 1);
        }

        // 7 — KOMŞU FARKI. Süreksizlik parlıyor. 1 teksel adımla merkezi fark;
        // 5 cm'lik sıçrama tam beyaz.
        if (mode < 7.5)
        {
            float t = 1.0 / max(_SnowResolution, 1.0);

            float dx = abs(SnowSurfaceAt(puv + float2(t, 0)) - SnowSurfaceAt(puv - float2(t, 0)));
            float dz = abs(SnowSurfaceAt(puv + float2(0, t)) - SnowSurfaceAt(puv - float2(0, t)));

            return half4((half3)saturate(max(dx, dz) / 0.05).xxx, 1);
        }

        // 8 — DÜNYA Y BANDI. Her metre bir bant; yüzey sıçrarsa bantlar kırılır.
        if (mode < 8.5)
        {
            float band = frac(IN.positionWS.y);
            return half4((half3)lerp(0.2, 0.9, step(0.5, band)).xxx, 1);
        }

        // 9 — BÖLGE MASKESİ. Yakın bölgenin sönüm karesi doğrudan görünür.
        if (mode < 9.5)
        {
            float inside = SnowInsideMask(puv);
            return half4((half3)inside.xxx, 1);
        }

        // 10 — VERİ KAYNAĞI. Yeşil = yakın durum, mavi = kaskad, kırmızı = kar
        // çizgisi eğrisi. Hangi pikselin nereden beslendiği tek bakışta.
        float inside2 = SnowInsideMask(puv);

        float2 cuv = (IN.positionWS.xz - _SnowFarCenter) / max(_SnowFarAreaSize, 1e-3) + 0.5;
        bool inCascade = all(cuv >= 0.0) && all(cuv <= 1.0);

        float3 col = inside2 > 0.5 ? float3(0, 1, 0)
                   : inCascade    ? float3(0, 0.4, 1)
                                  : float3(1, 0, 0);

        return half4((half3)col, 1);
    }

    // PROJENİN SİSİ, URP'NİNKİ DEĞİL (rapor §7). Dağ `ApplyHeightFog`
    // kullanıyor; kar mesh'i `MixFog` kullandığı için sınırda farklı
    // parlaklıkta ayrışıyordu.
    color = ApplyHeightFog(color, GetCameraPositionWS(), IN.positionWS);

    return half4(color, 1.0);
}

#endif
