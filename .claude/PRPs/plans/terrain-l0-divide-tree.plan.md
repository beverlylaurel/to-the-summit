# Plan: L0 — Divide Tree sentezi

## Özet

Arazi üretiminin **iskelet** katmanı. ~540 km'lik bir bölge için zirve/boyun/sırt grafiği
(`Divide Tree`) üretilir ve **veri olarak repoya yazılır**. Yükseklik haritası (L1),
işaretler (L2) ve pişirme AYRI planlardır — bu planda yok.

Çıktı bir arazi değil, bir **grafik**: içerik (kamp, konak, mağara, anıt, zirve modülü)
bu grafiğin düğümlerine çapalanacak, dağ yeniden üretilse bile yerinde kalacak.

## Kullanıcı hikâyesi

Bir **tasarımcı** olarak, dağın ve çevresinin makro yapısını (hangi zirve nerede, hangi
sırt nereye bağlanıyor, hangi boyun kamp olur) **kararlı kimliklerle** görmek istiyorum;
böylece içerik yerleştirmesi arazinin yeniden üretilmesinden etkilenmez.

## Problem → Çözüm

**Şu an:** `MountainGenerator` radyal koni üretiyor — yükseklik yalnız merkeze uzaklığın
fonksiyonu. Ölçüldü: üst 2 km **her azimutta** 48–57°, yürünecek hat yok; ova eteğin
dışında radyal bir yelpaze; ufukta gezegen boşluğu.

**Olacak:** orometrik istatistiklere uyan bir zirve/boyun grafiği. Sırt (25–30°, rota
oradan geçer) ile duvar (50–60°, bakılır) ayrımı yapıdan doğar. Ana zirve 5709 m'ye
çakılır. Yön yön karakter (dağlık / tepelik / ova / plato) üretimin girdisidir.

## Üstveri

- **Karmaşıklık:** XL (yeni alt sistem + dış araç zinciri + mevcut üreticinin sökümü)
- **Kaynak spec:** `C:\Users\musta\Desktop\tts\specs\terrain\terrain-generation-spec.md`
- **Faz:** L0 (4 fazın ilki)
- **Tahmini dosya:** 6 yeni, 3 silinen, 4 belge

---

## ⛔ İKİ KAPI — bunlar cevaplanmadan uygulama başlamaz

### KAPI 1: Kirmse veritabanı indirilmeli (kullanıcı izni gerekir)

`data/alliso-sorted.txt` ve `data/prominence-p100.txt` **sahte dosya** — içlerinde yalnız
Google Drive bağlantısı var (ölçüldü: 1 ve 2 satır). Gerçek veri Andrew Kirmse'nin
küresel zirve veritabanı.

`Synthesis.ipynb` hücre 7 onu böyle kullanıyor:

```python
df = pd.read_csv(peaksFile)          # <- Kirmse CSV
filterHWidth = [km2deg(filterRadius), km2deg(filterRadius, filterCoords[0])]
df = df[np.logical_and(filat, filon)]  # bölgeyi lat/lon + yarıçapla kes
```

İstatistikler (prominence, dominance, isolation dağılımları) bu tablodan çıkıyor.

**Seçenekler:**

| yol | ne gerekir | risk |
|---|---|---|
| A. Kirmse CSV'sini indir | kullanıcının açık izni, Google Drive'dan büyük dosya | dış indirme |
| B. `data/dems/*.txt` kullan | 19 bölgenin **hazır divide tree**'si repoda mevcut (ör. `himalaya-everest.txt`) | dağılımlar tek bölgeden çıkar, çeşitlilik düşer |

**B seçeneği ölçüldü ve muhtemelen yeterli:** `data/dems/alps-montblanc.txt` "Peaks 818"
ile başlıyor ve `utils/divtree_reader.py`'nin okuduğu biçimde. Prominence bu ağaçtan
türetilebiliyor (`utils/metrics.py`). Yani küresel veritabanı **analiz** notebook'ları
için; sentez için tek bölgenin ağacı yetebilir.

