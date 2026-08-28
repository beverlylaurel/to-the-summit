// Diagnostic and inspection window for the snow subsystem — displays render texture channels,
// region/snap parameters, and scene setup controls.
// Invoked by: Menu — To The Summit/Snow/Snow Diagnostics.

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SnowDebugWindow : EditorWindow
{
    static readonly string[] ChannelNames =
    {
        "R — swe (snow water equivalent, m)",
        "G — rhoN (normalized density)",
        "B — wet (wetness)",
        "A — disturb (freshness)",
        "h — derived base depth (m)",
    };

    static readonly float[] ChannelRanges = { 0.60f, 1f, 1f, 1f, 1.20f };

    enum PreviewSource
    {
        State,
        Trail,
        SkyVisibility,
        WindShadow,
    }

    static readonly string[] SourceNames =
    {
        "State (RT_Snow)",
        "Trail (RT_Trail)",
        "Sky Visibility (RT_SkyVis)",
        "Wind Shadow (RT_WindShadow)",
    };

    PreviewSource source;
    int channel;
    float gridSize = 1f;
    float testSwe = 0.02f;
    Vector2 scroll;

    Material debugMaterial;
    RenderTexture preview;

    [MenuItem("To The Summit/Snow/Snow Diagnostics", false, 50)]
    static void Open() => GetWindow<SnowDebugWindow>("Snow Diagnostics").minSize = new Vector2(420f, 560f);

    void OnDisable()
    {
        if (debugMaterial != null) DestroyImmediate(debugMaterial);
        if (preview != null) { preview.Release(); DestroyImmediate(preview); }

        debugMaterial = null;
        preview = null;
    }

    void OnInspectorUpdate()
    {
        UpdatePreview();
        Repaint();
    }

    void UpdatePreview()
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null || !manager.IsReady) return;
        if (!EnsureMaterial()) return;

        RenderTexture shown = SourceTexture(manager);
        if (shown == null) return;

        EnsurePreview(shown.width);

        bool raw = source == PreviewSource.SkyVisibility || source == PreviewSource.WindShadow;
        float worldSize = raw ? SnowConstants.SkyAreaSize : manager.Settings.QualityData.AreaSize;
        Vector2 worldCenter = manager.AreaCenter;

        debugMaterial.SetFloat(SnowShaderIDs.DebugMode, raw ? 5f : channel);
        debugMaterial.SetFloat(SnowShaderIDs.DebugRange,
            raw ? 1f : ChannelRanges[channel]);
        debugMaterial.SetFloat(SnowShaderIDs.DebugBias, 0f);
        debugMaterial.SetFloat(SnowShaderIDs.DebugGridSize, gridSize);
        debugMaterial.SetVector(SnowShaderIDs.DebugWorldCenter,
            new Vector4(worldCenter.x, worldCenter.y, 0f, 0f));
        debugMaterial.SetFloat(SnowShaderIDs.DebugWorldSize, worldSize);

        Graphics.Blit(shown, preview, debugMaterial, 0);
    }

    void DrawIsolation()
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null) return;

        GameObject host = manager.gameObject;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Isolation", EditorStyles.boldLabel);

        Toggle<SnowfallRenderer>(host, "Snowfall (flakes)");
        Toggle<SnowCoverageDriver>(host, "Object snow cover");
        Toggle<SnowBurstParticles>(host, "Foot puff / spray");
        Toggle<SnowPersistence>(host, "Trail persistence");

        EditorGUILayout.HelpBox(
            "Toggle individual subsystems to isolate issues. Changes revert on exiting Play Mode.",
            MessageType.None);
    }

    static void Toggle<T>(GameObject host, string label) where T : MonoBehaviour
    {
        var component = host.GetComponent<T>();

        using (new EditorGUI.DisabledScope(component == null))
        {
            bool on = component != null && component.enabled;
            bool next = EditorGUILayout.Toggle(label, on);

            if (component != null && next != on) component.enabled = next;
        }
    }

    void DrawTestSnow()
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null || manager.Settings == null) return;

        SnowSettings settings = manager.Settings;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Test Snow", EditorStyles.boldLabel);

        testSwe = EditorGUILayout.Slider("Initial SWE (m)", testSwe, 0f, 0.10f);

        float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, settings.DefaultRhoN);
        float depth = testSwe * SnowConstants.RhoWater / Mathf.Max(rho, 1f);

        EditorGUILayout.LabelField("Equivalent",
            (depth * 100f).ToString("0.0") + " cm depth  (density " + rho.ToString("0") + " kg/m³)");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Fill World With Snow", GUILayout.Height(24f)))
            {
                settings.SetTestSnow(testSwe);
                manager.RefillRegion();
            }

            using (new EditorGUI.DisabledScope(!settings.HasTestSnow))
            {
                if (GUILayout.Button("Reset Settings", GUILayout.Height(24f)))
                {
                    settings.ClearTestSnow();
                    manager.RefillRegion();
                }
            }
        }

        EditorGUILayout.HelpBox(
            settings.HasTestSnow
                ? "Test snow is ACTIVE. Not saved to asset; resets on Play Mode exit or script recompilation."
                : "Using default values from asset (defaultSwe = " +
                  settings.DefaultSwe.ToString("0.000") + ").",
            settings.HasTestSnow ? MessageType.Warning : MessageType.None);
    }

    RenderTexture SourceTexture(SnowManager m) => source switch
    {
        PreviewSource.Trail => m.TrailTexture,
        PreviewSource.SkyVisibility => m.SkyVisTexture,
        PreviewSource.WindShadow => m.WindShadowTexture,
        _ => m.SnowTexture,
    };

    bool EnsureMaterial()
    {
        if (debugMaterial != null) return true;

        Shader shader = Shader.Find("Hidden/Snow/Debug");
        if (shader == null) return false;

        debugMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        return true;
    }

    void EnsurePreview(int size)
    {
        int shown = Mathf.Min(size, 512);
        if (preview != null && preview.width == shown) return;

        if (preview != null) { preview.Release(); DestroyImmediate(preview); }

        preview = new RenderTexture(shown, shown, 0, RenderTextureFormat.ARGB32)
        {
            name = "RT_SnowDebugPreview",
            hideFlags = HideFlags.HideAndDontSave,
        };
        preview.Create();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
        if (GUILayout.Button("Set Up Scene", GUILayout.Height(28f))) SetupScene();

        DrawIsolation();
        DrawTestSnow();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;

        if (manager == null || !manager.IsReady)
        {
            EditorGUILayout.HelpBox("SnowManager inactive. Enter Play Mode or run scene setup.",
                                    MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        SnowQualityData q = manager.Settings.QualityData;

        EditorGUILayout.LabelField("Quality", manager.Settings.Quality.ToString());
        EditorGUILayout.LabelField("Resolution", q.Resolution.ToString());
        EditorGUILayout.LabelField("Texel Size", (manager.TexelSize * 100f).ToString("0.###") + " cm");
        EditorGUILayout.LabelField("Area Center",
            manager.AreaCenter.x.ToString("0.000") + " , " + manager.AreaCenter.y.ToString("0.000"));
        EditorGUILayout.LabelField("Last Scroll",
            manager.LastScrollTexels.x + " , " + manager.LastScrollTexels.y + " texels");
        EditorGUILayout.LabelField("Trail Segments",
            (manager.CaptureActive ? "active" : "idle") +
            "   deformers " + SnowDeformerRegistry.Count);

        var profiler = Object.FindAnyObjectByType<SnowProfiler>();

        if (profiler != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Pass Times (spec §15.1)", EditorStyles.boldLabel);

            for (int i = 0; i < SnowProfiler.MarkerNames.Length; i++)
                EditorGUILayout.LabelField(SnowProfiler.MarkerNames[i],
                    profiler.MillisecondsFor(i).ToString("0.000") + " ms");

            EditorGUILayout.LabelField("TOTAL",
                profiler.TotalMilliseconds.ToString("0.000") + " ms   (target < 1.500)");
        }

        ISnowEnvironmentSource env = manager.Environment;

        if (env != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Environment Source", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Wind", env.WindSpeed.ToString("0.0") + " m/s  " +
                                                env.WindDirection.ToString("0.00"));
            EditorGUILayout.LabelField("Temperature", env.TemperatureC.ToString("0.0") + " °C");
            EditorGUILayout.LabelField("Sun Elevation", env.SunElevation01.ToString("0.00"));
            EditorGUILayout.LabelField("Precipitation", env.PrecipKind + "  " +
                                                       env.PrecipIntensity01.ToString("0.00"));
            EditorGUILayout.LabelField("Fog", env.FogDensity01.ToString("0.00"));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Broadcast State", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("IsSnowing", SnowRuntimeState.IsSnowing.ToString());
        EditorGUILayout.LabelField("SnowfallIntensity01", SnowRuntimeState.SnowfallIntensity01.ToString("0.00"));
        EditorGUILayout.LabelField("GroundCoverage01", SnowRuntimeState.GroundCoverage01.ToString("0.00"));
        EditorGUILayout.LabelField("LooseSnowFraction", SnowRuntimeState.LooseSnowFraction.ToString("0.00"));
        EditorGUILayout.LabelField("Stormness01", SnowRuntimeState.Stormness01.ToString("0.00"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        source = (PreviewSource)EditorGUILayout.Popup("Texture", (int)source, SourceNames);

        bool raw = source == PreviewSource.SkyVisibility || source == PreviewSource.WindShadow;
        using (new EditorGUI.DisabledScope(raw))
        {
            channel = EditorGUILayout.Popup("Channel", channel, ChannelNames);
        }

        gridSize = EditorGUILayout.Slider("Grid (m)", gridSize, 0.25f, 8f);
        EditorGUILayout.HelpBox(
            "Green grid represents world-space reference markers. Scroll accuracy is verified via " +
            "Menu -> To The Summit/Snow/Scroll Test.",
            MessageType.None);

        if (preview != null)
        {
            Rect rect = GUILayoutUtility.GetAspectRect(1f);
            EditorGUI.DrawPreviewTexture(rect, preview);
        }

        EditorGUILayout.EndScrollView();
    }

    const string SettingsPath = "Assets/Snow/Settings/SnowSettings.asset";
    const string ComputePath = "Assets/Snow/Shaders/SnowSim.compute";
    const string SkyShaderPath = "Assets/Snow/Shaders/Hidden_SnowSkyDepth.shader";
    const string SnowfallComputePath = "Assets/Snow/Shaders/SnowfallSim.compute";
    const string ParticleShaderPath = "Assets/Snow/Shaders/SnowfallParticle.shader";
    const string FlakeMaterialPath = "Assets/Snow/Settings/M_SnowFlake.mat";
    const string DriftMaterialPath = "Assets/Snow/Settings/M_SnowDrift.mat";
    const string PuffMaterialPath = "Assets/Snow/Settings/M_SnowPuff.mat";

    [MenuItem("To The Summit/Snow/Set Up Scene", false, 51)]
    static void SetupSceneMenu() => SetupScene();

    public static void SetupScene()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Snow subsystem setup cannot be run in Play Mode. " +
                           "Modifications made in Play Mode revert on exit. " +
                           "Exit Play Mode before running setup.");
            return;
        }

        EnsureLayer(SnowProjectCheck.DeformerLayer);
        EnsureLayer(SnowProjectCheck.OccluderLayer);

        SnowSettings settings = LoadOrCreateSettings();

        var manager = Object.FindAnyObjectByType<SnowManager>();

        if (manager == null)
        {
            var host = new GameObject("Snow System");
            manager = host.AddComponent<SnowManager>();
        }

        GameObject go = manager.gameObject;

        var bridge = go.GetComponent<SnowEnvironmentBridge>();
        if (bridge == null) bridge = go.AddComponent<SnowEnvironmentBridge>();

        var ground = go.GetComponent<SnowGroundHeight>();
        if (ground == null) ground = go.AddComponent<SnowGroundHeight>();

        var coverage = go.GetComponent<SnowCoverageDriver>();
        if (coverage == null) coverage = go.AddComponent<SnowCoverageDriver>();

        var snowfall = go.GetComponent<SnowfallRenderer>();
        if (snowfall == null) snowfall = go.AddComponent<SnowfallRenderer>();

        var burst = go.GetComponent<SnowBurstParticles>();
        if (burst == null) burst = go.AddComponent<SnowBurstParticles>();

        var sampler = go.GetComponent<SnowSampler>();
        if (sampler == null) sampler = go.AddComponent<SnowSampler>();

        var persistence = go.GetComponent<SnowPersistence>();
        if (persistence == null) persistence = go.AddComponent<SnowPersistence>();

        var fallLayers = go.GetComponent<SnowfallLayers>();
        if (fallLayers == null) fallLayers = go.AddComponent<SnowfallLayers>();

        var driftVfx = go.GetComponent<SnowDriftVfxController>();
        if (driftVfx == null) driftVfx = go.AddComponent<SnowDriftVfxController>();

        if (go.GetComponent<SnowProfiler>() == null)
            go.AddComponent<SnowProfiler>();

        var player = Object.FindAnyObjectByType<FirstPersonController>();

        var bridgeSerialized = new SerializedObject(bridge);
        bridgeSerialized.FindProperty("sunLight").objectReferenceValue = FindSun();
        bridgeSerialized.FindProperty("wind").objectReferenceValue =
            Object.FindAnyObjectByType<WindField>();
        bridgeSerialized.FindProperty("time").objectReferenceValue =
            Object.FindAnyObjectByType<TimeOfDay>();
        bridgeSerialized.FindProperty("temperature").objectReferenceValue =
            Object.FindAnyObjectByType<TemperatureField>();
        bridgeSerialized.FindProperty("weather").objectReferenceValue =
            Object.FindAnyObjectByType<WeatherState>();
        bridgeSerialized.FindProperty("atmosphere").objectReferenceValue =
            Object.FindAnyObjectByType<AtmosphereController>();
        bridgeSerialized.FindProperty("observer").objectReferenceValue =
            player != null ? player.transform : null;
        bridgeSerialized.ApplyModifiedProperties();

        var debugMenu = Object.FindAnyObjectByType<DebugMenu>();
        if (debugMenu != null)
        {
            var menuSerialized = new SerializedObject(debugMenu);
            menuSerialized.FindProperty("temperature").objectReferenceValue =
                Object.FindAnyObjectByType<TemperatureField>();
            menuSerialized.FindProperty("snowfall").objectReferenceValue = snowfall;
            menuSerialized.FindProperty("snowManager").objectReferenceValue = manager;
            menuSerialized.ApplyModifiedProperties();
        }

        var coverageSerialized = new SerializedObject(coverage);
        coverageSerialized.FindProperty("settings").objectReferenceValue = settings;
        coverageSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(coverage);

        var snowfallVfx = EnsureVfx(go, "VFX_Snowfall");
        var puffVfx = EnsureVfx(go, "VFX_SnowPuff");
        var sprayVfx = EnsureVfx(go, "VFX_SnowSpray");
        var spindriftVfx = EnsureVfx(go, "VFX_Spindrift");
        var curtainVfx = EnsureVfx(go, "VFX_SnowCurtain");

        var fallLayersSerialized = new SerializedObject(fallLayers);
        fallLayersSerialized.FindProperty("environment").objectReferenceValue = bridge;
        fallLayersSerialized.FindProperty("nearLayer").objectReferenceValue = snowfallVfx;
        fallLayersSerialized.FindProperty("computeFallback").objectReferenceValue = snowfall;
        fallLayersSerialized.FindProperty("followTarget").objectReferenceValue =
            Camera.main != null ? Camera.main.transform
                                : (player != null ? player.transform : null);

        fallLayersSerialized.FindProperty("groundReference").objectReferenceValue =
            player != null ? player.transform : null;
        fallLayersSerialized.ApplyModifiedProperties();

        var driftVfxSerialized = new SerializedObject(driftVfx);
        driftVfxSerialized.FindProperty("environment").objectReferenceValue = bridge;
        driftVfxSerialized.FindProperty("settings").objectReferenceValue = settings;
        driftVfxSerialized.FindProperty("spindrift").objectReferenceValue = spindriftVfx;
        driftVfxSerialized.FindProperty("curtain").objectReferenceValue = curtainVfx;
        driftVfxSerialized.ApplyModifiedProperties();

        var trailBody = EnsureTrailDeformer(player);
        EnsurePlayerSide(player, trailBody, sampler, burst, bridge);

        var driftVfxSerializedFollow = new SerializedObject(driftVfx);
        driftVfxSerializedFollow.FindProperty("followTarget").objectReferenceValue =
            player != null ? player.transform : null;
        driftVfxSerializedFollow.ApplyModifiedProperties();

        EditorUtility.SetDirty(fallLayers);
        EditorUtility.SetDirty(driftVfx);

        var groundSerialized = new SerializedObject(ground);
        groundSerialized.FindProperty("settings").objectReferenceValue = settings;
        groundSerialized.FindProperty("terrain").objectReferenceValue =
            Object.FindAnyObjectByType<Terrain>();
        groundSerialized.FindProperty("bakeCenter").objectReferenceValue =
            player != null ? player.transform : null;
        groundSerialized.ApplyModifiedProperties();

        var managerSerialized = new SerializedObject(manager);
        managerSerialized.FindProperty("detailNormal").objectReferenceValue =
            SnowTextureBaker.EnsureDetailNormal();
        managerSerialized.FindProperty("settings").objectReferenceValue = settings;
        managerSerialized.FindProperty("environmentSource").objectReferenceValue = bridge;
        managerSerialized.FindProperty("followTarget").objectReferenceValue =
            player != null ? player.transform : null;
        managerSerialized.FindProperty("groundHeight").objectReferenceValue = ground;
        managerSerialized.FindProperty("simCompute").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        managerSerialized.FindProperty("skyShader").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
        managerSerialized.ApplyModifiedProperties();

        Material flakeMat = LoadOrCreateParticleMaterial(FlakeMaterialPath, stretch: false, alpha: 1f);
        Material driftMat = LoadOrCreateParticleMaterial(DriftMaterialPath, stretch: true, alpha: 0.12f);

        var snowfallSerialized = new SerializedObject(snowfall);
        snowfallSerialized.FindProperty("settings").objectReferenceValue = settings;
        snowfallSerialized.FindProperty("snowfallCompute").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ComputeShader>(SnowfallComputePath);
        snowfallSerialized.FindProperty("flakeMaterial").objectReferenceValue = flakeMat;
        snowfallSerialized.FindProperty("driftMaterial").objectReferenceValue = driftMat;
        snowfallSerialized.FindProperty("followTarget").objectReferenceValue =
            Camera.main != null ? Camera.main.transform
                                : (player != null ? player.transform : null);
        snowfallSerialized.FindProperty("environmentSource").objectReferenceValue = bridge;
        snowfallSerialized.ApplyModifiedProperties();

        Material puffMat = LoadOrCreateParticleMaterial(PuffMaterialPath, stretch: false, alpha: 0.7f);

        var burstSerialized = new SerializedObject(burst);
        burstSerialized.FindProperty("snowfallCompute").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ComputeShader>(SnowfallComputePath);
        burstSerialized.FindProperty("material").objectReferenceValue = puffMat;
        burstSerialized.ApplyModifiedProperties();

        var samplerSerialized = new SerializedObject(sampler);
        samplerSerialized.FindProperty("manager").objectReferenceValue = manager;
        samplerSerialized.FindProperty("followTarget").objectReferenceValue =
            player != null ? player.transform : null;
        samplerSerialized.ApplyModifiedProperties();

        var persistenceSerialized = new SerializedObject(persistence);
        persistenceSerialized.FindProperty("manager").objectReferenceValue = manager;
        persistenceSerialized.FindProperty("simCompute").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        persistenceSerialized.ApplyModifiedProperties();

        managerSerialized.FindProperty("snowfallRenderer").objectReferenceValue = snowfall;
        managerSerialized.FindProperty("burstParticles").objectReferenceValue = burst;
        managerSerialized.FindProperty("persistence").objectReferenceValue = persistence;
        managerSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(persistence);
        EditorUtility.SetDirty(burst);
        EditorUtility.SetDirty(sampler);
        EditorUtility.SetDirty(snowfall);
        EditorUtility.SetDirty(manager);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);

        int added = AddFeatureToRenderers();

        AssetDatabase.SaveAssets();

        Debug.Log($"Snow subsystem set up. Render features added to {added} renderers.\n" +
                  SnowProjectCheck.Run());
    }

    static UnityEngine.VFX.VisualEffect EnsureVfx(GameObject host, string name)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(
            "Assets/Snow/VFX/" + name + ".vfx");

        if (asset == null) return null;

        Transform t = host.transform.Find(name);

        GameObject go = t != null ? t.gameObject : new GameObject(name);
        if (t == null) go.transform.SetParent(host.transform, false);

        var vfx = go.GetComponent<UnityEngine.VFX.VisualEffect>();
        if (vfx == null) vfx = go.AddComponent<UnityEngine.VFX.VisualEffect>();

        vfx.visualEffectAsset = asset;
        EditorUtility.SetDirty(vfx);

        return vfx;
    }

    static Transform EnsureTrailDeformer(FirstPersonController player)
    {
        if (player == null) return null;

        int layer = LayerMask.NameToLayer(SnowProjectCheck.DeformerLayer);
        if (layer < 0) return null;

        float footY = 0f;
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) footY = cc.center.y - cc.height * 0.5f;

        foreach (var oldName in new[] { "SnowFoot_L", "SnowFoot_R" })
        {
            Transform oldT = player.transform.Find(oldName);
            if (oldT != null) Object.DestroyImmediate(oldT.gameObject);
        }

        return EnsureTrailBody(player.transform, new Vector3(0f, footY, 0f), layer);
    }

    static Transform EnsureTrailBody(Transform parent, Vector3 localPos, int layer)
    {
        const string Name = "SnowTrailBody";

        Transform t = parent.Find(Name);
        GameObject go;

        if (t != null)
        {
            go = t.gameObject;

            var oldR = go.GetComponent<MeshRenderer>();
            if (oldR != null) Object.DestroyImmediate(oldR);

            var oldF = go.GetComponent<MeshFilter>();
            if (oldF != null) Object.DestroyImmediate(oldF);
        }
        else
        {
            go = new GameObject(Name);
            go.transform.SetParent(parent, false);
        }

        var col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        go.layer = layer;
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        var def = go.GetComponent<SnowDeformer>();
        if (def == null) def = go.AddComponent<SnowDeformer>();

        var defSo = new SerializedObject(def);
        defSo.FindProperty("radius").floatValue = 0.15f;
        defSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
        return go.transform;
    }

    static void EnsurePlayerSide(FirstPersonController player,
                                 Transform trailBody,
                                 SnowSampler sampler, SnowBurstParticles burst,
                                 SnowEnvironmentBridge bridge)
    {
        if (player == null) return;

        GameObject go = player.gameObject;
        Transform anchor = trailBody != null ? trailBody : player.transform;

        var rhythm = go.GetComponent<SnowStepRhythm>();
        if (rhythm == null) rhythm = go.AddComponent<SnowStepRhythm>();

        var rs = new SerializedObject(rhythm);
        rs.FindProperty("body").objectReferenceValue = go.GetComponent<CharacterController>();
        rs.ApplyModifiedProperties();

        var audio = go.GetComponent<SnowFootstepAudio>();
        if (audio == null) audio = go.AddComponent<SnowFootstepAudio>();

        var src = go.GetComponent<AudioSource>();
        if (src == null)
        {
            src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
        }

        var aus = new SerializedObject(audio);
        aus.FindProperty("sampler").objectReferenceValue = sampler;
        aus.FindProperty("source").objectReferenceValue = src;
        aus.FindProperty("footAnchor").objectReferenceValue = anchor;
        aus.FindProperty("rhythm").objectReferenceValue = rhythm;
        aus.ApplyModifiedProperties();

        var puff = go.GetComponent<SnowPuffEmitter>();
        if (puff == null) puff = go.AddComponent<SnowPuffEmitter>();

        var ps = new SerializedObject(puff);
        ps.FindProperty("sampler").objectReferenceValue = sampler;
        ps.FindProperty("particles").objectReferenceValue = burst;
        ps.FindProperty("footAnchor").objectReferenceValue = anchor;
        ps.FindProperty("rhythm").objectReferenceValue = rhythm;
        ps.ApplyModifiedProperties();

        var spray = go.GetComponent<SnowSprayController>();
        if (spray == null) spray = go.AddComponent<SnowSprayController>();

        var sps = new SerializedObject(spray);
        sps.FindProperty("sampler").objectReferenceValue = sampler;
        sps.FindProperty("particles").objectReferenceValue = burst;
        sps.FindProperty("footAnchor").objectReferenceValue = anchor;
        sps.FindProperty("velocitySource").objectReferenceValue = player.transform;
        sps.ApplyModifiedProperties();

        var move = go.GetComponent<SnowMovementModifier>();
        if (move == null) move = go.AddComponent<SnowMovementModifier>();

        var ms = new SerializedObject(move);
        ms.FindProperty("sampler").objectReferenceValue = sampler;
        ms.FindProperty("footAnchor").objectReferenceValue = anchor;
        ms.ApplyModifiedProperties();

        var accum = go.GetComponent<SnowCharacterAccumulator>();
        if (accum == null) accum = go.AddComponent<SnowCharacterAccumulator>();

        var acs = new SerializedObject(accum);
        acs.FindProperty("environmentSource").objectReferenceValue = bridge;
        acs.FindProperty("footAnchor").objectReferenceValue = anchor;
        acs.ApplyModifiedProperties();

        EditorUtility.SetDirty(go);
    }

    static Material LoadOrCreateParticleMaterial(string path, bool stretch, float alpha)
    {
        Texture2D atlas = SnowTextureBaker.EnsureFlakeAtlas();

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ParticleShaderPath);
            if (shader == null) return null;

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture(SnowShaderIDs.FlakeAtlas, atlas);
        material.SetFloat(SnowShaderIDs.StretchAlongVelocity, stretch ? 1f : 0f);
        material.SetFloat(SnowShaderIDs.AlphaScale, alpha);

        EditorUtility.SetDirty(material);
        return material;
    }

    static Light FindSun()
    {
        var clock = Object.FindAnyObjectByType<TimeOfDay>();

        if (clock != null)
        {
            var sun = new SerializedObject(clock)
                .FindProperty("sun").objectReferenceValue as Light;

            if (sun != null) return sun;
        }

        Light best = null;

        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (l.type != LightType.Directional || !l.isActiveAndEnabled) continue;
            if (best == null || l.intensity > best.intensity) best = l;
        }

        return best;
    }

    static SnowSettings LoadOrCreateSettings()
    {
        var asset = AssetDatabase.LoadAssetAtPath<SnowSettings>(SettingsPath);
        if (asset != null) return asset;

        string folder = Path.GetDirectoryName(SettingsPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Snow", "Settings");

        asset = ScriptableObject.CreateInstance<SnowSettings>();
        AssetDatabase.CreateAsset(asset, SettingsPath);
        return asset;
    }

    static void EnsureLayer(string name)
    {
        if (LayerMask.NameToLayer(name) >= 0) return;

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue)) continue;

            slot.stringValue = name;
            tagManager.ApplyModifiedProperties();
            return;
        }

        throw new System.InvalidOperationException(
            $"No free layer slots available; could not configure '{name}' (spec §1.3).");
    }

    static int AddFeatureToRenderers()
    {
        int added = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.StartsWith("Assets/")) continue;

            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) continue;

            bool has = false;
            foreach (ScriptableRendererFeature f in data.rendererFeatures)
                if (f is SnowRendererFeature) { has = true; break; }

            if (has) continue;

            var feature = ScriptableObject.CreateInstance<SnowRendererFeature>();
            feature.name = nameof(SnowRendererFeature);

            AssetDatabase.AddObjectToAsset(feature, data);
            data.rendererFeatures.Add(feature);

            EditorUtility.SetDirty(data);
            added++;
        }

        return added;
    }
}
