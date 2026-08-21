// ROL: kar durum dokularının sahibi. RT'leri yaratır/serbest bırakır, bölge merkezini
// snap'li takip eder ve bütün simülasyon geçişlerini tek CommandBuffer'a kuyruklar.
// Çağıran: SnowRenderPass (dağıtım), SnowDebugWindow (okur).

using UnityEngine;
using UnityEngine.Rendering;

/// Bir karede yapılacak simülasyon işi. Hangi dokunun okunup hangisine yazılacağı
/// KAYIT ANINDA sabitleniyor; dağıtım sonra koşuyor ama ping-pong kararları CPU'da
/// verilmiş oluyor.
public struct SnowFrameWork
{
    public bool Clear;
    public RenderTexture ClearTarget;

    public bool Scroll;
    public Vector2Int ScrollTexels;
    public RenderTexture ScrollSrc;
    public RenderTexture ScrollDst;

    /// Deformasyon kernel'lerinin yazdığı doku (kaydırmadan sonra, gevşemeden önce).
    public RenderTexture DeformTarget;

    public bool Relax;
    public RenderTexture RelaxSrc;
    public RenderTexture RelaxDst;

    public RenderTexture AccumulateTarget;
}

[DisallowMultipleComponent]
public class SnowManager : MonoBehaviour
{
    // ASSUMPTION: ScriptableRendererFeature bir asset, sahnedeki bir bileşene
    // [SerializeField] ile bağlanamaz. Renderer varlığı ile sahne arasındaki tek köprü
    // statik bir kayıt. Kapsam dar: yalnız render geçişi okur.
    public static SnowManager Active { get; private set; }

    /// TEŞHİS: kar zamanının hızı. 1 = gerçek zaman.
    ///
    /// Birikme saatler mertebesinde bir olay; gerçek zamanda test etmek dakikalarca
    /// beklemek demek. Bu kol §6'nın TAMAMINI aynı oranda hızlandırıyor — yağış,
    /// oturma, erime, ıslaklık — yani model bozulmuyor, yalnız saat hızlı akıyor.
    public static float SimulationSpeed = 1f;

    /// Gevşeme her 2 karede bir koşuyor (§11.1); karşılığında geçen zaman 2 kat.
    const int RelaxInterval = 2;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowSettings settings;
    [SerializeField] ComputeShader simCompute;
    [SerializeField] SnowOcclusionCapture occlusion;
    [SerializeField] SnowGroundHeight groundHeight;
    [SerializeField] SnowWeather weather;
    [SerializeField] SnowDeformerRegistry deformers;

    [Tooltip("Uzak kaskad (Faz 10). Boş bırakılabilir; o zaman clipmap'in dış halkaları " +
             "düz yedeği okur.")]
    [SerializeField] SnowFarCascade farCascade;

    [Tooltip("Bölgenin merkezinde duracak nesne. Normalde oyuncu.")]
    [SerializeField] Transform followTarget;

    RenderTexture state;
    RenderTexture stateTemp;

    ComputeBuffer massOut;
    ComputeBuffer ringSum;

    /// KERNEL'LER GEÇ ÇÖZÜLÜYOR. Kurucuda çözülünce compute o an derlenmemişse
    /// FindKernel -1 döndürür ve o değer önbellekte kalır.
    int clearKernel = -1;
    int scrollKernel = -1;
    int accumulateKernel = -1;
    int clearPendingKernel = -1;
    int compressKernel = -1;
    int ringSumKernel = -1;
    int depositKernel = -1;
    int relaxKernel = -1;

    /// Bölge merkezi, DÜNYA TEKSEL IZGARASINDA tam sayı olarak. Metre cinsinden
    /// tutmak kayma miktarını kesirli yapardı.
    Vector2Int centerTexel;

