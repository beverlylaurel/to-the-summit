using UnityEditor;
using UnityEngine;

/// CONTOUR MAP. Every cell where grading touches the terrain is red; overlay route lines
/// are green. If both coincide, grading is at the correct location.
///
/// Why needed: claims of "grading done at wrong location" could not be resolved by numbers alone.
/// Cross-sections perpendicular to route are noisy due to foreland mounds, showing cut at one point
/// and fill at another. The map resolves the question at a glance.
///
/// To be deleted once foreland and road texturing are settled.
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
    static void Open() => GetWindow<RouteShapeMapWindow>("Contour Map").Show();

    void OnGUI()
    {
        if (mask == null) mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);

        var route = AssetDatabase.LoadAssetAtPath<MountainRoute>(
            "Assets/Settings/MountainRoute.asset");

        EditorGUILayout.HelpBox(
            "Red: locations where grading touches the terrain.\n" +
            "Colored lines: drawn route.\n\n" +
            "If both coincide, grading is in the correct location.",
            MessageType.None);

        if (mask == null)
        {
            EditorGUILayout.HelpBox("Mask missing. Generated when terrain is rebuilt.",
                MessageType.Warning);
            return;
        }

        // Square area: map is square, distorting aspect ratio gives false overlap.
        float side = Mathf.Min(position.width - 20f, position.height - 110f);
        var area = new Rect(10f, 100f, side, side);

        GUI.DrawTexture(area, mask, ScaleMode.StretchToFill);

        if (route == null) return;

        // Route is stored normalized, matching map aspect ratio directly.
        // Y INVERTED: texture bottom-row up, screen top-down.
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
