using UnityEngine;
using UnityEngine.Rendering;

/// Dünya durumunu atmosfer hacmine çevirir. Gökyüzü havayı bilmez, hava gökyüzünü
/// bilmez; çeviri yalnız burada yapılır.
///
/// Güneşin yönü ve rengi buradan GEÇMİYOR: `TimeOfDay` iki ışığı da sürüyor, paket de
/// gökyüzünü aynı ışıklardan hesaplıyor. İkinci bir yol açmak "gökyüzü kızardı ama
/// gölgeler öğle yönünde" türü bir çelişki üretirdi.
///
/// Sınıf koşulsuz derleniyor, yalnız gövdesi pakete bağlı: `Bind` imzasında paket tipi
/// yok, böylece sahne kurulumu ve F1 paneli tanım henüz kurulmamışken de derleniyor.
public class SkyWeatherDriver : MonoBehaviour
{
    [Tooltip("Atmosfer ayarlarını taşıyan Volume bileşeni.")]
    [SerializeField] Volume skyVolume;

    [Tooltip("Yağış şiddetinin kaynağı.")]
    [SerializeField] WeatherState weather;

    [Tooltip("Yıldız alanının döndüğü ekseni ve saati veren bileşen.")]
    [SerializeField] TimeOfDay time;

    [SerializeField] SkyWeatherSettings settings;

#if URP_PBSKY
    PhysicallyBasedSky sky;
#endif

    /// TEŞHİS — GEÇİCİ. Yıldız alanının dönüşünü dondurur.
    public static bool FreezeStarRotation { get; set; }

    public void Bind(Volume skyVolumeRef, WeatherState weatherRef, TimeOfDay timeRef,
        SkyWeatherSettings settingsRef)
    {
        skyVolume = skyVolumeRef;
        weather = weatherRef;
        time = timeRef;
        settings = settingsRef;
    }

    void OnEnable()
    {
        if (skyVolume == null || weather == null || time == null || settings == null)
            throw new System.InvalidOperationException($"{nameof(SkyWeatherDriver)}: bağımlılıklar atanmadı.");

#if URP_PBSKY
        if (!skyVolume.profile.TryGet(out sky))
            throw new System.InvalidOperationException($"{nameof(SkyWeatherDriver)}: profilde {nameof(PhysicallyBasedSky)} yok.");

        // Harmanlama `overrideState` kapalı alanları atlıyor; sürülen her alan açık olmalı.
        sky.aerosolDensity.overrideState = true;
        sky.spaceRotation.overrideState = true;
#endif
    }

    void Update()
    {
#if URP_PBSKY
        sky.aerosolDensity.value =
            Mathf.Lerp(settings.clearAerosol, settings.stormAerosol, weather.Precipitation);

        // YILDIZLAR GÖK KUTBU ETRAFINDA DÖNER, günde bir tur. Eksen güneşinkiyle aynı;
        // ayrı bir eksen verilseydi güneşle yıldızlar farklı yönlerde dönerdi.
        //
        // Shader arama yönünü döndürüyor (`mul(-V, _SpaceRotation)`), yani yıldız alanı
        // ters yöne kayıyor — açının işareti bu yüzden negatif.
        // TEŞHİS ANAHTARI — GEÇİCİ. Gece işinde çalışma zamanında değişen üç şeyden biri
        // buydu; yıldız dokusu izolasyonla elendi, geriye bu kaldı.
        sky.spaceRotation.value = FreezeStarRotation
            ? Vector3.zero
            : Quaternion.AngleAxis(-time.Normalized * 360f, time.CelestialPole).eulerAngles;
#endif
    }
}
