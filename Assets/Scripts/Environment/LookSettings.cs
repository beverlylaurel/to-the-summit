using UnityEngine;

/// The color and tone of the image. Four corners are defined and the values in between are
/// blended: (clear / storm) x (day / night).
[CreateAssetMenu(fileName = "LookSettings", menuName = "To The Summit/Look Settings")]
public class LookSettings : ScriptableObject
{
    [System.Serializable]
    public struct LookProfile
    {
        [Tooltip("Exposure (EV). Negative = dark.")]
        [Range(-4f, 2f)] public float exposure;
        [Range(-100f, 100f)] public float contrast;
        [Tooltip("Doygunluk. Negatif = solgun, kasvetli.")]
        [Range(-100f, 100f)] public float saturation;
        public Color colorFilter;

        [Tooltip("Color temperature. Negative = cool/blue.")]
        [Range(-100f, 100f)] public float temperature;
        [Range(-100f, 100f)] public float tint;

        [Tooltip("Cooling of the shadows. The REAL lever of gloom: everything left in shadow " +
                 "turns blue and heavy while sunlit surfaces keep their warmth. A global " +
                 "temperature shift cannot do this — it would cool the dawn as well.")]
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

    [Header("Clear weather")]
    public LookProfile clearDay = new()
    {
        // ON A CLEAR DAY THE EXPOSURE OPENS FOR THE SNOW, NOT FOR THE SCENE AVERAGE.
        //
        // Measured (10:00, cloud shadow off, fully sunlit slope): ground luma
        // 0.921, deviation 0.0151. Snow is crushed on ACES's shoulder; the
        // difference produced by the surface texture fits into 4 of 255 levels
        // and reads as ONE SOLID WHITE on screen. Pulled 0.85 stops down, the
        // luma is 0.839 and the deviation 0.0274 — the relief comes back and
        // the snow is still the brightest thing in the scene.
        //
        // In photography a snowy scene is exposed FOR THE SNOW as well; exposed
        // for the average, snow blows out.
        exposure = -0.85f, contrast = 6f, saturation = -8f,
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

    [Header("Storm")]
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

    [Header("Golden hour")]
    [Tooltip("The OWN grade of dawn and sunset. The night/day mix was printing this hour cool " +
             "(-11 temperature) and pale (-17 saturation): however red the palette was, it " +
             "arrived on screen as pastel. A sunset only looks like fire with a warm and " +
             "saturated grade. It only engages in clear weather; in overcast weather a sunset " +
             "really is grey.")]
    public LookProfile goldenHour = new()
    {
        exposure = -0.8f, contrast = 10f, saturation = 10f,
        colorFilter = new Color(1f, 0.97f, 0.9f),
        temperature = 14f, tint = 4f, shadowChill = 0.25f,
        bloom = 0.35f, bloomThreshold = 1.1f,
        grain = 0.07f
    };

    /// <param name="storm">0 clear weather, 1 full storm.</param>
    /// <param name="day">0 night, 1 day.</param>
    /// <param name="horizon">1 = the sun exactly on the horizon: golden hour.</param>
    public LookProfile Evaluate(float storm, float day, float horizon)
    {
        var clear = LookProfile.Lerp(clearNight, clearDay, day);
        var stormy = LookProfile.Lerp(stormNight, stormDay, day);
        var mixed = LookProfile.Lerp(clear, stormy, storm);

        // The golden hour does not die completely in a storm: even under thick cloud the western
        // horizon reddens a little, and cutting it to full grey looked digital. The share is
        // small — the storm still dominates; as the intensity rises the red is crushed but never reaches zero.
        return LookProfile.Lerp(mixed, goldenHour, horizon * Mathf.Lerp(1f, 0.45f, storm));
    }
}
