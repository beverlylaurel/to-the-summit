# Plan: Volumetrik Sis (Wronski 2014 froxel hacmi)

## Özet

Mevcut analitik yükseklik sisi, kamera frustum'una hizalı 3B froxel hacmiyle değiştirilir
(Wronski 2014, SIGGRAPH). Yoğunluk modeli KORUNUR — sınır tabakası, inversiyon, serbest
troposfer, vadi sis denizi, banklar ve spindrift aynı fizikle devam eder — ama artık iki
yerde değerlendirilir: hacim içinde froxel yürüyüşüyle, hacmin ötesinde analitik olarak.
Kazanılan: ışın huzmeleri (god ray), bulut gölgelerinin sise düşmesi, gölgeli alanlarda
doğru ambient, saydam katmanlarda tutarlı sis.

## Kullanıcı hikâyesi

Dağa tırmanan oyuncu olarak, bulut katmanının altından süzülen ışık huzmelerini ve vadide
biriken sisin içinden geçen güneşi görmek istiyorum; böylece mesafe ve hacim algısı gerçek
bir dağda olduğu gibi kurulsun.

## Problem → Çözüm

**Şimdi:** Sis analitik bir integral; her yüzey shader'ı `ApplyHeightFog` çağırıyor. Sis
gölgelenmiyor — güneşin önündeki dağ veya bulut sise gölge düşürmüyor, dolayısıyla huzme
yok. Gölgeli alanlarda sis yalnız sönümlüyor, içeri saçmıyor.

**Sonra:** Sis, gölgelenmiş bir hacimden geliyor. Her froxel kendi yoğunluğunu ve kendi
içeri saçılan ışığını taşıyor; ışın boyunca birikim Beer-Lambert'e göre çözülüyor.

## Künye

- **Karmaşıklık:** XL (yeni alt sistem, compute shader, projede ilk)
- **Kaynak spec:** `C:\Users\musta\Desktop\tts\specs\fog\fog-spec.md` (971 satır, Wronski 2014 birebir)
- **Tahmini dosya:** 11 (4 yeni, 7 değişen)
- **Kullanıcı kararları:** dört fork da onaylandı (aşağıda "Kilitlenen kararlar")

---

## Kilitlenen kararlar

Bunlar spec'te AÇIK NOKTA olarak işaretli; kullanıcı tarafından karara bağlandı. Plan
uygulanırken tartışmaya açılmaz.

| # | Karar | Gerekçe |
|---|---|---|
| 1 | **Tek froxel hacmi + analitik kuyruk.** Hacim 0–1000 m; ötesi mevcut analitik integralle sürer. | Spec'in doğruladığı menzil 50–128 m; kademeli yaklaşımı ADLANDIRIR ama TANIMLAMAZ (`s.25`, §12.1). Doğrulanmış alanda spec, dışında bilinen çalışan çözüm. |
| 2 | **Homojen atmosferin sahibi gökyüzü paketi kalır.** Yeni sis yalnız yerel/heterojen ortamı taşır. | Spec'in kendi önerisi (§8.4, §12.2): çift sayım riski kompozisyonda değil, aynı olguyu iki kez saymakta. |
| 3 | **Bulut gölgeleri hacme enjekte edilir.** | Spec bunu hiç ele almıyor (§12.3) ama "bulut altından süzülen huzmeler" hedef problemlerden biri (§8.1) ve ESM tek başına bulut gölgesi taşımıyor. |
| 4 | **Mevcut dört katman + banklar + spindrift yoğunluk modeline taşınır.** | Davranış korunur, uygulama değişir. |

**Kararların birleşik sonucu — TEK YOĞUNLUK MODELİ, İKİ DEĞERLENDİRİCİ.** Karar 1 ile
karar 4 birbirine bağlı: analitik kuyruk da katmanları taşımak zorunda, yoksa hacmin
ötesinde sisin yapısı kaybolur. Bu yüzden `FogDensityAt` / `FogBankAt` / `SpindriftAt`
**silinmez**, ortak yoğunluk kaynağı olarak kalır; hem compute shader hem analitik kuyruk
onu çağırır.

---

## Zorunlu okuma

