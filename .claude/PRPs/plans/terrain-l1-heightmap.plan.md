# Plan: L1 — yükseklik haritası ve Unity arazisi

## Özet

L0 grafiğinden 4097² yükseklik haritası üretilir, **PNG-16 olarak repoya yazılır** ve
editör aracıyla Unity arazisine uygulanır. `MountainGenerator`'ın radyal koni üretimi
sökülür.

Bu adım bir değişmezi kırıyor ve planın en önemli kısmı o: arazi artık **tohum ve
ayarlardan yeniden üretilemiyor**.

## Kullanıcı hikâyesi

Bir **oyuncu** olarak Play'e bastığımda Divide Tree'den üretilmiş gerçek dağı görmek
istiyorum; şu an hâlâ eski radyal koni geliyor.

## Problem → Çözüm

**Şu an:** `MountainGenerator.Generate()` her kurulumda `MountainSettings`'ten radyal koni
hesaplayıp `terrainData.SetHeights()` çağırıyor. Ölçüldü: üst 2 km her azimutta 48–57°,
yürünecek hat yok.

**Olacak:** yükseklik haritası `Tools/terrain/` tarafından bir kez üretilip
`Assets/Terrain/MountainHeightmap.png` olarak repoya yazılır; editör aracı onu okuyup
aynı `terrainData`'ya uygular. Ölçülen sonuç: etekten zirveye 17.56 km yürünebilir rota,
ortanca 18.1°, teknik tırmanış %0.

## Üstveri

- **Karmaşıklık:** Large
- **Kaynak spec:** `specs/terrain/terrain-generation-spec.md` §5.4–5.8
- **Faz:** L1 (dördün ikincisi)
- **Tahmini dosya:** 3 yeni, 4 güncellenen, 1 kısmen silinen

---

## ⛔ EN ÖNEMLİ BULGU: bir değişmez kırılıyor

`.gitignore`'un kendi gerekçesi:

> **ÜRETİLEN ARAZİ VARLIKLARI.** Hepsi tohum ve ayarlardan birebir yeniden üretiliyor;
> kurulum betiği açılışta kendisi pişiriyor. Depoda tutmanın tek sonucu her üretimde
> LFS'e yetmiş megabayt daha yazmaktı — **3.8 GB oraya gitti.**

Bu yüzden şunlar izlenmiyor: `MountainTerrainData.asset` (34.6 MB),
`MountainHorizon.asset` (67 MB), `MountainNormals.asset` (44.7 MB),
`MountainSurfaceMaps.asset` (11.2 MB), `MountainSnowDrift.asset` (2.8 MB),
`MountainHeight.asset` (4.2 MB) — toplam **~165 MB**.

**L1 sonrası bu önerme yanlış oluyor.** Yeni arazi tohumdan üretilemiyor; Python + Argudo
+ 1.4 GB Kirmse veritabanı gerekiyor ve bunların hiçbiri repoda değil, olamaz da.

### Karar: yükseklik haritası KAYNAK olur, gerisi türetilmiş kalır

Ölçüldü (oyun alanı 4097², `terrainHeight` 6189 m ölçeğinde 16 bit):

| biçim | boyut |
|---|---|
| **PNG-16 sıkıştırılmış** | **13.8 MB** |
| npz (deflate) | 28.8 MB |
| ham `.r16` | 32.0 MB |

Nicemleme hatası ortalama **2.4 cm**, en kötü 4.7 cm (kuantum 9.4 cm). Unity arazisi
zaten 16 bit — kayıp yok.

| ne | nerede | neden |
|---|---|---|
| `MountainHeightmap.png` 14 MB | **repoda** | artık üretilemiyor, kaynak |
| `MountainTerrainData.asset` 34.6 MB | yerelde | haritadan tek adımda pişiyor |
| Normal / ufuk / yüzey / birikinti 128 MB | yerelde | araziden pişiyor |

Sürüm başına repo büyümesi **~14 MB**. `.gitignore`'un gerekçe yorumu **aynı adımda**
düzeltilecek; yoksa bir sonraki okuyan "her şey yeniden üretilebilir" sanır.

---

## Zorunlu okuma

