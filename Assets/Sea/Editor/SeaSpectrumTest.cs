// ROLE: numeric verification of the wave field. Measures the acceptance
// criteria of spec 6.8 and 7.
// CALLED BY: menu — To The Summit/Sea/Test Wave Field

using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// AN FFT FAILS SILENTLY.
///
/// If the RNG is not Gaussian the surface shows a regular pattern; if the
/// conjugate symmetry is broken the surface stays flat. Both look only
/// "slightly odd" on screen and get hunted in the wrong place for weeks. That
/// is why the acceptance criterion here is a **number**, not an eye.
public static class SeaSpectrumTest
{
    /// An environment that pins the wind to a known value. Coming from the
    /// weather system the measurement would not be repeatable.
    sealed class FixedEnvironment : ISeaEnvironmentSource
    {
        public Vector3 direction = Vector3.right;
        public float speed = 8f;

        public Vector3 WindDirection => direction;
        public float WindSpeed => speed;

        /// PINNED LIKE THE WIND. The swell wanders in the game; a measurement that
        /// wandered with it could not be compared with the one before it.
        public float swellPeriod = 10f;
        public float SwellPeriod => swellPeriod;
        public float SwellEnergyScale => 1f;
        public Vector3 SwellDirection => direction;

        public Light Sun => null;
        public float SunElevation01 => 0.5f;
        public float CloudCover01 => 0f;
        public float FogDensity01 => 0f;
        public SeaPrecipitationKind PrecipKind => SeaPrecipitationKind.None;
        public float PrecipIntensity01 => 0f;
    }

    struct Measurement
    {
        public float meanH;
        public float rmsH;
        public float rmsSlope;
        public float foldFraction;
        public float minJ;
        public float windBandFraction;
        public float dominantAngle;
    }

