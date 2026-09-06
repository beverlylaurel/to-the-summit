// ROL: adim atildiginda kalkan kar tozunu doguruyor (spec 19.3).
// Caginan: karakterin adim olayi.

using UnityEngine;

/// THE COUNT DERIVES FROM A MEASUREMENT (spec 19.3): 8 + 40 * depth * (1 - density).
/// With a fixed count the same cloud would come out in shallow snow and in deep snow
/// alike, and how loose the snow is would not show.
[DisallowMultipleComponent]
public class SnowPuffEmitter : MonoBehaviour
{
    [SerializeField] SnowSampler sampler;
    [SerializeField] SnowBurstParticles particles;

    [Tooltip("Ayak konumu.")]
    [SerializeField] Transform footAnchor;

    GroundSurfaceContact surfaceContact;


    [Header("Tetik")]
    [Tooltip("The source of the step event. Left empty, this component does nothing " +
             "on its own; EmitFootstep() is called from outside.")]
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

    void OnStep(int foot) => EmitFootstep();

    [Header("Ayarlar")]
    [SerializeField] float upSpeed = 0.9f;
    [SerializeField] float spread = 0.7f;
    [SerializeField] Vector2 sizeRange = new(0.02f, 0.06f);
    [SerializeField] Vector2 lifetimeRange = new(0.4f, 0.9f);

    public int LastCount { get; private set; }

    /// Spec 19.3 birebir. Esikler: derinlik 6 cm ustu VE yogunluk 0.50 alti.
    /// Sikismis patikada toz kalkmiyor - kalkacak gevsek tane yok.
    public static int PuffCountFor(SnowSample sample)
    {
        if (!sample.Valid) return 0;
        if (sample.Depth <= 0.06f) return 0;
        if (sample.Density01 >= 0.50f) return 0;

        return Mathf.RoundToInt(8f + 40f * sample.Depth * (1f - sample.Density01));
    }

    /// Karakterin adim olayindan cagriliyor.
    public void EmitFootstep()
    {
        LastCount = 0;

        Vector3 p = footAnchor != null ? footAnchor.position : transform.position;

        if (surfaceContact == null || !surfaceContact.SupportsSnow) return;
        if (sampler == null || particles == null) return;
        if (!sampler.TrySampleSnow(p, out SnowSample sample)) return;

        int count = PuffCountFor(sample);
        LastCount = count;

        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = Mathf.Abs(dir.y);

            Vector3 velocity = Vector3.up * upSpeed + dir * spread;

            particles.Emit(p, velocity,
                           Random.Range(sizeRange.x, sizeRange.y),
                           Random.Range(lifetimeRange.x, lifetimeRange.y));
        }
    }
}
