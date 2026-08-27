using UnityEngine;

/// How the world state is translated into atmosphere settings. The numbers are not buried
/// inside `SkyWeatherDriver`: it is an asset so the same driver can be reused with different settings.
[CreateAssetMenu(menuName = "To The Summit/Sky Weather Settings")]
public class SkyWeatherSettings : ScriptableObject
{
    // AEROSOL DENSITY = the ZENITH OPACITY of the aerosol column. The fraction of light the
    // aerosol layer absorbs while the observer looks straight up, dimensionless, 0-1.
    //
    // The ends on paper, from the extinction coefficient and a 1.2 km scale height (`[H20 p.605]`):
    //   clean mountain air  sigma ~ 5e-6 m^-1  -> 1 - exp(-0.006) ~ 0.006
    //   the package default sigma = 10e-6      -> 1 - exp(-0.012) ~ 0.012
    //   storm, snow, moisture sigma ~ 60e-6    -> 1 - exp(-0.072) ~ 0.069
    //
    // The range is deliberately narrow: overused, Mie buries the scene in haze and the sky
    // falls to grey (brief, Mie section).

    [Header("Aerosol")]
    [Tooltip("Zenith aerosol opacity with no storm. Clean high mountain air.")]
    [Range(0f, 0.2f)] public float clearAerosol = 0.006f;

    [Tooltip("Zenith aerosol opacity in a full storm. Snow, moisture and blown crystal.")]
    [Range(0f, 0.2f)] public float stormAerosol = 0.069f;
}
