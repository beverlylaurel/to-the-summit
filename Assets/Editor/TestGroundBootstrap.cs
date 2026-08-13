using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// TEST SAHNESİ. Mekanikleri dağın üstünde denemek pahalı: her Play'de arazi,
/// hava, bulut ve kar sistemi ayağa kalkıyor, hata ayıklamak için yürünmesi gereken
/// mesafe uzun ve sahne dosyası her denemede kirleniyor.
///
/// Burası düz bir kare alan: ölçek okunabilsin diye ızgara işaretli zemin, oyuncu,
/// yönlü ışık. Karakter, bitki, tırmanma — hepsi burada denenir, beğenilince ana
/// oyun sahnesine taşınır.
///
/// ATMOSFER YOK. Sis, bulut, kar ve rüzgâr sistemleri dağa bağlı ve buraya
/// kurulmuyor: mekanik testinde gereksiz, kurulum süresini ve hata yüzeyini
/// büyütüyor. Görsel doğrulama gerektiğinde ana sahnede yapılır.
public static class TestGroundBootstrap
{
    const string ScenePath = "Assets/Scenes/TestGround.unity";
    const string MaterialPath = "Assets/Settings/TestGround.mat";

    /// Alanın kenarı (metre). 200 m: koşarak yirmi saniyede geçilir, ölçek hissi
    /// için yeterli, kaybolmak için değil.
    const float Size = 200f;

    /// Izgara aralığı (metre). Boy, hız ve zıplama mesafesi bunlarla ölçülüyor.
    const float GridSpacing = 5f;

    const float PlayerHeight = 1.8f;
    const float EyeHeight = 1.65f;

    const string MainScenePath = "Assets/Scenes/Game.unity";

    /// Sahne değiştirmeden önce kaydetme sorusu: kaydedilmemiş iş varsa Unity
    /// soruyor, iptal edilirse geçiş yapılmıyor.
    static bool AskToSave() => EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

    [MenuItem("To The Summit/Sahne/Test Sahnesi _F6", false, 1)]
    public static void Open()
    {
        if (!AskToSave()) return;

        if (!File.Exists(ScenePath)) Create();
        else EditorSceneManager.OpenScene(ScenePath);
    }

    /// Oyun sahnesine dönüş. Menüde iki komşu satır: gidiş ve dönüş aynı yerde
    /// durmazsa "nasıl döneceğim" sorusu her seferinde tekrar sorulur.
    [MenuItem("To The Summit/Sahne/Oyun Sahnesi _F5", false, 0)]
    public static void OpenMain()
    {
        if (!AskToSave()) return;
        EditorSceneManager.OpenScene(MainScenePath);
    }

    [MenuItem("To The Summit/Sahne/Test Sahnesini Yeniden Kur", false, 2)]
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
        ToolLog.Write($"Test sahnesi kuruldu: {ScenePath} — {Size}x{Size} m, "
                + $"{GridSpacing} m ızgara.");
    }

    /// Zemin: tek quad, ızgara dokusu prosedürel. Terrain kurmak gereksiz — burada
    /// yükseklik yok, çarpışma düz bir kutuyla çözülüyor.
    static void BuildGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";

        // Unity'nin Plane ilkeli 10 metre; ölçek doğrudan kenar uzunluğunu veriyor.
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

        // Doku metre başına bir kez: ızgara karesi gerçek mesafeyi gösteriyor.
        material.SetTextureScale("_BaseMap", Vector2.one * (Size / GridSpacing));
        material.SetFloat("_Smoothness", 0.05f);
        EditorUtility.SetDirty(material);
        return material;
    }

    /// Izgara dokusu: kenarları koyu bir kare. Ölçek referansı — oyuncunun boyu,
    /// adım uzunluğu ve zıplama mesafesi buna bakarak okunuyor.
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

    /// Yönlü ışık: sabit, öğleden biraz önce. Gün döngüsü yok — mekanik testinde
    /// ışığın değişmesi karşılaştırmayı bozuyor.
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

    /// Oyuncu: ana sahnedekiyle AYNI bileşenler ve ölçüler. Farklı olsaydı burada
    /// doğrulanan hareket dağda başka türlü davranırdı.
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

        // Bakış OYUNCUDA, kamerada değil — ana sahnedeki bağlanma da böyle. Kamera
        // pivota bağlı; MouseLook o pivotu döndürüyor.
        player.AddComponent<MouseLook>().Bind(head.transform);

        // Serbest uçuş KAPALI başlar. Açıkken CharacterController'ı devre dışı
        // bırakıyor (ikisi aynı kontrolcüyü kullanıyor); açık kurulunca yürüyüş
        // kapalı kontrolcüye Move çağırıyor ve her kare hata basıyordu.
        var flyer = player.AddComponent<FreeFlyMovement>();
        flyer.Bind(head.transform);
        flyer.enabled = false;
    }
}
