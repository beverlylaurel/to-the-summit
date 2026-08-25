// ROL: kar sisteminin parçalarının BAĞLI olduğunu denetler — yazıldığını değil.
// Çağıran: SnowTestRunner.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// YAZILMIŞ OLMAK BAĞLI OLMAK DEĞİL.
///
/// Formül denetimi spec'in 68 formülünün 68'ini kodda buldu ve yine de üç ayrı
/// şey görünmedi: `SnowDetailNormals` doğru yazılmış ama TEK yerden
/// çağrılıyordu, `SnowSparkle` include'u eksikti ve altı kernel sessizce
/// derlenmiyordu, dağın kar katmanı hiç yoktu. Üçü de aynı sınıf: parça var,
/// bağı yok.
///
/// Bu bölüm o sınıfı tarıyor. Üç soru soruyor:
///
/// 1. Shader'da tanımlı her fonksiyon en az bir yerden çağrılıyor mu?
/// 2. `SnowShaderIDs`'deki her ad hem YAZILIYOR hem OKUNUYOR mu? Yazılmayan bir
///    uniform sessizce sıfır okunur — en pahalı hata sınıfı, çünkü hiçbir yerde
///    hata vermez.
/// 3. Runtime'daki her bileşen sahne kurulumunda ekleniyor mu?
///
/// İstisnalar açıkça listeleniyor ve her birinin gerekçesi yanında. Gerekçesiz
/// istisna, denetimi süs hâline getirir.
public static class SnowWiringTest
{
    const string ShaderDir = "Assets/Snow/Shaders";
    const string RuntimeDir = "Assets/Snow/Runtime";
    const string EditorDir = "Assets/Snow/Editor";

    /// Shader tarafında tanımlı ama BİLEREK çağrılmayanlar.
    static readonly Dictionary<string, string> FunctionExceptions = new()
    {
        // Shader giriş noktaları: `#pragma fragment` ile bağlanıyorlar, kod
        // içinden çağrılmıyorlar. Tarayıcı parantezli çağrı arıyor, pragma'yı
        // görmüyor.
        { "SnowLitFragment", "#pragma fragment girişi" },
        { "SnowShadowFragment", "#pragma fragment girişi" },
        { "SnowDepthNormalsFragment", "#pragma fragment girişi" },
    };

    /// Yazılmayan ama sorun olmayan uniform'lar.
    static readonly Dictionary<string, string> WriteExceptions = new()
    {
        { "_SnowDetailNormal", "materyalde de duruyor; global yayın SnowManager'da" },
    };

    /// Okunmayan (hiçbir shader'da tanımlı olmayan) ama sorun olmayan ID'ler.
    static readonly Dictionary<string, string> ReadExceptions = new()
    {
        { "_SnowLineY", "karakter shader'ının kendi çizgisi; kullanıcı ekleyecek (spec §16.1)" },
        { "_SnowAccum", "karakter shader'ının kendi birikmesi; kullanıcı ekleyecek (spec §16.1)" },
    };

    public static string Run(out bool pass)
    {
        var r = new StringBuilder();
        pass = true;

        r.AppendLine("# Kar — parçalar bağlı mı");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        var shaderFiles = Directory.GetFiles(ShaderDir, "*.*", SearchOption.AllDirectories)
            .Where(p => p.EndsWith(".hlsl") || p.EndsWith(".shader") || p.EndsWith(".compute"))
            .ToArray();

        // Dağın kar katmanı kar ağacının DIŞINDA; taramaya dahil.
        var allShaderText = new Dictionary<string, string>();

        foreach (string p in shaderFiles) allShaderText[p] = File.ReadAllText(p);

        foreach (string p in Directory.GetFiles("Assets/Shaders", "*.*")
                     .Where(p => p.EndsWith(".hlsl") || p.EndsWith(".shader")))
            allShaderText[p] = File.ReadAllText(p);

        pass &= Functions(r, shaderFiles, allShaderText);
        pass &= Uniforms(r, allShaderText);
        pass &= Components(r);
        pass &= Prose(r);

        r.AppendLine();
        r.AppendLine("SONUÇ: " + (pass ? "TAMAM" : "BAŞARISIZ"));
        return r.ToString();
    }

    // ------------------------------------------------------------------ 1

