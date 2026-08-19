# Gerekçeler

`SYSTEMS.md` **ne neyi okur**'u söyler ve kuralı emir kipinde yazar. Burası o kuralın
**neden** öyle olduğunu tutar: ölçüm, denenip başarısız olan yol, ürettiği belirti.

Ayrım mekanik:

| soru | dosya |
|---|---|
| Bu sistem neyi okur, neyi okumaz? | `SYSTEMS.md` |
| Kural ne? | `SYSTEMS.md` |
| Kural neden böyle, ne ölçüldü, ne denendi? | **burası** |
| Hangi belirtiyi gördük, sebebi neydi? | `SYMPTOMS.md` |
| Hangi karar ertelendi, tetikleyicisi ne? | `DECISIONS.md` |

Bir kural değişirse **ikisi birden** değişir. Buradaki bir kayıt gerekçesini yitirirse
silinir — telafi terimi geri eklenmez.

---

## Bulutlar

**Kapsama optik kalınlığa da girer.** Yoğunluk bir dönem yalnız `CloudMass`'ten, yani
yağıştan geliyordu; yağmursuz kapalı havada örtü inceliyor ve kapsama %100 iken bile
yıldızlar arasından geçiyordu. Bulutun kalınlığı yağışa bağlı değil — yağışsız stratus da
yıldızı tamamen keser. İkisinden büyüğü alınır, **toplanmaz**: fırtına kütlesi ile kapalı
hava aynı olguyu iki uçtan tarif ediyor.

**Yoğunluk kameradan bağımsızdır.** `DensityFadeValue` yoğunluğu kamera mesafesiyle
çarpıyor (`saturate(d / fadeInDistance)`). 5000 m'de bu küresel bir irtifa çarpanına
dönüşüyordu: yerde buluta ~2 km (çarpan 0.40), 20 km'de ~15 km (1.00) — **2.5 kat**. Bulut
yükseldikçe optik olarak kalınlaşıp gece simsiyah okunuyordu. Parametrenin gerçek işi
kameranın burnunda yoğun bulut oluşmasını engellemek ve o iş birkaç yüz metrede biter.
**300 m**'ye çekildi.

**Yoğunluk `WeatherState.Precipitation`'dan sürülmez** — o değer tavanla kesilmiş ve döngü
kurardı: kapsama → tepe → tavan kesimi → yağış → kapsama.

**Katman mutlak kotta** (`localClouds` açık). Kapalıyken ışın başlangıcı `(0,0,0)` oluyor
ve bulutlar oyuncuyla birlikte yükseliyordu.

**Rüzgârda maruziyet uygulanmaz** — oyuncu kayanın arkasına geçince iki kilometre
yukarıdaki bulut yavaşlayamaz.

---

## Kuşaklar ve hava dalgalanması

**Sınırlar mutlak metre değil orandır.** Tırmanışın altı yağmurda, üstü karda geçer ve dağ
değişse de bu oran korunur. Oran yalnız **referansı** verir; yağmur/kar sınırının kendisi
donma seviyesiyle hareket eder. Sabit sınırla dağda gözle görülen tek birikme işareti
kayboluyordu — kar sınırının fırtınada inip sonra çekilmesi.

**Kalıcı kar çizgisi hareketli sınırı okumaz, referansı okur** — buzul hava durumuyla
gelgit yapmaz.

**Dinginlik iki Perlin katmanının çarpımından.** Üçüncü ve çok yavaş bir gürültü nadiren
**açık pencere** açar. Pencerenin derinliği de değişken (kendi ayrı, daha yavaş gürültüsü):
sabit kalıntıyla her açılma birbirinin aynısıydı. Zirve kuşağında genlik daralır ama
**sıfırlanmaz** — sıfırlanınca yukarıda şiddet tek bir sabit sağanağa çakılıyor, saatlerce
hiçbir şey değişmiyordu.

**Açık pencere dalgalanmanın parçası değil**, ayrı hesaplanır. İçeriye gömülüyken zirvede
genlik sıfır olduğu için hiç açılmıyor ve bulut denizinin üstünde durma anı hiç
oluşamıyordu.

**Bulut kütlesi yağışı gecikmeyle izler.** Aynı değere bağlıyken yağışın durduğu karede
gökyüzü de açılıyordu; gerçekte bulut yağıştan sonra bir süre durur. Sonuç kendiliğinden
çıkıyor: kısa pencereler gökyüzünü açmadan geçer, uzun olanlar açar — ayrı bir kural yok.

**Bulut tepesi kesmesi bulut sisteminden itilir, sürücü çekmez.** Sürücü ayarın **nominal**
tavanını (7000 m) kullanıyordu: sönme 5800 m'de başlıyor, zirve 5686 m — kural hiç
işlemiyordu. Görüntüde yağış kesiliyor ama `WeatherState` yağmaya devam ettiği için sis,
ses ve zemin karı bulut denizinin üstünde de fırtına okuyordu.

**Karlılık da aynı zaman sabitiyle kayar.** Dışarıda bırakılırsa, ulaşılan seviye yukarı
anında sıçradığı için yağmurdan kara geçiş oyuncunun ne kadar hızlı yükseldiğine kalıyor.

**Rüzgâr neden iki sayı.** Esinti sürekli şiddetin üstüne biner ve tavanı aşar; aşınca
normalize değer kırpılır ama hız kırpılmaz — tanecikler hızlanmaya devam ederken ses ve
görüş tavana yapışık kalıyordu.

**Yükseklik neden "ulaşılan seviye".** Dağ sürekli yükselmez; sırt aşılıp boyuna inilir.
Anlık Y'ye bakılsa hava her inişte geri sarardı.

---

## Sis, görüş ve hava sinyalleri

### Bulut sisi: derinlik yazmayan geçişte mesafe uydurulmaz

**Kural:** bulut birleştirme geçişi araziye/göğe sisi bulutun **gerçek** mesafesinden
(`meanDistance` türevli derinlik dokusu) uygular. Bilinmeyen mesafeye sayı **uydurulmaz**.

**Gerekçe:** bulut yarı çözünürlükte çiziliyor, renk bilinear büyürken derinlik nokta
örnekleniyor. Uyuşmadıkları bleed halkasında paket derinliğe uzak düzlemin bir tık berisini
(`CLOUDS_RAW_FAR_CLIP_VALUE`) koyuyordu; sis o ~70 km sahte mesafeyle doyup bulut çevresine
siyah kontur basıyordu. Combine geçişi `Blend One SrcAlpha` ile derinlik **yazmadığı** için
uydurmanın tek etkisi bu konturdu. Kaldırıldı — halka artık uzak düzlem derinliğini korur,
`hasCloud` false olur, saydam bleed sissiz geçer.

