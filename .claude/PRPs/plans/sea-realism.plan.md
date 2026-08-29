# Plan: denizde düzenliliği bitirmek

Üç iş, tek plan. Ortak hedef: **denizde tekrar eden hiçbir yapı kalmasın** ve kıyıdaki
hareket kıyının kendi şeklinden doğsun.

Sıra bilinçli: 2 → 3 → 1. İkisi oyuncunun baktığı yeri (kıyı) düzeltiyor ve **aynı
mekanizmayı** paylaşıyor; döşeme kırma açık denizi ilgilendiriyor ve en pahalısı.

---

## Ölçülen durum

| Kaynak | Sayı | Belirti |
|---|---|---|
| FFT döşeme, tier 2 | L = 37 m, görünür alanda 110 tekrar | Yakın plandaki küçük dalgalar 37 m'de bir aynı |
| FFT döşeme, tier 1 | L = 191 m, 21 tekrar | Orta dalgalar 191 m'de bir aynı |
| FFT döşeme, tier 0 | L = 967 m, 4 tekrar | Görünür ama seyrek |
| Zaman döngüsü | 200 s = 20 dalga periyodu | Deniz 3,3 dakikada bir kendini tekrar ediyor |
| Kırılma | yok | Ölü dalga kıyıya 38°'de gelip 38°'de vuruyor |
| Swash | kıyı boyunca tek değişken, 286 m gürültü | Bütün sahil aynı anda kabarıyor |

`SeaDeform` dört şey uyguluyor — sığlaşma, choppiness sönümü, kıyı sönümü, kırılma
yüksekliği sınırı. **Dördü de skaler.** Hiçbiri dalga vektörünü döndürmüyor.

---

## Faz 2 — Kıyı dalgası: kırılma kendiliğinden çıksın

### Fikir

FFT alanını döndürmek yerine, sığ suda **kendi başına kıyıya paralel olan** ikinci bir
katman ekle. Sığ su dalgasının fazı yalnız derinliğe bağlı:

```
c = sqrt(g h)          sığ su hızı
k = ω / c              yerel dalga sayısı
φ(x) = ∫ k ds          kıyıya doğru biriken faz
```

**Tepeler sabit-faz eğrileridir; faz yalnız derinlikten geldiği için tepeler
sabit-derinlik konturlarıdır.** Yani koyda içeri bükülür, burunda dışarı — refraction
uydurulmuyor, geometriden düşüyor.

Düz eğimli bir kıyı için (`h = βx`) kapalı form var ve doğrulandı:

```
φ(h) = (2ω/β) · sqrt(h/g)
```

| derinlik | kıyıdan | dalga boyu | ardışık tepe arası |
|---|---|---|---|
| 0,25 m | 4 m | 16 m | 16 m |
| 1 m | 17 m | 31 m | 31 m |
| 4 m | 69 m | 63 m | 63 m |
| 16 m | 276 m | 125 m | 125 m |

Tepe aralığı sütun sütun `c·Tp`'ye eşit çıkıyor — kapalı form tutarlı. Derin sudaki
λ₀ = 156 m'den kıyıda 16 m'ye **10 kat sıkışma**; görünen shoaling bu.

### Ama gerçek batimetri düz eğimli değil

Kapalı form `h = βx` varsayıyor. Gerçek taban öyle değil, ve varsayımı zorlamak koylarda
yanlış faz verir. **Faz alanı da pişirilir** — `SeaBathymetry.Bake` ile aynı desen:

- CPU'da bir kez, derinlik gradyanı boyunca `∫ k ds` integre edilir.
- Sonuç `RHalf` doku (batimetri ile aynı çözünürlük).
- Sığ su varsayımı yalnız `h < spectrumDepth` bölgesinde; ötesinde katman zaten sönük.

Böylece keyfi batimetri, koy, burun, sığlık — hepsi doğru faz alır ve **hiçbir yerde
tekrar etmez**, çünkü kaynağı arazinin kendisi.

### Enerji devri — çift sayım yok

FFT zaten `shoreFade = smoothstep(0, SEA_SHORE_FADE_DEPTH, depth)` ile kıyıda sönüyor.
Kıyı dalgası **tam tersini** okur: derinlik azaldıkça açılır. Genliği Hs'ten gelir
(sığlaşma kazancıyla), yani ikinci bir "ne kadar büyük" ayarı doğmaz.

### Değişecek dosyalar

| Dosya | İş |
|---|---|
| `SeaBathymetry.cs` | `BakePhase(...)` ekle — faz alanı dokusu |
| `SeaManager.cs` | Fazı yayımla, `RefreshBathymetry` ile birlikte tazele |
| `SeaShaderIDs.cs` | `_SeaShorePhaseTex` |
| `SeaCommon.hlsl` | `SeaShoreWave(posXZ, depth, t)` — yükseklik + eğim |
| `SeaCommon.hlsl` → `SeaDeform` | Katmanı derinlikle harmanla |
| `SeaLit.shader` | Eğimi normale ekle |

