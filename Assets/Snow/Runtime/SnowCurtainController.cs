// ROL: süspansiyon perdelerinin tamponunu yönetir, simülasyonu kuyruğa yazar,
// çizimi kaydeder (spec §18.7).
// Çağıran: sahne; simülasyon SnowManager.Dispatch'ten.

using UnityEngine;
using UnityEngine.Rendering;

/// ON DÖRT PARÇACIK, BİLEREK (spec §18.7).
///
/// Maliyet parçacık sayısı değil FILL-RATE: her perde ekranın büyük kısmını
/// kaplayabiliyor. Sayıyı artırmak kazanç getirmiyor, kare süresini yakıyor.
///
/// Tetik §18.1'deki `driftActive` ile AYNI — ayrı bir rüzgâr eşiği
/// tanımlanmıyor. Savrulacak gevşek kar yoksa perde de yok.
[DisallowMultipleComponent]
public class SnowCurtainController : MonoBehaviour
{
    const int Stride = 12 * sizeof(float);
    const int Floats = 12;
    const int ThreadGroupSize = 64;

    /// Spec §18.7: capacity 14, Low preset'te 6.
    const int Capacity = 14;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;
    [SerializeField] ComputeShader snowfallCompute;
    [SerializeField] Material curtainMaterial;

    [Tooltip("Perdelerin doğduğu şeridin merkezinde duracak hedef.")]
    [SerializeField] Transform followTarget;

    [SerializeField] MonoBehaviour environmentSource;

    ISnowEnvironmentSource env;

    GraphicsBuffer curtains;
    MaterialPropertyBlock block;

    int kernel = -1;
    int alive;
    float driftActive;

    public int Alive => alive;
    public float DriftActive => driftActive;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SnowCurtainController)}: {nameof(settings)} atanmadı.");
        if (snowfallCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowCurtainController)}: compute atanmadı.");
        if (curtainMaterial == null)
            throw new System.InvalidOperationException($"{nameof(SnowCurtainController)}: materyal atanmadı.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowCurtainController)}: takip hedefi atanmadı.");

        env = environmentSource as ISnowEnvironmentSource;

        if (env == null)
        {
            Debug.LogError($"{nameof(SnowCurtainController)}: {nameof(ISnowEnvironmentSource)} bulunamadı. " +
                           "Perdeler devre dışı.");
            enabled = false;
            return;
        }

        curtains = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Capacity, Stride);
        curtains.SetData(new float[Capacity * Floats]);

        block ??= new MaterialPropertyBlock();
        kernel = snowfallCompute.FindKernel("KCurtainUpdate");

        alive = 0;
        driftActive = 0f;
    }

    void OnDisable()
    {
        curtains?.Dispose();
        curtains = null;
        alive = 0;
    }

    /// §18.1'in eşiğiyle AYNI: yoğunluk arttıkça savrulma eşiği yükseliyor
    /// (sinterlenme). Gevşek karda 5 m/s, sıkışmışta 11 m/s.
    public static float DriftActiveFor(float windSpeed, float looseFraction)
    {
        float rhoN = 1f - Mathf.Clamp01(looseFraction);
        float threshold = Mathf.Lerp(SnowConstants.DriftU10Loose,
                                     SnowConstants.DriftU10Packed, rhoN);

        return Mathf.Clamp01((windSpeed - threshold) / 4f);
    }

    void LateUpdate()
    {
        if (curtains == null) return;

        driftActive = DriftActiveFor(env.WindSpeed, SnowRuntimeState.LooseSnowFraction);

        int wanted = driftActive > 0.001f
            ? Mathf.RoundToInt(Capacity * settings.QualityData.VfxCapacityScale)
            : 0;

        alive = Mathf.Clamp(wanted, 0, Capacity);

        if (alive == 0) return;

        block.SetBuffer(SnowShaderIDs.Flakes, curtains);

        var rp = new RenderParams(curtainMaterial)
        {
            worldBounds = new Bounds(followTarget.position, Vector3.one * 200f),
            matProps = block,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
        };

        Graphics.RenderPrimitives(rp, MeshTopology.Triangles, 6, alive);
    }

    /// SnowManager tek CommandBuffer içinde çağırıyor (spec §15.2).
    public void Dispatch(CommandBuffer cmd)
    {
        if (curtains == null || alive == 0) return;

        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.SnowDeltaTime, Time.deltaTime);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.FlakeSeed, Time.frameCount * 0.013f);
        cmd.SetComputeIntParam(snowfallCompute, SnowShaderIDs.FlakeSeedU, Time.frameCount);

        cmd.SetComputeIntParam(snowfallCompute, SnowShaderIDs.FlakeCapacity, Capacity);
        cmd.SetComputeIntParam(snowfallCompute, SnowShaderIDs.FlakeAliveCount, alive);

        cmd.SetComputeVectorParam(snowfallCompute, SnowShaderIDs.DriftOrigin, followTarget.position);

        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.CurtainDriftActive, driftActive);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.CurtainScaleH, SnowConstants.SuspScaleH);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.CurtainAlphaBase,
                                 SnowConstants.SuspAlphaBase);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.CurtainSpawnDistance, 35f);
        cmd.SetComputeFloatParam(snowfallCompute, SnowShaderIDs.CurtainSpawnWidth, 40f);

        cmd.SetComputeBufferParam(snowfallCompute, kernel, SnowShaderIDs.Flakes, curtains);

        cmd.DispatchCompute(snowfallCompute, kernel,
                            Mathf.CeilToInt(Capacity / (float)ThreadGroupSize), 1, 1);
    }
}
