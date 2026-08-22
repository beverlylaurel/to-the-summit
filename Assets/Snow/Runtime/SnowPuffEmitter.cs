// ROL: adim atildiginda kalkan kar tozunu doguruyor (spec 19.3).
// Caginan: karakterin adim olayi.

using UnityEngine;

/// SAYI OLCUMDEN TURUYOR (spec 19.3): 8 + 40 * derinlik * (1 - yogunluk).
/// Sabit bir sayi kullanilsaydi sig karda da derin karda da ayni bulut
/// cikardi ve karin ne kadar gevsek oldugu gorunmezdi.
[DisallowMultipleComponent]
public class SnowPuffEmitter : MonoBehaviour
{
    [SerializeField] SnowSampler sampler;
    [SerializeField] SnowBurstParticles particles;

    [Tooltip("Ayak konumu.")]
    [SerializeField] Transform footAnchor;

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
