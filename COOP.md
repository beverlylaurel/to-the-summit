# Co-op borcu

Ağ katmanı gelince eli değecek yerlerin listesi. Hepsinin tetikleyicisi aynı tek olay,
o yüzden ayrı bir dosyada duruyorlar: ağa başlamadan **önce** baştan sona okunacak.

Buradaki hiçbir madde şu an bir hata değil. Tek oyuncuda hepsi doğru çalışıyor; ikinci
oyuncu geldiğinde yanlış olacaklar.

Üç bölüm var: **şu an duran borç**, **henüz yazılmamış ama bu katmanı isteyecek işler**,
ve **borç doğurmayanlar**. İkincisi listede çünkü ağ katmanını tasarlayan kişinin yakında
ne geleceğini bilmesi gerekiyor; üçüncüsü listede çünkü aynı şeyler tekrar tekrar
gündeme geliyordu.

Co-op'un **kararı** ve Claude'un uyarı sorumluluğu burada değil, `DECISIONS.md`'de.
Bu dosya envanter, karar değil.

Yeni bir borç fark edildiğinde **aynı adımda** buraya yazılır. Ödendiğinde satır silinir.

---

## 1. Rastgelelik paylaşılmıyor

Dört yerde yerel `UnityEngine.Random` var. Her istemci kendi tohumundan çektiği için
aynı anda farklı sonuç üretirler.

| Yer | Ne seçiyor |
|---|---|
| `ThunderPlayer` | Çakma aralığı, uzaklık, klip, perde/pan/ses, kesim frekansı |
| `LightningFlash` | Çakmanın yönü, geri vuruş sayısı ve genlikleri, aralıkları |
| `LightningBolt` | Kanalın biçimi, çatalların ayrılma noktası ve yönü |
| `AudioBand` | Klip seçimi ve geçiş zamanlaması |

**Olması gereken:** çakmanın **anı, uzaklığı ve yönü** paylaşılan bir durumdan gelmeli —
ya host yayar ya da ortak bir tohumdan türetilir. Kanalın kıvrımı ve klip seçimi görsel
süs; onlar yerel kalabilir.

**Neden önemli:** şimşek artık dünyada bir yerde. İki oyuncu aynı vadide durup farklı
yönde çakan farklı şimşekler görürse dünya tek bir dünya olmaktan çıkar. Ses gecikmesi
mesafeden türediği için gürleme zamanları da tutmaz.

**Maliyet:** `Struck` olayı zaten uzaklığı taşıyor ve `LightningFlash` konumu ondan
üretiyor. Tetikleyen tarafı otoriteye bağlamak yetiyor — zincirin gerisi kendiliğinden
hizalanır. Bu yapı bilerek böyle kuruldu.

**NE ZAMAN ÖDENECEK — kararlaştırıldı (2026-08-17):** ayrı bir iş olarak değil, **şimşek
spec'i yazılırken**. Şimşek zaten sıfırdan yeniden yazılacak (`specs/lightning/`); o tur
sırasında rastgeleliğin hangi kısmı paylaşılan durumdan gelecek, hangisi yerel süs kalacak
baştan kurulur. Sonradan eklenirse tetikleme zinciri ikinci kez elden geçer.

Yani bu madde **ağ katmanını beklemiyor** — şimşek spec'i başlarken açılır.

---

## 2. Dünya fırtınasının saati her istemcide ayrı işliyor

Fırtınanın kendisi artık oyuncuya bağlı değil: `WorldStorm` dünyanın durumu, kot yalnız
payını veriyor. Ama sürücü o fırtınayı `Time.time` üzerinden Perlin ile örneklüyor —
istemciler farklı anda bağlanırsa saatleri kaymış olur ve biri fırtına görürken öteki
açık hava görür. Deniz de aynı sayıdan türediği için ayrışma denizde de görünür.

**Olması gereken:** host fırtına saatini sürer, istemciler okur. Kot payı yerel kalabilir —
zaten oyuncunun bulunduğu yerin havası odur.

**Maliyet:** düşük. Sürücü zaten tek yerde ve dışarıya yalnızca `WeatherState` ile
`WindField` üzerinden yazıyor.

