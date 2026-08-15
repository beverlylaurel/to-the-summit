using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// F1 ile açılan test paneli. Esc oyunun kendi menüsüne ayrıldı.
/// Sistemlerin içine "debug modu" kavramı sızmaz; kilitler bileşenin KENDİ test
/// anahtarını kullanır. Bileşeni kapatmak, onu okumaya devam eden tüketicilere donmuş
/// bir durum bırakıyor ve tek hava durumu iki kanala ayrılıyordu.
public class DebugMenu : MonoBehaviour
{
    [SerializeField] FirstPersonController walker;
    [SerializeField] FreeFlyMovement flyer;
    [SerializeField] WeatherState weather;
    [SerializeField] AltitudeWeatherDriver weatherDriver;
    [SerializeField] WindField wind;
    [SerializeField] ThunderPlayer thunder;
    [SerializeField] LightningFlash lightning;
    [SerializeField] TimeOfDay time;
    [SerializeField] AtmosphereController atmosphere;
    [SerializeField] PrecipitationRenderer precipitation;
    [SerializeField] PerformanceHud hud;
    [SerializeField] ClimbHud climbHud;
    [SerializeField] CursorLock cursorLock;
    [Tooltip("Kar çarpışma yüzeyinin ayrışma probu. Ölçüm bitince prob da bu bölüm de silinir.")]
    [SerializeField] SnowCollisionProbe snowProbe;
    [Tooltip("Rota çizgilerinin oyun görünümü katmanı.")]
    [SerializeField] RouteOverlay routeOverlay;
    [Tooltip("Bulut ayarlarını taşıyan Volume bileşeni.")]
    [SerializeField] Volume cloudVolume;

    [Tooltip("Bulut ayarlarini havadan suren bilesen; \"Havadan ayir\" bunu kapatiyor.")]
    [SerializeField] CloudWeatherDriver cloudDriver;

    [Tooltip("Atmosfer ayarlarini havadan suren bilesen.")]
    [SerializeField] SkyWeatherDriver skyDriver;

    const float PanelWidth = 1560f;
    const float ColumnWidth = 300f;
    const float Margin = 24f;

    /// Oturum OYUNUN KENDİ HIZINDA ve yürüyerek başlar. Bir dönem serbest uçuş ve yüz
    /// kat hız açık başlıyordu — arazi büyükken her açılışta uzak noktaya gitmek
    /// gerekiyordu. Artık mesafe algısı ve bisiklet sürüşü doğru hissedilsin diye
    /// varsayılan gerçek hız; ikisi de F1 panelinde açık duruyor.
    const float StartSpeedMultiplier = 1f;

    float speedMultiplier = StartSpeedMultiplier;
    bool freeFly;






    static readonly int TerrainShadowId = Shader.PropertyToID("_TerrainShadowReceive");


    bool weatherLocked;
    float lockedPrecipitation = 0.6f;
    float lockedSnowiness;

    bool windLocked;
    float lockedWindStrength = 0.5f;
    float lockedWindAngle;

    /// Bulut ayarları `cloudVolume.profile` üzerinden sürülüyor — asset'in kendisi değil,
    /// Volume'un çalışma zamanı KOPYASI. `sharedProfile`'a yazmak işe yaramıyor: sahnede
    /// başka bir bileşen `.profile`'a dokunduğu an Volume harmanlamayı kopyadan yapmaya
    /// başlıyor ve asset'e yazılan değer hiç okunmuyor (ölçüldü: profil 0.71, yığın 0.40).
    /// Açılıştaki değerler geri al düğmeleri için saklanıyor.
    VolumetricClouds clouds;
#if URP_PBSKY
    PhysicallyBasedSky sky;
    VisualEnvironment visualEnvironment;
    bool detachSkyFromWeather;
    float sunIntensityDefault;
    float moonIntensityDefault;
#endif

