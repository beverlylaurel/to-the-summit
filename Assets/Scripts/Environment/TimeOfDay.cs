using System;
using UnityEngine;

/// Holds the time of day and rotates the sun. It knows nothing about the weather.
/// Light, fog and color grading are the side that consumes both.
[ExecuteAlways]
public class TimeOfDay : MonoBehaviour
{
    [SerializeField] Light sun;

    [Tooltip("0 = midnight, 0.25 = dawn, 0.5 = noon, 0.75 = sunset.")]
    [SerializeField, Range(0f, 1f)] float normalized = 0.3f;
    [Tooltip("Real duration of a full day (minutes). 0 = time does not flow.")]
    [SerializeField] float dayLengthMinutes = 40f;
    [Tooltip("Compass angle of east (degrees). The astronomical horizon rotates with it.")]
    [SerializeField] float eastHeading;

    [Header("Astronomy")]
    [Tooltip("Simulated calendar year. The date advances when the game clock crosses midnight.")]
    [SerializeField, Range(1901, 2099)] int calendarYear = 2026;
    [Tooltip("Day of year (1-365/366). Controls solar declination, day length and lunar phase.")]
    [SerializeField, Range(1, 366)] int calendarDayOfYear = 247;
    [Tooltip("Observer latitude. Default is central Turkey; change this if the mountain moves.")]
    [SerializeField, Range(-89f, 89f)] float latitudeDegrees = 39.0f;
    [Tooltip("Observer longitude, east positive. Used with the UTC offset for true local solar time.")]
    [SerializeField, Range(-180f, 180f)] float longitudeDegrees = 35.0f;
    [Tooltip("Local time offset from UTC. Turkey uses UTC+3 year-round.")]
    [SerializeField, Range(-12f, 14f)] float utcOffsetHours = 3.0f;

    [Header("Light")]
    [Tooltip("Raw sun color outside the atmosphere. The dawn tone derives from it, it is not " +
             "chosen separately — a filtering computation does that.")]
    [SerializeField] Color sunColor = Color.white;
    // MOON ALBEDO — its state BEFORE passing through the atmosphere. The rising moon being
    // too orange came from here, and the computation is this:
    //
    // The zenith optical depth from our air densities: R 0.046 · G 0.108 · B 0.265.
    // With the moon at 10° the air mass is ~5.6x, transmittance R 0.77 · G 0.55 · B 0.23.
    //
    //   0.62 0.70 0.92 -> at 10° 1.00 0.80 0.43 · at zenith 0.84 0.89 1.00
    //   0.52 0.64 1.00 -> at 10° 1.00 0.87 0.56 · at zenith 0.65 0.75 1.00
    //
    // Full compensation (0.29 0.42 1.00) removes the orange but pulls the overhead moon
    // toward violet. Halfway was chosen: the blue of the rising moon goes from 0.43 to 0.56
    // while the overhead moon does not cool too much.
    //
    // THE DEFAULT MATCHES THE SCENE. `MountainSceneBootstrap` writes this value too;
    // the field default used to be (0.52, 0.64, 1.00) and the two silently diverged.
    // The rule was already written down for `sunIntensity` and simply had not been
    // applied to the moon.
    [SerializeField] Color moonColor = new(0.586f, 0.653f, 0.818f);
    // 3.030782 is the sky package's calibration: 100000 lux of ground illuminance. The scene
    // setup writes it too, and the default was updated here so the two do not diverge.
    [SerializeField] float sunIntensity = 3.030782f;
    // The physical sun/full-moon illuminance ratio is far beyond the display's usable range.
    // 0.002 keeps the source more than ten stops below the sun; LookController's slow rod
    // adaptation then recovers a readable, still recognisably dark moonlit landscape.
    [SerializeField] float moonIntensity = 0.002f;

    [Tooltip("The moon's own directional light. IT DOES CAST A SHADOW: when its current energy " +
             "exceeds the sun it becomes the main light, and the scene setup gives " +
             "it soft shadows, so moonlight throws the mountain's shadow the way daylight does. " +
             "The sky package scatters it separately and draws its real phase.")]
    [SerializeField] Light moon;

