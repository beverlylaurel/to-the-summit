# FPS Analizi — Görüntüye Sıfır Dokunuşla İyileştirme Fırsatları

Tarih: 2026-08-29 · Yöntem: 4 paralel salt-okunur analiz ajanı (CPU scriptleri, shader/fragment
maliyeti, GPU/render ayarları, ölçüm altyapısı) + bulguların "görsel etki" filtresinden geçirilmesi.
Hiçbir dosya değiştirilmedi.

**Başlık kuralı:** Bu raporda "görüntü değişmez" iddiası iki seviyede kullanılıyor:

- **[KESİN]** — matematiksel olarak bit-identical: atlanan iş zaten 0 veya özdeş değer üretiyor
  (kanıt satır numarasıyla birlikte verilmiş). Test yine de yapılacak ama piksel farkı
  *olmaması yapısal olarak garantili*.
- **[TEST ŞARTLI]** — beklenen fark sıfır veya algı altı ama piksel seviyesinde garanti yok;
  A/B karesi + renk probu olmadan uygulanmaz.

---

## 0. Baz durum (ölçülmüş)

| Metrik | Değer | Kaynak |
|---|---|---|
| Frame time (genel) | 8.2–8.7 ms = **115–122 FPS** | DECISIONS.md:1711 |
| Draw / SetPass / Tri | 525 / 61 / 1248k | DECISIONS.md:1711 |
| Yağış anı | 89k canlı parçacık, 119 FPS | SYMPTOMS.md:1110 |
| GPU | RTX 4060 8 GB, D3D12, Editor | Logs/Editor.log:5 |
| Kalite tier | Kar **MEDIUM**, Deniz **MEDIUM** | SnowSettings.asset, SeaSettings.asset |
| FrameRateCap | vSync 0, hedef 244 FPS | FrameRateCap.cs:10-11 |

**Ölçüm altyapısı durumu:** CPU tarafı hazır (PerformanceSampler: FPS, 1% low, GC MB/s;
kar tarafında 5 GPU marker). **Eksik:** ProfilerCaptures/ boş, diske frame-log yok,
kar dışı sistemlerde (bulut/deniz/arazi/sis) GPU marker yok. İlk iş: baz Profiler
capture'ı almak — aşağıdaki tüm tahminler capture ile doğrulanmadan uygulanmaz.

---

## 1. [KESİN] Bulgu listesi — bit-identical, doğrudan uygulanabilir

### 1a. CPU tarafı (scriptler)

| # | Bulgu | Yer | Kazanç |
|---|---|---|---|
| C1 | **SampleSky × 3 her frame** — SkyRadiance(16 step) × 3 yön (güneşe/zenit/uzağa), her biri 8-stepli OpticalDepth içerir → ~384 iç döngü turu/frame. Sonuç **yalnız güneş yönüne bağlı** (kamera değil). 0.1° güneş eşiğiyle cache'le; 40 dk'lık günde güneş saniyede ~0.15° hareket eder → cache altında görsel fark imkânsız | AtmosphereController.cs:747-749, Atmosphere.cs:253-314 | **0.5–2 ms** |
| C2 | **BeamTransmittance her frame 3 kez** (24 step) — aynı şekilde yalnız güneş yönüne bağlı; aynı cache'e bağlanır | Atmosphere.cs:173-191, AtmosphereController.cs:496, TimeOfDay.cs:315 | 0.2–0.6 ms |
| C3 | **TimeOfDay.Apply eşikli** — güneş açı farkı <0.02° ise beam + light yazımları + `Changed` eventi atlanır (normalized ilerler) | TimeOfDay.cs:286-380 | C1+C2 ile atmosfer bloğu ~%90 |
| C4 | **NoonSunDirection her frame 2 quaternion hesaplıyor** — `DirectionAt(0.5f)` sabit; property cache'lenmeli. TerrainSurface.Update her frame çağırıyor | TimeOfDay.cs:198, TerrainSurface.cs:222 | küçük ama bedava |
| C5 | **SkyAmbientBaker follow-up bake** — her periyodik bake'ten sonra ertesi frame'de ikinci `DynamicGI.UpdateEnvironment` koşuyor; sürekli akan güneşte fiilen her 0.5 s'de 2 bake. Follow-up yalnız saat-sıçrama tespitinde tetiklenmeli (Paused/kadran atlaması) — sürekli akışta fark yok, sıçramada mevcut davranış korunur | SkyAmbientBaker.cs:55-80 | bake spike'ı yarıya |
| C6 | **PrecipitationRenderer kapı eşiği** — yoğunluk 0'ken (açık hava) 8 sınıf drift entegrasyonu + streak refresh + ~15 materyal yazımı koşulsuz dönüyor; çizim zaten atlanıyor, sadece CPU işi boşa | PrecipitationRenderer.cs:390-462 | açık havada 0.1–0.3 ms |
| C7 | **RenderParams/Bounds hoist + RainColor bir kez** | PrecipitationRenderer.cs:492-506, 460 | düşük |
| C8 | **Shader.SetGlobal dirty-flag** — ~20 global'in çoğu (FogBase, InversionWidth, FreeDensity/Falloff, SeaFalloff, PlanetRadius) settings'e bağlı sabit/yavaş; mevcut `ApplyShadowDistance` 25 m eşiği deseni genişletilir | AtmosphereController.cs:725-819 | orta-düşük |
| C9 | **LookController profil throttle** — 4× LookProfile.Lerp + 12 Volume yazımı her frame; girdiler yavaş. 0.1 s throttle (adapt smoothing hariç) | LookController.cs:91, 128-238 | 0.05–0.15 ms |
| C10 | **Probe okuma cache'i** — CloudLayerProbe her frame GetPixelBilinear; PrecipitationRenderer aynı texture'ı ikinci kez okuyor; ClimbHud 10 Hz'de üçüncü. 0.25 m hareket eşiğiyle tek okuma | CloudLayerProbe.cs:47-81 | 0.05–0.2 ms |
| C11 | **Ambient probe versiyonlama** — bake 0.5 s'de bir; iki tüketici her frame `probe.Evaluate` okuyor | LookController.cs:117-123, AtmosphereController.cs:840-848 | düşük |

