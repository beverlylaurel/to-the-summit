using System;
using UnityEngine;

/// Blends the rain and wind layers according to the weather.
/// There is no discrete "light/heavy" state; the layers mix with an equal-power crossfade.
public class WeatherAudio : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;

    [Header("Klipler")]
    [SerializeField] AudioClip rainLight;
    [SerializeField] AudioClip rainHeavy;
    [SerializeField] AudioClip[] windCalm;
    [SerializeField] AudioClip[] windStorm;

    [Header("Seviye")]
    [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
    [Tooltip("Driven rain hits surfaces harder; the wind's contribution to the rain sound.")]
    [SerializeField, Range(0f, 0.5f)] float windRainBoost = 0.2f;

    // The wind levels are deliberately not serialized: once in the Inspector the component in
    // the scene freezes on the old value and a change in code has no effect.
    const float WindVolume = 0.55f;   // the wind's level relative to the rain
    const float WindFloor = 0.14f;    // en dingin anda bile duyulan taban

    [Header("Zarf")]
    [Tooltip("Smoothing time while the wind rises. A gust comes fast.")]
    [SerializeField] float windAttack = 0.4f;
    [Tooltip("Smoothing time while the wind falls. A gust withdraws slowly.")]
    [SerializeField] float windRelease = 2.5f;
    [Tooltip("Rain changes with altitude, no fast attack needed.")]
    [SerializeField] float rainSmoothing = 2f;

    [Header("Timbre")]
    [Tooltip("Brightness of calm wind. Low = muffled.")]
    [SerializeField, Range(0f, 1f)] float windCalmBrightness = 0.35f;
    [Tooltip("How much the pitch moves as the wind hardens.")]
    [SerializeField, Range(0f, 0.3f)] float windPitchRange = 0.08f;

    AudioBand light;
    AudioBand heavy;
    AudioBand calm;
    AudioBand storm;

    public void Bind(WeatherState state, WindField windField,
        AudioClip lightClip, AudioClip heavyClip, AudioClip[] calmClips, AudioClip[] stormClips)
    {
        weather = state;
        wind = windField;
        rainLight = lightClip;
        rainHeavy = heavyClip;
        windCalm = calmClips;
        windStorm = stormClips;
    }

    void OnEnable()
    {
        if (weather == null)
            throw new InvalidOperationException($"{nameof(WeatherAudio)}: {nameof(weather)} is not assigned.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(WeatherAudio)}: {nameof(wind)} is not assigned.");
    }

    void Update()
    {
        EnsureBands();

        float precipitation = weather.Precipitation;

        // The sustained intensity decides which sound is playing, the gust how far that sound
        // rises. The two are read separately because the ear hears them separately.
        float sustained = wind.Strength;
        float felt = Mathf.Clamp01(sustained * (1f + wind.Gust));

        DriveRain(precipitation, felt);
        DriveWind(sustained, felt);
    }

    void DriveRain(float precipitation, float felt)
    {
        // THE RAIN SOUND PLAYS IF RAIN IS BEING DRAWN.
        //
        // `SnowRuntimeState.RainWeight01` is the rain's visual weight;
        // `PrecipitationRenderer` multiplies the drop density by it. If the audio does
        // not read the same number, rain is heard while snow falls — which is exactly
        // what happened when the snow system came online.
        float rain = precipitation * SnowRuntimeState.RainWeight01;

        // THE SAME AS THE VISUAL CUTOFF THRESHOLD. `PrecipitationRenderer` drops the
        // drop count to zero below 0.05; if the audio does not go quiet at the same
        // place the player hears drizzle without seeing a single drop.
        rain *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.05f, rain));

        ShelterExposure shelter = ShelterExposure.Active;
        float transmission = shelter != null ? shelter.RainTransmission : 1f;
        float shelterBrightness = shelter != null ? shelter.RainBrightness : 1f;

        float master = rain * masterVolume * transmission
                       * (1f + felt * windRainBoost);

        // Drizzle is muffled, a downpour is bright
        float brightness = Mathf.Lerp(0.55f, 1f, rain) * shelterBrightness;

        light.Drive(master * Mathf.Sqrt(1f - rain), brightness, 1f);
        heavy.Drive(master * Mathf.Sqrt(rain), brightness, 1f);
    }

    /// The level follows the gust, the band crossfade the sustained intensity. Had the crossfade
    /// been tied to the gust as well, the calm and storm mixes would swap places every eight
    /// seconds; what you would hear is not the wind hardening but the sound sliding around.
    void DriveWind(float sustained, float felt)
    {
        ShelterExposure shelter = ShelterExposure.Active;
        float transmission = shelter != null ? shelter.WindTransmission : 1f;
        float master = Mathf.Lerp(WindFloor, 1f, felt) * masterVolume * WindVolume
                       * transmission;

        // As the air speeds up, turbulence produces high frequencies
        float brightness = Mathf.Lerp(windCalmBrightness, 1f, felt)
                           * Mathf.Lerp(0.18f, 1f, transmission);
        float pitch = 1f + (felt - 0.5f) * 2f * windPitchRange;

        calm.Drive(master * Mathf.Sqrt(1f - sustained), brightness, pitch);
        storm.Drive(master * Mathf.Sqrt(sustained), brightness, pitch);
    }

    /// A recompile in Play mode can drop the bands; verified at the point of use.
    void EnsureBands()
    {
        if (light != null) return;

        // Old band objects may survive a reload; prevent duplication
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        light = new AudioBand(transform, "RainLight", new[] { rainLight }, rainSmoothing, rainSmoothing);
        heavy = new AudioBand(transform, "RainHeavy", new[] { rainHeavy }, rainSmoothing, rainSmoothing);
        calm = new AudioBand(transform, "WindCalm", windCalm, windAttack, windRelease);
        storm = new AudioBand(transform, "WindStorm", windStorm, windAttack, windRelease);
    }
}
