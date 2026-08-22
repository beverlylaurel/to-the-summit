// ROL: kar tanelerinin ve yer savrulmasının tamponlarını yönetir, simülasyonu
// kuyruğa yazar, çizimi kaydeder.
// Çağıran: sahne (SnowManager'ın yanında); simülasyon SnowManager.Dispatch'ten.

using UnityEngine;
using UnityEngine.Rendering;

/// VFX GRAPH YERİNE COMPUTE — GEREKÇESİ `DECISIONS.md`'de.
///
/// Spec §17.1 `VFX_Snowfall.vfx` istiyor. VFX Graph varlığı bir düğüm grafiği
/// ve metinden yazılamıyor; bu iş akışında üretilebilir tek karşılığı GPU'da
/// simüle edilen bir tane havuzu. Spec'in saydığı DAVRANIŞLARIN hepsi
/// uygulandı (snap'li doğum kutusu, ömür, rüzgâr hızı, türbülans, salınım,
/// asgari ekran boyu, sis fade'i, örtü ve zemin kesme, atlas, uzatma).
///
/// KAR YOKKEN MALİYET SIFIR (spec §15.2): yağış yoksa ne dispatch var ne çizim.
[DisallowMultipleComponent]
public class SnowfallRenderer : MonoBehaviour
{
    /// Tane başına 12 float (SnowfallSim.compute'taki `SnowFlake`).
    const int FlakeStride = 12 * sizeof(float);

    const int ThreadGroupSize = 64;

    /// Spec §17.1: Sistem A kapasitesi 40 000, Sistem B daha küçük.
    const int FlakeCapacity = 40000;
    const int DriftCapacity = 8000;

    /// Doğum kutusu (spec §17.1). Kameranın 11 m üstünde, rüzgâr yönünde 3 m.
    static readonly Vector3 SpawnBox = new(40f, 26f, 40f);
    const float SpawnUp = 11f;
    const float SpawnWindLead = 3f;

    /// Doğum kutusunun oturduğu ızgara. Snap yoksa kamera hareketinde doğum
    /// deseni yürüyor.
    const float SpawnSnap = 1f;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;
    [SerializeField] ComputeShader snowfallCompute;
    [SerializeField] Material flakeMaterial;
    [SerializeField] Material driftMaterial;

    [Tooltip("Doğum kutusunun merkezinde duracak hedef — genelde kamera.")]
    [SerializeField] Transform followTarget;

    [Tooltip("Yağış ve rüzgâr kaynağı.")]
    [SerializeField] MonoBehaviour environmentSource;

    [Header("Ayarlar")]
    [Tooltip("Tanenin taban boyu (m). Rastgele 0.6–1.7 katıyla çarpılıyor.")]
    [SerializeField] float flakeBaseSize = 0.018f;

    [Tooltip("Yer savrulmasının azami doğum oranı katsayısı.")]
    [SerializeField, Range(0f, 1f)] float spindriftRate = 1f;

    ISnowEnvironmentSource env;

    GraphicsBuffer flakes;
    GraphicsBuffer drift;

    int flakeKernel = -1;
    int driftKernel = -1;

    MaterialPropertyBlock flakeBlock;
    MaterialPropertyBlock driftBlock;

    Vector3 spawnCenter;
    Vector3 driftOrigin;

    int aliveFlakes;
    int aliveDrift;

    public int AliveFlakes => aliveFlakes;
    public int AliveDrift => aliveDrift;

