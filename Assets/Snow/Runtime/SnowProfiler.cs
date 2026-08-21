// ROL: kar sisteminin geçiş başına maliyetini ölçer (§11.2). Ölçmeden optimize etme.
// Çağıran: SnowManager (örnek adlarını kuyruğa yazıyor), SnowDebugWindow (tabloyu okur).

using UnityEngine;
using UnityEngine.Profiling;

[DisallowMultipleComponent]
public class SnowProfiler : MonoBehaviour
{
    /// Ölçülen geçişler. Ad CommandBuffer örnek adıyla BİREBİR aynı olmak zorunda.
    public static readonly string[] PassNames =
    {
        "Snow.Scroll",
        "Snow.Deformation",
        "Snow.Relax",
        "Snow.Accumulate",
        "Snow.Cascade",
    };

    Recorder[] recorders;

    /// GPU zamanlaması için Unity'nin GPU profillemesi açık olmalı; kapalıysa
    /// `gpuElapsedNanoseconds` sıfır döner ve tablo CPU sütununu gösterir.
    public bool HasGpuTiming { get; private set; }

    void OnEnable()
    {
        recorders = new Recorder[PassNames.Length];

        for (int i = 0; i < PassNames.Length; i++)
        {
            recorders[i] = Recorder.Get(PassNames[i]);
            recorders[i].enabled = true;

            // GPU zamanı ayrı bir bayrakla açılıyor; kapalıyken hiç toplanmıyor.
            recorders[i].CollectFromAllThreads();
        }
    }

    void OnDisable()
    {
        if (recorders == null) return;

        for (int i = 0; i < recorders.Length; i++)
            if (recorders[i] != null) recorders[i].enabled = false;

        recorders = null;
    }

    void LateUpdate()
    {
        if (recorders == null || recorders.Length == 0) return;

        HasGpuTiming = false;

        for (int i = 0; i < recorders.Length; i++)
            if (recorders[i].gpuElapsedNanoseconds > 0) HasGpuTiming = true;
    }

    /// Geçişin CPU süresi, milisaniye.
    public float CpuMilliseconds(int index)
    {
        if (recorders == null || index < 0 || index >= recorders.Length) return 0f;
        return recorders[index].elapsedNanoseconds * 1e-6f;
    }

    /// Geçişin GPU süresi, milisaniye. GPU profillemesi kapalıysa 0.
    public float GpuMilliseconds(int index)
    {
        if (recorders == null || index < 0 || index >= recorders.Length) return 0f;
        return recorders[index].gpuElapsedNanoseconds * 1e-6f;
    }

    /// §11.2'deki hedef bütçe, milisaniye. Ölçüm bunun üstüne çıkarsa tablo kırmızıya
    /// dönüyor — hedef, garanti değil.
    public static float BudgetMilliseconds(SnowQualityPreset quality)
    {
        switch (quality)
        {
            case SnowQualityPreset.Low: return 0.75f;
            case SnowQualityPreset.Medium: return 1.59f;
            default: return 2.60f;
        }
    }
}
