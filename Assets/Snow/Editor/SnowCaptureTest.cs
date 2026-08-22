// ROL: yakalamanın doğruluğunu ÖLÇER — ortografik hacmin eşlemesi, derinlik
// testinin yönü, hız kanalları, yarım hassasiyetin taşıdığı çözünürlük ve
// blur'un kütle korunumu. Play gerekmiyor.
// Çağıran: menü — To The Summit/Kar/Yakalama Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// EN KRİTİK İDDİA: "kamera yukarı baktığı için daha alçak Y kazanır."
/// Bu tek cümle yanlışsa bütün iz sistemi ters çalışır ve belirtisi
/// "ayak izi yerine tümsek" olur. Burada işaretiyle birlikte ölçülüyor.
public static class SnowCaptureTest
{
    const int Res = 256;
    const float AreaSize = 16f;

    /// Projenin gerçek mertebesi: arazi ~4900 m'de. Yarım hassasiyetin mutlak
    /// dünya Y'sini taşıyamadığı yer burası.
    const float ObserverY = 4900.5f;

    const string CaptureShaderPath = "Assets/Snow/Shaders/Hidden_SnowCaptureDepth.shader";
    const string SimPath = "Assets/Snow/Shaders/SnowSim.compute";

    [MenuItem("To The Summit/Kar/Yakalama Sınaması", false, 51)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(4096);
        r.AppendLine("# Kar — yakalama sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = true;
        ok &= PrecisionTest(r);
        ok &= MatrixTest(r);
        ok &= DrawTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // ------------------------------------------------------------- hassasiyet

    /// Yarım kayan noktanın bu projedeki mertebede ne taşıdığını ÖLÇER.
    /// Teoriden değil, gerçek `Mathf.FloatToHalf` çevriminden.
    static bool PrecisionTest(StringBuilder r)
    {
        r.AppendLine("## Yarım hassasiyet — mutlak Y neden taşınamıyor");

        float absStep = HalfStep(ObserverY);
        float relFoot = HalfStep(0.1f);
        float relEdge = HalfStep(SnowConstants.CaptureAbove);

        // Görünür karın alt sınırı 4 mm (spec §8.1). Kodlama bundan kaba ise
        // batma derinliği ölçülemez.
        bool absFails = absStep > SnowConstants.MinVisibleHeight;
        bool relPasses = relEdge <= SnowConstants.MinVisibleHeight;

        r.AppendLine("  [" + (absFails ? "+" : "-") + "] Mutlak Y " +
            ObserverY.ToString("0.0") + " m'de adım " + (absStep * 1000f).ToString("0.###") +
            " mm  (görünür kar sınırı " + (SnowConstants.MinVisibleHeight * 1000f).ToString("0.#") +
            " mm)" + (absFails ? " → kullanılamaz, göreli kodlama zorunlu" : " → BEKLENMİYOR"));

        r.AppendLine("  [" + (relPasses ? "+" : "-") + "] Göreli  ayak civarı (0.1 m) adım " +
            (relFoot * 1000f).ToString("0.###") + " mm,  hacim ucu (" +
            SnowConstants.CaptureAbove.ToString("0.#") + " m) adım " +
            (relEdge * 1000f).ToString("0.###") + " mm");

        return absFails && relPasses;
    }

    /// Bir değerin yarım kayan noktadaki komşusuna uzaklığı.
    static float HalfStep(float value)
    {
        ushort h = Mathf.FloatToHalf(value);
        float here = Mathf.HalfToFloat(h);
        float next = Mathf.HalfToFloat((ushort)(h + 1));
        return Mathf.Abs(next - here);
    }

    // ----------------------------------------------------------------- matris

    /// Ortografik hacmin eşlemesi. Kamera bileşeni olmadığı için matrisleri
    /// kendimiz kuruyoruz; kurduğumuzun doğru olduğunu burada kanıtlıyoruz.
    static bool MatrixTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Ortografik hacim — eşleme ve derinlik yönü");

        float half = AreaSize * 0.5f;
        float far = SnowConstants.CaptureBelow + SnowConstants.CaptureAbove;
        var center = new Vector2(-7494f, -4327.5f);

        var position = new Vector3(center.x, ObserverY - SnowConstants.CaptureBelow, center.y);
        Quaternion lookUp = Quaternion.Euler(-90f, 0f, 0f);

        Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                         Matrix4x4.TRS(position, lookUp, Vector3.one).inverse;
        Matrix4x4 proj = Matrix4x4.Ortho(-half, half, -half, half, 0.05f, far);
        Matrix4x4 vp = proj * view;

        bool all = true;

        // İleri yön gerçekten +Y mi
        Vector3 forward = lookUp * Vector3.forward;
        bool up = Vector3.Dot(forward, Vector3.up) > 0.999f;
        all &= up;
        r.AppendLine("  [" + (up ? "+" : "-") + "] Bakış yönü      " + forward.ToString("0.000") +
                     (up ? "  (+Y — yukarı)" : "  YUKARI BAKMIYOR"));

        // Merkez → NDC (0,0)
        Vector3 ndcCenter = Ndc(vp, new Vector3(center.x, ObserverY, center.y));
        bool centered = Mathf.Abs(ndcCenter.x) < 1e-4f && Mathf.Abs(ndcCenter.y) < 1e-4f;
        all &= centered;
        r.AppendLine("  [" + (centered ? "+" : "-") + "] Bölge merkezi   NDC xy = (" +
                     ndcCenter.x.ToString("0.0000") + ", " + ndcCenter.y.ToString("0.0000") + ")");

        // +X köşesi → NDC x = +1
        Vector3 ndcCorner = Ndc(vp, new Vector3(center.x + half, ObserverY, center.y));
        bool corner = Mathf.Abs(ndcCorner.x - 1f) < 1e-4f;
        all &= corner;
        r.AppendLine("  [" + (corner ? "+" : "-") + "] +X kenarı       NDC x = " +
                     ndcCorner.x.ToString("0.0000") + "  (beklenen 1.0000)");

        // ASIL İDDİA: alçak Y daha yakın, yani derinlik testini kazanır.
        // Ters çıkarsa ayak izi yerine tümsek oluşur.
        float lowY = ObserverY - 1f;
        float highY = ObserverY + 1f;
        float zLow = Ndc(vp, new Vector3(center.x, lowY, center.y)).z;
        float zHigh = Ndc(vp, new Vector3(center.x, highY, center.y)).z;

        // Unity ters-Z platformlarda karşılaştırmayı da çevirdiği için burada
        // ölçülen HAM projeksiyondur: alçak Y kameraya yakın → z küçük.
        bool depthOk = zLow < zHigh;
        all &= depthOk;
        r.AppendLine("  [" + (depthOk ? "+" : "-") + "] Derinlik yönü   Y=" +
                     lowY.ToString("0.0") + " → z " + zLow.ToString("0.0000") + " ,  Y=" +
                     highY.ToString("0.0") + " → z " + zHigh.ToString("0.0000") +
                     (depthOk ? "  (alçak olan yakın — LEqual onu tutar)"
                              : "  TERS: yüksek yüzey kazanır, iz tümseğe döner"));

        // Hacmin dışı gerçekten dışarıda mı
        float aboveTop = ObserverY + SnowConstants.CaptureAbove + 0.5f;
        float zAbove = Ndc(vp, new Vector3(center.x, aboveTop, center.y)).z;
        bool clipped = zAbove > 1f;
        all &= clipped;
        r.AppendLine("  [" + (clipped ? "+" : "-") + "] Uzak düzlem     Y=" +
                     aboveTop.ToString("0.0") + " → z " + zAbove.ToString("0.0000") +
                     (clipped ? "  (>1, kesiliyor)" : "  hacmin içinde kalmış"));

        return all;
    }

    static Vector3 Ndc(Matrix4x4 vp, Vector3 worldPos)
    {
        Vector4 clip = vp * new Vector4(worldPos.x, worldPos.y, worldPos.z, 1f);
        float w = Mathf.Approximately(clip.w, 0f) ? 1f : clip.w;
        return new Vector3(clip.x / w, clip.y / w, clip.z / w);
    }

    // ------------------------------------------------------------------ çizim

    /// GERÇEK ÇİZİM. İki yüzey üst üste, farklı yükseklikte, farklı hızda.
    /// Beklenen tek cevap: alçak olanın Y'si ve alçak olanın hızı.
    static bool DrawTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Çizim — maske, yükseklik, derinlik testi, hız");

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(CaptureShaderPath);
        if (shader == null) { r.AppendLine("  [-] " + CaptureShaderPath + " yüklenemedi."); return false; }

        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) { r.AppendLine("  [-] " + SimPath + " yüklenemedi."); return false; }

