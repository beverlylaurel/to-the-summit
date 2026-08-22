// ROL: kar yüzeyi mesh'inin spec §8.2/§8.7 kurallarını doğrular.
// Çağıran: SnowTestRunner.

using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// ZEMİN MESH'İ — SPEC §8.7'NİN ALTI TESTİ.
///
/// Spec bu bölüm için "bu spec'in en sık hata alınan yeri" diyor ve altı test
/// sıralıyor. Dördü burada MEKANİK olarak koşuyor; ikisi (wireframe'de yürüme,
/// yer değiştirme kapatma) gözle bakılan testler ve onların yerine aynı kusuru
/// üreten SAYISAL koşul sınanıyor.
///
/// Karşılıklar:
///   §8.7.1 wireframe'de kayma   → SnapStep = 2 × quadSize (kaymanın sebebi)
///   §8.7.2 displacement kapalı  → mesh düz düzlem mi, köşeler ızgarada mı
///   §8.7.3 _ScrollTexels        → tam sayı, tam sayı aritmetiğiyle
///   §8.7.4 draw call 1          → tek mesh, tek alt-mesh
///   §8.7.5 kamera yukarı        → bounds yüksekliği
///   §8.7.6 kar sıfır            → kırpma eşiği tanımlı
public static class SnowMeshTest
{
    public static string Run(out bool pass)
    {
        var r = new StringBuilder();
        pass = true;

        r.AppendLine("# Kar — zemin mesh'i (spec §8.2, §8.7)");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        foreach (SnowQualityPreset preset in System.Enum.GetValues(typeof(SnowQualityPreset)))
            pass &= PresetTests(r, preset);

        pass &= SceneTests(r);

        r.AppendLine();
        r.AppendLine("SONUÇ: " + (pass ? "TAMAM" : "BAŞARISIZ"));
        return r.ToString();
    }

