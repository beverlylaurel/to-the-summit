// ROL: kar durumunu CPU tarafına verir. Oyuncunun çevresindeki küçük bir
// bölgeyi bloklamadan geri okur (spec §19).
// Çağıran: SnowFootstepAudio, SnowMovementModifier, SnowSprayController.

using UnityEngine;
using UnityEngine.Rendering;

/// Bir noktadaki karın oyun tarafına görünen hâli (spec §19).
public struct SnowSample
{
    /// Kar yüzeyinin zeminden yüksekliği (m). İZ AÇILDIKTAN SONRAKİ hâli.
    public float Depth;

    /// İZ AÇILMADAN ÖNCEKİ kar sütunu (m). Yalnız yağış ve oturmayla değişir,
    /// yani saniyeler ölçeğinde sabittir.
    ///
    /// `Depth + SinkDepth` bunun yerine kullanılıyordu ve `trail.g`'yi (sırt)
    /// içeriyordu. Sırt `max` ile birikiyor ve konumu hız yönüne göre
    /// kaydırılıyor; gövdenin oturma yüksekliği onun peşinden 4 cm zıplayıp
    /// izin derinliğini basamaklandırıyordu.
    public float BaseHeight;

    /// Bu noktadaki oyulma (m) — batma derinliği.
    public float SinkDepth;

    /// 0 = toz, 1 = buz gibi.
    public float Density01;

    public float Wetness;

    /// Yüzey kabuğunun sağlamlığı (spec §18.3). `RT_Trail.B`.
    public float Crust;

    public bool Valid;
}

/// BLOKLAMAYAN OKUMA (spec §19). `GetData` çağırmak GPU'yu bekletiyor ve
/// kare süresini ikiye katlıyor; `AsyncGPUReadback` iki kare gecikmeyle
/// aynı veriyi veriyor. Ayak sesi ve hız çarpanı için iki kare fark etmez.
[DisallowMultipleComponent]
public class SnowSampler : MonoBehaviour
{
    /// Okunan pencerenin kenarı, teksel (spec §19).
    const int Window = 64;

    /// Kaç karede bir istek (spec §19).
    const int Interval = 4;

    [SerializeField] SnowManager manager;

    [Tooltip("Pencerenin merkezinde duracak hedef — genelde oyuncu.")]
    [SerializeField] Transform followTarget;

    readonly Color[] snowCache = new Color[Window * Window];
    readonly Color[] trailCache = new Color[Window * Window];

    Vector2Int windowOrigin;
    Vector2 cachedAreaCenter;
    float cachedTexelSize;
    float cachedAreaSize;
    int cachedResolution;

    bool snowReady;
    bool trailReady;
    bool snowPending;
    bool trailPending;

    int lastRequestFrame = -Interval;

    public bool HasData => snowReady && trailReady;

    void OnEnable()
    {
        if (manager == null)
            throw new System.InvalidOperationException($"{nameof(SnowSampler)}: {nameof(manager)} atanmadı.");
        if (followTarget == null)
            throw new System.InvalidOperationException($"{nameof(SnowSampler)}: takip hedefi atanmadı.");

        snowReady = false;
        trailReady = false;
        snowPending = false;
        trailPending = false;
    }

    void LateUpdate()
    {
        if (!manager.IsReady) return;
        if (snowPending || trailPending) return;
        if (Time.frameCount - lastRequestFrame < Interval) return;

        lastRequestFrame = Time.frameCount;

        SnowQualityData q = manager.Settings.QualityData;

        cachedResolution = q.Resolution;
        cachedAreaSize = q.AreaSize;
        cachedTexelSize = manager.TexelSize;
        cachedAreaCenter = manager.AreaCenter;

        windowOrigin = WindowOrigin(followTarget.position, cachedAreaCenter,
                                    cachedAreaSize, cachedResolution);

        snowPending = true;
        trailPending = true;

        AsyncGPUReadback.Request(manager.SnowTexture, 0,
            windowOrigin.x, Window, windowOrigin.y, Window, 0, 1,
            TextureFormat.RGBAFloat, OnSnowRead);

        AsyncGPUReadback.Request(manager.TrailTexture, 0,
            windowOrigin.x, Window, windowOrigin.y, Window, 0, 1,
            TextureFormat.RGBAFloat, OnTrailRead);
    }

