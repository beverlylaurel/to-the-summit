# Ölçek bağımlılıkları

Dağın boyu (`terrainSize`, `terrainHeight`) değiştiğinde neyin kendiliğinden kaydığı,
neyin elle düzeltilmesi gerektiği. **Dağ büyütülüp küçültülmeden önce baştan sona okunur.**

Yeni bir özellik eklerken sorulacak tek soru: *bu sayı dağın boyuna bağlı mı?* Cevap evet
ise buraya yazılır — hangi kategoriye girdiği ve büyüdüğünde ne olacağıyla birlikte.

Üç kategori var:

- **Kendiliğinden ölçeklenir** — orana bağlı, dokunmaya gerek yok
- **Bilerek mutlak** — fiziksel bir metre, dağın boyunu bilmemeli; ölçeklenirse yanlış olur
- **Elle bakılacak** — mutlak ama dağın boyuna göre anlamı değişir

---

## Kendiliğinden ölçeklenir

| ne | nasıl |
|---|---|
| Arazi konumu | `transform.position = -terrainSize / 2` — zirve origin'de kalır |
| Kuşak sınırları | `rainCeiling = zemin + yükseklik × RainShare`, `snowFloor` üstüne sulu kar payı |
| Tipi kuşağı | `max(snowFloor + 200, zirve − 1000)` — zirveden türer |
| Kar profili bant aralığı | zemin→zirve, 128 banda bölünür |
| `_TerrainOrigin`, `_TerrainSize` | `terrainData`'dan okunur |
| `_TerrainHeightArea` | `terrainData`'dan okunur (köşe, genişlik, yükseklik ölçeği) |
| Kamera far clip | `terrainSize × FarClipFactor` |
| Gürültü oktav sayısı | `terrainSize / (baseFrequency × minWavelength)` oranından |
| Eğim istatistikleri | hücre boyu `terrainSize / (res − 1)` üzerinden |

## Bilerek mutlak — ölçeklenmemeli

Bunlar gerçek dünyanın metreleri. Dağ iki katına çıkınca donma seviyesi iki katına
çıkmaz.

| ne | değer | neden mutlak |
|---|---|---|
| `stormCooling` / `daytimeWarming` | 3.25 / 1.63 °C | Donma seviyesini oynatan şey MESAFE değil SICAKLIK. Karşılığı `lapseRate` (6.5 °C/km) üzerinden çıkıyor: 500 m ve 251 m. Dağ büyüyünce sıcaklık farkı değişmez |
| `permanentSnowRise` | 400 m | Denge çizgisinin donma seviyesi üstündeki payı |
| `permanentSnowBand` | 350 m | Çizginin yumuşama genişliği |
| `snowlineSunLift` / `GullyDrop` / `Ragged` | 200 / 150 / 120 m | Bakı, oluk ve düzensizliğin kar çizgisini oynatma mesafesi |
| Sis yarı-yükseklikleri, inversiyon kotu | — | Atmosferin kendi yapısı |
| Bulut tabanı ve tavanı | — | Aynı |
| Gezegen yarıçapı | 6 360 km | Dünya |
| Sis hacmi menzili / dilim | 0.5 → 1000 m / 64 | Froxel hacmi gerçek metrelerde; dağ büyüyünce kameranın önündeki hava kalınlaşmaz. Dilim sayısı da sabit — üstel dağılım yakın alanı korur |
| `SunIntensity` / `MoonIntensity` | 3.030782 / 0.0199 | Gök cisimlerinin aydınlatması; dağ büyüyünce güneş güçlenmez. Oran 8,6 durak — gerçeğin (19 durak) altında ve BİLEREK, gerekçe `DECISIONS.md` |
| `moraineHeight` / `moraineSpacing` | 20 m / 420 m | Buzul moreni gerçek boyutu; dağ büyüyünce moren büyümez |
| `channelDepth` | 14 m | Dere yatağı derinliği |
| `hummockHeight` | 8 m | Tümsek yüksekliği |
| `snowDisplaceMax` | 3.2 m | Birikintinin gerçek yüksekliği; dağ büyüyünce kar yığını büyümez. Fiili tavan 8.0 m: birikinti alanı ×1.25, arazi ağırlığı ×2.0 |
| `snowDisplaceStart` | 0.18 m | Geometriye geçme eşiği; insan adımının karşılığı |
| Birikinti gövdesi | 45 × 16 m | Rüzgârın oluşturduğu yığının gerçek boyu |

