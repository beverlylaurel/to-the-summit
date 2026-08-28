using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// ROUTE BRUSH. Interactively paints bus road, spawn point, approach branches, camps, and shelters in Scene View.
public class RoutePainter : EditorWindow
{
    const string RoutePath = "Assets/Settings/MountainRoute.asset";

    float Spacing => Mathf.Max(2f, radius * 0.6f);

    enum Layer { Spawn, Road, Branch, Camp, Shop }
    enum Region { Start, Mountain }

    static readonly Layer[] StartLayers =
        { Layer.Spawn, Layer.Road, Layer.Branch, Layer.Camp, Layer.Shop };

    static readonly string[] StartLayerNames =
        { "Spawn", "Road", "Branch", "Camp", "Shop" };

    static readonly Color[] BranchColors =
    {
        new(1f, 0.55f, 0.1f),      // orange
        new(0.95f, 0.25f, 0.75f),  // magenta
        new(0.2f, 0.85f, 0.9f),    // cyan
        new(0.95f, 0.3f, 0.25f),   // red
        new(0.7f, 0.9f, 0.25f),    // lime
        new(0.75f, 0.6f, 1f),      // lilac
    };

    static readonly string[] BranchNames = { "Branch 1", "Branch 2", "Branch 3", "Main Branch" };

    static readonly Color SpawnColor = new(0.25f, 1f, 0.35f);
    static readonly Color RoadColor = new(0.9f, 0.88f, 0.82f);
    static readonly Color CampColor = new(0.35f, 0.55f, 1f);
    static readonly Color ShopColor = new(1f, 0.9f, 0.2f);

    MountainRoute route;
    Region region = Region.Start;
    Layer layer = Layer.Spawn;
    int branchIndex;
    float radius = 40f;
    bool painting;

    readonly Dictionary<List<MountainRoute.Mark>, Vector3[]> groundCache = new();

    Vector3 lastPaint;
    bool hasLastPaint;

    [MenuItem("To The Summit/Route Brush", false, 40)]
    static void Open() => GetWindow<RoutePainter>("Route Brush").Show();

