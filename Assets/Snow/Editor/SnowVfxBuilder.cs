// Generates VFX Graph assets for the snow subsystem via code (spec Phases 8, 9, 13).
// Invoked by: Menu.

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SnowVfxBuilder
{
    const string Folder = "Assets/Snow/VFX";
    const string EditorAsm = "Unity.VisualEffectGraph.Editor";

    [MenuItem("To The Summit/Snow/Generate VFX Graphs", false, 62)]
    static void BuildAll()
    {
        var r = new StringBuilder();
        r.AppendLine("# Snow — Generating VFX Graphs");

        try
        {
            BuildSnowfall(r);
            BuildPuff(r);
            BuildSpray(r);
            BuildSpindrift(r);
            BuildCurtain(r);
        }
        catch (Exception e)
        {
            Debug.LogError(r + "\nBUILD HALTED: " + e.GetType().Name + " — " + e.Message +
                           "\n" + e.StackTrace);
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(r.ToString());
    }

    // ------------------------------------------------------------ VFX_Snowfall

    static void BuildSnowfall(StringBuilder r)
    {
        object graph = NewGraph("VFX_Snowfall", r);

        object spawner = AddContext(graph, "VFXBasicSpawner", new Vector2(0, 0), r);
        object rate = AddBlock(spawner, "VFXSpawnerConstantRate", r);

        object rateParam = AddParameter(graph, "SpawnRate", typeof(float), 0f,
                                        new Vector2(-300, 0), r);
        LinkParameter(rateParam, rate, "Rate", r);

        object init = AddContext(graph, "VFXBasicInitialize", new Vector2(0, 200), r);

        SetSetting(init, "capacity", 120000u, r);
        SetWorldSpace(init, r);
        SetBounds(init, new Vector3(60f, 42f, 60f), r);

        object pos = AddBlock(init, "Block.PositionShape", r);
        SetSetting(pos, "shape", "OrientedBox", r);
        SetSetting(pos, "positionMode", "Volume", r);
        SetSlotField(pos, "Box", "size", new Vector3(40f, 26f, 40f), r);

        object life = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(life, "attribute", "lifetime", r);
        SetSetting(life, "Random", "Uniform", r);
        SetSlot(life, "A", 4f, r);
        SetSlot(life, "B", 9f, r);

        object size = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(size, "attribute", "size", r);
        SetSetting(size, "Random", "Uniform", r);
        SetSlot(size, "A", 0.018f * 0.6f, r);
        SetSlot(size, "B", 0.018f * 1.7f, r);

        object vel = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(vel, "attribute", "velocity", r);
        SetSetting(vel, "Random", "Uniform", r);
        SetSlotField(vel, "A", "vector", new Vector3(0f, -0.6f, 0f), r);
        SetSlotField(vel, "B", "vector", new Vector3(0f, -1.4f, 0f), r);

        object update = AddContext(graph, "VFXBasicUpdate", new Vector2(0, 500), r);

        AddBlock(update, "Block.Gravity", r);

        object turb = AddBlock(update, "Block.Turbulence", r);
        SetSetting(turb, "Mode", "Absolute", r);

        object turbParam = AddParameter(graph, "TurbulenceIntensity", typeof(float),
                                        0.15f, new Vector2(-300, 500), r);
        LinkParameter(turbParam, turb, "Intensity", r);
        SetSlot(turb, "frequency", 0.12f, r);
        SetSlot(turb, "octaves", 2, r);

        object windForce = AddBlock(update, "Block.Force", r);
        SetSetting(windForce, "Mode", "Absolute", r);

        object windParam = AddParameter(graph, "WindForce", typeof(Vector3),
                                        Vector3.zero, new Vector2(-300, 620), r);
        LinkParameter(windParam, windForce, "Force", r);

        object drag = AddBlock(update, "Block.Drag", r);
        SetSlot(drag, "dragCoefficient", 9.81f, r);

        object output = AddContext(graph, "URP.VFXURPLitPlanarPrimitiveOutput",
                                   new Vector2(0, 800), r);

        SetSetting(output, "blendMode", "Alpha", r);
        SetSetting(output, "zWriteMode", "Off", r);
        SetSetting(output, "useSoftParticle", true, r);

        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Snow/Textures/T_Flake_Atlas.png");

        if (atlas != null)
        {
            SetSetting(output, "uvMode", "Flipbook", r);
            SetSlot(output, "baseColorMap", atlas, r);
            SetSlotField(output, "flipBookSize", "x", 4, r);
            SetSlotField(output, "flipBookSize", "y", 4, r);

            object texIdx = AddBlock(init, "Block.SetAttribute", r);
            SetSetting(texIdx, "attribute", "texIndex", r);
            SetSetting(texIdx, "Random", "Uniform", r);
            SetSlot(texIdx, "A", 0f, r);
            SetSlot(texIdx, "B", 15.999f, r);
        }
        else
        {
            r.AppendLine("           [!] T_Flake_Atlas missing — keeping DefaultDot");
        }

        // The URP lit output already receives the main light, shadows and environment.
        // An emissive copy of the sun remained visible in terrain/cloud shadow and made flakes
        // look self-lit, so the graph deliberately has no emissive channel.
        SetSetting(output, "useEmissive", false, r);

        SetSlot(output, "smoothness", 0.2f, r);
        SetSlot(output, "metallic", 0f, r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

        object flutter = AddBlock(output, "Block.CustomHLSL", r);
        SetSetting(flutter, "m_BlockName", "Flutter", r);
        SetSetting(flutter, "m_HLSLCode", FlutterHlsl, r);

        object minPx = AddBlock(output, "Block.CustomHLSL", r);
        SetSetting(minPx, "m_BlockName", "Min Screen Size", r);
        SetSetting(minPx, "m_HLSLCode", MinScreenSizeHlsl, r);

        object fade = AddBlock(update, "Block.CustomHLSL", r);
        SetSetting(fade, "m_BlockName", "Lifetime Fade and Ground Clip", r);
        SetSetting(fade, "m_HLSLCode", FadeAndGroundHlsl, r);

        object groundParam = AddParameter(graph, "GroundY", typeof(float), 0f,
                                          new Vector2(-300, 700), r);
        LinkParameter(groundParam, fade, "_groundY", r);

        Link(spawner, init, r);
        Link(init, update, r);
        Link(update, output, r);

        Save(graph, r);
    }

    // ------------------------------------------------------------- common skeleton

    static void SetBounds(object init, Vector3 size, StringBuilder r)
    {
        SetSlotField(init, "bounds", "center", Vector3.zero, r);
        SetSlotField(init, "bounds", "size", size, r);
    }

    static (object init, object update, object output) Skeleton(
        object graph, uint capacity, float lifeA, float lifeB,
        float sizeA, float sizeB, Vector3 boundsSize, StringBuilder r)
    {
        object spawner = AddContext(graph, "VFXBasicSpawner", new Vector2(0, 0), r);
        object rate = AddBlock(spawner, "VFXSpawnerConstantRate", r);

        object rateParam = AddParameter(graph, "SpawnRate", typeof(float), 0f,
                                        new Vector2(-300, 0), r);
        LinkParameter(rateParam, rate, "Rate", r);

        object init = AddContext(graph, "VFXBasicInitialize", new Vector2(0, 200), r);
        SetSetting(init, "capacity", capacity, r);
        SetWorldSpace(init, r);
        SetBounds(init, boundsSize, r);

        object life = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(life, "attribute", "lifetime", r);
        SetSetting(life, "Random", "Uniform", r);
        SetSlot(life, "A", lifeA, r);
        SetSlot(life, "B", lifeB, r);

        object size = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(size, "attribute", "size", r);
        SetSetting(size, "Random", "Uniform", r);
        SetSlot(size, "A", sizeA, r);
        SetSlot(size, "B", sizeB, r);

        object update = AddContext(graph, "VFXBasicUpdate", new Vector2(0, 500), r);

        object output = AddContext(graph, "URP.VFXURPLitPlanarPrimitiveOutput",
                                   new Vector2(0, 800), r);
        SetSetting(output, "blendMode", "Alpha", r);
        SetSetting(output, "zWriteMode", "Off", r);

        Link(spawner, init, r);
        Link(init, update, r);
        Link(update, output, r);

        return (init, update, output);
    }

    // ------------------------------------------------------------- VFX_SnowPuff

    static void BuildPuff(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowPuff", r);

        var (init, update, output) =
            Skeleton(graph, 512, 0.4f, 0.9f, 0.02f, 0.06f,
                     new Vector3(6f, 6f, 6f), r);

        AddBlock(update, "Block.Gravity", r);
        AddBlock(update, "Block.Drag", r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

        Save(graph, r);
    }

    // ------------------------------------------------------------ VFX_SnowSpray

    static void BuildSpray(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowSpray", r);

        var (init, update, output) =
            Skeleton(graph, 3000, 0.5f, 1.1f, 0.03f, 0.10f,
                     new Vector3(10f, 8f, 10f), r);

        AddBlock(update, "Block.Gravity", r);

        object drag = AddBlock(update, "Block.Drag", r);
        SetSlot(drag, "dragCoefficient", 2.5f, r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

        Save(graph, r);
    }

    // ------------------------------------------------------------ VFX_Spindrift

    static void BuildSpindrift(StringBuilder r)
    {
        object graph = NewGraph("VFX_Spindrift", r);

        var (init, update, output) =
            Skeleton(graph, 8000, 1.2f, 3.0f, 0.01f, 0.03f,
                     new Vector3(80f, 10f, 80f), r);

        object spinPos = AddBlock(init, "Block.PositionShape", r);
        SetSetting(spinPos, "shape", "OrientedBox", r);
        SetSetting(spinPos, "positionMode", "Volume", r);
        SetSlotField(spinPos, "Box", "size", new Vector3(30f, 0.05f, 30f), r);

        object spinVel = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(spinVel, "attribute", "velocity", r);
        SetSetting(spinVel, "Random", "Uniform", r);
        SetSlotField(spinVel, "A", "vector", new Vector3(0f, 0.2f, 0f), r);
        SetSlotField(spinVel, "B", "vector", new Vector3(0f, 0.8f, 0f), r);

        object spinWind = AddBlock(update, "Block.Force", r);
        SetSetting(spinWind, "Mode", "Absolute", r);

        object spinWindParam = AddParameter(graph, "WindForce", typeof(Vector3),
                                            Vector3.zero, new Vector2(-300, 520), r);
        LinkParameter(spinWindParam, spinWind, "Force", r);

        object spinDrag = AddBlock(update, "Block.Drag", r);
        SetSlot(spinDrag, "dragCoefficient", 4f, r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "AlongVelocity", r);

        Save(graph, r);
    }

    // ----------------------------------------------------------- VFX_SnowCurtain

    static void BuildCurtain(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowCurtain", r);

        var (init, update, output) =
            Skeleton(graph, 14, 6f, 12f, 12f, 25f,
                     new Vector3(400f, 80f, 400f), r);

        object curPos = AddBlock(init, "Block.PositionShape", r);
        SetSetting(curPos, "shape", "OrientedBox", r);
        SetSetting(curPos, "positionMode", "Volume", r);
        SetSlotField(curPos, "Box", "size", new Vector3(80f, 5f, 80f), r);

        object curWind = AddBlock(update, "Block.Force", r);
        SetSetting(curWind, "Mode", "Absolute", r);

        object curWindParam = AddParameter(graph, "WindForce", typeof(Vector3),
                                           Vector3.zero, new Vector2(-300, 520), r);
        LinkParameter(curWindParam, curWind, "Force", r);

        object curDrag = AddBlock(update, "Block.Drag", r);
        SetSlot(curDrag, "dragCoefficient", 3f, r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "AlongVelocity", r);

        Save(graph, r);
    }

    // -------------------------------------------------------------- reflection

    static Assembly asm;

    static Assembly Asm => asm ??= AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == EditorAsm)
        ?? throw new InvalidOperationException(EditorAsm + " is not loaded.");

    const string MinScreenSizeHlsl =
@"void SnowMinScreenSize(inout VFXAttributes attributes)
{
    const float minPixelSize = 1.3f;

    float clipPosW = TransformPositionVFXToClip(attributes.position).w;

    float denom = attributes.size * 0.5f
                * min(abs(UNITY_MATRIX_P[0][0] * _ScreenParams.x),
                      abs(UNITY_MATRIX_P[1][1] * _ScreenParams.y));

    float2 newScale = (float2(minPixelSize, minPixelSize) * clipPosW) / max(denom, 1e-6f);

    float2 prevScale = float2(attributes.scaleX, attributes.scaleY);
    float2 expanded = max(prevScale, newScale);

    float areaRatio = (expanded.x * expanded.y) / max(prevScale.x * prevScale.y, 1e-6f);
    attributes.alpha *= saturate(1.0f / max(areaRatio, 1.0f));

    attributes.scaleX = expanded.x;
    attributes.scaleY = expanded.y;
}";

    const string FlutterHlsl =
@"void SnowFlakeFlutter(inout VFXAttributes attributes)
{
    float phase = frac(sin(float(attributes.particleId) * 12.9898) * 43758.5453) * 6.2831853;

    attributes.position += float3(-cos(attributes.age * 5.5 + phase) * (0.35 / 5.5),
                                   0.0,
                                   sin(attributes.age * 4.6 + phase) * (0.35 / 4.6));
}";

    const string FadeAndGroundHlsl =
@"void SnowFlakeFadeAndKill(inout VFXAttributes attributes, in float groundY)
{
    float t = attributes.age / max(attributes.lifetime, 1e-4);

    float fadeIn  = smoothstep(0.0, 0.08, t);
    float fadeOut = 1.0 - smoothstep(0.92, 1.0, t);

    attributes.alpha = fadeIn * fadeOut;

    if (attributes.position.y < groundY + 0.02)
        attributes.alive = false;
}";

    static Type Find(string shortName)
    {
        string full =
            shortName.StartsWith("Block.") ? "UnityEditor.VFX.Block." + shortName.Substring(6) :
            shortName.StartsWith("URP.")   ? "UnityEditor.VFX.URP." + shortName.Substring(4) :
                                             "UnityEditor.VFX." + shortName;

        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = a.GetType(full, false);
            if (t != null) return t;
        }

        throw new InvalidOperationException("VFX type not found: " + full);
    }

    // ------------------------------------------------------------ setting / slot

    static void SetSetting(object model, string name, object value, StringBuilder r)
    {
        MethodInfo getSetting = model.GetType().GetMethod("GetSetting",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string) }, null)
            ?? throw new InvalidOperationException("GetSetting missing.");

        object setting = getSetting.Invoke(model, new object[] { name });

        FieldInfo field = setting?.GetType()
            .GetField("field", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(setting) as FieldInfo
            ?? throw new InvalidOperationException(
                model.GetType().Name + " setting missing: " + name);

        object val = value is string str && field.FieldType.IsEnum
            ? Enum.Parse(field.FieldType, str)
            : Convert.ChangeType(value, field.FieldType);

        MethodInfo set = model.GetType().GetMethod("SetSettingValue",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string), typeof(object) }, null)
            ?? throw new InvalidOperationException("SetSettingValue missing.");

        set.Invoke(model, new[] { name, val });
        r.AppendLine("           setting " + name.PadRight(24) + val);
    }

    static void SetSlot(object model, string slotName, object value, StringBuilder r)
    {
        MethodInfo nb = model.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = model.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        if (nb == null || get == null)
            throw new InvalidOperationException(model.GetType().Name + " has no slots.");

        int n = (int)nb.Invoke(model, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(model, new object[] { i });
            if (Prop(slot, "name")?.GetValue(slot) as string != slotName) continue;

            PropertyInfo valProp = Prop(slot, "value")
                ?? throw new InvalidOperationException("Cannot write slot value.");

            valProp.SetValue(slot, value);

            object readBack = valProp.GetValue(slot);

            if (!Equals(readBack, value))
                throw new InvalidOperationException(
                    model.GetType().Name + "." + slotName + " could not be written: " +
                    "passed " + value.GetType().Name + " = " + value +
                    ", slot has " + (readBack?.GetType().Name ?? "null") + " = " + readBack +
                    ". Slot type differs — use SetSlotField to write sub-fields.");

            r.AppendLine("           slot    " + slotName.PadRight(24) + value);
            return;
        }

        throw new InvalidOperationException(
            model.GetType().Name + " has no slot: " + slotName);
    }

    static object NewGraph(string name, StringBuilder r)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Snow", "VFX");

        string path = Folder + "/" + name + ".vfx";

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            AssetDatabase.DeleteAsset(path);

        Type util = Asm.GetType("UnityEditor.VisualEffectAssetEditorUtility", false)
            ?? throw new InvalidOperationException("VisualEffectAssetEditorUtility missing.");

        MethodInfo create = util.GetMethod("CreateNewAsset",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateNewAsset missing.");

        create.Invoke(null, new object[] { path });

        Type resType = Asm.GetType("UnityEditor.VFX.VisualEffectResource", false)
            ?? Type.GetType("UnityEditor.VFX.VisualEffectResource, UnityEditor")
            ?? throw new InvalidOperationException("VisualEffectResource missing.");

        MethodInfo atPath = resType.GetMethod("GetResourceAtPath",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetResourceAtPath missing.");

        object resource = atPath.Invoke(null, new object[] { path })
            ?? throw new InvalidOperationException("Could not read resource: " + path);

        MethodInfo getGraph = Asm.GetType("UnityEditor.VFX.VFXGraphExtension", false)
            ?.GetMethod("GetOrCreateGraph", BindingFlags.Public | BindingFlags.Static);

        if (getGraph == null)
            getGraph = Asm.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(m => m.Name == "GetOrCreateGraph");

        if (getGraph == null)
            throw new InvalidOperationException("GetOrCreateGraph not found.");

        object graph = getGraph.Invoke(null, new[] { resource })
            ?? throw new InvalidOperationException("Could not create graph.");

        r.AppendLine("  [+] " + name + " created — " + path);
        return graph;
    }

    static object AddContext(object graph, string typeName, Vector2 pos, StringBuilder r)
    {
        Type t = Find(typeName);
        var ctx = ScriptableObject.CreateInstance(t)
            ?? throw new InvalidOperationException("Could not instantiate " + typeName);

        SetPosition(ctx, pos);
        AddChild(graph, ctx);

        r.AppendLine("      context " + typeName);
        return ctx;
    }

    static object AddBlock(object context, string typeName, StringBuilder r)
    {
        Type t = Find(typeName);
        var block = ScriptableObject.CreateInstance(t)
            ?? throw new InvalidOperationException("Could not instantiate " + typeName);

        AddChild(context, block);
        r.AppendLine("        block " + typeName);

        return block;
    }

    static bool Dump;

    [MenuItem("To The Summit/Snow/Generate VFX Graphs (dump)", false, 63)]
    static void BuildAllWithDump()
    {
        Dump = true;
        try { BuildAll(); }
        finally { Dump = false; }
    }

    static void DumpModel(object model, StringBuilder r)
    {
        MethodInfo getSettings = model.GetType().GetMethod("GetSettings",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool), typeof(object) }, null);

        var fields = model.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.GetCustomAttributes()
                         .Any(a => a.GetType().Name.StartsWith("VFXSetting")))
            .ToArray();

        foreach (FieldInfo f in fields)
            r.AppendLine("           setting " + f.Name.PadRight(28) +
                         f.FieldType.Name + " = " + (f.GetValue(model) ?? "null"));

        MethodInfo nb = model.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = model.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        if (nb == null || get == null) return;

        int n = (int)nb.Invoke(model, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(model, new object[] { i });
            if (slot == null) continue;

            string name = Prop(slot, "name")?.GetValue(slot) as string;
            object val = Prop(slot, "value")?.GetValue(slot);

            r.AppendLine("           slot    " + (name ?? "?").PadRight(28) +
                         (val == null ? "null" : val.GetType().Name + " = " + val));
        }
    }

    static void AddChild(object parent, object child)
    {
        MethodInfo add = parent.GetType().GetMethod("AddChild",
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("AddChild missing: " + parent.GetType().Name);

        add.Invoke(parent, new object[] { child, -1, true });
    }

    static void SetPosition(object model, Vector2 pos)
    {
        PropertyInfo p = model.GetType().GetProperty("position",
            BindingFlags.Public | BindingFlags.Instance);

        p?.SetValue(model, pos);
    }

    static void Link(object from, object to, StringBuilder r)
    {
        MethodInfo link = from.GetType().GetMethod("LinkTo",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { Find("VFXContext"), typeof(int), typeof(int) }, null)
            ?? throw new InvalidOperationException("LinkTo missing.");

        link.Invoke(from, new object[] { to, 0, 0 });
        r.AppendLine("      link    " + from.GetType().Name + " -> " + to.GetType().Name);
    }

    static object AddParameter(object graph, string name, Type type,
                               object value, Vector2 pos, StringBuilder r)
    {
        Type pt = Find("VFXParameter");

        var param = ScriptableObject.CreateInstance(pt)
            ?? throw new InvalidOperationException("Could not instantiate VFXParameter.");

        MethodInfo init = pt.GetMethod("Init",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(Type) }, null)
            ?? throw new InvalidOperationException("VFXParameter.Init missing.");

        init.Invoke(param, new object[] { type });

        SetPosition(param, pos);
        AddChild(graph, param);

        SetSetting(param, "m_ExposedName", name, r);
        SetSetting(param, "m_Exposed", true, r);

        MethodInfo getOut = pt.GetMethod("GetOutputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        if (getOut?.Invoke(param, new object[] { 0 }) is object outSlot)
            Prop(outSlot, "value")?.SetValue(outSlot, value);

        r.AppendLine("      param   " + name.PadRight(24) + type.Name + " = " + value);
        return param;
    }

    static void LinkParameter(object param, object target, string slotName,
                              StringBuilder r)
    {
        object outSlot = param.GetType().GetMethod("GetOutputSlot",
            BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(param, new object[] { 0 })
            ?? throw new InvalidOperationException("Parameter has no output slot.");

        MethodInfo nb = target.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = target.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        int n = (int)nb.Invoke(target, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(target, new object[] { i });
            if (Prop(slot, "name")?.GetValue(slot) as string != slotName) continue;

            MethodInfo link = outSlot.GetType().GetMethod("Link",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { outSlot.GetType(), typeof(bool) }, null)
                ?? throw new InvalidOperationException("VFXSlot.Link missing.");

            bool ok = (bool)link.Invoke(outSlot, new object[] { slot, true });

            r.AppendLine("      link    param -> " + target.GetType().Name +
                         "." + slotName + (ok ? "" : "  FAILED"));

            if (!ok) throw new InvalidOperationException(
                "Could not link parameter: " + slotName);

            return;
        }

        throw new InvalidOperationException(target.GetType().Name + " has no slot: " + slotName);
    }

    static void SetSlotField(object model, string slotName, string fieldName,
                             object value, StringBuilder r)
    {
        MethodInfo nb = model.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = model.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        int n = (int)nb.Invoke(model, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(model, new object[] { i });
            if (Prop(slot, "name")?.GetValue(slot) as string != slotName) continue;

            PropertyInfo valProp = Prop(slot, "value");
            object box = valProp.GetValue(slot);

            FieldInfo f = box.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    box.GetType().Name + " has no field: " + fieldName);

            f.SetValue(box, value);
            valProp.SetValue(slot, box);

            r.AppendLine("           slot    " + (slotName + "." + fieldName).PadRight(24) + value);
            return;
        }

        throw new InvalidOperationException(model.GetType().Name + " has no slot: " + slotName);
    }

    static PropertyInfo Prop(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        {
            PropertyInfo p = t.GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (p != null) return p;
        }

        return null;
    }

    static void DumpGraph(object graph, StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Final State");

        if (Prop(graph, "children")?.GetValue(graph) is not System.Collections.IEnumerable ctxs)
        {
            r.AppendLine("  [!] could not read graph children");
            return;
        }

        foreach (object ctx in ctxs)
        {
            r.AppendLine("  context " + ctx.GetType().Name);
            DumpModel(ctx, r);

            if (Prop(ctx, "children")?.GetValue(ctx) is not System.Collections.IEnumerable blocks)
                continue;

            foreach (object b in blocks)
            {
                r.AppendLine("    block " + b.GetType().Name);
                DumpModel(b, r);
            }
        }
    }

    static void SetWorldSpace(object init, StringBuilder r)
    {
        MethodInfo getData = init.GetType().GetMethod("GetData",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("GetData missing.");

        object data = getData.Invoke(init, null)
            ?? throw new InvalidOperationException("Particle data missing.");

        PropertyInfo spaceProp = data.GetType().GetProperty("space",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                data.GetType().Name + " has no `space` property.");

        object worldSpace = Enum.Parse(spaceProp.PropertyType, "World");
        spaceProp.SetValue(data, worldSpace);

        object readBack = spaceProp.GetValue(data);
        if (!Equals(readBack, worldSpace))
            throw new InvalidOperationException("Could not set space: " + readBack);

        r.AppendLine("           space   World");
    }

    static void Save(object graph, StringBuilder r)
    {
        if (Dump) DumpGraph(graph, r);

        foreach (string name in new[] { "UpdateSubAssets", "OnSaved" })
        {
            MethodInfo m = graph.GetType().GetMethod(name,
                BindingFlags.Public | BindingFlags.Instance);

            m?.Invoke(graph, null);
        }

        EditorUtility.SetDirty((UnityEngine.Object)graph);
        r.AppendLine("  [+] saved");
    }
}
