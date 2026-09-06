// ROL: derin karda hareketin ne kadar yavaslayacagini YAYINLAR. Karakter
// controller'ina KENDISI baglanmaz (spec 19.2, 1.4).
// Caginan: sahne; degeri okuyan taraf kullanicinin sectigi bilesendir.

using UnityEngine;

/// BAGLAMAK KULLANICININ ISI (spec 19.2: "Bunu character controller'a SEN
/// baglama"). Bilesen yalniz bir sayi yayinliyor; mevcut hareket koduna tek
/// satir eklenmedi.
[DisallowMultipleComponent]
public class SnowMovementModifier : MonoBehaviour
{
    [SerializeField] SnowSampler sampler;

    [Tooltip("Ayak konumu.")]
    [SerializeField] Transform footAnchor;

    GroundSurfaceContact surfaceContact;

    /// 1 = yavaslama yok. Kar yoksa veya veri gelmediyse hep 1.
    public float SpeedMultiplier { get; private set; } = 1f;

    /// Son okunan ornek — tesis ve pusskurtme bunu da kullaniyor.
    public SnowSample LastSample { get; private set; }

    void OnEnable() => surfaceContact = GroundSurfaceContact.Require(this);

    /// Spec 19.2 birebir. Sig karda yavaslama yok; derin ve GEVSEK karda en
    /// cok yavaslama var. Sikismis patikada kar derin olsa bile yavaslatmiyor
    /// - patika acmanin odulu bu.
    public static float SpeedFor(SnowSample sample)
    {
        if (!sample.Valid) return 1f;

        return 1f - Mathf.Clamp01((sample.Depth - 0.10f) / 0.60f)
                    * 0.45f * (1f - sample.Density01);
    }

    void LateUpdate()
    {
        Vector3 p = footAnchor != null ? footAnchor.position : transform.position;

        if (surfaceContact == null || !surfaceContact.SupportsSnow || sampler == null
            || !sampler.TrySampleSnow(p, out SnowSample sample))
        {
            SpeedMultiplier = 1f;
            LastSample = default;
            return;
        }

        LastSample = sample;
        SpeedMultiplier = SpeedFor(sample);
    }
}