    [MenuItem("To The Summit/Sea/Test Wave Field")]
    public static void Run()
    {
        var settings = AssetDatabase.LoadAssetAtPath<SeaSettings>(
            "Assets/Sea/Settings/SeaSettings.asset");
        var spectrum = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Sea/Shaders/SeaSpectrum.compute");
        var fft = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Sea/Shaders/SeaFFT.compute");
        var foam = AssetDatabase.LoadAssetAtPath<ComputeShader>(
            "Assets/Sea/Shaders/SeaFoam.compute");

        if (settings == null || spectrum == null || fft == null || foam == null)
        {
            Debug.LogError("Sea test: settings or a compute shader was not found.");
            return;
        }

        var env = new FixedEnvironment();

        // THE OBJECT IS CREATED INACTIVE. While active, `AddComponent` runs
        // `OnEnable` immediately and, with no `Bind` yet, the component
        // disables itself.
        var go = new GameObject("SeaSpectrumTest") { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false);

        var sim = go.AddComponent<SeaSimulation>();
        sim.Bind(settings, env, spectrum, fft, foam);

        go.SetActive(true);

        var report = new StringBuilder();
        report.AppendLine("WAVE FIELD MEASUREMENT");

        // THE INPUTS ARE PRINTED WITH THE RESULT. Two runs with different
        // settings produced the SAME text and Unity collapsed them into one
        // console entry — the second measurement looked like it had never
        // happened. A report that does not name its inputs cannot be told
        // apart from the previous one.
        report.AppendLine($"fetch {settings.fetch:F0} m   patch {settings.patchSizes}   " +
                          $"windSpread {settings.swell:F2}");
        report.AppendLine($"swell: period {settings.swellPeriodShort:F1}-{settings.swellPeriodLong:F1} s   " +
                          $"alpha {settings.swellAlpha:E3}   gamma {settings.swellGamma:F1}   " +
                          $"spread {settings.swellSpread:F0}   offset {settings.swellDirectionOffset:F0} deg");
        report.AppendLine();

        int failures = 0;
        int activeTiers = SeaQuality.Of(settings.quality).TierCount;

        // --- 0. The longest swell must not expose the spatial or temporal tile ---
        float longestWave = SeaConstants.G * settings.swellPeriodLong
                          * settings.swellPeriodLong / SeaConstants.TwoPi;
        float longCycles = settings.patchSizes.x / longestWave;
        report.AppendLine($"repeat budget: longest wave {longestWave:F1} m, " +
                          $"tier-0 cycles {longCycles:F1}, time loop {settings.loopPeriod:F0} s");
        if (longCycles < 8f)
        {
            report.AppendLine("  RED: tier 0 contains fewer than eight longest-period waves; " +
                              "the swell tile will read as a repeating train.");
            failures++;
        }
        if (settings.loopPeriod < 1800f)
        {
            report.AppendLine("  RED: the exact time loop is shorter than 30 minutes and can " +
                              "be learned during normal play.");
            failures++;
        }
        report.AppendLine();

        // --- 1. Conjugate symmetry and energy, U10 = 8 m/s ---
        env.speed = 8f;
        env.direction = Vector3.right;
        sim.Step(0f);

        report.AppendLine("U10 = 8 m/s, direction = +X");
        report.AppendLine("tier |  mean(h)  |  rms(h)  | rms(slope) |  J<0   | wind band");

        for (int k = 0; k < activeTiers; k++)
        {
            Measurement m = Measure(sim, k);
            report.AppendLine($"  {k}  | {m.meanH,9:F6} | {m.rmsH,8:F4} | {m.rmsSlope,10:F4} |" +
                              $" {m.foldFraction,5:P1} | {m.windBandFraction,5:P1}");

            // Spec §6.8: broken conjugate symmetry drives the mean away from
            // zero.
            if (Mathf.Abs(m.meanH) >= 1e-3f)
            {
                report.AppendLine($"  RED tier {k}: |mean(h)| = {Mathf.Abs(m.meanH):E3}" +
                                  " >= 1e-3. Conjugate symmetry is broken (spec §6.8).");
                failures++;
            }

            // Spec §7 / plan Phase 3 Step 3: expected folding band.
            if (m.foldFraction > 0.20f)
            {
                report.AppendLine($"  RED tier {k}: J<0 fraction {m.foldFraction * 100f:F1}%" +
                                  " > 20%. Choppiness is too high, the surface will knot.");
                failures++;
            }
        }

        report.AppendLine();

        // --- 1b. The CPU sea state and the rendered field must use one normalization ---
        //
        // Breaking, run-up and the HUD read the CPU moments, while the player sees the
        // GPU field. A sqrt(2) amplitude error once made the rendered Hs 3.32 m while
        // every shore decision was made with 2.31 m. Both halves can be individually
        // plausible and still form one visibly wrong coast, so parity is asserted here.
        float gpuHs8 = 4f * TotalRms(sim, activeTiers);
        SeaSpectrumMoments.Result cpu8 = SeaSpectrumMoments.Integrate(
            env.speed, settings, env.SwellPeriod, env.SwellEnergyScale,
            settings.TierBandLimits);
        float hsError = Mathf.Abs(gpuHs8 - cpu8.SignificantHeight)
                      / Mathf.Max(cpu8.SignificantHeight, 1e-4f);

        report.AppendLine($"Hs parity at U10=8: GPU {gpuHs8:F3} m, CPU " +
                          $"{cpu8.SignificantHeight:F3} m, error {hsError:P1}");
        if (hsError > 0.08f)
        {
            report.AppendLine("  RED: GPU and CPU significant wave height differ by more " +
                              "than 8%. Shore physics is not seeing the rendered sea.");
            failures++;
        }

        report.AppendLine();

        // --- 2. Stronger wind must raise the wave height ---
        env.speed = 3f; sim.Step(0f);
        float rms3 = TotalRms(sim, activeTiers);

        env.speed = 15f; sim.Step(0f);
        float rms15 = TotalRms(sim, activeTiers);

        // FOLDING IS MEASURED IN A STORM.
        //
        // At U10 = 8 the sea is gentle (Hs ~ 0.7 m) and J<0 is naturally zero.
        // Choppiness only shows it is doing work on a steep wave.
        float fold15 = 0f;
        float minJ15 = float.MaxValue;
        for (int k = 0; k < activeTiers; k++)
        {
            Measurement m = Measure(sim, k);
            fold15 = Mathf.Max(fold15, m.foldFraction);
            minJ15 = Mathf.Min(minJ15, m.minJ);
        }

        report.AppendLine($"total rms(h):  U10=3 -> {rms3:F4} m,  U10=15 -> {rms15:F4} m," +
                          $"  ratio {rms15 / Mathf.Max(rms3, 1e-6f):F2}x");

        report.AppendLine($"at U10=15 the highest J<0 fraction: {fold15 * 100f:F2}%," +
                          $" min(J) = {minJ15:F3}");

        // THE CRITERION IS `min(J) < 1`, NOT `J < 0`.
        //
        // Plan Phase 3 said "a J<0 fraction of 0% means choppiness is
        // ineffective"; that criterion is right for the open ocean but this
        // sea has a 12 km fetch and even at U10=15 Hs is about 1.4 m.
        // Measured: min(J) = 0.568 — the chain works, the surface really is
        // being sheared, it just does not steepen enough to fold. Were the
        // displacement not connected min(J) would be exactly 1.000; that is
        // the number that separates the two cases.
        //
        // Consequence: whitecaps are rare in this weather on the open sea.
        // Shore foam (spec §13.3) will be the dominant source.
        if (minJ15 > 0.9f)
        {
            report.AppendLine($"  RED: in a storm min(J) = {minJ15:F3} > 0.9." +
                              " Displacement derivatives are not feeding the Jacobian" +
                              " (spec §7).");
            failures++;
        }

        if (rms15 <= rms3 * 1.5f)
        {
            report.AppendLine("  RED: the wind went up fivefold and the wave height did" +
                              " not even grow 1.5x. The spectrum is not tied to the wind.");
            failures++;
        }

        // --- 3. A change of wind direction must turn the waves ---
        env.speed = 8f;
        env.direction = Vector3.right;   sim.Step(0f);
        float angleX = Measure(sim, 1).dominantAngle;

        env.direction = Vector3.forward; sim.Step(0f);
        float angleZ = Measure(sim, 1).dominantAngle;

        float turned = Mathf.Abs(Mathf.DeltaAngle(angleX * Mathf.Rad2Deg, angleZ * Mathf.Rad2Deg));
        report.AppendLine($"dominant slope direction: {angleX * Mathf.Rad2Deg,7:F1}° with +X wind," +
                          $" {angleZ * Mathf.Rad2Deg,7:F1}° with +Z wind, difference {turned:F1}°");

        if (turned < 45f)
        {
            report.AppendLine("  RED: the wind turned 90° and the wave direction turned" +
                              " less than 45°. Directional spreading is not tied to the wind.");
            failures++;
        }

        // --- 4. Swell must increase directional concentration ---
        env.direction = Vector3.right;

        float previousSwell = settings.swell;
        settings.swell = 0f; sim.Step(0f); float band0 = Measure(sim, 1).windBandFraction;
        settings.swell = 1f; sim.Step(0f); float band1 = Measure(sim, 1).windBandFraction;
        settings.swell = previousSwell;

        report.AppendLine($"energy share in the wind band (±30°): swell=0 -> {band0 * 100f:F1}%," +
                          $" swell=1 -> {band1 * 100f:F1}%");

        if (band1 <= band0 + 0.02f)
        {
            report.AppendLine("  RED: swell did not increase directional concentration." +
                              " Parallel wave trains will not form.");
            failures++;
        }

        Object.DestroyImmediate(go);

        report.AppendLine();
        report.AppendLine(failures == 0 ? "RESULT: passed." : $"RESULT: {failures} red.");

        if (failures == 0) Debug.Log(report.ToString());
        else Debug.LogError(report.ToString());
    }

