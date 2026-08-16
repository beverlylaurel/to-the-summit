using UnityEngine;

/// TEKERLEĞİN DIŞ KENARINI ÖLÇER. Üretilen modelde jant tam çember değil; ne kadar
/// kaçtığı ancak ölçülerek bilinir ve düzeltme de aynı ölçüden beslenir. Ölçüm ile
/// düzeltme aynı kaynaktan çıksın diye tek yerde duruyor.
///
/// GÖBEĞE DEĞİL KENARA BAKIYOR: teller ve göbek merkeze yakın duruyor, ortalamaya
/// karışsalardı "yarıçap" diye anlamsız bir sayı çıkardı. Her açı diliminin EN UZAK
/// köşesi alınıyor — o da jantın dış kenarı.
///
/// MERKEZ KUTUDAN DEĞİL ÇEMBERDEN: sınır kutusunun ortası yalnız en uçlara bakıyor,
/// çember uydurma (Kåsa) bütün kenarı hesaba katıyor. Dönme merkezi bu.
public class WheelProfile
{
    /// Açı çözünürlüğü KÖŞE SAYISINDAN türüyor. Sabit 720 dilim yoğun mesh'te doğruydu
    /// ama seyreltilmiş tekerlekte jant çevresinde birkaç yüz köşe kalıyor: dilimlerin
    /// çoğu boş düşüyor, uzun boşluklardan doldurulan profil gürültüye dönüyor ve
    /// düzeltme o gürültüyü yüzeye basıyordu (sapma 4.6 mm'den 8.1 mm'ye çıktı).
    ///
    /// Dilim başına ortalama üç köşe hedefleniyor; jant köşelerinin kabaca onda biri
    /// çevrede duruyor.
    static int BinCount(int vertices) => Mathf.Clamp(vertices / 120, 48, 720);

    int bins;

    public Vector3 Centre { get; private set; }
    public Vector3 Axis { get; private set; }
    public Vector3 Right { get; private set; }
    public Vector3 Up { get; private set; }

    public float Radius { get; private set; }
    public float Min { get; private set; }
    public float Max { get; private set; }

    /// Ortalamadan sapmanın karekök ortalaması. Tek bir çıkıntı bunu az yükseltir,
    /// yaygın ovallik çok.
    public float Deviation { get; private set; }

    /// Üç milimetreden fazla taşan açı dilimlerinin oranı. Küçükse çıkıntı, büyükse oval.
    public float WideFraction { get; private set; }

    public float Width { get; private set; }
    public float AxisOffset { get; private set; }

    float[] radii;

    /// Verilen eksende ölçer. Hesap DÜNYA UZAYINDA, yani metrede: parça dönüşümlerinde
    /// yüz kat ölçek var ve mesh'in kendi uzayında ölçülseydi bütün sayılar yüzde bire
    /// inerdi — iki milimetrelik salgı yirmi mikron görünüp sınırın altında kalıyordu.
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

    /// Kåsa çember uydurma: kenar noktalarına en küçük kareler anlamında oturan merkez.
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

    /// Boş kalan dilimler komşularından dolduruluyor. Boş dilim sıfır yarıçap demek
    /// olurdu ve düzeltme o açıda köşeleri merkeze çekerdi.
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

    /// PROFİL YUMUŞATILIYOR. Ölçülen dış kenar köşe köşe zıplıyor; düzeltme ham profile
    /// göre yapılsaydı o zıplama yüzeye kalıcı olarak yazılırdı. Pencere çevrenin yüzde
    /// üçü — jantın gerçek şişkinliği kırk derecelik bir yay, yani bu pencereden çok daha
    /// geniş ve yumuşatmadan sağ çıkıyor.
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

    /// Verilen açıdaki ölçülen dış yarıçap. Dilimler arasında doğrusal geçiliyor:
    /// basamaklı okunsaydı düzeltme jantta yarım derecelik basamaklar bırakırdı.
    public float RadiusAt(float angle)
    {
        float position = (angle + Mathf.PI) / (2f * Mathf.PI) * bins;
        int low = Mathf.FloorToInt(position);
        float t = position - low;

        return Mathf.Lerp(radii[((low % bins) + bins) % bins],
                          radii[((low + 1) % bins + bins) % bins], t);
    }
}
