// Verifies that components and assets of the snow subsystem are connected properly.
// Invoked by: SnowTestRunner.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

public static class SnowWiringTest
{
    const string ShaderDir = "Assets/Snow/Shaders";
    const string RuntimeDir = "Assets/Snow/Runtime";
    const string EditorDir = "Assets/Snow/Editor";

    static readonly Dictionary<string, string> FunctionExceptions = new()
    {
        { "SnowLitFragment", "#pragma fragment entry" },
        { "SnowShadowFragment", "#pragma fragment entry" },
        { "SnowDepthNormalsFragment", "#pragma fragment entry" },
    };

    static readonly Dictionary<string, string> WriteExceptions = new()
    {
        { "_SnowDetailNormal", "stored on material; global broadcast in SnowManager" },
    };

    static readonly Dictionary<string, string> ReadExceptions = new()
    {
        { "_SnowLineY", "character shader's own line; user will add (spec §16.1)" },
        { "_SnowAccum", "character shader's own accumulation; user will add (spec §16.1)" },
    };

    public static string Run(out bool pass)
    {
        var r = new StringBuilder();
        pass = true;

        r.AppendLine("# Snow — Wiring Check");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        var shaderFiles = Directory.GetFiles(ShaderDir, "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".hlsl") || p.EndsWith(".shader") || p.EndsWith(".compute"))
            .ToArray();

        var allShaderText = new Dictionary<string, string>();

        foreach (string p in shaderFiles) allShaderText[p] = File.ReadAllText(p);

        foreach (string p in Directory.GetFiles("Assets/Shaders", "*.*")
                     .Where(p => p.EndsWith(".hlsl") || p.EndsWith(".shader")))
            allShaderText[p] = File.ReadAllText(p);

        pass &= Functions(r, shaderFiles, allShaderText);
        pass &= Uniforms(r, allShaderText);
        pass &= Components(r);
        pass &= Prose(r);

        r.AppendLine();
        r.AppendLine("RESULT: " + (pass ? "PASSED" : "FAILED"));
        return r.ToString();
    }

    static bool Functions(StringBuilder r, string[] shaderFiles,
                          Dictionary<string, string> allText)
    {
        r.AppendLine("## Defined but uncalled shader functions");

        var defined = new Dictionary<string, string>();

        var def = new Regex(@"^\s*(?:float|half|uint|int|bool|void)[0-9x]*\s+(Snow\w+|RNMBlend|Wyvill\w*)\s*\(",
                            RegexOptions.Multiline);

        foreach (string p in shaderFiles)
        foreach (Match m in def.Matches(allText[p]))
            defined[m.Groups[1].Value] = Path.GetFileName(p);

        var orphans = new List<string>();

        foreach (var kv in defined)
        {
            if (FunctionExceptions.ContainsKey(kv.Key)) continue;

            int callers = 0;

            foreach (var f in allText)
            {
                string body = def.Replace(f.Value, "");
                if (Regex.IsMatch(body, @"\b" + Regex.Escape(kv.Key) + @"\s*\(")) callers++;
            }

            if (callers == 0) orphans.Add(kv.Key + "  (" + kv.Value + ")");
        }

        r.AppendLine("  Defined functions        " + defined.Count);
        r.AppendLine("  [" + M(orphans.Count == 0) + "] Uncalled             " + orphans.Count);

        foreach (string o in orphans) r.AppendLine("      - " + o);

        r.AppendLine();
        return orphans.Count == 0;
    }

    static bool Uniforms(StringBuilder r, Dictionary<string, string> allText)
    {
        r.AppendLine("## Uniform read/write consistency");

        string ids = File.ReadAllText(Path.Combine(RuntimeDir, "SnowShaderIDs.cs"));

        var entries = Regex.Matches(ids, @"public static readonly int (\w+)\s*=\s*Shader\.PropertyToID\(""(\w+)""\)")
            .Select(m => (Field: m.Groups[1].Value, Name: m.Groups[2].Value))
            .ToArray();

        string runtime = string.Join("\n",
            Directory.GetFiles(RuntimeDir, "*.cs").Select(File.ReadAllText));
        string editor = string.Join("\n",
            Directory.GetFiles(EditorDir, "*.cs").Select(File.ReadAllText));
        string csharp = runtime + "\n" + editor;

        string shaders = string.Join("\n", allText.Values);

        var neverWritten = new List<string>();
        var neverRead = new List<string>();

        foreach (var e in entries)
        {
            bool written = Regex.IsMatch(csharp,
                @"Set(?:Global|Compute)\w*\([^)]*SnowShaderIDs\." + e.Field + @"\b")
                || Regex.IsMatch(csharp, @"SetComputeTextureParam\([^;]*SnowShaderIDs\." + e.Field + @"\b")
                || Regex.IsMatch(csharp, @"\.Set\w+\(\s*SnowShaderIDs\." + e.Field + @"\b")
                || Regex.IsMatch(csharp, @"""" + e.Name + @"""");

            bool read = shaders.Contains(e.Name);

            if (!written && !WriteExceptions.ContainsKey(e.Name))
                neverWritten.Add(e.Name + "  (SnowShaderIDs." + e.Field + ")");

            if (!read && !ReadExceptions.ContainsKey(e.Name))
                neverRead.Add(e.Name + "  (SnowShaderIDs." + e.Field + ")");
        }

        r.AppendLine("  Defined IDs              " + entries.Length);
        r.AppendLine("  [" + M(neverWritten.Count == 0) + "] Never WRITTEN        " + neverWritten.Count +
                     "   (shader silently reads zero)");

        foreach (string n in neverWritten) r.AppendLine("      - " + n);

        r.AppendLine("  [" + M(neverRead.Count == 0) + "] Never READ           " + neverRead.Count +
                     "   (dead ID)");

        foreach (string n in neverRead) r.AppendLine("      - " + n);

        r.AppendLine();
        return neverWritten.Count == 0 && neverRead.Count == 0;
    }

    static bool Components(StringBuilder r)
    {
        r.AppendLine("## Runtime component scene setup check");

        string setup = File.ReadAllText(Path.Combine(EditorDir, "SnowDebugWindow.cs"));

        var components = Directory.GetFiles(RuntimeDir, "*.cs")
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Where(n => File.ReadAllText(Path.Combine(RuntimeDir, n + ".cs"))
                            .Contains("class " + n + " : MonoBehaviour"))
            .ToArray();

        var userSide = new Dictionary<string, string>
        {
            { "SnowDeformer", "character foot/leg bones" },
            { "SnowCharacterAccumulator", "character mesh" },
            { "SnowHeatSource", "fire/heat objects" },
            { "SnowFootstepAudio", "footstep events" },
            { "SnowPuffEmitter", "footstep events" },
            { "SnowSprayController", "footstep events" },
            { "SnowMovementModifier", "movement logic" },
        };

        var missing = components
            .Where(n => !setup.Contains("<" + n + ">") && !userSide.ContainsKey(n))
            .ToList();

        r.AppendLine("  User-side components     " + userSide.Count +
                     "   (placed per design decisions)");

        r.AppendLine("  MonoBehaviour            " + components.Length);
        r.AppendLine("  [" + M(missing.Count == 0) + "] Missing in setup     " + missing.Count);

        foreach (string m in missing) r.AppendLine("      - " + m);

        r.AppendLine();
        return missing.Count == 0;
    }

    static bool Prose(StringBuilder r)
    {
        r.AppendLine("## Specification requirements");

        var checks = new (string Section, string What, string File, string Needle)[]
        {
            ("§8.3",  "Snow material Queue = Geometry+50",
             "Assets/Snow/Shaders/SnowLit.shader", "Geometry+50"),

            ("§15.2", "Compute passes disabled when dormant",
             "Assets/Snow/Runtime/SnowManager.cs", "if (IsDormant) return;"),

            ("§15.2", "Per-material properties in single CBUFFER",
             "Assets/Snow/Shaders/SnowLitInput.hlsl", "CBUFFER_START(UnityPerMaterial)"),

            ("§14.2", "Detail normals on snow mesh",
             "Assets/Snow/Shaders/SnowLitForwardPass.hlsl", "SnowApplyDetailNormals"),

            ("§14.2", "Detail normals on mountain snow layer",
             "Assets/Shaders/MountainSurface.hlsl", "SnowApplyDetailNormals"),

            ("§3.4",  "Rain muted during snowfall",
             "Assets/Scripts/Weather/PrecipitationRenderer.cs", "SnowRuntimeState.RainWeight01"),

            ("§13.2", "No distance displacement fade",
             "Assets/Snow/Shaders/SnowLit.shader", ""),

            ("§9.2",  "Depth capture shader Cull Off",
             "Assets/Snow/Shaders/Hidden_SnowCaptureDepth.shader", "Cull Off"),

            ("§16",   "Object snow in separate shader",
             "Assets/Snow/Shaders/SnowCoverObject.shader", "SnowCoverMask"),
        };

        int bad = 0;

        foreach (var c in checks)
        {
            bool ok;

            if (c.Needle.Length == 0)
            {
                string body = File.Exists(c.File) ? File.ReadAllText(c.File) : "";
                ok = !body.Contains("distanceFade") && !body.Contains("_DisplacementFade");
            }
            else
            {
                ok = File.Exists(c.File) && File.ReadAllText(c.File).Contains(c.Needle);
            }

            if (!ok) bad++;

            r.AppendLine("  [" + M(ok) + "] " + c.Section.PadRight(7) + c.What);
        }

        r.AppendLine();
        r.AppendLine("  Checked                  " + checks.Length + "   failed " + bad);
        r.AppendLine();

        return bad == 0;
    }

    static string M(bool ok) => ok ? "+" : "-";
}
