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

    void LateUpdate()
    {
        Shader.SetGlobalVector(SnowShaderIDs.SnowUpDirection, upDirection.normalized);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowCoverage,
            Mathf.Clamp01(SnowRuntimeState.GroundCoverage01 * coverageScale));
    }

    void OnDisable()
    {
        // Bileşen kapanınca nesnelerin üstünde donmuş bir kar kalmasın.
        Shader.SetGlobalFloat(SnowShaderIDs.SnowCoverage, 0f);
    }
}