| Öncelik | Dosya | Satır | Neden |
|---|---|---|---|
| P0 | `Assets/Shaders/HeightFog.hlsl` | 1–130 | Global sözleşmesi: 27 global, hangi sistem yazıyor |
| P0 | `Assets/Shaders/HeightFog.hlsl` | 225–300 | `FogDensityAt`, `FogBankAt`, `FogBankPath` — korunacak yoğunluk modeli |
| P0 | `Assets/Shaders/HeightFog.hlsl` | 282–350 | `HeightFogIntegral` — analitik kuyruğun temeli |
| P0 | `Assets/Shaders/HeightFog.hlsl` | 579–613 | `ApplyHeightFog` — çağrı sözleşmesi, imza korunacak |
| P0 | `Assets/Scripts/Environment/AtmosphereController.cs` | 640–800 | Sis globallerinin yazıldığı yer; yeni ayarlar buraya |
| P1 | `Assets/VolumetricClouds/VolumetricCloudsURP.cs` | 1061–1200 | URP 17 RenderGraph deseni: `AddUnsafePass<PassData>` + `SetRenderFunc` |
| P1 | `Assets/VolumetricClouds/VolumetricCloudsURP.cs` | 1511–1600 | `_VolumetricCloudsShadowTexture` ayrılması ve bağlanması |
| P1 | `Assets/Scripts/Environment/AtmosphereSettings.cs` | tamamı | Ayar asset deseni |
| P1 | `Assets/Editor/MountainSceneBootstrap.cs` | 655–700 | Render feature kurulum deseni (`SerializedObject` ile shader bağlama) |
| P2 | `Assets/Shaders/MountainSurface.shader` | 170–180 | Tek `ApplyHeightFog` çağrı yeri |
| P2 | `Assets/Shaders/Sky.shader` | 110–230 | Yedek gökyüzü — `AirColor`/`SkyFogAmount` yalnız burada yaşıyor |
| P2 | `SYSTEMS.md` | 331–420 | Sis bölümü; aynı adımda güncellenecek |

## Dış dokümantasyon

| Konu | Kaynak | Ana çıkarım |
|---|---|---|
| Froxel hacmi, geçiş grafiği | spec §4, §5 | 4 geçiş: ESM → yoğunluk+aydınlatma → ışın yürüyüşü → uygulama |
| Beer-Lambert birikimi | spec §5.3.3 | `AccumulateScattering` ve `WriteOutput` kodu birebir alınacak |
| Ambient SH ürün integrali | spec §6.1 | HG'nin zonal SH açılımı: `float4(1, dir.y, dir.z, dir.x) * float4(1, g, g, g)` |
| ESM | spec §5.1, `s.64` | Downsample + `exp(z*EXPONENT)`, `shadow = saturate(occluder/receiver)` |
| Ters Z tuzağı | spec §9.3, §12.7 | Üstel derinlik dağılımı LİNEER görüş uzayı derinliğinden kurulacak |

---

## Kâğıtta hesaplanan sayılar

Spec bunların hiçbirine sayı vermiyor (§10.1). CLAUDE.md gereği bağlanmadan önce
hesaplandı.

### Hacim çözünürlüğü ve bellek
`160 × 90 × 64`, `R16G16B16A16_SFloat` = `160×90×64×8 B` = **7,37 MB**. İki doku
(yoğunluk+in-scattering, birikmiş saçılım) = **14,7 MB**. Kabul edilebilir; mevcut bellek
kullanımı ~1,3 GB.

### Derinlik dağılımı
Saf üstel (logaritmik dilim): `z(s) = near · (far/near)^s`, `s ∈ [0,1]`, **lineer görüş
uzayı derinliği** üzerinde.
- `near = 0.5 m`, `far = 1000 m` → dilim başına oran `(2000)^(1/63) = 1,128`, yani **%12,8**.
- Dilim 0 kalınlığı 0,064 m, dilim 63 kalınlığı 113 m.

**Neden 1000 m:** vadi sis denizi ve banklar kilometrelerce uzanır ama YAPI olarak
yakın-orta menzilde okunur; ötesini analitik kuyruk zaten doğru veriyor. Spec'in
doğruladığı 128 m'nin sekiz katı, ama üstel dağılım sayesinde yakın alan hassasiyeti
spec'inkinden düşük değil (spec 64 dilimi 128 m'ye yayıyordu, biz ilk 128 m'ye 37 dilim
koyuyoruz: `log(128/0.5)/log(1.128) = 46`).

### Sönümleme katsayısı birimi
**1/m.** Koschmieder: görüş mesafesi `V = 3.912/β`. HUD'daki 4245 m görüşe karşılık
`β = 9,2e-4 1/m`. Yani yoğunluk profili doğrudan 1/m döndürür; mevcut `_HeightFogDensity`
zaten bu mertebede.

