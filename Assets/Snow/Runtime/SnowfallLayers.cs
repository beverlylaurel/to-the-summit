// ROL: kar yağışının iki katmanını (yakın parçacık, uzak doku) tek şiddetten
// sürer (spec §17, §17.3).
// Çağıran: sahne (SnowManager'ın yanında).

using UnityEngine;
using UnityEngine.VFX;

/// İKİ KATMAN, TEK KAYNAK
/// `[KAYNAK: Langer ve ark., EGSR 2004]`.
///
/// Makalenin gözlemi: düşen kar aynı anda hem ayrı ayrı hareket eden
/// parçacıklardır hem de dinamik bir dokudur. Doku özelliklerini yalnız
/// parçacıkla yakalamak, render hızını ciddi biçimde düşürecek kadar çok
/// parçacık gerektiriyor. Çözüm seyrek parçacık + aralarını dolduran doku.
///
/// HER İKİSİ DE AYNI `i01`'DEN. Spec §17.3 bunu ayrıca uyarıyor: ayrı
/// kaynaklardan gelirse "yoğun kar yağıyor ama zemin birikmiyor" hatası
/// çıkıyor. Şiddet `SnowRuntimeState.SnowfallIntensity01`; bu bileşen onu
/// yalnız OKUYOR, kendi şiddetini üretmiyor.
[DisallowMultipleComponent]
public class SnowfallLayers : MonoBehaviour
{
    [Header("Yakın katman — VFX parçacıkları (spec §17.1)")]
    [Tooltip("VFX_Snowfall örneği. Boş bırakılırsa katman sürülmez; " +
             "mevcut compute yolu çalışmaya devam eder.")]
    [SerializeField] VisualEffect nearLayer;

    [Tooltip("Grafikteki spawn oranı özelliğinin adı.")]
    [SerializeField] string rateProperty = "SpawnRate";

    [Tooltip("Grafikteki türbülans şiddeti özelliğinin adı.")]
    [SerializeField] string turbulenceProperty = "TurbulenceIntensity";

    [Header("Uzak katman — doku perdeleri (spec §17.2)")]
    [Tooltip("Uzak doku katmanı. Boş bırakılırsa sürülmez.")]
    [SerializeField] SnowCurtainController farLayer;

    [Header("Çevre")]
    [Tooltip("Rüzgâr hızını okuyan köprü. Türbülans şiddeti ondan türüyor.")]
    [SerializeField] SnowEnvironmentBridge environment;

    /// Teşhis: yakın katmanın o anki oranı.
    public float NearRate { get; private set; }

    /// Teşhis: katman gerçekten sürülüyor mu.
    public bool NearDriven => nearLayer != null;

    void LateUpdate()
    {
        float i01 = SnowRuntimeState.SnowfallIntensity01;

        // Spec §17.2: şiddet 0.05'in altındaysa uzak katman devre dışı.
        // Yakın katman da sıfır oranla zaten parçacık üretmiyor.
        NearRate = Mathf.Lerp(0f, SnowConstants.MaxFlakeRate, i01);

        if (nearLayer == null) return;

        // ORAN GRAFİKTE DEĞİL BURADA. Grafikteki sabit oran yalnız
        // varsayılan; şiddet oyunun hava sisteminden geliyor (spec §17.3).
        if (nearLayer.HasFloat(rateProperty))
            nearLayer.SetFloat(rateProperty, NearRate);

        // Spec §17.1: `Intensity = 0.35 * _WindSpeed + 0.15`.
        if (environment != null && nearLayer.HasFloat(turbulenceProperty))
            nearLayer.SetFloat(turbulenceProperty,
                               0.35f * environment.WindSpeed + 0.15f);
    }
}