    /// Pencerenin sol alt köşesi, teksel. Doku sınırlarının dışına taşmıyor.
    public static Vector2Int WindowOrigin(Vector3 worldPos, Vector2 areaCenter,
                                          float areaSize, int resolution)
    {
        Vector2 uv = (new Vector2(worldPos.x, worldPos.z) - areaCenter) / areaSize
                     + new Vector2(0.5f, 0.5f);

        int cx = Mathf.RoundToInt(uv.x * resolution) - Window / 2;
        int cy = Mathf.RoundToInt(uv.y * resolution) - Window / 2;

        return new Vector2Int(Mathf.Clamp(cx, 0, resolution - Window),
                              Mathf.Clamp(cy, 0, resolution - Window));
    }

    void OnSnowRead(AsyncGPUReadbackRequest request)
    {
        snowPending = false;
        if (request.hasError) return;

        request.GetData<Color>().CopyTo(snowCache);
        snowReady = true;
    }

    void OnTrailRead(AsyncGPUReadbackRequest request)
    {
        trailPending = false;
        if (request.hasError) return;

        request.GetData<Color>().CopyTo(trailCache);
        trailReady = true;
    }

    public bool TrySampleSnow(Vector3 worldPos, out SnowSample sample)
    {
        sample = default;

        if (!HasData) return false;

        Vector2 uv = (new Vector2(worldPos.x, worldPos.z) - cachedAreaCenter) / cachedAreaSize
                     + new Vector2(0.5f, 0.5f);

        int tx = Mathf.RoundToInt(uv.x * cachedResolution) - windowOrigin.x;
        int ty = Mathf.RoundToInt(uv.y * cachedResolution) - windowOrigin.y;

        // Pencere dışında veri yok. Uydurmak yerine geçersiz dönüyoruz —
        // "kar yok" demek yanlış olurdu, "bilmiyoruz" doğru.
        if (tx < 0 || tx >= Window || ty < 0 || ty >= Window) return false;

        Color s = snowCache[ty * Window + tx];
        Color t = trailCache[ty * Window + tx];

        sample = Decode(s, t);
        return true;
    }

    /// İZSİZ DÜNYANIN KAR SÜTUNU (m). ANLIK — geri okuma yok.
    ///
    /// İz gövdesinin oturma yüksekliği bunu okuyor. Doku örneğini (`BaseHeight`)
    /// okusaydı KAPALI DÖNGÜ kurulurdu: gövde bastıkça yoğunluk artıyor,
    /// `baseHeight = SWE×1000/ρ` düşüyor, gövde iniyor, daha çok oyuyor.
    /// Arada 30 karede bir tazelenen asenkron geri okuma var, yani döngü
    /// gecikmeli — sonuç osilatör.
    ///
    /// Ölçüldü: gövdenin yerel Y'si yürürken 10 mm ile 30 mm arasında düzenli
    /// salınıyor ve izin genişliği onunla birlikte 21 tekselden 13'e inip
    /// çıkıyor (`SYMPTOMS.md`).
    ///
    /// Dünya değeri yalnız yağış ve oturmayla değişiyor; gövdenin yazdığı
    /// hiçbir şey buraya geri dönmüyor.
    public float WorldColumnHeight
    {
        get
        {
            if (manager == null || manager.WorldSwe < 0f) return 0f;

            float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax,
                                   Mathf.Clamp01(manager.WorldRhoN));

            return manager.WorldSwe * SnowConstants.RhoWater / Mathf.Max(rho, 1f);
        }
    }

    /// Doku değerlerinden oyun tarafının gördüğü hâle. Saf fonksiyon:
    /// Play'e girmeden sınanabiliyor.
    public static SnowSample Decode(Color snow, Color trail)
    {
        float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax,
                               Mathf.Clamp01(snow.g));

        float baseHeight = snow.r * SnowConstants.RhoWater / Mathf.Max(rho, 1f);

        return new SnowSample
        {
            Depth = Mathf.Max(baseHeight - trail.r + trail.g, 0f),
            BaseHeight = baseHeight,
            SinkDepth = trail.r,
            Density01 = Mathf.Clamp01(snow.g),
            Wetness = Mathf.Clamp01(snow.b),
            Crust = Mathf.Clamp01(trail.b),
            Valid = true,
        };
    }
}
