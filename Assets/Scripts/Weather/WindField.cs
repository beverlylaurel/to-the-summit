using UnityEngine;

/// Produces the wind vector. The precipitation is the first system to consume it, not its sole
/// owner — the climbing and the audio will read from here too.
///
/// The speed is made of two time scales: a slow base (the air itself) and a fast gust.
/// How hard it blows is set by the Severity coming from outside; the altitude and storm logic
/// belong to the weather's director, not here.
public class WindField : MonoBehaviour
{
    [SerializeField] WindSettings settings;

    /// 0 = almost still, 1 = a full storm. The weather's director writes it.
    public float Severity { get; set; } = 0.3f;

    /// TERRAIN EXPOSURE. 1 = an open ridge, 0 = a sheltered hollow. Pushed from outside:
    /// the wind does not know the terrain, but the terrain knows the wind. `TerrainWindShelter`
    /// drives this the way `AltitudeWeatherDriver` drives `Severity`.
    ///
    /// As long as the wind was global, the top of a ridge and the floor of a valley blew the same;
    /// yet that is the biggest difference felt on a mountain.
    public float Exposure { get; set; } = 0.6f;

    /// The PREVAILING wind direction, a unit vector. Separate from the instantaneous `Velocity`:
    /// it contains no gust and no wobble.
    ///
    /// The surface pattern reads this, not `Velocity`. Confusing the two makes the
    /// pattern slide across the world: the field is built on `dot(worldXZ, windAxis)` and in the
    /// middle of the mountain |worldXZ| is seven thousand metres — a gust's 0.14 radian deviation
    /// dragged the pattern by 980 metres (the body is 45 m). Particles, audio and drift keep
    /// reading the instantaneous wind.
    public Vector3 PrevailingDirection
    {
        get
        {
            float angle = overrideActive ? overrideAngle : PrevailingAngle;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }
    }

    float PrevailingAngle => settings.prevailingDegrees * Mathf.Deg2Rad;

    /// The wind speed in world space (m/s). There is no vertical component.
    public Vector3 Velocity { get; private set; }

    /// The sustained severity 0-1, gust excluded. It reaches exactly 1 at Severity 1.
    /// Slow-responding systems read this: the visibility does not open and close with an eight
    /// second gust, and the cloud layer does not rise and fall with one.
    public float Strength { get; private set; }

    /// THE FREE-AIR SPEED (m/s): it derives from `Severity` alone. Terrain exposure and the gust
    /// ARE NOT APPLIED.
    ///
    /// The cloud layer reads this. `Strength` is scaled by the exposure; had the layer read that,
    /// the cloud two kilometres up would slow down whenever the player stepped behind a rock.
    public float FreeAirSpeed => Mathf.Lerp(settings.calmSpeed, settings.stormSpeed,
        ShapeSeverity(overrideActive ? overrideSeverity : Mathf.Clamp01(Severity)));

    /// SEVERITY AT SEA LEVEL. `AltitudeWeatherDriver` writes it from the same world storm read at
    /// the shore's height.
    public float SeaLevelSeverity { get; set; } = 0.2f;

    /// THE FREE-AIR SPEED DOWN AT THE WATER.
    ///
    /// The sea used to read `FreeAirSpeed`, which is the wind where the PLAYER is standing. So the
    /// swell rose as the player climbed: measured, Hs 1.18 m at the shore and 3.66 m from the
    /// summit at the same instant. The water lives at sea level and reads the wind there.
    /// NO QUARTIC SHAPING HERE, AND THAT IS DELIBERATE.
    ///
    /// `ShapeSeverity` is a DISTRIBUTION choice made for the rain: it exists so a mid-range
    /// severity does not throw drops sideways, and it says so in its own comment. Applied to the
    /// water it flattens the sea instead -- measured, severity 0.55 came out as 4.0 m/s, which is
    /// Beaufort 3, so a storm overhead still left the swell at about a metre. The sea reads the
    /// severity straight: 0 is `calmSpeed`, 1 is `stormSpeed`.
    public float SeaLevelSpeed => Mathf.Lerp(settings.calmSpeed, settings.stormSpeed,
        overrideActive ? overrideSeverity : Mathf.Clamp01(SeaLevelSeverity));

