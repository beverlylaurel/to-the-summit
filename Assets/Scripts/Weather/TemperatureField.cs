using System;
using UnityEngine;

/// The air temperature. The fourth source: it stands alongside the weather, the wind and the clock.
///
/// The freezing level NOW DERIVES FROM HERE and goes to `AltitudeWeatherDriver`. It used to have
/// its own formula there ("reference elevation − storm drop + daytime rise") and that formula was
/// really an implicit temperature model: the consequences of temperature were computed without
/// temperature ever existing. Every new feature — breath, frost, chill, freezing — would have had
/// to invent its own temperature estimate; that is exactly what the architecture forbids.
///
/// The numbers DO NOT CHANGE the behaviour: the old formula's shifts in metres were expressed in
/// degrees. Multiplied by the 6.5 °C/km lapse rate the same elevations come out.
///
/// THE BASE TEMPERATURE WAS PULLED TOWARDS WINTER (2026-08-13). Precipitation was wanted to fall
/// as snow at every elevation. The only correct lever for that is here: the rain/snow boundary
/// derives from the freezing level and the freezing level from this number. Writing a second rule
/// on the surface saying "snow everywhere" would have inserted an independent source into the
/// atmosphere chain — the thing the architecture forbids.
///
/// The margin needed to cross the threshold was computed on paper: the sleet band runs to 220 m
/// above the freezing level and the snow line's irregularity moves it by ±110 m, so for FULL snow
/// at the ground the freezing level has to be at least 330 m below the ground. Including the noon
/// warming, at its warmest it comes to −211 m.
public class TemperatureField : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;

    [Tooltip("The base temperature at sea level (°C). It sets where the freezing level " +
             "will be. IT WAS −3: the freezing level stayed 462 m BELOW sea level, i.e. " +
             "the whole mountain was frozen and precipitation fell as snow at every " +
             "elevation. Greenery and RAIN were wanted at the start of the game; in that " +
             "setup both were impossible. With +7.8 the freezing level is 1200 m: the " +
             "plain (186 m) is +6.6 °C at noon and gets rain, snow starts about 1 km " +
             "above the camp at the foot, the summit is −29.3 °C (felt −38 °C with the " +
             "wind). A full storm lowers the freezing level by 500 m, so snow can fall " +
             "on the camp too. The number derives from the snow line: 1200 m × 6.5 °C/km. " +
             "Reasoning in DECISIONS.md → 'The plain's elevation'.")]
    [SerializeField] float seaLevelCelsius = 7.8f;

    [Tooltip("The lapse with altitude (°C / kilometre). The standard atmosphere's rate is 6.5.")]
    [SerializeField] float lapseRate = 6.5f;

    [Tooltip("What the noon warming adds (°C). Scaled by the day factor.")]
    [SerializeField] float daytimeWarming = 1.63f;

    [Tooltip("What a full storm subtracts (°C). A cold front pushes the freezing level down.")]
    [SerializeField] float stormCooling = 3.25f;

    [Tooltip("How much the wind lowers the felt temperature (°C per metre/second). " +
             "Real wind chill is not linear but it is close over this range; the point is " +
             "that the felt value is a SEPARATE number from the measured one.")]
    [SerializeField] float windChillPerSpeed = 0.45f;

    void OnEnable()
    {
        if (weather == null)
            throw new InvalidOperationException($"{nameof(TemperatureField)}: {nameof(weather)} is not assigned.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(TemperatureField)}: {nameof(wind)} is not assigned.");
        if (time == null)
            throw new InvalidOperationException($"{nameof(TemperatureField)}: {nameof(time)} is not assigned.");
    }

    public void Bind(WeatherState state, WindField field, TimeOfDay clock)
    {
        weather = state;
        wind = field;
        time = clock;
    }

    bool overrideActive;
    float overrideSeaLevelCelsius;

    /// DIAGNOSTIC OVERRIDE — the same pattern as `WindField.ApplyOverride`.
    ///
    /// It changes the sea-level temperature, and because `At` / `FeltAt` / `FreezingLevel`
    /// all three derive from it, the HUD, the freezing level, the snow line and the
    /// snowfall shift AT THE SAME TIME. Forcing a separate temperature on the snow system
    /// alone would produce the contradiction "the HUD says +8 °C while snow is falling".
    public void ApplyOverride(float seaLevelC)
    {
        overrideActive = true;
        overrideSeaLevelCelsius = seaLevelC;
    }

    public void ClearOverride() => overrideActive = false;

    public bool HasOverride => overrideActive;

    float SeaLevelC => overrideActive ? overrideSeaLevelCelsius : seaLevelCelsius;

    /// THERMAL INERTIA. The lagged form of `DayFactor`.
    ///
    /// The sun's heating is not instantaneous: the ground warms first and warms the air
    /// afterwards. In the real world the coldest moment of the day is SUNRISE — the sun is
    /// already up but the heat lost overnight has not come back yet; and the peak
    /// temperature is not at noon but a few hours after it.
    ///
    /// `DayFactor` was used directly: the temperature jumped several degrees in the second
    /// the sun touched the horizon.
    float warmth;

    bool warmthReady;

    /// The lag with which the warming follows the sun (seconds, game time).
    [Tooltip("The lag with which the air follows the sun. A large value: the morning is " +
             "colder, the evening stays milder.")]
    [SerializeField] float thermalLagSeconds = 2700f;

    void LateUpdate()
    {
        if (time == null) return;

        float target = time.DayFactor;

        // It settles on the target in the first frame: otherwise the scene would start from the
        // night temperature on every launch and warm up slowly.
        if (!warmthReady)
        {
            warmth = target;
            warmthReady = true;
            return;
        }

        warmth = Mathf.Lerp(warmth, target,
                            1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1f, thermalLagSeconds)));
    }

    /// The lagged day factor. Without `time` it falls back to the instantaneous value.
    float Warmth => warmthReady ? warmth : (time != null ? time.DayFactor : 0f);

    /// The measured air temperature at the given elevation (°C).
    public float At(float altitude) =>
        SeaLevelC
        - lapseRate * altitude * 0.001f
        + daytimeWarming * Warmth
        - stormCooling * weather.Precipitation;

    /// The felt temperature: the wind carries heat away from the skin, the thermometer does not
    /// see this. Chill, breath and later stamina will read this number.
    public float FeltAt(float altitude) =>
        At(altitude) - windChillPerSpeed * wind.Velocity.magnitude;

    /// The elevation where the temperature falls to zero (metres). The boundary where rain turns
    /// to snow comes from here. Because the lapse rate is constant, the inverse is solved in closed form.
    public float FreezingLevel =>
        (SeaLevelC
         + daytimeWarming * Warmth
         - stormCooling * weather.Precipitation) / (lapseRate * 0.001f);
}