**Denenip elenen:** (1) pikseli elemek → "1px sissiz halka da kontur" korkusu, ölçümle
çürüdü; (2) mesafeyi ışın menziline `clamp`lamak → sahte mesafe zaten menzilin içinde,
işe yaramadı; (3) sisi kapsamayla ağırlıklamak → sahte mesafe doygun olduğu için yine
görünüyordu. Üçü de yanlış yeri hedefliyordu; kök sebep tek: uydurma.

### Katmanlar

**Üç katman toplanır, çarpılmaz.** Çarpım üç ayrı belirtinin kaynağı oldu: derin profil
bulut denizini siliyor, sığ profil zirvede uzak sırtları karton bırakıyor, ikisini tek
kanaldan geçirmek de ikisini birden bozuyordu.

**Şafak denizi ayrı kanaldır.** Tek kanaldan geçince deniz yerleşik havanın profiliyle
yayılıyor, yol boyunca optik derinlik **on kat** çıkıyor ve şafakta yukarı bakan oyuncuya
bulutlar tamamen siliniyordu. Bir zamanlar yerleşik havayı da şafakta kalınlaştıran ayrı
bir çarpan vardı — aynı olayın iki mekanizması, ve derin olanı (yarı yükseklik 1400 m)
2.6 km yukarıdaki bulutlara kadar uzanıyordu.

**Katmanın derinliği yağıştan sürülür.** Tek yoğunluk hem yatay hem dikey yolu beslediği
için görüşü tek başına oynatmak ikisinden birini hep bozuyordu: görüşü açınca bulutlar
geldi ama arazi pusu bitti, kısınca sis geldi ama bulutlar silindi. Sabit derinlik de
yağışla çelişiyordu — sığ bırakınca 1000 m kotta sağanakta 5 km görüş çıkıyordu.

**Görüşün fiziksel tavanı.** Katman yükseldikçe seyreldiği için yoğunluğa bölmek zirvede
sınırsıza gidiyor ve ekranda "3900 km görüş" yazıyordu.

**Esinti okunmaz.** Ham hız okununca rüzgârın saniyelik sarsıntısı kayan dokuyu seğirtiyor
ve zamansal birikimin altında bulut kenarlarını blok blok pikselleştiriyordu.

### Sürüklenen kar

**Ataklarla gelir, sürekli şiddetle değil.** Kar taşınımı sürtünme hızının **küpüyle**
gider; küp, hamlenin tepesini patlamaya dibini sakinliğe çeviriyor. Gerçek spindrift 10-20
saniye fışkırır, diner, tekrar gelir — sürekli şiddetle sürülünce perde hiç kesilmeyen düz
bir akıntı oluyordu.

**Kretten fışkırır.** Tek arazi örneğiyle kret ayırt edilemiyor ve perde yamaca eşit
yayılıyordu. İleri/geri iki örnek gerekiyor.

**Kuyruk şart:** etki kretin çevresinde simetrik kaldığı sürece "savrulan duman" hiç
oluşmuyordu.

**Dikey profil kuvvet yasası, üstel değil.** Süspansiyon Rouse tipi dağılır; üstel sönüm
kuyruğu erken bitiriyor ve tüyler kısa kalıyordu.

**Yükseklik yerden ölçülür.** Deniz seviyesine göre sönen bir profil sırtın üstünde hiç
görünmez, vadide ise boğar.

**Sürüklenme kaynağını tüketir.** Bu bağ olmadan perde sonsuza kadar aynı şiddette
akıyordu.

**Kar sıfırın altında erimez.** Erime karlılık oranından sürülüyordu; o bir sıcaklık
vekiliydi ve sıfırın çok altındaki bandı bile "ılık" sayıp eritebiliyordu. Faz değişimi
enerji ister; enerji yoksa kar durur — dağın karının kalıcı olmasının sebebi bu.

**Rengi ile parlaklığı ayrı kurulur.** Gök örneği (`_HeightFogSunColor`) HDR ve üst
sınırsız, üstelik en büyük olduğu yer güneş yönündeki ufuk — yani şafak ve akşamüstü.
Olduğu gibi renk olarak geçirilirse 1'i aşar, beyaza kırpılır ve dağ fosforlu görünür;
katsayı kısmak yalnızca eşiği öteler. Seviyenin ölçüsü ufuk parıltısı değil **güneşin
yüksekliği**: şafakta yamaç hâlâ gölgedeyken perde ufkun parlaklığını alırsa dağ
aydınlatılmış görünür.

**Akış alanı bilerek kaba.** Işın 8 adımda integre ediliyor; daha ince desen undersampling
üretiyor ve perdenin içinde yağmur yağıyormuş gibi titreme bırakıyor. Sektörün çözümü
temporal reprojection + blue noise + TAA, bizde TAA yok (`DECISIONS.md`).

### Hava haritası ve bulut biçimi

**İmzaya sürüm dahil.** İmza yalnız ayarlardan kurulduğu sürece kod değişikliği haritaya
hiç yansımıyor, menüden "yeniden pişir" bile eski sonucu üretiyordu. Sürüm üreticinin
içinde durur çünkü onu artırmayı unutan, algoritmaya dokunan kişidir.

**Tazeleme editör yüklenirken**, sahne kurulumunda değil — düzeltmeyi değerlendiren kişi
eski haritaya bakmasın diye.

**Pişirme asset'in üstüne yazar.** Silip yeniden kurmak GUID'i düşürüp sahnedeki
başvuruları koparıyordu.

**Çekirdekler eliptik.** Mükemmel daire diye bulut yoktur; dairesel ayak izi bulutları
silindir olarak okutuyordu.

**Ayrı "kimlik" hash'i söküldü.** Sürekli kanalların `frac`'ı tamsayı geçişlerinde sıçrıyor
ve bulut içine fermuar kenarlı şerit perdeler çiziyor — **süreksiz fonksiyon sürekli alana
uygulanamaz.**

**Hava kolonsal.** 3B'den örneklenen dönemde gövdeye bağsız yüzen parçalar (kubbe/adacık)
doğuyordu.

