// ROLE: bakes the unreachable backdrop terrain that surrounds the playable square, as ONE
// static mesh asset. Runs in the editor only; nothing of this exists at runtime except the
// mesh and its material.
// CALLED BY: the "Dağ Yapımı/Uzak Sırayı Üret" menu item, and MountainSceneBootstrap.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// WHY A BAKED MESH AND NOT MORE TERRAIN.
///
/// The playable terrain is 30 km square at a 4097 heightmap — 7.32 m per texel. Growing it
/// to cover the backdrop would either drop that resolution (which is the gameplay surface)
/// or multiply the heightmap's memory. The backdrop needs neither: the player can never
/// reach it, it is never walked on, and at 20 km and beyond only its SILHOUETTE reads.
///
/// So it is a single static mesh with no collider, no terrain system and one draw call.
/// [SOURCE: the shipped answer for unreachable background terrain — Skyrim's terrain LOD
/// meshes, Firewatch's flat-shaded parallax bands.]
public static class DistantRangeBuilder
{
    const string MeshPath = "Assets/Terrain/DistantRange.asset";
    const string MaterialPath = "Assets/Settings/M_DistantRange.mat";
    const string ShaderName = "ToTheSummit/DistantRange";
    const string ObjectName = "Distant Range";

    /// The playable terrain's half size.
    const float PlayableHalf = 15000f;

    /// THE RING STARTS UNDER THE TERRAIN, NOT BESIDE IT.
    ///
    /// A ring beginning 120 m OUTSIDE the playable square left a slot between the two, and
    /// from the ground that slot read as a straight dark line running across the middle of
    /// the view — the one thing a backdrop must not do. Starting 600 m INSIDE means the first
    /// two rings are covered by the terrain itself, so the skirt is hidden rather than
    /// butted up against an edge.
    const float InnerMargin = -600f;

    /// HOW FAR THE RING GOES. The geometric horizon from the summit (6 km) is 276 km, and at
    /// 2 km the atmosphere controller opens visibility to 300 km — so a ring that stopped at
    /// 60 km would end inside clear air and show its own rim. 140 km is where the curvature
    /// drop below has taken the ground 1.5 km down, which is under the horizon from every
    /// altitude the player can stand at.
    const float OuterRadius = 140000f;

    /// Angular segments. 2048 puts a vertex every 61 m at 20 km — about five pixels at
    /// 1080p and 60 degrees, which is what a silhouette needs and no more.
    const int AngularSegments = 2048;

    /// Radial rings, spaced geometrically so the vertex spacing stays proportional to the
    /// distance instead of piling detail on the far rim.
    const int RadialRings = 80;

    /// Earth's radius (m). The ring's ground is pulled down by d^2/(2R) so the far rim sinks
    /// below the horizon on its own, the way real land does. At 140 km that is 1538 m.
    const float EarthRadius = 6371000f;

    /// The plain the ring sits on, and the sea it meets.
    const float SeaLevel = 30f;
    const float PlainHeight = 60f;