    /// Reads one Tex2DArray slice back to the CPU. `RGBAFloat` is requested:
    /// half precision would be noise of the measurement's own making.
    static Color[] Read(RenderTexture rt, int slice, int n)
    {
        var request = AsyncGPUReadback.Request(rt, 0, 0, n, 0, n, slice, 1,
                                               TextureFormat.RGBAFloat);
        request.WaitForCompletion();

        if (request.hasError)
            throw new System.InvalidOperationException(
                "Sea test: the GPU readback failed.");

        return request.GetData<Color>().ToArray();
    }

    static float TotalRms(SeaSimulation sim, int tierCount)
    {
        float sumSquares = 0f;
        for (int k = 0; k < tierCount; k++)
        {
            float r = Measure(sim, k).rmsH;
            sumSquares += r * r;
        }
        return Mathf.Sqrt(sumSquares);
    }

    /// Reads one tier back to the CPU and measures it.
    ///
    /// `Graphics.CopyTexture` + `GetPixels` IS NOT USED. Those two speak to
    /// different memory: the copy updates the GPU side, `GetPixels` reads the
    /// CPU side. The result comes out silently constant — in the first
    /// measurement all tiers returned the same -23.20 and that was mistaken
    /// for a shader bug.
    ///
    /// The size comes from the TEXTURE, not from `SeaConstants.FftSize`: on
    /// the Low preset the running FFT is 128 and a readback of 256 would
    /// request more than the texture holds.
    static Measurement Measure(SeaSimulation sim, int tier)
    {
        int n = sim.Displacement.width;

        Color[] d = Read(sim.Displacement, tier, n);
        Color[] e = Read(sim.Derivatives, tier, n);

        double sumH = 0, sumH2 = 0, sumSlope2 = 0;
        int folded = 0;
        float minJ = float.MaxValue;

        // THE DOMINANT DIRECTION COMES FROM THE COVARIANCE, NOT THE MEAN.
        //
        // Wave slope is symmetric in sign: there is as much trough as crest,
        // so the mean of sx goes to zero whatever the wind does. The direction
        // only falls out of the principal axis of the slope covariance.
        double Sxx = 0, Szz = 0, Sxz = 0;
        double inBand = 0, bandTotal = 0;

        for (int i = 0; i < d.Length; i++)
        {
            float h = d[i].g;
            sumH += h;
            sumH2 += (double)h * h;

            if (d[i].a < 0f) folded++;
            minJ = Mathf.Min(minJ, d[i].a);

            float sx = e[i].r;
            float sz = e[i].g;
            double energy = (double)sx * sx + (double)sz * sz;
            sumSlope2 += energy;

            Sxx += (double)sx * sx;
            Szz += (double)sz * sz;
            Sxz += (double)sx * sz;

            float length = Mathf.Sqrt((float)energy);
            if (length > 1e-5f)
            {
                bandTotal += energy;

                // With the wind on +X the band is |theta| < 30°; the direction
                // test does not use this measure, and in the swell test the
                // wind is always +X.
                float cos = Mathf.Abs(sx) / length;
                if (cos > 0.8660254f) inBand += energy;
            }
        }

        return new Measurement
        {
            meanH = (float)(sumH / d.Length),
            rmsH = Mathf.Sqrt((float)(sumH2 / d.Length)),
            rmsSlope = Mathf.Sqrt((float)(sumSlope2 / d.Length)),
            foldFraction = folded / (float)d.Length,
            minJ = minJ,
            windBandFraction = bandTotal > 0 ? (float)(inBand / bandTotal) : 0f,
            dominantAngle = 0.5f * Mathf.Atan2((float)(2.0 * Sxz), (float)(Sxx - Szz)),
        };
    }
}
