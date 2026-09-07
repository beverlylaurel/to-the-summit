using UnityEngine;

/// Rain-only presentation controls. Snow simulation never reads this asset.
[CreateAssetMenu(menuName = "To The Summit/Weather/Rain Motion Settings")]
public sealed class RainMotionSettings : ScriptableObject
{
    [Tooltip("Scales both fall displacement and the exposure trajectory.")]
    [Range(1f, 1.8f)] public float fallSpeedScale = 1.4f;
    [Tooltip("Fraction of the existing size-dependent wind response.")]
    [Range(0f, 1f)] public float windResponseScale = 0.12f;
}
