// ROL: kalite kademeleri ve her kademenin sayilari. Spec 15.3 tablosu.
// Cagiran: SeaSimulation, SeaSurface, SeaManager.

using UnityEngine;

public enum SeaQualityPreset { Low, Medium, High }

/// SPEC 15.3 TABLOSU, TEK YERDE.
///
/// Sayilar iki yerde durursa (bir SeaSimulation'da bir SeaSurface'te) biri
/// degisip oteki kalir ve mesh ile dalga alani farkli kaliteye gider.
public static class SeaQuality
{
    public readonly struct Levels
    {
        /// FFT izgara boyutu. `SeaConstants.FftSize` bunun UST SINIRI —
        /// compute'un `numthreads` degeri oradan geliyor ve degismiyor;
        /// bu deger dokunun boyutu ve donusumun uzunlugu.
        public readonly int FftSize;
        public readonly int FftLog2;

        /// Kac kademe hesaplaniyor. Kullanilmayan kademenin agirligi sifir
        /// ve dispatch'i hic yapilmiyor.
        public readonly int TierCount;

        public readonly int RingCount;
        public readonly float FinestQuad;

        public Levels(int fftSize, int fftLog2, int tierCount,
                      int ringCount, float finestQuad)
        {
            FftSize = fftSize;
            FftLog2 = fftLog2;
            TierCount = tierCount;
            RingCount = ringCount;
            FinestQuad = finestQuad;
        }
    }

    /// | Ayar          | Low  | Medium | High |
    /// | FFT           | 128  | 256    | 256  |
    /// | Kademe        | 2    | 3      | 3    |
    /// | Halka 0 quad  | 1.0  | 0.5    | 0.25 |
    /// | Halka         | 6    | 7      | 8    |
    ///
    /// HALKA SAYISI SPEC TABLOSUNDAN SAPIYOR — ÖLÇÜLDÜ.
    ///
    /// Spec §15.3 üçü için de 5/7/7 veriyor. Bu projenin mesh'i tek ızgara
    /// (spec §10.1'in izin verdiği sapma) ve dış yarıçapı
    /// `64 · quad · (2^halka − 1)`. Tabloyu birebir uygulayınca:
    ///
    ///   Low  (1.00 m, 5 halka) → 1984 m
    ///   High (0.25 m, 7 halka) → 2032 m
    ///
    /// İkisi de ufka yetişmiyor; deniz iki kilometrede kesik bir kenarla
    /// bitiyor (spec §10.6 kontrol 6 tam bunu yakalıyor). Halka sayısı
    /// yarıçap ~4 km'de kalacak şekilde seçildi:
    ///
    ///   Low  (1.00 m, 6 halka) → 4032 m
    ///   Med  (0.50 m, 7 halka) → 4064 m
    ///   High (0.25 m, 8 halka) → 4080 m
    ///
    /// Üçgen sayısı da spec'in 180k/480k/900k'sına uymuyor: tek ızgarada
    /// üçgen sayısı yalnız HALKA sayısına bağlı, quad boyuna değil. Quad
    /// boyu neyin ne kadar yakından çözüldüğünü belirliyor.
    public static Levels Of(SeaQualityPreset preset)
    {
        switch (preset)
        {
            case SeaQualityPreset.Low:    return new Levels(128, 7, 2, 6, 1.00f);
            case SeaQualityPreset.High:   return new Levels(256, 8, 3, 8, 0.25f);
            default:                      return new Levels(256, 8, 3, 7, 0.50f);
        }
    }

    /// Mesh'in dış yarıçapı (m). Halka 0 dolu kare, her halka bir öncekinin
    /// iki katı quad taşıyor ve kenar başına `SeaMeshBuilder.QuadPerSide`
    /// quad var.
    public static float OuterRadius(SeaQualityPreset preset)
    {
        Levels l = Of(preset);
        return (SeaMeshBuilder.QuadPerSide / 2) * l.FinestQuad * ((1 << l.RingCount) - 1);
    }

    /// KEYWORD `multi_compile` ILE ESLESMEK ZORUNDA.
    ///
    /// `Shader.EnableKeyword` ile acilan bir keyword shader'da
    /// `#pragma multi_compile` olarak tanimli degilse varyant HIC
    /// derlenmiyor ve `#if defined(...)` sessizce false kaliyor. Kar
    /// sisteminde uc detay katmani tam bu yuzden hic calismamisti.
    public static string Keyword(SeaQualityPreset preset)
    {
        switch (preset)
        {
            case SeaQualityPreset.Low:  return "_SEA_QUALITY_LOW";
            case SeaQualityPreset.High: return "_SEA_QUALITY_HIGH";
            default:                    return "_SEA_QUALITY_MEDIUM";
        }
    }

    static readonly string[] AllKeywords =
    {
        "_SEA_QUALITY_LOW", "_SEA_QUALITY_MEDIUM", "_SEA_QUALITY_HIGH",
    };

    /// Global keyword kurulur; digerleri kapatilir.
    public static void Apply(SeaQualityPreset preset)
    {
        string wanted = Keyword(preset);

        foreach (string k in AllKeywords)
        {
            if (k == wanted) Shader.EnableKeyword(k);
            else Shader.DisableKeyword(k);
        }
    }
}
