// ROLE: drives the two VFX layers of drifting snow (saltation, suspension) from a single
// threshold (spec §18.7).
// CALLED BY: the scene (next to SnowManager).

using UnityEngine;
using UnityEngine.VFX;

/// DRIFTING SNOW IS TWO LAYERS
/// `[SOURCE: Pomeroy & Gray 1990; PBSM 1993; Nishimura & Hunt 2000]`.
///
/// In meteorology drifting snow is split in two, and that maps directly onto two VFX
/// systems:
///
///   Saltation   1–5 cm    bouncing in contact with the surface, dense
///   Suspension  ≤ 5 m     held up by turbulence, sparse
///
/// BOTH HAVE THE SAME TRIGGER. Spec §18.7: "The trigger for both is `DriftActive01` from
/// §18.1. Do not define a separate threshold." The threshold lives here, in
/// `DriftActiveFor`; `SnowManager` reads it from there too. There is no second computation.
[DisallowMultipleComponent]
public class SnowDriftVfxController : MonoBehaviour
{
    [Header("Katmanlar")]
    [Tooltip("The VFX_Spindrift instance — saltation, hugging the ground. Left empty it " +
             "is not driven.")]
    [SerializeField] VisualEffect spindrift;

    [Tooltip("The VFX_SnowCurtain instance — suspension curtains. Left empty it " +
             "is not driven.")]
    [SerializeField] VisualEffect curtain;

    [Header("Dependencies")]
    [Tooltip("The bridge the wind speed is read from.")]
    [SerializeField] SnowEnvironmentBridge environment;

    [Tooltip("The source of the saltation rate.")]
    [SerializeField] SnowSettings settings;

    [Tooltip("The target the spawn boxes follow. It has to be THE PLAYER'S FOOT, not the " +
             "camera: saltation is a layer hugging the ground and the camera is at eye level.")]
    [SerializeField] Transform followTarget;

    [Header("Property names")]
    [SerializeField] string rateProperty = "SpawnRate";
    [SerializeField] string driftProperty = "DriftActive";

    [Tooltip("The name of the wind force property in the graph.")]
    [SerializeField] string windProperty = "WindForce";

    /// The saltation's drag coefficient — it has to be the same as `dragCoefficient` in the
    /// graph; the equilibrium speed is `F / drag`.
    const float SpindriftDrag = 4f;

    /// The suspension's drag coefficient.
    const float CurtainDrag = 3f;

    /// Spec §18.7: saltation is 0.7–1.1 times the wind, 0.9 in the middle.
    const float SpindriftWindShare = 0.9f;

    /// Spec §18.7: suspension is 0.7–0.95 times the wind, 0.82 in the middle.
    const float CurtainWindShare = 0.82f;

    /// Diagnostic: the current threshold value.
    public float DriftActive01 { get; private set; }

    /// Diagnostic: the saltation rate.
    public float SpindriftRate { get; private set; }

    /// Spec §18.7: saltation is born in the strip DOWNWIND of the camera.
    /// The strip is 30 m; the box centre is half that far ahead.
    const float SpindriftLead = 15f;

    /// Spec §18.7: suspension is born 35 m UPWIND of the camera —
    /// from there it comes onto us with the wind.
    const float CurtainUpwind = 35f;

    /// The suspension layer's box centre. The PBSM upper bound is 5 m; the box centre is
    /// in the middle of it.
    const float CurtainHeight = 2.5f;

    /// THE DRIFT THRESHOLD (spec §18.1).
    ///
    /// Loose snow lifts in a low wind, wind-packed snow stays in place up to a high wind;
    /// the threshold is blended between the two by the density. A 4 m/s band gives a soft
    /// opening above the threshold — drift starting abruptly at the threshold looked like a cut.
    public static float DriftActiveFor(float windSpeed, float looseFraction)
    {
        float rhoN = 1f - Mathf.Clamp01(looseFraction);
        float threshold = Mathf.Lerp(SnowConstants.DriftU10Loose,
                                     SnowConstants.DriftU10Packed, rhoN);

        return Mathf.Clamp01((windSpeed - threshold) / 4f);
    }

    void LateUpdate()
    {
        if (environment == null || settings == null) return;

        FollowTarget();

        DriftActive01 = DriftActiveFor(
            environment.WindSpeed, SnowRuntimeState.LooseSnowFraction);

        // Spec §18.7 System A: `_SpindriftRate * DriftActive01² * LooseSnowFraction`.
        //
        // IT IS SQUARED: just above the threshold the saltation starts weak and thickens
        // quickly as the wind rises. Linear, a thick layer would appear all at once at the
        // threshold.
        SpindriftRate = settings.SpindriftRate
                      * DriftActive01 * DriftActive01
                      * SnowRuntimeState.LooseSnowFraction;

        // THE WIND AS A FORCE. Without the velocity block `Orient: AlongVelocity`
        // collapses at zero speed and the particles shot out from a single point.
        Vector3 wind = environment.WindDirection * environment.WindSpeed;

        if (spindrift != null && spindrift.HasVector3(windProperty))
            spindrift.SetVector3(windProperty, wind * SpindriftWindShare * SpindriftDrag);

        if (curtain != null && curtain.HasVector3(windProperty))
            curtain.SetVector3(windProperty, wind * CurtainWindShare * CurtainDrag);

        Drive(spindrift, SpindriftRate);

        // The curtains have no rate; the capacity is 14 and the lifetime long. Only the
        // threshold goes to them and the graph derives the alpha from it (spec §18.7: it
        // fades as it rises, do not use a fixed alpha).
        if (curtain != null && curtain.HasFloat(driftProperty))
            curtain.SetFloat(driftProperty, DriftActive01);
    }

    /// THE SPAWN BOXES FOLLOW THE TARGET (spec §18.7).
    ///
    /// This was missing and both layers stood at the scene origin: measured, the position was
    /// (0,0,0) with the camera 7.5 km away. The rate was driven correctly, particles were
    /// born, and none of them was visible.
    ///
    /// Spec §18.7 puts the two layers in DIFFERENT places:
    ///   Saltation (spindrift)  — the camera's 30 m strip downwind,
    ///                            `y = groundY + random(0, 0.05)`. Hugging the ground.
    ///   Suspension (curtain)   — 35 m upwind, `y = groundY + h`.
    ///
    /// The spawn box in the graph is LOCAL; the world position comes from here.
    ///
    /// SNAPPED TO A 1 m GRID — the same reason as `SnowfallLayers`: without the snap the
    /// spawn pattern walks as the camera moves and the particles look like a cluster being
    /// dragged behind the camera.
    void FollowTarget()
    {
        if (followTarget == null) return;

        Vector3 wind = environment.WindDirection;
        Vector3 p = followTarget.position;

        if (spindrift != null)
            spindrift.transform.position = Snap(p + wind * SpindriftLead);

        if (curtain != null)
            curtain.transform.position = Snap(p - wind * CurtainUpwind
                                              + Vector3.up * CurtainHeight);
    }

    static Vector3 Snap(Vector3 v) =>
        new Vector3(Mathf.Floor(v.x), Mathf.Floor(v.y), Mathf.Floor(v.z));

    void Drive(VisualEffect vfx, float rate)
    {
        if (vfx == null) return;

        if (vfx.HasFloat(rateProperty)) vfx.SetFloat(rateProperty, rate);
        if (vfx.HasFloat(driftProperty)) vfx.SetFloat(driftProperty, DriftActive01);
    }
}
