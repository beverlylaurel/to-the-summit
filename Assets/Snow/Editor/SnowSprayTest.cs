// ROL: kar püskürtmesini ve savrulma eşiğini ÖLÇER — V̇ formülü, eşiğin
// gevşek/sıkışmış kar arasında kayması.
// Çağıran: menü — To The Summit/Snow/Spray Test.

using System.Text;
using UnityEditor;
using UnityEngine;

/// SALTASYON VE SÜSPANSİYON AYRI (spec §18.7) ama TETİK TEK (§18.1).
///
/// Saltasyon 1–5 cm, yüzeye yapışık ve yoğun; süspansiyon onun üstü. İkisi de
/// aynı `DriftActiveFor` eşiğinden açılıyor; bu sınama o eşiği ölçüyor.
public static class SnowSprayTest
{
    const string ComputePath = "Assets/Snow/Shaders/SnowfallSim.compute";

    const int Capacity = 4096;
    const int Stride = 12 * sizeof(float);

    const float GroundY = 100f;

    [MenuItem("To The Summit/Snow/Spray Test", false, 61)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — püskürtme ve savrulma sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = SprayTest(r);
        ok &= DriftGateTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // ------------------------------------------------------------ püskürtme

    static bool SprayTest(StringBuilder r)
    {
        r.AppendLine("## Püskürtme miktarı (spec §18.6)");
        r.AppendLine("  [i] [KAYNAK: Sumner, O'Brien & Hodgins, CGF 1999]");

        const float Width = 0.11f;
        const float PerM3 = 40000f;

        // SPEC'İN KENDİ SAĞLAMASI: bot 0.11 m, batma 0.20 m, hız 4 m/s
        // → V̇ = 0.088 m³/s → gevşeklik 0.8'de saniyede ~2800 parçacık.
        var reference = new SnowSample
        {
            SinkDepth = 0.20f,
            Density01 = 0.20f,     // gevşeklik 0.80
            Valid = true,
        };

        float rate = SnowSprayController.RateFor(reference, 4f, Width, PerM3);

        bool sanity = Mathf.Abs(rate - 2816f) < 40f;

        r.AppendLine("  [" + (sanity ? "+" : "-") + "] Spec sağlaması    " +
                     rate.ToString("0") + " parçacık/s  (spec ~2800, V̇ = 0.11 × 0.20 × 4 = " +
                     (Width * 0.20f * 4f).ToString("0.000") + " m³/s)");

        bool all = sanity;

        // EŞİKLER. Üçü de spec §18.6'dan; biri eksikse "yürürken de
        // püskürtme çıkıyor" olur (spec §22).
        (string name, float sink, float density, float speed, bool wanted)[] cases =
        {
            ("yürüyüş (1.5 m/s)",   0.20f, 0.20f, 1.5f, false),
            ("koşu (4 m/s)",        0.20f, 0.20f, 4.0f, true),
            ("sığ kar (4 cm)",      0.04f, 0.20f, 4.0f, false),
            ("sıkışmış patika",     0.20f, 0.60f, 4.0f, false),
            ("veri yok",            0.20f, 0.20f, 4.0f, false),
        };

        for (int i = 0; i < cases.Length; i++)
        {
            (string name, float sink, float density, float speed, bool wanted) c = cases[i];

            var sample = new SnowSample
            {
                SinkDepth = c.sink,
                Density01 = c.density,
                Valid = i != cases.Length - 1,
            };

            float got = SnowSprayController.RateFor(sample, c.speed, Width, PerM3);
            bool ok = (got > 0f) == c.wanted;
            all &= ok;

            r.AppendLine("  [" + (ok ? "+" : "-") + "] " + c.name.PadRight(20) +
                         got.ToString("0").PadLeft(6) + " parçacık/s  (beklenen " +
                         (c.wanted ? "> 0" : "0") + ")");
        }

        // HIZA VE DERİNLİĞE GÖRÜNÜR ŞEKİLDE BAĞLI (spec §21 Faz 13).
        float slow = SnowSprayController.RateFor(reference, 3f, Width, PerM3);
        float fast = SnowSprayController.RateFor(reference, 6f, Width, PerM3);

        var deeper = reference;
        deeper.SinkDepth = 0.40f;
        float deep = SnowSprayController.RateFor(deeper, 4f, Width, PerM3);

        bool scales = Mathf.Abs(fast / Mathf.Max(slow, 1f) - 2f) < 0.01f &&
                      Mathf.Abs(deep / Mathf.Max(rate, 1f) - 2f) < 0.01f;

        all &= scales;

        r.AppendLine("  [" + (scales ? "+" : "-") + "] Hız ve derinlikle  3→6 m/s: " +
                     slow.ToString("0") + " → " + fast.ToString("0") +
                     ",  20→40 cm: " + rate.ToString("0") + " → " + deep.ToString("0") +
                     "  (ikisi de doğrusal)");

        return all;
    }

    // ----------------------------------------------------------- savrulma eşiği

    static bool DriftGateTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Savrulma tetiği (spec §18.7 — §18.1 ile AYNI eşik)");

        float calm = SnowDriftVfxController.DriftActiveFor(5f, 0.9f);
        float onset = SnowDriftVfxController.DriftActiveFor(7f, 0.9f);
        float windy = SnowDriftVfxController.DriftActiveFor(12f, 0.9f);

        // SİNTERLENME EŞİĞİ YÜKSELTİYOR, YASAKLAMIYOR. Sıkışmış karda eşik
        // 10.7 m/s; 12 m/s onu aşıyor ve az da olsa savrulma başlıyor.
        // "Sıkışmışta hiç savrulmaz" beklemek modeli yanlış okumaktır.
        float packedWindy = SnowDriftVfxController.DriftActiveFor(12f, 0.05f);
        float packedModerate = SnowDriftVfxController.DriftActiveFor(9f, 0.05f);

        bool gated = calm <= 0f && onset > 0f && windy > onset &&
                     packedModerate <= 0f && packedWindy > 0f && packedWindy < windy * 0.5f;

        r.AppendLine("  [" + (gated ? "+" : "-") + "] Eşik              5 m/s → " +
                     calm.ToString("0.00") + ",  7 m/s → " + onset.ToString("0.00") +
                     ",  12 m/s → " + windy.ToString("0.00"));

        r.AppendLine("  [" + (gated ? "+" : "-") + "] Sinterlenme       sıkışmış karda 9 m/s → " +
                     packedModerate.ToString("0.00") + ",  12 m/s → " +
                     packedWindy.ToString("0.00") + "  (aynı rüzgârda gevşek kar " +
                     windy.ToString("0.00") + ")");

        r.AppendLine("  [i] Gevşek karda eşik 5 m/s, sıkışmışta 11 m/s. Saltasyon ve " +
                     "süspansiyon için ayrı eşik TANIMLANMADI — §18.1'inkiyle aynı.");

        return gated;
    }
}
