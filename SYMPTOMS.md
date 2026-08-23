# Belirti → Sebep

Ölçülerek bulunmuş belirtiler. Amaç tek: **aynı belirti tekrar görüldüğünde aramaya
baştan başlamamak.**

Her kayıt üç şey taşır: belirtinin kullanıcının ağzından hâli, ilk şüphelinin ne olduğu
(ve neden yanlış olduğu), gerçek sebep. Kayıt ancak **ölçümle** kapanmış bir turdan
doğar — tahminle çözülen bir şey buraya yazılmaz.

Bu dosyanın kendi dersi: **belirtinin göründüğü yer, belirtinin doğduğu yer değildir.**
İlk şüphelisi kayda geçmiş on iki belirtinin **on ikisinde de** ilk şüpheli yanlış çıktı —
her seferinde belirtinin en çok göze çarptığı katman seçilmişti. Bulut kontüründe üç ayrı
şüpheli (ışın yürüyüşü, zamansal birikim, mesafe sınırı) sırayla elendi; sebep dördüncüdeydi.

---

## Oyuncunun çevresinde büyük bir kare, onunla birlikte hareket ediyor

**Elenen şüpheliler — hepsi ölçümle:**

| Şüpheli | Nasıl elendi |
|---|---|
| Clipmap halkaları | Halkalar tamamen silindi (spec §8.1), kare kaldı |
| Zemin dokusu çözünürlüğü (7,32 m teksel) | Kar ile arazi arasındaki ayrılık ölçüldü: ortalama 1 cm, en fazla 6 cm. Mesh havada durmuyor |
| Dağın kar maskesi kapalı | "Zorla karla kapla" açıldı, hiçbir şey değişmedi |

**Araç yalanı:** bir tur `SnowSurface.enabled = false` ile "mesh kapalı, kare
duruyor" sonucuna varıldı. Mesh KAPANMAMIŞTI — `SnowSurfaceMesh` nesnesi
`activeInHierarchy: true` bulundu. O turun iki sonucu da geçersizdi.

**Sebep:** kar mesh'i ile arazi AYNI YERİ FARKLI KALINLIKTA boyuyordu.

- Mesh kenarında kalınlığı sıfıra indiriyor (spec §8.3, `h *= fade`)
- Arazi tam orada `SnowStateAt`'ten okuduğu 45 cm'i boyuyor

Aralarında 2 metre genişliğinde bir **hendek** kalıyor; kare o hendeğin
çerçevesi. Spec §8.3 doğru, ama varsayımı "mesh'in ötesinde kar yoktur" —
bizde arazinin kendi kar katmanı vardı.

**Ayırt eden ölçüm:** kalınlık probu. İki shader da kalınlığı aynı ölçekte gri
döndürüyor, aydınlatma çalışmıyor. Üç seviye tek bakışta göründü:

```
dışarısı        orta gri        ~45 cm
karenin içi     beyaza yakın    ~55-60 cm
kenar halkası   KAPKARA         ~0 cm      <- hendek
```

**Düzeltme:** arazinin karı derinlik değil ÖRTÜ (spec §16) — global skaler
`_SnowCoverage`, kalınlık `_SnowCoverThickness` (4 cm). `MountainSurface` artık
`SnowStateAt` okumuyor. Sınırdaki fark 45 cm yerine 4 cm.

**Kural:** iki katman aynı yeri boyuyorsa ikisi de AYNI büyüklüğü göstermek
zorunda. Biri derinlik biri örtü okuyorsa sınır her zaman görünür — ve sınır
oyuncuyu takip ediyorsa kare olur.

**Araç notu:** teşhis aracını kullanmadan önce aracın kendisi doğrulandı — kar
her yerde 45 cm iken prob orta gri gösterdi (45/60 = 0,75, beklenen). Bir önceki
turda bu yapılmadığı için yanlış sonuca varıldı.

---

## Ayak altında kare bir sırt/çıkıntı, oyuncuyla birlikte geliyor

**Elenen şüpheliler — hepsi izolasyon anahtarıyla, tek turda:**

| Şüpheli | Nasıl elendi |
|---|---|
| Kenar sönümü | Prob mod 4: o bölgede sönüm düz (=1) |
| Halka sırası | Prob mod 1: kırmızı→yeşil→mavi→sarı düzgün iç içe |
| Dikiş | F1 anahtarı kapatıldı, çıkıntı kaldı |
| Etek | F1 anahtarı kapatıldı, çıkıntı kaldı |

**Sebep:** kar mesh'i ±8 m'de yakın bölgeden uzak kaskada devrediyor ve devirde
**ham SWE ile yoğunluk AYRI AYRI** harmanlanıyordu. Derinlik `SWE × 1000 / ρ`,
yani doğrusal değil; iki büyüklüğü ayrı harmanlamak aradaki derinlik profilini
sıçratıyor.

**Ölçüm:** `derinlik yakın 52,5 cm (ρ 95) · kaskad 45,4 cm (ρ 110) · FARK 7,0 cm`.
7 cm, 45 cm'lik bir tabakada gözle görülür bir sırt — üstelik kare, çünkü
`SnowInsideMask` kare bir bölgenin kenarında sönüyor.

Yoğunluk farkı tasarımdan: kaskad bilinçli sadeleştirilmiş (spec Faz 10) ve
ıslaklık kanalı yok.

**Düzeltme:** derinliğin KENDİSİ harmanlanıyor. İki uç ne olursa olsun arada
tek yönlü düz bir rampa kalıyor.

**Kural:** iki bölgeyi birleştirirken **görünen büyüklüğü** harmanla, onu üreten
ham girdileri değil. Girdiler doğrusal olmayan bir bağıntıdan geçiyorsa ayrı
harmanlama her zaman basamak üretir.

**Araç notu:** prob görünümü (her şüpheli ayrı renk, ışıktan bağımsız) dört
şüpheliyi tek turda eledi. Ondan önce aynı belirtiye dokuz tur harcandı.

**Mekanizma artık yok** (2026-08-22): spec v2 §8.1 çok seviyeli clipmap'i
yasakladı, uzak kaskad §8.4 gereği silindi. Devir noktası diye bir yer kalmadı.
Yukarıdaki KURAL geçerliliğini koruyor — bu belge dersleri tutuyor, kodu değil.

---

## Kar yüzeyinde uzun, düz, dik bir sırt (çıkıntı)

**İlk şüpheli:** kenar sönümü. *(Yanlış — sönüm yarıçapı ve bandı üç kez
değişti, sırt aynı kaldı.)*

**Sebep:** eteğin iç dikişe konması. Etek 2 m AŞAĞI iniyor. Yamaçta dikişin
aşağı tarafındaki yüzey eteğin tepesinden alçakta kalıyor, etek açıkta kalıyor
ve dik bir duvar olarak görünüyor. Etek yalnız EN DIŞ halkada olmalı — orada
ötesinde yüzey yok, arazinin içine giriyor.

**Aynı turda çıkan iki kusur daha:**

1. **T-kavşağı.** İnce halkanın kenarında köşe aralığı kaba halkanınkinin
   üçte biri; aradaki iki ince köşe kaba kenarın düz çizgisinden sapıyor ve
   dikiş boyunca yarık açıyor. Sınır köşeleri artık yüksekliği KABA ızgaradan
   okuyor (bilinear dikiş).
2. **Derinlik payı basamağı.** Pay, halkalar bindirdiğinde hangisinin
   kazanacağını belirlemek içindi. Ortak snap'ten sonra bindirme sıfır;
   halkalar sınır çizgisini PAYLAŞIYOR ve pay tam o çizgide pay kadar basamak
   üretiyor. 1 mm → 2 cm → **0**.

**Kural:** bir düzeltme başka bir kusurun varlığına dayanıyorsa, o kusur
kapandığında düzeltme de kusura döner. Derinlik payı ve iç etek ikisi de
bindirmenin/boşluğun telafisiydi; bindirme sıfırlanınca ikisi de zarara geçti.

**Mekanizma artık yok** (2026-08-22): halka, delik, etek ve dikiş silindi —
mesh tek kare ızgara. Üç kusurun üçü de çok seviyeli clipmap'in kendi
karmaşıklığından doğmuştu; spec §8.1 bu yüzden yasaklıyor.

---

## Kar yüzeyi bloklar/basamaklar hâlinde; mesh kapatılınca zemin pürüzsüz

**İlk şüpheli:** clipmap halkalarının dikişi. *(Yanlış — halka sürgüsüyle
ölçüldü, basamaklar her halkada var.)*

**İkinci şüpheli:** kenar sönümü ve `clip` gürültüsü. *(Yanlış — sönüm
yarıçapı düzeltildi, basamaklar kaldı.)*

**Sebep:** zemin yüksekliği `TextureFormat.RHalf` dokuya **0–8000 m** aralığında
normalize edilip yazılıyordu. Half'ın bağıl adımı 2^-11 sabit; metre karşılığı
kotla birlikte büyüyor:

| Kot | Adım |
|---|---|
| 2000 m | **195 cm** |
| 4000 m | **391 cm** |
| 8000 m | **781 cm** |

Kar kalınlığı 26–45 cm. Zemin iki metrelik basamaklara oturunca kar yüzeyi
başka türlü çizilemezdi. Oyuncunun 206 m kotunda adım 12 cm — kalınlığın
yarısı, bu yüzden bloklar orada da görünüyordu.

**Ayırt eden ölçüm:** kar mesh'ini kapatmak. Dağın kendi kar katmanı (yer
değiştirme YOK, yalnız gölgeleme) pürüzsüz çıkıyordu. Yer değiştiren tek yüzey
bozuktu → hata yükseklik kaynağında.

