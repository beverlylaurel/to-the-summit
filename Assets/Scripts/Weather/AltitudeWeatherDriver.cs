using System;
using UnityEngine;

/// The director of the weather.
///
/// THE WEATHER IS A STATE OF THE WORLD, NOT OF THE PLAYER.
///
/// It used to be a pure function of the player's altitude: a storm existed because the player had
/// climbed into it, and it withdrew as they came back down (`TrackProgress` even remembered the
/// highest point reached and eased off on the way home). `DESIGN.md` forbids exactly that --
/// "the mountain does not target the player; a storm does not break out because the player is
/// there" -- and the sea sat at the end of the same chain, so the swell rose while the player
/// climbed and fell while they descended. Measured: Hs 1.18 m at the shore, 3.66 m from the
/// summit, same moment, same sea.
///
/// Now a single world storm moves on its own clock. Altitude no longer DRIVES the weather; it
/// says how hard that same weather bites at a given height -- fierce in the free air, softened
/// at sea level by the land. Everything reads the same storm: the shore, the mountain, the sea.
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

    /// Test switch: THE WORLD STORM is supplied from outside. Negative = off.
    ///
    /// This is used INSTEAD OF disabling the component. Disabled, `intensity` freezes but
    /// `AtmosphereController` keeps reading `StormIntensity` and `ClearWindow`: while the slider
    /// was dragged, precipitation, visibility, fog and colour followed, but cloud coverage,
    /// thickness, rain absorption and the high layer froze at the value they held at the moment
    /// of the lock. A single state was splitting into two channels and contradicting itself.
    ///
    /// It holds the WORLD, not the reading at the player's height. Held at the local intensity,
    /// the same split came back one level up: the sky was pinned while the sea kept following the
    /// world's own clock, so a locked storm sat over a swell that was quietly dying down.
    public float WorldStormOverride { get; set; } = -1f;

    /// The world's own weather, 0 calm to 1 full storm. It does not read the player.
    public float WorldStorm { get; private set; }

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
        // The player's own height, for the LOCAL reading only. It no longer decides what the
        // weather is doing -- see the note at the top of the file.
        float altitude = observer.position.y;

        SampleNoise();

        // THE WORLD STORM. One number for the whole map, on its own slow clock, with the
        // existing drift riding on top so a storm is not a smooth swell but a real spell of
        // weather. Nothing here reads the observer.
        bool overridden = WorldStormOverride >= 0f;

        if (overridden)
        {
            WorldStorm = Mathf.Clamp01(WorldStormOverride);
        }
        else
        {
            float stormRoll = Mathf.PerlinNoise(Time.time * settings.worldStormFrequency, 4.11f);
            WorldStorm = Mathf.Lerp(settings.worldStormLow, settings.worldStormHigh,
                                    Mathf.SmoothStep(0f, 1f, stormRoll));
        }

        float target = IntensityAt(altitude);

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

        // THE SEA DOES NOT LIVE WHERE THE PLAYER STANDS. It reads the same storm at ITS OWN
        // height, so the swell answers the weather instead of the climb.
        wind.SeaLevelSeverity = Mathf.Max(settings.windAtBase, IntensityAt(groundAltitude));
    }

    /// HOW HARD THE WORLD STORM BITES AT A GIVEN HEIGHT.
    ///
    /// One storm, read at a height. The altitude profile that used to BE the weather is now only
    /// its shape: gentle near the ground, hardening with height. Multiplying instead of replacing
    /// is what makes a calm day calm everywhere and a storm a storm everywhere.
    ///
    /// The sea-level share is not zero: a storm that leaves the water flat is not a storm.
    public float IntensityAt(float altitude)
    {
        // THE ALTITUDE PROFILE SCALES THE STORM, IT DOES NOT GATE IT.
        //
        // `Baseline` runs from `openingIntensity` at the foot to `stormPeak` at the top, and
        // `openingIntensity` is 0 -- so multiplying by it erased the weather at sea level
        // completely: measured, the shore intensity came out 0.00 whatever the world was doing,
        // and the swell sat at Hs 1.17 m for ever. The profile is now a SHARE of the storm, from
        // the sheltered value at the shore to the full thing up in the free air.
        float span = Mathf.Max(settings.stormPeak - settings.openingIntensity, 1e-3f);
        float profile01 = Mathf.Clamp01((Baseline(altitude) - settings.openingIntensity) / span);

        float share = Mathf.Lerp(settings.worldStormAtSeaLevel, 1f, profile01);

        return Mathf.Clamp01(WorldStorm * share * Variation(altitude));
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
