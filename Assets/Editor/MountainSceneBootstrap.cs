using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Sahnenin tek kaynağı. Unity her derleme sonrası çalışır, sahneyi buradaki
/// ayarlara getirir. Ayarlar değişmediyse hiçbir şey yapmaz.
[InitializeOnLoad]
public static class MountainSceneBootstrap
{
    // --- Oyuncu ---
    const float PlayerHeight = 1.8f;
    const float EyeHeight = 1.65f;
    const float FarClipFactor = 3f;   // kamera menzili = harita kenarı × bu

    /// Arazi LOD'unun siluette izin verdiği hata (piksel). Unity varsayılanı 5 ve o
    /// değer yalnızca sahnede yaşıyordu. Gölgeyi düşüren mesh de bu LOD'dan çiziliyor:
    /// kaba siluetin ışık yönünden izdüşümü, gölge kenarına üçgen dişler olarak
    /// vuruyordu — yaklaşınca beliriyordu çünkü gölge mesafesi 150 metre.
    const float TerrainPixelError = 2f;

    /// Basemap fiilen kapalı: Unity bu mesafenin ötesindeki araziyi malzemeyle değil,
    /// malzemenin bir kez pişirilmiş 1024'lük fotoğrafıyla çizer. O fotoğraf canlı
    /// değerlere kör (sürgüler uzakta "çalışmıyor" görünüyordu) ve 17 metrelik
    /// texellerinde keskin maske sınırları bilinear büyütmeyle baklava kenarlarına
    /// dönüyor. Uzak arazi de gerçek malzemeyle çizilir.
    const float TerrainBasemapDistance = 25000f;

    /// Kurulumun çalıştığı tek sahne: oyunun kendisi. Test sahnesi (`TestGround`)
    /// ayrı ve buraya dokunulmuyor — mekanikler orada denenir, oyun burada kurulur.
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
    const string PrecipitationShaderPath = "Assets/Shaders/Precipitation.shader";
    const string SkyShaderPath = "Assets/Shaders/Sky.shader";
    const string SkyMaterialPath = "Assets/Settings/Sky.mat";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string CloudMaterialPath = "Assets/Settings/VolumetricClouds.mat";

    static MountainSceneBootstrap()
    {
        EditorApplication.delayCall += Run;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    /// Derleme Play mode'dayken olursa Run kendini iptal eder ve bir daha tetiklenmez.
    /// Edit moduna dönüşte tekrar çalışsın diye buradan da çağrılır.
    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += Run;
    }

