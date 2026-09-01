// ROLE: lifecycle of the sea system. Publishes environment values as
// globals, bakes the bathymetry, fills in SeaRuntimeState.
// CALLED BY: nobody — runs on its own, dependencies come from the Inspector.

using System;
using UnityEngine;

/// THE SEA DOES NOT DRIVE, IT READS.
///
/// There is not, and will not be, a single line in this class that writes
/// `RenderSettings`, `VolumeProfile` or `Light.intensity` (spec §3.3, Phase 1
/// acceptance criterion). All it writes is `Shader.SetGlobal*` and
/// `SeaRuntimeState`.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SeaManager : MonoBehaviour
{
    [SerializeField] SeaSettings settings;

    [Tooltip("Environment source. Without it the system logs an error and disables itself.")]
    [SerializeField] SeaEnvironmentBridge environment;

    [Tooltip("Water depth is derived from this terrain.")]
    [SerializeField] Terrain terrain;

    ISeaEnvironmentSource env;
    Texture2D bathymetry;

    float bakedSeaLevel = float.NaN;

    /// `(beat angular frequency, beat depth, 0, 0)`.
    Vector4 waveGroups;

    public SeaSettings Settings => settings;

    public void Bind(SeaSettings source, SeaEnvironmentBridge bridge, Terrain target)
    {
        settings = source;
        environment = bridge;
        terrain = target;
    }

    void OnEnable()
    {
        env = environment;

        if (env == null)
        {
            // IT DOES NOT INVENT ITS OWN DEFAULT (spec §3.2).
            Debug.LogError($"{nameof(SeaManager)}: {nameof(environment)} is not assigned. " +
                           "Sea system disabled.");
            SeaRuntimeState.Active = false;
            enabled = false;
            return;
        }

        if (settings == null)
            throw new InvalidOperationException($"{nameof(SeaManager)}: {nameof(settings)} is not assigned.");

        if (terrain == null)
            throw new InvalidOperationException($"{nameof(SeaManager)}: {nameof(terrain)} is not assigned.");

        // MULTIPLE TERRAINS ARE NOT SUPPORTED (spec §9, §17).
        var all = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (all.Length > 1)
        {
            Debug.LogError($"{nameof(SeaManager)}: the scene has {all.Length} Terrains. " +
                           "The sea system supports a single terrain. Disabled.");
            SeaRuntimeState.Active = false;
            enabled = false;
            return;
        }

        RefreshBathymetry();
        SeaRuntimeState.Active = true;
    }

    void OnDisable()
    {
        SeaRuntimeState.Active = false;
        ReleaseBathymetry();
    }

    void ReleaseBathymetry()
    {
        if (bathymetry == null) return;

        if (Application.isPlaying) Destroy(bathymetry); else DestroyImmediate(bathymetry);
        bathymetry = null;
        bakedSeaLevel = float.NaN;
    }

    /// Called when the terrain or the sea level changes (spec §9).
    public void RefreshBathymetry()
    {
        ReleaseBathymetry();

        bathymetry = SeaBathymetry.Bake(terrain, settings.seaLevelY);
        bakedSeaLevel = settings.seaLevelY;
    }

    void Update()
    {
        if (env == null || settings == null || terrain == null) return;

        // If the sea level is changed from the Inspector the depth field goes
        // stale.
        if (!Mathf.Approximately(bakedSeaLevel, settings.seaLevelY))
            RefreshBathymetry();

        PublishEnvironment();
        PublishBathymetry();
        PublishSettings();
        UpdateState();
    }

    /// WIND IS THE MAIN INPUT OF THE SPECTRUM (spec §3.4).
    ///
    /// The sea DOES NOT build its own wind noise or gust simulation; it
    /// publishes what comes over the bridge.
    void PublishEnvironment()
    {
        Vector3 w = env.WindDirection * env.WindSpeed;
        Shader.SetGlobalVector(SeaShaderIDs.SeaWindWS, new Vector4(w.x, w.z, 0f, 0f));

        // LOOP-QUANTIZED TIME. Handing over `Time.time` directly would lose
        // float precision over a long session (spec §6.5).
        float t = Application.isPlaying ? Time.time : 0f;
        Shader.SetGlobalFloat(SeaShaderIDs.SeaTime, Mathf.Repeat(t, settings.loopPeriod));

        Shader.SetGlobalFloat(SeaShaderIDs.SunElevation01, env.SunElevation01);
        Shader.SetGlobalFloat(SeaShaderIDs.CloudCover01, env.CloudCover01);

        // Falling snow does not add foam to the sea surface — only rain
        // does (spec §13.5).
        float rain = env.PrecipKind == SeaPrecipitationKind.Rain
                   ? env.PrecipIntensity01 : 0f;
        Shader.SetGlobalFloat(SeaShaderIDs.PrecipIntensity01, rain);
    }

    void PublishBathymetry()
    {
        if (bathymetry == null) return;

        Vector3 o = terrain.transform.position;
        Vector3 s = terrain.terrainData.size;

        Shader.SetGlobalTexture(SeaShaderIDs.BathyTex, bathymetry);
        Shader.SetGlobalVector(SeaShaderIDs.BathyOriginXZ, new Vector4(o.x, o.z, 0f, 0f));
        Shader.SetGlobalVector(SeaShaderIDs.BathySizeXZ, new Vector4(s.x, s.z, 0f, 0f));
        Shader.SetGlobalFloat(SeaShaderIDs.BathyResolution, bathymetry.width);
        Shader.SetGlobalFloat(SeaShaderIDs.DeepWaterDepth, settings.deepWaterDepth);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaLevelY, settings.seaLevelY);
    }

    void PublishSettings()
    {
        SeaQuality.Levels level = SeaQuality.Of(settings.quality);
        SeaQuality.Apply(settings.quality);

        // THE WEIGHT OF AN UNUSED TIER IS ZERO.
        //
        // On Low the third tier is never computed; without zeroing its weight
        // the surface would read that tier's STALE texture and a frozen chop
        // layer would show.
        Vector3 weight = settings.tierWeights;
        if (level.TierCount < 3) weight.z = 0f;
        if (level.TierCount < 2) weight.y = 0f;

        Shader.SetGlobalVector(SeaShaderIDs.PatchSizes, settings.patchSizes);
        Shader.SetGlobalVector(SeaShaderIDs.TierWeights, weight);
        Shader.SetGlobalVector(SeaShaderIDs.ChoppinessPerTier, settings.choppinessPerTier);

        Shader.SetGlobalFloat(SeaShaderIDs.SpectrumDepth, settings.spectrumDepth);
        Shader.SetGlobalFloat(SeaShaderIDs.Fetch, settings.fetch);
        Shader.SetGlobalFloat(SeaShaderIDs.Swell, settings.swell);
        Shader.SetGlobalFloat(SeaShaderIDs.SmallWaveCutoff, settings.smallWaveCutoff);
        Shader.SetGlobalFloat(SeaShaderIDs.LoopPeriod, settings.loopPeriod);
        Shader.SetGlobalFloat(SeaShaderIDs.Choppiness,
                              settings.ChoppinessAt(env.WindSpeed));

        Shader.SetGlobalFloat(SeaShaderIDs.MaxShoalingGain, settings.maxShoalingGain);

        Shader.SetGlobalVector(SeaShaderIDs.ExtinctionRGB, settings.extinctionRgb);
        Shader.SetGlobalColor(SeaShaderIDs.UpwellingColor, settings.upwellingColor);
        Shader.SetGlobalFloat(SeaShaderIDs.RefractionStrength, settings.refractionStrength);
        Shader.SetGlobalFloat(SeaShaderIDs.RoughnessCalm, settings.roughnessCalm);
        Shader.SetGlobalFloat(SeaShaderIDs.RoughnessRough, settings.roughnessRough);

        Shader.SetGlobalFloat(SeaShaderIDs.ShoreFoamDepth, settings.shoreFoamDepth);
        Shader.SetGlobalColor(SeaShaderIDs.FoamColor, settings.foamColor);
        Shader.SetGlobalFloat(SeaShaderIDs.FoamRoughness, settings.foamRoughness);
        Shader.SetGlobalFloat(SeaShaderIDs.FoamTiling, settings.foamTiling);
        Shader.SetGlobalFloat(SeaShaderIDs.FoamBreakupTiling, settings.foamBreakupTiling);
    }

    /// EVERY INPUT OF THE INTEGRATION, NOT JUST THE WIND. Cached on the wind
    /// alone, editing the swell in the Inspector would leave Hs and Tp on the
    /// old spectrum with no error anywhere.
    readonly struct MomentInputs
    {
        readonly float wind, fetch, depth, swellPeriod, swellAlpha, swellGamma;

        public MomentInputs(float wind, SeaSettings s)
        {
            this.wind = wind;
            fetch = s.fetch;
            depth = s.spectrumDepth;
            swellPeriod = s.swellPeriod;
            swellAlpha = s.swellAlpha;
            swellGamma = s.swellGamma;
        }

        /// The wind moves continuously, so it gets a dead band; the rest are
        /// Inspector fields and any change at all counts.
        ///
        /// 0.1 m/s of dead band is worth 1 cm of Hs at 8 m/s (measured slope
        /// 0.11 m per m/s). Without it a gusting wind would re-integrate 1600
        /// steps every frame for a number that does not move.
        public bool Matches(in MomentInputs other)
        {
            return Mathf.Abs(wind - other.wind) < 0.1f
                && fetch == other.fetch
                && depth == other.depth
                && swellPeriod == other.swellPeriod
                && swellAlpha == other.swellAlpha
                && swellGamma == other.swellGamma;
        }
    }

    MomentInputs momentInputs;

    /// A default `MomentInputs` is all zeros, which could in principle match a
    /// real one. The flag says "nothing has been integrated yet" without relying
    /// on a sentinel value.
    bool momentsValid;

    /// THE SEA STATE COMES FROM THE SPECTRUM THAT IS RUNNING.
    ///
    /// Hs and Tp used to be read off the fetch-limited JONSWAP relations. Those
    /// describe the WIND SEA only, and the spectrum has a second partition: a
    /// swell with its own peak period and a fixed energy. In a dead calm the wind
    /// sea is nothing and the swell is everything, so both numbers were wrong
    /// exactly where it showed — the shore was surging with a 2.6 second period
    /// while the swell running under it is ten seconds long (`RATIONALE.md`).
    ///
    /// `SeaSpectrumMoments` integrates both partitions. It is not free (about
    /// 1600 steps), so it runs only when the wind actually changes.
    void UpdateState()
    {
        float u = Mathf.Max(env.WindSpeed, 0.1f);

        var inputs = new MomentInputs(u, settings);

        if (!momentsValid || !momentInputs.Matches(inputs))
        {
            SeaSpectrumMoments.Result m = SeaSpectrumMoments.Integrate(u, settings);
            SeaRuntimeState.SignificantWaveHeight = m.SignificantHeight;
            SeaRuntimeState.PeakPeriod = m.PeakPeriod;
            waveGroups = new Vector4(SeaConstants.TwoPi / Mathf.Max(m.BeatPeriod, 0.1f),
                                     m.BeatDepth, 0f, 0f);
            momentInputs = inputs;
            momentsValid = true;
        }

        Shader.SetGlobalFloat(SeaShaderIDs.PeakPeriod, SeaRuntimeState.PeakPeriod);

        // THE SURFACE SHADER NEEDS Hs, NOT THE HUD ONLY. The breaking
        // criterion (spec 8.3) compares the WAVE'S height with the depth; a
        // pixel only knows its own elevation, which is not the same thing.
        Shader.SetGlobalFloat(SeaShaderIDs.SignificantHeight,
                              SeaRuntimeState.SignificantWaveHeight);

        // THE SETS. Where the surf zone's outer edge stands depends on how big the
        // waves arriving right now are, and that changes from wave to wave because the
        // spectrum has two peaks. Without it the edge sits on one depth contour and
        // draws the shoreline's own curve.
        Shader.SetGlobalVector(SeaShaderIDs.WaveGroups, waveGroups);

        // THE SLOPE THE CASCADES CANNOT CARRY.
        //
        // Cox and Munk measured the sea's slope variance from sun glitter:
        // sigma^2 = 0.003 + 0.00512 U10. Measured against it, this surface
        // reaches 0.90x at 5 m/s and 0.54x at 20 m/s -- the gap grows with the
        // wind because the missing waves are the SHORT ones, and a stronger wind
        // puts more of its slope down there. That gap is what reads as jelly.
        //
        // WHAT THE MICRO LAYER OWES IS NOT A TASTE NUMBER. In the equilibrium
        // range the slope spectrum goes as 1/k, so every octave carries the same
        // slope variance. The band the cascades miss runs from 14 cm (the finest
        // patch over its resolution) down to the capillary crossover at 1.7 cm --
        // three octaves. Its share is those three out of every octave between the
        // spectral peak and that crossover.
        //
        // This closes the measured gap only part of the way (0.54x -> 0.77x at
        // 20 m/s). The rest sits in the JONSWAP tail inside the cascades' own
        // band, which is a separate correction and is NOT smuggled in here.
        const float MicroLongest = 0.14f;
        const float MicroShortest = 0.017f;

        float peakLength = SeaConstants.G * SeaRuntimeState.PeakPeriod
                         * SeaRuntimeState.PeakPeriod / SeaConstants.TwoPi;

        float octavesTotal = Mathf.Log(Mathf.Max(peakLength, MicroShortest * 2f)
                                       / MicroShortest, 2f);
        float octavesMissing = Mathf.Log(MicroLongest / MicroShortest, 2f);

        float coxMunk = 0.003f + 0.00512f * Mathf.Max(env.WindSpeed, 0f);
        float share = Mathf.Clamp01(octavesMissing / Mathf.Max(octavesTotal, 1f));

        Shader.SetGlobalFloat(SeaShaderIDs.MicroSlopeVariance, coxMunk * share);

        // HOW HIGH THE SWASH REACHES — STOCKDON, NOT A FIXED NUMBER.
        //
        // `runupMaxDepth` was 1.1 m whatever the sea was doing; on the measured 5.8%
        // shore that is 19 m of beach, and the water ran that far up in a dead calm.
        // R2% is the standard run-up parametrisation and it takes the sea state:
        //
        //     R2% = 1.1 ( 0.35 b sqrt(Hs L0) + sqrt(Hs L0 (0.563 b^2 + 0.004)) / 2 )
        //
        // with `b` the beach slope and `L0 = g Tp^2 / 2pi` the deep-water wavelength.
        // Measured against the same shore: 0.69 m (12 m) in a calm, 1.60 m (28 m) at
        // 20 m/s. [SOURCE: Stockdon et al. 2006]
        float l0 = SeaConstants.G * SeaRuntimeState.PeakPeriod * SeaRuntimeState.PeakPeriod
                 / SeaConstants.TwoPi;
        float b = settings.shoreSlope;
        float hsl0 = SeaRuntimeState.SignificantWaveHeight * l0;

        SeaRuntimeState.RunupHeight =
            1.1f * (0.35f * b * Mathf.Sqrt(hsl0)
                  + Mathf.Sqrt(hsl0 * (0.563f * b * b + 0.004f)) * 0.5f);

        Shader.SetGlobalFloat(SeaShaderIDs.RunupMaxDepth, SeaRuntimeState.RunupHeight);

        // THE PHASE IS A PHASE, NOT AN AMOUNT.
        //
        // This published `sin(...) * 0.5 + 0.5` — the surge AMOUNT — under the name
        // `_SeaShoreFoamPhase`, and the shader fed it straight into
        // `0.5 - 0.5 cos(2pi * phase)`. Running an amount through a cosine folds the
        // cycle in half: as the value swept 0.5 -> 1 -> 0.5 -> 0 -> 0.5 over one Tp,
        // the surge went 1 -> 0 -> 1 -> 0 -> 1. The swash ran at Tp/2 and lurched at
        // the turns. That is the "it still goes in and out too fast".
        //
        // A linear 0..1 sawtooth goes out now, and the surge is built from it in ONE
        // place — the same expression the shader uses, so the wet band and the foam
        // cannot drift apart.
        // THE SWASH DOES NOT RUN AT THE WAVE PERIOD.
        //
        // It used to: one swash per incoming wave, so the water went up and down
        // like a metronome. Measured on real beaches the swash period is ONE TO
        // THREE TIMES the incident period -- the backwash of one wave is still
        // draining when the next bore arrives, the two collide, and the pair
        // counts as a single swash. [SOURCE: Coastal Wiki, Swash zone dynamics]
        //
        // The period follows from the ballistic swash model, not a made-up
        // multiplier. A bore that can climb to R leaves the shoreline at
        // V = sqrt(2 g R) and decelerates at g*beta, so the run up takes
        //
        //     T_up = sqrt(2 R / g) / beta
        //
        // and the drain back is longer: field measurements put flow reversal at
        // 40-50% of the swash cycle. Measured here: R = 0.89 m on a 5.8% shore
        // gives T_up 7.3 s and a full swash of 16.9 s against a 6.9 s wave
        // period -- a ratio of 2.4, inside the observed 1-3 band, and it moves
        // with the sea state instead of being pinned to it.
        const float BackwashRatio = 1.3f;

        float tUp = Mathf.Sqrt(2f * SeaRuntimeState.RunupHeight / SeaConstants.G)
                  / Mathf.Max(b, 1e-3f);
        float swashPeriod = Mathf.Clamp(tUp * (1f + BackwashRatio), 2f, 40f);
        float uprushFraction = 1f / (1f + BackwashRatio);

        float t = Application.isPlaying ? Time.time : 0f;
        float phase = Mathf.Repeat(t / swashPeriod, 1f);

        SeaRuntimeState.ShoreFoamIntensity01 = SeaSwashSurge(phase, uprushFraction);

        Shader.SetGlobalFloat(SeaShaderIDs.ShoreFoamPhase, phase);
        Shader.SetGlobalFloat(SeaShaderIDs.SwashUprush, uprushFraction);
    }

    /// THE SWASH IS NOT A COSINE. `0.5 - 0.5 cos(2pi phase)` is symmetric, so the
    /// water withdrew exactly as fast as it arrived. A real swash rushes up and
    /// drains back slowly, and the ballistic model says why: going up it is a body
    /// thrown against gravity, so the height follows `s (2 - s)` -- quick at the
    /// start, easing into the turn. [SOURCE: Shen & Meyer 1963 ballistic swash]
    ///
    /// MIRRORED IN `SeaCommon.hlsl`. The shader draws the foam and the wet sand
    /// from the same curve; two copies that drift apart would put the foam and the
    /// waterline in different places.
    public static float SeaSwashSurge(float phase, float uprushFraction)
    {
        float up = Mathf.Clamp(uprushFraction, 0.05f, 0.95f);
        float s = phase < up ? phase / up : 1f - (phase - up) / (1f - up);
        return s * (2f - s);
    }
}
