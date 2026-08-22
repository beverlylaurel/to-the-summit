// ROL: kalite seviyelerinin sayısal karşılığı (spec §15.3). Preset seçilince
// bütün ölçüler buradan okunuyor; çağrı yerlerinde if/else yok.
// Çağıran: SnowSettings, SnowManager, SnowClipmap, SnowfallController.

using UnityEngine;

public enum SnowQualityPreset
{
    Low,
    Medium,
    High,
}

/// Bir kalite seviyesinin bütün ölçüleri. `readonly struct` — kopya ucuz,
/// yanlışlıkla değiştirilemez.
public readonly struct SnowQualityData
{
    /// Durum dokularının çözünürlüğü, teksel.
    public readonly int Resolution;

    /// Deformasyon bölgesinin kenar uzunluğu, metre. Üç presette de 16 (spec §6.1).
    public readonly float AreaSize;

    /// Clipmap halka sayısı.
    public readonly int RingCount;

    /// En içteki halkanın grid boyutu.
    public readonly int Ring0Grid;

    /// Gökyüzü görünürlük haritasının çözünürlüğü.
    public readonly int SkyResolution;

    /// Detay normal katmanı sayısı.
    public readonly int DetailLayers;

    /// Parıltı LOD sayısı. 0 = kapalı.
    public readonly int SparkleLods;

    /// KAccumulate'in kaç karede bir tam turu tamamladığı.
    public readonly int AccumulateTiles;

    /// VFX kapasite çarpanı.
    public readonly float VfxCapacityScale;

    /// İz içi AO yön sayısı. 0 = kapalı (spec §18.5).
    public readonly int AoDirs;

    /// İz içi AO yön başına adım sayısı.
    public readonly int AoSteps;

    /// Shader keyword'ü.
    public readonly string Keyword;

    public SnowQualityData(int resolution, float areaSize, int ringCount, int ring0Grid,
        int skyResolution, int detailLayers, int sparkleLods, int accumulateTiles,
        float vfxCapacityScale, int aoDirs, int aoSteps, string keyword)
    {
        Resolution = resolution;
        AreaSize = areaSize;
        RingCount = ringCount;
        Ring0Grid = ring0Grid;
        SkyResolution = skyResolution;
        DetailLayers = detailLayers;
        SparkleLods = sparkleLods;
        AccumulateTiles = accumulateTiles;
        VfxCapacityScale = vfxCapacityScale;
        AoDirs = aoDirs;
        AoSteps = aoSteps;
        Keyword = keyword;
    }
}

public static class SnowQuality
{
    public const string KeywordLow = "_SNOW_QUALITY_LOW";
    public const string KeywordMedium = "_SNOW_QUALITY_MEDIUM";
    public const string KeywordHigh = "_SNOW_QUALITY_HIGH";

    /// Spec §15.3 tablosu birebir. Alan boyu üç presette de 16 m (§6.1).
    public static SnowQualityData Get(SnowQualityPreset preset)
    {
        switch (preset)
        {
            case SnowQualityPreset.Low:
                return new SnowQualityData(512, 16f, 3, 240, 512, 1, 0, 8, 0.35f, 0, 0, KeywordLow);

            case SnowQualityPreset.High:
                return new SnowQualityData(1536, 16f, 4, 480, 1024, 4, 2, 4, 1f, 8, 3, KeywordHigh);

            default:
                return new SnowQualityData(1024, 16f, 4, 400, 1024, 2, 1, 4, 0.65f, 4, 2, KeywordMedium);
        }
    }
}
