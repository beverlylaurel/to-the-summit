// ROL: kar shading'inin iki zor formülünü ÖLÇER — parıltının mesafede
// sabit kalması ve Reoriented Normal Mapping'in kimlik özellikleri.
// Çağıran: menü — To The Summit/Kar/Shading Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;

/// GÖRÜNTÜYE BAKMIYOR, FORMÜLE BAKIYOR.
///
/// "Kar güzel mi" sorusunun karşılığı yok; ama §22'nin iki belirtisinin
/// (`Parıltı titriyor`, `Detay normal yanlış`) sayısal karşılığı var:
///
/// - Parıltı: ekran uzayındaki YOĞUNLUK mesafeden bağımsız olmalı. Naif
///   uygulamada uzakta bir piksele yüzlerce kristal düşer ve oran fırlar;
///   Bowles & Wang'in LOD uyarlaması tam bunu engelliyor.
/// - RNM: düz bir detayla harmanlanan taban DEĞİŞMEMELİ. `lerp` ile
///   harmanlansaydı taban yarıya iner ve bu sınama kırmızı yanardı.
public static class SnowShadingTest
{
    const int Res = 128;
    const string KernelPath = "Assets/Snow/Editor/SnowTestKernels.compute";
    const string SparklePath = "Assets/Snow/Shaders/SnowSparkle.hlsl";
    const string LightingPath = "Assets/Snow/Shaders/SnowLighting.hlsl";
    const string ForwardPath = "Assets/Snow/Shaders/SnowLitForwardPass.hlsl";
    const string DetailPath = "Assets/Snow/Shaders/SnowDetailNormals.hlsl";

