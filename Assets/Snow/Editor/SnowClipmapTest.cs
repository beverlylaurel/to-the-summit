// ROL: zemin mesh'ini ÖLÇER — halka ölçüleri, deliğin gerçekten kapanıp
// kapanmadığı, üçgen bütçesi, mesh sınırları, shader'ın hatasız derlenmesi.
// Çağıran: menü — To The Summit/Kar/Clipmap Sınaması.

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// EN KRİTİK İDDİA: "halkalar arası çatlak yok."
///
/// Her halka KENDİ ızgarasına snap'leniyor, yani iç halka dış halkanın
/// deliğine göre kayıyor. Kayma deliğin payını aşarsa yerde gerçek bir yarık
/// açılır ve altındaki çıplak arazi görünür. Burada bütün olası kaymalar
/// TEK TEK deneniyor — göz kararı değil, tüketici sayım.
public static class SnowClipmapTest
{
    const string ShaderPath = "Assets/Snow/Shaders/SnowLit.shader";

    [MenuItem("To The Summit/Kar/Clipmap Sınaması", false, 53)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Kar — clipmap sınaması");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = true;
        ok &= GeometryTest(r);
        ok &= CoverageTest(r);
        ok &= MeshTest(r);
        ok &= ShaderTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "SONUÇ: TAMAM — bütün sınamalar geçti."
                        : "SONUÇ: BAŞARISIZ — yukarıdaki satırlara bakın.");
        return r.ToString();
    }

    // ---------------------------------------------------------------- ölçüler

    static bool GeometryTest(StringBuilder r)
    {
        r.AppendLine("## Halka ölçüleri (spec §13.1)");

        bool all = true;

        foreach (SnowQualityPreset preset in System.Enum.GetValues(typeof(SnowQualityPreset)))
        {
            SnowQualityData q = SnowQuality.Get(preset);
            SnowMeshBuilder.Ring[] rings = SnowMeshBuilder.Describe(q);

            bool countOk = rings.Length == q.RingCount;
            all &= countOk;

            long triangles = 0;
            foreach (SnowMeshBuilder.Ring ring in rings)
                triangles += 2L * (ring.Grid * ring.Grid - ring.HoleQuads * ring.HoleQuads);

            r.AppendLine("  [" + M(countOk) + "] " + preset.ToString().PadRight(8) +
                         rings.Length + " halka,  " + (triangles / 1000f).ToString("0") +
                         " bin üçgen");

            foreach (SnowMeshBuilder.Ring ring in rings)
                r.AppendLine("      halka " + ring.Index + ": " +
                             ring.Extent.ToString("0.#").PadLeft(5) + " m,  quad " +
                             (ring.QuadSize * 100f).ToString("0.00").PadLeft(6) + " cm,  snap " +
                             (ring.SnapStep * 100f).ToString("0.00").PadLeft(6) + " cm,  delik " +
                             ring.HoleQuads + "² quad = " +
                             (ring.HoleQuads * ring.QuadSize).ToString("0.00") + " m");
        }

        // Spec §13.1: Medium ≈ 1.17 M üçgen, 4 çizim.
        SnowMeshBuilder.Ring[] medium = SnowMeshBuilder.Describe(SnowQuality.Get(SnowQualityPreset.Medium));
        long mediumTris = 0;
        foreach (SnowMeshBuilder.Ring ring in medium)
        {
            mediumTris += 2L * (ring.Grid * ring.Grid - ring.HoleQuads * ring.HoleQuads);
            if (ring.Outermost) mediumTris += ring.Grid * 4L * 2L;
        }

        bool budget = mediumTris < 1_300_000;
        all &= budget;
        r.AppendLine("  [" + M(budget) + "] Medium bütçesi " + mediumTris.ToString("N0") +
                     " üçgen  (spec tahmini ~1.17 M, tavan 1.30 M)");

        return all;
    }

    // ---------------------------------------------------------------- kapanma

    /// DELİK HER KAYMADA KAPANIYOR MU. İç halkanın merkezi dış halkanınkine
    /// göre kendi snap adımının katları kadar sapabiliyor; hepsi deneniyor.
    static bool CoverageTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Halkalar arası çatlak (bütün olası kaymalar)");

        bool all = true;

        foreach (SnowQualityPreset preset in System.Enum.GetValues(typeof(SnowQualityPreset)))
        {
            SnowQualityData q = SnowQuality.Get(preset);
            SnowMeshBuilder.Ring[] rings = SnowMeshBuilder.Describe(q);

            for (int i = 1; i < rings.Length; i++)
            {
                SnowMeshBuilder.Ring outer = rings[i];
                SnowMeshBuilder.Ring inner = rings[i - 1];

                float holeHalf = outer.HoleQuads * outer.QuadSize * 0.5f;
                float innerHalf = inner.Extent * 0.5f;

                float worstGap = float.NegativeInfinity;
                float worstOverlap = float.PositiveInfinity;
                int steps = Mathf.RoundToInt(outer.SnapStep / inner.SnapStep);

                // Dünya konumu ne olursa olsun iki merkez arasındaki fark, iç
                // halkanın snap adımının tam katlarıdır ve dış adımı aşmaz.
                for (int k = 0; k < steps; k++)
                {
                    float offset = k * inner.SnapStep;

                    // İç halka: [offset − innerHalf, offset + innerHalf]
                    // Delik:    [−holeHalf, +holeHalf]
                    float marginLow = -holeHalf - (offset - innerHalf);   // >0 ise örtüyor
                    float marginHigh = (offset + innerHalf) - holeHalf;

                    float margin = Mathf.Min(marginLow, marginHigh);

                    worstGap = Mathf.Max(worstGap, -margin);
                    worstOverlap = Mathf.Min(worstOverlap, margin);
                }

                bool sealed_ = worstGap <= 0f;
                all &= sealed_;

                r.AppendLine("  [" + M(sealed_) + "] " + preset.ToString().PadRight(8) +
                             "halka " + (i - 1) + "→" + i + ":  " + steps +
                             " olası kayma,  en kötü örtüşme " +
                             (worstOverlap * 100f).ToString("0.00") + " cm" +
                             (sealed_ ? "" : "  ÇATLAK " + (worstGap * 100f).ToString("0.00") + " cm"));
            }
        }

        return all;
    }

    // -------------------------------------------------------------------- mesh

    static bool MeshTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Mesh (Medium)");

        SnowQualityData q = SnowQuality.Get(SnowQualityPreset.Medium);
        SnowMeshBuilder.Ring[] rings = SnowMeshBuilder.Describe(q);

        bool all = true;

        for (int i = 0; i < rings.Length; i++)
        {
            SnowMeshBuilder.Ring ring = rings[i];
            Mesh mesh = SnowMeshBuilder.Build(ring);

            try
            {
                // ETEK: en dış halkanın dört kenarında quad başına iki üçgen
                // (rapor §5). Sayılmazsa sınama etek eklenince patlar.
                int skirt = ring.Outermost ? ring.Grid * 4 * 2 * 3 : 0;

                int expected = (ring.Grid * ring.Grid - ring.HoleQuads * ring.HoleQuads) * 6 + skirt;
                int actual = (int)mesh.GetIndexCount(0);

                bool indexOk = actual == expected;
                bool formatOk = mesh.indexFormat == IndexFormat.UInt32;
                bool boundsOk = Mathf.Approximately(mesh.bounds.size.y, 600f) &&
                                Mathf.Approximately(mesh.bounds.size.x, ring.Extent);

                // DELİK GERÇEKTEN BOŞ MU. Delik bölgesine düşen tek bir üçgen
                // bile varsa iç halkayla üst üste biner ve z-fighting olur.
                int insideHole = CountTrianglesInHole(mesh, ring);
                bool holeOk = insideHole == 0;

                bool ok = indexOk && formatOk && boundsOk && holeOk;
                all &= ok;

                r.AppendLine("  [" + M(ok) + "] halka " + i + ":  " + (actual / 3).ToString("N0") +
                             " üçgen (beklenen " + (expected / 3).ToString("N0") + "),  " +
                             mesh.indexFormat + ",  sınır " + mesh.bounds.size.ToString("0") +
                             ",  delikte üçgen " + insideHole);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        return all;
    }

    static int CountTrianglesInHole(Mesh mesh, SnowMeshBuilder.Ring ring)
    {
        if (ring.HoleQuads == 0) return 0;

        Vector3[] vertices = mesh.vertices;
        int[] indices = mesh.triangles;

        float holeHalf = ring.HoleQuads * ring.QuadSize * 0.5f;
        int count = 0;

        for (int t = 0; t < indices.Length; t += 3)
        {
            Vector3 a = vertices[indices[t]];
            Vector3 b = vertices[indices[t + 1]];
            Vector3 c = vertices[indices[t + 2]];

            Vector3 centroid = (a + b + c) / 3f;

            if (Mathf.Abs(centroid.x) < holeHalf && Mathf.Abs(centroid.z) < holeHalf) count++;
        }

        return count;
    }

    // ------------------------------------------------------------------ shader

    static bool ShaderTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Shader");

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        if (shader == null)
        {
            r.AppendLine("  [-] " + ShaderPath + " yüklenemedi.");
            return false;
        }

        bool hasError = ShaderUtil.ShaderHasError(shader);
        bool supported = shader.isSupported;

        // Kuyruk 8.3'ün istediği yerde mi: arazi önce, kar sonra.
        int expectedQueue = (int)RenderQueue.Geometry + 50;
        bool queueOk = shader.renderQueue == expectedQueue;

        r.AppendLine("  [" + M(!hasError) + "] Derleme         " +
                     (hasError ? "HATA VAR" : "hatasız"));
        r.AppendLine("  [" + M(supported) + "] Destek          " +
                     (supported ? "destekleniyor" : "DESTEKLENMİYOR"));
        r.AppendLine("  [" + M(queueOk) + "] Kuyruk          " + shader.renderQueue +
                     "  (beklenen " + expectedQueue + " = Geometry+50, spec §8.3)");

        // MESAJLAR RAPORA YAZILIYOR. "Hata var" demek teşhis değil; hangi
        // satır olduğu yazılmazsa bir tur Editor.log kazmakla geçer.
        foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
            r.AppendLine("      [" + m.severity + "] " + m.file + "(" + m.line + "): " + m.message);

        return !hasError && supported && queueOk;
    }

    static string M(bool ok) => ok ? "+" : "-";
}
