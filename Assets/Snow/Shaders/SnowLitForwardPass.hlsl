// ROL: kar yüzeyinin köşe yer değiştirmesi, normali ve ışıklandırması.
// Çağıran: SnowLit.shader.

#ifndef SNOW_LIT_FORWARD_PASS_INCLUDED
#define SNOW_LIT_FORWARD_PASS_INCLUDED

#include "SnowLighting.hlsl"
#include "../../Shaders/HeightFog.hlsl"

#include "SnowDetailNormals.hlsl"
#include "SnowTerrainShadow.hlsl"
#include "../../Shaders/StochasticTiling.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
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

/// KÖŞE YER DEĞİŞTİRMESİ (spec §8.3).
///
/// Kamera mesafesine göre kısma YOK — kısılırsa yüzey kayar (spec §8.3).
float3 SnowDisplacedPositionWS(float3 positionWS, out float heightOut)
{
    float groundY = SampleGroundHeight(positionWS.xz);

    // `SnowSurfaceAt` spec §8.3'ün `SnowSurfaceHeight` + sastrugi + kenar
    // sönümü zincirini tek yerde topluyor. Fragment normali de aynı
    // fonksiyondan türüyor (spec §8.6); ikisi ayrı yazılırsa geometri ile
    // normal farklı yüzeyi tarif eder.
    float h = SnowSurfaceAt(SnowWorldToUV(positionWS));

    heightOut = h;
    positionWS.y = groundY + h;

    return positionWS;
}

Varyings SnowLitVertex(Attributes IN)
{
    Varyings OUT = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

    float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

    float h;
    positionWS = SnowDisplacedPositionWS(positionWS, h);

    OUT.positionWS = positionWS;
    OUT.snowHeight = h;
    OUT.positionCS = TransformWorldToHClip(positionWS);
    // GÖLGE KOORDİNATI ÖTELEME DENENDİ VE İŞE YARAMADI.
    //
    // Kar yüzeyi araziden `h` metre yukarıda; gölge haritasına yazan arazi.
    // Örnekleme noktasını ışığa doğru `h` kadar kaydırmak akneyi çözer diye
    // denendi — ölçüm değişmedi (oran 0.856 → 0.851).
    //
    // Ölçülen gerçek: `mainLight.shadowAttenuation` kar yüzeyinde TEK DÜZE
    // 0.850 (215–219/255, desen yok). Işığın gölge gücü 1, yani bu tam gölge
    // değil; PCF taplarının ~%15'i gölgede okunuyor. Kaynağı henüz
    // bulunmadı — arazi caster'ı değil (öteleme çözmedi), karın kendisi değil
    // (artık gölge atmıyor).
    OUT.shadowCoord = TransformWorldToShadowCoord(positionWS);
    OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);

    return OUT;
}

/// Merkezi fark, adım DIŞARIDAN veriliyor. Vertex shader'ından çağrılabilen
/// hâli bu: `fwidth` yalnız fragman komutudur ve vertex'te derlenmez.
float3 SnowNormalAtStep(float2 uv, float t, float3 positionWS)
{
    // PAYDA ÖRNEKLEME ADIMININ DÜNYA BOYU, BİR TEKSEL DEĞİL.
    //
    // Merkezi fark `t` uv adımıyla alınıyor ve `t` uzaklıkla `fwidth`'ten
    // büyüyor. Payda bir tekselde sabit kalınca gradyan adım/teksel oranı
    // kadar ŞİŞİYOR ve normal deviriliyor.
    //
    // Ölçüldü (18 m yukarıdan, 20 cm kar, düz zemin): mesh pikselinin
    // 121583'ünde N.y < 0.2 — yani neredeyse tamamı YATAY. Arazi aynı karede
    // 0.5–0.8 arasında. Ekranda kar mesh'i şekilsiz düz bir levha, oyuncunun
    // çevresinde görünür bir KARE.
    float ws = t * _SnowAreaSize;

    float hL = SnowSurfaceAt(uv - float2(t, 0.0));
    float hR = SnowSurfaceAt(uv + float2(t, 0.0));
    float hD = SnowSurfaceAt(uv - float2(0.0, t));
    float hU = SnowSurfaceAt(uv + float2(0.0, t));

    float3 nGround = SampleGroundNormal(positionWS.xz);

    // KAR YÜZEYİ = ARAZİ + KALINLIK; EĞİMİ DE İKİSİNİN TOPLAMI.
    //
    // `SnowSurfaceAt` yalnız kar KALINLIĞINI döndürüyor, arazi yüksekliğini
    // değil. Merkezi fark bu yüzden yalnız kalınlığın gradyanını veriyordu ve
    // sabit kalınlıkta sıfır çıkıyordu — mesh eğimli bir yamaçta bile DİMDİK
    // bir normal taşıyordu.
    //
    // Ölçüldü (%15 eğimli yamaç, 20 cm kar, alçak güneş):
    //   mesh normali  (-0.008, 0.996, -0.008)   dimdik
    //   arazi normali ( 0.149, 0.991,  0.047)   eğimli
    // Sonuç: mesh araziden %11.4 daha koyu (0.6983 / 0.7885) ve yerel doku
    // 2.27 kat daha çalkantılı. Ekranda oyuncuyu takip eden 24 m'lik kare.
    // DÜZ zeminde iki normal de dikey olduğu için fark yok — tepeden yapılan
    // sınamalar bu yüzden temiz görünüyordu.
    //
    // Eğimler doğrusal toplanır (`SnowDetailNormals` ile aynı ilke): arazinin
    // eğimi + kar kalınlığının gradyanı.
    float2 zeminEgim = float2(nGround.x, nGround.z) / max(nGround.y, 1e-3);
    float2 karEgim   = float2(hL - hR, hD - hU) / max(2.0 * ws, 1e-6);

    float2 toplam = zeminEgim + karEgim;
    return normalize(float3(toplam.x, 1.0, toplam.y));
}

