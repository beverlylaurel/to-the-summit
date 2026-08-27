using UnityEngine;
using UnityEngine.Rendering;

/// Translates the world state into the atmosphere volume. The sky does not know the weather
/// and the weather does not know the sky; the translation happens only here.
///
/// The sun's direction and color DO NOT PASS through here: `TimeOfDay` drives both lights and
/// the package computes the sky from the same lights. Opening a second path would produce a
/// contradiction like "the sky turned red but the shadows point at noon".
///
/// The class compiles unconditionally and only its body depends on the package: there is no
/// package type in the `Bind` signature, so the scene setup and the F1 panel compile even
/// before the definition is installed.
public class SkyWeatherDriver : MonoBehaviour
{
    [Tooltip("The Volume component carrying the atmosphere settings.")]
    [SerializeField] Volume skyVolume;

    [Tooltip("Source of the precipitation intensity.")]
    [SerializeField] WeatherState weather;

    [Tooltip("Component providing the star field's rotation axis and the time.")]
    [SerializeField] TimeOfDay time;

    [SerializeField] SkyWeatherSettings settings;

#if URP_PBSKY
    PhysicallyBasedSky sky;
#endif

    public void Bind(Volume skyVolumeRef, WeatherState weatherRef, TimeOfDay timeRef,
        SkyWeatherSettings settingsRef)
    {
        skyVolume = skyVolumeRef;
        weather = weatherRef;
        time = timeRef;
        settings = settingsRef;
    }

    void OnEnable()
    {
        if (skyVolume == null || weather == null || time == null || settings == null)
            throw new System.InvalidOperationException($"{nameof(SkyWeatherDriver)}: dependencies are not assigned.");

#if URP_PBSKY
        if (!skyVolume.profile.TryGet(out sky))
            throw new System.InvalidOperationException($"{nameof(SkyWeatherDriver)}: profilde {nameof(PhysicallyBasedSky)} yok.");

        // Blending skips fields with `overrideState` off; every driven field has to be on.
        sky.aerosolDensity.overrideState = true;
        sky.spaceRotation.overrideState = true;
#endif
    }

    void Update()
    {
#if URP_PBSKY
        sky.aerosolDensity.value =
            Mathf.Lerp(settings.clearAerosol, settings.stormAerosol, weather.Precipitation);

        // THE STARS TURN ABOUT THE CELESTIAL POLE, one turn per day. The axis is the same as the
        // sun's; given a separate axis the sun and the stars would turn in different directions.
        //
        // The shader rotates the lookup direction (`mul(-V, _SpaceRotation)`), i.e. the star field
        // shifts the other way — which is why the angle's sign is negative.
        sky.spaceRotation.value =
            Quaternion.AngleAxis(-time.Normalized * 360f, time.CelestialPole).eulerAngles;

        // THE DAYTIME FADING OF THE STARS COMES FROM THE SUN'S ELEVATION. The shader holds a
        // bright star until -3° and the faintest until -18°; the threshold varies by magnitude.
        //
        // NOT A SECOND TIME SOURCE: the value comes from `TimeOfDay`'s sun direction, i.e. from
        // the same single state as the shadows and the sky computation.
        Shader.SetGlobalVector(StarFieldParamsId,
            new Vector4(time.SunDirection.y, 0f, 0f, 0f));
#endif
    }

#if URP_PBSKY
    static readonly int StarFieldParamsId = Shader.PropertyToID("_StarFieldParams");
#endif
}
