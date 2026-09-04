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
Kaydırma buna dokunmaz. Çözüm: dokuyu kendisiyle bozmak — aynı görüntüyü **farklı
oranda ölçeklenmiş** (x'te 1,31, y'de 1,19) ikinci bir örnekle gürültü maskesi
üzerinden karıştır. Farklı oran olduğu için iki katman hiçbir noktada hizalanamaz.

**İkinci örneği DÖNDÜRME.** Ahşap yönlüdür; döndürülen katman damarı çaprazlar ve
çapraz tarama deseni verir. Çatıda anında görünür. Dönüş sıfır, ayrım ölçek ve
kaymadan gelir.

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
