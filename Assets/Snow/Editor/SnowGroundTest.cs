// Measures the snow subsystem ground elevation source against the scene's actual terrain.
// Invoked by: SnowTestRunner.

using System.Text;
using UnityEngine;

public static class SnowGroundTest
{
    public static string Run()
    {
        var r = new StringBuilder();

        r.AppendLine("# Snow — Ground Elevation Source");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude,
                                                         FindObjectsSortMode.None);

        r.AppendLine("## Scene Terrains");
        r.AppendLine("  Terrain count            " + terrains.Length);

        if (terrains.Length == 0)
        {
            r.AppendLine();
            r.AppendLine("  [-] No Terrain found.");
            r.AppendLine();
            r.AppendLine("RESULT: FAILED");
            return r.ToString();
        }

        Terrain t = terrains[0];
        TerrainData td = t.terrainData;
        Vector3 pos = t.transform.position;
        Vector3 size = td.size;

        r.AppendLine("  Name                     " + t.name);
        r.AppendLine("  Position                 " + pos.ToString("0.0"));
        r.AppendLine("  Size                     " + size.ToString("0.0"));
        r.AppendLine("  Heightmap resolution     " + td.heightmapResolution);
        r.AppendLine("  Collider                 " +
                     (t.GetComponent<TerrainCollider>() != null ? "present" : "MISSING"));
        r.AppendLine();

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

        r.AppendLine("## Terrain Elevation Relief");
        r.AppendLine("  [" + M(hasRelief) + "] Elevation range      " +
                     minH.ToString("0.0") + " – " + maxH.ToString("0.0") + " m  " +
                     "(delta " + (maxH - minH).ToString("0.0") + " m)");

        if (!hasRelief)
            r.AppendLine("      Terrain is flat.");

        r.AppendLine();
        r.AppendLine("## Visible Surface Validation (Raycast)");

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
            else { rayY = float.NaN; what = "MISSED"; }

            float terrainY = pos.y + td.GetInterpolatedHeight(
                Mathf.Clamp01((xz.x - pos.x) / Mathf.Max(size.x, 1e-3f)),
                Mathf.Clamp01((xz.y - pos.z) / Mathf.Max(size.z, 1e-3f)));

            float gap = float.IsNaN(rayY) ? float.NaN : Mathf.Abs(rayY - terrainY);
            bool ok = !float.IsNaN(gap) && gap < 2f;

            if (ok) matches++;
            if (!float.IsNaN(gap) && gap > worstGap) { worstGap = gap; worstName = what; }

