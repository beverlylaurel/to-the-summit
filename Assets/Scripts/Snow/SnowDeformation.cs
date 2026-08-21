using System;
using UnityEngine;

/// KARDA AYAK İZİ. Oyuncuyu izleyen yerel deformasyon tamponunu yönetir.
///
/// NEDEN KAR ÖRTÜSÜ SİMÜLASYONUNDAN AYRI. Örtü simülasyonunun hücresi arazi
/// ızgarasıyla aynı olacak (7.32 m); ayak izi 0.3 m, yani hücrenin yirmi dörtte biri.
/// `[Cordonnier 2018, §6.2]` bunu kendisi söylüyor: "10m per cell only allows a
/// consideration of the general direction of the skiers". İz o ızgaraya yazılamaz.
///
/// DOKU DÜNYAYA DÖŞENİYOR. `uv = worldPos.xz / Extent`, doku Repeat sarımlı. Oyuncu
/// yürürken kopyalama yok; yalnız pencereye YENİ GİREN texel şeritleri sıfırlanıyor.
/// Kaydır-kopyala yöntemi her karede bir tam doku kopyası demekti.
///
/// Pencere `Extent` kenarlı kare, görünür bölge yarıçapı `Extent/2` olan çember.
/// Çember kareye içten teğet: döşemenin komşu kopyası hiçbir zaman görünmüyor.
///
/// ÇARPIŞMA İLK SÜRÜMDE BAĞLI DEĞİL — bilerek. Oyuncu kendi izinin üstünde iz
/// derinliği kadar (≤ 12 cm) havada kalıyor. Bunun okunup okunmadığı ÖLÇÜLECEK;
/// okunuyorsa CPU tarafı eklenir. Peşinen ikinci bir CPU/GPU ikizi yazmak `SnowDrift`
/// borcunu ikiye katlardı (bkz. `SnowDriftField` başlığı).
public class SnowDeformation : MonoBehaviour
{
    /// Pencere kenarı, metre. Görünür yarıçap bunun yarısı.
    const float Extent = 24f;

    /// 512 texel / 24 m = 4.7 cm. Ayak izi 0.3 m, yani iz altı yedi texel geniş —
    /// biçimi taşımaya yeter. 1024'e çıkmak 2.3 cm verirdi ama bölünmüş geometri
    /// zaten 11.4 cm'de kalıyor, doku ondan ince olması boşa gider.
    const int Resolution = 512;

    const float TexelSize = Extent / Resolution;

    /// Adım aralığı, metre. İnsan adımı 0.65-0.80 m; yürüyüş hızından bağımsız
    /// olması için MESAFEYE bağlı, zamana değil — koşarken izler seyrelmiyor.
    const float StepDistance = 0.72f;

    /// Ayak izinin ölçüleri, metre. Dağ botu 0.31 × 0.12; iz kardaki çökme olduğu
    /// için tabandan biraz geniş.
    const float FootLength = 0.34f;
    const float FootWidth = 0.15f;

    /// İz ekseninden yana kayma, metre. İki ayak arası genişlik.
    const float Stride = 0.11f;

    /// Azami iz derinliği, metre. Bundan derini için kar da yetmiyor: derinlik
    /// oradaki kar kalınlığıyla ayrıca sınırlanıyor.
    const float MaxDepth = 0.12f;

    /// Karın izi kapatma hızı, metre/saniye. Dingin havada pratikte sıfır; yağış ve
    /// rüzgâr açtıkça iz kapanıyor. 12 cm'lik iz tam fırtınada ~2 dakikada siliniyor.
    const float RefillCalm = 0.0f;
    const float RefillStorm = 0.001f;

    [Tooltip("Deformasyon compute shader'ı.")]
    [SerializeField] ComputeShader compute;
    [Tooltip("İzi bırakan gövde. Konumu ve yere basıp basmadığı buradan.")]
    [SerializeField] Transform walker;
    [Tooltip("Kar kalınlığı. İz derinliği oradaki kardan fazla olamaz.")]
    [SerializeField] SnowSurface snow;
    [Tooltip("Yağış şiddeti — izin kapanma hızını sürüyor.")]
    [SerializeField] WeatherState weather;
    [Tooltip("Rüzgâr — izin kapanma hızını sürüyor.")]
    [SerializeField] WindField wind;

    public void Bind(ComputeShader computeRef, Transform walkerRef, SnowSurface snowRef,
                     WeatherState weatherRef, WindField windRef)
    {
        compute = computeRef;
        walker = walkerRef;
        snow = snowRef;
        weather = weatherRef;
        wind = windRef;
    }

    RenderTexture deform;
    int stampKernel, clearKernel, refillKernel;

