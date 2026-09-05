# Blender — çalışma kuralları ve öğrenilenler

Blender'da varlık üretirken uyulacak kurallar. Her madde bir hatadan doğdu; hepsi
ya ölçümle ya kullanıcının yakalamasıyla ortaya çıktı. Yeni bir kural ancak
**gerçekten yapılmış bir hatadan** sonra eklenir.

Model dosyaları `Blender/` altında durur, `Assets/` içinde değil: `.blend` `Assets/`
içindeyken Unity onu doğrudan içe aktarır ve projeyi açan her makinede Blender kurulu
olmasını zorunlu kılar. Unity'ye FBX girer. `.blend` ve `.fbx` LFS'tedir, `*.blend1`
(Blender otomatik yedeği) hiç versiyonlanmaz.

---

## Geometri

### Hiçbir birleşim tam hizada bitmez

İki yüzey aynı düzlemde ve **aynı yöne bakıyorsa** ekran kartı hangisinin önde
olduğunu seçemez: benekli gürültü (z-fighting) çıkar. Sırt sırta duran yüzler
(birinin max'ı öbürünün min'i) sorunsuzdur — biri gizli kalır.

Kural: bir parça başka bir parçada bitiyorsa **3–4 mm içine girer**. Ne tam hizada
durur, ne boşluk bırakır.

Bu kuralın tuzağı: "boşluk kalmasın" diye küpeşteyi direğin **ötesine** uzatmak. Sıranın
ortasında direğin içine gömülür, ama **ucunda** direği aşıp havada kalır. Uç, komşunun
dış yüzünün 3 mm içinde biter.

### Uzun parçaya küçük pah uygulanmaz

14 × 20 × 40 mm'lik bir demir parçaya 1,6 mm pah verilince kenarlar çöktü ve
**24 sıfır alanlı yüz** bıraktı. Küçük parçada pah 0,8 mm ve tek segment, ya da hiç.

### Eğimli üst yüzde yükseklik X'e aittir, Y'ye değil

Alın panelini kurarken üst halkanın köşe yüksekliklerini yanlış köşelere yazmak üst
yüzü burar. Duvarın köşesi 2,97 m yerine 5,04 m'ye fırladı, çatının 1,84 m üstüne
çıktı. Prizma kurarken alt ve üst halkanın **köşe sırası aynı** olmalı.

### Boolean n-gon bırakır

İki üst üste boolean, alın duvarında 7 n-gon bıraktı, en büyüğü 13 köşeli. Ekranda
kırık, basamaklı kontur olarak görünür. Açıklıkları boolean'la kesmek yerine
**etrafında düz panellerden** kur: her yüz dörtgen kalır.

### Hareketli parça tek mesh ya da bağlı olur

Damperin diski, mili ve kolu ayrı sabit nesnelerdi: disk döndü, kol yerinde kaldı.
Bir parçanın adını taşıyan her şey ya aynı mesh'in içindedir ya ona `parent`'tır.

Döner parçanın **pivotu kendi ekseninde** olur: kapı kanadı menteşe hattında, damper
boru ekseninde. Böylece Unity'de tek bir transform döndürmek yeter, animasyon
dosyası gerekmez.

---

## UV ve doku

### Damar parçanın uzun eksenini izler

Ahşapta damar daima parçanın **uzun ekseni boyunca** akar — tahta ağacın boyunca
kesilir. İnce uzun bir direkte damar enine gidiyorsa, o tahtanın boyundan geniş bir
ağaçtan çıkması gerekirdi.

`uv.cube_project` UV'yi **dünya eksenlerine** göre kurar, parçanın biçimini bilmez.
100 × 100 × 2100 mm'lik veranda direğine damarı enine serdi.

Düzeltme **yüz bazında** olmalı — kutu projeksiyonunda her yüz farklı dünya eksenini
kullanır, nesnenin tamamını çevirmek yetmez. Her yüzde U'nun mu V'nin mi uzun eksen
boyunca ilerlediğine bak, damar enineyse yalnız o yüzü 90° çevir. Ölçüt: en uzun
kenar / ortanca kenar ≥ 2,2. Kutu biçimli parçalarda damar yönü serbesttir.

Taş ve metalde geçerli değil, yalnız yönlü malzemede.

### Doku ölçeği dünyaya kilitlenir

`cube_project(cube_size=N)` ile N metre dünya = 1 doku karesi. Böylece duvarda,
korkulukta ve sobada tahta aynı boyda çıkar; parça küçüldükçe doku büyümez.

### Tekrar iki ayrı sorundur

**Parçalar arası tekrar:** her parça UV'nin aynı köşesinden başlar, 43 çatı sırası
tıpatıp aynı pikseli gösterir. Çözüm: parça başına rastgele UV kayması.

**Parçanın kendi içindeki tekrar:** 1,74 m'lik döşem 2,1 m'lik direkte iki kez döner.
Kaydırma buna dokunmaz. Çözüm: aynı dokuyu ikinci kez, **kaydırılmış** olarak gürültü
maskesi üzerinden karıştırmak.

**İkinci örneği DÖNDÜRME.** Ahşap yönlüdür; döndürülen katman damarı çaprazlar ve
çapraz tarama deseni verir. Çatıda anında görünür.

**Ölçeğini de değiştirme.** İlk çözüm ayrımı ölçekten alıyordu (1,31 / 1,19). Yönlü
tahtada ölçek = **tahta genişliği**: gürültü maskesinin bir yanında tahtalar %31 geniş,
öbür yanında dar çıktı; çatı iki farklı çatı gibi okundu. Kullanıcı tam maske sınırını
işaretleyip sordu.

Doğrusu: ikinci örnek **aynı ölçekte**, kayma **yalnız damar boyunca**. Tahta ek yerleri
damara diktir; damar boyunca kaydırmak ek yerlerini yerinde bırakır, yalnız budak ve
leke dağılımını değiştirir. Tekrar kırılır, geometri bozulmaz. Maske geçişi de yumuşak
tutulur (0,14–0,86).

Üstüne `Object Info → Random` ile parça başına ton farkı (0,74–1,28) ve döşemden
büyük ölçekli gürültü: tekrar, kendisinden büyük bir değişimin altında kaybolur.

### PolyHaven malzemeleri normal haritasını bağlamadan gelir

