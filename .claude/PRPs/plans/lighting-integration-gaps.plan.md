# Plan: Işık/sis entegrasyon boşlukları ve belge ayrışmaları

## Summary

`LIGHTING_REVIEW.md`'nin on bulgusunun **onu da kodda doğrulandı**. Mimari sağlam;
sorunların tamamı iki ailede: (a) sis ve bulut gölgesi zincirinin bazı yüzeylere hiç
ulaşmaması, (b) kod değişip yorum/belge/varsayılanın geride kalması. Bu plan ikisini
ayrı commit ailelerinde kapatıyor.

## User Story

Oyuncu olarak, fırtınada dağ sise gömülürken denizin de gömülmesini ve bulut gölgesi
geçerken bisikletin de kararmasını istiyorum; böylece "gökyüzü kapalı ama nesneler
güneşli" çelişkisi kalmasın.

## Problem → Solution

Deniz, karlı nesneler ve kar tanecikleri **ölü** bir sis çağrısı (`MixFog`, sahnede
`m_Fog: 0`) kullanıyor ve bulut gölgesi cookie'sini üç yüzey hiç uygulamıyor
→ hepsi projenin tek sis fonksiyonuna (`ApplyHeightFog`) ve tek cookie çarpanına bağlanır.

## Metadata

- **Complexity**: Medium (7 dosya kod + 4 dosya belge, ~120 satır)
- **Source**: `C:\Users\musta\Desktop\LIGHTING_REVIEW.md` (analiz belgesi, PRD değil)
- **Estimated Files**: 11

---

## Doğrulama sonucu — MD'nin iddiaları

Her iddia grep ile kodda kontrol edildi. **Onu da doğru.**

| # | İddia | Doğrulama kanıtı | Verdi |
|---|---|---|---|
| 1 | Deniz yerel sise girmiyor | `SeaLit.shader:42` `multi_compile_fog`, `:490` `MixFog`; `Game.unity:17` `m_Fog: 0`; `RenderSettings.fog` yazan tek C# satırı yok; `SeaLit.shader:22` `Queue = Transparent-1` | ✅ |
| 2 | Cookie'yi çoğu yüzey almıyor | Uygulayan: `MountainSurface.shader:57` pragma + `:258-259` elle çarpım, `SnowfallParticle.shader:47` pragma. `SeaLit`, `SnowCoverObject`, `BikeSurface` grep'te hiç geçmiyor | ✅ |
| 3 | `SnowCoverObject`/`SnowfallParticle` de `MixFog` | `SnowCoverObject.shader:61,218`; `SnowfallParticle.shader:44,187` | ✅ |
| 4 | Alpenglow havaya bağlı değil | `TerrainSurface.cs:261` `DawnStrengthId = horizon * alive * settings.alpenglowStrength` — yağış/kapsama terimi yok | ✅ |
| 5 | Deniz gölge zayıflatması almıyor | `SeaLit.shader:206` `GetMainLight()` argümansız → `shadowAttenuation = 1` | ✅ |
| 6 | Ay tooltip'i bayat | `TimeOfDay.cs:45-46` "It CASTS NO SHADOW"; `MountainSceneBootstrap.cs:1165` `moon.shadows = LightShadows.Soft` | ✅ |
| 7 | Varsayılanlar ayrışmış | Alan `moonIntensity = 0.204f` (`TimeOfDay.cs:43`) vs bootstrap `MoonIntensity = 0.0199f` (`MountainSceneBootstrap.cs:401`) → **10.25×**; `moonColor` de ayrı | ✅ |
| 8 | `Sky.mat` fallback yarım-ölü | `Sky.shader:36-37,172,182,197` `_StarStrength`/`_MoonDirection` okuyor; C#'ta yazan yok (grep: yalnız `_SunColor`/`_MoonColor` yazılıyor, `AtmosphereController.cs:57-58`) | ✅ |
| 9 | Hüzme invariantı tutmuyor | `TimeOfDay.cs:311-312` renk `× sunFade`, `:343` şiddet `× sunFade` → çarpımda `sunFade²`; `:340-342` yorumu "eşit kalır" diyor | ✅ |
| 10 | Gölge mesafesi drift | `PC_RPAsset.asset:57` `m_ShadowDistance: 150`, `AtmosphereSettings.asset:66` `maxShadowDistance: 150`; `MountainSurface.shader:38` "fifty metres"; `SYSTEMS.md:658` "60 m" | ✅ |

