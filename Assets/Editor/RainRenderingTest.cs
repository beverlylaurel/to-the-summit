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
    const string TerrainShaderPath = "Assets/Shaders/MountainSurface.hlsl";
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
        ok &= WetSurfaceTest(report);

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
        string rendererSource = File.ReadAllText(RendererSourcePath);
        bool noEarlySubmission = !rendererSource.Contains("Graphics.RenderMesh");
        bool noEmptyPass = source.Contains("!PrecipitationRenderer.Active.CanDrawAfterClouds")
                        && rendererSource.Contains("if (!CanDrawAfterClouds) return;");

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
        report.AppendLine("  [" + Mark(noEmptyPass) + "] no uncullable pass when rain draws nothing");
        report.AppendLine("  [" + Mark(installed) + "] feature is installed in PC_Renderer");
        return afterClouds && renderGraph && noEarlySubmission && noEmptyPass && installed;
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
        bool projectedMotionAxis = source.Contains(
                                       "previousFreeDrift = freeDrift - classVelocity * exposure")
                                && source.Contains("trajectory = worldPos - previousWorldPos")
                                && source.Contains(
                                       "trajectory -= box * floor(trajectory / box + 0.5)")
                                && source.Contains("float3 projectedTrajectory = trajectory")
                                && source.Contains("projectedTrajectory / projectedLength")
                                && source.Contains("float rainLength = max(rainWidth, projectedLength);")
                                && !source.Contains("float3 up = fallAxis;");

        report.AppendLine("  [" + Mark(mask) + "] coverage is sampled from the mask");
        report.AppendLine("  [" + Mark(noRadianceCoverage) + "] radiance does not drive coverage");
        report.AppendLine("  [" + Mark(noRedundantAmbientSample) + "] no redundant ambient texture sample");
        report.AppendLine("  [" + Mark(contrastBlend) + "] rain uses additive physical contrast");
        report.AppendLine("  [" + Mark(fog) + "] rain uses the shared fog path");
        report.AppendLine("  [" + Mark(lod) + "] individual drops end at 18 m");
        report.AppendLine("  [" + Mark(noPerDropTurbulence) + "] no per-drop turbulence displacement");
        report.AppendLine("  [" + Mark(projectedMotionAxis)
                        + "] streak is built from the centre's swept trajectory");

        return mask && fog && lod && noRadianceCoverage && noRedundantAmbientSample
            && contrastBlend && noPerDropTurbulence && projectedMotionAxis;
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

        const float crosswind = 3f;
        float slowTilt = Mathf.Atan2(crosswind, drizzle) * Mathf.Rad2Deg;
        float fastTilt = Mathf.Atan2(crosswind, downpour) * Mathf.Rad2Deg;
        bool windLean = slowTilt > 35f && fastTilt > 18f && fastTilt < slowTilt;

        report.AppendLine("  [" + Mark(range) + "] terminal speed: "
            + drizzle.ToString("F2") + "-" + downpour.ToString("F2") + " m/s");
        report.AppendLine("  [" + Mark(readable) + "] speed at 18 m: "
            + slowAtLimit.ToString("F0") + "-" + fastAtLimit.ToString("F0") + " px/s");
        report.AppendLine("  [" + Mark(windLean) + "] 3 m/s crosswind tilt: "
            + fastTilt.ToString("F1") + "-" + slowTilt.ToString("F1") + " deg from vertical");

        return range && readable && windLean;
    }

    static bool WetSurfaceTest(StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("## Wet terrain and impact rings");

        float drizzleAfterMinute = TerrainSurface.AdvanceWetness(0f, 0.2f, 60f, 120f);
        float fullRainAfterEight = TerrainSurface.AdvanceWetness(0f, 1f, 8f, 120f);
        float dryAfterTwoMinutes = TerrainSurface.AdvanceWetness(1f, 0f, 120f, 120f);
        bool accumulation = drizzleAfterMinute > 0.7f
                         && Mathf.Abs(fullRainAfterEight - 0.6321f) < 0.001f;
        bool drying = Mathf.Abs(dryAfterTwoMinutes - 0.3679f) < 0.001f;

        string terrainShader = File.ReadAllText(TerrainShaderPath);
        bool filmGate = terrainShader.Contains("float rainFilm = smoothstep(0.015, 0.08, wet);")
                     && terrainShader.Contains("RainRings(ringLocal, _Time.y, _SurfaceRainIntensity)")
                     && terrainShader.Contains("* rainFilm * ringVisibility;");
        string terrainLighting = File.ReadAllText("Assets/Shaders/MountainSurface.shader");
        bool reflectiveFilm = terrainShader.Contains("surface.rainFilmNormalWS")
                           && terrainShader.Contains("surface.rainFilm =")
                           && terrainLighting.Contains("if (surface.rainFilm > 0.001h)")
                           && terrainLighting.Contains("AirColor(filmReflectionVector)")
                           && terrainLighting.Contains("const half WaterF0 = 0.0204h;")
                           && terrainLighting.Contains("* (1.0h - surface.snowMask);");
        string terrainSource = File.ReadAllText("Assets/Scripts/Terrain/TerrainSurface.cs");
        bool materialRainBinding = terrainSource.Contains(
            "material.SetFloat(RainIntensityId, rainIntensity);")
                                && !terrainSource.Contains(
            "Shader.SetGlobalFloat(RainIntensityId, rainIntensity);");
        string sharedRings = File.ReadAllText("Assets/Shaders/RainRings.hlsl");
        bool intensityControlsDensity = sharedRings.Contains("float eventRank =")
                                     && sharedRings.Contains("float eventWeight = smoothstep")
                                     && sharedRings.Contains("spatialSupport * eventWeight");
        bool temporalVariation = sharedRings.Contains("float eventCycle =")
                              && sharedRings.Contains("float2 eventSalt =")
                              && sharedRings.Contains("float2 eventKey = cell + eventSalt")
                              && sharedRings.Contains("float impactStrength = lerp")
                              && sharedRings.Contains("eventWeight * impactStrength");
        bool continuousCells = sharedRings.Contains("float2 RainRingCell(")
                            && sharedRings.Contains("cell + float2(side.x, 0.0)")
                            && sharedRings.Contains("cell + float2(0.0, side.y)")
                            && sharedRings.Contains("cell + side")
                            && sharedRings.Contains("float spatialSupport = 1.0 - smoothstep");
        int dryMaterial = terrainShader.IndexOf(
            "surface.smoothness = lerp(_RockSmoothness, sandSmoothness, sand);");
        int rainFilm = terrainShader.IndexOf(
            "surface.smoothness = lerp(surface.smoothness, _WetSmoothness, wet);");
        bool wetAfterMaterial = dryMaterial >= 0 && rainFilm > dryMaterial;

        report.AppendLine("  [" + Mark(accumulation) + "] rain accumulates to saturation: drizzle60="
                        + drizzleAfterMinute.ToString("F3") + ", full8="
                        + fullRainAfterEight.ToString("F3"));
        report.AppendLine("  [" + Mark(drying) + "] configured drying half-life curve: "
                        + dryAfterTwoMinutes.ToString("F3"));
        report.AppendLine("  [" + Mark(filmGate) + "] wet-film gate does not square rain intensity");
        report.AppendLine("  [" + Mark(reflectiveFilm)
                        + "] resolvable ground rings reflect the shared sky through a water film");
        report.AppendLine("  [" + Mark(materialRainBinding)
                        + "] rain intensity reaches the terrain material buffer");
        report.AppendLine("  [" + Mark(intensityControlsDensity)
                        + "] rain intensity controls ring event density");
        report.AppendLine("  [" + Mark(temporalVariation)
                        + "] each cell re-rolls impact position and strength every cycle");
        report.AppendLine("  [" + Mark(continuousCells)
                        + "] rings cross cell boundaries without square clipping");
        report.AppendLine("  [" + Mark(wetAfterMaterial) + "] rain film is applied after rock/sand selection");
        return accumulation && drying && filmGate && reflectiveFilm && materialRainBinding
            && intensityControlsDensity && temporalVariation && continuousCells && wetAfterMaterial;
    }

    static float TerminalVelocity(float diameterMm) =>
        9.65f - 10.3f * Mathf.Exp(-0.6f * diameterMm);

    static string Mark(bool value) => value ? "+" : "-";
}
