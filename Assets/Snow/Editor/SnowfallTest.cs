// Measures snowfall simulation — flake physics, spawn bounds, ground/roof clipping,
// wind drift, intensity mapping, drift threshold, and atlas validity.
// Invoked by: Menu — To The Summit/Snow/Precipitation Test.

using System.Text;
using UnityEditor;
using UnityEngine;

public static class SnowfallTest
{
    const string ComputePath = "Assets/Snow/Shaders/SnowfallSim.compute";
    const string ShaderPath = "Assets/Snow/Shaders/SnowfallParticle.shader";

    const int Capacity = 4096;
    const int Stride = 12 * sizeof(float);

    static readonly Vector3 SpawnCenter = new(0f, 111f, 0f);
    static readonly Vector3 SpawnExtent = new(20f, 13f, 20f);

    const float GroundY = 50f;

    [MenuItem("To The Summit/Snow/Precipitation Test", false, 57)]
    static void RunMenu() => Debug.Log(Run(out _));

    public static string Run(out bool ok)
    {
        var r = new StringBuilder(8192);
        r.AppendLine("# Snow — Precipitation Test");
        r.AppendLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        r.AppendLine();

        ok = CountTest(r);
        ok &= SimulationTest(r);
        ok &= AtlasTest(r);
        ok &= ShaderTest(r);

        r.AppendLine();
        r.AppendLine(ok ? "RESULT: PASSED — all tests completed successfully."
                        : "RESULT: FAILED — see above for details.");
        return r.ToString();
    }

    static bool CountTest(StringBuilder r)
    {
        r.AppendLine("## Density and Thresholds (spec §17.1, §17.2)");

        bool all = true;

        int none = SnowfallRenderer.FlakeCountFor(0f, 1f);
        bool zero = none == 0;
        all &= zero;
        r.AppendLine("  [" + M(zero) + "] Intensity 0          " + none +
                     " flakes  (dormant when clear)");

        string particleSrc = System.IO.File.ReadAllText(
            "Assets/Snow/Shaders/SnowfallParticle.shader");

        bool signSafe = !System.Text.RegularExpressions.Regex.IsMatch(
            particleSrc, @"(?<!abs\()\s*UNITY_MATRIX_P\._m11");

        all &= signSafe;
        r.AppendLine("  [" + M(signSafe) + "] _m11 sign-safe       " +
                     (signSafe ? "wrapped with abs" : "NO ABS — scale breaks on negative matrix"));

        int low = SnowfallRenderer.FlakeCountFor(0.06f, 1f);
        int mid = SnowfallRenderer.FlakeCountFor(0.24f, 1f);
        int high = SnowfallRenderer.FlakeCountFor(1f, 1f);

        bool lowMatchesSpec = Mathf.Abs(low - 0.06f * 16000f * 6.5f) < 2f;
        bool notClipped = high == Mathf.RoundToInt(16000f * 6.5f);
        bool monotone = low > 0 && low < mid && mid < high && lowMatchesSpec && notClipped;

        all &= monotone;
        r.AppendLine("  [" + M(monotone) + "] Intensity -> flakes   0.06 -> " + low +
                     ",  0.24 -> " + mid + ",  1.00 -> " + high);

        int lowQuality = SnowfallRenderer.FlakeCountFor(0.24f, 0.35f);
        bool scaled = lowQuality < mid;
        all &= scaled;
        r.AppendLine("  [" + M(scaled) + "] Quality scale        Medium " + mid +
                     " -> Low " + lowQuality + "  (VfxCapacityScale 0.35)");

        int calm = SnowfallRenderer.DriftCountFor(5f, 0.8f, 1f, 1f);
        int justBelow = SnowfallRenderer.DriftCountFor(7f, 0.8f, 1f, 1f);
        int windy = SnowfallRenderer.DriftCountFor(12f, 0.8f, 1f, 1f);
        int packed = SnowfallRenderer.DriftCountFor(12f, 0.10f, 1f, 1f);

        bool gate = calm == 0 && justBelow == 0 && windy > 0 && packed == 0;
        all &= gate;
        r.AppendLine("  [" + M(gate) + "] Drift threshold      5 m/s -> " + calm +
                     ",  7 m/s -> " + justBelow + ",  12 m/s -> " + windy +
                     ",  12 m/s packed -> " + packed);

        Vector3 a = SnowfallRenderer.SnapSpawnCenter(new Vector3(3.2f, 50f, -7.9f), Vector3.right);
        Vector3 b = SnowfallRenderer.SnapSpawnCenter(new Vector3(3.4f, 50.2f, -7.7f), Vector3.right);

        bool snapped = a == b &&
                       Mathf.Approximately(a.x, Mathf.Round(a.x)) &&
                       Mathf.Approximately(a.z, Mathf.Round(a.z));

        all &= snapped;
        r.AppendLine("  [" + M(snapped) + "] Spawn box snap        snapped to integer center: " +
                     a.ToString("0.0"));

        return all;
    }

