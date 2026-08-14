# Bulut sistemi — uçtan uca analiz ve yapılacaklar

Tarih: 2026-08-14. Kapsam: `CloudCommon.hlsl` (1596 satır), `VolumetricClouds.shader`,
`VolumetricCloudsFeature.cs`, `AtmosphereController.cs`, `CloudWeatherMapGenerator.cs`.

Belge üç bölüm: **ne yapıyoruz** (yöntem denetimi), **her belirtinin kök nedeni**
(kodda satır satır), **yapılacaklar** (sıralı, maliyetli, onay bekliyor).

---

## 0. Özet — bir cümlelik cevaplar

| Sorun | Kök neden | Çözüm | Maliyet |
|---|---|---|---|
| Soğan halkaları | Faz rastgeleliği ışının BAŞINDA (14 m), kabuklar bulut içinde (40-165 m adım) doğuyor | Rastgeleliği buluta GİRİŞTE ve o anki adım boyuna göre uygula | Bedava |
| Tekrar eden şekiller | Ana şekil dokusunun periyodu 9 km, görüş 28 km → ufka kadar 3 tekrar | İkinci okumayı döndürerek karıştır (anti-tiling) | ~%3 |
| Silindirimsi bulutlar | `ceiling01 *= 0.30 + 0.85·_Coverage` — tavan kapsamayla büyüyor, taban büyümüyor | Tavanı yatay büyümeyle aynı eğriden sür | Bedava |
| Genel FPS 180→100 | Yansıma haritası her karede pişiyordu (düzeltildi) + SSAO + 4096 gölge atlası | Ölçüm gerekiyor: üçünü tek tek kapat | — |
| Bulut içinde FPS 40 | Kaba kademe devre dışı, ışık sondası her adımda, erken çıkış eşiği çok düşük | Üç ayarın üçü de değişecek | Kazanç |
| Yağış 1'de bulut yok | Peçe integrali doğru ama tipide fizik zaten öyle | Peçeye taban koy, tamamen yutmasın | Bedava |
| SSAO pahalı | Tam çözünürlükte çalışıyor | `Downsample` aç, yarım çözünürlük | %50 kazanç |

**Yeniden yazmalı mıyız: HAYIR.** Mimari 2017 Nubis hattında ve doğru kurulmuş; hatalar
mimaride değil, üç-dört sayıda ve bir faz hesabında. Aşağıda gerekçesi.

---

## 1. Yöntem denetimi — doğru şeyi mi yapıyoruz

### 1.1 Mimari

```
Kare N:  ışın yürüyüşü  →  1/4 çözünürlük (downsample = 4)
         her kare bloğun 1 hücresi  →  16 karede bir piksel gerçekten hesaplanıyor
Zamansal çözüm:  taze piksel kendi örneğini alır, kalanlar geçmişten
                 komşuluk kelepçesi (min/max) ile
Bindirme:  tam çözünürlükte, mesafeyle açılan çadır filtresi
```

