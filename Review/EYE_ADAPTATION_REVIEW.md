# Göz Adaptasyonu — Analiz ve Gerçekçilik Listesi

Tarih: 2026-08-29 · Kapsam: `LookController` (exposure adaptasyonu), `LookSettings`
(ton profilleri), pozlama girdi zinciri (`TimeOfDay.SurfaceLightLevel`, ambient probe),
ton eşleme (ACES) ve gözün gerçek fizyolojisine ne kadar yaklaşılabileceği.
Yalnızca analiz — hiçbir dosya değiştirilmedi.

---

## 1. Mevcut sistem nasıl çalışıyor?

Zincir: `LookController.Update` her karede iki gerçek büyüklük okur, EV hedefine
çevirir, asimetrik zaman sabitiyle yumuşatır, Volume'ın `postExposure`'una yazar.

```
lightLevel = max( SurfaceLightLevel / 3.03 , probeZenit / 0.148 )
adaptTarget = clamp( 0.35 × (−log2 lightLevel) , 0 , 2.5 EV )
tau = hedef > mevcut ? 2.5 s (karanlığa açılış) : 0.5 s (ışığa kapanış)
postExposure = profil.exposure + adapt
```

**Doğru yapılanlar** (dokunulmamalı):

- Kaynak ekran ortalaması değil, bilinen büyüklükler: güneş+ay ışığının düz zeminde
  bıraktığı aydınlatma ve ambient probe'un zenit parlaklığı. Ekran-ortalama tabanlı
  adaptasyonun klasik tuzaklarından (sahneye bakan spot açınca karanlık fırlaması)
  yapısal olarak muaf.
- Adaptasyon **kısmi** (`adaptShare` 0.35): gece öğlene dönmiyor, karanlık karanlık kalıyor.
- Asimetrik zaman sabitleri: karanlığa açılış 2.5 s (rodopsin rejenerasyonu), ışığa
  kapanış 0.5 s (pupil). Fizyolojik yönlendirme doğru.
- Alt sınır 0.0005: alacakaranlık tek seviyeye düzleşmiyor.
- Şimşek çakmasının pozlamayı savurması yok — ışık seviyesi hedef yumuşatılmış.

**Eksik/yamalı taraf** (aşağıdaki listenin kaynağı): adaptasyon tek küresel sayı
(EV) ve tek girdi karışımı; gözün görsel sistemindeki beş ayrı mekanizmanın hiçbiri
ayrı ayrı modellenmemiş.

---

## 2. Göz gerçekte ne yapıyor? (adım adım)

1. **Pupil** — 1.5-8 mm, 0.3 s'de daralıp ~10 s'de genişler; ışığa karşı gecikmeli
   çift yönlü (daralma 4× hızlı). ~2 EV'lik pay.
2. **Rodopsin rejenerasyonu (scotopik adaptasyon)** — koni → çubuk geçişi 5-30 dakika
   sürer, ~6-7 EV'lik dev pay. Oyunda "2.5 s" ile temsil ediliyor; gerçek 100× yavaş.
3. **Weber yasası / kontrast algısı** — göz mutlak parlaklık değil *çevresine göre*
   fark algılar; karanlıkta kontrast duyarlılığı artar, gri tanımı kayar.
4. **Purkinje etkisi** — alacakaranlıkta renklerden scotopik parlaklığa geçilirken
   kırmızı karanlığa gömülür, mavi/gök mavisi "aydınlanır" (rodopsin tepe duyarlılığı
   507 nm). Taşra manzaralarının alacakaranlık mavisinin ana bileşeni.
5. **Kar körlüğü / ışığa yanıtın tavanı** — kar üstünde parlaklık 10⁵ cd/m²'yi geçer;
   göz kısılır ama tekdüze beyaz alan detay kaybettirir (oyundaki "kar crush" problemi,
   `clearDay.exposure = -0.85` bunun ölçülmüş karşılağı).

---

## 3. Gerçekçilik için yapılabilirler — öncelikli liste