### 1b. Shader / GPU tarafı — atlanan iş kanıtlanabilir şekilde 0 üretiyor

| # | Bulgu | Kanıt | Kazanç |
|---|---|---|---|
| S1 | **Swash lace noise her terrain pixel'inde hesaplanıyor** — dağın %99'unda swash=0. `laceNoise` maksimumu 0.875+0.375×0.5=1.0625 < 1.25 → `lace` daima 0. `if (swash > 0)` gate'i **bit-identical** | MountainSurface.hlsl:550-553 | **~40 hash (~400 ALU)/px, tüm dağ** |
| S2 | **Deniz/kıyı bloğu deniz seviyesinin çok üstünde de çalışıyor** — `seaSurge ∈ [0,1]` ⇒ `seaWetLevel ≤ _SeaLevelY + _SeaRunupHeight`; bunun üstündeki her pixel'de `seaWet ≡ 0` (S1 ile birlikte lace de 0). Tek texture okuma + ~40 ALU kayıp. Sahil bandının üstü için **bit-identical** | MountainSurface.hlsl:501-557 | 1 okuma + ~40 ALU/px, ekranın çoğu |
| S3 | **Bulut upscale pass döngü-değişkeni invariant** — `distance` ve `Weight(length(distance))` 7×7 döngüde 49 kez yeniden hesaplanıyor (screenUV/offsetUV döngüde sabit); hoist edilebilir. İki upscale pass'inde de (renk + transmittance) aynı durum → **bit-identical** | VolumetricCloudsUpscale.hlsl:16-76 | **~100 transandental/px, tam ekran** |
| S4 | **Deniz beyaz köpük bubble gürültüsü whitecap=0 iken hesaplanıyor** — `saturate(0 - x) ≡ 0`; bubbles'tan bağımsız. Shore-foam ikinci bubbles çağrısı da `band=0` iken gereksiz. Her ikisi gate'lenebilir, **bit-identical** (quad içi diverjans notu: dalga tepeleri yakınında branch karışımı olur, net kazanç açık denizde) | SeaLit.shader:360-395, 501-534 | 36–72 hash/px açık deniz |
| S5 | **gokLum her kar pixel'inde SampleSH(0,1,0) hesaplıyor** — sabit girdi; frame başına CPU'da hesaplanıp global'e yazılabilir (aynı formül; half rounding farkı tek düşük anlamlı bit, görsel etkisiz) | MountainSurface.shader:251 | küçük, bedava |
| S6 | **Heightmap ray tracing bayrağı açık ama URP raster yolunda kullanılmıyor** — `m_EnableHeightmapRayTracing: 1` pasif maliyet | Game.unity Terrain komponenti | küçük, kesin |
| S7 | **Sky.shader yalnız fallback** (PBSky devrede; birkaç kare çizilir) — yıldız/disk optimizasyonu gereksiz; ayrıca `StarField.hlsl` hiçbir shader'a include edilmiyor = ölü dosya (temizlik maddesi, FPS değil) | Sky.shader, StarField.hlsl | ihmal |
| S8 | **LightningScatter erken çıkışı doğru** — şimşek yokken maliyet 1 branch. Düzeltme gerekmez (doğrulandı) | HeightFog.hlsl:76-79 | — |

