// ROL: nesne üstü kar kaplamasının global değerlerini sürer (§9).
// `_SnowCoverage` ve `_SnowUpDirection` sahnedeki her yüzeyin okuduğu tek kaynak.
// Çağıran: kimse — kendi LateUpdate'inde çalışır.

using UnityEngine;

[DisallowMultipleComponent]
public class SnowCoverageDriver : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowWeather weather;

    [Tooltip("Engel kamerası. Kar yönü değişince o da eğilmeli.")]
    [SerializeField] SnowOcclusionCapture occlusion;

    [Header("Kaplama")]
    [Tooltip("Kaplamanın hava değişimine yetişme zaman sabiti, saniye. Nesneler " +
             "gözle görülür şekilde KADEMELİ kaplanır, anında değil.")]
    [SerializeField] float coverageTau = 240f;

    [Header("Kar yönü")]
    [Tooltip("Rüzgârın kar yönünü eğme miktarı. 0 = kar hep dik iner.")]
    [SerializeField, Range(0f, 0.6f)] float windTilt = 0.22f;

    [Tooltip("Eğimin doyduğu rüzgâr hızı, m/s.")]
    [SerializeField] float windTiltReference = 15f;

    float coverage;

    public float Coverage => coverage;
    public Vector3 UpDirection { get; private set; } = Vector3.up;

    void OnEnable()
    {
        if (weather == null)
            throw new System.InvalidOperationException("SnowCoverageDriver: SnowWeather atanmadı.");

        coverage = weather.Coverage;
        Apply();
    }

    void LateUpdate()
    {
        // Üstel yaklaşım: yağış kesilince kaplama da aynı yavaşlıkta geri çekiliyor.
        float dt = Time.deltaTime * Mathf.Max(SnowManager.SimulationSpeed, 0f);
        coverage += (weather.Coverage - coverage) * (1f - Mathf.Exp(-dt / Mathf.Max(coverageTau, 1e-3f)));

        // KAR YÖNÜ RÜZGÂRDA EĞİLİYOR (§8.4). Engel kamerası da bu yönden bakıyor,
        // yani fırtınada saçak altı da kar alıyor — ikisi tek kaynaktan sürülüyor.
        Vector3 wind = weather.WindWS;
        Vector3 tilt = wind.sqrMagnitude > 1e-6f
            ? wind.normalized * (windTilt * Mathf.Clamp01(weather.WindSpeed / Mathf.Max(windTiltReference, 1e-3f)))
            : Vector3.zero;

        Vector3 next = (Vector3.up - tilt).normalized;

        // ENGEL HARİTASI EŞİKLE YENİLENİYOR. Her karede yön atamak haritayı kirli
        // işaretler ve saniyede altmış kez yeniden çizdirirdi; eşik bir dereceye
        // karşılık geliyor.
        if (occlusion != null && Vector3.Dot(next, occlusion.UpDirection) < 0.99985f)
            occlusion.UpDirection = next;

        UpDirection = next;

        Apply();
    }

    void Apply()
    {
        Shader.SetGlobalFloat(SnowShaderIDs.SnowCoverage, coverage);
        Shader.SetGlobalVector(SnowShaderIDs.SnowUpDirection, UpDirection);
    }
}
