using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// BOOTSTRAPS BICYCLE IN SCENE. Model, materials, settings asset, and components wired
/// from code — scene is not edited manually (see CLAUDE.md).
///
/// Import settings handled in `ModelImportRules` (rig, scale, readability).
/// No texture files needed; surface uses procedural object-space shader `ToTheSummit/BikeSurface`.
public static class BikeBootstrap
{
    const string Folder = "Assets/Models/Bike";
    const string ModelPath = Folder + "/Bicycle.fbx";
    const string SettingsPath = "Assets/Settings/BikeSettings.asset";
    const string RoutePath = "Assets/Settings/MountainRoute.asset";
    const string ShaderName = "ToTheSummit/BikeSurface";

    const float ExpectedHeight = 1.20f;

    const string FrontWheelPart = "model_part25";
    const string RearWheelPart = "model_part14";
    const string HandlebarPart = "model_part8";
    const string SaddlePart = "model_part18";

    const string RackPart = "model_part10";
    const string PedalPart = "model_part11";

    const float PedalFrom = 0.14f;
    const float DriveBelow = 0.49f;
    const float SteeringFrom = 1.20f;

    static readonly (string Name, Color Colour, float Metallic, float Smoothness,
                     float Variation, float Grain, float Brushed,
                     float Dust, float Fade, float Grime)[] Surfaces =
    {
        ("Paint",   new Color(0.36f, 0.09f, 0.07f), 0.0f, 0.48f, 0.07f, 0.12f, 0.0f, 0.22f, 0.25f, 0.30f),
        ("Chrome",  new Color(0.55f, 0.56f, 0.58f), 0.85f, 0.50f, 0.04f, 0.14f, 0.8f, 0.22f, 0.04f, 0.38f),
        ("Leather", new Color(0.26f, 0.16f, 0.10f), 0.0f, 0.34f, 0.11f, 0.18f, 0.0f, 0.14f, 0.20f, 0.24f),
        ("Rubber",  new Color(0.07f, 0.07f, 0.08f), 0.0f, 0.22f, 0.04f, 0.10f, 0.0f, 0.16f, 0.06f, 0.30f),
        ("Steel",   new Color(0.13f, 0.13f, 0.14f), 0.85f, 0.32f, 0.05f, 0.14f, 0.4f, 0.20f, 0.03f, 0.55f),
    };

    static readonly Dictionary<string, string> PartSurface = new Dictionary<string, string>
    {
        { "model_part0",  "Chrome"  },  // headlight and cable cluster
        { "model_part1",  "Chrome"  },  // front small hardware
        { "model_part2",  "Chrome"  },  // brake lever (right)
        { "model_part3",  "Chrome"  },  // brake lever (left)
        { "model_part4",  "Rubber"  },  // handlebar grip (right)
        { "model_part5",  "Rubber"  },  // handlebar grip (left)
        { "model_part6",  "Chrome"  },  // front brake shoe (right)
        { "model_part7",  "Chrome"  },  // front brake shoe (left)
        { "model_part8",  "Chrome"  },  // handlebar
        { "model_part9",  "Paint"   },  // frame top plate
        { "model_part10", "Paint"   },  // rear rack and stays
        { "model_part11", "Chrome"  },  // crank and pedal
        { "model_part12", "Steel"   },  // chain
        { "model_part13", "Chrome"  },  // rear fender
        { "model_part15", "Chrome"  },  // fender stay
        { "model_part16", "Chrome"  },  // rear hub flange
        { "model_part17", "Chrome"  },  // rear stay rod
        { "model_part18", "Leather" },  // saddle
        { "model_part19", "Chrome"  },  // rear upper hardware
        { "model_part20", "Chrome"  },  // stem
        { "model_part21", "Chrome"  },  // front fork blade
        { "model_part22", "Chrome"  },  // front fork crown
        { "model_part23", "Chrome"  },  // front fender
        { "model_part24", "Paint"   },  // frame
    };

