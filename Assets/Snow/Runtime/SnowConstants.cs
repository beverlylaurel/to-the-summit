// ROL: kar sisteminin fiziksel sabitleri, C# tarafı.
// HLSL ikizi Shaders/SnowConstants.hlsl. Paylaşılan değerler `SharedWithHlsl`
// tablosunda; SnowDebugWindow o tabloyu .hlsl dosyasındaki #define'larla karşılaştırır.

/// Kar sisteminin fiziksel sabitleri. Sanatsal ayar buraya girmez — §14: sanatsal
/// olan `SnowSettings`, fiziksel olan burada.
public static class SnowConstants
{
    // --- HLSL ile paylaşılan (§1, §2.1, §5.3) ---

    public const float RhoMin = 50f;
    public const float RhoMax = 550f;
    public const float RhoWater = 1000f;

    public const float SigmaRef = 4000f;
    public const float RhoRef = 100f;
    public const float BearingN = 3.2f;

    public const float SweMax = 0.60f;

    /// Kuru kar duruş açısı 38 derece. tan(38) = 0.781.
    public const float ReposeTan = 0.781f;

    /// Sabit noktalı atomik kütle toplama ölçeği, 2^22 (§5.4).
    public const float MassFixedScale = 4194304f;

    /// Halka ağırlığı toplama ölçeği (§5.5).
    public const float RingSumScale = 65536f;

    /// Karin oturma zaman sabiti, saniye (6 saat).
    public const float SettleTau = 21600f;

    /// Tazelik sonum zaman sabiti, saniye (15 dakika).
    public const float DisturbTau = 900f;

    /// Islaklik zaman sabiti, saniye (30 dakika).
    public const float WetTau = 1800f;

    /// Derece-gun erime katsayisi, m/(C.s). 4 mm/(C.gun) = 0.004 / 86400.
    public const float MeltDDF = 4.63e-8f;

    // --- yalnız C# tarafı ---

    /// Bölge merkezinin snap ızgarası, metre (§2.4). Snap yapılmazsa izler teksel
    /// altı kayar ve titrer; §15 bunu "en sık ve en zor bulunan hata" diye anıyor.
    public const float SnapStep = 0.25f;

    /// Gökyüzü görünürlüğü haritasının kapsadığı kare alan, metre (§4.1).
    public const float OcclusionArea = 96f;

    /// Occlusion haritası bölge merkezi bu kadar kayınca yenilenir, metre (§4.2).
    public const float OcclusionMoveThreshold = 4f;

    /// Her compute kernel'i 8x8x1 (§11.3, §14).
    public const int GroupSize = 8;

    /// SnowDeformerGPU'nun bayt boyu (§5.1). Struct düzeni değiştirilmeyecek.
    public const int DeformerStride = 64;

    /// Damga atlası dilim çözünürlüğü (§5.2).
    public const int StampAtlasSize = 128;

    /// Damga dilimi başına temas alanı oranı (§5.2 tablosu).
    public static readonly float[] StampAreaFrac = { 0.785f, 0.62f, 0.62f, 0.78f, 0.90f, 0.70f };

    /// HLSL'deki #define ile birebir aynı olması gereken değerler.
    /// SnowDebugWindow bu tabloyu dosyadan okuduğu #define'larla karşılaştırır.
    public readonly struct SharedConstant
    {
        public readonly string Define;
        public readonly float Value;

        public SharedConstant(string define, float value)
        {
            Define = define;
            Value = value;
        }
    }

    public static readonly SharedConstant[] SharedWithHlsl =
    {
        new SharedConstant("SNOW_RHO_MIN", RhoMin),
        new SharedConstant("SNOW_RHO_MAX", RhoMax),
        new SharedConstant("SNOW_RHO_WATER", RhoWater),
        new SharedConstant("SNOW_SIGMA_REF", SigmaRef),
        new SharedConstant("SNOW_RHO_REF", RhoRef),
        new SharedConstant("SNOW_BEARING_N", BearingN),
        new SharedConstant("SNOW_SWE_MAX", SweMax),
        new SharedConstant("SNOW_REPOSE_TAN", ReposeTan),
        new SharedConstant("SNOW_MASS_FIXED_SCALE", MassFixedScale),
        new SharedConstant("SNOW_RING_SUM_SCALE", RingSumScale),
        new SharedConstant("SNOW_SETTLE_TAU", SettleTau),
        new SharedConstant("SNOW_DISTURB_TAU", DisturbTau),
        new SharedConstant("SNOW_WET_TAU", WetTau),
        new SharedConstant("SNOW_MELT_DDF", MeltDDF),
    };
}
