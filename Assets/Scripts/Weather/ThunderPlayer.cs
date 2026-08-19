using System;
using UnityEngine;

/// Rastgele aralıklarla şimşek çaktırır. Yağış şiddeti arttıkça sıklaşır ve yakınlaşır,
/// karlılık arttıkça kesilir. Çakma anında olay yayar, sesi ise arkadan gelir.
[RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
public class ThunderPlayer : MonoBehaviour
{
    [SerializeField] WeatherState weather;
    [SerializeField] ThunderSettings settings;

    [Header("Klipler")]
    [SerializeField] AudioClip[] distant;
    [SerializeField] AudioClip[] close;

    /// Havada sesin hızı (m/sn). Bir ayar değil, bir sabit.
    const float SpeedOfSound = 340f;

    /// Çakmanın uzaklığı, metre. Sesten önce yayılır — ışık anında gelir, ses yolda.
    ///
    /// Mesafeyi burası seçiyor ve tek kaynak burası: hem ses gecikmesi hem çakmanın
    /// dünyadaki yeri bundan türüyor. İkisi ayrı seçilseydi bir buçuk saniye sonra
    /// gürleyen bir gürültü, sekiz yüz metre ötede çakmış bir ışığa ait olurdu.
    public event Action<float> Struck;

    AudioSource source;
    AudioLowPassFilter filter;
    float timer;
    int lastDistantIndex = -1;
    int lastCloseIndex = -1;

    // Yolda olan gürültü: çakma oldu, ses henüz ulaşmadı
    AudioClip pendingClip;
    float pendingDelay;
    float pendingVolume;
    float pendingPitch;
    float pendingPan;
    float pendingCutoff;

    public void Bind(WeatherState state, ThunderSettings tuning,
        AudioClip[] distantClips, AudioClip[] closeClips)
    {
        weather = state;
        settings = tuning;
        distant = distantClips;
        close = closeClips;
    }

    void OnEnable()
    {
        if (weather == null || settings == null)
            throw new InvalidOperationException($"{nameof(ThunderPlayer)}: bağımlılıklar atanmadı.");
        if (distant == null || distant.Length == 0 || close == null || close.Length == 0)
            throw new InvalidOperationException($"{nameof(ThunderPlayer)}: klip listeleri boş.");

        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;

        // Gürültüler PlayOneShot ile çalınıyor, kaynağın kendi klibi kullanılmıyor.
        // Ama alçak geçiren filtresi olan klipsiz bir kaynak Unity'yi uyarı basmaya
        // itiyor; playOnAwake kapalı olduğu için atanan klip kendiliğinden çalmaz.
        source.clip = distant[0];

        filter = GetComponent<AudioLowPassFilter>();

        Reschedule();
    }

    void Update()
    {
        if (pendingClip != null)
        {
            pendingDelay -= Time.deltaTime;
            if (pendingDelay <= 0f) Boom();
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        Reschedule();

        if (weather.Precipitation < settings.minPrecipitation) return;
        if (weather.Snowiness >= settings.snowCutoff) return;

        Strike();
    }

    /// Test için beklemeden çaldırır
    public void TriggerNow() => Strike();

    void Reschedule()
    {
        float interval = Mathf.Lerp(settings.maxInterval, settings.minInterval, weather.Precipitation);
        timer = UnityEngine.Random.Range(interval * 0.6f, interval * 1.4f);
    }

    void Strike()
    {
        // Önceki çakmanın sesi hâlâ yoldaysa bırakılmaz, hemen ulaştırılır: üst üste
        // binen gürültü fırtınada olağan, sessizce düşürülen bir atım değil.
        if (pendingClip != null) Boom();

        // Yakın gürültü yalnızca yağış sertleştiğinde: etekte uzak ve sakin kalır.
        //
        // Eşikten sonra hızlı tırmanır. Doğrusal eğri eşiğin hemen üstünde neredeyse
        // sıfır kalıyordu: 0.65'te kırkta bir. Oysa fırtına eşiği geçtiyse yakın çakma
        // artık istisna değil. Karekök eğriyi başta dikleştirip uçta doyuruyor.
        // EŞİK SERT KESME. Altında olasılık "az" değil TAM SIFIR — dinginde yakın çakma
        // hiç olmaz, tasarım da bunu istiyor: sakin yağmurda uzaktan gürleme duyulur,
        // kol görünmez. Ama eşik 0.6'dayken 0.56'lık şiddetli yağmur da sıfır alıyordu
        // ve kol pratikte hiç görünmüyordu (kırk çakma denendi, kırkı da uzak).
        // 0.45'e indi: 0.56'da yakın çakma %38, 0.85'te %72.
        float closeChance = Mathf.Sqrt(Mathf.InverseLerp(settings.closeThreshold, 1f, weather.Precipitation))
                            * settings.closeChanceAtPeak;
        bool isClose = UnityEngine.Random.value < closeChance;

        pendingClip = isClose
            ? Pick(close, ref lastCloseIndex)
            : Pick(distant, ref lastDistantIndex);

        // Karlılık arttıkça seyrelir ama tamamen kısılmaz: çaldığında duyulmalı
        float fade = Mathf.Lerp(1f, settings.minVolume, Mathf.Clamp01(weather.Snowiness / settings.snowCutoff));

        // Hafif yağışta gürültü de sönük olmalı; şiddetle birlikte güçlenir
        fade *= Mathf.Lerp(0.45f, 1f, weather.Precipitation);

        // Aynı klip her atımda farklı mesafeden geliyormuş gibi duyulsun
        Vector2 cutoff = isClose ? settings.closeCutoff : settings.distantCutoff;
        pendingCutoff = UnityEngine.Random.Range(cutoff.x, cutoff.y);

        pendingPitch = 1f + UnityEngine.Random.Range(-settings.pitchVariation, settings.pitchVariation);
        pendingPan = UnityEngine.Random.Range(-settings.panVariation, settings.panVariation);
        pendingVolume = Mathf.Clamp01(
            fade * (1f + UnityEngine.Random.Range(-settings.volumeVariation, settings.volumeVariation)));

        Vector2 range = isClose ? settings.closeDistance : settings.distantDistance;
        float distance = UnityEngine.Random.Range(range.x, range.y);

        pendingDelay = distance / SpeedOfSound;

        // Işık önce. Ses yolda.
        Struck?.Invoke(distance);
    }

    /// Sesin ulaştığı an
    void Boom()
    {
        filter.cutoffFrequency = pendingCutoff;
        source.pitch = pendingPitch;
        source.panStereo = pendingPan;
        source.PlayOneShot(pendingClip, pendingVolume);

        pendingClip = null;
    }

    AudioClip Pick(AudioClip[] clips, ref int lastIndex)
    {
        if (clips.Length == 1) return clips[0];

        int index = UnityEngine.Random.Range(0, clips.Length - 1);
        if (index >= lastIndex && lastIndex >= 0) index++;

        lastIndex = index;
        return clips[index];
    }
}
