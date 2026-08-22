// ROL: kar sisteminin zemin yüksekliği kaynağını sahnedeki GERÇEK araziye karşı
// ölçer.
// Çağıran: SnowTestRunner.

using System.Text;
using UnityEngine;

/// KAR YÜZEYİ DÜZ BİR LEVHA ÇIKIYORSA İLK BAKILACAK YER BURASI.
///
/// `SnowGroundHeight` yüksekliği Unity Terrain'den pişiriyor. Görünen dağ o
/// Terrain DEĞİLSE (ayrı bir mesh'se) doku sabit kalıyor, kar yüzeyi de araziyi
/// takip etmeyen düz bir kare oluyor. SWE sıfırken `clip(h - 0.004)` her şeyi
/// kestiği için bu hata GÖRÜNMÜYOR; kar gelince ortaya çıkıyor.
///
/// Ölçüm iki bağımsız kaynağı karşılaştırıyor: Terrain'in kendi yüksekliği ve
/// yukarıdan atılan ışının çarptığı yüzey.
public static class SnowGroundTest
{
    public static string Run()
    {
        var r = new StringBuilder();

        r.AppendLine("# Kar — zemin yüksekliği kaynağı");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude,
                                                         FindObjectsSortMode.None);

        r.AppendLine("## Sahnedeki Terrain");
        r.AppendLine("  Terrain sayısı           " + terrains.Length);

        if (terrains.Length == 0)
        {
            r.AppendLine();
            r.AppendLine("  [-] Terrain YOK. `SnowGroundHeight` UnityTerrain yolunda çalışamaz;");
            r.AppendLine("      görünen arazi mesh ise `groundSource = MeshBake` gerekiyor.");
            r.AppendLine();
            r.AppendLine("SONUÇ: BAŞARISIZ");
            return r.ToString();
        }

        Terrain t = terrains[0];
        TerrainData td = t.terrainData;
        Vector3 pos = t.transform.position;
        Vector3 size = td.size;

        r.AppendLine("  Ad                       " + t.name);
        r.AppendLine("  Konum                    " + pos.ToString("0.0"));
        r.AppendLine("  Boyut                    " + size.ToString("0.0"));
        r.AppendLine("  Heightmap çözünürlüğü    " + td.heightmapResolution);
        r.AppendLine("  Collider                 " +
                     (t.GetComponent<TerrainCollider>() != null ? "var" : "YOK"));
        r.AppendLine();

        // Terrain'in kendi yükseklik aralığı: pişen dokunun taşıyacağı aralık.
        float minH = float.MaxValue, maxH = float.MinValue;
        const int Probe = 32;

        for (int y = 0; y < Probe; y++)
        for (int x = 0; x < Probe; x++)
        {
            float h = td.GetInterpolatedHeight(x / (float)(Probe - 1), y / (float)(Probe - 1));
            minH = Mathf.Min(minH, h);
            maxH = Mathf.Max(maxH, h);
        }

        bool hasRelief = (maxH - minH) > 1f;

        r.AppendLine("## Terrain gerçekten engebeli mi");
        r.AppendLine("  [" + M(hasRelief) + "] Yükseklik aralığı    " +
                     minH.ToString("0.0") + " – " + maxH.ToString("0.0") + " m  " +
                     "(fark " + (maxH - minH).ToString("0.0") + " m)");

        if (!hasRelief)
            r.AppendLine("      Terrain DÜZ. Pişen doku her yerde aynı değeri taşır ve kar " +
                         "yüzeyi düz bir levha olur.");

        r.AppendLine();

        // İkinci bağımsız kaynak: yukarıdan ışın. Görünen yüzey neyse ona çarpar.
        r.AppendLine("## Görünen yüzey Terrain mi (yukarıdan ışın)");

        var walker = Object.FindAnyObjectByType<FirstPersonController>();
        Vector3 center = walker != null ? walker.transform.position : pos + size * 0.5f;

        var offsets = new[]
        {
            Vector2.zero,
            new Vector2(50f, 0f), new Vector2(-50f, 0f),
            new Vector2(0f, 50f), new Vector2(0f, -50f),
            new Vector2(300f, 300f),
        };

        int hits = 0, terrainHits = 0, matches = 0;
        float worstGap = 0f;
        string worstName = "-";

        foreach (Vector2 o in offsets)
        {
            var xz = new Vector2(center.x + o.x, center.z + o.y);
            var origin = new Vector3(xz.x, pos.y + size.y + 500f, xz.y);

            string what;
            float rayY;

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 100000f))
            {
                hits++;
                rayY = hit.point.y;
                what = hit.collider.gameObject.name;
                if (hit.collider is TerrainCollider) terrainHits++;
            }
            else { rayY = float.NaN; what = "ÇARPMADI"; }

            // Terrain'in o noktadaki yüksekliği — dokunun taşıyacağı değer.
            float terrainY = pos.y + td.GetInterpolatedHeight(
                Mathf.Clamp01((xz.x - pos.x) / Mathf.Max(size.x, 1e-3f)),
                Mathf.Clamp01((xz.y - pos.z) / Mathf.Max(size.z, 1e-3f)));

            float gap = float.IsNaN(rayY) ? float.NaN : Mathf.Abs(rayY - terrainY);
            bool ok = !float.IsNaN(gap) && gap < 2f;

            if (ok) matches++;
            if (!float.IsNaN(gap) && gap > worstGap) { worstGap = gap; worstName = what; }

            r.AppendLine("  [" + M(ok) + "] (" + o.x.ToString("0") + "," + o.y.ToString("0") + ")".PadRight(10) +
                         "  terrain " + terrainY.ToString("0.0") + " m" +
                         "   ışın " + (float.IsNaN(rayY) ? "yok" : rayY.ToString("0.0") + " m") +
                         "   fark " + (float.IsNaN(gap) ? "-" : gap.ToString("0.0") + " m") +
                         "   çarpan: " + what);
        }

        r.AppendLine();
        r.AppendLine("  Işın çarptı              " + hits + " / " + offsets.Length);
        r.AppendLine("  TerrainCollider'a çarptı " + terrainHits + " / " + offsets.Length);
        r.AppendLine("  Terrain ile uyuşan       " + matches + " / " + offsets.Length);

        bool sourceIsVisible = matches == offsets.Length && hasRelief;

        r.AppendLine();

        if (!sourceIsVisible)
        {
            r.AppendLine("  [-] KAR YANLIŞ ZEMİNİ OKUYOR. En büyük fark " +
                         worstGap.ToString("0.0") + " m (çarpan: " + worstName + ").");
            r.AppendLine("      Kar yüzeyi Terrain'e göre yerleşiyor ama görünen arazi o değil.");
        }
        else
        {
            r.AppendLine("  [+] Kar sisteminin okuduğu zemin, görünen arazinin AYNISI.");
        }

        r.AppendLine();
        r.AppendLine("SONUÇ: " + (sourceIsVisible ? "TAMAM" : "BAŞARISIZ"));
        return r.ToString();
    }

    static string M(bool ok) => ok ? "+" : "-";
}
