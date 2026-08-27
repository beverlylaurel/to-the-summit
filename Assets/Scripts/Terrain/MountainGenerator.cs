using System;
using UnityEngine;

/// Procedural mountain heightmap. Every parameter comes from the MountainSettings asset.
[RequireComponent(typeof(Terrain))]
public class MountainGenerator : MonoBehaviour
{
    [SerializeField] MountainSettings settings;

    /// Slope distribution per elevation band. Order: walkable (0-30°), hard (30-45°),
    /// climbing (45-70°), wall (70°+). In percent.
    [System.Serializable]
    public struct SlopeBand
    {
        public float walkable;
        public float strenuous;
        public float climbable;
        public float wall;
        public float meanDegrees;
    }

    public const int AltitudeBandCount = 4;

    // Derived data: Generate or Measure recomputes it on every setup run and the consumers
    // (driver binding, report, Tuner) always read it after that computation. Serialized, every
    // computation dirtied the scene — every press of Play entered the commit as a difference in
    // the slope statistics.
    [System.NonSerialized] public SlopeBand[] bands = new SlopeBand[AltitudeBandCount];
    [System.NonSerialized] public float meanSlopeDegrees;
    /// The real summit of the generated terrain (metres). terrainHeight is only a ceiling.
    [System.NonSerialized] public float peakAltitude;
    [System.NonSerialized] public float groundAltitude;
    [HideInInspector] public string lastBuildSignature;

    struct Peak
    {
        public Vector2 center;
        public float radius;
        public float height;
    }

    Vector2 warpOffsetA, warpOffsetB, warpDetailOffsetA, warpDetailOffsetB;
    Vector2 radialOffset, terraceOffset, gridOffset;
    Vector2[] octaveOffsets;
    Peak[] peaks;
    int effectiveOctaves;

    public MountainSettings Settings => settings;

    /// The number of octaves the resolution can carry. More than that produces aliasing.
    public int EffectiveOctaves => effectiveOctaves;

    public void Bind(MountainSettings source) => settings = source;

    public void Generate() => Generate(settings.heightmapResolution);

    /// <param name="resolution">A lower resolution may be given for a preview.</param>
    public void Generate(int resolution)
    {
        if (settings == null)
            throw new System.InvalidOperationException($"{nameof(MountainGenerator)}: settings are not assigned.");
        if (settings.heightProfile == null || settings.heightProfile.length == 0)
            throw new System.InvalidOperationException($"{nameof(MountainGenerator)}: the profile curve is empty.");

        var terrain = GetComponent<Terrain>();
        var data = terrain.terrainData;

        data.heightmapResolution = resolution;
        data.size = new Vector3(settings.terrainSize, settings.terrainHeight, settings.terrainSize);

        // The summit stays at the origin even if the size changes
        transform.position = new Vector3(-settings.terrainSize * 0.5f, 0f, -settings.terrainSize * 0.5f);

        InitRandomState();

        int res = data.heightmapResolution;
        effectiveOctaves = MaxOctavesFor(res);
        var heights = new float[res, res];
        float inv = 1f / (res - 1f);

        // PARALLEL ROW BY ROW. Four thousand squared samples took about half a minute on a single
        // core; every cell is computed independently of the others and there is no shared state,
        // so splitting it is free.
        //
        // TWO CONDITIONS WERE MET. One: `AnimationCurve.Evaluate` is not thread safe (it keeps a
        // cache inside), so the profile curve is baked into an array first. Two: the landform
        // weights were in a shared field; they were moved to the stack.
        BakeProfileLut();

        System.Threading.Tasks.Parallel.For(0, res, z =>
        {
            float v = z * inv;
            for (int x = 0; x < res; x++)
                heights[z, x] = SampleHeight(x * inv, v);
        });

        Erode(heights, res);
        FileCrests(heights, res);
        VerifyFinite(heights, res);

        data.SetHeights(0, 0, heights);
        terrain.Flush();

        ComputeSlopeStats(heights, res);
    }