**Kural:** bunlardan biri dağın boyuna oranlanırsa atmosfer dağın büyüklüğüne göre farklı
fizik uygular ve tutarlılık zinciri kopar (`SYSTEMS.md`).

## Elle bakılacak

Mutlak sayılar ama dağın boyuna göre anlamları değişiyor. Dağ büyürse bunlar gözden
geçirilir.

| ne | şu an | dağ büyürse |
|---|---|---|
| `heightmapResolution` 4097 | 4.28 m/örnek | Metre başına örnek seyrelir; yakın plan kabalaşır |
| `SurfaceMapBaker.MapResolution` 1024 | 17.1 m/texel | Aynı |
| `ConcavityRadius` 6 texel | 103 m | Eğrilik ölçeği: birikintinin gördüğü arazi dalgası. Dağ büyürse metre karşılığı büyür |
| `forelandFanDrop` 60 m | eteğin dışı, ~4 km | Ovanın toplam alçalması; harita büyürse eğim azalır, birlikte bakılmalı |
| `NormalResolution` 2048 | 8.6 m/texel | Aynı |
| `HeightResolution` 1024 | 17 m/texel | Kret tespiti körelir: sürüklenen kar sırtlardan fışkırıyor, keskin sırt texel'den küçükse hiç görünmez |
| `HorizonResolution` 1024 | 17.1 m/texel | Arazi gölgesi kabalaşır |
| `TerrainSurface.Bands` 128 | 43 m/bant | Kar sınırının hareketi kaba adımlarla okunur |
| `snowTessNear` / `snowTessFar` 35 / 80 m | bölünme menzili | Menzil mutlak; dağ büyüyünce birikinti aynı mesafede çözülür ama daha az yer kaplar |
| `openingRise` 400 m | zeminin ilk %7'si | Oransal olarak daralır; açılış kuşağı kaybolabilir |
| `descentDeadband` 250 m | — | İniş ölü bandı oransal daralır |
| Kar çizgisi düzensizlik dalga boyu | `worldPos × 0.0016` ≈ 625 m | Dağa göre incelir, çizgi daha "dişli" okunur |
| `VolumetricClouds.cloudMapSize` 48 000 m | dağın ~2.7 katı | Hava haritası dağı kapsamalı; dağ büyürse birlikte büyümeli. 512 texel → 94 m/texel |
| `VolumetricClouds.shapeScale` 20 | bulut şekli dünyada 100 000 / 20 = **5 km** periyotta tekrarlıyor | Bulut kümesinin boyu. Dağ büyürse bulutlar orantısız küçük kalır; periyot dağın enine göre seçilmeli |
| `VolumetricClouds.bottomAltitude` 1200 m / `altitudeRange` 2000 m | katman 1200–3200 m, zirve 5709 m | Katman MUTLAK kotta (`localClouds` açık). Zirvenin katmanın üstünde kalması bilinçli: tırmanırken bulut denizini aşmak görülüyor. Dağ küçülürse zirve katmanın içinde kalır, büyürse aradaki fark açılır — ikisinde de birlikte bakılmalı |
| `maxHazeDistance` 60 000 m | — | Görüş menzili dağı görebilmeli |
| Bootstrap `SpawnPoint()` | eteğin dışı | Konum dağın boyundan türüyor, kontrol edilmeli |

---

## Arazi yeniden üretimi: neyin kırılacağı (2026-08-17)

Karar `DECISIONS.md` → "Arazi ölçeği". Değişen **boy değil yapı**: radyal koni yerine
Divide Tree, artı 300 km'ye uzanan üç bantlı mesafe temsili.

**`terrainSize` ve `terrainHeight` DEĞİŞMİYOR** — oyun alanı 17.5 km, zirve ~5709 m
kalıyor. Bu yüzden yukarıdaki üç tablonun neredeyse tamamı risk altında değil. Kırılanlar
tek bir yerde toplanıyor: **oyun alanına göre ölçeklenmiş ama artık 300 km görmesi gereken
şeyler.**

