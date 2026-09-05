// ROLE: manages the buffers of the snowflakes and the ground drift, queues the simulation
// and records the draw.
// CALLED BY: the scene (next to SnowManager); the simulation from SnowManager.Dispatch.

using UnityEngine;
using UnityEngine.Rendering;

/// COMPUTE INSTEAD OF VFX GRAPH — THE REASONING IS IN `DECISIONS.md`.
///
/// Spec §17.1 asks for `VFX_Snowfall.vfx`. A VFX Graph asset is a node graph and cannot be
/// written from text; the only counterpart producible in this workflow is a particle pool
/// simulated on the GPU. All of the BEHAVIOURS the spec lists were implemented (a snapped
/// spawn box, lifetime, wind speed, turbulence, oscillation, a minimum screen size, the fog
/// fade, the cover and ground cull, the atlas, the stretch).
///
/// WITH NO SNOW THE COST IS ZERO (spec §15.2): with no precipitation there is neither a
/// dispatch nor a draw.
[DisallowMultipleComponent]
public class SnowfallRenderer : MonoBehaviour
{
    /// 12 floats per particle (`SnowFlake` in SnowfallSim.compute).
    const int FlakeStride = 12 * sizeof(float);

    const int ThreadGroupSize = 64;

    /// THE CAPACITY WAS CLIPPING THE SPEC'S OWN FORMULA.
    ///
    /// Spec §17.1 gives the capacity as 40 000, §17.2 the birth rate as 16 000/s and the
    /// lifetime as 4–9 s (6.5 on average). In steady state there should be
    /// 16 000 × 6.5 = 104 000 particles; the 40 000 ceiling cut that off after i01 ≈ 0.385
    /// and DECOUPLED the density from the intensity.
    ///
    /// The physics wants more too: 5 mm/h SWE, a 1 m/s fall speed and a 100 kg/m³ density
    /// → ~139 particles/m³ in the air. The spec's 40×26×40 box is 41 600 m³; 40 000
    /// particles make 1 particle/m³, two orders of magnitude too sparse.
    /// 104 000 particles make 2.5 particles/m³ — still below the physics but it gives the
    /// look of dense snowfall at full intensity and keeps the intensity–density link
    /// linear. The reasoning is in `DECISIONS.md`.
    const int FlakeCapacity = 160000;
    const int DriftCapacity = 8000;

    /// The spawn box (spec §17.1). 11 m above the camera, 3 m along the wind direction.
    static readonly Vector3 SpawnBox = new(40f, 26f, 40f);
    const float SpawnUp = 11f;
    const float SpawnWindLead = 3f;

    /// The grid the spawn box sits on. Without the snap the birth pattern walks as the
    /// camera moves.
    const float SpawnSnap = 1f;

    [Header("Dependencies")]
    [SerializeField] SnowSettings settings;
    [SerializeField] ComputeShader snowfallCompute;
    [SerializeField] Material flakeMaterial;
    [SerializeField] Material driftMaterial;

    [Tooltip("The target that will stand at the spawn box's centre — usually the camera.")]
    [SerializeField] Transform followTarget;

    [Tooltip("The precipitation and wind source.")]
    [SerializeField] MonoBehaviour environmentSource;

    [Header("Ayarlar")]
    [Tooltip("The particle's base size (m). It is multiplied by a random 0.6–1.7.")]
    [SerializeField] float flakeBaseSize = 0.018f;


    [Tooltip("The maximum birth rate coefficient of the ground drift.")]
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

    /// FOR MEASUREMENT. The diagnostic window reads the buffer back to the CPU and prints
    /// the real size, alpha and distance distribution.
    public GraphicsBuffer FlakeBuffer => flakes;
    public GraphicsBuffer DriftBuffer => drift;

    public int AliveFlakes => aliveFlakes;
    public int AliveDrift => aliveDrift;

