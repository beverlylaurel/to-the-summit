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

## Arazi ölçeği: dağ ve oyun alanı korunur, çevre bölge eklenir (2026-08-17)

### Dağın boyu ve oyun alanı DEĞİŞMİYOR

Ölçüldü, mevcut kurulum tasarlandığı gibi çalışıyor:

| ne | ölçüm |
|---|---|
| Spawn → zirve | 11 576 m |
| Spawn → dağın eteği | 3 168 m |
| Bisikletle (19 km/s) | **10.0 dakika** |
| `road` | 193 nokta, 11.6–12.0 km bandı (ova yolu) |
| `branches` | 2804 nokta, 7.7–11.6 km — yoldan dağa çıkan **üç kol** |
| `camps` | 3 kamp, 7.75–8.91 km (eteğin iki yanı) |
| `shops` | 1 dükkân, 11.4 km |

Bir ara "arazi 17.5 → 36 km büyütülsün" önerildi; **ölçmeden söylenmişti, geri alındı.**
Ova dar sanılmıştı — spawn eksende varsayılmış, oysa köşede ve ova orada 3.2 km.

**Zirve kotu korunur** (~5709 m, `terrainHeight` tavanı 6189).

### Asıl sorun ölçek değil YAPI

Kesit ölçüldü — eğri içbükey, yani doğru şekilde:

| zirveden | kot | dilim eğimi |
|---|---|---|
| 1 000 m | 4 663 m | 56.8° |
| 2 000 m | 3 551 m | 48.0° |
| 4 000 m | 1 994 m | 34.3° |
| 6 000 m | 952 m | 25.1° |
| 8 408 m (etek) | 186 m | 21.1° |

Sorun sayıların büyüklüğü değil, **her azimutta aynı olması**. Bu radyal bir koni
(`secondaryPeaks: 0`): yükseklik yalnız merkeze uzaklığın fonksiyonu. Üst 2 km her yönde
48–57°, yani sürekli Eiger Kuzey Duvarı — yürünecek hiçbir hat yok.

Gerçek dağda **sırt** 25–30° (rotalar oradan gider), **duvar** 50–60° (bakılır, çıkılmaz).
Everest'te Güney Kol'dan zirveye ortalama 29°, çünkü sırttan gidiliyor.

**Yapay görünmesinin sebebi de aynı kök.** İki şikâyet, tek sebep: sırt/duvar ayrımı yok.
Divide Tree tam olarak bu ayrımı üretiyor.

### Üç bantlı mesafe temsili (gezegen boşluğu)

Ölçüm — ufuk küre üzerinde `sqrt(2Rh)`:

| kot | ufuk | arazi (8.76 km) |
|---|---|---|
| 186 m (ova) | 48.7 km | 6 kat eksik |
| 2 000 m | 159.7 km | 18 kat eksik |
| 5 709 m (zirve) | **269.9 km** | **31 kat eksik** |

Belirti: her kotta ufukta gezegen boşluğu görünüyor, yükseldikçe belirginleşiyor.

| bant | mesafe | temsil | çarpışma |
|---|---|---|---|
| oynanan | 0–18 km | Unity Terrain, tam detay, 4.28 m/örnek | var |
| çevre | 18–60 km | kaba mesh, ~100 m/örnek | yok |
| ufuk | 60–300 km | çok kaba mesh, ~1 km/örnek | yok |

Çevre + ufuk: tek mesh, tek çizim çağrısı, ~150 bin köşe, gölge yok, tek basit malzeme.
Bütün bölgenin verisi birkaç MB — **uzak dünya yakın dünyadan kat kat ucuz**, çünkü
pahalı olan çarpışma, doku ve detay ve uzakta hiçbiri yok.

**Oyuncu oraya asla yaklaşamayacağı için** LOD geçişi, akış ve güncelleme de yok. Bir kez
pişirilir, bir daha dokunulmaz.

**Ucuzluğun sebebi numara değil FİZİK:** 100 km ötedeki dağ hava perspektifinden dolayı
zaten mavi bir siluet. O saçılım (PBSky) zaten kurulu. 60 km'de ~200 m, 300 km'de ~1 km
köşe aralığı piksel altına düşüyor.

**EĞRİLİK ZORUNLU.** Uzak bant küre üzerinde kurulur:

| mesafe | zeminin düştüğü kot |
|---|---|
| 20 km | 31 m |
| 60 km | 282 m |
| 270 km | **5 715 m** |

Düz kurulursa uzak dağlar ufuk çizgisinin üstünde yüzer, anında sahte görünür.
(270 km'deki 5715 m düşüş ile zirvenin 5709 m'si örtüşüyor — zirveden tam 270 km görülmesi
bu yüzden.)

