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

    [Tooltip("Grafikteki rüzgâr kuvveti özelliğinin adı.")]
    [SerializeField] string windProperty = "WindForce";

    [Tooltip("Grafikteki tane emissive rengi özelliğinin adı.")]
    [SerializeField] string emissiveProperty = "FlakeEmissive";

    [Tooltip("Grafikteki zemin kotu özelliğinin adı.")]
    [SerializeField] string groundProperty = "GroundY";

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

    [Tooltip("Zemin kotunun okunduğu hedef — oyuncunun AYAĞI. Kamera değil: " +
             "kamera göz hizasında ve tane oraya inmeden ölür.")]
    [SerializeField] Transform groundReference;

    [Header("Devir")]
    [Tooltip("Compute tabanlı eski yağış. Yakın katman bağlıyken KAPATILIYOR; " +
             "iki yağış sistemi birden koşarsa kar iki katına çıkar.")]
    [SerializeField] SnowfallRenderer computeFallback;

    /// Teşhis: yakın katmanın o anki oranı.
    public float NearRate { get; private set; }

    /// Teşhis: grafiğe giden tane emissive'i.
    public Color NearEmissive { get; private set; }

    /// SÜRÜKLEME KATSAYISI GRAFİKTEKİYLE AYNI OLMAK ZORUNDA.
    ///
    /// Rüzgâr grafiğe KUVVET olarak gidiyor; denge hızı `F / drag`. Buradaki
    /// sayı `SnowVfxBuilder`'daki `dragCoefficient` ile aynı olmazsa tane
    /// rüzgârdan hızlı ya da yavaş sürüklenir.
    const float FlakeDrag = 9.81f;

    /// Spec §17.1'in formülü: `_FlakeEmissive * mainLightColor * 0.04`.
    ///
    /// SPEC'İN 0.04'Ü BU SAHNEDE 25 KAT DÜŞÜK KALIYOR. Ana ışık HDR ve
    /// şiddeti 2.7; `0.9 * 2.7 * 0.04 = 0.097` çıkıyor ve tane ekranda
    /// gökyüzünden ayırt edilemiyor.
    ///
    /// Ölçüldü — gökyüzü bölgesinde tane pikselleri, üç emissive değeri:
    ///     0.097 -> en parlak 161, gökyüzü medyanı 107   (görünmüyor)
    ///     2.5   -> en parlak 227, doygun piksel 0        (doğru)
    ///     20    -> en parlak 255, doygun piksel 113      (yanmış)
    ///
    /// Spec `_FlakeEmissive`'in DEĞERİNİ vermiyor, yalnız formülü veriyor;
    /// kalibrasyon bize ait.
    ///
    /// İKİNCİ KALİBRASYON — DOKU DEĞİŞTİ. Tane dokusu `DefaultDot`'tan (tam
    /// daire) 4×4 kar tanesi atlasına geçince aynı ekran alanında daha az
    /// piksel doluyor: dallı tane, boşluklu. Ölçüldü, gökyüzü bölgesinde en
    /// parlak piksel 222 -> 193. Ölçek 1.0'dan 1.6'ya çıkarıldı, tepe 225'e
    /// döndü ve hiçbir piksel doymadı.
    const float EmissiveScale = 1.6f;

    /// Tanenin kendi rengi; ana ışık bunu çarpıyor. Kar tanesi nötr beyaza
    /// yakın, hafif mavi kaçık — saçılma kısa dalga boyunu biraz daha çok
    /// dağıtıyor.
    static readonly Color FlakeTint = new Color(0.86f, 0.92f, 1f);

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

        // RÜZGÂR KUVVET OLARAK GİDİYOR (spec §17.1). Denge hızı `F / drag`
        // olduğu için hedef hız burada sürüklemeyle çarpılıyor.
        if (environment != null && nearLayer.HasVector3(windProperty))
            nearLayer.SetVector3(windProperty,
                                 environment.WindDirection * environment.WindSpeed * FlakeDrag);

        // ZEMİN KOTU (spec §17.1: tane zeminin 2 cm altına inince ölüyor).
        //
        // KAMERA DEĞİL AYAK. İlk sürüm `followTarget`i (kamerayı) kullandı ve
        // KARIN TAMAMINI SİLDİ: kamera göz hizasında (zeminden 1.65 m yukarıda),
        // kesme düzlemi de oraya çıkınca her tane daha havadayken ölüyordu —
        // ölçüldü, `aliveParticleCount = 0`.
        //
        // VFX'in zemin yükseklik dokusuna erişimi yok; oyuncunun ayak kotu
        // yeterince iyi bir yaklaşım, çünkü spawn kutusu yalnız 24 m.
        // KOT YEREL GÖNDERİLİYOR. Grafikteki `position` VFX'in kendi uzayında;
        // dünya kotunu doğrudan yollamak karın tamamını siliyordu.
        if (groundReference != null && nearLayer.HasFloat(groundProperty))
            nearLayer.SetFloat(groundProperty,
                               groundReference.position.y - nearLayer.transform.position.y);

        // TANE GECE PARLAMASIN. Emissive ana ışık renginden türüyor; sabit
        // bırakılırsa kar karanlıkta da aynı parlaklıkta duruyor.
        if (nearLayer.HasVector4(emissiveProperty))
        {
            Color light = environment != null && environment.Sun != null
                ? environment.Sun.color * environment.Sun.intensity
                : Color.white;

            NearEmissive = FlakeTint * light * EmissiveScale;
            nearLayer.SetVector4(emissiveProperty, NearEmissive);
        }
    }
}