    void OnEnable()
    {
        route = LoadOrCreate();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    static MountainRoute LoadOrCreate()
    {
        var asset = AssetDatabase.LoadAssetAtPath<MountainRoute>(RoutePath);
        if (asset != null)
        {
            EnsureBranches(asset);
            return asset;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(RoutePath));
        asset = CreateInstance<MountainRoute>();

        AssetDatabase.CreateAsset(asset, RoutePath);
        EnsureBranches(asset);
        return asset;
    }

    static void EnsureBranches(MountainRoute asset)
    {
        bool added = false;

        foreach (string name in BranchNames)
        {
            if (asset.branches.Exists(branch => branch.name == name)) continue;

            asset.branches.Add(new MountainRoute.Branch { name = name });
            added = true;
        }

        if (!added) return;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    void OnGUI()
    {
        if (route == null) route = LoadOrCreate();

        EditorGUILayout.HelpBox(
            "Left click: Mark point. Drag to paint continuous path.\n" +
            "Shift + click: Erase marks.\n" +
            "Ctrl + scroll wheel: Adjust brush radius.\n\n" +
            "Spawn: First click sets position, second click sets LOOK DIRECTION.",
            MessageType.None);

        bool next = GUILayout.Toggle(painting, "Brush Active", "Button", GUILayout.Height(28f));
        if (next != painting)
        {
            painting = next;
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space();
        region = (Region)GUILayout.SelectionGrid((int)region,
            new[] { "START", "MOUNTAIN" }, 2, GUILayout.Height(24f));

        if (region == Region.Mountain)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Mountain route is reserved for upper mountain climbing lines and danger zones.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.Space();
        int selected = Mathf.Max(0, System.Array.IndexOf(StartLayers, layer));
        layer = StartLayers[GUILayout.SelectionGrid(selected, StartLayerNames,
            StartLayerNames.Length)];

        if (layer == Layer.Branch)
        {
            var names = new string[route.branches.Count];
            for (int i = 0; i < names.Length; i++) names[i] = route.branches[i].name;

            branchIndex = GUILayout.SelectionGrid(Mathf.Clamp(branchIndex, 0, names.Length - 1),
                names, names.Length);

            var swatch = GUILayoutUtility.GetRect(0f, 6f);
            EditorGUI.DrawRect(swatch, BranchColors[branchIndex % BranchColors.Length]);
        }

        EditorGUILayout.Space();
        radius = EditorGUILayout.Slider("Radius (m)", radius, 5f, 300f);

        EditorGUILayout.Space();
        DrawStatus();

        EditorGUILayout.Space();
        if (GUILayout.Button("Spread Radii Realistically")) SpreadRadii();
        if (GUILayout.Button("Clear Selected Layer")) ClearLayer();
        if (GUILayout.Button("Inspect Raw Data Asset")) Selection.activeObject = route;
    }

    void DrawStatus()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) return;

        if (layer == Layer.Spawn)
        {
            EditorGUILayout.LabelField(route.spawnSet
                ? $"Spawn marked — Look yaw {route.spawnYaw:F0}°"
                : "Spawn not marked");
            return;
        }

        List<MountainRoute.Mark> marks = SelectedMarks();

        if (layer == Layer.Camp || layer == Layer.Shop)
        {
            string what = layer == Layer.Camp ? "camp" : "shop";
            if (marks.Count == 0)
            {
                EditorGUILayout.LabelField($"No {what}s marked yet");
                return;
            }

            float span = 0f;
            foreach (MountainRoute.Mark mark in marks) span += mark.radius * 2f;
            EditorGUILayout.LabelField($"{marks.Count} {what}(s)", $"Average {span / marks.Count:F0} m diameter");
            return;
        }

        if (marks.Count < 2)
        {
            EditorGUILayout.LabelField("Path not drawn yet");
            return;
        }

        float threshold = SteepThreshold();
        RouteProfile.Reading reading = RouteProfile.Measure(terrain, marks, threshold);

        float width = 0f;
        foreach (MountainRoute.Mark mark in marks) width += mark.radius * 2f;
        width /= marks.Count;

        EditorGUILayout.LabelField("Width", $"{width:F1} m");
        EditorGUILayout.LabelField("Length", $"{reading.length / 1000f:F2} km");
        EditorGUILayout.LabelField("Ascent", $"{reading.ascent:F0} m");

        float minutes = reading.length / 2.2f / 60f + reading.ascent / 600f;
        EditorGUILayout.LabelField("Est. Walk Time", $"~{minutes:F0} minutes");

        bool road = layer == Layer.Road;
        bool tooSteep = reading.maxGrade > threshold;

        string verdict = road
            ? (tooSteep ? "Too steep — Inaccessible by bus" : "Accessible by bus")
            : (tooSteep ? "Steep sections present — Requires pushing bike" : "Rideable throughout");

        EditorGUILayout.HelpBox(verdict, tooSteep ? MessageType.Warning : MessageType.Info);

        if (tooSteep)
            EditorGUILayout.LabelField("Steepest gradient",
                $"{reading.maxGrade * 100f:F0}% ({reading.steepLength:F0} m span)");
    }

    List<MountainRoute.Mark> SelectedMarks() => layer switch
    {
        Layer.Road => route.road,
        Layer.Branch => route.branches[Mathf.Clamp(branchIndex, 0, route.branches.Count - 1)].marks,
        Layer.Camp => route.camps,
        Layer.Shop => route.shops,
        _ => null
    };

    float SteepThreshold() =>
        layer == Layer.Road ? RouteProfile.RoadGrade : RouteProfile.BikeGrade;

    void SpreadRadii()
    {
        Undo.RecordObject(route, "Spread Radii");

        groundCache.Clear();

        Spread(route.road, 3.5f, 0.9f);

        for (int i = 0; i < route.branches.Count; i++)
        {
            if (route.branches[i].name.Contains("Main"))
            {
                Spread(route.branches[i].marks, 1.8f, 0.5f);
                continue;
            }

            float slot = ((i % 6) + Hash(i * 977 + 31) * 0.7f) / 5.7f;
            float own = Mathf.Lerp(0.9f, 1.9f, slot);
            Spread(route.branches[i].marks, own, own * 0.22f);
        }

        Spread(route.camps, 12f, 4f);
        Spread(route.shops, 7f, 2f);

        Flush();
    }

    static void Spread(List<MountainRoute.Mark> marks, float baseRadius, float variation)
    {
        const int Wavelength = 8;

        for (int i = 0; i < marks.Count; i++)
        {
            float wave = Mathf.PerlinNoise(i / (float)Wavelength, 0.5f) * 2f - 1f;
            float grain = (Hash(i) - 0.5f) * 0.35f;

            MountainRoute.Mark mark = marks[i];
            mark.radius = Mathf.Max(0.5f, baseRadius + (wave + grain) * variation);
            marks[i] = mark;
        }
    }

    static float Hash(int value)
    {
        uint h = (uint)value * 2654435761u;
        h ^= h >> 15;
        h *= 2246822519u;
        h ^= h >> 13;
        return (h & 0xffffffu) / 16777216f;
    }

    void ClearLayer()
    {
        Undo.RecordObject(route, "Clear Route Layer");

        switch (layer)
        {
            case Layer.Spawn: route.spawnSet = false; break;
            case Layer.Branch: route.branches[branchIndex].marks.Clear(); break;
            case Layer.Road: route.road.Clear(); break;
            case Layer.Camp: route.camps.Clear(); break;
            case Layer.Shop: route.shops.Clear(); break;
        }

        Flush();
    }

    void Save()
    {
        List<MountainRoute.Mark> edited = SelectedMarks();
        if (edited != null) groundCache.Remove(edited);

        Redraw();
    }

    void SaveAppended(List<MountainRoute.Mark> target, Vector3 world)
    {
        if (groundCache.TryGetValue(target, out Vector3[] cached))
        {
            if (cached.Length == target.Count - 1)
            {
                var grown = new Vector3[target.Count];
                cached.CopyTo(grown, 0);
                grown[target.Count - 1] = world;
                groundCache[target] = grown;
            }
            else groundCache.Remove(target);
        }

        Redraw();
    }

    void Redraw()
    {
        EditorUtility.SetDirty(route);
        SceneView.RepaintAll();
        Repaint();
    }

    void Flush()
    {
        Save();
        AssetDatabase.SaveAssetIfDirty(route);
    }

    void OnSceneGUI(SceneView view)
    {
        if (route == null) route = LoadOrCreate();

        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) return;

        DrawExisting(terrain);
        if (!painting || region != Region.Start) return;

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (!Probe(terrain, out Vector3 point)) return;

        DrawCursor(point);
        view.Repaint();

        HandleShortcuts();
        HandleClicks(terrain, point);
    }

