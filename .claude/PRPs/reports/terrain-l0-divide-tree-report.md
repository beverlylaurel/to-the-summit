# Uygulama raporu: L0 — Divide Tree sentezi

## Özet

540 km'lik bölgenin zirve/boyun/sırt grafiği üretildi ve repoya yazıldı. Zirve **5709 m,
bölgenin en yüksek noktası ve merkezde**. Python araç zinciri `Tools/terrain/`, veri
`Assets/Terrain/DivideTree.txt` (579 KB, 7268 zirve, 7267 boyun).

Unity tarafı yazıldı ama **doğrulanmadı** — kullanıcı uyurken derleme kontrolü yapılamıyor.
`feat/terrain-l0` dalında bekliyor.

## Plan ile gerçek

| ölçüt | plan | gerçek |
|---|---|---|
| Karmaşıklık | XL | XL |
| Yeni dosya | 6 | 10 |
| Sentez süresi | bilinmiyor | 396 s |
| Doğrulama | Unity'de kullanıcı | **bekliyor** |

## Görevler

| # | görev | durum | not |
|---|---|---|---|
| 1 | İki kapıyı kapat | bitti | Bölge `himalaya-everest`, Kirmse verisi indirildi |
| 2 | Python ortamı | bitti | `noise` derlenmedi → aynı imzalı yedek yazıldı |
| 3 | Referansı koştur | bitti | L0 25 s / L1 14 s; bulgular `DECISIONS.md` |
| 4 | Bölge profili | bitti | **Dört tur** — ayrıntı aşağıda |
| 5 | Zirveyi çak | bitti | Dağılım tavanı 5500 m'ye çekildi |
| 6 | `DivideTree` asset | yazıldı | **DOĞRULANMAMIŞ** |
| 7 | İçe aktarma | yazıldı | **DOĞRULANMAMIŞ** |
| 8 | Doğrulama penceresi | yazıldı | **DOĞRULANMAMIŞ** |
| 9 | Belgeler | bitti | `DECISIONS`, `SCALE`, `SYSTEMS` |

## Doğrulama

| seviye | durum | not |
|---|---|---|
| Python zinciri | **geçti** | Uçtan uca koştu, çıktı ölçüldü |
| Belirlenimlilik | **geçti** | Aynı tohum aynı grafiği veriyor |
| Görsel (Python) | **geçti** | Beş resim üretildi ve bakıldı |
| Derleme (C#) | **BEKLİYOR** | Unity açılmadan yapılamaz |
| Görsel (Unity) | **BEKLİYOR** | Pencere açılmadan görülemez |

## Plandan sapmalar

**Görev 4 dört tur sürdü, hepsi ölçümle:**

1. Karakter azimuta bağlanmıştı → pasta dilimi çıktı. Kaçmaya çalıştığımız radyal
   yapaylığın maskedeki hâli. Yapı **eksene** taşındı (Himalaya çizgiseldir).
2. Diziler `[kuzey, doğu]` kurulmuştu, referans `[doğu, kuzey]` bekliyor
   (`divtree_synthesis.py:24`, koddan doğrulandı). Silsile 90° dönük çıkmıştı.
3. Plato 3600 m tasarlandı, arazide 103 m çıktı. **Yüksekliği zirveler taşır**;
   yoğunluk yükseltildi.
4. Histogram eşlemesi (`mapToPDF`) kaldırıldı — bizim `elevMap`'imiz metre, yazarlarınki
   birimsiz gri tonlama.

**Ek sapma:** dağılım tavanı 5500 m'ye çekildi, yoksa sentez bizim zirveyi geçiyordu.

## Karşılaşılan sorunlar

| sorun | çözüm |
|---|---|
| `noise` paketi Windows'ta derlenmiyor | Aynı imzalı Perlin yedeği; referans kaynağa dokunulmadı |
| `fixedPeaks` tek satırda `squeeze()` sıfır boyuta düşüyor | İkinci sabit zirve — zaten doğrusu (Everest/Lhotse) |
| Ova ve plato çöküyordu | Yoğunluk sıfır değil düşük; tepecikler doğdu |
| Tek piksellik kesitten ölçüyordum | Alan ölçümüne geçildi (araç gürültülüydü) |

## Açık kalan

**Gerçek plato bu yöntemle çıkmıyor.** Yoğunluk düşükse ada ada kopuyor, yüksekse
dağlıktan ayırt edilemiyor. Sebep vadi oyma derinliği (`maxSlopeCoeff`), yoğunluk değil.
L1'in işi. Kayıt `DECISIONS.md`.

**Yüzey detayı** (L1) hâlâ açık — referans repoda erozyon kodu yok.

## Sonraki adım

Sabah: `feat/terrain-l0` derlenir, doğrulanır, `main`'e alınır. Sonra L1 planı.
