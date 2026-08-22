// ROL: Kar sisteminin dış dünyadan okuduğu her şey. Oyunun mevcut hava, rüzgâr
// ve gece/gündüz sistemleri bu arayüzü implemente eder.
// Kar sistemi bu değerleri ASLA yazmaz.
// Çağıran: SnowManager ve alt bileşenleri.

using UnityEngine;

public enum PrecipitationKind
{
    None,
    Rain,
    Snow,
    Sleet,
}

/// TEK KAPI. Hiçbir kar dosyası `RenderSettings.fog`, kendi gündöngüsü veya kendi
/// rüzgâr gürültüsünü kullanmaz; hepsi buradan okur (spec §3.1).
///
/// Kendi rüzgârını, kendi güneşini, kendi sisini kuran her satır bir hatadır.
public interface ISnowEnvironmentSource
{
    // --- Rüzgâr (mevcut rüzgâr sisteminden) ---

    /// Normalize, dünya uzayı, yatay.
    Vector3 WindDirection { get; }

    /// m/s.
    float WindSpeed { get; }

    // --- Gece/gündüz döngüsünden ---

    /// Ana directional light.
    Light Sun { get; }

    /// 0 = ufuk altı, 1 = tepe.
    float SunElevation01 { get; }

    /// Celsius. Gündöngüsü + mevsim bunu sürer.
    float TemperatureC { get; }

    /// SICAKLIĞIN SIFIRA İNDİĞİ KOT (m).
    ///
    /// SPEC EKLENTİSİ (§3.1'de yok). Kar çizgisi bundan türüyor: dağın
    /// belli bir kottan yukarısı doğuştan karlı. Ayrı bir "kar çizgisi"
    /// sayısı tanımlamak ikinci bir kaynak yaratırdı ve "sıcaklık +8 ama
    /// tepe karsız" gibi çelişkiler üretirdi. Gerekçe `DECISIONS.md`.
    float FreezingLevelY { get; }

    // --- Yağış (mevcut yağmur sisteminden) ---

    PrecipitationKind PrecipKind { get; }

    /// 0..1, mevcut sistemin şiddet değeri.
    float PrecipIntensity01 { get; }

    // --- Sis (sadece okunur, kar tanesi fade'i için) ---

    /// 0..1 normalize, mevcut sis sisteminden.
    float FogDensity01 { get; }
}
