// ROL: bu oyunun hava/ruzgar/gundongu/atmosfer sistemlerini deniz sistemine
// baglar. Yalnizca OKUR.
// Cagiran: SeaManager (ISeaEnvironmentSource olarak).

using UnityEngine;

/// KÖPRÜ TAHMİN EDİLMEZ, ÖLÇÜLÜR.
///
/// Spec §3.2: "Kullanıcının oyununa özel yapıştırıcı. Sen bunu tahmin
/// etmeye çalışma." Aşağıdaki bağlar bu projenin gerçek API'sinden ölçüldü:
///
///   WindField.PrevailingDirection  → WindDirection
///   WindField.FreeAirSpeed         → WindSpeed  (m/s, serbest hava)
///   TimeOfDay.SunHeight            → SunElevation01  (SunDirection.y)
///   AtmosphereController.Coverage  → CloudCover01
///   WeatherState.Precipitation     → PrecipIntensity01
///   TemperatureField.At(y)         → PrecipKind (kar/yağmur ayrımı)
///
/// GÖK RENGİ BAĞLANMADI. Bu projede gökyüzü hacimsel bulut sisteminden ve
/// skybox'tan geliyor; tek bir "zenit rengi" property'si yok. Manuel değer
/// kullanılıyor ve `TODO(kullanici)` bırakıldı — spec §3.2 bunu açıkça
/// izin veriyor: "Manuel varsayılan değerlerle sistem baştan sona çalışır."
///
/// BAĞIMLILIK INSPECTOR'DAN. `FindObjectOfType` ve singleton yok
/// (`CLAUDE.md`). Bağlanmamış bir kaynak manuel değere düşüyor.
[DisallowMultipleComponent]
public class SeaEnvironmentBridge : MonoBehaviour, ISeaEnvironmentSource
{
    [Header("Oyunun mevcut sistemleri")]
    [SerializeField] WindField wind;
    [SerializeField] WeatherState weather;
    [SerializeField] TimeOfDay timeOfDay;
    [SerializeField] AtmosphereController atmosphere;
    [SerializeField] TemperatureField temperature;
    [SerializeField] Light sunLight;

    [Header("Köprü kurulana kadar manuel değerler (spec §3.2)")]
    [SerializeField] Vector3 manualWindDirection = new Vector3(1f, 0f, 0f);
    [SerializeField] float manualWindSpeed = 8f;

    /// TODO(kullanici): gökyüzü rengi bu projede tek bir property'de
    /// durmuyor. Hacimsel bulut sistemi veya skybox'tan bir zenit/ufuk rengi
    /// çıkarılabilirse buraya bağlanmalı.
    ///
    /// Varsayılan [KAYNAK: Tessendorf 2004 §6.3 örnek shader —
    /// `sky = color(0.69, 0.84, 1)`].
    [SerializeField] Color manualSkyColor = new Color(0.69f, 0.84f, 1.00f);
    [SerializeField] Color manualHorizonColor = new Color(0.80f, 0.86f, 0.92f);

    [SerializeField, Range(0f, 1f)] float manualCloudCover = 0.3f;
    [SerializeField, Range(0f, 1f)] float manualFogDensity = 0.2f;

    /// Yağışın kar mı yağmur mu olduğu sıcaklıktan türüyor — ayrı bir
    /// "yağış türü" değişkeni yok ve kurulmuyor (ikinci kaynak olurdu).
    [Tooltip("Bu sıcaklığın altında yağış kar sayılır (°C).")]
    [SerializeField] float snowThresholdC = 0f;

    [Tooltip("Bu sıcaklığın üstünde yağış yağmur sayılır (°C). Arası sulu kar.")]
    [SerializeField] float rainThresholdC = 2f;

    // ------------------------------------------------------------ rüzgâr

    public Vector3 WindDirection
    {
        get
        {
            if (wind == null) return manualWindDirection.normalized;

            Vector3 d = wind.PrevailingDirection;
            d.y = 0f;

            return d.sqrMagnitude > 1e-6f ? d.normalized : manualWindDirection.normalized;
        }
    }

    /// U10 — 10 m referans yüksekliğindeki rüzgâr hızı.
    ///
    /// `FreeAirSpeed` serbest hava hızı; arazi maruziyeti (`TerrainWindShelter`)
    /// uygulanmamış hâli. Deniz açık su üstünde, yani siper yok — doğru olan
    /// bu. `Velocity.magnitude` kullanılsaydı yerel gust'lar spektruma
    /// girerdi ve spec §3.4 onu yasaklıyor.
    public float WindSpeed => wind != null ? wind.FreeAirSpeed : manualWindSpeed;

    // ------------------------------------------------------- gece/gündüz

    public Light Sun => sunLight;

    /// `TimeOfDay.SunHeight` zaten `SunDirection.y`, yani güneşin yükseklik
    /// sinüsü. `saturate` gece negatifi kesiyor.
    public float SunElevation01 =>
        timeOfDay != null ? Mathf.Clamp01(timeOfDay.SunHeight) : 0.5f;

    // ---------------------------------------------------------- atmosfer

    public Color SkyColor => manualSkyColor;

    public Color HorizonColor => manualHorizonColor;

    public float CloudCover01 =>
        atmosphere != null ? Mathf.Clamp01(atmosphere.Coverage) : manualCloudCover;

    /// Sis yoğunluğu deniz tarafından yalnız bilgi olarak okunuyor; sisin
    /// kendisi URP'nin `MixFog`'uyla uygulanıyor (spec §3.5).
    public float FogDensity01 => manualFogDensity;

    // ------------------------------------------------------------- yağış

    public SeaPrecipitationKind PrecipKind
    {
        get
        {
            if (weather == null || weather.Precipitation <= 0.001f)
                return SeaPrecipitationKind.None;

            if (temperature == null) return SeaPrecipitationKind.Rain;

            // Deniz seviyesindeki sıcaklık — deniz orada.
            float c = temperature.At(transform.position.y);

            if (c <= snowThresholdC) return SeaPrecipitationKind.Snow;
            if (c >= rainThresholdC) return SeaPrecipitationKind.Rain;

            return SeaPrecipitationKind.Sleet;
        }
    }

    public float PrecipIntensity01 =>
        weather != null ? Mathf.Clamp01(weather.Precipitation) : 0f;
}
