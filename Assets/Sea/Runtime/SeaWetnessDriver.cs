// ROLE: publishes the shore wetness band as global uniforms. The terrain
// material reads them; the sea NEVER TOUCHES the terrain.
// CALLED BY: nobody — runs on its own, dependencies come from the Inspector.

using System;
using UnityEngine;

/// THE SEA DOES NOT PAINT THE TERRAIN, IT PUBLISHES A LEVEL.
///
/// Spec §14. All that leaves this component is two floats: the top elevation
/// of the wet band and the band's thickness. The terrain material reads them
/// and adjusts its own albedo and roughness. The sea system writes nothing
/// into the terrain material — the other way round the two systems would
/// overwrite each other.
///
/// **THE BAND BREATHES WITH THE WAVES.** The top elevation follows the run-up
/// phase: as a wave advances up the shore the band widens, as it withdraws it
/// narrows.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SeaWetnessDriver : MonoBehaviour
{
    [SerializeField] SeaSettings settings;

    /// Fade thickness of the wet band (m). Wetness reaches zero this far
    /// above the level. [CALIBRATION]
    [Tooltip("Fade thickness of the wet band (m).")]
    [Range(0.05f, 2f)] public float fadeMeters = 0.35f;

    /// How much darker wet sand is than dry sand. [CALIBRATION]
    [Tooltip("Albedo multiplier of the wet surface. 1 = no darkening.")]
    [Range(0.2f, 1f)] public float darkening = 0.55f;

    public void Bind(SeaSettings source)
    {
        settings = source;
    }

    void OnEnable()
    {
        if (settings == null)
            throw new InvalidOperationException(
                $"{nameof(SeaWetnessDriver)}: {nameof(settings)} is not assigned.");
    }

    void OnDisable()
    {
        // WHEN THE SEA SHUTS DOWN THE BAND SHUTS DOWN TOO.
        //
        // Left as it was, removing the sea system would leave the terrain's
        // shore band permanently wet and the cause would be hunted for in
        // the terrain.
        Disable();
    }

    static void Disable()
    {
        // An elevation below the terrain: `smoothstep` then returns 0
        // everywhere.
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetLevelY, -100000f);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetFadeM, 1f);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetDarkening, 1f);
    }

    void Update()
    {
        if (settings == null) return;

        if (!SeaRuntimeState.Active)
        {
            Disable();
            return;
        }

        // The run-up phase is 0..1; the top of the band sits that far above
        // sea level (spec §8.5).
        float runup = settings.runupMaxDepth * SeaRuntimeState.ShoreFoamIntensity01;

        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetLevelY, settings.seaLevelY + runup);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetFadeM, fadeMeters);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetDarkening, darkening);
    }
}
