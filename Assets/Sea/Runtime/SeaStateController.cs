// ROLE: turns local weather and independent remote swell events into one continuous sea state.
// CALLED BY: SeaEnvironmentBridge. DebugMenu can replace its output with measured presets.

using UnityEngine;

public enum SeaTestPreset
{
    Natural,
    Calm,
    WindSea,
    Groundswell,
    CrossSea,
    Storm,
    Extreme,
}

/// THE SEA HAS MEMORY AND THE SWELL HAS ITS OWN WEATHER.
///
/// Local wind does not create a mature wave field in one frame and an existing sea does not
/// disappear when the wind drops. The filtered U10 below is the cheapest useful form of wave-age
/// memory. Remote swell is sampled from independent, smoothly joined keyframes: period, energy and
/// direction do not ride one Perlin value and direction is absolute world space, not an offset from
/// today's local wind.
[DefaultExecutionOrder(-190)]
[DisallowMultipleComponent]
public sealed class SeaStateController : MonoBehaviour
{
    [SerializeField] SeaSettings settings;
    [SerializeField] WindField wind;

    [SerializeField] SeaTestPreset testPreset;

    float filteredWindSpeed;
    Vector3 filteredWindDirection = Vector3.forward;
    bool filterReady;

    public SeaTestPreset TestPreset => testPreset;
    public bool IsOverridden => testPreset != SeaTestPreset.Natural;

    public void Bind(SeaSettings source, WindField windSource)
    {
        settings = source;
        wind = windSource;
        ResetWindMemory();
    }

    public void SetTestPreset(SeaTestPreset preset)
    {
        testPreset = preset;
    }

    void OnEnable() => ResetWindMemory();

    void ResetWindMemory()
    {
        if (wind == null) return;

        filteredWindSpeed = wind.SeaLevelSpeed;
        filteredWindDirection = Horizontal(wind.PrevailingDirection, Vector3.forward);
        filterReady = true;
    }

    void Update()
    {
        if (settings == null || wind == null || IsOverridden) return;

        float targetSpeed = wind.SeaLevelSpeed;
        Vector3 targetDirection = Horizontal(wind.PrevailingDirection, filteredWindDirection);

        if (!filterReady)
        {
            filteredWindSpeed = targetSpeed;
            filteredWindDirection = targetDirection;
            filterReady = true;
            return;
        }

        float tau = targetSpeed > filteredWindSpeed
                  ? settings.windSeaRiseSeconds
                  : settings.windSeaFallSeconds;
        float blend = 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(tau, 1f));

