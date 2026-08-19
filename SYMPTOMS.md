# Belirti → Sebep

Ölçülerek bulunmuş belirtiler. Amaç tek: **aynı belirti tekrar görüldüğünde aramaya
baştan başlamamak.**

Her kayıt üç şey taşır: belirtinin kullanıcının ağzından hâli, ilk şüphelinin ne olduğu
(ve neden yanlış olduğu), gerçek sebep. Kayıt ancak **ölçümle** kapanmış bir turdan
doğar — tahminle çözülen bir şey buraya yazılmaz.

Bu dosyanın kendi dersi: **belirtinin göründüğü yer, belirtinin doğduğu yer değildir.**
İlk şüphelisi kayda geçmiş dokuz belirtinin **dokuzunda da** ilk şüpheli yanlış çıktı —
her seferinde belirtinin en çok göze çarptığı katman seçilmişti. Bulut kontüründe üç ayrı
şüpheli (ışın yürüyüşü, zamansal birikim, mesafe sınırı) sırayla elendi; sebep dördüncüdeydi.

---

## Ekranın yarısı simsiyah, kamerayla birlikte geliyor

**İlk şüpheli:** sis. *(Yanlış — sis o pikselleri `renk × 1 + 0` ile aynen geçiriyordu.)*

**Sebep:** ters-Z'de `UNITY_RAW_FAR_CLIP_VALUE = 0`. O derinlikten dünya konumu geri
kurulunca sonuç **NaN**. NaN, `Blend One SrcAlpha` karışımından geçince arkasındaki her
şeyi siliyor — arazi, gökyüzü, bisiklet.

**Ayırt eden ölçüm:** Frame Debugger. Siyah, `Opaque Atmospheric Scattering`'e kadar
yoktu; bulut birleştirmesinde beliriyordu.

**Kural:** uzak düzlemden dünya konumu kurulacaksa ya yalnız **yön** kullanılır
(`normalize(far − kamera)` güvenli), ya da mesafe sınırlanır. Ham büyüklük kullanılmaz.

---

## Siluet kenarında tek piksellik kontur (gündüz koyu, denetimde beyaz)

