// Measures snow spray and saltation threshold — volume rate formula and
// threshold shift between loose and packed snow.
// Invoked by: Menu — To The Summit/Snow/Spray Test.

using System.Text;
using UnityEditor;
using UnityEngine;

public static class SnowSprayTest
{
    const string ComputePath = "Assets/Snow/Shaders/SnowfallSim.compute";

    const int Capacity = 4096;
    const int Stride = 12 * sizeof(float);

    const float GroundY = 100f;

    [MenuItem("To The Summit/Snow/Spray Test", false, 61)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Snow — Spray and Drift Test");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = SprayTest(r);
        ok &= DriftGateTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "RESULT: PASSED — all tests completed successfully."
                        : "RESULT: FAILED — see above for details.");
        return r.ToString();
    }

    static bool SprayTest(StringBuilder r)
    {
        r.AppendLine("## Spray Rate (spec §18.6)");
        r.AppendLine("  [i] [Reference: Sumner, O'Brien & Hodgins, CGF 1999]");

        const float Width = 0.11f;
        const float PerM3 = 40000f;

        var reference = new SnowSample
        {
            SinkDepth = 0.20f,
            Density01 = 0.20f,
            Valid = true,
        };

        float rate = SnowSprayController.RateFor(reference, 4f, Width, PerM3);

        bool sanity = Mathf.Abs(rate - 2816f) < 40f;

        r.AppendLine("  [" + (sanity ? "+" : "-") + "] Spec validation    " +
                     rate.ToString("0") + " particles/s  (spec ~2800, V̇ = 0.11 × 0.20 × 4 = " +
                     (Width * 0.20f * 4f).ToString("0.000") + " m³/s)");

        bool all = sanity;

        (string name, float sink, float density, float speed, bool wanted)[] cases =
        {
            ("walk (1.5 m/s)",       0.20f, 0.20f, 1.5f, false),
            ("run (4 m/s)",          0.20f, 0.20f, 4.0f, true),
            ("shallow snow (4 cm)",  0.04f, 0.20f, 4.0f, false),
            ("packed trail",         0.20f, 0.60f, 4.0f, false),
            ("no data",              0.20f, 0.20f, 4.0f, false),
        };

        for (int i = 0; i < cases.Length; i++)
        {
            (string name, float sink, float density, float speed, bool wanted) c = cases[i];

            var sample = new SnowSample
            {
                SinkDepth = c.sink,
                Density01 = c.density,
                Valid = i != cases.Length - 1,
            };

            float got = SnowSprayController.RateFor(sample, c.speed, Width, PerM3);
            bool ok = (got > 0f) == c.wanted;
            all &= ok;

            r.AppendLine("  [" + (ok ? "+" : "-") + "] " + c.name.PadRight(20) +
                         got.ToString("0").PadLeft(6) + " particles/s  (expected " +
                         (c.wanted ? "> 0" : "0") + ")");
        }

        float slow = SnowSprayController.RateFor(reference, 3f, Width, PerM3);
        float fast = SnowSprayController.RateFor(reference, 6f, Width, PerM3);

        var deeper = reference;
        deeper.SinkDepth = 0.40f;
        float deep = SnowSprayController.RateFor(deeper, 4f, Width, PerM3);

        bool scales = Mathf.Abs(fast / Mathf.Max(slow, 1f) - 2f) < 0.01f &&
                      Mathf.Abs(deep / Mathf.Max(rate, 1f) - 2f) < 0.01f;

        all &= scales;

        r.AppendLine("  [" + (scales ? "+" : "-") + "] Speed and depth scaling  3->6 m/s: " +
                     slow.ToString("0") + " -> " + fast.ToString("0") +
                     ",  20->40 cm: " + rate.ToString("0") + " -> " + deep.ToString("0") +
                     "  (both linear)");

        return all;
    }

    static bool DriftGateTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Drift Trigger (spec §18.7 — identical to §18.1 threshold)");

        float calm = SnowDriftVfxController.DriftActiveFor(5f, 0.9f);
        float onset = SnowDriftVfxController.DriftActiveFor(7f, 0.9f);
        float windy = SnowDriftVfxController.DriftActiveFor(12f, 0.9f);

        float packedWindy = SnowDriftVfxController.DriftActiveFor(12f, 0.05f);
        float packedModerate = SnowDriftVfxController.DriftActiveFor(9f, 0.05f);

        bool gated = calm <= 0f && onset > 0f && windy > onset &&
                     packedModerate <= 0f && packedWindy > 0f && packedWindy < windy * 0.5f;

        r.AppendLine("  [" + (gated ? "+" : "-") + "] Threshold          5 m/s -> " +
                     calm.ToString("0.00") + ",  7 m/s -> " + onset.ToString("0.00") +
                     ",  12 m/s -> " + windy.ToString("0.00"));

        r.AppendLine("  [" + (gated ? "+" : "-") + "] Sintering          packed snow 9 m/s -> " +
                     packedModerate.ToString("0.00") + ",  12 m/s -> " +
                     packedWindy.ToString("0.00") + "  (loose snow in same wind: " +
                     windy.ToString("0.00") + ")");

        r.AppendLine("  [i] Loose snow threshold 5 m/s, packed snow 11 m/s. Saltation and " +
                     "suspension share the same threshold from §18.1.");

        return gated;
    }
}
