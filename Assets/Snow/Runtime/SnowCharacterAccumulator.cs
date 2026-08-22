// ROL: karakterin üstünde biriken karı sürer. Karakterin materyaline
// `_SnowAccum` yazar; shader onu okumuyorsa hiçbir şey olmaz.
// Çağıran: sahne (karakterin üstünde).

using UnityEngine;

/// MEVCUT KARAKTER SHADER'I DEĞİŞTİRİLMEDİ (spec §1.4, §16.1).
///
/// Bu bileşen `MaterialPropertyBlock` ile `_SnowAccum` ve `_SnowLineY`
/// yazıyor. Karakter shader'ı bu property'leri tanımıyorsa yazma sessizce
/// yok sayılıyor — hiçbir şey bozulmuyor, sadece etki görünmüyor.
/// Property'lerin shader'a eklenmesi ayrı bir karardır ve kullanıcıya ait.
[DisallowMultipleComponent]
public class SnowCharacterAccumulator : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [Tooltip("Karın göründüğü renderer'lar. Boş bırakılırsa bu nesnenin "
             + "altındaki bütün renderer'lar bir kez taranır.")]
    [SerializeField] Renderer[] targets;

    [Tooltip("Yağmurun karı temizlemesi için gereken çevre kaynağı.")]
    [SerializeField] MonoBehaviour environmentSource;

    [Tooltip("Ayak konumu — bacak kar çizgisi buradan hesaplanıyor.")]
    [SerializeField] Transform footAnchor;

    [Header("Ayarlar")]
    [Tooltip("Kar yağarken saniyede biriken oran.")]
    [SerializeField] float accumulationRate = 0.05f;

    [Tooltip("Hız başına saniyede silinen oran.")]
    [SerializeField] float shakeOffRate = 0.06f;

    [Tooltip("Yağmurda saniyede silinen oran.")]
    [SerializeField] float rainClearRate = 0.4f;

    [Tooltip("Kapalı alan sayılan gökyüzü görünürlüğü.")]
    [SerializeField, Range(0f, 1f)] float shelteredBelow = 0.3f;

    [Tooltip("Kapalı alanda saniyede silinen oran.")]
    [SerializeField] float shelterClearRate = 0.25f;

    ISnowEnvironmentSource env;
    MaterialPropertyBlock block;

    Vector3 prevPos;
    float accum;
    float skyVisibility = 1f;

    public float Accumulation => accum;

    /// KÖPRÜYÜ KODDAN BAĞLAMAK İÇİN. Inspector alanı `MonoBehaviour` tutuyor
    /// — Unity arayüz tipini serileştiremiyor. Koddan bağlayan (veya sınayan)
    /// taraf bunu kullanıyor.
    public void SetEnvironment(ISnowEnvironmentSource source) => env = source;

    /// Gökyüzü görünürlüğü GPU'da; CPU tarafına buradan veriliyor.
    /// `SnowSampler` (Faz 9) gelince oradan beslenecek.
    public void SetSkyVisibility(float value) => skyVisibility = Mathf.Clamp01(value);

    void OnEnable()
    {
        env = environmentSource as ISnowEnvironmentSource;

        if (targets == null || targets.Length == 0)
            targets = GetComponentsInChildren<Renderer>();

        block ??= new MaterialPropertyBlock();
        prevPos = transform.position;
        accum = 0f;
    }

    void LateUpdate()
    {
        Vector3 p = transform.position;
        float speed = (p - prevPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
        prevPos = p;

        Step(Time.deltaTime, speed);
    }

    /// ADIM AYRI BİR METOTTA. `LateUpdate` yalnız hızı ölçüyor; birikme
    /// mantığı Play'e girmeden sınanabilsin diye dışarıdan çağrılabiliyor.
    public void Step(float dt, float speed)
    {
        accum += SnowRuntimeState.SnowfallIntensity01 * accumulationRate * dt * skyVisibility;
        accum -= speed * shakeOffRate * dt;

        if (skyVisibility < shelteredBelow) accum -= shelterClearRate * dt;

        // YAĞMUR KARI HIZLA SİLİYOR (spec §16.1) — ama koşul spec'inkinden
        // FARKLI, ölçülmüş sebeple.
        //
        // Spec `env.PrecipKind == Rain` diyor. Bu projede yağışın TÜRÜ yok:
        // köprü yağış varken hep `Rain` döndürüyor, kar kararını
        // `SnowfallController`'ın sıcaklık histerezisi veriyor (§3.4). Spec'in
        // koşulu uygulansaydı kar yağarken de karakter sürekli temizlenirdi —
        // ölçüldü, birikme 20 saniyede 0.000'da kaldı.
        //
        // Doğru koşul: yağış VAR ama kar DEĞİL. Kullanıcının kararıyla da
        // uyumlu: aynı anda hem yağmur hem kar görünmüyor (`DECISIONS.md`).
        bool raining = env != null &&
                       env.PrecipKind != PrecipitationKind.None &&
                       !SnowRuntimeState.IsSnowing;

        if (raining) accum -= rainClearRate * dt;

        accum = Mathf.Clamp01(accum);

        Publish();
    }

    void Publish()
    {
        block ??= new MaterialPropertyBlock();

        float lineY = footAnchor != null ? footAnchor.position.y : transform.position.y;

        block.SetFloat(SnowShaderIDs.SnowAccum, accum);
        block.SetFloat(SnowShaderIDs.SnowLineY, lineY);

        if (targets == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            Renderer r = targets[i];
            if (r != null) r.SetPropertyBlock(block);
        }
    }
}
