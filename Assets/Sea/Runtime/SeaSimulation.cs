// ROLE: produces the wave field. Drives the spectrum and IFFT compute
// shaders and publishes the result as global textures.
// CALLED BY: nobody — runs on its own, dependencies come from the Inspector.

using System;
using UnityEngine;
using UnityEngine.Rendering;

/// THE WAVE FIELD EVERY FRAME, THE SPECTRUM ONLY WHEN THE WIND CHANGES.
///
/// `KInitialSpectrum` is expensive and its result does not change while the
/// wind is steady; running it every frame is waste (spec §15.2). The
/// thresholds come from there: 0.25 m/s of speed, 3° of direction.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SeaSimulation : MonoBehaviour
{
    [SerializeField] SeaSettings settings;
    [SerializeField] SeaEnvironmentBridge environment;
    [SerializeField] ComputeShader spectrumShader;
    [SerializeField] ComputeShader fftShader;
    [SerializeField] ComputeShader foamShader;

    [Tooltip("When the sea is not visible no compute runs at all (spec §15.2).")]
    [SerializeField] SeaSurface surface;

    ISeaEnvironmentSource env;

    RenderTexture h0;
    RenderTexture spectrumHt;
    RenderTexture spectrumSlope;
    RenderTexture displacement;
    RenderTexture derivatives;
    RenderTexture slopeMoments;

    // FOAM PING-PONG. The decay reads the previous frame's value; reading and
    // writing a single texture would create a race.
    RenderTexture foamA;
    RenderTexture foamB;

    int kInitial = -1;
    int kTime = -1;
    int kFftH = -1;
    int kFftV = -1;
    int kAssemble = -1;
    int kFoam = -1;

    float lastWindSpeed = float.NaN;
    Vector3 lastWindDir = Vector3.zero;
    SpectrumInputs lastSpectrumSignature;

    /// EVERY INPUT `h0` DEPENDS ON. It outgrew a `Vector4` when the swell
    /// partition was added; packing nine numbers into four by hashing them
    /// would trade a visible bug for a silent one — a collision means the
    /// spectrum is never rebuilt and the Inspector value does nothing.
    readonly struct SpectrumInputs : System.IEquatable<SpectrumInputs>
    {
        readonly float swell, fetch, depth, cutoff;
        readonly float swellAlpha, swellOmega, swellGamma, swellSpread, swellDir;

        public SpectrumInputs(SeaSettings s, float swellPeriod, float swellEnergy)
        {
            swell = s.swell;
            fetch = s.fetch;
            depth = s.spectrumDepth;
            cutoff = s.smallWaveCutoff;
            swellAlpha = s.swellAlpha * swellEnergy;
            swellOmega = swellPeriod;
            swellGamma = s.swellGamma;
            swellSpread = s.swellSpread;
            swellDir = s.swellDirectionOffset;
        }

        public bool Equals(SpectrumInputs o) =>
            swell == o.swell && fetch == o.fetch && depth == o.depth
            && cutoff == o.cutoff && swellAlpha == o.swellAlpha
            && swellOmega == o.swellOmega && swellGamma == o.swellGamma
            && swellSpread == o.swellSpread && swellDir == o.swellDir;

        public override bool Equals(object o) => o is SpectrumInputs i && Equals(i);

        public override int GetHashCode() => swell.GetHashCode() ^ fetch.GetHashCode();
    }

    /// The spectrum is rebuilt whenever an input changes, so the event's energy moves in
    /// steps for the same reason its period does.
    static float Quantized(float v) => Mathf.Round(v * 20f) * 0.05f;

    int builtFftSize = -1;
    float lastFoamTime = float.NaN;
    SeaProfiler profiler;

    /// The Phase 2 numeric verification reads these textures.
    public RenderTexture Displacement => displacement;
    public RenderTexture Derivatives => derivatives;
    public RenderTexture SlopeMoments => slopeMoments;
    public RenderTexture H0 => h0;
    public RenderTexture Foam => foamA;

    public void Bind(SeaSettings source, SeaEnvironmentBridge bridge,
                     ComputeShader spectrum, ComputeShader fft, ComputeShader foam,
                     SeaSurface visibilitySource)
    {
        environment = bridge;
        surface = visibilitySource;
        Bind(source, (ISeaEnvironmentSource)bridge, spectrum, fft, foam);
    }

    /// BINDING THROUGH THE INTERFACE.
    ///
    /// `ISeaEnvironmentSource` exists exactly for this: pinning the wind to a
    /// known value so the wave field can be measured. The numeric
    /// verification (`SeaSpectrumTest`) uses this path — if the wind came
    /// from the weather system the measurement would not be repeatable.
    public void Bind(SeaSettings source, ISeaEnvironmentSource bridge,
                     ComputeShader spectrum, ComputeShader fft, ComputeShader foam)
    {
        settings = source;
        env = bridge;
        spectrumShader = spectrum;
        fftShader = fft;
        foamShader = foam;
    }

    void OnEnable()
    {
        // If `Bind` was called through the interface the serialized bridge
        // may be empty; in that case it is not overwritten.
        if (env == null) env = environment;

        if (env == null)
        {
            Debug.LogError($"{nameof(SeaSimulation)}: {nameof(environment)} is not assigned. " +
                           "The wave field is not produced.");
            enabled = false;
            return;
        }

        if (settings == null)
            throw new InvalidOperationException($"{nameof(SeaSimulation)}: {nameof(settings)} is not assigned.");
        if (spectrumShader == null)
            throw new InvalidOperationException($"{nameof(SeaSimulation)}: {nameof(spectrumShader)} is not assigned.");
        if (fftShader == null)
            throw new InvalidOperationException($"{nameof(SeaSimulation)}: {nameof(fftShader)} is not assigned.");
        if (foamShader == null)
            throw new InvalidOperationException($"{nameof(SeaSimulation)}: {nameof(foamShader)} is not assigned.");

        // KERNEL EXISTENCE IS CHECKED EXPLICITLY.
        //
        // `GetComputeShaderMessages` can come back empty while `FindKernel`
        // still throws — the snow system burned a round on that. The error is
        // not swallowed; it is thrown straight away.
        kInitial = spectrumShader.FindKernel("KInitialSpectrum");
        kTime = spectrumShader.FindKernel("KTimeSpectrum");
        kFftH = fftShader.FindKernel("KIFFTHorizontal");
        kFftV = fftShader.FindKernel("KIFFTVertical");
        kAssemble = fftShader.FindKernel("KAssemble");
        kFoam = foamShader.FindKernel("KFoam");

        CreateTextures();

        // Make sure the wind threshold fires on the very first frame.
        lastWindSpeed = float.NaN;
    }

    void OnDisable()
    {
        ReleaseTextures();
    }

    /// `GetTemporary` IS NOT USED (spec §15.2). Textures are created once and
    /// released in `OnDisable`.
    /// `mips` is for the DERIVATIVE texture alone. The surface samples slopes at
    /// grazing angles where one pixel covers tens of metres of water; without a mip
    /// chain the hardware picks a single texel out of that span and TAA turns the
    /// result into noise. Averaging is what makes distant water flat, and it has to
    /// be the hardware doing it per pixel -- a blanket distance fade cannot, because
    /// it also kills the waves that ARE resolved (measured: the surface went mirror
    /// at 520 m, where a 2 m wave still spans 7.1 pixels).
    RenderTexture Create(string label, RenderTextureFormat format, bool mips = false)
    {
        int n = SeaQuality.Of(settings.quality).FftSize;

        var rt = new RenderTexture(n, n, 0, format)
        {
            name = label,
            enableRandomWrite = true,
            filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear,

            // MANDATORY: the mesh surface samples from world coordinates and
            // the texture must repeat past the patch boundary (spec §10.4).
            wrapMode = TextureWrapMode.Repeat,

            useMipMap = mips,

            // The compute kernel writes mip 0; the chain is built by hand after the
            // dispatch, so Unity must not try to build it on render-target set.
            autoGenerateMips = false,
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = SeaConstants.TierCount,
            hideFlags = HideFlags.DontSave,
        };

        rt.Create();
        return rt;
    }

    void CreateTextures()
    {
        ReleaseTextures();
        builtFftSize = SeaQuality.Of(settings.quality).FftSize;

        // Half precision is enough for the FFT; `Float` would be twice the
        // bandwidth with no visual difference (spec §6.8).
        h0 = Create("Sea_H0", RenderTextureFormat.ARGBHalf);
        spectrumHt = Create("Sea_SpectrumHt", RenderTextureFormat.ARGBHalf);
        spectrumSlope = Create("Sea_SpectrumSlope", RenderTextureFormat.ARGBHalf);
        displacement = Create("Sea_Displacement", RenderTextureFormat.ARGBHalf);
        derivatives = Create("Sea_Derivatives", RenderTextureFormat.ARGBHalf, mips: true);
        slopeMoments = Create("Sea_SlopeMoments", RenderTextureFormat.ARGBHalf, mips: true);

        foamA = Create("Sea_FoamA", RenderTextureFormat.RHalf);
        foamB = Create("Sea_FoamB", RenderTextureFormat.RHalf);

        // FOAM ACCUMULATES: the first frame's "previous" value must be
        // defined. A fresh `RenderTexture` has undefined contents; without
        // clearing, foam starts decaying from a random level.
        Clear(foamA);
        Clear(foamB);
    }

    static void Clear(RenderTexture rt)
    {
        var previous = RenderTexture.active;

        for (int slice = 0; slice < SeaConstants.TierCount; slice++)
        {
            Graphics.SetRenderTarget(rt, 0, CubemapFace.Unknown, slice);
            GL.Clear(false, true, Color.clear);
        }

        RenderTexture.active = previous;
    }

    void ReleaseTextures()
    {
        Release(ref h0);
        Release(ref spectrumHt);
        Release(ref spectrumSlope);
        Release(ref displacement);
        Release(ref derivatives);
        Release(ref slopeMoments);
        Release(ref foamA);
        Release(ref foamB);
    }

    static void Release(ref RenderTexture rt)
    {
        if (rt == null) return;

        rt.Release();
        if (Application.isPlaying) Destroy(rt); else DestroyImmediate(rt);
        rt = null;
    }

    void Update()
    {
        if (env == null || settings == null) return;

        // A QUALITY CHANGE REBUILDS THE TEXTURES. Their size changes, so the
        // old ones cannot be used as they are.
        if (displacement == null || !displacement.IsCreated()
            || builtFftSize != SeaQuality.Of(settings.quality).FftSize)
            CreateTextures();

        // WHEN THE SEA IS NOT VISIBLE NOTHING RUNS (spec §15.2).
        //
        // Turning the camera away from the sea drops every compute pass. If
        // `surface` is not bound the gate is left open — silencing the
        // simulation when visibility is unknown would be a quiet "the sea is
        // frozen" bug.
        if (surface != null && !surface.IsVisible)
        {
            SeaRuntimeState.SimulationActive = false;
            profiler?.Skipped();
            return;
        }

        SeaRuntimeState.SimulationActive = true;

        Step(Application.isPlaying ? Time.time : 0f);
    }

    /// One simulation step. The editor test calls this too.
    public void Step(float time)
    {
        profiler ??= new SeaProfiler("Sea.Simulation");
        profiler.Begin();
        StepBody(time);
        profiler.End();
    }

    void StepBody(float time)
    {
        Vector3 direction = env.WindDirection;
        float speed = env.WindSpeed;

        // WIND IS NOT THE ONLY INPUT.
        //
        // Spec §15.2 only gives the wind threshold, but `h0` also depends on
        // swell, fetch, depth and cutoff length. Watching the wind alone
        // means changing swell from the Inspector does nothing — measured:
        // with swell 0 versus 1 the directional concentration came out
        // exactly the same.
        var signature = new SpectrumInputs(settings, environment.SwellPeriod,
                                          Quantized(environment.SwellEnergyScale));

        bool dirty = float.IsNaN(lastWindSpeed)
                  || Mathf.Abs(speed - lastWindSpeed) > 0.25f
                  || Vector3.Angle(direction, lastWindDir) > 3f
                  || !signature.Equals(lastSpectrumSignature);

        WriteSettings(spectrumShader, direction, speed);
        WriteSettings(fftShader, direction, speed);

        if (dirty)
        {
            InitialSpectrum();
            lastWindSpeed = speed;
            lastWindDir = direction;
            lastSpectrumSignature = signature;
        }

        // LOOP-QUANTIZED TIME. Handing over `Time.time` directly loses float
        // precision over a long session (spec §6.5).
        spectrumShader.SetFloat(SeaShaderIDs.SeaTime,
                                Mathf.Repeat(time, settings.loopPeriod));

        SeaQuality.Levels level = SeaQuality.Of(settings.quality);
        int groups = level.FftSize / 8;

        spectrumShader.SetTexture(kTime, SeaShaderIDs.H0RW, h0);
        spectrumShader.SetTexture(kTime, SeaShaderIDs.SpectrumHtRW, spectrumHt);
        spectrumShader.SetTexture(kTime, SeaShaderIDs.SpectrumSlopeRW, spectrumSlope);
        spectrumShader.Dispatch(kTime, groups, groups, level.TierCount);

        FftPass(kFftH);
        FftPass(kFftV);

        fftShader.SetTexture(kAssemble, SeaShaderIDs.SpectrumHtRW, spectrumHt);
        fftShader.SetTexture(kAssemble, SeaShaderIDs.SpectrumSlopeRW, spectrumSlope);
        fftShader.SetTexture(kAssemble, SeaShaderIDs.DisplacementRW, displacement);
        fftShader.SetTexture(kAssemble, SeaShaderIDs.DerivativesRW, derivatives);
        fftShader.SetTexture(kAssemble, SeaShaderIDs.SlopeMomentsRW, slopeMoments);
        fftShader.Dispatch(kAssemble, groups, groups, level.TierCount);

        // Mip 0 has just been overwritten; the rest of the chain is now stale.
        derivatives.GenerateMips();
        // The moments must be filtered by the SAME hardware that filters the slopes:
        // the variance is the difference between the two, so a different filter on
        // either side would not cancel.
        slopeMoments.GenerateMips();

        FoamPass(time);

        Shader.SetGlobalTexture(SeaShaderIDs.Displacement, displacement);
        Shader.SetGlobalTexture(SeaShaderIDs.Derivatives, derivatives);
        Shader.SetGlobalTexture(SeaShaderIDs.SlopeMoments, slopeMoments);
        Shader.SetGlobalTexture(SeaShaderIDs.Foam, foamA);
    }

    /// FOAM DECAY FOLLOWS THE ACTUAL ELAPSED TIME.
    ///
    /// `Time.deltaTime` cannot be used: the editor test calls `Step` with its
    /// own time and there are no frames there. The delta is derived from the
    /// value the caller passed in.
    void FoamPass(float time)
    {
        float dt = float.IsNaN(lastFoamTime) ? 0f : Mathf.Max(time - lastFoamTime, 0f);
        lastFoamTime = time;

        // When the loop wraps the difference goes negative; the `Max` above
        // clamps it to zero and foam only jumps to its target that frame.
        foamShader.SetFloat(SeaShaderIDs.DeltaTime, dt);

        foamShader.SetTexture(kFoam, SeaShaderIDs.DisplacementRW, displacement);
        foamShader.SetTexture(kFoam, SeaShaderIDs.FoamRW, foamB);
        foamShader.SetTexture(kFoam, SeaShaderIDs.FoamPrevRW, foamA);

        SeaQuality.Levels level = SeaQuality.Of(settings.quality);
        int groups = level.FftSize / 8;
        foamShader.Dispatch(kFoam, groups, groups, level.TierCount);

        (foamA, foamB) = (foamB, foamA);
    }

    void InitialSpectrum()
    {
        SeaQuality.Levels level = SeaQuality.Of(settings.quality);
        int groups = level.FftSize / 8;

        spectrumShader.SetTexture(kInitial, SeaShaderIDs.H0RW, h0);
        spectrumShader.Dispatch(kInitial, groups, groups, level.TierCount);
    }

    void FftPass(int kernel)
    {
        fftShader.SetTexture(kernel, SeaShaderIDs.SpectrumHtRW, spectrumHt);
        fftShader.SetTexture(kernel, SeaShaderIDs.SpectrumSlopeRW, spectrumSlope);

        // One row per group. The thread count is always `SEA_FFT_SIZE`; when
        // the running FFT is smaller the extra threads spin idle but keep
        // entering the barriers (`SeaFFT.compute`).
        SeaQuality.Levels level = SeaQuality.Of(settings.quality);
        fftShader.Dispatch(kernel, 1, level.FftSize, level.TierCount);
    }

    /// COMPUTE SHADER GLOBALS ARE WRITTEN SEPARATELY.
    ///
    /// `Shader.SetGlobal*` does not reach a compute shader reliably; the
    /// values are written straight onto the compute shader. The globals
    /// `SeaManager` publishes are for the surface shader.
    void WriteSettings(ComputeShader cs, Vector3 direction, float speed)
    {
        Vector3 w = direction * speed;
        cs.SetVector(SeaShaderIDs.SeaWindWS, new Vector4(w.x, w.z, 0f, 0f));

        cs.SetVector(SeaShaderIDs.PatchSizes, settings.patchSizes);
        cs.SetVector(SeaShaderIDs.ChoppinessPerTier, settings.choppinessPerTier);
        // CHOPPINESS RIDES THE WIND. See the reasoning next to the fields:
        // held constant, the surface never folded and no whitecap was ever born.
        cs.SetFloat(SeaShaderIDs.Choppiness, settings.ChoppinessAt(speed));
        cs.SetFloat(SeaShaderIDs.SpectrumDepth, settings.spectrumDepth);
        cs.SetFloat(SeaShaderIDs.Fetch, settings.fetch);
        cs.SetFloat(SeaShaderIDs.Swell, settings.swell);

        // THE SWELL'S PEAK COMES FROM A PERIOD, NOT A FETCH. A swell is born in
        // a storm we do not simulate; its period is what survives the journey.
        cs.SetFloat(SeaShaderIDs.SwellAlpha,
                    settings.swellAlpha * Quantized(environment.SwellEnergyScale));
        cs.SetFloat(SeaShaderIDs.SwellPeakOmega,
                    SeaConstants.TwoPi / Mathf.Max(1f, environment.SwellPeriod));
        cs.SetFloat(SeaShaderIDs.SwellGamma, settings.swellGamma);
        cs.SetFloat(SeaShaderIDs.SwellSpreadS, settings.swellSpread);
        cs.SetFloat(SeaShaderIDs.SwellDirOffset,
                    settings.swellDirectionOffset * Mathf.Deg2Rad);
        cs.SetFloat(SeaShaderIDs.SmallWaveCutoff, settings.smallWaveCutoff);
        cs.SetFloat(SeaShaderIDs.LoopPeriod, settings.loopPeriod);

        // The long end of the coarsest tier is not limited: there is no tier above it.
        Vector2 band = settings.TierBandLimits;
        cs.SetVector(SeaShaderIDs.TierCutoffK, new Vector4(band.x, band.y, 1e9f, 0f));

        SeaQuality.Levels level = SeaQuality.Of(settings.quality);
        cs.SetInt(SeaShaderIDs.FftSize, level.FftSize);
        cs.SetInt(SeaShaderIDs.FftLog2, level.FftLog2);
    }
}