**İlk şüpheli:** TAA. *(Yanlış — TAA'ya dokunulmadan kontur gitti.)*

**Sebep:** iki geçiş aynı pikseli **farklı sınıflandırıyordu**. Gök sisi `ZTest Equal` ile
onu "gökyüzü" sayıp sisliyor, paketin `Opaque Atmospheric Scattering`'i derinlik
dokusundan "geometri" sayıp kendi hava perspektifini biniyordu. Tek piksellik şeritte
çift işlem.

**Ayırt eden ölçüm:** dağın **gövdesi** düz kalıyordu, yalnız kenar bozuktu. Yüzey başına
hesaplanan bir şey tek piksellik kenar üretemez.

**Kural:** iki geçiş aynı pikseli farklı ölçütle sınıflandırıyorsa sıralarını ayır.
Geçiş noktaları `VolumetricFogFeature`'da yazılı.

---

## Gece etraf fazla aydınlık ("sisi kapatınca gerçekçi oluyor")

**İlk şüpheli:** ay şiddeti. *(Yanlış — `MoonIntensity` 0.0199'a dokunulmadı.)*

**Sebep:** sis renginin **seviyesi** elle yazılmış bir sabitti ve gökle birlikte
kaymıyordu. Gök gündüz–gece arası ~230 kat değişirken sis rengi 9.6 kat değişiyordu.

**Ölçüm:** sis rengi ÷ ortam probu DC → gündüz 1.43, gece 34.6. Kalibre oran 3.15.
Yani gündüz 2.2 kat fazla koyu, gece **11 kat fazla parlak**; gece sisin örttüğü her şey
3.5 durak yukarı kalkıyordu.

**Kural:** bir görsel seviye sabitten geliyorsa, o sabitin **hangi tek koşulda** doğru
olduğu sorulur. Cevap "biri" ise seviye ölçümden gelmelidir, ton sabitten.

---

## Ufka yapışık ince çizgi, kamerayla geliyor

**İlk şüpheli:** arazi geometrisi, uzak karo kenarları. *(Yanlış — sahnede sabit
kalmıyordu, kamerayla geliyordu.)*

**Sebep:** **sert kırpma**. `max(dir.y, 0.02)` sürekli ama türevi kırılıyor; göz türev
kırılmasını Mach bandı olarak okuyor. Aynı sınıf ikinci kez `abs(ks) > 1e-6 ? … : L`
eşiğinde çıktı — eşiğin iki yakası %16 fark veriyordu.

**Kural:** görsel bir gradyanı besleyen ifadede `max`/`min` ile taban koyma; yumuşak taban
kullan (`sqrt(y² + taban²)`) ya da seri açılımı (`1 − x/2 + x²/6`). Sıfırda sonsuz eğimli
üsler (`pow(x, <1)`) de aynı sınıf.

---

## Düzenli kafes deseni (sis banklarında, akış alanında)

**İlk şüpheli:** çözünürlük, örnekleme. *(Yanlış — desen matematikten geliyordu.)*

**Sebep:** sözde-rastgele alan **çarpımla** kurulmuştu: `sin(k₁·p) · sin(k₂·p)`. Bu ifade
ayrıştırılabilir ve düzenli bir kafes üretir; frekans karıştırmak bunu değiştirmez.
Koddaki yorum "iki farklı frekansın çarpımı tekrar desenini kırar" diyordu — kırmıyor.

**Kural:** rastgele alan modların **üst üste binmesidir**, çarpımı değil. Yönleri paralel
olmayan, dalga boyları oransız birkaç bileşen **toplanır**. Sinüs seçilirse CPU ile GPU
birebir aynı sonucu verir; hash tabanlı gürültü vermez ve bu projede alanın CPU kopyası
da var.

---

## Rüzgâr arttıkça yukarı uzanan, titreyen dikey şeritler

**İlk şüpheli:** akış alanının deseni. *(Yanlış — alan düzeltildi, şerit kaldı.)*

**Sebep:** **keskin bir alanın az örnekle taranması**. `SpindriftAt` içindeki sırt
algılayıcı (`crest`/`lee`) 60–80 m'lik keskin eşikler taşıyor ve her okumada dört ayrı
arazi yüksekliği örnekliyor. Işın boyunca sekiz örnek bu alanın üstünden atlıyor; kamera
kıpırdadıkça örnekler başka yere düşüyor.

**Kural:** örnek sayısını artırmak çözüm değil — alan keskin kenarlı. Tek okuma noktası +
kapalı biçim integral kullanılır. Gök yolu (`SkyFogDepth`) bunu baştan doğru yapıyordu;
arazi yolu ondan öğrendi.

---

## Bulut kenarında siyah kontur (arazi arkadayken görünür, gök arkadayken görünmez)

**İlk şüpheli:** bulut ışın yürüyüşü, sonra zamansal birikim, sonra mesafe sınırı.
*(Üçü de yanlış — kontur bunlar değişmeden duruyordu.)*

**Ayırt eden ölçüm:** F1 "Bulut sisini KAPAT" anahtarı (`_CloudFogOff`). Kapatınca kontur
gidiyor, yükseklik sisi arazide/gökte açık kalsa bile — yani sebep birleştirme geçişinin sis
bloğu. *(Anahtar teşhis bitince kaldırıldı; sebep kesin bulundu, ölçüm aracı yerinde bırakılmadı.)*

**Sebep:** bulut yarı çözünürlükte çiziliyor, renk **bilinear** büyütülüyor ama derinlik
**nokta** örnekleniyor. Uyuşmadıkları bleed halkasında `edgeOfClouds` tetikleniyor ve
derinliğe uzak düzlemin bir tık berisi (`CLOUDS_RAW_FAR_CLIP_VALUE`) **uyduruluyordu**.
Sis o sahte mesafeyle (~70 km) hesaplanınca doyuyor → halka hava rengine boyanıyor.
Arazi arkadayken (koyu) fark okunuyor, gök arkadayken (hava rengi≈gök) görünmüyor.
Derinlik çıkışı kapatılınca **her** bulut pikseli uydurma alıyor → ince kontur yerine
kocaman siyah lekeler. Aynı hata iki ölçekte.

**Çözüm:** kombine geçişi derinlik **yazmıyor** (`Blend One SrcAlpha`), yani uydurmanın
hiçbir işlevi yoktu. Kaldırıldı: halka pikseli uzak düzlem derinliğini korur → `hasCloud`
false → sissiz geçer (opaklık≈0, görünmez). Gövde gerçek `meanDistance` türevli derinlikle
sislenir. Eski "mesafeyi sınırla" telafisi (clamp) de silindi — gerekçesi kalktı.

**Not:** "pikseli elemek 1px sissiz halka bırakır, o da kontur" korkusu ölçümle çürüdü
(sis-tamamen-kapalı = temiz). Eleme değil, uydurmayı kesmek doğru olanmış.

---

## Katılımcı ortam (sis, bulut) fazla koyu ya da fazla parlak

**Sebep:** birim karışması. `RenderSettings.ambientProbe` **yüzey aydınlatması**
(ışınım) birimindedir; katılımcı ortam içeri saçtığı **radyansı** ister. Dönüşüm **π**.

**Ölçüm:** bu projede bir kez ölçüldü — probe DC luminansı 0.156, sis rengi 0.492, oran
**3.15**. Aynı dönüşüm üç tüketiciye de uygulandı: froxel sisi, bulut ortamı, sis rengi.

**Kural:** yüzeyler `SampleSH(normalWS)` ile doğru birimi alır, ortamlar almaz. Yeni bir
ortam tüketicisi eklenirse π sorulur.

---

## Şafakta güneşten uzak bulutlar yeterince kararmıyor

**İlk şüpheliler — üçü de yanlış çıktı:**

- **Ortam ışığı fazı boğuyor.** Işık payı probu her saatte, her yönde doğrudan terimi
  baskın gösterdi. Yanlış.
- **Ton eşleme 24 katı sıkıştırıyor.** Yanlış — faz karışımı düzeltilince fark ekranda
  göründü, yani görüntüleme zinciri aralığı hiç kırpmıyormuş. *(Bu şüpheli bir ara
  `GameProfile`'daki `Tonemapping` bileşenine bakılarak "Neutral" sanılıp kâğıtta
  elendi; ölçüm yanlıştı — `LookController` her karede `TonemappingMode.ACES` yazıyor.
  Profil asset'i ne yazarsa yazsın, ton eşlemenin sahibi odur.)*
- **Güneş geçirgenliğinin tabanı** (`SunTransmittanceFloor`, HZD'de çok saçılmanın
  yerine geçen terim; portta çok saçılım oktavları zaten var, yani çift sayım).
  Anahtarla kapatıldı — ekranda neredeyse hiçbir şey değişmedi. Yanlış. *Sebebi: optik
  derinlik kodun yorumundaki örnekten (16) alınmıştı; gerçekte çok daha düşük, taban
  hiç devreye girmiyor.*

**Gerçek sebep:** `PHASE_LOBE_BLEND` 0.5. `lerp` iki lobu **ağırlıklı ortalıyor** ve
geri lob (`HG(−0.5)`) 90°'de ileri lobun **üç katı** — uzak alanı tek başına o ayakta
tutuyordu. Sayı Frostbite'ın brief'teki varsayılanıydı, bu sahnede hiç doğrulanmamıştı.

**Ayırt eden ölçüm:** durak konturu — bulutun kendi radyansını (kapsamaya bölerek) bir
duraklık bantlara ayırıp döngüsel renge basar. Deste 3 durağa sıkışmış ve **%70'i tek
bant** içinde çıktı. Aynı kontur birleştirmeden önce/sonra iki kez okundu: hava
perspektifi orta alanı ~1 durak kaldırıyor, gerisi ışın yürüyüşünün kendisinde.

**Kural:** faz parametreleri **bağlıdır**, tek tek ayarlanamaz. Karışımı düşürmek geri
lobun payını azaltır ama ileri lobunkini yükseltir — güneş çevresi 1.7 kat parladı ve
ayrıca karşılanması gerekti. İleri lobun eksantrikliği de kaldıraç değil: g düşünce tepe
az iner, lob genişler, uzak alan yükselir (0.60'ta 90° değeri düzeltmeden önceki
hâlinden bile parlak).

---

## "Dikdörtgen dağ", "kenarlar zirveyle neredeyse aynı yükseklikte"

**İlk şüpheli yanlıştı.** Sanılan: arazi küçük, dağ sığmıyor. Ölçüldü: kütle 1500 m
eşiğinde **379 km**'ye uzanıyor — bu bir SİLSİLE, hiçbir boyutta sığmaz. Büyütmek tek
başına kenar kesilmesini çözmüyor.

**Gerçek sebep iki katmanlı.** Birincisi maskedeki radyal kubbe 13 km'ye kadar açıktı,
yani 6 km'de hâlâ yarı yükseklikteydi; her azimutta aynı kota düşen bir zirve halkası
üretiyordu (4–8 km bandı zirveden yalnız **191 m** aşağıda). Kubbe 4 km'de bitirildi.
İkincisi ve asıl olan: karakter maskesine yalıtım halkası eklendi ama **`massif` listeye
alınmamıştı** — halka karakteri kırdı, kotu kırmadı, çünkü `massif` elev'i 3540 m.
Doğu yönünde 9–14 km bandı 3015 m'de kaldı. `massif` listeye alınınca kapandı.

**Ayırt eden ölçüm:** bant bant "zirveden kaç metre aşağıda". 4–6 km bandı 191 m → **1090 m**.

**Ve dış sönüm yarıçapa göre yapılmamalı.** Arazi kare; halka 15 km'de sönerse karenin
köşesi (15√2 = 21.2 km) halkanın dışında kalır ve orada silsile geri gelir. Chebyshev
mesafe (`max(|x|,|y|)`) kareyi izler.

Dört kenarın ortancası: 3873/3665 m → **214–389 m**. Kenar denetimi artık üretimde,
1200 m tavanı aşan kenar hata fırlatıyor.

## Ekranın ortasında siyah sivri iğne

**Sebep:** `MountainGenerator.FileCrests()` L1'e taşınırken alınmamıştı. Üçgenleştirme
+ gürültü, ızgaraya çapraz sırtlarda tek hücrelik iğneler bırakıyor: **5249 hücre**
komşusunun 400 m üstünde, en kötüsü 1343 m.

**İlk düzeltme yanlıştı ve ölçüm yakaladı.** C#'ın kör yumuşatması ("pencerede tek başına
yüksek olanı indir") aynen taşındı ve **zirveyi 5709 → 5696 m**'ye indirdi: o tanıma
GERÇEK zirve de giriyor. Filtre iğneyle tepeyi ayırt edemiyor.

**Ayırt eden büyüklük eğim.** İğneler 14.7 m'de 400–1343 m yükseliyor, yani 88°'den dik;
gerçek kaya yüzü 72°'yi aşmıyor. Ama eğim tavanı da tek başına yetmedi — zirve konisi son
15 metrede 70 m düşüyor (78°) ve **5608 m**'ye indi. Üçüncü ölçüt uydurulmadı: gürültünün
ve erozyonun zirveleri korumak için zaten kullandığı `uplift` alanına bağlandı.

Sonuç: sivri **5249 → 0**, zirve **5709.0 m tam**.

## Zirve spawn'dan görünmüyor — ama ölçüm yalan söylüyordu

Görüş hattı probu "+12 m KAPALI" dedi. Engelin yeri sorulunca **zirvenin kendisi** çıktı
(zirveden 0.00 km). Dünya eğriliği görüş hattını son metrelerde zirvenin kendi kotunun
altına indiriyor, prob da onu engel sayıyor.

Işının **son 150 m'si** dışarıda bırakılınca gerçek sonuç: açıklık **−24 m, GÖRÜNÜYOR**.

**Kural:** görüş hattı probunda hedefin kendi hücresi engel listesine girmemeli. Bu üçüncü
kez aracın yalan söylemesi; aşağıdaki bölüme bakılır.

## Güneşle dönen, hiçbir gölge anahtarının etkilemediği koyu lekeler

Kullanıcı: *"bu gölgeler ne ayak? havada bulut yok"*, sonra *"gölge olmaması gereken
yerde gölge var"*.

**Üç şüpheli de ölçümle elendi**, hiçbiri değildi:

| şüpheli | ölçüm |
|---|---|
| ufuk haritası kabalığı | 1024 vs 4097 uyuşmazlık %0.6–2 |
| ufuk haritası açısal (16 yön) | %0.5–0.8, alçak güneş dahil |
| normal dokusu kabalığı | 2048'de taraf değiştiren piksel %0.2–1.2 |
| bulut cookie'si | anahtar kapatılınca ekran değişmedi |

**Gerçek sebep:** örgü domain aşamasında kabarıyor (`SnowDomainPositionWS`) ama ileri
ışıklandırma geçişi pişirilmiş arazi normalini kullanıyordu. `SnowDisplacedNormal`
DepthNormals geçişinde vardı, ileri geçişte yoktu — üç geçişten yalnız biri ayrıktı.
Fonksiyonun kendi yorumu doğru davranışı zaten yazıyordu: *"harmanlamazsa siluet kabarır
ama ışık düz yüzeyi aydınlatır."*

**Ayırt eden ölçüm:** renk probu. Gölgelendirme normali "sırtı dönük" derken `ddx/ddy`
ile alınan gerçek yüzey normali güneşi görüyordu — o piksellere ayrı bir renk (mor)
verilince sınıf tek bakışta ayrıldı.

## Güneş tam karşıda ve yüksekken ayağının dibindeki yamaç gölgede

Kullanıcı: *"08:23'te gölge oluşması için hiçbir sebep yok ki."* Haklıydı.

**Sebep:** ufuk yürüyüşü ilk adımda komşu texel'i okuyor; eğimli yamaçta o komşu zaten
yukarıda ve engel sayılıyor. Ama **eğimli bir düzlemde iki koşul birebir aynıdır** —
"ufuk güneşten yüksek" ile "N·L ≤ 0". Yani yamacın kendisi hem ufuk haritasında hem
N·L'de sayılıyordu.

**Ölçüm** (azimut 200°, zirveden 6 km içinde):

| | değer |
|---|---|
| ufuk açısı ortancası | 16.5° |
| kendi eğimi çıkınca gerçek engel | **2.0°** |
| ufkun TAMAMI kendi eğimi olan nokta | **%46** |
| güneş 30°'de gölgede kalan yüzey | **%36 → %9** |

Çıkarma **açı uzayında** yapılır: eğimler tanjant, tanjant farkı açı farkı değil.

## Batımda sahne kahverengi-siyah, kontrast patlıyor

**Sebep:** `SunBlend`'in üst eşiği `sin(3°)`. Güneş 3°'nin üstündeyken ışık **hep tam
şiddette** — hava kütlesi sönümü yok. Gerçekte 3°'de doğrudan huzme zenit değerinin
%5–10'u, 10°'de %30, 40°'de %75.

`CurrentSunColor` sönümü zaten hesaplıyordu ama ışığa yazılmıyordu; gerekçe "soğurmanın
sahibi gökyüzü paketi"ydi. **Paket bir Unity directional light'ını söndüremez** — soğurma
göğe uygulanıyor, güneşe uygulanmıyordu. Gök sönerken arazi tam güneş almaya devam
ediyordu.

Düzeltmeden sonra 17:49'da güneş şiddeti 3.020 → **0.258**.

## Kar sınırı sert, kenarda dantel gibi bir dokuya dönüyor

**İki yanlış düzeltme.** Sırayla:

1. **Eşik penceresi** `smoothstep(0.03, 0.18)` → `(0.03, 0.45)`. Gerekçe: kırılma
   gürültüsünün genliği (±0.15) pencerenin tamamı kadardı, yani ikili sonuç üretiyordu.
   Ölçüm doğruydu ama sebep bu değildi. **Yerinde bırakıldı**, geçişi genişletiyor.
2. **Bakı terimi kaba mip'ten okundu.** Karo normali 14.65 m'de ortanca 4° değişiyor,
   `dot`'u 0.17 kaydırıyor, `_SnowlineSunLift` 200 m ile çarpılınca kar çizgisi 15
   metrede 34 metre oynuyordu. Fizik olarak da doğru düzeltme: bakının kar çizgisine
   etkisi mevsimlik ışınım üzerinden ve o bir yamaç yüzü büyüklüğü, tek karonun değil.
   **Yerinde bırakıldı**, ama sebep bu da değildi.

**Gerçek sebep bant probuyla bulundu:** `cover`'ın **ORTALAMASI zaten yumuşaktı** —
yedi bandın hepsi geniş bir kuşak kaplıyordu. Sert olan **yerel varyanstı**: bantların
içi tuz-biberdi, komşu iki piksel birkaç bant atlıyordu.

`sprinkle = MountainFbm(worldPos * 0.05, 2)` — 20 m taban. Geçiş kuşağı boyunca her karo
bağımsız zar atıyordu. Gözün "sert" dediği şey kenarın **genişliği değil DOKUSU**.

Düzeltme: `worldPos * 0.008, 4` — 125 m taban, 4 oktav. Sınır dolaşarak düzensiz, ince
bileşen 1/8 genlikte duruyor.

**Kural:** "sert görünüyor" iki ayrı şey olabilir — dar geçiş ya da yüksek varyans.
İkisi ayrı ölçülür, yoksa doğru olan genişletilir ve belirti kalır.

## Ayar dosyası düzenlemesi oyuna ulaşmıyor

Bir belirtinin **dört turu** boşa gitti çünkü `.asset` düzenlemelerinin çalışma zamanına
geçtiği doğrulanmadan ölçüm yapıldı. İki ayrı tuzak var:

- **Volume alanları çalışma zamanı KOPYASINDAN okunuyor.** `DebugMenu.cs` bunu zaten
  yazmış: `cloudVolume.profile` asset'in kendisi değil. Ayrıca `CloudWeatherDriver`
  `globalSpeed`, `globalOrientation`, `cloudCoverage` ve `densityMultiplier`'ı **her kare
  yazıyor** — o dördüne asset'ten dokunmak tamamen ölü.
- **`.hlsl` geçer, `.asset` geçmez.** Shader düzenlemeleri kendiliğinden derleniyor;
  asset düzenlemeleri Unity yeniden okumadan (Ctrl+R) görünmüyor.

**Kural:** ayar üzerinden ölçüm yapmadan önce YOL DOĞRULANIR — sürücünün dokunmadığı bir
alan seçilir ve görülmemesi imkânsız bir değer verilir. Yol testi için sürülen bir alanı
seçmek (bu turda `densityMultiplier`) testi baştan geçersiz kılar.

## Yeni dağda eski dağdan birebir aynı yerler

Kullanıcı: *"terrain specinden önceki dağda gördüğüm birebir aynı bir kaç yer var."*

**İlk cevabım yanlıştı** — "tanıdık gelen desendir, şekil değil" dedim. Şekildi.

**Ölçüm.** Araziye yükseklik yazan yalnız üç yer var: `HeightmapImporter` (tamamını ezer),
`RouteTerrainShaper` (menzil 70 m, yalnız yol ve doğuş düzlüğü), `MountainGenerator`
(ondan önce çalışır, üstüne yazılır). Yani taban geometri kesin yeni.

**Gerçek sebep:** `SnowDisplacement` de geometri — köşeler fiilen hareket ediyor — ve
yatay şeklinin TAMAMI `SnowDriftShape(worldPos.xz, …)`'dan geliyor. O fonksiyon dünya
koordinatına bağlı, sabit hash'li. Yükseklik haritası değişti, bu değişmedi: aynı dünya
koordinatında aynı birikinti sırtı, aynı dalga, aynı yığın.

Aynısı yüzey deseninde de: `MountainBand`, oksit, liken, tanecik, kırılma — hepsi
`worldPos` anahtarlı.

**Ölçek:** `snowDisplaceMax` 3.2 m, yani birebir tekrar eden katman 5709 metrelik dağın
**1/1780'i**. Küçük, ama yüzeye yakından bakılınca ekranın çoğunu kaplıyor — şikâyet
yerindeydi.

**Düzeltme:** `_PatternSeed`, İKİ HASH KÖKÜNE birden uygulanıyor (`MountainHash`,
`SnowDriftHash`), tek tek çağrı yerlerine değil. Yeni bir prosedürel katman eklendiğinde
kaydırmayı unutmak mümkün değil.

**Kural:** dağ baştan üretildiğinde `patternSeed` de artırılır. Geometri yenilenip boya
eski kalırsa oyuncu yeri tanır.

## Kod diskte doğru, ekranda eski — sessiz bayatlık ailesi

Aynı sınıftan **dört** tuzak ölçüldü. Belirti hep aynı ve aldatıcı: ölçüm doğru,
düzeltme doğru, ekran değişmiyor. Sonra düzeltme "işe yaramadı" sanılıp geri alınıyor
ve gerçek sebep bir tur daha kaçıyor.

| ne bayatlıyor | neden | çözüm |
|---|---|---|
| Yükseklik haritası PNG'si | `AssetDatabase` önbelleği, dışarıdan yazılan dosyayı fark etmiyor | zorla yeniden içe aktarma |
| `.asset` ayarları | Volume çalışma-zamanı kopyası; dışarıdan düzenleme sessizce geri alınıyor | değerleri KODDAN asset'e yazmak (`MountainSceneBootstrap.EnsureCloudVolume`) |
| Arazi menüsü | düğme yanlış işi yapıyordu, harita hiç uygulanmıyordu | düğmenin tam zinciri koşması |
| **`.hlsl` include'ları** | Unity shader'ın hangi include'u kullandığını **takip etmiyor** | `ShaderIncludeWatcher` — include değişince shader'ları yeniden içe aktarır |
| **Shader hatası log'da yok** | hata İÇE AKTARMADA değil, varyant ilk derlenirken çıkıyor; `Editor.log`'a bakıp "temiz" demek yanıltıyor | Play'e girip materyali kullandır ya da kullanıcıdan konsolu iste |

**Kural:** bir düzeltme ekranda görünmediğinde ilk soru "yanlış mı yaptım" değil,
**"çalışan sürüm gerçekten yeni mi"**. Yol testi önce: görülmemesi imkânsız bir değer
ver, ekranda gör, sonra gerçek ölçüme geç.

---

## Arazide düzenli testere — ÜÇ ayrı sebep, üçü de ölçümle bulundu

Kullanıcı: *"dağdaki bu düzenli testereleri törpüler misin"*, *"gölge değil, arazi
testereli"*, *"bazıları büyük bazıları ufak, aralarında metre farkı var"*, *"88 metreye
ayarladığında bile var"*, ve ayrıca *"mikro çıkıntılar hiç yok, detaylar çok düz"*.

Hepsi doğruydu. Aynı görüntünün arkasında üç ayrı sebep vardı; ikisi kapandı, biri
yöntemin sınırı.

### 1 — Menü düğmesi haritayı hiç uygulamıyordu (bir gün yaktı)

`Araziyi Yeniden Üret` yalnız `gen.Generate()` çağırıyordu: araziyi
`MountainGenerator`'ın kendi prosedürel çıktısıyla — eski radyal koni, terasları ve
gürültüsüyle — dolduruyordu. Yükseklik haritası uygulanmıyordu.

`Logs/tools.log`: harita araziye en son **13:44:53**'te ulaştı. Sonraki dokuz saatte altı
pişirme yapıldı (8.8 m, 22 m, 58 m, 88 m, törpü, koruma maskesi) ve **hiçbiri ekrana
çıkmadı**. Testere de zaten o jeneratörün terasıydı.

**Kullanıcı iki kez doğruyu söyledi, ikisinde de dinlenmedi:**
- *"eski dağ gibi birebir aynı yerleri görüyorum"* — prosedürel üretim aynı tohumla aynı
  sonucu verir, pişen harita her turda değişiyordu. Tek başına kanıttı.
- *"terrain fırçasıyla 10 saniyede düzeltirim"* — fırça **canlı araziye** yazıyor, yani
  o an çalışan tek yol oydu.

Düğme artık tam zinciri koşuyor: PNG zorla okunur → imza sıfırlanır → kurulum baştan üretir.

### 2 — Nyquist alias: asıl testere (14.7 m, her irtifada)

Hücre 7.324 m → ızgaranın taşıyabildiği en kısa dalga 14.65 m. `detail.multifractal`
320 m tabandan **8 oktav** istiyordu: 320, 160, 80, 40, 20, **10, 5, 2.5** m. Son üç
oktav sınırın altında ve geri katlanıyor — katlandıkları yer tam 2 hücre.

```
pismis haritada          80 m     14.7 m
  ZIRVE                  1.72x     3.48x
  ALT 11 km              1.02x     3.41x

sentetik, oktav sayisina gore (cell 7.324 m, taban 320 m)
  oktav 8 (en kisa 2.5 m)   2-hucre 1.57x
  oktav 7 (5.0 m)                   1.49x
  oktav 6 (10.0 m)                  1.09x
  oktav 5 (20.0 m)                  0.23x   <- ucurum burada
```

**Alias yumuşatılamaz** — pürüz değil, ızgaranın taşıyamadığı bir dalganın görünüşü.
Denenen üç filtre (eğim tavanı, Gauss harmanlama, tepe yuvarlama) hep *sonucu* siliyordu.
Sınır `detail.multifractal` içine kondu; çağıranın doğru sayıyı bilmesine güvenilmiyor.

**"Detay yok" şikâyeti aynı mekanizmadan.** Aliası bastırmak için konan `file_crests`
(koşulsuz Gauss, σ = 2 hücre = 14.6 m) tam o dalga boyunda çalışıyordu. Ölçüldü: sınır
açıkken torpu OLMADAN 2 hücre zaten 0.03x; torpu eklenince 25 m bandı 0.58x → **0.03x**.
Telafi terimi silindi (`file_crests`, `despike`, `round_crests`).

Bundan önceki törpü sürümü de kendi testeresini üretiyordu: `minimum_filter` ile çıkarma
yapıyordu, sivri gidiyor ama yerine filtre yarıçapı boyunda **düz faset** kalıyordu
(14.7 m enerjisi 3.42× — taban 1.87×).

### 3 — ~937 m, %200 fazla: YÖNTEMİN KENDİSİ, ÇÖZÜLMEDİ

Divide tree'nin ilkel birimi zirve; aradaki sırt düz çadır, siluet üçgen. Üç kaldıraç da
kapalı:

- **Zirve sayısı serbest değil** — `scalingFactor` gerçek Kirmse yoğunluğundan; 30×30 km'de
  83 zirve = 10.8 km²'de bir, Himalaya için gerçekçi.
- **Sırt kaydırması düzlemsel** (`ridgesPerturbation 0.15`) — yandan kıvrılıyor, kot düz.
- **Gürültü genliği spec sınırında** (§5.7). Taban 320 → 800 m denendi: 937 m fazlası
  3.02 → 2.86. Genliği büyütmek uydurma zirve doğurur.

Gerçek sırtlardaki ara tümsekler prominence tabanının (100 m) altında olduğu için
veritabanında yok. Kapatmanın yolu L2/L3 mesh modülleri.

### Yanlış elenen şüpheliler — hepsi ölçüldü, hepsi masumdu

| şüpheli | ölçüm |
|---|---|
| hücre ölçeğinde teras | 44639 farklı kot, düz komşu %1.11, ikinci fark 0.58 m |
| L0 zirveleri düzenli aralıklı | en yakın komşu değişim katsayısı 0.641 (rastgele 0.523) |
| üçgenleştirme fasetleri | düzlemsel hücre oranı %5-7 |
| kar yer değiştirmesi | kapatıldı, testere kaldı |
| üçgen ağı sıklığı | 45 → 25 m, crease metriği aynı |
| normal dokusu çözünürlüğü | 2048 → 4096 → 2048 |
| arazi LOD'u / `heightmapPixelError` | `maxLOD` 1, 88 m — değişmedi |
| gölge / aydınlatma | kullanıcı ölçtü: arazi testereli, gölge onu takip ediyor |

### Kurallar

- **"Düzenli tekrar" şikâyetinde ölçek sorulur** — aynı belirti farklı ölçeklerde farklı
  sorumludan gelir. Araç 2B güç spektrumu: fraktal taban düz bir doğru, sapan dalga boyu
  sorumluyu adıyla söyler.
- **Spektrumda taban doğrusu kesim bölgesini DIŞARIDA bırakan banda uydurulur.** İlk
  ölçüm tüm banda uydurdu; sonlu oktavlı gürültünün roll-off'u uydurmayı aşağı çekti ve
  olmayan bir tepe gösterdi ("zirvede 73-90 m'de 2.3x"). 30-600 m'ye sınırlanınca 1.72x'e
  düştü, alçakta 1.02x — yani yoktu.
- **Değişikliğin ekrana ulaştığı doğrulanmadan ölçüm yapılmaz.** Bu oturumda üç katmanda
  aynı tuzak: `.asset` (Volume çalışma zamanı kopyası), PNG (`AssetDatabase` önbelleği),
  menü (yanlış işi yapıyor). Yol testi önce: görülmemesi imkânsız bir değer verilir,
  ekranda görülür, sonra gerçek ölçüme geçilir.


## Teşhis aracının kendisi

Bu oturumda araç **iki kez yalan söyledi** ve ikisi de tur kaybettirdi.

- **`renk × 8` yetmedi.** "Tam sıfır" ile "çok küçük"ü ayıramadı; gökyüzü parlakken ortam
  ışığıyla aydınlanan zemin sekiz katıyla da ekranda siyah kalıyor. Ölçek logaritmik
  olmalı ve sıfır ayrı bir durum olarak işaretlenmeli.
- **Tek renkli denetim "kim uyguladı" diyemez.** Macenta "sis uygulandı mı" sorusunu
  cevaplıyor ama arazi de gök de macentaya döndüğü için sorumluyu göstermiyor. "Kim"
  sorusu için her yazıcı **kendi rengini** basmalı.

**Kural:** aracın çözünürlüğü, ayırmak istediğin iki hipotezi ayırabiliyor mu — önce bu
sorulur. Ayıramıyorsa araç yalan söyler ve tur katlar.

Üçüncü kez, arazi tarafında: **"ortalama eğim" tırmanılabilirliği ölçmez.** Oyun alanının
4–6 km bandı 48.7° ortanca verdi ve "duvar" diye okundu. Ama gerçek dağda da yüzler
50°+'dır; rota yüzden değil **sırttan** gider. Doğru araç en-az-maliyetli hat aramasıydı
(Dijkstra, maliyet `mesafe × (1 + (eğim/25)⁴)`): etekten zirveye **17.56 km** yol,
ortanca **18.1°**, teknik tırmanış **%0**. Duvar yoktu.

**Kural:** bir soruyu alan ortalamasıyla cevaplamadan önce sor — cevap bir **yol** mu,
bir **alan** mı? Yol soruluyorsa yol aranır.

Ve aynı turda dördüncü kez: ölçüm **koridorun içinde** yapılmalıydı, "zirveden >10 km"
diye yapıldı ve bütün yönleri kapsadı. Ova 20.8° çıktı; koridorla sınırlanınca 6.3°.
Yanlış maske, yanlış sayı.


Beşinci ve altıncı, aynı oturumda, ikisi de **ölçüm turu yaktı**:

- **İki prob arasında öncelik hatası.** Prob 1'in koşulu `> 0.5`, prob 2'ninki `> 1.5`
  ve prob 1 kodda önce geliyordu. Değer 2 olunca prob 1 de doğru çıkıp önce dönüyor,
  prob 2'ye hiç sıra gelmiyordu. Kullanıcı doğru kutuyu işaretledi, ekrana yanlış prob
  çıktı — ve ben iki tur "yanlış kutudasın" dedim. **Kural:** mod seçen bir anahtar
  aralıkla test edilir, eşikle değil.
- **Prob 2'nin bantları deniz seviyesine göre yazılmıştı.** "Kar için güneş-gölge farkı
  2–3.5 diyafram" doğru bir sayı, ama 4900 m'de değil; orada gerçek değer ~3.8. Alet
  doğru araziye "fazla koyu" dedi. **Kural:** fiziksel referans alınırken hangi koşulda
  ölçüldüğü de yazılır — irtifa, hava, yüzey.


Yedinci, kar sınırında: **gradyan KARŞILAŞTIRAN prob hiçbir şey söylemez.** Dört girdinin
ekran üstü değişim hızını karşılaştırıp en hızlısını basan bir prob yazıldı; sınırda
"kot rampası" çıktı ve bu trivial olarak doğruydu — geçişte zaten değişen büyüklük her
zaman kazanır. Cevap mutlak bantlardan geldi: `cover` eşit aralıklı renklere bölününce
ortalamanın yumuşak, varyansın yüksek olduğu tek bakışta görüldü.

**Kural:** "hangisi" sorusu karşılaştırmayla, "ne kadar" sorusu mutlak ölçekle sorulur.
Sertlik bir *ne kadar* sorusudur.

Prob sonuç vermiyorsa sıradaki araç **Unity Frame Debugger**: hangi geçişin ekrana ne
yazdığını kesin gösterir. Yalnız kamera renk tamponuna yazan adımlara bakılır; motion
vector, gölge haritası ve ara doku adımları tuhaf görünür, normaldir.


## "Ekranın tamamı grenli, gökyüzü dahil" — desenin DC bileşeni

Kullanıcı yağış perdesini yalnız başına görmek için tanecikleri kapattı ve şunu dedi:
*"bu görüntü normal mi? f1'den yağmur ve karı kapattım net olarak görebilmek için."*
Ekranın her yeri — gökyüzü dahil — ince, düzgün bir grenle kaplıydı.

**Yanlış çıkan ilk şüpheli: mesafe.** "Uzaktaki tane piksel altı kalır, perde mesafeyle
sönmeli" diye üstel bir mesafe kapısı kondu (`textureRange = 260 m`). Sonuç: perde ufka
yapışık İNCE BİR ŞERİDE düştü. Geometrik olarak doğruydu — düz ovada 50–600 m aralığı
ekranda dar bir kuşağa sıkışır, perspektif mesafeyi ezer — ama işe yaramazdı, ve asıl
sorunu hiç ele almıyordu.

**Gerçek sebep: desenin ortalaması.** Pişirici deseni ortalaması 0.5 olacak şekilde
`[0,1]`'e eşliyor (`[Langer 2004, §7.7]`). O ortalama DOĞRUDAN opaklık olarak
kullanılıyordu, yani perde ekranın tamamına sabit bir gri sürüyordu — tam karda ~0.45.
Kullanıcının "gren" dediği şey tanelerin dokusu değil, desenin **DC bileşeniydi**.

Tane seyrek ve ayrıktır, aradaki hava saydamdır. Ortalamanın altı sıfıra iniyor, üstü
geriliyor: `alpha = saturate((a - 0.5) / 0.5)`. Sihirli katsayı yok — 0.5 pişiricinin
yazdığı ortalama.

**Ayırt eden ölçüm: opaklık probu** (son alfayı ayrık renk bantlarına basan görünüm).
Önce ekranın tamamı 0.32–0.50 bandındaydı; sonra 0.00–0.02 tabanı üstünde seyrek
0.08–0.32 tepeleri. Göz kararıyla "biraz azalttım" ile "doğru yere oturdu" ayrılamazdı.

**Aynı turda ikinci belirti: benekler kirli koyu okunuyordu.** 235 m görüşte gök tamamen
sis rengindeyken tanecikler gökten belirgin koyu düşüyordu. Sebep: perde `AirColor`'a
bağlanmıştı — o bakış yönüne bağlı ve gök gradyanını taşıyor. Doğrusu `SpindriftColor`.

Bunun şüpheli araması hiç gerekmedi: kural **zaten yazılıydı**, `HeightFog.hlsl:248` ve
`:611`, ikisi de "havada asılı tane gök rengine boyanmaz" diyor. Yeni bir görsel mevcut
bir büyüklüğe bağlanırken o büyüklüğün fiziksel karşılığı okunmadı.

