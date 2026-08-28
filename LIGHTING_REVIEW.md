# Işıklandırma ve Gölgelendirme Analizi

Tarih: 2026-08-28 · Kapsam: ışık zinciri (TimeOfDay, Atmosphere, PBSky paketi), gölge
sistemi (ufuk haritası, kaskad gölge haritası, bulut cookie'si), sis/hava perspektifi,
deniz, kar, bulut ve pozlama/ton eşleme. Yalnızca analiz — hiçbir dosya değiştirilmedi.

## Özet yargı

Mimari sağlam. Işık tek durumdan türüyor (`TimeOfDay` → güneş/ay ışıkları → PBSky göğü
→ ambient probe → sis/bulut/arazi), renk elle seçilmiyor (`Atmosphere.BeamTransmittance`
fizikten türetiyor), pozlama adaptasyonu gerçek miktarlardan okunuyor, arazi gölgesi üç
yolla ve "her harita kendi sorusunu cevaplar" kuralıyla kurulmuş. Bu kısma dokunulmamalı.

Bulunan sorunların tamamı iki ailede:

1. **Entegrasyon boşlukları** — sis ve bulut gölgesi zinciri sahnedeki her yüzeye
   ulaşmıyor. Arazi zincirin tamamını alıyor; **deniz yerel sisi hiç almıyor**, deniz,
   bisiklet ve karlı nesneler bulut gölgesi cookie'sini almıyor.
2. **Bayatlaşmış belge/yorum ayrışmaları** — kod değişmiş, yorum/tooltip/belge geride
   kalmış (ay gölgesi, gölge mesafesi, `Sky.mat` fallback yolu, bir matematik invariantı).

Kritik görsel sonuç: fırtınada dağ yüzlerce metrede sise gömülürken deniz ufka kadar
keskin kalıyor; kapalı havada bisiklet ve karlı kayalar çevresinden daha parlak duruyor.

## Bulgular özeti

| # | Önem | Sorun | Yer |
|---|------|-------|-----|
| 1 | Yüksek | Deniz yerel sise girmiyor (`MixFog` ama Unity fog kapalı; ayrıca PBSky saçılma geçişinden sonra çiziliyor) | `SeaLit.shader:42,490` |
| 2 | Yüksek | Bulut gölgesi cookie'sini yalnız arazi (elle) ve kar tanesi uyguluyor; deniz, bisiklet, karlı nesneler almıyor | `MountainSurface.shader:258` çevresi |
| 3 | Orta | `SnowCoverObject` ve `SnowfallParticle` da `MixFog` kullanıyor → yerel sis yok | `SnowCoverObject.shader:218`, `SnowfallParticle.shader:187` |
| 4 | Orta | Alpenglow hava durumuna bağlı değil; fırtınada da tüm güçle yanıyor | `TerrainSurface.cs:235-263` |
| 5 | Orta-Düşük | Denizin doğrudan ışığı hiçbir gölge zayıflatması almıyor (parıltı, su rengi, köpük) | `SeaLit.shader:206,293,476` |
| 6 | Düşük | Ay ışığı tooltip'i "gölge atmaz" diyor; sahne/bootstrap Soft gölge kuruyor | `TimeOfDay.cs:45-47` |
| 7 | Düşük | `TimeOfDay` alan varsayılanları sahne/bootstrap ile ayrışmış (ay şiddeti 10×) | `TimeOfDay.cs:36,39,43` |
| 8 | Düşük | `Sky.mat` fallback yolu yarım-ölü: `_StarStrength`/`_MoonDirection` yazan yok, veil yazıları yalnız fallback'e | `AtmosphereController.cs:451-452,461` |
| 9 | Düşük | "CurrentSunColor × intensity = gerçek hüzme" invariantı matematiksel olarak tutmuyor (LowSunFade iki kez giriyor) | `TimeOfDay.cs:306-344` |
| 10 | Düşük | Gölge mesafesi belge/koment drift: belge 60 m, yorum 50 m, asset 150 m | `SYSTEMS.md` Arazi ışığı; `PC_RPAsset.asset:57` |

---

## Bulgular ve çözüm önerileri

### 1. Deniz yerel sise girmiyor — Yüksek

**Kanıt.** `SeaLit.shader:42` `#pragma multi_compile_fog` ve `:490`
`color = MixFog(color, IN.fogCoord);` kullanıyor. Ama Unity fog'u projede hiç açılmıyor:
`Game.unity:17` `m_Fog: 0` ve runtime'da `RenderSettings.fog` yazan tek satır yok
(arama yapıldı; tek eşleşme `ISnowEnvironmentSource.cs` içindeki "kullanmıyoruz" yorumu).
Kapalı fog ile `MixFog` derleme olarak kimlik fonksiyon — **sıfır etki**.

Paketin aerial perspective'i de kurtarmıyor: PBSky'nin opak atmosferik saçılma geçişi
gökyüzünden hemen sonra çalışıyor (`VolumetricFogFeature.cs:54-69` yorumu bunu
doğruluyor), deniz ise `Queue = Transparent-1` (2999) ile bulutlardan bile sonra
çiziliyor. Yani deniz pikseli hem kendi shader'ında sis uygulamıyor hem üstüne
çizildiği paket saçılmasını örtyor.

**Etki.** Arazi `ApplyHeightFog` (yerel sis + banklar + sürülen kar) **ve** PBSky
perspektifi alırken deniz ikisini de almıyor. Fırtınada (140 m görüş) dağ 300 metrede
kaybolurken deniz ufka kadar doygun türkuaz kalır; şafakta vadisi sise gömen hava
denizin üstünde yoktur. Projenin kendi kuralı (`HeightFog.hlsl` başlığı: "her yüzey
aynı havada durur... ikinci bir yüzey geldiğinde sislenmemiş gelir") tam olarak bu
durumu yasaklıyor.

**Çözüm.** `SeaFragment` sonunda `MixFog` satırını
`color.rgb = ApplyHeightFog(color.rgb, _WorldSpaceCameraPos, IN.positionWS);` ile
değiştirmek (`BikeSurface.shader:290` referans implementation) ve `multi_compile_fog`
pragmasını kaldırmak. `HeightFog.hlsl` zaten bulut/sis kuyruğuyla (`FogPath`) uyumlu;
deniz kendi mesafesiyle sislenir — "her katman kendi mesafesiyle bir kez sislenir"
kuralına tam oturur. Kırılganlık düşük: `ApplyHeightFog` global'lerden okuyor, denize
özel yeni bağ gerektirmiyor.

**Ölçüm.** Değişiklik öncesi/sonrası fırtına kadrajında aynı deniz pikselinin
parlaklığı karşılaştırılmalı (color probe) — sis devreye girdiğine dair sayısal kanıt
elde edilmeden "oldu" denmemeli.

### 2. Bulut gölgesi cookie'si yüzeylerin çoğuna ulaşmıyor — Yüksek

**Kanıt.** Bulut gölgesi ana ışığın cookie dokusuna yazılıyor ve yüzeylerin bunu
uygulaması gerekiyor. Uygulayanlar: arazi — `MountainSurface.shader:258-260` (
`SampleMainLightCookie`'yi **elle** çarpıyor, `UniversalFragmentPBR`'yi elle yazdığı
için) ve kar tanecikleri — `SnowfallParticle.shader:47` (pragma var). Uygulamayanlar:

- `SeaLit.shader` — `_LIGHT_COOKIES` pragması yok, elle de çarpmıyor.
- `SnowCoverObject.shader:55-62` — pragma yok; `GetMainLight(IN.shadowCoord)`
  overload'u pozisyon almadığı için cookie örnekleyemez.
- `BikeSurface.shader:63-76` — pragma yok.

**Etki.** Kapalı havada veya bulut geçerken zemin kararıyor ama üstündeki bisiklet,
kayalar, ekipman ve deniz tam güneşle parlıyor. "Gökyüzü kapalı ama nesneler güneşli"
— projenin atmosfer tutarlılık kuralının (CLAUDE.md "hava, ... ışık ... çelişemez")
tam ihlali.

**Çözüm.** Üç shader'a da MountainSurface'taki aynı iki satırı taşımak:
`#pragma multi_compile_fragment _ _LIGHT_COOKIES` + `mainLight.color *=
SampleMainLightCookie(positionWS);`. Denizde bu çarpan parıltıya, su rengine
(`waterLight`) ve köpük ışığına **hepsine aynı anda** girmeli (aşağıda #5 ile birleşir).
Standard `UniversalFragmentPBR`/InputData yolunu kullanan shader'larda pragma tek
başına yeter; bike elle yazılmış akışta değilse önce kontrol edilmeli.

### 3. Karlı nesneler ve kar tanecikleri de `MixFog` kullanıyor — Orta

**Kanıt.** `SnowCoverObject.shader:61,218` ve `SnowfallParticle.shader:44,187`
`multi_compile_fog` + `MixFog` — #1 ile aynı sebep, sıfır etki.

**Etki.** `SnowCoverObject` opak kuyrukta çizildiği için PBSky'nin opak saçılmasını
kısmen alır (Rayleigh/uzak hava perspektifi); ama vadi sisi, bankalar ve sürülen kar
perdesi — `HeightFog`'un asıl sahibi olduğu medium — ona da ulaşmaz. Bisiklet
(`BikeSurface`) `ApplyHeightFog` kullanırken aynı sahnedeki karlı kaya kullanmaz: aynı
havada iki görünürlük. `SnowfallParticle` transparan olduğundan hiçbir sis görmez.

**Çözüm.** İkisinde de `MixFog` → `ApplyHeightFog` (pos ile). Parçacık tarafında maliyet
dikkat: `ApplyHeightFog` 8 örnekli integral + LUT okuma içeriyor; kar taneciği shader'ında
gerekirse `FogPath`'in transmittance-only kısmı ya da daha kaba adım sayısı düşünülebilir
— ama önce ölçülüp gerekli görülmeden eklenmemeli.

### 4. Alpenglow hava durumuna bağlı değil — Orta

**Kanıt.** `TerrainSurface.ApplyAlpenglow` (`TerrainSurface.cs:235-263`):
`strength = horizon² × alive × settings.alpenglowStrength`. Yağış, kapsama veya rüzgâr
terimi yok. Shader tarafında (`MountainSurface.hlsl:270-314`) doğrudan fazın kapısı
`TerrainSunShadow` — arazi geometrisi. Bulut cookie'si ve gölge haritası emission
toplamına (`lit += surface.emission`) uygulanmıyor.

**Etki.** Şafak + fırtına kombinasyonunda yoğun bulut kütlesinin arkasında kalan dağ
yüzeyi yine de kızıl alpenglow ile yanar. Sis paleti aynı durumda bilinçli olarak
soluyor (`AtmosphereController`: `duskOvercast` ile karartma) — iki türev çelişiyor.

**Çözüm.** İki seçenek, karar gerektirir: (a) bağ kur — `ApplyAlpenglow` içine
`precipitation`/`Coverage` ile doğrudan fazın gücünü kesecek bir çarpan (artçı faz
gökyüzü ışığından geldiği için tamamen sıfırlanmamalı, doğrudan faz öncelikli kesilmeli);
(b) bilinçli
kural olarak `SYSTEMS.md`'ye yaz ("alpenglow bulut örtüsünü yok sayar, gerekçe: X").
Mevcut hal ikisi de değil — belgesiz bir boşluk.

### 5. Deniz doğrudan ışıkta hiçbir gölge zayıflatması kullanmıyor — Orta-Düşük

**Kanıt.** `SeaLit.shader:206` `GetMainLight()` argümansız — bu overload URP'de
`shadowAttenuation = 1` döndürür ve pozisyon almadığı için cookie örnekleyemez.
Parıltı (`:293 glitter = mainLight.color * spec`), su rengi (`:235 waterLight`) ve
köpük (`:476` — `mainLight.shadowAttenuation` okuyor ama kaynağı 1) hep zayıflatmasız.

**Etki.** Dağın gölgesinde/ama bulut gölgesinde kalan su hâlâ güneş yolu çizer. #2 ile
birlikte çözülür: cookie çarpanı `mainLight.color`'a bir kez uygulanınca parıltı, su
rengi ve köpük zincirin tamamı takip eder. Arazi gölge haritasının denize uygulanması
ayrı ve daha pahalı bir iş (derinlik okuma + koordinat); sahil çizgisinde arazi gölgesi
önemsiz derecede nadir göründüğünden öncelik değil — cookie yeter.

### 6. Ay ışığı tooltip'i bayat — Düşük

**Kanıt.** `TimeOfDay.cs:45-47` tooltip: "It CASTS NO SHADOW: the sky package does not
count a shadowless object as the main light...". Ama `MountainSceneBootstrap.cs:1165`
`moon.shadows = LightShadows.Soft` kuruyor ve sahnede `Moon Light` `m_Shadows.m_Type: 2`
(Soft). `MarkAsSun`'ın gece devri (`TimeOfDay.cs:212-232`) ayın ana ışık olmasını ve
gölge atmasını zaten gerektiriyor — kod doğru, tooltip eski durumda kaldı.

**Çözüm.** Tooltip güncellenmeli: ay artık gölge atıyor; sebep (gece ana ışığın ay olması)
ve PBSky bağlantısı yeni metne taşınmalı. Projenin "belge aynı adımda düzeltilir" kuralı
gereği tek satırlık düzeltme.

### 7. `TimeOfDay` varsayılanları sahne ile ayrışmış — Düşük

**Kanıt.** `TimeOfDay.cs` alan varsayılanları: `moonIntensity = 0.204f` (`:43`),
`moonColor = (0.52, 0.64, 1.00)` (`:36`), `sunColor = (1, 0.97, 0.92)` (`:23`).
Bootstrap (`MountainSceneBootstrap.cs:400-402`) ve sahne (`Game.unity:1117-1120`):
`moonIntensity = 0.0199` (10× fark), `moonColor = (0.586, 0.653, 0.818)`,
`sunColor = (1, 0.96, 0.89)`. Kod `sunIntensity` için açıkça "sahne de yazıyor,
varsayılan burada güncellendi ki ayrışmasın" diyor (`:38-39`) — kural ay için
uygulanmamış.

**Etki.** Pratikte sahne değeri kazanır; ama yeni bir sahneye `TimeOfDay` eklendiğinde
veya bootstrap çalışmadan açıldığında gece 10× daha parlak olur — sessiz ayrışma.

**Çözüm.** `TimeOfDay` varsayılanlarını bootstrap değerleriyle eşitlemek (ya da
bootstrap'ın değer yazmayı bırakıp tek kaynağı component yapmak — ikisinden biri,
ikisi birden değil).

### 8. `Sky.mat` fallback yolu yarım-ölü — Düşük

**Kanıt.** Canlı gökyüzü PBSky (`GameProfile.asset` VisualEnvironment `skyType: 4`,
sahne `m_SkyboxMaterial: {fileID: 0}`); `Sky.mat` yalnız `m_FallbackSkyMaterial` olarak
bağlı (`MountainSceneBootstrap.cs:622-623`). Bu durumda:

- `AtmosphereController.ApplySky` (`:451-452`) disc veil renklerini (`_SunColor`,
  `_MoonColor`) fallback materyale yazıyor — canlı gökyüzünde etkisi yok (canlı disk
  ana ışıktan çiziliyor, kapama işini bulut kompozisyonu yapıyor).
- `Sky.shader`'ın yıldızları `_StarStrength`'ten besleniyor (`:37`) — **yazan yok**
  (arama: yalnız shader'da geçiyor). `_MoonDirection` için de aynısı; silindiği
  yorumla da kabul edilmiş (`AtmosphereController.cs:461-462`).
- `_DisableSunDisk`'i bulut ambient probu doğru yazıyor — o taraf çalışıyor.

**Etki.** Fallback devreye girdiğinde (LUT hazır değilkenki ilk kareler) yıldızsız ve
aysız bir gök çıkar; veil yazıları ölü koda dönüşmüş. Proje kuralı "çöp kod yok" bunu
kapsıyor.

**Çözüm.** Ya fallback'i gerçekten beslemek (`SkyWeatherDriver`'a yıldız şiddeti ve ay
yönü yayınlamak, veil yazılarını oraya taşımak) ya da `Sky.shader`'ı bilinçli olarak
"sadece hava rengi + güneş diski" fallback'ine indirip yıldız/ay kodunu kesmek. Yarı
durum kalmasın.

### 9. Hüzme invariantı yorumda tutmuyor — Düşük (belge)

**Kanıt.** `TimeOfDay.cs:340-344`: yorum "CurrentSunColor × intensity gerçek hüzmeyle
eşit kalır" diyor; matematik: `CurrentSunColor = Tint(beam·sunColor) · sunFade`
(`:310-312`) ve `intensity = sunIntensity · SunBlend · peak(beam) · sunFade`
(`:343-344`). Çarpım `beam · sunColor · sunIntensity · SunBlend · sunFade²` —
`LowSunFade` **iki kez** giriyor. Bulut tarafında kare alınması bilinçli ve belgeli
(`AtmosphereController.cs:499-501`, `cloudWarm *= cloudWarm`); ışık tarafındaki kare
muhtemelen aynı ailenin görünüm kararı ama invariant yorumu bunu söylemiyor.

**Çözüm.** Yorumu düzeltmek: "alçak güneşte hüzme bilinçli olarak iki kez söndürülür
(bir kez renkte, bir kez şiddette)" ya da karesi istenmiyorsa `sunFade`'i tek yerden
girmek. Sayı değişikliği görsel sonucu değiştirir — yalnızca belge düzeltmesi önerilir.

### 10. Gölge mesafesi belge drift — Düşük

**Kanıt.** `SYSTEMS.md` (Arazi ışığı): "gölge haritası | URP cascade | 60 m".
`MountainSurface.shader:40` civarı yorum: "it ends at fifty metres". Gerçek:
`PC_RPAsset.asset` `m_ShadowDistance: 150`, `AtmosphereSettings.asset`
`maxShadowDistance: 150` ve `ApplyShadowDistance` pratikte hep 150'e oturuyor
(berrak görüş 25 km × oran 0.8 > 150). `TimeOfDay` tooltip'lerindeki "50 m"li hiçbir
sayı artık var olmayan bir kalibrasyona referans veriyor olabilir.

**Çözüm.** Belge ve yorum 150 m'ye güncellenmeli; `SCALE.md` kapsamında mı diye
kontrol edilmeli (gölge mesafesi dağın boyuna bağlı bir sayı mı — bağlıysa orada da
kayıt olmalı).

---

## Sağlam olan ve dokunulmaması gerekenler

- **Tek kaynak zinciri:** güneş/ay renk ve şiddeti fizikten (`Atmosphere`), gök PBSky,
  ambient probu gökten (`SkyAmbientBaker` + bir-kare-geriden takip bake'i), sis rengi
  probun DC'sinden (`AmbientLevel` → `LevelScale`). İkinci kaynak yok.
- **Arazi gölgesinin üç yolu** ve "her harita kendi sorusunu cevaplar" ayrımı
  (ufuk haritası = sırt arkası, kaskad = hareketli nesne, cookie = bulut) —
  çarpımla birleşiyor, çift sayım yok.
- **Pozlama adaptasyonu:** gerçek miktarlardan (`SurfaceLightLevel`, probe zenit),
  kısmi kapanma (`adaptShare` 0.35), asimetrik zaman sabitleri (karanlığa yavaş,
  ışığa hızlı). Alt sınırın 0.0005'e çekilmesiyle alacakaranlık düzleşmemiş.
- **Sis sahipliği bölümü:** Rayleigh/aerosol PBSky'da, lokal medium (vadi sisi,
  bankalar, sürülen kar) `HeightFog`'da — `_HeightFogChroma`'nın silinmesi bu
  bölümün bilinçli sonucu.
- **Kar aydınlatması:** mesh ve arazi aynı modelden (`SnowDirectLight`/`SnowAmbient`),
  bölge sınırındaki parlaklık farkı ölçülüp kapatılmış; kar yansımalı güneş terimi
  (görüş faktörü ders kitabı formülü) gölge çarpanı bilinçli almıyor.
- **Deniz optiği:** tam Fresnel (Schlick değil), GGX parıltı, köpük ışığının
  ışınım olarak kısılması — fizik tarafı tutarlı. Sorunlar yalnızca ışık/sis
  entegrasyonunda (#1, #2, #5).

## Önerilen sıra

1. #1 + #2 + #5 (deniz kenarı: sis + cookie + gölge zayıflatması tek geçişte) —
   en yüksek görsel getiri, en düşük risk.
2. #3 (nesne karı + kar taneciği sisi) — aynı reçete, ayrı adım.
3. #4 — bağ kurma kararı (SYSTEMS.md + RATIONALE.md ile birlikte).
4. #6-#10 — belge/tooltip/varsayılan temizliği; davranış değiştirmiyor, ayrı commit.

Her düzeltme öncesi projenin kuralı geçerli: belirti ölçülecek (color probe / fog
denetim görünümü), ölçüm gelmeden bir sonraki düzeltme yazılmayacak.
