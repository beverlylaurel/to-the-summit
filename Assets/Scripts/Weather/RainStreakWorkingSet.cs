using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// THE SLICE OF THE STREAK DATABASE NEEDED PER FRAME — `[Garg 2006]`, `rain-spec.md` §6.3.1.
///
/// The database is 4500 slices (v 10 × h 9 × osc 10) × 5 camera angles. They do not all have to
/// be on the GPU: the scene has a SINGLE directional source (the sun), so only that direction's
/// four angular neighbours are read per frame.
///
///   4 neighbours (v,h) × 5 dcam × 10 osc = 200 slices
///
/// 3.4 MB at `size16`. No copy is made while the sun's angular cell does not change.
///
/// WHAT THE ANGLES ARE MEASURED AGAINST — confuse this and the streaks are lit wrong:
///
///   `v` — the light's elevation angle relative to the drop's FALL AXIS. Independent of the
///         camera. Measured: `θ_l = 90° − v` (at v = ±90 the database has a single `h`, i.e. a
///         pole; the mapping `rain-spec.md` §11.2-8 left unverified).
///   `h` — the light's azimuth from the drop's x axis. That axis is the projection of the
///         CAMERA's optical axis onto the plane perpendicular to the fall axis (`§2.1`), so `h`
///         turns when the camera turns. It has to be recomputed every frame.
///   `dcam` — the camera's deviation from vertical, `θ_v = 90° − dcam`. It varies from drop to
///         drop across the screen (each drop is seen from a different direction), so all five
///         angles stay in the working set.
public class RainStreakWorkingSet : MonoBehaviour
{
    [Tooltip("The baked streak database. `To The Summit/Rain/Set Up Streak Database` produces it.")]
    [SerializeField] RainStreakDatabase database;

    /// RESOLUTION LEVEL — `[Garg 2006, §5]`: "the resolution level with textures of
    /// widths just larger than the width of the projected rain streak".
    ///
    /// The levels in ascending order: `size4` (4×132), `size8` (8×263), `size16` (16×525).
    /// For a while the highest (`size16`) was always used; MEASUREMENT showed that was wrong.
    ///
    /// Our streaks are 1.2 pixels wide on screen (the `MinPixelWidth` floor) and their real
    /// widths are finer still: a 1.4 mm drop is 1.4 px at 1 metre and 0.28 px at 5 metres.
    /// The only case above 4 pixels is a drop larger than 4 mm closer than 1 metre — computed on
    /// paper, ONE OR TWO particles per frame. So the paper's rule says `size4` for the whole scene.
    ///
    /// The cost of using `size16` is not a needlessly large texture but DOWNSAMPLING: a streak
    /// 525 pixels tall comes down to 9 pixels on a distant drop (58×) and arrays have NO mipmaps,
    /// so the hardware cannot fix it either. The paper's footnote says exactly this —
    /// "to avoid artifacts due to severe down-sampling when rendering streaks far from
    /// the camera". At `size4` the ratio comes down to 14×.
    ///
    /// The cost on a near drop: a streak 228 pixels tall on screen comes from 132 pixels, i.e. a
    /// 1.7× magnification — a slight softening. The share of particles affected is under one percent.
    ///
    /// Not serialized: once in the Inspector the component in the scene freezes on the old value
    /// and a change in code has no effect.
    const int level = 0;

    /// The slice layout copied per frame. The order is FIXED — the shader computes the index
    /// from it and does no lookup.
    ///   slice = ((corner * 5) + dcamIndex) * 10 + osc
    const int Corners = 4;
    const int Osc = 10;

    Texture2DArray point, ambient;
    int cachedV = int.MinValue, cachedH = int.MinValue;

    /// Directional source streaks, 200 slices.
    public Texture2DArray Point => point;

    /// Ambient streaks, 50 slices (dcam × osc). There is no light direction, they do not depend
    /// on the cell and are built once.
    public Texture2DArray Ambient => ambient;

    /// The corner weights of the `(v,h)` cell — the shader will use them in the bilinear blend.
    public Vector2 CellBlend { get; private set; }

    /// Whether each of the four corners is in the database (1) or not (0). The order is the one
    /// the shader expects: (vLow,hLow) (vLow,hHigh) (vHigh,hLow) (vHigh,hHigh).
    ///
    /// THE ABSENCE IS INDEPENDENT OF `osc` — measured: 740 present per `dcam`, and
    /// 8 v × 9 h × 10 + 2 v × 1 h × 10 = 740, so what is missing is only the `h ≠ 170` cells at
    /// the `v = ±90` poles, and there all ten `osc` are missing together.
    /// That is why a single value per corner is enough and no per-slice table is needed.
    public Vector4 CornerPresent { get; private set; }

    /// How many rows of each `dcam` slice are valid (0-1), `dcam` ascending. The streak length
    /// shortens with the camera angle and the working set is filled to the longest one; the
    /// shader must not sample beyond this share.
    ///
    /// IT IS NOT COMPUTED FROM `cos(dcam)`. The ratio is close to it (measured: 1.000/0.940/0.771/
    /// 0.517/0.206 against 1.000/0.940/0.766/0.500/0.174) but it is 18% off at 80°.
    /// The value is read from the texture itself.
    public float[] DcamHeightFraction { get; private set; }

    int Level => Mathf.Min(level, database.Sizes.Length - 1);

    /// The scene is set up from code; the database is not dragged in by hand.
    public void Bind(RainStreakDatabase streakDatabase) => database = streakDatabase;

    void OnEnable()
    {
        if (database == null)
            throw new MissingReferenceException(
                $"{name}: the streak database is not bound. It has to be set up from the menu and given to the Inspector.");

        BuildAmbient();
    }

