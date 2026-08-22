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

    // UZAK KATMAN BURADAN SÜRÜLMÜYOR. `SnowfallCurtains` şiddeti
    // `SnowRuntimeState.SnowfallIntensity01`'den KENDİSİ okuyor — spec §17.3'ün
    // "her ikisi de aynı i01'den" kuralı böyle de sağlanıyor, tek kaynak
    // `SnowRuntimeState`. Buradan ikinci bir yol geçirmek aynı sayıyı iki kez
    // taşımak olurdu.
    //
    // Önceden burada `SnowCurtainController` tipinde bir alan vardı ve hiç
    // kullanılmıyordu; üstelik yanlış sistemi gösteriyordu (o §18.7'nin
    // SAVRULMA perdeleri, bu §17.2'nin YAĞIŞ perdeleri).

    [Header("Çevre")]
    [Tooltip("Rüzgâr hızını okuyan köprü. Türbülans şiddeti ondan türüyor.")]
    [SerializeField] SnowEnvironmentBridge environment;

    [Tooltip("Spawn kutusunun izlediği hedef — kamera.")]
    [SerializeField] Transform followTarget;

    [Header("Devir")]
    [Tooltip("Compute tabanlı eski yağış. Yakın katman bağlıyken KAPATILIYOR; " +
             "iki yağış sistemi birden koşarsa kar iki katına çıkar.")]
    [SerializeField] SnowfallRenderer computeFallback;

    /// Teşhis: yakın katmanın o anki oranı.
    public float NearRate { get; private set; }

    /// Teşhis: katman gerçekten sürülüyor mu.
    public bool NearDriven => nearLayer != null;

    /// TEK YAĞIŞ SİSTEMİ KOŞAR.
    ///
    /// Yakın katman bağlıysa compute yolu kapanıyor. İkisi birden koşarsa kar
    /// iki katına çıkar ve hangisinin ne çizdiği ayrılamaz — bir belirti
    /// görüldüğünde hangi sisteme bakılacağı belirsiz olur.
    ///
    /// Compute yolu VFX doğrulanana kadar duruyor; doğrulanınca silinecek
    /// (`DECISIONS.md` → Silinecek geçiciler).
    void OnEnable()
    {
        if (computeFallback != null)
            computeFallback.enabled = nearLayer == null;
    }

    void OnDisable()
    {
        // Bileşen kapanınca eski yol geri açılıyor: kar sisteminin tamamen
        // susması, yarısının susmasından iyidir.
        if (computeFallback != null) computeFallback.enabled = true;
    }

    void LateUpdate()
    {
        float i01 = SnowRuntimeState.SnowfallIntensity01;

        NearRate = Mathf.Lerp(0f, SnowConstants.MaxFlakeRate, i01);

        if (nearLayer == null) return;

        // SPAWN KUTUSU KAMERAYI İZLİYOR (spec §17.1): merkez
        // `cameraPos + up * 11 + windDir * 3`, 1 m ızgarasına SNAP'Lİ.
        //
        // Snap yoksa kamera hareketinde spawn deseni yürüyor — taneler
        // kameranın peşinden sürüklenen bir küme gibi görünüyor.
        if (followTarget != null)
        {
            Vector3 wind = environment != null ? environment.WindDirection : Vector3.zero;
            Vector3 c = followTarget.position + Vector3.up * 11f + wind * 3f;

            nearLayer.transform.position = new Vector3(
                Mathf.Floor(c.x), Mathf.Floor(c.y), Mathf.Floor(c.z));
        }

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
