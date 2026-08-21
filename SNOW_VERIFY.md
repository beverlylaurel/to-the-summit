# Kar sistemi v2 — doğrulama listesi

`unity-kar-sistemi-spec-v2.md`'nin on fazı yazıldı. **Hiçbiri Unity'de çalıştırılarak
doğrulanmadı** — yalnız derleme temiz (C#, compute, shader; 0 hata, 0 uyarı).

Bu dosya işi bitince silinir.

---

## Önce: tek düğme

1. Unity menü çubuğu → `To The Summit` → `Kar` → `Kar Teşhisi`
2. Pencerede en alta in, `Kurulum` → **`Sahneyi kur`**
3. Konsolda hata olmamalı. Kurulan bileşenler:
   - **Kar Sistemi** nesnesi: `SnowManager`, `SnowOcclusionCapture`, `SnowGroundHeight`,
     `SnowWeather`, `SnowCoverageDriver`, `SnowDeformerRegistry`, `SnowClipmap`,
     `SnowFarCascade`, `SnowPersistence`, `SnowProfiler`, `SnowSampler`,
     `SnowfallController`, `SnowAtmosphereDriver`
   - **Oyuncu**: `SnowFootstepDriver`, `SnowMovementModifier`, `SnowFootstepAudio`, `AudioSource`
4. **Play'den çık, Ctrl+S.** Sahne kaydedilmezse bunların hepsi Unity kapanınca gider.

---

## Faz 4 — zemin mesh'i

**Nereye bak:** Kar Teşhisi → `Kar yüzeyi (clipmap)`

- [ ] Halka sayısı **4**, üçgen sayısı **422 400**, kutu yeşil
- [ ] Oyunda kar araziye oturuyor, havada durmuyor
- [ ] Yürürken yüzey **dalgalanmıyor**
- [ ] Halkalar arasında çatlak yok (Wireframe'de bakılabilir)
- [ ] Frame Debugger'da kar için **4 çizim çağrısı**

Bozuksa ilk şüpheli: `SnowClipmap.snapStep`. Bütün halkalar tek adıma snap'leniyor;
ayrı adım çatlak açar.

## Faz 5 — deformasyon

**Nereye bak:** Kar Teşhisi → `Deformasyon`

- [ ] Yürüyünce iz kalıyor ve iz **bot şekilli** (yuvarlak leke değil)
- [ ] İzin kenarında kar sırtı var, hareket yönünde asimetrik
- [ ] Aynı hattan 5–6 kez geçince iz derinliği belirgin azalıyor (patika)
- [ ] **Zıplayarak ilerlerken iz kalmıyor**
- [ ] **Kütle testi:** `Yağış preseti` → `Clear`, sıcaklık **−6**, sonra
      `Kütle testini sıfırla`. Yürürken sapma **%0.5 içinde** kalmalı.

Kütle testi tutmuyorsa: `KRelax` gather formülasyonunda mı, `KRingSum` koşuyor mu.

## Faz 6 — gölgelendirme

- [ ] Kar beyaz ama patlamıyor, gölgesi mavimsi
- [ ] Güneşte parıltı var, kamera hareket ederken **titremiyor**
- [ ] Uzaklaştıkça parıltı yoğunluğu sabit
- [ ] Sıkışmış iz ile taze kar arasında albedo/pürüzlülük farkı görünüyor

Parıltı titriyorsa: `_SparkleCellSize` ve LOD uyarlaması (§8.4).

## Faz 7 — nesne üstü kar

Bu faz **elle malzeme atamak** istiyor: bir kayaya/çatıya
`To The Summit/Snow Cover Object` materyali ver.

- [ ] Üstünde kar var, altında yok
- [ ] Kenarlar düz çizgi değil
- [ ] `Yağış preseti`ni yükseltince kaplama kademeli artıyor

## Faz 8 — kar yağışı

**Nereye bak:** Kar Teşhisi → `Kar yağışı`

- [ ] `Heavy` preseti → taneler düşüyor, rüzgârda sürükleniyor
- [ ] Test küpünün altında kar yağmıyor
- [ ] Uzaktaki taneler kaybolmuyor
- [ ] `Blizzard` → `Etkin savrulma` sıfırdan büyük, yerde savrulma perdesi var
- [ ] Preset geçişi 45 saniyede yumuşak (hız kolu 1 iken)

**Bilinen sapma:** VFX Graph yerine compute parçacığı. Gerekçe `DECISIONS.md`'de.

## Faz 9 — oyun tarafı

**Nereye bak:** Kar Teşhisi → `Oyun tarafı`

- [ ] `Ayak altı derinlik` ve `Yoğunluk` doluyor
- [ ] Derin karda `Hız çarpanı` 1'in altına iniyor, patikada 1'e dönüyor
- [ ] Ayak kalkışında toz bulutu çıkıyor (materyal atanmışsa)
- [ ] `Son ayak sesi` derinliğe göre değişiyor

**Ses dosyası yok** — `SnowFootstepAudio` bileşenindeki beş klip dizisi boş.
Seçim mantığı çalışıyor, çalacak ses yok.

## Faz 10 — kalıcılık, kaskad, profil

**Nereye bak:** Kar Teşhisi → `Kar yağışı` ve `Profil`

- [ ] `Uzak kaskad` alanı 192 m / 1024 görünüyor
- [ ] `saklanan blok` sayısı yürüdükçe artıyor
- [ ] 100 m uzaklaşıp geri gelince bıraktığın iz kabaca yerinde
- [ ] `Profil` tablosunda toplam süre hedefin altında (High'ta 2.60 ms)

---

## Spec'ten sapmalar — hepsi kodda `ASSUMPTION` olarak yazılı

| Ne | Neden | Nerede yazılı |
|---|---|---|
| Durum dokusu `ARGBFloat`, half değil | Half'te birikme matematiksel olarak imkânsız; ölçüldü | `RATIONALE.md` |
| Snap adımı tam sayı teksele yuvarlandı | 0.25 m tam teksel etmiyor; kesirli snap titreme üretir | `RATIONALE.md` |
| Zemin dokusu `R16`, half değil | Half 6189 m menzilde 3 m hata ekler | `RATIONALE.md` |
| Engel kamerasına ayrı renderer | Ana renderer'daki gökyüzü/bulut geçişleri çöküyor | `RATIONALE.md` |
| `SetReplacementShader` yerine override materyal | O API SRP'de yok | `RATIONALE.md` |
| Clipmap tek snap adımı | Halka başına snap çatlak açıyor | `RATIONALE.md` |
| VFX Graph yerine compute parçacığı | Paket kurulu değil, `.vfx` metin olarak üretilemez | `DECISIONS.md` |
| Atmosfer sürücüsü kapalı kuruluyor | Projenin kendi atmosferi var, iki kaynak olamaz | `DECISIONS.md` |
| Lens karı renderer'a eklenmedi | §10.2 opsiyonel diyor | `DECISIONS.md` |
| Kaskad 6 m bloklara snap | 4 m tam teksel etmiyor | `RATIONALE.md` |
| Kalıcılık gezinerek yakalıyor | Geri okuma asenkron, blok çıkarken yakalanamaz | `RATIONALE.md` |
| `SnowBearing` silindi | Spec tanımlıyor, hiçbir yer çağırmıyor | `SnowCommon.hlsl` |
| `RT_Pending` yaratılmadı | §2.2 sayıyor ama son tasarımda hiçbir kernel kullanmıyor | burada |

## Eksik kalanlar

- **Ses dosyaları** — kullanıcıdan gelecek (§14).
- **Toz bulutu materyali** — `SnowFootstepDriver.puffMaterial` boş; atanana kadar
  toz çıkmıyor.
- **Nesne kar materyali** — hangi nesnelere verileceği tasarım kararı.
- **Eski yağmur sistemi** (`PrecipitationRenderer`) hâlâ çalışıyor. İki yağış sistemi
  aynı anda açık; biri kapatılmalı.
