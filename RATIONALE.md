# Gerekçeler

`SYSTEMS.md` **ne neyi okur**'u söyler ve kuralı emir kipinde yazar. Burası o kuralın
**neden** öyle olduğunu tutar: ölçüm, denenip başarısız olan yol, ürettiği belirti.

Ayrım mekanik:

| soru | dosya |
|---|---|
| Bu sistem neyi okur, neyi okumaz? | `SYSTEMS.md` |
| Kural ne? | `SYSTEMS.md` |
| Kural neden böyle, ne ölçüldü, ne denendi? | **burası** |
| Hangi belirtiyi gördük, sebebi neydi? | `SYMPTOMS.md` |
| Hangi karar ertelendi, tetikleyicisi ne? | `DECISIONS.md` |

Bir kural değişirse **ikisi birden** değişir. Buradaki bir kayıt gerekçesini yitirirse
silinir — telafi terimi geri eklenmez.

---

## Kayıtlar

100 kayıt. Başlığa tıkla, ya da başlıkta ara — dosyanın tamamını okuma.

- [Bulutlar](#bulutlar)
- [Kuşaklar ve hava dalgalanması](#kusaklar-ve-hava-dalgalanmasi)
- [Sis, görüş ve hava sinyalleri](#sis-gorus-ve-hava-sinyalleri)
- [Dağ yüzeyi](#dag-yuzeyi)
- [Gökyüzü ve gök cisimleri](#gokyuzu-ve-gok-cisimleri)
- [Bilinçli kuralların gerekçeleri](#bilincli-kurallarin-gerekceleri)
- [Yağış, ses ve şimşek](#yagis-ses-ve-simsek)
- [Volumetrik sis](#volumetrik-sis)
- [Işığın rengi](#isigin-rengi)
- [Renk düzenlemesi](#renk-duzenlemesi)
- [Arazi](#arazi)
- [Yağış perdesi — makaleden sapılan üç yer](#yagis-perdesi-makaleden-sapilan-uc-yer)
- [Langer spektral perdesi SİLİNDİ — iki kez denendi, ölçüldü](#langer-spektral-perdesi-silindi-iki-kez-denendi-olculdu)
- [Yağış sütunları DENENDİ VE ELENDİ — sisin içinden uzak sis yapısı görünmez](#yagis-sutunlari-denendi-ve-elendi-sisin-icinden-uzak-sis-yapisi-gorunmez)
- [Rüzgâr şiddeti → hız eşlemesi kare, doğrusal değil](#ruzgâr-siddeti-hiz-eslemesi-kare-dogrusal-degil)
- [Yükseklik bantları ÖLÇÜMLE ELENDİ; sınır tabakası kapalı biçimde](#yukseklik-bantlari-olcumle-elendi-sinir-tabakasi-kapali-bicimde)
- [Tanecik girdabın her kıvrımını yemez — atalet süzgeci](#tanecik-girdabin-her-kivrimini-yemez-atalet-suzgeci)
- [Girdap ölçeği kotla küçülür — kesilerek değil, enerji kaydırılarak](#girdap-olcegi-kotla-kuculur-kesilerek-degil-enerji-kaydirilarak)
- [Yakın yağmur: periyodik döşeme yoğunluk gradyanı taşıyamaz](#yakin-yagmur-periyodik-doseme-yogunluk-gradyani-tasiyamaz)
- [İz dokusunun çözünürlük seviyesi: makalenin kuralı tek seviye veriyor](#iz-dokusunun-cozunurluk-seviyesi-makalenin-kurali-tek-seviye-veriyor)
- [Langer'ın kendi makalesi "shower door" diyor — silme kararı yazarlarca doğrulandı](#langerin-kendi-makalesi-shower-door-diyor-silme-karari-yazarlarca-dogrulandi)
- [Yağış sıcaklıktan koparıldı](#yagis-sicakliktan-koparildi)
- [Kar tanesi terminal hızı dayatılmıyor, fizikten çıkıyor](#kar-tanesi-terminal-hizi-dayatilmiyor-fizikten-cikiyor)
- [VFX grafiklerinin sınır kutusu elle yazılıyor](#vfx-grafiklerinin-sinir-kutusu-elle-yaziliyor)
- [Kar tanesinin asgari ekran boyutu Custom HLSL bloğunda](#kar-tanesinin-asgari-ekran-boyutu-custom-hlsl-blogunda)
- [Sis yoğunluğu görüşte değil sönümlemede doğrusal](#sis-yogunlugu-goruste-degil-sonumlemede-dogrusal)
- [Quad perde yok — spec §17.2 ve §18.7 Sistem B silindi](#quad-perde-yok-spec-172-ve-187-sistem-b-silindi)
- [Rüzgâr yağışa KUVVET olarak giriyor, hız olarak değil](#ruzgâr-yagisa-kuvvet-olarak-giriyor-hiz-olarak-degil)
- [Adım mesafeden çıkıyor, zamandan değil](#adim-mesafeden-cikiyor-zamandan-degil)
- [Sakin hava 0.6 m/s, bulutlar 2 m/s — iki ayrı taban](#sakin-hava-06-ms-bulutlar-2-ms-iki-ayri-taban)
- [Salınım hız değil YER DEĞİŞTİRME, ve çıktı bağlamında](#salinim-hiz-degil-yer-degistirme-ve-cikti-baglaminda)
- [Türbülans tamamen rüzgâra bağlı — spec'in `+ 0.15` tabanı yok](#turbulans-tamamen-ruzgâra-bagli-specin-015-tabani-yok)
- [VFX sistemleri dünya uzayında simüle ediliyor](#vfx-sistemleri-dunya-uzayinda-simule-ediliyor)
- [`TransformPositionVFXToWorld` uzay ayarının yerini tutmaz](#transformpositionvfxtoworld-uzay-ayarinin-yerini-tutmaz)
- [Kar yoğunluğu kapasiteden gelir, kutudan değil](#kar-yogunlugu-kapasiteden-gelir-kutudan-degil)
- [Açık günün pozlaması kara göre seçildi (-0.15 → -1.0 EV)](#acik-gunun-pozlamasi-kara-gore-secildi--015--10-ev)
- [Kar dokusu albedonun yerine geçmez, çarpan olarak girer](#kar-dokusu-albedonun-yerine-gecmez-carpan-olarak-girer)
- [İz neden ikinci bir yüzeyle çizilmiyor](#iz-neden-ikinci-bir-yuzeyle-cizilmiyor)
- [Parıltıda sınırlanan şey LOD değil, ayak izi](#pariltida-sinirlanan-sey-lod-degil-ayak-izi)
- [Kar–gök çoklu yansıması](#kargok-coklu-yansimasi)
- [Neden izin duvarı göçüyor (duruş açısı)](#neden-izin-duvari-gocuyor-durus-acisi)
- [Neden simülasyon adımı `Time.deltaTime` değil](#neden-simulasyon-adimi-timedeltatime-degil)
- [Neden duvar bir yüksekliğe kadar dik duruyor (kohezyon)](#neden-duvar-bir-yukseklige-kadar-dik-duruyor-kohezyon)
- [Neden omuz yüksekliği gürültülü](#neden-omuz-yuksekligi-gurultulu)
- [Neden çukurun karartması çok yansımayla telafi ediliyor](#neden-cukurun-karartmasi-cok-yansimayla-telafi-ediliyor)
- [Neden `SnowDentAt` bölge dışında sıfır](#neden-snowdentat-bolge-disinda-sifir)
- [İz rasterize edilmiyor, hesaplanıyor](#iz-rasterize-edilmiyor-hesaplaniyor)
- [Duruş yüksekliği: gerçekçi değer, gürültünün genliğini de düzeltti](#durus-yuksekligi-gercekci-deger-gurultunun-genligini-de-duzeltti)
- [Kar yüzeyi yer şekilleri — ölçülmüş değerlerden](#kar-yuzeyi-yer-sekilleri-olculmus-degerlerden)
- [Karın speküleri: buzun F0'ı ve gerçek pürüzlülük](#karin-spekuleri-buzun-f0i-ve-gercek-puruzluluk)
- [Sarmalı diffuse: bölen (1+W)², W ölçülmüş nüfuz derinliğinden](#sarmali-diffuse-bolen-1w²-w-olculmus-nufuz-derinliginden)
- [Sastrugi arazi ölçüsüne çıkarılamadı: RMS eğim bütçesi dolu](#sastrugi-arazi-olcusune-cikarilamadi-rms-egim-butcesi-dolu)
- [Kalite keyword'ü hiç derlenmiyordu — üç detay katmanı ölüydü](#kalite-keywordu-hic-derlenmiyordu-uc-detay-katmani-oluydu)
- [Sabit eşliği testi kırıktı ve neyi kontrol ettiği yanlıştı](#sabit-esligi-testi-kirikti-ve-neyi-kontrol-ettigi-yanlisti)
- [Uzaktaki kar neden düzdü: tek kapı üç çıktıyı birden kesiyordu](#uzaktaki-kar-neden-duzdu-tek-kapi-uc-ciktiyi-birden-kesiyordu)
- [Çukur gölgesinin yarıçapı sabitti ve üç kat büyüktü](#cukur-golgesinin-yaricapi-sabitti-ve-uc-kat-buyuktu)
- [Parıltı ölçüldü, dokunulmadı](#parilti-olculdu-dokunulmadi)
- [Rüzgâr dokusu siperdeki yüzeye çiziliyordu — bağ tersti](#ruzgâr-dokusu-siperdeki-yuzeye-ciziliyordu-bag-tersti)
- [Kar yüzeyi neden geometri oldu](#kar-yuzeyi-neden-geometri-oldu)
- [Drift ve sastrugi neden ayrıldı](#drift-ve-sastrugi-neden-ayrildi)
- [Compute shader'da iki sessiz derleme tuzağı](#compute-shaderda-iki-sessiz-derleme-tuzagi)
- [Drift eğimi duruş açısını aşıyordu](#drift-egimi-durus-acisini-asiyordu)
- [Deniz: Phillips değil TMA spektrumu](#deniz-phillips-degil-tma-spektrumu)
- [Deniz: Schlick değil tam Fresnel](#deniz-schlick-degil-tam-fresnel)
- [Deniz: çok seviyeli clipmap değil tek ızgara](#deniz-cok-seviyeli-clipmap-degil-tek-izgara)
- [Deniz: FFT boyutu keyword değil uniform](#deniz-fft-boyutu-keyword-degil-uniform)
- [Kum bandı metreyle tanımlı ama görünen şey yerdeki genişliği](#kum-bandi-metreyle-tanimli-ama-gorunen-sey-yerdeki-genisligi)
- [Kum eğim sınırı 6°, duruş açısı 34° değil](#kum-egim-siniri-6-durus-acisi-34-degil)
- [Deniz plastik görünüyordu: yansıyan gökyüzü uydurmaydı](#deniz-plastik-gorunuyordu-yansiyan-gokyuzu-uydurmaydi)
- [Deniz tek boy dalgadan ibaretti: spektrumun tek tepesi vardı](#deniz-tek-boy-dalgadan-ibaretti-spektrumun-tek-tepesi-vardi)
- [Ölçüm aracı iki koşuyu ayırt edemiyordu](#olcum-araci-iki-kosuyu-ayirt-edemiyordu)
- [Köpük hiç doğmuyordu: eşik ölçülen en küçük Jacobian'ın altındaydı](#kopuk-hic-dogmuyordu-esik-olculen-en-kucuk-jacobianin-altindaydi)
- [Kıyı köpüğü kâğıt kenarıydı: gradyan çarpanla eziliyordu](#kiyi-kopugu-kâgit-kenariydi-gradyan-carpanla-eziliyordu)
- [Köpüğün kabarcığı ve kıyının izi — dokusuz ve tampon-suz](#kopugun-kabarcigi-ve-kiyinin-izi-dokusuz-ve-tampon-suz)
- [Köpük kâğıt gibiydi: HDR ışıkla çarpılıp beyaza kırpılıyordu](#kopuk-kâgit-gibiydi-hdr-isikla-carpilip-beyaza-kirpiliyordu)
- [Köpükteki tuhaf desenler: kıvrım yönü düz suda gürültüydü](#kopukteki-tuhaf-desenler-kivrim-yonu-duz-suda-gurultuydu)
- [Kıyı çizgisi çizilmiş bir çizgide bitiyordu](#kiyi-cizgisi-cizilmis-bir-cizgide-bitiyordu)
- [Denizin rengi: kırmızısı sıfır olan bir turkuaz](#denizin-rengi-kirmizisi-sifir-olan-bir-turkuaz)
- [Kum plastik görünüyordu: ıslaklık bandının tabanı yoktu](#kum-plastik-gorunuyordu-islaklik-bandinin-tabani-yoktu)
- [Dört yüzey sisi hiç almıyordu ve derleme hatası vermiyordu](#dort-yuzey-sisi-hic-almiyordu-ve-derleme-hatasi-vermiyordu)
- [Bulut gölgesi cookie'si üç yüzeye hiç ulaşmıyordu](#bulut-golgesi-cookiesi-uc-yuzeye-hic-ulasmiyordu)
- [Alpenglow fırtınayı görmüyordu](#alpenglow-firtinayi-gormuyordu)
- [Hüzme invariantı yorumda tutmuyordu](#huzme-invarianti-yorumda-tutmuyordu)
- [Ay gölge atıyor, tooltip "atmaz" diyordu](#ay-golge-atiyor-tooltip-atmaz-diyordu)
- [`TimeOfDay` ay varsayılanı sahneden 10.25 kat sapmıştı](#timeofday-ay-varsayilani-sahneden-1025-kat-sapmisti)
- [Gölge mesafesi: belge 60, yorum 50, gerçek 150](#golge-mesafesi-belge-60-yorum-50-gercek-150)
- [Kırılma ölçütü neden pikselin kotunu okuyamaz](#kirilma-olcutu-neden-pikselin-kotunu-okuyamaz)
- ["Altı zaten suyun altında" bir gerekçe değildi](#alti-zaten-suyun-altinda-bir-gerekce-degildi)
- [Hs ve Tp neden formülden değil spektrumdan okunuyor](#hs-ve-tp-neden-formulden-degil-spektrumdan-okunuyor)
- [Kırılma köpüğünün ekranı kaplaması iki kapıyla kapandı](#kirilma-kopugunun-ekrani-kaplamasi-iki-kapiyla-kapandi)
- [F1'den silinen teşhis anahtarları (2026-08-29)](#f1den-silinen-teshis-anahtarlari-2026-08-29)
- [Arazi kutusunun dışı neden sabit derinlik olamaz](#arazi-kutusunun-disi-neden-sabit-derinlik-olamaz)
- [Gürültü hash'i dünya ölçeğinde ölçülerek seçildi](#gurultu-hashi-dunya-olceginde-olculerek-secildi)
- [Tek ölçekli bir kenar, periyodu olmasa da desen okunur](#tek-olcekli-bir-kenar-periyodu-olmasa-da-desen-okunur)
- [Göz uyumu: tavan neden `tanh`, banda neden cd/m² denmedi (2026-08-29)](#goz-uyumu-tavan-neden-tanh-banda-neden-cdm²-denmedi-2026-08-29)
- [İnceleme maddelerinden yapılmayanlar ve sebepleri (2026-08-29)](#inceleme-maddelerinden-yapilmayanlar-ve-sebepleri-2026-08-29)
- [Gökyüzünün güneşi neden sahnenin güneşinden ayrıldı (2026-08-29)](#gokyuzunun-gunesi-neden-sahnenin-gunesinden-ayrildi-2026-08-29)
- [Açık hava görüşü 25 km → 60 km (2026-08-29)](#acik-hava-gorusu-25-km-60-km-2026-08-29)
- [Palet yeşil-griye çevrildi (2026-08-30)](#palet-yesil-griye-cevrildi-2026-08-30)
- [Ayak izi: kalınlıkla ölçeklenmiş büyüklük İKİNCİ kez kalınlıkla çarpılmaz](#ayak-izi-kalinlikla-olceklenmis-buyukluk-ikinci-kez-kalinlikla-carpilmaz)

---

## Bulutlar

**Kapsama optik kalınlığa da girer.** Yoğunluk bir dönem yalnız `CloudMass`'ten, yani
yağıştan geliyordu; yağmursuz kapalı havada örtü inceliyor ve kapsama %100 iken bile
yıldızlar arasından geçiyordu. Bulutun kalınlığı yağışa bağlı değil — yağışsız stratus da
yıldızı tamamen keser. İkisinden büyüğü alınır, **toplanmaz**: fırtına kütlesi ile kapalı
hava aynı olguyu iki uçtan tarif ediyor.

**Yoğunluk kameradan bağımsızdır.** `DensityFadeValue` yoğunluğu kamera mesafesiyle
çarpıyor (`saturate(d / fadeInDistance)`). 5000 m'de bu küresel bir irtifa çarpanına
dönüşüyordu: yerde buluta ~2 km (çarpan 0.40), 20 km'de ~15 km (1.00) — **2.5 kat**. Bulut
yükseldikçe optik olarak kalınlaşıp gece simsiyah okunuyordu. Parametrenin gerçek işi
kameranın burnunda yoğun bulut oluşmasını engellemek ve o iş birkaç yüz metrede biter.
**300 m**'ye çekildi.

**Yoğunluk `WeatherState.Precipitation`'dan sürülmez** — o değer tavanla kesilmiş ve döngü
kurardı: kapsama → tepe → tavan kesimi → yağış → kapsama.

**Katman mutlak kotta** (`localClouds` açık). Kapalıyken ışın başlangıcı `(0,0,0)` oluyor
ve bulutlar oyuncuyla birlikte yükseliyordu.

**Rüzgârda maruziyet uygulanmaz** — oyuncu kayanın arkasına geçince iki kilometre
yukarıdaki bulut yavaşlayamaz.

---

## Kuşaklar ve hava dalgalanması

**Sınırlar mutlak metre değil orandır.** Tırmanışın altı yağmurda, üstü karda geçer ve dağ
donma seviyesiyle hareket eder. Sabit sınırla dağda gözle görülen tek birikme işareti

gelgit yapmaz.

**Dinginlik iki Perlin katmanının çarpımından.** Üçüncü ve çok yavaş bir gürültü nadiren
**açık pencere** açar. Pencerenin derinliği de değişken (kendi ayrı, daha yavaş gürültüsü):
sabit kalıntıyla her açılma birbirinin aynısıydı. Zirve kuşağında genlik daralır ama
**sıfırlanmaz** — sıfırlanınca yukarıda şiddet tek bir sabit sağanağa çakılıyor, saatlerce
hiçbir şey değişmiyordu.

**Açık pencere dalgalanmanın parçası değil**, ayrı hesaplanır. İçeriye gömülüyken zirvede
genlik sıfır olduğu için hiç açılmıyor ve bulut denizinin üstünde durma anı hiç
oluşamıyordu.

**Bulut kütlesi yağışı gecikmeyle izler.** Aynı değere bağlıyken yağışın durduğu karede
gökyüzü de açılıyordu; gerçekte bulut yağıştan sonra bir süre durur. Sonuç kendiliğinden
çıkıyor: kısa pencereler gökyüzünü açmadan geçer, uzun olanlar açar — ayrı bir kural yok.

**Bulut tepesi kesmesi bulut sisteminden itilir, sürücü çekmez.** Sürücü ayarın **nominal**
tavanını (7000 m) kullanıyordu: sönme 5800 m'de başlıyor, zirve 5686 m — kural hiç
işlemiyordu. Görüntüde yağış kesiliyor ama `WeatherState` yağmaya devam ettiği için sis,
ses ve zemin karı bulut denizinin üstünde de fırtına okuyordu.

anında sıçradığı için yağmurdan kara geçiş oyuncunun ne kadar hızlı yükseldiğine kalıyor.

**Rüzgâr neden iki sayı.** Esinti sürekli şiddetin üstüne biner ve tavanı aşar; aşınca
normalize değer kırpılır ama hız kırpılmaz — tanecikler hızlanmaya devam ederken ses ve
görüş tavana yapışık kalıyordu.

**Yükseklik neden "ulaşılan seviye".** Dağ sürekli yükselmez; sırt aşılıp boyuna inilir.
Anlık Y'ye bakılsa hava her inişte geri sarardı.

---

## Sis, görüş ve hava sinyalleri

### Bulut sisi: derinlik yazmayan geçişte mesafe uydurulmaz

**Kural:** bulut birleştirme geçişi araziye/göğe sisi bulutun **gerçek** mesafesinden
(`meanDistance` türevli derinlik dokusu) uygular. Bilinmeyen mesafeye sayı **uydurulmaz**.

**Gerekçe:** bulut yarı çözünürlükte çiziliyor, renk bilinear büyürken derinlik nokta
örnekleniyor. Uyuşmadıkları bleed halkasında paket derinliğe uzak düzlemin bir tık berisini
(`CLOUDS_RAW_FAR_CLIP_VALUE`) koyuyordu; sis o ~70 km sahte mesafeyle doyup bulut çevresine
siyah kontur basıyordu. Combine geçişi `Blend One SrcAlpha` ile derinlik **yazmadığı** için
uydurmanın tek etkisi bu konturdu. Kaldırıldı — halka artık uzak düzlem derinliğini korur,
`hasCloud` false olur, saydam bleed sissiz geçer.

**Denenip elenen:** (1) pikseli elemek → "1px sissiz halka da kontur" korkusu, ölçümle
çürüdü; (2) mesafeyi ışın menziline `clamp`lamak → sahte mesafe zaten menzilin içinde,
işe yaramadı; (3) sisi kapsamayla ağırlıklamak → sahte mesafe doygun olduğu için yine
görünüyordu. Üçü de yanlış yeri hedefliyordu; kök sebep tek: uydurma.

### Katmanlar

**Üç katman toplanır, çarpılmaz.** Çarpım üç ayrı belirtinin kaynağı oldu: derin profil
bulut denizini siliyor, sığ profil zirvede uzak sırtları karton bırakıyor, ikisini tek
kanaldan geçirmek de ikisini birden bozuyordu.

**Şafak denizi ayrı kanaldır.** Tek kanaldan geçince deniz yerleşik havanın profiliyle
yayılıyor, yol boyunca optik derinlik **on kat** çıkıyor ve şafakta yukarı bakan oyuncuya
bulutlar tamamen siliniyordu. Bir zamanlar yerleşik havayı da şafakta kalınlaştıran ayrı
bir çarpan vardı — aynı olayın iki mekanizması, ve derin olanı (yarı yükseklik 1400 m)
2.6 km yukarıdaki bulutlara kadar uzanıyordu.

**Katmanın derinliği yağıştan sürülür.** Tek yoğunluk hem yatay hem dikey yolu beslediği
için görüşü tek başına oynatmak ikisinden birini hep bozuyordu: görüşü açınca bulutlar
geldi ama arazi pusu bitti, kısınca sis geldi ama bulutlar silindi. Sabit derinlik de
yağışla çelişiyordu — sığ bırakınca 1000 m kotta sağanakta 5 km görüş çıkıyordu.

**Görüşün fiziksel tavanı.** Katman yükseldikçe seyreldiği için yoğunluğa bölmek zirvede
sınırsıza gidiyor ve ekranda "3900 km görüş" yazıyordu.

**Esinti okunmaz.** Ham hız okununca rüzgârın saniyelik sarsıntısı kayan dokuyu seğirtiyor
ve zamansal birikimin altında bulut kenarlarını blok blok pikselleştiriyordu.

**Ataklarla gelir, sürekli şiddetle değil.** Kar taşınımı sürtünme hızının **küpüyle**
saniye fışkırır, diner, tekrar gelir — sürekli şiddetle sürülünce perde hiç kesilmeyen düz
bir akıntı oluyordu.

**Kretten fışkırır.** Tek arazi örneğiyle kret ayırt edilemiyor ve perde yamaca eşit
yayılıyordu. İleri/geri iki örnek gerekiyor.

**Kuyruk şart:** etki kretin çevresinde simetrik kaldığı sürece "savrulan duman" hiç
oluşmuyordu.

**Dikey profil kuvvet yasası, üstel değil.** Süspansiyon Rouse tipi dağılır; üstel sönüm
kuyruğu erken bitiriyor ve tüyler kısa kalıyordu.

**Yükseklik yerden ölçülür.** Deniz seviyesine göre sönen bir profil sırtın üstünde hiç
görünmez, vadide ise boğar.

**Sürüklenme kaynağını tüketir.** Bu bağ olmadan perde sonsuza kadar aynı şiddette
akıyordu.

vekiliydi ve sıfırın çok altındaki bandı bile "ılık" sayıp eritebiliyordu. Faz değişimi
enerji ister; enerji yoksa kar durur — dağın karının kalıcı olmasının sebebi bu.

**Rengi ile parlaklığı ayrı kurulur.** Gök örneği (`_HeightFogSunColor`) HDR ve üst
sınırsız, üstelik en büyük olduğu yer güneş yönündeki ufuk — yani şafak ve akşamüstü.
Olduğu gibi renk olarak geçirilirse 1'i aşar, beyaza kırpılır ve dağ fosforlu görünür;
katsayı kısmak yalnızca eşiği öteler. Seviyenin ölçüsü ufuk parıltısı değil **güneşin
yüksekliği**: şafakta yamaç hâlâ gölgedeyken perde ufkun parlaklığını alırsa dağ
aydınlatılmış görünür.

**Akış alanı bilerek kaba.** Işın 8 adımda integre ediliyor; daha ince desen undersampling
üretiyor ve perdenin içinde yağmur yağıyormuş gibi titreme bırakıyor. Sektörün çözümü
temporal reprojection + blue noise + TAA, bizde TAA yok (`DECISIONS.md`).

### Hava haritası ve bulut biçimi

**İmzaya sürüm dahil.** İmza yalnız ayarlardan kurulduğu sürece kod değişikliği haritaya
hiç yansımıyor, menüden "yeniden pişir" bile eski sonucu üretiyordu. Sürüm üreticinin
içinde durur çünkü onu artırmayı unutan, algoritmaya dokunan kişidir.

**Tazeleme editör yüklenirken**, sahne kurulumunda değil — düzeltmeyi değerlendiren kişi
eski haritaya bakmasın diye.

**Pişirme asset'in üstüne yazar.** Silip yeniden kurmak GUID'i düşürüp sahnedeki
başvuruları koparıyordu.

**Çekirdekler eliptik.** Mükemmel daire diye bulut yoktur; dairesel ayak izi bulutları
silindir olarak okutuyordu.

**Ayrı "kimlik" hash'i söküldü.** Sürekli kanalların `frac`'ı tamsayı geçişlerinde sıçrıyor
ve bulut içine fermuar kenarlı şerit perdeler çiziyor — **süreksiz fonksiyon sürekli alana
uygulanamaz.**

**Hava kolonsal.** 3B'den örneklenen dönemde gövdeye bağsız yüzen parçalar (kubbe/adacık)
doğuyordu.

**Tavanı yalnız kolon-sabit alanlar sürebilir.** Yüksekliğe göre değişen bir alan tavanı
sürerse üst yüzey o alanın izo-yüzeyi olur ve 3B pürüzsüz gürültünün izo-yüzeyi yuvarlak
kapaktır — yani kubbe. Fırtına dolgusunun dalgası bu kurala aykırıydı (`sec4.r`, örneğin
y'sini taşıyordu) ve yüksek kapsamada tepeleri kubbeleştiriyordu.

**Zarf kapsamayı kısar, şekli çarpmaz.** Çarpanla inceltince eşiği tepede yalnız gürültü
zirveleri geçiyor ve hayatta kalanlar sivrilerek iğneye dönüyordu — bulut üstlerindeki koni
ormanının kaynağı.

**Kapsama–gök bağı alt-doğrusal (üs 0.65).** Doğrusal bağ "%35 bulut" derken göğü %22
örtüyor ve min %30 kuralını deliyordu.

**Dikey uzanım ekranda kalibre edildi** (F1 atölyesi). Sayılar **birlikte** ayarlandı —
birini tek başına eski değerine döndürmek silindiri geri getirir.

**Fırtına dolgusunun tabanı varyanslı.** Sabit taban eşiği her yerde doyurup örtüyü tek
parça halıya çeviriyordu.

**Döşeme tekrarına iki önlem yetmiyor.** Boş bölgelerin kanalları 48 km periyotlu alanlarla
boyanmasa tek doku değişkeni 2.86 km'de döşenen şekil gürültüsü kalıyor ve örtü zirveden
kafes gibi okunuyordu; ikinci örneklemin (37°, ×1.26) kendi periyodu da menzilin altında,
zirveden kafes yine okunuyor (ölçüldü, ekran görüntüsüyle).

### Işık ve örnekleme

**Kapsama yoğunluğu ayrıca çarpmaz.** Nubis eşik remap'inin ardından kapsamayla bir daha
çarpar; ikisi ekranda karşılaştırıldı ve bizimki seçildi (`DECISIONS.md`).

**Sonda adımları üstel.** Kazanç sayıda değil dağılımda: dörtte ilk adım 80 m, beşte
38.7 m — gölgenin belirleyicisi ilk birkaç yüz metre olduğu için yakın gölge iki kat ince
çözülür. Koni çekirdeği sürgü aralığının tamamını (2–8) ayrı yönle karşılar; eksik kalsaydı
üstteki örnekler aynı yönü okuyup maliyeti tekrara harcardı.

**Sonda ön yüzde tam örnekler** (HZD kuralı). Kabarık kenarların kendi gölgesi onları üç
boyutlu okutan şey; erozyonsuz gölge kenarı düz bir zar gibi aydınlatıyor. Derinde ışık
zaten sönmüş.

**Sonda kendi mesafesini taşır.** Sabit "çok uzak" varsayınca detay sönümü ve kenar eğrisi
birincil ışınla ayrışıyor, gövde bir alandan gölge başka alandan okunuyordu —
beyazlaşmanın eski kökü bu sınıftandı.

**Geçiş harmansız.** Üstel karışım kontur şeritleri basıyordu; blok deseni yalnız kompozit
çadır filtresiyle örtülür ve filtre yarıçapı bulanıklıkla birebir takas eder.

**Boş bölge sıçraması tam adım katlarında.** Serbest uzunlukta yapılınca ışının örnekleme
kafesini kaydırıyor — komşu piksellerden biri sıçrayıp öbürü sıçramayınca buluta sabit
metrelik kuantalarla giriliyordu.

**Mercek sapması kapsamayı çarpar.** Şekil alanına toplamsal binerken geçişi dikleştirip
kuyruğu inceltiyordu.

**Jitter yerel koordinat + sin'siz hash.** Gezegen-merkezli sin hash'i fp32'de dejenereydi
ve ışık kabukları çıplak soğan halkaları basıyordu — halka sagasının gerçek kökü.

**Işık kapısı dar.** Geniş bant doğrudan güneşi kısıp bulutları ambient'e bırakıyor, renk
siliniyordu.

**Yoğunluk eğrisi uzun kuyruklu kuvvet** (t^~3): smoothstep erken doyup blok yapıyordu.
**Mercek profili toplamsal** — kapsamaya çarpım üç kez ölü vida çıktı (kapı/erken-çıkış
mıhları) — ve katsayısı bilerek gürültü genliğinin altında: aşarsa sınırı pürüzsüz harita
konturu çizer, kenar duvarlaşır. **Saçak kapısı geniş bantlı** (0.10–0.42); dar bant dış
sınırı keskin duvara çeviriyordu. **Peçe rengi süzer**, alfa kırpımı dağı bulutun içinden
gösteriyordu. **Kapalı örtünün taban çeyreği benekli** — tekdüze gri tavan "bulut yok"
okunuyordu.

**Yağış karartması tek çarpan.** Ayrı çarpan aynı işi iki kez yapıyor ve kalın gövdede
ambient dörtte bire iniyordu — bulutlar siyahımsı griye kaçıyordu.

**Bulut ortamı radyans ister, ışınım değil.** Dönüşüm π. Aynı fark froxel sisinde
ölçülmüştü (probe DC 0.156, sis rengi 0.492 — oran 3.15 ≈ π) ve orada düzeltilmişti; bulut
tarafı aynı hatayla kalmış, gece bulut kendi ışımasını π kat eksik alıyordu.

**Gezegen yarıçapı gerçek değeriyle durur.** Küçültülünce deniz zirvenin hemen dibinde
bitiyor (235 km'de 13 km) ve ufuktaki bulutlar yok oluyordu. Kenarı **sönüm** saklar,
geometri değil — sönüm mesafesi denizin ufkuna eşitlenirse ufuktaki bulut tam karışıma
girip kaybolur.

**Sanat yönü harmanı pişirmede.** Kaba sıçrama haritası bu sonuçtan türüyor; çalışma
zamanında harmanlansaydı sıçrama boyanmış bulutun üstünden atlardı. Pişmiş harita adı
boyanan dosyanın **içerik** hash'ini taşır — aynı dosya yeniden boyandığında ad değişmiyor
ve harita bayat kalıyordu.

### Renk ve gökyüzü

**Bulut ve sis rengi aynı kaynaktan.** Ayrı sabitler gökyüzü kızarırken sisi soluk
bırakıyordu.

**Batış kızıllığı eklenir, çarpmaz.** Batışta bulut zaten karanlık; sıfıra yakını kızılla
çarpmak siyah bırakır ve kızıllık hiç görünmüyordu.

**Şafağın altın ucu sabit ve bilinçli abartma.** Altın saatte (+5°) fizik zaten sabite eşit
(0.569 / 0.571); fark yalnız doğuş anında ve orada fizik haklı — gerçek doğuş, yirmi dakika
sonrasından sönüktür. Sabit o ilerlemeyi düzleyip doğuşu **3.7 kat** parlatıyor. Aerosol,
adım sayısı ve örnek yüksekliği hipotezlerinin üçü de ölçülüp çürütüldü (`DECISIONS.md`).

**Kazanç tek.** İki ayrı sabit vardı, aynı adı taşıyorlardı ve farklı işler yapıyorlardı;
biri değişince öteki yerinde kalıyor, gökyüzü ile ondan türeyen değer ayrışıyordu.

**Çok saçılma şart.** Olmadan batış ufkunun mavi kanalı tükeniyor, doygunluk 0.98'e
çıkıyor ve renk turuncu değil saf kırmızı oluyordu; ayrıca gök tamamen karardığı için
kazanç şişiyor ve en parlak yer ton eşlemede kırpılıyordu.

**Sis renginin seviyesi gökten.** Sabitken ölçüldü: gök gündüz–gece arası ~**230 kat**
değişirken sis rengi **9.6 kat** değişiyordu — gündüz 2.2 kat fazla koyu, gece 11 kat fazla
parlak. Gece sisin örttüğü her şey 3.5 durak yukarı kalkıyor, "sisi kapatınca gece
gerçekçi oluyor" belirtisini üretiyordu. Katsayı üç renge birden uygulanır; ayrı ayrı
oturtmak aralarındaki oranları ezerdi (zenit'in yağışa bağlı payı, gölge tarafının şafak
payı o oranların içinde).

**Hava ve gökyüzü tek fonksiyon.** İki ayrı formül tutulduğu sürece her hava/saat köşesinde
ayrışıp dağı "parlayan karton" olarak gökten koparıyorlardı.

**İnen ışının integrali ayrı.** Eğim kırpması işareti yutuyordu ve inen ışın "ufka paralel"
sayılıp yolu tavana yapışıyordu — arazinin bittiği yerde ekranın alt yarısı tam sise, yani
karanlık aşağı-hava rengine boyanıyordu.

**Bank çarpanı yalnız kameranın yerelinden.** Bank alanı katmana yansıtılınca sinüs deseni
gökyüzüne şerit şerit basılıyordu.

**Yüksek katman aynı fonksiyona karışır.** Ayrı düz bir taban renge karışıyordu: şafakta
hacimsel katman güneşe doğru altına gömülürken yüksek katman her yönde aynı griye gidiyor,
iki katman arasında hem renk hem kenar farkı duruyordu. Ufuk sınırı sert eşikte alfa sıfır
olmadığı için gökte jilet gibi yatay bir çizgi bırakıyordu.

**Güneş diski söner, hâlesi kalmaz.** İkisi aynı katsayıyla dururken fırtınada güneş, görüş
140 m olmasına rağmen keskin bir leke bırakıyordu. Sönüm sisin kolon optik derinliğinden
(`β/k`): berrak 0.91, yağışlı 0.22, fırtına 0.00.

**Koschmieder 3.9.** Önceki 1.6, sisi olduğundan iki kat seyrek gösteriyordu.

**Test kilitleri bileşeni kapatmaz.** Bileşen kapatılınca `StormIntensity`/`ClearWindow`
donuyor ama okunmaya devam ediyordu: F1 sürgüsü yağışı, görüşü, sisi ve rengi sürerken
bulutlar kilitlenme anındaki hâlde kalıyor, tek hava durumu iki kanala ayrılıyordu.

---

## Dağ yüzeyi

**Yön hâkim olandan, anlık hızdan değil.** Birikinti alanı `dot(worldXZ, windAxis)`
üzerinden kuruluyor ve dağın ortasında |worldXZ| yedi bin metre — bir hamlenin 0.14
radyanlık sapması bütün deseni **980 metre** sürüklüyordu (gövde 45 m).

**Kapsama ile kalınlık ayrı.** Rüzgâr sırttan alıp oyuğa bıraktığı için kalınlık düzlükte
ve çukurda birikir, dik yamaçta ince kalır. Bir parmak kar altındaki taşı gösterir, yarım
metre göstermez.

**İki ayrı kalınlık kanalı.** Tek kanalken birikinti çukurunda kayanın rengi karın içinden
geri geliyor ve rüzgâr ekseninde uzamış gri şeritler bırakıyordu. 60 cm de 90 cm de taşı
tamamen gizler; gömülme doyar.

**Birikinti alanı gerekli**, çünkü kot bandı, eğim ve maruziyet arazi ızgarasında (4.28 m)
değiştiği için derinlik dört metrenin altında dümdüzdü.

**Konkavlık okunmaz.** Yüzey haritasının konkavlık kanalı akış birikiminden türüyor ve
ızgaraya hizalı gürültü taşıyor; birikintiye girdiğinde yamaçta tarama çizgileri
bırakıyordu (F1 izolasyon anahtarıyla ölçüldü).

**Arazi ağırlığı tek kaynak.** Uçlar arası 3.0 kat; saha ölçümü rüzgâraltı yamaçta iki kat,
taze karda dörde kadar. `TerrainWindShelter` eskiden kendi kabartma hesabını yapıyordu ve
iki cevap ayrışıyordu — karın derin rüzgâraltı yığını saydığı yerde oyuncu tam rüzgâr
hissedebiliyordu. Gölgelendirme `lee`'yi anlık normalden, `hollow`'u konkavlık kanalından
ayrı ayrı türetiyordu; geometri eklenince üçüncü bir kopya çıkacaktı.

**Eğrilik çekirdeği dairesel.** Kare kutu ortalamasıydı ve kare çekirdeğin frekans cevabı
eksenlere hizalı: kanal ızgaraya hizalı bir desen taşıyor ve büyütüldüğü her yerde yüzeyde
tarama çizgisine dönüşüyordu.

**Maruziyet birikim ağırlığından, gökyüzü açıklığından değil.** Gökyüzü kanalı yönsüz: bir
çukurun tabanı göğü görmez ama rüzgâr da almaz, oysa göğü gören bir rüzgâraltı terası
taranmaz.

**Yeni gürültü örneği alınmaz** — arazi fragmanında her ek örneğin kare hızında ölçülebilir
bir payı var.

**Pütür ayrı bir oktav** çünkü taş kabartısının dalga boyu çökme höyükleri için fazla iri.

**Tazelik ayrı bir sinyal.** Kalıcı çizginin üstünde örtü zaten dolu; yeni yağan karın
kapsamaya ekleyeceği bir şey yok, o yüzden fırtına sonrası yüzey hiç değişmiyordu.

**Uzak "pırıltı şeridi" söküldü** — hücre gürültüsüyle taklit, çizik dokusu basıyor.

**Kar gece matlaşır.** Yönlü ışık aya çevrildiğinde gündüz pürüzsüzlüğü dar speküler lobla
çakıyor ve dalgalı kar normali yüzünden kamera oynadıkça yanıp sönüyordu — gece boyunca
süren sahte pırıltı. Şiddet düşünce diffuse zemine gömülüyor ama dar lob tonemap'ten sağ
çıkıyor: oran değil görünürlük değişiyor. Gerçekte ay ışığında kar cila verir, çakım vermez.

**Alpenglow gölgeye tabi.** Gölgesiz emisyon şafakta sahneyi düz bir vuruşla yakıyordu.

**Dört geçiş de yer değiştirmeyi uygular** — biri atlanırsa gölge yüzeyin altında kalır.

düşüyor, zemin beyazken tepeden yağmur yağıyordu. Yükseltme payı yumuşatma bandından küçük
kalırsa çizginin alt ucu kar kuşağının içine sarkıyor ve aynı çelişki dar bir şeritte geri
geliyordu.

**Taze kar kot ekseninde.** Tek global sayıyla dağın tamamı aynı anda beyazlıyor ve öyle
kalıyordu; üstelik birikme hızını *oyuncunun bulunduğu kotun* havası sürüyordu. Erime donma
çekilmesi buradan çıkıyor.

**Hava kaynaklı değerler global.** `UnityPerMaterial` tamponunun içindeyken
`material.SetFloat` ile yazılan değer shader'a **ulaşmıyordu**: tampon eski değerinde
kalıyor, kar maskesi her kotta kapalı okunuyordu.

---

## Gökyüzü ve gök cisimleri

**Soğurmanın tek sahibi paket.** Bizimki de uygulansaydı aynı atmosfer iki kez soğururdu.
Ölçülmüştü: öğlen ışığa `şiddet 2.55 · renk 1.00 0.88 0.70` yazılıyordu, mavi kanal
kaynakta 0.70'e iniyordu ve gökyüzü lacivert kalıyordu.

**Güneşin bandı −12°'de bitiyor.** Paket göğü ışığın yönünden ve şiddetinden hesapladığı
için güneş ufkun altında sıfırlanırsa ALACAKARANLIK DA SÖNÜYOR — 18:10'da gece yarısı
karanlığı çıkıyordu. Arazi bundan yanlış aydınlanmaz: ışık neredeyse yatay geldiği için düz
zeminde `N·L` negatif, yalnız güneşe bakan dik yamaçlar ışık alır.

**Ay ayrı ışık olmak zorunda.** Tek yönlü ışığa iki cisim sığmıyordu: ay güneşin tam
karşısında (`MoonDirection = −SunDirection`), yön bir tanedir ve devir anında disk 180°
atlıyordu. Yapısal, ölçmeye gerek yok.

**Ay gökyüzünü de aydınlatmalı.** Tek cisimken sıçrama kaçınılmazdı ve ölçüldü: 19:12'de
probe `0.00000`, 19:22'de `0.00228` — atlama tam güneşin şiddetinin sıfırlandığı anda,
çünkü gökyüzü o ana kadar güneşten sürülüyor, sonra aya geçiyordu.

**Analitik probe devre dışı** — o yol çoklu saçılım taşımıyor ve alacakaranlıkta sıfır
veriyordu.

**Probe sıçramada iki kez pişer.** `DynamicGI` gökyüzü materyalini okuyor ama materyali
render geçişi yazıyor, yani pişirme bir kare geriden görüyor. Zaman akarken görünmez; saat
sıçrayıp durunca tek pişirme eski göğü yakalıyor ve probe donuyordu. `LookController`
pozlamayı ondan okuduğu için gece sahnesi gündüz pozlamasıyla çiziliyor, her şey siyah
çıkıyordu.

**Ortam kipi `Skybox` olmalı.** `Flat` kalırsa paketin dinamik probe'u hiç devreye girmiyor
ve ortam ışığı donmuş bir renkte kalıyor; ölçüldü — probe öğle ve gece birebir
`0.223 0.293 0.420`, tepe ile taban da aynı.

**Yıldızlar için küp harita yolu ölçülüp elendi.** 512'lik yüzde teksel 0.176°, ekranda
piksel 0.047° — yıldız zorunlu olarak dört piksel ve bilineer süzmeyle leke oluyordu; bir
piksel için 2048'lik yüz, yani **201 MB** gerekirdi. Durağan doku ayrıca titreyemez.
~6000 yıldız, kadir 0–6, sayım küp kökle dağıtılıyor (kadir başına ~2.5 kat, gerçek sayıma
yakın).

**`(1 − skyOpacity)` gündüzü kapatmıyor** — bir dönem öyle varsayıldı, ölçüm çürüttü:
zenitte gündüz opaklık ~0.2, yıldızların %80'i geçiyordu ve sabah 8'de gökyüzü yıldızlıydı.

**Ay şiddeti 0.0199, albedosu 0.586 0.653 0.818.** Doğan ay uzun atmosfer yolundan geçip
sarıya kayıyor; taban soğutuldu ki soğurma sonrası sonuç nötre yaklaşsın. Doygunluk bir kez
düşürüldü ve ton lineer uzayda eski ışımaya ölçeklendi (Y = 0.3844): 10°'de eski renk
`1.00 0.80 0.43`, yeni renk `1.00 0.87 0.56`.

**Güneşin yönü/rengi hava sürücüsünden geçmez.** İkinci bir yol "gökyüzü kızardı ama
gölgeler öğle yönünde" çelişkisini üretirdi.

---

## Bilinçli kuralların gerekçeleri

**"Gündüz"ün iki ölçüsü.** `DayFactor` (−0.22 → 0.45) geniş olmazsa sabah 8 ile öğle 12
aynı parlaklıkta görünür; `sunOverMoon` (−0.12 → 0.04) dar olmazsa güneşle ay ufukta yarım
saat boyunca aynı anda yanar.

**Aşağı inmek havayı geri sarmaz.** Boyun geçişleri fırtınayı kapatmamalı; ulaşılan
seviyenin belli bir mesafe altına inene kadar hiçbir şey değişmez, ötesinde yavaş geriler.

**Bulut kütlesinin üstünde yağış yok.** Tırmanışın son bölümü bulut denizinin üstüne
çıkıştır: yağış diner, gökyüzü açılır, altında deniz kalır — rüzgâr sönmez. Ölçü kütlenin
bittiği kot, çünkü yoğunluk profili tavana varmadan sıfırlanıyor: en kabarık bulut bile
kendi tepesinin yarısından itibaren sönmeye başlıyor, yayvan olanlar katmanın alt üçte
birinde bitiyor. Tavana yaslanmış sönüm geç kalıyor ve oyuncu denizin üstünde dururken
üstüne kar düşmeye devam ediyordu.

**Kapsamanın alt sınırı.** Sınırın altında gökyüzü boş ve bulutlar cılız görünüyor. Tek
istisna açık pencere ve bilinçli: iki kural aksi halde çelişiyordu — sürücü "bulutlar
aralanır, zirve görünür" diye söz verirken taban o anın hiç gelmemesini sağlıyordu.

**Kapsama eğrisinin dikleşmesi.** Adı "lead" olduğu sürece kod yapmadığı bir şeyi vaat
ediyordu.

**Taze karı şiddet biriktirir.** Çisenti de sonunda örter ama sağanaktan çok daha uzun
sürede; eşikle sürülürse şiddet örtüyü hiç etkilemez, yalnızca açıp kapatır.

**Arazi gölgesi ufuk haritasından.** Gölge haritası kullanılınca üçgen silüetlerin gölgesi
sırtlarda testere dişiydi; araya giren ışın yürüyüşü de kenarda ya jilet ya nokta bırakıp
iki kez geri alındı. Ufuk alanı pürüzsüz: ne üçgen var ne rastgelelik. Gölge menzili
sınırsız, kenar güneşin ufka açısal yakınlığıyla yumuşuyor.

**Bulut ve arazi aynı hava.** Yer seviyesinin görüşü olduğu gibi yükseklere taşınınca,
dağın kilometrelerce net göründüğü havada bulutlar birkaç yüz metrede yok oluyordu.

**Bulutun boyu tipten, metre cinsinden.** Ayrıca bir **tavan kanalı** vardı — çekirdek
başına kubbe, MAX ile birleşen, bulanıklaştırılan, sonra shader'da beş çarpandan geçen
ikinci bir yükseklik kaynağı. Ürettikleri ekranda tek tek görüldü: dar kolonun komşusundan
tavan miras alması (**parmaklar**), çarpanların üst üste binip tavanı sıfıra indirmesi
(**dümdüz çökmüş örtü**), tavanın yatayda hızlı değişmesi (**sivri uçlar**). Kanal ve
zinciri silindi. Boy metre, katman oranı değil: HZD'nin hacimsel katmanı 2.5 km, bizimki
5.3 km (kümülonimbusa yer açmak için); oran kullanılsaydı katmanı kalınlaştırmak bütün
bulutları birlikte uzatırdı — tavan kanalı zaten bunu telafi etmek için icat edilmişti.

**Zarf çarpar.** Bir dönem eşiği yükseltiyordu çünkü çarpınca tepeler iğneye dönüyordu; o
gözlem doğruydu ama sebep zarf değil sönüm bandının **genişliğiydi** — tavan kanalı
yüzünden ~1.2 km'ye yayılıyor ve gürültünün özellik boyuyla (~1 km) yarışıyordu. Metre
boyla bant her tipte 180–456 m, özellik boyunun altında: daralma pürüzsüz, tepe kubbe.

**Kolon-sabit alan yüksekliği süremez** — sürerse desenini dikey sütun olarak basar; tanımı
zaten "dikeyde değişmeyen".

**Hava perspektifi bulutun uzaklığından.** Işın görüş mesafesine göre kesiliyor (maliyetin
ana kaynağı) ama durduğu nokta bulut içeriğine göre basamak basamak oynuyor; katmana giriş
açısı yalnız yüksekliğe bağlı olduğu için o basamaklar gökyüzüne eksen etrafında simetrik,
iç içe halkalar olarak biniyordu.

**Görüş sınırında şekil de kaybolmalı.** Şekil alfada taşınıyor; perspektif kapandığında
kapsama da doldurulmazsa düz gri görülmesi gereken yerde silik siluet kalır.

**Örnekleme kafesi tek.** Adım boyu sahne derinliğine bağlanınca komşu pikseller farklı
kafeste örnekleniyor ve **arazinin silüeti buluta desen olarak basılıyor**.

**Işına bağlı dallanma yasak.** Böyle bir dal, geçirgenliğin eşiği geçtiği YÜZEYİ uzayda
çiziyor: bulutun ortasında makasla kesilmiş düz beyaz ada (yoğunluk dalı), kenarda koyu zar
(sonda dalı), ikinci bir halka ailesi (sonda aralığı dalı). Üçü de yaşandı ve söküldü.

**Döşeme kırıcı.** Taban gürültüsü dünyada 2.86 km'de aynen tekrar ediyor; yakından bir-iki
tekrar görünür ve göz seçemez, zirveden yüzlercesi aynı anda görünür ve desen okunur.
37°'lik ikinci örneklem, 7.7 km'lik 3B büküm ve 5.7 km'lik kolon warp'ı kendileri de
kafestir. Şekil alanına 48 km periyotlu analitik bir kaydırma biner (genlik 1400 m =
döşemenin yarısı; gradyan 0.23, uzay katlanmaz) ve **ikinci örnekleme de aynısı uygulanır**
— yalnız birincisi bükülseydi ikincisi kendi tekrarını olduğu gibi taşırdı.

**Tanecik yoğunluğu bükülmüş eğriden.** Doğrusal olsa hafif yağışta ekran tanecikle
doluyor.

---

## Yağış, ses ve şimşek

**Yağış payı yumuşatılır** (~2.5 s), yoksa bulut kenarından geçerken yağmur bıçak gibi
kesiliyordu.

**Tane kendi rengini seçmez.** Kendi ışığını üretmiyor, göğün ışığını saçıyor; havanın
rengi çarpanla parlatılınca şafakta turuncu, gece koyu, şimşekte parlak oluyor — hiçbiri
ayrıca ayarlanmıyor. Sabit bir beyaz, kapalı gökyüzünün önünde patlayıp taneleri yıldız
gibi gösteriyordu.

**Biçim kümelenmedir.** Havada süzülen şey yüzlerce kristalin birbirine yapışmış hâli;
kristalin kolları bir iki milimetre ve ancak göze değdiğinde görünür. Altı kollu silüet
mikroskop görüntüsünü gökyüzüne koymaktı.

**Damla gevşeme süresiyle uyar.** Süzülmesiz hız, her hamlede bütün yağmuru aynı karede tek
parça yatırıyordu. Dönme ve girdap sürekli şiddete bağlanınca aynı tane hızlanırken
dönmesi sabit kalıyordu.

**Band geçişi esintiye bağlanmaz.** Bağlansaydı dingin ve fırtına karışımı sekiz saniyede
bir yer değiştirir; rüzgârın sertleştiği değil, sesin oraya buraya kaydığı duyulurdu.

**Varyasyon geçişi band susmasını beklemez** — dingin band ancak şiddet uca dayandığında
susuyor ve pratikte tek klip dönüyordu.

**Mesafenin tek sahibi `ThunderPlayer`.** İkisi ayrı seçilseydi bir buçuk saniye sonra
gürleyen bir gürültü, sekiz yüz metre ötede çakmış bir ışığa ait olurdu. Sekiz kilometre
gerçekten yirmi dört saniye demek.

**Şimşek ışığı yönlü kalır.** Çakma iki kilometrenin üstünde olduğu için arazi boyunca
yayılan gradyan zaten küçük (beş yüz metre ötedeki bir çakma için 2.3 kat), buna karşılık
menzili tüm sahneyi kaplayan bir nokta ışık Forward+ kümelemesini işlevsiz bırakırdı.
Baskın ipucu ("yakın çakma kör eder, uzak olan soluk kalır") tamamen ters kare sönümden
geliyor.

**Bulutun parlaması ışın yürüyüşünün içine konamaz** — o on altı kareye yayılıyor ve
parlama blok blok titrerdi.

**Sis de parlamalı.** Sisin rengi sabit tutulunca fırtınada — şimşeğin çaktığı tek havada —
görüş yedi yüz metreye düşüyor ve arazinin büyük kısmı o değişmeyen rengin altında
kalıyordu: yüzey aydınlansa bile üstü örtülü olduğu için görünmüyordu.

**Parlama yerde toplanır, yönde değil.** Yön mesafe taşımıyor — yaklaştıkça büyümesi
gereken leke sabit açıda kalıyordu.

**Kol yalnız yakında.** Gerçekte de uzak şimşek kolunu göstermez: araya giren bulut ve hava
kanalı yutar, geriye denizin aydınlanması kalır.

---

## Volumetrik sis

**Sis rüzgâr barınağını OKUMAZ.** `TerrainWindShelter` içindeki sırt algılayıcı
(`crest`/`lee`) 60–80 m'lik keskin eşikler taşıyor, froxel ızgarası ve ışın örneklemesi
o eşiklerin üstünden atlıyor ve kamera kıpırdadıkça yer değiştiren dikey şeritler
kalıyordu. Kodda sis tarafında tek bir `Shelter` referansı yok; bağ bilerek kurulmadı.

**Alanlar sinüs toplamıdır.** Bank ve akış alanı iki sinüsün çarpımıydı ve yorumu "çarpım
tekrar desenini kırar" diyordu; kırmıyor — `sin(k₁·p)·sin(k₂·p)` ayrıştırılabilir bir
ifadedir ve düzenli bir kafes üretir.

**Geçiş sırası ve `+2`.** `+2` gök sisini paketin `Opaque Atmospheric Scattering`
geçişinden sonraya düşürüyor; aynı anda çalışırlarsa siluet pikselini biri "gök" biri
"geometri" sayıp çift işliyor ve tek piksellik kontur bırakıyorlar. Gök sisi bir ara
bulutlardan SONRAYA alınmıştı — o zaman bulutu da sisliyordu ama **sonsuz mesafeden**;
bulut 2 km'de duruyor. Bir katmanı komşusunun mesafesiyle sislemek ya çift sayım ya yanlış
mesafe demek, ikisi de bulut kenarında sınır bırakıyor.

**`FogPath` neden ayrı döndürüyor.** Bulut önceden çarpılmış geliyor (`xyz` kapsamayla
ağırlıklı, `w` arkasını geçiren pay) ve saçılım payının bulutun kapsadığı orana
ölçeklenmesi gerekiyor.

**Ortamın seviyesi sis renginden.** Probe'un SH'si yüzey aydınlatması birimindedir; ortamın
istediği ortamın içeri saçtığı radyans. Ölçüldü: probe DC luminansı **0.156**, sis rengi
**0.492** — oran **3.15** (≈ π), ve o farkla ara mesafedeki puslu sırtlar kayboluyordu.

---

## Işığın rengi

**Gölge çizgisi yürümeli.** Sabit bir irtifa bandı dağın tamamını birlikte pembeleştirip
birlikte söndürüyordu; yapay duran şey buydu. Dünya'nın gölgesinin yüksekliği: 0.5° →
240 m, 1° → 975 m, 1.5° → 2190 m.

**Artçı fazda kaynak noktasal değil.** Güneş battıktan sonra aydınlatan şey kızıla
boyanmış bütün gökyüzü; yönlü sönüm ve güneş yönlü arazi gölgesi o fazda anlamsız.

**Kızıllık hesaplanır.** Sönüm üstel olduğu için iki renk arasında doğrusal geçiş bunu
üretemiyordu. En parlak kanal bire oturtulur — ölçekleyip birin üstüne çıkarmak ton
eşlemenin doygunluk düşürdüğü bölgeye taşıyor ve rengi beyaza çeviriyordu.

**Hava kütlesi ufuk altında sabitlenir** (ufukta ~22'ye tırmanır). Eski taban onu güneş
ufka varmadan kesiyordu: renk turuncuda kilitleniyor, batımın kızıl fazı hiç üretilmiyordu
— zincirin hiçbir tüketicisi kızıl gösteremezdi, kaynakta yoktu.

**Altın uç açık yazılır** çünkü süzülmüş güneşten çarpımla sarı üretilemez (yeşili
tükenmiş).

**Altın saat kademesi gerekli:** gece↔gündüz karışımı bu saati soğuk ve soluk basıyordu,
palet ne kadar kızıl olursa olsun ekrana pastel geliyordu.

---

## Renk düzenlemesi

**Pozlama karanlık ucu kaldırmaz.** Bir kez denendi: gece göğü ton eğrisinin dibinde kalıp
keskin sınırlı siyah bir bölge ürettiğinde `adaptShare` yükseltildi, belirti kapandı ama
parlak uç da yükselip gece sahnesini aydınlattı (`DECISIONS.md` → "Gecedeki fasulye
kapandı").

**Bloom eşiği faz karışımına bağlı.** `PHASE_LOBE_BLEND` 0.5'ten 0.15'e indirilince güneş
çevresindeki bulut **1.7 kat** parladı ve mevcut eşiği (1.10) aşıp bloom'a girmeye başladı
— deste kendisi hâle üretiyordu. Eşik beş ön ayarda da aynı oranla yükseltildi (altın saat
1.10 → 2.00). Ölçüm `SYMPTOMS.md` → "Şafakta güneşten uzak bulutlar yeterince kararmıyor".

---

## Arazi

**Ufuk haritası noktanın kendi eğimini çıkarır.** Eğimli düzlemde "ufuk güneşten yüksek"
ile "N·L ≤ 0" birebir aynı koşuldur; ikisi birden sayılırsa gölge iki kez uygulanır.

**Işıklandırma normali yer değiştirmeyi bilmeli.** Biri kullanmazsa siluet kabarır ama
ışık altındaki düz yüzeyi aydınlatır.

**En parlak kanal alınır** çünkü `Tint()` rengi aynı kanala göre normalize ediyor — böylece
renk ve şiddet aynı eğriyi izliyor.

**Kar yansıması gökyüzünden büyük.** Sahnede GI yok; gölgedeki bir noktanın çevresini güneş
vuran kar sarıyor ve kar albedosu 0.8.

**Tohum hash köküne uygulanır**, çağrı yerlerine değil — yeni katman eklendiğinde kaydırmayı
unutmak mümkün olmasın diye.

**Halkanın sönümü Chebyshev.** Arazi kare, köşe 15√2 = 21.2 km'de; yarıçap sönümü orada
silsileyi geri getiriyordu.

**Yalıtım halkası neden gerekli.** L0 bir silsile üretiyor ve kütle 1500 m eşiğinde 379 km'ye
uzanıyor; oyun alanı ne kadar büyük olursa olsun kenarda kesilir. Silsile ancak oyun alanının
ötesinde geri geliyor — o da uzak bantların işi.

**Nyquist sınırı.** Hücre 7.324 m → taşınabilir en kısa dalga 14.65 m. 320 m tabandan 8
oktav istenince son üçü (10, 5, 2.5 m) sınırın altında kalıp 2 hücrelik zikzak olarak geri
katlanıyordu; pişmiş haritada anomali 14.7 m'de, tabanın 3.5 katı. Ayrıntı `SYMPTOMS.md` →
"Arazide düzenli testere".

## Yağış perdesi — makaleden sapılan üç yer

Perde `[Langer 2004]`'ten port edildi. Portun üç yerinde makalenin yaptığı şey yerine
kendi kısayolum duruyordu; üçü de ekranda göründü ve kullanıcı bildirdi.

**Döşeme başına bağımsız sentez.** Makale her ekran döşemesi için AYRI IFFT alıyor
(`§7`), yani komşu döşemelerin gürültüsü ilişkisiz. Ben tek doku pişirip döşemeler
arasında küçük bir kaydırmayla (`tileIndex * 0.37`) paylaştırdım ve bunu dosya başlığına
"θ saf dönmedir, C saf zaman ölçeğidir" diye çözülmüş gibi yazdım. İki iddia da doğru,
ama üçüncüsü — bağımsızlık — hiç ele alınmamıştı. Komşular desenin neredeyse aynı yerini
okuyunca ekran tek lekenin ızgarasına döndü. Kullanıcı: *"niye bu kadar düzenliler"*.
Karşılığı döşeme indisinden hash: bağımsız sentezin ucuz karşılığı, aynı gürültünün
ilişkisiz bölgeleri.

**Genleşme odağı — sonunda tamamen söküldü.** Önce odağı, akış yönünde 1000 m öteye
konan bir dünya noktasının izdüşümünden buluyordum; nokta kameranın arkasına düşünce kod
ekran merkezine sıçrıyordu. Onu kaybolan noktayla düzelttim (yön vektörünü `w = 0` ile
izdüşümden geçirmek), ışınsal ve paralel kip arasına sürekli geçiş koydum. Dönme
GEÇMEDİ: *"sağ sol yaptıkça bazıları saat yönünde, bazıları tersine tam tur atıyorlar."*

Makaleyi satır satır okuyunca sebep çıktı ve odağın hiçbir suçu yokmuş.

**Yöntem döndürmeye uygun değil.** `§5.2`, faz kare kare ARTIMLI güncelleniyor:

    φ(ωx,ωy,t+1) := C(t)·(cosθ(t)·ωx + sinθ(t)·ωy)/√(ωx²+ωy²) · φ(ωx,ωy,t)

Genlik alanı `|α̂|` sabit; θ yalnız TAŞINMA yönünü değiştiriyor. Ben θ=0 pişirip UV
döndürüyordum. Cebiri açınca bu `α̂(R₋θ ω)` demek — zamansal kısım birebir aynı, ama
rastgele faz alanı da dönüyor. Yani desen katı cisim gibi dönüyor. Odağın iki yanındaki
döşemeler ters yönlerde dönüyordu; belirti tam buydu.

**Makale θ'yı zamanla zaten değiştirmiyor.** `§7.2`, birebir: *"the parameters C and θ
varied from one image tile to the next, but did not vary over time."* Zamanla değişen tek
örnekleri odağı sinüzoidal kaydırmak. `§8` de "θ her döşeme içinde sabitti" diye
kaydediyor. Serbest bakan birinci şahıs kamera makalenin doğruladığı alanın DIŞINDA.

Çözüm makalenin kendi ilk yapılandırması (`§6.2`, `human_condition`): tek doku, tüm
katmana dikişsiz döşenmiş, döşeme başına θ yok. Örnek sayısı 4'ten 1'e indi, döşeme
ızgarası ve hash gereksizleşti, dönme ekran geneline indi.

**Kalan kusur bilinçli:** θ değişince ekranın tamamı rijit döner. Ekran geneli olduğu için
yavaş ve sınırlı. Tam çözümü θ'yı da pişirmek (16 yön, M=64 → 3.9 MB, iki yön arası
harmanlama, 8 örnek); kalan dönme görünür olursa oraya bakılır — `DECISIONS.md`.

**Yağmur — makale çözmüş, ben yanlış okumuştum.** Perdeyi baştan "kar perdesi" diye
kurdum. Sonra "dikey iz bu çerçevede üretilemez, çünkü `ω_t` yalnız ω'nın yönüne bağlı ve
iz üreten modlar durgun" diye yazdım. Doğruydu ama sonuç yanlıştı: makale `§7`'de tam
bunu yapıyor —

> "We used vertical motion direction and a high value of C, such that the only spatial
> frequency components that contributed to the spectral sum were those in which |ωy| was
> near zero, that is, only long wavelengths in the y direction."

Mekanizma zamansal Nyquist kesmesi: `C` büyürse `|ω_t| > T/2` olan modlar sıfırlanıyor ve
geriye yalnız hareket eksenine dik modlar kalıyor. Onlar gerçekten durgun — ve **iz zaten
odur**, hareket bulanıklığına uğramış bir damla. Kar 6, yağmur 60. Bandı kaydırmaya gerek
yok; kaydırmıştım, üstelik yağmuru üç oktavdan ikiye düşürüyordu.

**Kare alma iki kez yanlış ele alındı.** Pişiricide `v *= v` vardı; onu "iki mekanizma
aynı işi yapıyor" diye sildim ve yerine shader'da ortalama çıkarma koydum. Sonra makalede
buldum: `§5.6` gerçekten kareyi alıyor ("we apply a non-linear transformation, namely we
square the α values"). O aşamada "bizde sisin işini ikinci kez yapıyordu" diye sapmayı
savundum. O da yanlıştı.

İkisi AYNI işi yapmıyor. Kare alma her yerde gradyan bırakıp tepeleri öne çıkarıyor;
ortalama çıkarma ekranın yarısını tam sıfıra kırpıp ikili maske üretiyor. Makalenin
çıktısı ayrık beyaz lekeler, bizimki gürültüydü — kullanıcı "bizdekinin bununla alakası
yok" dedi ve haklıydı.

Üstelik üç eğri üst üste biniyordu: kare (silinmişti), ortalama çıkarma, ve
olunca çıktının makaleyle ilgisi kalmadı. Şimdi tek eğri var ve makalenin koyduğu yerde.

**Ders:** referanstan sapmalar tek tek savunulur ama BİRİKİR. Üç sapmanın hangisinin ne
bozduğu, üçü aynı anda dururken bilinemez. Önce referans birebir üretilir, sonra tek tek
sapılır.

## Langer spektral perdesi SİLİNDİ — iki kez denendi, ölçüldü

`[Langer 2004]`'ün spektral yağış perdesi uygulandı, makalenin birebir hâline getirildi,
iki farklı yapılandırmada denendi ve ikisinde de karşılığı çıkmadı. Kod silindi; kalan
şey burada yazılı ders.

**Birinci deneme — tüm ekran.** Makalenin kendi bileşimi: `I = 250·α + (1−α)·I_bg`,
opaklık ortalaması 0.29. Ölçüldü, ekranın tamamına sabit bir tül sürüyordu. Makalede
doğru, çünkü orada perde kar fırtınasının TEK katmanı ve arka plan düz bir resim.
Bizde sis zaten o işi yapıyor; iki tül üst üste biniyordu.

**İkinci deneme — orta bant.** `rain-spec.md` §10.4'ün tarif ettiği yer: yakını taneler,
uzağı sis, arası perde. Alt sınır tanecik kutusu (12 m), üst sınır yağışın kendi görüşü
(`1900·R^(−0.63)` = 162 m). Ölçüldü: bant 12-129 m, yoğunluk 0.88, en güçlü 30-40 m.
Görünür fark yok.

**Sebep tek cümlede:** yağmurda görüş 162 m'ye indiği için perdenin bandı ekranda dar bir
şeride sıkışıyor ve o şeritte sis zaten opak. Langer'ın hibriti SİSİ OLMAYAN bir sahne
için tasarlanmış — makalenin bütün örnekleri düz arka plan görüntüleri.

**Yol boyunca ölçülen ve saklanmaya değer üç şey:**

- **Yöntem θ'nın zamanla değişmesine uygun değil.** Makale fazı kare kare artımlı
  işliyor (`§5.2`) ve genlik alanı sabit kalıyor; pişmiş dokuyu döndürmek faz alanını da
  döndürüyor ve desen katı cisim gibi dönüyor. Makale de θ'yı zamanla değiştirmiyor
  (`§7.2`). Serbest bakan birinci şahıs kamera yöntemin doğrulanmış alanının dışında.
- **Desenin ortalaması havadır.** Pişirici ortalamayı 0.5'e eşliyor; o ortalama doğrudan
  opaklık olursa ekrana sabit gri sürülür.
- **Sapmalar çarpımsal birikir.** Bir dönem üç eğri üst üste binmişti (kare alma, ortalama
  çıkarma, ağırlık). Üçü aynı anda dururken hangisinin ne bozduğu ölçülemiyor. Önce
  referans birebir üretilir, sonra tek tek sapılır.

## Yağış sütunları DENENDİ VE ELENDİ — sisin içinden uzak sis yapısı görünmez

Uzakta "orada yağmur yağıyor" hissi için sisin yoğunluğuna yağışla açılan büyük ölçekli
kolonlar eklendi (froxel hacminde, üç oktav değer gürültüsü, 60-240 m). Kod yazıldı,
derlendi, sonra ölçümle elendi ve silindi.

**Ölçüm.** Yağmurda görüş 162 m, sönümleme 0.0242 /m. 360 farklı yatay bakış yönü için
ışın boyunca optik derinlik integre edildi:

| yol | optik derinlik sapması | komşu ışınlar arası geçirgenlik farkı |
|---|---|---|
| 100 m | ±%3.0 | 0.0008 |
| 200 m | ±%3.6 | 0.0003 |
| 400 m | ±%4.3 | 0.0000 |

Her yağış şiddetinde aynı sonuç. Görünmez.

**Sebep.** Optik derinliğin çoğu ilk elli metrede birikiyor ve orası BÜTÜN yönler için
aynı. Uzaktaki kolonun katkısı, yakındaki ortak sisin altında kalıyor. Alanın kendi
genliği ±%38 olmasına rağmen yol integrali ±%3'e iniyor.

**Ve bu fiziksel olarak doğru.** Sağanağın İÇİNDEYKEN uzaktaki sütunları göremezsin;
sütun ancak sağanağın DIŞINDAN, göreli berrak havadan bakınca görünür. Bizde yağış
global (tek `weather.Precipitation`), yani "orada yağıyor burada yağmıyor" durumu
kurulamıyor. Sütunların ön koşulu **uzayda değişen yağış**, ve o ayrı bir iş.

**Yol boyunca ölçülen iki şey saklanmaya değer:**

- **Sinüs toplamı sütun ölçeğinde ızgara verir.** Bank beş sinüs ve 350-1700 m; ekranda
  nadiren birkaç periyot göründüğü için düzenliliği fark edilmiyor. Aynı yapı 60-240 m'ye
  indirilince ekranda onlarca periyot görünüyor ve ÇAPRAZ IZGARA çıkıyor — Python'da
  üretilip bakıldı, birebir kodda kayıtlı belirti. Değer gürültüsü bunu çözüyor (ızgara
  skoru 0.42, bankınki 0.73).
- **"Basit kesire yakınlık" ızgara testi işe yaramıyor.** `limit_denominator(6)` ile
  neredeyse her oran bir kesire %2 içinde düşüyor; ayırt etmiyor. Kullanılabilir test
  görsel + otokorelasyon.

## Rüzgâr şiddeti → hız eşlemesi kare, doğrusal değil

Şiddet Perlin'den geliyor ve zamanın çoğunu orta bantta geçiriyor. Doğrusal eşlemede
`Lerp(2, 14, 0.5)` = 8 m/s, yani "yarı yarıya rüzgâr" Beaufort 5 demekti. Oyun sürekli
sert esintide geçiyordu.

Belirti yağmurda okundu, rüzgârda değil. Kullanıcı "yatayda hareket eden damlalar var"
dedi. Ölçüm zinciri:

- Yörünge açıları hesaplandı: 8.5 m/s'de 0.5 mm damla yataydan **13.4°**, 1 mm damla 25°.
  Damlaların %63'ü 28°'nin altında. Fizik doğru — damla rüzgârın yatay hızını tam yer.
  kar değil. **Rüzgâr sürüklenmesi kapatılınca yatay hareket bitti.**
- Yani hata damlada değil, rüzgârın büyüklüğündeydi.

Kare eşleme uçları korur (0 → calmSpeed, 1 → stormSpeed) ve yalnız orta bandı indirir.
Bu bir fizik yasası DEĞİL, dağılım kararıdır; öyle olduğu `WindField.ShapeSeverity`
başında da yazılı.

**Denize uygulanmıyor.** Karar damlanın sürüklenmesi için verildi; deniz spektrumu aynı
şiddeti U10 olarak okuyor ve orta bandın ezilmesi dalgayı düzleştiriyordu — dünya
fırtınası 0,55'teyken deniz rüzgârı 4,0 m/s'de kalıyordu. `SeaLevelSpeed` şiddeti doğrusal
çeviriyor. Dağılım kararı tüketicisiyle birlikte gider; ortak sayıya yapıştırılmaz.

**Tek yerde uygulanıyor.** `Strength` aynı hızdan türediği için sis kapanması, sürüklenen
kar eşiği, girdap genliği, ses ve bulut hızı birlikte iniyor. Ayrı ayrı ayarlansaydı hava
kendi içinde çelişirdi.

**Yol boyunca kapanan iki gerçek hata:**

- İz boyu ve saydamlığı `TerminalVelocity(r)` okuyordu, yani rüzgârı hiç görmüyordu.
  Oysa ikisi de pozlama boyunca SÜPÜRÜLEN YOLDAN çıkar. İnce damlada iz 4.3× kısa
  çiziliyordu (3.4 cm yerine 14.6 cm olmalıydı) ve kısa yatay çizik "dolu sekiyor" gibi
  okunuyordu. Bileşke hıza geçildi: ortanca alfa 0.363 → 0.347, damla başına ekran alanı
  2.68 → 4.74 px² (+%77).
- Teşhis anahtarı `UpdateStreaks` içinde bağlanıyordu ve o metodun önünde dört erken
  çıkış var. Biri tutsa uniform hiç yazılmayacak, HLSL varsayılanı (0,0,0) — yani "hepsi
  kapalı" — kalacaktı. **Teşhis aracının kendisi sessizce yalan söyler.** Koşulsuz
  bağlamaya taşındı.

**Aracın çözünürlüğü hipotezi ayırmalı.** Yörünge açısı önce yedi renk bandına basıldı;
kullanıcı ayıramadı ("gözüm seçmiyor, hepsi birbirine benziyor"). Renk sorusu bırakılıp
ELEME kondu: ekranda yalnız bir grup çizildi, soru "yatay olanlar hangisinde kalıyor"
oldu ve tek turda kapandı.

## Yükseklik bantları ÖLÇÜMLE ELENDİ; sınır tabakası kapalı biçimde

Rüzgârın sınır tabakası (yerde sıfır, yükseldikçe logaritmik) yağan yağışa eklenecekti.
Sürüklenme sınıf başına CPU'da integre ediliyor ve tek vektör yüksekliğe göre değişemez,
o yüzden ilk çözüm **sınıf başına dört yükseklik bandı** oldu: her banda ayrı kayma
integre edilir, tanecik kendi kotuna göre iki bandın arasında harmanlanır.

**Ölçüm bunu yıktı.** Bantların kaymaları zamanla SINIRSIZ ayrışıyor — 13.7 m/s rüzgârda
30 saniyede 101 m. Kutuya sarıldıktan sonra aradaki fark rastgele bir sayıya dönüşüyor
(±24 m). Alan periyodik olduğu için "en kısa temsilci" seçmek görüntüyü sürekli tutuyor,
ama TANECİĞİN YÖRÜNGESİNİ tutmuyor: damla düşerken bantlar arasında geçtiği için o
rastgele fark ona yatay hız olarak biniyor.

    en kötü: 24 m x 0.88 bant/sn = 21 m/s sahte yatay hız  (rüzgârın kendisi 13.7 m/s)

Belirti tam olarak buydu: "yağmur havada kar gibi sürükleniyor".

**Doğrusu kapalı biçim.** Damla yavaş havada geçirdiği süre boyunca serbest akışın
gerisinde kalır; bu gecikme SINIRLI bir integraldir:

    L(z) = (U/v_t) * INTEGRAL_z^{z_ref} (1 - f(z')) dz',   f(z) = ln(z/z0)/ln(z_ref/z0)

Analitik hâli `G(z) = z - z*(ln(z/z0) - 1)/L`. Gecikme tek değişkenli, düzgün ve monoton;
türevi `dL/dt = U(1 - f(z))`, yani damlanın yatay hızı tam olarak `U*f(z)`. Sayısal olarak
doğrulandı: iki ifade arasındaki fark 4e-9.

**Ders:** periyodik bir alanda "sarma güvenli harman" ALANIN sürekliliğini korur, ama
harmanlanan şey bir taneciğin YÖRÜNGESİYSE yetmez. Sınırsız biriken iki büyüklüğün farkı
alınacaksa fark kapalı biçimde ve sınırlı olarak kurulmalıdır.

## Tanecik girdabın her kıvrımını yemez — atalet süzgeci

Sürüklenme denklemi birinci mertebeden: tanecik alçak geçiren bir süzgeçtir. Gevşeme
süresi `tau = v_t/g`, `omega` frekanslı zorlamaya genlik oranı `1/sqrt(1+(omega*tau)^2)`
— birinci mertebe kutbun frekans cevabı, Stokes sayısı literatürünün standart hâli.

Tanecik alanın İÇİNDEN GEÇTİĞİ için gördüğü frekans uzamsal ölçekten doğuyor:
`omega ~ k*|V| + omega_zaman`. Girdap ölçeği bir adım önce dört kat sıklaştırılmıştı ve
ince oktav 13.85 m/s'de 27.5 rad/s ~ 4 Hz'e çıkmıştı. Damlanın tau'su 0.21 sn — 4 Hz'i
takip edemez, ortalar. Model tam genliği uyguluyordu, damla yaprak gibi çırpıyordu.

Ölçülen kazançlar (rüzgâr 13.7 m/s):

    0.5 mm damla  tau 0.206  kaba 0.504  ince 0.201
    1.1 mm damla  tau 0.455  kaba 0.245  ince 0.088
    5.0 mm damla  tau 0.932  kaba 0.105  ince 0.037

**YAĞMURU KARDAN AYIRAN ŞEY BU.** Sapma genliğinin iz boyuna oranı:

    yağmur 0.5 mm   5.6 cm / 19.5 cm = 0.29   -> çizgi okunur
    yağmur 5.0 mm   1.2 cm / 24.5 cm = 0.05

Oran 1'i geçince yol bir çizgi değil bir kıvrım olarak okunuyor. Eskiden yağmur da o
tarafta duruyordu.

**Telafi terimi silindi.** Fark daha önce elle konmuş bir `lerp(1.5, 0.4, dropSize)`
katsayısıyla taklit ediliyordu. Fiziği koyunca gerekçesi kalmadı; terim geri gelmez.
Yağmurun sapması 3-4 kat, karınki 1.6 kat azaldı — aradaki makas budur.

## Girdap ölçeği kotla küçülür — kesilerek değil, enerji kaydırılarak

Atalet süzgeci konduktan sonra sapma/iz oranı ölçüldü ve uç %10 fırtınada 1.5'te kaldı:
yere yakın ince damlalar. Sınır tabakası yatay hızlarını kestiği için izleri kısalıyor,
ama girdap genliği her kotta aynıydı.

Sebep: yüzey tabakasında girdabın BOYU `l ≈ κz` ile büyür. Yere yakın 10.5 m'lik girdap
fiziksel olarak sığmaz — zemin onu keser. Bizim alanın dalga boyu ise her kotta sabit.

**İlk çözüm yanlıştı ve ölçümle elendi.** Genliği `min(1, κz/λ)` ile kesmek denendi
(`DECISIONS.md`'ye o hâliyle yazılmıştı). Sonuç:

    yağmur sapması  3.6 cm  ->  0.2 cm   (18 kat)
    kar, kot 2 m   40.8 cm  ->  3.5 cm

Yağmur bıçak gibi düzleşiyor, kar yerde savrulmayı tamamen bırakıyordu — yani yer
blizzard'ı ortadan kalkıyordu. Hata formülün ENERJİYİ YOK ETMESİ: sığmayan girdabın
enerjisi kaybolmaz, küçük ölçeklere geçer. Yüzey tabakasında `σ_u` yükseklikle
neredeyse sabittir; değişen ölçektir.

**Doğrusu payı kaydırmak.** Kaba oktav ancak sığdığı kadar enerji tutar, kalanı ince
oktava geçer. Toplam hız değişintisi korunur; yer değiştirme yine de düşer, çünkü küçük
girdabın yer değiştirmesi `1/k` ile küçüktür.

Taban 50/50 seçildi çünkü mevcut alanın oktav ağırlıkları (0.5 / 0.165) zaten
`k_ince/k_kaba = 3` oranında — yani hız değişintisi iki oktavda eşit dağılmış.

Ölçülen sonuç, uç %10'daki sapma/iz oranı:

    orta hava   1.55  ->  0.75
    fırtına     1.58  ->  0.42
    kar, kot 2 m  40.8 cm -> 15.0 cm   (iz boyu 11 cm, yani hâlâ savruluyor)

**Ders:** "sığmayan ölçeği kes" sezgisi bir enerji spektrumunda yanlıştır. Kesilen
enerjinin nereye gittiği yazılmadan hiçbir bant kapatılmaz.

## Yakın yağmur: periyodik döşeme yoğunluk gradyanı taşıyamaz

Hacim `r³` ile büyüdüğü için tanecik bütçesinin neredeyse tamamı uzağa gidiyordu: 48 m'lik
tek kutuda 5 metrenin içinde 1 188 tanecik, yani binde beş. Oysa oyuncunun TEK TEK DAMLA
olarak okuduğu hacim orası.

**Önce sürekli radyal dağılım ölçüldü.** `yoğunluk ∝ r^-p`, temsil payı `r^p` ile ölçekli
(toplam damla sayısı korunacak şekilde). Ekran kaplaması, kilopiksel, yağış 1.0:

    p     ortanca alfa   5 m içi            16-24 m    toplam
    0     0.265          124  ( 2 215 tanecik)   657     1482
    1     0.270          242  (10 708)           576     1696
    1.5   0.255          265  (22 885)           506     1696
    2     0.222          276  (47 571)           409     1609

`p = 1` her ölçütte kazanıyor: toplam +%14, yakın alan +%95, ortanca alfa değişmiyor.
`p = 2`'de uzak kabuk seyreliyor ve ortanca %16 düşüyor.

**Ama radyal dağılım DOĞRUDAN UYGULANAMIYOR.** Tanecikler kameranın etrafında periyodik
olarak sarılıyor ve periyodik bir döşeme yoğunluk gradyanı taşıyamaz — periyodiklik
tekdüzelik demektir. Konumu radyal olarak BÜKMEK de denenmedi çünkü kâğıtta çürüdü:
büküm Jacobian'ı hızı da ölçekler, yakın damlalar sürünmeye başlar. Aynı sınıftan bir hata
bir gün önce ölçülmüştü (yükseklik bantları).

**Uygulanabilir biçim iç içe kutu.** Her kutu kendi içinde tekdüze, kendi kaymasıyla
integre ediliyor, kendi kutusuna sarıyor — yani hareket her yerde tam doğru. İç kutunun
kapsadığı yerde yoğunluklar toplanıyor. Ölçüldü (yağış 1.0):

    tek kutu 48          5 m içi  87   toplam  934
    48 + 12, iç %5       5 m içi 194   toplam 1027
    48 + 12, iç %10      5 m içi 227   toplam 1033   <- seçilen
    48 + 12, iç %20      5 m içi 265   toplam 1003
    48 + 16 + 6          5 m içi 221   toplam 1061

İç %10 (25 000 tanecik, 12 m kutu) sürekli dağılımın kazancını yakalıyor: yakın alan iki
buçuk kat, toplam +%11, ortanca alfa sabit. Üçüncü kutu kayda değer bir şey eklemiyor.

**Temsil payı konumdan türetildi.** Kutuya bağlansaydı aynı noktadaki iki tanecik farklı
opaklıkta çıkardı. İç kutunun yoğunluk katkısı kendi sönüm eğrisiyle giriyor, yoksa
sınırda opaklık sıçrardı.

## İz dokusunun çözünürlük seviyesi: makalenin kuralı tek seviye veriyor

`[Garg 2006, §5]` "projeksiyon genişliğinden az büyük" seviyeyi seçmeyi söylüyor. Kural
uygulandı ve sahnenin tamamı için tek cevap çıktı:

    ekran genişliği (MinPixelWidth tabanı)   1.2 px
    gerçek genişlik, 1.4 mm damla @ 1 m      1.4 px      @ 5 m  0.28 px
    4 px'i aşan tek durum                    4 mm'den iri damla, 1 m'den yakın

Karede bir iki tanecik. Yani `size4` doğru; `size16` dört kat fazlaydı.

Asıl bedel boşa giden doku değil, **alt örnekleme**: 525 piksel yüksekliğindeki iz uzak
damlada 9 piksele iniyor (58 kat) ve dizilerde mipmap yok, donanım düzeltemiyor.
Makalenin dipnotu tam bunu söylüyor. `size4`'te oran 14 kata iniyor, çalışma kümesi
3.4 MB → 0.21 MB.

**Üç seviyeyi birden bağlamaya gerek kalmadı** — dinamik seçim yalnız o %1'lik yakın
kuyruk için işe yarardı. Ertelenmiş bir işin doğru cevabı bazen "gerekmiyormuş" oluyor;
kural uygulanmadan bilinmiyordu.

## Langer'ın kendi makalesi "shower door" diyor — silme kararı yazarlarca doğrulandı

Spektral perde ölçülüp silindikten sonra makale baştan sona okundu. §6.2'de yazarlar
yöntemin kusurunu kendileri adlandırıyor: spektral kar tek başına "atmosferik dokusal
etki" veriyor ama bireysel taneden yoksun ve **"shower door" etkisine düşebiliyor** —
yani ekrana yapışmış bir duş camı gibi okunuyor. Bizim iki turda ölçtüğümüz belirti
birebir buydu.

Makalenin çözümü perdeyi tanecikle BİRLİKTE kullanmak. Bizde o birleşim çalışmadı çünkü
üçüncü bir katman var: yağışın kendi görüş mesafesi. Perdenin bandı sise sıkışıyor.

**Spec'in atladığı bir şey de bulundu:** Langer §6.2'de yöntemin YAĞMUR uzantısını da
veriyor (`ventana.avi`) — dikey yön ve yüksek `C` ile yalnız `|ω_y| ≈ 0` bileşenleri
Bizde konusu yok: yağmur Garg-Nayar iz veritabanından geliyor ve o çok daha fiziksel.

**Cordonnier tarafında spec sadık.** Tam okumada doğrulanan üç madde:
- §9: "cell widths from 2 to 10 meters ... robust to scale changes" — bizim 7.32 m içeride,
  yani spec'in "88 m/hücre" uyarısı 90 km varsayımından geliyordu ve bizde geçersiz.
- §6.2: "10m per cell only allows a consideration of the general direction of the skiers"
  — ayak izi simülasyon ızgarasına sığmaz, ayrı sistem olmak zorunda.
- §5.4'teki `max(D, -k_erosion·curv_W)` makalede birebir öyle. Aşındırılan miktarın mevcut
  karı aşmaması için `min` olması gerektiği şüphesi yerinde; uygulamada `min` alınacak ve
  sapma burada yazılı.

---


## Yağış sıcaklıktan koparıldı

**Kural.** `SnowfallController` yalnız yağış olup olmadığına bakar. Sıcaklık kapısı yok:
yağıyorsa kardır.

**Neden.** Kullanıcı kararı, kar çizgisi kaldırılırken konan kuralın devamı: *"kar
yağıyorsa kar tutar, bu kadar basit bir mantık olmalı."* Spec §3.4'ün 0.5/2.0 °C
histerezisi bunun tersini yapıyordu — dağın alt kotlarında (+6 °C) yağış hep yağmur
kalıyordu ve kar hiç görünmüyordu.

**Ne kayboldu.** `Wetness` sıcaklıktan türüyordu, kaynağı kalmadı → sabit 0, tane her
zaman kuru. `RainWeight01` histerezisin çıktısıydı → sabit 0, yağmur hiç çizilmiyor.
Tane terminal hızı ıslaklıkla lerp ediliyordu (`0.6–1.4` ↔ `1.4–3.0`) → kuru bant
kaldı.

**Ölçüm — sıcaklık zaten yağış ŞİDDETİNİ sürmüyordu.** Kopartma istendiğinde ilk
varsayım "donma seviyesi şiddeti de etkiliyordur" idi ve yanlış çıktı: `Baseline()`
eğrisini `referenceRainCeiling`/`referenceStormFloor`'dan okuyor, bunlar `Bind()`'da
kotlardan hesaplanan SABİTLER. Sıcaklığın sürdüğü `rainCeiling`/`stormFloor` yalnız iki
HUD satırında görünüyordu, hesaba hiç girmiyordu. O yüzden kopartma tek bir dosyaya
dokundu; kalan zincir (donma seviyesi → yağmur tavanı → HUD) ölü kod olarak silindi.


## Kar tanesi terminal hızı dayatılmıyor, fizikten çıkıyor

**Kural.** `VFX_Snowfall`'ın Update bağlamında yerçekimi (−9.81 m/s²) ve sürükleme
(katsayı 9.81) var; terminal hız dengeden çıkıyor: `v = g / drag = 1 m/s`.

**Neden — denenen ve başarısız olan yol.** Önce spec §17.1'in yazdığı gibi başlangıç
hızı yazıldı (`velocity = (0, −0.6…−1.4, 0)`). Kar düşmedi; havada savruldu. Sebep
`Block.Turbulence`'ın `Relative` kipi: o kip hızı bir hedefe ÇEKİYOR
(`velocity += (hedef − velocity) * drag * dt`) ve hedef türbülans alanı, ortalaması
sıfır. Yazılan terminal hız bir saniyede yeniyordu.

**Neden dayatmak yerine denge.** Sürükleme katsayısı iki davranışı birden açıklıyor:
terminal hızın düşük olması (1 m/s) ve karın rüzgârda savrulması (hafif tane rüzgâr
hızına hızla yaklaşır). Sabit hız dayatmak ikincisini ayrı bir terim olarak eklemeyi
gerektirirdi.

**Uçlar.** `v = 1 m/s` spec'in kuru kar için istediği 0.6–1.4 bandının ortası. Islak kar
bandı (1.4–3.0) şu an erişilemez — ıslaklık sıcaklıktan geliyordu, yağış sıcaklıktan
koparıldı.


## VFX grafiklerinin sınır kutusu elle yazılıyor

**Kural.** `SnowVfxBuilder.SetBounds` her grafiğe açık bir `bounds` veriyor; kutu
cömert tutuluyor.

**Neden.** `VFXBasicInitialize.bounds` varsayılanı 1 m³. Unity o kutuyu frustum'a göre
kırpıyor ve sistem hiç çizilmiyor — `isVisible` false. Belirti "kar yağmıyor"du;
zincirin her adımı doğru sayı veriyor, 39892 parçacık yaşıyor, hiçbiri ekrana
gelmiyordu.

**Neden cömert.** Fazla büyük kutu yalnız kırpmayı gevşetir (birkaç gereksiz çizim);
küçük kutu sistemi tamamen yok eder. Asimetrik risk.

**Neden `Automatic` değil.** Unity gerçek kutuyu her frame GPU'da hesaplayabiliyor ama
bu bir okuma-geri maliyeti. Ölçüm için geçici olarak açıldı (kar düşüyor mu sorusunu
ışıktan bağımsız cevapladı), ölçüm bitince kapatıldı.


## Kar tanesinin asgari ekran boyutu Custom HLSL bloğunda

**Kural.** `VFX_Snowfall`'ın çıktı bağlamında bir `CustomHLSL` bloğu var:
`scaleX = max(scaleX, newScale.x)`. Tane hiçbir mesafede 1.3 pikselin altına
düşmüyor, ama yakındayken gerçek dünya boyutunu koruyor.

**Neden hazır blok kullanılmadı.** `ScreenSpaceSize` bunu vermiyor: `SizeMode`
seçeneklerinin hiçbiri asgari değil (`PixelAbsolute`,
`PixelRelativeToResolution`, `RatioRelativeTo*` — paket kaynağından okundu).
`PixelAbsolute` denendi ve boyutu SABİTLEDİ; yakındaki tane de 1.3 piksele
kilitlendi, kar toz gibi göründü.

**Formül sıfırdan yazılmadı.** `ScreenSpaceSize`'ın kendi HLSL'i alındı, tek fark
son iki satırda atama yerine `max`. Spec §17.1'in
`distToCam * (px / h) * 2 * tan(fov/2)` ifadesiyle aynı büyüklük; aynı işi yapan
çalışan bir ifade zaten paketin içindeydi.

**Doğrulandı ölçümle:** `minPixelSize` geçici olarak 20 yapıldı — taneler her
mesafede iri toplar oldu ve FPS 114'ten 14'e düştü (overdraw). Mekanizmanın
çalıştığı da, 1.3'ün neden 1.3 olduğu da aynı ölçümden çıktı.

**BÜYÜTMEK ALFAYI KISAR — enerji korunumu.** Blok bir dönem yalnız ölçeği
büyütüyordu. Taneyi piksel tabanına çekmek kapladığı ALANI büyütüyor; alfa
sabit kalırsa büyüme oranı kadar ışık uyduruluyor.

Ölçüm: `px/rad = 769` (fov 60, 888 px yükseklik). Taban 1.8 cm tane **10.6
m**'de tam 1.3 piksel; ötesinde taban devreye giriyor ve alanı şişiriyor.

| mesafe | gerçek boy | alan çarpanı |
|---|---|---|
| 5 m | 2.77 px | ×1 (dokunmuyor) |
| 13 m | 1.07 px | ×1.5 |
| 20 m | 0.69 px | ×3.5 |
| 40 m | 0.35 px | ×14 |

Spawn kutusu 40×26×40; hacminin çoğu 10.6 m'nin ötesinde. 89 bin tane üst üste
binince ekranda süt gibi bir örtü çıkıyordu ve örtünün keskin dikdörtgen kenarı
**kutunun kendi duvarıydı**. Kullanıcı bunu üç kez "kâğıt gibi incecik, derinliği
yok" diye bildirdi.

Düzeltme `alpha *= saturate(1 / alanOrani)`. 10.6 m'den yakın tane hiç
etkilenmiyor; uzak tane görünür kalıyor (tabanın amacı buydu) ama sahte
parlaklık vermiyor.


## Sis yoğunluğu görüşte değil sönümlemede doğrusal

**Kural.** `SnowEnvironmentBridge.FogDensity01` görüş mesafesini `σ = 1/V`
üzerinden 0..1'e çeviriyor `[KAYNAK: Koschmieder — σ = 3.912 / V]`.

**Neden.** Önceki hâli görüş mesafesinde doğrusaldı ve fiziksel olarak ters
sonuç veriyordu: 1150 metre görüşte sis yoğunluğu **0.95** çıkıyordu, yani
"neredeyse tam sis". 1150 m berrak bir havadır.

**Belirti buydu:** o dönem duran uzak yağış perdesinin alpha'sı `1 − fog * 0.6`
ile 0.10'dan 0.043'e düşüyordu ve perde ekranda görünmüyordu. Perdeler sonradan
silindi (yukarı bak) ama yanlış değer compute yağışını da kısıyordu; düzeltme
onun için geçerliliğini koruyor.

**Uçlar kâğıtta:** 60 m → 1.00, 100 m → 0.60, 200 m → 0.30, 500 m → 0.12,
1150 m → 0.05, 20 km → 0.00. 3.912 sabiti hem pay hem paydada olduğu için
sadeleşiyor.


## Quad perde yok — spec §17.2 ve §18.7 Sistem B silindi

**Kural.** Ne uzak yağış perdesi (`SnowfallCurtains`) ne süspansiyon perdesi
(`SnowCurtainController`) projede duruyor. İkisi de spec'te var, ikisi de
uygulandı, ikisi de aynı gerekçeyle silindi.

**Neden.** Devasa kameraya bakan quad kaçınılmaz olarak KÂĞIT gibi okunuyor:
kenarı ekranda düz bir çizgi, içi derinliksiz. Kullanıcı bunu üç kez bildirdi
("kâğıt gibi incecik, bir derinliği yok"). Sorun alfa ya da doku değil,
GEOMETRİ: 12–25 m'lik on dört bilboard hacim taklidi yapamıyor.

**Ölçüm.** Tüm `VisualEffect` bileşenleri kapatıldı; dikdörtgenler ekranda
kaldı. `SnowCurtainController.enabled = false` yapıldı; kayboldular. Belirti
sahibini ancak bu iki yakalı ölçümle buldu — üç tur boyunca VFX suçlanmıştı.

**Yerine ne geçti.** Havanın savrulan karla dolu olması `FogDensity01`
üzerinden geliyor: hacimsel bir büyüklük, görüş mesafesinden (Koschmieder)
türüyor. Saltasyon (`VFX_Spindrift`, 1–5 cm, yere yapışık) duruyor — o gerçek
parçacık, bilboard hilesi değil.

**Telafi terimi geri eklenmeyecek.** "Biraz daha soluk yaparsak" ile
düzelmiyor; iki denemede de aynı yerden kırıldı.


## Rüzgâr yağışa KUVVET olarak giriyor, hız olarak değil

**Kural.** `SnowfallLayers` grafiğe `WindForce = yön × hız × 9.81` yazıyor;
9.81 grafikteki `dragCoefficient` ile aynı sayı.

**Neden.** Hız dayatmak yerçekimini bozardı: `velocity = wind` yazmak düşey
bileşeni de eziyor. Kuvvet vermek ikisini ayırıyor — sürükleme yatayda hızı
rüzgâra çekiyor (`F/drag = wind`), düşeyde yerçekimiyle dengeleniyor
(`g/drag = 1 m/s`). Tek katsayı iki davranışı birden veriyor.

**Bağlı sayı uyarısı:** `SnowfallLayers.FlakeDrag` ile builder'daki
`dragCoefficient` aynı olmak zorunda. Farklı olsalar tane rüzgârdan hızlı ya da
yavaş sürüklenir.


## Adım mesafeden çıkıyor, zamandan değil

**Kural.** `SnowStepRhythm` alınan yolu biriktiriyor; her `strideLength / 2`
metrede bir ayak düşüyor.

**Neden.** Sabit bir zamanlayıcı hız değişince yanlış ritim verir: yavaş
yürürken ayaklar kayar, koşarken adım sıklığı yetişmez. Mesafe tabanlı ritim
hız arttıkça kendiliğinden sıklaşıyor.

**Ayak fazı ve adım olayı aynı sayıdan.** İki ayrı bileşene bölünseydi ikisinin
fazı kayabilirdi — ses bir ayakta, iz öbüründe düşerdi.


## Sakin hava 0.6 m/s, bulutlar 2 m/s — iki ayrı taban

**Kural.** `WindSettings.calmSpeed = 0.6` (yüzey),
`CloudWeatherDriver.calmAloftSpeed = 2` (bulut katmanı).

**Neden yüzey düştü.** 2 m/s Beaufort 2 ("hafif esinti") demek: dağda hiçbir
zaman durgun hava olmuyordu. Kar terminal hızı `g/drag = 1 m/s` olduğu için
yatay rüzgâr doğrudan eğime çevriliyor:

| rüzgâr | dikeyden sapma |
|---|---|
| 0.6 m/s | 31° |
| 2.0 m/s (eski taban) | 63° |
| 14 m/s (fırtına) | 86° |

Panel "rüzgâr 0" derken ekranda 63° yatık kar vardı; kullanıcı bunu "sanki çok
ufak bir rüzgâr varmış gibi" diye bildirdi ve haklıydı. 0.6 Beaufort 1 ("hafif
hava"); maruziyet çarpanıyla (0.35–1.45) birlikte 0.21–0.87 m/s, yani 12–41°
arası bir çeşitlilik.

**Neden bulut tabanı ayrı.** `calmSpeed` aynı zamanda bulut ilerleme hızını
sürüyordu (`CloudWeatherDriver`, `FreeAirSpeed` üzerinden). Yüzeyi durultmak
gökyüzünü de durduruyordu: 7.2 km/h → 2.2 km/h, on dakikada 360 m, 2000 m'lik
bulut katmanında fark edilmez bir hareket.

Fizik ikisini zaten ayırıyor: yüzey rüzgârını YER SÜRTÜNMESİ yavaşlatır, serbest
atmosfer ondan etkilenmez. Bulut katmanı kendi tabanını alıyor; yüzey tabanı
değişince gökyüzü etkilenmiyor.

**Ölçüm:** kilitli rüzgâr 0'da HUD 0,5 m/s, taneye giden yatay hız 0.51 m/s,
dikeyden sapma 26.8°.

**Yön DEĞİŞMEDİ.** `directionSpread` 35°, `directionDrift` 0.02 Hz — sakin hava
±35°'yi 50 saniyelik periyotla dolaşıyor, bu zaten doğru. Kilitliyken yön
`overrideAngle ± 8°`'e daralıyor; bu teşhis kilidinin amacı, hata değil.


## Salınım hız değil YER DEĞİŞTİRME, ve çıktı bağlamında

**Kural.** `VFX_Snowfall`'ın çıktı bağlamında `SnowFlakeFlutter` bloğu tanenin
konumunu `(0.35/ω)` genlikli bir salınımla kaydırıyor; faz parçacık kimliğinden.

**Neden hız değil.** Spec §17.1 `Set Position (Add)` ile `... * 0.35 * dt`
veriyor. CustomHLSL bloğu `deltaTime`'a ULAŞAMIYOR: VFX'te bu sembolü blokların
kendisi `VFXNamedExpression(VFXBuiltInExpression.DeltaTime, "deltaTime")` ile
bildiriyor (paket kaynağı, `FlipbookPlay.cs:255`) ve CustomHLSL öyle bir bildirim
yapamıyor. Derleme "undeclared identifier 'deltaTime'" ile düştü.

Salınım sınırlı bir titreşim olduğu için integrali kapalı formda alındı:
`∫ 0.35·sin(ωt) dt = −(0.35/ω)·cos(ωt)`. Genlik x'te `0.35/5.5 = 6.4 cm`,
z'de `0.35/4.6 = 7.6 cm`. Kare hızından bağımsız, birikme hatası yok.

**Neden çıktı bağlamında.** Salınım yalnız tanenin NEREDE ÇİZİLDİĞİNİ
değiştirmeli; zemin kesmesini ve birikmeyi etkilememeli.

**Neden türbülans yetmiyor.** İkisi farklı şeyler ve bu ayrım ölçümle çıktı:

| | türbülans | salınım |
|---|---|---|
| kaynağı | havanın ortak hareketi | tanenin kendi çırpınması |
| uzayda | tutarlı, dalga boyu ~8 m | taneden taneye bağımsız |
| zamanda | sabit alan | tanenin yaşına bağlı |
| net yön | var | yok |

`DECISIONS.md`'de "türbülans benzer bir etki veriyor" diye ertelenmişti; o
varsayım yanlıştı. Türbülans TUTARLI olduğu için duran bir oyuncu tek gürültü
lobunun içinde kalıyor ve hafif bir rüzgâr gibi okunuyor.

**Genlik kontrolü:** 7 cm, tane boyutunun (1.8 cm) dört katı — görünür. Daha
küçük olsaydı piksel altında kalırdı.


## Türbülans tamamen rüzgâra bağlı — spec'in `+ 0.15` tabanı yok

**Kural.** `Intensity = 0.35 * WindSpeed`. Spec §17.1 `+ 0.15` de veriyor ve
sayıları `[KALİBRASYON]` diye işaretliyor.

**Neden.** Türbülans bloğu uzayda TUTARLI bir alan üretiyor. Sabit bir taban,
rüzgâr sıfırken bile `0.15 / 0.9 = 0.167 m/s`'lik ORTAK bir sürüklenme bırakıyor
— 5 saniyelik ömürde 83 cm, ve çevredeki bütün taneler aynı yöne. Tutarlı hava
akımı zaten rüzgârın tanımı; rüzgâr yoksa net sürüklenme de olmamalı. Rüzgârsız
havada tanenin çırpınması ayrı terimden geliyor (salınım, yukarı bak).

**Bugün görünür bir farkı yok:** `WindSettings.calmSpeed = 2 m/s`, yani rüzgâr
hiç sıfıra inmiyor ve taban terim hiçbir zaman baskın olmuyor (2 m/s'de
`0.70` yerine `0.85`). Değişiklik ileriye dönük: sakin hava hızı düşürülürse
hayalet rüzgâr belirtisi doğmasın.


## VFX sistemleri dünya uzayında simüle ediliyor

**Kural.** Beş grafiğin de `VFXDataParticle.space` alanı World
(`SnowVfxBuilder.SetWorldSpace`). Bulunamazsa fırlatılıyor — sessizce yerel
kalmak belirtiyi geri getirir.

**Neden.** Yerel uzayda parçacık konumu objeye GÖRE tutuluyor; obje kayınca
yaşayan bütün parçacıklar onunla ışınlanıyor. Yağış kutusu oyuncuyu 1 m
ızgarasında takip ediyor (`SnowfallLayers`), yani yürürken saniyede birkaç kez
89 bin tane birden bir metre atlıyordu.

**Belirti buydu:** "yürürken kar tanecikleri çok hızlı yer değiştiriyor, sanki
sürekli farklı farklı render oluyor gibi".

**Ölçüm — iki yaka, `timeScale = 0`.** `SnowfallLayers` kapatılıp zaman
durduruldu, kare yakalandı, kutu kaydırıldı, tekrar yakalandı:

| kaydırma | değişen piksel | ortalama parlaklık |
|---|---|---|
| 1 m | %0.35 | 61.9 → 61.9 |
| 30 m | %0.16 | 61.9 → 61.8 |

Yerel uzayda 30 m kaydırma bütün karı ekrandan çıkarırdı. Değişen pay yeni
doğan taneler; yaşayanlar yerinde kaldı.

**Snap silinmedi.** Izgara hâlâ 1 m; onun işi spawn deseninin kameranın peşinden
sürüklenmesini önlemek ve o gerekçe duruyor. Işınlanma snap'ten değil UZAYDAN
geliyordu.


## `TransformPositionVFXToWorld` uzay ayarının yerini tutmaz

**Kural.** Grafiğe giden kotlar sistemin uzayıyla AYNI uzayda gönderiliyor.
Bugün sistem dünya uzayında, o yüzden kot da dünya (`groundReference.position.y`).

**Neden — iki kez karın tamamını sildi.** Sistem yerel uzaydayken
`attributes.position` kutu merkezine göre ±10 çıkıyordu; dünya kotu 205 ile
karşılaştırınca koşul her tane için doğruydu. Çözüm diye
`TransformPositionVFXToWorld` eklendi ve DÜZELTMEDİ: `groundY = 0` yazıldığında
bile `alive` sıfır kaldı, yani dönüşüm beklenen değeri vermiyor.

Doğru çözüm dönüşüm değil, uzayı düzeltmekti — sistem World'e alındı (yukarı bak)
ve iki taraf da dünya koordinatı konuştu. **Ders:** uzaylar uyuşmuyorsa shader'da
dönüştürmeye çalışmadan önce sistemin uzayına bak.


## Kar yoğunluğu kapasiteden gelir, kutudan değil

**Kural.** Spawn kutusu spec §17.1'in verdiği (40, 26, 40); yoğunluk kapasiteyle
ayarlanıyor (120000).

**Neden — kutu küçültmek ters teptti.** Yoğunluk `kapasite / hacim` olduğu için
kutuyu daraltmak kâğıtta yoğunluğu artırıyor. (24,20,24) ve (20,16,20) denendi,
kâğıtta 3.5 ve 6.2 tane/m³ çıktı ve **ekranda kar azaldı**.

Sebep rüzgâr: 12 m/s'de tane 10 metreyi 0.85 saniyede geçiyor. Dar kutuda tane
kameranın çevresinde hiç kalmıyor, bir kenardan girip öbüründen çıkıyor. Spec'in
geniş kutusu tam bu yüzden geniş.

## Açık günün pozlaması kara göre seçildi (-0.15 → -1.0 EV)

**Kural:** `LookSettings.clearDay.exposure` sahne ortalamasına göre değil,
KARIN ekranda nereye düştüğüne göre ayarlanır.

**Ölçüm** (10:00, bulut gölgesi kapalı, tam güneşli yamaç, ekranın alt %15'i):

| Pozlama | Zemin luması | Sapma |
|---|---|---|
| -0.15 EV | 0.921 | 0.0151 |
| -1.00 EV | 0.839 | 0.0274 |

Kar 0.921'de ACES'in omzunda: yüzey dokusunun, normal haritanın ve mikro
kabartının ürettiği bütün fark 255 seviyenin ~4'üne sığıyor. Ekranda tek parça
beyaz olarak okunuyor — kullanıcının "kar tuttuğu zaman yer fazla bembeyaz"
belirtisi tam olarak bu. 0.85 durak kısılınca sapma iki katına çıkıyor ve kar
hâlâ sahnenin en parlak yüzeyi.

**Denenmiş ve yetmeyen yol:** dokunun gücünü artırmak. Güç 0.9 → 1.6'da sapma
0.0151 → 0.0285'e çıkıyor ama aynı anda izole koyu mavi lekeler beliriyor;
kontrastı omuza rağmen zorlamak, deseni değil ARTEFAKTI büyütüyor.

**Kapsam:** yalnız `clearDay`. Fırtınalı gün zaten -0.8 EV, altın saat -0.8;
oralarda kar omuza dayanmıyor.

## Kar dokusu albedonun yerine geçmez, çarpan olarak girer

**Kural:** fotogrametri albedosu kendi UZAMSAL ortalamasına bölünüp 1
civarında bir katsayıya dönüştürülür, seviye fizikten (taze 0.90 / sıkışmış
0.70) gelmeye devam eder.

**Neden:** doku yerine konsaydı kar örneğinin kendi pozlaması sahneye taşınırdı
ve ışıklandırma zinciri o aralığa göre ayarlı olduğu için her saat farklı
kayardı.

**Bir kez yanlış yapıldı:** ortalama olarak PİKSELİN KENDİ parlaklığı
(`(r+g+b)/3`) kullanıldı. Bu her pikseli 1'e normalize ediyor, yani dokunun
parlaklık desenini tamamen siliyor — geriye yalnız renk tonu kalıyor. Ölçüldü:
güç 0 ile 3 arasında ekran sapması 0.01003 ↔ 0.00971, yani desen hiç gelmiyor.
Dört dokunun doğrusal uzamsal ortalaması ölçülüp sabit olarak gömüldü.

## İz neden ikinci bir yüzeyle çizilmiyor

**Kural:** kar izi arazinin KENDİ yüzeyinde relief mapping ile veriliyor.
Arazinin üstüne oturan ayrı bir kar mesh'i yok.

**Neden:** yamanın nereye konduğu fark etmiyor, sınırın kendisi kusur üretiyor.
Üç günün ölçülmüş bilançosu:

| Yama nerede | Belirti | Ölçüm |
|---|---|---|
| Arazi de sütun kadar yükselirken | karakter gömülü başlıyor | ayak 205.539, kaya 205.489, çizilen yüzey 205.98 |
| Arazi kayada kalırken | 24 m'lik kenar geometrik basamak | yama 0.45 m havada |
| Yama yalnız izde çizilirken | iz görünmüyor | sim'de oluk 0.24 m, ekranda yok — arazi örtüyor |
| Yama araziyle aynı kotta | 1.4 cm dudak, sıyırtma açıda "havada levha" | yerel sütun 0.496 / dünya 0.482 |

Dördü de aynı şeyin yüzleri: opak bir örtünün üstüne ikinci bir yüzey koyunca
o yüzeyin bittiği yer görünür.

**Sevkiyat oyunları da böyle yapmıyor.** Batman: Arkham Origins yükseklik
alanını runtime'da üretip ya tessellation (PC) ya relief mapping (konsol) ile
AYNI yüzeye uyguluyor; "üçgen yoğunluğundan bağımsız", "yarı-düşük frekanslı
detay" [GDC 2014, Colin Barré-Brisebois]. Rise of the Tomb Raider dinamik
tessellation kullanıyor, deformasyon arazinin kendi geometrisinde [GPU Pro,
"Deferred Snow Deformation in Rise of the Tomb Raider"]. İkisinde de ikinci
yüzey yok.

**Bizde tessellation yolu kapalı:** arazi bir Unity Terrain, köşe aralığı
7.32 m (30 km / 4097). Geriye konsol yolu kalıyor ve ayak izi tam olarak onun
tarif ettiği detay sınıfı.

**Bedeli, bilinçli:** relief mapping siluet vermez — izin kenarı ufka karşı
çıkıntı yapmaz — ve çok sıyırtma açıda çözünürlüğü düşer. `DECISIONS.md`.

**Ölçüldü (10:00, kar 0.45 m, tepeden bakış):** iz pikselleri luma 0.6720,
çevre 0.8436 → **kontrast %20.3**. Kare yok, havada kalan parça yok.

## Parıltıda sınırlanan şey LOD değil, ayak izi

**Kural:** `pixelFootprint` 4 cm'de kırpılıyor; LOD serbest.

**Neden:** Bowles & Wang hücreyi ayak izine göre büyütüp yoğunluğu ekran
uzayında sabit tutuyor. LOD iki seviyeyle sınırlanınca hücre büyüyemiyor,
`cellsPerPixel` şişiyor, `pTarget` sıfıra iniyor ve eşik 1'e dayanıyor —
uzakta hiçbir kristal parlamıyor. Belirti (iri dikdörtgen lekeler) kapanmıştı
ama parıltı da ölmüştü.

Dikdörtgenlerin gerçek sebebi `fwidth(posWS.xz)`: sıyırtma açıda patlıyor,
hücre metrelerce oluyor, tek hücre onlarca pikseli kaplıyor.

**Ölçüldü (10:00, ufka yakın bakış):** yakın 114, orta 45, uzak 0 parlak
nokta. Mesafe kapısı 28→50 m.

## Kar–gök çoklu yansıması

**Kural:** kar için gök ışınımı `1 / (1 − a·s)` ile büyütülüyor; a kar
albedosu, s göğün geri yansıtma payı (sabit 0.25).

**Neden:** kar gelen ışığın ~%85'ini geri gönderir, gök onun bir kısmını
tekrar aşağı yansıtır. Sonsuz seri kapalı biçimde bu çarpanı verir. Terim
olmadan kar sahasının gölgeleri olduğundan koyu çıkar.

**Ölçüm (10:00, poz dondurulmuş, bulut gölgesi kapalı, sabit bakış):**
terim kapalı 0.5120 → açık 0.8125. Katsayı gölgeye bağlıyken kazanç %58.7;
sabit 0.25 ile tam güneşte fark ölçüm gürültüsünün altında (%0.0), çünkü
orada ortamın toplam içindeki payı küçük ve tonemap omzu sıkıştırıyor.

**Katsayı neden sabit.** Bir tur gölgeye bağlandı ("güneş kısıldıysa üstümüzde
bulut vardır") ve yanlıştı: dağın kendi gölgesi açık gökte de olur. Bulut
kapsaması global olarak yayınlanmıyor (ölçüldü: `_CloudCoverage` global 0),
yani ayrım yapılamıyor — ayrım yapamayan katsayı sabit kalır.

**Tetikleyici:** bulut kapsaması global olarak yayınlanırsa `s` ona bağlanır;
tam bulutta 0.65 ile çarpan 2.4'e çıkar ve bulut gölgesindeki kar gerçekten
olması gerektiği kadar aydınlanır. Şu an bulut gölgesi altında zemin 0.09
luma ölçülüyor (güneşlisi 0.85) — bu, terimin eksik kalan yarısı.

## Neden izin duvarı göçüyor (duruş açısı)

**Kural:** `KRepose` bir tekselin komşusundan en fazla `tan(38°) × tekselBoyu`
kadar derin olmasına izin veriyor (`SNOW_REPOSE_TAN`).

**Neden 38°:** gevşek kuru karın talus açısı [Cordonnier ve ark., EG 2018, §5.4].
Fiziksel bir büyüklük; görünüme göre ayarlanmış bir katsayı değil.

**Denenip başarısız olan yol:** kenar bükümünün dalga boyunu değiştirmek
(11 → farklı ölçekler). Belirti sürdü, çünkü sorun bükümde değil BÜKÜLEN
ŞEYDE idi: 1 teksellik dik duvar ±0.9 teksel büküldüğünde lob üretir. Genlik
ile özellik boyu aynı mertebede olamaz.

**Ürettiği belirti (olmadığında):** "dikdörtgen 1 tane ayağım varmış gibi iz",
"dümdüz yürürken zigzag", "kenarlarda dağılma yok". Üçü de tek sebebin
belirtisi — ölçüm `SYMPTOMS.md`.

**Yalnız derinleştiriyor:** `trail.r = max(trail.r, min(koni, sinir))`. `min`
doğrudan yazılırsa çekirdek izi SİLER — kar sütunu birikmemişken sınır sıfır
çıkıyor. Bu bir kez yaşandı ve bütün iz dokusu 0.00 cm ölçüldü.

## Neden simülasyon adımı `Time.deltaTime` değil

**Kural:** `SnowManager.Dispatch` adımı `Time.time` farkından türetiyor.

**Neden:** `RecordRenderGraph` her kamera için ayrı koşuyor (oyun görünümü,
sahne görünümü, yardımcı kameralar) ve `Time.frameCount` bunların arasında
ilerleyebiliyor. Kare sayacına bakan muhafaza tutmuyor, her çağrı tam bir
karelik zaman uyguluyordu.

**Ölçüm:** KDeform 525 karede 1602 kez koştu (3.05 kat). Ayırt eden araç
çekirdeğe konan çağrı başına sabit düşümdü; kayıp / düşüm = çağrı sayısı.

**Ürettiği belirti:** ayak izinin saniyeler içinde kapanması. Ama hızlanan
yalnız iz değildi — oturma, kabuk, birikme ve sastrugi de aynı katsayıyla
akıyordu. Belirti izde görüldü çünkü tek gözlenebilir olan oydu.

**Telafi terimi geri alındı:** bu bulunmadan önce dolma hızına tavan
(`SNOW_MAX_FILL_RATE`) konmuştu. Gerekçesi ortadan kalktığı için terim silindi.

## Neden duvar bir yüksekliğe kadar dik duruyor (kohezyon)

**Kural:** `KRepose` yalnız `SNOW_STAND_*` yüksekliğinin ÜSTÜNDE kalan payı
göçürüyor; altı dik kalıyor. Yükseklik yoğunlukla artıyor (0.06 → 0.18 m).

**Neden:** talus açısı KOHEZYONSUZ tanelerin açısı. Kar sinterlenir ve gerçek
bir kohezyonu vardır — günlük gözlem de bunu söylüyor: karda ayak izinin
duvarı dik durur, yalnız tepesinde küçük bir göçük olur.

**Ölçüm:** saf talus modeli 22 cm'lik izi 76 cm genişletti (her yana 28 cm).
Kohezyon eklenince 42 cm; duvar 3 tekselde (7 cm) iniyor, omuz üstte kalıyor.

**Ürettiği belirti (olmadığında):** "iz şu an çok geniş".

## Neden omuz yüksekliği gürültülü

**Kural:** `SNOW_STAND_NOISE` duruş yüksekliğini yerel olarak ±%45 dalgalandırıyor,
dalga boyu 18 cm.

**Neden:** sabit yükseklik omuzun dış sınırını DÜZ bir çizgi yapıyor; iz kenarı
her yerde aynı ve keskin çıkıyor.

**Dalga boyu kuralı:** omuz 4-9 teksel (9-21 cm), dalga boyu 18 cm — omuzdan
uzun. Kısa olsaydı omuzu yok ederdi; aynı hata kenar bükümünde yapılmıştı.

**Ölçüm:** aynı izin üç kesitinde sol omuz 2.5 / 4.8 / 5.5 cm, sağ omuz
3.7 / 1.5 / 6.4 cm. Önce üçü de aynıydı.

**Ürettiği belirti (olmadığında):** "iz kenarı çok keskin, istediğim dağılmaya
sahip değil, dağılma yok bile diyebilirim".

## Neden çukurun karartması çok yansımayla telafi ediliyor

**Kural:** `MountainSurface.hlsl` çukurun görüş payını `V / (1 - a(1-V))` ile
uyguluyor, doğrudan `V` ile değil.

**Neden:** çukur göğü dar açıdan görüyor ama kaybolan gök ışığının yerine
çukurun KENDİ BEYAZ DUVARLARI geçiyor. Albedo 0.91 olan bir kovukta V=0.65
iken net kayıp %5, %35 değil. Aynı formül `SnowAmbient`'te de kullanılıyor.

**Ürettiği belirti (olmadığında):** beyaz yüzeyi düz oranla karartmak onu
GRİ yapıyor — "kar izi gri?? niye gri onu da bilmiyorum".

## Neden `SnowDentAt` bölge dışında sıfır

**Kural:** iz derinliği `SnowInsideMask` ile çarpılıyor.

**Neden:** `saturate(uv)` kenardaki tekseli bölge dışındaki her noktaya
kopyalıyor. Sınırdaki bir teksel oyuluysa o oyuk dünyaya şerit olarak yayılıyor
ve maskenin kestiği yerde DİKDÖRTGEN bir plato çıkıyor.

**Ürettiği belirti (olmadığında):** "karın içinden dikdörtgen cisimler çıkıyor";
F1 → "Kar yok" ile kayboluyor, çünkü o düğme kar derinliğini sıfırlıyor.

**Not:** durum dokusunun aynı sorunu `SnowStateAt` içinde zaten çözülmüştü
(bölge dışı dünyanın genel değerine harmanlanıyor). İzin dünya karşılığı yok,
o yüzden harman değil sıfır.

## İz rasterize edilmiyor, hesaplanıyor

**Kural:** iz bırakan nesne bir küre olarak tanımlanır; `KDeform` tekselin iki
kare arasında süpürülen doğru parçasına yatay uzaklığını kapalı formülle bulup
oymayı `batma − (R − √(R²−d²))` ile yazar.

**Neden:** eski yol nesnenin alt yüzeyini aşağıdan bakan ortografik bir
yakalamaya rasterize ediyor, sonucu 4-tap Poisson ile bulanıklaştırıyor ve
kapsama payını (`cap.a`) oyma profili olarak okuyordu (Batman GDC 2014 yolu).
Kenar o zincirin ÜÇ ayrı yerinde teksel ızgarasına takılıyordu: rasterin kendi
kenarı, blur'un tapları, kapsamanın eşiği.

**Ölçüm:** temiz zeminde, tek prob, karakter kendi yürüdü — iz kenarı ±1.5
teksel dalgalanıyordu, dalga boyu 5 teksel. Yakın planda yumru, uzakta testere
dişi. Kullanıcı bunu günlerce "zigzag" ve "tarak" olarak bildirdi.

**Denenip başarısız olan yol:** dört tur boyunca gövde yüksekliği, gövde
şekli, damga kadansı ve ızgara snap'i ölçüldü; hiçbiri belirtiyi açıklamadı.
Yol ölçüldüğünde yanal sapma **1.5 mm (0.06 teksel)** çıktı — dümdüz. Yani
kaynak hareket değil ÇİZİM yoluydu.

**Yan kazanç:** nesnenin dünya Y'si artık ize hiç girmiyor. Batma derinliğini
kar söylüyor (taşıma gücü, yoğunluk, kabuk). Gövdenin yüksekliğini karın
durumundan okuyup karı gövdeyle ezmek kapalı bir döngüydü ve 30 karelik
asenkron geri okuma onu osilatöre çeviriyordu.

## Duruş yüksekliği: gerçekçi değer, gürültünün genliğini de düzeltti

**Kural:** `SNOW_STAND_LOOSE = 1.5 cm`, `SNOW_STAND_PACKED = 7 cm`.

**Neden:** duruş yüksekliği "karın taşıyabildiği dik duvar" demek. 6 cm gevşek
kar için fazla; taze kar o kadar dik duvar tutmaz.

**Ölçüm (kâğıtta, teksel 2.34 cm, R = 15 cm, batma = 22 cm):**

| duruş | göçen pay | omuz genişliği |
|---|---|---|
| 6 cm (eski)  | 1.0 cm | 1.3 cm = **0.5 teksel** |
| 1.5 cm (yeni)| 5.5 cm | 7.0 cm = **3.0 teksel** |

Kullanıcının "çok keskin sınırları var, kenarlarda dağılma yok" belirtisinin
sayısal karşılığı bu: omuz yarım tekseldi, yani duvar.

**Gürültünün genliği aynı sayıdan türüyor.** Kenarın kayması
`durus × genlik / tan(38°)`:

| durum | kayma |
|---|---|
| durus 6 cm, genlik 0.45 (eski) | ±3.5 cm = **±1.5 teksel** → görünür zigzag |
| durus 1.5 cm, genlik 0.50 (yeni) | ±1.0 cm = **±0.4 teksel** → teksel altı |

Yani gürültü bir kez kaldırıldı (zigzag üretiyordu) ve duruş yüksekliği
düzeltildikten sonra geri kondu — çünkü aynı bağlı gürültü artık dört kat
küçük bir yüksekliği modüle ediyor. Dalga boyu 12.5 cm (5.3 teksel): teksel
ızgarasından uzun, omuzdan (3 teksel) uzun.

**Geçiş sayısı:** omuz 0.5 tekselden 3 teksele çıkınca 3 geçiş yetmiyor —
`KRepose` geçiş başına bir teksel yayıyor. `ReposeIterations` 6.

## Kar yüzeyi yer şekilleri — ölçülmüş değerlerden

**Kural:** bozulmamış kar yüzeyi dört ölçekte rölyef taşıyor: fBm tabanı,
ripple, sastrugi, mikro tane.

**Kaynak.** Filhol & Sturm 2015, "Snow bedforms: A review, new data, and a
formation model", JGR Earth Surface; Kochanski, Anderson & Tucker 2019, "The
evolution of snow bedforms in the Colorado Front Range", The Cryosphere 13:1267.

Arazide ölçülmüş yedi yer şekli ve boyutları:

| Yer şekli | Yükseklik/derinlik | Dalga boyu / aralık | Yön |
|---|---|---|---|
| plane bed | — | — | rüzgâr < 6.4 m/s **ve** kar < 1.4 gün |
| ripple | 0.5–2 cm | 10–25 cm | rüzgâra dik |
| snow step | dikey yüz < 2 cm | — | sivri uç yok |
| barchan | 7–55 cm | 40 cm+ | hilal |
| sastrugi | 14–40 cm | sivri uç aralığı 45–90 cm | rüzgâra paralel |
| snow wave | — | tepe aralığı 10–20 m | rüzgâra dik/eğik |

Rüzgâr eşikleri de ölçülmüş: kar hareketi 7–14 m/s, sastrugi oluşumu en az
20 m/s. Bu yüzden sakin havada yüzey plane bed'e yakın kalıyor.

**fBm tabanı self-affine.** Doğal yüzeylerin güç spektrumu `C(q) ~ q^(-2(H+1))`.
Oktavlar arası genlik oranı keyfi değil: frekans iki katına çıkarken genlik
`2^(-H)`. Kar için H = 0.8, oran 0.574. Bu kural olmadan oktav genlikleri elle
seçiliyor ve yüzey ya tek ölçekli (tarak) ya da gürültülü çıkıyor.

**Dalga boyları neden kısaltıldı.** İlk değerler ölçülen "snow wave" ölçeğine
(10–20 m) yakındı ve eğim 1.5–2.3° veriyordu — gözle görünmüyor. Bizim bölge
24 m; o dalga boyu tek bir eğime dönüşüp yüzeyi eğik gösteriyor. Oktavlar
1.25 / 0.63 / 0.31 / 0.16 m'ye çekildi, eğimler 5.0–7.6°.

**Öğlen görünürlük ayrı bir terim gerektiriyor.** Ölçüldü: güneş tepedeyken
(SunHeight 0.88) 7°'lik eğim NdotL'yi %1 değiştiriyor. Yatık ışıkta
(SunHeight 0.27) aynı yüzey net görünüyor. Işıktan bağımsız görünürlük
çukurların ortam örtmesinden geliyor — yüzeyin yüksekliğinden, eğiminden değil.

## Karın speküleri: buzun F0'ı ve gerçek pürüzlülük

**Belirti.** "Kar zemininde ışığın vurma açısına göre bazen sulu zemin gibi
gözüküyor." Daha önce aynı yüzey için "metalik bir görüntü" bildirilmişti.

**İlk iki şüpheli yanlış çıktı.** Pürüzlülük aralığı iki kez daraltıldı
(0.26/0.48 → 0.45/0.72) ve ikisinde de belirti sürdü. Daraltmak yetmiyordu
çünkü aralığın sıkışmış ucu hâlâ yanlış mertebedeydi.

**Ölçüm.** Öğle, 20 cm kar, düz zemin, post kapalı: diffuse 1.747 /
spekuler 4.133 — spekulerin toplam içindeki payı **%70**. Karın fiziği ~%1
söylüyor.

İki çarpan birlikte:

| Büyüklük | Eski | Fizik | Oran |
|---|---|---|---|
| F0 | 0.04 (URP dielektrik varsayılanı, n = 1.5) | 0.018 (buz, n = 1.31) | 2.2× |
| Pürüzsüzlük (sıkışmış) | 0.72 | 0.22 | — |
| GGX tepe yoğunluğu D(0) = 1/(πα²) | 52 | 0.75 | 69× |

F0 = ((n−1)/(n+1))² ile hesaplandı; n = 1.31 buzun görünür bölgedeki kırılma
indisi.

**Neden 0.72 oraya yazılmıştı.** Gerekçe şuydu: "iz yalnız kararıyor,
karşılığında parlamıyor; albedo düşerken pürüzlülük de düşmeli, kaybolan
yayınık ışığın yerini speküler alıyor." Fizikte bu yanlış — sıkışmak F0'ı
değiştirmez, yalnız yüzeyi biraz düzleştirir. İzin içi ile dışı arasındaki
fark albedodan ve yer şeklinden gelmek zorunda, speküler patlamasından değil.

**İkinci hata: aynı sayı iki yerde ayrı duruyordu.** `MountainSurface.hlsl`
0.28, `SnowLighting.hlsl` 0.45 kullanıyordu. Yorum "iki yol aynı sayıyı
kullanmak zorunda" diyordu ama sayı kopyalanmıştı. Aynı kar arazide ve kar
mesh'inde iki farklı parlaklıkla çiziliyordu. Artık ikisi de
`SNOW_ROUGH_PACKED` / `SNOW_ROUGH_FRESH` okuyor.

**Uygulama.** URP'nin `InitializeBRDFData` metalik yolu F0'ı `kDielectricSpec`
= 0.04'e sabitliyor ve dışarıdan değiştirilemiyor. `SnowInitBRDF`
(`SnowLighting.hlsl`) `InitializeBRDFDataDirect` çağırıp reflectivity'yi
kendisi veriyor. Kar çizen üç yol da bunu kullanıyor; F0 kar maskesiyle
harmanlanıyor çünkü aynı pikselde kaya da olabilir.

## Sarmalı diffuse: bölen (1+W)², W ölçülmüş nüfuz derinliğinden

**Enerji hatası.** Wrap `(N·L + W)/(1+W)` biçiminde yazılmıştı. Kâğıtta
yarımküre entegrali:

```
2π/(1+W) · ∫_{-W}^{1} (u+W) du = 2π/(1+W) · (1+W)²/2 = π(1+W)
```

Lambert'inki `π`. Yani wrap **(1+W) kat fazla enerji** çıkarıyor — yüzey
aldığından çoğunu geri veriyor. W = 0.55 döneminde %55 fazlaydı. Bölen
`(1+W)²` olmalı.

Normalizasyon KONTRASTI DEĞİŞTİRMİYOR: oran her iki formda da aynı kalıyor
(dot=0 / dot=1 oranı W'ye bağlı, bölene değil). Kontrastı belirleyen tek şey
W. Bu yüzden "wrap kapalıyken daha iyi" belirtisi normalizasyonla kapanmaz.

**W'nin fiziksel karşılığı.** Işığın kar içinde yanal yayıldığı mesafenin
yüzeyin eğrilik yarıçapına oranı.

[ÖLÇÜM: yeşil ışıkta (550 nm) karın e-katlanma derinliği 37.4 mm.]

Ripple'ın eğrilik yarıçapı `R = λ²/(4π²A) = 0.17²/(4π²·0.0029) = 25 cm`.
Oran `3.7/25 = 0.15`.

Yol: 0.55 → 0.20 (kullanıcının teşhis anahtarıyla, göz kararı) → 0.15
(ölçülmüş sayı). İlk iki değer tahmindi; üçüncüsünün arkasında bir uzunluk
ölçüsü var.

## Sastrugi arazi ölçüsüne çıkarılamadı: RMS eğim bütçesi dolu

**Arazi ölçüsü.** Filhol & Sturm 2015: sastrugi 15-40 cm derin, sivri uç
aralığı 45-90 cm. Bizde `HEIGHT × BASE = 0.180 × 0.055 = 1.0 cm` — 15-40×
eksik.

**Ama bütçe dolu.** Yüzeyin toplam RMS eğimi bileşenlerin karekök toplamı:

| Bileşen | Genlik | Dalga boyu | 2πA/λ | Derece |
|---|---|---|---|---|
| Sastrugi | 0.5 cm | 60 cm | 0.052 | 3.0° |
| Ripple | 0.29 cm | 17 cm | 0.106 | 6.1° |
| fBm (4 oktav) | 1.5 cm | 1.25 m ↓ | 0.190 | 10.8° |
| Mikro (3 oktav) | 0.12 cm ↓ | 8.3 cm ↓ | 0.163 | 9.2° |
| **Toplam** | | | **0.277** | **15.5°** |

Arazide ölçülmüş kar yüzeyi RMS eğimi 5-15°. Zaten üst sınırdayız. Sastrugiyi
10× büyütmek toplamı 0.585'e, yani **30°**'ye çıkarıyor — iki kat aşım.

**Çelişki gerçek ve çözümü sabit değiştirmek değil.** 5-15° bandı sastrugi
OLMAYAN kar yüzeyi için; sastrugi alanında eğim gerçekten yüksek. Doğru
mimari sastrugi genliğini rüzgâr maruziyetine bağlamak: korunaklı yamaçta
plane bed, maruz sırtta arazi ölçüsü. Ortalama düşük kalır, sırtta 15-40 cm
çıkar.

Kodda bu bağ YOK. `SNOW_SASTRUGI_BASE`'in yorumu "fırtınada rüzgâr çarpanı
zaten 1'e çıkarıyor" diyor ama öyle bir çarpan hiçbir yerde yazılı değil —
`BASE` sabit bir kısıcı. Rüzgâr maruziyeti (`TerrainWindShelter`) shader'a
global olarak da yayınlanmıyor.

**Neden şimdi değiştirilmedi.** Sastrugi genliği bir tur önce 4.5 cm'den
(25° eğim) düşürülmüştü; gerekçe kullanıcının "zemin titriyor / koyu lekeler"
bildirimiydi. O bildirim spekülerin toplam içindeki payı %70 iken alındı.
Aydınlatma o kadar bozukken geometriye verilen karar güvenilmez. Önce
F0 + pürüzlülük düzeltmesi ekranda görülecek.

## Kalite keyword'ü hiç derlenmiyordu — üç detay katmanı ölüydü

`SnowManager.ApplyQualityKeyword` `Shader.EnableKeyword("_SNOW_QUALITY_*")`
çağırıyordu ama **hiçbir shader'da o keyword için pragma yoktu**. Varyant
derlenmediği için `#if defined(_SNOW_QUALITY_HIGH)` her zaman false kalıyor,
`SnowDetailNormals`'ın üç katmanı (mezo 0.6 m, mikro 5 cm, ezilmiş 0.25 m)
hiç çalışmıyordu. `SnowSurfaceDetailNormal` yalnız taban normalini geri
döndürüyordu.

Yan etki tesadüfen doğruydu: `SnowLighting.hlsl`'in `#if !defined(_SNOW_QUALITY_LOW)`
bloğu (parıltı) hep açık kalıyordu — LOW da tanımsız olduğu için.

`#pragma multi_compile _SNOW_QUALITY_LOW _SNOW_QUALITY_MEDIUM _SNOW_QUALITY_HIGH`
iki shader'a eklendi. Aktif kademe Medium; mezo katmanı artık çalışıyor,
mikro ve ezilmiş High'da açılıyor.

## Sabit eşliği testi kırıktı ve neyi kontrol ettiği yanlıştı

`SnowConstantsTest` "48/55" veriyordu. Yedi ayrığın hepsi **ölü C# sabitiydi**:
`CompactGain`, `RimStrength`, `RimMax` ve beş `Sastrugi*` — hiçbiri hiçbir
yerden okunmuyordu. Eşleşecek iki taraf yoktu, yalnız iki ayrı sayı duruyordu.

Ölü sabit yanlış belge de taşıyor: C# `SastrugiLength`'i "rüzgâr yönündeki
dalga boyu" diye tanımlıyordu, HLSL'de ise LENGTH rüzgâra DİK eksende. Bir
tur bu yüzden yanlış eksende düzeltme yapıldı.

**Testin iddiası da fazlaydı.** Her HLSL define'ının C# karşılığı olmasını
şart koşuyordu ve 62 shader-only sabit yüzünden kalıcı olarak kırıktı. Gerçek
risk aynı büyüklüğün iki tarafta AYRI yazılması; yalnız shader'ın okuduğu bir
sabitin (`SNOW_ICE_F0`, `SNOW_RIPPLE_AMP`, parıltı kapıları) CPU karşılığı
olması gerekmiyor. Artık sayılıyor ama `ok`'u bozmuyor.

Ayrıca `EdgeFadeRange` gerçek bir çiftti ve tabloda yoktu — eklendi.
`AoRadius` / `AoStrength` ölüydü — silindi.

Test şimdi yeşil: 47/47.

## Uzaktaki kar neden düzdü: tek kapı üç çıktıyı birden kesiyordu

**Belirti.** "Oyuncu yakınındaki detaylar gözüküyor ama azıcık ilerisi
dümdüz. Oraya doğru yürüdükçe oradaki detaylar da geliyor."

**Sebep.** `SnowSampleSurface` üç çıktıyı (`albedoTint`, `roughAdd`,
`normalSlope`) tek bir `guc` ile ölçekliyordu ve o `guc` içinde
`SNOW_SURF_FADE 8/28` vardı. 28 m'den sonra üçü de sıfır — **yerine hiçbir
şey gelmiyordu**. Yürüdükçe mesafe düşüyor, detay "geliyor".

**Kapının gerekçesi kabartıda geçerli, renkte değil.** Ölçüldü: doku 4096²,
döşeme 2.5 m → teksel 0.61 mm. 28 m'de bir ekran pikseli ~89 teksel kaplıyor,
yani mip 6-7. Trilinear filtreleme deseni zaten ortalıyor ve yumuşak bir
lekeye çeviriyor. Renk için kesmenin karşılığı yok; kesince düzlük kalıyor.

Kabartı başka: piksel altına düşen normal aliasing ve titreme üretiyor, ve
mip'lenmiş bir normal haritası düzleşip yanlış parlaklık veriyor (Toksvig).
8-28 m korunuyor.

Yeni kapı 80-250 m. Tekrarlamayı stokastik döşeme kırıyor, o yüzden uzak
mesafede ızgara okunmuyor.

**Yan kazanç.** Kabartı kapısı kapalıyken normal ve pürüzlülük dokuları hiç
okunmuyor: uzakta doku erişimi on ikiden dörde iniyor. Önceden `guc`
sıfır olsa bile on iki okuma yapılıp sonuç sıfırla çarpılıyordu.

## Çukur gölgesinin yarıçapı sabitti ve üç kat büyüktü

`SnowReliefShadow` ufuk tanjantını `derinlik / yarıçap`'tan buluyor. Yarıçap
`SNOW_CAVITY_RADIUS = 0.135 m` sabitiydi ve gerekçesi "iz 27 cm, yarısı 13.5"
diyordu — iz **tek kapsülken** doğruydu.

Ayak izi üç kapsüle bölününce gerçek yarıçap değişti. Ölçüldü (sahnedeki
`SnowFootprintDeformer`, `bootWidth = 0.11`):

| Bölüm | Oran (`bol.z`) | Yarıçap |
|---|---|---|
| Ön taban | 1.00 | 5.5 cm |
| Topuk | 0.84 | 4.6 cm |
| Bel | 0.62 | 3.4 cm |

Ortalama 4.5 cm — sabit **üç kat büyük**. Ufuk tanjantı üçte bire düşüyor:
5 cm derinlikte 13.5 cm yarıçapla horizon 20°, 4.5 cm ile 48°. Yani çukurun
kendi gölgesi alçak güneşte olması gerekenden çok zayıf çıkıyordu.

Sabit silindi; `SnowManager.BuildTrailSegments` parça yarıçaplarını ortalayıp
`_SnowCavityRadius` globaline yazıyor. Ortalama alınıyor, en büyük değil: üç
kapsül aynı çukurun parçası ve gölgeyi birlikte kuruyorlar.

Alt sınır `max(_SnowCavityRadius, 1e-3)`: sahnede hiç deformer yoksa global
sıfır kalıyor ve `dent` de sıfır olduğu için `0/0` NaN üretirdi.

## Parıltı ölçüldü, dokunulmadı

`_SparkleIntensity = 7.0` ölçülmemiş bir sayı diye şüpheliydi. Ölçüldü:
öğle, düz zemin, post kapalı — `sparkle × 7 = 0.161`, diffuse 1.747. Pay
**%9**. Gerçek kar fotoğraflarında parıltının toplam enerji payı küçük,
tepe noktalar parlak; bu mertebe doğru.

Spekuler 150× düştüğü için parıltının GÖRELİ ağırlığı arttı ama mutlak
değeri değişmedi. Ekranda fazla görünürse ölçülecek; şüphe tek başına
değişiklik gerekçesi değil.

## Rüzgâr dokusu siperdeki yüzeye çiziliyordu — bağ tersti

`SnowSurfaceWeights` "rüzgâr" doku ağırlığını şöyle kuruyordu:

```hlsl
// RUZGAR MARUZIYETI: siperde kalan yuzey oluk tutmaz.
half ruzgar = saturate(SampleWindShadow(posWS) * 1.2 - 0.1);
```

`SampleWindShadow` **korunaklılığı** ölçüyor. Kendi tanımı (`SnowCommon.hlsl`,
spec §18.0 birebir): "> 0 → rüzgâr gölgesinde (birikme bölgesi), 0 → açık
(erozyon mümkün)". Kod o değeri **doğru orantıyla** rüzgâr dokusuna
bağlıyordu: oluklu, sastrugi çizgili doku SİPERDEKİ yüzeye çiziliyordu.

Yorumun kendisi tersini söylüyordu. İki taraf da aynı satırda duruyordu ve
üç tur boyunca kimse okumadı.

Fizik ve spec aynı yöne bakıyor: sastrugi ve oluk EROZYON şekli, spec §18.0
gölgede aşınmayı tamamen kapatıyor ("`curvW` sıfırlanır → aşınma yok, sadece
birikme"). Siperde kar birikir, yumuşak ve düz kalır.

`ruzgar = 1.0 - saturate(SampleWindShadow(posWS) * 1.2)`. Ofset (`- 0.1`)
düştü: ters yönde karşılığı yok.

**Beklenen görsel etki büyük.** Açık arazide `wRuzgar` 0'dan 0.9'a çıkıyor.
Doku dağılımı tersine dönüyor. `Ruzgar` haritasının kıvrımlı oyukları bir
kez sorun çıkarmıştı (`SNOW_SURF_EGIM_TAVANI` 0.7 → 0.35 orada düşürüldü);
eğim tavanı hâlâ 0.35 ve o düzeltme yerinde duruyor.

## Kar yüzeyi neden geometri oldu

**Belirti.** "Hafif uzak zemin detaysız gözüküyor. Kar zeminindeki detayların
render mesafesini artıralım."

İki tur boyunca doku ve LOD kapıları suçlandı; ikisi de gerçek kusurdu ama
belirtiyi kapatmadı. Sebep yapısal: **normal haritası silüete ve örtüşmeye
katkı vermiyor.** Sıyırtma açıda bir yüzeyin görünümünü tamamen o ikisi
belirliyor — tepenin arkası görünmüyorsa yüzey vardır, gölgelendirme ne
derse desin.

**Bu iş bir kez denenmiş ve fizik yüzünden geri alınmış.**
`MountainSurface.shader` yorumu: *"ayak 205.539, kaya 205.489, çizilen yüzey
205.98 — karakter yarım metre gömülü başlıyordu."* O tur kar yüksekliğinin
geometriden tamamen çıkarılmasıyla bitti.

Bu turda fizik uyumu işin **parçası**: `SnowSurfaceHeight` (C# ikizi) +
`SnowHeightParityTest` (512 örnek, 0.02 mm sapma) + `SnowGroundOffset`
(karakteri her kare yüzeye oturtuyor). Üçü olmadan aynı yere çıkılırdı.

**Neden ayrı kar mesh'i değil.** Kullanıcı kararı: mesh bu projede iki kez
sorun çıkardı. Tessellation ayrı bir nesne üretmiyor, Terrain'in kendi
üçgenlerini bölüyor.

**Ölçek tavanı.** Terrain köşe aralığı 7.32 m (30 km / 4096), donanım bölme
tavanı 64 → en ince geometri 11.4 cm. Bu aşılamaz; alt-11-cm her şey normal
haritasında kalıyor ve orada kalması doğru.

**Ölçülen değerler** (edit modda, ayrı kamera, tek değişken `_SnowDbgNoTess`):

| Aşama | Açık-kapalı fark |
|---|---|
| Görev 3 — gerçek alan bağlandı, genlikler eski | %16.95 |
| Görev 5 — drift eklendi | drift tek başına %29.54 |
| Görev 7 — genlikler arazi ölçüsünde | %48.72 |

İkili test (sabit 2 m yer değiştirme, kamera zeminin 1 m üstünde): tess açık
%45.4 gökyüzü (kamera yüzeyin içinde kaldı), kapalı %0. Yer değiştirmenin
uçtan uca uygulandığının kesin kanıtı.

## Drift ve sastrugi neden ayrıldı

Sastrugi **erozyon** şekli — rüzgâr karı oyuyor, keskin sırt ve dik yüz
bırakıyor, oluşumu 20 m/s üstü rüzgâr istiyor. Drift **birikme** şekli —
rüzgârın yavaşladığı siperde çöküyor, yuvarlak ve yumuşak. Spec §18.0 zaten
rüzgâr gölgesinde aşınmayı tamamen kapatıyor.

**Bu ayrım RMS eğim bütçesini çözdü.** İkisi aynı noktada toplansaydı yüzeyin
toplam eğimi ölçülen 5-15° bandını iki kat aşardı — bir tur önce ölçülüp
"Sastrugi arazi ölçüsüne çıkarılamadı" diye yazılmıştı. Ayrıldıkları için
`SNOW_SASTRUGI_BASE` ve `SNOW_RIPPLE_BASE` kısıtları güvenle silinebildi ve
genlikler arazi ölçüsüne çıktı.

**Ölçülen ayrım** (sahte rüzgâr gölgesi dokusu: sol yarı siper, sağ yarı açık;
her anahtar tek değişken):

| Anahtar | SOL (siper) | SAĞ (açık) |
|---|---|---|
| Drift | 43217 piksel | 0 |
| Sastrugi | 1 piksel | 227 |

## Compute shader'da iki sessiz derleme tuzağı

`SnowHeightProbe.compute` yazılırken iki kez kernel "geçersiz" çıktı ve
`GetComputeShaderMessages` **boş döndü**. Hata mesajı yok, yalnız
`FindKernel` başarısız.

1. **`fwidth` compute aşamasında tanımsız.** `SnowPikselBoyu` onu
   kullanıyordu. `SHADER_STAGE_COMPUTE` altında 0 döndürülüyor — doğru değer,
   çünkü compute'u yalnız eşlik testi kullanıyor ve orada örnekleme frekansı
   sonsuz.
2. **`SAMPLER` makrosu URP core `Common.hlsl`'den geliyor.**
   `GlobalSamplers.hlsl` ondan önce include edilirse aynı sessiz hata.
   `SnowSim.compute` zaten doğru sırayı kullanıyordu.

Ders: compute kernel'i geçersizse önce include sırasına ve fragment-only
içsel fonksiyonlara bakılır, formüle değil.


## Drift eğimi duruş açısını aşıyordu

**Belirti.** Alçak güneşte (17:39, bulut %60) tepeciklerin gölgeleri uzun,
keskin ve neredeyse siyah şeritler hâlinde çıkıyordu. Kullanıcı: "tepeciklerin
gölgelerinde sorun var, bu ne böyle."

**Sorumlu tek turda bulundu.** F1 izolasyon anahtarı: drift.

**Kâğıtta.** 30 cm genlik / 90 cm dalga boyu → eğim `2π·0.15/0.90 = 1.05`,
yani **46.3°**. Karın duruş açısı 38-45° ve **birikme o açıyı aşamaz** — aşan
malzeme akar. (Sastrugi aşabiliyor çünkü erozyon şekli: rüzgâr oyuyor, yüzey
sertleşiyor.)

**Genlik değil dalga boyu değişti.** Genliği kesmek tepecikleri yok ederdi;
dalga boyunu uzatmak onları BÜYÜK ama YUMUŞAK yapıyor — birikme şekilleri
zaten öyle. [KAYNAK: Filhol & Sturm 2015, dune ölçeği 1-5 m.]

30 cm / 2.00 m → eğim `2π·0.15/2.00 = 0.471`, yani **25.2°**.

**Görev 6'daki "ayrım bütçeyi çözdü" iddiası fazlaydı.** Ayrım yalnız uçlarda
çalışıyor (maruziyet 0 veya 1); ortada iki katman da kısmi ve genlikleri
karekök toplanıyor. Maruziyet 0.5'te ölçülen toplam:

| Bileşen | Eğim katkısı |
|---|---|
| Sastrugi (yarım) | 0.524 |
| Drift (yarım, düzeltilmiş) | 0.236 |
| Ripple | 0.222 |
| fBm | 0.190 |
| Mikro | 0.160 |
| **Toplam RMS** | **33.6°** |

Arazide ölçülmüş kar yüzeyi RMS eğimi 5-15°.

**İkinci tur: dalga boyunu uzatmak yanlıştı.** 30 cm / 2.00 m eğimi 25°'ye
indirdi ama tepecikleri büyüttü; gölgeler metrelerce geniş amip lekelerine
döndü (kullanıcı: "çok tuhaf duruyor"). Dalga boyu gölgenin BOYUNU değil
ALANINI değiştiriyor — boy yüksekliğe bağlı, genişlik tepeye.

İki turun ortak paydası **genlikti**. Referans görüntüdeki tepecikler 40-100
cm aralıklı; 2-3 m ölçek "snow wave" (10-20 m) ile dune arası, kar yüzeyi
değil.

**Üçüncü tur: genlik yarıya, dalga boyu geri.** 15 cm / 90 cm → eğim 27.6°,
alçak güneş gölgesi 85 cm.

**Sastrugi de aynı hataydı ve aynı turda düzeltildi.** 20 cm / 60 cm → 46.3°,
gölge 1.13 m — drift'in ilk hâliyle birebir aynı. Erozyon şekli olduğu için
duruş açısını aşabilir ama 46° RMS olarak fazla ve aynı belirtiyi üretirdi.
Aralık 0.60 → 0.90 (arazi ölçüsünün üst ucu, dışına çıkılmadı): eğim 34.9°.

| | Eğim | Alçak güneş gölgesi |
|---|---|---|
| Drift | 46.3° → **27.6°** | 1.70 m → **0.85 m** |
| Sastrugi | 46.3° → **34.9°** | 1.13 m → **0.85 m** |
| **Toplam RMS** | 39° → **28.8°** | |

Hedef bandın (5-15°) hâlâ üstünde. Ama o band DÜZ kar alanı için; burada
drift ve sastrugi alanı çiziliyor ve orada eğim gerçekten yüksek. Bir sonraki
belirtide bakılacak yer fBm (0.19) ve mikro (0.16) — ikisi birlikte 0.25 ve
hiçbir arazi ölçümüne dayanmıyorlar.

## Deniz: Phillips değil TMA spektrumu

Tessendorf'un 2001'de kullandığı Phillips spektrumu yüksek dalga sayılarında kötü
yakınsıyor ve sanatçının kazanç/filtre parametrelerini elle ayarlamasını gerektiriyor.
Horvath'ın TMA modeli geniş bir rüzgâr hızı **ve su derinliği** aralığında elle ayar
istemeden makul sonuç veriyor.

Belirleyici olan derinlik: kıyıdan bakılan bir denizde derinlik parametresi zorunlu,
Phillips'te yok. `[KAYNAK: Horvath 2015, DigiPro]`

**Kitaigorodskii sönümünün tek satırlık hali TUZAK.** `1 − ½(2−ωh)²` parabolü
`ωh > 2` için geri düşüyor ve `saturate` onu sıfıra kırpıyor; bütün kısa dalgaların
enerjisi siliniyor. Ölçüldü: 60 m derinlikte kademe 1 ve 2'nin `h0` dokusu tamamen
sıfır çıktı, oysa tepe dalga boyu 12.7 m ve tam o kademede. Doğru tanım üç dallı:
`ωh ≥ 2` için 1.

## Deniz: Schlick değil tam Fresnel

Schlick yaklaşıklığı sıyırma açılarında belirgin sapıyor ve kıyıdan bakılan denizin
karakteri tam orada. Kâğıtta: 2° bakışta F = 0.805, 6°'de 0.527, 45°'de 0.029.

İki dallı tam form doğrudan Tessendorf'un örnek shader'ından; maliyeti bir `acos`,
bir `asin` ve iki trigonometrik oran — yüzey zaten tam ekranı kaplamıyor.

## Deniz: çok seviyeli clipmap değil tek ızgara

Geometry clipmap kurulsaydı `[KAYNAK: Asirvatham & Hoppe, GPU Gems 2]`'nin **altı**
parçası birlikte gerekirdi: tek sayı ızgara boyutu, 12 blok, dört `m×3` fix-up şeridi,
dört yönelimli L-trim, dejenere üçgen çevresi, `alpha = max(αx, αy)` geçiş harmanlaması.
Biri eksikse mesh yırtılıyor, delik açıyor veya titriyor.

Yerine tek sürekli mesh: her halkanın quad boyu bir öncekinin tam 2 katı ve halkalar
arası vertex **paylaşılıyor**, yani T-junction yapısal olarak imkânsız.

**Hizalama ispatı:** tüm quad boyutları en ince quad boyutunun ikinin kuvveti katı,
dolayısıyla en ince quad boyutuna eşit TEK bir snap adımı her halkanın vertex'lerini
kendi kafesinde tutuyor. Seviye başına ayrı snap gerekmiyor, seviyeler arası kayma
olamıyor.

Ölçüldü: 280 833 vertex, 558 080 üçgen, yetim vertex 0, dejenere üçgen 0.

**Bedeli:** üçgen sayısı yalnız halka sayısına bağlı, quad boyuna değil. Spec'in
kalite tablosundaki üçgen sayıları (180k/480k/900k) bu mesh'te tutmuyor; kalite
kademesi neyin ne kadar yakından çözüldüğünü değiştiriyor, kaç üçgen çizildiğini değil.

## Deniz: FFT boyutu keyword değil uniform

`numthreads` bir shader keyword'üne bağlansaydı her varyant için ayrı
`GetKernelThreadGroupSizes` ve ayrı dispatch sayısı gerekirdi. Bunun yerine
`numthreads` her zaman 256; çalışan FFT 128 ise fazla iş parçacıkları boşta döner.

**Erken `return` edemezler.** `GroupMemoryBarrierWithGroupSync` ayrık bir dalda
tanımsız davranıştır. Yalnız bellek işlemleri kapatılıyor; döngü sınırı sabit
tamponundan geldiği için grup içinde aynı ve döngü uniform kalıyor.



## Kum bandı metreyle tanımlı ama görünen şey yerdeki genişliği

Kıyı kumu, deniz kotunun altında ve üstünde bir yükseklik bandı olarak tanımlı.
Bandı doğrudan metre olarak seçmek yanıltıcı: ekranda görünen, bandın **yerdeki**
genişliği ve o genişliği kıyının eğimi belirliyor.

**Ölçüldü** (30×30 km arazi, deniz kotu 30 m, 1400² örnek):

| Büyüklük | Değer |
|---|---|
| Bant içindeki ortalama eğim | 2.14° |
| Bant içindeki en dik örnek | 5.13° |
| 1 m yükseklik ≈ yerde | ~27 m |

Kesitlerde ±9/−6 m'lik bir bant 293–720 m genişliğinde çıkıyordu (bir kesitte,
neredeyse düz bir alanda 6527 m). O bir kumsal değil, kum ovası.

Seçilen: **üstte 1.6 m, altta 1.2 m, geçiş 0.6 m** → kuru şerit ~43 m, su altındaki
sığ taban ~32 m. Gerçek bir kumsalın fırtına bermi bu mertebede.

**Bu sayı dağın boyuna değil, kıyının eğimine bağlı.** Kıyı yeniden oyulursa
yeniden ölçülmeli (`SCALE.md`).

## Kum eğim sınırı 6°, duruş açısı 34° değil

Kum ~34°'ye kadar durur; sınır oraya konsaydı bu kıyıda **hiç** devreye girmezdi —
bandın en dik örneği 5.13°. Eğim o hâlde bir koşul olmaktan çıkıyor ve kıyının
tamamı tek tip kum oluyor. 6°'de yatık şeritler tam kum alıyor, 5° civarındaki
yerler kaybediyor: yamalanma yalnız gürültüden değil arazinin kendisinden de geliyor.

**Pencere kosinüs olarak CPU'da hesaplanıyor.** Kaya ve çakıl maskeleri
`cos(sınır) ± 0.08` yazıyor; bu 38°'de çalışıyor ama sığ bir sınırda kırılıyor:
`cos(6°) + 0.06 = 1.05` ve hiçbir yüzey oraya varamıyor — maske dümdüz zeminde bile
0.73'te doyuyordu. CPU `cos(sınır ± 3°)` gönderiyor, pencere sınır nerede olursa
olsun üç derece kalıyor.


## Deniz plastik görünüyordu: yansıyan gökyüzü uydurmaydı

Kullanıcının ifadesi: "denizin yüzeyi plastik gibi", "denizin renkleri berbat".

Ölçüldü, kod okunarak: `SeaEnvironmentBridge.SkyColor` ve `HorizonColor`
`[SerializeField]` iki sabit döndürüyordu — `(0.69, 0.84, 1.00)` ve
`(0.80, 0.86, 0.92)`. Deniz gerçek gökyüzünü hiç okumuyordu.

İki belirti buradan çıkıyor:

1. **Ufukta renk yanlış.** Sıyırma açısında Fresnel 1'e gider, yani yüzey
   TAMAMEN yansımadır ve ufuktaki denizin rengi göğün rengi OLMAK ZORUNDADIR.
   Kaydedilen karede gök gri, deniz ufka kadar turkuaz. Fiziksel olarak imkânsız.
2. **Plastiklik.** Sabit bir yansıma + tek Blinn lobu (`pow(dot(N,H), 2/r²)`)
   plastik tarifidir: yansıma açıyla değişmiyor, speküler sıyırmada uzamıyor.

Düzeltme, ikinci bir kaynak KURMADAN: sahne zaten her karede ortamı pişiriyor
(`SkyAmbientBaker` → `DynamicGI.UpdateEnvironment()`), yani yansıma probe'u
gerçekten çizilen gökyüzü. `GlossyEnvironmentReflection` onu pürüzlülüğe göre
örnekliyor. Güneş lobu GGX'e çevrildi ve Fresnel ile ağırlıklandırıldı —
eskiden ham toplanıyordu, yani dik aşağı bakarken bile güneş yüzeyde yanıyordu.

Su kütlesinin rengi de sabitti; artık albedo gibi davranıyor ve gök ışınımıyla
güneş üstüne biniyor. Eskiden gece de aynı turkuazdı — arkasında ışık olmayan
bir renk.

**Bilinen sınır:** hacimsel bulutlar skybox'tan SONRA çizilen bir render
özelliği, yani pişen küpe girmiyorlar. Kapalı gökyüzü probe'a hâlâ mavi olarak
ulaşıyor. Kapsama yansımayı gri ve sönük bir kubbeye çekiyor; bulutlar bir
probe'a girdiği gün bu terim silinir (`DECISIONS.md`).


## Deniz tek boy dalgadan ibaretti: spektrumun tek tepesi vardı

Kullanıcının ifadesi: "dalgalar çok düzenli", "dalgalar irili ufaklı olmalı",
"her şey çok hızlı hareket ediyor".

**Ölçüldü.** Kıyıda `WindField.Severity = 0.2` (hava sürücüsü etek bandını
sıfıra çiviliyor, `openingIntensity = 0`) ve şiddet→hız eğrisi dördüncü kuvvet:

    FreeAirSpeed = lerp(0.6, 14, 0.2^4) = 0.62 m/s

U₁₀ = 0.62 m/s ve fetch 12 km ile JONSWAP tepesi:

    omega_p = 22 (g²/(U F))^(1/3) = 5.16 rad/s  ->  Tp = 1.22 s,  lambda = 2.3 m

Yani denizin tamamı 2.3 metrelik tek boy bir kırışıklıktı. "Hep aynı" ve "çok
hızlı" şikâyetlerinin ikisi de tek sebepten: **spektrumun tek tepesi vardı ve o
tepe çok kısaydı.**

### Düzeltme: çift tepeli spektrum

Gerçek bir açık kıyı rüzgâr sıfırken bile ölü değildir — yüzlerce kilometre
ötedeki fırtınaların ürettiği ölü dalga (swell) gelir; uzun, yavaş, dar bantlı
ve yerel rüzgârla ilgisi yok. Spektruma ikinci bir tepe eklendi:

- **Rüzgâr denizi** — yerel U₁₀, yerel fetch (150 km, açık okyanus).
- **Swell** — periyottan verilen tepe (10 s → 156 m), yüksek gamma (dar bant),
  sabit yön dağılımı, rüzgâr yönünden 38° kaymış.

Kayma önemli: swell ile rüzgâr denizi çaprazlaşınca fitilli desen kırılıyor.

**Ölçüm, U₁₀ = 8 m/s** (`To The Summit/Sea/Test Wave Field`):

| kademe | bant | önce rms(h) | sonra rms(h) |
|---|---|---|---|
| 0 | λ > 48 m | ~0 | 0.680 m |
| 1 | 9–48 m | 0.689 m | 0.464 m |
| 2 | λ < 9 m | 0.139 m | 0.096 m |

Rüzgâr tepkisi 1.21× → **3.31×** (rüzgâr 5 kat arttığında). Sakin havada
Hs ≈ 1.65 m, fırtınada Hs ≈ 5.5 m.

`swellAlpha` kâğıtta değil ölçümle bulundu: enerji alpha ile doğru orantılı,
rms karekökle. İlk denemede tier 0 rms 1.92 m çıktı (Hs ~7.7 m, sakin hava için
saçma); hedef 0.25 m rms için alpha × (0.25/1.92)² uygulandı.

### Kademe boyları asal seçildi

512 / 128 / 24'te ilk ikisinin ortak çarpanı 128'di, yani iki döşeme aynı yerde
faza giriyor ve tekrar gözle görünüyordu. Yeni boylar **967 / 191 / 37** — üçü de
asal, ikişerli ortak çarpanları yok.
[KAYNAK: rtryan98, "Ocean Rendering" — *"if a common factor for any two values
of L exists, then the tiling will be visible"*]

Kademe 0 ayrıca büyüdü: 512 m'de 156 metrelik swell'in döşemeye üç periyodu
sığıyordu, yani üç mod — o kadar az mod tanımı gereği periyodik desendir.
967 m'de altı.

## Ölçüm aracı iki koşuyu ayırt edemiyordu

`Test Wave Field` girdilerini yazmıyordu. Farklı ayarlarla iki koşu AYNI metni
üretti ve Unity ikisini tek konsol girdisine katladı — ikinci ölçüm hiç
olmamış gibi göründü. Rapor artık fetch, patch, swell periyodu ve alpha'yı
başlığa basıyor. Girdisini adlandırmayan bir rapor bir öncekinden ayırt edilemez.


## Köpük hiç doğmuyordu: eşik ölçülen en küçük Jacobian'ın altındaydı

Kullanıcının ifadesi: "kıyıdaki köpük beyazlığı rezalet, kağıt gibi, köpükle
alakası yok, smooth değil".

**Ölçüldü.** Beyaz başlık köpüğü (`whitecap`) Jacobian'dan doğuyor:

    target = saturate((SEA_FOAM_J_THRESHOLD - J) / SEA_FOAM_J_RANGE)

Eşik 0.55. Tam fırtınada (U₁₀ = 15 m/s) tüm alandaki **en küçük** Jacobian
0.580 ölçüldü. Yani `target` her yerde tam sıfır: **açık denizde hiçbir rüzgârda
tek bir beyaz başlık doğmuyordu.** Ekranda görülen tek köpük kıyı bandıydı, o da
tek başına duruyordu — bu yüzden "köpükle alakası yok".

Sebep sabit `choppiness = 1.1`. Yatay yer değiştirme yüzeyi katlayamayacak kadar
küçüktü. Sakin bir ölü dalga gerçekten yuvarlaktır ve kırılmaz; fırtına denizi
gerçekten kırılacak kadar diktir — **tek sayı ikisini birden karşılayamaz.**

Choppiness rüzgâra bağlandı (sakin 0.55 → fırtına 2.4, 15 m/s'de tam).
Ölçüm: fırtınada min(J) **0.580 → 0.219**, yani eşiğin altına iniyor ve köpük
doğuyor. Sakin havada üstünde kalıyor, köpük yok.

Choppiness İKİ yerde yazılıyor (compute'a simülasyondan, vertex için global
olarak `SeaManager`'dan). İki ayrı `Lerp` biri düzenlendiğinde ayrışır ve
köpüğün hesaplandığı yer değiştirme, mesh'in kurulduğu yer değiştirmeyle
tutmaz olurdu — `SeaSettings.ChoppinessAt` tek kaynak.

## Kıyı köpüğü kâğıt kenarıydı: gradyan çarpanla eziliyordu

    shoreFoam = saturate((shoreFoam - breakup * 0.45) * 2.5);

O `× 2.5` bandın tüm geçişini iki değere sıkıştırıyordu: içi düz beyaz, kenarı
kesik. Kâğıt tarifi.

Gürültü artık **kaplamayı karartmıyor, su çizgisini oynatıyor**: bandın ölçüldüğü
derinliğe ekleniyor. Aynı düzensiz dış hat çıkıyor ama gradyan yerinde kalıyor —
kenar çizgiyle bitmek yerine yamalara dağılıyor.

Üstüne üç şey daha:

- **Kabarcık dokusu.** Beyaz başlıkta gürültü 0.55–1.30 arası bir çarpandı, yani
  yalnız soluklaştırıyordu; artık kaplamayı ALTTAN yiyor, desende delik açıyor.
- **İnce köpük saydam.** Doğrusal harman her köpük izini eşit opak yapıyordu.
  Kaplamanın smoothstep'i alınıyor: ince köpük altındaki suyu gösteriyor.
- **Köpüğün speküleri.** `_SeaFoamRoughness` tanımlıydı ve HİÇ OKUNMUYORDU;
  köpük hiç speküler almıyordu, kırılan tepenin ıslak parlaklığı yoktu.
- **Kabarma tek çizgi hâlinde ilerlemiyor.** Kıyı boyunca yavaş bir alan
  fazı kaydırıyor: bir koy dolarken yanındaki boşalıyor.


## Köpüğün kabarcığı ve kıyının izi — dokusuz ve tampon-suz

### Kabarcık: value noise köpük değildir

Köpük deseni üç oktav value noise'tan geliyordu. Value noise kaç oktav konursa
konsun **yumuşak tepeler alanı** üretir; köpük ise birbirine sıkışmış yuvarlak
kabarcıklar ve aralarındaki ince duvarlardır. "Kâğıt gibi" şikâyetinin bir
parçası bu: desen boya lekesi gibi okunuyordu.

Worley (hücresel) alan tam olarak o yapıyı tarif ediyor. İki ölçek üst üste
(iri kabarcıkların arasına küçükleri sıkışıyor), sonuç kareleniyor ki duvarlar
ince kalsın — doğrusal sönüm her kabarcığı yumuşak bir tümseğe çeviriyor ve
kütle yine boyaya dönüyor. Doku gerekmiyor.

### Kıyı izi: periyodik bir olayın hafızası hesaplanır, saklanmaz

Kıyı köpüğü faza göre yalnız **parlayıp sönüyordu**: şerit yerinde duruyor, ne
bir dalga çıkıyor ne geriye bir şey kalıyordu. Gerçek kıyı köpüğü iki iş yapar —
dalga gelir taze köpüğü serer, sonra su çekilir ve solan dantel bir kalıntı
bırakır.

Kalıntı geçmiş ister, geçmiş de normalde kalıcı bir doku ister (kamera-göreli
bir RT, her kare kaydırma ve sönüm). **Gerekmedi:** kabarma PERİYODİK, yani "bu
nokta en son ne zaman suyun altındaydı" kapalı biçimde biliniyor. Kosinüs
kabarmasında bir noktanın örtülü kaldığı pencere tepe etrafında simetrik, o
yüzden suyun o noktadan çekildiği faz bir `acos` ile çıkıyor.

    reach      = derinlik / bant derinliği
    surge      = 0.5 - 0.5 cos(2π faz)
    taze       = bore'un şu anda durduğu yer
    yarıPencere= acos(1 - 2 reach) / 2π
    geçenSüre  = frac(faz - (1 - yarıPencere))
    kalıntı    = exp(-geçenSüre × 2.4)

Bedeli bir `acos` ve bir `exp`. Kazanç: kamera-göreli bir RT, yeniden izdüşüm
ve kare kare sönüm dispatch'i yok.


## Köpük kâğıt gibiydi: HDR ışıkla çarpılıp beyaza kırpılıyordu

Kullanıcının ifadesi: "deniz köpüğü çok yoğun, aşırı yoğun, çok fazla beyazlık var
ve köpük gibi değil", "köpükte tuhaf desenler var".

Köpüğün ışığı şöyleydi:

    foamLight = gunesRengi * (NoL + spec*0.25) + skyRefl * 0.35
    renk      = foamColor * foamLight

`skyRefl` ortam probe'undan geliyor ve HDR: parlak gökte üç kanalda da 1'in belirgin
üstünde. 0.93'lük bir köpük rengiyle çarpılınca sonuç her kanalda taşıyor, ton eşleme
saf beyaza kırpıyor. **Köpüğün içindeki her kabarcık, her kenar, her gradyan orada
düzleşiyor.** "Kâğıt gibi" tam olarak bu; ve köpüğü artırmak yalnızca daha çok beyaz
üretiyordu, çünkü zaten doymuştu.

Köpük artık herhangi bir difüz yüzey gibi ışıklanıyor — gökyüzü ışınım olarak
(`SampleSH`), toplam albedodan ÖNCE 1'e kırpılıyor. Renk de 0.93'ten 0.78'e indi:
deniz köpüğü kâğıt beyazı değildir.

Kaplama tarafında üç düzeltme:
- Kabarcık gürültüsü kaplamayı yiyordu ama arkasından gelen `× 1.4` yediğini geri
  koyuyordu — desen yine kapanıp levhaya dönüyordu. Çarpan kalktı, delikler açık kaldı.
- Kırılma kazancı 1.60 → 0.85.
- Kıyı bandı 1.2 m derinlikti; 2.14°'lik bu kıyıda o **32 metrelik** bir beyaz şerit
  demek. 0.6 m'ye indi.
- Köpük hiçbir zaman tam opak değil (0.92 → 0.80): gerçek köpüğün altından su görünür.

## Köpükteki tuhaf desenler: kıvrım yönü düz suda gürültüydü

Köpük deseni `foldDir`'e göre döndürülüp geriliyordu (`atan2(foldDir)`). `foldDir`
türev dokusunun zw kanalı; **neredeyse düz bir denizde o değer sayısal gürültü.**
Sonuç piksel başına FARKLI bir dönme, yani dalgalarla hiç ilgisi olmayan sürtme
lekeleri.

Kıvrımın büyüklüğü ölçülüyor artık: zayıfsa eksen rüzgâr yönüne düşüyor — gerçek
köpük şeritlerinin dizildiği yön zaten odur.

## Kıyı çizgisi çizilmiş bir çizgide bitiyordu

Deniz mesh'i `depth <= 0`'da kesiliyor. Köpük o geometrik kenarda tam parlaklıkta
bitiyor ve karşısında kuru kum başlıyor: bir yanda beyaz, öbür yanda koyu, arada
hiçbir şey yok.

Gerçek kabarma durgun su çizgisinde bitmez — kumsala çıkar ve çekilirken incelen bir
dantel bırakır. O kısmı **arazi** çiziyor artık. İkinci kaynak değil: deniz zaten
kabarma kotunu yayınlıyor (`_SeaWetLevelY` fazı taşıyor), kum da onun altına kalıntıyı
çiziyor. Gürültü kaplamayı alttan yiyor, böylece dantel kendi kenarında bitmiyor.

`runupMaxDepth` 0.45 → 1.1 m: ıslak şerit de kumsalda yukarı çıkıyor.

## Denizin rengi: kırmızısı sıfır olan bir turkuaz

`upwellingColor` `(0, 0.2, 0.3)` idi. Kırmızı kanalı TAM SIFIR olan bir su, hangi ışık
altında olursa olsun havuz turkuazı verir — gerçek deniz suyunun geri saçtığı ışıkta
az da olsa kırmızı vardır ve seviye çok daha düşüktür. `(0.03, 0.11, 0.14)`.


## Kum plastik görünüyordu: ıslaklık bandının tabanı yoktu

Kullanıcının ifadesi: "niye ışık vurduğunda plastik bir görüntü oluşuyor", zeminde kar
yokken, doğrudan kuma bakarken.

İlk şüpheli yanlıştı — "üstündeki kar" dedim, zeminde kar yoktu.

**Dokular ölçüldü** (`Assets/Textures/Sand`, 256² örneklem):

| Harita | Ortalama |
|---|---|
| Albedo | 0.61 / 0.54 / 0.43 — sıcak bej |
| Pürüzlülük | 0.67 — kum kendi başına mat |
| AO | 0.92 |

Yani ekrandaki gri ve cila dokudan gelmiyor. Sebep tek satırda:

    float seaWet = 1.0 - smoothstep(_SeaWetLevelY - _SeaWetFadeM, _SeaWetLevelY, worldPos.y);

Bu bir bant değil **yarım uzay**: su çizgisi kotunun altındaki HER nokta 1 dönüyor —
denize bir metre mi bin metre mi uzakta, hiç bakmıyor. İki sonucu birden:

    albedo   = 0.55 x (0.61, 0.54, 0.43) = (0.34, 0.30, 0.24)   koyu gri
    puruzluluk = 0.35 x 0.67 = 0.23                              cila

0.23 pürüzlülük ıslak kum değil, verniklenmiş yüzeydir; geniş ve yumuşak parlama
oradan geliyor.

Islaklık artık gerçek bir bant: kabarma çizgisinden `_SeaWetBandM` (1.6 m) aşağıya
kadar. Altı zaten su altında ve denizin kendisi çiziyor. Islak kumun parlaklığı da
0.35 yerine 0.65 çarpanıyla — ıslak kum gerçekte 0.40-0.45 pürüzlülükte durur.

Aynı hata yeni eklenen kıyı dantelinde de vardı (o da tek yanlı bandı okuyordu);
ikisi artık aynı bandı paylaşıyor.


## Dört yüzey sisi hiç almıyordu ve derleme hatası vermiyordu

`MixFog` Unity'nin kendi sisini uyguluyor. Sahnede `m_Fog: 0` ve çalışma zamanında
`RenderSettings.fog` yazan tek satır yok — yani o çağrı **kimlik fonksiyonu**, etkisi tam
sıfır. Hata vermiyor, uyarı vermiyor; bu yüzden dört yüzeyde birden fark edilmeden kaldı:

    SeaLit.shader           MixFog(color, IN.fogCoord)
    SnowCoverObject.shader  MixFog(color, IN.fogFactor)
    SnowfallParticle.shader MixFog(color, IN.fogFactor)
    BikeSurface.shader      (pragma kalıntısı; kendisi zaten ApplyHeightFog kullanıyordu)

Belirti: fırtınada (140 m görüş) dağ 300 metrede kaybolurken deniz ufka kadar keskin,
bisiklet sise gömülüyken yanındaki karlı kaya net. `HeightFog.hlsl`'in kendi başlığı bunu
zaten yasaklıyor: *"her yüzey aynı havada durur"*.

Aynı hata bisiklette bir kez yaşanmış ve düzeltilmişti (`BikeSurface.shader` yorumu);
referans uygulama elimizdeydi.

**Kabul kriteri sayıya bağlandı:** `grep -rn "MixFog" Assets` boş dönmeli. Ölü bir çağrı
derleyiciden geçtiği için tek koruma budur.

**Deniz için ek kontrol:** `ApplyHeightFog` → `SampleTerrainHeight` UV'yi `saturate` ile
kırpıyor (`VolumetricFogShared.hlsl`), yani deniz mesh'i arazi sınırının dışına taştığında
kenar değeri okunuyor — artefakt yok, ayrı bir clamp gerekmedi.

## Bulut gölgesi cookie'si üç yüzeye hiç ulaşmıyordu

Bulut gölgesi ana ışığın cookie dokusuna yazılıyor; yüzeyin onu uygulaması gerekiyor.
Uygulayan yalnız arazi (elle) ve kar tanecikleriydi. Deniz, karlı nesneler ve bisiklet
tam güneşle parlıyordu — "gökyüzü kapalı ama nesneler güneşli", `CLAUDE.md`'nin atmosfer
tutarlılık kuralının tam ihlali.

**İki ayrı reçete var ve karıştırılırsa ya hiç uygulanır ya iki kez:**

- `UniversalFragmentPBR` kullanan shader (bisiklet): `#pragma multi_compile_fragment _
  _LIGHT_COOKIES` **yeter**, URP kendi örnekler.
- Işıklandırmayı elle yazan shader (arazi, deniz, karlı nesne): pragma **artı**
  `mainLight.color *= SampleMainLightCookie(positionWS)`.

Denizde ayrıca `GetMainLight()` argümansız çağrılıyordu — bu overload
`shadowAttenuation = 1` döndürüyor ve konum almadığı için cookie de örnekleyemiyor. Dağın
gölgesindeki su hâlâ güneş yolu çiziyordu. `GetMainLight(shadowCoord)` ile ikisi birden
geldi; parıltı, su rengi ve köpük üçü de `mainLight.color` okuduğu için tek çarpım
zincirin tamamına yayılıyor.

## Alpenglow fırtınayı görmüyordu

`ApplyAlpenglow` gücü `horizon² × alive × ayar` ile yazıyordu — yağış, kapsama, rüzgâr,
hiçbiri yok. Fırtınalı şafakta kalın bulut kütlesinin arkasındaki dağ yüzü yine kızıl
yanıyordu; oysa sis paleti aynı durumda `duskOvercast` ile bilerek soluyor. Tek göğün iki
türevi birbirini yalanlıyordu.

Kapsama çarpanı eklendi (`lerp(1, 0.25, coverage)`). **Sıfırlanmadı:** alpenglow'un iki
fazı var — doğrudan huzmenin yüzü sıyırması ve yüzün kızıla boyanmış gökten aldığı artçı
faz. Bulut ilkini öldürür, ikincisini yalnız söndürür.

Kapsama, göğün, sisin ve bulutların okuduğu AYNI yerden geliyor
(`AtmosphereController.Coverage`) — ikinci bir eşleme kurulmadı.

## Hüzme invariantı yorumda tutmuyordu

`TimeOfDay` yorumu "`CurrentSunColor × intensity` gerçek hüzmeye eşit kalır" diyordu.
Matematik:

    CurrentSunColor = Tint(beam · sunColor) · sunFade
    intensity       = sunIntensity · SunBlend · max(beam) · sunFade
    çarpım          = beam · sunColor · sunIntensity · SunBlend · sunFade²

`LowSunFade` **iki kez** giriyor. Kare BİLİNÇLİ — hemen üstündeki yorum zaten "kısıcı
renge de uygulanıyor, yoksa `Tint()` normalize ettiği için bulutlar bir anda pembeleşiyor"
diyor. Yanlış olan sayı değil, invariant cümlesiydi. Sayıya dokunulmadı; yorum düzeltildi.

## Ay gölge atıyor, tooltip "atmaz" diyordu

`MarkAsSun`'ın gece devri ayı ana ışık yapıyor ve `MountainSceneBootstrap` ona
`LightShadows.Soft` kuruyor. Tooltip kod değişirken geride kalmış.

## `TimeOfDay` ay varsayılanı sahneden 10.25 kat sapmıştı

Alan varsayılanı `moonIntensity = 0.204f`, kurulum betiği `0.0199f` yazıyor. Sahne
pratikte kazandığı için hiçbir şey yanlış görünmüyordu; ama bootstrap çalışmadan açılan
ya da yeni bir sahneye eklenen `TimeOfDay` geceyi on kat parlak yakıyordu. Aynı kural
`sunIntensity` için zaten yazılıydı, aya uygulanmamıştı. Varsayılanlar eşitlendi —
davranış değişmiyor.

## Gölge mesafesi: belge 60, yorum 50, gerçek 150

`PC_RPAsset.asset` ve `AtmosphereSettings.asset` ikisi de 150; `ApplyShadowDistance`
pratikte hep 150'e oturuyor (berrak görüş 25 km × 0.8 > 150). Belge ve yorum artık
gerçeği söylüyor. Gölge mesafesi dağın boyuna bağlı değil — görüşe bağlı, o yüzden
`SCALE.md` kaydı gerekmiyor.


## Kırılma ölçütü neden pikselin kotunu okuyamaz

Kırılma ölçütü `H / h > gamma` — H **dalganın yüksekliği**, h derinlik. Kodda H yerine
`2 * |y - deniz kotu|` vardı: pikselin o andaki dikey yer değiştirmesi.

Bu ikame iki şeyi bozuyor:

1. **Çukur da tepe kadar uzaktır.** Durgun kottan sapan her nokta "dalga" sayılıyor, yani
   sığ suda yüzeyin tamamı ölçütü geçiyor. Ölçüldü: |y − kot| = 0,40 m iken köpük 20 m'ye
   kadar 0,75 alfa. Göz 1,7 m'deyken ekrandaki suyun %87'si o mesafeden yakın.
2. **Hava durumu düşüyor.** Sakin denizde de fırtınada da sapmanın MUTLAK değeri alındığı
   için ölçüt neredeyse aynı yerde tetikleniyor. Oysa sörf kuşağının genişliği doğrudan
   deniz durumunun ölçüsüdür.

Hs zaten hesaplanıyordu (`SeaRuntimeState.SignificantWaveHeight`) ama yalnız HUD'a
gidiyordu. Global olarak yayımlandı; kırılma onu yerel derinliğe göre sığlaştırıp
kullanıyor. Yeni bir büyüklük uydurulmadı — var olan büyüklük doğru yere bağlandı.

Sonuç aynı kıyı kesitinde ölçüldü — köpüğün bittiği mesafe:

| rüzgâr | Hs | köpük sınırı |
|---|---|---|
| 0,5 m/s | 0,10 m | 3 m |
| 3 m/s | 0,59 m | 29 m |
| 8 m/s | 1,58 m | 88 m |
| 20 m/s | 3,96 m | > 120 m |

**Tepe çarpanı neden var.** Hs derinliğin fonksiyonu, yani tek başına kıyıya paralel,
hiç kımıldamayan temiz bir şerit üretir. Kırılma tepede olur, arkasındaki çukur berrak
sudur. `crest` çarpanı bandı dalganın üstüne oturtuyor — telafi değil, ölçütün eksik
kalan yarısı.

## "Altı zaten suyun altında" bir gerekçe değildi

`seaWet` bandının tabanının yorumu şunu diyordu: *"Altı suyun altında ve deniz onu zaten
çiziyor."* Deniz onu çizmiyor — **gösteriyor**. `refracted` sahne rengini örnekliyor ve
sönüm ilk metrelerde çok zayıf: 25 m açıkta ışın suda 2,8 m yol alıyor, geçirgenlik
0,43 / 0,80 / 0,87. Kuru kum albedosu (0,61 / 0,54 / 0,43) neredeyse olduğu gibi göze
geliyordu.

Bandın tabanı silinmedi, kapsamı düzeltildi: tabanın işi sudan YUKARIDAKİ zeminin ıslak
sayılmasını durdurmaktı (eklendiği belirti oydu) ve o işi yapmaya devam ediyor. Su
altındaki zemin ayrı bir terimle ıslak. Danteli ve ıslak parlamayı yalnız swash sürüyor:
su altında ne dantel var ne de yüzeydeki su filmi.


## Hs ve Tp neden formülden değil spektrumdan okunuyor

Fetch sınırlı JONSWAP bağıntıları — `Tp = 2pi/omega_p`, `Hs = 0.0016 sqrt(gF/U^2) U^2/g` —
**rüzgâr denizini** tarif eder. Çalışan spektrumda ikinci bir parça var: kendi tepe
periyodu (10 s), kendi yönü ve **rüzgârdan bağımsız sabit enerjisi** olan ölü dalga. Sakin
havada denizi ayakta tutan tek şey o; iki sayı da onu görmüyordu.

Belirti buradan çıktı: kıyıdaki koşu-yukarı fazının periyodu Tp'dir, ve 0,5 m/s'de Tp
2,63 saniyeydi. Kumsalda beyazlık iki saniyede bir gelip gidiyordu. Altındaki gerçek
dalga ise on saniyelik.

**Formül düzeltilmedi, kaynak değiştirildi.** `SeaSpectrumMoments` iki parçanın 1B
spektrumunu 0,05–8 rad/s bandında 0,005 adımla integre ediyor:

- `Hs = 4 sqrt(m0)`, m0 = iki parçanın toplam varyansı
- `Tp` = TOPLAM spektrumun tepesi, yani enerjiyi hangi parça taşıyorsa onun periyodu

Formüller compute shader'ınkilerin aynısı — biri değişirse öteki de değişmeli, ikisi aynı
spektrumu tarif ediyor.

**Maliyet ölçüldü:** integrasyon 0,187 ms (editörde, ilk hâli 0,295 ms'ti; `alpha`, derinlik
ölçeği ve `omega^5` döngüden çıkarıldı). Rüzgâr 0,1 m/s'den az değiştiyse yeniden
hesaplanmıyor — o kadar rüzgâr 8 m/s'de 1 cm Hs eder.

## Kırılma köpüğünün ekranı kaplaması iki kapıyla kapandı

Hs düzelince kırılma kuşağı sakin havada 3 m'den 45 m'ye çıktı — çünkü gerçek Hs 0,10
değil 0,74 m. Kuşağın GENİŞLİĞİ doğru, ama kuşağın tamamı beyaz değil: `crest` çarpanı
köpüğü tepelere oturtuyor.

Yüzey kotu dağılımı üzerinden ortalama alfa (sigma = Hs/4):

| mesafe | derinlik | U=0,5 | U=3 | U=8 | U=20 |
|---|---|---|---|---|---|
| 3–29 m | 0,36–1,68 m | 0,10 | 0,10 | 0,10 | 0,10 |
| 45 m | 2,35 m | 0,03 | 0,10 | 0,10 | 0,10 |
| 88 m | 4,13 m | 0 | 0 | 0,12 | 0,12 |
| 200 m | 5,34 m | 0 | 0 | 0,11 | 0,13 |

Düzeltmeden önce aynı kuşakta alfa 0,75'ti. Ortalama beyazlık 7,5 kat düştü ve kuşağın
sınırı artık havayla birlikte hareket ediyor.


## F1'den silinen teşhis anahtarları (2026-08-29)

Panelde on dokuz kar probu duruyordu: `_SnowDebugDent`, `_SnowDebugProbe`,
`_SnowDebugCover`, `_SnowDebugNormal` ve on beş `_SnowDbgNo*`. Hepsi kapanmış
belirtiler içindi — yerdeki lekeler, izin kenarındaki basamak, oyuncuyu takip eden kare
(`SYMPTOMS.md`, üçü de ölçümle kapandı).

Anahtar silinince shader dalı ölür: global bir daha yazılmaz, `if` hep yanlış kalır. Bu
yüzden panel ve shader tarafı **aynı adımda** gitti — 39 kullanım yeri, altı dosya. Kalan
yol her zaman normal yoldu; hiçbir davranış değişmedi.

Aynı temizlikte iki ölü şey daha çıktı:

- **`_TerrainShadowOff`** — arazinin gölge okumasını kapatan anahtar. Onu yazan panel
  satırı çoktan silinmişti, yani global hep 0'dı ve dal hiç çalışmıyordu. Ölçüm bittiği
  için anahtar da gitti.
- **İki "TEMPORARY" yorumu** — ışık-gölge sınırındaki çizgi probu ve dünya koordinatı
  cetveli. Kodları yıllar önce silinmiş, yorumları kalmıştı.


## Arazi kutusunun dışı neden sabit derinlik olamaz

`SeaSampleDepth` kutu dışında tek satırla `_SeaDeepWaterDepth` döndürüyordu ve yorumu
"arazinin ötesi açık deniz" diyordu. Doğru olan cümle bu değil: **derinlik sürekli bir
alandır**, ve o satır onu kare bir sınırda 25 metreden 200 metreye zıplatıyordu.

Derinlik üç şeyi birden sürüyor — soğurma (`SeaVolumeColor`), sığlaşma kazancı
(`SeaShoalingGain`) ve kırılma ölçütü. Üçü birden aynı çizgide zıplayınca sonuç renkte
görünen keskin bir kenar oluyor.

Çözüm ölçüden çıktı, tercihten değil:

- Kenardaki gerçek derinlik: dört kenarda da %100 su, ortalama **25,4 m**.
- Kenar öncesi son 500 metrede yatağın eğimi: **%0,61**. O eğimi sürdürmek 200 m'ye
  inmek için 28,6 km ister — mesh'in ulaştığı 4064 m'nin çok ötesi.
- Seçilen rampa **4000 m**, yani %4,4: gerçek bir kıta yamacı eğimi ve mesh'in görünür
  menzilinin tamamını kaplıyor.

`saturate(uv)` en yakın kenar tekselini okuduğu için değer sınırda **süreklidir**; rampa
onun üstüne biner. Dallanma da gitti.

## Gürültü hash'i dünya ölçeğinde ölçülerek seçildi

Denizin `SeaHash21`/`SeaHash22`'si koordinatı katlamadan büyük çarpanlarla hash'liyordu.
Kıyı kilometrelerce uzakta; `frac()`'in girdisi milyonlara çıkınca float'ın mantisi
bitiyor ve hash birkaç değere düşüyor. 64×64 hücrede ölçüldü: başlangıç 15000'de
**4096 hücrede 20 farklı değer**. Köpükteki dama deseni buydu.

Yeni hash uydurulmadı: `MountainSurface.hlsl`'deki `MountainHash` aynı sorunu aynı
ölçekte çözmüş — önce `fmod(abs(p), 512)`, sonra 0,1031 gibi KÜÇÜK çarpanlar. Aynı yapı
iki boyuta taşındı ve ölçüm tekrarlandı: her başlangıçta ~2400 farklı değer, dünya
konumundan bağımsız.

Periyot 512: köpüğün döşemesinde 640 metrelik bir tekrar demek, kabarcık deseni için
görüş menzilinin çok ötesi.


## Tek ölçekli bir kenar, periyodu olmasa da desen okunur

Kıyı köpüğünün kenarı iki gürültüden kuruluyordu: ~3 m ince doku ve ~16 m kaba kırılma.
Özilinti ölçüldü, gerçek bir periyot YOK (korelasyon boyunun ötesindeki en büyük yerel
tepe 0,11–0,23). Ama kullanıcı haklıydı ve neden haklı olduğunu ikinci ölçüm söyledi:
kenarın salınımı 32 metreden sonra büyümüyor.

Bu, "desen" için periyot gerekmediğini gösteriyor: **tek ölçek yeter.** Bütün dişler aynı
boyda olunca göz düzen görür. Gerçek bir su çizgisinde koy, dil ve parmak aynı anda
vardır — ölçek serbesttir.

Düzeltme "daha çok oktav" değil, **doğru yere oktav**: düz bir fBm denendi, enerjiyi
yayarak ince dokuyu öldürdü (1 m penceresinde 0,29 → 0,05 m). Mevcut ince oktavlar
korunup üstlerine iki kaba oktav eklendi. Ölçüt tekti — salınım kaba uca doğru büyümeye
devam etmeli ve ince uç düşmemeli.

Kaba oktavların çarpanları `_SeaFoamBreakupTiling`'e bağlı (0,0292 ve 0,0117), yani ayar
değişirse dört ölçek birlikte kayıyor; ikinci bir kaynak yok.


## Göz uyumu: tavan neden `tanh`, banda neden cd/m² denmedi (2026-08-29)

**Tavan doyum, kırpma değil.** Ölçüm `SYMPTOMS.md`'de: sert kırpma geceyi tek pozlamaya
düzleştiriyordu. `tanh` seçildi çünkü tavanın **altında birebir doğrusal** — öğlen 0,098 →
0,098, 08:00 0,3045 → 0,3043, altın saat 0,700 → 0,697 — yani gündüz ve altın saat
kımıldamıyor, yalnız gece geri geliyor: gece yarısı 2,50 → 2,47, 05:30 2,50 → 3,05.

**Tavan fizik, pay tasarım.** Rodopsin rejenerasyonu gerçek gözde 6-7 kademelik kazanç
verir; **doyan** şey bu. Oyunun bu kazancın ne kadarını harcadığı `adaptShare`, ve o bir
sıkıştırma kararı. Eskiden ikisi tek kırpılmış sayının içinde birbirine dolanmıştı; ayrı
ayrı düşünülemediği için kırpma yıllarca görülmedi.

**Çubuk görüşü bandı oyunun aralığına konuldu, cd/m²'ye değil — ve bu bilinçli.** Gerçek gün
öğleden aysız geceye ~18 kademe iner; bu oyunda 9,4 (ölçüldü: öğlen 0,28 kademe altta, en
karanlık saat 9,45). Bandı gerçek parlaklığa çakarsak mezopik bölge ~15 kademe altta kalır
ve terim **hiç çalışmaz**, çünkü oyunun gecesi kendi öğlenine göre gerçeğinden yaklaşık bin
kat parlak. Sıkıştırma zaten var; band da sıkıştırılmış aralığa oturuyor. Çıkan ağırlıklar
tutarlı: gece yarısı ay tepedeyken 0,66 — dolunaylı kar gerçekten mezopiktir — alacakaranlık
sonrası ay doğmadan 1,0.

**Purkinje'nin RENK yarısı yazılmadı.** Kayma bir *parlaklık ağırlığı* değişimidir: V(λ)
yerine V′(λ) (tepe 507 nm). sRGB birincillerinde oran V′/V ≈ (0,019 · 0,48 · 10,3); luminans
korunacak şekilde normalize edilince (0,017 · 0,443 · 9,41). Bunu `colorFilter` gibi bir
**çarpan** olarak uygulamak mavi kanalı 9,4 ile çarpar — gece neon mavisi olur. Doğru işlem
"V′ ağırlıklı griye doygunluk düşürmek"tir ve `ColorAdjustments.saturation` sabit fotopik
luma kullandığı için bunu ifade edemez; ayrı bir geçiş ister.

Yazılan yarı doğru olanı: **çubuklar renksizdir.** Renklilik kaybı uygulandı, renk kayması
uygulanmadı. Gece profillerinde zaten elle ayarlanmış bir mavi eğilim var (`colorFilter`
0,92/0,95/1,00, sıcaklık −14) ve onun üstüne ikinci bir kaynak konmadı.

**Doygunluk payı `adaptShare`'den geliyor, yeni bir sayı uydurulmadı.** Profilden **kalan**
renkliliğin (`100 + profile.saturation`) payı alınıyor: açık gecede −20 → −48, fırtınalı
gecede −36 → −58. Aynı sıkıştırma, iki eksen.


## İnceleme maddelerinden yapılmayanlar ve sebepleri (2026-08-29)

**A1 "adaptShare'i yöne göre böl" — yapılmadı, çünkü inceleme kendi içinde çelişiyor.**
§4 tablosu "gerçek ~0,5 ışığa, ~0,25 karanlığa" diyor, §A1 metni tam tersini: "ışığa 0,25,
karanlığa 0,45". Fizyoloji §A1'i doğruluyor (karanlığa açılma payı büyük). Ama asıl mesele
şu: iki paylı bir kurulum `adaptTarget > adapt` testine bağlı olduğu için **histerezis
bandı** yaratır, ve gece bandın genişliği ~1 EV çıkar — küçük bir aydınlanmada pozlama bir
kademe birden düşer. Ölçüm zaten gerçek sebebin pay değil **tavan** olduğunu gösterdi;
tavan doyuma çevrilince maddenin gerekçesi ortadan kalktı.

**A2 "bakış yönü karışımı" — yapılmadı.** İncelemenin kendi §6'sı ekran-ortalama uyumu
"güneşe bakınca fırlama" diye reddediyor; bakış yönü probu aynı sorunun yumuşatılmışı.
Ayrıca `LookController`'a kamera bağımlılığı sokar ve incelemenin kendisi 2-3 s ek yumuşatma
şart koşuyor. Getiri, taşıdığı salınım riskini karşılamıyor.

**A3 "alt sınırı 0,0005 → 0,00006" — yapılmadı, ama "konusuz" demek YANLIŞTI.**
Edit mod ölçümüne bakıp "seviye 0,0014'ün altına hiç inmiyor, sınır devreye girmiyor"
demiştim. Play bunu çürüttü: gün batımında seviye **0,0002**, sınırın altında. Sınır
çalışıyor.

Ama indirmenin çaresi yine de bu değil. Sınıra gece değil, **gökyüzü probunun ufuktaki
çöküşü** dayanıyor (`SYMPTOMS.md`). Sınırı indirmek bozuk girdinin daha derinini görmek
demek: gün batımı bugün 3,33 EV'ye açılıyor, sınır düşürülürse daha da açılır. Önce girdi
düzelecek.

**B2 "karanlıkta glare artsın" — yapılmadı.** Yönü doğru (göz bebeği 2→8 mm, alan 16 kat,
kaynak çevresindeki hale büyür). Ama `bloom.intensity` bir stil parametresi, veiling
luminance değil; 16 çarpanının orada fiziksel bir karşılığı yok ve incelemenin önerdiği
"ucuz sürüm" savunulabilir bir katsayı taşımıyor. Proje kuralı: karşılığı yazılamayan
katsayı bağlanmaz.

**B3 "Weber kontrastı" — zaten var, elle.** Gece profilleri kontrastı düşürüyor (açık gündüz
6 → açık gece 3, fırtınalı gece −3). Eksik olan mekanizma değil, **neye bağlı olduğu**:
saate bağlı, ışığa değil. Aynı kusur doygunlukta vardı ve bu turda kapatıldı; kontrast
elle ayarlanmış bir eğri olduğu için aynı işlem ona körlemesine uygulanmadı.

**C grubu** — incelemenin kendi tavsiyesi: yerel ton eşleme pahalı, bleaching konfor riski.


## Gökyüzünün güneşi neden sahnenin güneşinden ayrıldı (2026-08-29)

Tek `Light` iki role birden hizmet ediyordu ve rollerin istediği büyüklük farklı: sahne
atmosferden **geçmiş** ışını ister, gökyüzü LUT'u atmosfere **giren** ışını. Aynı değeri
ikisine de vermek sönümü üç kez uygulamak demekti ve ufukta gökyüzünü tamamen öldürüyordu.

**Bu proje bu hatayı bir kez daha yaşamış.** `LookController`'ın yorumu: "Once the double sun
attenuation was removed and twilight came down to its real level." O tur sahnenin pozlaması
için kapatılmış; gökyüzünün aynı ışıktan beslendiğini kimse kontrol etmemiş.

**`SunBlend` neden kaldı.** Sönüm değil, astronomik alacakaranlığı −18°'de bitiren kapı.
Kaldırılsaydı LUT gece boyunca gezegenin içinden geçen bir güneşle aydınlanırdı. Ufukta
değeri 0,98 — yani gün batımını hiç kısmıyor, ki mesele oydu.

**Neden `max`, neden anahtar değil.** İlk sürüm kancaya yalnız güneşi yazdı ve geceyi
öldürdü: kapı kapandıktan sonra override sıfır yazıyor, paket de ayı okuyamıyordu.
`if (kapı > 0) güneş else null` de olurdu ama kapının iki yakasında 0,0119 → ~0 → 0,00039
diye bir çukur bırakırdı. Büyüğünü almak dikişi tamamen kaldırıyor ve iki kaynaktan hiçbiri
öbürünün altında kapatılmıyor.

**Öğlenin %36 parlaması bir bedel değil, düzeltmenin kendisi.** Kaybolan şey çift sönümdü.
Sabit `ReferenceSkyLuminance = 0,148` bunu doğruluyor: ölçümle konmuş "öğlen gökyüzü" değeri,
hatayla 0,097 okunuyordu (%34 altında), düzeltmeyle 0,132 (%11 altında). Kalibrasyon
yapıldığında gökyüzü daha doğruymuş.

**Diskin sönümlü kalması bir istisna değil, aynı kuralın öbür yüzü.** LUT atmosferi hesaplar,
disk atmosferin arkasındadır. Birine giren, ötekine geçen ışın verilir.


## Açık hava görüşü 25 km → 60 km (2026-08-29)

Değerin **hiçbir gerekçesi yoktu** — dört belgede de kaydı yok. Ayarın kendi tooltip'i ise
gerçek aralığı (100-200 km, irtifada) ve düşük tutmanın belirtisini ("bulut denizini siler")
yazmış durumdaydı.

**Neden bugüne kadar görünmedi:** bulut shader'ındaki `edgeFog` ince bulutu hiç söndürmüyordu.
İki hata birbirini örtüyordu; kontur düzeltilince alttaki çıktı.

**60, tooltip'in 100-200'ü değil, çünkü ikisi farklı yükseklikten konuşuyor.** Tooltip'in
aralığı 2000 m için ve orada zaten sağlanıyor: sınır tabakası tersinme kapağıyla kesiliyor,
geriye serbest katman kalıyor ve 3000 m'de eşdeğer görüş 423 km. `clearVisibility` ise **yer
seviyesi** değeri. Kamera 283 m'de, kıyıda; orada 100 km istisnai bir gün olur, varsayılan
hava değil. WMO'nun "istisnai berraklık" eşiği 50 km, temiz kıyı/dağ havası açık günde 50-80 km.

Ölçülmüş sonuçlar `SYMPTOMS.md` → "Uzak bulutlar sise gömülüyor".


## Palet yeşil-griye çevrildi (2026-08-30)

Ton kararı `DESIGN.md`'de: koyu palet, ağır, depresif. Bu kaydın konusu **hangi yön** olduğu.

**Doygunluk tek başına kasvet üretmiyor — ölçüldü.** İlk süpürme yalnız doygunluğu gezdirdi
(−14 / −40 / −60 / −80) ve kullanıcının cevabı şu oldu: *"renklerle oynanmamış ki, sadece
siyah/beyaza doğru kaymış."* Doğru teşhis. Doygunluk düşürmek rengi **yok eder**, kasvet ise
bir **yön** ister: grinin bir tarafa yaslanması lazım.

**İkinci süpürme doygunluğu sabitledi (−40) ve yalnız rengin yönünü gezdirdi.** Üç yön
denendi, hepsi kaynaklı:

| yön | kaynak | sonuç |
|---|---|---|
| yeşil-gri | Death Stranding'in paleti — gri, yeşil çalan | **seçildi** |
| soğuk siyan | The Revenant'ın kışı, mat mavi-gri | elendi |
| hastalıklı sarı | çürüme, bleach-bypass bölgesi | elendi |

Yeşil-gri kazandı çünkü herkes maviye gider; mavi **soğuk** okunur, yeşil-gri **hasta**.
Kartpostal hissini kıran şey buydu — gökyüzü maviden kirli petrole düştü.

**Kontrasta dokunulmadı, bilinçli.** Referansın yaptığı da bu: doygunluk iner, kontrast
kalır. Kontrastı düşürmek "soluk" verir, "ağır" vermez.

**Altın saat ayrı tutuldu.** Doygunluğu 10 → 0 indi ama sıcaklığı 14 → 18 çıktı ve tek sıcak
filtreyi o taşıyor. Revenant'ın kuralı: soğuk bir dünyada **tek** sıcak kaynak, baştan sona
soğuk olandan daha ağır durur.

Ölçülen değerler `LookSettings.asset`'te; buradaki kayıt yönün **neden** o yön olduğu.


## Ayak izi: kalınlıkla ölçeklenmiş büyüklük İKİNCİ kez kalınlıkla çarpılmaz

**Kural.** Bir büyüklük zaten kar kalınlığından türüyorsa, üstüne mutlak kalınlık çarpanı
konmaz. Konursa etki kare olarak düşer ve ince karda ortadan kalkar.

**Neden.** Kar izinin üç ayrı yerinde aynı hata vardı ve üçü çarpılıyordu:

| yer | terim | 1 cm karda | 20 cm karda |
|---|---|---|---|
| `KRim` | `saturate(baseH / 0.25)` | 0,040 | 0,745 |
| `KDeform` | tavan yerel yoğunluklu `baseH` | sıkıştıkça düşer | bağlayıcı değil |
| `MountainSurface.hlsl` | `saturate(trailDepth * 20)` | 0,14 | 1,00 |

Oyma zaten 19 kat düşerken rim 360 kat düştü. Üçü de "ince karda iz zayıf olmalı" sezgisiyle
yazılmıştı — ama bu sezgi **zaten** oymanın içinde kodlanmıştı; terimler onu tekrarladı.

**Ayırt eden ölçüm.** Aynı dört metre, dört kar derinliğinde, 2 cm ızgarayla örneklendi.
Oymanın kar kalınlığıyla orantılı düşmesi doğru çıktı; rim'in ondan bağımsız çökmesi
yanlış çıktı. Sayı olmasa üçü de makul görünüyordu.

**Doğru soru "ne kadar derin" değil, "var mı".** Eğim zaten derinliğin türevidir; sığ çukur
kendiliğinden yumuşak eğim verir. Normale giren kapı bu yüzden **varlık** kapısı oldu
(2 mm eşiği: altı doku gürültüsü, üstü ayak izi), büyüklük kapısı değil.

**İnce karda iz derinlikle değil MALZEMEYLE okunur.** 1 cm karda bot zemini açar; karşıtlık
kar↔zemin albedosudur. Bu yüzden çözüm kabartmayı büyütmek değil, kar maskesine oyma
terimini bağlamak oldu — sistemin dört sorusundan biri eksikti, beşincisi eklendi.



## Kot fırtınayı kapı gibi tutamaz — payını verir (2026-09-01)

`IntensityAt` önce kot profilini fırtınayla **çarpıyordu**. Profilin deniz seviyesindeki
değeri `openingIntensity: 0` — yani çarpım kıyıda her zaman sıfır. Ölçüldü: dünya 0,40
iken kıyı şiddeti 0,00, deniz rüzgârı `windAtBase` tabanında 3,0 m/s'ye çakılı, Hs 1,17 m
ve hiç oynamıyor. "Büyük dalgalar yok" belirtisinin kaynağı buydu; dalga kodunda değil.

Profil artık fırtınanın **payı**: kıyıda `worldStormAtSeaLevel` (0,55), serbest havada
tamamı. Süpürme ölçümü:

| dünya | kıyı şiddeti | deniz rüzgârı | Hs |
|---|---|---|---|
| 0,05 | 0,03 | 5,2 m/s (taban) | 1,66 m |
| 0,70 | 0,41 | 7,5 m/s | 2,19 m |
| 1,00 | 0,59 | 9,5 m/s | 2,64 m |

Oyuncu zirveye taşındığında deniz kıpırdamadı (9,5 m/s, Hs 2,64 m) — `DESIGN.md`'nin
"fırtına oyuncu orada olduğu için çıkmaz" kuralı artık ölçülebilir durumda.

**Elle ayar kaldıracı da dünyaya taşındı.** F1 paneli yerel yağışı kilitlerken deniz
dünyanın saatini izlemeye devam ediyordu: gökyüzü sabit, dalga sönüyor. Kilit tek bir
sayıyı — `WorldStormOverride` — tutuyor, gerisi ondan türüyor.


## Dalganın şekli bir eğri seçimi değil, Ursell sayısının sonucudur (2026-09-01)

Kullanıcı üç şeyi ayrı ayrı beğenmedi: açık denizdeki dalgaların şekli, kıyıya vuran
dalgalar, ve "bazen sörf yapılacak dalga olmalı". Üçünün de tek bir kökü var ve
literatürde kapalı formda çözülmüş.

### Ölçüm: kıyımız hiçbir zaman sörf dalgası veremez

Kırılma tipini **Iribarren sayısı** belirler: `ξ₀ = tanβ / √(H₀/L₀)`, `L₀ = gT²/2π`.
Sınırlar: `ξ₀ < 0,5` dökülen (spilling), `0,5 < ξ₀ < 3,3` **dalan (plunging — sörf dalgası)**,
`ξ₀ > 3,3` tırmanan (surging).

Bizim sayılarımız: arazi eğimi z=2000'de 1:26 (0,038), ayar `shoreSlope` 0,058,
`swellPeriod` **sabit 10 s**, Hs ~1,7 m.

| eğim | T | L₀ | ξ₀ | tip |
|---|---|---|---|---|
| 0,038 (gerçek arazi) | 10 s | 156 m | 0,37 | dökülen |
| 0,058 (ayar) | 10 s | 156 m | 0,56 | sınırda |
| 0,038 | 14 s | 306 m | 0,52 | dalan |
| 0,058 | 16 s | 400 m | 0,90 | **dalan** |

Yani mevcut kurulumda dalan dalga **hiç** çıkmıyor, çünkü peryot sabit. "Bazen sörf
dalgası" isteğinin karşılığı bir efekt değil: **uzun peryotlu bir ölü dalga (groundswell)
olayının bazen gelmesi.** 10 s rüzgâr denizi dökülür, 14–16 s ölü dalga aynı kıyıda dalar.
[KAYNAK: Battjes 1974 surf similarity; Coastal Wiki, Surf similarity parameter]

### Şekil: Ursell → çarpıklık ve asimetri → analitik profil

Dalga derinliğe girdikçe önce **çarpık** (skewed: sivri tepe, geniş düz çukur), sonra
**asimetrik** (asymmetric: dik ön yüz, yatık arka — testere dişi) olur. İkisi ayrı
şeylerdir [KAYNAK: Elgar & Guza 1985].

Ruessink ve ark. 2012, 30.000'den fazla saha ölçümüne dayanarak bu ikisini tek bir
boyutsuz sayıya, **Ursell sayısına** bağladı:

```
Ur = (3/8) · H·k / (k h)³
B  = p₁ + (p₂ − p₁) / (1 + exp((p₃ − log Ur)/p₄))     p₁=0  p₂=0,857  p₃=−0,471  p₄=0,297
φ  = −(π/2) · tanh(p₅ / Ur^p₆)                         p₅=0,815  p₆=0,672
r  = tanh(0,931·B)
```

`(r, φ)` ikilisi Abreu ve ark. 2010'un analitik dalga formunu sürer:

```
u(t) = U f · [ sin(ωt) + r sinφ/(1+f) ] / [ 1 − r cos(ωt+φ) ],   f = √(1−r²)
```

Uçları: `r = 0` → saf sinüs (derin su). `φ = −π/2` → saf hız çarpıklığı (sivri tepe,
düz çukur). `φ = 0` → saf ivme çarpıklığı (**testere dişi, dik ön yüz** — kırılmanın
hemen öncesi). Aradaki her şey doğada birlikte bulunur.
[KAYNAK: Abreu, Silva, Sancho & Temperville 2010, Coastal Engineering; Ruessink ve ark. 2012]

Bugün shader'da bunun yerine `c * (1 + 0,45·c)` var — elle seçilmiş bir eğri, hiçbir şeyden
türemiyor ve derinlikle değişmiyor. Bir dalga her derinlikte aynı şekle sahip.

### Kırılma tek noktadan başlar, sonra yana soyulur

Thürey ve ark. 2007 kırılmayı iki koşulla yakalıyor: `|∇H| > t_H` **ve** `∇H·u < 0` —
yani yalnız **öne bakan yüz**, arka yüz değil. Eşik `t_H = p_H·g·Δt/Δx`, `p_H = 1/4`.
Ve kritik gözlem: dalga tüm boyunca aynı anda kırılmıyor; bir noktada başlıyor, sonra
cephe boyunca **yayılıyor**.

Sörf literatürü aynı şeye **soyulma açısı** diyor: kırılmış beyaz suyun izi ile hâlâ
kırılmamış cephe arasındaki açı. 0° = dalga bir anda tüm boyunca kırılır (sörf edilemez).
Sörf edilebilir aralık **30°–70°**; 20–45° hızlı ve ileri seviye, 46–55° orta, 56–70°
başlangıç. [KAYNAK: Scarfe 2002; Mead & Black]

Bizim kıyı trenimizin fazı yalnız derinliğe bağlı: `phase = 2ω√h/(β√g) − ωt`. Aynı
derinlikteki her nokta aynı anda kırılıyor, yani **soyulma açısı sıfır**. Sahteliğin
büyük kısmı bu.

### Setler

Dalgalar tek tek değil **set** hâlinde gelir: tipik olarak 3–10 (bazen 12–16) dalgalık
gruplar, aralarında durgunluk. Sebep farklı dalga boylarındaki trenlerin girişimi;
spektrum dar oldukça gruplaşma belirginleşir, yani **ölü dalgada set yapısı en güçlüdür.**
Setin en büyüğü ortalarda, 5.–8. dalga civarında. "Yedinci dalga" halk inanışı bunun
kabaca doğru olan yanı. [KAYNAK: Longuet-Higgins 1984 zarf teorisi; Masson & Chandler 1993]

### Şoaling katsayısı: Green yasası yerine tam ifade

Bugün `SeaShoalingGain` Green yasasını (`h^(−1/4)`) kullanıyor; bu yalnız sığ su limitinde
doğru. Doğrusal dalga teorisinin her derinlikte geçerli ifadesi:

```
K_sh = cosh(kh) / √(kh + sinh(kh)·cosh(kh))
```

Sığ limitte Green yasasına iner, derin suda 1'e gider — yani tavan (`maxShoalingGain 2,2`)
elle konmuş bir sınır olmaktan çıkar. [KAYNAK: Abreu ve ark. 2012, denklem 2]

Kırılma sınırı `H/h = 0,78` (McCowan 1894); eğime bağlı hâli zaten `SeaBreakerIndex`'te.

### Öne atma: yörünge genliği, uydurma kesir değil

Kıyı treninin tepesi öne eğiliyor. Eskiden eğim `min(|h|, 1.5) * 0.35` idi — 1,5 m'lik
sınırın da 0,35'in de arkasında bir ölçüm yoktu.

Doğrusal dalga teorisinde yüzey parçacığının yatay salınım genliği `A / tanh(kh)`. Derin
suda `tanh → 1` ve yörünge dairedir; dip yükseldikçe `tanh(kh)` çöküyor, yörünge yatay bir
süpürmeye yassılıyor. Sığlaşan tepenin öne eğilmesi tam olarak budur.

**Katlanma korkusu yanlış hesaptı.** Uygulamadan önce kağıtta bakıldı:

| derinlik | periyot | k (rad/m) | A/tanh(kh) | kA |
|---|---|---|---|---|
| 0,3 m | 10 s | 0,366 | 0,71 m | 0,26 |
| 3 m | 16 s | 0,072 | 3,6 m | 0,26 |

Sivri uç `kA = 1`'de, ilmek `kA > 1`'de. İkisi de 0,26 — yüzey kendi içinden geçmiyor.
Tavan (`SEA_SHORE_THROW_AK = 1`) kağıdın kapsamadığı durumlar için ve tam sivri uçta.

**Oyunun kendi sayılarıyla ölçüldü** (dünya fırtınası 0,85, deniz seviyesi rüzgârı
8,4 m/s, Hs 2,37 m, Tp 6,7 s): genliğin tepe yaptığı 2,5 m derinlikte `k = 0,19 rad/m`,
atma 0,60 m, `kA = 0,11`. Eski kesir aynı noktada 0,135 m veriyordu — yani yörünge 4,4 kat
büyük, ama hâlâ sivri ucun sekizde biri.

**Kum berraklığına dokunmuyor.** Aynı pin altında (açık pencere, fırtına 0,57, sabah)
atma açık/kapalı:

| kare | luma açık | luma kapalı | kum detayı açık | kum detayı kapalı |
|---|---|---|---|---|
| kum1 (1 m) | 34,14 | 33,93 | 0,547 | 0,551 |
| kum3 (2 m) | 24,31 | 24,53 | 0,406 | 0,403 |

Fark gürültünün içinde. Kullanıcının koyduğu sınır — sığ suda kum net görünecek — korundu.

### Kıyı treni açık denizin dörtte biri kadar

Gözle doğrulanamadı, çünkü görülecek bir şey yok. Oyunun kendi sayılarıyla:

| dünya fırtınası | deniz seviyesi rüzgârı | Hs | Tp | surf bandı | bant genişliği |
|---|---|---|---|---|---|
| 0,00 | 5,2 m/s | 1,64 m | 5,7 s | 0 – 6,3 m | 108 m |
| 0,60 | 6,8 m/s | 2,00 m | 6,3 s | 0 – 7,6 m | 131 m |
| 0,85 | 8,4 m/s | 2,37 m | 6,7 s | 0 – 9,0 m | 156 m |
| 1,00 | 9,4 m/s | 2,60 m | 7,0 s | 0 – 9,9 m | 171 m |

Kıyı treninin genliği `gamma*h/2 * SHARE * grip`, ve `grip` ile `take` aynı `1 − h/onset`
sönümünü iki kez uyguluyor: `amp(h) = 0,2535 * h * (1 − h/10)³`. Tepesi 2,5 m derinlikte
ve **0,27 m** — tepeden çukura 0,53 m. Açık denizde Hs 2,37 m iken kıyıya varan dalga
yarım metre. Kırılan dalganın kendisi görünmediği için "kırılma dudağı" da görünmüyor:
yörünge doğru, ölçek yanlış.

Kaynak sabitler: `SEA_SHORE_WAVE_SHARE = 0.65`, `SEA_SHORE_WAVE_BREAK_MULT = 1.5`.
Bir sonraki adım bu ikisidir, atma değil.

### Uzak denizin titremesi lob genişliğinden gelmiyor

Belirti: "denizde belli bir mesafeden sonra titreme var." Ufuk bandı %21 daraltılınca
titreme %21 azaldı; kalanın kaynağı gök yansıması sanılıyordu.

**Önce ölçüm aracı.** Sabit kamerayla 12 kare alınıyor. Kare farkının iki kaynağı var:
dalganın gerçekten hareket etmesi ve piksel altı dalgaların örnekleme noktasından geçip
yansımayı açıp kapatması. Ayıran şey ölçek — gerçek hareket 16×16 kutu ortalamasından
sağ çıkar (bir tepe yüzlerce piksel), aliasing çıkmaz:

    titreşim = (piksel zaman sapması) − (blok zaman sapması)

Aracın kendisi gradyanıyla doğrulandı: yakın bantta %2,15, orta bantta %8,88, ufka yakın
bantta %11,00. Belirti tam olarak bu — uzaklaştıkça artıyor.

**Fiziksel terim eklendi, görüntü değişmedi.** Çözülemeyen eğim varyansı GGX lobuna
verildi (`a² += σ²`, Bruneton ve ark. 2010). Spektrumdan gelen varyans, U10 = 5,2 m/s'de
kademe başına 0,00088 / 0,00475 / 0,00793, toplam 0,0136.

| bant | önce | sonra | 50 kat |
|---|---|---|---|
| ufka yakın | %11,00 | %11,07 | %3,34 |
| orta | %8,88 | %9,12 | %3,53 |
| yakın | %2,15 | %2,60 | %0,50 |

Terimi 50 katına çıkarmak titremeyi üçte birine indiriyor — yani **shader canlı ve
mekanizma doğru**, ama fiziksel büyüklük yetmiyor. Hesap da bunu söylüyor: taban
pürüzlülük 0,051, tüm kademeler kaybolduğunda `sqrt(0,051⁴ + 0,0136)` = 0,117 → algısal
pürüzlülük 0,342. Eski elle ayarlanmış rampanın vardığı yer 0,35 idi. **Ayar doğruymuş.**

Terim yine de kalıyor: üç ayarlanmış sayı gitti ve pürüzlülük artık spektruma bağlı.
Kapiler bant dördüncü kademe olarak geldiğinde kendiliğinden takip edecek.

**Kalan titremenin kaynağı başka.** 50 katta düzelmesi lobun ilgili olduğunu söylüyor ama
fiziksel varyansın yetmemesi şunu gösteriyor: kalan kademeler de **nokta örnekleniyor**,
yani korunan kademelerin kendi mip filtresinin sildiği varyans da geri verilmiyor. Doğru
adım LEADR/mip yolu — her kademe için mip seviyesinin altında kalan varyans. Ölçülmeden
yazılmayacak.

**Kum berraklığı korundu:** detay 0,547 → 0,559 (1 m) ve 0,406 → 0,409 (2 m). Terim
yakında sıfır olduğu için sığ su tanım gereği etkilenmiyor.

### Kıyı treninin ölçeği: pay iki kez, sönüm iki kez uygulanıyordu

Belirti kullanıcıdan: "ayakta normal bir şekilde denize bakıyorum, deniz çok ince
gözüküyor, sanki boyum 1 metre gibi."

**Göz yüksekliği doğruydu.** Ölçüldü: kamera zeminden 1,70 m, dikey FOV 60°, en-boy 2,16.
Düz denizde ufuk tam göz hizasındadır; berm'de duran oyuncunun gözü sudan 2,7 m yukarıda,
ekranın alt kenarı 4,3 m öndeki su, 25 m ötesine ~90 piksel düşüyor. Perspektif doğru.

**Eksik olan dalga yüksekliğiydi.** Deniz ölçeği ufku kesen tepelerden okunur. Hiçbir tepe
göz hizasına ulaşmayınca deniz düz bir levha gibi görünüyor.

**Sebep: iki ayrı çarpan iki kez uygulanıyordu.**

- `SEA_SHORE_WAVE_SHARE` hem `amp`'ta hem `SeaDeform`'daki çapraz geçiş ağırlığında vardı
  → yüzeye 0,65² = 0,42'si ulaşıyordu.
- Aynı derinlik sönümü de iki kez: `amp` içinde kareli `grip`, `SeaDeform`'da doğrusal
  `take`.

Net genlik `0,1648 · h · (1 − h/D)³` idi. Düzeltilmişi `0,1648 · h · (1 − h/D)`:

| dünya fırtınası | D (m) | eski tepe | eski t-ç | yeni tepe | yeni t-ç |
|---|---|---|---|---|---|
| 0,57 | 7,02 | h = 1,76 m | 0,24 m | h = 3,51 m | **0,58 m** |
| 0,85 | 10,04 | h = 2,51 m | 0,35 m | h = 5,02 m | **0,83 m** |

2,4 kat, ve tepe noktası kıyıdan uzaklaştı — dalga kırılmadan hemen önce en büyüktür,
doğrusu budur.

**Ama tavanı kaldırmak yanlıştı.** İlk denemede çapraz geçiş 1'e kadar açıldı: açık deniz
alanı sığ suda tamamen silindi, yüzeyin kısa dalga detayı gitti. Ölçüldü, 2 m derinlikte
luma 24,63 → 11,52, detay 0,406 → 0,388. İzolasyonla doğrulandı (`git stash`, aynı pin,
aynı kare): eski kod 24,63, yeni kod 11,52.

Doğrusu iki sabit: yükseklik payı 0,65 (Thornton & Guza'nın doygun sörf kuşağı,
`H/h = 0,51`) ve geçiş tavanı 0,65 (kırılmaya uğramayan kısa çırpıntının payı). Sonuç:

| kare | önce | sonra |
|---|---|---|
| kum1 (1 m) | luma 33,84 / detay 0,552 | luma 33,88 / detay 0,550 |
| kum3 (2 m) | luma 24,63 / detay 0,406 | luma 22,04 / detay 0,401 |
| kafes yoğunlaşması | %24,85 | %24,96 |

Kum berraklığı korundu; 2 m'de %10 kararma, 2,4 kat büyüyen dalganın yüzeyi
kırıştırmasının doğal sonucu.

**`SEA_SHORE_WAVE_BREAK_MULT` 1,5'te bırakıldı.** Sörf kuşağı kırılma derinliğinde başlar;
1,5 katı, kırılmadan önceki sığlaşma bandını da alan yumuşak bir devir teslim. Değiştirmek
kuşağın genişliğini oynatır, yüksekliğini değil — sorun yükseklikteydi.

### Denizin rüzgârı: karanın korunağı denize uygulanıyordu

Deniz seviyesi şiddeti `IntensityAt(groundAltitude)`'dan geliyordu ve o fonksiyon alçakta
fırtınayı `worldStormAtSeaLevel = 0,55` ile çarpıyor. Bu çarpanın gerekçesi kendi
yorumunda yazılı: kıyının kara kütlesi tarafından korunması. Ama denizin rüzgârı açık
sudan geliyor; orada koruyacak kara yok.

**Aynı karede iki formül, 400 kare boyunca** (dünya fırtınası 0,95):

| | min | ortalama | max |
|---|---|---|---|
| eski (`IntensityAt`) | 0,541 | 0,545 | 0,549 |
| yeni (`WorldStorm × Variation`) | 0,984 | 0,992 | 0,999 |

Hız 9,0 → 13,9 m/s. Salınımın kendi aralığı 1,036 – 1,051, yani gürültü değil çarpan
belirleyici.

**Tam tarama, her aşamada bekleyerek:**

| dünya fırtınası | rüzgâr | Hs | Tp |
|---|---|---|---|
| 0,10 | 5,20 m/s | 1,63 m | 5,7 s |
| 0,35 | 7,03 m/s | 2,05 m | 6,3 s |
| 0,57 | 9,60 m/s | 2,65 m | 7,0 s |
| 0,80 | 12,30 m/s | 3,26 m | 7,7 s |
| 0,95 | 14,00 m/s | 3,64 m | 8,0 s |

Öncesi 5,2 – 9,4 m/s ve Hs 1,59 – 2,60 m idi. Deniz artık Beaufort 3'ten 7'ye kadar
gidiyor.

**İlk ölçüm yalan söyledi.** Aşamalar arası bekleme yoktu: `WorldStormOverride` aynı
karede yazılıp okunuyordu, yani bir önceki aşamanın rüzgârı okunuyordu. Sayılar
düşüşü gösterdi (5,2 – 6,0 m/s), yani değişikliğin tersini. Aynı karede iki formülü
birden okuyan ikinci araç bunu ayırdı.

**Kum berraklığı korundu:** detay 0,550 → 0,575 (1 m) ve 0,401 → 0,396 (2 m). Kafes
yoğunlaşması %24,96 → %10,25, yani yüzey daha az düzenli.

### Planın üç maddesi ölçüldü, ikisi düştü

`Review/ultra_realistic_living_sea_master_plan.md` üç eksik sayıyordu. Ölçüm:

**Okyanus ataleti — gereksiz.** Süre-sınırlı büyüme yasasıyla (CERC) simüle edildi: 6
gerçek saatlik oyun, saniyede bir adım. Ataletli ve bugünkü Hs **birebir aynı** çıktı
(1,73 – 3,15 m, salınım 1,42 m). Sebep: deniz bir kez gelişince fiziksel olarak sönmüyor —
rüzgâr dinince ölü dalgaya dönüşüp binlerce km yol alıyor, 6 saatlik döngüde kaybı yok
denecek kadar az. Büyüme yasası var, sönme yasası yok.

Ayrıca oran ters: dalga gelişimi 9,5 – 15 oyun saati, fırtına döngüsü 6 saat. Deniz havayı
yakalayamaz; fiziksel atalet çeşitliliği artırmaz, ortalamaya oturtur.

Ve aranan şey zaten var: `SwellEvent01`, 9 oyun saatlik saatiyle ve yerel rüzgârdan
bağımsız. Rüzgâr sabit tutulup yalnız o gezdirildiğinde Hs 1,57 → 3,79 m (U10 5,2) ve
Tp 5,7 → 15,9 s. Rüzgârın oynattığından fazla. Üstüne atalet koymak ikinci bir yavaş
kaynak olurdu.

**Set modülasyonu — gereksiz.** Grup uzunluğu tayfın darlığından: enerji taşıyan bantta
(3ωp'ye kadar, standart) `nu = 0,322`, yani **grup başına 3,1 dalga**. Literatür
`nu 0,20 – 0,35` ve 3 – 5 dalga. Zaten doğru.

İlk hesap 1,5 – 2,7 dalga demişti ve yanlıştı: kuyruğun tamamına (9× tepe frekansı) kadar
integral alıyordu. `nu` kesim noktasına aşırı duyarlı — 1,5 rad/s'de 0,199, 8 rad/s'de
0,384. Oşinografide enerji bandında ölçülür.

**Stokes asimetrisi — iddia yanlış, ama pay var.** Plan "choppiness artırılınca mesh iç
içe geçiyor" diyor. Ölçüldü, tepe dalga boyunda `lambda·k·a`:

| fırtına | U10 | choppiness | k·a | lambda·k·a |
|---|---|---|---|---|
| 0,10 | 5,2 m/s | 1,19 | 0,099 | 0,118 |
| 0,57 | 9,6 m/s | 1,73 | 0,108 | 0,187 |
| 0,95 | 14,0 m/s | 2,27 | 0,116 | **0,263** |

Sivri uç 1,0'da; yüzey sınırın %26'sında, dört kat pay var. Ama diklik `k·a ≈ 0,11` gerçek
bir rüzgâr denizinin dikliği (0,05 – 0,15) ve o diklikte ikinci mertebe Stokes düzeltmesi
zaten küçük (`k·a/2`, yani genliğin %6'sı). Sayı "artır" demiyor, "artırabilirsin" diyor.

Fırtınada choppiness 2,4 / 3,6 / 4,8 kareleri kullanıcıya gösterildi: fark görülmedi,
**2,4 kaldı**. Mesh hiçbirinde bozulmadı, yani payın gerçek olduğu da doğrulandı.

### Yağmur halkası: hız sudan, sayı yağıştan

Denize (ve zemine) düşen damlanın bıraktığı halka hiçbir yerde yoktu — ölçüldü, `ripple`
diye bir şey ne deniz ne arazi shader'ında geçiyor. Yağmurun denize tek etkisi pürüzlülüğü
0,22'ye çekmek ve köpüğe 0,06 eklemekti.

**İki sayı da uydurulmadı.**

*Hız.* Kılcal-yerçekimi dalgasının minimum faz hızı `c = sqrt(2·sqrt(σg/ρ))`. σ = 0,0728
N/m, ρ = 1000 → **0,231 m/s**, dalga boyu 1,73 cm [KAYNAK: Lamb, Hydrodynamics §267].
Bir çarpmanın bıraktığı halka bu hızla gider ve her damla için aynıdır.

*Sayı.* Projenin kendi çapası: şiddet 1,0 = 50 mm/h. Marshall-Palmer medyan hacim çapı
`D₀ = 0,9·R^0,21` mm → 50 mm/h'de 2,0 mm, hacim 4,2 mm³. Debi (1,39×10⁻⁵ m³/m²s) bölü
hacim = **3300 damla/m²/s**.

**Sonuç tasarımı belirledi.** 3300 damla/m²/s, 1 s ömür ve 23 cm yarıçapla her metrekarede
her an yüzden fazla halka üst üste biner. Yani ayrı ayrı çember çizmek yanlış olurdu;
gerçek görüntü kaynayan bir benek dokusu. Üç katman (hücre 0,11 / 0,19 / 0,37 m) o dokuyu
veriyor; tek katman kafes olarak okunuyor.

**Ölçüldü.** Aynı donmuş kare, yağmur kapalı ve açık, fark:

| şiddet | uzak su | yakın su | uzak suda %1'den fazla değişen |
|---|---|---|---|
| 0,3 | 1,59 luma | 0,64 | %47,7 |
| 1,0 | 2,39 luma | 1,09 | %72,2 |

Şiddetle doğru ölçekleniyor. **Kum berraklığı etkilenmedi** (yağmursuz karede detay
0,550 → 0,551 ve 0,401 → 0,394), çünkü halka yağış sıfırken hiç hesaplanmıyor.

Genlik (`SEA_RAIN_RING_SLOPE` 0,12) tek göz kararı sayı: 1 mm'lik bir tepe 1,7 cm dalga
boyunda 0,37 eğim demek, üç katman bunu paylaşıyor. Gözle fazla ya da az bulunursa
değişecek tek sayı budur.
