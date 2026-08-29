# FPS Analizi — Koda Karşı Denetlenmiş Sürüm

Tarih: 2026-08-29 · İlk sürüm dört paralel analiz ajanıyla yazıldı, **bu sürüm her maddesi
koda karşı tek tek doğrulanarak düzeltildi.** Denetimde `[KESİN]` listesinin sekiz
maddesinden ikisi çürüdü, ikisi yanlış etiketliydi.

**Başlık kuralı:**

- **[KESİN]** — matematiksel olarak bit-identical: atlanan iş zaten 0 ya da özdeş değer
  üretiyor, kanıtı satır numarasıyla. Test yine yapılır ama piksel farkı olmaması yapısal
  olarak garanti.
- **[TEST ŞARTLI]** — beklenen fark sıfır ya da algı altı, piksel garantisi yok. A/B karesi
  olmadan uygulanmaz.

**Denetimin kendi dersi:** ilk sürümde `[KESİN]` etiketiyle gelen maddelerden **dördü**
yanlış çıktı:

- **S1** aritmetik hatası — "terim daima 0" değil, kumsalda çalışıyor
- **S3** hiç çağrılmayan bir fonksiyon — kazanç sıfır
- **A3** yalnız runtime'a bakmış, editör aracının okumasını atlamış
- **S7** yalnız `Assets/` altında aramış — dosya `Packages/` altından iki kez include ediliyor,
  silinseydi gökyüzü bozulurdu