### A. Kolay ve düşük risk (belge/davranış farkı küçük)

**A1. `adaptShare` ve `exposureCap`'i ışık seviyesine bölgeden bağımlı kılmak.**
Şu an sabit 0.35 / 2.5 EV. Gerçekte gözün kapanma payı (ışığa) küçük, açılma payı
(karanlığa) büyük ve doygun. Tek satırlık değişiklik: `adaptShare`'i ışığa kapanırken
0.25'e, karanlığa açılırken 0.45'e taşıyan iki katsayı. Mevcut girdi zinciri aynı kalır.

**A2. Zenit + güneş karışımına **yerel** bir terim eklemek: bakış yönü.**
Göz baktığı yere adapte olur; tepeye bakınca gökyüzü, vadiye bakınca zemin. Ucuz
yaklaşım: `probe.Evaluate(lookDirection)` ile bakış yönündeki probe değerini
`max()` yerine **ağırlıklı karışım** olarak kullanmak (örn. %70 düz zemin + %30
bakış yönü). Azami taşma olmadan "tepeye bakınca ekranda hafifçe karanlıklaşma"
etkisi gelir. Riski: kamerayı sallayan oyuncuda pozlama salınımı — 2-3 s ek
yumuşatma şart.

**A3. Tanı penceresi ve log ölçek.** `lightLevel`'in log2'si alınıyor ama
`0.0005` alt sınırın altındaki fiziksel fırlama (ay ışığı min.) şimdiden klampda.
Alt sınırı **3 duraklı** yapınca (0.0005→0.00006) gece daha "gerçek" görünür ama
oyun oynanabilirliği düşer. Bilinçli kral kararı — DECISIONS.md'ye yazılmalı.

### B. Orta iş — tek mekanizma ekleyenler

**B1. Purkinje kayması (en yüksek görsel getiri / maliyet oranı).**
`tonemapping` sonrası ya da `ShadowsMidtonesHighlights` yerine ayrı bir geçişte:
ışık seviyesi düşünce görüntünün renk tepeciğini maviye kaydıran + koda müdahale
etmeden *kırmızı kanalın kontrastını düşüren* bir eğri. Uygulanış:
`postExposure` yanına LUT yerine **renk uzayı bazlı iki katsayı** (`purkinje`
0-1), `Lighting` değil **LookController** tarafında sürülür. Kar beyazı bozulmaz,
ama gece gri tonu "gök mavisi gri" olur. Kimyasal doğru: rodopsin 507 nm.

**B2. Glare/bloom kanalını ışık seviyesine bağlamak.**
`bloom.intensity` şu an profilden sabit. Gerçekte karanlıkta gözün glare'i artar
(ay çevresinde hale). Ucuz sürüm: `bloom.intensity = profil.bloom × (1 +
karanlıkPay)`, `threshold` sabit. Yüksek risk yok, çünkü zaten bloom var.

**B3. Ufuk parlaklığına göre kontrast kapanması (Weber yasası yaklaşımı).**
`contrast` değerini sabit profilden almak yerine, sahnedeki **kabaca ölçülmüş
min-max luma aralığına** göre daraltmak (histogram okumadan, ışık seviyesi + bakış
yönü ile modellenmiş). "Gece kontrastı artar" hissi fizyolojik doğru.

**B4. Karanlık gecede satürazisyon çöküşü — koni/çubuk kruvası.**
`scotopik` geçiş bandında (roughly probe zenit 0.15-0.02 arası) satürazisyonu
**ekstra** düşüren bir terim: `saturation × (0.7 + 0.3 × scotopikOran)`.
Mevcut profiller gece zaten -20/-36 çekiyor; bu terim oranı **fiziksel** yapar
(sabitle değil), gün gece arasını yumuşatır.

### C. Büyük iş — mimari karar isteyenler

**C1. Yerel adaptasyon (yerel ton eşleme)** — ekran bölgesel histogram, çok pahalı;
URP'de hazır karşılığı yok. Önerilmez.

