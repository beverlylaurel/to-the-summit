using UnityEngine;

/// Bakes the ambient light from the REAL sky.
///
/// The package's own probe came from the analytic `RenderSky` in C# and that path carries no
/// multiple scattering — twilight comes from multiple scattering exactly, and with the sun below
/// the horizon single scattering is zero. It was measured: at 18:36 the drawn sky was red, the
/// probe `0.00000`, the scene pitch black. The analytic path was disabled.
///
/// The skybox material is PBSky itself, so it draws the twilight the LUT produces;
/// `DynamicGI.UpdateEnvironment()` converts it into spherical harmonics.
///
/// THE BAKE IS RATE LIMITED. Spherical harmonics and the reflection cube are regenerated; called
/// every frame it doubles the frame time. The sky does not change visibly within a second.
public class SkyAmbientBaker : MonoBehaviour
{
    [Tooltip("Source of the sun direction. The bake is only refreshed when the sky has moved.")]
    [SerializeField] TimeOfDay time;

    [Tooltip("Shortest interval between two bakes (seconds).")]
    [SerializeField, Range(0.1f, 5f)] float minimumInterval = 0.5f;

    [Tooltip("The sun direction moving this far triggers a rebake (degrees).")]
    [SerializeField, Range(0.05f, 5f)] float movementDegrees = 0.25f;

    Vector3 bakedSunDirection = Vector3.zero;
    float nextBakeTime = -1f;

    /// THE BAKE READS ONE FRAME BEHIND. `DynamicGI.UpdateEnvironment()` reads the sky material
    /// in its current state, but the material's parameters are written by the render pass — so at
    /// `LateUpdate` the material still holds the PREVIOUS frame's state.
    ///
    /// With time flowing continuously it is invisible: a new bake arrives every frame and one
    /// frame of delay goes unnoticed. But when the clock JUMPS and a single bake is done while
    /// the sun also stops (with "Freeze time" checked in F1) the probe FREEZES on the old sky.
    ///
    /// Measured: jumping from noon to night, at 00:00 `ambient zenith` was still
    /// 0.0793 0.1064 0.1355 — the noon value. Because `LookController` reads the exposure from
    /// that probe, the night scene was drawn with the daytime exposure and everything came out
    /// black; the clouds were affected the most.
    int followUpBakes;

    public void Bind(TimeOfDay timeRef) => time = timeRef;

    void OnEnable()
    {
        if (time == null)
            throw new System.InvalidOperationException($"{nameof(SkyAmbientBaker)}: the dependency is not assigned.");

        bakedSunDirection = Vector3.zero;
        nextBakeTime = -1f;
        followUpBakes = 0;
    }

    void LateUpdate()
    {
        // The angle threshold: with the sun 0.25° away the sky has changed measurably.
        float moved = Vector3.Angle(bakedSunDirection, time.SunDirection);
        bool skyMoved = bakedSunDirection == Vector3.zero || moved >= movementDegrees;

        if (skyMoved && Time.time >= nextBakeTime)
        {
            bakedSunDirection = time.SunDirection;
            nextBakeTime = Time.time + minimumInterval;

            // This bake reads the old material state; the follow-up bake will take the new one.
            followUpBakes = 1;

            DynamicGI.UpdateEnvironment();
            return;
        }

        // THE FOLLOW-UP BAKE IS NOT RATE LIMITED: its purpose is to close a one-frame gap, and
        // limited it the delay would simply stay. At most one per frame, so it costs one bake.
        if (followUpBakes > 0)
        {
            followUpBakes--;
            DynamicGI.UpdateEnvironment();
        }
    }
}
