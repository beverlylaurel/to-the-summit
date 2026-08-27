using System;
using System.Collections.Generic;
using UnityEngine;

public enum AlertSeverity { Warning, Critical }

public readonly struct PerformanceAlert
{
    public readonly string Message;
    public readonly AlertSeverity Severity;

    public PerformanceAlert(string message, AlertSeverity severity)
    {
        Message = message;
        Severity = severity;
    }
}

/// Listens to the sampler and turns threshold breaches into warnings. It draws nothing.
public class PerformanceWatchdog : MonoBehaviour
{
    [SerializeField] PerformanceSampler sampler;

    [Header("Thresholds")]
    [SerializeField] float warningFps = 60f;
    [SerializeField] float criticalFps = 30f;
    [Tooltip("A stutter warning if the 1% low falls below this fraction of the average.")]
    [SerializeField] float stutterRatio = 0.5f;
    [Tooltip("This many MB of managed memory growth per second = garbage produced every frame.")]
    [SerializeField] float gcGrowthMbPerSec = 1f;
    [SerializeField] int drawCallLimit = 2000;
    [SerializeField] int triangleLimit = 4000000;
    [SerializeField] long memoryLimitMb = 6000;
    [Tooltip("How long a warning stays on screen after the condition has passed.")]
    [SerializeField] float holdSeconds = 3f;

    readonly Dictionary<string, PerformanceAlert> latest = new();
    readonly Dictionary<string, float> lastSeen = new();
    readonly List<PerformanceAlert> visible = new();
    readonly List<string> expired = new();

    public IReadOnlyList<PerformanceAlert> Alerts => visible;

    public void Bind(PerformanceSampler source) => sampler = source;

    void OnEnable()
    {
        if (sampler == null)
            throw new InvalidOperationException($"{nameof(PerformanceWatchdog)}: {nameof(sampler)} is not assigned.");

        sampler.Sampled += Evaluate;
    }

    void OnDisable()
    {
        if (sampler != null) sampler.Sampled -= Evaluate;
    }

    void Evaluate(PerformanceSnapshot s)
    {
        float now = Time.realtimeSinceStartup;

        if (s.AverageFps < criticalFps)
            Raise("fps", $"FPS kritik: {s.AverageFps:F0}", AlertSeverity.Critical, now);
        else if (s.AverageFps < warningFps)
            Raise("fps", $"FPS low: {s.AverageFps:F0}", AlertSeverity.Warning, now);

        if (s.AverageFps > 0f && s.OnePercentLowFps < s.AverageFps * stutterRatio)
            Raise("stutter", $"Stutter: 1% low {s.OnePercentLowFps:F0} / avg {s.AverageFps:F0}", AlertSeverity.Warning, now);

        if (s.ManagedGrowthMbPerSec > gcGrowthMbPerSec)
            Raise("gc", $"GC pressure: {s.ManagedGrowthMbPerSec:F1} MB/s", AlertSeverity.Warning, now);

        if (s.DrawCalls > drawCallLimit)
            Raise("draw", $"Draw calls high: {s.DrawCalls}", AlertSeverity.Warning, now);

        if (s.Triangles > triangleLimit)
            Raise("tri", $"Triangles high: {s.Triangles / 1000}k", AlertSeverity.Warning, now);

        if (s.TotalMemoryMb > memoryLimitMb)
            Raise("mem", $"Bellek kritik: {s.TotalMemoryMb} MB", AlertSeverity.Critical, now);

        Refresh(now);
    }

    void Raise(string key, string message, AlertSeverity severity, float now)
    {
        latest[key] = new PerformanceAlert(message, severity);
        lastSeen[key] = now;
    }

    /// Drops warnings whose time has expired. Prevents flicker on short fluctuations.
    void Refresh(float now)
    {
        expired.Clear();
        foreach (var pair in lastSeen)
            if (now - pair.Value > holdSeconds)
                expired.Add(pair.Key);

        foreach (var key in expired)
        {
            lastSeen.Remove(key);
            latest.Remove(key);
        }

        visible.Clear();
        foreach (var alert in latest.Values)
            visible.Add(alert);

        visible.Sort((a, b) => b.Severity.CompareTo(a.Severity));
    }
}