    /// Pencerenin sol-alt köşesinin MUTLAK texel indeksi. Şerit temizleme bunun
    /// değişiminden çıkıyor.
    Vector2Int windowMin;
    bool windowValid;

    Vector3 lastStep;
    bool hasLastStep;
    bool rightFoot;

    static readonly int DeformTexId = Shader.PropertyToID("_SnowDeformTex");
    static readonly int DeformId = Shader.PropertyToID("_SnowDeform");
    static readonly int DeformTargetId = Shader.PropertyToID("_Deform");
    static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
    static readonly int ResolutionMaskId = Shader.PropertyToID("_ResolutionMask");
    static readonly int TexelSizeId = Shader.PropertyToID("_TexelSize");
    static readonly int StampCenterId = Shader.PropertyToID("_StampCenter");
    static readonly int StampAxisId = Shader.PropertyToID("_StampAxis");
    static readonly int StampLengthId = Shader.PropertyToID("_StampLength");
    static readonly int StampWidthId = Shader.PropertyToID("_StampWidth");
    static readonly int StampDepthId = Shader.PropertyToID("_StampDepth");
    static readonly int ClearOriginId = Shader.PropertyToID("_ClearOrigin");
    static readonly int ClearSizeId = Shader.PropertyToID("_ClearSize");
    static readonly int RefillAmountId = Shader.PropertyToID("_RefillAmount");

    void OnEnable()
    {
        if (compute == null || walker == null || snow == null || weather == null || wind == null)
            throw new InvalidOperationException($"{nameof(SnowDeformation)}: bağımlılıklar atanmadı.");

        // Toroidal sarma compute tarafında BİT MASKESİYLE yapılıyor; kuvvet
        // olmayan çözünürlük orada sessizce yanlış yuva verirdi.
        if ((Resolution & (Resolution - 1)) != 0)
            throw new InvalidOperationException(
                $"{nameof(SnowDeformation)}: çözünürlük ikinin kuvveti olmalı ({Resolution}).");

        stampKernel = compute.FindKernel("Stamp");
        clearKernel = compute.FindKernel("ClearStrip");
        refillKernel = compute.FindKernel("Refill");

        // RFloat DEĞİL RHalf: değer aralığı ±0.12 m ve texel başına 2 bayt yeterli
        // (yarım hassasiyet o aralıkta ~6e-5 m çözüyor, izin binde biri). RFloat
        // tamponu 1 MB'den 0.5 MB'ye iniyor.
        deform = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.RHalf)
        {
            name = "Snow Deformation",
            enableRandomWrite = true,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
            useMipMap = false,
        };
        deform.Create();

