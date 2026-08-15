using UnityEngine;

/// Dünya durumunun atmosfer ayarlarına nasıl çevrileceği. Sayılar `SkyWeatherDriver`'ın
/// içine gömülmüyor: aynı sürücü farklı ayarla tekrar kullanılabilsin diye asset.
[CreateAssetMenu(menuName = "To The Summit/Gökyüzü Hava Ayarları")]
public class SkyWeatherSettings : ScriptableObject
{
    // AEROSOL YOĞUNLUĞU = aerosol sütununun ZENİT OPAKLIĞI. Gözlemci dik yukarı bakarken
    // aerosol katmanının soğurduğu ışık oranı, birimsiz, 0-1.
    //
    // Uçlar kâğıtta, sönüm katsayısı ve 1.2 km ölçek yüksekliğinden (`[H20 s.605]`):
    //   temiz dağ havası  σ ≈ 5e-6 m⁻¹  → 1 − exp(−0.006) ≈ 0.006
    //   paketin varsayılanı σ = 10e-6   → 1 − exp(−0.012) ≈ 0.012
    //   fırtına, kar ve nem σ ≈ 60e-6   → 1 − exp(−0.072) ≈ 0.069
    //
    // Aralık bilerek dar: Mie aşırı kullanıldığında sahne pusa gömülüyor ve gökyüzü
    // griye düşüyor (brief, Mie bölümü).

    [Header("Aerosol")]
    [Tooltip("Fırtına yokken zenit aerosol opaklığı. Temiz yüksek dağ havası.")]
    [Range(0f, 0.2f)] public float clearAerosol = 0.006f;

    [Tooltip("Tam fırtınada zenit aerosol opaklığı. Kar, nem ve savrulan kristal.")]
    [Range(0f, 0.2f)] public float stormAerosol = 0.069f;
}
