using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// KÖŞE RENGİNE MALZEME MASKESİ SÜRER. Üretilen modelde UV yok ve farklı malzemeler aynı
/// mesh'te geliyor; tutamağın nerede bittiği, kablonun nerede başladığı bir eşik sayısıyla
/// tarif edilemiyor. Fırça o sınırı gözle koyuyor.
///
/// MASKE KÖŞEDE DURUYOR, DOKUDA DEĞİL. Model 1.5 milyon köşe taşıyor: santimetre altı
/// çözünürlük demek, doku çözünürlüğünden yüksek ve UV gerektirmiyor. Gölgelendirici üç
/// kanalı üç malzeme olarak okuyor (bkz. `BikeSurface`).
///
/// IŞIN İÇİN GEÇİCİ ÇARPIŞMA. Parçalarda çarpışma yok; boyarken hedefe `MeshCollider`
/// takılıp iş bitince alınıyor. Kalıcı bırakılsaydı üç milyon üçgenlik çarpışma sahnede
/// dururdu.
///
/// KOMŞU ARAMA IZGARAYLA. Her darbede bütün köşeleri taramak yarım milyon karşılaştırma
/// demek; köşeler bir kez hücrelere bölünüyor, fırça yalnız değdiği hücrelere bakıyor.
public class VertexBrush : EditorWindow
{
    static readonly string[] ChannelNames = { "Kırmızı — kauçuk", "Yeşil — deri", "Mavi — çelik" };

    [SerializeField] MeshFilter target;
    [SerializeField] int channel;
    [SerializeField] float radius = 0.03f;
    [SerializeField] float strength = 1f;
    [SerializeField] bool erase;

    Mesh mesh;
    Vector3[] vertices;
    Color32[] colours;
    MeshCollider collider;

    /// Köşe ızgarası: hücre boyutu fırça yarıçapından, hücre başına köşe listesi.
    Dictionary<Vector3Int, List<int>> grid;
    float cell;

    bool painting;

    [MenuItem("To The Summit/Model/Bisiklet/Malzeme Fırçası", false, 124)]
    static void Open() => GetWindow<VertexBrush>("Malzeme Fırçası").Show();

    void OnEnable()
    {
        SceneView.duringSceneGui += OnScene;

        // Derleme sonrası hedef alanı seri hâlde duruyor ama mesh, ızgara ve çarpışma
        // kayboluyor; hazırlık yenilenmezse fırça sessizce ölü kalıyordu.
        if (target != null) Prepare();
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnScene;
        Release();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Boyanacak parçayı seç (sahnede tıkla, sonra aşağıdaki alana sürükle).\n" +
            "2. Kanal seç: her kanal bir malzeme.\n" +
            "3. Sahnede sol tuşla sür. Shift basılıyken siler.\n\n" +
            "Ctrl+tekerlek yarıçapı değiştirir.", MessageType.None);

        MeshFilter picked = (MeshFilter)EditorGUILayout.ObjectField(
            "Parça", target, typeof(MeshFilter), true);

