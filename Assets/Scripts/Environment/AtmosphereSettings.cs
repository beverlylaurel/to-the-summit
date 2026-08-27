using UnityEngine;

/// The atmosphere's look settings. Every number of the fog, cloud and sky lives here.
///
/// As long as they sat on the component as `[SerializeField]` there were three copies of each
/// value: the default in code, the serialized copy in the scene, and the asset. The scene wins,
/// and on top of that Unity rewrites the scene to disk from its own memory whenever it likes — a
/// fix made in code disappeared silently. Living in one file, there is no second copy left to diverge.
[CreateAssetMenu(menuName = "To The Summit/Atmosphere", fileName = "AtmosphereSettings")]
public class AtmosphereSettings : ScriptableObject
{
    [Header("Visibility (metres)")]
    [Tooltip("In clear weather, daytime. At two thousand metres on the mountain the real " +
             "clear-air visibility is 100-200 km; in clean air Rayleigh alone gives ~300 km. " +
             "Keeping it low does not only wash out the distance: it also inflates the optical " +
             "depth of the ray climbing 2.6 km from the ground to the cloud and erases the sea of cloud.")]
    public float clearVisibility = 25000f;
    [Tooltip("In the heaviest rain.")]
    public float rainVisibility = 900f;
    [Tooltip("How much further the visibility shortens in a hard wind. A blizzard closes the view.")]
    [Range(0f, 0.9f)] public float windClosure = 0.65f;

    [Header("Colour — daytime")]
    public Color clearDay = new(0.60f, 0.68f, 0.80f);
    public Color rainDay = new(0.42f, 0.45f, 0.50f);

    [Header("Colour — dawn and sunset")]
    [Tooltip("In overcast weather the dawn is dimmer and paler. The tone in clear weather " +
             "comes from TimeOfDay's transmittance computation, it is not chosen here.")]
    public Color duskOvercast = new(0.62f, 0.44f, 0.38f);
    [Tooltip("How dominant the warm tone is.")]
    [Range(0f, 1f)] public float duskStrength = 0.75f;

    [Header("Renk — gece")]
    public Color clearNight = new(0.05f, 0.07f, 0.12f);
    public Color rainNight = new(0.06f, 0.07f, 0.09f);

    [Header("Cloud coverage")]
    [Tooltip("The cloud coverage in clear weather.")]
    [Range(0f, 1f)] public float clearCoverage = 0.18f;
    [Tooltip("The coverage in the heaviest precipitation.")]
    [Range(0f, 1f)] public float stormCoverage = 0.95f;
    [Tooltip("The coverage's lower bound. Below it the sky is empty and the clouds look thin.")]
    [Range(0f, 1f)] public float minCoverage = 0.4f;
    [Tooltip("The coverage reached when the clear window is fully open. This is the only thing that can pierce " +
             "the floor: rare, short, and the reward of the climb — the clouds part and the summit shows.")]
    [Range(0f, 1f)] public float openCoverage = 0.1f;
    [Tooltip("The coverage's gain with the severity. The sky closes before the precipitation " +
             "fully hardens; not a shift in time but a steepening of the curve.")]
    [Range(0f, 1f)] public float coverageGain = 0.35f;

    [Header("Cloud layer")]
    [Tooltip("The cloud layer's lower bound (metres).")]
    public float cloudBottom = 2600f;
    [Tooltip("The cloud base in calm weather (metres). On a rainless, windless night the cold " +
             "air settles into the valley, the base comes down here and a sea of cloud is seen from the summit.")]
    public float calmCloudBottom = 1700f;
    [Tooltip("The time the base takes to reach its new height (seconds). The mass is heavy, " +
             "sekiz saniyelik esintileriyle inip kalkmaz.")]
    public float cloudBottomSmoothing = 120f;

    [Header("Bulut kalitesi")]
    [Tooltip("At how many times the visibility the cloud blends completely into the atmosphere. " +
             "The clouds are kilometres up and the air at that height is clearer.")]
    [Range(2f, 12f)] public float hazeVisibilityFactor = 5.5f;
    [Tooltip("The ceiling of the blend distance (metres). Altitude thins the air but does not " +
             "make it infinitely clear; on a horizontal view the ray still crosses kilometres of " +
             "air. Left without a ceiling, at the summit the horizon of the sea of cloud ends as " +
             "a bare line without sinking into the haze. This is NOT THE DRAW RADIUS: where the " +
             "sea ends is set by the planet radius. If this number is made equal to the sea's own " +
             "horizon the cloud at the horizon goes into full blend and disappears — it is kept markedly longer.")]
    public float maxHazeDistance = 55000f;

    [Tooltip("The radius of the cloud sphere (metres). The distance at which the sea meets the " +
             "horizon is sqrt(2·R·Δh), and Δh here is the eye's margin ABOVE THE SEA — a few " +
             "hundred metres at most at the summit. Shrinking it bends the sea down early and " +
             "destroys the clouds at the horizon from the summit: at 235 km the sea ended at " +
             "13 km. At the real radius the edge stays beyond the distance where the fade closes and is never seen.")]
    public float planetRadius = 6360000f;

