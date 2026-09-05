using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// FOREST OUTPOST IMPORT. Ten loot buildings authored in Blender arrive as FBX plus,
/// per building, one tint and one roughness/metallic atlas. The surface itself is not
/// baked: each material tiles its own texture through `Cabin/WeatheredLit` and the
/// atlas only carries the slow weathering multiplier.
///
/// WHY NOT A FULLY BAKED ALBEDO. These buildings are assembled from hundreds of small
/// slabs — the trapper roof alone is 240 separate shingles. Unwrapping that into one
/// atlas leaves roughly 30x8 pixels per island; the packer's margin then exceeds the
/// islands themselves and filtering pulls the empty background in, so the roof reads
/// blotched. The tint atlas tolerates the same poor packing because it carries a
/// multiplier whose neighbouring values are visually indistinguishable.
///
/// Everything here is derived from OutpostManifest.json, written by the Blender export.
/// Nothing is typed twice: tiling scale, detile offset and texture names all come from
/// the material that actually produced the bake.
public static class OutpostImportSetup
{
    const string Manifest = "Assets/Editor/OutpostManifest.json";
    const string ModelDir = "Assets/Models/Outposts";
    const string TexDir = "Assets/Textures/Outposts";
    const string TilingDir = TexDir + "/Tiling";
    const string MatDir = "Assets/Materials/Outposts";
    const string PrefabDir = "Assets/Prefabs/Outposts";

    [Serializable] class MatEntry
    {
        public string mat; public bool proc; public float tiling;
        public float detX; public float detY; public string baseTex; public string nrmTex;
    }
    [Serializable] class Item { public string name; public int atlas; public MatEntry[] mats; }
    [Serializable] class Book { public Item[] items; }

    [MenuItem("To The Summit/Outposts/Import Setup")]
    public static void Run()
    {
        var book = JsonUtility.FromJson<Book>(File.ReadAllText(Manifest));
        if (book?.items == null || book.items.Length == 0)
        {
            Debug.LogError($"Outpost manifest is empty or unreadable: {Manifest}");
            return;
        }

        EnsureDir(MatDir);
        EnsureDir(PrefabDir);

        try
        {
            AssetDatabase.StartAssetEditing();
            ConfigureTextures();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();

        var shader = Shader.Find("Cabin/WeatheredLit");
        if (shader == null) { Debug.LogError("Shader 'Cabin/WeatheredLit' not found."); return; }
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");

        int madeMat = 0, madePrefab = 0;
        foreach (var item in book.items)
        {
            var tint = Load<Texture2D>($"{TexDir}/{item.name}_Tint.png");
            var rm = Load<Texture2D>($"{TexDir}/{item.name}_RoughMetal.png");
            var remap = new Dictionary<string, Material>();

            foreach (var m in item.mats)
            {
                string path = $"{MatDir}/{m.mat}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(m.proc ? urpLit : shader);
                    AssetDatabase.CreateAsset(mat, path);
                    madeMat++;
                }
                mat.shader = m.proc ? urpLit : shader;

                if (m.proc)
                {
                    // Glass is the one surface with no tiling texture: it is transparent,
                    // so a weathering multiplier would have nothing to multiply.
                    SetTransparent(mat);
                    mat.SetColor("_BaseColor", new Color(0.82f, 0.86f, 0.87f, 0.30f));
                    mat.SetFloat("_Smoothness", 0.94f);
                    mat.SetFloat("_Metallic", 0f);
                }
                else
                {
                    mat.SetTexture("_BaseMap", Load<Texture2D>($"{TilingDir}/{m.baseTex}"));
                    if (!string.IsNullOrEmpty(m.nrmTex))
                        mat.SetTexture("_BumpMap", Load<Texture2D>($"{TilingDir}/{m.nrmTex}"));
                    mat.SetTexture("_TintMap", tint);
                    mat.SetTexture("_RoughMetalMap", rm);
                    mat.SetTextureScale("_BaseMap", new Vector2(m.tiling, m.tiling));
                    mat.SetVector("_DetileOffset", new Vector4(m.detX, m.detY, 0f, 0f));
                    mat.SetFloat("_BumpScale", 1f);
                    mat.SetFloat("_RoughnessScale", 1f);
                }
                EditorUtility.SetDirty(mat);
                remap[m.mat] = mat;
            }

            string fbx = $"{ModelDir}/{item.name}.fbx";
            if (!ApplyRemap(fbx, remap)) continue;
            if (BuildPrefab(fbx, item.name)) madePrefab++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Outposts: {book.items.Length} buildings, {madeMat} new materials, " +
                  $"{madePrefab} prefabs written to {PrefabDir}.");
    }

