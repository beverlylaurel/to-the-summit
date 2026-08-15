using UnityEngine;
using UnityEngine.Rendering;

/// Dünya durumunu atmosfer hacmine çevirir. Gökyüzü havayı bilmez, hava gökyüzünü
/// bilmez; çeviri yalnız burada yapılır.
///
/// Güneşin yönü ve rengi buradan GEÇMİYOR: `TimeOfDay` ana ışığı sürüyor, paket de
/// gökyüzünü aynı ışıktan hesaplıyor. İkinci bir yol açmak "gökyüzü kızardı ama gölgeler
/// öğle yönünde" türü bir çelişki üretirdi.
///
/// Sınıf koşulsuz derleniyor, yalnız gövdesi pakete bağlı: `Bind` imzasında paket tipi
/// yok, böylece sahne kurulumu ve F1 paneli tanım henüz kurulmamışken de derleniyor.
public class SkyWeatherDriver : MonoBehaviour
{
    [Tooltip("Atmosfer ayarlarını taşıyan Volume bileşeni.")]
    [SerializeField] Volume skyVolume;

    [Tooltip("Yağış şiddetinin kaynağı.")]
    [SerializeField] WeatherState weather;

    [SerializeField] SkyWeatherSettings settings;

#if URP_PBSKY
    PhysicallyBasedSky sky;
#endif

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

#if URP_PBSKY
        if (!skyVolume.profile.TryGet(out sky))
            throw new System.InvalidOperationException($"{nameof(SkyWeatherDriver)}: profilde {nameof(PhysicallyBasedSky)} yok.");

        // Harmanlama `overrideState` kapalı alanları atlıyor; sürülen her alan açık olmalı.
        sky.aerosolDensity.overrideState = true;
#endif
    }

    void Update()
    {
#if URP_PBSKY
        sky.aerosolDensity.value =
            Mathf.Lerp(settings.clearAerosol, settings.stormAerosol, weather.Precipitation);
#endif
    }
}
