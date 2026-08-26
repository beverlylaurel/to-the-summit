# Kar Yüzeyi Geometrisi — Uygulama Planı

> **Ajan işçiler için:** GEREKLİ ALT BECERİ: `superpowers:subagent-driven-development` (tavsiye edilen) veya `superpowers:executing-plans` ile bu planı görev görev uygula. Adımlar takip için onay kutusu (`- [ ]`) kullanıyor.

**Hedef:** Kar yüzeyinin alt-metre tepeciklerini gerçek geometriye çevirmek — silüet kırılsın, tepeler birbirini gölgelesin, örtüşme olsun.

**Mimari:** Unity Terrain'in ürettiği üçgenler donanım tessellation'ı ile kameraya göre bölünüyor; her yeni köşe `SnowYuzeyRolyef`'in verdiği yükseklik kadar dünya +Y yönünde kaydırılıyor. Yükseklik fonksiyonu tek otorite: görsel de fizik de onu okuyor. Fizik tarafı için fonksiyonun C# ikizi yazılıyor ve bir eşlik testi ikisinin aynı sonucu verdiğini doğruluyor.

**Teknoloji:** Unity 6000.5.6f1, URP, HLSL Shader Model 5.0 (hull/domain shader), D3D12.

---

## Neden bu plan böyle kurulu

Bu iş bu projede **bir kez denendi ve geri alındı**. `MountainSurface.shader`'ın kendi yorumu:

> *"Yükseltiliyordu ve fizik tarafında karşılığı yoktu: `CharacterController` arazi collider'ının, yani KAYANIN üstünde duruyor. Ölçüldü: ayak 205.539, kaya 205.489, çizilen yüzey 205.98 — karakter yarım metre gömülü başlıyordu ve göz kar yüzeyinin altında kalıyordu."*

Sonuç: kar yüksekliği geometriden tamamen çıkarıldı. Bugün arazide alt-7.32-metre hiçbir şey geometri değil.

