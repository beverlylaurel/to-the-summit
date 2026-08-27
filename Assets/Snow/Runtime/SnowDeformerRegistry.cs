// ROLE: the list of active SnowDeformers in the scene. SnowManager walks this list;
// it does not search.
// CALLED BY: SnowDeformer (registration), SnowManager (the piece buffer).

using System.Collections.Generic;

/// NO SEARCH, A REGISTRY. `FindObjectsByType` scans the scene every frame and
/// allocates (spec §0.8). The component registers itself; the list is always
/// current and walking it is free.
///
/// The same pattern as `SnowHeatRegistry` in spec §18.2 — a static registry is
/// wanted there too, and here.
public static class SnowDeformerRegistry
{
    /// 64: more than enough for the number of objects leaving marks in the snow at once in a
    /// scene. If it is exceeded the list grows, there is no error — only a one-off allocation.
    const int InitialCapacity = 64;

    static readonly List<SnowDeformer> Active = new(InitialCapacity);

    public static int Count => Active.Count;

    public static SnowDeformer Get(int index) => Active[index];

    public static void Register(SnowDeformer deformer)
    {
        if (deformer == null || Active.Contains(deformer)) return;
        Active.Add(deformer);
    }

    public static void Unregister(SnowDeformer deformer) => Active.Remove(deformer);

    /// When the editor domain is not reloaded (Play mode domain reload off) the static list
    /// survives from the previous session and carries dead references.
    [UnityEngine.RuntimeInitializeOnLoadMethod(
        UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnLoad() => Active.Clear();
}