### ESM `EXPONENT`
**80.** Taşma sınırı: `exp(80) = 5,5e34 < 3,4e38` (float32 tavanı) ✓. Daha küçük değer
(ör. 40) shadow leaking'i artırır; daha büyük değer taşar.

### Faz fonksiyonu anizotropisi
Sis kendi `g` değerini taşır (gökyüzünün `_AerosolAnisotropy`'sinden AYRI — farklı ortam:
sis su damlacığı, aerosol toz). Başlangıç `g = 0.6`; su damlacığı ileri saçılımı
belirgindir ama bulut kadar değil.

### Hacim sınırında süreklilik
`far`'ın ötesi analitik kuyruk. Kompozisyon Beer-Lambert gereği:
```
T_toplam = T_hacim × T_kuyruk
L_toplam = L_hacim + T_hacim × L_kuyruk
```
Kuyruk aynı yoğunluk modelini `far`'dan yüzeye kadar integre ettiği için geçiş
**yapı gereği sürekli**; ayrıca blend penceresi gerekmez.

---

## Değişecek dosyalar

| Dosya | İşlem | Gerekçe |
|---|---|---|
| `Assets/Shaders/VolumetricFog.compute` | YENİ | Üç kernel: yoğunluk+aydınlatma, ışın yürüyüşü, (ESM ayrı geçiş) |
| `Assets/Shaders/VolumetricFogShared.hlsl` | YENİ | Froxel↔dünya dönüşümleri, hacim UVW hesabı; hem compute hem yüzey shader'ı okur |
| `Assets/Scripts/Fog/VolumetricFogFeature.cs` | YENİ | `ScriptableRendererFeature`; dört geçişi RenderGraph'a kaydeder |
| `Assets/Scripts/Fog/VolumetricFogSettings.cs` | YENİ | `ScriptableObject`: menzil, çözünürlük, `g`, ESM üsteli, temporal ağırlık |
| `Assets/Shaders/HeightFog.hlsl` | DEĞİŞİR | Yoğunluk modeli KALIR; `ApplyHeightFog` hacmi örnekleyip kuyruğu ekler; `_HeightFogChroma` silinir |
| `Assets/Scripts/Environment/AtmosphereController.cs` | DEĞİŞİR | `_HeightFogChroma` yazımı silinir; sis ayar asset'i bağlanır |
| `Assets/Scripts/Environment/AtmosphereSettings.cs` | DEĞİŞİR | Chroma alanları silinir |
| `Assets/Editor/MountainSceneBootstrap.cs` | DEĞİŞİR | Feature kurulumu, ayar asset'i, gece seviyesi yeniden ölçümü |
| `SYSTEMS.md` | DEĞİŞİR | Sis bölümü yeniden yazılır; bağlar güncellenir |
| `DECISIONS.md` | DEĞİŞİR | Kilitlenen dört karar + kapanan iki bekleyen ölçüm |
| `SCALE.md` | DEĞİŞİR | Hacim menzili ve dilim sayısı "bilerek mutlak" |

## Yapılmayacaklar

- **Kademeli (cascaded) hacim.** Karar 1 ile elendi.
- **Async compute.** Spec öneriyor (§6.4) ama URP'de kurulumu ayrı iş; önce doğru sonuç, sonra ölçüm.
- **Point light döngüsü.** Sahnede şimşek dışında dinamik ışık yok; Forward+ cluster verisi bağlanmayacak.
- **Partikül / analitik şekil enjeksiyonu** (spec §6.6). Vadi sisi ve banklar zaten yoğunluk profilinde.
- **Saydam nesnelere çok katmanlı uygulama.** Sahnede sisin içinden bakılan saydam yüzey yok.
- **Yedek gökyüzü materyalinin (`Sky.shader`) sis yolu.** Dokunulmuyor; `AirColor`/`SkyFogAmount` orada kalıyor.
- **Birim test.** Projede test yok ve doğrulama kültürü ölçüm tabanlı (aşağıya bak).

---

## Adım adım görevler

### Görev 1 — Yoğunluk modelini paylaşılabilir hâle getir
- **EYLEM:** `HeightFog.hlsl`'deki `FogDensityAt`, `FogBankAt`, `SpindriftAt` ve bağlı
  globalleri `VolumetricFogShared.hlsl`'e taşı; `HeightFog.hlsl` onu include etsin.
- **NEDEN:** Compute shader `HeightFog.hlsl`'i include edemez — o dosya yüzey shader
  bağlamına (`_WorldSpaceCameraPos`, URP aydınlatma) bağlı.
