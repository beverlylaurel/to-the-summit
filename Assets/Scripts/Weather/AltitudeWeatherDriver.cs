using System;
using UnityEngine;

/// The director of the weather. The only input is the player's **altitude** — not the distance
/// covered. Walking around the mountain without gaining height does not harden the weather.
///
/// Bands:
///   opening     → very light rain, almost no wind
///   rain        → hardens gradually, no snow
///   transition  → the rain withdraws, snow settles in
///   procedural  → sometimes a blizzard, sometimes calm snow
///   summit      → the fluctuation closes, a permanent full storm
public class AltitudeWeatherDriver : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] Transform observer;
    [SerializeField] TimeOfDay time;
    [SerializeField] WeatherDriverSettings settings;

    [Header("Band boundaries (metres) — all computed by the bootstrap")]
    [Tooltip("The elevation of the flat terrain.")]
    [SerializeField] float groundAltitude;
    [Tooltip("The long-term average of the freezing level. The permanent snow line derives " +
             "from it — derived from the moving boundary, the glacier would have tides too.")]
    [SerializeField] float referenceRainCeiling;
    [SerializeField] float referenceStormFloor;
    [Tooltip("The altitude where the fluctuation closes and the permanent storm begins.")]
    [SerializeField] float stormPeakAltitude;
    [Tooltip("The mountain's summit. Only the climb indicator reads it.")]
    [SerializeField] float summitAltitude;

    // The bands are defined relative to the mountain's height: when the mountain changes the
    // elevations shift on their own, and which part of the climb is stormy stays fixed.
    //
    // They are not serialized. A serialized field's copy in the scene overrides the default in
    // code: once the band boundaries had been saved with a wrong value, changing the code did
    // nothing at all and the difference was only seen in the game.
    const float RainShare = 0.10f;    // this much of the mountain is rain only
    const float UpperBandShare = 0.04f;   // the width of the sleet band above it

    // Perlin is theoretically 0-1, but sampled along a line it practically wanders between ~0.30
    // and ~0.70; it almost never reaches the ends. Putting a threshold on the raw value is
    // therefore misleading: a threshold of 0.80 was never crossed and the window never opened.
    // This range is stretched to full width first; only then do the thresholds mean anything.
    const float NoiseFloor = 0.30f;
    const float NoiseCeiling = 0.70f;

    // The open window's threshold, on the normalized noise.
    const float WindowOpen = 0.65f;
    const float WindowBand = 0.15f;

    // At the summit the threshold rises. The window grows rarer but opens fully when it does —
    // damping the amplitude would have been wrong: it should be rare and full, not weak and frequent.
    const float SummitWindowOpen = 0.85f;

    // Precipitation dies where the cloud **mass** ends, not at the layer's nominal ceiling.
    //
    // The density profile zeroes before reaching the ceiling: even the puffiest cloud starts
    // fading from 55% of its own top, and the flat ones end in the lower third of the layer.
    // A fade anchored to the ceiling was therefore too late — snow kept falling on the player
    // while they stood above the sea of cloud.
    //
    // How far below the ceiling the mass ends, and over what band the fade happens. The fade
    // ends this far below the ceiling, and its band starts below that.

    float intensity;
    float cloudMass;
    float windowRoll;
    float driftCombined;
    float progressAltitude;
    bool initialized;

    /// The storm's raw severity: its state above the cloud ceiling, before the fade.
    ///
    /// Cloud coverage and thickness read this, not `WeatherState.Precipitation`. Precipitation
    /// stops because there is no cloud left above you, but the sea below is the sea of the same
    /// storm; thinning it just because you climbed would make no sense.
    public float StormIntensity => intensity;

    /// The severity the cloud mass sees. The *lagged* form of the precipitation: when the rain
    /// stops the cloud does not disperse at once, and when it restarts the cloud does not gather
    /// at once. Short clear windows therefore pass without opening the sky and long ones open it —
    /// which it is depends on the window's duration, not on a separate rule.
    public float CloudMass => cloudMass;

    /// DRY-AIR CLOUDINESS. Even with zero precipitation the sky does not stay empty: a low
    /// passes, moisture is carried, coverage wanders over hours. INDEPENDENT of precipitation but
    /// on the same timeline — not a separate source of randomness, the driver's own clock.
    ///
    /// The atmosphere reads it as the FLOOR of the coverage: when precipitation arrives the
    /// coverage is already rising, and this value only answers "how closed is the sky while it
    /// is not raining".
    public float DryCoverage { get; private set; }

    /// The top of the cloud column (metres). The atmosphere pushes it every frame — only it
    /// knows the real height (weather map + the current cloud base). The driver does not pull it,
    /// so that the two systems are not bound by a mutual reference. An ELEVATION is sent rather
    /// than a scalar cutoff: the snow profile computes each band's cutoff from its own elevation.
    public float CloudColumnTop { get; set; } = float.PositiveInfinity;

    /// What is left of the precipitation when the clear window is fully open. Indicator only.
    public float WindowResidue { get; private set; }

    /// 0 = closed, 1 = the weather is completely clear. Rare and short-lived.
    ///
    /// Cloud coverage reads this and goes below its permanent lower bound — this is the only way
    /// that bound can be crossed. Without it the two rules contradicted each other: the driver
    /// promised "the clouds part, the summit shows" while the atmosphere said "no path may go
    /// below the floor". The second swallowed the first and the promised moment never came.
    public float ClearWindow { get; private set; }

    /// Test switch: opens the window fully without waiting for the noise. Because the opening is
    /// rare and unpredictable, seeing its effect otherwise takes minutes.
    public bool ForceWindow { get; set; }

    /// Test switch: the reached level is not tracked and no smoothing is applied. The weather
    /// follows the instantaneous elevation immediately. Both are deliberate gameplay rules; this
    /// only removes the wait when flying around freely to see the weather at some elevation.
    public bool Instant { get; set; }

    /// Test switch: the target severity is supplied from outside. Negative = off.
    ///
    /// This is used INSTEAD OF disabling the component. Disabled, `intensity` freezes but
    /// `AtmosphereController` keeps reading `StormIntensity` and `ClearWindow`: while the slider
    /// was dragged, precipitation, visibility, fog and colour followed, but cloud coverage,
    /// thickness, rain absorption and the high layer froze at the value they held at the moment
    /// of the lock. A single state was splitting into two channels and contradicting itself.
    public float IntensityOverride { get; set; } = -1f;

    /// The altitude the weather looks at: not the instantaneous Y but the level the climb reached.
    public float ProgressAltitude => progressAltitude;

    /// The altitude where the permanent storm begins.
    public float BlizzardAltitude => stormPeakAltitude;

    /// A fixed reference derived from the long-term average of the freezing level. The permanent
    /// snow line reads this: a glacier does not have tides with the weather.
    public float ReferenceStormFloor => referenceStormFloor;

    /// The mountain's real summit. For the indicator only.
    public float SummitAltitude => summitAltitude;

    /// Duz arazinin kotu. Kar profili bant araligini buradan kuruyor.
    public float GroundAltitude => groundAltitude;

    public void Bind(WeatherState state, WindField windField, Transform target,
        TimeOfDay clock, WeatherDriverSettings tuning, float ground, float peak)
    {
        weather = state;
        wind = windField;
        observer = target;
        time = clock;
        settings = tuning;

        groundAltitude = ground;
        summitAltitude = peak;

        float height = Mathf.Max(1f, peak - ground);

        // The lower part of the climb is spent in rain, the upper in snow. The sleet band between
        // them is narrow: each should be "only", and the transition should read as a boundary, not a band.
        referenceRainCeiling = ground + height * RainShare;
        referenceStormFloor = referenceRainCeiling + height * UpperBandShare;

        // The summit storm is in the last 1000 metres. It shifts on its own if the mountain changes.
        stormPeakAltitude = Mathf.Max(referenceStormFloor + 200f, peak - 1000f);
    }

    void OnEnable()
    {
        if (weather == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(weather)} is not assigned.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(wind)} is not assigned.");
        if (settings == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(settings)} is not assigned.");
        if (observer == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(observer)} is not assigned.");
        if (time == null)
            throw new InvalidOperationException($"{nameof(AltitudeWeatherDriver)}: {nameof(time)} is not assigned.");
        initialized = false;
    }

    void Update()
    {
        float altitude = TrackProgress(observer.position.y);

        SampleNoise();

        bool overridden = IntensityOverride >= 0f;

        float target = overridden ? IntensityOverride : Baseline(altitude) * Variation(altitude);

        // However far the target jumps, the real value slides into place: going from a downpour to
        // calm weather in an instant would be physically impossible.
        if (!initialized || Instant || overridden)
        {
            intensity = target;
            cloudMass = target;
            initialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, settings.smoothingSeconds));
            intensity = Mathf.Lerp(intensity, target, t);

            // The mass has its own, far slower time constant. Driven from the same value the
            // clouds thinned instantly along with the precipitation: the rain stopped and the sky
            // opened in the same frame. In reality a cloud lingers for a while after the rain.
            float m = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, settings.cloudLagSeconds));
            cloudMass = Mathf.Lerp(cloudMass, target, m);
        }

        // Dry-air cloudiness: a slowly wandering one-dimensional field. Perlin is continuous and
        // differentiable, so the coverage does not jump; the period is on the order of minutes.
        float wander = Mathf.PerlinNoise(Time.time / Mathf.Max(1f, settings.cloudWanderSeconds), 0.37f);
        DryCoverage = Mathf.Lerp(settings.dryCoverageLow, settings.dryCoverageHigh, wander);

        // There can be no precipitation above the cloud ceiling: with no cloud overhead, no snow
        // falls. The top of the column is PUSHED FROM THE ATMOSPHERE (CloudColumnTop), not computed
        // here. The setting's nominal ceiling (7000 m) used to be used here; the mass's real top is
        // set by the weather map's column height at that point and is much lower in most places.
        // Against the nominal value the fade started at 5800 m while the summit is 5686 m — so the
        // rule never fired at all: precipitation continued above the sea of cloud as well.
        ClearWindow = WindowAt(altitude);

        weather.Set(intensity * CeilingAt(observer.position.y));

        // The wind is tied to the same value: as the precipitation hardens the wind hardens too,
        // and when a lull comes they die down together.
        // The base is a lower bound; using it as a scale pushed the calm moments up as well.
        // It is driven from the unfaded severity: above the clouds there is no precipitation but
        // there is still wind — the summit stays merciless even when nothing falls.
        wind.Severity = Mathf.Max(settings.windAtBase, intensity);
    }

    /// A mountain does not rise continuously: you cross a ridge, drop down its length, then climb
    /// again. Looking at the instantaneous altitude, the weather would rewind on every descent.
    /// Instead the level the climb reached is tracked: instantly upward, with a dead band and a lag downward.
    float TrackProgress(float altitude)
    {
        if (!initialized) progressAltitude = altitude;

        // No tracking while the test switch is on: seeing the weather at some elevation should not
        // require flying there and waiting out the dead band and the retreat
        if (Instant)
        {
            progressAltitude = altitude;
            return progressAltitude;
        }

        if (altitude > progressAltitude)
        {
            progressAltitude = altitude;
            return progressAltitude;
        }

        // Descents within the dead band do not affect the weather at all
        float floor = altitude + settings.descentDeadband;
        if (progressAltitude <= floor) return progressAltitude;

        // A real descent past the band too: the weather should soften when you come down for camp
        float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, settings.descentSeconds));
        progressAltitude = Mathf.Lerp(progressAltitude, floor, t);

        return progressAltitude;
    }

    /// The ground severity coming from the altitude. It passes linearly between the band corners.
    ///
    /// The corners are read from the REFERENCE elevations, not from the moving freezing level. The
    /// two are different physics: this curve is the orographic precipitation profile (the higher
    /// you go the more it rains), whereas the freezing level is temperature. Tied to the moving
    /// boundary, once the severity reached 1.0 at the summit the boundary dropped below the
    /// opening plateau and the curve broke there — a jump from 0.12 to 0.41 over 14 metres. The
    /// separation also frees the snow line to come all the way down to the ground: the severity
    /// curve is not affected by it.
    float Baseline(float altitude)
    {
        float openingEnd = groundAltitude + settings.openingRise;

        if (altitude <= openingEnd) return settings.openingIntensity;

        if (altitude < referenceRainCeiling)
            return Mathf.Lerp(settings.openingIntensity, settings.rainPeak,
                Mathf.InverseLerp(openingEnd, referenceRainCeiling, altitude));

        // Transition band: it descends from the rain ceiling to the storm's calm floor
        if (altitude < referenceStormFloor)
            return Mathf.Lerp(settings.rainPeak, settings.stormBase,
                Mathf.InverseLerp(referenceRainCeiling, referenceStormFloor, altitude));

        if (altitude < stormPeakAltitude)
            return Mathf.Lerp(settings.stormBase, settings.stormPeak,
                Mathf.InverseLerp(referenceStormFloor, stormPeakAltitude, altitude));

        return 1f;
    }

    /// The noises are sampled ONCE per frame. The fluctuation can now be asked for per elevation
    /// (the snow profile asks for 128 elevation bands); calling Perlin again on every query was
    /// both wasteful and a source of side effects: ClearWindow drifted to the value of the last
    /// elevation asked about.
    void SampleNoise()
    {
        float t = Time.time;

        windowRoll = Mathf.InverseLerp(NoiseFloor, NoiseCeiling,
            Mathf.PerlinNoise(t * settings.clearWindowFrequency, 77.3f));

        // THE WINDOW'S DEPTH VARIES TOO. With a fixed residue (0.15) every opening was identical
        // to the last. Given its own slow noise the windows no longer resemble each other: in most
        // of them the precipitation stops completely, in some it keeps drizzling.
        // The frequency is separate from the window's — the same one would lock the two together.
        float residueRoll = Mathf.InverseLerp(NoiseFloor, NoiseCeiling,
            Mathf.PerlinNoise(t * settings.clearWindowResidueFrequency, 12.9f));

        // The curve is squared: the residue is mostly near zero and occasionally rises.
        WindowResidue = settings.clearWindowResidue * residueRoll * residueRoll;

        // The noise is not reset between bands: one continuous stream, the phase is not broken
        float slow = Mathf.PerlinNoise(t * settings.slowFrequency, 0f);
        float fast = Mathf.PerlinNoise(t * settings.fastFrequency, 31.7f);
        driftCombined = slow * 0.7f + fast * 0.3f;
    }

    /// How open the clear window is at that elevation. The threshold rises towards the summit: the
    /// window grows rarer there but opens fully when it does.
    ///
    /// The clear window is computed *outside* the fluctuation. Left inside, it was exited early
    /// because the amplitude falls to zero at the summit, and the window never opened at exactly
    /// the place it was wanted most: the moment of standing above the sea of cloud never happened.
    float WindowAt(float altitude)
    {
        if (ForceWindow) return settings.clearWindowStrength;

        float summit = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(stormPeakAltitude - 300f, stormPeakAltitude, altitude));
        float open = Mathf.Lerp(WindowOpen, SummitWindowOpen, summit);

        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(open, open + WindowBand, windowRoll))
               * settings.clearWindowStrength;
    }

    /// A time-varying multiplier. Noise at two rates is superposed to break the uniformity.
    /// Pure: it returns the same value for the same elevation in the same frame.
    float Variation(float altitude)
    {
        float amount = VariationAmount(altitude);
        float multiplier = 1f + (driftCombined - 0.5f) * 2f * amount;

        // The window's effect is independent of the fluctuation's amplitude. Scaled by the
        // amplitude it faded over 300 metres on entering the summit band, and at the summit it
        // jumped to full effect because there was no scale: the sky opened the moment you crossed the line.
        return multiplier * Mathf.Lerp(1f, WindowResidue, WindowAt(altitude));
    }

    /// The share of the precipitation that falls as snow at that elevation.

    /// The share of the precipitation surviving at that elevation relative to the cloud column's top.
    public float CeilingAt(float altitude) =>
        1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(CloudColumnTop, CloudColumnTop + 300f, altitude));

    float VariationAmount(float altitude)
    {
        // This too comes from the reference: the fluctuation's amplitude is the air mass's
        // character, not the current position of the snow line.
        if (altitude < referenceRainCeiling) return settings.rainVariation;

        // Entering the summit band the amplitude NARROWS over 300 metres — it is not zeroed. At
        // zero the severity pinned to 1.00 at the summit: as you gained height the weather turned
        // into a single constant downpour and nothing changed for hours. Even with the base
        // amplitude the range is narrow (0.70-1.00): the summit is still merciless, but not dead.
        float fade = Mathf.Lerp(1f, settings.summitVariation, Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(stormPeakAltitude - 300f, stormPeakAltitude, altitude)));

        return Mathf.Lerp(settings.rainVariation, settings.stormVariation,
            Mathf.InverseLerp(referenceRainCeiling, referenceStormFloor, altitude)) * fade;
    }
}
