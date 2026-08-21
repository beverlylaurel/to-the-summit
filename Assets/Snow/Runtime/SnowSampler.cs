// ROL: kar durumunu CPU'ya alır ve oyun koduna açar (§12).
// Ayak sesi, hareket cezası, toz bulutu ve karakter kar çizgisi hep buradan okuyor.
// Çağıran: SnowFootstepAudio, SnowMovementModifier, SnowFootstepDriver.

using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// Bir noktadaki kar durumu (§12).
public struct SnowSample
{
    public float depth;        // metre
    public float density01;    // 0 = toz, 1 = buz gibi
    public float wetness;      // 0..1
    public float disturb;      // 0..1
    public bool valid;         // yakın alan içinde mi
}

[DisallowMultipleComponent]
public class SnowSampler : MonoBehaviour
{
    /// Okunan bölgenin kenarı, teksel (§12). 64 x 64 x 1.17 cm = 75 cm — oyuncunun
    /// bastığı yeri ve iki adımlık çevresini kapsıyor.
    const int RegionTexels = 64;

    /// Kaç karede bir okunuyor. Bloklamayan; ~2 kare gecikme kabul edilebilir.
    const int ReadInterval = 4;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowManager manager;

    [Tooltip("Bölgenin merkezinde duracak nesne. Normalde oyuncu.")]
    [SerializeField] Transform followTarget;

    Vector4[] region;

    /// Okunan bölgenin sol alt köşesi, MUTLAK dünya teksel ızgarasında.
    ///
    /// Merkez teksel değil mutlak koordinat saklanıyor: bölge oyuncu yürüdükçe
    /// kayıyor ve okuma iki kare gecikmeli geliyor. Mutlak ızgara sayesinde eski
    /// bir okuma da doğru dünya noktasına denk düşüyor.
    Vector2Int regionOrigin;

    bool hasRegion;
    bool requestPending;

    /// YOK EDİLMİŞ NESNE KONTROLÜ. Geri okuma geri döndüğünde bileşen çoktan
    /// yok edilmiş olabiliyor ve `isActiveAndEnabled` o durumda istisna atıyor.
    /// Düz bir alan okumak güvenli: C# nesnesi yaşıyor, yok olan yerel taraf.
    bool alive;

    public bool HasData => hasRegion;

    void OnEnable()
    {
        if (manager == null)
            throw new System.InvalidOperationException("SnowSampler: SnowManager atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (followTarget == null)
            throw new System.InvalidOperationException("SnowSampler: takip hedefi atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        region = new Vector4[RegionTexels * RegionTexels];
        hasRegion = false;
        requestPending = false;
        alive = true;
    }

    void OnDisable() => alive = false;

    void LateUpdate()
    {
        if (requestPending || !manager.IsReady) return;
        if (Time.frameCount % ReadInterval != 0) return;

        int resolution = manager.Settings.QualityData.Resolution;
        float texelSize = manager.TexelSize;

        Vector3 position = followTarget.position;

        // Oyuncunun bulunduğu tekselin doku içindeki yeri.
        Vector2 center = manager.AreaCenter;

        int localX = Mathf.FloorToInt((position.x - center.x) / texelSize) + resolution / 2;
        int localY = Mathf.FloorToInt((position.z - center.y) / texelSize) + resolution / 2;

        int x = Mathf.Clamp(localX - RegionTexels / 2, 0, resolution - RegionTexels);
        int y = Mathf.Clamp(localY - RegionTexels / 2, 0, resolution - RegionTexels);

        // Mutlak ızgara: doku sol alt köşesinin dünya tekseli + yerel ofset.
        Vector2Int centerTexel = manager.CenterTexel;
        regionOrigin = new Vector2Int(centerTexel.x - resolution / 2 + x,
                                      centerTexel.y - resolution / 2 + y);

        requestPending = true;

        AsyncGPUReadback.Request(manager.StateTexture, 0, x, RegionTexels, y, RegionTexels, 0, 1,
                                 TextureFormat.RGBAFloat, OnReadback);
    }

    void OnReadback(AsyncGPUReadbackRequest request)
    {
        requestPending = false;

        // Hata YUTULMUYOR ama istisna da atılmıyor: geri okuma oyun kapanırken
        // düzenli olarak iptal ediliyor ve bu bir hata değil.
        if (!alive || request.hasError) return;

        NativeArray<Vector4> data = request.GetData<Vector4>();
        if (data.Length < region.Length) return;

        data.CopyTo(region);
        hasRegion = true;
    }

    /// Verilen dünya noktasındaki kar durumu. Okunan bölgenin dışındaysa `valid` false.
    public bool TrySampleSnow(Vector3 worldPos, out SnowSample sample)
    {
        sample = default;
        if (!hasRegion) return false;

        float texelSize = manager.TexelSize;
        if (texelSize <= 0f) return false;

        int absX = Mathf.FloorToInt(worldPos.x / texelSize);
        int absY = Mathf.FloorToInt(worldPos.z / texelSize);

        int lx = absX - regionOrigin.x;
        int ly = absY - regionOrigin.y;

        if (lx < 0 || ly < 0 || lx >= RegionTexels || ly >= RegionTexels) return false;

        Vector4 state = region[ly * RegionTexels + lx];

        sample.density01 = Mathf.Clamp01(state.y);
        sample.wetness = Mathf.Clamp01(state.z);
        sample.disturb = Mathf.Clamp01(state.w);

        float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax, sample.density01);
        sample.depth = state.x * SnowConstants.RhoWater / Mathf.Max(rho, 1f);

        sample.valid = true;
        return true;
    }
}
