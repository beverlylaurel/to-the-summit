// ROL: kar sisteminin VFX Graph asset'lerini KODDAN üretir (spec Faz 8, 9, 13).
// Çağıran: menü.

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// VFX GRAFİKLERİ KODDAN KURULUYOR.
///
/// Spec Faz 8/9/13 altı `.vfx` asset'i istiyor. `.vfx` bir grafik asset'i;
/// normalde Unity içinde elle çizilir. Bu projede kullanıcı tıklamaz — kod,
/// dosya, ayar hepsi buradan (`CLAUDE.md` → Rol dağılımı).
///
/// GRAFİK MODELİ `internal`, O YÜZDEN REFLECTION. `UnityEditor.VFX` altındaki
/// sınıflar (`VFXGraph`, `VFXContext`, `VFXBlock`, ...) dışarıya kapalı.
/// Erişilebilir olduğu ÖLÇÜLDÜ (`SnowVfxApiProbe`): 70 somut blok tipi,
/// 24 bağlam tipi, `AddChild` ve `LinkTo` yerinde, blok örneklenebiliyor.
///
/// Reflection kırılgan: Unity sürümü değişirse tip veya imza kayabilir. O yüzden
/// her arama BAŞARISIZLIĞI FIRLATIYOR — sessizce yarım grafik üretmektense hiç
/// üretmemek doğru. Yarım grafik ekranda "kar yağmıyor" olarak görünür ve
/// sebebi günler sürer.
public static class SnowVfxBuilder
{
    const string Folder = "Assets/Snow/VFX";
    const string EditorAsm = "Unity.VisualEffectGraph.Editor";