| öncelik | dosya | satır | neden |
|---|---|---|---|
| P0 | `Assets/Scripts/Terrain/MountainGenerator.cs` | 57–121 | `Generate()` — sökülecek olan; `SetHeights` çağrısı burada |
| P0 | `Assets/Editor/MountainSceneBootstrap.cs` | 1185–1210 | `CreateMountain` — `TerrainData` asset'ini kuran yer |
| P0 | `.gitignore` | 63–76 | Kırılan değişmezin gerekçesi |
| P0 | `DECISIONS.md` | "L0 uygulandı", "Yüzey detayı ÇÖZÜLDÜ", "Yaklaşma koridoru" | Ölçülmüş sınırlar |
| P1 | `Tools/terrain/l1_play.py` | tamamı | Kırpma + mesh + rasterleme; çalışıyor ve ölçüldü |
| P1 | `Tools/terrain/detail.py` | tamamı | Multifraktal + kısıtlı erozyon; zirveyi birebir koruyor |
| P1 | `Assets/Editor/DivideTreeImporter.cs` | tamamı | İçe aktarma kalıbı — L1 aracı buna benzeyecek |
| P2 | `Assets/Editor/SurfaceMapBaker.cs` | 127–140 | `Bake(Terrain, float)` — imza `Terrain` alıyor, üretici değil |
| P2 | `Assets/Editor/MountainTunerWindow.cs` | 22, 44, 137, 197 | Üreticiye bağlı **tek** araç |

---

## Keşif: söküm sanılandan küçük

Tüketiciler tarandı. **Neredeyse hepsi `Terrain`/`terrainData` ile konuşuyor,
`MountainGenerator` ile değil:**

| tüketici | neye bağlı | L1'de kırılır mı |
|---|---|---|
| `SurfaceMapBaker.Bake(Terrain, float)` | `Terrain` | **hayır** |
| `TerrainSurface` | `terrainData.size` | **hayır** |
| `SnowSurface` | `Terrain` | **hayır** |
| `ForelandProbe` | `terrainData.size` | **hayır** |
| `RouteTerrainShaper.Shape(Terrain, MountainRoute)` | `Terrain` | **hayır** |
| `MountainTunerWindow` | `MountainGenerator`, `MountainSettings.heightProfile` | **evet** |
| `MountainSceneBootstrap` | `MountainGenerator.Generate()` | **evet** |

Yani arazi verisi aynı yere (`terrainData`) aynı biçimde girdiği sürece zincirin gerisi
hiç etkilenmiyor. Bu, L1'i Large yapan şeyin söküm değil **saklama kararı** olduğunu
gösteriyor.

---

## Mimarî karar

**Yaklaşım:** Python üretir → PNG-16 repoya → editör aracı `SetHeights` → mevcut zincir
değişmeden devam.

**Değerlendirilen ve reddedilenler:**

- **`TerrainData.asset`'i commit'lemek** — 34.6 MB, üstelik Unity'nin ikili biçimi;
  delta'lanmıyor ve her pişirmede tamamı yeniden yazılıyor. PNG-16 hem yarısı kadar hem
  araç bağımsız.
- **L1'i C#'a port etmek** — `divtree_to_dem.py` 887 satır, `triangle` (kısıtlı Delaunay),
  `shapely` ve `scipy` kullanıyor. L0'daki gerekçenin aynısı: çalışma zamanında hiç
  gerekmiyor.
- **Araziyi hiç saklamayıp her kurulumda Python çağırmak** — kullanıcının makinesinde
  1.4 GB veritabanı ve Python ortamı gerektirir; "kullanıcı yalnız Unity'de tıklar"
  kuralını çiğner.

### B sorusunun cevabı: `detail.py` KALIR, C# kopyaları GİDER

`DECISIONS.md` bir ara "`Erode`, teraslar, çok oktavlı gürültü **kalıyor**" diye yazmıştı.
**Bu kayıt düzeltilecek.** O sırada detay katmanı henüz yazılmamıştı; gece `detail.py`
içinde spec'e göre yeniden yazıldı ve ölçüldü. İki kopya tutmak dublikasyon olur ve
C# tarafının **çağıranı kalmayacak**.

Giden: `Generate`, `SampleHeight`, `Erode`, `FileCrests`, `BakeProfileLut`, `Foreland`,
`MoraineField`, teras/çakıl/kanal/havza üreticileri, `InitPeaks`, `ProfileAt` ve
`MountainSettings`'in bunlara ait alanları.