| ne | şu an | gereken | durum |
|---|---|---|---|
| **Kamera far clip** | `terrainSize × FarClipFactor` = **52.5 km** | ≥ 300 km | **KIRILIR** — uzak bant kırpma düzleminin ötesinde, hiç çizilmez |
| **`maxHazeDistance`** | 55 000 m | ~300 km | **KIRILIR** — 55 km'de pus doyuma gidiyor, uzak bant tek renk lapa çıkar |
| **`cloudMapSize`** | 40 000 m | görünen bölgeyi kapsamalı | **KIRILIR** — bulutlar uzak dağların üstünde biter, gökyüzü ortadan kesilir |
| `MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE` | 200 000 m | 300 km | bakılacak — ufkun %74'ü |
| Ufuk haritası (`HorizonResolution` 1024, 17.5 km) | oyun alanı | 300 km | **YENİ İŞ** — 100 km ötedeki dağlar şafakta güneşi kapatmalı; şu an modellenmiyor |
| Bulut gölge mesafesi 8 000 m | oyun alanı | — | sorun yok, yerel |

**Derinlik hassasiyeti:** far clip 52.5 → 300 km, yani 5.7 kat. Ters-Z ile float derinlik
bunu rahat taşıyor; asıl belirleyici yakın düzlem. Yine de uzak bant geldiğinde z-savaşı
kontrol edilir.

**Şekil parametreleri gereksizleşiyor.** `MountainSettings` içindeki radyal koni alanları
(`heightProfile`, `mountainRadius`, `peakSpread`, `ridgeInfluence`, teras ve erozyon
grubu) yerlerini Argudo parametrelerine bırakacak. `forelandFanDrop` 60 m de öyle — ova
artık radyal bir yelpaze değil, üretilen bölgenin parçası.

**Değişmeyenler (boy sabit kaldığı için):** kuşak sınırları, tipi kuşağı, kar profili
bantları, yüzey haritaları, `ConcavityRadius`, `snowTess*`, `TerrainSurface.Bands`,
"bilerek mutlak" tablosunun tamamı.

## L0 (Divide Tree) sayıları — 2026-08-17

Üretim `Tools/terrain/`, çıktı `Assets/Terrain/DivideTree.txt`.

| ne | değer | dağın boyuna bağlı mı |
|---|---|---|
| Bölge kenarı | 540 km | **Hayır** — ufuk mesafesinden (`sqrt(2Rh)`), gezegen yarıçapına bağlı |
| Oyun alanı | 17 517 m | **Evet** — `terrainSize` |
| Zirve kotu | 5 709 m | **Evet** — `terrainHeight` |
| Rakip tavanı | 5 500 m | **Evet** — zirveden türer, 209 m altında |
| Prominence tabanı | 100 m | **Hayır** — gerçek metre, orometrik tanım |
| Analiz kutusu yarıçapı | 120 km | Hayır — gerçek dünyada, Everest merkezli |
| Kot ölçeği | 0.6222 | **Evet** — rakip tavanı ÷ Everest 8840 m |
| Maske çözünürlüğü | 1024² | Hayır — 527 m/piksel, zirve aralığından (~11 km) çok ince |
| Zirve sayısı | 7 268 | Hayır — gerçek bölgenin yoğunluğu × alan × maske ortalaması |

**Dağın boyu değişirse:** zirve kotu, rakip tavanı ve kot ölçeği birlikte kayar; bölge
kenarı ve prominence tabanı **kaymaz**. Bölge kenarı ancak gezegen yarıçapı ya da
oyuncunun çıkabileceği en yüksek kot değişirse yeniden hesaplanır.

**`heightmapResolution` 4097 ile ilişki:** L0 grafik, ızgara değil — çözünürlükten
bağımsız. Izgaraya L1'de dönüşüyor; oradaki 4.28 m/örnek sınırı yukarıdaki tabloda
zaten yazılı.

## Onaylanmış dağ

Şu anki değerler `Assets/Settings/MountainSettings.asset` içinde: `terrainSize 17517`,
`terrainHeight 6189`, gerçek zirve **5686 m**, zemin **186 m**. Dağın onaylanma kaydı
`DECISIONS.md` → "Onaylanmış dağ: v1".
