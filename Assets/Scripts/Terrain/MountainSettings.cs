using UnityEngine;

/// Every generation parameter of the mountain. The bootstrap and the tuner window look at the same asset.
[CreateAssetMenu(fileName = "MountainSettings", menuName = "To The Summit/Mountain Settings")]
public class MountainSettings : ScriptableObject
{
    [Header("Boyut")]
    [Tooltip("Must be 2^n + 1: 513, 1025, 2049, 4097 (Unity's maximum).")]
    public int heightmapResolution = 4097;
    [Tooltip("Metres. One edge of the base.")]
    public float terrainSize = 8192f;
    [Tooltip("Metres. The summit's maximum height.")]
    public float terrainHeight = 2100f;
    public int seed = 1;

    [Header("Genel Form")]
    [Tooltip("The mountain foot's share of the map. Outside it is the flat starting terrain.")]
    [Range(0.1f, 0.48f)] public float mountainRadius = 0.38f;
    [Tooltip("The mountain's silhouette. X: distance from the centre (0 summit, 1 foot). Y: height.")]
    public AnimationCurve heightProfile;
    [Tooltip("The ground level around the mountain (0-1).")]
    [Range(0f, 0.2f)] public float baseHeight = 0.03f;
    [Tooltip("The base's deviation from a circle. 0 = a perfect circle, and the ring patterns stand out.")]
    [Range(0f, 0.6f)] public float radialDistortion = 0.3f;
    [Tooltip("Angular frequency of the base distortion. Small = a few wide lobes.")]
    [Range(0.5f, 8f)] public float radialFrequency = 2.5f;

    [Header("Secondary peaks")]
    [Tooltip("The number of shoulders and side peaks added around the main summit.")]
    [Range(0, 8)] public int secondaryPeaks = 3;
    [Tooltip("The side peaks' distance from the centre, as a share of the mountain radius.")]
    [Range(0.1f, 0.9f)] public float peakSpread = 0.45f;
    [Tooltip("The side peaks' height range, as a share of the main summit.")]
    public Vector2 peakHeightRange = new(0.45f, 0.75f);
    [Tooltip("The side peaks' width range, as a share of the mountain radius.")]
    public Vector2 peakRadiusRange = new(0.25f, 0.45f);

    [Header("Ridges (ridged noise)")]
    [Tooltip("The octave count is not a setting: as many as the resolution can carry are used " +
             "automatically. More than that means aliasing, i.e. specks and pits.")]
    [Range(0.5f, 8f)] public float baseFrequency = 2.5f;
    [Range(1.5f, 3f)] public float lacunarity = 2.1f;
    [Range(0.2f, 0.8f)] public float gain = 0.42f;
    [Tooltip("0 = a smooth cone, 1 = completely broken up by ridges.")]
    [Range(0f, 1f)] public float ridgeInfluence = 0.65f;
    [Tooltip("The multiplier of the ridge effect at the foot. Low = a soft foot and a sharp summit.")]
    [Range(0f, 1f)] public float ridgeFootDamping = 0.35f;
    [Tooltip("The sharpness of the ridges. 1 = rounded tops, 3 = a knife edge.")]
    [Range(0.5f, 3f)] public float ridgeSharpness = 2f;

    [Header("Domain Warp (organik bozma)")]
    [Range(0f, 0.6f)] public float warpStrength = 0.35f;
    [Range(0.5f, 8f)] public float warpFrequency = 2.5f;
    [Tooltip("Second-stage warping. It adds a fine fold on top of the large-scale folds.")]
    [Range(0f, 0.3f)] public float warpDetailStrength = 0.08f;
    [Range(2f, 20f)] public float warpDetailFrequency = 9f;

