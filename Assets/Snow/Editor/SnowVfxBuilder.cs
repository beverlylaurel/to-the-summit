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
            BuildPuff(r);
            BuildSpray(r);
            BuildSpindrift(r);
            BuildCurtain(r);
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
        object rate = AddBlock(spawner, "VFXSpawnerConstantRate", r);

        // Oran runtime'da `SnowfallLayers`'dan geliyor (spec §17.3): VFX
        // yoğunluğu ve `_SnowfallSWERate` AYNI `i01`'den türüyor.
        object rateParam = AddParameter(graph, "SpawnRate", typeof(float), 0f,
                                        new Vector2(-300, 0), r);
        LinkParameter(rateParam, rate, "Rate", r);

        // --- Initialize
        object init = AddContext(graph, "VFXBasicInitialize", new Vector2(0, 200), r);

        // Kapasite 40000 (spec §17.1). Runtime'da `Spawn Rate` ile kontrol
        // ediliyor; kapasite tavanı grafikte duruyor.
        SetSetting(init, "capacity", 40000u, r);

        // Spec §17.1: `Set Position (AABox)`, kutu (40, 26, 40), KUTUNUN
        // TAMAMINA spawn (yüzeyine değil).
        //
        // `PositionBox` bir ŞEKİL, blok değil (ölçüldü). Bloğu `PositionShape`;
        // şekli `shape` ayarında taşıyor.
        object pos = AddBlock(init, "Block.PositionShape", r);

        // Enum'da `Box` yok, `OrientedBox` var (ölçüldü — PositionShape.Type).
        SetSetting(pos, "shape", "OrientedBox", r);
        SetSetting(pos, "positionMode", "Volume", r);
        SetSlotField(pos, "Box", "size", new Vector3(40f, 26f, 40f), r);

        // Ömür 4–9 s (spec §17.1).
        object life = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(life, "attribute", "lifetime", r);
        SetSetting(life, "Random", "Uniform", r);
        SetSlot(life, "A", 4f, r);
        SetSlot(life, "B", 9f, r);

        // Boyut: taban 0.018 m, random 0.6–1.7× (spec §17.1).
        object size = AddBlock(init, "Block.SetAttribute", r);
        SetSetting(size, "attribute", "size", r);
        SetSetting(size, "Random", "Uniform", r);

        // Taban 0.018 m, random 0.6–1.7× (spec §17.1). Uçlar burada çarpılıyor;
        // shader'da ikinci bir çarpan yok.
        SetSlot(size, "A", 0.018f * 0.6f, r);
        SetSlot(size, "B", 0.018f * 1.7f, r);

        // --- Update: türbülans (spec §17.1)
        object update = AddContext(graph, "VFXBasicUpdate", new Vector2(0, 500), r);

        object turb = AddBlock(update, "Block.Turbulence", r);

        // Spec §17.1: `Intensity = 0.35 * _WindSpeed + 0.15`. Rüzgâra bağlı
        // olduğu için parametreden sürülüyor; taban değer 0.15.
        object turbParam = AddParameter(graph, "TurbulenceIntensity", typeof(float),
                                        0.15f, new Vector2(-300, 500), r);
        LinkParameter(turbParam, turb, "Intensity", r);
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

    // ------------------------------------------------------------- ortak iskelet

    /// Dört kısa ömürlü sistem aynı iskeleti paylaşıyor: spawn → init →
    /// update → çıktı. Tek fark ayarları; iskeleti kopyalamak dört yerde
    /// aynı hatayı yapmak olurdu.
    static (object init, object update, object output) Skeleton(
        object graph, uint capacity, float lifeA, float lifeB,
        float sizeA, float sizeB, StringBuilder r)
    {
        object spawner = AddContext(graph, "VFXBasicSpawner", new Vector2(0, 0), r);
        object rate = AddBlock(spawner, "VFXSpawnerConstantRate", r);

        // Oran dışarıdan sürülüyor (spec §17.3, §18.7). Parametre olmasaydı
        // `SetFloat` sessizce düşerdi.
        object rateParam = AddParameter(graph, "SpawnRate", typeof(float), 0f,
                                        new Vector2(-300, 0), r);
        LinkParameter(rateParam, rate, "Rate", r);

        object init = AddContext(graph, "VFXBasicInitialize", new Vector2(0, 200), r);
        SetSetting(init, "capacity", capacity, r);

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

    /// AYAK TOZ BULUTU (spec §19.3). Spec tetiği veriyor (`depth > 0.06 &&
    /// density01 < 0.50`) ama parçacığın sayılarını vermiyor; değerler
    /// `SnowPuffEmitter`'dan taşınıyor — Faz 9'da zaten ölçülüp yerleşmişler.
    static void BuildPuff(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowPuff", r);

        var (init, update, output) =
            Skeleton(graph, 512, 0.4f, 0.9f, 0.02f, 0.06f, r);

        AddBlock(update, "Block.Gravity", r);
        AddBlock(update, "Block.Drag", r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

        Save(graph, r);
    }

    // ------------------------------------------------------------ VFX_SnowSpray

    /// KOŞARKEN PÜSKÜRTME (spec §18.6)
    /// `[KAYNAK: Sumner, O'Brien & Hodgins, CGF 1999]`.
    ///
    /// Miktar uydurulmuyor, simülasyondan geliyor: `V̇ = genişlik × batma × hız`.
    /// O hesap `SnowSprayController`'da; grafik yalnız parçacığı çiziyor.
    static void BuildSpray(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowSpray", r);

        // Kapasite 3000, ömür 0.5–1.1 s, boyut 0.03–0.10 m (spec §18.6).
        var (init, update, output) =
            Skeleton(graph, 3000, 0.5f, 1.1f, 0.03f, 0.10f, r);

        // Yerçekimi −9.81 × 0.35, drag 2.5 (spec §18.6).
        AddBlock(update, "Block.Gravity", r);

        object drag = AddBlock(update, "Block.Drag", r);
        SetSlot(drag, "dragCoefficient", 2.5f, r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "FaceCameraPlane", r);

        Save(graph, r);
    }

    // ------------------------------------------------------------ VFX_Spindrift

    /// SALTASYON KATMANI (spec §18.7 Sistem A)
    /// `[KAYNAK: Pomeroy & Gray 1990; PBSM 1993]`.
    ///
    /// YERE YAPIŞIK: 1–5 cm. 1.5 m'ye spawn edilmiyor — o süspansiyon, ayrı
    /// sistem. Spec bunu ayrıca uyarıyor.
    static void BuildSpindrift(StringBuilder r)
    {
        object graph = NewGraph("VFX_Spindrift", r);

        // Ömür 1.2–3.0 s (spec §18.7). Boyut küçük ve çok sayıda.
        var (init, update, output) =
            Skeleton(graph, 8000, 1.2f, 3.0f, 0.01f, 0.03f, r);

        // `Orient: Along Velocity`, 4–8× uzatılmış (spec §18.7).
        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "AlongVelocity", r);

        Save(graph, r);
    }

    // ----------------------------------------------------------- VFX_SnowCurtain

    /// SÜSPANSİYON PERDELERİ (spec §18.7 Sistem B).
    ///
    /// KAPASİTE 14 — BİLİNÇLİ OLARAK DÜŞÜK. Her parçacık devasa (genişlik
    /// 12–25 m); maliyet fill-rate. Spec sayıyı ve gerekçesini birlikte veriyor.
    static void BuildCurtain(StringBuilder r)
    {
        object graph = NewGraph("VFX_SnowCurtain", r);

        // Ömür 6–12 s; boyut genişlik 12–25 m (spec §18.7).
        var (init, update, output) =
            Skeleton(graph, 14, 6f, 12f, 12f, 25f, r);

        object orient = AddBlock(output, "Block.Orient", r);
        SetSetting(orient, "mode", "AlongVelocity", r);

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
        // AYAR HER ZAMAN MODELİN KENDİ ALANI DEĞİL. `VFXBasicInitialize.capacity`
        // parçacık verisine yönlendiriliyor ve bağlamda öyle bir alan yok
        // (ölçüldü — `GetSetting` override'lı). O yüzden tip `GetSetting`'den
        // okunuyor, alandan değil.
        MethodInfo getSetting = model.GetType().GetMethod("GetSetting",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string) }, null)
            ?? throw new InvalidOperationException("GetSetting yok.");

        object ayar = getSetting.Invoke(model, new object[] { name });

        FieldInfo alan = ayar?.GetType()
            .GetField("field", BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(ayar) as FieldInfo
            ?? throw new InvalidOperationException(
                model.GetType().Name + " ayarı yok: " + name);

        object son = value is string str && alan.FieldType.IsEnum
            ? Enum.Parse(alan.FieldType, str)
            : Convert.ChangeType(value, alan.FieldType);

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
            if (Prop(slot, "name")?.GetValue(slot) as string != slotName) continue;

            PropertyInfo degerP = Prop(slot, "value")
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
        return ctx;
    }

    static object AddBlock(object context, string typeName, StringBuilder r)
    {
        Type t = Find(typeName);
        var block = ScriptableObject.CreateInstance(t)
            ?? throw new InvalidOperationException(typeName + " örneklenemedi.");

        AddChild(context, block);
        r.AppendLine("        blok  " + typeName);

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

            string ad = Prop(slot, "name")?.GetValue(slot) as string;
            object deger = Prop(slot, "value")?.GetValue(slot);

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

    /// EXPOSED PARAMETRE. Grafik dışarıdan sürülebilsin diye.
    ///
    /// Parametre YOKSA `VisualEffect.SetFloat` sessizce düşüyor: `HasFloat`
    /// false dönüyor, çağrı hiçbir şey yapmıyor ve dışarıdan "kar yağmıyor"
    /// görünüyor. Denetleyicinin yazdığı her ad burada karşılığını bulmalı.
    static object AddParameter(object graph, string name, Type type,
                               object value, Vector2 pos, StringBuilder r)
    {
        Type pt = Find("VFXParameter");

        var param = ScriptableObject.CreateInstance(pt)
            ?? throw new InvalidOperationException("VFXParameter örneklenemedi.");

        MethodInfo init = pt.GetMethod("Init",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(Type) }, null)
            ?? throw new InvalidOperationException("VFXParameter.Init yok.");

        init.Invoke(param, new object[] { type });

        SetPosition(param, pos);
        AddChild(graph, param);

        // `exposedName` ve `exposed` yalnız GET; alanları `[VFXSetting]`
        // (ölçüldü — VFXParameter.cs:56-59), o yüzden ayar yoluyla yazılıyor.
        // Çocuk eklendikten SONRA: ayar değişimi grafiği haberdar ediyor.
        SetSetting(param, "m_ExposedName", name, r);
        SetSetting(param, "m_Exposed", true, r);

        // Varsayılan değer parametrenin kendi çıkış slotunda duruyor.
        MethodInfo getOut = pt.GetMethod("GetOutputSlot",
            BindingFlags.Public | BindingFlags.Instance);

        if (getOut?.Invoke(param, new object[] { 0 }) is object outSlot)
            Prop(outSlot, "value")?.SetValue(outSlot, value);

        r.AppendLine("      param   " + name.PadRight(24) + type.Name + " = " + value);
        return param;
    }

    /// Parametrenin çıkışını bir bloğun giriş slotuna bağlıyor.
    static void LinkParameter(object param, object target, string slotName,
                              StringBuilder r)
    {
        object outSlot = param.GetType().GetMethod("GetOutputSlot",
            BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(param, new object[] { 0 })
            ?? throw new InvalidOperationException("Parametrenin çıkış slotu yok.");

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
                ?? throw new InvalidOperationException("VFXSlot.Link yok.");

            bool ok = (bool)link.Invoke(outSlot, new object[] { slot, true });

            r.AppendLine("      bağ     param → " + target.GetType().Name +
                         "." + slotName + (ok ? "" : "  BAŞARISIZ"));

            if (!ok) throw new InvalidOperationException(
                "Parametre bağlanamadı: " + slotName);

            return;
        }

        throw new InvalidOperationException(target.GetType().Name + " slotu yok: " + slotName);
    }

    /// YAPI SLOTU İÇİNDEKİ ALAN. `Box` slotu `OrientedBox` taşıyor; kutunun
    /// boyu onun `size` alanında. Slotun değerini komple değiştirmek yerine
    /// mevcut değeri alıp alanını yazıyoruz — merkez ve açı korunuyor.
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

            PropertyInfo degerP = Prop(slot, "value");
            object kutu = degerP.GetValue(slot);

            FieldInfo f = kutu.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    kutu.GetType().Name + " alanı yok: " + fieldName);

            f.SetValue(kutu, value);
            degerP.SetValue(slot, kutu);

            r.AppendLine("           slot   " + (slotName + "." + fieldName).PadRight(24) + value);
            return;
        }

        throw new InvalidOperationException(model.GetType().Name + " slotu yok: " + slotName);
    }

    /// ÖZELLİK ARAYICI, BELİRSİZLİK GÜVENLİ.
    ///
    /// `children` hiyerarşide gölgeleniyor (`VFXModel` ve türevleri ayrı ayrı
    /// tanımlıyor); düz `GetProperty` `AmbiguousMatchException` atıyor. En
    /// türemiş tipten başlayıp yukarı yürüyor, ilk bulduğunu alıyor.
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

    /// GRAFİĞİN SON HÂLİ. Ayarlar uygulandıktan SONRA dökülüyor: slot adları
    /// ayara göre değişiyor (şekil kutu olunca `arcSphere` gidiyor, kutu
    /// geliyor). Kurulum sırasında dökmek eski adları gösterir.
    static void DumpGraph(object graph, StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Son hâl");

        if (Prop(graph, "children")?.GetValue(graph) is not System.Collections.IEnumerable ctxs)
        {
            r.AppendLine("  [!] grafik çocukları okunamadı");
            return;
        }

        foreach (object ctx in ctxs)
        {
            r.AppendLine("  bağlam  " + ctx.GetType().Name);
            DumpModel(ctx, r);

            if (Prop(ctx, "children")?.GetValue(ctx) is not System.Collections.IEnumerable blocks)
                continue;

            foreach (object b in blocks)
            {
                r.AppendLine("    blok  " + b.GetType().Name);
                DumpModel(b, r);
            }
        }
    }

    static void Save(object graph, StringBuilder r)
    {
        if (Dump) DumpGraph(graph, r);

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