        filteredWindSpeed = Mathf.Lerp(filteredWindSpeed, targetSpeed, blend);
        filteredWindDirection = Vector3.Slerp(filteredWindDirection, targetDirection, blend).normalized;
    }

    public float WindSpeed
    {
        get
        {
            switch (testPreset)
            {
                case SeaTestPreset.Calm:        return 2.0f;
                case SeaTestPreset.WindSea:     return 10.0f;
                case SeaTestPreset.Groundswell: return 3.0f;
                case SeaTestPreset.CrossSea:    return 10.0f;
                case SeaTestPreset.Storm:       return 14.0f;
                case SeaTestPreset.Extreme:     return 14.0f;
                default:
                    if (filterReady) return filteredWindSpeed;
                    return wind != null ? wind.SeaLevelSpeed : 8f;
            }
        }
    }

    public Vector3 WindDirection
    {
        get
        {
            switch (testPreset)
            {
                case SeaTestPreset.Calm:
                case SeaTestPreset.WindSea:
                case SeaTestPreset.Groundswell:
                case SeaTestPreset.CrossSea:
                case SeaTestPreset.Storm:
                case SeaTestPreset.Extreme:
                    return Heading(205f);
                default:
                    if (filterReady) return filteredWindDirection;
                    return wind != null ? Horizontal(wind.PrevailingDirection, Vector3.forward)
                                        : Vector3.forward;
            }
        }
    }

    public float SwellPeriod
    {
        get
        {
            switch (testPreset)
            {
                case SeaTestPreset.Calm:        return 8f;
                case SeaTestPreset.WindSea:     return 8f;
                case SeaTestPreset.Groundswell: return 16f;
                case SeaTestPreset.CrossSea:    return 13f;
                case SeaTestPreset.Storm:       return 14f;
                case SeaTestPreset.Extreme:     return 17f;
                default:                        return NaturalSwellPeriod(Now);
            }
        }
    }

    public float SwellEnergyScale
    {
        get
        {
            switch (testPreset)
            {
                case SeaTestPreset.Calm:        return 0.45f;
                case SeaTestPreset.WindSea:     return 0.55f;
                case SeaTestPreset.Groundswell: return 6.0f;
                case SeaTestPreset.CrossSea:    return 4.0f;
                case SeaTestPreset.Storm:       return 5.0f;
                case SeaTestPreset.Extreme:     return 9.0f;
                default:                        return NaturalSwellEnergy(Now);
            }
        }
    }

    public Vector3 SwellDirection
    {
        get
        {
            switch (testPreset)
            {
                case SeaTestPreset.Calm:        return Heading(188f);
                case SeaTestPreset.WindSea:     return Heading(220f);
                case SeaTestPreset.Groundswell: return Heading(150f);
                case SeaTestPreset.CrossSea:    return Heading(115f);
                case SeaTestPreset.Storm:       return Heading(196f);
                case SeaTestPreset.Extreme:     return Heading(160f);
                default:                        return NaturalSwellDirection(Now);
            }
        }
    }

    public float RemoteEvent01
    {
        get
        {
            if (IsOverridden)
                return Mathf.InverseLerp(settings.swellEnergyMin, settings.swellEnergyMax,
                                         SwellEnergyScale);

            return EventStrength(Now);
        }
    }

    float Now => Application.isPlaying ? Time.time : 0f;

    float NaturalSwellPeriod(float time)
    {
        float eventStrength = EventStrength(time);
        float independent = SmoothNoise(time, settings.seaStateSegmentSeconds * 0.73f, 0xA511E9B3u);
        float driver = Mathf.Clamp01(eventStrength * 0.62f + independent * 0.38f);
        float period = Mathf.Lerp(settings.swellPeriodShort, settings.swellPeriodLong, driver);
        return Mathf.Round(period * 4f) * 0.25f;
    }

    float NaturalSwellEnergy(float time)
    {
        float eventStrength = EventStrength(time);
        float independent = SmoothNoise(time + 97.3f, settings.seaStateSegmentSeconds * 1.17f,
                                        0x63D83595u);
        float driver = Mathf.Clamp01(eventStrength * 0.72f + independent * 0.28f);

        // Strong remote events are uncommon, but unlike the old timeline they are not forced
        // to wait 22 minutes after every Play press. The seed chooses where in the sequence the
        // session begins, and F1 presets make every state immediately testable.
        driver = Mathf.Pow(driver, 1.35f);
        return Mathf.Lerp(settings.swellEnergyMin, settings.swellEnergyMax, driver);
    }

    Vector3 NaturalSwellDirection(float time)
    {
        float seconds = Mathf.Max(settings.swellDirectionSegmentSeconds, 120f);
        float x = time / seconds + settings.seaStateSeed * 0.00137f;
        int i = Mathf.FloorToInt(x);
        float f = Smooth01(x - i);
        float a = Hash01(i, 0xB5297A4Du) * 360f;
        float b = Hash01(i + 1, 0xB5297A4Du) * 360f;
        return Heading(a + Mathf.DeltaAngle(a, b) * f);
    }

    float EventStrength(float time)
    {
        float broad = SmoothNoise(time, settings.seaStateSegmentSeconds, 0x1B56C4E9u);
        float detail = SmoothNoise(time + 211.7f, settings.seaStateSegmentSeconds * 0.43f,
                                   0x9E3779B9u);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(broad * 0.78f + detail * 0.22f));
    }

    float SmoothNoise(float time, float seconds, uint salt)
    {
        float x = time / Mathf.Max(seconds, 1f) + settings.seaStateSeed * 0.00137f;
        int i = Mathf.FloorToInt(x);
        float f = Smooth01(x - i);
        return Mathf.Lerp(Hash01(i, salt), Hash01(i + 1, salt), f);
    }

    static float Smooth01(float x) => x * x * (3f - 2f * x);

    static float Hash01(int value, uint salt)
    {
        unchecked
        {
            uint x = (uint)value ^ salt;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return (x & 0x00FFFFFFu) / 16777216f;
        }
    }

    static Vector3 Heading(float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
    }

    static Vector3 Horizontal(Vector3 value, Vector3 fallback)
    {
        value.y = 0f;
        return value.sqrMagnitude > 1e-8f ? value.normalized : fallback;
    }
}