    static bool SimulationTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Flake Simulation (spec §17.1)");

        var cs = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        if (cs == null) { r.AppendLine("  [-] " + ComputePath + " could not be loaded."); return false; }

        var rig = new Rig(cs, Capacity);
        bool all = true;

        try
        {
            rig.SetGround(GroundY);
            rig.SetSky(occluderY: -9999f);
            rig.SetWind(new Vector3(4f, 0f, 0f), 4f);

            rig.Step(0.016f);
            Flake[] first = rig.Read();

            int inside = 0;
            for (int i = 0; i < Capacity; i++)
                if (InsideBox(first[i].Position)) inside++;

            bool fills = inside == Capacity;
            all &= fills;
            r.AppendLine("  [" + M(fills) + "] Volume instantly full " + inside + " / " + Capacity);

            float minSize = float.MaxValue, maxSize = 0f;
            for (int i = 0; i < Capacity; i++)
            {
                minSize = Mathf.Min(minSize, first[i].Size);
                maxSize = Mathf.Max(maxSize, first[i].Size);
            }

            bool sized = maxSize / Mathf.Max(minSize, 1e-6f) > 2.0f;
            all &= sized;
            r.AppendLine("  [" + M(sized) + "] Size distribution     " + (minSize * 1000f).ToString("0.0") +
                         " – " + (maxSize * 1000f).ToString("0.0") + " mm  (ratio " +
                         (maxSize / minSize).ToString("0.00") + ")");

            var used = new bool[16];
            for (int i = 0; i < Capacity; i++)
            {
                int frame = Mathf.Clamp(Mathf.RoundToInt(first[i].Frame), 0, 15);
                used[frame] = true;
            }

            int usedCount = 0;
            foreach (bool u in used) if (u) usedCount++;

            bool frames = usedCount >= 14;
            all &= frames;
            r.AppendLine("  [" + M(frames) + "] Atlas distribution    " + usedCount +
                         " / 16 cells utilized");

            Vector3 before = MeanPosition(first);

            for (int i = 0; i < 20; i++) rig.Step(0.05f);
            Flake[] moved = rig.Read();

            Vector3 after = MeanPosition(moved);
            Vector3 delta = after - before;

            bool falls = delta.y < -0.5f;
            bool drifts = delta.x > 0.5f;

            all &= falls && drifts;
            r.AppendLine("  [" + M(falls) + "] Falls                 in 1s Delta_y = " +
                         delta.y.ToString("0.00") + " m");
            r.AppendLine("  [" + M(drifts) + "] Drifts with wind     Delta_x = " +
                         delta.x.ToString("0.00") + " m  (+X 4 m/s wind)");

            rig.Reset();
            rig.SetWind(new Vector3(-4f, 0f, 0f), 4f);
            rig.Step(0.016f);
            Vector3 b0 = MeanPosition(rig.Read());

            for (int i = 0; i < 20; i++) rig.Step(0.05f);
            float dxBack = MeanPosition(rig.Read()).x - b0.x;

            bool reversed = dxBack < -0.5f;
            all &= reversed;
            r.AppendLine("  [" + M(reversed) + "] Reversed direction   Delta_x = " +
                         dxBack.ToString("0.00") + " m  (-X wind)");

            rig.Reset();
            rig.SetWind(Vector3.zero, 0f);
            rig.SetGround(SpawnCenter.y + SpawnExtent.y + 5f);
            rig.Step(0.05f);
            rig.Step(0.05f);

            Flake[] grounded = rig.Read();
            int aged = 0;
            for (int i = 0; i < Capacity; i++) if (grounded[i].Age > 0.02f) aged++;

            bool groundKill = aged == 0;
            all &= groundKill;
            r.AppendLine("  [" + M(groundKill) + "] Ground clipping       ground above flakes leaves " +
                         aged + " alive (must be 0)");

            rig.Reset();
            rig.SetGround(GroundY);
            rig.SetSky(occluderY: SpawnCenter.y + SpawnExtent.y + 3f);
            rig.Step(0.05f);
            rig.Step(0.05f);

            Flake[] roofed = rig.Read();
            int survivors = 0;
            for (int i = 0; i < Capacity; i++) if (roofed[i].Age > 0.02f) survivors++;

            bool roofKill = survivors == 0;
            all &= roofKill;
            r.AppendLine("  [" + M(roofKill) + "] Sky occlusion clip   under roof leaves " + survivors +
                         " alive (must be 0)");

            rig.Reset();
            rig.SetSky(occluderY: -9999f);
            rig.Step(0.016f);
            float youngAlpha = MeanAlpha(rig.Read());

            for (int i = 0; i < 40; i++) rig.Step(0.05f);
            float matureAlpha = MeanAlpha(rig.Read());

            bool ramps = youngAlpha < 0.15f && matureAlpha > 0.6f;
            all &= ramps;
            r.AppendLine("  [" + M(ramps) + "] Alpha ramp            spawn " +
                         youngAlpha.ToString("0.000") + " -> 2s later " +
                         matureAlpha.ToString("0.000"));

            rig.Reset();
            rig.SetAlive(Capacity / 2);
            rig.Step(0.05f);

            Flake[] partial = rig.Read();
            int liveTail = 0;
            for (int i = Capacity / 2; i < Capacity; i++) if (partial[i].Lifetime > 0f) liveTail++;

            bool tailDead = liveTail == 0;
            all &= tailDead;
            r.AppendLine("  [" + M(tailDead) + "] Disabled slots       " + liveTail +
                         " alive (must be 0)");
        }
        finally
        {
            rig.Dispose();
        }

