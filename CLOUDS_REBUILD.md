# Bulut sistemi — teknik kayıt

Portun (`UnityVolumetricCloudsURP`, MIT) makaleyle sekiz farkı, hangisinin nasıl kapandığı,
ölçülmüş sayılar ve kurtarılmış gürültü hash'i. Amaç tek şey: **aynı hata iki kez
ölçülmesin.**

> **Belgeler arası iş bölümü.**
> **Bağlar ve bilinçli kurallar `SYSTEMS.md` → Bulutlar'da** — güncel olan orasıdır, bu
> dosyada bağ listesi tutulmaz.
> `NUBIS_NOTES.md` makale okumalarını tutuyor: 12 soru, her cevabın yanında kaynak sayfa.
> Buradaki dersler ÖLÇÜMDEN, oradaki cevaplar MAKALEDEN gelir; ikisi karışmaz.
> `DECISIONS.md` kararları ve tetikleyicilerini tutuyor.

---

## Portun makaleyle farkları

Temel: `UnityVolumetricCloudsURP` (MIT, jiaozi158'in HDRP→URP portu), `Assets/VolumetricClouds/`.

`NUBIS_NOTES.md` ile satır satır karşılaştırıldı. Bulunan sekiz fark, kapatılma sırasıyla:

| # | Konu | Portta | `[H18]` | Durum |
|---|---|---|---|---|
| 1 | Hava haritası | `GetCloudCoverageData` sabit `half4(0.9, 0, 0.25, 1)` | R/G iki kapsama kanalı, B azami yükseklik `[s.11]` | **kapandı** |
| 2 | Şekil gürültüsü kanalları | `_Worley128RGBA.r` | `R(sn_r, fBm(g,b,a)−1, 1, 0, 1)` `[s.14]` | **fark yok** — doku tek kanal, fBm pişmiş (ölçüldü) |
| 3 | Detay gürültüsü kanalları | `_ErosionNoise.x` | `dn_r·0.625 + dn_g·0.25 + dn_b·0.125` `[s.14]` | **fark yok** — aynı sebep |
| 4 | Detay ↔ kapsama yönü | kapsama arttıkça detay artar | `0.35·e^(−g_c·0.75)`, azalır `[s.14]` | **kapandı** |
| 5 | Detayın yükseklikle tersi | yok | `L_i(DN_fbm, 1−DN_fbm, SAT(p_h·5))` `[s.14]` | **kapandı** |
| 6 | Yükseklik fonksiyonu | tek eğri dokusu | iki ayrı: SA şekil, DA yoğunluk `[s.12-13]` | **kapandı** (7b) |
| 7 | Yoğunluk zinciri sırası | gradyan kapsama-remap'inin içinde | DA en son çarpan `[s.14-16]` | **kapandı** (7a + 7b) |
| 8 | Örs | yok | `[s.17]` | **kapandı** |

**2 ve 3 nasıl kapandı:** dokular PIL ile okundu, üçü de `mod=L` — tek kanal. İsimlerindeki
RGB/RGBA yanıltıcı. `WorleyNoise128RGBA` min 0.090 / ort 0.753, ham Worley dağılımı değil;
oktav toplamı ve Perlin-Worley remap'i üretim anında dokuya pişmiş. Portun `.r` / `.x`
örneklemesi doğru, kayıp yok.

### 1 — hava haritası (kapandı)

Portta yol tamamen sökülmüştü, geriye yalnız yorum satırları kalmıştı. Geri getirilen:

- `VolumetricClouds.shader` — `_CloudMapTexture`, `_CloudMapTiling`, `_CloudCoverage`
- `VolumetricCloudsUtilities.hlsl` — `GetCloudCoverageData` gerçek örnekleme yapıyor
- `VolumetricCloudsVolume.cs` — `cloudMap`, `cloudMapSize`, `cloudCoverage`
- `VolumetricCloudsURP.cs` — üçünü materyale yüklüyor
- `Assets/Editor/CloudMapGenerator.cs` — haritayı üretiyor
- `MountainSceneBootstrap.EnsureCloudVolume` — haritayı profile bağlıyor

Harita kanalları `[H18 s.11]`: **R** seyrek kapsama (w_c0), **G** yoğun kapsama (w_c1),
**B** azami bulut yüksekliği (w_h). Kapsama sürgüsü iki harita arasında geçiyor:
`WM_c = max(w_c0, SAT(g_c − 0.5) × w_c1 × 2)`.

**A** yoğunluk (w_d) — 7b'de eklendi, tüketicisi `DensityAlter`. Aralık [0.35, 0.65];
`DA` içinde 2 ile çarpıldığı için etkisi [0.70, 1.30] ve 0.5 nötr `[H18 Ek B.3]`.

Portun üstüne eklenen üç şey, üçü de gerekçeli:

- `_CloudCoverage` (`g_c`) — portta yok, `[H18 s.11]` formülü olmadan çalışmıyor
- `cloudMapSize` metre — ham `_CloudMapTiling` yerine; fiziksel karşılığı olan tek sayı.
  48 000 m, silinen sistemin `weatherMapWorldSize`'ı ile aynı (`SCALE.md`)
- Dünya XZ döşemesi — HDRP'nin `_NormalizationFactor` kubbe eşlemesi gezegen ölçeği için

Kamera kayması `GetCloudCoverageData`'nın **içinde** yapılıyor. Sebep: gölge yolu
(`VolumetricCloudsShadows.hlsl:61`) aynı fonksiyonu kaymasız çağırıyor; kayma çağıran
tarafta kalsaydı iki yol farklı dünya noktası okurdu.

Üretici sinüs hash'i kullanmıyor — tamsayı bit karıştırıcı (aşağıdaki kurtarılan kod).

**Üreticinin kendisi makaleden gelmiyor.** `[H18]` R kanalı için "elle çizilmiş yerleşim"
diyor, üretim yöntemi vermiyor. fBm, kendi aralığına normalize, eşik 0.50, plato genişliği
0.15 — hepsi bizim, ölçülerek seçildi:

| kenar biçimi | bulutlu alan | doygun çekirdek | bulut içi ort |
|---|---|---|---|
| doğrusal germe (kenar = 1 − eşik) | %47 | **%0.24** | 0.148 |
| plato, kenar 0.15 | %47 | **%23** | 0.743 |

Doğrusal germe çekirdeği 1.0'a taşımıyor; o sırada shader `coverage²` de aldığı için
bulut tamamen görünmez oluyordu (o terim sonradan kaldırıldı, aşağıda). Ekranda görünen
oran **doygun çekirdek** oranını izliyor, sıfır olmayan alanı değil — %47 bulutlu harita
gökyüzünün çeyreğinden azını doldurdu.

### 7a — zincir sırası ve `g_c` bağı (kapandı)

`VolumetricCloudsUtilities.hlsl`, `EvaluateCloudProperties`:

- Remap sınırı `1 − densityErosionAO.x × WM_c × (1 − shapeFactor)` yerine
  `1 − g_c × WM_c` `[s.14]`. Kapsama sürgüsü zincire buradan giriyor; portta bu bağ yoktu
  ve sürgünün **alt yarısı ölüydü** (`max(R, SAT(g_c−0.5)…)` 0.5 altında sabit).
- Yoğunluk-yükseklik gradyanı (`densityErosionAO.x`) remap sınırının içinden çıkarılıp
  zincirin **en sonuna** çarpan olarak alındı `[s.14-16]`.

Portun kendi terimlerinden `× coverage²` **sonradan kaldırıldı**: kapsama zaten remap
sınırından giriyor, kare almak ikinci kez cezalandırıyordu. Kapsaması 0.6'ya inen bölge
0.36'ya düşüyor ve remap sınırıyla birleşince tam delik açılıyordu — kapalı gökte bile
boşluk çıkmasının sebebi buydu.

Duran tek port terimi: `lerp(1, lowFrequencyNoise, shapeFactor)` — şekil sürgüsü.

### 7b — SA/DA ayrımı (kapandı)

`[H18 Ek B.2]` ve `[Ek B.3]`'teki iki fonksiyon `VolumetricCloudsUtilities.hlsl`'e girdi:
`HeightAlter` şekli, `DensityAlter` yoğunluğu değiştiriyor. Zincir `[s.14-16]`:

```
SN_sample × SA  →  kapsamayla remap  →  detayla remap  →  × DA × g_d
```

Portun eğri dokusunun `.x` kanalı (`densityCurve` sürgüsü) yerini `DensityAlter` aldı;
sürgü ölü kalacağı için Volume'dan, editörden ve `PrepareCustomLutData`'dan silindi.
`.y` (erozyon eğrisi) ve `.z` (ortam örtmesi) duruyor.

### 4 ve 5 — detay değiştirici (kapandı)

Tek fonksiyonda toplandı: `DetailModifier` `[H18 Ek B.5]`.

| | portta | şimdi |
|---|---|---|
| yükseklik | sabit `1 − detail`, her kotta aynı | `lerp(detail, 1 − detail, SAT(p_h × 5))` — tabanda tüylü, tepede yuvarlak |
| kapsama | `× 0.75 × WM_c`, kapsamayla **artıyordu** | `× 0.35 × e^(−g_c × 0.75)`, kapsamayla **azalıyor** |

Erozyon sürgüsü bunun üstünde ayrı çarpan kaldı; 1.0'da makaleyle birebir. Mikro erozyon
(portun kendi eklemesi, makalede yok) aynı fonksiyonu kullanıyor — iki detay katmanı ters
yönlere çalışmasın diye.

### 8 — örs (kapandı)

Örs **iki fonksiyonu birden** değiştiriyor `[H18 s.17]`: şekli üs olarak, yoğunluğu ayrıca
azaltarak. İkincisi olmadan tepe fazla yoğun kalıyor (makale s.17 şekil 12b).

Yeni sürgü `anvilAmount`, varsayılan **0** — o değerde üs 1'e, `lerp` 1'e sadeleşiyor,
yani mevcut görüntü değişmiyor.

### 9 — güneş geçirgenliğinin tabanı (kapandı)

**Belirti.** Bulut içi kapkara. Yoğunluk düşürülünce siyah gidiyor ama bulut yassılaşıp
boşalıyor — yani yoğunluk doğru, karartan başka bir şey.

**Sebep, kâğıtta bulundu — sürgü denemesiyle değil:**

| terim | tavanı | siyah yapabilir mi |
|---|---|---|
| Toz etkisi | `lerp(1, powder, 0.25)` → en kötü **0.75×** | hayır, matematiksel olarak imkânsız |
| Erozyon örtmesi | `sqrt(0.35 × 0.1)` = 0.19, yalnız ortam ışığından | hayır |
| Ortam / güneş kısıcı | ikisi de 1.0, nötr | hayır |
| **Güneş geçirgenliği** | `sigmaT` 0.04 × adım 1000 m × yoğunluk 0.2 × 2 adım → `extinction ≈ 16`, `exp(−16) = 1.1e−7`; ikinci oktav `exp(−8.4) = 2.2e−4` | **evet, sıfır** |

Bulut içine güneşten hiçbir şey ulaşmıyordu; geriye yalnız ortam ışığı kalıyordu.

**Düzeltme** `[H18 Ek B.6]` `Attenuation`: geçirgenliğin **tabanı** var.
`exp(−b × a_c) × 0.7`, `b = 6`, `a_c = 0.2` `[H18 s.58]` → **0.211**. Güneşe bakarken
kelepçe gevşiyor, yarıya iniyor. Gerçek bulut kara değil çünkü ışık içeride çok kez
saçılıyor; HZD çok saçılmayı bu tabanla karşılıyor.

Portta `EvaluateSunTransmittance` saf `exp(-extinction)` yazıyordu, taban yoktu. Taban
oktav başına Beer terimine uygulandı; faz fonksiyonu dokunulmadan kaldı.

### `sky brief.md`'nin bulut maddeleri

| madde | durum |
|---|---|
| Ortam ışığında güneş diski hariç | **kapandı** — portun `_DisableSunDisk` global'i vardı, `Sky.shader` okumuyordu |
| Enerji korunumlu analitik entegrasyon | **zaten vardı** — port Frostbite'ın formülünü kullanıyor, aynı slaytı kaynak gösteriyor |
| İki loblu HG, normalize | **kapandı** — port iki HG'yi topluyordu (integral 2, enerji korunmuyor); `lerp`e çevrildi, `g0=0.8 / g1=−0.5 / α=0.5` |
| Aerial perspective bulutlara | **açık** — ertelenen sky işinde |

İki lobun toplanması yerine ortalanması bulutları **yaklaşık yarı yarıya söndürür**. Bu
beklenen: eksik olan enerji zaten yoktu. Karanlık gelirse çözüm çift enerjiyi geri koymak
değil, ışık seviyesine bakmaktır.

### Makalede ölçülmüş ama UYGULANMAYANLAR

Bilerek bırakıldı, sebebiyle birlikte — tekrar araştırılmasın.

| madde | neden uygulanmadı |
|---|---|
| Adım büyüme oranı 0.04 `[s.40-43]` | Port kendi uyarlanabilir şemasını kullanıyor: boş uzayda büyük adım, buluta girince geri sarıp küçük adım (`activeSampling`). Makalenin doğrusal büyümesi onun üstüne binmez |
| Kapsama düşükken adımı kısalt, `mult_min = 0.4` `[s.44-47]` | Portta `totalDistance = stepS × _NumPrimarySteps` — adımı çarpmak örnekleme sıklığını değil **ışının menzilini** değiştiriyor. Kısaltmak uzak bulutları keser. Makalede adım ve menzil bağımsız |
| Yoğunluk düşükken adımı uzat, `div_min = 0.4` `[s.48, s.51]` | Aynı sebep, ters yönde: menzil uzar, kare süresi öngörülemez olur |
| Aydınlatmaya mavi gürültü `+= bluenoise × 0.003` `[s.26]` | Portun bantlaşma çaresi başka: adım titremesi (`integrationNoise`) + zamansal birikim. Bantlaşma görülürse buraya dönülür |

**Uygulanan:** ışık adımı 2 → **4**. Makale ölçüp seçmiş `[s.36-39, Tablo 1]`: *"2: çok
daha iyi, ince detay eksik / 4: azalan getiri başlıyor"*, maliyet 2.80 → 3.85 ms. Portun
şemasıyla çakışmıyor, düz parametre.

### Açık kalan

Yoğunluk, şekil ve aydınlatma zinciri bitti; bağların tamamı kuruldu (`SYSTEMS.md`).

Tek açık başlık **bulut rengi**: ortam sondasının alt yüzü ufkun altına bakıyor ve
`Sky.shader` orada zemin çizmeyip pus rengini uzatıyor, bulutların altı o renkle
aydınlanıyor. Düzeltmesi ertelenen atmosfer işinde — `DECISIONS.md`.

---

## Ölçülerek bulunmuş dersler

Bunlar ölçülerek bulundu, tekrar bulunmasın.

1. **Yoğunluk alanı ve gölge sondası görüş ışınına bakamaz.** `transmittance` üzerinden
   dallanan her şey ekranda izo-yüzey çiziyor: bulut ortasında kesik ada, kenarda koyu
   zar, halka ailesi. Ucuzlatma yalnız ışından bağımsız ölçütlerle (mesafe, LOD, kademe).

2. **Örnekleme kafesi ekranda tek olmalı.** Adım boyu ışının kendi geçmişinden
   (geçirgenlik, önceki yoğunluk) türetilirse komşu pikseller ayrışır → eşmerkezli kabuk.

3. **Kolon-sabit bir alan yüksekliği süremez.** Sürerse desenini dikey sütun olarak basar.

4. **Kaba eleme üst sınırı gerçek formülün AYNISI olmalı**, elden yazılmış yaklaşıklık
   değil. Altında kalırsa bulut, sıçrama hücresinin ekseninde düz kenarlı kıymığa kesilir.
   Sayılar tek yerde tutulup sınır onlardan türetilmeli.

5. **Mesafe girdi değildir.** Tipi/kapsamayı mesafeyle kaydırmak, bulutu kameranın
   nerede olduğuna göre değiştirir — üstüne uçunca şekil değişiyor.

6. **Erken çıkışta kuyruk kapatılır, kesilmez.** Geçirgenlik olduğu gibi bırakılırsa
   alfa 0.88'de kalır ve arka plan bulutun içinden görünür.

7. **Türetilmiş asset kendi kendini tazelemeli.** Geçerlilik imzası ayarlar **ve**
   algoritma sürümünden kurulur; sürüm üreticinin yanında durur. Asset'in ÜSTÜNE yazılır,
   silinip yeniden kurulmaz (GUID düşerse sahnedeki başvurular kopar).

8. **Çekirdek bütçesi alan oranı olarak tutulur** (toplam çekirdek alanı / harita alanı).
   Elden yazılmış çarpan 4.3 kat doyma üretti: kapsama kanalı her yerde 1, haritada ayrı
   bulut kalmadı, sınırları gürültü çizdi.

9. **Gürültü hash'i tamsayı karıştırıcı olmalı.** `Frac(Sin(Dot(p,k))*43758)` girdisi
   küçük tamsayı hücre koordinatı olduğunda korele çıkıyor: Worley'nin öznitelik noktası
   her hücrede aynı göreli yere düşüyor ve doku **kare ızgara** oluyor. Bugün "kafes",
   "hepsi aynı boy", "düzenli pufçuklar" diye görülen her şeyin kökü buydu.

10. **Katman kalınlığı boyun ölçeğidir.** Gradyan katmana normalize; katman kalınlaşınca
    bütün bulutlar birlikte uzuyor. HZD 2.5 km kullanıyor.

11. **Çizim menzili gürültü ölçeğini kilitliyor.** Adım mesafeyle büyüdüğü için uzakta
    mip yükseliyor ve ince gürültü ortalamaya yatıyor. 300 km menzilde yakın alan ince,
    uzak alan kaba gürültü istiyor — tek ölçek ikisini veremez. HZD hacimsel bulutu
    35 km'de kesip ötesini 2B katmana bırakıyor.

12. **Ayar sürgüsü ölçeklemez, eşiği kaydırır.** `harita × sürgü` yazılırsa haritanın
    sıfır olduğu yer hiçbir sürgüde kapanmaz — %100 kapsama gökyüzünü kapatmıyordu.

---

## Kaynak

HZD/Nubis, Schneider 2015 — modelleme s.34-37, aydınlatma s.50-69, render s.70-85.
Nubis 2017 (Decima) ve Nubis³ (2023, 3B voksel) devamı.

---

## Kurtarılan kod: gürültü hash'i

Silinen `CloudNoiseGenerator`'dan tek kurtarılan parça. Sinüs hash'i küçük tamsayı hücre
koordinatlarında korele çıkıyor ve Worley'yi kare ızgaraya çeviriyordu (madde 9).

```csharp
static uint Mix(uint h)
{
    h ^= h >> 16; h *= 0x7feb352du;
    h ^= h >> 15; h *= 0x846ca68bu;
    h ^= h >> 16;
    return h;
}

// Worley öznitelik noktası — girdi sarmalanmış tamsayı hücre koordinatı
static Vector3 Hash3(Vector3 p)
{
    uint x = (uint)Mathf.RoundToInt(p.x);
    uint y = (uint)Mathf.RoundToInt(p.y);
    uint z = (uint)Mathf.RoundToInt(p.z);
    uint seed = Mix(x * 0x9E3779B1u) ^ Mix(y * 0x85EBCA77u) ^ Mix(z * 0xC2B2AE3Du);

    return new Vector3(
        Mix(seed ^ 0x27D4EB2Fu) / 4294967296f,
        Mix(seed ^ 0x165667B1u) / 4294967296f,
        Mix(seed ^ 0xD3A2646Cu) / 4294967296f);
}

// Perlin köşe değeri — aynı sebep
static float Hash1(Vector3 p)
{
    uint x = (uint)Mathf.RoundToInt(p.x);
    uint y = (uint)Mathf.RoundToInt(p.y);
    uint z = (uint)Mathf.RoundToInt(p.z);
    return Mix(Mix(x * 0x9E3779B1u) ^ Mix(y * 0x85EBCA77u) ^ Mix(z * 0xC2B2AE3Du))
           / 4294967296f;
}
```

Doku yapısı (HZD s.31) doğruydu, korunacak:
- Taban 128³ RGBA: R = `Remap(perlin(4), worley(6) − 1, 1, 0, 1)`, G/B/A = Worley 6/12/24
- Detay 32³ RGB: Worley 8/16/32
- Curl 128² RGB: ıraksamasız
