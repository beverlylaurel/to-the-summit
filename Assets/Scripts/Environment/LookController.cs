using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Hava ve günün saatine göre renk düzenlemesini sürer.
/// Hava sistemi ve gün döngüsü birbirini tanımaz; ikisini burada tüketiyoruz.
[ExecuteAlways]
[RequireComponent(typeof(Volume))]
public class LookController : MonoBehaviour
{
    /// POZLAMA UYUMU. Göz karanlığa açılır ama farkın tamamını kapatmaz; `adaptShare`
    /// kapatılan pay, `exposureCap` açılabilecek en fazla durak.
    ///
    /// Tavan 0.6'ydı ve o sayı gökyüzü FİZİKSEL OLARAK kararmıyorken ayarlanmıştı. Çifte
    /// güneş sönümü kaldırılıp alacakaranlık gerçek seviyesine inince 0.6 EV hiçbir şeyi
    /// açmaz oldu: gök verisi vardı ama ekranda zifiri karanlık görünüyordu.
    [Header("Pozlama uyumu")]
    [Tooltip("Karanlıkta açılabilecek en fazla durak (EV).")]
    [SerializeField, Range(0f, 6f)] float exposureCap = 2.5f;

    /// 0.35'TE KALIYOR. Bir ara 0.60'a çıkarıldı: gece göğü ton eğrisinin dibinde
    /// oturuyordu, veri tüm görüş alanında 1 duraktan az değişirken ekranda "siyah
    /// bölge / normal gök" diye ikiye ayrılıyordu ve +1 EV bunu kapatıyordu.
    ///
    /// AMA POZLAMA YANLIŞ ALET. Karanlık ucu kaldırırken parlak ucu da kaldırdı; ay
    /// ışığındaki kar ve tüm gece sahnesi gereğinden aydınlık oldu. Karanlık ucu tek
    /// başına kaldıran şey ton eğrisidir: gece profilinde kontrast düşürüldü.
    [Tooltip("Işık farkının kapatılan payı. 1 = tam normalizasyon, şafağı öğlene çevirir.")]
    [SerializeField, Range(0f, 1f)] float adaptShare = 0.35f;

    [SerializeField] LookSettings look;
    [SerializeField] WeatherState weather;
    [SerializeField] TimeOfDay time;

    [Header("Önizleme (yalnızca editörde)")]
    [Tooltip("Açıkken hava ve saat sistemleri yerine aşağıdaki değerler kullanılır.")]
    [SerializeField] bool preview;
    [SerializeField, Range(0f, 1f)] float previewStorm = 0.8f;
    [SerializeField, Range(0f, 1f)] float previewDay = 0.6f;


    ColorAdjustments colorAdjustments;
    WhiteBalance whiteBalance;
    ShadowsMidtonesHighlights shadows;
    Bloom bloom;
    FilmGrain filmGrain;
    Tonemapping tonemapping;

    public LookSettings Look => look;

    public void Bind(LookSettings settings, WeatherState weatherState, TimeOfDay timeOfDay)
    {
        look = settings;
        weather = weatherState;
        time = timeOfDay;

        Initialize();
    }

    /// Önizleme değerlerini dışarıdan sürmek için (ayar penceresi kullanır)
    public void SetPreview(bool enabled, float storm, float day)
    {
        preview = enabled;
        previewStorm = storm;
        previewDay = day;
        Apply();
    }

    /// ExecuteAlways yüzünden OnEnable, AddComponent anında çalışır — o an Bind henüz
    /// çağrılmamış olabilir. Kurulum bu yüzden ikisinden hangisi önce gelirse orada yapılır.
    void OnEnable() => Initialize();

    void Initialize()
    {
        if (look == null) return;

        EnsureOverrides();
        Apply();
    }

    void Update() => Apply();

    /// Volume profilinde gereken efektler yoksa eklenir. Profil asset olarak diskte durur,
    /// bu yüzden bir kez eklenir ve kalıcıdır.
    void EnsureOverrides()
    {
        var profile = GetComponent<Volume>().profile;
        if (profile == null)
            throw new InvalidOperationException($"{nameof(LookController)}: Volume profili yok.");

        colorAdjustments = Ensure<ColorAdjustments>(profile);
        whiteBalance = Ensure<WhiteBalance>(profile);
        shadows = Ensure<ShadowsMidtonesHighlights>(profile);
        bloom = Ensure<Bloom>(profile);
        filmGrain = Ensure<FilmGrain>(profile);
        tonemapping = Ensure<Tonemapping>(profile);

        tonemapping.mode.overrideState = true;
        tonemapping.mode.value = TonemappingMode.ACES;
    }

    /// Ortam probe'unun zenit yönündeki parlaklığı. Gökyüzü paketi probe'u her kare
    /// gökyüzünden pişiriyor, yani bu sahnenin GERÇEK gök aydınlığı.
    static readonly Vector3[] ZenithDirection = { Vector3.up };
    static readonly Color[] ZenithResult = new Color[1];

    static float AmbientZenithLuminance()
    {
        RenderSettings.ambientProbe.Evaluate(ZenithDirection, ZenithResult);

        Color zenith = ZenithResult[0];
        return zenith.r * 0.2126f + zenith.g * 0.7152f + zenith.b * 0.0722f;
    }

    static T Ensure<T>(VolumeProfile profile) where T : VolumeComponent
        => profile.TryGet(out T component) ? component : profile.Add<T>(true);

