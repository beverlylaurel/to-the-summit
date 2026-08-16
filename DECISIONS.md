# Kararlar

Bilinçli olarak ertelenmiş veya sınırlandırılmış kararlar. Amaç: "sonra bakarız" demenin
kaybolmaması. Her kaydın **tetikleyicisi** var — o belirti görüldüğünde karar yeniden açılır.

Karar geri alındığında kaydı sil, geçerliyse yerinde bırak.

Kayıtların çoğu kapanmış karardır: okunur, uyulur, dokunulmaz. Aksiyon gerektiren üç tür
aşağıda ayrı listelenir — "şu an neyi bekliyoruz" sorusunun cevabı bütün dosyayı okumadan
alınabilsin diye. **Yeni kayıt bu üç türden birine giriyorsa aynı adımda buraya da yazılır;
iş bitince buradan silinir.**

## Bloke eden açık sorular

Cevaplanmadan ilgili sisteme kod yazılmaz.


- **Ekipman ve kamp** — ekipmanın envanterde nasıl durduğu, kamp/sığınakta ne yapıldığı
  → [Oynanış mekaniği netleşmeden koda başlanmaz](#oynanış-mekaniği-netleşmeden-koda-başlanmaz)
- **Tırmanma ayrıntıları** — tutamak üretimi hangi veriden beslenir, düşüş hasarının
  eğrisi, ip fizik olarak mı kural olarak mı
  → [Tırmanma üç modlu, modu ZEMİN belirler](#tırmanma-üç-modlu-modu-zemin-belirler)

## Silinecek geçiciler


- **Ova ve patika ölçüm araçları** (`ForelandProbe`, F1'deki kurulum süresi logu) — ova
  ve yol dokusu oturunca silinir
- **Bisiklet maskesi yeniden seyreltmede silinecek** — malzeme maskesi köşe renginde duruyor;
  model yeniden seyreltilirse topoloji değişir ve boyama kaybolur. Seyreltme yapıldı
  (3.1 M → 200 bin), boyama artık güvenle yapılabilir; bütçe değişirse maske aktarımı
  yazmak gerekir


## Bekleyen kararlar

- **Cepheyi ne sürecek** — yaklaşmanın yarısında kar başlaması isteniyor; şiddet şu an
  yalnız rakımdan geliyor ve ovada minimum
  → [Yaklaşmada kar bir CEPHEDEN gelir](#yaklaşmada-kar-bir-cepheden-gelir)


### `DepthNormals` fragman maliyeti KABUL EDİLDİ

`SnowDisplacedNormal` bu geçişte fragman başına çağrılıyor. Kaldırılması denenmedi çünkü
gerekçesi geçerli: SSAO bu tamponu okuyor ve kar birikintisinin eğimi orada olmazsa
kabartının dibindeki gölge hiç oluşmuyor. Ucuzlatmak için köşe normalini yazmak, geçişi
var eden sorunu geri getirir — arazi örgüsünün üçgen kırıkları "yüzey kıvrımı" okunup
zeminde kafes çizgileri bırakıyordu.

**Tetikleyici:** kare süresi bütçesi zorlanırsa önce bu geçişin GPU maliyeti ölçülür;
ölçmeden ucuzlatma yapılmaz.

---

### Işından bağımsızlık için ödenen kare süresi KABUL EDİLDİ

Üç ucuzlatma bilerek söküldü ve bedeli ödenmiş sayılıyor: doğruluk, kare süresinden önce
gelir. Sökülen ucuzlatmalar geri gelirse belirtileri de geri gelir — bu oturumda aynı
sınıftan üç hata ölçüldü (çarpım kafesi, az örnekli keskin alan, sıra bağımlı çakışma).

**Tetikleyici:** bir build'de kare süresi hedefin altına düşerse ölçüm build'de yapılır,
editörde değil.

---

### ESM YAZILMIYOR — gölge mesafesi 60 m

Planın 3. adımıydı. Yazılmadı çünkü kazancı gölge mesafesiyle sınırlı: `PC_RPAsset`'te
`m_ShadowDistance: 60`, yani arazinin kendi huzmesi zaten yalnız ilk altmış metrede var.
ESM'nin çözdüğü sorun (gölge haritası kenarlarındaki sızıntı ve aliasing) o mesafede
görünür bir kazanç üretmiyor; bulut gölgesi de haritadan değil ana ışık cookie'sinden
geliyor, yani ESM'nin dokunmadığı bir yol.

**Tetikleyici:** gölge mesafesi büyütülürse (ya da arazi huzmesi uzak mesafede
istenirse) karar yeniden açılır. O ana kadar yazmak, ölçmeden büyük bir sistem eklemek
olur.

---
## Bekleyen ölçümler

- **Güneş 3.03'e çıktıktan sonra yüzeyler** — arazi, kar ve bisiklet 1.5'e göre
  ayarlanmıştı; yeniden ayar gerekip gerekmediğine bakılmadı
  → [Güneş şiddeti pakete kalibre edildi](#güneş-şiddeti-pakete-kalibre-edildi-15--3030782)
- **Gölge mesafesi 60 m, sis hacmi 1000 m** — arazi huzmesi yalnız ilk 60 metrede
  oluşabiliyor; keskin ışık kolonu istendiğinde ilk bakılacak yer
  → [Volumetrik sis](#volumetrik-sis-wronski-froxel-hacmi-geldi-2026-08-16)
- **Kare süresi jitter'ı** — editörde ölçüldü, patoloji çıkmadı; aynı ölçüm bir
  **derlemede** tekrarlanacak. (Ayrıntı kaydı yok: indeks bir kayda bağ veriyordu ama o
  kayıt dosyada hiç yazılmamış. Bağ kaldırıldı, madde kendi kendine yeter.)
- **Ortamın ölçeği.** Probe SH'si yüzey aydınlatması birimindedir, ortam ise içeri saçılan
  radyans ister. Ölçüldü: probe DC luminansı 0.156, sis rengi 0.492 — oran **3.15**.
  Kâğıtta: 3 km'deki sırt eski yolda `0.962·air`, yeni yolda `0.509·air`, yani 1.9 kat
  koyu; ekranda ara mesafedeki puslu sırtlar kayboluyordu. Seviye zincirin kendi sis
  renginden alındı, SH yalnız yön şekillendirmesi olarak kaldı.
- **Komut tamponu globalleri compute'a ULAŞMIYOR.** `Shader.SetGlobalX` ile yazılanlar
  ulaşıyor; URP'nin `cmd.SetGlobal...` ile yazdıkları (`_MainLightColor`,
  `_MainLightPosition`, cookie matrisi) sessizce sıfır okunuyor. Belirti: hacimde hiç
  doğrudan ışık yoktu, sahne düz görünüyordu ve hata da vermiyordu. Ana ışık, cookie
  matrisi ve ortam SH'si C#'tan açıkça geçiyor; dokular kernel'e elle bağlanıyor.
- **Compute keyword'leri elle yönetilemiyor.** `multi_compile` denendi: bu keyword'ler
  URP tarafından global olarak da açıldığı için aynı kümedeki iki gölge keyword'ü birden
  açık kalıyor ve o varyant hiç derlenmemiş oluyor. Varyant `#define` ile sabitlendi;
  `PC_RPAsset` ayarı değişirse compute başlığı da değişmeli.

**AÇIK KALAN — GÖLGE MESAFESİ 60 m.** Hacim 1000 m ama URP'nin gölge mesafesi 60 m, yani
arazinin kestiği huzme yalnız ilk 60 metrede oluşabiliyor. Bulut gölgesi cookie'den
geldiği için o sınıra takılmıyor ama çözünürlüğü 8000 m'ye 256 teksel (31 m/teksel), yani
büyük ölçekli yapı veriyor, keskin kolon vermiyor. İkisi de ayar meselesi; keskin ışık
huzmesi istendiğinde ilk bakılacak yer burası.

**YAPILMAYANLAR:** ESM (spec §5.1) yazılmadı — gölge mesafesi 60 m'yken getirisi sınırlı.
Async compute, point light döngüsü, partikül enjeksiyonu kapsam dışı bırakıldı.

## Arazi mimarisi: dört katman, sembolik çapa (2026-08-17)

Tasarım tarafı `DESIGN.md`'de. Burası **teknik** karşılığı: dağ nasıl üretilecek ve
içerik ona nasıl tutunacak.

### Dağ pişmiş İÇERİK, çalışma zamanı üretimi değil

Dağ her oyunda yeniden üretilmez; herkeste **aynı dağ**. Editörde bir kez üretilir,
çıktısı repoya yazılır, çalışma zamanı yalnızca yükler.

**Gerekçe:** co-op senkron probleminin tamamını ortadan kaldırıyor — paylaşılan durum
yoksa senkronlanacak bir şey de yok. Üretim kodunun hızı da önemsizleşiyor; tek seferlik.
Önemli olan **çıktının kararlılığı**.

### Dört katman

| katman | ne | değişince |
|---|---|---|
| **L0 iskelet** | Divide Tree: zirve/boyun/sırt grafiği | her şey |
| **L1 yükseklik** | DEM — L0 + gürültü + erozyon | yeniden üretilebilir |
| **L2 işaretler** | eğim kuşağı, bakı, korunaklılık, düz alan | türetilmiş, önbellek |
| **L3 yerleşim** | mağara, kamp, konak, mezar, anıt | elle, **kaybolmamalı** |

**L0 SAKLANIR.** Spec'in boru hattı Divide Tree'yi DEM'e çevirip atıyor (§5.6). Biz veri
olarak tutacağız — içerik ona çapalanacak. Baştan kurulursa bedava, sonradan geri
çıkarmak imkânsız.

**L3 DÜNYA KOORDİNATI TUTMAZ.** `(düğüm kimliği, yerel ofset, oturma kuralı)` tutar.
Erozyon değişip L1 baştan üretilse bile kamp yerinde kalır, kendini yeniden oturtur.
Mutlak koordinat kullanılırsa her yeniden üretim tüm yerleştirme emeğini çöpe atar —
"ileride değiştirince patlar mı" sorusunun cevabı bu tek karardır.

### Mağara, tırmanış yüzeyi ve zirve MESH; arazi yükseklik haritası kalır

Yükseklik haritası her (x,z) için tek yükseklik tutar: mağara, çıkıntı, tavan **temsil
edilemez**. Voksel araziye geçmek tüm arazi shader'ını ve çarpışmayı çöpe atardı.

Aynı sınırın ikinci sonucu ölçüldü: `heightmapResolution` 4097 → örnek başına **4.28 m**.
Tırmanılacak çıkıntı ~1 m, Hillary Step ~12 m — yani **üç örnek**. Tırmanış geometrisi de
haritaya sığmıyor.

Üç inşa kipi:

| kip | nerede | nasıl | alan payı |
|---|---|---|---|
| üretilen | yaklaşma, vadi, alt yamaç | yükseklik haritası | ~%90 |
| üretilen + modül | orta dağ: tırmanış kesiti, mağara, kamp | harita + gömülü mesh | ~%9 |
| elle tasarlanan | son kol → zirve | tamamen mesh, bölüm gibi | ~%1 |

Alanın %1'i dramanın yarısını taşıyor. Zirve rastgeleliğe bırakılmaz.

**Sonucu:** tırmanma mekaniği **önce mesh üstünde** kurulur, arazi eğimi ikincildir.

### Zorluk YÜZEYDEN türer, yükseklikten değil

Mekanik eğimi ve zemini okur; yükseklikle korelasyon üretimden gelir. Alçakta dik bir
duvar bulunursa o da tırmanıştır. Eşik yok, özel durum yok — projenin kendi mimarî
kuralı (sistemler duruma bakar, sabite değil).

### Co-op: tek oturum tohumu

Dağ pişmiş içerik olunca geriye tek risk kalıyor: çalışma zamanında rastgelelikten
türeyen her şey. **Bir oturum tohumu, host verir, her şey ondan türer.** Tohumsuz
`Random` ve birikimli durum yasak.

Mevcut ihlaller zaten envanterde: `COOP.md` madde 1 (şimşek) ve madde 6 (bulut rüzgârı).

**Tetikleyici:** araziye ya da yerleştirmeye dokunan her iş bu kaydı okumadan başlamaz.

---

## Spec sırası: terrain → snow → rain → lightning (2026-08-17)

**Gerekçe — terrain önce:** `terrain-generation-spec.md` bir ekleme değil, **tam yeniden
üretim** (Divide Tree + orometri + erozyon). Mevcut dağ gidiyor. Kar birikimi, yüzey
kalibrasyonu, gölge mesafesi, rota, ova — hepsi arazinin üstünde duruyor. Kar önce
yapılırsa arazi değişince kar işinin iyi kısmı ikinci kez yapılır.

**snow ikinci:** oyunun kimliği. Ölçülmüş açık bir hata da burada kapanıyor — yağış
partikülleri ışık okumuyor, gece karı öğle karıyla aynı. Bekleyen "cepheyi ne sürecek"
kararı da bu spec'in içi.

**rain üçüncü:** partikül boru hattı karla ortak, kardan sonra ucuz. Ayrıca oyun alanında
neredeyse hiç görünmüyor — yağmur −367 m'de bitiyor, oynanan kot 2000–5700 m.

**lightning son:** en yalıtık ve en küçük (488 satır), yalnız fırtınada. Sona kalması bir
şey kaybettirmiyor. Co-op borcu (`COOP.md` madde 1) o tur sırasında ödenecek.

**Maliyet — açıkça:** terrain en sarsıcı olanı. Atmosfer yeni oturdu; dağ değişince ölçek
bağımlı sayıların bir kısmı kayacak. Başlamadan **önce `SCALE.md` baştan sona okunacak**.

---

## Gecedeki "fasulye" kapandı: sebep gökyüzü değil, gece ışık seviyesiydi (2026-08-16)

**Belirti.** Gece gökyüzünde devasa, keskin kenarlı siyah bölge. Zenit merkezli, irtifayla
büyüyor, yükseğe uçunca tüm göğü kaplıyor. Haftalarca gökyüzü hesabında arandı.

**Orada değildi.** Gökyüzü shader'ına on dokuz modluk geçici sonda kondu. Ölçümler:
`rayIntersectsAtmosphere`, `lookAboveHorizon`, `tFrag`, NaN/negatif — hepsi temiz; 4B tablo
dolu; **durak konturu tüm gökte tek sınır verdi**, yani en parlak ve en sönük yer arasında
1 duraktan az fark var. Aynı veri ×50 basıldığında fasulye yok, ×1 basıldığında var.

Sonuç: 2 kattan küçük bir fark ekrana basılırken siyah/görünür diye ikiye ayrılıyordu.

**Araç iki kez yalan söyledi, ikisi de yakalandı.** Önce LUT eksenlerini parlaklıkla
basmak (gece pozlaması tavanda, düz renge ezdi), sonra ×50 parlatma (göreli karanlığı
doyurup yuttu). Bundan sonrası ton ve kontur ile ölçüldü — ikisi de pozlamadan bağımsız.

**Kök sebep: ay on dört durak fazla parlaktı.** Gerçek güneş/ay oranı 19 durak, bizde
5,3'tü. `MoonIntensity` 0.204 → 0.0058, sonra bulut görünürlüğü için 0.0199.

**Yol boyunca düzeltilen ayrı kusurlar:** `m_HDRColorBufferPrecision` 0 → 1 (R11G11B10
mavi kanalında %3 basamaklar, düz gökte eş merkezli halkalar); `m_ColorGradingMode` 0 → 1
(gece değerleri 32 düğümlü LDR LUT'un en alt hücrelerinde eziliyordu, keskin kenarların
kaynağı); `m_ColorGradingLutSize` 32 → 64; kamerada `dithering` açıldı.

**Denenip GERİ ALINAN:** `adaptShare` 0.35 → 0.60. Fasulyeyi kapatıyordu ama karanlık ucu
kaldırırken parlak ucu da kaldırdı. Pozlama bu iş için yanlış alet; karanlık ucun aleti
gece profilinin `contrast` değeri (6 → −22).

**Ders.** Belirtinin göründüğü yer, belirtinin doğduğu yer değildir. Önce verinin kendisi
ölçülür, sonra onu ekrana basan zincir.

## Gece seviyesi: ayı BULUT belirledi, sis yenilenince tekrar bakılacak

`MoonIntensity` önce 0.0058'e çekildi (−4 durak hedefi, pozlama uyumunun tavana dayalı
olduğu formülden). Arazi doğru göründü ama **ay ışığındaki bulut eşiğin altında kalıp
simsiyah çıkıyordu** — kapsama arttıkça yıldızlar kayboluyordu, yani bulut oradaydı ve
göğü kapatıyordu, kendi katkısı görünmüyordu.

Sürgüyle ölçüldü: ay yükseltilince bulut karla **birlikte ve orantılı** parlıyor. Yani
bulutun saçılım integrali sağlam, mesele eşikti. 0.0199 ikisinin de eşiğin üstünde olduğu
en düşük değer. Etkin oran 396:1 = 8,6 durak (gerçek 19 durak).

**TAVAN ARTIK KIL PAYI BAĞLI.** Uyum `0.35 × 7,25 = 2,54` istiyor, `exposureCap` 2,5'te
kırpıyor. Buradan yapılan değişiklik şu an ekrana birebir iniyor; ay biraz daha
yükseltilirse kırpma kalkar ve kısıntının %65'i geri gelir.

**Sise bakarak yapılan iki kırpma GERİ ALINDI:** kar albedosu 0.66 → 0.90 ve ayın ilk
gözle ayarı. Belirtinin sebebi (yükseklik sisi) çürüyünce sayı da düştü.

**KAPANDI (2026-08-16).** Sis yeniden yazıldı ve "ortam biraz aydınlık" kalıntısının
sebebi bulundu: gece seviyesi değil, SİS RENGİNİN SEVİYESİYDİ. Renk elle yazılmış bir
sabitti ve gökle birlikte kaymıyordu — gök gündüz-gece arası ~230 kat değişirken sis
rengi 9.6 kat değişiyordu. Ölçüm: gece sis rengi ortam probunun 34.6 katı, kalibre oran
ise 3.15; yani gece sis 11 kat fazla parlaktı ve örttüğü her şeyi 3.5 durak yukarı
kaldırıyordu.

Seviye artık probe'dan geliyor, sabit yalnız ton taşıyor (`SYSTEMS.md` → sis rengi).
`MoonIntensity` 0.0199'a DOKUNULMADI: ölçüm ayın değil sisin yanlış olduğunu gösterdi.

## Bulut ayı DOĞRUDAN alıyor — eski kayıt ölçümle çürüdü

Eskiden "ay bulutları yalnız ortam ışığından aydınlatıyor, doğrudan ışık maliyeti
ölçülmedi" yazıyordu. Ölçüldü, yanlışmış.

Gece bulutun okuduğu ışık F1'e basıldı: `bulut ışığı: Moon Light`, `bulut ışık rengi
0,00551 0,00700 0,01157`, `ortam tepe 0,00004 0,00011 0,00033`. Yani bulut doğru cismi
görüyor ve aldığı doğrudan ışık ortamın **35 katı**. Zincir: URP ana ışığı gece aya
düşüyor (ay `LightShadows.Soft` taşıdığı için her iki paketin `GetMainLight`'ı da onu
seçiyor), gökyüzü paketi `_MainLightColor`'ı aydan yazıyor, bulut onu okuyor.

Ek maliyet yok: doğrudan ışık zaten uygulanıyordu.

## `VolumetricClouds.mat` yerel değişiklikleri izlenmiyor (2026-08-17)

**Karar:** dosya repoda kalıyor ama `git update-index --skip-worktree` ile işaretlendi.

**Gerekçe:** `VolumetricCloudsURP` rüzgâr birikimini (`_WindVector`,
`_VerticalErosionWindDisplacement`) her karede materyale yazıyor — materyal bu pakette
rüzgâr durumunun deposu. Her Play sonrası diff çıkıyor ve gerçek değişiklikleri
gizliyordu. `.gitignore` işe yaramaz: dosya izlendiği için ignore yok sayılır.
Tamamen çıkarmak da olmaz — renderer feature onu GUID'le arıyor, temiz klonda bulut yok.

**Değerlerin kaybolması sorun değil:** `resetWindOnStart` açılışta rüzgârı sıfırlıyor,
yani commit'teki sayı zaten kullanılmıyor.

**MALİYET — okunmadan geçilmesin:** `skip-worktree` **yerel bir ayardır, repoda
taşınmaz.** Başka makinede klonlanınca aynı gürültü geri gelir; orada komut tekrar
çalıştırılır:

```
git update-index --skip-worktree Assets/Settings/VolumetricClouds.mat
```

Materyalde **bilerek** bir değişiklik yapılacaksa (şekil ölçeği, gürültü, kapsama)
işaret önce kaldırılır, yoksa değişiklik sessizce commit'lenmez:

```
git update-index --no-skip-worktree Assets/Settings/VolumetricClouds.mat
```

**Tetikleyici:** materyalde yapılan bir ayar değişikliği commit'e girmiyorsa ya da temiz
klonda bulut ayarları beklenenden farklıysa buraya bakılır.

## Paketin sisi kapalı başlıyor

**Karar.** `Fog` override'ı profile eklendi ama `enabled = false`.

**Gerekçe.** Kendi yükseklik sisimiz sis bankları, inversiyon tavanı ve vadi sis denizi
taşıyor; paketin `Fog`'unda bunların karşılığı yok — düz üstel yükseklik sisi. İkisi
birlikte açılırsa sis iki kez uygulanır ve hangisinin ne kattığı ayırt edilemez.

**Tetikleyici.** Kendi yükseklik sisimizin taşıdığı üç özellik (bank, inversiyon, sis
denizi) pakete taşınabildiğinde ya da gereksiz görüldüğünde açılır ve bizimki silinir.

**Maliyet.** Sis rengi gökyüzünden türemiyor; `AtmosphereController`'ın kendi renk
zincirinden geliyor. Gökyüzü fiziksel, sis değil — ufukta ton farkı çıkabilir.

## Güneş şiddeti pakete kalibre edildi (1.5 → 3.030782)

**Karar.** `TimeOfDay.sunIntensity` **3.030782**. Sayı paketin kendi önerisi (100000 lux
yer aydınlığı, pozlama 0), bizim seçimimiz değil. Hem koddaki varsayılan hem sahne kurulumu
yazıyor.

**Tetikleyici ÇALDI.** Öğle vakti hava tuhaf görünüyordu; gökyüzü sahneye göre sönük
kalıyordu çünkü paket gök parlaklığını ana ışıktan türetiyor ve 1.5 kalibrasyonun yarısı.

**Neden telafi değil de kaynak düzeltildi.** İki aday vardı: güneşi 3.03'e çıkarmak ya da
paketin `exposure`/`multiplier` alanından ~1.01 EV telafi etmek. İkincisi gökyüzünü
düzeltip ışığı yanlış bırakırdı — aynı belirti gölge, yansıma ve bulut aydınlatmasında
sürerdi. Kaynak tek olmalı.

**Açık kalan.** 1.5 sayısının üstüne arazi, kar, bisiklet ve ACES tonemap oturmuştu.
Yüzeylerin yeniden ayarlanması gerekip gerekmediği BAKILMADI — belirti fazla parlak arazi,
patlamış kar ya da sönmüş kontrast olur. `LookController` pozlaması ilk bakılacak yer.
