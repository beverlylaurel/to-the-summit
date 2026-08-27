// ROLE: connects this game's weather / wind / day-night / atmosphere systems
// to the sea system. READS ONLY.
// CALLED BY: SeaManager (as an ISeaEnvironmentSource).

using UnityEngine;

/// THE BRIDGE IS MEASURED, NOT GUESSED.
///
/// Spec §3.2: "Glue specific to the user's game. Do not try to guess it."
/// The bindings below were measured against this project's real API:
///
///   WindField.PrevailingDirection  -> WindDirection
///   WindField.FreeAirSpeed         -> WindSpeed  (m/s, free air)
///   TimeOfDay.SunHeight            -> SunElevation01  (SunDirection.y)
///   AtmosphereController.Coverage  -> CloudCover01
///   WeatherState.Precipitation     -> PrecipIntensity01
///   TemperatureField.At(y)         -> PrecipKind (snow / rain split)
///
/// SKY COLOR IS NOT BOUND. In this project the sky comes from the volumetric
/// cloud system and the skybox; there is no single "zenith color" property. A
/// manual value is used and a `TODO(user)` was left — spec §3.2 explicitly
/// allows it: "With manual default values the system works end to end."
///
/// DEPENDENCIES COME FROM THE INSPECTOR. No `FindObjectOfType`, no singletons
/// (`CLAUDE.md`). An unbound source falls back to its manual value.
[DisallowMultipleComponent]
public class SeaEnvironmentBridge : MonoBehaviour, ISeaEnvironmentSource
{
    [Header("The game's existing systems")]
    [SerializeField] WindField wind;
    [SerializeField] WeatherState weather;
    [SerializeField] TimeOfDay timeOfDay;
    [SerializeField] AtmosphereController atmosphere;
    [SerializeField] TemperatureField temperature;
    [SerializeField] Light sunLight;

    [Header("Manual values until the bridge is wired (spec §3.2)")]
    [SerializeField] Vector3 manualWindDirection = new Vector3(1f, 0f, 0f);
    [SerializeField] float manualWindSpeed = 8f;

    /// TODO(user): sky color does not live in a single property in this
    /// project. If a zenith / horizon color can be extracted from the
    /// volumetric cloud system or the skybox, it should be bound here.
    ///
    /// Default [SOURCE: Tessendorf 2004 §6.3 sample shader —
    /// `sky = color(0.69, 0.84, 1)`].
    [SerializeField] Color manualSkyColor = new Color(0.69f, 0.84f, 1.00f);
    [SerializeField] Color manualHorizonColor = new Color(0.80f, 0.86f, 0.92f);

    [SerializeField, Range(0f, 1f)] float manualCloudCover = 0.3f;
    [SerializeField, Range(0f, 1f)] float manualFogDensity = 0.2f;

    /// Whether precipitation is snow or rain follows from temperature —
    /// there is no separate "precipitation kind" variable and none will be
    /// created (it would be a second source of truth).
    [Tooltip("Below this temperature precipitation counts as snow (°C).")]
    [SerializeField] float snowThresholdC = 0f;

    [Tooltip("Above this temperature precipitation counts as rain (°C). Sleet in between.")]
    [SerializeField] float rainThresholdC = 2f;

    /// SET UP FROM CODE. The scene is built through
    /// `MountainSceneBootstrap`; nothing is wired by hand.
    ///
    /// The sun must be `TimeOfDay`'s OWN light: the first setup picked the
    /// first directional light in the scene and that turned out to be the
    /// LIGHTNING light.
    public void Bind(WindField windField, WeatherState weatherState,
                     TimeOfDay time, AtmosphereController atmosphereController,
                     TemperatureField temperatureField, Light sun)
    {
        wind = windField;
        weather = weatherState;
        timeOfDay = time;
        atmosphere = atmosphereController;
        temperature = temperatureField;
        sunLight = sun;
    }

    // -------------------------------------------------------------- wind

    public Vector3 WindDirection
    {
        get
        {
            if (wind == null) return manualWindDirection.normalized;

            Vector3 d = wind.PrevailingDirection;
            d.y = 0f;

            return d.sqrMagnitude > 1e-6f ? d.normalized : manualWindDirection.normalized;
        }
    }

    /// U10 — wind speed at the 10 m reference height.
    ///
    /// `FreeAirSpeed` is the free-air speed, before terrain exposure
    /// (`TerrainWindShelter`) is applied. The sea sits over open water, so
    /// there is no shelter — this is the correct one. Using
    /// `Velocity.magnitude` would feed local gusts into the spectrum and
    /// spec §3.4 forbids that.
    public float WindSpeed => wind != null ? wind.FreeAirSpeed : manualWindSpeed;

    // --------------------------------------------------------- day/night

    public Light Sun => sunLight;

    /// `TimeOfDay.SunHeight` is already `SunDirection.y`, i.e. the sine of
    /// the sun's elevation. `saturate` cuts the negative night values.
    public float SunElevation01 =>
        timeOfDay != null ? Mathf.Clamp01(timeOfDay.SunHeight) : 0.5f;

    // -------------------------------------------------------- atmosphere

    public Color SkyColor => manualSkyColor;

    public Color HorizonColor => manualHorizonColor;

    public float CloudCover01 =>
        atmosphere != null ? Mathf.Clamp01(atmosphere.Coverage) : manualCloudCover;

    /// Fog density is read by the sea for information only; the fog itself is
    /// applied with URP's `MixFog` (spec §3.5).
    public float FogDensity01 => manualFogDensity;

    // ----------------------------------------------------- precipitation

    public SeaPrecipitationKind PrecipKind
    {
        get
        {
            if (weather == null || weather.Precipitation <= 0.001f)
                return SeaPrecipitationKind.None;

            if (temperature == null) return SeaPrecipitationKind.Rain;

            // Temperature at sea level — that is where the sea is.
            float c = temperature.At(transform.position.y);

            if (c <= snowThresholdC) return SeaPrecipitationKind.Snow;
            if (c >= rainThresholdC) return SeaPrecipitationKind.Rain;

            return SeaPrecipitationKind.Sleet;
        }
    }

    public float PrecipIntensity01 =>
        weather != null ? Mathf.Clamp01(weather.Precipitation) : 0f;
}