    void Apply()
    {
        if (look == null || colorAdjustments == null) return;

        float storm, day, horizon;

        if (preview || weather == null || time == null)
        {
            storm = previewStorm;
            day = previewDay;
            horizon = 0f;
        }
        else
        {
            storm = Mathf.Clamp01(weather.Precipitation);
            day = time.DayFactor;
            horizon = time.HorizonFactor;
        }

        var profile = look.Evaluate(storm, day, horizon);

        // POZLAMA UYUMU. Işık artık fizikten geliyor ve şafakta huzme geçirgenliği
        // 0.11'e, gece sıfıra iniyor — fizik doğru ama sabit pozlamada ekran sönük
        // kalıyor. Gerçek hayatta şafak bulutlarının parlak görünmesinin sebebi ışığın
        // güçlü olması değil, gözün o karanlığa göre AÇILMASIDIR; fotoğrafta da
        // pozlama göğe göre ayarlanır.
        //
        // Seviye ekrandan okunmuyor: sahnenin ışığını zaten biliyoruz (huzme + gök).
        // Öğle 0 EV, karanlık saatlerde yukarı açılır. Log2 çünkü pozlama EV cinsinden.
        // KAYNAK DEĞİŞTİ: eskiden `Atmosphere` modelinden (`BeamLevel`, `SkyLevel`,
        // `MoonLevel`) okunuyordu. O model artık IŞIĞI SÜRMÜYOR — soğurmanın sahibi
        // gökyüzü paketi, model yalnız sis ve bulut tonunu besliyor. Pozlama, sahneyi
        // aydınlatmayan bir modele göre açılıp kapanıyordu; şafakta ışık tam şiddetteyken
        // model hâlâ "karanlık" dediği için fazladan açıyordu.
        //
        // Şimdi iki GERÇEK büyüklük okunuyor, ikisi de öğlen 1'e normalize:
        //   güneş   — yönlü ışığın şiddeti / kalibrasyon sabiti
        //   gökyüzü — ortam probe'unun zenit parlaklığı / öğlen ölçümü
        //
        // Uçlar kâğıtta: öğlen 1 → uyum 0 EV. Gece güneş ~0.07, gök ~0.03 → tavan 0.6 EV.
        // Şafakta güneş ufku geçer geçmez 1'e çıkıyor → uyum 0, fazladan açma yok.
        const float ReferenceSunIntensity = 3.030782f;
        const float ReferenceSkyLuminance = 0.148f;

        // `SurfaceLightLevel` iki cismin DÜZ ZEMİNE ulaşan katkısını topluyor; her biri
        // kendi yüksekliğiyle çarpılı. Şiddet tek başına yanıltıyordu: güneş ufkun
        // altındayken şiddeti hâlâ büyük ama zemine hiç ulaşmıyor (`N·L` negatif).
        // Çarpansız hâlde 18:30'da uyum 0.81 EV'de kalıyor ve sahne zifiri görünüyordu.
        float lightLevel = time != null
            ? Mathf.Max(time.SurfaceLightLevel / ReferenceSunIntensity,
                        AmbientZenithLuminance() / ReferenceSkyLuminance)
            : 1f;

        // UYUM KISMİDİR. Farkın tamamını kapatmak (tam normalizasyon) şafağı öğlene
        // çeviriyordu — Unreal'in belgelerinde de aynı tuzak anlatılıyor: alt sınır
        // düşük tutulunca kamera gece sahnesini "yetersiz pozlanmış" sanıp gündüz gibi
        // gösteriyor. Göz de öyle çalışmaz: karanlığa açılır ama farkın ancak yarısını
        // kapatır, geri kalanı karanlık olarak KALIR.
        //
        // ALT SINIR 0.02 → 0.0005. Eskiden `lightLevel` 0.02'de kırpılıyordu, yani
        // uyum en fazla 5.6 durak görebiliyordu; gerçek gece bundan çok daha aşağıda ve
        // kırpma alacakaranlığı tek bir seviyeye düzlüyordu.
        float adapt = Mathf.Clamp(adaptShare * -Mathf.Log(Mathf.Max(0.0005f, lightLevel), 2f),
                                  0f, exposureCap);

        Set(colorAdjustments.postExposure, profile.exposure + adapt);
        Set(colorAdjustments.contrast, profile.contrast);
        Set(colorAdjustments.saturation, profile.saturation);
        Set(colorAdjustments.colorFilter, profile.colorFilter);

        Set(whiteBalance.temperature, profile.temperature);
        Set(whiteBalance.tint, profile.tint);

        // VİNYET YOK. Ekran köşelerini karartmak oyuncuyla dünya arasına duvar koyuyor
        // ve hareket hâlinde belli oluyor. Merkeze odaklama işini bizde fizik yapıyor:
        // hava perspektifi, üç katmanlı sis, mesafeyle mavileşme.
        //
        // Yerine GÖLGE SOĞUTMASI: kasvet gölgeden gelir. Gölgede kalan her şey mavileşip
        // ağırlaşırken güneş gören yüzey sıcaklığını korur — kontrast parlaklıkta değil
        // RENKTE. Global sıcaklık kaydırması bunu veremez, şafağı da soğutur.
        shadows.shadows.overrideState = true;
        shadows.shadows.value = new Vector4(
            Mathf.Lerp(1f, 0.92f, profile.shadowChill),
            Mathf.Lerp(1f, 0.97f, profile.shadowChill),
            Mathf.Lerp(1f, 1.10f, profile.shadowChill), 0f);

        shadows.highlights.overrideState = true;
        shadows.highlights.value = new Vector4(
            Mathf.Lerp(1f, 1.02f, profile.shadowChill), 1f,
            Mathf.Lerp(1f, 0.97f, profile.shadowChill), 0f);

        Set(bloom.intensity, profile.bloom);
        Set(bloom.threshold, profile.bloomThreshold);

        Set(filmGrain.intensity, profile.grain);
    }

    static void Set(FloatParameter parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    static void Set(ClampedFloatParameter parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    static void Set(ColorParameter parameter, Color value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    static void Set(MinFloatParameter parameter, float value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }
}