Bu planın 9–11. görevleri (C# ikizi, eşlik testi, `GroundSnap`) o hatanın tekrarını engelliyor. **Atlanamazlar.**

## Riskler ve nasıl sıfırlandıkları

| Risk | Nasıl sıfırlanıyor | Görev |
|---|---|---|
| Patch sınırında çatlak | Kenar faktörü **yalnız kenarın iki ucundan** hesaplanıyor. Komşu patch aynı iki köşeyi görür → aynı faktörü üretir. Umut değil, kimlik. | 1 |
| Yer değiştirmede çatlak | Yükseklik **yalnız dünya XZ**'ye bağlı. Aynı konumdaki iki köşe aynı değeri alır. | 1 |
| Gölge yüzeyle kaymış | Dört geçişin dördü de aynı hull/domain'i kullanıyor. | 2 |
| Gölge geçişinde yanlış mesafe | Gölge geçişinde `_WorldSpaceCameraPos` ışığın konumu. Kendi globalimiz `_SnowTessCameraPos` yayınlanıyor. | 1, 2 |
| Terrain LOD ile çakışma | Tessellation 60 m'de biter; Terrain LOD geçişi `heightmapPixelError = 2` ile yüzlerce metrede. Bölgeler ayrık. | doğrulama |
| Üçgen patlaması | `_SnowTessMax` ayar asset'inde, F1'de görünür, tek sayıyla geri çekilebilir. | 1, 12 |
| Fizik uyuşmazlığı (bir kez oldu) | C# ikizi + eşlik testi + `GroundSnap` | 9, 10, 11 |
| İz çift sayımı | Ölçülerek karar veriliyor, tahminle değil | 8 |
| Her şey birden bozulursa | `_SnowDbgNoTess` anahtarı **ilk görevde** kuruluyor; her görev ayrı commit. | 1 |

**En büyük risk azaltıcı: Görev 1 yükseklik alanına hiç dokunmuyor.** Geçici bir sinüs dalgasıyla tüm boru hattı (hull, domain, dört geçiş, çatlak, performans) doğrulanıyor. Altyapı çalıştığı kanıtlanmadan gerçek alan bağlanmıyor.

## Doğrulama bu projede ne demek

Test paketi yok (`CLAUDE.md`). Her görevin doğrulaması üç biçimden biri:

1. **Derleme kontrolü** — `ShaderUtil.ShaderHasError` (Unity MCP `RunCommand` ile, ekli örnek her görevde)
2. **Ekran doğrulaması** — kullanıcı Play'e basar, ne göreceği yazılı
3. **Gerçek test** — C# tarafı için `SnowConstantsTest` deseninde editör testi (Görev 10)

Shader düzenlemesinden sonra **her zaman** `date > Logs/refresh.trigger` çalıştırılır, 12 sn beklenir, sonra derleme kontrolü yapılır.

---

## Dosya yapısı

| Dosya | Sorumluluk | Durum |
|---|---|---|
| `Assets/Snow/Shaders/SnowTessellation.hlsl` | Hull, patch-constant, köşe interpolasyonu, yer değiştirme çağrısı. Dört geçişin paylaştığı tek kaynak. | **YENİ** |
| `Assets/Shaders/MountainSurface.shader` | Dört geçişe pragma + domain gövdesi | değişecek |
| `Assets/Snow/Shaders/SnowRelief.hlsl` | Yer değiştirme LOD eşiği, drift katmanı, maruziyet ayrımı | değişecek |
| `Assets/Snow/Shaders/SnowConstants.hlsl` | Yeni sabitler | değişecek |
| `Assets/Snow/Shaders/SnowCommon.hlsl` | Yeni globaller | değişecek |
| `Assets/Snow/Runtime/SnowSurfaceHeight.cs` | `SnowYuzeyRolyef`'in C# ikizi | **YENİ** |
| `Assets/Snow/Shaders/SnowHeightProbe.compute` | Eşlik testinin GPU tarafı | **YENİ** |
| `Assets/Snow/Editor/SnowHeightParityTest.cs` | GPU ↔ CPU eşlik testi | **YENİ** |
| `Assets/Snow/Runtime/SnowManager.cs` | `_SnowTessCameraPos` ve tessellation ayarlarını yayınlar | değişecek |
| `Assets/Snow/Runtime/SnowSettings.cs` | Tessellation ayarları | değişecek |
| `Assets/Snow/Runtime/SnowShaderIDs.cs` | Yeni shader ID'leri | değişecek |
| `Assets/Scripts/Player/GroundSnap.cs` | Karakteri kar yüzeyine oturtur | değişecek |
| `Assets/Scripts/Debug/DebugMenu.cs` | `_SnowDbgNoTess`, `_SnowDbgNoDrift` anahtarları | değişecek |

---

## Görev 1: Tessellation altyapısı — geçici sinüs dalgasıyla

**Amaç:** Boru hattının tamamını gerçek yükseklik alanına dokunmadan doğrulamak. Bu görev bittiğinde çatlak, gölge, performans ve izolasyon anahtarı sorularının hepsi kapanmış olur.

**Dosyalar:**
- Oluştur: `Assets/Snow/Shaders/SnowTessellation.hlsl`
- Değiştir: `Assets/Snow/Shaders/SnowCommon.hlsl`
- Değiştir: `Assets/Shaders/MountainSurface.shader` (yalnız ForwardLit geçişi)
- Değiştir: `Assets/Snow/Runtime/SnowShaderIDs.cs`
- Değiştir: `Assets/Snow/Runtime/SnowSettings.cs`
- Değiştir: `Assets/Snow/Runtime/SnowManager.cs`
- Değiştir: `Assets/Scripts/Debug/DebugMenu.cs`

- [ ] **Adım 1: Globalleri bildir**

`Assets/Snow/Shaders/SnowCommon.hlsl` içinde, `float _SnowCavityRadius;` satırının hemen altına:

```hlsl
// --------------------------------------------------------- tessellation

/// ANA KAMERANIN KONUMU. `_WorldSpaceCameraPos` GOLGE GECISINDE ISIGIN
/// konumunu tutuyor; bolme faktoru ondan hesaplanirsa golge geometrisi ileri
/// gecisinkiyle uyusmaz ve golge yuzeyden kayar.
float3 _SnowTessCameraPos;

/// En yuksek bolme faktoru. Donanim tavani 64; Terrain kose araligi 7.32 m
/// oldugu icin 64'te en ince geometri 11.4 cm oluyor.
float _SnowTessMax;

/// Faktorun tam oldugu mesafe (m).
float _SnowTessNear;

/// Faktorun 1'e indigi mesafe (m) — otesinde bolme yok.
float _SnowTessFar;

/// 1 iken bolme tamamen kapali: butun kenar faktorleri 1.
float _SnowDbgNoTess;
```

- [ ] **Adım 2: Tessellation dosyasını yaz**

`Assets/Snow/Shaders/SnowTessellation.hlsl` (yeni dosya):

```hlsl
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

    return lerp(1.0, _SnowTessMax, t);
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
```

- [ ] **Adım 3: ForwardLit geçişini tessellation'a bağla**

`Assets/Shaders/MountainSurface.shader`, ForwardLit geçişinde.

3a. `#pragma vertex Vertex` ve `#pragma target 3.5` satırlarını şununla değiştir:

```hlsl
            #pragma vertex SnowTessVertex
            #pragma hull SnowHull
            #pragma domain SnowDomain
            #pragma fragment Fragment
            #pragma target 5.0
```

3b. `#include "MountainSurface.hlsl"` satırının hemen altına:

```hlsl
            #include "../Snow/Shaders/SnowTessellation.hlsl"
```

3c. Mevcut `Varyings Vertex(Attributes IN)` fonksiyonunun **imzasını** değiştir — gövdesine dokunma:

```hlsl
            Varyings VertexFromWS(float3 positionWS)
            {
                Varyings OUT;
```

ve gövdenin başındaki şu satırı **sil** (artık parametre olarak geliyor):

```hlsl
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
```

3d. `VertexFromWS`'in kapanan `}` işaretinden sonra ekle:

```hlsl
            /// Hull oncesi gecis: yalniz dunya konumuna cevirir.
            SnowTessControlPoint SnowTessVertex(Attributes IN)
            {
                SnowTessControlPoint o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return o;
            }

            [domain("tri")]
            Varyings SnowDomain(SnowTessFactors factors,
                                OutputPatch<SnowTessControlPoint, 3> patch,
                                float3 bary : SV_DomainLocation)
            {
                return VertexFromWS(SnowTessKonum(patch, bary));
            }
```

- [ ] **Adım 4: Shader ID'lerini ekle**

`Assets/Snow/Runtime/SnowShaderIDs.cs`, `CavityRadius` tanımının altına:

```csharp
    // --- Tessellation ---
    public static readonly int TessCameraPos = Shader.PropertyToID("_SnowTessCameraPos");
    public static readonly int TessMax = Shader.PropertyToID("_SnowTessMax");
    public static readonly int TessNear = Shader.PropertyToID("_SnowTessNear");
    public static readonly int TessFar = Shader.PropertyToID("_SnowTessFar");
```

- [ ] **Adım 5: Ayarları ekle**

`Assets/Snow/Runtime/SnowSettings.cs`, `sparkleIntensity` alanının altına:

```csharp
    [Header("Tessellation")]
    [Tooltip("En yüksek bölme faktörü. Donanım tavanı 64; Terrain köşe " +
             "aralığı 7.32 m olduğu için 64'te en ince geometri 11.4 cm.")]
    [SerializeField, Range(1f, 64f)] float tessMax = 64f;

    [Tooltip("Faktörün tam olduğu mesafe (m).")]
    [SerializeField, Min(1f)] float tessNear = 15f;

    [Tooltip("Faktörün 1'e indiği mesafe (m). Ötesinde bölme yok.")]
    [SerializeField, Min(2f)] float tessFar = 60f;
```

Ayrıca `SparkleIntensity` özelliğinin yanına:

```csharp
    public float TessMax => tessMax;
    public float TessNear => tessNear;
    public float TessFar => tessFar;
```

- [ ] **Adım 6: Ayarları yayınla**

`Assets/Snow/Runtime/SnowManager.cs`, `Shader.SetGlobalVector(SnowShaderIDs.SastrugiWindDir, ...)` çağrısının hemen altına:

```csharp
        // TESSELLATION ANA KAMERAYI GÖRMEK ZORUNDA. Gölge geçişinde
        // `_WorldSpaceCameraPos` ışığın konumu; bölme faktörü ondan
        // hesaplanırsa gölge geometrisi ileri geçişinkiyle uyuşmaz ve gölge
        // yüzeyden kayar.
        if (Camera.main != null)
            Shader.SetGlobalVector(SnowShaderIDs.TessCameraPos,
                                   Camera.main.transform.position);

        Shader.SetGlobalFloat(SnowShaderIDs.TessMax, settings.TessMax);
        Shader.SetGlobalFloat(SnowShaderIDs.TessNear, settings.TessNear);
        Shader.SetGlobalFloat(SnowShaderIDs.TessFar, settings.TessFar);
```

- [ ] **Adım 7: İzolasyon anahtarını kur**

`Assets/Scripts/Debug/DebugMenu.cs`:

`Prob(ref kDuz, DuzId, "  NORMALI TAMAMEN DUZLESTIR");` satırının altına:

```csharp
                Prob(ref kTess,     TessId,     "  tessellation (geometri)");
```

`bool kFbm, kRipple, kSastrugi, kMicro, kLod, kTexN, kDuz;` satırını değiştir:

```csharp
    bool kFbm, kRipple, kSastrugi, kMicro, kLod, kTexN, kDuz, kTess;
```

`static readonly int DuzId = Shader.PropertyToID("_SnowDbgFlatNormal");` satırının altına:

```csharp
    static readonly int TessId     = Shader.PropertyToID("_SnowDbgNoTess");
```

`ProbIdleri` dizisinde `FbmId, RippleId, SastrugiId, MicroId, LodId, TexNId, DuzId,` yazan satırı değiştir:

```csharp
        FbmId, RippleId, SastrugiId, MicroId, LodId, TexNId, DuzId, TessId,
```

"Probları kapat" düğmesindeki sıfırlama satırını değiştir:

```csharp
                    kFbm = kRipple = kSastrugi = kMicro = kLod = kTexN = kDuz = kTess = false;
```

- [ ] **Adım 8: Derlemeyi doğrula**

```bash
cd /d "D:\ME\game\to the summit" && date > Logs/refresh.trigger
```

12 saniye bekle, sonra Unity MCP `RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        string y = "Assets/Shaders/MountainSurface.shader";
        AssetDatabase.ImportAsset(y, ImportAssetOptions.ForceSynchronousImport);
        Shader sh = AssetDatabase.LoadAssetAtPath<Shader>(y);
        result.Log("hata: " + ShaderUtil.ShaderHasError(sh));
        foreach (var m in ShaderUtil.GetShaderMessages(sh))
            result.LogError(m.line + ": " + m.message + " | " + m.messageDetails);
    }
}
```

Beklenen: `hata: False`, hiç `[Error]` satırı yok.

- [ ] **Adım 9: Ekranda doğrula**

Kullanıcıdan Play'e basmasını iste. Sorulacaklar:

1. **Zemin dalgalı mı?** 1 m aralıklı, 20 cm yüksek düzenli tepecikler görünmeli. Görünmüyorsa tessellation hiç çalışmıyor.
2. **Ufka bak — silüet kırılıyor mu?** Tepecik profilleri ufuk çizgisini tırtıklı yapmalı. Bu, normal haritasının asla yapamadığı şey; **işin bütün amacı bu.**
3. **Çatlak var mı?** Yüzeyde dikey yarıklar, arasından gökyüzü veya siyah görünen boşluklar. **Olmamalı.** Varsa kenar faktörü hesabı yanlış yazılmış.
4. **FPS ve `Tri` sayacı ne?** Öncesi: 33K üçgen / 169 FPS. `Tri` 300K'yı aşıyorsa `tessMax` düşürülecek.
5. **F1 → "tessellation (geometri)" işaretlenince zemin düzleşiyor mu?** İzolasyon anahtarının çalıştığını doğrular.

- [ ] **Adım 10: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Shaders/SnowTessellation.hlsl Assets/Snow/Shaders/SnowCommon.hlsl Assets/Shaders/MountainSurface.shader Assets/Snow/Runtime/SnowShaderIDs.cs Assets/Snow/Runtime/SnowManager.cs Assets/Snow/Runtime/SnowSettings.cs Assets/Scripts/Debug/DebugMenu.cs && git commit -m "Tessellation altyapisi: ForwardLit gecisi, gecici sinus dalgasi"
```

---

## Görev 2: Diğer üç geçiş

**Amaç:** Gölge, derinlik ve normal tamponları ileri geçişle aynı geometriyi görsün. Biri eksik kalırsa gölge yüzeyden kayar, SSAO çöp okur.

**Dosyalar:**
- Değiştir: `Assets/Shaders/MountainSurface.shader` (ShadowCaster, DepthOnly, DepthNormals)

- [ ] **Adım 1: ShadowCaster**

Pragma bloğunu değiştir:

```hlsl
            #pragma vertex SnowTessVertex
            #pragma hull SnowHull
            #pragma domain SnowDomain
            #pragma fragment ShadowFragment
            #pragma target 5.0
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
```

`#include "MountainSurfaceInput.hlsl"` altına ekle:

```hlsl
            #include "../Snow/Shaders/SnowTessellation.hlsl"
```

`Varyings Vertex(Attributes IN)` imzasını `Varyings VertexFromWS(float3 positionWS)` yap ve gövdedeki `float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);` satırını sil.

Fonksiyonun kapanışından sonra ekle:

```hlsl
            SnowTessControlPoint SnowTessVertex(Attributes IN)
            {
                SnowTessControlPoint o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return o;
            }

            [domain("tri")]
            Varyings SnowDomain(SnowTessFactors factors,
                                OutputPatch<SnowTessControlPoint, 3> patch,
                                float3 bary : SV_DomainLocation)
            {
                return VertexFromWS(SnowTessKonum(patch, bary));
            }
```

- [ ] **Adım 2: DepthOnly**

Pragma:

```hlsl
            #pragma vertex SnowTessVertex
            #pragma hull SnowHull
            #pragma domain SnowDomain
            #pragma fragment DepthFragment
            #pragma target 5.0
```

`#include "MountainSurfaceInput.hlsl"` altına `#include "../Snow/Shaders/SnowTessellation.hlsl"`.

Vertex bloğu şu hale gelir:

```hlsl
            Varyings VertexFromWS(float3 positionWS)
            {
                Varyings OUT;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            SnowTessControlPoint SnowTessVertex(Attributes IN)
            {
                SnowTessControlPoint o;
                o.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                return o;
            }

            [domain("tri")]
            Varyings SnowDomain(SnowTessFactors factors,
                                OutputPatch<SnowTessControlPoint, 3> patch,
                                float3 bary : SV_DomainLocation)
            {
                return VertexFromWS(SnowTessKonum(patch, bary));
            }
```

- [ ] **Adım 3: DepthNormals**

Aynı dönüşüm: `#pragma fragment frag` korunur, `#pragma target 5.0` olur, `vertex`/`hull`/`domain` pragmaları eklenir, include eklenir, `Vertex` → `VertexFromWS` + iki yeni fonksiyon (Adım 2'deki gövdenin aynısı, yalnız `Varyings` yapısı bu geçişinki).

- [ ] **Adım 4: Derlemeyi doğrula**

Görev 1 Adım 8'deki komutun aynısı. Beklenen: `hata: False`.

- [ ] **Adım 5: Ekranda doğrula**

Play. Sorulacaklar:

1. **Gölge yüzeyle uyuşuyor mu?** Tepeciklerin gölgesi tepeciklerin dibinden başlamalı. Kaymışsa ShadowCaster tessellate edilmemiş.
2. **Zeminde kafes/ızgara deseni var mı?** Varsa DepthNormals eksik — SSAO çöp okuyor.

- [ ] **Adım 6: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Shaders/MountainSurface.shader && git commit -m "Tessellation dort gecise de baglandi: golge, derinlik, normal"
```

---

## Görev 3: Gerçek yükseklik alanına bağla

**Amaç:** Geçici sinüsü `SnowYuzeyRolyef` ile değiştirmek.

**Dosyalar:**
- Değiştir: `Assets/Snow/Shaders/SnowTessellation.hlsl`
- Değiştir: `Assets/Snow/Shaders/SnowConstants.hlsl`

- [ ] **Adım 1: Terrain köşe aralığı sabitini ekle**

`Assets/Snow/Shaders/SnowConstants.hlsl`, `SNOW_ICE_F0` tanımının hemen üstüne:

```hlsl
/// TERRAIN KOSE ARALIGI (m). Olculdu: arazi 30000 m, heightmap cozunurlugu
/// 4097 -> 30000/4096 = 7.32 m.
///
/// Tessellation faktoru 64'te (donanim tavani) en ince geometri 7.32/64 =
/// 11.4 cm. Bu bir TAVAN: alt-11-cm hicbir sey geometri olamaz, normal
/// haritasinda kalir. `SNOW_TESS_MIN_DALGA` o tavandan tureniyor.
///
/// DAGIN BOYUNA BAGLI — `SCALE.md`'de kayitli.
#define SNOW_TERRAIN_VERTEX_SPACING    7.32

```

- [ ] **Adım 2: Kar derinliğini veren fonksiyonun adını ölç**

```bash
cd /d "D:\ME\game\to the summit" && grep -rn "karDerinligi\|karKalinligi" "Assets/Shaders/MountainSurface.hlsl" | head -5
```

ve

```bash
cd /d "D:\ME\game\to the summit" && grep -rn "float SnowDepthAt\|SnowDepthAt(" Assets/Snow/Shaders/SnowCommon.hlsl | head -3
```

Çıkan gerçek fonksiyon adı bir sonraki adımda `SnowDepthAt` yerine kullanılır. `MountainSurface.hlsl` içinde `karKalinligi` nasıl hesaplanıyorsa aynı yol izlenir — ikinci bir kaynak kurulmaz.

- [ ] **Adım 3: Geçici sinüsü sil, gerçek alanı bağla**

`SnowTessellation.hlsl` içindeki `SnowTessYerDegistirme`'yi tamamen değiştir:

```hlsl
/// KAR YUZEYININ YUKSEKLIGI — GORSEL VE FIZIK AYNI FONKSIYONU OKUYOR.
///
/// `SnowYuzeyRolyef` piksel ayak izi istiyor cunku analitik gurultu
/// mip'lenmiyor. KOSE ASAMASINDA piksel ayak izi YOK: `fwidth` yalniz
/// fragment'te tanimli. Yerine KOSE ARALIGI veriliyor — bolme faktoru ne
/// kadar yuksekse kose o kadar sik, yani ornekleme frekansi o kadar yuksek.
/// Ikisi ayni isi goruyor: bir dalganin tasinabilmesi icin dalga boyu ornek
/// araliginin iki kati olmali.
float SnowTessYerDegistirme(float3 posWS)
{
    if (_SnowDbgNoTess > 0.5) return 0.0;

    float d = distance(posWS, _SnowTessCameraPos);
    float t = saturate((_SnowTessFar - d) / max(_SnowTessFar - _SnowTessNear, 1e-3));
    float faktor = lerp(1.0, _SnowTessMax, t);

    // Terrain kose araligi / bolme faktoru = yeni kose araligi.
    float koseAraligi = SNOW_TERRAIN_VERTEX_SPACING / max(faktor, 1.0);

    float karDerinligi = SnowDepthAt(posWS);   // <- Adim 2'de olculen gercek ad

    return SnowYuzeyRolyef(posWS.xz, koseAraligi, karDerinligi);
}
```

- [ ] **Adım 4: Include sırasını düzelt**

`SnowTessellation.hlsl` `SnowYuzeyRolyef`'i çağırıyor; o `SnowRelief.hlsl`'de. Dosyanın başındaki include bloğunu değiştir:

```hlsl
#include "SnowCommon.hlsl"
#include "SnowRelief.hlsl"
```

- [ ] **Adım 5: Derlemeyi doğrula**

Görev 1 Adım 8'deki komut. Beklenen: `hata: False`.

Derleme hatası `undeclared identifier "SnowYuzeyRolyef"` verirse: `MountainSurface.shader` içinde `SnowTessellation.hlsl` include'u `MountainSurface.hlsl`'den **sonra** gelmeli — o zaten `SnowRelief.hlsl`'i çekiyor.

- [ ] **Adım 6: Ekranda doğrula**

Play. Sorulacaklar:

1. **Düzenli sinüs deseni gitti mi?** Yerine düzensiz, doğal tepecikler gelmeli.
2. **Tepecikler ne kadar yüksek?** Şu an sastrugi genliği 1 cm — muhtemelen **neredeyse hiç** görünmeyecek. Bu **beklenen**; Görev 7 genliği arazi ölçüsüne çıkaracak.
3. **Çatlak var mı?**

Bu görevin başarı ölçütü "güzel görünmesi" değil, **düzensiz ve çatlaksız** olması.

- [ ] **Adım 7: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Shaders/SnowTessellation.hlsl Assets/Snow/Shaders/SnowConstants.hlsl && git commit -m "Yer degistirme gercek yukseklik alanindan"
```

---

## Görev 4: Yer değiştirme LOD eşiği

**Amaç:** 11.4 cm'lik geometri tavanının altındaki oktavlar geometriye girmesin — girerlerse örneklenemeyip titrerler. Normal haritasında kalmaları doğru.

**Dosyalar:**
- Değiştir: `Assets/Snow/Shaders/SnowConstants.hlsl`
- Değiştir: `Assets/Snow/Shaders/SnowRelief.hlsl`
- Değiştir: `Assets/Snow/Shaders/SnowTessellation.hlsl`

- [ ] **Adım 1: Eşik sabitini ekle**

`SnowConstants.hlsl`, `SNOW_TERRAIN_VERTEX_SPACING` tanımının hemen altına:

```hlsl
/// GEOMETRIYE GIREN EN KISA DALGA BOYU (m).
///
/// Kagitta: en ince geometri 11.4 cm (7.32 m / 64). Nyquist bir dalganin
/// tasinabilmesi icin dalga boyunun ornek araliginin iki kati olmasini
/// istiyor -> 22.8 cm. Guvenlik payiyla 50 cm.
///
/// Bunun altindaki oktavlar (ripple 17 cm, mikro 8.3 cm) YER DEGISTIRMEYE
/// GIRMIYOR, normal haritasinda kaliyor. Girerlerse ornekleme frekansinin
/// altinda kalip kamera kipirdadikca titrerler — belirti bir kez olculdu
/// ("zemin tir tir titriyor").
#define SNOW_TESS_MIN_DALGA            0.50
```

- [ ] **Adım 2: Kipli oktav ağırlığı yardımcısını ekle**

`SnowRelief.hlsl`, `SnowOktavAgirligi` fonksiyonunun hemen altına:

```hlsl
/// Geometri kipinde `SNOW_TESS_MIN_DALGA`'nin altindaki oktav tamamen
/// kapaniyor: o dalga boyu kose araliginin altinda kaliyor ve tasinamiyor.
/// Piksel kipinde eski davranis aynen suruyor.
float SnowOktavAgirligiKipli(float dalgaBoyu, float pikselBoyu, bool yalnizGeometri)
{
    if (yalnizGeometri && dalgaBoyu < SNOW_TESS_MIN_DALGA) return 0.0;

    return SnowOktavAgirligi(dalgaBoyu, pikselBoyu);
}
```

- [ ] **Adım 3: `SnowYuzeyRolyef` imzasına kip ekle**

```hlsl
float SnowYuzeyRolyef(float2 worldXZ, float pikselBoyu, float karDerinligi,
                      bool yalnizGeometri)
```

Fonksiyonun içindeki **her** `SnowOktavAgirligi(X, pikselBoyu)` çağrısını `SnowOktavAgirligiKipli(X, pikselBoyu, yalnizGeometri)` yap. Üç yer var: fBm döngüsü, ripple, sastrugi.

- [ ] **Adım 4: Çağıranları güncelle**

```bash
cd /d "D:\ME\game\to the summit" && grep -rn "SnowYuzeyRolyef(" Assets/ --include=*.hlsl
```

Her çağrıya son parametre eklenir:
- `SnowTessellation.hlsl` içindeki çağrı → `true`
- `SnowRelief.hlsl` → `SnowYuzeyEgim` içindeki dört çağrı → `false`

- [ ] **Adım 5: Derlemeyi doğrula**

Görev 1 Adım 8'deki komut. Beklenen: `hata: False`.

- [ ] **Adım 6: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Shaders/SnowConstants.hlsl Assets/Snow/Shaders/SnowRelief.hlsl Assets/Snow/Shaders/SnowTessellation.hlsl && git commit -m "Yer degistirmeye 50 cm dalga boyu esigi: alti normal haritasinda kaliyor"
```

---

## Görev 5: Drift katmanı

**Amaç:** Fotoğraftaki yumuşak birikme tepeciklerini üretmek. Sastrugi bir **erozyon** şekli (keskin, rüzgâra paralel); drift bir **birikme** şekli (yuvarlak, yumuşak).

**Dosyalar:**
- Değiştir: `Assets/Snow/Shaders/SnowConstants.hlsl`
- Değiştir: `Assets/Snow/Shaders/SnowRelief.hlsl`
- Değiştir: `Assets/Snow/Shaders/SnowCommon.hlsl`
- Değiştir: `Assets/Scripts/Debug/DebugMenu.cs`

- [ ] **Adım 1: Sabitleri ekle**

`SnowConstants.hlsl`, `SNOW_SASTRUGI_WIND_TAU` tanımının hemen altına:

```hlsl
/// DRIFT — BIRIKME TEPECIKLERI.
///
/// Sastrugi erozyon sekli: ruzgar kari OYUYOR, keskin sirt ve dik yuz
/// birakiyor. Drift bunun tersi: ruzgarin tasidigi kar bir yerde COKUYOR ve
/// yuvarlak, yumusak tepecik birakiyor. Ikisi ayni yerde olmuyor.
///
/// [KAYNAK: Filhol & Sturm 2015, kar yer sekilleri sinifi — olculen aralik
/// 2 cm (ripple) ile 2.5 m (whaleback dune) arasi; drift tepecikleri bu
/// araligin ortasinda.]
///
/// Genlik tepe-dip 30 cm, dalga boyu 90 cm -> egim 2*pi*0.15/0.90 = 1.05,
/// yani 46 derece. Karin durus acisi 38-45 derece; drift o sinirin hemen
/// ustunde duruyor cunku birikme sirasinda kar kendini destekliyor
/// (kohezyon, `SNOW_STAND_LOOSE` ile ayni fizik).
#define SNOW_DRIFT_HEIGHT              0.30
#define SNOW_DRIFT_LENGTH              0.90

/// Ruzgar yonundeki uzama. Drift tepecikleri ruzgar boyunca uzuyor ama
/// sastrugi kadar degil (sastrugi 2.20 m).
#define SNOW_DRIFT_WIDTH               1.60
```

- [ ] **Adım 2: Teşhis anahtarını bildir**

`SnowCommon.hlsl`, `float _SnowDbgNoTess;` satırının altına:

```hlsl
/// 1 iken drift katmani kapali.
float _SnowDbgNoDrift;
```

- [ ] **Adım 3: Drift katmanını yaz**

`SnowRelief.hlsl`, `SnowYuzeyRolyef` içinde sastrugi bloğunun hemen **altına**, `return h;` satırından önce:

```hlsl
    // --- DRIFT: birikme tepecikleri, YUMUSAK ---
    //
    // Sastrugi `n*n*(3-2n)` ile ust yarisi duzlestirilip alt yarisi
    // diklestiriliyor (erozyon: dik ruzgarustu yuz). Drift'te o islem YOK —
    // ham deger yuvarlak tepe veriyor, birikmenin kendi bicimi bu.

    float2 pd = float2(dot(worldXZ, w)   / SNOW_DRIFT_WIDTH,
                       dot(worldXZ, dik) / SNOW_DRIFT_LENGTH);

    if (_SnowDbgNoDrift <= 0.5)
    h += (SnowValueNoise(pd) - 0.5) * min(SNOW_DRIFT_HEIGHT, tavan)
       * SnowOktavAgirligiKipli(SNOW_DRIFT_LENGTH, pikselBoyu, yalnizGeometri);

```

- [ ] **Adım 4: Teşhis anahtarını panele ekle**

`DebugMenu.cs`, Görev 1 Adım 7'deki desende:

`Prob(ref kTess, TessId, "  tessellation (geometri)");` satırının altına:

```csharp
                Prob(ref kDrift,    DriftId,    "  drift (birikme tepecikleri)");
```

`bool kFbm, ... kDuz, kTess;` satırını `..., kTess, kDrift;` yap.

`static readonly int TessId = ...` altına:

```csharp
    static readonly int DriftId    = Shader.PropertyToID("_SnowDbgNoDrift");
```

`ProbIdleri` dizisine `DriftId` ekle, sıfırlama satırına `= kDrift` ekle.

- [ ] **Adım 5: Derlemeyi doğrula**

Görev 1 Adım 8'deki komut. Beklenen: `hata: False`.

- [ ] **Adım 6: Ekranda doğrula**

Play. Sorulacak: **yumuşak, yuvarlak tepecikler görünüyor mu?** 30 cm yüksek, 90 cm aralık. Fotoğraftaki karakter bu.

Şu an drift **her yerde** çalışıyor — Görev 6 onu rüzgâr maruziyetine bağlayacak.

- [ ] **Adım 7: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Shaders/SnowConstants.hlsl Assets/Snow/Shaders/SnowRelief.hlsl Assets/Snow/Shaders/SnowCommon.hlsl Assets/Scripts/Debug/DebugMenu.cs && git commit -m "Drift katmani: yumusak birikme tepecikleri"
```

---

## Görev 6: Rüzgâr maruziyetiyle ayrışma

**Amaç:** Korunaklı yerde drift, maruz sırtta sastrugi. Bu aynı zamanda **RMS eğim bütçesini çözüyor**: aynı noktada iki katman birden olmadığı için ortalama eğim ölçülen 5–15° bandında kalıyor, yerel olarak 40–50°'ye çıkıyor.

**Dosyalar:**
- Değiştir: `Assets/Snow/Shaders/SnowRelief.hlsl`
- Değiştir: `Assets/Snow/Shaders/SnowTessellation.hlsl`
- Değiştir: `Assets/Shaders/MountainSurface.hlsl`

- [ ] **Adım 1: İmzaya maruziyet ekle**

`SnowRelief.hlsl`:

```hlsl
float SnowYuzeyRolyef(float2 worldXZ, float pikselBoyu, float karDerinligi,
                      bool yalnizGeometri, float maruziyet)
```

`float tavan = karDerinligi * SNOW_BEDFORM_DEPTH_FRAC;` satırının hemen altına:

```hlsl
    // MARUZIYET IKI SEKLI AYIRIYOR.
    //
    // Sastrugi EROZYON sekli ve olusumu 20 m/s ustu ruzgar istiyor; ruzgarin
    // supurdugu acik sirtta olusuyor. Drift BIRIKME sekli ve ruzgarin
    // yavasladigi siperde cokuyor. Ayni noktada ikisi birden olmuyor.
    //
    // Spec 18.0 bunu zaten soyluyor: ruzgar golgesinde asinma tamamen kapali
    // ("curvW sifirlanir -> asinma yok, sadece birikme").
    //
    // YAN KAZANC — RMS EGIM BUTCESI. Iki katman ayni yerde toplansaydi
    // yuzeyin toplam egimi olculen 5-15 derece bandini iki kat asardi
    // (`RATIONALE.md` -> "Sastrugi arazi olcusune cikarilamadi"). Ayrildiklari
    // icin ortalama bantta kaliyor, yerel olarak 40-50 dereceye cikiyor.
    float sastrugiPay = maruziyet;
    float driftPay    = 1.0 - maruziyet;
```

Sastrugi satırını değiştir:

```hlsl
    h += (ns - 0.5) * min(SNOW_SASTRUGI_HEIGHT * SNOW_SASTRUGI_BASE, tavan) * sastrugiPay
       * SnowOktavAgirligiKipli(SNOW_SASTRUGI_LENGTH, pikselBoyu, yalnizGeometri);
```

Drift satırını değiştir:

```hlsl
    h += (SnowValueNoise(pd) - 0.5) * min(SNOW_DRIFT_HEIGHT, tavan) * driftPay
       * SnowOktavAgirligiKipli(SNOW_DRIFT_LENGTH, pikselBoyu, yalnizGeometri);
```

- [ ] **Adım 2: Tessellation maruziyeti geçirsin**

`SnowTessellation.hlsl` içinde `SnowTessYerDegistirme`'nin son satırını değiştir:

```hlsl
    // Maruziyet `SampleWindShadow`'un TERSI: o fonksiyon korunakliligi
    // olcuyor (spec 18.0: "> 0 -> golgede"). Ayni cevirme
    // `SnowSurfaceWeights`'te de yapiliyor, iki yer ayni yonu okumak zorunda.
    float maruziyet = 1.0 - saturate(SampleWindShadow(posWS) * 1.2);

    return SnowYuzeyRolyef(posWS.xz, koseAraligi, karDerinligi, true, maruziyet);
```

- [ ] **Adım 3: `SnowYuzeyEgim` imzasına yükseklik ekle**

`SampleWindShadow` 3B konum istiyor, `SnowYuzeyEgim` yalnız `worldXZ` alıyor.

```hlsl
half2 SnowYuzeyEgim(float2 worldXZ, float yerY, float karDerinligi, out float yukseklik)
```

Gövdenin başına, `pikselBoyu` hesabının altına:

```hlsl
    float maruziyet = 1.0 - saturate(
        SampleWindShadow(float3(worldXZ.x, yerY, worldXZ.y)) * 1.2);
```

Dört `SnowYuzeyRolyef` çağrısına `false, maruziyet` eklenir.

- [ ] **Adım 4: `SnowYuzeyEgim` çağıranını güncelle**

`Assets/Shaders/MountainSurface.hlsl`, 655 satırı civarı:

```hlsl
            half2 yuzeyEgim = SnowYuzeyEgim(izPos.xz, izPos.y, karKalinligi, yuzeyYuksekligi)
```

- [ ] **Adım 5: Derlemeyi doğrula**

Görev 1 Adım 8'deki komut. Beklenen: `hata: False`.

- [ ] **Adım 6: Ekranda doğrula**

Play. Sorulacak: **korunaklı ve maruz alanlar farklı mı görünüyor?** Sırtlarda keskin sastrugi, kuytularda yumuşak drift. Rüzgâr gölgesi arazi biçiminden geldiği için etki yavaş değişir; ani sınır **olmamalı**.

- [ ] **Adım 7: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Shaders/SnowRelief.hlsl Assets/Snow/Shaders/SnowTessellation.hlsl Assets/Shaders/MountainSurface.hlsl && git commit -m "Drift ve sastrugi ruzgar maruziyetiyle ayrildi"
```

---

## Görev 7: Genliği arazi ölçüsüne çıkar

**Amaç:** Sastrugi şu an `HEIGHT × BASE = 0.180 × 0.055 = 1.0 cm`. Arazi ölçümü 15–40 cm. `BASE` gereksiz ikinci kısıt — `tavan` (kar derinliği × 0.60) zaten sığ karı koruyor ve 20 cm karda 12 cm veriyor; sastrugi ona hiç değmiyor.

**Dosyalar:**
- Değiştir: `Assets/Snow/Shaders/SnowConstants.hlsl`
- Değiştir: `Assets/Snow/Shaders/SnowRelief.hlsl`

- [ ] **Adım 1: `SNOW_SASTRUGI_BASE`'i sil, `HEIGHT`'i arazi ölçüsüne getir**

`SnowConstants.hlsl` içinde `SNOW_SASTRUGI_BASE` tanımını ve üstündeki yorum bloğunu tamamen sil. `SNOW_SASTRUGI_HEIGHT` bloğunu şununla değiştir:

```hlsl
/// Olculen sastrugi derinligi 15-40 cm, sivri uc araligi 45-90 cm.
/// [KAYNAK: Filhol & Sturm 2015.]
///
/// 0.180 x BASE 0.055 = 1.0 cm idi, arazi olcusunun 15-40 kati altinda.
/// `BASE` gereksiz ikinci kisitti: `tavan` (kar derinligi x 0.60) sig kari
/// zaten koruyor ve 20 cm karda 12 cm veriyor — sastrugi ona hic degmiyordu.
///
/// 0.20 = araligin alt ucu. Egim 2*pi*0.10/0.60 = 1.05, yani 46 derece.
/// Karin durus acisi 38-45 derece ama sastrugi bir EROZYON sekli: ruzgar
/// oydugu ve yuzey sertlestigi icin durus acisinin uzerinde durabiliyor.
///
/// LENGTH RUZGARA DIK EKSENDE, WIDTH RUZGAR YONUNDE (`SnowYuzeyRolyef`).
/// Bir tur LENGTH 0.60 -> 2.00 yapildi "egim cok dik" diye; YANLIS EKSENDI
/// ve sastrugiyi enine sisirip yonsuzlestirdi. Geri alindi.
#define SNOW_SASTRUGI_HEIGHT         0.20
#define SNOW_SASTRUGI_LENGTH         0.60
#define SNOW_SASTRUGI_WIDTH          2.20
```

- [ ] **Adım 2: `SNOW_RIPPLE_BASE`'i sil**

Aynı gerekçe. `AMP × BASE = 0.012 × 0.24 = 0.29 cm`; arazi 0.5–2 cm.

```hlsl
/// RIPPLE. Olculen: 0.5-2 cm yuksek, 10-25 cm dalga boyu, ruzgara DIK.
///
/// 0.012 x BASE 0.24 = 0.29 cm idi. `BASE` `SNOW_SASTRUGI_BASE` ile ayni
/// gerekceyle silindi: `tavan` sig kari zaten koruyor.
///
/// 0.006 = tepe-dip 1.2 cm (arazi araliginin ortasi), 17 cm dalga boyunda
/// egim 2*pi*0.006/0.17 = 0.22, yani 12.5 derece.
///
/// Ripple 17 cm dalga boyuyla `SNOW_TESS_MIN_DALGA` (50 cm) esiginin
/// altinda: geometriye girmiyor, normal haritasinda kaliyor.
#define SNOW_RIPPLE_AMP              0.006
#define SNOW_RIPPLE_LENGTH           0.17
```

- [ ] **Adım 3: Kullanım yerlerini düzelt**

`SnowRelief.hlsl` ripple satırı:

```hlsl
    h += (SnowValueNoise(pr) * 2.0 - 1.0) * min(SNOW_RIPPLE_AMP, tavan)
       * SnowOktavAgirligiKipli(SNOW_RIPPLE_LENGTH, pikselBoyu, yalnizGeometri);
```

Sastrugi satırı (Görev 6 Adım 1'de yazılan hâlinden `* SNOW_SASTRUGI_BASE` çıkarılır):

```hlsl
    h += (ns - 0.5) * min(SNOW_SASTRUGI_HEIGHT, tavan) * sastrugiPay
       * SnowOktavAgirligiKipli(SNOW_SASTRUGI_LENGTH, pikselBoyu, yalnizGeometri);
```

- [ ] **Adım 4: Kalıntı taraması**

```bash
cd /d "D:\ME\game\to the summit" && grep -rn "SNOW_SASTRUGI_BASE\|SNOW_RIPPLE_BASE" Assets/ || echo "kalinti yok"
```

Beklenen: `kalinti yok`.

- [ ] **Adım 5: Sabit eşliği testini çalıştır**

Unity MCP `RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool ok;
        string rapor = SnowConstantsTest.Run(out ok);
        result.Log("ESLIK OK: " + ok);
        result.Log(rapor);
    }
}
```

Beklenen: `ESLIK OK: True`.

- [ ] **Adım 6: Ekranda doğrula**

Play. Sorulacaklar:

1. **Tepecikler artık fotoğraftaki ölçekte mi?** 15–30 cm yüksek olmalı.
2. **Silüet belirgin kırılıyor mu?** Bu görevden sonra ufuk çizgisi net tırtıklı olmalı.
3. **Zemin titriyor mu?** Titriyorsa `SNOW_TESS_MIN_DALGA` yükseltilecek.

- [ ] **Adım 7: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Shaders/SnowConstants.hlsl Assets/Snow/Shaders/SnowRelief.hlsl && git commit -m "Sastrugi ve ripple genligi arazi olcusune cikti, BASE kisitlari silindi"
```