    /// Files down the sharp tips: cells rising above the average of their neighbours are pulled
    /// down by a fraction of how far they overshoot.
    ///
    /// The folding of the ridge noise produced every peak as a single-sample tooth and the ridge
    /// lines turned into a saw. Because concavities produce no overshoot, valleys and slopes are
    /// left untouched; and because a broad ridge's own curvature produces only a small overshoot
    /// at the sample scale, the large form barely moves.
    ///
    /// The filing is PROPORTIONAL, not thresholded. A thresholded version was tried and was wrong:
    /// it shaved every tooth to just below the threshold and what was left was a field of
    /// equal-sized mini pyramids — uniformity, a more visible pattern than irregular large teeth.
    /// Proportionally a large tooth comes down a lot and a small one a little; the irregularity is kept.
    ///
    /// How it differs from thermal erosion: it moves no material and does not look at the angle.
    /// Erosion was tried with the strength raised and it turned the mountain into scree cones —
    /// the problem was not the steepness of the slope but the sharpness of the tip.
    void FileCrests(float[,] heights, int res)
    {
        if (settings.crestSoftening <= 0f) return;

        // The window is two samples in radius. A one-sample window only sees single-sample sharp
        // tips; a sharp ridge running diagonally across the grid is broken into two- or three-sample
        // steps (each step a quad) and survived the narrow window. Up close the full resolution
        // showed that staircase and far away the LOD skipped the corners and flattened the
        // silhouette — that was the reason for "the teeth appear as you get closer".
        const int Iterations = 4;
        const int Radius = 2;

        // THE FILING IS APPLIED TO THE MOUNTAIN ONLY. It ran on the plain as well and erased the
        // hummocks there: the window is 2 samples in radius, i.e. 37 metres across — the whole size
        // of the plain's thinnest hummock. A bump that size counted as entirely "excess" inside the
        // window and came down to 0.45^4 = 4% over four rounds. Measured: the plain layer produces
        // ±5 metres and arrived on the terrain as 1.7 metres.
        //
        // The filing's job is to prevent sharp ridges running diagonally across the grid from
        // becoming staircases; that problem exists on the mountain's steep faces, not on the flat.
        float centre = (res - 1) * 0.5f;
        float skirt = settings.mountainRadius * (res - 1);

        var next = new float[res, res];

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            System.Array.Copy(heights, next, heights.Length);

            // PARALLEL ROW BY ROW. Four rounds, twenty-five samples per cell, a four thousand
            // squared grid: one and a half billion reads. Every row only reads from `heights` and
            // writes to `next`, so splitting it is safe.
            System.Threading.Tasks.Parallel.For(Radius, res - Radius, z =>
            {
            for (int x = Radius; x < res - Radius; x++)
            {
                float sum = 0f;

                for (int dz = -Radius; dz <= Radius; dz++)
                for (int dx = -Radius; dx <= Radius; dx++)
                    sum += heights[z + dz, x + dx];

                float mean = (sum - heights[z, x]) / 24f;

                float excess = heights[z, x] - mean;
                if (excess <= 0f) continue;

                float offsetX = x - centre, offsetZ = z - centre;
                float distance = Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);

                // It falls to zero outside the foot line; the transition band was kept narrow so
                // it does not spill onto the plain.
                float strength = settings.crestSoftening
                    * (1f - Mathf.SmoothStep(skirt * 0.92f, skirt * 1.08f, distance));

                if (strength > 0f) next[z, x] = heights[z, x] - excess * strength;
            }
            });

