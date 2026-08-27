// ROL: cevresine sicaklik alani yayar (spec 18.2, Grosbellet theta alani).
// Caginan: SnowHeatRegistry (kayit).

using UnityEngine;

/// IT DOES NOTHING IN UPDATE. The component only registers itself;
/// its position and fields are read by `SnowHeatRegistry`. It was NOT ADDED to the
/// fire/torch prefabs - that is a prefab change and the user's decision (spec 1.4).
[DisallowMultipleComponent]
public class SnowHeatSource : MonoBehaviour
{
    [Tooltip("Radius of effect (m) — at this distance the effect is EXACTLY zero.")]
    public float radius = 2.5f;

    [Tooltip("The snow height removed at the centre (m). Grosbellet's theta field.")]
    public float strength = 0.45f;

    void OnEnable() => SnowHeatRegistry.Register(this);
    void OnDisable() => SnowHeatRegistry.Unregister(this);
}