    /// Lays the ten prefabs out in a row so the import can be judged in one look,
    /// spaced by each building's own bounds rather than a fixed step. Remove with
    /// the sibling menu item; nothing here is meant to stay in the scene.
    [MenuItem("To The Summit/Outposts/Spawn Check Row")]
    public static void SpawnRow()
    {
        ClearRow();
        var root = new GameObject("ZZ_OutpostCheck");
        Undo.RegisterCreatedObjectUndo(root, "Outpost check row");
        float x = 0f;
        // Ana siginak da satira girer: atlaslari yeniden pisirildi, karakollarla
        // AYNI isikta yan yana gorulmeden "bozulmadi" denemez.
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir })
                                 .Select(AssetDatabase.GUIDToAssetPath).ToList();
        string cabin = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models/Cabin" })
                                    .Select(AssetDatabase.GUIDToAssetPath)
                                    .FirstOrDefault(p => p.EndsWith(".fbx"));
        if (cabin != null) paths.Insert(0, cabin);
        foreach (var p in paths)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (pf == null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
            var b = Bounds(go);
            x += b.extents.x + 1.6f;
            go.transform.position = new Vector3(x, 0f, 0f);
            x += b.extents.x;
        }
        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log($"Outpost check row spawned across {x:0.0} m.");
    }

    /// Renders the check row with its own camera and light straight to a PNG.
    /// The scene view frames the mountain, not the row, and a scene-view grab is
    /// not a reliable acceptance test for an import.
    [MenuItem("To The Summit/Outposts/Capture Check Row")]
    public static void CaptureRow()
    {
        var root = GameObject.Find("ZZ_OutpostCheck") ?? Selection.activeGameObject;
        if (root == null) { Debug.LogWarning("Spawn the check row, or select an object to capture."); return; }
        var b = Bounds(root);

        var camGo = new GameObject("ZZ_CheckCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.42f, 0.48f, 0.58f);
        cam.fieldOfView = 32f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        float dist = b.size.x * 0.62f + 12f;
        camGo.transform.position = b.center + new Vector3(-b.size.x * 0.10f, b.size.y * 0.75f, -dist);
        camGo.transform.LookAt(b.center);

        var lightGo = new GameObject("ZZ_CheckLight");
        var li = lightGo.AddComponent<Light>();
        li.type = LightType.Directional;
        li.intensity = 1.5f;
        li.color = new Color(1f, 0.96f, 0.9f);
        lightGo.transform.rotation = Quaternion.Euler(38f, 152f, 0f);

        const int W = 1900, H = 620;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;

        string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "OutpostRow.png");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());

        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(camGo);
        UnityEngine.Object.DestroyImmediate(lightGo);
        Debug.Log($"Outpost row captured to {path} (row is {b.size.x:0.0} m wide).");
    }

    [MenuItem("To The Summit/Outposts/Clear Check Row")]
    public static void ClearRow()
    {
        var old = GameObject.Find("ZZ_OutpostCheck");
        if (old != null) Undo.DestroyObjectImmediate(old);
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// Texture roles are decided by file name because the Blender bake names them:
    /// atlases and roughness carry data, not colour, and must not pass through sRGB.
    static void ConfigureTextures()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            string f = Path.GetFileName(p);
            bool normal = f.Contains("_nor_") || f.Contains("_Normal") || f.Contains("_nor.");
            bool data = f.EndsWith("_Tint.png") || f.EndsWith("_RoughMetal.png")
                        || f.Contains("_rough") || f.Contains("_Rough") || f.Contains("_arm");

            var want = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = !normal && !data;
            if (ti.textureType == want && ti.sRGBTexture == srgb && ti.mipmapEnabled) continue;

            ti.textureType = want;
            ti.sRGBTexture = srgb;
            ti.mipmapEnabled = true;
            ti.wrapMode = f.EndsWith("_Tint.png") || f.EndsWith("_RoughMetal.png")
                ? TextureWrapMode.Clamp     // atlas: kenar tekrar etmemeli
                : TextureWrapMode.Repeat;
            ti.SaveAndReimport();
        }
    }

    static bool ApplyRemap(string fbx, Dictionary<string, Material> remap)
    {
        var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (mi == null) { Debug.LogWarning($"Model not found: {fbx}"); return false; }
        foreach (var kv in remap)
        {
            var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key);
            mi.AddRemap(id, kv.Value);
        }
        mi.SaveAndReimport();
        return true;
    }

    /// One prefab per building so the forest can place instances without touching
    /// the imported model, and so door pivots survive as their own transforms.
    static bool BuildPrefab(string fbx, string name)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        if (src == null) return false;
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        inst.name = $"Outpost_{name}";
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>())
        {
            var f = r.GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) r.gameObject.AddComponent<MeshCollider>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        string path = $"{PrefabDir}/Outpost_{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(inst, path);
        UnityEngine.Object.DestroyImmediate(inst);
        return true;
    }

    static T Load<T>(string p) where T : UnityEngine.Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(p);
        if (a == null) Debug.LogWarning($"Missing asset: {p}");
        return a;
    }

    static void EnsureDir(string dir)
    {
        if (AssetDatabase.IsValidFolder(dir)) return;
        string parent = Path.GetDirectoryName(dir).Replace('\\', '/');
        EnsureDir(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(dir));
    }

    static void SetTransparent(Material m)
    {
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    }
}
