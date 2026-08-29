using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Drives the colour grading according to the weather and the time of day.
/// The weather system and the day cycle do not know each other; the two are consumed here.
[ExecuteAlways]
[RequireComponent(typeof(Volume))]
public class LookController : MonoBehaviour
{
    /// EXPOSURE ADAPTATION. The eye opens up in the dark but does not close the whole
    /// difference; `adaptShare` is the share it closes, `rodCeilingEV` is where that opening
    /// SATURATES.
    ///
    /// IT IS A SATURATION, NOT A CLIP, and that difference is the whole reason the number
    /// changed. It used to be `Mathf.Clamp(..., 0f, 2.5f)`.
    ///
    /// MEASURED over a full day: the raw target is 2.60 EV at midnight, 2.78 at 03:00, 3.16 at
    /// 05:00 and 3.30 at 05:30. Every one of them is above 2.5 — so EVERY NIGHT HOUR came out
    /// at exactly 2.50. Three and a half stops of real sky change, the moon rising, the moon
    /// setting, the dark hour before dawn, all rendered at one single exposure. This is the same
    /// trap the LOWER bound already had (a clamp at 0.02 flattened twilight into one level); it
    /// was sitting at the other end of the range and nobody had looked at that end.
    ///
    /// `tanh` closes it and touches nothing else. Below the ceiling it IS the linear share, so
    /// noon, dawn and the golden hour come out as before to three decimals; above it the curve
    /// bends over, never crosses the ceiling, and the ordering survives however dark it gets.
    ///
    /// THE CEILING IS PHYSICS, THE SHARE IS DESIGN. Rhodopsin regeneration is worth 6-7 stops of
    /// gain in a real eye, and that gain is what saturates. How much of it this game spends is
    /// `adaptShare`, a deliberate compression. Tangled into one clipped number the two could not
    /// be reasoned about separately, which is why the clipping went unnoticed.
    ///
    /// WHAT IT COSTS, measured: midnight 2.50 -> 2.47, 03:00 2.50 -> 2.55, 05:30 2.50 -> 3.05,
    /// noon 0.098 -> 0.098. The night gets its ordering back and the day does not move.
    [Header("Pozlama uyumu")]
    [Tooltip("Where opening to darkness saturates (EV) — rhodopsin's own ceiling.")]
    [SerializeField, Range(0f, 10f)] float rodCeilingEV = 6.5f;

    /// IT STAYS AT 0.35. It was raised to 0.60 for a while: the night sky sat at the bottom of
    /// the tone curve, and while the data varied by less than one stop across the whole field of
    /// view the screen split into "a black region / a normal sky", and +1 EV closed that.
    ///
    /// BUT EXPOSURE IS THE WRONG TOOL. Lifting the dark end lifted the bright end too; snow in
    /// moonlight and the whole night scene became brighter than they should be. What lifts the
    /// dark end on its own is the tone curve: the contrast was lowered in the night profile.
    [Tooltip("The share of the light difference that is closed. 1 = full normalization, which turns dawn into noon.")]
    [SerializeField, Range(0f, 1f)] float adaptShare = 0.35f;

    /// The time constant of opening to darkness (seconds). Rhodopsin regeneration is slow.
    const float AdaptToDarkSeconds = 2.5f;

    /// The time constant of closing to light (seconds). The pupil closes fast.
    const float AdaptToLightSeconds = 0.5f;

    /// The current exposure adaptation. It reaches the target by smoothing.
    float adapt;

    /// Below noon, in stops, where rod vision starts and where it has fully taken over.
    ///
    /// THE BAND IS PLACED ON THIS GAME'S RANGE, NOT ON cd/m2, and that is deliberate rather
    /// than lazy. A real day spans about 18 stops from noon to a moonless night; this one spans
    /// 9.4 (measured: 0.28 stops below noon at midday, 9.45 at the darkest hour). Anchored to
    /// real luminance the mesopic band would sit around 15 stops down and the term would never
    /// fire at all, because the game's night is roughly a thousand times brighter than a real
    /// one relative to its own noon.
    ///
    /// Measured weights that come out of this placement: 0.66 at midnight with the moon up —
    /// which is right, a full moon on snow really is mesopic — and 1.0 in the hour after dusk
    /// before the moon rises, the genuinely darkest part of the cycle.
    const float ScotopicOnsetStops = 5f;
    const float ScotopicFullStops = 9f;

