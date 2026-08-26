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
    ///
    /// EŞLEME GÖRÜŞTE DEĞİL SÖNÜMLEMEDE DOĞRUSAL.
    /// `[KAYNAK: Koschmieder — görüş V ile sönümleme σ ters orantılı,
    /// σ = 3.912 / V]`.
    ///
    /// Önceki hâli görüş mesafesinde doğrusaldı ve fiziksel olarak ters
    /// sonuç veriyordu: 1150 m görüşte sis yoğunluğu 0.95 çıkıyordu — yani
    /// "neredeyse tam sis". 1150 m berrak bir havadır. Aradaki her şey sise
    /// boğuluyordu; uzak yağış perdesi alpha'sını 0.10'dan 0.043'e düşürüp
    /// görünmez kılan da buydu (ölçüldü, `SYMPTOMS.md`).
    ///
    /// 3.912 sabiti hem pay hem paydada olduğu için sadeleşiyor; 1/V yetiyor.
    ///
    /// Uçlar:
    ///        60 m -> 1.00      200 m -> 0.30      1150 m -> 0.05
    ///       100 m -> 0.60      500 m -> 0.11     20000 m -> 0.00
    public float FogDensity01
    {
        get
        {
            if (atmosphere == null) return manualFogDensity;

            float sigma = 1f / Mathf.Max(1f, atmosphere.Visibility);
            float sigmaFull = 1f / Mathf.Max(1f, fogFullVisibility);
            float sigmaClear = 1f / Mathf.Max(1f, fogClearVisibility);

            return Mathf.Clamp01(Mathf.InverseLerp(sigmaClear, sigmaFull, sigma));
        }
    }

    static Vector3 SafeHorizontal(Vector3 v)
    {
        var flat = new Vector3(v.x, 0f, v.z);
        return flat.sqrMagnitude > 1e-8f ? flat.normalized : Vector3.right;
    }
}
