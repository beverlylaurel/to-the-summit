// ROL: Kar sisteminin dışarıya bildirdiği durum. Salt okunur.
// Mevcut hava/ses/gameplay sistemleri bunları okuyabilir.
// Çağıran: SnowfallController ve SnowManager yazar; dışarısı yalnız okur.

/// YAYINLAR, UYGULAMAZ (spec §3.3).
///
/// Kar sistemi `Stormness01`'i kullanıp sisi, güneş şiddetini veya ambient'i
/// DEĞİŞTİRMEZ. Sadece yayınlar. Mevcut sis sistemi isterse okuyup kendi
/// kararını verir. Kar sistemi içinde `RenderSettings`, `VolumeProfile` veya
/// `Light.intensity` yazan tek bir satır olmayacak.
public static class SnowRuntimeState
{
    /// 0..1 aktif kar şiddeti.
    public static float SnowfallIntensity01 { get; internal set; }

    /// 0..1 zeminde ne kadar kar var.
    public static float GroundCoverage01 { get; internal set; }

    /// 0..1 savrulabilir gevşek kar.
    public static float LooseSnowFraction { get; internal set; }

    /// 0..1 rüzgâr × yağış.
    public static float Stormness01 { get; internal set; }

    /// Kar yağıyor mu (spec §3.4).
    public static bool IsSnowing { get; internal set; }

    /// YAĞMURUN AĞIRLIĞI. 1 = yağmur tam güçte, 0 = susturuldu.
    ///
    /// Kar başlayınca 1'den 0'a rampa iniyor; kar şiddeti ancak bu SIFIRA
    /// ULAŞTIKTAN sonra yükselmeye başlıyor. İkisi asla aynı anda görünmüyor
    /// — çapraz soldurma yumuşak geçiş değil, iki yağışın üst üste
    /// binmesidir (`DECISIONS.md`).
    public static float RainWeight01 { get; internal set; } = 1f;

    /// Oyun kapanırken veya sistem devre dışı kalırken sıfırlanıyor — statik
    /// alanlar Play oturumları arasında yaşıyor ve bayat değer okutuyor.
    internal static void Reset()
    {
        SnowfallIntensity01 = 0f;
        GroundCoverage01 = 0f;
        LooseSnowFraction = 0f;
        Stormness01 = 0f;
        IsSnowing = false;
        RainWeight01 = 1f;
    }
}