- **A2** shader'ın instanced terrain yolunu desteklediğini varsaymış — desteklemiyor, açılınca
  arazi tamamen kayboldu (Play'de görüldü, geri alındı)

Bir altıncısının (**C1**) öncülü doğruydu ama kazanç tahmini 8-30 kat şişikti.

Ortak desen: **kapsam dar tutulmuş.** S1 aritmetiği, S3 anahtar durumunu, A3 editör
tarafını, S7 `Packages/` klasörünü, A2 shader'ın yeteneğini kontrol etmemiş. Etiket,
doğrulamanın yerine geçmez — ve `[KESİN]` etiketi en tehlikelisi, çünkü test atlatıyor.

---

## 0. Baz durum

| Metrik | Değer | Kaynak |
|---|---|---|
| Frame time | **7,4–7,9 ms = 127–135 FPS** | 2026-08-29 oturumu, ekran HUD'ı |
| Draw / SetPass / Tri | 166–168 / 60–62 / 988k | aynı oturum, 60 km görüşle |
| GPU | RTX 4060 8 GB, D3D12, Editor | `Logs/Editor.log` |
| Kalite tier | Kar MEDIUM, Deniz MEDIUM | `SnowSettings.asset`, `SeaSettings.asset` |
| FrameRateCap | vSync 0, hedef 244 FPS | `FrameRateCap.cs:10-11` |

İlk sürüm 8,2–8,7 ms diyordu ve kaynağı `DECISIONS.md:1711` idi — kendi ölçümü değil,
alıntı. Ölçüm o tarihten sonra değişti.

**Ölçüm altyapısı:** CPU tarafı hazır (`PerformanceSampler`: FPS, %1 low, GC MB/s; karda 5
GPU marker). Eksik: `ProfilerCaptures/` boş, diske frame-log yok, kar dışı sistemlerde
(bulut/deniz/arazi/sis) GPU marker yok.

---

## 1. [KESİN] — bit-identical, doğrudan uygulanabilir

> **ÖLÇÜLDÜ — CPU tarafının tamamı beklenenden bir mertebe küçük.** C1 uygulanıp
> zamanlandı: kare başı tüm atmosfer işi (3 × `SkyRadiance` + 2 × `BeamTransmittance`)
> **0,0662 ms**. İlk sürümün C1 için verdiği 0,5–2 ms tahmini 8–30 kat şişik. 130 FPS'te
> önbelleğin kazancı 0,0624 ms, yani 7,5 ms'lik karenin **%0,8'i**.
>
> C1 en büyük CPU kalemiydi; gerisi daha küçük. `Dal B toplamı ~1–2,5 ms` beklentisi bu
> yanlış sayının üstüne kuruluydu ve geçersiz. **Bu karede zaman CPU'da değil.**
>
> Ayrıca ölçüldü: 0,1°'lik eşik gün doğumu/batımında gök radyansında **%5,4** göreli
> sıçrama bırakıyor (öğlende %0,0001 — bu yüzden şafakta ölçmek şarttı). Güvenli eşik
> 0,02° (%1,1). Yani önbellek bedava değil: %0,8 için ayarlanması gereken bir eşik taşıyor.
>
> **C1/C2 uygulandı, ölçüldü, geri alındı.** Aşağıdaki CPU kalemleri doğru tespitler ama
> hepsinin toplam getirisi kare başı ~0,15 ms mertebesinde. Profiler capture''ı alınmadan
> CPU tarafına dokunulmaz.

### 1a. CPU

| # | Bulgu | Yer | Kazanç |
|---|---|---|---|
| **C1** | **`SampleSky` × 3 her kare.** `SkyRadiance` 16 dış adım, her adımda 8 adımlı `OpticalDepth` → **384 iç tur/kare**. Üç yön de (güneşe/zenit/uzağa) `sunFlat`'tan türüyor: girdi **yalnız güneş yönü**, kamera değil. 0,1° eşikli cache; 40 dakikalık günde güneş saniyede ~0,15° gider | `AtmosphereController.cs:753-755`, `Atmosphere.cs:253-314` | **0,5–2 ms** |
| **C2** | **`BeamTransmittance` her karede 2 kez** (24 adım), aynı şekilde yalnız güneş yönüne bağlı; C1 ile aynı cache'e bağlanır. *(İlk sürüm 3 diyordu; çağrı yeri **iki**)* | `AtmosphereController.cs:502`, `TimeOfDay.cs:315` | 0,15–0,4 ms |
| **C4** | **`NoonSunDirection` her kare 2 quaternion + normalize.** `DirectionAt(0.5f)` sabit; property cache'lenir. `TerrainSurface.Update` her kare çağırıyor | `TimeOfDay.cs:198,274-284`, `TerrainSurface.cs:222` | küçük, bedava |
| **C6** | **Yağış kapısı yok.** `Update` gövdesinde (390-462) **sıfır `return`** — ölçüldü. Yoğunluk 0'ken bile 8 sınıf sürüklenme entegrasyonu + streak refresh + ~15 materyal yazımı dönüyor. Çizim zaten atlanıyor | `PrecipitationRenderer.cs:390-462` | açık havada 0,1–0,3 ms |
| **C7** | `RenderParams`/`Bounds` hoist + `RainColor` bir kez | `PrecipitationRenderer.cs:492-506,460` | düşük |
| **C8** | **`Shader.SetGlobal` dirty-flag.** ~20 global'in çoğu (FogBase, InversionWidth, FreeDensity/Falloff, SeaFalloff, PlanetRadius) ayara bağlı sabit/yavaş; mevcut `ApplyShadowDistance` 25 m eşiği deseni genişletilir | `AtmosphereController.cs:725-819` | orta-düşük |
| **C10** | **Probe okuma cache'i.** `CloudLayerProbe.Sample` her karede `GetPixelBilinear`; **iki** tüketici (`PrecipitationRenderer:398`, `ClimbHud:142`). 0,25 m hareket eşiğiyle tek okuma. *(İlk sürüm üç tüketici diyordu)* | `CloudLayerProbe.cs:77-80` | 0,05–0,15 ms |
| **C11** | **Ambient probe versiyonlama.** Bake 0,5 s'de bir; iki tüketici her kare `probe.Evaluate` okuyor | `LookController.cs:117-123`, `AtmosphereController.cs:840-848` | düşük |

### 1b. Shader / GPU

| # | Bulgu | Kanıt | Kazanç |
|---|---|---|---|
| **S2** | **Deniz/kıyı bloğu ıslak bandın çok üstünde de çalışıyor.** `seaSurge ∈ [0,1]` ⇒ `seaWetLevel ≤ _SeaLevelY + _SeaRunupHeight`; üstündeki her pikselde `seaWet ≡ 0`. Tek doku okuma + ~40 ALU kayıp | `MountainSurface.hlsl:501-557` | 1 okuma + ~40 ALU/px, ekranın çoğu |
| **S4** | **Deniz köpük kabarcık gürültüsü `whitecap = 0` iken hesaplanıyor.** `saturate(0 - x) ≡ 0`, bubbles'tan bağımsız. Kıyı köpüğündeki ikinci çağrı da `band = 0` iken gereksiz | `SeaLit.shader:427-428, 591` | 36–72 hash/px açık deniz |
| **S1'** | **Swash dantel gürültüsü kapısı** — `laceBand = swash`, `swash = 0` ⇒ `lace = 0`. Kapı `if (swash > 0)` **bit-identical**. **Gerekçe düzeltildi:** ilk sürüm "lace daima 0" diyordu, yanlış (aşağıya bak) | `MountainSurface.hlsl:534-543` | ~40 hash/px, dağın büyük kısmı |
| **S6** | **Heightmap ray tracing bayrağı açık, URP raster yolunda kullanılmıyor** — `m_EnableHeightmapRayTracing: 1` pasif maliyet | `Game.unity:3006` | küçük, kesin |
| ~~S7~~ | **REDDEDİLDİ — dosya CANLI.** `PhysicallyBasedSky.shader` onu **iki kez** include ediyor (satır 23 ve 246) ve `EvaluateStarField` çağırıyor; `SkyWeatherDriver` de `_StarFieldParams`'ı sürüyor. Rapor yalnız `Assets/` altında aramış, referans `Packages/` altında. Silinseydi gökyüzü bozulurdu | `PhysicallyBasedSky.shader:23,205,246,421` | — |
| **S8** | `LightningScatter` erken çıkışı doğru; düzeltme gerekmez | `HeightFog.hlsl:76-79` | — |

### 1c. Ayarlar

| # | Bulgu | Kanıt |
|---|---|---|
| **A1** | **Mipmap Streaming kapalı** — `streamingMipmapsActive: 0`, **iki tier'da da**. 4× 4K normal harita, 138 MB RainStreakDatabase, terrain yan dosyaları. Açılınca aynı mip'ler seçilir, yalnız kullanılmayan üst mip'ler bellekten iner → piksel farkı yok, 8 GB kartta VRAM basıncı düşer | `QualitySettings.asset:39,93` |
| ~~A2~~ | **REDDEDİLDİ — ARAZİYİ TAMAMEN SİLİYOR.** Açıldı ve Play'de zemin çizilmedi: Tri 988k → 561k, Draw 166 → 58, FPS 130 → 153. Kazanç değil, arazinin yokluğu. Sebep: `MountainSurface` instanced terrain yolunu desteklemiyor. Raporun "aynı LOD algoritması ve geometri" varsayımı shader'ın desteklediğini kabul ediyor; etmiyor | ölçüldü, geri alındı |
| ~~A3~~ | **REDDEDİLDİ.** `ModelImportRules.cs:54` her içe aktarmada `isReadable = !character` diye zorluyor, gerekçesi yazılı: *"part measurement and zoning tools access mesh data"*. `BikeBootstrap` gerçekten okuyor (`MeshZones.Build(rack.sharedMesh, …)`). Runtime okuma yok — ama **editör aracı okuyor**, ve rapor yalnız runtime'a bakmış. Kapatmak bisiklet kurulumunu bozar; denendi, postprocessor bayrağı anında geri açtı | `ModelImportRules.cs:40-54` |

---

## 2. [TEST ŞARTLI]

İlk sürümden **taşınanlar** (yanlış etiketliydiler):

| # | Fırsat | Neden [KESİN] değil |
|---|---|---|
| **C3** | `TimeOfDay.Apply` eşikli (güneş açı farkı < 0,02° ise beam + ışık yazımları atlanır) | Eşik `Changed` **event'ini de** atlıyor. Event tüketicileri güncelleme kaçırabilir — davranış değişikliği, bit-identical değil |
| **C5** | `SkyAmbientBaker` follow-up bake'i yalnız saat sıçramasında | Sürekli akışta kaldırmak probe'u **kalıcı olarak bir kare bayat** bırakır. "Fark yok" iddiası ölçülmedi |
| **C9** | `LookController` profil throttle (0,1 s) | Adapt yumuşatması her kare koşmak **zorunda**; ayırmak mimari dokunuş. Kazanç zaten 0,05–0,15 ms |
| **S5** | `gokLum` = `SampleSH(0,1,0)` kare başına CPU'da | Raporun kendi ifadesi: *"half rounding farkı tek düşük anlamlı bit"*. Tanım gereği bit-identical **değil** |

İlk sürümden **korunanlar**:

| # | Fırsat | Beklenen | Risk / test şartı |
|---|---|---|---|
| **T1** | **UYGULANDI.** HDR 64 → 32 bit (R11G11B10f) | **Play'de bant görülmedi** (gökyüzü, deniz ufku, sis gradyanı, gece). **FPS DEĞİŞMEDİ** — raporun "en büyük tekil GPU kazancı" iddiası çürüdü. Renk tamponu yarıya indi; kazanç kare hızı değil, pay. Kalıyor | ölçüldü |
| **T2** | DepthNormals pass'te tessellation kaldırmak — o pass zaten `_GroundNormals`'ten düz normal yazıyor | vertex yükü ~64× | Depth-priming farkı teorik; A/B karesi |
| **T3** | ShadowCaster/DepthOnly tess faktörü (ör. maks/4) — yer değiştirme ≤ 30 cm, 150 m shadowmap'te texel 7 cm | orta | Gölge silueti teknik olarak değişir |
| **T5** | Far clip **90000** → 60000 (doğrulandı: `Game.unity:772`); bulut katmanı 48 km | depth hassasiyeti de düzelir | Bulut/sky shader'ları far plane'e duyarlı; ufuk karesi A/B |
| **T6** | **UYGULANDI.** Berrak hava gölge tavanı 150 → 110 m | Gölgelenen alan **%46** azaldı, kesilme görülmedi. **FPS DEĞİŞMEDİ**. Kalıyor | ölçüldü |
| **T7** | Kar iki-ölçek stokastik okuma skip'i + `fineLap` 4 → 2 örnek | orta | Desen örneği değişir; yakında görünürse iptal |
| **T8** | Precipitation `AirColor` vertex başına (parçacık başına 4 kez) | yağmurda vertex ALU ~%20 | Mimari değişiklik; yağış zaten hızlı, öncelik düşük |
| **T9** | Volume framework "Every Frame" → "Via Scripting" | düşük | Tetik gecikirse hava geçişinde fark |

---

## 3. ÇÜRÜYEN MADDELER — uygulanmayacak

### S1 — "lace daima 0" YANLIŞ (aritmetik hatası)

İlk sürüm: *"laceNoise maksimumu 0.875 + 0.375 × 0.5 = 1.0625 < 1.25 → lace daima 0"*.

`× 0.5` **zaten** ikinci FBM'in kendi toplamına uygulanmış:

```
MountainFbm(p, 3) → 0.5 + 0.25 + 0.125           = 0.875
MountainFbm(p, 2) → (0.5 + 0.25) × 0.5           = 0.375
laceNoise maksimumu                               = 1.25      ← eşiğin tam kendisi
```

`MountainHash` `frac()` döndürüyor, yani `MountainNoise ∈ [0,1)` — sınır doğru.

```hlsl
lace = saturate((swash - (1.25 - laceNoise) * 0.7) * 2.2)
```

Tipik `laceNoise ≈ 0.65` için eşik ≈ **0,42**: swash bunu geçince dantel çiziliyor. Terim
ölü değil, kumsalda çalışıyor.

**Kapının kendisi geçerli** (`swash = 0 ⇒ lace = 0`, bit-identical) ve S1' olarak yukarıda
duruyor. Çürüyen şey gerekçe — "daima 0" diye terimi **silen** kişi kumsaldaki danteli siler.

### S3 — kazanç SIFIR, ve hata "optimizasyon fırsatı" sanılmış

İlk sürüm: *"bulut upscale döngü değişmezi 49 kez yeniden hesaplanıyor, hoist edilebilir,
bit-identical, ~100 transandental/px tam ekran"*.

**İki ayrı sorun:**

1. **Fonksiyon çağrılmıyor.** `_LOW_RESOLUTION_CLOUDS` anahtarı yalnız
   `resolutionScale < 1 && upscaleMode == Bilateral` iken açılıyor
   (`VolumetricCloudsURP.cs:545`). Ayar `Bilinear` → anahtar kapalı → `BilateralUpscale`
   hiç koşmuyor. İddia edilen tam ekran kazancı **sıfır**.
2. **Değişmez bir fırsat değil, hata.** `VolumetricCloudsUpscale.hlsl:31` döngünün içinde
   ama `i`/`j` içermiyor — uzamsal ağırlık 49 tapın hepsinde aynı çıkıyor ve
   normalizasyonda sadeleşiyor, yani filtrenin düşüşü **hiç çalışmıyor**. Hoist etmek bozuk
   çıktıyı bit-identical korur. Ayrıca filtre, adı "bilateral" olduğu hâlde derinliğe hiç
   bakmıyor.

Bu düzeltme bir kez yapıldı ve ekranda hiçbir şey değişmedi — çünkü kod ölü. Mod
`Bilateral`'e alınmadıkça buraya dokunmanın FPS karşılığı yok.

---

## 4. Dokunulmayacaklar — görüntü değiştirir

Shadowmap 4096→2048, soft shadow quality, cascade split'ler, MSAA 2x, RenderScale, FSR,
bulut adım sayıları (128/8), `shadowDistance` 12000, SSAO, kar/deniz kalite tier'ları,
kar normal 4K→2K, terrain pixel error, opaque texture (SeaLit kırılması), vignette/SSAO
geri dönüşleri.

**Bulut `resolutionScale`** de bu listeye girdi: 0,5 → 1,0 kontur artefaktını gizliyor ama
**kare hızının yarısını** yiyor (130 → 60 FPS, ölçüldü). Kontur ayrı bir yoldan çözüldü —
`SYMPTOMS.md` → "Bulut kenarı konturu".

**Kırmızı bayraklar:** ışıktan bağımsızlık ucuzlatmalarının sökülmesi (`DECISIONS.md` ~218);
kar clipmap Medium (kullanıcı görüp seçti, `DECISIONS.md:1714`); ESM gölgesi 150 m'de
kazanç üretmiyor (ölçüldü, reddedildi); Profiler'da Rendering+GPU modülleri açık olmadan
sayaçlar 0 döner (`DECISIONS.md:1385`).

---

## 5. Uygulama sırası

**Adım 0 — ölçüm:** bir oturum Profiler capture'ı (Render + GPU modülleri açık).
Aşağıdaki sıralama tahmindir; capture ile yeniden sıralanır.

**C1/C2 ölçüldü ve geri alındı** (bölüm 1'deki kutu). CPU tarafı, Profiler capture'ı
alınana kadar sıraya girmiyor: en büyük kalemi 0,066 ms çıkan bir bütçede kalanları
aramak boşa tur.

1. **S2, S4, S1'** — shader kapıları. CPU kalemlerinin aksine ölçek burada: maliyet
   ekranın çoğunda piksel başına ödeniyor. Her biri ayrı commit, piksel diff'i 0 olmalı.
2. **A1** — doku akışı. Bedava, ve VRAM tarafında gerçek. (A2 ve A3 reddedildi.)
3. **Profiler capture'ı** — 988k üçgen, 166 draw, yarı çözünürlüklü bulut marşı, froxel
   sisi. Zamanın nerede olduğu buradan görülür; kalan sıralama ondan sonra yazılır.
4. **[TEST ŞARTLI]** — T1'den başlayarak tek tek, screenshot-diff protokolüyle.
5. **CPU kalemleri (C4, C6, C7, C8, C10, C11)** — doğru tespitler, toplamı ~0,15 ms.
   Yalnız capture zamanın CPU'da olduğunu gösterirse.

**Protokol (her madde için aynı):** F1'den zaman durdur + havayı elle ayarla + kapsamayı
kilitle → önce/sonra aynı kadraj → piksel diff → fark 0. Fark çıkarsa madde geri alınır ve
`SYMPTOMS.md`'ye yazılır.


---

## 6. Ölçümün söylediği: darboğaz bulunamadı

İki bağımsız yük yarıya indirildi ve **kare hızı iki seferinde de kıpırdamadı**:

| ne azaltıldı | ne kadar | FPS |
|---|---|---|
| piksel bant genişliği (T1) | renk tamponu yarıya | değişmedi |
| gölgelenen alan (T6) | %46 | değişmedi |

Buna CPU tarafındaki ölçüm de ekleniyor: en büyük CPU kalemi (C1, tüm atmosfer işi)
**0,066 ms** — 7,5 ms'lik karenin %0,9'u.

Yani kare **ne piksel bant genişliğine, ne gölge alanına, ne de bu CPU işlerine** bağlı.
Üç yönde de aranıp bulunamadı. Kalan adaylar ölçülmeden sıralanamaz: bulut ışın yürüyüşü
(yarı çözünürlük, 128 adım), froxel sis hacmi, arazi tessellation'ı, ana iş parçacığı.

**Bir sonraki adım tahmin değil, capture.** Profiler'da Rendering + GPU modülleri açık bir
oturum alınmadan bu rapordan başka madde uygulanmaz — çünkü rapordaki her tahmin şimdiye
kadar ya çürüdü ya da bir mertebe şişik çıktı.

