# Ölçek bağımlılıkları

Dağın boyu (`terrainSize`, `terrainHeight`) değiştiğinde neyin kendiliğinden kaydığı,
neyin elle düzeltilmesi gerektiği. **Dağ büyütülüp küçültülmeden önce baştan sona okunur.**

Yeni bir özellik eklerken sorulacak tek soru: *bu sayı dağın boyuna bağlı mı?* Cevap evet
ise buraya yazılır — hangi kategoriye girdiği ve büyüdüğünde ne olacağıyla birlikte.

**ÖLÇEK ARTIK ARAZİDEN OKUNUYOR.** Dağ elle yapılıyor (`Dağ Yapımı` penceresi) ve
taban/zirve her kurulumda `TerrainData`'dan ölçülüyor — bir ayar dosyasından değil.
zirve fırtınası kendiliğinden kayıyor.

Bu bir kez bozuldu ve ölçüldü: `peakAltitude` normalize kotu `MountainSettings.terrainHeight`
(6189 m) ile çarpıyordu ama arazinin gerçek tavanı 8000 m'ye çıkmıştı. Zirve 6001 m yerine
türeyip dağı baştan aşağı beyaza boyuyordu. İkisi de artık araziden.

Aşağıdaki tablolar hâlâ geçerli — ama **elle bakılacak** sütunu artık yalnız araziden
türetilemeyen sayılar için.

Üç kategori var:

- **Kendiliğinden ölçeklenir** — orana bağlı, dokunmaya gerek yok
- **Bilerek mutlak** — fiziksel bir metre, dağın boyunu bilmemeli; ölçeklenirse yanlış olur
- **Elle bakılacak** — mutlak ama dağın boyuna göre anlamı değişir

---

## Kendiliğinden ölçeklenir

| ne | nasıl |
|---|---|
| Arazi konumu | `transform.position = -terrainSize / 2` — zirve origin'de kalır |
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
| Sis yarı-yükseklikleri, inversiyon kotu | — | Atmosferin kendi yapısı |
| Bulut tabanı ve tavanı | — | Aynı |
| Gezegen yarıçapı | 6 360 km | Dünya |
| Sis hacmi menzili / dilim | 0.5 → 1000 m / 64 | Froxel hacmi gerçek metrelerde; dağ büyüyünce kameranın önündeki hava kalınlaşmaz. Dilim sayısı da sabit — üstel dağılım yakın alanı korur |
| `SunIntensity` / `MoonIntensity` | 3.030782 / 0.0199 | Gök cisimlerinin aydınlatması; dağ büyüyünce güneş güçlenmez. Oran 8,6 durak — gerçeğin (19 durak) altında ve BİLEREK, gerekçe `DECISIONS.md` |
| `moraineHeight` / `moraineSpacing` | 20 m / 420 m | Buzul moreni gerçek boyutu; dağ büyüyünce moren büyümez |
| `channelDepth` | 14 m | Dere yatağı derinliği |
| `hummockHeight` | 8 m | Tümsek yüksekliği |
| Birikinti gövdesi | 45 × 16 m | Rüzgârın oluşturduğu yığının gerçek boyu |
| Kar bölgesi `AreaSize` | 16 m | Oyuncunun etrafındaki deformasyon penceresi. Ölçüsü OYUNCUNUN adımı, dağın boyu değil — dağ iki katına çıkınca ayak izi büyümez. Üç presette de 16 |
| Kar `SnapStep` | 0.25 m | Bölgenin oturduğu ızgara. Teksel boyuna değil oyuncunun hareketine ölçülü; üç presette de tam sayı teksele denk gelmeli (8 / 16 / 24) |
| Kar halka 0 kapsamı | 8 m | Oyuncunun çevresindeki yoğun geometri; ölçüsü adım ve ayak izi, dağın boyu değil |
| Kar halka oranı | ×3 | Clipmap kademesi; dağ büyüyünce kademe sayısı değişmez |
| Kar uzak kaskadı | 192 m / 512 teksel | Yakın bölgenin dışındaki kar durumu. Ölçüsü görüş mesafesi değil, karın kot bazında değiştiği mesafe |
| Kar kalıcılık bloğu | 4 m | Saklanan parçanın boyu; ayak izi ölçeğine göre seçildi, dağın boyuna değil |
| Perde doğum mesafesi | 35 m | Savrulan kar tabakasının kameraya ulaşma mesafesi |
| Kar `SkyAreaSize` | 96 m | Gökyüzü görünürlük haritasının kapsamı; kar yağışının önünü kesen yakın geometrinin menzili |
| Kar yoğunlukları 50–550 kg/m³, su 1000 | — | Malzemenin kendi fiziği |
| Kar sıcaklık eşikleri (−20 / −5 / 5 °C, kar 0.5 / 2.0) | °C | Suyun fiziği; dağ büyüyünce kar farklı sıcaklıkta erimez |
| Saltasyon 1–5 cm, süspansiyon ≤ 5 m | — | Rüzgârın taşıdığı tanenin gerçek yükseklikleri (PBSM) |

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
| `HorizonResolution` 1024 | 17.1 m/texel | Arazi gölgesi kabalaşır |
| `openingRise` 400 m | zeminin ilk %7'si | Oransal olarak daralır; açılış kuşağı kaybolabilir |
| `descentDeadband` 250 m | — | İniş ölü bandı oransal daralır |
| `VolumetricClouds.cloudMapSize` 48 000 m | dağın ~2.7 katı | Hava haritası dağı kapsamalı; dağ büyürse birlikte büyümeli. 512 texel → 94 m/texel |
| `VolumetricClouds.shapeScale` 20 | bulut şekli dünyada 100 000 / 20 = **5 km** periyotta tekrarlıyor | Bulut kümesinin boyu. Dağ büyürse bulutlar orantısız küçük kalır; periyot dağın enine göre seçilmeli |
| `VolumetricClouds.bottomAltitude` 1200 m / `altitudeRange` 2000 m | katman 1200–3200 m, zirve 5709 m | Katman MUTLAK kotta (`localClouds` açık). Zirvenin katmanın üstünde kalması bilinçli: tırmanırken bulut denizini aşmak görülüyor. Dağ küçülürse zirve katmanın içinde kalır, büyürse aradaki fark açılır — ikisinde de birlikte bakılmalı |
| `maxHazeDistance` 60 000 m | — | Görüş menzili dağı görebilmeli |
| Bootstrap `SpawnPoint()` | eteğin dışı | Konum dağın boyundan türüyor, kontrol edilmeli |

