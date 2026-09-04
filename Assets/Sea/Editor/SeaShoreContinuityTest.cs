// ROLE: guards the shore swash against one-frame motion and foam-brightness jumps.
// CALLED BY: menu - To The Summit/Sea/Test Shore Continuity.

using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SeaShoreContinuityTest
{
    const string ShaderPath = "Assets/Sea/Shaders/SeaLit.shader";

    [MenuItem("To The Summit/Sea/Test Shore Continuity", false, 82)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        const float uprush = 1f / 2.3f;
        const float period = 16.9f;
        const float frameDt = 1f / 120f;
        float phaseStep = frameDt / period;
        float maxFoamStep = 0f;
        float maxSurgeStep = 0f;

        for (int ri = 0; ri <= 1000; ri++)
        {
            float reach = ri / 1000f;

            for (float phase = 0f; phase < 1f; phase += phaseStep)
            {
                float next = Mathf.Repeat(phase + phaseStep, 1f);
                maxFoamStep = Mathf.Max(maxFoamStep,
                    Mathf.Abs(FoamCoverage(next, uprush, reach)
                            - FoamCoverage(phase, uprush, reach)));
                maxSurgeStep = Mathf.Max(maxSurgeStep,
                    Mathf.Abs(SeaManager.SeaSwashSurge(next, uprush)
                            - SeaManager.SeaSwashSurge(phase, uprush)));
            }
        }

        // A changing sea state must alter only the next clock increment. It must
        // not reinterpret all elapsed time using a new period.
        float clock = 0.42f;
        float before = clock;
        clock = SeaManager.SeaSwashClockStep(clock, frameDt, 123f,
                                             period, 0.31f, 1f);
        float normalClockStep = clock - before;
        before = clock;
        clock = SeaManager.SeaSwashClockStep(clock, frameDt, 123f + frameDt,
                                             5f, 0.31f, 1f);
        float changedPeriodStep = clock - before;

        string shader = File.ReadAllText(ShaderPath);
        bool shaderContract = shader.Contains("float leaveT = 0.5 - sin(asin(")
                           && shader.Contains("float residueBirth = 0.15625;")
                           && shader.Contains("float shoreFoam = band * max(fresh, residue);");

        bool foamContinuous = maxFoamStep < 0.01f;
        bool motionContinuous = maxSurgeStep < 0.01f;
        bool clockContinuous = normalClockStep > 0f && normalClockStep < 0.002f
                            && changedPeriodStep > 0f && changedPeriodStep < 0.005f;
        ok = shaderContract && foamContinuous && motionContinuous && clockContinuous;

        var report = new StringBuilder(512);
        report.AppendLine("# Sea Shore Continuity Test");
        report.AppendLine("  [" + Mark(foamContinuous) + "] max foam step at 120 FPS: "
                        + maxFoamStep.ToString("F6"));
        report.AppendLine("  [" + Mark(motionContinuous) + "] max surge step at 120 FPS: "
                        + maxSurgeStep.ToString("F6"));
        report.AppendLine("  [" + Mark(clockContinuous) + "] clock steps, normal / changed period: "
                        + normalClockStep.ToString("F6") + " / "
                        + changedPeriodStep.ToString("F6"));
        report.AppendLine("  [" + Mark(shaderContract) + "] shader uses the matched backwash inverse");
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    // Mirrors the scalar coverage part of SeaLit. Spatial breakup and lighting
    // only multiply this value; they cannot repair a temporal discontinuity here.
    static float FoamCoverage(float phase, float uprush, float reach)
    {
        float surge = SeaManager.SeaSwashSurge(phase, uprush);
        float fresh = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(surge - 0.30f, surge + 0.10f, reach));

        float leaveT = 0.5f
                     - Mathf.Sin(Mathf.Asin(Mathf.Clamp(2f * reach - 1f, -1f, 1f)) / 3f);
        float leavePhase = uprush + (1f - uprush) * leaveT;
        float since = Mathf.Repeat(phase - leavePhase, 1f);
        float residueGain = Mathf.Lerp(0.15625f, 0.55f,
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.08f, since)));
        float residue = residueGain * Mathf.Exp(-since * 2.4f);
        return Mathf.Max(fresh, residue);
    }

    static string Mark(bool value) => value ? "+" : "-";
}
