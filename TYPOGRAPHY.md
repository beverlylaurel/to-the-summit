# Tipografi Sistemi

Bu dosya `To The Summit` arayüz tipografisinin görsel ve uygulama sözleşmesidir. Kamera
vizörü, HUD, envanter, etkileşim istemleri, galeri, ayarlar ve diğer oyun arayüzleri aynı
temel sistemi kullanır.

## Seçilen aile: Inconsolata

Oyunun genel arayüz yazı ailesi **Inconsolata**dır. Monospace yapısı ölçüm, irtifa,
sıcaklık, süre ve kamera verisini kararlı sütunlarda tutar. Açık harf biçimleri ve teknik
ritmi, `ICONS.md` içindeki İnce Üçlü ikon diliyle eşleşir.

Bu seçim arayüz içindir. Dünyadaki el yazıları, basılı belgeler, mezar taşları veya başka
diegetik nesneler kendi bağlamına uygun yazı kullanabilir; bunlar genel UI ailesini
değiştirmez.

## Kullanılacak gerçek ağırlıklar

| görev | ağırlık | kullanım |
|---|---:|---|
| gövde ve açıklama | 400 Regular | cümleler, yardım, açıklama |
| HUD ve değer | 500 Medium | irtifa, sıcaklık, kamera değerleri |
| başlık ve eylem | 600 SemiBold | panel başlığı, seçili eylem, önemli kısa etiket |

Sahte kalın veya sahte italik üretilmez. Bu üç gerçek font ağırlığı projeye ayrı font
varlığı olarak alınır. 300 ve altı ağırlıklar parlak kar ve hareketli görüntü üzerinde
zayıf kaldığı için arayüzde kullanılmaz. 700 ve üstü ağırlıklar ikonların ince çizgi
dengesini bastırdığı için kullanılmaz.

## Hiyerarşi

| rol | önerilen boyut | satır yüksekliği | harf kullanımı |
|---|---:|---:|---|
| küçük HUD etiketi | 12–13 px | 1,0 | kısa ise BÜYÜK HARF |
| HUD değeri | 14–18 px | 1,0 | verinin doğal biçimi |
| gövde metni | 15–17 px | 1,4 | normal cümle düzeni |
| panel başlığı | 20–24 px | 1,1 | Başlık Düzeni |
| büyük bölüm başlığı | 28–36 px | 1,05 | Başlık Düzeni |

12 px altındaki metin temel arayüz bilgisi taşımaz. Daha küçük işaret zorunluysa metin
yerine `ICONS.md` kurallarındaki küçük kademe ikon kullanılır veya düzen yeniden kurulur.

## Yazım ve aralık kuralları

- Uzun metinler normal cümle düzeninde yazılır; tamamı büyük harfe çevrilmez.
- Büyük harf yalnız kısa HUD sınıfları ve durum etiketlerinde kullanılır: `ISO`, `EV`,
  `RAKIM`, `RÜZGÂR` gibi.
- Büyük harfli kısa etiketlerde harf aralığı `0.06em`; gövde metninde `0`; büyük
  başlıklarda en fazla `-0.01em` olur.
- Monospace yapıyı düzeltmek için karakterler elle birbirine yaklaştırılmaz. Bütün bir
  metin rolü tek aralık değeri kullanır.
- Sayılar sağa hizalanır veya sabit başlangıç noktasından dizilir. Bir değer değişirken
  komşu ikon ve etiket yer değiştirmez.
- Ondalık ayırıcı oyuncuya gösterilen Türkçe metinde virgüldür: `0,3`, `−18,5 °C`.
- Birim ile sayı arasında bölünmez boşluk kullanılır: `2.847 m`, `18 °C`, `12 km/sa`.
- Eksi işareti kısa tire değil matematiksel eksi `−` olur.

## İnce Üçlü ikonlarla eşleşme

- Metin ve ikon aynı ön plan renk tokenını kullanabilir; ikona ayrı parlaklık efekti
  eklenmez.
- 16–23 px ikon, yanında 12–15 px metinle; 24–39 px ikon, 15–20 px metinle kullanılır.
- İkon optik olarak metnin x yüksekliğine ortalanır. Yalnız kutu merkezlerini eşitlemek
  yeterli değilse en fazla 1 px dikey optik düzeltme yapılır.
- İkon ile ilk harf arasındaki boşluk küçük kullanımda en az 8 px, orta kullanımda en
  az 10 px olur.
- İkonun katman çizgileriyle rekabet eden metin dış çizgisi, gölgesi veya glow kullanılmaz.

## Renk ve zemin

Font kendi başına saf beyaz olmak zorunda değildir; bulunduğu arayüzün nötr ön plan
tokenını alır. İkincil bilgi renk değiştirmek yerine opaklıkla geri çekilir. Canlı ve
çok renkli tipografi kullanılmaz.

Metin gece, parlak kar ve sisli orta kontrast zeminlerde sınanır. Sahne üstündeki metin
doğrudan görüntüye bırakılmaz; okunurluk gerekiyorsa arayüz sisteminin kontrollü koyu
zemini veya vignette alanı kullanılır.

## Türkçe ve glif kapsamı

Font varlığı aşağıdaki dizeyle sınanmadan kabul edilmez:

```text
ÇĞİÖŞÜ çğıöşü — − + % ° 0123456789
```

Runtime font atlası bu glifleri, noktalama işaretlerini ve kullanılan birim sembollerini
içerir. Eksik glifi başka bir fonttan sessizce tamamlayan fallback görünümü kabul edilmez.

## Kabul kontrolü

- Gerçek 400, 500 ve 600 ağırlıkları mı kullanılıyor?
- Türkçe gliflerin tamamı aynı Inconsolata ailesinden mi geliyor?
- 12 px küçük etiket parlak kar ve hareketli kamera üzerinde okunuyor mu?
- Değişen sayısal değerler komşu öğeleri oynatmıyor mu?
- Büyük harf yalnız kısa etiketlerde mi kullanılıyor?
- Metinde sahte kalın, sahte italik, glow veya dış çizgi bulunmuyor mu?
- İkon ve metin optik olarak aynı satırda mı?

## Referans

- Font karşılaştırma panosu: `docs/ui/font-style-board.html`
- İkon sözleşmesi: `ICONS.md`

Karşılaştırma panosu araştırma kaydıdır. Uygulamada bu dosyadaki Inconsolata kuralları
otoritedir.