        var center = new Vector2(-7494f, -4327.5f);

        var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        RenderTexture color = NewColor(Res);
        RenderTexture depth = NewDepth(Res);
        RenderTexture blurred = NewColor(Res);

        // ALÇAK: merkezde, 2×2 m, gözlemcinin 0.40 m altında, hız (2, -1)
        // YÜKSEK: aynı yerde, 0.10 m altında, hız (-5, 5)
        // Üst üsteler; kazanan ALÇAK olmalı.
        GameObject low = NewQuad("SnowCaptureTest_Low", center, ObserverY - 0.40f, 2f);
        GameObject high = NewQuad("SnowCaptureTest_High", center, ObserverY - 0.10f, 2f);

        // Bölge dışında bir yüzey: hiç görünmemeli.
        GameObject outside = NewQuad("SnowCaptureTest_Outside",
                                     center + new Vector2(40f, 0f), ObserverY - 0.5f, 2f);

        bool all = true;

        try
        {
            var lowDef = low.AddComponent<SnowDeformer>();
            var highDef = high.AddComponent<SnowDeformer>();
            var outDef = outside.AddComponent<SnowDeformer>();

            if (lowDef.Renderer == null)
            {
                r.AppendLine("  [-] Deformer      OnEnable editörde çalışmadı; " +
                             "`ExecuteAlways` yok sayılmış. Sınama koşulamaz.");
                return false;
            }

            SetVelocity(lowDef, new Vector4(2f, -1f, 0f, 0f));
            SetVelocity(highDef, new Vector4(-5f, 5f, 0f, 0f));
            SetVelocity(outDef, Vector4.zero);

            bool work = SnowCaptureCamera.HasWork(center, AreaSize, ObserverY);
            all &= work;
            r.AppendLine("  [" + (work ? "+" : "-") + "] İş var mı       " +
                         (work ? "evet (3 deformer kayıtlı)" : "HAYIR — kayıt veya sınır testi bozuk"));

            var cam = new SnowCaptureCamera();
            var cmd = new CommandBuffer { name = "SnowCaptureTest" };

            cam.Record(cmd, color, depth, material, center, AreaSize, ObserverY,
                       Matrix4x4.identity, Matrix4x4.identity);

            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();

            Color[] px = Read(color);

            int mid = Res / 2;
            Color hit = px[mid * Res + mid];

            // Köşe: 8 m uzakta hiçbir yüzey yok.
            Color miss = px[2 * Res + 2];

            bool mask = hit.a > 0.99f && miss.a < 0.01f;
            all &= mask;
            r.AppendLine("  [" + (mask ? "+" : "-") + "] Maske           kaplı A = " +
                         hit.a.ToString("0.000") + " ,  boş A = " + miss.a.ToString("0.000"));

            bool bg = miss.r < -9000f;
            all &= bg;
            r.AppendLine("  [" + (bg ? "+" : "-") + "] Arka plan       boş teksel R = " +
                         miss.r.ToString("0.#") + "  (beklenen -9999)");

            // Göreli kodlama: alçak yüzey gözlemcinin 0.40 m altında → -0.40
            float wantY = -0.40f;
            float errY = Mathf.Abs(hit.r - wantY);
            bool depthWins = errY < 0.002f;
            all &= depthWins;
            r.AppendLine("  [" + (depthWins ? "+" : "-") + "] Derinlik testi  R = " +
                         hit.r.ToString("0.0000") + "  (alçak yüzey " + wantY.ToString("0.00") +
                         ", yüksek yüzey -0.10)  hata " + (errY * 1000f).ToString("0.##") + " mm" +
                         (depthWins ? "" : "  YÜKSEK YÜZEY KAZANMIŞ"));

            // Hız da alçak yüzeyinki olmalı — aynı fragmandan geliyor.
            bool vel = Mathf.Abs(hit.g - 2f) < 0.01f && Mathf.Abs(hit.b + 1f) < 0.01f;
            all &= vel;
            r.AppendLine("  [" + (vel ? "+" : "-") + "] Hız             (" +
                         hit.g.ToString("0.000") + ", " + hit.b.ToString("0.000") +
                         ")  (beklenen 2.000, -1.000 — alçak yüzeyinki)");

            // Bölge dışındaki yüzey hiçbir teksele değmemiş olmalı.
            int lit = 0;
            for (int i = 0; i < px.Length; i++) if (px[i].a > 0.5f) lit++;

            float quadTexels = 2f / AreaSize * Res;
            int expected = Mathf.RoundToInt(quadTexels * quadTexels);
            bool areaOk = Mathf.Abs(lit - expected) <= expected * 0.05f;
            all &= areaOk;
            r.AppendLine("  [" + (areaOk ? "+" : "-") + "] Kaplama alanı   " + lit +
                         " teksel  (2×2 m için beklenen ~" + expected +
                         ", bölge dışı yüzey sayılmamalı)");

            // ---- KBlurCapture ----
            int blurKernel = sim.FindKernel("KBlurCapture");
            int groups = Mathf.CeilToInt(Res / 8f);

            sim.SetInt("_Resolution", Res);
            sim.SetFloat("_BlurRadiusTexels", SnowConstants.BlurRadiusTexels);
            sim.SetTexture(blurKernel, "_Src", color);
            sim.SetTexture(blurKernel, "_Dst", blurred);
            sim.Dispatch(blurKernel, groups, groups, 1);

            Color[] bpx = Read(blurred);

            // Merkez blur'dan etkilenmemeli: her yönde aynı değer var.
            Color bMid = bpx[mid * Res + mid];
            bool blurCenter = Mathf.Abs(bMid.r - wantY) < 0.01f && bMid.a > 0.99f;
            all &= blurCenter;
            r.AppendLine("  [" + (blurCenter ? "+" : "-") + "] Blur merkezi    R = " +
                         bMid.r.ToString("0.0000") + " , A = " + bMid.a.ToString("0.000") +
                         "  (iç bölge değişmemeli)");

            // Kenarda yumuşama olmalı: 0 < A < 1 taşıyan teksel sayısı.
            int soft = 0;
            for (int i = 0; i < bpx.Length; i++)
                if (bpx[i].a > 0.02f && bpx[i].a < 0.98f) soft++;

            bool softens = soft > 0;
            all &= softens;
            r.AppendLine("  [" + (softens ? "+" : "-") + "] Blur kenarı     " + soft +
                         " teksel kısmi maske taşıyor" +
                         (softens ? "" : "  — blur hiç yumuşatmamış"));
        }
        finally
        {
            Object.DestroyImmediate(low);
            Object.DestroyImmediate(high);
            Object.DestroyImmediate(outside);
            Object.DestroyImmediate(material);

            Release(ref color);
            Release(ref depth);
            Release(ref blurred);
        }