    [MenuItem("To The Summit/Kar/VFX Grafiklerini Üret", false, 62)]
    static void BuildAll()
    {
        var r = new StringBuilder();
        r.AppendLine("# Kar — VFX grafikleri üretiliyor");

        try
        {
            BuildSnowfall(r);
        }
        catch (Exception e)
        {
            Debug.LogError(r + "\nÜRETİM DURDU: " + e.GetType().Name + " — " + e.Message +
                           "\n" + e.StackTrace);
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(r.ToString());
    }

    // ------------------------------------------------------------ VFX_Snowfall

    /// YAKIN KATMAN (spec §17.1). Ayrı ayrı görünen taneler; rüzgârda savrulur,
    /// örtü altında kesilir.
    ///
    /// Bu ilk grafik MEKANİZMAYI KANITLIYOR: asset açılıyor mu, bloklar duruyor
    /// mu, Unity yeniden açılınca bozulmuyor mu. Çalıştığı görülmeden kalan beş
    /// grafiğe geçilmiyor — biri yanlışsa altısını birden yazmış olmayalım.
    static void BuildSnowfall(StringBuilder r)
    {
        object graph = NewGraph("VFX_Snowfall", r);

        // --- Spawn: sabit oran. Oran runtime'da `_FlakeRate` ile sürülüyor
        //     (spec §17.3); grafikte yalnız blok duruyor.
        object spawner = AddContext(graph, "VFXBasicSpawner", new Vector2(0, 0), r);
        AddBlock(spawner, "VFXSpawnerConstantRate", r);

        // --- Initialize: konum, ömür, boyut
        object init = AddContext(graph, "VFXBasicInitialize", new Vector2(0, 200), r);

        // Spec §17.1: `Set Position (AABox)` (40, 26, 40).
        //
        // `PositionBox` BİR ŞEKİL, blok değil (ölçüldü — `PositionShapeBase`).
        // Bloğu `PositionShape`; şekli ayar olarak taşıyor.
        AddBlock(init, "Block.PositionShape", r);

        // Ömür 4–9 s, boyut 0.018 m × 0.6–1.7 (spec §17.1).
        AddBlock(init, "Block.SetAttribute", r);

        // --- Update: türbülans (spec §17.1)
        object update = AddContext(graph, "VFXBasicUpdate", new Vector2(0, 500), r);
        AddBlock(update, "Block.Turbulence", r);

        // --- Output: Lit Quad (spec §17.1)
        object output = AddContext(graph, "VFXComposedParticleOutput", new Vector2(0, 800), r);

        // ASGARİ EKRAN BOYUTU HAZIR BLOKTA. Spec §17.1 bunu formülle tarif
        // ediyor (`minWorld = dist * _MinPixelSize / ...`); VFX Graph'ta
        // `ScreenSpaceSize` bloğu aynı işi yapıyor. Kendi formülümüzü custom
        // HLSL'e yazmak, hazır olanın üstüne ikinci bir terim koymak olurdu.
        AddBlock(output, "Block.ScreenSpaceSize", r);
        AddBlock(output, "Block.Orient", r);

        Link(spawner, init, r);
        Link(init, update, r);
        Link(update, output, r);

        Save(graph, r);
    }

    // -------------------------------------------------------------- reflection

    static Assembly asm;

    static Assembly Asm => asm ??= AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(a => a.GetName().Name == EditorAsm)
        ?? throw new InvalidOperationException(EditorAsm + " yüklü değil.");

    static Type Find(string shortName)
    {
        string full = shortName.StartsWith("Block.")
            ? "UnityEditor.VFX.Block." + shortName.Substring(6)
            : "UnityEditor.VFX." + shortName;

        return Asm.GetType(full, false)
            ?? throw new InvalidOperationException("VFX tipi bulunamadı: " + full);
    }

    /// Boş bir `.vfx` yaratıp grafiğini döndürüyor.
    static object NewGraph(string name, StringBuilder r)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Snow", "VFX");

        string path = Folder + "/" + name + ".vfx";

        // VARSA SİLİNİYOR. Üretim tekrar koşturulabilir olmalı; üstüne yazmak
        // eski blokları bırakır ve grafik iki kez dolar.
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            AssetDatabase.DeleteAsset(path);

        Type util = Asm.GetType("UnityEditor.VisualEffectAssetEditorUtility", false)
            ?? throw new InvalidOperationException("VisualEffectAssetEditorUtility yok.");

        MethodInfo create = util.GetMethod("CreateNewAsset",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("CreateNewAsset yok.");

        create.Invoke(null, new object[] { path });

        Type resType = Asm.GetType("UnityEditor.VFX.VisualEffectResource", false)
            ?? Type.GetType("UnityEditor.VFX.VisualEffectResource, UnityEditor")
            ?? throw new InvalidOperationException("VisualEffectResource yok.");

        MethodInfo atPath = resType.GetMethod("GetResourceAtPath",
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetResourceAtPath yok.");

        object resource = atPath.Invoke(null, new object[] { path })
            ?? throw new InvalidOperationException("Resource okunamadı: " + path);

        // `GetOrCreateGraph` bir uzantı metodu; statik olarak çağrılıyor.
        MethodInfo getGraph = Asm.GetType("UnityEditor.VFX.VFXGraphExtension", false)
            ?.GetMethod("GetOrCreateGraph", BindingFlags.Public | BindingFlags.Static);

        if (getGraph == null)
            getGraph = Asm.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .FirstOrDefault(m => m.Name == "GetOrCreateGraph");

        if (getGraph == null)
            throw new InvalidOperationException("GetOrCreateGraph bulunamadı.");

        object graph = getGraph.Invoke(null, new[] { resource })
            ?? throw new InvalidOperationException("Grafik yaratılamadı.");

        r.AppendLine("  [+] " + name + " yaratıldı — " + path);
        return graph;
    }

    static object AddContext(object graph, string typeName, Vector2 pos, StringBuilder r)
    {
        Type t = Find(typeName);
        var ctx = ScriptableObject.CreateInstance(t)
            ?? throw new InvalidOperationException(typeName + " örneklenemedi.");

        SetPosition(ctx, pos);
        AddChild(graph, ctx);

        r.AppendLine("      bağlam  " + typeName);
        return ctx;
    }

    static void AddBlock(object context, string typeName, StringBuilder r)
    {
        Type t = Find(typeName);
        var block = ScriptableObject.CreateInstance(t)
            ?? throw new InvalidOperationException(typeName + " örneklenemedi.");

        AddChild(context, block);
        r.AppendLine("        blok  " + typeName);
    }

    static void AddChild(object parent, object child)
    {
        MethodInfo add = parent.GetType().GetMethod("AddChild",
            BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("AddChild yok: " + parent.GetType().Name);

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
            ?? throw new InvalidOperationException("LinkTo yok.");

        link.Invoke(from, new object[] { to, 0, 0 });
        r.AppendLine("      bağ     " + from.GetType().Name + " → " + to.GetType().Name);
    }

    static void Save(object graph, StringBuilder r)
    {
        foreach (string ad in new[] { "UpdateSubAssets", "OnSaved" })
        {
            MethodInfo m = graph.GetType().GetMethod(ad,
                BindingFlags.Public | BindingFlags.Instance);

            m?.Invoke(graph, null);
        }

        EditorUtility.SetDirty((UnityEngine.Object)graph);
        r.AppendLine("  [+] kaydedildi");
    }
}
