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
    float testSwe = 0.02f;
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

    /// İZOLASYON ANAHTARLARI. Belirtiden sorumluyu bulmanın tek yolu
    /// şüphelileri TEK TEK kapatmak; tahmin turu yakıyor.
    ///
    /// Şüphelilerin TAMAMI burada, tek seferde. Biri eksik olsaydı "hepsini
    /// kapattım hâlâ oluyor" cevabı hiçbir şey söylemezdi.
    ///
    /// Bu bölüm kar sistemi kabul edilince silinecek.
    void DrawIsolation()
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null) return;

        GameObject host = manager.gameObject;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("İzolasyon", EditorStyles.boldLabel);

        Toggle<SnowfallRenderer>(host, "Kar yağışı (taneler)");
        Toggle<SnowCurtainController>(host, "Süspansiyon perdeleri");
        Toggle<SnowSurface>(host, "Kar yüzeyi (zemin mesh'i)");
        Toggle<SnowCoverageDriver>(host, "Nesne üstü kar");
        Toggle<SnowBurstParticles>(host, "Ayak tozu / püskürtme");
        Toggle<SnowPersistence>(host, "İz kalıcılığı");

        EditorGUILayout.HelpBox(
            "Her satır bir şüpheli. Kapatıp belirtinin kaybolduğu satır sorumludur. " +
            "Play'den çıkınca hepsi geri açılır — sahneye yazılmıyor.",
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

    /// SINAMA KARI. Ayar dosyasına DOKUNMUYOR: değer `NonSerialized` bir
    /// alanda duruyor, Play'den çıkınca ve her derlemede sıfırlanıyor.
    /// Geri almayı unutmak mümkün değil.
    ///
    /// Bu bölüm kar sistemi kabul edilince silinecek
    /// (`DECISIONS.md` → Silinecek geçiciler).
    void DrawTestSnow()
    {
        SnowManager manager = SnowManager.Active;
        if (manager == null || manager.Settings == null) return;

        SnowSettings settings = manager.Settings;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Sınama karı", EditorStyles.boldLabel);

        testSwe = EditorGUILayout.Slider("Başlangıç SWE (m)", testSwe, 0f, 0.10f);

        float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, settings.DefaultRhoN);
        float depth = testSwe * SnowConstants.RhoWater / Mathf.Max(rho, 1f);

        EditorGUILayout.LabelField("Karşılığı",
            (depth * 100f).ToString("0.0") + " cm derinlik  (yoğunluk " + rho.ToString("0") + " kg/m³)");

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Dünyayı karla doldur", GUILayout.Height(24f)))
            {
                settings.SetTestSnow(testSwe);
                manager.RefillRegion();
            }

            using (new EditorGUI.DisabledScope(!settings.HasTestSnow))
            {
                if (GUILayout.Button("Ayarları geri al", GUILayout.Height(24f)))
                {
                    settings.ClearTestSnow();
                    manager.RefillRegion();
                }
            }
        }

        EditorGUILayout.HelpBox(
            settings.HasTestSnow
                ? "Sınama karı AÇIK. Ayar dosyasına yazılmadı; Play'den çıkınca " +
                  "veya derleme olunca kendiliğinden sıfırlanır."
                : "Ayar dosyasındaki değer kullanılıyor (defaultSwe = " +
                  settings.DefaultSwe.ToString("0.000") + ").",
            settings.HasTestSnow ? MessageType.Warning : MessageType.None);
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

        DrawIsolation();
        DrawTestSnow();

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

        var profiler = Object.FindAnyObjectByType<SnowProfiler>();

        if (profiler != null)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Geçiş süreleri (spec §15.1)", EditorStyles.boldLabel);

            for (int i = 0; i < SnowProfiler.MarkerNames.Length; i++)
                EditorGUILayout.LabelField(SnowProfiler.MarkerNames[i],
                    profiler.MillisecondsFor(i).ToString("0.000") + " ms");

            EditorGUILayout.LabelField("TOPLAM",
                profiler.TotalMilliseconds.ToString("0.000") + " ms   (hedef < 1.500)");
        }

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
    const string PuffMaterialPath = "Assets/Snow/Settings/M_SnowPuff.mat";
    const string CurtainShaderPath = "Assets/Snow/Shaders/SnowCurtain.shader";
    const string CurtainMaterialPath = "Assets/Snow/Settings/M_SnowCurtain.mat";
    const string SnowLitMaterialPath = "Assets/Snow/Settings/M_SnowLit.mat";

    /// SAHNE ELLE DÜZENLENMİYOR. Proje kuralı: bileşen ekleme, referans bağlama ve
    /// layer açma kodda yapılıyor; kullanıcı yalnız düğmeye basıyor.
    /// KURULUM TEK YERDE, İKİ TETİKLEYİCİ.
    ///
    /// `SnowAutoWire` bunu eksik referans gördüğünde kendiliğinden çağırıyor;
    /// düğme de yerinde duruyor. Ayrı bir sınıfa çıkarmak denendi ve on üç
    /// sabit, altı yardımcı metot peşinden sürüklendi — kazancı yoktu.
    public static void SetupScene()
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

        var surface = go.GetComponent<SnowSurface>();
        if (surface == null) surface = go.AddComponent<SnowSurface>();

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

        var curtains = go.GetComponent<SnowCurtainController>();
        if (curtains == null) curtains = go.AddComponent<SnowCurtainController>();

        // VFX KATMANLARI: bileşen sahnede duruyor ama VFX referansları BOŞ.
        // Boşken hiçbir şey yapmıyorlar — mevcut compute yolu çalışmaya devam
        // ediyor. İkisi birden bağlanırsa kar iki katına çıkar (`DECISIONS.md`).
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

        // F1 menüsündeki Kar bölümü de buradan bağlanıyor; elle atama yok.
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

        // ÖRTÜ AYARLARI. Bağlanmazsa global'ler 0 kalıyor, maske sıfır çıkıyor
        // ve dağda hiç kar görünmüyor — "kod koşmuyor" ile aynı belirti.
        var coverageSerialized = new SerializedObject(coverage);
        coverageSerialized.FindProperty("settings").objectReferenceValue = settings;
        coverageSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(coverage);

        var fallLayersSerialized = new SerializedObject(fallLayers);
        fallLayersSerialized.FindProperty("environment").objectReferenceValue = bridge;
        fallLayersSerialized.FindProperty("farLayer").objectReferenceValue = curtains;
        fallLayersSerialized.ApplyModifiedProperties();

        var driftVfxSerialized = new SerializedObject(driftVfx);
        driftVfxSerialized.FindProperty("environment").objectReferenceValue = bridge;
        driftVfxSerialized.FindProperty("settings").objectReferenceValue = settings;
        driftVfxSerialized.ApplyModifiedProperties();

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
        managerSerialized.FindProperty("captureShader").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Shader>(CaptureShaderPath);
        managerSerialized.FindProperty("skyShader").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
        managerSerialized.ApplyModifiedProperties();

        Material snowLit = LoadOrCreateSnowMaterial();

        var surfaceSerialized = new SerializedObject(surface);
        surfaceSerialized.FindProperty("settings").objectReferenceValue = settings;
        surfaceSerialized.FindProperty("manager").objectReferenceValue = manager;
        surfaceSerialized.FindProperty("snowMaterial").objectReferenceValue = snowLit;
        surfaceSerialized.ApplyModifiedProperties();

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
        Material curtainMat = LoadOrCreateCurtainMaterial();

        var curtainSerialized = new SerializedObject(curtains);
        curtainSerialized.FindProperty("settings").objectReferenceValue = settings;
        curtainSerialized.FindProperty("snowfallCompute").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<ComputeShader>(SnowfallComputePath);
        curtainSerialized.FindProperty("curtainMaterial").objectReferenceValue = curtainMat;
        curtainSerialized.FindProperty("followTarget").objectReferenceValue =
            Camera.main != null ? Camera.main.transform
                                : (player != null ? player.transform : null);
        curtainSerialized.FindProperty("environmentSource").objectReferenceValue = bridge;
        curtainSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(curtains);

        managerSerialized.FindProperty("persistence").objectReferenceValue = persistence;
        managerSerialized.FindProperty("curtains").objectReferenceValue = curtains;
        managerSerialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(persistence);

        EditorUtility.SetDirty(burst);
        EditorUtility.SetDirty(sampler);

        EditorUtility.SetDirty(snowfall);
        EditorUtility.SetDirty(surface);
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
        material.SetTexture(SnowShaderIDs.SastrugiNoise, SnowTextureBaker.EnsureSastrugiNoise());
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

    static Material LoadOrCreateCurtainMaterial()
    {
        Texture2D noise = SnowTextureBaker.EnsureCurtainNoise();

        var material = AssetDatabase.LoadAssetAtPath<Material>(CurtainMaterialPath);

        if (material == null)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(CurtainShaderPath);
            if (shader == null) return null;

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, CurtainMaterialPath);
        }

        material.SetTexture(SnowShaderIDs.CurtainNoise, noise);
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