    static bool Probe(Terrain terrain, out Vector3 point)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        var collider = terrain.GetComponent<TerrainCollider>();

        if (collider != null && collider.Raycast(ray, out RaycastHit hit, 100000f))
        {
            point = hit.point;
            return true;
        }

        point = default;
        return false;
    }

    void HandleShortcuts()
    {
        Event e = Event.current;
        if (e.type != EventType.ScrollWheel || !e.control) return;

        radius = Mathf.Clamp(radius * (e.delta.y > 0f ? 1f / 1.15f : 1.15f), 5f, 300f);

        e.Use();
        Repaint();
    }

    void HandleClicks(Terrain terrain, Vector3 point)
    {
        Event e = Event.current;
        if (e.button != 0) return;

        bool down = e.type == EventType.MouseDown;
        bool drag = e.type == EventType.MouseDrag;
        if (!down && !drag && e.type != EventType.MouseUp) return;

        if (e.type == EventType.MouseUp)
        {
            hasLastPaint = false;
            Flush();
            e.Use();
            return;
        }

        if (e.shift)
        {
            if (down) { Erase(terrain, point); Flush(); }
            e.Use();
            return;
        }

        if (layer == Layer.Spawn)
        {
            if (down) { PaintSpawn(terrain, point); Flush(); }
            e.Use();
            return;
        }

        bool line = layer == Layer.Road || layer == Layer.Branch;
        if (drag && !line) return;

        if (hasLastPaint && Vector3.Distance(point, lastPaint) < Spacing) return;

        Undo.RecordObject(route, "Paint Route Mark");
        var mark = new MountainRoute.Mark
        {
            position = MountainRoute.ToNormalized(point, terrain),
            radius = radius
        };

        List<MountainRoute.Mark> target = SelectedMarks();
        target.Add(mark);

        lastPaint = point;
        hasLastPaint = true;

        SaveAppended(target, new Vector3(point.x, point.y + 1f, point.z));
        e.Use();
    }

    void PaintSpawn(Terrain terrain, Vector3 point)
    {
        Undo.RecordObject(route, "Mark Spawn");

        if (!route.spawnSet)
        {
            route.spawn = MountainRoute.ToNormalized(point, terrain);
            route.spawnSet = true;
        }
        else
        {
            Vector3 from = MountainRoute.ToWorld(route.spawn, terrain);
            Vector2 direction = new Vector2(point.x - from.x, point.z - from.z);

            if (direction.sqrMagnitude > 1f)
                route.spawnYaw = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        Save();
    }

    void Erase(Terrain terrain, Vector3 point)
    {
        Undo.RecordObject(route, "Erase Route Mark");

        if (layer == Layer.Spawn)
        {
            route.spawnSet = false;
            Save();
            return;
        }

        var target = layer switch
        {
            Layer.Road => route.road,
            Layer.Branch => route.branches[branchIndex].marks,
            Layer.Camp => route.camps,
            _ => route.shops
        };

        for (int i = target.Count - 1; i >= 0; i--)
        {
            Vector3 world = MountainRoute.ToWorld(target[i].position, terrain);
            float dx = world.x - point.x, dz = world.z - point.z;
            if (dx * dx + dz * dz <= radius * radius) target.RemoveAt(i);
        }

        Save();
    }

    void DrawCursor(Vector3 point)
    {
        Handles.color = layer switch
        {
            Layer.Spawn => SpawnColor,
            Layer.Road => RoadColor,
            Layer.Branch => BranchColors[branchIndex % BranchColors.Length],
            Layer.Camp => CampColor,
            _ => ShopColor
        };

        Handles.DrawWireDisc(point + Vector3.up * 2f, Vector3.up, radius);
    }

    void DrawExisting(Terrain terrain)
    {
        DrawLine(terrain, route.road, RoadColor, RouteProfile.RoadGrade);

        for (int i = 0; i < route.branches.Count; i++)
            DrawBranch(terrain, route.branches[i], BranchColors[i % BranchColors.Length]);

        Handles.color = CampColor;
        foreach (MountainRoute.Mark camp in route.camps)
        {
            Vector3 world = Ground(terrain, camp.position);
            Handles.DrawWireDisc(world, Vector3.up, camp.radius);
            Handles.CubeHandleCap(0, world + Vector3.up * 8f, Quaternion.identity, 16f,
                EventType.Repaint);
        }

        Handles.color = ShopColor;
        foreach (MountainRoute.Mark shop in route.shops)
        {
            Vector3 world = Ground(terrain, shop.position);
            Handles.DrawWireDisc(world, Vector3.up, shop.radius);
            Handles.SphereHandleCap(0, world + Vector3.up * 8f, Quaternion.identity, 16f,
                EventType.Repaint);
        }

        if (!route.spawnSet) return;

        Vector3 spawn = Ground(terrain, route.spawn);
        Handles.color = SpawnColor;
        Handles.DrawWireDisc(spawn, Vector3.up, 12f);
        Handles.DrawWireDisc(spawn, Vector3.up, 4f);

        float yaw = route.spawnYaw * Mathf.Deg2Rad;
        var forward = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
        Handles.ArrowHandleCap(0, spawn + Vector3.up * 3f,
            Quaternion.LookRotation(forward), 90f, EventType.Repaint);
    }

    void DrawBranch(Terrain terrain, MountainRoute.Branch branch, Color color) =>
        DrawLine(terrain, branch.marks, color, RouteProfile.BikeGrade);

    void DrawLine(Terrain terrain, List<MountainRoute.Mark> marks, Color color,
        float steepThreshold)
    {
        if (marks.Count == 0) return;

        Vector3[] ground = GroundCached(terrain, marks);

        Handles.color = color;
        Handles.DrawAAPolyLine(4f, Smooth(ground));

        Handles.color = Color.red;
        for (int i = 1; i < ground.Length; i++)
        {
            float run = Vector2.Distance(new Vector2(ground[i - 1].x, ground[i - 1].z),
                                         new Vector2(ground[i].x, ground[i].z));
            if (run < 0.5f) continue;

            if (Mathf.Abs(ground[i].y - ground[i - 1].y) / run > steepThreshold)
                Handles.DrawAAPolyLine(7f, ground[i - 1], ground[i]);
        }

        DrawCorridor(marks, ground, color);
    }

    static void DrawCorridor(List<MountainRoute.Mark> marks, Vector3[] ground, Color color)
    {
        if (ground.Length < 2) return;

        var left = new Vector3[ground.Length];
        var right = new Vector3[ground.Length];

        for (int i = 0; i < ground.Length; i++)
        {
            Vector3 before = ground[Mathf.Max(i - 1, 0)];
            Vector3 after = ground[Mathf.Min(i + 1, ground.Length - 1)];

            var forward = new Vector2(after.x - before.x, after.z - before.z);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector2.right;
            forward.Normalize();

            var side = new Vector3(-forward.y, 0f, forward.x) * marks[i].radius;
            left[i] = ground[i] + side;
            right[i] = ground[i] - side;
        }

        Handles.color = new Color(color.r, color.g, color.b, 0.55f);
        Handles.DrawAAPolyLine(2f, Smooth(left));
        Handles.DrawAAPolyLine(2f, Smooth(right));
    }

    static Vector3[] Smooth(Vector3[] points)
    {
        const int Steps = 4;
        if (points.Length < 3) return points;

        var output = new Vector3[(points.Length - 1) * Steps + 1];
        int w = 0;

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)];
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p3 = points[Mathf.Min(i + 2, points.Length - 1)];

            for (int step = 0; step < Steps; step++)
            {
                float t = step / (float)Steps;
                float t2 = t * t, t3 = t2 * t;

                output[w++] = 0.5f * (2f * p1 + (-p0 + p2) * t
                    + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                    + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
            }
        }

        output[w] = points[points.Length - 1];
        return output;
    }

    Vector3[] GroundCached(Terrain terrain, List<MountainRoute.Mark> marks)
    {
        if (groundCache.TryGetValue(marks, out Vector3[] cached)
            && cached.Length == marks.Count)
            return cached;

        var ground = new Vector3[marks.Count];
        for (int i = 0; i < marks.Count; i++) ground[i] = Ground(terrain, marks[i].position);

        groundCache[marks] = ground;
        return ground;
    }

    static Vector3 Ground(Terrain terrain, Vector2 normalized)
    {
        Vector3 world = MountainRoute.ToWorld(normalized, terrain);
        world.y = terrain.SampleHeight(world) + terrain.transform.position.y + 1f;
        return world;
    }
}
