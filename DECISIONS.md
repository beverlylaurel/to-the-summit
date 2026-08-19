# Kararlar

Bilinçli olarak ertelenmiş veya sınırlandırılmış kararlar. Amaç: "sonra bakarız" demenin
kaybolmaması. Her kaydın **tetikleyicisi** var — o belirti görüldüğünde karar yeniden açılır.

Karar geri alındığında kaydı sil, geçerliyse yerinde bırak.

Kayıtların çoğu kapanmış karardır: okunur, uyulur, dokunulmaz. Aksiyon gerektiren üç tür
aşağıda ayrı listelenir — "şu an neyi bekliyoruz" sorusunun cevabı bütün dosyayı okumadan
alınabilsin diye. **Yeni kayıt bu üç türden birine giriyorsa aynı adımda buraya da yazılır;
iş bitince buradan silinir.**

## Ova kontrastı düzeltilmiyor — irtifadan geliyor

**Karar.** Düz zeminde güneş-gölge farkı ~4 diyafram kalıyor. Dokunulmadı.

**Gerekçe.** İlk okuma "fazla koyu"ydu, çünkü referans olarak **deniz seviyesi** sayısı
alınmıştı (kar için 2–3.5 diyafram). Oyuncu 4900 m'de. O irtifada gerçek ölçüm: doğrudan
huzme ~1050 W/m², yayınık gök ~60–80 W/m² → **~3.8 diyafram**. Okuduğumuz değer doğru.

Yüksekte gölgenin sert olması gerçek bir şey; ince atmosfer daha az saçıyor.

**`sunIntensity` ile düzeltilemez, yapısal olarak.** Gökyüzü paketi göğü güneş
şiddetinden hesaplıyor: güneş kısılınca gök de kısılır, oran değişmez. Sayı zaten paketin
kendi önerisi (`MountainSceneBootstrap`), bizim seçimimiz değil. Bir kez 1.5 denendi ve
gök sahneye göre sönük kaldı.

**Tetikleyici — geri dönülecek belirti:** ovada gölge hâlâ okunamayacak kadar koyuysa
çözüm güneş sabiti değil, gökyüzü paketinin aerosol/bulanıklık parametresidir — yayınık
payı oradan gelir.


## Oyun alanı 17.5 → 30 km, ve yalıtım halkası

**Karar.** `terrainSize` 30000 m. Dağın eteği 8.4 km'de bitiyor, oradan arazi kenarına
(15 km) kadar her yönde ova zorlanıyor.

**Gerekçe — ölçüm.** İlk şüpheli "arazi küçük"tü ve yanlıştı. L0 bir SİLSİLE üretiyor:
kütle 1500 m eşiğinde 379 km'ye uzanıyor, yani arazi ne kadar büyük olursa olsun kenarda
kesilir. 17.5 km'lik karede kuzey ve doğu kenarının **tamamı** 3665–3873 m'deydi.

Kesilmeyi kaldıran şey boyut değil **maske**. Boyut yalnız halkaya yer açıyor: 8.4 km
etek + 6.6 km ova = 15 km yarı genişlik.

**İki şart da ölçüldü ve karşılandı:**

| şart | ölçüm |
|---|---|
| kenarda kesilme yok | dört kenar ortancası 214–389 m, en yüksek 915 m (tavan 1200 m, üretim denetliyor) |
| 360° yürünebilir kuşak | her azimutta 8–14.9 km halkasında 687 m altı zemin var, ortanca 209 m; turun %0'ı 1000 m üzerine çıkmıyor |
| kapalı tur | 67.2 km, ortanca 8.4°, %92 yürünür, teknik %0.3 |
| zirve spawn'dan görünür | 24 m açıklık (önce 124 m kapalı) |

**Maliyet.** Örnek aralığı 4.28 → 7.32 m kabalaştı (4097² sabit, PNG 14.3 MB, repo boyutu
değişmedi). Tırmanılan yüzeyler mesh modül olacağı için kabul edildi — arazi onların
oturduğu zemin.

**Yan etki, aynı adımda ödendi.** `terrainSize` normalize konum tutan her şeyi kaydırıyor:
`MountainRoute.asset`'in 3002 noktası `0.5 + (u−0.5)×(17517/30000)` ile yeniden
ölçeklendi, dünya konumları korundu (spawn zirveden 11576 m'de kaldı, bisiklet turu
bozulmadı). Tam liste `SCALE.md` → "terrainSize değişince elle düzeltilecekler".

**Tetikleyici — geri dönülecek belirti:** uzak bantlar (18–60 km) gelince 15 km'deki
ova/silsile geçişi görünür bir dikiş yaratırsa halkanın dış sönümü o banda taşınır.


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

