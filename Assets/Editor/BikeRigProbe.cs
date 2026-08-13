using UnityEditor;
using UnityEngine;

/// DÖNEN PARÇALARI DOĞRULAR. Kurulum betiği ön takımı bir eşiğe göre ayırıyor: eşiğin
/// önündeki her parça direksiyonla dönüyor. Eşiğin doğru olup olmadığı ancak parçalar
/// GERÇEKTEN döndürülünce görülüyor — kadro kımıldıyorsa eşik geride, gidon yerinde
/// kalıyorsa ileride.
///
/// Play'e basmadan çalışıyor: fizik, girdi ve hız devreye girmeden yalnız hiyerarşi
/// sınanıyor. Play'de bakılsaydı yanlış dönen parça ile yanlış hesaplanan açı birbirine
/// karışırdı.
///
/// GEÇİCİ ARAÇ. Hiyerarşi oturunca silinecek (bkz. `DECISIONS.md`).
public class BikeRigProbe : EditorWindow
{
    Transform steering;
    Transform frontWheel;
    Transform rearWheel;

    Quaternion steeringRest = Quaternion.identity;
    Quaternion wheelRest = Quaternion.identity;

    float steer;
    float spin;

    [MenuItem("To The Summit/Model/Bisiklet Rig Kontrolü", false, 122)]
    static void Open() => GetWindow<BikeRigProbe>("Bisiklet Rig").Show();

    void OnEnable() => Bind();

    void Bind()
    {
        var bike = Object.FindAnyObjectByType<BikeController>();
        if (bike == null)
        {
            steering = frontWheel = rearWheel = null;
            return;
        }

        steering = Find(bike.transform, "Steering");
        frontWheel = Find(bike.transform, "FrontWheel");
        rearWheel = Find(bike.transform, "RearWheel");

        if (steering != null) steeringRest = steering.localRotation;
        if (frontWheel != null) wheelRest = frontWheel.localRotation;

        steer = 0f;
        spin = 0f;
    }

    static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
            if (child.name == name) return child;

