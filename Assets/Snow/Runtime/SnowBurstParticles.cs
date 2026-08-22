// ROL: kısa ömürlü patlama tipi tanelerin havuzu — ayak tozu (spec §19.3) ve
// kar püskürtme (spec §18.6) bunu paylaşıyor.
// Çağıran: SnowPuffEmitter, SnowSprayController.

using UnityEngine;
using UnityEngine.Rendering;

/// DOĞUM CPU'DA, ENTEGRASYON GPU'DA.
///
/// Patlama tanelerinin doğum yeri ve hızı oyunun kendi olaylarından geliyor
/// (ayak nereye bastı, hangi hızla). O bilgi zaten CPU'da; GPU'ya taşıyıp
/// orada doğurmak fazladan bir tampon ve bir kernel demek olurdu.
///
/// Yuvalar sırayla kullanılıyor. Havuz dolduğunda en eskiler üzerine
/// yazılıyor — patlama taneleri kısa ömürlü, kaybolan zaten ölmek üzereydi.
[DisallowMultipleComponent]
public class SnowBurstParticles : MonoBehaviour
{
    /// Tane başına 12 float (SnowfallSim.compute'taki `SnowFlake`).
    const int Stride = 12 * sizeof(float);
    const int Floats = 12;
    const int ThreadGroupSize = 64;

    [Header("Bağımlılıklar")]
    [SerializeField] ComputeShader snowfallCompute;
    [SerializeField] Material material;

    [Header("Havuz")]
    [SerializeField] int capacity = 3000;

    [Header("Fizik")]
    [Tooltip("Yerçekimi çarpanı. Kar tanesi hafif, sürüklenme baskın (spec §18.6).")]
    [SerializeField] float gravityScale = 0.35f;

    [SerializeField] float drag = 2.5f;

    [Tooltip("Rüzgârın tanesi ne kadar sürüklediği.")]
    [SerializeField] float windPull = 0.25f;

    [Tooltip("Ömür boyunca boyutun büyüme hızı (dağılma).")]
    [SerializeField] float growth = 0.6f;

    GraphicsBuffer buffer;
    float[] staging;
    MaterialPropertyBlock block;

    int kernel = -1;
    int cursor;
    int liveEstimate;

    public int Capacity => capacity;

    /// Kaç tanenin canlı SAYILDIĞI. Kesin değil (GPU'da ölenler burada
    /// düşmüyor); yalnız çizim ve dispatch kapısı için.
    public int LiveEstimate => liveEstimate;

    void OnEnable()
    {
        if (snowfallCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowBurstParticles)}: compute atanmadı.");
        if (material == null)
            throw new System.InvalidOperationException($"{nameof(SnowBurstParticles)}: materyal atanmadı.");

        buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, Stride);
        buffer.SetData(new float[capacity * Floats]);

        staging = new float[Floats];
        block ??= new MaterialPropertyBlock();

        kernel = snowfallCompute.FindKernel("KBurstUpdate");

        cursor = 0;
        liveEstimate = 0;
    }

    void OnDisable()
    {
        buffer?.Dispose();
        buffer = null;
        liveEstimate = 0;
    }

    /// Tek tane doğurur. Çağıran kaç tane istediğine kendi karar veriyor.
    public void Emit(Vector3 position, Vector3 velocity, float size, float lifetime)
    {
        if (buffer == null) return;

        staging[0] = position.x;
        staging[1] = position.y;
        staging[2] = position.z;
        staging[3] = 0f;                    // age
        staging[4] = velocity.x;
        staging[5] = velocity.y;
        staging[6] = velocity.z;
        staging[7] = lifetime;
        staging[8] = size;
        staging[9] = Random.value * 6.2831853f;
        staging[10] = Mathf.Floor(Random.value * 16f);
        staging[11] = 0f;                   // alpha

        buffer.SetData(staging, 0, cursor * Floats, Floats);

        cursor = (cursor + 1) % capacity;
        liveEstimate = Mathf.Min(liveEstimate + 1, capacity);
    }

    /// SnowManager tek CommandBuffer içinde çağırıyor (spec §15.2).
    public void Dispatch(CommandBuffer cmd)
    {
        if (buffer == null || liveEstimate == 0) return;

        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.SnowDeltaTime, Time.deltaTime);
        cmd.SetComputeIntParam(snowfallCompute, SnowShaderIDs.FlakeCapacity, capacity);

        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.BurstGravity,
                                 Physics.gravity.y * gravityScale);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.BurstDrag, drag);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.BurstWindPull, windPull);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.BurstGrowth, growth);

        cmd.SetComputeBufferParam(snowfallCompute, kernel, SnowShaderIDs.Flakes, buffer);

        int groups = Mathf.CeilToInt(capacity / (float)ThreadGroupSize);
        cmd.DispatchCompute(snowfallCompute, kernel, groups, 1, 1);
    }

    void LateUpdate()
    {
        if (buffer == null || liveEstimate == 0) return;

        block.SetBuffer(SnowShaderIDs.Flakes, buffer);

        var rp = new RenderParams(material)
        {
            worldBounds = new Bounds(transform.position, Vector3.one * 60f),
            matProps = block,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };

        Graphics.RenderPrimitives(rp, MeshTopology.Triangles, 6, capacity);
    }
}
