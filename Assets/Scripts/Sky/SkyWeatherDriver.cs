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
        sky.spaceRotation.value =
            Quaternion.AngleAxis(-time.Normalized * 360f, time.CelestialPole).eulerAngles;

        // YILDIZLARIN GÜNDÜZ SOLMASI GÜNEŞ YÜKSEKLİĞİNDEN. Shader parlak yıldızı −3°'ye,
        // en sönüğünü −18°'ye kadar bekletiyor; eşik kadire göre değişiyor.
        //
        // İKİNCİ BİR ZAMAN KAYNAĞI DEĞİL: değer `TimeOfDay`in güneş yönünden geliyor,
        // yani gölgelerle ve gökyüzü hesabıyla aynı tek durumdan.
        Shader.SetGlobalVector(StarFieldParamsId,
            new Vector4(time.SunDirection.y, 0f, 0f, 0f));
#endif
    }

#if URP_PBSKY
    static readonly int StarFieldParamsId = Shader.PropertyToID("_StarFieldParams");
#endif
}
