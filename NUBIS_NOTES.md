# Nubis okuma notları

Kaynaklar (`C:\Users\musta\Downloads\nubis`):
`[H18]` haggstrom-2018 · `[N15]` nubis-2015-hzd · `[N17]` nubis-2017-decima · `[N22]` nubis-2022-evolved

**Bu dosya makale özeti değil, SORU–CEVAP.** Sorular okumadan önce yazıldı ve hepsi
2026-08-14'te ekranda görülmüş bir belirtiden geliyor. Cevap yazarken kaynak sayfa
zorunlu: `[N22 s.34]`. Kaynağı olmayan cümle nottan sayılmaz, tahmindir.

Okuma **ilerledikçe** doldurulur, sonunda değil.

## Hiçbir şey kaçmasın diye: okuma kuralları

1. **15'er sayfa, sırayla, atlama yok.** "Bu bize lazım değil" diye sayfa geçilmez.
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

> *(cevap)*

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

> *(cevap)*

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

*(Ayrıntısı ve yeniden yansıtma sonraki sayfalarda.)*

---

## Sorulmamış bulgular

Yukarıdaki on iki sorunun hiçbirine girmeyen ama önemli görünen her şey. Soru listesi
bizim bildiğimiz eksiklerden yapıldı; bilmediklerimiz burada birikir. Kaynak zorunlu.

**Katman ikiye bölünmüş** `[N22 s.16]`: alçak **Stratus alt-katmanı** ve yüksek
**Cirrus alt-katmanı**. Kamera stratus alt-katmanının içinden geçiyor. Bizdeki
"hacimsel + üstte 2B" ayrımının 2022'deki karşılığı bu.

**NDF alanı 16 km × 16 km** `[N22 s.19]`. Bizim hava haritamız 48 km'ydi. Onlarınki
üç kat küçük — yani tekrar daha sık ama görüş menzili de dar.

**Katman kotları 256 m – 2048 m** `[N22 s.20]`, insan figürüyle ölçeklenmiş. Kalınlık
1792 m. 2015'te 1500–4000 m'ydi; alçalmış ve incelmiş.

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

## Okuma defteri

Kesintisiz olmalı. Boşluk = okunmamış sayfa.

| makale | toplam | okunan | eksik |
|---|---|---|---|
| `[N15]` nubis-2015 | **99** | s.18–87 | **s.1–17, s.88–99** |
| `[N17]` nubis-2017 | **108** | — | s.1–108 |
| `[N22]` nubis-2022 | **207** | s.1–82 | s.83–207 |
| `[H18]` haggstrom-2018 | **~100** | — | s.1–100 |

**Toplam ~514 sayfa, okunan 80.** Kalan 434.

`[H18]`'in sayısı kesin değil (PDF nesne akışları sıkıştırılmış, ham sayım çalışmadı;
linearization ipucu `/N 100` diyor). Okuma sırasında son sayfaya varılınca kesinleşir.

`[N15]`'in atlanan kısımları da okunacak — s.1–17 giriş, s.88+ optimizasyon bölümü.
Bugün ortadan girilip ortada bırakılmıştı.
