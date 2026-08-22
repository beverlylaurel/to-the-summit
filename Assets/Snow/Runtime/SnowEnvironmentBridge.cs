// ROL: Oyunun mevcut hava/ışık sistemlerini ISnowEnvironmentSource'a bağlar.
// Bu dosya projeye özeldir; TODO'ları kullanıcı dolduracak.
// Çağıran: SnowManager, Inspector'dan atanarak.

using UnityEngine;

/// MANUEL DEĞERLERLE SİSTEM BAŞTAN SONA ÇALIŞIR (spec §3.2). Köprü sonradan
/// bağlanır. Bu, kar sisteminin geliştirilmesini mevcut sistemlere dokunmadan
/// mümkün kılıyor.
///
/// Aşağıdaki TODO'ların yanında bu projedeki HAZIR karşılıkları yazılı. Bağlamak
/// bir karardır ve kullanıcıya aittir; spec bu dosyanın tahmin edilmesini
/// açıkça yasaklıyor.
public class SnowEnvironmentBridge : MonoBehaviour, ISnowEnvironmentSource
{
    [Header("Bağlanacak mevcut sistemler — Inspector'dan atayın")]
    [SerializeField] Light sunLight;

    // TODO(kullanici): Asagidaki alanlari kendi hava/ruzgar/gunDongusu
    // bilesenlerinize baglayin. BU PROJEDEKI HAZIR KARSILIKLAR:
    //
    //   [SerializeField] WindField wind;                  // WindDirection, WindSpeed
    //   [SerializeField] TimeOfDay time;                  // SunElevation01
    //   [SerializeField] TemperatureField temperature;    // TemperatureC
    //   [SerializeField] WeatherState weather;            // PrecipIntensity01, PrecipKind
    //   [SerializeField] AtmosphereController atmosphere; // FogDensity01
    //   [SerializeField] Transform observer;              // sicaklik irtifaya bagli
    //
    // Ifadeler:
    //   WindDirection    -> new Vector3(wind.Velocity.x, 0f, wind.Velocity.z).normalized
    //   WindSpeed        -> new Vector2(wind.Velocity.x, wind.Velocity.z).magnitude
    //   SunElevation01   -> Mathf.Clamp01(time.SunHeight)
    //   TemperatureC     -> temperature.At(observer.position.y)
    //   PrecipIntensity01-> weather.Precipitation
    //   PrecipKind       -> weather.Precipitation > 0.001f ? Rain : None
    //                       (projede yagisin "turu" kavrami yok; kar karari
    //                        SnowfallController histerezisinde, spec §3.4)
    //   FogDensity01     -> 1f - Mathf.InverseLerp(minVisibility, maxVisibility,
    //                                              atmosphere.Visibility)
    //                       (Visibility metre; iki sinir kullanicinin verecegi sayi)

    [Header("Köprü kurulana kadar kullanılacak manuel değerler")]
    [SerializeField] Vector3 manualWindDirection = Vector3.right;
    [SerializeField] float manualWindSpeed = 3f;
    [SerializeField] float manualTemperatureC = -4f;
    [SerializeField] PrecipitationKind manualPrecipKind = PrecipitationKind.Snow;
    [SerializeField, Range(0f, 1f)] float manualPrecipIntensity = 0.5f;
    [SerializeField, Range(0f, 1f)] float manualFogDensity = 0.2f;

    public Vector3 WindDirection => manualWindDirection.normalized;      // TODO: wind
    public float WindSpeed => manualWindSpeed;                           // TODO: wind
    public Light Sun => sunLight;

    public float SunElevation01 => sunLight != null
        ? Mathf.Clamp01(Vector3.Dot(-sunLight.transform.forward, Vector3.up))
        : 0f;

    public float TemperatureC => manualTemperatureC;                     // TODO: gunDongusu
    public PrecipitationKind PrecipKind => manualPrecipKind;             // TODO: hava
    public float PrecipIntensity01 => manualPrecipIntensity;             // TODO: hava
    public float FogDensity01 => manualFogDensity;                       // TODO: sis
}
