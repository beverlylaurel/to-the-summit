using UnityEngine;

/// Shared tuning for restrained first-person camera motion. Values stay deliberately below
/// normal head-bob presets: the motion should be felt while moving, not demand attention.
[CreateAssetMenu(menuName = "To The Summit/Player/View Motion Settings")]
public sealed class PlayerViewMotionSettings : ScriptableObject
{
    [Header("Walk")]
    [Min(0.1f)] public float walkStride = 1.70f;
    [Min(0f)] public float walkVertical = 0.010f;
    [Min(0f)] public float walkLateral = 0.006f;
    [Min(0f)] public float walkRollDegrees = 0.08f;

    [Header("Sprint")]
    [Min(0.1f)] public float sprintStride = 2.25f;
    [Min(0f)] public float sprintVertical = 0.018f;
    [Min(0f)] public float sprintLateral = 0.010f;
    [Min(0f)] public float sprintRollDegrees = 0.14f;

    [Header("Turn inertia")]
    [Min(1f)] public float fullTurnSpeedDegrees = 320f;
    [Min(0f)] public float turnLateral = 0.0035f;
    [Min(0f)] public float turnRollDegrees = 0.22f;
    [Min(0.01f)] public float turnResponseSeconds = 0.09f;
    [Min(0.01f)] public float turnReturnSeconds = 0.16f;

    [Header("Landing")]
    [Min(0f)] public float landingDip = 0.014f;
    [Min(0f)] public float landingMinimumSpeed = 2.5f;
    [Min(0.01f)] public float landingReturnSeconds = 0.18f;

    [Header("Terrain contact")]
    [Tooltip("Vertical discontinuities below this size are treated as continuous ground.")]
    [Min(0f)] public float stepDetectionThreshold = 0.015f;
    [Tooltip("Maximum camera counter-offset applied to an abrupt terrain step.")]
    [Min(0f)] public float terrainStepMaxOffset = 0.035f;
    [Min(0.01f)] public float terrainStepReturnSeconds = 0.11f;

    [Header("Blending")]
    [Min(0.01f)] public float movementFadeSeconds = 0.16f;
    [Min(0.01f)] public float sprintBlendSeconds = 0.28f;
    [Min(0f)] public float movingThreshold = 0.12f;
}
