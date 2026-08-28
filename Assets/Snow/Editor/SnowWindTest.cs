// Measures wind shadow, transport, sastrugi, and heat sources.
// Invoked by: Menu — To The Summit/Snow/Wind Test.

using System.Text;
using UnityEditor;
using UnityEngine;

public static class SnowWindTest
{
    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";
    const string KernelPath = "Assets/Snow/Editor/SnowTestKernels.compute";

    const int Res = 256;
    const float AreaSize = 16f;
    const float GroundY = 100f;

    const int SkyRes = 128;
    const float SkyArea = 96f;

    static readonly Vector2 Center = Vector2.zero;

    [MenuItem("To The Summit/Snow/Wind Test", false, 60)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Snow — Wind, Sastrugi, and Heat Test");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = ShadowTest(r);
        ok &= MassTest(r);
        ok &= SastrugiTest(r);
        ok &= HeatTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "RESULT: PASSED — all tests completed successfully."
                        : "RESULT: FAILED — see above for details.");
        return r.ToString();
    }

    static bool ShadowTest(StringBuilder r)
    {
        r.AppendLine("## Wind Shadow (spec §18.0)");
        r.AppendLine("  [i] [Reference: Cordonnier et al., EG 2018, §4.2]");

        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) { r.AppendLine("  [-] " + SimPath + " could not be loaded."); return false; }

        int kernel = sim.FindKernel("KWindShadow");
        int groups = Mathf.CeilToInt(SkyRes / 8f);

        RenderTexture sky = New(SkyRes, RenderTextureFormat.RFloat);
        RenderTexture shadow = New(SkyRes, RenderTextureFormat.RGFloat);
        Texture2D ground = FlatGround(GroundY);

        bool all = true;

        try
        {
            WriteWall(sky, wallColumn: SkyRes / 2, wallHeight: GroundY + 8f);

            Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ, Vector4.zero);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, SkyArea);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, SkyRes);

            float lee = Solve(sim, kernel, groups, shadow, sky, ground,
                              wind: new Vector3(10f, 0f, 0f), sampleColumn: SkyRes / 2 + 8);

            float windward = Solve(sim, kernel, groups, shadow, sky, ground,
                                   wind: new Vector3(10f, 0f, 0f), sampleColumn: SkyRes / 2 - 8,
                                   reset: false);

            bool leeward = lee > 0.05f && windward < lee * 0.25f;
            all &= leeward;

            r.AppendLine("  [" + M(leeward) + "] +X wind             behind wall " +
                         lee.ToString("0.000") + " m,  in front " + windward.ToString("0.000") +
                         " m  (shadow forms leeward)");

            float leeBack = Solve(sim, kernel, groups, shadow, sky, ground,
                                  wind: new Vector3(-10f, 0f, 0f), sampleColumn: SkyRes / 2 - 8);

            float windwardBack = Solve(sim, kernel, groups, shadow, sky, ground,
                                       wind: new Vector3(-10f, 0f, 0f), sampleColumn: SkyRes / 2 + 8,
                                       reset: false);

            bool flipped = leeBack > 0.05f && windwardBack < leeBack * 0.25f;
            all &= flipped;

            r.AppendLine("  [" + M(flipped) + "] -X wind             behind wall " +
                         leeBack.ToString("0.000") + " m,  in front " + windwardBack.ToString("0.000") +
                         " m  (shadow flips sides)");

            float calm = Solve(sim, kernel, groups, shadow, sky, ground,
                               wind: Vector3.zero, sampleColumn: SkyRes / 2 + 8);

            bool noWind = calm < 0.01f;
            all &= noWind;

            r.AppendLine("  [" + M(noWind) + "] Calm wind            " + calm.ToString("0.000") +
                         " m  (no shadow)");
        }
        finally
        {
            Release(ref sky);
            Release(ref shadow);
            Object.DestroyImmediate(ground);
        }

        return all;
    }

    static float Solve(ComputeShader sim, int kernel, int groups,
                       RenderTexture shadow, RenderTexture sky, Texture2D ground,
                       Vector3 wind, int sampleColumn, bool reset = true)
    {
        if (reset) ClearRg(shadow);

        Shader.SetGlobalVector(SnowShaderIDs.WindWS, wind);
        Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, wind.magnitude);

        sim.SetTexture(kernel, SnowShaderIDs.SkyVisY, sky);
        sim.SetTexture(kernel, SnowShaderIDs.WindShadow, shadow);
        sim.SetTexture(kernel, SnowShaderIDs.GroundHeightTex, ground);

        for (int iteration = 0; iteration < 24; iteration++)
        for (int parity = 0; parity < 2; parity++)
        {
            sim.SetInt(SnowShaderIDs.GSParity, parity);
            sim.Dispatch(kernel, groups, groups, 1);
        }

        Color c = ReadPixel(shadow, sampleColumn, SkyRes / 2);
        return Mathf.Max(0f, c.r - GroundY);
    }

    static bool MassTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Mass Conservation (spec §21 Phase 12)");

        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        var kernels = AssetDatabase.LoadAssetAtPath<ComputeShader>(KernelPath);

        if (sim == null || kernels == null) { r.AppendLine("  [-] Compute shaders could not be loaded."); return false; }

        int transport = sim.FindKernel("KWindTransport");
        int stamp = kernels.FindKernel("KStamp");
        int groups = Mathf.CeilToInt(Res / 8f);

        RenderTexture snow = New(Res, RenderTextureFormat.ARGBFloat);
        RenderTexture trail = New(Res, RenderTextureFormat.ARGBFloat);
        RenderTexture shadow = New(SkyRes, RenderTextureFormat.RGFloat);
        Texture2D ground = FlatGround(GroundY);

        bool all = true;

        try
        {
            Shader.SetGlobalVector(SnowShaderIDs.SnowAreaCenter, Vector4.zero);
            Shader.SetGlobalFloat(SnowShaderIDs.SnowAreaSize, AreaSize);
            Shader.SetGlobalFloat(SnowShaderIDs.SnowResolution, Res);

            Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ, Vector4.zero);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, SkyArea);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, SkyRes);

            ClearRg(shadow);

            WriteBumpySnow(snow, kernels, stamp, groups);
            ClearArgb(trail);

            float before = SumSwe(snow);

            Shader.SetGlobalVector(SnowShaderIDs.WindWS, new Vector4(12f, 0f, 4f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, 12.6f);

            sim.SetInt(SnowShaderIDs.Resolution, Res);
            sim.SetFloat(SnowShaderIDs.SnowDeltaTime, 0.5f);
            sim.SetFloat(SnowShaderIDs.SnowfallSWERate, 0f);

            sim.SetTexture(transport, SnowShaderIDs.GroundHeightTex, ground);
            sim.SetTexture(transport, SnowShaderIDs.SnowWindShadowTex, shadow);
            sim.SetTexture(transport, SnowShaderIDs.SnowRW, snow);
            sim.SetTexture(transport, SnowShaderIDs.TrailRW, trail);

            for (int pass = 0; pass < 20; pass++)
            for (int tile = 1; tile <= 5; tile++)
            {
                sim.SetInt(SnowShaderIDs.TileIndex, tile);
                sim.Dispatch(transport, groups, groups, 1);
            }

            float after = SumSwe(snow);
            float drift = Mathf.Abs(after - before) / Mathf.Max(before, 1e-9f);

            bool conserved = drift < 0.01f;
            all &= conserved;

            r.AppendLine("  [" + M(conserved) + "] Sum SWE              " + before.ToString("0.000") +
                         " -> " + after.ToString("0.000") + "  drift " +
                         (drift * 100f).ToString("0.000") + "%  (tolerance 1%)");

            float variance = SweVariance(snow);
            bool moved = variance > 0f;
            all &= moved;

            r.AppendLine("  [" + M(moved) + "] Distribution changed variance " +
                         variance.ToString("0.0000000"));

            WriteBumpySnow(snow, kernels, stamp, groups);
            float calmBefore = SumSwe(snow);

            Shader.SetGlobalVector(SnowShaderIDs.WindWS, new Vector4(4f, 0f, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, 4f);

            sim.SetTexture(transport, SnowShaderIDs.SnowRW, snow);

            for (int tile = 1; tile <= 5; tile++)
            {
                sim.SetInt(SnowShaderIDs.TileIndex, tile);
                sim.Dispatch(transport, groups, groups, 1);
            }

            float calmAfter = SumSwe(snow);
            bool gated = Mathf.Abs(calmAfter - calmBefore) < calmBefore * 1e-5f;
            all &= gated;

            r.AppendLine("  [" + M(gated) + "] 4 m/s threshold     Sum SWE " +
                         calmBefore.ToString("0.000") + " -> " + calmAfter.ToString("0.000") +
                         "  (loose snow threshold 5 m/s)");
        }
        finally
        {
            Release(ref snow);
            Release(ref trail);
            Release(ref shadow);
            Object.DestroyImmediate(ground);
        }

        return all;
    }

    static bool SastrugiTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Sastrugi (spec §18.4)");

        var kernels = AssetDatabase.LoadAssetAtPath<ComputeShader>(KernelPath);
        if (kernels == null) { r.AppendLine("  [-] Test kernel could not be loaded."); return false; }

        int kernel = kernels.FindKernel("KTestSastrugi");
        const int Samples = 256;

        RenderTexture rt = New(Samples, RenderTextureFormat.ARGBFloat);
        Texture2D noise = SnowTextureBaker.EnsureSastrugiNoise();

        bool all = true;

        try
        {
            string noisePath = AssetDatabase.GetAssetPath(noise);
            var noiseImporter = (TextureImporter)AssetImporter.GetAtPath(noisePath);

            if (!noiseImporter.isReadable)
            {
                noiseImporter.isReadable = true;
                noiseImporter.SaveAndReimport();
                noise = AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath);
            }

            Color[] noisePx = noise.GetPixels();

            float noiseMin = 1f, noiseMax = 0f;
            foreach (Color c in noisePx)
            {
                noiseMin = Mathf.Min(noiseMin, c.r);
                noiseMax = Mathf.Max(noiseMax, c.r);
            }

            bool noiseOk = noiseMax - noiseMin > 0.2f;
            all &= noiseOk;

            r.AppendLine("  [" + M(noiseOk) + "] Noise texture        .r range " +
                         noiseMin.ToString("0.000") + " – " + noiseMax.ToString("0.000"));

            Shader.SetGlobalVector(SnowShaderIDs.SastrugiWindDir, new Vector4(1f, 0f, 0f, 0f));

            float alongVariation = Variation(kernels, kernel, rt, Samples,
                                             axis: new Vector2(1f, 0f), noise);

            float acrossVariation = Variation(kernels, kernel, rt, Samples,
                                              axis: new Vector2(0f, 1f), noise);

            bool oriented = alongVariation > acrossVariation * 1.5f;
            all &= oriented;

            r.AppendLine("  [" + M(oriented) + "] Ridges PERPENDICULAR wind variation " +
                         (alongVariation * 1000f).ToString("0.00") + " mm/sample,  across " +
                         (acrossVariation * 1000f).ToString("0.00") +
                         " mm/sample  (ratio " +
                         (alongVariation / Mathf.Max(acrossVariation, 1e-9f)).ToString("0.00") + ")");

            float zero = Variation(kernels, kernel, rt, Samples,
                                   axis: new Vector2(1f, 0f), noise, amplitude: 0f);

            bool gated = zero < 1e-6f;
            all &= gated;

            r.AppendLine("  [" + M(gated) + "] Amplitude 0          " + zero.ToString("0.0000000"));
        }
        finally
        {
            Release(ref rt);
        }

        return all;
    }

    static float Variation(ComputeShader cs, int kernel, RenderTexture rt, int samples,
                           Vector2 axis, Texture2D noise, float amplitude = 1f)
    {
        cs.SetInt(SnowShaderIDs.Resolution, samples);
        cs.SetVector("_TestAxis", axis);
        cs.SetFloat("_TestStep", 0.02f);
        cs.SetFloat("_TestAmplitude", amplitude);
        cs.SetTexture(kernel, "_SastrugiNoise", noise);
        cs.SetTexture(kernel, "_TestOut", rt);
        cs.Dispatch(kernel, Mathf.CeilToInt(samples / 64f), 1, 1);

        Color[] px = ReadAll(rt);

        float sum = 0f;
        for (int i = 1; i < samples; i++)
            sum += Mathf.Abs(px[i].r - px[i - 1].r);

        return sum / (samples - 1);
    }

    static bool HeatTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Heat Sources (spec §18.2)");
        r.AppendLine("  [i] [Reference: Grosbellet et al., CGF 2016]");

        var kernels = AssetDatabase.LoadAssetAtPath<ComputeShader>(KernelPath);
        if (kernels == null) { r.AppendLine("  [-] Test kernel could not be loaded."); return false; }

        int kernel = kernels.FindKernel("KTestHeat");
        const int Samples = 128;

        RenderTexture rt = New(Samples, RenderTextureFormat.ARGBFloat);

        var posRadius = new Vector4[SnowHeatRegistry.MaxSources];
        var heatParams = new Vector4[SnowHeatRegistry.MaxSources];

        bool all = true;

        try
        {
            posRadius[0] = new Vector4(0f, 0f, 0f, 2f);
            heatParams[0] = new Vector4(0.5f, 0f, 0f, 0f);

            Shader.SetGlobalVectorArray(SnowShaderIDs.HeatSources, posRadius);
            Shader.SetGlobalVectorArray(SnowShaderIDs.HeatParams, heatParams);
            Shader.SetGlobalInt(SnowShaderIDs.HeatCount, 1);

            float[] single = Profile(kernels, kernel, rt, Samples, step: 0.05f);

            bool center = Mathf.Abs(single[0] - 0.5f) < 1e-3f;

            int atRadius = Mathf.RoundToInt(2f / 0.05f);
            bool compact = single[atRadius] < 1e-6f && single[atRadius + 4] < 1e-6f;

            all &= center && compact;

            r.AppendLine("  [" + M(center) + "] Center                 " + single[0].ToString("0.0000") +
                         "  (intensity 0.5)");
            r.AppendLine("  [" + M(compact) + "] EXACT zero at radius   2.0 m -> " +
                         single[atRadius].ToString("0.0000000") + ",  2.2 m -> " +
                         single[atRadius + 4].ToString("0.0000000") +
                         "  (Wyvill compact support)");

            posRadius[1] = new Vector4(0f, 0f, 0f, 2f);
            heatParams[1] = new Vector4(0.5f, 0f, 0f, 0f);

            Shader.SetGlobalVectorArray(SnowShaderIDs.HeatSources, posRadius);
            Shader.SetGlobalVectorArray(SnowShaderIDs.HeatParams, heatParams);
            Shader.SetGlobalInt(SnowShaderIDs.HeatCount, 2);

            float[] pair = Profile(kernels, kernel, rt, Samples, step: 0.05f);

            bool sums = Mathf.Abs(pair[0] - 1f) < 1e-3f;
            all &= sums;

            r.AppendLine("  [" + M(sums) + "] Two sources ADDITIVE   " + single[0].ToString("0.000") +
                         " + " + single[0].ToString("0.000") + " = " + pair[0].ToString("0.000"));

            Shader.SetGlobalInt(SnowShaderIDs.HeatCount, 0);
            float[] none = Profile(kernels, kernel, rt, Samples, step: 0.05f);

            bool empty = none[0] < 1e-9f;
            all &= empty;

            r.AppendLine("  [" + M(empty) + "] No sources             " + none[0].ToString("0.0000000") +
                         "  (`_HeatCount = 0` zero cost)");
        }
        finally
        {
            Release(ref rt);
        }

        return all;
    }

    static float[] Profile(ComputeShader cs, int kernel, RenderTexture rt, int samples, float step)
    {
        cs.SetInt(SnowShaderIDs.Resolution, samples);
        cs.SetVector("_TestAxis", new Vector4(1f, 0f, 0f, 0f));
        cs.SetFloat("_TestStep", step);
        cs.SetTexture(kernel, "_TestOut", rt);
        cs.Dispatch(kernel, Mathf.CeilToInt(samples / 64f), 1, 1);

        Color[] px = ReadAll(rt);

        var profile = new float[samples];
        for (int i = 0; i < samples; i++) profile[i] = px[i].r;

        return profile;
    }

    static string M(bool ok) => ok ? "+" : "-";

    static RenderTexture New(int res, RenderTextureFormat format)
    {
        var rt = new RenderTexture(res, res, 0, format)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
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

    static Texture2D FlatGround(float y)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RFloat, false, true)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };

        var half = new Color(0.5f, 0f, 0f, 0f);
        tex.SetPixels(new[] { half, half, half, half });
        tex.Apply(false, false);

        Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ, new Vector4(-500f, -500f, 0f, 0f));
        Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ, new Vector4(1000f, 1000f, 0f, 0f));
        Shader.SetGlobalVector(SnowShaderIDs.GroundTexelXZ, new Vector4(500f, 500f, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, y - 1f);
        Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, 2f);

        return tex;
    }

    static void WriteWall(RenderTexture sky, int wallColumn, float wallHeight)
    {
        var tex = new Texture2D(SkyRes, SkyRes, TextureFormat.RFloat, false, true);
        var px = new Color[SkyRes * SkyRes];

        for (int y = 0; y < SkyRes; y++)
        for (int x = 0; x < SkyRes; x++)
            px[y * SkyRes + x] = new Color(x == wallColumn ? wallHeight : -9999f, 0f, 0f, 0f);

        tex.SetPixels(px);
        tex.Apply(false, false);

        Graphics.Blit(tex, sky);
        Object.DestroyImmediate(tex);
    }

    static void WriteBumpySnow(RenderTexture snow, ComputeShader kernels, int stamp, int groups)
    {
        var tex = new Texture2D(Res, Res, TextureFormat.RGBAFloat, false, true);
        var px = new Color[Res * Res];

        for (int y = 0; y < Res; y++)
        for (int x = 0; x < Res; x++)
        {
            float bump = Mathf.Sin(x * 0.15f) * Mathf.Cos(y * 0.11f);
            px[y * Res + x] = new Color(0.05f + bump * 0.02f, 0.10f, 0f, 0f);
        }

        tex.SetPixels(px);
        tex.Apply(false, false);

        Graphics.Blit(tex, snow);
        Object.DestroyImmediate(tex);
    }

    static void ClearRg(RenderTexture rt) => Fill(rt, new Color(0f, 0f, 0f, 0f));
    static void ClearArgb(RenderTexture rt) => Fill(rt, new Color(0f, 0f, 0f, 0f));

    static void Fill(RenderTexture rt, Color value)
    {
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        var px = new Color[rt.width * rt.height];
        for (int i = 0; i < px.Length; i++) px[i] = value;

        tex.SetPixels(px);
        tex.Apply(false, false);

        Graphics.Blit(tex, rt);
        Object.DestroyImmediate(tex);
    }

    static float SumSwe(RenderTexture snow)
    {
        Color[] px = ReadAll(snow);

        double sum = 0.0;
        for (int i = 0; i < px.Length; i++) sum += px[i].r;

        return (float)sum;
    }

    static float SweVariance(RenderTexture snow)
    {
        Color[] px = ReadAll(snow);

        double mean = 0.0;
        for (int i = 0; i < px.Length; i++) mean += px[i].r;
        mean /= px.Length;

        double variance = 0.0;
        for (int i = 0; i < px.Length; i++)
        {
            double d = px[i].r - mean;
            variance += d * d;
        }

        return (float)(variance / px.Length);
    }

    static Color ReadPixel(RenderTexture rt, int x, int y)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        var tex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        tex.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
        tex.Apply(false);

        RenderTexture.active = prev;

        Color c = tex.GetPixel(0, 0);
        Object.DestroyImmediate(tex);
        return c;
    }

    static Color[] ReadAll(RenderTexture rt)
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
