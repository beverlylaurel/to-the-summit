// ROL: kalite seviyelerinin sayısal karşılığı (spec §15.3). Preset seçilince
// bütün ölçüler buradan okunuyor; çağrı yerlerinde if/else yok.
// Çağıran: SnowSettings, SnowManager, SnowSurface, SnowfallController.

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
    /// Durum dokularının çözünürlüğü, teksel. İKİNİN KUVVETİ (spec §6.4).
    public readonly int Resolution;

    /// Deformasyon bölgesinin kenar uzunluğu, metre. Üç presette de 24 (spec §6.1).
    public readonly float AreaSize;

    /// Kar mesh'inin kenar başına quad sayısı. İKİNİN KUVVETİ ve
    /// `MeshGrid ≤ Resolution` (spec §6.4).
    public readonly int MeshGrid;

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

    /// Tek quad'ın kenarı, metre (spec §6.4).
    public float QuadSize => AreaSize / MeshGrid;

    /// Tek teksel'in kenarı, metre.
    public float TexelSize => AreaSize / Resolution;

    /// Bölgenin oturduğu ızgara adımı, metre (spec §6.4).
    ///
    /// 2 katsayısı DOĞRULUK İÇİN GEREKLİ DEĞİL; `1 × quadSize` de geçerli.
    /// 2 katı, RT kaydırma sıklığını yarıya indirdiği için tercih edilmiş.
    /// Doğruluk `MeshGrid` ve `Resolution`'ın ikinin kuvveti olmasından geliyor.
    public float SnapStep => QuadSize * SnowConstants.SnapQuads;

    /// Bir snap adımının kaç teksele denk geldiği.
    ///
    /// TAM SAYI ARİTMETİĞİ, FLOAT DEĞİL. `SnapStep / TexelSize` sadeleşince
    /// `SnapQuads × Resolution / MeshGrid` çıkıyor; ikisi de ikinin kuvveti
    /// olduğu için bölme kalansız. Float'la hesaplanırsa 4.0078 gibi bir değer
    /// çıkıp sessizce yuvarlanır ve izler teksel altı titrer (spec §22).
    public int ScrollTexels => SnowConstants.SnapQuadsInt * (Resolution / MeshGrid);
}

public static class SnowQuality
{
    public const string KeywordLow = "_SNOW_QUALITY_LOW";
    public const string KeywordMedium = "_SNOW_QUALITY_MEDIUM";
    public const string KeywordHigh = "_SNOW_QUALITY_HIGH";

    /// Spec §6.1 ve §15.3 tabloları birebir. Alan boyu üç presette de 24 m.
    ///
    /// IZGARALAR İKİNİN KUVVETİ. Spec'in ilk hâli 255/511/1023 veriyordu ve
    /// `SnapStep / texelSize` hiçbir satırda tam sayı çıkmıyordu (ölçüldü:
    /// 4.0157 / 4.0078 / 2.0020). Tek sayı şartı çok seviyeli clipmap'in iç içe
    /// geçme gereğiydi; tek seviye çizdiğimiz için geçerli değil.
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