Bu, [jpgrenier'in anlattığı şemanın](https://www.jpgrenier.org/clouds.html) aynısı:
"full frame is reconstructed over 16 frames with one-sixteenth resolution per frame".
Yani seçilen yol sektör standardı ve doğru.

**Ama bizde kritik bir fark var:** o şemada geçmiş ile taze örnek **harmanlanıyor**
(16. kare ~%75 ağırlıkla). Bizde harman yok — `VolumetricClouds.shader:151`:

> *"Harman yok. Taze olmayan pikselin `current`'ı kendi örneği değil, bloğun tek ışın
> yürüyüşü — 16 piksel için aynı değer. Ona doğru çekmek çözmeye çalıştığımız alt-piksel
> ayrıntısını geri siler."*

Karar gerekçeli ve doğru. **Sonucu şu:** zamansal birikim gürültüyü ortalayamaz. Yani
"jitter koy, TAA halleder" stratejisi bizde çalışmaz — bu, dün denediğim piksel-piksel
adım oynatmasının neden piksellenme getirdiğini de açıklıyor. **Bu mimaride her piksel
tek örnektir; o örnek temiz olmak zorundadır.**

### 1.2 Adım bütçesi

`raymarchSteps = 140`, `stepGrowthDistance = 1900 m`, taban adım `2000/140 = 14.3 m`.

| mesafe | adım |
|---|---|
| 0 m | 14.3 m |
| 1 km | 21.8 m |
| 3 km | 36.8 m |
| 8 km | 74.4 m |
| 20 km | 164.7 m |

Bütçe makul. Döngü tavanı `steps × 5 = 700`, erken çıkış `transmittance < 0.06`.

### 1.3 Şekil alanı

- Hava haritası: 48 km periyot, pişmiş, çekirdek tabanlı (kapsama, tip, taban, tavan)
- Ana şekil: 3B Perlin-Worley, `_CloudScale = 0.0003`
- Aşındırma: `detailScale = 0.0011`
- Curl bükümü, makaslama, konvektif yükselme, kolon-sabit tepe tümsekleri

Bu da standart. Kod içindeki yorumlar önceki denemelerin ve neden geri alındıklarının
kaydını tutuyor — bu kalitede bir kayıt çoğu üretim kodunda yok, korunmalı.

### 1.4 Karar: yeniden yazma

Yeniden yazmak üç şeyi kaybettirir: (1) hava haritası pişirme zinciri, (2) yüksek katman
ve peçe entegrasyonu, (3) kodda biriken "şunu denedik, şu yüzden geri aldık" bilgisi.
Kazanç ise sıfır — çünkü aşağıdaki beş sorunun beşi de mevcut mimaride, sayıyla ya da
birkaç satırla çözülüyor. **Yeniden yazmıyoruz.**

---

## 2. Sorunların kök nedenleri

### 2.1 Soğan halkaları — ÇÖZÜLDÜ (uygulanmayı bekliyor)

**Ölçüm:** F1'deki adım boyu sürgüsü 0.15×'e çekilince halkalar tamamen kayboldu
(FPS 15'e düştü). Yoğunlukla artıyor, doku kademesiyle ilgisiz.

**Mekanizma.** `CloudCommon.hlsl:1236` — faz rastgeleliği yalnız **ilk adımda**
uygulanıyor:

```hlsl
if (i == 0 && dither > 0.0) step = min(step, max(nominalStep * dither, 1.0));
```

Işının başında `nominalStep` **14 m**. Yani pikseller arası faz farkı en fazla 14 m.
Ama kabuklar bulutun içinde doğuyor ve orada adım **40-165 m**. Faz farkı adımın
%10'una düşünce komşu pikseller neredeyse aynı yerde basamak atıyor — ekranda eşmerkezli
halka.

Literatür de aynı yeri işaret ediyor:
[Vertex Fragment](https://www.vertexfragment.com/ramblings/volumetric-cloud-banding/):
*"Randomly offset each ray's initial step position by a fraction of the step size...
adaptive stepping by itself is not sufficient — it must be paired with ray jittering."*
Kritik kelime **fraction of the step size** — bizim payımız adımın kesri değil, sabit
14 m.

**Çözüm.** Rastgeleliği buluta girişte ve o anki adım boyuna göre uygula. Kaba
kademeden ince kademeye geçiş noktası zaten var (`CloudCommon.hlsl:1249`):

```hlsl
if (coarsePass && density > 0.0)
{
    coarsePass = false;
    emptyRun = 0;
    prevDensity = 0.0;
    samplePoint -= direction * step;
    travelled -= step;
    // EKLENECEK: girişte faz kaydır — pay ADIMIN KESRİ
    float entry = dither * nominalStep;
    travelled += entry;
    samplePoint += direction * entry;
    continue;
}
```

Maliyet **sıfır**: örnek sayısı değişmiyor, yalnız nerede başladığı değişiyor.

**Risk:** giriş fazı piksel piksel değiştiği için bulut ön kenarında hafif gren
oluşabilir. Zamansal harman olmadığı için (bkz. 1.1) bu gren kalıcıdır. Karşı önlem:
faz payını tam adım yerine yarım adımla sınırlamak (`dither * nominalStep * 0.5`) —
halka gücünü yarıya indirir, gren görünmez kalır. Önce tam pay denenir.

### 2.2 Tekrar eden şekiller — sayısal

**Ölçüm.** Doku periyotları:

| katman | çarpan | dünya periyodu |
|---|---|---|
| ana şekil | ×0.37 | **9 009 m** |
| orta oktav | ×1.26 | 2 646 m |
| kolon dokusu | ×3.0 | 1 111 m |
| aşındırma | — | 909 m |
| hava haritası | — | 48 000 m |

Görüş mesafesi **28 788 m**. Yani ufka kadar ana şekil **3 kez**, orta oktav **11 kez**
tekrar ediyor. Zirveden bakınca göz bunu yakalıyor — çünkü yukarıdan bakınca aynı anda
onlarca periyot görüş alanına giriyor.

Hava haritası 48 km ile yeterli; sorun **3B şekil dokusunda**.

**Çözüm** (literatürde "anti-tiling"; Blackrack'in KSP bulutlarında ve
[bitsquid](http://bitsquid.blogspot.com/2016/07/volumetric-clouds.html)'de aynı yöntem):
aynı dokuyu ikinci kez, **döndürülmüş ve kaydırılmış** koordinatla oku, hava
haritasından gelen düşük frekanslı bir maskeyle karıştır. Periyot matematiksel olarak
kalkmaz ama tekrar deseni gözle bulunamaz hâle gelir.

Kodda zaten döndürülmüş ikinci okuma var (`CloudCommon.hlsl:495`, `rot` vektörü) ama
**aynı ölçekte ve sabit açıda**. Yapılacak: açıyı hava haritasının bir kanalından sür,
yani her bölgede farklı açı.

Maliyet: ek doku okuması yok (mevcut okuma kullanılıyor), yalnız koordinat hesabı. ~%3.

### 2.3 Silindirimsi bulutlar — tek satır

`CloudCommon.hlsl:553`:

```hlsl
ceiling01 *= saturate(0.30 + 0.85 * _Coverage);
```

Tavan **küresel kapsamayla** büyüyor. Yatay genişlik ise `localCoverage` eşiğinin
düşmesiyle büyüyor — ikisi farklı eğriler. Kapsama %25'ten %70'e çıkarken tavan
`0.51 → 0.90` (×1.75), yatay ayak izi ise eşik yumuşamasıyla çok daha yavaş büyüyor.
Sonuç: aynı genişlikte daha uzun bulut = silindir.

Kodun kendi yorumu bunu itiraf ediyor: *"kolon kapsaması sürgüyle dimdik tırmanınca
bulutlar genişlemeden sadece DİKEY büyüyordu"* — kolon bazlı ezme o yüzden geri
alınmış ama küresel eğri aynı hatayı daha yumuşak yapıyor.

**Çözüm.** Gerçek kümülüste genişlik ile yükseklik birlikte büyür (en/boy oranı 1:1 ile
1:1.5 arası; kümülonimbusta 1:3'e çıkar ama o zaten ayrı tip). Tavanı yatay büyümenin
karekökü ile sür:

```hlsl
// Tavan yatay büyümeyle AYNI ORANDA: kapsama arttığında ayak izi genişliyor,
// tavan da onun kökü kadar yükseliyor. Doğrusal bağlanınca bulutlar genişlemeden
// uzuyor ve silindire dönüyordu.
float lateralGrowth = saturate(0.30 + 0.85 * _Coverage);
ceiling01 *= lerp(0.75, 1.0, sqrt(lateralGrowth));
```

Sayılar denemeye açık; asıl düzeltme **doğrusal yerine kök**.

### 2.4 Bulut içinde FPS 40 — üç ayrı sebep

**Sebep 1: kaba kademe devre dışı.** İçerideyken ışın hemen yoğunluk buluyor,
`coarsePass` kapanıyor ve bir daha açılmıyor (`emptyRun` sayacı ancak boşlukta artıyor).
Yani bütün yol tam örneklemeyle yürünüyor.

**Sebep 2: ışık sondası her adımda.** `CloudCommon.hlsl:1374`:

```hlsl
int probeMask = transmittance > 0.6 ? 0 : transmittance > 0.35 ? 1 : 3;
if (cachedLit < 0.0 || (travelled < 5000.0 && probeMask == 0) || ...)
```

5 km içinde ve geçirgenlik 0.6 üstündeyken sonda **her adımda** çalışıyor. Sonda =
5 üstel örnek + koni çekirdeği + uzak komşu örneği ≈ 7 yoğunluk okuması. İçerideyken ilk
onlarca adım tam bu bölgede.

**Sebep 3: erken çıkış geç.** `transmittance < 0.06` — yani ışın %94 kapandıktan sonra
duruyor. Kalan %6'nın ekrana katkısı görünmez; 0.06 yerine **0.12** kullanmak kalite
farkı yaratmadan adım sayısını belirgin düşürür.

**Çözüm paketi:**

1. İçeride kaba kademeye dönüşü kolaylaştır: `emptyRun` eşiğini düşür, ayrıca
   `transmittance < 0.5` olduğunda sonda mesafe kapısını (5 km) kaldır
2. Sonda kapısını geçirgenliğe bağla: `travelled < 5000 && transmittance > 0.85`
3. Erken çıkışı `0.06 → 0.12`

Beklenen kazanç: bulut içinde %40-60. Ölçülecek.

### 2.5 Genel FPS 180 → 100

Bu oturumda üç şey açıldı ve üçü de aday:

1. **Yansıma haritası pişirme** — `DynamicGI.UpdateEnvironment()` gökyüzü rengi %2
   kayınca çağrılıyordu; renk sürekli oynadığı için pratikte her kare. **Düzeltildi**
   (saniyede bir), ama kullanıcı hâlâ 100 FPS görüyorsa etkisi ölçülmeli.
2. **SSAO** — tam çözünürlükte, `Downsample: 0`.
3. **Gölge atlası 4096** + soft shadow yüksek + gölge mesafesi 60 m.

**Yapılacak:** üçünü tek tek kapatıp FPS ölç. F1'e üç geçici anahtar koyulacak.

### 2.6 Yağış 1'de bulutların kaybolması

`CloudCommon.hlsl:1563` — peçe:

```hlsl
float veil = exp(-HeightFogIntegral(origin, origin + direction * firstHit, veilDrift)
                 * FogBankAt(origin.xz) * 0.6);
scattered = lerp(airHere * coverage, scattered, veil);
```

Peçe **rengi** süzüyor, alfayı değil — doğru yaklaşım. Ama tipide `snowVisibility = 320 m`
ve bulut tabanı 1745 m: integral öyle büyüyor ki `veil ≈ 0`, bulut tamamen havanın
rengine çöküyor. Fizik olarak doğru (tipide gökyüzü görülmez) ama oyunda "bulut yok"
hissi veriyor.

**Çözüm:** peçeye taban koy — en yoğun tipide bile bulutun kendi rengi %15 kalsın.
Fiziksel gerekçe: kar tanesi ileri saçılım yapar, bulut tabanının parlaklığı zeminden
tamamen kopmaz.

```hlsl
scattered = lerp(airHere * coverage, scattered, max(veil, 0.15));
```

### 2.7 SSAO'yu kapatmadan ucuzlatmak

URP'nin SSAO'sunda `Downsample` seçeneği var (şu an kapalı). Yarım çözünürlükte
hesaplayıp yükseltmek maliyeti yaklaşık yarıya indiriyor; bizim yarıçapımız 0.3 m ve
nesne ölçeğinde detay istediğimiz için kayıp küçük.

Ölçü: [Intel'in rehberi](https://gamedesigning.org/learn/ambient-occlusion/) SSAO'yu
kare süresinin **%5-10'u** olarak veriyor. 180 FPS'te kare 5.5 ms → SSAO 0.3-0.6 ms.
Yani 180→100 düşüşünün (5.5 → 10 ms, +4.5 ms) tek başına sebebi SSAO **olamaz**.
Ölçmeden suçlamayalım.

Alternatif: [HTrace GTAO](https://ipgames.gitbook.io/htrace-ao/comparisons-with-unitys-ao/htrace-gtao-vs.-urp-ssao)
tam/yarım/çeyrek çözünürlük ve daha iyi yükseltici sunuyor; URP SSAO'nun bilinen
sorunları (halo, şerit, düzensiz örtme) onda yok. Ücretli asset — kural artık editör
araçlarına izin veriyor ama bu çalışma zamanı bileşeni, ayrı karar.

**Öneri:** önce `Downsample` aç ve ölç. Yetmezse GTAO'yu konuşuruz.

---

## 3. Yapılacaklar — sıralı

Sıra bilinçli: önce bedava ve kesin olanlar, sonra ölçüm, en sonda pazarlık gerektirenler.

### A. Bedava ve kesin (onay verirsen hepsi tek turda)

1. **Halka düzeltmesi** — giriş fazını adımın kesri yap (2.1)
2. **Silindir düzeltmesi** — tavanı yatay büyümenin köküyle sür (2.3)
3. **Peçe tabanı** — tipide bulut tamamen kaybolmasın (2.6)
4. **Bulut içi performans** — sonda kapısı + erken çıkış eşiği (2.4)

### B. Ölçüm (senden tek cevap istiyorum)

5. **FPS suçlusu** — F1'e üç anahtar: SSAO kapat / gölge atlasını 2048 yap / yansıma
   pişirmesini durdur. Üçünü tek tek kapatıp FPS'i söyle. Ondan sonra doğru yeri açarım.

### C. Ölçüm sonrası

6. **Anti-tiling** — açıyı hava haritasından sür (2.2). ~%3 maliyet, önce A ve B
   bitsin ki neyin ne kadar yediğini bilelim.
7. **SSAO Downsample** — B'nin sonucuna göre.

### D. Geçici araçların temizliği

Bu oturumda F1'e eklenenler işleri bitince silinecek: bulut yoğunluk/gök ışığı çarpanı,
adım boyu çarpanı, doku kademesi kilidi, bisiklet yükseklik göstergesi, biriken kayma.
`DECISIONS.md`'ye kaydedildi.

---

## 4. Bisiklet (araştırma yok, ölçüm var)

**Ölçüm:** `çarpışma 140.56 | model altı 140.98 | kapsül altı 140.63`

Kapsül çarpışmanın 7 cm üstünde — bu doğru, `skinWidth` kadar. Ama **modelin altı
kapsülün 35 cm üstünde**. Yani fizik doğru yerde, görüntü havada asılı.

Sebep: kurulum betiği modeli kökün üstünde bir kez hizalıyor, sahnedeki eski konum
kalabiliyor. **Düzeltildi:** `BikeController.Start()` artık açılışta modelin altını
ölçüp bisikleti zemine oturtuyor (kapsülü geçici kapatarak, yoksa `CharacterController`
konum atamasını bir sonraki karede geri alıyor).

Gölge de buna bağlıydı: 35 cm boşluk, 31° güneşte gölgeyi 67 cm öteliyor. Bisiklet
oturunca gölge de yerine oturacak.

---

## 5. Kaynaklar

- [Vertex Fragment — Volumetric Cloud Banding](https://www.vertexfragment.com/ramblings/volumetric-cloud-banding/)
- [jpgrenier — Volumetric Clouds](https://www.jpgrenier.org/clouds.html)
- [bitsquid — Volumetric Clouds](http://bitsquid.blogspot.com/2016/07/volumetric-clouds.html)
- [Guerrilla — Nubis³ (2023)](https://www.guerrilla-games.com/read/nubis-cubed)
- [Guerrilla — Nubis, Evolved](https://www.guerrilla-games.com/read/nubis-evolved)
- [Optimisations for Real-Time Volumetric Cloudscapes (arXiv)](https://arxiv.org/pdf/1609.05344)
- [HTrace GTAO vs URP SSAO](https://ipgames.gitbook.io/htrace-ao/comparisons-with-unitys-ao/htrace-gtao-vs.-urp-ssao)
- [Unity URP — Ambient Occlusion](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@10.0/manual/post-processing-ssao.html)
