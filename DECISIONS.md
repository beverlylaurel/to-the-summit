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

## Bekleyen ölçümler

- **Güneş 3.03'e çıktıktan sonra yüzeyler** — arazi, kar ve bisiklet 1.5'e göre
  ayarlanmıştı; yeniden ayar gerekip gerekmediğine bakılmadı
  → [Güneş şiddeti pakete kalibre edildi](#güneş-şiddeti-pakete-kalibre-edildi-15--3030782)
- **Yıldızların dönüş yönü** — shader arama yönünü döndürdüğü için açı negatif verildi,
  ekranda doğrulanmadı; ters akıyorsa düzeltme tek işaret
  → [Gece tamamlandı](#gece-tamamlandı-yıldızlar-geldi-ay-ikincil-kaynak-oldu)
- **Bulutların gece görünümü** — ay bulutları yalnız ortam ışığından aydınlatıyor,
  doğrudan ışık maliyeti ölçülmedi
  → [Bulutlar ayı doğrudan almıyor](#bulutlar-ayı-doğrudan-almıyor--maliyeti-ölçülmedi)
- **Gece seviyesi sisin üstünde ayarlandı** — sis yeniden yazılınca `MoonIntensity` ve
  gece profili yeniden değerlendirilir; sisin katkısı çıkınca gece koyulaşacak
  → [Gece seviyesi sisin üstünde ayarlandı](#gece-seviyesi-sisin-üstünde-ayarlandı--sis-yenilenince-tekrar-bakılacak)
- **Hava perspektifi + yükseklik sisi birlikte** — paketin atmosferik saçılımı açık,
  bizim yükseklik sisimiz de duruyor; ikisinin üst üste binip binmediği bakılmadı
  → [Paketin sisi kapalı başlıyor](#paketin-sisi-kapalı-başlıyor)

- **`DepthNormals` fragman maliyeti** — `SnowDisplacedNormal` fragman başına çağrılıyor
  ve üç `SnowDisplacement` açıyor: piksel başına dokuz doku okuması (birikim ağırlığından
  önce altıydı). Hiç ölçülmedi; gradyanın köşeye taşınması ya da geçişin kapatılması
  masada
- **Kare süresi jitter'ı** — editörde ölçüldü, patoloji çıkmadı; aynı ölçüm bir
  **derlemede** tekrarlanacak. (Ayrıntı kaydı yok: indeks bir kayda bağ veriyordu ama o
  kayıt dosyada hiç yazılmamış. Bağ kaldırıldı, madde kendi kendine yeter.)
- **Işından bağımsızlık için ödenen kare süresi** — üç ucuzlatma söküldü, bedeli
  ölçülmedi
  → [Geçirgenliğe bağlı üç ucuzlatma söküldü](#geçirgenliğe-bağlı-üç-ucuzlatma-söküldü--bedeli-ölçülmedi)

---

### Geçirgenliğe bağlı üç ucuzlatma söküldü — bedeli ölçülmedi

**Karar (2026-08-14).** Yoğunluk alanı ve gölge sondası artık görüş ışınının
geçirgenliğine bakmıyor. Sökülenler:

| ne | eşik | kazandırdığı |
|---|---|---|
| `deep` — tepe tümseği, büyük oktav, 3B büküm okunmuyordu | `< 0.35` | 3 adet 3B okuma |
| sonda aralığı — ışık sondası iki adımda bir | `< 0.25` | sonda başına ~35 doku okuması |
| sonda kademesi — erozyon katmanı okunmuyordu | `<= 0.70` | 2 adet doku okuması |

**Gerekçe.** Üçü de eşiğin geçildiği yüzeyi ekranda çiziyordu: bulutun ortasında
kesilmiş düz beyaz ada, kenarda koyu zar, ikinci halka ailesi. Ekran görüntüsüyle
doğrulandı.

**Bedel ölçülmedi.** Kazanç da kayıp da tamamen bulutun **iç** kısmında, yani kare
süresinin zaten en kötü olduğu yerde (bulutun içinde ~40 FPS). Sökme bu sayıyı
düşürür, ne kadar bilinmiyor.

**Tetikleyici.** Bulut içinde kare süresi kabul edilemez olursa geri dönülür — ama
aynı eşiklerle değil. Ucuzlatma **ışından bağımsız** bir ölçüte bağlanır: mesafe, LOD,
ya da yürüyüşün kendi kademesi. Işına bağlanan her ucuzlatma kenar üretir.

---

### Şafağın altın ucu el sabitinde kaldı — bilinçli abartma

**2026-08-12 güncelleme:** Sabitin PARLAKLIĞI el ayarında kalıyor ama TONU artık güneşin
yüksekliğini izliyor: ufka yaklaştıkça altın kızıla dönüyor `(0.9, 0.52, 0.11)` →
`(0.85, 0.20, 0.05)`. Tek sabitken güneş ufkun on derece üstündeyken de dibindeyken de
aynı sarıyı veriyordu ve batımda kızıllık hiç gelmiyordu. Fizik gerekçesi: kızıllık yol
uzunluğundan doğar, güneş alçaldıkça mavi ve yeşil süpürülür.

**Karar:** `AirColor`'daki `gold = (0.9, 0.52, 0.11)` ve bulut aydınlatmasındaki
`golden = (1.0, 0.9, 0.55)` açık yazılı sabitler olarak kalıyor (2026-08-11).

**Ölçüm.** Güneş yönünde, ufkun 2° üstünde, luminans:

| güneş | fizik | sabit |
|---|---|---|
| 0° (doğuş) | 0.152 | 0.571 |
| +2° | 0.338 | 0.571 |
| **+5°** | **0.569** | **0.571** |

**Gerekçe:** Altın saatte fizik sabite zaten eşit. Fark yalnız doğuş anında ve orada fizik
haklı — gerçek bir doğuş, yirmi dakika sonrasından sönüktür. Sabit o ilerlemeyi düzleyip
doğuşu 3.7 kat parlatıyor. Bu bir model eksiği değil, **bilinçli abartma**: oyunun istediği
şafak gerçeğinden gösterişli.

**Çürütülen hipotezler** (tekrar denenmesin diye):
- *Aerosol eksik.* Denendi ve ölçüldü: Mie katsayısı 10×/20×/40× yapıldığında doğuş ufku
  0.152 → 0.101 → 0.040 → 0.007, yani **sönüyor**. Teğet yolda eklenen aerosol hâleyi
  güçlendirmekten çok huzmeyi yutuyor. Alacakaranlık sönümünü de düzeltmiyor
  (−6°/0° oranı 0.0406 → 0.0366; gerçek 0.0085).
- *Yürüyüş kaba.* 16 → 128 adım doğuşta yalnız %18 kazandırıyor (0.152 → 0.180).
- *Örnek ufka çok uzak.* 2° → 1° denendi: parlaklığı yakalıyor ama kontrastı sertleştirip
  bulutları beyazlatıyor, gökyüzünü karartıyor. Geri alındı.

**Tetikleyici:** Doğuş anının fizikle çizilmesi istenirse önce bu üçünün dışında bir
mekanizma bulunmalı — bulunmadan sabit kaldırılmaz. Sabit yalnız güneşin TAM azimutunda
baskın (`pow(sunDot, 1.8)` ile sönüyor), çeper zaten fizik örneğinden geliyor ve saatle
ilerliyor.

**Maliyet:** Doğuş anı gerçeğinden parlak. Altın saat ve sonrası fizikle uyumlu.

---

### Alacakaranlığın derin ucu hâlâ eksik — Ψ_ms açı ekseni 48'de kaldı

**Karar:** Çok saçılma tablosunun açı ekseni 24 → 48'e çıkarıldı ve sivil alacakaranlık
düzeldi; daha derini (−8° altı) düzeltilmedi (2026-08-11).

**Ölçüm** — doğuştaki değere oran:

| güneş | önce (24) | sonra (48) | gerçek |
|---|---|---|---|
| −2° | 0.335 | 0.352 | 0.199 |
| −4° | 0.106 | **0.055** | 0.040 |
| −6° | 0.0406 | **0.0039** | 0.0085 |
| −8° | 0.0000 | 0.0003 | 0.0020 |

**Ayrıştırma:** kazancın tamamı açı ekseninden. Yön sayısı (16→32), adım sayısı (12→24)
ve kot ekseni (16→24) ölçüldü, **hiçbiri değiştirmiyor**. 48'in ötesi de kazandırmıyor —
64 ve 96'daki oynama yakınsama değil, 16 yönlü örneklemenin gürültüsü. Atmosfer tavanını
60 → 100 km yapmak da neredeyse etkisiz (−6°'de 0.0406 → 0.0414).

**Kalan sapma:** −2°'de hâlâ 1.8 kat parlak, −8° ve altında ~6 kat sönük ve −12°'nin
altında tam sıfır. Sebep muhtemelen 16 yönlü küre örneklemesinin gürültü tabanı; yön
sayısını artırmak ölçüldü ve **açı ekseni kaba kaldığı sürece** işe yaramıyordu, ince
eksenle birlikte yeniden denenmedi.

**Tetikleyici:** Gece oynanışı derinleşir ve alacakaranlığın son yarım saati önem
kazanırsa; ya da −12° civarında sert bir kararma gözle görülürse. O turda yön sayısı
ince eksenle birlikte ölçülür.

**Maliyet:** Tablo kurulumu 2× (tek seferlik), bellek 9 KB. Gündüz etkilenmiyor:
güneş +5° ve üstünde fark %2.5'in altında.

---

### Ton eşleme ACES'te kaldı

**Karar:** URP ton eşlemesi ACES; Neutral'a geçilmedi (2026-08-11).
**Gerekçe:** ACES doygun turuncuyu kırpıp tonunu kaydırıyor — şafak bandının en parlak
yerinde olan tam bu. Neutral tonu korur ama ACES'in kontrast eğrisini getirmez; düz
geçiş "daha az sinematik" bir görüntü verir ve kullanıcı bunu reddetti. Kontrast telafisiyle
(ColorAdjustments) yapılabilir ama bu tek satırlık bir değişiklik değil, baştan bir
kalibrasyon turu.
**Tetikleyici:** Doygun turuncuda kırpılma/ton kayması gözle rahatsız edici hâle gelirse,
ya da renk kalibrasyonu için ayrı bir tur ayrılırsa.
**Maliyet:** Şafağın en parlak bandında ton doğruluğu ACES'in eğrisine bağlı.

---

### Oynanış mekaniği netleşmeden koda başlanmaz

**Karar:** Ekipman, kaynak ve tırmanma sistemlerinin hiçbiri yazılmaz; önce mekanik
tam ve net kararlaştırılır. Kullanıcı bunu açıkça istedi ve hatırlatma görevini
Claude'a verdi.
**Tetikleyici:** Kullanıcı bu sistemlerden birine başlamak istediğinde Claude önce
aşağıdaki açık soruların cevaplandığını doğrular.
**Açık kalanlar:** Ekipmanın envanterde nasıl durduğu, kamp/sığınakta ne yapıldığı.

---

### Tırmanma üç modlu, modu ZEMİN belirler

**Karar:** Hareket üç moda ayrılır ve hangisinin geçerli olduğunu arazinin kendisi
söyler — eğim ve kar/buz örtüsü (2026-08-11).

| mod | koşul | dayanıklılık |
|---|---|---|
| Yürüyüş / scramble | eğim < ~35° | yavaş erir |
| Krampon + buz baltası | karlı-buzlu, 35-55° | orta |
| El tırmanışı | kaya, > 55° | hızlı erir |

**Gerekçe:** Terrain zaten eğimi ve örtüyü biliyor; mod seçimi yeni veri üretmeden
oradan çıkar. 90-120 dakikalık koşunun gerektirdiği 92 m/dk dikey hız da ancak rotanın
çoğu birinci modda olursa tutuyor — teknik bölümler gerilimi yaratan yavaş noktalar.

**Ekipman:** Mevcut listeye (nut, piton, buz vidası, jumar) **buz baltası** ve
**krampon** eklenir.

**Alete geçiş oyuncunun işi.** Krampon takmak, baltayı çıkarmak zaman alır ve oyuncu
karar verir; otomatik geçiş değil. Buzlu yamaca kramponsuz girmek mümkün — ve tehlikeli.

**Düşüş gerçek.** Düşme hasarı yüksekliğe bağlı; yeterince yüksekten düşmek öldürür.
Ankraj bu yüzden hayat kurtarır, süs değil.

**Tutamaklar prosedürel.** Kaya yüzeyinden türetilir, elle yerleştirilmez — tek dağ
olmasına rağmen rota tasarımını elle döşemek zorunda kalmamak için.

**Açık kalanlar:** Tutamak üretiminin hangi veriden beslendiği (yüzey normali, gürültü,
malzeme maskesi); düşüş hasarının eğrisi; ipin fizik olarak mı yoksa kural olarak mı
modelleneceği.

---

### Co-op: ağ yok, mimari hazır tutulur

**Karar:** Tırmanma, envanter ve kaynak sistemleri tek oyuncu için yazılır; ağ katmanı
kurulmaz. Ama oyuncu durumu BAŞINDAN ayrı bir veri katında tutulur (2026-08-11).

**Somut kural:** Dayanıklılık, kalori, sıcaklık, envanter, ankraj gibi oyuncu durumu
`MonoBehaviour.Update` içinde, görünümle iç içe tutulmaz. Sade bir veri katında durur;
görünüm onu okur, girdi onu değiştirir. Ağ gelince o kat sarmalanır, yeniden yazılmaz.

**Gerekçe:** Önce ağ kurmak, "co-op'un şekli tırmanma mekaniğine bağlı" kaydının uyardığı
hatanın ta kendisi — mekanik yokken kurulan ağ modeli ya yeniden yazılır ya mekaniği
kısıtlar. Hiç düşünmemek ise en pahalı sistemlerin (oyuncu durumu taşıyanlar) baştan
yazımını riske atıyor. Orta yolun ek maliyeti neredeyse sıfır: `CLAUDE.md`'nin zaten
dayattığı ayrım (girdi → durum → görünüm, enjekte edilen bağımlılık, event'le konuşma).

**Tetikleyici:** Co-op fiilen istendiğinde `COOP.md` envanteri açılır; bu karar o zaman
silinir.

**Maliyet:** Ertelendiği sürece sıfır. Kural çiğnenirse (durum görünümün içine sızarsa)
maliyet geri gelir — o yüzden kural, karar kadar önemli.

---

### Alçak güneş kısıcısı — şafak fizikten değil, karardan

**Karar:** Işın-küre kesişimi tamamen bırakıldı; yer kesişimi AÇIYLA soruluyor. Alçak
güneşte şafağın ölçülü kalması ayrı ve sürekli bir kısıcının işi (2026-08-11).

**Neden gerekti:** Küre hesabı gezegen ölçeğinde güvenilmez — `|origin|² − R²` iki 4·10¹³
sayının farkı, float32 adımı ~4·10⁶. Gözlemci deniz seviyesindeyken sonuç yuvarlama
gürültüsüydü ve işareti güneşin yüksekliğine göre değişiyordu: güneş 27°'de çalışıyor,
29°'de "yere çarptı" sayılıp huzme sıfırlanıyor, sahne kararıyordu.

**Ama düzeltmek görünümü bozdu.** Beğenilen şafak, o gürültünün ufuk örneklerini
sıfırlamasının ürünüymüş: güneş 0°'de fizik (0.315, 0.099, 0.048) veriyor, gürültülü hâl
(0.000, 0.000, 0.000). Fizik açılınca şafak kızıla kayıyor.

**Çözüm iki parçalı:**
1. Kesişim açıyla sorulur — deterministik, donanımdan bağımsız.
2. `Atmosphere.LowSunFade` sürekli bir çarpan: ufukta 0, 5°'de 1. HUZME, GÜNEŞ RENGİ,
   AY RENGİ ve İKİ UFUK ÖRNEĞİ aynı çarpanı kullanır.

**Neden hepsi aynı çarpan:** `Tint()` en parlak kanalı 1'e çektiği için huzme sönerken
renk tam doygun kalıyor. Kısıcı yalnız şiddete uygulanınca bulutlar alçak güneşte
pembeleşiyordu. Eşik olarak kurulduğunda ise güneş o açıyı geçtiği anda sahne toptan
sıçrıyordu (17:37 ve 06:20 civarında bıçak gibi geçiş). Sürekli ve ortak olmak zorunda.

**Bulut ortamı da ufka kayar.** Bulutun parlak yüzü normalde zenitten beslenir; zenit
alçak güneşte mavi, doğrudan ışık kızıl, ikisi üst üste binince bulut PEMBE okunuyordu.
`HorizonFactor` ile ton ufka kaydırılır — gerçekte de bulutu aydınlatan şey parlak ufuktur.

**Tetikleyici:** Şafağın rengi değiştirilmek istenirse oynanacak yer `LowSunFadeSine`
(5°) ve bulut ortamının ufka kayma penceresi. Fizik yolu açılmak istenirse kısıcı
kaldırılır — o zaman şafak kızıla kayar, karar yeniden verilir.

**Maliyet:** Güneş 5°'nin altında gök örnekleri fiziksel değil, bilinçli olarak kısık.

---

### Gümüş kenar açılmadı — öne saçılma tepesi 0.45'te kaldı

**Karar:** Bulut faz fonksiyonunun öne saçılma tepesi `g = 0.45`, `rimStrength = 0.08`
olarak kalıyor. Güçlendirme denendi ve beğenilmedi (2026-08-11).

**Ölçüm** — güneşe bakarken faz çarpanı:

| g / rim | 0° | 10° | 20° | 30° |
|---|---|---|---|---|
| **0.45 / 0.08 (mevcut)** | **1.016** | 1.010 | 1.000 | 1.000 |
| 0.65 / 0.15 | 1.355 | 1.254 | 1.090 | 1.000 |
| 0.65 / 0.10 | 1.237 | 1.169 | 1.060 | 1.000 |

**Bulgu:** Vida `rimStrength` DEĞİL. Mevcut `g` ile tepe zaten 1.20; `rimStrength` en uçta
(0.8) bile çarpanı 1.16'ya çıkarıyor, yani gümüş kenar pratikte yok. Etkiyi belirleyen
`g`'nin kendisi — 0.45'te tepe 1.20, 0.65'te 3.37, 0.75'te 7.0.

**Neden vazgeçildi:** 0.65 + 0.15 ve 0.65 + 0.10 ekranda denendi, ikisi de fazla bulundu.
`g = 0.75` zaten daha önce beyaz kontur bıraktığı için 0.45'e indirilmişti; ara değerler de
geçmedi.

**Tetikleyici:** Şafakta bulut kenarlarının cansız olduğu şikâyeti doğarsa. O turda
`rimStrength` ile uğraşılmaz — doğrudan `g` denenir, ve tepe 3'ü aşınca doyma riski
başlıyor.

**Maliyet:** Güneşe bakan bulut kenarında parlama yok; kenar ile gövde aynı aydınlıkta.

---

### TAA açık — blok desenini eritecek katman

**Karar:** Kamera anti-aliasing modu **Temporal**, kalite High (2026-08-11).

**Gerekçe:** Bulut yürüyüşü düşük çözünürlükte yapılıyor ve blok deseni ekranda tül perde
gibi okunuyordu. Kendi zamansal geçişimiz onu eritemiyor: harmansız, çünkü üstel karışım
kontur şeritleri basıyordu. TAA hem deseni eritti hem yürüyüş çözünürlüğünü 1/16'ya
indirmenin önünü açtı.

**Bilinen bedeli:** TAA hareket vektörleriyle çalışıyor. Yağış tanecikleri
`ForceNoMotion` ile çiziliyor, yani hızlı harekette hayalet bırakabilirler. Şu ana kadar
görülmedi; görülürse tanecikler hareket vektörü üretecek şekilde ayarlanır.

**Tetikleyici:** Hızlı dönüşte iz/hayalet şikâyeti gelirse.
**Maliyet:** TAA'nın kendi kare maliyeti; karşılığında ışın sayısında %44 kazanıldı.

---

### Vinyet ve hareket bulanıklığı oyunda YOK

**Karar:** `Vignette` ve `MotionBlur` post-process katmanları profilden **silindi**,
kapalı bırakılmadı (2026-08-11).

**Gerekçe — vinyet:** Ekran köşelerini karartmak oyuncuyla dünya arasına duvar koyuyor ve
hareket hâlinde belli oluyor; karanlık sahnede daha da kötü. Var oluş sebebi merkeze
odaklamak, ama bizde derinliği zaten fizik veriyor: hava perspektifi, üç katmanlı sis,
mesafeyle mavileşme. Kullanıcının açık kuralı.

**Gerekçe — hareket bulanıklığı:** Oyunda hiç olmayacak. Kapalı bırakmak "acaba açsak mı"
sorusunu açık tutuyordu.

**Silindi, devre dışı bırakılmadı:** devre dışı bir katman profilde durur ve bir gün
yanlışlıkla açılır.

---

### Renk düzenlemesi LookController'ın işi, bootstrap'in değil

**Karar:** Grade `LookController` + `LookSettings` üzerinden yürür (2026-08-12).

**Denendi ve geri alındı:** Grade bootstrap'ten statik olarak kurulmaya çalışıldı. İki
sorun çıktı: (1) `LookController` profili zaten çalışma anında yönetiyor ve her kare
`LookSettings`'ten sürüyor — ikinci bir otorite aynı profile yazınca çakıştı; (2) bootstrap
paylaşılan profile, `LookController` ise örnek profile yazdığı için alt asset silinince
`MissingReferenceException` fırlattı.

**Doğrusu:** grade havaya ve saate göre harmanlanıyor (açık/fırtına × gündüz/gece, üstüne
altın saat kademesi). Statik bir grade bu sistemin sağladığı her şeyi kaybederdi.

**Katman İNCE tutuluyor.** Altındaki boru hattı fiziksel olarak kalibre: sahne kazancı
3.6, ACES, pozlama tavanı 0.6 EV, şafak renk çalışması. Global doygunluk ya da sıcaklık
kaydırması hepsini birden bozar — grade bu yüzden **gölgeye nişanlı**, highlight'a
dokunmuyor.

| katman | değer | neden |
|---|---|---|
| Shadows Midtones Highlights | gölge soğuk, highlight nötr-ılık | Asıl kaldıraç; şafak sıcaklığı korunur |
| Color Adjustments | doygunluk −18, kontrast +6 | Yarı yol stilizasyon; fazlası karın tonunu öldürür |
| Film Grain | 0.18, tepki 0.8 | Fotografik ağırlık, fark edilmeden |
| Bloom | 0.12, eşik 1.1 | Parlama iyimserlik okutuyor |

**Eklenmeyecekler:** kromatik sapma ve lens bozulması (vinyetle aynı aile), alan derinliği
(tırmanmada tutamağı göremezsin), ezilmiş siyahlar (karlı oyunda gölge detayı ölür).

**Tetikleyici:** Grade bir saatte iyi başka saatte kötü görünürse — şafak, öğle, gece ve
fırtınada ayrı ayrı doğrulanmalı.

---

### Kapsama tabanı düşürülmedi

**Karar:** `clearCoverage 0.18`, `minCoverage 0.27`, `openCoverage 0.10` yerinde kalıyor
(2026-08-11). Hava paketinde 0.10 / 0.14 / 0.04'e indirilmişti, ekranda görülüp geri
alındı.

**Gerekçe:** Amaç "gerçekten açık bir sabah mümkün olsun" idi. Taban indirilince alt kotta
kapsama 0.27'den 0.18'e düşüyor — gökyüzü açılıyor ama bulutlar cılızlaşıyor. Kullanıcı
ekrandaki sonucu beğenmedi.

**Bu, açık gökyüzünün kapandığı anlamına gelmez:** açık pencere tabanı zaten `openCoverage`
ile deliyor, ve bulut kütlesinin yağıştan geciken hâli (`CloudMass`) uzun pencerelerde
kapsamayı kendiliğinden indiriyor. Açıklık oradan geliyor, tabandan değil.

**Tetikleyici:** Alt kotta "hep aynı yoğunlukta bulut" şikâyeti geri gelirse — ama o zaman
önce `CloudMass` gecikmesinin süresine bakılır, tabana değil.
**Maliyet:** Alt kotta kapsama 0.27'nin altına yalnızca açık pencerede iniyor.

---

### Kar sınırı koşu başına kaymıyor — koşu kavramı yok

**Karar:** Kar sınırı (`rainCeiling` / `snowFloor`) dağın yüksekliğine oranlı sabit
kalıyor; koşudan koşuya değişmiyor (2026-08-11).

**Gerekçe:** Gerçekte kar sınırı sıcaklıkla günler ölçeğinde oynar, 90 dakikada kayda
değer şekilde hareket etmez. Sürekli bir gürültüyle oynatmak tırmanışın ortasında karın
çekilmesi gibi tuhaf bir şey üretirdi. Doğrusu **koşu başına sabit bir kayma** (±250 m):
aynı rotada her seferinde krampon kararı farklı yerde verilir, ama koşu boyunca tutarlı
kalır. Bunun için koşunun başlangıcı, tohumu ve bitişi lazım — henüz yok.

**Bağ:** Kar sınırı `snowiness`'i sürüyor; o da görüşü, arazi kar örtüsünü ve tırmanma
modunu (krampon/buz baltası) belirliyor. Yani bu bir atmosfer parametresi değil,
**oynanış** parametresi.

**Tetikleyici:** Koşu/oturum sistemi yazıldığında. Tohum oradan gelir.
**Maliyet:** Her koşu aynı kotta kara geçiyor; rota ezberi bir tık kolaylaşıyor.

---

### Kar birikim ağırlığı pişiyor — hâkim rüzgâr yönü sabit

**Karar:** Arazinin kar tutma ağırlığı (`MountainSnowDrift`) pişirme anında, hâkim rüzgâr
yönüne göre hesaplanıyor. Çalışma anında yön değiştirilemez (2026-08-13).

**Gerekçe:** Ağırlık `W = 1 + 0.5·Ωs + 0.5·Ωc` bağıntısından geliyor ve `Ωs` rüzgâr
yönündeki eğim. Çalışma anında hesaplansaydı her fragman ve her bölünmüş köşe için ek
bir gradyan okuması gerekirdi; üstelik çarpışmanın CPU ikizi de aynı normali ayrıca
örneklemek zorunda kalır, iki taraf ayrışırdı. Pişmiş hâlde üçü de **aynı dokuyu** okuyor.

Rüzgâr yönü zaten sabit: birikinti saatlerin işi, anlık esintinin değil
(bkz. `WindSettings.prevailingDegrees`).

**Bayatlık:** Açı asset'in ADINDA taşınıyor (`MountainSnowDrift-205`). `prevailingDegrees`
değişirse kurulum haritayı bayat sayıp yeniden pişiriyor — elle bir adım yok.

**Tetikleyici:** Mevsimlik ya da fırtınaya bağlı hâkim yön değişimi istenirse. O zaman
ya birkaç yön pişirilip harmanlanır (doku dizisi), ya da ağırlık çalışma anına taşınır
ve maliyeti ölçülür.

**Maliyet:** Bir doku daha (1024² R8 + mip, 2.7 MB). Hâkim yön çalışma anında
değişemiyor.

---

### Karın çarpışma yüzeyi ikinci kopyadan geliyor

**Karar:** Karın geometrisi GPU'da (`SnowDisplacement.hlsl`), çarpışma yüzeyi CPU'da
(`SnowSurface` + `SnowDriftField`) hesaplanıyor — aynı formülün iki ayrı yazımı
(2026-08-12).

**Neden:** Tek kaynaktan üretmenin iki yolu var, ikisi de pahalı. Compute shader'dan
geri okuma her sorguda kare senkronu istiyor. C#'tan HLSL üretmek derleme adımı ekliyor
ve hata ayıklamayı zorlaştırıyor. Fonksiyon otuz satır; karma tam sayı aritmetiğiyle
yazıldığı için iki taraf bit birebir aynı sayıyı üretebiliyor.

**Ayrışma riski gerçek.** Biri değişip öteki değişmezse belirti sessiz: "kar var ama
içinden geçiyorum" ya da "karın on santim üstünde yürüyorum". F1'deki ayrışma probu
(`SnowCollisionProbe`) CPU yüzeyini işaretlerle çiziyor; görsel yüzeye oturmuyorsa
ayrışma vardır.

**Prob KALICI (2026-08-13).** Doğrulama bittiğinde silinmek üzere yazılmıştı; iki kopya
durduğu sürece ayrışma riski de duruyor ve prob o riski tek bakışta gösteren tek araç.
Ölçüm bitince silinen bir teşhis değil, regresyon aracı. Kapalı kurulur, F1'den açılır,
kapalıyken hiçbir maliyeti yok. İki kopya tek kaynağa indiğinde prob da gider.

**Tetikleyici:** Üçüncü bir tüketici çıkarsa (kar üstünde iz bırakma, kar altına
gömülme, araç) iki kopya üçe çıkar — o noktada tek kaynak zorunlu olur.

**Maliyet:** `SnowDrift.hlsl` ya da `SnowMacroDepth` değiştiğinde C# ikizi AYNI ADIMDA
değişmek zorunda.

---

### Play mode domain reload KAPALI

**Karar:** Editörde **Enter Play Mode Options** açık, **Reload Domain** ve **Reload Scene**
kapalı (2026-08-13). Play'e basmak anında oluyor.

**Gerekçe:** Her Play'de domain yeniden yükleniyordu ve bekleme uzuyordu. Hız açıkça
istendi, risk bilerek kabul edildi.

**RİSK — statik alanlar artık sıfırlanmıyor.** Play'ler arasında değerler taşınıyor,
olaylara iki kez abone olunabiliyor, sayaçlar sıfırlanmıyor. Belirti sinsi: ilk Play
doğru, ikincisi yanlış.

**Tetikleyici:** Play modunda açıklanamayan bir davranış görülürse — ikinci Play'de farklı
sonuç, önceki oturumdan kalmış değer, çift abone olay — düzeltmeye girişmeden ÖNCE bu
ayar geçici olarak geri alınır ve belirtinin durup durmadığına bakılır.

**Kural:** Yazılan bir statik alan eklendiğinde `[RuntimeInitializeOnLoadMethod]` ile elle
sıfırlanır. Salt okunur statik tablolar sorun değil.

---

### Yolu ARAZİ değil DOKU gösterecek

**Karar:** Rota tesviyesi araziyi eğim sınırına çekiyor, kazı ve dolgu yapıyor, 45 cm'lik
bir iz oyuyor — ama yolun GÖRÜNÜR olmasını sağlamıyor. Görünürlük yüzey dokusundan
gelecek (2026-08-13).

**Ölçüm.** Dört hatta dik kesit alındı:

| hat | iz | ne olmuş |
|---|---|---|
| Hat 1 | 2.08 m yarma | Tepe kesilmiş |
| Ana hat | 2.54 m yarma | Aynı |
| Hat 2 | −0.97 m | Çukur dolguyla geçilmiş, yol arazinin üstünde |
| Hat 3 | −0.22 m | Arazi zaten hedefteydi |

Yani tesviye çalışıyor ve doğru davranıyor: çukuru dolduruyor, tepeyi kesiyor. Ama
arazi zaten eğim sınırının altındaysa kesilecek bir şey kalmıyor ve geriye yalnız
45 cm'lik oyuk kalıyor — 13 metrelik koridora yayılınca %7'lik sığ bir tekne, gözle
"yol" olarak okunmuyor.

**İz 45 cm'den 1 metreye çıkarıldı (aynı gün).** Kırk beş santim 13 metrelik koridora
yayılınca %7'lik bir tekne bırakıyordu ve oyunda görünmüyordu. Bir metre aynı koridorda
%15 kenar eğimi veriyor: belirgin, ama hendek değil. Uzun kullanılan gerçek yollar yarım
ile iki metre arasında çöker.

**Yine de yolu doku gösterecek.** Düz arazide yolu ayıran şey kot değil ZEMİN: sıkışmış
toprak, çakıl, aşınmış iz, kenarındaki bitki farkı. Geometri tabanı açıyor, karakteri
doku verecek.

**Ayrıca arazi ızgarası buna izin vermiyor:** 4.28 m/hücre. Gerçek bir patika iki metre
geniştir, yani yarım hücre — yükseklik haritası onu hiçbir derinlikte taşıyamaz.
Koridor şu an 12-14 metre, ızgaranın çözebildiği en dar şerit.

**Tetikleyici:** Ova yüzey dokuları geldiğinde yol dokusu da aynı adımda kurulur; o
zaman geometri ile doku birlikte değerlendirilir ve gerekirse iz derinliği ayarlanır.

---

### Yaklaşmada kar bir CEPHEDEN gelir

**Karar:** Yaklaşmanın ortasında başlayan kar, konuma bağlı bir tetikleyiciyle değil,
hava şiddetine eklenen bir **cephe terimiyle** üretilecek (2026-08-13).

**Ölçüm:** `AltitudeWeatherDriver` şiddeti yalnız rakımdan türetiyor
(`Baseline(altitude) * Variation(altitude)`). Ova 186 metrede ve taban değerde, bu
yüzden yaklaşma açık havada geçiyor.

**Neden tetikleyici değil:** "şu noktaya gelince kar başlat" ikinci bir hava kaynağı
olurdu. Cephe terimi mevcut şiddete girdiğinde bulut, rüzgâr, sıcaklık, görüş ve ses
kendiliğinden izliyor — biri unutulup "kar yağıyor ama gökyüzü açık" çelişkisi doğmuyor.

**AÇIK SORU — cepheyi ne sürecek:**

| | davranış | bedeli |
|---|---|---|
| Koşu saati | Cephe oyuncuyu beklemez; hızlı olan önde kalır, yavaş olan erken yakalanır | Kar tam "yolun yarısında" başlamaz |
| Yol boyunca mesafe | Kar hep aynı yerde başlar | Havanın oyuncunun adımını izlemesi; fiziksel karşılığı yok |

Önerilen: koşu saati. Gerçekçilik kuralı bunu söylüyor ve gerginlik katıyor.

**Tetikleyici:** Yaklaşma koridoru oynanabilir hale geldiğinde karar verilir; ondan önce
hangisinin daha iyi hissettirdiği bilinemez.

---

### Bir koşu 90-120 dakika — DÖNÜŞ YOK

**Karar:** Otobüs durağından zirveye hedef süre 90-120 dakika. **Oyun zirvede biter,
iniş yoktur** (2026-08-13 güncellemesi; ilk kayıt dönüşü de içeriyordu).
**Gerekçe:** Tek oturumda biter, ciddi bir sefer hissi verir, ölüm acıtır ama yıkıcı
olmaz. 45-60 dakika kaynak yönetimini önemsizleştiriyor; 2-3 saat ara kayıt gerektiriyor
ve "ölünce baştan" kuralıyla çelişiyor.

**Bütçe, dönüş kalkınca yeniden hesaplandı:**

| bölüm | süre |
|---|---|
| Yaklaşma, bisikletle (3.5-9 km) | 12-30 dk |
| Tırmanış (5500 m dikey, 30° yamaçta 66 m/dk) | ~83 dk |
| **Toplam** | **95-115 dk** |

Yaklaşma yürüyerek 27-68 dakika sürüyor ve bütçeyi taşırıyor: **bisiklet süs değil,
süre kısıtının parçası.**

**HIZ BİLİNÇLİ OLARAK SIKIŞTIRILMIŞ.** Gerçek bir dağcı saatte 600-800 m dikey çıkar;
5500 m dört ila dokuz saat eder. Oyundaki tempo bunun yaklaşık beş katı. Bu bir hata
değil, oyun için verilmiş bir karar — ama "gerçekçi" diye okunmamalı, yoksa buradan
türetilen her sayı (dayanıklılık tüketimi, kalori, susuzluk) beş kat yanlış çıkar.
Gerçekçilik kuralı yüzeyde, hava zincirinde ve davranışta geçerli; ölçekte değil.

**Türetilen kısıt — dağ çoğunlukla YÜRÜNEBİLİR olmak zorunda.** Dağ dikey bir duvar
olamaz: rota yürüyüş ve scramble ağırlıklı, teknik bölümler yavaşlatan noktalar olarak
serpiştirilir.

**İkinci türetilen kısıt — zor rotanın ödülü TIRMANIŞIN İÇİNDE olmalı.** Dönüş yokken
"inişte kolaylar" diye bir teselli yok. Uzun ve zor hat kendi içinde kazandırmalı:
daha yüksekte bitmeli, tek sığınağı taşımalı ya da zirveye daha kısa bir çıkışa
bağlanmalı. Yoksa üç hat değil, bir hat ve iki ceza olur.

**Tetikleyici:** Süre değişirse dikey hız kısıtı yeniden hesaplanır; rota tasarımı ve
teknik bölüm yoğunluğu ona bağlı.

---

### İrtifa hastalığı tek mekanizma: dayanıklılık tavanı

**Karar:** İrtifa hastalığı yükseldikçe maksimum dayanıklılığı düşürür. Başka belirti
eklenebilir ama mekanik BASİT kalır (2026-08-11).
**Gerekçe:** Mevcut kaynak zincirine doğrudan bağlanıyor (ağırlık → dayanıklılık →
kalori → üşüme), yeni sayaç ve yeni UI gerektirmiyor. Görüş bozulması, nefes/ses ve el
titremesi seçenekleri değerlendirildi ve reddedildi: ilki uzun sürede yorucu, üçü birden
mekaniği karmaşıklaştırıyor.
**Tetikleyici:** Oyuncu neden yavaşladığını anlamıyorsa sessiz bir belirti (nefes sesi
gibi) eklenir — ama sayaç değil, his olarak.

---

---

### Oyun yapısı: sabit dağ, uzun koşu, geri dönüş

**Karar:** Tek ve sabit dağ. Ölünce baştan başlanır; kazanılan para ve xp kalıcıdır.
Ekipman ölümde dağda kalmaz — kurtarma seferi gibi bir mekanik yok, taşınan her şey
oyuncuyla birlikte gider. Meta ilerleme daha iyi ekipman satın almaktır (oyun içi para,
gerçek para değil).
**Tekrar oynanabilirlik araziden değil havadan gelir:** aynı dağ dingin bir sabahta ve
zirve fırtınasında bambaşka oynanır. `AltitudeWeatherDriver` bunu zaten üretiyor.
**Ezber bilinçli:** Gerçek dağcı da rotayı ezberler. Sabit dağ, kamp ve sığınak
noktalarının elle yerleştirilmesini de mümkün kılıyor.
**Zorluk dengesi kendiliğinden korunuyor:** Daha iyi ekipman oyunu kolaylaştırmıyor,
daha yükseğe çıkmayı mümkün kılıyor — ve 4709 m üstü zaten sürekli fırtına. Tek şart
ekipmanın ağırlık taşımaya devam etmesi; yoksa çanta ekonomisi ölür.

---

### Geri dönüş: zirve zorunlu değil

**Karar:** Oyuncu istediği noktada inmeye karar verebilir. Sağ salim inerse o ana kadar
kazandığı para ve xp'yi korur. Ölürse hepsini kaybeder.
**Gerekçe:** Gerçek dağcılıkta zirve zorunlu değildir, sağ dönmek asıl başarıdır. Oyun
tarafında bunun karşılığı, iki saatlik koşuyu tek bir kumar olmaktan çıkarıp sürekli
yeniden verilen bir karara dönüştürmesi: her metre yukarıda "biraz daha çıkayım mı,
elimdekiyle ineyim mi" sorusu var. Kaybetmek kötü şans değil, oyuncunun kendi kararı olur.
**Bağlantı:** Kamp ve sığınak noktaları bu kararın verildiği yerler. Sığınakta havanın
açılmasını beklemek de bir strateji.
**Açık:** İniş nasıl sonlanıyor — dağın eteğine varmak mı, belirli bir çıkış noktası mı.

---

### Kaynaklar tek zincire bağlanır, HUD sessiz kalır

**Karar:** Can, açlık, üşüme ve ağırlık ayrı sayaçlar olarak izlenmez. Zincir:
ağırlık dayanıklılık tüketimini artırır → kalori dayanıklılığın yenilenme hızını belirler →
üşümek kalori yakar ve el becerisini düşürür → kritik soğuk ve açlık cana işler.
Can kendiliğinden dolmaz, kampta dolar.
**Gerekçe:** Bağlanmazsa oyuncu dört sayaç izler, bu angarya. Bağlanınca tek bir soru
kalır: "ne taşıyorum". Döngü kendi kendini dengeliyor — fazla yük daha çok yemek, daha
çok yemek daha fazla yük.
**HUD:** Sürekli görünen tek şey dayanıklılık. Diğerleri bedenden hissedilir (elin
titremesi, nefes, ekran kenarında buzlanma) ve kritik eşikte uyarı belirir. Tam sayılar
Tab ekranında.
**Tetikleyici:** Oyuncu neyin azaldığını anlayamıyorsa görünürlük kararı yeniden açılır.

---

### Oksijen tüpü acil durum eşyası

**Karar:** Zirve 5709 m; gerçekte oksijen 7000 m üstünde gerekir. Tüp sürekli kullanılan
bir kaynak değil, irtifa hastalığı krizini geçirmek için taşınan acil durum eşyası olacak.
**Alternatif:** Dağı yükseltmek. Şimdilik tercih edilmedi.
**Tetikleyici:** Dağın yüksekliği değişirse yeniden değerlendirilir.

---

### Kam (friend) ertelendi, nut ve piton yeterli

**Karar:** Ankraj araçlarından kam kullanılmayacak. Nut, piton, buz vidası ve jumar var.
**Gerekçe:** Kam kaya yarığına sıkışarak tutar; terrain 4.15 m/örnek çözünürlükte ve
yarık barındırmıyor. Yarıklar ancak üstüne konacak kaya mesh'leriyle gelir.
**Tetikleyici:** Kaya mesh'leri eklendiğinde.

---

### Co-op sonraya bırakıldı — uyarı sorumluluğu Claude'da

**Karar:** Ağ altyapısı şimdi kurulmuyor. Co-op iptal değil, ertelendi.
**Gerekçe:** Co-op'un şekli tırmanma mekaniğine bağlı — ip var mı, oyuncular birbirini
tutuyor mu, ayrı rotalardan mı çıkılıyor. Mekanik yazılmadan kurulan ağ modeli ya
yeniden yazılır ya mekaniği kısıtlar. Mevcut mimari (event tabanlı, ScriptableObject
ayarlı, deterministik terrain) ağ eklemeye zaten engel değil.

**Tetikleyici — kritik:** Kullanıcı ağ katmanını etkileyecek bir özellik istediğinde
Claude **işe başlamadan önce** uyarır: "Dur, bunu yapmadan önce co-op altyapısını
kurmalıyız." Karar kullanıcınındır, uyarı Claude'un sorumluluğudur.

Uyarı gerektiren işler — oyuncu durumu, otorite veya paylaşılan dünya durumu taşıyanlar:
tırmanma ve ip mekaniği, envanter, hasar/sıcaklık/dayanıklılık, kaydetme, kamp ve
etkileşimli objeler, oyuncuya bağlı hava/ilerleme otoritesi.

Uyarı gerektirmeyen işler — görsel ve yerel olanlar: tanecik biçimi, shader, ses karışımı,
post-process, terrain üretimi, debug araçları. Her seferinde tekrarlamak gürültüdür.

**Envanter ayrı dosyada:** Ağ gelince eli değecek yerlerin listesi `COOP.md`'de — hangi
sistem ne yapıyor, ne olması gerekiyor, maliyeti ne. Karar ve uyarı kuralı burada kalır,
envanter orada büyür; hepsinin tetikleyicisi aynı tek olay olduğu için oraya baştan sona
okunacak bir liste olarak yazılıyor. Yeni bir borç fark edildiğinde aynı adımda oraya
eklenir.

**Maliyet:** Ertelendiği sürece sıfır. Yanlış anda ertelenirse oyuncu durumu tutan her
sistemin yeniden yazımı.

---

### Ayak izi ertelendi — co-op uyarısı verildi

**Karar:** Kar üzerinde ayak izi şimdi yazılmıyor. Karın kalınlığı ve rüzgârın yüzeyi
tarayışı yapıldı; iz bırakma yapılmadı.

**Gerekçe:** Ayak izi oyuncunun **dünyaya yazması** demek. Oyuncunun çevresinde dünyaya
çakılı bir harita gerekiyor, izler oraya yazılıyor, yağışla siliniyor. Bu paylaşılan
dünya durumudur: co-op'ta izleri kimin tuttuğu, kimin yaydığı ve kimin otorite olduğu
kararlaştırılmadan yazılırsa ağ eklendiğinde yeniden yazılır. Uyarı verildi, kullanıcı
ertelemeyi seçti.

**İkinci gerekçe:** İzin karşılığı üstünde yürünecek bir tırmanma sistemi olduğunda
yüksek. Şu an test serbest uçuşla yapılıyor, yani yazılsa da görülmeyecek.

**Tetikleyici:** Tırmanma mekaniği yazıldığında yeniden açılır — ya co-op altyapısıyla
birlikte ya da bilinçli bir borç olarak `COOP.md`'ye kaydedilerek.

**Maliyet:** Ertelendiği sürece sıfır. Yanlış anda yazılırsa haritayı tutan, yayan ve
silen üç parçanın yeniden yazımı.

---

### Yağış tanecik bütçesi tamamı her kare işleniyor

**Karar:** Mesh 90.000 quad (360.000 vertex) sabit. Yoğunluk elemesi vertex shader'ın
içinde: eşiği geçemeyen tanecik sıfır boyutla çiziliyor. Yani tanecik görünmese de
tüm vertex işi yapılıyor — sekiz hız sınıfı, iki oktav türbülans, dört hash, kristal
tipi seçimi.
**Ölçek:** Dağ eteğinde yağış 0.12, yoğunluk 0.037. Yaklaşık 3.300 tanecik görünüyor,
86.700 tanesi tam maliyetle işlenip atılıyor.
**Denendi, olmadı:** Tanecikler eleme tohumuna göre sıralandı (bu kısım kaldı) ve her
karede `Mesh.SetSubMesh` ile indeks sayısı kısıldı. Yağış tamamen görünmez oldu;
`SubMeshDescriptor`'ın `vertexCount` alanı doldurulduğunda da düzelmedi. Sebep
bulunamadan geri alındı.
**Doğru çözüm:** Mesh'i tamamen kaldırıp `Graphics.RenderPrimitives` ile çizmek.
Tohumlar `vertexID`'den türetilir; eleme tohumu doğrudan `particleIndex / ParticleCount`
olur, yani sıralama bedava gelir ve çizilecek vertex sayısı her kare serbestçe verilir.
`MeshFilter`/`MeshRenderer` ve `BuildMesh` tamamen gider.
**Tetikleyici:** Yağış kare hızında ölçülebilir bir pay tuttuğunda; Profiler'da
doğrulanmadan başlanmamalı.
**Maliyet:** `Precipitation.shader` vertex girdisi yeniden yazılır, `PrecipitationRenderer`
çizim yolunu değiştirir.

---

### Tek terrain, tiling yok — 4.15 m/örnek kabul edildi

**Karar:** Tek Unity Terrain, 4097 çözünürlük (Unity tavanı). Harita 16988 m →
**4.15 m/örnek**. Önceki tetikleyici sınırı (2 m) bilerek aşıldı; kullanıcı bu hali onayladı.
**Gerekçe:** Dağın silüeti ve ölçeği istenen halde. Tiling dört kat üretim süresi (~10 dk),
dikiş ve komşuluk yönetimi getiriyor; mağara ve materyal sistemleri dört terrain'i bilmek
zorunda kalırdı. Yakın plandaki detay heightmap'ten değil, üstüne konacak kaya mesh'lerinden
gelecek.
**Bedeli — bilinerek kabul edildi:** 8 metreden küçük hiçbir arazi biçimi heightmap'te var
olamaz. Tutamak, çıkıntı, dar sahanlık gibi 1-3 metrelik şeyler ayrı mesh gerektirir.
**Tetikleyici:** Kaya mesh'leriyle telafi denendikten sonra hâlâ yakın plan yetersiz kalıyorsa;
tırmanma sistemi heightmap'ten tutamak okumak zorunda kalırsa.
**Alternatifler:** (B) haritayı 8494 m'ye küçült → 2.07 m/örnek, ama dağ yarıya iner.
(C) 2×2 tiling → 2.07 m/örnek, harita korunur.
**Maliyet:** `MountainSceneBootstrap` + `MountainGenerator` değişir. Hava, ses, oyuncu etkilenmez.

---

### Onaylanmış dağ: v1

**Karar:** 2026-08-04'te üretilen dağ (seed 53195, 16988 m, 5346 m) kullanıcı tarafından
kabul edildi. Yedeği `Backups/mountain-v1/` altında.
**Not:** Yükseklik profili eğrisinde yalnızca iki nokta var, ikisi de X ekseninin sonunda —
dağ gövdesi sabit yükseklikte, yani sivri koni değil **yayla formu**. Beğenilen silüetin
sebebi bu. Eğriye dokunmak formu tamamen değiştirir.
**Geri yükleme:** Kullanıcı istediğinde Claude asset'i `Backups/mountain-v1/` içinden geri
kopyalar. Kullanıcı komut çalıştırmaz.
**Dikkat:** `Backups/` 2026-08-05'te `.gitignore`'a alındı, yani bu yedek yalnızca bu
makinede. İkinci yedek git geçmişinde: `a44674f` commit'indeki
`Assets/Terrain/MountainTerrainData.asset` zaten v1'dir (LFS'te).
**Tetikleyici:** Dağ ayarlarıyla oynarken beğenilen hal kaybolursa.

---

### Scripting backend Mono

**Karar:** Mono. IL2CPP kurulu ama kullanılmıyor, MSVC C++ toolchain kurulmadı.
**Gerekçe:** IL2CPP build süresi dakikalar; geliştirme boyunca iterasyonu öldürür. Editörde
Play backend'den etkilenmiyor.
**Tetikleyici:** İlk gerçek build. IL2CPP AOT derlediği için `System.Reflection.Emit` ve
runtime kod üretimi kırılır — geçiş test edilmeden release alınmamalı.
**Maliyet:** Visual Studio "Desktop development with C++" workload kurulumu (~7 GB).

---

### Mağaralar SDF marching cubes ile, sonra

**Karar:** Overhang ve içine girilebilen mağaralar heightmap'e sığmıyor. Terrain'e delik açılıp
yerel SDF mesh oturtulacak. Henüz yapılmadı.
**Gerekçe:** Nereye konacakları ancak rota ve kamp noktaları netleşince bilinebilir.
**Tetikleyici:** Tırmanma rotası belirlendiğinde.
**Maliyet:** Yeni sistem. Terrain hole + yerel marching cubes mesher.

---

### Yağış prosedürel GPU, VFX Graph değil

**Karar:** Tanecikler vertex shader'da üretiliyor (`Precipitation.shader`). VFX Graph kullanılmadı.
**Gerekçe:** `.vfx` dosyaları node editöründe elle kuruluyor; Claude yazamaz, kullanıcıya
tıklatmak gerekirdi. Prosedürel yaklaşım tek draw call, sıfır CPU.
**Tetikleyici:** Yağış dışında çarpışma/etkileşim gerektiren karmaşık VFX gerekirse.
**Maliyet:** Paket kurulumu + graph'ların elle kurulması.

---

### Girdi doğrudan cihazdan okunuyor

**Karar:** `FirstPersonController`, `MouseLook`, `CursorLock` ve `FreeFlyMovement`,
`Keyboard.current` / `Mouse.current` okuyor. Template'le gelen
`InputSystem_Actions.inputactions` kullanılmıyor.
**Gerekçe:** Tuş atama sistemi yok; şimdi soyutlama yazmak kullanılmayan katman üretmek olur.
**Tetikleyici:** Tuş atama menüsü veya gamepad desteği istendiğinde. Tırmanma girdileri
netleştiğinde tek seferde doğru kurulacak.
**Maliyet:** Girdi okuyan her sistem action referansına geçer.

---

### Mobile render pipeline asset'leri duruyor

**Karar:** `Assets/Settings/Mobile_RPAsset.asset`, `Mobile_Renderer.asset` ve QualitySettings
içindeki "Mobile" kalite seviyesi silinmedi.
**Gerekçe:** PC oyununda çöp, ama `ProjectSettings/QualitySettings.asset` içinden referanslı.
Unity açıkken ProjectSettings düzenlemek riskli — Unity kapanırken bellekteki halini üstüne yazar.
**Tetikleyici:** Unity kapalıyken temizlenecek.
**Maliyet:** QualitySettings YAML düzenlemesi + iki asset silme.

---

### Konkavlık haritası D8 artefaktı taşıyor — katkısı bilerek küçük

**Karar:** `SurfaceMapBaker`'ın akış birikimi D8 (her hücre yükünü **tek** komşuya
aktarır). Klasik sonucu: komşu hücreler sıfır ile yüksek değer arasında gidip geliyor,
harita ızgaraya hizalı bir gürültü taşıyor. Konkavlık kanalı bu haritadan türediği için
yüzeyde de taşınıyor.

**Sonuç:** Konkavlığın kar kalınlığına ve kabartıya katkısı **bilerek küçük tutuluyor**
(`MountainSurface.hlsl`, kar yığılması). Büyütülünce gürültü kalınlığa, oradan kabartıya
geçiyor ve yamaçta dişli, düzenli bir desen bırakıyor.

**Gerçek çözüm:** çok yönlü akış (MFD — yükü eğimle orantılı dağıt) ya da birikim
haritasına bulanıklaştırma geçişi.

**Tetikleyici:** Konkavlığın payını artırmak gerektiğinde, ya da yüzeyde ızgara deseni
görüldüğünde.
**Maliyet:** Oyuklarda kar birikmesi olabileceğinden zayıf.

**Ders (2026-08-05):** Yüzey haritalarını üreten kod, materyali yazmadan **önce** tek
başına doğrulanmalıydı — haritayı bir quad'a çizip bakmak yeterdi, hata orada anında
görülürdü. Materyalin ilk denemesi bu yüzden geri alındı.

---

### Yakın plan nesnelerin ışık borcu

**Karar:** Bisiklet için kurulan ışık ayarları props gelene kadar olduğu gibi kalıyor
(2026-08-14). Üçü bilerek eksik bırakıldı, üçünün de tetikleyicisi aynı gün gelecek.

**1. Kapalı hacimlerde yansıma.** Sahnenin yansıma kaynağı gökyüzü; çadırın içindeki
metal eşya dışarıdaki gökyüzünü aynalar. **Tetikleyici:** kapalı ya da yarı kapalı bir
mekân (çadır, sığınak, kulübe) sahneye girdiğinde. **Çözüm:** o hacme yerel yansıma probu.
**Maliyet:** şimdilik yok, kapalı mekân yok.

**2. Yansıma katsayısındaki 2.2 çarpanı.** Şiddet ortam ışığının parlaklığından türüyor
ama çarpan elle konmuş: `reflectionIntensity = clamp01(gökSeviyesi * 2.2)`.
`ambientStrength` değişirse yansıma da kayar. **Tetikleyici:** "gündüz yansıma sönük
kaldı" ya da "gece yine parlıyor" belirtisi. **Maliyet:** bugün doğru çalışıyor,
ölçülerek kondu.

**3. Gölge mesafesi 60 m.** Bu mesafenin ötesindeki hareketli nesneler gölge düşürmüyor.
Bisiklette 50 mm'lik boşluk bile fark edildi; uzaktaki kamp çadırının hiç gölgesi olmaması
daha çok batar. **Tetikleyici:** kamp alanı ya da ekipman uzaktan görünür olduğunda.
**Çözüm:** mesafeyi artırmak (dokel kabalaşır, gölge pikselleşir) ya da uzak nesneler için
ayrı bir yöntem. **Maliyet:** yakın planda gölge kalitesi şu an iyi, uzakta yok.

**4. SSAO yarıçapı 0.3 m.** Bisiklet ölçeğine göre seçildi. Üç santimlik karabinada etkisi
zayıf, üç metrelik çadırda kuytuyu tam yakalamaz. **Tetikleyici:** ölçekçe çok farklı
nesneler bir arada göründüğünde. **Maliyet:** küçük, kabul edilebilir.


---

### SSAO yalnız nesnelerde

**Karar:** ScreenSpaceAmbientOcclusion boru hattında AÇIK ama araziye okutulmuyor
(2026-08-14). Anahtar yalnız nesne gölgelendiricilerinde bildiriliyor; `MountainSurface`
onu bildirmediği için arazi etkilenmiyor.

**Önceki karar (2026-08-09):** özellik tamamen kapalıydı, gerekçesi aşağıda. Tetikleyici
gerçekleşti — bisiklet geldi ve kuytuları kararmadığı için fazla parlak duruyordu;
kararın öngördüğü iki çözümden "yalnız objelere AO" seçildi.
**Gerekçe:** SSAO derinlik tamponundan çalışıyor ve arazi örgüsünün üçgen yüzeylerini
"yüzey kıvrımı" sanıp gölgeliyor: zeminde, yakında (30 m falloff), dünyaya çakılı,
saatten bağımsız yumuşak kafes çizgileri. DepthNormals geçişine pürüzsüz pişmiş normal
yazmak yetmedi — kırık, normalde değil derinlikte; ekran-uzayı AO bu üçgen ölçeğinde
araziyle temelde uyumsuz. Büyük ölçekli oyuk gölgesini pişmiş maruziyet kanalı zaten
veriyor; SSAO'nun tek katkısı cm ölçekli kontak gölgeydi.
**Tetikleyici:** Sahneye yakın plan objeler (props, kamp, ekipman) geldiğinde — onların
kontak gölgesi SSAO ister; o gün ya arazi maskeli SSAO ya da yalnız objelere AO çözümü
aranır. Arazi tesselasyonu belirgin incelirse de yeniden denenebilir.
**Maliyet:** Kaya çıkıntılarında santimetre ölçekli kontak gölge yok; sahne şu an
arazi+kar olduğu için görünür kayıp küçük.


---

### Gezegen yarıçapı küçültülmedi — vantaj payı yetmiyor

**Karar:** Bulut küresi gerçek yarıçapla (6360 km) duruyor; HZD'nin sahne ölçeğini
zorlamak için yarıçapı küçültme numarası uygulanmadı (2026-08-09).
**Gerekçe:** Denizin ufka değdiği mesafe `sqrt(2·R·Δh)` ve Δh gözün **deniz üstündeki
payı**. Bizde zirve ~2000 m, sakin havanın bulut tabanı 1700 m — pay birkaç yüz metre.
235 km yarıçapta deniz zirveden 13 km'de bitiyor ve ufuktaki bulutlar yok oluyor.
Kenarın görünmemesi için denizin ucunun sönümün kapandığı mesafeden (60 km) uzak olması
gerekiyor; bu da Δh=350 m için ≥ 5000 km demek. HZD'de oyuncu bulutların kilometrelerce
altında duruyor, orada aynı numara çalışıyor; bizim vantajımız bulut kotuna çok yakın.
**Tetikleyici:** Bulut tabanı zirveden belirgin şekilde (≥1500 m) aşağı çekilirse ya da
oyuncunun bulut denizine göre yüksekliği kalıcı olarak artarsa yeniden hesaplanır —
`planetRadius` ayar alanı bu yüzden duruyor, sabite geri gömülmedi.
**Maliyet:** Yatay ışınlarda katman span'i uzun kalıyor, adım bütçesinden kazanç yok.

---

### Nubis'in remap sonrası kapsama çarpanı alınmadı

**Karar:** `Remap(shape, 1−kapsama, 1, 0, 1)` sonrasında Nubis'in yaptığı `× kapsama`
çarpanı uygulanmıyor (2026-08-09).
**Gerekçe:** Remap hayatta kalan dilimi 0-1'e normalize ediyor, yani düşük kapsamalı
saçakta bile tepe yoğunluğu 1.0'a çıkabiliyor; Nubis bunu ikinci bir çarpanla
kapsamanın gerçek payına indiriyor. Bizde aynı işi sert kapsama kapısı
(`smoothstep(0.08, 0.26)`) ve uzun kuyruklu kuvvet eğrisi (`pow(t, 3-4)`) yapıyor.
İkisi F1 sürgüsüyle ekranda yan yana karşılaştırıldı; çarpanlı hâl seyrek gökyüzünde
bulutları fazla tülleştirdi, kullanıcı mevcut hâli seçti. Karşılaştırma kodu ve F1
bölümü aynı adımda silindi.
**Tetikleyici:** "Bulutlar hep aynı opaklıkta, seyrek gökte küçük bulut da tam opak"
belirtisi görülürse geri dönülür — o zaman kapsama kapısının eşiği yerine çarpan
denenir. Kapsama kapısının sabitleri değişirse de yeniden bakılır.
**Maliyet:** Düşük kapsamalı bölgede yoğunluk Nubis'inkinden yüksek kalıyor; kenar
inceliği kapıya ve kuvvet eğrisine bağlı, kapsamaya değil.

---

### Fırtına hücreleri shader'da yapılamaz — pişirmeye ait

**Karar:** Fırtınanın kolon seçmesi (HZD'nin yağış sinyalinin karşılığı) shader'da
denendi ve terk edildi; fırtına şimdilik `stormFill` ile tekdüze dolduruyor (2026-08-09).
**Gerekçe:** Hücre maskesi haritanın A (tavan) ve G (tip) kanallarından türetildi:
`smoothstep(eşik, eşik+0.12, A×G)`. Bu iki kanal pişirmede bulanıklaştırılıyor
(`BlurPeriodic(A, 2)`, `BlurPeriodic(G, 3)`) ve çekirdek başına sabit — km ölçekli,
pürüzsüz alanlar. Dar pencereli smoothstep onların üstüne binince bulut biçimli bir ayak
izi değil, yuvarlak kenarlı geniş bir leke çıkıyor; maskeyle çarpılan her itme o lekenin
geometrisini miras alıyor. F1'de üç itme ayrı ayrı denendi: **üçü de kubbe üretti**,
en güçlüsü tavan itmesi (bulutun üst yüzeyini doğrudan `ceiling01` çiziyor, pürüzsüz
geniş alan kalkınca üst yüzey de pürüzsüz ve geniş oluyor). Tavan itmesi silinse bile
kapsama ve tip itmeleri kubbeyi daha zayıf ama aynı biçimde üretiyor — sorun itmelerde
değil maskede.

Kapsama kanalında (R) bu sorun yok: elips çekirdekler, üstel yarıçaplar, yama kırılması
ve iç çatlaklar ona yapı veriyor. Doğru yer **pişirme**: fırtına sırası çekirdek başına
verilmeli ki kubbe kapağı ve eğim garantisi (`heightCap = çap × en-boy`) korunsun. O da
beşinci kanal demek; dört kanal dolu.
**Tetikleyici:** "Fırtınada gökyüzü tek parça kalkıyor, açık gökle kara kule yan yana
duramıyor" belirtisi rahatsız edici hâle gelirse geri dönülür — o zaman iş pişiriciye
taşınır ve beşinci kanalın maliyeti (ikinci doku + okuma) karşılığında alınır.
**Maliyet:** Fırtına tekdüze; yağış da kolon kalınlığından türüyor, harita üzerinde
tanımlı yağış bölgeleri yok. Çalışma `firtina-hucreleri` dalında duruyor.

---

### Yürüyüş çözünürlüğü 1/9'da kaldı — TAA da yetmedi

**Karar:** `downsample` 3 (2026-08-11). 1/16 TAA geldikten sonra denendi ve geri alındı.

**Denendi:** TAA açılınca kaydın tetikleyicisi ateşlendi ("kare geneline TAA gelirse aynı
gün geri dönülür") ve 1/16'ya geçildi. Tül perde deseni gerçekten eridi, ışın sayısı %44
düştü.

**Neden geri alındı:** Blok deseni gövdede kayboldu ama **siluet kenarında** kaldı. Kenar
basamağı 3 pikselden 4'e çıkınca TAA tek başına eritemedi; kasvet grade'inin kontrastı
(+6) da kenarları belirginleştirdi. Çadır filtresini güçlendirmek çözüm değil — kenarı
eritmiyor, bulanıklaştırıyor; bu takas kayıtta zaten yazılıydı.

**Kalan:** TAA açık kaldı (kendi kaydı var) ve gövdedeki tül desenini o eritiyor. Çadır
filtresi eski gücünde değil, yalnız kenarı eritecek kadar açık (yakında %18, uzakta %50).

**Tetikleyici:** Kare bütçesi sıkışırsa 1/16 yeniden gündeme gelir — ama o zaman kenar
için TAA'dan başka bir eritici (örneğin kenar farkında bir yeniden örnekleme) gerekir.
**Maliyet:** Işın sayısında %44'lük kazanç alınmıyor.

### Mevsim sonbahar

**2026-08-13 daraltma — İKLİM KIŞ.** Yağışın her kotta kar olarak düşmesi istendi;
temel sıcaklık 4.78 °C'den −3 °C'ye çekildi (`TemperatureField`). Artık dağın tamamı
donma seviyesinin üstünde: zemin öğlen −2.6 °C. Kar YAĞMADIKÇA zemin çıplak kalıyor
(kalıcı kar çizgisi yerinde), ama yağan kar her kotta tutuyor ve erimiyor.

Sonbahar kararı **palet için** geçerli kalıyor (altın, pas, yaprak dökümü); iklim
kışa dönmüş bir sonbahar sonu. Yaklaşma koridoru (otobüs durağı → yol → kamp) bu
sıcaklıkta karlı olur — koridor yazılırken ya iklim kota göre yumuşatılacak ya da
koridor karlı tasarlanacak.

**Tetikleyici:** koridorda yeşil/kuru zemin isteniyorsa sıcaklık zincirine geri
dönülür; "her kotta kar" kararıyla çelişir, ikisi birlikte çözülür.

**Karar:** oyun sonbaharda geçiyor (2026-08-12). Işık, renk düzenlemesi ve ileride
gelecek bitki örtüsü bu mevsime göre kurulacak: altın ve pas baskın, yeşil yalnız
korunaklı nemli ceplerde, zeminde yaprak dökümü.

**Gerekçe:** kar dağı, akşamüstü kamp ve şafak açılışı için en zengin palet; kışa dönen
dünyanın hikâyesiyle de uyumlu.

**Tetikleyici:** mevsim değişikliği KÜÇÜK bir ayar değildir — palet, döküm oranları ve
tür listesi birlikte değişir.

### Test sahnesi ayrı: mekanikler dağda denenmiyor

**Karar:** karakter, bitki, tırmanma gibi mekanikler `Assets/Scenes/TestGround.unity`
sahnesinde deneniyor — 200×200 m düz alan, 5 m ızgara, oyuncu ve sabit ışık (2026-08-12).
Oyun sahnesi (`Game.unity`) yalnız oyunun kendisi.

**Gerekçe:** her denemede arazi, hava, bulut ve kar sistemi ayağa kalkıyordu; hata
ayıklamak için yürünmesi gereken mesafe uzundu ve sahne dosyası her denemede kirleniyordu.
Test nesneleri ana sahnede birikince "bu ne zaman eklendi" sorusu cevaplanamaz oluyordu.

**Sonuç:** `MountainSceneBootstrap` artık YALNIZ oyun sahnesinde çalışıyor (yol kontrolü);
test sahnesi açıkken dağ kurmuyor. Oyun sahnesinde kalan test nesneleri
(`TestCharacter`, `Vegetation`) kurulum tarafından siliniyor.

**Tetikleyici:** test sahnesinde doğrulanan bir şey oyun sahnesinde başka davranıyorsa — o zaman
oyuncu kurulumu iki yerde ayrışmıştır ve ortak bir kuruluma çıkarılır.

**Maliyet:** atmosfer test sahnesinde yok. Görsel doğrulama oyun sahnesinde yapılıyor.

---

### Bulut sistemi söküldü, hazır bir uygulamadan yeniden kurulacak

**Karar (2026-08-14).** Kendi hacimsel bulut sistemimiz tamamen silindi. Yerine
`UnityVolumetricCloudsURP` (jiaozi158, MIT — HDRP'nin kendi sisteminin URP portu)
alınacak.

**Gerekçe.** HZD'nin dört satırlık yoğunluk formülünün üstüne on birden fazla kendi
terimimiz birikmişti; her biri bir belirtiyi kapatmak için eklenmiş, her biri yenisini
doğurmuştu. Kökteki hata ise en altta, gürültü dokusunun kendisindeydi: Worley'nin
öznitelik noktasını yerleştiren sinüs hash'i küçük tamsayı hücre koordinatlarında korele
çıkıyor ve doku düzensiz değil kare ızgara oluyordu. Üstteki katmanlarda saatlerce
düzeltme arandı.

**Tetikleyici.** Yeni sistemde görüntü yanlışsa: önce onun parametrelerine ve ürettiği
dokulara bakılır, tek seferde tek sayı değişir. **Repo'nun üstüne kendi terimimiz
eklenmez.** Ekleme ihtiyacı doğuyorsa önce ilgili makale okunur.

**Maliyet.** Repo Unity 2022.3 / URP 14.0.7 hedefliyor, biz Unity 6000.5 / URP 17'yiz:
HLSL taşınır, render geçişi RenderGraph'a yeniden yazılır.

**Sonuç:** port alındı, yoğunluk/şekil/aydınlatma zinciri makaleye göre düzeltildi ve
bağların tamamı kuruldu. Güncel bağ listesi `SYSTEMS.md` → Bulutlar; farkların teknik
kaydı `CLOUDS_REBUILD.md`. Bu karar kapandı, tetikleyicisi de geçersiz.


---

## Gökyüzü/atmosfer yeniden yazımına BAŞLANDI (2026-08-15)

**Karar.** `sky brief.md`'nin tarif ettiği sistem sıfırdan yazılmadı; `UnityPhysicallyBasedSkyURP`
(jiaozi158, MIT) gömülü paket olarak kuruldu. Paket HDRP'nin Physically Based Sky'ının URP
portu ve brief'in zincirini zaten taşıyor: Transmittance LUT, Sky-View LUT, Aerial
Perspective LUT, Rayleigh/Mie/ozon, dinamik ambient probe.

**Gerekçe.** Bulut sistemimiz aynı yazarın portu ve bu paketle çalışmak üzere yazılmış:
`URP_PBSKY` tanımlıyken bulutlar gezegen merkezini/yarıçapını gökyüzünden alıyor, ambient
probe'u paylaşıyor ve hava perspektifinden geçiyor (7 numaralı birleştirme pass'i). Kendi
atmosferimizi yazmak bu bağların hepsini elle kurmak demekti.

**Ne devredildi.** `AtmosphereController` artık `RenderSettings.skybox`, `ambientLight`,
`ambientMode` ve `reflectionIntensity` YAZMIYOR; yansıma pişirme (`DynamicGI.UpdateEnvironment`)
ve `ReflectionFrozen` teşhisi silindi. Gökyüzü, ortam ışığı ve yansıma tek sahipte.

**Ne kaldı.** `AtmosphereController` hava ve oyun sinyallerinin sahibi olarak duruyor:
görüş mesafesi, kapsama, bulut kuşağı, sis bankları, savrulan kar, rüzgâr globali, gölge
mesafesi. Gökyüzü bunları bilmiyor; çeviriyi `SkyWeatherDriver` yapıyor.

**Uyarı.** Brief 2016 ve 2020 katsayı setlerini ayrı tutmayı şart koşuyor; birleştirilmeyecek.

## Gökyüzü paketinde çifte güneş sönümü kaldırıldı

**Karar.** `mainLightColor` artık kameradaki `EvaluateSunColorAttenuation` ile
ÇARPILMIYOR. Paketin kaynağına dokunulan ikinci yer.

**Ölçüm.** Ortam probe'u (zenit): 17:54 → `0.029`, 18:10 → `0.000`, 18:30 → `0.000`,
18:41 → `0.000`, 18:46 → `0.005`. Gökyüzü güneş ufku geçer geçmez TAM SIFIR oluyor ve
otuz altı dakika öyle kalıyordu.

**Sebep.** Sönüm iki kez uygulanıyordu: LUT'un içinde örnek başına
(`PhysicallyBasedSkyPrecomputation.shader`, `EvaluateSunColorAttenuation(dot(N,L), r)`) ve
bir kez daha C#'ta kamera konumunda. İkincisi güneş ufkun altına inince sıfır oluyor ve
her şeyi sıfırla çarpıyordu. Hillaire (EGSR 2020) denklem 3 ve 11'de `Ei` atmosfer
DIŞINDAKİ aydınlıktır; sönüm integralin içinde `T(c,x)` ve `S(x,li)` olarak durur.

**Disk etkilenmiyor.** Gök cismi rengini `mainLight`'tan ayrıca alıyor
(`color.linear × intensity × π`) ve kızıllığını görüş ışını boyunca shader'da kazanıyor.

**Denenip GERİ ALINAN iki yama** (aynı belirtiye, yanlış yerden):
- Işığın yönünü ufukta kırpmak — güneş batmayı bırakıp ufukta yatay kayıyordu.
- Diski `_HideCelestialBody` ile gizlemek — belirtiyi saklıyordu, sebebi değil.
Kullanıcı doğru soruyu sordu: "doğrusu gerçek batış görüntüsü değil mi?"

**Tetikleyici.** Paket güncellenirse kaybolur; belirti gün batımından sonra gökyüzünün
bir anda tam siyaha düşmesi.

## Gökyüzü paketinde uyumluluk kipi kapatıldı

**Karar.** `PhysicallyBasedSkyURP.cs`'teki beş `#region Non Render Graph Pass` bloğu
`#if !UNITY_6000_0_OR_NEWER` ile kapatıldı. Paketin kaynağına dokunulmuş bir yer var,
tek yer burası.

**Gerekçe.** URP 17'de uyumluluk kipi API'leri kaldırıldı: `OnCameraSetup(CommandBuffer,
ref RenderingData)` ve `Execute(ScriptableRenderContext, ref RenderingData)` artık taban
sınıfta yok, `override` derlenmiyor (CS0115 × 4). Paket URP 14'e yazılmış ve bu bloklarda
yalnız `[Obsolete]` niteliğini sürümle kapatmış, metotları değil. Bulut portu (aynı yazar,
daha yeni) tam olarak bu kalıbı kullanıyor — düzeltme bizim icadımız değil, portun kendi
çözümü.

**Güvenli olmasının sebebi.** Bloklar temiz ayrılmış: uyumluluk kipinin durumu
(`mainLightColor`, `GetMainLight(LightData)`) kendi bölgesinde duruyor, RenderGraph yolu
kendi yerelini kullanıyor. Paylaşılan yardımcılar ayrı `#region Shared` bloklarında.

**Tetikleyici.** Paket güncellenirse bu düzeltme kaybolur ve aynı dört hata döner. Yeni
sürüm URP 17'yi destekliyorsa düzeltme gereksizleşir ve bu kayıt silinir.

## Atmosferik soğurmanın tek sahibi gökyüzü paketi

**Karar.** Yönlü ışığa HAM güneş yazılıyor. `TimeOfDay`'in `Tint` / `BeamLevel` /
`LowSunFade` süzmesi ışığa UYGULANMIYOR.

**Ölçüm.** Öğlen ışığa `şiddet 2.55 (tepe 3.03'ün %84'ü) · renk 1.00 0.88 0.70`
yazılıyordu. Mavi kanal daha kaynakta 0.70'e iniyor, paket üstüne kendi transmittance'ını
uyguluyordu — Rayleigh'in en çok saçtığı kanal iki kez kesiliyor ve öğlen gökyüzü lacivert
kalıyordu. F1'e izolasyon anahtarı konup yan yana görüldü.

**Gerekçe.** Port zaten bunun için alındı; atmosfer modelini iki yerde tutmanın anlamı yok.

**Ne kaldı.** `Atmosphere` modeli silinmedi ama artık ışığı sürmüyor. Sis rengi, bulut
tonu, arazi şafak rengi ve pozlama uyumu hâlâ ondan besleniyor. Kendi yükseklik sisimiz
pakete taşındığında o zincir de gider.

**Maliyet.** Arazinin şafak kızıllığı artık bizim ayarladığımız eğriden değil, paketin
soğurmasından geliyor. `duskStrength`, `duskOvercast` gibi ayarlar hâlâ SİSE etki ediyor
ama ışığa etmiyor.

## Gökyüzü devri sonrası artık taraması (2026-08-15)

`ambientMode` olayından sonra "yazanı silindi ama değeri kaldı" deseni baştan tarandı.

1. **`m_AmbientMode` diskte hâlâ `3` (Flat).** Bootstrap çalışma zamanında Skybox'a alıyor
   ve sahneyi dirty işaretliyor, ama SAHNE KAYDEDİLMEZSE kayboluyor.

2. **`m_ReflectionIntensity` 1'de donmuş.** `AtmosphereController` bunu gök seviyesinden
   türetiyordu ve bu ÖLÇÜLMÜŞ bir gerekti (kaldırılınca bisikletin kromu gece parlıyordu).
   Yazan kod kaldırıldı. Paketin yansıma küpü gece zaten karanlık olduğu için muhtemelen
   sorun değil — ama **doğrulanmadı**. Belirti aynı: gece metal yüzeyler parlar.

3. **`LookController` pozlama uyumu eski modeli okuyor.** `BeamLevel`, `SkyLevel`,
   `MoonLevel` — hepsi `Atmosphere`'dan, o da artık IŞIĞI sürmüyor. Pozlama, sahneyi
   aydınlatmayan bir modele göre açılıp kapanıyor. Şafakta belirti: gerçek ışık tam
   şiddetteyken model hâlâ "karanlık" dediği için pozlama fazladan açılır. Yorumlardaki
   kalibrasyon (`AdaptShare 0.35`, `ExposureCap 0.6`, R6 kaydı) eski ışığa göre yapılmıştı.

4. **Ölü global yazmalar:** `_StarStrength` ve `_MoonDirection` — ikisini de yalnız
   `Sky.shader` okuyordu, o da artık skybox değil.

5. **`ApplySky()` her kare `Sky.mat`'e yazıyor.** BİLEREK BIRAKILDI. O materyal paketin
   `m_FallbackSkyMaterial`'ı: feature kapatıldığında gökyüzü ondan çiziliyor. Beslemeyi
   kesmek yedek gökyüzünü donmuş bırakırdı — `ambientMode`'da yaşadığımızın aynısı.
   Maliyeti kare başına birkaç materyal yazması.

**Durum: 1-4 kapandı.** 1 sahne kaydedildi; 2 yansıma şiddeti bootstrap'te sahiplendi;
3 pozlama uyumu gerçek ölçülere bağlandı; 4 ölü yazmalar silindi. 5 bilinçli.

**Yanlış alarm çıkanlar:** `_SunDirection` (yükseklik sisi okuyor), `_PlanetRadius`
(`LightningBolt` okuyor), `_SunColor` ve `_MoonColor` (yalnız materyale yazılıyor, bulut
uniform'uyla çakışmıyor).

## Ay ikinci gök cismi oldu — yön dönüşü kapandı

**Karar.** Ay artık ayrı bir yönlü ışık VE gökyüzü paketinde ikinci gök cismi. Paketin
kaynağına dokunulan üçüncü yer.

**Neden yapısaldı.** Tek yönlü ışığa iki cisim sığmıyor: ay güneşin tam karşısında, yön
bir tanedir ve devir anında disk 180° atlıyordu. Bant ayarıyla yalnız yerini değiştirdik,
üç tur böyle geçti. Ölçmeye gerek yoktu, geometriden çıkıyordu.

**Ne değişti.**
- Paket: ikinci cisim uniform seti, `GetCelestialBody(index)`, `RenderSunDisk` döngüsü.
  Ay `type = 1` olduğu için evre ve dünya parıltısı paketin kendi hesabından geliyor.
- Sky-view, çoklu saçılım, hava perspektifi ve zemin aydınlatması `_CelestialBodyCount`
  üzerinden iki cismi topluyor. **Asıl yol `AtmosphericScattering.hlsl`'deki analitik
  yürüyüştü** (`LOCAL_SKY` tanımlı); önce yalnız LUT'ları düzeltmek yetmedi.
- Ay gölge düşürmüyor: paketin `GetMainLight`'ı gölgesiz cismi ana ışık saymayıp
  `RenderSettings.sun`'a düşüyor, böylece gökyüzü hep güneşten sürülüyor.

**Ölçüm.** 19:11→19:22 arası `probe tepe`: önce `0.00000 / 0.00000 / 0.00000 / 0.0154 /
0.0154` (sıçrama), sonra `0.0768 / 0.0036 / 0.0036 / 0.0036 / 0.0036` (düz taban).

**Kapanan iki tuzak.**
- İki ay görünüyordu: ikinci cismin verisi `if (mainLight != null)` bloğunun içindeydi,
  ana ışığın çözülemediği karede yön donuyordu.
- Bulut gölge geçişi gece `NullReferenceException` atıyordu: koddan eklenen ışıkta
  `UniversalAdditionalLightData` yok. İki ışıkta da garanti edildi.

## Gece tamamlandı: yıldızlar geldi, ay ikincil kaynak oldu

**Yıldızlar PROSEDÜREL (2026-08-16).** Önce `StarFieldGenerator` küp harita üretiyordu;
silindi. Sebep ölçüldü: 512'lik yüzde bir teksel 0.176°, ekranda 1920px/90° FOV'da bir
piksel 0.047°. Yani her yıldız zorunlu olarak dört piksel genişliğindeydi ve bilineer
süzme onu 2×2 tekseline yayıp yumuşak lekeye çeviriyordu — "bulanık", "çok büyük",
"pikselleşme" şikâyetlerinin üçü de bu tek sayıdan. Bir piksele inmek 2048'lik yüz, yani
RGBAHalf'ta **201 MB** isterdi.

Ayrıca **durağan doku titreyemez**: sintilasyon istendiği an doku yolu zaten elenmişti.

Yeni yol `Assets/Shaders/StarField.hlsl`: yön küp yüzü ızgarasına (yüz başına 128, hücre
0.70°) bölünüyor, hücre hash'inden konum/kadir/renk üretiliyor. Yarıçap ekran-uzayı
türevinden geliyor, yani çözünürlükten bağımsız olarak ~1 piksel; parlak yıldız biraz
büyük (gözde de öyle okunur). Sayı 1500 → ~6000, çıplak gözle görülen gerçek sayı.

**Sintilasyon hava kütlesine bağlı.** Ufka yakın yıldız çok daha kalın hava katmanından
geçiyor, zenitte neredeyse sabit durur. Kendi zamanlayıcısı yok: `_Time` ve hash fazı,
iki frekanslı (tek sinüs düzenli nabız gibi okunuyordu).

**GÜNDÜZ SOLMASI AÇIKÇA YAZILDI — eski gerekçe yanlıştı.** "Paket yıldızları
`(1 − skyOpacity)` ile çarpıyor, gündüzü o halleder, elle kural yazmak çifte sayım olur"
denmişti. ÖLÇÜLDÜ: zenitte gündüz optik derinlik ~0.2, yani opaklık ~0.2 ve yıldızların
%80'i geçiyor — sabah 8'de gökyüzü yıldızlıydı. Gerçekte yıldızları saklayan şey opaklık
değil, gök parlaklığının 10⁵ kat büyük olması; bizim yıldızlar gece görünsün diye
yükseltildiği için gündüz de hayatta kalıyorlardı.

Solma güneş yüksekliğinden ve **kadire göre ayrı ayrı**: parlak yıldız güneş −3°'nin
altına inince görünür, en sönüğü −18°'yi (astronomik alacakaranlığın sonu) bekler. Değer
`TimeOfDay`in güneş yönünden geliyor, ikinci bir zaman kaynağı kurulmadı.

**Sayılar (üretici silinmeden önce ölçülmüştü, dağılım korundu):** kadir histogramı
4/50/121/259/436/630 (1500 örnekte), kadir başına ~2.5 kat — gerçek sayıma yakın.

**ÇARPAN 0.08 → 0.55, ÖLÇÜT DEĞİŞTİ.** Eski ölçüt "en parlak yıldız gece zenit göğünden
12 kat parlak" idi ve o sayı gökyüzü beş durak daha parlakken kurulmuştu. Ay fiziksel
orana çekilip gece koyulaşınca oran 400 kata çıktı ama yıldızlar ekrandan **kayboldu** —
çünkü görünürlüğü gökle kıyas değil, yıldızın ekrandaki kendi seviyesi belirliyor. Oran
yanlış ölçüttü.

Yeni ölçüt fiziksel ve gökten bağımsız: **6. kadir çıplak gözün sınırında olmalı.** Gece
pozlaması ×2 alınarak kadir 2 → sRGB ~0.42, kadir 4 → ~0.19, kadir 6 → ~0.08.

**DOĞRULANMAYAN TEK ŞEY — yıldızların dönüş YÖNÜ.** Shader arama yönünü döndürüyor
(`mul(-V, _SpaceRotation)`), bu yüzden açı negatif verildi. Ekranda yıldızlar ters yöne
akıyorsa düzeltme tek işaret: `SkyWeatherDriver`'daki `-time.Normalized * 360f`.

## Bulutlar ayı doğrudan almıyor — maliyeti ölçülmedi

**Durum.** Bulut geçişinin TEK yönlü ışığı var (`_SunColor`). Gece ay bulutları doğrudan
değil, gökyüzünden pişen ortam probe'u üzerinden aydınlatıyor. Sonuç: ayda gümüş kenar
yok, faz fonksiyonu gece devrede değil.

**Neden yapılmadı.** İkinci ışık, ışın yürüyüşünde örnek başına ikinci bir
`EvaluateSunTransmittance` demek. Işık adımı 8 ve bu döngü bulut aydınlatmasının en pahalı
kısmı; maliyet kabaca iki katına çıkar ve kazanç YALNIZ gece. Ölçüm yapılmadan bu takas
kabul edilemez.

**Tetikleyici.** Gece bulutlarının düz ve ölü görünmesi rahatsız ederse. O turda önce
kare süresi ölçülür, sonra yazılır.

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