        return all;
    }

    static bool AtlasTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Flake Atlas (spec §17.1)");

        Texture2D atlas = SnowTextureBaker.EnsureFlakeAtlas();

        if (atlas == null)
        {
            r.AppendLine("  [-] Atlas could not be generated.");
            return false;
        }

        string path = AssetDatabase.GetAssetPath(atlas);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);

        bool readable = importer.isReadable;
        if (!readable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        Color[] px = atlas.GetPixels();
        int cell = atlas.width / 4;

        var coverage = new float[16];
        bool all = true;

        for (int cy = 0; cy < 4; cy++)
        for (int cx = 0; cx < 4; cx++)
        {
            float sum = 0f;

            for (int y = 0; y < cell; y++)
            for (int x = 0; x < cell; x++)
                sum += px[(cy * cell + y) * atlas.width + (cx * cell + x)].a;

            coverage[cy * 4 + cx] = sum / (cell * cell);
        }

        float min = float.MaxValue, max = 0f;
        foreach (float c in coverage) { min = Mathf.Min(min, c); max = Mathf.Max(max, c); }

        bool filled = min > 0.01f;
        all &= filled;
        r.AppendLine("  [" + M(filled) + "] Every cell filled     coverage " + (min * 100f).ToString("0.0") +
                     "% – " + (max * 100f).ToString("0.0") + "%");

        var signature = new float[16][];

        for (int c = 0; c < 16; c++) signature[c] = CellSignature(px, atlas.width, cell, c);

        int distinct = 0;
        float closest = float.MaxValue;

        for (int i = 0; i < 16; i++)
        {
            bool unique = true;

            for (int j = 0; j < i; j++)
            {
                float diff = SignatureDistance(signature[i], signature[j]);
                closest = Mathf.Min(closest, diff);
                if (diff < 0.05f) unique = false;
            }

            if (unique) distinct++;
        }

        bool varied = distinct == 16;
        all &= varied;
        r.AppendLine("  [" + M(varied) + "] Distinct cells        " + distinct +
                     " / 16 unique patterns  (closest pair delta " +
                     closest.ToString("0.000") + ", threshold 0.050)");

        float edge = 0f;
        for (int cy = 0; cy < 4; cy++)
        for (int cx = 0; cx < 4; cx++)
        for (int x = 0; x < cell; x++)
            edge = Mathf.Max(edge, px[(cy * cell) * atlas.width + (cx * cell + x)].a);

        bool clean = edge < 0.02f;
        all &= clean;
        r.AppendLine("  [" + M(clean) + "] Cell edges clean     max edge alpha " +
                     edge.ToString("0.000"));

        return all;
    }

    static bool ShaderTest(StringBuilder r)
    {
        r.AppendLine();
        r.AppendLine("## Shader");

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);

        if (shader == null) { r.AppendLine("  [-] " + ShaderPath + " could not be loaded."); return false; }

        bool hasError = ShaderUtil.ShaderHasError(shader);

        r.AppendLine("  [" + M(!hasError) + "] Compilation           " +
                     (hasError ? "ERRORS FOUND" : "clean"));

        foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
            r.AppendLine("      [" + m.severity + "] " + m.file + "(" + m.line + "): " + m.message);

        (string needle, string symptom)[] checks =
        {
            ("_MinPixelSize", "Distant flakes disappear"),
            ("_FogDensity01", "White points in fog"),
            ("SampleSceneDepth", "Missing soft particle blending"),
            ("_StretchAlongVelocity", "Missing wind stretch"),
        };

        string source = System.IO.File.ReadAllText(ShaderPath);
        bool all = !hasError;

        foreach ((string needle, string symptom) c in checks)
        {
            bool found = source.Contains(c.needle);
            all &= found;
            r.AppendLine("  [" + M(found) + "] " + c.needle.PadRight(24) +
                         (found ? "" : "MISSING -> " + c.symptom));
        }

        return all;
    }

    static float[] CellSignature(Color[] px, int width, int cell, int index)
    {
        int cx = index % 4;
        int cy = index / 4;
        int block = cell / 8;

        var sig = new float[64];

        for (int by = 0; by < 8; by++)
        for (int bx = 0; bx < 8; bx++)
        {
            float sum = 0f;

            for (int y = 0; y < block; y++)
            for (int x = 0; x < block; x++)
                sum += px[(cy * cell + by * block + y) * width + (cx * cell + bx * block + x)].a;

            sig[by * 8 + bx] = sum / (block * block);
        }

        return sig;
    }

    static float SignatureDistance(float[] a, float[] b)
    {
        float max = 0f;
        for (int i = 0; i < a.Length; i++) max = Mathf.Max(max, Mathf.Abs(a[i] - b[i]));
        return max;
    }

    static bool InsideBox(Vector3 p)
    {
        Vector3 d = p - SpawnCenter;
        return Mathf.Abs(d.x) <= SpawnExtent.x + 1e-3f &&
               Mathf.Abs(d.y) <= SpawnExtent.y + 1e-3f &&
               Mathf.Abs(d.z) <= SpawnExtent.z + 1e-3f;
    }

    static Vector3 MeanPosition(Flake[] f)
    {
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < f.Length; i++) sum += f[i].Position;
        return sum / f.Length;
    }

    static float MeanAlpha(Flake[] f)
    {
        float sum = 0f;
        for (int i = 0; i < f.Length; i++) sum += f[i].Alpha;
        return sum / f.Length;
    }

    static string M(bool ok) => ok ? "+" : "-";

    struct Flake
    {
        public Vector3 Position;
        public float Age;
        public Vector3 Velocity;
        public float Lifetime;
        public float Size;
        public float Phase;
        public float Frame;
        public float Alpha;
    }

    sealed class Rig
    {
        readonly ComputeShader cs;
        readonly int capacity;
        readonly int kernel;

        GraphicsBuffer buffer;
        Texture2D ground;
        Texture2D sky;

        int alive;

        public Rig(ComputeShader cs, int capacity)
        {
            this.cs = cs;
            this.capacity = capacity;
            kernel = cs.FindKernel("KFlakeUpdate");

            buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, Stride);
            alive = capacity;

            Reset();

            cs.SetVector(SnowShaderIDs.SpawnCenter, SpawnCenter);
            cs.SetVector(SnowShaderIDs.SpawnExtent, SpawnExtent);
            cs.SetFloat(SnowShaderIDs.FlakeBaseSize, 0.018f);
            cs.SetFloat(SnowShaderIDs.TurbulenceIntensity, 0.15f);
            cs.SetFloat(SnowShaderIDs.TurbulenceFrequency, 0.12f);
            cs.SetFloat(SnowShaderIDs.TurbulenceDrag, 0.9f);
            cs.SetFloat(SnowShaderIDs.FlutterFreq, 5.5f);
            cs.SetFloat(SnowShaderIDs.FlutterAmp, 0.35f);

            Shader.SetGlobalFloat(SnowShaderIDs.SnowWetness, 0f);

            Shader.SetGlobalVector(SnowShaderIDs.SkyCenterXZ, Vector4.zero);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyAreaSize, 200f);
            Shader.SetGlobalFloat(SnowShaderIDs.SkyResolution, 4f);
        }

        public void Reset()
        {
            buffer.SetData(new float[capacity * 12]);
            alive = capacity;
        }

        public void SetAlive(int count) => alive = count;

        public void SetWind(Vector3 wind, float speed)
        {
            Shader.SetGlobalVector(SnowShaderIDs.WindWS, wind);
            Shader.SetGlobalFloat(SnowShaderIDs.WindSpeed, speed);
        }

        public void SetGround(float y)
        {
            if (ground != null) Object.DestroyImmediate(ground);

            ground = new Texture2D(2, 2, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var half = new Color(0.5f, 0f, 0f, 0f);
            ground.SetPixels(new[] { half, half, half, half });
            ground.Apply(false, false);

            Shader.SetGlobalVector(SnowShaderIDs.GroundOriginXZ, new Vector4(-500f, -500f, 0f, 0f));
            Shader.SetGlobalVector(SnowShaderIDs.GroundSizeXZ, new Vector4(1000f, 1000f, 0f, 0f));
            Shader.SetGlobalVector(SnowShaderIDs.GroundTexelXZ, new Vector4(500f, 500f, 0f, 0f));
            Shader.SetGlobalFloat(SnowShaderIDs.GroundBaseY, y - 1f);
            Shader.SetGlobalFloat(SnowShaderIDs.GroundHeightRange, 2f);
        }

        public void SetSky(float occluderY)
        {
            if (sky != null) Object.DestroyImmediate(sky);

            sky = new Texture2D(4, 4, TextureFormat.RFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = new Color(occluderY, 0f, 0f, 0f);

            sky.SetPixels(px);
            sky.Apply(false, false);
        }

        public void Step(float dt)
        {
            cs.SetInt(SnowShaderIDs.FlakeCapacity, capacity);
            cs.SetInt(SnowShaderIDs.FlakeAliveCount, alive);
            cs.SetFloat(SnowShaderIDs.SnowDeltaTime, dt);
            cs.SetFloat(SnowShaderIDs.FlakeSeed, Random.value * 100f);

            cs.SetTexture(kernel, SnowShaderIDs.GroundHeightTex, ground);
            cs.SetTexture(kernel, SnowShaderIDs.SnowSkyVisTex, sky);
            cs.SetBuffer(kernel, SnowShaderIDs.Flakes, buffer);

            cs.Dispatch(kernel, Mathf.CeilToInt(capacity / 64f), 1, 1);
        }

        public Flake[] Read()
        {
            var raw = new float[capacity * 12];
            buffer.GetData(raw);

            var flakes = new Flake[capacity];

            for (int i = 0; i < capacity; i++)
            {
                int o = i * 12;

                flakes[i] = new Flake
                {
                    Position = new Vector3(raw[o], raw[o + 1], raw[o + 2]),
                    Age = raw[o + 3],
                    Velocity = new Vector3(raw[o + 4], raw[o + 5], raw[o + 6]),
                    Lifetime = raw[o + 7],
                    Size = raw[o + 8],
                    Phase = raw[o + 9],
                    Frame = raw[o + 10],
                    Alpha = raw[o + 11],
                };
            }

            return flakes;
        }

        public void Dispose()
        {
            buffer?.Dispose();
            buffer = null;

            if (ground != null) Object.DestroyImmediate(ground);
            if (sky != null) Object.DestroyImmediate(sky);
        }
    }
}
