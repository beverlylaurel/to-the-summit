// ROL: savrulan karın iki VFX katmanını (saltasyon, süspansiyon) tek eşikten
// sürer (spec §18.7).
// Çağıran: sahne (SnowManager'ın yanında).

using UnityEngine;
using UnityEngine.VFX;

/// SAVRULAN KAR İKİ KATMAN
/// `[KAYNAK: Pomeroy & Gray 1990; PBSM 1993; Nishimura & Hunt 2000]`.
///
/// Meteorolojide savrulan kar ikiye ayrılıyor ve bu doğrudan iki VFX sistemine
/// karşılık geliyor:
///
///   Saltasyon    1–5 cm    yüzeyle temas hâlinde zıplayarak, yoğun
///   Süspansiyon  ≤ 5 m     türbülansla askıda, seyrek
///
/// İKİSİNİN DE TETİĞİ AYNI. Spec §18.7: "Her ikisinin de tetiği §18.1'deki
/// `DriftActive01`. Ayrı eşik tanımlama." Bu bileşen o değeri
/// `SnowCurtainController.DriftActiveFor` üzerinden okuyor — ikinci bir eşik
/// hesabı kurmuyor.
[DisallowMultipleComponent]
public class SnowDriftVfxController : MonoBehaviour
{
    [Header("Katmanlar")]
    [Tooltip("VFX_Spindrift örneği — saltasyon, yere yapışık. Boş bırakılırsa " +
             "sürülmez.")]
    [SerializeField] VisualEffect spindrift;

    [Tooltip("VFX_SnowCurtain örneği — süspansiyon perdeleri. Boş bırakılırsa " +
             "sürülmez.")]
    [SerializeField] VisualEffect curtain;

    [Header("Bağımlılıklar")]
    [Tooltip("Rüzgâr hızını okuyan köprü.")]
    [SerializeField] SnowEnvironmentBridge environment;

    [Tooltip("Saltasyon oranının kaynağı.")]
    [SerializeField] SnowSettings settings;

    [Header("Özellik adları")]
    [SerializeField] string rateProperty = "SpawnRate";
    [SerializeField] string driftProperty = "DriftActive";

    /// Teşhis: o anki eşik değeri.
    public float DriftActive01 { get; private set; }

    /// Teşhis: saltasyon oranı.
    public float SpindriftRate { get; private set; }

    void LateUpdate()
    {
        if (environment == null || settings == null) return;

        DriftActive01 = SnowCurtainController.DriftActiveFor(
            environment.WindSpeed, SnowRuntimeState.LooseSnowFraction);

        // Spec §18.7 Sistem A: `_SpindriftRate * DriftActive01² * LooseSnowFraction`.
        //
        // KARE ALINIYOR: eşiğin hemen üstünde saltasyon zayıf başlıyor,
        // rüzgâr arttıkça hızla kalınlaşıyor. Doğrusal olsaydı eşikte birden
        // kalın bir tabaka belirirdi.
        SpindriftRate = settings.SpindriftRate
                      * DriftActive01 * DriftActive01
                      * SnowRuntimeState.LooseSnowFraction;

        Drive(spindrift, SpindriftRate);

        // Perdelerin oranı yok; kapasite 14 ve ömür uzun. Onlara yalnız eşik
        // gidiyor, alpha'yı grafik ondan türetiyor (spec §18.7: yükseldikçe
        // soluklaşır, sabit alpha kullanma).
        if (curtain != null && curtain.HasFloat(driftProperty))
            curtain.SetFloat(driftProperty, DriftActive01);
    }

    void Drive(VisualEffect vfx, float rate)
    {
        if (vfx == null) return;

        if (vfx.HasFloat(rateProperty)) vfx.SetFloat(rateProperty, rate);
        if (vfx.HasFloat(driftProperty)) vfx.SetFloat(driftProperty, DriftActive01);
    }
}