    /// Simülasyon çalışacak mı. Kar yağmıyorsa ve savrulma yoksa hiçbir şey
    /// yapılmıyor.
    public bool HasWork => aliveFlakes > 0 || aliveDrift > 0;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SnowfallRenderer)}: {nameof(settings)} atanmadı.");
        if (snowfallCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowfallRenderer)}: {nameof(snowfallCompute)} atanmadı.");
        if (flakeMaterial == null || driftMaterial == null)
            throw new System.InvalidOperationException($"{nameof(SnowfallRenderer)}: materyaller atanmadı.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowfallRenderer)}: takip hedefi atanmadı.");

        env = environmentSource as ISnowEnvironmentSource;

        if (env == null)
        {
            Debug.LogError($"{nameof(SnowfallRenderer)}: {nameof(ISnowEnvironmentSource)} bulunamadı. " +
                           "Kar yağışı devre dışı.");
            enabled = false;
            return;
        }

        flakes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, FlakeCapacity, FlakeStride);
        drift = new GraphicsBuffer(GraphicsBuffer.Target.Structured, DriftCapacity, FlakeStride);

        // Tamponlar sıfırla başlıyor: `lifetime = 0` yani hepsi kapalı.
        flakes.SetData(new float[FlakeCapacity * 12]);
        drift.SetData(new float[DriftCapacity * 12]);

        flakeKernel = snowfallCompute.FindKernel("KFlakeUpdate");
        driftKernel = snowfallCompute.FindKernel("KDriftUpdate");

        flakeBlock ??= new MaterialPropertyBlock();
        driftBlock ??= new MaterialPropertyBlock();

        aliveFlakes = 0;
        aliveDrift = 0;
    }

    void OnDisable()
    {
        flakes?.Dispose();
        drift?.Dispose();

        flakes = null;
        drift = null;

        aliveFlakes = 0;
        aliveDrift = 0;
    }

    void LateUpdate()
    {
        if (flakes == null) return;

        UpdateCounts();
        UpdateSpawnVolume();

        if (!HasWork) return;

        Draw();
    }

    /// AÇIK YUVA SAYISI ŞİDDETTEN TÜRÜYOR. VFX yoğunluğu ile
    /// `_SnowfallSWERate` AYNI `i01` değerinden geliyor (spec §17.2); ayrı
    /// kaynaklardan gelseler "yağıyor ama birikmiyor" olurdu.
    void UpdateCounts()
    {
        aliveFlakes = FlakeCountFor(SnowRuntimeState.SnowfallIntensity01,
                                    settings.QualityData.VfxCapacityScale);

        aliveDrift = DriftCountFor(env.WindSpeed, SnowRuntimeState.LooseSnowFraction,
                                   spindriftRate, settings.QualityData.VfxCapacityScale);
    }

    /// Kararlı durumda canlı tane sayısı = doğum oranı × ortalama ömür.
    /// Saf fonksiyon: Play'e girmeden sınanabiliyor.
    public static int FlakeCountFor(float intensity01, float capacityScale)
    {
        const float MeanLifetime = 6.5f;

        float wanted = intensity01 * SnowConstants.MaxFlakeRate * MeanLifetime * capacityScale;
        return Mathf.Clamp(Mathf.RoundToInt(wanted), 0, FlakeCapacity);
    }

    /// YER SAVRULMASI EŞİĞİ (spec §17.1 Sistem B): rüzgâr 7 m/s üstü VE
    /// gevşek kar oranı 0.15 üstü. Sıkışmış yüzeyde savrulacak tane yok.
    public static int DriftCountFor(float windSpeed, float looseFraction,
                                    float rate, float capacityScale)
    {
        if (windSpeed <= 7f || looseFraction <= 0.15f) return 0;

        float gate = rate * Mathf.Pow(Mathf.Clamp01((windSpeed - 7f) / 10f), 2f) * looseFraction;

        return Mathf.Clamp(Mathf.RoundToInt(gate * DriftCapacity * capacityScale),
                           0, DriftCapacity);
    }

    /// Doğum kutusunun merkezi, 1 m ızgarasına snap'li (spec §17.1).
    /// Snap yoksa kamera hareketinde doğum deseni yürüyor.
    public static Vector3 SnapSpawnCenter(Vector3 followPosition, Vector3 windDirection)
    {
        Vector3 raw = followPosition + Vector3.up * SpawnUp + windDirection * SpawnWindLead;

        return new Vector3(
            Mathf.Round(raw.x / SpawnSnap) * SpawnSnap,
            Mathf.Round(raw.y / SpawnSnap) * SpawnSnap,
            Mathf.Round(raw.z / SpawnSnap) * SpawnSnap);
    }

    void UpdateSpawnVolume()
    {
        spawnCenter = SnapSpawnCenter(followTarget.position, env.WindDirection);
        driftOrigin = followTarget.position;
    }

    /// SnowManager tek CommandBuffer içinde çağırıyor (spec §15.2).
    public void Dispatch(CommandBuffer cmd)
    {
        if (flakes == null || !HasWork) return;

        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.SnowDeltaTime, Time.deltaTime);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.FlakeSeed, Time.frameCount * 0.017f);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.FlakeBaseSize, flakeBaseSize);

        cmd.SetComputeVectorParam(snowfallCompute, SnowShaderIDs.SpawnCenter, spawnCenter);
        cmd.SetComputeVectorParam(snowfallCompute, SnowShaderIDs.SpawnExtent, SpawnBox * 0.5f);

        // TÜRBÜLANS RÜZGÂRLA ARTIYOR (spec §17.1).
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.TurbulenceIntensity,
                                 0.35f * env.WindSpeed + 0.15f);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.TurbulenceFrequency, 0.12f);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.TurbulenceDrag, 0.9f);

        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.FlutterFreq, 5.5f);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.FlutterAmp, 0.35f);

        cmd.SetComputeVectorParam(snowfallCompute, SnowShaderIDs.DriftOrigin, driftOrigin);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.DriftStripLength, 30f);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.DriftStripWidth, 20f);

        if (aliveFlakes > 0)
            DispatchPool(cmd, flakeKernel, flakes, FlakeCapacity, aliveFlakes);

        if (aliveDrift > 0)
            DispatchPool(cmd, driftKernel, drift, DriftCapacity, aliveDrift);
    }

    void DispatchPool(CommandBuffer cmd, int kernel, GraphicsBuffer buffer, int capacity, int alive)
    {
        cmd.SetComputeIntParam(snowfallCompute, SnowShaderIDs.FlakeCapacity, capacity);
        cmd.SetComputeIntParam(snowfallCompute, SnowShaderIDs.FlakeAliveCount, alive);
        cmd.SetComputeBufferParam(snowfallCompute, kernel, SnowShaderIDs.Flakes, buffer);

        // Yalnız AÇIK yuvalar işleniyor; kapalılar için iş yok.
        int groups = Mathf.CeilToInt(alive / (float)ThreadGroupSize);
        cmd.DispatchCompute(snowfallCompute, kernel, groups, 1, 1);
    }

    void Draw()
    {
        var bounds = new Bounds(followTarget.position, Vector3.one * 400f);

        if (aliveFlakes > 0)
        {
            flakeBlock.SetBuffer(SnowShaderIDs.Flakes, flakes);

            var rp = new RenderParams(flakeMaterial)
            {
                worldBounds = bounds,
                matProps = flakeBlock,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            Graphics.RenderPrimitives(rp, MeshTopology.Triangles, 6, aliveFlakes);
        }

        if (aliveDrift > 0)
        {
            driftBlock.SetBuffer(SnowShaderIDs.Flakes, drift);

            var rp = new RenderParams(driftMaterial)
            {
                worldBounds = bounds,
                matProps = driftBlock,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };

            Graphics.RenderPrimitives(rp, MeshTopology.Triangles, 6, aliveDrift);
        }
    }
}