    /// Acilistaki degerler: her satirin ↺'u ve "Bulut ayarlarini geri al" buradan okuyor.
    /// Cizim aninda yakalanamaz — `CloudWeatherDriver` kapsama, yogunluk ve ruzgari her
    /// karede yaziyor, ilk cizimde okunan deger zaten surulmus olan olurdu.
    /// `VolumeParameter` hem `Equals`'i hem `GetHashCode`'u DEGERINDEN turetiyor: sozluk
    /// varsayilan karsilastiriciyla kullanilirsa surgu oynadigi anda anahtarin hash'i
    /// degisiyor ve kayit bulunamaz oluyor. Anahtar kimlik olmali, deger degil.
    sealed class ParameterIdentity : IEqualityComparer<VolumeParameter>
    {
        public static readonly ParameterIdentity Default = new();
        public bool Equals(VolumeParameter a, VolumeParameter b) => ReferenceEquals(a, b);
        public int GetHashCode(VolumeParameter p) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(p);
    }

    readonly Dictionary<VolumeParameter, float> cloudFloatDefaults = new(ParameterIdentity.Default);
    readonly Dictionary<VolumeParameter, bool> cloudBoolDefaults = new(ParameterIdentity.Default);
    bool detachFromWeather;

    bool open;
    float timeScale = 1f;

    Vector2 scroll;
    // GUIStyle SERİLEŞTİRİLMİYOR. Unity onu kaydetmeye çalıştığında içindeki font
    // referansı derleme sonrası geçersiz kalıyor ve her yeniden yüklemede
    // "Deleting invalid font reference" uyarısı basılıyor. Biçim zaten her
    // kullanımda kuruluyor, saklanacak bir şey yok.
    [System.NonSerialized] GUIStyle header;
    [System.NonSerialized] GUIStyle title;

    public void Bind(FirstPersonController walkerRef, FreeFlyMovement flyerRef,
        WeatherState weatherRef, AltitudeWeatherDriver driverRef, WindField windRef,
        ThunderPlayer thunderRef, LightningFlash lightningRef, TimeOfDay timeRef,
        AtmosphereController atmosphereRef, PrecipitationRenderer precipitationRef,
        PerformanceHud hudRef, ClimbHud climbHudRef,
        CursorLock cursorLockRef, SnowCollisionProbe snowProbeRef,
        RouteOverlay routeOverlayRef, Volume cloudVolumeRef, CloudWeatherDriver cloudDriverRef,
        SkyWeatherDriver skyDriverRef)
    {
        cloudVolume = cloudVolumeRef;
        cloudDriver = cloudDriverRef;
        skyDriver = skyDriverRef;
        cursorLock = cursorLockRef;
        walker = walkerRef;
        flyer = flyerRef;
        weather = weatherRef;
        weatherDriver = driverRef;
        wind = windRef;
        thunder = thunderRef;
        lightning = lightningRef;
        time = timeRef;
        atmosphere = atmosphereRef;
        precipitation = precipitationRef;
        hud = hudRef;
        climbHud = climbHudRef;
        snowProbe = snowProbeRef;
        routeOverlay = routeOverlayRef;
    }

    void OnEnable()
    {
        if (walker == null || flyer == null || weather == null || weatherDriver == null
            || wind == null || thunder == null || lightning == null || time == null
            || atmosphere == null
            || precipitation == null || hud == null || climbHud == null
            || cursorLock == null || snowProbe == null || routeOverlay == null
            || cloudVolume == null || cloudDriver == null)
            throw new InvalidOperationException($"{nameof(DebugMenu)}: bağımlılıklar atanmadı.");

        if (!cloudVolume.profile.TryGet(out clouds))
            throw new InvalidOperationException($"{nameof(DebugMenu)}: profilde {nameof(VolumetricClouds)} yok.");

        // Üç bileşen için de biriktiriliyor; temizlik burada bir kez, yakalama üç kez.
        cloudFloatDefaults.Clear();
        cloudBoolDefaults.Clear();

        CaptureDefaults(clouds);

        detachFromWeather = !cloudDriver.enabled;

#if URP_PBSKY
        if (!cloudVolume.profile.TryGet(out sky))
            throw new InvalidOperationException($"{nameof(DebugMenu)}: profilde {nameof(PhysicallyBasedSky)} yok.");
        if (!cloudVolume.profile.TryGet(out visualEnvironment))
            throw new InvalidOperationException($"{nameof(DebugMenu)}: profilde {nameof(VisualEnvironment)} yok.");

        CaptureDefaults(sky);
        CaptureDefaults(visualEnvironment);

        sunIntensityDefault = time.SunIntensity;
        moonIntensityDefault = time.MoonIntensity;
        detachSkyFromWeather = skyDriver != null && !skyDriver.enabled;
#endif

        // Hız çarpanı yalnızca panel çizilirken uygulanıyordu; panel hiç açılmazsa
        // başlangıç değeri de hiç etkili olmuyordu.
        walker.SpeedMultiplier = speedMultiplier;
        flyer.SpeedMultiplier = speedMultiplier;

        walker.enabled = !freeFly;
        flyer.enabled = freeFly;
        time.Paused = true;
        weatherDriver.Instant = true;

        open = false;
    }