    [InitializeOnLoadMethod]
    static void SyncMaterials()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) return;

        foreach (var surface in Surfaces)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                $"{Folder}/Bicycle_{surface.Name}.mat");

            if (material == null) continue;

            bool changed = false;
            changed |= Set(material, "_BaseColor", surface.Colour);
            changed |= Set(material, "_Metallic", surface.Metallic);
            changed |= Set(material, "_Smoothness", surface.Smoothness);
            changed |= Set(material, "_Variation", surface.Variation);
            changed |= Set(material, "_Grain", surface.Grain);
            changed |= Set(material, "_Brushed", surface.Brushed);
            changed |= Set(material, "_Dust", surface.Dust);
            changed |= Set(material, "_Fade", surface.Fade);
            changed |= Set(material, "_Grime", surface.Grime);

            if (changed) EditorUtility.SetDirty(material);
        }
    }

    static bool Set(Material material, string property, float value)
    {
        if (Mathf.Approximately(material.GetFloat(property), value)) return false;

        material.SetFloat(property, value);
        return true;
    }

    static bool Set(Material material, string property, Color value)
    {
        if (material.GetColor(property) == value) return false;

        material.SetColor(property, value);
        return true;
    }

    [MenuItem("To The Summit/Model/Bicycle/Set Up In Scene", false, 120)]
    static void Build()
    {
        Dictionary<string, Material> materials = BuildMaterials();
        if (materials.Count == 0) return;

        BikeSettings settings = LoadOrCreateSettings();
        Selection.activeGameObject = Place(materials, settings);
    }

    static Dictionary<string, Material> BuildMaterials()
    {
        var materials = new Dictionary<string, Material>();

        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"[Bicycle] Shader not found: {ShaderName}");
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

    static Material WheelMaterial(string name, Transform space,
        Vector3 axisWorld, WheelProfile profile)
    {
        Shader shader = Shader.Find(ShaderName);
        Material material = LoadOrCreate(shader, name);

        Vector3 centre = space.InverseTransformPoint(profile.Centre) * space.lossyScale.x;

        material.SetFloat("_WheelMode", 1f);
        material.SetVector("_WheelCentre", centre);
        material.SetFloat("_WheelRadius", profile.Radius);
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

    static GameObject Place(Dictionary<string, Material> materials, BikeSettings settings)
    {
        Selection.activeGameObject = null;

        var existing = Object.FindAnyObjectByType<BikeController>();
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (prefab == null)
        {
            Debug.LogError("[Bicycle] Model could not be imported.");
            return null;
        }

        var root = new GameObject("Bicycle");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);

        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction);

        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        Paint(model, materials);
        Painted(model);
        Zone(model, materials);
        Report(model);

        Rig(model, out Transform steering, out Transform frontWheel, out Transform rearWheel);

        model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

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

        root.AddComponent<BikeWheels>().Bind(bike, settings, frontWheel, rearWheel, Vector3.forward);
        root.AddComponent<BikeSteeringVisual>().Bind(bike, steering);

        var input = root.GetComponent<BikePlayerInput>();
        Ride(bike, input, model);

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        root.transform.position = SpawnPoint();

        Bounds placed = Measure(model);
        var down = new Ray(placed.center + Vector3.up * 2f, Vector3.down);

        if (Physics.Raycast(down, out RaycastHit ground, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            float gap = placed.min.y - ground.point.y;
            root.transform.position -= Vector3.up * gap;

            ToolLog.Write($"[Bicycle] Ground gap measured at {gap * 1000f:F0} mm, "
                        + "closed; wheels resting on ground.");
        }

        Undo.RegisterCreatedObjectUndo(root, "Set up bicycle");
        return root;
    }

    static void Ride(BikeController bike, BikePlayerInput input, GameObject model)
    {
        var walker = Object.FindAnyObjectByType<FirstPersonController>();
        if (walker == null)
        {
            Debug.LogWarning("[Bicycle] No player in scene; riding not bound.");
            return;
        }

        var look = walker.GetComponent<MouseLook>();
        var body = walker.GetComponent<CharacterController>();
        var camera = walker.GetComponentInChildren<Camera>();

        if (look == null || body == null || camera == null)
        {
            Debug.LogWarning("[Bicycle] Player missing look, collider, or camera.");
            return;
        }

        Transform saddle = FindPart(model, SaddlePart);
        Vector3 seat = bike.transform.InverseTransformPoint(
            saddle.GetComponent<Renderer>().bounds.center);

        var rider = walker.GetComponent<BikeRider>();
        if (rider == null) rider = walker.gameObject.AddComponent<BikeRider>();

        // MouseLook owns pitch on the camera's parent pivot. Passing the camera child here
        // made riding write pitch to a different transform and conflicted with view motion.
        rider.Bind(walker, look, body, camera.transform.parent, bike, input, seat);
        EditorUtility.SetDirty(rider);
    }

    static void Paint(GameObject model, Dictionary<string, Material> materials)
    {
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            string surface = PartSurface.TryGetValue(renderer.name, out string named)
                ? named : "Paint";

            renderer.sharedMaterial = materials[surface];
        }
    }

    static void Painted(GameObject model)
    {
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var copy = AssetDatabase.LoadAssetAtPath<Mesh>(
                $"{Folder}/Generated/{filter.name}_Paint.asset");

            if (copy != null) filter.sharedMesh = copy;
        }
    }

    static void Zone(GameObject model, Dictionary<string, Material> materials)
    {
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
            materials["Paint"],
            materials["Chrome"],
            materials["Rubber"],
        };

        var pedal = FindPart(model, PedalPart).GetComponent<MeshFilter>();
        Matrix4x4 pedalToWorld = pedal.transform.localToWorldMatrix;

        pedal.sharedMesh = Zoned("Pedal", () =>
            MeshZones.Build(pedal.sharedMesh, point =>
                Mathf.Abs(pedalToWorld.MultiplyPoint3x4(point).z - middle) > PedalFrom ? 1 : 0,
                2, "Pedal"));

        pedal.GetComponent<Renderer>().sharedMaterials = new[]
        {
            materials["Chrome"],
            materials["Rubber"],
        };
    }

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

    static void Rig(GameObject model, out Transform steering,
        out Transform frontWheel, out Transform rearWheel)
    {
        Transform frontPart = FindPart(model, FrontWheelPart);
        Transform rearPart = FindPart(model, RearWheelPart);
        Transform barPart = FindPart(model, HandlebarPart);

        SetupWheel(frontPart, model.transform.forward, "Front", "WheelFront");
        SetupWheel(rearPart, model.transform.forward, "Rear", "WheelRear");

        Vector3 frontHub = frontPart.GetComponent<Renderer>().bounds.center;
        Vector3 rearHub = rearPart.GetComponent<Renderer>().bounds.center;
        Vector3 bar = barPart.GetComponent<Renderer>().bounds.center;

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

        throw new System.InvalidOperationException($"[Bicycle] Part not found: {name}");
    }

    static void Report(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<MeshRenderer>();
        var report = new StringBuilder();

        Bounds whole = Measure(model);
        report.Append($"[Bicycle] {renderers.Length} parts, total bounds "
                    + $"{whole.size.x:F2} x {whole.size.y:F2} x {whole.size.z:F2} m "
                    + $"(expected height {ExpectedHeight:F2} m)");

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

            report.Append($"\n  {renderers[i].name,-14} {surface,-8} {triangles,7} tri   "
                        + $"size {b.size.x:F2} x {b.size.y:F2} x {b.size.z:F2}   "
                        + $"center fwd{local.x:F2} up{local.y:F2} side{local.z:F2}");
        }

        report.Append($"\n  TOTAL {total} triangles");
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
