// ROL: bölge kaydırmasının ve snap ızgarasının doğruluğunu ÖLÇER. Göz kararı
// yok — her sınamanın tek doğru cevabı var, sayı olarak basılıyor.
// Çağıran: menü — To The Summit/Snow/Scroll Test.

using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// ARAÇ ÖNCE KENDİNİ DOĞRULUYOR (CLAUDE.md — "aracın kendisi önce doğrulanır").
///
/// Geri okuma yolunda y ekseni çevrilebiliyor: `RWTexture2D[id.xy]`'nin y'si ile
/// `Texture2D.GetPixels()`'in y'si aynı yöne bakmak zorunda değil. Bu sınama
/// çevrimi VARSAYMIYOR, damgadan ÖLÇÜYOR ve beklenen değerleri ona göre kuruyor.
/// Varsayılsaydı bir işaret hatası ya sahte başarısızlık ya da gizlenmiş bug olurdu.
public static class SnowScrollTest
{
    const int Res = 1024;
    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";
    const string StampPath = "Assets/Snow/Editor/SnowTestKernels.compute";
    const string ManagerPath = "Assets/Snow/Runtime/SnowManager.cs";

    static readonly Vector4 Edge = new(-1f, -2f, -3f, -4f);

    [MenuItem("To The Summit/Snow/Scroll Test", false, 49)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(4096);
        r.AppendLine("# Kar — kaydırma ve snap sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = true;
        ok &= ScrollTests(r);
        ok &= SnapTests(r);
        ok &= ReleaseTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // ---------------------------------------------------------------- kaydırma

    static bool ScrollTests(StringBuilder r)
    {
        r.AppendLine("## KScroll — içerik dünyaya çakılı kalıyor mu");

        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) { r.AppendLine("  [-] " + SimPath + " yüklenemedi."); return false; }

        var stampCs = AssetDatabase.LoadAssetAtPath<ComputeShader>(StampPath);
        if (stampCs == null) { r.AppendLine("  [-] " + StampPath + " yüklenemedi."); return false; }

        int stampKernel = stampCs.FindKernel("KStamp");
        int scrollKernel = sim.FindKernel("KScroll");
        int groups = Mathf.CeilToInt(Res / 8f);

        RenderTexture src = NewRT(Res);
        RenderTexture dst = NewRT(Res);

        bool all = true;

        try
        {
            // --- ARAÇ DOĞRULAMASI: damga geri okundu mu, y çevrildi mi ---
            stampCs.SetInt("_Resolution", Res);
            stampCs.SetTexture(stampKernel, "_Dst", src);
            stampCs.Dispatch(stampKernel, groups, groups, 1);

            Color[] stamp = Read(src);

            if (!DetectOrientation(stamp, r, out bool flipped)) return false;

            r.AppendLine("  [+] Okuma yolu           damga birebir geri geldi, y ekseni " +
                         (flipped ? "ÇEVRİK (hesaba katıldı)" : "aynı yönde"));

            // --- KAYDIRMA VAKALARI ---
            Vector2Int[] cases =
            {
                new(0, 0),          // birim: içerik hiç kaymamalı
                new(7, 3),
                new(-5, 11),
                new(4, -4),         // Medium'da bir SnapStep = 4 teksel
                new(Res, 0),        // tamamen dışarı: her teksel yeni şerit
                new(-1200, 1200),   // iki eksende de dışarı
            };

            foreach (Vector2Int d in cases)
            {
                sim.SetInt("_Resolution", Res);
                sim.SetVector("_ScrollTexels", new Vector4(d.x, d.y, 0f, 0f));
                sim.SetVector("_NewEdgeValue", Edge);
                sim.SetTexture(scrollKernel, "_Src", src);
                sim.SetTexture(scrollKernel, "_Dst", dst);
                sim.Dispatch(scrollKernel, groups, groups, 1);

                Color[] got = Read(dst);

                int bad = 0;
                float maxErr = 0f;
                int edgeCount = 0;

                for (int ay = 0; ay < Res; ay++)
                for (int ax = 0; ax < Res; ax++)
                {
                    // Dizi indeksinden GPU id'sine: çevrim ölçüldü, varsayılmadı.
                    int gy = flipped ? Res - 1 - ay : ay;
                    int sx = ax + d.x;
                    int sy = gy + d.y;

                    bool inside = sx >= 0 && sx < Res && sy >= 0 && sy < Res;

                    float ex = inside ? sx : Edge.x;
                    float ey = inside ? sy : Edge.y;
                    if (!inside) edgeCount++;

                    Color c = got[ay * Res + ax];
                    float e = Mathf.Max(Mathf.Abs(c.r - ex), Mathf.Abs(c.g - ey));

                    if (e > 0f) { bad++; if (e > maxErr) maxErr = e; }
                }

                bool pass = bad == 0;
                all &= pass;

                r.AppendLine("  [" + (pass ? "+" : "-") + "] delta " +
                    ("(" + d.x + ", " + d.y + ")").PadRight(16) +
                    " uyuşmayan " + bad + " / " + (Res * Res) +
                    "   yeni şerit " + edgeCount +
                    (pass ? "" : "   MAKS HATA " + maxErr.ToString("F3")));
            }
        }
        finally
        {
            Release(ref src);
            Release(ref dst);
        }

        return all;
    }

    /// Damgadan y yönünü ölçüyor. x hiç çevrilmez; çevrilmişse araç bozuktur ve
    /// sınama durur — bozuk araçla kernel suçlamak bir tur yakar.
    static bool DetectOrientation(Color[] stamp, StringBuilder r, out bool flipped)
    {
        flipped = false;

        float g00 = stamp[0].g;
        bool guess = Mathf.Abs(g00 - (Res - 1)) < 0.5f;

        if (!guess && Mathf.Abs(g00) > 0.5f)
        {
            r.AppendLine("  [-] Okuma yolu           damga bozuk: (0,0)'da G = " +
                         g00.ToString("F3") + ", 0 ya da " + (Res - 1) + " olmalıydı. " +
                         "ARAÇ GÜVENİLMEZ, kaydırma sınanmadı.");
            return false;
        }

        for (int ay = 0; ay < Res; ay++)
        for (int ax = 0; ax < Res; ax++)
        {
            Color c = stamp[ay * Res + ax];
            float ex = ax;
            float ey = guess ? Res - 1 - ay : ay;

            if (Mathf.Abs(c.r - ex) > 0f || Mathf.Abs(c.g - ey) > 0f)
            {
                r.AppendLine("  [-] Okuma yolu           damga (" + ax + "," + ay + ")'de " +
                             "beklenen (" + ex + "," + ey + "), gelen (" +
                             c.r.ToString("F3") + "," + c.g.ToString("F3") + "). " +
                             "ARAÇ GÜVENİLMEZ, kaydırma sınanmadı.");
                return false;
            }
        }

        flipped = guess;
        return true;
    }

    // -------------------------------------------------------------------- snap

    static bool SnapTests(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## SnapToTexelGrid — kesirli snap var mı");

        bool all = true;

        foreach (SnowQualityPreset p in System.Enum.GetValues(typeof(SnowQualityPreset)))
        {
            SnowQualityData q = SnowQuality.Get(p);
            float texel = q.TexelSize;
            float ratio = q.SnapStep / texel;
            float err = Mathf.Abs(ratio - q.ScrollTexels);

            // Bir SnapStep tam sayı teksele denk gelmezse merkez teksel altı
            // titrer; belirtisi izlerin kayması olur (spec §6.4).
            //
            // FLOAT ORANI TAM SAYI HESABIYLA KARŞILAŞTIRILIYOR. Spec'in ilk
            // hâli float'ı üç haneye yuvarlayıp "tam" demişti; gerçek oran
            // 4.0078'di ve hata orada saklandı.
            bool pass = err < 1e-4f;
            all &= pass;

            r.AppendLine("  [" + (pass ? "+" : "-") + "] " + p.ToString().PadRight(8) +
                " teksel " + (texel * 100f).ToString("F4") + " cm   " +
                "SnapStep / teksel = " + ratio.ToString("F6") +
                "   tam sayı hesabı " + q.ScrollTexels +
                (pass ? "  (uyuyor)" : "  AYRIŞMA — snap bozuk"));
        }

        // Gerçek dünya süpürmesi: sahnedeki koordinat mertebesinde (~-7500 m)
        // büyük sayı hassasiyeti snap'i bozuyor mu.
        SnowQualityData med = SnowQuality.Get(SnowQualityPreset.Medium);
        float t = med.AreaSize / med.Resolution;

        const float X0 = -7494f;
        float maxDev = 0f;
        int backwards = 0;
        int prev = int.MinValue;
        int steps = 0;

        for (float x = X0; x <= X0 + 20f; x += 0.01f)
        {
            Vector2Int c = SnowManager.SnapToTexelGrid(new Vector3(x, 0f, x), t, med.SnapStep);

            float want = Mathf.Floor(x / med.SnapStep) * med.SnapStep;
            float got = c.x * t;

            maxDev = Mathf.Max(maxDev, Mathf.Abs(got - want));
            if (c.x < prev) backwards++;
            prev = c.x;
            steps++;
        }

        bool sweepPass = maxDev < t * 0.5f && backwards == 0;
        all &= sweepPass;

        r.AppendLine("  [" + (sweepPass ? "+" : "-") + "] Süpürme  x = -7494 → -7474, " +
            steps + " adım   maks sapma " + (maxDev * 1000f).ToString("F4") + " mm " +
            "(sınır " + (t * 500f).ToString("F3") + " mm)   geri sıçrama " + backwards);

        return all;
    }

    // ---------------------------------------------------------------- sızıntı

    /// Her RenderTexture alanı OnDisable'da bırakılıyor mu. Yeni bir doku eklenip
    /// bırakma unutulursa Play'den çıkışta sızıntı olur; belirtisi editörün
    /// birkaç Play turundan sonra şişmesi, sebebi ise günler sonra aranır.
    static bool ReleaseTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## SnowManager — her doku bırakılıyor mu");

        string src = System.IO.File.ReadAllText(ManagerPath);

        var declared = new List<string>();

        foreach (Match m in Regex.Matches(src, @"^\s*RenderTexture\s+([A-Za-z0-9_,\s]+);",
                                          RegexOptions.Multiline))
            foreach (string n in m.Groups[1].Value.Split(','))
                declared.Add(n.Trim());

        var released = new HashSet<string>();
        foreach (Match m in Regex.Matches(src, @"Release\(ref\s+([A-Za-z0-9_]+)\)"))
            released.Add(m.Groups[1].Value);

        bool all = declared.Count > 0;
        var missing = new StringBuilder();

        foreach (string n in declared)
            if (!released.Contains(n)) { all = false; missing.Append(' ').Append(n); }

        r.AppendLine("  [" + (all ? "+" : "-") + "] " + declared.Count +
            " RenderTexture alanı, " + released.Count + " bırakma" +
            (all ? "" : "   BIRAKILMAYAN:" + missing));

        return all;
    }

    // ------------------------------------------------------------------ yardım

    static RenderTexture NewRT(int res)
    {
        var rt = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBHalf)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
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

    /// RGBAFloat + linear: yarım kayan noktadaki tam sayılar (≤ 2048) birebir
    /// geliyor, gama dönüşümü araya girmiyor.
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