- **DOĞRULAMA:** Derleme temiz; sahne görüntüsü DEĞİŞMEMELİ (saf taşıma).

### Görev 2 — Froxel dönüşümleri
- **EYLEM:** `VolumetricFogShared.hlsl`'e `FroxelUVWFromViewDepth`, `ViewDepthFromSlice`,
  `FroxelWorldPos` ekle. Dağılım: `z(s) = near·(far/near)^s`.
- **TUZAK:** **Ters Z.** Dağılım lineer görüş uzayı derinliğinden kurulacak, clip-space
  z'den değil (spec §12.7).
- **DOĞRULAMA:** Kâğıt kontrolü — `s=0 → 0.5 m`, `s=1 → 1000 m`, dilim oranı 1,128.

### Görev 3 — ESM geçişi
- **EYLEM:** URP ana ışık shadowmap'ini 4× downsample edip `exp(z·80)` uzayına çevir,
  ayrılabilir 11 piksellik box filtre uygula. Hedef `R32F`.
- **AYNALA:** spec `s.64` kodu birebir.
- **TUZAK:** Ters Z; `EXPONENT` taşma sınırı hesaplandı (yukarıda).
- **DOĞRULAMA:** ESM dokusunu F1'den ekrana bas; gölge sınırları yumuşak ve titremesiz.

