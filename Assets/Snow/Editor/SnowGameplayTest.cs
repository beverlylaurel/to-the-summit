// Measures gameplay-side API functions — sample decoding, footstep surface selection,
// speed modifiers, particle counts, and sampling window mapping.
// Invoked by: Menu — To The Summit/Snow/Gameplay Test.

using System.Text;
using UnityEditor;
using UnityEngine;

public static class SnowGameplayTest
{
    [MenuItem("To The Summit/Snow/Gameplay Test", false, 58)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Snow — Gameplay Test");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = DecodeTest(r);
        ok &= FootstepTest(r);
        ok &= SpeedTest(r);
        ok &= PuffTest(r);
        ok &= WindowTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "RESULT: PASSED — all tests completed successfully."
                        : "RESULT: FAILED — see above for details.");
        return r.ToString();
    }

    static bool DecodeTest(StringBuilder r)
    {
        r.AppendLine("## Sample Decoding (spec §19)");

        SnowSample s = SnowSampler.Decode(new Color(0.02f, 0.10f, 0.3f, 0f),
                                          new Color(0.05f, 0.01f, 0f, 0f));

        bool depth = Mathf.Abs(s.Depth - 0.16f) < 1e-4f;
        bool sink = Mathf.Abs(s.SinkDepth - 0.05f) < 1e-4f;
        bool density = Mathf.Abs(s.Density01 - 0.10f) < 1e-4f;
        bool wet = Mathf.Abs(s.Wetness - 0.3f) < 1e-4f;

        bool all = depth && sink && density && wet;

        r.AppendLine("  [" + M(all) + "] SWE 0.020 / rhoN 0.10 / carve 5 cm / rim 1 cm -> " +
                     "depth " + (s.Depth * 100f).ToString("0.0") + " cm (expected 16.0), " +
                     "sink " + (s.SinkDepth * 100f).ToString("0.0") + " cm, " +
                     "density " + s.Density01.ToString("0.00") + ", " +
                     "wetness " + s.Wetness.ToString("0.00"));

        SnowSample deep = SnowSampler.Decode(new Color(0.005f, 0.10f, 0f, 0f),
                                             new Color(0.50f, 0f, 0f, 0f));

        bool clamped = deep.Depth >= 0f;
        all &= clamped;
        r.AppendLine("  [" + M(clamped) + "] Over-carved depth        " +
                     deep.Depth.ToString("0.000") + " m  (cannot be negative)");

        return all;
    }

    static bool FootstepTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Footstep Surface Selection (spec §19.1)");

        (float depth, float density, SnowFootstepSurface want, string note)[] cases =
        {
            (0.010f, 0.10f, SnowFootstepSurface.None,    "under 2 cm — fallback terrain audio"),
            (0.019f, 0.90f, SnowFootstepSurface.None,    "just below threshold"),
            (0.021f, 0.90f, SnowFootstepSurface.Packed,  "shallow + packed"),
            (0.050f, 0.56f, SnowFootstepSurface.Packed,  "above density threshold"),
            (0.050f, 0.54f, SnowFootstepSurface.Shallow, "below density threshold"),
            (0.079f, 0.20f, SnowFootstepSurface.Shallow, "just below 8 cm"),
            (0.200f, 0.29f, SnowFootstepSurface.Powder,  "deep + loose powder"),
            (0.200f, 0.31f, SnowFootstepSurface.Deep,    "deep + medium density"),
        };

        bool all = true;

        foreach ((float depth, float density, SnowFootstepSurface want, string note) c in cases)
        {
            var sample = new SnowSample
            {
                Depth = c.depth,
                Density01 = c.density,
                Valid = true,
            };

            SnowFootstepSurface got = SnowFootstepAudio.SelectSurface(sample);
            bool ok = got == c.want;
            all &= ok;

            r.AppendLine("  [" + M(ok) + "] " + (c.depth * 100f).ToString("0.0").PadLeft(5) +
                         " cm,  density " + c.density.ToString("0.00") + " -> " +
                         got.ToString().PadRight(8) + " (expected " + c.want + ")   " + c.note);
        }

        bool dry = !SnowFootstepAudio.IsWet(new SnowSample { Wetness = 0.54f, Valid = true });
        bool wet = SnowFootstepAudio.IsWet(new SnowSample { Wetness = 0.56f, Valid = true });

        all &= dry && wet;
        r.AppendLine("  [" + M(dry && wet) + "] Wet variant threshold   0.54 -> dry,  0.56 -> wet");

        bool invalid = SnowFootstepAudio.SelectSurface(default) == SnowFootstepSurface.None;
        all &= invalid;
        r.AppendLine("  [" + M(invalid) + "] Invalid sample            None  (no data -> no snow sound)");

        return all;
    }

    static bool SpeedTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Movement Speed Modifier (spec §19.2)");

        bool all = true;

        float none = SnowMovementModifier.SpeedFor(default);
        bool noneOk = Mathf.Approximately(none, 1f);
        all &= noneOk;
        r.AppendLine("  [" + M(noneOk) + "] No data             x" + none.ToString("0.000"));

        float shallow = SnowMovementModifier.SpeedFor(
            new SnowSample { Depth = 0.08f, Density01 = 0f, Valid = true });

        bool shallowOk = Mathf.Approximately(shallow, 1f);
        all &= shallowOk;
        r.AppendLine("  [" + M(shallowOk) + "] 8 cm loose          x" + shallow.ToString("0.000") +
                     "  (threshold 10 cm)");

        float deepLoose = SnowMovementModifier.SpeedFor(
            new SnowSample { Depth = 0.70f, Density01 = 0f, Valid = true });

        bool deepOk = Mathf.Abs(deepLoose - 0.55f) < 1e-4f;
        all &= deepOk;
        r.AppendLine("  [" + M(deepOk) + "] 70 cm powder        x" + deepLoose.ToString("0.000") +
                     "  (expected 0.550 — max slowdown)");

        float deepPacked = SnowMovementModifier.SpeedFor(
            new SnowSample { Depth = 0.70f, Density01 = 1f, Valid = true });

        bool packedOk = Mathf.Approximately(deepPacked, 1f);
        all &= packedOk;
        r.AppendLine("  [" + M(packedOk) + "] 70 cm packed        x" + deepPacked.ToString("0.000") +
                     "  (trail packing bonus)");

        return all;
    }

    static bool PuffTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Footstep Spindrift Puff (spec §19.3)");

        bool all = true;

        int shallow = SnowPuffEmitter.PuffCountFor(
            new SnowSample { Depth = 0.05f, Density01 = 0.1f, Valid = true });

        int packed = SnowPuffEmitter.PuffCountFor(
            new SnowSample { Depth = 0.30f, Density01 = 0.60f, Valid = true });

        int loose = SnowPuffEmitter.PuffCountFor(
            new SnowSample { Depth = 0.30f, Density01 = 0.10f, Valid = true });

        int deeper = SnowPuffEmitter.PuffCountFor(
            new SnowSample { Depth = 0.60f, Density01 = 0.10f, Valid = true });

        bool gates = shallow == 0 && packed == 0 && loose > 0;
        bool grows = deeper > loose;

        all &= gates && grows;

        r.AppendLine("  [" + M(gates) + "] Thresholds           5 cm -> " + shallow +
                     ",  30 cm packed -> " + packed + ",  30 cm loose -> " + loose);
        r.AppendLine("  [" + M(grows) + "] Scales with depth   30 cm -> " + loose +
                     ",  60 cm -> " + deeper);

        return all;
    }

    static bool WindowTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Readback Window (spec §19)");

        var center = new Vector2(-7494f, -4327.5f);
        const float AreaSize = 16f;
        const int Resolution = 1024;

        Vector2Int mid = SnowSampler.WindowOrigin(new Vector3(center.x, 0f, center.y),
                                                  center, AreaSize, Resolution);

        bool centered = mid.x == 512 - 32 && mid.y == 512 - 32;

        Vector2Int corner = SnowSampler.WindowOrigin(
            new Vector3(center.x + AreaSize, 0f, center.y + AreaSize),
            center, AreaSize, Resolution);

        bool clamped = corner.x == Resolution - 64 && corner.y == Resolution - 64;

        Vector2Int low = SnowSampler.WindowOrigin(
            new Vector3(center.x - AreaSize, 0f, center.y - AreaSize),
            center, AreaSize, Resolution);

        bool clampedLow = low.x == 0 && low.y == 0;

        bool all = centered && clamped && clampedLow;

        r.AppendLine("  [" + M(centered) + "] Center            " + mid + "  (expected (480, 480))");
        r.AppendLine("  [" + M(clamped) + "] Top-right corner   " + corner +
                     "  (expected (960, 960))");
        r.AppendLine("  [" + M(clampedLow) + "] Bottom-left corner " + low + "  (expected (0, 0))");

        return all;
    }

    static string M(bool ok) => ok ? "+" : "-";
}
