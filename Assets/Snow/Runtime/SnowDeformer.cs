// ROLE: makes this object leave a mark in the snow. It describes itself as one or more
// capsule pieces.
// CALLED BY: SnowDeformerRegistry (registration), SnowManager (the piece buffer).

using UnityEngine;

/// THE TRAIL IS NOT RASTERIZED, IT IS COMPUTED.
///
/// The old form DREW the object's underside into an orthographic capture looking up from
/// below (the Batman GDC 2014 route). It gave the real shape for free but the edge snagged
/// on the texel grid in three separate places: the raster's own edge, the Poisson blur's
/// four taps, and the coverage share's threshold. Measured: walking straight, the trail's
/// edge wobbles by ±1.5 texels — lumpy up close, saw teeth at distance.
///
/// The new form: the object is a CAPSULE (two ends + a radius). `KDeform` finds the texel's
/// horizontal distance to the capsule in closed form and writes the excavation as a
/// continuous function of that distance. No grid, no raster, no blur, no threshold.
///
/// THE HEIGHT IS NOT PUBLISHED. How far it sinks is told by the snow (bearing capacity,
/// density, crust); the object only says WHERE it is and how much it compacts the load.
/// Had the object's Y been read and had that Y been derived from the snow's state, a loop
/// would close — it closed once (`SYMPTOMS.md`).
///
/// SEPARATE FOOTPRINTS WERE TRIED AND REVERTED. The step event falls every half stride
/// (39 cm) and the mark appeared ALL AT ONCE at that moment — the user reported it as
/// "like placing a block in Minecraft, delayed". On top of that, separate left/right stamps
/// read as a zigzag on screen. The irregularity has to come from a CONTINUOUS field, not
/// from a discrete stamp.
[ExecuteAlways]
[DisallowMultipleComponent]
public class SnowDeformer : MonoBehaviour
{
    [Tooltip("The radius of the capsule crushing the snow (m).")]
    [SerializeField, Min(0.01f)] float radius = 0.15f;

    [Tooltip("How much the radius varies along the path (0 = fixed).")]
    [SerializeField, Range(0f, 0.5f)] float widthWobble = 0.16f;

    [Tooltip("How much the sinking varies along the path (0 = fixed).")]
    [SerializeField, Range(0f, 0.5f)] float depthWobble = 0.22f;

    [Tooltip("The wavelength of the variation (m). It should be of the order of a stride.")]
    [SerializeField, Min(0.05f)] float wobbleLength = 0.55f;

    Vector3 prevPosition;
    Vector3 segmentA, segmentB;

    /// The horizontal distance travelled (m). The irregularity depends on DISTANCE, not on
    /// TIME: standing still the mark does not move, and walking slowly gives the same pattern.
    float travelled;

    public float Radius => radius;

    /// Horizontal speed (m/s). It makes the ridge asymmetric along the direction of motion
    /// (spec §10.2).
    public Vector2 VelocityXZ { get; private set; }

    /// The number of pieces to be written to the snow texture this frame.
    public virtual int SegmentCount => 1;

    /// `a.xyz` the start, `a.w` the radius; `b.xyz` the end, `b.w` the sinking multiplier.
    ///
    /// THE WIDTH AND THE DEPTH FLUCTUATE ALONG THE PATH — CONTINUOUSLY, NOT ABRUPTLY.
    ///
    /// A fixed radius and a fixed sinking give a uniform groove like a pipe
    /// (the user reported it: "is a trail really this regular when you walk in one
    /// direction?"). In real walking every footfall lands in a slightly different place at a
    /// slightly different depth, and the groove widens and narrows along its length.
    ///
    /// The modulation depends on the DISTANCE TRAVELLED and is CONTINUOUS. A discrete stamp
    /// was tried (one mark per step) and rejected: the mark appeared all at once every 39 cm.
    /// A continuous wave gives the same irregularity but the trail's tip grows one notch
    /// longer every frame — nothing appears out of nowhere.
    public virtual void GetSegment(int index, out Vector4 a, out Vector4 b)
    {
        // A separate sample at each end: the piece itself narrows and widens along its length.
        float wA = Dalga(travelled - Vector3.Distance(segmentA, segmentB));
        float wB = Dalga(travelled);

        float rA = radius * (1f + wA * widthWobble);
        float derinlik = 1f + wB * depthWobble;

        a = new Vector4(segmentA.x, segmentA.y, segmentA.z, rA);
        b = new Vector4(segmentB.x, segmentB.y, segmentB.z, derinlik);
    }

    /// VALUE NOISE, NOT A SINE, −1..1.
    ///
    /// It was the sum of two sines first and it was exactly PERIODIC: the pattern repeated
    /// itself identically every 55 cm (the user reported it: "it has a very regular pattern,
    /// it always makes the same mark"). Two sines at different frequencies are not
    /// "irregular", only longer in period.
    ///
    /// The value noise comes from a hash: it does not repeat, but because it depends on the
    /// distance it is reproducible — the same path gives the same trail and rewinding is not broken.
    float Dalga(float s)
    {
        float u = s / Mathf.Max(0.05f, wobbleLength);

        return (Gurultu(u) * 0.62f
              + Gurultu(u * 2.17f + 11.3f) * 0.26f
              + Gurultu(u * 4.61f + 37.9f) * 0.12f) * 2f - 1f;
    }

    /// One-dimensional value noise, 0..1. The hashes of two integer cells are blended with a
    /// smoothstepped fraction.
    static float Gurultu(float u)
    {
        float h = Mathf.Floor(u);
        float f = u - h;
        f = f * f * (3f - 2f * f);

        return Mathf.Lerp(Hash((int)h), Hash((int)h + 1), f);
    }

    static float Hash(int n)
    {
        uint x = (uint)n * 747796405u + 2891336453u;
        x = ((x >> ((int)(x >> 28) + 4)) ^ x) * 277803737u;
        x = (x >> 22) ^ x;

        return x * (1f / 4294967296f);
    }

    protected virtual void OnEnable()
    {
        prevPosition = transform.position;
        segmentA = prevPosition;
        segmentB = prevPosition;
        VelocityXZ = Vector2.zero;
        travelled = 0f;

        SnowDeformerRegistry.Register(this);
    }

    protected virtual void OnDisable() => SnowDeformerRegistry.Unregister(this);

    /// THE PIECE IS STORED, NOT DERIVED.
    ///
    /// `SnowManager` reads the piece at draw time; at that moment this component's
    /// `LateUpdate` may or may not have run. Because the piece is stored here the read order
    /// does not change the result: at worst a one-frame-old piece is used, and because the
    /// pieces are joined end to end no gap forms in the trail.
    protected virtual void LateUpdate()
    {
        Vector3 p = transform.position;

        segmentA = prevPosition;
        segmentB = p;

        Vector3 yatay = p - prevPosition;
        yatay.y = 0f;
        travelled += yatay.magnitude;

        Vector3 v = (p - prevPosition) / Mathf.Max(Time.deltaTime, 1e-4f);
        VelocityXZ = new Vector2(v.x, v.z);

        prevPosition = p;
    }
}