        windowValid = false;
        hasLastStep = false;
    }

    void OnDisable()
    {
        if (deform == null) return;
        deform.Release();
        Destroy(deform);
        deform = null;
    }

    void Update()
    {
        Vector3 position = walker.position;
        Vector2 center = new(position.x, position.z);

        AdvanceWindow(center);
        StampSteps(position);
        RefillStep();

        Shader.SetGlobalTexture(DeformTexId, deform);
        Shader.SetGlobalVector(DeformId,
            new Vector4(Extent, center.x, center.y, Extent * 0.5f));
    }

    /// Pencere oyuncuyla kayıyor. Kopyalama yok: dünyaya döşenmiş dokuda pencereye
    /// YENİ GİREN texel'ler eskiden pencerenin arka ucundaki veriyi taşıyor, o yüzden
    /// yalnız o şeritler sıfırlanıyor.
    void AdvanceWindow(Vector2 center)
    {
        Vector2 corner = center - new Vector2(Extent, Extent) * 0.5f;
        Vector2Int next = new(Mathf.FloorToInt(corner.x / TexelSize),
                              Mathf.FloorToInt(corner.y / TexelSize));

        if (!windowValid)
        {
            // İlk kare: pencerenin tamamı bilinmeyen veri taşıyor.
            ClearRegion(next, new Vector2Int(Resolution, Resolution));
            windowMin = next;
            windowValid = true;
            return;
        }

        int dx = next.x - windowMin.x;
        int dy = next.y - windowMin.y;

        // Bir karede pencere boyu kadar yol alındıysa (ışınlanma, sahne yükleme)
        // şerit hesabı anlamsız — tamamı sıfırlanıyor.
        if (Mathf.Abs(dx) >= Resolution || Mathf.Abs(dy) >= Resolution)
        {
            ClearRegion(next, new Vector2Int(Resolution, Resolution));
            windowMin = next;
            return;
        }

        if (dx > 0)
            ClearRegion(new Vector2Int(windowMin.x + Resolution, windowMin.y),
                        new Vector2Int(dx, Resolution));
        else if (dx < 0)
            ClearRegion(new Vector2Int(next.x, windowMin.y),
                        new Vector2Int(-dx, Resolution));

        if (dy > 0)
            ClearRegion(new Vector2Int(next.x, windowMin.y + Resolution),
                        new Vector2Int(Resolution, dy));
        else if (dy < 0)
            ClearRegion(new Vector2Int(next.x, next.y),
                        new Vector2Int(Resolution, -dy));

        windowMin = next;
    }

    void ClearRegion(Vector2Int origin, Vector2Int size)
    {
        if (size.x <= 0 || size.y <= 0) return;

        compute.SetTexture(clearKernel, DeformTargetId, deform);
        compute.SetInt(ResolutionId, Resolution);
        compute.SetInt(ResolutionMaskId, Resolution - 1);
        compute.SetInts(ClearOriginId, origin.x, origin.y);
        compute.SetInts(ClearSizeId, size.x, size.y);
        compute.Dispatch(clearKernel,
                         Mathf.CeilToInt(size.x / 8f), Mathf.CeilToInt(size.y / 8f), 1);
    }

    /// Adımlar MESAFEYE göre atılıyor. Zamana bağlansaydı koşarken izler seyrelir,
    /// dururken üst üste binerdi.
    void StampSteps(Vector3 position)
    {
        if (!hasLastStep)
        {
            lastStep = position;
            hasLastStep = true;
            return;
        }

        Vector3 delta = position - lastStep;
        delta.y = 0f;

        float travelled = delta.magnitude;
        if (travelled < StepDistance) return;

        Vector2 axis = new Vector2(delta.x, delta.z) / travelled;

        // Kaç adım atlandıysa hepsi basılıyor: kare düşerse iz zinciri kopmamalı.
        int steps = Mathf.Min(Mathf.FloorToInt(travelled / StepDistance), 8);
        for (int i = 1; i <= steps; i++)
        {
            float t = i * StepDistance / travelled;
            Vector3 foot = lastStep + delta * t;

            rightFoot = !rightFoot;
            Vector2 side = new(-axis.y, axis.x);
            Vector2 at = new Vector2(foot.x, foot.z) + side * (rightFoot ? Stride : -Stride);

            Stamp(at, axis, new Vector3(at.x, foot.y, at.y));
        }

        lastStep += delta * (steps * StepDistance / travelled);
    }

    void Stamp(Vector2 at, Vector2 axis, Vector3 sample)
    {
        // DERİNLİK ORADAKİ KARDAN FAZLA OLAMAZ. Çıplak kayada iz yok; ince örtüde
        // sığ iz. `DepthAt` görsel yüzeyle aynı hesabı yapıyor.
        float available = snow.DepthAt(sample);
        float depth = Mathf.Min(MaxDepth, available);
        if (depth < 0.01f) return;

        compute.SetTexture(stampKernel, DeformTargetId, deform);
        compute.SetInt(ResolutionId, Resolution);
        compute.SetInt(ResolutionMaskId, Resolution - 1);
        compute.SetFloat(TexelSizeId, TexelSize);
        compute.SetVector(StampCenterId, new Vector4(at.x, at.y, 0f, 0f));
        compute.SetVector(StampAxisId, new Vector4(axis.x, axis.y, 0f, 0f));
        compute.SetFloat(StampLengthId, FootLength);
        compute.SetFloat(StampWidthId, FootWidth);
        compute.SetFloat(StampDepthId, depth);

        // Damga kutusu compute içindeki `reach` ile aynı: en uzun eksenin 1.5 katı,
        // iki yana.
        int half = Mathf.CeilToInt(Mathf.Max(FootLength, FootWidth) * 1.5f / TexelSize);
        int side = half * 2;
        compute.Dispatch(stampKernel, Mathf.CeilToInt(side / 8f), Mathf.CeilToInt(side / 8f), 1);
    }

    /// İzin kapanması yağıştan ve rüzgârdan geliyor — ayrı bir zamanlayıcı yok.
    /// Kar örtüsü simülasyonu gelince kaynağı gerçek rüzgâr taşınımı olacak; bu
    /// fonksiyon o zaman girdisini değiştirir, sistem değişmez.
    void RefillStep()
    {
        float driven = Mathf.Max(weather.Precipitation * weather.Snowiness, wind.Strength * 0.5f);
        float rate = Mathf.Lerp(RefillCalm, RefillStorm, Mathf.Clamp01(driven));
        if (rate <= 0f) return;

        compute.SetTexture(refillKernel, DeformTargetId, deform);
        compute.SetInt(ResolutionId, Resolution);
        compute.SetInt(ResolutionMaskId, Resolution - 1);
        compute.SetFloat(RefillAmountId, rate * Time.deltaTime);
        compute.Dispatch(refillKernel, Resolution / 8, Resolution / 8, 1);
    }
}
