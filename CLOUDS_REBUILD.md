# Bulut sistemi — yeniden yazım envanteri

Eski sistem silinmeden önce yazıldı. Amaç tek şey: **hangi bağ vardı, yenisinde ne
yeniden kurulacak.** Kod değil, sözleşme listesi.

> **Belgeler arası iş bölümü.** Bulut konusunda **geçerli olan tek belge budur.**
> `SYSTEMS.md`'nin bulut kısmı silinmiş koda ait, oraya bakılmaz (başında uyarısı var).
> `DECISIONS.md` yalnız kararın kendisini ve tetikleyicisini tutuyor.
> `NUBIS_NOTES.md` makale okumalarını tutuyor — soru-cevap, her cevabın yanında kaynak
> sayfa. Buradaki dersler ÖLÇÜMDEN, oradaki cevaplar MAKALEDEN gelir; ikisi karışmaz.
> Yeni sistem çalışır hâle gelince bu dosya `SYSTEMS.md`'ye taşınır ve buradaki bağlar
> orada güncellenir; iki yerde birden bulut anlatımı durmaz.

---

## v1 KURALI: HİÇBİR BAĞ YOK

**İlk sürüm bu belgedeki hiçbir bağı kurmaz.** Ne aşağıdaki girdileri okur, ne aşağıdaki
tüketicilere veri verir. Tamamen kendi başına, kendi ayarlarıyla çalışır.

Bağlar ancak v1 **görsel olarak onaylandıktan sonra**, **teker teker** eklenir. Her bağdan
sonra buluta tekrar bakılır; görüntü bozulursa o bağ geri alınır ve sebebi bulunmadan
bir sonrakine geçilmez.

Gerekçe ölçülmüş: 2026-08-14'te bulut sistemi aynı anda hava durumuna, rüzgâra, saate,
yağışa ve şimşeğe bağlıydı. Her belirtide hangi kaynağın suçlu olduğu ayırt edilemedi ve
her tur bir telafi terimi eklendi — on bir tanesi birikti, sonunda sistem silindi.

Aşağıdaki iki liste **v2 ve sonrası** içindir.

---

## Bulut sisteminin OKUDUKLARI (girdi)

| kaynak | ne veriyor | nereye gidiyor |
|---|---|---|
| `WeatherState` | yağış şiddeti, karlılık | kapsama hedefi, yoğunluk, soğurma |
| `WindField` | şiddet + yön | sürüklenme hızı, makaslama yönü |
| `TimeOfDay` | güneş yönü/rengi, ay, gündüz katsayısı | aydınlatma, batım tonu, gece rengi |
| `AtmosphereSettings` | bütün ayarlar | — |
| Hava haritası (pişmiş) | R kapsama, G tip, B taban kayması | yerleşim |
| Taban/detay/curl gürültüsü | şekil, aşındırma, türbülans | — |

**Kural:** bulut kendi zamanlayıcısını/rastgeleliğini kurmaz; hepsi yukarıdakilere bağlanır.

---

## Bulut sistemini OKUYANLAR (çıktı) — yenisinde geri bağlanacak

### 1. Yer bulut gölgesi
`HeightFog.hlsl` içindeki `CloudShadowAt`, yüzey shader'larından çağrılıyor.
**Sözleşme:** gökyüzü hangi yoğunluk alanından besleniyorsa yer gölgesi de aynısından
beslenmek zorunda. İkinci bir yaklaşım kurulursa gökte bulut olmayan yerde gölge çıkıyor.
Işın yer noktasından güneşe doğru kaydırılıp (`slide`) harita okunuyor.

### 2. `CloudCeiling` → `AltitudeWeatherDriver`
Bulut tepesinin üstünde yağış yoktur. Gerçek yüksekliği yalnız bulut sistemi bilir;
sürücüye **itiliyor** (sürücü çekmiyor — iki sistem birbirine referansla bağlanmasın diye).
Nominal tavan kullanılırsa kural hiç işlemiyor.

