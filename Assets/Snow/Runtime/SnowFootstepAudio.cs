// ROL: ayak sesi seçimi (§12.1). Kar durumundan hangi ses ailesinin çalacağını
// belirler ve çalar.
// SES DOSYALARI KULLANICIDAN — burada yalnız seçim mantığı var (§14).
// Çağıran: SnowFootstepDriver (ayak bastığında).

using UnityEngine;

/// Ayak sesi aileleri (§12.1).
public enum SnowFootstepSurface
{
    /// Kar yok denecek kadar ince: zemin sesi.
    Hard = 0,

    /// Sıkışmış, gıcırtılı.
    Packed = 1,

    /// Sığ, çıtırtılı.
    Shallow = 2,

    /// Yumuşak, boğuk.
    Powder = 3,

    /// Derin, hışırtılı.
    Deep = 4,
}

[System.Serializable]
public class SnowFootstepClipSet
{
    [Tooltip("Kuru varyant.")]
    public AudioClip[] dry;

    [Tooltip("Islak varyant. Boşsa kuru çalınır.")]
    public AudioClip[] wet;
}

[DisallowMultipleComponent]
public class SnowFootstepAudio : MonoBehaviour
{
    /// Bu derinliğin altında zemin sesi.
    const float HardDepth = 0.02f;

    /// Bu derinliğin altında sığ; üstünde toz ya da derin.
    const float ShallowDepth = 0.08f;

    /// Sığ karda bu yoğunluğun üstü sıkışmış sayılıyor.
    const float PackedDensity = 0.55f;

    /// Bu yoğunluğun altı toz.
    const float PowderDensity = 0.30f;

    /// Bu ıslaklığın üstünde ıslak varyant.
    const float WetThreshold = 0.55f;

    [Header("Bağımlılıklar")]
    [SerializeField] SnowSampler sampler;
    [SerializeField] AudioSource source;

    [Header("Sesler")]
    [Tooltip("Sıra: Hard, Packed, Shallow, Powder, Deep.")]
    [SerializeField] SnowFootstepClipSet[] clipSets = new SnowFootstepClipSet[5];

    [SerializeField, Range(0f, 1f)] float volume = 0.8f;
    [SerializeField, Range(0f, 0.5f)] float pitchJitter = 0.08f;

    /// Son seçilen yüzey. Teşhis penceresi bunu gösteriyor.
    public SnowFootstepSurface LastSurface { get; private set; }

    public bool LastWasWet { get; private set; }

    void OnEnable()
    {
        if (sampler == null)
            throw new System.InvalidOperationException("SnowFootstepAudio: SnowSampler atanmadı. Kar Teşhisi > Sahneyi kur çalıştır.");
    }

    /// Ayak yere bastığında çağrılıyor.
    public void PlayFootstep(Vector3 worldPos)
    {
        SnowFootstepSurface surface = SnowFootstepSurface.Hard;
        bool wet = false;

        if (sampler.TrySampleSnow(worldPos, out SnowSample sample))
        {
            surface = Classify(sample);
            wet = sample.wetness > WetThreshold;
        }

        LastSurface = surface;
        LastWasWet = wet;

        PlayClip(surface, wet);
    }

    /// §12.1'deki karar zinciri, sırası dahil birebir.
    public static SnowFootstepSurface Classify(SnowSample sample)
    {
        if (sample.depth < HardDepth) return SnowFootstepSurface.Hard;

        if (sample.depth < ShallowDepth)
            return sample.density01 > PackedDensity
                ? SnowFootstepSurface.Packed
                : SnowFootstepSurface.Shallow;

        return sample.density01 < PowderDensity
            ? SnowFootstepSurface.Powder
            : SnowFootstepSurface.Deep;
    }

    void PlayClip(SnowFootstepSurface surface, bool wet)
    {
        if (source == null || clipSets == null) return;

        int index = (int)surface;
        if (index < 0 || index >= clipSets.Length) return;

        SnowFootstepClipSet set = clipSets[index];
        if (set == null) return;

        // Islak varyant yoksa kuru çalınıyor: eksik ses sessizlikten iyidir ve
        // eksikliği kolayca duyuluyor.
        AudioClip[] clips = wet && set.wet != null && set.wet.Length > 0 ? set.wet : set.dry;
        if (clips == null || clips.Length == 0) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clip, volume);
    }
}
