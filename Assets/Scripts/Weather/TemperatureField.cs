using System;
using UnityEngine;

/// Havanın sıcaklığı. Dördüncü kaynak: hava, rüzgâr ve saatin yanında duruyor.
///
/// Donma seviyesi ARTIK BURADAN TÜREYİP `AltitudeWeatherDriver`'a gidiyor. Önceden orada
/// kendi formülü vardı ("referans kot − fırtına düşüşü + gündüz yükselişi") ve o formül
/// aslında örtük bir sıcaklık modeliydi: sıcaklığın kendisi hiç var olmadan sonuçları
/// hesaplanıyordu. Nefes, kırağı, üşüme, donma gibi her yeni özellik kendi sıcaklık
/// tahminini uydurmak zorunda kalacaktı — mimarinin yasakladığı şey tam olarak bu.
///
/// Sayılar davranışı DEĞİŞTİRMİYOR: eski formülün metre cinsinden kaymaları sıcaklık
/// cinsinden ifade edildi. Sıcaklık düşüş oranı 6.5 °C/km ile çarpılınca aynı kotlar
/// çıkıyor.
///
/// TEMEL SICAKLIK KIŞA ÇEKİLDİ (2026-08-13). Yağışın her kotta kar olarak düşmesi
/// istendi. Bunun tek doğru kaldıracı burası: yağmur/kar sınırı donma seviyesinden
/// türüyor ve donma seviyesi bu sayıdan. Yüzeye "her yerde kar" diye ikinci bir kural
/// yazmak, atmosfer zincirine bağımsız bir kaynak sokardı — mimarinin yasakladığı şey.
///
/// Eşiği geçmek için gereken pay kâğıtta hesaplandı: sulu kar bandı donma seviyesinin
/// 220 m üstüne kadar sürüyor ve kar çizgisi düzensizliği ±110 m oynatıyor, yani
/// zeminde TAM kar için donma seviyesi zeminin en az 330 m altında olmalı. Öğle
/// ısınması dahil en sıcak hâlde −211 m çıkıyor.
public class TemperatureField : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;

    [Tooltip("Deniz seviyesindeki temel sıcaklık (°C). Donma seviyesinin nerede " +
             "olacağını bu belirler. −3 İDİ: donma seviyesi deniz seviyesinin 462 m " +
             "ALTINDA kalıyordu, yani dağın tamamı donmuştu ve yağış her kotta kar " +
             "olarak düşüyordu. Oyunun başında yeşillik ve YAĞMUR istendi; o " +
             "kurulumda ikisi de imkânsızdı. +7.8 ile donma seviyesi 1200 m: ova " +
             "(186 m) öğlen +6.6 °C ve yağmur alıyor, kar etekteki kamptan ~1 km " +
             "yukarıda başlıyor, zirve −29.3 °C (rüzgârla hissedilen −38 °C). Tam " +
             "fırtına donma seviyesini 500 m indiriyor, yani kampa da kar " +
             "yağabiliyor. Sayı kar çizgisinden türedi: 1200 m × 6.5 °C/km. " +
             "Gerekçe DECISIONS.md → 'Ovanın kotu'.")]
    [SerializeField] float seaLevelCelsius = 7.8f;

    [Tooltip("Yükseklikle düşüş (°C / kilometre). Standart atmosferin oranı 6.5.")]
    [SerializeField] float lapseRate = 6.5f;

    [Tooltip("Öğle ısınmasının kattığı (°C). Gündüz katsayısıyla ölçeklenir.")]
    [SerializeField] float daytimeWarming = 1.63f;

    [Tooltip("Tam fırtınanın düşürdüğü (°C). Soğuk cephe donma seviyesini aşağı iter.")]
    [SerializeField] float stormCooling = 3.25f;

    [Tooltip("Rüzgârın hissedilen sıcaklığı düşürme payı (°C, metre/saniye başına). " +
             "Gerçek rüzgâr soğuğu doğrusal değil ama bu aralıkta yakın; asıl iş " +
             "hissedilenin ölçülenden AYRI bir sayı olması.")]
    [SerializeField] float windChillPerSpeed = 0.45f;

    void OnEnable()
    {
        if (weather == null)
            throw new InvalidOperationException($"{nameof(TemperatureField)}: {nameof(weather)} atanmadı.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(TemperatureField)}: {nameof(wind)} atanmadı.");
        if (time == null)
            throw new InvalidOperationException($"{nameof(TemperatureField)}: {nameof(time)} atanmadı.");
    }

    public void Bind(WeatherState state, WindField field, TimeOfDay clock)
    {
        weather = state;
        wind = field;
        time = clock;
    }

    bool overrideActive;
    float overrideSeaLevelCelsius;

    /// TEŞHİS GEÇERSİZ KILMASI — deseni `WindField.ApplyOverride` ile aynı.
    ///
    /// Deniz seviyesi sıcaklığını değiştiriyor, `At` / `FeltAt` / `FreezingLevel`
    /// üçü de ondan türediği için HUD, donma seviyesi, kar çizgisi ve kar
    /// yağışı AYNI ANDA kayıyor. Yalnız kar sistemine ayrı bir sıcaklık
    /// dayatmak "HUD +8 °C derken kar yağıyor" çelişkisini üretirdi.
    public void ApplyOverride(float seaLevelC)
    {
        overrideActive = true;
        overrideSeaLevelCelsius = seaLevelC;
    }

    public void ClearOverride() => overrideActive = false;

    public bool HasOverride => overrideActive;

    float SeaLevelC => overrideActive ? overrideSeaLevelCelsius : seaLevelCelsius;

    /// TERMAL EYLEMSİZLİK. `DayFactor`'ın gecikmeli hâli.
    ///
    /// Güneşin ısıtması anında değil: yer önce ısınır, havayı sonra ısıtır.
    /// Gerçek dünyada günün en soğuk anı GÜN DOĞUMUdur — güneş çoktan çıkmıştır
    /// ama gece boyunca kaybedilen ısı henüz geri gelmemiştir; tepe sıcaklık da
    /// öğlede değil, öğleden birkaç saat sonradır.
    ///
    /// `DayFactor` doğrudan kullanılıyordu: güneş ufka değdiği saniyede sıcaklık
    /// birkaç derece zıplıyordu.
    float warmth;

    bool warmthReady;

    /// Isınmanın güneşi izleme gecikmesi (saniye, oyun zamanı).
    [Tooltip("Havanın güneşi izleme gecikmesi. Büyük değer: sabah daha soğuk, " +
             "akşam daha ılık kalır.")]
    [SerializeField] float thermalLagSeconds = 2700f;

    void LateUpdate()
    {
        if (time == null) return;

        float target = time.DayFactor;

        // İlk karede hedefe oturuyor: yoksa sahne her açılışta gece
        // sıcaklığından başlayıp yavaşça ısınırdı.
        if (!warmthReady)
        {
            warmth = target;
            warmthReady = true;
            return;
        }

        warmth = Mathf.Lerp(warmth, target,
                            1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1f, thermalLagSeconds)));
    }

    /// Gecikmeli gündüz katsayısı. `time` yoksa anlık değere düşüyor.
    float Warmth => warmthReady ? warmth : (time != null ? time.DayFactor : 0f);

    /// Verilen kottaki ölçülen hava sıcaklığı (°C).
    public float At(float altitude) =>
        SeaLevelC
        - lapseRate * altitude * 0.001f
        + daytimeWarming * Warmth
        - stormCooling * weather.Precipitation;

    /// Hissedilen sıcaklık: rüzgâr deriden ısıyı taşır, termometre bunu görmez.
    /// Üşüme, nefes ve ileride dayanıklılık bu sayıyı okuyacak.
    public float FeltAt(float altitude) =>
        At(altitude) - windChillPerSpeed * wind.Velocity.magnitude;

    /// Sıcaklığın sıfıra indiği kot (metre). Yağmurun kara döndüğü sınır buradan gelir.
    /// Düşüş oranı sabit olduğu için tersi kapalı biçimde çözülüyor.
    public float FreezingLevel =>
        (SeaLevelC
         + daytimeWarming * Warmth
         - stormCooling * weather.Precipitation) / (lapseRate * 0.001f);
}
