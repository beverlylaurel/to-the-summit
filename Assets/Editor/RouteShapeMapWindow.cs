using UnityEditor;
using UnityEngine;

/// TESVİYE HARİTASI. Şekillendirmenin araziye dokunduğu her hücre kırmızı; üstüne
/// çizilen rota hatları yeşil. İkisi çakışıyorsa tesviye doğru yerde demektir.
///
/// Neden gerekti: "tesviye yanlış yerde yapılmış" iddiası sayılarla çözülemedi. Rotaya
/// dik kesitler ovanın kendi tepecikleri yüzünden gürültülü çıkıyor ve bir noktada
/// yarma, ötekinde dolgu ölçülüyor. Harita soruyu tek bakışta kapatıyor.
///
/// Ova ve yol dokusu oturunca silinir.
public class RouteShapeMapWindow : EditorWindow
{
    const string MaskPath = "Assets/Terrain/RouteShapeMask.png";

    static readonly Color[] BranchColors =
    {
        new(0.2f, 1f, 0.3f),
        new(0.4f, 1f, 0.9f),
        new(1f, 1f, 0.3f),
        new(1f, 0.6f, 0.9f),
    };

    Texture2D mask;

    [MenuItem("To The Summit/Terrain/Contour Map", false, 27)]
    static void Open() => GetWindow<RouteShapeMapWindow>("Tesviye Haritası").Show();

    void OnGUI()
    {
        if (mask == null) mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);

        var route = AssetDatabase.LoadAssetAtPath<MountainRoute>(
            "Assets/Settings/MountainRoute.asset");

        EditorGUILayout.HelpBox(
            "Kırmızı: tesviyenin araziye dokunduğu yerler.\n" +
            "Renkli çizgiler: çizilmiş rota.\n\n" +
            "İkisi çakışıyorsa şekillendirme doğru yerde.",
            MessageType.None);

        if (mask == null)
        {
            EditorGUILayout.HelpBox("Maske yok. Arazi yeniden üretilince oluşuyor.",
                MessageType.Warning);
            return;
        }

        // Kare alan: harita kare, oranı bozmak çakışmayı yalancı gösterir.
        float side = Mathf.Min(position.width - 20f, position.height - 110f);
        var area = new Rect(10f, 100f, side, side);

        GUI.DrawTexture(area, mask, ScaleMode.StretchToFill);

        if (route == null) return;

        // Rota normalize saklandığı için doğrudan haritanın oranına düşüyor.
        // Y TERS: doku alt satırdan yukarı, ekran üstten aşağı.
        DrawLine(area, route.road, Color.white);

        for (int i = 0; i < route.branches.Count; i++)
            DrawLine(area, route.branches[i].marks,
                     BranchColors[i % BranchColors.Length]);

        if (!route.spawnSet) return;

        Handles.color = Color.magenta;
        Vector2 spawn = ToScreen(area, route.spawn);
        Handles.DrawSolidDisc(spawn, Vector3.forward, 4f);
    }

    static void DrawLine(Rect area, System.Collections.Generic.List<MountainRoute.Mark> marks,
        Color color)
    {
        if (marks.Count < 2) return;

        var points = new Vector3[marks.Count];
        for (int i = 0; i < marks.Count; i++) points[i] = ToScreen(area, marks[i].position);

        Handles.color = color;
        Handles.DrawAAPolyLine(2f, points);
    }

    static Vector2 ToScreen(Rect area, Vector2 normalized) =>
        new(area.x + normalized.x * area.width,
            area.y + (1f - normalized.y) * area.height);
}
