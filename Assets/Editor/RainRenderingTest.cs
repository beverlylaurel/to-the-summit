// Guards the rain rendering contracts that previously regressed independently:
// physical motion, lighting-independent coverage, near-field LOD and shared fog.

using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RainRenderingTest
{
    const string DatabasePath = "Assets/Rain/RainStreakDatabase.asset";
    const string ShaderPath = "Assets/Shaders/Precipitation.shader";
    const string FeaturePath = "Assets/Scripts/Weather/PrecipitationRenderFeature.cs";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string RendererSourcePath = "Assets/Scripts/Weather/PrecipitationRenderer.cs";

    [MenuItem("To The Summit/Rain/Rendering Test", false, 41)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder(2048);
        report.AppendLine("# Rain Rendering Test");
        report.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        ok = DatabaseTest(report);
        ok &= ShaderContractTest(report);
        ok &= RenderOrderTest(report);
        ok &= MotionTest(report);

        report.AppendLine();
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static bool RenderOrderTest(StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("## Render order");

        string source = File.ReadAllText(FeaturePath);
        bool afterClouds = source.Contains("RenderPassEvent.AfterRenderingTransparents + 1");
        bool renderGraph = source.Contains("AddRasterRenderPass<PassData>")
                        && source.Contains("DrawAfterClouds(context.cmd)");
        bool noEarlySubmission = !File.ReadAllText(RendererSourcePath).Contains("Graphics.RenderMesh");

        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererPath);
        bool installed = false;
        if (renderer != null)
        {
            foreach (var feature in renderer.rendererFeatures)
                installed |= feature is PrecipitationRenderFeature;
        }

        report.AppendLine("  [" + Mark(afterClouds) + "] precipitation runs after cloud compositing");
        report.AppendLine("  [" + Mark(renderGraph) + "] RenderGraph pass submits the rain mesh");
        report.AppendLine("  [" + Mark(noEarlySubmission) + "] no normal transparent-queue submission");
        report.AppendLine("  [" + Mark(installed) + "] feature is installed in PC_Renderer");
        return afterClouds && renderGraph && noEarlySubmission && installed;
    }

    static bool DatabaseTest(StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("## Streak database");

        var database = AssetDatabase.LoadAssetAtPath<RainStreakDatabase>(DatabasePath);
        if (database == null)
        {
            report.AppendLine("  [-] Database asset is missing.");
            return false;
        }

        bool valid = database.Angles != null && database.Angles.Length == 5;
        int arrays = 0;

        if (valid)
        {
            foreach (var angle in database.Angles)
            {
                valid &= angle.Point != null && angle.Ambient != null && angle.Mask != null;
                valid &= angle.Point.Length == database.Sizes.Length;
                valid &= angle.Ambient.Length == database.Sizes.Length;
                valid &= angle.Mask.Length == database.Sizes.Length;

                if (!valid) break;

                for (int level = 0; level < database.Sizes.Length; level++)
                {
                    var ambient = angle.Ambient[level];
                    var mask = angle.Mask[level];
                    valid &= ambient != null && mask != null;
                    if (!valid) break;

                    valid &= ambient.width == mask.width;
                    valid &= ambient.height == mask.height;
                    valid &= ambient.depth == mask.depth;
                    arrays++;
                }
            }
        }

        report.AppendLine("  [" + Mark(valid) + "] lighting-independent masks: " + arrays);
        return valid;
    }

    static bool ShaderContractTest(StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("## Shader contracts");

        string source = File.ReadAllText(ShaderPath);
        bool mask = source.Contains("TEXTURE2D_ARRAY(_StreakMask)")
                 && source.Contains("float rainMask = saturate(maskStreak) * endFade;");
        bool fog = source.Contains("FogPath(_WorldSpaceCameraPos, IN.worldPos")
                && source.Contains("* fogTransmittance;");
        bool lod = source.Contains("smoothstep(10.0, 18.0, centerDistance)");
        bool noRadianceCoverage = !source.Contains("max(ambientStreak, pointStreak)");
        bool noRedundantAmbientSample = !source.Contains("TEXTURE2D_ARRAY(_StreakAmbient)");
        bool contrastBlend = source.Contains("Blend SrcAlpha One, Zero One")
                          && source.Contains("IN.ambientColor * (AmbientCollectionRatio - 1.0)")
                          && !source.Contains("maskStreak * IN.ambientColor");
        bool noPerDropTurbulence = !source.Contains("float3 Turbulence(")
                                && !source.Contains("velocityFluctuation");

        report.AppendLine("  [" + Mark(mask) + "] coverage is sampled from the mask");
        report.AppendLine("  [" + Mark(noRadianceCoverage) + "] radiance does not drive coverage");
        report.AppendLine("  [" + Mark(noRedundantAmbientSample) + "] no redundant ambient texture sample");
        report.AppendLine("  [" + Mark(contrastBlend) + "] rain uses additive physical contrast");
        report.AppendLine("  [" + Mark(fog) + "] rain uses the shared fog path");
        report.AppendLine("  [" + Mark(lod) + "] individual drops end at 18 m");
        report.AppendLine("  [" + Mark(noPerDropTurbulence) + "] no per-drop turbulence displacement");

        return mask && fog && lod && noRadianceCoverage && noRedundantAmbientSample
            && contrastBlend && noPerDropTurbulence;
    }

    static bool MotionTest(StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("## Physical motion bounds");

        float drizzle = TerminalVelocity(1f);
        float downpour = TerminalVelocity(5f);
        bool range = Mathf.Abs(drizzle - 4.00f) < 0.05f
                  && Mathf.Abs(downpour - 9.14f) < 0.05f
                  && downpour > drizzle;

        const float pixelsPerRadian = 935.31f; // 1080 px, 60 degree vertical FOV
        float slowAtLimit = pixelsPerRadian * drizzle / 18f;
        float fastAtLimit = pixelsPerRadian * downpour / 18f;
        bool readable = slowAtLimit > 200f && fastAtLimit > slowAtLimit;

        report.AppendLine("  [" + Mark(range) + "] terminal speed: "
            + drizzle.ToString("F2") + "-" + downpour.ToString("F2") + " m/s");
        report.AppendLine("  [" + Mark(readable) + "] speed at 18 m: "
            + slowAtLimit.ToString("F0") + "-" + fastAtLimit.ToString("F0") + " px/s");

        return range && readable;
    }

    static float TerminalVelocity(float diameterMm) =>
        9.65f - 10.3f * Mathf.Exp(-0.6f * diameterMm);

    static string Mark(bool value) => value ? "+" : "-";
}
