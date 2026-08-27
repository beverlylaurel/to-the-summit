using UnityEngine;
using UnityEngine.Rendering;

/// TRANSLATES THE WORLD STATE INTO CLOUD SETTINGS. The system drawing the clouds is a render
/// feature; it cannot read the weather, the wind and the clock itself. This component is a
/// one-way bridge: it reads the world and writes to the cloud Volume. There is no reverse
/// direction — what the cloud is doing is told by `CloudLayerProbe`.
///
/// The links are added one at a time; the sky is looked at again after each one
/// (see `CLOUDS_REBUILD.md`).
public class CloudWeatherDriver : MonoBehaviour
{
    [Tooltip("The Volume carrying the cloud settings.")]
    [SerializeField] Volume cloudVolume;

    [Tooltip("The wind field. Free-air speed and prevailing direction come from here.")]
    [SerializeField] WindField wind;

    [Tooltip("The storm source. Density comes from here.")]
    [SerializeField] AltitudeWeatherDriver weatherDriver;

    [Tooltip("The ONLY source of global coverage. The rule lives there; here it is only consumed.")]
    [SerializeField] AtmosphereController atmosphere;

    [Tooltip("Conversion coefficients from the world state to cloud settings.")]
    [SerializeField] CloudWeatherSettings settings;

    /// The port's `globalSpeed` is km/h: the transition multiplies `deltaTime` by 1/3.6
    /// (`VolumetricCloudsURP`, `deltaTime *= -0.277778f`). The wind field gives m/s.
    const float MetersPerSecondToKilometersPerHour = 3.6f;

    VolumetricClouds clouds;

    void OnEnable()
    {
        if (cloudVolume == null || wind == null || weatherDriver == null
            || atmosphere == null || settings == null)
            throw new System.InvalidOperationException($"{nameof(CloudWeatherDriver)}: dependencies are not assigned.");

        if (!cloudVolume.profile.TryGet(out clouds))
            throw new System.InvalidOperationException($"{nameof(CloudWeatherDriver)}: profilde {nameof(VolumetricClouds)} yok.");

        // Blending skips fields whose `overrideState` is off; every driven field has to be on.
        clouds.globalSpeed.overrideState = true;
        clouds.globalOrientation.overrideState = true;
        clouds.cloudCoverage.overrideState = true;
        clouds.densityMultiplier.overrideState = true;
    }

    [Tooltip("The lowest speed the cloud layer keeps while the surface is calm (m/s). " +
             "The free atmosphere is not affected by surface friction.")]
    [SerializeField] float calmAloftSpeed = 2f;

    void Update()
    {
        // The direction comes from the PREVAILING wind, not the instantaneous speed: a gust's
        // direction wobbles within fractions of a second and a cloud mass does not behave that way.
        Vector3 heading = wind.PrevailingDirection;
        float degrees = Mathf.Atan2(heading.z, heading.x) * Mathf.Rad2Deg;
        if (degrees < 0f) degrees += 360f;

        // THE CLOUD BASE IS SEPARATE. Because of friction the surface wind nearly
        // stops in calm weather (`WindSettings.calmSpeed` 0.6 m/s); the free
        // atmosphere does not. Sharing the same number, the sky would freeze on a calm day.
        float aloft = Mathf.Max(wind.FreeAirSpeed, calmAloftSpeed);

        clouds.globalSpeed.value = aloft * MetersPerSecondToKilometersPerHour;
        clouds.globalOrientation.value = degrees;

        // COVERAGE COMES FROM THE ATMOSPHERE. The rule stands there in one place: storm mass,
        // dry-air rhythm, the clear window and the test lock. Had a second mapping been built
        // here, the sky could say "overcast" while the clouds said "clear".
        clouds.cloudCoverage.value = atmosphere.Coverage;

        // Density comes from `CloudMass` — NOT `WeatherState.Precipitation`. Precipitation is cut
        // at the ceiling: it zeroes above the cloud and would form a coverage → top → ceiling cut →
        // precipitation → coverage loop. `CloudMass` is the lagged form of precipitation; when it is
        // cut the cloud does not disperse at once. The contract is written in `AltitudeWeatherDriver.StormIntensity`.
        // COVERAGE ENTERS THE OPTICAL THICKNESS TOO. Density used to come only from `CloudMass`,
        // i.e. from precipitation. In rainless overcast weather the cover thinned optically and
        // even at 100% coverage the STARS came through (measured: they are visible looking up
        // from the ground).
        //
        // In reality a cloud's thickness does not depend on precipitation: a rainless stratus cuts
        // a star completely too, and a single cumulus is opaque as well. Coverage means "how much
        // of the sky is covered"; what is covered has to be opaque.
        //
        // The LARGER of the two is taken, not the sum: the storm mass and the overcast describe the
        // same phenomenon from two ends, and each thickens the cover on its own.
        float opticalDrive = Mathf.Max(weatherDriver.CloudMass, atmosphere.Coverage);

        clouds.densityMultiplier.value =
            Mathf.Lerp(settings.calmDensity, settings.stormDensity, opticalDrive);
    }

    public void Bind(Volume cloudVolumeRef, WindField windRef,
        AltitudeWeatherDriver weatherDriverRef, AtmosphereController atmosphereRef,
        CloudWeatherSettings settingsRef)
    {
        cloudVolume = cloudVolumeRef;
        wind = windRef;
        weatherDriver = weatherDriverRef;
        atmosphere = atmosphereRef;
        settings = settingsRef;
    }
}
