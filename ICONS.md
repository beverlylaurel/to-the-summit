# İkon Sistemi

Bu dosya `To The Summit` arayüzündeki bütün ikonların görsel sözleşmesidir. Kamera
vizörü, HUD, envanter, etkileşim istemleri, galeri, ayarlar ve gelecekte eklenecek tüm
arayüzler aynı sistemi kullanır.

## Seçilen dil: İnce Üçlü

İkonların kimliği, ana silüeti izleyen iç içe topoğrafik konturlardan gelir. Çizim;
**İnce Gravür** hassasiyeti ile **Topoğrafik** katman yapısının birleşimidir.

Sistem ölçeğe uyarlanır. “İnce Üçlü” adı her boyutta zorunlu olarak üç çizgi çizileceği
anlamına gelmez; ikon büyüdükçe topoğrafik ayrıntının kademeli olarak açılmasını tanımlar.

| ekrandaki ikon boyutu | kontur sayısı | amaç |
|---|---:|---|
| 16–23 px | 1 | silüeti temiz tutmak |
| 24–39 px | 2 | kimliği gösterirken keskinliği korumak |
| 40 px ve üzeri | 3 | topoğrafik katmanları tam göstermek |

Kontur görünürlüğü dıştan içe **%100 / %55 / %25** olur. İç katmanlar ana formu takip
eder; aralarında ekranda en az yaklaşık 1,5 px temiz boşluk bırakılır. Glow, blur ve
gölge kullanılmaz.

## Kanonik çizim tarifi

Bu bölüm stilin sayısal otoritesidir. Yeni ikonlar yaklaşık görünüşe göre değil, aşağıdaki
reçeteyle üretilir.

| özellik | zorunlu değer |
|---|---|
| ana çalışma alanı | `viewBox="0 0 32 32"` |
| geometrik merkez | `(16, 16)` |
| dış güvenli alan | en az 2 birim |
| dolgu | yok (`fill="none"`) |
| çizgi uçları | yuvarlak (`round`) |
| çizgi birleşimleri | yuvarlak (`round`) |
| dış kontur kalınlığı | 1 birim |
| orta kontur kalınlığı | 0,75 birim |
| iç kontur kalınlığı | 0,625 birim |
| orta kontur geometrisi | merkezden `%75` ölçeklenmiş ana yol |
| iç kontur geometrisi | merkezden `%56,25` ölçeklenmiş ana yol |
| dış / orta / iç opaklığı | `%100 / %55 / %25` |

Ölçek dönüşümleri tam olarak şu merkezden uygulanır:

```text
orta: translate(16 16) scale(0.75)   translate(-16 -16)
iç:   translate(16 16) scale(0.5625) translate(-16 -16)
```

Çizgi kalınlığı iç geometriyle beraber ölçeklenmez. SVG kaynaklarında üç katman kendi
çizgi kalınlığını açıkça taşır. İçe ölçeklenen yol ana silüetin dışına çıkarsa sorun
katmanda gizlenmez; ana yol yeniden çizilir.

### Boyut kademelerinin üretimi

- **Küçük:** yalnız dış yol görünür.
- **Orta:** aynı dış yol ve `%75` orta yol görünür.
- **Büyük:** dış, `%75` orta ve `%56,25` iç yolların üçü görünür.
- Bir ikon ara bir piksel boyutunda gösterilse bile en yakın küçük kademenin ayrıntı
  sayısını kullanır; fazladan kontur açılmaz.
- Unity UI'da ikonun `RectTransform` konumu ve boyutu tam piksele oturur. Kesirli ölçek
  veya kesirli ekran konumu kabul edilmez.

### Ana yol geometrisi

- Ana silüet 32 × 32 ızgarada çizilir; koordinatlar mümkün olduğunca tam veya yarım
  birime oturur.
- Görsel ağırlık merkezi `(16, 16)` noktasından en fazla 0,5 birim sapar. Bilinçli optik
  düzeltme gerekiyorsa kaynak dosyada yorumla belirtilir.
- Bir ikonun dış sınırı normalde `x/y = 2..30` aralığında kalır.
- Daire ve lens gibi tanınmayı taşıyan boşluklar iç kontur sayılmaz; ana yolun semantik
  parçasıdır ve bütün boyut kademelerinde kalır.
- Yeni bir ayrıntı 16 px tek kontur görünümünde kavramı daha hızlı okutamıyorsa eklenmez.

Kanonik kaynak sayfası `docs/ui/icon-style-ince-uclu-master.svg` dosyasıdır. Kamera,
envanter, sıcaklık ve zirve ikonlarının yol yapısı, katman dönüşümleri ve opaklıkları bu
dosyada doğrudan incelenebilir. Yeni ikon önce bu sayfanın kopyasında üretilip mevcut dört
ikonla yan yana karşılaştırılır.

## Değişmez çizim kuralları

- Önce ikonun tek konturlu silüeti çözülür. Tek kontur 16 px'de anlaşılmıyorsa ikon
  tamamlanmış sayılmaz.