/// NORMAL FRAGMENT'TA, MERKEZİ FARKLA (spec §13.3). Vertex'te hesaplanırsa
/// normal quad başına sabit kalır ve yüzey bloklu görünür (spec §22).
/// Adım piksel ayak izinden büyür — uzakta örnekleme aralığı genişleyince
/// normal kaynamaz.
float3 SnowNormalAt(float2 uv, float3 positionWS)
{
    float t = max(1.0 / _SnowResolution, length(fwidth(uv)) * 0.5);
    return SnowNormalAtStep(uv, t, positionWS);
}

/// KAR NEREDE ÇİZİLMEZ (spec §8.4).
///
/// Üç kural, üçü de zorunlu. Birincisi köşe shader'ında (kenar sönümü);
/// ikisi burada:
///
/// 2. 4 mm'nin altındaki kar hiç çizilmiyor. Hem mesh kenarını hem karın
///    eridiği yerleri z-fighting'siz çözüyor.
/// 3. Gürültülü bitiş `[KAYNAK: Company of Heroes 2, KGC 2013]` — kar kenarı
///    düz çizgi değil, gürültülü lekeler hâlinde bitiyor.
/// KESME KARIN VARLIĞINDAN, OYULMUŞ YÜZEYDEN DEĞİL.
///
/// `h` oyma ve sırt uygulandıktan SONRAKİ yüzey. Ona bakınca derin bir ayak
/// izi eşiğin altına düşüyor, piksel kesiliyor ve izin dibinde ÇIPLAK ZEMİN
/// görünüyordu (kullanıcı bildirdi: "adım attığım yerde zeminin karaltısını
/// görüyorum").
///
/// Eşiğin sorduğu soru "burada kar VAR MI"; cevabı `baseH`, yani oyulmamış
/// kar sütunu. İz o sütunun içinde bir ÇUKUR — kar orada duruyor, yalnız
/// yüzeyi alçalmış.
void SnowClipEdge(float h, float baseH, float3 positionWS, float2 uv)
{
    clip(baseH - SNOW_MIN_VISIBLE_HEIGHT);

    float edgeFade = saturate((baseH - SNOW_MIN_VISIBLE_HEIGHT)
                              / max(_SnowEdgeFadeRange, 1e-4));

    // BÖLGE KENARI KESMEYE GİRMİYOR — DENENDİ VE KARENİN KENDİSİYDİ.
    //
    // `SnowEdgeFade` yalnız YÜKSEKLİĞE uygulanıyor (`SnowSurfaceAt`): mesh
    // kenarda araziyle aynı kota iniyor ve basamak oluşmuyor. Bir tur bu
    // sönüm kesmeye de bağlandı, gerekçesi iki yüzey arasındaki %2.3
    // parlaklık farkını lekeli bir kuşakla gizlemekti.
    //
    // Fark kaynağında kapanınca (arazi de karın ışıklandırmasını kullanıyor,
    // mesh normali arazi eğimini taşıyor) kuşağın gerekçesi kalmadı ve
    // KENDİSİ görünür oldu: kenarda granüllü bir hat. Ölçüldü — kuşak
    // kapatılınca sınır tamamen kayboluyor.


    // STOKASTİK DÖŞEME. Düz döşemede aynı leke sabit periyotla tekrar ediyor
    // ve zemin düzenli bir ızgara gibi okunuyordu (kullanıcı bildirdi).
    float breakup = SampleStochasticMask(TEXTURE2D_ARGS(_SnowBreakup, sampler_SnowBreakup),
                                         positionWS.xz * _SnowBreakupScale);

    clip(edgeFade - breakup * 0.6);
}

