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
        Shader.SetGlobalFloat(SeaShaderIDs.RunupMaxDepth, settings.runupMaxDepth);

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

    /// THE PEAK PERIOD FOLLOWS FROM THE SPECTRUM.
    ///
    /// JONSWAP peak frequency `wp = 22 (g² / (U10 F))^(1/3)` and `Tp = 2pi/wp`.
    /// [SOURCE: Horvath 2015 / JONSWAP]
    ///
    /// Significant wave height Hs, from fetch-limited growth:
    /// `Hs ~ 0.0016 (g F / U10²)^(1/2) U10² / g`.
    /// [SOURCE: JONSWAP fetch-limited growth relation]
    void UpdateState()
    {
        float u = Mathf.Max(env.WindSpeed, 0.1f);
        float g = SeaConstants.G;
        float f = settings.fetch;

        float omegaP = 22f * Mathf.Pow(g * g / (u * f), 1f / 3f);
        SeaRuntimeState.PeakPeriod = SeaConstants.TwoPi / Mathf.Max(omegaP, 1e-4f);

        float dimensionlessFetch = g * f / (u * u);
        SeaRuntimeState.SignificantWaveHeight =
            0.0016f * Mathf.Sqrt(dimensionlessFetch) * u * u / g;

        Shader.SetGlobalFloat(SeaShaderIDs.PeakPeriod, SeaRuntimeState.PeakPeriod);

        // Run-up phase: a breaking wave advances up the shore and withdraws
        // (spec §8.5). Its period follows the spectrum's peak period.
        float t = Application.isPlaying ? Time.time : 0f;
        float phase = t * (SeaConstants.TwoPi / Mathf.Max(SeaRuntimeState.PeakPeriod, 0.1f));
        float runup = Mathf.Sin(phase) * 0.5f + 0.5f;

        SeaRuntimeState.ShoreFoamIntensity01 = runup;
        Shader.SetGlobalFloat(SeaShaderIDs.ShoreFoamPhase, runup);
    }
}