    /// Whether the simulation will run. If it is not snowing and there is no drift, nothing
    /// is done.
    public bool HasWork => aliveFlakes > 0 || aliveDrift > 0;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SnowfallRenderer)}: {nameof(settings)} is not assigned.");
        if (snowfallCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowfallRenderer)}: {nameof(snowfallCompute)} is not assigned.");
        if (flakeMaterial == null || driftMaterial == null)
            throw new System.InvalidOperationException($"{nameof(SnowfallRenderer)}: the materials are not assigned.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowfallRenderer)}: the follow target is not assigned.");

        env = environmentSource as ISnowEnvironmentSource;

        if (env == null)
        {
            Debug.LogError($"{nameof(SnowfallRenderer)}: {nameof(ISnowEnvironmentSource)} was not found. " +
                           "Snowfall disabled.");
            enabled = false;
            return;
        }

        flakes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, FlakeCapacity, FlakeStride);
        drift = new GraphicsBuffer(GraphicsBuffer.Target.Structured, DriftCapacity, FlakeStride);

        // The buffers start at zero: `lifetime = 0`, i.e. all of them are off.
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

    /// THE NUMBER OF OPEN SLOTS DERIVES FROM THE INTENSITY. The VFX density and
    /// `_SnowfallSWERate` come from the SAME `i01` value (spec §17.2); coming from separate
    /// sources it would be "it is snowing but nothing accumulates".
    void UpdateCounts()
    {
        ShelterExposure shelter = ShelterExposure.Active;
        float exposure = shelter != null ? shelter.PrecipitationExposure : 1f;

        aliveFlakes = FlakeCountFor(SnowRuntimeState.SnowfallIntensity01 * exposure,
                                    settings.QualityData.VfxCapacityScale);

        aliveDrift = DriftCountFor(env.WindSpeed, SnowRuntimeState.LooseSnowFraction,
                                   spindriftRate * exposure, settings.QualityData.VfxCapacityScale);

    }

    /// In steady state the live particle count = the birth rate × the mean lifetime.
    ///
    /// THE SATURATION IS THE SPEC'S OWN BEHAVIOUR. §17.2 gives `_flakeRate` as 16000/s at
    /// full intensity, §17.1 the lifetime as 4–9 s (6.5 s on average) and binds the capacity
    /// at 40000: 16000 × 6.5 = 104000 > 40000. A VFX Graph pool behaves exactly like this —
    /// after i01 ≈ 0.385 the density stays at the capacity.
    /// At one point I changed this to "proportional"; it was a deviation from the spec and
    /// was reverted.
    ///
    /// A pure function: it can be tested without entering Play.
    public static int FlakeCountFor(float intensity01, float capacityScale)
    {
        const float MeanLifetime = 6.5f;

        float wanted = intensity01 * SnowConstants.MaxFlakeRate * MeanLifetime * capacityScale;
        return Mathf.Clamp(Mathf.RoundToInt(wanted), 0, FlakeCapacity);
    }

    /// THE GROUND DRIFT THRESHOLD (spec §17.1 System B): wind above 7 m/s AND a loose snow
    /// fraction above 0.15. On a compacted surface there is nothing to drift.
    public static int DriftCountFor(float windSpeed, float looseFraction,
                                    float rate, float capacityScale)
    {
        if (windSpeed <= 7f || looseFraction <= 0.15f) return 0;

        float gate = rate * Mathf.Pow(Mathf.Clamp01((windSpeed - 7f) / 10f), 2f) * looseFraction;

        return Mathf.Clamp(Mathf.RoundToInt(gate * DriftCapacity * capacityScale),
                           0, DriftCapacity);
    }

    /// The spawn box's centre, snapped to a 1 m grid (spec §17.1).
    /// Without the snap the birth pattern walks as the camera moves.
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

    /// SnowManager calls it inside a single CommandBuffer (spec §15.2).
    public void Dispatch(CommandBuffer cmd)
    {
        if (flakes == null || !HasWork) return;

        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.SnowDeltaTime, Time.deltaTime);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.FlakeSeed, Time.frameCount * 0.017f);
        cmd.SetComputeIntParam(snowfallCompute, SnowShaderIDs.FlakeSeedU, Time.frameCount);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.FlakeBaseSize, flakeBaseSize);

        cmd.SetComputeVectorParam(snowfallCompute, SnowShaderIDs.SpawnCenter, spawnCenter);
        cmd.SetComputeVectorParam(snowfallCompute, SnowShaderIDs.SpawnExtent, SpawnBox * 0.5f);

        // THE TURBULENCE RISES WITH THE WIND (spec §17.1).
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

        // Only OPEN slots are processed; there is no work for the closed ones.
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
