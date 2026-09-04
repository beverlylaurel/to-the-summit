using System;
using UnityEngine;
using UnityEngine.Rendering;

/// Draws the precipitation in a single draw call. The particle positions are not kept on the
/// CPU; the vertex shader produces them from time + the wind drift.
public class PrecipitationRenderer : MonoBehaviour
{
    public static PrecipitationRenderer Active { get; private set; }

    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] Shader shader;
    [Tooltip("The single source of the cloud layer. Precipitation falls under the thick columns.")]
    [SerializeField] CloudLayerProbe cloudLayer;
    [Tooltip("The point the precipitation is sampled at — the player.")]
    [SerializeField] Transform observer;

    [Tooltip("The per-frame working set of the Garg-Nayar streak database. If empty, rain " +
             "streaks are drawn the old procedural way rather than from the database.")]
    [SerializeField] RainStreakWorkingSet streaks;

    [Tooltip("The source of the sun direction. A streak's look depends strongly on the light " +
             "direction — the paper's Scenario 1 shows the same scene noticeably different at 60° and 10° azimuth.")]
    [SerializeField] TimeOfDay timeOfDay;

    /// The integration time the streak is drawn for (seconds). It sets both the streak's LENGTH
    /// (the database crop) and its TRANSPARENCY: `α = 2r₀/(v·T_exp)`, a short exposure gives a
    /// more opaque streak.
    /// It DOES NOT DERIVE from the frame time — if it did, the rain's look would change with fps.
    ///
    /// WHAT THIS QUANTITY IS: the RETINA's integration time, not a camera shutter. Nobody is
    /// filming this scene; a person is standing in the rain. What makes rain read as streaks
    /// rather than dots is the eye — Bloch's law puts the critical duration at 50-100 ms, and a
    /// moving bright point persists about that long. Setting it to a shutter speed answers a
    /// question nobody asked.
    ///
    /// IT USED TO BE 1/60 s AND THE RAIN READ AS DOTS (reported by the user: "the distant drops
    /// are tiny and drift about like snowflakes"). MEASURED at 60° FOV / 888 px = 769 px/rad,
    /// two frames differenced so only the drops remain: median blob 2.0 x 2.0 px, aspect 1.00.
    /// The model was not wrong — it was obeyed. At 1/60 s the mean drop (1.65 mm, 5.82 m/s)
    /// leaves 9.7 cm, i.e. 3.1 px at 24 m. A 3-pixel mark carries no direction, so its motion
    /// reads as drifting rather than falling. The length WAS the whole complaint.
    ///
    /// The ends, on paper, at 50 ms:
    ///
    ///   0.5 mm   2.02 m/s   10.1 cm    3.3 px at 24 m   15.7 px at 5 m   alpha 0.0049
    ///   1.65 mm  5.82 m/s   29.1 cm    9.3 px at 24 m   44.8 px at 5 m   alpha 0.0057
    ///   5.0 mm   9.14 m/s   45.7 cm   14.6 px at 24 m   70.3 px at 5 m   alpha 0.0109
    ///
    /// THE FADING IS NOT A SIDE EFFECT, IT IS THE PHYSICS. The same light is spread over three
    /// times the pixels, so alpha falls by the same factor — `[Garg 2006]`'s own relation. A long
    /// exposure gives long faint streaks in a real photograph too. Do not "compensate" it.
    ///
    /// `DatabasePeriod` DOES NOT FOLLOW THIS. It is the drop's own oscillation period, a physical
    /// constant; the two being equal at 1/60 was a coincidence. Above one period the texture
    /// repeats — footnote 13 — and the shader already merges copies with `frac`.
    const float ExposureTime = 1f / 20f;

    /// The oscillation period the database was baked at. `T_db = 1/60`, the `2π/ω₂` of an
    /// r₀ = 1.6 mm drop (`rain-spec.md` §5.3).
    const float DatabasePeriod = 1f / 60f;

    /// CALIBRATION — MEASURED. The database streaks are in the radiance of their own render
    /// setup (source at 10 m); our sun is not that source and the absolute level does not carry over.
    /// `rain-spec.md` §11.3.5 makes this coefficient mandatory.
    ///
    /// THE TARGET RATIO IS NOT 1 — THE FIRST CALIBRATION WAS A LOGICAL ERROR.
    ///
    /// The ratio had been set to 3 so it would match the sky behind it. But something at the same
    /// brightness as its background is INVISIBLE by definition: looking at the sky, the background
    /// is the sky and the drop is at the sky's brightness, so the contrast is zero. The user said
    /// "I cannot see the drops when I look up".
    ///
    /// A drop produces no light but it CAN be brighter than its background, and it is: it refracts
    /// into the camera the light it gathers from a wide solid angle (the whole sky dome included),
    /// while the background it covers is a much smaller solid angle. `[Tatarchuk 2006, §3.6.1]`:
    /// "a drop tends to be much brighter than its background".
    ///
    /// On paper the contrast is `α × (ratio − 1)`, with a median α of 0.377:
    ///   ratio 1.0 → 0.00  invisible
    ///   ratio 1.5 → 0.19  weak
    ///   ratio 2.0 → 0.38  clear
    ///   ratio 3.0 → 0.75  strong
    ///
    /// Measured: at ×1 the ratio is 0.16-0.36, at ×3 the median is 0.8. To carry the ratio to 2.0,
    /// 3 × 2.0/0.8 = 7.5.
    const float SourceScale = 7.5f;

    /// PARTICLE DENSITIES, drops/m³. The shader derives the representation share FROM THE
    /// POSITION: `N(r) = 1000 / density(r)`, density = outer + (where the inner box covers) inner.
    ///
    /// The share has to derive from the position: two particles at the same point must represent
    /// the SAME number of real drops regardless of which box they came from.
    ///
    /// The real density of heavy rain is ~1000/m³. In the outer box it is 2.03, and inside the
    /// inner box 2.03 + 14.47 = 16.5 → the representation share drops from 491 to 61. So a near
    /// drop carries a cluster far closer to reality.
    ///
    /// It enters the coverage, not the geometry: `α_eff = 1 − (1−α)^N`.
    static float OuterDensity =>
        (PrecipitationParticles - NearParticles) / (BoxSize.x * BoxSize.y * BoxSize.z);

    static float NearDensity =>
        NearParticles / (NearBoxSize.x * NearBoxSize.y * NearBoxSize.z);

    // The settings are deliberately not serialized: once in the Inspector the component in the
    // scene freezes on the old values and a change in code has no effect.

    /// The precipitation box is 48 m; it wraps with the camera at the centre, so the visible
    /// radius is 24 m.
    ///
    /// HISTORY: 48 → 20 → 12 → 19 → 24 → 32 → 48. Every step was measured, but the first five were
    /// measured while the `widen` exponent was 0.5. When the exponent came down to 0.35 (see
    /// `Precipitation.shader`, the thickness compensation) the table changed and the reason for
    /// narrowing disappeared.
    ///
    /// SWEPT WITH EXPONENT 0.35 (intensity 0.4, 250 000 particles, 20 000 samples):
    ///
    ///    32 m → radius 16 m, median alpha 0.352, invisible 7.2%
    ///    48 m → radius 24 m, median alpha 0.352, invisible 7.2%   ← chosen
    ///    64 m → radius 32 m, median alpha 0.318, invisible 7.7%
    ///   100 m → radius 50 m, median alpha 0.272, invisible 8.3%
    ///
    /// 32 → 48 IS FREE: the opacity does not change at all and the radius grows by 1.5×. The
    /// reason is that the representation share has saturated — as the box grows, both the distance
    /// penalty and the saturation share rise and the two cancel each other out to 48. The cost
    /// begins at 64 m.
    ///
    /// The fill cost does not rise either: the particle count is fixed and the grains shrink on
    /// screen as they get further away.
    ///
    /// THE THICKNESS NO LONGER SETS THE BOX. The box was narrowed for it at one point: a drop's
    /// real thickness only reaches the screen once the quad exceeds the 1.2 pixel raster floor, and
    /// for a 3 mm drop that limit is 2.6 m. But because the volume grows with r³, even at 32 m only
    /// 0.2% of the drops were that close (0.07% at 48 m) — both count as zero. The thickness
    /// gradation on screen is carried not by the real width but by the `pow(widen, -0.35)`
    /// brightness compensation, and that works independently of the distance.
    ///
    /// THE ONLY REAL LIMIT ON GROWING THE RADIUS IS AT 64 m: the opacity drops 10% there.
    /// Anything further is carried not by particles but by the fog's precipitation-driven
    /// visibility (`AtmosphereController`, 18000·R^−0.70).
    static readonly Vector3 BoxSize = new(48f, 48f, 48f);

    /// THE INNER BOX. The precipitation box wraps PERIODICALLY around the camera, and a periodic
    /// tiling cannot carry a density gradient — that is, a single box cannot give "dense near,
    /// sparse far". Because the volume grows with `r³`, almost the whole budget went far away:
    /// in a single 48 m box there were 1 188 particles within 5 metres, i.e. five in a thousand.
    /// Yet that is the volume the player reads as INDIVIDUAL DROPS.
    ///
    /// The answer is nested boxes: both centred on the camera, each uniform within itself and each
    /// wrapping to its own box. Where the inner box covers, the densities are SUMMED, so the near
    /// field grows dense on its own. The motion stays exactly right in both boxes because each is
    /// integrated with its own drift.
    ///
    /// THE EXPONENT WAS SWEPT. First a continuous radial distribution (`density ∝ r^-p`) was
    /// measured; the best was `p = 1`, i.e. `1/r`. Then how much of that the box scheme captures
    /// was measured (intensity 1.0, screen coverage in kilopixels):
    ///
    ///   single box 48        within 5 m  87   total  934
    ///   48 + 12, inner 5%    within 5 m 194   total 1027
    ///   48 + 12, inner 10%   within 5 m 227   total 1033   ← chosen
    ///   48 + 12, inner 20%   within 5 m 265   total 1003   (the median alpha falls)
    ///   48 + 16 + 6          within 5 m 221   total 1061   (a third box is not worth it)
    ///
    /// At 10% the near-field coverage is TWO AND A HALF TIMES larger, the total rises 11% and the
    /// median alpha does not change. At 20% the near field rises a little more but the total and
    /// the median fall.
    static readonly Vector3 NearBoxSize = new(12f, 12f, 12f);

    /// Raised from 90 000. Measured: 90 000 particles in a 48 m cube box means 0.8 drops/m³, while
    /// the real density of heavy rain is ~1000/m³ by Marshall-Palmer. So the old ceiling was
    /// really below even moderate intensity and read as 50% DRIZZLE (the user reported it).
    ///
    /// The count was raised rather than the drop inflated: growing the representation share
    /// thickens the streak and breaks the realism, while the count is directly the quantity that
    /// was missing.
    const int PrecipitationParticles = 250000;

    /// The share falling to the inner box. In the mesh the FIRST `NearParticles` particles are in
    /// the inner box and the rest in the outer; the flag is carried in the vertex position's `y`.
    const int NearParticles = 25000;

    const int ParticleCount = PrecipitationParticles;

    const int PrecipitationSubMesh = 0;
    /// The curve converting intensity into the number of particles drawn — FROM MARSHALL-PALMER.
    ///
    /// In the distribution `N₀` is constant and `Λ = 4.1·R^(−0.21)`, so the total drop count is
    /// `N = N₀/Λ ∝ R^0.21`. As the rain hardens the NUMBER of drops barely rises; what rises is
    /// the drop SIZE. What makes a downpour a downpour is large, fast drops.
    ///
    /// It used to be 1.6, i.e. the count bent hard with the intensity. The intensity already drives
    /// the size through Λ — double counting. Measured: at intensity 0.30 only 14% of the particles
    /// were drawn (36k) and light rain vanished from the screen.
    const float DensityExponent = 0.21f;
    const int MeshSeed = 1;

    // Rain. The drop size sets both the fall speed and the resistance to wind:
    // fine drizzle flies sideways, a large drop comes down steeply. The scales are applied
    // per drop in the shader; the values here are the ends of the band.
    /// Terminal velocity — the Atlas relation of Gunn & Kinzer's measurements:
    ///   `v(D) = 9.65 − 10.3·exp(−0.6·D)`,  D = diameter (mm)
    ///
    /// The diameter band is 0.5-5 mm, so the speed is 2.02-9.14 m/s.
    ///
    /// IT USED TO BE EXAGGERATED TO 16 m/s and the reason was written down: "the particles are
    /// 16-24 m away so the angular speed stays low". That exaggeration belonged to the old visual
    /// model. The STREAK LENGTH is now drawn from the physical terminal velocity (`v·T_exp`); if
    /// the motion runs at a different speed the drop leaves a streak shorter than the path it
    /// travelled — measured, 16 against 9.14, so the streak was 57% of the real path.
    ///
    /// The speed cannot be computed per drop: the wind drift is integrated per class on the CPU.
    /// It derives from the class's representative radius, the same formula as in the shader.
    static float TerminalVelocity(float t)
    {
        float diameterMm = 0.5f + 4.5f * t;
        return 9.65f - 10.3f * Mathf.Exp(-0.6f * diameterMm);
    }
    /// THE WIND'S BOUNDARY LAYER IS IN THE SHADER, NOT HERE.
    ///
    /// The drift is integrated per class here and a single vector cannot vary with height. One
    /// attempt set up four height bands per class; it was ELIMINATED BY MEASUREMENT: the bands'
    /// drifts diverge without bound over time (101 m in 30 s) and once wrapped to the box the
    /// difference between them turns into a random number. As a falling drop crossed from one band
    /// to another, that difference rode on it as a false horizontal speed of up to 21 m/s.
    ///
    /// The right way is in closed form and per drop: `Precipitation.shader`, around `WIND_LAG_TOP`.
    /// Only the FREE STREAM drift lives here.
    const float RainWindFactor = 0.85f;   // the share of the wind a large drop takes
    const float RainWindLightFactor = 1f; // a fine drop takes the wind in full
    // Were the speed continuous, every drop would multiply the drift by a different scale and the
    // wrap point would stop being a multiple of the box and make the drops jump. A separate drift
    // is kept per class.
    const int RainSpeedClasses = 8;
    static readonly int BoxSizeId = Shader.PropertyToID("_BoxSize");
    static readonly int StreakPointId = Shader.PropertyToID("_StreakPoint");
    static readonly int StreakMaskId = Shader.PropertyToID("_StreakMask");
    static readonly int StreakCellBlendId = Shader.PropertyToID("_StreakCellBlend");
    static readonly int StreakCornerPresentId = Shader.PropertyToID("_StreakCornerPresent");
    static readonly int StreakMirrorId = Shader.PropertyToID("_StreakMirror");
    static readonly int StreakDcamFractionId = Shader.PropertyToID("_StreakDcamFraction");
    static readonly int StreakExposureId = Shader.PropertyToID("_StreakExposure");
    static readonly int StreakDbPeriodId = Shader.PropertyToID("_StreakDbPeriod");
    static readonly int StreakSourceScaleId = Shader.PropertyToID("_StreakSourceScale");
    static readonly int StreakSunRadianceId = Shader.PropertyToID("_StreakSunRadiance");
    static readonly int RainDensityId = Shader.PropertyToID("_RainDensity");

    static readonly int RainDriftsId = Shader.PropertyToID("_RainDrifts");
    static readonly int RainDriftsNearId = Shader.PropertyToID("_RainDriftsNear");
    static readonly int NearBoxSizeId = Shader.PropertyToID("_NearBoxSize");
    static readonly int RainDirectionsId = Shader.PropertyToID("_RainDirections");

    /// The atmosphere writes it, here it is only READ: so that if the wind threshold has not been
    /// crossed the dust submesh is not drawn at all. Setting up a second threshold computation
    /// would split the two systems.
    static readonly int DensityId = Shader.PropertyToID("_Density");
    static readonly int PrecipitationId = Shader.PropertyToID("_Precipitation");
    Mesh mesh;
    Material material;
    readonly Vector4[] rainDrifts = new Vector4[RainSpeedClasses];
    readonly Vector4[] rainDriftsNear = new Vector4[RainSpeedClasses];
    readonly Vector4[] rainDirections = new Vector4[RainSpeedClasses];
    readonly Vector3[] rainVelocities = new Vector3[RainSpeedClasses];
    float density;
    float precipitation;

    /// FOR THE F1 PANEL. The intensity and the density are read separately: with something on
    /// screen there is no other way to tell which of the two it came from.
    public float DebugRainIntensity => precipitation;
    public float DebugDensity => density;
    float localFactor = 1f;

    /// The precipitation no longer falls from the sky as one sheet: its source is the cloud column
    /// overhead. A one-way, read-only link to the cloud system — the precipitation does not ask
    /// which cloud is raining, it only reads "how much is above me right now".
    public void Bind(WeatherState state, WindField windField, Shader precipitationShader,
                     CloudLayerProbe layer, Transform eye,
                     RainStreakWorkingSet streakSet, TimeOfDay clock)
    {
        weather = state;
        wind = windField;
        shader = precipitationShader;
        cloudLayer = layer;
        observer = eye;
        streaks = streakSet;
        timeOfDay = clock;
    }

    void OnEnable()
    {
        if (weather == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(weather)} is not assigned.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(wind)} is not assigned.");
        if (shader == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(shader)} is not assigned.");
        if (cloudLayer == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(cloudLayer)} is not assigned.");
        if (observer == null)
            throw new InvalidOperationException($"{nameof(PrecipitationRenderer)}: {nameof(observer)} is not assigned.");

        Active = this;

        RefreshDensity();

        // If the filtered speed starts from zero the direction vector normalizes to zero in the
        // first frames and produces NaN in the shader; it is initialized with the fall speed.
        for (int i = 0; i < RainSpeedClasses; i++)
        {
            float t = i / (RainSpeedClasses - 1f);
            rainVelocities[i] = Vector3.down * TerminalVelocity(t);
            rainDirections[i] = WithSpeed(rainVelocities[i]);
        }
    }

    void OnDisable()
    {
        if (Active == this) Active = null;
    }

    /// The direction and the MAGNITUDE in one vector: `xyz` the unit direction, `w` the resultant
    /// speed (m/s).
    ///
    /// The shader needs the magnitude because the boundary-layer lag is a distance derived from
    /// wind speed. A normalized direction alone would lose that quantity.
    static Vector4 WithSpeed(Vector3 velocity)
    {
        float speed = velocity.magnitude;
        Vector3 direction = speed > 1e-4f ? velocity / speed : Vector3.down;
        return new Vector4(direction.x, direction.y, direction.z, speed);
    }

    /// PREPARES THE STREAK DATABASE PER FRAME — `[Garg 2006, §5]`.
    ///
    /// All three angles are determined here and all three depend on something DIFFERENT:
    ///   the light's elevation — the sun's angle relative to the drop's fall axis
    ///   the light's azimuth   — the component of that angle relative to the camera's axis
    ///   `θ_v`                 — the angle between the camera's view and the fall direction (in the
    ///                           shader, per drop, because it differs across the screen)
    void UpdateStreaks(Vector3 rainVelocity)
    {
        if (streaks == null || timeOfDay == null) return;

        var camera = Camera.main;
        if (camera == null) return;

        Vector3 fall = rainVelocity.sqrMagnitude > 1e-8f
            ? rainVelocity.normalized
            : Vector3.down;

        streaks.Refresh(timeOfDay.PrimaryLightDirection, fall, camera.transform.forward);

        if (streaks.Point == null || streaks.Mask == null) return;

        material.SetTexture(StreakPointId, streaks.Point);
        material.SetTexture(StreakMaskId, streaks.Mask);
        material.SetVector(StreakCellBlendId, streaks.CellBlend);
        material.SetVector(StreakCornerPresentId, streaks.CornerPresent);
        material.SetFloat(StreakMirrorId, streaks.MirroredAzimuth ? 1f : 0f);
        material.SetFloatArray(StreakDcamFractionId, streaks.DcamHeightFraction);
        material.SetFloat(StreakExposureId, ExposureTime);
        material.SetFloat(StreakDbPeriodId, DatabasePeriod);
        // DIRECT CELESTIAL RADIANCE. Day and night use the same light that illuminates the scene;
        // continuing to read the sun after the moon handover made rain go black at night.
        Color source = timeOfDay.PrimaryLightColor * timeOfDay.PrimaryLightIntensity;
        material.SetVector(StreakSunRadianceId,
            new Vector4(source.r, source.g, source.b, timeOfDay.PrimaryLightIntensity));

        material.SetFloat(StreakSourceScaleId, SourceScale);
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (mesh != null) Destroy(mesh);
    }

    /// THE DENSITY IS PER FRAME, NOT ON AN EVENT.
    ///
    /// It used to be computed once in the `WeatherState.Changed` event. But one of its
    /// inputs is `SnowRuntimeState.RainWeight01` and that changes with the snow fraction
    /// slider — and the slider PUBLISHES NO weather event. The result: even with the snow
    /// fraction at 1, the rain kept being drawn at the density of the last event
    /// (measured: with `RainWeight01 = 0` the screen was full of rain streaks).
    void RefreshDensity()
    {
        WeatherState state = weather;
        if (state == null) return;

        // WHILE SNOW FALLS THE RAIN GOES QUIET (snow spec §3.4, §17.1).
        //
        // `SnowRuntimeState` is the state the snow system PUBLISHES; reading it is not
        // a call between systems but a declared interface. The snow system reads nothing
        // from here either — the link is one-way.
        float rainIntensity = state.Precipitation * SnowRuntimeState.RainWeight01;

        // MARSHALL-PALMER IS INVALID AT ZERO, A GATE IS MANDATORY.
        //
        // The `N ∝ R^0.21` curve is very steep near zero: even at intensity 0.001 the density comes
        // out 0.234, i.e. a quarter of the particles are drawn. The weather APPROACHES zero with
        // smoothing but never settles on it; the result was rain on screen while the panel showed
        // "precipitation 0.00" (the user reported it, seen with the diameter probe).
        //
        // The relation is right for R > 0, but at R → 0 the precipitation EVENT itself has to end.
        // The gate is in the intensity's lowest slice: below 0.05 is not even drizzle (R < 2.5 mm/h),
        // and the drop count goes to zero there.
        density = Mathf.Pow(rainIntensity, DensityExponent)
                * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.05f, rainIntensity));

        precipitation = rainIntensity;
    }

    void Update()
    {
        EnsureResources();
        RefreshDensity();

        // LINK 4: the precipitation is scaled by THAT COLUMN's coverage — so it can rain under this
        // one while the neighbouring cloud does not. Above the column's top it does not rain at all;
        // the ceiling cut is `AltitudeWeatherDriver`'s job, only the horizontal distribution is here.
        localFactor = cloudLayer.CoverageAt(observer.position);

        // Fine drizzle falls slowly and takes the wind in full, a large drop comes down fast and
        // resists. Every angle in between fills in on its own.
        //
        // A drop cannot follow a change in the wind instantly: the relaxation time is terminal
        // velocity / g — fine drizzle turns into a gust quickly, a large drop late. Taken without
        // filtering, every gust laid the whole rain over in the same frame as one sheet: a cardboard
        // curtain. Both the position and the streak direction derive from the same filtered speed;
        // if they diverge the streak stretches in a direction other than the one the drop is going.
        for (int i = 0; i < RainSpeedClasses; i++)
        {
            float t = i / (RainSpeedClasses - 1f);
            float fallSpeed = TerminalVelocity(t);
            Vector3 target = Vector3.down * fallSpeed
                             + wind.Velocity * Mathf.Lerp(RainWindLightFactor, RainWindFactor, t);

            float blend = 1f - Mathf.Exp(-Time.deltaTime * 9.81f / fallSpeed);
            rainVelocities[i] = Vector3.Lerp(rainVelocities[i], target, blend);

            rainDrifts[i] = Advance(rainDrifts[i], rainVelocities[i], BoxSize);
            rainDriftsNear[i] = Advance(rainDriftsNear[i], rainVelocities[i], NearBoxSize);
            rainDirections[i] = WithSpeed(rainVelocities[i]);
        }
        // smaller, and settles on the air's speed instantly. There is no vertical drift, because a
        // grain's height above the ground derives from the terrain surface, not from the box.

        material.SetVector(BoxSizeId, BoxSize);
        // UNCONDITIONAL: the representation share derives from here and there are four early exits
        // ahead of `UpdateStreaks`. If the uniform is not written the HLSL default is zero and the
        // density collapses to the floor.
        material.SetVector(RainDensityId, new Vector4(OuterDensity, NearDensity, 0f, 0f));
        material.SetVector(NearBoxSizeId, NearBoxSize);
        material.SetVectorArray(RainDriftsId, rainDrifts);
        material.SetVectorArray(RainDriftsNearId, rainDriftsNear);
        material.SetVectorArray(RainDirectionsId, rainDirections);
        material.SetFloat(DensityId, density * localFactor);
        material.SetFloat(PrecipitationId, precipitation * localFactor);

        UpdateStreaks(rainVelocities[RainSpeedClasses / 2]);

    }

    /// The drift accumulates; it wraps to a multiple of its own box to preserve float precision.
    /// Wrapping to the wrong box makes the particles slide across the world.
    static Vector3 Advance(Vector3 drift, Vector3 velocity, Vector3 box)
    {
        drift += velocity * Time.deltaTime;
        return new Vector3(
            Mathf.Repeat(drift.x, box.x),
            Mathf.Repeat(drift.y, box.y),
            Mathf.Repeat(drift.z, box.z));
    }

    /// A recompile in Play mode can drop the mesh and the material; they are verified at the point of use.
    void EnsureResources()
    {
        if (mesh == null) mesh = BuildMesh();
        if (material == null) material = new Material(shader);
    }

    /// DRAWN DIRECTLY, AND ONLY WHEN THERE IS SOMETHING TO DRAW. Below the density threshold
    /// nothing is submitted at all: with no precipitation, 250 000 quads never enter the vertex
    /// shader. Drawn unconditionally, every particle of a system that is off was processed and then
    /// culled at zero size — the full cost of something invisible.
    ///
    /// The renderer feature calls this after the volumetric cloud composite. Drawing through the
    /// normal transparent queue puts the rain underneath that full-screen pass, which erases every
    /// drop over sky pixels and leaves only drops backed by terrain visible.
    public void DrawAfterClouds(RasterCommandBuffer command)
    {
        if (!isActiveAndEnabled || material == null || mesh == null) return;
        if (density * localFactor <= 0.0005f) return;

        command.DrawMesh(mesh, Matrix4x4.identity, material, PrecipitationSubMesh, 0);
    }

    /// Every particle is a quad. The corner information is in UV0, the particle seed in UV1/UV2, and
    /// the particle TYPE in the vertex position's x. The position channel is not otherwise used; the
    /// shader produces the world position from the seed.
    Mesh BuildMesh()
    {
        int vertexCount = ParticleCount * 4;

        var positions = new Vector3[vertexCount];
        var corners = new Vector2[vertexCount];
        var seedXY = new Vector2[vertexCount];
        var seedZW = new Vector2[vertexCount];
        var indices = new int[ParticleCount * 6];

        var random = new System.Random(MeshSeed);

        for (int i = 0; i < ParticleCount; i++)
        {
            var xy = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
            var zw = new Vector2((float)random.NextDouble(), (float)random.NextDouble());

            // The inner box flag is in `y`; `x` and `z` stay empty. `x` used to carry a particle
            // TYPE (0 precipitation / 1 drifting snow), but `ParticleCount` equals
            // `PrecipitationParticles`, so the second population was always empty and the shader
            // never read the channel. Removed rather than left as a flag nothing sets.
            float near = i < NearParticles ? 1f : 0f;

            int v = i * 4;
            corners[v + 0] = new Vector2(0f, 0f);
            corners[v + 1] = new Vector2(1f, 0f);
            corners[v + 2] = new Vector2(1f, 1f);
            corners[v + 3] = new Vector2(0f, 1f);

            for (int c = 0; c < 4; c++)
            {
                positions[v + c] = new Vector3(0f, near, 0f);
                seedXY[v + c] = xy;
                seedZW[v + c] = zw;
            }

            int t = i * 6;
            indices[t + 0] = v + 0;
            indices[t + 1] = v + 1;
            indices[t + 2] = v + 2;
            indices[t + 3] = v + 0;
            indices[t + 4] = v + 2;
            indices[t + 5] = v + 3;
        }

        var built = new Mesh { name = "Precipitation", indexFormat = IndexFormat.UInt32 };
        built.SetVertices(positions);
        built.SetUVs(0, corners);
        built.SetUVs(1, seedXY);
        built.SetUVs(2, seedZW);
        built.subMeshCount = 1;
        built.SetIndices(indices, 0, PrecipitationParticles * 6,
                         MeshTopology.Triangles, PrecipitationSubMesh, false);

        // Because the positions are produced in the shader the computed bounds are meaningless; culling is off
        built.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        return built;
    }
}