    /// Sine of -6 degrees: the end of civil twilight, and the earliest the rods can take over.
    const float CivilTwilightSine = -0.1045f;

    /// The floor under the light level. IT ENGAGES, AND WHERE IT ENGAGES IS A BUG.
    ///
    /// Measured in Play: the level is 0.0058 at midnight and 0.0018 before dawn, all well
    /// above the floor — and then 0.0002 AT SUNSET, below it. The floor is doing its job
    /// (`log2(0)` is negative infinity) but the reason it is reached is that the sky probe
    /// collapses at the horizon, not that the scene is dark. At 17:30 the sky term reads 0.25;
    /// half an hour later it reads 0.0002 while the drawn sky is blazing orange.
    ///
    /// SYMPTOMS.md, "Gokyuzu probu gun dogumunda ve batiminda cokuyor".
    const float LightLevelFloor = 0.0005f;

    /// WHAT THE ADAPTATION IS DOING RIGHT NOW. Read by the F1 panel; the chain is four
    /// multiplications deep and every question about it ("is the sky or the sun driving this",
    /// "is the ceiling engaging", "why is the night flat") needs the intermediate values.
    public float LightLevel { get; private set; } = 1f;
    public float SunTermNormalized { get; private set; } = 1f;
    public float SkyTermNormalized { get; private set; }
    public float AdaptationEV => adapt;
    public float ScotopicWeight { get; private set; }

    [SerializeField] LookSettings look;
    [SerializeField] WeatherState weather;
    [SerializeField] TimeOfDay time;

    [Header("Preview (editor only)")]
    [Tooltip("While on, the values below are used instead of the weather and clock systems.")]
    [SerializeField] bool preview;
    [SerializeField, Range(0f, 1f)] float previewStorm = 0.8f;
    [SerializeField, Range(0f, 1f)] float previewDay = 0.6f;


    ColorAdjustments colorAdjustments;
    WhiteBalance whiteBalance;
    ShadowsMidtonesHighlights shadows;
    Bloom bloom;
    FilmGrain filmGrain;
    Tonemapping tonemapping;

    public LookSettings Look => look;

    public void Bind(LookSettings settings, WeatherState weatherState, TimeOfDay timeOfDay)
    {
        look = settings;
        weather = weatherState;
        time = timeOfDay;

        Initialize();
    }

    /// For driving the preview values from outside (the settings window uses it)
    public void SetPreview(bool enabled, float storm, float day)
    {
        preview = enabled;
        previewStorm = storm;
        previewDay = day;
        Apply();
    }

    /// Because of ExecuteAlways, OnEnable runs the moment AddComponent is called — at which
    /// point Bind may not have been called yet. So the setup happens in whichever comes first.
    void OnEnable() => Initialize();

    void Initialize()
    {
        if (look == null) return;

        EnsureOverrides();
        Apply();
    }

    void Update() => Apply();

    /// The effects needed are added to the Volume profile if they are missing. The profile lives
    /// on disk as an asset, so they are added once and stay.
    void EnsureOverrides()
    {
        var profile = GetComponent<Volume>().profile;
        if (profile == null)
            throw new InvalidOperationException($"{nameof(LookController)}: Volume profili yok.");

        colorAdjustments = Ensure<ColorAdjustments>(profile);
        whiteBalance = Ensure<WhiteBalance>(profile);
        shadows = Ensure<ShadowsMidtonesHighlights>(profile);
        bloom = Ensure<Bloom>(profile);
        filmGrain = Ensure<FilmGrain>(profile);
        tonemapping = Ensure<Tonemapping>(profile);

        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;
    }

    /// The ambient probe's brightness in the zenith direction. The sky package bakes the probe
    /// from the sky every frame, so this is the scene's REAL sky brightness.
    static readonly Vector3[] ZenithDirection = { Vector3.up };
    static readonly Color[] ZenithResult = new Color[1];

    static float AmbientZenithLuminance()
    {
        RenderSettings.ambientProbe.Evaluate(ZenithDirection, ZenithResult);

        Color zenith = ZenithResult[0];
        return zenith.r * 0.2126f + zenith.g * 0.7152f + zenith.b * 0.0722f;
    }

    static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        => profile.TryGet(out T component) ? component : profile.Add<T>(true);