**Karar kullanıcınındır.** Ben indirme yapamam.

### KAPI 2: Hangi silsilenin karakteri

`Synthesis.ipynb` hücre 5'te hazır ön ayarlar var, aralarında:

```python
#regionName, filterCoords = 'himalaya', [27.8575, 86.8267]  # himalaya: everest
#regionName, filterCoords = 'karakoram', [35.8283, 76.3608] # karakoram
#regionName, filterCoords = 'alps', [45.8325, 7.0]          # mont blanc
```

Repoda DEM'i **hazır** olanlar: `himalaya-everest`, `himalaya-annapurna`, `karakoram`,
`alps-montblanc`, `alps-ecrins`, `alps-dolomites`, `andes-*`, `rockies`, `patagonia`,
`norway`, `pyrenees`, `colorado`, `appalachians`, `alaska`, `gobi`, `sahara`, `highlands`,
`yangshuo`.

**Öneri: `himalaya-everest`.** Gerekçe: oyunun kurgusu Everest ölçeğinde (zirve 5709 m,
ölümcül irtifa, konak-kamp zinciri) ve o bölgenin orometrisi tam olarak "bir baskın dev +
çevresinde kademeli daha küçük zirveler + bir yanda yüksek plato (Tibet)" karakterini
taşıyor — `DECISIONS.md`'deki "her yer dağ olmayacak" kuralına doğal uyum.

Kullanıcı başka bir karakter isterse (Karakurum daha keskin ve buzul ağırlıklı, Alpler
daha yeşil ve alçak) tek satır değişir.

---

## Zorunlu okuma

| öncelik | dosya | satır | neden |
|---|---|---|---|
| P0 | `specs/terrain/terrain-generation-spec.md` | §5.3 (384–520), §5.6 (522–560) | Divide Tree sentezi ve DEM'e çevrim; L0 sınırı §5.6'nın **başı** |
| P0 | `orometry-terrains-master/synthesis/divtree_synthesis.py` | 487–694 | `synthDivideTree` ana döngü — bizim boru hattımızın kalbi |
| P0 | `orometry-terrains-master/utils/divtree_reader.py` | tamamı (91) | **Dosya biçimi** — L0 serileştirmesi buradan türeyecek |
| P0 | `DECISIONS.md` | "Arazi mimarisi", "Arazi ölçeği" | Dört katman, sembolik çapa, üç bant, her yer dağ olmayacak |
| P0 | `SCALE.md` | "Arazi yeniden üretimi: neyin kırılacağı" | Kırılan üç sayı |
| P1 | `orometry-terrains-master/Synthesis.ipynb` | hücre 5, 7, 19, 32 | Bölge seçimi, filtreleme, zirve sayısı hesabı, çıktı yazımı |
| P1 | `Assets/Scripts/Terrain/MountainSettings.cs` | 1–14 | ScriptableObject + `[Header]`/`[Tooltip]` kalıbı |
| P1 | `Assets/Editor/ToolLog.cs` | 1–25 | **Araç çıktısı dosyaya, konsola değil** |
| P2 | `Assets/Scripts/Terrain/MountainGenerator.cs` | tamamı | Sökülecek olan; bağlantıları görmek için |
| P2 | `Assets/Editor/SurfaceMapBaker.cs` | 84–124, 185 | `AssetDatabase` ile asset yazma kalıbı |
| P2 | `DESIGN.md` | tamamı | Ton kararları — sırt/duvar ayrımının oynanış gerekçesi |

## Dış kaynaklar

| konu | kaynak | çıkarım |
|---|---|---|
| Divide Tree tanımı | `[Helman 2005]`, spec §5.1 | Zirve↔key saddle **bijektif**; grafik değil **ağaç** |
| Sentez döngüsü | `[Argudo 2019]` §5.3, kod 487–694 | Prominence **grubu başına** ayrı geçiş; her geçiş öncekinin olasılık haritasını günceller |
| Optimal transport | `ot` (POT) kütüphanesi | Dağılım eşleme (`matchProminences`, `matchDominances`) OT kullanıyor — C#'a portu haftalar |
| Zirve yoğunluğu | `Synthesis.ipynb` hücre 19 | `totalNumPeaks = densityFactor × (terrainKm/(2×filterRadius))² × gerçekZirveSayısı` |