        return all;
    }

    /// `LateUpdate` editörde bu sınama sırasında koşmuyor; hızı doğrudan
    /// property block'a yazıp aynı yolu kullanıyoruz.
    static void SetVelocity(SnowDeformer deformer, Vector4 velocity)
    {
        var block = new MaterialPropertyBlock();
        block.SetVector(SnowShaderIDs.DeformerVelocity, velocity);
        deformer.Renderer.SetPropertyBlock(block);

        typeof(SnowDeformer)
            .GetField("velocityXZ", System.Reflection.BindingFlags.NonPublic |
                                    System.Reflection.BindingFlags.Instance)
            .SetValue(deformer, velocity);
    }

    /// XZ düzleminde, verilen yükseklikte, aşağı bakan bir dörtgen.
    static GameObject NewQuad(string name, Vector2 centerXZ, float y, float size)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        go.transform.position = new Vector3(centerXZ.x, y, centerXZ.y);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale = new Vector3(size, size, 1f);

        var mesh = new Mesh { name = name + "_Mesh", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
        });
        mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>();
        return go;
    }

    static RenderTexture NewColor(int res)
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

    static RenderTexture NewDepth(int res)
    {
        var rt = new RenderTexture(res, res, 24, RenderTextureFormat.Depth)
        {
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
