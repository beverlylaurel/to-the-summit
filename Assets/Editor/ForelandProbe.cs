using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// ARAZİ ÖLÇÜMÜ. "Tepecik var mı", "iz oyulmuş mu", "hendekler nerede" soruları gözle
/// birkaç turda cevaplanamadı; her turda sayı değiştirildi ve sonuç yine "göremiyorum"
/// oldu. Bu araç ÜRETİLMİŞ ARAZİYİ okuyor — üretecin ne yapması gerektiğini değil, ne
/// yaptığını.
///
/// Dört ölçüm: ova engebesi, hatta dik kesit, hat boyunca tarama, hendek avı.
///
/// Ova ve yol dokusu oturunca silinir.
public static class ForelandProbe
{
    /// Örnek aralığı (metre). Arazi ızgarası 4.28 m/örnek; iki metre onu iki katı
    /// sıklıkta okuyor, yani ızgaranın taşıyabildiği her detay yakalanıyor.
    const float Step = 2f;

    /// Ölçüm hattının uzunluğu (metre).
    const float Length = 800f;

    // ------------------------------------------------------------- ova engebesi

    [MenuItem("To The Summit/Arazi/Ova Ölçümü", false, 23)]
    static void Measure()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogWarning("Sahnede terrain yok.");
            return;
        }

        Vector3 size = terrain.terrainData.size;
        Vector3 origin = terrain.transform.position;
        var centre = new Vector2(origin.x + size.x * 0.5f, origin.z + size.z * 0.5f);

        MountainRoute route = LoadRoute();

        // 1) Doğuş noktasından dağa doğru: oyuncunun fiilen yürüdüğü zemin.
        if (route != null && route.spawnSet)
        {
            Vector3 spawn = MountainRoute.ToWorld(route.spawn, terrain);
            var toMountain = (centre - new Vector2(spawn.x, spawn.z)).normalized;
            Report(terrain, "Doğuştan dağa doğru", new Vector2(spawn.x, spawn.z), toMountain);
        }

        // 2) Ovadan teğet bir hat. İlk sürüm dağın üstündeydi: köşegen ofset 0.30
        // verilmişti ama merkeze uzaklık 0.42 çıkıyor ve dağ yarıçapı 0.48. Ölçülen ova
        // değil yamaçtı. Şimdi ofset 0.42, yani uzaklık 0.59 — eteğin belirgin dışı.
        float offset = size.x * 0.42f;
        var plainStart = new Vector2(centre.x + offset, centre.y + offset);
        var tangent = new Vector2(-1f, 1f).normalized;

        float distance = (plainStart - centre).magnitude / size.x;
        Report(terrain, $"Ova (merkeze uzaklık {distance:F2}, dağ yarıçapı 0.48)",
               plainStart, tangent);
    }

    static void Report(Terrain terrain, string label, Vector2 start, Vector2 direction)
    {
        int count = Mathf.RoundToInt(Length / Step);
        var heights = new float[count];

        for (int i = 0; i < count; i++)
        {
            Vector2 point = start + direction * (i * Step);
            heights[i] = terrain.SampleHeight(new Vector3(point.x, 0f, point.y))
                       + terrain.transform.position.y;
        }

        float low = float.MaxValue, high = float.MinValue;
        foreach (float h in heights) { low = Mathf.Min(low, h); high = Mathf.Max(high, h); }

        // YEREL KABARTI: 60 metrelik pencerede tepe-çukur farkı. Toplam aralık eğimi de
        // içeriyor ve düz bir rampa da yüksek çıkıyor; tepecik sorusunun cevabı pencere
        // içindeki fark.
        int window = Mathf.RoundToInt(60f / Step);
        float relief = 0f, reliefSum = 0f;
        int windows = 0;

        for (int i = 0; i + window < count; i += window)
        {
            float wLow = float.MaxValue, wHigh = float.MinValue;
            for (int k = i; k < i + window; k++)
            {
                wLow = Mathf.Min(wLow, heights[k]);
                wHigh = Mathf.Max(wHigh, heights[k]);
            }

            relief = Mathf.Max(relief, wHigh - wLow);
            reliefSum += wHigh - wLow;
            windows++;
        }

        // TEPE SAYISI. Komşu örneğe sabit fark aramak hep sıfır basıyordu: 60 metre
        // genişliğinde 6 metrelik bir tümseğin tepesinde iki metrede düşüş 2.7 cm.
        // Şimdi ±10 metrelik pencerede yerel en yüksek aranıyor.
        int reach = Mathf.RoundToInt(10f / Step);
        int peaks = 0;

        for (int i = reach; i < count - reach; i++)
        {
            bool top = true;
            for (int k = i - reach; k <= i + reach && top; k++)
                if (k != i && heights[k] >= heights[i]) top = false;

            if (top) peaks++;
        }

        float maxSlope = 0f;
        for (int i = 1; i < count; i++)
            maxSlope = Mathf.Max(maxSlope, Mathf.Abs(heights[i] - heights[i - 1]) / Step);

        float average = windows > 0 ? reliefSum / windows : 0f;
        float spacing = peaks > 0 ? Length / peaks : 0f;

        Debug.Log($"[Ova ölçümü] {label}\n"
                + $"  toplam aralık   {high - low:F1} m\n"
                + $"  60 m pencerede  en fazla {relief:F1} m, ortalama {average:F1} m\n"
                + $"  tepe sayısı     {peaks} ({Length:F0} m boyunca, {spacing:F0} m arayla)\n"
                + $"  en dik parça    %{maxSlope * 100f:F0}");
    }

    // -------------------------------------------------------------- patika izi

    /// HATTA DİK KESİT. İz oyulduysa ortada bir çukur görünür.
    [MenuItem("To The Summit/Arazi/Patika Kesiti", false, 24)]
    static void CrossSection()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        MountainRoute route = LoadRoute();
        if (terrain == null || route == null) return;

        foreach (MountainRoute.Branch branch in route.branches)
            Section(terrain, branch.name, branch.marks);

        Section(terrain, "Yol", route.road);
    }

    static void Section(Terrain terrain, string label, List<MountainRoute.Mark> marks)
    {
        if (marks.Count < 6) return;

        // Hattın ortasından örnek: uçlarda kavşak ve bitiş etkisi var.
        int middle = marks.Count / 2;
        Vector3 here = MountainRoute.ToWorld(marks[middle].position, terrain);
        Vector3 next = MountainRoute.ToWorld(marks[middle + 1].position, terrain);

        var along = new Vector2(next.x - here.x, next.z - here.z).normalized;
        var across = new Vector2(-along.y, along.x);

        var line = new StringBuilder();
        float centreHeight = 0f, edgeHeight = 0f;
        int edgeCount = 0;

        for (float offset = -40f; offset <= 40f; offset += 4f)
        {
            float h = terrain.SampleHeight(new Vector3(here.x + across.x * offset, 0f,
                                                       here.z + across.y * offset))
                    + terrain.transform.position.y;

            if (Mathf.Abs(offset) < 1f) centreHeight = h;
            if (Mathf.Abs(offset) >= 32f) { edgeHeight += h; edgeCount++; }

            line.Append($"\n  {offset,5:F0} m   {h:F2}");
        }

        edgeHeight /= Mathf.Max(1, edgeCount);

        Debug.Log($"[Patika kesiti] {label} (yarıçap {marks[middle].radius:F1} m)\n"
                + $"  merkez {centreHeight:F2} m, kenar ortalaması {edgeHeight:F2} m\n"
                + $"  >>> İZ DERİNLİĞİ {edgeHeight - centreHeight:F2} m <<<"
                + line);
    }

    // ---------------------------------------------------------- boyunca tarama

    /// HAT BOYUNCA TARAMA. Tek kesit yanıltıcı: bir nokta yarmaya, öteki dolguya
    /// düşebiliyor. Bu ölçüm hat boyunca kırk noktada "burada oyuk var mı" diye soruyor.
    [MenuItem("To The Summit/Arazi/Rota Boyunca Tarama", false, 25)]
    static void ScanRoute()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        MountainRoute route = LoadRoute();
        if (terrain == null || route == null) return;

        Scan(terrain, "Yol", route.road);
        foreach (MountainRoute.Branch branch in route.branches)
            Scan(terrain, branch.name, branch.marks);
    }

    static void Scan(Terrain terrain, string label, List<MountainRoute.Mark> marks)
    {
        if (marks.Count < 12)
        {
            Debug.Log($"[Rota taraması] {label}: nokta az ({marks.Count})");
            return;
        }

        int step = Mathf.Max(1, marks.Count / 40);
        int cut = 0, fill = 0, flat = 0, samples = 0;
        float deepest = 0f, sum = 0f;

        for (int i = step; i < marks.Count - step; i += step)
        {
            Vector3 here = MountainRoute.ToWorld(marks[i].position, terrain);
            Vector3 next = MountainRoute.ToWorld(marks[i + 1].position, terrain);

            var along = new Vector2(next.x - here.x, next.z - here.z);
            if (along.sqrMagnitude < 1e-4f) continue;

            along.Normalize();
            var across = new Vector2(-along.y, along.x);

            float centre = terrain.SampleHeight(here);

            float side = 0f;
            foreach (float offset in new[] { -40f, -30f, 30f, 40f })
                side += terrain.SampleHeight(new Vector3(here.x + across.x * offset, 0f,
                                                         here.z + across.y * offset));
            side *= 0.25f;

            float depth = side - centre;
            sum += depth;
            samples++;
            deepest = Mathf.Max(deepest, depth);

            if (depth > 0.4f) cut++;
            else if (depth < -0.4f) fill++;
            else flat++;
        }

        if (samples == 0) return;

        Debug.Log($"[Rota taraması] {label} — {samples} nokta\n"
                + $"  ortalama oyuk {sum / samples:F2} m, en derin {deepest:F2} m\n"
                + $"  oyuk {cut}, dolgu {fill}, dokunulmamış {flat}"
                + $"   (%{100 * cut / samples} oyuk)");
    }

    // -------------------------------------------------------------- hendek avı

    /// HENDEK AVI. "Tesviye yanlış yerde" iddiasını doğrudan sınayan ölçüm: arazi
    /// taranıp en derin oyuklar bulunuyor, sonra her birinin EN YAKIN ROTA NOKTASINA
    /// uzaklığı yazılıyor.
    ///
    /// Hepsi koridorun içindeyse (30 metrenin altı) kazı rotanın üstünde demektir ve
    /// görülen hendekler yolun kendisi. Uzaktaysa koordinat hatası var.
    [MenuItem("To The Summit/Arazi/Hendek Avı", false, 26)]
    static void HuntTrenches()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        MountainRoute route = LoadRoute();
        if (terrain == null || route == null) return;

        TerrainData data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        float cell = data.size.x / (res - 1);
        float vertical = data.size.y;
        Vector3 origin = terrain.transform.position;

        // Çevre yarıçapı 8 hücre (~34 m): koridorun dışı. Oyuk, hücrenin bu halkanın
        // ortalamasından ne kadar alçak olduğu.
        const int Ring = 8;

        var found = new List<(float Depth, Vector2 Point)>();

        for (int z = Ring; z < res - Ring; z += 2)
        for (int x = Ring; x < res - Ring; x += 2)
        {
            float here = heights[z, x];
            float ring = (heights[z - Ring, x] + heights[z + Ring, x]
                        + heights[z, x - Ring] + heights[z, x + Ring]) * 0.25f;

            float depth = (ring - here) * vertical;
            if (depth < 1.5f) continue;

            found.Add((depth, new Vector2(origin.x + x * cell, origin.z + z * cell)));
        }

        if (found.Count == 0)
        {
            Debug.Log("[Hendek avı] 1.5 metreden derin oyuk yok.");
            return;
        }

        found.Sort((a, b) => b.Depth.CompareTo(a.Depth));

        var marks = new List<Vector2>();
        Collect(marks, route.road, terrain);
        foreach (MountainRoute.Branch branch in route.branches)
            Collect(marks, branch.marks, terrain);

        var report = new StringBuilder();
        int near = 0, far = 0, shown = 0;

        foreach ((float depth, Vector2 point) in found)
        {
            float nearest = float.MaxValue;
            foreach (Vector2 mark in marks)
                nearest = Mathf.Min(nearest, Vector2.Distance(mark, point));

            if (nearest <= 30f) near++; else far++;

            if (shown < 20)
            {
                report.Append($"\n  {depth,5:F1} m derin   rotaya {nearest,6:F0} m");
                shown++;
            }
        }

        Debug.Log($"[Hendek avı] 1.5 m'den derin {found.Count} oyuk\n"
                + $"  ROTANIN ÜSTÜNDE (<30 m): {near}\n"
                + $"  ROTADAN UZAKTA:          {far}\n"
                + $"  en derin yirmisi:{report}");
    }

    static void Collect(List<Vector2> into, List<MountainRoute.Mark> marks, Terrain terrain)
    {
        foreach (MountainRoute.Mark mark in marks)
        {
            Vector3 world = MountainRoute.ToWorld(mark.position, terrain);
            into.Add(new Vector2(world.x, world.z));
        }
    }

    static MountainRoute LoadRoute() =>
        AssetDatabase.LoadAssetAtPath<MountainRoute>("Assets/Settings/MountainRoute.asset");
}