### Görev 4 — Yoğunluk + hacim aydınlatması kernel'i
- **EYLEM:** Her froxel için: yoğunluk (Görev 1'in modeli) → alpha; in-scattering → RGB.
  Bileşenler: ana ışık × ESM × **bulut gölgesi** + ambient probe SH × HG zonal SH.
- **AYNALA:** spec §5.2; ambient için §6.1'deki `float4(1, dir.y, dir.z, dir.x) * float4(1,g,g,g)`.
- **TUZAK:** Ambient probe gökyüzünden pişiyor; **atmosferin in-scattering'ini TEKRAR
  eklemek çift sayımdır** (karar 2). Ambient yalnız sisin gölgeli tarafını doldurur.
- **DOĞRULAMA:** Tek froxel dilimini ekrana bas; yoğunluk profili irtifayla üstel düşmeli.

### Görev 5 — Işın yürüyüşü kernel'i
- **EYLEM:** 2B compute, Z boyunca yürü, `AccumulateScattering` + `WriteOutput`.
- **AYNALA:** spec §5.3.2 ve §5.3.3 kodu birebir.
- **DOĞRULAMA:** Son dilimin alpha'sı `exp(-Σβ)` olmalı; sabit yoğunlukta kâğıt hesabıyla karşılaştır.

### Görev 6 — Uygulama ve analitik kuyruk
- **EYLEM:** `ApplyHeightFog` imzası KORUNUR. İçi: hacimden `SAMPLE_TEXTURE3D_LOD`,
  yüzey `far`'ın ötesindeyse kuyruk integrali eklenir, kompozisyon yukarıdaki formülle.
- **TUZAK:** Çağıranlar değişmiyor — `MountainSurface`, `Precipitation`, `LightningBolt`
  aynı fonksiyonu çağırmaya devam ediyor.
- **DOĞRULAMA:** Hacim menzili 0'a indirilince görüntü ESKİ hâline dönmeli (kuyruk tek başına).

### Görev 7 — `_HeightFogChroma` sökümü
- **EYLEM:** Globali, `AtmosphereController` yazımını ve `AtmosphereSettings` alanlarını sil.
- **NEDEN:** Karar 2 — Rayleigh'in sahibi gökyüzü paketi; chroma çift sayım.
- **DOĞRULAMA:** Öğlen ufuk rengi kaymamalı; kayarsa paket hava perspektifi zaten o işi
  yapmıyor demektir, karar 2 yeniden açılır.

### Görev 8 — Feature kurulumu ve bootstrap
- **EYLEM:** `VolumetricFogFeature`'ı `PC_Renderer`'a ekle (bulut ve gökyüzünden SONRA).
  Ayar asset'ini üret ve bağla.
- **AYNALA:** `MountainSceneBootstrap.EnsureSkyFeature` deseni (`SerializedObject` ile
  shader bağlama, `Create()` ELLE ÇAĞRILMAZ).
- **TUZAK:** `Create()`'i elle çağırmak `VolumeManager` yığını hazır değilken NRE veriyor
  (bu tuzağa iki kez düşüldü, `DECISIONS.md`'de kayıtlı).

### Görev 9 — Gece seviyesini yeniden ölç
- **EYLEM:** Sis katkısı değiştiği için `MoonIntensity` (şu an 0.0199) ve gece profilinin
  `contrast`/`exposure` değerleri yeniden ölçülür.
- **NEDEN:** `DECISIONS.md` → "Gece seviyesi: ayı BULUT belirledi" kaydının tetikleyicisi
  tam olarak bu.
- **DOĞRULAMA:** 23:00'te bulut, kar ve gökyüzü eşiğin üstünde; `exposureCap` kırpması
  kontrol edilir (şu an 2,54 isterken 2,5'te kırpıyor).

### Görev 10 — Belgeler
- **EYLEM:** `SYSTEMS.md` sis bölümü yeniden yazılır (yeni bağlar: ESM, bulut gölgesi,
  ambient probe, hacim↔kuyruk sınırı). `DECISIONS.md`'ye dört kilitlenen karar; kapanan
  iki bekleyen ölçüm ("hava perspektifi + yükseklik sisi birlikte", "gece seviyesi")
  silinir. `SCALE.md`'ye hacim menzili ve dilim sayısı "bilerek mutlak" satırı.
- **NEDEN:** CLAUDE.md: aynı adımda güncellenir, sonraya bırakılmaz.

---

## Doğrulama

**Projede birim test yok** (test framework kurulu, `Assets/Tests` yok). Doğrulama kültürü
ölçüm tabanlı; plan buna uyar.

### Derleme
Unity derlemesi temiz; Console'da shader hatası yok.

### Ölçüm tabanlı doğrulama (F1 paneli)
| Ne | Nasıl | Beklenen |
|---|---|---|
| Saf taşıma (Görev 1) | Öncesi/sonrası aynı saat, aynı bakış | Görüntü fark yok |
| Hacim↔kuyruk sınırı | Hacim menzilini 1000 → 200 m | Sınırda kenar/atlama YOK |
| Hacim kapalı | Menzil 0 | Eski analitik görüntü |
| God ray | 08:00, bulut kapsaması %60 | Bulut altından huzme |
| Çift sayım | Öğlen ufuk rengi, chroma öncesi/sonrası | Kayma yok |
| Kare süresi | Performans göstergesi | Hedef < 1,5 ms (spec XboxOne'da 1,1 ms) |

### Elle doğrulama listesi
- [ ] 06:00 şafak — sis kızıllığı gökyüzüyle çelişmiyor
- [ ] 12:00 — vadi sis denizi yerinde, inversiyon kesimi keskin
- [ ] 23:00 — gece seviyesi Görev 9'da yeniden ölçüldü
- [ ] Zirveden bakış — hacmin ötesinde kuyruk sürüyor, ufukta sınır yok
- [ ] Fırtına — yağış şiddeti sis yoğunluğunu artırıyor

---

## Riskler

| Risk | Olasılık | Etki | Azaltma |
|---|---|---|---|
| Compute shader projede ilk; taklit edilecek desen yok | Kesin | Orta | RenderGraph deseni bulut feature'ından; `AddUnsafePass` içinde `DispatchCompute` |
| `R16G16B16A16_SFloat` random write platform desteği | Düşük | Yüksek | `SystemInfo.IsFormatSupported(..., LoadStore)` kontrolü, Görev 8'de |
| Frustum hizalı hacim + TAA jitter etkileşimi | Orta | Orta | Spec zaten kasıtlı jitter öneriyor (§6.2); TAA jitter'ı froxel hizasına dahil edilecek |
| Hacim menzili 1000 m spec'in doğruladığı 128 m'nin 8 katı | Orta | Orta | Üstel dağılım yakın alanı koruyor (46 dilim ilk 128 m'de); sınır Görev 6'da ölçülüyor |
| Gece seviyesinin yeniden kayması | Yüksek | Düşük | Görev 9 zaten planın adımı |
| `_HeightFogChroma` sökümü ufuk rengini bozar | Düşük | Orta | Görev 7 doğrulaması; bozulursa karar 2 yeniden açılır |

## Notlar

- Spec'in §10 "Açık noktalar" bölümündeki her sayı bu planda kâğıtta hesaplandı; hiçbiri
  tahminle bırakılmadı.
- Spec `[NOT: makalede yok]` etiketiyle kendi eklemelerini işaretliyor. Bu plandaki
  kararların dördü de o etiketli bölgeden geliyor, yani makaleye aykırı değil —
  makalenin sessiz kaldığı yer.
- Sıra önemli: Görev 1 saf taşıma olduğu için tek başına doğrulanabilir. Bozulma olursa
  hacim işine geçmeden yakalanır.
