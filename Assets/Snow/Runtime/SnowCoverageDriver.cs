// ROL: nesne üstü kar maskesinin okuduğu iki globali yayınlar.
// Çağıran: sahne (SnowManager'ın yanında).

using UnityEngine;

/// TEK SAHİP. `_SnowCoverage` ve `_SnowUpDirection`'ı yalnız bu bileşen
/// yazıyor. İki yerden yazılsaydı hangisinin kazandığı çalışma sırasına
/// kalırdı ve belirti "bazı nesneler karlanmıyor" olurdu.
///
/// Zeminde ne kadar kar varsa nesnelerde de o kadar (spec §16) — kaplama
/// `SnowRuntimeState`'ten geliyor, ayrı bir ölçüm yapılmıyor.
[DisallowMultipleComponent]
public class SnowCoverageDriver : MonoBehaviour
{
    [Tooltip("Karın biriktiği yön. Dünya yukarısı dışında bir şey vermek "
             + "yalnız yerçekimi yönü değişen sahnelerde anlamlı.")]
    [SerializeField] Vector3 upDirection = Vector3.up;

    [Tooltip("Kaplamanın nesnelere yansırken çarpıldığı katsayı. 1 = zeminle "
             + "birebir aynı.")]
    [SerializeField, Range(0f, 2f)] float coverageScale = 1f;

    [Tooltip("Örtü ayarlarının kaynağı. Arazi ve nesneler aynı sayıları okuyor.")]
    [SerializeField] SnowSettings settings;

    void LateUpdate()
    {
        Shader.SetGlobalVector(SnowShaderIDs.SnowUpDirection, upDirection.normalized);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowCoverage,
            Mathf.Clamp01(SnowRuntimeState.GroundCoverage01 * coverageScale));

        if (settings == null) return;

        // ÖRTÜ AYARLARI DA BURADAN. Arazinin kar katmanı ile nesne shader'ı
        // aynı sayıları okumak zorunda; ayrışırlarsa sınırda iki farklı kar
        // görünür (ölçüldü: arazi derinlikten, mesh örtüden okurken kenarda
        // 45 cm'lik hendek — `SYMPTOMS.md`).
        Shader.SetGlobalFloat(SnowShaderIDs.CoverSlopeSharpness, settings.CoverSlopeSharpness);
        Shader.SetGlobalFloat(SnowShaderIDs.CoverBreakupStrength, settings.CoverBreakupStrength);
        Shader.SetGlobalFloat(SnowShaderIDs.CoverEdgeSharpness, settings.CoverEdgeSharpness);
        Shader.SetGlobalFloat(SnowShaderIDs.CoverThickness, settings.CoverThickness);
    }

    void OnDisable()
    {
        // Bileşen kapanınca nesnelerin üstünde donmuş bir kar kalmasın.
        Shader.SetGlobalFloat(SnowShaderIDs.SnowCoverage, 0f);
    }
}
