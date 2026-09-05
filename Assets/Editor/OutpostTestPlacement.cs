using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// TEST PLACEMENT. Drops every building around the player's spawn so the whole set
/// can be walked in one session — silhouettes against the real sky, real fog, real
/// snow, at the distances the forest loop will actually use.
///
/// This is scaffolding, not level design. Everything lands under one root so a single
/// menu item removes it again; nothing is written to the scene file unless the scene
/// is saved deliberately.
///
/// Each building is grounded by raycasting the terrain collider, the same way
/// MountainSceneBootstrap grounds the player — models are authored with their floor
/// at y = 0, so the hit point is the transform position.
public static class OutpostTestPlacement
{
    const string RootName = "TestOutposts";
    const string PrefabDir = "Assets/Prefabs/Outposts";
    const string CabinDir = "Assets/Models/Cabin";
    const string RoutePath = "Assets/Settings/MountainRoute.asset";

    [MenuItem("To The Summit/Outposts/Place Around Spawn")]
    public static void Place()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null) { Debug.LogError("No Terrain in the scene."); return; }

        Vector3 spawn = FindSpawn(terrain);
        Remove();

        var models = LoadModels();
        if (models.Count == 0) { Debug.LogError($"No prefabs under {PrefabDir}."); return; }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Place outposts");
        root.transform.position = spawn;

        // Ring radius follows the set, not a guess: enough arc for the widest
        // building plus a walking gap between neighbours.
        float widest = 0f;
        foreach (var m in models) widest = Mathf.Max(widest, Footprint(m));
        float step = widest + 11f;
        float radius = Mathf.Max(34f, step * models.Count / (2f * Mathf.PI));

        int placed = 0;
        var report = new List<string>();
        for (int i = 0; i < models.Count; i++)
        {
            float a = (i / (float)models.Count) * Mathf.PI * 2f;
            var flat = spawn + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            if (!Ground(terrain, flat, out Vector3 pos)) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(models[i], root.transform);
            go.transform.position = pos;
            // Girisler spawn'a bakar: yapiyi ilk goren yuzu her zaman on cephesi olsun.
            var look = spawn - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            go.isStatic = true;
            placed++;
            report.Add($"{go.name} @ {pos.y:0.0} m");
        }

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"Placed {placed} buildings on a {radius:0} m ring around spawn " +
                  $"{spawn}.\n{string.Join("\n", report)}");
    }

    /// Two views answer two different questions, so both are rendered: the plan view
    /// shows spacing and overlap, the eye-level turn shows whether each building sits
    /// on the ground the way the player will see it.
    [MenuItem("To The Summit/Outposts/Capture Ring")]
    public static void Capture()
    {
        var root = GameObject.Find(RootName);
        if (root == null) { Debug.LogWarning("Place the ring first."); return; }
        var b = Bounds(root);
        Vector3 spawn = root.transform.position;

        var lightGo = new GameObject("ZZ_RingLight");
        var li = lightGo.AddComponent<Light>();
        li.type = LightType.Directional;
        li.intensity = 1.5f;
        li.color = new Color(1f, 0.96f, 0.9f);
        lightGo.transform.rotation = Quaternion.Euler(42f, 150f, 0f);

        var camGo = new GameObject("ZZ_RingCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.42f, 0.48f, 0.58f);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 900f;

        cam.orthographic = true;
        cam.orthographicSize = b.size.x * 0.56f;
        camGo.transform.position = new Vector3(b.center.x, b.max.y + 120f, b.center.z);
        camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Save(cam, 1100, 1100, "OutpostRing_Top.png");

        cam.orthographic = false;
        cam.fieldOfView = 60f;
        camGo.transform.position = spawn + Vector3.up * 1.7f;
        var tiles = new Texture2D[4];
        for (int i = 0; i < 4; i++)
        {
            camGo.transform.rotation = Quaternion.Euler(-4f, i * 90f, 0f);
            tiles[i] = Shot(cam, 900, 520);
        }
        SaveGrid(tiles, "OutpostRing_Eye.png");

        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        Debug.Log($"Ring captured. Extent {b.size.x:0.0} x {b.size.z:0.0} m, " +
                  $"height range {b.min.y:0.0}-{b.max.y:0.0} m.");
    }

    [MenuItem("To The Summit/Outposts/Remove Test Placement")]
    public static void Remove()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    static List<GameObject> LoadModels()
    {
        var list = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(g => g != null).ToList();

        // Ana siginak prefab degil, dogrudan model: karakollarla ayni cemberde
        // durmali ki olcegi ve tonu yan yana karsilastirilabilsin.
        var cabin = AssetDatabase.FindAssets("t:Model", new[] { CabinDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => p.EndsWith(".fbx"));
        if (cabin != null)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(cabin);
            if (go != null) list.Insert(0, go);
        }
        return list;
    }

    /// Same recipe the bootstrap uses for the player: ray from above the terrain's
    /// top down onto the collider. SampleHeight alone misses collider offsets.
    static bool Ground(Terrain terrain, Vector3 at, out Vector3 pos)
    {
        float top = terrain.transform.position.y + terrain.terrainData.size.y + 100f;
        var ray = new Ray(new Vector3(at.x, top, at.z), Vector3.down);
        var col = terrain.GetComponent<TerrainCollider>();
        if (col != null && col.Raycast(ray, out RaycastHit hit, top + 1000f))
        {
            pos = new Vector3(at.x, hit.point.y, at.z);
            return true;
        }
        pos = new Vector3(at.x, terrain.SampleHeight(at) + terrain.transform.position.y, at.z);
        return true;
    }

    /// The player object only exists in play mode, so the spawn is read from the
    /// same route asset the bootstrap spawns from — the ring must sit where the
    /// player actually lands, not where the editor camera happens to be.
    static Vector3 FindSpawn(Terrain terrain)
    {
        var route = AssetDatabase.LoadAssetAtPath<MountainRoute>(RoutePath);
        if (route != null && route.spawnSet)
        {
            Ground(terrain, MountainRoute.ToWorld(route.spawn, terrain), out var spawn);
            return spawn;
        }

        var cc = Object.FindAnyObjectByType<CharacterController>();
        if (cc != null) return cc.transform.position;

        Ground(terrain, terrain.transform.position + terrain.terrainData.size * 0.5f, out var mid);
        Debug.LogWarning("Route has no spawn set — ring centred on the terrain instead.");
        return mid;
    }

    static Texture2D Shot(Camera cam, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        return tex;
    }

    static void Save(Camera cam, int w, int h, string file)
    {
        var tex = Shot(cam, w, h);
        Write(tex.EncodeToPNG(), file);
        Object.DestroyImmediate(tex);
    }

    static void SaveGrid(Texture2D[] tiles, string file)
    {
        int w = tiles[0].width, h = tiles[0].height;
        var grid = new Texture2D(w * 2, h * 2, TextureFormat.RGB24, false);
        for (int i = 0; i < 4; i++)
        {
            // Row 0 of the sheet is the BOTTOM row in texture space, so the first
            // two headings land on top only if the pairs are written in this order.
            int col = i % 2, row = 1 - i / 2;
            grid.SetPixels(col * w, row * h, w, h, tiles[i].GetPixels());
            Object.DestroyImmediate(tiles[i]);
        }
        grid.Apply();
        Write(grid.EncodeToPNG(), file);
        Object.DestroyImmediate(grid);
    }

    static void Write(byte[] png, string file)
    {
        string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", file);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, png);
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static float Footprint(GameObject prefab)
    {
        var b = Bounds(prefab);
        return Mathf.Max(b.size.x, b.size.z);
    }
}
