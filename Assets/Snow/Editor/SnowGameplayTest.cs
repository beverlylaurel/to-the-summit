// ROL: oyun tarafı API'sini ÖLÇER — örnek çözümleme, ayak sesi yüzey seçimi,
// hız çarpanı, toz sayısı, pencere eşlemesi.
// Çağıran: menü — To The Summit/Kar/Oynanış Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;

/// SPEC §19'UN TABLOSU SATIR SATIR. Bu tablo yanlış uygulanırsa belirti
/// "sığ karda derin kar sesi" olur ve kulakla ayırt edilmesi zordur; burada
/// her satır kendi eşiğinin iki yakasından deneniyor.
public static class SnowGameplayTest
{
    [MenuItem("To The Summit/Kar/Oynanış Sınaması", false, 58)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — oynanış sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = DecodeTest(r);
        ok &= FootstepTest(r);
        ok &= SpeedTest(r);
        ok &= PuffTest(r);
        ok &= WindowTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // --------------------------------------------------------------- çözümleme

    static bool DecodeTest(StringBuilder r)
    {
        r.AppendLine("## Örnek çözümleme (spec §19)");

        // SWE 0.02, rhoN 0.10 → ρ 100 → taban 20 cm. İz 5 cm oyulmuş,
        // kenarda 1 cm sırt.
        SnowSample s = SnowSampler.Decode(new Color(0.02f, 0.10f, 0.3f, 0f),
                                          new Color(0.05f, 0.01f, 0f, 0f));

        bool depth = Mathf.Abs(s.Depth - 0.16f) < 1e-4f;
        bool sink = Mathf.Abs(s.SinkDepth - 0.05f) < 1e-4f;
        bool density = Mathf.Abs(s.Density01 - 0.10f) < 1e-4f;
        bool wet = Mathf.Abs(s.Wetness - 0.3f) < 1e-4f;

        bool all = depth && sink && density && wet;

        r.AppendLine("  [" + M(all) + "] SWE 0.020 / rhoN 0.10 / carve 5 cm / rim 1 cm → " +
                     "derinlik " + (s.Depth * 100f).ToString("0.0") + " cm (beklenen 16.0), " +
                     "batma " + (s.SinkDepth * 100f).ToString("0.0") + " cm, " +
                     "yoğunluk " + s.Density01.ToString("0.00") + ", " +
                     "ıslaklık " + s.Wetness.ToString("0.00"));

        // Oyulma tabandan derinse yüzey sıfıra clamp'lenmeli — negatif
        // derinlik oyun tarafına sızarsa hız çarpanı 1'in üstüne çıkar.
        SnowSample deep = SnowSampler.Decode(new Color(0.005f, 0.10f, 0f, 0f),
                                             new Color(0.50f, 0f, 0f, 0f));

        bool clamped = deep.Depth >= 0f;
        all &= clamped;
        r.AppendLine("  [" + M(clamped) + "] Aşırı oyulma          derinlik " +
                     deep.Depth.ToString("0.000") + " m  (negatif olamaz)");

        return all;
    }

    // --------------------------------------------------------------- ayak sesi

    static bool FootstepTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Ayak sesi yüzeyi (spec §19.1)");

        (float depth, float density, SnowFootstepSurface want, string note)[] cases =
        {
            (0.010f, 0.10f, SnowFootstepSurface.None,    "2 cm altı — mevcut zemin sesi"),
            (0.019f, 0.90f, SnowFootstepSurface.None,    "eşiğin hemen altı"),
            (0.021f, 0.90f, SnowFootstepSurface.Packed,  "sığ + sıkışmış"),
            (0.050f, 0.56f, SnowFootstepSurface.Packed,  "yoğunluk eşiğinin üstü"),
            (0.050f, 0.54f, SnowFootstepSurface.Shallow, "yoğunluk eşiğinin altı"),
            (0.079f, 0.20f, SnowFootstepSurface.Shallow, "8 cm'nin hemen altı"),
            (0.200f, 0.29f, SnowFootstepSurface.Powder,  "derin + gevşek"),
            (0.200f, 0.31f, SnowFootstepSurface.Deep,    "derin + orta yoğunluk"),
        };

        bool all = true;

        foreach ((float depth, float density, SnowFootstepSurface want, string note) c in cases)
        {
            var sample = new SnowSample
            {
                Depth = c.depth,
                Density01 = c.density,
                Valid = true,
            };

            SnowFootstepSurface got = SnowFootstepAudio.SelectSurface(sample);
            bool ok = got == c.want;
            all &= ok;

            r.AppendLine("  [" + M(ok) + "] " + (c.depth * 100f).ToString("0.0").PadLeft(5) +
                         " cm,  yoğunluk " + c.density.ToString("0.00") + " → " +
                         got.ToString().PadRight(8) + " (beklenen " + c.want + ")   " + c.note);
        }

        // Islak varyant eşiği.
        bool dry = !SnowFootstepAudio.IsWet(new SnowSample { Wetness = 0.54f, Valid = true });
        bool wet = SnowFootstepAudio.IsWet(new SnowSample { Wetness = 0.56f, Valid = true });

        all &= dry && wet;
        r.AppendLine("  [" + M(dry && wet) + "] Islak varyant eşiği   0.54 → kuru,  0.56 → ıslak");

        // Geçersiz örnek asla kar sesi seçmemeli.
        bool invalid = SnowFootstepAudio.SelectSurface(default) == SnowFootstepSurface.None;
        all &= invalid;
        r.AppendLine("  [" + M(invalid) + "] Geçersiz örnek        None  (veri yoksa kar sesi yok)");

        return all;
    }

