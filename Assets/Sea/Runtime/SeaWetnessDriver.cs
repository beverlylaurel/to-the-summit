// ROL: kiyi islaklik bandini global uniform olarak yayinlar. Arazi
// materyali okuyor; deniz araziye DOKUNMUYOR.
// Cagiran: yok — kendi basina calisiyor, bagimliliklari Inspector'dan.

using System;
using UnityEngine;

/// DENİZ ARAZİYİ BOYAMIYOR, BİR SEVİYE YAYINLIYOR.
///
/// Spec §14. Buradan çıkan tek şey iki `float`: ıslak bandın üst kotu ve
/// bandın kalınlığı. Arazi materyali onları okuyup kendi albedo ve
/// pürüzlülüğünü ayarlıyor. Deniz sistemi arazi materyaline hiçbir şey
/// yazmıyor — tersi olsaydı iki sistem birbirini ezerdi.
///
/// **BANT DALGALARLA NEFES ALIYOR.** Üst kot kabarma (run-up) fazından
/// türüyor: dalga kıyıya ilerleyince bant genişliyor, çekilince daralıyor.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SeaWetnessDriver : MonoBehaviour
{
    [SerializeField] SeaSettings settings;

    /// Islak bandın yumuşama kalınlığı (m). Bu kadar yükselince ıslaklık
    /// sıfıra iniyor. [KALİBRASYON]
    [Tooltip("Islak bandın yumuşama kalınlığı (m).")]
    [Range(0.05f, 2f)] public float fadeMeters = 0.35f;

    /// Islak kumun kuru kuma göre koyuluğu. [KALİBRASYON]
    [Tooltip("Islak yüzeyin albedo çarpanı. 1 = hiç koyulmuyor.")]
    [Range(0.2f, 1f)] public float darkening = 0.55f;

    public void Bind(SeaSettings source)
    {
        settings = source;
    }

    void OnEnable()
    {
        if (settings == null)
            throw new InvalidOperationException(
                $"{nameof(SeaWetnessDriver)}: {nameof(settings)} atanmadı.");
    }

    void OnDisable()
    {
        // DENİZ KAPANINCA BANT DA KAPANIYOR.
        //
        // Seviye olduğu gibi bırakılsaydı deniz sistemi kaldırıldığında
        // arazinin kıyı bandı kalıcı olarak ıslak kalırdı ve sebebi
        // arazide aranırdı.
        Kapat();
    }

    static void Kapat()
    {
        // Arazinin altında kalan bir kot: `smoothstep` her yerde 0 veriyor.
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetLevelY, -100000f);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetFadeM, 1f);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetDarkening, 1f);
    }

    void Update()
    {
        if (settings == null) return;

        if (!SeaRuntimeState.Active)
        {
            Kapat();
            return;
        }

        // Kabarma fazı 0..1; bandın üst kotu deniz seviyesinin o kadar
        // üstünde (spec §8.5).
        float runup = settings.runupMaxDepth * SeaRuntimeState.ShoreFoamIntensity01;

        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetLevelY, settings.seaLevelY + runup);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetFadeM, fadeMeters);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetDarkening, darkening);
    }
}
