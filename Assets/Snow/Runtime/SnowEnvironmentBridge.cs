// ROLE: binds the game's existing weather/light systems to ISnowEnvironmentSource.
// CALLED BY: SnowManager, assigned from the Inspector.

using UnityEngine;

/// THE SNOW IS TIED TO THE EXISTING WEATHER (spec 3.1, 3.2).
///
/// Every field is read from the real system, and there is NO FALLBACK: a missing
/// reference throws on enable rather than quietly publishing a made-up number.
///
/// It used to fall back to hand-typed values (wind 3 m/s, -4 C, precipitation 0.5) so a
/// half-built scene would still run. The scene is built now, and the fallbacks had
/// stopped being a convenience: a snow system reading -4 C from a field nobody set looks
/// exactly like a snow system reading -4 C from a thermometer.
///
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


    void OnEnable()
    {
        Require(wind, nameof(wind));
        Require(time, nameof(time));
        Require(temperature, nameof(temperature));
        Require(weather, nameof(weather));
        Require(atmosphere, nameof(atmosphere));
        Require(observer, nameof(observer));
    }

    void Require(Object reference, string field)
    {
        if (reference == null)
            throw new System.InvalidOperationException(
                $"{nameof(SnowEnvironmentBridge)}: {field} is not assigned.");
    }

    public Vector3 WindDirection => SafeHorizontal(wind.Velocity);

    public Vector3 PrevailingWindDirection => wind.PrevailingDirection;

    public float WindSpeed => new Vector2(wind.Velocity.x, wind.Velocity.z).magnitude;

    public Light Sun => sunLight;

    public float SunElevation01 => Mathf.Clamp01(time.SunHeight);

    public float TemperatureC => temperature.At(observer.position.y);

    /// THE PROJECT HAS NO NOTION OF A PRECIPITATION "KIND". If there is precipitation,
    /// `Rain` is reported; the decision that the snow is snow lives in
    /// `SnowfallController`'s temperature hysteresis (spec §3.4). Guessing the kind here
    /// would put two different decisions in two separate places.
    /// The rain/snow boundary is the thermometer's, not this bridge's: the sky, the sea and
    /// the rock all read the same `SnowFractionAt` so they cannot disagree about what is
    /// falling on them.
    public float SnowFraction01 => temperature.SnowFractionAt(observer.position.y);

    /// THE KIND AND THE FRACTION COME FROM THE SAME NUMBER, so the sky cannot drop snow
    /// while the sea takes rain. That contradiction was measured on 2026-09-03: the
    /// fraction was stuck at 1 and this property answered `Rain` to everyone who asked.
    public PrecipitationKind PrecipKind
    {
        get
        {
            if (PrecipIntensity01 <= 0.001f) return PrecipitationKind.None;
            float snow = SnowFraction01;
            if (snow >= 0.999f) return PrecipitationKind.Snow;
            if (snow <= 0.001f) return PrecipitationKind.Rain;
            return PrecipitationKind.Sleet;
        }
    }

    public float PrecipIntensity01 => Mathf.Clamp01(weather.Precipitation);

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
