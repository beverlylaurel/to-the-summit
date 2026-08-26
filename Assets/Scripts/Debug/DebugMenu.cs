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
    [SerializeField] SnowManager snowManager;
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


    /// 0 kapalı · 1 bant · 2 opaklık · 3 perde yok. Şüphelilerin tamamı tek yerde:
    /// "perde bir şey yapıyor mu", "doğru yerde mi", "gücü doğru mu" üç ayrı soru ve
    /// üçü dışarıdan aynı görünüyor.

    bool weatherLocked;

    /// KAR ORANI: yağışın ne kadarı kar. 1 kar, 0 yağmur.
    ///
    /// Eskiden bu sürgü yağışı açıp SICAKLIĞI donmanın altına indiriyordu ve
    /// kar/yağmur kararını `SnowfallController`'ın histerezisi veriyordu.
    /// Histerezis kaldırılınca sürgü yalancı oldu: 0 yapılınca hiçbir şeye
    /// dokunmuyor, kar yağmaya devam ediyordu.
    ///
    /// Artık doğrudan `SnowManager.SnowFraction01`'i sürüyor — sıcaklık
    /// karışmıyor, karar tek yerden geliyor.
    float lockedSnowFraction = 1f;

    /// Teşhis: rüzgâr taşınımı ve gölgesi ayrı ayrı kapatılabiliyor.
    bool windTransportOff;
    bool windShadowOff;

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

    /// Ölçülen ortalama derinlik (mm). `MeanRhoN` normalize yoğunluk;
    /// `SnowDensity` ile aynı eşleme (50–550 kg/m³).
    static float SnowDepthMm(SnowManager mgr)
    {
        float rho = Mathf.Lerp(50f, 550f, mgr.MeanRhoN);
        return mgr.MeanSwe * 1000000f / Mathf.Max(rho, 1f);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame) Toggle();

        if (weatherLocked) weatherDriver.IntensityOverride = lockedPrecipitation;

        // Kar oranı kilitten BAĞIMSIZ sürülüyor: yağış kilidi kapalıyken de
        // "şu an kar mı yağmur mu" denenebilmeli.
        if (snowManager != null) snowManager.SnowFraction01 = lockedSnowFraction;
        if (windLocked) wind.ApplyOverride(lockedWindStrength, lockedWindAngle);

        // KİLİT AÇILINCA DA KOŞMALI: geçersiz kılmayı temizleyen taraf burası.
        // `if (weatherLocked)` içine konsaydı kilit kapandığında kar sonsuza
        // kadar dayatılmış kalırdı.
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
            // TEK SÜRGÜ. Eskiden ayrı bir "kar şiddeti" vardı ve yağışı açıp
            // sıcaklığı donmanın altına indiriyordu; kar/yağmur kararını
            // sıcaklık histerezisi veriyordu.
            //
            // Yağış sıcaklıktan koparıldığında o sürgü YALANCI oldu: kar 0
            // yapılınca `IntensityOverride`'a hiç dokunmuyor, yağış sürgüsünün
            // yazdığı değer kalıyor ve kar yağmaya devam ediyordu. Kullanıcı
            // ekran görüntüsüyle bildirdi.
            //
            // Artık yağış varsa kar var; ikinci bir sürgünün anlatacağı bir şey
            // yok.
            GUILayout.Label($"Yağış şiddeti {lockedPrecipitation:F2}");
            lockedPrecipitation = GUILayout.HorizontalSlider(lockedPrecipitation, 0f, 1f);

            // KAR ORANI, KAR ŞİDDETİ DEĞİL. Şiddeti yukarıdaki sürgü veriyor;
            // bu sürgü o yağışın kaça kar kaça yağmur bölüneceğini söylüyor.
            // İkisi ayrı soru: "ne kadar yağıyor" ve "ne yağıyor".
            // ANAHTAR, SÜRGÜ DEĞİL. Eşik 0.5; "karışık" diye bir durum yok,
            // ya kar yağar ya yağmur (`SnowfallController`).
            GUILayout.Label($"Yağış türü: {(lockedSnowFraction >= 0.5f ? "KAR" : "YAĞMUR")}" +
                            $"   (sürgü {lockedSnowFraction:F2}, eşik 0.50)   " + SnowStatus());
            lockedSnowFraction = GUILayout.HorizontalSlider(lockedSnowFraction, 0f, 1f);
            GUILayout.Label(SnowStateStatus());

            // HALKA SINIRI TEŞHİSİ. Halkalar ±8, ±16, ±32, ±64 m. Kusur bir
            // halkanın kenarındaysa halka sayısı azalınca kusur da o sınırla
            // birlikte içeri kayar.
            SnowManager mgr = snowManager;

            // ---------------------------------------------- KAR SINAMA ORTAMI
            //
            // Birikme, oturma ve iz saat mertebesinde işliyor; gerçek zamanda
            // beklemek dakikalar alıyor ve hata aramayı imkânsız kılıyor.
            //
            // Zaman çarpanı SAHTE DURUM YAZMIYOR: aynı fizik daha hızlı koşuyor
            // (`_DeltaTimeEff` ölçekleniyor). Doldurma düğmeleri ise durumu
            // doğrudan yazıyor — "şu derinlikte kar varken iz nasıl görünüyor"
            // sorusunu beklemeden sormak için.
            if (mgr != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label("— KAR SINAMASI —");

                GUILayout.Label($"Simülasyon hızı ×{mgr.SimTimeScale:F0}   " +
                                (mgr.SimTimeScale > 1.5f ? "HIZLANDIRILMIŞ" : "gerçek zaman"));

                mgr.SimTimeScale = Mathf.Round(
                    GUILayout.HorizontalSlider(mgr.SimTimeScale, 1f, 500f));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Kar yok"))    mgr.FillSnowDepth(0f);
                if (GUILayout.Button("1 cm"))       mgr.FillSnowDepth(0.01f);
                if (GUILayout.Button("5 cm"))       mgr.FillSnowDepth(0.05f);
                if (GUILayout.Button("20 cm"))      mgr.FillSnowDepth(0.20f);
                if (GUILayout.Button("50 cm"))      mgr.FillSnowDepth(0.50f);
                GUILayout.EndHorizontal();

                GUILayout.Label($"Ölçülen: örtü {SnowRuntimeState.GroundCoverage01:F3}   " +
                                $"SWE {mgr.MeanSwe * 1000f:F2} mm   " +
                                $"derinlik {SnowDepthMm(mgr):F1} mm");

                // İZ HAM GÖRÜNÜMÜ — TEK CEVAPLI TEST.
                //
                // Işıklandırma, paralaks ve relief ışını KAPALI; ekrana yalnız
                // iz dokusunun kendi değeri basılıyor (kırmızı = derinlik).
                // Zigzag bu görünümde de varsa kaynak VERİDE, yoksa ÇİZİMDE.
                // İki hipotezi ayıran başka bir gözlem yok.
                bool hamOnce = izHam;
                izHam = GUILayout.Toggle(izHam, "İzin ham hâli (ışıksız, paralakssız)");
                if (izHam != hamOnce) Shader.SetGlobalFloat(SnowDebugDentId, izHam ? 1f : 0f);

                bool normalOnce = normalHam;
                normalHam = GUILayout.Toggle(normalHam,
                    "Yüzey normali (K=NdotL, Y=wrap, M=N.y)");

                if (normalHam != normalOnce)
                    Shader.SetGlobalFloat(SnowDebugNormalId, normalHam ? 1f : 0f);


                GUILayout.Label("Kar ışıklandırması — terimleri kapat:");

                Anahtar(ref dbgNoSpec,       DbgNoSpecId,       "  speküler");
                Anahtar(ref dbgNoSparkle,    DbgNoSparkleId,    "  parıltı");
                Anahtar(ref dbgNoTrans,      DbgNoTransId,      "  arkadan sızma");
                Anahtar(ref dbgNoWrap,       DbgNoWrapId,       "  sarmalı diffuse (düz NdotL)");
                Anahtar(ref dbgNoAO,         DbgNoAOId,         "  ortam örtmesi (AO)");
                Anahtar(ref dbgNoBounce,     DbgNoBounceId,     "  kar-kar yatay transferi");
                Anahtar(ref dbgNoShadowTint, DbgNoShadowTintId, "  gölge rengi");

                GUILayout.Label("Kar yüzey rölyefi — terimleri kapat:");

                Anahtar(ref dbgNoFbm,      DbgNoFbmId,      "  fBm tabanı");
                Anahtar(ref dbgNoRipple,   DbgNoRippleId,   "  ripple");
                Anahtar(ref dbgNoSastrugi, DbgNoSastrugiId, "  sastrugi");
                Anahtar(ref dbgNoMicro,    DbgNoMicroId,    "  mikro tane");
                Anahtar(ref dbgNoLod,      DbgNoLodId,      "  oktav LOD eşiği");


                if (GUILayout.Button("Ayarları geri al (sınama)"))
                {
                    mgr.SimTimeScale = 1f;
                    mgr.RefillRegion();
                    izHam = false;
                    Shader.SetGlobalFloat(SnowDebugDentId, 0f);
                    normalHam = false;
                    Shader.SetGlobalFloat(SnowDebugNormalId, 0f);


                    dbgNoSpec = dbgNoSparkle = dbgNoTrans = dbgNoWrap = false;
                    dbgNoAO = dbgNoBounce = dbgNoShadowTint = false;
                    Shader.SetGlobalFloat(DbgNoSpecId, 0f);
                    Shader.SetGlobalFloat(DbgNoSparkleId, 0f);
                    Shader.SetGlobalFloat(DbgNoTransId, 0f);
                    Shader.SetGlobalFloat(DbgNoWrapId, 0f);
                    Shader.SetGlobalFloat(DbgNoAOId, 0f);
                    Shader.SetGlobalFloat(DbgNoBounceId, 0f);
                    Shader.SetGlobalFloat(DbgNoShadowTintId, 0f);

                    dbgNoFbm = dbgNoRipple = dbgNoSastrugi = false;
                    dbgNoMicro = dbgNoLod = false;
                    Shader.SetGlobalFloat(DbgNoFbmId, 0f);
                    Shader.SetGlobalFloat(DbgNoRippleId, 0f);
                    Shader.SetGlobalFloat(DbgNoSastrugiId, 0f);
                    Shader.SetGlobalFloat(DbgNoMicroId, 0f);
                    Shader.SetGlobalFloat(DbgNoLodId, 0f);

                }

                GUILayout.Space(6f);
            }

            if (mgr != null)
            {
                bool nextWt = GUILayout.Toggle(windTransportOff, "Rüzgâr taşınımını kapat (teşhis)");
                if (nextWt != windTransportOff)
                {
                    windTransportOff = nextWt;
                    mgr.WindTransportOff = windTransportOff;
                }

                bool nextWs = GUILayout.Toggle(windShadowOff, "Rüzgâr gölgesini kapat (teşhis)");
                if (nextWs != windShadowOff)
                {
                    windShadowOff = nextWs;
                    mgr.WindShadowOff = windShadowOff;
                }
            }
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

    /// İz ham görünümü açık mı (`_SnowDebugDent`).
    bool izHam;

    static readonly int SnowDebugDentId = Shader.PropertyToID("_SnowDebugDent");



    /// Yüzey normali teşhis görünümü açık mı (`_SnowDebugNormal`).
    bool normalHam;

    static readonly int SnowDebugNormalId = Shader.PropertyToID("_SnowDebugNormal");

    /// İZ KENARINDAKİ BASAMAĞIN KAYNAĞINI AYIRAN ANAHTARLAR.
    ///
    /// Basamak üç tur yanlış yerde arandı — yumuşatma çekirdeğinin
    /// altörneklemesi, kenar gürültüsünün bloklu bileşeni, bilinear
    /// filtrelemenin türev süreksizliği. Üçü de gerçek kusurdu, üçü de
    /// düzeltildi, basamak durdu. Şüphelilerin TAMAMI aynı anda kapatılabilir
    /// olmalı ki sorumlu tek turda bulunsun.

    /// KAR IŞIKLANDIRMASININ HER TERİMİ. Alçak güneşte keskin kenarlı
    /// adacıklar bildirildi; maske, cavity, bulut gölgesi, gölge haritası ve
    /// geometri sırayla elendi. Terimlerin TAMAMI aynı anda kapatılabilir.
    bool dbgNoSpec, dbgNoSparkle, dbgNoTrans, dbgNoWrap,
         dbgNoAO, dbgNoBounce, dbgNoShadowTint;

    /// Kar yüzey rölyefinin terimleri ve oktav LOD eşiği.
    bool dbgNoFbm, dbgNoRipple, dbgNoSastrugi, dbgNoMicro, dbgNoLod;

    static readonly int DbgNoFbmId      = Shader.PropertyToID("_SnowDbgNoFbm");
    static readonly int DbgNoRippleId   = Shader.PropertyToID("_SnowDbgNoRipple");
    static readonly int DbgNoSastrugiId = Shader.PropertyToID("_SnowDbgNoSastrugi");
    static readonly int DbgNoMicroId    = Shader.PropertyToID("_SnowDbgNoMicro");
    static readonly int DbgNoLodId      = Shader.PropertyToID("_SnowDbgNoLod");

    static readonly int DbgNoSpecId       = Shader.PropertyToID("_SnowDbgNoSpec");
    static readonly int DbgNoSparkleId    = Shader.PropertyToID("_SnowDbgNoSparkle");
    static readonly int DbgNoTransId      = Shader.PropertyToID("_SnowDbgNoTrans");
    static readonly int DbgNoWrapId       = Shader.PropertyToID("_SnowDbgNoWrap");
    static readonly int DbgNoAOId         = Shader.PropertyToID("_SnowDbgNoAO");
    static readonly int DbgNoBounceId     = Shader.PropertyToID("_SnowDbgNoBounce");
    static readonly int DbgNoShadowTintId = Shader.PropertyToID("_SnowDbgNoShadowTint");



    /// Bir anahtarı çizer ve değiştiyse shader'a yazar.
    static bool Anahtar(ref bool durum, int id, string etiket)
    {
        bool once = durum;
        durum = GUILayout.Toggle(durum, etiket);

        if (durum != once) Shader.SetGlobalFloat(id, durum ? 1f : 0f);

        return durum;
    }

    string SnowStatus()
    {
        if (!SnowRuntimeState.IsSnowing) return "kar yok";

        return snowfall != null
            ? $"yağıyor, {snowfall.AliveFlakes} tane"
            : "yağıyor";
    }

    /// DEVİR NOKTASINDAKİ BASAMAK.
    ///
    static float Rho(float rhoN) => Mathf.Lerp(50f, 550f, Mathf.Clamp01(rhoN));

    static float Depth(float swe, float rhoN) =>
        swe < 0f ? 0f : swe * 1000f / Mathf.Max(Rho(rhoN), 1f);

    /// KAR DURUMU OKUNABİLİR OLMALI.
    ///
    /// "Kar yok" belirtisi zincirin herhangi bir halkasında kopabilir: kar
    /// yağmıyor, sıcaklık yüksek, ya da doku boş. Üçü de ekrandan aynı
    /// görünüyor. Bu satır üçünü sayıyla ayırıyor.
    ///
    /// KAR İRTİFAYA BAĞLI DEĞİL. Yükseklikten türeyen kar çizgisi kaldırıldı;
    /// kar yağarsa tutar. Yüksekte karın çok olması sıcaklıktan geliyor.
    string SnowStateStatus()
    {
        float rhoN = Shader.GetGlobalFloat("_FallbackRhoN");
        float rho = Mathf.Lerp(50f, 550f, Mathf.Clamp01(rhoN));

        // `GroundCoverage01` durum dokusunun geri okuması: kar varsa 1'e
        // yakın, doku boşsa 0.
        return $"yağıyor mu {(SnowRuntimeState.IsSnowing ? "EVET" : "hayır")}   " +
               $"şiddet {SnowRuntimeState.SnowfallIntensity01:F2}   " +
               $"yeni kar ρ {rho:F0}   " +
               $"DOKUDA {SnowRuntimeState.GroundCoverage01:F2}   " +
               $"gevşek {SnowRuntimeState.LooseSnowFraction:F2}";
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
