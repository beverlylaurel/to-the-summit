# To The Summit — Çalışma Kuralları

Karlı, fırtınalı bir dağ dünyası. Unity 6000.5.6f1, URP, birinci şahıs.

**Oyun dağa tırmanma oyunu DEĞİL.** Ana dağ kalır, asla kaldırılmaz; tırmanışa dayalı iş
yapılmaz. Türün ne olduğu henüz söylenmedi (`DESIGN.md` → askıdaki bölümler).

## Komutlar

Unity açıkken **Unity MCP** kullanılır: konsol okuma, `RunCommand` ile editör kodu
çalıştırma, Play'e girip çıkma. Araç yoksa oturum eskidir, kullanıcıya söylenir.

| iş | nasıl |
|---|---|
| Derleme tetikle | `date > Logs/refresh.trigger` — Unity odaksız derler |
| Sahne | `Assets/Scenes/Game.unity` (F5), test için `TestGround.unity` (F6) |
| Sahneyi yeniden kur | `To The Summit/Scene/Rebuild Test Scene` |
| Arazi yüzey haritaları | `To The Summit/Terrain/Surface Maps` |
| Çökme sonrası başlat | `Unity.exe -projectPath "D:\ME\game	o the summit"` |

**`To The Summit/Terrain/Regenerate Terrain` yükseklik haritasını SIFIRDAN üretir** ve elle
yontulmuş dağı siler. Yüzey haritalarını tazelemek için değil.

## Rol dağılımı

- **Claude yapar.** Kod, dosya, klasör, ayar — hepsi Claude tarafından yazılır.
- **Kullanıcı sadece Unity içinde tıklanması zorunlu olan yerleri yapar** (Play, editör penceresi etkileşimleri).
- Claude bir şeyi otomatikleştirebiliyorsa otomatikleştirir. Kullanıcıya "şu menüye tıkla" demek son çaredir.
- Sahne kurulumu `Assets/Editor/MountainSceneBootstrap.cs` üzerinden koddan yönetilir. Elle sahne düzenleme yok.

## Dil

**Kaynak dosyaların içi tamamen İngilizce.** Tanımlayıcı, yorum, log metni,
menü yolu, sahne nesnesi adı, shader property adı — hepsi. Türkçe karakter
`Assets/` altında hiçbir `.cs`, `.shader`, `.hlsl`, `.compute` dosyasında
geçmez.

Belgeler (`*.md`) ve commit mesajları Türkçe kalır; onlar kullanıcı için.

**Ekranda kullanıcıya görünen metin de Türkçe.** F1 test paneli, tırmanış göstergesi,
performans uyarıları — bölüm başlığı, etiket, buton, onay kutusu, durum satırı. Ayrım
"kaynak mı, arayüz mü" değil: **kim okuyor?** Kod okuyan Claude, panel okuyan kullanıcı.
Tanımlayıcı ve yorum bu satırların içinde de İngilizce kalır; yalnız görünen dize Türkçe.

## Kod mimarisi

React component mantığı geçerlidir: her parça kendi içinde kapalı, dışarıdan gelen parametreyle çalışır, başka parçalara bağımlı değildir.

Unity'deki karşılıkları:

- **Bağımlılık Inspector'dan enjekte edilir.** `[SerializeField]` alanla dışarıdan verilir. `FindObjectOfType`, singleton, `GameObject.Find` kullanılmaz — bunlar gizli bağımlılık yaratır.
- **Ayarlar `ScriptableObject`'e taşınır.** Bir sistemin ayarları koda gömülmez; asset olarak dışarıdan verilir. Aynı sistem farklı ayarla tekrar kullanılabilir olur.
- **Sistemler birbirini doğrudan çağırmaz.** İletişim event/callback ile olur. Tırmanma sistemi kar sistemini bilmez, hava sistemi oyuncuyu bilmez.
- **Bir script tek iş yapar.** İki iş yapıyorsa ikiye bölünür.
- **Public API dar tutulur.** Dışarıdan erişilmesi gerekmeyen her şey `private`.

Hedef: bir sistemi değiştirmek veya silmek diğerlerini bozmaz. "Bunu değiştirmek çok maliyetli, her yeri bozarız" cümlesi kurulacak bir durum oluşmamalıdır. Böyle bir risk doğuyorsa mimari yanlıştır, mimari düzeltilir.

## Atmosfer tutarlılığı

Hava, rüzgâr, bulut, sis, ışık, ses ve renk düzenlemesi **tek bir durumdan** türer ve
birbiriyle çelişemez. Yeni bir özellik eklerken bu zincire nasıl bağlanacağı baştan
belirlenir; bağımsız çalışan ikinci bir kaynak yaratılmaz.

