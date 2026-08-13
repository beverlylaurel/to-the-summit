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

    [MenuItem("To The Summit/Model/Bisiklet/Rig Kontrolü", false, 121)]
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
    /// - Jant çember değil: dönme merkezi doğru ama kenar oynuyor. Kaçıklık küçük, sapma
    ///   büyük. Çözüm `WheelRounding` — pivot oynatmak işe yaramaz.
    ///
    /// Ölçümün kendisi `WheelProfile`'da: düzeltme de aynı ölçüden besleniyor, iki ayrı
    /// hesap olsaydı biri düzeltip diğeri "hâlâ bozuk" derdi.
    static void MeasureWheel(string label, Transform wheel)
    {
        var filter = wheel.GetComponentInChildren<MeshFilter>();
        Mesh mesh = filter.sharedMesh;

        WheelProfile profile = WheelProfile.Measure(mesh, filter.transform, wheel.forward);

        Vector3 boxCentre = filter.transform.TransformPoint(mesh.bounds.center);
        Vector3 fitted = profile.Centre;

        Debug.Log($"[Tekerlek] {label}\n"
            + $"  yarıçap {profile.Radius:F3} m  (en dar {profile.Min:F3}, "
            + $"en geniş {profile.Max:F3})\n"
            + $"  yuvarlaklık sapması {(profile.Max - profile.Min) * 1000f:F0} mm, "
            + $"ortalamadan sapma {profile.Deviation * 1000f:F1} mm (rms)\n"
            + $"  3 mm'den fazla taşan açı dilimi: {profile.WideFraction * 100f:F0}% "
            + $"(küçükse çıkıntı, büyükse oval)\n"
            + $"  pivot kaçıklığı {Vector3.Distance(boxCentre, fitted) * 1000f:F0} mm "
            + $"(kutu merkezi {Format(boxCentre)} → uydurma {Format(fitted)})\n"
            + $"  genişlik {profile.Width * 1000f:F0} mm, "
            + $"eksende kayma {profile.AxisOffset * 1000f:F0} mm");
    }
}
