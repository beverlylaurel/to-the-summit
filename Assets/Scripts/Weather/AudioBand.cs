using System;
using UnityEngine;

/// Tek bir ses bandı. Varyasyon listesinden döngüsel klip çalar; seviye, parlaklık ve
/// perde dışarıdan verilir.
///
/// Seviye asimetrik yumuşatılır: yükselirken hızlı, düşerken yavaş. Rüzgâr fiziksel
/// olarak böyle davranır — esinti aniden gelir, yavaşça çekilir.
///
/// Varyasyonlar arası geçiş iki kaynak arasında çapraz sönümlemeyle yapılır. Bandın
/// susmasını beklemek işe yaramıyordu: dingin band ancak şiddet uca dayandığında
/// susuyor, yani pratikte ilk klip sonsuza kadar dönüyordu.
public class AudioBand
{
    const float MinCutoff = 400f;
    const float MaxCutoff = 22000f;
    /// Bu seviyenin altındaki kaynak duraklatılır: sıfır sesle çalmak klibi çözmeye
    /// devam eder, yani duyulmayan bir band bedava değildir.
    const float SilenceThreshold = 0.004f;
    const float CrossfadeSeconds = 4f;
    const float MinHoldSeconds = 30f;
    const float MaxHoldSeconds = 45f;

    readonly AudioClip[] variations;
    readonly AudioSource[] sources = new AudioSource[2];
    readonly AudioLowPassFilter filter;
    readonly float attackSeconds;
    readonly float releaseSeconds;

    int currentIndex = -1;
    int active;          // o an ön planda olan kaynak
    float blend;         // 0 tamamen active, 1 tamamen diğeri
    float holdTimer;
    float level;

    /// Her band kendi objesinde durur: Unity'de alçak geçiren filtre objedeki tüm
    /// kaynakların karışımına uygulanır, band başına filtre ancak böyle mümkün olur.
    public AudioBand(Transform parent, string name, AudioClip[] clips, float attack, float release)
    {
        if (clips == null || clips.Length == 0)
            throw new ArgumentException($"{nameof(AudioBand)}: klip listesi boş.");

        variations = clips;
        attackSeconds = attack;
        releaseSeconds = release;

        var host = new GameObject(name);
        host.transform.SetParent(parent, false);

        for (int i = 0; i < sources.Length; i++)
        {
            var source = host.AddComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.bypassReverbZones = true;
            sources[i] = source;
        }

        filter = host.AddComponent<AudioLowPassFilter>();
        filter.cutoffFrequency = MaxCutoff;

        sources[active].clip = NextVariation();
        sources[active].Play();
        holdTimer = UnityEngine.Random.Range(MinHoldSeconds, MaxHoldSeconds);
    }

    /// <param name="target">Hedef seviye (0-1).</param>
    /// <param name="brightness">0 boğuk, 1 tam açık. Alçak geçiren filtreyi sürer.</param>
    /// <param name="pitch">Perde çarpanı. 1 = klibin kendi perdesi.</param>
    public void Drive(float target, float brightness, float pitch)
    {
        target = Mathf.Clamp01(target);

        float duration = target > level ? attackSeconds : releaseSeconds;
        float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, duration));
        level = Mathf.Lerp(level, target, t);

        AdvanceCrossfade();

        // Eşit güç: iki kaynağın toplam enerjisi geçiş boyunca sabit kalır,
        // doğrusal karışım ortada belirgin bir çukur bırakırdı
        SetLevel(sources[active], level * Mathf.Cos(blend * Mathf.PI * 0.5f));
        SetLevel(sources[1 - active], level * Mathf.Sin(blend * Mathf.PI * 0.5f));

        foreach (var source in sources) source.pitch = pitch;

        // Kesim frekansı logaritmik ölçekte kaydırılır; kulak frekansı böyle algılar
        filter.cutoffFrequency = MinCutoff * Mathf.Pow(MaxCutoff / MinCutoff, Mathf.Clamp01(brightness));
    }

    /// Duyulmayan kaynak duraklatılır; klip çözme maliyeti ancak böyle biter.
    /// Duraklatma konumu koruduğu için geri açıldığında kaldığı yerden devam eder.
    static void SetLevel(AudioSource source, float volume)
    {
        source.volume = volume;

        bool audible = volume > SilenceThreshold;
        if (audible == source.isPlaying) return;

        if (audible) source.UnPause();
        else source.Pause();
    }

    void AdvanceCrossfade()
    {
        if (variations.Length == 1) return;

        // Geçiş sürüyor: tamamlandığında roller değişir, eski kaynak susar
        if (blend > 0f)
        {
            blend = Mathf.MoveTowards(blend, 1f, Time.deltaTime / CrossfadeSeconds);
            if (blend < 1f) return;

            sources[active].Stop();
            active = 1 - active;
            blend = 0f;
            holdTimer = UnityEngine.Random.Range(MinHoldSeconds, MaxHoldSeconds);
            return;
        }

        holdTimer -= Time.deltaTime;
        if (holdTimer > 0f) return;

        var next = sources[1 - active];
        next.clip = NextVariation();
        next.time = 0f;
        next.Play();
        blend = Mathf.Epsilon;
    }

    AudioClip NextVariation()
    {
        currentIndex = NextIndexExcluding(currentIndex);
        return variations[currentIndex];
    }

    int NextIndexExcluding(int excluded)
    {
        if (variations.Length == 1) return 0;
        if (excluded < 0) return UnityEngine.Random.Range(0, variations.Length);

        int index = UnityEngine.Random.Range(0, variations.Length - 1);
        return index >= excluded ? index + 1 : index;
    }
}
