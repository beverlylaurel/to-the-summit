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

    public void Tick(ISnowEnvironmentSource env)
    {
        // YAĞIŞ VARSA KAR VAR. Sıcaklık kapısı yok.
        //
        // Eskiden §3.4'ün histerezisi vardı: 0.5 °C altı kar, 2.0 °C üstü
        // yağmur. Kaldırıldı — kar çizgisi kaldırılırken konan kuralın aynısı
        // geçerli: yağıyorsa kardır, tutar.
        SnowRuntimeState.IsSnowing = env.PrecipKind != PrecipitationKind.None;

        // Yağmur yolu susuyor: iki yağış üst üste binmesin.
        SnowRuntimeState.RainWeight01 = 0f;

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
