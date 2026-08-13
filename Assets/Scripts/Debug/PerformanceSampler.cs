using System;
using UnityEngine;
using UnityEngine.Profiling;

/// Tek bir ölçüm anının verisi. Sampler üretir, dinleyenler tüketir.
public readonly struct PerformanceSnapshot
{
    public readonly float InstantFps;
    public readonly float InstantMs;
    public readonly float AverageFps;
    public readonly float OnePercentLowFps;
    public readonly long TotalMemoryMb;
    public readonly long ManagedMemoryMb;
    public readonly float ManagedGrowthMbPerSec;
    public readonly int DrawCalls;
    public readonly int SetPassCalls;
    public readonly int Triangles;

    public PerformanceSnapshot(float instantMs, float averageMs, float onePercentLowFps,
        long totalMemoryMb, long managedMemoryMb, float managedGrowthMbPerSec,
        int drawCalls, int setPassCalls, int triangles)
    {
        InstantMs = instantMs;
        InstantFps = instantMs > 0f ? 1000f / instantMs : 0f;
        AverageFps = averageMs > 0f ? 1000f / averageMs : 0f;
        OnePercentLowFps = onePercentLowFps;
        TotalMemoryMb = totalMemoryMb;
        ManagedMemoryMb = managedMemoryMb;
        ManagedGrowthMbPerSec = managedGrowthMbPerSec;
        DrawCalls = drawCalls;
        SetPassCalls = setPassCalls;
        Triangles = triangles;
    }
}

/// Performans metriklerini toplar. Kimseyi tanımaz, sadece yayınlar.
public class PerformanceSampler : MonoBehaviour
{
    [SerializeField] float refreshInterval = 0.25f;
    [Tooltip("1% low hesabı için tutulan kare sayısı.")]
    [SerializeField] int sampleCapacity = 512;

    public event Action<PerformanceSnapshot> Sampled;
    public PerformanceSnapshot Current { get; private set; }

    float[] frameTimes;
    float[] sortBuffer;
    int writeIndex;
    int filled;
    float timer;
    long lastManagedBytes;
    float lastSampleTime;

    void Start()
    {
        lastManagedBytes = Profiler.GetMonoUsedSizeLong();
        lastSampleTime = Time.realtimeSinceStartup;
    }

    /// Play mode'da script derlenince domain reload olur ve Awake tekrar çalışmaz.
    /// Tamponların varlığı ve boyutu bu yüzden kullanım anında doğrulanır.
    void EnsureBuffers()
    {
        if (frameTimes == null || frameTimes.Length != sampleCapacity)
        {
            frameTimes = new float[sampleCapacity];
            writeIndex = 0;
            filled = 0;
        }

        if (sortBuffer == null || sortBuffer.Length != sampleCapacity)
            sortBuffer = new float[sampleCapacity];
    }

    void Update()
    {
        EnsureBuffers();

        float dt = Time.unscaledDeltaTime;

        frameTimes[writeIndex] = dt;
        writeIndex = (writeIndex + 1) % frameTimes.Length;
        if (filled < frameTimes.Length) filled++;

        timer += dt;
        if (timer < refreshInterval) return;
        timer = 0f;

        Current = Build(dt);
        Sampled?.Invoke(Current);
    }

    PerformanceSnapshot Build(float dt)
    {
        long managedBytes = Profiler.GetMonoUsedSizeLong();
        float now = Time.realtimeSinceStartup;
        float elapsed = Mathf.Max(0.0001f, now - lastSampleTime);

        // GC toplaması sonrası düşüş olur; sadece büyümeyi raporla
        float growthMbPerSec = Mathf.Max(0f, (managedBytes - lastManagedBytes) / (1024f * 1024f) / elapsed);

        lastManagedBytes = managedBytes;
        lastSampleTime = now;

        int drawCalls = 0, setPassCalls = 0, triangles = 0;
#if UNITY_EDITOR
        drawCalls = UnityEditor.UnityStats.drawCalls;
        setPassCalls = UnityEditor.UnityStats.setPassCalls;
        triangles = UnityEditor.UnityStats.triangles;
#endif

        return new PerformanceSnapshot(
            dt * 1000f,
            AverageMs(),
            OnePercentLowFps(),
            Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024),
            managedBytes / (1024 * 1024),
            growthMbPerSec,
            drawCalls, setPassCalls, triangles);
    }

    float AverageMs()
    {
        if (filled == 0) return 0f;

        float sum = 0f;
        for (int i = 0; i < filled; i++) sum += frameTimes[i];
        return sum / filled * 1000f;
    }

    /// En kötü %1 karenin ortalaması; takılmaları ortalama FPS gizler, bu gizlemez
    float OnePercentLowFps()
    {
        if (filled == 0) return 0f;

        Array.Copy(frameTimes, 0, sortBuffer, 0, filled);
        Array.Sort(sortBuffer, 0, filled);

        int worstCount = Mathf.Max(1, filled / 100);
        float sum = 0f;
        for (int i = filled - worstCount; i < filled; i++) sum += sortBuffer[i];

        return sum > 0f ? worstCount / sum : 0f;
    }
}
