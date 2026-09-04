// Measures snow shading equations — sparkle density distance invariance and
// slope-space normal blending properties.
// Invoked by: Menu — To The Summit/Snow/Shading Test.

using System.Text;
using UnityEditor;
using UnityEngine;

public static class SnowShadingTest
{
    const int Res = 128;
    const string KernelPath = "Assets/Snow/Editor/SnowTestKernels.compute";
    const string SparklePath = "Assets/Snow/Shaders/SnowSparkle.hlsl";
    const string LightingPath = "Assets/Snow/Shaders/SnowLighting.hlsl";
    const string SurfacePath = "Assets/Shaders/MountainSurface.hlsl";
    const string ForwardPath = "Assets/Shaders/MountainSurface.shader";
    const string DetailPath = "Assets/Snow/Shaders/SnowDetailNormals.hlsl";

    [MenuItem("To The Summit/Snow/Shading Test", false, 55)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Snow — Shading Test");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(KernelPath);

        if (cs == null)
        {
            r.AppendLine("  [-] " + KernelPath + " could not be loaded.");
            ok = false;
            return r.ToString();
        }

        ok = SparkleTest(r, cs);
        ok &= RnmTest(r, cs);
        ok &= WiringTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "RESULT: PASSED — all tests completed successfully."
                        : "RESULT: FAILED — see above for details.");
        return r.ToString();
    }

    static bool SparkleTest(StringBuilder r, ComputeShader cs)
    {
        r.AppendLine("## Sparkles — Distance Density Invariance (spec §14.4)");
        r.AppendLine("  [i] [Reference: Bowles & Wang, SIGGRAPH 2015]");

        int kernel = cs.FindKernel("KTestSparkle");
        int groups = Mathf.CeilToInt(Res / 8f);

        RenderTexture rt = NewRt(Res);

        float[] footprints = { 0.002f, 0.008f, 0.032f, 0.128f, 0.512f };
        var fractions = new float[footprints.Length];
        bool all = true;

        try
        {
            cs.SetInt("_Resolution", Res);
            cs.SetFloat("_SparkleCellSize", 0.004f);
            cs.SetFloat("_SparkleDensity", 0.06f);
            cs.SetFloat("_SparkleSharpness", 8f);
            cs.SetVector("_TestViewDir", new Vector4(0.3f, 0.9f, 0.3f, 0f));
            cs.SetVector("_TestLightDir", new Vector4(-0.4f, 0.8f, 0.45f, 0f));

            for (int i = 0; i < footprints.Length; i++)
            {
                cs.SetFloat("_TestFootprint", footprints[i]);
                cs.SetTexture(kernel, "_TestOut", rt);
                cs.Dispatch(kernel, groups, groups, 1);

                Color[] px = Read(rt);

                int lit = 0;
                for (int p = 0; p < px.Length; p++) if (px[p].r > 0.5f) lit++;

                fractions[i] = lit / (float)px.Length;
            }

            float min = float.MaxValue, max = 0f;
            foreach (float f in fractions) { min = Mathf.Min(min, f); max = Mathf.Max(max, f); }

            bool stable = min > 0.002f && max < min * 6f;
            all &= stable;

            var line = new StringBuilder("  [" + M(stable) + "] Sparkle pixel fraction  ");
            for (int i = 0; i < footprints.Length; i++)
                line.Append((footprints[i] * 1000f).ToString("0")).Append(" mm->")
                    .Append((fractions[i] * 100f).ToString("0.00")).Append("%   ");

            r.AppendLine(line.ToString());
            r.AppendLine("  [i] Min/max ratio " + (max / Mathf.Max(min, 1e-6f)).ToString("0.00") + "x");
        }
        finally
        {
            Release(ref rt);
        }

        return all;
    }

    static bool RnmTest(StringBuilder r, ComputeShader cs)
    {
        r.AppendLine();
        r.AppendLine("## Detail Normals — Slope-space Addition (spec §14.2)");

        int kernel = cs.FindKernel("KTestRnm");
        RenderTexture rt = NewRt(8);

        bool all = true;

        try
        {
            cs.SetTexture(kernel, "_TestOut", rt);
            cs.Dispatch(kernel, 1, 1, 1);

            Color[] px = Read(rt);

            float flatDetail = Mag(px[0]);
            float flatBase = Mag(px[1]);
            float unitLength = px[2].r;

            bool a = flatDetail < 1e-3f;
            bool b = flatBase < 1e-3f;
            bool c = Mathf.Abs(unitLength - 1f) < 1e-3f;

            all &= a && b && c;

            r.AppendLine("  [" + M(a) + "] Zero detail preserves base normal  delta " +
                         flatDetail.ToString("0.000000"));
            r.AppendLine("  [" + M(b) + "] Flat base passes detail normal      delta " +
                         flatBase.ToString("0.000000"));
            r.AppendLine("  [" + M(c) + "] Result has unit length              |n| = " +
                         unitLength.ToString("0.000000"));
        }
        finally
        {
            Release(ref rt);
        }

        return all;
    }

    static bool WiringTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Mandatory Shader Uniform Connections");

        (string file, string needle, string symptom)[] checks =
        {
            (LightingPath, "_SunElevation01", "Nighttime sparkle without sunGate"),
            (LightingPath, "SnowSparkle(", "Sparkle evaluation missing"),
            (LightingPath, "_ShadowTint", "Shadow tinting missing"),
            (LightingPath, "DirectBRDFSpecular", "Specular evaluation missing"),
            (ForwardPath, "ApplyHeightFog", "Height fog evaluation missing"),
            (SurfacePath, "SnowApplyDetailNormals", "Detail normals not blended"),
            (SurfacePath, "SNOW_MIN_VISIBLE_HEIGHT", "Missing edge clip threshold"),
            (DetailPath, "SampleDetailSlope", "Slope accumulation missing"),
            (SparklePath, "log2", "Sparkle LOD calculation missing"),
            (LightingPath, "crustMask", "Crust shading missing"),
        };

        bool all = true;

        foreach ((string file, string needle, string symptom) c in checks)
        {
            bool found = System.IO.File.Exists(c.file) &&
                         System.IO.File.ReadAllText(c.file).Contains(c.needle);

            all &= found;

            r.AppendLine("  [" + M(found) + "] " + c.needle.PadRight(24) +
                         (found ? "" : "MISSING -> " + c.symptom));
        }

        string detail = System.IO.File.ReadAllText(DetailPath);
        bool noLerpBlend = !detail.Contains("lerp(baseSample") && !detail.Contains("lerp(packed");
        all &= noLerpBlend;

        r.AppendLine("  [" + M(noLerpBlend) + "] No lerp blending in detail normals");

        string lighting = System.IO.File.ReadAllText(LightingPath);
        int aoInAmbient = lighting.IndexOf("ambient *= heightAO", System.StringComparison.Ordinal);
        bool aoOnlyAmbient = aoInAmbient >= 0 &&
                             !lighting.Contains("diffuse *= heightAO") &&
                             !lighting.Contains("lightCol * heightAO");

        all &= aoOnlyAmbient;
        r.AppendLine("  [" + M(aoOnlyAmbient) + "] AO applied to ambient only");

        return all;
    }

    static float Mag(Color c) => new Vector3(c.r, c.g, c.b).magnitude;
    static string M(bool ok) => ok ? "+" : "-";

    static RenderTexture NewRt(int res)
    {
        var rt = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave,
        };
        rt.Create();
        return rt;
    }

    static void Release(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        Object.DestroyImmediate(rt);
        rt = null;
    }

    static Color[] Read(RenderTexture rt)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
        tex.Apply(false);

        RenderTexture.active = prev;

        Color[] px = tex.GetPixels();
        Object.DestroyImmediate(tex);
        return px;
    }
}