---

## Arazi yeniden üretimi: neyin kırılacağı (2026-08-17)

Karar `DECISIONS.md` → "Arazi ölçeği". Değişen **boy değil yapı**: radyal koni yerine
Divide Tree, artı 300 km'ye uzanan üç bantlı mesafe temsili.

**`terrainHeight` DEĞİŞMİYOR** (zirve 5709 m). **`terrainSize` 17517 → 30000 m OLDU.**

Sebep ölçüldü, tahmin değil: L0 bir SİLSİLE üretiyor ve kütle 1500 m eşiğinde 379 km'ye
uzanıyor. 17.5 km'lik karede kuzey ve doğu kenarının **tamamı** 3665–3873 m'de kesiliyordu
— ekranda dikey duvar. Büyütmek tek başına bunu çözmez (her boyutta kesilir); çözüm
maskedeki **yalıtım halkası**: etekten (8.4 km) kenara kadar her yönde ova. 30 km o
halkaya yer açıyor — etek 8.4 km, ova 8.4→15 km, yani dağın 360° çevresinde
yürünebilir kuşak.

`terrainSize` DEĞİŞTİĞİNDE ELLE DÜZELTİLECEKLER (bu turda yapıldı, kayıt sonraki tur için):

| ne | neden kayar | ne yapıldı |
|---|---|---|
| `MountainRoute.asset` (3002 nokta) | konumlar **normalize**; aynı oran daha uzağa düşer | hepsi `0.5 + (u−0.5)×(eski/yeni)` ile yeniden ölçeklendi, dünya konumları korundu |
| `HeightmapImporter.SpawnUv` | aynı sebep | metre cinsinden yeniden hesaplandı |
| `bake_heightmap.SPAWN_UV` | aynı sebep | aynı |
| örnek aralığı | 4097² sabit | 4.28 → 7.32 m/örnek kabalaştı; tırmanılan yüzeyler mesh modül olacağı için kabul edildi |
| `region_profile.PLAY_KM`, `bake_heightmap.CROP_KM` | maske ve kırpma oyun alanına bağlı | 30.0 / 40.0 |

Bu yüzden yukarıdaki üç tablonun bir kısmı risk altında. Kırılanlar iki yerde toplanıyor:
**oyun alanına göre ölçeklenmiş ama artık 300 km görmesi gereken şeyler**, ve
**normalize konum tutan her asset**.

