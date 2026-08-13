using UnityEngine;

/// Görüntünün rengi ve tonu. Dört köşe tanımlanır, ara değerler harmanlanır:
/// (açık hava / fırtına) × (gündüz / gece).
[CreateAssetMenu(fileName = "LookSettings", menuName = "To The Summit/Look Settings")]
public class LookSettings : ScriptableObject
{
    [System.Serializable]
    public struct LookProfile
    {
        [Tooltip("Pozlama (EV). Negatif = karanlık.")]
        [Range(-4f, 2f)] public float exposure;
        [Range(-100f, 100f)] public float contrast;
        [Tooltip("Doygunluk. Negatif = solgun, kasvetli.")]
        [Range(-100f, 100f)] public float saturation;
        public Color colorFilter;

        [Tooltip("Renk sıcaklığı. Negatif = soğuk/mavi.")]
        [Range(-100f, 100f)] public float temperature;
        [Range(-100f, 100f)] public float tint;

        [Tooltip("Gölgelerin soğuması. Kasvetin ASIL kaldıracı: gölgede kalan her şey " +
                 "mavileşip ağırlaşırken güneş gören yüzeyler sıcaklığını korur. Global " +
                 "sıcaklık kaydırması bunu yapamaz — o şafağı da soğutur.")]
        [Range(0f, 1f)] public float shadowChill;

        [Range(0f, 3f)] public float bloom;
        [Range(0f, 3f)] public float bloomThreshold;

        [Range(0f, 1f)] public float grain;

        public static LookProfile Lerp(LookProfile a, LookProfile b, float t)
        {
            return new LookProfile
            {
                exposure = Mathf.Lerp(a.exposure, b.exposure, t),
                contrast = Mathf.Lerp(a.contrast, b.contrast, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                colorFilter = Color.Lerp(a.colorFilter, b.colorFilter, t),
                temperature = Mathf.Lerp(a.temperature, b.temperature, t),
                tint = Mathf.Lerp(a.tint, b.tint, t),
                shadowChill = Mathf.Lerp(a.shadowChill, b.shadowChill, t),
                bloom = Mathf.Lerp(a.bloom, b.bloom, t),
                bloomThreshold = Mathf.Lerp(a.bloomThreshold, b.bloomThreshold, t),
                grain = Mathf.Lerp(a.grain, b.grain, t)
            };
        }
    }

    [Header("Açık hava")]
    public LookProfile clearDay = new()
    {
        exposure = 0f, contrast = 6f, saturation = -8f,
        colorFilter = Color.white,
        temperature = -4f, tint = 0f, shadowChill = 0.45f,
        bloom = 0.35f, bloomThreshold = 1.1f,
        grain = 0.05f
    };

    public LookProfile clearNight = new()
    {
        exposure = -0.9f, contrast = 3f, saturation = -20f,
        colorFilter = new Color(0.92f, 0.95f, 1f),
        temperature = -14f, tint = 2f, shadowChill = 0.7f,
        bloom = 0.3f, bloomThreshold = 1.15f,
        grain = 0.14f
    };

    [Header("Fırtına")]
    public LookProfile stormDay = new()
    {
        exposure = -0.35f, contrast = 0f, saturation = -26f,
        colorFilter = new Color(0.96f, 0.97f, 1f),
        temperature = -10f, tint = 0f, shadowChill = 0.85f,
        bloom = 0.45f, bloomThreshold = 1f,
        grain = 0.12f
    };

    public LookProfile stormNight = new()
    {
        exposure = -1.3f, contrast = -3f, saturation = -36f,
        colorFilter = new Color(0.88f, 0.92f, 1f),
        temperature = -18f, tint = 2f, shadowChill = 1f,
        bloom = 0.4f, bloomThreshold = 1.05f,
        grain = 0.2f
    };

    [Header("Altın saat")]
    [Tooltip("Şafak ve batımın KENDİ kademesi. Gece↔gündüz karışımı bu saati soğuk " +
             "(-11 sıcaklık) ve soluk (-17 doygunluk) basıyordu: palet ne kadar kızıl " +
             "olursa olsun ekrana pastel geliyordu. Batış ancak sıcak ve doygun bir " +
             "kademeyle yangın gibi görünür. Yalnız açık havada devreye girer; kapalı " +
             "havada batış gerçekte de gridir.")]
    public LookProfile goldenHour = new()
    {
        exposure = -0.8f, contrast = 10f, saturation = 10f,
        colorFilter = new Color(1f, 0.97f, 0.9f),
        temperature = 14f, tint = 4f, shadowChill = 0.25f,
        bloom = 0.35f, bloomThreshold = 1.1f,
        grain = 0.07f
    };

    /// <param name="storm">0 açık hava, 1 tam fırtına.</param>
    /// <param name="day">0 gece, 1 gündüz.</param>
    /// <param name="horizon">1 = güneş tam ufukta: altın saat.</param>
    public LookProfile Evaluate(float storm, float day, float horizon)
    {
        var clear = LookProfile.Lerp(clearNight, clearDay, day);
        var stormy = LookProfile.Lerp(stormNight, stormDay, day);
        var mixed = LookProfile.Lerp(clear, stormy, storm);

        // Altın saat fırtınada tamamen ölmez: kalın bulutlu batışta da batı ufku
        // hafif kızarır, tam gri kesim dijital duruyordu. Pay küçük — fırtına yine
        // baskın; şiddet arttıkça kızıl ezilir ama sıfıra inmez.
        return LookProfile.Lerp(mixed, goldenHour, horizon * Mathf.Lerp(1f, 0.45f, storm));
    }
}
