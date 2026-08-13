using System.Text;
using UnityEditor;
using UnityEngine;

/// BİSİKLETİ SAHNEYE KURAR. Model, materyal, ayar asset'i ve bileşenler koddan
/// bağlanıyor — sahne elle düzenlenmiyor (bkz. `CLAUDE.md`).
///
/// İçe aktarma ayarı burada DEĞİL: rig, ölçek ve okunabilirlik `ModelImportRules`'ta,
/// yani modelin her yenilenmesinde kendiliğinden uygulanıyor. Menüye bağlı olsaydı
/// modeli değiştirip menüye basmayı unutmak sessizce yanlış ayar bırakırdı.
///
/// DOKU DOSYASI YOK. Model remesh edilmeden geldiği için UV'sine güvenilmiyor; yüzey
/// `ToTheSummit/BikeSurface` ile prosedürel boyanıyor ve bütün desen dünya konumundan
/// türüyor.
///
/// PARÇA TABLOSU BASILIYOR. Modelde 26 parça var ve adlarından hangisinin ne olduğu
/// anlaşılmıyor (`model_part7`); boyut ve konum yazılınca eşleştirilebiliyor.
public static class BikeBootstrap
{
    const string Folder = "Assets/Models/Bike";
    const string ModelPath = Folder + "/Bicycle.fbx";
    const string SettingsPath = "Assets/Settings/BikeSettings.asset";
    const string RoutePath = "Assets/Settings/MountainRoute.asset";
    const string ShaderName = "ToTheSummit/BikeSurface";

    /// Bisikletin gerçek boyu (metre). Model 120 cm yükseklikle geliyor.
    const float ExpectedHeight = 1.20f;

    // Parça tablosundan okunan eşleşmeler. Tekerlek çapı 0.73 m ve ikisi bisikletin iki
    // ucunda; gidon tam genişlikte ve en yukarıda — hepsi ölçüden ayırt edilebiliyor.
    const string FrontWheelPart = "model_part25";
    const string RearWheelPart = "model_part14";
    const string HandlebarPart = "model_part8";

    /// Direksiyonla dönen parçaların başladığı yer: modelin arka ucundan itibaren metre.
    /// Ön takımın tamamı (çatal, gidon, fren kolları, kablolar) bu eşiğin önünde duruyor.
    /// Yanlış parça dönüyorsa `BikeRigProbe` ile görülüp bu sayı değiştirilir.
    const float SteeringFrom = 1.20f;

    /// Malzeme takımı. Bisiklet dört yüzeyden ibaret: boyalı çelik, mat krom, lastik,
    /// deri. Hepsi aynı gölgelendirici, farklı ayar.
    static readonly (string Name, Color Colour, float Metallic, float Smoothness,
                     float Dust, float Fade)[] Surfaces =
    {
        ("Paint",   new Color(0.42f, 0.10f, 0.07f), 0.0f, 0.45f, 0.35f, 0.30f),
        ("Chrome",  new Color(0.62f, 0.63f, 0.65f), 0.9f, 0.55f, 0.30f, 0.05f),
        ("Rubber",  new Color(0.09f, 0.09f, 0.10f), 0.0f, 0.18f, 0.45f, 0.10f),
        ("Leather", new Color(0.24f, 0.15f, 0.09f), 0.0f, 0.30f, 0.25f, 0.20f),
    };

    [MenuItem("To The Summit/Model/Bisikleti Sahneye Kur", false, 121)]
    static void Build()
    {
        Material[] materials = BuildMaterials();
        if (materials.Length == 0) return;

        BikeSettings settings = LoadOrCreateSettings();
        Selection.activeGameObject = Place(materials[0], settings);
    }

