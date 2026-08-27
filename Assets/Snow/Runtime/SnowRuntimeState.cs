// ROLE: the state the snow system announces to the outside. Read-only.
// The existing weather/audio/gameplay systems may read these.
// CALLED BY: SnowfallController and SnowManager write; the outside only reads.

/// IT PUBLISHES, IT DOES NOT APPLY (spec §3.3).
///
/// The snow system does not use `Stormness01` to CHANGE the fog, the sun intensity or
/// the ambient. It only publishes. The existing fog system may read it and make its own
/// decision. There will not be a single line inside the snow system writing to
/// `RenderSettings`, `VolumeProfile` or `Light.intensity`.
public static class SnowRuntimeState
{
    /// 0..1 the active snowfall intensity.
    public static float SnowfallIntensity01 { get; internal set; }

    /// 0..1 zeminde ne kadar kar var.
    public static float GroundCoverage01 { get; internal set; }

    /// 0..1 loose snow that can be drifted.
    public static float LooseSnowFraction { get; internal set; }

    /// 0..1 wind × precipitation.
    public static float Stormness01 { get; internal set; }

    /// Whether it is snowing (spec §3.4).
    public static bool IsSnowing { get; internal set; }

    /// THE RAIN'S WEIGHT. 1 = rain at full strength, 0 = silenced.
    ///
    /// When the snow starts a ramp descends from 1 to 0; the snow intensity only starts
    /// rising after this REACHES ZERO. The two are never visible at the same time
    /// — a crossfade is not a soft transition but two precipitations laid on top of
    /// each other (`DECISIONS.md`).
    public static float RainWeight01 { get; internal set; } = 1f;

    /// It is reset when the game closes or the system is disabled — static fields live
    /// between Play sessions and hand out a stale value.
    internal static void Reset()
    {
        SnowfallIntensity01 = 0f;
        GroundCoverage01 = 0f;
        LooseSnowFraction = 0f;
        Stormness01 = 0f;
        IsSnowing = false;
        RainWeight01 = 1f;
    }
}
