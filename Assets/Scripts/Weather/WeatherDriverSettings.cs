using UnityEngine;

/// The settings of the altitude-driven weather driver: the severity curve, the fluctuation, the
/// clear window, the smoothing and the descent behaviour.
///
/// As long as they sat on the component as `[SerializeField]` there were three copies of each
/// value: the default in code, the serialized copy in the scene, and the one actually running.
/// The scene wins, and on top of that Unity rewrites the scene to disk from its own memory
/// whenever it likes — a fix made in code was silently reverted.
///
/// The band elevations are not here and must not be: they are not settings but measures derived
/// from the mountain. The setup script computes them from the mountain's ground and summit and
/// writes them to the component; when the mountain changes they shift on their own.
[CreateAssetMenu(menuName = "To The Summit/Weather Driver", fileName = "WeatherDriverSettings")]
public class WeatherDriverSettings : ScriptableObject
{
    [Header("Opening")]
    [Tooltip("The altitude the opening ends at (metres above the ground). Up to here the weather stays calm.")]
    public float openingRise = 400f;
    [Tooltip("The severity during the opening. It has to be very light.")]
    /// ZERO: the run starts in CLEAR weather. It was 0.12 and left a constant light
    /// precipitation at the base; when the climate was pulled towards winter that precipitation
    /// turned to snow and the game started stormy from the first second. The design says
    /// "clear at the start, a front midway through the approach" (see DECISIONS.md).
    [Range(0f, 1f)] public float openingIntensity;

    [Header("Severity curve")]
    [Tooltip("The rain's severity at its ceiling.")]
    [Range(0f, 1f)] public float rainPeak = 0.65f;
    [Tooltip("The severity once the high band settles in. Lower than the rain ceiling: the storm starts calm.")]
    [Range(0f, 1f)] public float stormBase = 0.4f;
    [Tooltip("The severity just before entering the summit storm.")]
    [Range(0f, 1f)] public float stormPeak = 0.9f;

    [Header("Dalgalanma")]
    [Tooltip("The fluctuation amplitude in the rain zone.")]
    [Range(0f, 1f)] public float rainVariation = 0.4f;
    [Tooltip("The fluctuation amplitude in the high band. The real variety is here.")]
    [Range(0f, 1f)] public float stormVariation = 0.55f;
    [Tooltip("How fast the weather's general state changes. 0.005 ≈ 3.5 minutes.")]
    public float slowFrequency = 0.005f;
    [Tooltip("The rate of the short gusts. 0.02 ≈ 50 seconds.")]
    public float fastFrequency = 0.02f;

    [Tooltip("The share of the amplitude left in the summit band. Set to 0, the weather up " +
             "there pins to a single constant downpour and does not change for hours. With 0.3 " +
             "the range is 0.70-1.00: the summit is still merciless but not dead.")]
    [Range(0f, 1f)] public float summitVariation = 0.3f;

    [Header("Clear window")]
    [Tooltip("Rarely the weather clears completely: the clouds part and the summit shows.")]
    [Range(0f, 1f)] public float clearWindowStrength = 0.8f;
    [Tooltip("How often clear windows occur. 0.0025 ≈ one attempt every 7 minutes.")]
    public float clearWindowFrequency = 0.0025f;
    [Tooltip("What is left of the precipitation when the window is fully open. With its own " +
             "slow noise it wanders between 0 and this value: in most windows the precipitation " +
             "stops completely, in some it keeps drizzling. Held constant, every opening was " +
             "identical to the last.")]
    [Range(0f, 0.5f)] public float clearWindowResidue = 0.22f;
    [Tooltip("How fast the residue changes. It has to be independent of the window's own " +
             "frequency; at the same frequency the two lock and every window opens to the same depth.")]
    public float clearWindowResidueFrequency = 0.0009f;

    [Header("Smoothing")]
    [Tooltip("The time the severity takes to reach the target (seconds). Prevents a sudden jump from downpour to calm snow.")]
    public float smoothingSeconds = 25f;
    [Tooltip("The time the cloud mass takes to reach the target (seconds). It has to be much " +
             "longer than the precipitation's: a cloud lingers for a while after the rain " +
             "stops. Made equal, the sky clears in the frame the precipitation stops.")]
    public float cloudLagSeconds = 150f;

    [Header("Dry-air cloudiness")]
    [Tooltip("Even without precipitation the sky does not stay empty: a low passes, moisture " +
             "is carried, coverage wanders over hours. This value is that wandering's period (seconds).")]
    public float cloudWanderSeconds = 420f;
    [Tooltip("The lowest value coverage can fall to in dry weather.")]
    [Range(0f, 1f)] public float dryCoverageLow = 0.4f;
    [Tooltip("The highest value coverage can rise to in dry weather. The sky can close without " +
             "precipitation too — overcast does not mean rainy.")]
    [Range(0f, 1f)] public float dryCoverageHigh = 0.85f;
    [Tooltip("Descending this far below the reached level does not affect the weather (metres). " +
             "Col crossings and route deviations should not take the storm back.")]
    public float descentDeadband = 250f;
    [Tooltip("How long the weather takes to retreat when the descent goes past the dead band (seconds).")]
    public float descentSeconds = 90f;

    [Header("Wind")]
    [Tooltip("The wind severity during the opening.")]
    [Range(0f, 1f)] public float windAtBase = 0.2f;
}
