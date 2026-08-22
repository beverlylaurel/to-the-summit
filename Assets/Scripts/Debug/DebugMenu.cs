using System;
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
    [SerializeField] TemperatureField temperature;
    [SerializeField] SnowfallRenderer snowfall;
    [SerializeField] PerformanceHud hud;
    [SerializeField] ClimbHud climbHud;
    [SerializeField] CursorLock cursorLock;
    [Tooltip("Rota çizgilerinin oyun görünümü katmanı.")]
    [SerializeField] RouteOverlay routeOverlay;
    [Tooltip("Bulut ayarlarını taşıyan Volume bileşeni.")]
    [SerializeField] Volume cloudVolume;

    [Tooltip("Bulut ayarlarini havadan suren bilesen; \"Havadan ayir\" bunu kapatiyor.")]
    [SerializeField] CloudWeatherDriver cloudDriver;

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

    static readonly int TerrainShadowId = Shader.PropertyToID("_TerrainShadowReceive");

    /// 0 kapalı · 1 bant · 2 opaklık · 3 perde yok. Şüphelilerin tamamı tek yerde:
    /// "perde bir şey yapıyor mu", "doğru yerde mi", "gücü doğru mu" üç ayrı soru ve
    /// üçü dışarıdan aynı görünüyor.

    bool weatherLocked;

    /// KAR YAĞIŞIN KENDİSİ, AYRI BİR SİSTEM DEĞİL. Sürgü yağışı açıp
    /// sıcaklığı donmanın altına indiriyor; "kar mı yağmur mu" kararını yine
    /// `SnowfallController`'ın histerezisi veriyor. Ayrı bir "kar şiddeti"
    /// kaynağı olsaydı yağışla çelişebilirdi.
    float lockedSnow;

    /// Sürgü açıkken dayatılan DENİZ SEVİYESİ sıcaklığı.
    ///
    /// Donma seviyesi = (deniz sv. + gündüz ısınması − fırtına soğuması) / 0.0065.
    /// −2 °C'de öğlen ve yağışsız uçta bile donma seviyesi −57 m çıkıyor, yani
    /// oyuncunun kotu ne olursa olsun kar. Sayı kâğıtta bu iki uçtan seçildi.
    const float SnowSeaLevelC = -2f;
    float lockedPrecipitation = 0.6f;

    bool windLocked;
    float lockedWindStrength = 0.5f;
    float lockedWindAngle;

    /// Bulut ayarları `cloudVolume.profile` üzerinden sürülüyor — asset'in kendisi değil,
    /// Volume'un çalışma zamanı KOPYASI. `sharedProfile`'a yazmak işe yaramıyor: sahnede
    /// başka bir bileşen `.profile`'a dokunduğu an Volume harmanlamayı kopyadan yapmaya
    /// başlıyor ve asset'e yazılan değer hiç okunmuyor (ölçüldü: profil 0.71, yığın 0.40).
    /// Açılıştaki değerler geri al düğmeleri için saklanıyor.
    VolumetricClouds clouds;

    /// Acilistaki degerler: her satirin ↺'u ve "Bulut ayarlarini geri al" buradan okuyor.
    /// Cizim aninda yakalanamaz — `CloudWeatherDriver` kapsama, yogunluk ve ruzgari her
    /// karede yaziyor, ilk cizimde okunan deger zaten surulmus olan olurdu.
    float coverageDefault;
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
        CursorLock cursorLockRef,
        RouteOverlay routeOverlayRef, Volume cloudVolumeRef, CloudWeatherDriver cloudDriverRef)
    {
        cloudVolume = cloudVolumeRef;
        cloudDriver = cloudDriverRef;
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
        routeOverlay = routeOverlayRef;
    }

    void OnEnable()
    {
        if (walker == null || flyer == null || weather == null || weatherDriver == null
            || wind == null || thunder == null || lightning == null || time == null
            || atmosphere == null
            || precipitation == null || hud == null || climbHud == null
            || cursorLock == null || routeOverlay == null
            || cloudVolume == null || cloudDriver == null)
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

    /// BAĞLAMA `Start`'TA, `OnEnable`'DA DEĞİL. Bileşenin kendi `OnEnable`'ı henüz
    /// çalışmamış olabiliyor ve Unity iki `OnEnable` arasında sıra garanti etmiyor.
    /// `Start` hepsinden sonra çalışır; sürücüler değerleri `Update`'te yazdığı için
    /// yakalanan varsayılan hâlâ havanın ezmediği hâl.
    void Start()
    {
        if (!cloudVolume.profile.TryGet(out clouds))
            throw new InvalidOperationException($"{nameof(DebugMenu)}: profilde {nameof(VolumetricClouds)} yok.");

        coverageDefault = clouds.cloudCoverage.value;

        // Parametrenin `overrideState`'i kapalıysa harmanlama onu atlıyor: sürgü profile
        // yazıyor ama yığına hiç geçmiyor.
        clouds.cloudCoverage.overrideState = true;

        detachFromWeather = !cloudDriver.enabled;
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
        // PANELDEN BAĞIMSIZ. Yorumun kendisi "yazılmazsa sıfır kalır ve arazi hiç gölge
        // almaz" diyordu ama satır panel çizim kodundaydı: panel KAPALIYKEN global hiç
        // yazılmıyor, sıfır kalıyor ve arazi gölgesiz çiziliyordu. Oyunun normal hâli
        // panel kapalı olduğu için bu, oynanışın tamamını etkiliyordu.
        Shader.SetGlobalFloat(TerrainShadowId, 1f);
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) Toggle();

        if (weatherLocked) weatherDriver.IntensityOverride = lockedPrecipitation;
        if (windLocked) wind.ApplyOverride(lockedWindStrength, lockedWindAngle);

        // KİLİT AÇILINCA DA KOŞMALI: geçersiz kılmayı temizleyen taraf burası.
        // `if (weatherLocked)` içine konsaydı kilit kapandığında kar sonsuza
        // kadar dayatılmış kalırdı.
        ApplySnowOverride();
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
    /// BULUT KAPSAMASI. Panelde kalan tek bulut ayarı; gerisi ayarlandı ve profile
    /// yazıldı, sürgü olarak durmalarına gerek yok.
    ///
    /// "Havadan ayır" sürgünün çalışması için şart: `CloudWeatherDriver` kapsamayı her
    /// karede fırtınadan yazıyor, sürücü kapatılmazsa sürgünün yazdığı değer bir sonraki
    /// karede eziliyor.
    void DrawClouds()
    {
        BeginSection("Bulut");

        bool detach = GUILayout.Toggle(detachFromWeather, "Havadan ayır (elle ayar)");
        if (detach != detachFromWeather)
        {
            detachFromWeather = detach;
            cloudDriver.enabled = !detach;
        }

        clouds.cloudCoverage.value = CloudRow("Kapsama", clouds.cloudCoverage.value,
            coverageDefault, clouds.cloudCoverage.min, clouds.cloudCoverage.max, "F2");

        EndSection();
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

    string clockInput = "19:11";

    /// Bir gün 24 saat, yani bir dakika `1/1440`. Saat kaydırılırken sarma yapılıyor:
    /// 00:00'ın bir dakika öncesi 23:59.
    void StepMinutes(float minutes)
    {
        time.SetNormalized(time.Normalized + minutes / 1440f);
    }

    /// "19:11" ya da "19.11" kabul ediliyor. Saat 0-23, dakika 0-59 dışındaysa
    /// yazılan değer yok sayılıyor — sessizce yanlış bir saate atlamasın.
    static bool TryParseClock(string text, out float normalized)
    {
        normalized = 0f;

        string[] parts = text.Split(':', '.');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out int hours) || !int.TryParse(parts[1], out int minutes)) return false;
        if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59) return false;

        normalized = (hours + minutes / 60f) / 24f;
        return true;
    }

    void DrawTimeOfDay()
    {
        BeginSection("Günün saati");

        GUILayout.Label($"Saat {time.Clock}");

        float value = time.Normalized;
        float next = GUILayout.HorizontalSlider(value, 0f, 1f);
        if (!Mathf.Approximately(next, value)) time.SetNormalized(next);

        // SÜRGÜ DAKİK DEĞİL: bir ekran pikseli ~5 dakikaya denk geliyor ve ölçüm
        // alırken belirli bir dakikaya oturmak imkânsızdı. Yazıyla giriş ve dakika
        // adımı bunun için.
        using (new GUILayout.HorizontalScope())
        {
            GUILayout.Label("Saat gir", GUILayout.Width(56f));
            clockInput = GUILayout.TextField(clockInput, 5, GUILayout.Width(50f));

            if (GUILayout.Button("Git", GUILayout.Width(36f)) && TryParseClock(clockInput, out float typed))
                time.SetNormalized(typed);

            if (GUILayout.Button("−1 dk", GUILayout.Width(48f))) StepMinutes(-1f);
            if (GUILayout.Button("+1 dk", GUILayout.Width(48f))) StepMinutes(1f);
        }

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

        GUILayout.Label($"Yağış {weather.Precipitation:F2}");

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
            }
        }

        using (new Disabled(!weatherLocked))
        {
            GUILayout.Label($"Yağış şiddeti {lockedPrecipitation:F2}");
            lockedPrecipitation = GUILayout.HorizontalSlider(lockedPrecipitation, 0f, 1f);

            GUILayout.Label($"Kar şiddeti {lockedSnow:F2}   " + SnowStatus());
            lockedSnow = GUILayout.HorizontalSlider(lockedSnow, 0f, 1f);
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
            GUILayout.Label($"Yağmur tavanı {weatherDriver.RainCeiling:F0} m");
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

    string SnowStatus()
    {
        if (!SnowRuntimeState.IsSnowing) return "kar yok";

        return snowfall != null
            ? $"yağıyor, {snowfall.AliveFlakes} tane"
            : "yağıyor";
    }

    /// SÜRGÜ GERÇEK SİSTEMLERE YAZIYOR, KAR SİSTEMİNE DEĞİL.
    ///
    /// Yağış `AltitudeWeatherDriver`'a, soğuk `TemperatureField`'a gidiyor.
    /// Kar sistemi ikisini de köprüden okuyor, ayrıca dayatma almıyor. Böylece
    /// HUD'daki sıcaklık, donma seviyesi, kar çizgisi ve yağan kar tek bir
    /// durumdan türüyor — `CLAUDE.md` → Atmosfer tutarlılığı.
    void ApplySnowOverride()
    {
        if (temperature == null) return;

        bool wantSnow = weatherLocked && lockedSnow > 0.001f;

        if (wantSnow)
        {
            // Kar sürgüsü yağış şiddetini de o an devralıyor; iki sürgü aynı
            // sayıyı sürseydi hangisinin kazandığı ekrandan anlaşılmazdı.
            weatherDriver.IntensityOverride = lockedSnow;
            temperature.ApplyOverride(SnowSeaLevelC);
        }
        else if (temperature.HasOverride)
        {
            temperature.ClearOverride();
        }
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
