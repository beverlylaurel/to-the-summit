// ROL: SnowConstants.cs ile SnowConstants.hlsl'in BİREBİR aynı değerleri
// taşıdığını doğrular (spec §0.10, Faz 0 kabul kriteri).
// Çağıran: menü — To The Summit/Kar/Sabit Eşliğini Sına, ve SnowProjectCheck.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// SABİT AYRIŞMASI SESSİZDİR. Simülasyon GPU'da bir eşikle, CPU'daki karar başka
/// bir eşikle çalışırsa belirti "bazen oluyor bazen olmuyor" olur ve hangi
/// tarafın yanlış olduğu ekrandan anlaşılmaz. Bu test o ayrışmayı derleme
/// zamanında yakalıyor.
///
/// Eşleşme tablosu ELLE tutuluyor. Otomatik ad dönüşümü (RhoMin → SNOW_RHO_MIN)
/// denendi ve yanıltıcı: `MeltDdf → SNOW_MELT_DDF` tutuyor ama `SweMax →
/// SNOW_SWE_MAX` ile `MaxSweRate → SNOW_MAX_SWE_RATE` aynı kurala uymuyor.
/// Yanlış eşleşen bir çift testi yeşil gösterirdi.
public static class SnowConstantsTest
{
    const string HlslPath = "Assets/Snow/Shaders/SnowConstants.hlsl";

    /// C# alan adı → HLSL define adı. Yeni sabit eklenince buraya da eklenecek;
    /// eklenmezse test "eşleşmesi tanımsız" diye kırmızı yanar.
    static readonly (string csharp, string hlsl)[] Pairs =
    {
        ("RhoMin", "SNOW_RHO_MIN"),
        ("RhoMax", "SNOW_RHO_MAX"),
        ("RhoWater", "SNOW_RHO_WATER"),
        ("SnapQuads", "SNOW_SNAP_QUADS"),
        ("EdgeFadeStart", "SNOW_EDGE_FADE_START"),
        ("MinVisibleHeight", "SNOW_MIN_VISIBLE_HEIGHT"),
        ("LooseN", "SNOW_LOOSE_N"),
        ("PackedN", "SNOW_PACKED_N"),
        ("PackedSinkScale", "SNOW_PACKED_SINK_SCALE"),
        ("RimVelocityBias", "SNOW_RIM_VELOCITY_BIAS"),
        ("RimRefDepth", "SNOW_RIM_REF_DEPTH"),
        ("RimBlurTexels", "SNOW_RIM_BLUR_TEXELS"),
        ("WindFill", "SNOW_WIND_FILL"),
        ("SettleTau", "SNOW_SETTLE_TAU"),
        ("DisturbTau", "SNOW_DISTURB_TAU"),
        ("MeltDdf", "SNOW_MELT_DDF"),
        ("DriftBias", "SNOW_DRIFT_BIAS"),
        ("RainMeltBoost", "SNOW_RAIN_MELT_BOOST"),
        ("SweMax", "SNOW_SWE_MAX"),
        ("MaxSweRate", "SNOW_MAX_SWE_RATE"),
        ("MaxFlakeRate", "SNOW_MAX_FLAKE_RATE"),
        ("SkyAreaSize", "SNOW_SKY_AREA_SIZE"),
        ("SkyMoveThreshold", "SNOW_SKY_MOVE_THRESHOLD"),
        ("WindShadowC", "SNOW_WINDSHADOW_C"),
        ("ErosionRate", "SNOW_EROSION_RATE"),
        ("DriftU10Loose", "SNOW_DRIFT_U10_LOOSE"),
        ("DriftU10Packed", "SNOW_DRIFT_U10_PACKED"),
        ("MaxHeatSources", "SNOW_MAX_HEAT_SOURCES"),
        ("HeatMeltRate", "SNOW_HEAT_MELT_RATE"),
        ("HeatWetRate", "SNOW_HEAT_WET_RATE"),
        ("TWarm", "SNOW_T_WARM"),
        ("TCool", "SNOW_T_COOL"),
        ("TFreeze", "SNOW_T_FREEZE"),
        ("CrustGain", "SNOW_CRUST_GAIN"),
        ("CrustWindGain", "SNOW_CRUST_WIND_GAIN"),
        ("CrustMeltTau", "SNOW_CRUST_MELT_TAU"),
        ("CrustBury", "SNOW_CRUST_BURY"),
        ("CrustSolid", "SNOW_CRUST_SOLID"),
        ("CrustBreakPen", "SNOW_CRUST_BREAK_PEN"),
        ("CrustSinkScale", "SNOW_CRUST_SINK_SCALE"),
        ("SastrugiWindTau", "SNOW_SASTRUGI_WIND_TAU"),
        ("SuspScaleH", "SNOW_SUSP_SCALE_H"),
        ("SuspAlphaBase", "SNOW_SUSP_ALPHA_BASE"),
        ("SuspMaxHeight", "SNOW_SUSP_MAX_HEIGHT"),
        ("SprayParticlesPerM3", "SNOW_SPRAY_PARTICLES_PER_M3"),
        ("EdgeFadeRange", "SNOW_EDGE_FADE_RANGE"),

        // --- Kar yuzeyi geometrisi (Gorev 9: C# ikizi) ---
        ("TerrainVertexSpacing", "SNOW_TERRAIN_VERTEX_SPACING"),
        ("TessMinDalga", "SNOW_TESS_MIN_DALGA"),
        ("BedformDepthFrac", "SNOW_BEDFORM_DEPTH_FRAC"),
        ("FbmAmp", "SNOW_FBM_AMP"),
        ("FbmScale", "SNOW_FBM_SCALE"),
        ("FbmGain", "SNOW_FBM_GAIN"),
        ("RippleAmp", "SNOW_RIPPLE_AMP"),
        ("RippleLength", "SNOW_RIPPLE_LENGTH"),
        ("SastrugiHeight", "SNOW_SASTRUGI_HEIGHT"),
        ("SastrugiLength", "SNOW_SASTRUGI_LENGTH"),
        ("SastrugiWidth", "SNOW_SASTRUGI_WIDTH"),
        ("DriftHeight", "SNOW_DRIFT_HEIGHT"),
        ("DriftLength", "SNOW_DRIFT_LENGTH"),
        ("DriftWidth", "SNOW_DRIFT_WIDTH"),
        ("GroupSize", "SNOW_GROUP_SIZE"),
    };

