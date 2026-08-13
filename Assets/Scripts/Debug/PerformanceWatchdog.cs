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

/// Sampler'ı dinler, eşik aşımlarını uyarıya çevirir. Çizim yapmaz.
public class PerformanceWatchdog : MonoBehaviour
{
    [SerializeField] PerformanceSampler sampler;

    [Header("Eşikler")]
    [SerializeField] float warningFps = 60f;
    [SerializeField] float criticalFps = 30f;
    [Tooltip("1% low, ortalamanın bu oranının altına düşerse takılma uyarısı.")]
    [SerializeField] float stutterRatio = 0.5f;
    [Tooltip("Saniyede bu kadar MB managed bellek büyümesi = her karede çöp üretiliyor.")]
    [SerializeField] float gcGrowthMbPerSec = 1f;
    [SerializeField] int drawCallLimit = 2000;
    [SerializeField] int triangleLimit = 4000000;
    [SerializeField] long memoryLimitMb = 6000;
    [Tooltip("Koşul geçtikten sonra uyarı ekranda ne kadar kalsın.")]
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
            throw new InvalidOperationException($"{nameof(PerformanceWatchdog)}: {nameof(sampler)} atanmadı.");

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
            Raise("fps", $"FPS düşük: {s.AverageFps:F0}", AlertSeverity.Warning, now);

        if (s.AverageFps > 0f && s.OnePercentLowFps < s.AverageFps * stutterRatio)
            Raise("stutter", $"Takılma: 1% low {s.OnePercentLowFps:F0} / ort {s.AverageFps:F0}", AlertSeverity.Warning, now);

        if (s.ManagedGrowthMbPerSec > gcGrowthMbPerSec)
            Raise("gc", $"GC baskısı: {s.ManagedGrowthMbPerSec:F1} MB/sn", AlertSeverity.Warning, now);

        if (s.DrawCalls > drawCallLimit)
            Raise("draw", $"Draw call yüksek: {s.DrawCalls}", AlertSeverity.Warning, now);

        if (s.Triangles > triangleLimit)
            Raise("tri", $"Üçgen yüksek: {s.Triangles / 1000}k", AlertSeverity.Warning, now);

        if (s.TotalMemoryMb > memoryLimitMb)
            Raise("mem", $"Bellek kritik: {s.TotalMemoryMb} MB", AlertSeverity.Critical, now);

        Refresh(now);
    }

    void Raise(string key, string message, AlertSeverity severity, float now)
    {
        latest[key] = new PerformanceAlert(message, severity);
        lastSeen[key] = now;
    }

    /// Süresi dolan uyarıları düşürür. Kısa dalgalanmalarda yanıp sönmeyi önler.
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