### 1c. Ayar tarafı — görüntü değiştirmeden bellek/çizim yolu

| # | Bulgu | Not |
|---|---|---|
| A1 | **Mipmap Streaming kapalı** — 4× 4K normal harita (95 MB PNG), 138 MB RainStreakDatabase, terrain yan dosyaları 300 MB+. `streamingMipmapsActive: 1` aynı mip'leri ekranda seçer, sadece kullanılmayan üst mip'ler bellekten iner → piksel farkı yok, VRAM basıncı ciddi düşer (8 GB kart) | QualitySettings.asset |
| A2 | **Terrain Draw Instanced kapalı** — `m_DrawInstanced: 0`; aynı LOD algoritması/ geometri, instanced çizim yolu. 30 km terrain + 4097² heightmap için CPU submit maliyeti düşer. A/B karesiyle doğrulanır | Game.unity Terrain |
| A3 | **Bike FBX `isReadable: 1`** — runtime mesh okuma yapılmıyorsa (grep ile doğrulanacak) kapatmak VRAM/CPU kopyasını düşürür, render'a etkisi yok | Bicycle.fbx.meta |

---

## 2. [TEST ŞARTLI] — beklenen kazanç büyük, piksel garantisi yok; A/B olmadan dokunulmaz

| # | Fırsat | Beklenen | Risk / test şartı |
|---|---|---|---|
| T1 | **HDR 64-bit → 32-bit** (`m_HDRColorBufferPrecision: 1→0`, R11G11B10f) — bant genişliği ~%35-40; RTX 4060'ta muhtemelen en büyük tekil GPU kazancı | Banding riski: Unity bizzat "hafif mavi/sarı banding nadir durumlarda" diyor; bu sahne gradyan-yoğun (sis + PBSky + bulut). Pin'lenmiş saat/hava koşullarında gökyüzü, deniz ufku, sis gradyanı screenshot-diff'i. Banding görünmüyorsa geçer | **yuksek** |
| T2 | **DepthNormals pass'te tessellation'ı kaldırmak** — o pass zaten `_GroundNormals`'ten düz normal yazıyor (tess displaced normal içermiyor); faktör 1'de normal değeri özdeş. Vertex yükü o pass'te ~64× azalır | Depth-priming farkı teorik; A/B karesi | orta-yüksek |
| T3 | **ShadowCaster/DepthOnly tess faktörünü düşürmek** (ör. maks/4) — yer değiştirme ≤30 cm, 150 m shadowmap'ta texel 7 cm | Gölge silüeti teknik olarak değişir; kademeli düşür + gölge kenarı A/B | orta |
| T4 | **Follow-up bake birleştirme** — C5'in alternatif varyantı; yalnız "Freeze time + saat sıçrama" senaryosunda probe 0.5 s gecikebilir | O senaryo için ayrı test | düşük |
| T5 | **Far clip 90000 → 60000** — çizilen geometri yok gibi (bulut katmanı 48 km); depth hassasiyeti de düzelir | Bulut/sky shader'ları far plane'e duyarlı olabilir; ufuk karesi A/B | düşük-orta |
| T6 | **Berrak hava gölge tavanı 150 → ~110 m** — kod içi yorum bizzat "25 m gölge farkı gözle ayırt edilmez" diyor; DrawShadows alanı ~%46 azalır | Uzak gölge fade bölgesi hafif değişir; aynı saat/açıda A/B | orta (ölçüm: DrawShadows payı önce Profiler'dan) |
| T7 | **Kar iki-ölçek stokastik okuma skip'i** (relief fade bölgesinde ikinci ölçek atlanır) + **fineLap 4 örnek → 2 örnek** — uzakta sub-pixel, yakında desen kayar | Karakter korunur ama desen örneği değişir — projenin "ölçmeden düzeltme yok" kuralı gereği ölçümle | orta; yakında görünürse iptal |
| T8 | **Precipitation AirColor vertex başına** — parçacık başına 4 kez hesaplanıyor; compute'a taşımak mimari değişiklik, yağmurda vertex ALU ~%20 | Görüntü korunur ama büyük iş; yağış zaten 119 FPS — öncelik düşük | düşük öncelik |
| T9 | **Volume framework "Every Frame" → "Via Scripting"** — parametre yazımlarında update tetikleme | Tetik gecikirse hava geçişinde fark; C8/C9 ile aynı dalda ama ayrı test | düşük |

---

## 3. Dokunulmayacaklar — görüntü değiştirir (kayıt için)

Shadowmap 4096→2048, soft shadow quality, cascade split'ler, MSAA 2x, RenderScale,
FSR, bulut adım sayıları (128/8) ve shadowDistance 12000, SSAO, kar/deniz kalite
tier'ları (her iki sistemde tier düşürmek görüntüyü değiştirir: kar detay normali +
sparkle + AO kaybolur; deniz kırınımı flat renge döner), snow normal 4K→2K,
terrain pixel error, opaque texture (SeaLit kırılması), vignette/SSAO geri dönüşleri.

