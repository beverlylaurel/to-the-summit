// ROLE: publishes the two globals the object snow mask reads.
// CALLED BY: the scene (next to SnowManager).

using UnityEngine;

/// A SINGLE OWNER. `_SnowCoverage` and `_SnowUpDirection` are written by this component
/// only. Written from two places, which one wins would depend on the execution order and
/// the symptom would be "some objects do not get snow".
///
/// However much snow is on the ground, that much is on the objects (spec §16) — the
/// coverage comes from `SnowRuntimeState`, no separate measurement is made.
[DisallowMultipleComponent]
public class SnowCoverageDriver : MonoBehaviour
{
    [Tooltip("The direction snow settles from. Giving anything other than world up "
             + "only means something in scenes where the gravity direction changes.")]
    [SerializeField] Vector3 upDirection = Vector3.up;

    [Tooltip("The coefficient the coverage is multiplied by as it carries to the objects. "
             + "1 = exactly the same as the ground.")]
    [SerializeField, Range(0f, 2f)] float coverageScale = 1f;

    [Tooltip("The source of the cover settings. The terrain and the objects read the same numbers.")]
    [SerializeField] SnowSettings settings;

    [Tooltip("The manager reading the world snow. The cover derives from here WITH NO LAG.")]
    [SerializeField] SnowManager snowManager;

    /// THE COVER COMES FROM THE WORLD SNOW, NOT FROM A READBACK.
    ///
    /// `SnowRuntimeState.GroundCoverage01` used to be read; that value comes from an async
    /// GPU readback and refreshes once every thirty frames. Because the snow mesh updates
    /// IMMEDIATELY, a visible SQUARE was left around the player in between: the inside
    /// showed the new state and the outside the state from thirty frames ago (the user
    /// reported it twice — once with the inside white and the outside black, once the
    /// other way round).
    ///
    /// `SnowManager.WorldSwe` is integrated on the CPU from the same precipitation and has
    /// no lag. The curve is the same as the surface shader's: the `SNOW_MIN_VISIBLE_HEIGHT`
    /// threshold and the `SNOW_EDGE_FADE_RANGE` band.
    float DunyaOrtusu()
    {
        if (snowManager == null || snowManager.WorldSwe < 0f)
            return SnowRuntimeState.GroundCoverage01;

        float rho = Mathf.Lerp(SnowConstants.RhoMin, SnowConstants.RhoMax,
                               snowManager.WorldRhoN);

        float derinlik = snowManager.WorldSwe * SnowConstants.RhoWater / Mathf.Max(rho, 1f);

        return Mathf.Clamp01((derinlik - SnowConstants.MinVisibleHeight)
                             / SnowConstants.EdgeFadeRange);
    }

    void LateUpdate()
    {
        Shader.SetGlobalVector(SnowShaderIDs.SnowUpDirection, upDirection.normalized);
        Shader.SetGlobalFloat(SnowShaderIDs.SnowCoverage,
            Mathf.Clamp01(DunyaOrtusu() * coverageScale));

        if (settings == null) return;

        // THE COVER SETTINGS COME FROM HERE TOO. The terrain's snow layer and the object
        // shader have to read the same numbers; if they diverge, two different snows show
        // at the boundary (measured: with the terrain reading from the depth and the mesh
        // from the cover, a 45 cm ditch at the edge — `SYMPTOMS.md`).
        Shader.SetGlobalFloat(SnowShaderIDs.CoverSlopeSharpness, settings.CoverSlopeSharpness);
        Shader.SetGlobalFloat(SnowShaderIDs.CoverBreakupStrength, settings.CoverBreakupStrength);
        Shader.SetGlobalFloat(SnowShaderIDs.CoverEdgeSharpness, settings.CoverEdgeSharpness);
        Shader.SetGlobalFloat(SnowShaderIDs.CoverThickness, settings.CoverThickness);
    }

    void OnDisable()
    {
        // So no frozen snow is left on the objects when the component is switched off.
        Shader.SetGlobalFloat(SnowShaderIDs.SnowCoverage, 0f);
    }
}
