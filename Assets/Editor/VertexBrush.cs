using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// PAINTS MATERIAL MASKS ONTO VERTEX COLORS. Generated models lack UVs and combine different materials
/// in single meshes; brush defines boundaries interactively.
///
/// MASK LIVES IN VERTICES, NOT TEXTURES. Models carry millions of vertices providing sub-centimeter
/// resolution without UV unwrapping. Shader maps vertex channels to materials (`BikeSurface`).
///
/// Painting performed in custom preview window for selected parts only.
public class VertexBrush : EditorWindow
{
    static readonly string[] SlotNames = { "Matte", "Semi-Matte", "Metallic" };

    static readonly string[] SlotHints =
    {
        "Rubber, cable housing, plastic, handlebar grips. Diffuse, non-reflective.",
        "Leather, fabric, painted surfaces. Soft specular without sharp reflections.",
        "Steel, chrome, aluminum, chain. Reflective.",
    };

    [SerializeField] MeshFilter target;
    [SerializeField] int channel;
    [SerializeField] Color colour = new Color(0.07f, 0.07f, 0.08f);
    [SerializeField] float radius = 0.03f;
    [SerializeField] float strength = 1f;
    [SerializeField] bool erase;

    Mesh mesh;
    Vector3[] vertices;
    Color32[] colours;
    Vector2[] surfaces;
    MeshCollider collider;

    Dictionary<Vector3Int, List<int>> grid;
    Vector3[] centres;
    int[] triangles;
    float cell;

    PreviewRenderUtility preview;

    Vector3 eye;
    float yaw = 30f;
    float pitch = 12f;

    readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
    bool flying;
    double lastFrame;

    bool painting;

    [MenuItem("To The Summit/Model/Bicycle/Material Brush", false, 124)]
    static void Open() => GetWindow<VertexBrush>("Material Brush").Show();

    void OnEnable()
    {
        if (target != null) EditorApplication.delayCall += Prepare;
    }

    void OnDisable()
    {
        Release();

        preview?.Cleanup();
        preview = null;
    }

    void OnGUI()
    {
        Toolbar();

        Rect view = GUILayoutUtility.GetRect(position.width, position.height - 152f);

        if (target == null || mesh == null)
        {
            EditorGUI.HelpBox(view,
                "Drag part to paint into slot above.\n\n"
                + "Left click: paint, Shift + click: erase.\n"
                + "Right click: rotate view, Middle click: pan, Scroll wheel: zoom.",
                MessageType.Info);
            return;
        }

        Viewport(view);
        Footer();
    }

    void Toolbar()
    {
        EditorGUILayout.Space(4f);

        MeshFilter picked = (MeshFilter)EditorGUILayout.ObjectField(
            "Part", target, typeof(MeshFilter), true);

        if (picked != target)
        {
            MeshFilter chosen = picked;

            EditorApplication.delayCall += () =>
            {
                Release();
                target = chosen;
                Prepare();
                Repaint();
            };
        }

        using (new EditorGUI.DisabledScope(target == null))
        {
            channel = EditorGUILayout.Popup("Material", channel, SlotNames);
            EditorGUILayout.LabelField(" ", SlotHints[channel], EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            radius = EditorGUILayout.Slider("Radius (m)", radius, 0.003f, 0.2f);
            erase = GUILayout.Toggle(erase, "Eraser", EditorStyles.miniButton, GUILayout.Width(52f));
            EditorGUILayout.EndHorizontal();

            strength = EditorGUILayout.Slider("Strength", strength, 0.05f, 1f);
        }

        colour = EditorGUILayout.ColorField("Color", colour);
    }

    void Footer()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField($"Vertices {vertices.Length:N0}   Painted {Painted():N0}",
            EditorStyles.miniLabel);

        if (GUILayout.Button("Clear Material", EditorStyles.miniButton, GUILayout.Width(110f)))
            Clear(channel);

        if (GUILayout.Button("Clear All", EditorStyles.miniButton, GUILayout.Width(110f)))
            Clear(-1);

        EditorGUILayout.EndHorizontal();
    }