---

## Görev 8: İz çift sayımı

**Amaç:** Ayak izi hem yer değiştirmede hem relief mapping'de oyulursa iki kat derin görünür. Ölçerek karar vermek — tahminle değil.

**Dosyalar:**
- Değiştir (ölçüme göre): `Assets/Snow/Shaders/SnowTessellation.hlsl`
- Değiştir (ölçüme göre): `Assets/Shaders/MountainSurface.hlsl`

- [ ] **Adım 1: İzin şu an nerede olduğunu ölç**

```bash
cd /d "D:\ME\game\to the summit" && grep -n "SnowDentAt" Assets/Snow/Shaders/SnowRelief.hlsl | head -10
```

`SnowYuzeyRolyef` izi okumuyor — iz ayrı bir alan (`SnowDentAt`) ve yalnız `SnowReliefOffset` üzerinden doku uzayında uygulanıyor. Yani şu an yer değiştirmede **iz yok**.

- [ ] **Adım 2: Ekranda ölç**

Play. Kullanıcı karda yürür ve şuna bakar: **iz görünüyor mu, yoksa çevresindeki tepecikler onu yutuyor mu?**

Tepecikler 20–30 cm, iz derinliği 5–15 cm. İz muhtemelen kaybolmuş olacak.

- [ ] **Adım 3: İz kaybolduysa yer değiştirmeye ekle**

