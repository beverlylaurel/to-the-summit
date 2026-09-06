using UnityEngine;

/// Optional metadata for a collider that participates in player surface interactions.
/// Unmarked terrain accepts simulated snow; unmarked constructed geometry does not.
[DisallowMultipleComponent]
public sealed class GroundSurfaceProperties : MonoBehaviour
{
    [SerializeField] bool acceptsSnow;

    public bool AcceptsSnow => acceptsSnow;
}