### Riskler

| Risk | Olasılık | Etki | Ne yapılacak |
|---|---|---|---|
| Batimetri tekseli 7,32 m; faz gradyanı basamakları büyütür, tepelerde teraslama | **YÜKSEK** | Görünür artefakt | Faz dokusu bikübik örneklenir; teraslama ölçülür (tepe konumunun kıyı boyunca sapması), gerekirse faz pişirilirken derinlik yumuşatılır |
| Kıyı dalgası + FFT üst üste binip genlik iki katına çıkar | ORTA | Dalgalar çok büyük | Devir ölçülür: toplam rms(h) derinliğe göre çizilir, tek tepe olmalı |
| Dalga boyu kuralı: tepe aralığı 16 m, mesh karosu 0,5 m — sorun yok; ama batimetri tekseli 7,32 m tepe aralığının yarısına yakın | ORTA | En sığ tepeler çözülemez | 16 m'lik tepe 7,32 m teksele sığmaz; en sığ bölgede faz **kapalı formla** hesaplanıp pişmiş alana harmanlanır |
| Kıyı dalgası mesh'i araziyle kesiştirir | DÜŞÜK | Titreme | Mevcut `hMax = γh/2` sınırı katmana da uygulanır |

---

## Faz 3 — Swash iki boyutlu ve gruplu

### Fikir

Swash, kıyı dalgasının **koşusudur**. Şu an fazı global; olması gereken: her noktanın
fazı, oraya varan dalganın fazı.

```
swashPhase(x) = φ_kıyı(x) − ω t
```

Aynı pişmiş faz alanı. Sonuç: kabarma kıyı boyunca **yürür**, koy ve burun farklı anda
kabarır. 286 metrelik "hep birlikte" bandı ortadan kalkar — ikinci bir gürültü eklemeden.

### Gruplar — set set gelmek

Gerçek kıyıda dalgalar set hâlinde gelir. Kaynağı spektrumun iki tepesidir: rüzgâr denizi
ve ölü dalga farklı periyotlarda, vuruşma (beat) yaratıyorlar.

**Uydurulmayacak.** Zaten iki parçayı ayrı ayrı integre ediyoruz (`SeaSpectrumMoments`);
her ikisinin tepe frekansı elimizde. İki frekansın vuruşması:

```
T_grup = 1 / |1/Tp_rüzgâr − 1/Tp_ölü|
```

Bu ölçülecek: sakin havada iki tepe de ~10 s'ye yakınsa T_grup çok uzun çıkar (dakikalar)
— o zaman set yok demektir ve **ekleme yapılmaz**. Sayı bakılmadan terim yazılmaz.

### Değişecek dosyalar

| Dosya | İş |
|---|---|
| `SeaSpectrumMoments.cs` | İki parçanın tepe frekansını ayrı döndür |
| `SeaManager.cs` | Grup periyodunu hesapla ve yayımla (ölçüm anlamlıysa) |
| `SeaLit.shader` | Kıyı köpüğü fazını pişmiş alandan al |
| `SeaWetnessDriver.cs` | Islak bandın tepesi aynı fazdan |

### Riskler

| Risk | Olasılık | Etki | Ne yapılacak |
|---|---|---|---|
| Faz alanı kıyı boyunca hızlı değişirse köpük parçalanır | ORTA | Dağınık köpük | Faz gradyanının kıyı boyunca bileşeni ölçülür; eşiği aşarsa alan yumuşatılır |
| Grup periyodu anlamsız çıkar (dakikalar) | **ORTA** | Terim boşa yazılır | Ölçüm ÖNCE yapılır; anlamsızsa madde düşer ve `DECISIONS.md`'ye gerekçesiyle yazılır |

---

## Faz 1 — FFT döşemesini kırmak

### Fikir

Ubisoft La Forge yöntemi: FFT'nin ürettiği yer değiştirme/türev haritalarını **altıgen
karolarla** sentezle. Teksel başına üç karo örneklenir, ağırlıklar merkezde 1, kenara
doğru düşer; harman **varyans koruyan**:

```
G = ( Σ wᵢ (Gᵢ − μ) ) / sqrt( Σ wᵢ² ) + μ
```

**Bizde histogram dönüşümü GEREKMİYOR.** Tessendorf alanı rastgele fazların toplamı, yani
tanım gereği Gauss. Heitz–Neyret'in "Gaussianize / de-Gaussianize" adımları Gauss olmayan
girdi içindir; bizim girdi zaten Gauss, dolayısıyla ortalama ve varyans korumak yeterli.
Bu, yöntemi hem ucuzlatır hem tam yapar.

### Hangi tier'a uygulanacak — ölçüyle