    /// Snap adımının teksel karşılığı.
    // ASSUMPTION: §2.4 SnapStep = 0.25 m diyor ama 24 m / 2048 = 1.17 cm tekselde
    // 0.25 m tam sayı teksel ETMİYOR (21.33). Kesirli snap, snap yapmamakla aynı
    // belirtiyi üretir. Adım 0.25 m'ye en yakın tam sayı teksele yuvarlanıyor.
    int snapTexels = 1;

    bool pendingClear;
    bool pendingScroll;
    Vector2Int pendingScrollTexels;

    public Vector2Int LastScrollTexels { get; private set; }

    public bool IsReady => state != null && state.IsCreated();
    public SnowSettings Settings => settings;
    public SnowOcclusionCapture Occlusion => occlusion;
    public SnowGroundHeight GroundHeight => groundHeight;
    public SnowWeather Weather => weather;
    public SnowDeformerRegistry Deformers => deformers;
    public SnowFarCascade FarCascade => farCascade;
    public RenderTexture StateTexture => state;
    public int SnapTexels => snapTexels;
    public float TexelSize => settings != null ? settings.QualityData.TexelSize : 0f;
    public Vector2 AreaCenter => (Vector2)centerTexel * TexelSize;
    public Vector2Int CenterTexel => centerTexel;