    int Painted()
    {
        int count = 0;
        foreach (Color32 painted in colours)
            if (painted.a > 8) count++;

        return count;
    }

    void Viewport(Rect view)
    {
        preview ??= new PreviewRenderUtility();

        Input(view);

        Bounds bounds = target.GetComponent<Renderer>().bounds;
        float span = Mathf.Max(0.05f, bounds.size.magnitude);

        Fly(span);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        preview.BeginPreview(view, GUIStyle.none);

        preview.camera.transform.SetPositionAndRotation(eye, rotation);
        preview.camera.nearClipPlane = span * 0.002f;
        preview.camera.farClipPlane = span * 40f;
        preview.camera.fieldOfView = 35f;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f);

        preview.lights[0].intensity = 1.4f;
        preview.lights[0].transform.rotation = Quaternion.Euler(35f, yaw + 40f, 0f);
        preview.lights[1].intensity = 0.7f;
        preview.lights[1].transform.rotation = Quaternion.Euler(-20f, yaw + 200f, 0f);

        Material[] materials = target.GetComponent<Renderer>().sharedMaterials;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            Material material = materials.Length > 0
                ? materials[Mathf.Min(i, materials.Length - 1)] : null;

            preview.DrawMesh(mesh, target.transform.localToWorldMatrix, material, i);
        }

        preview.camera.Render();
        GUI.DrawTexture(view, preview.EndPreview(), ScaleMode.StretchToFill, false);

        Cursor(view);
    }

    void Fly(float span)
    {
        double now = EditorApplication.timeSinceStartup;
        float step = (float)(now - lastFrame);
        lastFrame = now;

        if (!flying || held.Count == 0) return;

        var move = Vector3.zero;
        if (held.Contains(KeyCode.W)) move.z += 1f;
        if (held.Contains(KeyCode.S)) move.z -= 1f;
        if (held.Contains(KeyCode.D)) move.x += 1f;
        if (held.Contains(KeyCode.A)) move.x -= 1f;
        if (held.Contains(KeyCode.E)) move.y += 1f;
        if (held.Contains(KeyCode.Q)) move.y -= 1f;

        if (move == Vector3.zero) return;

        float speed = span * (held.Contains(KeyCode.LeftShift) ? 2.4f : 0.8f);
        eye += Quaternion.Euler(pitch, yaw, 0f) * move.normalized * speed * Mathf.Min(step, 0.1f);

        Repaint();
    }

    void Cursor(Rect view)
    {
        if (Event.current.type != EventType.Repaint) return;
        if (!view.Contains(Event.current.mousePosition)) return;
        if (!Trace(view, Event.current.mousePosition, out RaycastHit hit)) return;

        Handles.SetCamera(preview.camera);
        Handles.color = erase || Event.current.shift
            ? new Color(1f, 0.35f, 0.2f) : new Color(0.2f, 0.9f, 1f);

        Handles.DrawWireDisc(hit.point, hit.normal, radius);
    }

    void Input(Rect view)
    {
        Event current = Event.current;
        if (!view.Contains(current.mousePosition)) return;

        float span = Mathf.Max(0.05f, target.GetComponent<Renderer>().bounds.size.magnitude);
        Quaternion look = Quaternion.Euler(pitch, yaw, 0f);

        if (current.type == EventType.ScrollWheel)
        {
            eye -= look * Vector3.forward * current.delta.y * span * 0.04f;
            current.Use();
            Repaint();
            return;
        }

        if (current.type == EventType.MouseDown && current.button == 1)
        {
            flying = true;
            lastFrame = EditorApplication.timeSinceStartup;
            current.Use();
            return;
        }

        if (current.type == EventType.MouseUp && current.button == 1)
        {
            flying = false;
            held.Clear();
            current.Use();
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 1)
        {
            yaw += current.delta.x * 0.25f;
            pitch = Mathf.Clamp(pitch + current.delta.y * 0.25f, -89f, 89f);
            current.Use();
            Repaint();
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 2)
        {
            eye -= look * new Vector3(current.delta.x, -current.delta.y, 0f) * span * 0.002f;
            current.Use();
            Repaint();
            return;
        }

        if (flying && (current.type == EventType.KeyDown || current.type == EventType.KeyUp))
        {
            if (current.type == EventType.KeyDown) held.Add(current.keyCode);
            else held.Remove(current.keyCode);

            current.Use();
            Repaint();
            return;
        }

        if (current.button != 0) return;

        if (current.type == EventType.MouseDown
            && Trace(view, current.mousePosition, out RaycastHit down))
        {
            Undo.RegisterCompleteObjectUndo(mesh, "Material brush");

            painting = true;
            Paint(down.point, current.shift);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && painting
                 && Trace(view, current.mousePosition, out RaycastHit drag))
        {
            Paint(drag.point, current.shift);
            current.Use();
        }
        else if (current.type == EventType.MouseUp && painting)
        {
            painting = false;
            Commit();
            current.Use();
        }
    }

    bool Trace(Rect view, Vector2 mouse, out RaycastHit hit)
    {
        hit = default;
        if (preview == null || collider == null) return false;

        Camera camera = preview.camera;

        float x = Mathf.InverseLerp(view.xMin, view.xMax, mouse.x) * 2f - 1f;
        float y = 1f - Mathf.InverseLerp(view.yMin, view.yMax, mouse.y) * 2f;

        float height = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * (view.width / Mathf.Max(1f, view.height));

        Vector3 direction = camera.transform.rotation
            * new Vector3(x * width, y * height, 1f).normalized;

        return collider.Raycast(new Ray(camera.transform.position, direction), out hit, 10000f);
    }

    void Prepare()
    {
        if (target == null) return;

        EnsureWritable();
        mesh = target.sharedMesh;
        vertices = mesh.vertices;

        triangles = mesh.triangles;

        colours = mesh.colors32;
        if (colours == null || colours.Length != vertices.Length)
            colours = new Color32[vertices.Length];

        surfaces = mesh.uv2;
        if (surfaces == null || surfaces.Length != vertices.Length)
            surfaces = new Vector2[vertices.Length];

        collider = target.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.hideFlags = HideFlags.HideAndDontSave;

        Bounds bounds = target.GetComponent<Renderer>().bounds;
        eye = bounds.center - Quaternion.Euler(pitch, yaw, 0f)
            * Vector3.forward * bounds.size.magnitude * 1.4f;

        BuildGrid();
    }

    void EnsureWritable()
    {
        Mesh source = target.sharedMesh;
        string path = AssetDatabase.GetAssetPath(source);

        if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) return;

        const string folder = "Assets/Models/Bike/Generated";
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Models/Bike", "Generated");

        string copyPath = $"{folder}/{target.name}_Paint.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(copyPath);

        if (existing == null)
        {
            existing = Unweld(source);

            AssetDatabase.CreateAsset(existing, copyPath);
            AssetDatabase.SaveAssets();

            ToolLog.Write($"[Brush] {target.name} paintable copy created: {copyPath}, vertices "
                + $"{source.vertexCount:N0} -> {existing.vertexCount:N0}");
        }

        target.sharedMesh = existing;
    }

    static Mesh Unweld(Mesh source)
    {
        Vector3[] positions = source.vertices;
        Vector3[] normals = source.normals;
        Vector2[] uv = source.uv;

        var mesh = new Mesh { name = source.name, indexFormat = IndexFormat.UInt32 };
        mesh.subMeshCount = source.subMeshCount;

        var newPositions = new List<Vector3>();
        var newNormals = new List<Vector3>();
        var newUv = new List<Vector2>();
        var newTriangles = new List<int>[source.subMeshCount];

        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            int[] indices = source.GetTriangles(sub);
            newTriangles[sub] = new List<int>(indices.Length);

            foreach (int index in indices)
            {
                newTriangles[sub].Add(newPositions.Count);
                newPositions.Add(positions[index]);

                if (normals.Length > 0) newNormals.Add(normals[index]);
                if (uv.Length > 0) newUv.Add(uv[index]);
            }
        }

        mesh.SetVertices(newPositions);
        if (newNormals.Count == newPositions.Count) mesh.SetNormals(newNormals);
        if (newUv.Count == newPositions.Count) mesh.SetUVs(0, newUv);

        for (int sub = 0; sub < source.subMeshCount; sub++)
            mesh.SetTriangles(newTriangles[sub], sub);

        mesh.SetColors(new Color32[newPositions.Count]);
        mesh.SetUVs(1, new Vector2[newPositions.Count]);
        mesh.RecalculateBounds();

        return mesh;
    }

    void Release()
    {
        if (collider != null) DestroyImmediate(collider);

        collider = null;
        mesh = null;
        vertices = null;
        colours = null;
        grid = null;
    }

    void BuildGrid()
    {
        float scale = Mathf.Max(1e-6f, target.transform.lossyScale.x);
        cell = Mathf.Max(1e-5f, radius / scale);

        int count = triangles.Length / 3;
        centres = new Vector3[count];
        grid = new Dictionary<Vector3Int, List<int>>(count / 4 + 1);

        for (int t = 0; t < count; t++)
        {
            centres[t] = (vertices[triangles[t * 3]]
                        + vertices[triangles[t * 3 + 1]]
                        + vertices[triangles[t * 3 + 2]]) / 3f;

            Vector3Int key = Cell(centres[t]);
            if (!grid.TryGetValue(key, out List<int> bucket))
                grid[key] = bucket = new List<int>();

            bucket.Add(t);
        }
    }

    Vector3Int Cell(Vector3 local) => new Vector3Int(
        Mathf.FloorToInt(local.x / cell),
        Mathf.FloorToInt(local.y / cell),
        Mathf.FloorToInt(local.z / cell));

    void Paint(Vector3 worldPoint, bool eraseNow)
    {
        Vector3 local = target.transform.InverseTransformPoint(worldPoint);

        float scale = Mathf.Max(1e-6f, target.transform.lossyScale.x);
        float localRadius = radius / scale;

        Vector3Int centre = Cell(local);
        int reach = Mathf.CeilToInt(localRadius / cell);
        bool removing = eraseNow || erase;

        var paint = new Color32(
            (byte)Mathf.RoundToInt(colour.r * 255f),
            (byte)Mathf.RoundToInt(colour.g * 255f),
            (byte)Mathf.RoundToInt(colour.b * 255f),
            (byte)Mathf.RoundToInt(255f * strength));

        var slot = new Vector2(channel, 0f);

        for (int x = -reach; x <= reach; x++)
        for (int y = -reach; y <= reach; y++)
        for (int z = -reach; z <= reach; z++)
        {
            var key = new Vector3Int(centre.x + x, centre.y + y, centre.z + z);
            if (!grid.TryGetValue(key, out List<int> bucket)) continue;

            foreach (int triangle in bucket)
            {
                if (Vector3.Distance(centres[triangle], local) > localRadius) continue;

                for (int corner = 0; corner < 3; corner++)
                {
                    int index = triangles[triangle * 3 + corner];

                    if (removing)
                    {
                        Color32 cleared = colours[index];
                        cleared.a = 0;
                        colours[index] = cleared;
                        continue;
                    }

                    colours[index] = paint;
                    surfaces[index] = slot;
                }
            }
        }

        mesh.SetColors(colours);
        mesh.SetUVs(1, surfaces);
        Repaint();
    }

    void Clear(int slot)
    {
        for (int i = 0; i < colours.Length; i++)
        {
            if (slot >= 0 && Mathf.RoundToInt(surfaces[i].x) != slot) continue;

            Color32 current = colours[i];
            current.a = 0;
            colours[i] = current;
        }

        mesh.SetColors(colours);
        Commit();
    }

    void Commit()
    {
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
    }
}
