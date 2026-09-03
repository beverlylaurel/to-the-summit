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
    Vector4 tierSlopeVariance;

    /// The shore's own geometry: found once, because the terrain does not move.
    Vector2 shoreAnchor;
    Vector2 shoreNormal;
    bool shoreAnchorValid;

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
        Vector4 weight = settings.tierWeights;
        if (level.TierCount < 4) weight.w = 0f;
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

    /// WHY A BREAKING WAVE DOES NOT BREAK ALL AT ONCE.
    ///
    /// A crest that meets the beach exactly parallel breaks along its whole length in the
    /// same instant. Nothing on a real coast does that, and surfers have a name for the
    /// difference: the PEEL ANGLE, between the trail of broken water and the crest still
    /// standing. Zero degrees is the wave that shuts down in one go; 30 to 70 degrees is
    /// the range anyone can ride. [SOURCE: Scarfe 2002; Mead & Black]
    ///
    /// The angle is not invented here, it is what survives refraction. Snell's law for
    /// water waves keeps `sin(theta) / c` constant, and `c` collapses from `gT/2pi` in
    /// deep water to `sqrt(g h)` at the breaker line, so a swell arriving at `theta_0`
    /// still meets the shore at a small residual `theta_b`. That residual IS the peel.
    ///
    /// ONE GLOBAL VECTOR, NOT A PER-PIXEL DIRECTION. The phase has to stay integrable:
    /// feeding the local bathymetry gradient into it once already drew the crests as
    /// rings following every depth contour (`SYMPTOMS.md`). The shore normal is therefore
    /// sampled ONCE per frame, near the viewer, and the along-shore term it produces is a
    /// plain linear phase.
    void PublishPeel(float slope)
    {
        // THE SHORE DOES NOT MOVE WITH THE PLAYER, AND NEITHER MAY ITS PHASE.
        //
        // Both halves of this were wrong and the symptom was one: walking nine metres
        // along the beach swung the phase at a FIXED patch of water by 18.3 radians --
        // 2.9 whole crest-to-trough cycles. Standing water rose and fell because the
        // viewer walked.
        //
        //   * the normal was sampled UNDER THE CAMERA, so it changed as the player moved;
        //   * the phase was `dot(peel, posXZ)` with no origin, and posXZ is about 13500,
        //     so a change of 0.001 in the peel moved the phase by 13 radians.
        //
        // The anchor fixes the second even when the peel legitimately changes -- the wind
        // does turn. Measured at the anchor the phase is zero whatever the peel is, and a
        // hundred metres out a one percent peel change now moves it by 0.09 radians.
        EnsureShoreAnchor();

        // A zero peel still has to carry the anchor: the shader subtracts it either way.
        Vector4 flat = new Vector4(0f, 0f, shoreAnchor.x, shoreAnchor.y);

        Vector2 up = shoreNormal;
        if (up.sqrMagnitude < 1e-8f) { Shader.SetGlobalVector(SeaShaderIDs.ShorePeel, flat); return; }

        // THE SWELL'S HEADING, NOT THE WIND'S.
        //
        // What breaks on the beach is the swell, and the swell was born in a storm that
        // is not today's weather -- the spectrum already says so, `swellDirectionOffset`
        // turns it away from the local wind. Driving the peel off the wind instead made
        // the crests almost shore-normal and the peel angle came out 5 degrees: still a
        // close-out. The offset is what puts the wave on the beach at an angle.
        Vector3 wd = env.WindDirection;
        Vector2 travel = new Vector2(wd.x, wd.z);
        if (travel.sqrMagnitude < 1e-8f) { Shader.SetGlobalVector(SeaShaderIDs.ShorePeel, flat); return; }
        travel.Normalize();

        float off = settings.swellDirectionOffset * Mathf.Deg2Rad;
        travel = new Vector2(travel.x * Mathf.Cos(off) - travel.y * Mathf.Sin(off),
                             travel.x * Mathf.Sin(off) + travel.y * Mathf.Cos(off));

        float cos0 = Mathf.Clamp(Vector2.Dot(travel, up), -1f, 1f);
        float theta0 = Mathf.Acos(Mathf.Abs(cos0));

        // Snell: sin(theta_b) = sin(theta_0) * c_b / c_0.
        float tp = Mathf.Max(SeaRuntimeState.PeakPeriod, 1f);
        float c0 = SeaConstants.G * tp / SeaConstants.TwoPi;
        float hBreak = Mathf.Max(SeaRuntimeState.SignificantWaveHeight / 0.78f, 0.2f);
        float cb = Mathf.Sqrt(SeaConstants.G * hBreak);

        float sinB = Mathf.Clamp(Mathf.Sin(theta0) * cb / Mathf.Max(c0, 0.01f), -1f, 1f);

        // The along-shore wavenumber at the breaker line.
        float omega = SeaConstants.TwoPi / tp;
        float kb = omega / Mathf.Max(cb, 0.01f);

        Vector2 along = new Vector2(-up.y, up.x);
        if (Vector2.Dot(travel, along) < 0f) along = -along;

        Vector2 peel = along * (kb * sinB);

        // `zw` carries the anchor: the shader measures the along-shore phase from it.
        Shader.SetGlobalVector(SeaShaderIDs.ShorePeel,
                               new Vector4(peel.x, peel.y, shoreAnchor.x, shoreAnchor.y));
    }

    /// WHERE THE WATER MEETS THE LAND, FOUND ONCE.
    ///
    /// The terrain is static, so the shore's orientation is a constant of this world and
    /// has no business being resampled every frame. Marched outward along +X at the
    /// terrain's mid-Z until the ground drops under the sea; that crossing is the anchor,
    /// and the bottom's gradient there is the shore normal.
    void EnsureShoreAnchor()
    {
        if (shoreAnchorValid) return;
        shoreAnchorValid = true;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float z = origin.z + size.z * 0.5f;

        // FROM THE MIDDLE OUTWARD, NOT FROM THE EDGE.
        //
        // The map's western edge is already open water and its floor is flat, so a march
        // that starts there stops on the first sample and reads a zero gradient -- and the
        // early-out below then zeroed the whole peel. Measured: the anchor came back
        // (0, 0). The mountain sits in the middle, so marching seaward from the centre
        // finds the coast the player actually stands on.
        float centre = origin.x + size.x * 0.5f;
        float found = centre;
        const float March = 25f;
        for (float x = centre; x < origin.x + size.x; x += March)
        {
            if (terrain.SampleHeight(new Vector3(x, 0f, z)) + origin.y < settings.seaLevelY)
            {
                found = x;
                break;
            }
        }

        shoreAnchor = new Vector2(found, z);

        const float Step = 40f;
        Vector3 at = new Vector3(found, 0f, z);
        float hx0 = terrain.SampleHeight(at + new Vector3(-Step, 0f, 0f));
        float hx1 = terrain.SampleHeight(at + new Vector3(+Step, 0f, 0f));
        float hz0 = terrain.SampleHeight(at + new Vector3(0f, 0f, -Step));
        float hz1 = terrain.SampleHeight(at + new Vector3(0f, 0f, +Step));

        Vector2 up = new Vector2(hx1 - hx0, hz1 - hz0);
        shoreNormal = up.sqrMagnitude < 1e-8f ? Vector2.zero : up.normalized;
    }

    /// EVERY INPUT OF THE INTEGRATION, NOT JUST THE WIND. Cached on the wind
    /// alone, editing the swell in the Inspector would leave Hs and Tp on the
    /// old spectrum with no error anywhere.
    readonly struct MomentInputs
    {
        readonly float wind, fetch, depth, swellPeriod, swellAlpha, swellGamma, swellEnergy;

        public MomentInputs(float wind, SeaSettings s, float swell, float swellEnergy)
        {
            this.wind = wind;
            this.swellEnergy = swellEnergy;
            fetch = s.fetch;
            depth = s.spectrumDepth;
            swellPeriod = swell;
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
                && Mathf.Abs(swellEnergy - other.swellEnergy) < 0.02f
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

        var inputs = new MomentInputs(u, settings, env.SwellPeriod, env.SwellEnergyScale);

        if (!momentsValid || !momentInputs.Matches(inputs))
        {
            SeaSpectrumMoments.Result m = SeaSpectrumMoments.Integrate(u, settings, env.SwellPeriod,
                                                                        env.SwellEnergyScale,
                                                                        settings.TierBandLimits);
            SeaRuntimeState.SignificantWaveHeight = m.SignificantHeight;
            SeaRuntimeState.PeakPeriod = m.PeakPeriod;
            waveGroups = new Vector4(SeaConstants.TwoPi / Mathf.Max(m.BeatPeriod, 0.1f),
                                     m.BeatDepth, 0f, 0f);
            tierSlopeVariance = m.TierSlopeVariance;
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

        // WHAT THE PIXEL CANNOT SEE STILL HAS TO BE PAID FOR.
        //
        // A tier whose waves fall below one pixel is dropped from the normal, and the
        // slope variance it carried leaves with it: the far water flattens into a
        // mirror and then flickers as the sample point crosses crests it no longer
        // resolves. The surface shader adds this variance back as reflection lobe
        // width instead [SOURCE: Bruneton, Neyret & Holzschuch 2010].
        Shader.SetGlobalVector(SeaShaderIDs.TierSlopeVariance, tierSlopeVariance);

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
        Shader.SetGlobalFloat(SeaShaderIDs.ShoreSlope, b);

        PublishPeel(b);
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
