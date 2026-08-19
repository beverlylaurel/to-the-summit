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
    /// LOD ne zaman seyreltmeye başlasın (ekran pikseli). 1'e indirmenin bedeli
    /// ölçüldü: FPS 170 -> 60. Seyreltmenin NE KADAR kabalaşacağını bu değil
    /// `TerrainMaxLod` sınırlıyor.
    const float TerrainPixelError = 2f;

    /// EN KABA LOD KADEMESI. Testere belirtisinin sebebi buydu ve analitik olarak
    /// olculdu: her kademe ornekleri atliyor, ara nokta dogrusal kuruluyor ve aradaki
    /// fark siluete basamak olarak cikiyor.
    ///
    ///   LOD 1  adim 14.6 m   ortanca hata  2.15 m   %95   8.7 m
    ///   LOD 2  adim 29.3 m   ortanca hata  6.44 m   %95  25.9 m
    ///   LOD 3  adim 58.6 m   ortanca hata 15.0 m    %95  60.1 m
    ///   LOD 4  adim 117 m    ortanca hata 31.9 m    %95  126 m
    ///
    /// Bu, belirtinin her yanini aciklıyor: farkli yamalar farkli kademede oldugu icin
    /// testerelerin boyu metrelerce degisiyor; en kaba kademede yama iki ucgene inince
    /// duz yuzlu piramit cikiyor; ve yukseklik haritasini bulaniklastirmak ise yaramiyor
    /// cunku hata veride degil, veriden ATLANAN orneklerde.
    ///
    /// 1'de en kaba adim 14.6 m, yani ortanca hata 2.15 m. Sifir tam detay demek ve
    /// 30 km'de odenemez.
    const int TerrainMaxLod = 1;

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
    const string FogComputePath = "Assets/Shaders/VolumetricFog.compute";
    const string FogSettingsPath = "Assets/Settings/VolumetricFogSettings.asset";
    const string SkyFogShaderPath = "Assets/Shaders/SkyFog.shader";
    const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    const string CloudMaterialPath = "Assets/Settings/VolumetricClouds.mat";
    const string CloudWeatherPath = "Assets/Settings/CloudWeatherSettings.asset";
    const string SkyWeatherPath = "Assets/Settings/SkyWeatherSettings.asset";
    const string MoonLightName = "Moon Light";

    /// `EnsureCloudVolume`'un bulut bileşenini yazdığı Volume. F1 paneli buradan bağlanıyor.
    static UnityEngine.Rendering.Volume cloudVolume;

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

    /// TAM ZİNCİR. Eskiden yalnız `gen.Generate()` çağırıyordu ve bu, arazinin
    /// içeriğini `MountainGenerator`'ın KENDİ prosedürel çıktısıyla dolduruyordu —
    /// eski radyal koni, terasları ve çok oktavlı gürültüsüyle. Yükseklik haritası
    /// uygulanmıyor, tesviye yapılmıyor, yüzey haritaları pişirilmiyordu.
    ///
    /// BU BİR GÜN YAKTI. Kullanıcı testere belirtisini kovalarken bu düğmeye onlarca
    /// kez bastı; her basış benim ürettiğim yükseklik haritasını eski jeneratörün
    /// çıktısıyla EZDİ. `Logs/tools.log`: yükseklik haritası araziye en son 13:44:53'te
    /// uygulanmış, aradaki dokuz saatte altı kez yeniden pişirildi ve hiçbiri ulaşmadı.
    /// Testere de zaten eski jeneratörün terasıydı.
    ///
    /// İmza da sıfırlanıyor: `GetAssetDependencyHash` PNG dışarıdan yazıldığında
    /// güncellenmeyebiliyor ve kurulum "değişmemiş" deyip üretimi atlıyor.
    /// Kurulumu dışarıdan koşturur. `Dağ Yapımı` penceresi kaydettikten sonra çağırıyor:
    /// pencere yüzey haritalarını bayat ilan ediyor ama tazeleyen bir şey yoktu ve
    /// kullanıcı doğru gölgelendirmeyi ancak Play'e girip çıkınca görüyordu.
    public static void Rebuild() => RegenerateTerrain();

    [MenuItem("To The Summit/Arazi/Araziyi Yeniden Üret", false, 20)]
    static void RegenerateTerrain()
    {
        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null)
            throw new System.InvalidOperationException(
                "Sahnede MountainGenerator yok; önce kurulum çalışmalı.");

        gen.lastBuildSignature = string.Empty;
        SurfaceMapBaker.Invalidate();

        EditorUtility.SetDirty(gen);
        Run();
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

        EnsureSkyFeature();
        EnsureCloudFeature();
        EnsureFogFeature();
        EnsureCurtainFeature();
        Phase("gökyüzü, bulut ve sis geçişleri");

        // SIRA ÖNEMLİ: `EnsureCloudVolume` sahnedeki Volume'u bulup `cloudVolume` statiğine
        // yazıyor, gökyüzü de onun profiline yazıyor. Ayrı arama yapılsaydı sahnedeki
        // birden fazla Volume arasından başkası seçilebilirdi.
        EnsureCloudVolume();
        EnsureSkyVolume();
        Phase("gökyüzü ve bulut hacimleri");

        var settings = current = LoadOrCreateSettings();

        var gen = Object.FindAnyObjectByType<MountainGenerator>();
        if (gen == null)
        {
            gen = CreateMountain(settings);
            changed = true;
        }

        gen.Bind(settings);

        Phase("dağ bileşeni");

        // ARAZİNİN KAYNAĞI `Dağ YapımI` PENCERESİ. Dağ elle yapılıyor ve pencere
        // yükseklik alanını doğrudan `TerrainData`'ya yazıyor. Kurulum araziyi ÜRETMİYOR,
        // yalnız ona bağlı olan her şeyi (yüzey haritaları, doğuş, bileşenler) tazeliyor.
        //
        // Düzenlenebilir asıl: `Assets/Terrain/Sculpts/*.bytes` (1025², float32).
        // Üretilmiş sonuç: `MountainTerrainData.asset`.
        //
        // `regenerated` yalnız AYAR imzasına bakıyor; yüzey haritalarının tazeliğini
        // `SurfaceMapBaker.MapsCurrent` ayrı karar veriyor ve pencere kaydederken
        // `Invalidate()` çağırdığı için harita orada bayat ilan ediliyor.
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

        // DITHERING AÇIK. Gökyüzü tüm görüş alanında 1 duraktan az değişiyor (ölçüldü,
        // durak konturu tek sınır veriyor). Bu kadar düz bir gradyanda 8 bit çıkışın
        // basamakları lekeli bantlara dönüyor; belirti "gökyüzünde devasa koyu bölge"
        // diye okunuyordu ve haftalarca gökyüzü hesabında arandı — orada değildi.
        //
        // URP varsayılanı KAPALI. Açıkken son geçişte mavi gürültü ekleniyor, basamak
        // sınırı eriyor. TAA ile birlikte çalışır, deseni ayrıca zamanda da dağıtır.
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

        // KUŞAKLAR ÖLÇÜLEN ARAZİDEN. Taban eskiden `baseHeight × terrainHeight` ile
        // ayardan geliyordu (186 m) ve elle yapılan dağın gerçek ovası 0 m olunca hava
        // kuşakları kayıyordu. `gen.Measure()` ikisini de araziden okuyor.
        gen.Measure();

        var driver = Object.FindAnyObjectByType<AltitudeWeatherDriver>();
        driver.Bind(weatherState, windField, player.transform,
            Object.FindAnyObjectByType<TimeOfDay>(), thermometer,
            LoadOrCreate<WeatherDriverSettings>(WeatherDriverPath),
            gen.groundAltitude, gen.peakAltitude);
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

        // Bulut katmanının tek kaynağı. Yağış ve şimşek kotlarını buradan okuyor,
        // ikisinden de ÖNCE kurulmak zorunda.
        EnsureCloudLayerProbe(player, ref changed);

        // Yağış her çalışmada yeniden bağlanır: bulut kaynağı burada kuruluyor ve
        // yağışın nereden düştüğünü o belirliyor.
        var precipitationRenderer = Object.FindAnyObjectByType<PrecipitationRenderer>();
        var precipitationShader = AssetDatabase.LoadAssetAtPath<Shader>(PrecipitationShaderPath);
        if (precipitationShader == null)
            throw new System.InvalidOperationException($"Shader bulunamadı: {PrecipitationShaderPath}");
        // SPEKTRAL PERDE DESENİ. Yoksa burada pişiyor — yalnız yüklemek yarışa açıktı:
        // fırıncının `InitializeOnLoadMethod`'u ile bootstrap'ın `delayCall`'ı sıralanmıyor
        // ve doku silinmişse bootstrap önce koşup patlıyordu.
        var curtainPattern = SpectralPrecipitationBaker.EnsureExists();
        if (curtainPattern == null)
            throw new System.InvalidOperationException(
                "Spektral yağış deseni üretilemedi: Assets/Settings/SpectralPrecipitation.asset");

        precipitationRenderer.Bind(weatherState, windField, precipitationShader, curtainPattern,
            Object.FindAnyObjectByType<CloudLayerProbe>(), player.transform);
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

        EnsureSunAndMoon(ref changed);
        EnsureSkyDrivers(ref changed);

    #if URP_PBSKY
        // GÜNEŞ ŞİDDETİ GÖKYÜZÜ PAKETİNİN KALİBRASYONUNDAN. Paket 100000 lux yer
        // aydınlığına göre kurulu ve gökyüzü parlaklığını ana ışıktan türetiyor; 1.5'te
        // gök sahneye göre sönük kalıyordu. Sayı paketin kendi önerisi, bizim seçimimiz
        // değil — gök ile sahnenin göreli parlaklığı buradan geliyor.
        var timeOfDay = Object.FindAnyObjectByType<TimeOfDay>();
        timeOfDay.SunIntensity = 3.030782f;

        // AY GÖKYÜZÜNÜ AYDINLATAN TEK KAYNAK. Paket geceleyin ayı güneş yerine koyup
        // atmosferi ondan hesaplıyor; ortam probe'u da o gökyüzünden pişiyor. Değer göz
        // kararı bulundu, gerçek ay parlaklığının karşılığı değil.
        // 0.204 → 0.0199. AY ON DÖRT DURAK FAZLA PARLAKTI ve gecenin gündüz gibi
        // okunmasının kökü buydu; üstüne yapılan pozlama/kontrast düzeltmeleri yamaydı.
        //
        // Gerçek oran: dolunay aydınlanması ≈ 0,25 lüks, güneş ≈ 133.000 lüks — arada
        // 19 durak var. Eski değerde güneş 3,0308'e karşı etkin ay 0,204 × renk ışıması
        // 0,384 = 0,078, yani 39:1 = 5,3 durak. Gece öğlenin beş durak altındaydı.
        //
        // Yeni değerde etkin 0,00765 → 396:1 = 8,6 durak. Fiziğin hâlâ 10,4 durak
        // üstünde ve bu BİLEREK: tam fiziksel gece için pozlamanın 19 durak açması
        // gerekirdi, `exposureCap` 2,5'te duruyor.
        //
        // SAYIYI BULUT BELİRLEDİ. Önce 0.0058'e çekilmişti (−4 durak hedefi, formülden);
        // arazi doğru göründü ama ay ışığındaki bulut eşiğin altında kalıp simsiyah
        // çıkıyordu. Sürgüyle ölçüldü: bulut karla birlikte ve orantılı parlıyor, yani
        // saçılım integrali sağlam, mesele eşikti. 0.0199 ikisinin de eşiğin üstünde
        // olduğu en düşük değer.
        //
        // TAVAN KIL PAYI BAĞLI. Pozlama uyumu `0.35 × 7,25 = 2,54` istiyor, `exposureCap`
        // 2,5'te kırpıyor. Yani buradan yapılan değişiklik ŞU AN ekrana birebir iniyor,
        // ama ay biraz daha yükseltilirse kırpma kalkar ve kısıntının %65'i geri gelir.
        timeOfDay.MoonIntensity = 0.0199f;

        // Ay albedosu. Doğan ay atmosferden geçerken sarıya kayıyordu; taban soğutuldu.
        // Hesap `TimeOfDay.moonColor` yorumunda.
        //
        // DOYGUNLUK DÜŞÜRÜLDÜ. Pozlama uyum payı yükselince gece bir durak açıldı ve o
        // soğuk taban olduğundan fazla göze çarptı. Ay ışığı fizikte güneş ışığının gri
        // regolitten yansıması, yani nötre yakın; gecenin mavi görünmesi gözün karanlıkta
        // maviye kaymasından (Purkinje) geliyor ve bu kadar doygun değil.
        //
        // TON DEĞİŞTİ, PARLAKLIK DEĞİŞMEDİ. Doygunluğu düşürmek tek başına ışıma gücünü
        // de yükseltiyor — `(0.72,0.80,1.00)` denendi, sahne bir tık daha aydınlandı.
        // Ton lineer uzayda eski rengin ışımasına (Y = 0.3844) ölçeklendi; `MoonIntensity`
        // bu yüzden 0.204'te kalıyor ve `SurfaceLightLevel` üzerinden pozlama uyumu da
        // kaymıyor.
        timeOfDay.MoonColor = new Color(0.586f, 0.653f, 0.818f, 1f);
    #endif

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

        // POZLAMA UYUM PAYI SAHNEYE YAZILIYOR. Alan serileştirilmiş: koddaki varsayılanı
        // değiştirmek sahnedeki eski örneği etkilemiyor, ölçülen düzeltme kaybolurdu.
        // Gerekçe ve ölçüm `LookController.adaptShare` başında.
        // YENİDEN ARANIYOR: yukarıdaki dal bileşeni bu karede yaratmış olabilir, o
        // durumda eldeki başvuru hâlâ boş.
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

    /// SİS RENDER GEÇİŞİ. Froxel hacmini iki compute dispatch'le dolduruyor; sonucu
    /// `_FogScatteringVolume` olarak global bağlıyor ve `HeightFog.hlsl` her yüzey
    /// shader'ında onu örnekliyor.
    ///
    /// SIRA: gökyüzü ve buluttan SONRA ekleniyor. Hacim aydınlatması bulut gölgesini ana
    /// ışığın cookie dokusundan okuyor; o doku bulut geçişi tarafından yazılıyor.
    /// SPEKTRAL YAĞIŞ PERDESİ FEATURE'I. Sis feature'ıyla aynı kalıp: var olan örnek de
    /// güncelleniyor, yalnız yokken kurulmuyor — sonradan alan eklenince eski örnek boş
    /// kalıp geçişi sessizce susturuyor.
    static void EnsureCurtainFeature()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException($"Renderer bulunamadı: {RendererPath}");

        const string ShaderPath = "Assets/Shaders/SpectralPrecipitation.shader";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
            throw new System.InvalidOperationException($"Perde shader'ı bulunamadı: {ShaderPath}");

        SpectralPrecipitationFeature feature = null;
        foreach (var existing in renderer.rendererFeatures)
            if (existing is SpectralPrecipitationFeature found) { feature = found; break; }

        bool isNew = feature == null;
        if (isNew)
        {
            feature = ScriptableObject.CreateInstance<SpectralPrecipitationFeature>();
            feature.name = "Spektral Yağış Perdesi";
        }

        var serialized = new SerializedObject(feature);
        serialized.FindProperty("curtainShader").objectReferenceValue = shader;
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

    static void EnsureFogFeature()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException($"Renderer bulunamadı: {RendererPath}");

        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(FogComputePath);
        if (compute == null)
            throw new System.InvalidOperationException($"Sis compute shader'ı bulunamadı: {FogComputePath}");

        // VAR OLAN ÖRNEK DE GÜNCELLENİYOR, yalnız yokken kurulmuyor. Feature'a sonradan
        // alan eklendiğinde erken dönen bir kurulum o alanı boş bırakıyor ve geçiş
        // sessizce çalışmıyor — sis buradan bir kez kaybedildi.
        VolumetricFogFeature feature = null;
        foreach (var existing in renderer.rendererFeatures)
            if (existing is VolumetricFogFeature found) { feature = found; break; }

        bool isNew = feature == null;
        if (isNew)
        {
            feature = ScriptableObject.CreateInstance<VolumetricFogFeature>();
            feature.name = "Volumetrik Sis";
        }

        // Bağlar eklemeden ÖNCE yazılıyor: `Create()` örnek listeye girer girmez Unity
        // tarafından çağrılıyor ve bağlar boşsa geçiş hiç kuyruğa girmiyor.
        var serialized = new SerializedObject(feature);
        serialized.FindProperty("compute").objectReferenceValue = compute;
        serialized.FindProperty("settings").objectReferenceValue =
            LoadOrCreate<VolumetricFogSettings>(FogSettingsPath);

        var skyFogShader = AssetDatabase.LoadAssetAtPath<Shader>(SkyFogShaderPath);
        if (skyFogShader == null)
            throw new System.InvalidOperationException($"Gökyüzü sisi shader'ı bulunamadı: {SkyFogShaderPath}");

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

    /// GÖKYÜZÜ RENDER GEÇİŞİ. `PhysicallyBasedSkyURP` (jiaozi158, MIT — HDRP'nin PBSky'ının
    /// URP portu). Rayleigh, Mie ve ozon soğurmasını LUT'lardan hesaplıyor; aynı LUT'lar
    /// hava perspektifini ve ambient probe'u da besliyor.
    ///
    /// Bulut portu bu paketle çalışmak üzere yazılmış: `URP_PBSKY` tanımlıyken bulutlar
    /// gezegen merkezini ve yarıçapını buradan alıyor, ambient probe'u paylaşıyor ve
    /// hava perspektifinden geçiyor. Tanımı `SkyPackageDefine` kuruyor.
    static void EnsureSkyFeature()
    {
    #if URP_PBSKY
        var renderer = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(RendererPath);
        if (renderer == null)
            throw new System.InvalidOperationException($"Renderer bulunamadı: {RendererPath}");

        foreach (var existing in renderer.rendererFeatures)
            if (existing is PhysicallyBasedSkyURP) return;

        var skyShader = Shader.Find("Hidden/Skybox/PhysicallyBasedSky");
        var lutShader = Shader.Find("Hidden/Sky/PhysicallyBasedSkyPrecomputation");
        if (skyShader == null || lutShader == null)
            throw new System.InvalidOperationException(
                "Gökyüzü shader'ları bulunamadı. Paket kurulu mu: " +
                "Packages/com.jiaozi158.unity-physically-based-sky-urp");

        var feature = ScriptableObject.CreateInstance<PhysicallyBasedSkyURP>();
        feature.name = "Physically Based Sky";

        // Feature shader'ları `Create()` içinde `Shader.Find` ile KARŞILAŞTIRIYOR; eşleşmezse
        // hata basıp hiçbir geçiş eklemiyor. Bu yüzden örnek üretildikten hemen sonra,
        // `Create()` elle çağrılmadan önce bağlanıyorlar.
        var serialized = new SerializedObject(feature);
        serialized.FindProperty("m_Shader").objectReferenceValue = skyShader;
        serialized.FindProperty("m_LutShader").objectReferenceValue = lutShader;
        serialized.FindProperty("m_FallbackSkyMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
        // Gök yansıması bulutları da içersin: paket yansıma küpünü pişirirken bu materyali
        // kullanıyor. Boş bırakılırsa yansımada gök var, bulut yok.
        serialized.FindProperty("m_VolumetricCloudsMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>(CloudMaterialPath);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // `Create()` ELLE ÇAĞRILMIYOR — bulut feature'ında çağrılıyor ama burada olmaz.
        // Gökyüzü feature'ının `Create()`'i ilk satırında `VolumeManager.instance.stack`
        // okuyor; bootstrap `delayCall`'dan çalışırken hacim yığını henüz kurulmamış
        // oluyor ve `NullReferenceException` atıyor. Shader'lar zaten eklemeden ÖNCE
        // bağlandığı için Unity kendi `Create()`'ini çağırdığında doğrulama geçiyor.
        renderer.rendererFeatures.Add(feature);
        AssetDatabase.AddObjectToAsset(feature, renderer);
        EditorUtility.SetDirty(renderer);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererPath);
    #endif
    }

    /// GÜNEŞ SOĞURMASI BULUTUN KENDİ KONUMUNDA. Kapalıyken bulut shader'ı
    /// `sun.color * PI` yazıyor, yani HAM güneşi — soğurma hiç uygulanmıyor ve bulutlar
    /// 18:00'de bile bembeyaz kalıyordu. Açıkken `_PHYSICALLY_BASED_SUN` devreye giriyor
    /// ve gökyüzü paketinin `EvaluateSunColorAttenuation`'ı bulutun bulunduğu KOTTA
    /// uygulanıyor; kameradaki değerden daha doğru, çünkü bulut 2-5 km yukarıda.
    static void SetCloudSunAttenuation(VolumetricCloudsURP feature)
    {
    #if URP_PBSKY
        var serialized = new SerializedObject(feature);
        var property = serialized.FindProperty("sunAttenuation");
        if (property.boolValue) return;

        property.boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        // `Create()` ÇAĞRILMIYOR: `_PHYSICALLY_BASED_SUN` anahtarı her karede
        // `UpdateMaterialProperties` içinde bu bool'a bakılarak ayarlanıyor, kurulum
        // gerektirmiyor. Elle çağırmak `Create()`'in içindeki hacim okumasında patlıyor.
        EditorUtility.SetDirty(feature);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RendererPath);
    #endif
    }

    /// GÖKYÜZÜ HACMİ. Üç override bulut hacminin profiline yazılıyor; bulut portu gezegen
    /// yarıçapını ve ambient kipini oradan okuyor.
    ///
    /// YALNIZ ASSET'E yazılıyor. `Volume.profile` asset'in çalışma-zamanı kopyası ve
    /// kopyaya `Add<T>()` ile bileşen üretmek DENENDİ: üretilen bileşenler hiçbir asset'e
    /// ait olmuyor, Play'e girerken yok ediliyor ve profilin bileşen listesi bozuluyor
    /// (`VolumeComponent.parameters` null). Kopya zaten her domain reload'da asset'ten
    /// yeniden üretiliyor, yani asset doğruysa kopya da doğru oluyor.
    static void EnsureSkyVolume()
    {
    #if URP_PBSKY
        if (cloudVolume == null)
            throw new System.InvalidOperationException(
                "Gökyüzü hacmi bulut hacminden sonra kurulmalı: `cloudVolume` yazılmamış.");

        ApplySkyOverrides(cloudVolume.sharedProfile);

        // ORTAM KİPİ SKYBOX OLMAK ZORUNDA. Sahnede `Flat` kalmıştı: `AtmosphereController`
        // eskiden hem kipi hem rengi yazıyordu, yazan kod kaldırıldı ama sahnedeki kip
        // kaldı ve paketin dinamik probe'u hiç devreye girmedi. ÖLÇÜLDÜ — probe öğle ve
        // gece birebir aynıydı (`0.223 0.293 0.420`) ve tepe ile taban da aynıydı, yani
        // gökyüzünden pişmiş değil düz bir renkti. Bulutlar günün her saatinde o donmuş
        // rengi yiyordu.
        // YANSIMA ŞİDDETİ 1, YANİ KISILMIYOR. Eskiden `AtmosphereController` bunu gök
        // seviyesinden türetiyordu ve ölçülmüş bir gerekti: pişen harita gece kararmıyor,
        // bisikletin kromu karanlıkta parlıyordu. O gerekçe ORTADAN KALKTI — paket
        // yansıma küpünü gerçek gökyüzünden pişiriyor, gece küpün kendisi karanlık.
        // Telafi terimi geri eklenmiyor; kısıcı yalnız haritanın yalan söylediği yerde
        // gerekliydi.
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

        // Fiziksel gökyüzü seçiliyor; 0 "gökyüzü yok" demek ve paket hiçbir şey çizmiyor.
        SetSky(visualEnvironment.skyType, (int)VisualEnvironment.SkyType.PhysicallyBased);

        // DÜNYA UZAYI. Kamera uzayında gök ve bulutlar kamerayla birlikte taşınıyor;
        // 5709 m'lik dağda katmanın üstüne çıkmak imkânsız hâle geliyordu. Bulut tarafında
        // `localClouds` anahtarı da bu değeri izliyor — ikisi tek yerden geliyor.
        SetSky(visualEnvironment.renderingSpace, VisualEnvironment.RenderingSpace.World);

        // Ortam ışığı gökyüzünden pişiyor. `AtmosphereController` artık `ambientLight`
        // yazmıyor: iki yazar aynı kareyi çekiştirince sonuç yazma sırasına kalıyordu.
        SetSky(visualEnvironment.skyAmbientMode, VisualEnvironment.SkyAmbientMode.Dynamic);

        SetSky(pbrSky.type, PhysicallyBasedSky.PhysicallyBasedSkyModel.EarthAdvanced);

        // HAVA PERSPEKTİFİ. Brief'in şartı: uzak dağ, silüet ve BULUT aynı atmosferik
        // perdeden geçmeli. Bulut geçişi bunu okuyup 7 numaralı birleştirme pass'ine
        // geçiyor.
        SetSky(pbrSky.atmosphericScattering, true);

        // YILDIZLAR PROSEDÜREL. Küp harita silindi: 512'lik yüzde bir teksel 0.176°,
        // ekranda bir piksel 0.047° — her yıldız zorunlu olarak dört piksel genişliğinde
        // ve bilineer süzmeyle yumuşak bir lekeydi. Bir piksele inmek 2048'lik yüz, yani
        // RGBAHalf'ta 201 MB isterdi. Ayrıca durağan doku TİTREYEMEZ.
        // Üretim ve sayılar `Assets/Shaders/StarField.hlsl` başında.
        //
        // ÇARPAN 0.08 → 0.55. Eski sayı gökyüzü BEŞ DURAK DAHA PARLAKKEN kurulmuştu ve
        // ölçütü "en parlak yıldız gökten 12 kat parlak" idi. Ay fiziksel orana çekilince
        // gök koyulaştı ama yıldızların MUTLAK seviyesi yerinde kaldı; oran 400 kata
        // çıktı, buna karşın ekranda yıldızlar kayboldu. Oran yanlış ölçüttü: görünürlüğü
        // gökle kıyas değil, yıldızın ekrandaki kendi seviyesi belirliyor.
        //
        // Yeni ölçüt fiziksel: 6. KADİR ÇIPLAK GÖZÜN SINIRINDA OLMALI. Gece pozlaması
        // ×2 (profil −1.5 EV + uyum tavanı 2.5 EV) alınarak kâğıtta:
        //
        //   kadir 0–1  bağıl 1.00  → 0.55 × 2 = 1.10   → doygun nokta
        //   kadir 2    bağıl 0.158 → 0.174             → sRGB ~0.42, belirgin
        //   kadir 4    bağıl 0.025 → 0.0276            → sRGB ~0.19, sönük ama var
        //   kadir 6    bağıl 0.004 → 0.0044            → sRGB ~0.08, tam sınırda
        //
        // GÜNDÜZ SOLMASI ARTIK AÇIKÇA YAZILI. Eskiden `(1 − skyOpacity)`in halledeceği
        // varsayılmıştı; ölçüldü, yanlış — zenitte gündüz opaklık ~0.2 ve sabah 8'de
        // gökyüzü yıldızlıydı. Solma güneş yüksekliğinden, kadire göre ayrı ayrı.
        SetSky(pbrSky.spaceEmissionMultiplier, 0.55f);

        // PAKETİN SİSİ ŞİMDİLİK KAPALI. Kendi yükseklik sisimiz sis bankları, inversiyon
        // ve vadi sis denizi taşıyor; pakette bunların karşılığı yok. İkisi birlikte
        // açılırsa sis iki kez uygulanıyor. Geçiş `DECISIONS.md`'de kayıtlı.
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

    /// BAĞ 2 ve 3: bulut katmanının oyun tarafındaki tek kaynağı. Yağış kesimi ve tırmanma
    /// göstergesi kotları buradan alıyor; bulutları çizen render özelliğine soramazlar.
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

        // GİRDİ BAĞLARI: dünya durumunu bulut ayarlarına çeviren tek yön.
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

    /// GÖKYÜZÜ SÜRÜCÜLERİ. Bulut sondasıyla aynı nesnede duruyorlar — ikisi de aynı
    /// Volume'u sürüyor — ama kurulumları AYRI, çünkü `TimeOfDay`'e bağımlılar ve o
    /// sahne kurulumunda daha sonra üretiliyor. Bulut sondasının içinde kurulduklarında
    /// sıfırdan kurulan bir sahnede saat henüz yokken bağlanıyorlardı.
    static void EnsureSkyDrivers(ref bool changed)
    {
        var probe = Object.FindAnyObjectByType<CloudLayerProbe>();
        if (probe == null)
            throw new System.InvalidOperationException(
                "Gökyüzü sürücüleri bulut sondasından sonra kurulmalı: sonda yok.");

        // Atmosferin hava bağı bulut sondasıyla aynı nesnede: ikisi de aynı Volume'u
        // sürüyor ve aynı hava durumundan besleniyor.
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

        // ORTAM IŞIĞI GERÇEK GÖKYÜZÜNDEN PİŞİYOR. Paketin analitik probe'u çoklu saçılım
        // taşımıyordu ve alacakaranlıkta sıfır veriyordu; devre dışı bırakıldı.
        var ambientBaker = Object.FindAnyObjectByType<SkyAmbientBaker>();
        if (ambientBaker == null)
        {
            ambientBaker = probe.gameObject.AddComponent<SkyAmbientBaker>();
            changed = true;
        }

        ambientBaker.Bind(Object.FindAnyObjectByType<TimeOfDay>());
        EditorUtility.SetDirty(ambientBaker);
    }

    /// Gürültü dokuları materyalde duruyor, hiçbir kod atamıyor — repo hazır materyalle geliyordu.
    /// Eşleşme shader'daki örneklemeden: `_Worley128RGBA` düşük frekans şekil, `_ErosionNoise` detay.
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
        if (!profile.TryGet(out VolumetricClouds clouds))
        {
            clouds = profile.Add<VolumetricClouds>(overrides: true);
            clouds.name = nameof(VolumetricClouds);
            AssetDatabase.AddObjectToAsset(clouds, profile);
        }

        // F1 paneli de bunu okuyor: sahnede birden fazla Volume var, ayrı ayrı aranırsa
        // panel bulutu taşımayan profile bağlanabilir.
        cloudVolume = volume;

        clouds.state.value = true;

        // KATMAN MUTLAK KOTTA. Yerel olmayan kipte ışın başlangıcı `float3(0,0,0)`, yani
        // kamera deniz seviyesindeymiş gibi davranılıyor ve bulutlar oyuncuyla birlikte
        // yükseliyor — 5709 m'lik dağda katmanın üstüne hiç çıkılamıyordu. Ayrıca hava
        // haritası kamera XZ'siyle kaydırılıyor; `CloudLayerProbe` mutlak XZ okuduğu için
        // gökyüzüyle gösterge ayrışıyordu. Yerel kip ikisini de düzeltiyor.
        clouds.localClouds.value = true;
        clouds.localClouds.overrideState = true;

        // BAĞ 1: yer bulut gölgesi. Bulut sistemi gölgeyi ana ışığın cookie dokusuna
        // yazıyor, arazi shader'ı `_LIGHT_COOKIES` ile okuyor.
        clouds.shadows.value = true;
        clouds.shadows.overrideState = true;
        // Harita ayar değil, bağlantı: olmadan kapsama alanı yok.
        clouds.cloudMap.value = CloudMapGenerator.EnsureExists();
        clouds.cloudMap.overrideState = true;

        // AYARLANMIŞ DEĞERLER. F1'de göz kararı bulunup onaylandı; burada duruyorlar ki
        // sahne yeniden kurulduğunda geri gelsinler. Kapsama, yoğunluk, rüzgâr hızı ve
        // yönü Play'de `CloudWeatherDriver` tarafından havadan yazılıyor — buradaki
        // değerler sürücü kapalıyken (F1 → "Havadan ayır") geçerli olan başlangıç.
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
        // HARITA PERIYODU 48 KM. Doseme kirici bu periyoda gore tasarlandi; harita
        // 40 km'de kalinca ikisi hizalanmiyor ve kirici kendi kafesini birakiyor.
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
        // BULUT GOLGESI KESKINLIGI. Golge ayri bir cookie dokusundan geliyor (URP'nin 60 m
        // kaskad golgesiyle ilgisi yok). Keskinlik = texel boyu = bolge / cozunurluk.
        //   - shadowDistance: cookie kamera-merkezli ve oyuncuyu takip ediyor; 30 km arenayi
        //     statik kaplamasi gerekmiyor. 12 km yaricap uzaktaki golgeleri de yakalar.
        //   - Ultra1024: 12 km icin texel ~27 m (arazi yontma hucresiyle ayni). Iki 3x3
        //     bulanik gecisi bu ince texelde dar penumbra birakir: BELIRGIN ama dogal
        //     yumusak, ezik degil. 256'da texel ~70 m'ydi ve gecisler onu lapaya ceviriyordu.
        // Ikisi de arena YATAY olcegine bagli, dagin boyuna DEGIL (SCALE.md).
        SetCloud(clouds.shadowResolution, VolumetricClouds.CloudShadowResolution.Ultra1024);
        SetCloud(clouds.shadowDistance, 12000f);

        // ADIM SAYISI. Menzil = adim boyu x adim sayisi, adim boyu altitudeRange/6'dan.
        // 80'de menzil 44 km; ufka bakista isin katmanda uzun kalip yuruyus erken bitiyordu.
        SetCloud(clouds.numPrimarySteps, 128);
        SetCloud(clouds.numLightSteps, 8);
        SetCloud(clouds.temporalAccumulationFactor, 0.95f);
        SetCloud(clouds.perceptualBlending, 1.00f);
        SetCloud(clouds.fadeInStart, 0f);
        // YAKIN SONUM 300 M. 5000'de saturate(d/fadeInDistance) kuresel bir irtifa
        // carpanina donusuyor (yerde ~2 km, 20 km'de ~15 km) ve bulut yukseldikce gece
        // simsiyah okunuyordu. Gercek isi kameranin burnunda yogun bulut olusmasini
        // engellemek; o is birkac yuz metrede biter.
        SetCloud(clouds.fadeInDistance, 300f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile));
    }

    /// `overrideState` açılmadan harmanlama parametreyi atlıyor: değer profile yazılır ama
    /// yığına hiç geçmez.
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

        // SAÇILMA TABLOSU. `LightningLutBaker` yoksa pişiriyor; burada yalnız
        // bağlanıyor. Bulunamazsa parlama sessizce kaybolurdu, o yüzden yüksek sesle.
        var scatterLut = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Settings/LightningScatterLut.asset");
        if (scatterLut == null)
            throw new System.InvalidOperationException(
                "Şimşek saçılma tablosu yok: Assets/Settings/LightningScatterLut.asset");

        flash.Bind(thunder, atmosphere, observer, tuning,
            Object.FindAnyObjectByType<CloudLayerProbe>(), scatterLut, 9000f, terrain);
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
        var go = new GameObject("Time Of Day");
        go.AddComponent<TimeOfDay>();
    }

    /// GÜNEŞ VE AY AYRI IŞIK. Tek ışığa iki cisim sığmıyordu: ay güneşin tam karşısında,
    /// yön bir tanedir ve devir anında disk 180° atlıyordu.
    ///
    /// AY GÖLGE DÜŞÜRMÜYOR ve bu bilinçli: gökyüzü paketinin `GetMainLight`'ı gölgesiz
    /// cismi ana ışık saymayıp `RenderSettings.sun`'a düşüyor. Böylece gökyüzü her zaman
    /// güneşten sürülüyor, ana ışık gece bile değişmiyor.
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

        // AY GÖLGE DÜŞÜRÜYOR ve bu gerekli: paketin `GetMainLight`'ı gölgesiz cismi ana
        // ışık saymayıp `RenderSettings.sun`'a düşüyor. Gökyüzü saçılımı tek cisimden
        // geldiği için ay ana ışık olamazsa gece göğü hiç aydınlanmıyor.
        //
        // URP yalnız ana ışığın gölgesini çiziyor, yani iki gölge kaynağı oluşmuyor.
        moon.shadows = LightShadows.Soft;

        // GÜNEŞ BİLEŞENDEN AYIRT EDİLİYOR, ADDAN DEĞİL. Sahnede ÜÇ yönlü ışık var:
        // güneş, ay ve şimşek. Eskiden yalnız ay adı eleniyordu ve `FindObjectsByType`
        // sırası garantili olmadığı için ŞİMŞEK ışığı güneş sanılıp `TimeOfDay`'e
        // bağlanabiliyordu. O olduğunda gerçek güneş hiç sürülmüyor, son şiddetinde
        // (3.03) ve son açısında gökte asılı kalıyor — gece yarısında ayın yanında
        // ikinci bir parlak cisim olarak görünüyordu. Ölçüldü: cisim0 yönü güneşi
        // +31°'de gösteriyordu, saat 00:00'da.
        //
        // Şimşek `[RequireComponent(typeof(Light))]` ile kendi ışığını taşıyor, yani
        // bileşeninden kesin ayırt ediliyor.
        Light sun = null;
        foreach (var light in Object.FindObjectsByType<Light>())
        {
            if (light.type != LightType.Directional) continue;
            if (light == moon) continue;
            if (light.GetComponent<LightningFlash>() != null) continue;

            if (sun != null)
                throw new System.InvalidOperationException(
                    $"Sahnede birden fazla güneş adayı yönlü ışık var: {sun.name} ve {light.name}. " +
                    "Hangisinin güneş olduğu belirsiz.");

            sun = light;
        }

        if (sun == null)
            throw new System.InvalidOperationException(
                "Güneş bulunamadı: ay ve şimşek dışında yönlü ışık yok.");

        // URP EK VERİSİ İKİ IŞIKTA DA OLMALI. Unity bu bileşeni yalnız ışık MENÜDEN
        // eklendiğinde otomatik koyuyor; koddan eklenen ışıkta olmuyor. Bulut gölge
        // geçişi ana ışığın cookie ayarlarını buradan okuyor ve yoksa
        // `NullReferenceException` atıyor. Ana ışık gece aya, gündüz güneşe döndüğü için
        // ikisinde de bulunmak zorunda.
        EnsureLightData(sun, ref changed);
        EnsureLightData(moon, ref changed);

        timeOfDay.Bind(sun, moon);
        EditorUtility.SetDirty(timeOfDay);
        EditorUtility.SetDirty(moon);
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
            Object.FindAnyObjectByType<RouteOverlay>(FindObjectsInactive.Include),
            cloudVolume,
            Object.FindAnyObjectByType<CloudWeatherDriver>());

        EditorUtility.SetDirty(menu);

        // GEÇİCİ: tırtık işaretleyici. Kullanıcı ekran ortasını tırtıklı yere
        // doğrultup M'ye basıyor, koordinat `Logs/notches.log`'a yazılıyor. Belirti
        // çözülünce bileşen de bu blok da silinir.
        var marker = Object.FindAnyObjectByType<NotchMarker>();
        if (marker == null)
        {
            marker = menu.gameObject.AddComponent<NotchMarker>();
            changed = true;
        }

        marker.Bind(player.GetComponentInChildren<Camera>());
        EditorUtility.SetDirty(marker);
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
            Object.FindAnyObjectByType<TemperatureField>(),
            Object.FindAnyObjectByType<CloudLayerProbe>());

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
