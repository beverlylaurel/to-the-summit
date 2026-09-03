// ROLE: drives the snow collecting on the character. It writes `_SnowAccum` to the
// character's material; if the shader does not read it nothing happens.
// CALLED BY: the scene (on the character).

using UnityEngine;

/// THE EXISTING CHARACTER SHADER WAS NOT CHANGED (spec §1.4, §16.1).
///
/// This component writes `_SnowAccum` and `_SnowLineY` with a `MaterialPropertyBlock`.
/// If the character shader does not know these properties the write is silently
/// ignored — nothing breaks, the effect simply does not show.
/// Adding the properties to the shader is a separate decision and belongs to the user.
[DisallowMultipleComponent]
public class SnowCharacterAccumulator : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The renderers the snow shows on. Left empty, every renderer under this "
             + "object is scanned once.")]
    [SerializeField] Renderer[] targets;

    [Tooltip("The environment source needed for the rain to clean the snow off.")]
    [SerializeField] MonoBehaviour environmentSource;

    [Tooltip("The foot position — the leg snow line is computed from it.")]
    [SerializeField] Transform footAnchor;

    [Header("Ayarlar")]
    [Tooltip("The rate accumulated per second while it is snowing.")]
    [SerializeField] float accumulationRate = 0.05f;

    [Tooltip("The rate wiped off per second, per unit of speed.")]
    [SerializeField] float shakeOffRate = 0.06f;

    [Tooltip("The rate wiped off per second in rain.")]
    [SerializeField] float rainClearRate = 0.4f;

    [Tooltip("The sky visibility counted as indoors.")]
    [SerializeField, Range(0f, 1f)] float shelteredBelow = 0.3f;

    [Tooltip("The rate wiped off per second indoors.")]
    [SerializeField] float shelterClearRate = 0.25f;

    ISnowEnvironmentSource env;
    MaterialPropertyBlock block;

    Vector3 prevPos;
    float accum;
    float skyVisibility = 1f;

    public float Accumulation => accum;

    /// FOR BINDING THE BRIDGE FROM CODE. The Inspector field holds a `MonoBehaviour`
    /// — Unity cannot serialize an interface type. The side binding (or testing) it from
    /// code uses this.
    public void SetEnvironment(ISnowEnvironmentSource source) => env = source;

    /// The sky visibility is on the GPU; it is supplied to the CPU side from here.
    /// Once `SnowSampler` (Phase 9) arrives it will be fed from there.
    public void SetSkyVisibility(float value) => skyVisibility = Mathf.Clamp01(value);

    void OnEnable()
    {
        env = environmentSource as ISnowEnvironmentSource;

        if (targets == null || targets.Length == 0)
            targets = GetComponentsInChildren<Renderer>();

        block ??= new MaterialPropertyBlock();
        prevPos = transform.position;
        accum = 0f;
    }

    void LateUpdate()
    {
        Vector3 p = transform.position;
        float speed = (p - prevPos).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
        prevPos = p;

        Step(Time.deltaTime, speed);
    }

    /// THE STEP IS IN A SEPARATE METHOD. `LateUpdate` only measures the speed; the
    /// accumulation logic is callable from outside so it can be tested without entering Play.
    public void Step(float dt, float speed)
    {
        accum += SnowRuntimeState.SnowfallIntensity01 * accumulationRate * dt * skyVisibility;
        accum -= speed * shakeOffRate * dt;

        if (skyVisibility < shelteredBelow) accum -= shelterClearRate * dt;

        // RAIN WIPES THE SNOW OFF FAST (spec §16.1) — but the condition DIFFERS from the
        // spec's, for a measured reason.
        //
        // The spec says `env.PrecipKind == Rain`. The condition here is wider, and stays
        // wider now that the bridge answers Rain / Sleet / Snow properly (2026-09-03; it
        // used to answer `Rain` to everyone).
        //
        // SLEET IS THE REASON. The kind turns to `Sleet` anywhere in the 0.5-2.0 C band,
        // but what actually FALLS is decided by one threshold at a snow share of 0.5
        // (`SnowfallController` — the user's decision: never both at once). At a share of
        // 0.3 the kind is Sleet while rain is what is falling, and `PrecipKind == Rain`
        // would leave the character caked in snow through it.
        //
        // So the condition asks the question that matches what is drawn: there IS
        // precipitation and snow is NOT the thing coming down.
        bool raining = env != null &&
                       env.PrecipKind != PrecipitationKind.None &&
                       !SnowRuntimeState.IsSnowing;

        if (raining) accum -= rainClearRate * dt;

        accum = Mathf.Clamp01(accum);

        Publish();
    }

    void Publish()
    {
        block ??= new MaterialPropertyBlock();

        float lineY = footAnchor != null ? footAnchor.position.y : transform.position.y;

        block.SetFloat(SnowShaderIDs.SnowAccum, accum);
        block.SetFloat(SnowShaderIDs.SnowLineY, lineY);

        if (targets == null) return;

        for (int i = 0; i < targets.Length; i++)
        {
            Renderer r = targets[i];
            if (r != null) r.SetPropertyBlock(block);
        }
    }
}
