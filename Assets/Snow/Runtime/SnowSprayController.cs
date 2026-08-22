// ROL: kosarken kalkan kar puskurtmesini surer (spec 18.6).
// Caginan: sahne (karakterin ustunde).

using UnityEngine;

/// PUSKURTME MIKTARI UYDURULMUYOR, SIMULASYONDAN TURUYOR (spec 18.6)
/// [KAYNAK: Sumner, O'Brien & Hodgins, CGF 1999].
///
/// Birim zamanda yerinden edilen kar hacmi V = genislik x batma x hiz.
/// Ucunun ucu de elimizde: batma SnowSampler'dan, hiz hareket kaynagindan,
/// temas genisligi deformer proxy'sinden. Sabit bir oran kullanilsaydi
/// yavaslayinca da hizlanınca da ayni bulut cikardi.
///
/// MEVCUT HAREKET KODUNA SATIR EKLENMEDI (spec 18.6, 1.4). Hiz kaynagi
/// Inspector'dan atanan bir Transform'un konum farkindan olculuyor.
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

    public float LastRate { get; private set; }
    public float LastSpeed { get; private set; }

    void OnEnable()
    {
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

        if (sampler == null || particles == null) return;

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
