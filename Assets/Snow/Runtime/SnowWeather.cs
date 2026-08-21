// ROL: yağış presetleri arasında yumuşak geçiş yapar ve simülasyonun okuduğu hava
// değerlerini üretir: yağış hızı, rüzgâr, ıslaklık, sıcaklık, kaplama.
// Çağıran: SnowManager (her karede okur ve compute'a bağlar).

using UnityEngine;

[DisallowMultipleComponent]
public class SnowWeather : MonoBehaviour
{
    /// Preset geçiş süresi, saniye (§10.3). Geçişler ANLIK OLMAZ.
    const float TransitionDuration = 45f;

    /// Rüzgâr esintisinin gürültü hızı (§10.3).
    const float GustFrequency = 0.09f;

    [Header("Presetler")]
    [Tooltip("Sırasıyla Clear, Light, Moderate, Heavy, Blizzard.")]
    [SerializeField] SnowWeatherPreset[] presets = new SnowWeatherPreset[0];

    [Header("Projenin hava zinciri")]
    [Tooltip("Bağlanırsa yağış şiddeti ve sıcaklık BURADAN geliyor; preset seçimi elle " +
             "yapılmıyor. Boşsa kar sistemi kendi presetini sürüyor.")]
    [SerializeField] WeatherState weatherState;

    [SerializeField] TemperatureField temperature;

    [Tooltip("Sıcaklığın okunacağı kot. Normalde oyuncu.")]
    [SerializeField] Transform altitudeSource;

    [SerializeField] int activePreset;

    [Header("Sıcaklık")]
    [Tooltip("Hava sıcaklığı, Celsius. Erime ve ıslaklık bundan türer.")]
    [SerializeField] float temperatureC = -6f;

    [Header("Rüzgâr")]
    [Tooltip("Rüzgârın yatay yönü, derece.")]
    [SerializeField] float windDirectionDegrees;

    /// PRESET KONUMU SÜREKLİ. 0 = Clear, 4 = Blizzard, aradaki her değer iki presetin
    /// karışımı.
    ///
    /// Ayrık preset + geçiş sayılacı yerine tek bir konum tutuluyor çünkü dış hava
    /// zinciri sürekli bir şiddet veriyor (0..1). Ayrık olsaydı şiddet salınırken
    /// preset iki değer arasında zıplar ve geçiş hiç bitmezdi.
    float presetPosition;
    float targetPosition;

    float snowfallSWERate;
    float windSpeedBase;
    float windSpeed;
    Vector3 windWS = Vector3.forward;
    float snowWetness;
    float coverage;
    float gustTime;

    /// Presetlerin en yükseği. Bir kez hesaplanıyor — Update'te dizi taramak yasak.
    float maxSWERate;

    public float SnowfallSWERate => snowfallSWERate;
    public float WindSpeed => windSpeed;
    public Vector3 WindWS => windWS;
    public float SnowWetness => snowWetness;
    public float TemperatureC => temperatureC;
    public float Coverage => coverage;
    public int PresetCount => presets.Length;
    public int ActivePreset => activePreset;

    /// Sirasi verilen preset. Yagis parcaciklari kapasite ve dogum hizini buradan okuyor;
    /// birikme hizi da ayni asset'ten cikiyor, iki kaynak olmasin diye (§15).
    public SnowWeatherPreset GetPreset(int index)
    {
        if (presets == null || index < 0 || index >= presets.Length) return null;
        return presets[index];
    }

    public string ActivePresetName =>
        presets != null && activePreset >= 0 && activePreset < presets.Length && presets[activePreset] != null
            ? presets[activePreset].name
            : "yok";

    void OnEnable()
    {
        if (presets == null || presets.Length == 0)
            throw new System.InvalidOperationException("SnowWeather: preset listesi boş.");

        activePreset = Mathf.Clamp(activePreset, 0, presets.Length - 1);
        presetPosition = activePreset;
        targetPosition = activePreset;

        maxSWERate = 0f;
        for (int i = 0; i < presets.Length; i++)
            if (presets[i] != null) maxSWERate = Mathf.Max(maxSWERate, presets[i].SnowfallSWERate);

        Evaluate(0f);

        // Lens özelliği bir renderer ASSET'İ; sahneye başka köprü yok.
        SnowLensFeature.ActiveWeather = this;
    }

    void OnDisable()
    {
        if (SnowLensFeature.ActiveWeather == this) SnowLensFeature.ActiveWeather = null;
    }