    void OnDisable()
    {
        if (point != null) Destroy(point);
        if (ambient != null) Destroy(ambient);
        point = ambient = null;
        cachedV = cachedH = int.MinValue;
    }

    /// Computes the sun's angle in the drop's frame and refreshes the working set if needed.
    /// `fallAxis` is the precipitation's WORLD direction (downward), `viewAxis` the camera's
    /// optical axis.
    public void Refresh(Vector3 sunDirection, Vector3 fallAxis, Vector3 viewAxis)
    {
        // The drop's frame: the y axis is the OPPOSITE of the fall (`§2.1`).
        Vector3 up = -fallAxis.normalized;

        // The x axis: the projection of the camera's optical axis onto the plane perpendicular
        // to y. With the camera looking straight up or down the projection goes to zero; the
        // azimuth is undefined there anyway and any perpendicular axis gives the same result.
        Vector3 x = Vector3.ProjectOnPlane(viewAxis, up);
        x = x.sqrMagnitude > 1e-8f ? x.normalized : Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        Vector3 y = Vector3.Cross(up, x);

        Vector3 l = sunDirection.normalized;

        // `v` is the elevation measured from the horizontal plane: +90 one pole (from above), −90 the other.
        float v = Mathf.Asin(Mathf.Clamp(Vector3.Dot(l, up), -1f, 1f)) * Mathf.Rad2Deg;

        // `h` is the azimuth from the x axis. The database samples 10°–170°; 180°–360° is served
        // by a mirrored texture (`§5.2`), so the sign goes to the shader separately.
        float h = Mathf.Atan2(Vector3.Dot(l, y), Vector3.Dot(l, x)) * Mathf.Rad2Deg;
        MirroredAzimuth = h < 0f;
        h = Mathf.Abs(h);

        int vLow = LowerIndex(database.Vertical, v, out float vT);
        int hLow = LowerIndex(database.Horizontal, h, out float hT);
        CellBlend = new Vector2(vT, hT);

        if (vLow == cachedV && hLow == cachedH) return;
        cachedV = vLow;
        cachedH = hLow;
        BuildPoint(vLow, hLow);
    }

    /// Whether the azimuth was mirrored — `§5.2`: the texture is flipped horizontally above 180°.
    public bool MirroredAzimuth { get; private set; }

    /// The index of the lower neighbour on the axis and the blend share. The axis is in ascending order.
    static int LowerIndex(int[] axis, float value, out float t)
    {
        if (value <= axis[0]) { t = 0f; return 0; }
        if (value >= axis[^1]) { t = 0f; return axis.Length - 1; }

        int i = 0;
        while (i + 1 < axis.Length && axis[i + 1] <= value) i++;
        t = Mathf.InverseLerp(axis[i], axis[i + 1], value);
        return i;
    }

    void BuildAmbient()
    {
        var angles = database.Angles;
        var source = angles[0].Ambient[Level];

        ambient = new Texture2DArray(source.width, source.height, angles.Length * Osc,
                                     source.graphicsFormat, TextureCreationFlags.None)
        {
            name = "RainStreakAmbient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        for (int d = 0; d < angles.Length; d++)
        {
            var src = angles[d].Ambient[Level];
            for (int osc = 0; osc < Osc; osc++)
                Graphics.CopyTexture(src, osc, 0, 0, 0, src.width, src.height,
                                     ambient, d * Osc + osc, 0, 0, 0);
        }

        StoreHeightFractions();
    }

    void BuildPoint(int vLow, int hLow)
    {
        var angles = database.Angles;
        var tallest = angles[0].Point[Level];

        if (point == null)
        {
            point = new Texture2DArray(tallest.width, tallest.height,
                                       Corners * angles.Length * Osc,
                                       tallest.graphicsFormat, TextureCreationFlags.None)
            {
                name = "RainStreakWorkingSet",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        int vHigh = Mathf.Min(vLow + 1, database.Vertical.Length - 1);
        int hHigh = Mathf.Min(hLow + 1, database.Horizontal.Length - 1);
        var corners = new[] { (vLow, hLow), (vLow, hHigh), (vHigh, hLow), (vHigh, hHigh) };

        var present = Vector4.zero;
        for (int c = 0; c < Corners; c++)
        {
            var (cv, ch) = corners[c];
            present[c] = angles[0].Present[RainStreakDatabase.SliceIndex(cv, ch, 0)];
        }
        CornerPresent = present;

        for (int c = 0; c < Corners; c++)
        {
            var (vi, hi) = corners[c];
            for (int d = 0; d < angles.Length; d++)
            {
                var src = angles[d].Point[Level];
                for (int osc = 0; osc < Osc; osc++)
                {
                    int slice = RainStreakDatabase.SliceIndex(vi, hi, osc);

                    // A MISSING COMBINATION: not in the database (at extreme vertical angles the
                    // streak degenerates, `§5.4.5`). It is not copied; so the slice does not keep
                    // the previous frame's content, its weight goes to the shader as zero.
                    if (angles[d].Present[slice] == 0) continue;

                    Graphics.CopyTexture(src, slice, 0, 0, 0, src.width, src.height,
                                         point, (c * angles.Length + d) * Osc + osc, 0, 0, 0);
                }
            }
        }
    }

    /// The working set is the height of the longest array; the short `dcam`s sit at the top and
    /// leave the bottom empty. The shader must not sample that gap.
    void StoreHeightFractions()
    {
        var angles = database.Angles;
        float tallest = angles[0].Point[Level].height;

        DcamHeightFraction = new float[angles.Length];
        for (int d = 0; d < angles.Length; d++)
            DcamHeightFraction[d] = angles[d].Point[Level].height / tallest;
    }
}
