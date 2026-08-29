// Verifies that SnowConstants.cs and SnowConstants.hlsl have IDENTICAL values (spec §0.10).
// Invoked by: Menu — To The Summit/Snow/Test Constant Parity, and SnowProjectCheck.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class SnowConstantsTest
{
    const string HlslPath = "Assets/Snow/Shaders/SnowConstants.hlsl";

    static readonly (string csharp, string hlsl)[] Pairs =
    {
        ("RhoMin", "SNOW_RHO_MIN"),
        ("RhoMax", "SNOW_RHO_MAX"),
        ("RhoWater", "SNOW_RHO_WATER"),
        ("SnapQuads", "SNOW_SNAP_QUADS"),
        ("EdgeFadeStart", "SNOW_EDGE_FADE_START"),
        ("MinVisibleHeight", "SNOW_MIN_VISIBLE_HEIGHT"),
        ("LooseN", "SNOW_LOOSE_N"),
        ("PackedN", "SNOW_PACKED_N"),
        ("PackedSinkScale", "SNOW_PACKED_SINK_SCALE"),
        ("LateralEscape", "SNOW_LATERAL_ESCAPE"),
        ("RimVelocityBias", "SNOW_RIM_VELOCITY_BIAS"),
        ("RimBlurTexels", "SNOW_RIM_BLUR_TEXELS"),
        ("WindFill", "SNOW_WIND_FILL"),
        ("SettleTau", "SNOW_SETTLE_TAU"),
        ("DisturbTau", "SNOW_DISTURB_TAU"),
        ("MeltDdf", "SNOW_MELT_DDF"),
        ("DriftBias", "SNOW_DRIFT_BIAS"),
        ("RainMeltBoost", "SNOW_RAIN_MELT_BOOST"),
        ("SweMax", "SNOW_SWE_MAX"),
        ("MaxSweRate", "SNOW_MAX_SWE_RATE"),
        ("MaxFlakeRate", "SNOW_MAX_FLAKE_RATE"),
        ("SkyAreaSize", "SNOW_SKY_AREA_SIZE"),
        ("SkyMoveThreshold", "SNOW_SKY_MOVE_THRESHOLD"),
        ("WindShadowC", "SNOW_WINDSHADOW_C"),
        ("ErosionRate", "SNOW_EROSION_RATE"),
        ("DriftU10Loose", "SNOW_DRIFT_U10_LOOSE"),
        ("DriftU10Packed", "SNOW_DRIFT_U10_PACKED"),
        ("MaxHeatSources", "SNOW_MAX_HEAT_SOURCES"),
        ("HeatMeltRate", "SNOW_HEAT_MELT_RATE"),
        ("HeatWetRate", "SNOW_HEAT_WET_RATE"),
        ("TWarm", "SNOW_T_WARM"),
        ("TCool", "SNOW_T_COOL"),
        ("TFreeze", "SNOW_T_FREEZE"),
        ("CrustGain", "SNOW_CRUST_GAIN"),
        ("CrustWindGain", "SNOW_CRUST_WIND_GAIN"),
        ("CrustMeltTau", "SNOW_CRUST_MELT_TAU"),
        ("CrustBury", "SNOW_CRUST_BURY"),
        ("CrustSolid", "SNOW_CRUST_SOLID"),
        ("CrustBreakPen", "SNOW_CRUST_BREAK_PEN"),
        ("CrustSinkScale", "SNOW_CRUST_SINK_SCALE"),
        ("SastrugiWindTau", "SNOW_SASTRUGI_WIND_TAU"),
        ("SuspScaleH", "SNOW_SUSP_SCALE_H"),
        ("SuspAlphaBase", "SNOW_SUSP_ALPHA_BASE"),
        ("SuspMaxHeight", "SNOW_SUSP_MAX_HEIGHT"),
        ("SprayParticlesPerM3", "SNOW_SPRAY_PARTICLES_PER_M3"),
        ("EdgeFadeRange", "SNOW_EDGE_FADE_RANGE"),

        // --- Snow surface geometry (C# mirror) ---
        ("TerrainVertexSpacing", "SNOW_TERRAIN_VERTEX_SPACING"),
        ("TessMinWavelength", "SNOW_TESS_MIN_WAVELENGTH"),
        ("BedformDepthFrac", "SNOW_BEDFORM_DEPTH_FRAC"),
        ("FbmAmp", "SNOW_FBM_AMP"),
        ("FbmScale", "SNOW_FBM_SCALE"),
        ("FbmGain", "SNOW_FBM_GAIN"),
        ("RippleAmp", "SNOW_RIPPLE_AMP"),
        ("RippleLength", "SNOW_RIPPLE_LENGTH"),
        ("SastrugiHeight", "SNOW_SASTRUGI_HEIGHT"),
        ("SastrugiLength", "SNOW_SASTRUGI_LENGTH"),
        ("SastrugiWidth", "SNOW_SASTRUGI_WIDTH"),
        ("DriftHeight", "SNOW_DRIFT_HEIGHT"),
        ("DriftLength", "SNOW_DRIFT_LENGTH"),
        ("DriftWidth", "SNOW_DRIFT_WIDTH"),
        ("GroupSize", "SNOW_GROUP_SIZE"),
    };

    static readonly string[] CsharpOnly = { "SnapQuadsInt", "MeshBoundsHeight", "ReposeIterations" };

    [MenuItem("To The Summit/Snow/Test Constant Parity", false, 60)]
    static void RunMenu() => Debug.Log(Run(out bool ok) + (ok ? "" : "\nPARITY FAILED."));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder();
        ok = true;

        if (!File.Exists(HlslPath))
        {
            ok = false;
            return "SnowConstants.hlsl not found: " + HlslPath;
        }

        Dictionary<string, double> defines = ParseDefines(File.ReadAllText(HlslPath));

        var csharp = new Dictionary<string, double>();
        foreach (FieldInfo f in typeof(SnowConstants).GetFields(BindingFlags.Public | BindingFlags.Static))
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

            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            double tolerance = scale > 0.0 ? scale * 1e-6 : 1e-12;

            if (Math.Abs(a - b) > tolerance)
            {
                report.AppendLine($"MISMATCH  {cs} = {a} , {hl} = {b}");
                ok = false;
                continue;
            }

            matched++;
        }

        foreach (string name in csharp.Keys)
        {
            bool listed = false;
            foreach ((string cs, string _) in Pairs) if (cs == name) { listed = true; break; }
            foreach (string only in CsharpOnly) if (only == name) { listed = true; break; }
            if (!listed) { report.AppendLine($"NOT IN TABLE  C# {name}"); ok = false; }
        }

        int hlslOnly = 0;
        foreach (string name in defines.Keys)
        {
            bool listed = false;
            foreach ((string _, string hl) in Pairs) if (hl == name) { listed = true; break; }
            if (!listed) hlslOnly++;
        }

        report.AppendLine($"Shader-only defines: {hlslOnly} constants (expected).");

        ok &= GridRuleTests(report);

        report.Insert(0, ok
            ? $"Constant parity PASSED — {matched}/{Pairs.Length} pairs matched.\n"
            : $"Constant parity FAILED — {matched}/{Pairs.Length} pairs matched.\n");

        return report.ToString();
    }

    static bool GridRuleTests(StringBuilder r)
    {
        bool all = true;

        r.AppendLine();
        r.AppendLine("## Grid Rule — Power of Two (spec §6.4)");

        foreach (SnowQualityPreset p in Enum.GetValues(typeof(SnowQualityPreset)))
        {
            SnowQualityData q = SnowQuality.Get(p);

            bool gridPow = IsPowerOfTwo(q.MeshGrid);
            bool resPow = IsPowerOfTwo(q.Resolution);
            bool order = q.MeshGrid <= q.Resolution;
            bool divides = q.Resolution % q.MeshGrid == 0;

            int scroll = SnowConstants.SnapQuadsInt * (q.Resolution / q.MeshGrid);
            bool scrollOk = divides && q.ScrollTexels == scroll && scroll > 0;

            bool pass = gridPow && resPow && order && divides && scrollOk;
            all &= pass;

            r.AppendLine("  [" + (pass ? "+" : "-") + "] " + p.ToString().PadRight(8) +
                         " grid " + q.MeshGrid.ToString().PadLeft(4) +
                         (gridPow ? "" : " (NOT 2^n)") +
                         "   resolution " + q.Resolution.ToString().PadLeft(4) +
                         (resPow ? "" : " (NOT 2^n)") +
                         (order ? "" : "   GRID > RESOLUTION") +
                         (divides ? "   res/grid " + (q.Resolution / q.MeshGrid)
                                  : "   DOES NOT DIVIDE") +
                         "   _ScrollTexels " + q.ScrollTexels);
        }

        return all;
    }

    static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;

    static readonly Regex DefineLine =
        new(@"^\s*#define\s+(SNOW_[A-Z0-9_]+)\s+(-?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?)",
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
