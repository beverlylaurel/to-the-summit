# Plan: Kar Sistemi (mevcut projeye entegrasyon)

## Özet

`C:\Users\musta\Desktop\tts\specs\snow\unity-kar-sistemi-spec.md` (2571 satır, 24 bölüm,
14 faz) birebir uygulanacak. Sistem sıfırdan kuruluyor — projede kar yok, önceki iki
kar sistemi (`741e6b7`) tamamen söküldü. Spec'in çekirdek varsayımı ile projenin
bugünkü hâli birebir örtüşüyor: sis, yağmur, gece/gündüz ve rüzgâr var ve çalışıyor;
kar yok.

## Kullanıcı hikâyesi

Tırmanan oyuncu olarak, yürüdüğüm yerde iz bırakan, zamanla dolan, sıcaklıkla eriyen ve
rüzgârla savrulan bir kar istiyorum; mevcut hava ve ışık sistemleri bozulmadan.

## Sorun → Çözüm

Dağ çıplak kaya, kar sistemi yok → spec'in 14 fazı sırayla uygulanıp kar geri geliyor,
bu sefer mevcut sistemleri **okuyan**, onlara yazmayan bir mimariyle.

## Meta

- **Karmaşıklık**: XL — 60+ yeni dosya, 14 faz, ~6000 satır
- **Kaynak spec**: `C:\Users\musta\Desktop\tts\specs\snow\unity-kar-sistemi-spec.md`
- **Faz**: 0'dan başlıyor
- **Tahmini dosya**: 62 yeni, 0 mevcut dosya değişimi (onay alınanlar hariç)

---

## 0. Bu planın spec'e eklediği şey

Spec projeyi tanımıyor. Bu plan **yalnız spec'in bilemeyeceği şeyleri** ekliyor:
projenin gerçek API'leri, ölçülmüş ön koşullar, spec içi çelişkiler ve bütçe riski.
Spec'te yazan hiçbir sayı, isim veya teknik burada değiştirilmedi.

---

## 1. Ön koşullar — Faz 0 kontrolleri ŞİMDİ ölçüldü

Spec §1.2'nin tablosu. `SnowProjectCheck` yine yazılacak (kabul kriteri), ama
sonuçları şimdiden belli:

| Kontrol | Beklenen | Ölçülen | Sonuç |
|---|---|---|---|
| Color Space | Linear | `m_ActiveColorSpace: 1` | ✅ |
| URP aktif | evet | PC_RPAsset + PC_Renderer | ✅ |
| Depth Texture | açık | `m_RequireDepthTexture: 1` | ✅ |
| Compute shader desteği | var | çalışma zamanı kontrolü | Faz 0'da doğrula |
| VFX Graph paketi | kurulu | **`manifest.json`'da YOK** | ⚠️ Faz 8'e kadar sadece uyar |
| Boş layer slotu | ≥ 2 | 27 boş | ✅ |
| Environment Lighting | Skybox | PhysicallyBasedSky sürüyor | Faz 0'da raporla |
| Terrain | tek | 1 adet (`Mountain`, `MountainGenerator`) | ✅ `groundSource = UnityTerrain` çalışır |

**Mevcut kare bütçesi (F1 panelinden ölçüldü):** `Draw 525`, `SetPass 61`, `Tri 1248k`,
`8.2–8.7 ms` (115–122 FPS). Spec §13.1'in clipmap'i **+1.17 M üçgen ve +4 draw call**
getiriyor — üçgen sayısı ikiye katlanıyor. Spec bunu kendisi uyarıyor
("mevcut bütçenin üstüne binen ek yüktür"). **Faz 4'te ölçülüp kabul edilecek;
kabul edilmezse Low preset (0.4 M üçgen).** Bu bir karar noktasıdır, plan onu
kullanıcı yerine vermez.

**VRAM:** +48 MB (Medium). Low preset 15 MB.

---

## 2. Mevcut sistemlerin gerçek API'si — köprünün bağlanacağı yer

Spec §3.1 `ISnowEnvironmentSource`'u tanımlıyor, §3.2 köprüyü **kullanıcının
doldurması gereken TODO'larla** bırakıyor ("Sen bunu tahmin etmeye çalışma").
Plan tahmin etmiyor; **hazır eşleşmeyi** veriyor, doldurma kararı kullanıcının.

| Arayüz üyesi | Projedeki kaynak | Tam ifade | Not |
|---|---|---|---|
| `WindDirection` | `WindField` | `wind.Velocity.normalized` (y sıfırlanmış) | `WindField.Velocity` : `Vector3`, `public ... { get; private set; }` |
| `WindSpeed` | `WindField` | `wind.Velocity.magnitude` | m/s. `Strength` 0..1'dir, hız değil — karıştırma |
| `Sun` | sahnedeki directional light | Inspector'dan atanır | `TimeOfDay.Bind(directional, moonLight)` ile bağlı olan ışık |
| `SunElevation01` | `TimeOfDay` | `Mathf.Clamp01(time.SunHeight)` | `TimeOfDay.SunHeight` zaten `public float ... { get; private set; }` |
| `TemperatureC` | `TemperatureField` | `temperature.At(observer.position.y)` | İrtifaya bağlı; gözlemci Transform'u gerekiyor |
| `PrecipKind` | `WeatherState` | **türetilecek** — aşağıya bak | `WeatherState`'te artık `Snowiness` YOK |
| `PrecipIntensity01` | `WeatherState` | `weather.Precipitation` | 0..1 |
| `FogDensity01` | `AtmosphereController` | **normalize edilecek** — aşağıya bak | `Visibility` metre cinsinden |