    /// TABLODA OLMAYAN, KASITLI. `SnapQuadsInt` `SnapQuads`'ın tam sayı ikizi
    /// ve HLSL karşılığı yok — shader'da bölme yapılmıyor. `MeshBoundsHeight`
    /// CPU'da mesh sınırı kuruyor, shader okumuyor.
    ///
    /// DOKUZ ÇİFT TABLODAN ÇIKARILDI ÇÜNKÜ C# TARAFI ÖLÜYDÜ. `CompactGain`,
    /// `RimStrength`, `RimMax` ve beş `Sastrugi*` sabiti hiçbir yerden
    /// okunmuyordu; test "eşlik bozuk" diyordu ama eşleşecek iki taraf yoktu.
    /// Sastrugi'de C# yorumu HLSL'in TERSİNİ söylüyordu (LENGTH'i rüzgâr
    /// yönü sanıyordu); ölü sabit yanlış belge de taşıyor. `FillGain` bir
    /// fonksiyon, sabit değil — `SNOW_FILL_GAIN` diye bir define hiç yok.
    static readonly string[] CsharpOnly = { "SnapQuadsInt", "MeshBoundsHeight", "ReposeIterations" };

    [MenuItem("To The Summit/Kar/Sabit Eşliğini Sına", false, 60)]
    static void RunMenu() => Debug.Log(Run(out bool ok) + (ok ? "" : "\nEŞLİK BOZUK."));