Sonra half round-trip adımı kot kot hesaplandı ve sayı tabloya döküldü.

**Kural:** dokuya normalize edilmiş bir büyüklük yazarken, aralığın ÜST ucundaki
adımı metre cinsinden hesapla. Half 0..1 aralığında ucuz görünür; 8000 m ile
çarpıldığında metre olur. Spec §7.1 `RHalf` diyor ama küçük arazi varsayıyor —
bilinçli sapma, gerekçesi `DECISIONS.md`.

---

## Kar açılınca çevrede basamaklı, dikey çizgili devasa bir duvar

**İlk şüpheli:** mesh'in araziden ayrı durması. *(Yanlış — 128 m'de ortalama
ayrılık 1 cm.)*

**İkinci şüpheli:** dış halkanın kenarındaki kalınlık basamağı. *(Yanlış — halka
sürgüsüyle ölçüldü: duvar 1'de de 4'te de aynı yerde.)*

**Üçüncü şüpheli:** gölgeleme. *(Kısmen — yer değiştirme kapatılınca kalınlık
gitti, çizgiler kaldı. Ama sebep bu değildi.)*

**Sebep: İKİ SAYI AYRIŞMIŞTI.** `SnowMeshBuilder` halkaları
`Ring0Extent × RingScale^i` ile büyütüyor: ±4, ±12, ±36, **±108 m**. Kenar
sönümüne yayınlanan yarıçap ise `AreaSize × 2^(R−1) × 0.5` = **64 m**
hesaplıyordu. Sönüm 64 m'de kalınlığı sıfırlayınca `clip(h − 0.004)` kar
yüzeyini **ortasından** testere gibi kesiyordu. Basamaklar dörtgen köşegenleri,
dikey çizgiler kesik kenarın normalleri.

`AreaSize` kar DURUMU bölgesi (16 m); halka ölçüsüyle ilgisi yok. İkisini aynı
sanmak iki gün yaktı.

**Ayırt eden ölçüm:** `SnowMeshBuilder.Describe`'ın verdiği gerçek halka
ölçüleri ile shader'a yayınlanan yarıçapı yan yana bastırmak. Fark tek satırda
göründü: `±0.0 m` / `±108.0 m`.

**Kural:** bir shader'a "sınır" gönderiyorsan, o sınırı üreten geometriden
TÜRET. İkinci bir formülle yeniden hesaplama — iki formül er geç ayrışır ve
ayrıştığında hiçbir yerde hata vermez. Sınama artık ikisini karşılaştırıyor.

---

## Kar yağıyor ama dağ çıplak; ayak altında beyaz kare seninle geliyor

**İlk şüpheli:** kar mesh'inin araziden ayrı durması. *(Yanlış — 128 m'de 1089
nokta ölçüldü, ortalama ayrılık 1 cm.)*

**İkinci şüpheli:** pişen zemin dokusunun ters indeksli olması. *(Yanlış — 0.0 m
fark; ters indeks olsaydı 1017 m verecekti.)*

