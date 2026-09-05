using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class EnvironmentValidationTest
{
    [MenuItem("To The Summit/Validation/Test Environment Scenarios", false, 11)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        IReadOnlyList<EnvironmentValidationScenario> scenarios = EnvironmentValidationCatalog.All;
        var ids = new HashSet<string>();
        bool unique = true;
        bool ranges = true;

        foreach (EnvironmentValidationScenario scenario in scenarios)
        {
            unique &= ids.Add(scenario.id);
            ranges &= !string.IsNullOrWhiteSpace(scenario.id)
                   && scenario.hour >= 0f && scenario.hour < 24f
                   && scenario.storm >= 0f && scenario.storm <= 1f
                   && scenario.windSeverity >= 0f && scenario.windSeverity <= 1f
                   && scenario.swashPhase >= 0f && scenario.swashPhase < 1f
                   && scenario.fieldOfView >= 20f && scenario.fieldOfView <= 90f
                   && scenario.snowDepth >= 0f;
        }

        EnvironmentValidationScenario uprush = EnvironmentValidationCatalog.Find("coast-snow-uprush");
        EnvironmentValidationScenario backwash = EnvironmentValidationCatalog.Find("coast-snow-backwash");
        bool snowPair = uprush != null && backwash != null
                     && uprush.playerXZ == backwash.playerXZ
                     && Mathf.Approximately(uprush.seaTime, backwash.seaTime)
                     && Mathf.Approximately(uprush.snowDepth, backwash.snowDepth)
                     && !Mathf.Approximately(uprush.swashPhase, backwash.swashPhase);

        string runner = File.ReadAllText("Assets/Editor/Validation/EnvironmentValidationRunner.cs");
        string seaManager = File.ReadAllText("Assets/Sea/Runtime/SeaManager.cs");
        string seaSimulation = File.ReadAllText("Assets/Sea/Runtime/SeaSimulation.cs");
        string wind = File.ReadAllText("Assets/Scripts/Weather/WindField.cs");
        bool outputContract = runner.Contains("ScreenCapture.CaptureScreenshot")
                           && runner.Contains("report.md")
                           && runner.Contains("\"Temp\", \"Validation\", \"Environment\"")
                           && runner.Contains("SessionState.SetBool(RunningKey, true)");
        bool deterministicHooks = seaManager.Contains("EditorSwashPhaseOverride")
                               && seaManager.Contains("float t = SimulationTime")
                               && seaSimulation.Contains("EditorTimeOverride")
                               && wind.Contains("EditorTimeOverride");

        ok = scenarios.Count >= 7 && unique && ranges && snowPair
          && outputContract && deterministicHooks;

        var report = new StringBuilder();
        report.AppendLine("# Environment Validation Test");
        report.AppendLine($"  [{Mark(scenarios.Count >= 7)}] seven acceptance scenarios");
        report.AppendLine($"  [{Mark(unique)}] scenario ids are unique");
        report.AppendLine($"  [{Mark(ranges)}] authored values are inside safe ranges");
        report.AppendLine($"  [{Mark(snowPair)}] snow pair changes phase while holding the environment");
        report.AppendLine($"  [{Mark(outputContract)}] PNG plus Markdown report contract");
        report.AppendLine($"  [{Mark(deterministicHooks)}] sea, swash and wind clocks can be pinned in Editor");
        report.AppendLine(ok ? "RESULT: PASSED" : "RESULT: FAILED");
        return report.ToString();
    }

    static string Mark(bool value) => value ? "+" : "-";
}
