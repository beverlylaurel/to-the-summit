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

    /// HOW TALL THE WET BAND IS (m).
    ///
    /// THE BAND USED TO HAVE NO BOTTOM. The terrain read
    /// `1 - smoothstep(level - fade, level, y)`, which is 1 for EVERY point below
    /// the waterline elevation — a metre from the water or a kilometre inland, it
    /// all counted as soaking wet. Measured on the sand: the albedo went to 0.55 of
    /// a warm beige (grey on screen) and the roughness to 0.35 of 0.67 (0.23, i.e.
    /// lacquer). The whole beach came out grey and plastic.
    ///
    /// A swash zone is a band: from the run-up line down to about where the water
    /// stands. Below that the ground is under water and the sea draws it anyway.
    [Tooltip("Height of the wet band below the run-up line (m).")]
    [Range(0.2f, 6f)] public float bandMeters = 1.6f;

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
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetBandM, 1f);
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
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetBandM, bandMeters);
        Shader.SetGlobalFloat(SeaShaderIDs.SeaWetDarkening, darkening);
    }
}
