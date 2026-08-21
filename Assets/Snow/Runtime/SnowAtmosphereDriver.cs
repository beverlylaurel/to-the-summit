// ROL: yağışın atmosfere yansıması (§10.2). Sis, güneş, ortam ışığı.
// Sadece parçacık yetmez: fırtına hissi ışığın ve sisin de değişmesinden geliyor.
// Çağıran: kimse — kendi LateUpdate'inde çalışır.
//
// DİKKAT: bu projenin kendi atmosfer zinciri var (`AtmosphereController`,
// `TimeOfDay`). İkisi aynı anda açıkken sis ve güneş iki kaynaktan sürülür.
// Karar ve tetikleyici `DECISIONS.md` → "Kar v2 kendi hava kaynağını sürüyor".

using UnityEngine;

[DisallowMultipleComponent]
public class SnowAtmosphereDriver : MonoBehaviour
{
    /// Fırtınada güneşin soğuması: 6500 K → 7800 K.
    static readonly Color ClearSunColor = Mathf.CorrelatedColorTemperatureToRGB(6500f);
    static readonly Color StormSunColor = Mathf.CorrelatedColorTemperatureToRGB(7800f);

    /// Kar fırtınasında sis rengi gri-mavi.
    static readonly Color StormFogColor = new Color(0.62f, 0.66f, 0.72f);

    [Header("Bağımlılıklar")]
    [SerializeField] SnowWeather weather;

    [Tooltip("Güneş. Fırtınada kısılıyor ve soğuyor.")]
    [SerializeField] Light sun;

    [Header("Devreye alma")]
    [Tooltip("Projenin kendi atmosfer sistemi varken kapalı tutulur; iki kaynak " +
             "aynı sisi süremez.")]
    [SerializeField] bool driveFog;

    [SerializeField] bool driveSun;
    [SerializeField] bool driveAmbient;

    [Header("Taban değerler")]
    [Tooltip("Açık havadaki sis yoğunluğu. Preset çarpanı bununla çarpılıyor.")]
    [SerializeField] float baseFogDensity = 0.005f;

    [SerializeField] float baseSunIntensity = 1f;
    [SerializeField] float baseAmbientIntensity = 1f;

    float stormness;

    /// 0 = açık, 1 = blizzard. Preset yerine YAĞIŞ ŞİDDETİNDEN türüyor: geçiş
    /// sırasında atmosfer de kademeli değişsin.
    public float Stormness => stormness;

    void OnEnable()
    {
        if (weather == null)
            throw new System.InvalidOperationException("SnowAtmosphereDriver: SnowWeather atanmadı.");

        Apply();
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        // Kaplama zaten yağış şiddetinin en yüksek presete oranı; fırtına ölçüsü de o.
        stormness = Mathf.Clamp01(weather.Coverage);

        SnowWeatherPreset preset = weather.GetPreset(weather.ActivePreset);
        float fogMultiplier = preset != null ? preset.FogMultiplier : 1f;

        if (driveFog)
        {
            RenderSettings.fogDensity = baseFogDensity * fogMultiplier;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.ambientSkyColor, StormFogColor, stormness);
        }

        if (driveSun && sun != null)
        {
            sun.intensity = baseSunIntensity * Mathf.Lerp(1f, 0.25f, stormness);
            sun.color = Color.Lerp(ClearSunColor, StormSunColor, stormness);
        }

        // ORTAM ŞİDDETİ FIRTINADA ARTIYOR. Işık kaybolmuyor, dağılıyor: bulut ve kar
        // güneşi her yöne saçıyor, gölgeler yumuşuyor.
        if (driveAmbient)
            RenderSettings.ambientIntensity = baseAmbientIntensity * Mathf.Lerp(1f, 1.35f, stormness);
    }
}
