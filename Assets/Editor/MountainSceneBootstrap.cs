using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Single source of truth for the scene. Runs after every domain reload, bringing the scene
/// to these configured settings. Does nothing if settings have not changed.
[InitializeOnLoad]
public static class MountainSceneBootstrap
{
    // --- Player ---
    const float PlayerHeight = 1.8f;
    const float EyeHeight = 1.65f;
    const float FarClipFactor = 3f;   // Camera far clip = map dimension * factor

    /// Maximum error allowed for terrain LOD silhouette (pixels).
    const float TerrainPixelError = 2f;

    /// COARSEST LOD LEVEL. Limits coarseness to maintain silhouette accuracy.
    const int TerrainMaxLod = 1;

    /// Basemap effectively disabled: forces terrain rendering with full material rather than baked 1024 basemap.
    const float TerrainBasemapDistance = 25000f;

    /// Main game scene path where bootstrapping runs.
    const string MainScenePath = "Assets/Scenes/Game.unity";

    const string TerrainDataPath = "Assets/Terrain/MountainTerrainData.asset";
    const string SettingsPath = "Assets/Settings/MountainSettings.asset";
    const string LookSettingsPath = "Assets/Settings/LookSettings.asset";
    const string TerrainMaterialPath = "Assets/Settings/TerrainMaterialSettings.asset";
    const string AtmospherePath = "Assets/Settings/AtmosphereSettings.asset";
    const string ThunderPath = "Assets/Settings/ThunderSettings.asset";
    const string LightningPath = "Assets/Settings/LightningSettings.asset";
    const string WindPath = "Assets/Settings/WindSettings.asset";
    const string RoutePath = "Assets/Settings/MountainRoute.asset";
    const string WeatherDriverPath = "Assets/Settings/WeatherDriverSettings.asset";
    const string BoltShaderPath = "Assets/Shaders/LightningBolt.shader";
    const string BoltMaterialPath = "Assets/Settings/LightningBolt.mat";
    const string SurfaceShaderPath = "Assets/Shaders/MountainSurface.shader";
    const string SandTexturePath = "Assets/Textures/Sand";
    const string PrecipitationShaderPath = "Assets/Shaders/Precipitation.shader";
    const string RainStreakDatabasePath = "Assets/Rain/RainStreakDatabase.asset";
    const string SeaSettingsPath = "Assets/Sea/Settings/SeaSettings.asset";
    const string SeaSurfaceShaderPath = "Assets/Sea/Shaders/SeaLit.shader";
    const string SeaSpectrumPath = "Assets/Sea/Shaders/SeaSpectrum.compute";
    const string SeaFftPath = "Assets/Sea/Shaders/SeaFFT.compute";
    const string SeaFoamPath = "Assets/Sea/Shaders/SeaFoam.compute";
    const string SkyShaderPath = "Assets/Shaders/Sky.shader";
    const string SkyMaterialPath = "Assets/Settings/Sky.mat";
    const string FogComputePath = "Assets/Shaders/VolumetricFog.compute";
    const string FogSettingsPath = "Assets/Settings/VolumetricFogSettings.asset";
    const string SkyFogShaderPath = "Assets/Shaders/SkyFog.shader";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string CloudMaterialPath = "Assets/Settings/VolumetricClouds.mat";
    const string CloudWeatherPath = "Assets/Settings/CloudWeatherSettings.asset";
    const string SkyWeatherPath = "Assets/Settings/SkyWeatherSettings.asset";
    const string MoonLightName = "Moon Light";

    /// Volume where `EnsureCloudVolume` writes volumetric cloud component.
    static UnityEngine.Rendering.Volume cloudVolume;

    static MountainSceneBootstrap()
    {
        EditorApplication.delayCall += Run;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += Run;
    }

    public static void Rebuild() => RegenerateTerrain();

    /// Re-applies component wiring without regenerating terrain data. Editor diagnostics and
    /// migrations use this when a new runtime dependency is introduced.
    public static void RefreshSceneBindings() => Run();

