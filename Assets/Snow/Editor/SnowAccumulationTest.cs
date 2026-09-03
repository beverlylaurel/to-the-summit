// Measures accumulation, melting, rain effect, sky visibility, wind transport distribution,
// and precipitation hysteresis. Play mode not required.
// Invoked by: Menu — To The Summit/Snow/Accumulation Test.

using System.Text;
using UnityEditor;
using UnityEngine;

public static class SnowAccumulationTest
{
    const int Res = 256;
    const float AreaSize = 16f;
    const float ObserverY = 4900.5f;
    const float GroundY = ObserverY - 1f;

    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";

    static readonly Vector2 Center = new(-7494f, -4327.5f);

    [MenuItem("To The Summit/Snow/Accumulation Test", false, 54)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Snow — Accumulation Test");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = TemperatureIndependenceTest(r);
        ok &= IntensityTest(r);
        ok &= GpuTests(r);

        r.AppendLine();
        r.AppendLine(ok ? "RESULT: PASSED — all tests completed successfully."
                        : "RESULT: FAILED — see above for details.");
        return r.ToString();
    }

    static bool TemperatureIndependenceTest(StringBuilder r)
    {
        r.AppendLine("## Precipitation Temperature Independence");

        var env = new FakeEnvironment { PrecipKind = PrecipitationKind.Rain, PrecipIntensity01 = 1f };
        var controller = new SnowfallController();
        controller.Reset();

        bool all = true;
        float[] temps = { -20f, -5f, 0f, 1f, 5f, 30f };

        foreach (float t in temps)
        {
            env.TemperatureC = t;
            controller.Tick(env, 1f);

            if (!SnowRuntimeState.IsSnowing) all = false;
        }

        r.AppendLine("  [" + M(all) + "] Snows at all temperatures       " +
                     "-20, -5, 0, 1, 5, 30 °C -> IsSnowing true on all");

        env.TemperatureC = -5f;
        controller.Tick(env, 0f);

        bool rain = !SnowRuntimeState.IsSnowing
                    && SnowRuntimeState.RainWeight01 > 0.999f;

        controller.Tick(env, 1f);

        bool snow = SnowRuntimeState.IsSnowing
                    && SnowRuntimeState.RainWeight01 < 0.001f;

        r.AppendLine("  [" + M(rain && snow) + "] Snow fraction selects precip      " +
                     "0 -> rain, 1 -> snow, both at -5 °C");

        bool noOverlap = true;
        bool thresholdOk = true;

        foreach (float frac in new[] { 0f, 0.25f, 0.49f, 0.5f, 0.51f, 0.75f, 1f })
        {
            controller.Tick(env, frac);

            bool s = SnowRuntimeState.IsSnowing;
            bool rn = SnowRuntimeState.RainWeight01 > 0.001f;

            if (s == rn) noOverlap = false;

            if (s && SnowRuntimeState.SnowfallIntensity01 < 0.999f) thresholdOk = false;
            if (rn && SnowRuntimeState.RainWeight01 < 0.999f) thresholdOk = false;

            if ((frac >= 0.5f) != s) thresholdOk = false;
        }

        r.AppendLine("  [" + M(noOverlap) + "] Snow and rain NEVER overlap       " +
                     "0 / 0.25 / 0.49 / 0.50 / 0.51 / 0.75 / 1 — mutually exclusive");

        r.AppendLine("  [" + M(thresholdOk) + "] Threshold 0.50, full intensity     " +
                     "winning precip gets 100%");

        all &= noOverlap && thresholdOk;

        env.PrecipKind = PrecipitationKind.None;
        env.TemperatureC = -20f;
        controller.Tick(env, 1f);

        bool noPrecipNoSnow = !SnowRuntimeState.IsSnowing;
        r.AppendLine("  [" + M(noPrecipNoSnow) + "] No precip -> no snow             " +
                     "-20 °C and PrecipKind.None -> IsSnowing " + SnowRuntimeState.IsSnowing);

        env.PrecipKind = PrecipitationKind.Rain;
        env.TemperatureC = 5f;
        controller.Tick(env, 1f);

        bool dry = controller.Wetness < 1e-6f;
        r.AppendLine("  [" + M(dry) + "] Dry flakes                      " +
                     "5 °C -> Wetness " + controller.Wetness.ToString("0.00"));

        controller.Reset();

        return all && rain && snow && noPrecipNoSnow && dry;
    }