**KEY_INSIGHT:** `synthDivideTree`'nin imzasında iki parametre bizim iki tasarım kararımızın
doğrudan karşılığı — uydurmaya gerek yok:

```python
def synthDivideTree(distributions, distribsPromBin, distribsPromAcc, promGroups, stepPeaks,
                    probMap, probMapSaddles, elevMap, fixedData, synthParams):
    fixedPeaks = fixedData['fixedPeaks']   # <- ana zirveyi 5709 m'ye ÇAKMAK
    ...
    # probMap  <- YÖN YÖN KARAKTER (nerede zirve çıkabilir)
    # elevMap  <- kaba yükseklik kılavuzu (plato / ova / dağlık)
```

**APPLIES_TO:** Görev 4 ve 5.
**GOTCHA:** `probMap` sıfır olan yerde zirve **hiç** çıkmaz; ova ve plato yönleri buradan
kurulur, sonradan filtreyle değil.

---

## Mimarî karar: PORT DEĞİL, ÇEVRİM

**Yaklaşım:** Python referansı **olduğu gibi** çalıştırılır (offline, Claude tarafından),
çıktısı repoya yazılır, Unity yalnız **okur**.

**Gerekçe:**

| ölçüt | port (C#) | çevrim (Python offline) |
|---|---|---|
| Bağımlılıklar | Delaunay + MST + KD-tree + EDT + medial axis + **optimal transport** elle yazılacak | hazır, yazarların kendi kodu |
| Doğruluk riski | yüksek — OT'nin yanlış portu sessizce yanlış dağılım verir | sıfır, referans implementasyon |
| Çalışma sıklığı | — | **dağ bir kez üretiliyor** (karar: pişmiş içerik) |
| Determinizm | tohum yönetimi gerekir | **çıktı commit'lenir**, determinizm konusu bile değil |
| Kullanıcı yükü | yok | **yok** — Python'u Claude çalıştırır, kullanıcı Unity'de yalnız sonucu görür |

`CLAUDE.md` "kullanıcı yalnız Unity'de tıklar" kuralı **ihlal edilmiyor**: komutları ben
çalıştırıyorum.

**Değerlendirilen ve reddedilen:**
- **Tam C# portu** — OT ve medial axis yüzünden orantısız maliyet; üstelik çalışma zamanında
  hiç gerekmiyor.
- **Python'u Unity'den çağırmak** — çalışma zamanı bağımlılığı yaratır, pişmiş içerik
  kararına aykırı.
- **Kendi basitleştirilmiş algoritmamızı yazmak** — `CLAUDE.md`: "Repo'nun üstüne kendi
  terimimiz eklenmez."

**Ortam ölçüldü:** Python 3.11.9 ✓, numpy 2.4.6 ✓, PIL 12.3.0 ✓ — `scipy`, `scikit-image`,
`POT` **eksik**, kurulacak. Bunlar geliştirme aracı; Unity projesine girmiyorlar.

---

## Uyulacak kalıplar

### AYAR = ScriptableObject
```csharp
// KAYNAK: Assets/Scripts/Terrain/MountainSettings.cs:1-14
[CreateAssetMenu(fileName = "MountainSettings", menuName = "To The Summit/Mountain Settings")]
public class MountainSettings : ScriptableObject
{
    [Header("Boyut")]
    [Tooltip("2^n + 1 olmalı: 513, 1025, 2049, 4097 (Unity maksimumu).")]
    public int heightmapResolution = 4097;
```

### ARAÇ ÇIKTISI DOSYAYA
```csharp
// KAYNAK: Assets/Editor/ToolLog.cs:5-10
/// ARAÇ ÇIKTISI DOSYAYA GİDER, KONSOLA DEĞİL.
/// Yeni araç yazarken kural: bilgi `ToolLog.Write`, sorun `Debug.LogWarning` ya da
/// `Debug.LogError`. Konsolda görünen her satır bakılması gereken bir şey olmalı.
```

### ASSET YAZIMI
```csharp
// KAYNAK: Assets/Editor/SurfaceMapBaker.cs:185, 120
AssetDatabase.CreateAsset(texture, MapPath);
public static Texture2D Load() => AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
```

### HATA YUTULMAZ
```csharp
// KAYNAK: Assets/Scripts/Debug/DebugMenu.cs:117 (projede TEK bir try bloğu bile yok)
throw new InvalidOperationException($"{nameof(DebugMenu)}: bağımlılıklar atanmadı.");
```

### MENÜ
```csharp
// KAYNAK: Assets/Editor/CloudMapGenerator.cs:49
[MenuItem("To The Summit/Bulut/Hava Haritasını Üret", false, 40)]
```

### TEST KALIBI — YOK
Projede test paketi yok, tek bir `try` bloğu bile yok. `prp-plan`'ın "unit test yaz /
test paketini çalıştır" adımları bu projede **derleme kontrolü + kullanıcının Unity'de
doğrulaması**na dönüşür (`CLAUDE.md` skill kuralı: proje kuralı kazanır).

---

## Değişecek dosyalar

| dosya | işlem | gerekçe |
|---|---|---|
| `Tools/terrain/requirements.txt` | OLUŞTUR | scipy, scikit-image, POT, pandas sürümleri sabitlensin — üretim tekrarlanabilir olsun |
| `Tools/terrain/synth_l0.py` | OLUŞTUR | Referansı çağıran ince sarmalayıcı: bölge seç, probMap/elevMap kur, fixedPeaks ver, ağacı yaz |
| `Tools/terrain/region_profile.py` | OLUŞTUR | Yön yön karakter maskesi (dağlık/tepelik/ova/plato) → `probMap` + `elevMap` |
| `Assets/Terrain/DivideTree.asset` | OLUŞTUR (çıktı) | L0 verisi — Unity tarafının okuduğu tek şey |
| `Assets/Scripts/Terrain/DivideTree.cs` | OLUŞTUR | ScriptableObject: zirveler, boyunlar, kenarlar, **kararlı kimlikler** |
| `Assets/Editor/DivideTreeImporter.cs` | OLUŞTUR | Python çıktısını (`.txt`) `DivideTree.asset`'e çeviren editör aracı |
| `Assets/Editor/DivideTreeWindow.cs` | OLUŞTUR | Görsel doğrulama: ağacı tepeden çizer (repo'daki `PlotDivtrees.ipynb` karşılığı) |
| `SYSTEMS.md` | GÜNCELLE | Yeni bağ: L0 → içerik çapası |
| `SCALE.md` | GÜNCELLE | L0'ın ölçek bağımlı sayıları |
| `DECISIONS.md` | GÜNCELLE | Kapı 1 ve 2'nin cevapları kayda geçer |
| `Assets/Scripts/Terrain/MountainGenerator.cs` | **DOKUNULMAZ** | L1'e kadar çalışmaya devam ediyor; söküm L1 planında |

## Bu planda YOK

- **L1** — DEM sentezi (`divtree_to_dem.py`), erozyon, yükseklik haritası
- **L2** — eğim kuşağı, bakı, korunaklılık, çığ maruziyeti maskeleri
- **L3** — kamp/konak/mağara/anıt yerleştirmesi
- **Uzak bantlar** — 18–60 km ve 60–300 km mesh'leri, eğrilik
- **`MountainGenerator` sökümü** — L1 planında; şimdi silinirse arazi hiç üretilemez
- **`MountainRoute.asset`'in taşınması** — yeni arazi olmadan rota yeniden konumlanamaz.
  L0'da yalnız **hangi düğümlere çapalanacağı** belirlenir, taşıma L2'de.
- **Kamera far clip / haze / cloudMapSize düzeltmeleri** — uzak bant planında
- **Çapalı düzeltme işlemleri** — sırt sürekliliği; L1 çıktısı olmadan ölçülemez

---

## Adım adım görevler

### Görev 1: Kapı 1 ve 2'yi kapat
- **EYLEM:** Kullanıcıya iki soruyu sor, cevapları `DECISIONS.md`'ye yaz.
- **UYGULA:** (a) Kirmse CSV indirilecek mi yoksa `data/dems/himalaya-everest.txt`
  yeterli mi? (b) Hangi bölge karakteri?
- **GOTCHA:** İndirme kullanıcının açık izni olmadan yapılmaz.
- **DOĞRULA:** `DECISIONS.md`'de iki karar da gerekçesiyle yazılı.

### Görev 2: Python ortamı
- **EYLEM:** `Tools/terrain/requirements.txt` yaz, kur.
- **UYGULA:** `numpy`, `scipy`, `scikit-image`, `POT`, `pandas`, `Pillow` — sürümler sabit.
- **GOTCHA:** Bunlar **Unity projesine girmiyor**; `Tools/` klasörü `Assets/` dışında.
  `CLAUDE.md` "gereksiz paket kurulmaz" kuralı Unity paketleri içindir.
- **DOĞRULA:** `python -c "import scipy, skimage, ot, pandas"` hatasız.

### Görev 3: Referansı olduğu gibi koştur (temel çizgi)
- **EYLEM:** `Synthesis.ipynb`'in akışını script'e çevir, **hiçbir parametreyi
  değiştirmeden** çalıştır.
- **UYGULA:** Seçilen bölge → dağılımlar → `synthDivideTree` → çıktı `.txt`.
- **GOTCHA:** Önce **referansın kendisi** çalışmalı. Kendi parametrelerimizi bozuk bir
  temelin üstüne koyarsak neyin bozduğunu ayıramayız (`CLAUDE.md`: ölçmeden düzeltme yok).
- **DOĞRULA:** Çıktı `.txt`, `utils/divtree_reader.py` ile geri okunuyor; zirve sayısı
  hücre 19'un hesabıyla uyuşuyor.

### Görev 4: Bölge profili — yön yön karakter
- **EYLEM:** `region_profile.py` — `probMap`, `probMapSaddles`, `elevMap` üret.
- **UYGULA:** 540 km kare üzerinde yön/uzaklık tabanlı maske: bir sektör dağlık (yüksek
  olasılık), bir sektör plato (düşük olasılık + yüksek `elevMap`), bir sektör ova (sıfır
  olasılık + düşük `elevMap`), bir sektör tepelik (orta).
- **MİRRORLA:** `Synthesis.ipynb` hücre 19'daki `densityFactor` hesabı; maske alanı zirve
  sayısını doğrudan etkiliyor.
- **GOTCHA:** `probMap == 0` olan yerde zirve **hiç** çıkmaz — ova ve plato buradan kurulur,
  sonradan silinerek değil.
- **DOĞRULA:** Maske PNG olarak yazılır, gözle bakılır: dört karakter bölgesi ayırt edilir.

### Görev 5: Ana zirveyi çakmak
- **EYLEM:** `fixedPeaks` ile merkeze 5709 m'lik zirveyi koy.
- **UYGULA:** spec s.792 "sabit zirveler (Everest'in kendisini buraya koy)".
  `fixedData['fixedPeaks']` = `[[x, y, 5709]]`, merkez koordinatta.
- **GOTCHA:** `globalMaxElev` varsayılanı 9000; bizim tavanımız `terrainHeight` 6189.
  Uyuşmazsa sentez daha yüksek komşular üretmeye çalışır.
- **DOĞRULA:** Çıktıda en yüksek zirve **tam 5709 m** ve merkezde; ikinci zirve ondan
  düşük.

### Görev 6: `DivideTree` ScriptableObject
- **EYLEM:** `Assets/Scripts/Terrain/DivideTree.cs` yaz.
- **UYGULA:** Zirve dizisi (kimlik, konum, kot), boyun dizisi (kimlik, konum, kot, bağladığı
  iki zirve), kenar listesi. **Kimlik = üretimdeki sıra numarası**, konumdan türetilmez.
- **MİRRORLA:** `MountainSettings.cs:1-14` — `[CreateAssetMenu]`, `[Header]`, `[Tooltip]`.
- **GOTCHA:** `divtree_reader.py` okurken **yeniden sıralıyor** (`peakReorder`,
  `saddleReorder`). Kimlik kararlılığı için yeniden sıralamadan **önceki** indeks
  saklanmalı, yoksa kırpma sınırı değişince tüm kimlikler kayar ve içerik çapaları kopar.
- **DOĞRULA:** Derleme temiz; asset Inspector'da açılıyor.

### Görev 7: İçe aktarma aracı
- **EYLEM:** `Assets/Editor/DivideTreeImporter.cs`.
- **UYGULA:** `[MenuItem("To The Summit/Arazi/Divide Tree'yi İçe Aktar", false, 10)]`,
  `.txt` oku → `DivideTree.asset` yaz.
- **MİRRORLA:** `SurfaceMapBaker.cs:185` (`AssetDatabase.CreateAsset`), `ToolLog.Write`.
- **GOTCHA:** Yükseklikler dosyada **feet**; `divtree_reader.py` `feet2m` uyguluyor. Bizim
  de uygulamamız şart, yoksa 5709 m yerine 1740 m çıkar.
- **DOĞRULA:** İçe aktarılan zirve sayısı Python çıktısındakiyle **birebir** aynı.

### Görev 8: Görsel doğrulama penceresi
- **EYLEM:** `Assets/Editor/DivideTreeWindow.cs`.
- **UYGULA:** Tepeden görünüm: zirveler nokta (boyut = prominence), boyunlar küçük kare,
  sırtlar çizgi. Oyun alanının 17.5 km'lik karesi çerçeve olarak çizilir.
- **MİRRORLA:** `PlotDivtrees.ipynb`'in yaptığı çizim; proje tarafında `LookTunerWindow.cs`
  pencere kalıbı.
- **GOTCHA:** Bu **teşhis aracı** — `CLAUDE.md`'ye göre onaya tabi değil ama işi bitince
  silinmez, çünkü L1/L2/L3 boyunca lazım olacak.
- **DOĞRULA:** Kullanıcı pencereyi açıp şunları görebiliyor: (1) merkezde tek baskın zirve,
  (2) ondan çıkan sırt hatları, (3) bir yönde ova/plato boşluğu, (4) 17.5 km çerçevesi
  içinde yeterli sırt.

### Görev 9: Belgeler
- **EYLEM:** `SYSTEMS.md`, `SCALE.md`, `DECISIONS.md` güncelle. **Ayrı adım değil, görev.**
- **UYGULA:** `SYSTEMS.md`'ye yeni bağ (L0 → içerik çapası, kimlik kararlılığı kuralı);
  `SCALE.md`'ye L0'ın ölçek bağımlı sayıları (bölge boyu 540 km, zirve sayısı, prominence
  eşiği — hangisi dağın boyuna bağlı hangisi değil); `DECISIONS.md`'ye Kapı 1/2 cevapları.
- **DOĞRULA:** Üç belgede de bu turun kararları yazılı; hiçbiri "sonra".

---

## Doğrulama

### Derleme (test paketi yerine)
```bash
cd "D:/ME/game/to the summit" && ls Logs/tools.log
```
BEKLENEN: Unity derlemesi hatasız, `Logs/tools.log` içinde içe aktarma özeti.

### Python zinciri
```bash
cd "D:/ME/game/to the summit/Tools/terrain" && python synth_l0.py --verify
```
BEKLENEN: Üretilen ağaç geri okunuyor; zirve/boyun sayıları tutarlı; en yüksek zirve
5709 m ve merkezde.

### Belirlenimlilik
```bash
cd "D:/ME/game/to the summit/Tools/terrain" && python synth_l0.py --seed 36044 --out a.txt && python synth_l0.py --seed 36044 --out b.txt && diff a.txt b.txt && echo AYNI
```
BEKLENEN: `AYNI`. Aynı tohum → bayt bayt aynı çıktı (co-op şartı).

### Elle doğrulama (kullanıcı, Unity'de)
- [ ] `To The Summit/Arazi/Divide Tree'yi İçe Aktar` çalışıyor, konsol temiz
- [ ] `DivideTreeWindow` açılıyor
- [ ] Merkezde **tek baskın zirve** var, çevresindekiler belirgin şekilde daha alçak
- [ ] En az bir yönde **ova/plato** boşluğu görünüyor (her yer zirve değil)
- [ ] 17.5 km çerçevesi içinde zirveden ovaya **sırt hattı** izlenebiliyor

## Kabul ölçütleri

- [ ] İki kapı da `DECISIONS.md`'de cevaplı
- [ ] Python zinciri aynı tohumla aynı çıktıyı veriyor
- [ ] `DivideTree.asset` repoda, Unity'den okunuyor
- [ ] Düğüm kimlikleri kararlı (aynı tohum → aynı kimlikler)
- [ ] Doğrulama penceresi dört maddeyi de gösteriyor
- [ ] Üç belge aynı turda güncellendi
- [ ] Ölü kod yok; `MountainGenerator` **bilerek** duruyor ve gerekçesi yazılı

## Riskler

| risk | olasılık | etki | azaltma |
|---|---|---|---|
| Kirmse CSV indirilemez | orta | yüksek | B seçeneği: `data/dems/himalaya-everest.txt` — hazır ağaç, repoda |
| `POT` kurulmaz (derleme gerektirir) | düşük | yüksek | Tekerlek (wheel) yoksa `pip install pot` yerine conda; son çare OT'siz mod (dağılım eşleme atlanır, kalite düşer) |
| Kimlikler kırpma sınırıyla kayar | **yüksek** | **yüksek** | Görev 6 GOTCHA — yeniden sıralama öncesi indeks saklanır. Bu, tüm L3 çapa mimarisinin dayandığı nokta |
| 540 km'de zirve sayısı patlar | orta | orta | Hücre 19 formülü zirve sayısını alanla **kare** ölçekliyor: 17.5→540 km, alan 950 kat. Prominence eşiği yükseltilerek sınırlanır |
| Sentez saatlerce sürer | orta | düşük | Bir kez çalışıyor; pişmiş içerik |
| `globalMaxElev` 9000 ile `terrainHeight` 6189 çelişir | yüksek | orta | Görev 5 GOTCHA |

## Notlar

**Ölçek bağımlılığı (Kural G):**

| sayı | dağın boyuna bağlı mı |
|---|---|
| Bölge kenarı 540 km | **Hayır** — ufuk mesafesinden türüyor (`sqrt(2Rh)`), gezegen yarıçapına bağlı |
| Oyun alanı 17.5 km | Evet — `terrainSize` |
| Ana zirve 5709 m | Evet — `terrainHeight` |
| Zirve sayısı | **Hayır** — gerçek bölgenin yoğunluğu × alan |
| Prominence eşiği (`promEpsilon` 30 m) | **Hayır** — gerçek metre, orometrik tanım |
| `filterRadius` | Hayır — analiz penceresi, gerçek dünyada |

**Neden L0 tek başına bir faz:** L1 (DEM) L0'sız yazılamaz, ama L0 L1'siz **doğrulanabilir**
— ağaç tepeden çizilince doğru olup olmadığı görülür. Bu, dört fazın içinde tek başına
anlamlı çıktı veren ilk halka. Yanlışsa burada yakalanır; L1'e taşınırsa 950 kat veri
üretildikten sonra yakalanır.
