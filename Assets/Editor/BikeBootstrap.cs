using System.IO;
using UnityEditor;
using UnityEngine;

/// BİSİKLETİ SAHNEYE KURAR. Model, doku, materyal, ayar asset'i ve bileşenler koddan
/// bağlanıyor — sahne elle düzenlenmiyor (bkz. `CLAUDE.md`).
///
/// URP'nin Lit gölgelendiricisi ROUGHNESS OKUMUYOR, smoothness okuyor ve onu metallic
/// haritasının ALFA kanalında bekliyor. Meshy ikisini ayrı dosya veriyor. Bu yüzden iki
/// harita burada tek dokuda birleştiriliyor: R = metallic, A = 1 - roughness.
/// Birleştirilmezse yüzey ya tamamen mat ya tamamen ayna çıkıyor.
public static class BikeBootstrap
{
    const string Folder = "Assets/Models/Bike";
    const string ModelPath = Folder + "/Bicycle.fbx";
    const string AlbedoPath = Folder + "/Bicycle_Albedo.png";
    const string NormalPath = Folder + "/Bicycle_Normal.png";
    const string MetallicPath = Folder + "/Bicycle_Metallic.png";
    const string RoughnessPath = Folder + "/Bicycle_Roughness.png";
    const string MaskPath = Folder + "/Bicycle_MetallicSmoothness.png";
    const string MaterialPath = Folder + "/Bicycle.mat";
    const string SettingsPath = "Assets/Settings/BikeSettings.asset";
    const string RoutePath = "Assets/Settings/MountainRoute.asset";

    /// Bisikletin gerçek boyu (metre). Meshy 110 cm yükseklikle verdi; uzunluk ondan
    /// türüyor ve import ölçeği bununla doğrulanıyor.
    const float ExpectedHeight = 1.10f;

    [MenuItem("To The Summit/Model/Bisikleti Sahneye Kur", false, 121)]
    static void Build()
    {
        ConfigureTextures();
        Material material = BuildMaterial();
        ConfigureModel();

        BikeSettings settings = LoadOrCreateSettings();
        GameObject bike = Place(material, settings);

        Selection.activeGameObject = bike;
        Debug.Log($"[Bisiklet] kuruldu: {bike.name}");
    }

    // ------------------------------------------------------------------ dokular

    static void ConfigureTextures()
    {
        SetTexture(AlbedoPath, sRGB: true, normal: false);
        SetTexture(NormalPath, sRGB: false, normal: true);

        // Metallic ve roughness OKUNABİLİR olmalı: birleştirme onları CPU'da okuyor.
        SetTexture(MetallicPath, sRGB: false, normal: false, readable: true);
        SetTexture(RoughnessPath, sRGB: false, normal: false, readable: true);
    }