    static bool Functions(StringBuilder r, string[] shaderFiles,
                          Dictionary<string, string> allText)
    {
        r.AppendLine("## Tanımlı ama çağrılmayan shader fonksiyonu");

        var defined = new Dictionary<string, string>();

        var def = new Regex(@"^\s*(?:float|half|uint|int|bool|void)[0-9x]*\s+(Snow\w+|RNMBlend|Wyvill\w*)\s*\(",
                            RegexOptions.Multiline);

        foreach (string p in shaderFiles)
        foreach (Match m in def.Matches(allText[p]))
            defined[m.Groups[1].Value] = Path.GetFileName(p);

        var orphans = new List<string>();

        foreach (var kv in defined)
        {
            if (FunctionExceptions.ContainsKey(kv.Key)) continue;

            int callers = 0;

            foreach (var f in allText)
            {
                // Kendi tanımı sayılmıyor: çağrıyı ayırt etmek için tanım satırı çıkarılıyor.
                string body = def.Replace(f.Value, "");
                if (Regex.IsMatch(body, @"\b" + Regex.Escape(kv.Key) + @"\s*\(")) callers++;
            }

            if (callers == 0) orphans.Add(kv.Key + "  (" + kv.Value + ")");
        }

        r.AppendLine("  Tanımlı fonksiyon        " + defined.Count);
        r.AppendLine("  [" + M(orphans.Count == 0) + "] Çağrılmayan          " + orphans.Count);

        foreach (string o in orphans) r.AppendLine("      - " + o);

        r.AppendLine();
        return orphans.Count == 0;
    }

    // ------------------------------------------------------------------ 2