Kalan: `Measure`, `ComputeSlopeStats`, `SlopeBand`, `AltitudeBandCount` — bunlar araziyi
**okuyor**, üretmiyor; `MountainSceneBootstrap`'in raporu onlara bağlı.

---

## Uyulacak kalıplar

### ARAÇ ÇIKTISI DOSYAYA
```csharp
// KAYNAK: Assets/Editor/ToolLog.cs:5-10
/// ARAÇ ÇIKTISI DOSYAYA GİDER, KONSOLA DEĞİL.
/// bilgi `ToolLog.Write`, sorun `Debug.LogWarning` ya da `Debug.LogError`.
```

### İÇE AKTARMA + DENETİM
```csharp
// KAYNAK: Assets/Editor/DivideTreeImporter.cs:19-22, 118-133
if (!File.Exists(SourcePath))
    throw new FileNotFoundException($"Divide Tree kaynağı yok: {SourcePath}. ...");
// ... ice aldiktan sonra sozlesme denetimi, sorun varsa Debug.LogWarning
```

### MENÜ
```csharp
// KAYNAK: Assets/Editor/DivideTreeImporter.cs:16
[MenuItem("To The Summit/Arazi/Divide Tree'yi İçe Aktar", false, 10)]
```

### ARAZİYE YAZMA
```csharp
// KAYNAK: Assets/Scripts/Terrain/MountainGenerator.cs:66-70, 100-101
var data = terrain.terrainData;
data.heightmapResolution = resolution;
data.size = new Vector3(settings.terrainSize, settings.terrainHeight, settings.terrainSize);
transform.position = new Vector3(-settings.terrainSize * 0.5f, 0f, -settings.terrainSize * 0.5f);
data.SetHeights(0, 0, heights);
terrain.Flush();
```

### TEST KALIBI — YOK
Projede test paketi yok, tek bir `try` bloğu bile yok. Skill'in test adımları burada
**derleme kontrolü + kullanıcının Unity'de doğrulaması**.

---

## Değişecek dosyalar

| dosya | işlem | gerekçe |
|---|---|---|
| `Tools/terrain/bake_heightmap.py` | OLUŞTUR | L0 → mesh → 4097² → PNG-16, tek komut |
| `Assets/Terrain/MountainHeightmap.png` | OLUŞTUR (çıktı) | **Yeni kaynak**, 14 MB, repoda |
| `Assets/Editor/HeightmapImporter.cs` | OLUŞTUR | PNG → `terrainData.SetHeights` |
| `Assets/Scripts/Terrain/MountainGenerator.cs` | KISMEN SİL | Üretim gider, ölçüm kalır |
| `Assets/Scripts/Terrain/MountainSettings.cs` | KISMEN SİL | Radyal koni ve ova alanları |
| `Assets/Editor/MountainSceneBootstrap.cs` | GÜNCELLE | `Generate()` yerine içe aktarma |
| `Assets/Editor/MountainTunerWindow.cs` | GÜNCELLE ya da SİL | Ayarladığı alanlar kalmıyor |
| `.gitignore` | GÜNCELLE | Gerekçe yorumu artık yanlış |
| `SYSTEMS.md`, `SCALE.md`, `DECISIONS.md` | GÜNCELLE | Planın adımları |

## Bu planda YOK

