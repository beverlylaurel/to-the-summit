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

    int scrollStateKernel = -1;
    int accumulateKernel = -1;

    Vector2Int centerTexel;
    bool pendingClear;
    bool pendingScroll;
    Vector2Int pendingScrollTexels;

    /// KASKADIN ORTALAMA SWE'Sİ. Karenin DIŞINDAKİ karı bu doku besliyor;
    /// sıfırsa dağ çıplak kalıyor ve ekranda "kar sadece ayağımın altında"
    /// görünüyor. Yakın bölgenin sayısı ölçülüyordu, bunun ki ölçülmüyordu.
    public float MeanSwe { get; private set; } = -1f;

    bool readbackPending;
    int lastReadbackFrame = -1;

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

        scrollStateKernel = simCompute.FindKernel("KFarScrollState");
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
                new Vector4(0f, settings.DefaultRhoN, 0f, 0f));

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

            // Yeni açılan şerit kar çizgisi eğrisinden doluyor (KFarScrollState).
            cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.FarNewEdgeValue,
                new Vector4(0f, settings.DefaultRhoN, 0f, 0f));

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

        RequestReadback();
    }

    /// Otuz karede bir, tek örnek. Ölçüm aracı; maliyeti yok denecek kadar az.
    void RequestReadback()
    {
        if (readbackPending) return;
        if (Time.frameCount - lastReadbackFrame < 30) return;

        lastReadbackFrame = Time.frameCount;
        readbackPending = true;

        // FORMAT ŞART. `RT_SnowFar` RGHalf (4 bayt/piksel); `Vector2` 8 bayt.
        // Yanlış boyutta okunan yarı-hassas bitler float32 olarak yorumlanınca
        // denormal çıkıyor ve ölçüm her zaman 0.0000 yazıyordu — aracın
        // kendisi yalan söylüyordu. Okuma açıkça RGBAFloat'a çevriliyor.
        UnityEngine.Rendering.AsyncGPUReadback.Request(far, 0, TextureFormat.RGBAFloat, request =>
        {
            readbackPending = false;
            if (request.hasError || far == null) return;

            var data = request.GetData<Color>();

            float sum = 0f;
            int step = Mathf.Max(1, data.Length / 4096);
            int n = 0;

            for (int i = 0; i < data.Length; i += step) { sum += data[i].r; n++; }

            MeanSwe = n > 0 ? sum / n : 0f;
        });
    }

    void Scroll(CommandBuffer cmd, int groups)
    {
        // Kaskad HER ZAMAN durum dokusu; yeni şerit kar çizgisinden doluyor.
        cmd.SetComputeTextureParam(simCompute, scrollStateKernel, SnowShaderIDs.FarSrc, far);
        cmd.SetComputeTextureParam(simCompute, scrollStateKernel, SnowShaderIDs.FarDst, farTemp);
        cmd.DispatchCompute(simCompute, scrollStateKernel, groups, groups, 1);

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