    static bool Uniforms(StringBuilder r, Dictionary<string, string> allText)
    {
        r.AppendLine("## Uniform hem yazılıyor hem okunuyor mu");

        string ids = File.ReadAllText(Path.Combine(RuntimeDir, "SnowShaderIDs.cs"));

        var entries = Regex.Matches(ids, @"public static readonly int (\w+)\s*=\s*Shader\.PropertyToID\(""(\w+)""\)")
            .Select(m => (Field: m.Groups[1].Value, Name: m.Groups[2].Value))
            .ToArray();

        string runtime = string.Join("\n",
            Directory.GetFiles(RuntimeDir, "*.cs").Select(File.ReadAllText));
        string editor = string.Join("\n",
            Directory.GetFiles(EditorDir, "*.cs").Select(File.ReadAllText));
        string csharp = runtime + "\n" + editor;

        string shaders = string.Join("\n", allText.Values);

        var neverWritten = new List<string>();
        var neverRead = new List<string>();

        foreach (var e in entries)
        {
            bool written = Regex.IsMatch(csharp,
                @"Set(?:Global|Compute)\w*\([^)]*SnowShaderIDs\." + e.Field + @"\b")
                || Regex.IsMatch(csharp, @"SetComputeTextureParam\([^;]*SnowShaderIDs\." + e.Field + @"\b")
                || Regex.IsMatch(csharp, @"\.Set\w+\(\s*SnowShaderIDs\." + e.Field + @"\b")
                || Regex.IsMatch(csharp, @"""" + e.Name + @"""");

            // Okunuyor = herhangi bir shader dosyasında adı geçiyor.
            bool read = shaders.Contains(e.Name);

            if (!written && !WriteExceptions.ContainsKey(e.Name))
                neverWritten.Add(e.Name + "  (SnowShaderIDs." + e.Field + ")");

            if (!read && !ReadExceptions.ContainsKey(e.Name))
                neverRead.Add(e.Name + "  (SnowShaderIDs." + e.Field + ")");
        }

        r.AppendLine("  Tanımlı ID               " + entries.Length);
        r.AppendLine("  [" + M(neverWritten.Count == 0) + "] Hiç YAZILMAYAN       " + neverWritten.Count +
                     "   (shader sessizce sıfır okur)");

        foreach (string n in neverWritten) r.AppendLine("      - " + n);

        r.AppendLine("  [" + M(neverRead.Count == 0) + "] Hiç OKUNMAYAN        " + neverRead.Count +
                     "   (ölü ID)");

        foreach (string n in neverRead) r.AppendLine("      - " + n);

        r.AppendLine();
        return neverWritten.Count == 0 && neverRead.Count == 0;
    }

    // ------------------------------------------------------------------ 3

    static bool Components(StringBuilder r)
    {
        r.AppendLine("## Runtime bileşeni sahne kurulumunda ekleniyor mu");

        string setup = File.ReadAllText(Path.Combine(EditorDir, "SnowDebugWindow.cs"));

        var components = Directory.GetFiles(RuntimeDir, "*.cs")
            .Select(p => Path.GetFileNameWithoutExtension(p))
            .Where(n => File.ReadAllText(Path.Combine(RuntimeDir, n + ".cs"))
                            .Contains("class " + n + " : MonoBehaviour"))
            .ToArray();

        // KULLANICI TARAFI BİLEŞENLER. Bunlar karakterin kemiğine, ateşin
        // üstüne veya adım olayına bağlanıyor; sahne kurulumu bunları
        // yerleştiremez çünkü nereye takılacakları bir TASARIM kararı
        // (spec §1.4, `DECISIONS.md` → Bekleyen kararlar).
        var userSide = new Dictionary<string, string>
        {
            { "SnowDeformer", "karakterin ayak/bacak kemiklerine" },
            { "SnowCharacterAccumulator", "karakter mesh'ine" },
            { "SnowHeatSource", "ateş/ısı veren nesnelere" },
            { "SnowFootstepAudio", "adım olayına" },
            { "SnowPuffEmitter", "adım olayına" },
            { "SnowSprayController", "adım olayına" },
            { "SnowMovementModifier", "hareket koduna" },
        };

        var missing = components
            .Where(n => !setup.Contains("<" + n + ">") && !userSide.ContainsKey(n))
            .ToList();

        r.AppendLine("  Kullanıcı tarafı         " + userSide.Count +
                     "   (sahneye kurulum yerleştiremez, bkz. DECISIONS)");

        r.AppendLine("  MonoBehaviour            " + components.Length);
        r.AppendLine("  [" + M(missing.Count == 0) + "] Kurulumda YOK        " + missing.Count);

        foreach (string m in missing) r.AppendLine("      - " + m);

        r.AppendLine();
        return missing.Count == 0;
    }

    // ------------------------------------------------------------------ 4

    /// SPEC'İN DÜZMETİN KOŞULLARI.
    ///
    /// Formül denetimi kod bloklarını tarıyor; spec'in bir kısmı ise DÜZMETİN.
    /// Sapmaların tamamı orada çıktı — kod bloğu verilen 68 formülün 68'i
    /// doğruydu. Aşağıdakiler o düzmetinden çıkan, mekanik olarak
    /// denetlenebilir koşullar. Her satırın yanında spec bölümü var.
    static bool Prose(StringBuilder r)
    {
        r.AppendLine("## Spec'in düzmetin koşulları");

        var checks = new (string Section, string What, string File, string Needle)[]
        {
            ("§8.3",  "Kar materyali Queue = Geometry+50",
             "Assets/Snow/Shaders/SnowLit.shader", "Geometry+50"),

            ("§15.2", "Kar yoksa compute pass'leri kapalı",
             "Assets/Snow/Runtime/SnowManager.cs", "if (IsDormant) return;"),

            ("§15.2", "Per-material property'ler tek CBUFFER'da",
             "Assets/Snow/Shaders/SnowLitInput.hlsl", "CBUFFER_START(UnityPerMaterial)"),


            ("§14.2", "Detay normalleri kar mesh'inde",
             "Assets/Snow/Shaders/SnowLitForwardPass.hlsl", "SnowApplyDetailNormals"),

            ("§14.2", "Detay normalleri dağın kar katmanında",
             "Assets/Shaders/MountainSurface.hlsl", "SnowApplyDetailNormals"),

            ("§3.4",  "Yağmur kar yağarken susuyor",
             "Assets/Scripts/Weather/PrecipitationRenderer.cs", "SnowRuntimeState.RainWeight01"),

            ("§13.2", "Mesafeye göre displacement kısma YOK",
             "Assets/Snow/Shaders/SnowLit.shader", ""),

            ("§9.2",  "Yakalama shader'ı Cull Off",
             "Assets/Snow/Shaders/Hidden_SnowCaptureDepth.shader", "Cull Off"),

            ("§16",   "Nesne karı ayrı shader (mevcut shader'lar değişmedi)",
             "Assets/Snow/Shaders/SnowCoverObject.shader", "SnowCoverMask"),
        };

        int bad = 0;

        foreach (var c in checks)
        {
            bool ok;

            if (c.Needle.Length == 0)
            {
                // Olumsuz koşul: mesafeye göre kısma OLMAMALI.
                string body = File.Exists(c.File) ? File.ReadAllText(c.File) : "";
                ok = !body.Contains("distanceFade") && !body.Contains("_DisplacementFade");
            }
            else
            {
                ok = File.Exists(c.File) && File.ReadAllText(c.File).Contains(c.Needle);
            }

            if (!ok) bad++;

            r.AppendLine("  [" + M(ok) + "] " + c.Section.PadRight(7) + c.What);
        }

        r.AppendLine();
        r.AppendLine("  Denetlenen               " + checks.Length + "   eksik " + bad);
        r.AppendLine();

        return bad == 0;
    }

    static string M(bool ok) => ok ? "+" : "-";
}
