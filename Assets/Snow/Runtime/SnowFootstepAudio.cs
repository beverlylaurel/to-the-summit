// ROL: adımın hangi kar sesini çalacağını seçer ve çalar (spec §19.1).
// Çağıran: karakterin adım olayı.

using UnityEngine;

/// Karın üstündeki adımın ses türü (spec §19.1).
public enum SnowFootstepSurface
{
    /// Kar yok — mevcut zemin sesi çalmalı, kar sistemi karışmıyor.
    None,
    Packed,
    Shallow,
    Powder,
    Deep,

    /// Sağlam kabuk: üstünde neredeyse hiç batmadan yürünüyor (spec §18.3).
    Crust,
}

/// PROJENİN İLK AYAK SESİ SİSTEMİ. Spec §19.1 "mevcut ayak sesi sistemine
/// yeni bir yüzey tipi olarak eklenir" diyor ama projede ayak sesi sistemi
/// YOK. Bu yüzden burada yalnız KAR sesleri var; kar yoksa `None` dönüyor ve
/// karar çağırana bırakılıyor.
///
/// Klip atanmamışsa hiçbir şey çalmıyor — sessiz kalmak yanlış ses çalmaktan
/// iyidir.
[DisallowMultipleComponent]
public class SnowFootstepAudio : MonoBehaviour
{
    [Header("Bağımlılıklar")]
    [SerializeField] SnowSampler sampler;
    [SerializeField] AudioSource source;

    [Tooltip("Ayak konumu.")]
    [SerializeField] Transform footAnchor;

    [Header("Klipler")]
    [SerializeField] AudioClip[] packed;
    [SerializeField] AudioClip[] shallow;
    [SerializeField] AudioClip[] powder;
    [SerializeField] AudioClip[] deep;
    [SerializeField] AudioClip[] crust;

    [Tooltip("Islak varyantlar. Boşsa kuru klipler çalıyor.")]
    [SerializeField] AudioClip[] packedWet;
    [SerializeField] AudioClip[] shallowWet;
    [SerializeField] AudioClip[] powderWet;
    [SerializeField] AudioClip[] deepWet;

    /// Son adımda seçilen yüzey — teşhis için.
    public SnowFootstepSurface LastSurface { get; private set; }

    /// YÜZEY SEÇİMİ SPEC §19.1 TABLOSU BİREBİR. Sıra önemli: sığ ve sıkışmış
    /// kontrolü toz kontrolünden ÖNCE geliyor, yoksa ince ama gevşek kar
    /// "toz" sayılır ve derin kar sesi çalar.
    public static SnowFootstepSurface SelectSurface(SnowSample sample)
    {
        if (!sample.Valid) return SnowFootstepSurface.None;

        if (sample.Depth < 0.02f) return SnowFootstepSurface.None;

        // KABUK ÖNCE. Kabuklu yüzeyde altındaki karın derinliği ne olursa
        // olsun duyulan ses kabuğun sesidir (spec §18.3).
        if (sample.Crust > SnowConstants.CrustSolid) return SnowFootstepSurface.Crust;

        if (sample.Depth < 0.08f && sample.Density01 > 0.55f) return SnowFootstepSurface.Packed;
        if (sample.Depth < 0.08f) return SnowFootstepSurface.Shallow;
        if (sample.Density01 < 0.30f) return SnowFootstepSurface.Powder;

        return SnowFootstepSurface.Deep;
    }

    /// Islak varyant eşiği (spec §19.1).
    public static bool IsWet(SnowSample sample) => sample.Wetness > 0.55f;

    /// Karakterin adım olayından çağrılıyor. `false` dönerse kar sesi yok;
    /// çağıran kendi zemin sesini çalmalı.
    public bool PlayFootstep()
    {
        Vector3 p = footAnchor != null ? footAnchor.position : transform.position;

        if (sampler == null || !sampler.TrySampleSnow(p, out SnowSample sample))
        {
            LastSurface = SnowFootstepSurface.None;
            return false;
        }

        LastSurface = SelectSurface(sample);
        if (LastSurface == SnowFootstepSurface.None) return false;

        AudioClip[] bank = ClipsFor(LastSurface, IsWet(sample));
        if (bank == null || bank.Length == 0 || source == null) return false;

        source.PlayOneShot(bank[Random.Range(0, bank.Length)]);
        return true;
    }

    AudioClip[] ClipsFor(SnowFootstepSurface surface, bool wet)
    {
        AudioClip[] wetBank = surface switch
        {
            SnowFootstepSurface.Packed => packedWet,
            SnowFootstepSurface.Shallow => shallowWet,
            SnowFootstepSurface.Powder => powderWet,
            SnowFootstepSurface.Deep => deepWet,
            _ => null,
        };

        if (wet && wetBank != null && wetBank.Length > 0) return wetBank;

        return surface switch
        {
            SnowFootstepSurface.Packed => packed,
            SnowFootstepSurface.Shallow => shallow,
            SnowFootstepSurface.Powder => powder,
            SnowFootstepSurface.Deep => deep,
            SnowFootstepSurface.Crust => crust,
            _ => null,
        };
    }
}
