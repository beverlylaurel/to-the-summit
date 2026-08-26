// ROL: Terrain ucgenlerini kameraya gore boler ve yeni koseleri kar
// yuzeyinin yuksekligi kadar kaydirir.
// Cagiran: MountainSurface.shader'in dort gecisi de.

#ifndef SNOW_TESSELLATION_INCLUDED
#define SNOW_TESSELLATION_INCLUDED

#include "SnowCommon.hlsl"

/// Hull'a giren kontrol noktasi. Yalniz dunya konumu tasiniyor: dort gecisin
/// dordu de `Attributes`'ta sadece `positionOS` aliyor ve geri kalan her sey
/// konumdan tureniyor.
struct SnowTessControlPoint
{
    float3 positionWS : INTERNALTESSPOS;
};

struct SnowTessFactors
{
    float edge[3] : SV_TessFactor;
    float inside  : SV_InsideTessFactor;
};

/// KENAR FAKTORU YALNIZ KENARIN IKI UCUNDAN HESAPLANIYOR.
///
/// Catlak su durumda olusuyor: iki komsu patch ortak kenari FARKLI sayida
/// parcaya boluyor, aradan bosluk goruluyor. Kanonik cozum faktoru patch'e
/// degil KENARA baglamak — komsu patch ayni iki koseyi gordugu icin ayni
/// sayiyi uretir. Bu bir umut degil, kimlik: girdi ayni, cikti ayni.
///
/// [KAYNAK: NVIDIA, "My Tessellation Has Cracks!", GDC 2012 — bitisik
/// patch'ler catlaksiz cizim icin ortak kenarda AYNI faktoru almak zorunda.]
float SnowTessKenarFaktoru(float3 a, float3 b)
{
    float3 orta = (a + b) * 0.5;
    float  d    = distance(orta, _SnowTessCameraPos);

    float t = saturate((_SnowTessFar - d) / max(_SnowTessFar - _SnowTessNear, 1e-3));

    // TABAN 1, GLOBALLER SIFIRKEN DE. `SnowManager` `ExecuteAlways` degil:
    // edit modda ve Play'in ilk karesinde butun tess globalleri sifir kaliyor.
    // `_SnowTessMax` sifirken `lerp` faktoru 1'in ALTINA indirebilir ve
    // donanim gecersiz faktorde patch'i tamamen atar — yuzey kaybolur.
    return max(lerp(1.0, _SnowTessMax, t), 1.0);
}

SnowTessFactors SnowPatchConstant(InputPatch<SnowTessControlPoint, 3> patch)
{
    SnowTessFactors f;

    if (_SnowDbgNoTess > 0.5)
    {
        f.edge[0] = f.edge[1] = f.edge[2] = 1.0;
        f.inside  = 1.0;
        return f;
    }

    // HLSL kurali: `edge[i]` i'inci KOSENIN KARSISINDAKI kenar.
    f.edge[0] = SnowTessKenarFaktoru(patch[1].positionWS, patch[2].positionWS);
    f.edge[1] = SnowTessKenarFaktoru(patch[2].positionWS, patch[0].positionWS);
    f.edge[2] = SnowTessKenarFaktoru(patch[0].positionWS, patch[1].positionWS);

    f.inside  = (f.edge[0] + f.edge[1] + f.edge[2]) / 3.0;

    return f;
}

/// `fractional_odd`: faktor surekli degisiyor, yeni koseler sifir uzunluktan
/// buyuyerek beliriyor. `integer` kullanilirsa kamera yaklastikca geometri
/// BASAMAK BASAMAK degisiyor ve yuzey gorunur bicimde zipliyor.
[domain("tri")]
[outputcontrolpoints(3)]
[outputtopology("triangle_cw")]
[partitioning("fractional_odd")]
[patchconstantfunc("SnowPatchConstant")]
SnowTessControlPoint SnowHull(InputPatch<SnowTessControlPoint, 3> patch,
                              uint id : SV_OutputControlPointID)
{
    return patch[id];
}

/// GECICI — GOREV 1 ICIN. Gercek yukseklik alani Gorev 3'te baglaniyor.
/// Amaci altyapiyi (catlak, golge, performans) yukseklik alanindan bagimsiz
/// dogrulamak: 1 m dalga boyu, 20 cm genlik, silueti gorunur bicimde kiriyor.
float SnowTessYerDegistirme(float3 posWS)
{
    if (_SnowDbgNoTess > 0.5) return 0.0;

    return sin(posWS.x * 6.2831853) * cos(posWS.z * 6.2831853) * 0.20;
}

/// Baricentrik interpolasyon + yer degistirme. Her gecis kendi `Varyings`'ini
/// kurdugu icin domain shader gecise ozel; ortak olan bu iki satir.
///
/// YER DEGISTIRME DUNYA +Y YONUNDE, YUZEY NORMALI BOYUNCA DEGIL. Kar yatay
/// birikiyor; egimli arazide normal boyunca kaydirmak kari yamaca dik
/// yapistirir.
float3 SnowTessKonum(OutputPatch<SnowTessControlPoint, 3> patch, float3 bary)
{
    float3 posWS = patch[0].positionWS * bary.x
                 + patch[1].positionWS * bary.y
                 + patch[2].positionWS * bary.z;

    posWS.y += SnowTessYerDegistirme(posWS);

    return posWS;
}

#endif
