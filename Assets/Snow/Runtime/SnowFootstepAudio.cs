// ROLE: chooses which snow sound a step plays and plays it (spec §19.1).
// CALLED BY: the character's step event.

using UnityEngine;

/// The sound type of a step on snow (spec §19.1).
public enum SnowFootstepSurface
{
    /// No snow — the existing ground sound should play, the snow system does not interfere.
    None,
    Packed,
    Shallow,
    Powder,
    Deep,

    /// A solid crust: it is walked on with almost no sinking (spec §18.3).
    Crust,
}

/// THE PROJECT'S FIRST FOOTSTEP SYSTEM. Spec §19.1 says "it is added to the existing
/// footstep system as a new surface type", but the project HAS no footstep system.
/// So there are only SNOW sounds here; with no snow it returns `None` and the decision
/// is left to the caller.
///
/// With no clip assigned nothing plays — staying silent is better than playing the
/// wrong sound.
[DisallowMultipleComponent]
public class SnowFootstepAudio : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] SnowSampler sampler;
    [SerializeField] AudioSource source;

    [Tooltip("Ayak konumu.")]
    [SerializeField] Transform footAnchor;

    GroundSurfaceContact surfaceContact;


    [Header("Tetik")]
    [Tooltip("The source of the step event. Left empty, this component does nothing " +
             "on its own; PlayFootstep() is called from outside.")]
    [SerializeField] SnowStepRhythm rhythm;

    // THE STEP IS SUBSCRIBED TO AN EVENT, NOT A CALL. The rhythm component does not know
    // this class; the walking system can change without this changing.
    void OnEnable()
    {
        surfaceContact = GroundSurfaceContact.Require(this);
        if (rhythm != null) rhythm.Stepped += OnStep;
    }

    void OnDisable()
    {
        if (rhythm != null) rhythm.Stepped -= OnStep;
    }

    void OnStep(int foot) => PlayFootstep();

    [Header("Klipler")]
    [Tooltip("THE CLIPS WILL BE SUPPLIED LATER. An empty array stays silent; the surface " +
             "selection and the triggering work without clips, only no sound comes out.")]
    [SerializeField] AudioClip[] packed;
    [SerializeField] AudioClip[] shallow;
    [SerializeField] AudioClip[] powder;
    [SerializeField] AudioClip[] deep;
    [SerializeField] AudioClip[] crust;

    [Tooltip("Wet variants. If empty the dry clips play.")]
    [SerializeField] AudioClip[] packedWet;
    [SerializeField] AudioClip[] shallowWet;
    [SerializeField] AudioClip[] powderWet;
    [SerializeField] AudioClip[] deepWet;

    /// The surface chosen on the last step — for diagnostics.
    public SnowFootstepSurface LastSurface { get; private set; }

    /// THE SURFACE SELECTION IS SPEC §19.1's TABLE EXACTLY. The order matters: the shallow
    /// and compacted checks come BEFORE the powder check, otherwise thin but loose snow
    /// counts as "powder" and the deep snow sound plays.
    public static SnowFootstepSurface SelectSurface(SnowSample sample)
    {
        if (!sample.Valid) return SnowFootstepSurface.None;

        if (sample.Depth < 0.02f) return SnowFootstepSurface.None;

        // THE CRUST FIRST. On a crusted surface, whatever the depth of the snow beneath,
        // the sound heard is the crust's (spec §18.3).
        if (sample.Crust > SnowConstants.CrustSolid) return SnowFootstepSurface.Crust;

        if (sample.Depth < 0.08f && sample.Density01 > 0.55f) return SnowFootstepSurface.Packed;
        if (sample.Depth < 0.08f) return SnowFootstepSurface.Shallow;
        if (sample.Density01 < 0.30f) return SnowFootstepSurface.Powder;

        return SnowFootstepSurface.Deep;
    }

    /// The wet variant threshold (spec §19.1).
    public static bool IsWet(SnowSample sample) => sample.Wetness > 0.55f;

    /// Called from the character's step event. If it returns `false` there is no snow sound;
    /// the caller should play its own ground sound.
    public bool PlayFootstep()
    {
        Vector3 p = footAnchor != null ? footAnchor.position : transform.position;

        if (surfaceContact == null || !surfaceContact.SupportsSnow)
        {
            LastSurface = SnowFootstepSurface.None;
            return false;
        }

        if (sampler == null || !sampler.TrySampleSnow(p, out SnowSample sample))
        {
            LastSurface = SnowFootstepSurface.None;
            return false;
        }

        LastSurface = SelectSurface(sample);
        if (LastSurface == SnowFootstepSurface.None) return false;

        AudioClip[] bank = ClipsFor(LastSurface, IsWet(sample));
        if (bank == null || bank.Length == 0 || source == null) return false;

        source.PlayOneShot(bank[Random.Range(0, bank.Length)]);
        return true;
    }

    AudioClip[] ClipsFor(SnowFootstepSurface surface, bool wet)
    {
        AudioClip[] wetBank = surface switch
        {
            SnowFootstepSurface.Packed => packedWet,
            SnowFootstepSurface.Shallow => shallowWet,
            SnowFootstepSurface.Powder => powderWet,
            SnowFootstepSurface.Deep => deepWet,
            _ => null,
        };

        if (wet && wetBank != null && wetBank.Length > 0) return wetBank;

        return surface switch
        {
            SnowFootstepSurface.Packed => packed,
            SnowFootstepSurface.Shallow => shallow,
            SnowFootstepSurface.Powder => powder,
            SnowFootstepSurface.Deep => deep,
            SnowFootstepSurface.Crust => crust,
            _ => null,
        };
    }
}