`SnowTessellation.hlsl` içinde `SnowTessYerDegistirme`'nin son satırını değiştir:

```hlsl
    // IZ DE GEOMETRIYE GIRIYOR.
    //
    // Cevresindeki kar 20-30 cm tepecikler halinde yukselirken iz duz kalirsa
    // iz gorunmez oluyor: 10 cm'lik bir cukur 30 cm'lik tepeciklerin arasinda
    // secilmiyor. Iz de ayni geometriye girmek zorunda.
    float iz = SnowDentAt(SnowWorldToUV(posWS));

    return SnowYuzeyRolyef(posWS.xz, koseAraligi, karDerinligi, true, maruziyet) - iz;
```

Ve `MountainSurface.hlsl` içindeki `SnowReliefOffset` çağrısı (529 satırı civarı) **kaldırılır** — aynı çukur iki kez oyulmasın. Kaldırılan satırın yerine gerekçe yorumu yazılır:

```hlsl
        // RELIEF MAPPING KALKTI — IZ ARTIK GEOMETRI.
        //
        // Doku uzayinda paralaks ile oyuluyordu; yer degistirme geldikten
        // sonra ayni cukur iki kez uygulaniyor ve iz iki kat derin
        // gorunuyordu. Gercek geometri paralaksin verdigi her seyi zaten
        // veriyor, ustune silueti de kiriyor.
```

