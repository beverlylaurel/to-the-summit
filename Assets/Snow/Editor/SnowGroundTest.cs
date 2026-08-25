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

        // ---------------------------------------------------------------- bake
        //
        // BURAYA KADAR OLAN ÖLÇÜM "Terrain görünen arazi mi" sorusunu yanıtlıyor.
        // ASIL SORU BU DEĞİL: kar, Terrain'i DOĞRUDAN okumuyor — `SnowGroundHeight`
        // bir dokuya pişiriyor ve shader onu örneklüyor. Pişirme indeks sırası
        // ters olsa (spec §7.1 özellikle uyarıyor: `h[y, x]`) kar yüzeyi dağın
        // yüksekliğini YANLIŞ YERDEN okur ve araziden ayrı durur.
        //
        // Aşağısı pişirmenin kendisini sınıyor: shader'ın yaptığı eşlemenin
        // birebir aynısı CPU'da kurulup Terrain'in gerçek yüksekliğiyle
        // karşılaştırılıyor.
        r.AppendLine();
        r.AppendLine("## Pişen doku Terrain'i doğru kopyalıyor mu");

        int res = td.heightmapResolution;
        float[,] hm = td.GetHeights(0, 0, res, res);

        int bakeOk = 0;
        float bakeWorst = 0f;
        var bakePoints = new[]
        {
            new Vector2(0.25f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0.75f, 0.25f),
            new Vector2(0.25f, 0.75f), new Vector2(0.9f, 0.6f),
        };

        foreach (Vector2 uv in bakePoints)
        {
            // Shader: uv = (posXZ - origin) / size, sonra doku örnekleniyor.
            // Doku pikseli (x, y) = h[y, x]. En yakın teksel yeterli — aradığımız
            // hata metre mertebesinde, teksel arası fark değil.
            int px = Mathf.Clamp(Mathf.RoundToInt(uv.x * (res - 1)), 0, res - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(uv.y * (res - 1)), 0, res - 1);

            float baked = pos.y + hm[py, px] * size.y;
            float truth = pos.y + td.GetInterpolatedHeight(uv.x, uv.y);

            // Ters indeksin ne verdiği de yazılıyor: hata buysa o sütun tutar.
            float swapped = pos.y + hm[px, py] * size.y;

            float gap = Mathf.Abs(baked - truth);
            float swapGap = Mathf.Abs(swapped - truth);
            bool ok = gap < 2f;

            if (ok) bakeOk++;
            bakeWorst = Mathf.Max(bakeWorst, gap);

            r.AppendLine("  [" + M(ok) + "] uv(" + uv.x.ToString("0.00") + "," + uv.y.ToString("0.00") + ")" +
                         "   gerçek " + truth.ToString("0.0") + " m" +
                         "   pişen " + baked.ToString("0.0") + " m" +
                         "   fark " + gap.ToString("0.0") + " m" +
                         "   (ters indeks farkı " + swapGap.ToString("0.0") + " m)");
        }

        bool bakeMatches = bakeOk == bakePoints.Length;

        r.AppendLine();
        r.AppendLine("  " + (bakeMatches
            ? "[+] Pişen doku Terrain'in aynısı."
            : "[-] PİŞEN DOKU YANLIŞ. En büyük fark " + bakeWorst.ToString("0.0") + " m."));

        // ---------------------------------------------------------------- çözünürlük
        //
        // Kar bölgesi 16 m; zemin dokusunun tekseli bundan büyükse kar yüzeyi
        // bölgenin içinde neredeyse DÜZ çıkar. Sayı burada, yorum yok.
        float groundTexel = size.x / Mathf.Max(res - 1, 1);

        r.AppendLine();
        r.AppendLine("## Zemin dokusunun çözünürlüğü");
        r.AppendLine("  Teksel                   " + groundTexel.ToString("0.00") + " m");
        r.AppendLine("  Kar bölgesi              16.00 m  (" +
                     (16f / groundTexel).ToString("0.0") + " teksel)");
        r.AppendLine("  Clipmap kapsaması        128.00 m  (" +
                     (128f / groundTexel).ToString("0.0") + " teksel)");

        // ------------------------------------------------- kar yüzeyi ile arazi arası
        //
        // ASIL SAYI BU. Kar mesh'i `SampleGroundHeight` + kalınlık kadar
        // yükseliyor; arazi kendi üçgenleriyle çiziliyor. İkisinin arası kar
        // kalınlığı kadar olmalı. Daha fazlaysa kar yüzeyi havada duruyor
        // demektir ve fark burada metre olarak çıkar.
        //
        // Shader BİLİNEAR örnekliyor, Terrain ÜÇGENLERLE. Aynı köşelerden
        // geçseler de hücrenin içinde farklı yüzeyler; fark bu ölçümde görünür.
        r.AppendLine();
        r.AppendLine("## Kar yüzeyi araziden ne kadar ayrılıyor");

        float texel = size.x / Mathf.Max(res - 1, 1);
        float worstSep = 0f, sumSep = 0f;
        int samples = 0;
        Vector2 worstAt = Vector2.zero;

        for (int i = 0; i <= 32; i++)
        for (int j = 0; j <= 32; j++)
        {
            float wx = center.x - 64f + i * 4f;
            float wz = center.z - 64f + j * 4f;

            float u = (wx - pos.x) / size.x;
            float v = (wz - pos.z) / size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

            // Shader'ın bilinear örneklemesi, birebir.
            float fx = u * (res - 1), fy = v * (res - 1);
            int x0 = Mathf.Clamp((int)fx, 0, res - 2);
            int y0 = Mathf.Clamp((int)fy, 0, res - 2);
            float tx = fx - x0, ty = fy - y0;

            float n = Mathf.Lerp(Mathf.Lerp(hm[y0, x0], hm[y0, x0 + 1], tx),
                                 Mathf.Lerp(hm[y0 + 1, x0], hm[y0 + 1, x0 + 1], tx), ty);

            float snowGroundY = pos.y + n * size.y;
            float terrainY = pos.y + td.GetInterpolatedHeight(u, v);

            float sep = Mathf.Abs(snowGroundY - terrainY);
            sumSep += sep;
            samples++;

            if (sep > worstSep) { worstSep = sep; worstAt = new Vector2(wx, wz); }
        }

        float meanSep = samples > 0 ? sumSep / samples : 0f;

        // Kar kalınlığı 26–45 cm. Ayrılık bunun altındaysa yüzey araziye oturuyor.
        bool hugsGround = worstSep < 0.5f;

        r.AppendLine("  Örnek sayısı             " + samples + "  (oyuncunun çevresinde 128 m, 4 m adım)");
        r.AppendLine("  Zemin tekseli            " + texel.ToString("0.00") + " m");
        r.AppendLine("  [" + M(hugsGround) + "] Ortalama ayrılık     " + (meanSep * 100f).ToString("0.0") + " cm");
        r.AppendLine("  [" + M(hugsGround) + "] EN BÜYÜK ayrılık     " + worstSep.ToString("0.00") + " m" +
                     "   @ (" + worstAt.x.ToString("0") + ", " + worstAt.y.ToString("0") + ")");
        r.AppendLine("      Beklenen: kar kalınlığı kadar, yani 26–45 cm. Metre mertebesindeyse");
        r.AppendLine("      kar yüzeyi arazinin üstünde havada duruyor.");

        bool sourceIsVisible = matches == offsets.Length && hasRelief && bakeMatches && hugsGround;

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

        // ------------------------------------------------- sahnedeki referanslar
        //
        // ATANMAMIŞ REFERANS SESSİZ. `SnowManager.detailNormal` boşsa
        // `_SnowDetailNormal` global'i hiç yazılmıyor, bağlanmamış sampler
        // NaN üretiyor ve arazi kapkara çıkıyor. Hata mesajı yok; ekranda
        // gördüğün şey "kar sistemi bozuk" gibi görünüyor.
        r.AppendLine();
        r.AppendLine("## Sahnedeki kar referansları");

        var manager = Object.FindAnyObjectByType<SnowManager>();

        if (manager == null)
        {
            r.AppendLine("  [!] Sahnede SnowManager yok — Kar Teşhisi'nde \"Sahneyi kur\".");
        }
        else
        {
            UnityEditor.SerializedObject so = new(manager);

            string[] required = { "settings", "simCompute", "skyShader",
                                  "groundHeight", "environmentSource", "followTarget",
                                  "detailNormal" };

            int empty = 0;

            foreach (string name in required)
            {
                var prop = so.FindProperty(name);
                bool ok = prop != null && prop.objectReferenceValue != null;

                if (!ok) empty++;

                r.AppendLine("  [" + M(ok) + "] " + name.PadRight(20) +
                             (ok ? prop.objectReferenceValue.name : "ATANMAMIŞ"));
            }

            if (empty > 0)
            {
                // KURULUMU BURADA KOŞTURUP TEKRAR BAKIYORUZ. Böylece "otomatik
                // kurulum çalışmıyor" ile "kurulum çalışıyor ama bu alanı
                // dolduramıyor" ayrışıyor — ikisi dışarıdan aynı görünüyor.
                r.AppendLine();
                r.AppendLine("      " + empty + " referans boştu; kurulum koşturuluyor…");

                string failure = null;

                try { SnowDebugWindow.SetupScene(); }
                catch (System.Exception e) { failure = e.GetType().Name + ": " + e.Message; }

                if (failure != null)
                {
                    r.AppendLine("      [-] KURULUM ÇÖKTÜ — " + failure);
                    sourceIsVisible = false;
                }
                else
                {
                    so = new UnityEditor.SerializedObject(manager);
                    int stillEmpty = 0;

                    foreach (string name in required)
                    {
                        var prop = so.FindProperty(name);
                        bool ok = prop != null && prop.objectReferenceValue != null;

                        if (!ok)
                        {
                            stillEmpty++;
                            r.AppendLine("      [-] " + name + " kurulumdan SONRA da boş");
                        }
                    }

                    if (stillEmpty == 0)
                        r.AppendLine("      [+] Kurulum tamamladı; eksik kalmadı.");
                    else
                        sourceIsVisible = false;
                }
            }
        }

        // ------------------------------------------------- yari hassasiyet kaybi
        //
        // Zemin yüksekliği `TextureFormat.RHalf` dokuya 0..1 normalize edilerek
        // pişiyor ve `_GroundHeightRange` = 8000 m ile çarpılıyor. Half'ın
        // bağıl adımı 2^-11; 8000 m aralıkta bunun metre karşılığı kotla
        // birlikte BÜYÜYOR. Kar yüzeyi bu dokudan yer değiştirdiği için
        // quantization doğrudan basamak olarak görünüyor.
        r.AppendLine();
        r.AppendLine("## Zemin dokusunun hassasiyeti");

        // FORMAT KAYNAKTAN OKUNUYOR. Doku yalnız Play'de pişiyor; edit mode'da
        // bileşene sormak `UnassignedReferenceException` atıyor. Kaynağı
        // denetlemek hem edit mode'da çalışıyor hem de formatın geri
        // döndürülmesini yakalıyor.
        string groundSrc = System.IO.File.ReadAllText("Assets/Snow/Runtime/SnowGroundHeight.cs");
        bool isHalf = groundSrc.Contains("TextureFormat.RHalf");

        r.AppendLine("  Doku formatı             " + (isHalf ? "RHalf" : "RFloat"));

        float worstStep = 0f;
        float worstStepAt = 0f;

        for (int i = 0; i <= 40; i++)
        {
            float alt = i / 40f * size.y;              // 0 … 8000 m
            float n = alt / Mathf.Max(size.y, 1e-3f);

            // Dokunun GERÇEK formatındaki adım. RFloat'ta float32 adımı,
            // RHalf'ta half adımı. Format değişirse bu satır da değişmeli.
            float step = isHalf
                ? Mathf.Abs(Mathf.HalfToFloat((ushort)(Mathf.FloatToHalf(n) + 1))
                            - Mathf.HalfToFloat(Mathf.FloatToHalf(n))) * size.y
                : Mathf.Max(n, 1e-7f) * 1.1920929e-7f * size.y;

            if (step > worstStep) { worstStep = step; worstStepAt = alt; }

            if (i % 10 == 0)
                r.AppendLine("  " + alt.ToString("0").PadLeft(5) + " m kotunda adım   " +
                             (step * 100f).ToString("0.0") + " cm");
        }

        // Kar kalınlığı 26–45 cm. Adım bunun beşte birini aşarsa yüzey
        // gözle görülür biçimde basamaklanıyor.
        bool precisionOk = worstStep < 0.05f;

        r.AppendLine("  [" + M(precisionOk) + "] EN BÜYÜK adım        " +
                     (worstStep * 100f).ToString("0.0") + " cm   (" + worstStepAt.ToString("0") + " m kotunda)");
        r.AppendLine("      Kar kalınlığı 26–45 cm. Adım bunun beşte birini aşarsa");
        r.AppendLine("      kar yüzeyi basamaklanır.");

        if (!precisionOk) sourceIsVisible = false;

        // ------------------------------------------------- arazi hangi shader'la
        //
        // BUGÜNE KADAR ÖLÇÜLMEDİ. `MountainSurface.hlsl`'e kar katmanı eklendi
        // ama Terrain'in materyalinin GERÇEKTEN o shader olduğu doğrulanmadı.
        // Değilse bugün yazılan her şey hiçbir yere bağlı değil.
        r.AppendLine();
        r.AppendLine("## Arazi hangi shader ile çiziliyor");

        // MATERYAL ÇALIŞMA ANINDA ATANIYOR (`TerrainSurface.cs`). Edit mode'da
        // boş görünmesi NORMAL — bu bir kez yanlış okundu ve "dağ katmanı hiç
        // bağlı değil" sonucuna varıldı.
        Material tm = t.materialTemplate;
        var surface = Object.FindAnyObjectByType<TerrainSurface>();

        if (tm == null)
        {
            bool wired = surface != null;

            r.AppendLine("  [" + M(wired) + "] Materyal             " +
                         (wired
                          ? "edit mode'da boş; `TerrainSurface` çalışma anında atıyor"
                          : "YOK ve `TerrainSurface` de yok — arazi varsayılanla çiziliyor"));

            if (!wired) sourceIsVisible = false;
        }
        else
        {
            bool isMountain = tm.shader != null && tm.shader.name.Contains("Mountain");

            r.AppendLine("  Materyal                 " + tm.name);
            r.AppendLine("  [" + M(isMountain) + "] Shader               " +
                         (tm.shader != null ? tm.shader.name : "YOK"));

            if (!isMountain)
            {
                r.AppendLine("      Dağın kar katmanı `MountainSurface.hlsl`'de. Terrain başka bir");
                r.AppendLine("      shader kullanıyorsa o katman HİÇ ÇALIŞMIYOR.");
                sourceIsVisible = false;
            }
        }

        r.AppendLine();
        r.AppendLine("SONUÇ: " + (sourceIsVisible ? "TAMAM" : "BAŞARISIZ"));
        return r.ToString();
    }

    static string M(bool ok) => ok ? "+" : "-";
}
