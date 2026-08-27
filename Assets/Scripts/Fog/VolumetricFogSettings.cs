using UnityEngine;

/// Settings of the froxel fog volume. The numbers come from Wronski 2014 and from calculations
/// on paper; the reasoning is in `.claude/PRPs/plans/volumetric-fog.plan.md` -> "Numbers computed on paper".
///
/// DENSITY IS NOT HERE: it is owned by `AtmosphereController` and `AtmosphereSettings`.
/// Where the air is how dense derives from the weather; this asset only carries the volume's own
/// geometry and light response. Mixed together, the fog would be driven from two sources.
[CreateAssetMenu(menuName = "To The Summit/Volumetrik Sis", fileName = "VolumetricFogSettings")]
public class VolumetricFogSettings : ScriptableObject
{
    [Header("Hacim")]
    [Tooltip("Resolution of the volume along the screen axes. Wronski uses 160x90 and the cost " +
             "stays INDEPENDENT of the screen resolution.")]
    [SerializeField, Range(80, 320)] int width = 160;

    [SerializeField, Range(45, 180)] int height = 90;

    [Tooltip("Number of depth slices. Wronski uses 64 or 128 (platform dependent).")]
    [SerializeField, Range(32, 128)] int sliceCount = 64;

    /// THE RANGE IS 1000 m. The range Wronski verified is 50-128 m; for a longer range he says
    /// "an exponential distribution or a cascaded approach" but DOES NOT DEFINE the cascaded one.
    /// A single volume plus an analytic tail was chosen instead (`DECISIONS.md`, decision 1).
    ///
    /// Thanks to the exponential distribution the range grows eightfold without losing near-field
    /// precision: 46 slices fall in the first 128 metres, where Wronski spreads his entire volume
    /// over that distance with 64.
    [Tooltip("View space depth the volume starts at (metres).")]
    [SerializeField, Range(0.1f, 5f)] float nearDistance = 0.5f;

    [Tooltip("Depth the volume ends at (metres). Beyond it the analytic tail takes over.")]
    [SerializeField, Range(100f, 4000f)] float farDistance = 1000f;

    [Header("Light response")]
    /// The fog's OWN anisotropy. Separate from the sky package's `_AerosolAnisotropy`, because it
    /// is a different medium: fog is water droplets (forward scattering is clear but not as much
    /// as a cloud), the sky's is dust aerosol.
    [Tooltip("Henyey-Greenstein anizotropisi. 0 izotropik, 1 tamamen ileri.")]
    [SerializeField, Range(0f, 0.95f)] float anisotropy = 0.6f;

    /// The ambient contribution comes from the ambient probe, i.e. the single state baked from
    /// the sky. Adding the atmosphere's in-scattering here as well would be double counting — the
    /// homogeneous atmosphere is owned by the sky package (`DECISIONS.md`, decision 2).
    [Tooltip("Share of ambient light shadowed fog receives.")]
    [SerializeField, Range(0f, 2f)] float ambientDimmer = 1f;

    [Tooltip("The main light's contribution to the fog.")]
    [SerializeField, Range(0f, 2f)] float lightDimmer = 1f;

    public int Width => width;
    public int Height => height;
    public int SliceCount => sliceCount;
    public float NearDistance => nearDistance;
    public float FarDistance => Mathf.Max(farDistance, nearDistance * 2f);
    public float Anisotropy => anisotropy;
    public float AmbientDimmer => ambientDimmer;
    public float LightDimmer => lightDimmer;
}
