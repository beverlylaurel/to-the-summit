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

    [Tooltip("Spawn kutularının izlediği hedef. OYUNCUNUN AYAĞI olmalı, kamera " +
             "değil: saltasyon yere yapışık bir katman ve kamera göz hizasında.")]
    [SerializeField] Transform followTarget;

    [Header("Özellik adları")]
    [SerializeField] string rateProperty = "SpawnRate";
    [SerializeField] string driftProperty = "DriftActive";

    /// Teşhis: o anki eşik değeri.
    public float DriftActive01 { get; private set; }

    /// Teşhis: saltasyon oranı.
    public float SpindriftRate { get; private set; }

    /// Spec §18.7: saltasyon kameranın RÜZGÂR YÖNÜNDEKİ şeridinde doğuyor.
    /// Şerit 30 m; kutu merkezi yarısı kadar ileride.
    const float SpindriftLead = 15f;

    /// Spec §18.7: süspansiyon kameranın RÜZGÂR ÜSTÜNDE 35 m'sinde doğuyor —
    /// oradan rüzgârla üstümüze geliyor.
    const float CurtainUpwind = 35f;

    /// Süspansiyon katmanının kutu merkezi. PBSM üst sınırı 5 m; kutu
    /// merkezi ortasında.
    const float CurtainHeight = 2.5f;

    void LateUpdate()
    {
        if (environment == null || settings == null) return;

        FollowTarget();

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

    /// SPAWN KUTULARI HEDEFİ İZLİYOR (spec §18.7).
    ///
    /// Bu yoktu ve iki katman da sahne orijininde duruyordu: ölçüldü, konum
    /// (0,0,0), kamera 7.5 km ötede. Oran doğru sürülüyordu, parçacıklar
    /// doğuyordu, hiçbiri görünmüyordu.
    ///
    /// Spec §18.7 iki katmanı FARKLI yere koyuyor:
    ///   Saltasyon (spindrift)  — kameranın rüzgâr yönündeki 30 m'lik şeridi,
    ///                            `y = groundY + random(0, 0.05)`. Yere yapışık.
    ///   Süspansiyon (curtain)  — rüzgâr üstünde 35 m, `y = groundY + h`.
    ///
    /// Grafikteki spawn kutusu YEREL; dünya konumu buradan geliyor.
    ///
    /// 1 m IZGARASINA SNAP'Lİ — `SnowfallLayers` ile aynı sebep: snap yoksa
    /// kamera hareketinde spawn deseni yürüyor ve taneler kameranın peşinden
    /// sürüklenen bir küme gibi görünüyor.
    void FollowTarget()
    {
        if (followTarget == null) return;

        Vector3 wind = environment.WindDirection;
        Vector3 p = followTarget.position;

        if (spindrift != null)
            spindrift.transform.position = Snap(p + wind * SpindriftLead);

        if (curtain != null)
            curtain.transform.position = Snap(p - wind * CurtainUpwind
                                              + Vector3.up * CurtainHeight);
    }

    static Vector3 Snap(Vector3 v) =>
        new Vector3(Mathf.Floor(v.x), Mathf.Floor(v.y), Mathf.Floor(v.z));

    void Drive(VisualEffect vfx, float rate)
    {
        if (vfx == null) return;

        if (vfx.HasFloat(rateProperty)) vfx.SetFloat(rateProperty, rate);
        if (vfx.HasFloat(driftProperty)) vfx.SetFloat(driftProperty, DriftActive01);
    }
}