    /// Yükseklik haritasını ayarlardan SIFIRDAN kurar. Kurulum normalde yalnız ayar
    /// imzası değişince üretiyor; sahnede araziye elle dokunulduğunda (fırça, yükseltme)
    /// imza aynı kaldığı için o değişiklik kalıcı oluyordu.
    ///
    /// Üretim deterministik — bütün rastgelelik `settings.seed`'den türüyor — yani sonuç
    /// eskisinin birebir aynısı, eksik olan yalnız elle yapılan değişiklik.
    ///
    /// Yüzey haritaları araziden türüyor: bundan sonra "Yüzey Haritaları" penceresinden
    /// yeniden pişirilmezse eski arazinin gölgesi ve karı hayalet olarak kalır.
    [MenuItem("To The Summit/Arazi/Araziyi Yeniden Üret", false, 20)]
    static void RegenerateTerrain()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null)
            throw new System.InvalidOperationException(
                "Sahnede MountainGenerator yok; önce kurulum çalışmalı.");

        EditorUtility.DisplayProgressBar("Dağ", "Yükseklik haritası üretiliyor...", 0.5f);
        try { gen.Generate(); }
        finally { EditorUtility.ClearProgressBar(); }

        EditorUtility.SetDirty(gen);
        EditorUtility.SetDirty(gen.GetComponent<Terrain>().terrainData);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    /// Run sırasında yüklenen ayarlar; yardımcı metotlar ölçüyü buradan okur
    static MountainSettings current;

    static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;

        // YALNIZ ANA SAHNE. Kurulum aktif sahneye çalışıyor; test sahnesi açıkken
        // oraya dağ, hava ve kar sistemi kurup düz alanı boğardı.
        if (scene.path != MainScenePath) return;

        bool changed = false;

        // SÜRE ÖLÇÜMÜ. Kurulum her derlemeden sonra çalışıyor ve beklenen sürenin ne
        // kadarı Unity'nin derlemesi, ne kadarı bizim işimiz belli değildi. Aşama
        // aşama ölçülüyor; toplam eşiğin altındaysa hiç basılmıyor ki konsol kirlenmesin.
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
        Phase("ölü script taraması");

        EnsureCloudFeature();
        EnsureCloudVolume();
        Phase("bulut geçişi");

        var settings = current = LoadOrCreateSettings();

        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null)
        {
            gen = CreateMountain(settings);
            changed = true;
        }

        gen.Bind(settings);

        Phase("dağ bileşeni");

        // ROTA DA İMZAYA GİRİYOR. Tesviye araziyi üretimden hemen sonra kesiyor ve
        // üstüne ikinci kez uygulanamıyor (kesilmiş yamacı yeniden kesmek olurdu).
        // Rota değişince arazi baştan üretilip yeniden şekillendiriliyor.
        var route = AssetDatabase.LoadAssetAtPath<MountainRoute>(RoutePath);
        string signature = settings.BuildSignature()
                         + "|s" + RouteTerrainShaper.Version
                         + "|" + RouteSignature(route);
        bool regenerated = gen.lastBuildSignature != signature;

        if (regenerated)
        {
            EditorUtility.DisplayProgressBar("Dağ", "Yükseklik haritası üretiliyor...", 0.5f);
            try
            {
                gen.Generate();

                // Tesviye ÜRETİMDEN HEMEN SONRA, yüzey haritaları pişmeden önce:
                // haritalar araziden türüyor ve sonradan kesilirse eğim, gölge ve kar
                // hesapları eski araziye ait kalır.
                EditorUtility.DisplayProgressBar("Dağ", "Rota araziye işleniyor...", 0.8f);
                RouteTerrainShaper.Shape(gen.GetComponent<Terrain>(), route);
            }
            finally { EditorUtility.ClearProgressBar(); }

            gen.lastBuildSignature = signature;
            EditorUtility.SetDirty(gen);
            EditorUtility.SetDirty(gen.GetComponent<Terrain>().terrainData);
            AssetDatabase.SaveAssets();
            changed = true;
        }

        if (!regenerated) gen.Measure();
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

        // TEST NESNELERİ ANA SAHNEDE DURMUYOR. Karakter, bitki örtüsü ve mekanik
        // denemeleri `TestGround` sahnesinde; ana sahne dağın kendisi.
        if (RemoveStaleTestObjects()) changed = true;

        var snap = player.GetComponent<GroundSnap>();
        if (snap == null)
        {
            snap = player.gameObject.AddComponent<GroundSnap>();
            changed = true;
        }

        snap.Bind(gen.GetComponent<Terrain>());
        EditorUtility.SetDirty(snap);

        var camera = player.GetComponentInChildren<Camera>();
        float farClip = current.terrainSize * FarClipFactor;
        if (!Mathf.Approximately(camera.farClipPlane, farClip))
        {
            camera.farClipPlane = farClip;
            EditorUtility.SetDirty(camera);
            changed = true;
        }

        // TAA: bulut yürüyüşü ekranın 1/9'unda yapılıyor ve blok deseni tül gibi
        // okunuyor. Kaydın (DECISIONS.md → "Yürüyüş çözünürlüğü 1/9'da kaldı")
        // tetikleyicisi tam olarak buydu: deseni eritecek zamansal katman gelirse
        // açılacaktı. URP'nin kendi TAA'sı hazır geliyor, ayrı bir şey yazmaya gerek yok.
        var cameraData = camera.GetUniversalAdditionalCameraData();
        if (cameraData.antialiasing != AntialiasingMode.TemporalAntiAliasing)
        {
            cameraData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            cameraData.taaSettings.quality = TemporalAAQuality.High;
            EditorUtility.SetDirty(cameraData);
            changed = true;
        }

        var terrainComponent = gen.GetComponent<Terrain>();
        if (!Mathf.Approximately(terrainComponent.heightmapPixelError, TerrainPixelError)
            || !Mathf.Approximately(terrainComponent.basemapDistance, TerrainBasemapDistance)
            || terrainComponent.shadowCastingMode != ShadowCastingMode.Off)
        {
            terrainComponent.heightmapPixelError = TerrainPixelError;
            terrainComponent.basemapDistance = TerrainBasemapDistance;

            // ARAZİ GÖLGE HARİTASINA YAZMIYOR. Kendi gölgesini yükseklik alanından
            // yürüyerek hesaplıyor (bkz. `TerrainSunShadow`) ve o hesap kilometrelerce
            // uzağı taşıyor; harita altmış metrede bitiyor. İkisi birden açıkken arazi
            // kendi kendini gölgeliyordu ve ovada çizgi çizgi gölge akneleri çıkıyordu.
            //
            // Haritada yalnız HAREKETLİ nesneler kalıyor: bisiklet, oyuncu, ileride
            // çadır ve ekipman. Onların gölgesi araziye böyle düşüyor.
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

        // Ayarlar sürücüden önce yüklenir: bulut tavanı orada tanımlı ve yağışın nerede
        // kesileceğini o belirliyor. İki yerde ayrı tanımlanırsa "bulutların üstündeyim
        // ama tepemden kar yağıyor" durumu geri gelir.
        var atmosphereSettings = LoadOrCreateAtmosphereSettings();

        var atmosphere = Object.FindAnyObjectByType<AtmosphereController>();
        if (atmosphere == null)
        {
            atmosphere = weatherState.gameObject.AddComponent<AtmosphereController>();
            changed = true;
        }

        // Rüzgâr her çalışmada yeniden bağlanır: ayar asset'i sonradan eklendiğinde
        // sahnedeki bileşende alan boş kalıyor ve hata ancak Play'e basınca çıkıyordu.
        var windField = Object.FindAnyObjectByType<WindField>();
        windField.Bind(LoadOrCreate<WindSettings>(WindPath));
        EditorUtility.SetDirty(windField);

        // Kuşak sınırları dağın zemini ve zirvesinden türer; dağ değişince kayar
        // Sıcaklık dördüncü kaynak: donma seviyesi ondan türüyor, sürücü kendi
        // modelini kurmuyor.
        var thermometer = weatherState.GetComponent<TemperatureField>();
        if (thermometer == null)
        {
            thermometer = weatherState.gameObject.AddComponent<TemperatureField>();
            changed = true;
        }

        thermometer.Bind(weatherState, windField, Object.FindAnyObjectByType<TimeOfDay>());
        EditorUtility.SetDirty(thermometer);

        var driver = Object.FindAnyObjectByType<AltitudeWeatherDriver>();
        driver.Bind(weatherState, windField, player.transform,
            Object.FindAnyObjectByType<TimeOfDay>(), thermometer,
            LoadOrCreate<WeatherDriverSettings>(WeatherDriverPath),
            gen.Settings.baseHeight * gen.Settings.terrainHeight, gen.peakAltitude);
        EditorUtility.SetDirty(driver);

        // Arazi maruziyeti: rüzgâr sırtta hızlanır, oyukta kesilir. Rüzgâr araziyi
        // bilmez, arazi rüzgârı bilir — bu yüzden ayrı bir bileşen ölçüp itiyor.
        var shelter = windField.GetComponent<TerrainWindShelter>();
        if (shelter == null)
        {
            shelter = windField.gameObject.AddComponent<TerrainWindShelter>();
            changed = true;
        }

        shelter.Bind(player.transform, SurfaceComponent(gen, ref changed), windField);
        EditorUtility.SetDirty(shelter);

        // Yağış her çalışmada yeniden bağlanır: bulut kaynağı burada kuruluyor ve
        // yağışın nereden düştüğünü o belirliyor.
        var precipitationRenderer = Object.FindAnyObjectByType<PrecipitationRenderer>();
        var precipitationShader = AssetDatabase.LoadAssetAtPath<Shader>(PrecipitationShaderPath);
        if (precipitationShader == null)
            throw new System.InvalidOperationException($"Shader bulunamadı: {PrecipitationShaderPath}");
        precipitationRenderer.Bind(weatherState, windField, precipitationShader, atmosphere);
        EditorUtility.SetDirty(precipitationRenderer);

        // Eski kurulumdan kalan çizim bileşenleri: yağış artık doğrudan çiziliyor
        // (iki alt parça, kapalı olan hiç gönderilmiyor). Kalırlarsa mesh'in tamamını
        // ikinci kez çizerler.
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

        // Eski, eksik bileşenli Debug objesi varsa baştan kurulur
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


        // Bakış hareketten ayrı: serbest uçuşa geçince yürüyüş kapanıyor,
        // bakış oradaysa fare de ölüyordu
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

        // Kapalı başlamalı: açıkken CharacterController'ı devre dışı bırakıyor,
        // oyuncu çarpışmasız kalıp zeminden düşüyor
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

        // Debug menüsünde olduğu gibi her çalışmada yeniden bağlanır
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
            // Önceki çalışmada bağlanamadan eklenmiş olabilir
            lookController.Bind(LoadOrCreateLookSettings(), weatherState,
                Object.FindAnyObjectByType<TimeOfDay>());
            changed = true;
        }

        Phase("sistemler");

        EnsureTerrainSurface(gen, regenerated, ref changed);
        Phase("yüzey haritaları");

        EnsureSnowSurface(gen, player, ref changed);
        EnsureRouteOverlay(gen, ref changed);
        EnsureClimbHud(player, gen, ref changed);
        EnsureDebugMenu(player, ref changed);
        Phase("kar, rota, göstergeler");

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
        }

        Phase("sahne kaydı");

        // Eşik 200 ms: altındaki kurulumlar zaten fark edilmiyor.
        if (clock.ElapsedMilliseconds >= 200)
            ToolLog.Write($"[Kurulum] toplam {clock.ElapsedMilliseconds} ms{timings}");
    }

    static readonly string[] BandNames =
    {
        "0-25%   etek",
        "25-50%  alt yamaç",
        "50-75%  üst yamaç",
        "75-100% zirve"
    };

    /// Kuşak başına hedef dağılım: yürünebilir, zorlu, tırmanma, duvar.
    /// Aşağıda yürüyüş baskın, yukarı çıktıkça tırmanma ve geçilemez duvar artar.
    static readonly float[,] BandTargets =
    {
        { 75f, 20f,  5f,  0f },
        { 55f, 30f, 14f,  1f },
        { 35f, 35f, 27f,  3f },
        { 15f, 30f, 45f, 10f }
    };

    /// Üretilen dağın ölçüm sonucunu ve kullanılan parametreleri dosyaya yazar.
    /// Console'a bakmaya gerek kalmasın diye.
    static void WriteMountainReport(MountainGenerator gen)
    {
        var report = new System.Text.StringBuilder();

        report.AppendLine($"# Dağ {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        var s = gen.Settings;
        report.AppendLine($"{s.terrainSize} m taban, {s.terrainHeight} m zirve, " +
                          $"{s.heightmapResolution} çözünürlük " +
                          $"({s.terrainSize / (s.heightmapResolution - 1f):F2} m/örnek)");
        report.AppendLine($"Ortalama eğim {gen.meanSlopeDegrees:F1}°   " +
                          $"gerçek zirve {gen.peakAltitude:F0} m " +
                          $"(tavanın %{gen.peakAltitude / s.terrainHeight * 100f:F0}'i)");
        report.AppendLine($"Zemin {s.baseHeight * s.terrainHeight:F0} m");
        report.AppendLine();
        report.AppendLine("## Yükseklik kuşağı başına eğim dağılımı (sadece dağ)");
        report.AppendLine("Kuşak                 Yürü   Zorlu  Tırman  Duvar   Ort");
        report.AppendLine("                     0-30°  30-45°  45-70°   70°+");

        for (int i = 0; i < MountainGenerator.AltitudeBandCount; i++)
        {
            var band = gen.bands[i];
            report.AppendLine(
                $"{BandNames[i],-20} {band.walkable,5:F1}  {band.strenuous,6:F1}  " +
                $"{band.climbable,6:F1}  {band.wall,5:F1}  {band.meanDegrees,5:F1}°");
            report.AppendLine(
                $"{"hedef",-20} {BandTargets[i, 0],5:F0}  {BandTargets[i, 1],6:F0}  " +
                $"{BandTargets[i, 2],6:F0}  {BandTargets[i, 3],5:F0}");
        }

        report.AppendLine();
        report.AppendLine("## Parametreler");
        report.AppendLine($"seed {s.seed}   mountainRadius {s.mountainRadius}   baseHeight {s.baseHeight}");
        report.AppendLine($"radialDistortion {s.radialDistortion}   radialFrequency {s.radialFrequency}");
        report.AppendLine($"ikincil zirve {s.secondaryPeaks}   yayılım {s.peakSpread}   " +
                          $"yükseklik {s.peakHeightRange}   yarıçap {s.peakRadiusRange}");
        report.AppendLine($"oktav {gen.EffectiveOctaves} (çözünürlükten)   " +
                          $"baseFrequency {s.baseFrequency}   " +
                          $"lacunarity {s.lacunarity}   gain {s.gain}");
        report.AppendLine($"ridgeInfluence {s.ridgeInfluence}   ridgeFootDamping {s.ridgeFootDamping}   " +
                          $"ridgeSharpness {s.ridgeSharpness}");
        report.AppendLine($"warp {s.warpStrength} @ {s.warpFrequency}   " +
                          $"detay {s.warpDetailStrength} @ {s.warpDetailFrequency}");
        report.AppendLine($"teras kaba {s.coarseTerraceStrength}/{s.coarseTerraceBands}   " +
                          $"ince {s.fineTerraceStrength}/{s.fineTerraceBands}");
        report.AppendLine($"terraceSharpness {s.terraceSharpness}   " +
                          $"kot kayması {s.terraceOffsetAmount} @ {s.terraceOffsetFrequency}   " +
                          $"güç değişimi {s.terraceVariation} @ {s.terraceVariationFrequency}");
        report.AppendLine($"erozyon {s.erosionIterations} iterasyon   talus {s.talusAngle}°   " +
                          $"oran {s.erosionRate}");
        report.AppendLine($"zirve platosu {s.summitPlateauStart} düzlük {s.summitFlatness}");

        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/mountain.log", report.ToString(), System.Text.Encoding.UTF8);
    }

    /// Gökyüzü materyali. Bulut parametreleri her karede AtmosphereController tarafından yazılır.
    static Material LoadOrCreateSkyMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        if (material != null)
        {
            // (bulut dokusu ataması silindi)
            return material;
        }

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
        if (shader == null)
            throw new System.InvalidOperationException($"Shader bulunamadı: {SkyShaderPath}");

        material = new Material(shader) { name = "Sky" };
        AssetDatabase.CreateAsset(material, SkyMaterialPath);
        AssetDatabase.SaveAssets();

        // (bulut dokusu ataması silindi)
        return material;
    }

    /// BULUT RENDER GEÇİŞİ. `VolumetricCloudsURP` (jiaozi158, MIT — HDRP'nin URP portu)
    /// URP renderer'ına alt nesne olarak ekleniyor. Feature'ın kendi materyali var; shader
    /// gizli olduğu için materyal asset olarak üretilip bağlanıyor.
    static void EnsureCloudFeature()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException($"Renderer bulunamadı: {RendererPath}");

        var shader = Shader.Find("Hidden/Sky/VolumetricClouds");
        if (shader == null)
            throw new System.InvalidOperationException("Bulut shader'ı bulunamadı: Hidden/Sky/VolumetricClouds");

        var material = AssetDatabase.LoadAssetAtPath<Material>(CloudMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "VolumetricClouds" };
            AssetDatabase.CreateAsset(material, CloudMaterialPath);
        }
        BindCloudTextures(material);

        foreach (var existing in renderer.rendererFeatures)
            if (existing is VolumetricCloudsURP) return;

        var feature = ScriptableObject.CreateInstance<VolumetricCloudsURP>();
        feature.name = "Volumetric Clouds";

        var serialized = new SerializedObject(feature);
        serialized.FindProperty("material").objectReferenceValue = material;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // `Create()` ÖRNEK ÜRETİLİRKEN Unity tarafından bir kez çağrılıyor ve materyal o
        // an henüz atanmamış oluyor; feature "Material is empty" deyip erken dönüyor.
        // Materyal bağlandıktan sonra elle tekrar çağrılıyor ki geçişler kurulsun.
        feature.Create();

        renderer.rendererFeatures.Add(feature);
        AssetDatabase.AddObjectToAsset(feature, renderer);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererPath);
    }

    /// Gürültü dokuları materyalde duruyor, hiçbir kod atamıyor — repo hazır materyalle geliyordu.
    /// Eşleşme shader'daki örneklemeden: `_Worley128RGBA` düşük frekans şekil, `_ErosionNoise` detay.
    static void BindCloudTextures(Material material)
    {
        material.SetTexture("_Worley128RGBA", LoadCloudTexture("WorleyNoise128RGBA"));
        material.SetTexture("_ErosionNoise", LoadCloudTexture("WorleyNoise32RGB"));
        material.SetTexture("_CloudLutTexture", LoadCloudTexture("CloudLutRainAO"));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }

    static Texture LoadCloudTexture(string fileName)
    {
        var path = $"Assets/VolumetricClouds/Textures/{fileName}.png";
        var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
        if (texture == null)
            throw new System.InvalidOperationException($"Bulut dokusu bulunamadı: {path}");
        return texture;
    }

    /// BULUT VOLUME BİLEŞENİ. Ayarlar `VolumetricClouds` (VolumeComponent) üzerinden geliyor ve
    /// varsayılanı KAPALI; sahnedeki profile ekleyip açıyoruz.
    ///
    /// v1 KURALI: buraya bizim hiçbir ayarımız yazılmıyor (bkz. `CLOUDS_REBUILD.md`).
    /// Repo'nun varsayılanları neyse o çalışıyor.
    static void EnsureCloudVolume()
    {
        var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        if (volume == null)
            throw new System.InvalidOperationException("Sahnede Volume yok, bulut hacmi eklenemedi.");
        if (volume.sharedProfile == null)
            throw new System.InvalidOperationException($"{volume.name} Volume'unda profil yok.");

        var profile = volume.sharedProfile;
        if (profile.TryGet(out VolumetricClouds existing))
        {
            if (!existing.state.value)
            {
                existing.state.value = true;
                EditorUtility.SetDirty(profile);
            }
            return;
        }

        var clouds = profile.Add<VolumetricClouds>(overrides: true);
        clouds.state.value = true;
        clouds.name = nameof(VolumetricClouds);
        AssetDatabase.AddObjectToAsset(clouds, profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile));
    }

    // (AssignCloudNoise SİLİNDİ — bulut dokuları yeniden yazılıyor.)

    static readonly int BaseNoiseId = Shader.PropertyToID("_BaseNoise");
    static readonly int DetailNoiseId = Shader.PropertyToID("_DetailNoise");

    /// Ayarlar asset'i tek kaynak: hem bootstrap hem tuner penceresi buna bakar
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

    /// Hava: durum, rüzgâr, yüksekliğe bağlı sürücü ve yağış çizimi
    static void CreateWeather()
    {
        var go = new GameObject("Weather");

        go.AddComponent<WeatherState>();
        go.AddComponent<WindField>();

        // Bağlama Run'da yapılır: kuşak sınırları ve bulut tavanı orada hesaplanıyor,
        // burada ikinci bir kopyasını tutmak ikisinin ayrışmasına açık kapı bırakır.
        go.AddComponent<AltitudeWeatherDriver>();

        var precipitation = new GameObject("Precipitation");
        precipitation.transform.SetParent(go.transform, false);
        precipitation.AddComponent<PrecipitationRenderer>();
    }

    /// Ses: katman harmanı ve gök gürültüsü. WeatherState/WindField ile aynı ağaçta durur.
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

        // Bağlama burada değil, EnsureStorm'da: orası her çalışmada girdiği için hem yeni
        // hem eski sahneleri kapsıyor. İki yerde bağlamak ikinci bir doğru yaratırdı.
    }

    /// Gök gürültüsü ve şimşek. Her çalışmada yeniden bağlanır — bir ayar asset'i ya da
    /// bağımlılık sonradan eklendiğinde sahnedeki bileşende o alan boş kalıyor ve hata
    /// ancak Play'e basınca çıkıyordu.
    static void EnsureStorm(WeatherState state, Transform observer,
        AtmosphereController atmosphere, Terrain terrain, ref bool changed)
    {
        var thunder = Object.FindAnyObjectByType<ThunderPlayer>();
        if (thunder == null)
            throw new System.InvalidOperationException("Sahnede ThunderPlayer yok.");

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

        flash.Bind(thunder, atmosphere, observer, tuning);
        EditorUtility.SetDirty(flash);

        // Kol ışıkla aynı nesnede durabilir: ikisi de aynı çakmayı çiziyor ve kol
        // konumu ışıktan okuyor. Ayrı nesne, ayrı yerde duruyormuş izlenimi verirdi.
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
            throw new System.InvalidOperationException($"Shader bulunamadı: {BoltShaderPath}");

        material = new Material(shader) { name = "LightningBolt" };
        AssetDatabase.CreateAsset(material, BoltMaterialPath);
        AssetDatabase.SaveAssets();

        return material;
    }

    static AudioClip LoadClip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
            throw new System.InvalidOperationException($"Ses bulunamadı: {path}");

        return clip;
    }

    /// Klasördeki verilen önekle başlayan tüm klipleri ada göre sıralı döndürür
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
            throw new System.InvalidOperationException($"Ses bulunamadı: {folder}/{prefix}*");

        paths.Sort(System.StringComparer.Ordinal);

        var clips = new AudioClip[paths.Count];
        for (int i = 0; i < paths.Count; i++)
            clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);

        return clips;
    }

    /// Gün döngüsü. Sahnedeki yönlü ışığı sürer, havayı tanımaz.
    static void CreateTimeOfDay()
    {
        var sun = Object.FindAnyObjectByType<Light>();
        if (sun == null || sun.type != LightType.Directional)
            throw new System.InvalidOperationException("Sahnede yönlü ışık bulunamadı.");

        var go = new GameObject("Time Of Day");
        go.AddComponent<TimeOfDay>().Bind(sun);
    }

    /// Renk düzenlemesi. Mevcut Global Volume objesine bağlanır.
    static void CreateLookController(WeatherState weatherState)
    {
        var volume = Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        if (volume == null)
            throw new System.InvalidOperationException("Sahnede Volume bulunamadı.");

        volume.gameObject.AddComponent<LookController>().Bind(
            LoadOrCreateLookSettings(),
            weatherState,
            Object.FindAnyObjectByType<TimeOfDay>());
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

    /// Esc paneli. Sistemleri tanıyan tek yer burası; sistemlerin kendisi paneli bilmez.
    ///
    /// Her çalışmada yeniden bağlanır: panele yeni bir bağımlılık eklendiğinde sahnedeki
    /// bileşende o alan boş kalıyor ve panel çizilirken patlıyordu.
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
            player.GetComponentInChildren<SnowCollisionProbe>(true),
            Object.FindAnyObjectByType<RouteOverlay>(FindObjectsInactive.Include));

        EditorUtility.SetDirty(menu);
    }

    /// Script'i silinmiş bileşenler sahnede boş kabuk olarak kalır: Unity uyarı basar,
    /// sahne dosyası ölü blok taşır. WeatherFog kaldırılıp yerini AtmosphereController
    /// alınca tam bunu yaşadık.
    static bool RemoveMissingScripts(Scene scene)
    {
        int removed = 0;

        foreach (var root in scene.GetRootGameObjects())
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);

        if (removed > 0)
            ToolLog.Write($"Sahneden {removed} adet kayıp script kaldırıldı.");

        return removed > 0;
    }

    /// Geçici geliştirme araçları: ölçüm, eşik denetimi, gösterge, kare hızı limiti
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

    /// Dağ yüzeyi materyali. Yüzey haritaları dağın biçiminden çıkarılır; dağ
    /// değiştiğinde onlar da değişmeli — yalnızca "harita yok" koşuluna bağlıyken
    /// yeniden üretilen dağın üstünde eski dağın çakıl ve maruziyet haritası kalıyordu.
    /// Rotanın içeriğinden türeyen imza. Nokta konumları ve yarıçapları değişince
    /// arazinin yeniden şekillenmesi gerekiyor; dosyanın değiştiği tarihe bakmak
    /// yetmiyor, fırça her darbede dosyayı yazıyor ama şekli hep değiştirmiyor.
    static string RouteSignature(MountainRoute route)
    {
        if (route == null) return "-";

        var hash = new System.Text.StringBuilder();
        hash.Append(route.road.Count);

        foreach (MountainRoute.Branch branch in route.branches)
            hash.Append('|').Append(branch.marks.Count);

        hash.Append('|').Append(route.camps.Count);

        // Sayılar aynı kalıp konumlar değişebiliyor: birkaç örnek noktanın kendisi de
        // imzaya giriyor. Hepsini almak binlerce noktada dizgiyi şişiriyor.
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

    /// Yüzey bileşenini bulur, yoksa ekler. Rüzgâr sığınağı da aynı bileşene bağlanıyor
    /// ve kurulum sırasında ondan ÖNCE geliyor: referans o anda yoksa sığınak sessizce
    /// boş kalır ve arazi rüzgârı hiç etkilemez. Veriyi `EnsureTerrainSurface` bağlıyor,
    /// burası yalnız bileşenin var olduğunu garanti ediyor.
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

        // Birikim ağırlığı hâkim rüzgâr yönüne göre pişiyor; açı asset'in ADINDA
        // taşınıyor ki yön değişince harita bayat sayılsın ve yeniden pişsin.
        float prevailing = LoadOrCreate<WindSettings>(WindPath).prevailingDegrees;

        var maps = SurfaceMapBaker.Load();
        if (regenerated || !SurfaceMapBaker.MapsCurrent(prevailing))
        {
            EditorUtility.DisplayProgressBar("Yüzey", "Haritalar çıkarılıyor...", 0.5f);
            try { maps = SurfaceMapBaker.Bake(terrain, prevailing); }
            finally { EditorUtility.ClearProgressBar(); }

            changed = true;
        }

        var surface = SurfaceComponent(gen, ref changed);
        surface.Bind(
            LoadOrCreateTerrainMaterialSettings(),
            Object.FindAnyObjectByType<WeatherState>(),
            Object.FindAnyObjectByType<WindField>(),
            Object.FindAnyObjectByType<TimeOfDay>(),
            Object.FindAnyObjectByType<AltitudeWeatherDriver>(),
            Object.FindAnyObjectByType<TemperatureField>(),
            maps,
            SurfaceMapBaker.LoadDrift(),
            SurfaceMapBaker.LoadNormals(),
            SurfaceMapBaker.LoadHorizon(),
            SurfaceMapBaker.LoadHeight(),
            LoadSurfaceSet("SnowPowder"),
            LoadSurfaceSet("SnowPacked"),
            AssetDatabase.LoadAssetAtPath<Shader>(SurfaceShaderPath));

        EditorUtility.SetDirty(surface);
    }

    /// Karın ÇARPIŞMA yüzeyi ve ayrışma probu. Kar geometrik olarak yükseliyor ama
    /// TerrainCollider bunu bilmiyor; oyuncunun ayağı ikinci bir zemin katmanına
    /// oturuyor. Her çalışmada yeniden bağlanır — kar ayarı ya da rüzgâr kaynağı
    /// sonradan değiştiğinde sahnedeki örnek eski referansta kalmasın.
    static void EnsureSnowSurface(MountainGenerator gen, FirstPersonController player,
        ref bool changed)
    {
        var terrain = gen.GetComponent<Terrain>();

        var snow = terrain.GetComponent<SnowSurface>();
        if (snow == null)
        {
            snow = terrain.gameObject.AddComponent<SnowSurface>();
            changed = true;
        }

        snow.Bind(terrain.GetComponent<TerrainSurface>(),
                  Object.FindAnyObjectByType<WindField>(),
                  LoadOrCreateTerrainMaterialSettings(),
                  terrain);
        EditorUtility.SetDirty(snow);

        var controller = new SerializedObject(player);
        controller.FindProperty("snow").objectReferenceValue = snow;
        if (controller.ApplyModifiedPropertiesWithoutUndo()) changed = true;

        // Prob KENDİ KAPALI NESNESİNDE. Oyuncunun üstüne eklenseydi `AddComponent`
        // `OnEnable`'ı anında çağırırdı ve bileşen daha bağlanmadan bağımlılık
        // hatası fırlatırdı. Kapalı bir nesneye eklenen bileşende OnEnable beklemede
        // kalıyor; ölçüm F1'den açılınca çalışıyor.
        var probe = player.GetComponentInChildren<SnowCollisionProbe>(true);
        if (probe == null)
        {
            var host = new GameObject("Snow Probe");
            host.SetActive(false);
            host.transform.SetParent(player.transform, false);
            probe = host.AddComponent<SnowCollisionProbe>();
            changed = true;
        }

        probe.Bind(snow, player.transform);
        EditorUtility.SetDirty(probe);
    }

    /// Rota çizgilerinin oyun görünümü katmanı. KAPALI kurulur: gösterge, sürekli
    /// çizilen bir katman değil. Kapalı nesneye eklendiği için `OnEnable` bağlanmadan
    /// çalışmıyor.
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

    /// Ayar asset'i yoksa koddaki varsayılanlarla oluşturur. Varsa dokunmaz: asset artık
    /// tek doğru, kod yalnızca ilk hâli veriyor.
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

    /// Tırmanış göstergesi yalnızca okur; her çalışmada yeniden bağlanır ki
    /// dağ veya hava sistemi yenilendiğinde eski referansla kalmasın.
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
            Object.FindAnyObjectByType<TemperatureField>());

        EditorUtility.SetDirty(climbHud);
    }

    /// Dağ eteğinin biraz dışı: düz arazide, ayaklar yüzeye değecek şekilde
    /// Ana sahnede kalmış test nesnelerini siler. Test karakteri ve bitki örtüsü
    /// buradan kaldırıldı; sahnede duran eski kopyalar kendiliğinden gitmiyor.
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

    /// Yüzey malzemesi seti. `TextureIngest` üretiyor, `StochasticTextureBaker`
    /// dönüştürüyor; burada yalnız yükleniyor. Yoksa null döner ve shader detay
    /// dalını hiç açmaz.
    static SurfaceMaterialSet LoadSurfaceSet(string prefix)
    {
        string path = $"{TextureIngest.Folder}/{prefix}.asset";
        var set = AssetDatabase.LoadAssetAtPath<SurfaceMaterialSet>(path);

        // Ham haritalar projede ama set asset'i yoksa kur: doku elle kopyalanmış
        // olabilir. `TextureIngest` klasörden alırken zaten kuruyor; bu yol o
        // adımdan geçmemiş dosyalar için.
        if (set == null && File.Exists($"{TextureIngest.Folder}/{prefix}_Normal.png"))
        {
            set = ScriptableObject.CreateInstance<SurfaceMaterialSet>();
            set.assetPrefix = prefix;
            AssetDatabase.CreateAsset(set, path);
            AssetDatabase.SaveAssets();
        }

        if (set == null) return null;

        StochasticTextureBaker.EnsureAll();
        return set;
    }

    /// Doğuş yeri ve bakış yönü. İŞARETLİYSE rota asset'inden (bkz. `RoutePainter`);
    /// değilse hesaplanmış nokta. Elle işaretlenmiş bir doğuş, hesabın bilemeyeceği
    /// şeyi biliyor: dağın hangi yüzünden bakıldığını.
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

    /// Verilen XZ'nin ÇARPIŞMA yüzeyindeki kotu. `SampleHeight` yükseklik haritasını
    /// okuyor, collider'ın gerçekte nerede olduğunu değil; ikisi köşegende ayrışıyor ve
    /// oyuncu zemine gömülüyor.
    ///
    /// Işın sahneye değil arazinin KENDİ collider'ına atılıyor: sahne geneline atılınca
    /// oyuncunun kendi kapsülüne çarpıyor ve her kurulum çalışması onu bir kapsül boyu
    /// yukarı taşıyordu.
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

        // Dağ eteğinin biraz dışı, ama harita kenarını asla aşmadan.
        // mountainRadius 0.45'i geçtiğinde 1.12 çarpanı doğuş noktasını haritanın
        // dışına atıyor ve altında zemin kalmıyordu.
        const float SafeEdge = 0.47f;

        float radius = current.mountainRadius;
        float distance = Mathf.Min(radius * 1.12f, SafeEdge) * current.terrainSize;

        var pos = new Vector3(distance, 0f, 0f);

        // Çarpışma yüzeyini ışınla bul: SampleHeight yükseklik haritasını okur,
        // collider'ın gerçekte nerede olduğunu değil. İkisi ayrışırsa oyuncu zemine gömülür.
        //
        // Işın sahneye değil, arazinin kendi collider'ına atılıyor. Sahne geneline atmak
        // oyuncunun kapsülüne çarpıyordu — oyuncu tam doğuş noktasında durduğu için her
        // kurulum çalışması onu kendi tepesine, bir kapsül boyu yukarı taşıyordu ve sahne
        // her derlemede oyuncu biraz daha yükselmiş olarak kaydediliyordu.
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