- **Şimşek yağan kar ve yağmuru aydınlatmalı** — kar/yağmur spec'lerine geçildiğinde
  ışık kaynağı listesine şimşek eklenecek
  → [Şimşek–yağış etkileşimi ERTELENDİ](#şimşek-yağış-etkileşimi-ertelendi)
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

Tasarım tarafı `DESIGN.md`'de; burası teknik karşılığı.

**Dağ pişmiş İÇERİK.** Editörde bir kez üretilir, çıktı repoya yazılır, çalışma zamanı
yalnız yükler; herkeste aynı dağ. Co-op senkron probleminin tamamını ortadan kaldırıyor —
paylaşılan durum yoksa senkronlanacak şey de yok. Üretim hızı önemsiz, **çıktının
kararlılığı** önemli.

| katman | ne | değişince |
|---|---|---|
| **L0 iskelet** | Divide Tree: zirve/boyun/sırt grafiği | her şey |
| **L1 yükseklik** | DEM — L0 + gürültü + erozyon | yeniden üretilebilir |
| **L2 işaretler** | eğim kuşağı, bakı, korunaklılık, düz alan | türetilmiş, önbellek |
| **L3 yerleşim** | mağara, kamp, konak, mezar, anıt | elle, **kaybolmamalı** |

**L0 SAKLANIR.** Spec Divide Tree'yi DEM'e çevirip atıyor (§5.6); biz veri olarak
tutuyoruz, içerik ona çapalanıyor. Baştan kurulursa bedava, sonradan geri çıkarmak
imkânsız.

**L3 DÜNYA KOORDİNATI TUTMAZ** — `(düğüm kimliği, yerel ofset, oturma kuralı)` tutar.
L1 baştan üretilse bile kamp yerinde kalır. Mutlak koordinat her yeniden üretimde tüm
yerleştirme emeğini çöpe atardı; "ileride değiştirince patlar mı" sorusunun cevabı bu.

**Mağara, tırmanış yüzeyi ve zirve MESH.** Yükseklik haritası her (x,z) için tek yükseklik
tutar: mağara, çıkıntı, tavan temsil edilemez. Voksele geçmek arazi shader'ını ve
çarpışmayı çöpe atardı. İkinci sonuç ölçüldü: 4097 çözünürlükte örnek başına 4.28 m;
tırmanılacak çıkıntı ~1 m, Hillary Step ~12 m — **üç örnek**.

| kip | nerede | nasıl | alan payı |
|---|---|---|---|
| üretilen | yaklaşma, vadi, alt yamaç | yükseklik haritası | ~%90 |
| üretilen + modül | orta dağ: tırmanış kesiti, mağara, kamp | harita + gömülü mesh | ~%9 |
| elle tasarlanan | son kol → zirve | tamamen mesh | ~%1 |

Alanın %1'i dramanın yarısını taşıyor; zirve rastgeleliğe bırakılmaz. Tırmanma mekaniği
**önce mesh üstünde** kurulur, arazi eğimi ikincildir.

**Zorluk yüzeyden türer, yükseklikten değil.** Mekanik eğimi ve zemini okur; alçakta dik
bir duvar bulunursa o da tırmanıştır. Eşik yok, özel durum yok.

**Co-op: tek oturum tohumu.** Host verir, çalışma zamanındaki her rastgelelik ondan türer.
Tohumsuz `Random` ve birikimli durum yasak. Mevcut ihlaller `COOP.md` madde 1 ve 6.

**Tetikleyici:** araziye ya da yerleştirmeye dokunan her iş bu kaydı okumadan başlamaz.

---

## Arazi ölçeği: dağ ve oyun alanı korunur, çevre bölge eklenir (2026-08-17)

### Dağın boyu ve oyun alanı DEĞİŞMİYOR

| ne | ölçüm |
|---|---|
| Spawn → zirve | 11 576 m |
| Spawn → dağın eteği | 3 168 m |
| Bisikletle (19 km/s) | **10.0 dakika** |
| `road` | 193 nokta, 11.6–12.0 km (ova yolu) |
| `branches` | 2804 nokta, 7.7–11.6 km — yoldan dağa **üç kol** |
| `camps` | 3 kamp, 7.75–8.91 km | 
| `shops` | 1 dükkân, 11.4 km |

Zirve kotu korunur (~5709 m, `terrainHeight` tavanı 6189). "Arazi 36 km'ye büyütülsün"
önerisi **ölçmeden** söylenmişti, geri alındı — spawn eksende sanılmıştı, oysa köşede.

### Asıl sorun ölçek değil YAPI

| zirveden | kot | dilim eğimi |
|---|---|---|
| 1 000 m | 4 663 m | 56.8° |
| 2 000 m | 3 551 m | 48.0° |
| 4 000 m | 1 994 m | 34.3° |
| 6 000 m | 952 m | 25.1° |
| 8 408 m (etek) | 186 m | 21.1° |

Eğri içbükey, yani doğru. Sorun sayılar değil **her azimutta aynı olmaları** — radyal koni
(`secondaryPeaks: 0`), yükseklik yalnız merkeze uzaklığın fonksiyonu. Üst 2 km her yönde
48–57°: sürekli Eiger Kuzey Duvarı, yürünecek hat yok.

Gerçekte **sırt** 25–30° (rotalar oradan), **duvar** 50–60° (bakılır, çıkılmaz); Everest'te
Güney Kol'dan zirveye ortalama 29°. "Yapay görünüyor" ve "yürünmüyor" tek kökten: sırt/duvar
ayrımı yok. Divide Tree tam olarak bu ayrımı üretiyor.

### Üç bantlı mesafe temsili (gezegen boşluğu)

Ufuk `sqrt(2Rh)`: ova 186 m → 48.7 km, 2000 m → 159.7 km, zirve 5709 m → **269.9 km**.
Arazi 8.76 km, yani zirvede **31 kat** eksik — belirti her kotta ufukta gezegen boşluğu.

| bant | mesafe | temsil | çarpışma |
|---|---|---|---|
| oynanan | 0–18 km | Unity Terrain, 4.28 m/örnek | var |
| çevre | 18–60 km | kaba mesh, ~100 m/örnek | yok |
| ufuk | 60–300 km | çok kaba mesh, ~1 km/örnek | yok |

Çevre + ufuk tek mesh, tek çizim çağrısı, ~150 bin köşe, gölge yok. Bir kez pişirilir,
oyuncu yaklaşamayacağı için LOD/akış yok.

**Ucuzluğun sebebi numara değil fizik:** 100 km ötedeki dağ hava perspektifinden zaten mavi
siluet (PBSky kurulu), 60 km'de ~200 m / 300 km'de ~1 km köşe aralığı piksel altına düşüyor.

**Eğrilik zorunlu.** Zemin düşüşü: 20 km → 31 m, 60 km → 282 m, 270 km → **5 715 m**. Düz
kurulursa uzak dağlar ufuk çizgisinin üstünde yüzer. (270 km'deki 5715 m ile zirvenin
5709 m'si örtüşüyor — zirveden tam 270 km görülmesi bu yüzden.)

**Mesh, gökyüzü dokusu değil.** Oyuncu dikeyde 5.5 km yükseliyor, ufuk 49 → 270 km açılıyor;
tırmandıkça ufkun ardından yeni dağlar doğuyor. Boyalı skybox bunu yapamaz.

**Uzakta ağaç nesne değil renk** — ~2 km ötesinde orman arazi dokusunun tonudur. Bu kural
konmazsa uzak bantlar ucuz olmaktan çıkar.

### Tek jeneratör, geniş bölge, üç çözünürlük

Üretilen bir dağ değil **bir bölge**: ~540 km kare, oyun alanı merkezdeki 17.5 km. Argudo'nun
ölçeği zaten bu; 540 km verilirse silsile üretir — "etrafta farklı dağlar olsun" isteği
yöntemin doğal çıktısı. Üç bant aynı alandan örneklendiği için dikiş yok.

**Her yer dağ olmayacak.** 360° zirve duvarı tuhaf görünür; gerçek silsilenin etrafı çeşitli
(Everest'in kuzeyi Tibet platosu). Argudo kendi başına istatistiğe uyup her yere zirve
serper — yön yön karakter dağılımı (dağlık/tepelik/ova/plato) ona ayrıca verilir. Ayar değil,
üretimin girdisi.

### Yapı araziye uyar, arazi yapıya değil

"Kamp ve konak için arazi düzleştirilecek" denmişti, **yanlış**. Dağ yapıları eğime kurulur:
Namche Bazaar teraslarla, dağ evleri taş sekilere ve istinat duvarlarına.

**Kural:** bina kendi temelini taşır, arazi olduğu gibi kalır. Kazandırdığı: yeniden üretim
güvenli, daha gerçekçi, seki/istinat duvarı karakter veriyor.

Yerleştirme ölçütü "düz yer bul" değil **"uygun eğim bandı bul"** (~10–30°); çadır için
3×2 m'lik <15° her yerde var. **Asıl kısıt eğim değil güvenlik** — kaya düşme hattına, çığ
oluğuna, kar birikme çanağına yapı konmaz. L2'nin işi: eğim, bakı, korunaklılık, çığ.

### Üretim son söz değil: çapalı düzeltme işlemleri

Bilinen tek gerçek ihtiyaç **sırtın yürünebilir sürekliliği** — erozyon sırtta kopukluk
bırakabilir. Düzeltmeler yükseklik haritasına **elle yapılmaz** (yeniden üretimde silinir);
L0'a çapalanmış işlem olarak saklanır ("47–48 boyunları arasındaki kopukluğu kapat") ve
üretimden sonra otomatik uygulanır. Boru hattı: **üret → çapalı işlemleri uygula → pişir.**

Kamp ve konak bu listede yok — yapı araziye uyduğu için düzeltme gerektirmiyorlar.

### Sınır doğal olacak

Oyuncu arazinin kenarına ulaşıp çarpışmasız banda girmemeli. Sınır görünmez duvar değil
**arazinin kendisi**: nehir, yarma, buzul çatlak alanı, uçurum hattı.

**Tetikleyici:** ufukta boşluk görülürse ya da uzak dağlar ufuk çizgisinin üstünde
yüzüyorsa buraya bakılır.

---

## L0 girdisi: Everest bölgesi, mesafeye göre prominence eşiği (2026-08-17)

Plan: `.claude/PRPs/plans/terrain-l0-divide-tree.plan.md`.

**Kaynak veri.** `orometry-terrains-master/data/` içindeki dosyalar repoda **sahte** (yalnız
Drive bağlantısı). Gerçekleri `Desktop/tts/specs/terrain`'de, **repoya girmiyorlar**:
`alliso-sorted.txt` 1.1 GB / 24 749 538 satır, `prominence-p100.txt` 331 MB / 7 798 709
satır. Sütunlar: enlem, boylam, kot(ft), key saddle enlem/boylam, prominence(ft).

**Bölge: `himalaya-everest`**, merkez `[27.8575, 86.8267]`. 100 km yarıçapta 5 230 zirve,
0.131/km², en yüksek beş 8840·8480·8440·8370·8348 m, prominence ortancası 70 m.

Gerekçe: orometrisi "bir baskın dev + kademeli komşular + bir yanda yüksek plato (Tibet)"
karakterinde — "her yer dağ olmayacak" kuralına doğal uyum. Ayrıca **yeşil→kar** isteğini
karşılayan tek aday: Khumbu'da 2500–4000 m rododendron ormanı, ağaç sınırı ~4000 m, kar
çizgisi ~5200 m. (Karakurum baştan çıplak; Alpler hem alçak hem baştan sona yeşil.)
**Sınır:** bölge seçimi yalnız şeklin istatistiğini veriyor; bitki örtüsü ve kar çizgisi
L2 ile yüzey malzemelerinin işi.

**Prominence eşiği mesafeye göre kademeli.** Ham yoğunluk 540 km'ye taşınınca 38 127 zirve
çıkıyor, sentez saatler sürüyor.

| bant | alan | eşik | zirve |
|---|---|---|---|
| oyun alanı 17.5 km | 306 km² | 0 m (tam detay) | ~40 |
| çevre 18–60 km | 11 000 km² | 100 m | ~490 |
| ufuk 60–270 km | 217 700 km² | 300 m | ~1 920 |
| | | **toplam** | **~2 450** |

Gerekçe fizik: 100 km'de bir ekran pikseli ≈ 47 m, 300 m'den alçak tümsek zaten
çözülmüyor. 15 kat azalma, görsel kayıp yok.

**Ölçek bağımlılığı:** eşikler mutlak metre (prominence tanımı dağın boyuna bağlı değil);
bant sınırları ufuk mesafesinden, yani gezegen yarıçapından türüyor.

**Tetikleyici:** ufukta dağlar seyrek ya da kalabalık görünüyorsa, ya da zirve sayısı
patlarsa buraya bakılır.

---

## Referans boru hattı uçtan uca koşturuldu — bulgular (2026-08-17)

Unity'ye tek satır yazmadan önce yazarların kodu kendi verisiyle koşturuldu. Ortam:
Python 3.11.9 + numpy · scipy · scikit-image · POT · pandas · shapely · triangle. `noise`
paketi Windows'ta derlenmiyor; aynı imzada Perlin yedeği yazıldı.

| adım | süre | çıktı |
|---|---|---|
| Bölge kesme + birleştirme | ~1 dk | 7 330 zirve |
| L0 `synthDivideTree` | **25 sn** | 949 zirve, 948 boyun, 1809–8562 m |
| Poisson (90×90 km, r=0.18) | 26 sn | 151 093 örnek |
| L1 `divideTreeToMesh` | **14 sn** | 172 332 köşe, 344 583 üçgen |
| DEM rasterleme (30 m/px) | 5 sn | 3000×3000, 228–8553 m |

**Kanıtlandı:** makro yapı doğru (dallanan sırtlar, vadiler, tutarlı akarsu ağı, doğru
drenaj) · `probMap = 0` gerçekten kesiyor, o bölge ovaya döndü — "her yer dağ olmayacak"
mekanizması uçtan uca doğrulandı · sırt sürekliliği var · `MountainGenerator.Erode`
Python'a birebir çevrildi, 3000²×20 iterasyon 10 sn.

**Kanıtlanmadı — asıl risk: yüzey detayı.** Argudo iskeleti ve vadileri veriyor, yüzeyi
vermiyor; erozyon kodu repoda yok, o parça Galin 2019'un işi ve yayınlanmamış. Bir deneme
başarısız oldu: çok oktavlı gürültü + mevcut erozyon, sonuç daha pürüzsüz çıktı — 46°
talus × 20 iterasyon 30 m ızgarada eklenen detayın tamamını sildi.

**Ders:** detay katmanı takılan değil **tasarlanan** bir şey. Frekans içeriği, genliği ve
nereye uygulanacağı hesaplanır; yer tutucu gürültüyle geçiştirilemez.
(Sonradan doğrulandı — bkz. `SYMPTOMS.md`, Nyquist alias.)

**Ovanın kusuru:** zirve konmayan bölge fazla düz çıkıyor, mesh uzak noktalar arasında düz
interpolasyon yapıyor. Kullanıcının tarifi "tepecikli düz ova"; çare alçak prominence'lı
tepecikler ve/veya detay katmanını oraya da uygulamak.

**Not — söküm tablosu geçersiz.** Bu kayıtta `Erode`, teraslar ve çok oktavlı gürültünün
`MountainGenerator`'da kalacağı yazıyordu. Sonradan arazi içeriğinin tamamı pişirilmiş
yükseklik haritasından geliyor; jeneratörün prosedürel çıktısı araziye hiç girmiyor
(`SYMPTOMS.md`, "menü düğmesi haritayı hiç uygulamıyordu").

**Tetikleyici:** yakın planda üçgen yüzeyler görülüyorsa detay katmanı eksik demektir.

---

## Ovanın kotu KAPANDI: 186 m kalıyor, sıcaklık değişti (2026-08-17)

**Ölçüm.** Oyun alanı 4097², kot 517–5709 m, ortanca eğim **38.2°**, ova yok.
Eğim payları: yürünür (0–15°) %6.7, dik yürünür %24.5, el-ayak %35.6, tırmanış %33.2.

**Çelişki.** Zirve 5709 m, ova 186 m, arası 9 km → ortalama iniş 32°. Gerçekte yok ve
Argudo üretemez (istatistik gerçek dağlardan). Everest'te Base Camp 5364 m, en yakın ova
100+ km ötede. Ama kullanıcının tarifi ("tepecikli düz ovada başla", "yeşillik, sonra
kar", bisikletle 10 dk) **kot söylemiyor** — 186 m arazinin sayısı, tasarım kararı değil.

**KARAR: A — ova 186 m kalıyor, sıcaklık değişiyor.** Önce B (ova 2400 m, Khumbu vadisi
karşılığı) öneriliyordu; **bir kısıt yanlış varsayıldığı için değişti** — sıcaklık modeli
sabit sanılmıştı, oysa `seaLevelCelsius` tek bir serbest sayı.

İkisinde de ovada +6.5 °C olacak şekilde ayarlanırsa:

| | ova 186 m | ova 2400 m |
|---|---|---|
| Gereken `seaLevelCelsius` | **+7.8** | +22.1 |
| Kar çizgisi | 1 200 m | 3 400 m |
| **Zirvede** | **−29.3 °C** | −15.0 °C |
| **Tırmanılacak dikey** | **5 523 m** | 3 309 m |
| Ortalama eğim | 33.3° | 21.5° |

186 m tırmanışı %67 uzatıyor (oyunun adı bu), zirveyi gerçekten öldürücü yapıyor
(−29 °C, rüzgârla −38 °C hissedilen) ve mevcut hiçbir şeyi bozmuyor — spawn, yol, üç kol,
kamplar, `SCALE.md` yerinde. **Ödenen:** oyun alanı ortalama 33°, Argudo'nun kendiliğinden
üretmeyeceği bir diklik; ama oyun alanı bölgenin %0.1'i ve uzak bantlardan görünmez.

**Uygulanan: `seaLevelCelsius` −3 → +7.8.** −3 donma seviyesini deniz seviyesinin 462 m
ALTINA koyuyordu: dağın tamamı donmuş, her kotta kar, "başlangıçta yeşillik ve yağmur"
**imkânsız**. +7.8 → donma seviyesi 1200 m (= 1200 × 6.5 °C/km); ova +6.6 °C ve yağmur,
sulu kar 1200–1422 m, saf kar üstünde. Tam fırtına donma seviyesini 500 m indiriyor.

`Game.unity:2166` serileştirilmiş −3 taşıyordu, o da güncellendi. Mimari değişiklik
gerekmedi (`AltitudeWeatherDriver.UpdateFreezingLevel` zaten `FreezingLevel` okuyor).

**DOĞRULANMAMIŞ:** yağmur tavanı −368 m'ydi, yani oyunda bugüne kadar hiç yağmur yağmadı.

---

## Yüzey detayı ÇÖZÜLDÜ — tarif ve dersler (2026-08-17)

Referans repoda erozyon kodu yok (yayınlanmamış). Spec'ten yazıldı: `Tools/terrain/detail.py`.
Sonuç: 4.28 m/örnek, zirve **tam 5709 m** korunuyor, düğüm kotlarında ortanca kayma 17 m
(prominence tavanı 65 m'nin altında).

**Tarif — üç parça, üçü de gerekli:**

1. **İnce taban mesh**, `refineDistance` 120 → **30 m**. Asıl kırılma noktası: gürültü
   genliği prominence tavanıyla sınırlı (65 m) ve 320 m dalga boyunda ancak 18° eğim
   üretiyor, yüzeyler 30–50°. **Gürültü yüzeyleri kıramaz, üstünde gezer.**
2. **Multifraktal gürültü** (§3.5), genlik 52 m < prominence tabanı 65 m (§5.7). Her oktav
   **döndürülerek** örnekleniyor (§3.6) — hizalı oktavlar ızgara artefaktı üretir.
   `× (1 − U)` ile çarpılıyor: zirve ve boyunlarda sıfır, kot birebir korunuyor.
3. **Çok ölçekli kısıtlı erozyon** (§5.8), telafi edici uplift `g(r) = (1−r²)³`,
   `R_infl` 400 m.

**Dört ders — üçü hatadan:**

- **"100/50/30 m" IZGARA ÇÖZÜNÜRLÜĞÜDÜR**, iterasyon sayısı değil. Ölçüldü: 46.1° talus ×
  4.28 m hücre = 4.45 m'lik maksimum adım, eklenen detayın tamamı o eşiğin üstünde ve
  erozyon onu baştan yiyordu. Doğrusu: kaba ızgaraya indir, aşındır, farkı geri büyüt.
- **Farkın geri büyütülmesi pürüzsüz olmalı.** `np.repeat` dikdörtgen ızgara artefaktı
  çıkardı (`SYMPTOMS.md` "sert kırpma" sınıfı); kübik spline kullanılıyor.
- **Spec'in uplift işareti yanlış.** `H_{i+1} = E_i + U·ΔH_i, ΔH_i = E_i − H_i` — erozyon
  alçalttığına göre `ΔH < 0` ve bu daha da alçaltıyor; zirvede (U=1) sonuç `2E − H`, yani
  erozyonun iki katı. Uygulanan: `H_{i+1} = (1−U)·E_i + U·H_i`.
- **`α` fonksiyonu makalede YOK** (spec §9.1 açık nokta). Seçilen: o ana kadar birikmiş
  değerin `[0,1]`'e normalize hâli — makalenin tarif ettiği **davranışı** veriyor (vadi
  düzleşir, zirve detay kazanır). Formül değil, seçim; burada kayıtlı.

**Açık:** düğüm kotlarında en kötü kayma **172 m**, prominence tavanının üstünde. Kaynağı
100 m ızgarada aşınan ve `R_infl` 400 m'nin dışında kalan bir düğüm. `R_infl`'in ölçekle
büyümesi gerekebilir; ölçülmedi.

**Tetikleyici:** yakın planda üçgen yüzey görülüyorsa `refineDistance`'a bakılır,
gürültüye değil.

---

## Yaklaşma koridoru: ova oyun alanına giriyor (2026-08-17)

Ova 186 m'de kalınca maskenin onu oyun alanına sokması gerekti. Güneybatıya (212°) inen tek
bir **koridor** — azimut sektörü değil, eksene uzaklık; sektör pasta dilimi üretiyor
(bir kez yaşandı).

| bant | kot | ortanca eğim | yürünür |
|---|---|---|---|
| ova 10–14 km | 407–688 m | **6.3°** | **%91** |
| etek 8–10 km | 324–1414 m | 10.1° | %72 |
| yamaç 6–8 km | 355–2051 m | 23.2° | %37 |
| kütle 0–4 km | 1623–5709 m | 42.7° | %12 |

Ova "hafif tepecikli düz" — istenen buydu; spawn 186 m'de, bisiklet turu bozulmadı.

**Tırmanılabilirlik: rota VAR.** Alan ortalaması 4–6 km bandında 48.7° verip duvar
sandırdı — **yanlış araç**. En-az-maliyetli hat (Dijkstra, maliyet `mesafe × (1+(eğim/25)⁴)`):
17.56 km yol (düz mesafe 8.41 km, %109 zikzak), ortanca **18.1°**, %90'lık dilim 26°, en dik
adım 42.2°, teknik tırmanış (>45°) **%0**. Gerçek dağ da böyledir: yüzler dik, rota sırttan
gider ve iki katı yol yürür. Oyuncu rotayı **bulmak** zorunda, zorlanmak zorunda değil.

**Geri alınan deneme.** Koridorun tepesi kütleye "otursun" diye doğrusal rampa yazıldı;
ölçüm kötüleşti, geri alındı — duvar kalkmadı, dışarı itildi (yamaç 6–8 km: 23.2°/%37 →
**52.4°/%8**). Sebep: rampa `t=3`'ten başlıyor, 6–8 km hâlâ kütle kotunda kalıyor.

---

## L0 uygulandı — yöntemin ölçülmüş sınırları (2026-08-17)

Plan `.claude/PRPs/plans/terrain-l0-divide-tree.plan.md`, araç zinciri `Tools/terrain/`,
çıktı `Assets/Terrain/DivideTree.txt` (7268 zirve, 7267 boyun, tohum 36044). Dördü
düzeltildi, biri açık.

**Yükseklik ZİRVELERDEN gelir, `elevMap`'ten değil.** `elevMap` yalnız zirvenin **nereye**
konacağını söylüyor; zirvesiz bölgede yükseklik bilgisi yok ve arazi tabana çöküyor.
Ölçüm: plato 3600 m tasarlandı, yoğunluk 0.06 verilince arazide ortanca **103 m** çıktı;
yoğunluk yükseltilince 1894 m'ye geldi. **Kural:** bir bölgenin belli kotta durmasını
istiyorsan oraya zirve koymak zorundasın.

**`probMap`'in ilk indeksi X'tir.** `divtree_synthesis.py:24` → `normCoords[:,0]` X.
`[kuzey, doğu]` kurulursa silsile **90° dönük** çıkar. Bir kez yaşandı.

**Histogram eşlemesi uygulanmıyor.** Yazarların `mapToPDF` adımı kontrol görselleri
birimsiz olduğu için zorunlu; bizim `elevMap`'imiz **metre** ve tasarımdan geliyor
(ova 186, plato 3600, zirve 5709). Eşlemek bozuyor — bir turda plato 3600 → **1127 m**.

**Dağılımın tavanı zirvenin ALTINDA olmalı.** Tavan zirveyle eşit olunca prominence/
dominance adımı sentezlenmiş bir zirveyi **5789 m**'ye çıkardı ve bizimki üçüncü sıraya
düştü. Tavan 5500 m'ye çekildi; zirve artık inşaat gereği 209 m üstte — Everest 8840 ile
Lhotse 8516 arasındaki 324 m'nin bu ölçekteki karşılığı.

**AÇIK: gerçek bir plato bu yöntemle çıkmıyor.** Yoğunluk düşükse ada ada kopuyor, yüksekse
dağlıktan ayırt edilemiyor. Sebep vadi oyma derinliği (`maxSlopeCoeff`) ve sırt–akarsu
mesafesi, yoğunluk değil. Şimdilik yoğunluk 0.60 ile "yüksek tepelik"; manzara olduğu için
(120 km+) kabul edilebilir ama **çözülmüş değil**.
**Tetikleyici:** kuzey ufkunda plato yerine sıradan dağ görülüyorsa buraya bakılır.

**Referans kodunun iki sınırı — dışarıdan aşıldı, kaynağa dokunulmadı:**
- `noise` paketi Windows'ta derlemiyor; aynı imzalı Perlin yedeği (`Tools/terrain/noise.py`).
  Gürültü `elevMap`'e %5 ağırlıkla giren bir sarsıntı, türü sonucu belirlemiyor.
- `fixedPeaks` tek satırda `squeeze()` ile sıfır boyuta düşüp `concatenate`'i patlatıyor.
  İkinci sabit zirve konuldu — zaten doğrusu: Everest'in yanında Lhotse var.

**Kimlik = dizi indeksi; `readDivideTree` KULLANILMAZ.** Referansın okuyucusu kırpma
sınırında düğümleri yeniden sıralıyor (`peakReorder`, `saddleReorder`); içerik çapaları
kimliklere bağlı olduğu için diziler doğrudan yazılıp doğrudan okunuyor.

---

## Şimşek kolu: R&W yükseltilir, DBM ERTELENDİ (2026-08-19)

**Karar.** Kol üretimi Reed & Wyvill istatistiklerine çekilecek; dielektrik kırılma
modeli (DBM) yazılmayacak.

**Araştırma — DBM ne veriyor.** `[Kim & Lin 2004, Pacific Graphics]` hedeflemeyi ayrı
bir özellik olarak değil modelin doğal parçası olarak veriyor: ızgarada elektrik
potansiyeli φ tutulur, başlangıç (bulut) φ=0 negatif, hedef (zirve) φ=1 pozitif sınır
koşuludur, ara Laplace'tan (∇²φ=0) çözülür, büyüme `p_i = φ_i^η / Σφ_j^η` olasılığıyla
ilerler. Makalenin sözü: ark herhangi bir negatif bölgeden başlayıp herhangi bir pozitif
nesnede sonlanabilir. Bedava gelenler: η dallanmayı sürer (1 yoğun, 3 düz, kullanışlı
aralık 1-4), engel içi φ=0 yapılınca ark iter, geri vuruşlar Poisson'a (∇²φ=−4πρ) geçip
önceki kanala artık yük bırakarak aynı yolu izler.

**Neden yazılmıyor — TALEP YOK.** `DESIGN.md`'de şimşek hiç geçmiyor: tehlike mekaniği
yok, belirli bir zirveye vurması oynanışın istediği bir şey değil. §9.1.4 makalenin
kendi eksik listesinden geliyor, bizim ihtiyacımızdan değil.

İkinci sırada maliyet/görünürlük: kol YALNIZ yakın çakmalarda ve <0.5 sn çiziliyor;
DBM ise 3B ızgara + Laplace/Poisson çözücü + pişirme hattı + her çakmada hedefe uydurma
demek. R&W yükseltmesi birkaç düzine satır C#. Ayrıca port ettiğimiz makale (Dobashi)
üç problemini AYDINLATMA üzerine kuruyor ve kol geometrisini bilerek R&W'ye devrediyor.

**"Gerçek zamanlı değil" TEK BAŞINA gerekçe DEĞİL** — bu kayıt bir dönem öyle diyordu ve
yanlıştı: proje zaten içerik pişiriyor (bkz. "Dağ pişmiş İÇERİK"). Kollar da editörde
üretilip asset olarak saklanabilirdi. Eleme sebebi hız değil, talebin olmaması.

**Yerine ne yapılacak.** Hedefleme için Laplace çözmek şart değil: lider hedefe
doğrultulur ve maksimum segment açısı küçültülür, böylece istenen mesafeyi istenen yönde
kat eder. Kol üretimi R&W'ye çekilir — dallar ana koldan ortalama **16°** sapar (normal
dağılım), dallanma ÖZYİNELEMELİdir ve her kuşakta özellikler çarpanla azalır:
yarıçap ×0.5, dallanma olasılığı ×0.8, dal uzunluğu ×0.5, maksimum segment açısı ×1.3
(dal ebeveynden daha kıvrımlı olur). Mevcut `LightningBolt` bunların üçünü de yapmıyor.

**R&W'nin PARLAMASI alınmıyor** — `G = Σ g·l·e^(−(d/W)²)` Gauss, faz açısı yok; Dobashi
"sezgisel, gerçek fizikten farklı" diye tam bunu eleştiriyor. R&W'den geometri alınır,
parlama alınmaz.

**Tetikleyici.** Şimşeğin belirli bir zirveye vurması OYNANIŞ gereği olursa (anlatı
çapası, tehlike mekaniği) DBM yeniden açılır — ama önce ucuz hedefleme denenir. Hız
gerekirse devam çalışması var: Kim & Lin 2007, *Fast Animation of Lightning Using an
Adaptive Mesh*, TVCG 13:390-402.

**Maliyet.** R&W yükseltmesi küçük: yalnız C# geometri, shader yok, bütçe riski yok.
DBM büyük: ızgara, Laplace çözücü, ve gerçek zamanlı olmadığı için pişirme altyapısı.

## Şimşek–yağış etkileşimi ERTELENDİ (2026-08-19)

**Karar.** Şimşek flaşı şu an yağan kar tanelerini ve yağmur izlerini aydınlatmıyor.
Bilerek bırakıldı.

**Gerekçe.** `lightning-spec.md` §9.3.5 bunu açık nokta olarak işaretliyor ve Dobashi
makalesinde hiç ele alınmamış. Kar ve yağmur spec'leri yeniden yazılacak; şimdi
bağlanırsa o yazımda ikinci kez sökülecek.

**Nereye bağlanacak.** `snow-spec.md` §7 ve `rain-spec.md` §6.3.3'te tane başına ışık
kaynağı işleme yeri var — şimşek oraya ÜÇÜNCÜ kaynak olarak girer (güneş ve gök zaten
var). Tane kendi rengini seçmiyor, üstüne düşen ışığı saçıyor; bu yüzden `_LightningFlash`
globalini okumak yeterli, ayrı bir renk ayarı açılmayacak (`RATIONALE.md` → Yağış: "tane
kendi rengini seçmez").

**Tetikleyici.** Kar veya yağmur spec'ine geçildiği an. Fırtınada şimşek çakarken
tanelerin kararması ya da flaşa hiç tepki vermemesi belirtisi de aynı kaydı açar.

**Maliyet.** Küçük: tane aydınlatması zaten bir ışık toplamı, dördüncü terim eklemek.
Asıl iş şimşeğin çakma anının o sistemlere ulaştırılması — global zaten yazılıyor.

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