    /// The sun's peak intensity. Because the sky package derives its own brightness from the
    /// main light, the relative brightness of the sky and the scene is set from here; the F1
    /// panel drives it.
    /// A celestial body's contribution to the light, 0-1. NOT ATMOSPHERIC, GEOMETRIC:
    /// absorption and reddening are the sky package's job.
    ///
    /// THE SUN'S BAND IS DEEP AND THE MOON'S NARROW — and that asymmetry is deliberate. The
    /// package computes the sky from the light's direction and intensity: once the sun drops
    /// below the horizon and its intensity is zeroed, TWILIGHT GOES OUT TOO. But the sun keeps
    /// lighting the sky while it is below the horizon; civil twilight lasts half an hour. The
    /// band reaches down to -12° so that scattering reaches the package.
    ///
    /// The terrain is not lit wrongly by this: with the light below the horizon it arrives
    /// almost horizontally and `N·L` stays negative on flat ground. Steep slopes facing the
    /// sun receive some light — that is exactly what alpenglow is.
    ///
    /// The moon's band stays narrow: the moon is a secondary source and we do not model a
    /// twilight for it.
    ///
    /// THE FLOOR IS -18°: THE END OF ASTRONOMICAL TWILIGHT. -12° was tried and was not enough —
    /// with the sun at -11.5° (~18:46) the intensity zeroed, the sky went out and the moon had
    /// not yet risen enough to carry it; 18:38-18:46 was pitch black. In reality the sky stays
    /// lit down to -18°, and night begins there.
    ///
    /// sin(3°) ~ 0.0523, sin(-18°) ~ -0.3090.
    const float MoonHorizonBand = 0.0523f;
    const float SunHorizonTop = 0.0523f;
    const float SunTwilightFloor = -0.3090f;