**Denizin ikinci tüketicisi olduğu yer.** Dalga spektrumu `U10`'dan türüyor; rüzgâr
istemciler arası ayrışırsa deniz de ayrışır — biri fırtınalı deniz görür, öteki sakin.
Bu **yeni bir borç değil**, bu borcun kapsamını genişletiyor.

---

## 3. Zaman yerel akıyor

`TimeOfDay.normalized` her istemcide kendi `deltaTime`'ıyla ilerliyor. `WindField` ve
`AltitudeWeatherDriver` gürültülerini `Time.time`'dan örneklüyor; bulut evrimi de öyle.

Bunlar **deterministik** — aynı zaman değeri aynı sonucu verir. Yani ayrı ayrı düzeltmeye
gerek yok, tek bir paylaşılan saat hepsini birden hizalar. Ama saat paylaşılmazsa hepsi
birden kayar: farklı gökyüzü, farklı rüzgâr, farklı bulut.

**Olması gereken:** günün saati ortak koşu durumundan gelmeli, `Time.time` yerine ondan
türeyen bir değer örneklenmeli.

**Denizin de tüketicisi olduğu yer.** `SeaSimulation` dalga alanının zamanını
`Time.time`'dan alıyor. FFT deterministik (aynı `t`, aynı spektrum, aynı yüzey), yani
paylaşılan saat sorunu kendiliğinden çözer — ama saat paylaşılmazsa iki istemci aynı
dalgayı farklı fazda görür. Kıyıda yan yana duran iki oyuncu için bu doğrudan görünür.

Deniz **yeni borç doğurmuyor**: mesh her istemcide kendi kamerasına snap'leniyor ve
bunun paylaşılmasına gerek yok.

---

## 5. Test paneli dünyayı yerel eziyor

`DebugMenu` rüzgârı ve bulut kapsamasını doğrudan yazıyor, havayı ise sürücünün kendi
dünya anahtarından (`AltitudeWeatherDriver.WorldStormOverride`) veriyor; `LightningFlash.Held`
çakmayı donduruyor. Bunlar bilerek yerel: ölçüm ve hata ayıklama aracı.

**Olması gereken:** co-op oturumunda ya kapalı olmalı ya da otoritedeki oyuncuda çalışıp
herkese yayılmalı. Karar verilmeden açık bırakılırsa "bende fırtına vardı sende yoktu"
diye bir hata raporu üretir.

---

## 6. Bulut sürüklenmesi her istemcide yerel birikiyor

`VolumetricCloudsURP` bulut konumunu her karede biriktiriyor:
`windVector += deltaTime × globalSpeed × yön`. Başlangıç sıfır, artış yerel kare
süresinden geliyor.

`CloudWeatherDriver` rüzgârı bağladı, yani `globalSpeed` artık sıfır değil ve birikim
gerçekten işliyor.

Tek oyuncuda doğru: tek bir birikim var.

İkinci oyuncu geldiğinde yanlış olacak: iki istemcinin kare süreleri ve başlama anları
farklı, dolayısıyla `windVector` ayrışır. Aynı anda gökyüzüne bakan iki oyuncu **farklı
bulut deseni** görür; sonradan katılan ise bambaşkasını. Yer bulut gölgesi de aynı
alandan türediği için gölgeler de tutmaz.

**Ne olması gerekiyor:** birikim yerel `deltaTime` toplamından değil, ağdan gelen mutlak
bir dünya zamanından türemeli — `windVector = worldTime × globalSpeed × yön`. O zaman
her istemci aynı deseni hesaplar.

**Maliyet:** küçük. Birikim tek yerde (`VolumetricCloudsURP.UpdateMaterialProperties`).
Asıl iş mutlak dünya zamanının ağdan gelmesi — o zaten gerekecek (bkz. madde 3).

---

## 7. Ayak izi yerel simülasyon

`SnowManager` izi kendi GPU dokusuna yazıyor (`RT_SnowTrail`) ve o doku
tamamen yerel: kimin yazdığı, kimin yaydığı, kimin otorite olduğu tanımlı
değil. Tek oyuncuda doğru — iz yazan da okuyan da aynı makine.

