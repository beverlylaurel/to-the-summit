using UnityEngine;

/// MEASURES OUTER RIM RUNOUT. Generated wheel model is not a perfect circle;
/// runout amount is determined by measurement, and correction feeds from the same reading.
/// Measurement and correction share this unified source.
///
/// EXAMINES OUTER RIM, NOT HUB: spokes and hub sit near center; averaging them would yield
/// a meaningless radius. The FURTHEST vertex in each angular bin is sampled — corresponding to outer rim.
///
/// CENTER FROM CIRCLE FIT, NOT BOUNDING BOX: bounding box center only samples extremes,
/// while Kåsa circle fitting accounts for the entire perimeter to establish rotation center.
public class WheelProfile
{
    /// Angular bin resolution derives from VERTEX COUNT. A constant 720 bins was accurate on dense meshes,
    /// but decimated wheels retain only a few hundred vertices along the rim: most bins fell empty,
    /// gap interpolation generated noise, and correction imprinted that noise onto surface.
    ///
    /// Target is roughly three vertices per bin; roughly one tenth of rim vertices lie along perimeter.
    static int BinCount(int vertices) => Mathf.Clamp(vertices / 120, 48, 720);

    int bins;

    public Vector3 Centre { get; private set; }
    public Vector3 Axis { get; private set; }
    public Vector3 Right { get; private set; }
    public Vector3 Up { get; private set; }

    public float Radius { get; private set; }
    public float Min { get; private set; }
    public float Max { get; private set; }

    /// Root-mean-square deviation from mean radius. A single protrusion causes low increase,
    /// generalized ovality causes high increase.
    public float Deviation { get; private set; }

    /// Fraction of angular bins exceeding 3 mm deviation. Small indicates local bump, large indicates ovality.
    public float WideFraction { get; private set; }

    public float Width { get; private set; }
    public float AxisOffset { get; private set; }

    float[] radii;

    /// Measures along specified axis in WORLD SPACE (meters): part transforms have
    /// 100x scale; measuring in mesh space would scale numbers down by 1/100,
    /// hiding 2 mm runout below threshold.
    public static WheelProfile Measure(Mesh mesh, Transform space, Vector3 axis)
    {
        var profile = new WheelProfile { Axis = axis.normalized };
        profile.bins = BinCount(mesh.vertexCount);
        profile.radii = new float[profile.bins];

        Vector3 reference = Mathf.Abs(profile.Axis.y) > 0.9f ? Vector3.right : Vector3.up;
        profile.Right = Vector3.Normalize(Vector3.Cross(profile.Axis, reference));
        profile.Up = Vector3.Cross(profile.Right, profile.Axis);

        Matrix4x4 toWorld = space.localToWorldMatrix;
        Vector3 boxCentre = toWorld.MultiplyPoint3x4(mesh.bounds.center);

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);

        var far = new Vector2[profile.bins];
        var hit = new bool[profile.bins];

        float thickMin = float.MaxValue, thickMax = float.MinValue;

        foreach (Vector3 vertex in vertices)
        {
            Vector3 offset = vertex - boxCentre;

            float along = Vector3.Dot(offset, profile.Axis);
            thickMin = Mathf.Min(thickMin, along);
            thickMax = Mathf.Max(thickMax, along);

            var plane = new Vector2(Vector3.Dot(offset, profile.Right),
                                    Vector3.Dot(offset, profile.Up));

            int bin = profile.BinOf(Mathf.Atan2(plane.y, plane.x));

            if (!hit[bin] || plane.sqrMagnitude > far[bin].sqrMagnitude)
            {
                far[bin] = plane;
                hit[bin] = true;
            }
        }

        profile.Width = thickMax - thickMin;
        profile.AxisOffset = (thickMax + thickMin) * 0.5f;

        Vector2 fitted = profile.FitCircle(far, hit);
        profile.Centre = boxCentre + profile.Right * fitted.x + profile.Up * fitted.y;