### MD'nin eksik/yanıltıcı kaldığı üç yer

1. **#2 — `SnowCoverObject` için pragma TEK BAŞINA YETMEZ.** MD "standard
   `UniversalFragmentPBR` yolunu kullanan shader'larda pragma tek başına yeter" diyor.
   `SnowCoverObject.shader:198` `SnowDirectLight(mainLight, ...)` ile **elle yazılmış**
   kar ışıklandırmasını kullanıyor, `UniversalFragmentPBR`'ı değil. Bu yüzden
   `MountainSurface` gibi **elle çarpım** gerekiyor.
2. **#2 — `BikeSurface` için pragma YETER.** MD "önce kontrol edilmeli" diyor; kontrol
   edildi: `BikeSurface.shader:289` `UniversalFragmentPBR(lighting, surface)` kullanıyor,
   yani keyword açılınca URP cookie'yi kendi uygular.
3. **#5 — `SnowCoverObject` gölge zayıflatmasını ZATEN alıyor.** `:170`
   `GetMainLight(IN.shadowCoord)`. Eksik olan yalnız cookie. Deniz ikisini de almıyor.

---

## UX Design

**Internal change — no user-facing UI.** Görsel sonuç:

### Before
```
fırtına, görüş 140 m
  dağ      : 300 m'de sise gömülü
  DENİZ    : ufka kadar keskin, doygun turkuaz     <-- çelişki
  bisiklet : sise gömülü (ApplyHeightFog var)
  karlı kaya: keskin                               <-- çelişki

bulut gölgesi geçerken
  zemin    : kararıyor
  DENİZ    : tam güneşle parlıyor                  <-- çelişki
  bisiklet : tam güneşle parlıyor                  <-- çelişki
```

### After
```
fırtına, görüş 140 m
  dağ / deniz / bisiklet / karlı kaya : hepsi aynı havada, her katman KENDİ mesafesiyle

bulut gölgesi geçerken
  zemin / deniz / bisiklet / karlı kaya : hepsi aynı cookie ile kararıyor
```

---

## Mandatory Reading

| Öncelik | Dosya | Satır | Neden |
|---|---|---|---|
| P0 | `Assets/Shaders/BikeSurface.shader` | 78-85, 285-292 | **Referans uygulama.** Aynı hata bisiklette bir kez yapıldı ve düzeltildi; yorumu da orada |
| P0 | `Assets/Shaders/HeightFog.hlsl` | 495-520 | `ApplyHeightFog` imzası ve "her yüzey aynı havada" kuralı |
| P0 | `Assets/Shaders/MountainSurface.shader` | 55-60, 255-262 | Cookie'nin elle çarpıldığı yer — kar/deniz için birebir kopyalanacak desen |
| P1 | `Assets/Sea/Shaders/SeaLit.shader` | 40-58, 200-215, 480-495 | Değişecek üç yer: pragma, `GetMainLight`, `MixFog` |
| P1 | `Assets/Snow/Shaders/SnowCoverObject.shader` | 55-65, 165-200, 215-220 | Elle yazılmış kar ışığı yolu |
| P1 | `Assets/Scripts/Terrain/TerrainSurface.cs` | 233-263 | `ApplyAlpenglow`, #4 için |
| P2 | `Assets/Scripts/Environment/TimeOfDay.cs` | 20-50, 300-350 | #6, #7, #9 |
| P2 | `Assets/Shaders/Sky.shader` | 30-45, 165-200 | #8 fallback yolu |

## External Documentation

Gerek yok — hepsi proje içi yerleşik desenler. `SampleMainLightCookie` URP'nin kendi
`Lighting.hlsl`'inden geliyor ve `MountainSurface` zaten kullanıyor.

---

## Patterns to Mirror

