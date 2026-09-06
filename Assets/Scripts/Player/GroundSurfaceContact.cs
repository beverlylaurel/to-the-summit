using UnityEngine;

/// Measures the physical surface directly under the character. World-space snow data is
/// meaningful only when this contact says the character is standing on a snow-bearing surface.
[DefaultExecutionOrder(-450)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class GroundSurfaceContact : MonoBehaviour
{
    const float ProbeLift = 0.35f;
    const float ProbeDistance = 1.6f;

    readonly RaycastHit[] hits = new RaycastHit[24];
    public Collider Collider { get; private set; }
    public Vector3 Point { get; private set; }
    public Vector3 Normal { get; private set; } = Vector3.up;
    public bool HasContact => Collider != null;
    public bool SupportsSnow { get; private set; }

    public static GroundSurfaceContact Require(Component owner)
    {
        if (owner == null) return null;

        CharacterController body = owner.GetComponentInParent<CharacterController>();
        if (body == null) return owner.GetComponentInParent<GroundSurfaceContact>();

        GroundSurfaceContact contact = body.GetComponent<GroundSurfaceContact>();
        return contact != null ? contact : body.gameObject.AddComponent<GroundSurfaceContact>();
    }

    void OnEnable() => RefreshNow();

    void LateUpdate() => RefreshNow();

    public void RefreshNow()
    {
        Collider = null;
        SupportsSnow = false;
        Point = transform.position;
        Normal = Vector3.up;

        Vector3 origin = transform.position + Vector3.up * ProbeLift;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, ProbeDistance,
                                            Physics.DefaultRaycastLayers,
                                            QueryTriggerInteraction.Ignore);

        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = hits[i].collider;
            if (candidate == null || candidate.transform.IsChildOf(transform)) continue;
            if (hits[i].distance >= nearest) continue;

            nearest = hits[i].distance;
            Collider = candidate;
            Point = hits[i].point;
            Normal = hits[i].normal;
        }

        if (Collider == null) return;

        GroundSurfaceProperties properties = Collider.GetComponentInParent<GroundSurfaceProperties>();
        SupportsSnow = properties != null
            ? properties.AcceptsSnow
            : Collider is TerrainCollider;
    }
}