**Üçüncü şüpheli:** `Shader.SetGlobalFloat`'ın compute'a ulaşmaması.
*(Yanlış — ölçüldü: 12.345 yazıldı, compute'ta 12.345 okundu.)*

**Sebep:** kar çizgisi yalnız İLK TEMİZLİKTE ve bölgeye YENİ giren şeritte
uygulanıyordu. Oyun +8 °C'de açılıyor, donma seviyesi 1451 m, oyuncu 205 m'de —
çizginin altında, bölge doğru olarak SWE 0 ile doluyor. Sonra sıcaklık düşüp
çizgi −557 m'ye inince **mevcut tekseller güncellenmiyor**. Birikme 1.39e-6 m/s,
görünür kalınlığa saatler sürüyor.

Beyaz kare de kar mesh'inin toplam kapsaması: 16 m × 2³ = **128 m**.

**Ayırt eden ölçüm:** F1'e iki ayrı sayı kondu — kar çizgisinden hesaplanan
kalınlık (45.5 cm, DOĞRU) ve durum dokusunun geri okuması (`DOKUDA 0.00`,
BOŞ). İkisi aynı satırda görününce sebep tek bakışta ayrıldı: çizgi doğru,
doku boş.

**Kural:** bir alanı "başlangıçta doldur" diye kurduysan, o alanı süren
değişken SONRADAN değişebiliyorsa doldurma da tekrarlanmalı. Yoksa belirti
"özellik hiç çalışmıyor" gibi görünür, oysa yalnız bir kez çalışmıştır.

---

## Havada solucan/sigara dumanı gibi kar; bir yerde yağıyor, bir yerde boş gökyüzü

**İlk şüpheli:** tane sayısı az. *(Yanlış — sayı dört katına çıkarıldı, desen aynen kaldı,
sadece daha kalabalık oldu.)*

**Sebep:** doğum hash'i çöküyordu. `frac(sin(dot(p, k)) * 43758.5)` float32'de büyük
girdide tekrar eden değer üretiyor.

**Ayırt eden ölçüm:** GPU aritmetiği float32 olarak Python'da taklit edildi, üretilen
X koordinatlarının TEKİL SAYISI sayıldı:

| tane | tekil X değeri | oran |
|---|---|---|
| 13 000 | 1 887 | %14.5 |
| 104 000 | 5 237 | **%5.0** |

Yüz bin tane beş bin dikey hat üzerine yığılıyor. "Solucan" o hatların kendisi,
"sigara dumanı" hattın rüzgârla eğilmiş hâli, "bir yerde boş gökyüzü" de hatların
arasındaki boşluk.

PCG3D ile aynı ölçüm: 104 000/104 000 tekil, kova sapması ×1.04, eksenler arası
korelasyon 0.0003.

**Kural:** indis veya dünya koordinatı büyüyebilen hiçbir yerde `frac(sin(...))` hash
kullanılmaz. Tam sayı hash (PCG3D) girdinin büyüklüğünden etkilenmiyor. Parıltı
hash'i de aynı sınıftaydı — hücre `floor(posWS.xz / cellSize)`, 6000 m'lik dağda
milyonlara çıkıyor.

---

## On ayrı sınama birden "yanlış sonuç" diyor, hepsi sıfır

**İlk şüpheli:** sınamaların ölçtüğü on ayrı davranış. *(Yanlış — hepsi doğruydu.)*

**Sebep:** tek satırlık eksik include. `SnowSparkle.hlsl` `SnowCommon.hlsl`'i include
etmiyordu; `SnowTestKernels.compute`'un ALTI kerneli birden derlenmedi ve dispatch'ler
sessizce sıfır döndü.

**Ayırt eden ölçüm:** sınama koşucusuna `ShaderUtil.GetComputeShaderMessages` eklendi
ve mesajlar raporun BAŞINA yazıldı. Sebep tek satırda göründü.

**Kural:** compute sonucu topluca sıfırsa önce derleme mesajına bakılır, davranışa
değil. Derlenmeyen bir kernel ile yanlış hesaplayan bir kernel dışarıdan aynı görünür.

---

## Ekranın tamamı bembeyaz, ayak altında hareket eden siyahlık, 10 FPS

**İlk şüpheli:** tane sayısı ve tane parlaklığı. *(Yanlış — ikisi de düzeltildi,
belirti aynen kaldı.)*

**İkinci şüpheli:** çizimin başka bir kameraya sızması. *(Yanlış — kar sisteminde
gerçek `Camera` yok, yakalama da gökyüzü de elle çiziliyor.)*

**Sebep:** asgari ekran boyu ifadesinde işaret hatası. Yazdığım hâl:

```hlsl
float tanHalfFov = 1.0 / max(UNITY_MATRIX_P._m11, 1e-4);
```

D3D render hedefine çizerken projeksiyonun `[1][1]` öğesi **negatife düşüyor**
(y ekseni ters çevriliyor). `max(-1.732, 1e-4)` → `1e-4`, ölçek 10 000 katına
çıkıyor: 23 m uzaktaki 2 cm'lik tane **680 m**'lik bir dörtgen oluyor. On üç bin
tanenin on üçü bile ekranı kapatıyor; beyazlık tanenin rengi değil, üst üste
binen dev dörtgenler. Ayak altındaki "siyahlık" da aynı dörtgenlerin arkadan
aydınlanmış olanları.

**Ayırt eden ölçüm:** iki aşamalı.
1. Tampon CPU'ya okundu: boy 0.011–0.031 m, alpha 0.007–1.0, mesafe 1.3–23 m —
   **hepsi doğru**, hesaplanan kaplama %1.3. Yani hata tamponda değil, shader'da.
2. `_MinPixelSize` çalışma anında 0'a çekildi → beyazlık gitti, taneler ilk kez
   göründü. Şişen terim buydu.

**Aracın kendi yalanı:** ölçüm aracı piksel boyunu **C#'ta** `cam.fieldOfView`
ile hesaplıyordu ve doğru sayıyı veriyordu; shader başka bir ifade kullanıyordu.
Araç "beklenen" sütununu doğru bastığı için ilk turda "demek ki taneler değil"
sonucu çıktı. **Bir ifadeyi ölçmek istiyorsan ifadenin kendisini ölç, niyetini
değil.**

**Kural:** aynı işi yapan çalışan bir kod varsa ifade oradan kopyalanır.
`Precipitation.shader` → `PixelsPerRadian()` bu tuzağı yıllar önce yemiş ve
yanına `abs` şart diye not düşmüş. Kar tarafında sıfırdan yazıldığı için not da
kaybolmuştu. Sınama artık kaynağı denetliyor: `abs` olmadan `_m11` geçemiyor.

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

koordinatına bağlı, sabit hash'li. Yükseklik haritası değişti, bu değişmedi: aynı dünya
koordinatında aynı birikinti sırtı, aynı dalga, aynı yığın.

Aynısı yüzey deseninde de: `MountainBand`, oksit, liken, tanecik, kırılma — hepsi
`worldPos` anahtarlı.

**1/1780'i**. Küçük, ama yüzeye yakından bakılınca ekranın çoğunu kaplıyor — şikâyet
yerindeydi.

**Düzeltme:** `_PatternSeed`, İKİ HASH KÖKÜNE birden uygulanıyor (`MountainHash`,
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

Bunun şüpheli araması hiç gerekmedi: kural **zaten yazılıydı**, `HeightFog.hlsl:248` ve
`:611`, ikisi de "havada asılı tane gök rengine boyanmaz" diyor. Yeni bir görsel mevcut
bir büyüklüğe bağlanırken o büyüklüğün fiziksel karşılığı okunmadı.

## "Damlalar yere çarpıp sekiyor sanki dolu yağıyor gibi" / "yatayda hareket eden damlalar var"

**İlk şüpheliler — üçü de yanlış çıktı.** Yere yakın, küçük ve hareketli üç şey vardı ve
(tanecikleri tam zeminde, yatay akıyor), yağan kar (çırpınıyor). Hepsi tek seferde F1
anahtarı olarak kondu; **üçü de kapatıldığında sekme sürdü.**

**Gerçek sebep iki katmanlıydı.**

Birincisi iz boyu: `rainLength` ve saydamlık `TerminalVelocity(r)` okuyordu, yani rüzgârı
hiç görmüyordu. İkisi de pozlama boyunca SÜPÜRÜLEN YOLDAN çıkar. Rüzgâr 8.5 m/s'de ince
damla 3.4 cm çiziliyordu, gerçek yol 14.6 cm — 4.3 kat kısa. Kısa + yatık = çizgi değil
tanecik, yani "dolu".

İkincisi rüzgârın kendisi: şiddet → hız eşlemesi doğrusaldı ve 0.57 şiddet doğrudan
8.5 m/s (Beaufort 5) veriyordu. O rüzgârda 1 mm damla yataydan 25° iner — fizik doğru,
rüzgâr fazla. Eşleme kareye alındı (`WindField.ShapeSeverity`), uçlar sabit kaldı.

**Ayırt eden ölçüm: ELEME, renk değil.** Yörünge açısı önce yedi renk bandına basıldı ve
kullanıcı ayıramadı ("gözüm seçmiyor, hepsi birbirine benziyor"). Renk bırakılıp eleme
kondu — ekranda yalnız 20°'den yatık, sonra yalnız 20°'den dik damlalar çizildi. Cevap tek
turda geldi. Ardından CPU tarafında hız vektörünün iki bileşeni (rüzgâr sürüklenmesi /
düşme) ayrı ayrı kapatıldı: **rüzgâr kapatılınca yatay hareket bitti.**

**Aracın kendisi iki kez yalan söyleyecekti.** (1) İzolasyon anahtarı `UpdateStreaks`
içinde bağlanıyordu, o metodun önünde dört erken çıkış var — biri tutsa uniform hiç
yazılmayacak, HLSL varsayılanı (0,0,0), yani "hepsi kapalı" görünecekti. (2) `debugScale`
BÜTÜN prob kiplerinde 40× büyütüyordu; "tür" probu 40 kat büyütülmüş şeritler gösterdi ve
ölçtüğü geometriyi bozdu. Teşhis aracı önce doğrulanır.


## "kar yağmıyor" — VFX bağlıydı, parçacık doğuyordu, ekranda hiçbir şey yoktu

**İlk şüpheli yanlış çıktı: sıcaklık.** Oyuncu 206 m'de, ölçülen +6 °C, donma
seviyesi deniz seviyesinin altında — "sıcak olduğu için yağmur yağıyor, kar
değil" diye okundu. Sıcaklık kapısı kaldırıldıktan **sonra da** kar yağmadı.

**İkinci şüpheli de yanlış: Play donuktu.** `runInBackground: 0` yüzünden Unity
odaksızken `Update` koşmuyordu; ölçülen bütün sıfırlar bayattı. Açıldı, tick
geldi, zincir uçtan uca doğrulandı — `Precipitation 1 → IsSnowing → NearRate
16000 → SpawnRate 16000 → alive 39892`. **Kar hâlâ görünmüyordu.**

**Gerçek sebep sınır kutusuydu.** `VFXBasicInitialize.bounds` varsayılanı
1 m³. Unity o kutuyu frustum'a göre kırpıyor: `VFXRenderer.isVisible` false,
sistem hiç çizilmiyor. 39892 parçacık vardı ve hiçbiri ekrana gelmiyordu.
`SnowVfxBuilder` beş grafiğin hiçbirine bounds yazmamıştı.

**Ayırt eden ölçüm:** `Renderer.bounds.size` = (1, 1, 1) ile `culled: true`
yan yana. Zincirin her adımı doğru sayı veriyordu; yalnız son adım — çizim —
sessizce atlanıyordu. Zinciri sonuna kadar okumak, "veri akıyor demek ki
çalışıyor" varsayımını kırdı.

**Yan bulgu: `Unity_RunCommand` Play'i düşürüyor.** İlk çağrı C# derliyor,
derleme domain reload tetikliyor, Play çıkıyor. Play modunda ölçüm alırken
ölçüm aracının ölçtüğü şeyi öldürdüğü fark edilene kadar iki tur yandı.


## "kar yukarı doğru yağıyor" / "yukarı aşağı sağa sola hareket ediyor"

**Sebep türbülansın kip'iydi, şiddeti değil.** `Block.Turbulence` `Relative`
modda hızı bir HEDEFE ÇEKİYOR (`velocity += (hedef − velocity) * drag * dt`);
hedef türbülans alanı, ortalaması sıfır. Başlangıçta yazılan −0.6…−1.4 m/s
terminal hız bir saniyede yeniyor, geriye rastgele savrulma kalıyor.

**Çözüm hızı dayatmak değil, fizikten çıkarmak oldu.** Yerçekimi (−9.81) +
sürükleme (9.81) dengesi terminal hızı kendisi veriyor: `v = g/drag = 1 m/s`,
spec §17.1'in kuru kar için istediği 0.6–1.4 bandının ortası. Türbülans
`Absolute` moda alındı — kuvvet ekliyor, hızı ezmiyor. Yüksek sürükleme aynı
zamanda karın NEDEN savrulduğunu açıklıyor: hafif tane rüzgâr hızına hızla
yaklaşır. İki davranış tek katsayıdan.

**Ayırt eden ölçüm — üçüncü araçta bulundu.** İlk iki araç yalan söyledi:

1. Ekran görüntüsündeki parlak nokta sayımı. Tespit eşiği gökyüzü/dağ
   kontrastına bağlı, tanelerin ancak %13'ünü buluyordu; üstelik spawn
   kesilince taneler aynı anda ölüyor (39970 → 5723) ve dağılımı kaydırıyordu.
   Ortalama y "yukarı" çıktı — gerçeğin tersi.
2. `cam.Render()` ile manuel yakalama. URP'nin gökyüzü ve post adımlarını
   atlıyor, sahne kapkara geliyor. Parlaklık yargısı için kullanılamaz.

Çalışan araç: `boundsMode = Automatic`. Unity gerçek parçacık kutusunu her
frame hesaplayıp `Renderer.bounds`'a yazıyor — ışıktan, ölümden ve gözden
bağımsız bir sayı. Ölçüm:

    Spawn kutusu        205,0 … 231,0 m   (merkez 218, yükseklik 26)
    Gerçek parçacıklar  197,1 … 230,9 m   (merkez 214, yükseklik 33,8)

Taneler kutunun **7,9 m altına** inmiş, üst sınır spawn sınırında kalmış.
Hareket tek yönlü. Terminal 1 m/s × max ömür 9 s = 9 m; ölçülen 7,9 m tutarlı.
Prob ölçümden sonra kaldırıldı.


## "uzakta siyah tanecikler hareket ediyor"

**Kar taneleri siyah çiziliyordu.** `Orient: Face Camera Plane` normali kameraya
çeviriyor; güneş yandan gelince `N·L ≈ 0` ve Lit quad kararıyor. Gerçek kar
tanesi çok saçıcı — ışığı her yöne dağıtır, tek yönlü diffuse ile
modellenemez. Spec §17.1 bunu emissive ile karşılıyor; builder emissive'i hiç
yazmamıştı.

**Aynı ekranda ikinci sapma: "yakınımda kar yok".** `ScreenSpaceSize` bloğu
`PixelAbsolute` modda boyutu SABİTLİYOR — yakındaki tane de uzaktaki de tam
1.3 piksel. Spec `size = max(size, minWorld)` istiyor, yani 1.3 piksel TABAN.
Bloğun hiçbir modu bunu vermiyor (`PixelAbsolute`,
`PixelRelativeToResolution`, `RatioRelativeTo*` — paket kaynağından okundu).
Blok çıkarıldı; asgari boyut ayrı iş, `DECISIONS.md`'de.


## `SetSlot` "yazdım" dedi, asset sıfır kaldı

Kar tanesine terminal hız yazıldı, log doğruladı, `.vfx` dosyasında
`{"vector":{"x":0,"y":0,"z":0}}` duruyordu. `velocity` slotu `Vector3` değil
`UnityEditor.VFX.Vector` sarmalayıcısı; `PropertyInfo.SetValue` tip
uyuşmazlığında varsayılanı bırakıp sessizce dönüyor.

**Düzeltme yalnız o çağrı değil, yardımcının kendisi oldu.** `SetSlot` artık
yazdığını geri okuyup karşılaştırıyor, eşleşmezse hangi tipin beklendiğini
söyleyerek fırlatıyor. Sessiz düşüş bir kez bulundu; ikincisini araç yakalar.


## "uzaktaki kar kayboluyor" — iki ayrı sebep, ikisi de ölçüldü

**Birincisi asgari ekran boyutunun hiç olmamasıydı.** 20 metredeki 2 cm'lik tane
ekranda yarım pikselin altına düşüyor ve kayboluyor. Spec §17.1 bunu ayrıca
uyarıyor. `ScreenSpaceSize` bloğu denendi ve YANLIŞ çıktı: hiçbir modu asgari
değil, `PixelAbsolute` boyutu sabitliyor — yakındaki tane de 1.3 piksele
kilitlendi, kar toz gibi göründü. Çözüm `CustomHLSL` bloğu: aynı paketin kendi
formülü, atama yerine `max`.

**İkincisi uzak katmanın hiç yazılmamış olmasıydı.** `SnowfallLayers.farLayer`
alanı `SnowCurtainController` tipindeydi ve HİÇ KULLANILMIYORDU; üstelik yanlış
sistemi gösteriyordu — o §18.7'nin savrulma perdeleri (tetik rüzgâr), §17.2'nin
yağış perdeleri değil. `DECISIONS.md`'deki "mevcut shader bu davranışı birebir
yapıyor" kaydı bu ikisini karıştırmıştı.

**Yeni katman kurulduğunda da görünmedi — üçüncü sebep sisti.** `FogDensity01`
görüş mesafesinde doğrusal eşleniyordu ve 1150 metrede **0.95** veriyordu.
Perde alpha'sı `1 − fog * 0.6` ile 0.10'dan 0.043'e düşüyordu. Eşleme
sönümlemeye çevrildi (Koschmieder, `σ = 1/V`); aynı görüş artık 0.05 veriyor.

**Ayırt eden ölçüm — "hiç çizilmiyor" mu "çok soluk" mu.** Perde açık ve kapalı
iki kare alındı, yakın katman susturuldu, piksel farkı ölçüldü: ekranın %36.8'i
etkileniyor, en büyük fark 100/255, ortalama 1.05/255. Yani çiziliyordu ve
zayıftı. Göz kararıyla ikisi ayrılamazdı — ekranda ikisi de "yok" görünüyor.


## Sahne kurulumu Play modunda çalıştırıldı ve sessizce yarım kaldı

Play'de eklenen bileşenler ve bağlar Play çıkınca siliniyor; sahne dosyasına hiç
yazılmıyor. Bir tur boyunca "VFX bağlandı" görüldü, Play kapandı, sahnede
`VisualEffect` referansı sıfır kaldı ve kar yağmadı.

İkinci kez `MarkSceneDirty` "This cannot be used during play mode" fırlattı —
ama kurulumun SONUNDA, o noktaya kadar yarım iş yapılmıştı.

**Kapı en başa kondu:** `SetupScene` artık `EditorApplication.isPlaying` ise
hemen hata verip çıkıyor.


## "kar tutmuyor" — yağış sıcaklıktan koparılmıştı, ERİME kopmamıştı

Kar yağıyordu, `SnowfallSWERate` doğruydu (1.39e-6 m/s = spec'in 5 mm/saat'i),
zemin birikmesi neredeyse durmuştu: 45 saniyede **3.6e-9 m/s**.

**İlk şüpheli yanlış çıktı: gökyüzü görünürlüğü.** `fall = SWERate * dt * skyVis`
ve kâğıtta ters hesap `skyVis ≈ 0.16` gerektiriyordu. Doku okundu: `occlY`
her tekselde −9999, yani engel yok, `skyVis = 1`. Şüpheli elendi.

**Gerçek sebep derece-gün erimesiydi.** Dağın eteğinde hava **+4.8 °C**
çıkıyordu; `melt = 4.63e-8 * max(0, T)` yağışın %98'ini yiyordu. Ayırt eden
ölçüm: `SnowEnvironmentBridge.temperature` referansı Play'de null yapıldı
(sıcaklık manuel −4 °C'ye düştü), aynı ölçüm tekrarlandı:

    +4.8 °C : 3.6e-9  m/s
    −4.0 °C : 1.10e-7 m/s      otuz kat

**Düzeltme sıcaklık modelini bozmadan yapıldı:** `seaLevelCelsius` +7.8 → −2.
Kar çizgisi ve yağış-sıcaklık bağı kaldırıldığı için oyun "her kotta kar tutar"
diyor; sıcaklık modeli de buna uymalı. 206 m'de −1.7 °C, zirvede −42.8 °C.

**Ölçüm aracının sınırı:** `MeanSwe` 64² indirgenmiş durumdan otuz karede bir
okunuyor ve kısa pencerelerde gürültülü — rüzgâr kapatıldığında hız beklenenin
tersine düştü. Otuz katlık fark güvenilir, %20'lik farklar değil.


## "yürürken iz kalmıyor" — sahnede tek bir deformer yoktu

`SnowDeformer` sayısı sıfırdı. Yakalama pass'i çalışıyordu, kar birikiyordu,
iz bırakacak hiçbir nesne yoktu. Spec §1.4 karakter proxy'lerini "ayrı ayrı
onay al" diye bekletiyor; test istendiğinde kuruldu.

**Ölçüm aracı bir kez yalan söyledi.** `RT_Capture` `ARGBHalf`; ilk okuma
`GetData<float>` ile yapıldı ve "max 0.0078, ayak altında ≈ 0" verdi — yani
"yakalama çalışmıyor". Doğru tiple (`RGBAFloat` dönüşümü) okununca:

    maskeli teksel 114     R = −0.03 (ayak alt yüzeyi, gözlemciye göre)

114 teksel iki ayağın alanına birebir uyuyor (2 × 0.11 × 0.28 m² / 0.0234 m²).
Trail dokusunda `carve max 1.08 mm`, 113 teksel. Zincir baştan sona çalışıyordu.

**`Renderer.isVisible` bu iş için ölçüt değil** — birinci şahısta ayak zaten
kameraya görünmüyor, `False` okumak yakalamanın çalışmadığı anlamına gelmiyor.


## "kar yağışı rüzgârdan etkilenmiyor"

Grafikte rüzgâr terimi HİÇ YOKTU. Türbülans vardı (ortalaması sıfır, savuruyor
ama taşımıyor), spec §17.1'in `Hız = _WindWS + (0, −terminalVel, 0)` terimi
yoktu; kar 13 m/s rüzgârda bile dimdik iniyordu.

Rüzgâr HIZ olarak değil KUVVET olarak verildi: aşağıdaki sürükleme zaten hızı
sıfıra çekiyor, `F = wind × drag` dengesi tam `velocity = wind` veriyor ve düşey
eksende yerçekimi bozulmuyor. Ölçüldü: `WindForce` beklenen değere birebir eşit,
`F / drag = 13.97 m/s` = tam rüzgâr hızı.


## "beyaz örtü geziyor, kâğıt gibi incecik, derinliği yok"

Suçlu elemeyle bulundu: `SnowCurtainController` (§18.7 savrulma perdeleri).
Kapatılınca örtü tamamen gitti; benim eklediğim §17.2 yağış perdeleri değildi.

**İki sebep birlikte.** Dokusu tek bir düşük frekanslı fbm'di — yumuşak gri bir
bulut, hiç tanecik yok. Ve `_NearFade` 4 m'ydi; perde 12–25 m genişliğinde,
10 m ötede bile ekranın yarısını kaplayıp düz bir levha gibi duruyor.

Doku yeniden üretildi (akış şeridi × damgalanmış tanecik) ve `_NearFade` 18 m'ye
çıkarıldı. İkinci sürümde ikinci bir `TilingFbm(u*6, v*6)` denendi ve gözle
görülür düzenli bir IZGARA çıkardı — fbm yüksek frekansta tekrar ediyor;
üçüncü sürüm taneleri tek tek damgalıyor.


## Kar sistemi AY IŞIĞINA bağlanmıştı

`SnowEnvironmentBridge.Sun` = **Moon Light**, `intensity = 0` — tam gündüzde.
Tane emissive'i sıfır çıkıyordu.

Sebep `FindSun()`'ın "ilk aktif directional light"ı almasıydı. Sahnede ÜÇ tane
var (Directional Light 2.7, Moon Light 0, Lightning 0) ve tarama sırası ayı
önce buldu. Ana ışık artık `TimeOfDay`'in `sun` alanından soruluyor — gündöngü
hangisinin güneş olduğunu zaten biliyor.

**Kar aydınlatmasının geri kalanı temiz çıktı:** `SnowLitForwardPass` ana ışık +
gölge + ek ışıklar + `SampleSH` ambient okuyor, parıltı `_SunElevation01` ile
kapılı, savrulma ve yağış perdeleri `GetMainLight()` kullanıyor.


## VFX zemin kesmesi karın TAMAMINI sildi — iki kez

Spec §17.1 `if (position.y < groundHeight + 0.02) alive = false` istiyor.
Eklendi ve `aliveParticleCount` sıfıra düştü. Üç sürüm:

1. `groundY` olarak `followTarget` (KAMERA) yollandı. Kamera göz hizasında,
   zeminden 1.65 m yukarıda; kesme düzlemi oraya çıkınca her tane daha
   havadayken ölüyordu.
2. Kot oyuncunun ayağından alındı ama `attributes.position` VFX'in YEREL
   uzayında (±10) ve dünya kotu 205 ile karşılaştırılıyordu — koşul her tane
   için doğru.
3. `TransformPositionVFXToWorld` eklendi; sonuç yine sıfırın altında çıktı.
   Ayırt eden ölçüm: `groundY = 0` yazıldı, `alive` yine 0 — yani dönüşüm
   ne yerel ne dünya, güvenilmez.

Çözüm: dönüşüm kaldırıldı, kot C# tarafında yerele çevrildi
(`zeminKotu − kutuKonumu`). İkisi de orada dünya koordinatı olarak biliniyor.
Ölçüldü: `GroundY = −12.46` = beklenen yerel kot, `alive = 39915`.

**Ayırt eden araç:** `GroundY = −99999` yazmak. Kesme etkisiz kalınca `alive`
39902'ye fırladı — sorunun kesme düzleminde olduğu tek turda kesinleşti.


## Kar tanesi yoğunluğu: kutuyu küçültmek TERS tepti

"Yağış 1 iken yeterli kar göremiyorum." Kâğıtta sebep açıktı: spec §17.1'in
kapasite 40000 + kutu (40,26,40) birleşimi `0.96` tane/m³ veriyor, gerçek yoğun
kar 3–10 tane/m³.

Yoğunluk `kapasite / hacim` olduğu için önce kutu küçültüldü — (24,20,24) sonra
(20,16,20). Kâğıtta yoğunluk 3.5 ve 6.2'ye çıktı; **ekranda kar AZALDI.**
Sebep: rüzgâr 12 m/s'de tane 10 metreyi 0.85 saniyede geçiyor, dar kutuda
kameranın çevresinde hiç kalmıyor. Spec'in geniş kutusu tam bunun için.

Kutu spec'e döndürüldü, yoğunluk kapasiteden alındı (120000). Ölçüldü:
`alive 89067`, 119 FPS.

**Ayrıca doku değişince parlaklık yeniden kalibre edildi.** `DefaultDot` tam
daireydi; 4×4 kar tanesi atlası dallı ve boşluklu, aynı ekran alanında daha az
piksel dolduruyor. Gökyüzü bölgesinde en parlak piksel 222 → 193'e düşmüştü,
emissive ölçeği 1.0 → 1.6 ile 225'e döndü, hiçbir piksel doymadı.


## `fix.md` denetimi — 16 iddianın 6'sı doğrulandı, 3'ü geçersiz çıktı

Kullanıcı bir analiz raporu verdi ve "ezbere hareket etme, önce doğrula" dedi.
Her madde kodda arandı.

**Doğrulanan ve düzeltilenler:**

*Yağmur sesi kar yağarken çalıyordu.* `WeatherAudio.DriveRain` ham
`weather.Precipitation` okuyordu — ne `RainWeight01` çarpanı ne de
`PrecipitationRenderer`'ın 0.05 kesme eşiği. Kar sistemi yağmuru görselde
kapattığında ses açık kaldı; rapor bunu "hafif yağmurda ses var görüntü yok"
diye yakalamış, kar tarafı devreye girince tam bir tutarsızlığa dönüşmüş.

*Gece ana ışık aya devredilmiyordu.* `RenderSettings.sun` hep güneşe
sabitlenmişti. Sabitlemenin kendisi bilinçliydi (şimşek güneşten parlak olduğu
için Unity ana ışığı bir kareliğine ona kaptırıyordu) ama hangisine
sabitlendiği günün saatinden bağımsızdı. Ölçüldü: saat 0.00'da ana ışık artık
`Moon Light`, gündüz `Directional Light`.

*Alpenglow'da arazi kendi gölgesini kapatıyordu.* `TerrainSunShadow` güneş
ufka değdiği an `1.0` dönüyordu; gün batımından sonra vadi dipleri sırtlarla
aynı parlaklıkta kalıyordu. Sınır ufkun 0.035 altına çekildi ve arada gölge
yumuşakça bırakılıyor.

*Göz adaptasyonu tek karede sıçrıyordu.* `LookController` `adapt`i doğrudan
yazıyordu. Asimetrik üstel yumuşatma kondu — karanlığa açılma 2.5 s, aydınlığa
kısılma 0.5 s; insan gözünde ikisi aynı hızda değil.

*`_TerrainShadowReceive` `DebugMenu`'ye bağımlıydı.* Global yalnız
`DebugMenu.Update()`'te yazılıyordu; panel sahnede yoksa Unity globali sıfır
başlar ve **arazi tamamen gölgesiz** çizilir. Anahtar `_TerrainShadowOff`'a
çevrildi: kimse yazmazsa 0 kalır, 0 da "kapatma" demek — varsayılan artık
doğru tarafa düşüyor.

*Sıcaklıkta termal eylemsizlik yoktu.* `daytimeWarming * DayFactor` anındaydı;
güneş ufka değdiği saniyede hava birkaç derece zıplıyordu. Gerçekte günün en
soğuk anı gün doğumudur. `DayFactor` 45 dakikalık bir gecikmeyle izleniyor.

**Geçersiz çıkanlar — iddia kodla uyuşmadı:**

*Bulut tabanı gecikmesi.* Rapor `activeCloudBottom`'ın görsel bulut tabanı
olduğunu varsayıyor. Kodun kendi yorumu aksini söylüyor: "`CloudBottom` ve
`CloudTop` KALDIRILDI, silinen bulut sistemine aitti"; gerçek kotlar
`CloudLayerProbe`'dan geliyor. Madde eski mimariye ait.

*Bulut gölgesi çifte kararma.* Rapor "ortam ışığı sıfıra yaklaşır" diyor.
Kodda cookie yalnız `mainLight.color`'ı çarpıyor; ambient ayrı kanaldan
geliyor ve yorumda bu açıkça yazılı.

*Şimşek sesi `timeScale` desenkronu.* Rapor "unscaled yerine simülasyon zamanı
kullanılmalı" diyor; `ThunderPlayer` zaten `Time.deltaTime` kullanıyor.

*SkyFog silüet konturu.* Rapor `ZTest Equal`'ın ufuk çizgisinde tek piksellik
kontur bıraktığını söylüyor. Kod bunu **zaten çözmüş** ve çözümü yorumda
anlatmış: derinlik TAMPONU ile derinlik DOKUSU silüet pikselinde ayrışıyordu,
şimdi ikinci bir kapı var (`SampleSceneDepth` uzak düzlem değilse `discard`).
Rapor eski bir sürümü analiz etmiş.

*Ufuk haritası sınırında gölge kaybı.* Rapor kenar kelepçeleme istiyor. Kodda
`return 1.0` bilinçli ve gerekçesi yorumda: pişirilmiş arazi orada bitiyor,
güneşi kesecek kütle de yok, doğru cevap "engel yok". Aksi denenmiş ve ovada
zemini simsiyah yapmış.

**Yapılamayan:** bisikletin zemin direnci (`RollingResistance`). Alan gerçekten
boşta duruyor ama `TerrainSurface`'te zemin TİPİ API'si yok (`WindWeightAt` ve
`SlopeAt` var). Önce zemin sınıflandırması gerekiyor — `DECISIONS.md`.

---

## "Etrafta kâğıt gibi incecik beyaz örtü geziyor, bir derinliği yok"

Kullanıcı üç kez bildirdi, üç turda üç ayrı sistem suçlandı. **İkisi de doğruydu —
iki ayrı kusur aynı belirtiyi veriyordu.**

**Elenen şüpheliler — hepsi ölçümle:**

| Şüpheli | Nasıl elendi |
|---|---|
| `VFX_SnowCurtain` | Kapatıldı, örtü kaldı |
| `VFX_Spindrift` | Kapatıldı, örtü kaldı |
| Sıfır hızda `Orient: AlongVelocity` (normalize(0)) | `WindForce` sıfırdan (48,0,20)'ye çıkarıldı, dikdörtgen yerinde kaldı |
| Tek bir dev quad | Spawn 25'e indirildi, 142 tane kaldı — dev quad YOK |

**Gerçek sebep, iki tane:**

**1. Asgari ekran boyutu alfayı kısmıyordu.** Tane 1.3 piksele çekilirken kapladığı
alan büyüyor, alfa sabit kalıyordu. Ölçüm: `px/rad = 769`, taban 1.8 cm tane
**10.6 m**'den sonra piksel altına düşüyor; 20 m'de alan ×3.5, 40 m'de ×14.
Kutu 40×26×40, hacminin çoğu 10.6 m'nin ötesinde — 89 bin tane üst üste binip
süt gibi bir örtü çıkarıyordu. Keskin dikdörtgen kenar örtünün değil, **spawn
kutusunun duvarı**.

**2. `SnowCurtainController`, VFX değil.** İkinci kusur hiç VFX değildi: compute
ile sürülen, `Graphics.RenderPrimitives` ile çizilen 14 quad. Tüm `VisualEffect`
bileşenleri kapatılınca dikdörtgenler EKRANDA KALDI — belirti bu ölçümle
sahibini buldu.

**Ayırt eden ölçüm:** kamerayı 180° döndürmek. Kusur yalnız belirli açılarda
görünüyordu; tek açıya bakan üç tur onu kaçırdı.

## "Yağış 1 kar 1 iken ekranda yağmur izleri var"

**İlk şüpheli (yanlış):** `RainWeight01` hesaplanmıyor. Ölçüldü: `RainWeight01 = 0`,
doğru değer.

**Gerçek sebep:** yoğunluk `WeatherState.Changed` OLAYINDA bir kez hesaplanıyordu.
Girdilerinden biri `SnowRuntimeState.RainWeight01` ve o kar oranı sürgüsüyle
değişiyor — sürgü hava olayı yayınlamıyor. Yağmur son olaydaki yoğunlukta donup
kalıyordu. `RefreshDensity()` artık `Update`'te.

## "Yürürken kar tanecikleri çok hızlı yer değiştiriyor, sürekli yeniden render oluyor gibi"

**Sebep:** VFX sistemleri YEREL uzaydaydı (`VFXDataParticle.m_Space: 0`).
Yerel uzayda parçacık konumu objeye göre tutuluyor; yağış kutusu oyuncuyu 1 m
ızgarasında takip ettiği için her snap yaşayan 89 bin taneyi birlikte
ışınlıyordu. Yürüme hızında saniyede birkaç kez.

**Ayırt eden ölçüm:** `timeScale = 0` + `SnowfallLayers` kapalı, kare yakala,
kutuyu kaydır, tekrar yakala. Dünya uzayında 30 m kaydırma ekranın yalnız
%0.16'sını değiştirdi ve ortalama parlaklık sabit kaldı (61.9 → 61.8) — yerel
uzayda bütün kar ekrandan çıkardı.

**Snap suçlu değildi.** İlk akla gelen ızgarayı kaldırmak; o gerekçesi geçerli
(spawn deseninin kameranın peşinden sürüklenmesini önlüyor). Işınlanma
snap'ten değil UZAYDAN geliyordu.

## "Rüzgâr 0 ama kar belirli bir yöne yağıyor — yürürken düzeliyor, dururken rüzgâr varmış gibi"

**Gerçek sebep: rüzgâr GERÇEKTEN var.** Panel sürgüsü hızı değil ŞİDDETİ
sürüyor; şiddet 0 → `WindSettings.calmSpeed`. Ölçüm: `WindSpeed = 1.80 m/s`,
`WindForce = (17.54, 0, −0.47)` → `17.54 / 9.81 = 1.79 m/s`, tam +x. HUD da
dürüst yazıyor: "Hız 2,0 m/s". Sakin hava sıfır hava değil.

**Çözüm:** `calmSpeed` 2.0 → 0.6 m/s (Beaufort 1). Taneyi dikeyden 63° yerine
31° yatırıyor. Bulut katmanı kendi tabanını aldı, yoksa sakin günde gökyüzü
donuyordu — gerekçe `RATIONALE.md`.

**Yürürken neden düzeliyor:** yürüme hızı 2.2 m/s, koşma 4 m/s. Kendi hareketin
1.8 m/s'lik sürüklenmeyle aynı büyüklükte, o yüzden bağıl hareket baskın çıkıyor
ve sürüklenme göze batmıyor. Dururken tek hareket o.

**İlk şüpheli (yanlış):** türbülansın `+ 0.15` tabanı. Doğru bir kusur ama
BASKIN değil: rüzgâr 2 m/s'de türbülans zaten 0.70, taban terimin payı küçük.
Yine de düzeltildi — gerekçe `RATIONALE.md`.

**Bu turda ortaya çıkan gerçek eksik:** tanenin kendi çırpınması (spec §17.1
"Salınım") hiç uygulanmamıştı. Türbülans onun yerini tutmuyor — biri tutarlı,
diğeri dağınık.

## "Yakın uzun tanecikler sola düşerken biraz ilerideki tanecikler sağa düşüyor"

**Gerçek sebep:** rüzgârın sınır tabakası çarpanı `profile` her damla için KENDİ
altındaki araziden hesaplanıyordu (`aboveGround = damla.y − TerrainHeightAt(damla.xz)`).
Düz ovada doğru, dik dağda değil: 48 m'lik yağmur kutusu içinde arazi onlarca
metre oynuyor ve yan yana iki damladan biri "yerden 2 m", öteki "yerden 30 m"
çıkıyor. `profile` 0.3 ile 1.0 arasında zıplayınca rüzgâr tepkileri de zıplıyor
ve yağmurun ortak yönü kalmıyor.

**Elenen şüpheliler — hepsi ölçümle:**

| Şüpheli | Nasıl elendi |
|---|---|
| Türbülans genliği | Kâğıtta: sakin havada yanal dalgalanma 0.03 m/s, düşme 4.5 m/s → eğim sapması <0.5°. Fırtınada 8°. Karışıklığı açıklamıyor |
| `response = response;` ölü satırı | CPU zaten ölçekliyor (`lerp(0.03, 0.25, felt)`); satır artık, eksik ölçekleme değil |
| Yakın/uzak kutuların ayrı kayması | `dropClass` ikisinde de aynı; kutu ve kayma tutarlı seçiliyor |
| `TerminalVelocity` CPU/shader uyuşmazlığı | İkisi de aynı Atlas formülü, aralık 0.5–5.0 mm — uyuşuyor |

**Ayırt eden ölçüm:** `profile` geçici olarak `1.0` sabitlendi. İzler tek yönde
toplandı; geri alınınca yeniden dağıldı. İki yakalı geçiş.

**Çözüm:** referans arazi kotu KAMERANIN altından alınıyor
(`TerrainHeightAt(cameraPos.xz)`). Sınır tabakası araziyle birlikte yükselir,
damladan damlaya kırılmaz; kutu yalnız 48 m, o ölçekte profil sürekli olmalı.

## "Bu ne, ayaklarımın gölgesi mi?" — aşağı bakınca iki kara leke

**Sebep:** ayak proxy'leri (`SnowFoot_L/R`) `ShadowCastingMode.ShadowsOnly` ile
kuruluyordu. O kip kutuyu ana kameradan GİZLER ama gölgesini ÇİZER — kipin
amacı zaten bu. Karakter modeli olmadığı için ayakların altında iki serbest
gölge duruyordu.

**Çözüm:** görünmezlik katmandan (`SnowDeformer` katmanı URP renderer'ın
opak/saydam maskelerinden çıkarıldı), gölge kapatıldı (`ShadowCastingMode.Off`).
Yakalama pass'i `cmd.DrawRenderer` ile AÇIK materyalle çizdiği için oyma
bozulmuyor — normal çizim yolundan bağımsız.

Spec 1.3 KAMERANIN culling mask'ine dokunmayı yasaklıyor; değişen renderer
varlığının katman maskesi, o başka bir ayar.

## "Kar tutması için ne kadar beklemeliyim" → tutmuyor

**Ölçüm (tam kar, rüzgârsız, oyun içi zamanla):**

| | hız | beklenenin katı |
|---|---|---|
| düzeltme öncesi | 1.06e-9 m/s | 1/1316 |
| sıcaklık −33 °C | 2.07e-9 m/s | 1/673 |
| compute parametresi düzeltildi | 2.95e-9 m/s | 1/470 |
| beklenen (`_SnowfallSWERate`) | 1.39e-6 m/s | 1 |

**Aracın kendisi önce doğrulandı.** İlk tur duvar saatiyle ölçüldü; Unity
odaksızken Play tick atmayabildiği için `Time.time` farkına geçildi. Kareler
sayıldı: 119 saniyede 13301 kare, yani Play koşuyordu — ölçüm geçerli.

**Elenen şüpheliler:**

| Şüpheli | Nasıl elendi |
|---|---|
| Gökyüzü örtüsü (`skyVis`) | `RT_SkyVis` baştan sona −9999 nöbetçi değeri; `SampleSkyVisibility` bunu 1.0'a çeviriyor (kâğıtta doğrulandı) |
| Döşeme döndürmesi | 1024/8 = 128 grup, 4 döşem → 32×8 = 256 = tam `tileWidth`, kapsama oranı 1 |
| Erime | −33 °C'de hız yalnız iki katına çıktı; ikincil sızıntı, baskın değil |
| Rüzgâr yeniden dağıtımı | Rüzgâr 0.5 m/s → `saturate(0.5/12) = 0.042`, 470 katı açıklayamaz |

**Bulunan sebep (kısmi):** `_SnowfallSWERate` oyunda
`Shader.SetGlobalFloat` ile yayınlanıyordu. **Compute shader'lar global shader
değişkenlerini almıyor.** Çekirdek, editör sınamalarının aynı compute asset'ine
`sim.SetFloat` ile yazıp BIRAKTIĞI eski değeri okuyordu — sıfır olmamasının
sebebi de bu.

**BİRİM SINAMASININ GEÇİP OYUNUN ÇALIŞMAMASININ SEBEBİ:** sınama
`sim.SetFloat` (compute parametresi), oyun `Shader.SetGlobalFloat` (global)
kullanıyordu. İki ayrı yol; sınama oyunun kullandığı yolu hiç denemiyordu.

**Kalan 470 kat henüz bulunmadı.**

**GERÇEK SEBEP BULUNDU — yarım hassasiyet.** `RT_Snow` `ARGBHalf`'tı. R kanalı
su eşdeğerini (m) tutuyor ve tipik değer 1e-6 – 1e-2. Half'ta 6.1e-5'in altı
SUBNORMAL, temsil adımı sabit **5.96e-8**. Kare başına eklenen
`1.39e-6 × dt(0.036) = 5.0e-8` — adımın ALTINDA. Artış yuvarlanmada eriyordu.

`ARGBFloat`'a alındı. Ölçüm: hız 1.371e-6 m/s, beklenen 1.39e-6 → **oran 0.986**.

Aynı sınıfın emsali zaten projede vardı: `RT_SkyVis` mutlak dünya Y tuttuğu için
RHalf'tan RFloat'a alınmıştı. Ders: **birikimli (integre eden) bir doku half
olamaz** — artış adımdan küçükse toplam hiç ilerlemez.

Bir önceki turda "compute globalleri okumuyor" diye yazılan gerekçe YANLIŞTI;
`_TemperatureC` testi çekirdeğin globalleri okuduğunu gösterdi. O değişiklik
zararsız kaldı ama sebep o değildi.

## Kar birikiyor ama zemin beyazlamıyor — örtü 0'da kalıyor

**Sebep:** `_RainOnSnow01` hava sisteminin `PrecipKind` ETİKETİNDEN türüyordu.
Yağış sıcaklıktan koparıldığından beri o etiket −5 °C'de bile `Rain` diyor.
Sonuç: kar YAĞARKEN zemin üstüne yağmur yağıyormuş gibi işliyordu —
`rho += rainOnSnow * 25 * dt/60` yoğunluğu 55'ten 167 kg/m³'e çıkarıyor,
derinlik (`SWE × 1000 / ρ`) üçte bire iniyor ve 4 mm'lik
`SNOW_MIN_VISIBLE_HEIGHT` eşiğini hiç geçemiyor.

Ölçüm: SWE 5.5e-4, yoğunluk 167, derinlik 3.31 mm, örtü 0.

**Çözüm:** `rainOnSnow` artık karın KENDİ keskin kararından
(`SnowRuntimeState.IsSnowing` / `RainWeight01`), etiketten değil. Kar mı yağmur
mu tek bir yerde karara bağlanıyor (`SnowfallController`, eşik 0.5) ve fizik de
aynı kararı okuyor.

Ölçüm sonrası: örtü **0.9999**, `_RainOnSnow01` 0.

**Ders:** aynı olgunun iki ayrı doğruluk kaynağı olamaz. Görsel keskin karar
veriyorken fiziğin başka bir etikete bakması, ekranda kar yağarken zeminde
yağmur işletiyordu.

## Zeminde düzenli, tekrar eden koyu leke ızgarası

**Sebep:** kar kenarı gürültüsü (`_SnowBreakup`) DÜZ döşemeyle örnekleniyordu
(`posWS.xz * scale`). Aynı desen sabit periyotla tekrar edince zemin yukarıdan
ızgara gibi okunuyor.

**Çözüm:** `SampleStochasticMask` — mevcut `StochasticTiling.hlsl`'in hex
ızgarası, hücre başına rastgele kayma, varyans geri kazanımı. LUT'suz sürüm,
çünkü maskede histogramın birebir korunması görünmüyor.

Aynı yöntem projede detay normalleri için ZATEN vardı; breakup dokusu
atlanmıştı.

## Ayak izi karı delip çıplak zemini gösteriyor

**İki ayrı sebep, ikisi de düzeltildi:**

1. Oyma `baseH`'a kadar gidebiliyordu, yani kar katmanının TAMAMINI
   kaldırabiliyordu. Gerçekte ayağın altındaki kar SIKIŞIR: gevşek (ρ≈100)
   sıkışmışa (ρ≈325) dönerken hacim yoğunluk oranı kadar küçülür ve dipte her
   zaman kar kalır. Oyma artık `baseH × (1 − ρ_gevşek/ρ_sıkı)` ile sınırlı.

2. Kesme (`clip`) OYULMUŞ yüzeye bakıyordu. Derin bir iz eşiğin altına düşünce
   piksel kesiliyordu. Eşiğin sorduğu soru "burada kar var mı"; cevabı oyulmamış
   `baseH`. İz artık kar sütununun içinde bir ÇUKUR.

## İz kenarında diken diken duvar

**Sebep:** `SNOW_RIM_STRENGTH` 1.8 ve tavan 10 cm. 20 cm karda sırt hedefi
~20 cm çıkıyor, tavana kırpılsa bile karın yarısı kadar duvar oluyordu.
`rim = max(rim, ...)` birikimi bunu kare kare tırtıklı bir zarfa çeviriyordu.

**Çözüm:** güç 0.30, tavan 2 cm, ve sırt kendi izinden yüksek olamıyor
(`min(raised, blurCarve)`). Oyma burada sıkıştırma — hacmin çoğu yana taşınmıyor.

## Kar sadece oyuncunun çevresindeki KARE alanda tutuyor

**İki ayrı katman, iki ayrı sebep:**

1. **Bölge dışı sabit 0'dı.** `_FallbackSWE` "dünyanın genel kar durumu"nu
   taşıyor ama `settings.DefaultSwe` (0) ile besleniyordu — hava sisteminden
   hiç haber almıyordu. Kar 24 m'lik pencerede birikiyor, dünya öğrenmiyordu.

   **Çözüm:** `SnowManager.WorldSwe` — aynı yağış oranını dünya çapında entegre
   ediyor, aynı 6 saatlik oturma eğrisini uyguluyor. Üç yerde birden kullanılıyor
   (bölge dışı, kaydırma kenarı, ilk doldurma) ki oyuncu yürüdükçe kalınlık
   basamak yapmasın. Ölçüm: `_FallbackSWE` 0 → 3.58 mm.

2. **Kar mesh'i 24 m ve bu TASARIM.** `SnowSurface.Extent = AreaSize × 0.5`;
   mesh deformasyon bölgesiyle bilerek aynı kareyi kaplıyor (spec §6.1). Onun
   dışında kar, DAĞIN KENDİ kar katmanından gelmeli
   (`MountainSurface.hlsl`, global `_SnowCoverage`).

   **AÇIK:** `_SnowCoverage = 0.99996` ölçüldü ama dağ karanlık kaldı; mesh
   kenarında sert basamak görünüyor. Dağın kar maskesi neden geçmiyor, henüz
   bulunmadı.

## İz çukur gibi, dik duvarlı ve dipte karanlık

**Sebep zincirinin tamamı ölçümle çıktı — üç ayrı kusur:**

1. **Sıkışma kare başına uygulanıyordu.** Spec §10.1'in
   `compact = SNOW_COMPACT_RATE * saturate(...)` ifadesinde `dt` yok. 100 fps'de
   0.1 saniyelik bir ayak teması 10 kare eder ve rhoN 0.10'dan 0.55'e TEK
   adımda çıkıyordu.

2. **SWE korunduğu için yoğunluk artışı doğrudan yükseklik kaybı.**
   `baseH = SWE × 1000 / ρ`; rhoN 0.01 → 0.55 demek 20 cm kar → 3 cm demek.
   Derinlik oymadan değil SIKIŞMADAN geliyordu — oymayı sınırlamak
   (iki tur denendi) hiçbir şey değiştirmedi.

3. **Taşıma gücü yoktu.** Spec `min(penetration, baseH)` diyor, yani kar ne
   kadar kalınsa ayak o kadar batıyor. O ifade oyuncunun karın ÜSTÜNDE
   yürüdüğünü varsayıyor; bizimki araziye bastığı için batma her zaman
   tabakanın tamamıydı.

**Çözümler:** sıkışma saniye başına (`× _SnowDeltaTime`, hız 0.25/s), tek
geçişte en fazla `SNOW_MAX_COMPACT_PER_PASS` (0.06) yoğunlaşma, batmaya mutlak
sınır (`SNOW_MAX_SINK` 8 cm). Spec'in "5–6 geçişten sonra patika oluşur"
tarifi korunuyor; tek iz sığ kalıyor.

**Başarısız denemeler (geri alındı):**

| Deneme | Neden battı |
|---|---|
| `carve`'a şev açısı sınırı | Duvar `carve`'da değil yoğunluktaydı; hiçbir şey değişmedi |
| Yoğunluğa şev açısı sınırı | Kütle korunmuyor — kısıt alçak tekseli yükseltip yükseği alçaltmıyor, çukur her karede kenardan doluyor ve İZ TAMAMEN SİLİNDİ |

**Araç yalanı:** kesit probu yüzeyi `baseH − carve + rim` ile hesaplıyordu,
shader'ın oyma sınırını uygulamıyordu. "0 cm" gösterdiği yerde gerçek değer
0.92 cm'di. Prob shader'ın formülüyle hizalandı.

## Oyuncunun çevresinde parlaklığı farklı bir KARE

**Sebep 1 — otuz kare gecikme.** Dağın kar katmanı `_SnowCoverage`'dan
besleniyordu, o da async GPU geri okumasından: otuz karede bir tazeleniyor.
Kar mesh'i ANINDA güncelleniyor. Arada içerisi yeni durumu, dışarısı otuz kare
önceki durumu gösteriyordu — kullanıcı belirtiyi iki kez, ters yönlerde
bildirdi (bir kez içerisi beyaz dışarısı siyah, bir kez tam tersi).

**Çözüm:** `_SnowCoverage` artık `SnowManager.WorldSwe`'den, CPU'da, gecikmesiz.
Eğri yüzey shader'ınınkiyle aynı (`MinVisibleHeight` eşiği, `EdgeFadeRange`
bandı); üç yer de aynı sabitleri okuyor.

**Sebep 2 — AÇIK.** Gecikme kalkınca altındaki gerçek uyumsuzluk açığa çıktı:
tam örtüde mesh dağdan **%15 karanlık** (dağ R212 G207 B207, mesh R181 G174
B176). Albedo, yoğunluk, tazelik formülü ve detay normal dokusu birebir aynı;
fark aydınlatma yolunda.

Elenen: ortam (ikisi de `SampleSH × AO × diffuse`, ikisinde de yansıma küresi
yok — bu yüzden mesh'e `GlobalIllumination` eklemek %3 karanlıktan %14 parlağa
kaçırdı ve geri alındı), wrap diffuse (kâğıtta mesh'i %5 PARLAK yapıyor,
karartmıyor).

Kalan şüpheli: mesh'in yükseklik tabanlı AO'su (`SnowHeightAO`) — dağda
karşılığı yok.

**Sebep 3 — kar mesh'i gölge alıyordu.** İki yakalı ölçüm (öğle, tam örtü,
sınırın iki yakası aynı karede):

| durum | oran (mesh / dağ) |
|---|---|
| başlangıç | 0.847 |
| mesh'in AO'su kapalı | 0.835 — AO ELENDİ |
| mesh gölge ALMIYOR + ATMIYOR | **0.999** |
| mesh yalnız ATMIYOR | 0.856 |
| arazi caster'ları da kapalı | 0.881 |

Gölgeyi tamamen kesmek farkı kapatıyor, yani kaynak gölge zinciri. Atma tarafı
düzeltildi (mesh kendi gölgesini alıyordu; kar araziye oturuyor, arazi zaten
kendi gölgesini atıyor ve karın öz-gölgelemesi ayrı bir terimde —
`SnowHeightAO`). Kalan pay hâlâ AÇIK: bütün caster'lar kapalıyken bile oran
0.881, oysa gölge alma kapalıyken 0.999.

**Kalan pay ÖLÇÜLDÜ ama sebebi bulunamadı.** `mainLight.shadowAttenuation`
doğrudan ekrana basıldı: kar yüzeyinde **tek düze 0.850** (215–219/255, desen
yok). Işığın gölge gücü 1, yani bu tam gölge değil — PCF taplarının ~%15'i
gölgede okunuyor, yani yüzey her yerde bir gölge sınırına oturuyor.

Elenenler: karın kendi caster'ı (kapatıldı, 0.847 → 0.856), arazi caster'ı
(gölge koordinatı kar kalınlığı kadar ışığa ötelendi, 0.856 → 0.851 — fark yok),
mesh AO'su (0.835, fark yok).

`receiveShadows = false` yapınca oran tam 0.999 oluyor.

**GÖLGE ZİNCİRİ YANLIŞ ŞÜPHELİYDİ — KAPANDI.** Sınırın iki yakasında AYNI
büyüklükleri basan bir prob kurulunca (kar ve dağ shader'ına aynı numaralarla
altı mod; araç önce sabit renkle doğrulandı: iki taraf da 0.400 okudu) gölge
terimi 1.000/1.000 çıktı. Yani gölge hiç suçlu değildi; `receiveShadows`'u
kapatmak farkı kapatıyordu çünkü BAŞKA bir terimi de birlikte kapatıyordu.

Gerçek sebepler ÜÇ TANE, üçü de ayrı ayrı ölçüldü:

| terim | kar mesh | arazi | oran |
|---|---|---|---|
| gölge | 1.000 | 1.000 | 1.000 |
| albedo | 0.923 | 0.923 | 1.000 |
| pürüzlülük | 0.480 | 0.480 | 1.000 |
| **normal (N.y)** | **0.041** | **0.998** | ← 1. sebep |
| **doğrudan ışık** | **6.041** | **0.868** | ← 2. ve 3. sebep |

1. **`RNMBlend` paketlenmemiş normal döndürüyordu.** Girdisi 0..1, çıktısı
   −1..1 idi; her katman bir öncekini paketli sanıp `*2−1` uyguluyordu. Düz
   yüzeyde bile `(0,0,1) → (−1,−1,1)`. Kâğıtta tek harman sonrası
   N.y = 1/√3 = 0.577, iki harman sonrası 0.051; ölçüm 0.5647 ve 0.0415 —
   birebir. Mesh şekilsiz düz bir levhaydı.

2. **Speküler URP sözleşmesine aykırı kullanılıyordu.** `DirectBRDFSpecular`
   yalnız D·V skalerini döndürür; `brdfData.specular` (dielektrikte ~0.04) ve
   `NdotL` çarpanları yoktu. Doğrudan ışığın %68'i buradan geliyordu:
   spec 4.133 → düzeltmeden sonra 0.146. Spec §14.1 de aynı satırı taşıyor,
   yani hata spec'te; kod ona sadıktı.

3. **Kar mesh'i bulut gölgesini hiç okumuyordu.** `SnowLit.shader`'da
   `_LIGHT_COOKIES` pragma'sı yoktu. Ölçüm anında arazideki cookie değeri
   **0.0421** (güneşin %96'sını kesen bulut); arazi kararırken oyuncunun
   çevresindeki kar aynı parlaklıkta kalıyordu. Doğrudan ışık oranı 11533 → 1.255.

Üçü düzeltildikten sonra son renk oranı **1.160** (başlangıç 0.847, ara
değerler 24.9 ve 15.6). Kalan payın sahibi wrap diffuse + translüsanlık +
parıltı; üçü de karın bilinçli özellikleri.

**Dördüncü bir sebep: parıltı yalnız mesh'te vardı.** Normal düzelince
`saturate(dot(N,L)*4)` kapısı ilk kez açıldı ve mesh benek benek oldu, arazi
düz kaldı — kare bu kez parıltıyla çizildi. Parıltı araziye de bağlandı
(`MountainSurface.shader`, `snowMask` ağırlıklı) ve ayarları materyalden
global'e taşındı: iki yüzey iki farklı sayıyla parıldayamaz.

---

## Arazi simsiyah, oyuncunun çevresindeki kare normal

**Kullanıcının ağzından:** "kare dışı alan yine simsiyah oldu? bilinçli mi"

**İlk şüpheli (yanlış):** ışıklandırma, gölge, sis.

**Gerçek sebep:** Play sırasında shader yeniden içe aktarılınca `TerrainSurface`
runtime materyali (`HideFlags.DontSave`) nesne olarak ayakta kalıyor ama
ÜZERİNE YAZILMIŞ TÜM DEĞERLER siliniyor. `_TerrainSize` sıfıra düşüyor,
`uv = (pos − origin) / 0` sonsuz oluyor ve arazinin TAMAMI NaN basıyor.
`EnsureMaterial` yalnız `material != null` kontrol ediyordu; `ApplySettings`
de `appliedRevision` eşit kaldığı için atlıyordu.

**Ayırt eden ölçüm:** kimlik maskesiyle ayrılmış piksellerde arazinin
albedo/ortam/doğrudan/normal kanalları **162674 pikselin 162674'ünde NaN**;
gölge ve N·L kanalları temiz görünüyordu çünkü `saturate(NaN)` D3D'de 0
döndürüyor. Araç önce sabit renkle doğrulandı.

**Not:** bu yalnız editörde, Play sırasında shader reimport edilince oluyor.
Ama iki tur ölçümü çöpe attı ve kullanıcıya iki kez yanlış belirti gösterdi.
`EnsureMaterial` artık `material.HasVector(TerrainSizeId)` de kontrol ediyor ve
`Update` her kare çağırdığı için kendini onarıyor.


---

## Kar izi damga gibi, RDR2 oluğu değil

**Kullanıcının ağzından:** "ben rdr2 tarzı oluk istiyorum, adım gibi gözükmesin"
ve "yumuşak oluk sığ iz istiyorum".

**İlk şüpheli (yanlış):** oymanın derinliği, `SNOW_MAX_SINK`.

**Gerçek sebep ÜÇ TANE, üçü de ayrı ölçüldü.**

**1. Oymanın enine kesiti dik duvarlı bir basamaktı.**

```
enine kesit (mm, 23.4 mm teksel): 0 0 0 0 0 0 0 0 80 80 80 80 0 0 0 0
```

8 cm derin, 9 cm geniş, geçiş SIFIR. `KDeform` yakalamayı `cap.a > 0.5` ile
ikili bir kapıdan geçiriyordu. Oysa bulanıklaştırılmış kapsama payı zaten
yumuşak bir rampa:

```
cap.a kesiti: 0.00 0.04 0.31 0.65 0.81 0.98 1.00 1.00 1.00 0.94 0.70 0.20 0.00
```

Eşik bu rampanın tamamını atıyordu. Kapsama artık yanal yük profili olarak
kullanılıyor; kesit yumuşadı:

```
0 0 9 40 65 80 80 80 80 80 80 77 | 28 | 65 80 80 80 80 80 71 49 14 0 0
```

İki ayak izi, her biri ~19 cm, üç tekselde yumuşak kenar.

**2. `min(..., tasimaSiniri)` rampayı düzleştiriyordu.** Tavanın üstündeki
her değer tam tavana iniyordu. Tavan artık en derin noktanın sınırı, profili
kapsama veriyor.

**3. Detay normalleri oluğun EĞİMİNİ siliyordu — asıl sebep buydu.**

Ölçüm zinciri:

| ölçülen | değer |
|---|---|
| oluğun derinliği (yüzey geometrisi) | 7.5 cm, yumuşak profil |
| merkezi farkın ham bileşeni `\|hD-hU\|` | 39, 22, 17, 19, 35 mm — gradyan var |
| detay ÖNCESİ N.y | 0.766 – 1.000 (oluk görülüyor) |
| detay SONRASI N.y | 0.998 her yerde (oluk yok) |
| son görüntüde oluk kontrastı, detay devrede | **%0.8** |
| aynı ölçüm, taban normali doğrudan | **%10.6** |

Detay şiddeti sıfıra indirilince bile kontrast %0.5'te kaldı: sorun şiddet
değil, RNM'nin bu bağlamdaki davranışıydı. `RNMBlend(taban, DÜZ detay)`
kimliği yerinde ölçüldü ve TUTMADI — cebirsel olarak tutması gerekirken
tabanı değil düz normali döndürdü.

**Çözüm:** detay artık eğim uzayında toplanıyor
(`SnowDetailNormals.hlsl` → `SampleDetailSlope`). Dosyanın kendi ilkesi zaten
buydu: "normaller türev, türev doğrusal toplanır". Eğim toplamı tabanı
yapısı gereği korur — detay sıfırsa sonuç tabanın kendisidir.

**Sonuç:** oluk kontrastı **%1.0 → %13.3**. Ekranda iki sürekli paralel oluk;
damga yok, dipte çıplak zemin yok.