    static float SunBlend(float directionY) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SunTwilightFloor, SunHorizonTop, directionY));

    static float MoonBlend(float directionY) =>
        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-MoonHorizonBand, MoonHorizonBand, directionY));

    public float SunIntensity
    {
        get => sunIntensity;
        set => sunIntensity = value;
    }

    /// The full-moon peak intensity before phase and atmospheric extinction. It remains a
    /// separate directional source; phase changes its irradiance without dimming the lit pixels
    /// of the rendered lunar disc.
    public float MoonIntensity
    {
        get => moonIntensity;
        set => moonIntensity = value;
    }

    /// The moon's own color — the albedo of its surface, BEFORE passing through the
    /// atmosphere. Near the horizon the moon travels a long path through the atmosphere, blue
    /// is absorbed and the disc shifts to yellow-orange; that is physically correct. Choose a
    /// cooler base and the post-absorption result approaches neutral.
    public Color MoonColor
    {
        get => moonColor;
        set => moonColor = value;
    }



    public event Action<TimeOfDay> Changed;

    public float Normalized => normalized;

    public int CalendarYear => calendarYear;
    public int CalendarDayOfYear => calendarDayOfYear;
    public float MoonIlluminatedFraction { get; private set; }
    public float MoonAgeDays { get; private set; }

    /// Freezes time for testing.
    public bool Paused { get; set; }

    /// Readable time in hour:minute form.
    public string Clock
    {
        get
        {
            float hours = normalized * 24f;
            return $"{Mathf.FloorToInt(hours):00}:{Mathf.FloorToInt(hours % 1f * 60f):00}";
        }
    }

    /// The sun's elevation above the horizon: 1 at the zenith, 0 at the horizon, negative at night.
    public float SunHeight { get; private set; }

    /// 0 full night, 1 full day. The sky color, fog color and color grading read it.
    ///
    /// Not to be confused with `sunOverMoon` below: that is a switch saying whether the light's
    /// source is the sun or the moon, and it deliberately turns over a far narrower band. This
    /// one answers "how much daylight is there" and has to be wide. They are different
    /// questions; fitting one to the other either splits the light source in two for half an
    /// hour at the horizon or makes 8 in the morning as bright as noon.
    public float DayFactor { get; private set; }

    /// Unit vector pointing toward the sun. The sky disc uses it.
    static readonly int SunHeightId = Shader.PropertyToID("_SunHeight");

    public Vector3 SunDirection { get; private set; } = Vector3.up;

    /// Direction toward the celestial light that currently contributes the most direct radiance.
    /// Rain uses the same handover as the scene instead of continuing to read the sun at night.
    public Vector3 PrimaryLightDirection => PrimaryLight == moon ? MoonDirection : SunDirection;

    /// Linear source tone of the dominant celestial light. Brightness is carried separately.
    public Color PrimaryLightColor => PrimaryLight != null ? PrimaryLight.color : Color.black;

    /// Intensity of the dominant celestial light after horizon gating and atmospheric extinction.
    public float PrimaryLightIntensity => PrimaryLight != null ? PrimaryLight.intensity : 0f;
    public float SunLightIntensity => sun != null ? sun.intensity : 0f;
    public Light SunLight => sun;

    Light PrimaryLight
    {
        get
        {
            if (sun == null) return moon;
            if (moon == null) return sun;
            return sun.intensity >= moon.intensity ? sun : moon;
        }
    }

    /// 1 = the sun is exactly on the horizon (dawn or sunset), 0 = at the zenith or deep night.
    /// The warm orange tones mix according to it.
    public float HorizonFactor { get; private set; }

    /// The sun's current color. Orange at dawn, close to white at the zenith.
    public Color CurrentSunColor { get; private set; } = Color.white;


    /// Unit vector pointing toward the moon. It comes from an independent lunar orbit; only a
    /// full moon is approximately opposite the sun.
    public Vector3 MoonDirection { get; private set; } = Vector3.down;

    /// LIGHT REACHING FLAT GROUND. The two bodies' contributions are summed and each is
    /// multiplied by ITS OWN elevation: the intensity of a body below the horizon does not
    /// reach flat ground (`N·L` negative). Exposure adaptation reads this.
    public float SurfaceLightLevel
    {
        get
        {
            float level = 0f;
            if (sun != null) level += sun.intensity * Mathf.Max(0f, -sun.transform.forward.y);
            if (moon != null) level += moon.intensity * Mathf.Max(0f, -moon.transform.forward.y);
            return level;
        }
    }




    /// Reduces a color to a tone: the brightest channel becomes 1 and the falloff is handed to the intensity.
    static Color Tint(Vector3 v)
    {
        float peak = Mathf.Max(v.x, Mathf.Max(v.y, v.z));
        return peak <= 1e-6f ? Color.black
             : new Color(v.x / peak, v.y / peak, v.z / peak, 1f);
    }

    /// The sun's direction at noon. Permanent properties of the surface look at this: lichen
    /// settles according to annual sun exposure, and tied to the instantaneous sun position it would blink through the day.
    public Vector3 NoonSunDirection => DirectionAt(0.5f).SunDirection;

    /// The celestial north pole has an elevation equal to observer latitude. The star field
    /// rotates around the same physical axis used by the ephemeris.
    public Vector3 CelestialPole
    {
        get
        {
            Quaternion heading = Quaternion.Euler(0f, eastHeading, 0f);
            Vector3 north = heading * Vector3.back;
            float latitude = latitudeDegrees * Mathf.Deg2Rad;
            return (north * Mathf.Cos(latitude) + Vector3.up * Mathf.Sin(latitude)).normalized;
        }
    }

    public void Bind(Light directional, Light moonLight)
    {
        sun = directional;
        moon = moonLight;
        // Atmospheric extinction owns the sun's chromatic shift. A second 5000 K filter on
        // the already warm source multiplied the noon spectrum into an orange light.
        if (sun != null) sun.useColorTemperature = false;
        MarkAsSun();

#if URP_PBSKY
        PhysicallyBasedSkyURP.SunLight = directional;
        PhysicallyBasedSkyURP.MoonLight = moonLight;
#endif
    }

    /// URP picks the main directional light by whichever is brightest. Because a lightning
    /// flash is brighter than the sun it takes over the main light at that moment and the
    /// mountain's shadows shift for a frame. Marking the sun explicitly pins the choice.
    void MarkAsSun()
    {
        // AT NIGHT THE MAIN LIGHT IS HANDED TO THE MOON.
        //
        // It used to be pinned to the sun always. Once the sun dropped below the horizon
        // and its intensity approached zero, URP kept seeing the EXTINGUISHED sun as the
        // main light: with the moon overhead nothing cast a shadow in the moon's direction.
        //
        // The original purpose of the marking is preserved — because a lightning flash is
        // brighter than the sun, Unity's "pick the brightest" behaviour handed the main
        // light to the lightning for a frame. The explicit assignment still pins that
        // choice; only which one it pins to now depends on the time of day.
        Light wanted = PrimaryLight;

        // The equality check avoids an unnecessary write: this is a scene setting and written
        // every frame it keeps the scene permanently dirty.
        if (wanted != null && RenderSettings.sun != wanted) RenderSettings.sun = wanted;
    }

    /// Sets the time directly, for tests and previews
    public void SetNormalized(float value)
    {
        normalized = Mathf.Repeat(value, 1f);
        Apply();
    }

    /// Sets the simulated date without touching the clock. Useful for seasonal previews and
    /// deterministic validation.
    public void SetCalendarDate(int year, int dayOfYear)
    {
        calendarYear = Mathf.Clamp(year, 1901, 2099);
        calendarDayOfYear = Mathf.Clamp(dayOfYear, 1,
            DateTime.IsLeapYear(calendarYear) ? 366 : 365);
        Apply();
    }

    void OnEnable()
    {
        MarkAsSun();
        Apply();
    }

    void Update()
    {
        if (Application.isPlaying && !Paused && dayLengthMinutes > 0f)
        {
            float next = normalized + Time.deltaTime / (dayLengthMinutes * 60f);
            int elapsedDays = Mathf.FloorToInt(next);
            normalized = Mathf.Repeat(next, 1f);
            if (elapsedDays > 0) AdvanceCalendar(elapsedDays);
        }

        Apply();
    }

    CelestialEphemeris.Sample DirectionAt(float clock) => CelestialEphemeris.Evaluate(
        calendarYear, calendarDayOfYear, clock, latitudeDegrees, longitudeDegrees,
        utcOffsetHours, eastHeading);

    void AdvanceCalendar(int days)
    {
        while (days-- > 0)
        {
            calendarDayOfYear++;
            int daysInYear = DateTime.IsLeapYear(calendarYear) ? 366 : 365;
            if (calendarDayOfYear <= daysInYear) continue;

            calendarDayOfYear = 1;
            calendarYear = Mathf.Min(2099, calendarYear + 1);
        }
    }

    void Apply()
    {
        CelestialEphemeris.Sample celestial = DirectionAt(normalized);
        SunDirection = celestial.SunDirection;
        MoonDirection = celestial.MoonDirection;
        MoonIlluminatedFraction = celestial.MoonIlluminatedFraction;
        MoonAgeDays = celestial.MoonAgeDays;
        float elevation = SunDirection.y;

        SunHeight = elevation;

        // Let it soften over a wide band: twilight must not end abruptly.
        // Kept narrow, 8 in the morning looked as bright as noon.
        DayFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.22f, 0.45f, elevation));

        // Proximity to the horizon: this drives the warm tones of dawn and sunset
        HorizonFactor = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(Mathf.Abs(elevation) / 0.32f));

        // The light's color is information independent of the light object: the clouds, the fog
        // and the mountain surface read it too. Kept inside `if (sun != null)` it was never
        // updated and froze at white — which is why the red of dawn showed up nowhere.
        // THE BEAM PASSING THROUGH THE ATMOSPHERE. The color is not chosen: Rayleigh consumes
        // the blue, Mie builds the white halo, and ozone swallows the green at twilight and
        // leaves violet. There is NO normalization — a reddening beam has to DIM. The old form
        // always pulled the brightest channel to 1: the sunset locked into a red that never
        // dimmed and hurt the eye.
        // Ground-reaching direct sunlight: its normalized tone goes to the directional light,
        // while the brightest channel carries the matching energy in intensity. The sky and
        // clouds receive separate top-of-atmosphere radiance below.
        Vector3 beam = Atmosphere.BeamTransmittance(0f, SunDirection);

        // Color and intensity are carried separately: most consumers use the color as a TONE
        // and the falloff is carried by the light's intensity. Their product equals the real beam.
        // THE DIMMER IS APPLIED TO THE COLOR TOO. Because `Tint()` pulls the brightest channel
        // to 1, the color stayed fully saturated while the beam dimmed: applied only to the
        // intensity, clouds turned pink all at once under a low sun. Color and intensity have to follow the same curve.
        CurrentSunColor = Tint(Vector3.Scale(beam,
            new Vector3(sunColor.r, sunColor.g, sunColor.b)));

        // TWO BODIES, TWO LIGHTS. Fitting them into one light is structurally impossible and a
        // real lunar orbit is antipodal only near full moon. Each body drives its own light; the
        // sky package draws the moon separately as a second celestial body.
        //
        // The band asymmetry stays: the sun's reaches -18° (the end of astronomical twilight)
        // because it drives the sky; the moon's is ±3°, and we do not model a twilight for a
        // secondary source.
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sun.color = CurrentSunColor;

            // AIR MASS EXTINCTION IS APPLIED TO THE LIGHT TOO. For a while it was not, with the
            // reasoning "absorption is owned by the sky package" — but the package cannot dim a
            // Unity directional light. The result: the sky dimmed at sunset while the terrain
            // kept taking full sun.
            //
            // `SunBlend` is not a falloff but a GATE: `SunHorizonTop` is sin(3°), so above 3°
            // it always returns 1. In reality the direct beam at 3° is 5-10% of its zenith
            // value, 30% at 10° and 75% at 40°.
            //
            // Measured (color probe 2, sunlit flat ground): the sun-shadow difference was 5+
            // stops, while the real value for snow in clear weather is 2.5-3. That is where the
            // contrast blow-up came from.
            //
            // The BRIGHTEST CHANNEL is taken, not the luminance: `Tint()` normalizes the color
            // by the same channel, so the color and the intensity follow the same curve.
            //
            // BeamTransmittance already contains the continuous low-sun limiter. Applying it
            // again squared the fade and caused the direct light to jump by orders of magnitude
            // in the first few degrees after sunrise.
            float extinction = Mathf.Max(beam.x, Mathf.Max(beam.y, beam.z));
            sun.intensity = sunIntensity * SunBlend(SunDirection.y) * extinction;

            // The sky is not fed the ground light. It performs its own path integration, so it
            // receives source radiance above the atmosphere and only the geometric twilight gate.
            // Sun and moon remain separate inputs; no max/handover discards one under the other.
            float skySun = sunIntensity * SunBlend(SunDirection.y);
            PhysicallyBasedSkyURP.SkySunRadiance = sunColor.linear * (skySun * Mathf.PI);
        }

        if (moon != null)
        {
            moon.transform.rotation = Quaternion.LookRotation(-MoonDirection);
            Vector3 moonBeam = Atmosphere.BeamTransmittance(0f, MoonDirection);
            moon.color = Tint(Vector3.Scale(moonBeam,
                new Vector3(moonColor.r, moonColor.g, moonColor.b)));

            float moonExtinction = Mathf.Max(moonBeam.x, Mathf.Max(moonBeam.y, moonBeam.z));
            // Ground illumination follows lunar phase. A small earthshine floor keeps the new
            // moon from becoming a discontinuous on/off source.
            float phaseLight = Mathf.Lerp(0.01f, 1f,
                Mathf.Pow(MoonIlluminatedFraction, 1.35f));
            float skyMoon = moonIntensity * MoonBlend(MoonDirection.y) * phaseLight;
            moon.intensity = skyMoon * moonExtinction;

#if URP_PBSKY
            PhysicallyBasedSkyURP.SkyMoonRadiance = moonColor.linear * (skyMoon * Mathf.PI);
            PhysicallyBasedSkyURP.MoonSurfaceRadiance = moonColor.linear * (moonIntensity * Mathf.PI);
#endif
        }

        // Pick the actual strongest direct source only after both lights have their current-frame
        // energy. This removes the dawn/dusk interval where URP was pinned to an extinguished sun.
        MarkAsSun();

        // The sun elevation is published GLOBALLY as well. The version carried as a material
        // property did not close the night gate in the terrain shader (the snow sparkle kept
        // being drawn all night); the global path took effect in the same frame. There is a
        // single sun in the whole scene and the value has no per-material meaning anyway.
        Shader.SetGlobalFloat(SunHeightId, SunHeight);

        Changed?.Invoke(this);
    }
}
