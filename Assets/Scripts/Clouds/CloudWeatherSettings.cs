using UnityEngine;

/// How the world state is translated into cloud settings. The numbers are not buried inside
/// `CloudWeatherDriver`: it is an asset so the same driver can be reused with different settings.
///
/// The values at the calm end deliberately match the approved look in the profile — with the
/// storm at zero it looks as though no link had been added, which makes it possible to tell
/// what the link contributes.
[CreateAssetMenu(menuName = "To The Summit/Cloud Weather Settings")]
public class CloudWeatherSettings : ScriptableObject
{
    // COVERAGE IS NOT HERE. `AtmosphereController` holds its rule (storm mass, dry air rhythm,
    // clear window, test lock) and the cloud consumes it as is. A second mapping placed here
    // would let the sky and the cloud contradict each other.

    [Header("Density")]
    [Tooltip("Density multiplier with no storm.")]
    [Range(0f, 1f)] public float calmDensity = 0.4f;

    [Tooltip("Density multiplier in a full storm.")]
    [Range(0f, 1f)] public float stormDensity = 0.6f;
}
