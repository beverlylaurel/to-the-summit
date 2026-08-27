using System;
using UnityEngine;

/// A single audio band. Plays looping clips from a variation list; level, brightness and pitch
/// are supplied from outside.
///
/// The level is smoothed asymmetrically: fast on the way up, slow on the way down. Wind behaves
/// that way physically — a gust arrives suddenly and withdraws slowly.
///
/// The transition between variations is a crossfade between two sources. Waiting for the band to
/// fall silent did not work: a calm band only falls silent when the intensity reaches the
/// extreme, so in practice the first clip looped forever.
public class AudioBand
{
    const float MinCutoff = 400f;
    const float MaxCutoff = 22000f;
    /// A source below this level is paused: playing at zero volume keeps decoding the clip, so
    /// an inaudible band is not free.
    const float SilenceThreshold = 0.004f;
    const float CrossfadeSeconds = 4f;
    const float MinHoldSeconds = 30f;
    const float MaxHoldSeconds = 45f;

    readonly AudioClip[] variations;
    readonly AudioSource[] sources = new AudioSource[2];
    readonly AudioLowPassFilter filter;
    readonly float attackSeconds;
    readonly float releaseSeconds;

    int currentIndex = -1;
    int active;          // the source currently in the foreground
    float blend;         // 0 fully active, 1 fully the other one
    float holdTimer;
    float level;

    /// Each band lives on its own object: in Unity a low-pass filter applies to the mix of every
    /// source on the object, and a per-band filter is only possible this way.
    public AudioBand(Transform parent, string name, AudioClip[] clips, float attack, float release)
    {
        if (clips == null || clips.Length == 0)
            throw new ArgumentException($"{nameof(AudioBand)}: the clip list is empty.");

        variations = clips;
        attackSeconds = attack;
        releaseSeconds = release;

        var host = new GameObject(name);
        host.transform.SetParent(parent, false);

        for (int i = 0; i < sources.Length; i++)
        {
            var source = host.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.bypassReverbZones = true;
            sources[i] = source;
        }

        filter = host.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = MaxCutoff;

        sources[active].clip = NextVariation();
        sources[active].Play();
        holdTimer = UnityEngine.Random.Range(MinHoldSeconds, MaxHoldSeconds);
    }

    /// <param name="target">Target level (0-1).</param>
    /// <param name="brightness">0 muffled, 1 fully open. Drives the low-pass filter.</param>
    /// <param name="pitch">Pitch multiplier. 1 = the clip's own pitch.</param>
    public void Drive(float target, float brightness, float pitch)
    {
        target = Mathf.Clamp01(target);

        float duration = target > level ? attackSeconds : releaseSeconds;
        float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, duration));
        level = Mathf.Lerp(level, target, t);

        AdvanceCrossfade();

        // Equal power: the total energy of the two sources stays constant through the
        // transition, while a linear mix would leave a clear dip in the middle
        SetLevel(sources[active], level * Mathf.Cos(blend * Mathf.PI * 0.5f));
        SetLevel(sources[1 - active], level * Mathf.Sin(blend * Mathf.PI * 0.5f));

        foreach (var source in sources) source.pitch = pitch;

        // The cutoff frequency is shifted on a logarithmic scale; that is how the ear perceives frequency
        filter.cutoffFrequency = MinCutoff * Mathf.Pow(MaxCutoff / MinCutoff, Mathf.Clamp01(brightness));
    }

    /// An inaudible source is paused; only that ends the clip decoding cost.
    /// Because pausing preserves the position it resumes where it left off when turned back on.
    static void SetLevel(AudioSource source, float volume)
    {
        source.volume = volume;

        bool audible = volume > SilenceThreshold;
        if (audible == source.isPlaying) return;

        if (audible) source.UnPause();
        else source.Pause();
    }

    void AdvanceCrossfade()
    {
        if (variations.Length == 1) return;

        // A transition is in progress: when it completes the roles swap and the old source falls silent
        if (blend > 0f)
        {
            blend = Mathf.MoveTowards(blend, 1f, Time.deltaTime / CrossfadeSeconds);
            if (blend < 1f) return;

            sources[active].Stop();
            active = 1 - active;
            blend = 0f;
            holdTimer = UnityEngine.Random.Range(MinHoldSeconds, MaxHoldSeconds);
            return;
        }

        holdTimer -= Time.deltaTime;
        if (holdTimer > 0f) return;

        var next = sources[1 - active];
        next.clip = NextVariation();
        next.time = 0f;
        next.Play();
        blend = Mathf.Epsilon;
    }

    AudioClip NextVariation()
    {
        currentIndex = NextIndexExcluding(currentIndex);
        return variations[currentIndex];
    }

    int NextIndexExcluding(int excluded)
    {
        if (variations.Length == 1) return 0;
        if (excluded < 0) return UnityEngine.Random.Range(0, variations.Length);

        int index = UnityEngine.Random.Range(0, variations.Length - 1);
        return index >= excluded ? index + 1 : index;
    }
}
