// ROL: mevcut yağış şiddetinden kar yağışını türetir ve durumu yayınlar.
// Yağmuru KAPATMAZ, yalnız `SnowRuntimeState.IsSnowing` bildirir (spec §3.4).
// Çağıran: SnowManager (LateUpdate).

using UnityEngine;

/// TEK KAYNAK, TEK ŞİDDET (spec §17.2).
///
/// VFX yoğunluğu ile `_SnowfallSWERate` AYNI `i01` değerinden türüyor. Ayrı
/// kaynaklardan gelselerdi belirti "yoğun kar yağıyor ama zemin birikmiyor"
/// olurdu ve ikisinden hangisinin yanlış olduğu ekrandan anlaşılmazdı.
public sealed class SnowfallController
{
    /// Histerezisin hafızası. Eşikte titremeyi bu tutuyor: tek eşik olsaydı
    /// sıcaklık 0.5 °C civarında salınırken kar saniyede birkaç kez başlayıp
    /// dururdu.
    bool snowing;

    /// Yağmur→kar el değiştirmesinin rampası. Ani kesme "yağmur kayboldu"
    /// diye okunuyor; bu süre boyunca yağmur soluyor, kar sonra başlıyor.
    const float HandoffSeconds = 0.8f;

    float rainWeight = 1f;

    public float SnowfallSweRate { get; private set; }
    public float FlakeRate { get; private set; }

    /// Tanenin ıslaklığı — VFX terminal hızını ve salınımını bundan alıyor
    /// (spec §17.1). Sıfırın altında kuru toz, üstünde ağır sulu kar.
    public float Wetness { get; private set; }

    public void Reset()
    {
        snowing = false;
        rainWeight = 1f;
        SnowfallSweRate = 0f;
        FlakeRate = 0f;
        Wetness = 0f;
    }

    public void Tick(ISnowEnvironmentSource env)
    {
        float t = env.TemperatureC;

        // Spec §3.4 birebir.
        if (snowing && t > SnowConstants.SnowOffAbove) snowing = false;
        if (!snowing && t < SnowConstants.SnowOnBelow) snowing = true;

        bool precipActive = env.PrecipKind != PrecipitationKind.None;

        SnowRuntimeState.IsSnowing = precipActive && snowing;

        // EL DEĞİŞTİRME SIRAYLA. Kar açıkken yağmur 0'a iniyor; kar şiddeti
        // yağmurun kalanı kadar KISILIYOR. Üst üste binme matematiksel
        // olarak imkânsız: ikisinin ağırlığı aynı rampanın iki ucu.
        float step = Time.deltaTime / HandoffSeconds;
        rainWeight = Mathf.MoveTowards(rainWeight, SnowRuntimeState.IsSnowing ? 0f : 1f, step);

        SnowRuntimeState.RainWeight01 = rainWeight;

        SnowRuntimeState.SnowfallIntensity01 =
            SnowRuntimeState.IsSnowing ? env.PrecipIntensity01 * (1f - rainWeight) : 0f;

        float i01 = SnowRuntimeState.SnowfallIntensity01;

        SnowfallSweRate = Mathf.Lerp(0f, SnowConstants.MaxSweRate, i01);
        FlakeRate = Mathf.Lerp(0f, SnowConstants.MaxFlakeRate, i01);

        // GEÇİŞ BANDINDA TEK TÜR. 0.5 °C altı kuru kar, 2.0 °C üstü yağmur;
        // arada tanenin BİÇİMİ değişiyor (sulu kar). İki ayrı tanecik setini
        // çapraz soldurmak yumuşak geçiş değil, iki yağışın üst üste
        // binmesidir — `DECISIONS.md`.
        Wetness = Mathf.Clamp01(Mathf.InverseLerp(SnowConstants.SnowOnBelow,
                                                  SnowConstants.SnowOffAbove, t));

        Shader.SetGlobalFloat(SnowShaderIDs.SnowfallSWERate, SnowfallSweRate);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowWetness, Wetness);
    }
}
