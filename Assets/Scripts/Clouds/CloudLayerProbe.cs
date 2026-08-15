using UnityEngine;
using UnityEngine.Rendering;

/// BULUT KATMANININ TEK KAYNAĞI. Bulutları çizen sistem bir render özelliği; oyun
/// tarafındaki tüketiciler (yağış kesimi, tırmanma göstergesi) ona doğrudan soramaz.
/// Bu bileşen aynı Volume ayarlarını ve aynı hava haritasını okuyup kotları veriyor.
///
/// Sözleşme: gökyüzünü çizen veri neyse burada okunan da odur. İkinci bir yaklaşım
/// kurulmuyor — kurulursa gökte bulut olmayan yerde tavan çıkar.
public class CloudLayerProbe : MonoBehaviour
{
    [Tooltip("Bulut ayarlarını taşıyan Volume.")]
    [SerializeField] Volume cloudVolume;

    [Tooltip("Tavanı itilecek hava sürücüsü.")]
    [SerializeField] AltitudeWeatherDriver driver;

    [Tooltip("Tavanın okunacağı nokta — oyuncu.")]
    [SerializeField] Transform observer;

    static readonly int CloudBottomId = Shader.PropertyToID("_CloudBottom");
    static readonly int CloudTopId = Shader.PropertyToID("_CloudTop");

    VolumetricClouds clouds;
    Texture2D map;

    /// Katmanın tabanı (metre). Sütuna göre değişmiyor.
    public float Bottom => clouds.bottomAltitude.value;

    /// Katmanın olabileceği en yüksek kot (metre). Gösterge bunu aralığın üst ucu olarak
    /// yazıyor; belirli bir sütunun tepesi için `TopAt` kullanılır.
    public float MaxTop => clouds.bottomAltitude.value + clouds.altitudeRange.value;

    void OnEnable()
    {
        if (cloudVolume == null || driver == null || observer == null)
            throw new System.InvalidOperationException($"{nameof(CloudLayerProbe)}: bağımlılıklar atanmadı.");

        if (!cloudVolume.profile.TryGet(out clouds))
            throw new System.InvalidOperationException($"{nameof(CloudLayerProbe)}: profilde {nameof(VolumetricClouds)} yok.");

        map = clouds.cloudMap.value as Texture2D;
        if (map == null)
            throw new System.InvalidOperationException($"{nameof(CloudLayerProbe)}: hava haritası atanmadı.");
    }

    void LateUpdate()
    {
        driver.CloudColumnTop = TopAt(observer.position);

        // BAĞ 8: ortak globaller. Şimşek kolu (`LightningBolt.shader`) çakmayı bulut
        // kabuğuyla kesiştiriyor ve kotları buradan okuyor. Eskiden `AtmosphereController`
        // yayınlıyordu — silinen bulut modelinin kotlarıydı, gökyüzünde çizilenle ilgisi
        // yoktu. Kabuk küresel olduğu için sütun tepesi değil katmanın azamisi veriliyor.
        Shader.SetGlobalFloat(CloudBottomId, Bottom);
        Shader.SetGlobalFloat(CloudTopId, MaxTop);
    }

    /// O sütunun bulut tepesi (metre). Hava haritasının B kanalı azami bulut yüksekliğini
    /// taşıyor (`w_h`, `[H18 s.11]`); shader da yoğunluğu tam bu kotta kesiyor.
    ///
    /// Sütunda hiç bulut yoksa sonsuz dönüyor: "tepesi yok" ile "tepesi yerde" aynı şey
    /// değil. İkincisi yağışı her yerde keserdi.
    public float TopAt(Vector3 worldPosition)
    {
        Color sample = Sample(worldPosition);
        if (CoverageOf(sample) <= 0f) return float.PositiveInfinity;

        return clouds.bottomAltitude.value + clouds.altitudeRange.value * sample.b;
    }

    /// O sütunun kapsaması [0,1]. Gökyüzünün ne kadarının kapandığı değil — o sütunda
    /// bulut olma oranı.
    public float CoverageAt(Vector3 worldPosition) => CoverageOf(Sample(worldPosition));

    Color Sample(Vector3 worldPosition)
    {
        float size = clouds.cloudMapSize.value;
        return map.GetPixelBilinear(worldPosition.x / size, worldPosition.z / size);
    }

    /// Shader'daki formülün AYNISI: `WM_c = max(w_c0, SAT(g_c − 0.5) × w_c1 × 2)`
    /// `[H18 s.11]`. İki yerde iki formül olursa gösterge gökyüzüyle çelişir.
    float CoverageOf(Color sample) => Mathf.Max(sample.r,
        Mathf.Clamp01(clouds.cloudCoverage.value - 0.5f) * sample.g * 2f);

    public void Bind(Volume cloudVolumeRef, AltitudeWeatherDriver driverRef, Transform observerRef)
    {
        cloudVolume = cloudVolumeRef;
        driver = driverRef;
        observer = observerRef;
    }
}