### 3. Katman kotları → `ClimbHud`
"Bulut katmanı 1717–5100 m (içinde / altında / üstünde)". Taban dinamik: sakin havada
iniyor, yağış ve rüzgâr yükseltiyor.

### 4. Yağış başlangıcı → `PrecipitationRenderer`
Yağmur/kar bulut tabanının altında doğuyor.

### 5. Yansıma ve çevre ışığı → `AtmosphereController`
Gök seviyesi `RenderSettings.reflectionIntensity` ve `DynamicGI.UpdateEnvironment`
sürüyor (kısılmış, saniyede bir). Bulut kapsaması gök seviyesini düşürüyor.
**Ölçülmüş kural:** yansıma ölçeklemesi kaldırılınca gece metal parçalar parlıyor.

### 6. Şimşek
`LightningFlash` çakma noktasını bulut katmanının içine yerleştiriyor; bindirme geçişi
`_LightningFlash`'i bulut alfasıyla çarpıp kütleyi içeriden aydınlatıyor. Işın
yürüyüşünün içine konamaz (yürüyüş kareye yayılı, parlama blok blok titrer).

### 7. Güneş yaması (kapalı gökte)
Bindirme geçişinde `a(1−a)` çanıyla orta kalınlıkta tepe yapan sıcak yama. Işın
yürüyüşü veremiyor: ışık sondası yatay güneşte sıfıra iniyor.

### 8. Gökyüzü ve sis ile ortak globaller
`_SunDirection`, `_LightningFlash`, `_PlanetRadius`, `_Coverage`, `_CloudBottom`,
`_CloudWind`, `_WeatherMap`, `_WeatherMapScale`, `_BaseNoise`, `_CloudScale`,
`_Evolution`. Bunlar `HeightFog.hlsl`'de bildiriliyor çünkü sis dosyası önce include
ediliyor; iki yerde bildirilirse derleyici çakışıyor.

### 9. Sahne kurulumu
`MountainSceneBootstrap` bulut geçişini, dokuları ve ayarı bağlıyor.

### 10. F1 paneli
`DebugMenu` bulut bölümleri — sürgüler doğrudan `AtmosphereSettings`'e yazıyor.

---

## Yeni sisteme taşınacak DERSLER

Bunlar ölçülerek bulundu, tekrar bulunmasın.

1. **Yoğunluk alanı ve gölge sondası görüş ışınına bakamaz.** `transmittance` üzerinden
   dallanan her şey ekranda izo-yüzey çiziyor: bulut ortasında kesik ada, kenarda koyu
   zar, halka ailesi. Ucuzlatma yalnız ışından bağımsız ölçütlerle (mesafe, LOD, kademe).

2. **Örnekleme kafesi ekranda tek olmalı.** Adım boyu ışının kendi geçmişinden
   (geçirgenlik, önceki yoğunluk) türetilirse komşu pikseller ayrışır → eşmerkezli kabuk.

3. **Kolon-sabit bir alan yüksekliği süremez.** Sürerse desenini dikey sütun olarak basar.

4. **Kaba eleme üst sınırı gerçek formülün AYNISI olmalı**, elden yazılmış yaklaşıklık
   değil. Altında kalırsa bulut, sıçrama hücresinin ekseninde düz kenarlı kıymığa kesilir.
   Sayılar tek yerde tutulup sınır onlardan türetilmeli.

5. **Mesafe girdi değildir.** Tipi/kapsamayı mesafeyle kaydırmak, bulutu kameranın
   nerede olduğuna göre değiştirir — üstüne uçunca şekil değişiyor.

6. **Erken çıkışta kuyruk kapatılır, kesilmez.** Geçirgenlik olduğu gibi bırakılırsa
   alfa 0.88'de kalır ve arka plan bulutun içinden görünür.

7. **Türetilmiş asset kendi kendini tazelemeli.** Geçerlilik imzası ayarlar **ve**
   algoritma sürümünden kurulur; sürüm üreticinin yanında durur. Asset'in ÜSTÜNE yazılır,
   silinip yeniden kurulmaz (GUID düşerse sahnedeki başvurular kopar).

