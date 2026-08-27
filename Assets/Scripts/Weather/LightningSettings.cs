using UnityEngine;

/// The brightness, colour, envelope and visible bolt of a lightning strike.
///
/// In a separate file for the same reason as `ThunderSettings`: sitting on the component, the
/// scene copy overrides the code and Unity rewrites that copy to disk whenever it likes.
///
/// How far away the strike is is not here — `ThunderPlayer` picks that and carries it with the
/// event. The distances here only answer the question "how much counts as near"; they do not
/// interfere with the strike's own position.
[CreateAssetMenu(menuName = "To The Summit/Lightning", fileName = "LightningSettings")]
public class LightningSettings : ScriptableObject
{
    [Header("Proximity threshold")]
    [Tooltip("Closer than this counts as fully 'near' (metres).")]
    public float nearDistance = 800f;
    [Tooltip("Farther than this counts as fully 'distant' (metres).")]
    public float farDistance = 5000f;

    [Tooltip("The strike's tendency to fall where the player is looking. 0 is entirely random — " +
             "every direction of the sky is equal, but because the field of view is narrow you " +
             "miss most of the strikes. 1 is almost always ahead.")]
    [Range(0f, 1f)] public float forwardBias = 0.65f;

    [Header("Light")]
    [Tooltip("The light intensity at the reference distance. The real intensity fades with the " +
             "square of the distance: a strike twice as far lights a quarter as much. That is " +
             "why lightning bursting nearby dazzles. The noon sun is around 1.4.")]
    public float intensityAtReference = 9f;
    [Tooltip("The distance the intensity above is measured at (metres). It should be close to " +
             "the cloud layer's height above the player: because the strike is inside the layer, " +
             "the real distance cannot fall below that height even with the horizontal distance " +
             "at zero. Given a thousand metres, even a near strike stayed at half the sun.")]
    public float referenceDistance = 3000f;
    [Tooltip("Lightning is a cold, bluish light; it does not have the sun's warmth.")]
    public Color flashColor = new(0.80f, 0.87f, 1f);

    [Header("Sky and cloud")]
    [Tooltip("The glow the cloud mass takes on a distant strike.")]
    [Range(0f, 3f)] public float distantGlow = 0.4f;
    [Tooltip("The glow the cloud mass takes on a near strike.")]
    [Range(0f, 3f)] public float closeGlow = 1.6f;
    [Tooltip("The radius of the lit patch in the sea of cloud (metres). The glow halves this " +
             "far from the strike point.")]
    public float glowRadius = 2500f;

    [Header("Zarf")]
    [Tooltip("The time from zero to full brightness (seconds). Lightning opens instantly.")]
    public float riseSeconds = 0.015f;
    [Tooltip("The fade time constant of a near strike (seconds). Sharp and short — but not as " +
             "short as real lightning lasts. A flash dying in sixty milliseconds lasts six " +
             "frames and was being missed; in reality the reason it is noticed is that it is a " +
             "hundred thousand times brighter than its surroundings, whereas here only seven times.")]
    public float closeDecay = 0.18f;
    [Tooltip("The fade time constant of a distant strike (seconds). Because the light scatters " +
             "inside the cloud mass, from far away it appears as a longer, broader glow.")]
    public float distantDecay = 0.35f;
    [Tooltip("The interval between two return strokes (seconds).")]
    public Vector2 strokeGap = new(0.04f, 0.13f);