**İz hâlâ görünüyorsa** bu adım atlanır ve gerekçesi `RATIONALE.md`'ye yazılır.

- [ ] **Adım 4: Derlemeyi doğrula ve ekranda kontrol et**

Görev 1 Adım 8'deki komut, sonra Play.

Sorulacaklar:
1. **İz görünüyor mu?**
2. **İz iki kat derin mi?** Öyleyse relief mapping kaldırılmamış.
3. **İzin kenarları pürüzlü mü, yoksa geometrik basamak mı var?** Basamak varsa `SNOW_TESS_MIN_DALGA` izin genişliğinden büyük.

- [ ] **Adım 5: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Shaders/SnowTessellation.hlsl Assets/Shaders/MountainSurface.hlsl && git commit -m "Ayak izi yer degistirmeye girdi, relief mapping cift sayimi kalkti"
```

---

## Görev 9: Yükseklik fonksiyonunun C# ikizi

**Amaç:** Fizik tarafının aynı yüzeyi görmesi. Bu görev atlanırsa karakter tepeciklerin içinden geçer — **bu hata bu projede bir kez yapıldı** (planın başındaki alıntı).

**Dosyalar:**
- Oluştur: `Assets/Snow/Runtime/SnowSurfaceHeight.cs`
- Değiştir: `Assets/Snow/Runtime/SnowConstants.cs`
- Değiştir: `Assets/Snow/Editor/SnowConstantsTest.cs`

- [ ] **Adım 1: HLSL gürültü ve rölyef gövdelerini oku**

```bash
cd /d "D:\ME\game\to the summit" && grep -n -A 30 "float SnowValueNoise" Assets/Snow/Shaders/SnowCommon.hlsl
```

```bash
cd /d "D:\ME\game\to the summit" && sed -n '/float SnowYuzeyRolyef/,/^}/p' Assets/Snow/Shaders/SnowRelief.hlsl
```

C# ikizi bu iki fonksiyonun **birebir** aynısı olacak — aynı hash sabitleri, aynı harmanlama eğrisi, aynı oktav sırası. Tek bit farkı eşlik testini kırar. **Formül yeniden türetilmez, satır satır çevrilir.**

- [ ] **Adım 2: Sabitleri C# tarafına taşı**

Yeni sabitler `SnowConstants.cs`'e eklenir ve `SnowConstantsTest.cs`'in `Pairs` tablosuna çift olarak yazılır:

```csharp
        ("TerrainVertexSpacing", "SNOW_TERRAIN_VERTEX_SPACING"),
        ("TessMinDalga", "SNOW_TESS_MIN_DALGA"),
        ("DriftHeight", "SNOW_DRIFT_HEIGHT"),
        ("DriftLength", "SNOW_DRIFT_LENGTH"),
        ("DriftWidth", "SNOW_DRIFT_WIDTH"),
        ("SastrugiHeight", "SNOW_SASTRUGI_HEIGHT"),
        ("SastrugiLength", "SNOW_SASTRUGI_LENGTH"),
        ("SastrugiWidth", "SNOW_SASTRUGI_WIDTH"),
        ("RippleAmp", "SNOW_RIPPLE_AMP"),
        ("RippleLength", "SNOW_RIPPLE_LENGTH"),
        ("FbmAmp", "SNOW_FBM_AMP"),
        ("FbmScale", "SNOW_FBM_SCALE"),
        ("FbmGain", "SNOW_FBM_GAIN"),
        ("BedformDepthFrac", "SNOW_BEDFORM_DEPTH_FRAC"),