**Tavanı yalnız kolon-sabit alanlar sürebilir.** Yüksekliğe göre değişen bir alan tavanı
sürerse üst yüzey o alanın izo-yüzeyi olur ve 3B pürüzsüz gürültünün izo-yüzeyi yuvarlak
kapaktır — yani kubbe. Fırtına dolgusunun dalgası bu kurala aykırıydı (`sec4.r`, örneğin
y'sini taşıyordu) ve yüksek kapsamada tepeleri kubbeleştiriyordu.

**Zarf kapsamayı kısar, şekli çarpmaz.** Çarpanla inceltince eşiği tepede yalnız gürültü
zirveleri geçiyor ve hayatta kalanlar sivrilerek iğneye dönüyordu — bulut üstlerindeki koni
ormanının kaynağı.

**Kapsama–gök bağı alt-doğrusal (üs 0.65).** Doğrusal bağ "%35 bulut" derken göğü %22
örtüyor ve min %30 kuralını deliyordu.

**Dikey uzanım ekranda kalibre edildi** (F1 atölyesi). Sayılar **birlikte** ayarlandı —
birini tek başına eski değerine döndürmek silindiri geri getirir.

**Fırtına dolgusunun tabanı varyanslı.** Sabit taban eşiği her yerde doyurup örtüyü tek
parça halıya çeviriyordu.

**Döşeme tekrarına iki önlem yetmiyor.** Boş bölgelerin kanalları 48 km periyotlu alanlarla
boyanmasa tek doku değişkeni 2.86 km'de döşenen şekil gürültüsü kalıyor ve örtü zirveden
kafes gibi okunuyordu; ikinci örneklemin (37°, ×1.26) kendi periyodu da menzilin altında,
zirveden kafes yine okunuyor (ölçüldü, ekran görüntüsüyle).

### Işık ve örnekleme

**Kapsama yoğunluğu ayrıca çarpmaz.** Nubis eşik remap'inin ardından kapsamayla bir daha
çarpar; ikisi ekranda karşılaştırıldı ve bizimki seçildi (`DECISIONS.md`).

**Sonda adımları üstel.** Kazanç sayıda değil dağılımda: dörtte ilk adım 80 m, beşte
38.7 m — gölgenin belirleyicisi ilk birkaç yüz metre olduğu için yakın gölge iki kat ince
çözülür. Koni çekirdeği sürgü aralığının tamamını (2–8) ayrı yönle karşılar; eksik kalsaydı
üstteki örnekler aynı yönü okuyup maliyeti tekrara harcardı.

**Sonda ön yüzde tam örnekler** (HZD kuralı). Kabarık kenarların kendi gölgesi onları üç
boyutlu okutan şey; erozyonsuz gölge kenarı düz bir zar gibi aydınlatıyor. Derinde ışık
zaten sönmüş.

**Sonda kendi mesafesini taşır.** Sabit "çok uzak" varsayınca detay sönümü ve kenar eğrisi
birincil ışınla ayrışıyor, gövde bir alandan gölge başka alandan okunuyordu —
beyazlaşmanın eski kökü bu sınıftandı.

**Geçiş harmansız.** Üstel karışım kontur şeritleri basıyordu; blok deseni yalnız kompozit
çadır filtresiyle örtülür ve filtre yarıçapı bulanıklıkla birebir takas eder.

**Boş bölge sıçraması tam adım katlarında.** Serbest uzunlukta yapılınca ışının örnekleme
kafesini kaydırıyor — komşu piksellerden biri sıçrayıp öbürü sıçramayınca buluta sabit
metrelik kuantalarla giriliyordu.

**Mercek sapması kapsamayı çarpar.** Şekil alanına toplamsal binerken geçişi dikleştirip
kuyruğu inceltiyordu.

**Jitter yerel koordinat + sin'siz hash.** Gezegen-merkezli sin hash'i fp32'de dejenereydi
ve ışık kabukları çıplak soğan halkaları basıyordu — halka sagasının gerçek kökü.

**Işık kapısı dar.** Geniş bant doğrudan güneşi kısıp bulutları ambient'e bırakıyor, renk
siliniyordu.

**Yoğunluk eğrisi uzun kuyruklu kuvvet** (t^~3): smoothstep erken doyup blok yapıyordu.
**Mercek profili toplamsal** — kapsamaya çarpım üç kez ölü vida çıktı (kapı/erken-çıkış
mıhları) — ve katsayısı bilerek gürültü genliğinin altında: aşarsa sınırı pürüzsüz harita
konturu çizer, kenar duvarlaşır. **Saçak kapısı geniş bantlı** (0.10–0.42); dar bant dış
sınırı keskin duvara çeviriyordu. **Peçe rengi süzer**, alfa kırpımı dağı bulutun içinden
gösteriyordu. **Kapalı örtünün taban çeyreği benekli** — tekdüze gri tavan "bulut yok"
okunuyordu.

**Yağış karartması tek çarpan.** Ayrı çarpan aynı işi iki kez yapıyor ve kalın gövdede
ambient dörtte bire iniyordu — bulutlar siyahımsı griye kaçıyordu.

**Bulut ortamı radyans ister, ışınım değil.** Dönüşüm π. Aynı fark froxel sisinde
ölçülmüştü (probe DC 0.156, sis rengi 0.492 — oran 3.15 ≈ π) ve orada düzeltilmişti; bulut
tarafı aynı hatayla kalmış, gece bulut kendi ışımasını π kat eksik alıyordu.

**Gezegen yarıçapı gerçek değeriyle durur.** Küçültülünce deniz zirvenin hemen dibinde
bitiyor (235 km'de 13 km) ve ufuktaki bulutlar yok oluyordu. Kenarı **sönüm** saklar,
geometri değil — sönüm mesafesi denizin ufkuna eşitlenirse ufuktaki bulut tam karışıma
girip kaybolur.

**Sanat yönü harmanı pişirmede.** Kaba sıçrama haritası bu sonuçtan türüyor; çalışma
zamanında harmanlansaydı sıçrama boyanmış bulutun üstünden atlardı. Pişmiş harita adı
boyanan dosyanın **içerik** hash'ini taşır — aynı dosya yeniden boyandığında ad değişmiyor
ve harita bayat kalıyordu.

### Renk ve gökyüzü

**Bulut ve sis rengi aynı kaynaktan.** Ayrı sabitler gökyüzü kızarırken sisi soluk
bırakıyordu.

**Batış kızıllığı eklenir, çarpmaz.** Batışta bulut zaten karanlık; sıfıra yakını kızılla
çarpmak siyah bırakır ve kızıllık hiç görünmüyordu.

**Şafağın altın ucu sabit ve bilinçli abartma.** Altın saatte (+5°) fizik zaten sabite eşit
(0.569 / 0.571); fark yalnız doğuş anında ve orada fizik haklı — gerçek doğuş, yirmi dakika
sonrasından sönüktür. Sabit o ilerlemeyi düzleyip doğuşu **3.7 kat** parlatıyor. Aerosol,
adım sayısı ve örnek yüksekliği hipotezlerinin üçü de ölçülüp çürütüldü (`DECISIONS.md`).

**Kazanç tek.** İki ayrı sabit vardı, aynı adı taşıyorlardı ve farklı işler yapıyorlardı;
biri değişince öteki yerinde kalıyor, gökyüzü ile ondan türeyen değer ayrışıyordu.

**Çok saçılma şart.** Olmadan batış ufkunun mavi kanalı tükeniyor, doygunluk 0.98'e
çıkıyor ve renk turuncu değil saf kırmızı oluyordu; ayrıca gök tamamen karardığı için
kazanç şişiyor ve en parlak yer ton eşlemede kırpılıyordu.

**Sis renginin seviyesi gökten.** Sabitken ölçüldü: gök gündüz–gece arası ~**230 kat**
değişirken sis rengi **9.6 kat** değişiyordu — gündüz 2.2 kat fazla koyu, gece 11 kat fazla
parlak. Gece sisin örttüğü her şey 3.5 durak yukarı kalkıyor, "sisi kapatınca gece
gerçekçi oluyor" belirtisini üretiyordu. Katsayı üç renge birden uygulanır; ayrı ayrı
oturtmak aralarındaki oranları ezerdi (zenit'in yağışa bağlı payı, gölge tarafının şafak
payı o oranların içinde).

**Hava ve gökyüzü tek fonksiyon.** İki ayrı formül tutulduğu sürece her hava/saat köşesinde
ayrışıp dağı "parlayan karton" olarak gökten koparıyorlardı.

**İnen ışının integrali ayrı.** Eğim kırpması işareti yutuyordu ve inen ışın "ufka paralel"
sayılıp yolu tavana yapışıyordu — arazinin bittiği yerde ekranın alt yarısı tam sise, yani
karanlık aşağı-hava rengine boyanıyordu.

**Bank çarpanı yalnız kameranın yerelinden.** Bank alanı katmana yansıtılınca sinüs deseni
gökyüzüne şerit şerit basılıyordu.

**Yüksek katman aynı fonksiyona karışır.** Ayrı düz bir taban renge karışıyordu: şafakta
hacimsel katman güneşe doğru altına gömülürken yüksek katman her yönde aynı griye gidiyor,
iki katman arasında hem renk hem kenar farkı duruyordu. Ufuk sınırı sert eşikte alfa sıfır
olmadığı için gökte jilet gibi yatay bir çizgi bırakıyordu.

**Güneş diski söner, hâlesi kalmaz.** İkisi aynı katsayıyla dururken fırtınada güneş, görüş
140 m olmasına rağmen keskin bir leke bırakıyordu. Sönüm sisin kolon optik derinliğinden
(`β/k`): berrak 0.91, yağışlı 0.22, fırtına 0.00.

**Koschmieder 3.9.** Önceki 1.6, sisi olduğundan iki kat seyrek gösteriyordu.

**Test kilitleri bileşeni kapatmaz.** Bileşen kapatılınca `StormIntensity`/`ClearWindow`
donuyor ama okunmaya devam ediyordu: F1 sürgüsü yağışı, görüşü, sisi ve rengi sürerken
bulutlar kilitlenme anındaki hâlde kalıyor, tek hava durumu iki kanala ayrılıyordu.


---

## Dağ yüzeyi

**Yön hâkim olandan, anlık hızdan değil.** Birikinti alanı `dot(worldXZ, windAxis)`
üzerinden kuruluyor ve dağın ortasında |worldXZ| yedi bin metre — bir hamlenin 0.14
radyanlık sapması bütün deseni **980 metre** sürüklüyordu (gövde 45 m).

**Kapsama ile kalınlık ayrı.** Rüzgâr sırttan alıp oyuğa bıraktığı için kalınlık düzlükte
ve çukurda birikir, dik yamaçta ince kalır. Bir parmak kar altındaki taşı gösterir, yarım
metre göstermez.

**İki ayrı kalınlık kanalı.** Tek kanalken birikinti çukurunda kayanın rengi karın içinden
geri geliyor ve rüzgâr ekseninde uzamış gri şeritler bırakıyordu. 60 cm de 90 cm de taşı
tamamen gizler; gömülme doyar.

**Birikinti alanı gerekli**, çünkü kot bandı, eğim ve maruziyet arazi ızgarasında (4.28 m)
değiştiği için derinlik dört metrenin altında dümdüzdü.

**Konkavlık okunmaz.** Yüzey haritasının konkavlık kanalı akış birikiminden türüyor ve
ızgaraya hizalı gürültü taşıyor; birikintiye girdiğinde yamaçta tarama çizgileri
bırakıyordu (F1 izolasyon anahtarıyla ölçüldü).

**Arazi ağırlığı tek kaynak.** Uçlar arası 3.0 kat; saha ölçümü rüzgâraltı yamaçta iki kat,
taze karda dörde kadar. `TerrainWindShelter` eskiden kendi kabartma hesabını yapıyordu ve
iki cevap ayrışıyordu — karın derin rüzgâraltı yığını saydığı yerde oyuncu tam rüzgâr
hissedebiliyordu. Gölgelendirme `lee`'yi anlık normalden, `hollow`'u konkavlık kanalından
ayrı ayrı türetiyordu; geometri eklenince üçüncü bir kopya çıkacaktı.

**Eğrilik çekirdeği dairesel.** Kare kutu ortalamasıydı ve kare çekirdeğin frekans cevabı
eksenlere hizalı: kanal ızgaraya hizalı bir desen taşıyor ve büyütüldüğü her yerde yüzeyde
tarama çizgisine dönüşüyordu.

**Maruziyet birikim ağırlığından, gökyüzü açıklığından değil.** Gökyüzü kanalı yönsüz: bir
çukurun tabanı göğü görmez ama rüzgâr da almaz, oysa göğü gören bir rüzgâraltı terası
taranmaz.

**Yeni gürültü örneği alınmaz** — arazi fragmanında her ek örneğin kare hızında ölçülebilir
bir payı var.

**Pütür ayrı bir oktav** çünkü taş kabartısının dalga boyu çökme höyükleri için fazla iri.

**Tazelik ayrı bir sinyal.** Kalıcı çizginin üstünde örtü zaten dolu; yeni yağan karın
kapsamaya ekleyeceği bir şey yok, o yüzden fırtına sonrası yüzey hiç değişmiyordu.

**Uzak "pırıltı şeridi" söküldü** — hücre gürültüsüyle taklit, çizik dokusu basıyor.

**Kar gece matlaşır.** Yönlü ışık aya çevrildiğinde gündüz pürüzsüzlüğü dar speküler lobla
çakıyor ve dalgalı kar normali yüzünden kamera oynadıkça yanıp sönüyordu — gece boyunca
süren sahte pırıltı. Şiddet düşünce diffuse zemine gömülüyor ama dar lob tonemap'ten sağ
çıkıyor: oran değil görünürlük değişiyor. Gerçekte ay ışığında kar cila verir, çakım vermez.

**Alpenglow gölgeye tabi.** Gölgesiz emisyon şafakta sahneyi düz bir vuruşla yakıyordu.

**Dört geçiş de yer değiştirmeyi uygular** — biri atlanırsa gölge yüzeyin altında kalır.

**Kalıcı kar çizgisi türetilir.** Ayrı sabit tutulduğunda çizgi kar kuşağının altına
düşüyor, zemin beyazken tepeden yağmur yağıyordu. Yükseltme payı yumuşatma bandından küçük
kalırsa çizginin alt ucu kar kuşağının içine sarkıyor ve aynı çelişki dar bir şeritte geri
geliyordu.

**Taze kar kot ekseninde.** Tek global sayıyla dağın tamamı aynı anda beyazlıyor ve öyle
kalıyordu; üstelik birikme hızını *oyuncunun bulunduğu kotun* havası sürüyordu. Erime donma
seviyesinin altında dakikalar, üstünde saatler sürer — kar sınırının fırtınada inip sonra
çekilmesi buradan çıkıyor.

**Hava kaynaklı değerler global.** `UnityPerMaterial` tamponunun içindeyken
`material.SetFloat` ile yazılan değer shader'a **ulaşmıyordu**: tampon eski değerinde
kalıyor, kar maskesi her kotta kapalı okunuyordu.

---

## Gökyüzü ve gök cisimleri

**Soğurmanın tek sahibi paket.** Bizimki de uygulansaydı aynı atmosfer iki kez soğururdu.
Ölçülmüştü: öğlen ışığa `şiddet 2.55 · renk 1.00 0.88 0.70` yazılıyordu, mavi kanal
kaynakta 0.70'e iniyordu ve gökyüzü lacivert kalıyordu.

**Güneşin bandı −12°'de bitiyor.** Paket göğü ışığın yönünden ve şiddetinden hesapladığı
için güneş ufkun altında sıfırlanırsa ALACAKARANLIK DA SÖNÜYOR — 18:10'da gece yarısı
karanlığı çıkıyordu. Arazi bundan yanlış aydınlanmaz: ışık neredeyse yatay geldiği için düz
zeminde `N·L` negatif, yalnız güneşe bakan dik yamaçlar ışık alır.

**Ay ayrı ışık olmak zorunda.** Tek yönlü ışığa iki cisim sığmıyordu: ay güneşin tam
karşısında (`MoonDirection = −SunDirection`), yön bir tanedir ve devir anında disk 180°
atlıyordu. Yapısal, ölçmeye gerek yok.

**Ay gökyüzünü de aydınlatmalı.** Tek cisimken sıçrama kaçınılmazdı ve ölçüldü: 19:12'de
probe `0.00000`, 19:22'de `0.00228` — atlama tam güneşin şiddetinin sıfırlandığı anda,
çünkü gökyüzü o ana kadar güneşten sürülüyor, sonra aya geçiyordu.

**Analitik probe devre dışı** — o yol çoklu saçılım taşımıyor ve alacakaranlıkta sıfır
veriyordu.

**Probe sıçramada iki kez pişer.** `DynamicGI` gökyüzü materyalini okuyor ama materyali
render geçişi yazıyor, yani pişirme bir kare geriden görüyor. Zaman akarken görünmez; saat
sıçrayıp durunca tek pişirme eski göğü yakalıyor ve probe donuyordu. `LookController`
pozlamayı ondan okuduğu için gece sahnesi gündüz pozlamasıyla çiziliyor, her şey siyah
çıkıyordu.

**Ortam kipi `Skybox` olmalı.** `Flat` kalırsa paketin dinamik probe'u hiç devreye girmiyor
ve ortam ışığı donmuş bir renkte kalıyor; ölçüldü — probe öğle ve gece birebir
`0.223 0.293 0.420`, tepe ile taban da aynı.

**Yıldızlar için küp harita yolu ölçülüp elendi.** 512'lik yüzde teksel 0.176°, ekranda
piksel 0.047° — yıldız zorunlu olarak dört piksel ve bilineer süzmeyle leke oluyordu; bir
piksel için 2048'lik yüz, yani **201 MB** gerekirdi. Durağan doku ayrıca titreyemez.
~6000 yıldız, kadir 0–6, sayım küp kökle dağıtılıyor (kadir başına ~2.5 kat, gerçek sayıma
yakın).

**`(1 − skyOpacity)` gündüzü kapatmıyor** — bir dönem öyle varsayıldı, ölçüm çürüttü:
zenitte gündüz opaklık ~0.2, yıldızların %80'i geçiyordu ve sabah 8'de gökyüzü yıldızlıydı.

**Ay şiddeti 0.0199, albedosu 0.586 0.653 0.818.** Doğan ay uzun atmosfer yolundan geçip
sarıya kayıyor; taban soğutuldu ki soğurma sonrası sonuç nötre yaklaşsın. Doygunluk bir kez
düşürüldü ve ton lineer uzayda eski ışımaya ölçeklendi (Y = 0.3844): 10°'de eski renk
`1.00 0.80 0.43`, yeni renk `1.00 0.87 0.56`.

**Güneşin yönü/rengi hava sürücüsünden geçmez.** İkinci bir yol "gökyüzü kızardı ama
gölgeler öğle yönünde" çelişkisini üretirdi.


---

## Bilinçli kuralların gerekçeleri

**"Gündüz"ün iki ölçüsü.** `DayFactor` (−0.22 → 0.45) geniş olmazsa sabah 8 ile öğle 12
aynı parlaklıkta görünür; `sunOverMoon` (−0.12 → 0.04) dar olmazsa güneşle ay ufukta yarım
saat boyunca aynı anda yanar.

**Aşağı inmek havayı geri sarmaz.** Boyun geçişleri fırtınayı kapatmamalı; ulaşılan
seviyenin belli bir mesafe altına inene kadar hiçbir şey değişmez, ötesinde yavaş geriler.

**Bulut kütlesinin üstünde yağış yok.** Tırmanışın son bölümü bulut denizinin üstüne
çıkıştır: yağış diner, gökyüzü açılır, altında deniz kalır — rüzgâr sönmez. Ölçü kütlenin
bittiği kot, çünkü yoğunluk profili tavana varmadan sıfırlanıyor: en kabarık bulut bile
kendi tepesinin yarısından itibaren sönmeye başlıyor, yayvan olanlar katmanın alt üçte
birinde bitiyor. Tavana yaslanmış sönüm geç kalıyor ve oyuncu denizin üstünde dururken
üstüne kar düşmeye devam ediyordu.

**Kapsamanın alt sınırı.** Sınırın altında gökyüzü boş ve bulutlar cılız görünüyor. Tek
istisna açık pencere ve bilinçli: iki kural aksi halde çelişiyordu — sürücü "bulutlar
aralanır, zirve görünür" diye söz verirken taban o anın hiç gelmemesini sağlıyordu.

**Kapsama eğrisinin dikleşmesi.** Adı "lead" olduğu sürece kod yapmadığı bir şeyi vaat
ediyordu.

**Taze karı şiddet biriktirir.** Çisenti de sonunda örter ama sağanaktan çok daha uzun
sürede; eşikle sürülürse şiddet örtüyü hiç etkilemez, yalnızca açıp kapatır.

**Arazi gölgesi ufuk haritasından.** Gölge haritası kullanılınca üçgen silüetlerin gölgesi
sırtlarda testere dişiydi; araya giren ışın yürüyüşü de kenarda ya jilet ya nokta bırakıp
iki kez geri alındı. Ufuk alanı pürüzsüz: ne üçgen var ne rastgelelik. Gölge menzili
sınırsız, kenar güneşin ufka açısal yakınlığıyla yumuşuyor.

**Bulut ve arazi aynı hava.** Yer seviyesinin görüşü olduğu gibi yükseklere taşınınca,
dağın kilometrelerce net göründüğü havada bulutlar birkaç yüz metrede yok oluyordu.

**Bulutun boyu tipten, metre cinsinden.** Ayrıca bir **tavan kanalı** vardı — çekirdek
başına kubbe, MAX ile birleşen, bulanıklaştırılan, sonra shader'da beş çarpandan geçen
ikinci bir yükseklik kaynağı. Ürettikleri ekranda tek tek görüldü: dar kolonun komşusundan
tavan miras alması (**parmaklar**), çarpanların üst üste binip tavanı sıfıra indirmesi
(**dümdüz çökmüş örtü**), tavanın yatayda hızlı değişmesi (**sivri uçlar**). Kanal ve
zinciri silindi. Boy metre, katman oranı değil: HZD'nin hacimsel katmanı 2.5 km, bizimki
5.3 km (kümülonimbusa yer açmak için); oran kullanılsaydı katmanı kalınlaştırmak bütün
bulutları birlikte uzatırdı — tavan kanalı zaten bunu telafi etmek için icat edilmişti.

**Zarf çarpar.** Bir dönem eşiği yükseltiyordu çünkü çarpınca tepeler iğneye dönüyordu; o
gözlem doğruydu ama sebep zarf değil sönüm bandının **genişliğiydi** — tavan kanalı
yüzünden ~1.2 km'ye yayılıyor ve gürültünün özellik boyuyla (~1 km) yarışıyordu. Metre
boyla bant her tipte 180–456 m, özellik boyunun altında: daralma pürüzsüz, tepe kubbe.

**Kolon-sabit alan yüksekliği süremez** — sürerse desenini dikey sütun olarak basar; tanımı
zaten "dikeyde değişmeyen".

**Hava perspektifi bulutun uzaklığından.** Işın görüş mesafesine göre kesiliyor (maliyetin
ana kaynağı) ama durduğu nokta bulut içeriğine göre basamak basamak oynuyor; katmana giriş
açısı yalnız yüksekliğe bağlı olduğu için o basamaklar gökyüzüne eksen etrafında simetrik,
iç içe halkalar olarak biniyordu.

**Görüş sınırında şekil de kaybolmalı.** Şekil alfada taşınıyor; perspektif kapandığında
kapsama da doldurulmazsa düz gri görülmesi gereken yerde silik siluet kalır.

**Örnekleme kafesi tek.** Adım boyu sahne derinliğine bağlanınca komşu pikseller farklı
kafeste örnekleniyor ve **arazinin silüeti buluta desen olarak basılıyor**.

**Işına bağlı dallanma yasak.** Böyle bir dal, geçirgenliğin eşiği geçtiği YÜZEYİ uzayda
çiziyor: bulutun ortasında makasla kesilmiş düz beyaz ada (yoğunluk dalı), kenarda koyu zar
(sonda dalı), ikinci bir halka ailesi (sonda aralığı dalı). Üçü de yaşandı ve söküldü.

**Döşeme kırıcı.** Taban gürültüsü dünyada 2.86 km'de aynen tekrar ediyor; yakından bir-iki
tekrar görünür ve göz seçemez, zirveden yüzlercesi aynı anda görünür ve desen okunur.
37°'lik ikinci örneklem, 7.7 km'lik 3B büküm ve 5.7 km'lik kolon warp'ı kendileri de
kafestir. Şekil alanına 48 km periyotlu analitik bir kaydırma biner (genlik 1400 m =
döşemenin yarısı; gradyan 0.23, uzay katlanmaz) ve **ikinci örnekleme de aynısı uygulanır**
— yalnız birincisi bükülseydi ikincisi kendi tekrarını olduğu gibi taşırdı.

**Tanecik yoğunluğu bükülmüş eğriden.** Doğrusal olsa hafif yağışta ekran tanecikle
doluyor.


---

## Yağış, ses ve şimşek

**Yağış payı yumuşatılır** (~2.5 s), yoksa bulut kenarından geçerken yağmur bıçak gibi
kesiliyordu.

**Tane kendi rengini seçmez.** Kendi ışığını üretmiyor, göğün ışığını saçıyor; havanın
rengi çarpanla parlatılınca şafakta turuncu, gece koyu, şimşekte parlak oluyor — hiçbiri
ayrıca ayarlanmıyor. Sabit bir beyaz, kapalı gökyüzünün önünde patlayıp taneleri yıldız
gibi gösteriyordu.

**Biçim kümelenmedir.** Havada süzülen şey yüzlerce kristalin birbirine yapışmış hâli;
kristalin kolları bir iki milimetre ve ancak göze değdiğinde görünür. Altı kollu silüet
mikroskop görüntüsünü gökyüzüne koymaktı.

**Damla gevşeme süresiyle uyar.** Süzülmesiz hız, her hamlede bütün yağmuru aynı karede tek
parça yatırıyordu. Dönme ve girdap sürekli şiddete bağlanınca aynı tane hızlanırken
dönmesi sabit kalıyordu.

**Band geçişi esintiye bağlanmaz.** Bağlansaydı dingin ve fırtına karışımı sekiz saniyede
bir yer değiştirir; rüzgârın sertleştiği değil, sesin oraya buraya kaydığı duyulurdu.

**Varyasyon geçişi band susmasını beklemez** — dingin band ancak şiddet uca dayandığında
susuyor ve pratikte tek klip dönüyordu.

**Mesafenin tek sahibi `ThunderPlayer`.** İkisi ayrı seçilseydi bir buçuk saniye sonra
gürleyen bir gürültü, sekiz yüz metre ötede çakmış bir ışığa ait olurdu. Sekiz kilometre
gerçekten yirmi dört saniye demek.

**Şimşek ışığı yönlü kalır.** Çakma iki kilometrenin üstünde olduğu için arazi boyunca
yayılan gradyan zaten küçük (beş yüz metre ötedeki bir çakma için 2.3 kat), buna karşılık
menzili tüm sahneyi kaplayan bir nokta ışık Forward+ kümelemesini işlevsiz bırakırdı.
Baskın ipucu ("yakın çakma kör eder, uzak olan soluk kalır") tamamen ters kare sönümden
geliyor.

**Bulutun parlaması ışın yürüyüşünün içine konamaz** — o on altı kareye yayılıyor ve
parlama blok blok titrerdi.

**Sis de parlamalı.** Sisin rengi sabit tutulunca fırtınada — şimşeğin çaktığı tek havada —
görüş yedi yüz metreye düşüyor ve arazinin büyük kısmı o değişmeyen rengin altında
kalıyordu: yüzey aydınlansa bile üstü örtülü olduğu için görünmüyordu.

**Parlama yerde toplanır, yönde değil.** Yön mesafe taşımıyor — yaklaştıkça büyümesi
gereken leke sabit açıda kalıyordu.

**Kol yalnız yakında.** Gerçekte de uzak şimşek kolunu göstermez: araya giren bulut ve hava
kanalı yutar, geriye denizin aydınlanması kalır.

---

## Volumetrik sis

**Savrulan kar tek kaynaktan.** Perde bir dönem hem froxel hacminde (her hücrede
`SpindriftAt`) hem arazi yolunda hesaplanıyordu. İkisi de aynı hatayı yapıyordu: alanın
içindeki sırt algılayıcı (`crest`/`lee`) 60–80 m'lik keskin eşikler taşıyor, froxel ızgarası
ve ışın örneklemesi o eşiklerin üstünden atlıyor ve kamera kıpırdadıkça yer değiştiren
dikey şeritler kalıyordu.

**Alanlar sinüs toplamıdır.** Bank ve akış alanı iki sinüsün çarpımıydı ve yorumu "çarpım
tekrar desenini kırar" diyordu; kırmıyor — `sin(k₁·p)·sin(k₂·p)` ayrıştırılabilir bir
ifadedir ve düzenli bir kafes üretir.

**Geçiş sırası ve `+2`.** `+2` gök sisini paketin `Opaque Atmospheric Scattering`
geçişinden sonraya düşürüyor; aynı anda çalışırlarsa siluet pikselini biri "gök" biri
"geometri" sayıp çift işliyor ve tek piksellik kontur bırakıyorlar. Gök sisi bir ara
bulutlardan SONRAYA alınmıştı — o zaman bulutu da sisliyordu ama **sonsuz mesafeden**;
bulut 2 km'de duruyor. Bir katmanı komşusunun mesafesiyle sislemek ya çift sayım ya yanlış
mesafe demek, ikisi de bulut kenarında sınır bırakıyor.

**`FogPath` neden ayrı döndürüyor.** Bulut önceden çarpılmış geliyor (`xyz` kapsamayla
ağırlıklı, `w` arkasını geçiren pay) ve saçılım payının bulutun kapsadığı orana
ölçeklenmesi gerekiyor.

**Ortamın seviyesi sis renginden.** Probe'un SH'si yüzey aydınlatması birimindedir; ortamın
istediği ortamın içeri saçtığı radyans. Ölçüldü: probe DC luminansı **0.156**, sis rengi
**0.492** — oran **3.15** (≈ π), ve o farkla ara mesafedeki puslu sırtlar kayboluyordu.


---

## Işığın rengi

**Gölge çizgisi yürümeli.** Sabit bir irtifa bandı dağın tamamını birlikte pembeleştirip
birlikte söndürüyordu; yapay duran şey buydu. Dünya'nın gölgesinin yüksekliği: 0.5° →
240 m, 1° → 975 m, 1.5° → 2190 m.

**Artçı fazda kaynak noktasal değil.** Güneş battıktan sonra aydınlatan şey kızıla
boyanmış bütün gökyüzü; yönlü sönüm ve güneş yönlü arazi gölgesi o fazda anlamsız.

**Kızıllık hesaplanır.** Sönüm üstel olduğu için iki renk arasında doğrusal geçiş bunu
üretemiyordu. En parlak kanal bire oturtulur — ölçekleyip birin üstüne çıkarmak ton
eşlemenin doygunluk düşürdüğü bölgeye taşıyor ve rengi beyaza çeviriyordu.

**Hava kütlesi ufuk altında sabitlenir** (ufukta ~22'ye tırmanır). Eski taban onu güneş
ufka varmadan kesiyordu: renk turuncuda kilitleniyor, batımın kızıl fazı hiç üretilmiyordu
— zincirin hiçbir tüketicisi kızıl gösteremezdi, kaynakta yoktu.

**Altın uç açık yazılır** çünkü süzülmüş güneşten çarpımla sarı üretilemez (yeşili
tükenmiş).

**Altın saat kademesi gerekli:** gece↔gündüz karışımı bu saati soğuk ve soluk basıyordu,
palet ne kadar kızıl olursa olsun ekrana pastel geliyordu.

---

## Renk düzenlemesi

**Pozlama karanlık ucu kaldırmaz.** Bir kez denendi: gece göğü ton eğrisinin dibinde kalıp
keskin sınırlı siyah bir bölge ürettiğinde `adaptShare` yükseltildi, belirti kapandı ama
parlak uç da yükselip gece sahnesini aydınlattı (`DECISIONS.md` → "Gecedeki fasulye
kapandı").

**Bloom eşiği faz karışımına bağlı.** `PHASE_LOBE_BLEND` 0.5'ten 0.15'e indirilince güneş
çevresindeki bulut **1.7 kat** parladı ve mevcut eşiği (1.10) aşıp bloom'a girmeye başladı
— deste kendisi hâle üretiyordu. Eşik beş ön ayarda da aynı oranla yükseltildi (altın saat
1.10 → 2.00). Ölçüm `SYMPTOMS.md` → "Şafakta güneşten uzak bulutlar yeterince kararmıyor".

---

## Arazi

**Ufuk haritası noktanın kendi eğimini çıkarır.** Eğimli düzlemde "ufuk güneşten yüksek"
ile "N·L ≤ 0" birebir aynı koşuldur; ikisi birden sayılırsa gölge iki kez uygulanır.

**Işıklandırma normali yer değiştirmeyi bilmeli.** Biri kullanmazsa siluet kabarır ama
ışık altındaki düz yüzeyi aydınlatır.

**En parlak kanal alınır** çünkü `Tint()` rengi aynı kanala göre normalize ediyor — böylece
renk ve şiddet aynı eğriyi izliyor.

**Kar yansıması gökyüzünden büyük.** Sahnede GI yok; gölgedeki bir noktanın çevresini güneş
vuran kar sarıyor ve kar albedosu 0.8.

**Tohum hash köküne uygulanır**, çağrı yerlerine değil — yeni katman eklendiğinde kaydırmayı
unutmak mümkün olmasın diye.

**Halkanın sönümü Chebyshev.** Arazi kare, köşe 15√2 = 21.2 km'de; yarıçap sönümü orada
silsileyi geri getiriyordu.

**Yalıtım halkası neden gerekli.** L0 bir silsile üretiyor ve kütle 1500 m eşiğinde 379 km'ye
uzanıyor; oyun alanı ne kadar büyük olursa olsun kenarda kesilir. Silsile ancak oyun alanının
ötesinde geri geliyor — o da uzak bantların işi.

**Nyquist sınırı.** Hücre 7.324 m → taşınabilir en kısa dalga 14.65 m. 320 m tabandan 8
oktav istenince son üçü (10, 5, 2.5 m) sınırın altında kalıp 2 hücrelik zikzak olarak geri
katlanıyordu; pişmiş haritada anomali 14.7 m'de, tabanın 3.5 katı. Ayrıntı `SYMPTOMS.md` →
"Arazide düzenli testere".


## Yağış perdesi — makaleden sapılan üç yer

Perde `[Langer 2004]`'ten port edildi. Portun üç yerinde makalenin yaptığı şey yerine
kendi kısayolum duruyordu; üçü de ekranda göründü ve kullanıcı bildirdi.

**Döşeme başına bağımsız sentez.** Makale her ekran döşemesi için AYRI IFFT alıyor
(`§7`), yani komşu döşemelerin gürültüsü ilişkisiz. Ben tek doku pişirip döşemeler
arasında küçük bir kaydırmayla (`tileIndex * 0.37`) paylaştırdım ve bunu dosya başlığına
"θ saf dönmedir, C saf zaman ölçeğidir" diye çözülmüş gibi yazdım. İki iddia da doğru,
ama üçüncüsü — bağımsızlık — hiç ele alınmamıştı. Komşular desenin neredeyse aynı yerini
okuyunca ekran tek lekenin ızgarasına döndü. Kullanıcı: *"niye bu kadar düzenliler"*.
Karşılığı döşeme indisinden hash: bağımsız sentezin ucuz karşılığı, aynı gürültünün
ilişkisiz bölgeleri.

**Genleşme odağı.** Odağı, akış yönünde 1000 m öteye konan bir dünya noktasının ekran
izdüşümünden buluyordum. Kar dik düştüğü için o nokta BAŞUCUNDA; kamerayı çevirince
izdüşümü kararsız bölgede geziyor, arkaya düşünce de kod ekran merkezine sıçrıyordu.
Kullanıcı: *"kamerayı hareket ettirince 360 derece dönüyorlar"*. Doğrusu kaybolan nokta —
yön vektörünü `w = 0` ile izdüşümden geçirmek. Çıkan `w`, yönün görüş eksenine yatkınlığı;
sıfıra giderken odak sonsuza gider ve akış ekranda paralelleşir. Sınır durum artık
tanımlı, geçiş sürekli.

**Yağmur.** Perdeyi baştan "kar perdesi" diye kurdum; yağmur yalnız opaklık çarpanıyla
ayrılıyordu, desen aynıydı. Ayrışması gereken iki şey var ve ikisi de pişirmede: bant
(damla taneden küçük, bir oktav yukarı) ve zamansal frekans (daha hızlı, dolayısıyla
Nyquist kesmesi daha çok bulanıklık üretir). Hız kanal başına PİŞMEK zorunda — döngüyü
çalışma zamanında hızlı oynatmak hızı artırır ama bulanıklığı artırmaz, sonuç strobe olur.

**Dikey iz denenmedi, çünkü kâğıtta çıkmıyor.** `ω_t = C·(ω·v̂)/|ω|` yalnız ω'nın YÖNÜNE
bağlı. İz üretmek spektrumu hareket ekseninde daraltmak demek; o modların `ω_t`'si sıfıra
gider ve desen durur. Yani bu çerçevede "hareketli iz" diye bir şey yok. İz, taneyi tek
tek çizen katmanın işi.

**Kare alma silindi.** Pişiricide `v *= v` vardı, gerekçesi "opaklığı aralığın altına
sıkıştırmak". Shader ortalamayı çıkarmaya başlayınca ikisi aynı işi yapar oldu — üstelik
kare alma saklanan ortalamayı 0.29'a kaydırdığı için shader'ın çıkardığı 0.5 ölçülenle
tutmuyordu. Telafi terimi geri eklenmez kuralının aynısı: gerekçesi kalmayan terim gider.