    void OnEnable()
    {
        if (settings == null)
            throw new System.InvalidOperationException("SnowManager: SnowSettings atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (simCompute == null)
            throw new System.InvalidOperationException("SnowManager: SnowSim.compute atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (followTarget == null)
            throw new System.InvalidOperationException("SnowManager: takip hedefi atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (occlusion == null)
            throw new System.InvalidOperationException("SnowManager: SnowOcclusionCapture atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (groundHeight == null)
            throw new System.InvalidOperationException("SnowManager: SnowGroundHeight atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (weather == null)
            throw new System.InvalidOperationException("SnowManager: SnowWeather atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (deformers == null)
            throw new System.InvalidOperationException("SnowManager: SnowDeformerRegistry atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        Active = this;

        CreateResources();
        SnowQuality.ApplyKeywords(settings.Quality);

        snapTexels = Mathf.Max(1, Mathf.RoundToInt(SnowConstants.SnapStep / TexelSize));
        centerTexel = SnapToTexelGrid(followTarget.position);

        pendingClear = true;
        pendingScroll = false;

        WriteGlobals();
    }

    void OnDisable()
    {
        if (Active == this) Active = null;

        ReleaseTexture(ref state);
        ReleaseTexture(ref stateTemp);

        // Serbest bırakılmazsa Unity uyarı basar ve bu bir hatadır, susturulmaz (§11.3).
        massOut?.Release();
        ringSum?.Release();
        massOut = null;
        ringSum = null;
    }

    void CreateResources()
    {
        SnowQualityData q = settings.QualityData;

        // ARGBFloat, ARGBHalf DEĞİL.
        //
        // Spec §2.1 half diyor ama sonucu kağıtta hesaplayınca sistem çalışmıyor.
        // swe = 0.02 civarında half'in adımı 1.53e-5. Bir birikme adımında eklenen
        // miktar 8.33e-7 m/s x 0.067 s = 5.6e-8, yani adımın 273'te biri: her yazma
        // aynı sayıya geri yuvarlanıyor ve kar HİÇ birikmiyor. Ölçüldü: 90 saniyede
        // swe tam olarak sabit kaldı. Aynı sorun rhoN oturmasında ve wet gevşemesinde.
        state = CreateBuffer("RT_State", q.Resolution);
        stateTemp = CreateBuffer("RT_StateTemp", q.Resolution);

        int capacity = q.MaxDeformers;
        massOut = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
        ringSum = new ComputeBuffer(capacity, sizeof(uint), ComputeBufferType.Structured);
    }

    static RenderTexture CreateBuffer(string bufferName, int resolution)
    {
        var rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGBFloat)
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

    static void ReleaseTexture(ref RenderTexture rt)
    {
        if (rt == null) return;

        rt.Release();
        DestroyImmediate(rt);
        rt = null;
    }

    /// Dünya konumunu global teksel ızgarasına snap'ler. Izgara dünya orijinine
    /// çapalı: aynı dünya noktası her zaman aynı teksele düşer.
    Vector2Int SnapToTexelGrid(Vector3 worldPos)
    {
        float snapWorld = snapTexels * TexelSize;

        int x = Mathf.FloorToInt(worldPos.x / snapWorld) * snapTexels;
        int y = Mathf.FloorToInt(worldPos.z / snapWorld) * snapTexels;

        return new Vector2Int(x, y);
    }

    void LateUpdate()
    {
        if (!IsReady) return;

        UpdateRegion();

        // GLOBALLER HER KAREDE YAZILIYOR, yalnız merkez kayınca değil.
        //
        // Aynı nesnedeki bileşenlerin OnEnable sırası eklenme sırasına bağlı ve
        // SnowManager ilk sırada: zemin yükseklik dokusu OnEnable anında henüz
        // pişmemiş oluyor ve global null kalıyordu. On iki SetGlobal çağrısının
        // maliyeti ölçülemez; sıra bağımlılığının bedeli bir kare siyah zemin.
        WriteGlobals();

        // Engel haritası KENDİ eşiğine bakıyor (4 m); çoğu karede erken çıkıyor.
        Vector2 center = AreaCenter;
        occlusion.UpdateCapture(new Vector3(center.x, followTarget.position.y, center.y));

        // Deformer'lar dağıtımdan ÖNCE toplanıyor: tampon bu karenin temaslarını
        // taşımalı, bir öncekini değil.
        deformers.Collect();
    }

    /// Yıkılabilir/açılıp kapanan nesneler bunu çağırır (§4.2).
    public void MarkOcclusionDirty() => occlusion.MarkDirty();

    void UpdateRegion()
    {
        Vector2Int next = SnapToTexelGrid(followTarget.position);
        if (next == centerTexel) return;

        // Teksel id'si merkez +delta kadar kayınca aynı dünya içeriği kaynakta
        // +delta teksel ötede kalır. KScroll bu yüzden src = id + _ScrollTexels okuyor.
        Vector2Int delta = next - centerTexel;

        pendingScrollTexels = pendingScroll ? pendingScrollTexels + delta : delta;
        pendingScroll = true;

        centerTexel = next;
        WriteGlobals();
    }

    void WriteGlobals()
    {
        SnowQualityData q = settings.QualityData;
        Vector2 center = AreaCenter;

        Shader.SetGlobalVector(SnowShaderIDs.SnowAreaCenter, new Vector4(center.x, center.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.SnowAreaSize, q.AreaSize);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowResolution, q.Resolution);
        Shader.SetGlobalTexture(SnowShaderIDs.SnowStateTex, state);

        // Bölge dışında deformasyon yok ama kar var (§7.2).
        Shader.SetGlobalFloat(SnowShaderIDs.FallbackSWE, settings.DefaultSWE);
        Shader.SetGlobalFloat(SnowShaderIDs.FallbackRhoN, settings.DefaultRhoN);

        // Zemin yüksekliği — fragment shader'ları için global (§3).
        Shader.SetGlobalTexture(SnowShaderIDs.GroundHeightTex, groundHeight.HeightTexture);
        Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ,
            new Vector4(groundHeight.OriginXZ.x, groundHeight.OriginXZ.y, 0f, 0f));
        Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ,
            new Vector4(groundHeight.SizeXZ.x, groundHeight.SizeXZ.y, 0f, 0f));
        Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, groundHeight.BaseY);
        Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, groundHeight.HeightRange);
        Shader.SetGlobalVector(SnowShaderIDs.GroundHeightUV,
            new Vector4(groundHeight.HeightUV.x, groundHeight.HeightUV.y, 0f, 0f));
    }

    /// Render geçişi bunu çağırır. İşi TÜKETİR: aynı karede ikinci bir kamera
    /// çağırırsa iş kalmaz, simülasyon karede bir kez koşar.
    public bool BeginFrameWork(out SnowFrameWork work)
    {
        work = default;
        if (!IsReady) return false;

        work.Clear = pendingClear;
        work.ClearTarget = state;

        work.Scroll = pendingScroll;
        work.ScrollTexels = pendingScrollTexels;
        work.ScrollSrc = state;
        work.ScrollDst = stateTemp;

        if (pendingScroll)
        {
            LastScrollTexels = pendingScrollTexels;
            Swap();
        }

        work.DeformTarget = state;

        // GEVŞEME KOMŞU OKUYOR, bu yüzden ping-pong şart: yerinde yazsaydı bir teksel
        // güncellenmiş komşusunu okur ve kütle korunmazdı.
        work.Relax = Time.frameCount % RelaxInterval == 0;

        if (work.Relax)
        {
            work.RelaxSrc = state;
            work.RelaxDst = stateTemp;
            Swap();
        }

        work.AccumulateTarget = state;

        pendingClear = false;
        pendingScroll = false;
        pendingScrollTexels = Vector2Int.zero;

        return true;
    }

    void Swap()
    {
        RenderTexture swap = state;
        state = stateTemp;
        stateTemp = swap;

        Shader.SetGlobalTexture(SnowShaderIDs.SnowStateTex, state);
    }

    /// Bütün geçişleri tek CommandBuffer'a kuyruklar. Graphics.ExecuteCommandBuffer
    /// çağrılmaz (§11.1).
    public void Dispatch(CommandBuffer cmd, in SnowFrameWork work)
    {
        ResolveKernels();

        SnowQualityData q = settings.QualityData;
        int groups = Mathf.CeilToInt(q.Resolution / (float)SnowConstants.GroupSize);

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.Resolution, q.Resolution);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DefaultSWE, settings.DefaultSWE);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DefaultRhoN, settings.DefaultRhoN);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DefaultWet, settings.DefaultWet);

