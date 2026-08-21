#ifndef TOTHESUMMIT_SNOW_TESSELLATION_INCLUDED
#define TOTHESUMMIT_SNOW_TESSELLATION_INCLUDED

#include "SnowDisplacement.hlsl"

// KAR BİRİKİNTİSİ İÇİN BÖLÜNME. Arazi 4.28 m/örnek; birikintinin gövdesi ~8 m, yüzey
// dalgası ~2.6 m — ikincisi mevcut ızgarada hiç çözülemiyor. Hull/domain aşaması
// yalnız GEREKEN yerde üçgeni bölüyor: yakın VE karlı yamalar. Uzak arazi, çıplak
// kaya ve dik duvar bölünmeden geçiyor.
//
// DÖRT GEÇİŞTE DE AYNI. ForwardLit, ShadowCaster, DepthOnly, DepthNormals aynı yer
// değiştirmeyi uygulamak ZORUNDA — biri atlanırsa gölge yüzeyin altında kalır, bulut
// birikintinin önüne geçer, SSAO hayalet gölge basar. URP'nin hazır gölge ve derinlik
// geçişleri kendi vertex fonksiyonlarını getirdiği için o ikisi burada elle yazıldı.

// _SnowTessNear ve _SnowTessFar SnowDisplacement.hlsl'de bildirildi: yer değiştirme
// sönümü onlardan türüyor ve ikisi ayrı tanımlanamaz.
float _SnowTessFactor;      // en yakın yamada kenar başına bölünme

struct TessellationControlPoint
{
    float4 positionOS : INTERNALTESSPOS;
};

struct TessellationFactors
{
    float edge[3] : SV_TessFactor;
    float inside  : SV_InsideTessFactor;
};

/// Bölünme öncesi köşe: hiçbir şey yapmıyor, konumu taşıyor. Asıl iş domain'de.
TessellationControlPoint SnowTessVertex(float4 positionOS)
{
    // `point` HLSL'de ayrılmış kelime (geometri shader'ı girdi türü) — değişken
    // adı olarak kullanılamıyor.
    TessellationControlPoint controlPoint;
    controlPoint.positionOS = positionOS;
    return controlPoint;
}

/// Bir KÖŞENİN bölünme talebi. Mesafeden ve kar derinliğinden: karsız yamayı bölmenin
/// karşılığı yok. Yalnız dünya konumuna bakıyor — LOD çatlağı kuralı.
float SnowTessEdgeFactor(float3 worldPos)
{
    float toCamera = distance(worldPos, _WorldSpaceCameraPos);

    float near = 1.0 - smoothstep(_SnowTessNear, _SnowTessFar, toCamera);
    if (near <= 0.001) return 1.0;

    // Bölünme, yer değiştirme BAŞLAMADAN tamamlanıyor. Eşikler eskiden üst üste
    // biniyordu (bölünme 0.18-0.54, yer değiştirme 0.18-0.36) ve karın en keskin
    // değiştiği bant tam da üçgenlerin bölünmediği banttı: görsel yüzey orada 4.28
    // metrelik düz parçalara oturuyor, çarpışma yüzeyi ise gerçek fonksiyonu izliyordu.
    // Belirti ince karda toplar bazen karın altında bazen üstünde kalıyordu; kalın
    // karda ikisi çakışıyordu.
    float depth = SnowMacroDepth(worldPos);
    float thick = smoothstep(_SnowDisplaceStart * 0.4, _SnowDisplaceStart, depth);

    float macro = lerp(1.0, _SnowTessFactor, near * thick);

    // AYAK İZİ KENDİ BANDINI İSTİYOR. Arazi üçgeni 7.32 m; makro katsayı 6 ile kenar
    // 1.22 metreye iniyor, oysa iz 0.34 m. İz bandında katsayı 64 → kenar 0.114 m.
    // Bant dar (8-14 m) tutuluyor: 14 m yarıçapta ~23 arazi üçgeni var, 64 katsayıyla
    // ~94 bin alt üçgen eder.
    // ARAZİ İZ İÇİN BÖLÜNMÜYOR.
    //
    // Bir dönem 22-46 metre bandında katsayı 16'ya çıkıyordu ve kare başına ÜÇ MİLYON
    // üçgen ediyordu (ölçüldü, ekrandan okundu). İzin yakın planını `SnowPatch` taşıyor
    // ve iki halkası 48 metreyi zaten örtüyor — deformasyon penceresinin görünür
    // yarıçapının tamamı. Arazinin izle işi kalmadı.

    return macro;
}

/// Kenar faktörü İKİ UCUN ORTALAMASI. Yamanın merkezinden hesaplansaydı komşu
/// yamalar ortak kenar için farklı sayı üretir ve arada delik açılırdı.
TessellationFactors SnowPatchConstant(InputPatch<TessellationControlPoint, 3> patch)
{
    float3 world0 = TransformObjectToWorld(patch[0].positionOS.xyz);
    float3 world1 = TransformObjectToWorld(patch[1].positionOS.xyz);
    float3 world2 = TransformObjectToWorld(patch[2].positionOS.xyz);

    float f0 = SnowTessEdgeFactor(world0);
    float f1 = SnowTessEdgeFactor(world1);
    float f2 = SnowTessEdgeFactor(world2);

    TessellationFactors factors;

    // edge[i], i'nci köşenin KARŞISINDAKİ kenar.
    factors.edge[0] = 0.5 * (f1 + f2);
    factors.edge[1] = 0.5 * (f2 + f0);
    factors.edge[2] = 0.5 * (f0 + f1);
    factors.inside = (factors.edge[0] + factors.edge[1] + factors.edge[2]) / 3.0;

    return factors;
}

[domain("tri")]
[outputcontrolpoints(3)]
[outputtopology("triangle_cw")]
[partitioning("fractional_odd")]
[patchconstantfunc("SnowPatchConstant")]
TessellationControlPoint SnowHull(InputPatch<TessellationControlPoint, 3> patch,
                                  uint id : SV_OutputControlPointID)
{
    return patch[id];
}

/// Yeni köşenin dünya konumu: baryantrik karışım + kar yüksekliği. Konum kontrol
/// noktalarından, yükseklik DÜNYA KONUMUNDAN — ikisinin ayrılması çatlağı engelleyen şey.
float3 SnowDomainPositionWS(InputPatch<TessellationControlPoint, 3> patch,
                            float3 barycentric)
{
    float3 positionOS = patch[0].positionOS.xyz * barycentric.x
                      + patch[1].positionOS.xyz * barycentric.y
                      + patch[2].positionOS.xyz * barycentric.z;

    float3 positionWS = TransformObjectToWorld(positionOS);
    positionWS.y += SnowTotalDisplacement(positionWS);
    return positionWS;
}

/// Yer değiştirmenin ürettiği yüzey normali. Gölgelendirme bunu arazi normaliyle
/// harmanlıyor; harmanlamazsa siluet kabarır ama ışık düz yüzeyi aydınlatır.
float3 SnowDisplacedNormal(float3 worldPos, float3 baseNormal)
{
    float2 gradient = SnowDisplacementGradient(worldPos);
    if (dot(gradient, gradient) < 1e-8) return baseNormal;

    // Eğim vektöründen normal: yüzey y = h(x,z) için normal (-dh/dx, 1, -dh/dz).
    // Arazi normaliyle toplanıyor, yerine geçmiyor — kar yamacın üstünde duruyor.
    float3 snowNormal = normalize(float3(-gradient.x, 1.0, -gradient.y));
    return normalize(baseNormal + snowNormal - float3(0.0, 1.0, 0.0));
}

#endif