**Kırmızı bayraklar (önceden denenmiş, tekrar denenmeyecek):** ışıktan bağımsızlık
ucuzlatmalarının sökülmesi bilinçli kabul (DECISIONS.md ~218: "sökülen ucuzlatmalar
geri gelirse belirtileri de geri gelir"); kar clipmap Medium (kullanıcı görüp seçti,
DECISIONS.md:1714); ESM gölgesi 150 m'de kazanç üretmiyor (ölçüldü, reddedildi);
"ölçmeden ucuzlatma yok" — Profiler'da Rendering+GPU modülleri açık olmadan sayaçlar
0 döner (DECISIONS.md:1385).

---

## 4. Önerilen uygulama sırası

**Adım 0 — Ölçüm (ön şart):** bir oturum Profiler capture'ı (Render + GPU modülleri
açık) + PerformanceSampler'ın diske log yazması (tek ek: oturum sonu CSV). Hedef:
DrawShadows / bulut pass / terrain / deniz / sis pass paylarının gerçek kırılımı.
Aşağıdaki sıralama tahmindir; capture ile yeniden sıralanır.

**Dal A — [KESİN] shader gate'leri (S1-S5):** her biri bit-identical; tek dalda,
her madde ayrı commit. Doğrulama: pin'lenmiş zaman/hava koşulu (DebugMenu: zaman
durdur + havayı elle ayarla + kapsamayı kilitle) → değişiklik öncesi/sonrası aynı
kadrajdan screenshot diff + mevcut snow debug probu. Fark 0 piksel olmalı; 1-2
piksel dönerse madde geri alınır.

**Dal B — [KESİN] CPU cache'leri (C1-C11):** C1+C2+C3 birlikte (aynı cache mekanizması:
TimeOfDay'e 0.02°-eşikli "atmosfer değişti" sinyali), sonra diğerleri tek tek.
Doğrulama: görsel aynı + PerformanceSampler'da frame time düşüşü kaydı.

**Dal C — Ayarlar (A1-A3):** streaming + draw instanced + isReadable; A/B karesi.

**Dal D — [TEST ŞARTLI] (T1-T9):** Dal A-C'nin ölçümü oturduktan sonra, en büyük
kazanç adayı T1'den başlayarak tek tek, her biri için screenshot-diff protokolüyle.
T6 için önce Profiler'da DrawShadows payının doğrulanması şart.

**Beklenen toplam (tahmin, ölçümsüz):** Dal A+B CPU'da ~1-2.5 ms; T1 geçerse GPU
bant genişliğinde ~%35. 8.2-8.7 ms bazda Dal A+B tek başına 115-122 → ~135-150 FPS
bandına taşıyabilir; T1 ile GPU sınırındaki karelerde ek getiri.

---

## 5. Rapor boyunca tekrarlanacak kural

Bu projede görsel doğrulama aracı zaten var: DebugMenu'de zaman dondurma, hava
kilitleme, kapsam kilidi, sis aç/kapa anahtarları + snow debug probe'ları. Her
değişiklik için protokol aynı: **pin'lenmiş durum → önce/sonra kadraj → piksel
diff → fark 0.** Ölçüm gelmeden sonraki maddeye geçilmez; fark çıkarsa madde geri
alınır ve SYMPTOMS.md'ye yazılır.
