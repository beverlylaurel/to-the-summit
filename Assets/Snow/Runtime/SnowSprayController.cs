// ROL: kosarken kalkan kar puskurtmesini surer (spec 18.6).
// Caginan: sahne (karakterin ustunde).

using UnityEngine;

/// THE SPRAY AMOUNT IS NOT INVENTED, IT DERIVES FROM THE SIMULATION (spec 18.6)
/// [SOURCE: Sumner, O'Brien & Hodgins, CGF 1999].
///
/// The volume of snow displaced per unit time is V = width x sinking x speed.
/// All three are in hand: the sinking from SnowSampler, the speed from the motion
/// source, the contact width from the deformer proxy. With a fixed rate the same
/// cloud would come out whether you slowed down or sped up.
///
/// NO LINE WAS ADDED TO THE EXISTING MOTION CODE (spec 18.6, 1.4). The speed source
/// is measured from the position difference of a Transform assigned in the Inspector.
[DisallowMultipleComponent]
public class SnowSprayController : MonoBehaviour
{
    [Header("Bagimliliklar")]
    [SerializeField] SnowSampler sampler;
    [SerializeField] SnowBurstParticles particles;

    [Tooltip("Ayak konumu; ornekleme ve dogum burada.")]
    [SerializeField] Transform footAnchor;

    [Tooltip("Hizi olculecek nesne. Bos birakilirsa bu nesnenin kendisi.")]
    [SerializeField] Transform velocitySource;

    [Header("Ayarlar")]
    [Tooltip("Botun veya tekerlegin temas genisligi (m).")]
    [SerializeField] float contactWidth = 0.11f;

    [Tooltip("Metre kup basina parcacik. Spec 18.6 [KALIBRASYON].")]
    [SerializeField] float sprayParticlesPerM3 = 40000f;

    [SerializeField] float upSpeed = 1.4f;
    [SerializeField] Vector2 sizeRange = new(0.03f, 0.10f);
    [SerializeField] Vector2 lifetimeRange = new(0.5f, 1.1f);

    Vector3 prevPos;
    float carry;
    GroundSurfaceContact surfaceContact;

    public float LastRate { get; private set; }
    public float LastSpeed { get; private set; }

    void OnEnable()
    {
        surfaceContact = GroundSurfaceContact.Require(this);
        Transform source = velocitySource != null ? velocitySource : transform;
        prevPos = source.position;
        carry = 0f;
    }

    /// Spec 18.6 birebir. Esikler: hiz 2 m/s ustu, batma 5 cm ustu,
    /// gevseklik 0.5 ustu. Sikismis patikada puskurtme yok - firlayacak
    /// gevsek tane yok.
    public static float RateFor(SnowSample sample, float speed,
                                float contactWidth, float particlesPerM3)
    {
        if (!sample.Valid) return 0f;

        float loose = 1f - sample.Density01;

        if (speed <= 2f) return 0f;
        if (sample.SinkDepth <= 0.05f) return 0f;
        if (loose <= 0.5f) return 0f;

        float volumeRate = contactWidth * sample.SinkDepth * speed;
        return particlesPerM3 * volumeRate * loose;
    }

    void LateUpdate()
    {
        Transform source = velocitySource != null ? velocitySource : transform;

        Vector3 p = source.position;
        Vector3 delta = p - prevPos;
        prevPos = p;

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        LastSpeed = delta.magnitude / dt;
        LastRate = 0f;

        if (surfaceContact == null || !surfaceContact.SupportsSnow
            || sampler == null || particles == null)
        {
            carry = 0f;
            return;
        }

        Vector3 foot = footAnchor != null ? footAnchor.position : transform.position;

        if (!sampler.TrySampleSnow(foot, out SnowSample sample)) return;

        LastRate = RateFor(sample, LastSpeed, contactWidth, sprayParticlesPerM3);

        if (LastRate <= 0f) { carry = 0f; return; }

        // Kesirli parcacik tasiniyor: dusuk oranlarda da duzgun akiyor.
        carry += LastRate * dt;

        int count = Mathf.FloorToInt(carry);
        carry -= count;

        Vector3 moveDir = delta.sqrMagnitude > 1e-8f ? delta.normalized : transform.forward;

        for (int i = 0; i < count; i++)
        {
            // Firlatma hizi hareket hiziyla orantili: saltasyon splash
            // fizigiyle tutarli (firlayan tanenin hizi carpma hizinin bir
            // kesridir).
            Vector3 hemisphere = Random.insideUnitSphere;
            hemisphere.y = Mathf.Abs(hemisphere.y);

            Vector3 velocity = moveDir * (LastSpeed * 0.5f)
                             + Vector3.up * Random.Range(0.8f, 2.0f) * (upSpeed / 1.4f)
                             + hemisphere * 0.9f;

            particles.Emit(foot + moveDir * 0.2f, velocity,
                           Random.Range(sizeRange.x, sizeRange.y),
                           Random.Range(lifetimeRange.x, lifetimeRange.y));
        }
    }
}
