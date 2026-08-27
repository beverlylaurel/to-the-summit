// ROLE: the pool of short-lived burst particles — the footstep puff (spec §19.3) and
// the snow spray (spec §18.6) share it.
// CALLED BY: SnowPuffEmitter, SnowSprayController.

using UnityEngine;
using UnityEngine.Rendering;

/// BIRTH ON THE CPU, INTEGRATION ON THE GPU.
///
/// The birth place and velocity of the burst particles come from the game's own events
/// (where the foot landed, at what speed). That information is already on the CPU;
/// carrying it to the GPU and giving birth there would mean an extra buffer and a kernel.
///
/// The slots are used in order. When the pool fills, the oldest are overwritten —
/// burst particles are short-lived, and the one lost was about to die anyway.
[DisallowMultipleComponent]
public class SnowBurstParticles : MonoBehaviour
{
    /// 12 floats per particle (`SnowFlake` in SnowfallSim.compute).
    const int Stride = 12 * sizeof(float);
    const int Floats = 12;
    const int ThreadGroupSize = 64;

    [Header("Dependencies")]
    [SerializeField] ComputeShader snowfallCompute;
    [SerializeField] Material material;

    [Header("Havuz")]
    [SerializeField] int capacity = 3000;

    [Header("Fizik")]
    [Tooltip("Gravity multiplier. A snow grain is light and drag dominates (spec §18.6).")]
    [SerializeField] float gravityScale = 0.35f;

    [SerializeField] float drag = 2.5f;

    [Tooltip("How much the wind drags the grain.")]
    [SerializeField] float windPull = 0.25f;

    [Tooltip("How fast the size grows over the lifetime (dispersal).")]
    [SerializeField] float growth = 0.6f;

    GraphicsBuffer buffer;
    float[] staging;
    MaterialPropertyBlock block;

    int kernel = -1;
    int cursor;
    int liveEstimate;

    public int Capacity => capacity;

    /// How many particles are COUNTED as alive. Not exact (those dying on the GPU are not
    /// subtracted here); only for the draw and dispatch gate.
    public int LiveEstimate => liveEstimate;

    void OnEnable()
    {
        if (snowfallCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowBurstParticles)}: the compute is not assigned.");
        if (material == null)
            throw new System.InvalidOperationException($"{nameof(SnowBurstParticles)}: the material is not assigned.");

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

    /// Gives birth to a single particle. The caller decides how many it wants.
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

    /// SnowManager calls it inside a single CommandBuffer (spec §15.2).
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
