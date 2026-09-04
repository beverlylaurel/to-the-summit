using UnityEngine;
using UnityEngine.Rendering;

/// Central controller for the real-time snow subsystem: manages state textures, region tracking,
/// global uniform publishing, and simulation pass dispatches.
[DisallowMultipleComponent]
public class SnowManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] SnowSettings settings;

    [Tooltip("Bridge connecting weather, wind, and time-of-day systems to snow.")]
    [SerializeField] MonoBehaviour environmentSource;

    [Tooltip("Target object centered in active snow region (player).")]
    [SerializeField] Transform followTarget;

    [SerializeField] SnowGroundHeight groundHeight;

    [Tooltip("Snowfall renderer.")]
    [SerializeField] SnowfallRenderer snowfallRenderer;

    [Tooltip("Snow burst and spray particle pool.")]
    [SerializeField] SnowBurstParticles burstParticles;

    [Tooltip("Snow detail normal texture (spec §14.2).")]
    [SerializeField] Texture2D detailNormal;

    [Tooltip("Snow trail persistence storage across region boundaries.")]
    [SerializeField] SnowPersistence persistence;

    [Tooltip("Snow simulation compute shader.")]
    [SerializeField] ComputeShader simCompute;

    [Tooltip("Hidden/Snow/SkyDepth shader.")]
    [SerializeField] Shader skyShader;

    const string OccluderLayerName = "SnowOccluder";
    const int ReducedResolution = 64;
    const int ReadbackInterval = 30;
    const int WindShadowIterations = 24;
    const float WindShadowAngleThreshold = 15f;
    const int WindTransportTiles = 5;

    public static SnowManager Active { get; private set; }

    ISnowEnvironmentSource env;

    RenderTexture trail, trailTemp;
    RenderTexture snow, snowTemp;
    RenderTexture skyVis;
    RenderTexture windShadow;
    RenderTexture rimBlur;
    RenderTexture skyDepth;
    RenderTexture reduced;

    Material skyMaterial;

    const int TrailSegmentStride = 16;
    const int MaxTrailSegments = 32;

    ComputeBuffer trailSegmentBuffer;
    readonly Vector4[] trailSegmentData = new Vector4[MaxTrailSegments * 2];
    int trailSegmentCount;

    readonly SnowSkyCamera skyCamera = new();
    readonly SnowfallController snowfall = new();

    int clearKernel = -1;
    int scrollKernel = -1;
    int deformKernel = -1;
    int reposeKernel = -1;
    int rimBlurHKernel = -1;
    int rimBlurVKernel = -1;
    int rimKernel = -1;
    int accumulateKernel = -1;
    int reduceKernel = -1;
    int windShadowKernel = -1;
    int windTransportKernel = -1;

    int windShadowIterationsLeft;
    Vector2 windShadowDirection;
    Vector2 sastrugiWindDir = Vector2.right;

    int accumulateTile;
    int lastReadbackFrame = -1;
    bool readbackPending;

    float[] windShadowCpu;
    int windShadowCpuRes;
    bool windShadowCpuRequested;
    Vector2 windShadowCpuCenter;

    public float WorldSnowDepth { get; private set; }
    bool coverageMeasured;

    float lastSimulatedTime = -1f;
    float simDeltaTime;


    public float MeanSwe { get; private set; } = -1f;
    public float MeanRhoN { get; private set; } = -1f;

    public bool IsDormant => !pendingClear
                             && !pendingFill
                             && !SnowRuntimeState.IsSnowing
                             && SnowRuntimeState.GroundCoverage01 < 0.01f;

    Vector2Int centerTexel;
    bool pendingClear;
    bool pendingScroll;
    Vector2Int pendingScrollTexels;

    public bool IsReady { get; private set; }

    public SnowSettings Settings => settings;
    public ISnowEnvironmentSource Environment => env;

    /// HOW MUCH OF THE FALLING PRECIPITATION IS SNOW. It comes from the environment, not
    /// from here: the snow system reads the weather, it does not decide it.
    ///
    /// It used to be a settable property defaulting to 1, and the only writer was the F1
    /// slider -- so in normal play snow fell at every altitude and every temperature. The
    /// sea meanwhile read its own answer from the same thermometer and got rain. Measured
    /// 2026-09-03, `SYMPTOMS.md`.
    public float SnowFraction01 =>
        snowFractionOverrideActive ? snowFractionOverride
                                   : (env != null ? Mathf.Clamp01(env.SnowFraction01) : 1f);

    bool snowFractionOverrideActive;
    float snowFractionOverride;

    /// DIAGNOSTIC OVERRIDE -- the same pattern as `WindField.ApplyOverride`.
    public void ApplySnowFractionOverride(float snowFraction01)
    {
        snowFractionOverrideActive = true;
        snowFractionOverride = Mathf.Clamp01(snowFraction01);
    }

    public void ClearSnowFractionOverride() => snowFractionOverrideActive = false;
    public SnowGroundHeight GroundHeight => groundHeight;

    public bool CaptureActive => trailSegmentCount > 0;
    public RenderTexture TrailTexture => trail;
    public RenderTexture SnowTexture => snow;
    public RenderTexture SkyVisTexture => skyVis;
    public RenderTexture WindShadowTexture => windShadow;

    public Vector2 AreaCenter => new(centerTexel.x * TexelSize, centerTexel.y * TexelSize);
    public float TexelSize => settings.QualityData.AreaSize / settings.QualityData.Resolution;
    public Vector2Int LastScrollTexels { get; private set; }

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: {nameof(settings)} is not assigned.");
        if (simCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: SnowSim.compute is not assigned.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: Follow target is not assigned.");
        if (groundHeight == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: {nameof(groundHeight)} is not assigned.");
        if (skyShader == null)
            throw new System.InvalidOperationException($"{nameof(SnowManager)}: {nameof(skyShader)} is not assigned.");

        env = environmentSource as ISnowEnvironmentSource;

        if (env == null)
        {
            Debug.LogError($"{nameof(SnowManager)}: {nameof(ISnowEnvironmentSource)} not found. Snow system disabled.");
            enabled = false;
            return;
        }

        SnowQualityData q = settings.QualityData;

        trail = Create("RT_Trail", q.Resolution, RenderTextureFormat.ARGBHalf);
        trailTemp = Create("RT_TrailTemp", q.Resolution, RenderTextureFormat.ARGBHalf);
        snow = Create("RT_Snow", q.Resolution, RenderTextureFormat.ARGBFloat);
        snowTemp = Create("RT_SnowTemp", q.Resolution, RenderTextureFormat.ARGBFloat);
        skyVis = Create("RT_SkyVis", q.SkyResolution, RenderTextureFormat.RFloat);
        windShadow = Create("RT_WindShadow", q.SkyResolution, RenderTextureFormat.RGFloat);
        rimBlur = Create("RT_RimBlur", q.Resolution, RenderTextureFormat.RHalf);
        skyDepth = CreateDepth("RT_SkyDepth", q.SkyResolution);
        reduced = Create("RT_SnowReduced", ReducedResolution, RenderTextureFormat.ARGBFloat);

        skyMaterial = new Material(skyShader) { hideFlags = HideFlags.HideAndDontSave };

        clearKernel = simCompute.FindKernel("KClear");
        scrollKernel = simCompute.FindKernel("KScroll");
        deformKernel = simCompute.FindKernel("KDeform");
        reposeKernel = simCompute.FindKernel("KRepose");
        rimBlurHKernel = simCompute.FindKernel("KRimBlurH");
        rimBlurVKernel = simCompute.FindKernel("KRimBlurV");
        rimKernel = simCompute.FindKernel("KRim");
        accumulateKernel = simCompute.FindKernel("KAccumulate");
        reduceKernel = simCompute.FindKernel("KReduceState");
        windShadowKernel = simCompute.FindKernel("KWindShadow");
        windTransportKernel = simCompute.FindKernel("KWindTransport");

        windShadowIterationsLeft = WindShadowIterations;
        windShadowDirection = Vector2.zero;
        sastrugiWindDir = Vector2.right;

        accumulateTile = 0;
        lastReadbackFrame = -1;
        readbackPending = false;

        windShadowCpu = null;
        windShadowCpuRes = 0;
        windShadowCpuRequested = false;
        coverageMeasured = false;

        snowfall.Reset();
        skyCamera.Rescan(LayerMask.NameToLayer(OccluderLayerName));

        lastSimulatedTime = -1f;

        centerTexel = SnapToTexelGrid(followTarget.position, TexelSize, settings.QualityData.SnapStep);
        pendingClear = true;
        pendingScroll = false;

        SnowRuntimeState.Reset();
        ApplyQualityKeyword(q);

        Active = this;
        IsReady = true;

        WriteGlobals();
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
        IsReady = false;

        SnowRuntimeState.Reset();

        trailSegmentCount = 0;

        if (trailSegmentBuffer != null)
        {
            trailSegmentBuffer.Release();
            trailSegmentBuffer = null;
        }

        if (skyMaterial != null)
        {
            DestroyImmediate(skyMaterial);
            skyMaterial = null;
        }

        snowfall.Reset();

        Release(ref trail);
        Release(ref trailTemp);
        Release(ref snow);
        Release(ref snowTemp);
        Release(ref skyVis);
        Release(ref windShadow);
        Release(ref rimBlur);
        Release(ref skyDepth);
        Release(ref reduced);
    }

    void LateUpdate()
    {
        if (!IsReady) return;

        UpdateRegion();
        snowfall.Tick(env, SnowFraction01);
        TickWorldSnow(Time.deltaTime * Mathf.Max(0f, SimTimeScale));
        WriteGlobals();
    }

    public static Vector2Int SnapToTexelGrid(Vector3 worldPos, float texelSize, float snapStep)
    {
        float snapped = snapStep;
        float x = Mathf.Floor(worldPos.x / snapped) * snapped;
        float z = Mathf.Floor(worldPos.z / snapped) * snapped;

        return new Vector2Int(Mathf.RoundToInt(x / texelSize), Mathf.RoundToInt(z / texelSize));
    }

    void UpdateRegion()
    {
        Vector2Int next = SnapToTexelGrid(followTarget.position, TexelSize, settings.QualityData.SnapStep);
        if (next == centerTexel) return;

        Vector2Int delta = next - centerTexel;

        pendingScrollTexels = pendingScroll ? pendingScrollTexels + delta : delta;
        pendingScroll = true;

        centerTexel = next;
    }

    void WriteGlobals()
    {
        SnowQualityData q = settings.QualityData;
        Vector2 center = AreaCenter;

        Shader.SetGlobalVector(SnowShaderIDs.SnowAreaCenter, new Vector4(center.x, center.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.SnowAreaSize, q.AreaSize);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowResolution, q.Resolution);

        Shader.SetGlobalTexture(SnowShaderIDs.SnowStateTex, snow);
        Shader.SetGlobalTexture(SnowShaderIDs.SnowTrailTex, trail);
        Shader.SetGlobalTexture(SnowShaderIDs.SnowSkyVisTex, skyVis);
        Shader.SetGlobalTexture(SnowShaderIDs.SnowWindShadowTex, windShadow);

        Vector3 wind = env.WindDirection * env.WindSpeed;

        Shader.SetGlobalVector(SnowShaderIDs.WindWS, new Vector4(wind.x, wind.y, wind.z, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, env.WindSpeed);
        Shader.SetGlobalFloat(SnowShaderIDs.TemperatureC, env.TemperatureC);
        Shader.SetGlobalFloat(SnowShaderIDs.SunElevation01, env.SunElevation01);
        Shader.SetGlobalFloat(SnowShaderIDs.FogDensity01, env.FogDensity01);

        float rainOnSnow = SnowRuntimeState.IsSnowing
                         ? 0f
                         : SnowRuntimeState.RainWeight01 * env.PrecipIntensity01;

        Shader.SetGlobalFloat(SnowShaderIDs.RainOnSnow01, rainOnSnow);

        if (detailNormal != null)
            Shader.SetGlobalTexture(SnowShaderIDs.SnowDetailNormal, detailNormal);

        Shader.SetGlobalColor(SnowShaderIDs.ShadowTint, settings.ShadowTint);
        Shader.SetGlobalFloat(SnowShaderIDs.TranslucencyStrength, settings.TranslucencyStrength);
        Shader.SetGlobalFloat(SnowShaderIDs.SparkleCellSize, settings.SparkleCellSize);
        Shader.SetGlobalFloat(SnowShaderIDs.SparkleDensity, settings.SparkleDensity);
        Shader.SetGlobalFloat(SnowShaderIDs.SparkleSharpness, settings.SparkleSharpness);
        Shader.SetGlobalFloat(SnowShaderIDs.SparkleIntensity, settings.SparkleIntensity);

        PublishTexture("_SnowSurfTazeColor", settings.SurfTazeColor);
        PublishTexture("_SnowSurfTazeNormal", settings.SurfTazeNormal);
        PublishTexture("_SnowSurfTazeRough", settings.SurfTazeRough);
        PublishTexture("_SnowSurfTozColor", settings.SurfTozColor);
        PublishTexture("_SnowSurfTozNormal", settings.SurfTozNormal);
        PublishTexture("_SnowSurfTozRough", settings.SurfTozRough);
        PublishTexture("_SnowSurfYerlesmisColor", settings.SurfYerlesmisColor);
        PublishTexture("_SnowSurfYerlesmisNormal", settings.SurfYerlesmisNormal);
        PublishTexture("_SnowSurfYerlesmisRough", settings.SurfYerlesmisRough);
        PublishTexture("_SnowSurfWindColor", settings.SurfWindColor);
        PublishTexture("_SnowSurfWindNormal", settings.SurfWindNormal);
        PublishTexture("_SnowSurfWindRough", settings.SurfWindRough);

        Shader.SetGlobalFloat("_SnowSurfTileMeters", Mathf.Max(0.01f, settings.SurfTileMeters));
        Shader.SetGlobalFloat("_SnowMultiScatter", 1f);
        Shader.SetGlobalFloat("_SnowSurfStrength", settings.SurfStrength);

        Shader.SetGlobalFloat(SnowShaderIDs.FallbackSWE, Mathf.Max(0f, WorldSwe));

        float dunyaRhoN = MeanRhoN >= 0f ? MeanRhoN
                        : (WorldRhoN >= 0f ? WorldRhoN : settings.DefaultRhoN);
        Shader.SetGlobalFloat(SnowShaderIDs.FallbackRhoN, dunyaRhoN);

        float worldDepth = SnowBaseHeightMeters(Mathf.Max(0f, WorldSwe), dunyaRhoN);
        Shader.SetGlobalFloat(SnowShaderIDs.WorldSnowDepth, worldDepth);
        WorldSnowDepth = worldDepth;

        Vector2 rawWind = new Vector2(env.PrevailingWindDirection.x,
                                      env.PrevailingWindDirection.z);

        if (env.WindSpeed >= SnowConstants.DriftU10Loose && rawWind.sqrMagnitude > 1e-4f)
        {
            rawWind.Normalize();
            float k = 1f - Mathf.Exp(-Time.deltaTime / SnowConstants.SastrugiWindTau);
            sastrugiWindDir = Vector2.Lerp(sastrugiWindDir, rawWind, k).normalized;
        }

        Shader.SetGlobalVector(SnowShaderIDs.SastrugiWindDir,
            new Vector4(sastrugiWindDir.x, sastrugiWindDir.y, 0f, 0f));

        if (Camera.main != null)
            Shader.SetGlobalVector(SnowShaderIDs.TessCameraPos,
                                   Camera.main.transform.position);

        Shader.SetGlobalFloat(SnowShaderIDs.TessMax, settings.TessMax);
        Shader.SetGlobalFloat(SnowShaderIDs.TessNear, settings.TessNear);
        Shader.SetGlobalFloat(SnowShaderIDs.TessFar, settings.TessFar);

        SnowHeatRegistry.Publish(AreaCenter, q.AreaSize);

        SnowRuntimeState.Stormness01 =
            Mathf.Clamp01(env.PrecipIntensity01 * Mathf.Clamp01(env.WindSpeed / 15f));

        if (!coverageMeasured)
        {
            float fallbackHeight = settings.DefaultSwe * SnowConstants.RhoWater /
                                   Mathf.Max(1f, Mathf.Lerp(SnowConstants.RhoMin,
                                                            SnowConstants.RhoMax,
                                                            settings.DefaultRhoN));

            SnowRuntimeState.GroundCoverage01 =
                Mathf.Clamp01(fallbackHeight / SnowConstants.MinVisibleHeight);
        }

        Vector2 skyCenter = skyCamera.Center;

        Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ,
            new Vector4(skyCenter.x, skyCenter.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, SnowConstants.SkyAreaSize);
        Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, q.SkyResolution);
    }

    public void Dispatch(CommandBuffer cmd, Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        if (!IsReady) return;

        if (lastSimulatedTime < 0f) lastSimulatedTime = Time.time;

        float elapsed = Time.time - lastSimulatedTime;
        if (elapsed <= 0f) return;

        lastSimulatedTime = Time.time;
        simDeltaTime = Mathf.Min(elapsed, Time.maximumDeltaTime);

        if (IsDormant) return;

        SnowQualityData q = settings.QualityData;
        int groups = Mathf.CeilToInt(q.Resolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.Resolution, q.Resolution);

        if (pendingClear)
        {
            ClearTo(cmd, snow, groups, WorldSnowValue);
            ClearTo(cmd, trail, groups, Vector4.zero);
            ClearTo(cmd, trailTemp, groups, Vector4.zero);

            pendingClear = false;
        }

        if (pendingFill)
        {
            ClearTo(cmd, snow, groups, new Vector4(fillSwe, fillRhoN, 0f, 0f));
            ClearTo(cmd, trail, groups, Vector4.zero);
            ClearTo(cmd, trailTemp, groups, Vector4.zero);

            pendingFill = false;
        }

        if (pendingScroll)
        {
            LastScrollTexels = pendingScrollTexels;

            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.ScrollTexels,
                new Vector4(pendingScrollTexels.x, pendingScrollTexels.y, 0f, 0f));

            Scroll(cmd, groups, ref trail, ref trailTemp, Vector4.zero);
            Scroll(cmd, groups, ref snow, ref snowTemp, WorldSnowValue);

            pendingScroll = false;
            pendingScrollTexels = Vector2Int.zero;
        }

        cmd.BeginSample(SnowProfiler.MarkerNames[0]);
        DispatchSky(cmd, restoreView, restoreProj);
        cmd.EndSample(SnowProfiler.MarkerNames[0]);

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowfallSWERate,
                                 snowfall.SnowfallSweRate);

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.FallbackRhoN,
                                 WorldRhoN >= 0f ? WorldRhoN : settings.DefaultRhoN);

        cmd.BeginSample(SnowProfiler.MarkerNames[1]);
        BuildTrailSegments(cmd);
        cmd.EndSample(SnowProfiler.MarkerNames[1]);

        cmd.BeginSample(SnowProfiler.MarkerNames[2]);
        DispatchTrail(cmd, groups);
        cmd.EndSample(SnowProfiler.MarkerNames[2]);

        cmd.BeginSample(SnowProfiler.MarkerNames[3]);
        DispatchAccumulate(cmd, groups);

        DispatchWindShadow(cmd);

        float driftActive = SnowDriftVfxController.DriftActiveFor(
            env.WindSpeed, SnowRuntimeState.LooseSnowFraction);

        if (driftActive > 0f)
            DispatchWindTransport(cmd, groups);

        if (persistence != null) persistence.Dispatch(cmd);

        cmd.EndSample(SnowProfiler.MarkerNames[3]);

        cmd.BeginSample(SnowProfiler.MarkerNames[4]);
        if (snowfallRenderer != null) snowfallRenderer.Dispatch(cmd);
        if (burstParticles != null) burstParticles.Dispatch(cmd);
        cmd.EndSample(SnowProfiler.MarkerNames[4]);

        cmd.SetGlobalTexture(SnowShaderIDs.SnowStateTex, snow);
        cmd.SetGlobalTexture(SnowShaderIDs.SnowTrailTex, trail);
    }

    void DispatchTrail(CommandBuffer cmd, int groups)
    {
        if (!CaptureActive) return;

        Texture ground = groundHeight.HeightTexture;
        if (ground == null) return;

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowDeltaTime, simDeltaTime);
        SnowQualityData quality = settings.QualityData;
        float rimBlurTexels = SnowConstants.RimBlurMeters / Mathf.Max(quality.TexelSize, 1e-4f);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.RimBlurTexels, rimBlurTexels);

        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.GroundHeightTex, ground);
        cmd.SetComputeBufferParam(simCompute, deformKernel, SnowShaderIDs.TrailSegments, trailSegmentBuffer);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.Trail, trail);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.TrailOut, trailTemp);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, deformKernel, SnowShaderIDs.SnowOut, snowTemp);
        cmd.DispatchCompute(simCompute, deformKernel, groups, groups, 1);

        (trail, trailTemp) = (trailTemp, trail);
        (snow, snowTemp) = (snowTemp, snow);

        for (int i = 0; i < SnowConstants.ReposeIterations; i++)
        {
            cmd.SetComputeTextureParam(simCompute, reposeKernel, SnowShaderIDs.Trail, trail);
            cmd.SetComputeTextureParam(simCompute, reposeKernel, SnowShaderIDs.Snow, snow);
            cmd.SetComputeTextureParam(simCompute, reposeKernel, SnowShaderIDs.TrailOut, trailTemp);
            cmd.DispatchCompute(simCompute, reposeKernel, groups, groups, 1);

            (trail, trailTemp) = (trailTemp, trail);
        }

        cmd.SetComputeTextureParam(simCompute, rimBlurHKernel, SnowShaderIDs.Src, trail);
        cmd.SetComputeTextureParam(simCompute, rimBlurHKernel, SnowShaderIDs.Dst, trailTemp);
        cmd.DispatchCompute(simCompute, rimBlurHKernel, groups, groups, 1);

        cmd.SetComputeTextureParam(simCompute, rimBlurVKernel, SnowShaderIDs.Src, trailTemp);
        cmd.SetComputeTextureParam(simCompute, rimBlurVKernel, SnowShaderIDs.CarveOut, rimBlur);
        cmd.DispatchCompute(simCompute, rimBlurVKernel, groups, groups, 1);

        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.Trail, trail);
        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.BlurredCarve, rimBlur);
        cmd.SetComputeTextureParam(simCompute, rimKernel, SnowShaderIDs.TrailOut, trailTemp);
        cmd.DispatchCompute(simCompute, rimKernel, groups, groups, 1);

        (trail, trailTemp) = (trailTemp, trail);
    }

    void DispatchSky(CommandBuffer cmd, Matrix4x4 restoreView, Matrix4x4 restoreProj)
    {
        if (!skyCamera.NeedsRefresh(AreaCenter)) return;

        skyCamera.Record(cmd, skyVis, skyDepth, skyMaterial,
                         AreaCenter, followTarget.position.y, restoreView, restoreProj);
    }

    public void RefillRegion() => pendingClear = true;

    public float WorldSwe { get; private set; } = -1f;
    public float WorldRhoN { get; private set; } = -1f;

    const float WorldSettledRhoN = 0.28f;

    void TickWorldSnow(float dt)
    {
        if (WorldSwe < 0f)
        {
            WorldSwe = settings.DefaultSwe;
            WorldRhoN = settings.DefaultRhoN;
        }

        float fall = snowfall.SnowfallSweRate * dt;

        if (fall > 0f)
        {
            float taze = Mathf.InverseLerp(50f, 550f, 55f);
            float total = WorldSwe + fall;

            WorldRhoN = (WorldRhoN * WorldSwe + taze * fall) / Mathf.Max(total, 1e-9f);
            WorldSwe = Mathf.Min(total, SnowConstants.SweMax);
        }

        if (WorldRhoN < WorldSettledRhoN)
            WorldRhoN += (WorldSettledRhoN - WorldRhoN) * (1f - Mathf.Exp(-dt / 21600f));
    }

    Vector4 WorldSnowValue => new Vector4(WorldSwe, WorldRhoN, 0f, 0f);

    public float SimTimeScale { get; set; } = 1f;

    public void FillSnowDepth(float meters)
    {
        const float FreshDensity = 55f;
        const float WaterDensity = 1000f;

        fillSwe = Mathf.Max(0f, meters) * FreshDensity / WaterDensity;
        fillRhoN = Mathf.InverseLerp(50f, 550f, FreshDensity);
        pendingFill = true;

        WorldSwe = fillSwe;
        WorldRhoN = fillRhoN;
    }

    bool pendingFill;
    float fillSwe;
    float fillRhoN;

    public void MarkSkyVisDirty()
    {
        skyCamera.Rescan(LayerMask.NameToLayer(OccluderLayerName));
    }

    void DispatchWindShadow(CommandBuffer cmd)
    {
        Vector2 wind = new Vector2(env.WindDirection.x, env.WindDirection.z);

        if (wind.sqrMagnitude > 1e-4f)
        {
            wind.Normalize();
            float dot = Vector2.Dot(wind, windShadowDirection);

            if (windShadowDirection == Vector2.zero ||
                dot < Mathf.Cos(WindShadowAngleThreshold * Mathf.Deg2Rad))
            {
                windShadowDirection = wind;
                windShadowIterationsLeft = WindShadowIterations;
            }
        }

        if (windShadowIterationsLeft <= 0)
        {
            if (!windShadowCpuRequested && windShadow != null)
            {
                windShadowCpuRequested = true;
                windShadowCpuCenter = skyCamera.Center;
                cmd.RequestAsyncReadback(windShadow, OnWindShadowRead);
            }
            return;
        }

        windShadowCpuRequested = false;
        windShadowIterationsLeft--;

        SnowQualityData q = settings.QualityData;
        int groups = Mathf.CeilToInt(q.SkyResolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeTextureParam(simCompute, windShadowKernel, SnowShaderIDs.SkyVisY, skyVis);
        cmd.SetComputeTextureParam(simCompute, windShadowKernel, SnowShaderIDs.WindShadow, windShadow);

        Texture ground = groundHeight.HeightTexture;
        if (ground == null) return;

        cmd.SetComputeTextureParam(simCompute, windShadowKernel, SnowShaderIDs.GroundHeightTex, ground);

        for (int parity = 0; parity < 2; parity++)
        {
            cmd.SetComputeIntParam(simCompute, SnowShaderIDs.GSParity, parity);
            cmd.DispatchCompute(simCompute, windShadowKernel, groups, groups, 1);
        }
    }

    void OnWindShadowRead(AsyncGPUReadbackRequest request)
    {
        if (request.hasError) { windShadowCpuRequested = false; return; }

        Unity.Collections.NativeArray<Vector2> data = request.GetData<Vector2>();

        int res = Mathf.RoundToInt(Mathf.Sqrt(data.Length));
        if (res * res != data.Length) { windShadowCpuRequested = false; return; }

        if (windShadowCpu == null || windShadowCpu.Length != data.Length)
            windShadowCpu = new float[data.Length];

        for (int i = 0; i < data.Length; i++)
            windShadowCpu[i] = data[i].x;

        windShadowCpuRes = res;
    }

    public float WindShadowAt(Vector3 posWS)
    {
        if (windShadowCpu == null || windShadowCpuRes <= 0) return 0f;

        float size = SnowConstants.SkyAreaSize;

        float u = (posWS.x - windShadowCpuCenter.x) / size + 0.5f;
        float v = (posWS.z - windShadowCpuCenter.y) / size + 0.5f;

        if (u < 0f || u > 1f || v < 0f || v > 1f) return 0f;

        float fx = u * (windShadowCpuRes - 1);
        float fy = v * (windShadowCpuRes - 1);

        int x0 = Mathf.Clamp((int)fx, 0, windShadowCpuRes - 1);
        int y0 = Mathf.Clamp((int)fy, 0, windShadowCpuRes - 1);
        int x1 = Mathf.Min(x0 + 1, windShadowCpuRes - 1);
        int y1 = Mathf.Min(y0 + 1, windShadowCpuRes - 1);

        float tx = fx - x0;
        float ty = fy - y0;

        float a = windShadowCpu[y0 * windShadowCpuRes + x0];
        float b = windShadowCpu[y0 * windShadowCpuRes + x1];
        float c = windShadowCpu[y1 * windShadowCpuRes + x0];
        float d = windShadowCpu[y1 * windShadowCpuRes + x1];

        float wz = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);

        return Mathf.Max(0f, wz - posWS.y);
    }

    public Vector2 SastrugiWindDir => sastrugiWindDir;

    void DispatchWindTransport(CommandBuffer cmd, int groups)
    {
        Texture ground = groundHeight.HeightTexture;
        if (ground == null) return;

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowDeltaTime, simDeltaTime);

        cmd.SetComputeTextureParam(simCompute, windTransportKernel, SnowShaderIDs.GroundHeightTex, ground);
        cmd.SetComputeTextureParam(simCompute, windTransportKernel,
                                   SnowShaderIDs.SnowWindShadowTex, windShadow);
        cmd.SetComputeTextureParam(simCompute, windTransportKernel, SnowShaderIDs.SnowRW, snow);
        cmd.SetComputeTextureParam(simCompute, windTransportKernel, SnowShaderIDs.TrailRW, trail);

        for (int tile = 1; tile <= WindTransportTiles; tile++)
        {
            cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TileIndex, tile);
            cmd.DispatchCompute(simCompute, windTransportKernel, groups, groups, 1);
        }
    }

    void DispatchAccumulate(CommandBuffer cmd, int groups)
    {
        SnowQualityData q = settings.QualityData;

        Texture ground = groundHeight.HeightTexture;
        if (ground == null) return;

        int tiles = Mathf.Max(1, q.AccumulateTiles);
        accumulateTile = (accumulateTile + 1) % tiles;

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DeltaTimeEff,
                                 simDeltaTime * tiles * Mathf.Max(0f, SimTimeScale));
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TileIndex, accumulateTile);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TileCount, tiles);

        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.GroundHeightTex, ground);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.SnowSkyVisTex, skyVis);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.SnowOut, snowTemp);

        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.Trail, trail);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.TrailOut, trailTemp);

        int tileGroups = Mathf.Max(1, groups / tiles);
        cmd.DispatchCompute(simCompute, accumulateKernel, tileGroups, groups, 1);

        int tileWidth = q.Resolution / tiles;
        int tileX = accumulateTile * tileWidth;

        cmd.CopyTexture(snowTemp, 0, 0, tileX, 0, tileWidth, q.Resolution,
                        snow, 0, 0, tileX, 0);

        cmd.CopyTexture(trailTemp, 0, 0, tileX, 0, tileWidth, q.Resolution,
                        trail, 0, 0, tileX, 0);

        DispatchReduce(cmd);
    }

    void DispatchReduce(CommandBuffer cmd)
    {
        if (readbackPending) return;
        if (Time.frameCount - lastReadbackFrame < ReadbackInterval) return;

        lastReadbackFrame = Time.frameCount;

        int reduceGroups = Mathf.CeilToInt(ReducedResolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeTextureParam(simCompute, reduceKernel, SnowShaderIDs.Snow, snow);
        cmd.SetComputeTextureParam(simCompute, reduceKernel, SnowShaderIDs.ReducedOut, reduced);
        cmd.DispatchCompute(simCompute, reduceKernel, reduceGroups, reduceGroups, 1);

        readbackPending = true;
        cmd.RequestAsyncReadback(reduced, OnReduced);
    }

    void OnReduced(AsyncGPUReadbackRequest request)
    {
        readbackPending = false;

        if (!IsReady || request.hasError) return;

        Unity.Collections.NativeArray<Color> data = request.GetData<Color>();

        float coverage = 0f;
        float loose = 0f;
        float swe = 0f;
        float rhoN = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            swe += data[i].r;
            rhoN += data[i].g;
            coverage += data[i].b;
            loose += 1f - data[i].g;
        }

        float inv = 1f / Mathf.Max(1, data.Length);

        SnowRuntimeState.GroundCoverage01 = Mathf.Clamp01(coverage * inv);
        SnowRuntimeState.LooseSnowFraction = Mathf.Clamp01(loose * inv);

        MeanSwe = swe * inv;
        MeanRhoN = rhoN * inv;

        coverageMeasured = true;
    }

    void BuildTrailSegments(CommandBuffer cmd)
    {
        trailSegmentCount = 0;

        Vector2 center = AreaCenter;
        float half = settings.QualityData.AreaSize * 0.5f;

        float radiusSum = 0f;
        int radiusCount = 0;

        for (int i = 0; i < SnowDeformerRegistry.Count; i++)
        {
            SnowDeformer d = SnowDeformerRegistry.Get(i);
            if (d == null) continue;

            int n = d.SegmentCount;

            for (int k = 0; k < n && trailSegmentCount < MaxTrailSegments; k++)
            {
                d.GetSegment(k, out Vector4 a, out Vector4 b);

                float r = a.w;

                float minX = Mathf.Min(a.x, b.x) - r, maxX = Mathf.Max(a.x, b.x) + r;
                float minZ = Mathf.Min(a.z, b.z) - r, maxZ = Mathf.Max(a.z, b.z) + r;

                if (maxX < center.x - half || minX > center.x + half) continue;
                if (maxZ < center.y - half || minZ > center.y + half) continue;

                int slot = trailSegmentCount * 2;
                trailSegmentData[slot]     = a;
                trailSegmentData[slot + 1] = b;

                radiusSum += r;
                radiusCount++;

                trailSegmentCount++;
            }

            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.TrailVelocityXZ,
                                      new Vector4(d.VelocityXZ.x, d.VelocityXZ.y, 0f, 0f));
        }

        if (trailSegmentCount == 0) return;

        Shader.SetGlobalFloat(SnowShaderIDs.CavityRadius,
                              radiusSum / Mathf.Max(radiusCount, 1));

        trailSegmentBuffer ??= new ComputeBuffer(MaxTrailSegments * 2, TrailSegmentStride);
        trailSegmentBuffer.SetData(trailSegmentData, 0, 0, trailSegmentCount * 2);

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TrailSegmentCount, trailSegmentCount);
    }

    void ClearTo(CommandBuffer cmd, RenderTexture target, int groups, Vector4 value)
    {
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.ClearValue, value);
        cmd.SetComputeTextureParam(simCompute, clearKernel, SnowShaderIDs.Dst, target);
        cmd.DispatchCompute(simCompute, clearKernel, groups, groups, 1);
    }

    void Scroll(CommandBuffer cmd, int groups, ref RenderTexture src, ref RenderTexture dst,
                Vector4 newEdge)
    {
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.NewEdgeValue, newEdge);
        cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.Src, src);
        cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.Dst, dst);
        cmd.DispatchCompute(simCompute, scrollKernel, groups, groups, 1);

        (src, dst) = (dst, src);
    }

    static void ApplyQualityKeyword(SnowQualityData quality)
    {
        Shader.DisableKeyword(SnowQuality.KeywordLow);
        Shader.DisableKeyword(SnowQuality.KeywordMedium);
        Shader.DisableKeyword(SnowQuality.KeywordHigh);

        Shader.EnableKeyword(quality.Keyword);
    }

    static void PublishTexture(string name, Texture2D tex)
    {
        if (tex != null) Shader.SetGlobalTexture(name, tex);
    }

    static float SnowBaseHeightMeters(float swe, float rhoN)
    {
        float rho = Mathf.Max(SnowConstants.RhoMin + rhoN * (SnowConstants.RhoMax - SnowConstants.RhoMin), 1f);
        return swe * SnowConstants.RhoWater / rho;
    }

    static RenderTexture Create(string name, int resolution, RenderTextureFormat format)
    {
        var rt = new RenderTexture(resolution, resolution, 0, format)
        {
            name = name,
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

    static RenderTexture CreateDepth(string name, int resolution)
    {
        var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.Depth)
        {
            name = name,
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
        DestroyImmediate(rt);
        rt = null;
    }
}
