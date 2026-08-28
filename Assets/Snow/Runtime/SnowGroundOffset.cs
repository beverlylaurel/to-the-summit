// ROLE: keeps the character on top of the snow surface. The terrain collider represents
// the rock; the snow rises above it as geometry.
// CALLED BY: nobody — it runs on its own, its dependencies come from the Inspector.

using System;
using UnityEngine;

/// THE CHARACTER STANDS ON THE DRAWN SURFACE, NOT ON THE ROCK.
///
/// `CharacterController` stands on the terrain collider and that collider comes from the
/// heightmap — at 7.32 m resolution, without snow. When the snow surface rises 15-30 cm
/// with tessellation the character stays that far buried.
///
/// THIS MISTAKE WAS MADE ONCE. The `MountainSurface.shader` comment: "the foot at 205.539,
/// the rock at 205.489, the drawn surface at 205.98 — the character started half a metre
/// buried and the eye stayed below the snow surface." That round ended with the snow
/// height being removed from the geometry entirely.
///
/// The function read is the twin of the one the shader uses (`SnowSurfaceHeight`) and
/// their parity is tested by `SnowHeightParityTest`: 512 samples, 1 mm tolerance.
///
/// ONLY WHILE GROUNDED. While airborne (jumping, falling) the surface correction is not
/// applied; applied, the character would be pulled upward in mid-air.
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class SnowGroundOffset : MonoBehaviour
{
    [Tooltip("The snow manager. The snow depth and the wind exposure are read from here; " +
             "left empty the character stays on the rock.")]
    [SerializeField] SnowManager snowManager;

    [Tooltip("Smoothing time constant of the surface correction (s). Zero = instant.")]
    [SerializeField, Min(0f)] float smoothing = 0.06f;

    CharacterController controller;

    /// The correction currently applied. As the character walks the surface height
    /// changes; applied INSTANTLY the camera jumps.
    float uygulanan;

    void Awake() => controller = GetComponent<CharacterController>();

    void OnEnable() => uygulanan = 0f;

    /// LATEUPDATE, NOT UPDATE. `FirstPersonController` moves in `Update`; the correction
    /// has to come after it, otherwise it runs one frame behind.
    void LateUpdate()
    {
        if (snowManager == null) return;

        // No surface correction while airborne: the character must not be pulled up while
        // jumping. The correction returns to zero slowly so the landing is soft.
        float hedef = controller.isGrounded ? SurfaceHeight() : 0f;

        float k = smoothing > 0f
            ? 1f - Mathf.Exp(-Time.deltaTime / smoothing)
            : 1f;

        float yeni = Mathf.Lerp(uygulanan, hedef, k);
        float fark = yeni - uygulanan;

        if (Mathf.Abs(fark) > 1e-5f)
        {
            // Moved with the controller disabled: `Move` would resolve collisions and the
            // character would snag on the ground with its own capsule and shake.
            controller.enabled = false;
            transform.position += new Vector3(0f, fark, 0f);
            controller.enabled = true;
        }

        uygulanan = yeni;
    }

    float SurfaceHeight()
    {
        float depth = snowManager.WorldSnowDepth;
        if (depth <= 0f) return 0f;

        Vector3 p = transform.position;

        return SnowSurfaceHeight.ReliefWorld(p, depth,
                                             snowManager.WindShadowAt(p),
                                             snowManager.SastrugiWindDir);
    }

    void OnValidate()
    {
        if (snowManager == null && Application.isPlaying)
            throw new InvalidOperationException(
                $"{nameof(SnowGroundOffset)}: {nameof(snowManager)} is not assigned.");
    }
}
