using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// KÖŞE RENGİNE MALZEME MASKESİ SÜRER. Üretilen modelde UV yok ve farklı malzemeler aynı
/// mesh'te geliyor; tutamağın nerede bittiği, kablonun nerede başladığı bir eşik sayısıyla
/// tarif edilemiyor. Fırça o sınırı gözle koyuyor.
///
/// MASKE KÖŞEDE DURUYOR, DOKUDA DEĞİL. Model milyonlarca köşe taşıyor: santimetre altı
/// çözünürlük demek, üstelik UV gerektirmiyor. Gölgelendirici üç kanalı üç malzeme olarak
/// okuyor (bkz. `BikeSurface`).
///
/// BOYAMA PENCERENİN İÇİNDE VE YALNIZ SEÇİLİ PARÇA ÇİZİLİYOR. Sahnede boyamak, sekiz
/// kilometre ötedeki karanlık bir noktaya gidip parçayı bulmak ve komşu parçaların
/// arkasından boyamak demekti.
///
/// IŞIN GERÇEK KONUMDAN. Parça sahnedeki yerinde çiziliyor ve ışın oraya atılıyor:
/// önizleme için ayrı bir dünya kurulsaydı fırçanın değdiği nokta ile mesh'in köşeleri
/// farklı uzaylarda kalırdı.
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

    /// Köşe ızgarası: hücre boyutu fırça yarıçapından, hücre başına köşe listesi. Her
    /// darbede bütün köşeleri taramak yarım milyon karşılaştırma demek.
    Dictionary<Vector3Int, List<int>> grid;
    float cell;

    PreviewRenderUtility preview;
    float yaw = 30f;
    float pitch = 12f;
    float zoom = 1.4f;
    Vector3 focus;

    bool painting;

    [MenuItem("To The Summit/Model/Bisiklet/Malzeme Fırçası", false, 124)]
    static void Open() => GetWindow<VertexBrush>("Malzeme Fırçası").Show();

    void OnEnable()
    {
        // Derleme sonrası hedef alanı seri hâlde duruyor ama mesh, ızgara ve çarpışma
        // kayboluyor; hazırlık yenilenmezse fırça sessizce ölü kalıyordu.
        if (target != null) EditorApplication.delayCall += Prepare;
    }

    void OnDisable()
    {
        Release();

        preview?.Cleanup();
        preview = null;
    }

    // -------------------------------------------------------------------- gui

    void OnGUI()
    {
        Toolbar();

        Rect view = GUILayoutUtility.GetRect(position.width, position.height - 158f);

        if (target == null || mesh == null)
        {
            EditorGUI.HelpBox(view,
                "Boyanacak parçayı yukarıdaki alana sürükle.\n\n"
                + "Sol tuş boyar, Shift basılıyken siler.\n"
                + "Sağ tuş döndürür, orta tuş kaydırır, tekerlek yakınlaştırır.",
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

            EditorGUILayout.BeginHorizontal();
            radius = EditorGUILayout.Slider("Yarıçap (m)", radius, 0.003f, 0.2f);
            erase = GUILayout.Toggle(erase, "Silgi", EditorStyles.miniButton, GUILayout.Width(52f));
            EditorGUILayout.EndHorizontal();

            strength = EditorGUILayout.Slider("Şiddet", strength, 0.05f, 1f);
        }

        if (target != null) ChannelSurface();
    }

    /// KANALIN MALZEMESİ BURADAN AYARLANIYOR. Kanal adı (kauçuk, deri, çelik) yalnız bir
    /// etiket; yüzeyin nasıl göründüğü materyalde duruyor. Fırçayı kullanan kişi rengi
    /// seçemezse boyama "şu kanalı sür, sonucu başka yerden ayarla" hâline geliyor.
    ///
    /// Değer parçanın BÜTÜN materyallerine yazılıyor: bir kanal her yerde aynı malzemeyi
    /// anlatmalı, aynı bisikletin iki parçasında farklı kauçuk olmamalı.
    void ChannelSurface()
    {
        Material[] materials = target.GetComponent<Renderer>().sharedMaterials;
        if (materials.Length == 0 || materials[0] == null) return;

        string prefix = "_Mask" + "RGB"[channel];

        var colour = materials[0].GetColor(prefix + "Color");
        float metallic = materials[0].GetFloat(prefix + "Metallic");
        float smoothness = materials[0].GetFloat(prefix + "Smoothness");

        EditorGUILayout.Space(2f);
        EditorGUI.BeginChangeCheck();

        colour = EditorGUILayout.ColorField("Renk", colour);

        EditorGUILayout.BeginHorizontal();
        metallic = EditorGUILayout.Slider("Metaliklik", metallic, 0f, 1f);
        smoothness = EditorGUILayout.Slider("Parlaklık", smoothness, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        if (!EditorGUI.EndChangeCheck()) return;

        foreach (Material material in materials)
        {
            if (material == null) continue;

            material.SetColor(prefix + "Color", colour);
            material.SetFloat(prefix + "Metallic", metallic);
            material.SetFloat(prefix + "Smoothness", smoothness);
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
        Repaint();
    }

    void Footer()
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField($"Köşe {vertices.Length:N0}   Boyalı {Painted():N0}",
            EditorStyles.miniLabel);

        if (GUILayout.Button("Kanalı temizle", EditorStyles.miniButton, GUILayout.Width(110f)))
            Clear(channel);

        if (GUILayout.Button("Hepsini temizle", EditorStyles.miniButton, GUILayout.Width(110f)))
            Clear(-1);

        EditorGUILayout.EndHorizontal();
    }

    int Painted()
    {
        int count = 0;
        foreach (Color32 colour in colours)
            if (colour.r > 8 || colour.g > 8 || colour.b > 8) count++;

        return count;
    }

    // ---------------------------------------------------------------- görüntü

    void Viewport(Rect view)
    {
        preview ??= new PreviewRenderUtility();

        Input(view);

        Bounds bounds = target.GetComponent<Renderer>().bounds;
        float distance = Mathf.Max(0.05f, bounds.size.magnitude * zoom);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 position = focus - rotation * Vector3.forward * distance;

        preview.BeginPreview(view, GUIStyle.none);

        preview.camera.transform.SetPositionAndRotation(position, rotation);
        preview.camera.nearClipPlane = distance * 0.01f;
        preview.camera.farClipPlane = distance * 10f;
        preview.camera.fieldOfView = 35f;
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f);

        // İki ışık: karşıdan ana, arkadan dolgu. Tek ışıkta siyah kauçuk ile koyu çelik
        // birbirinden ayırt edilemiyor. Işıklar kamerayla dönüyor, yoksa parçayı
        // çevirdiğinde karanlıkta kalıyor.
        preview.lights[0].intensity = 1.4f;
        preview.lights[0].transform.rotation = Quaternion.Euler(35f, yaw + 40f, 0f);
        preview.lights[1].intensity = 0.7f;
        preview.lights[1].transform.rotation = Quaternion.Euler(-20f, yaw + 200f, 0f);

        // YALNIZ SEÇİLİ PARÇA. Alt-mesh'ler kendi materyalleriyle çiziliyor ki boyama
        // oyundaki hâliyle görünsün.
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

    /// Fırça halkası: imlecin altındaki yüzeye çiziliyor. Halka olmadan fırçanın nereye
    /// değdiği ancak boyadıktan sonra anlaşılıyor.
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

    // ------------------------------------------------------------------ girdi

    void Input(Rect view)
    {
        Event current = Event.current;
        if (!view.Contains(current.mousePosition)) return;

        if (current.type == EventType.ScrollWheel)
        {
            zoom = Mathf.Clamp(zoom * (1f + current.delta.y * 0.05f), 0.15f, 4f);
            current.Use();
            Repaint();
            return;
        }

        // Sağ tuş döndürüyor, orta tuş kaydırıyor: sol tuş boyamaya ayrıldı, yoksa her
        // fırça darbesi kamerayı da oynatırdı.
        if (current.type == EventType.MouseDrag && current.button == 1)
        {
            yaw += current.delta.x;
            pitch = Mathf.Clamp(pitch + current.delta.y, -85f, 85f);
            current.Use();
            Repaint();
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 2)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float scale = target.GetComponent<Renderer>().bounds.size.magnitude * zoom * 0.002f;

            focus -= rotation * new Vector3(current.delta.x * scale,
                -current.delta.y * scale, 0f);

            current.Use();
            Repaint();
            return;
        }

        if (current.button != 0) return;

        if (current.type == EventType.MouseDown
            && Trace(view, current.mousePosition, out RaycastHit down))
        {
            // Darbe başına değil, fırça basımı başına kayıt: sürüklerken her karede
            // kayıt alınsaydı geri alma yığını yüz binlerce köşeyle dolardı.
            Undo.RegisterCompleteObjectUndo(mesh, "Malzeme fırçası");

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

    /// Pencere içindeki noktadan parçaya ışın. Önizleme kamerası parçanın gerçek dünya
    /// konumunda duruyor, o yüzden ışın doğrudan sahnedeki çarpışmaya atılabiliyor.
    bool Trace(Rect view, Vector2 mouse, out RaycastHit hit)
    {
        hit = default;
        if (preview == null || collider == null) return false;

        Vector2 local = mouse - view.position;
        var point = new Vector3(local.x, view.height - local.y, 0f);

        Camera camera = preview.camera;
        camera.pixelRect = new Rect(0f, 0f, view.width, view.height);

        return collider.Raycast(camera.ScreenPointToRay(point), out hit, 10000f);
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

        // Işın için geçici çarpışma; `Release` alıyor. Kalıcı bırakılsaydı iki yüz bin
        // üçgenlik çarpışma sahnede dururdu.
        collider = target.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.hideFlags = HideFlags.HideAndDontSave;

        focus = target.GetComponent<Renderer>().bounds.center;
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
            existing = Instantiate(source);
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

    // ----------------------------------------------------------------- boyama

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
                colours[index] = Blend(colours[index], strength * falloff, removing);
            }
        }

        mesh.SetColors(colours);
        Repaint();
    }

    Color32 Blend(Color32 colour, float amount, bool removing)
    {
        byte[] rgb = { colour.r, colour.g, colour.b };
        float goal = removing ? 0f : 255f;

        rgb[channel] = (byte)Mathf.RoundToInt(Mathf.Lerp(rgb[channel], goal, amount));

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
