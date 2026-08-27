// ROL: Deniz sisteminin dis dunyadan okudugu her sey. Oyunun mevcut hava,
// ruzgar, bulut ve gece/gunduz sistemleri bu arayuzu implemente ediyor.
// Deniz sistemi bu degerleri ASLA yazmiyor.
// Cagiran: SeaManager.

using UnityEngine;

public enum SeaPrecipitationKind { None, Rain, Snow, Sleet }

/// DENİZ SÜRMEZ, OKUR.
///
/// Spec §3'ün temel kuralı. Deniz sistemi içinde `RenderSettings`,
/// `VolumeProfile` veya `Light.intensity` yazan tek bir satır olmayacak;
/// Faz 1 kabul kriteri bunu kod aramasıyla doğruluyor.
public interface ISeaEnvironmentSource
{
    // --- Rüzgâr: dalga spektrumunun ANA girdisi (spec §6) ---

    /// Normalize, dünya uzayı, yatay.
    Vector3 WindDirection { get; }

    /// m/s, 10 m referans yüksekliği (U10).
    float WindSpeed { get; }

    // --- Gece/gündüz ---

    Light Sun { get; }

    /// `saturate(dot(-sunForward, up))`. Güneş parıltısının gece kapanma
    /// kapısı buradan (spec §12.5).
    float SunElevation01 { get; }

    // --- Atmosfer: su yüzeyi yansımasının girdisi (spec §12) ---

    /// Zenit gökyüzü rengi.
    Color SkyColor { get; }

    Color HorizonColor { get; }

    /// 0 açık, 1 kapalı.
    float CloudCover01 { get; }

    float FogDensity01 { get; }

    // --- Yağış: köpük ve yüzey pürüzlülüğü (spec §13) ---

    SeaPrecipitationKind PrecipKind { get; }

    float PrecipIntensity01 { get; }
}