| tier | L | görünür tekrar | karar |
|---|---|---|---|
| 2 | 37 m | 110 | **uygulanacak** |
| 1 | 191 m | 21 | **uygulanacak** |
| 0 | 967 m | 4 | uygulanmayacak — maliyet kazancı karşılamıyor |

Maliyet: uygulanan tier'ların her doku okuması 3 katına çıkar. Deniz shader'ında tier
başına üç doku dizisi okunuyor (yer değiştirme, türev, köpük). İki tier × 3 doku × 2 ek
örnek = **kare başına 12 ek doku okuması**. Ölçülecek, kabul edilmeden yazılmayacak.

### Zaman döngüsü

`loopPeriod` 200 s = 20 dalga periyodu. Kısa. Yükseltmenin maliyeti yok — sayı yalnız
`Mathf.Repeat` içinde. Ama uzatınca float hassasiyeti bozulur (spec §6.5 bu yüzden
koymuş). Ölçülecek: 200 / 600 / 1800 s'de fazın hassasiyet kaybı.

### Değişecek dosyalar

| Dosya | İş |
|---|---|
| `SeaCommon.hlsl` | `SeaHexBlend(...)` — altıgen karo, üç örnek, varyans koruyan harman |
| `SeaCommon.hlsl` | `SeaSampleDisplacement` / `SeaSampleSlope` / `SeaSampleFoam` onu kullansın |
| `SeaSettings.cs` | Hangi tier'da açık olduğu ayar (varsayılan: 1 ve 2) |
| `SeaQualityPreset.cs` | Low'da kapalı |

### Riskler

| Risk | Olasılık | Etki | Ne yapılacak |
|---|---|---|---|
| Türev ve Jacobian harmanla tutarsızlaşır → köpük yanlış yerde | **YÜKSEK** | Köpük dalgadan kopar | Türevler AYNI ağırlıklarla harmanlanır; Jacobian harmanlanmış türevden YENİDEN hesaplanır, harmanlanmaz |
| Varyans koruma yer değiştirmede ölçek kayması yapar | ORTA | Dalga boyu doğru, yükseklik yanlış | `Test Wave Field` rms(h) karşılaştırması: harman öncesi/sonrası %5 içinde kalmalı |
| 12 ek doku okuması kare süresini düşürür | ORTA | FPS | Build'de ölçülür; düşerse yalnız tier 2'de bırakılır |
| Kıyıda kıyı dalgası zaten hâkimken boşa maliyet | DÜŞÜK | İsraf | Sığ suda harman kapatılır (`shoreFade` ile) |

---

## Yapılmayacaklar

- **FFT alanını döndürerek refraction.** Uzamsal değişen döndürme alanı geriyor; kıyı
  dalgası katmanı aynı sonucu artefaktsız veriyor.
- **Sprey / serpinti.** Ayrı iş; bu plan yüzeyle ilgili.
- **Gelgit.** `seaLevelY` sabit; oynanışı etkiler, ayrı karar.
- **Tier 0'a döşeme kırma.** Ölçüm karşılığını vermiyor.

## Kabul ölçütleri

| Faz | Ölçüt |
|---|---|
| 2 | Tepeler koyda içeri bükülüyor: kıyı boyunca tepe yönü ile derinlik gradyanı arasındaki açı, kıyıya yaklaşırken küçülmeli — 4 m'de ve 0,5 m'de ölçülür |
| 2 | Toplam rms(h) derinliğe göre tek tepeli, FFT+kıyı dalgası çift sayım yok |
| 3 | Kabarma zamanı kıyı boyunca değişiyor: iki nokta arasındaki faz farkı sıfırdan farklı ve batimetriyle uyumlu |
| 1 | Kenar salınımı testine benzer ölçüm: yer değiştirme alanının özilintisi 37 m ve 191 m'de tepe VERMEMELİ |
| 1 | `Test Wave Field` rms(h) harman öncesine göre %5 içinde |
| hepsi | 17 shader 0 hata, sabit eşliği tam, konsol temiz |

## Belge borcu

- `SYSTEMS.md` — kıyı dalgası hangi kaynaklardan besleniyor, ne okumuyor
- `RATIONALE.md` — neden alan döndürülmedi, neden histogram dönüşümü gerekmedi
- `SCALE.md` — faz alanı arazi çözünürlüğüne bağlı; dağ değişirse yeniden pişer
- `DECISIONS.md` — tier 0'ın dışarıda bırakılması, grup terimi düşerse gerekçesi
- `SYMPTOMS.md` — yalnız ölçümle kapanan bir belirti çıkarsa

## Karmaşıklık

**YÜKSEK.** Üç faz, ~10 dosya, bir yeni CPU pişirme adımı ve bir shader sıcak yolu.
Saat tahmini vermiyorum — bu projede iş, tur sayısıyla değil **ölçümle** kapanıyor.
Her fazın kendi kabul ölçütü var; ölçüt sağlanmadan sonraki faza geçilmez.