**Olması gereken:** iz dünyaya çakılı bir harita ve paylaşılan bir durum.
İkinci oyuncunun izi birincide görünmeli, ve iki oyuncu aynı yere basınca
kar iki kez sıkışmalı.

**Neden şimdi büyüdü:** iz artık yalnız gölgelendirme değil, GERÇEK GEOMETRİ
(`SnowTessYerDegistirme` içinde `SnowDentSmooth`). Karakter kendi izinin
üstünde duruyor. İz senkronize değilse iki oyuncu farklı zeminde yürür —
görsel uyuşmazlık değil, konum uyuşmazlığı.

**Maliyet:** deformasyon bölgesi 24 m ve kamerayla kayıyor. Paylaşım o
bölgenin durumu değil, DEFORMER OLAYLARI üzerinden kurulmalı (kim, nerede,
hangi yarıçapla bastı) — bölge içeriği her istemcide aynı olaylardan
deterministik olarak yeniden üretilir.

**Bu madde daha önce "henüz yazılmadı" bölümündeydi ve orada kalmıştı;
ayak izi yazıldığı hâlde satır taşınmamıştı.**

---

## Henüz yazılmadı, ama bu katmanı isteyecek

Bunlar borç değil — ortada düzeltilecek kod yok. Ama ağ katmanını **tasarlayan** kişinin
bilmesi gerekiyor: yakında ne geleceğini bilmeden kurulan bir otorite modeli, geldiğinde
yeniden kuruluyor.

| Ne | Ne isteyecek | Ayrıntı |
|---|---|---|
| **Envanter ve ekipman** | Oyuncu durumu, kayıp/ölüm senkronu | aynı madde |
| **Kamp ve sığınak** | Paylaşılan etkileşimli obje, ortak koşu durumu | aynı madde |

Bu satırlardan biri yazıldığı gün karşılığı yukarıdaki borç listesine geçer.

---

## Borç doğurmayanlar

Bunlar ağ eklendiğinde olduğu gibi kalabilir; listeye tekrar girmesinler:

- **Terrain üretimi** — tohumdan deterministik, her istemcide aynı dağ çıkar
- **Kar yüzeyi tessellation'ı ve yer değiştirmesi** — tamamen yerel görüntü;
  her istemci kendi kamerasına göre bölüyor, dünya aynı kalıyor.

  **AMA BİR KURAL DOĞURDU.** Kar yüzeyi yükseklik fonksiyonu
  (`SnowYuzeyRolyef` ve C# ikizi `SnowSurfaceHeight`) **saf kalmak zorunda**:
  girdisi yalnız dünya konumu, kar durumu ve rüzgâr maruziyeti. Kare sayacı,
  `Time` veya yerel rastgelelik girerse iki oyuncu farklı zeminde yürür —
  karakter konumu ağ üzerinde paylaşıldığı için bu doğrudan uyuşmazlık olur.

  Şu an temiz. "Rüzgârla dalgalanan yüzey" gibi bir özellik eklenirse borç
  anında doğar ve bu satır borç listesine taşınır.
- **Yüzey haritaları** — terrain'den türüyor, ayrıca taşınmaları gerekmez
- **Gölgelendiriciler, tanecik biçimi, ses karışımı, post-process** — tamamı yerel görüntü
- **Ayar asset'leri** — build'in parçası, çalışma zamanında değişmiyorlar

  değişiyor. Ağ gelince preset ve geçiş zamanı sunucudan gelmeli; birikme hesabı
  deterministik olduğu için başka bir şey senkronlanması gerekmiyor.

- **İz kalıcılığı yok.** Kalıcılık yalnız kar durumunu (SWE, yoğunluk) saklıyor;
  ayak izi bölgeden çıkınca unutuluyor. Tek oyuncuda görünmüyor — bölge 24 m ve
  oyuncu merkezinde. Co-op'ta ikinci oyuncu birincinin izini bölge dışından
  gelirken göremez. Çözümü depo çözünürlüğünü izinkine çıkarmak (4 m blok için
  64² değil 171²) ya da izi ayrı, seyrek bir yapıda tutmak.
