# Belirti → Sebep

Ölçülerek bulunmuş belirtiler. Amaç tek: **aynı belirti tekrar görüldüğünde aramaya
baştan başlamamak.**

Her kayıt üç şey taşır: belirtinin kullanıcının ağzından hâli, ilk şüphelinin ne olduğu
(ve neden yanlış olduğu), gerçek sebep. Kayıt ancak **ölçümle** kapanmış bir turdan
doğar — tahminle çözülen bir şey buraya yazılmaz.

Bu dosyanın kendi dersi: **belirtinin göründüğü yer, belirtinin doğduğu yer değildir.**
İlk şüphelisi kayda geçmiş on iki belirtinin **on ikisinde de** ilk şüpheli yanlış çıktı —
her seferinde belirtinin en çok göze çarptığı katman seçilmişti. Bulut kontüründe üç ayrı
şüpheli (ışın yürüyüşü, zamansal birikim, mesafe sınırı) sırayla elendi; sebep dördüncüdeydi.

---

## Deniz açılınca bulut örtüsü kesik bir levhaya dönüyor — ÇÖZÜLDÜ (2026-08-31)

**Belirti:** yukarıdan aşağı bakarken bulut katmanı dik kenarlı bir dörtgende bitiyor.
Kapsama %90 olsa bile. **Aşağıdan yukarı bakarken sorun yok.**

**İlk şüpheli yanlıştı:** "bulut ışın yürüyüşü denizi engel sayıyor". Buna dayanarak
ulaşım 33 km'ye çekildi ve zirveden bile kesiyordu, sonra 4 km'ye geri alındı — belirti
mesafeyle ilgili değildi, ölçüm de o yönde yapılmamıştı.

**Ayırt eden ölçüm:** aynı kadraj, aynı kapsama, tek değişken **denizin renderer'ı**.
Deniz açıkken kesik, deniz gizliyken bulut tam. Yani ışın kesilmiyor, **deniz bulutun
üstüne çiziliyor**.

**Gerçek sebep:** bulut birleştirmesi `BeforeRenderingTransparents`'ta koşuyor, deniz ise
saydam kuyrukta (`Transparent-1`) — yani bulutlardan **sonra**. Deniz opak (`Blend Off`,
`ZWrite On`), bulut sahne derinliğine bir şey yazmıyor, dolayısıyla deniz test edecek bir şey
bulamadan birleştirilmiş bulutun üstüne biniyor.

**Çözüm:** bulut geçişi `AfterRenderingTransparents`'a alındı
(`VolumetricCloudsURP.cs`). Deniz artık bulutlardan önce çiziliyor, bulut onun üstüne
biniyor. Tek satır, tek yön.

Işın yürüyüşü hâlâ opak-sonrası derinliği okuyor, yani denizi bilmiyor. Bu bir bedel
değil: bulut katmanı 2–5 km'de duruyor, suya varan ışın ya katmana hiç girmiyor (kamera
katmanın altında, su ufkun altında) ya da önce katmanı geçiyor (kamera katmanın üstünde) —
iki durumda da bulutun suyun üstüne çizilmesi zaten doğru olan.

**ÜÇ YANLIŞ ÇÖZÜM DENENDİ VE ÖLÇÜMLE ELENDİ:**

1. **`PC_Renderer.asset` → `depthTexture: 1`.** Bulut derinliği sahne derinliğine yazılınca
   deniz ile bulut aynı değeri paylaşıp z-çakışmasına giriyor: 22 km'de gökyüzüne dağılmış
   yeşil benekler.
2. **Denizin shader'ında `_VolumetricCloudsDepthTexture`'a karşı `clip`.** Bulut derinliği
   çeyrek çözünürlükte, klip ikili: bulut siluetleri denizin üstünde basamak basamak çıktı.
3. **Denizi opak kuyruğa almak (`Geometry+450`).** Bulut tarafını gerçekten düzeltiyor ve
   **suyu bozuyor**: derinlik ve renk kopyaları opak kuyruktan SONRA alınıyor, opak kuyruğun
   içinden deniz henüz var olmayan bir dokuyu okuyor. Su çizgisinde kalınlık probuyla
   ölçüldü — saydam kuyrukta kıyıya yakın yeşil, açıkta mavi (gerçek derinlik geçişi); opak
   kuyrukta bant boyunca tek düz değer. Su sütunu, kırılma ve sığ su rengi hep o okumaya
   bağlı. Ayrıca deniz derinlik ön-geçişine de giriyordu: üçgen 802k → 1139k, 17 km'de
   8,2 → 9,6 ms.

**Dersi:** belirtinin göründüğü katmanı düzeltmek, o katmanın **başka** bir şeye bağlı
olduğunu görmeden yapılırsa bir hatayı ötekiyle değişiyor. Denizin sırası bulut için
yanlıştı ama su için doğruydu; taşınması gereken buluttu.

**Not:** `depthTexture: 1` bir kez de kontur için denenmiş ve "kontur düzeldi" sanılmıştı;
o doğrulama %90 kapsamada yapıldığı için yanlıştı. Konturun sebebi ayrı (NaN, aşağıda).

## Denizin üstünde, kamerayla gelen kare bir çerçeve (2026-08-31)

**Kullanıcının ağzından:** "çok yüksekten aşağıya baktığımda böyle tuhaf bir görüntü
çıkıyor", "kare devam ediyor. beni takip ediyor ayrıca." Yüksekten (14–16 km) denize
bakınca suyun üstünde ince, dörtgen bir çizgi; içi ve dışı su, aradaki tek fark ton.

**ÜÇ ŞÜPHELİ SIRAYLA YANLIŞ ÇIKTI.** Üçü de tahminle seçildi, hiçbiri ölçülmemişti:

1. **Halka sınırındaki T-birleşimi.** Gerçekten vardı — `x = 4064 m` sınırında köşeler
   32 m aralıkla duruyor, dış halkanın karesi 64 m. Ölçüldü, dikildi. **Kare gitmedi.**
2. **`max(fwidth(x), fwidth(z))` piksel ayak izi.** Eş-değer eğrisi kare, ekran ortasında
   da eksen değiştirme kırığı var. İkisi de gerçek kusurdu, düzeltildi. **Kare gitmedi.**
3. **Hacimsel bulut.** Bulut kapatıldı, kare durdu.

**Gerçek sebep:** `SeaSampleDepth`, batimetri kutusunun (30 km, araziyle çakışık) dışında
derinliği `lerp` ile derin suya çekiyordu. Derinlik sınırda **sürekli** ama **türevi
sıçrıyor**; `SeaSampleBottomSlope` türev alıyor ve kırılma/köpük ölçütü ona bağlı. Sonuç:
kutunun kenarını izleyen ince parlak çizgi.

**Ayırt eden ölçüm:** `_SeaBathySizeXZ` çalışma anında 30 km'den 120 km'ye çıkarıldı.
Çizgi kutuyla birlikte kadraj dışına çıktı. Kutu değişmeseydi çizgi yerinde kalırdı.

**Çözüm:** rampa `smoothstep` oldu; kutu kenarında türev sıfırdan başlıyor.

**Kullanıcının anahtarı, üç turu birden kesti.** F1 tarzı beş anahtar (deniz/bulut/sis/
gökyüzü/arazi) verildi, kullanıcı "1" dedi. Şüpheli listesi bir bakışta denize indi.
Anahtarlar ilk turda verilseydi üç yanlış tahmin de yapılmazdı.

**Karışmasın:** aynı manzarada, denizden bağımsız, **arazinin kendi 30 km kenarı** da
dörtgen bir sınır olarak görünüyor. Deniz kapatıldığında o duruyor. Ayrı konu.

---

## Kayıtlar

138 kayıt. Başlığa tıkla, ya da başlıkta ara — dosyanın tamamını okuma.

- [Oyuncunun çevresinde büyük bir kare, onunla birlikte hareket ediyor](#oyuncunun-cevresinde-buyuk-bir-kare-onunla-birlikte-hareket-ediyor)
- [Ayak altında kare bir sırt/çıkıntı, oyuncuyla birlikte geliyor](#ayak-altinda-kare-bir-sirtcikinti-oyuncuyla-birlikte-geliyor)
- [Kar yüzeyinde uzun, düz, dik bir sırt (çıkıntı)](#kar-yuzeyinde-uzun-duz-dik-bir-sirt-cikinti)
- [Kar yüzeyi bloklar/basamaklar hâlinde; mesh kapatılınca zemin pürüzsüz](#kar-yuzeyi-bloklarbasamaklar-hâlinde-mesh-kapatilinca-zemin-puruzsuz)
- [Kar açılınca çevrede basamaklı, dikey çizgili devasa bir duvar](#kar-acilinca-cevrede-basamakli-dikey-cizgili-devasa-bir-duvar)
- [Kar yağıyor ama dağ çıplak; ayak altında beyaz kare seninle geliyor](#kar-yagiyor-ama-dag-ciplak-ayak-altinda-beyaz-kare-seninle-geliyor)
- [Havada solucan/sigara dumanı gibi kar; bir yerde yağıyor, bir yerde boş gökyüzü](#havada-solucansigara-dumani-gibi-kar-bir-yerde-yagiyor-bir-yerde-bos-gokyuzu)
- [On ayrı sınama birden "yanlış sonuç" diyor, hepsi sıfır](#on-ayri-sinama-birden-yanlis-sonuc-diyor-hepsi-sifir)
- [Ekranın tamamı bembeyaz, ayak altında hareket eden siyahlık, 10 FPS](#ekranin-tamami-bembeyaz-ayak-altinda-hareket-eden-siyahlik-10-fps)
- [Ekranın yarısı simsiyah, kamerayla birlikte geliyor](#ekranin-yarisi-simsiyah-kamerayla-birlikte-geliyor)
- [Siluet kenarında tek piksellik kontur (gündüz koyu, denetimde beyaz)](#siluet-kenarinda-tek-piksellik-kontur-gunduz-koyu-denetimde-beyaz)
- [Gece etraf fazla aydınlık ("sisi kapatınca gerçekçi oluyor")](#gece-etraf-fazla-aydinlik-sisi-kapatinca-gercekci-oluyor)
- [Ufka yapışık ince çizgi, kamerayla geliyor](#ufka-yapisik-ince-cizgi-kamerayla-geliyor)
- [Düzenli kafes deseni (sis banklarında, akış alanında)](#duzenli-kafes-deseni-sis-banklarinda-akis-alaninda)
- [Rüzgâr arttıkça yukarı uzanan, titreyen dikey şeritler](#ruzgâr-arttikca-yukari-uzanan-titreyen-dikey-seritler)
- [BULUT KENARI KONTURU — İKİ AYRI HATA, BİRBİRİNE KARIŞTIRILMASIN](#bulut-kenari-konturu-iki-ayri-hata-birbirine-karistirilmasin)
- [(A) Bulutta VE dağda birebir aynı kontur — kenar halkası eleniyor](#a-bulutta-ve-dagda-birebir-ayni-kontur-kenar-halkasi-eleniyor)
- [(B) Bulut çevresinde SİYAH kontur — uydurulan mesafe sisi doyuruyor](#b-bulut-cevresinde-siyah-kontur-uydurulan-mesafe-sisi-doyuruyor)
- [Neden bu ikisi birbirine karıştı — ve tekrarlanmaması için](#neden-bu-ikisi-birbirine-karisti-ve-tekrarlanmamasi-icin)
- [Katılımcı ortam (sis, bulut) fazla koyu ya da fazla parlak](#katilimci-ortam-sis-bulut-fazla-koyu-ya-da-fazla-parlak)
- [Şafakta güneşten uzak bulutlar yeterince kararmıyor](#safakta-gunesten-uzak-bulutlar-yeterince-kararmiyor)
- ["Dikdörtgen dağ", "kenarlar zirveyle neredeyse aynı yükseklikte"](#dikdortgen-dag-kenarlar-zirveyle-neredeyse-ayni-yukseklikte)
- [Ekranın ortasında siyah sivri iğne](#ekranin-ortasinda-siyah-sivri-igne)
- [Zirve spawn'dan görünmüyor — ama ölçüm yalan söylüyordu](#zirve-spawndan-gorunmuyor-ama-olcum-yalan-soyluyordu)
- [Güneşle dönen, hiçbir gölge anahtarının etkilemediği koyu lekeler](#gunesle-donen-hicbir-golge-anahtarinin-etkilemedigi-koyu-lekeler)
- [Güneş tam karşıda ve yüksekken ayağının dibindeki yamaç gölgede](#gunes-tam-karsida-ve-yuksekken-ayaginin-dibindeki-yamac-golgede)
- [Batımda sahne kahverengi-siyah, kontrast patlıyor](#batimda-sahne-kahverengi-siyah-kontrast-patliyor)
- [Ayar dosyası düzenlemesi oyuna ulaşmıyor](#ayar-dosyasi-duzenlemesi-oyuna-ulasmiyor)
- [Yeni dağda eski dağdan birebir aynı yerler](#yeni-dagda-eski-dagdan-birebir-ayni-yerler)
- [Kod diskte doğru, ekranda eski — sessiz bayatlık ailesi](#kod-diskte-dogru-ekranda-eski-sessiz-bayatlik-ailesi)
- [Arazide düzenli testere — ÜÇ ayrı sebep, üçü de ölçümle bulundu](#arazide-duzenli-testere-uc-ayri-sebep-ucu-de-olcumle-bulundu)
- [Teşhis aracının kendisi](#teshis-aracinin-kendisi)
- ["Ekranın tamamı grenli, gökyüzü dahil" — desenin DC bileşeni](#ekranin-tamami-grenli-gokyuzu-dahil-desenin-dc-bileseni)
- ["Damlalar yere çarpıp sekiyor sanki dolu yağıyor gibi" / "yatayda hareket eden damlalar var"](#damlalar-yere-carpip-sekiyor-sanki-dolu-yagiyor-gibi-yatayda-hareket-eden-damlalar-var)
- ["kar yağmıyor" — VFX bağlıydı, parçacık doğuyordu, ekranda hiçbir şey yoktu](#kar-yagmiyor-vfx-bagliydi-parcacik-doguyordu-ekranda-hicbir-sey-yoktu)
- ["kar yukarı doğru yağıyor" / "yukarı aşağı sağa sola hareket ediyor"](#kar-yukari-dogru-yagiyor-yukari-asagi-saga-sola-hareket-ediyor)
- ["uzakta siyah tanecikler hareket ediyor"](#uzakta-siyah-tanecikler-hareket-ediyor)
- [`SetSlot` "yazdım" dedi, asset sıfır kaldı](#setslot-yazdim-dedi-asset-sifir-kaldi)
- ["uzaktaki kar kayboluyor" — iki ayrı sebep, ikisi de ölçüldü](#uzaktaki-kar-kayboluyor-iki-ayri-sebep-ikisi-de-olculdu)
- [Sahne kurulumu Play modunda çalıştırıldı ve sessizce yarım kaldı](#sahne-kurulumu-play-modunda-calistirildi-ve-sessizce-yarim-kaldi)
- ["kar tutmuyor" — yağış sıcaklıktan koparılmıştı, ERİME kopmamıştı](#kar-tutmuyor-yagis-sicakliktan-koparilmisti-erime-kopmamisti)
- ["yürürken iz kalmıyor" — sahnede tek bir deformer yoktu](#yururken-iz-kalmiyor-sahnede-tek-bir-deformer-yoktu)
- ["kar yağışı rüzgârdan etkilenmiyor"](#kar-yagisi-ruzgârdan-etkilenmiyor)
- ["beyaz örtü geziyor, kâğıt gibi incecik, derinliği yok"](#beyaz-ortu-geziyor-kâgit-gibi-incecik-derinligi-yok)
- [Kar sistemi AY IŞIĞINA bağlanmıştı](#kar-sistemi-ay-isigina-baglanmisti)
- [VFX zemin kesmesi karın TAMAMINI sildi — iki kez](#vfx-zemin-kesmesi-karin-tamamini-sildi-iki-kez)
- [Kar tanesi yoğunluğu: kutuyu küçültmek TERS tepti](#kar-tanesi-yogunlugu-kutuyu-kucultmek-ters-tepti)
- [`fix.md` denetimi — 16 iddianın 6'sı doğrulandı, 3'ü geçersiz çıktı](#fixmd-denetimi-16-iddianin-6si-dogrulandi-3u-gecersiz-cikti)
- ["Etrafta kâğıt gibi incecik beyaz örtü geziyor, bir derinliği yok"](#etrafta-kâgit-gibi-incecik-beyaz-ortu-geziyor-bir-derinligi-yok)
- ["Yağış 1 kar 1 iken ekranda yağmur izleri var"](#yagis-1-kar-1-iken-ekranda-yagmur-izleri-var)
- ["Yürürken kar tanecikleri çok hızlı yer değiştiriyor, sürekli yeniden render oluyor gibi"](#yururken-kar-tanecikleri-cok-hizli-yer-degistiriyor-surekli-yeniden-render-oluyor-gibi)
- ["Rüzgâr 0 ama kar belirli bir yöne yağıyor — yürürken düzeliyor, dururken rüzgâr varmış gibi"](#ruzgâr-0-ama-kar-belirli-bir-yone-yagiyor-yururken-duzeliyor-dururken-ruzgâr-varmis-gibi)
- ["Yakın uzun tanecikler sola düşerken biraz ilerideki tanecikler sağa düşüyor"](#yakin-uzun-tanecikler-sola-duserken-biraz-ilerideki-tanecikler-saga-dusuyor)
- ["Bu ne, ayaklarımın gölgesi mi?" — aşağı bakınca iki kara leke](#bu-ne-ayaklarimin-golgesi-mi-asagi-bakinca-iki-kara-leke)
- ["Kar tutması için ne kadar beklemeliyim" → tutmuyor](#kar-tutmasi-icin-ne-kadar-beklemeliyim-tutmuyor)
- [Kar birikiyor ama zemin beyazlamıyor — örtü 0'da kalıyor](#kar-birikiyor-ama-zemin-beyazlamiyor-ortu-0da-kaliyor)
- [Zeminde düzenli, tekrar eden koyu leke ızgarası](#zeminde-duzenli-tekrar-eden-koyu-leke-izgarasi)
- [Ayak izi karı delip çıplak zemini gösteriyor](#ayak-izi-kari-delip-ciplak-zemini-gosteriyor)
- [İz kenarında diken diken duvar](#iz-kenarinda-diken-diken-duvar)
- [Kar sadece oyuncunun çevresindeki KARE alanda tutuyor](#kar-sadece-oyuncunun-cevresindeki-kare-alanda-tutuyor)
- [İz çukur gibi, dik duvarlı ve dipte karanlık](#iz-cukur-gibi-dik-duvarli-ve-dipte-karanlik)
- [Oyuncunun çevresinde parlaklığı farklı bir KARE](#oyuncunun-cevresinde-parlakligi-farkli-bir-kare)
- [Arazi simsiyah, oyuncunun çevresindeki kare normal](#arazi-simsiyah-oyuncunun-cevresindeki-kare-normal)
- [Kar izi damga gibi, RDR2 oluğu değil](#kar-izi-damga-gibi-rdr2-olugu-degil)
- [Kar izinde hareket ederken titreme](#kar-izinde-hareket-ederken-titreme)
- [1 cm karda yüzey delik deşik, kare geri geldi](#1-cm-karda-yuzey-delik-desik-kare-geri-geldi)
- [Kare oyuncuyla birlikte geliyor (kalan %2.3)](#kare-oyuncuyla-birlikte-geliyor-kalan-23)
- [Kare EĞİMLİ arazide geri geliyor (düz zeminde yok)](#kare-egimli-arazide-geri-geliyor-duz-zeminde-yok)
- [Kare DERİNLİKLE ölçekleniyor: 1 ve 5 cm temiz, 20 ve 50 cm'de var](#kare-derinlikle-olcekleniyor-1-ve-5-cm-temiz-20-ve-50-cmde-var)
- [Yerinde beklerken iz yuvarlak bir çukur gibi derinleşiyor](#yerinde-beklerken-iz-yuvarlak-bir-cukur-gibi-derinlesiyor)
- [İz kenarı çok düzgün / testere dişi gibi tekrarlıyor](#iz-kenari-cok-duzgun-testere-disi-gibi-tekrarliyor)
- [İz dikdörtgen, geniş, kenarında dağılma yok (üçü tek kök)](#iz-dikdortgen-genis-kenarinda-dagilma-yok-ucu-tek-kok)
- [Yön değiştirirken iz dikdörtgen / satır satır çıkıyor](#yon-degistirirken-iz-dikdortgen-satir-satir-cikiyor)
- [Uzaktaki parıltılar kocaman piksel gibi görünüyor](#uzaktaki-pariltilar-kocaman-piksel-gibi-gorunuyor)
- [24 m'lik kare geri geldi (gece/şafak, koyu sahnede parlak alan)](#24-mlik-kare-geri-geldi-gecesafak-koyu-sahnede-parlak-alan)
- ["Kar tuttuğu zaman yer fazla bembeyaz, doku hiç görünmüyor"](#kar-tuttugu-zaman-yer-fazla-bembeyaz-doku-hic-gorunmuyor)
- ["İzin üstü doygun mavi/hardal leke, ortasında kara sürme"](#izin-ustu-doygun-mavihardal-leke-ortasinda-kara-surme)
- ["Kar izi yine havada" — sonra "kar izi kayboldu"](#kar-izi-yine-havada-sonra-kar-izi-kayboldu)
- ["Kar izi havada" — asıl sebep: GÖZ KARIN İÇİNDEYDİ](#kar-izi-havada-asil-sebep-goz-karin-icindeydi)
- ["Dikdörtgen ayağım varmış gibi iz, dümdüz yürürken zigzag" — asıl sebep: KAR DİK DUVAR TUTUYORDU](#dikdortgen-ayagim-varmis-gibi-iz-dumduz-yururken-zigzag-asil-sebep-kar-dik-duvar-tutuyordu)
- ["İz saniyeler içinde kayboluyor" — asıl sebep: SİMÜLASYON KARE BAŞINA BİRDEN ÇOK KEZ İLERLİYORDU](#iz-saniyeler-icinde-kayboluyor-asil-sebep-simulasyon-kare-basina-birden-cok-kez-ilerliyordu)
- ["İz kenarı tırtıl gibi, düzenli diş" — asıl sebep: DAMGA KADANSI](#iz-kenari-tirtil-gibi-duzenli-dis-asil-sebep-damga-kadansi)
- ["Farklı açılara dönünce iz zigzaglı, ama HER ZAMAN değil" — asıl sebep: KALICILIK BLOKLARI CANLI İZİ EZİYORDU](#farkli-acilara-donunce-iz-zigzagli-ama-her-zaman-degil-asil-sebep-kalicilik-bloklari-canli-izi-eziyordu)
- [Aynı belirtinin ASIL sebebi: SIRT ÇUKURUN DERİNLİĞİNDEN ÇIKARILIYORDU](#ayni-belirtinin-asil-sebebi-sirt-cukurun-derinliginden-cikariliyordu)
- ["Zigzag HER ZAMAN olmuyor" — asıl sebep: SIRT GÖVDENİN OTURMA YÜKSEKLİĞİNE SIZIYORDU](#zigzag-her-zaman-olmuyor-asil-sebep-sirt-govdenin-oturma-yuksekligine-siziyordu)
- [Zigzag/tarak — ASIL SEBEP: YOĞUNLUK, GEOMETRİ DEĞİL](#zigzagtarak-asil-sebep-yogunluk-geometri-degil)
- ["Yuvarlak kaynak nasıl zigzag iz çıkarır" — çıkarmıyor, YOL zigzag](#yuvarlak-kaynak-nasil-zigzag-iz-cikarir-cikarmiyor-yol-zigzag)
- ["Zigzag" — dört gün, gerçek sebep çizim yolunda çıktı](#zigzag-dort-gun-gercek-sebep-cizim-yolunda-cikti)
- [Sırt hesaplanıyordu ama hiç çizilmiyordu](#sirt-hesaplaniyordu-ama-hic-cizilmiyordu)
- ["Dışarıdaki karda hiçbir detay yok" — kapı üç turluk işi yutmuştu](#disaridaki-karda-hicbir-detay-yok-kapi-uc-turluk-isi-yutmustu)
- [Görünürlüğü genlik değil EĞİM belirliyor](#gorunurlugu-genlik-degil-egim-belirliyor)
- [Aynı alanı 36 kez hesaplamak](#ayni-alani-36-kez-hesaplamak)
- [Yüzey titremesi — DÖRT ayrı kaynak, dördü de rüzgâra bağlıydı](#yuzey-titremesi-dort-ayri-kaynak-dordu-de-ruzgâra-bagliydi)
- [Kar kalınlığının görsel karşılığı yoktu](#kar-kalinliginin-gorsel-karsiligi-yoktu)
- [İzin kenarı "border" gibi koyu — geçişi yumuşatınca dairesel HALE çıktı](#izin-kenari-border-gibi-koyu-gecisi-yumusatinca-dairesel-hale-cikti)
- [Ölçüm aracı yalan söylüyordu: `cam.Render()` tonemap'i atlıyor](#olcum-araci-yalan-soyluyordu-camrender-tonemapi-atliyor)
- ["Şafak ve ikindide zemin kapkara" — sistem hatası değil, DAĞIN KENDİ GÖLGESİ](#safak-ve-ikindide-zemin-kapkara-sistem-hatasi-degil-dagin-kendi-golgesi)
- [Ölçüm oturumu kendini bozdu: aynı kare 106 → 21](#olcum-oturumu-kendini-bozdu-ayni-kare-106-21)
- ["1 cm'de iz yok, 20/50 cm'de çok geniş" — İKİ ayrı sebep, ikisi de kâğıtta çıktı](#1-cmde-iz-yok-2050-cmde-cok-genis-iki-ayri-sebep-ikisi-de-kâgitta-cikti)
- ["İzin kenarları pikselimsi, border var gibi" — yumuşatma çekirdeği ALTÖRNEKLİYORDU](#izin-kenarlari-pikselimsi-border-var-gibi-yumusatma-cekirdegi-altornekliyordu)
- [Kare basamakların ASIL sebebi: normal bir TÜREV, bilinear filtreleme C1 süreksiz](#kare-basamaklarin-asil-sebebi-normal-bir-turev-bilinear-filtreleme-c1-sureksiz)
- [Normal karışımındaki `lerp` — ölçüldü, sorun değil](#normal-karisimindaki-lerp-olculdu-sorun-degil)
- [Kare konturun ASIL sebebi: çukur gölgesindeki İKİ SERT EŞİK](#kare-konturun-asil-sebebi-cukur-golgesindeki-iki-sert-esik)
- [Çukur gölgesi: ışın yürüyüşü silindi, horizon analitik oldu](#cukur-golgesi-isin-yuruyusu-silindi-horizon-analitik-oldu)
- [Gölge tavanı sabitti, atmosferden türetildi](#golge-tavani-sabitti-atmosferden-turetildi)
- [HUD yanlış bilgi veriyordu: iki farklı kapsama aynı adı taşıyordu](#hud-yanlis-bilgi-veriyordu-iki-farkli-kapsama-ayni-adi-tasiyordu)
- [Alçak güneşte keskin kenarlı adacıklar — eksik olan KAR-KAR yatay transferi](#alcak-guneste-keskin-kenarli-adaciklar-eksik-olan-kar-kar-yatay-transferi)
- [Alçak güneşte keskin adacıklar — SASTRUGİ eğimi fizikten üç kat dik](#alcak-guneste-keskin-adaciklar-sastrugi-egimi-fizikten-uc-kat-dik)
- [Alçak güneşte keskin adacıklar — fBm'in RMS eğimi fizikten iki-üç kat fazla](#alcak-guneste-keskin-adaciklar-fbmin-rms-egimi-fizikten-iki-uc-kat-fazla)
- [Keskin adacıkların ASIL sebebi: detay normalinin MAKRO katmanı — çift sayım](#keskin-adaciklarin-asil-sebebi-detay-normalinin-makro-katmani-cift-sayim)
- [Yukarıdaki "makro katman" teşhisi ÇÜRÜDÜ — ölçüm aracı yalan söylüyordu](#yukaridaki-makro-katman-teshisi-curudu-olcum-araci-yalan-soyluyordu)
- [`_SNOW_QUALITY_*` hiçbir shader'da tanımlı değil — üç katman ölü kod](#_snow_quality_-hicbir-shaderda-tanimli-degil-uc-katman-olu-kod)
- [ÇÖZÜLDÜ: yüzey rölyefinin toplam RMS eğimi fizikten iki-üç kat fazlaydı](#cozuldu-yuzey-rolyefinin-toplam-rms-egimi-fizikten-iki-uc-kat-fazlaydi)
- ["Hafif uzak zemin detaysız gözüküyor"](#hafif-uzak-zemin-detaysiz-gozukuyor)
- ["Bu nasıl bir gölgelendirme aklım almıyor"](#bu-nasil-bir-golgelendirme-aklim-almiyor)
- [Su çizgisi basamaklı, testere gibi — deniz mesh'i inceltilince DEĞİŞMİYOR](#su-cizgisi-basamakli-testere-gibi-deniz-meshi-inceltilince-degismiyor)
- [Ölçüm aracı bütün kademelerde aynı sabiti döndürüyor](#olcum-araci-butun-kademelerde-ayni-sabiti-donduruyor)
- [GPU süresi hep 0.000 ms — "deniz bedava"](#gpu-suresi-hep-0000-ms-deniz-bedava)
- ["Kıyı full köpük. Saçma sapan." — rüzgâr 0,5 m/s'ken](#kiyi-full-kopuk-sacma-sapan-ruzgâr-05-msken)
- [Sığ suyun altındaki kum kuru çiziliyordu](#sig-suyun-altindaki-kum-kuru-ciziliyordu)
- ["Alttan bir beyazlık gelip gidiyor, 2 saniyede bir"](#alttan-bir-beyazlik-gelip-gidiyor-2-saniyede-bir)
- ["Denizin sınırlarında smooth geçiş yok, keskin çizgilerle ayrılıyor"](#denizin-sinirlarinda-smooth-gecis-yok-keskin-cizgilerle-ayriliyor)
- [Denizde ızgara/dama deseni — köpükte](#denizde-izgaradama-deseni-kopukte)
- [F1'de son üç onay kutusuna tıklanmıyor](#f1de-son-uc-onay-kutusuna-tiklanmiyor)
- ["Denizin bittiği sınır sert. Köpüğün sınırı yumuşak, denizinki değil."](#denizin-bittigi-sinir-sert-kopugun-siniri-yumusak-denizinki-degil)
- ["Aşağıdan gelen beyazlık hâlâ çok hızlı gidip geliyor, ayrıca çok ileri gidiyor"](#asagidan-gelen-beyazlik-hâlâ-cok-hizli-gidip-geliyor-ayrica-cok-ileri-gidiyor)
- ["Köpükler niye düzenli desenlere ve aralıklara sahip?"](#kopukler-niye-duzenli-desenlere-ve-araliklara-sahip)
- [Köpük şeritleri eşit aralıklı](#kopuk-seritleri-esit-aralikli)
- ["Köpükler dalgaların köpüğüyse, dalgalar düzenli demektir"](#kopukler-dalgalarin-kopuguyse-dalgalar-duzenli-demektir)
- ["Dalgalar niye sadece kıyıda?" — deniz suçsuz, rüzgâr yok](#dalgalar-niye-sadece-kiyida-deniz-sucsuz-ruzgâr-yok)
- [Gece hep aynı parlaklıkta — ay doğuyor, batıyor, hiçbir şey değişmiyor](#gece-hep-ayni-parlaklikta-ay-doguyor-batiyor-hicbir-sey-degismiyor)
- [Gökyüzü probu gün doğumunda ve batımında çöküyor — KAPANDI](#gokyuzu-probu-gun-dogumunda-ve-batiminda-cokuyor-kapandi)
- [Ufuktaki uzak bulutların kenarında parlak kontur (yakın bulutlarda yok)](#ufuktaki-uzak-bulutlarin-kenarinda-parlak-kontur-yakin-bulutlarda-yok)
- [Uzak bulutlar sise gömülüyor — ve bunu bir ikinci hata gizliyormuş](#uzak-bulutlar-sise-gomuluyor-ve-bunu-bir-ikinci-hata-gizliyormus)
- [Kar izi 1 cm ve 5 cm'de hiç görünmüyor](#kar-izi-1-cm-ve-5-cmde-hic-gorunmuyor)
- [Deniz dibinde kar birikiyor](#deniz-dibinde-kar-birikiyor)
- [Denizde kayan kahverengi lekeler](#denizde-kayan-kahverengi-lekeler)
- [Uzaktan köpük yapıştırılmış leke gibi duruyor](#uzaktan-kopuk-yapistirilmis-leke-gibi-duruyor)

---

## Oyuncunun çevresinde büyük bir kare, onunla birlikte hareket ediyor

**Elenen şüpheliler — hepsi ölçümle:**

| Şüpheli | Nasıl elendi |
|---|---|
| Clipmap halkaları | Halkalar tamamen silindi (spec §8.1), kare kaldı |
| Zemin dokusu çözünürlüğü (7,32 m teksel) | Kar ile arazi arasındaki ayrılık ölçüldü: ortalama 1 cm, en fazla 6 cm. Mesh havada durmuyor |
| Dağın kar maskesi kapalı | "Zorla karla kapla" açıldı, hiçbir şey değişmedi |

**Araç yalanı:** bir tur `SnowSurface.enabled = false` ile "mesh kapalı, kare
duruyor" sonucuna varıldı. Mesh KAPANMAMIŞTI — `SnowSurfaceMesh` nesnesi
`activeInHierarchy: true` bulundu. O turun iki sonucu da geçersizdi.

**Sebep:** kar mesh'i ile arazi AYNI YERİ FARKLI KALINLIKTA boyuyordu.

- Mesh kenarında kalınlığı sıfıra indiriyor (spec §8.3, `h *= fade`)
- Arazi tam orada `SnowStateAt`'ten okuduğu 45 cm'i boyuyor

Aralarında 2 metre genişliğinde bir **hendek** kalıyor; kare o hendeğin
çerçevesi. Spec §8.3 doğru, ama varsayımı "mesh'in ötesinde kar yoktur" —
bizde arazinin kendi kar katmanı vardı.

**Ayırt eden ölçüm:** kalınlık probu. İki shader da kalınlığı aynı ölçekte gri
döndürüyor, aydınlatma çalışmıyor. Üç seviye tek bakışta göründü:

```
dışarısı        orta gri        ~45 cm
karenin içi     beyaza yakın    ~55-60 cm
kenar halkası   KAPKARA         ~0 cm      <- hendek
```

**Düzeltme:** arazinin karı derinlik değil ÖRTÜ (spec §16) — global skaler
`_SnowCoverage`, kalınlık `_SnowCoverThickness` (4 cm). `MountainSurface` artık
`SnowStateAt` okumuyor. Sınırdaki fark 45 cm yerine 4 cm.

**Kural:** iki katman aynı yeri boyuyorsa ikisi de AYNI büyüklüğü göstermek
zorunda. Biri derinlik biri örtü okuyorsa sınır her zaman görünür — ve sınır
oyuncuyu takip ediyorsa kare olur.

**Araç notu:** teşhis aracını kullanmadan önce aracın kendisi doğrulandı — kar
her yerde 45 cm iken prob orta gri gösterdi (45/60 = 0,75, beklenen). Bir önceki
turda bu yapılmadığı için yanlış sonuca varıldı.

---

## Ayak altında kare bir sırt/çıkıntı, oyuncuyla birlikte geliyor

**Elenen şüpheliler — hepsi izolasyon anahtarıyla, tek turda:**

| Şüpheli | Nasıl elendi |
|---|---|
| Kenar sönümü | Prob mod 4: o bölgede sönüm düz (=1) |
| Halka sırası | Prob mod 1: kırmızı→yeşil→mavi→sarı düzgün iç içe |
| Dikiş | F1 anahtarı kapatıldı, çıkıntı kaldı |
| Etek | F1 anahtarı kapatıldı, çıkıntı kaldı |

**Sebep:** kar mesh'i ±8 m'de yakın bölgeden uzak kaskada devrediyor ve devirde
**ham SWE ile yoğunluk AYRI AYRI** harmanlanıyordu. Derinlik `SWE × 1000 / ρ`,
yani doğrusal değil; iki büyüklüğü ayrı harmanlamak aradaki derinlik profilini
sıçratıyor.

**Ölçüm:** `derinlik yakın 52,5 cm (ρ 95) · kaskad 45,4 cm (ρ 110) · FARK 7,0 cm`.
7 cm, 45 cm'lik bir tabakada gözle görülür bir sırt — üstelik kare, çünkü
`SnowInsideMask` kare bir bölgenin kenarında sönüyor.

Yoğunluk farkı tasarımdan: kaskad bilinçli sadeleştirilmiş (spec Faz 10) ve
ıslaklık kanalı yok.

**Düzeltme:** derinliğin KENDİSİ harmanlanıyor. İki uç ne olursa olsun arada
tek yönlü düz bir rampa kalıyor.

**Kural:** iki bölgeyi birleştirirken **görünen büyüklüğü** harmanla, onu üreten
ham girdileri değil. Girdiler doğrusal olmayan bir bağıntıdan geçiyorsa ayrı
harmanlama her zaman basamak üretir.

**Araç notu:** prob görünümü (her şüpheli ayrı renk, ışıktan bağımsız) dört
şüpheliyi tek turda eledi. Ondan önce aynı belirtiye dokuz tur harcandı.

**Mekanizma artık yok** (2026-08-22): spec v2 §8.1 çok seviyeli clipmap'i
yasakladı, uzak kaskad §8.4 gereği silindi. Devir noktası diye bir yer kalmadı.
Yukarıdaki KURAL geçerliliğini koruyor — bu belge dersleri tutuyor, kodu değil.

---

## Kar yüzeyinde uzun, düz, dik bir sırt (çıkıntı)

**İlk şüpheli:** kenar sönümü. *(Yanlış — sönüm yarıçapı ve bandı üç kez
değişti, sırt aynı kaldı.)*

**Sebep:** eteğin iç dikişe konması. Etek 2 m AŞAĞI iniyor. Yamaçta dikişin
aşağı tarafındaki yüzey eteğin tepesinden alçakta kalıyor, etek açıkta kalıyor
ve dik bir duvar olarak görünüyor. Etek yalnız EN DIŞ halkada olmalı — orada
ötesinde yüzey yok, arazinin içine giriyor.

**Aynı turda çıkan iki kusur daha:**

1. **T-kavşağı.** İnce halkanın kenarında köşe aralığı kaba halkanınkinin
   üçte biri; aradaki iki ince köşe kaba kenarın düz çizgisinden sapıyor ve
   dikiş boyunca yarık açıyor. Sınır köşeleri artık yüksekliği KABA ızgaradan
   okuyor (bilinear dikiş).
2. **Derinlik payı basamağı.** Pay, halkalar bindirdiğinde hangisinin
   kazanacağını belirlemek içindi. Ortak snap'ten sonra bindirme sıfır;
   halkalar sınır çizgisini PAYLAŞIYOR ve pay tam o çizgide pay kadar basamak
   üretiyor. 1 mm → 2 cm → **0**.

**Kural:** bir düzeltme başka bir kusurun varlığına dayanıyorsa, o kusur
kapandığında düzeltme de kusura döner. Derinlik payı ve iç etek ikisi de
bindirmenin/boşluğun telafisiydi; bindirme sıfırlanınca ikisi de zarara geçti.

**Mekanizma artık yok** (2026-08-22): halka, delik, etek ve dikiş silindi —
mesh tek kare ızgara. Üç kusurun üçü de çok seviyeli clipmap'in kendi
karmaşıklığından doğmuştu; spec §8.1 bu yüzden yasaklıyor.

---

## Kar yüzeyi bloklar/basamaklar hâlinde; mesh kapatılınca zemin pürüzsüz

**İlk şüpheli:** clipmap halkalarının dikişi. *(Yanlış — halka sürgüsüyle
ölçüldü, basamaklar her halkada var.)*

**İkinci şüpheli:** kenar sönümü ve `clip` gürültüsü. *(Yanlış — sönüm
yarıçapı düzeltildi, basamaklar kaldı.)*

**Sebep:** zemin yüksekliği `TextureFormat.RHalf` dokuya **0–8000 m** aralığında
normalize edilip yazılıyordu. Half'ın bağıl adımı 2^-11 sabit; metre karşılığı
kotla birlikte büyüyor:

| Kot | Adım |
|---|---|
| 2000 m | **195 cm** |
| 4000 m | **391 cm** |
| 8000 m | **781 cm** |

Kar kalınlığı 26–45 cm. Zemin iki metrelik basamaklara oturunca kar yüzeyi
başka türlü çizilemezdi. Oyuncunun 206 m kotunda adım 12 cm — kalınlığın
yarısı, bu yüzden bloklar orada da görünüyordu.

**Ayırt eden ölçüm:** kar mesh'ini kapatmak. Dağın kendi kar katmanı (yer
değiştirme YOK, yalnız gölgeleme) pürüzsüz çıkıyordu. Yer değiştiren tek yüzey
bozuktu → hata yükseklik kaynağında.

Sonra half round-trip adımı kot kot hesaplandı ve sayı tabloya döküldü.

**Kural:** dokuya normalize edilmiş bir büyüklük yazarken, aralığın ÜST ucundaki
adımı metre cinsinden hesapla. Half 0..1 aralığında ucuz görünür; 8000 m ile
çarpıldığında metre olur. Spec §7.1 `RHalf` diyor ama küçük arazi varsayıyor —
bilinçli sapma, gerekçesi `DECISIONS.md`.

---

## Kar açılınca çevrede basamaklı, dikey çizgili devasa bir duvar

**İlk şüpheli:** mesh'in araziden ayrı durması. *(Yanlış — 128 m'de ortalama
ayrılık 1 cm.)*

**İkinci şüpheli:** dış halkanın kenarındaki kalınlık basamağı. *(Yanlış — halka
sürgüsüyle ölçüldü: duvar 1'de de 4'te de aynı yerde.)*

**Üçüncü şüpheli:** gölgeleme. *(Kısmen — yer değiştirme kapatılınca kalınlık
gitti, çizgiler kaldı. Ama sebep bu değildi.)*

**Sebep: İKİ SAYI AYRIŞMIŞTI.** `SnowMeshBuilder` halkaları
`Ring0Extent × RingScale^i` ile büyütüyor: ±4, ±12, ±36, **±108 m**. Kenar
sönümüne yayınlanan yarıçap ise `AreaSize × 2^(R−1) × 0.5` = **64 m**
hesaplıyordu. Sönüm 64 m'de kalınlığı sıfırlayınca `clip(h − 0.004)` kar
yüzeyini **ortasından** testere gibi kesiyordu. Basamaklar dörtgen köşegenleri,
dikey çizgiler kesik kenarın normalleri.

`AreaSize` kar DURUMU bölgesi (16 m); halka ölçüsüyle ilgisi yok. İkisini aynı
sanmak iki gün yaktı.

**Ayırt eden ölçüm:** `SnowMeshBuilder.Describe`'ın verdiği gerçek halka
ölçüleri ile shader'a yayınlanan yarıçapı yan yana bastırmak. Fark tek satırda
göründü: `±0.0 m` / `±108.0 m`.

**Kural:** bir shader'a "sınır" gönderiyorsan, o sınırı üreten geometriden
TÜRET. İkinci bir formülle yeniden hesaplama — iki formül er geç ayrışır ve
ayrıştığında hiçbir yerde hata vermez. Sınama artık ikisini karşılaştırıyor.

---

## Kar yağıyor ama dağ çıplak; ayak altında beyaz kare seninle geliyor

**İlk şüpheli:** kar mesh'inin araziden ayrı durması. *(Yanlış — 128 m'de 1089
nokta ölçüldü, ortalama ayrılık 1 cm.)*

**İkinci şüpheli:** pişen zemin dokusunun ters indeksli olması. *(Yanlış — 0.0 m
fark; ters indeks olsaydı 1017 m verecekti.)*

**Üçüncü şüpheli:** `Shader.SetGlobalFloat`'ın compute'a ulaşmaması.
*(Yanlış — ölçüldü: 12.345 yazıldı, compute'ta 12.345 okundu.)*

**Sebep:** kar çizgisi yalnız İLK TEMİZLİKTE ve bölgeye YENİ giren şeritte
uygulanıyordu. Oyun +8 °C'de açılıyor, donma seviyesi 1451 m, oyuncu 205 m'de —
çizginin altında, bölge doğru olarak SWE 0 ile doluyor. Sonra sıcaklık düşüp
çizgi −557 m'ye inince **mevcut tekseller güncellenmiyor**. Birikme 1.39e-6 m/s,
görünür kalınlığa saatler sürüyor.

Beyaz kare de kar mesh'inin toplam kapsaması: 16 m × 2³ = **128 m**.

**Ayırt eden ölçüm:** F1'e iki ayrı sayı kondu — kar çizgisinden hesaplanan
kalınlık (45.5 cm, DOĞRU) ve durum dokusunun geri okuması (`DOKUDA 0.00`,
BOŞ). İkisi aynı satırda görününce sebep tek bakışta ayrıldı: çizgi doğru,
doku boş.

**Kural:** bir alanı "başlangıçta doldur" diye kurduysan, o alanı süren
değişken SONRADAN değişebiliyorsa doldurma da tekrarlanmalı. Yoksa belirti
"özellik hiç çalışmıyor" gibi görünür, oysa yalnız bir kez çalışmıştır.

---

## Havada solucan/sigara dumanı gibi kar; bir yerde yağıyor, bir yerde boş gökyüzü

**İlk şüpheli:** tane sayısı az. *(Yanlış — sayı dört katına çıkarıldı, desen aynen kaldı,
sadece daha kalabalık oldu.)*

**Sebep:** doğum hash'i çöküyordu. `frac(sin(dot(p, k)) * 43758.5)` float32'de büyük
girdide tekrar eden değer üretiyor.

**Ayırt eden ölçüm:** GPU aritmetiği float32 olarak Python'da taklit edildi, üretilen
X koordinatlarının TEKİL SAYISI sayıldı:

| tane | tekil X değeri | oran |
|---|---|---|
| 13 000 | 1 887 | %14.5 |
| 104 000 | 5 237 | **%5.0** |

Yüz bin tane beş bin dikey hat üzerine yığılıyor. "Solucan" o hatların kendisi,
"sigara dumanı" hattın rüzgârla eğilmiş hâli, "bir yerde boş gökyüzü" de hatların
arasındaki boşluk.

PCG3D ile aynı ölçüm: 104 000/104 000 tekil, kova sapması ×1.04, eksenler arası
korelasyon 0.0003.

**Kural:** indis veya dünya koordinatı büyüyebilen hiçbir yerde `frac(sin(...))` hash
kullanılmaz. Tam sayı hash (PCG3D) girdinin büyüklüğünden etkilenmiyor. Parıltı
hash'i de aynı sınıftaydı — hücre `floor(posWS.xz / cellSize)`, 6000 m'lik dağda
milyonlara çıkıyor.

---

## On ayrı sınama birden "yanlış sonuç" diyor, hepsi sıfır

**İlk şüpheli:** sınamaların ölçtüğü on ayrı davranış. *(Yanlış — hepsi doğruydu.)*

**Sebep:** tek satırlık eksik include. `SnowSparkle.hlsl` `SnowCommon.hlsl`'i include
etmiyordu; `SnowTestKernels.compute`'un ALTI kerneli birden derlenmedi ve dispatch'ler
sessizce sıfır döndü.

**Ayırt eden ölçüm:** sınama koşucusuna `ShaderUtil.GetComputeShaderMessages` eklendi
ve mesajlar raporun BAŞINA yazıldı. Sebep tek satırda göründü.

**Kural:** compute sonucu topluca sıfırsa önce derleme mesajına bakılır, davranışa
değil. Derlenmeyen bir kernel ile yanlış hesaplayan bir kernel dışarıdan aynı görünür.

---

## Ekranın tamamı bembeyaz, ayak altında hareket eden siyahlık, 10 FPS

**İlk şüpheli:** tane sayısı ve tane parlaklığı. *(Yanlış — ikisi de düzeltildi,
belirti aynen kaldı.)*

**İkinci şüpheli:** çizimin başka bir kameraya sızması. *(Yanlış — kar sisteminde
gerçek `Camera` yok, yakalama da gökyüzü de elle çiziliyor.)*

**Sebep:** asgari ekran boyu ifadesinde işaret hatası. Yazdığım hâl:

```hlsl
float tanHalfFov = 1.0 / max(UNITY_MATRIX_P._m11, 1e-4);
```

D3D render hedefine çizerken projeksiyonun `[1][1]` öğesi **negatife düşüyor**
(y ekseni ters çevriliyor). `max(-1.732, 1e-4)` → `1e-4`, ölçek 10 000 katına
çıkıyor: 23 m uzaktaki 2 cm'lik tane **680 m**'lik bir dörtgen oluyor. On üç bin
tanenin on üçü bile ekranı kapatıyor; beyazlık tanenin rengi değil, üst üste
binen dev dörtgenler. Ayak altındaki "siyahlık" da aynı dörtgenlerin arkadan
aydınlanmış olanları.

**Ayırt eden ölçüm:** iki aşamalı.
1. Tampon CPU'ya okundu: boy 0.011–0.031 m, alpha 0.007–1.0, mesafe 1.3–23 m —
   **hepsi doğru**, hesaplanan kaplama %1.3. Yani hata tamponda değil, shader'da.
2. `_MinPixelSize` çalışma anında 0'a çekildi → beyazlık gitti, taneler ilk kez
   göründü. Şişen terim buydu.

**Aracın kendi yalanı:** ölçüm aracı piksel boyunu **C#'ta** `cam.fieldOfView`
ile hesaplıyordu ve doğru sayıyı veriyordu; shader başka bir ifade kullanıyordu.
Araç "beklenen" sütununu doğru bastığı için ilk turda "demek ki taneler değil"
sonucu çıktı. **Bir ifadeyi ölçmek istiyorsan ifadenin kendisini ölç, niyetini
değil.**

**Kural:** aynı işi yapan çalışan bir kod varsa ifade oradan kopyalanır.
`Precipitation.shader` → `PixelsPerRadian()` bu tuzağı yıllar önce yemiş ve
yanına `abs` şart diye not düşmüş. Kar tarafında sıfırdan yazıldığı için not da
kaybolmuştu. Sınama artık kaynağı denetliyor: `abs` olmadan `_m11` geçemiyor.

---

## Ekranın yarısı simsiyah, kamerayla birlikte geliyor

**İlk şüpheli:** sis. *(Yanlış — sis o pikselleri `renk × 1 + 0` ile aynen geçiriyordu.)*

**Sebep:** ters-Z'de `UNITY_RAW_FAR_CLIP_VALUE = 0`. O derinlikten dünya konumu geri
kurulunca sonuç **NaN**. NaN, `Blend One SrcAlpha` karışımından geçince arkasındaki her
şeyi siliyor — arazi, gökyüzü, bisiklet.

**Ayırt eden ölçüm:** Frame Debugger. Siyah, `Opaque Atmospheric Scattering`'e kadar
yoktu; bulut birleştirmesinde beliriyordu.

**Kural:** uzak düzlemden dünya konumu kurulacaksa ya yalnız **yön** kullanılır
(`normalize(far − kamera)` güvenli), ya da mesafe sınırlanır. Ham büyüklük kullanılmaz.

---

## Siluet kenarında tek piksellik kontur (gündüz koyu, denetimde beyaz)

**İlk şüpheli:** TAA. *(Yanlış — TAA'ya dokunulmadan kontur gitti.)*

**Sebep:** iki geçiş aynı pikseli **farklı sınıflandırıyordu**. Gök sisi `ZTest Equal` ile
onu "gökyüzü" sayıp sisliyor, paketin `Opaque Atmospheric Scattering`'i derinlik
dokusundan "geometri" sayıp kendi hava perspektifini biniyordu. Tek piksellik şeritte
çift işlem.

**Ayırt eden ölçüm:** dağın **gövdesi** düz kalıyordu, yalnız kenar bozuktu. Yüzey başına
hesaplanan bir şey tek piksellik kenar üretemez.

**Kural:** iki geçiş aynı pikseli farklı ölçütle sınıflandırıyorsa sıralarını ayır.
Geçiş noktaları `VolumetricFogFeature`'da yazılı.

---

## Gece etraf fazla aydınlık ("sisi kapatınca gerçekçi oluyor")

**İlk şüpheli:** ay şiddeti. *(Yanlış — `MoonIntensity` 0.0199'a dokunulmadı.)*

**Sebep:** sis renginin **seviyesi** elle yazılmış bir sabitti ve gökle birlikte
kaymıyordu. Gök gündüz–gece arası ~230 kat değişirken sis rengi 9.6 kat değişiyordu.

**Ölçüm:** sis rengi ÷ ortam probu DC → gündüz 1.43, gece 34.6. Kalibre oran 3.15.
Yani gündüz 2.2 kat fazla koyu, gece **11 kat fazla parlak**; gece sisin örttüğü her şey
3.5 durak yukarı kalkıyordu.

**Kural:** bir görsel seviye sabitten geliyorsa, o sabitin **hangi tek koşulda** doğru
olduğu sorulur. Cevap "biri" ise seviye ölçümden gelmelidir, ton sabitten.

---

## Ufka yapışık ince çizgi, kamerayla geliyor

**İlk şüpheli:** arazi geometrisi, uzak karo kenarları. *(Yanlış — sahnede sabit
kalmıyordu, kamerayla geliyordu.)*

**Sebep:** **sert kırpma**. `max(dir.y, 0.02)` sürekli ama türevi kırılıyor; göz türev
kırılmasını Mach bandı olarak okuyor. Aynı sınıf ikinci kez `abs(ks) > 1e-6 ? … : L`
eşiğinde çıktı — eşiğin iki yakası %16 fark veriyordu.

**Kural:** görsel bir gradyanı besleyen ifadede `max`/`min` ile taban koyma; yumuşak taban
kullan (`sqrt(y² + taban²)`) ya da seri açılımı (`1 − x/2 + x²/6`). Sıfırda sonsuz eğimli
üsler (`pow(x, <1)`) de aynı sınıf.

---

## Düzenli kafes deseni (sis banklarında, akış alanında)

**İlk şüpheli:** çözünürlük, örnekleme. *(Yanlış — desen matematikten geliyordu.)*

**Sebep:** sözde-rastgele alan **çarpımla** kurulmuştu: `sin(k₁·p) · sin(k₂·p)`. Bu ifade
ayrıştırılabilir ve düzenli bir kafes üretir; frekans karıştırmak bunu değiştirmez.
Koddaki yorum "iki farklı frekansın çarpımı tekrar desenini kırar" diyordu — kırmıyor.

**Kural:** rastgele alan modların **üst üste binmesidir**, çarpımı değil. Yönleri paralel
olmayan, dalga boyları oransız birkaç bileşen **toplanır**. Sinüs seçilirse CPU ile GPU
birebir aynı sonucu verir; hash tabanlı gürültü vermez ve bu projede alanın CPU kopyası
da var.

---

## Rüzgâr arttıkça yukarı uzanan, titreyen dikey şeritler

**İlk şüpheli:** akış alanının deseni. *(Yanlış — alan düzeltildi, şerit kaldı.)*

algılayıcı (`crest`/`lee`) 60–80 m'lik keskin eşikler taşıyor ve her okumada dört ayrı
arazi yüksekliği örnekliyor. Işın boyunca sekiz örnek bu alanın üstünden atlıyor; kamera
kıpırdadıkça örnekler başka yere düşüyor.

**Kural:** örnek sayısını artırmak çözüm değil — alan keskin kenarlı. Tek okuma noktası +
kapalı biçim integral kullanılır. Gök yolu (`SkyFogDepth`) bunu baştan doğru yapıyordu;
arazi yolu ondan öğrendi.

---

## BULUT KENARI KONTURU — İKİ AYRI HATA, BİRBİRİNE KARIŞTIRILMASIN

Aynı yerde, aynı satırda, birbirine hiç benzemeyen iki hata var. Bir kez ikisi aynı sanılıp
birinin çözümü ötekinin üstüne yazıldı ve **kapanmış bir hata geri geldi**. Ayrı tutuluyorlar.

Ayırt eden soru tek: **kontur hangi renkte ve nerede?**

| | Kontur nasıl görünüyor | Nerede |
|---|---|---|
| **(A)** halka eleniyor | arkasında ne varsa onun rengi — sissiz kalıyor | bulutta **ve dağda**, birebir aynı |
| **(B)** mesafe uyduruluyor | **siyah** | yalnız bulut çevresinde |

---

## (A) Bulutta VE dağda birebir aynı kontur — kenar halkası eleniyor

**Belirti:** her siluetin kenarında ince bir çizgi. Kullanıcının ifadesiyle: *"birebir aynı
kontur bulutlarda da var, dağda da var."* Gün batımında en okunaklı.

**İlk şüpheliler yanlıştı, dördü de ölçümle elendi:** yarı çözünürlük, büyütme filtresi,
zamansal birikim, adım gürültüsü. Büyütme filtresi ayrıca ayrı bir turda düzeltildi
(uzamsal ağırlığı ölüydü, derinliğe hiç bakmıyordu) ve **ekranda hiçbir şey değişmedi** —
sebep orada değildi.

**"Tam çözünürlükte kayboluyor" bir teşhis DEĞİLDİR.** `resolutionScale = 1` aynı anda beş
şeyi birden değiştiriyor. Bir kez bu gözlem sebep sanıldı ve kare hızının yarısı buna
ödendi (130 → 60 FPS). Hata gizlenmişti, çözülmemişti.

**Gerçek sebep:** bulut kenarı pikselinde derinlik gerçek değil. O piksel **eleniyordu**
(`hasCloud` false → sis uygulanmıyor). Elenince her siluetin çevresinde bir piksellik
**sissiz halka** kalıyor, hemen içeride sis uygulanıyor — sert açma-kapama. Halkanın
arkasında ne varsa (bulut, dağ, gök) sissiz göründüğü için kontur kazanıyor. Konturun
bulutta ve dağda **aynı** olmasının sebebi bu: kontur bulutun değil, **halkanın**.

**Ayırt eden ölçüm:** bulut yokken kontur da yok.

**Çözüm:** eleme yok. Kenar pikseli uzak düzlemin bir tık berisinde **sonlu bir proxy**
alıyor ve sisleniyor. Elenen tek şey GERÇEK uzak düzlem: orada ters-Z'de dünya konumu
sonsuza gidiyor, NaN çıkıyor ve `Blend One SrcAlpha` üzerinden arkadaki her şeyi siliyor.

**Bu proxy tek başına güvenli değil — (B)'yi doğurur. Clamp'siz kullanılmaz.**

---

## (B) Bulut çevresinde SİYAH kontur — uydurulan mesafe sisi doyuruyor

**Belirti:** yalnız bulutların çevresinde siyah bir çizgi. Arazi arkadayken okunuyor, gök
arkadayken görünmüyor (hava rengi ≈ gök).

**İlk şüpheli:** bulut ışın yürüyüşü, sonra zamansal birikim, sonra mesafe sınırı.
*(Üçü de yanlış — kontur bunlar değişmeden duruyordu.)*

**Ayırt eden ölçüm:** F1 "Bulut sisini kapat". Kapatınca kontur gidiyor, yükseklik sisi
arazide ve gökte açık kalsa bile — yani sebep birleştirme geçişinin sis bloğu.

**Gerçek sebep:** (A)'nın sonlu proxy'sinden dünya konumu geri kurulunca mesafe astronomik
çıkıyor (~70 km ve ötesi). NaN değil, ama float hassasiyeti bitiyor; sisin integrali doyuma
gidip halkayı hava rengine boyuyor.

**Çözüm — ve burası kritik:** proxy'yi kaldırmak DEĞİL, **mesafeyi sınırlamak**. Sınır
uydurma değil: sahnede hiçbir şey **uzak düzlemden** öteye gidemez (`_ProjectionParams.z`),
ve paket kendi hava perspektifini de aynı şekilde sınırlıyor. Yalnız **yol** sınırlanıyor;
optik derinliğe tavan konulmuyor, yani beyazlama ve fırtına ucu etkilenmiyor.

**Derinlik çıkışı kapalıyken** her bulut pikseli proxy alır ve ekran kocaman siyah lekelerle
dolar (ölçüldü). O yolda mesafe bilinmiyor: sis uygulanmaz.

---

---

## (B) GERİ DÖNDÜ VE SEBEBİ NaN'MIŞ (2026-08-31)

**Belirti:** aynı siyah kontur, ama (A) ve (B)'nin shader düzeltmeleri **ikisi de kodda
duruyorken**. Yalnız seyrek bulutta görünüyor; kapsama %90'ın üstündeyken görünmüyor.

**Gerçek sebep:** halkanın sahte mesafesiyle `FogPath` taşıyor ve **NaN** dönüyor.
`Blend One SrcAlpha` onu her bulutun çevresine siyah olarak boyuyor.

**NEDEN BU KADAR UZUN SÜRDÜ — ölçüm aracı yapısal olarak yalan söylüyordu:**

```hlsl
fogScattering *= _CloudFogEnabled;   // NaN * 0 = NaN
```

Bu satır bir NaN'ı **susturamaz**. Yani kaydın kendi ayırt edici testi olan "bulut sisini
kapat" konturu kaldırmıyor ve ölçüm her seferinde "sis değil" diyor. O yanlış sonuçla
sırayla büyütme filtresi, bulut gölgesi, bulut sahne-derinliği ve sis geçişi özelliği
denendi — dördü de ölçümle elendi, çünkü dördü de suçsuzdu.

**Ayırt eden şey kullanıcının bilgisi oldu:** *"yükseklik sisini kapattığımda düzeliyor."*
`FogEnabled` çarpan koymuyor, **yoğunlukları sıfırlıyor**; integral hiç taşmıyor. İki
anahtarın farkı buydu.

**Çözüm:** halka uydurulmuş mesafe almıyor. Tek piksel geniş olduğu için gerçek mesafe
hemen yanında: sekiz komşudan uzak değerden en çok sapan (en yakın yüzey) alınıyor.
Hiçbiri geçerli değilse piksel uzak değerde kalıyor ve **hiç sislenmiyor** — sissiz halka
(A), siyah halkadan çok daha küçük bir kusur, ve hiçbir yerde uydurma sayı yok.

**İki kural:**

1. **Bir değer NaN olabiliyorsa çarpanla kapatılamaz.** Sebep, sayının üretildiği yerde
   düzeltilir. Bir anahtar "kapattım ama değişmedi" diyorsa, anahtarın o yolu gerçekten
   kesip kesmediği ayrıca doğrulanır.
2. **Belirtiyi gizleyen koşulda test etme.** Bu tur bir kez "çözüldü" denildi; doğrulama
   %90 bulut kapsamasında yapılmıştı, yani konturun görünemediği koşulda.


## Neden bu ikisi birbirine karıştı — ve tekrarlanmaması için

(B) çözülürken (A) ile aynı hata sanıldı. (B)'yi kapatmanın kolay yolu proxy'yi kaldırıp
pikseli elemekti — ama eleme tam olarak (A)'nın sebebidir ve o **zaten ölçülüp yazılmıştı**.
Commit'in kendi notu: *"Eski 'mesafeyi sınırla' telafisi (clamp) de silindi — gerekçesi
kalktı."* Gerekçe kalkmamıştı: clamp, proxy'yi güvenli kılan şeydi. İkisi birbirinin
alternatifi değil, **birbirinin tamamlayıcısı**.

Kaydı geri getiren şey `git log -S"kontur"` oldu; kullanıcı "bunu daha önce çözmüştük"
dediği için bakıldı.

**Kural:** aynı satırdaki iki belirti aynı sebep değildir. Bir düzeltme başka bir
düzeltmenin terimini siliyorsa, silinen terimin gerekçesi **ayrıca** çürütülmelidir —
"gerekçesi kalktı" demek yetmez.

**Not:** bu satırdaki ÜÇÜNCÜ hata ayrı bir kayıtta: "Uzak bulutlardaki parlak kontur"
(sönümleme ile dolgu aynı ağırlığı paylaşıyordu). Üçü de aynı iki satırda yaşıyor, üçü de
farklı.

---


## Katılımcı ortam (sis, bulut) fazla koyu ya da fazla parlak

**Sebep:** birim karışması. `RenderSettings.ambientProbe` **yüzey aydınlatması**
(ışınım) birimindedir; katılımcı ortam içeri saçtığı **radyansı** ister. Dönüşüm **π**.

**Ölçüm:** bu projede bir kez ölçüldü — probe DC luminansı 0.156, sis rengi 0.492, oran
**3.15**. Aynı dönüşüm üç tüketiciye de uygulandı: froxel sisi, bulut ortamı, sis rengi.

**Kural:** yüzeyler `SampleSH(normalWS)` ile doğru birimi alır, ortamlar almaz. Yeni bir
ortam tüketicisi eklenirse π sorulur.

---

## Şafakta güneşten uzak bulutlar yeterince kararmıyor

**İlk şüpheliler — üçü de yanlış çıktı:**

- **Ortam ışığı fazı boğuyor.** Işık payı probu her saatte, her yönde doğrudan terimi
  baskın gösterdi. Yanlış.
- **Ton eşleme 24 katı sıkıştırıyor.** Yanlış — faz karışımı düzeltilince fark ekranda
  göründü, yani görüntüleme zinciri aralığı hiç kırpmıyormuş. *(Bu şüpheli bir ara
  `GameProfile`'daki `Tonemapping` bileşenine bakılarak "Neutral" sanılıp kâğıtta
  elendi; ölçüm yanlıştı — `LookController` her karede `TonemappingMode.ACES` yazıyor.
  Profil asset'i ne yazarsa yazsın, ton eşlemenin sahibi odur.)*
- **Güneş geçirgenliğinin tabanı** (`SunTransmittanceFloor`, HZD'de çok saçılmanın
  yerine geçen terim; portta çok saçılım oktavları zaten var, yani çift sayım).
  Anahtarla kapatıldı — ekranda neredeyse hiçbir şey değişmedi. Yanlış. *Sebebi: optik
  derinlik kodun yorumundaki örnekten (16) alınmıştı; gerçekte çok daha düşük, taban
  hiç devreye girmiyor.*

**Gerçek sebep:** `PHASE_LOBE_BLEND` 0.5. `lerp` iki lobu **ağırlıklı ortalıyor** ve
geri lob (`HG(−0.5)`) 90°'de ileri lobun **üç katı** — uzak alanı tek başına o ayakta
tutuyordu. Sayı Frostbite'ın brief'teki varsayılanıydı, bu sahnede hiç doğrulanmamıştı.

**Ayırt eden ölçüm:** durak konturu — bulutun kendi radyansını (kapsamaya bölerek) bir
duraklık bantlara ayırıp döngüsel renge basar. Deste 3 durağa sıkışmış ve **%70'i tek
bant** içinde çıktı. Aynı kontur birleştirmeden önce/sonra iki kez okundu: hava
perspektifi orta alanı ~1 durak kaldırıyor, gerisi ışın yürüyüşünün kendisinde.

**Kural:** faz parametreleri **bağlıdır**, tek tek ayarlanamaz. Karışımı düşürmek geri
lobun payını azaltır ama ileri lobunkini yükseltir — güneş çevresi 1.7 kat parladı ve
ayrıca karşılanması gerekti. İleri lobun eksantrikliği de kaldıraç değil: g düşünce tepe
az iner, lob genişler, uzak alan yükselir (0.60'ta 90° değeri düzeltmeden önceki
hâlinden bile parlak).

---

## "Dikdörtgen dağ", "kenarlar zirveyle neredeyse aynı yükseklikte"

**İlk şüpheli yanlıştı.** Sanılan: arazi küçük, dağ sığmıyor. Ölçüldü: kütle 1500 m
eşiğinde **379 km**'ye uzanıyor — bu bir SİLSİLE, hiçbir boyutta sığmaz. Büyütmek tek
başına kenar kesilmesini çözmüyor.

**Gerçek sebep iki katmanlı.** Birincisi maskedeki radyal kubbe 13 km'ye kadar açıktı,
yani 6 km'de hâlâ yarı yükseklikteydi; her azimutta aynı kota düşen bir zirve halkası
üretiyordu (4–8 km bandı zirveden yalnız **191 m** aşağıda). Kubbe 4 km'de bitirildi.
İkincisi ve asıl olan: karakter maskesine yalıtım halkası eklendi ama **`massif` listeye
alınmamıştı** — halka karakteri kırdı, kotu kırmadı, çünkü `massif` elev'i 3540 m.
Doğu yönünde 9–14 km bandı 3015 m'de kaldı. `massif` listeye alınınca kapandı.

**Ayırt eden ölçüm:** bant bant "zirveden kaç metre aşağıda". 4–6 km bandı 191 m → **1090 m**.

**Ve dış sönüm yarıçapa göre yapılmamalı.** Arazi kare; halka 15 km'de sönerse karenin
köşesi (15√2 = 21.2 km) halkanın dışında kalır ve orada silsile geri gelir. Chebyshev
mesafe (`max(|x|,|y|)`) kareyi izler.

Dört kenarın ortancası: 3873/3665 m → **214–389 m**. Kenar denetimi artık üretimde,
1200 m tavanı aşan kenar hata fırlatıyor.

## Ekranın ortasında siyah sivri iğne

**Sebep:** `MountainGenerator.FileCrests()` L1'e taşınırken alınmamıştı. Üçgenleştirme
+ gürültü, ızgaraya çapraz sırtlarda tek hücrelik iğneler bırakıyor: **5249 hücre**
komşusunun 400 m üstünde, en kötüsü 1343 m.

**İlk düzeltme yanlıştı ve ölçüm yakaladı.** C#'ın kör yumuşatması ("pencerede tek başına
yüksek olanı indir") aynen taşındı ve **zirveyi 5709 → 5696 m**'ye indirdi: o tanıma
GERÇEK zirve de giriyor. Filtre iğneyle tepeyi ayırt edemiyor.

**Ayırt eden büyüklük eğim.** İğneler 14.7 m'de 400–1343 m yükseliyor, yani 88°'den dik;
gerçek kaya yüzü 72°'yi aşmıyor. Ama eğim tavanı da tek başına yetmedi — zirve konisi son
15 metrede 70 m düşüyor (78°) ve **5608 m**'ye indi. Üçüncü ölçüt uydurulmadı: gürültünün
ve erozyonun zirveleri korumak için zaten kullandığı `uplift` alanına bağlandı.

Sonuç: sivri **5249 → 0**, zirve **5709.0 m tam**.

## Zirve spawn'dan görünmüyor — ama ölçüm yalan söylüyordu

Görüş hattı probu "+12 m KAPALI" dedi. Engelin yeri sorulunca **zirvenin kendisi** çıktı
(zirveden 0.00 km). Dünya eğriliği görüş hattını son metrelerde zirvenin kendi kotunun
altına indiriyor, prob da onu engel sayıyor.

Işının **son 150 m'si** dışarıda bırakılınca gerçek sonuç: açıklık **−24 m, GÖRÜNÜYOR**.

**Kural:** görüş hattı probunda hedefin kendi hücresi engel listesine girmemeli. Bu üçüncü
kez aracın yalan söylemesi; aşağıdaki bölüme bakılır.

## Güneşle dönen, hiçbir gölge anahtarının etkilemediği koyu lekeler

Kullanıcı: *"bu gölgeler ne ayak? havada bulut yok"*, sonra *"gölge olmaması gereken
yerde gölge var"*.

**Üç şüpheli de ölçümle elendi**, hiçbiri değildi:

| şüpheli | ölçüm |
|---|---|
| ufuk haritası kabalığı | 1024 vs 4097 uyuşmazlık %0.6–2 |
| ufuk haritası açısal (16 yön) | %0.5–0.8, alçak güneş dahil |
| normal dokusu kabalığı | 2048'de taraf değiştiren piksel %0.2–1.2 |
| bulut cookie'si | anahtar kapatılınca ekran değişmedi |

DepthNormals geçişinde vardı, ileri geçişte yoktu — üç geçişten yalnız biri ayrıktı.
Fonksiyonun kendi yorumu doğru davranışı zaten yazıyordu: *"harmanlamazsa siluet kabarır
ama ışık düz yüzeyi aydınlatır."*

**Ayırt eden ölçüm:** renk probu. Gölgelendirme normali "sırtı dönük" derken `ddx/ddy`
ile alınan gerçek yüzey normali güneşi görüyordu — o piksellere ayrı bir renk (mor)
verilince sınıf tek bakışta ayrıldı.

## Güneş tam karşıda ve yüksekken ayağının dibindeki yamaç gölgede

Kullanıcı: *"08:23'te gölge oluşması için hiçbir sebep yok ki."* Haklıydı.

**Sebep:** ufuk yürüyüşü ilk adımda komşu texel'i okuyor; eğimli yamaçta o komşu zaten
yukarıda ve engel sayılıyor. Ama **eğimli bir düzlemde iki koşul birebir aynıdır** —
"ufuk güneşten yüksek" ile "N·L ≤ 0". Yani yamacın kendisi hem ufuk haritasında hem
N·L'de sayılıyordu.

**Ölçüm** (azimut 200°, zirveden 6 km içinde):

| | değer |
|---|---|
| ufuk açısı ortancası | 16.5° |
| kendi eğimi çıkınca gerçek engel | **2.0°** |
| ufkun TAMAMI kendi eğimi olan nokta | **%46** |
| güneş 30°'de gölgede kalan yüzey | **%36 → %9** |

Çıkarma **açı uzayında** yapılır: eğimler tanjant, tanjant farkı açı farkı değil.

## Batımda sahne kahverengi-siyah, kontrast patlıyor

**Sebep:** `SunBlend`'in üst eşiği `sin(3°)`. Güneş 3°'nin üstündeyken ışık **hep tam
şiddette** — hava kütlesi sönümü yok. Gerçekte 3°'de doğrudan huzme zenit değerinin
%5–10'u, 10°'de %30, 40°'de %75.

`CurrentSunColor` sönümü zaten hesaplıyordu ama ışığa yazılmıyordu; gerekçe "soğurmanın
sahibi gökyüzü paketi"ydi. **Paket bir Unity directional light'ını söndüremez** — soğurma
göğe uygulanıyor, güneşe uygulanmıyordu. Gök sönerken arazi tam güneş almaya devam
ediyordu.

Düzeltmeden sonra 17:49'da güneş şiddeti 3.020 → **0.258**.

## Ayar dosyası düzenlemesi oyuna ulaşmıyor

Bir belirtinin **dört turu** boşa gitti çünkü `.asset` düzenlemelerinin çalışma zamanına
geçtiği doğrulanmadan ölçüm yapıldı. İki ayrı tuzak var:

- **Volume alanları çalışma zamanı KOPYASINDAN okunuyor.** `DebugMenu.cs` bunu zaten
  yazmış: `cloudVolume.profile` asset'in kendisi değil. Ayrıca `CloudWeatherDriver`
  `globalSpeed`, `globalOrientation`, `cloudCoverage` ve `densityMultiplier`'ı **her kare
  yazıyor** — o dördüne asset'ten dokunmak tamamen ölü.
- **`.hlsl` geçer, `.asset` geçmez.** Shader düzenlemeleri kendiliğinden derleniyor;
  asset düzenlemeleri Unity yeniden okumadan (Ctrl+R) görünmüyor.

**Kural:** ayar üzerinden ölçüm yapmadan önce YOL DOĞRULANIR — sürücünün dokunmadığı bir
alan seçilir ve görülmemesi imkânsız bir değer verilir. Yol testi için sürülen bir alanı
seçmek (bu turda `densityMultiplier`) testi baştan geçersiz kılar.

## Yeni dağda eski dağdan birebir aynı yerler

Kullanıcı: *"terrain specinden önceki dağda gördüğüm birebir aynı bir kaç yer var."*

**İlk cevabım yanlıştı** — "tanıdık gelen desendir, şekil değil" dedim. Şekildi.

**Ölçüm.** Araziye yükseklik yazan yalnız üç yer var: `HeightmapImporter` (tamamını ezer),
`RouteTerrainShaper` (menzil 70 m, yalnız yol ve doğuş düzlüğü), `MountainGenerator`
(ondan önce çalışır, üstüne yazılır). Yani taban geometri kesin yeni.

koordinatına bağlı, sabit hash'li. Yükseklik haritası değişti, bu değişmedi: aynı dünya
koordinatında aynı birikinti sırtı, aynı dalga, aynı yığın.

Aynısı yüzey deseninde de: `MountainBand`, oksit, liken, tanecik, kırılma — hepsi
`worldPos` anahtarlı.

**1/1780'i**. Küçük, ama yüzeye yakından bakılınca ekranın çoğunu kaplıyor — şikâyet
yerindeydi.

**Düzeltme:** `_PatternSeed`, İKİ HASH KÖKÜNE birden uygulanıyor (`MountainHash`,
kaydırmayı unutmak mümkün değil.

**Kural:** dağ baştan üretildiğinde `patternSeed` de artırılır. Geometri yenilenip boya
eski kalırsa oyuncu yeri tanır.

## Kod diskte doğru, ekranda eski — sessiz bayatlık ailesi

Aynı sınıftan **dört** tuzak ölçüldü. Belirti hep aynı ve aldatıcı: ölçüm doğru,
düzeltme doğru, ekran değişmiyor. Sonra düzeltme "işe yaramadı" sanılıp geri alınıyor
ve gerçek sebep bir tur daha kaçıyor.

| ne bayatlıyor | neden | çözüm |
|---|---|---|
| Yükseklik haritası PNG'si | `AssetDatabase` önbelleği, dışarıdan yazılan dosyayı fark etmiyor | zorla yeniden içe aktarma |
| `.asset` ayarları | Volume çalışma-zamanı kopyası; dışarıdan düzenleme sessizce geri alınıyor | değerleri KODDAN asset'e yazmak (`MountainSceneBootstrap.EnsureCloudVolume`) |
| Arazi menüsü | düğme yanlış işi yapıyordu, harita hiç uygulanmıyordu | düğmenin tam zinciri koşması |
| **`.hlsl` include'ları** | Unity shader'ın hangi include'u kullandığını **takip etmiyor** | `ShaderIncludeWatcher` — include değişince shader'ları yeniden içe aktarır |
| **Shader hatası log'da yok** | hata İÇE AKTARMADA değil, varyant ilk derlenirken çıkıyor; `Editor.log`'a bakıp "temiz" demek yanıltıyor | Play'e girip materyali kullandır ya da kullanıcıdan konsolu iste |

**Kural:** bir düzeltme ekranda görünmediğinde ilk soru "yanlış mı yaptım" değil,
**"çalışan sürüm gerçekten yeni mi"**. Yol testi önce: görülmemesi imkânsız bir değer
ver, ekranda gör, sonra gerçek ölçüme geç.

---

## Arazide düzenli testere — ÜÇ ayrı sebep, üçü de ölçümle bulundu

Kullanıcı: *"dağdaki bu düzenli testereleri törpüler misin"*, *"gölge değil, arazi
testereli"*, *"bazıları büyük bazıları ufak, aralarında metre farkı var"*, *"88 metreye
ayarladığında bile var"*, ve ayrıca *"mikro çıkıntılar hiç yok, detaylar çok düz"*.

Hepsi doğruydu. Aynı görüntünün arkasında üç ayrı sebep vardı; ikisi kapandı, biri
yöntemin sınırı.

### 1 — Menü düğmesi haritayı hiç uygulamıyordu (bir gün yaktı)

`Araziyi Yeniden Üret` yalnız `gen.Generate()` çağırıyordu: araziyi
`MountainGenerator`'ın kendi prosedürel çıktısıyla — eski radyal koni, terasları ve
gürültüsüyle — dolduruyordu. Yükseklik haritası uygulanmıyordu.

`Logs/tools.log`: harita araziye en son **13:44:53**'te ulaştı. Sonraki dokuz saatte altı
pişirme yapıldı (8.8 m, 22 m, 58 m, 88 m, törpü, koruma maskesi) ve **hiçbiri ekrana
çıkmadı**. Testere de zaten o jeneratörün terasıydı.

**Kullanıcı iki kez doğruyu söyledi, ikisinde de dinlenmedi:**
- *"eski dağ gibi birebir aynı yerleri görüyorum"* — prosedürel üretim aynı tohumla aynı
  sonucu verir, pişen harita her turda değişiyordu. Tek başına kanıttı.
- *"terrain fırçasıyla 10 saniyede düzeltirim"* — fırça **canlı araziye** yazıyor, yani
  o an çalışan tek yol oydu.

Düğme artık tam zinciri koşuyor: PNG zorla okunur → imza sıfırlanır → kurulum baştan üretir.

### 2 — Nyquist alias: asıl testere (14.7 m, her irtifada)

Hücre 7.324 m → ızgaranın taşıyabildiği en kısa dalga 14.65 m. `detail.multifractal`
320 m tabandan **8 oktav** istiyordu: 320, 160, 80, 40, 20, **10, 5, 2.5** m. Son üç
oktav sınırın altında ve geri katlanıyor — katlandıkları yer tam 2 hücre.

```
pismis haritada          80 m     14.7 m
  ZIRVE                  1.72x     3.48x
  ALT 11 km              1.02x     3.41x

sentetik, oktav sayisina gore (cell 7.324 m, taban 320 m)
  oktav 8 (en kisa 2.5 m)   2-hucre 1.57x
  oktav 7 (5.0 m)                   1.49x
  oktav 6 (10.0 m)                  1.09x
  oktav 5 (20.0 m)                  0.23x   <- ucurum burada
```

**Alias yumuşatılamaz** — pürüz değil, ızgaranın taşıyamadığı bir dalganın görünüşü.
Denenen üç filtre (eğim tavanı, Gauss harmanlama, tepe yuvarlama) hep *sonucu* siliyordu.
Sınır `detail.multifractal` içine kondu; çağıranın doğru sayıyı bilmesine güvenilmiyor.

**"Detay yok" şikâyeti aynı mekanizmadan.** Aliası bastırmak için konan `file_crests`
(koşulsuz Gauss, σ = 2 hücre = 14.6 m) tam o dalga boyunda çalışıyordu. Ölçüldü: sınır
açıkken torpu OLMADAN 2 hücre zaten 0.03x; torpu eklenince 25 m bandı 0.58x → **0.03x**.
Telafi terimi silindi (`file_crests`, `despike`, `round_crests`).

Bundan önceki törpü sürümü de kendi testeresini üretiyordu: `minimum_filter` ile çıkarma
yapıyordu, sivri gidiyor ama yerine filtre yarıçapı boyunda **düz faset** kalıyordu
(14.7 m enerjisi 3.42× — taban 1.87×).

### 3 — ~937 m, %200 fazla: YÖNTEMİN KENDİSİ, ÇÖZÜLMEDİ

Divide tree'nin ilkel birimi zirve; aradaki sırt düz çadır, siluet üçgen. Üç kaldıraç da
kapalı:

- **Zirve sayısı serbest değil** — `scalingFactor` gerçek Kirmse yoğunluğundan; 30×30 km'de
  83 zirve = 10.8 km²'de bir, Himalaya için gerçekçi.
- **Sırt kaydırması düzlemsel** (`ridgesPerturbation 0.15`) — yandan kıvrılıyor, kot düz.
- **Gürültü genliği spec sınırında** (§5.7). Taban 320 → 800 m denendi: 937 m fazlası
  3.02 → 2.86. Genliği büyütmek uydurma zirve doğurur.

Gerçek sırtlardaki ara tümsekler prominence tabanının (100 m) altında olduğu için
veritabanında yok. Kapatmanın yolu L2/L3 mesh modülleri.

### Yanlış elenen şüpheliler — hepsi ölçüldü, hepsi masumdu

| şüpheli | ölçüm |
|---|---|
| hücre ölçeğinde teras | 44639 farklı kot, düz komşu %1.11, ikinci fark 0.58 m |
| L0 zirveleri düzenli aralıklı | en yakın komşu değişim katsayısı 0.641 (rastgele 0.523) |
| üçgenleştirme fasetleri | düzlemsel hücre oranı %5-7 |
| kar yer değiştirmesi | kapatıldı, testere kaldı |
| üçgen ağı sıklığı | 45 → 25 m, crease metriği aynı |
| normal dokusu çözünürlüğü | 2048 → 4096 → 2048 |
| arazi LOD'u / `heightmapPixelError` | `maxLOD` 1, 88 m — değişmedi |
| gölge / aydınlatma | kullanıcı ölçtü: arazi testereli, gölge onu takip ediyor |

### Kurallar

- **"Düzenli tekrar" şikâyetinde ölçek sorulur** — aynı belirti farklı ölçeklerde farklı
  sorumludan gelir. Araç 2B güç spektrumu: fraktal taban düz bir doğru, sapan dalga boyu
  sorumluyu adıyla söyler.
- **Spektrumda taban doğrusu kesim bölgesini DIŞARIDA bırakan banda uydurulur.** İlk
  ölçüm tüm banda uydurdu; sonlu oktavlı gürültünün roll-off'u uydurmayı aşağı çekti ve
  olmayan bir tepe gösterdi ("zirvede 73-90 m'de 2.3x"). 30-600 m'ye sınırlanınca 1.72x'e
  düştü, alçakta 1.02x — yani yoktu.
- **Değişikliğin ekrana ulaştığı doğrulanmadan ölçüm yapılmaz.** Bu oturumda üç katmanda
  aynı tuzak: `.asset` (Volume çalışma zamanı kopyası), PNG (`AssetDatabase` önbelleği),
  menü (yanlış işi yapıyor). Yol testi önce: görülmemesi imkânsız bir değer verilir,
  ekranda görülür, sonra gerçek ölçüme geçilir.

## Teşhis aracının kendisi

Bu oturumda araç **iki kez yalan söyledi** ve ikisi de tur kaybettirdi.

- **`renk × 8` yetmedi.** "Tam sıfır" ile "çok küçük"ü ayıramadı; gökyüzü parlakken ortam
  ışığıyla aydınlanan zemin sekiz katıyla da ekranda siyah kalıyor. Ölçek logaritmik
  olmalı ve sıfır ayrı bir durum olarak işaretlenmeli.
- **Tek renkli denetim "kim uyguladı" diyemez.** Macenta "sis uygulandı mı" sorusunu
  cevaplıyor ama arazi de gök de macentaya döndüğü için sorumluyu göstermiyor. "Kim"
  sorusu için her yazıcı **kendi rengini** basmalı.

**Kural:** aracın çözünürlüğü, ayırmak istediğin iki hipotezi ayırabiliyor mu — önce bu
sorulur. Ayıramıyorsa araç yalan söyler ve tur katlar.

Üçüncü kez, arazi tarafında: **"ortalama eğim" tırmanılabilirliği ölçmez.** Oyun alanının
4–6 km bandı 48.7° ortanca verdi ve "duvar" diye okundu. Ama gerçek dağda da yüzler
50°+'dır; rota yüzden değil **sırttan** gider. Doğru araç en-az-maliyetli hat aramasıydı
(Dijkstra, maliyet `mesafe × (1 + (eğim/25)⁴)`): etekten zirveye **17.56 km** yol,
ortanca **18.1°**, teknik tırmanış **%0**. Duvar yoktu.

**Kural:** bir soruyu alan ortalamasıyla cevaplamadan önce sor — cevap bir **yol** mu,
bir **alan** mı? Yol soruluyorsa yol aranır.

Ve aynı turda dördüncü kez: ölçüm **koridorun içinde** yapılmalıydı, "zirveden >10 km"
diye yapıldı ve bütün yönleri kapsadı. Ova 20.8° çıktı; koridorla sınırlanınca 6.3°.
Yanlış maske, yanlış sayı.

Beşinci ve altıncı, aynı oturumda, ikisi de **ölçüm turu yaktı**:

- **İki prob arasında öncelik hatası.** Prob 1'in koşulu `> 0.5`, prob 2'ninki `> 1.5`
  ve prob 1 kodda önce geliyordu. Değer 2 olunca prob 1 de doğru çıkıp önce dönüyor,
  prob 2'ye hiç sıra gelmiyordu. Kullanıcı doğru kutuyu işaretledi, ekrana yanlış prob
  çıktı — ve ben iki tur "yanlış kutudasın" dedim. **Kural:** mod seçen bir anahtar
  aralıkla test edilir, eşikle değil.
- **Prob 2'nin bantları deniz seviyesine göre yazılmıştı.** "Kar için güneş-gölge farkı
  2–3.5 diyafram" doğru bir sayı, ama 4900 m'de değil; orada gerçek değer ~3.8. Alet
  doğru araziye "fazla koyu" dedi. **Kural:** fiziksel referans alınırken hangi koşulda
  ölçüldüğü de yazılır — irtifa, hava, yüzey.

ekran üstü değişim hızını karşılaştırıp en hızlısını basan bir prob yazıldı; sınırda
"kot rampası" çıktı ve bu trivial olarak doğruydu — geçişte zaten değişen büyüklük her
zaman kazanır. Cevap mutlak bantlardan geldi: `cover` eşit aralıklı renklere bölününce
ortalamanın yumuşak, varyansın yüksek olduğu tek bakışta görüldü.

**Kural:** "hangisi" sorusu karşılaştırmayla, "ne kadar" sorusu mutlak ölçekle sorulur.
Sertlik bir *ne kadar* sorusudur.

Prob sonuç vermiyorsa sıradaki araç **Unity Frame Debugger**: hangi geçişin ekrana ne
yazdığını kesin gösterir. Yalnız kamera renk tamponuna yazan adımlara bakılır; motion
vector, gölge haritası ve ara doku adımları tuhaf görünür, normaldir.

## "Ekranın tamamı grenli, gökyüzü dahil" — desenin DC bileşeni

Kullanıcı yağış perdesini yalnız başına görmek için tanecikleri kapattı ve şunu dedi:
*"bu görüntü normal mi? f1'den yağmur ve karı kapattım net olarak görebilmek için."*
Ekranın her yeri — gökyüzü dahil — ince, düzgün bir grenle kaplıydı.

**Yanlış çıkan ilk şüpheli: mesafe.** "Uzaktaki tane piksel altı kalır, perde mesafeyle
sönmeli" diye üstel bir mesafe kapısı kondu (`textureRange = 260 m`). Sonuç: perde ufka
yapışık İNCE BİR ŞERİDE düştü. Geometrik olarak doğruydu — düz ovada 50–600 m aralığı
ekranda dar bir kuşağa sıkışır, perspektif mesafeyi ezer — ama işe yaramazdı, ve asıl
sorunu hiç ele almıyordu.

**Gerçek sebep: desenin ortalaması.** Pişirici deseni ortalaması 0.5 olacak şekilde
`[0,1]`'e eşliyor (`[Langer 2004, §7.7]`). O ortalama DOĞRUDAN opaklık olarak
kullanılıyordu, yani perde ekranın tamamına sabit bir gri sürüyordu — tam karda ~0.45.
Kullanıcının "gren" dediği şey tanelerin dokusu değil, desenin **DC bileşeniydi**.

Tane seyrek ve ayrıktır, aradaki hava saydamdır. Ortalamanın altı sıfıra iniyor, üstü
geriliyor: `alpha = saturate((a - 0.5) / 0.5)`. Sihirli katsayı yok — 0.5 pişiricinin
yazdığı ortalama.

**Ayırt eden ölçüm: opaklık probu** (son alfayı ayrık renk bantlarına basan görünüm).
Önce ekranın tamamı 0.32–0.50 bandındaydı; sonra 0.00–0.02 tabanı üstünde seyrek
0.08–0.32 tepeleri. Göz kararıyla "biraz azalttım" ile "doğru yere oturdu" ayrılamazdı.

**Aynı turda ikinci belirti: benekler kirli koyu okunuyordu.** 235 m görüşte gök tamamen
sis rengindeyken tanecikler gökten belirgin koyu düşüyordu. Sebep: perde `AirColor`'a

Bunun şüpheli araması hiç gerekmedi: kural **zaten yazılıydı**, `HeightFog.hlsl:248` ve
`:611`, ikisi de "havada asılı tane gök rengine boyanmaz" diyor. Yeni bir görsel mevcut
bir büyüklüğe bağlanırken o büyüklüğün fiziksel karşılığı okunmadı.

## "Damlalar yere çarpıp sekiyor sanki dolu yağıyor gibi" / "yatayda hareket eden damlalar var"

**İlk şüpheliler — üçü de yanlış çıktı.** Yere yakın, küçük ve hareketli üç şey vardı ve
(tanecikleri tam zeminde, yatay akıyor), yağan kar (çırpınıyor). Hepsi tek seferde F1
anahtarı olarak kondu; **üçü de kapatıldığında sekme sürdü.**

**Gerçek sebep iki katmanlıydı.**

Birincisi iz boyu: `rainLength` ve saydamlık `TerminalVelocity(r)` okuyordu, yani rüzgârı
hiç görmüyordu. İkisi de pozlama boyunca SÜPÜRÜLEN YOLDAN çıkar. Rüzgâr 8.5 m/s'de ince
damla 3.4 cm çiziliyordu, gerçek yol 14.6 cm — 4.3 kat kısa. Kısa + yatık = çizgi değil
tanecik, yani "dolu".

İkincisi rüzgârın kendisi: şiddet → hız eşlemesi doğrusaldı ve 0.57 şiddet doğrudan
8.5 m/s (Beaufort 5) veriyordu. O rüzgârda 1 mm damla yataydan 25° iner — fizik doğru,
rüzgâr fazla. Eşleme kareye alındı (`WindField.ShapeSeverity`), uçlar sabit kaldı.

**Ayırt eden ölçüm: ELEME, renk değil.** Yörünge açısı önce yedi renk bandına basıldı ve
kullanıcı ayıramadı ("gözüm seçmiyor, hepsi birbirine benziyor"). Renk bırakılıp eleme
kondu — ekranda yalnız 20°'den yatık, sonra yalnız 20°'den dik damlalar çizildi. Cevap tek
turda geldi. Ardından CPU tarafında hız vektörünün iki bileşeni (rüzgâr sürüklenmesi /
düşme) ayrı ayrı kapatıldı: **rüzgâr kapatılınca yatay hareket bitti.**

**Aracın kendisi iki kez yalan söyleyecekti.** (1) İzolasyon anahtarı `UpdateStreaks`
içinde bağlanıyordu, o metodun önünde dört erken çıkış var — biri tutsa uniform hiç
yazılmayacak, HLSL varsayılanı (0,0,0), yani "hepsi kapalı" görünecekti. (2) `debugScale`
BÜTÜN prob kiplerinde 40× büyütüyordu; "tür" probu 40 kat büyütülmüş şeritler gösterdi ve
ölçtüğü geometriyi bozdu. Teşhis aracı önce doğrulanır.


## "kar yağmıyor" — VFX bağlıydı, parçacık doğuyordu, ekranda hiçbir şey yoktu

**İlk şüpheli yanlış çıktı: sıcaklık.** Oyuncu 206 m'de, ölçülen +6 °C, donma
seviyesi deniz seviyesinin altında — "sıcak olduğu için yağmur yağıyor, kar
değil" diye okundu. Sıcaklık kapısı kaldırıldıktan **sonra da** kar yağmadı.

**İkinci şüpheli de yanlış: Play donuktu.** `runInBackground: 0` yüzünden Unity
odaksızken `Update` koşmuyordu; ölçülen bütün sıfırlar bayattı. Açıldı, tick
geldi, zincir uçtan uca doğrulandı — `Precipitation 1 → IsSnowing → NearRate
16000 → SpawnRate 16000 → alive 39892`. **Kar hâlâ görünmüyordu.**

**Gerçek sebep sınır kutusuydu.** `VFXBasicInitialize.bounds` varsayılanı
1 m³. Unity o kutuyu frustum'a göre kırpıyor: `VFXRenderer.isVisible` false,
sistem hiç çizilmiyor. 39892 parçacık vardı ve hiçbiri ekrana gelmiyordu.
`SnowVfxBuilder` beş grafiğin hiçbirine bounds yazmamıştı.

**Ayırt eden ölçüm:** `Renderer.bounds.size` = (1, 1, 1) ile `culled: true`
yan yana. Zincirin her adımı doğru sayı veriyordu; yalnız son adım — çizim —
sessizce atlanıyordu. Zinciri sonuna kadar okumak, "veri akıyor demek ki
çalışıyor" varsayımını kırdı.

**Yan bulgu: `Unity_RunCommand` Play'i düşürüyor.** İlk çağrı C# derliyor,
derleme domain reload tetikliyor, Play çıkıyor. Play modunda ölçüm alırken
ölçüm aracının ölçtüğü şeyi öldürdüğü fark edilene kadar iki tur yandı.


## "kar yukarı doğru yağıyor" / "yukarı aşağı sağa sola hareket ediyor"

**Sebep türbülansın kip'iydi, şiddeti değil.** `Block.Turbulence` `Relative`
modda hızı bir HEDEFE ÇEKİYOR (`velocity += (hedef − velocity) * drag * dt`);
hedef türbülans alanı, ortalaması sıfır. Başlangıçta yazılan −0.6…−1.4 m/s
terminal hız bir saniyede yeniyor, geriye rastgele savrulma kalıyor.

**Çözüm hızı dayatmak değil, fizikten çıkarmak oldu.** Yerçekimi (−9.81) +
sürükleme (9.81) dengesi terminal hızı kendisi veriyor: `v = g/drag = 1 m/s`,
spec §17.1'in kuru kar için istediği 0.6–1.4 bandının ortası. Türbülans
`Absolute` moda alındı — kuvvet ekliyor, hızı ezmiyor. Yüksek sürükleme aynı
zamanda karın NEDEN savrulduğunu açıklıyor: hafif tane rüzgâr hızına hızla
yaklaşır. İki davranış tek katsayıdan.

**Ayırt eden ölçüm — üçüncü araçta bulundu.** İlk iki araç yalan söyledi:

1. Ekran görüntüsündeki parlak nokta sayımı. Tespit eşiği gökyüzü/dağ
   kontrastına bağlı, tanelerin ancak %13'ünü buluyordu; üstelik spawn
   kesilince taneler aynı anda ölüyor (39970 → 5723) ve dağılımı kaydırıyordu.
   Ortalama y "yukarı" çıktı — gerçeğin tersi.
2. `cam.Render()` ile manuel yakalama. URP'nin gökyüzü ve post adımlarını
   atlıyor, sahne kapkara geliyor. Parlaklık yargısı için kullanılamaz.

Çalışan araç: `boundsMode = Automatic`. Unity gerçek parçacık kutusunu her
frame hesaplayıp `Renderer.bounds`'a yazıyor — ışıktan, ölümden ve gözden
bağımsız bir sayı. Ölçüm:

    Spawn kutusu        205,0 … 231,0 m   (merkez 218, yükseklik 26)
    Gerçek parçacıklar  197,1 … 230,9 m   (merkez 214, yükseklik 33,8)

Taneler kutunun **7,9 m altına** inmiş, üst sınır spawn sınırında kalmış.
Hareket tek yönlü. Terminal 1 m/s × max ömür 9 s = 9 m; ölçülen 7,9 m tutarlı.
Prob ölçümden sonra kaldırıldı.


## "uzakta siyah tanecikler hareket ediyor"

**Kar taneleri siyah çiziliyordu.** `Orient: Face Camera Plane` normali kameraya
çeviriyor; güneş yandan gelince `N·L ≈ 0` ve Lit quad kararıyor. Gerçek kar
tanesi çok saçıcı — ışığı her yöne dağıtır, tek yönlü diffuse ile
modellenemez. Spec §17.1 bunu emissive ile karşılıyor; builder emissive'i hiç
yazmamıştı.

**Aynı ekranda ikinci sapma: "yakınımda kar yok".** `ScreenSpaceSize` bloğu
`PixelAbsolute` modda boyutu SABİTLİYOR — yakındaki tane de uzaktaki de tam
1.3 piksel. Spec `size = max(size, minWorld)` istiyor, yani 1.3 piksel TABAN.
Bloğun hiçbir modu bunu vermiyor (`PixelAbsolute`,
`PixelRelativeToResolution`, `RatioRelativeTo*` — paket kaynağından okundu).
Blok çıkarıldı; asgari boyut ayrı iş, `DECISIONS.md`'de.


## `SetSlot` "yazdım" dedi, asset sıfır kaldı

Kar tanesine terminal hız yazıldı, log doğruladı, `.vfx` dosyasında
`{"vector":{"x":0,"y":0,"z":0}}` duruyordu. `velocity` slotu `Vector3` değil
`UnityEditor.VFX.Vector` sarmalayıcısı; `PropertyInfo.SetValue` tip
uyuşmazlığında varsayılanı bırakıp sessizce dönüyor.

**Düzeltme yalnız o çağrı değil, yardımcının kendisi oldu.** `SetSlot` artık
yazdığını geri okuyup karşılaştırıyor, eşleşmezse hangi tipin beklendiğini
söyleyerek fırlatıyor. Sessiz düşüş bir kez bulundu; ikincisini araç yakalar.


## "uzaktaki kar kayboluyor" — iki ayrı sebep, ikisi de ölçüldü

**Birincisi asgari ekran boyutunun hiç olmamasıydı.** 20 metredeki 2 cm'lik tane
ekranda yarım pikselin altına düşüyor ve kayboluyor. Spec §17.1 bunu ayrıca
uyarıyor. `ScreenSpaceSize` bloğu denendi ve YANLIŞ çıktı: hiçbir modu asgari
değil, `PixelAbsolute` boyutu sabitliyor — yakındaki tane de 1.3 piksele
kilitlendi, kar toz gibi göründü. Çözüm `CustomHLSL` bloğu: aynı paketin kendi
formülü, atama yerine `max`.

**İkincisi uzak katmanın hiç yazılmamış olmasıydı.** `SnowfallLayers.farLayer`
alanı `SnowCurtainController` tipindeydi ve HİÇ KULLANILMIYORDU; üstelik yanlış
sistemi gösteriyordu — o §18.7'nin savrulma perdeleri (tetik rüzgâr), §17.2'nin
yağış perdeleri değil. `DECISIONS.md`'deki "mevcut shader bu davranışı birebir
yapıyor" kaydı bu ikisini karıştırmıştı.

**Yeni katman kurulduğunda da görünmedi — üçüncü sebep sisti.** `FogDensity01`
görüş mesafesinde doğrusal eşleniyordu ve 1150 metrede **0.95** veriyordu.
Perde alpha'sı `1 − fog * 0.6` ile 0.10'dan 0.043'e düşüyordu. Eşleme
sönümlemeye çevrildi (Koschmieder, `σ = 1/V`); aynı görüş artık 0.05 veriyor.

**Ayırt eden ölçüm — "hiç çizilmiyor" mu "çok soluk" mu.** Perde açık ve kapalı
iki kare alındı, yakın katman susturuldu, piksel farkı ölçüldü: ekranın %36.8'i
etkileniyor, en büyük fark 100/255, ortalama 1.05/255. Yani çiziliyordu ve
zayıftı. Göz kararıyla ikisi ayrılamazdı — ekranda ikisi de "yok" görünüyor.


## Sahne kurulumu Play modunda çalıştırıldı ve sessizce yarım kaldı

Play'de eklenen bileşenler ve bağlar Play çıkınca siliniyor; sahne dosyasına hiç
yazılmıyor. Bir tur boyunca "VFX bağlandı" görüldü, Play kapandı, sahnede
`VisualEffect` referansı sıfır kaldı ve kar yağmadı.

İkinci kez `MarkSceneDirty` "This cannot be used during play mode" fırlattı —
ama kurulumun SONUNDA, o noktaya kadar yarım iş yapılmıştı.

**Kapı en başa kondu:** `SetupScene` artık `EditorApplication.isPlaying` ise
hemen hata verip çıkıyor.


## "kar tutmuyor" — yağış sıcaklıktan koparılmıştı, ERİME kopmamıştı

Kar yağıyordu, `SnowfallSWERate` doğruydu (1.39e-6 m/s = spec'in 5 mm/saat'i),
zemin birikmesi neredeyse durmuştu: 45 saniyede **3.6e-9 m/s**.

**İlk şüpheli yanlış çıktı: gökyüzü görünürlüğü.** `fall = SWERate * dt * skyVis`
ve kâğıtta ters hesap `skyVis ≈ 0.16` gerektiriyordu. Doku okundu: `occlY`
her tekselde −9999, yani engel yok, `skyVis = 1`. Şüpheli elendi.

**Gerçek sebep derece-gün erimesiydi.** Dağın eteğinde hava **+4.8 °C**
çıkıyordu; `melt = 4.63e-8 * max(0, T)` yağışın %98'ini yiyordu. Ayırt eden
ölçüm: `SnowEnvironmentBridge.temperature` referansı Play'de null yapıldı
(sıcaklık manuel −4 °C'ye düştü), aynı ölçüm tekrarlandı:

    +4.8 °C : 3.6e-9  m/s
    −4.0 °C : 1.10e-7 m/s      otuz kat

**Düzeltme sıcaklık modelini bozmadan yapıldı:** `seaLevelCelsius` +7.8 → −2.
Kar çizgisi ve yağış-sıcaklık bağı kaldırıldığı için oyun "her kotta kar tutar"
diyor; sıcaklık modeli de buna uymalı. 206 m'de −1.7 °C, zirvede −42.8 °C.

**Ölçüm aracının sınırı:** `MeanSwe` 64² indirgenmiş durumdan otuz karede bir
okunuyor ve kısa pencerelerde gürültülü — rüzgâr kapatıldığında hız beklenenin
tersine düştü. Otuz katlık fark güvenilir, %20'lik farklar değil.


## "yürürken iz kalmıyor" — sahnede tek bir deformer yoktu

`SnowDeformer` sayısı sıfırdı. Yakalama pass'i çalışıyordu, kar birikiyordu,
iz bırakacak hiçbir nesne yoktu. Spec §1.4 karakter proxy'lerini "ayrı ayrı
onay al" diye bekletiyor; test istendiğinde kuruldu.

**Ölçüm aracı bir kez yalan söyledi.** `RT_Capture` `ARGBHalf`; ilk okuma
`GetData<float>` ile yapıldı ve "max 0.0078, ayak altında ≈ 0" verdi — yani
"yakalama çalışmıyor". Doğru tiple (`RGBAFloat` dönüşümü) okununca:

    maskeli teksel 114     R = −0.03 (ayak alt yüzeyi, gözlemciye göre)

114 teksel iki ayağın alanına birebir uyuyor (2 × 0.11 × 0.28 m² / 0.0234 m²).
Trail dokusunda `carve max 1.08 mm`, 113 teksel. Zincir baştan sona çalışıyordu.

**`Renderer.isVisible` bu iş için ölçüt değil** — birinci şahısta ayak zaten
kameraya görünmüyor, `False` okumak yakalamanın çalışmadığı anlamına gelmiyor.


## "kar yağışı rüzgârdan etkilenmiyor"

Grafikte rüzgâr terimi HİÇ YOKTU. Türbülans vardı (ortalaması sıfır, savuruyor
ama taşımıyor), spec §17.1'in `Hız = _WindWS + (0, −terminalVel, 0)` terimi
yoktu; kar 13 m/s rüzgârda bile dimdik iniyordu.

Rüzgâr HIZ olarak değil KUVVET olarak verildi: aşağıdaki sürükleme zaten hızı
sıfıra çekiyor, `F = wind × drag` dengesi tam `velocity = wind` veriyor ve düşey
eksende yerçekimi bozulmuyor. Ölçüldü: `WindForce` beklenen değere birebir eşit,
`F / drag = 13.97 m/s` = tam rüzgâr hızı.


## "beyaz örtü geziyor, kâğıt gibi incecik, derinliği yok"

Suçlu elemeyle bulundu: `SnowCurtainController` (§18.7 savrulma perdeleri).
Kapatılınca örtü tamamen gitti; benim eklediğim §17.2 yağış perdeleri değildi.

**İki sebep birlikte.** Dokusu tek bir düşük frekanslı fbm'di — yumuşak gri bir
bulut, hiç tanecik yok. Ve `_NearFade` 4 m'ydi; perde 12–25 m genişliğinde,
10 m ötede bile ekranın yarısını kaplayıp düz bir levha gibi duruyor.

Doku yeniden üretildi (akış şeridi × damgalanmış tanecik) ve `_NearFade` 18 m'ye
çıkarıldı. İkinci sürümde ikinci bir `TilingFbm(u*6, v*6)` denendi ve gözle
görülür düzenli bir IZGARA çıkardı — fbm yüksek frekansta tekrar ediyor;
üçüncü sürüm taneleri tek tek damgalıyor.


## Kar sistemi AY IŞIĞINA bağlanmıştı

`SnowEnvironmentBridge.Sun` = **Moon Light**, `intensity = 0` — tam gündüzde.
Tane emissive'i sıfır çıkıyordu.

Sebep `FindSun()`'ın "ilk aktif directional light"ı almasıydı. Sahnede ÜÇ tane
var (Directional Light 2.7, Moon Light 0, Lightning 0) ve tarama sırası ayı
önce buldu. Ana ışık artık `TimeOfDay`'in `sun` alanından soruluyor — gündöngü
hangisinin güneş olduğunu zaten biliyor.

**Kar aydınlatmasının geri kalanı temiz çıktı:** `SnowLitForwardPass` ana ışık +
gölge + ek ışıklar + `SampleSH` ambient okuyor, parıltı `_SunElevation01` ile
kapılı, savrulma ve yağış perdeleri `GetMainLight()` kullanıyor.


## VFX zemin kesmesi karın TAMAMINI sildi — iki kez

Spec §17.1 `if (position.y < groundHeight + 0.02) alive = false` istiyor.
Eklendi ve `aliveParticleCount` sıfıra düştü. Üç sürüm:

1. `groundY` olarak `followTarget` (KAMERA) yollandı. Kamera göz hizasında,
   zeminden 1.65 m yukarıda; kesme düzlemi oraya çıkınca her tane daha
   havadayken ölüyordu.
2. Kot oyuncunun ayağından alındı ama `attributes.position` VFX'in YEREL
   uzayında (±10) ve dünya kotu 205 ile karşılaştırılıyordu — koşul her tane
   için doğru.
3. `TransformPositionVFXToWorld` eklendi; sonuç yine sıfırın altında çıktı.
   Ayırt eden ölçüm: `groundY = 0` yazıldı, `alive` yine 0 — yani dönüşüm
   ne yerel ne dünya, güvenilmez.

Çözüm: dönüşüm kaldırıldı, kot C# tarafında yerele çevrildi
(`zeminKotu − kutuKonumu`). İkisi de orada dünya koordinatı olarak biliniyor.
Ölçüldü: `GroundY = −12.46` = beklenen yerel kot, `alive = 39915`.

**Ayırt eden araç:** `GroundY = −99999` yazmak. Kesme etkisiz kalınca `alive`
39902'ye fırladı — sorunun kesme düzleminde olduğu tek turda kesinleşti.


## Kar tanesi yoğunluğu: kutuyu küçültmek TERS tepti

"Yağış 1 iken yeterli kar göremiyorum." Kâğıtta sebep açıktı: spec §17.1'in
kapasite 40000 + kutu (40,26,40) birleşimi `0.96` tane/m³ veriyor, gerçek yoğun
kar 3–10 tane/m³.

Yoğunluk `kapasite / hacim` olduğu için önce kutu küçültüldü — (24,20,24) sonra
(20,16,20). Kâğıtta yoğunluk 3.5 ve 6.2'ye çıktı; **ekranda kar AZALDI.**
Sebep: rüzgâr 12 m/s'de tane 10 metreyi 0.85 saniyede geçiyor, dar kutuda
kameranın çevresinde hiç kalmıyor. Spec'in geniş kutusu tam bunun için.

Kutu spec'e döndürüldü, yoğunluk kapasiteden alındı (120000). Ölçüldü:
`alive 89067`, 119 FPS.

**Ayrıca doku değişince parlaklık yeniden kalibre edildi.** `DefaultDot` tam
daireydi; 4×4 kar tanesi atlası dallı ve boşluklu, aynı ekran alanında daha az
piksel dolduruyor. Gökyüzü bölgesinde en parlak piksel 222 → 193'e düşmüştü,
emissive ölçeği 1.0 → 1.6 ile 225'e döndü, hiçbir piksel doymadı.


## `fix.md` denetimi — 16 iddianın 6'sı doğrulandı, 3'ü geçersiz çıktı

Kullanıcı bir analiz raporu verdi ve "ezbere hareket etme, önce doğrula" dedi.
Her madde kodda arandı.

**Doğrulanan ve düzeltilenler:**

*Yağmur sesi kar yağarken çalıyordu.* `WeatherAudio.DriveRain` ham
`weather.Precipitation` okuyordu — ne `RainWeight01` çarpanı ne de
`PrecipitationRenderer`'ın 0.05 kesme eşiği. Kar sistemi yağmuru görselde
kapattığında ses açık kaldı; rapor bunu "hafif yağmurda ses var görüntü yok"
diye yakalamış, kar tarafı devreye girince tam bir tutarsızlığa dönüşmüş.

*Gece ana ışık aya devredilmiyordu.* `RenderSettings.sun` hep güneşe
sabitlenmişti. Sabitlemenin kendisi bilinçliydi (şimşek güneşten parlak olduğu
için Unity ana ışığı bir kareliğine ona kaptırıyordu) ama hangisine
sabitlendiği günün saatinden bağımsızdı. Ölçüldü: saat 0.00'da ana ışık artık
`Moon Light`, gündüz `Directional Light`.

*Alpenglow'da arazi kendi gölgesini kapatıyordu.* `TerrainSunShadow` güneş
ufka değdiği an `1.0` dönüyordu; gün batımından sonra vadi dipleri sırtlarla
aynı parlaklıkta kalıyordu. Sınır ufkun 0.035 altına çekildi ve arada gölge
yumuşakça bırakılıyor.

*Göz adaptasyonu tek karede sıçrıyordu.* `LookController` `adapt`i doğrudan
yazıyordu. Asimetrik üstel yumuşatma kondu — karanlığa açılma 2.5 s, aydınlığa
kısılma 0.5 s; insan gözünde ikisi aynı hızda değil.

*`_TerrainShadowReceive` `DebugMenu`'ye bağımlıydı.* Global yalnız
`DebugMenu.Update()`'te yazılıyordu; panel sahnede yoksa Unity globali sıfır
başlar ve **arazi tamamen gölgesiz** çizilir. Anahtar `_TerrainShadowOff`'a
çevrildi: kimse yazmazsa 0 kalır, 0 da "kapatma" demek — varsayılan artık
doğru tarafa düşüyor.

*Sıcaklıkta termal eylemsizlik yoktu.* `daytimeWarming * DayFactor` anındaydı;
güneş ufka değdiği saniyede hava birkaç derece zıplıyordu. Gerçekte günün en
soğuk anı gün doğumudur. `DayFactor` 45 dakikalık bir gecikmeyle izleniyor.

**Geçersiz çıkanlar — iddia kodla uyuşmadı:**

*Bulut tabanı gecikmesi.* Rapor `activeCloudBottom`'ın görsel bulut tabanı
olduğunu varsayıyor. Kodun kendi yorumu aksini söylüyor: "`CloudBottom` ve
`CloudTop` KALDIRILDI, silinen bulut sistemine aitti"; gerçek kotlar
`CloudLayerProbe`'dan geliyor. Madde eski mimariye ait.

*Bulut gölgesi çifte kararma.* Rapor "ortam ışığı sıfıra yaklaşır" diyor.
Kodda cookie yalnız `mainLight.color`'ı çarpıyor; ambient ayrı kanaldan
geliyor ve yorumda bu açıkça yazılı.

*Şimşek sesi `timeScale` desenkronu.* Rapor "unscaled yerine simülasyon zamanı
kullanılmalı" diyor; `ThunderPlayer` zaten `Time.deltaTime` kullanıyor.

*SkyFog silüet konturu.* Rapor `ZTest Equal`'ın ufuk çizgisinde tek piksellik
kontur bıraktığını söylüyor. Kod bunu **zaten çözmüş** ve çözümü yorumda
anlatmış: derinlik TAMPONU ile derinlik DOKUSU silüet pikselinde ayrışıyordu,
şimdi ikinci bir kapı var (`SampleSceneDepth` uzak düzlem değilse `discard`).
Rapor eski bir sürümü analiz etmiş.

*Ufuk haritası sınırında gölge kaybı.* Rapor kenar kelepçeleme istiyor. Kodda
`return 1.0` bilinçli ve gerekçesi yorumda: pişirilmiş arazi orada bitiyor,
güneşi kesecek kütle de yok, doğru cevap "engel yok". Aksi denenmiş ve ovada
zemini simsiyah yapmış.

**Yapılamayan:** bisikletin zemin direnci (`RollingResistance`). Alan gerçekten
boşta duruyor ama `TerrainSurface`'te zemin TİPİ API'si yok (`WindWeightAt` ve
`SlopeAt` var). Önce zemin sınıflandırması gerekiyor — `DECISIONS.md`.

---

## "Etrafta kâğıt gibi incecik beyaz örtü geziyor, bir derinliği yok"

Kullanıcı üç kez bildirdi, üç turda üç ayrı sistem suçlandı. **İkisi de doğruydu —
iki ayrı kusur aynı belirtiyi veriyordu.**

**Elenen şüpheliler — hepsi ölçümle:**

| Şüpheli | Nasıl elendi |
|---|---|
| `VFX_SnowCurtain` | Kapatıldı, örtü kaldı |
| `VFX_Spindrift` | Kapatıldı, örtü kaldı |
| Sıfır hızda `Orient: AlongVelocity` (normalize(0)) | `WindForce` sıfırdan (48,0,20)'ye çıkarıldı, dikdörtgen yerinde kaldı |
| Tek bir dev quad | Spawn 25'e indirildi, 142 tane kaldı — dev quad YOK |

**Gerçek sebep, iki tane:**

**1. Asgari ekran boyutu alfayı kısmıyordu.** Tane 1.3 piksele çekilirken kapladığı
alan büyüyor, alfa sabit kalıyordu. Ölçüm: `px/rad = 769`, taban 1.8 cm tane
**10.6 m**'den sonra piksel altına düşüyor; 20 m'de alan ×3.5, 40 m'de ×14.
Kutu 40×26×40, hacminin çoğu 10.6 m'nin ötesinde — 89 bin tane üst üste binip
süt gibi bir örtü çıkarıyordu. Keskin dikdörtgen kenar örtünün değil, **spawn
kutusunun duvarı**.

**2. `SnowCurtainController`, VFX değil.** İkinci kusur hiç VFX değildi: compute
ile sürülen, `Graphics.RenderPrimitives` ile çizilen 14 quad. Tüm `VisualEffect`
bileşenleri kapatılınca dikdörtgenler EKRANDA KALDI — belirti bu ölçümle
sahibini buldu.

**Ayırt eden ölçüm:** kamerayı 180° döndürmek. Kusur yalnız belirli açılarda
görünüyordu; tek açıya bakan üç tur onu kaçırdı.

## "Yağış 1 kar 1 iken ekranda yağmur izleri var"

**İlk şüpheli (yanlış):** `RainWeight01` hesaplanmıyor. Ölçüldü: `RainWeight01 = 0`,
doğru değer.

**Gerçek sebep:** yoğunluk `WeatherState.Changed` OLAYINDA bir kez hesaplanıyordu.
Girdilerinden biri `SnowRuntimeState.RainWeight01` ve o kar oranı sürgüsüyle
değişiyor — sürgü hava olayı yayınlamıyor. Yağmur son olaydaki yoğunlukta donup
kalıyordu. `RefreshDensity()` artık `Update`'te.

## "Yürürken kar tanecikleri çok hızlı yer değiştiriyor, sürekli yeniden render oluyor gibi"

**Sebep:** VFX sistemleri YEREL uzaydaydı (`VFXDataParticle.m_Space: 0`).
Yerel uzayda parçacık konumu objeye göre tutuluyor; yağış kutusu oyuncuyu 1 m
ızgarasında takip ettiği için her snap yaşayan 89 bin taneyi birlikte
ışınlıyordu. Yürüme hızında saniyede birkaç kez.

**Ayırt eden ölçüm:** `timeScale = 0` + `SnowfallLayers` kapalı, kare yakala,
kutuyu kaydır, tekrar yakala. Dünya uzayında 30 m kaydırma ekranın yalnız
%0.16'sını değiştirdi ve ortalama parlaklık sabit kaldı (61.9 → 61.8) — yerel
uzayda bütün kar ekrandan çıkardı.

**Snap suçlu değildi.** İlk akla gelen ızgarayı kaldırmak; o gerekçesi geçerli
(spawn deseninin kameranın peşinden sürüklenmesini önlüyor). Işınlanma
snap'ten değil UZAYDAN geliyordu.

## "Rüzgâr 0 ama kar belirli bir yöne yağıyor — yürürken düzeliyor, dururken rüzgâr varmış gibi"

**Gerçek sebep: rüzgâr GERÇEKTEN var.** Panel sürgüsü hızı değil ŞİDDETİ
sürüyor; şiddet 0 → `WindSettings.calmSpeed`. Ölçüm: `WindSpeed = 1.80 m/s`,
`WindForce = (17.54, 0, −0.47)` → `17.54 / 9.81 = 1.79 m/s`, tam +x. HUD da
dürüst yazıyor: "Hız 2,0 m/s". Sakin hava sıfır hava değil.

**Çözüm:** `calmSpeed` 2.0 → 0.6 m/s (Beaufort 1). Taneyi dikeyden 63° yerine
31° yatırıyor. Bulut katmanı kendi tabanını aldı, yoksa sakin günde gökyüzü
donuyordu — gerekçe `RATIONALE.md`.

**Yürürken neden düzeliyor:** yürüme hızı 2.2 m/s, koşma 4 m/s. Kendi hareketin
1.8 m/s'lik sürüklenmeyle aynı büyüklükte, o yüzden bağıl hareket baskın çıkıyor
ve sürüklenme göze batmıyor. Dururken tek hareket o.

**İlk şüpheli (yanlış):** türbülansın `+ 0.15` tabanı. Doğru bir kusur ama
BASKIN değil: rüzgâr 2 m/s'de türbülans zaten 0.70, taban terimin payı küçük.
Yine de düzeltildi — gerekçe `RATIONALE.md`.

**Bu turda ortaya çıkan gerçek eksik:** tanenin kendi çırpınması (spec §17.1
"Salınım") hiç uygulanmamıştı. Türbülans onun yerini tutmuyor — biri tutarlı,
diğeri dağınık.

## "Yakın uzun tanecikler sola düşerken biraz ilerideki tanecikler sağa düşüyor"

**Gerçek sebep:** rüzgârın sınır tabakası çarpanı `profile` her damla için KENDİ
altındaki araziden hesaplanıyordu (`aboveGround = damla.y − TerrainHeightAt(damla.xz)`).
Düz ovada doğru, dik dağda değil: 48 m'lik yağmur kutusu içinde arazi onlarca
metre oynuyor ve yan yana iki damladan biri "yerden 2 m", öteki "yerden 30 m"
çıkıyor. `profile` 0.3 ile 1.0 arasında zıplayınca rüzgâr tepkileri de zıplıyor
ve yağmurun ortak yönü kalmıyor.

**Elenen şüpheliler — hepsi ölçümle:**

| Şüpheli | Nasıl elendi |
|---|---|
| Türbülans genliği | Kâğıtta: sakin havada yanal dalgalanma 0.03 m/s, düşme 4.5 m/s → eğim sapması <0.5°. Fırtınada 8°. Karışıklığı açıklamıyor |
| `response = response;` ölü satırı | CPU zaten ölçekliyor (`lerp(0.03, 0.25, felt)`); satır artık, eksik ölçekleme değil |
| Yakın/uzak kutuların ayrı kayması | `dropClass` ikisinde de aynı; kutu ve kayma tutarlı seçiliyor |
| `TerminalVelocity` CPU/shader uyuşmazlığı | İkisi de aynı Atlas formülü, aralık 0.5–5.0 mm — uyuşuyor |

**Ayırt eden ölçüm:** `profile` geçici olarak `1.0` sabitlendi. İzler tek yönde
toplandı; geri alınınca yeniden dağıldı. İki yakalı geçiş.

**Çözüm:** referans arazi kotu KAMERANIN altından alınıyor
(`TerrainHeightAt(cameraPos.xz)`). Sınır tabakası araziyle birlikte yükselir,
damladan damlaya kırılmaz; kutu yalnız 48 m, o ölçekte profil sürekli olmalı.

## "Bu ne, ayaklarımın gölgesi mi?" — aşağı bakınca iki kara leke

**Sebep:** ayak proxy'leri (`SnowFoot_L/R`) `ShadowCastingMode.ShadowsOnly` ile
kuruluyordu. O kip kutuyu ana kameradan GİZLER ama gölgesini ÇİZER — kipin
amacı zaten bu. Karakter modeli olmadığı için ayakların altında iki serbest
gölge duruyordu.

**Çözüm:** görünmezlik katmandan (`SnowDeformer` katmanı URP renderer'ın
opak/saydam maskelerinden çıkarıldı), gölge kapatıldı (`ShadowCastingMode.Off`).
Yakalama pass'i `cmd.DrawRenderer` ile AÇIK materyalle çizdiği için oyma
bozulmuyor — normal çizim yolundan bağımsız.

Spec 1.3 KAMERANIN culling mask'ine dokunmayı yasaklıyor; değişen renderer
varlığının katman maskesi, o başka bir ayar.

## "Kar tutması için ne kadar beklemeliyim" → tutmuyor

**Ölçüm (tam kar, rüzgârsız, oyun içi zamanla):**

| | hız | beklenenin katı |
|---|---|---|
| düzeltme öncesi | 1.06e-9 m/s | 1/1316 |
| sıcaklık −33 °C | 2.07e-9 m/s | 1/673 |
| compute parametresi düzeltildi | 2.95e-9 m/s | 1/470 |
| beklenen (`_SnowfallSWERate`) | 1.39e-6 m/s | 1 |

**Aracın kendisi önce doğrulandı.** İlk tur duvar saatiyle ölçüldü; Unity
odaksızken Play tick atmayabildiği için `Time.time` farkına geçildi. Kareler
sayıldı: 119 saniyede 13301 kare, yani Play koşuyordu — ölçüm geçerli.

**Elenen şüpheliler:**

| Şüpheli | Nasıl elendi |
|---|---|
| Gökyüzü örtüsü (`skyVis`) | `RT_SkyVis` baştan sona −9999 nöbetçi değeri; `SampleSkyVisibility` bunu 1.0'a çeviriyor (kâğıtta doğrulandı) |
| Döşeme döndürmesi | 1024/8 = 128 grup, 4 döşem → 32×8 = 256 = tam `tileWidth`, kapsama oranı 1 |
| Erime | −33 °C'de hız yalnız iki katına çıktı; ikincil sızıntı, baskın değil |
| Rüzgâr yeniden dağıtımı | Rüzgâr 0.5 m/s → `saturate(0.5/12) = 0.042`, 470 katı açıklayamaz |

**Bulunan sebep (kısmi):** `_SnowfallSWERate` oyunda
`Shader.SetGlobalFloat` ile yayınlanıyordu. **Compute shader'lar global shader
değişkenlerini almıyor.** Çekirdek, editör sınamalarının aynı compute asset'ine
`sim.SetFloat` ile yazıp BIRAKTIĞI eski değeri okuyordu — sıfır olmamasının
sebebi de bu.

**BİRİM SINAMASININ GEÇİP OYUNUN ÇALIŞMAMASININ SEBEBİ:** sınama
`sim.SetFloat` (compute parametresi), oyun `Shader.SetGlobalFloat` (global)
kullanıyordu. İki ayrı yol; sınama oyunun kullandığı yolu hiç denemiyordu.

**Kalan 470 kat henüz bulunmadı.**

**GERÇEK SEBEP BULUNDU — yarım hassasiyet.** `RT_Snow` `ARGBHalf`'tı. R kanalı
su eşdeğerini (m) tutuyor ve tipik değer 1e-6 – 1e-2. Half'ta 6.1e-5'in altı
SUBNORMAL, temsil adımı sabit **5.96e-8**. Kare başına eklenen
`1.39e-6 × dt(0.036) = 5.0e-8` — adımın ALTINDA. Artış yuvarlanmada eriyordu.

`ARGBFloat`'a alındı. Ölçüm: hız 1.371e-6 m/s, beklenen 1.39e-6 → **oran 0.986**.

Aynı sınıfın emsali zaten projede vardı: `RT_SkyVis` mutlak dünya Y tuttuğu için
RHalf'tan RFloat'a alınmıştı. Ders: **birikimli (integre eden) bir doku half
olamaz** — artış adımdan küçükse toplam hiç ilerlemez.

Bir önceki turda "compute globalleri okumuyor" diye yazılan gerekçe YANLIŞTI;
`_TemperatureC` testi çekirdeğin globalleri okuduğunu gösterdi. O değişiklik
zararsız kaldı ama sebep o değildi.

## Kar birikiyor ama zemin beyazlamıyor — örtü 0'da kalıyor

**Sebep:** `_RainOnSnow01` hava sisteminin `PrecipKind` ETİKETİNDEN türüyordu.
Yağış sıcaklıktan koparıldığından beri o etiket −5 °C'de bile `Rain` diyor.
Sonuç: kar YAĞARKEN zemin üstüne yağmur yağıyormuş gibi işliyordu —
`rho += rainOnSnow * 25 * dt/60` yoğunluğu 55'ten 167 kg/m³'e çıkarıyor,
derinlik (`SWE × 1000 / ρ`) üçte bire iniyor ve 4 mm'lik
`SNOW_MIN_VISIBLE_HEIGHT` eşiğini hiç geçemiyor.

Ölçüm: SWE 5.5e-4, yoğunluk 167, derinlik 3.31 mm, örtü 0.

**Çözüm:** `rainOnSnow` artık karın KENDİ keskin kararından
(`SnowRuntimeState.IsSnowing` / `RainWeight01`), etiketten değil. Kar mı yağmur
mu tek bir yerde karara bağlanıyor (`SnowfallController`, eşik 0.5) ve fizik de
aynı kararı okuyor.

Ölçüm sonrası: örtü **0.9999**, `_RainOnSnow01` 0.

**Ders:** aynı olgunun iki ayrı doğruluk kaynağı olamaz. Görsel keskin karar
veriyorken fiziğin başka bir etikete bakması, ekranda kar yağarken zeminde
yağmur işletiyordu.

## Zeminde düzenli, tekrar eden koyu leke ızgarası

**Sebep:** kar kenarı gürültüsü (`_SnowBreakup`) DÜZ döşemeyle örnekleniyordu
(`posWS.xz * scale`). Aynı desen sabit periyotla tekrar edince zemin yukarıdan
ızgara gibi okunuyor.

**Çözüm:** `SampleStochasticMask` — mevcut `StochasticTiling.hlsl`'in hex
ızgarası, hücre başına rastgele kayma, varyans geri kazanımı. LUT'suz sürüm,
çünkü maskede histogramın birebir korunması görünmüyor.

Aynı yöntem projede detay normalleri için ZATEN vardı; breakup dokusu
atlanmıştı.

## Ayak izi karı delip çıplak zemini gösteriyor

**İki ayrı sebep, ikisi de düzeltildi:**

1. Oyma `baseH`'a kadar gidebiliyordu, yani kar katmanının TAMAMINI
   kaldırabiliyordu. Gerçekte ayağın altındaki kar SIKIŞIR: gevşek (ρ≈100)
   sıkışmışa (ρ≈325) dönerken hacim yoğunluk oranı kadar küçülür ve dipte her
   zaman kar kalır. Oyma artık `baseH × (1 − ρ_gevşek/ρ_sıkı)` ile sınırlı.

2. Kesme (`clip`) OYULMUŞ yüzeye bakıyordu. Derin bir iz eşiğin altına düşünce
   piksel kesiliyordu. Eşiğin sorduğu soru "burada kar var mı"; cevabı oyulmamış
   `baseH`. İz artık kar sütununun içinde bir ÇUKUR.

## İz kenarında diken diken duvar

**Sebep:** `SNOW_RIM_STRENGTH` 1.8 ve tavan 10 cm. 20 cm karda sırt hedefi
~20 cm çıkıyor, tavana kırpılsa bile karın yarısı kadar duvar oluyordu.
`rim = max(rim, ...)` birikimi bunu kare kare tırtıklı bir zarfa çeviriyordu.

**Çözüm:** güç 0.30, tavan 2 cm, ve sırt kendi izinden yüksek olamıyor
(`min(raised, blurCarve)`). Oyma burada sıkıştırma — hacmin çoğu yana taşınmıyor.

## Kar sadece oyuncunun çevresindeki KARE alanda tutuyor

**İki ayrı katman, iki ayrı sebep:**

1. **Bölge dışı sabit 0'dı.** `_FallbackSWE` "dünyanın genel kar durumu"nu
   taşıyor ama `settings.DefaultSwe` (0) ile besleniyordu — hava sisteminden
   hiç haber almıyordu. Kar 24 m'lik pencerede birikiyor, dünya öğrenmiyordu.

   **Çözüm:** `SnowManager.WorldSwe` — aynı yağış oranını dünya çapında entegre
   ediyor, aynı 6 saatlik oturma eğrisini uyguluyor. Üç yerde birden kullanılıyor
   (bölge dışı, kaydırma kenarı, ilk doldurma) ki oyuncu yürüdükçe kalınlık
   basamak yapmasın. Ölçüm: `_FallbackSWE` 0 → 3.58 mm.

2. **Kar mesh'i 24 m ve bu TASARIM.** `SnowSurface.Extent = AreaSize × 0.5`;
   mesh deformasyon bölgesiyle bilerek aynı kareyi kaplıyor (spec §6.1). Onun
   dışında kar, DAĞIN KENDİ kar katmanından gelmeli
   (`MountainSurface.hlsl`, global `_SnowCoverage`).

   **AÇIK:** `_SnowCoverage = 0.99996` ölçüldü ama dağ karanlık kaldı; mesh
   kenarında sert basamak görünüyor. Dağın kar maskesi neden geçmiyor, henüz
   bulunmadı.

## İz çukur gibi, dik duvarlı ve dipte karanlık

**Sebep zincirinin tamamı ölçümle çıktı — üç ayrı kusur:**

1. **Sıkışma kare başına uygulanıyordu.** Spec §10.1'in
   `compact = SNOW_COMPACT_RATE * saturate(...)` ifadesinde `dt` yok. 100 fps'de
   0.1 saniyelik bir ayak teması 10 kare eder ve rhoN 0.10'dan 0.55'e TEK
   adımda çıkıyordu.

2. **SWE korunduğu için yoğunluk artışı doğrudan yükseklik kaybı.**
   `baseH = SWE × 1000 / ρ`; rhoN 0.01 → 0.55 demek 20 cm kar → 3 cm demek.
   Derinlik oymadan değil SIKIŞMADAN geliyordu — oymayı sınırlamak
   (iki tur denendi) hiçbir şey değiştirmedi.

3. **Taşıma gücü yoktu.** Spec `min(penetration, baseH)` diyor, yani kar ne
   kadar kalınsa ayak o kadar batıyor. O ifade oyuncunun karın ÜSTÜNDE
   yürüdüğünü varsayıyor; bizimki araziye bastığı için batma her zaman
   tabakanın tamamıydı.

**Çözümler:** sıkışma saniye başına (`× _SnowDeltaTime`, hız 0.25/s), tek
geçişte en fazla `SNOW_MAX_COMPACT_PER_PASS` (0.06) yoğunlaşma, batmaya mutlak
sınır (`SNOW_MAX_SINK` 8 cm). Spec'in "5–6 geçişten sonra patika oluşur"
tarifi korunuyor; tek iz sığ kalıyor.

**Başarısız denemeler (geri alındı):**

| Deneme | Neden battı |
|---|---|
| `carve`'a şev açısı sınırı | Duvar `carve`'da değil yoğunluktaydı; hiçbir şey değişmedi |
| Yoğunluğa şev açısı sınırı | Kütle korunmuyor — kısıt alçak tekseli yükseltip yükseği alçaltmıyor, çukur her karede kenardan doluyor ve İZ TAMAMEN SİLİNDİ |

**Araç yalanı:** kesit probu yüzeyi `baseH − carve + rim` ile hesaplıyordu,
shader'ın oyma sınırını uygulamıyordu. "0 cm" gösterdiği yerde gerçek değer
0.92 cm'di. Prob shader'ın formülüyle hizalandı.

## Oyuncunun çevresinde parlaklığı farklı bir KARE

**Sebep 1 — otuz kare gecikme.** Dağın kar katmanı `_SnowCoverage`'dan
besleniyordu, o da async GPU geri okumasından: otuz karede bir tazeleniyor.
Kar mesh'i ANINDA güncelleniyor. Arada içerisi yeni durumu, dışarısı otuz kare
önceki durumu gösteriyordu — kullanıcı belirtiyi iki kez, ters yönlerde
bildirdi (bir kez içerisi beyaz dışarısı siyah, bir kez tam tersi).

**Çözüm:** `_SnowCoverage` artık `SnowManager.WorldSwe`'den, CPU'da, gecikmesiz.
Eğri yüzey shader'ınınkiyle aynı (`MinVisibleHeight` eşiği, `EdgeFadeRange`
bandı); üç yer de aynı sabitleri okuyor.

**Sebep 2 — AÇIK.** Gecikme kalkınca altındaki gerçek uyumsuzluk açığa çıktı:
tam örtüde mesh dağdan **%15 karanlık** (dağ R212 G207 B207, mesh R181 G174
B176). Albedo, yoğunluk, tazelik formülü ve detay normal dokusu birebir aynı;
fark aydınlatma yolunda.

Elenen: ortam (ikisi de `SampleSH × AO × diffuse`, ikisinde de yansıma küresi
yok — bu yüzden mesh'e `GlobalIllumination` eklemek %3 karanlıktan %14 parlağa
kaçırdı ve geri alındı), wrap diffuse (kâğıtta mesh'i %5 PARLAK yapıyor,
karartmıyor).

Kalan şüpheli: mesh'in yükseklik tabanlı AO'su (`SnowHeightAO`) — dağda
karşılığı yok.

**Sebep 3 — kar mesh'i gölge alıyordu.** İki yakalı ölçüm (öğle, tam örtü,
sınırın iki yakası aynı karede):

| durum | oran (mesh / dağ) |
|---|---|
| başlangıç | 0.847 |
| mesh'in AO'su kapalı | 0.835 — AO ELENDİ |
| mesh gölge ALMIYOR + ATMIYOR | **0.999** |
| mesh yalnız ATMIYOR | 0.856 |
| arazi caster'ları da kapalı | 0.881 |

Gölgeyi tamamen kesmek farkı kapatıyor, yani kaynak gölge zinciri. Atma tarafı
düzeltildi (mesh kendi gölgesini alıyordu; kar araziye oturuyor, arazi zaten
kendi gölgesini atıyor ve karın öz-gölgelemesi ayrı bir terimde —
`SnowHeightAO`). Kalan pay hâlâ AÇIK: bütün caster'lar kapalıyken bile oran
0.881, oysa gölge alma kapalıyken 0.999.

**Kalan pay ÖLÇÜLDÜ ama sebebi bulunamadı.** `mainLight.shadowAttenuation`
doğrudan ekrana basıldı: kar yüzeyinde **tek düze 0.850** (215–219/255, desen
yok). Işığın gölge gücü 1, yani bu tam gölge değil — PCF taplarının ~%15'i
gölgede okunuyor, yani yüzey her yerde bir gölge sınırına oturuyor.

Elenenler: karın kendi caster'ı (kapatıldı, 0.847 → 0.856), arazi caster'ı
(gölge koordinatı kar kalınlığı kadar ışığa ötelendi, 0.856 → 0.851 — fark yok),
mesh AO'su (0.835, fark yok).

`receiveShadows = false` yapınca oran tam 0.999 oluyor.

**GÖLGE ZİNCİRİ YANLIŞ ŞÜPHELİYDİ — KAPANDI.** Sınırın iki yakasında AYNI
büyüklükleri basan bir prob kurulunca (kar ve dağ shader'ına aynı numaralarla
altı mod; araç önce sabit renkle doğrulandı: iki taraf da 0.400 okudu) gölge
terimi 1.000/1.000 çıktı. Yani gölge hiç suçlu değildi; `receiveShadows`'u
kapatmak farkı kapatıyordu çünkü BAŞKA bir terimi de birlikte kapatıyordu.

Gerçek sebepler ÜÇ TANE, üçü de ayrı ayrı ölçüldü:

| terim | kar mesh | arazi | oran |
|---|---|---|---|
| gölge | 1.000 | 1.000 | 1.000 |
| albedo | 0.923 | 0.923 | 1.000 |
| pürüzlülük | 0.480 | 0.480 | 1.000 |
| **normal (N.y)** | **0.041** | **0.998** | ← 1. sebep |
| **doğrudan ışık** | **6.041** | **0.868** | ← 2. ve 3. sebep |

1. **`RNMBlend` paketlenmemiş normal döndürüyordu.** Girdisi 0..1, çıktısı
   −1..1 idi; her katman bir öncekini paketli sanıp `*2−1` uyguluyordu. Düz
   yüzeyde bile `(0,0,1) → (−1,−1,1)`. Kâğıtta tek harman sonrası
   N.y = 1/√3 = 0.577, iki harman sonrası 0.051; ölçüm 0.5647 ve 0.0415 —
   birebir. Mesh şekilsiz düz bir levhaydı.

2. **Speküler URP sözleşmesine aykırı kullanılıyordu.** `DirectBRDFSpecular`
   yalnız D·V skalerini döndürür; `brdfData.specular` (dielektrikte ~0.04) ve
   `NdotL` çarpanları yoktu. Doğrudan ışığın %68'i buradan geliyordu:
   spec 4.133 → düzeltmeden sonra 0.146. Spec §14.1 de aynı satırı taşıyor,
   yani hata spec'te; kod ona sadıktı.

3. **Kar mesh'i bulut gölgesini hiç okumuyordu.** `SnowLit.shader`'da
   `_LIGHT_COOKIES` pragma'sı yoktu. Ölçüm anında arazideki cookie değeri
   **0.0421** (güneşin %96'sını kesen bulut); arazi kararırken oyuncunun
   çevresindeki kar aynı parlaklıkta kalıyordu. Doğrudan ışık oranı 11533 → 1.255.

Üçü düzeltildikten sonra son renk oranı **1.160** (başlangıç 0.847, ara
değerler 24.9 ve 15.6). Kalan payın sahibi wrap diffuse + translüsanlık +
parıltı; üçü de karın bilinçli özellikleri.

**Dördüncü bir sebep: parıltı yalnız mesh'te vardı.** Normal düzelince
`saturate(dot(N,L)*4)` kapısı ilk kez açıldı ve mesh benek benek oldu, arazi
düz kaldı — kare bu kez parıltıyla çizildi. Parıltı araziye de bağlandı
(`MountainSurface.shader`, `snowMask` ağırlıklı) ve ayarları materyalden
global'e taşındı: iki yüzey iki farklı sayıyla parıldayamaz.

---

## Arazi simsiyah, oyuncunun çevresindeki kare normal

**Kullanıcının ağzından:** "kare dışı alan yine simsiyah oldu? bilinçli mi"

**İlk şüpheli (yanlış):** ışıklandırma, gölge, sis.

**Gerçek sebep:** Play sırasında shader yeniden içe aktarılınca `TerrainSurface`
runtime materyali (`HideFlags.DontSave`) nesne olarak ayakta kalıyor ama
ÜZERİNE YAZILMIŞ TÜM DEĞERLER siliniyor. `_TerrainSize` sıfıra düşüyor,
`uv = (pos − origin) / 0` sonsuz oluyor ve arazinin TAMAMI NaN basıyor.
`EnsureMaterial` yalnız `material != null` kontrol ediyordu; `ApplySettings`
de `appliedRevision` eşit kaldığı için atlıyordu.

**Ayırt eden ölçüm:** kimlik maskesiyle ayrılmış piksellerde arazinin
albedo/ortam/doğrudan/normal kanalları **162674 pikselin 162674'ünde NaN**;
gölge ve N·L kanalları temiz görünüyordu çünkü `saturate(NaN)` D3D'de 0
döndürüyor. Araç önce sabit renkle doğrulandı.

**Not:** bu yalnız editörde, Play sırasında shader reimport edilince oluyor.
Ama iki tur ölçümü çöpe attı ve kullanıcıya iki kez yanlış belirti gösterdi.
`EnsureMaterial` artık `material.HasVector(TerrainSizeId)` de kontrol ediyor ve
`Update` her kare çağırdığı için kendini onarıyor.


---

## Kar izi damga gibi, RDR2 oluğu değil

**Kullanıcının ağzından:** "ben rdr2 tarzı oluk istiyorum, adım gibi gözükmesin"
ve "yumuşak oluk sığ iz istiyorum".

**İlk şüpheli (yanlış):** oymanın derinliği, `SNOW_MAX_SINK`.

**Gerçek sebep ÜÇ TANE, üçü de ayrı ölçüldü.**

**1. Oymanın enine kesiti dik duvarlı bir basamaktı.**

```
enine kesit (mm, 23.4 mm teksel): 0 0 0 0 0 0 0 0 80 80 80 80 0 0 0 0
```

8 cm derin, 9 cm geniş, geçiş SIFIR. `KDeform` yakalamayı `cap.a > 0.5` ile
ikili bir kapıdan geçiriyordu. Oysa bulanıklaştırılmış kapsama payı zaten
yumuşak bir rampa:

```
cap.a kesiti: 0.00 0.04 0.31 0.65 0.81 0.98 1.00 1.00 1.00 0.94 0.70 0.20 0.00
```

Eşik bu rampanın tamamını atıyordu. Kapsama artık yanal yük profili olarak
kullanılıyor; kesit yumuşadı:

```
0 0 9 40 65 80 80 80 80 80 80 77 | 28 | 65 80 80 80 80 80 71 49 14 0 0
```

İki ayak izi, her biri ~19 cm, üç tekselde yumuşak kenar.

**2. `min(..., tasimaSiniri)` rampayı düzleştiriyordu.** Tavanın üstündeki
her değer tam tavana iniyordu. Tavan artık en derin noktanın sınırı, profili
kapsama veriyor.

**3. Detay normalleri oluğun EĞİMİNİ siliyordu — asıl sebep buydu.**

Ölçüm zinciri:

| ölçülen | değer |
|---|---|
| oluğun derinliği (yüzey geometrisi) | 7.5 cm, yumuşak profil |
| merkezi farkın ham bileşeni `\|hD-hU\|` | 39, 22, 17, 19, 35 mm — gradyan var |
| detay ÖNCESİ N.y | 0.766 – 1.000 (oluk görülüyor) |
| detay SONRASI N.y | 0.998 her yerde (oluk yok) |
| son görüntüde oluk kontrastı, detay devrede | **%0.8** |
| aynı ölçüm, taban normali doğrudan | **%10.6** |

Detay şiddeti sıfıra indirilince bile kontrast %0.5'te kaldı: sorun şiddet
değil, RNM'nin bu bağlamdaki davranışıydı. `RNMBlend(taban, DÜZ detay)`
kimliği yerinde ölçüldü ve TUTMADI — cebirsel olarak tutması gerekirken
tabanı değil düz normali döndürdü.

**Çözüm:** detay artık eğim uzayında toplanıyor
(`SnowDetailNormals.hlsl` → `SampleDetailSlope`). Dosyanın kendi ilkesi zaten
buydu: "normaller türev, türev doğrusal toplanır". Eğim toplamı tabanı
yapısı gereği korur — detay sıfırsa sonuç tabanın kendisidir.

**Sonuç:** oluk kontrastı **%1.0 → %13.3**. Ekranda iki sürekli paralel oluk;
damga yok, dipte çıplak zemin yok.


---

## Kar izinde hareket ederken titreme

**Kullanıcının ağzından:** "kar izinde hareket ederken titreme oluyor".

**Ayırt eden ölçüm:** kamera TAMAMEN sabitken iki ardışık render'ın farkı.
Gölgelendirme deterministikse fark sıfır olmalı.

| durum | kare farkı ort | tepe |
|---|---|---|
| olduğu gibi | 0.00376 | 0.0902 |
| parıltı kapalı | 0.00315 | 0.0120 |
| **post kapalı** | **0.00000** | **0.0000** |

Post kapalıyken fark TAM SIFIR: kar gölgelendirmesinin kendisi kararlı,
titremenin tamamı zamansal filtreden (TAA) geliyor. Tepe sıçramaların
kaynağı parıltı — TAA'nın çözemediği piksel ölçeğinde bir sinyal; yoğunluk
0.06'dan 0.006'ya indirilince tepe fark 7.5 kat düştü.

Kayıt: gölgelendirme tarafında aranacak bir şey yok.


---

## 1 cm karda yüzey delik deşik, kare geri geldi

**Kullanıcının ağzından:** "1cm kar seçtim. bu ne rezalet? kare beni takip
ediyor."

**İlk şüpheli (yanlış):** kırılma gürültüsünün ölçeği (`_SnowBreakupScale`
3.0 → desen 33 cm'de tekrar, 256 teksellik doku → 1.3 mm'lik benekler).
Arazi AYNI gürültüyü kullanıyor ve düzgün görünüyor.

**Gerçek sebep: arazi `lerp`, mesh `clip`.** Aynı soruya iki farklı cevap.

| | arazi | kar mesh'i |
|---|---|---|
| kural | `saturate((raw − noise) × sharpness)` → **karışım** | `clip(edgeFade − breakup×0.6)` → **delik** |
| 1 cm'de sonuç | %29 beyaz, düzgün | yüzeyin yarısı kesilmiş |

`edgeFade` MUTLAK derinlikten geliyordu: 4 mm → 24 mm bandı, 1 cm'de
`(10−4)/20 = 0.30`. Gürültünün ortalaması da 0.30 — yani ince ama DÜZGÜN bir
örtü "her yeri kenar" sayılıp piksel ölçeğinde deliniyordu.

**Düzeltme 1 — bant 4→24 mm yerine 4→10 mm.** Sürekli örtü artık 1 cm'de
kapanıyor. Ölçüm: `_SnowCoverage` 1 cm'de **0.294 → 0.998**, mesh'te
`edgeFade` 1.0 → `clip(1 − 0.6·breakup) ≥ 0.4` → delik yok.

Tutma temposu (ölçülen 3.15 mm/dk @ %100):
tam fırtınada ilk beyazlama 1.3 dk / sürekli örtü 3.2 dk;
%30 şiddette 4.2 dk / 10.6 dk.

**Düzeltme 2 — bölge kenarı kesmeye de girdi.** `SnowEdgeFade` yalnız
YÜKSEKLİĞE uygulanıyordu: mesh kenarda araziyle aynı kota iniyor, basamak
olmuyor — ama pikselleri tam parlaklıkta çizilmeye devam ediyordu. Mesh ile
arazi iki ayrı ışıklandırma modeli kullandığı için aralarında **%2.3**
parlaklık farkı kalıyor (ölçüldü: iç 0.8318, dış 0.8132) ve düz bir alanda
bu fark KESKİN ÇİZGİ olarak okunuyor. Kenar sönümü kesmeye de girince geçiş
çizgi değil lekeli bir kuşak oluyor.

**Not:** kalan %2.3 iki ışıklandırma modelinin farkı — karın wrap diffuse'ü,
translüsanlığı ve `_ShadowTint`'li ortamı. Kaynağında kapatmak karın kendi
özelliklerini silmek demek; kenar kuşağı farkı görünmez kılıyor.


---

## Kare oyuncuyla birlikte geliyor (kalan %2.3)

**Kullanıcının ağzından:** "baksana benimle birlikte geliyor. çok can sıkıcı."

24 m'lik kare bir hata değil, deformasyon penceresinin kendisi — ayak izi
çözünürlüğünde simülasyon ancak oyuncunun çevresinde karşılanabiliyor
(`SCALE.md` → Kar bölgesi). Sorun pencerenin varlığı değil, KENARININ
görünmesiydi.

**Sebep: aynı kar, iki ışıklandırma modeli.** Kar mesh'i sarmal NdotL +
arkadan sızma + `_ShadowTint`'li ortam kullanıyordu; arazinin kar katmanı
URP'nin standart PBR'ı. Ölçüldü: sınırın iki yakası arasında **%2.3**
parlaklık farkı (iç 0.8318, dış 0.8132). Düz beyaz bir alanda %2 bile keskin
çizgi olarak okunuyor.

**Çözüm:** arazinin kar katmanı da `SnowDirectLight` + `SnowAmbient`
kullanıyor; kaya standart PBR'da kalıyor, ikisi `snowMask` ile harmanlanıyor.
Bunun ön koşulu `_ShadowTint` ve `_TranslucencyStrength`'in materyalden
GLOBAL'e taşınmasıydı — arazi ayrı bir materyal, per-materyal kalsalardı
sıfır okur ve fark yeniden doğardı. Aynı sebeple `_SnowBreakup` tanımı da tek
yere (`SnowCommon.hlsl`) indi.

**Sonuç:** oran **1.023 → 0.9955**. 1 cm'de de 20 cm'de de kare görünmüyor.


---

## Kare EĞİMLİ arazide geri geliyor (düz zeminde yok)

**Kullanıcının ağzından:** "baksana benimle birlikte geliyor... ne işe
yaradığını da anlamadım. iğnenç bi his"

**Test hatası (benim):** kareyi hep TEPEDEN (90°) sınadım ve temiz gördüm.
Kullanıcı eğik açıdan bakıyordu. Düz zeminde iki normal de dikey olduğu için
fark yok; eğimli yamaçta fark açılıyor. Dik duvar tepeden görünmez.

**Ölçüm (eğik açı, %15 eğimli yamaç, alçak güneş):**

| | kar mesh | arazi |
|---|---|---|
| parlaklık | 0.6983 | 0.7885 → **oran 0.8856** |
| yerel doku RMS | 0.0467 | 0.0271 → **1.72×** |
| albedo | eşit | eşit |
| **normal** | **(−0.008, 0.996, −0.008)** dimdik | **(0.149, 0.991, 0.047)** eğimli |

**Gerçek sebep:** `SnowSurfaceAt` yalnız kar KALINLIĞINI döndürüyor, arazi
yüksekliğini değil. `SnowNormalAt` merkezi farkı bu yüzden yalnız kalınlığın
gradyanını görüyordu ve sabit kalınlıkta SIFIR çıkıyordu — mesh eğimli bir
yamaçta bile dimdik bir normal taşıyordu. Arazi eğik, mesh dik → ışığı farklı
alıyor.

**Çözüm:** kar yüzeyi = arazi + kalınlık, eğimi de ikisinin toplamı.
Eğimler doğrusal toplanıyor (`SnowDetailNormals` ile aynı ilke):
`zeminEğim + karEğim`. `hHere` parametresi ve `lerp(nGround, nSnow, h/0.08)`
kapısı gereksizleşti — ince karda kalınlık gradyanı zaten küçük, kalın karda
büyük; davranış yapısal olarak doğru.

Sonuç: normal kar (0.5761, **0.9914**, 0.5205) vs arazi (0.5746, **0.9955**,
0.5237) — eşleşti. Parlaklık oranı **0.8856 → 0.9557**.

**İkinci sebep: kenar kuşağının KENDİSİ.** Bir tur önce `SnowEdgeFade`
kesmeye bağlanmıştı; gerekçesi iki yüzey arasındaki %2.3 farkı lekeli bir
kuşakla gizlemekti. Fark kaynağında kapanınca kuşak gereksizleşti ve kendisi
görünür oldu — kenarda granüllü bir hat. Kapatılınca sınır tamamen kayboldu.
`SnowEdgeFade` yine YÜKSEKLİKTE duruyor (basamağı o önlüyor), kesmede yok.


---

## Kare DERİNLİKLE ölçekleniyor: 1 ve 5 cm temiz, 20 ve 50 cm'de var

**Kullanıcının ağzından:** "1 ve 5cm'de düzelmiş, 20cm ve 50cmde sorun devam
ediyor?"

Bu ayrım tek başına sebebi veriyor: belirti kar DERİNLİĞİYLE ölçekleniyorsa
kaynağı da derinlikle ölçeklenen bir büyüklüktür.

**Gerçek sebep: mesh kar kalınlığı kadar yükseliyor, arazi yükselmiyor.**
`_SnowCoverThickness` (4 cm) tanımlı ama hiçbir yerde köşe shader'ına
girmiyordu — arazinin karı yalnız boyamaydı. Mesh ise gerçekten yükseliyor.
Bölge kenarında `SnowEdgeFade` yüksekliği son 2 metrede sıfıra indiriyor:

| kar | rampa | eğim |
|---|---|---|
| 1 cm | 2 m | %0.5 — görünmez |
| 5 cm | 2 m | %2.5 — görünmez |
| 20 cm | 2 m | **%10** |
| 50 cm | 2 m | **%25** |

**Ölçüm tuzağı:** `Mathf.SmoothStep(a, b, t)` HLSL'in `smoothstep(e0, e1, x)`
imzasıyla AYNI DEĞİL — Unity'ninki a..b arasında t ile interpolasyon yapıyor.
İlk hesabım bu yüzden sönümü %70 uv'de 0.036 gösterdi; gerçek değer 1.0.

**Çözüm:** arazi de dünyanın kar kalınlığı kadar yükseliyor
(`SnowWorldCoverHeight`, `MountainSurface.shader`'ın DÖRT geçişinde birden:
ileri, gölge, derinlik, DepthNormals). Mesh'in kenar sönümü de sıfıra değil
DÜNYA KAR SEVİYESİNE iniyor — `lerp(SnowWorldCoverHeight(), h, SnowEdgeFade)`.
Sınırda iki yüzey aynı kotta bitiyor, basamak kalmıyor.

Kar kalınlığı geometri olmayı SÜRDÜRÜYOR: 50 cm kar biriktiğinde zemin
gerçekten 50 cm yükseliyor — hem bölgede hem dışında.

Görsel doğrulama: 1, 5, 20, 50 cm × (yukarıdan eğik, göz hizası) — hepsinde
sınır görünmüyor.


---

## Yerinde beklerken iz yuvarlak bir çukur gibi derinleşiyor

**Kullanıcının ağzından:** "durduğumda iz yuvarlak olarak derinleşiyor. bunu
istemiyorum" ve "yuvarlağımsı bir şey görmek istemiyorum".

**Sebep:** sıkışma `_SnowDeltaTime` ile çarpılıyordu, yani GEÇEN SÜREYE
bağlıydı. Ayak durduğu sürece `snow.g` (yoğunluk) birikiyor; yoğunluk arttıkça
`baseH = SWE × 1000 / ρ` düşüyor ve yüzey alçalıyor. Oyma sabit kalsa bile
çukur derinleşiyordu.

`trail.r = max(trail.r, target)` biriktirmiyor — oyma suçlu değildi.

**Çözüm:** sıkışma o karede AÇILAN oymaya orantılı
(`SNOW_COMPACT_GAIN * yeniOyma / baseH`). Kar da böyle davranır: yük sabitken
sıkışma bir kerede dengeye gelir, beklemek ek sıkışma üretmez. Yan fayda: kare
hızından da bağımsız — eskiden `dt` çarpanı bu yüzden eklenmişti.

Ölçüm: 200 kare yürüyüş + 15 saniye bekleme sonunda `snow.a` = 0.0600, yani
tam olarak `SNOW_MAX_COMPACT_PER_PASS` tavanı; süre boyunca artmıyor.

**Ayrıca oluk inceltildi ve yuvarlaklık kaldırıldı.** Gövde küre (36 cm çap)
yerine oval (22×12×40 cm) ve hareket yönüne hizalı. Dik kesit 17 tekselden
10 teksele indi (40 cm → 23 cm).


---

## İz kenarı çok düzgün / testere dişi gibi tekrarlıyor

**Kullanıcının ağzından:** "izin sınırlarında dağılmalar ekleyebilir misin?
prosedürel, düzensiz."

**İlk şüpheli (yanlış çıktı):** kapsamayı gürültüyle EŞİK gibi kesmek —
`saturate((kapsama - gürültü*A) / (1-A))`. Merkezi korur gibi görünüyor ama
ölçüldü ve battı: bölme rampanın kontrastını `1/(1-A)` kadar artırıyor, A=0.60'ta
kenar rampası tamamen yok oldu (kesit `0 0 0 80 80 … 80 0` — dik duvar), iz
21.9 cm'den 14.6 cm'ye indi, yer yer tek tekselde koptu. İstenen 3–5 cm sapmaya
ancak izi bozarak ulaşıyordu.

**Gerçek çözüm:** gürültüyü eşiğe değil, kapsamanın OKUNDUĞU teksele uygula.
Okuma konumu iki ölçekli gürültüyle kaydırılınca rampa olduğu gibi taşınıyor:
sınır oynuyor, profil bozulmuyor, iz kopmuyor (dolu 111/111 sütun, rampa eski
profille birebir, kenar sapması 2–4 cm).

**İkinci belirti — testere dişi:** ilk kaydırma tam sayı teksele yuvarlanıyordu
(`round`). Kenar teksel teksel zıplayınca düzensizlik yerine DÜZENLİ bir testere
dişi çıktı — ekrandan görüldü, ölçümde değil. Kaydırma bilinear'a çevrildi (dört
komşudan harmanlama); kenar organik, tekrar etmeyen dağılmaya döndü.

**Ayırt eden ölçüm:** izin enine kesiti (rampa var mı / dik duvar mı), sütun
doluluk oranı (kopma var mı) ve kenar y konumunun doğrusal eğilim çıkarılmış
RMS'i (dağılma genliği cm). Görsel testere dişini sayı yakalamadı — düzenli
desen RMS'i şişirmiyor; ekran gerekti.

**Ölçüm tuzağı:** sahnede iki `CharacterController` var (`Player`, `Bicycle`);
`FindAnyObjectByType` bisikleti döndürüp yürüyüşü 4 km ötede boş bölgeye
yaptırdı. Probe HER ZAMAN `SnowTrailBody`'nin ebeveynine takılır.


---

## İz dikdörtgen, geniş, kenarında dağılma yok (üçü tek kök)

**Kullanıcının ağzından:** "dikdörtgen ayak sorunu devam ediyor, hafif
yuvarlağımsı olmalıydı", "oluk çok geniş, daralt demiştim", "oluk kenarlarında
dağılmalar yok".

**İlk şüpheli (kısmen yanlış):** kenar dağılması eklenmişti (warp) ama kullanıcı
görmüyordu; "warp çalışmıyor" sanıldı. Gerçekte warp çalışıyordu — sorun izin
ÇOK GENİŞ olmasıydı; 35 cm izde 3 cm dağılma göze düzgün kenar okunuyordu.

**Ölçüm tuzağı 1 — yanlış nesne:** sahnede iki `CharacterController` var
(`Player`, `Bicycle`); `FindAnyObjectByType` bisikleti döndürüp yürüyüşü 4 km
ötede boş bölgeye yaptırıyordu, iz hiç oluşmuyordu. Probe HER ZAMAN
`SnowTrailBody`'nin ebeveynine takılır.

**Ölçüm tuzağı 2 — kar yok:** yeni Play oturumunda kar birikmemişti (swe=0),
iz oluşamıyordu. Test öncesi `SnowManager.FillSnowDepth(0.20f)` şart.

**Gerçek kök (kaynak RT karşılaştırmasıyla):** eski gövde basık ovaldi
(22×12×40). İki bağımsız sebep:
- **Geniş:** 12 cm yükseklikte küre 20 cm karda TAMAMEN gömülüyor, izini
  ekvatoruyla (22 cm) bırakıyor; carve blur'uyla 35 cm çıkıyor. CAPTURE RT
  (küre gölgesi) 14 cm, TRAIL 35 cm — fark blur+ekvator.
- **Düz taban / dikdörtgen:** basık alt yüzey geniş bir düz alan; ayrıca gövde
  oyuncunun ayağında (kar sütununun TABANINDA) duruyor, küre tüm sütunu delip
  batma `enFazlaOyma=8cm` sınırına HER YERDE dayanıyor → 80 mm plato
  (kesit `3 23 63 80×11 74 36 4`).

**Çözüm:** iki değişiklik.
1. **Geometri** 15×24×34: 24 cm yükseklik yarıçapı (12 cm) batmadan büyük, dar
   alt eğri iz bırakıyor → 16 cm, U'ya yakın kesit. Dar izde warp görünür oluyor.
2. **Kar yüzeyine oturtma:** `SnowTrailBodyAlign` gövdeyi kar yüzeyinin 5 cm
   altına koyuyor (tabana değil) → küre az batıyor, düz taban U'ya dönüyor.
   Yükseklik iz-öncesi kalınlıktan (`Depth + SinkDepth`) türetiliyor, yoksa
   küre kendi izini okuyup derinleşir.

**Ayırt eden ölçüm:** CAPTURE vs CAPTURE_BLUR vs TRAIL RT genişliklerini AYRI
ölçmek (blur payını izole eder), durarak tek damga (yürürken eksen karışması
olmadan net enine kesit), ve gövde world yaw'ı (align dönüyor mu — 90° çıktı,
dönüyor). Görsel: dar, yüzeysel, kenarı organik dağınık oluk (RDR2 tarzı).


---

## Yön değiştirirken iz dikdörtgen / satır satır çıkıyor

**Kullanıcının ağzından:** "farklı yönlere hareket ederken belirgin oluyor o
dikdörtgen iz", "niye satır satır iz çıkıyor farklı yöne hareket ederken",
"iz bırakan kaynak dikdörtgen olduktan sonra nereyi düzeltirsen düzelt sadece
yama yapmış olursun".

**Yanlış çıkan şüpheliler (sırayla, hepsi ölçüldü):**
- *Adım sapması (jitter).* Kısıldı, doku ölçümünde `ZIGZAG=0` çıktı ama ekranda
  desen sürdü. Katkısı vardı, kökü değildi.
- *Yetersiz yumuşatma.* Capture blur 1.5 → 2.5 → 4.0 teksel yapıldı; düz
  gidişte kenar temizlendi, çaprazda desen kaldı. Üstünü örtüyordu.
- *Gövde şekli.* Oval → daire yapıldı; yön bağımlılığı kalktı ama tek başına
  yetmedi.

**Gerçek kök — KAYNAK MASKESİ BİNARY.** `Hidden_SnowCaptureDepth` maskeye
sabit `1.0` yazıyor ve `RT_Capture` MSAA'sız açılıyordu: bir teksel ya tamamen
nesnenin içinde ya tamamen dışında. 16 cm gövde 2.3 cm tekselde ~7 teksel;
kenar örtüşmesi olmadan yuvarlak siluet köşeli bir bloğa raster ediliyor, her
kare basılan bu blok ardışık damgalarda merdiven/dilim deseni üretiyor. Yön
değişince damga adımı teksel ızgarasına açı yaptığı için desen belirginleşiyor.

**Ayırt eden ölçüm:** `RT_Capture`'ın alfa kanalını ENİNE okuyup ARA DEĞERLİ
teksel saymak. MSAA öncesi: `0 0 100 100 100 100 100 100 0 0` → ara değer 0
(binary, kare kenar). MSAA sonrası: `… 100 100 100 100 100 100 50 0 …` → kenar
tekseli kısmi kapsama taşıyor. Trail kesiti de sertten yumuşağa döndü:
`9 19 42 55 66 78 79 80 64 35 18 11 4`.

**Çözüm:** `RT_Capture` ve derinlik tamponu `antiAliasing = 4`. Yanında iki
destek düzeltmesi: gövde dönel simetrik (yön bağımlılığı yok) ve gövde
yüksekliği zamanda yumuşatılıyor (damgalar ortak kotta).

**Ders:** belirti "şekil bozuk" ise önce ŞEKLİN ÜRETİLDİĞİ yere bakılır. Blur
ve carve yumuşatmaları zincirin sonunda; kaynak binary olduğu sürece her biri
yalnız yama.


---

## Uzaktaki parıltılar kocaman piksel gibi görünüyor

**Kullanıcının ağzından:** "kar üzerindeki parlamalar çok yapay duruyor",
"uzağa baktığımda aynı parlamalar kocaman pixel olarak gözüküyor", "parlama
sadece yakında olsun", "çok abartı duruyor, gerçekçi olmalı".

**Sebep:** parıltı yoğunluğu Bowles & Wang yöntemiyle ekran uzayında sabit
tutuluyor — bu doğru ve titremeyi önlüyor — ama parıltının BOYUTU hücre boyuna
eşit ve hücre piksel ayak izine göre LOD'lanıyor. Uzakta hücre metrelerce
büyüyor, tek hücre birçok pikseli birden kaplıyor ve ekranda iri parlak
bloklar çıkıyor. Yöntemin kendisi bozuk değil; eksik olan üst mesafe sınırı.

**Neden mesafe, ayak izi değil:** ayak izi bakış açısıyla da değişiyor,
grazing açıda patlıyor. Ayak izine bağlanan bir kapı, aynı uzaklıktaki düz
zeminle eğik yamacı farklı kapatırdı.

**Çözüm:** `SNOW_SPARKLE_FADE_START` 6 m / `_END` 16 m arasında smoothstep ile
sönüm. Yanında yoğunluk 0.006 → 0.0035 ve parlaklık 12 → 7: gerçek karda
parıltı seyrektir, kristallerin yalnız güneşi tam yansıtan azınlığı seçilir.

**Doğrulama:** öğle + açık hava, üç bakış açısı (yakın/orta/ufka yakın). Uzak
alan düzgün beyaz, yakında seyrek ince parıltı.


---

## 24 m'lik kare geri geldi (gece/şafak, koyu sahnede parlak alan)

**Kullanıcının ağzından:** "kare sorunu geri geldi", "illallah ettim şu kareden".

**Yanlış çıkan ilk şüpheliler:** kar maskesi (`snowMask` matematiği arazide 1
veriyor), `skyVis` (bölge dışında 1.0 dönüyor), tone mapping.

**ÖLÇÜM ARACI ÜÇ KEZ YALAN SÖYLEDİ — asıl ders bu:**
1. `LookController` her kare pozlamaya adaptasyon ekliyor. Mesh'i kapatıp
   ölçünce sahne ortalaması düşüyor, pozlama açılıyor ve arazi "parlak"
   görünüyordu.
2. Mesh'i kapatmak kar sistemini yarım bırakıyor; arazi bozuk değerlerle
   çizilip turkuaza patlıyordu. "Mesh kapalı" karşılaştırması geçersiz.
3. Üç farklı shader halinde aynı sayı (0.618) çıktı — shader değişikliklerinin
   ölçüme hiç yansımadığının işaretiydi.

Geçerli yöntem: mesh AÇIK kalır, ekran MERKEZİ (mesh) ile KENARI (arazi)
karşılaştırılır ve karar `ScreenCapture` görüntüsüyle doğrulanır.

**Gerçek kök — birden çok girdi ayrışması, en büyüğü DAĞ GÖLGESİ.** Arazi
`TerrainSunShadow` ile dağın kendi gölgesini uyguluyordu, kar mesh'i hiç
uygulamıyordu: veriler arazi materyalinin `UnityPerMaterial` bloğunda ve mesh
başka materyal kullanıyor. Güneş ufka yakınken arazi kendi gölgesinde koyulup
gölge tonuyla maviye çalıyor, mesh gölgesiz ve nötr kalıyordu.

Yanında üç ayrışma daha: yoğunluk (`_FallbackRhoN` yalnız yağıştan
güncelleniyordu, doku ayrıca sıkışıyor), kar sütunu (arazi 4 cm'lik nesne örtüsü
sabitini kullanıyordu), AO (arazi sabit 1.0 veriyordu).

**Ölçüm:** oran 1.61 → 1.08; gün boyu 11 saatte tarandı.

**Denendi ve geri alındı:** mesh'e alpenglow eklemek. Şafak oranını 1.02'den
0.25'e BOZDU — arazi ile aynı `gate` terimi kurulamadı.

**KALICI ÇÖZÜM — ikinci çizimi kaldırmak.** Yukarıdaki eşitlemeler oranı
1.61'den 1.08'e indirdi ama sıfırlamadı ve kare gözle görünmeye devam etti:
sınır oyuncuyla birlikte kaydığı için %8 fark bile yakalanıyor. Kök, iki ayrı
shader'ın AYNI düz yüzeyi çizmesiydi. Mesh artık yalnız yerel sapmanın
(`SNOW_LOCAL_MIN` üstü iz/sırt) olduğu yerde çiziliyor — ölçüldü: alanın
%0.6'sı. Düz alan tek shader'dan geldiği için orada fark imkânsız.
Doğrulandı: temiz Play oturumu, 06:26 / 12:00 / 17:02, oyuncunun üstünden
aşağı bakış — zemin baştan sona tek parça, yalnız iz görünüyor.

**Yan etki — iz kenarları havada kaldı.** Mesh iz-only yapılınca oluğun DUVARI
boşlukta bitiyordu: duvarın üst kenarı iz dışındaki düz kar yüzeyine
bağlanıyor, o yüzey artık çizilmediği için kenar asılı kalıyordu (kullanıcı
yandan bakarken bildirdi). Eşik komşuların en büyüğünden okunarak izin
çevresinde `SNOW_LOCAL_SKIRT_TEXELS` (3 teksel ≈ 7 cm) genişliğinde bir şerit
bırakıldı; duvar oraya oturuyor. Şerit düz alanda arazi kotunda
(`SnowSurfaceAt` sapma sıfırken `baseHeight` veriyor), basamak yapmıyor.

---

## "Kar tuttuğu zaman yer fazla bembeyaz, doku hiç görünmüyor"

**İlk şüpheli — YANLIŞ: doku bağlanmamış.** Dört fotogrametri seti içe
aktarıldı, `SnowSettings`'e atandı, `SnowManager` global olarak yayınlıyordu.
Çalışma zamanında doğrulandı: `_SnowSurfTazeColor(global)=T_SnowSurf_Taze_Color
2048x2048`. Ekranda hiçbir fark yoktu; güç 0 ile 3 arasında zemin luması
0.87873 ↔ 0.87753. Bağlantı sağlamdı, ölçüm yalan söylüyordu.

**Ölçümü bozan üç ayrı şey vardı — üçü de kapatılana kadar hiçbir sayı
güvenilir değildi:**

1. **`.hlsl` düzenlemesi shader'a ulaşmıyordu.** `MountainSurface.hlsl`
   değişince Unity `.shader`'ı yeniden derlemiyor. `AssetDatabase.ImportAsset(...,
   ForceUpdate)` çağrılmadan yapılan her ölçüm ESKİ kodu ölçüyordu.

2. **`SnowManager` global'i her karede geri yazıyordu.** Ölçüm için
   `Shader.SetGlobalFloat("_SnowSurfStrength", 30)` yazmak işe yaramıyor;
   bir sonraki `Update` ayardaki değeri (0.65) basıyor. Zorlama testi ancak
   `SnowSettings.asset` üzerinden yapılınca gerçek sonucu verdi: luma
   0.88 → 0.34. Sistem baştan beri çalışıyordu.

3. **Bulut gölgesi ekranda geziniyordu.** Aynı ayarla 40 saniye arayla alınan
   iki kare: luma 0.386 ve 0.437. Dakikalar arası A/B karşılaştırmaları
   bu yüzden anlamsızdı. Ölçüm sırasında `VolumetricClouds.shadows` kapatıldı.

**Gerçek sebep — ÜÇ AYRI KUSUR, üçü de ölçüldü:**

- **Albedo dokusu kendi parlaklığına bölünüyordu.** `SnowSampleSurface`
  içindeki `ortalama = (renk.r+renk.g+renk.b)/3` PİKSELİN KENDİ parlaklığıydı;
  ona bölünce her piksel 1'e normalize oluyor ve dokunun deseni tamamen
  siliniyordu, geriye yalnız renk tonu kalıyordu. Dokunun UZAMSAL ortalaması
  (doğrusal uzayda ölçülüp sabit olarak gömüldü) kullanılınca desen yerinde
  kaldı.

- **Kar dokusunun bilgisi renkte değil.** Ölçüldü: albedo haritalarının bağıl
  sapması %0.9–2.3 (kar beyazdır, renginde bilgi yoktur). Normal haritalarda
  rms eğim 0.06–0.09, tepe 0.28–0.45. Görüntüyü normal taşıyor; albedo yalnız
  ton veriyor.

- **KAR ACES'İN OMZUNDA EZİLİYORDU.** Bulut gölgesi kapatılıp tam güneşli
  yamaç ölçülünce: luma 0.921, sapma 0.0151. Yani dokunun ürettiği bütün fark
  255 seviyenin ~4'üne sığıyor ve ekranda TEK PARÇA BEYAZ olarak okunuyor.
  Pozlama 0.85 durak kısılınca luma 0.839, sapma 0.0274 — kabartı geri geldi,
  kar hâlâ sahnenin en parlağı. `LookSettings.clearDay.exposure` -0.15 → -1.0.

**Ayrıca ölçüldü — ortam ışığı yönsüz.** `RenderSettings.ambientProbe` yukarı
ve aşağı için AYNI değeri veriyor (0.223, 0.293, 0.420): PBSky'ın yer terimi
yok, gökyüzü ufkun altında da çiziliyor. Güneş kapatıldığında zemin sapması
0.00232'ye düşüyor — ortam terimi normalden hiç etkilenmiyor. Doğrudan ışığın
payı %40, ortamın %60. Kar bu yüzden şekilsizdi. Arazi tarafında ortam artık
gök görünürlüğüyle (`SampleSkyVisibility`) kısılıyor ve AO'nun kar altında
düzleştirilme payı 0.70'ten 0.55'e indirildi.

**Yan bulgu — `n.xy / n.z` patlıyor.** Normal haritanın mavi kanalı BC7
sıkıştırmasıyla sıfıra yaklaştığı teksellerde eğim sonsuza gidiyor ve ekranda
izole koyu mavi noktalar çıkıyor. Eğime fiziksel tavan kondu: kar 35 dereceden
dik mikro yüzeyde durmaz (`SNOW_SURF_EGIM_TAVANI = 0.7`).

---

## "İzin üstü doygun mavi/hardal leke, ortasında kara sürme"

**Belirti:** kar mesh'inin çizdiği iz bölgesi fotogrametri dokusunun aşırı
doygun hâline dönüştü; arazi normal görünürken yalnız İZ bozuktu.

**Gerçek sebep — materyal global'i ezmişti.** Yüzey dokusu iki yerde birden
tanımlıydı: `SnowLit.shader`'ın `Properties` bloğunda materyal özelliği olarak
ve `SnowManager`'da global olarak. Unity'de materyal özelliği global'i EZER.
Materyalde eski deneme değeri kalmıştı: `_SnowSurfStrength: 3`. Arazi global'i
(0.9) okuyordu, mesh materyaldeki 3'ü — yani aynı kar iki farklı güçle
çiziliyordu ve izin üstünde çarpan kanal kanal ayrışıyordu.

**Ayırt eden ölçüm:** materyal dosyasında `_SnowSurfStrength: 3`; global
`Shader.GetGlobalFloat("_SnowSurfStrength") = 0.9`.

**Kalıcı çözüm:** özellikler shader'dan ve materyalden tamamen silindi. Yüzey
dokusunun tek sahibi `SnowSettings` → `SnowManager` → global zinciri. İki
yüzeyin farklı doku/güç görmesi artık mümkün değil.

**Ayrıca kondu:** albedo çarpanına [0.8, 1.2] tavanı. Beyaz bir maddenin
uzamsal albedo değişimi %20'yi geçmez; deseni kabartı taşır. Tavan olmadan
yanlış bir güç değeri yüzeyi yine doygun lekeye çevirebilirdi.

---

## "Kar izi yine havada" — sonra "kar izi kayboldu"

**İlk şüpheli — YANLIŞ: etek (skirt) yetmiyor.** Daha önce izin duvarı boşlukta
bitiyordu ve çözüm `SNOW_LOCAL_SKIRT_TEXELS` ile çevresine şerit bırakmaktı. Bu
belirti ona benziyor ama sebebi başka: şerit yerindeydi, bant KOMPLE yükselmişti.

**Ölçüm — simülasyon sağlamdı.** Gövdenin altında enine kesit: düz karda
`Depth = 0.496 m`, oluğun ortasında `0.346 m`, kenarda `0.502-0.504`. Yani oyma
15 cm, yığılma 6-8 mm — hepsi makul. Bozukluk fizikte değil ÇİZİMDE.

**İKİNCİ ŞÜPHELİ DE YANLIŞ: "arazi kar sütununu eklemiyor".** Mesh köşesi
`groundY + h` yazıyor ve `h` sütunun tamamı (0.496 m) — arazi bunu eklemiyor
sanıldı ve sütun çıkarıldı (`- SnowBaseAt(uv)`). Sonuç: iz TAMAMEN KAYBOLDU.
Arazi zaten `SnowWorldCoverHeight()` kadar yükseliyor (`MountainSurface.shader`
dört geçişte de) — ölçüldü: 0.4862 m. Çıkarma mesh'i arazinin yarım metre
altına gömdü. Geri alındı.

**Ölçüm önce yapılsaydı iki tur birden kapanırdı:** izin tamamı taranınca en
yüksek yığın 1.5 cm, en derin oluk 16 cm çıktı. Yani ortada yarım metrelik bir
duvar hiç yoktu; görünen şey 16 cm'lik oluğun DUVARININ `clip()` tarafından
ortasından kesilmesiydi. Kesik yüz, kameraya yakın açıdan boşlukta duran bir
duvar gibi okunuyor.

**Gerçek sebep — sert `clip()` sınırı.** İki kusuru birden üretiyordu:
oluk duvarını ortasından kesiyor, ve mesh'in yerel sütunu ile arazinin dünya
sütunu birebir aynı olmadığı için (0.496 / 0.482) sınırda 1.4 cm'lik BASAMAK
bırakıyordu — kullanıcının "dağılmalarla arazi birleşiminde çok keskin
sınırlar" dediği şey bu.

**Çözüm — yumuşak varlık.** `SnowTrailPresence(uv)` sapmanın 0..1 arası yumuşak
ölçüsünü veriyor (dokuz örnek, `SNOW_LOCAL_SKIRT_TEXELS` yarıçapı 3'ten 9
teksele çıkarıldı). Yükseklik geçiş bandında dünya sütununa harmanlanıyor, yani
mesh sınıra vardığında tam arazi kotunda; kesme de varlığın 0.02'sinde, sapmanın
milimetre altında yapılıyor. Köşe, fragman yüksekliği ve merkezi fark AYNI
harmanı okuyor — biri okumazsa normal ile geometri farklı yüzeyi tarif eder.

---

## "Kar izi havada" — asıl sebep: GÖZ KARIN İÇİNDEYDİ

Üç ayrı tur boyunca mesh'in yüksekliği suçlandı ve üç formül denendi; hiçbiri
belirtiyi kapatmadı. Ölçüm sırası şöyle yürüdü:

| Şüpheli | Ölçüm | Sonuç |
|---|---|---|
| Zemin dokusu kaba | teksel 7.32 m | **yanlış** — bilinear okuma araziyle 3 cm içinde |
| Arazi sütunu eklemiyor | `SnowWorldCoverHeight` = 0.486 m | **yanlış** — arazi dört geçişte de yükseliyor |
| İz duvarı çok yüksek | en yüksek yığın 1.5 cm, en derin oyuk 16 cm | **yanlış** — duvar yok |
| Göz kotu | gövde 206.18, zemin 205.99, kar yüzeyi 206.48 | **DOĞRU** |

`CharacterController` arazi collider'ının, yani KAYANIN üstünde duruyor; arazi
çizimi ise kar sütunu kadar yükseliyor. Aradaki fark kadar (ölçüldü: 0.30 m)
göz kar yüzeyinin ALTINDA kalıyor. Oyuncu karın içinde yürüyor: sıyırtma
bakışta kamera yüzeyin altına düşüyor, kar mesh'i tepede asılı görünüyor ve
iz "havada" okunuyor. `SnowEyeHeight` gözü sütun kadar (batma payı düşülerek)
yükseltiyor; collider'a dokunulmuyor.

**İkinci bulgu — sastrugi yalnız mesh'te vardı.** `SnowSurfaceAt` yüksekliğe
±3.5 cm sastrugi ekliyordu; arazi köşeleri 7.3 m arayla olduğu için aynı sırtı
taşıyamıyor. Mesh'in çizildiği yer arazinin 3.5 cm üstüne çıkıyor ve kenarda
dışarı taşan üçgenler görünüyordu. Sastrugi geometriden çıkarıldı.

**Üçüncü bulgu — "yalnız izi çiz" kısıtı ÇUKURU GÖRÜNMEZ YAPIYOR.** Arazi
kesintisiz ve opak bir örtü; oluk onun altına iniyor. Ölçüldü: simülasyonda
oluk 0.24 m derin, ekranda hiç yok. Çukuru örten bir yüzey varken çukur
görünmez — mesh bölgenin tamamını çizmek zorunda. Kısıt kaldırıldı; kare
sorununun kökü olan "iki yüzey farklı görünüyor" durumu bugün doku ve
ışıklandırma birleştirilerek kurutuldu.

---

## "Dikdörtgen ayağım varmış gibi iz, dümdüz yürürken zigzag" — asıl sebep: KAR DİK DUVAR TUTUYORDU

Belirti üç ayrı şikâyet gibi göründü ve üç ayrı tur yaktı: (1) iz oluk değil
hendek, (2) düz yürürken zigzag/örgü, (3) kenarda dağılma yok. Üçü de aynı
sebebin belirtisi.

| Şüpheli | Ölçüm | Sonuç |
|---|---|---|
| Gövde merkezden kaçık | 1.5 mm sapma | **yanlış** — teksel boyunun onda biri |
| Adım titremesi (`lateralJitter`) | kaldırıldı, iz aynı | **yanlış** |
| Kenar bükümü dalga boyu | 3.5 → 3.0 cm, iz aynı | **yanlış tek başına** |
| Yakalamanın kapsama rampası | enine kesit ölçüldü | **DOĞRU** |

İzin enine kesiti (cm, teksel 2.34 cm):

```
0 0 0 0 0 0 0 0 0 0  10.9  19.0 19.2 19.2 19.2 19.2 19.2 19.2 19.1 19.2 19.0  15.3 5.1 0 0
```

Tek tekselde 0'dan 19 cm'ye çıkan **dik duvar**, sonra 10 teksel düz taban:
silindir damgası. Zigzag da buradan geliyordu — kenar bükümü (±0.9 teksel)
1 teksellik duvarı olduğu gibi kaydırıyor, iz lob lob çıkıyor. Büküm genliği
kaydırdığı özelliğin boyuyla aynı mertebedeyse özelliği yok eder.

Eksik olan **fizik**: gevşek kar dik duvar tutmaz, duruş açısına (~38°) kadar
göçer. `KRepose` eklendi — bir teksel komşusundan en fazla `tan(θ)×tekselBoyu`
kadar derin olabiliyor. Ölçülen yeni kesit:

```
0 1.1 3.0 4.8 6.6 8.5 10.3 12.1 14.0 15.8 17.6 19.4 21.3 | 22.0 x8 | 21.0 19.1 ... 2.7 0.8 0
```

Teksel başına 1.83 cm = tan(38°)×2.34 cm. Her yanda 12 teksellik omuz,
ortada 19 cm düz taban. Büküm/omuz oranı %7'ye düştü.

## "İz saniyeler içinde kayboluyor" — asıl sebep: SİMÜLASYON KARE BAŞINA BİRDEN ÇOK KEZ İLERLİYORDU

| Şüpheli | Ölçüm | Sonuç |
|---|---|---|
| Yağışın izi doldurması (formül) | fizik formülü 55 sn'de 0.08 cm | **yanlış** — ölçülen 2 cm |
| Eski `SNOW_FILL_GAIN = 900` artığı | kaynak temiz, tavan konsa da sönüm sürdü | **yanlış** |
| Rüzgârın doldurması | rüzgâr 0.54 m/s, eşik 4 m/s | **yanlış** — terim sıfır |
| Kalıcılık bloklarının bulanıklaştırması | `Unpack` yalnız bölgeye giren blokta koşuyor | **yanlış** |
| Adımın kaynağı | KDeform 525 karede 1602 kez | **DOĞRU** |

Ayırt eden ölçüm: çekirdeğe **çağrı başına sabit düşüm** (`trail.r -= 1e-5`)
konup kayıp kare sayısına bölündü. `RecordRenderGraph` her kamera için ayrı
koşuyor ve `Time.frameCount` bunların arasında ilerleyebildiği için kare
sayacına bakan koruma tutmuyordu; her çağrı TAM bir karelik `Time.deltaTime`
uyguluyordu. Adım artık geçen zamandan türüyor (`Time.time` farkı), kamera
sayısından bağımsız.

**Yan bulgu:** hızlanan yalnız iz değildi — oturma, kabuk, birikme ve sastrugi
de aynı katsayıyla akıyordu.

**Ölçüm tuzağı:** "kaç teksel dolu" sayacı bu belirtide YALAN SÖYLÜYOR.
Oyuncunun altındaki canlı gövde her kare yeniden oyuluyor, sayaç sabit kalıyor
ve "sönüm yok" diyor. Doğru araç tek teksel, tek kanal, zamana karşı.

**İkinci ölçüm tuzağı:** Unity odakta değilken Game view nadiren çiziliyor ve
kar simülasyonu saniyede ~1.2 kez koşuyor (ölçüldü: 314 çağrı / 266 sn).
Odaksız yapılan her iz ölçümü seyrek örneklenmiş çıkıyor.

---

## "İz kenarı tırtıl gibi, düzenli diş" — asıl sebep: DAMGA KADANSI

Ölçüm ortamında (Unity odakta değil) izin kenarı düzenli dişliydi. Üç ayrı
filtre denendi ve üçü de kesmedi:

| Şüpheli | Ölçüm | Sonuç |
|---|---|---|
| Kenar bükümünün teksel altı ölçeği | kaldırıldı, diş duruyor | **yanlış** |
| `SnowDentSmooth` köşegen-anizotropik | 3x3 eş yönlü yapıldı, diş duruyor | **yanlış** |
| Yumuşatma yarıçapı / eğim adımı | 3.0 ve 2 teksel, diş duruyor | **yanlış** |
| Damga kadansı | periyot HIZLA ölçekleniyor | **DOĞRU** |

Ayırt eden ölçüm — izin genişliğinin salınım periyodu:

| yürüme hızı | periyot | damga aralığı |
|---|---|---|
| 1.2 m/s | 20 teksel | 47 cm |
| 0.3 m/s | 7 teksel | 16 cm |

Izgara merdiveni olsaydı periyot hızdan BAĞIMSIZ olurdu. Hızla ölçeklendiğine
göre kaynak zamanda ayrık: gövde damga başına tek poz basıyor ve damgalar
birleşmeyecek kadar seyrekse birleşimin sınırı taraklanıyor.

İki damga arasındaki mesafe = hız / simülasyon frekansı. Simülasyon render'a
bağlı; Unity odakta değilken Game view saniyede ~2 kez çiziliyor (ölçüldü:
314 çağrı / 266 sn), o yüzden ölçüm ortamında damgalar 16-47 cm arayla düşüyor.
160 FPS'te aralık 0.75 cm, damgalar tamamen örtüşüyor.

**Ölçüm ortamı tuzağı:** odaksız editörde alınan HER iz görüntüsü bu tarağı
taşır. İz görünümü ölçülecekse ya odak verilecek ya da belirti bu kadansla
karıştırılmayacak.

**Ayrık yön ayrımı:** eksen boyunca (+X) yürüyüşte iz pürüzsüz çıkıyor, köşegen
yürüyüşte dişli. Damgalar eksen boyunca üst üste otururken köşegende ızgarayla
farklı kesişiyor ve tarak görünür hale geliyor.

---

## "Farklı açılara dönünce iz zigzaglı, ama HER ZAMAN değil" — asıl sebep: KALICILIK BLOKLARI CANLI İZİ EZİYORDU

Bu belirti günlerce kovalandı ve her turda yanlış katmana bakıldı. Ayırt eden
gözlem kullanıcıdan geldi: **iz gövdesi yuvarlak, yuvarlak bir kaynak düz
çizgide süpürülünce zigzag çıkamaz.** İkincisi de ondan geldi: **her zaman
olmuyor.** İkisi birlikte suçluyu render katmanından çıkarıp olay-tetiklemeli
bir yazıcıya indirdi.

| Şüpheli | Ölçüm | Sonuç |
|---|---|---|
| Gövde dikdörtgen | mesh `Sphere`, 0.30 x 0.24 x 0.30 | **yanlış** |
| Gövde oyuncudan kaçık, dönünce yay çiziyor | yatay ofset 0.0000 m | **yanlış** |
| Gövde yüksekliği salınıyor | kare kare 0.1 cm, tek yönlü | **yanlış** |
| Damga kadansı | 162 FPS'te damga aralığı 0.75 cm | **yanlış** |
| Izgara merdiveni / filtre yarıçapı | üç ayrı filtre denendi, diş durdu | **yanlış** |
| Relief ışını (paralaks) | HAM görünümde de zigzag var | **yanlış** |
| Kalıcılık bloklarının geri yüklemesi | eksene hizalı bloklar, aralıklı | **DOĞRU** |

**Ayırt eden araç:** `_SnowDebugDent` — ışıklandırma, paralaks ve relief ışını
kapalı, ekrana yalnız iz dokusunun kendi değeri. Zigzag o görünümde de vardı,
yani kaynak VERİDE. Bu araç olmadan render tarafında dört tur harcandı.

**İki ayrı hata:**

1. **Sınır dışı okuma.** `step = _BlockTexels / _BlockStored` tam sayı bölmesi:
   171/64 = 2. Paketleme bloğun 171 tekselinin yalnız 128'ini kapsıyordu; açma
   tarafında aynı bölme `src`'yi 85'e çıkarıp 64x64'lük tamponun dışını
   okutuyordu (indeks 5525, tampon 4096) ve okunan çöp doğrudan `trail.r`'a
   yazılıyordu. Eşleme orantılı hâle getirildi.

2. **Depo izi taşıyamıyor.** 4 m blok başına 64x64, yani teksel başına 6.25 cm
   — izin çizildiği çözünürlüğün ÜÇTE BİRİ. Önce `=` yerine `max` denendi ve
   YETMEDİ: iz hâlâ siliniyordu. Kalıcılık artık ize hiç dokunmuyor; yalnız
   kar durumunu (SWE, yoğunluk) hatırlıyor.

**Ayırt eden ölçüm:** kalıcılık geçici olarak kapatıldı. Aynı git-dön-gel
yürüyüşü — açıkken 206 teksel, kapalıyken 8362. Kapalıyken derinlik de aynı
(22.00 cm), yani silen tek şey oydu. Düzeltmeden sonra kalıcılık AÇIK: 8362
teksel, genişlik 19-22 teksel sabit.

**Neden aralıklı:** geri yükleme yalnız (a) daha önce paketlenmiş ve (b) bölgeye
YENİ giren bloklarda koşuyor. Paketleme kare başına bir blok ve asenkron geri
okumayla ilerliyor, yani hangi bloğun saklandığı zamanlamaya bağlı. Taze bir
yöne düz yürürken hiç tetiklenmiyor; dönünce, geri gelince veya 4 m'lik ızgara
çizgisi tekrar geçilince tetikleniyor.

**Tekrarlanabilir tetikleyici:** 10 m düz yürü, dön, aynı yoldan geri gel.

---

## Aynı belirtinin ASIL sebebi: SIRT ÇUKURUN DERİNLİĞİNDEN ÇIKARILIYORDU

Yukarıdaki kalıcılık kaydı gerçek bir hataydı ama belirtiyi kapatmadı. Sebep
şuydu: bütün ölçümler `trail.r` üzerinde yapıldı, oysa ekranda görünen
`trail.r - trail.g`. Ham teşhis görünümü de aynı farkı basıyor. `g` hiç
uzamsal olarak ölçülmemişti.

Ölçüldü, aynı iz, aynı kare:

```
trail.r genisligi   : 19 22 20 21 21 20 22 21 20 21 ...   (sabit)
(r - g) genisligi   : 12 12 12 13 14 17 20 20 19 19 19 17 13 13 12 12 ...
```

`r` boyunca sabit, `r - g` periyodik olarak 19'dan 12'ye çöküyor. Periyot
~33 teksel (77 cm).

`trail.g` karın YUKARI İTİLMİŞ kısmı — izin kenarındaki kabarma. Onu çukurun
derinliğinden çıkarmak iki ayrı geometriyi tek sayıya sıkıştırıyor, ve sırt tam
olarak omuzun üstüne düştüğü için omuzu siliyor: omuzda derinlik 8-12 cm, sırt
4 cm; fark eşiğin altına iniyor ve iz orada BİTİYOR.

Deseni periyodik yapan şey `KRim`: sırt, bulanık oymayı yakalanan HIZLA
kaydırarak örnekliyor (`SNOW_RIM_VELOCITY_BIAS`) ve `max` ile birikiyor. Gövde
ilerledikçe kaydırma yer değiştiriyor, sırt iz boyunca dalga dalga basılıyor.
Yön değişince desen de değişiyor — kullanıcının "farklı açılara dönünce"
demesinin sebebi bu.

**Düzeltme:** relief derinliği artık yalnız `trail.r`. Ölçüm sonrası genişlik
20-24 teksel, düzenli çöküş yok.

**Ders:** ekranda görünen büyüklük neyse ÖLÇÜLEN de o olmalı. `r` ölçülüp
`r - g` çizildiği için altı tur boyunca yanlış katmana bakıldı — filtre,
paralaks, damga kadansı, kalıcılık.

---

## "Zigzag HER ZAMAN olmuyor" — asıl sebep: SIRT GÖVDENİN OTURMA YÜKSEKLİĞİNE SIZIYORDU

Kullanıcı bunu üç kez söyledi ve üç kez geometriye bakıldı. Aralıklı olan bir
belirtinin sebebi geometri olamaz; geometri her zaman aynıdır. Aralıklı olan
şey ZAMANLAMADIR.

`SnowTrailBodyAlign` gövdenin oturma yüksekliğini şöyle alıyordu:

```
izOncesiYuzey = ss.Depth + ss.SinkDepth
```

`SnowSampler.Decode`'a bakılınca açılımı çıkıyor:

```
Depth     = baseHeight - trail.r + trail.g
SinkDepth = trail.r
toplam    = baseHeight + trail.g          <-- SIRT İÇERİDE
```

`trail.g` sırt: `max` ile birikiyor, konumu yakalanan HIZLA kaydırılarak
örnekleniyor (`SNOW_RIM_VELOCITY_BIAS`), 0 ile 4 cm arasında düzensiz zıplıyor.
Gövde onun peşinden inip çıkıyor, iz derinliği basamaklanıyor. Yön değişince
sırdın kaydırması da değişiyor — "farklı açılarda bozuluyor" bundan.

**Aralıklı olmasının sebebi:** bu örnek `SnowSampler`'ın asenkron geri
okumasından geliyor ve okuma **30 karede bir** tazeleniyor (`ReadbackInterval`).
Basamağın boyu = hız × 30 kare; görünürlüğü o kadansla yürüyüş hızının fazına
bağlı. Bazı hızlarda basamak teksel altına düşüyor ve belirti kayboluyor.

**Düzeltme:** `SnowSample.BaseHeight` eklendi — iz öncesi kar sütunu, yalnız
yağış ve oturmayla değişiyor, saniyeler ölçeğinde sabit. Geri okumanın
gecikmesi onu etkilemiyor.

**Ölçüldü:** izin genişliği düzeltmeden önce 12↔19 teksel arasında 33 tekselllik
periyotla salınıyordu; sonra 20 teksel, sapması 1-2.

**Ders:** ARALIKLI BELİRTİ ZAMANLAMAYI GÖSTERİR. Kullanıcı "her zaman olmuyor"
dediğinde aranacak şey geometri değil, kadans: geri okuma aralığı, asenkron
istek, olay tetiklemeli yazıcı.

---

## Zigzag/tarak — ASIL SEBEP: YOĞUNLUK, GEOMETRİ DEĞİL

Bu belirti günlerce geometri tarafında arandı ve orada değildi.

**Ayırt eden araç:** iki dokunun ayrı ayrı PNG olarak dışa alınması. Aynı
yürüyüş, L şeklinde: önce +X, sonra çapraz.

| doku | yatay kol | çapraz kol |
|---|---|---|
| `trail.r` (oyma derinliği) | kusursuz düz | **kusursuz düz** |
| `snow.g` (yoğunluk) | tek renk | **düzenli enine çizgi** |

Derinlik iki kolda da temizdi. Bütün ölçümler ona bakıyordu; belirti ise
yoğunluktaydı ve yoğunluk hem albedoyu hem pürüzlülüğü sürdüğü için ekranda
diş/tarak olarak çıkıyordu.

**Yoğunluğu bozan üç ayrı hata:**

1. **Sıkışma kare başına artışa bağlıydı.** `compact = f(trail.r - eskiOyma)`
   tekselin yoğunluğuna onun HANGİ KAREDE ilk basıldığını kazıyor. Eksen
   boyunca ilk-temas kümeleri düzgün sütunlar, çaprazda merdiven.

2. **Geri besleme.** `trail.r / baseH` yazıldığında döngü kapanıyor:
   `baseH = SWE×1000/ρ`, `snow.g` artınca `baseH` düşüyor, oran yükseliyor,
   `snow.g` yine artıyor. Ayak altındaki her karede bir kez dönüyor, yani son
   yoğunluk KAÇ KARE ayak altında kalındığına bağlı. Referans `SNOW_MAX_SINK`
   sabitine bağlandı. Ölçüm: yoğunluk aralığı 0.0102–0.1676 → 0.0102–0.0912.

3. **Sıkışma yalnız ANLIK TEMASTA yazılıyordu.** Blok `penetration > 0.0005`
   dalının içindeydi. Oyma diskin YUMUŞAK kapsama rampasıyla yazılıyor ve o
   rampa diskin sert silüetinden geniş; yoğunluk ise yalnız sert silüetin
   içinde güncelleniyordu. İki sınır farklı olunca ardışık damgaların birleşimi
   derinlikte pürüzsüz, yoğunlukta TARAKLI çıkıyor. Blok dışarı alındı;
   yoğunluk artık oymanın saf fonksiyonu.

**Dördüncü hata — kanal çakışması.** Geçiş sayacı `snow.a`'da tutuluyordu ama o
kanal aynı zamanda bozulma/tazelik: `KAccumulate` onu söndürüyor,
`MountainSurface` `yerelBozulma` olarak okuyup doku harmanına sürüyor. Sayaç
doğrudan görünüme sızıyordu. Sayaç tümden kaldırıldı.

**Ders:** EKRANDA GÖRÜNEN BÜYÜKLÜK NEYSE ÖLÇÜLEN DE O OLMALI. Altı tur boyunca
`trail.r` ölçülüp `snow.g`'nin sürdüğü bir belirti kovalandı. Şüpheli listesi
tükendiğinde yapılacak şey daha çok hipotez değil, ÇİZİME GİREN HER KANALI tek
tek dışa almaktır.

**Yanlış çıkan şüpheliler (hepsi ölçüldü):** gövde şekli (küre), gövde ofseti
(0.0000 m), gövde yüksekliği salınımı (yok), oyuncunun yolu (yanal sapma
±0.05 cm), damga kadansı, ızgara merdiveni, üç ayrı filtre yarıçapı, relief
ışın yürüyüşü (kapatıldı, diş durdu), iz eğiminin normale katılması
(kapatıldı, diş durdu), yüzey dokuları (kapatıldı, diş durdu).

## "Yuvarlak kaynak nasıl zigzag iz çıkarır" — çıkarmıyor, YOL zigzag

Kullanıcının sorusu doğruydu ve cevabı şu: iz sadık, yol değil.

Bütün ölçüm yürüyüşleri `cc.Move`'u DOĞRUDAN çağıran bir probla yapılmıştı ve
o yolun yanal sapması ±0.05 cm çıkıyordu — dümdüz. Gerçek `FirstPersonController`
üzerinden, sabit W girdisiyle aynı ölçüm yapıldığında iz kıvrılıyor, dönüyor,
hatta tam bir halka çiziyor.

**Ayırt eden ölçüm:** `InputSystem.QueueStateEvent` ile W enjekte edilip iz
dokusu PNG olarak dışa alındı. Aynı karede iki iz görünüyor: sentetik yürüyüşün
düz çizgisi ve denetleyici yürüyüşünün yılanı.

**Ders:** ÖLÇÜM ARACI GERÇEK YOLU ATLAMAMALI. Prob `cc.Move`'u çağırarak
denetleyiciyi baypas ediyordu; belirtinin kaynağı tam olarak baypas edilen
yerdeydi.

## "Zigzag" — dört gün, gerçek sebep çizim yolunda çıktı

**Kullanıcının ağzından:** "düz yürümeme rağmen iz yalpalıyor", "tarak gibi
çıkıyor", "çapraz giderken zigzaglaşıyor", "bazen oluyor bazen olmuyor".

**Yanlış çıkan şüpheliler (hepsi ölçüldü):** gövde şekli (küre), gövde ofseti,
gövde yüksekliği salınımı, oyuncunun yolu, damga kadansı, ızgara merdiveni,
üç ayrı filtre yarıçapı, relief ışın yürüyüşü, iz eğiminin normale katılması,
yüzey dokuları, yoğunluk kanalı, bölge kaydırması.

**Ölçüm tuzağı — BEŞ PROB AYNI ANDA.** Ölçüm bileşenleri üst üste eklenmişti ve
her biri `cc.Move` çağırıyordu; karakter kare başına beş kez hareket ediyor,
gövde hizası her çağrıda farklı hız görüyordu. "Periyot hızdan bağımsız, demek
ki uzamsal" sonucu bu kirli veriden çıktı ve bir turu yaktı. Probları tek tek
temizleyip TEK prob koşturulunca sayılar tamamen değişti.

**Ayırt eden ölçüm:** temiz zeminde tek prob, karakterin kendi yürüyüşü.
- yolun yanal sapması **1.5 mm = 0.06 teksel** — dümdüz
- iz genişliği 15 teksel, sapma ±1, üç farklı hızda aynı
- eksene paralel yürüyüşte 12–13, sapma ±1

Yani hareket kusursuzdu; bozan ÇİZİM yoluydu.

**Gerçek sebep — iki kaynak:**

1. **Rasterizasyon.** İz, her kare bir küreyi aşağıdan bakan ortografik bir
   yakalamaya çizip bulanık kapsamasını okuyarak açılıyordu. Kenar o zincirin
   üç ayrı yerinde teksel ızgarasına takılıyordu.
2. **Duruş yüksekliği gürültüsü.** `KRepose`'un ±%45 gürültüsü, 6 cm'lik duruş
   yüksekliğini modüle ederken kenarı **±1.5 teksel** oynatıyordu.

**Çözüm:** yakalama zinciri tamamen silindi; iz artık analitik
(`batma − (R − √(R²−d²))`, d = doğru parçasına uzaklık). Gürültü, duruş
yüksekliği gerçeğe çekildikten sonra (6 cm → 1.5 cm) ±0.4 teksel genlikle geri
kondu. Sayılar `RATIONALE.md`.

**Ders:** "kaynak yuvarlaksa iz nasıl zigzag olur" sorusunun cevabı, kaynağın
yuvarlak OLMASI değil, o yuvarlağın ızgaraya nasıl yazıldığıydı. Şekli
tartışmak dört tur yaktı; yazma yolunu ölçmek bir tur sürdü.

## Sırt hesaplanıyordu ama hiç çizilmiyordu

**Kullanıcının ağzından:** "kar yüzeyi ile iz arasındaki geçiş çok keskin,
yapay duruyor".

**İlk şüpheli (yanlış):** oyma profilinin kenarı fazla dik.

**Ayırt eden ölçüm — enine kesit (49 cm kar, +X yürüyüş):**
```
derinlik (mm):  0  13  47  82 117 152 176 188 196 201 204 ... 41  8  0
sırt     (mm):  0   0   0   0  32  34  38  40 ...  40  32 27 20 14  9  0
```
Oyma profili zaten yumuşaktı (her yanda 5 tekselllik rampa). Ama izin iki
yanında **4 cm'lik bir kabarma** vardı ve o ekrana hiç ulaşmıyordu:
`SnowDentAt` yalnız `trail.r` okuyor.

**Gerçek sebep:** `trail.g` (sırt) yazılıyor, dolduruluyor, eritiliyor — ama
hiçbir çizim yolu okumuyordu. Oluk düz kardan tek çizgiyle ayrılıyordu.

**Çözüm:** normal ve iz-içi gölge `trail.r − trail.g` okuyor; ışın yürüyüşü
`trail.r`'da kaldı. Ayrım şart: sırt ışın alanından çıkarıldığında omuzu
siliyordu (ölçüldü, `r − g` genişliği 19 tekselden 12'ye çöküyordu).

## "Dışarıdaki karda hiçbir detay yok" — kapı üç turluk işi yutmuştu

**Kullanıcının ağzından:** "iz ile dışardaki kar iki ayrı dünya gibi", "sadece
texture ekledik, başka hiçbir şey yapmadık dışardaki kara", "gözle görülür
hiçbir gelişme yok, dalga mı geçiyorsun".

**Yanlış çıkan şüpheli:** genliklerin küçük olması. Üç tur boyunca yer şekli
eklendi, genlikler artırıldı, oktav eklendi — ekranda hiçbir karşılığı olmadı.

**Ayırt eden ölçüm:** `MountainSurface.hlsl`'de çukur eğiminin normale
girdiği satır:

```hlsl
surface.normalWS = normalize(lerp(n, ..., saturate(izDerinlik * 20.0)));
```

Sastrugi, ripple, whaleback ve mikro rölyefin hepsi `SnowDentSlope` üzerinden
geliyordu ve bu ağırlıkla harmanlanıyordu. Düz karda `izDerinlik = 0`, yani
ağırlık **tam sıfır**. Eklenen her şey ekrana hiç ulaşmıyordu.

O kapı yalnız İZ için doğruydu — iz olmayan yerde çukur eğimi de olmamalı.
Ama yüzeyin kendi rölyefi karın olduğu her yerde var.

**Doğrulama:** genlik on katına çıkarılıp kare alındı; rölyef net göründü.
Yani bağlantı çalışıyordu, kapı kesiyordu. 1× değerlerde eğim 2.3° — gözle
görülmez.

**Ders:** BİR ALANIN EKRANA ULAŞTIĞINI ÖNCE DOĞRULA. Üç tur boyunca genlik
ayarlandı; tek bir "10× yap ve bak" ölçümü kapıyı bir turda buldu.

## Görünürlüğü genlik değil EĞİM belirliyor

**Ölçüm:** ilk yer şekli değerleri 1.5 m dalga boyunda ±4 cm genlikteydi.
Eğim `atan(2A/λ)` = **1.5°**. Ekranda hiçbir şey görünmüyordu.

Aynı genlik 17 cm dalga boyunda 8° veriyor. Dalga boyu kısaldıkça aynı genlik
çok daha dik.

**İkinci ölçüm:** güneş tepedeyken (12:00, SunHeight 0.88) 7°'lik eğim NdotL'yi
%1 değiştiriyor — yüzey yine düz okunuyor. Yatık ışıkta (SunHeight 0.27) aynı
yüzey net görünüyor. Bu FİZİK, hata değil.

Öğlen görünürlük ışıktan bağımsız bir terim gerektiriyor: çukurların ortam
örtmesi (`SNOW_SURFACE_AO`). O da yüzeyin YÜKSEKLİĞİNDEN geliyor, eğiminden
değil.

## Aynı alanı 36 kez hesaplamak

**Belirti:** "compiling shaders çok uzun sürüyor".

**Sebep:** yer şekilleri `SnowShadeHeightAt`'e konmuştu. O alanı
`SnowDentSmooth` 9 tap ile, onu da `SnowDentSlope` 4 tap ile okuyor: piksel
başına 36 çağrı × 6 gürültü = **180 örnek**.

**Çözüm:** yüzey rölyefi ayrı bir fonksiyonda, kendi 4 örnekli gradyanıyla —
24 örnek. Yükseklik alanına yalnız izin kendisi giriyor.

## Yüzey titremesi — DÖRT ayrı kaynak, dördü de rüzgâra bağlıydı

**Kullanıcının ağzından:** "siyahımsı alanlar değişip duruyor, acayip hızlı",
"zemin tir tir titriyor", "yavaşladı ama geçmedi", ve sonunda kendisi buldu:
"rüzgârın şiddetinden etkileniyor".

Yer şekilleri `dot(worldXZ, eksen)` üzerinden kuruluyor ve dağın ortasında
|worldXZ| yedi bin metre. Eksende ya da genlikte en küçük oynama ekranda
büyüyor.

**Kaynak 1 — eksen anlık rüzgârdan.** `normalize(_WindWS.xz)`; rüzgâr 0.6 m/s
iken vektör küçük ve yönü kare kare zıplıyor. Yumuşatılmış `_SastrugiWindDir`
zaten vardı, yanlış olan bağlanmıştı.

**Kaynak 2 — aliasing.** Prosedürel alan mip'lenmiyor; en ince oktav 1.6 cm ve
uzakta bir piksel bundan geniş alan kaplayınca örneklenemiyor. Her oktav artık
`fwidth` ile ölçülen piksel ayak izine göre sönüyor (Nyquist).

**Kaynak 3 — genlik anlık rüzgâr şiddetinden.**
`saturate((_WindSpeed − eşik) / aralık)`; şiddet kare kare oynadığı için
genlik oynuyordu. Fizik de bunu yasaklıyor: sastrugi GEÇMİŞ rüzgârın izi,
oluşumu saatler sürer.

**Kaynak 4 — yumuşatılmış yön bile sürükleniyordu.** Sakin havada anlık yön
rastgele döndüğü için 120 s'lik yumuşatma onu yavaşlatıyor ama durdurmuyordu.
İki düzeltme: yön yalnız kar taşıma eşiğinin (7 m/s) üstünde güncelleniyor,
VE kaynak `WindField.PrevailingDirection` (sabit hâkim yön), anlık hız yönü
değil.

**Ölçüm:** fırtınada (12.39 m/s) anlık yön `(1.00, 0.00, 0.01)`, hâkim yön
`(1.00, 0.00, 0.00)`. O `0.01`'lik sapma 7 km'lik koordinatta deseni **70
metre** kaydırıyor.

**Ders:** `WindField.PrevailingDirection`'ın kendi yorumu bu tuzağı zaten
kayıt altına almıştı ("bir hamlenin 0.14 radyanlık sapması deseni 980 metre
sürüklüyordu"). Aynı hata ikinci kez yapıldı çünkü yeni kod anlık kaynağı
okudu. DÜNYA KOORDİNATINA BAĞLI HER DESEN HÂKİM YÖNÜ KULLANMALI.

## Kar kalınlığının görsel karşılığı yoktu

**Kullanıcının ağzından:** "1cm, 5cm, 20cm, 50cm arasında bir fark yok, kar
yükselmiyor, hepsinde aynı".

**Ölçüm:** kar sütunu doğru hesaplanıyordu (1.0 / 5.0 / 20.0 / 50.0 cm).
Sorun veride değil çizimdeydi.

**Ölçüm aracı tuzağı:** ilk kontrast ölçümü komşu piksel farkıydı ve dört
derinlikte de aynı çıktı (1.83–1.86). O ölçü MİKRO rölyefi görüyor ve mikro
kar derinliğine bağlı değil. fBm'in dalga boyu 1.25 m; 32–64 piksellik
bloklarla ölçülünce fark ortaya çıktı.

**Eksik bağ:** yer şekilleri kar tabakasını OYAN şekiller, ondan derin
olamazlar. 1 cm karda 18 cm'lik sastrugi imkânsız. Genlik tavanı
`karDerinliği × 0.60`.

**İkinci bağ:** arazi oyuklarının gömülme payı sabit 0.55'ti — 1 cm kar da
50 cm kar da arazinin kabartısını aynı oranda gömüyordu. Santimetrelik örtü
metrelik çukuru kapatmaz.

**Sonuç (büyük ölçekli kontrast):** 1cm 4.65, 5cm 5.24, 20cm 6.63, 50cm 6.61.
20↔50 doygun; fiziksel olarak da doğru, o iki kalınlıkta yüzey benzer görünür.

---

## İzin kenarı "border" gibi koyu — geçişi yumuşatınca dairesel HALE çıktı

**Belirti (kullanıcı):** "kar izinde kenarlarda koyulaşma var, sanki border gibi.
dışardaki kar ile mükemmel bir şekilde aynı karmış gibi hissettirmiyor. geçiş
felaket kötü."

**İlk şüpheli (yanlış):** normalin döndüğü bandın darlığı.
`SNOW_CARVE_SMOOTH_TEXELS` 2.6 → 5.5 (6 cm → 13 cm) yapıldı. Ölçüldü: koyu hat
5-10 px, iz 150 px — bandı genişletmek görüntüyü değiştirmedi. Bant zaten sorun
değildi.

**İkinci deneme (durumu kötüleştirdi):** çöküntü kuyruğu uzatılıp sığlaştırıldı
(`SNOW_SETTLE_TAIL` 0.20 → 0.12, `SNOW_SETTLE_TAIL_LEN` 0.40 → 1.00). Geçiş
gerçekten yumuşadı ama yerine **kusursuz dairesel bir koyu hale** geçti — üstel
çürüme her yönde aynı mesafede bittiği için izin çevresine bir "glow" çiziyordu.
Yapaylık azalmadı, yer değiştirdi.

**Gerçek sebep:** kenarın yumuşaklığı kuyruğun UZUNLUĞUNDAN değil, kuyruğun
DÜZENSİZLİĞİNDEN geliyor. Düzgün bir fonksiyon ne kadar uzatılırsa uzatılsın
düzgün bir sınır çiziyor.

**Çözüm:** kuyruk menzili dünya uzayında gürültüyle modüle edildi
(`SNOW_SETTLE_TAIL_SCALE` 5 1/m = 20 cm dalga boyu, menzil 2-8 cm arası).
Gürültü MENZİLDE, genlikte değil: genlikte olsa kuyruk yer yer kesilip lekeli
bir desen verirdi.

**Ölçüm (tam pipeline karesi, 16:19):** iz içi 93/112/124, dış kar 120/97/117 —
aralıklar örtüşüyor. İz artık sistematik olarak koyu değil; kalan koyuluk
çukurun kendi gölgesi.

---

## Ölçüm aracı yalan söylüyordu: `cam.Render()` tonemap'i atlıyor

**Belirti:** kendi `RenderTexture`'ıma aldığım karelerde kar ortalaması 48/255
çıkıyordu — kar bu kadar koyu olamaz, ama "gece" değildi (saat 16:19,
`SunHeight` 0.38).

**Şüpheli (yanlış):** shader'ın gölge/occlusion terimlerinin fazla koyulaştırması.
Terimleri tek tek kısmaya başlanacaktı.

**Gerçek sebep:** `cam.targetTexture = rt; cam.Render();` post-process yığınını
(tonemap, pozlama) çalıştırmıyor. Okunan şey ham HDR'nin RGB24'e kırpılmışı.

**Ayırt eden ölçüm:** aynı kare, aynı saat, aynı açı —
`ScreenCapture.CaptureScreenshot` ile ortalama **163**, `cam.Render()` ile **48**.
3.4 kat fark; ölçüm aracının kendisi hipotez üretiyordu.

**Kural:** iz/kar parlaklığı karşılaştırması yalnız `ScreenCapture` ile yapılır.
`cam.Render()` sadece geometri/şekil bakmak için kullanılabilir.

---

## "Şafak ve ikindide zemin kapkara" — sistem hatası değil, DAĞIN KENDİ GÖLGESİ

**Belirti (ölçüm):** saat taramasında zemin parlaklığı şafak 9.1, ikindi 13.9,
öğle 202.7. Güneş ufkun üstündeydi (`SunHeight` 0.111 ve 0.165), yani gece
değildi. Karlı bir yüzey alacakaranlıkta gökyüzünün mavi ışığını yansıtır,
siyah olmaz.

**İlk şüpheli (yanlış):** kar shader'ının ambient okuması. Bu oturumda eklenen
`SNOW_SURFACE_AO` (0.50) ve `SnowReliefShadow` (taban 0.55) çarpımı 0.275 —
tam da eksik görünen faktör kadar. Terimler tek tek kısılacaktı.

**Ayırt eden ölçüm:** karın yanına standart URP Lit küre (albedo 0.85,
`Smoothness` 0.15). Kar shader'ı suçluysa küre parlak, kar karanlık olmalıydı.

| saat | küre | kar | kar/küre |
|---|---|---|---|
| şafak SH=0.111 | 7.6 | 10.8 | 1.41 |
| öğle SH=0.883 | 29.0 | 54.6 | 1.88 |
| ikindi SH=0.165 | 62.8 | 78.5 | 1.25 |

Kar HER saatte standart Lit'ten parlak. Kar shader'ı ışığı yutmuyor.

**Gerçek sebep:** ilk taramanın koştuğu konum yamacın gölgeli tarafındaydı.
Alçak güneşte dağın kendisi ön planı gölgeliyor — o karelerde uzak yamaç
aydınlık, yakın zemin karanlıktı; ikisi aynı karede duruyordu ve bu tek başına
cevaptı. Zirveye yakın açık bir noktada aynı saatte zemin 78.5.

**İki kez yanıltan ölçüm aracı:**
1. Prob koşusu bittiğinde `fpc.enabled = true` yapılıyordu; karakter serbest
   kalıp dağdan düştü ve sonraki ölçüm 384 m'de, sisin içinde koştu (kare
   baştan sona düz turkuazdı — sis, gökyüzü değil). Düzeltme: ölçüm boyunca
   konum her karede yeniden yazılıyor.
2. Kadraj-oranıyla seçilen ölçüm blokları kamera eğimi değişince gökyüzü yerine
   uzak araziye düşüyordu.

**Kural:** parlaklık şikâyetinde önce KONUM doğrulanır. "Karanlık" bir kare,
sahnenin karanlık olduğunu değil, kameranın karanlık bir yerde olduğunu
gösterebilir.

---

## Ölçüm oturumu kendini bozdu: aynı kare 106 → 21

**Belirti:** izin sırtı topaklara bölündükten sonra alınan kare, değişiklikten
önceki kareye göre beş kat karanlık çıktı (ortalama 106.2 → 21.2). İlk okuma
"yaptığın değişiklik sahneyi kararttı" idi.

**Eleme (tek-cevaplı test):** değişiklik `git stash` ile geçici geri alındı ve
**aynı kurulumda** kare tekrar çekildi. Topaksız 23.2, topaklı 21.2 — fark yok.
Sırt topaklaması suçlu değil.

**Ölçüm boyunca kayan üç durum, üçü de sırayla yanılttı:**

1. **Konum.** Ölçüm bitince `fpc.enabled = true` yapılıyordu; karakter serbest
   kalıp dağdan düştü. Sonraki kare 1718 m yerine 204 m'de, bambaşka bir ışıkta
   koştu. HUD'daki "Bulunduğun yükseklik" iki kareyi karşılaştırmadan **önce**
   okunmalı.
2. **Saat.** `tod.Paused = true` tek başına tutmuyor — ölçümün üç saniyesinde
   oyun saati geceye ilerledi. Saat çekim karesine kadar HER KARE yeniden
   yazılmalı.
3. **Bulut kapsaması.** Kendi dinamiğiyle dalgalanıyor: aynı noktada bir
   ölçümde %28, ötekinde %63, üçüncüde %82. `AtmosphereController.CoverageLocked`
   ile kilitlendi ama parlaklık düzelmedi — `AtmosphereSettings.minCoverage`
   (0.4) kilidin üstüne bir `max` uyguluyor.

**Kapanmadı.** Üç durum da sabitlendikten sonra bile kare 21 civarında kaldı,
`sacak.png`'nin 106'sına dönmedi. Kalan tek fark **Play oturumunun yaşı**: aynı
oturum on iki saattir açık ve saat, hava, kapsama defalarca zorla değiştirildi.
Bir sonraki adım Play'i kapatıp açmak ve aynı kareyi tekrar almak — o ölçüm
kullanıcının makinesinde yapılacak.

**Kural:** parlaklık karşılaştırması yapan her kare, karşılaştırılan iki kareyi
**aynı Play oturumunda ve arka arkaya** almalı. Saatler arayla alınmış iki kare
farklı sahnelerdir.

---

## "1 cm'de iz yok, 20/50 cm'de çok geniş" — İKİ ayrı sebep, ikisi de kâğıtta çıktı

**Belirti (kullanıcı):** "1cm'de ayak izi yok. 5cm'de çok hafif, neredeyse
gözükmüyor. 20 ve 50cm'dekiler ise çok büyük geniş izler."

**Ölçüm (kâğıtta, koddaki formüllerle):**

| kar | batma | yayılım | iz genişliği |
|---|---|---|---|
| 1 cm | 0.9 cm | 0.0 cm | 23 cm |
| 5 cm | 4.5 cm | 0.7 cm | 25 cm |
| 20 cm | 15.0 cm | 14.1 cm | 56 cm |
| 50 cm | 15.0 cm | 14.1 cm | 56 cm |

Şikâyetin üç maddesi de tabloda duruyor. Sebepler AYRI:

**Sığ kar — sıkışma yanlış referansla ölçülüyordu.** `KCompact`
`saturate(trail.r / SNOW_MAX_SINK)` yazıyordu: 1 cm karda 0.9 / 15 = **0.06**,
yani kar sütununun %90'ı ezilmiş olmasına rağmen yoğunluk hiç artmıyordu. Sığ
karda iz zaten derinlikten okunamaz (0.9 cm çukur, 23 cm genişlik = 4° eğim);
orada izi görünür kılan DOKU ve YOĞUNLUK.
Referans mevcut karın ezilebilir payı oldu: `min(SnowBaseHeight(swe,
_FallbackRhoN), SNOW_MAX_SINK)`. Oran 1 cm'de 0.06 → 0.90, yoğunluk 116 →
343 kg/m³ (izsiz kar 100).
Döngü kapanmıyor çünkü kalınlık `snow.g`'den değil SABİT referans yoğunluktan
çıkıyor — `snow.r` (SWE) yoğunluktan bağımsız. `SnowBaseHeight(snow.r, snow.g)`
yazılsaydı `snow.g` artınca kalınlık düşer, oran yükselir, `snow.g` yine
artardı.

**Derin kar — duvar kum gibi davranıyordu.** `SNOW_STAND_LOOSE` 4 cm'di, yani
kar 4 cm'den yüksek duvar tutamıyor sayılıyordu ve gerisi göçüp 14 cm omuz
açıyordu. Kar kohezyonludur: `h = 2c/(rho g)`, taze tozda c ≈ 300-1000 Pa ve
rho ≈ 100 kg/m³ → **60 cm ile 2 m**. Kar mağarasının kazılabilmesinin sebebi bu.
4 → 12 cm: yayılım 14.1 → 3.8 cm, iz **56 → 35 cm**.

**Yan etki yakalandı:** `SNOW_STAND_PACKED` 0.07'ydi ve LOOSE 0.12'ye çıkınca
sıralama tersine döndü — `lerp(LOOSE, PACKED, packed)` sıkışmış karda duvarı
ALÇALTIYORDU. Sıkışmış kar daha kohezyonludur; 0.200 yapıldı.

**20 ile 50 cm arasındaki batma farksızlığı hata değil.** İkisi de
`SNOW_MAX_SINK` tavanında; taşıma gücü karın YOĞUNLUĞUNA bağlı, derinliğine
değil. Aynı basınç aynı yoğunlukta aynı derinliğe batar.

**İkinci tur — 35 cm de geniş bulundu.** İzin yatay etki alanı üç parça:
bot yarıçapı 5.5 cm (gerçek bot ölçüsü, dokunulmadı), duvar yayılımı 3.8 cm,
çevre kuyruğu 7.0 cm. Kuyruk tek başına genişliğin beşte ikisiydi.
`SNOW_STAND_LOOSE` 12 → 14 cm (yayılım 3.8 → 1.3) ve `SNOW_SETTLE_TAIL_LEN`
0.70 → 0.55 (kuyruk 7.0 → 5.5). İz **35 → 27 cm**; sığ kar da 24 → 21 cm.
Kuyruğun kısalması haleyi geri getirmiyor çünkü saçaklanma menzilde, uzunlukta
değil.

---

## "İzin kenarları pikselimsi, border var gibi" — yumuşatma çekirdeği ALTÖRNEKLİYORDU

**Belirti (kullanıcı, fotoğrafla):** "izin kenarları niye koyu gri? border var
gibi? ayrıca niye pixelimsi bir yapı var kenarlarda? smooth geçiş nerede?"

**Ayırt eden ölçüm — basamak boyu:** fotoğrafta iz ~250 px ve gerçek genişliği
27 cm → 9.3 px/cm. Kenar basamakları ~20 px = **2.15 cm**. Ölçüldü:
`_SnowAreaSize` 24 m, `_SnowResolution` 1024 → teksel **2.34 cm**. Basamak tam
teksel ızgarası; blok gürültü (9 cm hücre) ya da damga kadansı olamaz.

**Gerçek sebep:** `SnowDentSmooth` 9-tap çadır çekirdeği ve
`SNOW_CARVE_SMOOTH_TEXELS` onun TAP ARALIĞI. Yarıçap 1 tekseli aştığında taplar
arasında örneklenmeyen teksel kalıyor — filtre yumuşatmıyor, ALTÖRNEKLİYOR ve
ızgarayla aliasing yapıp düzenli kare desen çıkarıyor. Değer bir önceki turda
2.6'dan **5.5**'e çıkarılmıştı: taplar 5.5 teksel aralandı, aralarında 4.5
teksel atlandı. Fonksiyonun kendi yorumu tavanı zaten söylüyordu ("yarıçap 1.5
tekselden büyük olamaz") ve yok sayılmıştı.

**Aynı hata bir turu da yakmıştı:** 2.6 → 5.5 yapıldığında "bandı genişletmek
görüntüyü değiştirmedi" diye ölçülmüştü. Değiştirmemesinin sebebi buymuş —
kazandığı yumuşaklığı aliasing olarak geri veriyordu. Bir düzeltmenin "etkisi
yok" çıkması, o düzeltmenin iki zıt etkisinin birbirini götürdüğü anlamına
gelebilir.

**Çözüm:** 1.0 = 3×3 komşu teksel, klasik çadır filtresi. Bant 2 teksel ≈
4.7 cm, 27 cm izin %17'si.

**Geçişin gerçek sınırı çözünürlük.** İz bugün 11.5 teksel geniş; bir ayak izini
11 tekselle temsil etmek yumuşak kenar için yetmiyor. Bandı büyütmek çare değil
(Nyquist), çözüm teksel boyunu küçültmek — `DECISIONS.md`.

**İKİNCİ KAYNAK — asıl kare köşeler bundandı.** Tap aralığı düzeltildikten
sonra kenar hâlâ basamaklıydı (kullanıcı ikinci fotoğrafla bildirdi: "kenarlarda
koca kareli köşeler var"). `SnowKemirilmisYaricap` yarıçapı iki bileşenle
modüle ediyordu ve biri `SnowWarpedBlockNoise`'du: `floor()` tabanlı, hücre
içinde SABİT, sınırda sıçrayan bir alan.

Ölçüldü: hücre `1/SNOW_EDGE_BLOCK_SCALE` = 9.1 cm, iz 27 cm. **İzin kenarı üç
blokla çiziliyordu** — "koca kare köşe" tam olarak bu. Domain warp hücreleri
eğriyor ama parça parça sabit bir alanı sürekli yapmıyor; kareliğin kaynağı
warp değil `floor`.

Blok bileşeni bir tur "kenar düzgün bir dalga oluyor, tomurcuk yok" diye
eklenmişti. O teşhis eksikti: sorun süreksizliğin yokluğu değil, ÖLÇEK
ÇEŞİTLİLİĞİNİN yokluğuydu — tek oktav sürekli gürültü düz bir dalga verir.
Üç oktava çıkarıldı (11.1 / 5.6 / 2.8 cm, toplam ±%31) ve blok tamamen
silindi. Fraktal kenar hem tomurcuklu hem köşesiz.

---

## Kare basamakların ASIL sebebi: normal bir TÜREV, bilinear filtreleme C1 süreksiz

**Belirti:** iz kenarı iki tur üst üste kare basamaklıydı. Kullanıcı ikisini de
fotoğrafla bildirdi ("niye pixelimsi bir yapı var kenarlarda", sonra
"kenarlarda koca kareli köşeler var"), sonunda "olmuyor, olmuyor" dedi.

**İki tur yanlış yere bakıldı.** Bulunanların ikisi de gerçek kusurdu ve
düzeltildi, ama basamağı ikisi de üretmiyordu:
1. `SNOW_CARVE_SMOOTH_TEXELS` 5.5 — çekirdek altörnekliyordu (aliasing).
2. `SnowWarpedBlockNoise` — 9.1 cm'lik `floor` hücreleri (koca köşeler).

**Gerçek sebep [KAYNAK: Wronski, "Bilinear texture filtering — artifacts,
alternatives, and frequency domain analysis"]:** normal bir TÜREV
OPERATÖRÜDÜR. Bilinear interpolasyon birinci dereceden; türevi teksel içinde
SABİT, teksel sınırında sıçrıyor (C0 sürekli, C1 süreksiz). Yükseklik
alanından türevle normal çıkarınca bu sıçrama doğrudan normale geçiyor ve
yüzey teksel boyunda düz parçalara ayrılıyor.

Yani basamak gürültüden, yumuşatmadan ya da çözünürlükten DEĞİL, alanın nasıl
okunduğundan geliyordu. Ne kadar oktav eklenirse eklensin, çekirdek ne kadar
düzeltilirse düzeltilsin türev parça parça sabit kaldığı sürece basamak durur.

**"Çözünürlüğü artıralım" yolu ölçümle çürüdü.** Batman: Arkham Origins aynı
işi `Min(512, ¼ × yüzey)` teksellik bir alanla yapıyor — bizim
`_SnowResolution` 1024'ün yarısı — ve kenarı yumuşak. Bir önceki turda
`DECISIONS.md`'ye yazılan "2048 → 200 MB VRAM" seçeneği bu yüzden gereksizdi;
sorun çözünürlükte değildi.

**Çözüm:** kübik B-spline filtreleme (C2 sürekli — örnek noktalarında hem değer
hem türev sürekli), dört bilinear tapla
[KAYNAK: Sigg & Hadwiger, "Fast Third-Order Texture Filtering", GPU Gems 2 §20].
16 tam tap yerine 4; eski 9-tap çadır çekirdeğinden de ucuz. B-spline ayrıca
yumuşattığı için çadır çekirdeğine gerek kalmadı, `SNOW_CARVE_SMOOTH_TEXELS`
silindi.

**Kural:** bir yükseklik alanından normal üretiliyorsa filtrelemenin C1
sürekliliği ZORUNLUDUR. Bilinear ile alınan her normal haritası teksel boyunda
faceted çıkar; bu ayarla düzelmez, filtre değiştirilir.

---

## Normal karışımındaki `lerp` — ölçüldü, sorun değil

**Şüphe:** Batman: Arkham Origins sunumu yönlü veriyi lerp'lemenin yanlış
olduğunu söylüyor ve geçiş için Reoriented Normal Mapping kullanıyor.
`MountainSurface.hlsl` kar normalini araziye `normalize(lerp(...))` ile
karıştırıyor.

**Ölçüm — nlerp'in slerp'ten en büyük açı sapması:**

| iki normal arası açı | sapma |
|---|---|
| 20° | 0.04° |
| 40° | 0.32° |
| 60° | 1.11° |
| 90° | 4.07° |
| 120° | 10.89° |

**Sonuç: yapılmadı.** Karışan iki normal BAĞIMSIZ değil — `snowNormal` doğrudan
`normalWS`'ten başlıyor, üstüne yalnız detay eğimleri biniyor. Aradaki açı kaya
bump'ı kadar, yani 20-40°: sapma **0.3°**, ekranda görünmez. RNM'in çözdüğü
problem tanjant uzayında bir detay normalini bir tabana OTURTMAK; buradaki
işlem iki normali karıştırmak ve nlerp o iş için yeterli.

**Ders:** bir kaynağın "şu yanlıştır" demesi, o yanlışın BİZİM sayılarımızda
görünür olduğu anlamına gelmiyor. Sapma önce ölçülür.

---

## Kare konturun ASIL sebebi: çukur gölgesindeki İKİ SERT EŞİK

**Nasıl bulundu:** F1'e beş izolasyon anahtarı kondu (kar dokusu karışımı,
relief paralaksı, çukurun kendi gölgesi, izin normale kattığı eğim, izin ham
hâli). Kullanıcı tek turda söyledi: **"çukurun kendi gölgesi yapıyormuş"**.

**Öncesinde üç tur yanlış yerde arandı** — yumuşatma çekirdeğinin
altörneklemesi, kenar gürültüsünün bloklu bileşeni, bilinear filtrelemenin
türev süreksizliği. Üçü de gerçek kusurdu ve düzeltildi, ama konturu hiçbiri
çizmiyordu. Dördüncü tahmin yapılmayıp anahtar kondu ve sorumlu bir turda çıktı.

**Sebep 1 — erken çıkış bir STEP fonksiyonuydu:**
```hlsl
if (dent < 0.005) return 1.0h;
```
`dent` bilinear filtrelenmiş bir yükseklik alanı; eşiğin geçtiği yer teksel
içinde lineer, yani sınır tam ızgaraya oturuyor. Gölge kare kenarlı başlıyor —
"koca kareli köşeler" bu. `smoothstep(0.002, 0.020, dent)` ile bir PAYA
çevrildi ve pay sonuca çarpılıyor. Üst sınır 2 cm, bir tekselin (2.34 cm)
altında: geçiş bandı izi şişirmiyor.

**Sebep 2 — engelin payı çukurun KENDİ derinliğine bölünüyordu:**
```hlsl
engel = max(engel, saturate((komsu - isinDerinlik) / max(dent, 1e-3)));
```
Sığ çukurda payda küçülüp oran anında 1'e fırlıyor: eşiğin bir milimetre
üstünde gölge zaten tam. İki sert geçiş üst üste biniyordu. Payda sabit bir
referans uzunluk oldu (`SNOW_RELIEF_SHADOW_REF` = 3 cm, ayak izi duvarının
mertebesi).

**Ders:** bilinear bir alan üzerindeki HER sert eşik teksel ızgarasını görünür
kılar. Filtrelemeyi düzeltmek yetmiyor — alanı tüketen tarafta `if`, `step`
veya sıfıra yakın bölme varsa ızgara oradan geri geliyor.

---

## Çukur gölgesi: ışın yürüyüşü silindi, horizon analitik oldu

**Belirti:** iki sert eşik yumuşatıldıktan sonra bile kullanıcı "çukurun kendi
gölgesi yokken hâlâ çok daha iyi görünüyor" dedi. Yani eşikler gerçek kusurdu
ama fonksiyonun kendisi de fazlaydı.

**Ölçüm — gölgenin fiziksel karşılığı:** çukurun görüş faktörü
`V = R²/(R²+d²)`, çoklu saçılım dolgusu `V / (1 − a(1−V))`, kar albedosu 0.85.

| iz | V | etkin | koyulaşma |
|---|---|---|---|
| 15 cm derin / 27 cm geniş | 0.448 | 0.844 | %16 |
| 10 cm | 0.646 | 0.924 | %8 |
| 5 cm | 0.852 | 0.975 | %3 |

Kar yüksek albedolu ve çok saçıcı; çoklu saçılım çukuru dolduruyor. Gerçek
ayak izi "koyu delik" değil, hafif gölgeli çanak.

**Asıl kazanç ışını atmak.** Beş adımlık yürüyüş, her adımda bir doku
örneklemesi ve `max` birleştirmesi — hepsi bilinear alan üzerinde ve hepsi
eşikli. Yerine ÇUKURUN HORİZONU: duvar eğimi `dent / SNOW_CAVITY_RADIUS`, bu
o noktadan görünen ufkun tanjantı; güneşin tanjantı bundan küçükse ışık
duvarın arkasında. Tamamen analitik, `dent`'in sürekli fonksiyonu, hiçbir eşik
ve hiçbir doku okuması yok — **basamak matematiksel olarak imkânsız**. Yirmi
doku okuması da gitti.

Ölçüldü (kâğıtta, R = 13.5 cm): 15 cm çukurda horizon 48°, yani güneş 48°'nin
altındayken taban doğrudan güneş görmüyor; 5 cm çukurda 20°.

**Ders:** bir görsel terim ışın yürütüyorsa, önce o terimin analitik bir
karşılığı olup olmadığı sorulur. Işın yürüyüşü hem eşik hem örnekleme
getiriyor; ikisi de ızgarayı görünür kılıyor.

---

## Gölge tavanı sabitti, atmosferden türetildi

**Durum:** ışın yürüyüşü analitik horizona çevrildikten sonra kullanıcı "çok
daha iyi oldu" dedi, ama tavan hâlâ elle konmuş bir sayıydı:
`SNOW_SHADOW_FLOOR = 0.55`, yani her havada ve her saatte %45 koyulaşma.

**Büyüklüğün fiziksel karşılığı:** gölgedeki yüzey doğrudan güneşi almıyor,
yalnız göğü ve çevresinden yansıyanı alıyor. Tavan = GÖK PAYI, yani difüz
ışınım / (difüz + direkt). Çağıran taraf bunu gerçek ışıktan hesaplıyor:
`Luminance(SampleSH(+Y))` ile `Luminance(mainLight.color) · sat(dir.y)`.

**Kar çok saçıcı** — gölgedeki kar gök payında kalmıyor, çevresindeki aydınlık
kar ona yansıtıyor. Tek yansımalık dolgu: albedo 0.85 × çevreyi görme payı
~0.5 = `SNOW_SHADOW_BOUNCE` 0.43.

| koşul | gök payı | tavan | koyulaşma |
|---|---|---|---|
| açık öğle | 0.15 | 0.52 | %48 |
| açık ikindi | 0.28 | 0.59 | %41 |
| alçak güneş 15° | 0.40 | 0.66 | %34 |
| parçalı bulut | 0.65 | 0.80 | %20 |
| kapalı hava | 1.00 | 1.00 | **%0** |

**Eski sabit meğer AÇIK ÖĞLE için doğruymuş** (0.52 ≈ 0.55); yanlış olan onu
her koşulda kullanmaktı. Artık bulut kapsaması arttıkça gölge kendiliğinden
siliniyor — kapalı havada flat light, dağcılıkta bilinen durum.

**`SNOW_SHADOW_LOW_SUN` silindi.** Alçak güneşte gölgeyi kısan bir telafi
terimiydi ve gerekçesi "ışın uzun yol alıyor, `engel` her yerde doyuyor"du.
Işın yürüyüşü kalkınca gerekçe de kalktı; üstelik tavan zaten güneş alçalınca
kendiliğinden yükseliyor. Gerekçesini yitiren terim geri eklenmez.

---

## HUD yanlış bilgi veriyordu: iki farklı kapsama aynı adı taşıyordu

**Belirti (kullanıcı, iki kare):** aynı yere bakan iki karede biri lacivert-siyah
ve zeminde belirgin koyu lekeler var, öteki açık gri. HUD ikisinde de "Bulut
kapsaması %0" ve "bu sütunda bulut yok" yazıyor.

**Ölçüm:** aynı anda `AtmosphereController.Coverage` = **%19**,
`CloudLayerProbe.CoverageAt(oyuncu)` = **%0**. İkisi farklı büyüklük ve HUD
yalnız ikincisini yazıyordu.

**Gerçek sebep:** koyu lekeler bulut gölgesi. Gölge oyuncunun ÜSTÜNDEKİ
buluttan değil, GÜNEŞ YÖNÜNDEKİ buluttan geliyor. Yerel kapsama o soruyu
hiçbir zaman cevaplamıyor — oyuncunun üstü açıkken güneş yönü kapalı olabilir.

**Bu ikinci sefer.** Kodun kendi yorumu birincisini kaydetmiş: "HUD %0
gösterirken ekranda bulut vardı ve 'bulut olmayan yerde çizgi var' sanıldı."
Kayıt vardı ama etiket düzeltilmemişti; aynı tuzak ikinci kez yanlış teşhise
götürdü.

**Çözüm:** iki satır ayrı ayrı yazılıyor —
`Bulut — üstünde` (yerel) ve `Bulut — gökte ... (gölge bundan gelir)` (küresel).

**Ders:** bir teşhis aracının yanıltıcı olduğu bir kez ölçüldüyse, kaydı yazmak
yetmiyor — ARACIN KENDİSİ düzeltilmeli. Yorumdaki uyarı ekranda görünmüyor.

---

## Alçak güneşte keskin kenarlı adacıklar — eksik olan KAR-KAR yatay transferi

**Belirti (kullanıcı, aynı kadrajdan dört saat):** 16:12 normal, 17:34 sepia,
17:49 ve 06:20'de zemin neredeyse siyah ve üzerinde keskin kenarlı açık
adacıklar.

**Elenen şüpheliler:**
- **Bulut gölgesi** — kullanıcı bulut %0 iken de aynı kareyi verdi.
- **Geometri** — Tri 10k → 433k çıkıyordu ama kadraj aynıydı; artış alçak
  güneşte gölge kaskadlarının uzayıp 200k'lık arazi mesh'lerini gölge
  geçişine sokması. Terrain'in kendi `shadowCastingMode`'u zaten Off.
- **Gölge haritası (shadow acne)** — F1'e anahtar kondu, kullanıcı kapattı:
  "hiçbir şey değiştirmiyor". Elendi.

**Gerçek sebep:** alçak güneşte düz zeminde NdotL ≈ 0.07; arazinin ±5°'lik
dalgası onu 0 ile 0.15 arasında gezdiriyor. Dolaylı ışık yetersizse NdotL'nin
sıfıra gittiği yerler TAMAMEN kararıyor, 0.15 olanlar görünüyor — keskin
adacıklar bu. Kar gerçekte kararmaz çünkü komşu aydınlık kardan ışık alır.

**Ölçüm:** aydınlık kar ~180, gölgeli ~15 → oran **0.08**. `SnowLighting.hlsl`
kendi kaydında aynı sayı duruyor: "zemin luması 0.0898 ↔ 0.8461, yani 1/9".
Kâğıtta olması gereken **0.49** (albedo 0.85, gölgeli nokta yarımkürenin
~yarısını aydınlık kar görüyor). Altı kat eksik.

**Neden mevcut terim yetmiyordu:** kar-gök çoklu yansıması vardı
(`1/(1−a·s)` = 1.29) ve bu farkı kapatmaya çalışıyordu. Yanlış yönü
modelliyor — eksik olan dikey değil **yatay** transfer. `SampleSH` de bunu
içeremiyor: SH statik, güneşin o anki katkısını taşımıyor, bu yüzden gölge
güneş ne kadar parlarsa parlasın aynı kalıyordu.

**Çözüm:** `ambient += güneşRenk · sat(yön.y) · albedo · SNOW_LATERAL_BOUNCE`,
katsayı 0.85 × 0.5 = 0.43. Gölgeye BAĞLANMIYOR — aydınlık kar da komşusundan
ışık alıyor; kar sahasının gerçekten parlak olmasının sebebi bu. Gölgeye
bağlansaydı telafi terimi olurdu.

Kâğıtta uçlar: öğle oran 0.37, şafak 0.42 — ikisi de ölçülü kar gölgesi
aralığına (0.4-0.6) oturuyor.

**Ders:** "gölge çok koyu" belirtisinde önce hangi YÖNDEN ışık geldiği sorulur.
Dikey (gök) terimi eklemek yatay (komşu yüzey) eksiğini kapatmıyor, yalnız
sayıyı biraz büyütüp gerçek sebebi gizliyor.

---

## Alçak güneşte keskin adacıklar — SASTRUGİ eğimi fizikten üç kat dik

**Belirti (kullanıcı, aynı kadrajdan dört saat):** 16:12 normal, 17:49 ve 06:20'de
zemin neredeyse siyah ve üzerinde keskin kenarlı açık adacıklar. Adacıklar
paralel şeritler hâlinde diziliydi.

**Sırayla elenen şüpheliler:**
- **Bulut gölgesi** — bulut %0 iken de aynı kare geldi.
- **Geometri** — Tri 10k → 433k çıkıyordu ama kadraj aynıydı; artış alçak
  güneşte gölge kaskadlarının uzamasıydı. Terrain'in `shadowCastingMode`'u Off.
- **Gölge haritası** — F1 anahtarıyla kapatıldı, değişmedi.
- **Kar örtüsü maskesi** — teşhis görünümünde kırmızı her yerde 1; maske ve
  `cavity` sıfırlanmıyor, kaya görünmüyor.
- **Wrap diffuse** — kapatılınca zemin kapkara oldu ama lekeler AYDINLIK kaldı.
  `wrapNdotL = (dot+0.55)/1.55` her zaman `saturate(dot)`'tan büyüktür, yani
  wrap kapatınca her yer koyulaşmalı. Lekelerin aydınlık kalması, orada
  diffuse dışı bir terimin taşıdığını gösterdi: normali güneşten kaçık ama
  AO'su yüksek yerler.

**Ayırt eden ölçüm:** yüzey normali teşhis görünümü (kırmızı = düz NdotL,
yeşil = wrap, mavi = N.y). Zemin mavi çıktı — N.y yüksek, NdotL ~0.13, alçak
güneşte yatay kar için doğru. **Lekeler BEYAZ** çıktı, yani orada NdotL ≈ 1.
Alçak güneşte NdotL'nin 1'e çıkması yüzeyin ~83° eğimli olması demek; düz
karda imkânsız. Normal bozuktu.

**Gerçek sebep — her rölyef teriminin eğimi ölçüldü:**

| terim | genlik | dalga boyu | eğim |
|---|---|---|---|
| fBm | 5.5 cm | 125 cm | 15° |
| ripple | 1.2 cm | 17 cm | 24° |
| **sastrugi** | **18 cm** | **60 cm** | **62°** |
| mikro A/B/C | 0.8–0.1 cm | 8–2 cm | 30–35° |

Sastrugi'nin H/L oranı 0.30; arazide ölçülen değer **0.05–0.10**. Üç kat dik.
60 cm aralıkla 62°'lik yüzler, yüzeyi testere dişine çeviriyor ve alçak güneşte
NdotL leke leke 1'e fırlıyor.

**Sabitin kendi yorumu hedefi doğru yazmış, sayı onu vermiyormuş:** "Yükseklik
18 cm, aralık 60 cm -> eğim 31°". Sinüs için en büyük eğim `2πA/L` =
2π×0.18/0.60 = 1.88, yani 62°. İki kat hata.

Kökeni ölçü karışıklığı: kaynaktaki "sivri uç aralığı 45-90 cm" sastrugi'nin
**enine** aralığı, rüzgâr yönündeki dalga boyu değil. Sastrugi rüzgâr yönünde
metrelerce uzar; enine ölçü `SNOW_SASTRUGI_WIDTH`'te zaten duruyordu.

**Çözüm:** `SNOW_SASTRUGI_LENGTH` 0.60 → 2.00 m. Eğim 29°, H/L 0.09 — hem
yorumun kendi hedefi hem arazi ölçümü.

**Ders:** bir yorumun yazdığı SONUÇ ile sabitlerin verdiği sonuç ayrı ayrı
doğrulanmalı. Burada gerekçe doğru, hedef doğru, sayı yanlıştı — ve yorum
doğru olduğu için üç tur boyunca kimse sabite bakmadı.

---

## Alçak güneşte keskin adacıklar — fBm'in RMS eğimi fizikten iki-üç kat fazla

**Belirti (kullanıcı, aynı kadrajdan dört saat):** 16:12 normal; 17:49 ve 06:20'de
zemin koyu ve üzerinde keskin kenarlı açık adacıklar.

**Ölçüm koşulu (kullanıcının verdiği):** bulut kapsaması 0, yağış 0, 50 cm kar,
saat 17:49, 206 m irtifa, yere bakış. Koşul HER KARE yeniden yazılıyor.

**Ölçüm aracının iki kusuru önce düzeltildi:**
1. `AtmosphereController.CoverageLocked` gerçekten kilitlemiyordu — taban
   (`minCoverage` / `DryCoverage`) kilidin üstünden geçiyordu. 0 yazılıp 0.40
   okunuyordu. Kilitliyken taban artık uygulanmıyor.
2. Play modda `AssetDatabase.ImportAsset` shader'ı geçici boşaltıyor; hemen
   ardından alınan kare sahnesiz çıkıyor. Import ayrı bir adıma alındı.

**Anahtar taraması — on üç terim, aynı karede, aydınlık/gölgeli oranı:**

| koşul | oran | fark |
|---|---|---|
| BAZ | 0.75 | — |
| **fBm kapalı** | **0.86** | **+0.11** |
| bounce kapalı | 0.73 | −0.02 |
| LOD kapalı | 0.76 | +0.01 |
| speküler / parıltı / sızma / AO / gölge rengi / ripple / sastrugi / mikro | 0.75 | ±0.00 |

Tek anlamlı katkı fBm. (Wrap kapalı 0.80 çıkıyor ama zemin 64 → 12'ye
düşüyor; ölçek değişimi, kontrast değil.)

**Sebep:** fBm'in dört oktavının RMS eğimi **35°**; arazide ölçülen kar yüzeyi
RMS eğimi 5-15°. Taban oktav tek başına 15.5°. Güneş 2.4°'deyken 35°'lik bir
yüzey NdotL'yi 0 ile 0.6 arasında gezdiriyor ve zemin keskin adacıklara
ayrılıyor.

| oktav | genlik | dalga boyu | eğim |
|---|---|---|---|
| 1 | 5.50 cm | 125 cm | 15.5° |
| 2 | 3.16 cm | 62 cm | 17.6° |
| 3 | 1.81 cm | 31 cm | 20.0° |
| 4 | 1.04 cm | 16 cm | 22.7° |

**Çözüm:** `SNOW_FBM_AMP` 0.055 → 0.022, RMS eğim 15° (ölçülmüş aralığın üst
ucu, rüzgârlı kar). Ölçüldü: oran **0.75 → 0.85**.

**Renk kontrolü:** aydınlık RGB 100/68/40, gölgeli 87/58/33 — R/B oranı 2.54 ve
2.66, yani AYNI malzeme. Lekeler kar/kaya sınırı değil, aynı yüzeyin aydınlanma
farkı; kahverengilik gün batımı ışığından.

**Açık kalan:** oran 0.85, lekeler azaldı ama tamamen gitmedi. fBm'i daha da
kısmak fizik aralığının altına iner. Kalan kontrastın kaynağı ölçülmedi.

**Ders:** genlik tek başına anlamlı değil — ölçü EĞİM, yani genlik/dalga boyu.
Dört oktavlı bir fBm'de ince oktavlar eğimi domine ediyor: gain 0.574 genliği
küçültüyor ama frekans iki katına çıktığı için eğim her oktavda artıyor.

---

## Keskin adacıkların ASIL sebebi: detay normalinin MAKRO katmanı — çift sayım

**Belirti:** alçak güneşte zeminde keskin kenarlı açık/koyu adacıklar.
Kullanıcı aynı kadrajdan dört saat gönderdi; 16:12 temiz, 17:49 ve 06:20 lekeli.

**ÖNCE YANLIŞ ŞEYİ ÖLÇTÜM.** Aydınlık/gölgeli oranını (kontrast) ölçüyordum,
oysa şikâyet kenarların KESKİNLİĞİYDİ. Doğru ölçü gradyanın 99. yüzdeliği.
Ölçü değişince sıralama tamamen değişti:

| terim | kontrast oranı | p99 gradyan |
|---|---|---|
| BAZ | 0.87 | 22.0 |
| fBm kapalı | 0.88 | 20.0 |
| doku normali kapalı | 0.93 | **3.0** |

fBm kontrastı etkiliyordu ama keskinliği değil.

**İki yanlış aday daha ölçümle elendi:**
- `SNOW_SURF_EGIM_TAVANI` 0.35 → 0.20: p99 22 → 22, **hiç değişmedi**
  (çoğu piksel zaten tavanın altında). Geri alındı.
- `_SnowSurfStrength` 0.35 → 0.00 (ayar assetinden): p99 22 → 22, yine
  değişmedi. Yani suçlu `normalSlope` değil, `SnowApplyDetailNormals`.

**Katman taraması — sorumlu tek katman:**

| koşul | p99 gradyan | oran |
|---|---|---|
| BAZ | 22.0 | 0.87 |
| **makro kapalı (8 m)** | **3.0** | **0.94** |
| mezo kapalı (0.6 m) | 22.0 | 0.87 |
| mikro kapalı (5 cm) | 20.0 | 0.87 |
| detay normali tamamen kapalı | 3.0 | 0.94 |

Makro'yu kapatmak, TÜM detay normalini kapatmakla aynı sonucu veriyor.

**Gerçek sebep — ÇİFT SAYIM.** Makro katman 8 metreye gerilmiş bir fotogrametri
detay dokusuydu ve yorumu "rüzgâr dalgaları" diyordu. Ama rüzgâr dalgalarını
`SnowYuzeyRolyef` zaten üretiyor: fBm (1.25 m), ripple (17 cm), sastrugi.
Aynı ölçek iki kez modelleniyordu — biri arazide ölçülmüş verilerden
(Filhol & Sturm), öteki fotogrametri dokusundan. Dokunun taşıdığı keskin
desenler 8 m'ye gerilince metre boyunda adacıklar olarak okunuyordu.

**Çözüm:** makro katman silindi. Mezo (0.6 m) ve mikro (5 cm) duruyor — yakın
plan detayı onlarda ve ikisi de kenar sertliğine katkı vermiyor.

**Doğrulama, üç saat, sabit koşulda (bulut 0, yağış 0, 50 cm kar, 206 m):**

| saat | p99 gradyan | oran |
|---|---|---|
| 17:49 | 3.0 | 0.94 |
| 12:00 | 2.0 | 0.99 |
| 06:18 | 2.0 | 0.83 |

Başlangıç 17:49: p99 **28.0**, oran 0.75.

**Ders:** şikâyeti doğru büyüklüğe çevirmeden ölçme. "Keskin kenar" kontrast
değil GRADYAN. Yanlış büyüklüğü ölçtüğüm sürece fBm suçlu görünüyordu ve iki
tur onun üstünde harcandı.

---

## Yukarıdaki "makro katman" teşhisi ÇÜRÜDÜ — ölçüm aracı yalan söylüyordu

**Ne oldu:** makro katman silindikten sonra TEMİZ bir Play oturumunda ölçüldü
ve p99 gradyan **21** çıktı — anahtar taraması 3 vaat ediyordu.

**Sebep:** anahtar taraması (`k_*`) `AssetDatabase.ImportAsset`'ten sekiz
saniye sonra alınmıştı. Play modda import shader'ı geçici boşaltıyor; o
pencerede alınan kare yarım yüklü sahneyi gösteriyor ve düşük gradyan veriyor.
Aynı tuzak `SNOW_SURF_EGIM_TAVANI` ve `_SnowSurfStrength` ölçümlerini de bozmuş
olabilir — ikisi de "etkisiz" çıkmıştı.

**Temiz oturumda ölçülenler (ImportAsset yok, Play kullanıcı tarafından açıldı):**

| durum | zemin | p99 gradyan | oran |
|---|---|---|---|
| başlangıç (fBm 0.055, makro var) | 64.0 | 28.0 | 0.75 |
| fBm 0.022 + makro silinmiş | 63.4 | 21.0 | 0.84 |
| üstüne mezo 0.28 + mikro 0.22 | 63.4 | 22.0 | 0.84 |
| bulut %40 (kullanıcının kareleri) | 41.6 | 15.0 | 0.82 |

**Duran gerçek:** fBm genliğini fizik aralığına çekmek p99'u 28 → 21 indirdi
(%25). Makro katmanın silinmesi çift sayım gerekçesiyle doğru ama ölçülmüş
katkısı ayrıştırılamadı. Mezo/mikro genliğini yarıya indirmek p99'u HİÇ
değiştirmedi (21 → 22) — o değişiklik geri alındı.

**Kalite MEDIUM:** mikro katman (`_SNOW_QUALITY_HIGH`) bu sahnede hiç
derlenmiyor. Mezo derleniyor ama genliği keskinliği etkilemiyor, yani
keskinlik eğim GENLİĞİNDEN değil bir SÜREKSİZLİKTEN geliyor.

**Açık kalan:** adacıklar duruyor. Bir sonraki adım stokastik döşemenin hücre
sınırları (`SnowTriangleGrid` + `SnowCellOffset`) — genlikle ölçeklenmeyen tek
aday orası.

**Ders:** Play modda `AssetDatabase.ImportAsset` çağırdıktan sonra alınan
HİÇBİR kare güvenilir değil. Shader değişikliği edit modda
`Logs/refresh.trigger` ile derletilir, sonra Play açılır. Bu tuzak bu oturumda
üç ayrı teşhisi çürüttü.

---

## `_SNOW_QUALITY_*` hiçbir shader'da tanımlı değil — üç katman ölü kod

**Ölçüm:** `grep -rn "SNOW_QUALITY" Assets/Shaders Assets/Snow/Shaders` —
`#pragma multi_compile` listesinde bu keyword YOK. `Shader.IsKeywordEnabled`
global olarak MEDIUM döndürüyor ama shader onu hiç görmüyor.

Sonuç: `SnowDetailNormals.hlsl`'deki üç blok hiç derlenmiyor —
- mezo (0.6 m, `#if MEDIUM || HIGH`)
- mikro (5 cm, `#if HIGH`)
- ezilmiş (0.25 m, `#if HIGH`)

Bu, mezo genliğini 0.50 → 0.28 yapmanın p99'u neden hiç değiştirmediğini
açıklıyor: o satır zaten çalışmıyordu.

**p99 gradyan ölçümü ±4 birim gürültülü.** Aynı ayarda (`surfStrength` 0.35)
iki ayrı koşuda 18 ve 22 okundu. Bu büyüklükteki farklar anlamsız; ancak
28 → 17 gibi büyük değişimler güvenilir.

**Bu oturumda ölçülmüş gerçek kazanç tek:** `SNOW_FBM_AMP` 0.055 → 0.022,
RMS eğim 35° → 15° (arazide ölçülen 5-15°). p99 28 → 21.

`surfStrength` (kar dokusu normali) katkısı gürültünün içinde kaldı:
0.35 → 0.00 bir koşuda 22 → 17, ötekinde 18 → 17.

**Açık kalan:** adacıklar duruyor ve kaynağı bulunamadı. Sıradaki ayırıcı
ölçüm: normali tamamen düzleştirip (N = +Y) p99 ölçmek. 17 → ~2 düşerse
kaynak normal; 17'de kalırsa kaynak albedo/doku rengi ve normal tarafında
aramak boşuna.

---

## ÇÖZÜLDÜ: yüzey rölyefinin toplam RMS eğimi fizikten iki-üç kat fazlaydı

**Ayırt eden hesap — her terimin GERÇEK eğimi (taban çarpanlarıyla), 50 cm karda:**

| terim | genlik | dalga boyu | eğim |
|---|---|---|---|
| fBm oktav 1 | 2.20 cm | 125 cm | 6° |
| fBm oktav 4 | 0.42 cm | 16 cm | 10° |
| ripple | 0.42 cm | 17 cm | 9° |
| **sastrugi** | **4.50 cm** | **60 cm** | **25°** |
| **mikro A** | 0.44 cm | 8 cm | **18°** |
| **mikro B** | 0.22 cm | 4 cm | **21°** |
| **mikro C** | 0.08 cm | 2 cm | **18°** |
| | | **TOPLAM RMS** | **39°** |

Arazide ölçülen kar yüzeyi RMS eğimi **5-15°**. Güneş 2.4°'deyken 39°'lik bir
yüzey NdotL'yi sıfırdan geçirip zemini keskin adacıklara ayırıyor.

**İki kaynak, ikisi de taban değerinden:**

1. `SNOW_SASTRUGI_BASE` 0.25 → **0.08**. Rüzgâr sıfırken bile 4.5 cm sastrugi
   bırakıyordu (25° eğim, yüzeyin en dik tek bileşeni). Sabitin kendi yorumu
   "sakin havada yüzey plane bed'e yakın" diyordu ama sayı onu vermiyordu.
   Yeni genlik 1.44 cm, eğim 8.6°.

2. `SNOW_MICRO_AMP_A/B/C` × 0.4. Üç oktavın RMS'i 33°'ydi — tek başına en
   büyük kaynak. Dalga boyları 8/4/2 cm, yani yakın planda ekranda birkaç
   piksel; dik eğim doğrudan keskin gradyana dönüşüyor. Yeni RMS 13°.

Toplam RMS **39° → 21°**.

**Ölçüm (17:49, bulut 0, yağış 0, 50 cm kar, 206 m, sabit kadraj):**

| durum | zemin | p99 gradyan | oran |
|---|---|---|---|
| başlangıç | 64.0 | **28.0** | 0.75 |
| fBm 0.055 → 0.022 | 63.4 | 21.0 | 0.84 |
| + sastrugi tabanı ve mikro | 65.1 | **3.0** | 0.88 |

**Kare geçerlilik kriteri:** zemin parlaklığı 55-80 arasındaysa sahne tam
yüklü. `ImportAsset` sonrası bozuk pencerede zemin 220'ye fırlıyor ve p99
yapay olarak düşük çıkıyor — bu oturumda üç teşhis o yüzden çürüdü.

**Ders:** yüzey rölyefinde tek tek terimlere değil TOPLAM RMS EĞİME bakılır.
Her terim ayrı ayrı "makul" görünüyordu; yedi tanesinin karesel toplamı fiziği
üçe katlıyordu. Ve genlik tek başına anlamsız — ölçü `2πA/λ`.

**İkinci tur — RMS 21° hâlâ fazlaydı.** Kullanıcı "çok ufak kalmış" dedi;
kalan noktaların rengi ölçüldü (R/B 2.55 ↔ 2.35), AYNI malzeme çıktı — kaya
değil, aynı yüzeyin gölgeli tarafı. Bütün genlikler 0.7 ile çarpıldı:
fBm 0.015, ripple tabanı 0.24, sastrugi tabanı 0.055, mikro 0.0022/0.0011/0.0004.
Toplam RMS **15°** — arazide ölçülen aralığın (5-15°) üst ucu.

| durum | p99 gradyan | nokta/zemin |
|---|---|---|
| başlangıç | 28.0 | — |
| RMS 21° | 3.0 | 0.54 |
| RMS 15° | **2.0** | **0.80** |

Noktalar hâlâ var ama zemine yaklaştı (0.54 → 0.80) ve alanları %1'in altında.

## "Hafif uzak zemin detaysız gözüküyor"

**Belirti.** Oyuncunun yakınında kar detayı var, 10-20 m ötede yüzey düzleşiyor,
35 m'de tamamen düz. Yürüdükçe detay geliyor. Saat 17:39, alçak güneş.

**İlk şüpheli yanlıştı.** Yüzey dokusunun mesafe kapısı (`SNOW_SURF_FADE`
8/28 m) sanıldı; 30/120'ye uzatıldı ve **ekranda hiçbir değişiklik olmadı**.
`_SnowSurfStrength = 0.35` ile o katmanın payı zaten küçük.

**Gerçek sebep.** `SnowYuzeyEgim` ve `SnowMikroEgim` piksel ayak izini
`max(fwidth(worldXZ.x), fwidth(worldXZ.y))` ile ölçüyordu — pikselin **en
uzun** ekseni. Yere bakarken o eksen bakış yönünde patlıyor.

Kâğıtta (kamera 1.7 m, düz zemin):

| Mesafe | Bakış açısı | `max(fwidth)` |
|---|---|---|
| 10 m | 9.6° | 5.8 cm |
| 20 m | 4.9° | 23 cm |
| 40 m | 2.4° | **92 cm** |

40 m'de uzun eksen 92 cm, dik eksen hâlâ 4 cm. `SnowOktavAgirligi` Nyquist
kesimini o 92 cm'e göre uyguluyor ve bütün oktavları kapatıyor:

| Oktav | Dalga boyu | Kesildiği mesafe |
|---|---|---|
| Mikro | 8.3 cm | ~8 m |
| Ripple | 17 cm | ~12 m |
| Sastrugi | 60 cm | ~23 m |
| fBm | 1.25 m | ~34 m |

**Ayırt eden ölçüm.** Doku kapısını 4× uzatmak hiçbir şey değiştirmedi;
kesim mesafeleri ise kâğıtta belirtiyle birebir örtüştü.

**Aynı hata daha önce sparkle'da bulunmuştu** ve orada tavanla kapatılmıştı
(`SNOW_SPARKLE_MAX_FOOTPRINT`, "fwidth sıyırtma açıda patlıyor"). Rölyefte
kapatılmamıştı — aynı sınıfın ikinci kullanım yeri.

**Düzeltme.** `SnowPikselBoyu` = `sqrt(fx · fy)`, geometrik ortalama. Doku
filtrelemesi bu durumda anizotropik davranıyor: mip kısa eksenden seçiliyor.
Dik bakışta iki eksen eşit olduğu için davranış `max` ile aynı kalıyor —
titreme kontrolü ("zemin tir tir titriyor") o açıda ölçülmüştü ve bozulmuyor.

**BU DÜZELTME DE BELİRTİYİ KAPATMADI.** Kullanıcı bildirdi: "bi değişiklik
olmadı ki". Doku kapısı gibi bu da gerçek bir kusurdu ama sebep değildi.

**Gerçek sebep yapısaldı: normal haritası silüete ve örtüşmeye katkı
vermiyor.** Sıyırtma açıda bir yüzeyin görünümünü tamamen o ikisi belirliyor;
hangi LOD ayarı yapılırsa yapılsın düz bir üçgen düz görünür. Çözüm
`RATIONALE.md` → "Kar yüzeyi neden geometri oldu": Terrain üçgenleri
tessellation ile bölünüp gerçek yükseklik kadar kaydırılıyor.

**Ders.** Aynı belirtiye üç ayrı LOD/fade düzeltmesi yapıldı ve üçü de
tutmadı. Üçüncüde durup "bu ayar sınıfı belirtiyi kapatabilir mi" diye
sorulmalıydı — cevap hayırdı, çünkü hiçbir gölgelendirme ayarı silüet
üretemez.

## "Bu nasıl bir gölgelendirme aklım almıyor"

**Belirti.** Kar yüzeyinde keskin kenarlı, düz, onlarca metre büyüklüğünde
koyu lekeler. Ara ton yok — yüzey iki tonlu. Bulut gölgesine benziyor ama
değil.

**İlk şüphelim yanlıştı.** Lekelerin boyutuna bakıp "hiçbir kar katmanı 50 m
ölçekte desen üretmiyor, bulut gölgesi olmalı" dedim. Kullanıcı düzeltti:
*"bulutla alakası yok. kar yüzeyinin kendi gölgesi o!"*

**Gerçek sebep.** `MountainSurface.hlsl`, çukur ortam örtmesi:

```hlsl
half cukur = saturate(-surface.snowSurfaceHeight / SNOW_FBM_AMP);
```

`SNOW_FBM_AMP` = 1.5 cm ve o **yalnız fBm katmanının** genliği.
`snowSurfaceHeight` ise bütün katmanların toplamı. Drift (15 cm) ve sastrugi
(20 cm) arazi ölçüsüne çıkınca payda 10 kat küçük kaldı:

| Çukur | Eski `cukur01` | Yeni |
|---|---|---|
| 2 cm | **1.00** | 0.07 |
| 10 cm | **1.00** | 0.34 |
| 20 cm | **1.00** | 0.68 |

**Her çukur tam doygun.** Ara ton kalmıyor, yüzey iki tonlu lekelere
bölünüyor, komşu doygun çukurlar birleşince lekeler onlarca metreye çıkıyor.

**Ayırt eden ölçüm.** Lekelerin boyutu (20-50 m) hiçbir katmanın dalga boyuna
uymuyordu — bu beni yanlış yöne itti. Doğru soru "hangi katman bu ölçekte
desen üretir" değil, **"hangi terim doygunlaşıp komşuları birleştirir"**
olmalıydı. `saturate` doyduğunda desen ölçeği kaybolur; çıktının ölçeğine
bakarak girdinin ölçeği aranmaz.

**Düzeltme.** Payda rölyefin gerçek tavanı: `kar derinliği ×
SNOW_BEDFORM_DEPTH_FRAC`. `SnowYuzeyRolyef` yüksekliği zaten oraya kırpıyor —
iki taraf aynı ölçeği kullanıyor.

**Sınıf.** Bu bir NORMALİZASYON PAYDASI hatası. Aynı sınıfın diğer örneği:
`izDerinlik / SNOW_RELIEF_MAX_DEPTH` (iz için, doğru — iz derinliği gerçekten
o sabitle sınırlı). Bir payda değiştiğinde payın ölçeği de değişmiş olabilir.

## Su çizgisi basamaklı, testere gibi — deniz mesh'i inceltilince DEĞİŞMİYOR

**Kullanıcının ağzından:** "kıyı çizgisi basamaklı."

**İlk şüpheli — YANLIŞ:** deniz mesh'inin quad boyu. Kıyıdaki quad'lar 2 m ve
basamaklar da o mertebede görünüyordu.

**İkinci şüpheli — YANLIŞ:** kıyı köpüğü. Köpük kapatıldı, basamaklar aynen kaldı.

**Gerçek sebep:** arazi heightmap'inin kendi çözünürlüğü. 4097 teksel / 30 000 m
= **7.3 m per teksel**. Su düzlemi o çözünürlükteki üçgenlerle kesişiyor ve görünen
su çizgisi o kesişimin kenarı.

**Ayırt eden ölçüm:** aynı kare üç durumda çekildi —
köpüksüz, dalgasız ve High preset (deniz mesh'i 0.25 m quad, 656k üçgen).
**Üçünde de basamaklar birebir aynı.** Deniz tarafında hiçbir şey değiştirmiyor.

**Yapılan:** kıyı köpüğünün kenar gürültüsüne ~16 m ölçekli ikinci bir oktav eklendi.
Basamağı kaldırmıyor, kamufle ediyor — o ölçekten küçük bir gürültü orada hiçbir şey
örtmüyor. Kalıcı çözüm arazi tarafında (bkz. `DECISIONS.md`).

## Ölçüm aracı bütün kademelerde aynı sabiti döndürüyor

**Belirti:** dalga alanı testinde üç kademe de `-23.203130` verdi; shader hatası sanıldı.

**Gerçek sebep:** `Graphics.CopyTexture` + `Texture2D.GetPixels` **farklı belleği
konuşuyor**. Kopya GPU tarafını günceller, `GetPixels` CPU tarafını okur ve o hiç
yazılmamıştı. Okunan sayı `Texture2D`'nin başlangıç çöpüydü.

**Ayırt eden ölçüm:** `AsyncGPUReadback` ile aynı dokular okununca değerler anlamlı çıktı.

**Ders:** GPU dokusu CPU'ya ancak readback ile iner. `CopyTexture` bir GPU→GPU kopya.

## GPU süresi hep 0.000 ms — "deniz bedava"

**Gerçek sebep:** `Recorder.gpuElapsedNanoseconds` yalnız Profiler kayıt yaparken dolu;
kapalıyken **sessizce 0** dönüyor. Sıfır ile "ölçülemedi" ayrılamıyordu.

**Yapılan:** `SeaRuntimeState.GpuTimingAvailable` bayrağı. Bayrak false iken o sayıya
bakılmıyor.



## "Kıyı full köpük. Saçma sapan." — rüzgâr 0,5 m/s'ken

**İlk şüpheli — yanlış:** kıyı köpüğü bandı (`shoreFoam`). Ölçüldü ve elendi: bandın
alfası 8 m'den sonra 0,000. Bandın tamamı zaten dar — deniz alanının yalnız %2,2'si
1,7 m'den sığ, ve ölçülen kıyı profilinde su çizgisinden 2 m derinliğe ortanca **44 m**
var. Kıyı köpüğü ekranı dolduramaz.

**Gerçek sebep — kırılma köpüğü (`breakT`) dalganın boyu yerine PİKSELİN KOTUNU
okuyordu.** Formül `waveH = 2 * |y - _SeaLevelY|` yazıyordu. İki sonuç, ikisi de ölçüldü:

| |y − deniz kotu| | köpük alfasının bittiği mesafe |
|---|---|
| 0,05 m | — (yok) |
| 0,20 m | 6 m |
| 0,40 m | 20 m |

Göz 1,7 m'de: 20 m mesafe ufkun 4,9° altında, ekrandaki suyun **%87'si** o mesafeden
yakında. Yani sığlıkta her piksel "kırılıyor" sayılıyor ve kıyı tek parça beyaz sayfa
oluyordu. İkinci sonuç: çukur da tepe kadar uzak olduğu için ölçüt **hava durumundan
bağımsızdı** — ölü sakinlikte fırtınayla aynı kırılma.

**Ayırt eden ölçüm:** aynı kıyı kesitinde üç köpük teriminin alfası ayrı ayrı hesaplandı.
`shoreFoam` 8 m'de sıfırlanıyor, `whitecap` Jacobian eşiğinin (0,55) altına inmiyor,
`breakT` 20 m'ye kadar 0,75 veriyordu. Tek başına ekranı dolduran terim buydu.

**Düzeltme:** `waveH` artık yerel derinliğe göre sığlaştırılmış **Hs** (belirgin dalga
yüksekliği). Hs havayı taşıyor: 0,5 m/s'de 0,10 m, 20 m/s'de 3,96 m. Aynı kesitte köpüğün
bittiği mesafe 0,5 m/s'de 3 m, 3 m/s'de 29 m, 8 m/s'de 88 m, 20 m/s'de 120 m'nin ötesi.

## Sığ suyun altındaki kum kuru çiziliyordu

Aynı ekran görüntüsünün ikinci yarısı. Köpük bittiği yerde bile su kremsi-yeşil bir
tabaka olarak duruyordu.

**Sebep:** `seaWet` bandının bir tabanı var (`_SeaWetBandM`, 1,6 m) ve o tabanın altındaki
arazi ISLAK SAYILMIYOR. Bandın yorumu "altı zaten suyun altında, denizi çiziyor" diyordu;
deniz altındakini **gösteriyor**. Ölçüm: 25 m açıkta ışın suda 2,8 m yol alıyor, sönüm
(0,30 / 0,08 / 0,05 per m) ile geçirgenlik 0,43 / 0,80 / 0,87. Kuru kum albedosu
(0,61 / 0,54 / 0,43) neredeyse hiç sönmeden göze geliyordu.

**Düzeltme:** deniz kotunun altı tanım gereği ıslak. Bandın tabanı duruyor — onun işi
sudan YUKARIDAKİ zeminin ıslak sayılmasını durdurmak, ki eklendiği belirti oydu.


## "Alttan bir beyazlık gelip gidiyor, 2 saniyede bir"

**Sebep — sayı doğrudan hesaplanabildi, şüpheli aramaya gerek kalmadı.** Kıyıdaki
koşu-yukarı fazının periyodu doğrudan Tp:

    phase = t * (2pi / Tp)   ->   sin periyodu = Tp

Ve Tp fetch sınırlı JONSWAP bağıntısından geliyordu, yani YALNIZ rüzgâr denizinden.
0,5 m/s'de:

    omega_p = 22 * (g^2 / (U10 * F))^(1/3) = 2,39 rad/s   ->   Tp = 2,63 s

Kullanıcının "2 saniyede bir" dediği sayı buydu.

**Asıl mesele Tp değil, Tp'nin nereden geldiğiydi.** Spektrumun İKİ parçası var
(`SeaSpectrum.compute`): rüzgâr denizi ve ölü dalga. Ölü dalganın kendi tepe periyodu
10 saniye ve enerjisi rüzgârdan bağımsız — sakin havada denizde duran tek şey o. Hs ve Tp
ise yalnız birinci parçadan okunuyordu.

**Ölçüm — iki parça ayrı ayrı integre edildi:**

| U10 | eski Tp | gerçek Tp | eski Hs | gerçek Hs |
|---|---|---|---|---|
| 0,5 | 2,63 s | **9,97 s** | 0,10 m | **0,74 m** |
| 3 | 4,78 s | 9,97 s | 0,59 m | 1,17 m |
| 8 | 6,62 s | 6,63 s | 1,58 m | 2,31 m |
| 20 | 8,99 s | 8,98 s | 3,96 m | 4,91 m |

Rüzgâr sertleştikçe iki sayı birbirine yaklaşıyor — çünkü orada rüzgâr denizi ölü
dalgayı zaten örtüyor. Hata sakin havada, tam kullanıcının baktığı yerde.

**Düzeltme:** `SeaSpectrumMoments` iki parçayı da sayısal integre ediyor;
Hs = 4·sqrt(m0), Tp = toplam spektrumun tepesi. Aynı kapı Hs'i de düzeltiyor, yani
kırılma köpüğü de doğru sayıyı okuyor.


## "Denizin sınırlarında smooth geçiş yok, keskin çizgilerle ayrılıyor"

**Elenen şüpheliler — hepsi F1 izolasyon anahtarıyla, kullanıcı tarafından:**

| Şüpheli | Nasıl elendi |
|---|---|
| Kıyı ıslaklığı, dantel, arazi karı | Üçü de kapatıldı, kenar durdu |
| Dalgalar (mesh'in dikey yer değiştirmesi) | Kapatıldı, kenar durdu |
| **Deniz yüzeyi** | Kapatılınca kenar GİTTİ |

**Su çizgisi olamayacağı ölçüldü.** Batimetriden derinlik=0 konturu izlendi: 1646 metrelik
bir parçada uçları birleştiren doğrudan **393 m** sapıyor, yani kendi boyunun %24'ü kadar
kıvrılıyor. Kıyı düz değil.

**Gerçek sebep:** `SeaSampleDepth` arazi kutusunun dışına çıkar çıkmaz
`_SeaDeepWaterDepth` (200 m) döndürüyordu. Arazi kenarındaki gerçek derinlik ölçüldü:

| kenar | su oranı | derinlik |
|---|---|---|
| batı | %100 | 21,9 – 29,8 m |
| doğu | %100 | 12,9 – 30,0 m |
| güney | %100 | 26,1 – 29,5 m |
| kuzey | %100 | 23,9 – 28,8 m |

Ortalama 25,4 m. Bir teksel ötesi 200 m. **Sekiz katlık basamak** — ve arazi kutusu kare
olduğu için basamağın izi kusursuz düz bir çizgi, iki çizgi de bir köşede birleşiyor.
Derinlik soğurmayı, sığlaşmayı ve kırılmayı sürdüğü için basamak renkte doğrudan
görünüyor.

**Düzeltme:** `saturate(uv)` en yakın kenar tekselini okuyor (sınırda değer sürekli),
oradan 4000 m'de derin suya iniyor — %4,4 eğim, bir kıta yamacı.

## Denizde ızgara/dama deseni — köpükte

**Kullanıcının keşfi:** desen köpükten geliyor.

**Sebep — hash dünya ölçeğinde çöküyor.** `SeaHash21` ve `SeaHash22` koordinatı
katlamadan `frac(p * 127.1)` yapıyordu. Kıyı x = 12000'de; `frac()`'in girdisi 1,5 milyon
ve float 24 bit mantis taşıyor.

**Ölçüm — 64×64 hücrelik blokta kaç FARKLI değer çıkıyor (4096 mümkün):**

| hücre başlangıcı | mevcut hash | katlanmış hash |
|---|---|---|
| 0 | 1040 | 2378 |
| 1000 | 157 | 2449 |
| 9600 | 39 | 2265 |
| 12000 | 39 | 2402 |
| 15000 | **20** | 2377 |

Dört bin hücrede yirmi değer bir kafestir. `MountainHash` aynı duvara aynı ölçekte
çarpmış ve orada çözülmüştü: önce 512'lik periyoda katla, sonra KÜÇÜK çarpanlarla hash'le.
Aynı tarif denize taşındı.

## F1'de son üç onay kutusuna tıklanmıyor

**Sebep:** `EndColumn()` sütunun sonuna `GUILayout.FlexibleSpace()` koyuyordu. Kaydırma
görünümü içinde bu boşluğun üst sınırı yok; yerleşim geçişi ile çizim geçişi kontrollerin
yerinde anlaşamıyor. En uzun sütunun alt kontrolleri bir yerde çiziliyor, tıklamayı başka
yerde alıyordu. `FlexibleSpace` silindi — işi zaten dikey grubun kendisi yapıyor.


## "Denizin bittiği sınır sert. Köpüğün sınırı yumuşak, denizinki değil."

Kullanıcı ayrımı kendi koydu: aynı karede kıyı köpüğünün zeminle sınırı dağınık ve
yumuşak, denizin kendi sınırı kesme gibi. İki kenar da aynı yerde, aralarındaki tek fark
onları üreten ifade.

**İki sebep var, ikisi de sınırın kendisinde:**

1. **Kesme, düz bir eğri üzerinden yapılıyordu.** `clip(depth)` yalnız derinlik alanına
   bakıyor, o alan da 7,32 m'lik yükseklik haritasının düzgün interpolasyonu — yani
   çizilmiş bir eğri. Köpük ise `SeaFoamNoise` ile kırılıyor; farkı yaratan buydu.
   Kesme artık AYNI gürültüyle kaydırılıyor.

2. **Fresnel son piksele kadar 1 kalıyordu.** Sıyırma açısında yüzey tamamen ayna, yani
   su çizgisinin bir yanında tam gökyüzü yansıması, öbür yanında kum vardı. Bu sıçrama
   tek piksele düşüyordu ve "kesik kenar" görüntüsünü veren şey oydu.

   Milimetrelik bir su filmi ayna değildir — arayüzü kuracak ortam yok.
   `SEA_SHORE_FADE_DEPTH` (0,60 m) boyunca yansıma ve parıltı sönüyor; geriye sudan
   görünen zemin kalıyor, ki çizginin öbür yanındaki piksel zaten onu gösteriyor. İki
   yaka aynı renkte buluşuyor.

**Bir tur eksik kaldı — yansımayı söndürmek yetmedi.** Kenar gürültüsü su çizgisine
düzensiz bir ŞEKİL verdi ama kesme hâlâ ikili, ve çizgide duran en güçlü şey KÖPÜKTÜ:
bir yanda beyaz, öbür yanda kum, arada hiçbir şey. Yansımayı kısmak ona dokunmuyordu.

**Kapatan terim, en sonda, tek:**

    color = lerp(refracted, color, smoothstep(0, SEA_SHORE_FADE_DEPTH, depth));

`refracted` bu pikselin arkasındaki sahne rengi — üstüne deniz çizilmeseydi zeminin
görüneceği renk. Kıyıda kaydırma koruması kırılma sapmasını iptal ettiği için TAM O
piksel. Derinlik sıfıra giderken ona doğru harmanlanınca çizginin iki yakası aynı değerde
buluşuyor, kesmenin gösterecek bir şeyi kalmıyor. Yansıma, parıltı, su rengi ve köpük
hepsi birlikte geçiyor.

Alfa değil: yüzey bilerek opak çiziliyor (TAA'da hayalet ve sıralama sorunu, spec 12.6).
Bu, "su yoksa suyun rengi de yok" cümlesinin kendisi.

Ölçülen %5,8 kıyı eğiminde devir bandı kumda **10 m**: 0,9 m'de %2, 5,2 m'de %50,
10,4 m'de %100. Su çizgisini oradan arazinin danteli devralıyor.

**Dalga boyu denetimi:** kenar gürültüsünün genliği 0,06 m derinlik; ölçülen %5 kıyı
eğiminde su çizgisini 1,2 m oynatıyor. Onu üreten gürültünün kendi özellik boyu
(`_SeaFoamBreakupTiling` = 0,35) 2,9 m — genlik özellik boyunun altında, yani büküm
lapaya dönmüyor.


## "Aşağıdan gelen beyazlık hâlâ çok hızlı gidip geliyor, ayrıca çok ileri gidiyor"

Tp 2,63 s'den 9,97 s'ye çıkmıştı ama kullanıcı hâlâ hızlı diyordu. İki ayrı hata vardı,
ikisi de kodu okuyunca görülüyor.

**1. Miktar, faz olarak yayımlanıyordu — döngü ikiye katlanıyordu.**

`SeaManager` şunu yazıyordu:

    ShoreFoamPhase = sin(2pi t / Tp) * 0.5 + 0.5      // bu bir MIKTAR

Shader ise onu faz sanıp içine koyuyordu:

    surge = 0.5 - 0.5 * cos(2pi * phase)

Girdi bir Tp boyunca 0,5 → 1 → 0,5 → 0 → 0,5 süpürüyor; çıktı 1 → 0 → 1 → 0 → 1 oluyor.
Yani **swash Tp/2'de, 5 saniyede bir** gidip geliyordu ve dönüşlerde sıçrıyordu.

Artık doğrusal bir 0..1 testere dişi yayımlanıyor, surge tek yerde kuruluyor ve ıslak bant
ile köpük aynı ifadeyi okuyor.

**2. Çıkış yüksekliği sabitti — hava ne yaparsa yapsın 1,1 m.**

Ölçülen %5,8 kıyı eğiminde bu 19 metrelik kumsal demek, ve ölü sakinlikte de o kadar
gidiyordu. Stockdon R2% deniz durumunu okuyor:

    R2% = 1.1 ( 0.35 b sqrt(Hs L0) + sqrt(Hs L0 (0.563 b^2 + 0.004)) / 2 )

| U10 | Hs | Tp | R2% | yatay çıkış | dönüş periyodu |
|---|---|---|---|---|---|
| 0,5 | 0,74 m | 10,0 s | 0,69 m | 12 m | 10,0 s |
| 3 | 1,17 m | 10,0 s | 0,87 m | 15 m | 10,0 s |
| 8 | 2,31 m | 6,6 s | 0,81 m | 14 m | 6,6 s |
| 20 | 4,91 m | 9,0 s | 1,60 m | 28 m | 9,0 s |

Öncesi: dönüş 5,0 s, yatay 19 m — sabit.

**Üçüncü tutarsızlık, aynı adımda:** shader su seviyesini `max * phase` ile yükseltiyordu,
yani düzgün tırmanıp geri kopan bir testere; yanındaki köpük ise kosinüsü izliyordu. Bir
dalga için iki şekil. İkisi de artık `surge`'ü okuyor.


## "Köpükler niye düzenli desenlere ve aralıklara sahip?"

**İki yanlış hipotez, ikisi de ölçümle elendi — kod yazılmadan:**

| Hipotez | Nasıl elendi |
|---|---|
| Değer gürültüsünün ızgarası eksene hizalı | Oktavları döndürmek özilinti tepesini 0,36'dan yalnız 0,32'ye indirdi |
| Kenarın gerçek bir periyodu var | Korelasyon boyunun ötesindeki en büyük yerel tepe 0,11–0,23. Periyot yok |

**Araç bir kez yalan söyledi ve düzeltildi.** İlk özilinti taraması "her kıyı yönünde 2,0 m"
dedi — o taranan en kısa gecikmeydi, yani tepe değil kısa menzil korelasyonuydu. İkinci
sürüm önce korelasyon boyunu buluyor, tepeyi ancak onun ötesinde arıyor.

**Gerçek sebep: kenarın TEK ölçeği vardı.** 1 km'lik kıyı boyunca bandın kenarı izlendi ve
farklı pencerelerle düzleştirildikten sonra kalan salınım ölçüldü:

| pencere | kalan salınım |
|---|---|
| 1 m | 0,29 m |
| 8 m | 0,71 m |
| 32 m | 1,08 m |
| 64 m | 1,16 m |
| 128 m | **1,17 m** |

32 metreden sonra büyümüyor: o ölçekten büyük hiçbir yapı yok. Her diş aynı boyda çıkıyor
ve göz bunu tekrar olarak okuyor — matematiksel bir periyot olmasa bile.

**Düzeltme ölçüyle seçildi.** Düz bir 5 oktavlı fBm denendi ve ELENDİ: enerjiyi yayıp ince
dokuyu öldürdü (1 m'de 0,29 → 0,05). Kazanan, ince oktavların ÜSTÜNE iki kaba oktav
eklemek (~98 m, ~245 m):

| ölçüt | önce | sonra |
|---|---|---|
| 1 m penceresinde salınım | 0,29 m | 0,28 m |
| 128 m penceresinde | 1,17 m (düz) | 1,24 m (hâlâ artıyor) |
| bant genişliği | 2,5 – 9,5 m | 0 – 11,8 m |
| bant ortalaması | 5,9 m | 6,6 m |

Bant artık burunlarda kapanıp koylarda açılıyor; "her diş aynı" görüntüsünü kıran bu.


## Köpük şeritleri eşit aralıklı

**İlk şüpheli — dalga alanı — ölçümle elendi.** Yüzeyin özilintisi: ölü dalga 3 dalgada,
rüzgâr denizi 2 dalgada adım kaybediyor (gerçek denizde 2–4). Tepe uzunluğu dalga boyunun
**3,1 katı**, gerçek ölü dalgada 3–8. Alan fazla düzenli değil.

**Gerçek sebep: kabarcık alanının ızgarası, GERİLDİĞİ için görünüyor.** Çağıran taraf
kabarcıkları kıvrım yönü boyunca uzatmak için bir ekseni 0,35 ile eziyor. Ezilen bir Worley
ızgarası sıralarını hizalar; sakin havada kıvrım yönü rüzgâr eksenine düştüğü için, rüzgâr
bir dünya ekseni boyunca estiğinde sıralar da onunla hizalanıyor.

**Ölçüm — geriliş ekseni boyunca özilinti tepesi:**

| kıvrım açısı | mevcut | +alan büküm | +büküm, ince oktav döndürülmüş |
|---|---|---|---|
| 0° | **0,346** | 0,124 | 0,145 |
| 23° | 0,168 | 0,183 | 0,176 |
| 45° | 0,208 | 0,157 | 0,150 |
| 67° | 0,178 | 0,223 | 0,194 |
| 90° | 0,282 | 0,189 | 0,141 |
| **en kötü** | **0,346** | 0,223 | **0,194** |

0,3 üstü görünür tekrar. Kazanan üçüncü sütun: koordinat aramadan önce büküldü (büküm de
hücresel, yeni bir gürültü türü girmedi) ve ince oktav kendi ızgara yönünü aldı.


## "Köpükler dalgaların köpüğüyse, dalgalar düzenli demektir"

**Kullanıcı haklıydı ve ben yanlış yeri ölçmüştüm.** Spektrumun ZAMAN özilintisini
ölçmüştüm (ölü dalga 3 dalgada adım kaybediyor) ve "alan düzenli değil" demiştim. Oysa
soru uzaydaydı.

Dalga alanı GPU'dan geri okundu ve tepeleri KESEN yönde 1200 metrelik bir çizgi boyunca
özilinti ölçüldü:

| U10 | yön | korelasyon boyu | yerel tepe |
|---|---|---|---|
| 0,5 | +X (tepeleri kesen) | 31,5 m | 0,154 @ 177 m |
| 8 | +X | 14,0 m | **0,489 @ 190 m** |
| 8 | +Z (tepe boyunca) | 32,5 m | 0,077 @ 81 m |

**190 m, tier 1'in parça boyu (191 m).** Dalgalar gerçekten 191 metrede bir tekrar
ediyordu; köpük onu işaretliyordu. Köpük maskesi suçlu değildi — o ayrı ve daha küçük bir
kusurdu (`SeaFoamBubbles`, ayrı kayıt).

**Ders:** bir dokunun tekrar edip etmediği zaman ekseninde değil, **uzayda ve gerçek alan
üzerinde** ölçülür. Spektrum dar değildi; döşeme tekrar ediyordu.


## "Dalgalar niye sadece kıyıda?" — deniz suçsuz, rüzgâr yok

Denize verilen rüzgâr **deniz seviyesinde her zaman 0,62 m/s**. Zincir:

    windAtBase = 0.20                      hava sürücüsünün taban şiddeti
    ShapeSeverity(s) = s^4                 0.20^4 = 0.0016
    speed = lerp(calmSpeed, stormSpeed, .) = lerp(0.6, 14, 0.0016) = 0.62 m/s

O hızda rüzgâr denizi Hs 0,19 m. Görünen her şey sabit ölü dalgadan geliyordu: Hs 0,74 m,
dalga boyu 156 m, eğimi %0,5 — uzaktan camdan farksız. Kırılabildiği tek yer kıyı, çünkü
kırılma sığ su ister. Belirti buydu.

`s^4` eğrisi **dağ için** konmuş ve gerekçesi geçerli: doğrusal eşlemede oyun sürekli
8 m/s'de kalıyor, 1 mm'lik yağmur damlası yataydan 25° geliyordu. Ama aynı eğri, tabanı
0,6 m/s olan bir denizi imkânsız kılıyor.

**Düzeltme tabanda:** `calmSpeed` 0,6 → 3,0 m/s. Bofor 2, hafif esinti — açık bir kıyının
olağan hâli. Cam gibi deniz (Bofor 0) bir olaydır, varsayılan değil.

| şiddet | eski hız | yeni hız | eski Hs | yeni Hs |
|---|---|---|---|---|
| 0,20 (taban) | 0,62 m/s | 3,02 m/s | 0,74 m | **1,17 m** |
| 0,57 | 2,02 m/s | 4,16 m/s | — | 1,42 m |
| 1,00 | 14 m/s | 14 m/s | — | 3,66 m |

Fırtına ucu değişmedi; yağmur eğimini bozan 8,5 m/s hâlâ çok uzakta.

**Yan etki ölçüldü ve kapatıldı.** `Strength = sustained / stormSpeed` tabanı da rüzgâr
sayıyordu: taban yükselince açık bir sırt **ölü sakinlikte 0,311** okuyordu — kar
sürüklemeyi başlatan 0,22 eşiğinin üstünde. Dağ, rüzgârsız bir günde kar savuracaktı.

`Strength` artık tabanın ÜSTÜNDEKİ rüzgârı ölçüyor:

    Strength = (sustained - calmSpeed * maruziyet) / (stormSpeed - calmSpeed)

Hafif esinti kar sürüklemez, sisi kapatmaz, duyulmaz — o sistemlerin sorduğu "olağanın
ötesinde ne kadar rüzgâr var". Fırtına ucundaki sayılar aynen geri geldi (korunaklı 0,350,
açık 1,000); ölü sakinlikte her maruziyette sıfır.


## Gece hep aynı parlaklıkta — ay doğuyor, batıyor, hiçbir şey değişmiyor

**İlk şüpheli yanlıştı:** "gökyüzü zaten gece boyu aynı parlaklıkta". Değilmiş. Ölçüldü:
ışık seviyesi gece boyunca 0,0058 (gece yarısı, ay tepede) ile 0,0014 (18:30, ay henüz
doğmamış) arasında geziyor — **3,5 kademelik** gerçek bir değişim, üstelik doğru yönde.

**Gerçek sebep:** `LookController`'daki `Mathf.Clamp(..., 0f, exposureCap)`. Ham hedef gece
yarısı 2,60 EV, 03:00'te 2,78, 05:00'te 3,16, 05:30'da 3,30 — **hepsi 2,5'in üstünde**, yani
her gece saati tam olarak 2,50'ye kırpılıyordu. Veri oradaydı, kırpma yiyordu.

**Ayırt eden ölçüm:** günün 18 saatinde ham hedef ile kırpılmış hedefi yan yana yazdırmak.
Kırpılmamış sütun geziniyor, kırpılmış sütun on bir satır boyunca `2,50 2,50 2,50` diye
iniyor. Tek bakışta ayrılıyor.

Bu, alt sınırda bir kez daha yaşanmış bir hata: `lightLevel` 0,02'ye kırpılırken
alacakaranlık tek seviyeye düzleşmişti. Aynı kırpma **aralığın öbür ucunda** duruyordu ve o
uca kimse bakmamıştı. Düzeltme: sert kırpma yerine `tanh` doyumu — tavanın altında birebir
aynı, üstünde eğrilip tavanı hiç geçmiyor, sıralama hiç kaybolmuyor.


## Gökyüzü probu gün doğumunda ve batımında çöküyor — KAPANDI

**Belirti:** altın saatte sahnenin ortam ışığı çöküyor; pozlama bir anda tavana yapışıyor.

**İlk şüpheli yanlıştı:** ölçüm artefaktı. Edit modda `SkyAmbientBaker`'da `[ExecuteAlways]`
yok, `LateUpdate` hiç dönmüyor, probe donuyor — ilk süpürmede zenit gün boyu 0,2129'da
çakılıydı ve **araç yalan söylüyordu**. Kareler arasında dönen bir süpürme (her saatte 12
kare, zorlanmış repaint + `DynamicGI.UpdateEnvironment`) kurulunca sayılar canlandı.

**Gerçek sebep — ve ölçüm:** delik gerçek. Saatler ters sırada tekrar tarandı, aynı sayılar
5 haneye kadar geldi:

| güneş yüksekliği | zenit |
|---|---|
| +0,0923 | 0,0306 |
| +0,0462 | 0,0045 |
| +0,0231 | 0,000228 |
| +0,0116 | 0,0000101 |
| 0,0000 | **0,000000** |
| −0,0462 | **0,000000** |
| −0,0693 | 0,000161 |

Ufkun iki yakasında simetrik yükseklikte **163 kat** fark var ve arada tam sıfır bir bant.
Gerçek alacakaranlık ufka göre neredeyse simetriktir. Sebep paketin kendi notunda yazılı:
sky-view LUT **tek ışıktan** pişiyor, ay gökyüzünü aydınlatmıyor. Güneş ufka inince gündüz
dalı ölüyor, gece dalı henüz doğmuyor, ikisi ortada buluşamıyor.

`SurfaceLightLevel`'in aynı anda sıfırlanması **doğru**: ufka paralel ışın düz zemine sıfır
ışınım düşürür. Yanlış olan, gökyüzünün de sıfır olması.

**PLAY'DE DOĞRULANDI, ama tam sıfır değil.** Edit modda (probe skybox malzemesinden
pişirilerek) zenit tam `0,000000` okunuyordu. Play paketin `AmbientProbePass`'ini kullanıyor
ve orada 18:00'de `gök = 0,0002`. Delik gerçek, çöküşün büyüklüğü ise şu: **17:30'da gök
terimi 0,25, 18:00'de 0,0002 — 1250 kat.** Aynı anda ekranda turuncu bir gün batımı çizili.
Yani probe, ÇİZİLEN gökyüzünü okumuyor.

Bu, `SkyAmbientBaker`'ın kendi yorumunda tarif ettiği belirtinin ta kendisi: "18:36'da çizilen
gökyüzü kırmızıydı, probe 0,00000, sahne kapkaranlıktı." O yorum sorunu paketin analitik
`RenderSky` yoluna bağlayıp "analitik yol kapatıldı" diyor. **Kapanmamış.** Sahnede
`skyAmbientMode = Dynamic` olduğu için paketin `AmbientProbePass`'i her kare kuyruğa giriyor
ve render `LateUpdate`'ten sonra koştuğu için `SkyAmbientBaker`'ın pişirdiğini **eziyor**.
Düzeltme yazılmış ama üstüne yazılıyor.

**Yan etki — alt sınır devreye giriyor.** Işık seviyesi 0,0002, `LightLevelFloor` 0,0005.
Yani gün batımında pozlama "günün en karanlık anı"nı görüyor: 10,97 kademe, açılma **3,33 EV**
— gecenin en karanlık saatinden (2,95) daha fazla. Sıralama ters.

**GERÇEK SEBEP: atmosferik sönüm üç kez uygulanıyordu.**

`TimeOfDay` yönlü ışığa kendi atmosfer modelini uyguluyor — `BeamTransmittance` bir kez renge,
bir kez şiddete (`LowSunFade` de öyle; kodun kendi yorumu "iki kez, bilerek" diyor). Bu SAHNE
için doğru: batı güneşinde bir yamacın ALDIĞI ışık gerçekten kızarıp söner.

Ama paket gökyüzü LUT'unu `mainLight.color × mainLight.intensity` ile aydınlatıyor
(`PhysicallyBasedSkyURP.cs`, `celestialBodyData.color`) — yani **sahne için sönümlenmiş
ışıkla** — ve LUT kendi atmosferini zaten hesaplıyor. Sönüm üç kez.

**Ayırt eden ölçüm** — ışığın şiddeti ile zenit birebir aynı anda ölüyor:

| güneş yüksekliği | sönüm (beam×fade) | ışık.intensity | zenit |
|---|---|---|---|
| +0,115 | 0,619 | 1,876 | 0,0361 |
| +0,058 | 0,239 | 0,725 | 0,0101 |
| +0,023 | 0,008 | 0,024 | 0,000227 |
| 0,000 | **0,000** | **0,000** | 0,0000295 |

**Düzeltme:** LUT atmosfer üstü güneşi alıyor, sahne ışığı sönümlü kalıyor. Paketde
`PhysicallyBasedSkyURP.SkySunRadiance` kancası; `TimeOfDay` oraya `sunColor × sunIntensity ×
SunBlend × π` yazıyor. Güneş **diski** hâlâ sahne ışığından geliyor — diske bakarken atmosferi
gerçekten arasından görüyorsun, batan güneş kırmızı kalmalı.

**Sonuç, ölçüldü:**

| saat | h | önce | sonra |
|---|---|---|---|
| 12:00 | +0,883 | 0,0972 | 0,1319 |
| 17:30 | +0,115 | 0,0361 | 0,0726 |
| 17:45 | +0,058 | 0,0101 | 0,0521 |
| **18:00** | 0,000 | **0,0000295** | **0,0205** |
| 18:30 | −0,115 | 0,000205 | 0,0398 |
| 19:00 | −0,229 | 0,000274 | 0,0119 |
| 20:00 | −0,442 | 0,000332 | **0,000332** |
| 00:00 | −0,883 | 0,000394 | **0,000394** |

Gün batımı 690 kat düzeldi, gece **birebir korundu**. Pozlama 18:00'de 3,33 EV'den
**0,99 EV**'ye indi — 17:30'un 0,70'iyle aynı mertebe, sıralama düzeldi.

**Yan etki, bilinçli:** öğlen gökyüzü %36 parlak (0,0972 → 0,1319). Kaybolan şey çift sönümdü.
Doğrulayıcı işaret: `ReferenceSkyLuminance = 0,148` ölçümle konmuş "öğlen gökyüzü" sabiti;
öğlen değeri hatayla 0,097 (%34 altında), düzeltmeden sonra 0,132 (%11 altında). Sabit
kalibre edildiğinde gökyüzü daha doğruymuş — çift sönüm sonradan sızmış.

**Bir ara düzeltme geceyi öldürdü, ölçümle yakalandı.** Kancaya yalnız güneş yazılınca
−18°'den sonra kapı kapanıyor ve override hâlâ yürürlükte olduğu için paketin LUT'u
aydınlattığı **ay ışığını eziyordu**: gece zeniti 0,00039'dan TAM SIFIR'a düştü. İkisinin
büyüğü alınarak devir teslim dikişsiz yapıldı.

**Kalan, kapatılmadı:** LUT **yönünü** hâlâ URP'nin ana ışığından alıyor, ufkun altında o ay
oluyor. Yani alacakaranlık doğru enerjiyi yanlış yönden taşıyor — 18:00'de zenit 0,0205,
18:30'da 0,0398, arada iki katlık bir çukur. 690 katın yanında görünmez ama duruyor.
`DECISIONS.md` → "Gökyüzü LUT'u yönü ana ışıktan alıyor".


## Ufuktaki uzak bulutların kenarında parlak kontur (yakın bulutlarda yok)

**İlk şüpheli yanlış olurdu:** "yükseltme (upscale) kenarı taşırıyor". Taşırıyor, ama tek
başına kontur üretmiyor — kanıtı, F1'den bulut sisi kapatılınca taşma devam ediyor, kontur
gidiyor.

**Gerçek sebep:** sönümleme ile dolgu **aynı ağırlığı** paylaşıyordu.

```hlsl
half edgeFog = lerp(1.0, fogTransmittance, cloudCover);
cloudsColor.xyz = cloudsColor.xyz * edgeFog + fogScattering * cloudCover;
```

İnce kenarda `cloudCover ≈ 0` olduğu için `edgeFog ≈ 1` çıkıyor: bulutun kendi rengi **hiç
sönmüyor**. Hemen yanındaki gövde pikselinde `cloudCover ≈ 1`, ve 37 km'de geçirgenlik
neredeyse sıfır olduğu için gövde kendi rengini tamamen kaybedip hava ışığına dönüyor.
Aradaki fark, her uzak bulutun çevresine parlak bir çizgi olarak oturuyor.

**Neden yalnız uzakta:** yakında `fogTransmittance ≈ 1`, `lerp(1, ~1, cover)` de ≈ 1. İki
biçim aynı sonucu veriyor, fark görünmüyor.

**Yükseltme işi ağırlaştırıyor ama sebebi değil:** renk çift doğrusal yükseltiliyor, derinlik
nokta örnekleniyor. Silüetin dışına taşan parlak renk tam da sönümlenmeyen piksellere düşüyor.

**Ayırt eden ölçüm:** F1 → "Bulut sisi" aç/kapa, aynı saatte iki kare. Kapalıyken kontur yok.

**Düzeltme:** ikisi ayrı soru, ayrı ağırlık.

```hlsl
cloudsColor.xyz = cloudsColor.xyz * fogTransmittance + fogScattering * cloudCover;
```

Sönümleme **tam** — `cloudsColor.xyz` yalnız bulutun kendi ışığını taşıyor ve o ışık, bulut ne
kadar ince olursa olsun kameraya kadar bütün mesafeyi kat ediyor. Arkadaki yüzey ayrı geçişte
birleştiriliyor (`Blend One SrcAlpha`) ve sisini kendi yolunda alıyor.

Dolgu `cloudCover`'da **kalıyor**, ters sebeple: hava ışığı yalnız bulutun gerçekten kapattığı
pay kadar eklenmeli, yoksa pikselin geri kalanı sisi iki kez yer.

Bu, aynı satırdaki İKİNCİ kontur hatası. Birincisi siyahtı ve sebebi uydurma mesafeydi
(kenara uzak düzleme yakın bir derinlik yazılıyordu, sis doyuyordu); kaydı shader'ın kendi
yorumunda duruyor.


## Uzak bulutlar sise gömülüyor — ve bunu bir ikinci hata gizliyormuş

Konturu düzeltince (yukarıdaki kayıt) uzak bulutların görünürlüğü düştü. Belirti yeni değil;
**yeni olan, görünür olması.**

**İlk şüpheli yanlış olurdu:** "kontur düzeltmesi fazla sönümlüyor". Sönümleme doğru —
shader'ın kendi belgesi (`HeightFog.hlsl`, `FogPath`) tam da onu tarif ediyor: *"the scattering
share has to be scaled by how much the cloud covers"*, yani **dolgu** kapsamla ölçeklenir,
**sönümleme** tam uygulanır. `edgeFog` bu sözleşmeden sapmaydı ve ince bulutu hiç söndürmediği
için asıl sorunu örtüyordu.

**Gerçek sebep:** `clearVisibility = 25000`. Ayarın **kendi tooltip'i** belirtiyi önceden
yazmış:

> *"At two thousand metres on the mountain the real clear-air visibility is 100-200 km...
> Keeping it low does not only wash out the distance: it also inflates the optical depth of
> the ray climbing 2.6 km from the ground to the cloud and **erases the sea of cloud**."*

Uyarı yazılmış, değer düzeltilmemiş. Dört belgede de 25 km için yazılmış bir gerekçe yok.

**Ölçüm.** Bulut irtifasında hava zaten temiz (3000 m'de eşdeğer görüş 423 km); optik
derinliği şişiren şey, ışının ilk ~19 km'sini sınır tabakasının içinde geçirmesi.

| yatay | irtifa | 25 km ayarı | 60 km ayarı |
|---|---|---|---|
| 10 km | 2500 m | 0,716 | **0,813** |
| 20 km | 2500 m | 0,519 | **0,666** |
| 30 km | 2500 m | 0,376 | **0,545** |
| 50 km | 2500 m | 0,196 | **0,364** |
| 80 km | 2500 m | 0,074 | **0,199** |

Yer seviyesi görüşü 24,3 km → 52,3 km. WMO'nun "istisnai berraklık" sınıfı 50 km'nin üstü;
temiz kıyı/dağ havası açık günde 50-80 km. Tooltip'in 100-200 km'si **irtifa** için ve o zaten
serbest katmandan geliyor — 25 km olan, yer seviyesi değeriydi.

**Neden 100 değil de 60:** tooltip'in verdiği aralık dağ tepesi için; kamera 283 m'de, kıyıda.
Yer seviyesinde 100 km istisnai bir gün demek, varsayılan hava değil.


---

## Kar izi 1 cm ve 5 cm'de hiç görünmüyor

**Kullanıcının ağzından:** *"kar adım izi 1cm ve 5cm'de gözükmüyor. kar izi 1cm ve 5cm'de
20cm ve 50cm'deki gibi birebir aynı gözükmeli."*

**İlk şüpheli (yanlış):** oyma derinliği kar kalınlığıyla sınırlı, o yüzden iz sığ kalıyor.
Doğru ama **yetersiz** — oyma zaten oradaydı ve tek başına belirtiyi açıklamıyordu.

**Ölçüm.** Dört kar derinliğinde aynı dört metre yürünüp iz üstünden 2 cm ızgarayla örneklendi:

| kar | oyma maks | rim maks | ekranda |
|---|---|---|---|
| 1 cm | 5,1 mm | 0,11 mm | **hiç yok** |
| 5 cm | 25,6 mm | 2,8 mm | yok denecek kadar az |
| 20 cm | 108 mm | 40 mm (tavanda) | net |

Oyma 20 cm'den 1 cm'e **19 kat** düşüyor — bu doğru, bot bastığı kardan derine inemez. Ama
rim **360 kat** düşüyordu.

**Gerçek sebep — üst üste binmiş üç telafi terimi.** Üçü de aynı şeyi yapıyordu: zaten kar
kalınlığıyla ölçeklenmiş bir büyüklüğü bir kez daha mutlak kalınlıkla çarpmak.

1. `SnowSim.compute` `KRim`: `rimTarget = min(raised, blurCarve) * SNOW_RIM_STRENGTH *
   saturate(baseH / 0.25)`. `raised` zaten oymayla orantılı; ikinci çarpan 1 cm'de 0,040.
2. `SnowSim.compute` `KDeform`: `maxCarve = baseH * (1 - kalanPay)`. Paketleme sınırı derin
   kar için doğru; ince tabakada kar **yandan kaçar**, sıkışmaz. Ayrıca `baseH` yerel
   yoğunlukla ölçülüyordu — iz sıkıştıkça sütun kısalıyor, oyma kısalmıyor, tavan kendi
   kendine düşüyordu.
3. `MountainSurface.hlsl`: çukur eğimi normale `saturate(trailDepth * 20.0)` ağırlığıyla
   giriyordu. Eğim zaten derinliğin türevi; bu ağırlık 1 cm'de 0,14. Kapı **varlık** kapısı
   olmalı, büyüklük kapısı değil.

**Dördüncü ve asıl eksik: kar maskesinde iz terimi yoktu.** `SnowCoverMaskWithNoise` dört soru
soruyor — eğim, gök, çukur, gürültü — hiçbiri iz değil. Bot karı sıyırıp bitirse bile maske tam
kalıyordu, yani ince karda iz ancak beyaz-üstüne-beyaz olabiliyordu. Oysa 1 cm karda iz tam da
**zemini açtığı için** okunur; karşıtlık malzeme değişimidir, derinlik ipucu değil.

**Ayırt eden ölçüm — aracın kendisi iki kez yalan söyledi:**

- Yüzey shader'ına konan kırmızı prob hiç görünmedi. Sebep: kar pikselinde `surface.albedo`
  kullanılmıyor, `MountainSurface.shader` `SnowBuildSurfaceFrom` ile ayrı bir kar yüzeyi
  kuruyor. Prob ölü bir yola yazıyordu. Dosyanın canlı olduğu, içine kasıtlı sözdizimi hatası
  konup `ShaderUtil.ShaderHasError` **True** dönmesiyle kanıtlandı.
- İlk turlar saat **12:00**'de ölçüldü. Güneş tepedeyken çukur duvarı gölge vermez; iz en az
  okunduğu anda ölçülüyordu. 09:36'ya alınınca aynı iz görünür oldu.

**Sonuç.** 1 cm çözüldü: iz zemini açıyor, ekranda net. 5 cm iyileşti (oyma 25,6 → 31,8 mm,
rim 2,8 → 14,4 mm) ama hâlâ zayıf; orada zemin açılmıyor (1,7 cm kar kalıyor, doğru) ve tek
ipucu kabartma. 20 ve 50 cm hiç değişmedi — `SNOW_MAX_SINK` orada zaten bağlayıcı.
Kalan zayıflığın sebebi ve tetikleyicisi `DECISIONS.md`'de.

---

## Deniz dibinde kar birikiyor

**Kullanıcının ağzından:** *"denizin içine kar yağıyor. kıyıda sığ suda zemine baktığımda
suyun içinde yerde kar görünüyor?"*

**İlk şüpheli (yanlış çıkmadı, ama eksikti):** kar biriktirme kernel'i suyu bilmiyor.
Doğru — ama asıl mesele biriktirme değil, **çizim**: arazi kar maskesi dünya konumunu
okuyor ve deniz kotunu hiç sormuyordu.

**Ölçüm.** `Assets/Snow/` ağacının tamamında deniz seviyesine dair **tek satır yok**:

```
grep -rn "SeaLevel\|_SeaLevelY\|waterLevel" Assets/Snow/   →  sıfır eşleşme
```

`SnowCoverMaskWithNoise` dört soru soruyor — eğim, gök görünürlüğü, çukur (AO), gürültü.
Hiçbiri su değil. Kar çizgisi sahilden aşağı devam edip su altında sürüyordu.

**Ayırt eden ölçüm.** Aynı sahilde (`-11542, 30.1, -1370`, 40 m'de 2.4 m yükselen gerçek bir
kıyı), kar kaplaması 1'e zorlanıp aynı kadraj iki kez çekildi:

| kapı | deniz dibi | kum bandı |
|---|---|---|
| kapalı | **bembeyaz** | yok |
| açık | koyu | var |

**Gerçek sebep ve düzeltme.** Deniz suyu kara izin vermez: tuz donma noktasını −1,9 °C'ye
indirir ve denizin ısı sığası yüzey katmanını orada tutar; suya ulaşan tane erir, dip
biriktirmez. Aynısı her tırmanışta o suyla ıslanan swash bölgesi için de geçerli.

Maske artık `_SeaWetLevelY` — denizin zaten yayınladığı tırmanma kotu, ıslak kum bandının da
astığı çizgi — üstünde `_SeaWetFadeM` ile açılıyor. **İkinci bir sınır uydurulmadı**: kar tam
olarak kumun ıslak olmayı bıraktığı yerde başlıyor.

**Yağan tanecikler değiştirilmedi.** Denize kar yağar, sadece birikmez.

---

## Denizde kayan kahverengi lekeler

**Kullanıcının ağzından:** *"şu anda plastik bir görüntü var ve dalgalar çok yapay ve tuhaf."*

**İlk şüpheli (yanlış):** kırılma (refraction) su üstü piksellerini çekiyor. Kapatıldı —
lekeler durdu.

**İkinci şüpheli (yanlış):** deniz dibindeki kumun sığ sudan geçmesi. Renk zinciri
değiştirildi, lekeler yine durdu.

**Ayırt eden ölçüm.** `skyRefl` sabit magenta yapıldı. Magenta **tam lekelerin yerinde**,
aynı şekillerde çıktı. Kaynak yansımaymış.

**Gerçek sebep.** `GlossyEnvironmentReflection` her yön için cevap verir — ufkun altındakiler
dâhil. Viewer'a doğru eğilmiş bir dalga yüzeyinde yansıyan ışın ufkun altına dalıyor ve
ortamın orada ne varsa onu geri getiriyordu: sert kenarlı kahverengi lekeler, suyun üstünde
kayan kir gibi.

Fizik basit: o ışın **suya** çarpar, göğe değil. Dönmesi gereken şey suyun kendi yukarı
saçılımı — yüzeyin altındaki hacim için zaten hesapladığı büyüklük. Bant dar tutuldu
(R.y 0 → 0.06, ~3,5°); gerçek ufuk keskindir, geniş tutulunca bütün deniz düzleşiyor.

**Aynı adımda düzeltilen ikinci sapma — su rengi.** `extinctionRgb` (0,30, 0,08, **0,05**)
neredeyse saf suydu (Jerlov Tip I, tropik berrak); mavi hiç sönmediği için sığ su parlak
turkuaz çıkıyordu. Jerlov kıyı 3C: (0,346, 0,082, **0,154**). Yukarı saçılım rengi de aynı
tablodan, %1,9 geri saçılım payıyla: (0,030, 0,140, 0,111) — yeşil baskın, mavi ikinci.

---

## Uzaktan köpük yapıştırılmış leke gibi duruyor

**Ölçüm.** Köpüğün tek bir kabarcık oktavı vardı, `_SeaFoamTiling = 0.8` → özellikler
**1,25 m**. Yakında doğru; 100 m ve ötesinde iki üç piksel, yani düz bir yıkamaya
ortalanıyor ve beyaz başlık suyun üstüne yapıştırılmış soluk bir leke olarak okunuyor.

**Gerçek köpük iki ölçekte kurulur:** birkaç metrelik kaba **dantel** (kümeler ve
kanallar) ve onun içindeki kabarcıklar. Mesafede hayatta kalan kaba olanıdır ve eksik
olan oydu.

**Düzeltme.** İkinci oktav `foamUV * 0.20` ile eklendi (~6,25 m). Aşındırma bütçesi
**bölündü, eklenmedi**: 0,55 zaten Jacobian eşiğinin çözüldüğü Monahan kaplamasına göre
ayarlıydı, daha fazlası beyaz başlık alanını o yasanın altına iterdi. Yeni dağılım
0,30 ince + 0,25 kaba.

**Maliyet:** bir hücresel arama, yalnız `whitecap > 0` dalında. FPS değişmedi (142).
