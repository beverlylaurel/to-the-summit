using UnityEngine;
using UnityEngine.Rendering;

/// DÜNYA DURUMUNU BULUT AYARLARINA ÇEVİRİR. Bulutları çizen sistem bir render özelliği;
/// hava, rüzgâr ve saati kendisi okuyamaz. Bu bileşen tek yönlü köprü: dünyayı okur,
/// bulut Volume'una yazar. Ters yön yok — bulutun ne yaptığını `CloudLayerProbe` söyler.
///
/// Bağlar teker teker ekleniyor; her eklemede buluta tekrar bakılıyor
/// (bkz. `CLOUDS_REBUILD.md`).
public class CloudWeatherDriver : MonoBehaviour
{
    [Tooltip("Bulut ayarlarını taşıyan Volume.")]
    [SerializeField] Volume cloudVolume;

    [Tooltip("Rüzgâr alanı. Serbest hava hızı ve hâkim yön buradan.")]
    [SerializeField] WindField wind;

    [Tooltip("Fırtına kaynağı. Yoğunluk buradan.")]
    [SerializeField] AltitudeWeatherDriver weatherDriver;

    [Tooltip("Küresel kapsamanın TEK kaynağı. Kural orada, burada yalnız tüketiliyor.")]
    [SerializeField] AtmosphereController atmosphere;

    [Tooltip("Dünya durumunun bulut ayarlarına çevrim katsayıları.")]
    [SerializeField] CloudWeatherSettings settings;

    /// Portun `globalSpeed`'i km/h: geçiş `deltaTime`'ı 1/3.6 ile çarpıyor
    /// (`VolumetricCloudsURP`, `deltaTime *= -0.277778f`). Rüzgâr alanı m/s veriyor.
    const float MetersPerSecondToKilometersPerHour = 3.6f;

    VolumetricClouds clouds;

    void OnEnable()
    {
        if (cloudVolume == null || wind == null || weatherDriver == null
            || atmosphere == null || settings == null)
            throw new System.InvalidOperationException($"{nameof(CloudWeatherDriver)}: bağımlılıklar atanmadı.");

        if (!cloudVolume.profile.TryGet(out clouds))
            throw new System.InvalidOperationException($"{nameof(CloudWeatherDriver)}: profilde {nameof(VolumetricClouds)} yok.");

        // Harmanlama `overrideState` kapalı alanları atlıyor; sürülen her alan açık olmalı.
        clouds.globalSpeed.overrideState = true;
        clouds.globalOrientation.overrideState = true;
        clouds.cloudCoverage.overrideState = true;
        clouds.densityMultiplier.overrideState = true;
    }

    void Update()
    {
        // Yön HÂKİM rüzgârdan, anlık hızdan değil: esinti yönü saliseler içinde yalpalıyor
        // ve bulut kütlesi öyle davranmaz.
        Vector3 heading = wind.PrevailingDirection;
        float degrees = Mathf.Atan2(heading.z, heading.x) * Mathf.Rad2Deg;
        if (degrees < 0f) degrees += 360f;

        clouds.globalSpeed.value = wind.FreeAirSpeed * MetersPerSecondToKilometersPerHour;
        clouds.globalOrientation.value = degrees;

        // KAPSAMA ATMOSFERDEN. Kural orada tek yerde duruyor: fırtına kütlesi, kuru hava
        // ritmi, açık pencere ve test kilidi. Burada ikinci bir eşleme kurulsaydı gökyüzü
        // "kapalı" derken bulutlar "açık" diyebilirdi.
        clouds.cloudCoverage.value = atmosphere.Coverage;

        // Yoğunluk `CloudMass`'ten — `WeatherState.Precipitation` DEĞİL. Yağış tavanla
        // kesilmiş: bulutun üstüne çıkınca sıfırlanıyor ve kapsama → tepe → tavan kesimi →
        // yağış → kapsama döngüsü kurulurdu. `CloudMass` yağışın geciken hâli, kesildiğinde
        // bulut hemen dağılmıyor. Sözleşme `AltitudeWeatherDriver.StormIntensity`'de yazılı.
        clouds.densityMultiplier.value =
            Mathf.Lerp(settings.calmDensity, settings.stormDensity, weatherDriver.CloudMass);
    }

    public void Bind(Volume cloudVolumeRef, WindField windRef,
        AltitudeWeatherDriver weatherDriverRef, AtmosphereController atmosphereRef,
        CloudWeatherSettings settingsRef)
    {
        cloudVolume = cloudVolumeRef;
        wind = windRef;
        weatherDriver = weatherDriverRef;
        atmosphere = atmosphereRef;
        settings = settingsRef;
    }
}