    [Header("Rock bands / benches")]
    [Tooltip("Wide benches.")]
    [Range(0f, 1f)] public float coarseTerraceStrength = 0.4f;
    [Range(2, 40)] public int coarseTerraceBands = 8;
    [Tooltip("Mid-scale notches.")]
    [Range(0f, 1f)] public float fineTerraceStrength = 0.22f;
    [Range(4, 120)] public int fineTerraceBands = 40;
    [Tooltip("Large = steeper bands and flatter benches.")]
    [Range(1f, 4.5f)] public float terraceSharpness = 3f;
    [Tooltip("How far the band elevations shift from place to place. 0 = the same elevation everywhere (a ring pattern).")]
    [Range(0f, 1f)] public float terraceOffsetAmount = 0.8f;
    [Range(0.5f, 8f)] public float terraceOffsetFrequency = 2.5f;
    [Tooltip("How the terrace strength varies by place. 1 = some slopes have no steps at all.")]
    [Range(0f, 1f)] public float terraceVariation = 0.6f;
    [Range(0.5f, 8f)] public float terraceVariationFrequency = 3.5f;

    [Header("Tip filing")]
    [Tooltip("How much the sharp tips are softened each round. 0 = off. Proportional: " +
             "a large tooth comes down a lot and a small one a little, so the irregularity is " +
             "kept. A thresholded file was tried and was wrong — it shaved every tooth to just " +
             "below the threshold and left a field of equal-sized mini pyramids; uniformity reads as a pattern.")]
    [Range(0f, 1f)] public float crestSoftening = 0.4f;

    [Header("Termal Erozyon")]
    [Tooltip("0 = off. It turns sharp fractures into scree slopes without breaking the large form.")]
    [Range(0, 40)] public int erosionIterations = 12;
    [Tooltip("The steepest angle the material can hold at. Slopes above it flow downhill.")]
    [Range(30f, 70f)] public float talusAngle = 48f;
    [Tooltip("How much of the overflowing material moves each iteration.")]
    [Range(0.1f, 0.9f)] public float erosionRate = 0.5f;

    [Header("Zirve")]
    [Tooltip("The height fraction at which the summit flattens. 1 = no flattening.")]
    [Range(0.5f, 1f)] public float summitPlateauStart = 1f;
    [Tooltip("The flatness of the summit plateau. 0 = sharp, 1 = completely flat.")]
    [Range(0f, 1f)] public float summitFlatness = 0.5f;

    /// To tell whether the parameters changed. The bootstrap uses it to skip a needless regeneration.
    /// The version of the generation recipe. The recipe is not only the settings: the code itself
    /// is part of it. When the filing window was widened in a constant the signature did not
    /// change and the mountain was not regenerated — the fix was on disk and the old mountain on
    /// screen. This number is incremented every time the generation code changes.
    [Header("Plain (the mountain's foreground)")]
    [Tooltip("How far the plain falls away from the mountain (metres). The mountain's foreground " +
             "is not flat: streams carry material downhill and leave a slight fan gradient " +
             "running outward from the foot. Zero = a dead flat table.")]
    [Range(0f, 200f)] public float forelandFanDrop = 60f;

    [Tooltip("The height of the moraine ridges (metres). The arcs a glacier leaves behind as it " +
             "retreats; they line up concentrically around the mountain.")]
    [Range(0f, 40f)] public float moraineHeight = 20f;

    [Tooltip("The distance between two moraine ridges (metres).")]
    [Range(100f, 1500f)] public float moraineSpacing = 420f;

    [Tooltip("The depth of the stream beds (metres). Gullies running outward from the mountain; " +
             "these are the places the road wants a bridge or a ford.")]
    [Range(0f, 30f)] public float channelDepth = 14f;

    [Tooltip("The height of the hummocks (metres). Bumps you walk past; "
             + "not an obstacle to be climbed, the character of the ground. They are spread over "
             + "three scales (90/42/21 m) and applied patchily: one place is rough, the next is flat.")]
    [Range(0f, 15f)] public float hummockHeight = 8f;

    const int RecipeVersion = 14;

    public string BuildSignature()
    {
        return string.Join("|",
            RecipeVersion,
            heightmapResolution, terrainSize, terrainHeight, seed,
            mountainRadius, baseHeight, radialDistortion, radialFrequency,
            secondaryPeaks, peakSpread, peakHeightRange, peakRadiusRange,
            baseFrequency, lacunarity, gain,
            ridgeInfluence, ridgeFootDamping, ridgeSharpness,
            warpStrength, warpFrequency, warpDetailStrength, warpDetailFrequency,
            coarseTerraceStrength, coarseTerraceBands,
            fineTerraceStrength, fineTerraceBands,
            terraceSharpness, terraceOffsetAmount, terraceOffsetFrequency,
            terraceVariation, terraceVariationFrequency,
            erosionIterations, talusAngle, erosionRate, crestSoftening,
            summitPlateauStart, summitFlatness,
            forelandFanDrop, moraineHeight, moraineSpacing, channelDepth, hummockHeight,
            CurveSignature());
    }