### 2.1 `PrecipKind` — açık nokta

Projede yağışın "türü" diye bir kavram **yok**; `WeatherState` yalnız `Precipitation`
taşıyor (karlılık `741e6b7`'de silindi). Spec §3.4 zaten türü kar sisteminin kendi
histerezisinin belirlemesini istiyor:

```csharp
const float SNOW_ON_BELOW  = 0.5f;
const float SNOW_OFF_ABOVE = 2.0f;
```

Yani köprünün `PrecipKind` için makul karşılığı `Precipitation > 0 ? Rain : None`,
kar kararını `SnowfallController` histerezisi veriyor. **Bu bir bridge TODO'sudur;
spec §3.2 gereği kullanıcı dolduracak.** Plan onu kod olarak yazmıyor, hazır satırı
Faz 1 kabul listesine koyuyor.

### 2.2 `FogDensity01` — açık nokta

`AtmosphereController.Visibility` metre (görüş mesafesi). Spec 0..1 normalize istiyor.
Aynı şekilde bir bridge TODO'su. Hazır aday:
`Mathf.Clamp01(1f - Mathf.InverseLerp(minVis, maxVis, atmosphere.Visibility))`.
`minVis/maxVis` kullanıcının vereceği iki sayı.

### 2.3 `SnowRuntimeState` tüketicileri — hiçbiri bağlı değil

Spec §3.3 kar sisteminin **yalnız yayınladığını**, kimsenin bunu uygulamadığını
söylüyor. Projede bu değerleri okuyacak taraf henüz yok:

- `IsSnowing` → yağmuru susturması gereken `PrecipitationRenderer`. **Bağlamak
  kullanıcının işi** (spec §3.4 açıkça yazıyor). Faz 5 raporuna not düşülecek.
- `GroundCoverage01` → `_SnowCoverage` global, Faz 7'de nesne kaplaması okuyor.
- `Stormness01`, `LooseSnowFraction` → şimdilik tüketicisi yok.

---

## 3. Spec içi çelişkiler ve §0.2 gereği verilen çözümler

Spec "soru sorma, §20'ye bak, orada da yoksa en basit çözümü seç ve `// ASSUMPTION:`
yaz" diyor. Satır satır okumada üç çelişki bulundu:

| # | Çelişki | Nerede | Çözüm |
|---|---|---|---|
| 1 | `RT_WindShadow` formatı | §6.2 tablosu **RGHalf** (R=Wz, G=Wsz) ↔ §18.0 metni **RHalf** | **RGHalf.** `KWindShadow` kerneli hem `_WindShadowZOut` hem `_WindShadowSzOut` yazıyor; tek kanal yetmez. `// ASSUMPTION` düşülecek. |
| 2 | Spindrift spawn yüksekliği | §17.1 zaten `random(0, 0.05)` ↔ §18.7 "Faz 13'te `random(0, 0.05)` olarak **düzelt**" | Zaten doğru; Faz 13'te düzeltilecek bir şey yok. Faz 13 notuna yazılacak. |
| 3 | Klasör kuralı | Spec §1.5 `Assets/Snow/...` ↔ proje `CLAUDE.md` "`Assets/Scripts/<Sistem>/`" | **Spec kazanır** (kullanıcı "spec bağlayıcıdır" dedi). `CLAUDE.md`'nin klasör kuralına bu istisna aynı adımda yazılacak. |

Ek olarak spec'in **beklediği ama projede olmayan** bir şey:

| Ne | Spec | Proje | Sonuç |
|---|---|---|---|
| Mevcut ayak sesi sistemi | §19.1 "mevcut sisteme yeni yüzey tipi olarak eklenir" | **Ayak sesi sistemi yok** | `SnowFootstepAudio.cs` projedeki ilk ayak sesi sistemi olacak. Faz 9'da not düşülecek. |

---

## 4. Zorunlu okuma

| Öncelik | Dosya | Bölüm | Neden |
|---|---|---|---|
| P0 | spec | §0, §1, §3, §20 | Protokol, dokunma yasakları, entegrasyon katmanı, karar tablosu |
| P0 | spec | §6, §8, §9, §10, §11 | Veri modeli, z-fighting, yakalama, iz, birikme |
| P0 | `Assets/Scripts/Weather/WindField.cs` | 44–56, 103 | `Velocity`, `Strength`, `Gust` — köprünün rüzgâr ucu |
| P0 | `Assets/Scripts/Weather/TemperatureField.cs` | 67–88 | `At()`, `FreezingLevel` — köprünün sıcaklık ucu |
| P0 | `Assets/Scripts/Environment/TimeOfDay.cs` | 127–148 | `SunHeight`, `SunDirection`, `DayFactor` |
| P0 | `Assets/Scripts/Weather/WeatherState.cs` | tamamı | 26 satır; `Precipitation` tek değer |
| P1 | `Assets/Editor/MountainSceneBootstrap.cs` | `EnsureTerrainSurface` | Sahne kurulumunun deseni — kar kurulumu buna benzeyecek |
| P1 | `Assets/Scripts/Terrain/TerrainSurface.cs` | 1–120 | Shader ID cache'i, `Bind()` deseni, materyal sahipliği |
| P1 | `Assets/Scripts/Fog/VolumetricFogFeature.cs` | tamamı | Tek `ScriptableRendererFeature` + compute dispatch deseni |
| P2 | `Assets/Shaders/MountainSurface.shader` | 1–110 | URP pass yapısı, `include-rev` hilesi |
| P2 | `SYSTEMS.md`, `DECISIONS.md`, `SYMPTOMS.md` | tamamı | Belge güncelleme yükümlülüğü |

---

## 5. Uyulacak proje desenleri

Aşağıdakiler **projeden alınmış gerçek kod**. Kar dosyaları bunlara benzeyecek.

### DOSYA BAŞLIĞI
```csharp
// SOURCE: Assets/Scripts/Terrain/TerrainSurface.cs:1-4
// ROL: dağ yüzeyinin materyalini kurar ve ayarları shader'a yazar.
// Çağıran: MountainSceneBootstrap (kurulum), kendi Update'i (hava değerleri).
```
Spec §0.11 zaten `// ROL:` istiyor; proje aynı deseni kullanıyor, çağıranı da yazıyor.

### SHADER ID CACHE
```csharp
// SOURCE: Assets/Scripts/Terrain/TerrainSurface.cs:34-40
static readonly int SurfaceMapsId = Shader.PropertyToID("_SurfaceMaps");
static readonly int GroundNormalsId = Shader.PropertyToID("_GroundNormals");
```
Spec §0.8 `SnowShaderIDs.cs` istiyor — proje deseni birebir aynı, tek fark
ID'lerin ayrı dosyada toplanması.

### BAĞIMLILIK ENJEKSİYONU — `Bind()`
```csharp
// SOURCE: Assets/Scripts/Terrain/TerrainSurface.cs
public void Bind(TerrainMaterialSettings source, WeatherState weatherState, WindField windField,
    TimeOfDay timeOfDay, AltitudeWeatherDriver driver, TemperatureField thermometer,
    Texture2D maps, Texture2D windMap, ...)
```
`FindObjectOfType` yok, singleton yok. Editör bootstrap'i `SerializedObject` ile atıyor.

### HATA YUTMA YOK
```csharp
// SOURCE: Assets/Scripts/Terrain/TerrainSurface.cs
if (settings == null)
    throw new InvalidOperationException($"{nameof(TerrainSurface)}: {nameof(settings)} atanmadı.");
```
Boş `catch` yok, sessiz fallback yok. Spec §3.2 "`SnowManager` kaynak bulamazsa
hata basıp devre dışı kalır" ile aynı.

### RENDERER FEATURE + COMPUTE DISPATCH
```csharp
// SOURCE: Assets/Scripts/Fog/VolumetricFogFeature.cs
public class VolumetricFogFeature : ScriptableRendererFeature
{
    public override void Create() { pass = new Pass { renderPassEvent = ... }; }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        => renderer.EnqueuePass(pass);
}
```
Spec §15.2 "tek `ScriptableRenderPass`, `BeforeRenderingOpaques`" ile aynı.

### EDİTÖR KURULUM DÜĞMESİ
```csharp
// SOURCE: Assets/Editor/MountainSceneBootstrap.cs
var serialized = new SerializedObject(component);
serialized.FindProperty("settings").objectReferenceValue = asset;
serialized.ApplyModifiedProperties();
EditorUtility.SetDirty(component);
```
Kullanıcı elle bileşen bağlamıyor; kurulum kodda. `CLAUDE.md`: "Claude bir şeyi
otomatikleştirebiliyorsa otomatikleştirir."

### TEST YOK — DERLEME + OYUNCU DOĞRULAMASI
Projede test paketi yok, tek `try` bloğu yok. Skill'in "unit test yaz" adımı bu
projede **derleme kontrolü + kullanıcının Unity'de doğrulaması**na dönüşüyor.
Kar tarafındaki tek istisna spec'in istediği `Editor/SnowConstantsTest.cs`
(C# ↔ HLSL sabit eşliği) ve Faz 12'nin kütle testi.

### DERLEME TETİKLEME
```bash
cd /d "D:\ME\game\to the summit" && date > Logs/refresh.trigger
```
Unity odaksız derliyor; hata `Logs/Editor.log`'a düşüyor.

---

## 6. Üretilecek dosyalar (spec §21'den, birebir)

**Faz 0** — `Editor/SnowProjectCheck.cs`, `Runtime/SnowConstants.cs`,
`Runtime/SnowShaderIDs.cs`, `Runtime/SnowSettings.cs`, `Runtime/SnowQualityPreset.cs`,
`Shaders/SnowConstants.hlsl`, `Shaders/SnowCommon.hlsl`, `Editor/SnowConstantsTest.cs`

**Faz 1** — `Runtime/ISnowEnvironmentSource.cs`, `Runtime/SnowEnvironmentBridge.cs`,
`Runtime/SnowRuntimeState.cs`, `Runtime/SnowManager.cs`, `Runtime/SnowRenderPass.cs`,
`Runtime/SnowRendererFeature.cs`, `Runtime/SnowGroundHeight.cs`,
`Shaders/SnowSim.compute` (KClear, KScroll), `Editor/SnowDebugWindow.cs`,
`Shaders/Hidden_SnowDebug.shader`

**Faz 2** — `Runtime/SnowCaptureCamera.cs`, `Runtime/SnowDeformer.cs`,
`Runtime/SnowDeformerRegistry.cs`, `Shaders/Hidden_SnowCaptureDepth.shader`,
`SnowSim.compute` (+KBlurCapture)

**Faz 3** — `SnowSim.compute` (+KDeform, KRimBlurH, KRimBlurV, KRim)

**Faz 4** — `Runtime/SnowClipmap.cs`, `Runtime/SnowMeshBuilder.cs`,
`Shaders/SnowLit.shader`, `Shaders/SnowLitInput.hlsl`, `Shaders/SnowLitForwardPass.hlsl`

**Faz 5** — `Runtime/SnowSkyCamera.cs`, `Runtime/SnowfallController.cs`,
`Shaders/Hidden_SnowSkyDepth.shader`, `SnowSim.compute` (+KAccumulate)

**Faz 6** — `Shaders/SnowLighting.hlsl`, `Shaders/SnowSparkle.hlsl`,
`Shaders/SnowDetailNormals.hlsl`, `SnowLit.shader` (tamamlanır)

**Faz 7** — `Shaders/SnowCover.hlsl`, `Shaders/SnowCoverObject.shader`,
`Runtime/SnowCoverageDriver.cs`, `Runtime/SnowCharacterAccumulator.cs`

**Faz 8** — `VFX/VFX_Snowfall.vfx`, `Textures/T_Flake_Atlas.png`

**Faz 9** — `Runtime/SnowSampler.cs`, `Runtime/SnowFootstepAudio.cs`,
`Runtime/SnowMovementModifier.cs`, `Runtime/SnowProfiler.cs`, `VFX/VFX_SnowPuff.vfx`

**Faz 10** — `Runtime/SnowPersistence.cs`, `Runtime/SnowFarCascade.cs`

**Faz 11** — `SnowLighting.hlsl` (+SnowHeightAO), `SnowSim.compute` (KAccumulate/KDeform
genişler), `SnowLit.shader` (kabuk shading)

**Faz 12** — `Runtime/SnowHeatSource.cs`, `Runtime/SnowHeatRegistry.cs`,
`SnowSim.compute` (+KWindShadow, KWindTransport), `SnowCommon.hlsl`
(+SampleWindShadow, WyvillFalloff), `SnowLit.shader` (sastrugi), `Textures/T_Sastrugi_Noise.png`

**Faz 13** — `VFX/VFX_SnowSpray.vfx`, `VFX/VFX_SnowCurtain.vfx`,
`Runtime/SnowSprayController.cs`, `Runtime/SnowCurtainController.cs`

Hepsi `Assets/Snow/` altında. Bu ağacın dışına dosya yazılmayacak (§1.5).

**Ek (plan kararı, spec'te yok):** kurulumu otomatikleştiren bir editör düğmesi
gerekecek — projenin kuralı bu (`CLAUDE.md`: elle sahne düzenleme yok). Spec'in
dosya listesine ek dosya eklemek §0.9'a aykırı olduğu için bu iş
`Editor/SnowDebugWindow.cs` içine bir "Sahneyi kur" düğmesi olarak konacak;
o dosya zaten Faz 1 listesinde var.

---

## 7. YAPILMAYACAKLAR

- Mevcut proje ayarlarını değiştirmek (Color Space, URP asset, Volume profilleri,
  Directional Light, culling mask, fizik matrisi) — §1.1
- Sis, güneş, ambient, `RenderSettings`, `VolumeProfile`, `Light.intensity` yazmak — §3.3
- Yağmur VFX'ini kapatmak — §3.4 (yalnız `IsSnowing` yayınlanır)
- Kar sisteminin kendi rüzgârını / gündöngüsünü / sıcaklığını üretmesi — §3.6
- Shader Graph — §0.7
- Tessellation — §20 (geometry clipmap)
- Kapsül/stamp/basınç tabanlı deformasyon — §20 (alttan yakalama)
- Kütle transportu / atomik ile rim — §20 (`blur − carve`)
- Çığ, kartopu, buz gölü, sarkıt, multiplayer senkronu — §20
- Çoklu terrain — §7.1 (hata bas, dur)
- Çözünürlüğü 2048'e çıkarmak — §20
- Onaysız prefab / shader / script / layer değişikliği — §1.4

---

## 8. Onay kapıları (§1.4) — her biri ayrı ayrı sorulacak

| # | Ne | Faz | Etki |
|---|---|---|---|
| A1 | Ana kameranın Culling Mask'inden `SnowDeformer` çıkarılması | 0 (rapor) / 2 (gerek) | **Kullanıcı elle yapar**, kod yapmaz (§1.3) |
| A2 | Karakter prefab'ına ayak/bacak proxy mesh'leri | 2 | Prefab değişir |
| A3 | Çatı/köprü/kaya nesnelerinin layer'ı → `SnowOccluder` | 5 | Sahne değişir |
| A4 | Karakter shader'ına `_SnowAccum` + `_SnowLineY` | 7 | Shader değişir |
| A5 | Hangi nesnelerin `SnowCoverObject.shader`'a geçeceği | 7 | Materyal değişir — toplu değişim YOK, raporla |
| A6 | `FirstPersonController.SpeedMultiplier`'a hız cezası bağlanması | 9 | Kod değişir (property zaten var) |
| A7 | Ateş/meşale prefab'larına `SnowHeatSource` | 12 | Prefab değişir |
| A8 | `SnowSprayController`'ın hız kaynağı seçimi | 13 | Bileşen ayarı |

---

## 9. Faz faz görevler

Her fazın sonunda **dur**, kabul kriterlerini listele, kullanıcının Unity'de test
etmesini bekle. Her kabul listesine spec'in zorunlu kıldığı regresyon satırı dahil:
*mevcut sis, yağmur, gece/gündüz ve rüzgâr sistemleri bozulmadan çalışıyor.*

### Faz 0 — Proje kontrolü ve sabitler

- **ACTION**: 8 dosya (§21 Faz 0 listesi). Hiçbir proje ayarına dokunma.
- **IMPLEMENT**: `SnowProjectCheck` menü komutu `To The Summit/Kar/Proje Kontrolü`; §1.2
  tablosundaki 8 kontrolü çalıştırıp rapor yazar, **hiçbirini düzeltmez**.
  Rapora şu iki satır zorunlu: (a) "Ana kameranın Culling Mask'inden `SnowDeformer`
  layer'ını elle kaldırın." (b) "Yağmur sisteminiz `SnowRuntimeState.IsSnowing`
  true iken yağmuru kapatmalı."
  `SnowConstants.cs` ve `SnowConstants.hlsl` birebir aynı değerleri taşır;
  `SnowConstantsTest` bunu doğrular.
- **MIRROR**: dosya başlığı `// ROL:` deseni; ayarlar `ScriptableObject`;
  `Shader.PropertyToID` cache'i `SnowShaderIDs.cs`'te.
- **GOTCHA**: VFX Graph kurulu değil — Faz 0 **uyarı** basacak, kurulum yapmayacak.
  Layer'ları `SnowProjectCheck` **açmayacak**; boş slot olduğunu raporlayacak
  (spec §1.3 "boş slot yoksa dur" diyor, açma yetkisi vermiyor).
- **VALIDATE**: derleme temiz; menü komutu rapor üretiyor; `SnowConstantsTest` yeşil;
  hiçbir proje ayarı değişmemiş (`git diff ProjectSettings/` boş).

### Faz 1 — Entegrasyon katmanı ve durum dokuları

- **ACTION**: 10 dosya (§21 Faz 1). §3'ün tamamı burada doğuyor.
- **IMPLEMENT**: `ISnowEnvironmentSource` spec §3.1'deki imzayla birebir.
  `SnowEnvironmentBridge` §3.2'deki gibi — manuel değerler + TODO'lar.
  §2'deki eşleşme tablosu TODO satırlarının yanına yorum olarak konur ki kullanıcı
  tek satırda doldursun. `SnowRuntimeState` §3.3'teki 5 property.
  RT'ler §6.2 tablosundaki formatlarla; `RT_Trail` **ARGBHalf** (B/A Faz 11-12'de
  kullanılacak, şimdi 0 kalır). `RT_WindShadow` **RGHalf** (§3 çelişki #1).
  Bölge snap'i §6.4. `KClear`, `KScroll`. Zemin bake §7.1.
  `SnowDebugWindow`'a "Sahneyi kur" düğmesi (§6 ek).
- **MIRROR**: `Bind()` enjeksiyonu; `throw new InvalidOperationException` deseni;
  `VolumetricFogFeature`'ın renderer feature yapısı; bootstrap'in `SerializedObject`
  ile bağlama deseni.
- **IMPORTS**: `UnityEngine.Rendering`, `UnityEngine.Rendering.Universal`,
  `UnityEngine.Rendering.RenderGraphModule`
- **GOTCHA**:
  - Snap yapılmazsa izler teksel altı kayar — spec'in "en zor teşhis edilen hata"sı.
  - `RT_Trail`'i RGHalf açma; sonradan format değiştirmek bütün kernel'leri kırar (§6.2).
  - `SnowManager` kaynak bulamazsa **hata basıp devre dışı kalır**, varsayılan uydurmaz.
  - Kar sisteminde `RenderSettings` / `VolumeProfile` / `Light.intensity` yazan
    tek satır olmayacak — kabul kriteri kod aramasıyla doğrulanıyor.
- **VALIDATE**: `grep -rn "RenderSettings\.\|VolumeProfile\|\.intensity =" Assets/Snow`
  boş dönmeli. Play'de RT'ler yaratılıyor/serbest bırakılıyor, GC 0 B,
  debug içeriği oyuncu yürürken **dünyaya sabit**, terrain silueti görünüyor.

### Faz 2 — Yakalama

- **ACTION**: 5 dosya. **A2 onayı alınmadan proxy mesh eklenmez.**
- **IMPLEMENT**: `SnowCaptureCamera` §9.1'deki tam ayarlarla — **kamera kar
  hacminin ALTINDA ve YUKARI bakar** (`Euler(-90,0,0)`), `CaptureBelow = 3.0`,
  `CaptureAbove = 3.0`, `backgroundColor = (-9999,0,0,0)`, `cullingMask` yalnız
  `SnowDeformer`. Replacement shader §9.2, `Cull Off` **zorunlu**.
  `KBlurCapture` §9.4'teki 4-tap Poisson, `_BlurRadiusTexels = 1.5`.
- **GOTCHA**:
  - `Cull Off` yoksa yakalama **boş** çıkar (alt yüzey back-face'tir).
  - Kamera aşağı bakarsa yakalama ters olur.
  - Deformer yokken `Render()` çağrılmaz (§15.2).
  - Karakter mesh'ini `SnowDeformer` layer'ına taşıma — ana kamerada görünmez olur.
- **VALIDATE**: `RT_Capture`'da ayaklar görünüyor; zıplayınca kayboluyor; blur
  sonrası yumuşak; proxy mesh'ler ana kamerada görünmüyor (A1 yapılmışsa).

### Faz 3 — İz oluşumu

- **ACTION**: `SnowSim.compute` içine 4 kernel.
- **IMPLEMENT**: `KDeform` §10.1'deki kod ve dört sabit birebir.
  `KRimBlurH`/`KRimBlurV` separable blur, `_RimBlurTexels = 7`.
  `KRim` §10.2 — **`blur(carve) − carve`**, `_RimVelocityBias = 0.04 s`,
  `SNOW_RIM_STRENGTH = 1.8`, `SNOW_RIM_MAX = 0.10`, `SNOW_RIM_REF_DEPTH = 0.25`.
  İzlerin dolması §10.3.
- **GOTCHA**: `carve − blur` yazılırsa rim izin **içinde** oluşur. `depthScale`
  atlanırsa sırt derinlikten bağımsız kalır. `RT_Trail`'in B/A kanalları korunmalı.
- **VALIDATE**: `RT_Trail.G` izin **etrafında halka**; rim hareket yönünde asimetrik;
  `_FallbackSWE` yarıya inince rim inceliyor; 5–6 geçişte carve azalıyor ve
  `RT_Snow.G` yükseliyor → patika.

### Faz 4 — Zemin mesh'i ve arazi entegrasyonu

- **ACTION**: 5 dosya. **Bütçe kapısı burada.**
- **IMPLEMENT**: Clipmap §13.1'deki 4 halka. Vertex §13.2, normal §13.3 (fragment'ta,
  merkezi fark). Z-fighting §8.1 `clip(h - 0.004)`, kenar §8.2 gürültülü kırılma,
  **`Queue = Geometry + 50`** (§8.3).
- **GOTCHA**: halka snap'i **kendi quad boyutunun 2 katına**; `mesh.bounds` elle
  geniş (`600` m dikey); iç delik **gerçek**; kamera mesafesine göre displacement
  kısma yok.
- **VALIDATE**: 4 draw call (Frame Debugger); halkalar arası çatlak yok; yürürken
  dalgalanma yok; karın inceldiği yerde z-fighting yok; kenar gürültülü bitiyor;
  `GroundCoverage01 = 0` iken maliyet sıfır.
- **KAPI**: F1 panelinde `Tri` ve `ms` ölç. Öncesi 1248k / 8.2 ms. Sonrası kabul
  edilebilir değilse Low preset'e geç — **kullanıcı kararı**.

### Faz 5 — Gökyüzü haritası, birikme, hava entegrasyonu

- **ACTION**: 4 dosya. **A3 onayı** (çatı/köprü → `SnowOccluder`).
- **IMPLEMENT**: `SnowSkyCamera` §12.1 (aşağı bakar, `SkyAreaSize = 96`, dirty-flag'li).
  `KAccumulate` §11'deki kodun tamamı — yağış, rüzgâr yeniden dağıtımı, oturma,
  derece-gün erime, `_RainOnSnow01` çarpanı, ıslaklık, tazelik sönümü.
  `SnowfallController` §3.4 histerezisi ve §17.2 yoğunluk eşlemesi.
- **GOTCHA**: karakterler `SnowOccluder`'a **konmaz** (yoksa ayağının altına kar
  yağmaz). VFX yoğunluğu ve `_SnowfallSWERate` **aynı `i01`**'den türemeli.
  SkyVis her frame render edilmez.
- **VALIDATE**: şiddet 0.60'ta ~2.8 cm/saat (`Time.timeScale` ile hızlandır);
  çatı altında birikmiyor; sıcaklık > 0.5 °C'de kar duruyor; +5 °C'de eriyor;
  yağmurda daha hızlı eriyip ıslanıyor; rüzgâr yönü birikme dağılımını değiştiriyor.

### Faz 6 — Kar shading

- **ACTION**: 4 dosya.
- **IMPLEMENT**: §14.1 yüzey parametreleri, §14.2 **RNM** harmanlama ve 4 katman,
  §14.3 wrap diffuse + geçirgenlik + `sunGate`, §14.4 Bowles-Wang parıltısı.
  Sis **URP `MixFog`** ile (§14 başı).
- **GOTCHA**: normal'ler lerp/overlay ile harmanlanmaz. Micro katman 16 m'de
  kapanmazsa TAA kaynar. Karı gece aydınlatmak için ambient'e dokunma.
- **VALIDATE**: gündüz parıldıyor gece parıldamıyor; parıltı titremiyor; mesafeyle
  yoğunluk sabit; patika/taze kar farkı görünür; **mevcut sis kar üstünde doğru**.

### Faz 7 — Nesne ve karakter üstü kar

- **ACTION**: 4 dosya. **A4 ve A5 onayları.**
- **IMPLEMENT**: §16 `SnowCoverMask`, §16.1 `SnowCharacterAccumulator`
  (yağmurda hızlı temizlenme dahil).
- **GOTCHA**: mevcut nesne shader'ları değiştirilmez; `SnowCoverObject.shader`
  yeni bir shader'dır, kimin kullanacağı kullanıcının kararı — **toplu materyal
  değişimi yok, raporla**.
- **VALIDATE**: kayaların üstü karlı altı değil; kenarlar gürültülü; karakter
  açıkta karlanıp koşunca/içeri girince/yağmurda temizleniyor.

### Faz 8 — Kar yağışı

- **ACTION**: 2 dosya. **VFX Graph paketi burada zorunlu** — kurulum kullanıcı onayı ister.
- **IMPLEMENT**: §17.1 Sistem A (kar taneleri) ve Sistem B (spindrift, `y = groundY +
  random(0, 0.05)`), §17.2 yoğunluk eşlemesi.
- **GOTCHA**: örtü kesme bloğu **atlanmayacak**; asgari ekran boyutu zorunlu;
  sis fade'i uygulanmazsa siste beyaz noktalar kalır; spawn kutusu 1 m ızgarasına snap'li.
- **VALIDATE**: taneler mevcut rüzgâr yönünde sürükleniyor; çatı altına yağmıyor;
  sis yoğunlaştıkça fade oluyor; rüzgâr > 7 m/s'te savrulma başlıyor;
  yağış şiddeti ile birikme tutarlı.

### Faz 9 — Oyun tarafı ve profiler

- **ACTION**: 5 dosya. **A6 onayı.**
- **IMPLEMENT**: `SnowSampler` §19 (`AsyncGPUReadback`, 4 karede bir, 64×64),
  ayak sesi seçimi §19.1, hız cezası §19.2 (property yayınlar, bağlamak onayla),
  toz bulutu §19.3, `SnowProfiler`.
- **GOTCHA**: projede ayak sesi sistemi yok — bu ilk olacak (§3 tablosu).
  `SnowMovementModifier` controller'a **kendisi yazmaz**.
- **VALIDATE**: derin karda yavaşlıyor; compute toplamı < 1.5 ms;
  **karsız sahnede toplam < 0.05 ms**.

### Faz 10 — Kalıcılık ve uzak kaskad

- **ACTION**: 2 dosya. §21 Faz 10'daki tanımlar.
- **VALIDATE**: bölgeden çıkıp dönünce izler duruyor; halka 2–3 kaskaddan okuyor.

### Faz 11 — İz içi AO ve kabuk

- **ACTION**: mevcut kernel/shader genişler; yeni doku/kamera/VFX yok.
- **IMPLEMENT**: §18.5 `SnowHeightAO` (`cos²(φ)` ortalaması, **yalnız ambient**),
  §18.3 kabuk (üçgen sıcaklık profili, rüzgâr levhası, `RT_Trail.B`, kırılma).
- **GOTCHA**: AO doğrudan ışığa uygulanırsa izler siyah leke olur.
  Kabuk `RT_Trail.B`, patika `RT_Snow.G` — **ayrı tut**.
  Kabuk `T < 0` koşuluyla değil üçgen profille oluşur.
- **VALIDATE**: `_SnowAOStrength = 0` görüntüyü AO öncesine döndürüyor;
  −5 °C'de kabuk oluşuyor, −20 °C'de oluşmuyor, +5 °C'de eriyor;
  koşarak geçince kırılıyor ve derine batılıyor.

### Faz 12 — Rüzgâr taşınımı, engel yığılması, sastrugi, ısı kaynakları

- **ACTION**: 6 dosya/genişletme. **A7 onayı.**
- **IMPLEMENT**: §18.0 `KWindShadow` (Gauss-Seidel, dama tahtası, 24 iterasyon,
  `RT_WindShadow` **RGHalf**), §18.1 `KWindTransport` (**haç döşemesi, 5 dispatch,
  atomik YOK**), §18.4 sastrugi (aynı kernel, `RT_Trail.A` + shader detayı),
  §18.2 `SnowHeatSource`/`SnowHeatRegistry` (**16 elemanlı uniform dizi**,
  Wyvill düşüşü, sıcaklık alanları **toplanır**).
- **GOTCHA**: `Wz > A` iken aşınma **kapanmalı** (ters yazılırsa yığılma rüzgâr
  üstünde oluşur). Sastrugi UV'leri ters yazılırsa desen 90° yanlış olur.
  `_SastrugiWindDir` 120 s yumuşatılmalı. `SnowSurfaceAt`'e sastrugi terimi
  **eklenmezse** sırtlar ışığa tepki vermez — spec "en sık atlanan adım" diyor.
- **VALIDATE**: **kütle testi** — `KWindTransport` sırasında `Σ swe` değişmemeli
  (tolerans %1). Haç döşemesi doğruysa geçer, atomik/naif scatter'da geçmez.
  Duvarın arkasında yığılma, önünde aşınma; 4 m/s'de taşınım duruyor;
  iki ısı kaynağı üst üste binince **toplanıyor**.

### Faz 13 — Püskürtme ve süspansiyon perdeleri

- **ACTION**: 4 dosya. **A8 onayı.**
- **IMPLEMENT**: §18.6 püskürtme (**V̇ = genişlik × batma × hız**, sabit rate yok),
  §18.7 perdeler (capacity **14**, üstel yükseklik dağılımı, üstel alpha,
  zemin takibi, sis ile alpha azaltma).
- **GOTCHA**: §17.1 Sistem B'nin spawn yüksekliği **zaten** `random(0, 0.05)` —
  düzeltilecek bir şey yok (§3 çelişki #2). Perde maliyeti **fill-rate**;
  capacity artırma.
- **VALIDATE**: püskürtme hıza ve derinliğe görünür şekilde bağlı; perdeler
  yükseldikçe soluklaşıyor, arazinin içine girmiyor, 5 m'yi aşmıyor;
  Frame Debugger'da overdraw ölçüldü.

---

## 10. Doğrulama komutları

Projede test paketi yok. Doğrulama üç ayaklı:

### Derleme
```bash
cd /d "D:\ME\game\to the summit" && date > Logs/refresh.trigger
```
BEKLENEN: `Logs/Editor.log`'da `error CS` ve `Shader error` yok.

### Yasak yazma taraması (Faz 1 kabul kriteri)
```bash
cd /d "D:\ME\game\to the summit" && grep -rn "RenderSettings\.\|VolumeProfile\|\.intensity *=" Assets/Snow --include=*.cs
```
BEKLENEN: boş.

### Allocation taraması
```bash
cd /d "D:\ME\game\to the summit" && grep -rn "GetComponent<\|FindObjectOf\|FindAnyObjectBy\|SetFloat(\"" Assets/Snow/Runtime --include=*.cs
```
BEKLENEN: yalnız `Awake`/`OnEnable`/editör yollarında.

### Oyuncu doğrulaması
Her fazın kabul listesi. `Logs/play.log` Play sonrası okunur.

---

## 11. Riskler

| Risk | Olasılık | Etki | Azaltma |
|---|---|---|---|
| Clipmap üçgen bütçesi kabul edilemez (+1.17 M, mevcut 1.25 M) | **Yüksek** | Faz 4 durur | Faz 4 kapısında ölç; Low preset 0.4 M |
| VFX Graph paketi kurulu değil | Kesin | Faz 8 başlayamaz | Faz 0 uyarır; kurulum Faz 8'de onayla |
| Köprü TODO'ları doldurulmazsa sistem manuel değerlerle kalır | Orta | Kar hava sistemine tepki vermez | Spec'in kabul ettiği durum; §2 tablosu hazır |
| `PrecipKind` ve `FogDensity01` projede doğrudan karşılığı yok | Kesin | Köprüde iki TODO | §2.1, §2.2'de hazır ifadeler |
| Yağmur ve kar aynı anda yağar | Orta | Görsel çelişki | `IsSnowing` yayınlanır, bağlamak kullanıcının (§3.4) |
| `clip()` erken-Z'yi kapatıyor, overdraw artıyor | Orta | Kare süresi | Spec'in bilinçli takası (§8.1); Faz 4'te ölç |
| Faz 12 kütle testi geçmez (haç döşemesi yanlış) | Orta | Kar çoğalır/kaybolur | Kabul kriteri; atomik kullanma |
| VRAM +48 MB | Düşük | PC hedefi | Low preset 15 MB |
| Klasör kuralı `CLAUDE.md` ile çelişiyor | Kesin | Belge tutarsızlığı | Spec kazanır; `CLAUDE.md`'ye istisna aynı adımda yazılır |

---

## 12. Belge yükümlülükleri (proje kuralı, spec'te yok)

Her fazda **aynı adımda**:

- `SYSTEMS.md` — yeni bağ kurulduğunda: ne neyi okur, ne okumaz. Kar sisteminin
  "okur ama yazmaz" kuralı buraya açıkça girer.
- `DECISIONS.md` — ertelenen/sınırlanan her karar: tetikleyici + maliyet.
  Faz 4 bütçe kararı, VFX Graph kurulumu, köprü TODO'ları buraya.
- `RATIONALE.md` — bir kuralın **neden** öyle olduğu (ölçüm, denenip başarısız yol).
- `SYMPTOMS.md` — ölçümle kapanan her belirti.
- `SCALE.md` — dağın boyuna bağlı her yeni sayı.
- `COOP.md` — ağ katmanı gelince yeniden yazılacak her şey (deformasyon
  istemci-yerel, §20).
- `CLAUDE.md` — klasör kuralına `Assets/Snow/` istisnası (§3 çelişki #3).

---

## 13. Kabul

- [ ] 14 fazın hepsi sırayla, her birinin kabul kriterleri kullanıcı tarafından onaylanmış
- [ ] Spec'teki dosya/sınıf/kernel/sabit isimleri birebir kullanılmış
- [ ] `[KAYNAK]` etiketli hiçbir teknik değiştirilmemiş
- [ ] Sayısal değerler değiştirilmemiş
- [ ] `Assets/Snow/` dışına dosya yazılmamış
- [ ] `git diff ProjectSettings/` boş
- [ ] Onaysız hiçbir mevcut dosya değişmemiş
- [ ] Yasak yazma taraması boş
- [ ] Her fazda regresyon: sis, yağmur, gece/gündüz, rüzgâr çalışıyor
- [ ] Belgeler her fazda aynı adımda güncellenmiş

## Notlar

Spec 2571 satır, satır satır okundu. Bulunan üç çelişki §3'te, projede karşılığı
olmayan iki nokta §2.1/§2.2'de, spec'in beklediği ama olmayan bir sistem
(ayak sesi) §3 tablosunda. Bunların hiçbiri plan tarafından karara bağlanmadı;
spec'in kendi protokolüne (§0.2, §20) veya kullanıcı onayına havale edildi.

Ölçülen ön koşulların yedisi geçiyor, biri (VFX Graph) Faz 8'e kadar bloklamıyor.
Tek gerçek risk clipmap'in üçgen bütçesi; spec bunu kendisi uyarıyor ve Low preset
çıkışını veriyor.
