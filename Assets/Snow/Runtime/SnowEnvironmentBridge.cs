// ROL: Oyunun mevcut hava/ışık sistemlerini ISnowEnvironmentSource'a bağlar.
// Çağıran: SnowManager, Inspector'dan atanarak.

using UnityEngine;

/// KAR MEVCUT HAVAYA BAĞLI (spec §3.1, §3.2).
///
/// Her alan gerçek sistemden okunuyor. Bir referans atanmamışsa o alan
/// manuel değere düşüyor — sistem yarım kurulmuş sahnede de çalışsın diye.
/// Kar sistemi bu değerlerin HİÇBİRİNİ yazmıyor.
public class SnowEnvironmentBridge : MonoBehaviour, ISnowEnvironmentSource
{
    [Header("Mevcut sistemler")]
    [SerializeField] Light sunLight;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;
    [SerializeField] TemperatureField temperature;
    [SerializeField] WeatherState weather;
    [SerializeField] AtmosphereController atmosphere;

    [Tooltip("Sıcaklık irtifaya bağlı — hangi noktadan okunacak.")]
    [SerializeField] Transform observer;

    [Header("Görüş → sis yoğunluğu")]
    [Tooltip("Bu görüş mesafesinde sis tam (yoğunluk 1).")]
    [SerializeField] float fogFullVisibility = 60f;

    [Tooltip("Bu görüş mesafesinde sis yok (yoğunluk 0).")]
    [SerializeField] float fogClearVisibility = 20000f;

    [Header("Referans atanmadıysa kullanılacak değerler")]
    [SerializeField] Vector3 manualWindDirection = Vector3.right;
    [SerializeField] float manualWindSpeed = 3f;
    [SerializeField] float manualTemperatureC = -4f;
    [SerializeField] float manualFreezingLevelY = 1400f;
    [SerializeField] PrecipitationKind manualPrecipKind = PrecipitationKind.Snow;
    [SerializeField, Range(0f, 1f)] float manualPrecipIntensity = 0.5f;
    [SerializeField, Range(0f, 1f)] float manualFogDensity = 0.2f;

    public Vector3 WindDirection => wind != null
        ? SafeHorizontal(wind.Velocity)
        : manualWindDirection.normalized;

    public float WindSpeed => wind != null
        ? new Vector2(wind.Velocity.x, wind.Velocity.z).magnitude
        : manualWindSpeed;

    public Light Sun => sunLight;

    public float SunElevation01 => time != null
        ? Mathf.Clamp01(time.SunHeight)
        : (sunLight != null ? Mathf.Clamp01(Vector3.Dot(-sunLight.transform.forward, Vector3.up)) : 0f);

    public float TemperatureC => temperature != null && observer != null
        ? temperature.At(observer.position.y)
        : manualTemperatureC;

    public float FreezingLevelY => temperature != null
        ? temperature.FreezingLevel
        : manualFreezingLevelY;

    /// PROJEDE YAĞIŞIN "TÜRÜ" KAVRAMI YOK. Yağış varsa `Rain` bildiriliyor;
    /// karın kar olduğu kararı `SnowfallController`'ın sıcaklık histerezisinde
    /// (spec §3.4). Burada tür tahmin edilirse iki ayrı yerde iki farklı karar
    /// olurdu.
    public PrecipitationKind PrecipKind => weather != null
        ? (PrecipIntensity01 > 0.001f ? PrecipitationKind.Rain : PrecipitationKind.None)
        : manualPrecipKind;

    public float PrecipIntensity01 => weather != null
        ? Mathf.Clamp01(weather.Precipitation)
        : manualPrecipIntensity;

    /// GÖRÜŞ METRE, SİS 0..1. Dönüşüm burada yapılıyor çünkü sınırlar bu
    /// projeye ait; sis sistemine dokunulmuyor.
    public float FogDensity01 => atmosphere != null
        ? 1f - Mathf.Clamp01(Mathf.InverseLerp(fogFullVisibility, fogClearVisibility,
                                               atmosphere.Visibility))
        : manualFogDensity;

    static Vector3 SafeHorizontal(Vector3 v)
    {
        var flat = new Vector3(v.x, 0f, v.z);
        return flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.right;
    }
}
