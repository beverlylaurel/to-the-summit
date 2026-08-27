// ROLE: binds the game's existing weather/light systems to ISnowEnvironmentSource.
// CALLED BY: SnowManager, assigned from the Inspector.

using UnityEngine;

/// THE SNOW IS TIED TO THE EXISTING WEATHER (spec §3.1, §3.2).
///
/// Every field is read from the real system. If a reference is not assigned that field
/// falls back to a manual value — so the system works in a half-built scene too.
/// The snow system writes NONE of these values.
public class SnowEnvironmentBridge : MonoBehaviour, ISnowEnvironmentSource
{
    [Header("Mevcut sistemler")]
    [SerializeField] Light sunLight;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;
    [SerializeField] TemperatureField temperature;
    [SerializeField] WeatherState weather;
    [SerializeField] AtmosphereController atmosphere;

    [Tooltip("The temperature depends on altitude — which point it is read at.")]
    [SerializeField] Transform observer;

    [Header("Visibility → fog density")]
    [Tooltip("At this visibility the fog is full (density 1).")]
    [SerializeField] float fogFullVisibility = 60f;

    [Tooltip("At this visibility there is no fog (density 0).")]
    [SerializeField] float fogClearVisibility = 20000f;

    [Header("Values used when no reference is assigned")]
    [SerializeField] Vector3 manualWindDirection = Vector3.right;
    [SerializeField] float manualWindSpeed = 3f;
    [SerializeField] float manualTemperatureC = -4f;
    [SerializeField] PrecipitationKind manualPrecipKind = PrecipitationKind.Snow;
    [SerializeField, Range(0f, 1f)] float manualPrecipIntensity = 0.5f;
    [SerializeField, Range(0f, 1f)] float manualFogDensity = 0.2f;

    public Vector3 WindDirection => wind != null
        ? SafeHorizontal(wind.Velocity)
        : manualWindDirection.normalized;

    public Vector3 PrevailingWindDirection => wind != null
        ? wind.PrevailingDirection
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

    /// THE PROJECT HAS NO NOTION OF A PRECIPITATION "KIND". If there is precipitation,
    /// `Rain` is reported; the decision that the snow is snow lives in
    /// `SnowfallController`'s temperature hysteresis (spec §3.4). Guessing the kind here
    /// would put two different decisions in two separate places.
    public PrecipitationKind PrecipKind => weather != null
        ? (PrecipIntensity01 > 0.001f ? PrecipitationKind.Rain : PrecipitationKind.None)
        : manualPrecipKind;

    public float PrecipIntensity01 => weather != null
        ? Mathf.Clamp01(weather.Precipitation)
        : manualPrecipIntensity;

    /// VISIBILITY IS IN METRES, FOG IS 0..1. The conversion happens here because the
    /// bounds belong to this project; the fog system is not touched.
    ///
    /// THE MAPPING IS LINEAR IN THE EXTINCTION, NOT IN THE VISIBILITY.
    /// `[SOURCE: Koschmieder — the visibility V and the extinction σ are inversely
    /// proportional, σ = 3.912 / V]`.
    ///
    /// The previous form was linear in the visibility distance and gave a physically
    /// inverted result: at 1150 m of visibility the fog density came out 0.95 — i.e.
    /// "almost full fog". 1150 m is clear weather. Everything in between was drowned in
    /// fog; that is also what dropped the distant precipitation curtain's alpha from 0.10
    /// to 0.043 and made it invisible (measured, `SYMPTOMS.md`).
    ///
    /// Because the constant 3.912 appears in both the numerator and the denominator it
    /// cancels; 1/V is enough.
    ///
    /// The ends:
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
