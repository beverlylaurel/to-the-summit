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

> *(cevap)*

## 2. Gürültünün dünya periyodu neye göre seçilir?

Bizde taban gürültüsü 3333 m, katman 2500 m'ydi: bir periyot bile sığmıyor, gürültü
dikeyde değişmiyor, bulut sütuna dönüyordu. Periyot / katman kalınlığı / bulut eni
arasındaki oran ne olmalı?

> *(cevap)*

## 3. Kapsama sürgüsü ne yapar?

Bizde `harita × sürgü` yazılıydı ve %100'de bile gök kapanmıyordu (haritanın sıfır
olduğu yer hiçbir sürgüde dolmaz). Ölçekleme mi, eşik kaydırma mı, başka bir şey mi?

> *(cevap)*

## 4. Yükseklik gradyanı şekli ÇARPAR mı, eşiği mi yükseltir?

`[N15 s.35]` çarpıyor: `SetRange(gürültü × gradyan) × kapsama`. Bizde çarpınca tepeler
iğneye döndü. Sönüm bandının genişliği ile gürültünün özellik boyu arasındaki ilişki ne?

> *(cevap)*

## 5. Kenar yumuşaklığı nereden gelir?

Bizde `pow(t, 4)` uydurulmuştu. Makalelerde kenarı yumuşatan şey ne — erozyon mu,
yoğunluk eğrisi mi, örnekleme mi?

> *(cevap)*

## 6. Adım boyu ve mip seçimi

Bizde adım mesafeyle büyüyor, uzakta mip 4.5'e çıkıp gürültüyü siliyordu. Kaç örnek,
adım nasıl büyüyor, mip nasıl seçiliyor, çizim menzili kaç?

> *(cevap)*

## 7. Kameranın bulutun İÇİNDEN geçmesi

Bizim senaryomuz. `[N22]`'nin ana konusu. Yakın alanda ne değişiyor, maliyet nereden
çıkıyor, hangi yaklaşım terk edilmiş?

> *(cevap)*

## 8. Temporal yeniden yansıtma ve artefaktlar

Bizde harman yoktu, piksellenme kalıcıydı. History nasıl tutuluyor, komşuluk kelepçesi
nasıl, hareket hâlinde ne bozuluyor?

> *(cevap)*

## 9. Aydınlatma parametreleri

Koni kaç örnek, uzak örnek nerede, HG eksantrikliği kaç, powder formülü ve gücü,
yağışta soğurma nasıl artıyor?

> *(cevap)*

## 10. Bulut gölgesinin yere düşürülmesi

Bizde `CloudShadowAt` gökyüzüyle aynı alandan besleniyordu. Makalelerde ayrı bir gölge
haritası mı pişiriliyor, yoksa aynı alan mı okunuyor?

> *(cevap)*

## 11. Hava haritası kanalları ve nasıl sürüldükleri

`[N15 s.40]` R kapsama, G yağış, B tip diyor. `[N17]` weather map sistemini anlatıyor.
Kanallar zamanla nasıl değişiyor, simülasyon mu, boyanmış doku mu?

> *(cevap)*

## 12. Hangi optimizasyon ne kazandırıyor, hangisi artefakt üretiyor?

Bizde ışından türeyen her ucuzlatma ekranda izo-yüzey çizdi. Makalelerde hangi
ucuzlatmalar var, hangileri gönderilmiş, hangileri geri alınmış?

> *(cevap)*

---

## Sorulmamış bulgular

Yukarıdaki on iki sorunun hiçbirine girmeyen ama önemli görünen her şey. Soru listesi
bizim bildiğimiz eksiklerden yapıldı; bilmediklerimiz burada birikir. Kaynak zorunlu.

> *(boş)*

---

## Okuma defteri

Kesintisiz olmalı. Boşluk = okunmamış sayfa.

| makale | toplam | okunan | eksik |
|---|---|---|---|
| `[N15]` nubis-2015 | **99** | s.18–87 | **s.1–17, s.88–99** |
| `[N17]` nubis-2017 | **108** | — | s.1–108 |
| `[N22]` nubis-2022 | **207** | s.1–10 | s.11–207 |
| `[H18]` haggstrom-2018 | **~100** | — | s.1–100 |

**Toplam ~514 sayfa, okunan 80.** Kalan 434.

`[H18]`'in sayısı kesin değil (PDF nesne akışları sıkıştırılmış, ham sayım çalışmadı;
linearization ipucu `/N 100` diyor). Okuma sırasında son sayfaya varılınca kesinleşir.

`[N15]`'in atlanan kısımları da okunacak — s.1–17 giriş, s.88+ optimizasyon bölümü.
Bugün ortadan girilip ortada bırakılmıştı.
