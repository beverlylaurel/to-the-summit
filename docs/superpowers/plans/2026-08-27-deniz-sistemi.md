# Deniz Sistemi — Uygulama Planı

> **Ajan işçiler için:** GEREKLİ ALT BECERİ: `superpowers:subagent-driven-development` veya `superpowers:executing-plans` ile bu planı görev görev uygula. Adımlar takip için onay kutusu (`- [ ]`) kullanıyor.

**Kaynak:** `unity-deniz-sistemi-spec.md`. Spec'in **tamamı** uygulanıyor. Hiçbir bölüm çıkarılmadı.

**Hedef:** Kıyıdan görülen deniz. Oyuncu suya girmiyor ama kıyıda durabiliyor ve denize çok yakından bakabiliyor — dalga kırılması, kıyı köpüğü, ıslak kum ve refraksiyon **görünür mesafede**.

**Mimari:** Üç kademeli FFT dalga sentezi (Tessendorf/Horvath TMA), sığ su dönüşümü ve kırılma, kameraya snap'lenen tek ızgara mesh, tam Fresnel + refraksiyon + hacim soğurma. Rüzgâr, güneş, gökyüzü, sis ve yağış mevcut sistemlerden **okunuyor**, hiçbiri yazılmıyor.

**Teknoloji:** Unity 6000.5.6f1, URP, Compute Shader, HLSL. Shader Graph yok.

---

## Spec'in protokolü bu planda da geçerli

`unity-deniz-sistemi-spec.md` §0:

1. Fazları **sırayla** uygula. Faz N'in kabul kriterleri geçmeden Faz N+1'e geçme.
2. **Soru sorma.** Belirsizlik için spec §17 karar tablosuna bak.
3. Dosya, sınıf, kernel ve sabit adlarını ve sayısal değerleri **aynen** kullan.
4. `[KAYNAK: ...]` etiketli değeri **değiştirme, iyileştirme, basitleştirme.** `[KALİBRASYON]` etiketli değeri de verilen değerle başlat.
5. Spec kendi içinde çelişirse: kod yazmayı bırak, çelişkiyi **sayısal olarak** göster, karar bekle.
6. Mevcut proje dosyalarını değiştirme (spec §1). İki istisna var, ikisi de **ayrı onay** istiyor: kıyı arazisinin ıslaklık maskesi (Faz 9), deniz seviyesi altındaki arazinin görünürlüğü (Faz 5).
7. Shader Graph kullanma. Elle HLSL yaz.
8. `Update()` / `LateUpdate()` içinde allocation yasak. Property ID'leri `SeaShaderIDs.cs` içinde `static readonly int`.
9. Birimler: metre, saniye, Celsius. Açılar radyan.
10. Her dosyanın başında `// ROL:` bloğu.

## Bu projenin kuralları da geçerli

`CLAUDE.md` spec ile çelişmiyor ama dört yerde daha katı:

- **Bağımlılık Inspector'dan.** `FindObjectOfType`, singleton, `GameObject.Find` yok. `SeaEnvironmentBridge` bu projede `[SerializeField]` ile bağlanır.
- **Ayarlar ScriptableObject'e.** `SeaSettings` bir asset olacak (`SnowSettings` deseni).
- **Sahne kurulumu koddan.** `MountainSceneBootstrap` deniz nesnesini kurar; elle sahne düzenleme yok.
- **Belgeler aynı adımda.** Yeni bağ → `SYSTEMS.md`, gerekçe → `RATIONALE.md`, ertelenen karar → `DECISIONS.md`, ölçek bağı → `SCALE.md`, co-op borcu → `COOP.md`.

---

## Bu araziye özel ölçümler

Deniz seviyesi kararı için arazi ölçüldü (30 km × 30 km, taban y = 0, heightmap 4097):

| Deniz seviyesi | Kaplanan alan | Mevcut başlangıçtan en yakın kıyı |
|---|---|---|
| 5 m | %10.7 | 5.33 km |
| 10 m | %17.3 | 4.89 km |
| 20 m | %24.3 | 4.30 km |
| 40 m | %31.2 | 3.59 km |

Arazinin **%40'ı 0–100 m bandında** — dağın eteği geniş ve düz, deniz için hazır zemin. En alçak nokta 0 m, en yüksek 6021 m.

Mevcut oyuncu başlangıcı 205.5 m'de. **Oyun değişecek ve oyuncu kıyıya gidebilecek**; bu plan kıyı deneyimini tam kapsamıyla kuruyor. Deniz seviyesi `SeaSettings`'te ayarlanabilir; başlangıç değeri Faz 4'te seçilir ve gerekçesi `DECISIONS.md`'ye yazılır.

---

## Riskler ve nasıl sıfırlandıkları

| Risk | Nasıl sıfırlanıyor | Faz |
|---|---|---|
| FFT sessizce yanlış (RNG bozuk) | `mean(h)` ölçümü + `RT_Derivatives.x` görsel denetimi | 2 |
| Spektrum her frame hesaplanıyor | Rüzgâr eşiği; Profiler'da doğrulanıyor | 2 |
| Uzun oturumda dalga bozuluyor | Döngü kuantizasyonu | 2 |
| Mesh'te delik/yırtık | Halkalar arası vertex paylaşımı; spec §10.6'nın altı testi | 5 |
| Mesh araziyle kesişiyor | `shoreFade` | 6 |
| Kıyıda dalga sonsuz büyüyor | `_MaxShoalingGain` | 6 |
| Su tamamen opak/şeffaf | Tam Fresnel (Schlick değil) | 7 |
| Kayalar suyun içine sızıyor | Refraksiyon derinlik kontrolü | 7 |
| Mevcut sis/ışık/bulut bozuluyor | Deniz hiçbirine yazmıyor; kod aramasıyla doğrulanıyor | 1 |
| Compute kernel sessizce derlenmiyor | `HasKernel` kontrolü | 2 |
| Sabitler C#/HLSL ayrışıyor | `SeaConstantsTest` | 0 |

**Compute shader tuzakları** — kar sisteminde ölçüldü (`RATIONALE.md`). İkisi de `GetComputeShaderMessages` **boş dönerken** kernel'i geçersiz kılıyor:

1. `fwidth` compute aşamasında **tanımsız**.
2. `SAMPLER` makrosu URP core `Common.hlsl`'den geliyor; `GlobalSamplers.hlsl` ondan önce include edilirse aynı sessiz hata.

Her compute derlemesinden sonra `HasKernel` ile doğrula, mesaj listesine güvenme.

**Materyal tuzağı:** `AssetDatabase.ImportAsset` runtime'da kurulan materyalleri düşürüyor (`TerrainSurface` bunu yaşadı). Deniz materyali `Update` içinde `EnsureMaterial` deseniyle kurulacak.

## Doğrulama bu projede ne demek

Test paketi yok (`CLAUDE.md`). Spec'in "kullanıcı test etsin" adımları üç biçime düşüyor:

1. **Derleme kontrolü** — `ShaderUtil.ShaderHasError`, `ComputeShader.HasKernel`
2. **Sayısal test** — editör testi (`SnowConstantsTest` / `SnowHeightParityTest` deseni)
3. **Ekran doğrulaması** — kullanıcı Play'e basar, ne göreceği yazılı

Shader düzenlemesinden sonra **her zaman** `date > Logs/refresh.trigger`, 14 sn bekle, sonra derleme kontrolü.

**Her fazın kabul kriterlerine şu regresyon dahil** (spec §16): mevcut sis, yağmur, kar, gece/gündüz, ışıklandırma ve bulut sistemleri bozulmadan çalışıyor.

---

## Dosya yapısı

Spec §1.4: `Assets/Sea/{Runtime,Shaders,Editor,VFX,Textures,Settings}`