            System.Array.Copy(next, heights, heights.Length);
        }
    }

    /// Thermal erosion: material on slopes exceeding the talus angle flows to the neighbours below.
    /// Sharp fractures turn into scree slopes, the large-scale form is preserved.
    void Erode(float[,] heights, int res)
    {
        if (settings.erosionIterations <= 0) return;

        float cellSize = settings.terrainSize / (res - 1f);

        // The talus angle expressed in normalized height: the largest difference two neighbours
        // can hold without material moving
        float maxDelta = Mathf.Tan(settings.talusAngle * Mathf.Deg2Rad)
                         * cellSize / settings.terrainHeight;

        var delta = new float[res, res];

        for (int iteration = 0; iteration < settings.erosionIterations; iteration++)
        {
            System.Array.Clear(delta, 0, delta.Length);

            // THE EROSION IS NOT PARALLEL. It was tried and reverted: every cell writes to
            // neighbouring ROWS too (`delta[z-1, x]`, `delta[z+1, x]`), so the rows are not
            // independent. Split, two threads update the same cell at the same time and part of
            // the moved material is lost — a silent, local, unreproducible corruption.
            for (int z = 1; z < res - 1; z++)
            for (int x = 1; x < res - 1; x++)
            {
                float h = heights[z, x];

                // Look at the four neighbours; measure the sum of the differences exceeding the talus angle
                float e0 = Excess(h, heights[z, x - 1], maxDelta);
                float e1 = Excess(h, heights[z, x + 1], maxDelta);
                float e2 = Excess(h, heights[z - 1, x], maxDelta);
                float e3 = Excess(h, heights[z + 1, x], maxDelta);
                float excess = e0 + e1 + e2 + e3;

                if (excess <= 0f) continue;

                // Distribute the overflowing material in proportion to the slope
                float moved = Mathf.Min(excess, (h - LowestNeighbour(heights, x, z)) * 0.5f)
                              * settings.erosionRate;
                if (moved <= 0f) continue;

                delta[z, x] -= moved;
                delta[z, x - 1] += moved * (e0 / excess);
                delta[z, x + 1] += moved * (e1 / excess);
                delta[z - 1, x] += moved * (e2 / excess);
                delta[z + 1, x] += moved * (e3 / excess);
            }

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                heights[z, x] = Mathf.Clamp01(heights[z, x] + delta[z, x]);
        }
    }

    static float Excess(float h, float neighbour, float maxDelta)
    {
        float difference = h - neighbour - maxDelta;
        return difference > 0f ? difference : 0f;
    }

    static float LowestNeighbour(float[,] heights, int x, int z)
    {
        float lowest = heights[z, x - 1];
        if (heights[z, x + 1] < lowest) lowest = heights[z, x + 1];
        if (heights[z - 1, x] < lowest) lowest = heights[z - 1, x];
        if (heights[z + 1, x] < lowest) lowest = heights[z + 1, x];
        return lowest;
    }

    /// A corrupt value shows up on the terrain as single-sample pits and is hard to notice.
    /// It is verified after generation so it does not pass silently.
    static void VerifyFinite(float[,] heights, int res)
    {
        for (int z = 0; z < res; z++)
        for (int x = 0; x < res; x++)
        {
            float h = heights[z, x];
            if (float.IsNaN(h) || float.IsInfinity(h))
                throw new System.InvalidOperationException(
                    $"{nameof(MountainGenerator)}: invalid value in the heightmap ({x}, {z}) = {h}");
        }
    }

    /// Measures the current terrain without regenerating it.
    public void Measure()
    {
        var data = GetComponent<Terrain>().terrainData;
        int res = data.heightmapResolution;

        effectiveOctaves = MaxOctavesFor(res);
        ComputeSlopeStats(data.GetHeights(0, 0, res, res), res);
    }

    /// Noise above the sampling rate produces aliasing: single-sample random jumps, i.e. specks
    /// and pits. The finest wavelength has to be at least this many samples wide.
    const float MinSamplesPerWavelength = 4f;

    const int OctaveCeiling = 12;

    int MaxOctavesFor(int resolution)
    {
        float sampleSize = settings.terrainSize / (resolution - 1f);
        float minWavelength = MinSamplesPerWavelength * sampleSize;

        // Oktav i'nin dalgaboyu: terrainSize / (baseFrequency * lacunarity^i)
        float ratio = settings.terrainSize / (settings.baseFrequency * minWavelength);
        if (ratio <= 1f) return 1;

        int limit = Mathf.FloorToInt(Mathf.Log(ratio) / Mathf.Log(settings.lacunarity)) + 1;
        return Mathf.Clamp(limit, 1, OctaveCeiling);
    }

    void InitRandomState()
    {
        var rng = new System.Random(settings.seed);

        warpOffsetA = RandomOffset(rng);
        warpOffsetB = RandomOffset(rng);
        warpDetailOffsetA = RandomOffset(rng);
        warpDetailOffsetB = RandomOffset(rng);
        radialOffset = RandomOffset(rng);
        terraceOffset = RandomOffset(rng);
        gridOffset = RandomOffset(rng);

        octaveOffsets = new Vector2[OctaveCeiling];
        for (int i = 0; i < OctaveCeiling; i++)
            octaveOffsets[i] = RandomOffset(rng);

        InitPeaks(rng);
    }

    /// The side peaks are spread around the main summit; they give a sense of shoulders and secondary tops
    void InitPeaks(System.Random rng)
    {
        peaks = new Peak[settings.secondaryPeaks];
        float angleStep = Mathf.PI * 2f / Mathf.Max(1, settings.secondaryPeaks);

        for (int i = 0; i < peaks.Length; i++)
        {
            // An evenly spaced base angle plus a random deviation: so there are both clusters and gaps
            float angle = angleStep * (i + (float)rng.NextDouble() * 0.7f - 0.35f);
            float distance = settings.mountainRadius * settings.peakSpread
                             * (0.6f + (float)rng.NextDouble() * 0.8f);

            peaks[i] = new Peak
            {
                center = new Vector2(0.5f + Mathf.Cos(angle) * distance, 0.5f + Mathf.Sin(angle) * distance),
                radius = settings.mountainRadius * Mathf.Lerp(
                    settings.peakRadiusRange.x, settings.peakRadiusRange.y, (float)rng.NextDouble()),
                height = Mathf.Lerp(
                    settings.peakHeightRange.x, settings.peakHeightRange.y, (float)rng.NextDouble())
            };
        }
    }

    static Vector2 RandomOffset(System.Random rng)
        => new((float)rng.NextDouble() * 10000f, (float)rng.NextDouble() * 10000f);

    /// THE PROFILE CURVE IS BAKED INTO AN ARRAY. `AnimationCurve.Evaluate` is not thread safe:
    /// it caches the last key it looked up and calling it from two threads at once can return a
    /// wrong value. With generation parallelized this would not be a deadlock but a silent
    /// corruption — a wrong height at random places on the terrain.
    ///
    /// Two thousand samples is far above the curve's own resolution; and an array read is several
    /// times faster than a curve evaluation.
    const int ProfileLutSize = 2048;
    float[] profileLut;

    void BakeProfileLut()
    {
        profileLut = new float[ProfileLutSize];
        for (int i = 0; i < ProfileLutSize; i++)
            profileLut[i] = settings.heightProfile.Evaluate(i / (ProfileLutSize - 1f));
    }

    /// A read from the baked profile curve, interpolated.
    float ProfileAt(float t)
    {
        float x = Mathf.Clamp01(t) * (ProfileLutSize - 1);
        int i = (int)x;
        int j = Mathf.Min(i + 1, ProfileLutSize - 1);
        return Mathf.Lerp(profileLut[i], profileLut[j], x - i);
    }

    float SampleHeight(float u, float v)
    {
        // Domain warp: distorting the coordinates breaks the symmetry and gives natural ridges
        float wx = Mathf.PerlinNoise(u * settings.warpFrequency + warpOffsetA.x,
                                     v * settings.warpFrequency + warpOffsetA.y) - 0.5f;
        float wz = Mathf.PerlinNoise(u * settings.warpFrequency + warpOffsetB.x,
                                     v * settings.warpFrequency + warpOffsetB.y) - 0.5f;

        float dx2 = Mathf.PerlinNoise(u * settings.warpDetailFrequency + warpDetailOffsetA.x,
                                      v * settings.warpDetailFrequency + warpDetailOffsetA.y) - 0.5f;
        float dz2 = Mathf.PerlinNoise(u * settings.warpDetailFrequency + warpDetailOffsetB.x,
                                      v * settings.warpDetailFrequency + warpDetailOffsetB.y) - 0.5f;

        float su = u + wx * settings.warpStrength + dx2 * settings.warpDetailStrength;
        float sv = v + wz * settings.warpStrength + dz2 * settings.warpDetailStrength;

        float profile = MainProfile(su, sv);

        foreach (var peak in peaks)
        {
            float d = Vector2.Distance(new Vector2(su, sv), peak.center) / Mathf.Max(0.001f, peak.radius);
            if (d >= 1f) continue;

            float contribution = ProfileAt(d) * peak.height;
            profile = Mathf.Max(profile, contribution);
        }

        RidgedFbm(su, sv, out float low, out float detail);

        // The ridge effect strengthens with height: the foot stays soft, the summit sharpens
        float influence = settings.ridgeInfluence
                          * Mathf.Lerp(settings.ridgeFootDamping, 1f, profile);

        // The multiplier's mean is kept at 1; the ridge noise must not systematically lower the mountain
        float h = profile * (1f + influence * (low - 0.5f));

        // The terracing is applied only to the low-frequency main form. If the fine detail is
        // quantized, single-sample pits form wherever the noise crosses a band boundary.
        h = ApplyTerraces(h, su, sv, profile);
        h += profile * influence * detail;

        h = ApplySummitPlateau(h);

        h = settings.baseHeight + h * (1f - settings.baseHeight);

        // THE PLAIN is added last: its amplitudes are given in real metres and would shrink if
        // they went through the base scaling.
        h += Foreland(su, sv, profile);

        return Mathf.Clamp01(h);
    }

    /// THE MOUNTAIN'S FOREGROUND. The terrain generator makes a radial mountain and the profile
    /// falls to zero outside the radius: what was left was a perfectly flat table.
    ///
    /// NOT ONE NOISE BUT FIVE LANDFORMS. The previous version applied the same noise everywhere
    /// and varied its amplitude from place to place; the result was "everywhere looks the same",
    /// and rightly so: varying the amplitude of an area whose character does not change does not
    /// produce a different place, it produces a high and a low version of the same place.
    ///
    /// A real mountain foreground is divided into regions and the regions DO NOT RESEMBLE each other:
    ///   moraine field - chaotic mounds, closed hollows, no direction
    ///   outwash plain - almost flat, braided shallow beds
    ///   terraces      - stepped flats with short steep rises between them
    ///   gullied slope - dense parallel streams, sharp ridges
    ///   block field   - the apron below a rockfall, coarse and irregular
    ///
    /// Which landform is where comes from a low-frequency field (~2200 m), so a five kilometre
    /// route crosses two or three regions. The boundaries are warped: sharp circles read as artificial.
    ///
    /// THE LOWER BOUND IS 36 METRES. The terrain grid is 7.32 m/sample and a feature wants at
    /// least four or five samples. Nothing at the metre scale — rock, block, stone pile — can come
    /// out of here; those arrive as separate models.
    float Foreland(float su, float sv, float profile)
    {
        // It fades as the mountain's foot is approached: the terrain there already comes from the
        // mountain's own form and if two sources overlap the foot blisters.
        float outside = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(profile / 0.12f));
        if (outside <= 0.001f) return 0f;

        float dx = su - 0.5f;
        float dz = sv - 0.5f;
        float radius = Mathf.Sqrt(dx * dx + dz * dz);

        // The region boundaries are warped: unwarped low-frequency noise gives round patches and
        // the transitions read like circular arcs.
        float warpU = (SignedNoise(su * Frequency(900f) + 3.7f,
                                         sv * Frequency(900f) + 8.1f) * 0.5f) * 0.06f;
        float warpV = (SignedNoise(su * Frequency(900f) + 51.2f,
                                         sv * Frequency(900f) + 27.4f) * 0.5f) * 0.06f;

        float ru = su + warpU;
        float rv = sv + warpV;

        // AN ALLUVIAL FAN exists in every region: streams carry material downhill and the plain
        // falls away from the foot. Not a landform, the general tendency of the ground.
        float beyond = Mathf.Max(0f, radius - settings.mountainRadius);
        float metres = -beyond * settings.forelandFanDrop
                     / Mathf.Max(0.01f, 0.707f - settings.mountainRadius);

        // Landform weights: each landform has its own low-frequency field and the highest one
        // wins. The exponent was 5 and the winner only took 70% of the share: the remaining 30%
        // spread over the other four and cancelled each other out, erasing the region's character.
        // At the tenth power the winner's share rises to 95% and the boundaries are still soft.
        Span<float> landformWeights = stackalloc float[LandformCount];

        float total = 0f;
        for (int i = 0; i < LandformCount; i++)
        {
            float field = UnitNoise(ru * Frequency(2200f) + i * 37.13f + 9.4f,
                                            rv * Frequency(2200f) + i * 21.77f + 5.8f);
            landformWeights[i] = Mathf.Pow(Mathf.Clamp01(field), 10f);
            total += landformWeights[i];
        }

        if (total < 1e-5f) { landformWeights[0] = 1f; total = 1f; }

        metres += landformWeights[0] / total * MoraineField(ru, rv)
                + landformWeights[1] / total * OutwashPlain(ru, rv)
                + landformWeights[2] / total * Terraces(ru, rv)
                + landformWeights[3] / total * GullySlope(ru, rv)
                + landformWeights[4] / total * BoulderApron(ru, rv);

        // A SHARED HUMMOCK LAYER. Whatever the region's landform is, the ground is rough: a gravel
        // bar on the outwash plain, a bump on a terrace, a mound on the moraine field. Measured —
        // without this layer the outwash plain and terrace regions have only 1.5 metres of relief
        // over 60 metres and the peaks fall 160 metres apart; for someone walking, that is flat ground.
        //
        // The wavelengths are 34 and 21 metres: the size you walk past, not the size you climb
        // over. It does not go below thirty-six metres, the terrain grid (7.32 m) cannot resolve it.
        metres += Bumps(ru, rv);

        return metres / Mathf.Max(1f, settings.terrainHeight) * outside;
    }

    /// HUMMOCKS. The shared roughness that rides on top of every landform. Bumps you walk past;
    /// not an obstacle to be climbed, the ground's texture.
    ///
    /// The density is PATCHY: real terrain is not equally rough everywhere — one slope is stony
    /// and bumpy, the meadow beside it is flat. Uniform roughness reads as "flat ground with noise
    /// applied".
    float Bumps(float u, float v)
    {
        // Three scales: 55, 32 and 21 metres. At a single scale the hummocks all come out the same
        // size and read as "the same stamp repeated"; superposing three scales makes the size
        // distribution natural - coarse, medium and small side by side.
        float wide = SignedNoise(u * Frequency(95f) + 5.5f, v * Frequency(95f) + 9.2f);
        float mid = SignedNoise(u * Frequency(58f) + 44.1f, v * Frequency(58f) + 12.7f);
        float fine = SignedNoise(u * Frequency(37f) + 77.3f, v * Frequency(37f) + 51.8f);

        float patch = UnitNoise(u * Frequency(600f) + 88.4f, v * Frequency(600f) + 23.6f);
        float density = Mathf.Lerp(0.5f, 1.6f, Mathf.SmoothStep(0f, 1f, patch));

        return (wide * 1.6f + mid * 1.2f + fine * 0.8f)
             * density * settings.hummockHeight * 0.7f;
    }

    /// MORAINE FIELD. The chaotic mounds a glacier left behind and the closed hollows between them.
    /// No direction: no ridges, no arcs, just piles. The most tiring ground to walk - you cannot
    /// hold a straight line, you are constantly going up and down.
    float MoraineField(float u, float v)
    {
        // NO SINE. The arcs were produced with `sin(radius / spacing)` and repeated by definition:
        // evenly spaced ridges of equal height. Looking tangentially at a concentric pattern, the
        // same triangle appeared again and again on the horizon - a saw tooth.
        // Adding a warp hides the order, it does not remove it.
        //
        // In its place, ridge noise: two fields at two different scales that do not look at each
        // other. No ridge is the same as another and their spacings are not equal either.
        float coarse = SignedNoise(u * Frequency(430f) + 11.7f,
                                   v * Frequency(430f) + 4.1f);
        float ridgeCoarse = Mathf.Pow(1f - Mathf.Abs(coarse), 2.5f);

        float mid = SignedNoise(u * Frequency(190f) + 63.2f,
                                v * Frequency(190f) + 28.5f);
        float ridgeMid = Mathf.Pow(1f - Mathf.Abs(mid), 3f);

        // The two ridge families are NOT SUMMED, the highest is taken: summed, they rise twice as
        // high where they cross and the crossing points form a regular grid.
        float ridges = Mathf.Max(ridgeCoarse, ridgeMid * 0.75f);

        // Mounds: two scales, directionless and dense.
        float mound = SignedNoise(u * Frequency(88f) + 5.5f,
                                  v * Frequency(88f) + 9.2f) * 0.65f
                    + SignedNoise(u * Frequency(44f) + 44.1f,
                                  v * Frequency(44f) + 12.7f) * 0.4f;

        // CLOSED HOLLOWS (kettle holes): the moraine field's signature. The basins left by a melted
        // block of ice; filled with water they become ponds.
        float kettle = UnitNoise(u * Frequency(160f) + 71.9f,
                                 v * Frequency(160f) + 33.2f);
        float basin = -Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(kettle - 0.5f) * 2.6f), 3f) * 7f;

        // The ridge height varies from place to place: some pronounced, some erased.
        float relief = Mathf.Lerp(0.3f, 1.4f, UnitNoise(u * Frequency(520f) + 45.9f,
                                                        v * Frequency(520f) + 12.1f));

        return ridges * relief * settings.moraineHeight
             + mound * settings.hummockHeight + basin;
    }

    /// OUTWASH PLAIN. The flat spread by the gravel that glacial meltwater carried: almost dead
    /// flat, with braided shallow beds on it. The ground that has to be fast to walk, open and
    /// plain - this is the character of the easy route.
    float OutwashPlain(float u, float v)
    {
        // Braided beds: shallow and wide, merging into each other. The depth is under a metre;
        // crossing here does not slow you down, it only stops the ground being flat.
        float braid = UnitNoise(u * Frequency(120f) + 17.3f,
                                        v * Frequency(120f) + 61.5f);
        float cut = -Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(braid - 0.5f) * 2f), 3f) * 1.6f;

        float ripple = (SignedNoise(u * Frequency(52f) + 2.9f,
                                          v * Frequency(52f) + 8.3f) * 0.5f) * 0.9f;

        return cut + ripple;
    }

    /// TERRACES. The stepped flats left by old stream levels: wide flat areas with short steep
    /// rises between them. These are the places camps are pitched; walking is easy but you have
    /// to find the step.
    float Terraces(float u, float v)
    {
        // NO EQUAL STEPS. It was quantized with `floor(x * 5)`: five steps, each 5.2 metres, all
        // identical. In nature terrace heights vary with the stream, the time and the material;
        // equal steps read as a staircase.
        //
        // The step edges are now where a noise crosses its own thresholds: both the heights and
        // the spacings are irregular.
        float field = UnitNoise(u * Frequency(1400f) + 13.1f,
                                v * Frequency(1400f) + 47.6f);

        // The flats: instead of suppressing the area by its own gradient I pass it through a soft
        // step function. The threshold slides from place to place, so the distance between two
        // flats is not constant.
        float shift = UnitNoise(u * Frequency(760f) + 5.1f,
                                v * Frequency(760f) + 88.3f);

        // Four thresholds, each shifted separately in position and in height.
        float terrace = 0f;
        terrace += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.22f + shift * 0.10f,
                                                              0.30f + shift * 0.10f, field)) * 9f;
        terrace += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.44f - shift * 0.08f,
                                                              0.49f - shift * 0.08f, field)) * 6f;
        terrace += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.63f + shift * 0.12f,
                                                              0.72f + shift * 0.12f, field)) * 12f;
        terrace += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.84f - shift * 0.06f,
                                                              0.88f - shift * 0.06f, field)) * 5f;

        // The flats are not perfectly flat: a fine roughness remains.
        float grain = SignedNoise(u * Frequency(46f) + 6.2f,
                                  v * Frequency(46f) + 19.9f) * 0.55f;

        return terrace + grain;
    }

    /// GULLIED SLOPE. Dense parallel stream beds and the sharp ridges between them. Going down
    /// into a bed and back out slows you; going along one is fast but the terrain dictates your direction.
    float GullySlope(float u, float v)
    {
        float gully = UnitNoise(u * Frequency(260f) + 71.2f,
                                        v * Frequency(260f) + 3.4f);

        float ridge = 1f - Mathf.Abs(gully - 0.5f) * 2f;

        // Bed-floored with steep sides: the sixth power leaves width at the floor.
        float cut = -Mathf.Pow(Mathf.Clamp01(ridge), 4f) * settings.channelDepth;

        // The ridges themselves rise too: as the gullies are cut, what is between them stays as a ridge.
        float crest = Mathf.Pow(Mathf.Clamp01(1f - ridge), 2f) * 6f;

        return cut + crest;
    }

    /// BLOCK FIELD. The apron of coarse material shed from the slope: irregular, coarse grained,
    /// directionless. The slowest ground to walk. The metre-scale blocks themselves do not come
    /// from here; this is the undulating floor they stand on.
    float BoulderApron(float u, float v)
    {
        float lump = (SignedNoise(u * Frequency(95f) + 88.1f,
                                        v * Frequency(95f) + 14.6f) * 0.5f) * 1.4f
                   + (SignedNoise(u * Frequency(41f) + 39.7f,
                                        v * Frequency(41f) + 71.3f) * 0.5f) * 1.0f;

        // The apron lowers with distance from the mountain: thick near the source, thinning at its end.
        float taper = UnitNoise(u * Frequency(700f) + 4.4f,
                                        v * Frequency(700f) + 9.9f);

        return lump * settings.hummockHeight * Mathf.Lerp(0.6f, 1.8f, taper);
    }

    /// SIGNED NOISE, between -1 and 1. `Mathf.PerlinNoise` theoretically returns 0-1 but its mass
    /// gathers between 0.30 and 0.70: writing `(n - 0.5)` gives an amplitude that is not the
    /// expected ±0.5 but really ±0.22, and every layer silently halves.
    ///
    /// The 2.2 scale opens that narrowing back up, and the clamp cuts the rare overshoot at the
    /// ends. Without this correction a "5 metre hummock" arrived on the terrain as 1.4 metres.
    static float SignedNoise(float x, float y) =>
        Mathf.Clamp(Mathf.PerlinNoise(x, y) * 2.2f - 1.1f, -1f, 1f);

    /// The same correction in the 0-1 range. Threshold and mask computations read this: putting a
    /// threshold on a narrowed distribution effectively threw the threshold outside the range.
    static float UnitNoise(float x, float y) =>
        Mathf.Clamp01(Mathf.PerlinNoise(x, y) * 2.2f - 0.6f);

    /// The number of landforms. The weights are kept ON THE STACK: as a shared field, threads
    /// would overwrite each other's weights during parallel generation.
    const int LandformCount = 5;

    /// Frequency = terrain size / desired wavelength. Deriving it from metres rather than writing
    /// the number directly keeps a feature's real size when the mountain's size changes.
    /// The lower bound is 36 m: the terrain grid is 7.32 m/sample and anything finer aliases.
    float Frequency(float wavelength) =>
        settings.terrainSize / Mathf.Max(36f, wavelength);

    /// The main cone's profile. With a circular base the contour lines are circles too and the
    /// terraces look like concentric rings — the radius is distorted by angle.
    float MainProfile(float su, float sv)
    {
        float dx = su - 0.5f;
        float dz = sv - 0.5f;
        float radius = Mathf.Sqrt(dx * dx + dz * dz);

        float angle = Mathf.Atan2(dz, dx);
        float angularNoise = Mathf.PerlinNoise(
            Mathf.Cos(angle) * settings.radialFrequency + radialOffset.x,
            Mathf.Sin(angle) * settings.radialFrequency + radialOffset.y) - 0.5f;

        float effective = settings.mountainRadius * (1f + angularNoise * settings.radialDistortion * 2f);
        float dist = Mathf.Clamp01(radius / Mathf.Max(0.01f, effective));

        return Mathf.Max(0f, ProfileAt(dist));
    }

    float ApplyTerraces(float h, float su, float sv, float profile)
    {
        // No terraces outside the mountain. Because the grid shift produced a value even at zero
        // height, it was pitting the flat terrain.
        if (profile <= 0.001f) return h;

        // It should reach full strength quickly at the foot; it should only fade where the mountain ends
        float footFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(profile / 0.08f));

        // The terrace strength varies by place: pronounced benches on one slope, a plain gradient on another
        float variation = footFade * Mathf.Lerp(1f, Mathf.PerlinNoise(
            su * settings.terraceVariationFrequency + terraceOffset.x,
            sv * settings.terraceVariationFrequency + terraceOffset.y), settings.terraceVariation);

        // The band elevations shift by place; a fixed elevation produces a ring pattern
        float offset = (Mathf.PerlinNoise(
            su * settings.terraceOffsetFrequency + gridOffset.x,
            sv * settings.terraceOffsetFrequency + gridOffset.y) - 0.5f) * settings.terraceOffsetAmount;

        h = Terrace(h, settings.coarseTerraceBands, settings.coarseTerraceStrength * variation, offset);
        h = Terrace(h, settings.fineTerraceBands, settings.fineTerraceStrength * variation, offset * 3f);

        return h;
    }

    /// Splits the height into steps. Because the grid is shifted, the bands do not form at the
    /// same elevation everywhere — they shift from slope to slope like real rock bands.
    float Terrace(float h, int bands, float strength, float offset)
    {
        if (strength <= 0f) return h;

        float shift = offset / bands;
        float t = (h + shift) * bands;
        float band = Mathf.Floor(t);
        float frac = Mathf.Clamp01(t - band);
        float stepped = (band + Mathf.Pow(frac, settings.terraceSharpness)) / bands - shift;

        return Mathf.Lerp(h, stepped, strength);
    }

    float ApplySummitPlateau(float h)
    {
        float start = settings.summitPlateauStart;
        if (start >= 1f || h <= start) return h;

        return start + (h - start) * (1f - settings.summitFlatness);
    }

    /// Ridged multifractal: the absolute value of perlin is inverted, forming sharp ridges.
    /// <paramref name="low"/> is only the first octaves (in the 0-1 range) — the terracing is applied to this.
    /// <paramref name="detail"/> is the remaining high-frequency octaves, with a mean near zero.
    void RidgedFbm(float u, float v, out float low, out float detail)
    {
        float norm = 0f;
        float lowSum = 0f, lowNorm = 0f, highSum = 0f;
        float amp = 1f;
        float freq = settings.baseFrequency;

        int count = Mathf.Clamp(effectiveOctaves, 1, octaveOffsets.Length);

        // The terracing is applied only to the coarse half; quantizing the fine detail produces specks
        int split = Mathf.Clamp(count / 2, 1, count);

        for (int i = 0; i < count; i++)
        {
            float n = Mathf.PerlinNoise(u * freq + octaveOffsets[i].x, v * freq + octaveOffsets[i].y);

            // PerlinNoise rarely goes outside 0-1; if the base falls negative a fractional exponent produces NaN
            n = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(n * 2f - 1f)), settings.ridgeSharpness);

            norm += amp;

            if (i < split)
            {
                lowSum += n * amp;
                lowNorm += amp;
            }
            else
            {
                highSum += (n - 0.5f) * amp;
            }

            amp *= settings.gain;
            freq *= settings.lacunarity;
        }

        low = lowSum / lowNorm;
        detail = highSum / norm;
    }

    /// The slope budget. Because a single area-weighted histogram is dominated by the mountain's
    /// wide foot, the measurement is split into elevation bands.
    void ComputeSlopeStats(float[,] heights, int res)
    {
        float cellSize = settings.terrainSize / (res - 1f);
        float inv = 1f / (res - 1f);

        var counts = new int[AltitudeBandCount, 4];
        var totals = new int[AltitudeBandCount];
        var sums = new double[AltitudeBandCount];

        double allSum = 0;
        int allCount = 0;
        float highest = 0f, lowest = 1f;

        for (int z = 0; z < res - 1; z++)
        {
            float dv = z * inv - 0.5f;

            for (int x = 0; x < res - 1; x++)
            {
                // Flat terrain outside the mountain must not enter the measurement, it inflates the walkable share
                float du = x * inv - 0.5f;
                if (Mathf.Sqrt(du * du + dv * dv) > settings.mountainRadius) continue;

                float dhx = (heights[z, x + 1] - heights[z, x]) * settings.terrainHeight;
                float dhz = (heights[z + 1, x] - heights[z, x]) * settings.terrainHeight;
                float grad = Mathf.Sqrt(dhx * dhx + dhz * dhz) / cellSize;
                float deg = Mathf.Atan(grad) * Mathf.Rad2Deg;

                if (heights[z, x] > highest) highest = heights[z, x];
                if (heights[z, x] < lowest) lowest = heights[z, x];

                int band = Mathf.Clamp(
                    (int)(heights[z, x] * AltitudeBandCount), 0, AltitudeBandCount - 1);

                counts[band, deg < 30f ? 0 : deg < 45f ? 1 : deg < 70f ? 2 : 3]++;
                totals[band]++;
                sums[band] += deg;

                allSum += deg;
                allCount++;
            }
        }

        if (bands == null || bands.Length != AltitudeBandCount)
            bands = new SlopeBand[AltitudeBandCount];

        for (int b = 0; b < AltitudeBandCount; b++)
        {
            if (totals[b] == 0)
            {
                bands[b] = default;
                continue;
            }

            float scale = 100f / totals[b];
            bands[b] = new SlopeBand
            {
                walkable = counts[b, 0] * scale,
                strenuous = counts[b, 1] * scale,
                climbable = counts[b, 2] * scale,
                wall = counts[b, 3] * scale,
                meanDegrees = (float)(sums[b] / totals[b])
            };
        }

        meanSlopeDegrees = allCount > 0 ? (float)(allSum / allCount) : 0f;
        // THE SCALE IS READ FROM THE TERRAIN, NOT FROM THE SETTINGS. `settings.terrainHeight` was
        // the old procedural setup's number (6189); once the terrain started being made by hand
        // the ceiling rose to 8000 and the two diverged. The summit was read as 4642 m instead of
        // 6001 m, the weather bands derived from that wrong height and the mountain was covered in
        // snow from top to bottom.
        float top = GetComponent<Terrain>().terrainData.size.y;
        peakAltitude = highest * top;
        groundAltitude = lowest * top;
    }
}