    /// SEVERITY → SPEED CURVE. Not linear but quartic.
    ///
    /// This is not a law of physics but a DISTRIBUTION decision — openly so. The severity itself
    /// comes from Perlin and spends most of its time in the middle band. In a linear mapping
    /// "half wind" meant 8 m/s outright, i.e. Beaufort 5, and the game was permanently in a
    /// strong breeze.
    ///
    /// The symptom was read in the rain: at severity 0.57 the wind was 8.5 m/s and a 1 mm drop
    /// came down 25° from the horizontal — the physics was right, the wind was too much. The user
    /// said "there are drops moving horizontally"; the measurement confirmed the drift (F1 → it
    /// stopped once the wind drift was switched off).
    ///
    /// THE ENDS ARE FIXED: 0 → calmSpeed, 1 → stormSpeed. Only the middle band comes down:
    ///
    ///   severity 0.25 → 2.0 m/s instead of 5.0
    ///   severity 0.50 → 2.8 m/s instead of 8.0
    ///   severity 0.75 → 5.8 m/s instead of 11.0
    ///   severity 0.90 → 9.9 m/s instead of 12.8
    ///
    /// THE EXPONENT 4 WAS FOUND WITH A SLIDER, not by calculation — the right value is a
    /// preference. The slider was in F1 and was deleted once the value was found.
    ///
    /// APPLIED IN ONE PLACE, AND THE THRESHOLDS ARE PRESERVED. `Strength` derives from this speed
    /// too, so every wind-dependent system (the fog closing in, the drifting snow threshold, the
    /// vortex amplitude, the audio, the cloud speed) comes down together — the wind threshold
    /// (0.22) now opens at severity ~0.56 and a full blizzard at ~0.87. Tuned separately, the
    /// weather would contradict itself.
    ///
    /// Calm air is the rule, a storm is an event.
    static float ShapeSeverity(float severity)
    {
        float s = Mathf.Clamp01(severity);
        float sq = s * s;
        return sq * sq;
    }

    /// The instantaneous deviation riding on the sustained severity, −1..1. This is what is heard and seen.
    ///
    /// A single number could not carry both: when the gust exceeded the storm speed the normalized
    /// severity was clamped but the speed was not. The result was that the particles kept speeding
    /// up while the audio and the visibility stayed pinned to the ceiling — two consumers seeing
    /// the same wind at different severities.
    public float Gust { get; private set; }

    bool overrideActive;
    float overrideSeverity;
    float overrideAngle;

#if UNITY_EDITOR
    /// Fixed noise clock used only by deterministic editor validation scenarios.
    public float EditorTimeOverride { get; set; } = -1f;
#endif

    public void Bind(WindSettings tuning) => settings = tuning;

    /// A MISSING BINDING STOPS THE COMPONENT, IT DOES NOT DROWN THE CONSOLE.
    ///
    /// This used to throw and leave the component ENABLED. Unity logs the exception and carries
    /// on, so `Update` kept running and threw a bare `NullReferenceException` on `settings`
    /// every frame — sixty a second, no object name, no reason. The one line that said what was
    /// actually wrong sat somewhere above thousands of them, and the console reads as if the
    /// error survives a Clear because a fresh flood arrives the moment the game runs again.
    ///
    /// The error is not swallowed: it is louder now, because it names the object and it is the
    /// only line. Disabling is what makes it readable — a component that cannot compute its
    /// wind has nothing to contribute by ticking.
    void OnEnable()
    {
        if (settings != null) return;

        Debug.LogError($"{nameof(WindField)} ('{name}'): {nameof(settings)} atanmamış — "
                       + "bileşen durduruldu. Sahne bootstrap'i bağlamalı "
                       + "(MountainSceneBootstrap.EnsureWeather).", this);
        enabled = false;
    }

    /// For testing, fixes the base severity and the direction; the fluctuation (base oscillation +
    /// gust) keeps working on top. The old lock zeroed the Gust and disabled the component: real
    /// wind never blows flat, so whatever value the slider held, a dead wind was being tested.
    public void ApplyOverride(float strength, float angleDegrees)
    {
        overrideActive = true;
        overrideSeverity = Mathf.Clamp01(strength);
        overrideAngle = angleDegrees * Mathf.Deg2Rad;
    }

    /// Releases the lock: the severity comes from Severity again and the direction from its own drift.
    public void ClearOverride() => overrideActive = false;

