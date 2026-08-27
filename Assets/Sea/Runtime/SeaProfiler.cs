// ROL: deniz simulasyonunun GPU maliyetini olcup yayinlar.
// Cagiran: SeaSimulation (Adim icinde sarar).

using UnityEngine;
using UnityEngine.Profiling;

/// GPU SÜRESİ `CustomSampler` İLE, CPU SÜRESİYLE DEĞİL.
///
/// `Stopwatch` compute dispatch'i ölçmez: `Dispatch` komutu kuyruğa atıp
/// hemen dönüyor, GPU işi sonra yapıyor. Ölçülen sayı sürücünün komut
/// yazma süresi olur ve gerçek maliyetin onda biri çıkar.
///
/// `CustomSampler.Create(ad, collectGpuData: true)` GPU tarafını da
/// topluyor ve `Recorder.gpuElapsedNanoseconds` gerçek süreyi veriyor.
///
/// **SAYI BİR KARE GECİKMELİ.** GPU o kareyi bitirmeden değer okunamıyor;
/// `Recorder` bir önceki karenin sonucunu döndürüyor. Ani değişimde
/// (kalite kademesi, kamera çevirme) bir kare eski değer görünür.
public sealed class SeaProfiler
{
    readonly CustomSampler sampler;
    readonly Recorder recorder;

    /// Son karenin GPU süresi (ms).
    public float GpuMs { get; private set; }

    /// ÖLÇÜM GERÇEKTEN GELİYOR MU.
    ///
    /// `Recorder.gpuElapsedNanoseconds` yalnız Profiler kayıt yaparken
    /// dolu; kapalıyken SESSİZCE 0 dönüyor. Ölçüldü: editörde Profiler
    /// penceresi kapalıyken üç kalite kademesi de 0.000 ms gösterdi ve bu
    /// "deniz bedava" gibi okundu.
    ///
    /// Sıfır ile "ölçülemedi" ayrılmadan bu sayıya bakılmaz.
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

        // Bir kez bile dolu geldiyse ölçüm var demektir; sonraki gerçek
        // sıfırlar (deniz görünmüyor) bunu geri almıyor.
        if (ns > 0) Available = true;

        GpuMs = ns * 1e-6f;

        SeaRuntimeState.SimulationGpuMs = GpuMs;
        SeaRuntimeState.GpuTimingAvailable = Available;
    }

    /// Deniz görünmediği kare. Ölçüm yapılmıyor ama YAYINLANAN DEĞER
    /// sıfırlanıyor — yoksa panel son görünür karenin süresini gösterip
    /// "görünmezken de pahalı" yanılgısı yaratır.
    public void Skipped()
    {
        GpuMs = 0f;
        SeaRuntimeState.SimulationGpuMs = 0f;
        SeaRuntimeState.GpuTimingAvailable = Available;
    }
}