    [MenuItem("Dağ Yapımı/Uzak Sırayı Üret")]
    public static void Build()
    {
        var terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
        {
            Debug.LogError("Uzak sıra üretilemedi: sahnede Terrain yok.");
            return;
        }

        Vector3 centre = terrain.transform.position
                       + new Vector3(terrain.terrainData.size.x * 0.5f, 0f,
                                     terrain.terrainData.size.z * 0.5f);

        var massifs = PlaceMassifs(seed: 20260830);
        Mesh mesh = BuildMesh(massifs);

        System.IO.Directory.CreateDirectory("Assets/Terrain");
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
            EditorUtility.SetDirty(mesh);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, MeshPath);
        }

        Material material = EnsureMaterial();

        var go = GameObject.Find(ObjectName) ?? new GameObject(ObjectName);
        go.transform.position = new Vector3(centre.x, 0f, centre.z);
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        go.isStatic = true;

        // `??` DOES NOT WORK ON UNITY OBJECTS. A destroyed or missing component compares
        // equal to null through Unity's own operator but is not a null REFERENCE, so `??`
        // keeps the fake and the next line throws MissingComponentException.
        var filter = go.GetComponent<MeshFilter>();
        if (filter == null) filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;

        // NO SHADOWS EITHER WAY. The cascades end around 2 km; past that a shadow map has
        // nothing to say, and the ring's own relief is baked into its vertex colour.
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.receiveGI = ReceiveGI.LightProbes;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;

        AssetDatabase.SaveAssets();

        Debug.Log($"UZAK-SIRA: {massifs.Count} masif, {mesh.vertexCount} köşe, "
                  + $"{mesh.triangles.Length / 3} üçgen, {InnerMargin + PlayableHalf:0} m'den "
                  + $"{OuterRadius:0} m'ye.");
    }

    // ------------------------------------------------------------------ massifs

    /// A MASSIF IS THE UNIT OF DIFFERENCE.
    ///
    /// Plain fBm is statistically the same everywhere and at every scale, so no place can
    /// look different from any other — the eye reads a field of it as "the same bump again".
    /// Distinctness has to be put in ABOVE the noise: separate massifs, each with its own
    /// seed, its own height, its own ridge sharpness and its own rotation of the noise basis.
    /// [SOURCE: Musgrave on multifractals; the orometry line of terrain-synthesis work, which
    /// builds a range from peaks and saddles rather than from a single noise field.]
    struct Massif
    {
        public Vector2 Centre;
        public float Radius;
        public float Peak;
        public float RidgeShare;    // 0 = rounded and eroded, 1 = sharp alpine ridge
        public float Sharpness;     // exponent on the ridge field; higher = knife-edged
        public float Lacunarity;
        public float Gain;
        public float Rotation;
        public Vector2 NoiseOffset;
    }

    static List<Massif> PlaceMassifs(int seed)
    {
        var random = new System.Random(seed);
        var massifs = new List<Massif>();

        float inner = PlayableHalf + InnerMargin;

        // RANGES ARE CHAINS, NOT A SCATTER OF ISLANDS.
        //
        // Darts thrown at the annulus give isolated domes standing in a plain, which reads as
        // an archipelago however good each dome is. A real range is a LINE of massifs sharing
        // a divide, with cols between them. So each chain starts somewhere in the annulus and
        // WALKS, turning slowly, dropping a massif every 9 to 13 km.
        //
        // [SOURCE: the orometric picture of a range — a divide tree whose peaks hang off one
        // spine, with prominence falling away from it.]
        const int Chains = 26;
        const float MinSpacing = 6600f;

        for (int c = 0; c < Chains && massifs.Count < 260; c++)
        {
            // Chain heads are pushed outwards by a squared distribution: the near band needs
            // enough to read as "the next range", the far band only has to close the horizon.
            float u = (float)random.NextDouble();
            float r = Mathf.Lerp(inner + 3000f, OuterRadius * 0.92f, u * u);
            float a = (float)random.NextDouble() * Mathf.PI * 2f;

            var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);

            // The spine runs mostly ALONG the ring rather than towards the player: a chain
            // pointing at the camera reads as one lump, a chain across it reads as a range.
            float heading = a + Mathf.PI * 0.5f
                          + ((float)random.NextDouble() - 0.5f) * 1.4f;

            int links = 4 + random.Next(7);

            for (int k = 0; k < links && massifs.Count < 260; k++)
            {
                // Outside the playable SQUARE, not just outside a circle: the square's corners
                // reach 21.2 km, well past a 15.1 km circle.
                bool insideSquare = Mathf.Abs(p.x) < inner && Mathf.Abs(p.y) < inner;
                bool beyondRim = p.magnitude > OuterRadius * 0.98f;

                if (!insideSquare && !beyondRim)
                {
                    bool tooClose = false;
                    foreach (var m in massifs)
                        if ((m.Centre - p).sqrMagnitude < MinSpacing * MinSpacing)
                        { tooClose = true; break; }

                    if (!tooClose) massifs.Add(MakeMassif(random, p, inner));
                }

                // The divide wanders. A straight spine is as much of a tell as a straight
                // coastline; real main divides run 1.2 to 1.6 times their straight-line length.
                heading += ((float)random.NextDouble() - 0.5f) * 0.9f;
                float step = Mathf.Lerp(9000f, 13000f, (float)random.NextDouble());
                p += new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * step;
            }
        }

        return massifs;
    }

    static Massif MakeMassif(System.Random random, Vector2 p, float inner)
    {
        float far = Mathf.InverseLerp(inner, OuterRadius, p.magnitude);

        // Height falls with distance: the nearest ranges are the ones the player reads as
        // "the next valley", the far ones only close the horizon.
        float heightScale = Mathf.Lerp(1f, 0.6f, far);

        // SUMMIT ACCORDANCE. In one real range the summits cluster in a 500 to 1200 m band
        // rather than spreading uniformly, so the draw is skewed towards the top of the range
        // and only a few peaks stand clear of their neighbours.
        float roll = (float)random.NextDouble();
        float peak = Mathf.Lerp(900f, 2900f, Mathf.Pow(roll, 0.55f)) * heightScale;

        return new Massif
        {
            Centre = p,
            Radius = Mathf.Lerp(4200f, 9500f, (float)random.NextDouble()),
            Peak = peak,
            RidgeShare = Mathf.Lerp(0.45f, 1f, (float)random.NextDouble()),
            Sharpness = Mathf.Lerp(0.85f, 1.7f, (float)random.NextDouble()),
            Lacunarity = Mathf.Lerp(1.75f, 2.55f, (float)random.NextDouble()),
            Gain = Mathf.Lerp(0.42f, 0.66f, (float)random.NextDouble()),
            Rotation = (float)random.NextDouble() * Mathf.PI * 2f,
            NoiseOffset = new Vector2((float)random.NextDouble() * 4000f,
                                      (float)random.NextDouble() * 4000f),
        };
    }

    // ------------------------------------------------------------------ height

    static float Fbm(Vector2 p, int octaves, float lacunarity, float gain)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(p.x, p.y) * amp;
            norm += amp;
            p *= lacunarity;
            amp *= gain;
        }
        return sum / Mathf.Max(norm, 1e-4f);
    }

    static float Ridged(Vector2 p, int octaves, float lacunarity, float gain)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            float n = 1f - Mathf.Abs(Mathf.PerlinNoise(p.x, p.y) * 2f - 1f);
            sum += n * n * amp;
            norm += amp;
            p *= lacunarity;
            amp *= gain;
        }
        return sum / Mathf.Max(norm, 1e-4f);
    }

    /// SMOOTH MAXIMUM, NOT A SUM. Summing overlapping massifs piles their heights on top of
    /// each other and the range turns into one swollen dome; a smooth max keeps each massif's
    /// own height and opens a SADDLE where two of them meet, which is what a real col is.
    static float SmoothMax(float a, float b, float k)
    {
        float h = Mathf.Clamp01(0.5f + 0.5f * (a - b) / Mathf.Max(k, 1e-3f));
        return Mathf.Lerp(b, a, h) + k * h * (1f - h);
    }

    static float HeightAt(Vector2 world, List<Massif> massifs)
    {
        // THE PLAIN THE RANGES STAND ON. Without it the massifs float as separate cones over
        // a flat sheet; low relief between them is what makes them read as one landscape.
        float plain = PlainHeight
                    + Fbm(world * 0.00004f, 4, 2.1f, 0.5f) * 190f
                    - 60f;

        float height = plain;

        foreach (var m in massifs)
        {
            Vector2 d = world - m.Centre;
            float dist = d.magnitude;
            if (dist > m.Radius * 1.6f) continue;

            // The massif's own frame: rotating the noise basis per massif is what stops the
            // whole range sharing one axis-aligned grain, at no cost.
            float c = Mathf.Cos(m.Rotation), s = Mathf.Sin(m.Rotation);
            var local = new Vector2(d.x * c - d.y * s, d.x * s + d.y * c);

            // THE FIRST OCTAVE IS THE ONE THAT READS. At 20 to 60 km the mesh carries a
            // vertex every 46 to 184 m, so anything finer than about 400 m is averaged away
            // before it reaches the screen. The base wavelength is therefore ~1.7 km — the
            // scale of a ridge and its neighbouring couloir, not of surface texture.
            Vector2 n = local * 0.0006f + m.NoiseOffset;

            // ONE domain warp level. It bends the ridge lines off the noise's own grid;
            // more levels dissolve the ridge instead of bending it.
            var warp = new Vector2(Fbm(n * 0.6f + new Vector2(11.3f, 4.7f), 3, 2f, 0.5f) - 0.5f,
                                   Fbm(n * 0.6f + new Vector2(2.9f, 17.1f), 3, 2f, 0.5f) - 0.5f);
            n += warp * 0.9f;

            float ridged = Mathf.Pow(Ridged(n, 5, m.Lacunarity, m.Gain), m.Sharpness);
            float rolling = Fbm(n, 5, m.Lacunarity, m.Gain);
            float shape = Mathf.Lerp(rolling, ridged, m.RidgeShare);

            // THE FLOOR UNDER THE SHAPE IS WHAT MADE THEM DOMES. With `0.35 + 0.65 * shape`
            // a third of the height came from the profile alone, so every massif carried the
            // profile's own smooth dome no matter what the noise said. At 0.10 the silhouette
            // is the RIDGE FIELD and the profile only decides how far it reaches.
            float t = Mathf.Clamp01(1f - dist / m.Radius);
            float profile = Mathf.Pow(t * t * (3f - 2f * t), 0.75f);

            float local_h = plain + m.Peak * profile * (0.22f + 0.78f * shape);

            height = SmoothMax(height, local_h, m.Radius * 0.00018f * m.Peak * 0.12f + 40f);
        }

        return height;
    }

    // ------------------------------------------------------------------ mesh

    static Mesh BuildMesh(List<Massif> massifs)
    {
        var vertices = new List<Vector3>(AngularSegments * RadialRings);
        var colors = new List<Color32>(AngularSegments * RadialRings);
        var triangles = new List<int>(AngularSegments * (RadialRings - 1) * 6);

        float inner = PlayableHalf + InnerMargin;

        for (int ri = 0; ri < RadialRings; ri++)
        {
            // Geometric spacing: constant angular size per ring instead of constant metres.
            float t = ri / (float)(RadialRings - 1);
            float radiusScale = Mathf.Pow(OuterRadius / inner, t);

            for (int ai = 0; ai < AngularSegments; ai++)
            {
                float angle = ai * Mathf.PI * 2f / AngularSegments;
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);

                // The inner boundary follows the playable SQUARE, not a circle: a circle at
                // 15.1 km would cut through the corners, which reach 21.2 km.
                float squareR = inner / Mathf.Max(Mathf.Abs(ca), Mathf.Abs(sa));
                float radius = squareR * radiusScale;

                var world = new Vector2(ca * radius, sa * radius);

                // THE GEOLOGICAL HEIGHT AND THE DRAWN HEIGHT ARE TWO DIFFERENT THINGS.
                //
                // Snow does not care that the Earth curves away: a 2 km summit is above the
                // snow line whether it is 20 km or 120 km from the viewer. Colouring from the
                // CURVED height made the far ranges read as bare rock — the curvature had
                // taken 1.5 km off them before the snow line was tested.
                float ground = HeightAt(world, massifs);

                // THE EARTH BENDS AWAY. Without this the ring's far rim stands at full height
                // and ends in mid-air; with it the ground falls 1.5 km over 140 km and the rim
                // is under the horizon from any altitude the player can reach.
                float h = ground - radius * radius / (2f * EarthRadius);

                // The two innermost rings sit under the playable terrain and are pulled well
                // below its foot: nothing of them should ever be visible, and any tilt of the
                // camera that would catch them catches the terrain first.
                if (ri <= 1) h = Mathf.Min(h, SeaLevel) - 500f;

                vertices.Add(new Vector3(world.x, h, world.y));
                colors.Add(SurfaceColor(ground));
            }
        }

        for (int ri = 0; ri < RadialRings - 1; ri++)
        for (int ai = 0; ai < AngularSegments; ai++)
        {
            int a0 = ri * AngularSegments + ai;
            int a1 = ri * AngularSegments + (ai + 1) % AngularSegments;
            int b0 = a0 + AngularSegments;
            int b1 = a1 + AngularSegments;

            triangles.Add(a0); triangles.Add(b0); triangles.Add(b1);
            triangles.Add(a0); triangles.Add(b1); triangles.Add(a1);
        }

        var mesh = new Mesh { name = "DistantRange", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }

    /// COLOUR IS BAKED, NOT TEXTURED. At 20 km and beyond a texture is sub-pixel; what reads
    /// is the snow line as a horizontal band and the lit/shadowed split. Both are height and
    /// slope, so both fit in a vertex colour.
    static Color32 SurfaceColor(float height)
    {
        var rock = new Color(0.16f, 0.155f, 0.15f);
        var upland = new Color(0.19f, 0.20f, 0.185f);
        var snow = new Color(0.72f, 0.74f, 0.78f);

        float alpine = Mathf.InverseLerp(200f, 600f, height);
        Color ground = Color.Lerp(rock, upland, alpine);

        // THE SNOW LINE IS LOW, BECAUSE THIS WORLD IS COLD. The scene reads -0,6 C at the
        // beach and -13 C at 2 km; a Himalayan 5 km snow line would be wrong here, and a
        // 1250 m one left the ranges bare — MEASURED, the summits land around 60% of a
        // massif's nominal peak once the ridge field is averaged in, so a 2 km massif tops
        // out near 1,2 km and never reached the old band at all.
        //
        // It is a BAND, not a step: on a real range the line wanders a few hundred metres
        // with aspect and wind.
        float snowLine = Mathf.InverseLerp(650f, 1150f, height);
        Color c = Color.Lerp(ground, snow, snowLine * snowLine);

        return c;
    }

    // ------------------------------------------------------------------ material

    static Material EnsureMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shader = Shader.Find(ShaderName);

        if (shader == null)
        {
            Debug.LogError($"Uzak sıra shader'ı bulunamadı: {ShaderName}");
            return material;
        }

        if (material == null)
        {
            material = new Material(shader) { name = "M_DistantRange" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
            EditorUtility.SetDirty(material);
        }

        return material;
    }
}
