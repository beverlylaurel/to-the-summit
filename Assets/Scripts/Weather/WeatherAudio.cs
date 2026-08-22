using System;
using UnityEngine;

/// Yağmur ve rüzgâr katmanlarını hava durumuna göre harmanlar.
/// Ayrık "hafif/şiddetli" durumu yoktur; katmanlar eşit-güç geçişiyle karışır.
public class WeatherAudio : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] WindField wind;

    [Header("Klipler")]
    [SerializeField] AudioClip rainLight;
    [SerializeField] AudioClip rainHeavy;
    [SerializeField] AudioClip[] windCalm;
    [SerializeField] AudioClip[] windStorm;

    [Header("Seviye")]
    [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
    [Tooltip("Savrulan yağmur yüzeye daha çok çarpar; rüzgârın yağmur sesine katkısı.")]
    [SerializeField, Range(0f, 0.5f)] float windRainBoost = 0.2f;

    // Rüzgâr seviyeleri bilerek serileştirilmiyor: Inspector'a girince sahnedeki
    // bileşen eski değerle donuyor ve koddaki değişiklik etkisiz kalıyor.
    const float WindVolume = 0.55f;   // rüzgârın yağmura göre seviyesi
    const float WindFloor = 0.14f;    // en dingin anda bile duyulan taban

    [Header("Zarf")]
    [Tooltip("Rüzgâr yükselirken yumuşatma süresi. Esinti hızlı gelir.")]
    [SerializeField] float windAttack = 0.4f;
    [Tooltip("Rüzgâr düşerken yumuşatma süresi. Esinti yavaş çekilir.")]
    [SerializeField] float windRelease = 2.5f;
    [Tooltip("Yağmur yükseklikle değişir, hızlı atağa gerek yok.")]
    [SerializeField] float rainSmoothing = 2f;

    [Header("Tını")]
    [Tooltip("Dingin rüzgârın parlaklığı. Düşük = boğuk.")]
    [SerializeField, Range(0f, 1f)] float windCalmBrightness = 0.35f;
    [Tooltip("Rüzgâr sertleştikçe perdenin oynama miktarı.")]
    [SerializeField, Range(0f, 0.3f)] float windPitchRange = 0.08f;

    AudioBand light;
    AudioBand heavy;
    AudioBand calm;
    AudioBand storm;

    public void Bind(WeatherState state, WindField windField,
        AudioClip lightClip, AudioClip heavyClip, AudioClip[] calmClips, AudioClip[] stormClips)
    {
        weather = state;
        wind = windField;
        rainLight = lightClip;
        rainHeavy = heavyClip;
        windCalm = calmClips;
        windStorm = stormClips;
    }

    void OnEnable()
    {
        if (weather == null)
            throw new InvalidOperationException($"{nameof(WeatherAudio)}: {nameof(weather)} atanmadı.");
        if (wind == null)
            throw new InvalidOperationException($"{nameof(WeatherAudio)}: {nameof(wind)} atanmadı.");
    }

    void Update()
    {
        EnsureBands();

        float precipitation = weather.Precipitation;

        // Sürekli şiddet hangi sesin çaldığını, esinti o sesin ne kadar yükseldiğini
        // belirler. İkisi ayrı okunuyor çünkü kulak ikisini ayrı duyar.
        float sustained = wind.Strength;
        float felt = Mathf.Clamp01(sustained * (1f + wind.Gust));

        DriveRain(precipitation, felt);
        DriveWind(sustained, felt);
    }

    void DriveRain(float precipitation, float felt)
    {
        float master = precipitation * masterVolume
                       * (1f + felt * windRainBoost);

        // Çiseleme boğuk, sağanak tiz
        float brightness = Mathf.Lerp(0.55f, 1f, precipitation);

        light.Drive(master * Mathf.Sqrt(1f - precipitation), brightness, 1f);
        heavy.Drive(master * Mathf.Sqrt(precipitation), brightness, 1f);
    }

    /// Seviye esintiyi izler, band geçişi sürekli şiddeti. Geçiş de esintiye bağlansaydı
    /// dingin ve fırtına karışımı sekiz saniyede bir yer değiştirir; rüzgârın sertleştiği
    /// değil, sesin oraya buraya kaydığı duyulurdu.
    void DriveWind(float sustained, float felt)
    {
        float master = Mathf.Lerp(WindFloor, 1f, felt) * masterVolume * WindVolume;

        // Hava hızlandıkça türbülans yüksek frekans üretir
        float brightness = Mathf.Lerp(windCalmBrightness, 1f, felt);
        float pitch = 1f + (felt - 0.5f) * 2f * windPitchRange;

        calm.Drive(master * Mathf.Sqrt(1f - sustained), brightness, pitch);
        storm.Drive(master * Mathf.Sqrt(sustained), brightness, pitch);
    }

    /// Play mode'da yeniden derleme bandları düşürebilir; kullanım anında doğrulanır.
    void EnsureBands()
    {
        if (light != null) return;

        // Reload sonrası eski band objeleri kalmış olabilir; ikizlenmeyi önle
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);

        light = new AudioBand(transform, "RainLight", new[] { rainLight }, rainSmoothing, rainSmoothing);
        heavy = new AudioBand(transform, "RainHeavy", new[] { rainHeavy }, rainSmoothing, rainSmoothing);
        calm = new AudioBand(transform, "WindCalm", windCalm, windAttack, windRelease);
        storm = new AudioBand(transform, "WindStorm", windStorm, windAttack, windRelease);
    }
}
