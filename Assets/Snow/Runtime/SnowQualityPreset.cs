// ROL: kalite seviyeleri ve her seviyenin sayıları (§11.4).
// Çağıran: SnowManager (RT boyutu, kernel dağıtımı), sonraki fazlarda SnowClipmap ve VFX.

using UnityEngine;

public enum SnowQualityPreset
{
    Low,
    Medium,
    High,
}

/// Tek bir kalite seviyesinin sayıları. §11.4 tablosunun birebir karşılığı.
public readonly struct SnowQualityData
{
    /// Durum dokusunun kenar uzunluğu, teksel.
    public readonly int Resolution;

    /// Bölgenin dünya kenar uzunluğu, metre.
    public readonly float AreaSize;

    /// Geometry clipmap halka sayısı (Faz 4).
    public readonly int RingCount;

    /// Halka 0'ın quad ızgarası (Faz 4).
    public readonly int Ring0Grid;

    /// Aynı anda işlenen azami deformer sayısı (Faz 5).
    public readonly int MaxDeformers;

    /// Gökyüzü görünürlüğü haritasının çözünürlüğü (Faz 2).
    public readonly int OcclusionResolution;

    /// VFX kapasite çarpanı (Faz 8).
    public readonly float VfxCapacityScale;

    /// KAccumulate'in dokuyu böldüğü şerit sayısı (§11.1 tile rotasyonu).
    public readonly int AccumulateTiles;

    /// Shader keyword'ü (§8.5).
    public readonly string Keyword;

    public SnowQualityData(int resolution, float areaSize, int ringCount, int ring0Grid,
                           int maxDeformers, int occlusionResolution, float vfxCapacityScale,
                           int accumulateTiles, string keyword)
    {
        Resolution = resolution;
        AreaSize = areaSize;
        RingCount = ringCount;
        Ring0Grid = ring0Grid;
        MaxDeformers = maxDeformers;
        OcclusionResolution = occlusionResolution;
        VfxCapacityScale = vfxCapacityScale;
        AccumulateTiles = accumulateTiles;
        Keyword = keyword;
    }

    /// Bir tekselin dünya boyu, metre.
    public float TexelSize => AreaSize / Resolution;
}

public static class SnowQuality
{
    public const string KeywordLow = "_SNOW_QUALITY_LOW";
    public const string KeywordMedium = "_SNOW_QUALITY_MEDIUM";
    public const string KeywordHigh = "_SNOW_QUALITY_HIGH";

    static readonly SnowQualityData Low =
        new SnowQualityData(1024, 24f, 3, 160, 12, 512, 0.35f, 8, KeywordLow);

    static readonly SnowQualityData Medium =
        new SnowQualityData(1536, 24f, 4, 200, 24, 768, 0.65f, 4, KeywordMedium);

    static readonly SnowQualityData High =
        new SnowQualityData(2048, 24f, 4, 240, 32, 1024, 1.0f, 4, KeywordHigh);

    public static SnowQualityData Get(SnowQualityPreset preset)
    {
        switch (preset)
        {
            case SnowQualityPreset.Low: return Low;
            case SnowQualityPreset.Medium: return Medium;
            case SnowQualityPreset.High: return High;
        }

        // Enum'a yeni bir değer eklenip burası unutulursa sessizce High'a düşmek
        // yanlış çözünürlükle çalışmak demek. Açıkça fırlat.
        throw new System.ArgumentOutOfRangeException(nameof(preset), preset, "Tanımsız kar kalite seviyesi.");
    }

    /// Materyal keyword'lerini seviyeye göre aç/kapa (§8.5).
    public static void ApplyKeywords(SnowQualityPreset preset)
    {
        SnowQualityData data = Get(preset);

        Shader.DisableKeyword(KeywordLow);
        Shader.DisableKeyword(KeywordMedium);
        Shader.DisableKeyword(KeywordHigh);
        Shader.EnableKeyword(data.Keyword);
    }
}
