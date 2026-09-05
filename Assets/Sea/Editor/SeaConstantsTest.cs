// ROLE: verifies that SeaConstants.cs and SeaConstants.hlsl carry EXACTLY
// the same values (sea spec §0.10, Phase 0 acceptance criterion).
// CALLED BY: menu — To The Summit/Sea/Test Constant Parity.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// CONSTANT DRIFT IS SILENT. If the spectrum runs with one gamma on the GPU
/// and the breaking decision with another on the CPU, the symptom is "it
/// happens sometimes" and the screen cannot tell you which side is wrong.
///
/// A direct adaptation of `SnowConstantsTest`. Two lessons learned there
/// carried over:
///
/// 1. **The pairing table is maintained BY HAND.** Automatic name conversion
///    misleads: `GammaMild -> SEA_GAMMA_MILD` works but `FftSize ->
///    SEA_FFT_SIZE` and `FoamJThreshold -> SEA_FOAM_J_THRESHOLD` do not
///    follow the same rule.
/// 2. **An HLSL-only constant IS NOT AN ERROR.** A constant only the shader
///    reads does not need a CPU counterpart; demanding one kept the test
///    permanently red.
public static class SeaConstantsTest
{
    const string HlslPath = "Assets/Sea/Shaders/SeaConstants.hlsl";

    /// C# field name -> HLSL define name. A new constant has to be added
    /// here too; if it is not, the test goes red with "not in the table".
    static readonly (string csharp, string hlsl)[] Pairs =
    {
        ("G", "SEA_G"),
        ("TwoPi", "SEA_TWO_PI"),
        ("WaterIor", "SEA_WATER_IOR"),

        ("JonswapGamma", "SEA_JONSWAP_GAMMA"),
        ("JonswapSigmaLo", "SEA_JONSWAP_SIGMA_LO"),
        ("JonswapSigmaHi", "SEA_JONSWAP_SIGMA_HI"),

        ("HashPeriod", "SEA_HASH_PERIOD"),

        ("OffshoreRamp", "SEA_OFFSHORE_RAMP"),
        ("MinDepth", "SEA_MIN_DEPTH"),
        ("ShoreGeometryFadeDepth", "SEA_SHORE_GEOMETRY_FADE_DEPTH"),
        ("ShoreOpticalFadeDepth", "SEA_SHORE_OPTICAL_FADE_DEPTH"),
        ("ShoreOpticalMinPixels", "SEA_SHORE_OPTICAL_MIN_PIXELS"),
        ("ShoreEdgeNoise", "SEA_SHORE_EDGE_NOISE"),
        ("ChopFadeDepth", "SEA_CHOP_FADE_DEPTH"),
        ("GammaMild", "SEA_GAMMA_MILD"),
        ("GammaSteep", "SEA_GAMMA_STEEP"),
        ("BreakFoamGain", "SEA_BREAK_FOAM_GAIN"),

        ("FoamJThreshold", "SEA_FOAM_J_THRESHOLD"),
        ("FoamJRange", "SEA_FOAM_J_RANGE"),
        ("FoamDecay", "SEA_FOAM_DECAY"),
        ("FoamResidueDecay", "SEA_FOAM_RESIDUE_DECAY"),
        ("FoamResidueTransfer", "SEA_FOAM_RESIDUE_TRANSFER"),
        ("FoamWindDrift", "SEA_FOAM_WIND_DRIFT"),

        ("FftSize", "SEA_FFT_SIZE"),
        ("TierCount", "SEA_TIER_COUNT"),
    };

    /// Deliberately C#-only constants: the CPU computes with them and the shader
    /// never reads them, so a shader twin would be a value nothing can drift
    /// against. The mirror image of the HLSL-only rule above.
    static readonly string[] CsharpOnly = { "Sqrt2" };

    [MenuItem("To The Summit/Sea/Test Constant Parity", false, 80)]
    static void RunMenu() => Debug.Log(Run(out bool ok) + (ok ? "" : "\nPARITY BROKEN."));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder();
        ok = true;

        if (!File.Exists(HlslPath))
        {
            ok = false;
            return "SeaConstants.hlsl not found: " + HlslPath;
        }

        Dictionary<string, double> defines = ParseDefines(File.ReadAllText(HlslPath));

        // Read every C#-side constant by reflection — listing them by hand
        // would silently leave out a forgotten field.
        var csharp = new Dictionary<string, double>();
        foreach (FieldInfo f in typeof(SeaConstants).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!f.IsLiteral) continue;
            object v = f.GetRawConstantValue();
            csharp[f.Name] = v is int i ? i : Convert.ToDouble(v);
        }

        int matched = 0;

        foreach ((string cs, string hl) in Pairs)
        {
            if (!csharp.TryGetValue(cs, out double a))
            {
                report.AppendLine($"MISSING  C#   {cs}");
                ok = false;
                continue;
            }

            if (!defines.TryGetValue(hl, out double b))
            {
                report.AppendLine($"MISSING  HLSL {hl}");
                ok = false;
                continue;
            }

            // Relative tolerance: an absolute threshold misleads on small
            // numbers.
            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            double tolerance = scale > 0.0 ? scale * 1e-6 : 1e-12;

            if (Math.Abs(a - b) > tolerance)
            {
                report.AppendLine($"DIVERGED  {cs} = {a} , {hl} = {b}");
                ok = false;
                continue;
            }

            matched++;
        }

        // A C#-side constant missing from the table: either forgotten or dead.
        foreach (string name in csharp.Keys)
        {
            bool listed = false;
            foreach ((string cs, string _) in Pairs) if (cs == name) { listed = true; break; }
            foreach (string only in CsharpOnly) if (only == name) { listed = true; break; }
            if (!listed) { report.AppendLine($"NOT IN TABLE  C# {name}"); ok = false; }
        }

        // An HLSL-only constant is not an error — it is only counted.
        int hlslOnly = 0;
        foreach (string name in defines.Keys)
        {
            bool listed = false;
            foreach ((string _, string hl) in Pairs) if (hl == name) { listed = true; break; }
            if (!listed) hlslOnly++;
        }

        if (hlslOnly > 0)
            report.AppendLine($"Shader only: {hlslOnly} constants (expected).");

        report.Insert(0, ok
            ? $"Sea constant parity OK — {matched}/{Pairs.Length} pairs identical.\n"
            : $"Sea constant parity BROKEN — {matched}/{Pairs.Length} pairs matched.\n");

        return report.ToString();
    }

    static readonly Regex DefineLine =
        new(@"^\s*#define\s+(SEA_[A-Z0-9_]+)\s+(-?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?)",
            RegexOptions.Multiline | RegexOptions.Compiled);

    static Dictionary<string, double> ParseDefines(string text)
    {
        var map = new Dictionary<string, double>();

        foreach (Match m in DefineLine.Matches(text))
        {
            if (double.TryParse(m.Groups[2].Value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double value))
                map[m.Groups[1].Value] = value;
        }

        return map;
    }
}
