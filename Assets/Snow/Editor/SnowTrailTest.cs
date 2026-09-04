// Measures trail generation — carve persistence, surrounding rim formation,
// movement direction asymmetry, depth scaling, packed trail evolution, fill-in, and wind threshold.
// Invoked by: Menu — To The Summit/Snow/Trail Test.

using System.Text;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class SnowTrailTest
{
    const int Res = 1024;
    const float AreaSize = 16f;
    const float ObserverY = 4900.5f;

    const float GroundY = ObserverY - 1f;

    const float FootDiameter = 0.30f;
    const float FootRadius = FootDiameter * 0.5f;

    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";

    static readonly Vector2 Center = new(-7494f, -4327.5f);

    [MenuItem("To The Summit/Snow/Trail Test", false, 52)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Snow — Trail Test");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = Body(r);

        r.AppendLine();
        r.AppendLine(ok ? "RESULT: PASSED — all tests completed successfully."
                        : "RESULT: FAILED — see above for details.");
        return r.ToString();
    }

    static bool Body(StringBuilder r)
    {
        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) { r.AppendLine("  [-] " + SimPath + " could not be loaded."); return false; }

        var rig = new Rig(sim, Res, AreaSize, Center, GroundY, ObserverY);
        bool all = true;

        try
        {
            float baseH = SnowConstants.RhoWater * 0.02f /
                          Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, 0.10f);

            float footY = GroundY + baseH - 0.12f;

            r.AppendLine("## Setup");
            r.AppendLine("  [i] Resolution " + Res + ",  texel " +
                         (AreaSize / Res * 100f).ToString("0.00") + " cm,  foot radius " +
                         (FootRadius / (AreaSize / Res)).ToString("0.0") + " texels");
            r.AppendLine("  [i] Ground " + GroundY.ToString("0.000") + " m,  SWE 0.020, rhoN 0.10 -> " +
                         "base depth " + (baseH * 100f).ToString("0.0") + " cm");
            r.AppendLine();
            r.AppendLine("## Phase 3 Acceptance Criteria");

            // --- 1. Carve forms ---
            rig.ResetSnow(0.02f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);

            float carve1 = rig.Trail(Center).r;
            float looseDensity = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax,
                                            SnowConstants.LooseN);
            float packedDensity = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax,
                                             SnowConstants.PackedN);
            float expectedCarve = Mathf.Min(SnowConstants.MaxSink,
                                            baseH * (1f - looseDensity / packedDensity));
            bool carved = Mathf.Abs(carve1 - expectedCarve) < 0.006f;
            all &= carved;
            r.AppendLine("  [" + M(carved) + "] Carve forms          " + (carve1 * 100f).ToString("0.00") +
                         " cm  (physical ceiling " + (expectedCarve * 100f).ToString("0.00") + " cm)");

            // Smoothing must not shrink the footprint. The physical radius is defined as
            // the half-depth contour, so a sample at that radius should remain near 50% of
            // the centre depth. An inward-only shoulder once turned an 11 cm boot into a
            // visibly 4 cm-wide dark core.
            float boundaryDepth = rig.Trail(Center + Vector2.right * FootRadius).r;
            float boundaryRatio = boundaryDepth / Mathf.Max(carve1, 1e-5f);
            bool widthPreserved = boundaryRatio > 0.35f && boundaryRatio < 0.65f;
            all &= widthPreserved;
            r.AppendLine("  [" + M(widthPreserved) + "] Width preserved      depth at physical radius " +
                         (boundaryRatio * 100f).ToString("0.0") + "% (target 50%)");

            // --- 2. Carve persists ---
            rig.ClearCapture();
            rig.Deform(1.0f, 0f, 0f);
            float carve2 = rig.Trail(Center).r;
            bool persists = Mathf.Abs(carve2 - carve1) < 1e-4f;
            all &= persists;
            r.AppendLine("  [" + M(persists) + "] Carve persists       after 1s: " +
                         (carve2 * 100f).ToString("0.00") + " cm");

            // --- 3. Rim forms a ring around trail ---
            rig.Rim();
            RimProfile flat = rig.Profile(Center, 0.40f);

            // A soft sole shoulder starts inside the nominal radius, so displaced snow can
            // peak anywhere across that shoulder. Requiring the peak to sit outside the
            // mathematical capsule edge rewarded a hard step -- precisely the artefact the
            // smooth profile is designed to remove.
            bool ring = flat.Peak > 0.002f &&
                        flat.PeakRadius > FootRadius - SnowConstants.SoleEdge &&
                        flat.AtCenter < flat.Peak * 0.20f;
            all &= ring;
            r.AppendLine("  [" + M(ring) + "] Rim ring             center " +
                         (flat.AtCenter * 1000f).ToString("0.00") + " mm,  peak " +
                         (flat.Peak * 1000f).ToString("0.00") + " mm @ " +
                         (flat.PeakRadius * 100f).ToString("0.0") + " cm  (foot radius " +
                         (FootRadius * 100f).ToString("0.0") + " cm)");

            // --- 4. Rim scales with depth ---
            rig.ResetSnow(0.01f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, GroundY + baseH * 0.5f - 0.06f, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);
            rig.Rim();

            float rimHalf = rig.Profile(Center, 0.40f).Peak;
            bool scaled = rimHalf < flat.Peak * 0.75f;
            all &= scaled;
            r.AppendLine("  [" + M(scaled) + "] Rim scales with depth  half SWE -> peak " +
                         (flat.Peak * 1000f).ToString("0.00") + " -> " +
                         (rimHalf * 1000f).ToString("0.00") + " mm");

            // --- 5. Rim asymmetrical with velocity ---
            rig.ResetSnow(0.02f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);
            rig.Rim();
            float centroidStill = rig.Profile(Center, 0.40f).CentroidX;

            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, new Vector2(3f, 0f));
            rig.Deform(0.016f, 0f, 0f);
            rig.Rim();
            float centroidPlus = rig.Profile(Center, 0.40f).CentroidX;

            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, new Vector2(-3f, 0f));
            rig.Deform(0.016f, 0f, 0f);
            rig.Rim();
            float centroidMinus = rig.Profile(Center, 0.40f).CentroidX;

            float shiftPlus = centroidPlus - centroidStill;
            float shiftMinus = centroidMinus - centroidStill;

            bool asym = shiftPlus > 0.002f && shiftMinus < -0.002f;
            all &= asym;
            r.AppendLine("  [" + M(asym) + "] Rim asymmetry        centroid X: stationary " +
                         (centroidStill * 1000f).ToString("0.00") + " mm,  +X 3 m/s " +
                         (centroidPlus * 1000f).ToString("0.00") + " mm (" +
                         (shiftPlus * 1000f).ToString("+0.00;-0.00") + "),  -X 3 m/s " +
                         (centroidMinus * 1000f).ToString("0.00") + " mm (" +
                         (shiftMinus * 1000f).ToString("+0.00;-0.00") + ")");

            r.AppendLine("  [i] Poisson bias        stationary shift " +
                         (centroidStill * 1000f).ToString("0.00") +
                         " mm — 4-tap Poisson kernel x sum +0.5463 texels (spec §9.4).");

            // --- 6. Trail packing evolution ---
            rig.ResetSnow(0.02f, 0.10f);

            float firstSink = 0f, lastSink = 0f;
            int passesTo18 = -1;

            for (int pass = 0; pass < 40; pass++)
            {
                rig.ClearTrail();
                rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
                rig.Deform(0.016f, 0f, 0f);

                float sink = rig.Trail(Center).r;
                if (pass == 0) firstSink = sink;
                lastSink = sink;

                if (passesTo18 < 0 && pass > 0 && sink <= firstSink * SnowConstants.PackedSinkScale)
                    passesTo18 = pass + 1;
            }

            float rhoN = rig.Snow(Center).g;

            bool trailForms = lastSink < firstSink * 0.98f &&
                              rhoN > SnowConstants.LooseN + 0.02f;
            all &= trailForms;
            r.AppendLine("  [" + M(trailForms) + "] Trail compaction      initial sink " +
                         (firstSink * 100f).ToString("0.00") + " cm -> pass 40: " +
                         (lastSink * 100f).ToString("0.00") + " cm,  rhoN 0.100 -> " +
                         rhoN.ToString("0.000"));

            r.AppendLine("  [i] Compaction          repeated stamps settle toward an equilibrium; " +
                         "18% threshold pass " + passesTo18 + ".");

            // --- 7. Trail fill-in with precipitation ---
            rig.ResetSnow(0.02f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);
            float beforeFill = rig.Trail(Center).r;

            rig.ClearCapture();
            rig.Deform(60f, 8.33e-7f, 0f);
            float afterFill = rig.Trail(Center).r;

            float rhoTest = SnowConstants.RhoMin
                          + rig.Snow(Center).g * (SnowConstants.RhoMax - SnowConstants.RhoMin);
            float expectedDrop = 8.33e-7f * SnowConstants.FillGain(rhoTest) * 60f;
            // Trail textures are RHalf; at this magnitude one representable step is a
            // sizeable share of a one-minute fill. Include one half-float quantum.
            float fillTolerance = Mathf.Max(expectedDrop * 0.10f, 0.00015f);
            bool fills = Mathf.Abs((beforeFill - afterFill) - expectedDrop) < fillTolerance;
            all &= fills;
            r.AppendLine("  [" + M(fills) + "] Precipitation fill   " +
                         (beforeFill * 100f).ToString("0.00") + " -> " +
                         (afterFill * 100f).ToString("0.00") + " cm,  in 60s: " +
                         ((beforeFill - afterFill) * 100f).ToString("0.00") +
                         " cm  (expected " + (expectedDrop * 100f).ToString("0.00") + " cm)");

            bool densityStays = rig.Snow(Center).g > SnowConstants.LooseN + 1e-3f;
            all &= densityStays;
            r.AppendLine("  [" + M(densityStays) + "] Density preserved     after fill rhoN " +
                         rig.Snow(Center).g.ToString("0.000") + "  (fresh snow 0.100)");

            // --- 8. Wind fill threshold ---
            rig.ResetSnow(0.02f, 0.10f);
            rig.ClearTrail();
            rig.Stamp(Center, FootDiameter, footY, Vector2.zero);
            rig.Deform(0.016f, 0f, 0f);
            float w0 = rig.Trail(Center).r;

            rig.ClearCapture();
            rig.Deform(60f, 0f, 3f);
            float wLow = rig.Trail(Center).r;

            rig.Deform(60f, 0f, 10f);
            float wHigh = rig.Trail(Center).r;

            bool windGate = Mathf.Abs(wLow - w0) < 1e-5f && wHigh < wLow;
            all &= windGate;
            r.AppendLine("  [" + M(windGate) + "] Wind threshold       " +
                         (w0 * 100f).ToString("0.00") + " cm -> 3 m/s: " +
                         (wLow * 100f).ToString("0.00") + " cm (unchanged) -> 10 m/s: " +
                         (wHigh * 100f).ToString("0.00") + " cm  (threshold 4 m/s)");
        }
        finally
        {
            rig.Dispose();
        }

        all &= TestTapRhythm(r);

        return all;
    }

    static bool TestTapRhythm(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Repeated W Tap Regression");

        var go = new GameObject("SnowStepRhythm_Test");
        try
        {
            var rhythm = go.AddComponent<SnowStepRhythm>();
            MethodInfo process = typeof(SnowStepRhythm).GetMethod(
                "ProcessMotion", BindingFlags.Instance | BindingFlags.NonPublic);

            if (process == null)
            {
                r.AppendLine("  [-] ProcessMotion test seam not found.");
                return false;
            }

            int events = 0;
            rhythm.Stepped += _ => events++;

            // Six deliberate 6 cm taps. Each tap is shorter than both the 8.25 cm stop
            // threshold and the normal half stride, so the old stop-time reset produced
            // exactly zero steps despite 36 cm of cumulative travel.
            for (int i = 0; i < 6; ++i)
            {
                process.Invoke(rhythm, new object[] { 0.75f, 0.08f });
                process.Invoke(rhythm, new object[] { 0f, 0.016f });
            }

            bool tapsPass = events == 3 && rhythm.StepCount == 3;
            r.AppendLine("  [" + M(tapsPass) + "] Six 6 cm W taps      emitted " +
                         events + " planted steps (expected 3; old result 0)");

            int beforeJitter = rhythm.StepCount;
            for (int i = 0; i < 4; ++i)
            {
                process.Invoke(rhythm, new object[] { 0.50f, 0.02f });
                process.Invoke(rhythm, new object[] { 0f, 0.016f });
            }

            bool jitterPass = rhythm.StepCount == beforeJitter;
            r.AppendLine("  [" + M(jitterPass) + "] Four 1 cm jitters   emitted " +
                         (rhythm.StepCount - beforeJitter) + " steps (expected 0)");

            return tapsPass && jitterPass;
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    static string M(bool ok) => ok ? "+" : "-";

    struct RimProfile
    {
        public float AtCenter;
        public float Peak;
        public float PeakRadius;
        public float CentroidX;
    }

    sealed class Rig
    {
        readonly ComputeShader sim;
        readonly int res;
        readonly float areaSize;
        readonly Vector2 center;

        readonly int kClear, kDeform, kBlurH, kBlurV, kRim;
        readonly int groups;

        RenderTexture trail, trailTemp, snow, snowTemp, rimBlur;

        ComputeBuffer segments;
        readonly Vector4[] segmentData = new Vector4[2];
        readonly Texture2D ground;
        readonly Texture2D readOne;

        public Rig(ComputeShader sim, int res, float areaSize, Vector2 center,
                   float groundY, float observerY)
        {
            this.sim = sim;
            this.res = res;
            this.areaSize = areaSize;
            this.center = center;

            kClear = sim.FindKernel("KClear");
            kDeform = sim.FindKernel("KDeform");
            kBlurH = sim.FindKernel("KRimBlurH");
            kBlurV = sim.FindKernel("KRimBlurV");
            kRim = sim.FindKernel("KRim");
            groups = Mathf.CeilToInt(res / 8f);

            trail = Rt(res, RenderTextureFormat.ARGBHalf);
            trailTemp = Rt(res, RenderTextureFormat.ARGBHalf);
            snow = Rt(res, RenderTextureFormat.ARGBHalf);
            snowTemp = Rt(res, RenderTextureFormat.ARGBHalf);
            segments = new ComputeBuffer(2, 16);
            rimBlur = Rt(res, RenderTextureFormat.RHalf);

            readOne = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            ground = new Texture2D(2, 2, TextureFormat.RHalf, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var half = new Color(0.5f, 0f, 0f, 0f);
            ground.SetPixels(new[] { half, half, half, half });
            ground.Apply(false, false);

            Shader.SetGlobalVector(SnowShaderIDs.SnowAreaCenter,
                new Vector4(center.x, center.y, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.SnowAreaSize, areaSize);
            Shader.SetGlobalFloat(SnowShaderIDs.SnowResolution, res);

            Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ,
                new Vector4(center.x - areaSize, center.y - areaSize, 0f, 0f));
            Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ,
                new Vector4(areaSize * 2f, areaSize * 2f, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, groundY - 1f);
            Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, 2f);
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

        void Clear(RenderTexture target, Vector4 value)
        {
            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetVector(SnowShaderIDs.ClearValue, value);
            sim.SetTexture(kClear, SnowShaderIDs.Dst, target);
            sim.Dispatch(kClear, groups, groups, 1);
        }

        public void ResetSnow(float swe, float rhoN)
        {
            Shader.SetGlobalFloat(SnowShaderIDs.FallbackRhoN, rhoN);
            // Compute shader parameters are not guaranteed to inherit Shader globals.
            // Production binds this explicitly in SnowManager; the test must do the same
            // or its result depends on whichever value the asset retained from Play Mode.
            sim.SetFloat(SnowShaderIDs.FallbackRhoN, rhoN);
            Clear(snow, new Vector4(swe, rhoN, 0f, 0f));
        }
        public void ClearTrail() => Clear(trail, Vector4.zero);

        public void ClearCapture() => sim.SetInt(SnowShaderIDs.TrailSegmentCount, 0);

        public void Stamp(Vector2 worldXZ, float diameter, float surfaceY, Vector2 velocity)
        {
            segmentData[0] = new Vector4(worldXZ.x, 0f, worldXZ.y, diameter * 0.5f);
            // b.w is the current segment contract's sink multiplier. Zero silently turns
            // every synthetic test stamp into a no-op.
            segmentData[1] = new Vector4(worldXZ.x, 0f, worldXZ.y, 1f);

            segments.SetData(segmentData);

            sim.SetBuffer(kDeform, SnowShaderIDs.TrailSegments, segments);
            sim.SetInt(SnowShaderIDs.TrailSegmentCount, 1);
            sim.SetVector(SnowShaderIDs.TrailVelocityXZ,
                          new Vector4(velocity.x, velocity.y, 0f, 0f));
        }

        public void Deform(float dt, float snowfallSweRate, float windSpeed)
        {
            Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, windSpeed);

            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetFloat(SnowShaderIDs.SnowDeltaTime, dt);
            sim.SetFloat(SnowShaderIDs.SnowfallSWERate, snowfallSweRate);

            sim.SetTexture(kDeform, SnowShaderIDs.GroundHeightTex, ground);
            sim.SetTexture(kDeform, SnowShaderIDs.Trail, trail);
            sim.SetTexture(kDeform, SnowShaderIDs.TrailOut, trailTemp);
            sim.SetTexture(kDeform, SnowShaderIDs.Snow, snow);
            sim.SetTexture(kDeform, SnowShaderIDs.SnowOut, snowTemp);
            sim.Dispatch(kDeform, groups, groups, 1);

            (trail, trailTemp) = (trailTemp, trail);
            (snow, snowTemp) = (snowTemp, snow);
        }

        public void Rim()
        {
            sim.SetInt(SnowShaderIDs.Resolution, res);
            sim.SetFloat(SnowShaderIDs.RimBlurTexels,
                         SnowConstants.RimBlurMeters / (areaSize / res));

            sim.SetTexture(kBlurH, SnowShaderIDs.Src, trail);
            sim.SetTexture(kBlurH, SnowShaderIDs.Dst, trailTemp);
            sim.Dispatch(kBlurH, groups, groups, 1);

            sim.SetTexture(kBlurV, SnowShaderIDs.Src, trailTemp);
            sim.SetTexture(kBlurV, SnowShaderIDs.CarveOut, rimBlur);
            sim.Dispatch(kBlurV, groups, groups, 1);

            sim.SetTexture(kRim, SnowShaderIDs.Trail, trail);
            sim.SetTexture(kRim, SnowShaderIDs.Snow, snow);
            sim.SetTexture(kRim, SnowShaderIDs.BlurredCarve, rimBlur);
            sim.SetTexture(kRim, SnowShaderIDs.TrailOut, trailTemp);
            sim.Dispatch(kRim, groups, groups, 1);

            (trail, trailTemp) = (trailTemp, trail);
        }

        Vector2 WorldToTexel(Vector2 worldXZ)
        {
            Vector2 uv = (worldXZ - center) / areaSize + new Vector2(0.5f, 0.5f);
            return uv * res;
        }

        public Color Trail(Vector2 worldXZ) => One(trail, worldXZ);
        public Color Snow(Vector2 worldXZ) => One(snow, worldXZ);

        Color One(RenderTexture rt, Vector2 worldXZ)
        {
            Vector2 t = WorldToTexel(worldXZ);
            int x = Mathf.Clamp(Mathf.FloorToInt(t.x), 0, res - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(t.y), 0, res - 1);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            readOne.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            readOne.Apply(false);
            RenderTexture.active = prev;

            return readOne.GetPixel(0, 0);
        }

        public RimProfile Profile(Vector2 worldXZ, float windowRadius)
        {
            float texel = areaSize / res;
            int span = Mathf.CeilToInt(windowRadius / texel);

            Vector2 c = WorldToTexel(worldXZ);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(c.x) - span, 0, res - 1);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(c.y) - span, 0, res - 1);
            int w = Mathf.Min(span * 2 + 1, res - x0);
            int h = Mathf.Min(span * 2 + 1, res - y0);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = trail;

            var tex = new Texture2D(w, h, TextureFormat.RGBAFloat, false, true);
            tex.ReadPixels(new Rect(x0, y0, w, h), 0, 0);
            tex.Apply(false);

            RenderTexture.active = prev;

            Color[] px = tex.GetPixels();
            Object.DestroyImmediate(tex);

            const float BinSize = 0.01f;
            int bins = Mathf.CeilToInt(windowRadius / BinSize);
            var sum = new float[bins];
            var count = new int[bins];

            float weighted = 0f, weight = 0f;

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x0 + x + 0.5f - c.x) * texel;
                float dy = (y0 + y + 0.5f - c.y) * texel;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > windowRadius) continue;

                float rim = px[y * w + x].g;

                int bin = Mathf.Min(bins - 1, Mathf.FloorToInt(d / BinSize));
                sum[bin] += rim;
                count[bin]++;

                weighted += rim * dx;
                weight += rim;
            }

            var profile = new RimProfile
            {
                AtCenter = count[0] > 0 ? sum[0] / count[0] : 0f,
                CentroidX = weight > 1e-6f ? weighted / weight : 0f,
            };

            for (int b = 0; b < bins; b++)
            {
                if (count[b] == 0) continue;
                float mean = sum[b] / count[b];
                if (mean <= profile.Peak) continue;

                profile.Peak = mean;
                profile.PeakRadius = (b + 0.5f) * BinSize;
            }

            return profile;
        }

        public void Dispose()
        {
            Rel(ref trail); Rel(ref trailTemp);
            Rel(ref snow); Rel(ref snowTemp);
            Rel(ref rimBlur);
            segments?.Release();
            segments = null;

            Object.DestroyImmediate(ground);
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
