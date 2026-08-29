using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// The test panel opened with F1. Esc is reserved for the game's own menu.
/// The notion of a "debug mode" does not leak into the systems; the locks use the component's
/// OWN test switch. Disabling a component left a frozen state for the consumers that kept reading
/// it, and a single weather state split into two channels.
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
    [Tooltip("The game-view layer of the route lines.")]
    [SerializeField] RouteOverlay routeOverlay;
    [Tooltip("The Volume component carrying the cloud settings.")]
    [SerializeField] Volume cloudVolume;

    [Tooltip("Bulut ayarlarini havadan suren bilesen; \"Havadan ayir\" bunu kapatiyor.")]
    [SerializeField] CloudWeatherDriver cloudDriver;

    const float PanelWidth = 960f;
    const float ColumnWidth = 300f;
    const float Margin = 24f;

    /// A session starts AT THE GAME'S OWN SPEED and on foot. For a while it started with free
    /// flight and a hundredfold speed on — with a large terrain you had to travel to a distant
    /// point on every launch. Now the default is the real speed so that the sense of distance and
    /// the bike ride feel right; both are still available in the F1 panel.
    const float StartSpeedMultiplier = 1f;

    float speedMultiplier = StartSpeedMultiplier;
    bool freeFly;


    /// 0 off · 1 band · 2 opacity · 3 no curtain. Every suspect in one place:
    /// "is the curtain doing anything", "is it in the right place", "is its strength right" are
    /// three separate questions and all three look the same from outside.

    bool weatherLocked;

    /// SNOW FRACTION: how much of the precipitation is snow. 1 snow, 0 rain.
    ///
    /// This slider used to turn on the precipitation and push the TEMPERATURE below
    /// freezing, and the snow/rain decision came from `SnowfallController`'s hysteresis.
    /// When the hysteresis was removed the slider became a liar: set to 0 it touched
    /// nothing and the snow kept falling.
    ///
    /// It now drives `SnowManager.SnowFraction01` directly — temperature is not
    /// involved and the decision comes from one place.
    float lockedSnowFraction = 1f;

    /// Diagnostic: wind transport and its shadow can be switched off separately.
    bool windTransportOff;
    bool windShadowOff;

   float lockedPrecipitation = 0.6f;

    bool windLocked;
    float lockedWindStrength = 0.5f;
    float lockedWindAngle;

    /// SEA ISOLATION SWITCHES — EVERY SUSPECT AT ONCE.
    ///
    /// The globals already existed in `SeaShaderIDs` and the shader already read
    /// them, but NOBODY DROVE THEM: dead code with no way to reach it. "The sea is
    /// too white" has four candidate sources and from outside they look identical;
    /// adding one switch per round costs one Play session each, all four at once
    /// costs one.
    bool seaNoWaves, seaNoShallow, seaNoFoam, seaNoRefraction;

    /// Removes the sea surface entirely: whatever is left on screen is not the sea.
    bool seaNoSurface;

    /// The cloud settings are driven through `cloudVolume.profile` — the Volume's runtime COPY,
    /// not the asset itself. Writing to `sharedProfile` does not work: the moment another
    /// component in the scene touches `.profile`, the Volume starts blending from the copy and the
    /// value written to the asset is never read (measured: profile 0.71, stack 0.40).
    /// The launch values are kept for the revert buttons.
    VolumetricClouds clouds;

    /// The launch values: every row's ↺ and "Revert cloud settings" read from here.
    /// They cannot be captured at draw time — `CloudWeatherDriver` writes coverage, density and
    /// wind every frame, so the value read on the first draw would already be the driven one.
    float coverageDefault;
    bool detachFromWeather;

    bool open;
    float timeScale = 1f;

    Vector2 scroll;
    // GUIStyle IS NOT SERIALIZED. When Unity tries to save it the font reference inside
    // stays invalid after a recompile and a "Deleting invalid font reference" warning is
    // printed on every reload. The style is built at every use anyway, there is nothing
    // to store.
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
            throw new InvalidOperationException($"{nameof(DebugMenu)}: dependencies are not assigned.");

        // The speed multiplier was applied only while the panel was being drawn; if the panel was
        // never opened the initial value never took effect either.
        walker.SpeedMultiplier = speedMultiplier;
        flyer.SpeedMultiplier = speedMultiplier;

        walker.enabled = !freeFly;
        flyer.enabled = freeFly;
        time.Paused = true;
        weatherDriver.Instant = true;

        open = false;
    }

    /// BINDING IS IN `Start`, NOT `OnEnable`. The component's own `OnEnable` may not have run yet
    /// and Unity guarantees no order between two `OnEnable`s. `Start` runs after all of them; and
    /// because the drivers write their values in `Update`, the captured default is still the state
    /// the weather has not overwritten.
    void Start()
    {
        if (!cloudVolume.profile.TryGet(out clouds))
            throw new InvalidOperationException($"{nameof(DebugMenu)}: profilde {nameof(VolumetricClouds)} yok.");

        coverageDefault = clouds.cloudCoverage.value;

        // If the parameter's `overrideState` is off, blending skips it: the slider writes to the
        // profile but nothing ever reaches the stack.
        clouds.cloudCoverage.overrideState = true;

        detachFromWeather = !cloudDriver.enabled;
    }

    void OnDisable()
    {
        Time.timeScale = 1f;

        // If the component was disabled while the panel was open the cursor would stay free
        if (open) cursorLock.Restore();
        open = false;
    }

    /// Measured mean depth (mm). `MeanRhoN` is the normalized density;
    /// the same mapping as `SnowDensity` (50–550 kg/m³).
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

        // The snow fraction is driven INDEPENDENTLY of the lock: it must be possible to try
        // "is it snow or rain right now" while the precipitation lock is off too.
        if (snowManager != null) snowManager.SnowFraction01 = lockedSnowFraction;
        if (windLocked) wind.ApplyOverride(lockedWindStrength, lockedWindAngle);

        // IT HAS TO RUN WHEN THE LOCK OPENS TOO: this is the side that clears the override.
        // Put inside `if (weatherLocked)`, snow would stay forced forever once the lock
        // was switched off.
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

        GUILayout.Label("Test panel — F1 to exit", title);

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
        DrawSea();
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

    /// Opens a titled box; close it with EndSection
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

    /// CLOUD COVERAGE. The only cloud setting left in the panel; the rest were tuned and written
    /// to the profile, they do not need to stay as sliders.
    ///
    /// "Detach from weather" is required for the slider to work: `CloudWeatherDriver` writes the
    /// coverage from the storm every frame, and unless the driver is switched off the value the
    /// slider writes is overwritten on the next frame.
    void DrawClouds()
    {
        BeginSection("Bulut");

        bool detach = GUILayout.Toggle(detachFromWeather, "Detach from weather (manual)");
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

        GUILayout.Label($"Speed multiplier {speedMultiplier:F0}×");

        // The slider is quadratic: precise at small values, reaching 100× at the end
        float normalized = Mathf.Sqrt((speedMultiplier - 1f) / 99f);
        normalized = GUILayout.HorizontalSlider(normalized, 0f, 1f);
        speedMultiplier = 1f + normalized * normalized * 99f;

        walker.SpeedMultiplier = speedMultiplier;
        flyer.SpeedMultiplier = speedMultiplier;

        bool nextFreeFly = GUILayout.Toggle(freeFly, "Free flight (Q/E)");
        if (nextFreeFly != freeFly)
        {
            freeFly = nextFreeFly;
            walker.enabled = !freeFly;
            flyer.enabled = freeFly;
        }

        EndSection();
    }

    string clockInput = "19:11";

    /// A day is 24 hours, so one minute is `1/1440`. The clock wraps while being shifted:
    /// one minute before 00:00 is 23:59.
    void StepMinutes(float minutes)
    {
        time.SetNormalized(time.Normalized + minutes / 1440f);
    }

    /// Both "19:11" and "19.11" are accepted. If the hour is outside 0-23 or the minute outside
    /// 0-59 the entered value is ignored — so it does not silently jump to a wrong time.
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
        BeginSection("Time of day");

        GUILayout.Label($"Saat {time.Clock}");

        float value = time.Normalized;
        float next = GUILayout.HorizontalSlider(value, 0f, 1f);
        if (!Mathf.Approximately(next, value)) time.SetNormalized(next);

        // THE SLIDER IS NOT PRECISE: one screen pixel is about 5 minutes and landing on a
        // specific minute while taking a measurement was impossible. Text entry and the
        // minute step are there for that.
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
            if (GUILayout.Button("Dawn")) time.SetNormalized(0.25f);
            if (GUILayout.Button("Noon")) time.SetNormalized(0.5f);
            if (GUILayout.Button("Sunset")) time.SetNormalized(0.75f);
            if (GUILayout.Button("Gece")) time.SetNormalized(0f);
        }

        time.Paused = GUILayout.Toggle(time.Paused, "Saati durdur");

        GUILayout.Space(6f);
        GUILayout.Label($"Game speed {timeScale:F2}×");
        timeScale = GUILayout.HorizontalSlider(timeScale, 0f, 4f);
        Time.timeScale = timeScale;

        if (GUILayout.Button("Reset speed to normal")) timeScale = 1f;

        EndSection();
    }

    void DrawWeather()
    {
        BeginSection("Hava durumu");

        GUILayout.Label($"Precipitation {weather.Precipitation:F2}");

        // The driver is NOT DISABLED, its target is supplied from outside. Disabled,
        // `StormIntensity` and `ClearWindow` froze but the atmosphere kept reading them: while the
        // slider drove precipitation, visibility and colour, the cloud coverage stayed at the
        // value held at the moment of the lock and the two diverged.
        bool nextLock = GUILayout.Toggle(weatherLocked, "Set the weather manually");
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
            // A SINGLE SLIDER. There used to be a separate "snow intensity" that turned
            // on the precipitation and pushed the temperature below freezing; the
            // snow/rain decision came from the temperature hysteresis.
            //
            // When precipitation was decoupled from temperature that slider became a
            // LIAR: set to 0 it never touched `IntensityOverride`, the value written by
            // the precipitation slider remained and the snow kept falling. The user
            // reported it with a screenshot.
            //
            // Now if there is precipitation there is snow; a second slider has nothing
            // left to say.
            GUILayout.Label($"Precipitation intensity {lockedPrecipitation:F2}");
            lockedPrecipitation = GUILayout.HorizontalSlider(lockedPrecipitation, 0f, 1f);

            // SNOW FRACTION, NOT SNOW INTENSITY. The intensity comes from the slider
            // above; this one says how that precipitation splits into snow and rain.
            // Two separate questions: "how much is falling" and "what is falling".
            // A SWITCH, NOT A SLIDER. The threshold is 0.5; there is no "mixed" state,
            // either snow falls or rain does (`SnowfallController`).
            GUILayout.Label($"Precipitation type: {(lockedSnowFraction >= 0.5f ? "SNOW" : "RAIN")}" +
                            $"   (slider {lockedSnowFraction:F2}, threshold 0.50)   " + SnowStatus());
            lockedSnowFraction = GUILayout.HorizontalSlider(lockedSnowFraction, 0f, 1f);
            GUILayout.Label(SnowStateStatus());

            SnowManager mgr = snowManager;

            // ------------------------------------------- SNOW TEST ENVIRONMENT
            //
            // Accumulation, settling and tracks work on the scale of hours; waiting in
            // real time takes minutes and makes hunting a bug impossible.
            //
            // The time multiplier DOES NOT WRITE A FAKE STATE: the same physics runs
            // faster (`_DeltaTimeEff` is scaled). The fill buttons, on the other hand,
            // write the state directly — so that "how does a track look at this snow
            // depth" can be asked without waiting.
            if (mgr != null)
            {
                GUILayout.Space(6f);
                GUILayout.Label("— KAR SINAMASI —");

                GUILayout.Label($"Simulation speed ×{mgr.SimTimeScale:F0}   " +
                                (mgr.SimTimeScale > 1.5f ? "ACCELERATED" : "real time"));

                mgr.SimTimeScale = Mathf.Round(
                    GUILayout.HorizontalSlider(mgr.SimTimeScale, 1f, 500f));

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Kar yok"))    mgr.FillSnowDepth(0f);
                if (GUILayout.Button("1 cm"))       mgr.FillSnowDepth(0.01f);
                if (GUILayout.Button("5 cm"))       mgr.FillSnowDepth(0.05f);
                if (GUILayout.Button("20 cm"))      mgr.FillSnowDepth(0.20f);
                if (GUILayout.Button("50 cm"))      mgr.FillSnowDepth(0.50f);
                GUILayout.EndHorizontal();

                GUILayout.Label($"Measured: coverage {SnowRuntimeState.GroundCoverage01:F3}   " +
                                $"SWE {mgr.MeanSwe * 1000f:F2} mm   " +
                                $"derinlik {SnowDepthMm(mgr):F1} mm");

                if (GUILayout.Button("Revert settings (test)"))
                {
                    mgr.SimTimeScale = 1f;
                    mgr.RefillRegion();
                }

                GUILayout.Space(6f);
            }

            if (mgr != null)
            {
                bool nextWt = GUILayout.Toggle(windTransportOff, "Switch off wind transport (diagnostic)");
                if (nextWt != windTransportOff)
                {
                    windTransportOff = nextWt;
                    mgr.WindTransportOff = windTransportOff;
                }

                bool nextWs = GUILayout.Toggle(windShadowOff, "Switch off the wind shadow (diagnostic)");
                if (nextWs != windShadowOff)
                {
                    windShadowOff = nextWs;
                    mgr.WindShadowOff = windShadowOff;
                }
            }
        }

        GUILayout.Space(6f);

        // It does not run while the driver is locked; neither switch means anything then
        using (new Disabled(weatherLocked))
        {
            weatherDriver.Instant = GUILayout.Toggle(weatherDriver.Instant,
                "Weather follows the altitude instantly");

            GUILayout.Label($"Clear window {weatherDriver.ClearWindow:F2}  " +
                            $"residue {weatherDriver.WindowResidue:F2}");
            GUILayout.Label($"Severity {weatherDriver.StormIntensity:F2}  " +
                            $"cloud mass {weatherDriver.CloudMass:F2}  " +
                            $"ceiling share {weatherDriver.CeilingAt(walker.transform.position.y):F2}");
            weatherDriver.ForceWindow = GUILayout.Toggle(weatherDriver.ForceWindow,
                "Force the weather open");
        }

        if (GUILayout.Button("Fire lightning")) thunder.TriggerNow();

        // A strike not being seen can be two different things: the event never arrived, or it
        // arrived and was not drawn. From outside the two look the same, so the measurement is here.
        GUILayout.Label(lightning.LastDistance < 0f
            ? "Last strike: none"
            : $"Last strike: {lightning.LastDistance:F0} m   " +
              $"light {lightning.Intensity:F2}   glow {lightning.Glow:F2}");

        lightning.Held = GUILayout.Toggle(lightning.Held, "Hold the strike lit");

        EndSection();
    }

    string SnowStatus()
    {
        if (!SnowRuntimeState.IsSnowing) return "kar yok";

        return snowfall != null
            ? $"falling, {snowfall.AliveFlakes} flakes"
            : "falling";
    }

    /// THE STEP AT THE WRAP POINT.
    ///
    static float Rho(float rhoN) => Mathf.Lerp(50f, 550f, Mathf.Clamp01(rhoN));

    static float Depth(float swe, float rhoN) =>
        swe < 0f ? 0f : swe * 1000f / Mathf.Max(Rho(rhoN), 1f);

    /// THE SNOW STATE HAS TO BE READABLE.
    ///
    /// The symptom "no snow" can break at any link of the chain: it is not snowing,
    /// the temperature is high, or the texture is empty. All three look the same on
    /// screen. This line separates the three with numbers.
    ///
    /// SNOW DOES NOT DEPEND ON ALTITUDE. The snow line derived from elevation was
    /// removed; if snow falls it settles. There being more snow high up comes from
    /// the temperature.
    string SnowStateStatus()
    {
        float rhoN = Shader.GetGlobalFloat("_FallbackRhoN");
        float rho = Mathf.Lerp(50f, 550f, Mathf.Clamp01(rhoN));

        // `GroundCoverage01` is the readback of the state texture: near 1 if there is
        // snow, 0 if the texture is empty.
        return $"snowing {(SnowRuntimeState.IsSnowing ? "YES" : "no")}   " +
               $"intensity {SnowRuntimeState.SnowfallIntensity01:F2}   " +
               $"yeni kar ρ {rho:F0}   " +
               $"DOKUDA {SnowRuntimeState.GroundCoverage01:F2}   " +
               $"loose {SnowRuntimeState.LooseSnowFraction:F2}";
    }

    void DrawWind()
    {
        BeginSection("Wind");

        GUILayout.Label($"Severity {wind.Strength:F2}   Speed {wind.Velocity.magnitude:F1} m/s");

        // The lock fixes the base severity and the direction; the fluctuation keeps working on top
        // — the component is not disabled. A 0.5 on the slider is a 0.5 that breathes around itself.
        bool nextLock = GUILayout.Toggle(windLocked, "Set the wind manually");
        if (nextLock != windLocked)
        {
            windLocked = nextLock;
            if (!windLocked) wind.ClearOverride();
        }

        using (new Disabled(!windLocked))
        {
            GUILayout.Label($"Severity {lockedWindStrength:F2}");
            lockedWindStrength = GUILayout.HorizontalSlider(lockedWindStrength, 0f, 1f);

            GUILayout.Label($"Direction {lockedWindAngle:F0}°");
            lockedWindAngle = GUILayout.HorizontalSlider(lockedWindAngle, 0f, 360f);
        }

        EndSection();
    }

    /// SEA DIAGNOSTICS. The shader carries four switches and the panel is the only
    /// way to reach them. Each one removes ONE term, so what is left on screen names
    /// the term that produced the symptom.
    void DrawSea()
    {
        BeginSection("Sea");

        GUILayout.Label($"Hs {SeaRuntimeState.SignificantWaveHeight:F2} m   " +
                        $"Tp {SeaRuntimeState.PeakPeriod:F1} s");
        GUILayout.Label($"shore foam {SeaRuntimeState.ShoreFoamIntensity01:F2}");

        seaNoSurface    = GUILayout.Toggle(seaNoSurface,    "Switch off the sea surface");
        seaNoWaves      = GUILayout.Toggle(seaNoWaves,      "Switch off the waves");
        seaNoShallow    = GUILayout.Toggle(seaNoShallow,    "Switch off shallow water");
        seaNoFoam       = GUILayout.Toggle(seaNoFoam,       "Switch off the foam");
        seaNoRefraction = GUILayout.Toggle(seaNoRefraction, "Switch off refraction");

        // WRITTEN EVERY FRAME, NOT ON CHANGE. `SeaManager` republishes its own
        // globals every frame; a value written once here would be overwritten the
        // next frame and the checkbox would lie.
        Shader.SetGlobalFloat(SeaShaderIDs.DbgNoSurface,    seaNoSurface ? 1f : 0f);
        Shader.SetGlobalFloat(SeaShaderIDs.DbgNoWaves,      seaNoWaves ? 1f : 0f);
        Shader.SetGlobalFloat(SeaShaderIDs.DbgNoShallow,    seaNoShallow ? 1f : 0f);
        Shader.SetGlobalFloat(SeaShaderIDs.DbgNoFoam,       seaNoFoam ? 1f : 0f);
        Shader.SetGlobalFloat(SeaShaderIDs.DbgNoRefraction, seaNoRefraction ? 1f : 0f);

        if (GUILayout.Button("Revert settings (sea)"))
            seaNoSurface = seaNoWaves = seaNoShallow = seaNoFoam = seaNoRefraction = false;

        EndSection();
    }

    // (THE CLOUD SECTIONS WERE DELETED — the cloud system is being rewritten. The slider list and
    // where each link went are kept in `CLOUDS_REBUILD.md`; the panel will be rebuilt from there
    // once the new system arrives.)

    void DrawOverlays()
    {
        BeginSection("What to draw");

        GUILayout.Label($"Visibility {atmosphere.Visibility:F0} m");

        atmosphere.FogEnabled = GUILayout.Toggle(atmosphere.FogEnabled, "Height fog");
        precipitation.enabled = GUILayout.Toggle(precipitation.enabled, "Rain and snow");

        hud.enabled = GUILayout.Toggle(hud.enabled, "Performance readout");
        climbHud.enabled = GUILayout.Toggle(climbHud.enabled, "Climb readout");

        // The OBJECT, not the component: the layer sits on a disabled object.
        GameObject lines = routeOverlay.gameObject;
        bool showLines = GUILayout.Toggle(lines.activeSelf, "Route lines");
        if (showLines != lines.activeSelf) lines.SetActive(showLines);

        EndSection();
    }

    /// Helper that disables GUI.enabled for the scope
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