**MESH, GÖKYÜZÜ DOKUSU DEĞİL.** Oyuncu yatayda kısıtlı ama dikeyde 5.5 km yükseliyor;
ufuk 49 km'den 270 km'ye açılıyor, yani **tırmandıkça ufkun ardından yeni dağlar doğuyor**.
Boyalı skybox sabittir, bunu yapamaz. Mesh geometriden bedava yapar.

**Uzakta ağaç NESNE DEĞİL RENK.** ~2 km ötesinde orman arazi dokusunun tonudur. Billboard
ve mesh yalnız yakın alanda. Bu kural konmazsa uzak bantlar ucuz olmaktan çıkar.

### Tek jeneratör, geniş bölge, üç çözünürlük

Üretilecek olan bir dağ değil **bir bölge**: ~540 km kare (270 km yarıçap). Oyun alanı
onun merkezindeki 17.5 km.

Argudo'nun kendi ölçeği zaten bu — yöntem gerçek silsilelerin (Alpler, Himalaya)
orometrik istatistiklerinden çalışıyor. 17.5 km verilirse tek dağ üretir; 540 km verilirse
**silsile** üretir: farklı boyda ve karakterde dağlar, vadiler, etek tepeleri. "Etrafta
farklı dağlar olsun" isteği yöntemin doğal çıktısı, ek iş değil.

Üç bant aynı alandan örneklendiği için **dikiş sorunu yok**.

**HER YER DAĞ OLMAYACAK.** Uzak bant manzaradır, oynanacak alan değil — 360° zirve duvarı
tuhaf görünür. Gerçek bir silsilenin etrafı çeşitlidir: bir yönde asıl kütle ve etek
tepeleri, başka yönde açık ova, bir yönde vadi ve alçak sırtlar, bir yönde neredeyse düz.
(Everest'in kuzeyi Tibet platosu — kilometrelerce boş yüksek düzlük.)

Argudo kendi başına bırakılırsa istatistiğe uyup **her yere zirve serper**. Yön yön
karakter dağılımı ona ayrıca söylenecek: dağlık, tepelik, ova, plato. Bu bir ayar değil,
üretimin girdisi.

### Yapı araziye uyar, arazi yapıya değil

Bir ara "kamp ve konak için arazi yerel olarak düzleştirilecek" denmişti. **Yanlış.**

Gerçekte dağ yapıları eğime kurulur: Namche Bazaar dik bir çanağın içinde teraslarla,
dağ evleri taş sekilere, istinat duvarlarına, kademeli temellere oturur. Kimse dağı
düzleştirmez.

**Kural:** bina kendi temelini taşır — taş seki, istinat duvarı, kademeli kat, gerekirse
ayak. Arazi olduğu gibi kalır.

Kazandırdığı: yeniden üretim güvenli (silinecek arazi düzenlemesi yok), daha gerçekçi,
görsel olarak daha zengin — istinat duvarı ve seki karakter veriyor.

**Yerleştirme kuralı da değişiyor:** "düz yer bul" değil, **"uygun eğim bandı bul"** —
kabaca 10–30°. Çadır için 3×2 m'lik <15° bir düzlük yeterli, o her yerde var.

**Asıl kısıt eğim değil GÜVENLİK.** Kaya düşme hattına, çığ oluğuna, kar birikme çanağına
yapı konmaz — gerçekte de konmaz. L2'nin işi: eğim, bakı, korunaklılık, çığ maruziyeti.

### Üretim son söz değil: çapalı düzeltme işlemleri

Üretim her şeyi doğru veremez. Bilinen tek gerçek ihtiyaç: **sırtın yürünebilir
sürekliliği.** Erozyon sırtın ortasında bir kopukluk bırakabilir ve rota oradan geçemez.

**Düzeltmeler yükseklik haritasına ELLE YAPILMAZ** — dağ yeniden üretildiğinde hepsi
silinir. L3 ile aynı çözüm: düzeltme de **L0'a çapalanmış bir işlem** olarak saklanır
("47 numaralı boyunla 48 arasındaki sırtta kopukluğu kapat"), üretimden sonra otomatik
tekrar uygulanır.

Yani boru hattı tek yönlü değil: **üret → çapalı işlemleri uygula → pişir.**

Kamp ve konak bu listede **yok** — yapı araziye uyduğu için düzeltme gerektirmiyorlar.

### Sınır doğal olacak

Oyuncu 8.76 km'de arazinin kenarına ulaşabilir ve çarpışmasız banda girmemeli. Sınır
görünmez duvar değil, **arazinin kendisi** olacak: nehir, yarma, buzul çatlak alanı,
uçurum hattı.

**Tetikleyici:** ufukta boşluk görülürse ya da uzak dağlar ufuk çizgisinin üstünde
yüzüyorsa buraya bakılır.

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