    [MenuItem("To The Summit/Kar/Shading Sınaması", false, 55)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — shading sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(KernelPath);

        if (cs == null)
        {
            r.AppendLine("  [-] " + KernelPath + " yüklenemedi.");
            ok = false;
            return r.ToString();
        }

        ok = SparkleTest(r, cs);
        ok &= RnmTest(r, cs);
        ok &= WiringTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // ---------------------------------------------------------------- parıltı

    static bool SparkleTest(StringBuilder r, ComputeShader cs)
    {
        r.AppendLine("## Parıltı — mesafede yoğunluk sabit mi (spec §14.4)");
        r.AppendLine("  [i] [KAYNAK: Bowles & Wang, SIGGRAPH 2015]");

        int kernel = cs.FindKernel("KTestSparkle");
        int groups = Mathf.CeilToInt(Res / 8f);

        RenderTexture rt = NewRt(Res);

        // Yakın plandan uzağa: piksel ayak izi dört kat büyüyor. Hücre boyu
        // 4 mm; en uzakta bir piksele yüzlerce hücre düşüyor.
        float[] footprints = { 0.002f, 0.008f, 0.032f, 0.128f, 0.512f };

        var fractions = new float[footprints.Length];
        bool all = true;

        try
        {
            cs.SetInt("_Resolution", Res);
            cs.SetFloat("_SparkleCellSize", 0.004f);
            cs.SetFloat("_SparkleDensity", 0.06f);
            cs.SetFloat("_SparkleSharpness", 8f);
            cs.SetVector("_TestViewDir", new Vector4(0.3f, 0.9f, 0.3f, 0f));
            cs.SetVector("_TestLightDir", new Vector4(-0.4f, 0.8f, 0.45f, 0f));

            for (int i = 0; i < footprints.Length; i++)
            {
                cs.SetFloat("_TestFootprint", footprints[i]);
                cs.SetTexture(kernel, "_TestOut", rt);
                cs.Dispatch(kernel, groups, groups, 1);

                Color[] px = Read(rt);

                int lit = 0;
                for (int p = 0; p < px.Length; p++) if (px[p].r > 0.5f) lit++;

                fractions[i] = lit / (float)px.Length;
            }

            float min = float.MaxValue, max = 0f;
            foreach (float f in fractions) { min = Mathf.Min(min, f); max = Mathf.Max(max, f); }

            // Naif parıltıda oran ayak iziyle birlikte iki kat büyüklük
            // değişir. LOD uyarlaması çalışıyorsa dar bir bantta kalır.
            bool stable = min > 0.002f && max < min * 6f;
            all &= stable;

            var line = new StringBuilder("  [" + M(stable) + "] Parıldayan piksel oranı  ");
            for (int i = 0; i < footprints.Length; i++)
                line.Append((footprints[i] * 1000f).ToString("0")).Append(" mm→")
                    .Append((fractions[i] * 100f).ToString("0.00")).Append("%   ");

            r.AppendLine(line.ToString());
            r.AppendLine("  [i] En düşük/en yüksek oran " + (max / Mathf.Max(min, 1e-6f)).ToString("0.00") +
                         "×  (LOD uyarlaması yoksa bu sayı onlarca kat olur)");
        }
        finally
        {
            Release(ref rt);
        }

        return all;
    }

    // -------------------------------------------------------------------- RNM

    static bool RnmTest(StringBuilder r, ComputeShader cs)
    {
        r.AppendLine();
        r.AppendLine("## Detay normali — eğim uzayında toplama (spec §14.2)");
        r.AppendLine("  [i] RNM tabanı koruyamadı; ölçüm SYMPTOMS.md'de");

        int kernel = cs.FindKernel("KTestRnm");
        RenderTexture rt = NewRt(8);

        bool all = true;

        try
        {
            cs.SetTexture(kernel, "_TestOut", rt);
            cs.Dispatch(kernel, 1, 1, 1);

            Color[] px = Read(rt);

            float flatDetail = Mag(px[0]);
            float flatBase = Mag(px[1]);
            float unitLength = px[2].r;

            bool a = flatDetail < 1e-3f;
            bool b = flatBase < 1e-3f;
            bool c = Mathf.Abs(unitLength - 1f) < 1e-3f;

            all &= a && b && c;

            r.AppendLine("  [" + M(a) + "] Sıfır detay tabanı bozmuyor sapma " +
                         flatDetail.ToString("0.000000"));
            r.AppendLine("  [" + M(b) + "] Düz taban detayı geçiriyor  sapma " +
                         flatBase.ToString("0.000000"));
            r.AppendLine("  [" + M(c) + "] Sonuç birim uzunlukta       |n| = " +
                         unitLength.ToString("0.000000"));
        }
        finally
        {
            Release(ref rt);
        }

        return all;
    }

    // ------------------------------------------------------------------ bağlar

    /// KAYNAK TARAMASI, DAVRANIŞ SINAMASI DEĞİL. §22'nin dört belirtisi
    /// tek bir satırın unutulmasından doğuyor; o satırların yerinde
    /// olduğunu burada doğruluyoruz. Nasıl göründüğü kullanıcının testinde.
    static bool WiringTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Zorunlu bağlar (spec §22'nin belirtileri)");

        (string file, string needle, string symptom)[] checks =
        {
            (LightingPath, "_SunElevation01", "Gece kar parıldıyor → sunGate uygulanmamış"),
            (LightingPath, "SnowSparkle(", "Parıltı hiç yok"),
            (LightingPath, "_ShadowTint", "Gölge mavimsi değil"),
            (LightingPath, "DirectBRDFSpecular", "Yansıma yok"),
            (ForwardPath, "MixFog", "Mevcut sis karın üstünde çalışmıyor"),
            (ForwardPath, "SnowApplyDetailNormals", "Detay normalleri bağlanmamış"),
            (ForwardPath, "SNOW_MIN_VISIBLE_HEIGHT", "Karın kenarında titreme → clip eşiği yok"),
            (DetailPath, "SampleDetailSlope", "Detay normal yanlış → eğim toplamı yok"),
            (SparklePath, "log2", "Parıltı titriyor → LOD uyarlaması atlanmış"),
            (LightingPath, "SnowHeightAO", "İz içi AO yok → izler düz görünüyor"),
            (LightingPath, "cosPhi * cosPhi", "AO cos² ortalaması değil"),
            (LightingPath, "crustMask", "Kabuk shading'i yok"),
        };

        bool all = true;

        foreach ((string file, string needle, string symptom) c in checks)
        {
            bool found = System.IO.File.Exists(c.file) &&
                         System.IO.File.ReadAllText(c.file).Contains(c.needle);

            all &= found;

            r.AppendLine("  [" + M(found) + "] " + c.needle.PadRight(24) +
                         (found ? "" : "EKSİK → " + c.symptom));
        }

        // YASAK: normal'ler lerp ile harmanlanmamalı (spec §14.2, §20).
        string detail = System.IO.File.ReadAllText(DetailPath);
        bool noLerpBlend = !detail.Contains("lerp(baseSample") && !detail.Contains("lerp(packed");
        all &= noLerpBlend;

        r.AppendLine("  [" + M(noLerpBlend) + "] normal harmanlamada lerp YOK");

        // YASAK: AO doğrudan ışığa uygulanmamalı — gölgeyi iki kez saymaktır
        // ve izleri siyah lekelere çevirir (spec §18.5, §22).
        string lighting = System.IO.File.ReadAllText(LightingPath);

        int aoInAmbient = lighting.IndexOf("ambient *= heightAO", System.StringComparison.Ordinal);
        bool aoOnlyAmbient = aoInAmbient >= 0 &&
                             !lighting.Contains("diffuse *= heightAO") &&
                             !lighting.Contains("lightCol * heightAO");

        all &= aoOnlyAmbient;
        r.AppendLine("  [" + M(aoOnlyAmbient) + "] AO YALNIZ ortamda      " +
                     (aoOnlyAmbient ? "" : "doğrudan ışığa da uygulanmış → izler siyah leke olur"));

        return all;
    }

    // ----------------------------------------------------------------- yardım

    static float Mag(Color c) => new Vector3(c.r, c.g, c.b).magnitude;

    static string M(bool ok) => ok ? "+" : "-";

    static RenderTexture NewRt(int res)
    {
        var rt = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            hideFlags = HideFlags.HideAndDontSave,
        };
        rt.Create();
        return rt;
    }

    static void Release(ref RenderTexture rt)
    {
        if (rt == null) return;
        rt.Release();
        Object.DestroyImmediate(rt);
        rt = null;
    }

    static Color[] Read(RenderTexture rt)
    {
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, false, true);
        tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
        tex.Apply(false);

        RenderTexture.active = prev;

        Color[] px = tex.GetPixels();
        Object.DestroyImmediate(tex);
        return px;
    }
}
