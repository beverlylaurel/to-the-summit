using UnityEngine;

/// Physical and presentation tuning for the model-free player headlamp.
/// The two cones represent a real lamp's focused optic and its softer peripheral spill.
[CreateAssetMenu(menuName = "To The Summit/Player/Headlamp Settings")]
public sealed class HeadlampSettings : ScriptableObject
{
    [Header("Mount")]
    [Tooltip("Position of the lamp relative to the rendered first-person camera.")]
    public Vector3 mountOffset = new(0f, 0.065f, 0.045f);
    [Tooltip("A slight downward aim keeps useful light on the walking surface.")]
    public Vector3 mountEulerAngles = new(2.5f, 0f, 0f);

    [Header("Light source")]
    [Range(1500f, 20000f)] public float colorTemperatureKelvin = 5000f;
    [Min(0.01f)] public float switchResponseSeconds = 0.065f;
    public bool startsOn;

    [Header("Focused beam")]
    [Min(0f)] public float hotspotLumens = 320f;
    [Min(0.1f)] public float hotspotRange = 68f;
    [Range(1f, 179f)] public float hotspotOuterAngle = 42f;
    [Range(0f, 179f)] public float hotspotInnerAngle = 25f;
    [Range(0f, 1f)] public float hotspotShadowStrength = 0.82f;

    [Header("Peripheral spill")]
    [Min(0f)] public float spillLumens = 120f;
    [Min(0.1f)] public float spillRange = 26f;
    [Range(1f, 179f)] public float spillOuterAngle = 84f;
    [Range(0f, 179f)] public float spillInnerAngle = 58f;
}
