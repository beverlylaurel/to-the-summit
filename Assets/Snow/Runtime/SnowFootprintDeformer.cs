// ROLE: opens the trail as two separate footprints. Each foot is three capsules: heel,
// arch, forefoot. The foot is pinned in the world at the moment it lands, and stays there
// until it lifts.
// CALLED BY: SnowDeformerRegistry (registration), SnowManager (the piece buffer).

using UnityEngine;

/// A FOOTPRINT IS NOT EQUALLY DEEP EVERYWHERE.
///
/// [SOURCE: footprint drawing guides — "a foot would push deeper into only
/// SOME of the snow while stepping", "distinct toes and heel marks".]
/// The weight rides on the heel and the forefoot; the arch in between barely touches the
/// ground and the snow stays shallow there. A single uniform capsule therefore reads as
/// artificial.
///
/// Each foot is THREE CAPSULES:
///   forefoot — wide, deep
///   arch     — narrow, SHALLOW
///   heel     — medium width, deep
///
/// THE THREE EARLIER ATTEMPTS AND WHY THEY FAILED:
///
/// 1. A single stamp on the step event. The mark appeared ALL AT ONCE every half stride
///    (39 cm): the stamp is pressed AFTER the character has covered that distance
///    (the user: "like placing a block in Minecraft, delayed").
/// 2. Sweeping two lines continuously. The feet never left the ground and two parallel
///    grooves came out (the user: "my two feet are tied together with a rope").
/// 3. A fixed foot plus the leg's ploughing mark. The delay was solved but every step gave
///    a DUMBBELL shape: two ovals with a thin bar from the ploughing mark between them.
///    Walking sideways that bar turned into a straight line.
///
/// What is here is a corrected version of 3: there is NO ploughing mark (that was the
/// dumbbell's bar), and the foot is not one capsule but three (a real boot silhouette
/// instead of blobs). The position freezes at the moment of landing but the MARK is written
/// EVERY FRAME from that moment on — that is what closes the delay.
[DisallowMultipleComponent]
public class SnowFootprintDeformer : SnowDeformer
{
    [Header("Kaynak")]
    [Tooltip("The rhythm the step event is read from.")]
    [SerializeField] SnowStepRhythm rhythm;

    [Header("Stance")]
    [Tooltip("The distance between the two feet's centres (m). ~0.20 in a human stance.")]
    [SerializeField, Min(0.02f)] float stanceWidth = 0.20f;

    [Tooltip("The length of the boot sole (m).")]
    [SerializeField, Min(0.05f)] float bootLength = 0.30f;

    [Tooltip("The width of the boot sole (m).")]
    [SerializeField, Min(0.03f)] float bootWidth = 0.11f;

    [Tooltip("The toe's outward angle from the direction of travel (degrees).")]
    [SerializeField, Range(0f, 20f)] float toeOut = 7f;

    /// THE THREE SECTIONS OF THE BOOT SOLE.
    ///
    /// The values are ratios of the boot's length and width. The measures come from a real
    /// sole: the forefoot is the widest place, the arch markedly narrow, the heel between
    /// the two.
    ///   x = the centre's place along the foot axis (a ratio of the length, + forward)
    ///   y = the section's length (a ratio of the length)
    ///   z = the radius (a ratio of half the width)
    ///   w = the sinking share
    static readonly Vector4[] Bolumler =
    {
        new(+0.30f, 0.44f, 1.00f, 1.00f),   // forefoot
        new(+0.02f, 0.26f, 0.62f, 0.45f),   // arch — SHALLOW
        new(-0.31f, 0.34f, 0.84f, 0.95f),   // topuk
    };

    struct Ayak
    {
        public Vector3 pos;
        public Vector2 ileri;
        public bool    basili;
    }

    Ayak sol, sag;

    public override int SegmentCount => Bolumler.Length * 2;

    public override void GetSegment(int index, out Vector4 a, out Vector4 b)
    {
        // The base class supplies the path's fluctuation (width and depth).
        base.GetSegment(index, out Vector4 baseA, out Vector4 baseB);

        Ayak ayak = index < Bolumler.Length ? sol : sag;
        Vector4 bol = Bolumler[index % Bolumler.Length];

        float radius = bootWidth * 0.5f * bol.z * (baseA.w / Mathf.Max(Radius, 1e-4f));
        float halfLen    = Mathf.Max(0f, bootLength * bol.y * 0.5f - radius);

        var ileri3 = new Vector3(ayak.ileri.x, 0f, ayak.ileri.y);
        Vector3 mid = ayak.pos + ileri3 * (bootLength * bol.x);

        Vector3 pa = mid - ileri3 * halfLen;
        Vector3 pb = mid + ileri3 * halfLen;

        // A foot in the air leaves no mark: the sinking multiplier is zero and `KDeform`
        // writes nothing there.
        float pressure = ayak.basili ? baseB.w * bol.w : 0f;

        a = new Vector4(pa.x, pa.y, pa.z, radius);
        b = new Vector4(pb.x, pb.y, pb.z, pressure);
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        Yerlestir(ref sol, true);
        Yerlestir(ref sag, false);

        // Dururken iki ayak da yerde.
        sol.basili = true;
        sag.basili = true;

        if (rhythm != null) rhythm.Stepped += Bas;
    }

    protected override void OnDisable()
    {
        base.OnDisable();

        if (rhythm != null) rhythm.Stepped -= Bas;
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();

        if (rhythm == null) return;

        // ON STOPPING BOTH FEET COME DOWN. The rhythm resets the phase below the speed
        // threshold; leaving a foot hanging in the air there would be wrong.
        if (rhythm.Speed <= 0.001f)
        {
            if (!sol.basili) Yerlestir(ref sol, true);
            if (!sag.basili) Yerlestir(ref sag, false);

            sol.basili = true;
            sag.basili = true;
        }
    }

    /// A step fell: this foot is on the ground, the other lifts.
    void Bas(int ayak)
    {
        if (ayak == 0)
        {
            Yerlestir(ref sol, true);
            sol.basili = true;
            sag.basili = false;
        }
        else
        {
            Yerlestir(ref sag, false);
            sag.basili = true;
            sol.basili = false;
        }
    }

    /// Places the foot beside the body, according to the direction of travel.
    ///
    /// THE DIRECTION COMES FROM THE VELOCITY, NOT THE GAZE. While the player strafes (A/D)
    /// the body keeps facing forward but the feet turn to the direction of travel. Tied to
    /// the gaze, the marks would come out perpendicular to the walking line when strafing.
    void Yerlestir(ref Ayak ayak, bool solMu)
    {
        Vector3 ileri3 = new Vector3(VelocityXZ.x, 0f, VelocityXZ.y);

        if (ileri3.sqrMagnitude < 1e-4f)
        {
            ileri3 = transform.forward;
            ileri3.y = 0f;
        }

        if (ileri3.sqrMagnitude < 1e-6f) ileri3 = Vector3.forward;
        ileri3.Normalize();

        var sag3 = new Vector3(ileri3.z, 0f, -ileri3.x);

        // The toe points outward: the left foot to the left, the right foot to the right.
        float rad = (solMu ? -toeOut : toeOut) * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);

        ayak.pos = transform.position + sag3 * (solMu ? -0.5f : 0.5f) * stanceWidth;
        ayak.ileri = new Vector2(ileri3.x * c + ileri3.z * s,
                                 -ileri3.x * s + ileri3.z * c).normalized;
    }
}