    void Update()
    {
        float t = Time.time;
#if UNITY_EDITOR
        if (EditorTimeOverride >= 0f) t = EditorTimeOverride;
#endif

        // The sustained speed comes from Severity alone. The noise no longer produces this value,
        // it produces the deviation riding on top: a full storm regardless of what Perlin gives
        float severity = ShapeSeverity(
            overrideActive ? overrideSeverity : Mathf.Clamp01(Severity));
        float sustained = Mathf.Lerp(settings.calmSpeed, settings.stormSpeed, severity);

        // EXPOSURE SCALES THE SUSTAINED SPEED, not the gust: a ridge speeds the wind up and a
        // hollow cuts it. The gust's proportional structure is the same in both — in a sheltered
        // place the wind breathes too, it just blows small.
        //
        // Disabled while locked: whatever the test slider gives is what it should be, the terrain must not interfere.
        float exposure = 1f;
        if (!overrideActive)
        {
            exposure = Mathf.Lerp(settings.shelteredFactor, settings.exposedFactor,
                                  Mathf.Clamp01(Exposure));
            sustained *= exposure;
        }

        // The slow layer: the air's general state, on the scale of minutes
        float slow = Mathf.PerlinNoise(t * settings.baseFrequency, 0f) * 2f - 1f;

        // The fast layer: gusts, on the scale of seconds. It is squared with the sign preserved:
        // Perlin's symmetric oscillation made the wind look like it was breathing.
        // A real anemometer trace looks like a saw — sharp gusts with calm plateaus in between.
        // Squaring sharpens the peaks and lays the middles into plateaus; the 1.4 puts back the
        // amplitude that is lost.
        float fast = Mathf.PerlinNoise(t * settings.gustFrequency, 37f) * 2f - 1f;
        fast = fast * Mathf.Abs(fast) * 1.4f;

        // The buffet layer: sub-second hits. The gust is a 12 second wave; the 1-3 second
        // turbulence peak that ripples a jacket comes from this layer.
        float flicker = Mathf.PerlinNoise(t * settings.flickerFrequency, 71f) * 2f - 1f;

        // A storm hardens the gust: the same proportional gust is gentle on a calm day and hits
        // fiercely in a storm.
        float buffet = Mathf.Lerp(0.75f, 1.25f, severity);

        // The layers are summed, not multiplied: multiplied, the amplitudes magnify each other and
        // the ceiling rose to 1.75×. As the severity rose the gust exceeded the storm speed, the
        // normalized value was clamped and the wind stopped breathing at the summit.
        // The lower bound is −1: if the sliders are opened all the way at once the speed must not reverse.
        Gust = Mathf.Clamp(slow * settings.baseVariation
             + (fast * settings.gustAmount + flicker * settings.flickerAmount) * buffet,
             -1f, 1f);

        float speed = sustained * (1f + Gust);

        // The direction plays around the PREVAILING axis, it does not turn freely. The snow drift
        // on the surface sits on this axis and if the axis slides the whole pattern drags across
        // the world (see WindSettings.directionSpread).
        float prevailing = PrevailingAngle;
        float wander = Mathf.PerlinNoise(0f, t * settings.directionDrift) * 2f - 1f;

        float angle = overrideActive
            ? overrideAngle
            : prevailing + wander * settings.directionSpread * Mathf.Deg2Rad;

        // The gust moves the direction too: every gust deviates it by a few degrees and the wind
        // wobbles. It holds while locked as well — what is fixed is the axis the gust plays around.
        angle += fast * 0.14f;

        Velocity = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * speed;

        // STRENGTH MEASURES THE WIND ABOVE THE CALM FLOOR, NOT THE SPEED ITSELF.
        //
        // It used to be `sustained / stormSpeed`. That counted the floor as wind, and
        // once the floor was raised to a light breeze (3 m/s, so the coast is not
        // glass) an exposed ridge read 0.311 in DEAD CALM — past the 0.22 threshold
        // that starts drifting snow. The mountain would have been blowing snow on a
        // still day.
        //
        // A light breeze does not drift snow, does not close the fog and is not heard.
        // What those systems ask is "how much wind is there BEYOND the ordinary", and
        // the floor is the ordinary. The exposure scales the floor too, so a sheltered
        // hollow and an exposed ridge both read zero when nothing is blowing — and at
        // the storm end the numbers come back to what they were (sheltered 0.35,
        // exposed 1.0).
        float floorSpeed = settings.calmSpeed * exposure;

        Strength = Mathf.Clamp01((sustained - floorSpeed)
                                 / Mathf.Max(settings.stormSpeed - settings.calmSpeed, 1e-3f));
    }
}