8. **Çekirdek bütçesi alan oranı olarak tutulur** (toplam çekirdek alanı / harita alanı).
   Elden yazılmış çarpan 4.3 kat doyma üretti: kapsama kanalı her yerde 1, haritada ayrı
   bulut kalmadı, sınırları gürültü çizdi.

9. **Gürültü hash'i tamsayı karıştırıcı olmalı.** `Frac(Sin(Dot(p,k))*43758)` girdisi
   küçük tamsayı hücre koordinatı olduğunda korele çıkıyor: Worley'nin öznitelik noktası
   her hücrede aynı göreli yere düşüyor ve doku **kare ızgara** oluyor. Bugün "kafes",
   "hepsi aynı boy", "düzenli pufçuklar" diye görülen her şeyin kökü buydu.

10. **Katman kalınlığı boyun ölçeğidir.** Gradyan katmana normalize; katman kalınlaşınca
    bütün bulutlar birlikte uzuyor. HZD 2.5 km kullanıyor.

11. **Çizim menzili gürültü ölçeğini kilitliyor.** Adım mesafeyle büyüdüğü için uzakta
    mip yükseliyor ve ince gürültü ortalamaya yatıyor. 300 km menzilde yakın alan ince,
    uzak alan kaba gürültü istiyor — tek ölçek ikisini veremez. HZD hacimsel bulutu
    35 km'de kesip ötesini 2B katmana bırakıyor.

12. **Ayar sürgüsü ölçeklemez, eşiği kaydırır.** `harita × sürgü` yazılırsa haritanın
    sıfır olduğu yer hiçbir sürgüde kapanmaz — %100 kapsama gökyüzünü kapatmıyordu.

---

## Kaynak

HZD/Nubis, Schneider 2015 — modelleme s.34-37, aydınlatma s.50-69, render s.70-85.
Nubis 2017 (Decima) ve Nubis³ (2023, 3B voksel) devamı.

---

## Kurtarılan kod: gürültü hash'i

Silinen `CloudNoiseGenerator`'dan tek kurtarılan parça. Sinüs hash'i küçük tamsayı hücre
koordinatlarında korele çıkıyor ve Worley'yi kare ızgaraya çeviriyordu (madde 9).

```csharp
static uint Mix(uint h)
{
    h ^= h >> 16; h *= 0x7feb352du;
    h ^= h >> 15; h *= 0x846ca68bu;
    h ^= h >> 16;
    return h;
}

// Worley öznitelik noktası — girdi sarmalanmış tamsayı hücre koordinatı
static Vector3 Hash3(Vector3 p)
{
    uint x = (uint)Mathf.RoundToInt(p.x);
    uint y = (uint)Mathf.RoundToInt(p.y);
    uint z = (uint)Mathf.RoundToInt(p.z);
    uint seed = Mix(x * 0x9E3779B1u) ^ Mix(y * 0x85EBCA77u) ^ Mix(z * 0xC2B2AE3Du);

    return new Vector3(
        Mix(seed ^ 0x27D4EB2Fu) / 4294967296f,
        Mix(seed ^ 0x165667B1u) / 4294967296f,
        Mix(seed ^ 0xD3A2646Cu) / 4294967296f);
}

// Perlin köşe değeri — aynı sebep
static float Hash1(Vector3 p)
{
    uint x = (uint)Mathf.RoundToInt(p.x);
    uint y = (uint)Mathf.RoundToInt(p.y);
    uint z = (uint)Mathf.RoundToInt(p.z);
    return Mix(Mix(x * 0x9E3779B1u) ^ Mix(y * 0x85EBCA77u) ^ Mix(z * 0xC2B2AE3Du))
           / 4294967296f;
}
```

Doku yapısı (HZD s.31) doğruydu, korunacak:
- Taban 128³ RGBA: R = `Remap(perlin(4), worley(6) − 1, 1, 0, 1)`, G/B/A = Worley 6/12/24
- Detay 32³ RGB: Worley 8/16/32
- Curl 128² RGB: ıraksamasız