    void Apply()
    {
        if (look == null || colorAdjustments == null) return;

        float storm, day, horizon;

        if (preview || weather == null || time == null)
        {
            storm = previewStorm;
            day = previewDay;
            horizon = 0f;
        }
        else
        {
            storm = Mathf.Clamp01(weather.Precipitation);
            day = time.DayFactor;
            horizon = time.HorizonFactor;
        }

        var profile = look.Evaluate(storm, day, horizon);

        // EXPOSURE ADAPTATION. The light now comes from physics and at dawn the beam
        // transmittance falls to 0.11 and at night to zero — the physics is right, but at a fixed
        // exposure the screen stays dim. In real life the reason dawn clouds look bright is not
        // that the light is strong but that the eye OPENS UP to that darkness; in photography the
        // exposure is set for the sky too.
        //
        // The level is not read from the screen: we already know the scene's light (beam + sky).
        // Noon is 0 EV and it opens upward in the dark hours. Log2 because exposure is in EV.
        // THE SOURCE CHANGED: it used to be read from the `Atmosphere` model (`BeamLevel`,
        // `SkyLevel`, `MoonLevel`). That model NO LONGER DRIVES THE LIGHT — the absorption
        // belongs to the sky package and the model only feeds the fog and cloud tone. The
        // exposure was opening and closing according to a model that does not light the scene; at
        // dawn the light was at full strength while the model still said "dark", so it opened extra.
        //
        // Two REAL quantities are read now, both normalized to 1 at noon:
        //   sun — the directional light's intensity / a calibration constant
        //   sky — the ambient probe's zenith brightness / the noon measurement
        //
        // The ends on paper: noon 1 → adaptation 0 EV. At night the sun is ~0.07 and the sky ~0.03
        // → the 0.6 EV cap. At dawn the sun goes to 1 the moment it crosses the horizon → adaptation 0, no extra opening.
        const float ReferenceSunIntensity = 3.030782f;
        const float ReferenceSkyLuminance = 0.148f;

        // `SurfaceLightLevel` sums the two bodies' contribution reaching FLAT GROUND, each
        // multiplied by its own elevation. The intensity alone was misleading: with the sun below
        // the horizon its intensity is still large but none of it reaches the ground (`N·L` is
        // negative). Without the multiplier the adaptation stayed at 0.81 EV at 18:30 and the scene looked pitch black.
        SunTermNormalized = time != null ? time.SurfaceLightLevel / ReferenceSunIntensity : 1f;
        SkyTermNormalized = AmbientZenithLuminance() / ReferenceSkyLuminance;

        float lightLevel = time != null
            ? Mathf.Max(SunTermNormalized, SkyTermNormalized)
            : 1f;

        LightLevel = lightLevel;

        // How far below noon the scene is, in stops. Everything below reads this.
        float stops = Mathf.Max(0f, -Mathf.Log(Mathf.Max(LightLevelFloor, lightLevel), 2f));

        // THE ADAPTATION IS PARTIAL. Closing the whole difference (full normalization) turned
        // dawn into noon — Unreal's documentation describes the same trap: with the lower bound
        // kept low the camera takes a night scene for "underexposed" and shows it like daylight.
        // The eye does not work that way either: it opens to the dark but closes only about half
        // the difference, and the rest STAYS dark.
        //
        // THE LOWER BOUND WENT 0.02 → 0.0005. `lightLevel` used to be clamped at 0.02, so the
        // adaptation could see at most 5.6 stops; real night is far below that and the clamp
        // flattened twilight into a single level.
        //
        // AND THE UPPER BOUND WAS DOING THE SAME THING AT THE OTHER END. See `rodCeilingEV`:
        // the hard clamp is gone and the opening saturates INTO the ceiling instead of hitting
        // it flat.
        float adaptTarget = rodCeilingEV
                          * (float)System.Math.Tanh(adaptShare * stops / Mathf.Max(0.01f, rodCeilingEV));

        // THE EYE DOES NOT ADAPT INSTANTLY, AND NOT AT THE SAME RATE IN BOTH DIRECTIONS.
        //
        // The target was written directly: when lightning struck or when stepping
        // from shade into the sun the screen brightness jumped IN A SINGLE FRAME.
        //
        // In the human eye, adaptation to darkness (rhodopsin regeneration) is many
        // times slower than adaptation to light. Symmetric smoothing averages the two
        // and both come out wrong.
        //
        // If `adapt` is RISING we are opening to the dark — slow. If it is falling we
        // are closing to the light — fast.
        float tau = adaptTarget > adapt ? AdaptToDarkSeconds : AdaptToLightSeconds;
        adapt = Mathf.Lerp(adapt, adaptTarget,
                           1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, tau)));

        ScotopicWeight = ScotopicWeightOf(stops);

        Set(colorAdjustments.postExposure, profile.exposure + adapt);
        Set(colorAdjustments.contrast, profile.contrast);

        // ROD VISION HAS NO COLOUR AT ALL. The night profiles already drain some of it (-20
        // clear, -36 storm) and that hand-tuning stays; what it could not do is respond to the
        // LIGHT. Keyed to `DayFactor` it is a clock ramp — it fires on time rather than on
        // darkness, and it cannot tell a moonlit night from the black hour before moonrise.
        //
        // THE SHARE IS `adaptShare`, ON PURPOSE. The same number that says how much of the
        // eye's exposure gain this game spends now says how much of its chroma loss it spends,
        // and it is taken out of the chroma that is LEFT after the profile rather than piled on
        // top of it. One compression, two axes. If the two ever need to move apart they split
        // into two fields; the trigger for that is written in DECISIONS.md.
        Set(colorAdjustments.saturation,
            Mathf.Max(-100f, profile.saturation
                             - adaptShare * (100f + profile.saturation) * ScotopicWeight));
        Set(colorAdjustments.colorFilter, profile.colorFilter);

        Set(whiteBalance.temperature, profile.temperature);
        Set(whiteBalance.tint, profile.tint);

        // NO VIGNETTE. Darkening the screen corners puts a wall between the player and the world
        // and it shows while moving. Focusing on the centre is done by physics here: aerial
        // perspective, three-layer fog, blueing with distance.
        //
        // In its place, SHADOW COOLING: gloom comes from the shade. Everything left in shadow
        // turns blue and heavy while a sunlit surface keeps its warmth — the contrast is not in
        // brightness but in COLOUR. A global temperature shift cannot do this, it would cool the dawn as well.
        shadows.shadows.overrideState = true;
        shadows.shadows.value = new Vector4(
            Mathf.Lerp(1f, 0.92f, profile.shadowChill),
            Mathf.Lerp(1f, 0.97f, profile.shadowChill),
            Mathf.Lerp(1f, 1.10f, profile.shadowChill), 0f);

        shadows.highlights.overrideState = true;
        shadows.highlights.value = new Vector4(
            Mathf.Lerp(1f, 1.02f, profile.shadowChill), 1f,
            Mathf.Lerp(1f, 0.97f, profile.shadowChill), 0f);

        Set(bloom.intensity, profile.bloom);
        Set(bloom.threshold, profile.bloomThreshold);

        Set(filmGrain.intensity, profile.grain);
    }

    /// HOW FAR INTO ROD-ONLY VISION THE SCENE IS, 0..1. Two conditions the eye genuinely
    /// requires, multiplied together.
    ///
    /// THE LIGHT HAS TO BE LOW — `ScotopicOnsetStops`..`ScotopicFullStops` below noon.
    ///
    /// AND THE SUN HAS TO BE DOWN. Rods only take over after civil twilight, sun below -6
    /// degrees. This is not a guard bolted on to protect the sunset, it is the definition:
    /// civil twilight is photopic, you can still read a newspaper in it, and a grade that
    /// drained the colour out of a sunset would be wrong even if the light level agreed.
    ///
    /// It also happens to make the term immune to the hole the light chain has at the horizon
    /// (SYMPTOMS.md, "gokyuzu probu gun dogumunda ve batiminda tam sifir"): the sky probe reads
    /// EXACTLY zero from sun height +0.01 down to -0.07, some forty minutes of clock, which the
    /// level ramp on its own would read as the darkest moment of the whole day — in the middle
    /// of the golden hour.
    float ScotopicWeightOf(float stops)
    {
        float level = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(ScotopicOnsetStops, ScotopicFullStops, stops));

        if (time == null) return level;

        float sunDown = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(0f, CivilTwilightSine, time.SunHeight));

        return level * sunDown;
    }

    static void Set(FloatParameter parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    static void Set(ClampedFloatParameter parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    static void Set(ColorParameter parameter, Color value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    static void Set(MinFloatParameter parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }
}