    [MenuItem("To The Summit/Terrain/Regenerate Terrain", false, 20)]
    static void RegenerateTerrain()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null)
            throw new System.InvalidOperationException(
                "No MountainGenerator in scene; bootstrap must run first.");

        gen.lastBuildSignature = string.Empty;
        SurfaceMapBaker.Invalidate();

        EditorUtility.SetDirty(gen);
        Run();
    }

    static MountainSettings current;

    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;

        if (scene.path != MainScenePath) return;

        bool changed = false;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var timings = new System.Text.StringBuilder();
        long mark = 0;

        void Phase(string name)
        {
            long now = clock.ElapsedMilliseconds;
            if (now - mark >= 1) timings.Append($"\n  {name,-24} {now - mark} ms");
            mark = now;
        }

        if (RemoveMissingScripts(scene)) changed = true;
        Phase("missing scripts scan");

        EnsureSkyFeature();
        EnsureCloudFeature();
        EnsurePrecipitationFeature();
        EnsureFogFeature();
        Phase("sky, cloud, fog features");

        EnsureCloudVolume();
        EnsureSkyVolume();
        Phase("sky and cloud volumes");

        var settings = current = LoadOrCreateSettings();

        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null)
        {
            gen = CreateMountain(settings);
            changed = true;
        }

        gen.Bind(settings);

        Phase("mountain component");

        string signature = settings.BuildSignature();
        bool regenerated = gen.lastBuildSignature != signature;

        if (regenerated)
        {
            gen.lastBuildSignature = signature;
            EditorUtility.SetDirty(gen);
            EditorUtility.SetDirty(gen.GetComponent<Terrain>().terrainData);
            AssetDatabase.SaveAssets();
            changed = true;
        }

        gen.Measure();
        WriteMountainReport(gen);

        var player = Object.FindAnyObjectByType<FirstPersonController>();
        if (player == null)
        {
            player = CreatePlayer();
            changed = true;
        }

        SpawnPose(out Vector3 spawn, out Quaternion facing);
        if (player.transform.position != spawn || player.transform.rotation != facing)
        {
            player.transform.SetPositionAndRotation(spawn, facing);
            changed = true;
        }

        if (player.GetComponent<CursorLock>() == null)
        {
            player.gameObject.AddComponent<CursorLock>();
            changed = true;
        }

        if (RemoveStaleTestObjects()) changed = true;

        var snap = player.GetComponent<GroundSnap>();
        if (snap == null)
        {
            snap = player.gameObject.AddComponent<GroundSnap>();
            changed = true;
        }

        snap.Bind(gen.GetComponent<Terrain>());
        EditorUtility.SetDirty(snap);

        var snowOffset = player.GetComponent<SnowGroundOffset>();
        if (snowOffset == null)
        {
            snowOffset = player.gameObject.AddComponent<SnowGroundOffset>();
            changed = true;
        }

        var snowManager = Object.FindAnyObjectByType<SnowManager>(FindObjectsInactive.Include);
        var snowOffsetSo = new SerializedObject(snowOffset);
        var snowManagerProp = snowOffsetSo.FindProperty("snowManager");

        if (snowManagerProp.objectReferenceValue != snowManager)
        {
            snowManagerProp.objectReferenceValue = snowManager;
            snowOffsetSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(snowOffset);
            changed = true;
        }

        var camera = player.GetComponentInChildren<Camera>();
        float farClip = current.terrainSize * FarClipFactor;
        if (!Mathf.Approximately(camera.farClipPlane, farClip))
        {
            camera.farClipPlane = farClip;
            EditorUtility.SetDirty(camera);
            changed = true;
        }

        var cameraData = camera.GetUniversalAdditionalCameraData();
        if (cameraData.antialiasing != AntialiasingMode.TemporalAntiAliasing)
        {
            cameraData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            cameraData.taaSettings.quality = TemporalAAQuality.High;
            EditorUtility.SetDirty(cameraData);
            changed = true;
        }

        if (!cameraData.dithering)
        {
            cameraData.dithering = true;
            EditorUtility.SetDirty(cameraData);
            changed = true;
        }

        var terrainComponent = gen.GetComponent<Terrain>();
        if (terrainComponent.heightmapMaximumLOD != TerrainMaxLod
            || !Mathf.Approximately(terrainComponent.heightmapPixelError, TerrainPixelError)
            || !Mathf.Approximately(terrainComponent.basemapDistance, TerrainBasemapDistance)
            || terrainComponent.shadowCastingMode != ShadowCastingMode.Off)
        {
            terrainComponent.heightmapPixelError = TerrainPixelError;
            terrainComponent.heightmapMaximumLOD = TerrainMaxLod;
            terrainComponent.basemapDistance = TerrainBasemapDistance;
            terrainComponent.shadowCastingMode = ShadowCastingMode.Off;

            EditorUtility.SetDirty(terrainComponent);
            changed = true;
        }

        if (Object.FindAnyObjectByType<WeatherState>() == null)
        {
            CreateWeather();
            changed = true;
        }

        var weatherState = Object.FindAnyObjectByType<WeatherState>();

        var atmosphereSettings = LoadOrCreateAtmosphereSettings();

        var atmosphere = Object.FindAnyObjectByType<AtmosphereController>();
        if (atmosphere == null)
        {
            atmosphere = weatherState.gameObject.AddComponent<AtmosphereController>();
            changed = true;
        }

        var windField = Object.FindAnyObjectByType<WindField>();
        windField.Bind(LoadOrCreate<WindSettings>(WindPath));
        EditorUtility.SetDirty(windField);

        var thermometer = weatherState.GetComponent<TemperatureField>();
        if (thermometer == null)
        {
            thermometer = weatherState.gameObject.AddComponent<TemperatureField>();
            changed = true;
        }

        thermometer.Bind(weatherState, windField, Object.FindAnyObjectByType<TimeOfDay>());

        // SEA-LEVEL AIR TEMPERATURE. The freezing level, the snow line and whether the sea
        // sees rain or snow all derive from this one number (`TemperatureField`), so the
        // scene must not disagree with the component's own default.
        //
        // -2 was tried and put the freezing level BELOW sea level at every hour and every
        // storm, which made the plain snow-covered year round and meant no rain ever reached
        // the water. +7.8 puts it at 1163-1394 m. [DECISIONS.md, "Ovanin kotu"]
        var thermoSerialized = new SerializedObject(thermometer);
        thermoSerialized.FindProperty("seaLevelCelsius").floatValue = 7.8f;
        thermoSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(thermometer);

        gen.Measure();

        var driver = Object.FindAnyObjectByType<AltitudeWeatherDriver>();
        driver.Bind(weatherState, windField, player.transform,
            Object.FindAnyObjectByType<TimeOfDay>(),
            LoadOrCreate<WeatherDriverSettings>(WeatherDriverPath),
            gen.groundAltitude, gen.peakAltitude);
        EditorUtility.SetDirty(driver);

        var shelter = windField.GetComponent<TerrainWindShelter>();
        if (shelter == null)
        {
            shelter = windField.gameObject.AddComponent<TerrainWindShelter>();
            changed = true;
        }

        shelter.Bind(player.transform, SurfaceComponent(gen, ref changed), windField);
        EditorUtility.SetDirty(shelter);

        EnsureCloudLayerProbe(player, ref changed);

        var precipitationRenderer = Object.FindAnyObjectByType<PrecipitationRenderer>();
        var precipitationShader = AssetDatabase.LoadAssetAtPath<Shader>(PrecipitationShaderPath);
        if (precipitationShader == null)
            throw new System.InvalidOperationException($"Shader not found: {PrecipitationShaderPath}");

        var streakDatabase = AssetDatabase.LoadAssetAtPath<RainStreakDatabase>(RainStreakDatabasePath);
        if (streakDatabase == null)
            Debug.LogWarning(
                $"No streak database: {RainStreakDatabasePath}. Menu: " +
                "To The Summit/Rain/Set Up Streak Database");

        var streakSet = precipitationRenderer.GetComponent<RainStreakWorkingSet>();
        if (streakSet == null)
            streakSet = precipitationRenderer.gameObject.AddComponent<RainStreakWorkingSet>();
        streakSet.Bind(streakDatabase);

        precipitationRenderer.Bind(weatherState, windField, precipitationShader,
            Object.FindAnyObjectByType<CloudLayerProbe>(), player.transform,
            streakSet, Object.FindAnyObjectByType<TimeOfDay>());
        EditorUtility.SetDirty(precipitationRenderer);

        var staleRenderer = precipitationRenderer.GetComponent<MeshRenderer>();
        if (staleRenderer != null)
        {
            Object.DestroyImmediate(staleRenderer);
            changed = true;
        }

        var staleFilter = precipitationRenderer.GetComponent<MeshFilter>();
        if (staleFilter != null)
        {
            Object.DestroyImmediate(staleFilter);
            changed = true;
        }

        if (Object.FindAnyObjectByType<WeatherAudio>() == null)
        {
            CreateWeatherAudio();
            changed = true;
        }

        EnsureStorm(weatherState, player.transform, atmosphere,
            gen.GetComponent<Terrain>(), ref changed);

        var staleHud = Object.FindAnyObjectByType<PerformanceHud>();
        if (staleHud != null && staleHud.GetComponent<PerformanceSampler>() == null)
        {
            Object.DestroyImmediate(staleHud.gameObject);
            changed = true;
        }

        if (Object.FindAnyObjectByType<PerformanceSampler>() == null)
        {
            CreateDebugTools();
            changed = true;
        }

        var look = player.GetComponent<MouseLook>();
        if (look == null)
        {
            look = player.gameObject.AddComponent<MouseLook>();
            changed = true;
        }
        look.Bind(camera.transform.parent);
        EditorUtility.SetDirty(look);

        var flyer = player.GetComponent<FreeFlyMovement>();
        if (flyer == null)
        {
            flyer = player.gameObject.AddComponent<FreeFlyMovement>();
            changed = true;
        }
        flyer.Bind(camera.transform.parent);
        EditorUtility.SetDirty(flyer);

        if (flyer.enabled)
        {
            flyer.enabled = false;
            changed = true;
        }

        if (Object.FindAnyObjectByType<TimeOfDay>() == null)
        {
            CreateTimeOfDay();
            changed = true;
        }

        EnsureSunAndMoon(ref changed);
        EnsureSkyDrivers(ref changed);

    #if URP_PBSKY
        var timeOfDay = Object.FindAnyObjectByType<TimeOfDay>();
        timeOfDay.SunIntensity = 3.030782f;
        timeOfDay.MoonIntensity = 0.0199f;
        timeOfDay.MoonColor = new Color(0.586f, 0.653f, 0.818f, 1f);
    #endif

        atmosphere.Bind(
            atmosphereSettings,
            weatherState,
            Object.FindAnyObjectByType<WindField>(),
            Object.FindAnyObjectByType<TimeOfDay>(),
            driver,
            camera,
            LoadOrCreateSkyMaterial());
        EditorUtility.SetDirty(atmosphere);

        var lookController = Object.FindAnyObjectByType<LookController>();
        if (lookController == null)
        {
            CreateLookController(weatherState);
            changed = true;
        }
        else if (lookController.Look == null)
        {
            lookController.Bind(LoadOrCreateLookSettings(), weatherState,
                Object.FindAnyObjectByType<TimeOfDay>(),
                Object.FindAnyObjectByType<AtmosphereController>());
            changed = true;
        }

        lookController = Object.FindAnyObjectByType<LookController>();
        if (lookController != null)
        {
            var lookSerialized = new SerializedObject(lookController);
            var share = lookSerialized.FindProperty("adaptShare");
            if (!Mathf.Approximately(share.floatValue, 0.35f))
            {
                share.floatValue = 0.35f;
                lookSerialized.ApplyModifiedProperties();
                changed = true;
            }
        }

        Phase("systems");

        EnsureTerrainSurface(gen, regenerated, ref changed);
        Phase("surface maps");

        EnsureSea(gen, camera, weatherState, windField, atmosphere, thermometer, ref changed);
        Phase("sea");

        EnsureRouteOverlay(gen, ref changed);
        EnsureClimbHud(player, gen, ref changed);
        EnsureDebugMenu(player, ref changed);
        Phase("route, HUD");

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
        }

        Phase("scene save");

        if (clock.ElapsedMilliseconds >= 200)
            ToolLog.Write($"[Bootstrap] total {clock.ElapsedMilliseconds} ms{timings}");
    }

    static readonly string[] BandNames =
    {
        "0-25%   foot",
        "25-50%  lower slope",
        "50-75%  upper slope",
        "75-100% summit"
    };

    static readonly float[,] BandTargets =
    {
        { 75f, 20f,  5f,  0f },
        { 55f, 30f, 14f,  1f },
        { 35f, 35f, 27f,  3f },
        { 15f, 30f, 45f, 10f }
    };

    static void WriteMountainReport(MountainGenerator gen)
    {
        var report = new System.Text.StringBuilder();

        report.AppendLine($"# Mountain {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        var s = gen.Settings;
        report.AppendLine($"{s.terrainSize} m base, {s.terrainHeight} m peak, " +
                          $"{s.heightmapResolution} resolution " +
                          $"({s.terrainSize / (s.heightmapResolution - 1f):F2} m/sample)");
        report.AppendLine($"Mean slope {gen.meanSlopeDegrees:F1}°   " +
                          $"peak altitude {gen.peakAltitude:F0} m " +
                          $"({gen.peakAltitude / s.terrainHeight * 100f:F0}% of ceiling)");
        report.AppendLine($"Base {s.baseHeight * s.terrainHeight:F0} m");
        report.AppendLine();
        report.AppendLine("## Slope distribution per altitude band (mountain only)");
        report.AppendLine("Band                 Walk   Stren  Climb  Wall   Mean");
        report.AppendLine("                     0-30°  30-45°  45-70°   70°+");

        for (int i = 0; i < MountainGenerator.AltitudeBandCount; i++)
        {
            var band = gen.bands[i];
            report.AppendLine(
                $"{BandNames[i],-20} {band.walkable,5:F1}  {band.strenuous,6:F1}  " +
                $"{band.climbable,6:F1}  {band.wall,5:F1}  {band.meanDegrees,5:F1}°");
            report.AppendLine(
                $"{"target",-20} {BandTargets[i, 0],5:F0}  {BandTargets[i, 1],6:F0}  " +
                $"{BandTargets[i, 2],6:F0}  {BandTargets[i, 3],5:F0}");
        }

        report.AppendLine();
        report.AppendLine("## Parameters");
        report.AppendLine($"seed {s.seed}   mountainRadius {s.mountainRadius}   baseHeight {s.baseHeight}");
        report.AppendLine($"radialDistortion {s.radialDistortion}   radialFrequency {s.radialFrequency}");
        report.AppendLine($"secondary peaks {s.secondaryPeaks}   spread {s.peakSpread}   " +
                          $"height {s.peakHeightRange}   radius {s.peakRadiusRange}");
        report.AppendLine($"octaves {gen.EffectiveOctaves} (from resolution)   " +
                          $"baseFrequency {s.baseFrequency}   " +
                          $"lacunarity {s.lacunarity}   gain {s.gain}");
        report.AppendLine($"ridgeInfluence {s.ridgeInfluence}   ridgeFootDamping {s.ridgeFootDamping}   " +
                          $"ridgeSharpness {s.ridgeSharpness}");
        report.AppendLine($"warp {s.warpStrength} @ {s.warpFrequency}   " +
                          $"detail {s.warpDetailStrength} @ {s.warpDetailFrequency}");
        report.AppendLine($"terrace coarse {s.coarseTerraceStrength}/{s.coarseTerraceBands}   " +
                          $"fine {s.fineTerraceStrength}/{s.fineTerraceBands}");
        report.AppendLine($"terraceSharpness {s.terraceSharpness}   " +
                          $"offset {s.terraceOffsetAmount} @ {s.terraceOffsetFrequency}   " +
                          $"variation {s.terraceVariation} @ {s.terraceVariationFrequency}");
        report.AppendLine($"erosion {s.erosionIterations} iterations   talus {s.talusAngle}°   " +
                          $"rate {s.erosionRate}");
        report.AppendLine($"summit plateau {s.summitPlateauStart} flatness {s.summitFlatness}");

        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/mountain.log", report.ToString(), System.Text.Encoding.UTF8);
    }

    static Material LoadOrCreateSkyMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (material != null) return material;

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
        if (shader == null)
            throw new System.InvalidOperationException($"Shader not found: {SkyShaderPath}");

        material = new Material(shader) { name = "Sky" };
        AssetDatabase.CreateAsset(material, SkyMaterialPath);
        AssetDatabase.SaveAssets();

        return material;
    }

    static void EnsureFogFeature()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException($"Renderer not found: {RendererPath}");

        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(FogComputePath);
        if (compute == null)
            throw new System.InvalidOperationException($"Fog compute shader not found: {FogComputePath}");

        VolumetricFogFeature feature = null;
        foreach (var existing in renderer.rendererFeatures)
            if (existing is VolumetricFogFeature found) { feature = found; break; }

        bool isNew = feature == null;
        if (isNew)
        {
            feature = ScriptableObject.CreateInstance<VolumetricFogFeature>();
            feature.name = "Volumetric Fog";
        }

        var serialized = new SerializedObject(feature);
        serialized.FindProperty("compute").objectReferenceValue = compute;
        serialized.FindProperty("settings").objectReferenceValue =
            LoadOrCreate<VolumetricFogSettings>(FogSettingsPath);

        var skyFogShader = AssetDatabase.LoadAssetAtPath<Shader>(SkyFogShaderPath);
        if (skyFogShader == null)
            throw new System.InvalidOperationException($"Sky fog shader not found: {SkyFogShaderPath}");

        serialized.FindProperty("skyFogShader").objectReferenceValue = skyFogShader;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (isNew)
        {
            renderer.rendererFeatures.Add(feature);
            AssetDatabase.AddObjectToAsset(feature, renderer);
        }

        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererPath);
    }

    static void EnsureSkyFeature()
    {
    #if URP_PBSKY
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException($"Renderer not found: {RendererPath}");

        foreach (var existing in renderer.rendererFeatures)
            if (existing is PhysicallyBasedSkyURP) return;

        var skyShader = Shader.Find("Hidden/Skybox/PhysicallyBasedSky");
        var lutShader = Shader.Find("Hidden/Sky/PhysicallyBasedSkyPrecomputation");
        if (skyShader == null || lutShader == null)
            throw new System.InvalidOperationException(
                "Sky shaders not found. Check package: " +
                "Packages/com.jiaozi158.unity-physically-based-sky-urp");

        var feature = ScriptableObject.CreateInstance<PhysicallyBasedSkyURP>();
        feature.name = "Physically Based Sky";

        var serialized = new SerializedObject(feature);
        serialized.FindProperty("m_Shader").objectReferenceValue = skyShader;
        serialized.FindProperty("m_LutShader").objectReferenceValue = lutShader;
        serialized.FindProperty("m_FallbackSkyMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        serialized.FindProperty("m_VolumetricCloudsMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(CloudMaterialPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        renderer.rendererFeatures.Add(feature);
        AssetDatabase.AddObjectToAsset(feature, renderer);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererPath);
    #endif
    }

    static void SetCloudSunAttenuation(VolumetricCloudsURP feature)
    {
    #if URP_PBSKY
        var serialized = new SerializedObject(feature);
        var property = serialized.FindProperty("sunAttenuation");
        if (property.boolValue) return;

        property.boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(feature);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererPath);
    #endif
    }

    static void EnsureSkyVolume()
    {
    #if URP_PBSKY
        if (cloudVolume == null)
            throw new System.InvalidOperationException(
                "Sky volume must be setup after cloud volume: `cloudVolume` is null.");

        ApplySkyOverrides(cloudVolume.sharedProfile);

        bool ambientChanged = RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Skybox
                           || !Mathf.Approximately(RenderSettings.reflectionIntensity, 1f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        RenderSettings.reflectionIntensity = 1f;

        if (ambientChanged)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    #endif
    }

    #if URP_PBSKY
    static void ApplySkyOverrides(UnityEngine.Rendering.VolumeProfile profile)
    {
        if (!profile.TryGet(out VisualEnvironment visualEnvironment))
        {
            visualEnvironment = profile.Add<VisualEnvironment>(overrides: true);
            visualEnvironment.name = nameof(VisualEnvironment);
            AssetDatabase.AddObjectToAsset(visualEnvironment, profile);
        }

        if (!profile.TryGet(out PhysicallyBasedSky pbrSky))
        {
            pbrSky = profile.Add<PhysicallyBasedSky>(overrides: true);
            pbrSky.name = nameof(PhysicallyBasedSky);
            AssetDatabase.AddObjectToAsset(pbrSky, profile);
        }

        if (!profile.TryGet(out Fog fog))
        {
            fog = profile.Add<Fog>(overrides: true);
            fog.name = nameof(Fog);
            AssetDatabase.AddObjectToAsset(fog, profile);
        }

        SetSky(visualEnvironment.skyType, (int)VisualEnvironment.SkyType.PhysicallyBased);
        SetSky(visualEnvironment.renderingSpace, VisualEnvironment.RenderingSpace.World);
        SetSky(visualEnvironment.skyAmbientMode, VisualEnvironment.SkyAmbientMode.Dynamic);
        SetSky(pbrSky.type, PhysicallyBasedSky.PhysicallyBasedSkyModel.EarthAdvanced);
        SetSky(pbrSky.atmosphericScattering, true);
        SetSky(pbrSky.spaceEmissionMultiplier, 0.55f);
        SetSky(fog.enabled, false);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile));
    }
    #endif

    static void SetSky<T>(VolumeParameter<T> parameter, T value)
    {
        parameter.value = value;
        parameter.overrideState = true;
    }

    static void EnsureCloudFeature()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException($"Renderer not found: {RendererPath}");

        var shader = Shader.Find("Hidden/Sky/VolumetricClouds");
        if (shader == null)
            throw new System.InvalidOperationException("Cloud shader not found: Hidden/Sky/VolumetricClouds");

        var material = AssetDatabase.LoadAssetAtPath<Material>(CloudMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "VolumetricClouds" };
            AssetDatabase.CreateAsset(material, CloudMaterialPath);
        }
        BindCloudTextures(material);

        foreach (var existing in renderer.rendererFeatures)
            if (existing is VolumetricCloudsURP existingClouds)
            {
                SetCloudSunAttenuation(existingClouds);
                return;
            }

        var feature = ScriptableObject.CreateInstance<VolumetricCloudsURP>();
        feature.name = "Volumetric Clouds";

        var serialized = new SerializedObject(feature);
        serialized.FindProperty("material").objectReferenceValue = material;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        SetCloudSunAttenuation(feature);
        feature.Create();

        renderer.rendererFeatures.Add(feature);
        AssetDatabase.AddObjectToAsset(feature, renderer);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererPath);
    }

    static void EnsurePrecipitationFeature()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException($"Renderer not found: {RendererPath}");

        foreach (var existing in renderer.rendererFeatures)
            if (existing is PrecipitationRenderFeature) return;

        var feature = ScriptableObject.CreateInstance<PrecipitationRenderFeature>();
        feature.name = "Precipitation After Clouds";
        feature.Create();

        renderer.rendererFeatures.Add(feature);
        AssetDatabase.AddObjectToAsset(feature, renderer);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererPath);
    }

    static void EnsureCloudLayerProbe(FirstPersonController player, ref bool changed)
    {
        var probe = Object.FindAnyObjectByType<CloudLayerProbe>();
        if (probe == null)
        {
            probe = new GameObject("CloudLayerProbe").AddComponent<CloudLayerProbe>();
            changed = true;
        }

        probe.Bind(cloudVolume,
            Object.FindAnyObjectByType<AltitudeWeatherDriver>(),
            player.transform);

        EditorUtility.SetDirty(probe);

        var driver = Object.FindAnyObjectByType<CloudWeatherDriver>();
        if (driver == null)
        {
            driver = probe.gameObject.AddComponent<CloudWeatherDriver>();
            changed = true;
        }

        driver.Bind(cloudVolume,
            Object.FindAnyObjectByType<WindField>(),
            Object.FindAnyObjectByType<AltitudeWeatherDriver>(),
            Object.FindAnyObjectByType<AtmosphereController>(),
            LoadOrCreate<CloudWeatherSettings>(CloudWeatherPath));
        EditorUtility.SetDirty(driver);
    }

    static void EnsureSkyDrivers(ref bool changed)
    {
        var probe = Object.FindAnyObjectByType<CloudLayerProbe>();
        if (probe == null)
            throw new System.InvalidOperationException(
                "Sky drivers must be setup after cloud probe.");

        var skyDriver = Object.FindAnyObjectByType<SkyWeatherDriver>();
        if (skyDriver == null)
        {
            skyDriver = probe.gameObject.AddComponent<SkyWeatherDriver>();
            changed = true;
        }

        skyDriver.Bind(cloudVolume,
            Object.FindAnyObjectByType<WeatherState>(),
            Object.FindAnyObjectByType<TimeOfDay>(),
            LoadOrCreate<SkyWeatherSettings>(SkyWeatherPath));
        EditorUtility.SetDirty(skyDriver);

        var ambientBaker = Object.FindAnyObjectByType<SkyAmbientBaker>();
        if (ambientBaker == null)
        {
            ambientBaker = probe.gameObject.AddComponent<SkyAmbientBaker>();
            changed = true;
        }

        ambientBaker.Bind(Object.FindAnyObjectByType<TimeOfDay>());
        EditorUtility.SetDirty(ambientBaker);
    }

    static void BindCloudTextures(Material material)
    {
        material.SetTexture("_Worley128RGBA", LoadCloudTexture("WorleyNoise128RGBA"));
        material.SetTexture("_ErosionNoise", LoadCloudTexture("WorleyNoise32RGB"));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }

    static Texture LoadCloudTexture(string fileName)
    {
        var path = $"Assets/VolumetricClouds/Textures/{fileName}.png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
        if (texture == null)
            throw new System.InvalidOperationException($"Cloud texture not found: {path}");
        return texture;
    }

    static void EnsureCloudVolume()
    {
        var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        if (volume == null)
            throw new System.InvalidOperationException("No Volume in scene, cloud volume cannot be added.");
        if (volume.sharedProfile == null)
            throw new System.InvalidOperationException($"{volume.name} Volume has no profile.");

        var profile = volume.sharedProfile;
        if (!profile.TryGet(out VolumetricClouds clouds))
        {
            clouds = profile.Add<VolumetricClouds>(overrides: true);
            clouds.name = nameof(VolumetricClouds);
            AssetDatabase.AddObjectToAsset(clouds, profile);
        }

        cloudVolume = volume;

        clouds.state.value = true;
        clouds.localClouds.value = true;
        clouds.localClouds.overrideState = true;
        clouds.shadows.value = true;
        clouds.shadows.overrideState = true;
        clouds.cloudMap.value = CloudMapGenerator.EnsureExists();
        clouds.cloudMap.overrideState = true;

        SetCloud(clouds.cloudCoverage, 0.65f);
        SetCloud(clouds.densityMultiplier, 0.39f);
        SetCloud(clouds.globalSpeed, 20f);
        SetCloud(clouds.globalOrientation, 205f);
        SetCloud(clouds.shapeSpeedMultiplier, 1.00f);
        SetCloud(clouds.erosionSpeedMultiplier, 0.25f);
        SetCloud(clouds.verticalShapeWindSpeed, 0f);
        SetCloud(clouds.verticalErosionWindSpeed, 0f);
        SetCloud(clouds.shapeFactor, 0.40f);
        SetCloud(clouds.shapeScale, 34.1f);
        SetCloud(clouds.anvilAmount, 0f);
        SetCloud(clouds.bottomAltitude, 2086f);
        SetCloud(clouds.altitudeRange, 3298f);
        SetCloud(clouds.altitudeDistortion, 0.25f);
        SetCloud(clouds.cloudMapSize, 48000f);
        SetCloud(clouds.earthCurvature, 0.00f);

        SetCloud(clouds.erosionFactor, 1.00f);
        SetCloud(clouds.erosionScale, 107f);
        SetCloud(clouds.erosionOcclusion, 0.10f);
        SetCloud(clouds.microErosion, true);
        SetCloud(clouds.microErosionFactor, 0.70f);
        SetCloud(clouds.microErosionScale, 200f);

        SetCloud(clouds.extinctionCoefficient, 0.040f);
        SetCloud(clouds.powderEffectIntensity, 0.25f);
        SetCloud(clouds.multiScattering, 0.50f);
        SetCloud(clouds.ambientLightProbeDimmer, 1.00f);
        SetCloud(clouds.sunLightDimmer, 1.00f);
        SetCloud(clouds.shadowOpacity, 1.00f);
        SetCloud(clouds.shadowOpacityFallback, 0.00f);
        SetCloud(clouds.shadowResolution, VolumetricClouds.CloudShadowResolution.Ultra1024);
        SetCloud(clouds.shadowDistance, 12000f);

        // MEASURED, NOT PICKED. The clouds were 6.0 ms of an 11.4 ms frame — turning them off
        // took it to 5.4 and the main thread stopped waiting on the GPU. Breaking that bill down
        // one switch at a time: shadows, erosion and micro erosion are noise; the march is the
        // whole cost. 128/8 -> 96/6 measured 10.5 ms -> 7.6 ms with no visible difference on the
        // same frame, and 64/4 (6.3 ms) lost the thin clouds' detail.
        //
        // Both numbers sit inside what the paper's own table ships `[N22 p.183]`: light samples
        // 6 on PS4 and 10 on PS5, view samples 60-90 and 96-180.
        //
        // THIS IS THE SOURCE. The value was set on the asset three times and came back each
        // time — the bootstrap writes it from here on every run, which is exactly the trap
        // SYMPTOMS.md records under "kod diskte doğru, ekranda eski".
        SetCloud(clouds.numPrimarySteps, 96);
        SetCloud(clouds.numLightSteps, 6);
        SetCloud(clouds.temporalAccumulationFactor, 0.95f);
        SetCloud(clouds.perceptualBlending, 1.00f);
        SetCloud(clouds.fadeInStart, 0f);
        SetCloud(clouds.fadeInDistance, 300f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile));
    }

    static void SetCloud(FloatParameter parameter, float value)
    {
        parameter.value = value;
        parameter.overrideState = true;
    }

    static void SetCloud(IntParameter parameter, int value)
    {
        parameter.value = value;
        parameter.overrideState = true;
    }

    static void SetCloud(BoolParameter parameter, bool value)
    {
        parameter.value = value;
        parameter.overrideState = true;
    }

    static void SetCloud(VolumetricClouds.CloudShadowResolutionParameter parameter, VolumetricClouds.CloudShadowResolution value)
    {
        parameter.value = value;
        parameter.overrideState = true;
    }

    static readonly int BaseNoiseId = Shader.PropertyToID("_BaseNoise");
    static readonly int DetailNoiseId = Shader.PropertyToID("_DetailNoise");

    static MountainSettings LoadOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<MountainSettings>(SettingsPath);
        if (settings != null) return settings;

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));

        settings = ScriptableObject.CreateInstance<MountainSettings>();
        settings.heightProfile = MountainSettings.DefaultProfile();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssets();

        return settings;
    }

    static MountainGenerator CreateMountain(MountainSettings settings)
    {
        string dir = Path.GetDirectoryName(TerrainDataPath);
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
        if (data == null)
        {
            data = new TerrainData { heightmapResolution = settings.heightmapResolution };
            data.size = new Vector3(settings.terrainSize, settings.terrainHeight, settings.terrainSize);
            AssetDatabase.CreateAsset(data, TerrainDataPath);
        }

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "Mountain";
        return go.AddComponent<MountainGenerator>();
    }

    static FirstPersonController CreatePlayer()
    {
        var player = new GameObject("Player");

        var controller = player.AddComponent<CharacterController>();
        controller.height = PlayerHeight;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);

        var head = new GameObject("CameraPivot");
        head.transform.SetParent(player.transform, false);
        head.transform.localPosition = new Vector3(0f, EyeHeight, 0f);

        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam = camGo.GetComponent<Camera>();
        }

        cam.transform.SetParent(head.transform, false);
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;
        cam.farClipPlane = current.terrainSize * FarClipFactor;

        player.AddComponent<CursorLock>();

        return player.AddComponent<FirstPersonController>();
    }

    static void CreateWeather()
    {
        var go = new GameObject("Weather");

        go.AddComponent<WeatherState>();
        go.AddComponent<WindField>();
        go.AddComponent<AltitudeWeatherDriver>();

        var precipitation = new GameObject("Precipitation");
        precipitation.transform.SetParent(go.transform, false);
        precipitation.AddComponent<PrecipitationRenderer>();
    }

    static void CreateWeatherAudio()
    {
        var state = Object.FindAnyObjectByType<WeatherState>();
        var wind = Object.FindAnyObjectByType<WindField>();

        var audio = new GameObject("Audio");
        audio.transform.SetParent(state.transform, false);
        audio.AddComponent<WeatherAudio>().Bind(
            state, wind,
            LoadClip("Assets/Audio/Rain/rain_light.wav"),
            LoadClip("Assets/Audio/Rain/rain_heavy.wav"),
            LoadClips("Assets/Audio/Wind", "wind_calm"),
            LoadClips("Assets/Audio/Wind", "wind_storm"));

        var thunder = new GameObject("Thunder");
        thunder.transform.SetParent(state.transform, false);
        thunder.AddComponent<ThunderPlayer>();
    }

    static void EnsureStorm(WeatherState state, Transform observer,
        AtmosphereController atmosphere, Terrain terrain, ref bool changed)
    {
        var thunder = Object.FindAnyObjectByType<ThunderPlayer>();
        if (thunder == null)
            throw new System.InvalidOperationException("No ThunderPlayer in scene.");

        thunder.Bind(
            state,
            LoadOrCreate<ThunderSettings>(ThunderPath),
            LoadClips("Assets/Audio/Thunder", "thunder_distant"),
            LoadClips("Assets/Audio/Thunder", "thunder_close"));
        EditorUtility.SetDirty(thunder);

        var tuning = LoadOrCreate<LightningSettings>(LightningPath);

        var flash = Object.FindAnyObjectByType<LightningFlash>();
        if (flash == null)
        {
            var lightning = new GameObject("Lightning");
            lightning.transform.SetParent(thunder.transform.parent, false);
            flash = lightning.AddComponent<LightningFlash>();
            changed = true;
        }

        var scatterLut = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Settings/LightningScatterLut.asset");
        if (scatterLut == null)
            throw new System.InvalidOperationException(
                "No lightning scatter LUT: Assets/Settings/LightningScatterLut.asset");

        flash.Bind(thunder, atmosphere, observer, tuning,
            Object.FindAnyObjectByType<CloudLayerProbe>(), scatterLut, 9000f, terrain);
        EditorUtility.SetDirty(flash);

        var bolt = flash.GetComponent<LightningBolt>();
        if (bolt == null)
        {
            bolt = flash.gameObject.AddComponent<LightningBolt>();
            changed = true;
        }

        bolt.Bind(flash, terrain, tuning, LoadOrCreateBoltMaterial());
        EditorUtility.SetDirty(bolt);
    }

    static Material LoadOrCreateBoltMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(BoltMaterialPath);
        if (material != null) return material;

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(BoltShaderPath);
        if (shader == null)
            throw new System.InvalidOperationException($"Shader not found: {BoltShaderPath}");

        material = new Material(shader) { name = "LightningBolt" };
        AssetDatabase.CreateAsset(material, BoltMaterialPath);
        AssetDatabase.SaveAssets();

        return material;
    }

    static AudioClip LoadClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
            throw new System.InvalidOperationException($"Audio clip not found: {path}");

        return clip;
    }

    static AudioClip[] LoadClips(string folder, string prefix)
    {
        var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
        var paths = new System.Collections.Generic.List<string>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileName(path).StartsWith(prefix))
                paths.Add(path);
        }

        if (paths.Count == 0)
            throw new System.InvalidOperationException($"Audio clips not found: {folder}/{prefix}*");

        paths.Sort(System.StringComparer.Ordinal);

        var clips = new AudioClip[paths.Count];
        for (int i = 0; i < paths.Count; i++)
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);

        return clips;
    }

    static void CreateTimeOfDay()
    {
        var go = new GameObject("Time Of Day");
        go.AddComponent<TimeOfDay>();
    }

    static void EnsureLightData(Light light, ref bool changed)
    {
        if (light.GetComponent<UniversalAdditionalLightData>() != null) return;

        light.gameObject.AddComponent<UniversalAdditionalLightData>();
        EditorUtility.SetDirty(light.gameObject);
        changed = true;
    }

    static void EnsureSunAndMoon(ref bool changed)
    {
        var timeOfDay = Object.FindAnyObjectByType<TimeOfDay>();

        var moonObject = GameObject.Find(MoonLightName);
        if (moonObject == null)
        {
            moonObject = new GameObject(MoonLightName);
            changed = true;
        }

        var moon = moonObject.GetComponent<Light>();
        if (moon == null)
        {
            moon = moonObject.AddComponent<Light>();
            changed = true;
        }

        moon.type = LightType.Directional;
        moon.shadows = LightShadows.Soft;

        Light sun = null;
        foreach (var light in Object.FindObjectsByType<Light>())
        {
            if (light.type != LightType.Directional) continue;
            if (light == moon) continue;
            if (light.GetComponent<LightningFlash>() != null) continue;

            if (sun != null)
                throw new System.InvalidOperationException(
                    $"Multiple directional light candidates in scene: {sun.name} and {light.name}.");

            sun = light;
        }

        if (sun == null)
            throw new System.InvalidOperationException(
                "Sun light not found in scene.");

        EnsureLightData(sun, ref changed);
        EnsureLightData(moon, ref changed);

        timeOfDay.Bind(sun, moon);
        EditorUtility.SetDirty(timeOfDay);
        EditorUtility.SetDirty(moon);
    }

    static void CreateLookController(WeatherState weatherState)
    {
        var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        if (volume == null)
            throw new System.InvalidOperationException("No Volume found in scene.");

        volume.gameObject.AddComponent<LookController>().Bind(
            LoadOrCreateLookSettings(),
            weatherState,
            Object.FindAnyObjectByType<TimeOfDay>(),
            Object.FindAnyObjectByType<AtmosphereController>());
    }

    static LookSettings LoadOrCreateLookSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<LookSettings>(LookSettingsPath);
        if (settings != null) return settings;

        Directory.CreateDirectory(Path.GetDirectoryName(LookSettingsPath));

        settings = ScriptableObject.CreateInstance<LookSettings>();
        AssetDatabase.CreateAsset(settings, LookSettingsPath);
        AssetDatabase.SaveAssets();

        return settings;
    }

    static void EnsureDebugMenu(FirstPersonController player, ref bool changed)
    {
        var menu = Object.FindAnyObjectByType<DebugMenu>();
        if (menu == null)
        {
            menu = new GameObject("Debug Menu").AddComponent<DebugMenu>();
            changed = true;
        }

        menu.Bind(
            player,
            player.GetComponent<FreeFlyMovement>(),
            Object.FindAnyObjectByType<WeatherState>(),
            Object.FindAnyObjectByType<AltitudeWeatherDriver>(),
            Object.FindAnyObjectByType<WindField>(),
            Object.FindAnyObjectByType<ThunderPlayer>(),
            Object.FindAnyObjectByType<LightningFlash>(),
            Object.FindAnyObjectByType<TimeOfDay>(),
            Object.FindAnyObjectByType<AtmosphereController>(),
            Object.FindAnyObjectByType<PrecipitationRenderer>(),
            Object.FindAnyObjectByType<PerformanceHud>(),
            Object.FindAnyObjectByType<ClimbHud>(),
            player.GetComponent<CursorLock>(),
            Object.FindAnyObjectByType<RouteOverlay>(FindObjectsInactive.Include),
            cloudVolume,
            Object.FindAnyObjectByType<CloudWeatherDriver>(),
            Object.FindAnyObjectByType<SeaStateController>());

        EditorUtility.SetDirty(menu);
    }

    static bool RemoveMissingScripts(Scene scene)
    {
        int removed = 0;

        foreach (var root in scene.GetRootGameObjects())
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);

        if (removed > 0)
            ToolLog.Write($"Removed {removed} missing scripts from scene.");

        return removed > 0;
    }

    static void CreateDebugTools()
    {
        var go = new GameObject("Debug");

        var sampler = go.AddComponent<PerformanceSampler>();
        var watchdog = go.AddComponent<PerformanceWatchdog>();
        var hud = go.AddComponent<PerformanceHud>();

        watchdog.Bind(sampler);
        hud.Bind(sampler, watchdog);

        go.AddComponent<ClimbHud>();
        go.AddComponent<FrameRateCap>();
    }

    static string RouteSignature(MountainRoute route)
    {
        if (route == null) return "-";

        var hash = new System.Text.StringBuilder();
        hash.Append(route.road.Count);

        foreach (MountainRoute.Branch branch in route.branches)
            hash.Append('|').Append(branch.marks.Count);

        hash.Append('|').Append(route.camps.Count);

        Sample(hash, route.road);
        foreach (MountainRoute.Branch branch in route.branches) Sample(hash, branch.marks);
        Sample(hash, route.camps);

        return hash.ToString();
    }

    static void Sample(System.Text.StringBuilder hash, List<MountainRoute.Mark> marks)
    {
        if (marks.Count == 0) return;

        int step = Mathf.Max(1, marks.Count / 8);
        for (int i = 0; i < marks.Count; i += step)
            hash.Append('|').Append(marks[i].position.x.ToString("F4"))
                .Append(',').Append(marks[i].position.y.ToString("F4"))
                .Append(',').Append(marks[i].radius.ToString("F1"));
    }

    static TerrainSurface SurfaceComponent(MountainGenerator gen, ref bool changed)
    {
        var terrain = gen.GetComponent<Terrain>();
        var surface = terrain.GetComponent<TerrainSurface>();
        if (surface != null) return surface;

        changed = true;
        return terrain.gameObject.AddComponent<TerrainSurface>();
    }

    static void EnsureTerrainSurface(MountainGenerator gen, bool regenerated, ref bool changed)
    {
        var terrain = gen.GetComponent<Terrain>();

        float prevailing = LoadOrCreate<WindSettings>(WindPath).prevailingDegrees;

        var maps = SurfaceMapBaker.Load();
        if (regenerated || !SurfaceMapBaker.MapsCurrent(prevailing))
        {
            EditorUtility.DisplayProgressBar("Surface", "Extracting surface maps...", 0.5f);
            try { maps = SurfaceMapBaker.Bake(terrain, prevailing); }
            finally { EditorUtility.ClearProgressBar(); }

            changed = true;
        }

        var surface = SurfaceComponent(gen, ref changed);
        surface.Bind(
            BindSandTextures(LoadOrCreateTerrainMaterialSettings()),
            Object.FindAnyObjectByType<WeatherState>(),
            Object.FindAnyObjectByType<WindField>(),
            Object.FindAnyObjectByType<TimeOfDay>(),
            Object.FindAnyObjectByType<AtmosphereController>(),
            Object.FindAnyObjectByType<TemperatureField>(),
            Object.FindAnyObjectByType<FirstPersonController>() is FirstPersonController fpc ? fpc.transform : null,
            maps,
            SurfaceMapBaker.LoadDrift(),
            SurfaceMapBaker.LoadNormals(),
            SurfaceMapBaker.LoadHorizon(),
            SurfaceMapBaker.LoadHeight(),
            AssetDatabase.LoadAssetAtPath<Shader>(SurfaceShaderPath));

        EditorUtility.SetDirty(surface);
    }

    static void EnsureSea(MountainGenerator gen, Camera camera,
                          WeatherState weatherState, WindField windField,
                          AtmosphereController atmosphere,
                          TemperatureField thermometer, ref bool changed)
    {
        var seaSettings = AssetDatabase.LoadAssetAtPath<SeaSettings>(SeaSettingsPath);
        if (seaSettings == null)
            throw new System.InvalidOperationException($"Settings not found: {SeaSettingsPath}");

        var surfaceShader = AssetDatabase.LoadAssetAtPath<Shader>(SeaSurfaceShaderPath);
        if (surfaceShader == null)
            throw new System.InvalidOperationException($"Shader not found: {SeaSurfaceShaderPath}");

        var spectrum = AssetDatabase.LoadAssetAtPath<ComputeShader>(SeaSpectrumPath);
        var fft = AssetDatabase.LoadAssetAtPath<ComputeShader>(SeaFftPath);
        var foam = AssetDatabase.LoadAssetAtPath<ComputeShader>(SeaFoamPath);
        if (spectrum == null || fft == null || foam == null)
            throw new System.InvalidOperationException(
                $"Compute shaders not found: {SeaSpectrumPath} / {SeaFftPath} / {SeaFoamPath}");

        var root = Object.FindAnyObjectByType<SeaManager>(FindObjectsInactive.Include);
        if (root == null)
        {
            root = new GameObject("Sea").AddComponent<SeaManager>();
            changed = true;
        }

        var bridge = root.GetComponent<SeaEnvironmentBridge>();
        if (bridge == null)
        {
            bridge = root.gameObject.AddComponent<SeaEnvironmentBridge>();
            changed = true;
        }

        var seaState = root.GetComponent<SeaStateController>();
        if (seaState == null)
        {
            seaState = root.gameObject.AddComponent<SeaStateController>();
            changed = true;
        }
        seaState.Bind(seaSettings, windField);
        EditorUtility.SetDirty(seaState);

        var time = Object.FindAnyObjectByType<TimeOfDay>();
        var timeSo = new SerializedObject(time);
        var sun = timeSo.FindProperty("sun").objectReferenceValue as Light;

        bridge.Bind(windField, weatherState, time, atmosphere, thermometer, sun, seaSettings,
                    seaState);
        EditorUtility.SetDirty(bridge);

        // THE WADE LIMIT. The boundary is depth, not the waterline: shallow water is walkable
        // and the camera must not go under (`DECISIONS.md`).
        var wade = root.GetComponent<SeaWadeLimit>();
        if (wade == null)
        {
            wade = root.gameObject.AddComponent<SeaWadeLimit>();
            changed = true;
        }
        wade.Bind(Object.FindAnyObjectByType<FirstPersonController>(),
                  seaSettings,
                  gen.GetComponent<Terrain>(),
                  camera != null ? camera.transform : null);
        EditorUtility.SetDirty(wade);

        root.Bind(seaSettings, bridge, gen.GetComponent<Terrain>());
        EditorUtility.SetDirty(root);

        var surface = Object.FindAnyObjectByType<SeaSurface>(FindObjectsInactive.Include);
        if (surface == null)
        {
            var go = new GameObject("Sea Surface",
                                    typeof(MeshFilter), typeof(MeshRenderer));
            surface = go.AddComponent<SeaSurface>();
            changed = true;
        }

        var sim = root.GetComponent<SeaSimulation>();
        if (sim == null)
        {
            sim = root.gameObject.AddComponent<SeaSimulation>();
            changed = true;
        }

        sim.Bind(seaSettings, bridge, spectrum, fft, foam, surface);
        EditorUtility.SetDirty(sim);

        surface.Bind(seaSettings, surfaceShader, camera.transform);
        EditorUtility.SetDirty(surface);

        var wetness = root.GetComponent<SeaWetnessDriver>();
        if (wetness == null)
        {
            wetness = root.gameObject.AddComponent<SeaWetnessDriver>();
            changed = true;
        }

        wetness.Bind(seaSettings);
        EditorUtility.SetDirty(wetness);

        foreach (var b in new Behaviour[] { seaState, root, sim, surface, wetness })
        {
            b.enabled = false;
            b.enabled = true;
        }
    }

    static void EnsureRouteOverlay(MountainGenerator gen, ref bool changed)
    {
        var overlay = Object.FindAnyObjectByType<RouteOverlay>(FindObjectsInactive.Include);
        if (overlay == null)
        {
            var host = new GameObject("Route Overlay");
            host.SetActive(false);
            overlay = host.AddComponent<RouteOverlay>();
            changed = true;
        }

        overlay.Bind(AssetDatabase.LoadAssetAtPath<MountainRoute>(RoutePath),
                     gen.GetComponent<Terrain>());
        EditorUtility.SetDirty(overlay);
    }

    static AtmosphereSettings LoadOrCreateAtmosphereSettings() =>
        LoadOrCreate<AtmosphereSettings>(AtmospherePath);

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var settings = AssetDatabase.LoadAssetAtPath<T>(path);
        if (settings != null) return settings;

        Directory.CreateDirectory(Path.GetDirectoryName(path));

        settings = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(settings, path);
        AssetDatabase.SaveAssets();

        return settings;
    }

    static TerrainMaterialSettings BindSandTextures(TerrainMaterialSettings settings)
    {
        Texture2D Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{SandTexturePath}/{name}.png");

        Texture2D albedo = Load("T_Sand_Albedo");
        Texture2D normal = Load("T_Sand_Normal");
        Texture2D rough = Load("T_Sand_Roughness");
        Texture2D ao = Load("T_Sand_AO");

        if (albedo == null || normal == null || rough == null || ao == null)
            throw new System.InvalidOperationException(
                $"Sand textures not found: {SandTexturePath}");

        if (settings.sandAlbedo == albedo && settings.sandNormal == normal
            && settings.sandRoughness == rough && settings.sandAO == ao)
            return settings;

        settings.sandAlbedo = albedo;
        settings.sandNormal = normal;
        settings.sandRoughness = rough;
        settings.sandAO = ao;

        settings.revision++;
        EditorUtility.SetDirty(settings);

        return settings;
    }

    static TerrainMaterialSettings LoadOrCreateTerrainMaterialSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<TerrainMaterialSettings>(TerrainMaterialPath);
        if (settings != null) return settings;

        Directory.CreateDirectory(Path.GetDirectoryName(TerrainMaterialPath));

        settings = ScriptableObject.CreateInstance<TerrainMaterialSettings>();
        AssetDatabase.CreateAsset(settings, TerrainMaterialPath);
        AssetDatabase.SaveAssets();

        return settings;
    }

    static void EnsureClimbHud(FirstPersonController player, MountainGenerator gen, ref bool changed)
    {
        var climbHud = Object.FindAnyObjectByType<ClimbHud>();
        if (climbHud == null)
        {
            climbHud = Object.FindAnyObjectByType<PerformanceHud>().gameObject.AddComponent<ClimbHud>();
            changed = true;
        }

        climbHud.Bind(
            player.transform,
            gen.GetComponent<Terrain>(),
            Object.FindAnyObjectByType<AltitudeWeatherDriver>(),
            Object.FindAnyObjectByType<WeatherState>(),
            Object.FindAnyObjectByType<WindField>(),
            Object.FindAnyObjectByType<TimeOfDay>(),
            Object.FindAnyObjectByType<AtmosphereController>(),
            Object.FindAnyObjectByType<TerrainSurface>(),
            Object.FindAnyObjectByType<TemperatureField>(),
            Object.FindAnyObjectByType<CloudLayerProbe>());

        EditorUtility.SetDirty(climbHud);
    }

    static bool RemoveStaleTestObjects()
    {
        bool changed = false;

        foreach (string name in new[] { "TestCharacter", "Vegetation" })
        {
            var stale = GameObject.Find(name);
            if (stale == null) continue;

            Object.DestroyImmediate(stale);
            changed = true;
        }

        return changed;
    }

    static void SpawnPose(out Vector3 position, out Quaternion rotation)
    {
        var route = AssetDatabase.LoadAssetAtPath<MountainRoute>(RoutePath);
        var terrain = Object.FindAnyObjectByType<Terrain>();

        if (route != null && route.spawnSet && terrain != null)
        {
            position = GroundAt(MountainRoute.ToWorld(route.spawn, terrain));

            float yaw = route.spawnYaw * Mathf.Deg2Rad;
            rotation = Quaternion.LookRotation(
                new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)), Vector3.up);
            return;
        }

        position = SpawnPoint();
        rotation = Quaternion.identity;
    }

    static Vector3 GroundAt(Vector3 position)
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        float top = terrain.transform.position.y + terrain.terrainData.size.y + 100f;
        var ray = new Ray(new Vector3(position.x, top, position.z), Vector3.down);

        var ground = terrain.GetComponent<TerrainCollider>();
        if (ground != null && ground.Raycast(ray, out RaycastHit hit, top + 1000f))
            return new Vector3(position.x, hit.point.y + 0.05f, position.z);

        return new Vector3(position.x,
            terrain.SampleHeight(position) + terrain.transform.position.y + 0.05f,
            position.z);
    }

    static Vector3 SpawnPoint()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) return new Vector3(current.terrainSize * 0.45f, 5f, 0f);

        const float SafeEdge = 0.47f;

        float radius = current.mountainRadius;
        float distance = Mathf.Min(radius * 1.12f, SafeEdge) * current.terrainSize;

        var pos = new Vector3(distance, 0f, 0f);

        float top = terrain.transform.position.y + terrain.terrainData.size.y + 100f;
        var ray = new Ray(new Vector3(pos.x, top, pos.z), Vector3.down);

        var ground = terrain.GetComponent<TerrainCollider>();
        if (ground != null && ground.Raycast(ray, out var hit, top + 1000f))
        {
            pos.y = hit.point.y + 0.05f;
            return pos;
        }

        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.05f;
        return pos;
    }
}
