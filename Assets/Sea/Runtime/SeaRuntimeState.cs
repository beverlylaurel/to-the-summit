// ROLE: state the sea PUBLISHES. Other systems may read it; the sea
// applies none of it.
// CALLED BY: SeaManager (writes), HUD and audio systems (read).

/// THE SEA PUBLISHES, IT DOES NOT APPLY.
///
/// Spec §3.3. Nobody has to read these values; the sea writes them and
/// moves on. It never touches `RenderSettings`, `VolumeProfile` or
/// `Light.intensity` — the Phase 1 acceptance criterion verifies that with
/// a code search.
public static class SeaRuntimeState
{
    /// Significant wave height Hs (m). Mean of the highest third of the
    /// waves; the number oceanography uses to say how "big" a sea is.
    public static float SignificantWaveHeight { get; internal set; }

    /// Peak period Tp (s). The period carrying most of the spectrum's energy.
    public static float PeakPeriod { get; internal set; }

    /// Fraction of open water covered by whitecap foam.
    public static float WhitecapCoverage01 { get; internal set; }

    /// Current strength of the shore foam: the surge, 0 at the lowest point of
    /// the swash and 1 at the top of the run-up.
    public static float ShoreFoamIntensity01 { get; internal set; }

    /// How high the swash reaches above still water at its top (m). Stockdon's
    /// R2% for the current Hs, Tp and shore slope.
    public static float RunupHeight { get; internal set; }

    /// Whether the sea system is running. Stays false if `SeaManager`
    /// cannot find an `ISeaEnvironmentSource`.
    public static bool Active { get; internal set; }

    /// Whether the wave field was computed THIS frame. When the sea is not
    /// visible every compute pass is skipped (spec §15.2) and this goes
    /// false. The profiler's "cost while hidden" measurement reads it.
    public static bool SimulationActive { get; internal set; }

    /// GPU time of the last simulation step (ms). Written by `SeaProfiler`.
    public static float SimulationGpuMs { get; internal set; }

    /// Whether GPU timing is actually arriving. With the profiler not
    /// recording, `SimulationGpuMs` silently stays 0; without this flag
    /// "free" cannot be told apart from "not measured".
    public static bool GpuTimingAvailable { get; internal set; }
}
