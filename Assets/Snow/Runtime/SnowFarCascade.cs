// ROL: uzak kar kaskadı (§10, Faz 10). Yakın bölge 24 m; kaskad 192 m ve yalnız
// swe + rhoN taşıyor. Clipmap'in dış halkaları düz yedek yerine bunu okuyor, yani
// at ve araba izleri uzaktan da görünür kalıyor.
// Çağıran: SnowManager (dağıtım), SnowLitInput.hlsl (okuma).

using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class SnowFarCascade : MonoBehaviour
{
    /// Kaskadın kapsadığı kare alan, metre (§10).
    public const float AreaSize = 192f;

    /// Kaskad çözünürlüğü (§10).
    public const int Resolution = 1024;

    /// İndirgeme kaç karede bir koşuyor.
    ///
    /// Kağıtta hesap: 128x128 kaskad tekseli x 16x16 yakın teksel = 16.7 milyon okuma.
    /// Bu, tam çözünürlüklü bir geçişin DÖRT KATI — her karede koşturmak §11.2'nin
    /// bütün deformasyon bütçesini tek başına yerdi.
    ///
    /// Sekiz kare tazelik yeterli: kaskad yalnız 12 m'den uzakta okunuyor ve orada
    /// bir kaskad tekseli ekranda bir pikselin altında kalıyor.
    const int WriteInterval = 8;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowManager manager;
    [SerializeField] ComputeShader simCompute;

    [Tooltip("Kalıcılık deposu. Boş bırakılabilir; o zaman pencereden çıkan iz kaybolur.")]
    [SerializeField] SnowPersistence persistence;

    RenderTexture cascade;
    RenderTexture cascadeTemp;

    int clearKernel = -1;
    int scrollKernel = -1;
    int writeKernel = -1;

    /// Kaskad merkezi, KENDİ teksel ızgarasında tam sayı. Yakın bölgeyle aynı kural:
    /// kesirli kaydırma içeriği her adımda yeniden örnekler.
    Vector2Int centerTexel;

    readonly System.Collections.Generic.List<Vector2Int> pendingRestores =
        new System.Collections.Generic.List<Vector2Int>();

    bool pendingClear;
    bool pendingScroll;
    Vector2Int pendingScrollTexels;

    public RenderTexture CascadeTexture => cascade;
    public float TexelSize => AreaSize / Resolution;
    public Vector2 AreaCenter => (Vector2)centerTexel * TexelSize;

    void OnEnable()
    {
        if (manager == null)
            throw new System.InvalidOperationException("SnowFarCascade: SnowManager atanmadı.");
        if (simCompute == null)
            throw new System.InvalidOperationException("SnowFarCascade: SnowSim.compute atanmadı.");

        // RGHalf: yalnız swe ve rhoN. Islaklık ve tazelik uzakta okunmuyor.
        //
        // Half burada yeterli: kaskada YAZILAN değer yakın dokudan gelen bir ortalama,
        // üstüne küçük artımlar eklenmiyor. Yakın dokudaki hassasiyet sorunu birikimin
        // kendisinden geliyordu, depolamadan değil.
        cascade = Create("RT_FarCascade");
        cascadeTemp = Create("RT_FarCascadeTemp");

        centerTexel = SnapToTexelGrid(manager.AreaCenter);
        pendingClear = true;
        pendingScroll = false;

        WriteGlobals();
    }

    void OnDisable()
    {
        Release(ref cascade);
        Release(ref cascadeTemp);
    }

    static RenderTexture Create(string bufferName)
    {
        var rt = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.RGHalf)
        {
            name = bufferName,
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
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

    /// KASKAD BLOK IZGARASINA SNAP'LENİYOR, tek teksele değil.
    ///
    /// Kalıcılık 32 tekselliğine (6 m) bloklar hâlinde saklanıyor. Tek teksel
    /// kaydırmada blok sınırı hiç hizalanmaz ve geri yükleme bir bloğun 31 tekselini
    /// eski veriyle ezerdi. Blok adımıyla kaydırma tam örtüşüyor.
    Vector2Int SnapToTexelGrid(Vector2 worldXZ)
    {
        int block = SnowPersistence.BlockSizeTexels;
        float step = block * TexelSize;

        return new Vector2Int(Mathf.FloorToInt(worldXZ.x / step) * block,
                              Mathf.FloorToInt(worldXZ.y / step) * block);
    }

    void LateUpdate()
    {
        if (cascade == null) return;

        // GERİ YÜKLEME BİR KARE GECİKMELİ. Kaydırma CommandBuffer'da, yani çizim
        // sırasında koşuyor; CopyTexture ise anında. Aynı karede yazılsa kaydırmanın
        // altında kalırdı.
        FlushRestores();

        if (persistence != null) persistence.TickCapture(this);

        Vector2Int next = SnapToTexelGrid(manager.AreaCenter);
        if (next == centerTexel) return;

        Vector2Int delta = next - centerTexel;

        pendingScrollTexels = pendingScroll ? pendingScrollTexels + delta : delta;
        pendingScroll = true;

        // Yeni açılan şeridin blokları bir sonraki karede depodan geri yazılacak.
        if (persistence != null) QueueRestores(delta);

        centerTexel = next;
        WriteGlobals();
    }

    /// Kaydırmayla açılan şeride düşen blokları sıraya alır.
    void QueueRestores(Vector2Int delta)
    {
        int block = SnowPersistence.BlockSizeTexels;
        int blocksPerSide = Resolution / block;

        int stepsX = Mathf.Min(Mathf.Abs(delta.x) / block, blocksPerSide);
        int stepsY = Mathf.Min(Mathf.Abs(delta.y) / block, blocksPerSide);

        for (int i = 0; i < stepsX; i++)
        {
            int bx = delta.x > 0 ? blocksPerSide - 1 - i : i;
            for (int by = 0; by < blocksPerSide; by++) pendingRestores.Add(new Vector2Int(bx, by));
        }

        for (int i = 0; i < stepsY; i++)
        {
            int by = delta.y > 0 ? blocksPerSide - 1 - i : i;
            for (int bx = 0; bx < blocksPerSide; bx++) pendingRestores.Add(new Vector2Int(bx, by));
        }
    }

    void FlushRestores()
    {
        if (persistence == null || pendingRestores.Count == 0) return;

        int block = SnowPersistence.BlockSizeTexels;
        Vector2 corner = AreaCenter - Vector2.one * (AreaSize * 0.5f);

        for (int i = 0; i < pendingRestores.Count; i++)
        {
            Vector2Int local = pendingRestores[i];

            var worldXZ = new Vector2(corner.x + local.x * block * TexelSize,
                                      corner.y + local.y * block * TexelSize);

            persistence.RestoreBlock(SnowPersistence.WorldToBlock(worldXZ, TexelSize),
                                     local.x * block, local.y * block);
        }

        pendingRestores.Clear();
    }

    /// Kalıcılığın gezindiği blok. Her karede bir blok saklanıyor.
    public void GetCaptureBlock(int cursor, out Vector2Int blockCoord, out int texelX, out int texelY)
    {
        int block = SnowPersistence.BlockSizeTexels;
        int blocksPerSide = Resolution / block;

        int bx = cursor % blocksPerSide;
        int by = cursor / blocksPerSide % blocksPerSide;

        texelX = bx * block;
        texelY = by * block;

        Vector2 corner = AreaCenter - Vector2.one * (AreaSize * 0.5f);
        var worldXZ = new Vector2(corner.x + texelX * TexelSize, corner.y + texelY * TexelSize);

        blockCoord = SnowPersistence.WorldToBlock(worldXZ, TexelSize);
    }

    public int BlocksPerSide => Resolution / SnowPersistence.BlockSizeTexels;

    void WriteGlobals()
    {
        Vector2 center = AreaCenter;

        Shader.SetGlobalTexture(SnowShaderIDs.CascadeTex, cascade);
        Shader.SetGlobalVector(SnowShaderIDs.CascadeCenter, new Vector4(center.x, center.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.CascadeAreaSize, AreaSize);
    }

    /// SnowManager dağıtımın sonunda çağırıyor: yakın doku o karede güncellenmiş olmalı.
    public void Dispatch(CommandBuffer cmd, RenderTexture nearState)
    {
        ResolveKernels();

        int groups = Mathf.CeilToInt(Resolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.CascadeResolution, Resolution);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DefaultSWE, manager.Settings.DefaultSWE);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DefaultRhoN, manager.Settings.DefaultRhoN);

        if (pendingClear)
        {
            cmd.SetComputeTextureParam(simCompute, clearKernel, SnowShaderIDs.Cascade, cascade);
            cmd.DispatchCompute(simCompute, clearKernel, groups, groups, 1);
            pendingClear = false;
        }

        if (pendingScroll)
        {
            cmd.SetComputeIntParams(simCompute, SnowShaderIDs.CascadeScrollTexels,
                                    pendingScrollTexels.x, pendingScrollTexels.y);
            cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.CascadeSrc, cascade);
            cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.CascadeDst, cascadeTemp);
            cmd.DispatchCompute(simCompute, scrollKernel, groups, groups, 1);

            RenderTexture swap = cascade;
            cascade = cascadeTemp;
            cascadeTemp = swap;

            Shader.SetGlobalTexture(SnowShaderIDs.CascadeTex, cascade);

            pendingScroll = false;
            pendingScrollTexels = Vector2Int.zero;
        }

        if (Time.frameCount % WriteInterval == 0) DispatchWrite(cmd, nearState);
    }

    /// Yakın bölgeyi kaskada indirger. Yakın doku 24 m / 2048; kaskad 192 m / 1024 →
    /// bir kaskad tekseli 16 x 16 yakın tekselin ortalaması.
    void DispatchWrite(CommandBuffer cmd, RenderTexture nearState)
    {
        SnowQualityData q = manager.Settings.QualityData;

        float ratioF = (q.AreaSize / q.Resolution) / TexelSize;
        int ratio = Mathf.Max(1, Mathf.RoundToInt(1f / ratioF));

        int writeSize = q.Resolution / ratio;

        // Yakın bölgenin sol alt köşesi, kaskad tekselinde.
        Vector2 nearCorner = manager.AreaCenter - Vector2.one * (q.AreaSize * 0.5f);
        Vector2 cascadeCorner = AreaCenter - Vector2.one * (AreaSize * 0.5f);

        var origin = new Vector2Int(
            Mathf.RoundToInt((nearCorner.x - cascadeCorner.x) / TexelSize),
            Mathf.RoundToInt((nearCorner.y - cascadeCorner.y) / TexelSize));

        cmd.SetComputeTextureParam(simCompute, writeKernel, SnowShaderIDs.Cascade, cascade);
        cmd.SetComputeTextureParam(simCompute, writeKernel, SnowShaderIDs.State, nearState);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.Resolution, q.Resolution);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.CascadeRatio, ratio);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.CascadeWriteSize, writeSize);
        cmd.SetComputeIntParams(simCompute, SnowShaderIDs.CascadeWriteOrigin, origin.x, origin.y);

        int writeGroups = Mathf.CeilToInt(writeSize / (float)SnowConstants.GroupSize);
        cmd.DispatchCompute(simCompute, writeKernel, writeGroups, writeGroups, 1);
    }

    void ResolveKernels()
    {
        if (clearKernel >= 0) return;

        clearKernel = simCompute.FindKernel("KCascadeClear");
        scrollKernel = simCompute.FindKernel("KCascadeScroll");
        writeKernel = simCompute.FindKernel("KCascadeWrite");
    }
}