        profile.Summarise(far, hit, fitted);
        return profile;
    }

    int BinOf(float angle) =>
        Mathf.Clamp((int)((angle + Mathf.PI) / (2f * Mathf.PI) * bins), 0, bins - 1);

    /// Kåsa circle fitting: least-squares center fitting rim perimeter points.
    Vector2 FitCircle(Vector2[] points, bool[] hit)
    {
        float sx = 0f, sy = 0f, sxx = 0f, syy = 0f, sxy = 0f, sxz = 0f, syz = 0f, sz = 0f;
        int count = 0;

        for (int i = 0; i < bins; i++)
        {
            if (!hit[i]) continue;

            float x = points[i].x, y = points[i].y, z = x * x + y * y;
            sx += x; sy += y; sz += z;
            sxx += x * x; syy += y * y; sxy += x * y;
            sxz += x * z; syz += y * z;
            count++;
        }

        float n = count;
        float a11 = 2f * (sxx - sx * sx / n), a12 = 2f * (sxy - sx * sy / n);
        float a22 = 2f * (syy - sy * sy / n);
        float b1 = sxz - sx * sz / n, b2 = syz - sy * sz / n;

        float determinant = a11 * a22 - a12 * a12;
        if (Mathf.Abs(determinant) < 1e-12f) return Vector2.zero;

        return new Vector2((b1 * a22 - b2 * a12) / determinant,
                           (a11 * b2 - a12 * b1) / determinant);
    }

    void Summarise(Vector2[] far, bool[] hit, Vector2 fitted)
    {
        Min = float.MaxValue;
        Max = float.MinValue;

        int count = 0;

        for (int i = 0; i < bins; i++)
        {
            radii[i] = hit[i] ? Vector2.Distance(far[i], fitted) : 0f;
            if (!hit[i]) continue;

            Radius += radii[i];
            Min = Mathf.Min(Min, radii[i]);
            Max = Mathf.Max(Max, radii[i]);
            count++;
        }

        Radius /= Mathf.Max(1, count);
        FillGaps(hit);
        Smooth();

        int wide = 0;

        for (int i = 0; i < bins; i++)
        {
            float deviation = radii[i] - Radius;
            Deviation += deviation * deviation / bins;
            if (deviation > 0.003f) wide++;
        }

        Deviation = Mathf.Sqrt(Deviation);
        WideFraction = wide / (float)bins;
    }

    /// Empty bins are interpolated from neighbors. An empty bin would mean zero radius,
    /// causing correction to pull vertices toward center at that angle.
    void FillGaps(bool[] hit)
    {
        for (int i = 0; i < bins; i++)
        {
            if (hit[i]) continue;

            int back = i, forward = i;
            while (!hit[(back + bins) % bins]) back--;
            while (!hit[forward % bins]) forward++;

            float span = forward - back;
            float t = span > 0f ? (i - back) / span : 0f;
            radii[i] = Mathf.Lerp(radii[(back + bins) % bins], radii[forward % bins], t);
        }
    }

    /// PROFILE SMOOTHING. Raw measured rim profile has vertex-to-vertex jitter;
    /// correcting against raw profile would imprint that jitter into the surface.
    /// Filter window covers 3% of perimeter — actual rim bulge spans 40 degrees,
    /// much wider than window and surviving smoothing intact.
    void Smooth()
    {
        int window = Mathf.Max(1, bins / 32);
        var source = (float[])radii.Clone();

        for (int i = 0; i < bins; i++)
        {
            float total = 0f;
            for (int k = -window; k <= window; k++)
                total += source[((i + k) % bins + bins) % bins];

            radii[i] = total / (window * 2 + 1);
        }
    }

    /// Measured outer radius at given angle. Linearly interpolated across bins to avoid stepping artifacts.
    public float RadiusAt(float angle)
    {
        float position = (angle + Mathf.PI) / (2f * Mathf.PI) * bins;
        int low = Mathf.FloorToInt(position);
        float t = position - low;

        return Mathf.Lerp(radii[((low % bins) + bins) % bins],
                          radii[((low + 1) % bins + bins) % bins], t);
    }
}