    static bool IntensityTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Intensity Mapping (spec §17.2)");

        var env = new FakeEnvironment
        {
            PrecipKind = PrecipitationKind.Rain,
            TemperatureC = -5f,
        };

        var controller = new SnowfallController();
        controller.Reset();

        (float i01, float swe, float flake)[] table =
        {
            (0.06f, 8.33e-8f, 16000f * 0.06f),
            (0.24f, 3.33e-7f, 16000f * 0.24f),
            (0.60f, 8.33e-7f, 16000f * 0.60f),
            (1.00f, 1.39e-6f, 16000f * 1.00f),
        };

        bool all = true;

        foreach ((float i01, float swe, float flake) row in table)
        {
            env.PrecipIntensity01 = row.i01;
            controller.Tick(env, 1f);

            bool sweOk = Mathf.Abs(controller.SnowfallSweRate - row.swe) < row.swe * 0.01f;
            bool flakeOk = Mathf.Abs(controller.FlakeRate - row.flake) < row.flake * 0.01f;

            all &= sweOk && flakeOk;

            r.AppendLine("  [" + M(sweOk && flakeOk) + "] i01 " + row.i01.ToString("0.00") +
                         "  SWE " + controller.SnowfallSweRate.ToString("0.00e+0") +
                         " m/s (table " + row.swe.ToString("0.00e+0") + "),  flakes " +
                         controller.FlakeRate.ToString("0") + "/s (code " +
                         row.flake.ToString("0") + ")");
        }

        r.AppendLine("  [i] Flake code implemented as Linear (0.06 -> 960 flakes/s).");