```

Karşılıkları `SnowConstants.cs`'e HLSL'deki değerlerle birebir yazılır. Bu, iki tarafın sabit düzeyinde ayrışmasını testte yakalar.

- [ ] **Adım 3: İkizi yaz**

`Assets/Snow/Runtime/SnowSurfaceHeight.cs`:

```csharp
// ROL: kar yüzeyinin yüksekliğini CPU'da verir. `SnowRelief.hlsl` içindeki
// `SnowYuzeyRolyef`'in birebir ikizi.
// Çağıran: GroundSnap (karakteri yüzeye oturtur).

using UnityEngine;

/// GÖRSEL VE FİZİK AYNI YÜZEYİ GÖRMEK ZORUNDA.
///
/// Kar yüksekliği bir kez geometriye konmuş ve fizik tarafında karşılığı
/// olmadığı için geri alınmıştı: "ayak 205.539, kaya 205.489, çizilen yüzey
/// 205.98 — karakter yarım metre gömülü başlıyordu" (`MountainSurface.shader`
/// yorumu). Bu sınıf o boşluğu kapatıyor.
///
/// DUBLİKASYON BİLİNÇLİ VE SINANIYOR. Aynı formül iki dilde iki kez
/// yazılıyor; sapma `SnowHeightParityTest` ile yakalanıyor. Alternatifler
/// daha kötü: GPU'dan async geri okuma bir kare gecikmeli (karakter geçen
/// karenin yüzeyinde durur), senkron okuma boru hattını durdurup kare
/// süresini patlatır.
///
/// CO-OP: fonksiyon SAF. Girdisi yalnız dünya konumu, kar derinliği, rüzgâr
/// yönü ve maruziyet; kare sayacı, `Time` ve yerel rastgelelik YOK. Bu yüzden
/// her istemci aynı XZ'de aynı yüksekliği hesaplıyor ve ağ üzerinden yükseklik
/// paylaşmak gerekmiyor. Kural `COOP.md`'de yazılı ve bozulamaz.
public static class SnowSurfaceHeight
{
    /// `SnowCommon.hlsl` içindeki hash'in ikizi. Sabitler oradan kopyalanır,
    /// yeniden türetilmez.
    static float Hash21(Vector2 p)
    {
        // ADIM 1'DE OKUNAN HLSL GÖVDESİNİN BİREBİR C# KARŞILIĞI.
        // Uygulayan ajan burayı okuduğu koddan çevirir.
        return 0f;
    }

    /// `SnowCommon.hlsl` → `SnowValueNoise`'un ikizi.
    static float ValueNoise(Vector2 p)
    {
        Vector2 i = new(Mathf.Floor(p.x), Mathf.Floor(p.y));
        Vector2 f = new(p.x - i.x, p.y - i.y);

        Vector2 u = new(f.x * f.x * (3f - 2f * f.x),
                        f.y * f.y * (3f - 2f * f.y));

        float a = Hash21(i);
        float b = Hash21(i + Vector2.right);
        float c = Hash21(i + Vector2.up);
        float d = Hash21(i + Vector2.one);

        return Mathf.Lerp(Mathf.Lerp(a, b, u.x), Mathf.Lerp(c, d, u.x), u.y);
    }

    /// `SnowRelief.hlsl` → `SnowYuzeyRolyef`'in ikizi, geometri kipinde.
    ///
    /// `pikselBoyu` yerine 0 geçiliyor: CPU tarafında örnekleme frekansı
    /// sonsuz, LOD kesimi yok. Geometri eşiği (`SNOW_TESS_MIN_DALGA`) yine
    /// uygulanıyor — fizik yüzeyi GEOMETRİK yüzeyle aynı olmak zorunda,
    /// normal haritasındaki ince oktavlarla değil.
    public static float Rolyef(Vector2 worldXZ, float karDerinligi,
                               Vector2 sastrugiWindDir, float maruziyet)
    {
        // `SnowYuzeyRolyef`'in gövdesinin birebir C# karşılığı:
        // tavan → fBm dört oktav → ripple → sastrugi → drift.
        // Aynı sıra, aynı sabitler (`SnowConstants.cs`'ten okunur).
        return 0f;
    }
}
```

**Bu görevi uygulayan ajana:** `Hash21` ve `Rolyef` gövdeleri Adım 1'de okunan HLSL'den birebir çevrilerek doldurulur. `return 0f;` satırları kalırsa Görev 10'daki eşlik testi kırmızı verir ve bu **doğru davranıştır** — test tam da bunun için var.

- [ ] **Adım 4: Derlemeyi doğrula**

```bash
cd /d "D:\ME\game\to the summit" && date > Logs/refresh.trigger
```

12 sn bekle, Unity MCP `RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool ok;
        string rapor = SnowConstantsTest.Run(out ok);
        result.Log("SABIT ESLIGI: " + ok);
        result.Log(rapor);
    }
}
```

Beklenen: `SABIT ESLIGI: True` — yeni sabit çiftleri de eşleşiyor.

- [ ] **Adım 5: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Runtime/SnowSurfaceHeight.cs Assets/Snow/Runtime/SnowConstants.cs Assets/Snow/Editor/SnowConstantsTest.cs && git commit -m "Kar yuzeyi yukseklik fonksiyonunun C# ikizi"
```

---

## Görev 10: Eşlik testi

**Amaç:** C# ikizinin GPU ile aynı sonucu verdiğini kanıtlamak. Bu test olmadan ikiz sessizce sapar ve karakter yavaş yavaş yüzeyden ayrılır.

**Dosyalar:**
- Oluştur: `Assets/Snow/Shaders/SnowHeightProbe.compute`
- Oluştur: `Assets/Snow/Editor/SnowHeightParityTest.cs`

- [ ] **Adım 1: Compute shader — GPU tarafını okunabilir yap**

`Assets/Snow/Shaders/SnowHeightProbe.compute`:

```hlsl
// ROL: verilen dunya konumlarinda `SnowYuzeyRolyef`'i calistirip sonucu
// tampona yazar. Yalniz eslik testi kullaniyor.
#pragma kernel KHeightProbe

#include "SnowRelief.hlsl"

StructuredBuffer<float2> _ProbePositions;
StructuredBuffer<float>  _ProbeDepths;
StructuredBuffer<float>  _ProbeExposure;
RWStructuredBuffer<float> _ProbeResult;

int _ProbeCount;

[numthreads(64, 1, 1)]
void KHeightProbe(uint3 id : SV_DispatchThreadID)
{
    if ((int)id.x >= _ProbeCount) return;

    _ProbeResult[id.x] = SnowYuzeyRolyef(_ProbePositions[id.x], 0.0,
                                         _ProbeDepths[id.x], true,
                                         _ProbeExposure[id.x]);
}
```

- [ ] **Adım 2: Testi yaz**

`Assets/Snow/Editor/SnowHeightParityTest.cs`:

```csharp
// ROL: kar yüzeyi yükseklik fonksiyonunun GPU ve CPU sürümlerinin aynı
// sonucu verdiğini doğrular.
// Çağıran: menü (To The Summit/Kar/Yükseklik Eşliğini Sına).

using System.Text;
using UnityEditor;
using UnityEngine;

/// İKİ DİLDE YAZILMIŞ TEK FORMÜLÜN SINAMASI.
///
/// `SnowSurfaceHeight` `SnowRelief.hlsl`'in ikizi. İkisi ayrışırsa karakter
/// gördüğü yüzeyin üstünde ya da altında yürümeye başlar ve belirti YAVAŞ
/// büyür — bir sabit değiştiğinde tek tarafta unutulur. Bu test o sapmayı
/// değiştirildiği anda yakalıyor.
public static class SnowHeightParityTest
{
    const int OrnekSayisi = 512;
    const float ToleransMetre = 0.001f;

    [MenuItem("To The Summit/Kar/Yükseklik Eşliğini Sına", false, 61)]
    static void RunMenu() => Debug.Log(Run(out bool ok) + (ok ? "" : "\nEŞLİK BOZUK."));

    public static string Run(out bool ok)
    {
        ok = true;
        var rapor = new StringBuilder();

        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Snow/Shaders/SnowHeightProbe.compute");

        if (compute == null)
        {
            ok = false;
            return "SnowHeightProbe.compute bulunamadı.";
        }

        // TOHUM SABİT: test her koşuda aynı noktaları sınıyor. Rastgele
        // tohumla bir koşuda geçip ötekinde kalan bir sapma teşhis edilemez.
        var rnd = new System.Random(20260827);

        var konum = new Vector2[OrnekSayisi];
        var derinlik = new float[OrnekSayisi];
        var maruziyet = new float[OrnekSayisi];

        for (int i = 0; i < OrnekSayisi; i++)
        {
            konum[i] = new Vector2((float)(rnd.NextDouble() * 2000.0 - 1000.0),
                                   (float)(rnd.NextDouble() * 2000.0 - 1000.0));
            derinlik[i] = (float)(rnd.NextDouble() * 0.8);
            maruziyet[i] = (float)rnd.NextDouble();
        }

        var bufKonum = new ComputeBuffer(OrnekSayisi, sizeof(float) * 2);
        var bufDerinlik = new ComputeBuffer(OrnekSayisi, sizeof(float));
        var bufMaruz = new ComputeBuffer(OrnekSayisi, sizeof(float));
        var bufSonuc = new ComputeBuffer(OrnekSayisi, sizeof(float));

        bufKonum.SetData(konum);
        bufDerinlik.SetData(derinlik);
        bufMaruz.SetData(maruziyet);

        int k = compute.FindKernel("KHeightProbe");
        compute.SetBuffer(k, "_ProbePositions", bufKonum);
        compute.SetBuffer(k, "_ProbeDepths", bufDerinlik);
        compute.SetBuffer(k, "_ProbeExposure", bufMaruz);
        compute.SetBuffer(k, "_ProbeResult", bufSonuc);
        compute.SetInt("_ProbeCount", OrnekSayisi);
        compute.Dispatch(k, (OrnekSayisi + 63) / 64, 1, 1);

        var gpu = new float[OrnekSayisi];
        bufSonuc.GetData(gpu);

        bufKonum.Release();
        bufDerinlik.Release();
        bufMaruz.Release();
        bufSonuc.Release();

        Vector4 wd = Shader.GetGlobalVector("_SastrugiWindDir");
        Vector2 windDir = new(wd.x, wd.y);
        if (windDir.sqrMagnitude < 1e-6f) windDir = Vector2.right;

        float enBuyukSapma = 0f;
        int bozuk = 0;

        for (int i = 0; i < OrnekSayisi; i++)
        {
            float cpu = SnowSurfaceHeight.Rolyef(konum[i], derinlik[i],
                                                 windDir, maruziyet[i]);
            float sapma = Mathf.Abs(cpu - gpu[i]);

            if (sapma > enBuyukSapma) enBuyukSapma = sapma;

            if (sapma > ToleransMetre)
            {
                bozuk++;
                if (bozuk <= 5)
                    rapor.AppendLine($"AYRIK {konum[i]} GPU={gpu[i]:F5} CPU={cpu:F5} " +
                                     $"sapma={sapma * 1000f:F2} mm");
            }
        }

        ok = bozuk == 0;

        rapor.Insert(0, ok
            ? $"Yükseklik eşliği TAMAM — {OrnekSayisi} örnek, en büyük sapma " +
              $"{enBuyukSapma * 1000f:F3} mm.\n"
            : $"Yükseklik eşliği BOZUK — {bozuk}/{OrnekSayisi} örnek toleransı " +
              $"({ToleransMetre * 1000f:F1} mm) aştı, en büyük sapma " +
              $"{enBuyukSapma * 1000f:F2} mm.\n");

        return rapor.ToString();
    }
}
```

