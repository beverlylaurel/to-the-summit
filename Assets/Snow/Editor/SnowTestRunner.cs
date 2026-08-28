// Runs snow subsystem tests without manual editor interaction and writes results to file.
// Invoked by: timestamp updates to `Logs/snow-test.request`.

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SnowTestRunner
{
    const string RequestPath = "Logs/snow-test.request";
    const string ResultPath = "Logs/snow-test.log";
    const string TracePath = "Logs/snow-test.trace";

    const double Interval = 1.0;

    static DateTime stamp;
    static double next;

    static SnowTestRunner()
    {
        Trace("runner loaded  cwd=" + Directory.GetCurrentDirectory() +
              "  request=" + Path.GetFullPath(RequestPath) +
              "  exists=" + File.Exists(RequestPath));

        stamp = Stamp();
        EditorApplication.update += Tick;

        if (stamp != DateTime.MinValue) EditorApplication.delayCall += RunAndClear;
    }

    static void Trace(string line)
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText(TracePath,
            DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine,
            new UTF8Encoding(false));
    }

    static DateTime Stamp() => File.Exists(RequestPath)
        ? File.GetLastWriteTimeUtc(RequestPath)
        : DateTime.MinValue;

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + Interval;

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        DateTime current = Stamp();
        if (current == stamp) return;

        stamp = current;
        if (current != DateTime.MinValue) RunAndClear();
    }

    static void RunAndClear()
    {
        Trace("RunAndClear invoked, request exists=" + File.Exists(RequestPath));
        if (!File.Exists(RequestPath)) return;

        string report;

        try
        {
            report = Run();
        }
        catch (Exception e)
        {
            report = "RUN CRASHED: " + e;
            Trace("crashed: " + e.Message);
        }

        Directory.CreateDirectory("Logs");
        File.WriteAllText(ResultPath, report, new UTF8Encoding(false));

        Trace("report written, " + report.Length + " characters");

        File.Delete(RequestPath);
        stamp = DateTime.MinValue;
    }

    static void AppendShaderMessages(StringBuilder r)
    {
        string[] paths =
        {
            "Assets/Snow/Shaders/SnowSim.compute",
            "Assets/Snow/Shaders/SnowfallSim.compute",
            "Assets/Snow/Editor/SnowTestKernels.compute",
        };

        bool any = false;

        foreach (string path in paths)
        {
            var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            if (cs == null) { r.AppendLine("MISSING: " + path); any = true; continue; }

            int count = ShaderUtil.GetComputeShaderMessageCount(cs);
            if (count == 0) continue;

            any = true;
            r.AppendLine("SHADER ERROR — " + path);

            foreach (var m in ShaderUtil.GetComputeShaderMessages(cs))
                r.AppendLine("  " + m.file + "(" + m.line + "): " + m.message);
        }

        foreach (string path in AssetDatabase.FindAssets("t:Shader",
                     new[] { "Assets/Snow/Shaders", "Assets/Shaders" }))
        {
            string file = AssetDatabase.GUIDToAssetPath(path);
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(file);

            if (sh == null || ShaderUtil.GetShaderMessageCount(sh) == 0) continue;

            any = true;
            r.AppendLine("SHADER ERROR — " + file);

            foreach (var m in ShaderUtil.GetShaderMessages(sh))
                r.AppendLine("  " + m.file + "(" + m.line + "): " + m.message);
        }

        if (any) { r.AppendLine(new string('-', 72)); r.AppendLine(); }
    }

    static string Run()
    {
        var r = new StringBuilder(16384);

        r.AppendLine("SNOW TESTS — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine("Unity " + Application.unityVersion);
        r.AppendLine(new string('=', 72));
        r.AppendLine();

        AppendShaderMessages(r);

        bool all = true;

        Trace("test suites starting");

        all &= Section(r, "Project Check", () => SnowProjectCheck.Run(),
                       out_ => out_.Contains("RESULT: READY to proceed"));

        all &= Section(r, "Constant Parity", () => SnowConstantsTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Compute Global", () => SnowComputeGlobalTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Ground Source", () => SnowGroundTest.Run(),
                       out_ => out_.Contains("RESULT: PASSED"));

        all &= Section(r, "Wiring Check", () => SnowWiringTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Scroll", () => SnowScrollTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Trail", () => SnowTrailTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Accumulation", () => SnowAccumulationTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Shading", () => SnowShadingTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Coverage", () => SnowCoverTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Snowfall", () => SnowfallTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Gameplay", () => SnowGameplayTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Persistence", () => SnowPersistenceTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Wind", () => SnowWindTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        all &= Section(r, "Spray", () => SnowSprayTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[FAILED]"));

        r.AppendLine(new string('=', 72));
        r.AppendLine(all ? "OVERALL RESULT: PASSED" : "OVERALL RESULT: FAILED");

        return r.ToString();
    }

    static string Mark(bool ok) => ok ? "\n[PASSED]" : "\n[FAILED]";

    static bool Section(StringBuilder r, string title, Func<string> body, Func<string, bool> verdict)
    {
        Trace("suite: " + title);
        r.AppendLine("--- " + title + " " + new string('-', Math.Max(0, 66 - title.Length)));

        string text;

        try
        {
            text = body();
        }
        catch (Exception e)
        {
            r.AppendLine("EXCEPTION: " + e.GetType().Name + ": " + e.Message);
            r.AppendLine(e.StackTrace);
            r.AppendLine();
            return false;
        }

        r.AppendLine(text);
        r.AppendLine();

        return verdict(text);
    }
}
