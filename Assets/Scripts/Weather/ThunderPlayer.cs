using System;
using UnityEngine;

/// Fires lightning at random intervals. As the precipitation intensifies it grows more frequent
/// and closer. It raises an event at the moment of the strike; the sound arrives afterwards.
[RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
public class ThunderPlayer : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] ThunderSettings settings;

    [Header("Klipler")]
    [SerializeField] AudioClip[] distant;
    [SerializeField] AudioClip[] close;

    /// The speed of sound in air (m/s). Not a setting, a constant.
    const float SpeedOfSound = 340f;

    /// The distance of the strike, metres. Published before the sound — the light arrives
    /// instantly, the sound is on its way.
    ///
    /// The distance is chosen here and this is the only source: both the audio delay and the
    /// strike's place in the world derive from it. Chosen separately, a rumble arriving one and a
    /// half seconds later would belong to a flash eight hundred metres away.
    public event Action<float> Struck;

    AudioSource source;
    AudioLowPassFilter filter;
    float timer;
    int lastDistantIndex = -1;
    int lastCloseIndex = -1;

    // A rumble in flight: the strike happened, the sound has not arrived yet
    AudioClip pendingClip;
    float pendingDelay;
    float pendingVolume;
    float pendingPitch;
    float pendingPan;
    float pendingCutoff;
    float outdoorCutoff = 22000f;

    public void Bind(WeatherState state, ThunderSettings tuning,
        AudioClip[] distantClips, AudioClip[] closeClips)
    {
        weather = state;
        settings = tuning;
        distant = distantClips;
        close = closeClips;
    }

    void OnEnable()
    {
        if (weather == null || settings == null)
            throw new InvalidOperationException($"{nameof(ThunderPlayer)}: dependencies are not assigned.");
        if (distant == null || distant.Length == 0 || close == null || close.Length == 0)
            throw new InvalidOperationException($"{nameof(ThunderPlayer)}: the clip lists are empty.");

        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        // The rumbles are played with PlayOneShot; the source's own clip is not used.
        // But a clipless source carrying a low-pass filter makes Unity print a warning;
        // because playOnAwake is off the assigned clip does not play by itself.
        source.clip = distant[0];

        filter = GetComponent<AudioLowPassFilter>();

        Reschedule();
    }

    void Update()
    {
        ShelterExposure shelter = ShelterExposure.Active;
        float transmission = shelter != null ? shelter.ThunderTransmission : 1f;
        source.volume = transmission;
        filter.cutoffFrequency = Mathf.Min(outdoorCutoff,
            Mathf.Lerp(900f, outdoorCutoff, transmission));

        if (pendingClip != null)
        {
            pendingDelay -= Time.deltaTime;
            if (pendingDelay <= 0f) Boom();
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        Reschedule();

        if (weather.Precipitation < settings.minPrecipitation) return;

        Strike();
    }

    /// Fires without waiting, for testing
    public void TriggerNow() => Strike();

    void Reschedule()
    {
        float interval = Mathf.Lerp(settings.maxInterval, settings.minInterval, weather.Precipitation);
        timer = UnityEngine.Random.Range(interval * 0.6f, interval * 1.4f);
    }

    void Strike()
    {
        // If the previous strike's sound is still in flight it is not dropped, it is delivered
        // at once: overlapping rumbles are normal in a storm, a silently dropped strike is not.
        if (pendingClip != null) Boom();

        // A near rumble only once the precipitation hardens: at the foot it stays distant and calm.
        //
        // Past the threshold it climbs fast. A linear curve stayed nearly zero just above the
        // threshold: one in forty at 0.65. But if the storm has crossed the threshold a near
        // strike is no longer an exception. A square root steepens the curve early and saturates it.
        // THE THRESHOLD IS A HARD CUT. Below it the probability is not "small" but EXACTLY ZERO —
        // in calm weather a near strike never happens, and the design wants that: in quiet rain
        // distant rumbling is heard, no bolt is seen. But with the threshold at 0.6 a heavy 0.56
        // rain also got zero and the bolt was practically never seen (forty strikes tried, all
        // forty distant). It came down to 0.45: at 0.56 a near strike is 38%, at 0.85 it is 72%.
        float closeChance = Mathf.Sqrt(Mathf.InverseLerp(settings.closeThreshold, 1f, weather.Precipitation))
                            * settings.closeChanceAtPeak;
        bool isClose = UnityEngine.Random.value < closeChance;

        pendingClip = isClose
            ? Pick(close, ref lastCloseIndex)
            : Pick(distant, ref lastDistantIndex);

        float fade = 1f;

        // In light precipitation the rumble should be faint too; it strengthens with the intensity
        fade *= Mathf.Lerp(0.45f, 1f, weather.Precipitation);

        // So the same clip is heard as if it came from a different distance every strike
        Vector2 cutoff = isClose ? settings.closeCutoff : settings.distantCutoff;
        pendingCutoff = UnityEngine.Random.Range(cutoff.x, cutoff.y);

        pendingPitch = 1f + UnityEngine.Random.Range(-settings.pitchVariation, settings.pitchVariation);
        pendingPan = UnityEngine.Random.Range(-settings.panVariation, settings.panVariation);
        pendingVolume = Mathf.Clamp01(
            fade * (1f + UnityEngine.Random.Range(-settings.volumeVariation, settings.volumeVariation)));

        Vector2 range = isClose ? settings.closeDistance : settings.distantDistance;
        float distance = UnityEngine.Random.Range(range.x, range.y);

        pendingDelay = distance / SpeedOfSound;

        // Light first. Sound in flight.
        Struck?.Invoke(distance);
    }

    /// The moment the sound arrives
    void Boom()
    {
        outdoorCutoff = pendingCutoff;
        source.pitch = pendingPitch;
        source.panStereo = pendingPan;
        source.PlayOneShot(pendingClip, pendingVolume);

        pendingClip = null;
    }

    AudioClip Pick(AudioClip[] clips, ref int lastIndex)
    {
        if (clips.Length == 1) return clips[0];

        int index = UnityEngine.Random.Range(0, clips.Length - 1);
        if (index >= lastIndex && lastIndex >= 0) index++;

        lastIndex = index;
        return clips[index];
    }
}