        // HAZIRLIK BİR SONRAKİ KAREYE. Seçim değiştiğinde çarpışma ekleniyor ve ilk
        // boyamada mesh asset'i üretiliyor; ikisi de çizim ortasında yapılınca Unity'nin
        // yerleşim düzeni kırılıyor ("BeginLayoutGroup must be called first").
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
            channel = EditorGUILayout.Popup("Kanal", channel, ChannelNames);
            radius = EditorGUILayout.Slider("Yarıçap (m)", radius, 0.005f, 0.25f);
            strength = EditorGUILayout.Slider("Şiddet", strength, 0.05f, 1f);
            erase = EditorGUILayout.Toggle("Silgi", erase);
        }

        if (target == null || mesh == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Köşe", $"{vertices.Length:N0}");
        EditorGUILayout.LabelField("Boyalı köşe", $"{Painted():N0}");

        EditorGUILayout.Space();

        if (GUILayout.Button("Bu kanalı temizle")) Clear(channel);
        if (GUILayout.Button("Bütün maskeyi temizle")) Clear(-1);
    }

    int Painted()
    {
        int count = 0;
        foreach (Color32 colour in colours)
            if (colour.r > 8 || colour.g > 8 || colour.b > 8) count++;

        return count;
    }

    // ------------------------------------------------------------------ hedef

    void Prepare()
    {
        if (target == null) return;

        EnsureWritable();
        mesh = target.sharedMesh;
        vertices = mesh.vertices;

        colours = mesh.colors32;
        if (colours == null || colours.Length != vertices.Length)
            colours = new Color32[vertices.Length];

        // Çarpışma hedefin üstüne takılıyor; ışın buna atılıyor ve `OnDisable` alıyor.
        collider = target.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.hideFlags = HideFlags.HideAndDontSave;

        BuildGrid();
    }

    /// BOYANABİLİR KOPYA. Parçaların çoğu mesh'ini doğrudan FBX'ten alıyor; oraya köşe
    /// rengi yazılamaz, yazılsa da modelin her yeniden içe aktarımında silinir. İlk
    /// boyamada parçanın kendi kopyası üretiliyor ve kurulum betiği bu kopyayı bulup
    /// bağlıyor (bkz. `BikeBootstrap.Painted`).
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
            existing = Object.Instantiate(source);
            existing.name = source.name;
            existing.SetColors(new Color32[existing.vertexCount]);

            AssetDatabase.CreateAsset(existing, copyPath);
            AssetDatabase.SaveAssets();

            ToolLog.Write($"[Fırça] {target.name} için boyanabilir kopya üretildi: {copyPath}");
        }

        target.sharedMesh = existing;
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

    /// Izgara hücresi fırça yarıçapı kadar: fırça en fazla iki hücre komşuluğuna bakıyor.
    /// Hücre küçük olsaydı sözlük şişerdi, büyük olsaydı hücre başına düşen köşe artardı.
    void BuildGrid()
    {
        // Hücre MESH UZAYINDA. Köşeler o uzayda duruyor ve parça dönüşümünde yüz kat
        // ölçek var; dünya metresiyle kurulsaydı bütün model birkaç hücreye düşer,
        // ızgara hiçbir şey kazandırmazdı.
        float scale = Mathf.Max(1e-6f, target.transform.lossyScale.x);
        cell = Mathf.Max(1e-5f, radius / scale);
        grid = new Dictionary<Vector3Int, List<int>>(vertices.Length / 8 + 1);

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = Cell(vertices[i]);
            if (!grid.TryGetValue(key, out List<int> bucket))
                grid[key] = bucket = new List<int>();

            bucket.Add(i);
        }
    }

    Vector3Int Cell(Vector3 local) => new Vector3Int(
        Mathf.FloorToInt(local.x / cell),
        Mathf.FloorToInt(local.y / cell),
        Mathf.FloorToInt(local.z / cell));

    // ------------------------------------------------------------------ boyama

    void OnScene(SceneView view)
    {
        if (target == null || mesh == null) return;

        Event current = Event.current;

        // Ctrl+tekerlek yarıçapı değiştiriyor: fırça boyunu ayarlamak için pencereye
        // dönmek gerekmesin.
        if (current.type == EventType.ScrollWheel && current.control)
        {
            radius = Mathf.Clamp(radius * (1f - current.delta.y * 0.05f), 0.005f, 0.25f);
            BuildGrid();
            current.Use();
            Repaint();
            return;
        }

        int control = GUIUtility.GetControlID(FocusType.Passive);

        if (current.type == EventType.Layout)
            HandleUtility.AddDefaultControl(control);

        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        bool hit = collider.Raycast(ray, out RaycastHit surface, 1000f);

        if (hit)
        {
            Handles.color = erase || current.shift
                ? new Color(1f, 0.3f, 0.2f, 0.9f)
                : new Color(0.2f, 0.9f, 1f, 0.9f);

            Handles.DrawWireDisc(surface.point, surface.normal, radius);
            view.Repaint();
        }

        if (current.alt || current.button != 0) return;

        if (current.type == EventType.MouseDown && hit)
        {
            // Darbe başına değil, fırça basımı başına kayıt: sürüklerken her karede
            // kayıt alınsaydı geri alma yığını yüz binlerce köşeyle dolardı.
            Undo.RegisterCompleteObjectUndo(mesh, "Malzeme fırçası");

            painting = true;
            Paint(surface.point, current.shift);
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && painting && hit)
        {
            Paint(surface.point, current.shift);
            current.Use();
        }
        else if (current.type == EventType.MouseUp && painting)
        {
            painting = false;
            Commit();
            current.Use();
        }
    }

    void Paint(Vector3 worldPoint, bool eraseNow)
    {
        Vector3 local = target.transform.InverseTransformPoint(worldPoint);

        // Yarıçap dünyada metre, köşeler mesh uzayında: ölçek arada. Parça dönüşümünde
        // yüz kat ölçek var, çevrilmezse fırça ya bütün parçayı boyar ya hiçbir şeyi.
        float scale = Mathf.Max(1e-6f, target.transform.lossyScale.x);
        float localRadius = radius / scale;

        Vector3Int centre = Cell(local);
        int reach = Mathf.CeilToInt(localRadius / cell);
        bool removing = eraseNow || erase;

        for (int x = -reach; x <= reach; x++)
        for (int y = -reach; y <= reach; y++)
        for (int z = -reach; z <= reach; z++)
        {
            var key = new Vector3Int(centre.x + x, centre.y + y, centre.z + z);
            if (!grid.TryGetValue(key, out List<int> bucket)) continue;

            foreach (int index in bucket)
            {
                float distance = Vector3.Distance(vertices[index], local);
                if (distance > localRadius) continue;

                // Kenara doğru zayıflıyor: sert kenar boyalı ile boyasız arasında
                // görünür bir çizgi bırakıyor.
                float falloff = 1f - Mathf.SmoothStep(0f, 1f, distance / localRadius);
                float amount = strength * falloff;

                colours[index] = Blend(colours[index], amount, removing);
            }
        }

        mesh.SetColors(colours);
    }

    Color32 Blend(Color32 colour, float amount, bool removing)
    {
        byte[] rgb = { colour.r, colour.g, colour.b };
        float target = removing ? 0f : 255f;

        rgb[channel] = (byte)Mathf.RoundToInt(
            Mathf.Lerp(rgb[channel], target, amount));

        // Diğer kanallar aynı köşede duruyorsa siliniyor: bir köşe tek malzeme.
        if (!removing)
            for (int i = 0; i < 3; i++)
                if (i != channel)
                    rgb[i] = (byte)Mathf.RoundToInt(Mathf.Lerp(rgb[i], 0f, amount));

        return new Color32(rgb[0], rgb[1], rgb[2], 255);
    }

    void Clear(int only)
    {
        for (int i = 0; i < colours.Length; i++)
        {
            if (only < 0) { colours[i] = new Color32(0, 0, 0, 255); continue; }

            byte[] rgb = { colours[i].r, colours[i].g, colours[i].b };
            rgb[only] = 0;
            colours[i] = new Color32(rgb[0], rgb[1], rgb[2], 255);
        }

        mesh.SetColors(colours);
        Commit();
    }

    /// Boyama mesh ASSET'İNE yazılıyor. Sahnedeki örneğe yazılsaydı kurulum betiği bir
    /// daha çalıştığında kaybolurdu.
    void Commit()
    {
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
    }
}