Kaynaklar:
- `WeatherState` — yağış şiddeti, karlılık
- `WindField` — rüzgâr vektörü ve şiddeti (şiddetini `AltitudeWeatherDriver`, arazi
  maruziyetini `TerrainWindShelter` sürer)
- `TimeOfDay` — günün saati, güneş yönü, gündüz katsayısı
- `TemperatureField` — sıcaklık ve hissedilen sıcaklık; donma seviyesi buradan türer

Yeni sistem eklerken sorulacaklar:
- Bu özellik yağış şiddetinden etkilenmeli mi? Rüzgârdan? Günün saatinden?
- Şiddetlendiğinde diğer sistemler de şiddetlenmeli mi, yoksa tersine mi davranmalı?
- Kendi zamanlayıcısını/rastgeleliğini mi kuruyor, yoksa mevcut duruma mı bağlanıyor?

"Rüzgâr uğulduyor ama kar dik iniyor", "fırtına var ama gökyüzü açık", "gece oldu ama
bulutlar hâlâ gündüz rengi" gibi bir çelişki oluşuyorsa özellik yanlış bağlanmıştır.

Mevcut bağların tamamı `SYSTEMS.md`'de: ne neyi okur, ne neyi okumaz, hangi kural
bilinçli. İki sistem arasında yeni bir bağ kurulduğunda, bir bağ koptuğunda veya bilinçli
bir kural eklendiğinde `SYSTEMS.md` **aynı adımda** güncellenir. Sayılar orada tutulmaz;
eşik ve katsayı kodda ve ayar asset'lerinde durur.

## Bulut sistemi

Hacimsel bulutlar `UnityVolumetricCloudsURP` (MIT) üzerine kurulu, yoğunluk/şekil/aydınlatma
zinciri Nubis/HZD makalesine göre düzeltildi. Bağlar `SYSTEMS.md`'de ve değişmeye
devam ediyor — son eklenenler: kapsama → optik kalınlık, sis → bulut birleştirme geçişi.

- **Bağlar ve bilinçli kurallar `SYSTEMS.md` → Bulutlar.** Güncel olan orasıdır.
- **`CLOUDS_REBUILD.md`** teknik kayıt: portun makaleyle sekiz farkı, hangisi nasıl
  kapandı, ölçülmüş sayılar, kurtarılmış gürültü hash'i. Yeni bir sapma yapılacaksa
  önce oraya bakılır — aynı hata iki kez ölçülmesin.

**Repo'nun üstüne kendi terimimiz eklenmez.** Görüntü yanlışsa önce onun parametrelerine
ve ürettiği dokulara bakılır, tek seferde tek sayı değişir. Ekleme ihtiyacı doğuyorsa
önce ilgili makale okunur — eski sistem dört satırlık formülün üstüne on bir terim
biriktirdiği için silindi.

**Telafi terimi geri eklenmez.** Bir düzeltmenin gerekçesi ortadan kalktıysa terim de
gelmez; bu bir kez uygulandı (güneş yaması, `CLOUDS_REBUILD.md` bağ 7).

## Belge otoritesi

`SYSTEMS.md` yön gösterir, otorite değildir. Nereye bakılacağı oradan bulunur, davranış
**koddan doğrulanır**. İkisi çeliştiğinde kod haklıdır ve belge aynı adımda düzeltilir.
"Belgede öyle yazıyor" bir gerekçe değildir; hafızadan konuşmak da değildir.

`SYSTEMS.md` **bağ haritasıdır**: ne neyi okur, ne neyi okumaz, kural ne. Sayı ve gerekçe
tutmaz. Bir kuralın **neden** öyle olduğu — ölçüm, denenip başarısız olan yol, ürettiği
belirti — `RATIONALE.md`'de. Kural değişirse **ikisi birden** değişir; gerekçesini yitiren
kayıt silinir.

## Temizlik

- Çöp kod yok. Kullanılmayan dosya, ölü kod, yoruma alınmış kod, template artığı projede durmaz.
- Bir şey gereksizleşince aynı adımda silinir, sonraya bırakılmaz.
- Gereksiz paket/modül kurulmaz. Sadece o anki adım için gerekli olan kurulur.

## Klasör yapısı

```
Assets/
  Scripts/
    <Sistem>/          her sistem kendi klasöründe
  Editor/              editör araçları, sahne bootstrap
  Settings/            render pipeline, volume profilleri
  Terrain/             üretilen terrain verisi
  Scenes/
```

Yeni sistem = yeni klasör. Dosyalar `Assets/Scripts` kökünde birikmez.