    [Tooltip("The floor of the blend distance (metres). The visibility measures the air at your " +
             "feet: dense low air and falling precipitation, a layer a few hundred metres deep. " +
             "The cloud layer is above it and kilometres wide — a valley closing in with fog does " +
             "not set how much of the sea of cloud you see. Left without a floor the range fell " +
             "to four kilometres in a storm and to three hundred metres inside a cloud, and the " +
             "cloud right beside you was not drawn.")]
    public float minHazeDistance = 16000f;

    [Header("Height fog")]
    [Tooltip("The height difference over which the density halves in clear weather (metres). " +
             "The layer's SHALLOWNESS is the screw that separates horizontal from vertical haze: " +
             "a horizontal ray stays at the eye's elevation and travels kilometres inside the " +
             "layer (a distant ridge fades), while a ray to the cloud leaves it within a few hundred metres.")]
    public float fogHalfHeightClear = 400f;
    [Tooltip("The depth in a downpour (metres). In precipitation the column mixes vertically and " +
             "the rain fills it from top to bottom; left shallow it gave 5 km of visibility in a downpour at 1000 m.")]
    public float fogHalfHeightStorm = 2000f;
    [Tooltip("The elevation the density is measured at (metres). Usually the terrain's base.")]
    public float fogBaseAltitude;
    [Tooltip("The extinction budget at the visibility distance. 3.9 = Koschmieder: when we say " +
             "'visibility X metres', an object at X metres really does disappear (2% contrast). " +
             "A small value shows the fog thinner than it is — the HUD said 320 while things were visible out to 800.")]
    [Range(0.5f, 6f)] public float fogThickness = 3.9f;
    [Tooltip("The inversion ceiling (metres): cold air is trapped in the valley, warm air stands " +
             "above it and the two do not mix.")]
    public float inversionHeight = 1700f;
    [Tooltip("The softness of the cut (metres). Kept narrow, the boundary looks like a sharp surface.")]
    public float inversionWidth = 220f;
    [Tooltip("The free troposphere's visibility (metres). The air left above the inversion — " +
             "the air's own molecules. Independent of the precipitation: weather lives in the " +
             "boundary layer. The physical value is ~290 km (Rayleigh, β_green 13.6e-6 → " +
             "3.9/β). Reining it in strengthens the aerial perspective at high elevations but " +
             "also closes the cloud veil; the two share the same path.")]
    public float freeAirVisibility = 290000f;
    [Tooltip("The free layer's half height (metres). The Rayleigh scale height " +
             "8000 m × ln2. Many times broader than the boundary layer's — that is what separates them.")]
    public float freeAirHalfHeight = 5545f;
    [Tooltip("Precipitation raises the ceiling: in a storm moisture is carried upward and the fog band thickens.")]
    public float inversionStormRise = 900f;
    [Tooltip("How fast the fog disperses as the sun rises.")]
    [Range(0.1f, 1f)] public float valleyFogBurnOff = 0.45f;
    // 600 → 2200 m. At 600 m the valley floor was fogged like a WALL: even at 186 m the
    // visibility stayed at 1.8 km and the cloud layer at 1.7 km could not get through a 3 km path
    // and was erased completely.
    // 2200 m is a haze level: the valley looks filled and someone on the slope also sees the clouds.
    public float dawnSeaVisibility = 2200f;

    [Tooltip("The half-height scale of the dawn sea of fog (metres). Much smaller than the " +
             "general fog's: radiative valley fog is a SHALLOW layer 30-300 m deep, and from " +
             "above it you see a sea of fog. With the general scale (1400 m) the sea stretched " +
             "kilometres upward and a player 200 m above the valley was trapped at " +
             "600 m of visibility too.")]
    public float dawnSeaHalfHeight = 120f;
    [Tooltip("The strength of the fog banks in clear weather. At zero the fog is a uniform soup; " +
             "the banks do not wander and a slope is not wrapped and uncovered.")]
    [Range(0f, 1f)] public float fogBankClear = 0.35f;
    [Tooltip("The banks' strength in precipitation: storm fog wraps more patchily.")]
    [Range(0f, 1f)] public float fogBankStorm = 0.75f;
    [Tooltip("The visibility's breathing margin on the scale of minutes: within the same storm " +
             "the fog thickens and thins in episodes, it never stands still.")]
    [Range(0f, 0.5f)] public float visibilityBreathing = 0.2f;

    [Header("Cloud band")]
    [Tooltip("How far below the cloud base the foggy band starts (metres).")]
    public float deckLeadMeters = 400f;
    [Tooltip("The band's thickness (metres).")]
    public float deckThickness = 900f;
    [Tooltip("The visibility inside the band (metres).")]
    public float deckVisibility = 60f;
    [Tooltip("The visibility when a bank parts inside the band (metres). " +
             "The inside of a cloud is not a uniform soup: a slope appears and disappears.")]
    public float deckOpenVisibility = 260f;
    [Tooltip("The band's density varies with the weather; some of it is present in clear weather too.")]
    [Range(0f, 1f)] public float deckClearAmount = 0.35f;

    [Header("Shadow")]
    [Tooltip("The shadow distance in clear weather (metres). As the fog closes in this value falls.")]
    public float maxShadowDistance = 150f;
    [Tooltip("The shadow distance should be this share of the visibility. A shadow inside fog is not seen.")]
    [Range(0.3f, 1f)] public float shadowVisibilityRatio = 0.8f;
    [Tooltip("The softness of the change (seconds).")]
    public float transitionSeconds = 3f;
}