/// Fragman'da ortak kurulum: kesme, normal, yüzey. Hem ileri geçiş hem
/// DepthNormals aynı yüzeyi görmeli.
void SnowShadeSetup(float3 positionWS, out float3 N, out SnowSurface surface, out float height)
{
    float2 uv = SnowWorldToUV(positionWS);
    height = SnowSurfaceAt(uv);

    float4 state = SnowStateAt(uv);

    // KENARDA YALNIZ YÜKSEKLİK DEĞİL, MADDE DE DÜNYAYA DÖNÜYOR. Yoğunluk hem
    // albedoyu hem pürüzlülüğü sürüyor; kenarda dünyanın değerine
    // harmanlanmazsa iki yüzey aynı kotta bitse bile farklı renkte kalıyor.
    float kenar = SnowEdgeFade(uv);
    state.g = lerp(_FallbackRhoN, state.g, kenar);
    state.b = lerp(0.0,           state.b, kenar);
    state.a = lerp(0.0,           state.a, kenar);

    SnowClipEdge(height, SnowBaseHeight(state.r, state.g), positionWS, uv);

    // MESH YALNIZ YEREL SAPMAYI ÇİZİYOR.
    //
    // Kar tabanını ARAZİ çiziyor: `MountainSurface.shader` dört geçişinde de
    // `SnowWorldCoverHeight()` kadar yükseliyor ve karın ışıklandırmasını
    // uyguluyor. Mesh de aynı düz yüzeyi ikinci kez çizdiği sürece iki shader
    // arasındaki HER fark bölge sınırında kare olarak görünüyordu.
    //
    // Üç gün boyunca bu fark terim terim kapatılmaya çalışıldı — yoğunluk,
    // kar sütunu, AO, dağ gölgesi. Her biri ölçülüp kapatıldı (oran 1.61'den
    // 1.08'e indi) ama sıfırlanmadı: iki ayrı kod yolunun onlarca terimi var
    // ve biri eşitlenince başka bir saatte başkası ayrışıyor (alpenglow
    // denemesi şafağı 1.02'den 0.25'e bozdu). Kalan %8-13 statik görüntüde
    // zor seçiliyor ama sınır OYUNCUYLA BİRLİKTE KAYDIĞI için gözle
    // yakalanıyor.
    //
    // Kalıcı çözüm farkı küçültmek değil, ikinci çizimi kaldırmak: mesh
    // yalnız arazinin veremeyeceği yerel sapmayı (iz oyuğu, kenar sırtı)
    // çiziyor, düz alanda TAMAMEN çekiliyor. Düz alan tek shader'dan geldiği
    // için orada fark matematiksel olarak imkânsız.
    //
    // Sapma = oyma + sırt. `SNOW_LOCAL_MIN` bir tekselin gürültüsünün üstünde,
    // gözle seçilebilen en sığ izin altında.
    float4 sapmaTrail = SnowTrailAt(uv);
    float yerelSapma = sapmaTrail.r + sapmaTrail.g;
    clip(yerelSapma - SNOW_LOCAL_MIN);

    float4 trail = SnowTrailAt(uv);

    float dist = length(GetCameraPositionWS() - positionWS);
    float footprint = length(fwidth(positionWS.xz));

    float freshness = 1.0 - saturate((SnowDensity(state.g) - 100.0) / 350.0);

    N = SnowNormalAt(uv, positionWS);
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

    // DAĞIN KENDİ GÖLGESİ — arazi bunu uyguluyordu, mesh uygulamıyordu.
    // Güneş ufka yakınken arazi gölgeye girip koyulurken mesh parlak kalıyor,
    // bölge sınırı kare olarak görünüyordu (gerekçe `SnowTerrainShadow.hlsl`).
    // Işığa sırtı dönük yüzeyde hesap gereksiz: katkı zaten sıfır.
    if (dot(N, mainLight.direction) > 0.0)
        mainLight.shadowAttenuation *= SnowTerrainSunShadow(IN.positionWS, mainLight.direction);

    // BULUT GÖLGESİ ARAZİYLE AYNI KANALDAN. Gökyüzünü çizen yoğunluk alanının
    // kendisi; doğrudan güneşi kesiyor, gökten gelen dolaylı ışığa dokunmuyor.
    // Arazi bunu `MountainSurface.shader`'da aynı satırla uyguluyor.
#ifdef _LIGHT_COOKIES
    mainLight.color *= SampleMainLightCookie(IN.positionWS);
#endif

    // KENARDA AO DA DÜNYAYA DÖNÜYOR. `SnowHeightAO` kar yüzeyinin kendi
    // örtülmesi ve merkezde gerekli; arazi tarafında böyle bir alan yok, o
    // kendi `occlusion`'ını kullanıyor. Kenarda ikisi ayrışırsa sınır yine
    // parlaklık atlıyor — ölçüldü, kalan fark 1.08 kattı.
    float2 kenarUV = SnowWorldToUV(IN.positionWS);
    half heightAO = lerp(1.0h, SnowHeightAO(kenarUV, height), (half)SnowEdgeFade(kenarUV));

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