**İstisna — `Assets/Snow/`.** Kar sistemi runtime, shader, editör aracı ve ayarlarıyla
tek ağaçta duruyor (`Runtime/`, `Shaders/`, `Editor/`, `Settings/`). Spec §1.5 böyle
istiyor ve gerekçesi geçerli: kar iki kez silindi, ikisinde de parçaları dört ayrı
klasörden toplamak gerekti. Üçüncüsünde tek klasör silinecek. Gerekçe `DECISIONS.md`.

## Arazi

Dağ **elle yontuldu**, üretilmedi: `Assets/Editor/MountainBuilderWindow.cs`.
`MountainGenerator` ilk formu kurdu, bugünkü şekil ondan gelmiyor.

- Asıl veri `Assets/Terrain/MountainTerrainData.asset` (4097², 30 km kare, tavan 8000 m) —
  **git'te izlenmiyor.** Üzerine yazmadan önce yedek alınır.
- Yükseklik değişirse şunlar bayatlar ve **aynı adımda** yeniden pişirilir
  (`SurfaceMapBaker.Invalidate()` + `Bake()`): `MountainNormals`, `MountainHorizon`,
  `MountainHeight`, `MountainSurfaceMaps`, `MountainWindWeight`.

## Play modu

`enterPlayModeOptions = DisableDomainReload, DisableSceneReload`. Play'de açıklanamayan
davranışta ilk şüpheli budur.

Play modda yapılan sahne düzenlemesi uçar; kalıcı düzeltme edit modda yapılır ve sahne
dosyasından doğrulanır.

## Tasarım otoritesi

Oyunun **ne olduğu** `DESIGN.md`'de: register (absürt/kayıtsızlık, korku değil), yapı
(iniş yok, iki çıkış), anlatı kuralları (hikâye çevrede, sahibi belirsiz), ton yönetimi
(mod değil irtifa) ve üç yasak.

Oynanışa, anlatıya veya tona dokunan bir özellik eklenmeden **önce** oraya bakılır. Tek
soru: *bu hangi kayda hizmet ediyor?* Cevap "hiçbiri" ise özellik yanlış oyuna aittir.

Yeni bir ton/yapı kararı verildiğinde **aynı adımda** oraya yazılır.

## Belirti kaydı

Ölçülerek bulunmuş belirtiler ve gerçek sebepleri `SYMPTOMS.md`'de. Bir belirtiyle
karşılaşıldığında **önce oraya bakılır** — sekiz kaydın altısında ilk şüpheli yanlış
çıktı ve her yanlış şüpheli bir tur yaktı.

Yeni bir belirti ölçümle kapandığında aynı adımda oraya yazılır: kullanıcının ağzından
belirti, yanlış çıkan ilk şüpheli, gerçek sebep, ayırt eden ölçüm. Tahminle çözülen
bir şey yazılmaz — dosyanın değeri her kaydın ölçülmüş olmasından geliyor.

## Ölçmeden düzeltme yok

Belirti kodu okuyarak açıklanamıyorsa tahminle düzeltme yapılmaz. Kod her satırında doğru
görünüp sonuç yanlışsa, yanlış olan **varsayımdır** — ve varsayım ancak ölçülerek bulunur.

**İki turdan fazla "düzelttim, bir bak" denmişse dur.** Üçüncü turda kod değil ölçüm aracı
yazılır: ekrana basılan sayı, tek bakışta ayıran renk probu, sınırın iki yakasındaki değer.
Aracın kendisi önce doğrulanır — ışıktan, tonemap'ten, pozlamadan etkilenen bir teşhis
görünümü yalan söyler ve turları katlar.

Ölçüm sonucu gelmeden bir sonraki düzeltme yazılmaz. Aynı belirtiye üst üste dört farklı
"düzeltme" uygulamak, dördünün de yanlış yeri hedeflemesi demektir.

## Bir değere bağlanmadan önce

Yeni bir görsel/davranış mevcut bir değere (global, ayar, örnek) bağlanacaksa, bağlamadan
**önce** o değerin fiziksel karşılığı tek cümleyle yazılır: radyans mı, ışınım mı, gök mü,
yüzey mi, yön bağımlı mı. "Zaten var, elimin altında" gerekçe değildir.

Sonra uçlar **kâğıtta** hesaplanır — şafak, öğle, gece, kapalı hava, fırtına. Sabit katsayı
koyuluyorsa aralığın iki ucundaki sonuç yazılır.

**Kaynak HDR ve üst sınırsızsa katsayı ayarlanmaz, tavan konur.** Tavanın fiziksel bir
karşılığı olmalı. Renk ile parlaklık ayrılır: ton kaynaktan alınır (parlaklığı 1'e
normalize edilerek), seviye sınırlı ve bilinen bir büyüklükten kurulur.

Atlanırsa belirti hep aynı: bir saatte doğru, başka saatte fosforlu ya da kapkara.