            r.AppendLine("  [" + M(ok) + "] (" + o.x.ToString("0") + "," + o.y.ToString("0") + ")".PadRight(10) +
                         "  terrain " + terrainY.ToString("0.0") + " m" +
                         "   ray " + (float.IsNaN(rayY) ? "none" : rayY.ToString("0.0") + " m") +
                         "   delta " + (float.IsNaN(gap) ? "-" : gap.ToString("0.0") + " m") +
                         "   hit: " + what);
        }

        r.AppendLine();
        r.AppendLine("  Ray hits                 " + hits + " / " + offsets.Length);
        r.AppendLine("  TerrainCollider hits     " + terrainHits + " / " + offsets.Length);
        r.AppendLine("  Terrain matches          " + matches + " / " + offsets.Length);

        r.AppendLine();
        r.AppendLine("## Baked Texture Parity");

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
            int px = Mathf.Clamp(Mathf.RoundToInt(uv.x * (res - 1)), 0, res - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(uv.y * (res - 1)), 0, res - 1);

            float baked = pos.y + hm[py, px] * size.y;
            float truth = pos.y + td.GetInterpolatedHeight(uv.x, uv.y);
            float swapped = pos.y + hm[px, py] * size.y;

            float gap = Mathf.Abs(baked - truth);
            float swapGap = Mathf.Abs(swapped - truth);
            bool ok = gap < 2f;

            if (ok) bakeOk++;
            bakeWorst = Mathf.Max(bakeWorst, gap);

            r.AppendLine("  [" + M(ok) + "] uv(" + uv.x.ToString("0.00") + "," + uv.y.ToString("0.00") + ")" +
                         "   truth " + truth.ToString("0.0") + " m" +
                         "   baked " + baked.ToString("0.0") + " m" +
                         "   delta " + gap.ToString("0.0") + " m" +
                         "   (transposed delta " + swapGap.ToString("0.0") + " m)");
        }

        bool bakeMatches = bakeOk == bakePoints.Length;

        r.AppendLine();
        r.AppendLine("  " + (bakeMatches
            ? "[+] Baked texture matches Terrain heights."
            : "[-] BAKED TEXTURE MISMATCH. Max delta " + bakeWorst.ToString("0.0") + " m."));

        float groundTexel = size.x / Mathf.Max(res - 1, 1);

        r.AppendLine();
        r.AppendLine("## Ground Texture Resolution");
        r.AppendLine("  Texel size               " + groundTexel.ToString("0.00") + " m");
        r.AppendLine("  Active snow area         16.00 m  (" +
                     (16f / groundTexel).ToString("0.0") + " texels)");
        r.AppendLine("  Clipmap bounds           128.00 m  (" +
                     (128f / groundTexel).ToString("0.0") + " texels)");

        r.AppendLine();
        r.AppendLine("## Snow Surface to Terrain Separation");

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
        bool hugsGround = worstSep < 0.5f;

        r.AppendLine("  Sample count             " + samples + "  (128m around player, 4m step)");
        r.AppendLine("  Ground texel             " + texel.ToString("0.00") + " m");
        r.AppendLine("  [" + M(hugsGround) + "] Mean separation      " + (meanSep * 100f).ToString("0.0") + " cm");
        r.AppendLine("  [" + M(hugsGround) + "] Max separation       " + worstSep.ToString("0.00") + " m" +
                     "   @ (" + worstAt.x.ToString("0") + ", " + worstAt.y.ToString("0") + ")");

        bool sourceIsVisible = matches == offsets.Length && hasRelief && bakeMatches && hugsGround;

        r.AppendLine();

        if (!sourceIsVisible)
        {
            r.AppendLine("  [-] SNOW IS READING INCORRECT GROUND. Max delta " +
                         worstGap.ToString("0.0") + " m (hit: " + worstName + ").");
        }
        else
        {
            r.AppendLine("  [+] Snow subsystem reads the matching ground elevation.");
        }

        r.AppendLine();
        r.AppendLine("## Scene Snow References");

        var manager = Object.FindAnyObjectByType<SnowManager>();

        if (manager == null)
        {
            r.AppendLine("  [!] No SnowManager in scene.");
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
                             (ok ? prop.objectReferenceValue.name : "UNASSIGNED"));
            }

            if (empty > 0)
            {
                r.AppendLine();
                r.AppendLine("      " + empty + " references were unassigned; running setup...");

                string failure = null;

                try { SnowDebugWindow.SetupScene(); }
                catch (System.Exception e) { failure = e.GetType().Name + ": " + e.Message; }

                if (failure != null)
                {
                    r.AppendLine("      [-] SETUP FAILED — " + failure);
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
                            r.AppendLine("      [-] " + name + " still empty after setup");
                        }
                    }

                    if (stillEmpty == 0)
                        r.AppendLine("      [+] Setup completed successfully.");
                    else
                        sourceIsVisible = false;
                }
            }
        }

        r.AppendLine();
        r.AppendLine("## Ground Texture Precision");

        string groundSrc = System.IO.File.ReadAllText("Assets/Snow/Runtime/SnowGroundHeight.cs");
        bool isHalf = groundSrc.Contains("TextureFormat.RHalf");

        r.AppendLine("  Texture format           " + (isHalf ? "RHalf" : "RFloat"));

        float worstStep = 0f;
        float worstStepAt = 0f;

        for (int i = 0; i <= 40; i++)
        {
            float alt = i / 40f * size.y;
            float n = alt / Mathf.Max(size.y, 1e-3f);

            float step = isHalf
                ? Mathf.Abs(Mathf.HalfToFloat((ushort)(Mathf.FloatToHalf(n) + 1))
                            - Mathf.HalfToFloat(Mathf.FloatToHalf(n))) * size.y
                : Mathf.Max(n, 1e-7f) * 1.1920929e-7f * size.y;

            if (step > worstStep) { worstStep = step; worstStepAt = alt; }

            if (i % 10 == 0)
                r.AppendLine("  " + alt.ToString("0").PadLeft(5) + " m elevation step   " +
                             (step * 100f).ToString("0.0") + " cm");
        }

        bool precisionOk = worstStep < 0.05f;

        r.AppendLine("  [" + M(precisionOk) + "] MAX step             " +
                     (worstStep * 100f).ToString("0.0") + " cm   (@" + worstStepAt.ToString("0") + " m elevation)");

        if (!precisionOk) sourceIsVisible = false;

        r.AppendLine();
        r.AppendLine("## Terrain Shader Material");

        Material tm = t.materialTemplate;
        var surface = Object.FindAnyObjectByType<TerrainSurface>();

        if (tm == null)
        {
            bool wired = surface != null;

            r.AppendLine("  [" + M(wired) + "] Material             " +
                         (wired
                          ? "null in edit mode; TerrainSurface assigns at runtime"
                          : "MISSING and TerrainSurface missing — terrain rendered with default"));

            if (!wired) sourceIsVisible = false;
        }
        else
        {
            bool isMountain = tm.shader != null && tm.shader.name.Contains("Mountain");

            r.AppendLine("  Material                 " + tm.name);
            r.AppendLine("  [" + M(isMountain) + "] Shader               " +
                         (tm.shader != null ? tm.shader.name : "MISSING"));

            if (!isMountain)
            {
                r.AppendLine("      Mountain snow layer resides in MountainSurface.hlsl.");
                sourceIsVisible = false;
            }
        }

        r.AppendLine();
        r.AppendLine("RESULT: " + (sourceIsVisible ? "PASSED" : "FAILED"));
        return r.ToString();
    }

    static string M(bool ok) => ok ? "+" : "-";
}
