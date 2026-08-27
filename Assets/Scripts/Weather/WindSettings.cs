using UnityEngine;

/// The wind's speed, gust and direction settings.
///
/// As long as they sat on the component as `[SerializeField]` there were three copies of each
/// value: the default in code, the serialized copy in the scene, and the one actually running.
/// The scene wins, and on top of that Unity rewrites the scene to disk from its own memory
/// whenever it likes — a fix made in code was silently reverted.
///
/// Severity is not here: `AltitudeWeatherDriver` drives it. The wind does not decide how hard it
/// blows, only how it blows.
[CreateAssetMenu(menuName = "To The Summit/Wind", fileName = "WindSettings")]
public class WindSettings : ScriptableObject
{
    [Header("Base wind")]
    /// SURFACE WIND, not the free atmosphere. The cloud layer takes its own base from
    /// `CloudWeatherDriver` — surface friction slows the surface, what is above does not stop.
    ///
    /// It was 2 m/s and meant Beaufort 2 ("light breeze"): there was never still air on
    /// the mountain. Because snow's terminal velocity is 1 m/s, a 2 m/s wind tilts a
    /// flake 63° off vertical — the panel said "wind 0" while the screen showed
    /// obviously slanted snow. 0.6 is Beaufort 1 ("light air"); with the exposure
    /// multiplier it is 0.21-0.87 m/s, i.e. between 12° and 41°.
    [Tooltip("SURFACE speed at severity 0 (m/s). Must not be zero.")]
    public float calmSpeed = 0.6f;
    [Tooltip("Speed at severity 1 (m/s). Full storm.")]
    public float stormSpeed = 14f;

    [Header("Arazi maruziyeti")]
    [Tooltip("The fraction of the sustained speed left in a sheltered hollow. This is the " +
             "biggest difference felt on a mountain: you cannot stand on the ridge, thirty " +
             "kesilir.")]
    [Range(0.1f, 1f)] public float shelteredFactor = 0.35f;
    [Tooltip("The multiple of the sustained speed reached on an exposed ridge. The wind " +
             "compresses and speeds up as it crosses the crest.")]
    [Range(1f, 2.5f)] public float exposedFactor = 1.45f;
    [Tooltip("Speed of the base oscillation. 0.011 ≈ a 90 second period.")]
    public float baseFrequency = 0.011f;
    [Tooltip("How much the base speed oscillates around itself.")]
    [Range(0f, 1f)] public float baseVariation = 0.25f;

    [Header("Esinti")]
    [Tooltip("The gust's ratio to the base speed.")]
    [Range(0f, 1f)] public float gustAmount = 0.4f;
    [Tooltip("Gust frequency. 0.08 ≈ a 12 second period.")]
    public float gustFrequency = 0.08f;
    [Tooltip("The share of sub-second buffeting: short hits that ripple a jacket. " +
             "It rides on top of the gust.")]
    [Range(0f, 1f)] public float flickerAmount = 0.12f;
    [Tooltip("Buffet frequency. 0.5 ≈ a 2 second period.")]
    public float flickerFrequency = 0.5f;

    [Header("Direction")]
    [Tooltip("The mountain's PREVAILING wind direction (degrees, counter-clockwise from +X). " +
             "The snow pattern sits on this axis: the shape forms over hours, " +
             "it does not turn with an instantaneous gust.")]
    [Range(0f, 360f)] public float prevailingDegrees = 205f;

    [Tooltip("The instantaneous wind's swing around the prevailing direction (degrees). The " +
             "wind does not come from every direction; a mountain has a prevailing wind and the " +
             "gust plays around it. A free 720° sweep was tried and reverted: because the drift " +
             "field reads `dot(worldXZ, windAxis)`, in the middle of the mountain (|worldXZ| ≈ 7000 m) " +
             "a 0.14 radian deviation shifted the pattern by 980 metres — the body is 45 m.")]
    [Range(0f, 90f)] public float directionSpread = 35f;

    [Tooltip("Speed of the direction drift.")]
    public float directionDrift = 0.02f;
}
