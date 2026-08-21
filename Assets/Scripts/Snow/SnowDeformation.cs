using System;
using UnityEngine;

/// KARDA AÇILAN OLUK. Oyuncuyu izleyen yerel deformasyon tamponunu yönetir.
///
/// AYRIK AYAK İZİ DEĞİL. Derin karda insan bot izi bırakmaz; gövdesiyle karı
/// YARARAK sürekli bir oluk açar ve kar iki yana set olarak yığılır. Her karede
/// kat edilen yol bir doğru parçası olarak süpürülüyor, yani oluk hızdan bağımsız
/// olarak sürekli kalıyor.
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
/// ÇARPIŞMA İLK SÜRÜMDE BAĞLI DEĞİL — bilerek. Oyuncu kendi oluğunun üstünde
/// oluk derinliği kadar (≤ 18 cm) havada kalıyor. Bunun okunup okunmadığı ÖLÇÜLECEK;
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

    /// İz parçasının en kısa boyu, metre. Bundan kısa hareket biriktiriliyor: her
    /// karede dispatch açmak dururken bile GPU'yu meşgul ederdi. 0.15 m, texel'in üç
    /// katı — parçalar üst üste biniyor, oluk kopmuyor.
    const float SegmentDistance = 0.15f;

    /// Oluğun yarı genişliği, metre. Karı yaran şey ayak değil GÖVDE: bacaklar,
    /// kalça, sallanan kollar. Yürüyen bir insanın açtığı iz 0.4-0.45 m geniştir —
    /// 0.56 m denendi, ekranda yol gibi okundu.
    const float TrailHalfWidth = 0.21f;

    /// Azami oluk derinliği, metre. 0.35 denendi ve ÇOK DERİNDİ: ekranda iz değil
    /// kazılmış bir hendek okunuyordu (kullanıcı bildirdi). Yürüyen bir insan sert
    /// kabuklu karda 5-10 cm, gevşek karda baldıra kadar batar; oyunda okunması gereken
    /// şey hendek değil İZ, o yüzden üst uç 0.18 m. Derinlik ayrıca oradaki kar
    /// kalınlığıyla sınırlanıyor.
    const float MaxDepth = 0.18f;

    /// Karın izi kapatma hızı, metre/saniye. Dingin havada pratikte sıfır; yağış ve
    /// rüzgâr açtıkça oluk kapanıyor. 18 cm'lik oluk tam fırtınada ~3 dakikada siliniyor.
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

    Vector3 lastStamp;
    bool hasLastStamp;

    static readonly int DeformTexId = Shader.PropertyToID("_SnowDeformTex");
    static readonly int DeformId = Shader.PropertyToID("_SnowDeform");
    static readonly int DeformTargetId = Shader.PropertyToID("_Deform");
    static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
    static readonly int ResolutionMaskId = Shader.PropertyToID("_ResolutionMask");
    static readonly int TexelSizeId = Shader.PropertyToID("_TexelSize");
    static readonly int StampFromId = Shader.PropertyToID("_StampFrom");
    static readonly int StampToId = Shader.PropertyToID("_StampTo");
    static readonly int StampHalfWidthId = Shader.PropertyToID("_StampHalfWidth");
    static readonly int StampDepthId = Shader.PropertyToID("_StampDepth");
    static readonly int StampOriginId = Shader.PropertyToID("_StampOrigin");
    static readonly int StampSizeId = Shader.PropertyToID("_StampSize");
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
        hasLastStamp = false;
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
        StampTrail(position);
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

    /// İz PARÇA PARÇA süpürülüyor. Biriken hareket `SegmentDistance`'ı geçince o
    /// parça tek dispatch'le basılıyor; parçalar uçlarından bindiği için oluk sürekli.
    void StampTrail(Vector3 position)
    {
        if (!hasLastStamp)
        {
            lastStamp = position;
            hasLastStamp = true;
            return;
        }

        Vector3 delta = position - lastStamp;
        delta.y = 0f;
        if (delta.sqrMagnitude < SegmentDistance * SegmentDistance) return;

        Vector2 from = new(lastStamp.x, lastStamp.z);
        Vector2 to = new(position.x, position.z);

        // DERİNLİK ORADAKİ KARDAN FAZLA OLAMAZ. Çıplak kayada oluk yok; ince örtüde
        // sığ. Parçanın ortası örnekleniyor — parça 0.15 m, kar kalınlığı o mesafede
        // ölçülebilir biçimde değişmiyor.
        Vector3 middle = lastStamp + delta * 0.5f;
        float depth = Mathf.Min(MaxDepth, snow.DepthAt(middle));
        if (depth < 0.01f)
        {
            lastStamp = position;
            return;
        }

        // İŞLENECEK KUTU parçanın kendi sınırlarından çıkıyor: sabit kutu, uzun
        // parçada oluğun ucunu keser, kısa parçada boş texel işler.
        float reach = TrailHalfWidth * 2.2f;
        Vector2 min = Vector2.Min(from, to) - new Vector2(reach, reach);
        Vector2 max = Vector2.Max(from, to) + new Vector2(reach, reach);

        Vector2Int originTexel = new(Mathf.FloorToInt(min.x / TexelSize),
                                     Mathf.FloorToInt(min.y / TexelSize));
        Vector2Int sizeTexel = new(Mathf.CeilToInt(max.x / TexelSize) - originTexel.x + 1,
                                   Mathf.CeilToInt(max.y / TexelSize) - originTexel.y + 1);

        compute.SetTexture(stampKernel, DeformTargetId, deform);
        compute.SetInt(ResolutionId, Resolution);
        compute.SetInt(ResolutionMaskId, Resolution - 1);
        compute.SetFloat(TexelSizeId, TexelSize);
        compute.SetVector(StampFromId, new Vector4(from.x, from.y, 0f, 0f));
        compute.SetVector(StampToId, new Vector4(to.x, to.y, 0f, 0f));
        compute.SetFloat(StampHalfWidthId, TrailHalfWidth);
        compute.SetFloat(StampDepthId, depth);
        compute.SetInts(StampOriginId, originTexel.x, originTexel.y);
        compute.SetInts(StampSizeId, sizeTexel.x, sizeTexel.y);
        compute.Dispatch(stampKernel,
                         Mathf.CeilToInt(sizeTexel.x / 8f), Mathf.CeilToInt(sizeTexel.y / 8f), 1);

        lastStamp = position;
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
