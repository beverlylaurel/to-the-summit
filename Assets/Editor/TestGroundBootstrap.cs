using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// TEST SCENE BOOTSTRAP. Testing mechanics on the mountain is heavy: every Play mode run
/// initializes terrain, weather, cloud, and snow systems, requiring long travel distances
/// to debug and dirtying the main scene file.
///
/// This scene is a flat square arena: scale-calibrated grid ground, player, directional sun.
/// Character, vegetation, climbing mechanics are tuned here before deployment to main game scene.
///
/// NO ATMOSPHERE SYSTEMS. Fog, cloud, snow, and wind are excluded to keep setup fast
/// and isolate test scope. Visual integration is verified in the main game scene.
public static class TestGroundBootstrap
{
    const string ScenePath = "Assets/Scenes/TestGround.unity";
    const string MaterialPath = "Assets/Settings/TestGround.mat";

    /// Arena side length (meters). 200 m: traversable by sprinting in 20 seconds,
    /// sufficient for scale perception without getting lost.
    const float Size = 200f;

    /// Grid spacing (meters). Height, movement speed, and jump distances calibrated against this grid.
    const float GridSpacing = 5f;

    const float PlayerHeight = 1.8f;
    const float EyeHeight = 1.65f;

    const string MainScenePath = "Assets/Scenes/Game.unity";

    static bool AskToSave() => EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

    [MenuItem("To The Summit/Scene/Test Scene _F6", false, 1)]
    public static void Open()
    {
        if (!AskToSave()) return;

        if (!File.Exists(ScenePath)) Create();
        else EditorSceneManager.OpenScene(ScenePath);
    }

    [MenuItem("To The Summit/Scene/Game Scene _F5", false, 0)]
    public static void OpenMain()
    {
        if (!AskToSave()) return;
        EditorSceneManager.OpenScene(MainScenePath);
    }

    [MenuItem("To The Summit/Scene/Rebuild Test Scene", false, 2)]
    public static void Recreate()
    {
        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
        Create();
    }

    static void Create()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                                                NewSceneMode.Single);

        BuildGround();
        BuildLight();
        BuildPlayer();

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        ToolLog.Write($"Test scene built: {ScenePath} — {Size}x{Size} m, "
                + $"{GridSpacing} m grid.");
    }

    /// Ground: single plane primitive with procedural grid texture.
    static void BuildGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";

        // Unity Plane primitive is 10 meters; scale sets side length directly.
        ground.transform.localScale = new Vector3(Size / 10f, 1f, Size / 10f);

        ground.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMaterial();
    }

    static Material LoadOrCreateMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetTexture("_BaseMap", LoadOrCreateGrid());

        // Texture tiles once per meter; grid lines reflect true distance.
        material.SetTextureScale("_BaseMap", Vector2.one * (Size / GridSpacing));
        material.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// Grid texture: square with dark borders for spatial scale reference.
    static Texture2D LoadOrCreateGrid()
    {
        const string Path = "Assets/Settings/TestGrid.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
        if (existing != null) return existing;

        const int Size = 256;
        const int Line = 4;

        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var pixels = new Color32[Size * Size];

        var fill = new Color32(96, 96, 98, 255);
        var line = new Color32(52, 52, 56, 255);

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            pixels[y * Size + x] = (x < Line || y < Line) ? line : fill;

        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(Path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
    }

    /// Directional sun: fixed late morning lighting. No time-of-day cycle during mechanics testing.
    static void BuildLight()
    {
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(45f, 35f, 0f);
        light.shadows = LightShadows.Soft;
        light.intensity = 1.1f;
        light.color = new Color(1f, 0.97f, 0.92f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.48f, 0.58f);
        RenderSettings.ambientEquatorColor = new Color(0.32f, 0.34f, 0.36f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.14f);
    }

    /// Player: identical components and dimensions to main game scene.
    static void BuildPlayer()
    {
        var player = new GameObject("Player");

        var controller = player.AddComponent<CharacterController>();
        controller.height = PlayerHeight;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);

        player.AddComponent<FirstPersonController>();
        player.AddComponent<CursorLock>();
        player.transform.position = new Vector3(0f, 0.1f, 0f);

        var head = new GameObject("CameraPivot");
        head.transform.SetParent(player.transform, false);
        head.transform.localPosition = new Vector3(0f, EyeHeight, 0f);

        var camera = new GameObject("Main Camera") { tag = "MainCamera" };
        camera.AddComponent<Camera>();
        camera.AddComponent<AudioListener>();
        camera.transform.SetParent(head.transform, false);
        camera.GetComponent<Camera>().farClipPlane = 600f;

        player.AddComponent<MouseLook>().Bind(head.transform);

        // Free fly movement starts disabled.
        var flyer = player.AddComponent<FreeFlyMovement>();
        flyer.Bind(head.transform);
        flyer.enabled = false;
    }
}
