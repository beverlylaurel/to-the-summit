// ROLE: derives the snowfall from the existing precipitation intensity and publishes the state.
// CALLED BY: SnowManager (LateUpdate).

using UnityEngine;

/// ONE SOURCE, ONE INTENSITY (spec §17.2).
///
/// The VFX density and `_SnowfallSWERate` derive from the SAME `i01` value. Coming from
/// separate sources the symptom would be "heavy snow is falling but nothing accumulates"
/// and which of the two was wrong could not be told from the screen.
public sealed class SnowfallController
{
    public float SnowfallSweRate { get; private set; }
    public float FlakeRate { get; private set; }

    /// The grain's wetness — the VFX takes its terminal velocity and oscillation from it
    /// (spec §17.1). Because precipitation is decoupled from temperature the grain is always dry.
    public float Wetness { get; private set; }

    public void Reset()
    {
        SnowfallSweRate = 0f;
        FlakeRate = 0f;
        Wetness = 0f;
    }

    /// THE SNOW FRACTION IS AN INPUT, IT DOES NOT DERIVE FROM TEMPERATURE.
    ///
    /// `snowFraction01` is how much of the precipitation is snow: 1 all snow,
    /// 0 all rain, mixed in between. The default is 1 — the rule "if there is
    /// precipitation, snow falls".
    ///
    /// This decision used to be made by §3.4's temperature hysteresis and it was removed.
    /// Temperature WAS NOT PUT in its place: the decision comes from outside, so the weather
    /// system (or the F1 slider) drives whatever it wants and the snow system forces
    /// nobody.
    public void Tick(ISnowEnvironmentSource env, float snowFraction01)
    {
        float snowShare = Mathf.Clamp01(snowFraction01);

        // IF THERE IS PRECIPITATION THERE IS SNOW. No temperature gate.
        //
        // §3.4's hysteresis used to be here: below 0.5 °C snow, above 2.0 °C
        // rain. It was removed — the same rule as the one applied when the snow line was
        // removed: if it falls it is snow, and it settles.
        bool precipActive = env.PrecipKind != PrecipitationKind.None;

        // A HARD BOUNDARY: EITHER SNOW OR RAIN, NEVER BOTH.
        //
        // The share used to be SPLIT between the two (snow 0.5 → half snow, half rain) and
        // the reasoning was "because they sum to one they cannot overlap". That reasoning
        // was wrong: summing to one does not stop them BEING DRAWN AT THE SAME TIME, it
        // only draws both at half intensity. On screen snow and rain fell into each other
        // (the user reported it).
        //
        // In reality mixed precipitation (sleet) is its own phenomenon, not snow and rain
        // superposed. If we want it, it gets its own particle; for now, a threshold.
        //
        // The slider is now a SWITCH: 0.5 and above is snow, below is rain. The intensity is
        // NOT SPLIT — whichever wins takes all of the precipitation.
        bool karYagiyor = snowShare >= 0.5f;

        SnowRuntimeState.IsSnowing = precipActive && karYagiyor;

        SnowRuntimeState.RainWeight01 = precipActive && !karYagiyor ? 1f : 0f;

        SnowRuntimeState.SnowfallIntensity01 =
            SnowRuntimeState.IsSnowing ? env.PrecipIntensity01 : 0f;

        float i01 = SnowRuntimeState.SnowfallIntensity01;

        SnowfallSweRate = Mathf.Lerp(0f, SnowConstants.MaxSweRate, i01);
        FlakeRate = Mathf.Lerp(0f, SnowConstants.MaxFlakeRate, i01);

        // DRY SNOW. The wetness used to derive from the temperature; precipitation was
        // decoupled from temperature and it has no source left.
        Wetness = 0f;

        Shader.SetGlobalFloat(SnowShaderIDs.SnowfallSWERate, SnowfallSweRate);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowWetness, Wetness);
    }
}
