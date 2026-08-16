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

    /// TEŞHİS — GEÇİCİ. Arazi gölge haritasını okusun mu. Anahtar bir kez silinmişti;
    /// siyah zemin belirtisi için geri kondu. Ölçüldü: sis zinciri o pikselleri
    /// `renk × 1 + 0` ile geçiriyor (hacim probu zeminde kırmızı), yani siyah olan
    /// yüzeyin KENDİ aydınlatması. Gündüz var / gece yok + kamerayla gelen sabit
    /// mesafeli kenar, yönlü ışık gölgesini ve 60 m'lik gölge mesafesini işaret ediyor.
    bool terrainShadowReceive = true;

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

    /// SİS DENETİMİ — GEÇİCİ. Paketin hava perspektifi. Denetim boyunca kapatılıyor
    /// çünkü İKİNCİ bir saçılım kaynağı: `AtmosphericScattering.hlsl` onu yalnız
    /// GEOMETRİYE uyguluyor, göğe uygulamıyor ("This pass only handles geometry").
    /// Açık kalınca arazi macentanın üstüne paketin pusunu da alıyor ve gökten farklı
    /// bir değere oturuyor — ölçüldü: dağ soluk mor, gök doygun macenta. Araç "tek
    /// renk" vaat edip iki renk gösteriyordu; delik mi, ikinci kaynak mı ayırt edilemezdi.
    PhysicallyBasedSky pbrSky;
    bool aerialDefault;
    bool aerialOff;

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
        CursorLock cursorLockRef, SnowCollisionProbe snowProbeRef,
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

        if (!cloudVolume.profile.TryGet(out pbrSky))
            throw new InvalidOperationException($"{nameof(DebugMenu)}: profilde {nameof(PhysicallyBasedSky)} yok.");

        aerialDefault = pbrSky.atmosphericScattering.value;
        pbrSky.atmosphericScattering.overrideState = true;

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
        Shader.SetGlobalFloat(TerrainShadowId, terrainShadowReceive ? 1f : 0f);
        // PANELDEN BAĞIMSIZ. Bu satır bir dönem panel çizim kodunun içindeydi ve panel
        // KAPALIYKEN hiç çalışmıyordu: denetim açık olmasına rağmen paketin hava
        // perspektifi devrede kalıyor, o da yalnız GEOMETRİYE uygulandığı için dağ
        // macentanın üstüne bir de paketin pusunu alıyordu — gövde arka plandan açık
        // çıkıyor, siluette tek piksellik kontur kalıyordu. Araç bozuk sanıldı; bozuk
        // olan aracın çalıştırılma yeriydi.
        //
        // Kalıcı ayar değişmiyor: açılıştaki değer saklanıp geri yazılıyor.
        pbrSky.atmosphericScattering.value =
            (VolumetricFogFeature.FogAudit || aerialOff) ? false : aerialDefault;

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
        DrawFogDiagnostics();
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

    /// TEŞHİS — GEÇİCİ. Sis hacmi gerçekten çalışıyor mu. Gözle bakmak yetmiyor: hacim
    /// hiç dağıtılmasa da sahne "sisli" görünür, çünkü analitik kuyruk zaten çalışıyor.
    void DrawFogDiagnostics()
    {
        BeginSection("Teşhis: sis hacmi");

        Vector4 depth = VolumetricFogFeature.VolumeDepth;

        GUILayout.Label($"dağıtım: {VolumetricFogFeature.DispatchCount}");
        GUILayout.Label($"hacim {depth.x:F1} → {depth.y:F0} m · {depth.w:F0} dilim");
        GUILayout.Label($"gölge kodu: {(VolumetricFogFeature.ShadowKeywordOn ? "AÇIK" : "KAPALI")}");
        GUILayout.Label($"bulut gölgesi dokusu: {(VolumetricFogFeature.CookieBound ? "BAĞLI" : "YOK")}");

        Vector3 sh = VolumetricFogFeature.AmbientDC;
        Vector4 fc = VolumetricFogFeature.FogColor;
        GUILayout.Label($"hacim ortamı {sh.x:F4} {sh.y:F4} {sh.z:F4}");
        GUILayout.Label($"sis rengi    {fc.x:F4} {fc.y:F4} {fc.z:F4}");
        GUILayout.Label($"cookie matrisi: {(VolumetricFogFeature.CookieMatrixValid ? "GEÇERLİ" : "SIFIR")}");
        GUILayout.Label($"gök sisi: {(VolumetricFogFeature.SkyPassBound ? "KURULU" : "SHADER YOK")} · çizim {VolumetricFogFeature.SkyPassCount}");
        // SİS DENETİMİ — GEÇİCİ. Tek kutu, tek doğru görüntü: işaretliyken 40 m ötedeki
        // HER piksel dümdüz macenta olmak zorunda. Macenta olmayan leke = o pikseli çizen
        // shader sisi hiç uygulamıyor. Beğeni payı yok.
        VolumetricFogFeature.FogAudit =
            GUILayout.Toggle(VolumetricFogFeature.FogAudit, "SİS DENETİMİ (40 m · macenta)");

        // KATMAN PROBU — GEÇİCİ. "Sis uygulandı mı" değil, "KİM uyguladı" sorusu.
        VolumetricFogFeature.FogLayerProbe = GUILayout.Toggle(
            VolumetricFogFeature.FogLayerProbe,
            "KATMAN PROBU (yeşil arazi · kırmızı gök · mavi bulut)");

        // TEŞHİS — GEÇİCİ. Arazi gölge haritasını okumayı bıraksın. Siyah bölge
        // buna bağlıysa sorumlu gölge haritası, sis değil.
        terrainShadowReceive = GUILayout.Toggle(
            terrainShadowReceive, "Arazi gölge OKUSUN");

        // TEŞHİS — GEÇİCİ. Bulut birleştirmesindeki sis uygulaması.
        VolumetricFogFeature.FogCloudsDisabled = GUILayout.Toggle(
            VolumetricFogFeature.FogCloudsDisabled, "BULUT SİSİ KAPALI");

        // YÜZEY PROBU — GEÇİCİ. Sis atlanır, yüzeyin ham rengi 8 ile çarpılır.
        VolumetricFogFeature.FogSurfaceProbe = GUILayout.Toggle(
            VolumetricFogFeature.FogSurfaceProbe,
            "YÜZEY PROBU (sis atlanır · renk ×8)");

        // HACİM PROBU — GEÇİCİ. Hacim dokusunun okunan değeri.
        VolumetricFogFeature.FogVolumeProbe = GUILayout.Toggle(
            VolumetricFogFeature.FogVolumeProbe,
            "HACİM PROBU (kırmızı geçirgenlik · yeşil saçılım · siyah BOŞ)");

        // TEŞHİS — GEÇİCİ. Paketin KENDİ hava perspektifi. Bulut birleştirmesinde
        // `EvaluateAtmosphericScattering` ile uygulanıyor ve mesafeyle büyüyor; irtifa
        // arttıkça buluta olan mesafe de büyüdüğü için gece bulutları sıfıra doğru
        // çarpabiliyor. "Yükseldikçe bulutlar siyahlaşıyor" belirtisinin ilk şüphelisi.
        aerialOff = GUILayout.Toggle(aerialOff, "PAKET HAVA PERSPEKTİFİ KAPALI");



        // TEŞHİS — GEÇİCİ. HUD "görüş 145 m" derken kilometrelerce ötedeki dağ duruyor.
        // Shader'ın gerçekten kullandığı yoğunluk ile o görüşün gerektirdiği yoğunluk
        // yan yana basılıyor; hangisinin yalan söylediği tek bakışta çıkar.
        float density = Shader.GetGlobalFloat("_HeightFogDensity");
        float falloff = Shader.GetGlobalFloat("_HeightFogFalloff");
        float baseAlt = Shader.GetGlobalFloat("_HeightFogBase");
        float seaD = Shader.GetGlobalFloat("_FogSeaDensity");
        float seaF = Shader.GetGlobalFloat("_FogSeaFalloff");
        float freeD = Shader.GetGlobalFloat("_FogFreeDensity");
        float freeF = Shader.GetGlobalFloat("_FogFreeFalloff");
        float invH = Shader.GetGlobalFloat("_FogInversionHeight");
        float invW = Shader.GetGlobalFloat("_FogInversionWidth");

        float camY = Camera.main != null ? Camera.main.transform.position.y : 0f;
        float h = camY - baseAlt;

        float t = Mathf.Clamp01((h - (invH - invW)) / Mathf.Max(1f, 2f * invW));
        float lid = 1f - t * t * (3f - 2f * t);
        float local = density * Mathf.Exp(-falloff * h) * lid
                    + seaD * Mathf.Exp(-seaF * h)
                    + freeD * Mathf.Exp(-freeF * h);

        GUILayout.Label($"taban yoğunluk {density:E2} · sönme {falloff:E2}");
        GUILayout.Label($"kotta yoğunluk {local:E2}  (kot {h:F0} m)");
        GUILayout.Label($"bu yoğunluğun görüşü {(local > 1e-9f ? 3.912f / local : 0f):F0} m");

        VolumetricFogFeature.VolumeDisabled =
            GUILayout.Toggle(VolumetricFogFeature.VolumeDisabled, "Hacim KAPALI (eski sise dön)");

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