    /// Raporu döndürür; `ok` bütün çiftlerin eşleştiğini söyler.
    public static string Run(out bool ok)
    {
        var report = new StringBuilder();
        ok = true;

        if (!File.Exists(HlslPath))
        {
            ok = false;
            return "SnowConstants.hlsl bulunamadı: " + HlslPath;
        }

        Dictionary<string, double> defines = ParseDefines(File.ReadAllText(HlslPath));

        // C# tarafındaki bütün sabitleri yansımayla oku — elle listelemek
        // unutulan bir alanı sessizce dışarıda bırakırdı.
        var csharp = new Dictionary<string, double>();
        foreach (FieldInfo f in typeof(SnowConstants).GetFields(BindingFlags.Public | BindingFlags.Static))
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

            // Göreli tolerans: 4.63e-8 gibi küçük sayılarda mutlak eşik yanıltır.
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

        // Eşleşme tablosunda olmayan sabitler: ikisinden birinde var, tabloda yok.
        foreach (string name in csharp.Keys)
        {
            bool listed = false;
            foreach ((string cs, string _) in Pairs) if (cs == name) { listed = true; break; }
            foreach (string only in CsharpOnly) if (only == name) { listed = true; break; }
            if (!listed) { report.AppendLine($"TABLODA YOK  C# {name}"); ok = false; }
        }

        // TEK TARAFLI HLSL SABİTİ HATA DEĞİL.
        //
        // Testin yakaladığı gerçek risk, aynı büyüklüğün iki tarafta AYRI
        // yazılması. Yalnız shader'ın okuduğu bir sabitin (`SNOW_ICE_F0`,
        // `SNOW_RIPPLE_AMP`, parıltı kapıları…) CPU karşılığı olmasına gerek
        // yok; olmasını şart koşmak testi kalıcı olarak kırık tutuyordu
        // (55 çiftin yanında 50'den fazla "TABLODA YOK" satırı). Sayılıyor
        // ama `ok`'u bozmuyor.
        int hlslOnly = 0;

        foreach (string name in defines.Keys)
        {
            bool listed = false;
            foreach ((string _, string hl) in Pairs) if (hl == name) { listed = true; break; }
            if (!listed) hlslOnly++;
        }

        report.AppendLine($"Yalnız shader'da: {hlslOnly} sabit (beklenen).");

        ok &= GridRuleTests(report);

        report.Insert(0, ok
            ? $"Sabit eşliği TAMAM — {matched}/{Pairs.Length} çift birebir aynı.\n"
            : $"Sabit eşliği BOZUK — {matched}/{Pairs.Length} çift eşleşti.\n");

        return report.ToString();
    }

    /// IZGARA KURALI (spec §6.4).
    ///
    /// > `MeshGrid` ve `Resolution` ikisi de ikinin kuvveti olmak zorundadır,
    /// > ve `MeshGrid ≤ Resolution`. Bu sağlandığında
    /// > `quad/texel = Resolution / MeshGrid` daima tam sayıdır, dolayısıyla
    /// > `_ScrollTexels = SnapStep / texelSize` da daima tam sayıdır.
    ///
    /// HEPSİ TAM SAYI ARİTMETİĞİYLE. Spec'in ilk hâli oranı float'la hesaplayıp
    /// üç haneye yuvarlamıştı; 4.0078 "4.0" görünüyordu ve üç presetin üçü de
    /// kuralı çiğnerken tablo "her satırda tam sayı" diyordu. Float
    /// karşılaştırması bu hatayı bir daha yakalayamaz.
    static bool GridRuleTests(StringBuilder r)
    {
        bool all = true;

        r.AppendLine();
        r.AppendLine("## Izgara kuralı — ikinin kuvveti (spec §6.4)");

        foreach (SnowQualityPreset p in Enum.GetValues(typeof(SnowQualityPreset)))
        {
            SnowQualityData q = SnowQuality.Get(p);

            bool gridPow = IsPowerOfTwo(q.MeshGrid);
            bool resPow = IsPowerOfTwo(q.Resolution);
            bool order = q.MeshGrid <= q.Resolution;
            bool divides = q.Resolution % q.MeshGrid == 0;

            // Tam sayı yolu: SnapStep/texelSize sadeleşince bu çıkıyor.
            int scroll = SnowConstants.SnapQuadsInt * (q.Resolution / q.MeshGrid);
            bool scrollOk = divides && q.ScrollTexels == scroll && scroll > 0;

            bool pass = gridPow && resPow && order && divides && scrollOk;
            all &= pass;

            r.AppendLine("  [" + (pass ? "+" : "-") + "] " + p.ToString().PadRight(8) +
                         " ızgara " + q.MeshGrid.ToString().PadLeft(4) +
                         (gridPow ? "" : " (2^n DEĞİL)") +
                         "   çözünürlük " + q.Resolution.ToString().PadLeft(4) +
                         (resPow ? "" : " (2^n DEĞİL)") +
                         (order ? "" : "   IZGARA > ÇÖZÜNÜRLÜK") +
                         (divides ? "   res/grid " + (q.Resolution / q.MeshGrid)
                                  : "   BÖLÜNMÜYOR") +
                         "   _ScrollTexels " + q.ScrollTexels);
        }

        return all;
    }

    static bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;

    static readonly Regex DefineLine =
        new(@"^\s*#define\s+(SNOW_[A-Z0-9_]+)\s+(-?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?)",
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