| Dosya | Sorumluluk | Faz |
|---|---|---|
| `Editor/SeaProjectCheck.cs` | Proje ayarlarını raporlar, hiçbirini değiştirmez | 0 |
| `Runtime/SeaConstants.cs` | Sabitler, C# tarafı | 0 |
| `Shaders/SeaConstants.hlsl` | Aynı sabitler, HLSL | 0 |
| `Editor/SeaConstantsTest.cs` | İkisinin eşliğini sınar | 0 |
| `Runtime/SeaShaderIDs.cs` | `static readonly int` property ID'leri | 0 |
| `Runtime/SeaSettings.cs` | Ayar asset'i (ScriptableObject) | 0 |
| `Runtime/SeaQualityPreset.cs` | Low/Medium/High kademe verisi | 0 |
| `Shaders/SeaCommon.hlsl` | Ortak fonksiyonlar, globaller | 0 |
| `Runtime/ISeaEnvironmentSource.cs` | Dış dünyadan okunan her şey | 1 |
| `Runtime/SeaEnvironmentBridge.cs` | Bu oyunun sistemlerine köprü | 1 |
| `Runtime/SeaRuntimeState.cs` | Denizin yayınladığı durum | 1 |
| `Runtime/SeaManager.cs` | Yaşam döngüsü, global yayını | 1 |
| `Runtime/SeaRenderPass.cs` | Tek CommandBuffer, tüm dispatch'ler | 1 |
| `Runtime/SeaRendererFeature.cs` | URP renderer'a bağlanma | 1 |
| `Editor/SeaDebugWindow.cs` | RT görselleştirme | 1 |
| `Shaders/SeaSpectrum.compute` | KInitialSpectrum, KTimeSpectrum | 2 |
| `Shaders/SeaFFT.compute` | KIFFTHorizontal, KIFFTVertical | 2 |
| `Runtime/SeaSimulation.cs` | RT yaşam döngüsü, dispatch sırası | 2 |
| `Shaders/SeaFoam.compute` | KFoam | 3 |
| `Runtime/SeaBathymetry.cs` | Su derinliği bake | 4 |
| `Runtime/SeaMeshBuilder.cs` | Tek ızgara mesh üretimi | 5 |
| `Runtime/SeaSurface.cs` | Mesh yerleştirme, snap, materyal | 5 |
| `Shaders/SeaLit.shader` | Yüzey shader'ı | 5, 7 |
| `Shaders/SeaLitInput.hlsl` | Materyal property'leri, CBUFFER | 5 |
| `Shaders/SeaLitForwardPass.hlsl` | Vertex + fragment | 5 |
| `Shaders/SeaShallow.hlsl` | Sığlaşma, kırılma, kıyı sönümü | 6 |
| `Shaders/SeaOptics.hlsl` | Fresnel, refraksiyon, hacim, parıltı | 7 |
| `Shaders/SeaFoamShading.hlsl` | Köpük render | 8 |
| `Textures/T_Foam.png`, `T_FoamBreakup.png` | Köpük dokuları | 8 |
| `Runtime/SeaWetnessDriver.cs` | Islak kum uniform'ları | 9 |
| `Runtime/SeaProfiler.cs` | Pass süreleri | 10 |
| `Assets/Editor/MountainSceneBootstrap.cs` | Deniz nesnesini kurar ve bağlar | 1 |
| `Assets/Scripts/Debug/DebugMenu.cs` | F1 deniz anahtarları | 2+ |

---

## Faz 0 — Proje kontrolü ve sabitler

**Amaç:** Zeminin uygun olduğunu doğrulamak ve sayıların tek kaynağını kurmak. **Hiçbir proje ayarı değiştirilmiyor.**

- [ ] **Adım 1: Klasörleri aç**

```bash
cd /d "D:\ME\game\to the summit" && mkdir -p Assets/Sea/Runtime Assets/Sea/Shaders Assets/Sea/Editor Assets/Sea/VFX Assets/Sea/Textures Assets/Sea/Settings
```

- [ ] **Adım 2: `Editor/SeaProjectCheck.cs`**

Menü: `Tools/Sea/Project Check`. Spec §1.2 tablosundaki dokuz kontrolü yapar, rapor yazar, **hiçbirini otomatik düzeltmez.**

| Kontrol | Beklenen | Uymuyorsa |
|---|---|---|
| Color Space | Linear | Rapor: su optiği Gamma'da doğru çalışmaz, bu bir proje kararıdır |
| URP aktif | evet | Dur, bildir |
| **Opaque Texture** | **açık** | Dur, bildir — refraksiyon için zorunlu |
| **Depth Texture** | **açık** | Dur, bildir — derinlik rengi ve köpük için zorunlu |
| Compute desteği | `SystemInfo.supportsComputeShaders` | Dur, bildir |
| VFX Graph paketi | kurulu | Faz 8'e kadar sadece uyar |
| Boş layer slotu | >= 1 | Dur, bildir |
| Terrain sayısı | 1 | >1 ise dur, bildir |
| URP yerleşik su sistemi | — | Bulunursa **kullanma**, raporla, bekle |

- [ ] **Adım 3: Kontrolü çalıştır**

Unity MCP `RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("Color Space = " + PlayerSettings.colorSpace);
        result.Log("compute destegi = " + SystemInfo.supportsComputeShaders);

        var rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        result.Log("URP asset = " + (rp != null ? rp.name : "YOK"));

        if (rp != null)
        {
            result.Log("  supportsCameraOpaqueTexture = " + rp.supportsCameraOpaqueTexture);
            result.Log("  supportsCameraDepthTexture  = " + rp.supportsCameraDepthTexture);
        }

        result.Log("Terrain sayisi = " +
            Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None).Length);

        int bosLayer = 0;
        for (int i = 8; i < 32; i++)
            if (LayerMask.LayerToName(i).Length == 0) bosLayer++;
        result.Log("bos layer = " + bosLayer);
    }
}
```

**Opaque veya Depth Texture kapalıysa DUR.** Bunlar URP Asset ayarı ve spec §1.1 "URP Asset ayarlarını asla değiştirme" diyor. Kullanıcıya bildir, kararı o versin.

- [ ] **Adım 4: `Shaders/SeaConstants.hlsl`**

Spec §11.2'deki on dört sabit **aynen**, artı köpük sabitleri (§13.2) ve FFT boyutları:

```hlsl
// ROL: deniz sisteminin butun sabitleri. `SeaConstants.cs` ile BIREBIR ayni
// degerleri tasiyor; esligi `SeaConstantsTest` siniyor.

#ifndef SEA_CONSTANTS_INCLUDED
#define SEA_CONSTANTS_INCLUDED

#define SEA_G                    9.81      // [KAYNAK: Tessendorf 2004 4.2]
#define SEA_TWO_PI               6.28318530718
#define SEA_WATER_IOR            1.34      // [KAYNAK: Tessendorf 2004 6.1.2]
#define SEA_MIN_DEPTH            0.05      // [KALIBRASYON]
#define SEA_SHORE_FADE_DEPTH     0.60      // [KALIBRASYON]
#define SEA_CHOP_FADE_DEPTH      8.00      // [KALIBRASYON]
#define SEA_GAMMA_MILD           0.55      // [KAYNAK: DNV 2017]
#define SEA_GAMMA_STEEP          1.10      // [KAYNAK: Galvin 1969 / Weggel 1972]
#define SEA_BREAK_FOAM_GAIN      1.60      // [KALIBRASYON]
#define SEA_JONSWAP_GAMMA        3.30      // [KAYNAK: Horvath 2015 / JONSWAP]
#define SEA_JONSWAP_SIGMA_LO     0.07      // [KAYNAK: JONSWAP]
#define SEA_JONSWAP_SIGMA_HI     0.09      // [KAYNAK: JONSWAP]
#define SEA_MICHELL_STEEPNESS    0.142     // [KAYNAK: Michell 1893]
#define SEA_BULK_REFLECTIVITY    0.04      // [KAYNAK: Tessendorf 2004 7.1]

// --- Kopuk (spec 13.2) ---
#define SEA_FOAM_J_THRESHOLD     0.55      // [KALIBRASYON]
#define SEA_FOAM_J_RANGE         0.55      // [KALIBRASYON]
#define SEA_FOAM_DECAY           0.28      // 1/s [KALIBRASYON]

// --- FFT (spec 6.6, 6.8) ---
#define SEA_FFT_SIZE             256       // [KAYNAK: Tessendorf 2004 4.4]
#define SEA_FFT_LOG2             8
#define SEA_TIER_COUNT           3

#endif
```

Kademe boyutları (`PatchSize` 512/128/24, `TierWeight`, choppiness çarpanları) **sabit değil ayar** — `SeaSettings`'e gidiyor, çünkü spec onları `[KALİBRASYON]` işaretlemiş ve kalite presetine göre değişiyorlar.

- [ ] **Adım 5: `Runtime/SeaConstants.cs`**

Aynı on yedi değer, C# tarafı. `SnowConstants.cs` deseninde: her sabitin üstünde `///` yorumu ve `[KAYNAK]` / `[KALİBRASYON]` etiketi.

- [ ] **Adım 6: `Editor/SeaConstantsTest.cs`**

`SnowConstantsTest.cs`'in birebir uyarlaması. Önce onu oku:

```bash
cd /d "D:\ME\game\to the summit" && sed -n '1,80p' Assets/Snow/Editor/SnowConstantsTest.cs
```

**Kar testinden taşınan ders:** yalnız HLSL'de olan bir sabit **hata değil**, bilgi. Öyle sayılırsa test kalıcı kırık kalır — `SnowConstantsTest` bu yüzden düzeltilmişti.

Menü: `To The Summit/Deniz/Sabit Eşliğini Sına`.

- [ ] **Adım 7: Kalan Faz 0 dosyaları**

`SeaShaderIDs.cs`, `SeaSettings.cs`, `SeaQualityPreset.cs`, `SeaCommon.hlsl`.

`SeaCommon.hlsl` include sırası **kritik**:

```hlsl
// URP CORE ONCE. SAMPLER makrosu buradan geliyor; eksikse compute kernel
// SESSIZCE derlenmiyor - GetComputeShaderMessages bos doner ama FindKernel
// "kernel at index 0 is invalid" verir. Kar sisteminde bir tur bu yuzden
// yandi (RATIONALE.md).
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "SeaConstants.hlsl"
```

`SeaQualityPreset` spec §15.3 tablosunu taşır: FFT çözünürlüğü, kademe sayısı, mesh halka sayısı, halka 0 quad boyu, refraksiyon açık/kapalı, köpük yön uzatma, shader keyword.

- [ ] **Adım 8: Derleme ve test**

```bash
cd /d "D:\ME\game\to the summit" && date > Logs/refresh.trigger
```