**C2. Işıklı kaynak dikizine göre ışık saçılması (physically-based bloom)** —
`PhysicallyBasedSky` diskleri HDR; gerçek göz ışık saçılması için bloom'un
`spectrum` tabanlı (maviye kayan) hale modu gerekebilir. Bloom scatter'ın
renk matrisiyle yapılabilir; orta maliyet, çok spesifik getiri.

**C3. Zaman içinde pozlamanın "taşması" (photoreceptor bleaching).**
Güneşe doğrudan bakan oyuncuda kısa süreli detay kaybı (bleaching), bakış
çevrildikten sonra 1-2 s karanlık leke. GPU tarafında küçük bir accumulate
buffer + decay gerektirir. **Etik/konfor riski yüksek** ( baş ağrısı, fotosensitif
tetik); oyun konforu için tersini (glare halkası) seçmek daha güvenli. Önerilmez.

---

## 4. Sayısal çapraz kontrol — mevcut değerler göz değerleriyle

| Büyüklük | Oyun | Gerçek göz | Boşluk |
|---|---|---|---|
| Pupil payı | (modellenmemiş, adapt içinde) | ~2 EV | ok |
| Rodopsin payı | 2.5 EV cap | 6-7 EV | **2.5-3 EV eksik** |
| Karanlığa süre | 2.5 s | 5-30 dk | **bilinçli kısaltma** (oyun) |
| Işığa süre | 0.5 s | 0.3 s (daralma) | ok |
| adaptShare | 0.35 sabit | ~0.5 ışık, ~0.25 karanlık | kısmen ters |
| Purkinje | yok | alacakaranlıkta kuvvetli | **eksik** |
| Weber (kontrast) | profil sabiti | ışığa göre değişken | eksik |
| Glare (karanlık) | profil sabiti | artar | eksik |

Rodopsin dev payının oyunca indirilmesi **doğru** karar (oyuncu 20 dakika karanlık
beklemez). Ancak "cap 2.5" ile "share 0.35"in birlikte kullanımı, gece +1 EV'lik
ek açılışı engelliyor — A1'in asıl gerekçesi.

---

## 5. Önerilen uygulama sırası

1. **A1** (share bölgesel) + **B4** (scotopik satürasyon) — aynı dalda; sadece
   `LookController.Apply` içinde hesap, yeni asset alanı yok. Ölçüm: gece
   kadrajında probe zenit + postExposure logu.
2. **B1** (Purkinje) — ayrı dal; geçiş noktası `ShadowsMidtonesHighlights`
   (zaten var) yerine post-process sonuna mini bir Shader Graph/Blit.
   Ölçüm: alacakaranlık kadrajında kırmızı/mavi oranı.
3. **B2** (glare) + **A2** (bakış yönü karışımı) — ikisi de LookController'da;
   dikkat: A2 için `LookController`'a kamera referansı eklenmeli (Bind).
4. **B3** (Weber kontrast) — ölçüm gerektirir (luma histogramı debug HUD'a).
5. **C** grubu — bilinçli kral kararına; DECISIONS.md'ye yazılmadan denenmez.

Her adımda projenin kuralı geçerli: tek değişken, ölçüm, sonra sonraki adım.
Ölçüm aracı: `DebugMenu`'ye "exposure state" satırı (adapt, lightLevel, bakış
yönü probe değeri) — mevcut panelin genişletilmesi, yeni araç değil.

---

## 6. Bilinçli olarak yapılmayacaklar

- Tam fizyolojik süre (5-30 dk): oyun oynanabilirliğini öldürür.
- Ekran-ortalama (histogram) tabanlı adaptasyon: mevcut yapının getirdiği
  "güneşe bakınca fırlama" sorununu geri getirir.
- Bleaching (bakış sonrası leke): konfor riski.
- SSAO'ya geri dönüş, vignette: ikisi de bilinçli silinmiş; gerekçeleri
  `RATIONALE.md`'de duruyor.
