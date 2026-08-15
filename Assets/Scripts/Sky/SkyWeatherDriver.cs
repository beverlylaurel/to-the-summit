#if URP_PBSKY
using UnityEngine;
using UnityEngine.Rendering;

/// Dünya durumunu atmosfer hacmine çevirir. Gökyüzü havayı bilmez, hava gökyüzünü
/// bilmez; çeviri yalnız burada yapılır.
///
/// Güneşin yönü ve rengi buradan GEÇMİYOR: `TimeOfDay` ana ışığı sürüyor, paket de
/// gökyüzünü aynı ışıktan hesaplıyor. İkinci bir yol açmak "gökyüzü kızardı ama gölgeler
/// öğle yönünde" türü bir çelişki üretirdi.
public class SkyWeatherDriver : MonoBehaviour
{
    [Tooltip("Atmosfer ayarlarını taşıyan Volume bileşeni.")]
    [SerializeField] Volume skyVolume;

    [Tooltip("Yağış şiddetinin kaynağı.")]
    [SerializeField] WeatherState weather;

    [SerializeField] SkyWeatherSettings settings;

    PhysicallyBasedSky sky;

    public void Bind(Volume skyVolumeRef, WeatherState weatherRef, SkyWeatherSettings settingsRef)
    {
        skyVolume = skyVolumeRef;
        weather = weatherRef;
        settings = settingsRef;
    }

    void OnEnable()
    {
        if (skyVolume == null || weather == null || settings == null)
            throw new System.InvalidOperationException($"{nameof(SkyWeatherDriver)}: bağımlılıklar atanmadı.");

        if (!skyVolume.profile.TryGet(out sky))
            throw new System.InvalidOperationException($"{nameof(SkyWeatherDriver)}: profilde {nameof(PhysicallyBasedSky)} yok.");

        // Harmanlama `overrideState` kapalı alanları atlıyor; sürülen her alan açık olmalı.
        sky.aerosolDensity.overrideState = true;
    }

    void Update()
    {
        sky.aerosolDensity.value =
            Mathf.Lerp(settings.clearAerosol, settings.stormAerosol, weather.Precipitation);
    }
}
#endif