14 sn bekle, sonra Unity MCP `RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool ok;
        string rapor = SeaConstantsTest.Run(out ok);
        result.Log("SEA SABIT ESLIGI = " + ok);
        result.Log(rapor);
    }
}
```

**Kabul (spec Faz 0):** Project Check rapor üretiyor; hiçbir proje ayarı değişmemiş; Opaque ve Depth Texture durumu raporlanmış; C# ve HLSL sabitleri birebir aynı.

- [ ] **Adım 9: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea && git commit -m "Deniz Faz 0: proje kontrolu ve sabitler"
```

---

## Faz 1 — Entegrasyon katmanı

**Amaç:** Denizin mevcut sistemlerden **yalnız okumasını** kurmak. Spec'in en katı kuralı: `RenderSettings` / `VolumeProfile` / `Light.intensity` yazan tek satır olmayacak.

- [ ] **Adım 1: Bu oyunun kaynaklarını ölç**

Köprü tahminle yazılmaz (spec §3.2: "Sen bunu tahmin etmeye çalışma"). Gerçek bileşen ve property adları:

```bash
cd /d "D:\ME\game\to the summit" && grep -n "public.*WindSpeed\|public.*PrevailingDirection\|public.*Strength\|public.*WindDirection" Assets/Scripts/Weather/WindField.cs
```

```bash
cd /d "D:\ME\game\to the summit" && grep -n "public.*Precipitation\|public.*Snowiness\|public.*Rain" Assets/Scripts/Weather/WeatherState.cs
```

```bash
cd /d "D:\ME\game\to the summit" && grep -n "public.*Normalized\|public.*SunElevation\|public.*Sun" Assets/Scripts/Environment/TimeOfDay.cs
```

```bash
cd /d "D:\ME\game\to the summit" && grep -n "public.*Coverage\|public.*SkyColor\|public.*Horizon\|public.*Fog" Assets/Scripts/Environment/AtmosphereController.cs
```

Çıkan adlar Adım 3'te **aynen** kullanılır. Bulunamayan kaynak için köprü manuel değeri döndürür ve `TODO(kullanici)` yorumu bırakır — spec §3.2 bunu açıkça izin veriyor.

- [ ] **Adım 2: `ISeaEnvironmentSource.cs` ve `SeaRuntimeState.cs`**

Spec §3.1 ve §3.3'teki tanımlar **birebir**. Tek uyarlama: `PrecipitationKind` bu projede `WeatherState` üzerinden geliyorsa yeni enum kurulmaz, mevcut olan eşlenir.

- [ ] **Adım 3: `SeaEnvironmentBridge.cs`**

`CLAUDE.md`: bağımlılık Inspector'dan. Singleton ve `FindObjectOfType` yok.

```csharp
    [Header("Oyunun mevcut sistemleri — Inspector'dan bağlanır")]
    [SerializeField] WindField wind;
    [SerializeField] WeatherState weather;
    [SerializeField] TimeOfDay timeOfDay;
    [SerializeField] AtmosphereController atmosphere;
    [SerializeField] Light sunLight;

    [Header("Köprü kurulana kadar manuel değerler (spec §3.2)")]
    [SerializeField] Vector3 manualWindDirection = new Vector3(1, 0, 0);
    [SerializeField] float   manualWindSpeed     = 8f;
    // `manualSkyColor` varsayilani [KAYNAK: Tessendorf 2004 6.3 ornek shader]
    [SerializeField] Color   manualSkyColor      = new Color(0.69f, 0.84f, 1.00f);
    [SerializeField] Color   manualHorizonColor  = new Color(0.80f, 0.86f, 0.92f);
```

Her property önce bağlı bileşeni dener, yoksa manuel değeri döndürür.

- [ ] **Adım 4: `SeaManager.cs`, `SeaRenderPass.cs`, `SeaRendererFeature.cs`**

`SeaManager` `ISeaEnvironmentSource` bulamazsa **hata basıp devre dışı kalır** (spec §3.2). Kendi varsayılanını uydurmaz.

Rüzgâr yayını (spec §3.4):

```csharp
Shader.SetGlobalVector(SeaShaderIDs.SeaWindWS, env.WindDirection * env.WindSpeed);
```

Kendi rüzgâr noise'u veya gust simülasyonu **kurulmaz**.

`SeaRenderPass` tek `CommandBuffer`, `RenderPassEvent.BeforeRenderingOpaques` (spec §15.2).

- [ ] **Adım 5: Bootstrap'e bağla**

`MountainSceneBootstrap` deniz nesnesini kurar ve `SeaEnvironmentBridge`'in alanlarını sahnedeki bileşenlere bağlar. `SnowGroundOffset` bağlamasındaki desen izlenir (`SerializedObject` + `ApplyModifiedPropertiesWithoutUndo`).

- [ ] **Adım 6: `SeaDebugWindow.cs`**

RT görselleştirme penceresi. Faz 2'nin kabul kriteri buna bağlı: `RT_H0` ve `RT_Derivatives.x` gözle denetlenecek.

- [ ] **Adım 7: YAZMA YASAĞINI DOĞRULA**

Spec Faz 1 kabul kriteri, **atlanamaz**:

```bash
cd /d "D:\ME\game\to the summit" && grep -rn "RenderSettings\.\|VolumeProfile\|\.intensity *=" Assets/Sea/ || echo "TEMIZ - deniz hicbir global duruma yazmiyor"
```

Beklenen: `TEMIZ`.

- [ ] **Adım 8: GC allocation kontrolü**

```bash
cd /d "D:\ME\game\to the summit" && grep -rn "new \|GetTemporary\|ToString()" Assets/Sea/Runtime/SeaManager.cs Assets/Sea/Runtime/SeaRenderPass.cs
```

`Update`/`Execute` içindeki her `new` incelenir. Spec §0.8: allocation yasak.

- [ ] **Adım 9: Derleme kontrolü ve commit**

```bash
cd /d "D:\ME\game\to the summit" && date > Logs/refresh.trigger
```

14 sn bekle, `Unity_ReadConsole` ile 0 hata doğrula.

**Kabul (spec Faz 1):** köprü manuel değerlerle çalışıyor; global uniform'lar doğru; yazma yasağı kod aramasıyla doğrulanmış; frame başına 0 B GC; mevcut sistemler aynen çalışıyor.

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea Assets/Editor && git commit -m "Deniz Faz 1: entegrasyon katmani, yalniz okuma"
```

---

## Faz 2 — Spektrum ve IFFT

**Amaç:** Dalga alanını üretmek. **Bu fazın kabul kriterleri sayısal ve görsel; ikisi de geçmeden Faz 3'e geçilmiyor.**

**Neden bu kadar katı:** FFT sessizce yanlış çalışır. RNG Gauss değilse yüzey düzenli desen verir; eşlenik simetri bozuksa yüzey düz kalır. İkisi de ekranda "biraz tuhaf" görünür ve haftalarca yanlış yerde aranır.

- [ ] **Adım 1: `SeaSpectrum.compute` — `KInitialSpectrum`**

TMA spektrumu (spec §6.2), yönsel yayılma (§6.3), Gauss RNG (§6.1) → `RT_H0`.

Kitaigorodskii derinlik sönümü **dallanmasız** yazılır (spec §6.2 tek satırlık form):

```hlsl
float omegaH = omega * sqrt(h / SEA_G);
float phi = 0.5 * omegaH * omegaH
          + (-omegaH * omegaH + 2.0 * omegaH - 1.0) * step(1.0, omegaH);
phi = saturate(phi);
```

Başlangıç genlikleri (spec §6.1):

```hlsl
// ksi_r, ksi_i BAGIMSIZ Gauss (ortalama 0, std 1). Box-Muller ile uniform
// hash'ten uretilir; DUZ UNIFORM KULLANILMAZ - spektrum yanlis olur ve
// yuzey duzenli desen verir (spec 18 tuzak tablosu).
float2 h0 = (1.0 / sqrt(2.0)) * float2(ksiR, ksiI) * sqrt(Ph);
```

Küçük dalga bastırma `exp(-k*k*l*l)` (spec §6.3, `[KAYNAK: Tessendorf 2004 denklem 41]`).

- [ ] **Adım 2: `SeaSpectrum.compute` — `KTimeSpectrum`**

Zaman genliği (spec §6.1), eğim spektrumu ve displacement spektrumu (spec §6.7):

```hlsl
// h(k,t) = h0(k) e^{i w t} + h0*(-k) e^{-i w t}
// Bu bicim eslenik ozelligini korur ve herhangi bir t anindaki alani
// baska hicbir ani hesaplamadan uretir.

// EGIM SONLU FARKLA HESAPLANMIYOR (spec 6.7). eps(k) = i*k*h(k)
float2 slopeX = SeaCMul(float2(0, k.x), ht);
float2 slopeZ = SeaCMul(float2(0, k.y), ht);

// Displacement: D(k) = -i * (k/|k|) * h(k)
float2 dispX = SeaCMul(float2(0, -k.x / kLen), ht);
float2 dispZ = SeaCMul(float2(0, -k.y / kLen), ht);
```

Döngü kuantizasyonu (spec §6.5) **zorunlu**:

```hlsl
float omega0 = SEA_TWO_PI / _LoopPeriod;
float omega  = floor(SeaOmega(k, _SpectrumDepth) / omega0) * omega0;
```

Dispersiyon **yalnız sığ su formülü** (spec §6.4, §17): `omega^2 = g*k*tanh(k*D)`. Derin su formülü ayrıca yazılmaz — `tanh` derinde 1'e gidiyor.

- [ ] **Adım 3: `SeaFFT.compute` — Stockham, iki geçiş**

`[numthreads(SEA_FFT_SIZE, 1, 1)]`, `log2(256) = 8` adım, ping-pong, yatay sonra dikey. Üç kademe texture array slice'ı olarak.

Çıktı: `RT_Displacement` (xyz = Dx, h, Dz), `RT_Derivatives` (xy = eğim, zw = ∂D/∂x, ∂D/∂y).

- [ ] **Adım 4: `SeaSimulation.cs`**

RT'ler `Awake`'te bir kez (spec §11.1): `enableRandomWrite = true`, `filterMode = Bilinear`, **`wrapMode = Repeat`**, `useMipMap = false`, `dimension = Tex2DArray`, `volumeDepth = 3`, `Create()`.

`GetTemporary` **kullanılmaz** (spec §15.2). `OnDestroy`'da `Release()`.

Spektrum yeniden hesabı (spec §15.2):

```csharp
bool dirty = Mathf.Abs(env.WindSpeed - _lastWindSpeed) > 0.25f
          || Vector3.Angle(env.WindDirection, _lastWindDir) > 3f;
```

- [ ] **Adım 5: Compute derleme kontrolü**

`GetComputeShaderMessages` **boş dönebilir** ve kernel yine de geçersiz olabilir:

```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        foreach (string y in new[] {
            "Assets/Sea/Shaders/SeaSpectrum.compute",
            "Assets/Sea/Shaders/SeaFFT.compute" })
        {
            AssetDatabase.ImportAsset(y, ImportAssetOptions.ForceSynchronousImport);
            var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(y);
            result.Log(y.Substring(y.LastIndexOf('/') + 1) + " yuklendi=" + (cs != null));

            foreach (var m in ShaderUtil.GetComputeShaderMessages(cs))
                result.LogError("  " + m.line + ": " + m.message);
        }

        var spek = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Sea/Shaders/SeaSpectrum.compute");
        result.Log("KInitialSpectrum = " + spek.HasKernel("KInitialSpectrum"));
        result.Log("KTimeSpectrum    = " + spek.HasKernel("KTimeSpectrum"));

        var fft = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Sea/Shaders/SeaFFT.compute");
        result.Log("KIFFTHorizontal = " + fft.HasKernel("KIFFTHorizontal"));
        result.Log("KIFFTVertical   = " + fft.HasKernel("KIFFTVertical"));
    }
}
```

Beklenen: dördü de `True`.

- [ ] **Adım 6: Sayısal doğrulama — `mean(h)`**

Spec §6.8: **IFFT çıktısının ortalaması sıfıra çok yakın olmalı.** Değilse eşlenik simetri bozuk.

`Editor/SeaSpectrumTest.cs` yazılır; `RT_Displacement`'ın `y` kanalını CPU'ya okur (async readback) ve ortalamayı ölçer.

Beklenen: `|mean(h)| < 1e-3`.

- [ ] **Adım 7: Ekran doğrulaması — spec Faz 2 kabul kriterleri**

Kullanıcı Play'e basar, `SeaDebugWindow` açar:

1. **`RT_H0` merkeze göre simetrik ve gürültülü mü?** Düzenli desen görünüyorsa **RNG bozuk** (spec §6.1).
2. **`RT_Displacement.y` suya benzemiyor olabilir — bu NORMALDİR.** `[KAYNAK: Tessendorf 2004 Şekil 9/10]`
3. **`RT_Derivatives.x` su dalgası gibi görünmeli.** Bu, sistemin doğru çalıştığının en iyi göstergesi.
4. **Rüzgârı 3 → 15 m/s yap.** Dalga yüksekliği belirgin artmalı.
5. **Rüzgâr yönünü çevir.** Dalga yönü dönmeli.
6. **`_Swell` 0 → 1.** Dalgalar paralel trenlere dönüşmeli.
7. **Rüzgâr sabitken `KInitialSpectrum` çalışmamalı** (Profiler'da doğrula).

- [ ] **Adım 8: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea && git commit -m "Deniz Faz 2: TMA spektrumu ve IFFT"
```

---

## Faz 3 — Jacobian ve köpük verisi

**Amaç:** Katlanan yüzeyi tespit etmek. Bu, köpüğün **tek** kaynağı değil ama açık denizdeki tek kaynağı.

- [ ] **Adım 1: Jacobian — analitik, sayısal çözüm yok**

Spec §7, `[KAYNAK: Tessendorf 2004 §4.6 denklem 45–49]`. `KFFTVertical` çıkışında veya ayrı `KFoam` kernelinde:

```hlsl
float Jxx = 1.0 + lambda * dDxdx;
float Jyy = 1.0 + lambda * dDzdz;
float Jxy = lambda * dDxdz;
float J   = Jxx * Jyy - Jxy * Jxy;

// Ozdegerler ANALITIK - sayisal cozum gerekmiyor (spec 7)
float ort  = 0.5 * (Jxx + Jyy);
float fark = 0.5 * sqrt((Jxx - Jyy) * (Jxx - Jyy) + 4.0 * Jxy * Jxy);
float Jm   = ort - fark;      // minimum ozdeger: katlanmanin baslangic isareti

// Ozvektor e- : katlanmanin gerceklestigi YATAY YON (spec 13.2'de kullanilacak)
float qm = (Jm - Jxx) / max(Jxy, 1e-6);
float2 em = normalize(float2(1.0, qm));
```

`J` → `RT_Displacement.w`. `em` → `RT_Derivatives.zw`.

`∂Dx/∂x` gibi türevler **FFT ile** üretilir (ayrı spektrum kanalı), sonlu farkla değil — spec §6.7'nin gerekçesi burada da geçerli.

- [ ] **Adım 2: `SeaFoam.compute` — `KFoam`**

Spec §13.2. Köpük **anında oluşur, yavaş kaybolur**:

```hlsl
float J = _Displacement[id].w;
float target = saturate((SEA_FOAM_J_THRESHOLD - J) / SEA_FOAM_J_RANGE);

float prev = _FoamPrev[id];
float next = max(target, prev - SEA_FOAM_DECAY * _DeltaTime);
_Foam[id] = saturate(next);
```

**Doğrudan atama yapılmaz** — spec §18 tuzak: "Köpük anında kayboluyor / sönüm yerine doğrudan atama yapılmış".

`RT_Foam` ping-pong: `_FoamPrev` ve `_Foam` her frame yer değiştirir.

- [ ] **Adım 3: Sayısal doğrulama — Jacobian dağılımı**

`SeaSpectrumTest`'e eklenir:

```
J < 0 olan teksel orani, U10 = 10 m/s, choppiness 1.1 icin:
  beklenen  %0 - %8
  %0 ise    choppiness etkisiz veya displacement spektrumu bagli degil
  %20 ustu  choppiness cok yuksek, yuzey dugumlenecek (spec 18)
```

- [ ] **Adım 4: Ekran doğrulaması — spec Faz 3 kabul kriterleri**

`SeaDebugWindow`'da `RT_Foam`:

1. **Dalga tepelerinde köpük var, çukurlarda yok.**
2. **`_Choppiness` artınca köpük belirgin artıyor.**
3. **Köpük anında oluşup yavaş kayboluyor** (sönümü gözle takip et).

- [ ] **Adım 5: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea && git commit -m "Deniz Faz 3: Jacobian ve kopuk verisi"
```

---

## Faz 4 — Bathymetry

**Amaç:** Her teksel için su derinliği. Sığlaşma, kırılma ve kıyı sönümünün tamamı buna bağlı.

- [ ] **Adım 1: Deniz seviyesini seç**

Ölçüm planın başında. Seçim `SeaSettings._SeaLevelY`, ve **gerekçesi `DECISIONS.md`'ye yazılır**.

Öneri: **10 m** — alanın %17.3'ü su, kıyı çizgisi arazinin doğal alçak bandında. 5 m'de deniz küçük kalıyor, 40 m'de dağın eteği fazla suya giriyor.

Bu bir başlangıç değeri; oyun tasarımı kıyıyı nereye istiyorsa oraya çekilir.

- [ ] **Adım 2: `SeaBathymetry.cs` — CPU'da bir kez bake**

Spec §9 birebir. İki tuzak:

```csharp
// GetHeights [y, x] SIRALI doner - indeks sirasina dikkat (spec 9).
float[,] hm = td.GetHeights(0, 0, res, res);

// terrainData.heightmapTexture SHADER'DA DOGRUDAN ORNEKLENMEZ: Unity
// surumleri arasinda olcekleme sabitleri degisiyor. CPU'da bir kez bake
// etmek belirsizligi ortadan kaldiriyor (spec 9).
```

Format `RHalf`, `wrapMode = Clamp`, `filterMode = Bilinear`.

Globaller: `_BathyOriginXZ`, `_BathySizeXZ`, `_SeaLevelY`, `_BathyResolution`.

**Çoklu terrain desteklenmiyor** — birden fazla varsa `SeaManager` hata basıp devre dışı kalır (spec §9, §17).

- [ ] **Adım 3: Shader tarafı — `SeaCommon.hlsl`'e ekle**

```hlsl
// >0 su, <0 kara
float SampleDepth(float2 posXZ)
{
    float2 uv = (posXZ - _BathyOriginXZ) / _BathySizeXZ;
    if (any(uv < 0) || any(uv > 1)) return _DeepWaterDepth;   // terrain disi = derin
    return SAMPLE_TEXTURE2D_LOD(_BathyTex, sampler_LinearClamp, uv, 0).r;
}

// Taban egimi - kirilma indeksi icin (spec 8.3)
float SampleBottomSlope(float2 posXZ)
{
    float e = _BathySizeXZ.x / _BathyResolution;
    float dx = SampleDepth(posXZ + float2(e, 0)) - SampleDepth(posXZ - float2(e, 0));
    float dz = SampleDepth(posXZ + float2(0, e)) - SampleDepth(posXZ - float2(0, e));
    return length(float2(dx, dz)) / (2.0 * e);
}
```

`_DeepWaterDepth = 200 m` `[KALİBRASYON]`.

- [ ] **Adım 4: `RefreshBathymetry()`**

Runtime'da terrain değişirse çağrılır (spec §9). Bu projede arazi bootstrap'te üretiliyor; `MountainSceneBootstrap` arazi yeniden üretince bu metodu çağırmalı.

- [ ] **Adım 5: Sayısal doğrulama**

Üç bilinen noktada derinlik:

```csharp
// - oyuncunun basladigi yer (205.5 m)  -> yaklasik -195 m (kara)
// - arazinin en alcak noktasi (0 m)    -> yaklasik +10 m (su)
// - arazi disi                          -> 200 m (acik deniz)
```

Ayrıca `SampleBottomSlope`: kıyıda pozitif, açık denizde sıfıra yakın.

- [ ] **Adım 6: Ekran doğrulaması — spec Faz 4 kabul kriterleri**

`SeaDebugWindow`'da derinlik dokusu:

1. **Kıyı çizgisi doğru yerde mi?** Arazinin alçak bandıyla örtüşmeli.
2. **`SampleBottomSlope` kıyıda pozitif, açık denizde sıfıra yakın.**
3. **Çoklu terrain varsa hata basıp duruyor.**

- [ ] **Adım 7: Commit + `DECISIONS.md`**

Deniz seviyesi kararı ve gerekçesi `DECISIONS.md`'ye yazılır (tetikleyici: oyun tasarımı kıyıyı başka yere isterse).

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea DECISIONS.md && git commit -m "Deniz Faz 4: bathymetry ve deniz seviyesi karari"
```

---

## Faz 5 — Mesh

**Amaç:** Denizi çizecek geometri. **Spec'in en sık hata alınan bölümü; kuralları harfiyen uygulanacak.**

- [ ] **Adım 1: Neden tek ızgara — spec §10.1**

Çok seviyeli geometry clipmap **kurulmuyor**. Kurulsaydı `[KAYNAK: Asirvatham & Hoppe, GPU Gems 2 Bölüm 2]`'nin şu parçalarının **hepsi** gerekirdi: tek sayı ızgara boyutu, 12 blok, dört `m×3` fix-up şeridi, dört yönelimli L-trim, dejenere üçgen çevresi, `alpha = max(αx, αy)` geçiş harmanlaması. Biri eksikse mesh yırtılır.

Yerine **tek, sürekli mesh**. Merkeze yakın quad'lar küçük, uzaklaştıkça **ikinin kuvveti** adımlarla büyük.

**Hizalama ispatı (spec §10.1):** tüm quad boyutları en ince quad boyutunun ikinin kuvveti katı. Dolayısıyla en ince quad boyutuna eşit **tek bir snap adımı** her halkanın vertex'lerini kendi kafesinde tutar. Seviye başına ayrı snap gerekmez, seviyeler arası kayma olamaz.

- [ ] **Adım 2: `SeaMeshBuilder.cs` — spec §10.2 tablosu birebir**

| Halka | Yarıçap | Quad boyu |
|---|---|---|
| 0 | 0 – 32 m | 0.5 m (128×128 dolu kare) |
| 1 | 32 – 96 m | 1.0 m |
| 2 | 96 – 224 m | 2.0 m |
| 3 | 224 – 480 m | 4.0 m |
| 4 | 480 – 992 m | 8.0 m |
| 5 | 992 – 2016 m | 16.0 m |
| 6 | 2016 – 4064 m | 32.0 m |

Toplam ≈ 240 000 quad = 480 000 üçgen, **1 draw call**.

Kurallar (spec §10.2, hepsi zorunlu):

```csharp
mesh.indexFormat = IndexFormat.UInt32;        // vertex > 65535
mesh.bounds = new Bounds(Vector3.zero, new Vector3(8192, 400, 8192));
```

**Halkalar arası vertex PAYLAŞILIR:** dış halkanın iç kenarındaki her vertex, iç halkanın dış kenarındaki iki vertex'ten biriyle **aynı indekstir**. T-junction ve dikiş yapısal olarak imkânsız olur.

Materyal `Queue = Transparent - 1` (spec §12.6).

- [ ] **Adım 3: `SeaSurface.cs` — konumlandırma ve snap**

Spec §10.3 birebir:

```csharp
const float FinestQuad = 0.5f;
float SnapStep = FinestQuad;

Vector3 c = cameraTransform.position;
float sx = Mathf.Floor(c.x / SnapStep) * SnapStep;
float sz = Mathf.Floor(c.z / SnapStep) * SnapStep;
seaTransform.position = new Vector3(sx, _SeaLevelY, sz);
```

Deniz mesh'i **kamerayı takip eder**, oyuncuyu değil.

`SnapStep` FFT teksel boyutuyla ilişkili **olmak zorunda değildir** — FFT dokusu dünya koordinatından örneklenir. Kar sistemindeki `SnapStep / texelSize` tam sayı kuralı burada **geçerli değildir** (spec §10.3 bunu açıkça söylüyor).

Materyal `Update` içinde `EnsureMaterial` deseniyle kurulur — `AssetDatabase.ImportAsset` runtime materyalini düşürüyor (`TerrainSurface` bunu yaşadı).

- [ ] **Adım 4: `SeaLit.shader` — geçici düz mavi**

Bu fazda optik yok. Vertex shader spec §10.4'ün FFT örneklemesi + `seaMask`; fragment düz mavi + normal.

**`frac()` KULLANILMAZ** (spec §10.4): doku `wrapMode = Repeat` ve donanım zaten tekrarlıyor; `frac()` teksel sınırlarında dikiş yaratır.

Normal spec §10.5: **FFT eğim dokusundan**, merkezi fark değil. Uzakta sönüm:

```hlsl
float distFade = saturate(1.0 - (dist - 120.0) / 400.0);   // [KALIBRASYON]
N = normalize(lerp(float3(0,1,0), N, distFade));
```

- [ ] **Adım 5: Deniz seviyesi altındaki arazi — ONAY GEREKLİ**

Spec §1.3: bu sahne değişikliği ve **ayrı onay** istiyor. Kullanıcıya sorulacak: deniz seviyesinin altında kalan arazi görünür kalsın mı (refraksiyonda taban görünür), yoksa kesilsin mi?

**Öneri: görünür kalsın.** Faz 7'de refraksiyon o tabanı gösterecek; kesilirse su dipsiz görünür.

- [ ] **Adım 6: Doğrulama — spec §10.6'nın ALTI TESTİ**

Bu testler geçmeden Faz 6'ya geçilmez:

1. **Wireframe'de kamerayı gezdir.** Vertex'ler dünyaya sabit mi, kayıyor mu?
2. **Displacement'ı kapat** (`disp = 0`). Yüzey hâlâ bozuksa sorun mesh üretiminde, dalga sentezinde değil.
3. **Halka sınırlarına yakın bak.** Delik veya yırtık varsa vertex paylaşımı yanlış.
4. **Frame Debugger'da draw call sayısı 1 olmalı.**
5. **Kamerayı yukarı çevir.** Deniz kayboluyorsa `mesh.bounds` dar.
6. **Ufka bak.** Kesik kenar görünüyorsa halka 6 yarıçapı yetersiz.

Ek: dalgalar mesh üzerinde görünüyor ve kamera hareket ederken yüzey dünyaya sabit duruyor.

- [ ] **Adım 7: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea && git commit -m "Deniz Faz 5: tek izgara mesh ve gecici yuzey"
```

---

## Faz 6 — Sığ su dönüşümü

**Amaç:** Kıyıdan bakılan denizin görüntüsünü belirleyen bölüm. Üç ayrı fiziksel olay, üç ayrı kaynaktan.

- [ ] **Adım 1: `SeaShallow.hlsl` — sığlaşma (Green yasası)**

Spec §8.1:

```hlsl
// Yavas degisen bir egim uzerinde ilerleyen dalganin genligi h^(-1/4) ile
// orantili buyur. [KAYNAK: Green yasasi]
float ShoalingGain(float depthLocal, float depthRef)
{
    float d = max(depthLocal, SEA_MIN_DEPTH);
    return pow(depthRef / d, 0.25);
}
```

Kazanç `_MaxShoalingGain = 2.2` ile sınırlanır `[KALİBRASYON]` — Green yasası çok sığ suda sınırsıza gider, gerçekte kırılma devreye girer.

- [ ] **Adım 2: Kısalma ve dikleşme**

Spec §8.2:

```hlsl
// Yatay displacement sig suda azalir: dalga dikelesir, yatayda yayilmaz.
float chopScale = saturate(depthLocal / SEA_CHOP_FADE_DEPTH);   // 8.0 m
displacement.xz *= chopScale;
```

- [ ] **Adım 3: Kırılma — eğime bağlı γ**

Spec §8.3. **Sabit 0.78 kullanılmaz** (spec §17 karar tablosu):

```hlsl
// m = taban egimi (tan theta), bathymetry gradyanindan.
// 0.55 (cok hafif egim) -> 0.78 (McCowan referansi) -> 1.10 (dik)
float BreakerIndex(float slope)
{
    return lerp(SEA_GAMMA_MILD, SEA_GAMMA_STEEP, saturate(slope / 0.10));
}
```

`[KAYNAK: McCowan 1894]` γ ≈ 0.78; `[KAYNAK: Nelson 1983; DNV 2017]` alt sınır 0.55; `[KAYNAK: Galvin 1969; Weggel 1972]` dik sahilde 1.0 üstü.

Kırılma testi ve tepki:

```hlsl
float gamma  = BreakerIndex(bottomSlope);
float H      = 2.0 * abs(waveHeight);
float ratio  = H / max(depthLocal, SEA_MIN_DEPTH);
float breakT = saturate((ratio - gamma * 0.7) / (gamma * 0.3));

float hMax = gamma * depthLocal;
waveHeight = sign(waveHeight) * min(abs(waveHeight), hMax * 0.5);

foam += breakT * SEA_BREAK_FOAM_GAIN;
```

Derin su dikliği sınırı `[KAYNAK: Michell 1893]` `H/L = 0.142` için **ayrı kontrol yazılmaz** — FFT çıktısında aşılırsa Jacobian testi zaten köpük üretiyor (spec §8.3 son paragraf).

- [ ] **Adım 4: Kıyı çizgisi sönümü**

Spec §8.4:

```hlsl
float shoreFade = smoothstep(0.0, SEA_SHORE_FADE_DEPTH, depthLocal);
waveHeight      *= shoreFade;
displacement.xz *= shoreFade;
```

Bu olmadan mesh araziyle kesişir ve titrer (spec §18 tuzak tablosu).

- [ ] **Adım 5: Kabarma (run-up) bandı**

Spec §8.5. Periyot spektrumun tepe periyoduna bağlı:

```hlsl
float phase      = _Time.y * (SEA_TWO_PI / _PeakPeriod);
float runup      = sin(phase) * 0.5 + 0.5;
float runupDepth = lerp(0.0, _RunupMaxDepth, runup);       // 0.45 m [KALIBRASYON]
float effectiveDepth = depthLocal + runupDepth;
```

`SeaRuntimeState.ShoreFoamIntensity01` bu fazdan türetilir. Faz 8 (kıyı köpüğü) ve Faz 9 (ıslak kum) buna bağlanacak.

- [ ] **Adım 6: Vertex shader'a bağla**

Spec §10.4'ün sığ su bloğu:

```hlsl
float slope     = SampleBottomSlope(posWS.xz);
float shoal     = min(ShoalingGain(depth, _SpectrumDepth), _MaxShoalingGain);
float chopScale = saturate(depth / SEA_CHOP_FADE_DEPTH);
float shoreFade = smoothstep(0.0, SEA_SHORE_FADE_DEPTH, depth);

disp.y  *= shoal * shoreFade;
disp.xz *= chopScale * shoreFade;

float gamma = BreakerIndex(slope);
float hMax  = gamma * depth * 0.5;
disp.y = sign(disp.y) * min(abs(disp.y), hMax);
```

- [ ] **Adım 7: Ekran doğrulaması — spec Faz 6 kabul kriterleri**

Kullanıcı kıyıya gider ve bakar:

1. **Kıyıya yaklaşan dalgalar yükseliyor** (§8.1).
2. **Kıyıda yatay displacement azalıyor, dalgalar dikleşiyor** (§8.2).
3. **Dalga yüksekliği `γ·depth` sınırını aşmıyor** (§8.3).
4. **Kıyı çizgisinde dalga yüksekliği sıfıra iniyor, mesh araziyle kesişmiyor.**
5. **Taban eğimini değiştirince kırılma noktası kayıyor.**
6. **Açık denizde (`depth > 60 m`) hiçbir modülasyon yok.**

- [ ] **Adım 8: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea && git commit -m "Deniz Faz 6: siglasma, kirilma, kiyi sonumu"
```

---

## Faz 7 — Optik

**Amaç:** Suyu su gibi göstermek. Spec §12'nin tamamı.

- [ ] **Adım 1: Tam Fresnel — Schlick DEĞİL**

Spec §12.1 ve §17. Tessendorf'un örnek shader'ından **birebir**:

```hlsl
float SeaFresnel(float3 N, float3 V)
{
    float cosThetaI = abs(dot(V, N));
    float thetaI    = acos(saturate(cosThetaI));
    float sinThetaT = sin(thetaI) / SEA_WATER_IOR;
    if (sinThetaT >= 1.0) return 1.0;               // tam ic yansima
    float thetaT = asin(sinThetaT);

    if (thetaI < 1e-4)
    {
        float r = (SEA_WATER_IOR - 1.0) / (SEA_WATER_IOR + 1.0);
        return r * r;
    }

    float fs = sin(thetaT - thetaI) / sin(thetaT + thetaI);
    float ts = tan(thetaT - thetaI) / tan(thetaT + thetaI);
    return 0.5 * (fs * fs + ts * ts);
}
```

**Schlick kullanılmaz** — sıyırma açılarında belirgin sapar ve deniz görüntüsünde asıl karakter tam orada (spec §12.1, Tessendorf §6.2 Şekil 24).

- [ ] **Adım 2: Su hacmi — soğurma ve yukarı ışıma**

Spec §12.2, `[KAYNAK: Tessendorf 2004 §7.1]`:

```hlsl
float3 SeaVolumeColor(float waterPathLength)
{
    float3 K = _ExtinctionRGB;      // (0.30, 0.08, 0.05) 1/m [KALIBRASYON]
    return exp(-K * waterPathLength);
}
```

Kırmızı en hızlı, mavi en yavaş sönümlenir — suyun mavi görünmesinin sebebi.

`_UpwellingColor = (0.00, 0.20, 0.30)` `[KAYNAK: Tessendorf 2004 §6.3 örnek shader]`.

- [ ] **Adım 3: Refraksiyon — derinlik kontrolüyle**

Spec §12.3. Kontrol **atlanamaz**:

```hlsl
float2 refrOffset = N.xz * _RefractionStrength / max(dist, 1.0);   // 0.35 [KALIBRASYON]
float2 refrUV     = screenUV + refrOffset;

// Saptirma su yuzeyinin ONUNDEKI bir nesneyi orneklerse artefakt olur.
// Bu kontrol atlanirsa kiyida su, onundeki kayanin rengini "icine ceker".
float sceneDepthRefr = SampleSceneDepth(refrUV);
if (LinearEyeDepth(sceneDepthRefr) < i.screenPos.w) refrUV = screenUV;

float3 refracted = SampleSceneColor(refrUV);
```

Su kalınlığı:

```hlsl
float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
float thickness     = max(sceneEyeDepth - i.screenPos.w, 0.0);
float3 volume       = SeaVolumeColor(thickness);
float3 belowSurface = lerp(_UpwellingColor, refracted * volume, volume);
```

- [ ] **Adım 4: Yansıma — kendi gökyüzü modeli kurulmaz**

Spec §12.4, üç kaynak öncelik sırasıyla:

1. Sahnede `ReflectionProbe` varsa ondan (`GlossyEnvironmentReflection`)
2. Yoksa `env.SkyColor` / `env.HorizonColor` arasında lerp
3. `env.CloudCover01` ile karartma

```hlsl
float3 R = reflect(-V, N);
float3 skyRefl = lerp(_HorizonColor, _SkyColor, saturate(R.y));
skyRefl = lerp(skyRefl, skyRefl * 0.62, _CloudCover01);       // [KALIBRASYON]
```

**Planar reflection yazılmaz** (spec §12.4, §17): ekstra tam sahne render'ının maliyeti kazandırdığından fazla, ayrıca mevcut bulut sistemiyle çakışır.

- [ ] **Adım 5: Güneş parıltısı — gece kapanıyor, uzakta yayılıyor**

Spec §12.5:

```hlsl
float3 H = normalize(V + L);
float  roughness = lerp(_RoughnessCalm, _RoughnessRough, saturate(_WindSpeed / 20.0));
float3 spec = DirectBRDFSpecular(brdf, N, L, V);

// Gece parilti yok - gundongusunden geliyor
spec *= saturate(_SunElevation01 * 20.0);
```

`_RoughnessCalm = 0.02`, `_RoughnessRough = 0.14` `[KALİBRASYON]`.

Uzaktaki parıltı yayınık `[KAYNAK: Tessendorf 2004 §6 giriş]`:

```hlsl
roughness = lerp(roughness, 0.35, saturate((dist - 200.0) / 1500.0));   // [KALIBRASYON]
```

- [ ] **Adım 6: Birleştirme ve sis — SIRA ÖNEMLİ**

Spec §12.6:

```hlsl
float  F     = SeaFresnel(N, V);
float3 color = lerp(belowSurface, skyRefl, F) + spec;
color = lerp(color, _FoamColor, foam);          // KOPUK FRESNEL'DEN SONRA
color = MixFog(color, i.fogCoord);              // URP'nin kendi sisi
```

Köpük Fresnel'den **sonra** — köpük opak, saçan bir yüzey; altındaki suyun yansımasını göstermez (spec §18 tuzak: "Köpük yansıma gösteriyor").

**Kendi sis hesabı yazılmaz** (spec §3.5): `MixFog` kullanılır.

Materyal: `Queue = Transparent - 1`, `ZWrite On`, `Blend Off` — **opak çizilir**. Şeffaflık hissi refraksiyon ve soğurmadan gelir, alpha'dan değil. Bu, sıralama sorunlarını ve TAA hayaletlemesini ortadan kaldırır.

`seaMask == 0` fragment'lar `clip(-1)` ile atılır.

- [ ] **Adım 7: Ekran doğrulaması — spec Faz 7 kabul kriterleri**

1. **Sıyırma açısında su gökyüzünü yansıtıyor, tepeden bakınca içi görünüyor** (Fresnel çalışıyor).
2. **Sığ suda taban görünüyor, derinleştikçe mavileşiyor ve kayboluyor.**
3. **Refraksiyon var ama kıyıdaki kayalar suyun içine "sızmıyor"** (§12.3 kontrolü).
4. **Gündüz güneş parıltısı var, gece yok.**
5. **Uzaktaki parıltı yakındakinden daha yayınık.**
6. **Mevcut sis denizin üstünde doğru çalışıyor** (`MixFog` kullanılmış).
7. **Bulut örtüsünü artırınca yansıma kararıyor.**

Bu projede ek olarak **dört saatte birden** bakılır (kar sisteminde kullanılan desen): 07:00, 12:00, 17:30, 21:00. Gece parıltı **olmamalı**.

- [ ] **Adım 8: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea && git commit -m "Deniz Faz 7: optik - Fresnel, refraksiyon, hacim, parilti"
```

---

## Faz 8 — Köpük render

**Amaç:** Üç köpük kaynağını birleştirip çizmek.

- [ ] **Adım 1: Üç kaynak toplanır**

Spec §13.1:

```hlsl
float foam = 0;
foam += whitecapFoam;      // 13.2 - Jacobian, acik denizde tepe kopugu
foam += breakFoam;         // 8.3  - derinlik kaynakli kirilma
foam += shoreFoam;         // 13.3 - kiyi cizgisi
foam  = saturate(foam);
```

- [ ] **Adım 2: Tepe köpüğü — katlanma yönünde uzatılır**

Spec §13.2. `ê₋` özvektörü Faz 3'te `RT_Derivatives.zw`'ye yazılmıştı:

```hlsl
float2 foldDir = _Derivatives[id].zw;
float2 foamUV  = mul(Rotate2D(atan2(foldDir.y, foldDir.x)), posWS.xz * _FoamTiling);
foamUV.x *= 0.35;                                   // katlanma yonunde uzat
```

`[KAYNAK: Tessendorf 2004 denklem 48 — ê₋ katlanma yönünü gösterir]`

- [ ] **Adım 3: Kıyı köpüğü — kenar gürültüyle kırılır**

Spec §13.3, `[KAYNAK: Crest, SIGGRAPH 2017]`:

```hlsl
float effDepth  = depth + runupDepth;                                  // 8.5
float shoreFoam = 1.0 - smoothstep(0.0, _ShoreFoamDepth, effDepth);    // 1.2 m [KALIBRASYON]

// Kirilan dalga kiyiya dogru ilerlerken kopuk bandi da ilerler
shoreFoam *= 0.4 + 0.6 * _ShoreFoamPhase;

// Kenari gurultuyle kir, DUZ CIZGI OLMASIN (spec 18 tuzak tablosu)
float n = SAMPLE_TEXTURE2D(_FoamBreakup, sampler_FoamBreakup, posWS.xz * 0.35).r;
shoreFoam = saturate((shoreFoam - n * 0.45) * 2.5);                    // [KALIBRASYON]
```

- [ ] **Adım 4: Köpük shading — saçan yüzey, parlak değil**

Spec §13.4:

```
_FoamColor      = (0.92, 0.94, 0.95)     [KALIBRASYON]
_FoamRoughness  = 0.85                    [KALIBRASYON]
```

Köpük altındaki suyu kısmen gösterir (kabarcıklar) — refraksiyonu tamamen kesmez:

```hlsl
color = lerp(color, _FoamColor * lightAtten, foam * 0.9);
```

- [ ] **Adım 5: Yağmurun etkisi**

Spec §13.5:

```hlsl
roughness = lerp(roughness, 0.22, _PrecipIntensity01 * 0.7);   // [KALIBRASYON]
foam     += _PrecipIntensity01 * 0.06;                          // [KALIBRASYON]
```

Yağmur damlası halkaları için **ayrı sistem yazılmaz** — mevcut yağmur VFX'i zaten var (spec §13.5).

Bu projede `PrecipKind` kar da olabilir: kar yağarken deniz yüzeyine köpük eklenmez, yalnız yağmur. `WeatherState`'in ayrımı Faz 1'de köprüye bağlanmıştı.

- [ ] **Adım 6: Dokular**

`T_Foam.png` ve `T_FoamBreakup.png`. Bu projede doku üretimi **kredili servis** kullanıyorsa `CLAUDE.md` "Varlık üretimi" kuralları geçerli: teknik güvenlik ve işin amacı ayrı ayrı karşılanır, ilk deneme doğru olmalı.

Alternatif: prosedürel gürültü (`SnowValueNoise` deseni) ile başlanır, doku sonradan takılır.

- [ ] **Adım 7: Ekran doğrulaması — spec Faz 8 kabul kriterleri**

1. **Tepe köpüğü katlanma yönünde uzuyor.**
2. **Kıyı köpüğü bandı dalgalarla nefes alıyor.**
3. **Köpük kenarı gürültülü, düz çizgi değil.**
4. **Köpük yansımayı kesiyor** (§12.6 sıralaması doğru).

- [ ] **Adım 8: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea && git commit -m "Deniz Faz 8: kopuk render"
```

---

## Faz 9 — Islak kum

**Amaç:** Denizi araziye bağlayan en görünür detay. **Mevcut arazi shader'ına dokunuyor — ayrı onay gerekli.**

- [ ] **Adım 1: `SeaWetnessDriver.cs`**

Spec §14. Yalnız global uniform yazar:

```csharp
Shader.SetGlobalFloat(SeaShaderIDs.SeaWetLevelY, _SeaLevelY + maxRunupHeight);
Shader.SetGlobalFloat(SeaShaderIDs.SeaWetFadeM,  _WetFadeMeters);   // 0.35 [KALIBRASYON]
```

`maxRunupHeight` Faz 6'daki kabarma fazından türer — ıslak bant dalgalarla nefes alır.

- [ ] **Adım 2: Arazi shader'ına eklenecek iki satırı RAPORLA**

Spec §14 son paragraf: **"Mevcut arazi shader'ını sen değiştirme. Faz 9'da bu iki satırın nereye ekleneceğini raporla, kullanıcı onaylasın."**

Bu projede hedef `Assets/Shaders/MountainSurface.hlsl`. Eklenecek:

```hlsl
float wet = 1.0 - smoothstep(_SeaWetLevelY - _SeaWetFadeM, _SeaWetLevelY, positionWS.y);

albedo    = lerp(albedo, albedo * _WetDarkening, wet);          // 0.55 [KALIBRASYON]
roughness = lerp(roughness, roughness * 0.35, wet);             // [KALIBRASYON]
```

**Nereye:** `MountainSurface.hlsl` içinde kaya albedo/smoothness'ının kurulduğu yer (`surface.smoothness = lerp(_RockSmoothness, _WetSmoothness, wet)` satırının civarı). O blokta zaten bir `wet` değişkeni var (yağmur ıslaklığı) — **ad çakışması** olacak, deniz ıslaklığı ayrı adla eklenmeli.

**Ölç ve raporla:**

```bash
cd /d "D:\ME\game\to the summit" && grep -n "wet\|_WetSmoothness\|_SurfaceWetness" Assets/Shaders/MountainSurface.hlsl | head -20
```

Kullanıcı onaylamadan **hiçbir satır eklenmez**.

- [ ] **Adım 3: Onay sonrası uygula ve `SYSTEMS.md`'ye yaz**

Bu, kar sistemi ile deniz sistemi arasında **yeni bir bağ**: arazi materyali artık iki ıslaklık kaynağı okuyor (yağmur ve deniz). `SYSTEMS.md`'ye aynı adımda yazılır, `CLAUDE.md` atmosfer tutarlılığı gereği.

- [ ] **Adım 4: Ekran doğrulaması — spec Faz 9 kabul kriterleri**

1. **Kıyıda ıslak bant var.**
2. **Dalga çekilince bant daralıyor.**
3. **Islak kum daha koyu ve daha parlak.**

- [ ] **Adım 5: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea Assets/Shaders SYSTEMS.md && git commit -m "Deniz Faz 9: islak kum bandi"
```

---

## Faz 10 — Profiler ve kalite presetleri

**Amaç:** Maliyeti ölçmek ve kademelendirmek.

- [ ] **Adım 1: `SeaProfiler.cs`**

Her pass'in ms'i. Spec §15.1 hedefleri:

- compute toplamı **< 1.2 ms**
- deniz çizimi **< 1.5 ms**
- **deniz görünmezken toplam < 0.05 ms**

**Mevcut oyunun frame bütçesiyle karşılaştırmadan kabul edilmez** (spec §15.1). Bu projede referans: kar sistemi + bulutlar + arazi çalışırken ölçülen mevcut kare süresi.

- [ ] **Adım 2: Zorunlu optimizasyonları doğrula**

Spec §15.2, her biri ayrı kontrol:

| Optimizasyon | Nasıl doğrulanır |
|---|---|
| Spektrum her frame hesaplanmıyor | Profiler'da `KInitialSpectrum` rüzgâr sabitken görünmemeli |
| Deniz görünmüyorsa her şey kapalı | Kamerayı denizden çevir, compute pass'leri kaybolmalı |
| Tek `CommandBuffer` | Frame Debugger |
| Yarım hassasiyet | Tüm RT'ler `Half` — kod araması |
| SRP Batcher uyumu | Frame Debugger'da "SRP Batch" görünmeli |
| Kalıcı allocation | `GetTemporary` kod aramasında çıkmamalı |
| Bathymetry bir kez bake | `Awake` dışında çağrı olmamalı |

```bash
cd /d "D:\ME\game\to the summit" && grep -rn "GetTemporary\|RenderTextureFormat.ARGBFloat\|RenderTextureFormat.RFloat" Assets/Sea/ || echo "TEMIZ"
```

- [ ] **Adım 3: Üç kalite preseti**

Spec §15.3 tablosu birebir. Shader keyword'leri `_SEA_QUALITY_LOW/_MEDIUM/_HIGH`.

**Kar sisteminden ders:** keyword `Shader.EnableKeyword` ile açılıyorsa shader'da `#pragma multi_compile` **olmak zorunda**. Yoksa varyant hiç derlenmiyor ve `#if defined(...)` sessizce false kalıyor — kar sisteminde üç detay katmanı bu yüzden hiç çalışmamıştı.

```hlsl
#pragma multi_compile _SEA_QUALITY_LOW _SEA_QUALITY_MEDIUM _SEA_QUALITY_HIGH
```

- [ ] **Adım 4: Ekran doğrulaması — spec Faz 10 kabul kriterleri**

1. **Her pass'in ms'i görünüyor.**
2. **compute < 1.2 ms, deniz çizimi < 1.5 ms.**
3. **Deniz görünmezken toplam < 0.05 ms.**
4. **Üç preset de çalışıyor** (keyword'ü değiştir, görüntü ve maliyet değişmeli).

- [ ] **Adım 5: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add Assets/Sea && git commit -m "Deniz Faz 10: profiler ve kalite presetleri"
```

---

## Faz 11 — Belgeler

Spec'te yok; `CLAUDE.md` gereği. Diğer fazlar boyunca **aynı adımda** yazılanların toplanması ve eksik kalanların tamamlanması.

- [ ] **Adım 1: `SYSTEMS.md` — bağ haritası**

Yeni bölüm: **Deniz.** Ne neyi okuyor, ne neyi okumuyor:

```
Deniz OKUR:   ruzgar (U10 ve yon), gunes ve gundongusu, gokyuzu rengi,
              bulut kapsamasi, sis yogunlugu, yagis turu ve siddeti,
              arazi yuksekligi (bathymetry, bir kez)

Deniz YAZAR:  SeaRuntimeState (Hs, Tp, whitecap, kiyi kopugu) — yalniz
              yayin, kimse uygulamak zorunda degil
              _SeaWetLevelY / _SeaWetFadeM (Faz 9, arazi materyali okuyor)

Deniz ASLA YAZMAZ: RenderSettings, VolumeProfile, Light.intensity,
              sis ayarlari, bulut durumu, hava durumu
```

Ve bilinçli kurallar: kendi gökyüzü modeli yok, kendi sis hesabı yok, kendi rüzgâr noise'u yok, planar reflection yok.

- [ ] **Adım 2: `RATIONALE.md` — gerekçeler**

En az üç kayıt:

- **Neden Phillips değil TMA** (spec §6.2: Phillips yüksek dalga sayılarında kötü yakınsıyor ve elle ayar istiyor; TMA derinlik parametresi taşıyor ve kıyı için zorunlu)
- **Neden Schlick değil tam Fresnel** (sıyırma açılarında sapıyor, deniz karakteri tam orada)
- **Neden çok seviyeli clipmap değil tek ızgara** (clipmap'in altı zorunlu parçası, biri eksikse mesh yırtılıyor; hizalama ispatı)

- [ ] **Adım 3: `DECISIONS.md` — kararlar ve tetikleyiciler**

- **Deniz seviyesi** (Faz 4'te seçilen değer, tetikleyici: oyun tasarımı kıyıyı başka yere isterse)
- **Kapsam dışı bırakılanlar** ve tetikleyicileri: suya girme, yüzdürme, dalga izi, gelgit, nehir/göl, sualtı kamerası, caustics, iWave. Spec §2 bunları açıkça kapsam dışı sayıyor; **hangi belirtide geri döneceği** yazılır (örn. oyuncu suya girebilir hâle gelirse sualtı render yolu ve iWave gündeme gelir).
- **Çoklu terrain desteklenmiyor** (tetikleyici: arazi bölünürse)

- [ ] **Adım 4: `SCALE.md` — ölçek bağımlılıkları**

Dağın boyu değişirse ne kayar:

- `SEA_FETCH` (8 km) — deniz alanının çapından türüyor, **kendiliğinden kaymaz**
- Deniz seviyesi — arazi yeniden üretilirse kıyı çizgisi değişir, `RefreshBathymetry()` çağrılmalı
- Mesh halka yarıçapları — **bilerek mutlak**, oyuncunun gözünden mesafe
- `_DeepWaterDepth` (200 m) — arazi dışı varsayımı, **bilerek mutlak**

- [ ] **Adım 5: `COOP.md` — co-op borcu**

Deniz **görsel katman** ve borç doğurmuyor: her istemci kendi kamerasına göre mesh'i snap'liyor, FFT deterministik (aynı `t`, aynı spektrum, aynı sonuç).

**AMA iki kural doğuyor:**

1. **Dalga alanı deterministik kalmalı.** `SeaSimulation` zamanı `Time.time`'dan alıyor; iki istemcinin saati farklıysa dalgalar farklı fazda olur. Paylaşılan bir saat gerekiyor — `COOP.md`'de zaten "Zaman yerel akıyor" borcu var, deniz onun tüketicisi oluyor.
2. **Rüzgâr paylaşılmalı.** Deniz spektrumu `U10`'dan türüyor; rüzgâr istemciler arası ayrışırsa deniz de ayrışır. `COOP.md`'de "Havayı tek oyuncunun yüksekliği sürüyor" borcu var; deniz onun ikinci tüketicisi.

İkisi de **yeni borç değil**, mevcut borçların kapsamını genişletiyor. `COOP.md`'ye o iki maddenin altına yazılır.

- [ ] **Adım 6: Commit**

```bash
cd /d "D:\ME\game\to the summit" && git add SYSTEMS.md RATIONALE.md DECISIONS.md SCALE.md COOP.md && git commit -m "Deniz: belgeler"
```

---

## Bitiş doğrulaması

Bütün fazlar bittikten sonra tek turda:

- [ ] `SeaConstantsTest` yeşil
- [ ] `SeaSpectrumTest` yeşil (`|mean(h)| < 1e-3`, Jacobian oranı %0–8)
- [ ] `Tools/Sea/Project Check` temiz
- [ ] Dört compute kernel de `HasKernel = true`
- [ ] `SeaLit.shader` hatasız, dört geçiş
- [ ] Yazma yasağı kod aramasında temiz
- [ ] Draw call 1
- [ ] Mesh'te delik/yırtık yok, halka sınırları temiz
- [ ] Kıyıda mesh araziyle kesişmiyor
- [ ] Dalgalar kıyıya yaklaşınca yükseliyor ve kırılıyor
- [ ] Köpük üç kaynaktan da geliyor, kenarı gürültülü
- [ ] Refraksiyon çalışıyor, kayalar suya sızmıyor
- [ ] Gece parıltı yok
- [ ] Islak kum bandı dalgalarla nefes alıyor
- [ ] compute < 1.2 ms, çizim < 1.5 ms, görünmezken < 0.05 ms
- [ ] **Mevcut sis, yağmur, kar, gece/gündüz, ışıklandırma ve bulutlar bozulmadan çalışıyor**