    static void SetTexture(string path, bool sRGB, bool normal, bool readable = false)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) return;

        importer.textureType = normal ? TextureImporterType.NormalMap
                                      : TextureImporterType.Default;
        importer.sRGBTexture = sRGB;
        importer.isReadable = readable;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();
    }

    /// Metallic ve roughness'ı tek dokuda birleştirir. URP smoothness istiyor, elimizde
    /// roughness var: ikisi birbirinin tersi.
    static Texture2D BuildMask()
    {
        var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
        var roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(RoughnessPath);
        if (metallic == null || roughness == null) return null;

        int size = Mathf.Max(metallic.width, roughness.width);
        var mask = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;

            byte m = (byte)(Mathf.Clamp01(metallic.GetPixelBilinear(u, v).r) * 255f);
            byte s = (byte)((1f - Mathf.Clamp01(roughness.GetPixelBilinear(u, v).r)) * 255f);

            pixels[y * size + x] = new Color32(m, 0, 0, s);
        }

        mask.SetPixels32(pixels);
        mask.Apply(true);

        File.WriteAllBytes(MaskPath, mask.EncodeToPNG());
        Object.DestroyImmediate(mask);

        AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceUpdate);
        SetTexture(MaskPath, sRGB: false, normal: false);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
    }

    static Material BuildMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AlbedoPath));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath));

        Texture2D mask = BuildMask();
        if (mask != null)
        {
            material.SetTexture("_MetallicGlossMap", mask);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");

            // Çarpanlar 1: harita neyi söylüyorsa o. Kısılırsa doku boşuna pişmiş olur.
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
        }

        material.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(material);
        return material;
    }

    // ------------------------------------------------------------------- model

    static void ConfigureModel()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (importer == null) return;

        // Rig ve animasyon YOK: bisiklet bükülmüyor, parçalar dönüyor. Rig içe
        // aktarılırsa her parçaya gereksiz kemik ve deri bilgisi geliyor.
        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;

        // Materyal DIŞARIDAN: FBX'in kendi materyali dokusuz geliyor ve üstüne
        // yazılamıyor.
        importer.materialImportMode = ModelImporterMaterialImportMode.None;

        // Kesim yüzeyleri ve ince parçalar için: yumuşatma açısı düşükse jant telleri
        // fasetli görünüyor.
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;

        // Bölme aracı mesh'i CPU'da okuyor.
        importer.isReadable = true;

        importer.SaveAndReimport();
    }

    static BikeSettings LoadOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<BikeSettings>(SettingsPath);
        if (settings != null) return settings;

        settings = ScriptableObject.CreateInstance<BikeSettings>();
        AssetDatabase.CreateAsset(settings, SettingsPath);
        AssetDatabase.SaveAssets();
        return settings;
    }

    // ---------------------------------------------------------------- sahneye

    static GameObject Place(Material material, BikeSettings settings)
    {
        var existing = Object.FindAnyObjectByType<BikeController>();
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var root = new GameObject("Bicycle");

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        model.transform.localPosition = Vector3.zero;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = material;

        // ÖLÇEK DOĞRULANIYOR. İçe aktarma ölçeği yanlışsa bisiklet ya oyuncak ya dev
        // oluyor ve bu ancak yanına gidince fark ediliyor.
        Bounds bounds = Measure(model);
        Debug.Log($"[Bisiklet] model boyutu {bounds.size.x:F2} x {bounds.size.y:F2} x "
                + $"{bounds.size.z:F2} m (beklenen yükseklik {ExpectedHeight:F2} m)");

        var controller = root.AddComponent<CharacterController>();
        controller.height = Mathf.Max(0.6f, bounds.size.y);
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        root.AddComponent<BikeController>().Bind(settings);
        root.AddComponent<BikePlayerInput>();

        root.transform.position = SpawnPoint();

        Undo.RegisterCreatedObjectUndo(root, "Bisikleti kur");
        return root;
    }

    static Bounds Measure(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds();

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    /// Doğuş noktasının yanı. Bisiklet oyuncunun içinde doğmasın diye iki metre yana
    /// alınıyor; kot arazinin çarpışma yüzeyinden okunuyor.
    static Vector3 SpawnPoint()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        var route = AssetDatabase.LoadAssetAtPath<MountainRoute>(RoutePath);

        if (terrain == null || route == null || !route.spawnSet) return Vector3.zero;

        Vector3 world = MountainRoute.ToWorld(route.spawn, terrain);

        float yaw = route.spawnYaw * Mathf.Deg2Rad;
        var side = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        world += side * 2f;

        float top = terrain.transform.position.y + terrain.terrainData.size.y + 100f;
        var ray = new Ray(new Vector3(world.x, top, world.z), Vector3.down);

        var ground = terrain.GetComponent<TerrainCollider>();
        if (ground != null && ground.Raycast(ray, out RaycastHit hit, top + 1000f))
            world.y = hit.point.y + 0.05f;
        else
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y + 0.05f;

        return world;
    }
}
