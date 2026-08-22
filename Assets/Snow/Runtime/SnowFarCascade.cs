// ROL: yakın bölgenin dışındaki karın durumu — 192 m, 512 teksel, iki kanal
// (spec §21 Faz 10).
// Çağıran: SnowManager (Dispatch ve global yazımı).

using UnityEngine;
using UnityEngine.Rendering;

/// UZAKTA DA GERÇEK KAR VAR.
///
/// Yakın bölge 16 m. Ondan ötesi `_FallbackSWE` diye SABİT bir sayıydı: dağın
/// tamamı aynı kalınlıkta kar taşıyordu, öğlen erimiyordu, gece birikmiyordu.
/// Kaskad orada da birikme, oturma ve erime veriyor — 37.5 cm tekselde,
/// deformasyon olmadan.
[DisallowMultipleComponent]
public class SnowFarCascade : MonoBehaviour
{
    /// Spec §21 Faz 10.
    const float AreaSize = 192f;
    const int Resolution = 512;

    /// Kaskad kendi teksel ızgarasına snap'leniyor; yakın bölgeninkine değil.
    static float TexelSize => AreaSize / Resolution;

    [SerializeField] SnowSettings settings;
    [SerializeField] ComputeShader simCompute;

    [Tooltip("Kaskadın merkezinde duracak hedef — genelde oyuncu.")]
    [SerializeField] Transform followTarget;

    RenderTexture far;
    RenderTexture farTemp;

    int scrollKernel = -1;
    int accumulateKernel = -1;

    Vector2Int centerTexel;
    bool pendingClear;
    bool pendingScroll;
    Vector2Int pendingScrollTexels;

    public RenderTexture Texture => far;
    public Vector2 AreaCenter => new(centerTexel.x * TexelSize, centerTexel.y * TexelSize);
    public bool IsReady => far != null;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(SnowFarCascade)}: {nameof(settings)} atanmadı.");
        if (simCompute == null)
            throw new System.InvalidOperationException($"{nameof(SnowFarCascade)}: compute atanmadı.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowFarCascade)}: takip hedefi atanmadı.");

        far = Create("RT_SnowFar");
        farTemp = Create("RT_SnowFarTemp");

        scrollKernel = simCompute.FindKernel("KFarScroll");
        accumulateKernel = simCompute.FindKernel("KFarAccumulate");

        centerTexel = Snap(followTarget.position);
        pendingClear = true;
        pendingScroll = false;
    }

    void OnDisable()
    {
        Release(ref far);
        Release(ref farTemp);
    }

    static RenderTexture Create(string name)
    {
        var rt = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.RGHalf)
        {
            name = name,
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
            hideFlags = HideFlags.HideAndDontSave,
        };

        rt.Create();
        return rt;
    }

    static void Release(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        DestroyImmediate(rt);
        rt = null;
    }

    static Vector2Int Snap(Vector3 worldPos)
    {
        // Kaskadın kendi adımı: bir teksel. Daha ince snap'in karşılığı yok,
        // daha kaba olan görünür sıçrama üretir.
        return new Vector2Int(Mathf.FloorToInt(worldPos.x / TexelSize),
                              Mathf.FloorToInt(worldPos.z / TexelSize));
    }

    void LateUpdate()
    {
        if (far == null) return;

        Vector2Int next = Snap(followTarget.position);
        if (next == centerTexel) return;

        Vector2Int delta = next - centerTexel;

        pendingScrollTexels = pendingScroll ? pendingScrollTexels + delta : delta;
        pendingScroll = true;

        centerTexel = next;
    }

    /// SnowManager tek CommandBuffer içinde çağırıyor (spec §15.2).
    public void Dispatch(CommandBuffer cmd)
    {
        if (far == null) return;

        int groups = Mathf.CeilToInt(Resolution / (float)SnowConstants.GroupSize);
        Vector2 center = AreaCenter;

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.FarResolution, Resolution);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.FarAreaSize, AreaSize);
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.FarAreaCenter,
                                  new Vector4(center.x, center.y, 0f, 0f));

        if (pendingClear)
        {
            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.FarScrollTexels, Vector4.zero);
            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.FarNewEdgeValue,
                new Vector4(settings.DefaultSwe, settings.DefaultRhoN, 0f, 0f));

            // Kaydırmayı sıfır deltayla koşturmak dokuyu kenar değeriyle
            // doldurmuyor; temizlik için delta dokunun tamamı kadar veriliyor.
            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.FarScrollTexels,
                new Vector4(Resolution, Resolution, 0f, 0f));

            Scroll(cmd, groups);
            pendingClear = false;
        }

        if (pendingScroll)
        {
            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.FarScrollTexels,
                new Vector4(pendingScrollTexels.x, pendingScrollTexels.y, 0f, 0f));

            // Yeni açılan şerit dünyanın genel durumuyla doluyor.
            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.FarNewEdgeValue,
                new Vector4(settings.DefaultSwe, settings.DefaultRhoN, 0f, 0f));

            Scroll(cmd, groups);

            pendingScroll = false;
            pendingScrollTexels = Vector2Int.zero;
        }

        // Kaskad tam çözünürlükte her karede koşuyor: 512² ucuz ve döşeme
        // döndürmesi bu ölçekte kazanç getirmiyor.
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DeltaTimeEff, Time.deltaTime);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.FarSrc, far);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.FarDst, farTemp);
        cmd.DispatchCompute(simCompute, accumulateKernel, groups, groups, 1);

        (far, farTemp) = (farTemp, far);
    }

    void Scroll(CommandBuffer cmd, int groups)
    {
        cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.FarSrc, far);
        cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.FarDst, farTemp);
        cmd.DispatchCompute(simCompute, scrollKernel, groups, groups, 1);

        (far, farTemp) = (farTemp, far);
    }

    /// Kaskadı dünyanın güncel kar durumuyla baştan doldurur.
    public void RefillRegion() => pendingClear = true;

    public void WriteGlobals()
    {
        if (far == null) return;

        Vector2 center = AreaCenter;

        Shader.SetGlobalTexture(SnowShaderIDs.SnowFarTex, far);
        Shader.SetGlobalVector(SnowShaderIDs.SnowFarCenter,
                               new Vector4(center.x, center.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.SnowFarAreaSize, AreaSize);
    }
}
