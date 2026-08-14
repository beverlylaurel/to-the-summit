using System;
using UnityEngine;
using UnityEngine.InputSystem;

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

    const float PanelWidth = 960f;
    const float ColumnWidth = 300f;
    const float Margin = 24f;

    /// Oturum OYUNUN KENDİ HIZINDA ve yürüyerek başlar. Bir dönem serbest uçuş ve yüz
    /// kat hız açık başlıyordu — arazi büyükken her açılışta uzak noktaya gitmek
    /// gerekiyordu. Artık mesafe algısı ve bisiklet sürüşü doğru hissedilsin diye
    /// varsayılan gerçek hız; ikisi de F1 panelinde açık duruyor.
    const float StartSpeedMultiplier = 1f;

    float speedMultiplier = StartSpeedMultiplier;
    bool freeFly;

    bool weatherLocked;
    float lockedPrecipitation = 0.6f;
    float lockedSnowiness;

    bool windLocked;
    float lockedWindStrength = 0.5f;
    float lockedWindAngle;

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
        RouteOverlay routeOverlayRef)
    {
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
            || cursorLock == null || snowProbe == null || routeOverlay == null)
            throw new InvalidOperationException($"{nameof(DebugMenu)}: bağımlılıklar atanmadı.");

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
        DrawClouds();
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
    void BikeHeight()
    {
        var bike = UnityEngine.Object.FindAnyObjectByType<BikeController>();
        if (bike == null) return;

        var box = bike.GetComponentInChildren<Renderer>();
        if (box == null) return;

        // IŞIN BİSİKLETİN ALTINDAN başlıyor. Üstünden atıldığında bisikletin kendi
        // çarpışma kapsülüne çarpıp "zeminden -65 cm" gibi anlamsız bir sayı veriyordu.
        float bottom = box.bounds.min.y;
        var ray = new Ray(new Vector3(box.bounds.center.x, bottom - 0.02f, box.bounds.center.z),
            Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            GUILayout.Label("Bisiklet: zemin bulunamadı");
            return;
        }

        float gap = bottom - 0.02f - hit.point.y;
        float elevation = Mathf.Asin(Mathf.Clamp(time.SunHeight, -1f, 1f)) * Mathf.Rad2Deg;
        float slip = Mathf.Abs(elevation) > 0.5f
            ? gap / Mathf.Tan(Mathf.Abs(elevation) * Mathf.Deg2Rad) : 0f;

        GUILayout.Label($"Bisiklet zeminden {gap * 100f:F1} cm"
                        + $"   güneş {elevation:F1}°   gölge kayması {slip:F2} m");

        // ÜÇ YÜZEY AYRI AYRI. Bisiklet çarpışmaya oturuyor, göz görsel kar yüzeyini
        // görüyor; ikisi arasında fark varsa nesne havada ya da gömülü görünüyor.
        // Kapsülün tabanı da yazılıyor çünkü fizik onu zemine oturtuyor, modeli değil.
        var capsule = bike.GetComponent<CharacterController>();
        float capsuleBottom = bike.transform.position.y + capsule.center.y
                            - capsule.height * 0.5f;

        GUILayout.Label($"  çarpışma {hit.point.y:F2}   model altı {bottom:F2}"
                        + $"   kapsül altı {capsuleBottom:F2}"
                        + $"   kar {SnowDepth(bike.transform.position) * 100f:F0} cm");
    }

    /// Bisikletin durduğu noktadaki kar derinliği. Kar yüzeyi ayrı bir bileşende
    /// duruyor; yoksa sıfır okunuyor.
    float SnowDepth(Vector3 point)
    {
        var snow = UnityEngine.Object.FindAnyObjectByType<SnowSurface>();
        return snow != null ? snow.DepthAt(point) : 0f;
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

    void DrawMovement()
    {
        BeginSection("Hareket");

        GUILayout.Label($"Hız çarpanı {speedMultiplier:F0}×");

        BikeHeight();

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

        // TEŞHİS — güneşin ışık zinciri. Disk ve zemin aynı kaynaktan besleniyor;
        // hangisinin koptuğunu ayırmak için dördü birden görünür.
        var sc = time.CurrentSunColor;
        GUILayout.Label($"Yükseklik {Mathf.Asin(Mathf.Clamp(time.SunHeight, -1f, 1f)) * Mathf.Rad2Deg:F2}°"
                        + $"   Huzme {time.BeamLevel:F4}");
        GUILayout.Label($"Işık şiddeti {time.LightIntensity:F3}   Ay {time.MoonLevel:F4}");
        GUILayout.Label($"Güneş rengi {sc.r:F2} {sc.g:F2} {sc.b:F2}");

        // TEŞHİS — metal parçaların gece parlaması iki kaynaktan gelebiliyor: sahnenin
        // çevre ışığı ya da gökyüzünden pişen yansıma haritası. İkisi ayrı ayrı yazılıyor
        // ki hangisinin kararmadığı gözle değil sayıyla ayrılsın.
        Color ambient = RenderSettings.ambientLight;
        GUILayout.Label($"Çevre ışığı {ambient.r:F3} {ambient.g:F3} {ambient.b:F3}"
                        + $"   Yansıma {RenderSettings.reflectionIntensity:F2}");

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

    void DrawClouds()
    {
        BeginSection("Bulutlar");

        // TEŞHİS — bulutların gerçek süzülme hızı. Rüzgâr sıfırlanınca da hareket
        // ediyorlardı ve sebebi görünmüyordu: taban hız ayarı yer rüzgârını eziyordu.
        GUILayout.Label($"Süzülme {atmosphere.CloudSpeed:F1} m/s"
                        + $" = {atmosphere.CloudSpeed * 3.6f:F0} km/h");


        GUILayout.Label($"Kapsama %{atmosphere.Coverage * 100f:F0}   " +
                        $"taban {atmosphere.CloudBottom:F0} m");

        atmosphere.CoverageLocked = GUILayout.Toggle(atmosphere.CoverageLocked,
            "Bulut kapsamasını elle ayarla");

        using (new Disabled(!atmosphere.CoverageLocked))
        {
            GUILayout.Label($"Kapsama %{atmosphere.LockedCoverage * 100f:F0}");
            atmosphere.LockedCoverage = GUILayout.HorizontalSlider(atmosphere.LockedCoverage, 0f, 1f);

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Açık")) atmosphere.LockedCoverage = 0.12f;
                if (GUILayout.Button("Parçalı")) atmosphere.LockedCoverage = 0.5f;
                if (GUILayout.Button("Kapalı")) atmosphere.LockedCoverage = 0.92f;
            }
        }

        EndSection();
    }

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
