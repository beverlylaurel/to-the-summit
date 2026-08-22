// ROL: kar sisteminin GPU gecislerinin suresini olcer ve yayinlar (spec 15.1).
// Caginan: SnowDebugWindow.

using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/// OLCULMEDEN KABUL EDILMEZ (spec 15.1: "Mevcut oyunun frame butcesi ile
/// karsilastirmadan kabul etme"). Hedef: compute gecisleri toplami < 1.5 ms,
/// karsiz sahnede toplam < 0.05 ms.
///
/// Isaretciler SnowManager'in komut tamponunda aciliyor; burada yalniz
/// okunuyor.
[DisallowMultipleComponent]
public class SnowProfiler : MonoBehaviour
{
    /// SnowManager'in actigi ornekleyici adlari.
    public static readonly string[] MarkerNames =
    {
        "Kar.Gokyuzu",
        "Kar.Yakalama",
        "Kar.Iz",
        "Kar.Birikme",
        "Kar.Yagis",
    };

    readonly List<ProfilerRecorder> recorders = new();
    readonly float[] milliseconds = new float[MarkerNames.Length];

    public float TotalMilliseconds { get; private set; }

    public float MillisecondsFor(int index) =>
        index >= 0 && index < milliseconds.Length ? milliseconds[index] : 0f;

    void OnEnable()
    {
        recorders.Clear();

        foreach (string name in MarkerNames)
            recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Render, name, 15,
                                                    ProfilerRecorderOptions.Default));
    }

    void OnDisable()
    {
        foreach (ProfilerRecorder recorder in recorders) recorder.Dispose();
        recorders.Clear();
    }

    void LateUpdate()
    {
        TotalMilliseconds = 0f;

        for (int i = 0; i < recorders.Count; i++)
        {
            milliseconds[i] = Average(recorders[i]);
            TotalMilliseconds += milliseconds[i];
        }
    }

    /// Son on bes karenin ortalamasi. Tek kare okumak kare kare zipliyor ve
    /// okunamiyor.
    static float Average(ProfilerRecorder recorder)
    {
        if (!recorder.Valid || recorder.Count == 0) return 0f;

        double sum = 0.0;

        for (int i = 0; i < recorder.Count; i++)
            sum += recorder.GetSample(i).Value;

        // Nanosaniye -> milisaniye.
        return (float)(sum / recorder.Count * 1e-6);
    }
}
