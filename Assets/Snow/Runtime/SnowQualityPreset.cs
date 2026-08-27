// ROLE: the numeric meaning of the quality levels (spec §15.3). Once a preset is chosen
// every measure is read from here; there is no if/else at the call sites.
// CALLED BY: SnowSettings, SnowManager, SnowSurface, SnowfallController.

using UnityEngine;

public enum SnowQualityPreset
{
    Low,
    Medium,
    High,
}

/// All the measures of one quality level. A `readonly struct` — the copy is cheap and it
/// cannot be changed by accident.
public readonly struct SnowQualityData
{
    /// The state textures' resolution, in texels. A POWER OF TWO (spec §6.4).
    public readonly int Resolution;

    /// The edge length of the deformation region, in metres. 24 in all three presets (spec §6.1).
    public readonly float AreaSize;

    /// The snow mesh's quad count per edge. A POWER OF TWO and
    /// `MeshGrid ≤ Resolution` (spec §6.4).
    public readonly int MeshGrid;

    /// The sky visibility map's resolution.
    public readonly int SkyResolution;

    /// The number of detail normal layers.
    public readonly int DetailLayers;

    /// The number of sparkle LODs. 0 = off.
    public readonly int SparkleLods;

    /// How many frames KAccumulate takes to complete a full round.
    public readonly int AccumulateTiles;

    /// The VFX capacity multiplier.
    public readonly float VfxCapacityScale;

    /// The number of AO directions inside a trail. 0 = off (spec §18.5).
    public readonly int AoDirs;

    /// The number of steps per AO direction inside a trail.
    public readonly int AoSteps;

    /// The shader keyword.
    public readonly string Keyword;

    public SnowQualityData(int resolution, float areaSize, int meshGrid,
        int skyResolution, int detailLayers, int sparkleLods, int accumulateTiles,
        float vfxCapacityScale, int aoDirs, int aoSteps, string keyword)
    {
        Resolution = resolution;
        AreaSize = areaSize;
        MeshGrid = meshGrid;
        SkyResolution = skyResolution;
        DetailLayers = detailLayers;
        SparkleLods = sparkleLods;
        AccumulateTiles = accumulateTiles;
        VfxCapacityScale = vfxCapacityScale;
        AoDirs = aoDirs;
        AoSteps = aoSteps;
        Keyword = keyword;
    }

    /// The edge of a single quad, in metres (spec §6.4).
    public float QuadSize => AreaSize / MeshGrid;

    /// The edge of a single texel, in metres.
    public float TexelSize => AreaSize / Resolution;

    /// The grid step the region sits on, in metres (spec §6.4).
    ///
    /// The factor of 2 IS NOT NEEDED FOR CORRECTNESS; `1 × quadSize` is valid too.
    /// Twice that was preferred because it halves how often the RT is shifted.
    /// The correctness comes from `MeshGrid` and `Resolution` being powers of two.
    public float SnapStep => QuadSize * SnowConstants.SnapQuads;

    /// How many texels one snap step corresponds to.
    ///
    /// INTEGER ARITHMETIC, NOT FLOAT. Cancelling `SnapStep / TexelSize` gives
    /// `SnapQuads × Resolution / MeshGrid`; because both are powers of two the division
    /// leaves no remainder. Computed with floats a value like 4.0078 comes out, is
    /// silently rounded, and the trails shake below the texel (spec §22).
    public int ScrollTexels => SnowConstants.SnapQuadsInt * (Resolution / MeshGrid);
}

public static class SnowQuality
{
    public const string KeywordLow = "_SNOW_QUALITY_LOW";
    public const string KeywordMedium = "_SNOW_QUALITY_MEDIUM";
    public const string KeywordHigh = "_SNOW_QUALITY_HIGH";

    /// Spec §6.1 and §15.3's tables exactly. The region size is 24 m in all three presets.
    ///
    /// THE GRIDS ARE POWERS OF TWO. The spec's first form gave 255/511/1023 and
    /// `SnapStep / texelSize` came out an integer on no row (measured:
    /// 4.0157 / 4.0078 / 2.0020). The odd-number requirement came from the nesting needs of
    /// a multi-level clipmap; because we draw a single level it does not apply.
    public static SnowQualityData Get(SnowQualityPreset preset)
    {
        switch (preset)
        {
            case SnowQualityPreset.Low:
                return new SnowQualityData(512, 24f, 256, 512, 1, 0, 8, 0.35f, 0, 0, KeywordLow);

            case SnowQualityPreset.High:
                return new SnowQualityData(1024, 24f, 1024, 1024, 4, 2, 4, 1f, 8, 3, KeywordHigh);

            default:
                return new SnowQualityData(1024, 24f, 512, 1024, 2, 1, 4, 0.65f, 4, 2, KeywordMedium);
        }
    }
}
