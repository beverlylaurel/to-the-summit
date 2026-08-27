// ROL: SeaConstants.cs ile SeaConstants.hlsl'in BİREBİR aynı değerleri
// taşıdığını doğrular (deniz spec §0.10, Faz 0 kabul kriteri).
// Çağıran: menü — To The Summit/Sea/Test Constant Parity.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// SABİT AYRIŞMASI SESSİZDİR. Spektrum GPU'da bir γ ile, kırılma kararı
/// CPU'da başka bir γ ile çalışırsa belirti "bazen oluyor bazen olmuyor"
/// olur ve hangi tarafın yanlış olduğu ekrandan anlaşılmaz.
///
/// `SnowConstantsTest`'in birebir uyarlaması. O testte öğrenilen iki ders
/// buraya da taşındı:
///
/// 1. **Eşleşme tablosu ELLE tutulur.** Otomatik ad dönüşümü yanıltıcı:
///    `GammaMild → SEA_GAMMA_MILD` tutuyor ama `FftSize → SEA_FFT_SIZE` ile
///    `FoamJThreshold → SEA_FOAM_J_THRESHOLD` aynı kurala uymuyor.
/// 2. **Tek taraflı HLSL sabiti HATA DEĞİL.** Yalnız shader'ın okuduğu bir
///    sabitin CPU karşılığı olmasına gerek yok; şart koşmak testi kalıcı
///    olarak kırık tutuyordu.
public static class SeaConstantsTest
{
    const string HlslPath = "Assets/Sea/Shaders/SeaConstants.hlsl";

    /// C# alan adı → HLSL define adı. Yeni sabit eklenince buraya da
    /// eklenecek; eklenmezse test "tabloda yok" diye kırmızı yanar.
    static readonly (string csharp, string hlsl)[] Pairs =
    {
        ("G", "SEA_G"),
        ("TwoPi", "SEA_TWO_PI"),
        ("WaterIor", "SEA_WATER_IOR"),
        ("BulkReflectivity", "SEA_BULK_REFLECTIVITY"),

        ("JonswapGamma", "SEA_JONSWAP_GAMMA"),
        ("JonswapSigmaLo", "SEA_JONSWAP_SIGMA_LO"),
        ("JonswapSigmaHi", "SEA_JONSWAP_SIGMA_HI"),
        ("MichellSteepness", "SEA_MICHELL_STEEPNESS"),

        ("MinDepth", "SEA_MIN_DEPTH"),
        ("ShoreFadeDepth", "SEA_SHORE_FADE_DEPTH"),
        ("ChopFadeDepth", "SEA_CHOP_FADE_DEPTH"),
        ("GammaMild", "SEA_GAMMA_MILD"),
        ("GammaSteep", "SEA_GAMMA_STEEP"),
        ("BreakFoamGain", "SEA_BREAK_FOAM_GAIN"),

        ("FoamJThreshold", "SEA_FOAM_J_THRESHOLD"),
        ("FoamJRange", "SEA_FOAM_J_RANGE"),
        ("FoamDecay", "SEA_FOAM_DECAY"),

        ("FftSize", "SEA_FFT_SIZE"),
        ("FftLog2", "SEA_FFT_LOG2"),
        ("TierCount", "SEA_TIER_COUNT"),
    };

    /// Tabloda olmayan, kasıtlı C# sabitleri. Şu an yok; ileride CPU'nun
    /// hesapladığı ama shader'ın okumadığı bir sabit çıkarsa buraya girer.
    static readonly string[] CsharpOnly = { };

    [MenuItem("To The Summit/Sea/Test Constant Parity", false, 80)]
    static void RunMenu() => Debug.Log(Run(out bool ok) + (ok ? "" : "\nEŞLİK BOZUK."));

    public static string Run(out bool ok)
    {
        var report = new StringBuilder();
        ok = true;

        if (!File.Exists(HlslPath))
        {
            ok = false;
            return "SeaConstants.hlsl bulunamadı: " + HlslPath;
        }

        Dictionary<string, double> defines = ParseDefines(File.ReadAllText(HlslPath));

        // C# tarafındaki bütün sabitleri yansımayla oku — elle listelemek
        // unutulan bir alanı sessizce dışarıda bırakırdı.
        var csharp = new Dictionary<string, double>();
        foreach (FieldInfo f in typeof(SeaConstants).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!f.IsLiteral) continue;
            object v = f.GetRawConstantValue();
            csharp[f.Name] = v is int i ? i : Convert.ToDouble(v);
        }

        int matched = 0;

        foreach ((string cs, string hl) in Pairs)
        {
            if (!csharp.TryGetValue(cs, out double a))
            {
                report.AppendLine($"EKSİK  C#   {cs}");
                ok = false;
                continue;
            }

            if (!defines.TryGetValue(hl, out double b))
            {
                report.AppendLine($"EKSİK  HLSL {hl}");
                ok = false;
                continue;
            }

            // Göreli tolerans: küçük sayılarda mutlak eşik yanıltır.
            double scale = Math.Max(Math.Abs(a), Math.Abs(b));
            double tolerance = scale > 0.0 ? scale * 1e-6 : 1e-12;

            if (Math.Abs(a - b) > tolerance)
            {
                report.AppendLine($"AYRIK  {cs} = {a} , {hl} = {b}");
                ok = false;
                continue;
            }

            matched++;
        }

        // C# tarafında olup tabloda olmayan sabit: ya unutulmuş ya ölü.
        foreach (string name in csharp.Keys)
        {
            bool listed = false;
            foreach ((string cs, string _) in Pairs) if (cs == name) { listed = true; break; }
            foreach (string only in CsharpOnly) if (only == name) { listed = true; break; }
            if (!listed) { report.AppendLine($"TABLODA YOK  C# {name}"); ok = false; }
        }

        // Tek taraflı HLSL sabiti hata değil — yalnız sayılıyor.
        int hlslOnly = 0;
        foreach (string name in defines.Keys)
        {
            bool listed = false;
            foreach ((string _, string hl) in Pairs) if (hl == name) { listed = true; break; }
            if (!listed) hlslOnly++;
        }

        if (hlslOnly > 0)
            report.AppendLine($"Yalnız shader'da: {hlslOnly} sabit (beklenen).");

        report.Insert(0, ok
            ? $"Deniz sabit eşliği TAMAM — {matched}/{Pairs.Length} çift birebir aynı.\n"
            : $"Deniz sabit eşliği BOZUK — {matched}/{Pairs.Length} çift eşleşti.\n");

        return report.ToString();
    }

    static readonly Regex DefineLine =
        new(@"^\s*#define\s+(SEA_[A-Z0-9_]+)\s+(-?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?)",
            RegexOptions.Multiline | RegexOptions.Compiled);

    static Dictionary<string, double> ParseDefines(string text)
    {
        var map = new Dictionary<string, double>();

        foreach (Match m in DefineLine.Matches(text))
        {
            if (double.TryParse(m.Groups[2].Value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double value))
                map[m.Groups[1].Value] = value;
        }

        return map;
    }
}