MCP dört malzemeyi de `Normal` girişi **boş** kurdu. Dosyalar iniyor, node'lar
kuruluyor, bağlanmıyor — yüzey dümdüz kalıyor. İndirdikten sonra her malzemede
`Normal` ve `Roughness` bağlantısını **geri okuyarak doğrula**. `nor_gl` varyantını
kullan (`nor_dx`'in yeşil kanalı ters).

### Difüz haritası malzemeyi belirlemez

Döküm soba `rusty_metal_02` ile turuncu paslı bir varile döndü. Döküm demir siyaha
yakın ve mattır. Doğrusu: pas dokusunun yalnız **normal ve pürüz** haritalarını yüzey
detayı için al, rengi malzemenin kendi rengiyle ver.

---

## Denetim

Her düzenlemeden sonra tam denetim koşar — kullanıcının gözüne bırakılmaz. Kontroller:

| kontrol | eşik |
|---|---|
| havada kalan parça | temas ≥ −0,2 mm (4 mm gevşekti, 1 mm'lik kopuk kolu kaçırdı) |
| çatıyı delen | baca muaf |
| aynı yöne bakan çakışık yüzey | ≥ 0,0015 m² |
| n-gon | sıfır |
| sıfır alanlı yüz | sıfır |
| ters normal, non-manifold, bağımsız vertex | sıfır |
| hareketli parçadan kopuk | aynı mesh ya da `parent` |
| açılır kanat boşluğa sığıyor mu | kanat < boşluk |

Denetim "temiz" dediğinde iş bitmez: **yeni eklenen parçaya yakından bakılmadan**
rapor edilmez. Denetim ölçtüğü şeyi yakalar, ölçmediğini değil.

Onarım turunun kendisi de hata üretebilir — çakışmaları kapatan genel itici, iki
denizlik bandını duvardan kopardı. Onarımdan sonra denetim **tekrar** koşar.

---

## Kayıt

Hareketli parçalar **kapalı/başlangıç konumunda** kaydedilir. Dosya varlığın duruş
hâlidir, son ekran görüntüsünün pozu değil.

Işık ve efekt modele gömülmez. Soba her zaman yanmaz: kor ayrı bir nesnedir, yanma
bir **durumdur**, mesh'in özelliği değil. Işık kaynağı oyun tarafının işidir.

---

## Ölçü ve tasarım

### Sayılar bölünerek çözülür, kırpılarak değil

Güverte tahtası sayısını `int()` ile bulmak sağ kenarda **13,7 cm** açıkta bıraktı ve
korkuluk tam o boşluğun üstüne oturdu. Doğrusu genişliği çözmek:
`BW = (toplam − (n−1)·aralık) / n`. Sonuç tam örtmeli, ölçüyle doğrulanmalı.

### Karakter boyu ölçüttür

Ölçüler soyut değil, oyuncuya göre denetlenir. 1,83 m'lik karakterde kapı boşluğu
2,02 m iken baş üstü yalnız 19 cm kalıyordu — 2,10'a çıkarıldı. Kontrol edilecekler:
kapı boşluğu, saçak altı iç yükseklik, kiriş altı geçiş, pencere ortası (göz hizası
≈ boy − 12 cm), korkuluk üstü, basamak rıhtı.

Açılır kanadın boşluğa sığdığı da ölçülür: kanat < boşluk.

### Bir kütle taşıyıcısız durmaz

Basamakları tabla gibi yapmak altını boş bıraktı. Yere oturan her şey yere kadar
dolu olur; havada duran her şeyin taşıyıcısı görünür olur.

### Arazi varlığın parçası değildir

Çatının arkasına dağ yamacı diye taş kütlesi konuldu; altında tepe olmadığı için
havada asılı kaldı ve hata gibi okudu. Gömülme, varlığın Unity'de yamaca
oturtulmasından gelir. Varlık kendi başına bir **yapı** olarak durmalıdır.

### Ek yapı kendi çatısını ister

Verandaya ana çatının dik eğimi öne uzatılınca kenarda yükseklik 1,40 m'ye indi —
girilecek yer değil. İki seçenek vardır: ek yapıya kendi sundurma çatısını vermek,
ya da ana çatıyı uzatıp **duvarı yeterince yükseltmek**. Duvar 0,95 m iken saçak
1,45 m'de kalıyordu ve altına hiçbir şey bağlanamıyordu; 2,40 m'ye çıkınca ana çatı
öne uzatılabildi ve direk hizasında 2,85 m boşluk kaldı.

### Tipoloji araştırılır, uydurulmaz

Klasik olmayan bir yapı istendiğinde tasarım uydurmak yerine **gerçek tipoloji**
araştırıldı: Alp bivakları, `bivacco tipo Apollonio`, uzatılmış Berti modeli
3,50 × 2,30 × 2,60 m, ahşap kaburga üstüne galvaniz sac, yirmi parçaya sökülüyor,
her parça 25 kg. Gerçek ölçüler ve gerçek yapım mantığı, uydurulmuş biçimden her
zaman daha inandırıcıdır.

Ama tipoloji brief'i ezmemeli: fıçı kesitli bivak doğru ve gerçekti, kullanıcı
"sera gibi" dedi. Araştırma seçenek üretir, kararı brief verir.

---

## Görsel doğrulama

Blender viewport'u ölçüm aracıdır ve yanıltır. Bu turda üç kez yanılttı:

- **Solid gölgeleme sahte ışık kullanır.** Malzeme kapkara ve sahnede ışık yokken bile
  model aydınlık görünür. Malzeme yargısı **Rendered**'da verilir.
- **Kadraj sanılan yer değildir.** Yaw 203° arka alına bakıyordu, kapı ön alındaydı;
  "boolean tutmamış" sanıldı. Şüphe varsa nesnenin dünya koordinatı yazdırılır, hangi
  uca bakıldığı sayıyla doğrulanır.
- **`region_3d` değişikliği bazen ekrana yansımaz.** İki ardışık kare tıpatıp aynı
  çıktıysa görünüm değişmemiş demektir; `view_location` geri okunup kontrol edilir.

Material Preview'daki yeşil bulanıklık camda kusur değil, Blender'ın gömülü
`forest.exr` HDRI'sıdır (`use_scene_world = False`).

---

## Donusum sirasi ve tekrar uygulama

### Aynalama ile duzlem donusumu yer degistirmez

Besik catiyi tek egime cevirirken kaplama siralarini once kendi eksenlerinde
aynaladim, sonra yeni duzleme tasidim. Aynalama x'i degistirip z'yi biraktigi icin
tasima yanlis x'ten hesaplandi: sol yari 18,6 cm yerine **40 cm kalinlasti** ve
duzlemin 10,8 cm ustune cikti — catinin ortasinda boydan boya bir basamak.

Dogrusu once duzleme tasimak, sonra aynalamak; ya da aynalanan noktanin **eski
x'indeki** duzlem farkini kullanmak. Iki islem de x'e bagliysa sira onemlidir.

Yakalayan sey goz degil olcum oldu: her siranin ust noktasinin `zs(x)`'e uzakligi
yazdirildi, sol yari `+0.108`, sag yari `0.000` cikti.

### Yatay kaynak varsayan formul iki kez uygulanmaz

Veranda kirisini catiya yatirma formulu `z' = hedef(x) - (z_ust - z)`, kaynagin
**yatay** oldugunu varsayar. Genisletmeden sonra ayni formulu zaten yatirilmis
kirise tekrar uygulayinca `z_ust` yuksek uctan alindi ve kirisin sol ucu guverteye
kadar coktu — on cephede kosegen bir tahta olarak gorundu.

Idempotent olmayan donusum ikinci kez calistirilmaz; parca yeniden kurulur.

### Yarilari oteleyerek genisletme dikis yerinde yirtar

Genisletme, x>0 parcalarini +D, x<0 parcalarini -D oteleyip araya yeni sira eklemekle
yapildi. Kenari **tam x=0'da biten** parcalar (on duvarin orta paneli, arka duvarin
iki yarisi) tek yone gitti ve aralarinda 1,32 m'lik delik kaldi. Isaret testi
`x >= -0.001` sinirdaki parcayi rastgele bir yana atar.

Genisletmeden sonra her yuzey ailesinin x kapsami taranir, bosluk aranir. Yirtilan
panel yamanmaz, tek parca olarak yeniden kurulur.

---

## Cati ve baca

### Kaplamanin kalin ucu asagi bakar

Padavra/tahta kaplamada kalin dip kenar **asagi**, ince uc yukari bakar; ust sira
alttakinin uzerine biner. Mevcut catida iki yarida da terstir — kalin uc yokusa
bakiyordu, su binisin altina girerdi. Olcum: siranin iki x ucunda kalinligi karsilastir
(0,215 / 0,175).

### Baca 3 m kuralina uyar

Baca basligi, yatayda 3 m icindeki her cati noktasinin **0,6 m ustunde** biter.
Tek egimde ust kenar yukseldigi icin bacanin da uzamasi gerekti: 5,40 -> 6,12.

---

## Denetim araci

Denetim `.blend` icinde `hut_audit.py` metin blogu olarak durur ve
`bpy.app.driver_namespace['HUT_AUDIT']` ile calisir. Oturum arasinda kaybolmaz.

**Geometri degisince denetim de degisir.** Besik cati formulu gomulu kalan eski
denetim, tek egime gecince saglam parcalari "catiyi deliyor" diye isaretledi. Yanlis
alarm, gercek hatayi gormeyi engeller.

Cakisik yuzey testinde duzlem ici kutu, **dunya eksenine bagli** bir cerceveden
kurulur. `Vector.orthogonal()` egik duzlemde capraz bir eksen secti ve yan yana dizilen
43 kaplama sirasini "48 m2 cakisiyor" diye raporladi.

---

## Doku dosyalari

### Doku `.blend` icine gomulmez

MCP ile inen PolyHaven dokulari Windows Temp'e iner ve `.blend` icine paketlenir:
dosya **264 MB** oldu. Her surumu LFS'e tam kopya olarak girer.

Dogrusu: kullanilmayan node ve goruntuleri temizle, `unpack_all(WRITE_LOCAL)` ile
`Blender/textures/` altina cikar, dosyalari goruntu adiyla yeniden adlandir. Sonuc:
`.blend` 1,4 MB, dokular yaninda 108 MB ve surumler arasi degismiyor.

Temizlik olcusu **Material Output'tan geriye erisilebilirlik**: baglanmamis node
silinir, sonra hicbir node'un kullanmadigi goruntu silinir. 33 olu node, 164 MB.

### Baglanmamis harita temizlikte ortaya cikar

Bu temizlik, dort malzemeden ikisinin (`planks_brown_10` — 131 parca —
ve `weathered_planks`) normal haritasinin **hic bagli olmadigini** gosterdi; daha once
"normal haritalari bagladim" denmisti ama hepsi degil. `Hut_CastIron` ve `Hut_FluePipe`
ise `nor_dx` kullaniyordu.

Kural: normal denetimi **her malzeme icin tek tek** yazdirilir; "bagladim" yeterli degil.

### UV'yi yeniden projeleme, geri cek

Duvar yukseltildiginde doku dikeyde gerilir. `cube_project` tekrari olcegi duzeltir ama
dikey kaplama icin yapilan 90 derece dondurmeyi ve parca basina rastgele kaymayi siler.

Bunun yerine: her yuzde UV bileseninin dunya eksenine **dogrusal bagimliligi olculur**
(`u = a*z + b`), vertex tasindiktan sonra ayni dogru yeni koordinatta yeniden
degerlendirilir. Olcek de yon de kayma da korunur.

---

## Malzeme atamasi

Kose tahtalari cati ailesinin malzemesindeydi (`dark_planks` + gri cati tonlamasi) ve
duvarin ortasinda **dikey metal serit** gibi okundu. Parcanin ait oldugu aile,
gorunduğu yere gore secilir: kose tahtasi duvar kaplamasinin devamidir.

Yonlu dokuda parcanin uzun ekseni yetmez, **dokunun kendi tahta yonu** de sayilir.
Guverte tahtalari uzunlamasina UV aliyordu ama dokunun kendi ek yerleri tahtaya dik
geldigi icin zemin **fayans gibi** okundu. Ust yuzlerde UV 90 derece cevrildi.

---

## Damar kuralinin iki eksigi

Kural "damar parcanin uzun eksenini izler" idi. Iki yerde yetmedi.

### Dokunun kendi damar ekseni malzemeye gore degisir

UV'nin U ekseni damar demek degil. Her dokunun kendi yonu var; **olculur**:
kucultulmus kopyada satir ve sutun yonundeki toplam parlaklik farki alinir, damar
**degisimin az oldugu** eksen boyunca akar.

| doku | dU | dV | damar |
|---|---|---|---|
| planks_brown_10 | 290 | 695 | U |
| weathered_plank_siding | 95 | 336 | U |
| dark_planks | 148 | 810 | U |
| weathered_planks | 343 | 136 | **V** |

Dorttten biri ters cikti. "U damardir" varsayimiyla yazilan denetim, dogru duran
guverte tahtalarini hatali, hatali duran kenar tahtasini dogru gosterdi. Once dokuyu
olc, sonra parcayi denetle.

### Egik parcanin uzun ekseni dunya ekseni degildir

Kutu projeksiyonu yalniz X/Y/Z verir. Cati kenar tahtasi ve veranda kirisi **egim
yonunde** uzanir (0.93, 0, 0.36); kutu projeksiyonu onlara dunya X'ini verdi, damar
tahtayi 21 derece caprazladi ve yuzeyde balik sirti desen cikti.

Cozum kutu projeksiyonu degil, **eksene hizali duzlem projeksiyonu**: her yuzde
`e1 = uzun_eksen - n(n.uzun_eksen)` normalize edilir, `e2 = n x e1`, UV bu ikiliden
kurulur (damar ekseni e1'e gider). Olcek yine 2 m = 1 doku karesi, dunyaya kilitli
kalir.

Denetim de ayni sekilde yapilir: en buyuk yuzde U'nun dunya yonu en kucuk karelerle
cozulur, parcanin uzun ekseniyle acisi yazdirilir. Goz kararı degil, derece.

### Kaplama paneli bu kuralin disindadir

Yan duvarin pencere ustu paneli 4,9 oraninda yatay, ama uzerindeki kaplama **dikey**.
Duvar panelinde yonu panelin oranini degil, cephe kaplamasinin yonu belirler.

### Kaplayan tahta, kapattigi seyi tam kapatir

Cati kenar tahtasinin alt kenari duzlemin −184 mm'sinde bitiyordu, kaplama siralari
ise −188 mm'ye iniyordu. Aradaki **4 mm** her sira ucunda goruntuye giriyor; 44 kez
tekrarlayinca asagidan bakista tarak deseni cikiyor.

Kullanici bunu "texture bozuk" diye bildirdi — hakliydi, cunku goz **tekrar eden
geometriyi doku hatasi olarak okur**. Doku tarafinda arayan kaybeder; olcum kapatma
derinligini karsilastirir.

Kural: trim/kaplama parcasi, ortuğü parcanin sinirini **her iki ucta da asar**.
Burada alt kenar 30 mm indirildi (−214 mm), ust kenar zaten +24 mm proud'du.

---

## Cati bir yapidir, tek kabuk degil

Kaplama tahtalarini duzleme dizmek cati yapmak degil. Alttan bakildiginda tahtalar
**hicbir seye oturmuyordu**; "bir kutle tasiyicisiz durmaz" kuralini kendi isimde
cignedim. Dogru dizilim asagidan yukari:

**duvar -> mertek -> mertek arasi dolgu -> kaplama**

- Mertek egim boyunca uzanir, uzunlamasina dizilir (burada 80x140 mm, 75 cm arayla).
- Duvar ustu **mertegin altina** gelir, kaplamanin altina degil. Aradaki fark bir
  mertek boyudur; unutulursa duvar mertegin icine girer.
- Mertek arasinda duvar basinda **14 cm'lik acik bant** kalir. Bosluk dolgu takozuyla
  kapatilir; kapatilmazsa disaridan iceri gorunur ve kar savurur.
- On/arka duvar merteklere paralel oldugu icin dogrudan kaplamaya kadar cikar; o hatta
  denk gelen mertek ic tarafa kaydirilir.

### Hicbir eleman trim tahtalarinin disina tasmaz

Mertekler once fasyanin 1 mm otesinde bitiyordu ve **catinin disinda tahta uclari**
goruluyordu. Kural: tasiyici eleman, onu orten trim tahtasinin **ic yuzunde** biter
(3 mm gecmeyle).

Denetime kalici test eklendi: `ENV_X` (sacak fasyasinin dis yuzu) ve `ENV_Y` (rake
kenar tahtasinin dis yuzu) disina tasan her parca raporlanir. Trim ve baca muaf.

### "Catiyi delen" testi cati YUZEYINI olcer

Test once kaplamanin **altini** referans aliyordu; mertek, dolgu, alin duvari gibi
normalde kaplamanin altinda duran her yapi elemani yanlis alarm veriyordu. Yanlis
alarm gercek hatayi gizler. Dogru referans cati **ust yuzeyi**: oradan cikan bir sey
gercekten hatadir.

### Isik ve kor modelde durmaz

Sobanin icinde `Emission Strength 14` ile parlayan kor mesh'i kalmisti; her acidan
beyaz lekeler halinde goruluyordu. Yanma bir **durumdur**; kor da isik da oyun
tarafinin isi. Mesh ve malzeme silindi, `Stove_FuelPoint` bagi kaldi.

---

## Once fiziksel kurali yaz, sonra node kur

Pas isteginde once genel bir efekt kurdum: gurultu maskesi + renk karisimi. Sonuc duz
panelin **ortasinda duran turuncu lekeler** oldu — boya sicramasi gibi. Kullanici
hakli olarak "kotu bir pas" dedi.

Pasin nasil olustugunu biliyordum ama **yazmadan** node kurdum. Yazsaydim maske
kendiliginden cikardi:

- Pas suyun durdugu ve metalin aciga ciktigi yerde baslar: **dip, dikis, kenar, civata**.
- Yer cekimiyle **asagi akar**; iz dikeydir, leke degil.
- Rengi duz degildir; ayni lekede koyu ve acik oksit birlikte bulunur.
- Sobada dipte yogunlasir, yukari dogru kaybolur. Bacada tersi: cati ustunde hava
  aldigi icin yukari dogru artar.

Kurulan maske bunun birebir karsiligi: dusey uzatilmis gurultu (akma izi) x kot
egilimi (Object Z ile MapRange), rengi `arm` dokusunun AO kanaliyla degisken.

Ayni sinif hata daha once iki kez oldu: doku detile'inda **olcek = tahta genisligi**
oldugunu yazmadan olcegi degistirdim; catida **cati bir yapidir** demeden kaplamayi
duzleme dizdim. Uc seferinde de kurali sonradan, kullanici gosterince yazdim.

Kural: gorsel bir davranis kurmadan once **o davranisin fiziksel kaynagini bir cumleyle
yaz**. Cumle yoksa efekt genel cikar, genel efekt sahte gorunur.

### EEVEE'de `Geometry > Pointiness` calismaz

Pasi kenarlara toplamak icin Pointiness kullandim; EEVEE'de sabit deger dondugu icin
kenar terimi olu kaldi ve maske yalniz gurultuye dustu. Cycles'a ozel girdiler
kullanilmadan once render motoru kontrol edilir.

### Yuvarlak parca duz golgelenirse kutu gorunur

14 kenarli baca borusu duz golgelendigi icin dikdortgen bir kutu gibi okunuyordu.
Yuvarlak parcalarda yumusak golgeleme acilir, 40 dereceden keskin kenarlar sharp
isaretlenir. Silindirik her parca (boru, bilezik, baslik, ayak) bu kontrolden gecer.

---

## Parca basina ton, surekli yuzeyde dikis yapar

`Object Info > Random` ile parca basina ton farki, **cok sayida kucuk parcada**
(kaplama sirasi, korkuluk cubugu, guverte tahtasi) dogru calisir. Ama surekli bir
yuzey **az sayida nesneye** bolunmusse ayni teknik panel sinirinda sert bir ton
sicramasi birakir.

Uc kez ayni sebepten patladi:
- cati kenar tahtasi ikiye bolunmustu, ek yerinde iki farkli renk
- kose tahtalari ayri aile olunca duvarin ortasinda serit gibi okundu
- yan duvar dort panele bolunmus; pencerenin solunda **sert dikey ton siniri**

Iki degisiklik birlikte cozer:

1. **Mekansal ton gurultusu dunya konumuna baglanir.** `Texture Coordinate > Object`
   her nesnede sifirdan basladigi icin gurultu de panel sinirinda kesiliyordu;
   `Geometry > Position` ile desen bina boyunca surekli akar.
2. **Parca basina ton kisilir** (±27 -> ±10). Kaybedilen zenginlik, artik surekli olan
   mekansal gurultuden geri alinir (±26).

Olcut: ton degisimi parcadan **buyuk** olmali. Parca boyutunda degisim dikis olarak
okunur; parcadan buyuk degisim yuzeyin kendi karakteri olarak okunur.

---

## Dunyaya kilitli UV cikintiyi gizler

Dunya konumuna kilitli UV, ayni (x,z)'deki iki yuzeye **derinlikten bagimsiz** ayni
dokuyu verir. Kapi kanadinin uzerindeki mentese kolu ve kulp kanatla ayni mesh ve ayni
malzemedeydi: tahta derzi cikintinin ustunden kesintisiz geciyordu, parca kapiya
**boyanmis** gibi okunuyordu.

Iki ayri hata ust uste binmisti:
- Demir aksam (kol, bogaz, kulp) **ahsap malzemedeydi**. Demir demirdir; malzeme
  parcanin ne oldugunu soyler, nerede durdugunu degil.
- Ayni mesh icinde kaldigi icin UV'si de kanadin devamiydi.

Cozum: cikinti yuzleri ayri malzeme yuvasina alinir (burada `Hut_CastIron`), UV'sine
kendi kaymasi verilir. Kural: **bir yuzeyden cikan her parca, kendi malzemesini ve
kendi UV kaymasini tasir.**

Kulp ayrica duz bir plakaydi, priz kapagi gibi okunuyordu. Tutulacak parca **tutulabilir
gorunmeli**: iki topuz uzerine 56 mm oturan dikey cekme kolu.

---

## Yipranma: fiziksel kaynak + dogru renk uzayi

Yipranma da pas gibi rastgele leke degil. Her yuzey icin **once kural yazildi**, maske
ondan turetildi:

| yuzey | fiziksel kaynak | maske |
|---|---|---|
| dikey kaplama | yagmur asagi akar, alt bant nemli kalir | dusey uzatilmis gurultu + z bandi (0,52–1,10 m) |
| guverte, basamak | yurunen yuzey gumusler, derz kir tutar | genis gurultu + alt bant |
| direk, cerceve | dipte sicrama, genelde grileme | dusey iz + z bandi (0,37–0,98 m) |
| cati ortusu | su egimden asagi akar, alcak sacakta bekler | x gradyani (alcak uc koyu, yuksek uc solmus) |
| kupeste ustu | el degen yuzey **cilalanir** | z bandi x yukari bakan normal → puruz 0,30'a **iner** |
| kapi kulpu | el pasi **siler** | konum kutusu → pas x (1 − tutus) |
| cam | yagmur izi ve toz | dusey iz → puruz 0,16'ya kadar |

Asinma her zaman puruz artirmaz: dokunulan yuzeyde **azalir**. Tek yonlu yipranma
sahte gorunur; koyu (nem/kir) ve acik (gunes solmasi) iki katman birlikte kurulur.

### Doku ortalamasindan renk turetirken sRGB/dogrusal tuzagi

Yipranma rengini dokunun ortalamasindan turetmek dogru fikirdi, ama `image.pixels`
8-bit bir JPEG'de **sRGB kodlu** deger dondurur; renk soketi ise **dogrusal** bekler.
"Koyu" diye yazdigim 0,085 degeri, tabanin gercek dogrusal degerinin (0,052)
**ustundeydi**: karartmasi gereken katman aydinlatiyordu.

Belirti: maske dogru olcusune ragmen ekranda hicbir sey degismiyordu. Uc tur bosa
gitti. Once maskeyi Base Color'a baglayip **maskenin kendisi olculdu** (alt 0,46 / ust
0,19 — saglam), sonra yipranma kapatilip **taban albedosu ayni prob biriminde**
olculdu (0,068). O anda renklerin yanlis uzayda oldugu ortaya cikti.

Kural: bir doku ortalamasini shader'a deger olarak yazacaksan **once dogrusala cevir**:
`c/12.92` (c ≤ 0,04045) ya da `((c+0,055)/1,055)^2,4`. Ve etkiyi goz ile degil,
isiksiz albedo render'i ile olc — duvarda alt/ust farki %8,6 cikti, hedeflenen bant.

### Siyah gorunen metal, koyu boya degil yansimadir

Kapi aksami "simsiyah" diye bildirildi. Isiksiz albedo olcumu tersini soyledi: demirin
albedosu ahsabin **%90'i** — karanlik degil. Sebep `Metallic`: metal cevresini yansitir,
yansitacak parlak bir sey yoksa **siyah okur**.

Paslanmis/patinali demirin yuzeyi oksittir, oksit **metalik degildir**. `Metallic`
0,42 → 0,16 yapilinca parca kendi rengiyle okumaya basladi. Once albedoyu olc; sorun
renkte degilse metallige bak.

Ayrica kapi aksami sobanin `Hut_CastIron` malzemesindeydi. Soba dokum demirdir ve
gercekten siyaha yakindir; disarida duran dovme demir degildir — nemle koyu sicak
gri-kahve patina tutar. Ayri malzeme (`Hut_Ironwork`) acildi.

### Ates icindeki metal pas tutmaz

Izgara cubuklari icin "pasli metal" yanlis kaynak. Ates icinde:
- **ust yuz** yakitla suruklenir ve isiyla kararir — siyah oksit kabugu, pas yok
- **alt yuz ve uclar** soguk kalir, **kul** birikir — soluk gri, cok mat

Maske normalin z bileseninden gelir: yukari bakan yuz kabuk, asagi bakan yuz kul.

Soba camindaki (mika) is de rastgele degil: duman **camin soguk ve akisin yavas**
oldugu yerde birikir — kenarda ve ustte koyu, ortada temiz; sicak hava ortayi supurur.
Maske panelin merkezine olan normalize uzaklik + yukari egilim.

---

## Islev denetimi: "duruyor" ile "calisiyor" ayri seyler

Geometri denetimi temizken bile yapı eksik olabilir: o test **parçaların birbirine
göre** durumunu ölçer, **işin görülüp görülmediğini** değil. Genel kontrolde üç
zorunlu parça eksik çıktı, hiçbiri denetime takılmıyordu:

- **Sobanin altinda yanmaz zemin yoktu.** Ayaklar dogrudan ahsap dosemede duruyordu.
  Isin sagı solı degil, yanginin ta kendisi. Sac levha eklendi: yanlarda 0,16 m,
  ates kapisinin onunde 0,50 m tasar (gercek kural).
- **Kapinin ic yuzunde kulp yoktu.** Disaridan acilan, iceriden acilamayan kapi.
- **Esik yoktu.** Kapi boslugunun dibi dogrudan dosemeye aciliyordu; kar ve su iceri
  akar. Esik eklendi, kanadin alti 0,528 -> 0,566'ya alindi (6 mm bosluk).

Bunlari bulmanin yolu geometriyi tekrar taramak degil, **islevi sormak** oldu:
- bu parcanin isi ne? (soba yakar -> altinda ne var?)
- iki yonden de kullanilir mi? (kapi disaridan da iceriden de acilir)
- su/kar nereden girer? (esik, denizlik, sacak)
- karakter bunu yapabilir mi? (bas ustu, korkuluk, rihtlar)

Ayrica: **ic mekana bir kez bile bakilmamisti.** Kapali hacim, disaridan alinan hicbir
kareye girmez. Kacak testi (ic noktadan 360 isin) 0 kacak verdi ama eksik parcalari
gosteren sey render oldu, test degil. Her kapali hacim en az bir kez icerden gezilir.

---

## Unity'ye aktarim: prosedurel gorunumu tasima yontemi

FBX **prosedurel malzeme tasimaz**. Kabinin gorunumunun buyuk kismi node agacindaydi
(dunya konumlu maskeler, pas, is, detile, ton). Iki uc secenek de kotu:

- hepsini Unity shader'inda yeniden kurmak: gurultu uygulamalari farkli oldugu icin
  desen **birebir tutmaz**, ustelik piksel basina maliyet
- her seyi dokuya pisirmek: birebir tutar ama 952 m2 yuzeyi doseme dokusunun
  cozunurlugunde (2048 px/m) pisirmek imkansiz

Kullanilan yol ikisinin arasi ve oyunlarda standart olan:

- **yuksek frekans dosemede kalir** (UV0): ahsap albedo + normal, 2 m'de bir tekrar
- **dusuk frekans atlasa piser** (UV1): `son_renk / doseme_dokusu` orani tek bir
  **tint** haritasina; puruz ve metaliklik ayri bir haritaya

Unity'de: `albedo = doseme x tint x 2`, `puruz/metaliklik = atlas`. Dort doku ornegi,
sade lit aydinlatma. Yakindan keskin, uzaktan Blender ile ayni.

### Pisirmeden once statikleri birlestir

360 nesne = 360 renderer. Malzemeye gore birlestirince **14** kaldi (9 statik +
5 hareketli). Sira onemli: **once birlestir, sonra UV2 ac, sonra pisir** — tersi
yapilirsa paketleme bosa gider.

Ikinci UV `smart_project` ile aciliyor ama varsayilan paketleyici %9 kaplama verdi;
`average_islands_scale` + `pack_islands(shape_method='CONCAVE')` %24,8'e cikardi.

**Texel yogunlugu esit dagitilmaz.** Metal parcalar toplam alanin %2'si; esit
dagitimda 59 texel/m aliyorlardi ve pas izleri bulaniyordu. Paketlemeden once metal
nesnelerin UV'si 3 kat buyutuldu: ahsap 59, metal 178 texel/m — atlasta neredeyse
bedava.

### Import kurallari tum agaca dayatilmaz

Projedeki `ModelImportRules` postprocessor'u `Assets/Models/` altindaki her seye
`materialImportMode = None` ve `importTangents = None` uyguluyordu (bisiklet icin
dogru: prosedurel shader, 3M ucgen, normal haritasi yok). Kabin bunlarin ikisine de
muhtac; belirti "malzemelerim atanmiyor, yuzey dumduz" seklinde geldi ve **importer
ayarlari her seferinde sessizce sifirlandi**.

Cozum dosyanin kendi desenine uydu: alt dizinle kapsam (`Assets/Models/Cabin/`).
Bir postprocessor ayarlari geri aliyorsa Inspector'da ne yaparsan yap tutmaz — once
`OnPreprocessModel` ara.

## Orman karakollari: ortak kit ve tekrar eden hatalar

On loot yapisi (`Blender/outposts/`) tek bir kitten uretiliyor:
`Blender/scripts/outpost_kit.py` geometri yardimcilarini, malzeme zincirini ve
denetimi tasir; her yapinin kendi `build_<ad>.py` betigi vardir. Model yeniden
uretilebilir: betigi calistirmak .blend icerigini bastan kurar.

Uretim sirasinda su hatalar tekrar tekrar cikti; hepsi olcumle yakalandi:

**Cokgen silindir yaricapa ulasmaz.** N kenarli bir silindirin duz yuzu merkeze
`r*cos(pi/N)` kadar yakindir. Yan yana dizilen tomruklarda `r = adim/2` vermek
aralarindan isik sizdirir (olculdu: 10 kenarda 9.5 mm). Yaricap adimin
`cos(pi/N)`'ine bolunerek verilir, ya da parcalar gecmeli birlestirilir.

**Dik catida dusey mesafe yanlis testtir.** "Ayni x'te yuzeyin z'si" olcumu 55
derecelik bir A-frame'de kabugun icindeki butun mertekleri "delen" isaretler.
Denetim, kabuktan DIK isaretli uzaklik alan bir fonksiyon ister.

**Sindiri bindirmesi tek yonludur.** Siralari donusumlu derinlige koymak ust
sirayi alt siranin ALTINA sokar: su iceri girer ve sira cizgileri gorunmez.
Dogrusu lamanin kendi kesiti: dip ucu kalin, tepe ucu ince. Dis yuzey dipte
disariya tastigi icin bir ustteki siranin dibi bunun ince ucunun uzerine biner.

**Sindirinin altina kaplama sart.** Lamalar arasindaki derz, arkasinda bir sey
yoksa saf siyah bosluk gosterir ve cati cizilmis gibi okunur.

**Iki egim tepede bulusmaz.** Her yamacin kenar tahtasi `t = L`'de kesilirse
aralarinda `2*NX*TH` kadar (36 mm) acik kalir. Karsi tarafa tasirilip
kesistirilir.

**Ayni duzleme dusen iki yuz z-fighting yapar.** Sik cikan yerler: kiris ucu ile
kalkan tahtasi, sove ile duvar, bacalik etegi ile cati, kizak ustu ile ilk
kereste kati. Cozum parcayi 5-15 mm iceri almak ya da acikca ust uste bindirmek;
tam teget birakmak degil.

**Zemine oturan tabanlar sayilmaz.** Her parcanin z=0'daki alt yuzu ayni
duzleme duser, hicbiri gorunmez. Denetim bunlari eliyor, yoksa gercek hatalar
gurultuye bogulur.

**Ayni nesne icindeki kopya yuz de aranir.** Birlestirilmis bir parcada ayni
yerde iki kez uretilmis eleman (kule korkulugundaki yinelenen direk) nesne disi
karsilastirmada hic gorunmez; ekranda siyah bir seritti. Ayni nesnede yalniz TAM
KOPYA yuz sayilir: kutu kesisimi bitisik ucgenlerde de olusur ve ucgenlenmis bir
kapakta binlerce yanlis alarm verir.

**Damar ekseni tahmin edilmez, olculur.** `measure_grain` parlaklik gradyanini
U ve V'de karsilastirir; damar hangi eksende uzaniyorsa o eksende gradyan
kucuktur. `wooden_planks` U, `worn_corrugated_iron` V cikti; ikisini de yanlis
vermek direkleri tugla, sac levhalari tahta gibi gosterdi.

**Yipranma rengi notr gri secilirse butun malzemeler ayni tona yakinsar.** Her
katmanin agarma ve koyulasma rengi kendi malzemesinin ailesinden alinir: ahsapta
sicak gri, tasta mavi gri. Terk edilmis evde tomruk, tas ve plaka cati notr
griyle tek blok halinde okunuyordu.

**Doyum ile deger ayri ele alinir.** Koyulastirmak kirmiziligi azaltmaz, sadece
koyu kirmizi yapar. Kabuk dokusu ham haliyle tugla gibi okundugu icin doyum ayri
bir dugumle dusuruldu.

**Kontrast yapinin kendisinden gelmeli.** Avci Siginaginda cati yarma sindiri,
kalkan duvar yatay yuvarlak tomruk: iki ayri geometri ailesi. Ikisi de ayni
dokuyla kaplandiginda yapi tek parca bir yuzey gibi okunuyordu.

**Yapinin ana olcusu ISLEVDEN turer.** Avci Siginaginda kapi acikligi >= 1.75 m
olmali; kalkan ucgeninin o yukseklikteki genisligi kapiyi ve yatak payini
almalidir. Bu zincir tabani 4.60 m'ye, mahyayi 3.80 m'ye cikardi. Once olcu
secip sonra kapiyi sigdirmaya calismak iki tur kaybettirdi.

## Ikinci karakol grubu: dairesel kutle, kaya ve donuk parca dersleri

Bes yeni yapi (konik komurcu kulubesi, maden agzi, telsiz istasyonu, sapel,
su degirmeni) ayni kitle uretildi. Bu turda cikan yeni hatalar:

**`bpy.ops` ile UV izdusumu kirilgan.** `uv.cube_project` edit moduna girmeyi
ve dogru baglami gerektiriyor; taze bir dosyada ve toplu uretimde surekli
`poll()` hatasi verdi. Izdusum uc satirlik bir hesap: yuzun baskin normal
eksenine gore diger iki koordinat UV olur. Operator tamamen kaldirildi.

**UV DUNYA koordinatindan hesaplanmali.** Yerel koordinat kullanmak her parcayi
kendi merkezine gore 0 civarinda birakir; butun parcalar dokunun AYNI bolgesini
ornekler ve on direk ile arka direk birebir ayni gorunur. Dunya kilidi hem
tekrari kirar hem de kalin kiris ile ince citanin ayni tahta genisligini
gostermesini saglar.

**`rotation_euler` quaternion modunda sessizce hicbir sey yapmaz.** `K.log`
nesneyi QUATERNION moduna aliyor; sonradan `rotation_euler` yazilan su carki
yatay duzlemde kaldi. Belirti gorsel degil olcumseldi: carkin X acikligi 3.65 m
cikti, yani yaricap yanlis eksende. Mod acikca geri alinir.

**`matrix_parent_inverse` bayat matristen hesaplanirsa cocuk kayar.** Ebeveynin
`location`'i yazildiktan hemen sonra `matrix_world` okunursa birim matris gelir
ve cocuk parca ebeveynin donusumu kadar oteye duser (olculdu: 1.42 m). Once
`view_layer.update()`.

**Donme yuzeyinde eksen uzerindeki profil parcasi manifold disi birakir.**
r = 0 noktalari her dilimde ayri vertex uretir; kaynatilmazsa tepe acik kalir.
Kaynatildiktan sonra eksen boyunca uzanan profil parcasi tek bir kenara coker,
yuzleri sifir alanli oldugu icin silinir ve kenar bosta kalir. Ikisi de
temizlenir.

**Dairesel kutle KARE izgarayla yapilmaz.** Yukseklik alaninin kosesinde kalan
z = 0 duzlugu zeminle cakisip siyah testere disi birakir. Daire icin donme
kullanilir; kare izgara yalniz gercekten dortgen kutleler icin.

**Zemine tam oturan hicbir yuzey z = 0'da birakilmaz.** Arazi de orada; ikisi
z-fighting yapar. Taban 4-6 cm asagi indirilir.

**Doku adi ne oldugunu soylemez.** `rock_wall_08` "kaya duvar" degil DERZLI
ORGU duvardir; dogal kaya yuzune verildiginde yamaci sato duvarina cevirdi.
Dogal kirikli yuzey icin `rock_04`. Doku secmeden once ne oldugunu bilmek,
adina bakmaktan farkli bir istir.

**Kaya DUZ golgelenir, tomruk PURUZSUZ.** Puruzsuz golgeleme kaya kutlesini
bir tumsege cevirip tas karakterini yok eder; kirikli yuzey ancak duz
golgelemeyle okunur. Ayni parametre silindirik parcada tam tersi calisir.

**Ayni ormanda iki yapiya ayni renk kimligi verilmez.** Telsiz istasyonunun sac
kaplamasi once yesil kuruldu ve gozetleme kulesinin boyasiyla ayni aileye
dustu. Kaplama gri-maviye cekildi: her yapinin rengi tek olmali, yoksa siluet
ayri olsa bile kimlik karisir.

## Denetim: "temiz" ile "kullanilabilir" ayri seyler

`check_outposts.py` kaydedilmis .blend dosyalarini oldugu gibi olcer; hicbir
sey yeniden kurmaz. On yapinin ikinci gecisinde yakaladigi hatalarin hicbiri
topoloji denetiminden gecmiyordu -- hepsi "temiz" raporu almis modellerde
duruyordu.

**Kutu testi birlestirilmis agda ise yaramaz.** Cati tek nesnedir ve kutusu
butun binayi kaplar; kapinin her konumu o kutunun icindedir. Kapi acilma testi
bu yuzden on kapiyi da "acilamaz" gosterdi (iki tur kaybedildi). Dogru arac
BVH ucgen kesisimi.

**Kanat duvar duzlemine GOMULU olamaz.** Telsiz istasyonunun kapisi duvar
kalinliginin ortasindaydi: her iki yone donerken duvari kesiyordu. Modelde
kapi vardi, oyunda acilamiyordu. Kanat duvar kalinliginin disinda durur.

**Ag yalniz bir eksende kaydirilirsa parca gomulur.** Terk edilmis evin kanadi
X'te mentese kenarina tasinmis ama Z'de tasinmamisti; kendi yuksekliginin
yarisi kadar zeminin altinda duruyordu (taban z = -0.98). Dunya kutusunun alt
kotunu olcmek bunu tek satirda yakalar.

**`join` ILK parcanin originini korur.** Su carkinin mili asimetrik oldugu icin
ilk sirada oldugunda kendi orta noktasi origin sayildi ve butun cark 40 cm
kaydi; cark cati sacagini deldi. Birlestirilecek kumede ilk parca SIMETRIK
olmali, ya da birlestirmeden sonra origin duzeltilmeli.

**Gergi teli, oluk ve ray gibi bagli hatlar da denetlenir.** Telsiz direginin
gergi ucgeni once kapinin onunden, sonra catinin icinden gecti. Yapinin
kendisi kusursuzdu; kullanilamaz yapan sey ona bagli ince bir cizgiydi.

**Catiyi delmesi beklenen parca ile hata ayrilir.** Baca catidan gecmek
ZORUNDADIR, kabin duvari catinin oturdugu yerdir; gergi teli ise gecmemelidir.
Denetim beklenenleri adiyla eler, gerisini hata sayar.

**Kare izgarali yassi tumsek disk gibi okunur.** Maden pasa yigini alcak
tutuldugunda yerde duran gri bir daire oldu; yigin kendi suku acisiyla birikir,
yuksek ve dik olmali.

## Iki yapiyi karsilastirirken once CERCEVELEMEYI esitle

Ana siginak, on karakolun yaninda "duz" gorunuyordu. Sebebin dokuda oldugu
dusunuldu; olcum baskaydi.

Karsilastirma sayfasinda her yapi kendi kutusuna sigacak sekilde
cerceveleniyordu: karakollar 9 m'den, ana siginak 20 m'den. Ana siginak 10.2 m
uzunlugunda, karakollar 4-6 m; ayni kadraja sigdirmak onun ayrintisini 2.2 kat
kucultuyordu. AYNI MESAFEDEN bakildiginda kusaklar, tahta derzleri, sove
golgeleri ve doseme dokusu okunuyor.

Bunu once olcmeden malzeme degistirmek, olmayan bir problemi kovalamak olurdu.
Kural: iki varligi karsilastirmadan once kamera mesafesini esitle; "daha kotu
gorunuyor" cogu zaman "daha uzaktan bakiliyor" demektir.

Cerceveleme elendikten sonra kalan GERCEK farklar sunlardi ve duzeltildi:

- **Yipranma gradyani yapinin boyuna gore ayarsizdi.** SUN araligi 2.6 m'de
  doyuma ulasiyordu; 6 m'lik binanin ust yarisinin TAMAMI tek tip agarmis
  cikiyordu. Aralik yapinin gercek yuksekligine yayildi.
- **Ton gurultusu 0.28 olcekteydi** -- 7 m'de tek dalga, yani her tahta ayni
  ton. Karakollarda 1.6-3.4 kullaniliyordu; tahta basina ayri yaslanma buradan
  geliyor.
- **Doyum/deger denetimi yoktu.** Butun malzemeler ham doyumda kalip ayni
  kahve-griye dusuyordu.
- **Agarma rengi notrdu.** Her malzeme ayni gri tona yakinsiyordu; renk
  malzemenin kendi ailesinden alindi.
- **Tek ahsap ailesi.** Cati en soguga, direk ve soveler en sicaga cekilerek
  kutle uc tona ayrildi.

Geometrik rolyef zaten vardi (kusak 20 mm, cati sirasi 31 mm); eksik olan
rolyef degil, tonlama ve olcek uyumuydu.

## Insan olcegi ve FBX guvenligi

Butun karakollar 1,80 m boyunda ve 0,70 m capinda oyuncu kapsulu ile denetlenir.
Kullanilan alt sinirlar: gecit yuksekligi 1,88 m, standart kapi kanadi 0,78 m,
acik platform net yuruyecek alan 0,85 m, merdiven 0,88 m ve sundurma bas
boslugu 2,20 m. Binayi topluca buyutmek yerine kapi, merdiven ve dolasim alani
islevine gore boyutlandirilir; pencere parapeti de bitmis ic dosemeden olculur.

Kapi veya kemer boslugu acan konkav tek bir n-gon FBX'e birakilmaz. Disari
aktarici bu yuzu acikligin uzerinden uzun bir ucgenle bolebilir ve duvar/tas
boslugu delmis gibi gorunur. Cephe iki yan ayak ve ust lento gibi basit, konveks
yuzlere bolunur. `check_outposts.py` topoloji kadar bu insan olcegi kosullarini
da kaydedilmis `.blend` dosyalarinda denetler.

UV atlasinda paketleme araligi ile bake tasma payi birlikte belirlenir. Mevcut
UV1 adalari yaklasik 1,5 piksel aralikli oldugu icin tint ve roughness/metallic
bake payi 1 pikseldir. Birbirinden ilgisiz renk adalarinin mip seviyelerinde
karismamasi icin bu iki dusuk frekansli atlas Unity'de mipmap kullanmaz; taban,
normal ve karolu roughness dokulari mipmap kullanmaya devam eder.