    void OnDisable()
    {
        Time.timeScale = 1f;

        // Panel açıkken bileşen kapanırsa imleç serbest kalırdı
        if (open) cursorLock.Restore();
        open = false;
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) Toggle();

        if (weatherLocked)
        {
            weatherDriver.IntensityOverride = lockedPrecipitation;
            weatherDriver.SnowinessOverride = lockedSnowiness;
        }
        if (windLocked) wind.ApplyOverride(lockedWindStrength, lockedWindAngle);
    }

    void Toggle()
    {
        open = !open;

        if (open) cursorLock.Release();
        else cursorLock.Restore();
    }

    void OnGUI()
    {
        if (!open) return;

        EnsureStyles();

        float width = Mathf.Min(PanelWidth, Screen.width - Margin * 2f);
        float height = Screen.height - Margin * 2f;
        var area = new Rect((Screen.width - width) * 0.5f, Margin, width, height);

        GUILayout.BeginArea(area);

        GUILayout.Label("Test paneli — çıkmak için F1", title);

        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.BeginHorizontal();

        BeginColumn();
        DrawMovement();
        DrawTimeOfDay();
        EndColumn();

        BeginColumn();
        DrawWeather();
        DrawWind();
        EndColumn();

        BeginColumn();
        DrawCloudShape();
        DrawCloudErosion();
        EndColumn();

        BeginColumn();
        DrawCloudLight();
        DrawCloudQuality();
        EndColumn();

        BeginColumn();
        DrawSky();
        DrawOverlays();
        DrawSnowCollision();
        EndColumn();


        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void EnsureStyles()
    {
        header ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            margin = new RectOffset(0, 0, 0, 2)
        };

        title ??= new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 14,
            alignment = TextAnchor.MiddleCenter
        };
    }

    void BeginColumn() => GUILayout.BeginVertical(GUILayout.Width(ColumnWidth));
    void EndColumn()
    {
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
    }

    /// Başlıklı kutu açar; kapatmak için EndSection
    /// TEŞHİS — bisiklet zeminden ne kadar yukarıda. Gölgeyle arasında boşluk görülüyor
    /// ve iki açıklaması var: bisiklet havada duruyor ya da gölge kaydırılmış. Yükseklik
    /// ile güneşin açısı birlikte yazılıyor çünkü alçak güneşte bir santimlik boşluk bile
    /// gölgeyi metrelerce öteliyor — boşluğun büyüklüğü tek başına bir şey söylemiyor.
    void LateUpdate()
    {
        // Arazi gölge okuması teşhis anahtarıyla kapatılabiliyordu; anahtar silindi ama
        // global kalmalı — yazılmazsa sıfır kalır ve arazi gölge almaz.
        Shader.SetGlobalFloat(TerrainShadowId, 1f);
    }

    void BeginSection(string label)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label(label, header);
    }

    static void EndSection()
    {
        GUILayout.EndVertical();
        GUILayout.Space(4f);
    }

    /// BULUT AYARLARI. Degerler Volume profilinde, yani asset'te: surgu dogrudan ona
    /// yaziyor ve Play bitince degisiklik kaliyor. Her satirin sonundaki ↺ acilistaki
    /// degere doner.
    void DrawCloudShape()
    {
        BeginSection("Bulut — bicim");

        // Kapsama, yogunluk ve ruzgar `CloudWeatherDriver` tarafindan her karede
        // firtinadan yaziliyor; surucü kapatilmadan bu surgulerin yazdigi deger bir
        // sonraki karede eziliyor.
        bool detach = GUILayout.Toggle(detachFromWeather, "Havadan ayir (elle ayar)");
        if (detach != detachFromWeather)
        {
            detachFromWeather = detach;
            cloudDriver.enabled = !detach;
        }

        // ÖLÇÜM: toggle'ın gerçekten sürücüyü kapatıp kapatmadığı ve profilde o an ne
        // yazdığı. "Sürücü AÇIK" kalıyorsa sorun toggle'da, değerler yine de eziliyorsa
        // yazan başka biri var.
        GUILayout.Label($"sürücü {(cloudDriver.enabled ? "AÇIK" : "KAPALI")} · " +
            $"kapsama {clouds.cloudCoverage.value:F2} · yoğunluk {clouds.densityMultiplier.value:F2}");

        CloudSlider("Kapsama", clouds.cloudCoverage);
        CloudSlider("Yogunluk", clouds.densityMultiplier);
        CloudSlider("Ruzgar hizi (km/s)", clouds.globalSpeed, 0f, 200f, "F0");
        CloudSlider("Ruzgar yonu", clouds.globalOrientation);
        CloudSlider("Sekil hiz carpani", clouds.shapeSpeedMultiplier);
        CloudSlider("Erozyon hiz carpani", clouds.erosionSpeedMultiplier);
        CloudSlider("Dikey sekil ruzgari", clouds.verticalShapeWindSpeed, -50f, 50f, "F1");
        CloudSlider("Dikey erozyon ruzgari", clouds.verticalErosionWindSpeed, -50f, 50f, "F1");
        CloudSlider("Sekil orani", clouds.shapeFactor);
        CloudSlider("Sekil olcegi", clouds.shapeScale, 0.5f, 50f, "F1");
        CloudSlider("Ors", clouds.anvilAmount);
        CloudSlider("Taban kotu (m)", clouds.bottomAltitude, 0f, 8000f, "F0");
        CloudSlider("Katman kalinligi (m)", clouds.altitudeRange, 100f, 8000f, "F0");
        CloudSlider("Yukseklik carpitmasi", clouds.altitudeDistortion);
        CloudSlider("Harita boyu (m)", clouds.cloudMapSize, 1000f, 100000f, "F0");
        CloudSlider("Dunya egriligi", clouds.earthCurvature);

        EndSection();
    }

    /// GÖKYÜZÜ. Paketin atmosfer parametreleri; hepsi profile yazılıyor, Play bitince kalıyor.
    ///
    /// "Güneş şiddeti" buraya BİLEREK konuldu: paket 100000 lux yer aydınlığına kalibreli
    /// ve güneş şiddeti 3.03 bekliyor, bizimki 1.5. Hangi taraftan telafi edileceği
    /// ölçülmedi (`DECISIONS.md`) — iki sürgü yan yana durursa gözle ayrılır.
    void DrawSky()
    {
#if URP_PBSKY
        BeginSection("Gökyüzü");

        if (skyDriver != null)
        {
            bool detach = GUILayout.Toggle(detachSkyFromWeather, "Havadan ayır (elle ayar)");
            if (detach != detachSkyFromWeather)
            {
                detachSkyFromWeather = detach;
                skyDriver.enabled = !detach;
            }
        }

        float sun = CloudRow("Güneş şiddeti (tepe)", time.SunIntensity, sunIntensityDefault, 0f, 6f, "F2");
        if (!Mathf.Approximately(sun, time.SunIntensity)) time.SunIntensity = sun;

        // Ay aynı ışığa yazılıyor: paket geceleyin atmosferi ondan aydınlatıyor ve bulut
        // yolu `× π` ile ölçeklediği için buluta araziden ~3 kat fazla giriyor.
        float moon = CloudRow("Ay şiddeti (tepe)", time.MoonIntensity, moonIntensityDefault, 0f, 0.4f, "F3");
        if (!Mathf.Approximately(moon, time.MoonIntensity)) time.MoonIntensity = moon;

        // ÖLÇÜM: ışığa GERÇEKTEN yazılan şiddet ve renk. Tepe değerinden farkı bizim
        // kendi atmosfer süzmemiz (`BeamLevel` ve `CurrentSunColor`). Paket bu ışığı
        // okuyup üstüne kendi transmittance'ını uyguluyor — fark büyükse atmosfer iki
        // kez soğuruyor demektir.


        CloudSlider("Pozlama (EV)", sky.exposure, -5f, 5f, "F2");
        CloudSlider("Parlaklık çarpanı", sky.multiplier, 0f, 4f, "F2");

        GUILayout.Space(4f);
        CloudSlider("Hava yoğunluğu R", sky.airDensityR);
        CloudSlider("Hava yoğunluğu G", sky.airDensityG);
        CloudSlider("Hava yoğunluğu B", sky.airDensityB);
        CloudSlider("Hava tavanı (m)", sky.airMaximumAltitude, 1000f, 200000f, "F0");

        GUILayout.Space(4f);
        CloudSlider("Aerosol yoğunluğu", sky.aerosolDensity, "F3");
        CloudSlider("Aerosol anizotropi", sky.aerosolAnisotropy);
        CloudSlider("Aerosol tavanı (m)", sky.aerosolMaximumAltitude, 1000f, 50000f, "F0");

        GUILayout.Space(4f);
        CloudSlider("Ozon yoğunluğu", sky.ozoneDensityDimmer);
        CloudSlider("Ozon tabanı (m)", sky.ozoneMinimumAltitude, 0f, 60000f, "F0");
        CloudSlider("Ozon genişliği (m)", sky.ozoneLayerWidth, 1000f, 60000f, "F0");

        GUILayout.Space(4f);
        CloudSlider("Gezegen yarıçapı (km)", visualEnvironment.planetRadius, 100f, 12000f, "F0");

        if (GUILayout.Button("Gökyüzü ayarlarını geri al"))
        {
            RestoreDefaults(sky);
            RestoreDefaults(visualEnvironment);
            time.SunIntensity = sunIntensityDefault;
            time.MoonIntensity = moonIntensityDefault;
        }

        EndSection();
#endif
    }

    void DrawCloudErosion()
    {
        BeginSection("Bulut — erozyon");

        CloudSlider("Erozyon orani", clouds.erosionFactor);
        CloudSlider("Erozyon olcegi", clouds.erosionScale, 10f, 300f, "F0");
        CloudSlider("Erozyon ortmesi", clouds.erosionOcclusion);
        CloudToggle("Mikro erozyon", clouds.microErosion);
        CloudSlider("Mikro oran", clouds.microErosionFactor);
        CloudSlider("Mikro olcek", clouds.microErosionScale, 50f, 400f, "F0");

        EndSection();
    }

    void DrawCloudLight()
    {
        BeginSection("Bulut — isik");

        CloudSlider("Sonum katsayisi", clouds.extinctionCoefficient, "F3");
        CloudSlider("Toz etkisi", clouds.powderEffectIntensity);
        CloudSlider("Coklu sacilma", clouds.multiScattering);
        CloudSlider("Ortam isigi", clouds.ambientLightProbeDimmer);
        CloudSlider("Gunes isigi", clouds.sunLightDimmer);
        CloudToggle("Yere golge dusur", clouds.shadows);
        CloudSlider("Golge koyulugu", clouds.shadowOpacity);
        CloudSlider("Golge yedek koyulugu", clouds.shadowOpacityFallback);
        CloudSlider("Golge mesafesi (m)", clouds.shadowDistance, 1000f, 30000f, "F0");

        EndSection();
    }

    void DrawCloudQuality()
    {
        BeginSection("Bulut — kalite");

        CloudSlider("Gorus adimi", clouds.numPrimarySteps);
        CloudSlider("Isik adimi", clouds.numLightSteps);
        CloudSlider("Zamansal birikim", clouds.temporalAccumulationFactor);
        CloudSlider("Algisal harmanlama", clouds.perceptualBlending);
        CloudSlider("Sonumlenme baslangici (m)", clouds.fadeInStart, 0f, 10000f, "F0");
        CloudSlider("Sonumlenme mesafesi (m)", clouds.fadeInDistance, 100f, 50000f, "F0");

        if (GUILayout.Button("Bulut ayarlarini geri al")) RestoreDefaults(clouds);

        EndSection();
    }

    /// Parametrenin `overrideState`'i kapaliysa harmanlama onu atliyor: surgu profile
    /// yaziyor ama yigina hic gecmiyor. Panelin surdugu her alan acik olmak zorunda.
    void CaptureDefaults(VolumeComponent component)
    {
        foreach (var parameter in component.parameters)
        {
            parameter.overrideState = true;

            switch (parameter)
            {
                case FloatParameter f: cloudFloatDefaults[parameter] = f.value; break;
                case IntParameter i: cloudFloatDefaults[parameter] = i.value; break;
                case BoolParameter b: cloudBoolDefaults[parameter] = b.value; break;
            }
        }
    }

    void RestoreDefaults(VolumeComponent component)
    {
        foreach (var parameter in component.parameters)
        {
            if (parameter is FloatParameter f && cloudFloatDefaults.TryGetValue(parameter, out float value))
                f.value = value;
            else if (parameter is IntParameter i && cloudFloatDefaults.TryGetValue(parameter, out float intValue))
                i.value = Mathf.RoundToInt(intValue);
            else if (parameter is BoolParameter b && cloudBoolDefaults.TryGetValue(parameter, out bool flag))
                b.value = flag;
        }
    }

    void CloudSlider(string label, ClampedFloatParameter parameter, string format = "F2")
    {
        parameter.value = CloudRow(label, parameter.value, cloudFloatDefaults[parameter],
            parameter.min, parameter.max, format);
    }

    /// `FloatParameter` ve `MinFloatParameter`'in ust siniri yok; surgu icin burada veriliyor.
    void CloudSlider(string label, FloatParameter parameter, float min, float max, string format)
    {
        parameter.value = CloudRow(label, parameter.value, cloudFloatDefaults[parameter], min, max, format);
    }

    void CloudSlider(string label, ClampedIntParameter parameter)
    {
        parameter.value = Mathf.RoundToInt(CloudRow(label, parameter.value,
            cloudFloatDefaults[parameter], parameter.min, parameter.max, "F0"));
    }

    void CloudToggle(string label, BoolParameter parameter)
    {
        parameter.value = GUILayout.Toggle(parameter.value, label);
    }

    static float CloudRow(string label, float value, float original, float min, float max,
        string format)
    {
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label($"{label} {value.ToString(format)}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("↺", GUILayout.Width(26f))) value = original;
        }
        return GUILayout.HorizontalSlider(value, min, max);
    }

    void DrawMovement()
    {
        BeginSection("Hareket");

        GUILayout.Label($"Hız çarpanı {speedMultiplier:F0}×");

        // Sürgü karesel: küçük değerlerde hassas, uçta 100×'e ulaşır
        float normalized = Mathf.Sqrt((speedMultiplier - 1f) / 99f);
        normalized = GUILayout.HorizontalSlider(normalized, 0f, 1f);
        speedMultiplier = 1f + normalized * normalized * 99f;

        walker.SpeedMultiplier = speedMultiplier;
        flyer.SpeedMultiplier = speedMultiplier;

        bool nextFreeFly = GUILayout.Toggle(freeFly, "Serbest uçuş (Q/E)");
        if (nextFreeFly != freeFly)
        {
            freeFly = nextFreeFly;
            walker.enabled = !freeFly;
            flyer.enabled = freeFly;
        }

        EndSection();
    }

    void DrawTimeOfDay()
    {
        BeginSection("Günün saati");

        GUILayout.Label($"Saat {time.Clock}");

        float value = time.Normalized;
        float next = GUILayout.HorizontalSlider(value, 0f, 1f);
        if (!Mathf.Approximately(next, value)) time.SetNormalized(next);

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Şafak")) time.SetNormalized(0.25f);
            if (GUILayout.Button("Öğle")) time.SetNormalized(0.5f);
            if (GUILayout.Button("Batım")) time.SetNormalized(0.75f);
            if (GUILayout.Button("Gece")) time.SetNormalized(0f);
        }

        time.Paused = GUILayout.Toggle(time.Paused, "Saati durdur");

        GUILayout.Space(6f);
        GUILayout.Label($"Oyun hızı {timeScale:F2}×");
        timeScale = GUILayout.HorizontalSlider(timeScale, 0f, 4f);
        Time.timeScale = timeScale;

        if (GUILayout.Button("Hızı normale döndür")) timeScale = 1f;

        EndSection();
    }

    void DrawWeather()
    {
        BeginSection("Hava durumu");

        GUILayout.Label($"Yağış {weather.Precipitation:F2}   Karlılık {weather.Snowiness:F2}");

        // Sürücü KAPATILMAZ, hedefi dışarıdan verilir. Kapatınca `StormIntensity` ve
        // `ClearWindow` donuyor ama atmosfer onları okumaya devam ediyordu: sürgü
        // yağışı, görüşü ve rengi sürerken bulut kapsaması kilitlenme anında kalıyor,
        // ikisi ayrışıyordu.
        bool nextLock = GUILayout.Toggle(weatherLocked, "Havayı elle ayarla");
        if (nextLock != weatherLocked)
        {
            weatherLocked = nextLock;
            if (!weatherLocked)
            {
                weatherDriver.IntensityOverride = -1f;
                weatherDriver.SnowinessOverride = -1f;
            }
        }

        using (new Disabled(!weatherLocked))
        {
            GUILayout.Label($"Yağış şiddeti {lockedPrecipitation:F2}");
            lockedPrecipitation = GUILayout.HorizontalSlider(lockedPrecipitation, 0f, 1f);

            GUILayout.Label($"Kar oranı {lockedSnowiness:F2}   (0 yağmur, 1 kar)");
            lockedSnowiness = GUILayout.HorizontalSlider(lockedSnowiness, 0f, 1f);
        }

        GUILayout.Space(6f);

        // Sürücü kilitliyken çalışmıyor; iki anahtarın da anlamı kalmıyor
        using (new Disabled(weatherLocked))
        {
            weatherDriver.Instant = GUILayout.Toggle(weatherDriver.Instant,
                "Hava yüksekliğe anında uysun");

            GUILayout.Label($"Açık pencere {weatherDriver.ClearWindow:F2}  " +
                            $"kalıntı {weatherDriver.WindowResidue:F2}");
            GUILayout.Label($"Şiddet {weatherDriver.StormIntensity:F2}  " +
                            $"bulut kütlesi {weatherDriver.CloudMass:F2}  " +
                            $"tavan payı {weatherDriver.CeilingAt(walker.transform.position.y):F2}");
            GUILayout.Label($"Kar sınırı {weatherDriver.RainCeiling:F0} → " +
                            $"{weatherDriver.SnowFloor:F0} m");
            weatherDriver.ForceWindow = GUILayout.Toggle(weatherDriver.ForceWindow,
                "Havayı zorla aç");
        }

        if (GUILayout.Button("Şimşek çaktır")) thunder.TriggerNow();

        // Çakmanın görünmemesi iki ayrı şey olabilir: olay hiç gelmemiştir ya da gelmiş
        // ama çizilmemiştir. Dışarıdan ikisi aynı görünüyor, o yüzden ölçüm burada.
        GUILayout.Label(lightning.LastDistance < 0f
            ? "Son çakma: yok"
            : $"Son çakma: {lightning.LastDistance:F0} m   " +
              $"ışık {lightning.Intensity:F2}   parlama {lightning.Glow:F2}");

        lightning.Held = GUILayout.Toggle(lightning.Held, "Çakmayı sabit yak");

        EndSection();
    }

    void DrawWind()
    {
        BeginSection("Rüzgâr");

        GUILayout.Label($"Şiddet {wind.Strength:F2}   Hız {wind.Velocity.magnitude:F1} m/s");

        // Kilit taban şiddeti ve yönü sabitler; dalgalanma üstünde çalışmaya devam
        // eder — bileşen kapatılmaz. Sürgüdeki 0.5, etrafında nefes alan bir 0.5'tir.
        bool nextLock = GUILayout.Toggle(windLocked, "Rüzgârı elle ayarla");
        if (nextLock != windLocked)
        {
            windLocked = nextLock;
            if (!windLocked) wind.ClearOverride();
        }

        using (new Disabled(!windLocked))
        {
            GUILayout.Label($"Şiddet {lockedWindStrength:F2}");
            lockedWindStrength = GUILayout.HorizontalSlider(lockedWindStrength, 0f, 1f);

            GUILayout.Label($"Yön {lockedWindAngle:F0}°");
            lockedWindAngle = GUILayout.HorizontalSlider(lockedWindAngle, 0f, 360f);
        }

        EndSection();
    }

    // (BULUT BÖLÜMLERİ SİLİNDİ — bulut sistemi baştan yazılıyor. Sürgü listesi ve
    // hangi bağın nereye gittiği `CLOUDS_REBUILD.md`'de duruyor; yeni sistem gelince
    // panel oradan yeniden kurulacak.)

    void DrawOverlays()
    {
        BeginSection("Neyi çiz");

        GUILayout.Label($"Görüş mesafesi {atmosphere.Visibility:F0} m");

        atmosphere.FogEnabled = GUILayout.Toggle(atmosphere.FogEnabled, "Yükseklik sisi");
        precipitation.enabled = GUILayout.Toggle(precipitation.enabled, "Yağmur ve kar");

        hud.enabled = GUILayout.Toggle(hud.enabled, "Performans göstergesi");
        climbHud.enabled = GUILayout.Toggle(climbHud.enabled, "Tırmanış göstergesi");

        // Bileşen değil NESNE: katman kapalı bir nesnede duruyor.
        GameObject lines = routeOverlay.gameObject;
        bool showLines = GUILayout.Toggle(lines.activeSelf, "Rota çizgileri");
        if (showLines != lines.activeSelf) lines.SetActive(showLines);

        EndSection();
    }

    /// GEÇİCİ ÖLÇÜM BÖLÜMÜ. Karın görsel yüzeyi GPU'da, çarpışma yüzeyi CPU'da
    /// hesaplanıyor; ikisi ayrıştığında belirti sessiz. Prob CPU yüzeyini turuncu
    /// işaretlerle çiziyor — işaretler karın görünen yüzeyine oturmuyorsa ayrışma var.
    ///
    /// 8b doğrulanınca bu bölüm ve `SnowCollisionProbe` silinir.
    void DrawSnowCollision()
    {
        BeginSection("Teşhis: kar çarpışması");

        // Bileşen değil NESNE açılıyor: prob kapalı bir nesnede duruyor (kurulum
        // sırasında bağlanmadan OnEnable'a girmesin diye) ve `enabled` orada işlemez.
        GameObject host = snowProbe.gameObject;
        bool next = GUILayout.Toggle(host.activeSelf, "CPU yüzeyini çiz");
        if (next != host.activeSelf) host.SetActive(next);

        if (host.activeSelf)
        {
            GUILayout.Label($"Zemin {snowProbe.GroundHeight:F2} m");
            GUILayout.Label($"Kar derinliği {snowProbe.SnowDepth:F2} m");
            GUILayout.Label($"Ayak {snowProbe.FeetHeight:F2} m");

            // Ayak, zeminin kar kadar üstünde durmalı. Fark büyüyorsa oyuncu ya karın
            // içine gömülüyor ya da üstünde asılı kalıyor.
            float error = snowProbe.FeetHeight - (snowProbe.GroundHeight + snowProbe.SnowDepth);
            GUILayout.Label($"Ayak sapması {error:+0.00;-0.00} m");
        }

        if (GUILayout.Button("Ayarları geri al")) host.SetActive(false);

        EndSection();
    }

    /// GUI.enabled'ı kapsam boyunca kapatan yardımcı
    readonly struct Disabled : IDisposable
    {
        readonly bool previous;

        public Disabled(bool disabled)
        {
            previous = GUI.enabled;
            GUI.enabled = previous && !disabled;
        }

        public void Dispose() => GUI.enabled = previous;
    }
}