    // ------------------------------------------------------------------- hız

    static bool SpeedTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Hareket hızı (spec §19.2)");

        bool all = true;

        // Kar yoksa yavaşlama yok.
        float none = SnowMovementModifier.SpeedFor(default);
        bool noneOk = Mathf.Approximately(none, 1f);
        all &= noneOk;
        r.AppendLine("  [" + M(noneOk) + "] Veri yok          ×" + none.ToString("0.000"));

        // 10 cm altında yavaşlama yok.
        float shallow = SnowMovementModifier.SpeedFor(
            new SnowSample { Depth = 0.08f, Density01 = 0f, Valid = true });

        bool shallowOk = Mathf.Approximately(shallow, 1f);
        all &= shallowOk;
        r.AppendLine("  [" + M(shallowOk) + "] 8 cm gevşek       ×" + shallow.ToString("0.000") +
                     "  (eşik 10 cm)");

        // 70 cm gevşek karda en çok yavaşlama: 1 − 0.45 = 0.55.
        float deepLoose = SnowMovementModifier.SpeedFor(
            new SnowSample { Depth = 0.70f, Density01 = 0f, Valid = true });

        bool deepOk = Mathf.Abs(deepLoose - 0.55f) < 1e-4f;
        all &= deepOk;
        r.AppendLine("  [" + M(deepOk) + "] 70 cm toz         ×" + deepLoose.ToString("0.000") +
                     "  (beklenen 0.550 — azami yavaşlama)");

        // PATİKANIN ÖDÜLÜ: aynı derinlik, sıkışmış kar → yavaşlama yok.
        float deepPacked = SnowMovementModifier.SpeedFor(
            new SnowSample { Depth = 0.70f, Density01 = 1f, Valid = true });

        bool packedOk = Mathf.Approximately(deepPacked, 1f);
        all &= packedOk;
        r.AppendLine("  [" + M(packedOk) + "] 70 cm sıkışmış    ×" + deepPacked.ToString("0.000") +
                     "  (patika açmanın ödülü)");

        return all;
    }

    // ------------------------------------------------------------------ toz

    static bool PuffTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Ayak tozu (spec §19.3)");

        bool all = true;

        int shallow = SnowPuffEmitter.PuffCountFor(
            new SnowSample { Depth = 0.05f, Density01 = 0.1f, Valid = true });

        int packed = SnowPuffEmitter.PuffCountFor(
            new SnowSample { Depth = 0.30f, Density01 = 0.60f, Valid = true });

        int loose = SnowPuffEmitter.PuffCountFor(
            new SnowSample { Depth = 0.30f, Density01 = 0.10f, Valid = true });

        int deeper = SnowPuffEmitter.PuffCountFor(
            new SnowSample { Depth = 0.60f, Density01 = 0.10f, Valid = true });

        bool gates = shallow == 0 && packed == 0 && loose > 0;
        bool grows = deeper > loose;

        all &= gates && grows;

        r.AppendLine("  [" + M(gates) + "] Eşikler           5 cm → " + shallow +
                     ",  30 cm sıkışmış → " + packed + ",  30 cm gevşek → " + loose);
        r.AppendLine("  [" + M(grows) + "] Derinlikle artıyor 30 cm → " + loose +
                     ",  60 cm → " + deeper + "  (8 + 40·derinlik·gevşeklik)");

        return all;
    }

    // -------------------------------------------------------------- pencere

    static bool WindowTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Okuma penceresi (spec §19)");

        var center = new Vector2(-7494f, -4327.5f);
        const float AreaSize = 16f;
        const int Resolution = 1024;

        // Merkezde: pencere ortada.
        Vector2Int mid = SnowSampler.WindowOrigin(new Vector3(center.x, 0f, center.y),
                                                  center, AreaSize, Resolution);

        bool centered = mid.x == 512 - 32 && mid.y == 512 - 32;

        // Kenarda: pencere dokunun dışına TAŞMAMALI.
        Vector2Int corner = SnowSampler.WindowOrigin(
            new Vector3(center.x + AreaSize, 0f, center.y + AreaSize),
            center, AreaSize, Resolution);

        bool clamped = corner.x == Resolution - 64 && corner.y == Resolution - 64;

        Vector2Int low = SnowSampler.WindowOrigin(
            new Vector3(center.x - AreaSize, 0f, center.y - AreaSize),
            center, AreaSize, Resolution);

        bool clampedLow = low.x == 0 && low.y == 0;

        bool all = centered && clamped && clampedLow;

        r.AppendLine("  [" + M(centered) + "] Merkez            " + mid + "  (beklenen (480, 480))");
        r.AppendLine("  [" + M(clamped) + "] Sağ üst köşe      " + corner +
                     "  (beklenen (960, 960) — dokudan taşmıyor)");
        r.AppendLine("  [" + M(clampedLow) + "] Sol alt köşe      " + low + "  (beklenen (0, 0))");

        return all;
    }

    static string M(bool ok) => ok ? "+" : "-";
}
