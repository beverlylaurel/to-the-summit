// ROL: kar sınamalarını Unity'ye tıklamadan koşar ve sonucu dosyaya yazar.
// Çağıran: `Logs/snow-test.request` dosyasının zaman damgası.

using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// GEÇİCİ ARAÇ. Kar spec'i uygulanırken sınamaların Unity penceresine
/// tıklamadan koşabilmesi için var. Spec bitince silinecek
/// (`DECISIONS.md` → Silinecek geçiciler).
///
/// Deseni `BackgroundRefresh` ile aynı: tek dosyanın damgasına bakmak, dosya
/// sistemini taramaktan ölçülemeyecek kadar ucuz.
[InitializeOnLoad]
public static class SnowTestRunner
{
    const string RequestPath = "Logs/snow-test.request";
    const string ResultPath = "Logs/snow-test.log";
    const string TracePath = "Logs/snow-test.trace";

    const double Interval = 1.0;

    static DateTime stamp;
    static double next;

    static SnowTestRunner()
    {
        // NEREDE OLDUĞUMUZU YAZ. İstek dosyası bulunamazsa sebebi çalışma
        // dizinidir; tahmin etmek yerine çözülen tam yol kayda geçiyor.
        Trace("koşucu yüklendi  cwd=" + Directory.GetCurrentDirectory() +
              "  istek=" + Path.GetFullPath(RequestPath) +
              "  var mı=" + File.Exists(RequestPath));

        stamp = Stamp();
        EditorApplication.update += Tick;

        // Derleme sonrası bekleyen bir istek varsa hemen koş: tetikleyici
        // dosya derlemeden ÖNCE yazılıyor, damgası bu yüklemede değişmiş
        // görünmüyor.
        if (stamp != DateTime.MinValue) EditorApplication.delayCall += RunAndClear;
    }

    /// Adım adım iz. Koşu yarıda kalırsa nerede kaldığı görünür.
    static void Trace(string line)
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText(TracePath,
            DateTime.Now.ToString("HH:mm:ss.fff") + "  " + line + Environment.NewLine,
            new UTF8Encoding(false));
    }

    static DateTime Stamp() => File.Exists(RequestPath)
        ? File.GetLastWriteTimeUtc(RequestPath)
        : DateTime.MinValue;

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + Interval;

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

        DateTime current = Stamp();
        if (current == stamp) return;

        stamp = current;
        if (current != DateTime.MinValue) RunAndClear();
    }

    static void RunAndClear()
    {
        Trace("RunAndClear çağrıldı, istek var mı=" + File.Exists(RequestPath));
        if (!File.Exists(RequestPath)) return;

        string report;

        try
        {
            report = Run();
        }
        catch (Exception e)
        {
            report = "KOŞU ÇÖKTÜ: " + e;
            Trace("çöktü: " + e.Message);
        }

        Directory.CreateDirectory("Logs");
        File.WriteAllText(ResultPath, report, new UTF8Encoding(false));

        Trace("rapor yazıldı, " + report.Length + " karakter");

        File.Delete(RequestPath);
        stamp = DateTime.MinValue;
    }

    /// HER SINAMA AYRI YAKALANIYOR. Biri istisna atarsa diğerleri yine koşsun;
    /// yoksa tek bir bozuk sınama bütün raporu susturur ve neyin çalıştığı
    /// görünmez olur.
    static string Run()
    {
        var r = new StringBuilder(16384);

        r.AppendLine("KAR SINAMALARI — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine("Unity " + Application.unityVersion);
        r.AppendLine(new string('=', 72));
        r.AppendLine();

        bool all = true;

        Trace("suitler başlıyor");

        all &= Section(r, "Proje kontrolü", () => SnowProjectCheck.Run(),
                       out_ => out_.Contains("SONUÇ: devam edilebilir"));

        all &= Section(r, "Sabit eşliği", () => SnowConstantsTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[BAŞARISIZ]"));

        all &= Section(r, "Kaydırma", () => SnowScrollTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[BAŞARISIZ]"));

        all &= Section(r, "Yakalama", () => SnowCaptureTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[BAŞARISIZ]"));

        all &= Section(r, "İz", () => SnowTrailTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[BAŞARISIZ]"));

        all &= Section(r, "Clipmap", () => SnowClipmapTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[BAŞARISIZ]"));

        all &= Section(r, "Birikme", () => SnowAccumulationTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[BAŞARISIZ]"));

        all &= Section(r, "Shading", () => SnowShadingTest.Run(out bool ok) + Mark(ok),
                       out_ => !out_.Contains("[BAŞARISIZ]"));

        r.AppendLine(new string('=', 72));
        r.AppendLine(all ? "TOPLU SONUÇ: TAMAM" : "TOPLU SONUÇ: BAŞARISIZ");

        return r.ToString();
    }

    static string Mark(bool ok) => ok ? "\n[GEÇTİ]" : "\n[BAŞARISIZ]";

    static bool Section(StringBuilder r, string title, Func<string> body, Func<string, bool> verdict)
    {
        Trace("suit: " + title);
        r.AppendLine("--- " + title + " " + new string('-', Math.Max(0, 66 - title.Length)));

        string text;

        try
        {
            text = body();
        }
        catch (Exception e)
        {
            r.AppendLine("İSTİSNA: " + e.GetType().Name + ": " + e.Message);
            r.AppendLine(e.StackTrace);
            r.AppendLine();
            return false;
        }

        r.AppendLine(text);
        r.AppendLine();

        return verdict(text);
    }
}
