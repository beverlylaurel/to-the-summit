using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Derives the fog, the ambient light and the sky from a single colour.
/// Tuned separately they produced a hard boundary at the horizon and a "painted wall" feel.
///
/// It reads the weather, the wind and the clock; because the audio and the colour grading read the
/// same sources, the three cannot contradict.
[ExecuteAlways]
public class AtmosphereController : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;
    [SerializeField] TimeOfDay time;
    [Tooltip("For the clear window signal only. That is the only thing that can pierce the " +
             "coverage's permanent floor; no other value is read from here.")]
    [SerializeField] AltitudeWeatherDriver weatherDriver;
    [SerializeField] Camera view;

    [Tooltip("All of the look settings. As long as they sit on the component there is a " +
             "second copy of the value in the scene, and when Unity rewrote the scene to disk " +
             "from its own memory a fix made in code disappeared silently.")]
    [SerializeField] AtmosphereSettings settings;

    [SerializeField] Material skyMaterial;

    /// The surface illuminance (irradiance) → participating medium (radiance) conversion. It is π
    /// and it was measured in this project: with the probe's DC luminance at 0.156 the fog colour
    /// was 0.492, a ratio of 3.15.
    const float AmbientToMedium = 3.15f;

    static readonly int HeightFogColorId = Shader.PropertyToID("_HeightFogColor");
    static readonly int HeightFogDensityId = Shader.PropertyToID("_HeightFogDensity");
    static readonly int HeightFogFalloffId = Shader.PropertyToID("_HeightFogFalloff");
    static readonly int HeightFogBaseId = Shader.PropertyToID("_HeightFogBase");
    static readonly int FogSeaDensityId = Shader.PropertyToID("_FogSeaDensity");
    static readonly int FogSeaFalloffId = Shader.PropertyToID("_FogSeaFalloff");
    static readonly int FogInversionHeightId = Shader.PropertyToID("_FogInversionHeight");
    static readonly int FogInversionWidthId = Shader.PropertyToID("_FogInversionWidth");
    static readonly int FogFreeDensityId = Shader.PropertyToID("_FogFreeDensity");

    /// The raw lifting share (0-1), before it is multiplied by the density. The surface reads it
    /// and sweeps the snow off the ground: the threshold rule lives here so it is not set up a second time there.

    /// The RAW wind: direction × speed (m/s), w the instantaneous gust.
    /// For the snow only, and with the drift threshold applied on the CPU — zero in a light gust
    /// that lifts no snow. The vegetation reads the raw wind: leaves move in that gust too.
    static readonly int WindVectorId = Shader.PropertyToID("_WindVector");
    static readonly int FogFreeFalloffId = Shader.PropertyToID("_FogFreeFalloff");
    static readonly int FogBankDriftId = Shader.PropertyToID("_FogBankDrift");
    static readonly int FogBankStrengthId = Shader.PropertyToID("_FogBankStrength");
    static readonly int HeightFogShadowColorId = Shader.PropertyToID("_HeightFogShadowColor");
    static readonly int HeightFogZenithId = Shader.PropertyToID("_HeightFogZenith");
    static readonly int HeightFogSunColorId = Shader.PropertyToID("_HeightFogSunColor");
    static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
    static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    static readonly int MoonColorId = Shader.PropertyToID("_MoonColor");

    static readonly int PlanetRadiusId = Shader.PropertyToID("_PlanetRadius");



    float visibility;

    /// The visibility corresponding to the fog's REAL density. `visibility` is the air's target;
    /// the valley multiplier and the dawn sea's floor ride on top of it, so this is the distance
    /// seen on screen and the one the clouds blend at.
    float effectiveVisibility;

    /// The visibility of the SETTLED air only — the valley sea of fog excluded. The cloud range
    /// uses this: the cloud is 2.6 km up and a 120 m deep sea is none of its business.
    float settledVisibility;
    Color color;
    const float EditorApplyInterval = 0.1f;

    Vector3 fogDrift;
    float activeCloudBottom;
    float airThinning = 1f;
    Color shadowColor;

    /// The sky tones the clouds read. They carry COLOUR only; the brightness comes from the base
    /// colour, because there is a factor of π between radiance and irradiance and using it directly
    /// darkens the clouds.
    Color skyBright = Color.white, skyShade = Color.gray;

    Color zenith, targetZenith;
    float nextEditorApply;
    float appliedShadowDistance = -1f;
    float coverage;
    bool initialized;

    /// For testing, switches the height fog off; the terrain shows in the clear. The clouds' own
    /// aerial perspective is a separate mechanism and is not affected — tying the two to one switch
    /// produces false evidence like "I turned the fog off and the problem is still there".
    public bool FogEnabled { get; set; } = true;

    /// For testing, fixes the cloud coverage independently of the weather.
    public bool CoverageLocked { get; set; }
    public float LockedCoverage { get; set; } = 0.5f;


    /// The diagnostic panel plugs in the map it generates live from here; the global texture is
    /// republished on the next Apply. Persistence is the asset bake's job.





    public float Visibility => effectiveVisibility > 0f ? effectiveVisibility : visibility;

    /// Open so the debug panel can change the settings live.
    public AtmosphereSettings Settings => settings;

    /// GLOBAL CLOUD COVERAGE, 0-1. The sky colour, the fog, the star density and the reflection
    /// level use it; the volumetric cloud system reads it too, through `CloudWeatherDriver`.
    ///
    /// A SINGLE MAPPING. The rule lives here (storm mass, dry-air rhythm, clear window, test lock)
    /// and the cloud consumes it. With two mappings in two places the sky could say "overcast"
    /// while the clouds said "clear".
    public float Coverage => coverage;

    // `CloudBottom` and `CloudTop` WERE REMOVED: they belonged to the deleted cloud system and had
    // nothing to do with what is drawn in the sky. Their consumers now read from `CloudLayerProbe`.

    public void Bind(AtmosphereSettings source, WeatherState weatherState, WindField windField,
        TimeOfDay timeOfDay, AltitudeWeatherDriver driver, Camera camera, Material sky)
    {
        settings = source;
        weather = weatherState;
        wind = windField;
        time = timeOfDay;
        weatherDriver = driver;
        view = camera;
        skyMaterial = sky;

        Initialize();
    }

    /// Because of ExecuteAlways, OnEnable runs the moment AddComponent is called — at which point
    /// Bind may not have been called yet.
    void OnEnable() => Initialize();

    void Initialize()
    {
        if (settings == null || weather == null || wind == null || time == null
            || weatherDriver == null) return;

        initialized = false;
        Apply();
    }

    void Update()
    {
        // A full computation every frame is pointless in edit mode: the scene is static and it is
        // enough for the sky to look current. Left unrestricted, ExecuteAlways loads the editor for nothing.
        if (!Application.isPlaying)
        {
            if (Time.realtimeSinceStartup < nextEditorApply) return;
            nextEditorApply = Time.realtimeSinceStartup + EditorApplyInterval;
        }

        Apply();
    }

    void Apply()
    {
        if (settings == null || weather == null || wind == null || time == null
            || weatherDriver == null) return;

        float precipitation = weather.Precipitation;
        float day = time.DayFactor;



        // A sea of cloud forms in calm weather: cold air settles into the valley and finds a still
        // ceiling above it. The wind stirs that air and breaks the inversion, and precipitation
        // carries the moisture upward — both raise the base.
        // Where the base sits is the layer's own state too: tied to the faded precipitation, the
        // sea started to sink the moment the player climbed above it.
        float calm = (1f - weatherDriver.CloudMass) * (1f - wind.Strength);
        float targetBottom = Mathf.Lerp(settings.cloudBottom, settings.calmCloudBottom, calm);

        // The mass is heavy and does not rise and fall with a gust. Because the wind severity plays
        // with eight-second gusts, tied directly the layer would jump; the base moves with its own
        // weight, on the scale of minutes.
        if (!initialized) activeCloudBottom = targetBottom;
        else
            activeCloudBottom = Mathf.Lerp(activeCloudBottom, targetBottom,
                1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(1f, settings.cloudBottomSmoothing)));

        // RAIN'S VISIBILITY IS NOT CONSTANT, IT DERIVES FROM THE PRECIPITATION RATE.
        //
        // `V(m) = 18000 · R^(−0.70)`, R the precipitation rate (mm/h).
        //
        // THE COEFFICIENT WAS FITTED TO MEASURED DATA. At one point it read `1900 · R^(−0.63)` and
        // closed the view seven times too much — most likely a unit confusion, the "1.9 km"
        // coefficient taken as 1900 m. The optical attenuation values measured in Milan
        // (Hameed et al., an FSO study) refute it:
        //
        //   R =  25 mm/h → 7.3 dB/km  → visibility 2330 m   (the old formula said 387 m)
        //   R =  50 mm/h → 14.6 dB/km → visibility 1160 m   (the old formula 162 m)
        //   R = 100 mm/h → 23.8 dB/km → visibility  714 m
        //
        // The conversion is `σ = A/4.343`, `V = 3.912/σ` (Koschmieder). The fit is off by 0% at
        // R = 50 and 100, and by 19% at R = 25.
        //
        // THIS IS ONLY THE RAIN'S OWN ATTENUATION. The hazy FEEL of rainy weather comes from low
        // cloud and moisture; those are separately present in the cloud/ceiling chain. Summing the
        // two here would put the rain in the fog's place.
        // The rate comes from the SAME mapping as `PrecipitationRenderer`'s drop distribution
        // (intensity 1.0 = 50 mm/h), otherwise the drops would describe one density and the air another.
        //
        // It used to be a constant `rainVisibility = 900 m` blended LINEARLY with the intensity.
        // Measured: in full rain the screen had 2063 m of visibility while the physics says 167 m —
        // the rain barely hazed the air at all and the rain had no DEPTH (the near streaks right,
        // the distance empty). The user reported it.
        //
        // The exponential relation also fixes the shape itself: light rain closes the view a
        // little, a downpour closes it hard. A linear blend got both wrong.
        float rainRate = 50f * precipitation;                      // mm/sa
        float rainVisibility = rainRate > 0.01f
            ? 18000f * Mathf.Pow(rainRate, -0.70f)
            : settings.clearVisibility;

        // The constant stays as it is.
        float wet = rainVisibility;
        float targetVisibility = Mathf.Min(settings.clearVisibility,
            Mathf.Lerp(settings.clearVisibility, wet, Mathf.Min(1f, precipitation * 4f)));

        // As the wind drives it the visibility closes — that is a blizzard's real effect. It only
        // means anything while there is precipitation: in clear weather the wind does not close the view.
        //
        // WEIGHTED BY THE SNOW FRACTION. Drifting snow really does kill the visibility: the grains
        // lift off the ground and hang in the air, and the attenuation goes far above the
        // precipitation's own contribution. Rain has no such mechanism — the wind tilts and speeds
        // up a drop but does not increase the amount of water hanging in the air; the attenuation
        // comes from the precipitation rate.
        //
        // Measured: unweighted, at wind 0.95 and precipitation 1.0 the closing was 0.62, i.e. the
        // visibility fell from 1164 m to 445 m — a cut two and a half times the rain's own
        // attenuation, and sourced from the wind alone.
        float closure = wind.Strength * settings.windClosure * precipitation
                      * 0.2f;
        targetVisibility *= 1f - closure;

        // Fog banks are carried by the wind; because of surface friction they do not travel as fast
        // as the clouds. No wrapping — the reason is the same as for the cloud drift.
        fogDrift += wind.Velocity * (0.6f * Time.deltaTime);

        // The bank value at the camera's position. The band patches and the visibility breathing
        // come from the CPU and the spatial pattern from the GPU, reading the same field — two
        // consumers, one field.
        Vector2 camXZ = view != null
            ? new Vector2(view.transform.position.x, view.transform.position.z)
            : Vector2.zero;
        float bank = BankField(camXZ - new Vector2(fogDrift.x, fogDrift.z));

        // Visibility breathing: an oscillation on the scale of minutes. Within the same storm the fog
        targetVisibility *= 1f + (Mathf.PerlinNoise(Time.time * 0.008f, 53f) * 2f - 1f)
                                 * settings.visibilityBreathing;

        // The cloud band: passing through the clouds that settle on the mountain's slope, the
        // visibility closes. Climbing, you really do enter the cloud and come out into clear air
        // above it. The inside of the band is not a uniform soup: when a bank parts the visibility
        // opens and the slope appears and disappears. Because spatial structure cannot be made out
        // from inside anyway, the patches come from the CPU, in time — zero GPU cost.
        float deckClose = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.5f, bank));
        float deckVis = Mathf.Lerp(settings.deckOpenVisibility, settings.deckVisibility, deckClose);

        float deck = DeckAmount(precipitation);
        targetVisibility = Mathf.Lerp(targetVisibility, deckVis, deck);

        // At night the fog darkens; the colour is chosen together with the weather type
        Color dayColor = Color.Lerp(settings.clearDay, settings.rainDay, precipitation);
        Color nightColor = Color.Lerp(settings.clearNight, settings.rainNight, precipitation);
        Color targetColor = Color.Lerp(nightColor, dayColor, day);

        // Dawn and sunset: with the sun on the horizon the atmosphere reddens.
        // In overcast weather that warmth fades — it is filtered through the cloud.
        // The dawn tone is no longer picked by hand: TimeOfDay computes how the light is filtered
        // through the atmosphere, and both the fog and the cloud feed from the same colour. Keeping
        // separate constants left the fog pale while the sky reddened.
        Color dusk = Color.Lerp(time.CurrentSunColor, settings.duskOvercast, precipitation * 0.7f);
        // The day factor was removed from here: because DayFactor is still 0.25 at dawn it halved
        // the redness and the base colour stayed at night blue. HorizonFactor is already zeroed when
        // the sun is not near the horizon, so the redness closes by itself at night — a second
        // restriction is not needed.
        float duskMask = time.HorizonFactor * settings.duskStrength;

        // The side opposite the sun: the Earth's shadow rises from the horizon and it stays cold
        // there. This is the state without the redness applied; the sky shader crossfades between
        // the two with a directional factor.
        shadowColor = Color.Lerp(targetColor, nightColor, duskMask * 0.65f);

        // Dawn paints only the horizon; the top of the sky stays at night's blue. Reddening both
        // together kills the contrast: with everything in the same tone it reads not as an orange
        // colour but as a pale ground. What makes the orange striking is standing next to that blue.
        targetZenith = Color.Lerp(targetColor * 0.55f, targetColor, precipitation);

        // The base air colour is given half of the red: given all of it, the whole scene's air was
        // dipped in a burgundy sauce. The rest of the drama is the directional palette's job
        // (AirColor): the sun's side burns gold-red, the base stays modest.
        // The coefficient comes from a Python simulation (dusk_palette_sim.py, the "vivid" variant).
        targetColor = Color.Lerp(targetColor, dusk, duskMask * 0.55f);

        // THE LEVEL FROM THE SKY, THE TONE FROM A CONSTANT. The constants above (`clearDay`,
        // `clearNight`, the precipitation/snow variants, the dawn palette) now carry TONE only; the
        // brightness is set by the sky's own measure.
        //
        // The level used to come from a constant too, and it was measured: while the sky changed by
        // ~230× between day and night, the fog colour changed by 9.6×. The result was a constant
        // that was right in one weather condition and off everywhere else:
        //   day    probe DC 0.469 → fog 0.672 → ratio 1.43  (2.2× TOO DARK)
        //   night  probe DC 0.0020 → fog 0.0698 → ratio 34.6 (11.0× TOO BRIGHT)
        // At night everything the fog covered was shifted 3.5 stops up; "the night I see with the
        // fog off is realistic" was exactly this.
        //
        // The ratio 3.15 is this project's OWN measurement (the froxel fog's ambient source
        // investigation): the probe is in surface illuminance units, a participating medium wants
        // radiance, and the conversion is π.
        // ONE COEFFICIENT, THREE COLOURS. Fitting each colour to its own target would crush the
        // RATIOS between them: the zenith's precipitation-dependent share (0.55 in clear air, 1.0 in
        // precipitation) and the shadow side's dawn share live inside those ratios.
        float scale = LevelScale(targetColor, AmbientLevel() * AmbientToMedium);

        targetColor *= scale;
        targetZenith *= scale;
        shadowColor *= scale;

        if (!initialized)
        {
            visibility = targetVisibility;
            color = targetColor;
            zenith = targetZenith;
            initialized = true;
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, settings.transitionSeconds));
            visibility = Mathf.Lerp(visibility, targetVisibility, t);
            color = Color.Lerp(color, targetColor, t);
            zenith = Color.Lerp(zenith, targetZenith, t);
        }

        // Unity's fog is never used: because it is independent of height it comes out at the same
        // density at the summit and at the foot. The height fog lives in HeightFog.hlsl, as the
        // surface's own computation.
        ApplyHeightFog();

        // THE AMBIENT LIGHT, THE SKY AND THE REFLECTION ARE NOW IN THE PACKAGE.
        // `PhysicallyBasedSkyURP` bakes the skybox, the ambient probe and the reflection cube from
        // its own LUTs; a second writer here would leave the result to the write order within the frame.
        //
        // The `color` derived here stays: the height fog, the cloud tone and the audio/colour
        // grading read it.

        if (view != null) view.clearFlags = CameraClearFlags.Skybox;

        ApplyShadowDistance();
        ApplySky(precipitation);
    }

    /// Where the camera is within the cloud band: 0 outside, 1 fully inside.
    /// It passes softly at the band's edges, you do not enter it suddenly.
    float DeckAmount(float precipitation)
    {
        if (view == null) return 0f;

        // The band starts just below the cloud base and enters the layer:
        // the cloud you pass through and the cloud you see above have to be the same thing
        float altitude = view.transform.position.y;
        float center = activeCloudBottom - settings.deckLeadMeters + settings.deckThickness * 0.5f;
        float distance = Mathf.Abs(altitude - center) / Mathf.Max(1f, settings.deckThickness * 0.5f);

        float inside = 1f - Mathf.SmoothStep(0.55f, 1f, distance);
        return inside * Mathf.Lerp(settings.deckClearAmount, 1f, precipitation);
    }

    /// Nobody sees the shadow behind the fog. As the visibility closes the shadow distance shortens
    /// too: when the weather hardens, i.e. when the GPU is working hardest, the cost falls.
    void ApplyShadowDistance()
    {
        if (UniversalRenderPipeline.asset == null) return;

        float distance = Mathf.Min(settings.maxShadowDistance,
                                   visibility * settings.shadowVisibilityRatio);

        // It is only written when something WORTH NOTING changes. Writing to the pipeline asset
        // dirties it and triggers a settings refresh. The threshold has to be wide: the visibility
        // breathes constantly (bank noise + `visibilityBreathing`), and with a narrow threshold it
        // was crossed every frame and the writes never thinned out. Twenty-five metres of shadow
        // distance is indistinguishable by eye — the shadows are already fading at that distance.
        if (Mathf.Abs(distance - appliedShadowDistance) < 25f) return;

        appliedShadowDistance = distance;
        UniversalRenderPipeline.asset.shadowDistance = distance;
    }

    /// Because the cloud pass runs in a separate shader the parameters are written as globals;
    /// the sky and the clouds read the same values and cannot contradict.
    void ApplySky(float precipitation)
    {
        if (skyMaterial == null) return;

        // The coverage rises faster than the severity: the sky closes before the precipitation
        // fully hardens, otherwise a downpour looks like it starts under a clear sky. Not a shift in
        // time — the curve is steepened and the coverage reaches its ceiling before the severity does.
        // The layer's own state comes from the storm's raw severity. Precipitation fades once the
        // player climbs above the cloud ceiling — but the sea below is the sea of the same storm;
        // tied to the faded value, the clouds thinned the moment the summit was reached.
        float storm = weatherDriver.CloudMass;

        coverage = CoverageLocked
            ? LockedCoverage
            : Mathf.Lerp(settings.clearCoverage, settings.stormCoverage, Mathf.Clamp01(storm * (1f + settings.coverageGain)));

        // The lower bound normally binds: below it the clouds stay thin and scattered. The one
        // exception is the clear window — the moment the driver rarely opens, the only way the floor
        // can be pierced. Otherwise the two rules contradicted: the driver promised "the clouds part
        // and the summit shows" while the floor made sure that moment never came.
        // The coverage's floor comes FROM THE DRIVER: even with zero precipitation the sky closes
        // and opens to its own rhythm (see `AltitudeWeatherDriver.DryCoverage`). A fixed floor meant
        // the same sky every moment it was not raining.
        float floor = Mathf.Max(settings.minCoverage, weatherDriver.DryCoverage);

        // The one exception is the clear window — the moment the driver rarely opens, the only way
        // the floor can be pierced.
        //
        // THE FLOOR IS NOT APPLIED WHILE LOCKED. `CoverageLocked` is a DIAGNOSTIC switch: it means
        // "pin the coverage to this value". With the floor overriding it the lock was lying —
        // 0 was written and 0.40 read back, and the measurement was silently invalidated
        // (`SYMPTOMS.md`). While not locked the floor works exactly as before; the game's
        // behaviour does not change.
        if (!CoverageLocked)
            coverage = Mathf.Max(coverage,
                Mathf.Lerp(floor, settings.openCoverage, weatherDriver.ClearWindow));

        // THE CLOUD DRIFT LEFT HERE. The offset is now accumulated by the volumetric cloud system,
        // which reads the wind directly through `CloudWeatherDriver`; the smoothed direction/speed
        // pair here was being read by nobody.
        // Convective lift lasts through the day: its source is the warming ground. At night the
        // ground cools, the lift stops — the clouds only drift.


        // The sky gradient no longer goes to the material: the sky reads the same AirColor function
        // as the fog, and that function feeds from the _HeightFog* globals.

        // The disc's colour is the same as the light's; as the cloud thickens the sun is veiled
        float veil = 1f - coverage * 0.75f;
        skyMaterial.SetColor(SunColorId, time.CurrentSunColor * veil);
        skyMaterial.SetColor(MoonColorId, time.MoonTint * veil);

        // (THE CLOUD TEXTURE PUBLICATIONS WERE DELETED — the noise and the weather map are being rewritten.)
        //
        // `_CloudBottom` IS STILL written below: the lightning shader intersects the strike with
        // the cloud base sphere. The cloud STATE (coverage, base, top, drift) stays in this
        // component — it is the weather model's output, not render plumbing. The new cloud system
        // will read them; the list of links is in `CLOUDS_REBUILD.md`.

        // `_SunDirection` is the height fog's light direction. `_MoonDirection` WAS DELETED: only
        // `Sky.shader` read it, and that is no longer the skybox.
        Shader.SetGlobalVector(SunDirectionId, time.SunDirection);

        // The shear is a fixed distance: the lateral offset as a share of the layer thickness is
        // DIMENSIONLESS — the shader multiplies it by the layer thickness. It was being multiplied
        // here as well, and with a 5.3 km layer the offset came to 2927 m — wider than a typical
        // cloud, so the column's top came out beside its base and, with the rotation, turned into a
        // hook. With the layer at 2.5 km the same ratio gives 1500 m: the cloud's own scale.

        // The direction rotation falls with the wind severity: in a hard wind the air mass is
        // carried the same way through the whole layer, and in calm air the deviation stands out.

        // The cloud colours derive from the atmosphere's colour: at dawn the redness passes to the cloud too
        // The multiplier is kept near one: because the colour already arrives saturated, multiplying
        // by 1.5 overflowed the red channel and turned the cloud white. The brightness comes from
        // the ambient intensity, not from the colour.
        // THE CLOUD'S COLOUR FROM THE SKY, ITS BRIGHTNESS FROM THE OLD BASE. Giving the sky radiance
        // directly was tried and the clouds went pitch black: what lights a cloud is not the zenith
        // RADIANCE but the total IRRADIANCE arriving from the sky, and there is a factor of π
        // between them. The right information the radiance carries is COLOUR; the brightness is
        // already the calibrated base colour's job. Separated, the cloud turns orange at dawn
        // without going dark.
        // THE CLOUD'S LIGHT COMES FROM ITS OWN ELEVATION. The beam at ground level was being used:
        // at dawn, with the sun below the horizon, there is no light on the ground, so the cloud
        // was left unlit too and turned into a dark silhouette — even the thinnest fog swallowed it.
        //
        // In reality the cloud is 1.7 km up and sees the sun BEFORE THE GROUND does: while the
        // Earth's shadow is in the valley the cloud is above that shadow. That is exactly why cloud
        // bases burn orange at dawn — the same geometry as the alpenglow.
        //
        // Colour and intensity are combined: the transmittance carries both the reddening and the fade.
        Vector3 cloudBeam = Atmosphere.BeamTransmittance(activeCloudBottom, time.SunDirection);

        // THE CLOUD'S WARMING OPENS LATE. Direct light is deep red with a low sun; the cloud's
        // ambient light, coming from the zenith, is bluish. Superposed, they read PINK on screen and
        // the clouds went pink in the first quarter of dawn. With the limiter squared the warming
        // starts after three degrees: the pink window closes and the transition stays continuous.
        float cloudWarm = Atmosphere.LowSunFade(activeCloudBottom, time.SunDirection);
        cloudWarm *= cloudWarm;
        cloudBeam *= cloudWarm;


        // The cloud perspective changes together with the visibility. A fixed distance showed the
        // clouds still crisp while the mountain disappeared at three hundred metres: the two looked
        // like they were not sharing the same air.
        // Altitude thins the air but does not make it infinitely clear: on a horizontal view the ray
        // still crosses kilometres of air, and Rayleigh scattering limits the visibility even in the
        // clearest air. Left without a ceiling the blend rose to hundreds of kilometres at the
        // summit and the sea of cloud's horizon stood as a bare line.
        //
        // THE MEASURE DOES NOT SEE THE SEA. `Visibility` describes the TOTAL air at the camera's
        // elevation, and at dawn that is set by a 120 m deep sea of fog: it comes out at 1871 m, the
        // range is clamped to 16 km, and because of `marchable = start <= hazeDistance` every ray
        // entering the layer further than 16 km away — i.e. every direction more than 9.3° below the
        // horizon — was not drawn at all. From the ground the clouds filling the sky are in the
        // 5-25° band; half of them faded and the lower part was erased completely. The cloud is
        // 2.6 km up, far above the sea.
        float hazeDistance = settledVisibility * settings.hazeVisibilityFactor
                             / Mathf.Max(0.01f, airThinning);

        // The floor stops the visibility from dragging the cloud range. The visibility describes the
        // air at ground level while the cloud stands above it; linking the two linearly opened a
        // hole in the sky during a storm.
        hazeDistance = Mathf.Clamp(hazeDistance,
            settings.minHazeDistance, settings.maxHazeDistance);


        // The high layer fades as the volumetric cover closes: it is invisible from below anyway and
        // drawing it is pointless. Its type is chosen by the setting itself.
        // The warm tone riding on the cloud comes from the beam's transmittance colour and only
        // opens while the sun is near the horizon.
        //
        // Taking the colour FROM THE SKY was tried (R7) and deleted. The construction was right —
        // the beam is zero below the horizon, so the tone falls to black and pre-dawn clouds take no
        // colour at all. But the measurement showed the problem was not there: with the sun at −4°
        // the real ambient is ~13 lux (moonlight level) and on a real mountain the cloud bases do
        // not burn at that hour either. On top of that our twilight sky is already TOO bright
        // against reality (5.6× at −6°). So carrying colour below the horizon would have been
        // producing a phenomenon that does not exist. Dawn's real show is between −1° and +3°,
        // where the beam already exists.

        // `_CloudBottom` and `_CloudTop` ARE NOT PUBLISHED FROM HERE. Only the cloud system knows
        // the layer's real elevations; `CloudLayerProbe` publishes them (link 8). The
        // `activeCloudBottom` here is the old model's own value and stands only for the fog and sky.
        Shader.SetGlobalFloat(PlanetRadiusId, settings.planetRadius);


        // In a storm the cloud does not only cover the sky, it thickens too. From the same source as
        // the coverage: the thickness is the layer's own state as well.
    }

    /// Reads the column above the player from the weather map: the precipitation share rises with
    /// the coverage and with the cloud's puffiness (its type) — a flat thin layer does not rain, a
    /// puffy thick mass does. Because the map flows with the wind the reading point moves too; as
    /// the cloud passes the rain starts and stops.
    /// The sky's PHYSICAL colour in the given direction (Atmosphere.SkyRadiance). The raw radiance
    /// is of order 10⁻²; the gain only carries it into scene units, it does not change the colour.
    /// Visibility from the density (Koschmieder), WITH A PHYSICAL CEILING. Because the fog layer
    /// thins exponentially with height the division blows up high up: with a shallow layer the
    /// summit came out at "3900 km of visibility". Air is not a vacuum — even in the cleanest air
    /// Rayleigh scattering closes the view within a few hundred kilometres, and the ceiling is from there.
    const float AtmosphericVisibilityLimit = 300000f;

    float Visible(float density)
        => Mathf.Min(AtmosphericVisibilityLimit,
                     settings.fogThickness / Mathf.Max(1e-6f, density));

    /// The sky's PHYSICAL colour in the given direction. The gain is `Atmosphere.SceneGain` —
    /// SHARED with the exposure level. Kept separate, changing one left the other in place and the
    /// sky and the value derived from it diverged.
    static Color SampleSky(Vector3 view, Vector3 sun)
    {
        Vector3 r = Atmosphere.SkyRadiance(0f, view, sun) * Atmosphere.SceneGain;
        return new Color(r.x, r.y, r.z, 1f);
    }

    /// Preserves the source's BRIGHTNESS and carries its COLOUR to the target. The physical sky
    /// sample knows the right tone but its unit differs from what the cloud expects; combining them
    /// this way both brings the dawn colour and leaves the calibration intact.
    static Color Recolour(Color source, Color hue)
    {
        float sourceLuma = source.r * 0.2126f + source.g * 0.7152f + source.b * 0.0722f;
        float hueLuma = hue.r * 0.2126f + hue.g * 0.7152f + hue.b * 0.0722f;
        if (hueLuma <= 1e-5f) return source;

        float scale = sourceLuma / hueLuma;
        return new Color(hue.r * scale, hue.g * scale, hue.b * scale, 1f);
    }

    /// The height fog's parameters. The visibility already accounts for the weather, the wind and
    /// the cloud band; here it is only distributed over height.
    void ApplyHeightFog()
    {
        // The exponential coefficient from the half height: exp(-k · h) = 0.5 → k = ln2 / h
        // THE LAYER DEPTH COMES FROM THE WEATHER. The haze in clear air is a shallow boundary layer;
        // in precipitation the column mixes vertically and the rain fills it from top to bottom.
        // Holding it constant always broke one of the two: the shallow value gave 5 km of visibility
        // in a downpour at 1000 m, the deep one erased the sea of cloud in clear air. Like the
        // visibility and the inversion ceiling, this is driven from a single source (the
        // precipitation intensity), so the three cannot contradict.
        float halfHeight = Mathf.Lerp(settings.fogHalfHeightClear,
                                      settings.fogHalfHeightStorm, weather.Precipitation);
        float falloff = 0.6931f / Mathf.Max(1f, halfHeight);

        // The base density comes from the visibility. Because the fog thins as it rises, the
        // visibility only corresponds to this value at the base elevation and opens above it.
        float density = settings.fogThickness / Mathf.Max(1f, visibility);

        // Valley fog is a night's work: the ground loses heat through the night, the moisture in the
        // air condenses and the fog reaches its thickest at dawn. As the sun rises it warms the
        // ground and the fog melts from the top down. Tying this to the weather alone lost the
        // morning's own weight — the valley has to be full when you set out to climb.
        float burnOff = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time.SunHeight / settings.valleyFogBurnOff));

        // Valley fog is a PRODUCT OF THE NIGHT (radiative cooling): it gathers at night, disperses
        // with the morning sun and DOES NOT COME BACK in the evening. Tied to the sun elevation
        // alone the formula stays symmetric and refilled the valley at sunset — a player looking
        // from the ground could not see the clouds at sunset: the veil was right, the fog being
        // there was wrong.
        float clock = time.Normalized;
        float morningSide = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.6f, clock));
        float lateNight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.8f, 1f, clock));
        float seaSeason = Mathf.Max(morningSide, lateNight);

        // THE DAWN VALLEY FOG'S ONLY SOURCE IS THE SEA LAYER. There used to be a separate
        // multiplier here thickening the settled air (`valleyFogAtDawn`): the same phenomenon was
        // modelled by two mechanisms, and the deep one (half height 1400 m) reached up to the clouds
        // 2.6 km above and erased them. The sea ends at 120 m and stays in the valley.

        // The dawn sea of fog is a SEPARATE LAYER. Even on a clear night the valley floor fills
        // independently of the weather; the density derived from the visibility is so small in clear
        // air that the multiplier could not be felt. It melts with the sun.
        //
        // It used to be folded into the settled air's density with `max()` and passed through a
        // single channel: the CPU computed the sea with its own 120 m profile while the shader
        // spread it with the settled air's 1400 m profile. The shallow sea climbed to the cloud
        // base, the optical depth along the path came out TEN TIMES too large and at dawn it erased
        // the clouds entirely for a player looking up. Now there are two layers and two channels;
        // each applies its own height profile with its own coefficient in the shader.
        float seaFalloff = 0.6931f / Mathf.Max(1f, settings.dawnSeaHalfHeight);
        float seaDensity = settings.fogThickness / Mathf.Max(1f, settings.dawnSeaVisibility)
                           * (1f - burnOff) * seaSeason;

        // THE REAL VISIBILITY, FROM THE DENSITY ITSELF. The density is forced above (the valley
        // multiplier, the sea layer) but `visibility` did not see those forcings: the HUD said
        // 13.6 km while the real density corresponded to 600 m of fog — 22×. Because the cloud veil
        // uses the real one the clouds were erased, and the player said "there is 13 km of
        // visibility, where is the cloud".
        //
        // A SEPARATE FIELD: `visibility` is the air's target, an input. Writing it back from the
        // density sets up feedback (density from visibility, visibility from density) and the value
        // folded every frame and collapsed to metres.
        //
        // THE FREE TROPOSPHERE — A THIRD LAYER. The air's own molecules (Rayleigh): broad, and
        // INDEPENDENT OF THE PRECIPITATION because weather lives in the boundary layer.
        // It was modelled as an "above-inversion residual share" and multiplied by the boundary
        // layer's own shallow profile; it zeroed within a few thousand metres and, looking down from
        // the summit, a ridge thirty kilometres away stood at full contrast, like cardboard.
        float freeFalloff = 0.6931f / Mathf.Max(1f, settings.freeAirHalfHeight);
        float freeDensity = settings.fogThickness / Mathf.Max(1f, settings.freeAirVisibility);

        // In a storm the moisture is carried upward and the ceiling rises: the fog that settles into
        // the valley in calm weather wraps half the mountain in precipitation. Unlinked from the
        // weather the inversion stands like a fixed line and the summit stays needlessly clear in a storm.
        //
        // The ceiling and the cap are computed HERE: the visibility readout, the cloud range and the
        // air's thinness all have to feed from the same expression. Derived separately, one saw the
        // inversion and the other did not.
        float ceiling = settings.inversionHeight + weather.Precipitation * settings.inversionStormRise;

        // If the cloud base has come down to the inversion ceiling the two are the same layer: where
        // the fog ends the cloud has to begin, with no gap between them.
        ceiling = Mathf.Min(ceiling, activeCloudBottom);

        // The measure is AT THE CAMERA'S ELEVATION, the sum of the three layers: using the base
        // value made a player at the summit read the valley's visibility.
        float cameraHeight = view != null
            ? view.transform.position.y - settings.fogBaseAltitude : 0f;
        float eyeHeight = Mathf.Max(0f, cameraHeight);

        float lid = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(ceiling - settings.inversionWidth,
                              ceiling + settings.inversionWidth, cameraHeight));

        float eyeBoundary = density * Mathf.Exp(-falloff * eyeHeight) * lid;
        float eyeFree = freeDensity * Mathf.Exp(-freeFalloff * eyeHeight);
        float eyeSea = seaDensity * Mathf.Exp(-seaFalloff * eyeHeight);

        effectiveVisibility = Visible(eyeBoundary + eyeSea + eyeFree);

        // THE VEIL READS THE VISIBILITY FROM HERE. Rather than integrating the fog's opacity along
        // the ray it derives it with a single exponential; this is its owner, there is no second source.

        // The measure for the cloud range: the valley sea EXCLUDED. The sea ends at 120 m and the
        // cloud is 2.6 km up; the fog at the valley floor cannot set its range.
        settledVisibility = Visible(eyeBoundary + eyeFree);

        // The thinness of the air at the camera's elevation, relative to the total at the base
        // elevation. The clouds' blend distance uses this too — carried up as-is, the ground level's
        // visibility made the clouds vanish within a few hundred metres in weather where the
        // mountain was crisp for kilometres; the two have to share the same atmosphere.
        // The free layer is in both the numerator and the denominator: that is why it does not go to
        // zero at the summit.
        //
        // THE ORDER MATTERS: BEFORE the `FogEnabled` shutdown. Computed after it, the denominator is
        // zeroed while the numerator is not and the ratio blows up.
        airThinning = (eyeBoundary + eyeFree)
                      / Mathf.Max(1e-9f, density + freeDensity);

        // With the test switch off all three layers are zeroed: because the remaining parameters are
        // multiplied by the density in the shader they have no effect, so there is nothing else to switch off.
        if (!FogEnabled) { density = 0f; seaDensity = 0f; freeDensity = 0f; }

        // The banks' strength comes from the weather: storm fog wraps more patchily, and the dawn
        // sea wanders bank by bank too. A fixed strength thickened the fog everywhere at once.
        float bankStrength = Mathf.Lerp(settings.fogBankClear, settings.fogBankStorm,
            weather.Precipitation);
        bankStrength = Mathf.Max(bankStrength, 0.7f * (1f - burnOff));


        Shader.SetGlobalColor(HeightFogColorId, color);
        // The fog's shadow side is the same colour as the sky's: if the two diverge, at twilight the
        // mountain stays brighter than the dark sky behind it and sticks out like flat cardboard.
        // STEP 1: the horizon colour on the sun's side now comes from physics. The beam colour
        // (CurrentSunColor) used to be given here — but the beam and the SKY's colour in that
        // direction are different things: the beam reddens, and the sky scatters that redness into
        // an orange-gold band. Treating them as the same reduced dawn to a single colour.
        Vector3 sunFlat = new Vector3(time.SunDirection.x, 0f, time.SunDirection.z);
        sunFlat = sunFlat.sqrMagnitude > 1e-6f ? sunFlat.normalized : Vector3.forward;
        // The sample is taken 2° above the horizon: the band is strongest there. At 6° we were
        // reading air that had already thinned and the orange came out dull.
        //
        // 1° was tried and reverted: the physics catches `gold`'s brightness (0.84 / 0.90) but on
        // screen the clouds whitened and the sky darkened. Getting one degree closer to the horizon
        // hardens not only the brightness but the contrast.
        Vector3 sunwardHorizon = (sunFlat * Mathf.Cos(2f * Mathf.Deg2Rad)
                                  + Vector3.up * Mathf.Sin(2f * Mathf.Deg2Rad)).normalized;

        Vector3 awayHorizon = (-sunFlat * Mathf.Cos(2f * Mathf.Deg2Rad)
                               + Vector3.up * Mathf.Sin(2f * Mathf.Deg2Rad)).normalized;


        Color physicalSunward = SampleSky(sunwardHorizon, time.SunDirection);
        Color physicalZenith = SampleSky(Vector3.up, time.SunDirection);
        Color physicalAway = SampleSky(awayHorizon, time.SunDirection);

        // The cloud's BRIGHT face feeds from this, and the UNLIMITED state is used: the limiter
        // lowers the brightness and `Recolour` cannot take a tone from a source near zero, so it fell
        // back to the palette.
        Color sunwardHue = physicalSunward;

        // The SAME multiplier as the beam and the sun colour: if the three diverge the clouds go
        // pink under a low sun, or the sky turns red all at once.
        float lowSun = Atmosphere.LowSunFade(0f, time.SunDirection);
        physicalSunward *= lowSun;
        physicalAway *= lowSun;


        // In overcast weather the sky loses its own scattering and approaches the cloud's grey —
        // but not completely: even at a rainy dawn a red slit stays at the horizon.
        float overcast = weather.Precipitation * 0.55f;

        Shader.SetGlobalColor(HeightFogSunColorId,
            Color.Lerp(physicalSunward, color, overcast));

        // STEP 2: the zenith comes from physics too. What makes dawn striking is not the orange
        // itself but its standing next to the BLUE above it. With the zenith left on the old palette
        // the sky turned into a single-toned grey-brown ground.
        Shader.SetGlobalColor(HeightFogZenithId,
            Color.Lerp(physicalZenith, zenith, overcast));

        // STEP 3: the opposite horizon comes from physics too. The sky triple is complete; the
        // shader blends the directions in between from these three samples, so the spread is no
        // longer by hand but from the model.
        Shader.SetGlobalColor(HeightFogShadowColorId,
            Color.Lerp(physicalAway, shadowColor, overcast));


        // THE CLOUD'S AMBIENT COLOUR MOVES TO THE HORIZON AT DAWN. The bright face normally feeds
        // from the zenith, but the zenith stays BLUE under a low sun while the direct light is deep
        // red. Superposed, the cloud reads PINK and the first quarter of dawn went pink.
        //
        // In reality what lights a cloud is not the sky overhead but the bright horizon. With the
        // tone moved to the horizon the ambient and the direct light come from the same family: the
        // pink closes and dawn is warm throughout. `HorizonFactor` opens the window — full while the
        // sun is near the horizon, returning to the zenith as it rises.
        skyBright = Color.Lerp(physicalZenith, sunwardHue, time.HorizonFactor);
        skyBright = Color.Lerp(skyBright, color, overcast);

        // THE SHADOW FACE IS NOT WARMED. The bright face moves to the horizon (above) but the shadow
        // face must not: under a low sun the opposite horizon sample is itself red
        // (0.142, 0.084, 0.048) and carried onto the cloud, the cloud on the moon's side got both
        // red direct light and red ambient and reddened throughout. The shadow face has to stay
        // cool — the distinction between the two halves is born from that anyway.
        skyShade = Color.Lerp(physicalAway, color, overcast);


        Shader.SetGlobalFloat(HeightFogDensityId, density);
        Shader.SetGlobalFloat(HeightFogFalloffId, falloff);
        Shader.SetGlobalFloat(HeightFogBaseId, settings.fogBaseAltitude);
        Shader.SetGlobalFloat(FogSeaDensityId, seaDensity);
        Shader.SetGlobalFloat(FogSeaFalloffId, seaFalloff);
        Shader.SetGlobalFloat(FogInversionHeightId, ceiling);
        Shader.SetGlobalFloat(FogInversionWidthId, settings.inversionWidth);
        Shader.SetGlobalFloat(FogFreeDensityId, freeDensity);
        Shader.SetGlobalFloat(FogFreeFalloffId, freeFalloff);
        Shader.SetGlobalVector(FogBankDriftId, fogDrift);
        Shader.SetGlobalFloat(FogBankStrengthId, bankStrength);


        Vector3 flow = wind.Velocity;

        Shader.SetGlobalVector(WindVectorId,
            new Vector4(flow.x, flow.y, flow.z, wind.Gust));
    }

    /// The same field as FogBankAt in HeightFog.hlsl, without the multiplier (0..1).
    /// If the formula changes the two have to change together — two consumers, one field.
    static float BankField(Vector2 p)
    {
        // A SUM, NOT A PRODUCT — the reasoning is inside `VolumetricFogShared.hlsl → FogBankAt`.
        // The components have to be EXACTLY the same as there: two consumers, one field.
        float s = Mathf.Sin(Vector2.Dot(p, new Vector2( 0.003534f,  0.001081f))) * 0.34f
                + Mathf.Sin(Vector2.Dot(p, new Vector2( 0.001090f,  0.005607f))) * 0.26f
                + Mathf.Sin(Vector2.Dot(p, new Vector2(-0.005424f,  0.006239f))) * 0.20f
                + Mathf.Sin(Vector2.Dot(p, new Vector2(-0.011122f, -0.004720f))) * 0.13f
                + Mathf.Sin(Vector2.Dot(p, new Vector2( 0.005250f, -0.017167f))) * 0.07f;

        return Mathf.Clamp01(0.5f + 0.5f * s);
    }

    /// The DC term of the ambient probe baked from the sky. `SkyAmbientBaker` bakes it from the sky
    /// material every frame, so the chain is one-way: sky → probe → fog colour.
    /// The SAME quantity as the fog volume's ambient source (`sh[c,0] - sh[c,6]`) — if the two
    /// consumers do not see the same number, the volume and the analytic tail diverge at night.
    static float AmbientLevel()
    {
        SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;

        return Mathf.Max(0f,
            0.2126f * (probe[0, 0] - probe[0, 6])
          + 0.7152f * (probe[1, 0] - probe[1, 6])
          + 0.0722f * (probe[2, 0] - probe[2, 6]));
    }

    /// The coefficient that seats the base colour at the target brightness. The tone from the
    /// source, the level from a measurement — as the project's "before binding to a value" rule
    /// requires. It returns a coefficient, not a colour: the caller applies it to several colours
    /// and preserves the ratios between them.
    static float LevelScale(Color reference, float target)
    {
        float current = 0.2126f * reference.r + 0.7152f * reference.g + 0.0722f * reference.b;

        return current < 1e-6f ? 1f : target / current;
    }
}
