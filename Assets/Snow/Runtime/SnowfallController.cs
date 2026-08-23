// ROL: mevcut yağış şiddetinden kar yağışını türetir ve durumu yayınlar.
// Çağıran: SnowManager (LateUpdate).

using UnityEngine;

/// TEK KAYNAK, TEK ŞİDDET (spec §17.2).
///
/// VFX yoğunluğu ile `_SnowfallSWERate` AYNI `i01` değerinden türüyor. Ayrı
/// kaynaklardan gelselerdi belirti "yoğun kar yağıyor ama zemin birikmiyor"
/// olurdu ve ikisinden hangisinin yanlış olduğu ekrandan anlaşılmazdı.
public sealed class SnowfallController
{
    public float SnowfallSweRate { get; private set; }
    public float FlakeRate { get; private set; }

    /// Tanenin ıslaklığı — VFX terminal hızını ve salınımını bundan alıyor
    /// (spec §17.1). Yağış sıcaklıktan koparıldığı için tane her zaman kuru.
    public float Wetness { get; private set; }

    public void Reset()
    {
        SnowfallSweRate = 0f;
        FlakeRate = 0f;
        Wetness = 0f;
    }

    /// KAR ORANI GİRDİ, SICAKLIKTAN TÜREMİYOR.
    ///
    /// `snowFraction01` yağışın ne kadarının kar olduğu: 1 tamamen kar,
    /// 0 tamamen yağmur, arası karışık. Varsayılan 1 — "yağış varsa kar
    /// yağar" kuralı.
    ///
    /// Eskiden bu kararı §3.4'ün sıcaklık histerezisi veriyordu ve kaldırıldı.
    /// Yerine sıcaklık KONMADI: karar dışarıdan geliyor, böylece hava sistemi
    /// (ya da F1 sürgüsü) ne isterse onu sürüyor ve kar sistemi kimseyi
    /// zorlamıyor.
    public void Tick(ISnowEnvironmentSource env, float snowFraction01)
    {
        float snowShare = Mathf.Clamp01(snowFraction01);

        // YAĞIŞ VARSA KAR VAR. Sıcaklık kapısı yok.
        //
        // Eskiden §3.4'ün histerezisi vardı: 0.5 °C altı kar, 2.0 °C üstü
        // yağmur. Kaldırıldı — kar çizgisi kaldırılırken konan kuralın aynısı
        // geçerli: yağıyorsa kardır, tutar.
        bool precipActive = env.PrecipKind != PrecipitationKind.None;

        // KESKİN SINIR: YA KAR YA YAĞMUR, ASLA İKİSİ BİRDEN.
        //
        // Eskiden pay ikisine BÖLÜNÜYORDU (kar 0.5 → yarı kar yarı yağmur) ve
        // gerekçe "toplamları bir olduğu için üst üste binemezler"di. O akıl
        // yürütme yanlış: toplamın bir olması ikisinin AYNI ANDA ÇİZİLMESİNİ
        // engellemiyor, yalnız ikisini de yarı şiddette çiziyor. Ekranda kar ve
        // yağmur iç içe yağıyordu (kullanıcı bildirdi).
        //
        // Gerçekte de karışık yağış (sulusepken) ayrı bir olgudur, karla
        // yağmurun üst üste bindirilmesi değil. Onu istersek kendi taneciği
        // olur; şimdilik eşik.
        //
        // Sürgü artık ANAHTAR: 0.5 ve üstü kar, altı yağmur. Şiddet
        // BÖLÜNMÜYOR — hangisi kazanırsa yağışın tamamını alıyor.
        bool karYagiyor = snowShare >= 0.5f;

        SnowRuntimeState.IsSnowing = precipActive && karYagiyor;

        SnowRuntimeState.RainWeight01 = precipActive && !karYagiyor ? 1f : 0f;

        SnowRuntimeState.SnowfallIntensity01 =
            SnowRuntimeState.IsSnowing ? env.PrecipIntensity01 : 0f;

        float i01 = SnowRuntimeState.SnowfallIntensity01;

        SnowfallSweRate = Mathf.Lerp(0f, SnowConstants.MaxSweRate, i01);
        FlakeRate = Mathf.Lerp(0f, SnowConstants.MaxFlakeRate, i01);

        // KURU KAR. Islaklık sıcaklıktan türüyordu; yağış sıcaklıktan
        // koparıldı, kaynağı kalmadı.
        Wetness = 0f;

        Shader.SetGlobalFloat(SnowShaderIDs.SnowfallSWERate, SnowfallSweRate);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowWetness, Wetness);
    }
}
