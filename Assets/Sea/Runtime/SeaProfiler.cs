// ROLE: measures and publishes the GPU cost of the sea simulation.
// CALLED BY: SeaSimulation (wraps its step).

using UnityEngine;
using UnityEngine.Profiling;

/// GPU TIME VIA `CustomSampler`, NOT VIA CPU TIME.
///
/// A `Stopwatch` does not measure a compute dispatch: `Dispatch` queues the
/// command and returns immediately, the GPU does the work later. What you
/// would measure is the driver's command-writing time, about a tenth of the
/// real cost.
///
/// `CustomSampler.Create(name, collectGpuData: true)` also collects the GPU
/// side, and `Recorder.gpuElapsedNanoseconds` gives the real duration.
///
/// **THE NUMBER LAGS BY ONE FRAME.** The value cannot be read before the
/// GPU finishes that frame; `Recorder` returns the previous frame's result.
/// On a sudden change (quality preset, turning the camera away) one frame
/// of stale value shows.
public sealed class SeaProfiler
{
    readonly CustomSampler sampler;
    readonly Recorder recorder;

    /// GPU time of the last frame (ms).
    public float GpuMs { get; private set; }

    /// WHETHER THE MEASUREMENT IS ACTUALLY ARRIVING.
    ///
    /// `Recorder.gpuElapsedNanoseconds` is only populated while the profiler
    /// is recording; otherwise it SILENTLY returns 0. Measured: with the
    /// profiler window closed in the editor all three quality presets showed
    /// 0.000 ms, and that read as "the sea is free".
    ///
    /// Do not look at that number without separating zero from "not measured".
    public bool Available { get; private set; }

    public SeaProfiler(string name)
    {
        sampler = CustomSampler.Create(name, true);
        recorder = sampler.GetRecorder();
        recorder.enabled = true;
    }

    public void Begin() => sampler.Begin();

    public void End()
    {
        sampler.End();

        long ns = recorder.gpuElapsedNanoseconds;

        // One populated sample means measurement works; later genuine zeros
        // (the sea is not visible) do not take that back.
        if (ns > 0) Available = true;

        GpuMs = ns * 1e-6f;

        SeaRuntimeState.SimulationGpuMs = GpuMs;
        SeaRuntimeState.GpuTimingAvailable = Available;
    }

    /// A frame where the sea is not visible. Nothing is measured, but the
    /// PUBLISHED value is cleared — otherwise the panel would keep showing
    /// the last visible frame's time and suggest "expensive even when
    /// hidden".
    public void Skipped()
    {
        GpuMs = 0f;
        SeaRuntimeState.SimulationGpuMs = 0f;
        SeaRuntimeState.GpuTimingAvailable = Available;
    }
}
