using System;
using UnityEngine;

/// Places the lightning in the world and drives its lighting: it writes its own directional light
/// and the glow values the sky and the cloud read.
///
/// It does not know when it will strike or how far away it is — `ThunderPlayer` tells it.
/// It does not read the weather, the wind or the clock; the side that triggers it already does.
/// The randomness here is only which direction the strike is in and how many return strokes it makes.
///
/// The light stays directional. The strike is inside the cloud, i.e. above two kilometres: a bolt
/// striking five hundred metres away is 2550 m from the rock at its foot and one three kilometres
/// away 3900 m — only a 2.3× difference across the terrain. Against that, a point light whose
/// range covered the whole scene would make the Forward+ clustering useless. The dominant cue,
/// "a near strike blinds and a distant one stays faint", comes from the inverse square of the
/// distance and that is free; the direction now derives from the real position too. The point
/// light at the contact point of the bolt reaching the ground is `LightningBolt`'s job — that one
/// really is nearby, so its range can be kept narrow.
[RequireComponent(typeof(Light))]
public class LightningFlash : MonoBehaviour
{
    static readonly int FlashId = Shader.PropertyToID("_LightningFlash");
    static readonly int PositionId = Shader.PropertyToID("_LightningPosition");
    static readonly int ScatterLutId = Shader.PropertyToID("_LightningScatterLut");
    static readonly int ScatterTId = Shader.PropertyToID("_LightningScatterT");
    static readonly int SourcesId = Shader.PropertyToID("_LightningSources");
    static readonly int SourceCountId = Shader.PropertyToID("_LightningSourceCount");

    /// HOW MANY POINT SOURCES ALONG THE CHANNEL. The paper uses 50, but for offline rendering
    /// `[Dobashi 2001, §5.1]`; for us that many table lookups per pixel is expensive.
    ///
    /// Eight is enough to carry the channel's shape: with a single source the glow looks like a
    /// sphere and says nothing about where the bolt reaches. The gain falls off quickly as the
    /// number rises, because the table is smooth anyway.
    const int SourceCount = 8;

    /// The most return strokes one strike can carry
    const int MaxStrokes = 3;

    [SerializeField] ThunderPlayer thunder;
    [SerializeField] AtmosphereController atmosphere;
    [SerializeField] CloudLayerProbe cloudLayer;
    [SerializeField] Transform observer;
    [SerializeField] LightningSettings settings;

    /// The atmospheric scattering table (`LightningLutBaker` bakes it). The fog and the sky read
    /// from it; because the table is static it is written once, not frame by frame.
    /// To know where the channel ends: the sources are spread from the cloud to the GROUND.
    /// Using a fixed length buried the channel inside the terrain on a steep slope.
    [SerializeField] Terrain terrain;

    [SerializeField] Texture2D scatterLut;
    [SerializeField] float scatterCutoff = 9000f;

    /// The strike's place in the world and how long it lasts. The side drawing the bolt is fed
    /// from here: choosing the position a second time would put the light in one place and the bolt in another.
    public event Action<LightningStrike> Placed;

    /// Freezes the strike at its peak value. A strike lasts 0.1 seconds; answering "did that place
    /// light up" within six frames is impossible. A test switch.
    public bool Held { get; set; }

    /// The last strike's distance (metres). −1 if there has been none.
    public float LastDistance { get; private set; } = -1f;

    /// The current light intensity and cloud glow. Read in the panel: a strike not being seen can
    /// come either from the event never arriving or from it not being drawn, and the two look the
    /// same from outside.
    public float Intensity => flash != null ? flash.intensity : 0f;
    public float Glow { get; private set; }

    Light flash;

    readonly float[] strokeTime = new float[MaxStrokes];
    readonly float[] strokeAmplitude = new float[MaxStrokes];
    int strokeCount;

    bool active;
    float elapsed;
    float duration;
    float decayTau;
    float peakIntensity;
    float peakGlow;
    Vector3 origin;
    readonly Vector4[] sources = new Vector4[SourceCount];

