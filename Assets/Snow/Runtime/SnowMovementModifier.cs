// ROL: derin karda yürüyüşü yavaşlatır (§12.2).
// Çağıran: kimse — kendi Update'inde oyuncunun hız çarpanını yazıyor.

using UnityEngine;

[DisallowMultipleComponent]
public class SnowMovementModifier : MonoBehaviour
{
    /// Cezanın başladığı derinlik, metre. Bunun altında kar yürüyüşü etkilemiyor.
    const float PenaltyStart = 0.10f;

    /// Cezanın doyduğu ek derinlik, metre.
    const float PenaltyRange = 0.60f;

    /// Azami yavaşlama oranı, taze karda.
    const float PenaltyMax = 0.45f;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowSampler sampler;
    [SerializeField] FirstPersonController player;

    [Header("Yumuşatma")]
    [Tooltip("Hız çarpanının yeni değere yetişme süresi, saniye. Anlık geçiş " +
             "adım başına sıçrama yapıyor.")]
    [SerializeField] float smoothing = 0.25f;

    float current = 1f;

    /// Şu anki hız çarpanı. 1 = ceza yok.
    public float SpeedMultiplier => current;

    /// Son okunan derinlik, metre. Animator ve ses bunu kullanıyor.
    public float Depth { get; private set; }

    /// Son okunan yoğunluk. 0 = toz, 1 = buz gibi.
    public float Density01 { get; private set; }

    public float Wetness { get; private set; }

    public bool HasSample { get; private set; }

    void OnEnable()
    {
        if (sampler == null)
            throw new System.InvalidOperationException("SnowMovementModifier: SnowSampler atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
        if (player == null)
            throw new System.InvalidOperationException("SnowMovementModifier: oyuncu atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");

        current = 1f;
    }

    void OnDisable()
    {
        // Bileşen kapanınca oyuncu cezalı kalmasın.
        if (player != null) player.SpeedMultiplier = 1f;
    }

    void Update()
    {
        HasSample = sampler.TrySampleSnow(player.transform.position, out SnowSample sample);

        float target = 1f;

        if (HasSample)
        {
            Depth = sample.depth;
            Density01 = sample.density01;
            Wetness = sample.wetness;

            // SIKIŞMIŞ PATİKADA CEZA YOK. Aynı hattan geçe geçe yoğunluk yükselince
            // çarpan kendiliğinden 1'e dönüyor — ayrı bir "patika" kavramı yok.
            target = 1f - Mathf.Clamp01((sample.depth - PenaltyStart) / PenaltyRange)
                        * PenaltyMax * (1f - sample.density01);
        }

        current = smoothing > 1e-4f
            ? Mathf.Lerp(current, target, 1f - Mathf.Exp(-Time.deltaTime / smoothing))
            : target;

        player.SpeedMultiplier = current;

        // Animator'a derinlik yazmak §12.5'in kapsamı; burada yalnız değer açılıyor.
    }
}
