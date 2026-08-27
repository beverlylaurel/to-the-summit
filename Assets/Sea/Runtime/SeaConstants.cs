// ROL: deniz sisteminin butun sabitleri, C# tarafi. `SeaConstants.hlsl` ile
// BIREBIR ayni degerleri tasiyor; esligi `SeaConstantsTest` siniyor.
// Cagiran: SeaManager, SeaSimulation, SeaBathymetry.

/// SAYININ TEK KAYNAĞI OLMAK ZORUNDA.
///
/// Kar sisteminde aynı sabit iki dosyada ayrı yazılmıştı (`MountainSurface`
/// 0.28, `SnowBuildSurfaceFrom` 0.45) ve aynı kar iki farklı parlaklıkla
/// çiziliyordu. Yorum "iki yol aynı sayıyı kullanmak zorunda" diyordu ama
/// sayı kopyalanmıştı. Burada eşliği test sınıyor.
public static class SeaConstants
{
    // --- Fizik ---

    /// Yerçekimi. [KAYNAK: Tessendorf 2004 §4.2]
    public const float G = 9.81f;

    public const float TwoPi = 6.28318530718f;

    /// Suyun kırılma indisi. [KAYNAK: Tessendorf 2004 §6.1.2]
    public const float WaterIor = 1.34f;

    /// Su hacminin toplu yansıtması. [KAYNAK: Tessendorf 2004 §7.1]
    public const float BulkReflectivity = 0.04f;

    // --- Spektrum (JONSWAP / TMA) ---

    /// Tepe keskinliği. [KAYNAK: Horvath 2015 / JONSWAP]
    public const float JonswapGamma = 3.30f;

    /// Tepe genişliği; ω tepe frekansının altında ve üstünde farklı.
    /// [KAYNAK: JONSWAP]
    public const float JonswapSigmaLo = 0.07f;
    public const float JonswapSigmaHi = 0.09f;

    /// Derin su dikliği sınırı. [KAYNAK: Michell 1893]
    public const float MichellSteepness = 0.142f;

    // --- Sığ su ve kırılma ---

    /// Sıfıra bölmeyi engelleyen taban derinliği (m). [KALİBRASYON]
    public const float MinDepth = 0.05f;

    /// Kıyı çizgisinde dalga sönümü (m). [KALİBRASYON]
    public const float ShoreFadeDepth = 0.60f;

    /// Yatay displacement'ın sığ suda söndüğü derinlik (m). [KALİBRASYON]
    public const float ChopFadeDepth = 8.00f;

    /// Kırılma derinlik indeksi, eğime bağlı alt ve üst uç.
    /// [KAYNAK: DNV 2017; Galvin 1969 / Weggel 1972]
    public const float GammaMild = 0.55f;
    public const float GammaSteep = 1.10f;

    /// Kırılmanın ürettiği köpük kazancı. [KALİBRASYON]
    public const float BreakFoamGain = 1.60f;

    // --- Köpük (Jacobian) ---

    /// Jacobian eşiği ve geçiş aralığı. [KAYNAK: Tessendorf 2004 §4.6]
    public const float FoamJThreshold = 0.55f;
    public const float FoamJRange = 0.55f;

    /// Köpüğün sönüm hızı (1/s). [KALİBRASYON]
    public const float FoamDecay = 0.28f;

    // --- FFT ve ızgara ---

    /// FFT ızgara boyutu. [KAYNAK: Tessendorf 2004 §4.4]
    public const int FftSize = 256;
    public const int FftLog2 = 8;

    /// Kademe sayısı. [KAYNAK: Tessendorf 2004 §4.4; Dupuy & Bruneton 2012]
    public const int TierCount = 3;
}
