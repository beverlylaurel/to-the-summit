# To The Summit — Ortak Çalışma Kuralları

Unity 6000.5.6f1 ve URP kullanan, karlı ve fırtınalı bir dağ dünyasında geçen
birinci şahıs oyun projesi.

Ana dağ kalıcıdır ve kaldırılmaz. Oyun dağa tırmanma oyunu değildir; tür, yapı ve
ton kararları için `DESIGN.md` otoritedir.

## Çalışma biçimi

- Bu dosya Claude, Codex ve projede çalışan diğer ajanlar için ortak kuralları taşır.
- Ajan; kodu, dosyayı, ayarı, sahne kurulumunu ve doğrulamayı mümkün olduğu ölçüde
  kendisi yapar. Kullanıcıdan yalnızca gerçekten zorunlu editör etkileşimi istenir.
- Açıkça onaylanan görev veya plan, kapsamındaki normal ve geri alınabilir uygulama
  adımları için yeterli yetkidir. Her dosya ya da alt adım için yeniden onay istenmez.
- Ek onay yalnızca kapsam genişlemesi, geri alınması zor/yıkıcı işlem, kritik verinin
  üzerine yazma veya ücretli/kredili üretim gerektiğinde alınır.
- Okuma, arama, derleme, test, ölçüm, teşhis ve ilgili belge güncellemesi için onay
  gerekmez.
- Kullanıcı yeni bir mesajla kapsamı değiştirirse önceki çalışma güvenli bir noktada
  bırakılır ve yeni istek esas alınır.
- İş tamamlanmadan yan etkiler kontrol edilir; doğrulanmamış sonuç için “çalışıyor”
  denmez.

## Unity çalışma ortamı

- Unity açıkken konsol, Play modu ve editör işlemlerinde Unity MCP kullanılır.
- Derlemeyi tetiklemek için `Logs/refresh.trigger` dosyasının zamanı güncellenebilir.
- Ana sahne `Assets/Scenes/Game.unity`, test sahnesi `Assets/Scenes/TestGround.unity`.
- Sahne kurulumu `Assets/Editor/MountainSceneBootstrap.cs` tarafından yönetilir;
  kalıcı sahne düzeni elle kurulmaz.
- Enter Play Mode ayarlarında domain ve scene reload kapalıdır. Play modunda yapılan
  sahne değişiklikleri kalıcı kabul edilmez; sonuç edit modunda ve sahne dosyasında
  doğrulanır.
- Kullanıcı Play'e bastığını söylediğinde `Logs/play.log` ve Unity konsolu okunur.
- `To The Summit/Terrain/Regenerate Terrain` elle yontulmuş dağı sıfırdan üretir;
  yalnızca kullanıcı açıkça istediğinde çalıştırılır.

## Dil ve düzen

- `Assets/` altındaki kaynak kod, tanımlayıcı, yorum, log, shader property, menü yolu
  ve sahne nesnesi adları İngilizce olur; Türkçe karakter kullanılmaz.
- Oyuncunun gördüğü arayüz metni Türkçe olur.
- Proje belgeleri ve commit mesajları Türkçe kalır.
- Yeni genel sistemler `Assets/Scripts/<System>/` altında tutulur. Kar sistemi için
  mevcut `Assets/Snow/` istisnası korunur.
- Geçici teşhis artığı, kullanılmayan dosya, ölü kod, yorum içine alınmış eski kod ve
  gereksiz paket bırakılmaz.

## Mimari

- Bağımlılıklar açıkça enjekte edilir. Runtime kodunda `FindObjectOfType`, singleton
  ve `GameObject.Find` ile gizli bağımlılık kurulmaz.
- Bir bileşen tek sorumluluk taşır; sistemler event/callback veya dar arayüzlerle
  haberleşir.
- Public API yalnızca dışarıdan gerekli üyeleri içerir.
- Tasarımcı tarafından ayarlanacak, farklı örneklerde değişecek veya sistemlerce
  paylaşılacak ayarlar `ScriptableObject` içinde tutulur. Fiziksel sabitler, türetilmiş
  değerler ve yalnız uygulamaya ait ayrıntılar gerekçeleriyle kodda kalabilir.