        controller.Reset();
        return all;
    }

    static bool GpuTests(StringBuilder r)
    {
        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) { r.AppendLine("  [-] " + SimPath + " could not be loaded."); return false; }

        var rig = new Rig(sim, Res, AreaSize, Center, GroundY, ObserverY);
        bool all = true;

        try
        {
            r.AppendLine();
            r.AppendLine("## Accumulation (spec §11)");

            rig.ResetSnow(0f, 0.10f);
            rig.ClearSky();

            const float Rate = 8.33e-7f;
            const float Hour = 3600f;

            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: Vector2.zero);

            float swe = rig.Snow(Center).r;
            float wantSwe = Rate * Hour;
            bool sweOk = Mathf.Abs(swe - wantSwe) < wantSwe * 0.02f;
            all &= sweOk;

            float rhoN = rig.Snow(Center).g;
            float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, rhoN);
            float height = swe * SnowConstants.RhoWater / rho;

            r.AppendLine("  [" + M(sweOk) + "] SWE rate            " + (swe * 1000f).ToString("0.000") +
                         " mm/hour  (expected " + (wantSwe * 1000f).ToString("0.000") + ")");
            r.AppendLine("  [i] Equivalent          density " + rho.ToString("0") + " kg/m³ -> " +
                         (height * 100f).ToString("0.00") + " cm/hour");

            rig.ResetSnow(0f, 0.10f);
            rig.Accumulate(60f, Rate, temperature: -5f, rain: 0f, wind: Vector2.zero);

            float freshRho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, rig.Snow(Center).g);
            bool freshOk = Mathf.Abs(freshRho - 55f) < 2f;
            all &= freshOk;
            r.AppendLine("  [" + M(freshOk) + "] Fresh snow density  " + freshRho.ToString("0.0") +
                         " kg/m³  (dry, calm: lerp(55,145,wet=0) = 55)");

            rig.ResetSnow(0.02f, SnowDensityN(60f));
            rig.Accumulate(Hour * 6f, 0f, temperature: -5f, rain: 0f, wind: Vector2.zero);

            float settled = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, rig.Snow(Center).g);
            bool settleOk = settled > 60f && settled < 190f;
            all &= settleOk;
            r.AppendLine("  [" + M(settleOk) + "] Settlement          6 hours: 60 -> " +
                         settled.ToString("0.0") + " kg/m³  (target 190, tau 6 hours)");

            r.AppendLine();
            r.AppendLine("## Melting (spec §11, §3.5)");

            rig.ResetSnow(0.02f, 0.30f);
            float stored = rig.Snow(Center).r;

            rig.Accumulate(Hour, 0f, temperature: -5f, rain: 0f, wind: Vector2.zero);
            bool noMelt = Mathf.Abs(rig.Snow(Center).r - stored) < 1e-6f;
            all &= noMelt;
            r.AppendLine("  [" + M(noMelt) + "] -5 °C               SWE " +
                         (rig.Snow(Center).r * 1000f).ToString("0.0000") +
                         " mm  (stored " + (stored * 1000f).ToString("0.0000") +
                         " — no melt at negative temps)");

            rig.ResetSnow(0.02f, 0.30f);
            rig.Accumulate(Hour, 0f, temperature: 5f, rain: 0f, wind: Vector2.zero);

            float melted = stored - rig.Snow(Center).r;
            float wantMelt = SnowConstants.MeltDdf * 5f * Hour;
            bool meltOk = Mathf.Abs(melted - wantMelt) < wantMelt * 0.03f;
            all &= meltOk;
            r.AppendLine("  [" + M(meltOk) + "] +5 °C               " + (melted * 1000f).ToString("0.0000") +
                         " mm melted  (expected " + (wantMelt * 1000f).ToString("0.0000") +
                         " = DDF × 5 °C × 1 hour)");

            rig.ResetSnow(0.02f, 0.30f);
            rig.Accumulate(Hour, 0f, temperature: 5f, rain: 1f, wind: Vector2.zero);

            float meltedRain = stored - rig.Snow(Center).r;
            float wantRain = wantMelt * (1f + SnowConstants.RainMeltBoost);
            bool rainOk = Mathf.Abs(meltedRain - wantRain) < wantRain * 0.03f;
            all &= rainOk;
            r.AppendLine("  [" + M(rainOk) + "] +5 °C + rain        " + (meltedRain * 1000f).ToString("0.0000") +
                         " mm  (expected " + (wantRain * 1000f).ToString("0.0000") + " = x" +
                         (1f + SnowConstants.RainMeltBoost).ToString("0.0") + ")");

            rig.ResetSnow(0.02f, 0.30f);
            rig.Accumulate(1800f, 0f, temperature: 0f, rain: 1f, wind: Vector2.zero);
            float wet = rig.Snow(Center).b;
            bool wetOk = wet > 0.5f;
            all &= wetOk;
            r.AppendLine("  [" + M(wetOk) + "] Wetting             half hour wet " +
                         wet.ToString("0.000") + "  (tau 1800 s -> 0.632 expected)");

            r.AppendLine();
            r.AppendLine("## Crust (spec §18.3)");

            float[] temps = { -25f, -20f, -12f, -5f, 0f, 5f, 10f };
            var crusts = new float[temps.Length];

            for (int i = 0; i < temps.Length; i++)
            {
                rig.ResetSnow(0.02f, 0.30f);
                rig.Accumulate(1800f, 0f, temperature: 10f, rain: 1f, wind: Vector2.zero);
                rig.Accumulate(600f, 0f, temperature: temps[i], rain: 0f, wind: Vector2.zero);
                crusts[i] = rig.Crust(Center);
            }

            int peak = 0;
            for (int i = 1; i < crusts.Length; i++) if (crusts[i] > crusts[peak]) peak = i;

            bool triangle = Mathf.Approximately(temps[peak], -5f) &&
                            crusts[0] < 1e-4f && crusts[1] < 1e-4f &&
                            crusts[temps.Length - 1] < 1e-4f &&
                            crusts[2] < crusts[3] && crusts[4] < crusts[3];

            all &= triangle;

            var line = new StringBuilder("  [" + M(triangle) + "] Triangular profile  ");
            for (int i = 0; i < temps.Length; i++)
                line.Append(temps[i].ToString("0").PadLeft(3)).Append("°C->")
                    .Append(crusts[i].ToString("0.000")).Append("  ");

            r.AppendLine(line.ToString());
            r.AppendLine("  [i] Peak " + temps[peak].ToString("0") +
                         " °C  (spec: crust forms fastest around -5 °C)");

            rig.ResetSnow(0.02f, 0.30f);
            rig.Accumulate(600f, 0f, temperature: -5f, rain: 0f, wind: Vector2.zero);
            float dryCrust = rig.Crust(Center);

            bool dryGate = dryCrust < 1e-4f;
            all &= dryGate;
            r.AppendLine("  [" + M(dryGate) + "] Dry snow            " + dryCrust.ToString("0.0000") +
                         "  (wetness required for melt-freeze cycle)");

            rig.ResetSnow(0.02f, 0.10f);
            rig.Accumulate(Hour * 2f, 0f, temperature: -15f, rain: 0f, wind: new Vector2(14f, 0f));
            float slab = rig.Crust(Center);

            bool slabOk = slab > 1e-3f;
            all &= slabOk;
            r.AppendLine("  [" + M(slabOk) + "] Wind slab           " + slab.ToString("0.000") +
                         "  (14 m/s, -15 °C, dry — wind generates slab crust)");

            rig.Accumulate(600f, 8.33e-7f, temperature: -15f, rain: 0f, wind: Vector2.zero);
            float buried = rig.Crust(Center);

            bool buries = buried < slab;
            all &= buries;
            r.AppendLine("  [" + M(buries) + "] Fresh snow buries   " + slab.ToString("0.000") +
                         " -> " + buried.ToString("0.000"));

            r.AppendLine();
            r.AppendLine("## Sky Visibility (spec §12)");

            rig.ResetSnow(0f, 0.10f);
            rig.SetSkyHalfCovered(GroundY + 3f);
            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: Vector2.zero);

            float openSwe = rig.Snow(Center + new Vector2(4f, 0f)).r;
            float roofSwe = rig.Snow(Center - new Vector2(4f, 0f)).r;

            bool roofOk = roofSwe < openSwe * 0.05f && openSwe > wantSwe * 0.9f;
            all &= roofOk;
            r.AppendLine("  [" + M(roofOk) + "] Under roof          open " +
                         (openSwe * 1000f).ToString("0.000") + " mm,  covered " +
                         (roofSwe * 1000f).ToString("0.000") + " mm");

            r.AppendLine();
            r.AppendLine("## Wind Distribution (spec §11)");

            rig.ClearSky();
            rig.SetGroundRidge(GroundY, 2.4f);

            rig.ResetSnow(0f, 0.10f);
            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: new Vector2(8f, 0f));
            float plusWindward = rig.Snow(Center + new Vector2(4f, 0f)).r;
            float plusLeeward = rig.Snow(Center - new Vector2(4f, 0f)).r;

            rig.ResetSnow(0f, 0.10f);
            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: new Vector2(-8f, 0f));
            float minusWindward = rig.Snow(Center + new Vector2(4f, 0f)).r;
            float minusLeeward = rig.Snow(Center - new Vector2(4f, 0f)).r;

            float plusRatio = plusWindward / Mathf.Max(plusLeeward, 1e-9f);
            float minusRatio = minusWindward / Mathf.Max(minusLeeward, 1e-9f);

            bool windOk = Mathf.Abs(plusRatio - 1f) > 0.02f &&
                          (plusRatio - 1f) * (minusRatio - 1f) < 0f;
            all &= windOk;
            r.AppendLine("  [" + M(windOk) + "] Reversed direction  +X wind east/west ratio " +
                         plusRatio.ToString("0.000") + ",  -X wind " +
                         minusRatio.ToString("0.000"));

            rig.ResetSnow(0f, 0.10f);
            rig.Accumulate(Hour, Rate, temperature: -5f, rain: 0f, wind: Vector2.zero);
            float flatRatio = rig.Snow(Center + new Vector2(4f, 0f)).r /
                              Mathf.Max(rig.Snow(Center - new Vector2(4f, 0f)).r, 1e-9f);

            bool flatOk = Mathf.Abs(flatRatio - 1f) < 0.01f;
            all &= flatOk;
            r.AppendLine("  [" + M(flatOk) + "] Calm wind           ratio " +
                         flatRatio.ToString("0.0000") + "  (ridge present but no wind -> 1.0000)");
        }
        finally
        {
            rig.Dispose();
        }

        return all;
    }

    static float SnowDensityN(float rho) =>
        Mathf.Clamp01((rho - SnowConstants.RhoMin) / (SnowConstants.RhoMax - SnowConstants.RhoMin));

    static string M(bool ok) => ok ? "+" : "-";

    sealed class FakeEnvironment : ISnowEnvironmentSource
    {
        public Vector3 WindDirection { get; set; } = Vector3.right;
        public Vector3 PrevailingWindDirection { get; set; } = Vector3.right;
        public float WindSpeed { get; set; }
        public Light Sun => null;
        public float SunElevation01 { get; set; }
        public float TemperatureC { get; set; }
        public PrecipitationKind PrecipKind { get; set; }
        public float SnowFraction01 { get; set; } = 1f;
        public float PrecipIntensity01 { get; set; }
        public float FogDensity01 { get; set; }
    }

    sealed class Rig
    {
        readonly ComputeShader sim;
        readonly int res;
        readonly float areaSize;
        readonly Vector2 center;
        readonly float groundY;

        readonly int kClear, kAccumulate;
        readonly int groups;

        RenderTexture snow, snowTemp, trail, trailTemp, skyVis;
        Texture2D ground;
        readonly Texture2D readOne;

        public Rig(ComputeShader sim, int res, float areaSize, Vector2 center,
                   float groundY, float observerY)
        {
            this.sim = sim;
            this.res = res;
            this.areaSize = areaSize;
            this.center = center;
            this.groundY = groundY;

            kClear = sim.FindKernel("KClear");
            kAccumulate = sim.FindKernel("KAccumulate");
            groups = Mathf.CeilToInt(res / 8f);

            snow = Rt(res, RenderTextureFormat.ARGBHalf);
            snowTemp = Rt(res, RenderTextureFormat.ARGBHalf);

            trail = Rt(res, RenderTextureFormat.ARGBHalf);
            trailTemp = Rt(res, RenderTextureFormat.ARGBHalf);
            skyVis = Rt(res, RenderTextureFormat.RFloat);

            readOne = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            SetGroundFlat(groundY);

            Shader.SetGlobalVector(SnowShaderIDs.SnowAreaCenter,
                new Vector4(center.x, center.y, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.SnowAreaSize, areaSize);
            Shader.SetGlobalFloat(SnowShaderIDs.SnowResolution, res);

            Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ,
                new Vector4(center.x, center.y, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, areaSize);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, res);
        }

        static RenderTexture Rt(int res, RenderTextureFormat format)
        {
            var rt = new RenderTexture(res, res, 0, format)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        void SetGroundFlat(float y) => WriteGround((_, __) => 0.5f, y - 1f, 2f);

        public void SetGroundRidge(float baseY, float rise)
        {
            WriteGround((u, _) => 1f - Mathf.Abs(2f * u - 1f), baseY, rise);
        }

        void WriteGround(System.Func<float, float, float> value, float baseY, float range)
        {
            if (ground != null) Object.DestroyImmediate(ground);

            const int GroundRes = 64;

            ground = new Texture2D(GroundRes, GroundRes, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = new Color[GroundRes * GroundRes];

            for (int y = 0; y < GroundRes; y++)
            for (int x = 0; x < GroundRes; x++)
            {
                float u = (x + 0.5f) / GroundRes;
                float v = (y + 0.5f) / GroundRes;
                px[y * GroundRes + x] = new Color(value(u, v), 0f, 0f, 0f);
            }

            ground.SetPixels(px);
            ground.Apply(false, false);

            Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ,
                new Vector4(center.x - areaSize * 0.5f, center.y - areaSize * 0.5f, 0f, 0f));
            Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ,
                new Vector4(areaSize, areaSize, 0f, 0f));
            Shader.SetGlobalVector(SnowShaderIDs.GroundTexelXZ,
                new Vector4(areaSize / GroundRes, areaSize / GroundRes, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, baseY);
            Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, range);
        }

        public void ClearSky() => FillSky(_ => -9999f);

        public void SetSkyHalfCovered(float roofY) =>
            FillSky(x => x < res / 2 ? roofY : -9999f);

        void FillSky(System.Func<int, float> valueAtX)
        {
            var tex = new Texture2D(res, res, TextureFormat.RFloat, false, true);
            var px = new Color[res * res];

            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
                px[y * res + x] = new Color(valueAtX(x), 0f, 0f, 0f);

            tex.SetPixels(px);
            tex.Apply(false, false);
            Graphics.Blit(tex, skyVis);
            Object.DestroyImmediate(tex);
        }

        public void ResetSnow(float swe, float rhoN)
        {
            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetVector(SnowShaderIDs.ClearValue, new Vector4(swe, rhoN, 0f, 0f));
            sim.SetTexture(kClear, SnowShaderIDs.Dst, snow);
            sim.Dispatch(kClear, groups, groups, 1);

            sim.SetVector(SnowShaderIDs.ClearValue, Vector4.zero);
            sim.SetTexture(kClear, SnowShaderIDs.Dst, trail);
            sim.Dispatch(kClear, groups, groups, 1);
        }

        public float Crust(Vector2 worldXZ) => ReadOne(trail, worldXZ).b;

        public void Accumulate(float seconds, float sweRate, float temperature,
                               float rain, Vector2 wind)
        {
            Shader.SetGlobalFloat(SnowShaderIDs.TemperatureC, temperature);
            Shader.SetGlobalFloat(SnowShaderIDs.RainOnSnow01, rain);
            Shader.SetGlobalVector(SnowShaderIDs.WindWS, new Vector4(wind.x, 0f, wind.y, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, wind.magnitude);

            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetFloat(SnowShaderIDs.SnowfallSWERate, sweRate);
            sim.SetFloat(SnowShaderIDs.DeltaTimeEff, seconds);
            sim.SetInt(SnowShaderIDs.TileIndex, 0);
            sim.SetInt(SnowShaderIDs.TileCount, 1);

            sim.SetTexture(kAccumulate, SnowShaderIDs.GroundHeightTex, ground);
            sim.SetTexture(kAccumulate, SnowShaderIDs.SnowSkyVisTex, skyVis);
            sim.SetTexture(kAccumulate, SnowShaderIDs.Snow, snow);
            sim.SetTexture(kAccumulate, SnowShaderIDs.SnowOut, snowTemp);
            sim.SetTexture(kAccumulate, SnowShaderIDs.Trail, trail);
            sim.SetTexture(kAccumulate, SnowShaderIDs.TrailOut, trailTemp);
            sim.Dispatch(kAccumulate, groups, groups, 1);

            (snow, snowTemp) = (snowTemp, snow);
            (trail, trailTemp) = (trailTemp, trail);
        }

        public Color Snow(Vector2 worldXZ) => ReadOne(snow, worldXZ);

        Color ReadOne(RenderTexture rt, Vector2 worldXZ)
        {
            Vector2 uv = (worldXZ - center) / areaSize + new Vector2(0.5f, 0.5f);
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * res), 0, res - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * res), 0, res - 1);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            readOne.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            readOne.Apply(false);
            RenderTexture.active = prev;

            return readOne.GetPixel(0, 0);
        }

        public void Dispose()
        {
            Rel(ref snow);
            Rel(ref snowTemp);
            Rel(ref trail);
            Rel(ref trailTemp);
            Rel(ref skyVis);

            if (ground != null) Object.DestroyImmediate(ground);
            Object.DestroyImmediate(readOne);
        }

        static void Rel(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
            rt = null;
        }
    }
}