    string CurveSignature()
    {
        var keys = heightProfile.keys;
        var parts = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            parts[i] = $"{keys[i].time:F3},{keys[i].value:F3},{keys[i].outTangent:F2}";

        return string.Join(";", parts);
    }

    /// Randomizes the form parameters within sensible ranges. The size, height and resolution
    /// are preserved — those are design decisions, not a matter of variation.
    public void Randomize(System.Random rng)
    {
        seed = rng.Next(1, 100000);

        mountainRadius = Range(rng, 0.30f, 0.45f);
        radialDistortion = Range(rng, 0.15f, 0.45f);
        radialFrequency = Range(rng, 1.5f, 4.5f);

        secondaryPeaks = rng.Next(0, 6);
        peakSpread = Range(rng, 0.30f, 0.65f);
        float peakLow = Range(rng, 0.35f, 0.60f);
        peakHeightRange = new Vector2(peakLow, peakLow + Range(rng, 0.10f, 0.30f));
        float radiusLow = Range(rng, 0.18f, 0.35f);
        peakRadiusRange = new Vector2(radiusLow, radiusLow + Range(rng, 0.05f, 0.20f));

        baseFrequency = Range(rng, 1.8f, 3.5f);
        lacunarity = Range(rng, 1.9f, 2.3f);
        gain = Range(rng, 0.36f, 0.50f);
        ridgeInfluence = Range(rng, 0.45f, 0.80f);
        ridgeFootDamping = Range(rng, 0.20f, 0.60f);
        ridgeSharpness = Range(rng, 1.2f, 2.6f);

        warpStrength = Range(rng, 0.20f, 0.50f);
        warpFrequency = Range(rng, 1.5f, 4f);
        warpDetailStrength = Range(rng, 0.03f, 0.14f);
        warpDetailFrequency = Range(rng, 6f, 14f);

        coarseTerraceStrength = Range(rng, 0.20f, 0.55f);
        coarseTerraceBands = rng.Next(5, 13);
        fineTerraceStrength = Range(rng, 0.10f, 0.35f);
        fineTerraceBands = rng.Next(20, 46);
        terraceSharpness = Range(rng, 2f, 3.5f);
        terraceOffsetAmount = Range(rng, 0.5f, 1f);
        terraceOffsetFrequency = Range(rng, 1.5f, 4f);
        terraceVariation = Range(rng, 0.40f, 0.85f);
        terraceVariationFrequency = Range(rng, 2f, 5f);

        erosionIterations = rng.Next(6, 25);
        talusAngle = Range(rng, 38f, 60f);
        erosionRate = Range(rng, 0.35f, 0.7f);

        // Most mountains have a sharp summit; a plateau should come up only occasionally
        summitPlateauStart = rng.NextDouble() < 0.3 ? Range(rng, 0.80f, 0.95f) : 1f;
        summitFlatness = Range(rng, 0.2f, 0.7f);
    }

    static float Range(System.Random rng, float min, float max)
        => min + (float)rng.NextDouble() * (max - min);

    /// The default silhouette: splayed at the foot, steepening upward, sharp at the summit.
    /// The tangents are computed and supplied; so the curve does not bulge when a point is
    /// dragged, the tuner's "Fix the curve" button puts the tangents back into Auto mode.
    public static AnimationCurve DefaultProfile()
    {
        var curve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.1f, 0.78f),
            new Keyframe(0.3f, 0.48f),
            new Keyframe(0.55f, 0.24f),
            new Keyframe(0.8f, 0.08f),
            new Keyframe(1f, 0f));

        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);

        return curve;
    }

    void Reset() => heightProfile = DefaultProfile();
}
