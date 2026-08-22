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

        // --- Initialize
        object init = AddContext(graph, "VFXBasicInitialize", new Vector2(0, 200), r);

        // Spec §17.1: `Set Position (AABox)`, kutu (40, 26, 40), KUTUNUN
        // TAMAMINA spawn (yüzeyine değil).
        //
        // `PositionBox` bir ŞEKİL, blok değil (ölçüldü). Bloğu `PositionShape`;
        // şekli `shape` ayarında taşıyor.
        object pos = AddBlock(init, "Block.PositionShape", r);

        // Enum'da `Box` yok, `OrientedBox` var (ölçüldü — PositionShape.Type).
        SetSetting(pos, "shape", "OrientedBox", r);
        SetSetting(pos, "positionMode", "Volume", r);

        // Ömür 4–9 s (spec §17.1).
        object life = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(life, "attribute", "lifetime", r);
        SetSetting(life, "Random", "Uniform", r);

        // Boyut: taban 0.018 m, random 0.6–1.7× (spec §17.1).
        object size = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(size, "attribute", "size", r);
        SetSetting(size, "Random", "Uniform", r);

        // --- Update: türbülans (spec §17.1)
        object update = AddContext(graph, "VFXBasicUpdate", new Vector2(0, 500), r);

        object turb = AddBlock(update, "Block.Turbulence", r);
        SetSlot(turb, "frequency", 0.12f, r);
        SetSlot(turb, "octaves", 2, r);
        SetSlot(turb, "Drag", 0.9f, r);

        // --- Output: URP Lit Quad (spec §17.1)
        //
        // `VFXComposedParticleOutput` YETMİYOR: URP 17.5'te tek gölgeleme
        // seçeneği ShaderGraph (ölçüldü — somut `ParticleShading` tek). Spec
        // "Output Particle Lit Quad" istiyor; onun karşılığı URP paketindeki
        // `VFXURPLitPlanarPrimitiveOutput`, `primitiveType` varsayılanı Quad.
        object output = AddContext(graph, "URP.VFXURPLitPlanarPrimitiveOutput",
                                   new Vector2(0, 800), r);

        // Spec §17.1: `Blend = Alpha`, `Depth Write = Off`, `Soft Particles = On`.
        SetSetting(output, "blendMode", "Alpha", r);
        SetSetting(output, "zWriteMode", "Off", r);
        SetSetting(output, "useSoftParticle", true, r);

        // ASGARİ EKRAN BOYUTU HAZIR BLOKTA. Spec §17.1 formülle tarif ediyor
        // (`minWorld = dist * _MinPixelSize / ...`); `ScreenSpaceSize` +
        // `PixelAbsolute` aynı işi yapıyor. Kendi formülümüzü custom HLSL'e
        // yazmak hazır olanın üstüne ikinci bir terim koymak olurdu.
        object ss = AddBlock(output, "Block.ScreenSpaceSize", r);
        SetSlot(ss, "PixelSize", 1.3f, r);

        // Yönelim: `Face Camera Plane` (spec §17.1). Varsayılan zaten bu;
        // yine de yazılıyor ki varsayılan değişirse sessizce kaymasın.
        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

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

    /// İKİ ASSEMBLY'DE ARANIYOR. Bloklar ve temel bağlamlar VFX Graph
    /// paketinde, ama `Output Particle URP Lit Quad` (spec §17.1) URP
    /// paketinde: `UnityEditor.VFX.URP.VFXURPLitPlanarPrimitiveOutput`.
    /// Yalnız VFX assembly'sine bakınca "tip bulunamadı" veriyordu.
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

        throw new InvalidOperationException("VFX tipi bulunamadı: " + full);
    }

    // ------------------------------------------------------------ ayar / slot

    /// Blok veya bağlam ayarı. Enum'lar ADLA veriliyor; sayısal değer yazmak
    /// enum sırası değişince sessizce başka bir şey seçer.
    static void SetSetting(object model, string name, object value, StringBuilder r)
    {
        FieldInfo f = model.GetType()
            .GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                model.GetType().Name + " ayarı yok: " + name);

        object son = value is string str && f.FieldType.IsEnum
            ? Enum.Parse(f.FieldType, str)
            : Convert.ChangeType(value, f.FieldType);

        MethodInfo set = model.GetType().GetMethod("SetSettingValue",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string), typeof(object) }, null)
            ?? throw new InvalidOperationException("SetSettingValue yok.");

        set.Invoke(model, new[] { name, son });
        r.AppendLine("           ayar   " + name.PadRight(24) + son);
    }

    /// Giriş slotu değeri. Slot ADLA aranıyor — indeks kullanmak ayar
    /// değişince başka slotu yazar.
    static void SetSlot(object model, string slotName, object value, StringBuilder r)
    {
        MethodInfo nb = model.GetType().GetMethod("GetNbInputSlots",
            BindingFlags.Public | BindingFlags.Instance);
        MethodInfo get = model.GetType().GetMethod("GetInputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        if (nb == null || get == null)
            throw new InvalidOperationException(model.GetType().Name + " slot taşımıyor.");

        int n = (int)nb.Invoke(model, null);

        for (int i = 0; i < n; i++)
        {
            object slot = get.Invoke(model, new object[] { i });
            PropertyInfo adP = slot.GetType().GetProperty("name",
                BindingFlags.Public | BindingFlags.Instance);

            if (adP?.GetValue(slot) as string != slotName) continue;

            PropertyInfo degerP = slot.GetType().GetProperty("value",
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Slot değeri yazılamıyor.");

            degerP.SetValue(slot, value);
            r.AppendLine("           slot   " + slotName.PadRight(24) + value);
            return;
        }

        throw new InvalidOperationException(
            model.GetType().Name + " slotu yok: " + slotName);
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
        if (Dump) DumpModel(ctx, r);
        return ctx;
    }

    static object AddBlock(object context, string typeName, StringBuilder r)
    {
        Type t = Find(typeName);
        var block = ScriptableObject.CreateInstance(t)
            ?? throw new InvalidOperationException(typeName + " örneklenemedi.");

        AddChild(context, block);
        r.AppendLine("        blok  " + typeName);

        if (Dump) DumpModel(block, r);
        return block;
    }

    /// AYARLARI VE SLOTLARI YAZ. Hangi bloğun hangi ayarı açtığını tahmin etmek
    /// "ayar bulunamadı" hatasına çıkıyor; adları modelden okuyoruz.
    ///
    /// Menüden açılıyor; üretim kapalı koşuyor.
    static bool Dump;

    [MenuItem("To The Summit/Kar/VFX Grafiklerini Üret (döküm)", false, 63)]
    static void BuildAllWithDump()
    {
        Dump = true;
        try { BuildAll(); }
        finally { Dump = false; }
    }

    static void DumpModel(object model, StringBuilder r)
    {
        // --- ayarlar
        MethodInfo getSettings = model.GetType().GetMethod("GetSettings",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool), typeof(object) }, null);

        var alanlar = model.GetType()
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.GetCustomAttributes()
                         .Any(a => a.GetType().Name.StartsWith("VFXSetting")))
            .ToArray();

        foreach (FieldInfo f in alanlar)
            r.AppendLine("           ayar   " + f.Name.PadRight(28) +
                         f.FieldType.Name + " = " + (f.GetValue(model) ?? "null"));

        // --- slotlar
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

            string ad = slot.GetType().GetProperty("name",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(slot) as string;

            object deger = slot.GetType().GetProperty("value",
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(slot);

            r.AppendLine("           slot   " + (ad ?? "?").PadRight(28) +
                         (deger == null ? "null" : deger.GetType().Name + " = " + deger));
        }
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