        return null;
    }

    void OnGUI()
    {
        if (GUILayout.Button("Sahnedeki bisikleti bul")) Bind();

        if (steering == null || frontWheel == null || rearWheel == null)
        {
            EditorGUILayout.HelpBox(
                "Sahnede kurulu bisiklet yok. Önce To The Summit → Model → "
                + "Bisikleti Sahneye Kur.", MessageType.Info);
            return;
        }

        // Play'de bileşenler her karede dönüşü kendileri kuruyor; kaydırıcının yazdığı
        // değer bir sonraki `LateUpdate`'te siliniyor.
        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Play açıkken kaydırıcı işe yaramaz: tekerlek ve gidon bileşenleri her "
                + "karede dönüşün üstüne yazıyor. Play'den çık.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "Gidonu çevir: yalnız ön takım dönmeli — çatal, gidon, fren kolları, ön "
            + "çamurluk ve ön tekerlek. Kadro, sele, pedal ve arka tekerlek KIMILDAMAMALI.\n\n"
            + "Tekerleği döndür: iki tekerlek de kendi göbeğinde dönmeli. Göbeğin dışında "
            + "bir yerde dönüyorsa pivot yanlış yerde.",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        steer = EditorGUILayout.Slider("Gidon açısı (°)", steer, -40f, 40f);
        spin = EditorGUILayout.Slider("Tekerlek açısı (°)", spin, 0f, 360f);

        if (EditorGUI.EndChangeCheck()) Apply();

        EditorGUILayout.Space();

        if (GUILayout.Button("Sıfırla", GUILayout.Height(24f)))
        {
            steer = 0f;
            spin = 0f;
            Apply();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Tekerlekleri ölç", GUILayout.Height(24f)))
        {
            MeasureWheel("Ön", frontWheel);
            MeasureWheel("Arka", rearWheel);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Direksiyon ekseni",
            $"{steering.up.x:F2}, {steering.up.y:F2}, {steering.up.z:F2} "
            + $"({Vector3.Angle(Vector3.up, steering.up):F0}° eğik)");
        EditorGUILayout.LabelField("Ön göbek", Format(frontWheel.position));
        EditorGUILayout.LabelField("Arka göbek", Format(rearWheel.position));
        EditorGUILayout.LabelField("Dingil mesafesi",
            $"{Vector3.Distance(frontWheel.position, rearWheel.position):F2} m");
    }

    void Apply()
    {
        steering.localRotation = steeringRest * Quaternion.Euler(0f, steer, 0f);

        Quaternion wheel = wheelRest * Quaternion.Euler(0f, 0f, spin);
        frontWheel.localRotation = wheel;
        rearWheel.localRotation = wheel;

        SceneView.RepaintAll();
    }

    static string Format(Vector3 point) => $"{point.x:F2}, {point.y:F2}, {point.z:F2}";

    /// SALGININ SEBEBİNİ AYIRIR. Dönen tekerlek aşağı yukarı oynuyorsa iki ayrı sebep
    /// olabilir ve ikisinin çözümü farklı:
    ///
    /// - Pivot yanlış yerde: mesh yuvarlak ama dönme merkezi kenara kaçmış. Ölçüde
    ///   "kaçıklık" büyük, "yuvarlaklık sapması" küçük çıkar. Çözüm pivotu taşımak.
    /// - Tekerlek oval: dönme merkezi doğru ama jant çember değil. Kaçıklık küçük,
    ///   sapma büyük. Çözüm modeli düzeltmek — pivot oynatmak işe yaramaz.
    ///
    /// Ölçüm janta bakıyor, bütün mesh'e değil: göbek ve teller merkeze yakın duruyor ve
    /// ortalamaya karışsalardı yarıçap diye anlamsız bir sayı çıkardı. Her açı diliminin
    /// EN UZAK noktası alınıyor, o da jantın dış kenarı.
    static void MeasureWheel(string label, Transform wheel)
    {
        var filter = wheel.GetComponentInChildren<MeshFilter>();
        Vector3[] vertices = filter.sharedMesh.vertices;

        Vector3 axis = wheel.forward;
        Vector3 boxCentre = filter.GetComponent<Renderer>().bounds.center;

        // Düzlem içi iki dik yön: yarıçap ve açı bunlarla okunuyor.
        Vector3 right = Vector3.Normalize(Vector3.Cross(axis, Vector3.up));
        Vector3 up = Vector3.Cross(right, axis);

        const int Bins = 720;
        var far = new Vector2[Bins];
        var hit = new bool[Bins];

        float thickMin = float.MaxValue, thickMax = float.MinValue;

        foreach (Vector3 local in vertices)
        {
            Vector3 offset = filter.transform.TransformPoint(local) - boxCentre;

            float along = Vector3.Dot(offset, axis);
            thickMin = Mathf.Min(thickMin, along);
            thickMax = Mathf.Max(thickMax, along);

            var plane = new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, up));
            int bin = Mathf.Clamp((int)((Mathf.Atan2(plane.y, plane.x) + Mathf.PI)
                                        / (2f * Mathf.PI) * Bins), 0, Bins - 1);

            if (!hit[bin] || plane.sqrMagnitude > far[bin].sqrMagnitude)
            {
                far[bin] = plane;
                hit[bin] = true;
            }
        }

        // Çember uydurma (Kåsa): dış kenar noktalarına en iyi oturan merkez ve yarıçap.
        // Kutu merkezi yalnız sınırlara bakıyor, bu ise bütün kenarı hesaba katıyor.
        float sx = 0f, sy = 0f, sxx = 0f, syy = 0f, sxy = 0f, sxz = 0f, syz = 0f, sz = 0f;
        int count = 0;

        for (int i = 0; i < Bins; i++)
        {
            if (!hit[i]) continue;

            float x = far[i].x, y = far[i].y, z = x * x + y * y;
            sx += x; sy += y; sz += z;
            sxx += x * x; syy += y * y; sxy += x * y;
            sxz += x * z; syz += y * z;
            count++;
        }

        float n = count;
        float a11 = 2f * (sxx - sx * sx / n), a12 = 2f * (sxy - sx * sy / n);
        float a22 = 2f * (syy - sy * sy / n);
        float b1 = sxz - sx * sz / n, b2 = syz - sy * sz / n;

        float det = a11 * a22 - a12 * a12;
        float cx = (b1 * a22 - b2 * a12) / det;
        float cy = (a11 * b2 - a12 * b1) / det;

        float mean = 0f, min = float.MaxValue, max = float.MinValue;

        for (int i = 0; i < Bins; i++)
        {
            if (!hit[i]) continue;

            float r = Vector2.Distance(far[i], new Vector2(cx, cy));
            mean += r / n;
            min = Mathf.Min(min, r);
            max = Mathf.Max(max, r);
        }

        Vector3 fitted = boxCentre + right * cx + up * cy;

        Debug.Log($"[Tekerlek] {label}\n"
            + $"  yarıçap {mean:F3} m  (en dar {min:F3}, en geniş {max:F3})\n"
            + $"  yuvarlaklık sapması {(max - min) * 1000f:F0} mm — oval mı\n"
            + $"  pivot kaçıklığı {new Vector2(cx, cy).magnitude * 1000f:F0} mm "
            + $"(kutu merkezi {Format(boxCentre)} → uydurma {Format(fitted)})\n"
            + $"  genişlik {(thickMax - thickMin) * 1000f:F0} mm, "
            + $"eksende kayma {((thickMax + thickMin) * 0.5f) * 1000f:F0} mm");
    }
}
