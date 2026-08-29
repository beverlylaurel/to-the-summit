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

    [Header("Rock")]
    public Color rockPrimary = new(0.13f, 0.14f, 0.16f);
    [Tooltip("The secondary rock that shows in the geological bands.")]
    public Color rockSecondary = new(0.27f, 0.26f, 0.24f);
    [Tooltip("The scale of the surface grain. A large value means fine grain.")]
    public float grainScale = 0.08f;
    [Range(0f, 1f)] public float grainStrength = 0.35f;
    [Tooltip("The rock's gloss. Wet rock goes above this.")]
    [Range(0f, 1f)] public float rockSmoothness = 0.12f;

    [Header("Geological bands")]
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

    /// SAND — PART OF THE SHORE, NOT ALL OF IT.
    ///
    /// The band is tied to `_SeaLevelY`, the still-water level `SeaManager` publishes: a beach
    /// belongs to the sea's level, not to the run-up. The wet band (`_SeaWetLevelY`) breathes
    /// with every wave and a beach does not move that fast.
    ///
    /// Three conditions have to hold at once, and that is what makes only PART of the coast
    /// sandy: the elevation has to be inside the band, the slope has to be gentle enough for the
    /// sand to hold, and the patch field has to be open there. Where the shore is steep it stays
    /// rock — a headland; where it is gentle and the patch is open, it is a bay of sand.
    [Header("Sand — the shore")]
    [Tooltip("Master switch. Zero leaves the whole shore as rock.")]
    [Range(0f, 1f)] public float sandAmount = 1f;
    [Tooltip("Sand albedo. Ground054 (ambientCG, CC0).")]
    public Texture2D sandAlbedo;
    [Tooltip("Sand normal, OpenGL convention (+Y up).")]
    public Texture2D sandNormal;
    [Tooltip("Sand roughness. Linear, the R channel is read.")]
    public Texture2D sandRoughness;
    [Tooltip("Sand ambient occlusion. Linear, the R channel is read.")]
    public Texture2D sandAO;
    [Tooltip("The real size of one tile (metres). The texture is a beach sand set; the dimples " +
             "on it are ten to fifteen centimetres, so a two metre tile puts them at their own size.")]
    public float sandTexScale = 2f;
    [Tooltip("Tint multiplied over the albedo. White = the texture's own colour.")]
    public Color sandTint = Color.white;
    /// THE BAND IS IN METRES OF ELEVATION, BUT WHAT IS SEEN IS ITS WIDTH ON THE GROUND.
    /// Measured on this coast: the mean slope inside the shore band is 2.14° and the steepest
    /// 5.13°, so one metre of elevation is about 27 metres of ground. A 9 m band came out
    /// 293-720 m wide — a sand plain, not a beach. At 1.6 m the dry strip is about 43 m, which
    /// is the width of a real beach.
    ///
    /// This number is TIED TO THE SHORE'S GRADIENT, not to the mountain's height: if the coast
    /// is recarved steeper or gentler it has to be measured again (`SCALE.md`).
    [Tooltip("How far above sea level the sand reaches (metres). About 43 m of ground on this " +
             "coast; the storm berm of a real beach is that order.")]
    public float sandBandAbove = 1.6f;
    [Tooltip("How far below sea level the sand reaches (metres). The shallow bottom is seen " +
             "through the water, and a rock floor right at the waterline reads wrong.")]
    public float sandBandBelow = 1.2f;
    [Tooltip("The fade thickness at both ends of the band (metres). Zero gives a drawn line.")]
    public float sandFade = 0.6f;
    /// SIX DEGREES, NOT THE ANGLE OF REPOSE. Sand holds up to about 34°, but nothing on this
    /// shore is anywhere near that: the steepest sample in the band is 5.13°. A limit at the
    /// angle of repose would never fire and the slope would stop being a condition at all.
    /// At 6° the flatter stretches take full sand and the steeper ones lose it — the patchiness
    /// then comes from the terrain itself, not only from the patch field.
    [Tooltip("The slope at which sand starts to be lost (degrees). It fades out over ±3° " +
             "around this value.")]
    [Range(2f, 45f)] public float sandSlopeLimit = 6f;
    [Tooltip("The length of a sandy bay along the coast (metres). Nine hundred metres means " +
             "walking the shore alternates between two or three bays and headlands.")]
    public float sandPatchScale = 900f;
    [Tooltip("The share of the eligible shore that is sandy. 1 = the whole band is sand, " +
             "0 = none of it.")]
    [Range(0f, 1f)] public float sandCoverage = 0.55f;
    [Tooltip("The strength of the sand normal. The procedural rock relief is faded out by the " +
             "same mask, so the two do not add up.")]
    [Range(0f, 2f)] public float sandNormalStrength = 1f;

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