- İkinci ve üçüncü kontur ana silüetin iç katmanıdır; aynı form ailesini izler.
- Her çizgi ikonun anlamına veya katman yapısına hizmet eder.
- Bütün iç çizgiler ana ikon sınırının içinde kalır.
- Köşeler sakin ve hafif yuvarlaktır. Organik biçim korunur; el çizimi titreşimi eklenmez.
- Çizgi ağırlığı ikon seti boyunca tutarlıdır. Küçük ikonlarda ayrıntı değil silüet
  önceliklidir.
- İkonlar tek renkli çalışır ve bulundukları arayüzün ön plan rengini alır. İkonun kendi
  içinde canlı, çok renkli palet kullanılmaz.
- Durum farkı gerekiyorsa önce metin, opaklık veya çevresindeki arayüz bileşeni kullanılır;
  ikon geometrisi keyfî biçimde değiştirilmez.

## Kesinlikle kullanılmayacak öğeler

- İkonun dışında duran dekoratif çizgi, yay veya çentik
- Ölçüm ekseni, cetvel imi ve rastgele teknik işaret
- Çapraz tarama, bağımsız doku çizgisi ve karalama
- Anlam taşımayan kesik çizgi
- Glow, neon kenar, renk sapması veya bulanıklık
- Sırf “vintage” görünmek için eklenen aşınma ve gürültü
- Küçük ölçekte birleşerek parlak leke oluşturan zorunlu üçlü kontur

## Yeni ikon üretme sırası

1. Kavram tek cümleyle tanımlanır; ikonun hangi nesne veya eylemi anlattığı netleştirilir.
2. 32 × 32 tasarım ızgarasında tek konturlu ana silüet çizilir.
3. Çizim 16 px'de yalnız tek konturla sınanır.
4. Ana formun içinde onu takip eden ikinci kontur eklenir; 24 ve 32 px'de sınanır.
5. Üçüncü kontur yalnız 40 px ve üstü kullanım için eklenir.
6. Gece, parlak kar ve sisli orta kontrast zeminlerde okunurluk kontrol edilir.
7. Hareketli kamera üzerinde titreşim, birleşme ve bulanıklık olmadığı doğrulanır.

## Kabul kontrolü

Yeni veya değiştirilmiş her ikon için cevapların tamamı “evet” olmalıdır:

- 16 px'de kavram tek konturdan anlaşılabiliyor mu?
- 24–39 px aralığında çift kontur keskin mi?
- Üçüncü kontur yalnız yeterli alan olduğunda mı görünüyor?
- İç katmanlar ana silüeti gerçekten takip ediyor mu?
- İkon dışında anlamsız çizgi bulunmuyor mu?
- Parlak kar üzerinde çizgiler tek bir bulanık lekeye dönüşmüyor mu?
- Setin mevcut ikonlarıyla çizgi, köşe ve boşluk dili aynı mı?

## Referanslar

- Kanonik SVG kaynak sayfası: `docs/ui/icon-style-ince-uclu-master.svg`
- Nihai ölçek ve kullanım önizlemesi: `docs/ui/icon-style-ince-uclu-preview.html`
- Gravür–topoğrafik araştırma panosu: `docs/ui/icon-style-finalists.html`

HTML dosyaları tasarım referansıdır; runtime varlığı veya uygulama kaynağı değildir.
İkonlarla eşleşen Inconsolata tipografi sistemi `TYPOGRAPHY.md` dosyasında tanımlanır.

## Unity dosya ve kod düzeni

- Runtime ikon kataloğu: `Assets/UI/Icons/ThinTriple/ThinTripleIconSet.asset`
- Üç doğal raster kademesi katalog varlığının içine gömülür: `20 / 32 / 48 px`.
- İkon kimlikleri ve 32 × 32 yol verisi: `Assets/Scripts/UI/Icons/ThinTripleIconSet.cs`
- Ortak, adaptif çizici: `Assets/Scripts/UI/Icons/ThinTripleIconRenderer.cs`
- Sistemi kullanan arayüzler kendi alan klasörlerinde kalır; fotoğraf HUD'ı
  `Assets/Scripts/Photography/UI/VintagePhotoHud.cs` içindedir.

Yeni ikon geometrisi yalnız ortak `ThinTripleIconSet` varlığına eklenir. Kamera, envanter
ve başka arayüzler kendi ikon kopyalarını ya da kendi çizim yordamlarını taşımaz. Fontlar
`Assets/UI/Fonts/<Aile>/`, ikon katalogları `Assets/UI/Icons/<Sistem>/` altında tutulur.
HTML'deki `vector-effect: non-scaling-stroke` davranışı Unity bake aşamasında korunur:
dış/orta/iç çizgiler doğal çözünürlükte sırasıyla `1 / 0,75 / 0,625 px` kalır. Runtime'da
ikon doğal kademe boyutunda ve tam piksel koordinatında çizilir.

Diyafram, zamanlayıcı ve fare gibi evrensel sembollerin temel silüetleri Lucide'ın açık
kaynak ikon geometrisinden uyarlanmıştır. Lisans kopyası
`Assets/UI/Icons/ThinTriple/LUCIDE-LICENSE.txt` dosyasındadır; İnce Üçlü katmanları ve
ölçek davranışı bu projenin ortak kataloğunda uygulanır.
