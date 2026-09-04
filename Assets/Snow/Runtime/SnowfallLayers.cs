using UnityEngine;
using UnityEngine.VFX;

/// Drives dual-layer snowfall (near VFX particles and distant precipitation) from shared intensity (spec §17, §17.3).
[DisallowMultipleComponent]
public class SnowfallLayers : MonoBehaviour
{
    [Header("Near Layer — VFX Particles (spec §17.1)")]
    [Tooltip("VFX_Snowfall instance.")]
    [SerializeField] VisualEffect nearLayer;

    [Tooltip("VFX graph spawn rate property name.")]
    [SerializeField] string rateProperty = "SpawnRate";

    [Tooltip("VFX graph turbulence intensity property name.")]
    [SerializeField] string turbulenceProperty = "TurbulenceIntensity";

    [Tooltip("VFX graph wind force property name.")]
    [SerializeField] string windProperty = "WindForce";

    [Tooltip("VFX graph ground Y plane property name.")]
    [SerializeField] string groundProperty = "GroundY";

    [Header("Environment")]
    [Tooltip("Environment bridge providing wind speed and direction.")]
    [SerializeField] SnowEnvironmentBridge environment;

    [Tooltip("Follow target for spawn box — main camera.")]
    [SerializeField] Transform followTarget;

    [Tooltip("Ground reference transform — player foot level.")]
    [SerializeField] Transform groundReference;

    [Header("Fallback")]
    [Tooltip("Compute-based legacy snowfall renderer (disabled when VFX near layer is active).")]
    [SerializeField] SnowfallRenderer computeFallback;

    /// Diagnostics: Current near layer spawn rate.
    public float NearRate { get; private set; }

    const float FlakeDrag = 9.81f;

    public bool NearDriven => nearLayer != null;

    void OnEnable()
    {
        if (computeFallback != null)
            computeFallback.enabled = nearLayer == null;
    }

    void OnDisable()
    {
        if (computeFallback != null) computeFallback.enabled = true;
    }

    void LateUpdate()
    {
        float i01 = SnowRuntimeState.SnowfallIntensity01;

        NearRate = Mathf.Lerp(0f, SnowConstants.MaxFlakeRate, i01);

        if (nearLayer == null) return;

        if (followTarget != null)
        {
            Vector3 wind = environment != null ? environment.WindDirection : Vector3.zero;
            Vector3 c = followTarget.position + Vector3.up * 11f + wind * 3f;

            nearLayer.transform.position = new Vector3(
                Mathf.Floor(c.x), Mathf.Floor(c.y), Mathf.Floor(c.z));
        }

        if (nearLayer.HasFloat(rateProperty))
            nearLayer.SetFloat(rateProperty, NearRate);

        if (environment != null && nearLayer.HasFloat(turbulenceProperty))
            nearLayer.SetFloat(turbulenceProperty, 0.35f * environment.WindSpeed);

        if (environment != null && nearLayer.HasVector3(windProperty))
            nearLayer.SetVector3(windProperty,
                                 environment.WindDirection * environment.WindSpeed * FlakeDrag);

        if (groundReference != null && nearLayer.HasFloat(groundProperty))
            nearLayer.SetFloat(groundProperty, groundReference.position.y);

    }
}