- **L2** — eğim kuşağı, bakı, korunaklılık, çığ maruziyeti maskeleri
- **L3** — kamp/konak/mağara/anıt yerleştirmesi ve sembolik çapalar
- **Uzak bantlar** — 18–60 km ve 60–300 km mesh'leri, eğrilik, far clip, haze, cloudMapSize
- **Zirve modülü** — elle tasarlanan son kol
- **Rotanın yeniden konumlanması** — ölçülür ve raporlanır, taşınmaz (L3'ün işi)
- **Çapalı düzeltme işlemleri** — sırt sürekliliği; rota ölçümü gerekli olduğunu
  gösterirse L2'ye

---

## Adım adım görevler

### Görev 1: Pişirme betiği
- **EYLEM:** `Tools/terrain/bake_heightmap.py` — `l1_play.py` + `detail.py` zincirini tek
  komutta PNG-16'ya bağla.
- **UYGULA:** L0 npz oku → 26 km kırp → `divideTreeToMesh` (refine 30 m, poisson 45 m) →
  merkez 17.517 km'yi 4097²'ye rasterle → multifraktal + kısıtlı erozyon → `h/6189` ile
  16 bit'e nicemle → PNG kaydet.
- **GOTCHA:** Nicemleme `terrainHeight` 6189'a göre, `h.max()`'a göre DEĞİL. Maksimuma
  normalize edilirse dağın boyu tohumdan tohuma kayar ve `SCALE.md`'deki her şey bozulur.
- **DOĞRULA:** Geri okunan PNG ile kaynak dizi arasında en kötü fark < 0.1 m; zirve
  değeri tam 5709 m.

### Görev 2: `HeightmapImporter`
- **EYLEM:** `Assets/Editor/HeightmapImporter.cs`.
- **UYGULA:** `[MenuItem("To The Summit/Arazi/Yükseklik Haritasını Uygula", false, 12)]`.
  PNG-16 oku → `float[4097,4097]` → `data.heightmapResolution`, `data.size`,
  `transform.position` ayarla → `SetHeights` → `Flush`.
- **MİRRORLA:** `MountainGenerator.cs:66-70, 100-101` ve `DivideTreeImporter`'ın
  hata-yutmayan okuması.
- **GOTCHA:** Unity `SetHeights` dizisini `[z, x]` sırasıyla okuyor; PNG `[satır, sütun]`.
  L0'da bir kez eksen devrikliği yaşandı (silsile 90° dönük çıktı) — bu sefer içe aktarma
  sonrası zirvenin **merkezde** olduğu denetlenecek.
- **DOĞRULA:** `terrainData` yüksekliği 5709 m ve tepe noktası origin'de.

### Görev 3: Bootstrap'i bağla
- **EYLEM:** `MountainSceneBootstrap`'te `gen.Generate()` çağrısını içe aktarmayla değiştir.
- **GOTCHA:** Bootstrap `[InitializeOnLoad]`. Her derlemede pişirme çalışmamalı —
  yükseklik haritası zaten varsa **atlanmalı**, yoksa her kod değişikliğinde 4097²
  yeniden yükleniyor.
- **DOĞRULA:** Derleme sonrası kurulum sessiz; `Logs/tools.log`'da tek satır.

### Görev 4: Söküm
- **EYLEM:** `MountainGenerator`'dan üretim yolunu, `MountainSettings`'ten radyal koni ve
  ova alanlarını sil.
- **GOTCHA:** `MountainSettings.asset` serileştirilmiş; alan silinince Unity onları
  sessizce düşürür ama **`MountainTunerWindow` derlenmez**. Aynı adımda ele alınacak.
- **GOTCHA 2:** `Measure`, `ComputeSlopeStats`, `SlopeBand`, `AltitudeBandCount` KALIR —
  bootstrap'in raporu onlara bağlı.
- **DOĞRULA:** Derleme temiz, ölü kod yok, `grep -rn "heightProfile\|mountainRadius"`
  boş döner.

### Görev 5: Yüzey haritalarını yeniden pişir
- **EYLEM:** `SurfaceMapBaker`'ı yeni arazi üstünde çalıştır.
- **UYGULA:** Sıra: yükseklik haritası → `TerrainData` → yüzey/normal/ufuk/birikinti.
- **GOTCHA:** ~160 MB yerel yazma. `.gitignore` kapsamında, repoya girmiyor.
- **DOĞRULA:** Dört asset de yenilenmiş; sahnede arazi dokusu ve gölgesi doğru.

### Görev 6: Rota ve kamplar — ÖLÇ, TAŞIMA
- **EYLEM:** `MountainRoute.asset`'in 193 yol + 2804 kol + 3 kamp + 1 dükkân noktasının
  yeni arazide hangi kota düştüğünü ölç ve raporla.
- **GOTCHA:** Bu planda **taşınmıyorlar**. L3 sembolik çapa getirecek; şimdi taşımak iki
  kez iş demek.
- **DOĞRULA:** Rapor `Logs/tools.log`'da: kaç nokta suyun altında, kaç tanesi 30°'den dik
  zeminde, ova noktalarının kot dağılımı.

### Görev 7: Belgeler
- **EYLEM:** `.gitignore` gerekçesi, `SYSTEMS.md` (yeni bağ: yükseklik haritası kaynak),
  `SCALE.md` (L1 sayıları), `DECISIONS.md` (saklama kararı + "Erode kalıyor" kaydının
  düzeltmesi).
- **DOĞRULA:** Dördü de bu turda güncellendi.

---

## Doğrulama

### Pişirme
```bash
cd "D:/ME/game/to the summit" && python Tools/terrain/bake_heightmap.py --verify
```
BEKLENEN: PNG geri okunuyor, en kötü fark < 0.1 m, zirve 5709 m.

### Belirlenimlilik
```bash
cd "D:/ME/game/to the summit" && python Tools/terrain/bake_heightmap.py --out a.png && python Tools/terrain/bake_heightmap.py --out b.png && cmp a.png b.png && echo AYNI
```
BEKLENEN: `AYNI`.

### Elle doğrulama (kullanıcı, Unity'de)
- [ ] Derleme temiz
- [ ] `To The Summit → Arazi → Yükseklik Haritasını Uygula` çalışıyor, uyarı yok
- [ ] Sahne görünümünde dağ **koni değil**: sırtlar, vadiler, güneybatıya inen ova
- [ ] Play → spawn noktasında ova düz, bisikletle etek 10 dakika
- [ ] Zirveye bakınca tek baskın tepe, çevresinde alçak komşular
- [ ] **Eskiden:** her azimutta aynı eğim, ova radyal bir yelpaze

## Kabul ölçütleri

- [ ] Yükseklik haritası repoda, ~14 MB
- [ ] Türetilmiş varlıklar `.gitignore`'da ve gerekçesi güncel
- [ ] `MountainGenerator`'da üretim yolu yok, ölçüm kalmış
- [ ] Ölü kod yok; `heightProfile`/`mountainRadius` referansı kalmamış
- [ ] Yüzey haritaları yeni arazide pişmiş
- [ ] Rota noktaları ölçülmüş ve raporlanmış (taşınmamış)
- [ ] Dört belge aynı turda güncellenmiş

## Riskler

| risk | olasılık | etki | azaltma |
|---|---|---|---|
| Eksen devrikliği (PNG ↔ `SetHeights`) | **yüksek** | yüksek | L0'da bir kez yaşandı; Görev 2 denetimi zirvenin merkezde olduğunu doğruluyor |
| `MountainTunerWindow` derlenmez | yüksek | orta | Görev 4'te aynı adımda ele alınıyor |
| Bootstrap her derlemede yeniden yüklüyor | orta | orta | Görev 3 GOTCHA — harita varsa atla |
| Rota noktaları suyun altında/uçurumda | **yüksek** | düşük | Bu planda taşınmıyorlar; ölçülüp raporlanıyor |
| Repo her sürümde 14 MB büyüyor | kesin | düşük | Kabul edildi; alternatifi 165 MB |

## Notlar

**Ölçek bağımlılığı (Kural I):**

| sayı | dağın boyuna bağlı mı |
|---|---|
| Yükseklik haritası 4097² | Evet — `heightmapResolution` |
| Nicemleme ölçeği 6189 m | Evet — `terrainHeight` |
| Kırpma 26 km | **Hayır** — mesh üretiminin çalışma penceresi |
| `refineDistance` 30 m | **Hayır** — mesh üçgen boyu, gerçek metre |
| Poisson yarıçapı 45 m | **Hayır** — aynı |
| `R_infl` 400 m | **Hayır** — zirve koruma yarıçapı, gerçek metre |
| Gürültü genliği 52 m | **Hayır** — prominence tabanından türer |

**Ertelenen iki teknik madde** (`DECISIONS.md`'de kayıtlı, tetikleyicileriyle):

- **Gerçek plato çıkmıyor.** Sebep vadi oyma derinliği (`maxSlopeCoeff`), yoğunluk değil.
  Uzak bant planına ait — plato 120 km+ ötede ve oyun alanını etkilemiyor.
- **Düğüm kotlarında en kötü kayma 172 m**, prominence tavanı 65 m'nin üstünde. Kaynağı
  `R_infl` 400 m'nin dışında kalan bir düğüm. L1'de ölçülecek: oyun alanındaki düğümlerde
  de oluyor mu, yoksa yalnız uzakta mı? Oyun alanında değilse ertelenir.
