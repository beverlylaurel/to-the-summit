# Arayüz Yüzey Sistemi

Bu dosya `To The Summit` arayüzlerinin panel, zemin, kontur, opaklık ve yüzey
karakteri için görsel ve uygulama sözleşmesidir. İkon geometrisinin otoritesi
`ICONS.md`, yazı sisteminin otoritesi `TYPOGRAPHY.md` olarak kalır.

## Seçilen dil: Yağmur Camı

Oyunun ortak arayüz yüzeyi **Yağmur Camı**dır. Yüzey, yağışlı bir kamera camı gibi
çevreyi tamamen kapatmaz; soğuk yeşile çalan koyu ve yarı saydam bir tabaka altında
görüntüyü taşır. İnce, açık bir dış kontur ile daha silik bir iç kontur camın kenarını
tanımlar. Çok hafif üst yansıma ve kısa koyu gölge paneli zeminden ayırır.

Bu ad yalnız görsel stile aittir. Panel üzerinde hareket eden damla, akıntı, tarama
çizgisi veya dekoratif yağmur efekti kullanılmaz.

## Kanonik tokenlar

Runtime otoritesi `Assets/Scripts/UI/Style/RainGlassUi.cs` dosyasıdır. Temel ilişkiler:

- zemin: çok koyu, soğuk yeşil; normal yüzeyde yaklaşık `%54` opaklık
- güçlü bilgi yüzeyi: aynı zemin; yaklaşık `%58–68` opaklık
- ana metin ve ikon: kırık, soğuk beyaz; saf beyaz değil
- ikincil metin: aynı renk ailesi, daha düşük parlaklık ve opaklık
- dış kontur: açık gri-yeşil, yaklaşık yarı opak
- iç kontur: dış konturun çok daha silik sürümü
- tuş zemini: panelden az daha yoğun; ayrı ve canlı bir renk taşımaz

Sayılar başka dosyalara kopyalanmaz. Renk ve opaklık değerleri değiştirilirse yalnız
`RainGlassUi` güncellenir.

## Biçim

- Temel köşe dili keskin ya da en fazla 2 px yumuşaktır.
- Panel tek açık dış kontur ve gerektiğinde 3 px içeride silik ikinci kontur kullanır.
- Gölge kısa ve koyudur; geniş glow, neon veya parlak halo yoktur.
- Yüzey dokusu gösterişli değildir. Gürültü, çizik, kâğıt lifi ve karbon örgüsü ortak
  UI'ya taşınmaz.
- Saydamlık sahneyi hissettirir ama metin okunurluğunu sahne kontrastına teslim etmez.
- Büyük, kesintisiz siyah şeritler kullanılmaz. Bilgi kadar alan kaplayan yüzeyler
  tercih edilir.

## Yerleşimden bağımsızlık

Yağmur Camı bir yerleşim şablonu değildir. Saha Etiketi, vizör ölçüm yüzeyi, bildirim,
galeri başlığı ve gelecekteki envanter panelleri farklı biçimde yerleşebilir; aynı yüzey
tokenlarını kullanır.

Elde tutulan eşyanın kısa tanıtımı için seçilen ortak kalıp **Saplı Kart**tır: eşya
kimliği üst kartta, yalnız mevcut eylemler alt sırada ve aralarında kısa bir dikey bağ
bulunur. Kamera bu kalıbın ilk uygulamasıdır. Başka eşya yeni bir panel dili üretmez.

## Hareket

- Ortak giriş/çıkış yönü **Kısa Soldan + Ağır Cam**dır: panel grubu 4 px soldan
  yerine gelir ve çıkarken aynı yöne döner.
- Geçiş 220 ms sürer. Opaklık ve konum aynı sakin `smoothstep` eğrisini kullanır.
- Giriş ve çıkış birbirinin uzamsal tersidir; ayrı bir çıkış efekti icat edilmez.
- Sürekli salınım, parıltı, nefes alma ve dikkat isteyen döngüsel animasyon yoktur.
- Bilgi değişirken panel ölçüsü oynamaz; sayısal alanlar sabit kalır.
- Kamera hareketi sırasında arayüz keskin kalır. Lens odak kaybı yalnız dünya görüntüsüne
  uygulanır.

## Kısayol gösterimi

- Klavye kısayolu düz cümle metni olarak yazılmaz. `G`, `4`, `Q / E` ve `A / D` gibi
  girdiler her zaman ortak tuş rozeti içinde gösterilir; eylem adı rozetin yanında durur.
- Fare düğmesi `SAĞ TIK`, `SOL TIK` veya benzeri düz metinle tarif edilmez. Karşılığı
  olan İnce Üçlü fare ikonu kullanılır.
- Bir eylemin iki girdisi varsa iki ayrı girdi–eylem çifti gösterilir. `G / SAĞ TIK
  KAPAT` gibi metin içinde eğik çizgiyle birleştirilmez.
- Tuş rozeti ve eylem adı optik olarak aynı satıra ortalanır; bütün rozetler kendi
  grubunda aynı yüksekliği kullanır.

## Kabul kontrolü

- Sahne panelin arkasından seçiliyor mu?
- Parlak kar üzerinde ana metin ve İnce Üçlü ikon net mi?
- Dış ve iç kontur tek kalın çizgi gibi birleşmeden okunuyor mu?
- Panel, taşıdığı bilgiden belirgin biçimde daha geniş veya yüksek mi?
- Saf siyah, saf beyaz, neon, glow ya da canlı vurgu rengi var mı?
- Aynı ekrandaki bütün yüzeyler `RainGlassUi` tokenlarını mı kullanıyor?
- 720p yükseklikte alt kontroller ve metinler ekrandan taşmadan görünüyor mu?
