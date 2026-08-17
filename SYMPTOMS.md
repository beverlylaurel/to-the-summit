# Belirti → Sebep

Ölçülerek bulunmuş belirtiler. Amaç tek: **aynı belirti tekrar görüldüğünde aramaya
baştan başlamamak.**

Her kayıt üç şey taşır: belirtinin kullanıcının ağzından hâli, ilk şüphelinin ne olduğu
(ve neden yanlış olduğu), gerçek sebep. Kayıt ancak **ölçümle** kapanmış bir turdan
doğar — tahminle çözülen bir şey buraya yazılmaz.

Bu dosyanın kendi dersi: **belirtinin göründüğü yer, belirtinin doğduğu yer değildir.**
Aşağıdaki dokuz kaydın yedisinde ilk şüpheli, belirtinin en çok göze çarptığı katmandı ve
yedisinde de yanlış çıktı. Son kayıtta üç ayrı şüpheli sırayla elendi.

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

## Bulutların çevresinde halka / bulut kenarında kontur

**İlk şüpheli:** bulut ışın yürüyüşü. *(Yanlış — kapsama 0'da kontur da yoktu.)*

**Sebep:** bulut **kenarı** pikselinde derinlik gerçek değil; paket oraya uzak düzlemin
bir tık berisini koyuyor (`CLOUDS_RAW_FAR_CLIP_VALUE`). O pikseli **elemek** çözüm değil:
elenince her bulutun çevresinde bir piksellik sissiz halka kalıyor ve arkasındaki her şey
kontur kazanıyor.

**Kural:** bilinmeyen mesafeye sayı uydurma, pikseli de eleme — **mesafeyi sınırla**.
Sahnede hiçbir şey uzak düzlemden öteye gidemez.

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

Prob sonuç vermiyorsa sıradaki araç **Unity Frame Debugger**: hangi geçişin ekrana ne
yazdığını kesin gösterir. Yalnız kamera renk tamponuna yazan adımlara bakılır; motion
vector, gölge haritası ve ara doku adımları tuhaf görünür, normaldir.
