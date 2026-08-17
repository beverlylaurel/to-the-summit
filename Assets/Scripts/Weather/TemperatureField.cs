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
             "olacağını bu belirler.

" +
             "−3 İDİ: donma seviyesi deniz seviyesinin 462 m ALTINDA kalıyordu, yani " +
             "dağın tamamı donmuştu ve yağış her kotta kar olarak düşüyordu. Oyunun " +
             "başında yeşillik ve YAĞMUR istendi; o kurulumda ikisi de imkânsızdı.

" +
             "+7.8 ile donma seviyesi 1200 m: ova (186 m) öğlen +6.6 °C ve yağmur " +
             "alıyor, kar etekteki kamptan ~1 km yukarıda başlıyor, zirve −29.3 °C " +
             "(rüzgârla hissedilen −38 °C). Tam fırtına donma seviyesini 500 m " +
             "indiriyor, yani kampa da kar yağabiliyor.

" +
             "Sayı kar çizgisinden türetildi: 1200 m × 6.5 °C/km. Gerekçe " +
             "DECISIONS.md → 'Ovanın kotu'.")]
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

    /// Verilen kottaki ölçülen hava sıcaklığı (°C).
    public float At(float altitude) =>
        seaLevelCelsius
        - lapseRate * altitude * 0.001f
        + daytimeWarming * time.DayFactor
        - stormCooling * weather.Precipitation;

    /// Hissedilen sıcaklık: rüzgâr deriden ısıyı taşır, termometre bunu görmez.
    /// Üşüme, nefes ve ileride dayanıklılık bu sayıyı okuyacak.
    public float FeltAt(float altitude) =>
        At(altitude) - windChillPerSpeed * wind.Velocity.magnitude;

    /// Sıcaklığın sıfıra indiği kot (metre). Yağmurun kara döndüğü sınır buradan gelir.
    /// Düşüş oranı sabit olduğu için tersi kapalı biçimde çözülüyor.
    public float FreezingLevel =>
        (seaLevelCelsius
         + daytimeWarming * time.DayFactor
         - stormCooling * weather.Precipitation) / (lapseRate * 0.001f);
}
