using UnityEngine;

/// The look of the mountain surface. WHERE the layers go is read from the mountain's own shape
/// (see SurfaceMapBaker); the values here only set how much of them there is and in what colour.
[CreateAssetMenu(menuName = "To The Summit/Terrain Material", fileName = "TerrainMaterialSettings")]
public class TerrainMaterialSettings : ScriptableObject
{
    /// A counter incremented every time a setting changes. `TerrainSurface` reads it and only
    /// writes to the material when something really changed: forty-odd fields were being sent
    /// again every frame and none of them changes on its own.
    ///
    /// The Inspector and the settings window trigger `OnValidate`, so while tuning live the
    /// feedback is immediate.
    [System.NonSerialized] public int revision;

    void OnValidate() => revision++;

    [Header("Kaya")]
    public Color rockPrimary = new(0.13f, 0.14f, 0.16f);
    [Tooltip("The secondary rock that shows in the geological bands.")]
    public Color rockSecondary = new(0.27f, 0.26f, 0.24f);
    [Tooltip("The scale of the surface grain. A large value means fine grain.")]
    public float grainScale = 0.08f;
    [Range(0f, 1f)] public float grainStrength = 0.35f;
    [Tooltip("The rock's gloss. Wet rock goes above this.")]
    [Range(0f, 1f)] public float rockSmoothness = 0.12f;

    [Header("Jeolojik bantlar")]
    [Tooltip("The thickness of one band (metres).")]
    public float bandThickness = 130f;
    [Tooltip("How much the bands are bent by tectonics (metres). Zero = artificial straight lines.")]
    public float bandWarp = 150f;
    public float bandWarpScale = 0.0016f;
    [Range(0f, 1f)] public float bandContrast = 0.5f;

    [Header("Altitude tone")]
    [Tooltip("The warm earth tone mixed into the rock at low altitude.")]
    public Color lowlandTint = new(0.30f, 0.26f, 0.20f);
    [Tooltip("The cold tone left by glacial abrasion at high altitude.")]
    public Color alpineTint = new(0.29f, 0.32f, 0.37f);
    [Tooltip("The altitude at which the earth tone ends completely (metres).")]
    public float lowlandCeiling = 1400f;
    [Tooltip("The altitude at which the cold tone starts (metres).")]
    public float alpineFloor = 3200f;
    [Range(0f, 1f)] public float altitudeTintStrength = 0.5f;

    [Header("Lichen — concavity, shade and altitude")]
    public Color lichenColor = new(0.33f, 0.35f, 0.21f);
    [Range(0f, 1f)] public float lichenAmount = 0.5f;
    [Tooltip("The highest elevation lichen can live at (metres).")]
    public float lichenCeiling = 2600f;
    [Tooltip("How much of a hollow the moisture needs. A high value puts it only in deep clefts.")]
    [Range(0f, 1f)] public float lichenMoistureBias = 0.55f;
    [Tooltip("How much the lichen dries out on sunlit faces.")]
    [Range(0f, 1f)] public float lichenSunSensitivity = 0.7f;

    [Header("Oxide — follows the geological bands")]
    public Color oxideColor = new(0.40f, 0.20f, 0.10f);
    [Tooltip("The share of the iron-bearing layers. The stains do not leave the bands.")]
    [Range(0f, 1f)] public float oxideAmount = 0.3f;
    [Tooltip("Stain scale. A small value means wide stains.")]
    public float oxideScale = 0.004f;

    [Header("Gravel — from the accumulation map")]
    public Color screeColor = new(0.30f, 0.29f, 0.27f);
    [Range(0f, 1f)] public float screeAmount = 0.6f;
    [Tooltip("The thresholds where gravel begins and reaches full in the accumulation map. " +
             "With a narrow range only the densest gullies are picked.")]
    public Vector2 screeRange = new(0.62f, 0.88f);
    [Tooltip("The steepest angle gravel can hold on (degrees).")]
    [Range(10f, 60f)] public float screeSlopeLimit = 38f;

    /// The procedural surface's seed. The rock band, the oxide, the lichen, the grain, the
    /// fracture and the accumulation shape are all tied to world coordinates; without changing
    /// the seed, the same pattern appears at the same coordinate even if the terrain is
    /// regenerated from scratch.
    ///
    /// It is incremented when the mountain is regenerated too, otherwise places from the old
    /// mountain feel familiar — this happened once and was measured.
    public int patternSeed = 2;

    [Header("Wetness")]
    [Tooltip("How much the rock darkens in precipitation.")]
    [Range(0f, 1f)] public float wetDarkening = 0.45f;
    [Tooltip("The gloss a wet surface gains.")]
    [Range(0f, 1f)] public float wetSmoothness = 0.6f;
    [Tooltip("The drying time after the precipitation stops (seconds).")]
    public float dryingSeconds = 120f;

    [Header("Surface relief")]
    [Tooltip("The strength of the procedural normal. Zero = a plastic look.")]
    [Range(0f, 2f)] public float bumpStrength = 0.9f;
    public float bumpScale = 0.3f;

    [Header("Alpenglow — dawn and sunset")]
    // 0.9 -> 0.35: the glow used to be reined in by the altitude ramp (×0.35 at the base).
    // When the ramp was handed over to the Earth's shadow that reining disappeared and every
    // surface got the full strength — at sunset the scene was painted flat red.
    [Range(0f, 4f)] public float alpenglowStrength = 0.35f;
    [Tooltip("How much the faces looking at the sun stand out. At zero every direction glows " +
             "equally. It only engages while the sun is above the horizon: after it sets, what " +
             "lights the scene is not a point source but the whole sky painted red.")]
    [Range(0f, 1f)] public float alpenglowFacing = 0.7f;

    [Header("Shading")]
    [Tooltip("How much the exposure map dims the ambient light. Valley floors darken.")]
    [Range(0f, 1f)] public float cavityStrength = 0.55f;
}