- [ ] **Adım 3: Testin KIRMIZI verdiğini doğrula**

Görev 9'daki `return 0f;` gövdeleri hâlâ duruyorsa test kırmızı vermeli. Unity MCP `RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool ok;
        string rapor = SnowHeightParityTest.Run(out ok);
        result.Log("ESLIK OK: " + ok);
        result.Log(rapor);
    }
}
```

Beklenen: `ESLIK OK: False`, AYRIK satırları var.

**Bu adım testin kendisini doğruluyor.** Hep yeşil veren bir test hiçbir şey ölçmüyor demektir; önce kırmızı verdiği görülmeli.

- [ ] **Adım 4: İkizi doldur, testi yeşile çevir**

Görev 9 Adım 3'teki iki `return 0f;` gövdesi HLSL'den çevrilerek doldurulur. Test tekrar çalıştırılır.

Beklenen: `ESLIK OK: True`, en büyük sapma < 1 mm.

**Kırmızıysa** rapordaki AYRIK satırları hangi katmanın saptığını gösterir: sapma tam olarak bir katmanın genliği kadarsa (örneğin 0.30 m → drift, 0.20 m → sastrugi) o katman C#'ta eksik veya yanlış çevrilmiş.

- [ ] **Adım 5: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Snow/Editor/SnowHeightParityTest.cs Assets/Snow/Shaders/SnowHeightProbe.compute Assets/Snow/Runtime/SnowSurfaceHeight.cs && git commit -m "GPU-CPU yukseklik eslik testi"
```

---

## Görev 11: Karakteri yüzeye oturt

**Amaç:** `GroundSnap` kar yüzeyini görsün.

**Dosyalar:**
- Değiştir: `Assets/Scripts/Player/GroundSnap.cs`
- Değiştir: `Assets/Snow/Runtime/SnowManager.cs`
- Değiştir: `Assets/Editor/MountainSceneBootstrap.cs`

- [ ] **Adım 1: Mevcut kodu ve `SnowManager`'ın CPU okuma desenini oku**

```bash
cd /d "D:\ME\game\to the summit" && sed -n '30,70p' "Assets/Scripts/Player/GroundSnap.cs"
```

```bash
cd /d "D:\ME\game\to the summit" && grep -n "ReadPixels\|GetData\|AsyncGPUReadback\|kalicilik\|Persist" Assets/Snow/Runtime/SnowManager.cs | head -10
```

`SnowManager` kalıcılık için kar durumunu CPU'ya okuyorsa aynı desen kullanılır; yoksa Adım 2'deki iki metot o desende yazılır.

- [ ] **Adım 2: `SnowManager`'a iki okuma metodu ekle**

```csharp
    /// Bir noktadaki kar derinliği (m). Fizik tarafı bunu okuyor.
    ///
    /// SINGLETON YOK. Çağıran `[SerializeField]` ile bu bileşeni alıyor
    /// (`CLAUDE.md` — bağımlılık Inspector'dan enjekte edilir).
    public float DepthAt(Vector3 posWS)
    {
        // Kar durumu dokusunun CPU kopyasından okunur; kopya zaten
        // kalıcılık için tutuluyor. Bölge dışında sıfır.
        // Uygulayan ajan Adım 1'de bulduğu deseni izler.
        return 0f;
    }

    /// Bir noktadaki rüzgâr gölgesi (m). `SampleWindShadow`'un CPU ikizi.
    public float WindShadowAt(Vector3 posWS)
    {
        return 0f;
    }
```

**Bu görevi uygulayan ajana:** iki gövde Adım 1'de bulunan CPU okuma desenine göre doldurulur. Bölge dışı sorgusu 0 döner. Doku CPU'ya kopyalanmıyorsa kopya bu görevde eklenir — kar bölgesi 24 m ve çözünürlüğü 1024, yani 4 MB'lık bir `float` dizisi; kare başına değil bölge kaydığında güncellenir.

- [ ] **Adım 2b: `DepthAt`'i shader'a karşı doğrula**

İki gövde de sessizce yanlış dönebilir ve belirti yalnız "karakter biraz
gömülü" olarak görünür — teşhis edilmesi zor. Bir kez ölçülür.

Unity MCP `RunCommand` (Play sırasında, kullanıcı karda dururken):

```csharp
using UnityEngine;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var mgr = Object.FindFirstObjectByType<SnowManager>();
        var oyuncu = Object.FindFirstObjectByType<GroundSnap>();

        if (mgr == null || oyuncu == null) { result.LogError("bilesen yok"); return; }

        Vector3 p = oyuncu.transform.position;

        result.Log("konum " + p.ToString("F2") +
                   " | DepthAt=" + mgr.DepthAt(p).ToString("F4") +
                   " | WindShadowAt=" + mgr.WindShadowAt(p).ToString("F4"));
    }
}
```

Beklenen: `DepthAt` HUD'daki kar derinliğiyle aynı mertebede (aynı sayı
olmayabilir — HUD ortalama gösteriyor olabilir, ama 1 cm karda 0.5 m
dönmemeli). Sıfır dönüyorsa gövde doldurulmamış veya bölge sorgusu yanlış.

- [ ] **Adım 3: `SnowSurfaceHeight`'a dünya sarmalayıcısı ekle**

`SnowSurfaceHeight.cs` içine, `Rolyef`'in altına:

```csharp
    /// Dünya konumundan doğrudan yükseklik.
    ///
    /// Kar derinliği ve rüzgâr gölgesi DIŞARIDAN geliyor: bu sınıf saf kalmak
    /// zorunda (co-op kuralı, `COOP.md`) ve `SnowManager`'a bağımlı olmamalı
    /// (`CLAUDE.md` — sistemler birbirini doğrudan çağırmaz).
    public static float RolyefDunya(Vector3 posWS, float karDerinligi,
                                    float ruzgarGolgesi, Vector2 sastrugiWindDir)
    {
        if (karDerinligi <= 0f) return 0f;

        float maruziyet = 1f - Mathf.Clamp01(ruzgarGolgesi * 1.2f);

        return Rolyef(new Vector2(posWS.x, posWS.z), karDerinligi,
                      sastrugiWindDir, maruziyet);
    }
```

- [ ] **Adım 4: `GroundSnap`'i bağla**

`GroundSnap.cs`'in alan bildirimlerine ekle:

```csharp
    [Tooltip("Kar yöneticisi. Karakterin kar yüzeyine oturması için gerekli; " +
             "boş bırakılırsa karakter kayanın üstünde durur.")]
    [SerializeField] SnowManager snowManager;
```

57 satırı civarındaki satırı değiştir:

```csharp
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + clearance;
```

yerine:

```csharp
            float kayaY = terrain.SampleHeight(position) + terrain.transform.position.y;
            float karY = 0f;

            // KARAKTER ÇİZİLEN YÜZEYDE DURUYOR, KAYANIN ÜSTÜNDE DEĞİL.
            //
            // Kar yüzeyi tessellation ile 15-30 cm yükseliyor. Bu blok
            // olmadan karakter kayanın üstünde kalıyor ve tepeciklerin
            // içinden geçiyor. Aynı hata bir kez yapıldı ve kar yüksekliğinin
            // geometriden tamamen çıkarılmasıyla sonuçlandı
            // (`MountainSurface.shader` yorumu: "karakter yarım metre gömülü
            // başlıyordu").
            //
            // Okunan fonksiyon shader'ın kullandığının ikizi; eşliği
            // `SnowHeightParityTest` ile sınanıyor.
            if (snowManager != null)
            {
                Vector4 wd = Shader.GetGlobalVector("_SastrugiWindDir");
                Vector2 windDir = new(wd.x, wd.y);
                if (windDir.sqrMagnitude < 1e-6f) windDir = Vector2.right;

                karY = SnowSurfaceHeight.RolyefDunya(position,
                                                     snowManager.DepthAt(position),
                                                     snowManager.WindShadowAt(position),
                                                     windDir);
            }

            position.y = kayaY + karY + clearance;
```

- [ ] **Adım 5: Sahnede bağla**

`MountainSceneBootstrap.cs` içinde `GroundSnap` bileşeninin kurulduğu yere `snowManager` ataması eklenir. Elle sahne düzenleme yok (`CLAUDE.md`).

```bash
cd /d "D:\ME\game\to the summit" && grep -n "GroundSnap" Assets/Editor/MountainSceneBootstrap.cs
```

- [ ] **Adım 6: Derlemeyi doğrula**

```bash
cd /d "D:\ME\game\to the summit" && date > Logs/refresh.trigger
```

12 sn bekle, konsolu Unity MCP `Unity_ReadConsole` ile oku. Beklenen: 0 hata.

- [ ] **Adım 7: Ekranda doğrula**

Play. Sorulacaklar:

1. **Karakter tepeciklerin üstünde mi yürüyor?** Ayak hizası kar yüzeyinde olmalı, içinden geçmemeli.
2. **Tepeye çıkarken kamera yükseliyor mu?**
3. **Zıplama/titreme var mı?** Varsa yükseklik okuması kare kare değişiyor — `DepthAt` yerel dokudan okuyorsa doku güncellemesiyle senkron değil demektir.
4. **Spawn noktasında karakter gömülü mü?** Bu, bir önceki denemenin belirtisiydi.

- [ ] **Adım 8: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Scripts/Player/GroundSnap.cs Assets/Snow/Runtime/SnowSurfaceHeight.cs Assets/Snow/Runtime/SnowManager.cs Assets/Editor/MountainSceneBootstrap.cs && git commit -m "Karakter kar yuzeyine oturuyor"
```

---

## Görev 12: Belgeler

**Amaç:** Bağ haritası, gerekçeler, ölçek bağımlılıkları ve co-op kuralı aynı adımda güncellensin.

**Dosyalar:**
- Değiştir: `SYSTEMS.md`, `RATIONALE.md`, `SCALE.md`, `COOP.md`, `DECISIONS.md`, `SYMPTOMS.md`

- [ ] **Adım 1: `SYSTEMS.md` — bağ haritası**

"Kar görünümü (Faz 6)" bölümüne ekle:

```markdown
**Kar yüzeyi GEOMETRİ.** Terrain üçgenleri donanım tessellation'ı ile
kameraya göre bölünüyor (`SnowTessellation.hlsl`), yeni köşeler
`SnowYuzeyRolyef`'in verdiği yükseklik kadar dünya +Y yönünde kayıyor. Dört
geçiş de (ForwardLit, ShadowCaster, DepthOnly, DepthNormals) aynı hull/domain'i
kullanıyor — biri eksik kalsa gölge yüzeyden kayardı.

Kenar bölme faktörü **yalnız kenarın iki ucundan** hesaplanıyor; komşu patch
aynı iki köşeyi gördüğü için aynı faktörü üretiyor ve çatlak matematiksel
olarak imkânsız oluyor.

Bölme faktörü **ana kameranın** konumundan (`_SnowTessCameraPos`) geliyor,
`_WorldSpaceCameraPos`'tan değil: gölge geçişinde o değişken ışığın konumunu
tutuyor.

**50 cm eşiği.** Geometriye yalnız dalga boyu 50 cm'den uzun katmanlar giriyor
(fBm, drift, sastrugi). Ripple (17 cm) ve mikro (8.3 cm) normal haritasında
kalıyor — en ince geometri 11.4 cm ve altındaki dalga taşınamıyor.

**Fizik aynı fonksiyonu okuyor.** `GroundSnap` karakteri `SnowSurfaceHeight`'a
göre oturtuyor; o sınıf `SnowYuzeyRolyef`'in C# ikizi ve eşliği
`SnowHeightParityTest` ile sınanıyor.

**Drift ↔ sastrugi rüzgâr maruziyetiyle ayrılıyor.** Siperde birikme
tepecikleri, açıkta erozyon sırtları. Aynı noktada ikisi birden olmuyor.
```

- [ ] **Adım 2: `RATIONALE.md` — gerekçeler**

```markdown
## Kar yüzeyi neden geometri oldu

**Belirti.** "Hafif uzak zemin detaysız gözüküyor." Normal haritası silüete ve
örtüşmeye katkı vermiyor; sıyırtma açıda bir yüzeyin görünümünü tamamen silüet
ve kendi gölgesi belirliyor.

**Bir kez denendi ve fizik yüzünden geri alındı.** `MountainSurface.shader`
yorumu: "ayak 205.539, kaya 205.489, çizilen yüzey 205.98 — karakter yarım metre
gömülü başlıyordu". O tur kar yüksekliğinin geometriden tamamen çıkarılmasıyla
bitti.

Bu turda fizik uyumu işin **parçası**: `SnowSurfaceHeight` C# ikizi +
`SnowHeightParityTest` + `GroundSnap` bağlantısı. Üçü olmadan aynı yere
çıkılırdı.

**Neden ayrı kar mesh'i değil.** Kullanıcı kararı: mesh bu projede iki kez sorun
çıkardı. Tessellation ayrı bir nesne üretmiyor, Terrain'in kendi üçgenlerini
bölüyor.

**Ölçek tavanı.** Terrain köşe aralığı 7.32 m (30 km / 4096), donanım bölme
tavanı 64 → en ince geometri 11.4 cm. Bu aşılamaz; alt-11-cm her şey normal
haritasında kalıyor ve orada kalması doğru.

## Drift ve sastrugi neden ayrıldı

Sastrugi **erozyon** şekli — rüzgâr karı oyuyor, keskin sırt ve dik yüz
bırakıyor, oluşumu 20 m/s üstü rüzgâr istiyor. Drift **birikme** şekli —
rüzgârın yavaşladığı siperde çöküyor, yuvarlak ve yumuşak. Spec §18.0 zaten
rüzgâr gölgesinde aşınmayı tamamen kapatıyor.

**Yan kazanç: RMS eğim bütçesi çözüldü.** İkisi aynı noktada toplansaydı yüzeyin
toplam eğimi ölçülen 5-15° bandını iki kat aşardı. Ayrıldıkları için ortalama
bantta kalıyor, yerel olarak 40-50°'ye çıkıyor.
```

- [ ] **Adım 3: `SCALE.md` — ölçek bağımlılıkları**

```markdown
## Tessellation — elle bakılacak

`SNOW_TERRAIN_VERTEX_SPACING` (7.32 m) dağın boyundan ve heightmap
çözünürlüğünden türüyor: `terrainSize / (heightmapResolution − 1)`.

Dağ büyütülür veya küçültülürse **kendiliğinden kaymaz**, sabit yanlış kalır.
Sonucu `SNOW_TESS_MIN_DALGA`'yı da bozar: eşik o aralıktan hesaplanmış.

Dağ boyu değiştiğinde ikisi birden elden geçirilir:

    en ince geometri     = SNOW_TERRAIN_VERTEX_SPACING / 64
    SNOW_TESS_MIN_DALGA ≈ en ince geometri × 4     (Nyquist × 2 güvenlik payı)

`tessNear` / `tessFar` (15 / 60 m) **bilerek mutlaktır**: oyuncunun gözünden
mesafe, dağın boyuyla ilgisi yok.
```

- [ ] **Adım 4: `COOP.md` — kural**

"Borç doğurmayanlar" bölümüne ekle:

```markdown
- **Kar yüzeyi tessellation'ı ve yer değiştirmesi** — tamamen yerel görüntü;
  her istemci kendi kamerasına göre bölüyor, dünya aynı kalıyor.

  **AMA BİR KURAL DOĞURDU.** Kar yüzeyi yükseklik fonksiyonu
  (`SnowYuzeyRolyef` ve C# ikizi `SnowSurfaceHeight`) **saf kalmak zorunda**:
  girdisi yalnız dünya konumu, kar durumu ve rüzgâr maruziyeti. Kare sayacı,
  `Time` veya yerel rastgelelik girerse iki oyuncu farklı zeminde yürür ve
  karakter konumları ağ üzerinde uyuşmaz.

  Şu an temiz. "Rüzgârla dalgalanan yüzey" gibi bir özellik eklenirse borç
  anında doğar — o zaman bu satır borç listesine taşınır.
```

Ayrıca "Henüz yazılmadı" tablosundaki **"Kar üzerinde ayak izi"** satırı güncel
değil: yazıldı. O satır borç listesine taşınır.

- [ ] **Adım 5: `DECISIONS.md`**

```markdown
## Kar yüzeyi geometrisi: tessellation, ayrı mesh değil

**Karar (2026-08-27).** Kar yüzeyinin alt-metre tepecikleri donanım
tessellation'ı ile Terrain üçgenleri bölünerek üretiliyor. Ayrı bir kar mesh'i
kurulmuyor.

**Gerekçe.** Kullanıcı kararı: mesh bu projede iki kez sorun çıkardı.
Tessellation ayrı nesne üretmiyor, mevcut geometriyi bölüyor.

**Maliyet.** En ince geometri 11.4 cm ile sınırlı (Terrain köşe aralığı 7.32 m
÷ donanım tavanı 64). Ripple ve mikro oktavları geometriye giremiyor.

**TETİKLEYİCİ — hangi belirtide geri dönülür.** Tepecikler 11.4 cm tavanı
yüzünden geometrik olarak fazla yumuşak kalırsa. O durumda tek çıkar yol Terrain
heightmap çözünürlüğünü artırmak (4097 → 8193, köşe aralığı 3.66 m, en ince
geometri 5.7 cm); bellek dört katına çıkar.
```

- [ ] **Adım 6: `SYMPTOMS.md` düzeltmesi**

Bir önceki turda yazılan "Hafif uzak zemin detaysız gözüküyor" kaydı `sqrt(fx·fy)`
düzeltmesini "gerçek sebep" diye gösteriyor ama **belirti düzelmedi**. Kayıt
düzeltilir: o düzeltme gerçek bir kusurdu ama belirtinin sebebi değildi; gerçek
sebep normal haritasının silüete ve örtüşmeye katkı verememesi.

- [ ] **Adım 7: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add SYSTEMS.md RATIONALE.md SCALE.md COOP.md DECISIONS.md SYMPTOMS.md && git commit -m "Belgeler: kar yuzeyi geometrisi"
```

---

## Bitiş doğrulaması

Bütün görevler bittikten sonra tek turda:

- [ ] `SnowConstantsTest` yeşil
- [ ] `SnowHeightParityTest` yeşil, en büyük sapma < 1 mm
- [ ] Dört geçiş de derleniyor, hata yok
- [ ] Ufuk çizgisi tırtıklı — silüet kırılıyor
- [ ] Çatlak yok
- [ ] Gölge tepeciklerin dibinden başlıyor
- [ ] Zeminde kafes deseni yok (DepthNormals doğru)
- [ ] Karakter yüzeyin üstünde yürüyor, içinden geçmiyor
- [ ] Spawn noktasında gömülü değil
- [ ] Zemin titremiyor
- [ ] FPS ve `Tri` sayacı kabul edilebilir (öncesi: 33K üçgen / 169 FPS)
- [ ] F1 → "tessellation" anahtarı zemini düzleştiriyor
- [ ] F1 → "drift" anahtarı tepecikleri kaldırıyor
- [ ] Ayak izi görünüyor ve iki kat derin değil
