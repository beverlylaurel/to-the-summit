using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// IMPORT DIAGNOSTIC. Compares what the shader needs against what actually arrived
/// in the FBX, so a Blender-versus-Unity difference can be attributed instead of guessed.
///
/// CabinWeathered samples the tiling albedo with UV0 and both atlases with UV1, so a
/// missing or out-of-range UV1 turns the tint into a wrong lookup — usually black.
/// Face orientation is checked the same way: a mound whose normals point inward is
/// culled from outside and shows its interior instead.
public static class OutpostDiagnostics
{
    const string PrefabDirectory = "Assets/Prefabs/Outposts";
    const string TextureDirectory = "Assets/Textures/Outposts";

    [MenuItem("To The Summit/Outposts/Validate Assets")]
    public static void ValidateAssets()
    {
        var failures = new List<string>();
        var prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDirectory })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("Outpost_"))
            .OrderBy(path => path)
            .ToList();

        if (prefabPaths.Count != 8)
            failures.Add($"8 outpost prefab bekleniyordu, {prefabPaths.Count} bulundu.");

        foreach (string path in prefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                failures.Add($"Prefab yuklenemedi: {path}");
                continue;
            }

            var filters = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null).ToArray();
            var colliders = prefab.GetComponentsInChildren<MeshCollider>(true)
                .Where(collider => collider.sharedMesh != null).ToArray();
            if (filters.Length == 0)
                failures.Add($"Mesh bulunamadi: {path}");
            if (colliders.Length < filters.Length)
                failures.Add($"Collider eksik: {path} ({colliders.Length}/{filters.Length})");
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDirectory }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!name.EndsWith("_Tint") && !name.EndsWith("_RoughMetal"))
                continue;

            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.mipmapEnabled)
                failures.Add($"Atlas mipmap acik: {path}");
        }

        const string floorMaterialPath = "Assets/Materials/Cabin/weathered_planks.mat";
        var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(floorMaterialPath);
        if (floorMaterial == null || !floorMaterial.HasProperty("_RoughnessScale"))
            failures.Add($"Kabin zemin roughness ayari bulunamadi: {floorMaterialPath}");
        else if (floorMaterial.GetFloat("_RoughnessScale") < 1.15f)
            failures.Add($"Kabin zemini fazla parlak: roughness {floorMaterial.GetFloat("_RoughnessScale"):0.00}");

        if (failures.Count > 0)
            throw new System.InvalidOperationException("Outpost asset denetimi basarisiz:\n- " +
                                                       string.Join("\n- ", failures));

        Debug.Log($"Outpost asset denetimi gecti: {prefabPaths.Count} prefab collider'li, " +
                  "tint/roughness atlas mipmap'leri kapali ve kabin zemini dengeli.");
    }

    [MenuItem("To The Summit/Outposts/Diagnose Import")]
    public static void Diagnose()
    {
        var report = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/Outposts" })
                     .OrderBy(g => AssetDatabase.GUIDToAssetPath(g)))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) continue;

            report.AppendLine($"=== {System.IO.Path.GetFileName(path)} ===");
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                var mr = mf.GetComponent<MeshRenderer>();

                var uv0 = mesh.uv;
                var uv1 = mesh.uv2;
                report.AppendLine($"  {mf.name}: verts {mesh.vertexCount}, subs {mesh.subMeshCount}, " +
                                  $"uv0 {uv0.Length}, uv1 {uv1.Length}, norm {mesh.normals.Length}, " +
                                  $"tan {mesh.tangents.Length}");
                report.AppendLine($"    uv0 {Range(uv0)}");
                report.AppendLine($"    uv1 {Range(uv1)}");
                report.AppendLine($"    facing {Facing(mesh)}");
                if (mr != null)
                    report.AppendLine($"    mats {string.Join(", ", mr.sharedMaterials.Select(m => m == null ? "NULL" : m.name))}");
            }
        }
        Debug.Log(report.ToString());
        System.IO.File.WriteAllText(
            System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs", "OutpostDiag.txt"),
            report.ToString());
    }

    /// Renders every outpost from the same three-quarter angle the Blender previews
    /// use, on a neutral ground, so the two sheets can be laid side by side. A model
    /// judged against a different camera and a different sky is not judged at all.
    [MenuItem("To The Summit/Outposts/Capture Model Sheet")]
    public static void Sheet()
    {
        var paths = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/Outposts" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();

        var stage = new GameObject("ZZ_Sheet");
        var lightGo = new GameObject("ZZ_SheetLight", typeof(Light));
        var li = lightGo.GetComponent<Light>();
        li.type = LightType.Directional;
        li.intensity = 1.35f;
        li.color = new Color(1f, 0.97f, 0.93f);
        // Sun over the camera's shoulder. Pointed the other way it lit the far side
        // of every building and the sheet read as a material fault; round control
        // spheres kept a lit crown either way and hid the mistake.
        lightGo.transform.rotation = Quaternion.Euler(34f, 38f, 0f);
        lightGo.transform.SetParent(stage.transform);

        // CONTROL. Without ambient, one directional light leaves every shaded face
        // black and the sheet reports a material problem that is really a rig
        // problem. Blender's preview has a world; this gives Unity the same footing.
        var ambientMode = RenderSettings.ambientMode;
        var ambientLight = RenderSettings.ambientLight;
        float ambientIntensity = RenderSettings.ambientIntensity;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.50f);
        RenderSettings.ambientIntensity = 1f;
        DynamicGI.UpdateEnvironment();   // ambient probe is otherwise stale for cam.Render()

        var camGo = new GameObject("ZZ_SheetCam", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.78f, 0.80f, 0.83f);
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 200f;
        camGo.transform.SetParent(stage.transform);

        const int W = 520, H = 460;
        var tiles = new List<Texture2D>();
        foreach (string path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage.transform);
            go.transform.position = Vector3.zero;

            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

            float reach = b.size.magnitude;
            var dir = new Vector3(-0.62f, 0.46f, -0.64f).normalized;
            camGo.transform.position = b.center + dir * reach * 1.65f;
            camGo.transform.LookAt(b.center);

            tiles.Add(Shot(cam, W, H));
            Object.DestroyImmediate(go);
        }

        tiles.Add(ControlTile(cam, stage, W, H));

        const int columns = 4;
        int rows = Mathf.CeilToInt(tiles.Count / (float)columns);
        var grid = new Texture2D(W * columns, H * rows, TextureFormat.RGB24, false);
        var fill = Enumerable.Repeat(new Color(0.78f, 0.80f, 0.83f), W * H).ToArray();
        for (int i = 0; i < columns * rows; i++)
        {
            int col = i % columns, row = rows - 1 - i / columns;
            grid.SetPixels(col * W, row * H, W, H, i < tiles.Count ? tiles[i].GetPixels() : fill);
        }
        grid.Apply();
        System.IO.File.WriteAllBytes(
            System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs", "OutpostSheet.png"),
            grid.EncodeToPNG());

        foreach (var t in tiles) Object.DestroyImmediate(t);
        Object.DestroyImmediate(grid);
        Object.DestroyImmediate(stage);
        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambientLight;
        RenderSettings.ambientIntensity = ambientIntensity;
        Debug.Log($"Model sheet: {tiles.Count} outposts rendered to Logs/OutpostSheet.png " +
                  $"({string.Join(", ", paths.Select(System.IO.Path.GetFileNameWithoutExtension))}).");
    }

    /// EMPTY CONTROL. Three spheres of known albedo — 0.9, 0.5, 0.18 — under the same
    /// light as the models. Their rendered brightness says what the rig does to a
    /// surface whose colour is not in question, so a dark sheet can be blamed on the
    /// right thing. The measured values are logged, not eyeballed.
    static Texture2D ControlTile(Camera cam, GameObject stage, int w, int h)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        float[] steps = { 0.9f, 0.5f, 0.18f };
        var balls = new GameObject[steps.Length];
        for (int i = 0; i < steps.Length; i++)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.SetParent(stage.transform);
            ball.transform.position = new Vector3((i - 1) * 1.15f, 0f, 0f);
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(steps[i], steps[i], steps[i]));
            mat.SetFloat("_Smoothness", 0.15f);
            ball.GetComponent<Renderer>().sharedMaterial = mat;
            balls[i] = ball;
        }

        cam.transform.position = new Vector3(-0.62f, 0.46f, -0.64f).normalized * 4.4f;
        cam.transform.LookAt(Vector3.zero);
        var tex = Shot(cam, w, h);

        var log = new StringBuilder("Control spheres (albedo -> rendered sRGB at the lit crown): ");
        for (int i = 0; i < steps.Length; i++)
        {
            // Viewport coords, not screen: by now Shot() has cleared targetTexture and
            // cam.pixelWidth reports the editor window, not the image just rendered.
            // Sphere centre, not its crown: the centre is always inside the
            // silhouette, so the probe cannot slip off onto the background.
            Vector3 p = cam.WorldToViewportPoint(balls[i].transform.position);
            var c = tex.GetPixel(Mathf.RoundToInt(p.x * w), Mathf.RoundToInt(p.y * h));
            log.Append($"{steps[i]:0.00} -> {c.grayscale:0.000}   ");
            Object.DestroyImmediate(balls[i]);
        }
        Debug.Log(log.ToString());
        return tex;
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

    static string Range(Vector2[] uv)
    {
        if (uv == null || uv.Length == 0) return "MISSING";
        float x0 = uv.Min(v => v.x), x1 = uv.Max(v => v.x);
        float y0 = uv.Min(v => v.y), y1 = uv.Max(v => v.y);
        return $"x {x0:0.000}..{x1:0.000}  y {y0:0.000}..{y1:0.000}";
    }

    /// Fraction of triangles whose normal points away from the mesh centre. A closed,
    /// correctly wound shell sits near 1.0; a flipped one near 0.0.
    static string Facing(Mesh mesh)
    {
        var v = mesh.vertices;
        var t = mesh.triangles;
        if (t.Length == 0) return "no triangles";
        Vector3 c = Vector3.zero;
        for (int i = 0; i < v.Length; i++) c += v[i];
        c /= v.Length;

        int outward = 0, total = t.Length / 3;
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], d = v[t[i + 2]];
            Vector3 n = Vector3.Cross(b - a, d - a);
            if (Vector3.Dot(n, (a + b + d) / 3f - c) > 0f) outward++;
        }
        return $"{outward * 100f / total:0}% outward of {total} tris";
    }
}
