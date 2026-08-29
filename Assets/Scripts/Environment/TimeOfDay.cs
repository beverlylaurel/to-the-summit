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
    [Tooltip("South/north tilt of the arc. 0 = straight overhead, larger values give a lower arc.")]
    [SerializeField, Range(0f, 60f)] float arcTilt = 28f;
    [Tooltip("Compass angle of east (degrees). The arc rotates with it.")]
    [SerializeField] float eastHeading;

    [Header("Light")]
    [Tooltip("Raw sun color outside the atmosphere. The dawn tone derives from it, it is not " +
             "chosen separately — a filtering computation does that.")]
    [SerializeField] Color sunColor = new(1f, 0.97f, 0.92f);
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
    // While the ambient probe was frozen the night filled with a fake blue and the moon
    // looked unnecessary. Once the probe became honest the night fell to its real value and
    // the moon was left as the only source lighting the sky. The value was found by eye.
    //
    // THE DEFAULT MATCHES THE SCENE. It used to be 0.204 here while the setup script wrote
    // 0.0199 — a factor of 10.25. The scene wins in practice, so nothing looked wrong; but
    // a `TimeOfDay` dropped into a fresh scene, or one opened before the bootstrap ran, lit
    // the night ten times too brightly. Same rule as `sunIntensity` above.
    [SerializeField] float moonIntensity = 0.0199f;

    [Tooltip("The moon's own directional light. IT DOES CAST A SHADOW: at night the moon " +
             "becomes the main light (`MarkAsSun`'s night handover) and the scene setup gives " +
             "it soft shadows, so moonlight throws the mountain's shadow the way daylight does. " +
             "The sky itself is still always driven by the sun — the package draws the moon " +
             "separately, as a second celestial body.")]
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

    /// The moon's peak intensity. It is written to the SAME light as the sun, so at night the
    /// sky package puts the moon in the sun's place and lights the atmosphere from it. The
    /// value was tuned while the light chain was being filtered; moving to raw light dropped
    /// the `LowSunFade` multiplier and the night brightened.
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

    /// 1 = the sun is exactly on the horizon (dawn or sunset), 0 = at the zenith or deep night.
    /// The warm orange tones mix according to it.
    public float HorizonFactor { get; private set; }

    /// The sun's current color. Orange at dawn, close to white at the zenith.
    public Color CurrentSunColor { get; private set; } = Color.white;


    /// The moon is opposite the sun. Only this component uses it: nothing is left to be read
    /// from outside, the moon's own light and celestial body data are driven from here.
    Vector3 MoonDirection => -SunDirection;

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
    public Vector3 NoonSunDirection => DirectionAt(0.5f);

    /// THE CELESTIAL POLE — the axis the star field rotates about. The sun's arc turns about
    /// the same axis (in `DirectionAt` `local` rotates in the XY plane, i.e. the axis is +Z put
    /// through the same transform). Given the stars a separate axis, the sun and the stars
    /// would turn in different directions.
    public Vector3 CelestialPole =>
        Quaternion.Euler(0f, eastHeading, 0f)
        * (Quaternion.AngleAxis(arcTilt, Vector3.right) * Vector3.forward);

    public void Bind(Light directional, Light moonLight)
    {
        sun = directional;
        moon = moonLight;
        MarkAsSun();

#if URP_PBSKY
        // The moon is given to the sky package as a SECOND CELESTIAL BODY: its disc is drawn
        // independently of the main light, and its phase and earthshine come from the package's own computation.
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
        Light wanted = sun;

        if (moon != null && SunHeight <= NightHandoverHeight)
            wanted = moon;

        // The equality check avoids an unnecessary write: this is a scene setting and written
        // every frame it keeps the scene permanently dirty.
        if (wanted != null && RenderSettings.sun != wanted) RenderSettings.sun = wanted;
    }

    /// Below this elevation the main light is the moon. Not zero: with the sun exactly on the
    /// horizon its intensity is already near zero and its shadows stretch meaninglessly.
    const float NightHandoverHeight = -0.05f;

    /// Sets the time directly, for tests and previews
    public void SetNormalized(float value)
    {
        normalized = Mathf.Repeat(value, 1f);
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
            normalized = Mathf.Repeat(normalized + Time.deltaTime / (dayLengthMinutes * 60f), 1f);

        Apply();
    }

    /// The sun's direction at a given time. The sun traces an arc: it rises in the east,
    /// passes through a peak tilted to the south and sets in the west. Change only the tilt and
    /// it rises and sets at the same point — that is not an arc.
    Vector3 DirectionAt(float clock)
    {
        float angle = (clock - 0.25f) * 360f;
        var local = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);

        // Tilt the arc south, then turn it by the compass
        Vector3 direction = Quaternion.Euler(0f, eastHeading, 0f)
                            * (Quaternion.AngleAxis(arcTilt, Vector3.right) * local);

        return direction.normalized;
    }

    void Apply()
    {
        SunDirection = DirectionAt(normalized);
        float elevation = SunDirection.y;

        SunHeight = elevation;

        // The main light handover depends on the time of day, it is not a one-time setup.
        // Left in `Bind` and `OnEnable` the night would stay on the sun forever.
        MarkAsSun();

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
        // THE SUN BEAM IS FOR COLOR ONLY. The intensity no longer comes from here — absorption
        // is owned by the sky package and the raw sun is written to the light. `CurrentSunColor`
        // is still consumed: the fog color, the cloud tone and the terrain's dawn color feed on it.
        Vector3 beam = Atmosphere.BeamTransmittance(0f, SunDirection);

        // Color and intensity are carried separately: most consumers use the color as a TONE
        // and the falloff is carried by the light's intensity. Their product equals the real beam.
        // THE DIMMER IS APPLIED TO THE COLOR TOO. Because `Tint()` pulls the brightest channel
        // to 1, the color stayed fully saturated while the beam dimmed: applied only to the
        // intensity, clouds turned pink all at once under a low sun. Color and intensity have to follow the same curve.
        float sunFade = Atmosphere.LowSunFade(0f, SunDirection);
        CurrentSunColor = Tint(Vector3.Scale(beam,
            new Vector3(sunColor.r, sunColor.g, sunColor.b))) * sunFade;

        // TWO BODIES, TWO LIGHTS. Fitting them into one light was structurally impossible: the
        // moon is exactly opposite the sun, a direction is a single thing, and at the handover
        // the disc jumped 180°. Each body now drives its own light; the sky package draws the
        // moon separately as a second CELESTIAL BODY (`PhysicallyBasedSkyURP.MoonLight`).
        //
        // The band asymmetry stays: the sun's reaches -18° (the end of astronomical twilight)
        // because it drives the sky; the moon's is ±3°, and we do not model a twilight for a
        // secondary source.
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.LookRotation(-SunDirection);
            sun.color = sunColor;

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
            // THE PRODUCT IS NOT THE RAW BEAM. `LowSunFade` is applied TWICE on purpose —
            // once to the color (above) and once here to the intensity — so
            // `CurrentSunColor x intensity` comes to `beam x sunFade^2`. The comment used to
            // claim the two multiplied back to the real beam; they do not, and the squaring
            // is deliberate: a low sun is dimmed once as a color decision and once as a light
            // decision. The cloud side squares the same fade for the same reason
            // (`AtmosphereController`, `cloudWarm *= cloudWarm`).
            float extinction = Mathf.Max(beam.x, Mathf.Max(beam.y, beam.z)) * sunFade;
            sun.intensity = sunIntensity * SunBlend(SunDirection.y) * extinction;

            // THE SKY IS NOT LIT BY THE LIGHT THE GROUND GETS.
            //
            // Everything above this line dims and reddens the sun BECAUSE THAT IS WHAT A SLOPE
            // RECEIVES at sunset. The sky package computes its own atmosphere, so handing it the
            // same value applies the absorption twice more — the light already carries it in the
            // colour and again in the intensity.
            //
            // MEASURED: as the sun goes from +0.058 to 0 the light's intensity falls 0.725 ->
            // EXACTLY ZERO and the sky's zenith follows it down, 0.0101 -> 0.0000295. Sunset was
            // coming out THIRTEEN TIMES DARKER than a moonlit midnight, which is backwards by
            // about four orders of magnitude.
            //
            // `SunBlend` STAYS. It is not absorption, it is the gate that ends astronomical
            // twilight at -18 degrees; without it the LUT would be lit by a sun pointing through
            // the planet all night. At the horizon it is still 0.98, so the sky gets its sun.
            //
            // THE MOON HAS TO SURVIVE THE HAND-OVER. Written as the sun's radiance alone this
            // zeroed the night sky: past -18 degrees the gate closes, and the override was still
            // in force, so it replaced the moonlight the package had been lighting the LUT with.
            // Measured: the night zenith went from 0.00039 to EXACTLY ZERO. Taking the larger of
            // the two hands the sky over without a seam — the sun wins until twilight ends, the
            // moon from then on, and neither is ever switched off under the other.
            //
            // WHAT THIS DOES NOT FIX: the LUT still takes its DIRECTION from whichever light URP
            // calls the main one, and past the horizon that is the moon. So twilight carries the
            // right amount of energy from the wrong direction. Pre-existing, and the package's
            // own note says why (one light per LUT). DECISIONS.md.
            float skySun = sunIntensity * SunBlend(SunDirection.y);
            float skyMoon = moonIntensity * MoonBlend(MoonDirection.y);

            PhysicallyBasedSkyURP.SkySunRadiance = skySun >= skyMoon
                ? sunColor.linear * (skySun * Mathf.PI)
                : moonColor.linear * (skyMoon * Mathf.PI);
        }

        if (moon != null)
        {
            moon.transform.rotation = Quaternion.LookRotation(-MoonDirection);
            moon.color = moonColor;
            moon.intensity = moonIntensity * MoonBlend(MoonDirection.y);
        }

        // The sun elevation is published GLOBALLY as well. The version carried as a material
        // property did not close the night gate in the terrain shader (the snow sparkle kept
        // being drawn all night); the global path took effect in the same frame. There is a
        // single sun in the whole scene and the value has no per-material meaning anyway.
        Shader.SetGlobalFloat(SunHeightId, SunHeight);

        Changed?.Invoke(this);
    }
}
