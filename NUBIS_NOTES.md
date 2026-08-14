# Nubis okuma notları

Kaynaklar (`C:\Users\musta\Downloads\nubis`):
`[H18]` haggstrom-2018 · `[N15]` nubis-2015-hzd · `[N17]` nubis-2017-decima · `[N22]` nubis-2022-evolved

**Bu dosya makale özeti değil, SORU–CEVAP.** Sorular okumadan önce yazıldı ve hepsi
2026-08-14'te ekranda görülmüş bir belirtiden geliyor. Cevap yazarken kaynak sayfa
zorunlu: `[N22 s.34]`. Kaynağı olmayan cümle nottan sayılmaz, tahmindir.

Okuma **ilerledikçe** doldurulur, sonunda değil.

## Hiçbir şey kaçmasın diye: okuma kuralları

1. **20'şer sayfa, sırayla, atlama yok.** "Bu bize lazım değil" diye sayfa geçilmez.
   (Blok formül yoğunsa 12'ye düşülür — not incelirse kural çiğnenmiş olur.)
2. **Her bloktan sonra defter güncellenir** (`s.X–Y okundu`). Defter baştan sona
   KESİNTİSİZ olmalı; boşluk varsa o sayfalar okunmamıştır. Kanıt defterde, hafızada
   değil.
3. **Sorulara girmeyen her şey aşağıdaki "Sorulmamış bulgular" bölümüne** yazılır.
   Yukarıdaki 12 soru bizim BİLDİĞİMİZ eksikler; bilmediklerimiz oraya düşer.
4. **Bağlam biterse defterdeki son sayfadan devam edilir.** Oturum kesilse bile
   kaldığı yer bellidir.
5. Kaynaksız cümle yazılmaz.

---

## 1. Bir bulutun eni ve boyu neyden gelir?

Bizde yerleşimi gürültü belirliyordu, harita değil — sonuç ızgara. Hangi büyüklük
haritadan, hangisi gürültüden geliyor?

**`[N22]` boyu HARİTADAN alıyor — 2015'ten farkı bu.** Cloudscape "Nubis Data Fields"
(NDF) denen üst üste 2B alanlardan kuruluyor `[N22 s.18]`. Dikey profil modelinin
NDF'leri **beş kanal** `[N22 s.20]`:

- Cloud Min Height
- Cloud Max Height
- Cloud Coverage
- Cloud Bottom Type
- Cloud Top Type

Yani bir kolonun tabanı ve tavanı doğrudan haritada yazıyor; boy = max − min. 2015'te
(`[N15 s.34]`) boy yalnız tipten geliyordu ve harita üç kanaldı.

**Bizim için:** sildiğimiz A "tavan" kanalı fikren yanlış değilmiş — 2022 aynısını iki
kanalla yapıyor. Yanlış olan bizim uygulamamızdı (çekirdek başına kubbe + MAX birleşimi
+ shader'da beş çarpan). 2022'de tavan doğrudan okunuyor, türetilmiyor.

## 2. Gürültünün dünya periyodu neye göre seçilir?

Bizde taban gürültüsü 3333 m, katman 2500 m'ydi: bir periyot bile sığmıyor, gürültü
dikeyde değişmiyor, bulut sütuna dönüyordu. Periyot / katman kalınlığı / bulut eni
arasındaki oran ne olmalı?

> *(cevap)*

## 3. Kapsama sürgüsü ne yapar?

Bizde `harita × sürgü` yazılıydı ve %100'de bile gök kapanmıyordu (haritanın sıfır
olduğu yer hiçbir sürgüde dolmaz). Ölçekleme mi, eşik kaydırma mı, başka bir şey mi?

Kapsama **dikey profili çarpıyor**, sonra sonuç eşiğe dönüşüyor `[N22 s.30]`:

```hlsl
float dimensional_profile = vertical_profile * cloud_coverage;
```

Yani kapsama doğrudan gürültüyü ölçeklemiyor; önce profili ölçekliyor, profil de aşağıda
eşiği belirliyor. Kapsama 0 → profil 0 → eşik 1 → hiçbir gürültü geçemez. Kapsama 1 →
eşik profilin kendisi.

**Bizim hatamız buydu:** `saturate(harita.r × sürgü × 1.8)` yazmıştık — kapsamayı
haritayla ÇARPIP eşiğe çeviriyorduk. Haritanın sıfır olduğu yerde çarpım sıfır kalıyor
ve sürgü ne olursa olsun bulut gelmiyordu.

## 4. Yükseklik gradyanı şekli ÇARPAR mı, eşiği mi yükseltir?

`[N15 s.35]` çarpıyor: `SetRange(gürültü × gradyan) × kapsama`. Bizde çarpınca tepeler
iğneye döndü. Sönüm bandının genişliği ile gürültünün özellik boyu arasındaki ilişki ne?

**`[N22]`'de gradyan artık elle yazılmış eğri değil, 2B ARAMA TABLOSU** `[N22 s.21-22]`.
Yatay eksen "Top Type" (0→1), dikey eksen yükseklik; hücre değeri o yükseklikteki
yoğunluk. Tip 0'da düz ince dilim (stratus), tip 1'e doğru stratocumulus → cumulus:
taban yayılıp tepe dikleşiyor.

Yani üç `smoothstep` eğrisini karıştırmak yerine, geçişin tamamı **pişmiş bir dokudan**
okunuyor — ara değerler tanım gereği pürüzsüz.

*(Çarpma mı eşik mi sorusu henüz cevaplanmadı; ilerideki sayfalarda.)*

## 5. Kenar yumuşaklığı nereden gelir?

Bizde `pow(t, 4)` uydurulmuştu. Makalelerde kenarı yumuşatan şey ne — erozyon mu,
yoğunluk eğrisi mi, örnekleme mi?

**`[N22]`'de gradyan ne çarpıyor ne eşik yükseltiyor — ÇIKARILIYOR** `[N22 s.34]`:

```hlsl
float cloud_density = saturate(cloud_noise_composite - (1.0 - dimensional_profile));
```

Yani gürültüden `(1 − profil)` çıkarılıyor. Cebirsel olarak `gürültü + profil − 1`:
profil gürültüyü YUKARI itiyor, eşik sabit 0'da kalıyor, `saturate` kırpıyor.

**Bölme YOK.** Bizim `CloudRemap(shape, 1-cov, 1, 0, 1)` = `(shape − (1−cov)) / cov`
kullanıyorduk — payı aynı ama **kapsamaya bölüyorduk**. Bölme, kenar bandını
normalize edip yoğunluğu hızla 1'e çıkarıyor; keskin kenarların matematiksel kaynağı bu.
`[N22]` bölmediği için kenar doğal olarak yayvan çıkıyor.

**Profilin kendisi iki 2B arama tablosunun ÇARPIMI** `[N22 s.28]`:

```
vertical_profile = LUT(yükseklik, top_type) × LUT(yükseklik, bottom_type)
```

`[N22 s.21-22]` üst tip tablosu: tip 0 düz ince dilim (stratus) → tip 1 dikine kabaran
(cumulus). `[N22 s.26]` alt tip tablosu ayrı ve yatayda çok daha yumuşak — bulutun
tabanının tüylülüğünü o taşıyor. Elle yazılmış `smoothstep` eğrisi yok, hepsi doku.

## 6. Adım boyu ve mip seçimi

Bizde adım mesafeyle büyüyor, uzakta mip 4.5'e çıkıp gürültüyü siliyordu. Kaç örnek,
adım nasıl büyüyor, mip nasıl seçiliyor, çizim menzili kaç?

Kısmi cevap `[N22 s.34]`: kenarı yumuşatan ilk şey **bölmemek**. `saturate(gürültü −
(1 − profil))` kenarda doğrusal ve yavaş açılıyor; bizim yaptığımız gibi kapsamaya
bölünürse aynı bant kapsama küçüldükçe daralıp bıçağa dönüşüyor.

**BİRİKTİRME FORMÜLÜ — bizimkinden bambaşka** `[N22 s.61]`:

```hlsl
// Absorption from sampled density
light_absorption += sampled_density * (1.0 - light_absorption);

// Energy, attenuated by depth along the view ray
light_intensity  += (light_energy * sampled_density * (1.0 - light_absorption));

float3 color = direct_intensity * sun_color + amb_intensity * amb_color;
float  alpha = light_absorption;
```

`exp(−density × step)` ile geçirgenlik çarpımı YOK. Alfa harmanı gibi birikiyor ve
`light_absorption` kendiliğinden 1'e doyuyor — bizim "erken çıkışta alfa 0.88'de kalıyor,
arka plan bulutun içinden görünüyor" sorunumuz bu formülde yapısal olarak yok.

**Ambient tek satır** `[N22 s.59]`:
```hlsl
float ambient_scattering = pow(1.0 - dimensional_profile, 0.5);
```
Sonda yok, koni yok, gökten örnekleme yok — profilin tersinin karekökü. Bizim
`_CloudAmbient`, `ambientFloor`, `massWarmth`, `massBrightness` zincirinin karşılığı
bu tek satır.

## 7. Kameranın bulutun İÇİNDEN geçmesi

Bizim senaryomuz. `[N22]`'nin ana konusu. Yakın alanda ne değişiyor, maliyet nereden
çıkıyor, hangi yaklaşım terk edilmiş?

**Sayılar `[N22 s.39]`:**

```hlsl
float near_step_size           = 3.0;
float far_step_size_offset     = 60.0;
float step_adjustment_distance = 16384.0;

float step_size = near_step_size
                + ((far_step_size_offset * distance_from_camera) / step_adjustment_distance);
```

**Doğrusal**, üstel değil. Kamerada 3 m, 16.4 km'de 63 m.

Bizimkiyle karşılaştırma — bizde `(2000/110) × (1 + d/2300)`:

| mesafe | `[N22]` | bizde | oran |
|---|---|---|---|
| 0 m | **3 m** | 18 m | 6× kaba |
| 1 km | **6.7 m** | 26 m | 3.9× kaba |
| 10 km | **39 m** | 96 m | 2.5× kaba |
| 16 km | **63 m** | 143 m | 2.3× kaba |

**Mip sorunumuzun cevabı bu.** 3 m adımla 128³ doku mip 0'da okunuyor; ince gürültü
hiçbir yerde ortalamaya yatmıyor. Bizim 18 m'lik taban adımımızla doku daha kameranın
dibinde mip 1-2'ye düşüyordu. "İnce gürültü koyunca bulutlar kayboluyor" belirtisinin
kaynağı gürültü değil, **adım boyu**.

Karşılığında menzil kısa: `step_adjustment_distance` 16384 m, yani ölçek 16 km'ye göre
kurulmuş. Bizim 300 km'lik görüş menzilimizle bu adım boyu tutmaz — menzil kesilmeden
ince gürültü kullanılamaz.

## 8. Temporal yeniden yansıtma ve artefaktlar

Bizde harman yoktu, piksellenme kalıcıydı. History nasıl tutuluyor, komşuluk kelepçesi
nasıl, hareket hâlinde ne bozuluyor?

**Yeniden yansıtma BULUTUN KENDİ HAREKETİNİ hesaba katıyor** — kamera hareketini değil
yalnız. `[N22 s.152, 157]`:

```hlsl
// 1) Pikselin dünya konumu, AYRI BİR MESAFE TAMPONUNDAN
float3 view_space_vec  = CreateEyeRay(inViewportUV, inFovScale);
float  cloud_distance  = inCloudAttrWorkingBuffer.SampleLOD(inSampler, inUV, 0).r;
float3 cloud_world_space = mul(inInvViewMatrix, float4(view_space_vec * cloud_distance, 1.0)).xyz;

// 2) Bulutun KENDİ hareketi eklenir (gökyüzü için sürüklenme, fırtına için dönme)
cloud_world_space = lerp(cloud_world_space + scroll_direction_2D * inDeltaTime,
                         superstorm_rotated_world_space_position, superstorm_mask);

// 3) Bir önceki karedeki ekran konumu
float4 prev_sample_pos = mul(inReprojectionMatrix, float4(view_space_vec, 1.0));
prev_sample_pos /= prev_sample_pos.w;
prev_sample_pos.xy *= float2(0.5, -0.5);
prev_sample_pos.xy += float2(0.5, 0.5);
```

**İki şey bizde hiç yoktu:**

1. **Bulut mesafe tamponu.** Piksel başına bulutun dünya mesafesi ayrı bir tampona
   yazılıyor; geçmiş kareye eşleme o mesafeden kurulan dünya konumuyla yapılıyor.
   Mesafe olmadan doğru yeniden yansıtma zaten imkânsız.
2. **Bulutun kendi hareketi.** `+ scroll_direction_2D * inDeltaTime` — bulut rüzgârla
   kayıyorsa geçmiş kare de o kadar kaydırılarak okunuyor. Bu olmadan hareket eden
   bulutta geçmiş yanlış yerden okunur ve kenar bulanır/beneklenir.

`[N22 s.158]` iki görüntüyü yan yana koyuyor: hareketi hesaba katmayan sol taraf
gürültülü, katan sağ taraf temiz.

**Bizde zamansal harman hiç yoktu** — `VolumetricClouds.shader`'da "bilinçli HARMANSIZ"
yazıyordu ve piksellenme kalıcıydı. Doğru çözüm harmanı kapatmak değil, yeniden
yansıtmayı mesafe tamponu + bulut hareketiyle kurmakmış.

## 9. Aydınlatma parametreleri

Koni kaç örnek, uzak örnek nerede, HG eksantrikliği kaç, powder formülü ve gücü,
yağışta soğurma nasıl artıyor?

**Işık örneklemesi `[N22 s.41]`:** güneş yönünde **256 m** boyunca, örnekler ÜSTEL
aralıklı (örnek noktasına yakın sık, uzakta seyrek).

**Enerji ayrışımı `[N22 s.42]`:**
```
Light Energy = Direct Scattering + Ambient Scattering
```

**Doğrudan saçılma İKİ terimli `[N22 s.43]`:**
```
Direct Scattering = (Transmittance × Primary Scattering Phase)
                  + (Multiple Scattering × Secondary Scattering Phase)
```

Yani tek bir Henyey-Greenstein değil: birincil saçılma kendi fazıyla, çoklu saçılma
AYRI bir fazla. `[N22 s.44-45]` ikisinin geometrisini ayrı ayrı çiziyor — birincil
ışın buluta girip tek sekmede göze geliyor, çoklu saçılma içeride birkaç kez sekiyor.

Beer-Lambert `T = e^(−d)` temelde duruyor `[N22 s.46]`.

**Henyey-Greenstein** `[N22 s.48]` standart hâliyle:

```hlsl
float HenyeyGreenstein(float inCosAngle, float inG)
{
    float num = 1.0 - inG * inG;
    float denom = 1.0 + inG * inG - 2.0 * inG * inCosAngle;
    float rsqrt_denom = rsqrt(denom);
    return num * rsqrt_denom * rsqrt_denom * rsqrt_denom * (1.0 / (4.0 * M_PI));
}
```

**Çoklu saçılma hacmi** `[N22 s.54]` — bizim en çok uğraştığımız yer:

```hlsl
float ms_volume = Remap(dimensional_profile * step_size, 0.1, 1.0, 0.0, 1.0);
ms_volume *= pow(attenuated_light, cMultipleScatteringDepthPower);
ms_volume *= pow(height_fraction, cMultipleScatteringHeightPower);
```

Üç çarpan: **profil × adım boyu** (yani o dilimin optik kalınlığı), **sönümlenmiş ışık**
bir üsse, ve **yükseklik oranı** bir üsse.

**Bizim için kritik:** koyuluğu süren şey `attenuated_light` — yani ışık ışınının
geçirgenliği. Görüş ışınının geçirgenliği DEĞİL. Gün sonunda `buried = 1 − lit`e
kendiliğimizden varmıştık; `[N22]` aynı büyüklüğü kullanıyor ama üsle şekillendiriyor
ve ayrıca yüksekliğe bağlıyor (bulutun dibi daha koyu).

Yerel yoğunlukla (`local`) sürmek burada da yok — o bizim uydurmamızdı ve iki tonlu,
bıçak sınırlı görüntünün sebebiydi.

## 10. Bulut gölgesinin yere düşürülmesi

Bizde `CloudShadowAt` gökyüzüyle aynı alandan besleniyordu. Makalelerde ayrı bir gölge
haritası mı pişiriliyor, yoksa aynı alan mı okunuyor?

**`[H18 s.64]` — bölüm 6.2.4, LİMİTASYONLAR başlığı altında.** Yani çözülmüş değil,
kabul edilmiş bir kısıt:

> *"Uygulanan aşırı yeniden yansıtma optimizasyonu yüzünden bulutların içinden geçerken
> ciddi görsel kalite sorunları çıkıyor. Sebep, piksellerin kare hızından yavaş
> güncellenmesi ve bir sonraki karede pikselin temsil ettiği bulut kameranın arkasında
> kaldığında yeniden yansıtmanın yetmemesi. Yalnız en son işlenmiş pikselleri seçerek
> çözmek mümkün ama bu sefer kalite düşer, çünkü bulutlar çeyrek çözünürlükte
> işleniyor."*

**`[N22]`'nin cevabı farklı ve daha iyi:** mesafe tamponu + bulutun kendi hareketini
yeniden yansıtmaya katmak (soru 8). H18'de bu yok, o yüzden kısıt olarak kalmış.

**Bizim için sonuç:** kameranın buluttan geçmesi, çeyrek çözünürlük + yeniden yansıtma
ile YAPISAL olarak çelişiyor. İki seçenek var — ya `[N22]`'nin hareket-farkındalıklı
yansıtması kurulur, ya da bulut tam çözünürlükte yürünür (`[N22]` PS5'te bunu yapıyor).
Bizim eski `downsample = 4` + harmansız birleştirme kararı ikisinin de dışındaydı.

## 11. Hava haritası kanalları ve nasıl sürüldükleri

`[N15 s.40]` R kapsama, G yağış, B tip diyor. `[N17]` weather map sistemini anlatıyor.
Kanallar zamanla nasıl değişiyor, simülasyon mu, boyanmış doku mu?

**İki ayrı NDF kümesi, her birinin sonunda bir "Influence Mask"** `[N22 s.75]`:

- Vertical Profile Model NDF: min h, max h, kapsama, alt tip, üst tip **+ influence mask**
- 2.5D NDF (cirrus): kapsama, tip **+ influence mask**

**İki üretim yolu aynı boru hattında** `[N22 s.69-74]`:
- **NDF Generator** — prosedürel, döşenen hava haritası üretiyor
- **NDF Editor** — Houdini içinde elle heykel; bulutlar viewport'ta gerçek hâliyle
  görünüyor `[N22 s.72]`

İkisi "Influence NDFs" üzerinden birleşip "Authored NDFs" oluyor.

**Bölgesel:** dünya haritası bölgelere ayrılmış (San Francisco vb.) ve her bölgenin
kendi Influence NDF'i var `[N22 s.77]`. Yani bulut karakteri coğrafyaya bağlı.

**Hava durumu geçişi:** her hava durumu (Calm Cloudy / Stormy / Calm Clear) kendi
**cloudscape**'ini VE kendi bölgesel influence NDF'ini taşıyor; "Current Weather" bunların
harmanı `[N22 s.78]`. Yani hava değişimi tek bir kapsama sürgüsü değil, tam bir NDF
kümesinin diğerine geçmesi.

## 12. Hangi optimizasyon ne kazandırıyor, hangisi artefakt üretiyor?

Bizde ışından türeyen her ucuzlatma ekranda izo-yüzey çizdi. Makalelerde hangi
ucuzlatmalar var, hangileri gönderilmiş, hangileri geri alınmış?

**Ölçülmüş hedef: bulutlar ~0.4 ms** `[N22 s.82]` — GPU profilinde `Clouds 434 µs`,
ortalama/medyan `329 µs / 401 µs`.

Bizde bulutlar ~4 ms tutuyordu (bisikletde 280 FPS, gökyüzünde 130). **On kat fark.**

Örnekleme deseni seyrek `[N22 s.81]`: ekran, aralıklı piksellerden oluşan bir ızgarayla
örnekleniyor (her kare her piksel değil).

**Ucuz boşluk testi** `[N22 s.90]` — 3B gürültüye hiç dokunmadan:

```hlsl
float cloud_coverage      = GetCloudCoverageSample(sample_position);  // 1 doku okuması
float vertical_profile    = GetVerticalProfile(sample_position);      // 2 doku okuması
float dimensional_profile = vertical_profile * cloud_coverage;        // 1 çarpma

if (dimensional_profile < density_threshold) return 0.0;
```

Yani bizim "kaba eleme"nin karşılığı: üç 2B okuma + bir çarpma. Sıçrama haritası,
genişletme, üst sınır türetme yok — profil zaten kolonun tamamını tarif ettiği için
eşiği doğrudan test edebiliyorlar.

**PS4 / PS5 ölçekleme tablosu** `[N22 s.183]` — hangi kalem nereye kadar kısılıyor:

| | PS4 | PS5 |
|---|---|---|
| Çözünürlük | 960 × 540 | 1920 × 1080 |
| Işık ışını örneği | 6 | 10 |
| Görüş ışını örneği | 60–90 | 96–180 |
| Bulanıklık ölçeği (piksel) | 2× | 1× |
| Gürültü dokusu MIP | 1 | 0 |

**Bütçe** `[N22 s.184]`: fırtınaya bakarken **≤ 4 ms**, normalde **≤ 2-3 ms**.
Bizim TEK katmanımız 4 ms'ti — onların üç katmanlı en kötü durumu kadar.

**Çözünürlük TAM KARE.** PS5'te 1920×1080, yani yürüyüş tam çözünürlükte. Bizde
`downsample = 4` idi (1/16 piksel) ve piksellenmenin bir kısmı oradan geliyordu.

**Görüş ışını örneği 60-180**; bizde 110 (döngü sınırı 550). Aynı mertebede — sorun
örnek sayısı değil, adım boyunun mesafeyle nasıl büyüdüğü (bkz. soru 6).

---

## Sorulmamış bulgular

### `[H18]` — kimlik ve yapı

Fredrik Häggström, Umeå Üniversitesi yüksek lisans tezi, 2018. Arrowhead Game Studios'ta
yapılmış. **Doğrudan Schneider'in işini geliştirmeyi hedefliyor** `[H18 s.2]`.

**Performans hedefi: 2 ms altı**, NVIDIA GTX 980 Ti `[H18 s.2]`. Bizim referansımız
olabilir — Nubis'in PS5 sayıları (0.4 ms) konsola özel, bu masaüstü kartı.

**Özetteki iki bulgu tam bizim açık sorularımız** `[H18 abstract]`:
- güneşe atılan adım sayısı **tek haneye** indirilebiliyor
- ışın yürüyüşü adım boyu, **başlangıç mesafesine + küresel kapsamaya + küresel
  yoğunluğa** göre değiştirilebiliyor

İkincisi kritik: bizde adım yalnız mesafeye bağlıydı. Kapsama ve yoğunluğa da bağlamak
akla gelmemişti.

**Bölüm haritası** (basılı sayfa): 3.1 şekil/yoğunluk 10 · 3.1.2 hava haritası 10 ·
3.1.3 yüksekliğe bağlı fonksiyonlar 12 · 3.1.4 şekil ve detay gürültüsü 14 ·
3.1.5 örs 16 · 3.2 ışın yürüyüşü 18 · **3.2.1 optimizasyonlar 19** · 3.3 aydınlatma 27 ·
3.4 renk harmanı 32 · 3.5 render hattı 33 · 3.6 hareket 34 · **4 deneyler 35** ·
**6.2.4 bulutların içinden geçmek 64** ← soru 7 · **Ek B: KOD 77**


Yukarıdaki on iki sorunun hiçbirine girmeyen ama önemli görünen her şey. Soru listesi
bizim bildiğimiz eksiklerden yapıldı; bilmediklerimiz burada birikir. Kaynak zorunlu.

**Katman ikiye bölünmüş** `[N22 s.16]`: alçak **Stratus alt-katmanı** ve yüksek
**Cirrus alt-katmanı**. Kamera stratus alt-katmanının içinden geçiyor. Bizdeki
"hacimsel + üstte 2B" ayrımının 2022'deki karşılığı bu.

**NDF alanı 16 km × 16 km** `[N22 s.19]`. Bizim hava haritamız 48 km'ydi. Onlarınki
üç kat küçük — yani tekrar daha sık ama görüş menzili de dar.

**Katman kotları 256 m – 2048 m** `[N22 s.20]`, insan figürüyle ölçeklenmiş. Kalınlık
1792 m. 2015'te 1500–4000 m'ydi; alçalmış ve incelmiş.

**BİZİM SENARYOMUZUN BÖLÜMÜ: "Environments"** `[N22 s.84+]`. Amaçlar: açık dünya,
performanslı, detaylı. Konusu **orografik bulutlar** `[N22 s.88-93]` — dağa yaslanan,
zirveden bayrak gibi savrulan, dağın beline halka gibi oturan bulutlar. Kaynak yine
Clausse & Facy'nin 1961 kitabı; "cloud banner on the side of a mountain" fotoğrafı
doğrudan bizim oyunumuzun görüntüsü.

Bu bölüm "The Envelope Model" ile devam ediyor `[N22 s.94]` — repo'nun README'sinde
"Not Included" dediği **local clouds** bu olsa gerek. Bizim dağ senaryomuz için asıl
kaynak burası.

**Cirrus için AYRI model: 2.5-D** `[N22 s.62-67]`. NDF'i yalnız iki kanal (kapsama,
tip). Yoğunluk üç 2B dokunun tiple harmanı — `cr_streaky`, `cr_wispy`, `cr_round`:

```hlsl
density = ValueRemap(cloud_type, 0.5, 1.0,
              ValueRemap(cloud_type, 0.0, 0.5, cr_streaky, cr_wispy), cr_round);
density = pow(density, 1.0 - ValueRemap(cloud_coverage, 0.0, 1.0, -0.9, 0.9));
density *= ValueRemap(pow(cloud_coverage, 3.0), 0.0, 0.5, 0.0, 1.0);
```

Burada kapsama **üs olarak** giriyor (kontrast) + ayrıca çarpıyor. Eşik değil.
Işık için ince katmanda 4 örnek yetiyor `[N22 s.67]`.

**Yazarlık zinciri** `[N22 s.69]`: NDF Generator + NDF Editor → Influence NDFs →
Authored NDFs. Yani prosedürel üretim ve elle boyama aynı boru hattında birleşiyor.

**Onların hava haritası da DÖŞENİYOR** `[N22 s.70]` — ekrandaki hava penceresinde
tekrar açıkça görünüyor. Yani döşeme tekrarı kabul edilmiş bir durum, gizlenmemiş.

**Hareket tek satır** `[N22 s.37]`: `noise_sample_position = sample_position −
wind_direction × scroll_offset`. Örnekleme koordinatı rüzgârla kaydırılıyor, başka bir
şey yok. Bizdeki makaslama, konvektif yükselme, evrim, döşeme kırıcı — hiçbirinin
karşılığı yok.

**Gürültü tek doku, 4 kanal, 128³** `[N22 s.33]` — "Noise Composite". Kanallar artan
frekansta; ilki Perlin-Worley, kalanlar Worley. 2015'teki yapının aynısı, yani doku
tarafı yedi yılda değişmemiş.

**Perlin-Worley'nin tanımı** `[N22 s.32]`: Perlin ile `1−Worley` birleştiriliyor —
Perlin'in bağlantılılığı korunuyor, Worley'nin kabarcıkları ekleniyor.

**Terim:** "Nubis" adı Luke Howard'ın 1802'deki bulut sınıflandırmasından
("nubification") geliyor `[N22 s.12]`. İşe yaramaz ama kaynağı belli olsun.

---

## Zarf Modeli (Envelope Model) — DAĞ BULUTLARI

`[N22 s.94-106]`. Gökyüzü modelinden ayrı ikinci bir sistem: dağa yaslanan, zirveden
savrulan, belli bir yere çakılı bulut kütleleri. **Bizim oyunumuzun asıl ihtiyacı bu**
ve repo'nun "local clouds — Not Included" dediği şey.

**NDF: dört kanal** `[N22 s.106]` — Cloud Min Height, Cloud Max Height, Cloud Type,
Cloud Density. (Gökyüzündeki beş kanaldan farklı: kapsama yok, yoğunluk var.)

**Profil analitik, arama tablosu YOK** `[N22 s.97]`:

```hlsl
float height_fraction = Remap(height, min_height, max_height, 0.0, 1.0);
float top_gradient    = pow(1.0 - height_fraction, 1.5);
float bottom_gradient = pow(height_fraction, 2.0);
float edge_gradient   = Remap(sample_height, 0.0, 35.0, 1.0, 0.0);

float dimensional_profile = bottom_gradient * top_gradient * edge_gradient;
```

Üç gradyanın çarpımı. `edge_gradient` kütlenin yatay kenarını 35 birimde söndürüyor.

**Gürültü yükseklikle harmanlanıyor** `[N22 s.99]`:

```hlsl
float noise_height_blend = Remap(height_fraction, cloud_type + 0.1, cloud_type - 0.1);
float composite = lerp(wispy_noise, billowy_noise, noise_height_blend);
```

Tabanda tüylü (wispy), tepede kabarık (billowy) — ve **geçişin nerede olacağını
`cloud_type` belirliyor**. Bizim "taban ters Worley, tepe normal" yaklaşımımızın doğru
hâli bu: sabit bir yükseklikte değil, tipin söylediği yükseklikte.

**Yoğunluk** `[N22 s.105]`:

```hlsl
float cloud_density_sample =
    height_fraction * pow(saturate(noise_composite - (1.0 - dimensional_profile)), 0.27);

float inv_edge_signal_pow_3 = pow(inv_edge_signal, 3.0);

float cloud_density        = cloud_density_sample;
float cloud_coarse_density = pow(ValueErosion(dimensional_profile, 0.04), 0.5)
                             * inv_edge_signal_pow_3 * 5.0;
```

**ÜS 0.27.** Bizde `pow(t, 4)` vardı — birden büyük üs alt ucu eziyor, kenar bandını
daraltıyor. 0.27 birden KÜÇÜK: alt uç genişliyor. Kenar sertliği şikâyetinin sayısal
karşılığı burada.

Ayrıca **kaba yoğunluk için ayrı bir ifade** var (`cloud_coarse_density`) — iki kademeli
yürüyüşün ucuz kademesi gürültüyü hiç okumuyor, profili aşındırıp kullanıyor.

**Örnekleme koordinatı zarf kalınlığına göre kaydırılıyor** `[N22 s.102]`:

```hlsl
float3 noise_sample_pos = inSamplePosition
    + float3(0.0, 0.0, (1.0 - saturate((max_height - min_height) * 0.0125)) * 40.0);
```

İnce zarflar gürültünün farklı bir diliminden okuyor — aynı gürültüyle farklı karakter.

### Zarf modelinde aydınlatma

**Doğrudan saçılma** `[N22 s.112]`:
```hlsl
float transmittance = exp(-inSummedSamples);
float long_distance_shadow_sample = SampleLongDistanceShadowMap(inSamplePosition);
float direct_scattering = transmittance * long_distance_shadow_sample;
```
Ayrı bir **uzun mesafe gölge haritası** var — bulut kütlesi hem kendi ışık ışınından
hem de o haritadan gölge alıyor.

**Ambient** `[N22 s.113]`:
```hlsl
float height_fraction = ValueRemap(inSamplePosition.z, min_height, max_height, 0.0, 1.0);
float ambient_scattering = pow(1.0 - saturate(cloud_coarse_density), 0.25) * height_fraction;
```
Gökyüzü modelindeki `pow(1 - profil, 0.5)` yerine burada `pow(1 - kabaYoğunluk, 0.25)`
ve ayrıca **yükseklikle çarpım** — kütlenin dibi ambient'ten daha az pay alıyor.
(Dünyaları Z-yukarı: `inSamplePosition.z` yükseklik.)

### Zarf modelinde ışın yürüyüşü — SABİT ADIM DEĞİL

`[N22 s.115-118]`. Boş alanı geçmek için iki teknik:

- **Sphere Tracing** — Hart, John C. 1995, *"Sphere Tracing: Simple Robust Antialiased
  Rendering of Distance-Based Implicit Surfaces"*
- **Cone Step Mapping** — Dummer, Jonathan. 2006, *"Cone Step Mapping: An Iterative
  Ray-Heightfield Intersection Algorithm"*

Yani mesafe alanı mantığıyla, örnek noktasından kütleye olan güvenli mesafe kadar
sıçranıyor. Bizim `CloudSkipMap` + genişletme + `CloudSkipCoarseMeters` zincirimiz bunun
kaba ve elle ayarlanan hâliydi; sphere tracing aynı işi ölçüden türeterek yapıyor ve
"sıçrama bulutun üstünden atladı" hatası yapısal olarak imkânsız.

**Yazarlık** `[N22 s.107-109]`: zarf Houdini'de araziye yerleştiriliyor, dağın üstüne
bölge boyanıyor, sonuç motorda dağa yaslanmış bulut. Bizim "dağa yaslanan bulut"
ihtiyacımızın birebir karşılığı.

### Zarf modelinin maliyeti — ölçülmüş

`[N22 s.125-126]` GPU profilinde "Oro clouds — high res":

| | süre |
|---|---|
| İlk hâli | **4.2 ms** |
| Optimize | **1.3 ms** |

Yani orografik (dağ) bulutları gökyüzünden çok daha pahalı: gökyüzü 0.4 ms, dağ bulutu
1.3 ms. Toplam ~1.7 ms. Bizim tek katmanımız 4 ms tutuyordu.

**Zarf NDF'i aslında YEDİ kanal** `[N22 s.129]` — s.106'da dördü gösterilmişti, tam hâli:

Cloud Min Height · Cloud Max Height · Cloud Type · Cloud Density ·
**Cloud Distance · Upper Angle · Lower Angle**

Son üçü "Locations" için: kütlenin mesafesi ve üst/alt açıları. Muhtemelen zarf
geometrisini bir yamaç boyunca eğmek/yaslamak için.

### VFX bölümü — süper hücreler

`[N22 s.134+]`. Amaçlar: gerçekçi, güçlü, tehditkâr, performanslı. Konu **supercell**
fırtınaları: örs (anvil) ve mezosiklon `[N22 s.137-138]`. Referans hem fotoğraf hem
gerçek bir sayısal simülasyon (El Reno, OK 2013 — Leigh Orf, NCSA görselleştirmesi).

Bizim "sürekli fırtına" kuşağımızın karşılığı burası olabilir ama öncelik değil.

**Süper fırtına NDF'i** `[N22 s.139]`: gökyüzünün beş kanalı + **Superstorm Mask**.

**Mezosiklon dönmesi** `[N22 s.144-148]`: gürültü, fırtına merkezi etrafında döndürülmüş
koordinattan okunuyor. Eş merkezli halkalar farklı hızlarda dönüyor
(`time_offset * ring_rotation_speed[n] + ring_skew[n]`) — tek parça dönme yerine
kesme (shear) veren bir alan. Kaynak: "M.D.R. Vortex Field", Matthew D. Roach'un
sinüs/kosinüs dönme fikri.

**Fırtına maskesi** `[N22 s.152]`:
```hlsl
float superstorm_mask = pow(saturate(1.0 - length(pos.xy - center) / radius), 0.1);
```
Üs 0.1 — merkeze çok yakınına kadar 1'de kalıp kenarda hızla düşen bir maske.

**Örs (anvil)** `[N22 s.160-161]`: iki dikey gradyanın TOPLAMI (çarpımı değil) + üstte
cirrus NDF'i ile dışa yayılan başlık.

**Fırtına merkezine göre yoğunluk ölçeği** `[N22 s.165]`:
```hlsl
density_scale   = pow(min(1.0 - (dist / radius), 0.0) + 1.0, 0.5);
summed_density += d(n) * density_scale;
transmittance   = exp(-1.0 * summed_density);
```

**Ambient ayarları konuma göre harmanlanıyor** `[N22 s.167]`: normal bulut ayarı ile
fırtına ayarı arasında, merkeze uzaklık ve yüksekliğe göre lerp.

## Şimşek ve iç parlama — bizim `LightningFlash`'imizin doğrusu

`[N22 s.170-172]`. Bulutun içindeki çakma, **ışın yürüyüşünün İÇİNDE üçüncü bir enerji
terimi** olarak veriliyor:

```hlsl
potential_energy   = pow(1.0 - (d1 / radius), 12.0);      // ışığa uzaklık
height_gradient    = (d2 / height);                        // bulut tabanından yükseklik
pseudo_attenuation = (1.0 - saturate(fine_density * 5.0)); // yoğun yer daha az geçirir

glow_energy  = potential_energy * height_gradient * pseudo_attenuation;
light_energy = direct_scattering + ambient_scattering + glow_energy;
```

**Bizde çakma bindirme geçişinde** (`VolumetricClouds.shader`) `_LightningFlash * clouds.a`
olarak ekleniyordu — yani kütlenin İÇİNDEN değil, üstünden. Gerekçemiz "ışın yürüyüşü
kareye yayılı, parlama blok blok titrer"di. `[N22]` yürüyüşün içine koyabiliyor çünkü
zamansal yeniden yansıtması doğru kurulmuş (bkz. soru 8).

**Çakma dizisi** `[N22 s.175, 178]`: iki tür var — **Intra-Cloud** (bulut içi) ve
**Ground Discharge** (yere boşalma). Sıra:

```
Intra-Cloud → rastgele gecikme → ... (tekrar, şiddet artarak) → Intra-Cloud + Ground Discharge
```

Yani yere inen çakma tek başına gelmiyor; önce bulut içinde birkaç kez çakıp şiddetleniyor.
Kaynak: Martin A. Uman, *Lightning*.

## Sönüm katsayısı — hiç ayar olarak açmadığımız şey

`[N22 s.164]` üç render yan yana: `T = e^(−5d)`, `e^(−10d)`, `e^(−20d)`. Katsayı
büyüdükçe bulut opaklaşıyor ve kontrast artıyor. Bizde `exp(-density * step)` sabitti,
katsayı yoktu — `_DensityScale` dolaylı olarak aynı işi yapıyordu ama fiziksel karşılığı
belirsizdi.

## Üç bulut sistemi TEK IŞINDA — ve hava perspektifi

`[N22 s.187]` bir ışın boyunca sırayla: **Near Orographic → Far Orographic →
Tropospheric/Superstorm**. Üç ayrı model aynı yürüyüşte birleşiyor.

**Hava perspektifi OPAKLIKLA AĞIRLIKLANMIŞ ortalama mesafeden** `[N22 s.188]`:

```hlsl
while (...) { distance_sum += d[n] * sample_opacity; }

float weighted_sum = distance_sum / opacity_sum;
cloud_color = lerp(cloud_color, atmospherics_color, weighted_sum);
```

Bizde `firstHit` — ışının ilk değdiği yer — kullanılıyordu. Ağırlıklı ortalama daha
doğru: ince bir tülün arkasındaki kalın kütle mesafeyi kendine çeker.

**Şimşek maskesi AYRI VE KARARLI** `[N22 s.180]`: şimşek sistemi konumu üretip animasyonu
tetikliyor; **renderer o konumdan kararlı bir maske üretiyor**, çakma şiddeti maskeyle
çarpılıyor. Maske kare kare değişmiyor — titremenin çözümü bu.

## Hacim Veri Alanları (NVDF) — ileri adım

`[N22 s.193-198]`. Prosedürel alan yerine pişmiş hacim: iki 3B doku — **Cloud Density**
ve **Cloud Distance**. İkincisi mesafe alanı; "Source-Agnostic Distance Step Mapping"
ile boş alan sıçranarak geçiliyor. Kaynak Houdini'den gelen gerçek bulut modeli olabiliyor
(kök: Schneider'in 2011 *Rio* çalışması, Blue Sky Studios). Nubis³'ün voksel mimarisine
giden yol. Bizim için şimdilik kapsam dışı.

`[N22 s.202]` **Source-Agnostic Distance Step Mapping** şeması: ışın boş alanda mesafe
alanının söylediği kadar büyük sıçrıyor, kütleye girince örnekler sıklaşıyor. Kaynağın
prosedürel mi pişmiş mi olduğu fark etmiyor — "source-agnostic" o demek.

**Kaynakça** `[N22 s.206]`: Hart 1995 (sphere tracing), Dummer 2006 (cone step mapping),
Beer 1852, Henyey-Greenstein 1941, Uman *Lightning* 1969, Hamblyn *The Invention of
Clouds*, Hargrove *The Man Who Caught The Storm*.

## `[H18]` şekil ve yürüyüş — tam formüller

Basılı sayfa numarası kullanıyorum (PDF sayfası = basılı + 12).

### Hava haritası: İKİ kapsama kanalı `[H18 s.11]`

```
WM_c = max(w_c0, SAT(g_c − 0.5) × w_c1 × 2)
```

- **R** = seyrek kapsama haritası (elle çizilmiş bulut yerleşimi)
- **G** = yoğun kapsama haritası (gürültü, gök kapanırken devreye girer)
- **B** = azami bulut yüksekliği · **A** = bulut yoğunluğu

Küresel kapsama sürgüsü **iki harita arasında geçiş yapıyor**: düşükken yalnız R, 0.5'i
geçince G devreye girip göğü dolduruyor. Bizim "sürgü haritayı ölçekliyor, sıfır olan
yer hiç dolmuyor" sorunumuzun bir başka çözümü — ikinci bir harita.

### Katman: taban 400 m, tepe 1000 m `[H18 s.10]`

Bilerek alçak ve küçük: *"bulutlar daha küçük yapıldı ve yere yaklaştırıldı ki dünyada
dolaşırken daha çeşitli bulutlar görülsün."* Sanatsal karar olduğu açıkça yazılmış.

### Yükseklik fonksiyonları — İKİ TANE `[H18 s.12-13]`

**Şekil değiştiren** (kenarları yuvarlar):
```
SR_b = SAT(R(p_h, 0, 0.07, 0, 1))            // tabana doğru yuvarla
SR_t = SAT(R(p_h, w_h × 0.2, w_h, 1, 0))     // tepeye doğru; w_h haritanın B kanalı
SA   = SR_b × SR_t
```

**Yoğunluk değiştiren** (tabanı tüylü, tepeyi belirgin yapar):
```
DR_b = p_h × SAT(R(p_h, 0, 0.15, 0, 1))
DR_t = SAT(R(p_h, 0.9, 1.0, 1, 0))
DA   = g_d × DR_b × DR_t × w_d × 2
```

İkisi ayrı: biri **şekli**, öbürü **yoğunluğu** değiştiriyor. Bizde tek zarf vardı.

### Yoğunluk zinciri `[H18 s.14-16]`

Şekil gürültüsü 128³ RGBA (R alçak frekans Perlin-Worley, GBA artan Worley):
```
SN_sample = R(sn_r, (sn_g×0.625 + sn_b×0.25 + sn_a×0.125) − 1, 1, 0, 1)
```

Detay gürültüsü 32³ RGB (Worley):
```
DN_fbm = dn_r×0.625 + dn_g×0.25 + dn_b×0.125
DN_mod = 0.35 × e^(−g_c × 0.75) × L_i(DN_fbm, 1 − DN_fbm, SAT(p_h × 5))
```

**İki şey burada:**
- `e^(−g_c×0.75)` — **detayın etkisi küresel kapsamayla AZALIYOR.** Kapalı havada ince
  yapı gereksiz; bizde sabitti.
- `L_i(DN_fbm, 1−DN_fbm, SAT(p_h×5))` — **tabanda ters Worley, tepede normal**, geçiş
  ilk %20'de. Bizde de vardı ama sabit yükseklikteydi.

Son yoğunluk:
```
SN_nd = SAT(R(SN_sample × SA, 1 − g_c × WM_c, 1, 0, 1))
d     = SAT(R(SN_nd, DN_mod, 1, 0, 1)) × DA
```

**Sıra kritik:** şekil gürültüsü × şekil-yükseklik → kapsamayla remap → detayla remap →
**en son** yoğunluk-yükseklikle çarp. Bizde bu sıra karışıktı.

### Örs `[H18 s.17]`

```
SA_anvil = (SA)^SAT(R(p_h, 0.65, 0.95, 1, 1 − a_a × c_g))
DA_anvil = DA × L_i(1, SAT(R(√p_h, 0.4, 0.95, 1, 0.2)), a_a)
```
Örs, şekil fonksiyonunu **üs olarak** değiştiriyor. Ve örs eklenince yoğunluk modeli de
değiştirilmek zorunda — yoksa tepe fazla yoğun kalıyor (s.17 şekil 12b bunu gösteriyor).

### Maliyet modeli `[H18 s.18]`

```
t_tot = p × (n × t_d0 + n × s_n × t_d1)
```
p piksel, n görüş adımı, s_n güneş adımı. Optimizasyon bu üç değişkenden birine dokunur.

### Adım boyu ve optimizasyonlar `[H18 s.19-21]`

```
step_new = step_orig + dist_start × inc_rate
```
**Başlangıç mesafesine** doğrusal — N22'nin `3.0 + 60·d/16384`'üyle aynı aile.

Diğerleri:
1. **Bulut bulunana kadar uzun adım**, bulununca **bir adım geri** ve kısa adıma geç.
   Bulut geçilince tekrar uzun adım — ama **asgari kısa-adım mesafesi** dolmadan geri
   dönülmüyor (histerezis). Bizim `emptyRun >= 6`'mızın karşılığı.
2. **Uzun adımda yalnız TABAN şekli örnekleniyor, detay değil** — çünkü nihai şekil her
   zaman taban şekil hacminin İÇİNDE kalır `[H18 s.20-21 şekil 18]`. Bizim
   "cheap ≥ full garantisi"nin temiz ifadesi.
3. **Tam opaklığa varınca dur.**
4. **Kapsama düşükken adım KISALIR, yoğunluk düşükken adım UZAR.** İkisi de deneysel,
   §4.2'de test ediliyor. Bizde adım yalnız mesafeye bağlıydı — bu iki bağ hiç aklımıza
   gelmemişti.

### Güneş adımları `[H18 s.22]`

```
step_sun = 0.5 × (ch_stop − ch_start) / s_n        // s_n = 4
miplevel = Integer(i × 0.5)                        // i: 0..s_n
```

- Güneş yürüyüşünün **toplam mesafesi her zaman katman yüksekliğinin yarısı**, s_n ne
  olursa olsun. Bizde `lightProbeMeters` bağımsız bir sayıydı.
- **Mip seviyesi güneş adımı ilerledikçe artıyor**: ilk örnek mip 0, dördüncü mip 2.
  Uzaktaki gölge örneği daha kaba dokudan okunuyor.
- Örnek bulutun içinde değilse **güneş yürüyüşü hiç başlatılmıyor**.

### Yeniden yansıtma `[H18 s.22-25]`

- Her kare **4×4 bloğun 1 pikseli** işleniyor (1/16).
- Sıra **naif değil, çapraz desen** — naif sıra gözle seçilebilen bir örüntü bırakıyor.
- Hareket vektörleri ayrı bir render-target'ta (kırmızı x, yeşil y ekran uzayı).
- İşlenmeyen pikseller için hareket vektörüyle **önceki karedeki karşılık** bulunuyor.
- Sonuç: render süresi **1/10'a** iniyor (1/16 değil — yansıtmanın kendi maliyeti var).

**Bilinen sorunlar — birebir bizim belirtilerimiz `[H18 s.25]`:**

> *"Bulutların içinden geçerken göreli hareket çok büyük oluyor ve görsel olarak
> hoş olmayan bir sonuç veriyor. Bu, yeniden yansıtmayı kapatıp yalnız yeni ışın
> yürütülen pikselleri kullanarak çözülebilir; hayalet gider ama **daha piksellenmiş**
> bir sonuç gelir."*

Bizim `downsample = 4` + harmansız birleştirme kararımız tam olarak bu ödünleşmenin
kötü tarafında duruyordu. Ayrıca:
- Bulutlar cismin arkasında bile çizilmek zorunda (bir sonraki karede o piksel lazım).
- Hareket vektörü ekran dışını gösterirse yeni örnek kullanılıyor → hızlı dönüşte
  kenarlar piksellenir.

### Mavi gürültü `[H18 s.26]`

```
dist_start += (bn − 0.5) × 2 × step_new
```
Yürüyüş başlangıcını ±1 adım kaydırıyor. **Bantlaşmanın çözümü bu.** Bizde Bayer 4×4
kullanılıyordu; mavi gürültü ekran uzayında döşeniyor ve daha iyi dağılıyor. Bedeli:
görüntü belirgin şekilde daha gürültülü, hafif bulanıklaştırma gerekiyor.

### Aydınlatma `[H18 s.27-28]`

```
E(b, d_s) = e^(−b × d_s)                                    // Beer, b sanatsal terim
HG(θ, g)  = (1/4π)(1 − g²) / [1 + g² − 2g·cos θ]^(3/2)
IS_extra(θ) = cs_i × SAT(θ)^cs_e                            // güneş çevresine ek yoğunluk
IOS(θ) = L_i(max(HG(θ, in_s), IS_extra(θ)), HG(θ, −out_s), ivo)
```

**İçe ve dışa saçılma ayrı iki HG**, aralarında `ivo` ile lerp. Bizde tek faz vardı.
`b` açıkça "gerçekçi yoğunluktan kopuk, dengelemek için sanatsal terim" diye tanımlanmış.

### `[H18]` fiziksel olmayan aydınlatma düzeltmeleri `[H18 s.29-31]`

Uzun güneş adımları gerçek fizikle düzelmeyen üç sorun çıkarıyor; üçünün de çözümü
açıkça "fiziksel değil" diye etiketlenmiş.

**1. Dışa saçılma ambient'i** — güneş perspektifinden bakınca bulut kontursuz büyük beyaz
lekeye dönüyor:
```
OS_ambient = 1 − SAT(os_a × d^(R(p_h, 0.3, 0.9, 0.5, 1.0))) × (SAT(R(p_h, 0, 0.3, 0.8, 1.0)^0.8))
```

**2. Sönüm kelepçesi** — Beer yasası uzun güneş adımlarıyla aydınlık/karanlık arasında
aşırı kontrast veriyor:
```
A_clamp = max(E(b, d_s), E(b, a_c))          // b = 12, a_c = 0.2
```

**3. Yoğunluğa bağlı ambient** — kelepçe bu sefer bulut altını **düz gri ve detaysız**
bırakıyor:
```
A_alter = max(d × a_min, A_clamp)            // a_min = 0.2
```

**Bizim "bulutun ortası keskin kararıyor, iki tonlu görünüyor" belirtimizin karşılığı
2 ve 3.** Kelepçe koyu ucu sınırlıyor, yoğunluk-ambient'i kelepçelenmiş bölgeye detay
geri veriyor. Bizde ikisi de yoktu.

**Nihai aydınlatma** `[H18 s.32]`:
```
L_final(θ) = A_alter × IOS(θ) × OS_ambient
```

**Dokuz ayar, anlamlarıyla listelenmiş** `[H18 s.32]` — panel için hazır liste:
Beer terimi · içe saçılma · dışa saçılma · içe/dışa oranı (`ivo`) · ek gümüş kenar
şiddeti · ek gümüş kenar üssü · ambient dışa saçılma · **sönüm kelepçe değeri** ·
**asgari sönüm ambient'i**.

### Render hattı `[H18 s.33-34]`

Üç adım:
1. Bulut katmanını **çeyrek çözünürlükte** yürü. **Yürürken derinlik alınıp o adımdaki
   alfa ile ağırlıklanıyor** — hareket vektörleri o derinlikten hesaplanıyor, ayrıca
   3. adımda derinlik testi için saklanıyor. (N22 s.188'deki opaklık-ağırlıklı mesafenin
   aynısı.)
2. Hareket vektörleriyle önceki kareyi yeniden yansıt, karenin 1/16'sını yeni veriyle
   güncelle.
3. Yansıtılmış kareyi derinlik haritasıyla birlikte ekrana çiz.

**Gölge ayrı geçişte**: her fragman için güneşe doğru ışın yürüyüşü, **çeyrek çözünürlük
ve büyük adımlarla**. Bizim `CloudShadowAt`'imizin karşılığı — ama biz tek örnek
alıyorduk, onlar yürüyor.

### Hareket `[H18 s.34]`

Üç doku ayrı ayrı ötelenir:
- **hava haritası** → bulutların tamamı yer değiştirir
- **şekil gürültüsü** → bulutlar şekil değiştirir
- **detay gürültüsü** → türbülans, şekil çok değişmeden

Üçü yaklaşık aynı yönde ötelenirse bulut hem gider hem değişir. Bizim `_Evolution` +
`_CloudWind` ikilisinin doğru hâli bu — biz haritayı rüzgârın %72'siyle kaydırıyorduk,
onlar üçünü ayrı ayrı ele alıyor.

## `[H18]` ÖLÇÜLMÜŞ DENEYLER — sayılar

Hepsi GTX 980 Ti, tam ekran render süresi.

### Güneş adımı sayısı `[H18 s.36-39, Tablo 1]`

| `s_n` | süre | görsel |
|---|---|---|
| 100 (referans) | 60.00 ms | pürüzsüz aydınlatma kenarları |
| 1 | 1.90 ms | kaba, sert aydınlatma geçişleri |
| 2 | 2.80 ms | çok daha iyi, ince detay eksik |
| 3 | 3.35 ms | ince detay görünmeye başlıyor |
| **4** | **3.85 ms** | **azalan getiri başlıyor — SEÇİLEN** |
| 5 | 4.40 ms | 4'e göre çok küçük iyileşme |

Bizim `lightSteps = 4` doğru değermiş — ama tesadüfen.

### Adım büyüme oranı `[H18 s.40-43, Tablo 2]`

`step_new = step_orig + dist_start × inc_rate`, `step_orig = 14`

| `inc_rate` | süre | görsel |
|---|---|---|
| kullanılmıyor | 15.00 ms | referans, sabit adım |
| 0.1 | 1.30 ms | belirgin gürültü ve bantlaşma |
| 0.0667 | 1.70 ms | azaldı ama hâlâ görünür |
| 0.05 | 2.10 ms | çoğu gitti, tam ekranda hâlâ seçiliyor |
| **0.04** | **2.45 ms** | **tam ekranda bile görünmüyor — SEÇİLEN** |

**Sabit adım 15 ms, 0.04 ile 2.45 ms — altı kat.** Ve görsel fark küçük.

### Kapsama düşükken adımı KISALT `[H18 s.44-47, Tablo 3]`

```
step_new = step_orig × max(mult_min, min(1, R(g_c, 0.2, 0.7, mult_min, 1)))
```

Sorun: ufka doğru adım uzayınca **uzaktaki küçük bulutlar büyüklerden daha gürültülü**
görünüyor — adım bulutun yarıçapından büyük olunca bazı pikseller bulutu tamamen atlıyor.

| `mult_min` | süre | görsel |
|---|---|---|
| kullanılmıyor | 2.00 ms | kapsama düşükken gürültülü |
| 0.1 | 8.00 ms | referans, gürültü yok |
| 0.3 | 3.30 ms | referansa göre görünür fark yok |
| **0.4** | — | **en tutarlı performans — SEÇİLEN** |
| 0.5 | 2.30 ms | görünür bozulma |

### Yoğunluk düşükken adımı UZAT `[H18 s.48]`

```
step_new = step_orig × 1 / max(g_d, div_min)^0.8
```

Gerekçe: yoğunluk düşükken ışın tam opaklığa varamıyor ve **bütün aralığı yürüyor**.
`g_d = 0.1`'de süre 5 ms — `g_d = 1`'dekinin iki katı. Her örneğin katkısı azaldığı için
adım uzatılabilir.

### `[H18]` yoğunluk düşükken adımı uzat — tablo `[H18 s.51, Tablo 4]`

```
step_new = step_orig × 1 / max(g_d, div_min)^0.8
```

| `div_min` | süre | görsel |
|---|---|---|
| kullanılmıyor | 5.00 ms | `g_d = 1`'dekinin iki katı |
| 0.1 | 0.90 ms | aşırı gürültü |
| 0.2 | 1.50 ms | hâlâ çok belirgin |
| 0.3 | 2.00 ms | biraz belirgin |
| **0.4** | **2.50 ms** | **gürültünün çoğu gitti — SEÇİLEN** |

### Bulut TİPLERİ yalnız hava haritası + iki sürgüyle `[H18 s.53-57]`

Dört tip de aynı sistemden, **yalnız hava haritası ve iki küresel sayı** değişerek:

| tip | değişiklik | `g_c` | `g_d` |
|---|---|---|---|
| Kümülüs | yok (temel harita) | 0.6 | 1.0 |
| Stratokümülüs | yok | 0.8 | 0.5 |
| Stratus | yükseklik kanalı merkezde koyulaştırılmış (ince bulut) | 1.0 | 0.35 |
| Kalın stratus | iki kapsama kanalı da beyaz (tam) | 1.0 | 0.2 |
| Kümülonimbus | seyrek haritada merkeze büyük noktalar, yoğun harita çevrede kısılmış | 1.0 | 1.0 |

**Yoğunluk sürgüsü tipi belirliyor**: kapsama yüksek + yoğunluk düşük = stratus levhası;
ikisi de yüksek = kümülonimbus. Bizde bu ilişki hiç kurulmamıştı.

### Aydınlatma varsayılanları — TAM LİSTE `[H18 s.58]`

| ayar | değer |
|---|---|
| Beer terimi `b` | **6** |
| İçe saçılma `in_s` | 0.2 |
| Dışa saçılma `out_s` | 0.1 |
| İçe/dışa oranı `ivo` | 0.5 |
| Ek gümüş kenar şiddeti `cs_i` | 2.5 |
| Ek gümüş kenar üssü `cs_e` | 2 |
| Ambient dışa saçılma `os_a` | 0.9 |
| Sönüm kelepçesi `a_c` | 0.2 |
| Asgari sönüm ambient `a_min` | 0.2 |

**Batım/şafak** `[H18 s.59]`: yalnız üç sayı değişiyor — `in_s` 0.2→**0.5**,
`cs_i` 2.5→**8**, `cs_e` 2→**15**. Gerisi aynı.

**Gece** `[H18 s.60]`: aydınlatma ayarlarında **hiçbir değişiklik yok**; yalnız ışık
kaynağı ay oluyor (daha mavi, düşük şiddet).

Bizde gece için ayrı renk zinciri, `duskCore`, `duskWide`, `duskOverlap` gibi terimler
vardı. Burada üç sayı yetiyor.

### Performans — tip başına `[H18 s.61, Tablo 5]`

| tip | 1920×1080 | 960×540 |
|---|---|---|
| Kümülüs | 2.05 ms | 0.85 ms |
| Stratokümülüs | 2.20 ms | 0.90 ms |
| Stratus | 1.90 ms | 0.80 ms |
| Kümülonimbus | 2.00 ms | 0.85 ms |

Her ikisinde de yeniden yansıtma açık (efektif çeyrek çözünürlük).

### `[H18]` neyi YAPMADI `[H18 s.63-65]`

- **Curl gürültüsü kullanılmamış** — performans için. Gerekçe: Schneider'in bulutları
  yerinde hareket ediyor, curl ona hareket katıyor; bu uygulamada bulutlar rüzgâr yönünde
  gittiği için curl'ün önemi az.
- **Bulutun farklı parçaları farklı hızda hareket edemiyor** — dönen fırtına zor.
- **Bulutlar bir katmana bağlı** — hortum yere kadar inemiyor.
- **Orta ve yüksek irtifa bulutları yok** — yalnız alçak katman. Yüksek irtifa için
  "görüntü tabanlı bir çözüm üstte" öneriliyor.
- **Hava haritalarının hepsi ELLE ÇİZİLMİŞ.** Gelecek iş olarak bir üretici öneriliyor.
- **Ayrı bir hava sistemine bağlanmak GELECEK İŞ olarak listelenmiş** — yani onlar da
  bulutu önce bağımsız kurup sonra bağlamayı planlamış. Bizim v1 kuralımızı doğruluyor.

## `[H18]` EK B — GERÇEK KOD

Tez HLSL, Stingray motoru. Aşağıdakiler birebir kopyadır.

### Şekil değiştiren yükseklik fonksiyonu `[H18 Ek B.2]`

```hlsl
float HeightAlter(float percent_height, float4 weather_map)
{
    // Round bottom a bit
    float ret_val = saturate(ReMap(percent_height, 0.0, 0.07, 0.0, 1.0));

    // Round top a lot
    float stop_height = saturate(weather_map.b + 0.12);
    ret_val *= saturate(ReMap(percent_height, stop_height * 0.2, stop_height, 1.0, 0.0));

    // Apply anvil (cumulonimbus / "giant storm" clouds)
    ret_val = pow(ret_val, saturate(ReMap(percent_height, 0.65, 0.95, 1.0,
                                          (1 - cloud_anvil_amount * global_coverage))));
    return ret_val;
}
```

### Yoğunluk değiştiren yükseklik fonksiyonu `[H18 Ek B.3]`

```hlsl
float DensityAlter(float percent_height, float4 weather_map)
{
    // Have density be generally increasing over height
    float ret_val = percent_height;

    // Reduce density at base
    ret_val *= saturate(ReMap(percent_height, 0.0, 0.2, 0.0, 1.0));

    // Apply weather_map density
    ret_val *= weather_map.a * 2;

    // Reduce density for the anvil
    ret_val *= lerp(1, saturate(ReMap(pow(percent_height, 0.5), 0.4, 0.95, 1.0, 0.2)),
                    cloud_anvil_amount);

    // Reduce density at top to make better transition
    ret_val *= saturate(ReMap(percent_height, 0.9, 1.0, 1.0, 0.0));

    return ret_val;
}
```

### Taban gürültüsü `[H18 Ek B.4]`

```hlsl
float shape_noise = shape_sample.g * 0.625 + shape_sample.b * 0.25 + shape_sample.a * 0.125;
shape_noise = -(1 - shape_noise);
shape_noise = ReMap(shape_sample.r, shape_noise, 1.0, 0.0, 1.0);
```

### Detay gürültüsü `[H18 Ek B.5]`

```hlsl
float detail = (detail_noise.r) * 0.625 + (detail_noise.g) * 0.25 + (detail_noise.b) * 0.125; // FBM

// For low altitude regions the detail noise is used (inverted) to instead of creating
// round shapes, create more wispy shapes. Transitions to round shapes over altitude.
float detail_modifier = lerp(detail, 1 - detail, saturate(percent_height * 5.0));

// Reduce the amount of detail noise being "subtracted" with the global_coverage.
detail_modifier *= 0.35 * exp(-global_coverage * 0.75);

// Carve away more from the shape_noise using detail_noise
final_density = saturate(ReMap(shape_noise, detail_modifier, 1.0, 0.0, 1.0));
```

### Aydınlatma — TAMAMI `[H18 Ek B.6]`

```hlsl
float HG(float cos_angle, float g)
{
    float g2 = g * g;
    float val = ((1.0 - g2) / pow(1.0 + g2 - 2.0 * g * cos_angle, 1.5)) / (4 * 3.1415);
    return val;
}

float InOutScatter(float cos_angle)
{
    float first_hg  = HG(cos_angle, cloud_inscatter);
    float second_hg = cloud_silver_intensity * pow(saturate(cos_angle), cloud_silver_exponent);

    float in_scatter_hg  = max(first_hg, second_hg);
    float out_scatter_hg = HG(cos_angle, -cloud_outscatter);

    return lerp(in_scatter_hg, out_scatter_hg, cloud_in_vs_outscatter);
}

float Attenuation(float density_to_sun, float cos_angle)
{
    float prim = exp(-cloud_beer * density_to_sun);
    float scnd = exp(-cloud_beer * cloud_attenuation_clampval) * 0.7;

    // reduce clamping while facing the sun
    float checkval = ReMap(cos_angle, 0.0, 1.0, scnd, scnd * 0.5);

    return max(checkval, prim);
}

float OutScatterAmbient(float density, float percent_height)
{
    float depth    = cloud_outscatter_ambient * pow(density, ReMap(percent_height, 0.3, 0.9, 0.5, 1.0));
    float vertical = pow(saturate(ReMap(percent_height, 0.0, 0.3, 0.8, 1.0)), 0.8);

    float out_scatter = depth * vertical;
    out_scatter = 1.0 - saturate(out_scatter);

    return out_scatter;
}

float3 CalculateLight(float density, float density_to_sun, float cos_angle,
                      float percent_height, float bluenoise, float dist_along_ray)
{
    float attenuation_prob    = Attenuation(density_to_sun, cos_angle);
    float ambient_out_scatter = OutScatterAmbient(density, percent_height);

    // Can be calculated once for each march but gave no/tiny perf improvements.
    const float sun_highlight = InOutScatter(cos_angle);

    float attenuation = attenuation_prob * sun_highlight * ambient_out_scatter;

    // Ambient min (dist_along_ray used so that far away regions (huge steps)
    // arent calculated (wrongly))
    attenuation = max(density * cloud_ambient_minimum
                      * (1 - pow(saturate(dist_along_ray / 4000), 2)), attenuation);

    // combat banding a bit more
    attenuation += bluenoise * 0.003;

    float3 ret_color = attenuation * sun_color;
    return ret_color;
}
```

**Koddan çıkan üç ek ayrıntı** (metinde yoktu):

1. `Attenuation` içinde **kelepçe güneşe bakarken gevşiyor** (`checkval` `cos_angle` ile
   `scnd`'den `scnd*0.5`'e iniyor). Yani kelepçe her yerde aynı değil.
2. `CalculateLight` sonunda **asgari ambient mesafeyle söndürülüyor**:
   `(1 - pow(saturate(dist_along_ray/4000), 2))`. Gerekçe yorumda: uzak bölgelerde adım
   devasa olduğu için o hesap **yanlış** çıkıyor. 4000 m'de tamamen kapanıyor.
3. **Mavi gürültü aydınlatmaya da ekleniyor**: `attenuation += bluenoise * 0.003`.
   Bantlaşma yalnız yürüyüş başlangıcında değil, ışıkta da kırılıyor.

---

## Okuma defteri

Kesintisiz olmalı. Boşluk = okunmamış sayfa.

| makale | toplam | okunan | eksik |
|---|---|---|---|
| `[N15]` nubis-2015 | **99** | s.18–87 | **s.1–17, s.88–99** |
| `[N17]` nubis-2017 | **108** | — | s.1–108 |
| `[N22]` nubis-2022 | **207** | **s.1–207 TAMAM** | — |
| `[H18]` haggstrom-2018 | **93 PDF / 81 basılı** | **PDF s.1–93 TAMAM** | — |

**Toplam ~514 sayfa, okunan 80.** Kalan 434.

`[H18]` **81 BASILI sayfa**; PDF sayfası = basılı + 12, yani PDF ~93 sayfa. Basılı 68
(kaynakça sonu) = PDF 80. **Ekler (A parametreler, B KOD) PDF 81'den sonra.**

`[N15]`'in atlanan kısımları da okunacak — s.1–17 giriş, s.88+ optimizasyon bölümü.
Bugün ortadan girilip ortada bırakılmıştı.


---

## `[N22]` bitti — soruların durumu

| # | soru | durum |
|---|---|---|
| 1 | Bulutun eni/boyu | **cevaplandı** — s.20, 106, 129 |
| 2 | Gürültü periyodu / katman oranı | **kısmi** — adım boyu cevabı geldi (s.39), periyot oranı H18'de aranacak |
| 3 | Kapsama sürgüsü | **cevaplandı** — s.30 |
| 4 | Gradyan çarpar mı | **cevaplandı** — s.28, 34, 97 |
| 5 | Kenar yumuşaklığı | **cevaplandı** — s.34 (bölme yok), s.105 (üs 0.27) |
| 6 | Adım ve mip | **cevaplandı** — s.39, 183 |
| 7 | Buluta girmek | **kısmi** — Environments bölümü yakın alanı anlatıyor ama "kamera kütlenin İÇİNDE" ayrı bir başlık değil |
| 8 | Temporal | **cevaplandı** — s.152, 157, 158 |
| 9 | Aydınlatma sayıları | **cevaplandı** — s.41-48, 54, 59, 183 |
| 10 | Yer gölgesi | **kısmi** — uzun mesafe gölge haritası var (s.112) ama üretimi anlatılmıyor |
| 11 | Harita kanalları | **cevaplandı** — s.20, 75, 106, 129, 139 |
| 12 | Optimizasyonlar | **cevaplandı** — s.81, 82, 90, 125, 183, 184 |

Kalan üç boşluk (2, 7, 10) `[H18]` ve `[N17]`'de aranacak.
