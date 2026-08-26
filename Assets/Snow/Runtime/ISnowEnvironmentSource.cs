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

    /// HÂKİM RÜZGÂR YÖNÜ — anlık değil.
    ///
    /// Yer şekilleri (sastrugi, ripple) bu ekseni kullanıyor. Anlık yön
    /// kullanılırsa desen dünyada kayıyor: alan `dot(worldXZ, eksen)` üzerinden
    /// kuruluyor ve dağın ortasında |worldXZ| yedi bin metre — bir hamlenin
    /// 0.14 radyanlık sapması deseni 980 metre sürüklüyor. Aynı ölçüm
    /// `WindField.PrevailingDirection` yanında da kayıtlı.
    Vector3 PrevailingWindDirection { get; }

    // --- Gece/gündüz döngüsünden ---

    /// Ana directional light.
    Light Sun { get; }

    /// 0 = ufuk altı, 1 = tepe.
    float SunElevation01 { get; }

    /// Celsius. Gündöngüsü + mevsim bunu sürer.
    float TemperatureC { get; }

    // --- Yağış (mevcut yağmur sisteminden) ---

    PrecipitationKind PrecipKind { get; }

    /// 0..1, mevcut sistemin şiddet değeri.
    float PrecipIntensity01 { get; }

    // --- Sis (sadece okunur, kar tanesi fade'i için) ---

    /// 0..1 normalize, mevcut sis sisteminden.
    float FogDensity01 { get; }
}