### YEREL SİS UYGULAMASI
```hlsl
// SOURCE: Assets/Shaders/BikeSurface.shader:82-85, 290
// THE SAME AIR. The bike used to call Unity's own fog (`ComputeFogFactor` /
// `MixFog`) — with `m_Fog: 0` in the scene that call was DEAD and the bike took
// no fog at all: in a storm the mountain went white while the bike stayed sharp.
// Unity's fog is height independent anyway, which is why the project never uses it.
#include "HeightFog.hlsl"
...
color.rgb = ApplyHeightFog(color.rgb, GetCameraPositionWS(), input.positionWS);
```

### BULUT GÖLGESİ COOKIE'SİNİN ELLE ÇARPILMASI
```hlsl
// SOURCE: Assets/Shaders/MountainSurface.shader:57, 258-259
#pragma multi_compile_fragment _ _LIGHT_COOKIES
...
#ifdef _LIGHT_COOKIES
    mainLight.color *= SampleMainLightCookie(IN.positionWS);
#endif
```

### GÖLGE KOORDİNATIYLA ANA IŞIK
```hlsl
// SOURCE: Assets/Snow/Shaders/SnowCoverObject.shader:170
Light mainLight = GetMainLight(IN.shadowCoord);
```

### ATMOSFER DURUMUNU OKUYAN SÜRÜCÜ
```csharp
// SOURCE: Assets/Scripts/Terrain/TerrainSurface.cs:255-262
float horizon = time.HorizonFactor * time.HorizonFactor;
float alive = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.05f, time.SunHeight));
material.SetColor(DawnColorId, time.CurrentSunColor);
material.SetFloat(DawnStrengthId, horizon * alive * settings.alpenglowStrength);
```

---

## Files to Change

| Dosya | İşlem | Gerekçe |
|---|---|---|
| `Assets/Sea/Shaders/SeaLit.shader` | UPDATE | #1 sis, #2 cookie, #5 gölge |
| `Assets/Snow/Shaders/SnowCoverObject.shader` | UPDATE | #2 cookie (elle), #3 sis |
| `Assets/Snow/Shaders/SnowfallParticle.shader` | UPDATE | #3 sis |
| `Assets/Shaders/BikeSurface.shader` | UPDATE | #2 cookie (yalnız pragma) |
| `Assets/Scripts/Terrain/TerrainSurface.cs` | UPDATE | #4 alpenglow ↔ hava bağı |
| `Assets/Scripts/Environment/TimeOfDay.cs` | UPDATE | #6 tooltip, #7 varsayılan, #9 yorum |
| `Assets/Shaders/MountainSurface.shader` | UPDATE | #10 yorum (50 → 150 m) |
| `Assets/Shaders/Sky.shader` | UPDATE | #8 fallback sadeleştirme |
| `SYSTEMS.md` | UPDATE | #4 bağ, #10 gölge mesafesi |
| `RATIONALE.md` | UPDATE | #1-#5 gerekçeleri |
| `DECISIONS.md` | UPDATE | #8 kararı + tetikleyici |

## NOT Building

- **Arazi gölge haritasının denize uygulanması.** Derinlik okuma + koordinat dönüşümü
  gerektiriyor; kıyı şeridinde dağ gölgesi denize nadiren düşüyor. Cookie yeterli.
- **`SnowfallParticle` için tam `ApplyHeightFog`.** Önce transmittance-only ölçülecek;
  8 örnekli integral tanecik shader'ında pahalı olabilir. Ölçmeden ağır yol seçilmeyecek.
- **`sunFade`'in tek yere indirilmesi (#9).** Sayı değişikliği görsel sonucu değiştirir;
  yalnız yorum düzeltilecek.
- **PBSky paketine dokunmak.** Sis sahipliği bölümü bilinçli ve doğru.
- **Yeni bir sis/ışık kaynağı.** Her düzeltme MEVCUT tek kaynağa bağlanır.

---

## Step-by-Step Tasks

### Görev 0: Ölçüm aracı — F1 paneline "Deniz" bölümü

- **ACTION**: `DebugMenu`'ye deniz izolasyon anahtarları ekle. Projenin kuralı:
  şüphelilerin TAMAMI tek seferde eklenir.