        BindRegion(cmd, q);

        if (work.Clear)
        {
            // RT içeriği yaratıldıktan sonra TANIMSIZ; temizlenmezse ilk karelerde
            // çöp okunur.
            cmd.SetComputeTextureParam(simCompute, clearKernel, SnowShaderIDs.Dst, work.ClearTarget);
            cmd.DispatchCompute(simCompute, clearKernel, groups, groups, 1);
        }

        if (work.Scroll)
        {
            // ÖRNEK ADLARI SnowProfiler.PassNames İLE BİREBİR AYNI olmak zorunda;
            // Recorder adını buradan eşliyor.
            cmd.BeginSample(SnowProfiler.PassNames[0]);

            cmd.SetComputeIntParams(simCompute, SnowShaderIDs.ScrollTexels,
                                    work.ScrollTexels.x, work.ScrollTexels.y);
            cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.Src, work.ScrollSrc);
            cmd.SetComputeTextureParam(simCompute, scrollKernel, SnowShaderIDs.Dst, work.ScrollDst);
            cmd.DispatchCompute(simCompute, scrollKernel, groups, groups, 1);

            cmd.EndSample(SnowProfiler.PassNames[0]);
        }

        cmd.BeginSample(SnowProfiler.PassNames[1]);
        DispatchDeformation(cmd, work.DeformTarget);
        cmd.EndSample(SnowProfiler.PassNames[1]);

        if (work.Relax)
        {
            cmd.BeginSample(SnowProfiler.PassNames[2]);
            DispatchRelax(cmd, work, groups);
            cmd.EndSample(SnowProfiler.PassNames[2]);
        }

        cmd.BeginSample(SnowProfiler.PassNames[3]);
        DispatchAccumulate(cmd, work.AccumulateTarget, q, groups);
        cmd.EndSample(SnowProfiler.PassNames[3]);

        if (farCascade != null)
        {
            // KASKAD EN SONDA: yakın doku o karede güncellenmiş olmalı, yoksa kaskad
            // bir kare eski karı saklar ve iz sınırda kayar.
            cmd.BeginSample(SnowProfiler.PassNames[4]);
            farCascade.Dispatch(cmd, work.AccumulateTarget);
            cmd.EndSample(SnowProfiler.PassNames[4]);
        }
    }

    void ResolveKernels()
    {
        if (clearKernel >= 0) return;

        clearKernel = simCompute.FindKernel("KClear");
        scrollKernel = simCompute.FindKernel("KScroll");
        accumulateKernel = simCompute.FindKernel("KAccumulate");
        clearPendingKernel = simCompute.FindKernel("KClearPending");
        compressKernel = simCompute.FindKernel("KCompress");
        ringSumKernel = simCompute.FindKernel("KRingSum");
        depositKernel = simCompute.FindKernel("KDeposit");
        relaxKernel = simCompute.FindKernel("KRelax");
    }

    void BindRegion(CommandBuffer cmd, SnowQualityData q)
    {
        Vector2 center = AreaCenter;

        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.SnowAreaCenter,
                                  new Vector4(center.x, center.y, 0f, 0f));
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowAreaSize, q.AreaSize);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowResolution, q.Resolution);
    }

    /// SIKIŞMA → HALKA TOPLAMI → MEVDUAT. Sıra zorunlu: mevduat kütleyi halka
    /// ağırlıklarının toplamına bölüyor, toplam önce hazır olmalı (§5.5).
    void DispatchDeformation(CommandBuffer cmd, RenderTexture target)
    {
        int count = deformers.ActiveCount;
        if (count == 0 || settings.StampAtlas == null) return;

        // Tarama kutusu en büyük temas kutusunun KÖŞEGENİNİ kapsamalı: damga
        // deformer'ın yerel uzayında dönüyor.
        int boxTexels = Mathf.CeilToInt(deformers.MaxContactExtent * 1.15f / TexelSize);
        boxTexels = Mathf.Max(SnowConstants.GroupSize,
                              Mathf.CeilToInt(boxTexels / (float)SnowConstants.GroupSize)
                              * SnowConstants.GroupSize);

        int boxGroups = boxTexels / SnowConstants.GroupSize;

        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.DeformerCount, count);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.DeformerBoxTexels, boxTexels);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.RimVelocityBias, settings.RimVelocityBias);

        int clearGroups = Mathf.CeilToInt(count / 64f);
        cmd.SetComputeBufferParam(simCompute, clearPendingKernel, SnowShaderIDs.DeformerMassOut, massOut);
        cmd.SetComputeBufferParam(simCompute, clearPendingKernel, SnowShaderIDs.DeformerRingSum, ringSum);
        cmd.DispatchCompute(simCompute, clearPendingKernel, clearGroups, 1, 1);

        BindDeformKernel(cmd, compressKernel, target);
        cmd.DispatchCompute(simCompute, compressKernel, boxGroups, boxGroups, count);

        BindDeformKernel(cmd, ringSumKernel, target);
        cmd.DispatchCompute(simCompute, ringSumKernel, boxGroups, boxGroups, count);

        BindDeformKernel(cmd, depositKernel, target);
        cmd.DispatchCompute(simCompute, depositKernel, boxGroups, boxGroups, count);
    }

    void BindDeformKernel(CommandBuffer cmd, int kernel, RenderTexture target)
    {
        cmd.SetComputeTextureParam(simCompute, kernel, SnowShaderIDs.State, target);
        cmd.SetComputeTextureParam(simCompute, kernel, SnowShaderIDs.StampAtlas, settings.StampAtlas);
        cmd.SetComputeBufferParam(simCompute, kernel, SnowShaderIDs.Deformers, deformers.Buffer);
        cmd.SetComputeBufferParam(simCompute, kernel, SnowShaderIDs.DeformerMassOut, massOut);
        cmd.SetComputeBufferParam(simCompute, kernel, SnowShaderIDs.DeformerRingSum, ringSum);
    }

    void DispatchRelax(CommandBuffer cmd, in SnowFrameWork work, int groups)
    {
        cmd.SetComputeTextureParam(simCompute, relaxKernel, SnowShaderIDs.RelaxSrc, work.RelaxSrc);
        cmd.SetComputeTextureParam(simCompute, relaxKernel, SnowShaderIDs.RelaxDst, work.RelaxDst);

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.ReposeTan, SnowConstants.ReposeTan);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.RelaxRate, settings.RelaxRate);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.WindSpeed, weather.WindSpeed);

        // Rüzgâr yüksekken erken çıkış kapanıyor: bozulmamış yüzey de akıyor.
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.ForceRelaxAll,
                               weather.WindSpeed > settings.ForceRelaxWindSpeed ? 1 : 0);

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DeltaTimeEff,
                                 Time.deltaTime * RelaxInterval * Mathf.Max(SimulationSpeed, 0f));

        cmd.DispatchCompute(simCompute, relaxKernel, groups, groups, 1);
    }

    /// Birikme, oturma, erime. Dokunun 1/4'ü her karede (§11.1 tile rotasyonu);
    /// karşılığında geçen zaman 4 kat sayılıyor.
    void DispatchAccumulate(CommandBuffer cmd, RenderTexture target, SnowQualityData q, int groups)
    {
        // Zemin dokusu henüz pişmemişse birikme atlanıyor: null doku bağlamak
        // Unity hatası basar ve kernel çöpsüç okur.
        if (groundHeight.HeightTexture == null) return;

        int tiles = Mathf.Max(1, q.AccumulateTiles);
        int tileWidth = q.Resolution / tiles;
        int tileIndex = Time.frameCount % tiles;

        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.State, target);
        cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.GroundHeightTex,
                                   groundHeight.HeightTexture);

        // ENGEL HARİTASI açıkça bağlanıyor: compute dokuları Shader.SetGlobalTexture
        // ile gelmiyor, kernel başına bağlanmak zorunda.
        if (occlusion.OcclusionTexture != null)
            cmd.SetComputeTextureParam(simCompute, accumulateKernel, SnowShaderIDs.SnowOcclusionTex,
                                       occlusion.OcclusionTexture);

        Vector2 occlCenter = occlusion.LastCaptureCenter;
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.OcclCenterXZ,
                                  new Vector4(occlCenter.x, occlCenter.y, 0f, 0f));
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.OcclAreaSize, SnowConstants.OcclusionArea);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.OcclResolution, q.OcclusionResolution);

        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.GroundOriginXZ,
                                  new Vector4(groundHeight.OriginXZ.x, groundHeight.OriginXZ.y, 0f, 0f));
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.GroundSizeXZ,
                                  new Vector4(groundHeight.SizeXZ.x, groundHeight.SizeXZ.y, 0f, 0f));
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.GroundBaseY, groundHeight.BaseY);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.GroundHeightRange, groundHeight.HeightRange);
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.GroundHeightUV,
                                  new Vector4(groundHeight.HeightUV.x, groundHeight.HeightUV.y, 0f, 0f));

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DeltaTimeEff,
                                 Time.deltaTime * tiles * Mathf.Max(SimulationSpeed, 0f));
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TileIndex, tileIndex);
        cmd.SetComputeIntParam(simCompute, SnowShaderIDs.TileWidth, tileWidth);

        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowfallSWERate, weather.SnowfallSWERate);
        cmd.SetComputeVectorParam(simCompute, SnowShaderIDs.WindWS, weather.WindWS);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.WindSpeed, weather.WindSpeed);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SnowWetness, weather.SnowWetness);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.TemperatureC, weather.TemperatureC);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DriftBias, settings.DriftBias);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.SettleTau, SnowConstants.SettleTau);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.DisturbTau, SnowConstants.DisturbTau);
        cmd.SetComputeFloatParam(simCompute, SnowShaderIDs.MeltDDF, SnowConstants.MeltDDF);

        int tileGroupsX = Mathf.CeilToInt(tileWidth / (float)SnowConstants.GroupSize);
        cmd.DispatchCompute(simCompute, accumulateKernel, tileGroupsX, groups, 1);
    }
}
