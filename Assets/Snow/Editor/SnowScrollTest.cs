// Measures active region scrolling and texel grid snapping precision.
// Invoked by: Menu — To The Summit/Snow/Scroll Test.

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class SnowScrollTest
{
    const int Res = 1024;
    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";
    const string StampPath = "Assets/Snow/Editor/SnowTestKernels.compute";
    const string ManagerPath = "Assets/Snow/Runtime/SnowManager.cs";

    static readonly Vector4 Edge = new(-1f, -2f, -3f, -4f);

    [MenuItem("To The Summit/Snow/Scroll Test", false, 49)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(4096);
        r.AppendLine("# Snow — Scroll and Snap Test");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = true;
        ok &= ScrollTests(r);
        ok &= SnapTests(r);
        ok &= ReleaseTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "RESULT: PASSED — all tests completed successfully."
                        : "RESULT: FAILED — see above for details.");
        return r.ToString();
    }

    static bool ScrollTests(StringBuilder r)
    {
        r.AppendLine("## KScroll — World Anchoring Fidelity");

        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) { r.AppendLine("  [-] " + SimPath + " could not be loaded."); return false; }

        var stampCs = AssetDatabase.LoadAssetAtPath<ComputeShader>(StampPath);
        if (stampCs == null) { r.AppendLine("  [-] " + StampPath + " could not be loaded."); return false; }

        int stampKernel = stampCs.FindKernel("KStamp");
        int scrollKernel = sim.FindKernel("KScroll");
        int groups = Mathf.CeilToInt(Res / 8f);

        RenderTexture src = NewRT(Res);
        RenderTexture dst = NewRT(Res);

        bool all = true;

        try
        {
            stampCs.SetInt("_Resolution", Res);
            stampCs.SetTexture(stampKernel, "_Dst", src);
            stampCs.Dispatch(stampKernel, groups, groups, 1);

            Color[] stamp = Read(src);

            if (!DetectOrientation(stamp, r, out bool flipped)) return false;

            r.AppendLine("  [+] Readback path        orientation verified, Y-axis " +
                         (flipped ? "FLIPPED (accounted for)" : "aligned"));

            Vector2Int[] cases =
            {
                new(0, 0),
                new(7, 3),
                new(-5, 11),
                new(4, -4),
                new(Res, 0),
                new(-1200, 1200),
            };

            foreach (Vector2Int d in cases)
            {
                sim.SetInt("_Resolution", Res);
                sim.SetVector("_ScrollTexels", new Vector4(d.x, d.y, 0f, 0f));
                sim.SetVector("_NewEdgeValue", Edge);
                sim.SetTexture(scrollKernel, "_Src", src);
                sim.SetTexture(scrollKernel, "_Dst", dst);
                sim.Dispatch(scrollKernel, groups, groups, 1);

                Color[] got = Read(dst);

                int bad = 0;
                float maxErr = 0f;
                int edgeCount = 0;

                for (int ay = 0; ay < Res; ay++)
                for (int ax = 0; ax < Res; ax++)
                {
                    int gy = flipped ? Res - 1 - ay : ay;
                    int sx = ax + d.x;
                    int sy = gy + d.y;

                    bool inside = sx >= 0 && sx < Res && sy >= 0 && sy < Res;

                    float ex = inside ? sx : Edge.x;
                    float ey = inside ? sy : Edge.y;
                    if (!inside) edgeCount++;

                    Color c = got[ay * Res + ax];
                    float e = Mathf.Max(Mathf.Abs(c.r - ex), Mathf.Abs(c.g - ey));

                    if (e > 0f) { bad++; if (e > maxErr) maxErr = e; }
                }

                bool pass = bad == 0;
                all &= pass;

                r.AppendLine("  [" + (pass ? "+" : "-") + "] delta " +
                    ("(" + d.x + ", " + d.y + ")").PadRight(16) +
                    " mismatched " + bad + " / " + (Res * Res) +
                    "   new strip " + edgeCount +
                    (pass ? "" : "   MAX ERROR " + maxErr.ToString("F3")));
            }
        }
        finally
        {
            Release(ref src);
            Release(ref dst);
        }

        return all;
    }

    static bool DetectOrientation(Color[] stamp, StringBuilder r, out bool flipped)
    {
        flipped = false;

        float g00 = stamp[0].g;
        bool guess = Mathf.Abs(g00 - (Res - 1)) < 0.5f;

        if (!guess && Mathf.Abs(g00) > 0.5f)
        {
            r.AppendLine("  [-] Readback path corrupted: (0,0) G = " +
                         g00.ToString("F3") + ", expected 0 or " + (Res - 1));
            return false;
        }

        for (int ay = 0; ay < Res; ay++)
        for (int ax = 0; ax < Res; ax++)
        {
            Color c = stamp[ay * Res + ax];
            float ex = ax;
            float ey = guess ? Res - 1 - ay : ay;

            if (Mathf.Abs(c.r - ex) > 0f || Mathf.Abs(c.g - ey) > 0f)
            {
                r.AppendLine("  [-] Readback mismatch at (" + ax + "," + ay + "): " +
                             "expected (" + ex + "," + ey + "), got (" +
                             c.r.ToString("F3") + "," + c.g.ToString("F3") + ")");
                return false;
            }
        }

        flipped = guess;
        return true;
    }

    static bool SnapTests(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## SnapToTexelGrid — Texel Grid Snapping");

        bool all = true;

        foreach (SnowQualityPreset p in System.Enum.GetValues(typeof(SnowQualityPreset)))
        {
            SnowQualityData q = SnowQuality.Get(p);
            float texel = q.TexelSize;
            float ratio = q.SnapStep / texel;
            float err = Mathf.Abs(ratio - q.ScrollTexels);

            bool pass = err < 1e-4f;
            all &= pass;

            r.AppendLine("  [" + (pass ? "+" : "-") + "] " + p.ToString().PadRight(8) +
                " texel " + (texel * 100f).ToString("F4") + " cm   " +
                "SnapStep / texel = " + ratio.ToString("F6") +
                "   integer calc " + q.ScrollTexels +
                (pass ? "  (matched)" : "  MISMATCH — snap broken"));
        }

        SnowQualityData med = SnowQuality.Get(SnowQualityPreset.Medium);
        float t = med.AreaSize / med.Resolution;

        const float X0 = -7494f;
        float maxDev = 0f;
        int backwards = 0;
        int prev = int.MinValue;
        int steps = 0;

        for (float x = X0; x <= X0 + 20f; x += 0.01f)
        {
            Vector2Int c = SnowManager.SnapToTexelGrid(new Vector3(x, 0f, x), t, med.SnapStep);

            float want = Mathf.Floor(x / med.SnapStep) * med.SnapStep;
            float got = c.x * t;

            maxDev = Mathf.Max(maxDev, Mathf.Abs(got - want));
            if (c.x < prev) backwards++;
            prev = c.x;
            steps++;
        }

        bool sweepPass = maxDev < t * 0.5f && backwards == 0;
        all &= sweepPass;

        r.AppendLine("  [" + (sweepPass ? "+" : "-") + "] Sweep x = -7494 -> -7474, " +
            steps + " steps   max deviation " + (maxDev * 1000f).ToString("F4") + " mm " +
            "(limit " + (t * 500f).ToString("F3") + " mm)   backwards steps " + backwards);

        return all;
    }

    static bool ReleaseTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## SnowManager — Resource Release Check");

        string src = System.IO.File.ReadAllText(ManagerPath);

        var declared = new List<string>();

        foreach (Match m in Regex.Matches(src, @"^\s*RenderTexture\s+([A-Za-z0-9_,\s]+);",
                                          RegexOptions.Multiline))
            foreach (string n in m.Groups[1].Value.Split(','))
                declared.Add(n.Trim());

        var released = new HashSet<string>();
        foreach (Match m in Regex.Matches(src, @"Release\(ref\s+([A-Za-z0-9_]+)\)"))
            released.Add(m.Groups[1].Value);

        bool all = declared.Count > 0;
        var missing = new StringBuilder();

        foreach (string n in declared)
            if (!released.Contains(n)) { all = false; missing.Append(' ').Append(n); }

        r.AppendLine("  [" + (all ? "+" : "-") + "] " + declared.Count +
            " RenderTexture fields, " + released.Count + " released" +
            (all ? "" : "   LEAKED:" + missing));

        return all;
    }

    static RenderTexture NewRT(int res)
    {
        var rt = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBHalf)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
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