    /// The scene setup configures the light from here too: its shape is the component's own job
    /// and should not spread into the setup script.
    public void Bind(ThunderPlayer source, AtmosphereController air, Transform eye,
        LightningSettings tuning, CloudLayerProbe layer, Texture2D lut, float cutoff,
        Terrain ground)
    {
        terrain = ground;
        thunder = source;
        atmosphere = air;
        cloudLayer = layer;
        observer = eye;
        settings = tuning;
        scatterLut = lut;
        scatterCutoff = cutoff;

        PublishLut();

        var light = GetComponent<Light>();
        light.type = LightType.Directional;

        // Shadows off. URP picks the main directional light by whichever is brightest; at the
        // moment of a strike this light, brighter than the sun, takes over as the main light and
        // the mountain's shadows shift for a frame. Without shadows it is only summed as an extra light.
        light.shadows = LightShadows.None;
        light.color = tuning.flashColor;
        light.intensity = 0f;
    }

    void OnEnable()
    {
        if (thunder == null || atmosphere == null || observer == null || settings == null)
            throw new InvalidOperationException(
                $"{nameof(LightningFlash)}: dependencies are not assigned.");

        flash = GetComponent<Light>();
        flash.color = settings.flashColor;

        thunder.Struck += OnStruck;

        PublishLut();
        Apply(0f);
    }

    void OnDisable()
    {
        thunder.Struck -= OnStruck;
        active = false;

        Apply(0f);
    }

    void Update()
    {
        if (Held)
        {
            if (LastDistance >= 0f) Apply(1f);
            return;
        }

        if (!active) return;

        elapsed += Time.deltaTime;

        if (elapsed >= duration)
        {
            active = false;
            Apply(0f);
            return;
        }

        Apply(Envelope(elapsed));
    }

    /// distance: the strike's distance in metres
    void OnStruck(float distance)
    {
        float nearness = 1f - Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(settings.nearDistance, settings.farDistance, distance));

        // The direction is weighted towards where the player is looking. Distributing it entirely
        // at random is "correct", but because the field of view sees a fifth of the sky most
        // strikes fell behind and the storm looked empty. A lightning bolt missed is one that never struck.
        float look = Mathf.Atan2(observer.forward.z, observer.forward.x);
        float spread = Mathf.Lerp(Mathf.PI, Mathf.PI * 0.28f, settings.forwardBias);
        float bearing = look + UnityEngine.Random.Range(-spread, spread);

        Vector3 eye = observer.position;
        Vector3 strike = new Vector3(eye.x + Mathf.Cos(bearing) * distance, 0f,
                                     eye.z + Mathf.Sin(bearing) * distance);

        // Lightning discharges inside the cloud. It is placed in the layer's lower quarter: because
        // the channel grows downward, the visible discharge ends up near the base. The top is read
        // FROM THE STRIKE'S COLUMN; if the column is empty the layer's maximum top is used.
        float top = cloudLayer.TopAt(strike);
        if (float.IsPositiveInfinity(top)) top = cloudLayer.MaxTop;
        strike.y = Mathf.Lerp(cloudLayer.Bottom, top, 0.25f);
        origin = strike;

        // THE SOURCES RUN ALONG THE CHANNEL. With a single source representing the whole bolt the
        // glow looks like a sphere and says nothing about where the bolt reaches; the paper's
        // method is exactly to turn the bolts into an array of point sources (§3.2).
        //
        // The end points: the discharge point (the cloud's lower quarter) and the slope itself.
        // The energy is DIVIDED among the sources (the shader divides the sum by the count), so
        // the total brightness does not change — only where it is spread.
        float groundY = terrain != null
            ? terrain.SampleHeight(strike) + terrain.transform.position.y
            : cloudLayer.Bottom - 1000f;

        Vector3 foot = new Vector3(strike.x, groundY, strike.z);

        for (int i = 0; i < SourceCount; i++)
        {
            Vector3 p = Vector3.Lerp(origin, foot, (i + 0.5f) / SourceCount);
            sources[i] = new Vector4(p.x, p.y, p.z, 0f);
        }

        // ONCE PER STRIKE. The position does not change through the strike; writing eight vectors
        // every frame is pointless. `Apply` updates only the INTENSITY frame by frame.
        Shader.SetGlobalVectorArray(SourcesId, sources);
        Shader.SetGlobalFloat(SourceCountId, SourceCount);

        // Inverse square fade: the intensity is given at a reference distance and carried to the
        // real one. That is why lightning bursting nearby dazzles — the tone mapping saturates to
        // white there, which is what a watching eye does too.
        float reach = Vector3.Distance(eye, origin);
        float reference = Mathf.Max(1f, settings.referenceDistance);
        peakIntensity = settings.intensityAtReference * (reference * reference)
                        / Mathf.Max(1f, reach * reach);