- Serileştirilmiş alan eklendiğinde mevcut sahne/asset örneklerinin bağları; imza
  değiştiğinde bütün çağıranlar kontrol edilir.

## Dünya durumu ve atmosfer

Hava, rüzgâr, bulut, sis, ışık, ses ve renk düzenlemesi bağımsız ikinci kaynaklar
üretmez. Mevcut kaynaklar:

- `WeatherState`: yağış şiddeti
- `WindField`: rüzgâr vektörü, şiddeti ve esinti
- `TimeOfDay`: saat ve gök ışıkları
- `TemperatureField`: sıcaklık ve donma seviyesi

Bir bağ eklenmeden önce tüketilen değerin fiziksel anlamı yazılır: örneğin radyans,
ışınım, yüzey rengi veya yön bağımlı gök örneği. Uç koşullar şafak, öğle, gece,
kapalı hava ve fırtınada hesaplanır. HDR ve üst sınırsız kaynaklarda kör katsayı yerine
fiziksel anlamı olan sınır kullanılır; renk tonu ile parlaklık ayrı ele alınır.

Sistem bağlarının güncel haritası `SYSTEMS.md` içindedir ancak davranışın son otoritesi
koddur. Belge ile kod çelişirse kod ölçülür ve ilgili belge düzeltilir.

## Teşhis ve ölçüm

- Kod belirtiyi açıklamıyorsa tahminle düzeltme yapılmaz; hipotezleri ayıran ölçüm
  veya teşhis görünümü hazırlanır.
- Aynı belirti için iki başarısız düzeltmeden sonra üçüncü deneme doğrudan yeni bir
  ayar değil, doğrulanmış bir ölçüm aracı olur.
- Teşhis aracının boş kontrol vakası bulunur. Tonemap, pozlama, ışık veya hareketli
  arka planın sonucu kirletmediği kanıtlanır.
- Ölçüm tamamlanmadan aynı belirtiye yeni telafi terimi eklenmez.
- Geçici ölçüm kodu iş bitince silinir; gelecekte regresyonu yakalayan test ve araçlar
  kalıcı tutulur.

## Belge yönlendirmesi

Her görevde bütün belgeler okunmaz. Önce görevle ilgili olan seçilir:

- `DESIGN.md`: oynanış, anlatı, yapı veya ton değişikliği
- `SYSTEMS.md`: sistemler arası bağ ekleme, kaldırma veya değiştirme
- `RATIONALE.md`: önemli teknik karar, ölçüm, reddedilen yaklaşım veya kural gerekçesi
- `SYMPTOMS.md`: daha önce görülmüş belirti; yeni kayıt yalnız ölçülmüş kök neden
  kapandığında eklenir
- `DECISIONS.md`: bilinçli erteleme veya sınır; tetikleyici ve maliyetle birlikte
- `SCALE.md`: dağ ölçeğine bağlı sayı ya da davranış
- `COOP.md`: ağ katmanı geldiğinde yeniden yazılacak yeni borç
- `CLOUDS_REBUILD.md`: bulut modeline veya shader zincirine dokunan değişiklik
- `BLENDER.md`: Blender'da varlık üretimi — geometri, UV, doku, denetim kuralları

Yalnızca değişiklik gerçekten o belgenin sorumluluğunu etkiliyorsa belge güncellenir.
Sayılar ve ayar değerleri belgelerde çoğaltılmaz; kod veya ayar asset'i otoritedir.

## Kritik veri ve ücretli üretim

- `Assets/Terrain/MountainTerrainData.asset` elle yontulmuş, git dışında tutulan kritik
  veridir. Üzerine yazmadan önce yedek alınır.
- Arazi yüksekliği değişirse yüzey, normal, ufuk, yükseklik ve rüzgâr haritaları aynı
  iş kapsamında geçersiz kılınıp yeniden pişirilir; ayrıntılar `SCALE.md` ve ilgili
  editör araçlarından doğrulanır.
- Ücretli veya kredili varlık üretimi başlamadan önce kullanıcıya maliyet ve üretilecek
  varlıklar açıkça söylenir, ayrıca onay alınır.
- Beğenilmiş kimliği korumak için sonraki üretimlerde metni yeniden icat etmek yerine
  mevcut görsel referans kullanılır.
