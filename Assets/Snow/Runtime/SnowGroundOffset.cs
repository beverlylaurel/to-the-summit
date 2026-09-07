using UnityEngine;

/// Resolves the drawn snow surface after ordinary capsule movement. Constructed floors
/// retain their physical collision height; no remembered offset is subtracted on exit.
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class SnowGroundOffset : MonoBehaviour
{
    [SerializeField] SnowManager snowManager;
    CharacterController controller;
    GroundSurfaceContact surfaceContact;
    public bool IsSupporting { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        surfaceContact = GroundSurfaceContact.Require(this);
    }

    void OnEnable() => IsSupporting = false;
    void OnDisable() => IsSupporting = false;

    // Called by the movement owner, after Move and before any footstep consumers.
    public void Resolve(bool rising)
    {
        IsSupporting = false;
        if (!isActiveAndEnabled || controller == null || !controller.enabled) return;
        surfaceContact.RefreshNow();
        if (snowManager == null || !surfaceContact.SupportsSnow || rising) return;
        float depth = snowManager.WorldSnowDepth;
        if (depth <= 0f) return;
        Vector3 p = transform.position;
        float relief = SnowSurfaceHeight.ReliefWorld(p, depth,
            snowManager.WindShadowAt(p), snowManager.SastrugiWindDir);
        ResolveHeight(surfaceContact.Point.y + relief);
    }

    void ResolveHeight(float height)
    {
        float gap = height - surfaceContact.FootHeight;
        // Gravity owns descent. A falling character is caught only at the surface,
        // never pulled down from the apex of a jump or across a doorway.
        if (gap < -0.03f) return;
        // This is collision support, not a camera animation. Smoothing the correction
        // leaves a permanent penetration because gravity is applied again next frame.
        if (gap > 0f) controller.Move(Vector3.up * gap);
        // A ceiling may block the correction. Never bypass the capsule to reach snow.
        IsSupporting = Mathf.Abs(height - surfaceContact.FootHeight) <= 0.05f;
    }
}