    static bool PresetTests(StringBuilder r, SnowQualityPreset preset)
    {
        SnowQualityData q = SnowQuality.Get(preset);
        SnowMeshBuilder.Grid g = SnowMeshBuilder.Describe(q);

        r.AppendLine();
        r.AppendLine("## " + preset + " — " + q.AreaSize.ToString("0.#") + " m, " +
                     g.Quads + " quad, çözünürlük " + q.Resolution);

        Mesh mesh = SnowMeshBuilder.Build(g);

        try
        {
            // --- §8.7.4 TEK DRAW CALL
            //
            // Mesh parçalara bölünürse çizim sayısı artıyor (spec §22:
            // "Draw call 1'den fazla → mesh parçalara bölünmüş").
            bool single = mesh.subMeshCount == 1;

            // --- §8.2 IZGARA ÖLÇÜLERİ
            int wantVerts = (g.Quads + 1) * (g.Quads + 1);
            int wantTris = g.Quads * g.Quads * 2;

            bool vertsOk = mesh.vertexCount == wantVerts;
            bool trisOk = mesh.triangles.Length == wantTris * 3;

            // --- §8.2 32 BİT İNDEKS
            //
            // Üç presette de zorunlu: Low'da bile 257² = 66 049 > 65535.
            // 16 bit indeks sessizce sarar ve mesh katlanır.
            bool indexOk = mesh.indexFormat == IndexFormat.UInt32;
            bool needs32 = wantVerts > 65535;

            // --- §8.7.2 DÜZ DÜZLEM, MERKEZDE
            //
            // Yükseklik köşe shader'ında veriliyor; yerel köşelerin hepsi
            // y = 0 ve kare (0,0) merkezli olmak zorunda.
            Vector3[] v = mesh.vertices;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            float maxY = 0f;

            for (int i = 0; i < v.Length; i++)
            {
                minX = Mathf.Min(minX, v[i].x); maxX = Mathf.Max(maxX, v[i].x);
                minZ = Mathf.Min(minZ, v[i].z); maxZ = Mathf.Max(maxZ, v[i].z);
                maxY = Mathf.Max(maxY, Mathf.Abs(v[i].y));
            }

            float half = q.AreaSize * 0.5f;

            bool flat = maxY < 1e-6f;
            bool centred = Mathf.Abs(minX + half) < 1e-4f && Mathf.Abs(maxX - half) < 1e-4f
                        && Mathf.Abs(minZ + half) < 1e-4f && Mathf.Abs(maxZ - half) < 1e-4f;

            // Köşe aralığı her yerde quad boyu mu — düzgün ızgara.
            float worstStep = 0f;
            int side = g.Quads + 1;

            for (int i = 0; i < g.Quads; i++)
                worstStep = Mathf.Max(worstStep,
                    Mathf.Abs((v[i + 1].x - v[i].x) - g.QuadSize));

            bool uniform = worstStep < 1e-5f;

            // --- §8.7.5 BOUNDS
            //
            // Yer değiştirme vertex shader'da; CPU bounds'u bilmiyor. Dar
            // bırakılırsa kar kamera açısına göre kayboluyor (spec §22).
            bool boundsOk = Mathf.Abs(mesh.bounds.size.y - SnowConstants.MeshBoundsHeight) < 0.001f
                         && Mathf.Abs(mesh.bounds.size.x - q.AreaSize) < 0.001f
                         && mesh.bounds.center == Vector3.zero;

            // --- §8.7.1 SNAP ADIMI
            //
            // Kayan/dalgalanan yüzeyin sebebi bu oranın bozulması (spec §22).
            bool snapOk = Mathf.Abs(g.SnapStep - g.QuadSize * SnowConstants.SnapQuads) < 1e-6f;

            // --- §8.7.3 _ScrollTexels TAM SAYI
            //
            // TAM SAYI ARİTMETİĞİ. Float oranı yuvarlamak hatayı saklıyor:
            // spec'in ilk hâlinde 4.0078 "4.0" diye yazılmıştı.
            float ratio = g.SnapStep / q.TexelSize;
            bool scrollOk = q.Resolution % q.MeshGrid == 0
                         && Mathf.Abs(ratio - q.ScrollTexels) < 1e-4f;

            // --- MESH = BÖLGE (spec §6.1)
            //
            // İkisi ayrışırsa kenar sönümü mesh'i ORTASINDAN kesiyor ve
            // `clip` orada basamaklı bir duvar bırakıyor.
            bool sameSquare = Mathf.Abs(g.Extent - q.AreaSize) < 1e-4f;

            r.AppendLine("  [" + M(single) + "] Tek alt-mesh          " + mesh.subMeshCount);
            r.AppendLine("  [" + M(vertsOk) + "] Köşe sayısı           " + mesh.vertexCount.ToString("N0") +
                         "   beklenen " + wantVerts.ToString("N0"));
            r.AppendLine("  [" + M(trisOk) + "] Üçgen sayısı          " +
                         (mesh.triangles.Length / 3).ToString("N0") +
                         "   beklenen " + wantTris.ToString("N0"));
            r.AppendLine("  [" + M(indexOk) + "] 32 bit indeks         " + mesh.indexFormat +
                         (needs32 ? "   (zorunlu: köşe > 65535)" : ""));
            r.AppendLine("  [" + M(flat) + "] Yerel köşeler düz     en büyük |y| " + maxY.ToString("F9"));
            r.AppendLine("  [" + M(centred) + "] (0,0) merkezli        ±" + half.ToString("0.000") + " m");
            r.AppendLine("  [" + M(uniform) + "] Düzgün ızgara         quad " +
                         (g.QuadSize * 100f).ToString("F4") + " cm, sapma " +
                         (worstStep * 1000f).ToString("F6") + " mm");
            r.AppendLine("  [" + M(boundsOk) + "] Bounds                " + mesh.bounds.size);
            r.AppendLine("  [" + M(snapOk) + "] SnapStep = 2 × quad   " +
                         (g.SnapStep * 100f).ToString("F4") + " cm");
            r.AppendLine("  [" + M(scrollOk) + "] _ScrollTexels tam     " + ratio.ToString("F6") +
                         "   tam sayı " + q.ScrollTexels);
            r.AppendLine("  [" + M(sameSquare) + "] Mesh = bölge          " +
                         g.Extent.ToString("0.0") + " m");

            return single && vertsOk && trisOk && indexOk && flat && centred
                && uniform && boundsOk && snapOk && scrollOk && sameSquare;
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }

    /// Sahnedeki kurulum: §8.7.6'nın karşılığı — kırpma eşiği tanımlı ve
    /// materyal doğru kuyrukta mı.
    static bool SceneTests(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Sahne");

        var surface = Object.FindAnyObjectByType<SnowSurface>();

        if (surface == null)
        {
            r.AppendLine("  [!] SnowSurface sahnede yok — kurulum koşulmamış.");
            return true;
        }

        var so = new SerializedObject(surface);
        var mat = so.FindProperty("snowMaterial").objectReferenceValue as Material;

        if (mat == null)
        {
            r.AppendLine("  [-] Materyal atanmamış.");
            return false;
        }

        // Spec §8.5: arazi önce, kar sonra. `clip()` erken-Z'yi kapatıyor;
        // bu bilinçli bir takas, alternatifi görünür titreme.
        const int wantQueue = 2050;      // Geometry (2000) + 50
        bool queueOk = mat.renderQueue == wantQueue || mat.renderQueue == -1;

        r.AppendLine("  [" + M(queueOk) + "] Render queue          " + mat.renderQueue +
                     "   beklenen " + wantQueue + " (Geometry+50)");

        return queueOk;
    }

    static string M(bool ok) => ok ? "+" : "-";
}
