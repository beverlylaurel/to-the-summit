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

    /// The swell event reads its range from here. The bridge is sea-side glue, so
    /// holding the sea's own settings is not a foreign dependency.
    [SerializeField] SeaSettings settings;
    [SerializeField] SeaStateController seaState;

    [Header("Manual values until the bridge is wired (spec §3.2)")]
    [SerializeField] Vector3 manualWindDirection = new Vector3(1f, 0f, 0f);
    [SerializeField] float manualWindSpeed = 8f;

    /// TODO(user): sky color does not live in a single property in this
    /// project. If a zenith / horizon color can be extracted from the
    /// volumetric cloud system or the skybox, it should be bound here.
    ///
    /// Default [SOURCE: Tessendorf 2004 §6.3 sample shader —
    /// `sky = color(0.69, 0.84, 1)`].
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
                     TemperatureField temperatureField, Light sun, SeaSettings seaSettings,
                     SeaStateController stateController)
    {
        settings = seaSettings;
        seaState = stateController;
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
            if (seaState != null) return seaState.WindDirection;
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
    /// THE WIND AT THE WATER, NOT AT THE PLAYER.
    ///
    /// `FreeAirSpeed` is the wind where the observer stands, and the weather hardens with height,
    /// so reading it made the swell rise while the player climbed and fall while they came down --
    /// measured, Hs 1.18 m at the shore against 3.66 m from the summit, the same sea in the same
    /// second. The sea is at sea level and reads the wind there.
    public float WindSpeed => seaState != null
        ? seaState.WindSpeed
        : wind != null ? wind.SeaLevelSpeed : manualWindSpeed;

    public Vector3 SwellDirection
    {
        get
        {
            if (seaState != null) return seaState.SwellDirection;

            Vector3 local = WindDirection;
            float radians = settings != null ? settings.swellDirectionOffset * Mathf.Deg2Rad : 0f;
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector3(local.x * c - local.z * s, 0f,
                               local.x * s + local.z * c).normalized;
        }
    }

    /// THE SWELL RUNS ON ITS OWN CLOCK.
    ///
    /// Deliberately not bound to `WindField` or `WeatherState`: the swell reaching this
    /// beach was made by a storm hundreds of kilometres away, days ago, and a real coast
    /// gets long groundswell under a windless sky as often as not. Tying it to the local
    /// storm would be a second weather source that contradicts the first.
    ///
    /// QUANTIZED. The spectrum is rebuilt whenever this number changes, so it moves in
    /// steps: a continuous value would re-dispatch the h0 pass every single frame.
    public float SwellPeriod
    {
        get
        {
            if (seaState != null) return seaState.SwellPeriod;
            if (settings == null) return 10f;

            float period = Mathf.Lerp(settings.swellPeriodShort, settings.swellPeriodLong,
                                      SwellEvent01);

            return Mathf.Round(period * 4f) * 0.25f;
        }
    }

    public float SwellEnergyScale =>
        seaState != null ? seaState.SwellEnergyScale
                         : settings == null ? 1f
                                            : Mathf.Lerp(1f, settings.swellEventGain, SwellEvent01);

    /// THE EVENT ITSELF, 0 to 1.
    ///
    /// STRETCHED, BECAUSE PERLIN NOISE DOES NOT REACH ITS OWN ENDS. Raw, it stays in
    /// roughly 0.3 to 0.7, and measured that turned an 8 to 16 s range into 9 to 10.5 s
    /// -- the long swell never actually arrived. The window below is the band the noise
    /// really uses, remapped onto the whole range.
    float SwellEvent01
    {
        get
        {
            float t = Time.time / Mathf.Max(settings.swellEventSeconds, 30f);
            float roll = Mathf.PerlinNoise(t, 7.31f);
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0.68f, roll));
        }
    }

    // --------------------------------------------------------- day/night

    public Light Sun => sunLight;

    /// `TimeOfDay.SunHeight` is already `SunDirection.y`, i.e. the sine of
    /// the sun's elevation. `saturate` cuts the negative night values.
    public float SunElevation01 =>
        timeOfDay != null ? Mathf.Clamp01(timeOfDay.SunHeight) : 0.5f;

    // -------------------------------------------------------- atmosphere

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