- **IMPLEMENT**: `_SeaDbgNoFoam`, `_SeaDbgNoWaves`, `_SeaDbgNoShallow`,
  `_SeaDbgNoRefraction` (hepsi `SeaShaderIDs`'te zaten var, hiçbiri sürülmüyor) için
  checkbox + "Ayarları geri al" düğmesi. `Shader.SetGlobalFloat` ile doğrudan;
  bağımlılık gerekmiyor.
- **MIRROR**: `DebugMenu.DrawWind()` bölüm deseni (`BeginSection`/`EndSection`).
- **GOTCHA**: F1 panel kuralı — her yeni bölüm "Ayarları geri al" ile gelir, işi bitince
  bölüm silinir.
- **VALIDATE**: Play → F1 → Deniz bölümü görünüyor, köpük kapatılınca deniz köpüksüz.

### Görev 1: Deniz — sis + cookie + gölge (tek geçiş)

- **ACTION**: `SeaLit.shader`'da üç değişiklik.
- **IMPLEMENT**:
  1. `#pragma multi_compile_fog` **sil**, yerine `#pragma multi_compile_fragment _ _LIGHT_COOKIES` ekle.
  2. `#include "../../Shaders/HeightFog.hlsl"` ekle (SeaCommon'dan sonra).
  3. `:206` `Light mainLight = GetMainLight();` →
     ```hlsl
     float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
     Light mainLight = GetMainLight(shadowCoord);
     #ifdef _LIGHT_COOKIES
         mainLight.color *= SampleMainLightCookie(IN.positionWS);
     #endif
     ```
     Ayrıca `#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE`
     zaten `:39`'da var — kontrol et.
  4. `:490` `color = MixFog(color, IN.fogCoord);` →
     `color = ApplyHeightFog(color, _WorldSpaceCameraPos, IN.positionWS);`
  5. `Varyings.fogCoord` ve `ComputeFogFactor` çağrısı **ölü kod** olur → sil.
- **MIRROR**: BikeSurface sis deseni + MountainSurface cookie deseni.
- **GOTCHA**: `mainLight.color` düzeltmesi parıltı (`glitter`), su rengi (`waterLight`)
  ve köpük ışığının (`foamIrradiance`) **üçüne birden** girmeli — üçü de `mainLight.color`
  okuyor, tek yerden çarpmak yeterli. Ayrıca `HeightFog.hlsl` `_TerrainHeightMap`
  globalini okuyor; deniz mesh'i arazi dışında da uzanıyor, doku sınırında ne döndüğü
  kontrol edilmeli.
- **VALIDATE**: Fırtınada (F1 → yağış 1) deniz ufka kadar keskin **kalmamalı**.
  Aynı kadrajda değişiklik öncesi/sonrası aynı deniz pikselinin parlaklığı ölçülür.

### Görev 2: Karlı nesneler — cookie (elle) + sis

- **ACTION**: `SnowCoverObject.shader`.
- **IMPLEMENT**:
  1. `#pragma multi_compile_fog` sil, `#pragma multi_compile_fragment _ _LIGHT_COOKIES` ekle.
  2. `:170`'ten sonra `#ifdef _LIGHT_COOKIES mainLight.color *= SampleMainLightCookie(IN.positionWS); #endif`
  3. `:218` `MixFog` → `ApplyHeightFog(color, _WorldSpaceCameraPos, IN.positionWS)`;
     `HeightFog.hlsl` include et.
  4. `fogFactor` interpolant'ı ölü → sil.
- **MIRROR**: MountainSurface cookie bloğu (aynı elle çarpım — bu shader da
  `UniversalFragmentPBR` kullanmıyor).
- **GOTCHA**: **MD burada yanılıyor**: pragma tek başına yetmez, `SnowDirectLight`
  elle yazılmış yol. Gölge zayıflatması zaten var (`GetMainLight(IN.shadowCoord)`),
  tekrar ekleme.
- **VALIDATE**: Bulut gölgesi geçerken karlı kaya ile altındaki zemin **birlikte** kararmalı.

### Görev 3: Kar tanecikleri — sis

- **ACTION**: `SnowfallParticle.shader:187`.
- **IMPLEMENT**: Önce ölç: `ApplyHeightFog`'un tam integrali tanecik shader'ında kaç ms
  ekliyor. Ucuzsa tam yol; pahalıysa `FogPath`'in yalnız transmittance kısmı.
- **GOTCHA**: Tanecik sayısı 250 000; sekiz örnekli integral × tanecik başına ciddi
  olabilir. **Ölçmeden ağır yol seçilmeyecek.**
- **VALIDATE**: Kare süresi ölçümü (F1 performans göstergesi) öncesi/sonrası.

### Görev 4: Bisiklet — cookie (yalnız pragma)

- **ACTION**: `BikeSurface.shader`'a `#pragma multi_compile_fragment _ _LIGHT_COOKIES`.
- **GOTCHA**: `:289` `UniversalFragmentPBR` kullanıyor → URP cookie'yi kendi uygular,
  elle çarpım GEREKMEZ. (MD'nin "kontrol edilmeli" dediği yer; kontrol edildi.)
- **VALIDATE**: Bulut gölgesinde bisiklet ile zemin birlikte kararmalı.

### Görev 5: Alpenglow ↔ hava bağı — KARAR GEREKTİRİR

- **ACTION**: İki seçenekten biri, kullanıcı seçecek:
  - **(a) Bağ kur.** `TerrainSurface.ApplyAlpenglow` içine kapsama çarpanı:
    `strength *= lerp(1f, 0.25f, atmosphere.Coverage)`. Sıfırlanmaz — artçı faz
    gökyüzü ışığından geliyor, yalnız doğrudan faz kesilir.
    `TerrainSurface`'a `AtmosphereController` bağımlılığı eklenir (`[SerializeField]`,
    bootstrap'tan bağlanır).
  - **(b) Bilinçli kural.** `SYSTEMS.md`'ye "alpenglow bulut örtüsünü yok sayar,
    gerekçe: ..." satırı.
- **GOTCHA**: (a) yeni bir sistem bağı; `SYSTEMS.md` **aynı adımda** güncellenir.
  Sis paleti aynı durumda `duskOvercast` ile soluyor — iki türev çelişmemeli.
- **VALIDATE**: Şafak + fırtına: dağ yüzü kızıl yanmamalı.

### Görev 6: Ay tooltip'i (#6)

- **ACTION**: `TimeOfDay.cs:45-46` tooltip'ini güncelle.
- **IMPLEMENT**: "It CASTS NO SHADOW..." → ay artık gölge atıyor (`MarkAsSun` gece
  devrinde ay ana ışık oluyor, `MountainSceneBootstrap.cs:1165` Soft kuruyor); PBSky
  bağlantısı yeni metne taşınır.
- **VALIDATE**: Inspector'da tooltip okunur ve koda uyuyor.

### Görev 7: Varsayılan ayrışması (#7)

- **ACTION**: Tek kaynak seç — **ikisi birden değil**.
- **IMPLEMENT**: Önerilen: `TimeOfDay` alan varsayılanlarını bootstrap değerlerine
  eşitle (`moonIntensity = 0.0199f`, `moonColor = (0.586, 0.653, 0.818)`), ve
  `sunIntensity`'deki mevcut yorumun aynısını aya da yaz.
- **GOTCHA**: Sahne zaten doğru değeri taşıyor; bu değişiklik davranışı DEĞİŞTİRMEZ,
  yalnız bootstrap'sız açılan sahnede gece 10× parlak olmasını önler.
- **VALIDATE**: Boş sahneye `TimeOfDay` eklendiğinde gece parlaklığı sahnedekiyle aynı.

### Görev 8: `Sky.mat` fallback (#8) — KARAR GEREKTİRİR

- **ACTION**: İki seçenekten biri:
  - **(a) Besle.** `SkyWeatherDriver`'a `_StarStrength` ve `_MoonDirection` yayını ekle,
    veil yazılarını oraya taşı.
  - **(b) Kes.** `Sky.shader`'ı "hava rengi + güneş diski" fallback'ine indir; yıldız ve
    ay kodunu sil, `AtmosphereController:451-452` veil yazılarını da sil.
- **GOTCHA**: Fallback yalnız LUT hazır olmayan ilk karelerde devreye giriyor.
  (b) daha az kod, "çöp kod yok" kuralına daha uygun. `DECISIONS.md`'ye tetikleyiciyle yaz.
- **VALIDATE**: Fallback'e zorlanınca (LUT'u geçici boşalt) gök makul görünmeli.

### Görev 9: Hüzme invariantı yorumu (#9)

- **ACTION**: `TimeOfDay.cs:340-342` yorumunu düzelt.
- **IMPLEMENT**: "That way `CurrentSunColor x intensity` stays equal to the real beam"
  → "alçak güneşte hüzme BİLİNÇLİ olarak iki kez söndürülüyor: bir kez renkte
  (`:311`), bir kez şiddette (`:343`). Çarpım `beam · sunFade²`'dir, ham hüzme değil.
  Bulut tarafındaki kare de aynı ailenin görünüm kararı (`AtmosphereController:499-501`)."
- **GOTCHA**: **Sayı değiştirilmeyecek** — görsel sonucu değiştirir.
- **VALIDATE**: Yorum matematikle uyuşuyor.

### Görev 10: Gölge mesafesi drift (#10)

- **ACTION**: Üç yeri gerçeğe (150 m) eşitle.
- **IMPLEMENT**: `SYSTEMS.md:658` "60 m" → "150 m";
  `MountainSurface.shader:38` "it ends at fifty metres" → "150 metres".
  `SCALE.md`'ye bak: gölge mesafesi dağın boyuna bağlı mı? Bağlı değilse
  "bilerek mutlak" kaydı gerekmez, ama `AtmosphereSettings.maxShadowDistance`'ın
  görüşle ilişkisi (`ApplyShadowDistance`) not düşülmeli.
- **VALIDATE**: `grep -rn "50 m\|60 m" ` ile başka drift kalmadığı kontrol edilir.

---

## Testing Strategy

Projede test paketi yok. Doğrulama = **derleme kontrolü + ölçüm + kullanıcının Play'de bakması**.

| Kontrol | Nasıl | Beklenen |
|---|---|---|
| Shader derlemesi | Unity MCP `ShaderUtil.ShaderHasError` tüm shader'larda | 0 hata |
| C# derlemesi | Unity konsolu | 0 hata |
| Sabit eşliği | `To The Summit/Sea/Test Constant Parity` | 20/20 |
| Dalga alanı | `To The Summit/Sea/Test Wave Field` | RESULT: passed |
| Sis etkisi (deniz) | Aynı kadrajda color probe, öncesi/sonrası | Fırtınada deniz pikselinin parlaklığı sise doğru kayar |
| Cookie etkisi | Bulut gölgesi altında zemin/deniz/bisiklet/kaya parlaklık farkı | Fark kapanır |
| Tanecik maliyeti | F1 performans göstergesi, kare süresi | Kabul edilebilir artış (eşik önceden belirlenir) |

### Edge Cases
- [ ] Deniz mesh'i arazi heightmap'inin DIŞINDA — `ApplyHeightFog`'un `_TerrainHeightMap` okuması ne döner?
- [ ] Gece (güneş ufkun altında) — cookie dokusu ne içeriyor, ay ana ışıkken cookie geçerli mi?
- [ ] `FogEnabled` kapalıyken (F1 teşhis) deniz de kapanmalı
- [ ] Kar sistemi sahnede yokken `SnowCoverObject` sis yolu
- [ ] Bulut sistemi kapalıyken `_LIGHT_COOKIES` keyword'ü kapalı → `#ifdef` bloğu hiç derlenmez

---

## Validation Commands

### Derleme
```bash
cd /d "D:\ME\game\to the summit" && date > Logs/refresh.trigger
```
Ardından Unity konsolu okunur (MCP `GetConsoleLogs`). EXPECT: 0 hata.

### Shader taraması
Unity MCP RunCommand ile tüm `t:Shader` üzerinde `ShaderUtil.ShaderHasError`.
EXPECT: `withError=0`.

### Deniz sayısal testi
```
Menü: To The Summit/Sea/Test Wave Field
Menü: To The Summit/Sea/Test Constant Parity
```
EXPECT: `RESULT: passed`, `20/20 pairs identical`.

### Manuel doğrulama (kullanıcı)
- [ ] Play → batı kıyısı → F1 → yağış 1.0: deniz ufka kadar keskin **değil**
- [ ] Bulut gölgesi geçerken: zemin, deniz, bisiklet, karlı kaya **birlikte** kararıyor
- [ ] Şafak + fırtına: dağ yüzü kızıl yanmıyor (Görev 5a seçilirse)
- [ ] Gece: ay gölgesi düşüyor

---

## Acceptance Criteria

- [ ] `MixFog` çağrısı `Assets` altında sıfır (`grep -rn "MixFog" Assets` boş)
- [ ] `multi_compile_fog` pragması sıfır
- [ ] Ana ışığı okuyan her opak/yarı saydam yüzey shader'ında `_LIGHT_COOKIES` var
- [ ] `SeaLit` `GetMainLight(shadowCoord)` kullanıyor
- [ ] Tüm shader'lar hatasız derleniyor, C# 0 hata
- [ ] `SYSTEMS.md` yeni bağları taşıyor, `RATIONALE.md` gerekçeleri taşıyor
- [ ] Ölü kod bırakılmadı (`fogCoord`/`fogFactor` interpolant'ları silindi)

## Completion Checklist

- [ ] Belgeler **aynı adımda** güncellendi (`SYSTEMS.md`, `RATIONALE.md`, `DECISIONS.md`)
- [ ] Ölçüm sonucu gelmeden bir sonraki düzeltme yazılmadı
- [ ] Karar gerektiren iki madde (#4, #8) kullanıcıya soruldu
- [ ] İki commit ailesi ayrı: davranış değiştirenler / yalnız belge
- [ ] `SCALE.md` kontrol edildi (gölge mesafesi ölçek bağımlısı mı)

## Risks

| Risk | Olasılık | Etki | Azaltma |
|---|---|---|---|
| `ApplyHeightFog` deniz mesh'inin arazi dışındaki kısmında bozuk değer döndürür | Orta | Yüksek — ufukta artefakt | Önce `_TerrainHeightMap` sınır davranışı ölçülür; gerekirse clamp |
| Tanecik shader'ında sis maliyeti kabul edilemez | Orta | Orta | Görev 3'te önce ölç, transmittance-only alternatifi hazır |
| Cookie iki kez uygulanır (`UniversalFragmentPBR` + elle) | Düşük | Orta | Her shader'ın hangi yolu kullandığı planda yazılı: elle = MountainSurface/SnowCoverObject/SeaLit, otomatik = BikeSurface |
| Alpenglow bağı sis paletiyle çelişir | Düşük | Düşük | Aynı kaynaktan (`AtmosphereController.Coverage`) beslenir |
| `Sky.shader` kesilirse fallback gerçekten gerektiğinde yetersiz kalır | Düşük | Düşük | `DECISIONS.md`'ye tetikleyici yazılır |

## Notes

- **Sıra MD'nin önerdiği gibi**: 1+2+5 birlikte (deniz tek geçiş) → 3 → 4 → 6-10.
  Gerekçe: en yüksek görsel getiri en düşük riskle.
- **Bisiklet zaten bir kez bu hatayı yaşadı ve düzeltildi** (`BikeSurface.shader:82`
  yorumu). Aynı hata dört yüzeyde daha duruyor; referans uygulama elimizde.
- `MixFog`'un ölü olması **derleme hatası vermiyor** — bu yüzden dört yüzeyde birden
  fark edilmeden kaldı. Kabul kriterine "`MixFog` sıfır" konmasının sebebi bu.
- Görev 0 (F1 deniz bölümü) projenin "izolasyon anahtarları" kuralının gereği; deniz
  teşhis globalleri (`_SeaDbgNo*`) `SeaShaderIDs`'te tanımlı ama **hiçbiri sürülmüyor**
  — yani şu an ölü kod. Bu görev onları da canlandırıyor.