| ne | şu an | gereken | durum |
|---|---|---|---|
| **Kamera far clip** | `terrainSize × FarClipFactor` = **52.5 km** | ≥ 300 km | **KIRILIR** — uzak bant kırpma düzleminin ötesinde, hiç çizilmez |
| **`maxHazeDistance`** | 55 000 m | ~300 km | **KIRILIR** — 55 km'de pus doyuma gidiyor, uzak bant tek renk lapa çıkar |
| **`cloudMapSize`** | 40 000 m | görünen bölgeyi kapsamalı | **KIRILIR** — bulutlar uzak dağların üstünde biter, gökyüzü ortadan kesilir |
| `MAX_SKYBOX_VOLUMETRIC_CLOUDS_DISTANCE` | 200 000 m | 300 km | bakılacak — ufkun %74'ü |
| Ufuk haritası (`HorizonResolution` 1024, 30 km) | oyun alanı | 300 km | **YENİ İŞ** — 100 km ötedeki dağlar şafakta güneşi kapatmalı; şu an modellenmiyor |
| Bulut gölge mesafesi 12 000 m + çözünürlük 1024 | arena yatay | — | cookie kamera-merkezli, oyuncuyu takip eder; texel = bölge/çözünürlük ≈ 27 m. Arena genişlerse texel büyür (lapa) — çözünürlük birlikte artmalı. Dağın **boyuna** bağlı DEĞİL |

**Derinlik hassasiyeti:** far clip 52.5 → 300 km, yani 5.7 kat. Ters-Z ile float derinlik
bunu rahat taşıyor; asıl belirleyici yakın düzlem. Yine de uzak bant geldiğinde z-savaşı
kontrol edilir.

**Şekil parametreleri gereksizleşiyor.** `MountainSettings` içindeki radyal koni alanları
(`heightProfile`, `mountainRadius`, `peakSpread`, `ridgeInfluence`, teras ve erozyon
grubu) yerlerini Argudo parametrelerine bırakacak. `forelandFanDrop` 60 m de öyle — ova
artık radyal bir yelpaze değil, üretilen bölgenin parçası.

**Değişmeyenler (boy sabit kaldığı için):** kuşak sınırları, tipi kuşağı, kar profili
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

## Şu anki dağ

Arazi `Assets/Terrain/MountainTerrainData.asset`; düzenlenebilir asıl
`Assets/Terrain/Sculpts/_son.bytes` (1025², float32). Ayar dosyası DEĞİL — dağ elle
yapılıyor.

| ne | değer | nereden |
|---|---|---|
| Oyun alanı | 30 000 m | `TerrainData.size.x` |
| Dikey tavan | 8 000 m | `TerrainData.size.y` |
| Ölçülen zirve | ~6 001 m | `MountainGenerator.peakAltitude` (araziden) |
| Ölçülen taban | ~0 m | `MountainGenerator.groundAltitude` (araziden) |
| Izgara | 4097² | 7.32 m/örnek |
| Yapım ızgarası | 1025² | 29.3 m/hücre |

**Bunlardan türeyen hava kuşakları** (`AltitudeWeatherDriver`, oranlar sabit):

| kuşak | formül | şu anki değer |
|---|---|---|
| Yağmur tavanı | taban + boy × 0.10 | 600 m |
| Kar tabanı | yağmur tavanı + boy × 0.04 | 840 m |
| Zirve fırtınası | zirve − 1000 m | 5 001 m |

Dağ yeniden yontulunca dördü de kendiliğinden kayıyor. Karsız kalan pay şu an **%57**;
**boyun oranı** olduğu için (`RATIONALE.md` → Kuşaklar).

## Ayak izi bölünmesi — ELLE BAKILACAK

    üçgen kenarı = terrainSize / (heightmapResolution − 1) = 30000 / 4096 = 7.32 m
    bölünmüş kenar = 7.32 / 64 = 0.114 m
    ayak izi = 0.34 m  ->  iz üç bölünmüş üçgene oturuyor

`terrainSize` ya da `heightmapResolution` değişirse bu oran kayar. İz çözülmüyorsa
katsayı, üçgen kenarının izin üçte birine inmesini sağlayacak şekilde ayarlanır.
Donanım tavanı 64; üçgen 7.32 m'nin üstüne çıkarsa bölünmeyle çözülemez ve iz için
başka bir yol (ayrı yakın-plan mesh'i) gerekir.

**Kendiliğinden ölçeklenmeyen ama ölçekten BAĞIMSIZ olanlar:** deformasyon penceresi
(24 m), texel boyu (4.7 cm), ayak ölçüleri, adım aralığı, iz derinliği. Hepsi gerçek
dünya büyüklüğü; dağın boyuyla ilgileri yok.