    /// Yeni bir yağış seviyesine geçer. Geçiş 45 saniye sürer.
    /// Dış hava zinciri bağlıyken bu çağrı yok sayılıyor — iki kaynak olmaz.
    public void SetPreset(int index)
    {
        if (weatherState != null) return;

        targetPosition = Mathf.Clamp(index, 0, presets.Length - 1);
    }

    /// Dış zincirin sürdüğü sürekli şiddet. 0 = Clear, 1 = Blizzard.
    public void SetIntensity(float intensity01)
    {
        targetPosition = Mathf.Clamp01(intensity01) * (presets.Length - 1);
    }

    /// Dış zincir bağlıyken yok sayılıyor — sıcaklık oradan geliyor.
    public void SetTemperature(float celsius)
    {
        if (weatherState != null) return;
        temperatureC = celsius;
    }

    /// Dış hava zinciri bağlı mı. Teşhis penceresi sürgüleri buna göre kilitliyor.
    public bool DrivenExternally => weatherState != null;

    // TEŞHİS KOLU: preset geçişi ve esinti de kar zamanıyla akıyor. Aksi halde 600x
    // hızda birikme saatler ilerlerken geçiş hâlâ 45 GERÇEK saniye bekletiyor ve
    // ölçüm rampanın ortasında alınıyor. Hız 1 iken hiçbir şey değişmiyor.
    void Update() => Evaluate(Time.deltaTime * Mathf.Max(SnowManager.SimulationSpeed, 0f));

    /// Projenin hava zincirinden okur. Bağlı değilse hiçbir şey yapmıyor.
    ///
    /// ŞİDDET = yağış x karlılık. Yağmur yollarını v2 taşımıyor; karın payı neyse
    /// kar sistemi o kadar çalışıyor.
    void ReadExternalWeather()
    {
        if (weatherState == null) return;

        SetIntensity(weatherState.Precipitation * weatherState.Snowiness);

        if (temperature != null && altitudeSource != null)
            temperatureC = temperature.At(altitudeSource.position.y);
    }

    void Evaluate(float deltaTime)
    {
        gustTime += deltaTime;

        ReadExternalWeather();

        // BÜTÜN MENZİL 45 SANİYEDE. Preset başına değil: Clear'dan Blizzard'a
        // geçiş dört adım ve her birine 45 saniye vermek üç dakika ederdi.
        float speed = (presets.Length - 1) / TransitionDuration;
        presetPosition = Mathf.MoveTowards(presetPosition, targetPosition, deltaTime * speed);

        int fromIndex = Mathf.Clamp(Mathf.FloorToInt(presetPosition), 0, presets.Length - 1);
        int toIndex = Mathf.Clamp(fromIndex + 1, 0, presets.Length - 1);

        activePreset = Mathf.Clamp(Mathf.RoundToInt(presetPosition), 0, presets.Length - 1);

        SnowWeatherPreset from = presets[fromIndex];
        SnowWeatherPreset to = presets[toIndex];

        if (from == null || to == null)
            throw new System.InvalidOperationException("SnowWeather: preset listesinde boş girdi var.");

        float blend = presetPosition - fromIndex;

        snowfallSWERate = Mathf.Lerp(from.SnowfallSWERate, to.SnowfallSWERate, blend);

        float speedMin = Mathf.Lerp(from.WindSpeedMin, to.WindSpeedMin, blend);
        float speedMax = Mathf.Lerp(from.WindSpeedMax, to.WindSpeedMax, blend);
        windSpeedBase = Mathf.Lerp(speedMin, speedMax, 0.5f);

        // Esinti: taban hızın 0.75–1.25 katı arasında yavaş salınım (§10.3).
        float gust = 0.75f + 0.5f * Mathf.PerlinNoise(gustTime * GustFrequency, 0.37f);
        windSpeed = windSpeedBase * gust;

        float radians = windDirectionDegrees * Mathf.Deg2Rad;
        windWS = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * windSpeed;

        // Taze karın ıslaklığı sıcaklıkla artıyor: 0 C'nin altı kuru, +3 C tamamen ıslak.
        snowWetness = Mathf.Clamp01(temperatureC / 3f);


        // Kaplama yağış şiddetiyle yükseliyor; nesneler gözle görülür şekilde kaplanır (§9).
        coverage = maxSWERate > 0f ? Mathf.Clamp01(snowfallSWERate / maxSWERate) : 0f;
    }
}