    static Material[] BuildMaterials()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[Bisiklet] gölgelendirici bulunamadı: {ShaderName}");
            return new Material[0];
        }

        var materials = new Material[Surfaces.Length];

        for (int i = 0; i < Surfaces.Length; i++)
        {
            string path = $"{Folder}/Bicycle_{Surfaces[i].Name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", Surfaces[i].Colour);
            material.SetFloat("_Metallic", Surfaces[i].Metallic);
            material.SetFloat("_Smoothness", Surfaces[i].Smoothness);
            material.SetFloat("_Dust", Surfaces[i].Dust);
            material.SetFloat("_Fade", Surfaces[i].Fade);

            EditorUtility.SetDirty(material);
            materials[i] = material;
        }

        AssetDatabase.SaveAssets();
        return materials;
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

    // ----------------------------------------------------------------- sahneye

    static GameObject Place(Material material, BikeSettings settings)
    {
        // Seçim ÖNCE bırakılıyor: Inspector yok edilen nesneyi çizmeye devam edip
        // her açılışta bir yığın `MissingReferenceException` basıyordu.
        Selection.activeGameObject = null;

        var existing = Object.FindAnyObjectByType<BikeController>();
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (prefab == null)
        {
            Debug.LogError("[Bisiklet] model içe aktarılamadı.");
            return null;
        }

        var root = new GameObject("Bicycle");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);

        // Prefab bağı ÇÖZÜLÜYOR: prefab örneğinin çocukları yeniden ebeveynlenemiyor ve
        // dönen parçaları ayırmak tam olarak bunu gerektiriyor. Mesh'ler yine FBX'ten
        // paylaşılıyor — kopya çıkmıyor, yalnız hiyerarşi serbest kalıyor.
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);

        // Kök başlangıçta orijinde ve model dönüşsüz: bütün ölçüler böylece doğrudan
        // modelin kendi eksenlerinde okunuyor, dönüşüm çevirmeye gerek kalmıyor.
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterial = material;

        Report(model);
        Rig(model, out Transform steering, out Transform frontWheel, out Transform rearWheel);

        // Modelin uzunluk ekseni +X'te geliyor, oysa kontrolcü `transform.forward` (+Z)
        // yönünde sürüyor. Çeyrek tur çevirip modeli kökün önüne bakacak hâle getiriyoruz.
        model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

        // Tekerlekler yere değsin ve bisiklet kökün üstünde ortalansın: model kendi
        // orijinini nerede taşırsa taşısın, kök hep yer temasında duruyor.
        Bounds bounds = Measure(model);
        model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = Measure(model);

        var controller = root.AddComponent<CharacterController>();
        controller.height = Mathf.Max(0.6f, bounds.size.y);
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        var bike = root.AddComponent<BikeController>();
        bike.Bind(settings);
        root.AddComponent<BikePlayerInput>();

        // Tekerlek ekseni modelin genişlik ekseni (+Z). Direksiyon altındaki ön tekerlek
        // de aynı eksende dönüyor: eğik direksiyon pivotu ile tekerlek arasına dönüşü
        // sıfırlayan bir yuva konuyor (bkz. `Rig`).
        root.AddComponent<BikeWheels>().Bind(bike, settings, frontWheel, rearWheel, Vector3.forward);
        root.AddComponent<BikeSteeringVisual>().Bind(bike, steering);

        root.transform.position = SpawnPoint();

        Undo.RegisterCreatedObjectUndo(root, "Bisikleti kur");
        return root;
    }

    /// DÖNEN PARÇALARI AYIRIR. Model tek dosyada 26 parça olarak geliyor ama hepsi
    /// kımıldamaz bir hiyerarşide; tekerleğin kendi göbeğinde dönmesi ve ön takımın
    /// direksiyon ekseninde çevrilmesi için araya pivot nesneleri giriyor.
    ///
    /// Parçaların kendi dönüşüm orijini modelin orijininde duruyor: doğrudan
    /// döndürülselerdi tekerlek kendi ekseninde değil bisikletin ortasında dönerdi.
    static void Rig(GameObject model, out Transform steering,
        out Transform frontWheel, out Transform rearWheel)
    {
        Transform frontPart = FindPart(model, FrontWheelPart);
        Transform rearPart = FindPart(model, RearWheelPart);
        Transform barPart = FindPart(model, HandlebarPart);

        // Jant düzeltmesi göbek ölçümünden ÖNCE: düzeltme dış kenarı oynatıyor, pivot da
        // o kenardan uydurulan çemberin merkezine oturuyor. Sonra yapılsaydı pivot eski
        // mesh'in merkezinde kalırdı.
        RoundWheel(frontPart, model.transform.forward, "Ön");
        RoundWheel(rearPart, model.transform.forward, "Arka");

        Vector3 frontHub = frontPart.GetComponent<Renderer>().bounds.center;
        Vector3 rearHub = rearPart.GetComponent<Renderer>().bounds.center;
        Vector3 bar = barPart.GetComponent<Renderer>().bounds.center;

        // Direksiyon ekseni ÖLÇÜLÜYOR, yazılmıyor: ön göbekten gidon merkezine giden
        // doğru. Sabit bir açı yazılsaydı model değişince sessizce yalan olurdu.
        Vector3 axis = (bar - frontHub).normalized;

        steering = new GameObject("Steering").transform;
        steering.SetParent(model.transform, false);
        steering.localPosition = frontHub;
        steering.localRotation = Quaternion.FromToRotation(Vector3.up, axis);

        float back = Measure(model).min.x;

        foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.transform == frontPart) continue;
            if (renderer.bounds.center.x - back < SteeringFrom) continue;

            renderer.transform.SetParent(steering, true);
        }

        // Ön tekerlek direksiyonla birlikte çevriliyor ama kendi dönüşü modelin
        // ekseninde: eğik pivotun altına dönüşü sıfırlayan bir yuva giriyor, böylece iki
        // tekerlek de aynı yerel eksende (+Z) dönüyor ve `BikeWheels` tek eksenle
        // yetiniyor.
        var mount = new GameObject("FrontWheelMount").transform;
        mount.SetParent(steering, false);
        mount.position = frontHub;
        mount.rotation = model.transform.rotation;

        frontWheel = new GameObject("FrontWheel").transform;
        frontWheel.SetParent(mount, false);
        frontPart.SetParent(frontWheel, true);

        rearWheel = new GameObject("RearWheel").transform;
        rearWheel.SetParent(model.transform, false);
        rearWheel.localPosition = rearHub;
        rearPart.SetParent(rearWheel, true);
    }

    /// Tekerleğin dönme ekseni modelin genişlik ekseni. Ölçüm dünya uzayında yapılıyor:
    /// parça dönüşümlerinde yüz kat ölçek var ve mesh'in kendi uzayında milimetreler
    /// mikrona iniyor.
    static void RoundWheel(Transform part, Vector3 axisWorld, string label)
    {
        var filter = part.GetComponent<MeshFilter>();

        filter.sharedMesh = WheelRounding.Round(
            filter.sharedMesh, filter.transform, axisWorld, part.name, label);
    }

    static Transform FindPart(GameObject model, string name)
    {
        foreach (Transform child in model.GetComponentsInChildren<Transform>())
            if (child.name == name) return child;

        throw new System.InvalidOperationException($"[Bisiklet] parça yok: {name}");
    }

    /// Parçaları boyut ve konumla listeler. Hangi parçanın ne olduğu ancak böyle
    /// anlaşılıyor: tekerlek yüksek ve dar, sele küçük ve yukarıda, gidon önde.
    static void Report(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<MeshRenderer>();
        var report = new StringBuilder();

        Bounds whole = Measure(model);
        report.Append($"[Bisiklet] {renderers.Length} parça, toplam boyut "
                    + $"{whole.size.x:F2} x {whole.size.y:F2} x {whole.size.z:F2} m "
                    + $"(beklenen yükseklik {ExpectedHeight:F2} m)");

        int total = 0;

        for (int i = 0; i < renderers.Length; i++)
        {
            var filter = renderers[i].GetComponent<MeshFilter>();
            int triangles = filter != null && filter.sharedMesh != null
                ? filter.sharedMesh.triangles.Length / 3 : 0;
            total += triangles;

            Bounds b = renderers[i].bounds;
            Vector3 local = b.center - whole.min;

            report.Append($"\n  {renderers[i].name,-14} {triangles,7} üçgen   "
                        + $"boyut {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}   "
                        + $"merkez ön{local.x:F2} yük{local.y:F2} yan{local.z:F2}");
        }

        report.Append($"\n  TOPLAM {total} üçgen");
        Debug.Log(report.ToString());
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
