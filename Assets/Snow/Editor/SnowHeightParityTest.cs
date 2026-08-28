// Verifies that GPU and CPU height evaluations produce identical results.
// Invoked by: Menu (To The Summit/Snow/Test Height Parity).

using System.Text;
using UnityEditor;
using UnityEngine;

public static class SnowHeightParityTest
{
    const int SampleCount = 512;
    const float ToleranceMeters = 0.001f;

    [MenuItem("To The Summit/Snow/Test Height Parity", false, 61)]
    static void RunMenu() => Debug.Log(Run(out bool ok) + (ok ? "" : "\nPARITY FAILED."));

    public static string Run(out bool ok)
    {
        ok = true;
        var report = new StringBuilder();

        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Snow/Shaders/SnowHeightProbe.compute");

        if (compute == null)
        {
            ok = false;
            return "SnowHeightProbe.compute not found.";
        }

        var keys = new[] { "_SnowDbgNoFbm", "_SnowDbgNoRipple",
                           "_SnowDbgNoSastrugi", "_SnowDbgNoDrift" };
        var oldKey = new float[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            oldKey[i] = Shader.GetGlobalFloat(keys[i]);
            Shader.SetGlobalFloat(keys[i], 0f);
        }

        Vector4 wd = Shader.GetGlobalVector("_SastrugiWindDir");
        Vector2 windDir = new(wd.x, wd.y);
        if (windDir.sqrMagnitude < 1e-6f)
        {
            windDir = Vector2.right;
            Shader.SetGlobalVector("_SastrugiWindDir", new Vector4(1f, 0f, 0f, 0f));
        }

        var rnd = new System.Random(20260827);

        var positions = new Vector2[SampleCount];
        var depths = new float[SampleCount];
        var exposure = new float[SampleCount];

        for (int i = 0; i < SampleCount; i++)
        {
            positions[i] = new Vector2((float)(rnd.NextDouble() * 2000.0 - 1000.0),
                                       (float)(rnd.NextDouble() * 2000.0 - 1000.0));
            depths[i] = (float)(rnd.NextDouble() * 0.8);
            exposure[i] = (float)rnd.NextDouble();
        }

        var bufPosition = new ComputeBuffer(SampleCount, sizeof(float) * 2);
        var bufDepth = new ComputeBuffer(SampleCount, sizeof(float));
        var bufExposure = new ComputeBuffer(SampleCount, sizeof(float));
        var bufResult = new ComputeBuffer(SampleCount, sizeof(float));

        bufPosition.SetData(positions);
        bufDepth.SetData(depths);
        bufExposure.SetData(exposure);

        int k = compute.FindKernel("KHeightProbe");
        compute.SetBuffer(k, "_ProbePositions", bufPosition);
        compute.SetBuffer(k, "_ProbeDepths", bufDepth);
        compute.SetBuffer(k, "_ProbeExposure", bufExposure);
        compute.SetBuffer(k, "_ProbeResult", bufResult);
        compute.SetInt("_ProbeCount", SampleCount);
        compute.Dispatch(k, (SampleCount + 63) / 64, 1, 1);

        var gpu = new float[SampleCount];
        bufResult.GetData(gpu);

        bufPosition.Release();
        bufDepth.Release();
        bufExposure.Release();
        bufResult.Release();

        for (int i = 0; i < keys.Length; i++)
            Shader.SetGlobalFloat(keys[i], oldKey[i]);

        float maxDeviation = 0f;
        int failedCount = 0;

        for (int i = 0; i < SampleCount; i++)
        {
            float cpu = SnowSurfaceHeight.Relief(positions[i], depths[i],
                                                 windDir, exposure[i]);
            float deviation = Mathf.Abs(cpu - gpu[i]);

            if (deviation > maxDeviation) maxDeviation = deviation;

            if (deviation > ToleranceMeters)
            {
                failedCount++;
                if (failedCount <= 5)
                    report.AppendLine($"MISMATCH {positions[i]} depth={depths[i]:F3} " +
                                     $"exposure={exposure[i]:F3} " +
                                     $"GPU={gpu[i]:F5} CPU={cpu:F5} " +
                                     $"delta={deviation * 1000f:F2} mm");
            }
        }

        ok = failedCount == 0;

        report.Insert(0, ok
            ? $"Height parity PASSED — {SampleCount} samples, max delta " +
              $"{maxDeviation * 1000f:F4} mm.\n"
            : $"Height parity FAILED — {failedCount}/{SampleCount} samples exceeded tolerance " +
              $"({ToleranceMeters * 1000f:F1} mm), max delta " +
              $"{maxDeviation * 1000f:F2} mm.\n");

        return report.ToString();
    }
}
