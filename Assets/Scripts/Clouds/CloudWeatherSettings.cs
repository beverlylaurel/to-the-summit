using UnityEngine;

/// Dünya durumunun bulut ayarlarına nasıl çevrileceği. Sayılar `CloudWeatherDriver`'ın
/// içine gömülmüyor: aynı sürücü farklı ayarla tekrar kullanılabilsin diye asset.
///
/// Sakin uçtaki değerler bilerek profildeki onaylanmış görüntüyle aynı — fırtına sıfırken
/// bağ eklenmemiş gibi görünüyor, böylece bağın ne kattığı ayırt edilebiliyor.
[CreateAssetMenu(menuName = "To The Summit/Bulut Hava Ayarları")]
public class CloudWeatherSettings : ScriptableObject
{
    // KAPSAMA BURADA YOK. Kuralı `AtmosphereController` tutuyor (fırtına kütlesi, kuru
    // hava ritmi, açık pencere, test kilidi) ve bulut onu olduğu gibi tüketiyor. Buraya
    // ikinci bir eşleme konsaydı gökyüzü ile bulut çelişebilirdi.

    [Header("Yoğunluk")]
    [Tooltip("Fırtına yokken yoğunluk çarpanı.")]
    [Range(0f, 1f)] public float calmDensity = 0.4f;

    [Tooltip("Tam fırtınada yoğunluk çarpanı.")]
    [Range(0f, 1f)] public float stormDensity = 0.6f;
}