## Ölçek bağımlılıkları

Dağın boyu değiştiğinde nelerin kendiliğinden kaydığı, nelerin elle düzeltilmesi
gerektiği `SCALE.md`'de. Dağ büyütülüp küçültülmeden **önce** baştan sona okunur.

Yeni bir özellik eklerken tek soru sorulur: *bu sayı dağın boyuna bağlı mı?* Evetse
`SCALE.md`'ye **aynı adımda** yazılır — hangi kategoriye girdiğiyle birlikte
(kendiliğinden ölçeklenir / bilerek mutlak / elle bakılacak).

## Ertelenmiş kararlar

Bilinçli olarak ertelenen veya sınırlandırılan her karar `DECISIONS.md`'ye yazılır: karar,
gerekçe, **tetikleyici** (hangi belirti görülünce geri dönülecek), maliyet.

"Sonra bakarız" denen hiçbir şey sadece konuşmada kalmaz. Karar geri alındığında kaydı silinir.

Dosyanın başında durum indeksi var: **bloke eden açık sorular**, **bekleyen kararlar**,
**silinecek geçiciler**. Yeni kayıt bu üçünden birine giriyorsa aynı adımda indekse de
yazılır; iş bitince indeksten silinir. Kapanmış kararlar indekse girmez.

## Co-op borcu

Ağ katmanı gelince yeniden yazılması gerekecek bir şey fark edildiğinde `COOP.md`'ye
**aynı adımda** yazılır: ne yapıyor, ne olması gerekiyor, maliyeti ne. Ödendiğinde satır
silinir.

Kararın kendisi ve "başlamadan önce uyar" kuralı `DECISIONS.md`'de; `COOP.md` yalnızca
envanterdir.

## Varlık üretimi (kredili servisler)

Model, doku ve referans görsel kredili servislerde üretiliyor. **Her üretim kullanıcının
parasını harcıyor** — ilk deneme doğru olmak zorunda. Teknik olarak bozulmayan ama işe
yaramayan çıktı israftır.

İstem yazarken iki kriter **ayrı ayrı** karşılanır, biri diğerine feda edilmez:

- **Teknik güvenlik** — neyin üretimi bozacağı
- **İşin amacı** — çıktıdan ne beklendiği (özgünlük, detay, karakter)

Neyin kesileceği bu ayrımdan çıkar:

- **Kesilir — silueti tehdit eden geometri:** sarkan kayış, ip, karabina, gevşek kordon,
  elde tutulan alet. İnce ve serbest olan her şey lapa olur, rig'de çöker. Ekipman ayrı
  model olarak üretilip kemiğe takılır.
- **Kesilmez — yüzey detayı:** dikiş, panel, cep, fermuar, renk bloklama, aşınma, leke,
  solma, yara izi, bronzluk çizgisi. Dokuda taşınır, silueti bozmaz, karakteri o verir.

İkisini birden kesmek "güvenli ama kişiliksiz" üretir; bu bir kez oldu ve kredi yaktı.

Kimlik tutarlılığı: beğenilen yüz/tasarım sonraki turlarda **görsel referans** olarak
verilir, istem yeniden yazılmaz.

## İş akışı

- **Davranışı değiştiren her şey onaya tabidir.** Kaynak dosya yazma/düzenleme/silme, ayar asset'i değiştirme, geri alınamaz komut — önce ne yapılacağı söylenir, onay alınır, sonra uygulanır. "Küçük değişiklik" istisnası yoktur.
- **Onaya tabi değil:** okuma, arama, ölçüm, teşhis aracı kurma, belge güncellemesi. Ölçüm için izin istemek ölçümü imkânsız kılar.
- Kullanıcı "sorma" ya da "onay bekleme" derse o oturum için onay kuralı askıya alınır.
- Onay bir adım içindir, sonraki adıma taşınmaz. Plan onaylandıysa bu, plandaki her dosyayı arka arkaya yazma yetkisi değildir.
- Kullanıcı Play'e bastığını söylediğinde `Logs/play.log` okunur. Bulunan hata ve uyarılar kullanıcıya düzeltme önerisi olarak sunulur.
- Değişiklik bitmeden **yan etkileri** kontrol edilir; kullanıcı bariz sonuçları bildirmek zorunda kalmaz:
  UI'ya içerik eklendiyse taşma/kaydırma, bileşene alan eklendiyse sahnedeki eski örneklerin yeniden
  bağlanması, serileştirilmiş varsayılan değiştiyse mevcut asset/sahnenin bundan etkilenmediği,
  imza değiştiyse tüm çağıranlar.
- "Yaptım, çalışıyor" denmeden önce doğrulanır. Unity'de doğrulama gerekiyorsa kullanıcıdan sonuç istenir.
