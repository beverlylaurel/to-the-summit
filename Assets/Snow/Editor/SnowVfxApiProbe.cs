// ROL: VFX Graph'ın grafik kurma API'sinin reflection'la erişilebilir olup
// olmadığını ölçer. Faz 8 için kurucu araç yazılabilir mi sorusunun cevabı.
// Çağıran: menü (tek seferlik sonda).

using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// VFX GRAPH KODDAN KURULABİLİR Mİ.
///
/// Spec Faz 8/9/13 altı `.vfx` asset'i istiyor. `.vfx` bir grafik asset'i;
/// Unity içinde elle çizilir. Koddan kurmanın tek yolu `UnityEditor.VFX`
/// altındaki model sınıfları — ama onlar `internal`, yani ancak reflection'la.
///
/// Bu sonda "olur herhâlde" demek yerine tek tek arıyor: tip var mı, metot var
/// mı, imzası ne. Cevap gelmeden kurucu araca başlanmayacak.
///
/// İşi bitince silinecek (`DECISIONS.md` → Silinecek geçiciler).
public static class SnowVfxApiProbe
{
    const string EditorAsm = "Unity.VisualEffectGraph.Editor";

    [MenuItem("To The Summit/Kar/VFX API Sondası", false, 61)]
    static void Run()
    {
        var r = new StringBuilder();

        r.AppendLine("# VFX Graph — koddan grafik kurulabilir mi");
        r.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        Assembly asm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == EditorAsm);

        if (asm == null)
        {
            Debug.LogError(r + "  [-] " + EditorAsm + " YÜKLÜ DEĞİL.");
            return;
        }

        r.AppendLine("  [+] Assembly yüklü: " + asm.GetName().Name +
                     " " + asm.GetName().Version);
        r.AppendLine();

        // --- Tipler
        r.AppendLine("## Tipler");

        string[] tipler =
        {
            "UnityEditor.VFX.VFXGraph",
            "UnityEditor.VFX.VFXModel",
            "UnityEditor.VFX.VFXContext",
            "UnityEditor.VFX.VFXBlock",
            "UnityEditor.VFX.VFXLibrary",
            "UnityEditor.VFX.VFXSlot",
            "UnityEditor.VFX.VisualEffectAssetEditorUtility",
        };

        foreach (string ad in tipler)
        {
            Type t = asm.GetType(ad, false);

            r.AppendLine("  [" + M(t != null) + "] " + ad.PadRight(48) +
                         (t == null ? "YOK"
                                    : (t.IsPublic ? "public" : "internal") +
                                      (t.IsAbstract ? ", abstract" : "")));
        }

        // --- Kritik metotlar
        r.AppendLine();
        r.AppendLine("## Metotlar");

        Probe(r, asm, "UnityEditor.VFX.VisualEffectAssetEditorUtility", "CreateNewAsset");
        Probe(r, asm, "UnityEditor.VFX.VFXModel", "AddChild");
        Probe(r, asm, "UnityEditor.VFX.VFXLibrary", "GetBlocks");
        Probe(r, asm, "UnityEditor.VFX.VFXLibrary", "GetContexts");

        // --- Somut blok tipleri sayılıyor: kurucu araç bunlardan seçecek.
        r.AppendLine();
        r.AppendLine("## Somut tipler");

        Type blockType = asm.GetType("UnityEditor.VFX.VFXBlock", false);
        Type ctxType = asm.GetType("UnityEditor.VFX.VFXContext", false);

        int blok = 0, ctx = 0;

        foreach (Type t in asm.GetTypes())
        {
            if (t.IsAbstract) continue;
            if (blockType != null && blockType.IsAssignableFrom(t)) blok++;
            if (ctxType != null && ctxType.IsAssignableFrom(t)) ctx++;
        }

        r.AppendLine("  Blok tipi sayısı            " + blok);
        r.AppendLine("  Bağlam (context) tipi       " + ctx);

        // --- Örnekleme gerçekten oluyor mu: tek bir blok yaratmayı dene.
        r.AppendLine();
        r.AppendLine("## Örnekleme denemesi");

        Type deneme = asm.GetTypes().FirstOrDefault(
            t => !t.IsAbstract && blockType != null && blockType.IsAssignableFrom(t));

        if (deneme == null)
        {
            r.AppendLine("  [-] Denenecek somut blok bulunamadı.");
        }
        else
        {
            try
            {
                var o = ScriptableObject.CreateInstance(deneme);
                bool ok = o != null;

                r.AppendLine("  [" + M(ok) + "] " + deneme.Name +
                             " örneklendi: " + (ok ? "EVET" : "hayır"));

                if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            catch (Exception e)
            {
                r.AppendLine("  [-] " + deneme.Name + " örneklenemedi: " +
                             e.GetType().Name + " — " + e.Message);
            }
        }

        Debug.Log(r.ToString());
    }

    static void Probe(StringBuilder r, Assembly asm, string tip, string metot)
    {
        Type t = asm.GetType(tip, false);

        if (t == null)
        {
            r.AppendLine("  [-] " + (tip + "." + metot).PadRight(52) + "TİP YOK");
            return;
        }

        MethodInfo[] m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                      BindingFlags.Static | BindingFlags.Instance)
                          .Where(x => x.Name == metot).ToArray();

        if (m.Length == 0)
        {
            r.AppendLine("  [-] " + (tip + "." + metot).PadRight(52) + "METOT YOK");
            return;
        }

        r.AppendLine("  [+] " + (tip + "." + metot).PadRight(52) +
                     m.Length + " aşırı yükleme   " +
                     "(" + string.Join(", ", m[0].GetParameters().Select(p => p.ParameterType.Name)) + ")");
    }

    static string M(bool ok) => ok ? "+" : "-";
}
