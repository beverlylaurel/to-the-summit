using UnityEngine;

/// The frequency and sound of thunder, and how far away the strike is.
///
/// The delay is not a setting here: it follows from the distance. Giving both separately would
/// mean two systems deciding the same physical quantity independently — a delay of one and a
/// half seconds means five hundred metres, and the flash cannot be anywhere else.
///
/// As long as they sat on the component as `[SerializeField]` there were three copies of each
/// value: the default in code, the serialized copy in the scene, and the one actually running.
/// The scene wins, and on top of that Unity rewrites the scene to disk from its own memory
/// whenever it likes — a fix made in code was silently reverted. Living in one file, there is no
/// second copy left to diverge.
///
/// The clips themselves are not here: they are content, not settings, and the scene setup binds them.
[CreateAssetMenu(menuName = "To The Summit/Thunder", fileName = "ThunderSettings")]
public class ThunderSettings : ScriptableObject
{
    [Header("Frequency")]
    [Tooltip("Shortest interval between two thunders in the heaviest rain (seconds).")]
    public float minInterval = 15f;
    [Tooltip("Longest interval between two thunders while the rain is weak (seconds).")]
    public float maxInterval = 110f;
    [Tooltip("Below this intensity thunder never plays.")]
    [Range(0f, 1f)] public float minPrecipitation = 0.2f;
    [Tooltip("Lowest volume level preserved.")]
    [Range(0f, 1f)] public float minVolume = 0.5f;

    [Header("Proximity")]
    [Tooltip("Ceiling on the probability of a near strike. Because the curve is a square root " +
             "it reaches a third of this value even just above the threshold.")]
    [Range(0f, 1f)] public float closeChanceAtPeak = 0.85f;
    [Tooltip("The rain intensity at which near strikes begin. Below it only distant, calm " +
             "thunder plays — so the quiet opening at the foot of the mountain is not broken.")]
    [Range(0f, 1f)] public float closeThreshold = 0.45f;

    [Header("Varyasyon")]
    [Range(0f, 0.5f)] public float volumeVariation = 0.25f;
    [Range(0f, 0.5f)] public float pitchVariation = 0.15f;
    [Range(0f, 1f)] public float panVariation = 0.6f;
    [Tooltip("Cutoff frequency range of distant thunder (Hz). The air swallows the high frequencies.")]
    public Vector2 distantCutoff = new(400f, 1200f);
    [Tooltip("Cutoff frequency range of near thunder (Hz).")]
    public Vector2 closeCutoff = new(3000f, 8000f);

    [Header("Mesafe")]
    [Tooltip("Distance range of a near strike (metres).")]
    public Vector2 closeDistance = new(200f, 1500f);
    [Tooltip("Distance range of a distant strike (metres). Because sound travels 340 m per second, " +
             "8 km means twenty-four seconds — and it really is like that.")]
    public Vector2 distantDistance = new(2500f, 8000f);
}
