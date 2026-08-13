using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// BİSİKLETİ SAHNEYE KURAR. Model, materyal, ayar asset'i ve bileşenler koddan
/// bağlanıyor — sahne elle düzenlenmiyor (bkz. `CLAUDE.md`).
///
/// İçe aktarma ayarı burada DEĞİL: rig, ölçek ve okunabilirlik `ModelImportRules`'ta,
/// yani modelin her yenilenmesinde kendiliğinden uygulanıyor.
///
/// DOKU DOSYASI YOK. Modelde UV yok (FBX'te tek UV katmanı bile bulunmuyor); yüzey
/// `ToTheSummit/BikeSurface` ile nesne uzayında prosedürel boyanıyor.
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

    // BÖLGELİ PARÇALAR. Üretilen modelde farklı malzemeler aynı mesh'te: gidon ile
    // tutamak ve kablolar tek parça, bagaj ile zincir muhafazası ve pedal tek parça.
    // Sınırlar üçgen dağılımından ölçüldü (bkz. `MeshZones`).
    const string RackPart = "model_part10";
    const string PedalPart = "model_part11";

    /// Pedal gövdesinin başladığı yer: bisikletin orta düzleminden metre. Ölçüm: pedal
    /// gövdesi ile krank kolu ayrı kümeler, arada boşluk var. Kural iki pedal için de
    /// aynı — sağ pedal ayrı parça, sol pedal bagaj mesh'inin içinde.
    const float PedalFrom = 0.14f;

    /// Bagaj mesh'inde aktarma organları sınırı: parçanın kendi yüksekliğinin bu
    /// oranının altı. Ölçüm: alt bölgede 290 bin üçgen (muhafaza, krank, sol pedal),
    /// üstte 46 bin (bagaj tablası).
    const float DriveBelow = 0.49f;

    /// Direksiyonla dönen parçaların başladığı yer: modelin arka ucundan itibaren metre.
    /// Ön takımın tamamı (çatal, gidon, fren kolları, kablolar) bu eşiğin önünde duruyor.
    const float SteeringFrom = 1.20f;

    /// YÜZEY TAKIMI. Kadro boyası, krom, kauçuk, deri ve yağlı çelik. Hepsi aynı gölgelendirici, farklı ayar —
    /// malzemeyi ayıran şey renk değil, ışığa verdiği cevap: krom metalik ve fırça izli,
    /// deri yarı mat ve renkçe oynak.
    ///
    /// Lastiğin kendisi burada değil: tekerlek mesh'inin içinde ve ayrı materyal
    /// atanamıyor, gölgelendirici onu yarıçaptan ayırıyor (bkz. `WheelMaterial`).
    /// Kauçuk yine de var — gidon tutamağı ve pedal lastiği ayrı parça.
    static readonly (string Name, Color Colour, float Metallic, float Smoothness,
                     float Variation, float Grain, float Brushed,
                     float Dust, float Fade, float Grime)[] Surfaces =
    {
        ("Paint",   new Color(0.40f, 0.10f, 0.08f), 0.0f, 0.58f, 0.06f, 0.10f, 0.0f, 0.20f, 0.25f, 0.28f),
        ("Chrome",  new Color(0.60f, 0.61f, 0.63f), 0.9f, 0.64f, 0.03f, 0.12f, 0.7f, 0.18f, 0.04f, 0.34f),
        ("Leather", new Color(0.26f, 0.16f, 0.10f), 0.0f, 0.34f, 0.11f, 0.18f, 0.0f, 0.14f, 0.20f, 0.24f),
        ("Rubber",  new Color(0.07f, 0.07f, 0.08f), 0.0f, 0.22f, 0.04f, 0.10f, 0.0f, 0.16f, 0.06f, 0.30f),
        ("Steel",   new Color(0.13f, 0.13f, 0.14f), 0.85f, 0.32f, 0.05f, 0.14f, 0.4f, 0.20f, 0.03f, 0.55f),
    };

    /// PARÇA → YÜZEY. Eşleşme parça tablosundaki ölçüden çıkarıldı: konumu, boyu ve
    /// simetrik eşi olup olmadığı. Listede olmayan parça boyalı sayılıyor.
    ///
    /// Bu tablo GÖZLE DOĞRULANIR. Ölçü bir parçanın nerede durduğunu söylüyor, ne
    /// olduğunu söylemiyor; yanlış eşleşme görülünce burası düzeltilir.
    static readonly Dictionary<string, string> PartSurface = new Dictionary<string, string>
    {
        { "model_part0",  "Chrome"  },  // ön üstte kütle: far ve kablo demeti
        { "model_part1",  "Chrome"  },  // ön küçük parça
        { "model_part2",  "Chrome"  },  // fren kolu (sağ)
        { "model_part3",  "Chrome"  },  // fren kolu (sol)
        { "model_part4",  "Rubber"  },  // gidon tutamağı (sağ)
        { "model_part5",  "Rubber"  },  // gidon tutamağı (sol)
        { "model_part6",  "Chrome"  },  // ön fren pabucu (sağ)
        { "model_part7",  "Chrome"  },  // ön fren pabucu (sol)
        { "model_part8",  "Chrome"  },  // gidon
        { "model_part9",  "Paint"   },  // kadro üstü ince levha
        { "model_part10", "Paint"   },  // arka bagaj ve destekleri
        { "model_part11", "Chrome"  },  // krank ve pedal
        { "model_part12", "Steel"   },  // zincir — yağlı çelik, krom değil
        { "model_part13", "Chrome"  },  // arka çamurluk
        { "model_part15", "Chrome"  },  // çamurluk çubuğu
        { "model_part16", "Chrome"  },  // arka göbek yanı
        { "model_part17", "Chrome"  },  // arka uçtaki çubuk
        { "model_part18", "Leather" },  // sele
        { "model_part19", "Chrome"  },  // arka üstteki küçük parça
        { "model_part20", "Chrome"  },  // gidon boğazı
        { "model_part21", "Chrome"  },  // ön çatal kolu
        { "model_part22", "Chrome"  },  // ön çatal gövdesi
        { "model_part23", "Chrome"  },  // ön çamurluk
        { "model_part24", "Paint"   },  // kadro
    };

    [MenuItem("To The Summit/Model/Bisiklet/Sahneye Kur", false, 120)]
    static void Build()
    {
        Dictionary<string, Material> materials = BuildMaterials();
        if (materials.Count == 0) return;

        BikeSettings settings = LoadOrCreateSettings();
        Selection.activeGameObject = Place(materials, settings);
    }

    // --------------------------------------------------------------- materyal

    static Dictionary<string, Material> BuildMaterials()
    {
        var materials = new Dictionary<string, Material>();

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[Bisiklet] gölgelendirici bulunamadı: {ShaderName}");
            return materials;
        }

        foreach (var surface in Surfaces)
        {
            Material material = LoadOrCreate(shader, surface.Name);

            material.SetColor("_BaseColor", surface.Colour);
            material.SetFloat("_Metallic", surface.Metallic);
            material.SetFloat("_Smoothness", surface.Smoothness);
            material.SetFloat("_Variation", surface.Variation);
            material.SetFloat("_Grain", surface.Grain);
            material.SetFloat("_Brushed", surface.Brushed);
            material.SetFloat("_Dust", surface.Dust);
            material.SetFloat("_Fade", surface.Fade);
            material.SetFloat("_Grime", surface.Grime);
            material.SetFloat("_WheelMode", 0f);

            // ELLE BOYANAN KANALLARIN IŞIK DAVRANIŞI. Renk fırçadan seçiliyor ama
            // metaliklik ve parlaklık kanalın ne olduğundan çıkıyor: kauçuk mat, deri
            // yarı mat, çelik metalik. Fırçada üç kaydırıcı daha olsaydı her boyamada
            // üç karar daha verilirdi.
            material.SetFloat("_MaskRMetallic", 0f);
            material.SetFloat("_MaskRSmoothness", 0.22f);
            material.SetFloat("_MaskGMetallic", 0f);
            material.SetFloat("_MaskGSmoothness", 0.34f);
            material.SetFloat("_MaskBMetallic", 0.85f);
            material.SetFloat("_MaskBSmoothness", 0.32f);

            EditorUtility.SetDirty(material);
            materials[surface.Name] = material;
        }

        AssetDatabase.SaveAssets();
        return materials;
    }

    static Material LoadOrCreate(Shader shader, string name)
    {
        string path = $"{Folder}/Bicycle_{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        return material;
    }

    /// TEKERLEK MATERYALİ. Lastik, jant ve göbek tek mesh'te geldiği için ayrı materyal
    /// atanamıyor; ayrım gölgelendiricide yarıçaptan yapılıyor ve göbek ile dış yarıçap
    /// buradan veriliyor. İki tekerleğin ölçüsü farklı, o yüzden iki ayrı materyal.
    static Material WheelMaterial(string name, Transform space,
        Vector3 axisWorld, WheelProfile profile)
    {
        Shader shader = Shader.Find(ShaderName);
        Material material = LoadOrCreate(shader, name);

        // Göbek gölgelendiricinin okuduğu uzayda veriliyor: nesne uzayı, ölçekle
        // çarpılmış. Parça dönüşümünde yüz kat ölçek var; ham yerel konum verilseydi
        // göbek yarıçapın yüzde birinde kalır, bütün tekerlek lastik sayılırdı.
        Vector3 centre = space.InverseTransformPoint(profile.Centre) * space.lossyScale.x;

        material.SetFloat("_WheelMode", 1f);
        material.SetVector("_WheelCentre", centre);
        material.SetFloat("_WheelRadius", profile.Radius);

        // Eksen de nesne uzayında veriliyor: mesh verisi FBX'in Z-yukarı düzeninde,
        // Unity'nin Y-yukarı dönüşü parçanın transform'unda duruyor.
        material.SetVector("_WheelAxis",
            space.InverseTransformDirection(axisWorld).normalized);
        material.SetColor("_TireColor", new Color(0.07f, 0.07f, 0.08f));
        material.SetColor("_RimColor", new Color(0.58f, 0.59f, 0.61f));
        material.SetFloat("_Variation", 0.05f);
        material.SetFloat("_Grain", 0.10f);
        material.SetFloat("_Brushed", 0.5f);
        material.SetFloat("_Dust", 0.22f);
        material.SetFloat("_Fade", 0.04f);
        material.SetFloat("_Grime", 0.45f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
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

    static GameObject Place(Dictionary<string, Material> materials, BikeSettings settings)
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

        Paint(model, materials);
        Painted(model);
        Zone(model, materials);
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

    /// Tabloya göre materyal atar. Tabloda olmayan parça boyalı sayılıyor: eksik bir
    /// eşleşme atanmamış materyal bırakmasın.
    static void Paint(GameObject model, Dictionary<string, Material> materials)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            string surface = PartSurface.TryGetValue(renderer.name, out string named)
                ? named : "Paint";

            renderer.sharedMaterial = materials[surface];
        }
    }

    /// ELLE BOYANMIŞ KOPYALARI bağlar. Fırça, FBX'ten gelen mesh'e köşe rengi
    /// yazamadığı için parçanın kendi kopyasını üretiyor; kurulum o kopyayı bulmazsa
    /// boyama her "Sahneye Kur" ile düşerdi.
    static void Painted(GameObject model)
    {
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var copy = AssetDatabase.LoadAssetAtPath<Mesh>(
                $"{Folder}/Generated/{filter.name}_Paint.asset");

            if (copy != null) filter.sharedMesh = copy;
        }
    }

    /// BÖLGELİ PARÇALARA ayrı materyal bağlar. Gidon mesh'i bar, tutamak ve kablo
    /// olarak; bagaj mesh'i tabla ve aktarma organları olarak alt-mesh'lere ayrılıyor.
    ///
    /// Sonuç mesh'i dosyaya yazılıyor ve git'e girmiyor: üretilen varlık depoda durmaz.
    static void Zone(GameObject model, Dictionary<string, Material> materials)
    {
        // GİDON BÖLÜNMÜYOR. Tutamak ve kablo sınırı eşikle konuyordu ve tutamak bara
        // taşıyordu; sınır eşik sayısıyla tarif edilemiyor. Gidonun tamamı krom, kauçuk
        // olması gereken yerler ELLE boyanıyor (bkz. `VertexBrush`). Alt-mesh olarak
        // bırakılsaydı fırça o siyahı geri alamazdı — maske malzeme ekliyor, silmiyor.

        // PEDAL SINIRI DÜNYA UZAYINDA. İki pedal ayna simetrik ve ayrı mesh'lerde;
        // her mesh'in kendi ekseni farklı yöne bakıyor. Orta düzleme uzaklık ikisi için
        // de aynı kuralı veriyor: dışarıda kalan kütle pedal gövdesi, içeride kalan kol.
        float middle = Measure(model).center.z;

        var rack = FindPart(model, RackPart).GetComponent<MeshFilter>();
        Bounds carrier = rack.sharedMesh.bounds;
        Matrix4x4 toWorld = rack.transform.localToWorldMatrix;

        rack.sharedMesh = Zoned("Rack", () =>
            MeshZones.Build(rack.sharedMesh, point =>
            {
                if (MeshZones.Height(carrier, point) >= DriveBelow) return 0;

                float lateral = Mathf.Abs(toWorld.MultiplyPoint3x4(point).z - middle);
                return lateral > PedalFrom ? 2 : 1;
            }, 3, "Rack"));

        rack.GetComponent<Renderer>().sharedMaterials = new[]
        {
            materials["Paint"],    // bagaj tablası ve destekleri
            materials["Chrome"],   // zincir muhafazası ve krank kolu
            materials["Rubber"],   // sol pedal gövdesi
        };

        var pedal = FindPart(model, PedalPart).GetComponent<MeshFilter>();
        Matrix4x4 pedalToWorld = pedal.transform.localToWorldMatrix;

        pedal.sharedMesh = Zoned("Pedal", () =>
            MeshZones.Build(pedal.sharedMesh, point =>
                Mathf.Abs(pedalToWorld.MultiplyPoint3x4(point).z - middle) > PedalFrom ? 1 : 0,
                2, "Pedal"));

        pedal.GetComponent<Renderer>().sharedMaterials = new[]
        {
            materials["Chrome"],   // krank kolu ve mil
            materials["Rubber"],   // pedal gövdesi
        };
    }

    /// Bölgeli mesh'i dosyadan okur, yoksa üretir. VAR OLANIN ÜSTÜNE YAZILMIYOR: maske
    /// elle boyanıyor ve köşe renginde bu mesh'in içinde duruyor; her kurulumda yeniden
    /// üretilseydi boyama silinirdi. Bölge sınırı değiştirilecekse menüden sıfırlanıyor.
    static Mesh Zoned(string name, System.Func<Mesh> build)
    {
        const string folder = Folder + "/Generated";
        string path = $"{folder}/{name}_Zoned.asset";

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Folder, "Generated");

        Mesh mesh = build();
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();

        return mesh;
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
        // o kenardan uydurulan çemberin merkezine oturuyor.
        SetupWheel(frontPart, model.transform.forward, "Ön", "WheelFront");
        SetupWheel(rearPart, model.transform.forward, "Arka", "WheelRear");

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

    /// Tekerleği çembere oturtup kendi materyalini bağlar. Ölçüm dünya uzayında yapılıyor:
    /// parça dönüşümlerinde yüz kat ölçek var ve mesh'in kendi uzayında milimetreler
    /// mikrona iniyor.
    static void SetupWheel(Transform part, Vector3 axis, string label, string materialName)
    {
        var filter = part.GetComponent<MeshFilter>();

        filter.sharedMesh = WheelRounding.Round(
            filter.sharedMesh, filter.transform, axis, part.name, label);

        WheelProfile profile = WheelProfile.Measure(filter.sharedMesh, filter.transform, axis);
        part.GetComponent<Renderer>().sharedMaterial =
            WheelMaterial(materialName, filter.transform, axis, profile);
    }

    static Transform FindPart(GameObject model, string name)
    {
        foreach (Transform child in model.GetComponentsInChildren<Transform>())
            if (child.name == name) return child;

        throw new System.InvalidOperationException($"[Bisiklet] parça yok: {name}");
    }

    /// Parçaları boyut, konum ve atanan yüzeyle listeler. Hangi parçanın ne olduğu ancak
    /// böyle anlaşılıyor: tekerlek yüksek ve dar, sele küçük ve yukarıda, gidon önde.
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

            string surface = renderers[i].name == FrontWheelPart
                          || renderers[i].name == RearWheelPart ? "Wheel"
                : PartSurface.TryGetValue(renderers[i].name, out string named) ? named
                : "Paint";

            report.Append($"\n  {renderers[i].name,-14} {surface,-8} {triangles,7} üçgen   "
                        + $"boyut {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}   "
                        + $"merkez ön{local.x:F2} yük{local.y:F2} yan{local.z:F2}");
        }

        report.Append($"\n  TOPLAM {total} üçgen");
        ToolLog.Write(report.ToString());
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
