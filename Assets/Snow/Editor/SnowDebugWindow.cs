// ROL: kar sisteminin teşhis penceresi — durum dokularının kanallarını gösterir,
// bölge/snap sayılarını yazar. Bir de sahneyi kuran düğme.
// Çağıran: menü — To The Summit/Kar/Kar Teşhisi.

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SnowDebugWindow : EditorWindow
{
    static readonly string[] ChannelNames =
    {
        "R — swe (kar su eşdeğeri, m)",
        "G — rhoN (normalize yoğunluk)",
        "B — wet (ıslaklık)",
        "A — disturb (tazelik)",
        "h — türetilmiş taban derinlik (m)",
    };

    /// Her kanalın görüntüleme tavanı. rhoN/wet/disturb zaten 0..1.
    static readonly float[] ChannelRanges = { 0.60f, 1f, 1f, 1f, 1.20f };

    /// Yakalama dokusunun kanalları başka şeyler ölçüyor; kendi adları ve
    /// kendi aralıkları var. Durum dokusunun ölçeğiyle gösterilirse hepsi
    /// beyaz patlar.
    static readonly string[] CaptureChannelNames =
    {
        "R — alt yüzey Y (gözlemciye göre, m)",
        "G — hız X (m/s)",
        "B — hız Z (m/s)",
        "A — maske",
    };

    /// x = gösterimden çıkarılan taban, y = aralık.
    static readonly Vector2[] CaptureChannelScales =
    {
        new(-SnowConstants.CaptureBelow, SnowConstants.CaptureBelow + SnowConstants.CaptureAbove),
        new(-6f, 12f),
        new(-6f, 12f),
        new(0f, 1f),
    };

    enum PreviewSource
    {
        Durum,
        Iz,
        Yakalama,
        GokyuzuGorunurlugu,
        RuzgarGolgesi,
    }

    static readonly string[] SourceNames =
    {
        "Durum (RT_Snow)",
        "İz (RT_Trail)",
        "Yakalama (RT_CaptureBlur)",
        "Gökyüzü görünürlüğü (RT_SkyVis)",
        "Rüzgâr gölgesi (RT_WindShadow)",
    };

    PreviewSource source;
    int channel;
    float gridSize = 1f;
    Vector2 scroll;

    Material debugMaterial;
    RenderTexture preview;

    [MenuItem("To The Summit/Kar/Kar Teşhisi", false, 50)]
    static void Open() => GetWindow<SnowDebugWindow>("Kar Teşhisi").minSize = new Vector2(420f, 560f);

    void OnDisable()
    {
        if (debugMaterial != null) DestroyImmediate(debugMaterial);
        if (preview != null) { preview.Release(); DestroyImmediate(preview); }

        debugMaterial = null;
        preview = null;
    }

    /// BLIT BURADA, OnGUI'DE DEĞİL. Play modunda OnGUI içinde `Graphics.Blit`
    /// çağırmak aktif render hedefini editör GUI geçişinin altından çekiyor ve
    /// pencerenin tamamı siyah kalıyor — hiçbir istisna basmadan.
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

        bool raw = source == PreviewSource.GokyuzuGorunurlugu || source == PreviewSource.RuzgarGolgesi;
        bool capture = source == PreviewSource.Yakalama;

        float worldSize = raw ? SnowConstants.SkyAreaSize : manager.Settings.QualityData.AreaSize;
        Vector2 worldCenter = manager.AreaCenter;

        int captureChannel = Mathf.Min(channel, CaptureChannelScales.Length - 1);
        Vector2 captureScale = CaptureChannelScales[captureChannel];

        debugMaterial.SetFloat(SnowShaderIDs.DebugMode,
            raw ? 5f : (capture ? captureChannel : channel));
        debugMaterial.SetFloat(SnowShaderIDs.DebugRange,
            raw ? 1f : (capture ? captureScale.y : ChannelRanges[channel]));
        debugMaterial.SetFloat(SnowShaderIDs.DebugBias, capture ? captureScale.x : 0f);
        debugMaterial.SetFloat(SnowShaderIDs.DebugGridSize, gridSize);
        debugMaterial.SetVector(SnowShaderIDs.DebugWorldCenter,
            new Vector4(worldCenter.x, worldCenter.y, 0f, 0f));
        debugMaterial.SetFloat(SnowShaderIDs.DebugWorldSize, worldSize);

        Graphics.Blit(shown, preview, debugMaterial, 0);
    }

    RenderTexture SourceTexture(SnowManager m) => source switch
    {
        PreviewSource.Iz => m.TrailTexture,
        PreviewSource.Yakalama => m.CaptureBlurTexture,
        PreviewSource.GokyuzuGorunurlugu => m.SkyVisTexture,
        PreviewSource.RuzgarGolgesi => m.WindShadowTexture,
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

        EditorGUILayout.LabelField("Kurulum", EditorStyles.boldLabel);
        if (GUILayout.Button("Sahneyi kur", GUILayout.Height(28f))) SetupScene();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Durum", EditorStyles.boldLabel);

        SnowManager manager = SnowManager.Active;

        if (manager == null || !manager.IsReady)
        {
            EditorGUILayout.HelpBox("SnowManager etkin değil. Play'e basın veya sahneyi kurun.",
                                    MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        SnowQualityData q = manager.Settings.QualityData;

        EditorGUILayout.LabelField("Kalite", manager.Settings.Quality.ToString());
        EditorGUILayout.LabelField("Çözünürlük", q.Resolution.ToString());
        EditorGUILayout.LabelField("Teksel", (manager.TexelSize * 100f).ToString("0.###") + " cm");
        EditorGUILayout.LabelField("Bölge merkezi",
            manager.AreaCenter.x.ToString("0.000") + " , " + manager.AreaCenter.y.ToString("0.000"));
        EditorGUILayout.LabelField("Son kaydırma",
            manager.LastScrollTexels.x + " , " + manager.LastScrollTexels.y + " teksel");
        EditorGUILayout.LabelField("Yakalama",
            (manager.CaptureActive ? "aktif" : "boşta") +
            "   deformer " + SnowDeformerRegistry.Count);

        ISnowEnvironmentSource env = manager.Environment;

        if (env != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Çevre (okunan)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Rüzgâr", env.WindSpeed.ToString("0.0") + " m/s  " +
                                                 env.WindDirection.ToString("0.00"));
            EditorGUILayout.LabelField("Sıcaklık", env.TemperatureC.ToString("0.0") + " °C");
            EditorGUILayout.LabelField("Güneş yüksekliği", env.SunElevation01.ToString("0.00"));
            EditorGUILayout.LabelField("Yağış", env.PrecipKind + "  " +
                                                env.PrecipIntensity01.ToString("0.00"));
            EditorGUILayout.LabelField("Sis", env.FogDensity01.ToString("0.00"));
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Yayınlanan durum", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("IsSnowing", SnowRuntimeState.IsSnowing.ToString());
        EditorGUILayout.LabelField("SnowfallIntensity01", SnowRuntimeState.SnowfallIntensity01.ToString("0.00"));
        EditorGUILayout.LabelField("GroundCoverage01", SnowRuntimeState.GroundCoverage01.ToString("0.00"));
        EditorGUILayout.LabelField("LooseSnowFraction", SnowRuntimeState.LooseSnowFraction.ToString("0.00"));
        EditorGUILayout.LabelField("Stormness01", SnowRuntimeState.Stormness01.ToString("0.00"));

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Görüntü", EditorStyles.boldLabel);
        source = (PreviewSource)EditorGUILayout.Popup("Doku", (int)source, SourceNames);

        bool raw = source == PreviewSource.GokyuzuGorunurlugu || source == PreviewSource.RuzgarGolgesi;
        using (new EditorGUI.DisabledScope(raw))
        {
            if (source == PreviewSource.Yakalama)
            {
                channel = Mathf.Min(channel, CaptureChannelNames.Length - 1);
                channel = EditorGUILayout.Popup("Kanal", channel, CaptureChannelNames);
            }
            else
            {
                channel = EditorGUILayout.Popup("Kanal", channel, ChannelNames);
            }
        }

        gridSize = EditorGUILayout.Slider("Izgara (m)", gridSize, 0.25f, 8f);
        // IZGARA BİR TEST DEĞİL, ÖLÇEK ÇUBUĞU. Faz 1'de dokular boş — içinde
        // ızgarayla kıyaslanacak hiçbir şey yok. Kaydırma doğruluğu göz kararıyla
        // değil `To The Summit/Kar/Kaydırma Sınaması` ile ölçülüyor.
        EditorGUILayout.HelpBox(
            "Yeşil ızgara DÜNYAYA çakılı, ölçek çubuğudur. Kaydırma doğruluğu " +
            "menüdeki \"Kaydırma Sınaması\" ile ölçülür — gözle değil.",
            MessageType.None);

        if (preview != null)
        {
            Rect rect = GUILayoutUtility.GetAspectRect(1f);
            EditorGUI.DrawPreviewTexture(rect, preview);
        }

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ kurulum

    const string SettingsPath = "Assets/Snow/Settings/SnowSettings.asset";
    const string ComputePath = "Assets/Snow/Shaders/SnowSim.compute";
    const string CaptureShaderPath = "Assets/Snow/Shaders/Hidden_SnowCaptureDepth.shader";
    const string SnowLitShaderPath = "Assets/Snow/Shaders/SnowLit.shader";
    const string SkyShaderPath = "Assets/Snow/Shaders/Hidden_SnowSkyDepth.shader";
    const string SnowfallComputePath = "Assets/Snow/Shaders/SnowfallSim.compute";
    const string ParticleShaderPath = "Assets/Snow/Shaders/SnowfallParticle.shader";
    const string FlakeMaterialPath = "Assets/Snow/Settings/M_SnowFlake.mat";
    const string DriftMaterialPath = "Assets/Snow/Settings/M_SnowDrift.mat";
    const string SnowLitMaterialPath = "Assets/Snow/Settings/M_SnowLit.mat";

    /// SAHNE ELLE DÜZENLENMİYOR. Proje kuralı: bileşen ekleme, referans bağlama ve
    /// layer açma kodda yapılıyor; kullanıcı yalnız düğmeye basıyor.
    static void SetupScene()
    {
        EnsureLayer(SnowProjectCheck.DeformerLayer);
        EnsureLayer(SnowProjectCheck.OccluderLayer);

        SnowSettings settings = LoadOrCreateSettings();

        var manager = Object.FindAnyObjectByType<SnowManager>();

        if (manager == null)
        {
            var host = new GameObject("Kar Sistemi");
            manager = host.AddComponent<SnowManager>();
        }

        GameObject go = manager.gameObject;

        var bridge = go.GetComponent<SnowEnvironmentBridge>();
        if (bridge == null) bridge = go.AddComponent<SnowEnvironmentBridge>();

        var ground = go.GetComponent<SnowGroundHeight>();
        if (ground == null) ground = go.AddComponent<SnowGroundHeight>();

        var clipmap = go.GetComponent<SnowClipmap>();
        if (clipmap == null) clipmap = go.AddComponent<SnowClipmap>();

        if (go.GetComponent<SnowCoverageDriver>() == null)
            go.AddComponent<SnowCoverageDriver>();

        var snowfall = go.GetComponent<SnowfallRenderer>();
        if (snowfall == null) snowfall = go.AddComponent<SnowfallRenderer>();

        var player = Object.FindAnyObjectByType<FirstPersonController>();

        var bridgeSerialized = new SerializedObject(bridge);
        bridgeSerialized.FindProperty("sunLight").objectReferenceValue = FindSun();
        bridgeSerialized.ApplyModifiedProperties();

        var groundSerialized = new SerializedObject(ground);
        groundSerialized.FindProperty("settings").objectReferenceValue = settings;
        groundSerialized.FindProperty("terrain").objectReferenceValue =
            Object.FindAnyObjectByType<Terrain>();
        groundSerialized.FindProperty("bakeCenter").objectReferenceValue =
            player != null ? player.transform : null;
        groundSerialized.ApplyModifiedProperties();

        var managerSerialized = new SerializedObject(manager);
        managerSerialized.FindProperty("settings").objectReferenceValue = settings;
        managerSerialized.FindProperty("environmentSource").objectReferenceValue = bridge;
        managerSerialized.FindProperty("followTarget").objectReferenceValue =
            player != null ? player.transform : null;
        managerSerialized.FindProperty("groundHeight").objectReferenceValue = ground;
        managerSerialized.FindProperty("simCompute").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        managerSerialized.FindProperty("captureShader").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Shader>(CaptureShaderPath);
        managerSerialized.FindProperty("skyShader").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
        managerSerialized.ApplyModifiedProperties();

        Material snowLit = LoadOrCreateSnowMaterial();

        var clipmapSerialized = new SerializedObject(clipmap);
        clipmapSerialized.FindProperty("settings").objectReferenceValue = settings;
        clipmapSerialized.FindProperty("followTarget").objectReferenceValue =
            player != null ? player.transform : null;
        clipmapSerialized.FindProperty("snowMaterial").objectReferenceValue = snowLit;
        clipmapSerialized.ApplyModifiedProperties();

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

        managerSerialized.FindProperty("snowfallRenderer").objectReferenceValue = snowfall;
        managerSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(snowfall);
        EditorUtility.SetDirty(clipmap);
        EditorUtility.SetDirty(manager);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);

        int added = AddFeatureToRenderers();

        AssetDatabase.SaveAssets();

        Debug.Log($"Kar sistemi kuruldu. Renderer özelliği eklenen renderer sayısı: {added}.\n" +
                  SnowProjectCheck.Run());
    }

    /// Kar yüzeyi materyali. Halkaların HEPSİ aynı materyali paylaşıyor —
    /// ayrı ayrı olsaydı SRP Batcher dört halkayı dört çizime bölerdi.
    static Material LoadOrCreateSnowMaterial()
    {
        Texture2D breakup = SnowTextureBaker.EnsureBreakup();
        Texture2D detailNormal = SnowTextureBaker.EnsureDetailNormal();

        var material = AssetDatabase.LoadAssetAtPath<Material>(SnowLitMaterialPath);

        if (material == null)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SnowLitShaderPath);
            if (shader == null) return null;

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SnowLitMaterialPath);
        }

        material.SetTexture(SnowShaderIDs.SnowBreakup, breakup);
        material.SetTexture(SnowShaderIDs.SnowDetailNormal, detailNormal);
        EditorUtility.SetDirty(material);

        return material;
    }

    /// Tane ve savrulma materyalleri. İkisi aynı shader'ı paylaşıyor;
    /// farkları uzatma ve alpha çarpanı (spec §17.1).
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
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            if (l.type == LightType.Directional && l.isActiveAndEnabled) return l;

        return null;
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

    /// İlk boş KULLANICI yuvasına açıyor. Dolu bir yuvayı boşaltmıyor (spec §1.3).
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
            $"Boş layer yuvası kalmamış; '{name}' açılamadı. Kar sistemi kendi başına " +
            "bir layer boşaltmaz (spec §1.3).");
    }

    /// PAKET DOSYASINA YAZMA. `t:UniversalRendererData` araması paket içindeki
    /// renderer'ları da buluyor; oraya yazmak paketi kirletiyor ve güncellemede
    /// kayboluyor. Yalnız `Assets/` altındakiler.
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
