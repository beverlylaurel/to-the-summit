// ROL: yürüyüşü ayak temaslarına çevirir. İki bot deformer'ı üretir ve adım adım
// yere basar.
// Çağıran: kimse — kendi Update'inde çalışır.

using UnityEngine;

[DisallowMultipleComponent]
public class SnowFootstepDriver : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowDeformerRegistry registry;

    [Tooltip("Yürüyen nesne. Normalde oyuncu.")]
    [SerializeField] Transform followTarget;

    [Header("Yürüyüş")]
    [Tooltip("İki ayak basışı arasındaki mesafe, metre.")]
    [SerializeField] float strideLength = 0.75f;

    [Tooltip("Ayakların gövde ekseninden yanal uzaklığı, metre.")]
    [SerializeField] float footLateral = 0.11f;

    [Tooltip("Basılan ayağın gövdenin ne kadar önüne düştüğü, metre.")]
    [SerializeField] float footLead = 0.18f;

    [Tooltip("Bir ayağın yerde kaldığı süre, saniye.")]
    [SerializeField] float contactTime = 0.32f;

    [Header("Temas")]
    [SerializeField] Vector2 bootSize = new Vector2(0.30f, 0.11f);

    [Tooltip("Tek ayağa binen kütle, kg. 800 N / 9.81 = 81.5 kg (§5.3).")]
    [SerializeField] float footLoadKg = 81.5f;

    [Tooltip("Bacak boyu sınırı: bu derinlikten fazla batılamaz.")]
    [SerializeField] float maxSink = 0.45f;

    [Header("Oyun tarafı (Faz 9)")]
    [Tooltip("Ayak sesi. Boş bırakılabilir.")]
    [SerializeField] SnowFootstepAudio footstepAudio;

    [Tooltip("Toz bulutu için kar durumu. Boş bırakılabilir.")]
    [SerializeField] SnowSampler sampler;

    [Tooltip("Toz bulutu parçacık malzemesi. Boşsa bulut çıkmıyor.")]
    [SerializeField] Material puffMaterial;

    [Header("Zemin")]
    [SerializeField] LayerMask groundMask = ~0;

    [Tooltip("Ayağın altına bakılan mesafe, metre. Bundan uzaksa havadayız.")]
    [SerializeField] float groundProbeDistance = 1.4f;

    SnowDeformer[] feet;
    float[] contactTimers;

    /// Kalkış toz bulutu için: ayağın bastığı yer ve o anki hareket yönü.
    Vector3[] footPositions;
    Vector2[] footVelocities;
    bool[] footLifted;

    int nextFoot;

    // ASSUMPTION: §12.3 toz bulutunu VFX_SnowPuff.vfx'e veriyor ama VFX Graph paketi
    // projede kurulu değil ve .vfx metin olarak üretilemez. Yerine Unity'nin kendi
    // parçacık sistemi çalışma zamanında kuruluyor; sayı ve tetikleme kuralı §12.3'ten
    // birebir.
    ParticleSystem puff;
    ParticleSystem.EmitParams puffParams;

    Vector3 lastPosition;
    float travelled;
    bool hasLastPosition;

    /// HAVADAYKEN İZ YOK. Zıplayarak ilerlerken arkada iz kalması gerçek dışı ve
    /// oyunda hemen fark ediliyor.
    public bool Grounded { get; private set; }

    void OnEnable()
    {
        if (registry == null)
            throw new System.InvalidOperationException("SnowFootstepDriver: kayıt defteri atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (followTarget == null)
            throw new System.InvalidOperationException("SnowFootstepDriver: takip hedefi atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        feet = new SnowDeformer[2];
        contactTimers = new float[2];
        footPositions = new Vector3[2];
        footVelocities = new Vector2[2];
        footLifted = new bool[2];

        CreatePuff();

        feet[0] = CreateFoot(SnowDeformerShape.BootLeft, "Snow Foot L");
        feet[1] = CreateFoot(SnowDeformerShape.BootRight, "Snow Foot R");

        hasLastPosition = false;
        travelled = 0f;
        nextFoot = 0;
    }

    void OnDisable()
    {
        if (feet == null) return;

        for (int i = 0; i < feet.Length; i++)
            if (feet[i] != null) Destroy(feet[i].gameObject);

        feet = null;
        contactTimers = null;

        if (puff != null) Destroy(puff.gameObject);
        puff = null;
    }

    /// Toz bulutu sistemi. Doğum kapalı; her patlama elle tetikleniyor.
    void CreatePuff()
    {
        if (puffMaterial == null) return;

        var go = new GameObject("Snow Puff") { hideFlags = HideFlags.HideAndDontSave };
        puff = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = puff.main;
        main.startLifetime = 0.85f;
        main.startSpeed = 0f;
        main.startSize = 0.07f;
        main.gravityModifier = 0.12f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.maxParticles = 512;

        ParticleSystem.EmissionModule emission = puff.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = puff.shape;
        shape.enabled = false;

        // Toz havada asılı kalıyor: sürtünme yüksek, hız hızla sönüyor.
        ParticleSystem.LimitVelocityOverLifetimeModule limit = puff.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.35f;

        ParticleSystem.ColorOverLifetimeModule color = puff.colorOverLifetime;
        color.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.65f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = puffMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    SnowDeformer CreateFoot(SnowDeformerShape shape, string footName)
    {
        // KAPALI YARATILIYOR: AddComponent OnEnable'ı hemen çağırıyor ve kayıt defteri
        // henüz bağlanmamış olurdu.
        var go = new GameObject(footName) { hideFlags = HideFlags.HideAndDontSave };
        go.SetActive(false);

        var deformer = go.AddComponent<SnowDeformer>();
        deformer.Bind(registry, shape, bootSize, footLoadKg, maxSink);

        go.SetActive(true);
        return deformer;
    }

    void Update()
    {
        Vector3 position = followTarget.position;

        if (!hasLastPosition)
        {
            lastPosition = position;
            hasLastPosition = true;
            return;
        }

        Vector3 delta = position - lastPosition;
        lastPosition = position;

        Vector2 horizontal = new Vector2(delta.x, delta.z);
        float step = horizontal.magnitude;

        Vector2 velocity = Time.deltaTime > 0f ? horizontal / Time.deltaTime : Vector2.zero;

        Grounded = Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down,
                                   out RaycastHit hit, groundProbeDistance,
                                   groundMask, QueryTriggerInteraction.Ignore);

        travelled += step;

        if (Grounded && travelled >= strideLength && step > 1e-4f)
        {
            travelled -= strideLength;
            PlantFoot(position, horizontal / step, velocity, hit.point.y);
        }

        for (int i = 0; i < feet.Length; i++)
            UpdateFoot(i);
    }

    /// §12.3: derinlik 6 cm'den fazla VE kar gevşekse toz kalkıyor. Sıkışmış
    /// patikada hiçbir şey olmuyor — ayrı bir kural değil, aynı eşiğin sonucu.
    void EmitPuff(Vector3 position, Vector2 velocity, float strength)
    {
        if (puff == null || sampler == null) return;
        if (!sampler.TrySampleSnow(position, out SnowSample sample)) return;

        if (sample.depth <= 0.06f || sample.density01 >= 0.50f) return;

        int count = Mathf.RoundToInt((8f + 40f * sample.depth * (1f - sample.density01)) * strength);
        if (count <= 0) return;

        puffParams = new ParticleSystem.EmitParams
        {
            position = position + Vector3.up * 0.03f,
            applyShapeToPosition = false,
        };

        for (int i = 0; i < count; i++)
        {
            Vector2 spread = Random.insideUnitCircle * 0.10f;

            // Kalkış bulutu hareket yönünde savruluyor.
            var drift = new Vector3(velocity.x, 0f, velocity.y) * (0.25f * strength);

            puffParams.velocity = drift
                                + new Vector3(spread.x, Random.Range(0.35f, 0.95f) * strength, spread.y);
            puffParams.position = position + new Vector3(spread.x, 0.03f, spread.y);

            puff.Emit(puffParams, 1);
        }
    }

    void PlantFoot(Vector3 bodyPosition, Vector2 forward, Vector2 velocity, float groundY)
    {
        SnowDeformer foot = feet[nextFoot];

        // Sağ ayak sağa, sol ayak sola. Yön vektörünün dikeyi yanal ekseni veriyor.
        float side = nextFoot == 0 ? -1f : 1f;
        Vector2 lateral = new Vector2(-forward.y, forward.x) * (footLateral * side);
        Vector2 lead = forward * footLead;

        Vector2 footXZ = new Vector2(bodyPosition.x, bodyPosition.z) + lateral + lead;

        foot.transform.SetPositionAndRotation(
            new Vector3(footXZ.x, groundY, footXZ.y),
            Quaternion.Euler(0f, Mathf.Atan2(forward.x, forward.y) * Mathf.Rad2Deg, 0f));

        foot.Velocity = velocity;
        foot.Strength = 1f;

        contactTimers[nextFoot] = contactTime;
        footPositions[nextFoot] = foot.transform.position;
        footVelocities[nextFoot] = velocity;
        footLifted[nextFoot] = false;

        if (footstepAudio != null) footstepAudio.PlayFootstep(foot.transform.position);

        // İNİŞ BULUTU. Kalkış bulutundan zayıf: basınca kar çöküyor, kalkınca
        // savruluyor (§12.3).
        EmitPuff(foot.transform.position, velocity, 0.6f);

        nextFoot = 1 - nextFoot;
    }

    void UpdateFoot(int index)
    {
        if (contactTimers[index] <= 0f)
        {
            feet[index].Strength = 0f;

            // KALKIŞ BULUTU: temas bittiği anda, bir kez.
            if (!footLifted[index])
            {
                footLifted[index] = true;
                EmitPuff(footPositions[index], footVelocities[index], 1f);
            }

            return;
        }

        contactTimers[index] -= Time.deltaTime;

        // Kalkışta yumuşama: temas bitişine doğru yük azalıyor, iz kenarı sertlemiyor.
        feet[index].Strength = Mathf.Clamp01(contactTimers[index] / Mathf.Max(contactTime, 1e-4f));
    }
}