    [Header("Visible bolt")]
    [Tooltip("The farthest distance the bolt is drawn at (metres). Beyond it only the cloud glows.")]
    public float boltDistance = 7000f;
    [Tooltip("The distance the bolt appears at full brightness (metres). From here to " +
             "`boltDistance` it fades away.\n\n" +
             "A FADE INSTEAD OF A HARD CUT: there used to be a single limit and the bolt was " +
             "fully visible at 2499 m and invisible at 2501 m. In reality distant lightning is " +
             "visible, only thin and faint; the rain and air in between swallow the channel.")]
    public float boltFullDistance = 1800f;
    [Tooltip("How many pieces the channel is split into. Too few is angular, too many is a fine crinkle.")]
    [Range(4, 64)] public int boltSegments = 28;
    [Tooltip("The channel's wide oscillation, as a share of its own length. This is a walk: " +
             "the bends continue each other, they are not independent jumps. " +
             "A ratio rather than metres, because the forks are many times shorter than the main " +
             "channel: an absolute deviation doubles on them proportionally and the sharp break " +
             "approached the node spacing and turned into a saw.")]
    [Range(0f, 0.15f)] public float boltWaviness = 0.045f;
    [Tooltip("The share of sharp breaks riding on top of the wide oscillation. At zero the " +
             "channel descends as a soft arc and looks lifeless; at one only saw teeth are " +
             "left. A real channel has both scales at once.")]
    [Range(0f, 1f)] public float boltKink = 0.35f;
    [Tooltip("The channel's thickness (metres).")]
    public float boltWidth = 14f;
    // ---- BRANCHING: Reed & Wyvill 1994 ----
    //
    // The paper's single real physical observation: branches deviate from the main bolt by 16
    // degrees on average and the angles are NORMALLY distributed around that value. Using a fixed
    // angle (the old state, ~35 degrees) threw every fork in the same direction.
    //
    // The branching is RECURSIVE: a branch has branches. The old state was single-level and the
    // main channel looked like five sticks coming out of a trunk; a real discharge trees out,
    // thinning at every generation.
    [Tooltip("The branch's deviation angle from the main bolt — the mean (degrees). Reed & " +
             "Wyvill's observation is 16 degrees; the paper's only empirical constant.")]
    [Range(2f, 40f)] public float boltBranchAngle = 16f;
    [Tooltip("The spread of the deviation angle (degrees). The normal distribution's standard deviation.")]
    [Range(0f, 20f)] public float boltBranchSpread = 7f;
    [Tooltip("The deviation's cap (degrees). So the extreme values in the tail do not throw the " +
             "bolt back upward.")]
    [Range(10f, 80f)] public float boltBranchAngleMax = 50f;

    // THE EXPECTED NUMBER OF BRANCHES, NOT A PER-NODE PROBABILITY.
    //
    // The probability depended on the node count and did not scale: 27 candidate nodes × 0.2 = 5.4
    // branches from the main channel, each with 4.3 more → 23 branches in the second generation.
    // The tree pressed against the budget ceiling and looked like a root on screen (measured).
    //
    // Given an expected count, the branch count stays fixed even when `boltSegments` changes.
    [Tooltip("The EXPECTED number of branches born from the main channel. Independent of the node count.")]
    [Range(0f, 8f)] public float boltBranchCount = 2.2f;
    [Tooltip("The multiplier of the expected count at every generation. This is what keeps the tree from exploding.")]
    [Range(0.1f, 0.9f)] public float boltBranchCountDecay = 0.45f;
    [Tooltip("The branch's length relative to its parent. It is applied again at every " +
             "generation, so at 0.3 the second generation is a tenth of the parent — this is " +
             "what makes the tree terminate. At a high value the branches descend to the ground " +
             "along with the main channel and the bolt looks like a root; a real branch ends in the air.")]
    [Range(0.1f, 0.6f)] public float boltBranchLength = 0.3f;
    [Tooltip("The multiplier of the thickness at every generation.")]
    [Range(0.2f, 0.9f)] public float boltWidthDecay = 0.5f;
    [Tooltip("The multiplier of the sinuosity at every generation. In Reed & Wyvill a branch is " +
             "MORE sinuous than its parent: as its power falls the path is thrown about more.")]
    [Range(0.5f, 2f)] public float boltWavinessGrowth = 1.3f;

    [Tooltip("The most generations of branches. 0 = the main channel only.")]
    [Range(0, 5)] public int boltGenerations = 3;
    [Tooltip("The most lines that can be drawn at once. The tree's budget ceiling; " +
             "exceeded, the branching is cut off.")]
    [Range(1, 64)] public int boltMaxLines = 24;

    [Header("Contact point")]
    [Tooltip("The intensity of the point light where the bolt touches the ground. Unlike the " +
             "directional light, this one really is nearby, so its range can be kept narrow.")]
    public float groundIntensity = 600f;
    [Tooltip("That light's range (metres).")]
    public float groundRange = 700f;
}
