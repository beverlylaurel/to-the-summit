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

## 0. Karın görsel yüzeyi kameraya bağlı, çarpışma yüzeyi değil

`SnowDisplacement.hlsl` kar geometrisini kameraya uzaklıkla söndürüyor: bölünme
`snowTessNear`–`snowTessFar` arasında kapanıyor, yer değiştirme ondan önce sıfıra
iniyor. `SnowSurface.DepthAt` ise sönüm uygulamıyor — çarpışma kamerayı bilmez.

Tek oyuncuda doğru: oyuncu kameranın konumunda, ayağının altındaki sönüm 1.

İkinci oyuncu geldiğinde yanlış olacak: uzaktaki oyuncunun bastığı kar SENİN ekranında
düz çiziliyor ama o kendi karının üstünde duruyor. Uzaktan bakınca zeminin bir metre
üstünde yürüyor görünür.

**Ne olması gerekiyor:** ya sönüm görünürlükten değil bir LOD kademesinden türeyecek
ve uzak oyuncu kendi kademesini taşıyacak, ya da uzak oyuncunun ayağı ağdan gelen
mutlak kotla çizilecek.

**Maliyet:** sönüm formülü tek yerde (`SnowDisplacement`), değişimi küçük. Asıl iş
uzak oyuncunun kotunun ağdan gelmesi — o zaten gerekecek.

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

---

## 2. Havayı tek oyuncunun yüksekliği sürüyor

`AltitudeWeatherDriver` yağışı, karlılığı ve rüzgâr şiddetini `observer`'ın kotundan
hesaplıyor. İkinci oyuncu vadideyken birincisi zirvedeyse ikisi ayrı havada olur.

Üstelik sürücü **kendi durumunu biriktiriyor**: `progressAltitude` ulaşılan en yüksek
seviyeyi tutuyor ve aşağı inince yavaş geriliyor. Bu, oyuncuya değil **koşuya** ait bir
sayı; her istemcide ayrı tutulursa aynı koşunun iki farklı ilerlemesi olur.

**Olması gereken:** host sürer, istemciler okur. İlerleme ortak koşu durumunun parçası.

**Maliyet:** düşük. Sürücü zaten tek yerde ve dışarıya yalnızca `WeatherState` ile
`WindField` üzerinden yazıyor.

---

## 3. Zaman yerel akıyor

`TimeOfDay.normalized` her istemcide kendi `deltaTime`'ıyla ilerliyor. `WindField` ve
`AltitudeWeatherDriver` gürültülerini `Time.time`'dan örneklüyor; bulut evrimi de öyle.

Bunlar **deterministik** — aynı zaman değeri aynı sonucu verir. Yani ayrı ayrı düzeltmeye
gerek yok, tek bir paylaşılan saat hepsini birden hizalar. Ama saat paylaşılmazsa hepsi
birden kayar: farklı gökyüzü, farklı rüzgâr, farklı bulut.

**Olması gereken:** günün saati ortak koşu durumundan gelmeli, `Time.time` yerine ondan
türeyen bir değer örneklenmeli.

---

## 4. Zemindeki kar örtüsü yerel birikiyor

`TerrainSurface` kot bandı başına iki integral tutuyor (örtü ve kalınlık deposu). Borç
**küçüldü**: birikme hızı artık oyuncunun bulunduğu kottan değil, bandın kendi kotundaki
havadan geliyor — yani girdi tamamen paylaşılan saatten türeyen gürültü. İki istemci aynı
anda başlarsa aynı yere gider.

**Kalan borç:** integralin *başlangıç anı*. Sonradan katılan oyuncu sıfırdan başlar,
diğerininki dolu.

**Olması gereken:** katılırken profilin o anki hâli gönderilmeli, ya da integral
paylaşılan saatten yeniden koşturulmalı.

**Not:** görsel bir değer, oynanışa girmiyor. Öncelik düşük — ama iki oyuncu yan yana
durup birinin karlı diğerinin çıplak bir yamaç görmesi tuhaf.

---

## 5. Test paneli dünyayı yerel eziyor

`DebugMenu` rüzgârı ve bulut kapsamasını doğrudan yazıyor, havayı ise sürücünün kendi
hedef anahtarından (`AltitudeWeatherDriver.IntensityOverride`) veriyor; `LightningFlash.Held`
çakmayı donduruyor. Bunlar bilerek yerel: ölçüm ve hata ayıklama aracı.

**Olması gereken:** co-op oturumunda ya kapalı olmalı ya da otoritedeki oyuncuda çalışıp
herkese yayılmalı. Karar verilmeden açık bırakılırsa "bende fırtına vardı sende yoktu"
diye bir hata raporu üretir.

---

## Henüz yazılmadı, ama bu katmanı isteyecek

Bunlar borç değil — ortada düzeltilecek kod yok. Ama ağ katmanını **tasarlayan** kişinin
bilmesi gerekiyor: yakında ne geleceğini bilmeden kurulan bir otorite modeli, geldiğinde
yeniden kuruluyor.

| Ne | Ne isteyecek | Ayrıntı |
|---|---|---|
| **Kar üzerinde ayak izi** | Oyuncunun araziye yazdığı, dünyaya çakılı bir harita. Kimin tuttuğu, kimin yaydığı, kimin otorite olduğu | `DECISIONS.md` → "Ayak izi ertelendi" |
| **Tırmanma ve ip** | Oyuncular arası fiziksel bağ; iki oyuncunun aynı ipe asılı olması | `DECISIONS.md` → "Oynanış mekaniği netleşmeden koda başlanmaz" |
| **Envanter ve ekipman** | Oyuncu durumu, kayıp/ölüm senkronu | aynı madde |
| **Kamp ve sığınak** | Paylaşılan etkileşimli obje, ortak koşu durumu | aynı madde |
| **Bulut sürüklenmesi** | Bulutların konumu **her istemcide yerel birikiyor**: `VolumetricCloudsURP` her karede `windVector += deltaTime × globalSpeed × yön` yapıyor. Şu an `globalSpeed = 0` olduğu için hiçbir şey kaymıyor ve borç doğmuyor; rüzgâr bağlandığı gün iki oyuncu farklı gökyüzü görür — sonradan katılan ise bambaşkasını. Birikimin ağdan gelen mutlak bir zamandan türemesi gerekecek | `CLOUDS_REBUILD.md` → v1 bağları, rüzgâr |

Bu satırlardan biri yazıldığı gün karşılığı yukarıdaki borç listesine geçer.

---

## Borç doğurmayanlar

Bunlar ağ eklendiğinde olduğu gibi kalabilir; listeye tekrar girmesinler:

- **Terrain üretimi** — tohumdan deterministik, her istemcide aynı dağ çıkar
- **Yüzey haritaları** — terrain'den türüyor, ayrıca taşınmaları gerekmez
- **Gölgelendiriciler, tanecik biçimi, ses karışımı, post-process** — tamamı yerel görüntü
- **Ayar asset'leri** — build'in parçası, çalışma zamanında değişmiyorlar