        peakGlow = Mathf.Lerp(settings.distantGlow, settings.closeGlow, nearness);
        decayTau = Mathf.Max(0.001f,
            Mathf.Lerp(settings.distantDecay, settings.closeDecay, nearness));

        // The light comes from the strike to the eye; the direction the directional light faces is that path itself.
        transform.rotation = Quaternion.LookRotation((eye - origin).normalized);

        // A single damped glow looks plastic: real lightning discharges through the same channel
        // several times and reaches the eye as a flickering light.
        strokeCount = UnityEngine.Random.Range(1, MaxStrokes + 1);

        float when = 0f;
        for (int i = 0; i < strokeCount; i++)
        {
            strokeTime[i] = when;

            // The first discharge is the strongest, the later ones weaken
            strokeAmplitude[i] = i == 0 ? 1f : UnityEngine.Random.Range(0.35f, 0.9f);
            when += UnityEngine.Random.Range(settings.strokeGap.x, settings.strokeGap.y);
        }

        elapsed = 0f;
        duration = strokeTime[strokeCount - 1] + decayTau * 5f;
        active = true;
        LastDistance = distance;

        Placed?.Invoke(new LightningStrike(origin, cloudLayer.Bottom, distance, nearness, duration));
    }

    /// The overlapping fades of the strokes. The strongest is taken rather than summing them: two
    /// overlapping strokes summed exceed the peak value and saturate to white.
    float Envelope(float t)
    {
        float value = 0f;

        for (int i = 0; i < strokeCount; i++)
        {
            float age = t - strokeTime[i];
            if (age < 0f) continue;

            float rise = Mathf.Clamp01(age / Mathf.Max(0.0001f, settings.riseSeconds));
            value = Mathf.Max(value, strokeAmplitude[i] * rise * Mathf.Exp(-age / decayTau));
        }

        return value;
    }

    /// THE TABLE IS WRITTEN ONCE. It is a static asset; writing a global every frame is pointless.
    /// If it is not written the fog samples an empty texture and the glow disappears entirely — so
    /// it is called from both `Bind` and `OnEnable` (with the component sitting ready in the
    /// scene, `Bind` may not run).
    void PublishLut()
    {
        if (scatterLut != null) Shader.SetGlobalTexture(ScatterLutId, scatterLut);
        Shader.SetGlobalFloat(ScatterTId, scatterCutoff);
    }

    void Apply(float value)
    {
        flash.intensity = peakIntensity * value;

        // rgb is premultiplied: the sky and the cloud read the same value and do not pick the
        // colour separately. w gives the intensity on its own if needed.
        Glow = peakGlow * value;

        Color glow = settings.flashColor * (peakGlow * value);
        Shader.SetGlobalVector(FlashId, new Vector4(glow.r, glow.g, glow.b, peakGlow * value));

        // The position and the patch's radius. The cloud glows according to the distance from here
        // of the world point it finds by intersecting the ray direction with the layer — so a real
        // place on the sea lights up, not a direction.
        Shader.SetGlobalVector(PositionId,
            new Vector4(origin.x, origin.y, origin.z, Mathf.Max(1f, settings.glowRadius)));

    }
}

/// A strike's counterpart in the world. One place picks the position and the side drawing the bolt reads it.
public readonly struct LightningStrike
{
    /// The discharge point in the cloud. This is the source of the glow.
    public readonly Vector3 Origin;

    /// The elevation of the cloud base. The visible channel starts below it: the part inside the
    /// mass is invisible from the cloud anyway, and starting there hangs the channel in front of the cloud.
    public readonly float CloudBase;

    /// The strike's **ground** distance (metres): how many metres away the storm is.
    ///
    /// The number here is **not** the three-dimensional distance between the eye and the discharge
    /// point. Confusing the two meant the bolt was never drawn: the strike is inside the cloud,
    /// i.e. above two and a half kilometres, so even with the horizontal distance at zero the
    /// three-dimensional distance never falls below that height and the condition "draw the bolt
    /// up to this distance" was always exceeded.
    public readonly float Distance;

    /// 0 distant, 1 near
    public readonly float Nearness;

    /// The glow's total duration (seconds)
    public readonly float Duration;

    public LightningStrike(Vector3 origin, float cloudBase, float distance, float nearness,
        float duration)
    {
        Origin = origin;
        CloudBase = cloudBase;
        Distance = distance;
        Nearness = nearness;
        Duration = duration;
    }
}
